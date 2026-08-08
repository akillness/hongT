// AMENDMENT #15 (W-MV) — VIEW side of the dungeon movement bounds.
//
// The sim half of this amendment is proved by DungeonBoundsTests. What that
// suite cannot see is the part that actually breaks the game: the boundary wall
// ring, the camera follow window and the ash-wall curtain are all VIEW geometry
// derived from half-axes, and if any of them keeps the frozen 520 × 270 while
// the sim clamps to 554 × 418, the player walks through a wall.
//
// Two rows, deliberately separate:
//
//   FROZEN reproduction — feeding the frozen half-axes back through the new
//   parameterised path must produce EXACTLY what the constants produced. This
//   is what makes the arena, prologue, training and lobby routes structurally
//   incapable of moving: they call the one-argument overloads, which forward
//   the frozen pair. Coordinates are compared exactly (Is.EqualTo on the
//   double/float), not within a tolerance — the point of the row is that the
//   arithmetic is unchanged, and a tolerance would hide a real drift.
//
//   EXPANDED geometry — the ring must follow the clamp, stay on the painted
//   plate, and leave the player inside it.
//
// EditMode only: Build → inspect → DestroyImmediate, the EnvironmentBuilderTests
// grammar. Nothing here enters play mode or renders.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DungeonBoundsViewSyncTests
    {
        const string Stage = "cinder-span";

        static readonly string[] StageIds =
        {
            "cinder-span", "ember-gallery", "abyss-chancel",
            "witness-well", "echo-throne", "ash-verdict",
            "cinder-sluice", "ember-bastion", "ash-march",
        };

        // The painted backdrop quad: SceneBuilder's CourtBackdrop is
        // SimWorld(1536) × SimWorld(1024) centred on sim (768, 512).
        const double PlateMinX = 0.0, PlateMaxX = 1536.0;
        const double PlateMinY = 0.0, PlateMaxY = 1024.0;

        static float ExpandedW => DungeonBoundsSpec.ExpandedHalfWidth;
        static float ExpandedH => DungeonBoundsSpec.ExpandedHalfHeight;

        /// <summary>How far a ring module may sit off the stop line: §E5 relief
        /// (±6 px) + alcove retreat (40 px) + half wall thickness (12 px).</summary>
        const double RingPushPx = 58.0;
        /// <summary>The same budget expressed in e on the shorter (y) axis, which
        /// is the axis where a given push costs the most e.</summary>
        static double RingPushE => RingPushPx / (ExpandedH * 0.9);

        [TearDown]
        public void RestoreFrozenLayout()
        {
            // The layout core and the camera clamp both carry the active
            // half-axes in statics; a fixture that left them expanded would
            // hand the next fixture a playfield it never asked for.
            EnvironmentLayout.Compute(Stage, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
            VfxDirector.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
        }

        // ------------------------------------------------------------ helpers --

        static List<EnvironmentLayout.Module> Layout(string stageId, float halfW, float halfH)
        {
            var modules = EnvironmentLayout.Compute(stageId, halfW, halfH);
            Assert.That(modules, Is.Not.Null, $"Compute({stageId}) returned null");
            return modules;
        }

        static double EllipseE(double x, double y, double halfW, double halfH)
        {
            var nx = (x - SimConfig.ArenaX) / halfW;
            var ny = (y - SimConfig.ArenaY) / halfH;
            return System.Math.Sqrt(nx * nx + ny * ny);
        }

        static bool IsRingKind(string name)
            => name.StartsWith("env-wall-") || name.StartsWith("env-gate-");

        // ============================ ROW 1 — frozen reproduction ==============

        [Test]
        public void FrozenHalfAxes_ReproduceTheConstantLayoutExactly()
        {
            foreach (var stageId in StageIds)
            {
                // The one-argument overload is what every shipped non-dungeon
                // call site uses; the explicit pair is what the dungeon route
                // now passes. They must be the same layout, module for module.
                var implicitFrozen = Snapshot(EnvironmentLayout.Compute(stageId));
                var explicitFrozen = Snapshot(Layout(stageId,
                    SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight));

                Assert.That(explicitFrozen.Count, Is.EqualTo(implicitFrozen.Count),
                    $"{stageId}: module COUNT moved between the implicit and explicit "
                    + "frozen paths — the parameterisation is not a no-op");
                for (var i = 0; i < implicitFrozen.Count; i++)
                    Assert.That(explicitFrozen[i], Is.EqualTo(implicitFrozen[i]),
                        $"{stageId}: module {i} moved. Expected {implicitFrozen[i]}, "
                        + $"got {explicitFrozen[i]}");
            }
        }

        [Test]
        public void FrozenHalfAxes_PinTheShippedFloorRowsAndTerraceSpans()
        {
            // The floor candidate rows and the two y-facing Zone C terraces were
            // absolute sim literals; they are now offsets scaled by the active
            // half-axes. At scale 1.0 the products must land on the ORIGINAL
            // literals, exactly — 768 + (−368) × 1.0 == 400, not 399.99999.
            var modules = Layout(Stage, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);

            var floorRowY = new SortedSet<float>();
            foreach (var module in modules)
                if (module.Name.StartsWith("env-floor-")
                    && int.TryParse(module.Name.Substring(10), out var n) && n < 500)
                    floorRowY.Add(module.SimY);
            Assert.That(floorRowY, Is.SubsetOf(new[] { 496f, 604f, 712f }),
                "the frozen floor candidate rows are y 496 / 604 / 712 — a row at any "
                + "other y means the offset table no longer reproduces the literals");

            // Terraces: the south apron spans y 1010..1570 and the north rim
            // y −70..270 at the frozen ring. Both are read off the module pivot.
            var terraceY = new List<float>();
            foreach (var module in modules)
                if (module.SimX > 770f && module.SimX < 780f
                    && (module.SimY < 200f || module.SimY > 1200f))
                    terraceY.Add(module.SimY);
            Assert.That(terraceY, Does.Contain(1290f),
                "south apron pivot must stay at sim y 1290 (span 1010..1570)");
            Assert.That(terraceY, Does.Contain(100f),
                "north rim pivot must stay at sim y 100 (span −70..270)");
        }

        [Test]
        public void FrozenHalfAxes_ReproduceTheShippedStopConstants()
        {
            // MV-3: the stop-line derivation moved into the sim. The no-argument
            // properties are the §E8 contract constants and must not have moved.
            Assert.That(EnvironmentBuilder.EnemyStopEFor(SimConfig.ArenaHalfWidth),
                Is.EqualTo(EnvironmentBuilder.EnemyStopE),
                "EnemyStopEFor(frozen) must reproduce the frozen constant");
            Assert.That(EnvironmentBuilder.PlayerStopEFor(SimConfig.ArenaHalfWidth),
                Is.EqualTo(EnvironmentBuilder.PlayerStopE),
                "PlayerStopEFor(frozen) must reproduce the frozen constant");
            Assert.That(EnvironmentBuilder.EnemyStopE, Is.EqualTo(
                    (SimConfig.ArenaHalfWidth - SimConfig.EnemyMarginClamp)
                    / SimConfig.ArenaHalfWidth),
                "EnemyStopE must still derive from SimConfig.EnemyMarginClamp");
        }

        [Test]
        public void FrozenHalfAxes_LeaveTheCameraFollowWindowWhereItWas()
        {
            var rig = new GameObject("rig-frozen").AddComponent<CameraRig>();
            try
            {
                rig.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                // Within(1e-5): the rig folds these factors in a different
                // order than this expression, and float rounding differs in
                // the last bits (observed 2.4e-7). The pin still catches any
                // real move — the smallest meaningful change is ~1e-2.
                Assert.That(CameraRig.FollowClampX, Is.EqualTo(
                        SimConfig.ArenaHalfWidth * ViewWorld.Scale * 0.55f
                        * (CameraRig.DungeonCalmDistance / 20f)).Within(1e-5f),
                    "the frozen follow clamp X moved");
                Assert.That(CameraRig.FollowClampZ, Is.EqualTo(
                        SimConfig.ArenaHalfHeight * ViewWorld.Scale * 0.75f
                        * (CameraRig.DungeonCalmDistance / 20f)).Within(1e-5f),
                    "the frozen follow clamp Z moved");

                // A SHRINK request must be refused the same way the sim refuses
                // it: pulling the clamp inside the frozen playfield would strand
                // the player outside the follow window.
                rig.SetPlayfield(10f, 10f);
                Assert.That(CameraRig.FollowClampZ, Is.EqualTo(
                        SimConfig.ArenaHalfHeight * ViewWorld.Scale * 0.75f
                        * (CameraRig.DungeonCalmDistance / 20f)),
                    "a shrink request must clamp back to the frozen half-height");
            }
            finally
            {
                // The clamp lives in statics (ClampFollow is static), so a
                // fixture that left it expanded would hand DungeonFramingAndMood
                // Tests a follow window it never set up.
                rig.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                Object.DestroyImmediate(rig.gameObject);
            }
        }

        // ============================ ROW 2 — expanded geometry ================

        [Test]
        public void ExpandedHalfAxes_MoveTheWallRingOutWithTheClamp()
        {
            // Measured module extents carry the ring's own §E5 relief (±6 px), a
            // possible alcove retreat (40 px) and the half-thickness push (12 px),
            // and the gate exclusion means no module sits exactly on an apex. So
            // the assertion is on the SHAPE the modules are laid against — the
            // stop ellipse — with the measured pivots checked against it plus the
            // documented push budget, rather than on raw min/max.
            var frozenModules = Layout(Stage, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
            var frozenMeasured = RingExtent(frozenModules);
            var expandedModules = Layout(Stage, ExpandedW, ExpandedH);
            var expandedMeasured = RingExtent(expandedModules);

            var frozenRingY = SimConfig.ArenaHalfHeight
                * EnvironmentBuilder.EnemyStopEFor(SimConfig.ArenaHalfWidth);
            var expandedRingY = ExpandedH * EnvironmentBuilder.EnemyStopEFor(ExpandedW);
            Assert.That(expandedRingY, Is.GreaterThan(frozenRingY + 100f),
                "the expanded stop ellipse barely grew — check DungeonBoundsSpec");

            // Every ring module must sit on the EXPANDED ellipse. If the layout
            // were still using the frozen half-axes, the modules would sit at
            // e ≈ 0.64 of the expanded ellipse, not at its stop line.
            var stop = EnvironmentBuilder.EnemyStopEFor(ExpandedW);
            var worst = 0.0;
            foreach (var module in expandedModules)
            {
                if (!IsRingKind(module.Name)) continue;
                var e = EllipseE(module.SimX, module.SimY, ExpandedW, ExpandedH);
                if (e > 1.05) continue;            // ember-bastion outer battlement
                Assert.That(e, Is.EqualTo(stop).Within(RingPushE),
                    $"{module.Name} sits at e={e:F4} on the EXPANDED ellipse; the stop "
                    + $"line is {stop:F4}. A module near e {frozenRingY / expandedRingY:F2} "
                    + "means the ring is still laid out against the frozen half-axes — "
                    + "the MV-2 defect (the player walks through the wall).");
                if (System.Math.Abs(e - stop) > worst) worst = System.Math.Abs(e - stop);
            }
            Assert.That(worst, Is.GreaterThan(0.0), "no ring module was inspected");

            Assert.That(expandedMeasured.maxY, Is.GreaterThan(frozenMeasured.maxY),
                "the expanded ring's far edge did not move outward at all");
            Assert.That(expandedMeasured.maxX, Is.GreaterThan(frozenMeasured.maxX),
                "the expanded ring did not widen");
        }

        [Test]
        public void ExpandedRing_StaysOnThePlateAndKeepsThePlayerInside()
        {
            // The plate constraint is on the RING SHAPE (the sim lane's "링 4변
            // 전부 플레이트 내부"); individual modules may push up to RingPushPx
            // past it, which is why the south apron now starts at the plate edge
            // rather than 14 px inside the ring.
            var stop = EnvironmentBuilder.EnemyStopEFor(ExpandedW);
            var ringMinX = SimConfig.ArenaX - ExpandedW * stop;
            var ringMaxX = SimConfig.ArenaX + ExpandedW * stop;
            var ringMinY = SimConfig.ArenaY - ExpandedH * stop;
            var ringMaxY = SimConfig.ArenaY + ExpandedH * stop;

            Assert.That(ringMinX, Is.GreaterThan(PlateMinX), "expanded ring off the plate (−x)");
            Assert.That(ringMaxX, Is.LessThan(PlateMaxX), "expanded ring off the plate (+x)");
            Assert.That(ringMinY, Is.GreaterThan(PlateMinY), "expanded ring off the plate (−y)");
            Assert.That(ringMaxY, Is.LessThan(PlateMaxY),
                "expanded ring off the plate (+y) — the backdrop ends at sim y 1024");

            // The whole point: the PLAYER's reach must land inside the ring,
            // otherwise the amendment ships the wall-clipping bug.
            var playerE = EnvironmentBuilder.PlayerStopEFor(ExpandedW);
            Assert.That(SimConfig.ArenaY + ExpandedH * playerE, Is.LessThan(ringMaxY),
                "the player can reach PAST the wall ring on y");
            Assert.That(SimConfig.ArenaX + ExpandedW * playerE, Is.LessThan(ringMaxX),
                "the player can reach PAST the wall ring on x");

            // Every stage builds a ring that respects the same shape.
            foreach (var stageId in StageIds)
            {
                var measured = RingExtent(Layout(stageId, ExpandedW, ExpandedH));
                Assert.That(measured.maxY, Is.LessThanOrEqualTo(ringMaxY + RingPushPx),
                    $"{stageId}: a ring module pushed further past the stop line than "
                    + "the §E5 relief + alcove + half-thickness budget allows");
                Assert.That(measured.minY, Is.GreaterThanOrEqualTo(ringMinY - RingPushPx),
                    $"{stageId}: same, on the near edge");
            }
        }

        [Test]
        public void ExpandedFloor_IsNotBareOutsideTheFrozenRows()
        {
            // MV-5: the shipped candidate rows only cover |dy| ≤ 108. On the
            // expanded floor that is the middle third, so without the expansion
            // rows the new space renders as untextured plate.
            var modules = Layout(Stage, ExpandedW, ExpandedH);
            var frozenRowReach = 108.0 * (ExpandedH / SimConfig.ArenaHalfHeight);

            var far = 0;
            foreach (var module in modules)
            {
                if (!module.Name.StartsWith("env-floor-")) continue;
                if (!int.TryParse(module.Name.Substring(10), out var n) || n >= 500) continue;
                if (System.Math.Abs(module.SimY - SimConfig.ArenaY) > frozenRowReach + 1.0) far++;
            }
            Assert.That(far, Is.GreaterThan(0),
                "no floor accent landed outside the (scaled) frozen rows — the "
                + "expanded band would read as bare plate");

            // …and every panel it did place is still inscribed in the stop
            // ellipse, worst-case half-extent included (§E3 "전부 e ≤ EnemyStopE 내접").
            var stop = EnvironmentBuilder.EnemyStopEFor(ExpandedW);
            foreach (var module in modules)
            {
                if (!module.Name.StartsWith("env-floor-")) continue;
                if (!int.TryParse(module.Name.Substring(10), out var n) || n >= 500) continue;
                var corner = EllipseE(module.SimX + 75.0, module.SimY, ExpandedW, ExpandedH);
                Assert.That(corner, Is.LessThanOrEqualTo(stop + 1e-6),
                    $"{module.Name} (e={corner:F4}) pokes past the expanded stop line");
            }
        }

        [Test]
        public void ExpandedRing_DoesNotCollideWithTheZoneCTerraces()
        {
            // The terraces are +0.8 u raised decks laid OUTSIDE the ring to fill
            // the frustum. The north rim spans y −70..270 at the frozen ring; an
            // expanded ring reaches y ≈ 204 and the player's own reach y ≈ 212,
            // so a rim that did not retreat would be a deck the player walks into.
            var modules = Layout(Stage, ExpandedW, ExpandedH);
            var stop = EnvironmentBuilder.EnemyStopEFor(ExpandedW);
            var ringMinY = SimConfig.ArenaY - ExpandedH * stop;
            var ringMaxY = SimConfig.ArenaY + ExpandedH * stop;
            var playerE = EnvironmentBuilder.PlayerStopEFor(ExpandedW);
            var reachMinY = SimConfig.ArenaY - ExpandedH * playerE;
            var reachMaxY = SimConfig.ArenaY + ExpandedH * playerE;

            var sawNorth = false;
            var sawSouth = false;
            foreach (var module in modules)
            {
                // The two y-facing terraces are the huge Zone C slabs pivoted on
                // the arena meridian; the x-facing wings sit at x −860 / 2380.
                if (module.SimX < 770f || module.SimX > 780f) continue;
                if (module.SimY > 300f && module.SimY < 1200f) continue;
                var halfDepth = TerraceHalfDepth(module);
                if (module.SimY < 300f)
                {
                    sawNorth = true;
                    var near = module.SimY + halfDepth;
                    Assert.That(near, Is.LessThanOrEqualTo(ringMinY),
                        $"{module.Name}: the north rim terrace ends at y {near:F1}, "
                        + $"inside the expanded ring (y {ringMinY:F1}) — a +0.8 u deck "
                        + "over the playfield");
                    Assert.That(near, Is.LessThan(reachMinY),
                        $"{module.Name}: the player (reach y {reachMinY:F1}) can walk "
                        + "into the north rim deck");
                }
                else
                {
                    sawSouth = true;
                    var near = module.SimY - halfDepth;
                    Assert.That(near, Is.GreaterThanOrEqualTo(ringMaxY),
                        $"{module.Name}: the south apron starts at y {near:F1}, "
                        + $"inside the expanded ring (y {ringMaxY:F1})");
                    Assert.That(near, Is.GreaterThan(reachMaxY),
                        $"{module.Name}: the player (reach y {reachMaxY:F1}) can walk "
                        + "onto the south apron");
                    // …and it must not retreat off the plate, or the band between
                    // the plate edge and the apron becomes bare VoidFloor — the
                    // one thing the §E8 coverage gate measures.
                    // 1e-3 sim px of float slack: the layout clamps in double
                    // (Math.Min(…, PlateBottomY)) but the module stores floats,
                    // and the roundtrip lands ~1e-5 over the edge. A real void
                    // band is measured in whole pixels by the §E8 32px grid.
                    Assert.That(near, Is.LessThanOrEqualTo(PlateMaxY + 1e-3),
                        $"{module.Name}: the apron retreated past the plate edge, "
                        + "opening a void band the coverage gate will catch");
                }
            }
            Assert.That(sawNorth && sawSouth, Is.True,
                "the north rim / south apron terraces were not found — the pivot "
                + "convention this test keys off has changed");
        }

        [Test]
        public void ExpandedHalfAxes_ScaleTheCameraFollowWindowByTheSameFraction()
        {
            // MV-4. The orbit distance stays 17.5 / 21.5 — the framing arithmetic
            // says the player never leaves the frame at that distance — but the
            // follow CLAMP is a fraction of the playfield and must stay one, or
            // the player rides the frame edge at the new clamp extremes.
            var rig = new GameObject("rig-expanded").AddComponent<CameraRig>();
            try
            {
                rig.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                var frozenReachZ = SimConfig.ArenaHalfHeight
                    * EnvironmentBuilder.PlayerStopEFor(SimConfig.ArenaHalfWidth) * ViewWorld.Scale;
                var frozenRatio = CameraRig.FollowClampZ / frozenReachZ;

                rig.SetPlayfield(ExpandedW, ExpandedH);
                var expandedReachZ = ExpandedH
                    * EnvironmentBuilder.PlayerStopEFor(ExpandedW) * ViewWorld.Scale;
                var expandedRatio = CameraRig.FollowClampZ / expandedReachZ;

                Assert.That(expandedRatio, Is.EqualTo(frozenRatio).Within(0.02f),
                    "the follow window stopped tracking the same fraction of the "
                    + "player's reach — a const clamp on an expanded floor drops it "
                    + $"from {frozenRatio:F3} to {expandedRatio:F3}");
                Assert.That(CameraRig.FollowClampZ, Is.LessThan(expandedReachZ),
                    "the clamp must stay inside the reach or the camera stops "
                    + "tracking before the player stops moving");

                // And the clamp extreme must still leave the player on screen:
                // visible ground runs 6.46 u toward the camera at D 17.5 (pitch
                // 55, FOV 42) — computed, not guessed, in the report.
                Assert.That(expandedReachZ - CameraRig.FollowClampZ, Is.LessThan(6.46f),
                    "the player can end up further from the focus than the near "
                    + "edge of the visible ground");
            }
            finally
            {
                // The clamp lives in statics (ClampFollow is static), so a
                // fixture that left it expanded would hand DungeonFramingAndMood
                // Tests a follow window it never set up.
                rig.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                Object.DestroyImmediate(rig.gameObject);
            }
        }

        [Test]
        public void AshWallCurtain_SpansTheActivePlayfieldNotTheFrozenOne()
        {
            // MV-6. The ash wall is a full-height crush edge in the sim; a
            // curtain frozen at 540 sim px would stop 296 px short of the
            // expanded playfield and read as a gap that is not actually safe.
            VfxDirector.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
            var frozen = VfxDirector.WallSpanWorldForTests;
            Assert.That(frozen, Is.EqualTo(SimConfig.ArenaHalfHeight * 2f * ViewWorld.Scale),
                "the frozen ash-wall span moved");

            VfxDirector.SetPlayfield(ExpandedW, ExpandedH);
            Assert.That(VfxDirector.WallSpanWorldForTests,
                Is.EqualTo(ExpandedH * 2f * ViewWorld.Scale),
                "the ash-wall curtain did not follow the expanded playfield");

            VfxDirector.SetPlayfield(10f, 10f);
            Assert.That(VfxDirector.WallSpanWorldForTests, Is.EqualTo(frozen),
                "a shrink request must clamp back to the frozen span");
        }

        [Test]
        public void TheViewAndTheSimResolveTheSameHalfAxes()
        {
            // The one invariant the whole amendment rests on. The environment is
            // built BEFORE the sim exists, so the two sides cannot be compared at
            // runtime — they are kept equal by both running the sim's resolver.
            GameView.DungeonPlayfield(out var viewW, out var viewH);
            DungeonBoundsSpec.Resolve(GameView.DungeonProgression.Bounds,
                out var simW, out var simH);
            Assert.That(viewW, Is.EqualTo(simW), "view/sim half-WIDTH disagree");
            Assert.That(viewH, Is.EqualTo(simH), "view/sim half-HEIGHT disagree");

            // And the gate is actually armed — this test is the one that fails if
            // someone reverts GameView to DungeonProgressionConfig.All.
            Assert.That(GameView.DungeonProgression.Bounds.Active, Is.True,
                "AMENDMENT #15 is not armed: GameView.DungeonProgression carries no "
                + "bounds, so the sim clamps to the frozen ellipse");
            Assert.That(GameView.DungeonProgression.AdaptiveWaves, Is.True,
                "AMENDMENT #13 (adaptive waves) went dark");
            Assert.That(GameView.DungeonProgression.GradedLoot, Is.True,
                "AMENDMENT #14 (graded loot) went dark");
        }

        // ------------------------------------------------------------ helpers --

        static (double minX, double maxX, double minY, double maxY) RingExtent(
            List<EnvironmentLayout.Module> modules)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            var seen = false;
            foreach (var module in modules)
            {
                if (!IsRingKind(module.Name)) continue;
                // ember-bastion's outer battlement ring stands at e ≈ 1.12; it is
                // decoration outside the stop line and would inflate the extent.
                if (EllipseE(module.SimX, module.SimY,
                        EnvironmentLayoutHalfW(), EnvironmentLayoutHalfH()) > 1.05) continue;
                seen = true;
                if (module.SimX < minX) minX = module.SimX;
                if (module.SimX > maxX) maxX = module.SimX;
                if (module.SimY < minY) minY = module.SimY;
                if (module.SimY > maxY) maxY = module.SimY;
            }
            Assert.That(seen, Is.True, "no boundary ring modules found");
            return (minX, maxX, minY, maxY);
        }

        static double EnvironmentLayoutHalfW() => EnvironmentLayout.HalfW;
        static double EnvironmentLayoutHalfH() => EnvironmentLayout.HalfH;

        static double TerraceHalfDepth(EnvironmentLayout.Module module)
        {
            // Deck slab piece: SizeZ is in world units; back to sim px.
            var maxZ = 0f;
            for (var i = 0; i < module.Pieces.Count; i++)
                if (module.Pieces[i].SizeZ > maxZ) maxZ = module.Pieces[i].SizeZ;
            return maxZ / ViewWorld.Scale * 0.5;
        }

        static List<string> Snapshot(List<EnvironmentLayout.Module> modules)
        {
            var rows = new List<string>(modules.Count);
            foreach (var module in modules)
                rows.Add($"{module.Name}|{module.SimX:R}|{module.SimY:R}"
                         + $"|{module.HeightWorld:R}|{module.YawDeg:R}|{module.Pieces.Count}");
            return rows;
        }
    }
}
