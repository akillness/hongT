// EnvironmentBuilder contract (docs/SIM_SPEC_ENVIRONMENT.md AMENDMENT #12 §E8).
// All seven rows are EditMode-deterministic: Build → inspect → DestroyImmediate,
// no play mode, no scene assets, no rendering (StageDressingTests grammar).
//
// §E8 row map:
//   1 determinism      → Build_IsDeterministic_PerStage
//   2 ellipse fit      → Modules_RespectTheEllipseStopLine + StopConstants_DeriveFromSimConfigMargins
//   3 hazard clearance → GroundModules_ClearEveryStageHazard
//   4 coverage gate    → CameraGroundGrid_LeavesNoRegressingBareVoidFloor
//                        (Main 2026-08-07: the original frustum RAYCAST contract is
//                        void — the baked VoidFloor quad guarantees every ground ray
//                        hits SOMETHING. Replaced by a 32×32 ground-grid coverage
//                        classification: [env XZ footprint ∪ terrain plate ∪ beyond
//                        fogStart] vs bare VoidFloor, ratio pinned against a literal.)
//   5 collider zero    → EnvironmentRoot_CarriesNoColliders
//   6 budget           → Budget_VerticesMaterialsAndLights
//   7 sim immutability → owned by DungeonGoldenDigestTests (NOT duplicated here).
//
// Ring-slot facts baked into the assertions (EnvBuilder lane sync, 2026-08-07):
//   - ring gaps are LEGAL (breach rule skips slots near vents/ash-wall edges) —
//     no contiguous-segment-count assertion anywhere;
//   - gate pivots sit exactly ON the stop line (e == EnemyStopE), so the
//     non-floor band uses e >= EnemyStopE - RingJitterTolerance, not strict >;
//   - ember-bastion adds an outer wall ring (e ≈ 1.12) — outward is always legal.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class EnvironmentBuilderTests
    {
        // The contract pins exactly these nine logical stages (§E4 palette table).
        static readonly string[] StageIds =
        {
            "cinder-span", "ember-gallery", "abyss-chancel",
            "witness-well", "echo-throne", "ash-verdict",
            "cinder-sluice", "ember-bastion", "ash-march",
        };

        // Child-name vocabulary (§E2/§E3): the prefix names the zone, and the
        // ellipse test keys off it — floor = Zone A, wall/pillar/gate = Zone B,
        // gallery/bridge/channel/torch = Zone C, light = §E6 anchor (exempt).
        static readonly string[] KnownKinds =
        {
            "floor", "wall", "pillar", "gate", "gallery",
            "bridge", "channel", "torch", "light",
        };

        // Zone B procedural relief is ±6 sim-px radial (§E5); on the minor axis
        // that is 6/258 ≈ 0.0233 in e. 0.025 covers it with float headroom while
        // still rejecting anything visually inside the combat floor.
        const float RingJitterTolerance = 0.025f;
        const float FloorEpsilon = 1e-4f;

        // ---- dungeon camera constants -----------------------------------------
        // Deliberate literal copies of CameraRig's private dungeon profile
        // (reflection is banned; these are frozen spec values §E0/§E8):
        // pitch 55°, FOV 42, crowd orbit 24.5 (worst framing tier), fog band
        // starts at orbit distance + FogStartOffset 2. Aspect 21:9 is the widest
        // supported frame (aspectWiden clamps to 1 above the 1.5 reference, so
        // the orbit distance is NOT widened there — widest ground quad).
        const float DungeonPitch = 55f;
        const float DungeonFov = 42f;
        // DERIVED from the shipping camera, never copied. This was a literal
        // 24.5f and went stale the moment the crowd tier moved to 21.5 — the
        // grid was sampling a frustum (and a fog line, via FogStartOffset)
        // that no build uses. Third instance of the same raw-constant drift
        // this session; see wiki hongt-vacuous-gate-and-cancelled-scale §7.
        const float DungeonCrowdDistance = CameraRig.DungeonCrowdDistance;
        const float FogStartOffset = 2f;
        const float WideAspect = 21f / 9f;
        const int CoverageGridSize = 32;

        // Coverage-gate pin — MEASURED, not analytic. 2026-08-08 editor run:
        // all nine stages print bare=0.0000 while plate+fog alone leaves
        // withoutEnv=0.6289..0.6563, so losing Zone C jumps the ratio to ~0.63.
        //
        // That reference moved twice on 2026-08-08 and both moves were bugs
        // in the measurement, not in the environment:
        //   0.4199  AABB footprints, stale camera D=24.5
        //   0.6045  oriented footprints, stale camera
        //   0.6289  oriented footprints, shipping camera D=21.5
        // FootprintsOf projected renderer.bounds — the world-AXIS-ALIGNED box —
        // so every rotated piece claimed a square the size of its diagonal,
        // including the plate itself. It now transforms the mesh's local box
        // corners, which is exact for the box and quad modules that make up
        // almost the whole environment.
        //
        // What this gate does NOT measure: flatness. bare=0.0000 says no
        // ground sample is uncovered; the deployed frame can still read as one
        // colour because covered ground is untextured or fog-replaced. Those
        // are separate properties and this row only owns the first.
        // Pinned at 0.02 (measured 0 + slack for benign jitter).
        const float BareRatioRegressionGate = 0.02f;

        // ------------------------------------------------------------ helpers --

        static GameObject BuildOrFail(string stageId)
        {
            var root = EnvironmentBuilder.Build(stageId);
            Assert.That(root, Is.Not.Null, $"Build({stageId}) returned null");
            Assert.That(root.name, Is.EqualTo("StageEnvironment"),
                $"{stageId}: root must be named StageEnvironment");
            return root;
        }

        static void Destroy(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        /// <summary>env-&lt;kind&gt;-… → kind, or null when outside the vocabulary.</summary>
        static string KindOf(string childName)
        {
            const string prefix = "env-";
            if (childName == null || !childName.StartsWith(prefix)) return null;
            var rest = childName.Substring(prefix.Length);
            var dash = rest.IndexOf('-');
            var kind = dash < 0 ? rest : rest.Substring(0, dash);
            return System.Array.IndexOf(KnownKinds, kind) >= 0 ? kind : null;
        }

        /// <summary>World position → sim coords (inverse of ViewWorld.ToWorld).</summary>
        static Vector2 ToSim(Vector3 world)
            => new Vector2(world.x / ViewWorld.Scale, -world.z / ViewWorld.Scale);

        /// <summary>Ellipse parameter e(x,y) per §E3, from SimConfig geometry.</summary>
        static float EllipseE(Vector2 sim)
        {
            var nx = (sim.x - SimConfig.ArenaX) / SimConfig.ArenaHalfWidth;
            var ny = (sim.y - SimConfig.ArenaY) / SimConfig.ArenaHalfHeight;
            return Mathf.Sqrt(nx * nx + ny * ny);
        }

        /// <summary>Stage hazards: catalog override, else the frozen sim anchor
        /// table the stage actually runs with (StageDressingTests grammar).</summary>
        static HazardConfig[] HazardsFor(string stageId)
        {
            Assert.That(StageCatalog.TryGet(stageId, out var entry), Is.True,
                $"unknown stage id {stageId}");
            if (entry.HazardOverride != null) return entry.HazardOverride;
            Assert.That(entry.SimAnchorId, Is.EqualTo(entry.Id),
                $"{stageId} without an override must be its own sim anchor");
            Assert.That(CampaignStages.TryGet(stageId, 0, 0, 0, out var config),
                Is.True, stageId);
            return config.Hazards;
        }

        // ------------------------------------------------ 1. determinism (§E5) --

        static void Snapshot(Transform node, int depth, List<string> into)
        {
            foreach (Transform child in node)
            {
                // "R" round-trips the exact float bits — byte-identical contract.
                into.Add(
                    depth + "|" + child.name
                    + "|" + Row(child.localPosition)
                    + "|" + child.localRotation.x.ToString("R")
                    + "," + child.localRotation.y.ToString("R")
                    + "," + child.localRotation.z.ToString("R")
                    + "," + child.localRotation.w.ToString("R")
                    + "|" + Row(child.localScale));
                Snapshot(child, depth + 1, into);
            }
        }

        static string Row(Vector3 v)
            => v.x.ToString("R") + "," + v.y.ToString("R") + "," + v.z.ToString("R");

        [Test]
        public void Build_IsDeterministic_PerStage()
        {
            foreach (var stageId in StageIds)
            {
                GameObject first = null, second = null;
                try
                {
                    first = BuildOrFail(stageId);
                    second = BuildOrFail(stageId);
                    var a = new List<string>();
                    var b = new List<string>();
                    Snapshot(first.transform, 0, a);
                    Snapshot(second.transform, 0, b);
                    Assert.That(a, Is.Not.Empty, $"{stageId}: environment is empty");
                    Assert.That(b, Is.EqualTo(a),
                        $"{stageId}: two builds must produce an identical " +
                        "(name, position, rotation, scale) descendant sequence");
                }
                finally
                {
                    Destroy(first);
                    Destroy(second);
                }
            }
        }

        [Test]
        public void Build_UnknownStage_ReturnsNullWithoutThrowing()
        {
            Assert.That(EnvironmentBuilder.Build("no-such-stage"), Is.Null);
            Assert.That(EnvironmentBuilder.Build(null), Is.Null);
        }

        // ------------------------------------------- 2. ellipse stop line (§E3) --

        [Test]
        public void StopConstants_DeriveFromSimConfigMargins()
        {
            // Guards against a hand-typed 0.98/0.954: the property must EQUAL the
            // margin-derived quotient, so a SimConfig margin change moves the ring.
            Assert.That(EnvironmentBuilder.EnemyStopE, Is.EqualTo(
                (SimConfig.ArenaHalfWidth - SimConfig.EnemyMarginClamp)
                / SimConfig.ArenaHalfWidth).Within(1e-6f),
                "EnemyStopE must derive from SimConfig.EnemyMarginClamp");
            Assert.That(EnvironmentBuilder.PlayerStopE, Is.EqualTo(
                (SimConfig.ArenaHalfWidth - SimConfig.PlayerMarginClamp)
                / SimConfig.ArenaHalfWidth).Within(1e-6f),
                "PlayerStopE must derive from SimConfig.PlayerMarginClamp");
            Assert.That(EnvironmentBuilder.PlayerStopE,
                Is.LessThan(EnvironmentBuilder.EnemyStopE),
                "player stop line sits inside the enemy stop line");
        }

        [Test]
        public void Modules_RespectTheEllipseStopLine()
        {
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    foreach (Transform child in root.transform)
                    {
                        var kind = KindOf(child.name);
                        if (kind == null || kind == "light") continue; // named elsewhere; lights exempt
                        var e = EllipseE(ToSim(child.position));
                        if (kind == "floor")
                            Assert.That(e,
                                Is.LessThanOrEqualTo(EnvironmentBuilder.EnemyStopE + FloorEpsilon),
                                $"{stageId}: {child.name} (e={e:F4}) — a floor panel past the " +
                                "enemy stop line reads as walkable ground that is not");
                        else
                            Assert.That(e,
                                Is.GreaterThanOrEqualTo(
                                    EnvironmentBuilder.EnemyStopE - RingJitterTolerance),
                                $"{stageId}: {child.name} (e={e:F4}) — Zone B/C module " +
                                "inside the combat floor is a visual obstacle (§E3 banned)");
                    }
                }
                finally
                {
                    Destroy(root);
                }
            }
        }

        // ------------------------------- 2b. gimmick furniture is NON-EMPTY --
        //
        // Every other row here is an upper bound ("no module may…") or a
        // for-each over children, so all of them pass VACUOUSLY when a pass
        // emits nothing. That is not hypothetical: the first draft of the tide
        // bank rails computed its offset from HalfW (520, the X half-width)
        // instead of HalfH (110), pushed all four rails outside the arena
        // ellipse, emitted zero modules — and the whole suite stayed green.
        //
        // So assert the floor: a stage carrying a DISC gimmick must actually
        // grow furniture around it. Band gimmicks (AshWall, TideCurrent) are
        // deliberately excluded — they have no local silhouette to frame.
        static readonly HazardKind[] DiscGimmicks =
        {
            HazardKind.EmberVent, HazardKind.ObsidianPillar,
            HazardKind.RelicAltar, HazardKind.EmberPylon,
        };

        [Test]
        public void GimmickFurniture_RingsEveryDiscHazardStage()
        {
            var covered = 0;
            var worstHalfExtent = 0f;
            var worstHeight = 0f;
            var dimmestFurniture = float.MaxValue;
            var brightestFurniture = 0f;
            var floorLum = 0f;
            var samplePos = "";
            foreach (var stageId in StageIds)
            {
                var hazards = HazardsFor(stageId);
                var discs = 0;
                for (var i = 0; i < hazards.Length; i++)
                    if (System.Array.IndexOf(DiscGimmicks, hazards[i].Kind) >= 0) discs++;
                if (discs == 0) continue;

                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    var furniture = 0;
                    foreach (Transform child in root.transform)
                    {
                        // Furniture shares the floor vocabulary at a disjoint
                        // index band (500+) so it rides the clearance contract
                        // instead of inventing a kind the gate would skip.
                        if (!child.name.StartsWith("env-floor-5", System.StringComparison.Ordinal))
                            continue;
                        // A NAME is not a rock. Materialize builds the pivot
                        // before the piece loop, and SpawnLibraryPart bails
                        // silently when the terrain library fails to load, the
                        // family pool is empty, or the clone has no renderers -
                        // each leaving a correctly-named EMPTY pivot. Requiring
                        // a renderer is what makes this row non-vacuous, and it
                        // doubles as proof the library resolves in batchmode.
                        if (child.GetComponentInChildren<Renderer>() == null) continue;
                        furniture++;

                        // Clearance is measured PIVOT to pivot, so a part whose
                        // XZ silhouette is wider than FurnitureRingMargin has its
                        // inner edge inside the hazard disc while the gate stays
                        // green - and the height solve scales XZ by the same
                        // factor, so a flat library decal gets a large multiplier
                        // and can cover the very telegraph it frames (§E0.5).
                        // Measure it; a comment is not a number.
                        var rends = child.GetComponentsInChildren<Renderer>();
                        var b = rends[0].bounds;
                        for (var r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
                        var halfExtent = Mathf.Max(b.extents.x, b.extents.z);
                        if (halfExtent > worstHalfExtent) worstHalfExtent = halfExtent;
                        if (b.size.y > worstHeight) worstHeight = b.size.y;

                        // The legibility claim needs a NUMBER too. Two visual
                        // reads in this session were wrong on measurement, and
                        // a screenshot cannot say which quad is furniture. Pull
                        // the tint the renderer actually got and compare it to
                        // the floor panels it must contrast against: too close
                        // and it reads as another slab, too dark and it reads
                        // as a hole in the floor rather than a rock on it.
                        var mpb = new MaterialPropertyBlock();
                        rends[0].GetPropertyBlock(mpb);
                        var c = mpb.GetColor("_BaseColor");
                        var lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                        if (lum > 0f && lum < dimmestFurniture) dimmestFurniture = lum;
                        if (lum > brightestFurniture) brightestFurniture = lum;
                    }
                    // Floor reference from the SAME scene: furniture must be
                    // told apart from these, so the comparison has to be
                    // measured, not assumed from the palette constants.
                    foreach (Transform child in root.transform)
                    {
                        if (!child.name.StartsWith("env-floor-0", System.StringComparison.Ordinal))
                            continue;
                        var fr = child.GetComponentInChildren<Renderer>();
                        if (fr == null) continue;
                        var fb = new MaterialPropertyBlock();
                        fr.GetPropertyBlock(fb);
                        var fc = fb.GetColor("_BaseColor");
                        floorLum = fc.r * 0.299f + fc.g * 0.587f + fc.b * 0.114f;
                        break;
                    }
                    if (samplePos.Length == 0)
                    {
                        foreach (Transform child in root.transform)
                        {
                            if (!child.name.StartsWith("env-floor-5", System.StringComparison.Ordinal))
                                continue;
                            // Sim coords so a screenshot can be cropped on the
                            // real position instead of a guess at where the
                            // ring landed.
                            var w = child.position;
                            samplePos = $"{stageId} {child.name} sim=("
                                + $"{w.x / ViewWorld.Scale:F0},{-w.z / ViewWorld.Scale:F0})";
                            break;
                        }
                    }
                    TestContext.WriteLine(
                        $"  {stageId}: {discs} disc gimmick(s) -> {furniture} furniture");
                    Assert.That(furniture, Is.GreaterThan(0),
                        $"{stageId}: {discs} disc gimmick(s) but zero furniture "
                        + "modules - the pass emitted nothing and every other "
                        + "gate passed vacuously");
                }
                finally { if (root != null) Object.DestroyImmediate(root); }
                covered++;
            }
            Assert.That(covered, Is.GreaterThan(0),
                "no stage carries a disc gimmick - this test itself went vacuous");
            TestContext.WriteLine(
                $"furniture tint: dimmest={dimmestFurniture:F3} brightest="
                + $"{brightestFurniture:F3} vs floor={floorLum:F3} "
                + $"(ratio {(floorLum > 0 ? floorLum / dimmestFurniture : 0):F2}x) "
                + $"| {samplePos}");
            TestContext.WriteLine(
                $"furniture: worst xz half-extent={worstHalfExtent:F3}u  "
                + $"worst height={worstHeight:F3}u  "
                + $"budget={EnvironmentLayout.FurnitureMaxHalfExtent:F3}u");
            // A printed number nobody checks is how the 12px margin survived a
            // 70px silhouette. Pin it to the SHIPPING constant so widening the
            // ring without widening the cap (or vice versa) fails here instead
            // of quietly parking rocks back on top of the damage discs.
            Assert.That(worstHalfExtent,
                Is.LessThanOrEqualTo(EnvironmentLayout.FurnitureMaxHalfExtent + 1e-3f),
                "furniture silhouette exceeds the ring margin - its inner edge is "
                + "inside the hazard clearance the pivot test cannot see");
            Assert.That(worstHeight,
                Is.LessThanOrEqualTo(EnvironmentLayout.FurnitureMaxHeight + 1e-3f),
                "furniture taller than the occlusion cap - it can hide the ground "
                + "telegraph behind it at the 55° dungeon pitch (§E0.5)");
        }

        // ------------------------- 2c. outer silhouettes actually stand up --
        //
        // The pass above proved a shape exists; this one proves it is TALL.
        // Both matter separately, and the second is the one that silently
        // failed: every LibraryPart piece routes through SpawnLibraryPart,
        // which clamped height to FurnitureMaxHeight unconditionally, so the
        // first draft of this ring rendered as 0.425 u chips while the code
        // comment claimed "height is UNCAPPED here". Nothing caught it — the
        // furniture gate only counts env-floor-5*, and these are env-pillar-7*.
        //
        // So assert the opposite of the furniture rule: out here a silhouette
        // MUST exceed the cap, because exceeding it is the entire point.
        [Test]
        public void OuterSilhouettes_StandTallerThanTheOcclusionCap()
        {
            var covered = 0;
            var tallest = 0f;
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    var found = 0;
                    var overCap = 0;
                    foreach (Transform child in root.transform)
                    {
                        if (!child.name.StartsWith("env-pillar-7", System.StringComparison.Ordinal))
                            continue;
                        var rends = child.GetComponentsInChildren<Renderer>();
                        if (rends.Length == 0) continue;
                        found++;
                        var b = rends[0].bounds;
                        for (var r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
                        if (b.size.y > tallest) tallest = b.size.y;
                        if (b.size.y > EnvironmentLayout.FurnitureMaxHeight * 1.5f) overCap++;
                    }
                    Assert.That(found, Is.GreaterThan(0),
                        $"{stageId}: no outer silhouettes were built");
                    Assert.That(overCap, Is.GreaterThan(0),
                        $"{stageId}: {found} silhouette(s) built but none clears "
                        + $"1.5x the occlusion cap ({EnvironmentLayout.FurnitureMaxHeight:F3}u) "
                        + "- the cap is being applied where it must not be, so the "
                        + "ring is flat chips instead of silhouette");
                    covered++;
                }
                finally { if (root != null) Object.DestroyImmediate(root); }
            }
            TestContext.WriteLine(
                $"outer silhouettes: tallest={tallest:F3}u vs cap="
                + $"{EnvironmentLayout.FurnitureMaxHeight:F3}u on {covered} stage(s)");
            Assert.That(covered, Is.EqualTo(StageIds.Length));
        }

        // ------------------------------------------ 3. hazard clearance (§E3) --

        // Ground-level kinds share the fight plane with sim gimmicks; elevated
        // Zone C decks (gallery/bridge) and the sunken channel cannot collide
        // with a telegraph disc, and lights are points on other modules.
        static readonly string[] GroundKinds = { "floor", "wall", "pillar", "gate", "torch" };

        [Test]
        public void GroundModules_ClearEveryStageHazard()
        {
            foreach (var stageId in StageIds)
            {
                var hazards = HazardsFor(stageId);
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    foreach (Transform child in root.transform)
                    {
                        var kind = KindOf(child.name);
                        if (System.Array.IndexOf(GroundKinds, kind) < 0) continue;
                        var sim = ToSim(child.position);
                        foreach (var hazard in hazards)
                        {
                            var deltaX = sim.x - hazard.X;
                            var deltaY = sim.y - hazard.Y;
                            var distance = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
                            Assert.That(distance,
                                Is.GreaterThanOrEqualTo(
                                    hazard.Radius + StageCatalog.DressingHazardClearance),
                                $"{stageId}: {child.name} at ({sim.x:F0},{sim.y:F0}) is within " +
                                $"{hazard.Radius + StageCatalog.DressingHazardClearance} of the " +
                                $"{hazard.Kind} at ({hazard.X},{hazard.Y})");
                        }
                    }
                }
                finally
                {
                    Destroy(root);
                }
            }
        }

        // ---------------------------------------------- 4. coverage gate (§E1.5) --
        // No rendering and no raycasting: the dungeon camera is rebuilt as pure
        // geometry (PlaceOrbit grammar: rotation = Euler(pitch,0,0), position =
        // focus - rotation * forward * distance) and each of 32×32 viewport
        // samples is intersected with the y=0 ground plane analytically. A ground
        // sample is COVERED when its view depth reaches the fog band, or its XZ
        // point lies inside any terrain-plate renderer bound, or inside any
        // environment renderer bound. Everything else is bare VoidFloor. The
        // environment must (a) not regress past the pinned ratio and (b) strictly
        // reduce bare exposure versus plate+fog alone — Zone C must actually
        // stretch toward the frustum, not merely exist (Main's directive).

        readonly struct GroundSample
        {
            public readonly Vector3 Point;
            public readonly float ViewDepth;
            public GroundSample(Vector3 point, float viewDepth)
            {
                Point = point;
                ViewDepth = viewDepth;
            }
        }

        static List<GroundSample> SampleCameraGroundGrid()
        {
            var rotation = Quaternion.Euler(DungeonPitch, 0f, 0f);
            var forward = rotation * Vector3.forward;
            var position = ViewWorld.ArenaCenter - forward * DungeonCrowdDistance;
            var tanY = Mathf.Tan(DungeonFov * 0.5f * Mathf.Deg2Rad);
            var tanX = tanY * WideAspect;

            var samples = new List<GroundSample>(CoverageGridSize * CoverageGridSize);
            for (var j = 0; j < CoverageGridSize; j++)
            {
                for (var i = 0; i < CoverageGridSize; i++)
                {
                    var u = ((i + 0.5f) / CoverageGridSize) * 2f - 1f;
                    var v = ((j + 0.5f) / CoverageGridSize) * 2f - 1f;
                    var direction = rotation * new Vector3(u * tanX, v * tanY, 1f);
                    Assert.That(direction.y, Is.LessThan(0f),
                        "every dungeon viewport ray must descend to the ground plane");
                    var t = -position.y / direction.y;
                    var hit = position + direction * t;
                    // Unity linear fog attenuates by VIEW DEPTH, not radial range.
                    samples.Add(new GroundSample(hit, Vector3.Dot(hit - position, forward)));
                }
            }
            return samples;
        }

        /// <summary>
        /// XZ footprint of one renderer as an ORIENTED quad.
        ///
        /// This used to project renderer.bounds — the world-axis-aligned box —
        /// which over-counts every rotated piece: a thin wall at 45 degrees
        /// claims a square the size of its diagonal. That is why the gate read
        /// bare=0.0000 on all nine stages while a pixel histogram of the
        /// deployed frame showed the playfield was 74% one flat colour. The
        /// gate was measuring "inside SOME module's bounding box", not
        /// "covered by geometry".
        ///
        /// Taking the mesh's LOCAL bounds and transforming its four XZ corners
        /// gives the true rectangle for the box and quad modules that make up
        /// almost the whole environment. Authored library meshes are still
        /// approximated by their local box, but that box rotates with the part
        /// instead of growing to enclose it.
        /// </summary>
        readonly struct Footprint
        {
            public readonly Vector2 A, B, C, D;   // CCW in XZ
            public Footprint(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            { A = a; B = b; C = c; D = d; }

            public bool Contains(Vector2 p)
                => SameSide(A, B, p) && SameSide(B, C, p)
                && SameSide(C, D, p) && SameSide(D, A, p);

            // All four edge cross-products share a sign for an interior point.
            static bool SameSide(Vector2 e0, Vector2 e1, Vector2 p)
                => (e1.x - e0.x) * (p.y - e0.y) - (e1.y - e0.y) * (p.x - e0.x) >= 0f;
        }

        static List<Footprint> FootprintsOf(GameObject root)
        {
            var quads = new List<Footprint>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                var t = renderer.transform;
                if (mesh == null)
                {
                    // No mesh to orient (particles, lights): fall back to the
                    // world AABB rather than dropping the renderer entirely.
                    var b = renderer.bounds;
                    quads.Add(new Footprint(
                        new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z),
                        new Vector2(b.max.x, b.max.z), new Vector2(b.min.x, b.max.z)));
                    continue;
                }
                var lb = mesh.bounds;
                var e = lb.extents;
                var c = lb.center;
                // Four XZ corners of the local box, transformed into world.
                var p0 = t.TransformPoint(new Vector3(c.x - e.x, c.y, c.z - e.z));
                var p1 = t.TransformPoint(new Vector3(c.x + e.x, c.y, c.z - e.z));
                var p2 = t.TransformPoint(new Vector3(c.x + e.x, c.y, c.z + e.z));
                var p3 = t.TransformPoint(new Vector3(c.x - e.x, c.y, c.z + e.z));
                var a = new Vector2(p0.x, p0.z);
                var bb = new Vector2(p1.x, p1.z);
                var cc = new Vector2(p2.x, p2.z);
                var dd = new Vector2(p3.x, p3.z);
                // Winding depends on the transform's handedness; normalise so
                // Contains' same-sign test is valid for both.
                var cross = (bb.x - a.x) * (cc.y - a.y) - (bb.y - a.y) * (cc.x - a.x);
                quads.Add(cross >= 0f
                    ? new Footprint(a, bb, cc, dd)
                    : new Footprint(dd, cc, bb, a));
            }
            return quads;
        }

        static bool InAny(List<Footprint> quads, Vector3 point)
        {
            var p = new Vector2(point.x, point.z);
            for (var i = 0; i < quads.Count; i++)
                if (quads[i].Contains(p)) return true;
            return false;
        }

        [Test]
        public void CameraGroundGrid_LeavesNoRegressingBareVoidFloor()
        {
            var samples = SampleCameraGroundGrid();
            var fogStart = DungeonCrowdDistance + FogStartOffset;

            foreach (var stageId in StageIds)
            {
                Assert.That(StageCatalog.TryGet(stageId, out var entry), Is.True, stageId);
                GameObject plate = null, root = null;
                try
                {
                    // Terrain plate exactly as GameDirector.SetStageTerrain stands it up.
                    var prefab = Resources.Load<GameObject>("Terrain/terrain-" + entry.TerrainId);
                    Assert.That(prefab, Is.Not.Null,
                        $"{stageId}: terrain prefab {entry.TerrainId} missing");
                    plate = Object.Instantiate(prefab);
                    plate.transform.position = ViewWorld.ToWorld(768f, 512f, 0f);
                    var plateRects = FootprintsOf(plate);
                    Assert.That(plateRects, Is.Not.Empty, $"{stageId}: plate has no renderers");

                    root = BuildOrFail(stageId);
                    var envRects = FootprintsOf(root);
                    Assert.That(envRects, Is.Not.Empty,
                        $"{stageId}: environment has no renderers right after Build — " +
                        "meshes must be assigned synchronously");

                    int bareWithEnv = 0, bareWithoutEnv = 0;
                    foreach (var sample in samples)
                    {
                        if (sample.ViewDepth >= fogStart) continue;         // fog-covered
                        if (InAny(plateRects, sample.Point)) continue;      // plate-covered
                        bareWithoutEnv++;
                        if (InAny(envRects, sample.Point)) continue;        // env-covered
                        bareWithEnv++;
                    }

                    var total = (float)samples.Count;
                    var ratio = bareWithEnv / total;
                    // Durable evidence: NUnit only prints assert messages on FAILURE,
                    // so a green run would leave no measured ratio behind. WriteLine
                    // lands in the results XML <output> node every run — the pin
                    // below is re-checkable against measurement forever.
                    TestContext.WriteLine(
                        $"{stageId}: bare={ratio:F4} withoutEnv={bareWithoutEnv / total:F4}");
                    Assert.That(ratio, Is.LessThanOrEqualTo(BareRatioRegressionGate),
                        $"{stageId}: bare VoidFloor ratio {ratio:F4} " +
                        $"({bareWithEnv}/{samples.Count} samples; plate+fog alone leaves " +
                        $"{bareWithoutEnv / total:F4}) exceeds the pinned gate " +
                        $"{BareRatioRegressionGate:F4} — pin the first measured value here");
                    if (bareWithoutEnv > 0)
                        Assert.That(bareWithEnv, Is.LessThan(bareWithoutEnv),
                            $"{stageId}: the environment covers no frame area the plate/fog " +
                            "did not already cover — Zone B/C must reduce VoidFloor exposure");
                }
                finally
                {
                    Destroy(plate);
                    Destroy(root);
                }
            }
        }

        // ------------------------------------------------- 5. collider zero (§E2) --

        [Test]
        public void EnvironmentRoot_CarriesNoColliders()
        {
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty,
                        $"{stageId}: decoration must never own physics — the sim " +
                        "clamp is the only movement truth (ViewColliderStrip contract)");
                    Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty,
                        $"{stageId}: no 2D colliders either");
                }
                finally
                {
                    Destroy(root);
                }
            }
        }

        // ------------------------------------------------------- 6. budget (§E7) --

        [Test]
        public void Budget_VerticesMaterialsAndLights()
        {
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);

                    // The budget is RENDERED vertices (§E7). Two builder states
                    // are legal and must both be measured exactly:
                    //  - pre-combine: filters may SHARE small source meshes
                    //    (100 tiles × one 4-vert quad renders 400 verts) →
                    //    count per filter;
                    //  - post-StaticBatchingUtility.Combine: batched filters
                    //    all report the one combined mesh, which already holds
                    //    every instance's vertices exactly once → count that
                    //    mesh ONCE, or the sum overcounts N-fold.
                    // Renderer.isPartOfStaticBatch is the exact per-filter
                    // discriminator between the two.
                    var counted = new HashSet<Mesh>();
                    var vertices = 0;
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = filter.sharedMesh;
                        if (mesh == null) continue;
                        var renderer = filter.GetComponent<Renderer>();
                        if (renderer != null && renderer.isPartOfStaticBatch)
                        {
                            if (counted.Add(mesh)) vertices += mesh.vertexCount;
                        }
                        else vertices += mesh.vertexCount;
                    }
                    Assert.That(vertices, Is.LessThanOrEqualTo(60000),
                        $"{stageId}: environment vertex budget blown ({vertices} rendered)");

                    var materials = new HashSet<Material>();
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (material != null) materials.Add(material);
                    Assert.That(materials.Count, Is.LessThanOrEqualTo(8),
                        $"{stageId}: {materials.Count} distinct materials — batching " +
                        "ceiling is 8 (§E7 draw-call bound)");


                    // Durable evidence: NUnit only prints on FAILURE, so a green
                    // run would leave no measurement behind and "budget is fine"
                    // would be a claim instead of a number. These land in the
                    // results XML and make headroom re-checkable across runs.
                    TestContext.WriteLine(
                        $"{stageId}: verts={vertices}/60000  materials={materials.Count}/8");
                    Assert.That(root.GetComponentsInChildren<Light>(true).Length,
                        Is.LessThanOrEqualTo(4),
                        $"{stageId}: realtime light budget is 4 (§E6 WebGL forward)");
                }
                finally
                {
                    Destroy(root);
                }
            }
        }

        // ------------------------------------------------ 7. name vocabulary (§E2) --

        [Test]
        public void Children_FollowTheEnvNamingVocabulary()
        {
            Assert.That(StageCatalog.Entries.Count, Is.EqualTo(StageIds.Length),
                "catalog and environment contract must cover the same stages");
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = BuildOrFail(stageId);
                    foreach (Transform child in root.transform)
                        Assert.That(KindOf(child.name), Is.Not.Null,
                            $"{stageId}: direct child '{child.name}' must be " +
                            "env-<kind>-… with kind in {floor, wall, pillar, gate, " +
                            "gallery, bridge, channel, torch, light} — the zone " +
                            "tests key off this prefix");
                }
                finally
                {
                    Destroy(root);
                }
            }
        }
    }
}
