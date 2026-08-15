// §S1 skill shape vocabulary contract — silhouette, not tint.
//
// WHAT SILENTLY BREAKS WITHOUT THIS FIXTURE. Nothing throws, nothing fails to
// compile, no build error appears, and every effect still renders on screen.
// The skills simply stop being distinguishable from one another, and the AOE
// crown quietly misreports the damage edge. Before §S1, NINE events shared one
// expanding ring and differed only by colour — Dash (0.56,0.91,1.00) and Ward
// (0.56,0.85,1.00) sat 0.06 apart on a single channel with identical geometry,
// i.e. the same effect drawn twice. A regression back to that state is
// invisible to every other gate in the repo.
//
// So this fixture asserts nothing about colour. Re-reading the colour
// constants would restate the implementation and defend nothing; the property
// worth defending is GEOMETRY, and it is read back off the live LineRenderers
// the player actually sees.
//
// Three space errors are pinned here because none of them can announce itself:
//
//  1. UNIT SWAP. SpawnEruptionCrown takes `radiusSim` in SIM units (the rim
//     offset is applied BEFORE ViewWorld.ToWorld) and `riseWorld` in WORLD
//     units (added after conversion, straight up +y). The two parameters are
//     adjacent floats of the same type, so swapping them compiles clean and
//     misplaces the crown by 1/ViewWorld.Scale = 100x.
//
//  2. CIRCLE-VS-ELLIPSE. The sim judges every AOE with
//     hypot(dx, dy*SimConfig.IsoY) <= radius (CinderSim.IsoWithin), so the
//     true field is an ELLIPSE in sim space, COMPRESSED along y by the metric.
//     Dropping the `/IsoY` on the rim offset draws a plain circle that sits
//     PAST the real edge on y, promising a hit zone the sim will not honour at
//     precisely the moment the player is reading it to decide whether to move.
//
//  3. STRETCH ERASED BY NORMALISATION. SpawnShard normalises `direction`, so a
//     crack fan cannot encode its iso stretch there — the normalise throws it
//     away and off-axis arms MISS the damage edge, overshooting toward IsoY
//     times the intended radius while the on-axis pair still looks perfect.
//     SpawnCrackFan therefore solves each arm's world length PER BEARING. The
//     only symptom is off-axis arms overreaching a boundary nobody draws, so
//     it needs a test or it will be refactored back out.
//
// Every expectation below is derived from ViewWorld.Scale, SimConfig.IsoY and
// SimConfig.NovaRadius rather than from a baked world-space literal, so
// retuning the projection cannot leave this fixture asserting a stale number.
//
// A fourth invisible failure rides along, in the same spirit: every primitive
// the director builds must LOSE the Collider CreatePrimitive ships with. The
// convention is the RemovePrimitiveCollider helper, and the reason it exists is
// that a bare Object.Destroy is a NO-OP outside play mode — the collider simply
// survives and the editor logs an error nothing fails on. That is invisible to
// every gate here except a human reading console noise, which is exactly how it
// was found, so it gets an assertion too.
//
// ON CONSTRUCTION. Unity does NOT call Awake for a component added via
// AddComponent in an EditMode test — VfxDirector has no [ExecuteAlways] — so a
// bare AddComponent<VfxDirector>() leaves _novaRing, _pulseRing and the four
// ParticleSystems null and OnEvents dereferences them immediately. The fixture
// therefore invokes Awake by reflection to reach the state the running game is
// always in, and keeps driving the real public surface (OnEvents /
// ClearTransient) rather than poking the private spawners directly.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class SkillShapeVocabularyTests
    {
        /// <summary>
        /// Grave-pulse field radius in SIM units, mirroring the value OnEvents
        /// hands SpawnEruptionCrown (and the scorch beside it, `190f * 2f *
        /// ViewWorld.Scale`). The number itself is tuning; what this fixture
        /// defends is the SPACE it lives in. A rim built from the world-space
        /// figure would land at 1.9 sim units, and a rise built from the sim
        /// figure would stand 190 world units tall — both are the same typo.
        /// </summary>
        const float PulseFieldRadiusSim = 190f;

        /// <summary>Vertical reach of the pulse crown in WORLD units, as passed
        /// to `riseWorld`. Pinned because the plausible wrong answers are 190
        /// (sim units leaked through) and 1.15 * Scale (double-converted) —
        /// both of which this exact-value check rejects.</summary>
        const float PulseRiseWorld = 1.15f;

        /// <summary>Rim tolerance in SIM units. 0.5 on a 190-or-250 radius is
        /// under 0.3%, far tighter than any real defect — dropping /IsoY skews
        /// the rim by tens of sim units, a normalisation-erased stretch by
        /// dozens, and a unit swap by ~99% — yet ~12 orders of magnitude looser
        /// than the float round-trip through world space.</summary>
        const float RimToleranceSim = 0.5f;

        /// <summary>Fixed step used to age the shard pool. Small enough that
        /// every shard is sampled many times over any plausible life.</summary>
        const float StepSeconds = 0.02f;

        readonly List<GameObject> _roots = new List<GameObject>();
        readonly HashSet<GameObject> _preexisting = new HashSet<GameObject>();
        readonly Dictionary<string, object> _statics = new Dictionary<string, object>();

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            // OnEvents branches on ViewPrefs.ReducedMotion for shard COUNT.
            // Snapshot the pref (and the live value, which is cached in a
            // static) so the suite never pollutes the developer's editor.
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;
            // This fixture measures the legacy LineRenderer fallback geometry.
            // Runtime texture coverage lives in VfxRuntimeSheetTests; pin these
            // optional resources absent here so generated art landing in
            // Resources/Fx cannot silently turn this geometry test into a
            // "no Shard lines exist" failure.
            InjectTexture("_eruptionSheet", "_eruptionSheetProbed", null);
            InjectTexture("_crackFanMask", "_crackFanMaskProbed", null);
            InjectTexture("_shardStreakMask", "_shardStreakMaskProbed", null);

            // VfxDirector.Awake builds _wardShell with GameObject.CreatePrimitive
            // and never parents it, so every director leaves one extra ROOT
            // object behind that _roots cannot reach. Snapshot the scene the way
            // GameDirectorCampaignRouteTests does and sweep the difference in
            // teardown, or a sphere per director survives into later fixtures.
            _preexisting.Clear();
            foreach (var existing in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                _preexisting.Add(existing);

            // NOTE: this fixture deliberately does NOT set
            // LogAssert.ignoreFailingMessages. An earlier draft did, to tolerate
            // the "Destroy may not be called from edit mode" errors that Awake
            // and SpawnScorch used to emit. Those are gone: VfxDirector now
            // routes every primitive-collider teardown through
            // RemovePrimitiveCollider, which picks DestroyImmediate outside play
            // mode. With the cause fixed at the source, a blanket suppression
            // would only be a place for the NEXT logged regression to hide.
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _roots.Count; i++)
                if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();

            // Only ROOTS. Anything the director parented already died with the
            // line above — SpawnScorch re-parents its quad immediately, so the
            // sole survivor is Awake's _wardShell, which CreatePrimitive leaves
            // unparented and no SetParent ever claims (still true after the
            // RemovePrimitiveCollider fix: that changed collider teardown, not
            // parenting). Restricting the sweep to roots means this fixture can
            // never reach into a hierarchy a concurrent fixture owns.
            foreach (var live in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                if (live != null && live.transform.parent == null
                    && !_preexisting.Contains(live))
                    Object.DestroyImmediate(live);
            _preexisting.Clear();

            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
            foreach (var pair in _statics)
                StaticField(pair.Key).SetValue(null, pair.Value);
            _statics.Clear();
        }

        // ---------------------------------------------------------- tests --

        /// <summary>
        /// The grave-pulse crown must stand on the sim's own damage ellipse.
        /// Each rim shard is converted back out of world space into sim space
        /// and measured with the sim's own predicate — hypot(dx, dy*IsoY) —
        /// which must return the field radius for every shard. That single
        /// equality rejects both space errors at once: a radius/rise swap
        /// collapses the rim to ~1.15 sim units, and a plain circle inflates
        /// the measured radius by up to IsoY.
        /// </summary>
        [Test]
        public void PulseCrown_RimSitsOnTheSimDamageEllipse_AndRisesInWorldUnits()
        {
            const float castX = 700f, castY = 520f;
            var director = NewDirector();

            director.OnEvents(SimEvents.PulseCast,
                new SkillCastSnapshot { PlayerX = castX, PlayerY = castY });
            var crown = PeakShardGeometry(director);

            Assert.That(crown.Count, Is.GreaterThanOrEqualTo(4),
                "PulseCast must raise a crown of rim shards; fewer than 4 cannot read " +
                "as a ring at all and would make the ellipse check vacuous");

            var widestVerticalOffsetSim = 0f;
            for (var i = 0; i < crown.Count; i++)
            {
                var anchor = crown[i].Base;
                var dx = SimXOf(anchor) - castX;
                var dy = SimYOf(anchor) - castY;

                // The sim's own AOE predicate, evaluated on the drawn rim.
                var simRadius = IsoRadius(dx, dy);
                Assert.That(simRadius, Is.EqualTo(PulseFieldRadiusSim).Within(RimToleranceSim),
                    $"crown shard {i} does not sit on the sim's damage ellipse: " +
                    $"hypot(dx, dy*IsoY) = {simRadius:F2} sim units, expected " +
                    $"{PulseFieldRadiusSim}. A radius/rise unit swap lands the rim near " +
                    $"{PulseRiseWorld} sim units; dropping the /IsoY inflates it toward " +
                    $"{PulseFieldRadiusSim * SimConfig.IsoY:F0}");

                widestVerticalOffsetSim = Mathf.Max(widestVerticalOffsetSim, Mathf.Abs(dy));
            }

            // Directional anti-circle diagnostic. The ellipse is SQUASHED in y
            // by IsoY, so no rim point may reach the full radius along y. A
            // plain circle puts points out at radius*sin(angle), which breaks
            // this bound for every shard off the x axis.
            var verticalBoundSim = PulseFieldRadiusSim / SimConfig.IsoY + RimToleranceSim;
            Assert.That(widestVerticalOffsetSim, Is.LessThanOrEqualTo(verticalBoundSim),
                $"the crown reached {widestVerticalOffsetSim:F1} sim units along y, past the " +
                $"squashed ellipse bound of {verticalBoundSim:F1} — the rim has reverted to a " +
                "plain circle and now overstates the damage edge along y");

            // Rise is WORLD units, added after the conversion, straight up +y.
            for (var i = 0; i < crown.Count; i++)
            {
                var reach = crown[i].Reach;
                Assert.That(reach.y, Is.EqualTo(PulseRiseWorld).Within(1e-3f),
                    $"crown shard {i} rose {reach.y:F4} world units, expected " +
                    $"{PulseRiseWorld}. Sim units leaking into riseWorld would read " +
                    $"~{PulseFieldRadiusSim}; a double conversion ~{PulseRiseWorld * ViewWorld.Scale}");
                Assert.That(reach.y, Is.GreaterThan(HorizontalMagnitude(reach)),
                    $"crown shard {i} is wider than it is tall — a vertical eruption is the " +
                    "one silhouette the flat ring grammar cannot produce, so losing it costs " +
                    "the field its identity");
            }
        }

        /// <summary>
        /// Dash and Ward must differ in KIND. Dash rakes flat along the ground
        /// against the player's facing; Ward stands up off it. This is the
        /// assertion that fails the moment the two are collapsed back onto one
        /// flat ring — the exact state they were in when they were 0.06 apart
        /// on a single colour channel and otherwise identical.
        /// </summary>
        [Test]
        public void DashRakesFlatAgainstFacing_WhileWardStandsUp()
        {
            var dashRight = FireForShards(SimEvents.DashUsed, facing: 1);
            var dashLeft = FireForShards(SimEvents.DashUsed, facing: -1);
            var ward = FireForShards(SimEvents.WardCast, facing: 1);

            Assert.That(dashRight.Count, Is.GreaterThanOrEqualTo(2),
                "DashUsed must rake shards behind the player");
            Assert.That(ward.Count, Is.GreaterThanOrEqualTo(4),
                "WardCast must plant a shell of shards; fewer than 4 cannot read as one");

            // Dash is planar: it is spawned with rise: 0f, so its reach must
            // carry no vertical component whatsoever.
            var dashPeakVertical = 0f;
            for (var i = 0; i < dashRight.Count; i++)
            {
                var reach = dashRight[i].Reach;
                Assert.That(Mathf.Abs(reach.y), Is.LessThan(1e-4f),
                    $"dash shard {i} lifted {reach.y:F5} world units off the ground; the dash " +
                    "trail must stay flat or it stops reading as a trail");
                dashPeakVertical = Mathf.Max(dashPeakVertical, Mathf.Abs(reach.y));
            }

            // ...and it rakes BACKWARD. Driving both facings proves the rake
            // tracks Player.Facing rather than a baked constant, which a
            // single-facing test would happily accept.
            AssertAllRakeSignsMatch(dashRight, -1f, "facing +1 must throw the trail toward -x");
            AssertAllRakeSignsMatch(dashLeft, 1f, "facing -1 must throw the trail toward +x");

            // Ward is the opposite silhouette: vertical dominates horizontal.
            var wardLeastVertical = float.MaxValue;
            for (var i = 0; i < ward.Count; i++)
            {
                var reach = ward[i].Reach;
                var horizontal = HorizontalMagnitude(reach);
                Assert.That(reach.y, Is.GreaterThan(horizontal),
                    $"ward shard {i} is wider ({horizontal:F3}) than tall ({reach.y:F3}) — the " +
                    "shell must stand up, or Ward is a flat ring again");
                wardLeastVertical = Mathf.Min(wardLeastVertical, reach.y);
            }

            // The comparative claim, and the one that actually fails on a
            // collapse: the two events do not merely differ in tint, they
            // occupy different amounts of vertical space. The 0.25 world-unit
            // margin is far above float noise and far below the tuned rise, so
            // retuning intensity cannot break it but flattening Ward will.
            Assert.That(wardLeastVertical, Is.GreaterThan(dashPeakVertical + 0.25f),
                $"Ward's shortest shard ({wardLeastVertical:F3}) does not clear Dash's tallest " +
                $"({dashPeakVertical:F3}) by a readable margin. The two skills have collapsed " +
                "back onto one silhouette and are again separable only by a 0.06 colour delta");
        }

        /// <summary>
        /// The nova crack fan throws its arms to the damage EDGE, and the edge
        /// is the sim's ellipse. SpawnShard normalises `direction`, so the iso
        /// stretch cannot ride along in the direction vector — it has to be
        /// solved into each arm's world length per bearing. This test measures
        /// the drawn tips against the sim predicate, which is the only place
        /// that distinction is observable: with the stretch erased, off-axis
        /// arms drift out to IsoY times the intended radius while the on-axis
        /// pair still looks perfect.
        /// </summary>
        [Test]
        public void NovaCrackFan_ArmsSpanFromTheCastPointToTheDamageEllipse()
        {
            const float novaX = 700f, novaY = 520f;
            var director = NewDirector();

            director.OnEvents(SimEvents.NovaCast,
                new SkillCastSnapshot { NovaAtX = novaX, NovaAtY = novaY });
            var arms = PeakShardGeometry(director);

            Assert.That(arms.Count, Is.GreaterThanOrEqualTo(4),
                "NovaCast must throw a fan of crack arms; fewer than 4 cannot read as a " +
                "radial fracture and would make the bearing sweep below vacuous");

            var origin = ViewWorld.ToWorld(novaX, novaY, 0.05f);
            for (var i = 0; i < arms.Count; i++)
            {
                // A FAN radiates from one point; a CROWN stands on a rim. This
                // is what fails if the two families are ever swapped at a call
                // site, which nothing else here would notice.
                Assert.That(Vector3.Distance(arms[i].Base, origin), Is.LessThan(1e-4f),
                    $"crack arm {i} is anchored at {arms[i].Base} instead of the cast point " +
                    $"{origin} — a fan radiates from a single origin, so this arm has been " +
                    "planted on a rim like an eruption crown instead");

                var reach = arms[i].Reach;
                Assert.That(Mathf.Abs(reach.y), Is.LessThan(1e-4f),
                    $"crack arm {i} lifted {reach.y:F5} world units; cracked GROUND must stay " +
                    "flat, and lifting it would collide with the crown's vertical silhouette");

                // Convert the drawn arm back into sim space and ask the sim's
                // own question: does the tip sit on the damage edge?
                var tipRadius = IsoRadius(reach.x / ViewWorld.Scale, -reach.z / ViewWorld.Scale);
                Assert.That(tipRadius, Is.EqualTo(SimConfig.NovaRadius).Within(RimToleranceSim),
                    $"crack arm {i} reaches {tipRadius:F2} sim units, expected " +
                    $"{SimConfig.NovaRadius}. An arm whose iso stretch was erased by " +
                    $"SpawnShard's normalise drifts toward " +
                    $"{SimConfig.NovaRadius * SimConfig.IsoY:F0} off-axis; a world-unit " +
                    $"radiusSim collapses every arm to {SimConfig.NovaRadius * ViewWorld.Scale}");
            }
        }

        /// <summary>
        /// ClearTransient exists so a run cannot leak visuals into the lobby.
        /// A crack surviving the transition is exactly the failure it was
        /// written to prevent, and the shard pool is the newest thing it has
        /// to remember to release.
        /// </summary>
        [Test]
        public void ClearTransient_ReleasesEverySkillShard()
        {
            var director = NewDirector();

            director.OnEvents(
                SimEvents.PulseCast | SimEvents.WardCast | SimEvents.DashUsed | SimEvents.NovaCast,
                new SkillCastSnapshot
                {
                    PlayerX = 640f, PlayerY = 480f, NovaAtX = 640f, NovaAtY = 480f,
                });

            var shards = ShardLines(director);
            var liveBefore = 0;
            for (var i = 0; i < shards.Count; i++)
                if (shards[i].enabled) liveBefore++;

            // Without this the test would pass on a director that spawned
            // nothing at all.
            Assert.That(liveBefore, Is.GreaterThan(0),
                "the skill events must leave live shards for ClearTransient to release; " +
                "with none the cleanup assertion below proves nothing");

            director.ClearTransient();

            for (var i = 0; i < shards.Count; i++)
                Assert.That(shards[i].enabled, Is.False,
                    $"shard {i} survived ClearTransient — a leaked crack rides the run-end " +
                    "transition straight onto the lobby diorama");
        }

        /// <summary>
        /// Every primitive VfxDirector builds must lose its Collider.
        ///
        /// GameObject.CreatePrimitive ships a Collider, and none of these
        /// visuals wants one — they are decoration in a sim that owns all
        /// collision itself. The convention is the RemovePrimitiveCollider
        /// helper, which picks DestroyImmediate outside play mode because a
        /// bare Object.Destroy is a NO-OP there: the collider survives and the
        /// editor logs an error. That combination is invisible to every other
        /// gate — the game looks right, nothing throws, and the only symptom is
        /// console noise a human has to squint at. It is exactly how this was
        /// found. So the convention gets a test, or the next author reintroduces
        /// a bare Destroy for free.
        ///
        /// Coverage is every call site: Awake (the ward shell), SpawnScorch (the
        /// AOE quad), SyncPickups (gem and icon paths), and BuildHazardView
        /// (every HazardKind). The hazard builder is driven directly rather than
        /// through SyncHazards because SyncHazards also runs a per-kind ANIMATE
        /// pass over view fields only its own builder populates — feeding that
        /// synthetic state risks a NullReferenceException that would masquerade
        /// as a collider failure, and a guard that can fail for the wrong reason
        /// is worse than no guard. The builder owns every RemovePrimitiveCollider
        /// call in that file, so nothing is lost by entering there.
        /// </summary>
        [Test]
        public void EveryPrimitiveTheDirectorBuilds_LosesItsCollider()
        {
            var director = NewDirector();   // Awake: the ward shell

            // SpawnScorch's quad, reached the way the game reaches it.
            director.OnEvents(SimEvents.PulseCast | SimEvents.NovaCast,
                new SkillCastSnapshot
                {
                    PlayerX = 640f, PlayerY = 480f, NovaAtX = 640f, NovaAtY = 480f,
                });

            // SpawnPickupIcon when the sprite resolves, SpawnGem when it does
            // not — either branch builds a primitive, so both are covered
            // whichever way Resources.Load lands in this environment.
            director.SyncPickups(new[]
            {
                new PickupState
                {
                    Id = 1, Kind = PickupKind.EmberShard,
                    X = 640f, Y = 480f, Life = 5f, Bob = 0.25f,
                },
            });

            // Enum.GetValues rather than a hand-listed six, so a HazardKind
            // added later is covered without anyone remembering to come back.
            var kinds = System.Enum.GetValues(typeof(HazardKind));
            Assert.That(kinds.Length, Is.GreaterThan(0),
                "HazardKind has no members — the hazard builder sweep below would be vacuous");
            foreach (HazardKind kind in kinds)
                BuildHazardView(director, kind);

            // Both halves matter. GetComponentsInChildren would MISS two whole
            // call sites: Awake's ward shell AND SyncPickups' gem/icon are built
            // with CreatePrimitive and never parented, so they are SIBLINGS of
            // the director rather than descendants — and the ward shell is the
            // FIRST RemovePrimitiveCollider call in the file. Sweeping
            // everything new since SetUp catches orphans and descendants alike.
            var byName = new Dictionary<string, int>();
            var inspected = 0;
            var primitives = 0;
            foreach (var live in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (live == null || _preexisting.Contains(live)) continue;
                inspected++;
                // CreatePrimitive's fingerprint. VfxDirector adds a MeshFilter
                // NOWHERE else — every mesh in that file arrives via
                // CreatePrimitive — and no LineRenderer or ParticleSystem host
                // carries one, so this counts primitives and nothing else.
                if (live.GetComponent<MeshFilter>() != null)
                {
                    primitives++;
                    byName.TryGetValue(live.name, out var seen);
                    byName[live.name] = seen + 1;
                }

                var collider = live.GetComponent<Collider>();
                Assert.That(collider, Is.Null,
                    $"'{live.name}' still carries a {(collider != null ? collider.GetType().Name : "Collider")} " +
                    "— a primitive escaped RemovePrimitiveCollider. Outside play mode a bare " +
                    "Object.Destroy(collider) does nothing at all, so the collider lives on and " +
                    "the editor logs an error instead of failing anything. Route the call through " +
                    "RemovePrimitiveCollider, which picks DestroyImmediate when not playing.");
            }

            // A director that built no PRIMITIVES would sail through the loop
            // above, and counting new GameObjects cannot notice: the director
            // root, four Awake LineRenderer hosts, four particle hosts, the
            // Shard/KitBurst/HitSpark hosts and six Hazard roots (plus Edges
            // and Flow) are ~25-35 objects that never owned a collider and
            // clear any small threshold on their own. Only the MeshFilter
            // count is evidence that there was anything here to strip.
            Assert.That(primitives, Is.GreaterThanOrEqualTo(kinds.Length + 3),
                $"only {primitives} primitives exist among {inspected} new GameObjects. Awake's " +
                $"ward shell, the scorch quads, the pickup and {kinds.Length} hazard kinds should " +
                "build roughly 25 of them, so the collider sweep above judged almost nothing and " +
                "would pass just as green on a director that called CreatePrimitive not once");

            // Per-KIND, because the aggregate above is a sum. Every kind builds
            // at least two primitives (vent disc+fill, pillar+base ring, altar
            // disc+gem, current bed+edges+chevrons, pylon body/band/aura/scorch,
            // wall line/overlay/curtain), so a kind could lose every primitive
            // it owns and still clear a total carried by its siblings.
            foreach (HazardKind kind in kinds)
            {
                var rootName = $"Hazard-{kind}";
                Transform hazardRoot = null;
                foreach (var candidate in director.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == rootName) { hazardRoot = candidate; break; }

                Assert.That(hazardRoot, Is.Not.Null,
                    $"no '{rootName}' was built, so BuildHazardView returned without creating " +
                    "anything for that kind and the collider sweep judged nothing on its behalf");
                var kindPrimitives = hazardRoot.GetComponentsInChildren<MeshFilter>(true).Length;
                Assert.That(kindPrimitives, Is.GreaterThanOrEqualTo(2),
                    $"'{rootName}' owns {kindPrimitives} primitives; every kind builds at least " +
                    "two, so this one's CreatePrimitive calls have gone missing while the total " +
                    "stayed green on the other kinds");
            }

            // The named singletons, checked by name so the silent loss of any
            // one call site cannot hide inside another's count.
            foreach (var required in new[] { "WardShell", "AoeScorch" })
                Assert.That(byName.ContainsKey(required), Is.True,
                    $"no primitive named '{required}' was built, so its RemovePrimitiveCollider " +
                    $"call site went unjudged. Saw: {string.Join(", ", byName.Keys)}");
            Assert.That(byName.ContainsKey("Pickup") || byName.ContainsKey("PickupIcon"), Is.True,
                "SyncPickups built neither a 'Pickup' gem nor a 'PickupIcon' quad, so both of its " +
                $"RemovePrimitiveCollider call sites went unjudged. Saw: {string.Join(", ", byName.Keys)}");
        }

        // -------------------------------------------------------- helpers --

        /// <summary>One shard's jag-free endpoints. StepShardPool displaces
        /// interior points perpendicular to the axis but zeroes that
        /// displacement at both ends (sin(0) = sin(pi) = 0), so the first and
        /// last positions are exact and safe to measure.</summary>
        readonly struct ShardGeometry
        {
            public readonly Vector3 Base;   // t=0 — the spawn anchor
            public readonly Vector3 Tip;    // t=1 — full extension

            public ShardGeometry(Vector3 baseAt, Vector3 tip)
            {
                Base = baseAt;
                Tip = tip;
            }

            public Vector3 Reach => Tip - Base;
        }

        /// <summary>World -> sim is the exact inverse of ViewWorld.ToWorld,
        /// which maps (simX, simY) to (simX*Scale, h, -simY*Scale).</summary>
        static float SimXOf(Vector3 world) => world.x / ViewWorld.Scale;

        static float SimYOf(Vector3 world) => -world.z / ViewWorld.Scale;

        /// <summary>The sim's own AOE measure (CinderSim.IsoWithin compares this
        /// against a radius), so a drawn shape that satisfies it agrees with the
        /// hit box by construction rather than by a copied literal.</summary>
        static float IsoRadius(float dSimX, float dSimY)
            => Mathf.Sqrt(dSimX * dSimX + dSimY * dSimY * SimConfig.IsoY * SimConfig.IsoY);

        static float HorizontalMagnitude(Vector3 reach)
            => new Vector2(reach.x, reach.z).magnitude;

        VfxDirector NewDirector()
        {
            var root = new GameObject(nameof(SkillShapeVocabularyTests));
            _roots.Add(root);
            var director = root.AddComponent<VfxDirector>();
            InvokeAwake(director);
            return director;
        }

        /// <summary>
        /// Unity runs Awake on AddComponent only in play mode; VfxDirector
        /// carries no [ExecuteAlways], so in an EditMode test the director
        /// arrives with _novaRing, _pulseRing, _corpseRing, _channelBeam,
        /// _wardShell and all four ParticleSystems still null — and OnEvents
        /// dereferences _novaRing/_pulseRing unguarded on its very first line
        /// for NovaCast and PulseCast. Invoking Awake here reproduces the state
        /// the running game is always in, which is what lets these tests drive
        /// the real public entry point instead of reaching past it to the
        /// private spawners.
        /// </summary>
        static void InvokeAwake(VfxDirector director)
        {
            var awake = typeof(VfxDirector).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null,
                "VfxDirector.Awake is gone — an EditMode director cannot be brought up to the " +
                "state OnEvents requires, so this fixture needs a new construction seam");

            try
            {
                awake.Invoke(director, null);
            }
            catch (TargetInvocationException e)
            {
                // Unwrap: a bare TargetInvocationException hides which line of
                // Awake actually failed.
                Assert.Fail("VfxDirector.Awake threw while building the director in EditMode: "
                            + (e.InnerException != null ? e.InnerException.ToString() : e.ToString()));
            }
        }

        List<ShardGeometry> FireForShards(SimEvents events, int facing)
        {
            // A fresh director per event: the shard pool is shared and its
            // cursor wraps, so replaying events into one director would blend
            // two skills' geometry into one sample set.
            var director = NewDirector();
            director.OnEvents(events, new SkillCastSnapshot
            {
                PlayerX = 640f,
                PlayerY = 480f,
                PlayerFacing = facing,
            });
            return PeakShardGeometry(director);
        }

        /// <summary>
        /// Builds one hazard's visuals through VfxDirector's own private builder.
        /// Radius and Hp are set positive only so the primitive scales it derives
        /// are non-degenerate; the collider contract under test is indifferent to
        /// both. Entering at the builder rather than SyncHazards is deliberate —
        /// see EveryPrimitiveTheDirectorBuilds_LosesItsCollider.
        /// </summary>
        static void BuildHazardView(VfxDirector director, HazardKind kind)
        {
            var build = typeof(VfxDirector).GetMethod("BuildHazardView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null,
                "VfxDirector.BuildHazardView is gone — it owns most of the file's primitive " +
                "construction, so the collider guard needs a new entry point");

            var hazard = new HazardState
            {
                Kind = kind,
                X = 640f,
                Y = 480f,
                Radius = 90f,
                Hp = 100f,
                FrontX = 700f,
            };

            try
            {
                build.Invoke(director, new object[] { hazard });
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail($"VfxDirector.BuildHazardView threw for {kind}: "
                            + (e.InnerException != null ? e.InnerException.ToString() : e.ToString()));
            }
        }


        static void AssertAllRakeSignsMatch(List<ShardGeometry> shards, float expectedSign, string why)
        {
            for (var i = 0; i < shards.Count; i++)
            {
                var alongX = shards[i].Reach.x;
                Assert.That(Mathf.Abs(alongX), Is.GreaterThan(1e-3f),
                    $"dash shard {i} has no horizontal reach to rake");
                Assert.That(Mathf.Sign(alongX), Is.EqualTo(expectedSign),
                    $"dash shard {i} rakes toward {(alongX > 0f ? "+x" : "-x")}: {why}");
            }
        }

        void InjectTexture(string textureField, string probedField, Texture2D texture)
        {
            SetStatic(textureField, texture);
            SetStatic(probedField, true);
        }

        void SetStatic(string name, object value)
        {
            var field = StaticField(name);
            if (!_statics.ContainsKey(name)) _statics.Add(name, field.GetValue(null));
            field.SetValue(null, value);
        }

        static FieldInfo StaticField(string name)
        {
            var field = typeof(VfxDirector).GetField(name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"VfxDirector.{name} is gone");
            return field;
        }

        /// <summary>
        /// Ages the shard pool and returns each shard's PEAK geometry.
        ///
        /// Shards are spawned with zero-length positions — SpawnShard records
        /// centre/direction/rise but only StepShardPool writes LineRenderer
        /// vertices — so the pool must be stepped before there is anything to
        /// measure. Sampling the peak across the whole life, rather than one
        /// chosen frame, keeps the fixture independent of the per-event `life`
        /// tuning values: extension eases to 1.0 partway through any life and
        /// holds, so the peak is always the shard's full designed reach.
        /// </summary>
        static List<ShardGeometry> PeakShardGeometry(VfxDirector director)
        {
            var lines = ShardLines(director);
            var pool = ShardPool(director);
            var step = StepShardPoolMethod();

            var best = new Dictionary<LineRenderer, ShardGeometry>(lines.Count);
            var arguments = new object[] { pool, StepSeconds };

            // Hard bound so a pool that never retires cannot hang the runner;
            // the longest shard life in the kit needs a few dozen steps.
            for (var guard = 0; guard < 1000; guard++)
            {
                var anyLive = false;
                for (var i = 0; i < lines.Count; i++)
                    if (lines[i].enabled) { anyLive = true; break; }
                if (!anyLive) break;

                step.Invoke(null, arguments);

                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (!line.enabled) continue;

                    var count = line.positionCount;
                    Assert.That(count, Is.GreaterThanOrEqualTo(2),
                        $"shard line '{line.name}' has {count} positions — a shard needs at " +
                        "least two to have a silhouette");

                    var sample = new ShardGeometry(line.GetPosition(0), line.GetPosition(count - 1));
                    if (!best.TryGetValue(line, out var previous)
                        || sample.Reach.sqrMagnitude > previous.Reach.sqrMagnitude)
                        best[line] = sample;
                }
            }

            var peaks = new List<ShardGeometry>(best.Count);
            foreach (var pair in best)
                peaks.Add(pair.Value);

            Assert.That(peaks.Count, Is.GreaterThan(0),
                "no shard ever extended: the pool was never stepped, or the event raised " +
                "no shards at all");
            return peaks;
        }

        /// <summary>Shard hosts are GameObjects named "Shard" parented to the
        /// director — the only pool in VfxDirector using that name (the others
        /// are KitBurst / HitSpark / WaveWarning / NovaRing / PulseRing /
        /// CorpseRing / ChannelBeam / BoltStreak / ThreatArrow / AoeScorch and
        /// the four particle hosts), so the filter is unambiguous and survives
        /// refactors of the private pool internals.</summary>
        static List<LineRenderer> ShardLines(VfxDirector director)
        {
            var found = new List<LineRenderer>();
            foreach (var line in director.GetComponentsInChildren<LineRenderer>(true))
                if (line.gameObject.name == "Shard") found.Add(line);

            Assert.That(found.Count, Is.GreaterThan(0),
                "VfxDirector exposed no GameObject named \"Shard\" — either the skill event " +
                "raised no shards, or the §S1 shard family was renamed and this fixture's " +
                "host-name filter needs updating");
            return found;
        }

        static object ShardPool(VfxDirector director)
        {
            var field = typeof(VfxDirector).GetField("_shards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "VfxDirector._shards is gone — the §S1 shard pool was renamed or removed, so " +
                "this fixture can no longer age it");

            var pool = field.GetValue(director);
            Assert.That(pool, Is.Not.Null, "VfxDirector._shards was null");
            return pool;
        }

        static MethodInfo StepShardPoolMethod()
        {
            var method = typeof(VfxDirector).GetMethod("StepShardPool",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "VfxDirector.StepShardPool is gone — shard vertices are written nowhere else, " +
                "so this fixture cannot produce geometry to measure");
            return method;
        }

        /// <summary>
        /// Minimal ISimSnapshot stub rather than a driven CinderSim. OnEvents
        /// takes the event flags as a PARAMETER, so a real sim would only add
        /// the cost of ticking it into a casting state (charge, cooldowns,
        /// inputs) without changing a single assertion. What the geometry
        /// checks genuinely need is exact control of Player.X/Y/Facing and
        /// NovaX/Y, which a stub gives directly — and the ellipse expectations
        /// still couple to the sim through SimConfig.IsoY and
        /// SimConfig.NovaRadius, the constants CinderSim.IsoWithin judges every
        /// AOE with.
        /// </summary>
        sealed class SkillCastSnapshot : ISimSnapshot
        {
            public float PlayerX, PlayerY;
            public int PlayerFacing = 1;
            public float NovaAtX, NovaAtY;

            public SimMode Mode => default;
            public int Wave => 1;
            public int Score => 0;
            public int Kills => 0;
            public int Relics => 0;
            public float Charge => 100f;
            public float NovaCooldown => 0f;
            public float WardCooldown => 0f;
            public float NovaFlash => 0f;
            public int PendingSpawns => 0;
            public int LivingEnemies => 0;

            public PlayerState Player => new PlayerState
            {
                X = PlayerX,
                Y = PlayerY,
                Facing = PlayerFacing,
                Health = SimConfig.PlayerMaxHealth,
            };

            public IReadOnlyList<EnemyState> Enemies { get; } = new EnemyState[0];
            public IReadOnlyList<PickupState> Pickups { get; } = new PickupState[0];
            public SimEvents Events => SimEvents.None;
            public float NovaX => NovaAtX;
            public float NovaY => NovaAtY;
            public RunDigest Digest => default;
        }
    }
}
