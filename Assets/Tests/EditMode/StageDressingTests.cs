// Dressing-table integrity contract (spec §Lane T-a):
//  - every referenced object exists as a child of the cinder-span library
//    prefab and is a -feature-*/-prop-* (slab/apron = fight floor, banned);
//  - every placement sits OUTSIDE the combat plane rectangle;
//  - every placement clears every hazard of its stage by radius + 50;
//  - tables are static data — same content on every call (determinism).
//
// v1.2 fun pass (2026-08-05): HazardsFor already reads the LIVE catalog
// override tables, so Placements_ClearEveryStageHazard re-verifies the new
// gallery/well/verdict placements and the march anchor pylon automatically.
// Arithmetic re-check against the v1.2 tables (worst pair per stage, Euclidean
// margin beyond radius+50): gallery vent(560,720)↔prop-003(620,950) +97.7 ·
// well vent(980,500)↔prop-012(1040,250) +117.1 · verdict vent(980,720)↔
// prop-021(990,940) +80.2 · march pylon(768,520)↔feature-001(700,230) +217.9
// — all clear; ViewFunPass moved zero rows (margins confirmed both ways).
// Cycle-10 (2026-08-09): abyss-chancel and echo-throne are now dressed too, so
// DressedStages covers all 8 non-self-dressed stages. Their clearance was
// measured against the frozen CampaignStages anchors before the tables were
// written (chancel worst margin +75.2 vs vent(1100,450); throne +111.8 vs
// vent(1030,480)); these tests re-derive it rather than trusting that note.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageDressingTests
    {
        // Gate: G8 — cycle-2 added the three new anchor stages; cycle-10 closed
        // the last two holes (abyss-chancel/echo-throne) once T-b shipped.
        // A table that is not listed here is a table nothing checks — the same
        // silent shape as the particle seed that was broken for its whole life.
        static readonly string[] DressedStages =
        {
            "ember-gallery", "witness-well", "ash-verdict",
            "cinder-sluice", "ember-bastion", "ash-march",
            "abyss-chancel", "echo-throne",
        };

        static GameObject LoadLibrary()
        {
            var library = Resources.Load<GameObject>(
                "Terrain/terrain-" + StageCatalog.DressingLibraryTerrainId);
            Assert.That(library, Is.Not.Null, "dressing library prefab is missing");
            return library;
        }

        static HazardConfig[] HazardsFor(string stageId)
        {
            Assert.That(StageCatalog.TryGet(stageId, out var entry), Is.True,
                $"unknown stage id {stageId}");
            if (entry.HazardOverride != null) return entry.HazardOverride;
            // Cycle-2 anchors carry no override — clearance is checked against the
            // frozen CampaignStages placement table they actually run with.
            Assert.That(entry.SimAnchorId, Is.EqualTo(entry.Id),
                $"{stageId} without an override must be its own sim anchor");
            Assert.That(CampaignStages.TryGet(stageId, 0, 0, 0, out var config), Is.True, stageId);
            return config.Hazards;
        }

        [Test]
        public void DressedStages_HaveNonEmptyDistinctTables()
        {
            var seen = new List<StageCatalog.DressingPlacement[]>();
            foreach (var stageId in DressedStages)
            {
                var table = StageCatalog.DressingFor(stageId);
                Assert.That(table, Is.Not.Null.And.Not.Empty, $"{stageId} table missing");
                foreach (var prior in seen)
                    Assert.That(table, Is.Not.SameAs(prior), "stages must not share one table");
                seen.Add(table);
            }
            // cinder-span is the ONLY undressed route left: its own terrain
            // prefab carries authored dressing, so a table would double it.
            Assert.That(StageCatalog.DressingFor("cinder-span"), Is.Null,
                "cinder-span terrain is already authored — no table expected");
        }

        [Test]
        public void Placements_ReferenceExistingLibraryFeatureOrPropChildren()
        {
            var library = LoadLibrary();
            foreach (var stageId in DressedStages)
            {
                foreach (var placement in StageCatalog.DressingFor(stageId))
                {
                    Assert.That(
                        placement.ObjectName,
                        Does.Contain("-feature-").Or.Contain("-prop-"),
                        $"{stageId}: {placement.ObjectName} must be feature/prop " +
                        "(slab/apron are the immutable fight floor)");
                    Assert.That(
                        library.transform.Find(placement.ObjectName),
                        Is.Not.Null,
                        $"{stageId}: {placement.ObjectName} not found in library prefab");
                    Assert.That(placement.Scale, Is.GreaterThan(0f),
                        $"{stageId}: {placement.ObjectName} scale must be positive");
                }
            }
        }

        [Test]
        public void Placements_StayOutsideTheCombatPlane()
        {
            foreach (var stageId in DressedStages)
            {
                foreach (var placement in StageCatalog.DressingFor(stageId))
                {
                    var inside =
                        placement.SimX > StageCatalog.DressingPlaneMinX &&
                        placement.SimX < StageCatalog.DressingPlaneMaxX &&
                        placement.SimY > StageCatalog.DressingPlaneMinY &&
                        placement.SimY < StageCatalog.DressingPlaneMaxY;
                    Assert.That(inside, Is.False,
                        $"{stageId}: {placement.ObjectName} at ({placement.SimX},{placement.SimY}) " +
                        "sits inside the combat plane");
                }
            }
        }

        [Test]
        public void Placements_ClearEveryStageHazard()
        {
            foreach (var stageId in DressedStages)
            {
                var hazards = HazardsFor(stageId);
                foreach (var placement in StageCatalog.DressingFor(stageId))
                {
                    foreach (var hazard in hazards)
                    {
                        var deltaX = placement.SimX - hazard.X;
                        var deltaY = placement.SimY - hazard.Y;
                        var distance = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        Assert.That(
                            distance,
                            Is.GreaterThanOrEqualTo(hazard.Radius + StageCatalog.DressingHazardClearance),
                            $"{stageId}: {placement.ObjectName} at ({placement.SimX},{placement.SimY}) " +
                            $"is within {hazard.Radius + StageCatalog.DressingHazardClearance} of the " +
                            $"{hazard.Kind} at ({hazard.X},{hazard.Y})");
                    }
                }
            }
        }

        [Test]
        public void Tables_AreDeterministic()
        {
            foreach (var stageId in DressedStages)
            {
                var first = StageCatalog.DressingFor(stageId);
                var second = StageCatalog.DressingFor(stageId);
                Assert.That(second, Is.SameAs(first),
                    $"{stageId}: table identity must be stable (static data, no RNG)");
                for (var i = 0; i < first.Length; i++)
                {
                    Assert.That(second[i].ObjectName, Is.EqualTo(first[i].ObjectName));
                    Assert.That(second[i].SimX, Is.EqualTo(first[i].SimX));
                    Assert.That(second[i].SimY, Is.EqualTo(first[i].SimY));
                }
            }
        }

        // A dressed stage must put something in all four quadrants of the ring.
        // Count, not appearance: comparing stages by screenshot does not work
        // here - dominance and luminance move with each stage's own palette and
        // hazard overlays, so a nine-stage pixel sweep ranks the overlays rather
        // than the dressing. Quadrant occupancy is the same question asked where
        // it is actually decidable.
        //
        // This caught ember-bastion at SW 1 while its own comment claimed every
        // closed edge; the west wall had a north battlement and no south one.
        // The floor is presence, not balance - ash-march legitimately runs 1/3/1/3
        // because its wall hazard sweeps x 248..608 and dressing there would sit
        // inside the crush lane.
        [Test]
        public void DressedStages_OccupyEveryQuadrant()
        {
            const float centreX = 768f;
            const float centreY = 604f;
            foreach (var stageId in DressedStages)
            {
                var table = StageCatalog.DressingFor(stageId);
                var quadrant = new int[4];
                foreach (var placement in table)
                {
                    var index = (placement.SimX < centreX ? 0 : 1)
                        + (placement.SimY < centreY ? 0 : 2);
                    quadrant[index]++;
                }
                TestContext.WriteLine(
                    $"{stageId}: n={table.Length} quadrants NW/NE/SW/SE = "
                    + $"{quadrant[0]}/{quadrant[1]}/{quadrant[2]}/{quadrant[3]}");
                for (var i = 0; i < 4; i++)
                {
                    Assert.That(quadrant[i], Is.GreaterThan(0),
                        $"{stageId}: quadrant {"NW NE SW SE".Split(' ')[i]} is empty "
                        + $"({quadrant[0]}/{quadrant[1]}/{quadrant[2]}/{quadrant[3]}) "
                        + "- the ring has a hole the fixed camera will show");
                }
            }
        }
    }
}
