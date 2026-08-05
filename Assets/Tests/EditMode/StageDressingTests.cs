// Dressing-table integrity contract (spec §Lane T-a):
//  - every referenced object exists as a child of the cinder-span library
//    prefab and is a -feature-*/-prop-* (slab/apron = fight floor, banned);
//  - every placement sits OUTSIDE the combat plane rectangle;
//  - every placement clears every hazard of its stage by radius + 50;
//  - tables are static data — same content on every call (determinism).
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
        static readonly string[] DressedStages = { "ember-gallery", "witness-well", "ash-verdict" };

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
            Assert.That(entry.HazardOverride, Is.Not.Null,
                $"{stageId} must carry a hazard override (combo stage)");
            return entry.HazardOverride;
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
            // Undressed routes stay undressed until their lanes land.
            Assert.That(StageCatalog.DressingFor("cinder-span"), Is.Null,
                "cinder-span terrain is already authored — no table expected");
            Assert.That(StageCatalog.DressingFor("abyss-chancel"), Is.Null);
            Assert.That(StageCatalog.DressingFor("echo-throne"), Is.Null);
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
    }
}
