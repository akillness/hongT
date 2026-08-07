// Code-generated effects only: nova ring, ward shell, pickup gems.
// No asset dependencies; everything survives missing-prefab scaffolding.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class VfxDirector : MonoBehaviour
    {
        const int RingSegments = 48;

        LineRenderer _novaRing;
        Material _novaMaterial;
        float _novaTime;
        float _novaX, _novaY;

        GameObject _wardShell;
        Material _wardMaterial;

        readonly Dictionary<int, Transform> _pickupViews = new Dictionary<int, Transform>(16);
        readonly List<int> _stale = new List<int>(16);
        Material[] _pickupMaterials;
        Camera _camera;
        Transform _playerTransform;

        // --- presentation additions (presentation-impact-spec) ---------------
        // Grave Pulse (E) persistent field ring (#7). Duration mirrors
        // HackSpec.PulseDuration but is a View constant on purpose — the sim
        // value is read-only reference, not a live dependency.
        const float PulseRingSeconds = 3f;
        LineRenderer _pulseRing;
        Material _pulseMaterial;
        float _pulseTime;
        float _pulseX, _pulseY;
        // Pickup collect absorption (#13): collected icons fly to the player.
        struct FlyingPickup
        {
            public Transform View;
            public Vector3 Start;
            public Vector3 StartScale;
            public float T;
        }
        readonly List<FlyingPickup> _flying = new List<FlyingPickup>(8);
        Vector3 _playerWorld;         // cached each SyncWard call (every frame)
        bool _pickupCollectedFlag;    // set by OnEvents, consumed by SyncPickups
        // Extraction ceremony (#16): elite corpse marker ring + channel beam.
        LineRenderer _corpseRing;
        Material _corpseMaterial;
        float _corpseTime;            // marker countdown (sim corpse TTL 10 s)
        float _corpseX, _corpseY;     // view-cached corpse position
        LineRenderer _channelBeam;
        Material _channelMaterial;
        // --- VFX impact pass: AOE scorch decals + bolt streak -----------------
        // Research-backed (isometric readability): ground-anchored shapes read
        // better than airborne particles; consistent color language per skill.
        // All quads/lines are pooled and reuse MakeUnlit (WebGL transparent
        // seed contract — ViewWorld.cs) — zero per-cast allocation.
        struct Scorch
        {
            public Transform Quad;
            public Material Material;
            public float Life, MaxLife;
            public Color Color;
        }
        readonly Scorch[] _scorches = new Scorch[4];
        int _scorchCursor;
        // §W wave warnings live on a DEDICATED RING pool declared with the other
        // Burst pools below: a warning must READ as a ring outline, and sharing
        // any live pool would evict a skill visual mid-play.
        LineRenderer _boltStreak;
        Material _boltStreakMaterial;
        float _boltStreakTime;
        // --- V3 element particles (interview lane; interjection: 파티클 도입) --
        // 4 pre-created pooled systems, Emit(count) only — no per-cast objects.
        // Materials clone the PROVEN unlit transparent seed (MakeUnlit): the
        // URP Particles shader has zero material references in this build and
        // would be variant-stripped on WebGL (pink/opaque) — spec §V3 contract.
        ParticleSystem _boltSparks, _pulseRipple, _novaDebris, _aegisFlash;
        float _pulseNextEmit;   // 0.5 s resonance cadence while the field lives
        // --- campaign hazards (built once on first SyncHazards call) ---------
        struct HazardView
        {
            public Transform Root;
            public Renderer Ring;       // vent telegraph / altar glow / pylon aura
            public Material RingMaterial;
            public float PrevCycleT;    // eruption wrap detection (#17)
            public Transform FillDisc;  // vent: V2 imminence fill / pylon: persistent scorch
            public Material FillMaterial;
            // --- cycle-2 kinds (docs/SIM_SPEC_DUNGEONS.md) -------------------
            // Slots are per-kind disjoint, mirroring how Ring doubles for
            // vent/altar: current band / pylon body / wall lethal overlay …
            public Transform Body;
            public Material BodyMaterial;
            // … current chevron row / pylon ember band / wall front curtain …
            public Transform Aux;
            public Material AuxMaterial;
            // … current band edges / wall boundary line.
            public Transform Edge;
            public Material EdgeMaterial;
            public float PushSign;      // current flow direction (anchor lookup)
            public bool Down;           // pylon destroyed — one-shot fired
        }
        HazardView[] _hazardViews;
        // Chevron repeat pitch inside the tide-current flow row (world units).
        // The scroll offset wraps at this pitch so the row reads endless.
        const float ChevronSpacingWorld = 1.3f;
        // Ash-wall visuals span the arena height (sim 540 px -> world units).
        const float WallSpanWorld = SimConfig.ArenaHalfHeight * 2f * ViewWorld.Scale;

        /// <summary>
        /// Flow direction (+1/-1) for a tide current at a sim position.
        /// HazardState does not publish PushX, so resolve it once at build
        /// time from the frozen anchor tables (positions are the identity —
        /// every shipped current placement is unique). Ranks are irrelevant
        /// to hazards; zeros keep the lookup allocation-light and pure.
        /// </summary>
        static float CurrentPushSign(float x, float y)
        {
            var ids = CampaignStages.Ids;
            for (var s = 0; s < ids.Count; s++)
            {
                if (!CampaignStages.TryGet(ids[s], 0, 0, 0, out var config)) continue;
                var hazards = config.Hazards;
                if (hazards == null) continue;
                for (var h = 0; h < hazards.Length; h++)
                {
                    if (hazards[h].Kind != HazardKind.TideCurrent) continue;
                    if (hazards[h].X != x || hazards[h].Y != y) continue;
                    return hazards[h].PushX < 0f ? -1f : 1f;
                }
            }
            // AMENDMENT #7: trial placements live outside CampaignStages. Without
            // this pass both bands of the current trial drew the same chevrons,
            // so the one thing that trial teaches — 순류 vs 역류 — was invisible.
            var trials = TrainingTrials.Ids;
            for (var t = 0; t < trials.Length; t++)
            {
                if (!TrainingTrials.TryGet(trials[t], out var hazards)) continue;
                for (var h = 0; h < hazards.Length; h++)
                {
                    if (hazards[h].Kind != HazardKind.TideCurrent) continue;
                    if (hazards[h].X != x || hazards[h].Y != y) continue;
                    return hazards[h].PushX < 0f ? -1f : 1f;
                }
            }
            return 1f;   // unknown placement (e.g. future override) — default +x
        }

        /// <summary>
        /// Strip a primitive's auto-collider. Mirrors ActorView's helper of the
        /// same name, and for the same reason: <c>Destroy</c> is a no-op OUTSIDE
        /// play mode and Unity logs "Destroy may not be called from edit mode"
        /// instead of removing anything. Every VFX primitive here is decoration
        /// that must never own physics, so an unguarded strip left ~20 live
        /// colliders in any edit-mode context and made the director untestable.
        /// </summary>
        static void RemovePrimitiveCollider(GameObject primitive)
        {
            var collider = primitive.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        void Awake()
        {
            var ringObject = new GameObject("NovaRing");
            ringObject.transform.SetParent(transform, false);
            _novaRing = ringObject.AddComponent<LineRenderer>();
            _novaRing.loop = true;
            _novaRing.positionCount = RingSegments;
            _novaRing.widthMultiplier = 0.09f;
            _novaRing.useWorldSpace = true;
            _novaMaterial = ViewWorld.MakeAdditive(new Color(1f, 0.62f, 0.25f, 1f));
            _novaRing.sharedMaterial = _novaMaterial;
            _novaRing.enabled = false;

            // Grave Pulse field ring (#7) — one persistent LineRenderer.
            var pulseObject = new GameObject("PulseRing");
            pulseObject.transform.SetParent(transform, false);
            _pulseRing = pulseObject.AddComponent<LineRenderer>();
            _pulseRing.loop = true;
            _pulseRing.positionCount = RingSegments;
            _pulseRing.widthMultiplier = 0.06f;
            _pulseRing.useWorldSpace = true;
            _pulseMaterial = ViewWorld.MakeAdditive(new Color(0.953f, 0.349f, 0.173f, 0.6f));
            _pulseRing.sharedMaterial = _pulseMaterial;
            _pulseRing.enabled = false;

            // Extraction ceremony visuals (#16) — corpse marker + channel beam.
            var corpseObject = new GameObject("CorpseRing");
            corpseObject.transform.SetParent(transform, false);
            _corpseRing = corpseObject.AddComponent<LineRenderer>();
            _corpseRing.loop = true;
            _corpseRing.positionCount = 28;
            _corpseRing.widthMultiplier = 0.05f;
            _corpseRing.useWorldSpace = true;
            _corpseMaterial = ViewWorld.MakeUnlit(new Color(0.173f, 0.678f, 0.839f, 0.6f), true);
            _corpseRing.sharedMaterial = _corpseMaterial;
            _corpseRing.enabled = false;

            var beamObject = new GameObject("ChannelBeam");
            beamObject.transform.SetParent(transform, false);
            _channelBeam = beamObject.AddComponent<LineRenderer>();
            _channelBeam.positionCount = 2;
            _channelBeam.widthMultiplier = 0.04f;
            _channelBeam.useWorldSpace = true;
            _channelMaterial = ViewWorld.MakeUnlit(new Color(0.173f, 0.678f, 0.839f, 0.5f), true);
            _channelBeam.sharedMaterial = _channelMaterial;
            _channelBeam.enabled = false;

            _wardShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RemovePrimitiveCollider(_wardShell);
            _wardShell.name = "WardShell";
            _wardShell.transform.localScale = Vector3.one * 1.7f;
            _wardMaterial = ViewWorld.MakeUnlit(new Color(0.45f, 0.85f, 1f, 0.28f), true);
            _wardShell.GetComponent<Renderer>().sharedMaterial = _wardMaterial;
            _wardShell.SetActive(false);

            _pickupMaterials = new[]
            {
                ViewWorld.MakeUnlit(new Color(1f, 0.604f, 0.322f), false),  // shard #ff9a52
                ViewWorld.MakeUnlit(new Color(1f, 0.831f, 0.537f), false),  // flask #ffd489
                ViewWorld.MakeUnlit(new Color(0.561f, 0.914f, 1f), false),  // relic #8fe9ff
                ViewWorld.MakeUnlit(new Color(0.78f, 0.62f, 1f), false),    // equip shard (campaign)
            };

            // V3 element particle pool: 4 systems pre-created, emission off,
            // Emit(count) on events only. Unlit transparent seed material —
            // spec §V3 shader-stripping contract.
            _boltSparks = BuildElementParticles("BoltSparks",
                new Color(0.75f, 0.55f, 1f, 0.9f), 0.05f, 0.35f, 2.6f);
            _pulseRipple = BuildElementParticles("PulseRipple",
                new Color(0.35f, 0.85f, 0.5f, 0.8f), 0.045f, 0.5f, 1.4f);
            _novaDebris = BuildElementParticles("NovaDebris",
                new Color(0.953f, 0.42f, 0.2f, 0.9f), 0.06f, 0.7f, 3.2f);
            _aegisFlash = BuildElementParticles("AegisFlash",
                new Color(0.56f, 0.85f, 1f, 0.85f), 0.05f, 0.4f, 2.0f);
        }

        ParticleSystem BuildElementParticles(
            string name, Color color, float size, float life, float speed)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            var system = host.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            // NOTE: URP/Unlit ignores per-particle vertex color — element color
            // lives in the per-system material; fades are system-level (spec
            // V3 amended: no per-particle gradients on the unlit seed path).
            main.maxParticles = 96;                       // hard budget per system
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.6f;                  // debris arcs read grounded
            var emission = system.emission;
            emission.enabled = false;                     // Emit(count) only
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            // --- Unity advanced-VFX guide techniques, ported to the built-in
            // ParticleSystem. VFX Graph itself CANNOT ship here: it requires
            // compute shaders, which WebGL lacks and CLAUDE.md L27 forbids.
            // These four modules are the guide's portable ideas — organic
            // noise motion, lifetime shaping, and strip trails — at zero
            // texture cost and zero new shader variants.

            // 1) Noise: the guide's headline technique. Straight-line debris
            // reads as a particle system; curled debris reads as fire, ash and
            // magic. Cheap because it samples a procedural field, not a texture.
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 1.8f;
            noise.scrollSpeed = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Low;   // WebGL budget
            noise.damping = true;                              // strength scales with size

            // 2) Size over lifetime: a burst that pops in and shrinks away
            // reads as energy dissipating. A constant-size burst reads as
            // sprites being deleted.
            var sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0f)));

            // 3) Rotation over lifetime: breaks the billboard grid so debris
            // does not read as a card sheet. NOTE the Unity API trap — this
            // setter is RADIANS per second even though the inspector shows
            // degrees. Mathf.PI = 180 deg/s, a half-turn per second; passing
            // 180f here would be 28.6 revolutions per second, i.e. shimmer.
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // PROVEN seed path (MakeUnlit) — URP Particles shader would be
            // variant-stripped on WebGL (zero material references in build).
            renderer.sharedMaterial = ViewWorld.MakeAdditive(color);
            // Emission is off — Play() once so Emit(count) bursts actually
            // simulate (a stopped system spawns particles that never age).
            system.Play();
            return system;
        }

        public void OnEvents(SimEvents events, ISimSnapshot sim)
        {
            if ((events & SimEvents.NovaCast) != 0)
            {
                _novaTime = 0.42f;
                _novaX = sim.NovaX;
                _novaY = sim.NovaY;
                _novaRing.enabled = true;
                // Impact pass: trailing shockwave echo + ground scorch. The
                // echo ring is slower/smaller so the pair reads as one blast
                // wave; the scorch anchors the burn area for 1.2 s (isometric
                // readability: ground shapes > airborne glow).
                SpawnBurst(sim.NovaX, sim.NovaY, new Color(0.953f, 0.349f, 0.173f, 0.55f), 1.7f, 0.6f);
                // Radius: arena NovaRadius=250; dungeon ash-nova is 230 — the
                // 8% visual overshoot is imperceptible on a fading decal, so
                // one constant serves both modes (decoration, not judgement).
                SpawnScorch(sim.NovaX, sim.NovaY, SimConfig.NovaRadius * ViewWorld.Scale * 2f,
                    new Color(0.35f, 0.12f, 0.05f, 0.5f), 1.2f);
                // V3: ember debris arcs out of the blast center (gravity 0.6
                // pulls them down — grounded read, not airborne glow).
                if (_novaDebris != null)
                {
                    _novaDebris.transform.position = ViewWorld.ToWorld(sim.NovaX, sim.NovaY, 0.4f);
                    _novaDebris.Emit(ViewPrefs.ReducedMotion ? 13 : 26);
                }
                // §S1 잿불 노바 = detonation. The ring says "a radius", the
                // crack fan says "the ground broke here" — 8 arms thrown to
                // the real damage edge so the silhouette IS the hit box.
                SpawnCrackFan(sim.NovaX, sim.NovaY, new Color(1f, 0.62f, 0.22f, 0.95f),
                    SimConfig.NovaRadius, 0.5f,
                    ViewPrefs.ReducedMotion ? 5 : 8);
            }
            // --- dungeon kit one-shots (v0.2) --------------------------------
            // §S1 질주 = a TRAIL, not a bloom. Dash and Ward were 0.06 apart
            // on one colour channel and used the same ring: indistinguishable.
            // Two shards raked backward along the dash axis separate them by
            // shape, which survives colourblindness and a busy floor alike.
            if ((events & SimEvents.DashUsed) != 0)
            {
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.56f, 0.91f, 1f, 0.8f), 0.32f, 0.24f);
                var back = -sim.Player.Facing;
                SpawnShard(sim.Player.X, sim.Player.Y, new Color(0.62f, 0.95f, 1f, 0.85f),
                    0.9f, 0.22f, new Vector3(back, 0f, 0.18f), rise: 0f);
                SpawnShard(sim.Player.X, sim.Player.Y, new Color(0.62f, 0.95f, 1f, 0.7f),
                    0.7f, 0.22f, new Vector3(back, 0f, -0.18f), rise: 0f);
            }
            if ((events & SimEvents.ComboFinisher) != 0)
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(1f, 0.83f, 0.45f, 0.9f), 0.45f, 0.3f);
            if ((events & SimEvents.LevelUp) != 0)
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.62f, 0.95f, 0.88f, 0.95f), 0.8f, 0.6f);
            if ((events & SimEvents.ExtractionComplete) != 0)
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.62f, 0.95f, 0.88f, 1f), 1.1f, 0.7f);
            if ((events & SimEvents.BossPhase2) != 0)
            {
                // NovaX/Y is the LAST NOVA origin — find the living boss instead.
                var enemies = sim.Enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    if (!enemies[i].IsBoss || enemies[i].Dead) continue;
                    SpawnBurst(enemies[i].X, enemies[i].Y, new Color(1f, 0.35f, 0.3f, 0.9f), 1.4f, 0.5f);
                    break;
                }
            }
            if ((events & SimEvents.BoltCast) != 0)
            {
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.75f, 0.55f, 1f, 0.7f), 0.3f, 0.2f);
                // Streak toward the nearest living enemy (the sim's bolt rule);
                // fallback: facing direction at full range.
                FireBoltStreak(sim);
                // §S1 균열 화살 = a RIFT opening, not a puff. Four short
                // cracks at the muzzle give the launch a fracture read that
                // matches the name, and they point along the shot so the eye
                // is thrown down the streak instead of stalling at the caster.
                SpawnCrackFan(sim.Player.X, sim.Player.Y, new Color(0.82f, 0.62f, 1f, 0.9f),
                    75f, 0.26f, ViewPrefs.ReducedMotion ? 3 : 4, startAngle: 0.4f);
            }
            // Grave Pulse (#7): the E field persists 3 s in the sim but only
            // had a 0.2 s burst — show the actual damage radius for the full
            // duration, anchored at the cast position (field never moves).
            if ((events & SimEvents.PulseCast) != 0)
            {
                _pulseTime = PulseRingSeconds;
                _pulseX = sim.Player.X;
                _pulseY = sim.Player.Y;
                _pulseRing.enabled = true;
                // Cast-moment scorch under the field so the 3 s ring has a
                // grounded fill, not just an outline.
                SpawnScorch(sim.Player.X, sim.Player.Y, 190f * 2f * ViewWorld.Scale,
                    new Color(0.09f, 0.22f, 0.16f, 0.42f), 3f);
                _pulseNextEmit = 0f;   // V3: first ripple on the next tick sync
                // §S1 묘지 파동 = the ground OPENS. A flat ring cannot say
                // that; vertical shards standing on the rim can, and they are
                // the one silhouette nothing else in the kit uses — the field
                // is now identifiable from its outline alone, at any colour.
                SpawnEruptionCrown(sim.Player.X, sim.Player.Y,
                    new Color(0.42f, 0.95f, 0.62f, 0.9f),
                    190f, 1.15f, 0.75f, ViewPrefs.ReducedMotion ? 6 : 10);
            }
            if ((events & SimEvents.WardCast) != 0)
            {
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.56f, 0.85f, 1f, 0.8f), 0.5f, 0.3f);
                // V3: absorb flash — inward-reading cyan puff at the cast.
                if (_aegisFlash != null)
                {
                    _aegisFlash.transform.position = ViewWorld.ToWorld(sim.Player.X, sim.Player.Y, 0.9f);
                    _aegisFlash.Emit(ViewPrefs.ReducedMotion ? 6 : 12);
                }
                // §S1 공허 방패 / 랜턴 결계 = a SHELL closing inward. Every
                // other effect in the kit grows outward, so shards planted on
                // the rim and leaning IN invert the grammar — the defensive
                // skill is the one that contracts, which is exactly what the
                // player needs to read in a crowd.
                SpawnEruptionCrown(sim.Player.X, sim.Player.Y,
                    new Color(0.5f, 0.88f, 1f, 0.85f),
                    62f, 0.7f, 0.42f, ViewPrefs.ReducedMotion ? 5 : 8);
            }
            // Pickup absorption (#13): tells the next SyncPickups sweep that a
            // vanished pickup was collected (vs expired) this tick batch.
            if ((events & SimEvents.PickupCollected) != 0)
                _pickupCollectedFlag = true;
            // AMENDMENT #8: a companion signature skill fired. The event is a run-wide
            // mask, so the per-slot flash flag on the snapshot says WHICH slot cast —
            // reading it here is what keeps two simultaneous casts from collapsing into
            // one burst. Colour is per skill so the four archetypes stay tellable apart
            // at a glance, which is the whole point of giving each one its own skill.
            if ((events & SimEvents.CompanionSkillCast) != 0 && sim is IHackSnapshot hackSkills)
            {
                for (var slot = 0; slot < hackSkills.CompanionCount; slot++)
                {
                    if (!hackSkills.CompanionSkillCastingAt(slot)) continue;
                    SpawnBurst(
                        hackSkills.CompanionXAt(slot),
                        hackSkills.CompanionYAt(slot),
                        CompanionSkillColor(hackSkills.CompanionSkillIdAt(slot)),
                        CompanionSkillBurstRadius(hackSkills.CompanionSkillIdAt(slot)),
                        0.4f);
                }
            }
            // Extraction corpse marker (#16): cache the freshest dead elite
            // position. Corpse TTL is sim-owned (10 s) — marker is decoration.
            if ((events & SimEvents.EliteDown) != 0)
            {
                var enemies = sim.Enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Dead || e.IsBoss || e.Scale <= 1.2f) continue;
                    _corpseX = e.X;
                    _corpseY = e.Y;
                    _corpseTime = 10f;
                    _corpseRing.enabled = true;
                    break;
                }
            }
            if ((events & SimEvents.ExtractionComplete) != 0)
            {
                // Existing burst handles the completion pop — just clear.
                _corpseTime = 0f;
                _corpseRing.enabled = false;
                _channelBeam.enabled = false;
            }
            // Ember pylon destroyed (cycle-2, docs/SIM_SPEC_DUNGEONS.md §Gimmick
            // 2). The event has no pylon identity — find the freshly-dead pylon
            // (Hp <= 0, view not yet torn down) in the published hazard list.
            if ((events & SimEvents.PylonDown) != 0 && _hazardViews != null
                && sim is ICampaignSnapshot campaign)
            {
                var hazards = campaign.Hazards;
                for (var i = 0; i < hazards.Count && i < _hazardViews.Length; i++)
                {
                    var h = hazards[i];
                    if (h.Kind != HazardKind.EmberPylon) continue;
                    if (h.Hp > 0f || _hazardViews[i].Down) continue;
                    TearDownPylon(i, h.X, h.Y);
                }
            }
        }

        /// <summary>
        /// One-shot pylon destruction: pooled burst + debris, body/aura off,
        /// scorch ring on. Idempotent via HazardView.Down — reachable from
        /// both the PylonDown event and the SyncHazards Hp-edge fallback.
        /// </summary>
        void TearDownPylon(int index, float simX, float simY)
        {
            ref var view = ref _hazardViews[index];
            if (view.Down) return;
            view.Down = true;
            SpawnBurst(simX, simY, new Color(1f, 0.55f, 0.2f, 0.9f), 1.1f, 0.5f);
            if (_novaDebris != null)
            {
                _novaDebris.transform.position = ViewWorld.ToWorld(simX, simY, 0.5f);
                _novaDebris.Emit(ViewPrefs.ReducedMotion ? 8 : 18);
            }
            if (view.Body != null) view.Body.gameObject.SetActive(false);  // body + band
            if (view.Ring != null) view.Ring.enabled = false;              // aura gone
            if (view.FillDisc != null) view.FillDisc.gameObject.SetActive(true); // scorch stays
        }

        /// <summary>
        /// §W wave-arrival telegraph: warning rings at the spawn points the
        /// incoming wave will use. Directional "they come from here" read, not
        /// a headcount — the ring set is derived from the sim's PUBLIC
        /// deterministic <see cref="CinderSim.SpawnPointIndexFor"/> so the View
        /// never duplicates spawn-count rules. Boss waves ring red and larger.
        ///
        /// Deliberately a CONTRACTING ring, inverting the kit-burst grammar: a
        /// burst grows outward and reads "something happened here", while a
        /// telegraph must read "something ARRIVES here". Written as a ring
        /// outline rather than a ground quad because a filled quad is
        /// indistinguishable from the stage's own floor decals — verified from
        /// a live frame diff before this rewrite.
        /// Dedicated pool; zero new allocation after first use.
        /// </summary>
        public void SpawnWaveWarnings(int wave, bool boss)
        {
            // One ring per pool slot: enough to read the arrival arc without
            // carpeting the plate (8 spawn points exist).
            var color = boss
                ? new Color(0.95f, 0.16f, 0.12f, 0.95f)
                : new Color(1f, 0.62f, 0.20f, 0.9f);
            var radius = (boss ? 150f : 105f) * ViewWorld.Scale;
            for (var i = 0; i < _waveWarnings.Length; i++)
            {
                var point = SimConfig.SpawnPoints[CinderSim.SpawnPointIndexFor(wave, i)];
                ref var slot = ref _waveWarnings[i];
                if (slot.Ring == null)
                {
                    var ringObject = new GameObject("WaveWarning");
                    ringObject.transform.SetParent(transform, false);
                    slot.Ring = ringObject.AddComponent<LineRenderer>();
                    slot.Ring.loop = true;
                    slot.Ring.positionCount = 28;
                    // Heavier than a kit burst (0.05): this must carry across a
                    // busy plate at the exact moment the banner punches in.
                    slot.Ring.widthMultiplier = 0.10f;
                    slot.Ring.useWorldSpace = true;
                    slot.Material = ViewWorld.MakeUnlit(color, true);
                    slot.Ring.sharedMaterial = slot.Material;
                }
                slot.Center = ViewWorld.ToWorld(point[0], point[1], 0.06f);
                slot.Color = color;
                slot.MaxRadius = radius;
                slot.MaxLife = slot.Life = 0.9f;
                slot.Ring.enabled = true;
            }
        }

        /// <summary>§W telegraph step: radius contracts MaxRadius -> 15% while
        /// alpha holds, so the ring closes on the spawn point instead of fading
        /// out like a burst. Mirrors <see cref="StepRingPool"/> otherwise.</summary>
        static void StepWarningPool(Burst[] pool, float deltaTime)
        {
            for (var i = 0; i < pool.Length; i++)
            {
                ref var warning = ref pool[i];
                if (warning.Ring == null || !warning.Ring.enabled) continue;
                warning.Life -= deltaTime;
                if (warning.Life <= 0f) { warning.Ring.enabled = false; continue; }
                var progress = 1f - warning.Life / warning.MaxLife;
                var radius = warning.MaxRadius * Mathf.Lerp(1f, 0.15f, progress);
                for (var s = 0; s < 28; s++)
                {
                    var angle = (Mathf.PI * 2f * s) / 28f;
                    warning.Ring.SetPosition(s, warning.Center + new Vector3(
                        Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
                }
                // Hold opaque through the close, then release only at the end.
                var pulse = warning.Color;
                pulse.a = warning.Color.a * Mathf.Clamp01((1f - progress) * 3f);
                warning.Material.color = pulse;
            }
        }

        // --- simple expanding ring pool for kit one-shots ------------------------
        struct Burst
        {
            public LineRenderer Ring;
            public Material Material;
            public float Life, MaxLife, MaxRadius;
            public Vector3 Center;
            public Color Color;
        }
        readonly Burst[] _bursts = new Burst[8];
        int _burstCursor;
        // §C3 hit sparks: dedicated pool — sharing _bursts would let a nova
        // volley (up to 6 sparks/frame) evict live skill rings mid-play.
        readonly Burst[] _sparks = new Burst[12];
        int _sparkCursor, _sparkBudget;
        // §W wave warnings: dedicated pool, exactly one ring per spawn point.
        // Separate from _bursts/_sparks so a wave telegraph never evicts a live
        // skill or hit ring, and vice versa.
        readonly Burst[] _waveWarnings = new Burst[4];

        /// <summary>AMENDMENT #8 cast colours — one per signature skill, matched to the
        /// existing per-skill colour language (warm = fast, cold = wide, ember = heavy).</summary>
        static Color CompanionSkillColor(CompanionSkillId id) => id switch
        {
            CompanionSkillId.Volley => new Color(0.98f, 0.85f, 0.35f, 0.55f),
            CompanionSkillId.Hex => new Color(0.55f, 0.45f, 0.95f, 0.55f),
            CompanionSkillId.Quake => new Color(0.90f, 0.40f, 0.25f, 0.55f),
            _ => new Color(0.95f, 0.60f, 0.25f, 0.55f),
        };

        /// <summary>Burst radius in WORLD units, i.e. the sim radius scaled by
        /// <see cref="ViewWorld.Scale"/>, so the ring is drawn at the size the skill
        /// actually reaches instead of an invented one.</summary>
        static float CompanionSkillBurstRadius(CompanionSkillId id) =>
            HackSpec.CompanionSkill(ArchetypeOfSkill(id)).Radius * ViewWorld.Scale;

        static EnemyVisual ArchetypeOfSkill(CompanionSkillId id) => id switch
        {
            CompanionSkillId.Volley => EnemyVisual.Scout,
            CompanionSkillId.Hex => EnemyVisual.Shade,
            CompanionSkillId.Quake => EnemyVisual.Possessed,
            _ => EnemyVisual.EmberCohort,
        };

        void SpawnBurst(float simX, float simY, Color color, float maxRadiusWorld, float life)
        {
            ref var slot = ref _bursts[_burstCursor];
            _burstCursor = (_burstCursor + 1) % _bursts.Length;
            if (slot.Ring == null)
            {
                var ringObject = new GameObject("KitBurst");
                ringObject.transform.SetParent(transform, false);
                slot.Ring = ringObject.AddComponent<LineRenderer>();
                slot.Ring.loop = true;
                slot.Ring.positionCount = 28;
                slot.Ring.widthMultiplier = 0.05f;
                slot.Ring.useWorldSpace = true;
                slot.Material = ViewWorld.MakeAdditive(color);
                slot.Ring.sharedMaterial = slot.Material;
            }
            slot.Center = ViewWorld.ToWorld(simX, simY, 0.06f);
            slot.Color = color;
            slot.MaxRadius = maxRadiusWorld;
            slot.MaxLife = slot.Life = life;
            slot.Ring.enabled = true;
        }

        /// <summary>§C3: small contact ring at the struck enemy. Budgeted at
        /// 6/frame (nova hits 20 enemies in one tick); finisher hits gold 2x.</summary>
        public void SpawnHitSpark(float simX, float simY, bool finisher)
        {
            if (_sparkBudget >= 6) return;
            _sparkBudget += 1;
            ref var slot = ref _sparks[_sparkCursor];
            _sparkCursor = (_sparkCursor + 1) % _sparks.Length;
            if (slot.Ring == null)
            {
                var ringObject = new GameObject("HitSpark");
                ringObject.transform.SetParent(transform, false);
                slot.Ring = ringObject.AddComponent<LineRenderer>();
                slot.Ring.loop = true;
                slot.Ring.positionCount = 28;
                slot.Ring.widthMultiplier = 0.035f;
                slot.Ring.useWorldSpace = true;
                slot.Material = ViewWorld.MakeAdditive(Color.white);
                slot.Ring.sharedMaterial = slot.Material;
            }
            slot.Center = ViewWorld.ToWorld(simX, simY, 0.1f);
            slot.Color = finisher
                ? new Color(1f, 0.83f, 0.45f, 0.9f)
                : new Color(0.953f, 0.349f, 0.173f, 0.75f);
            slot.MaxRadius = finisher ? 0.6f : 0.3f;
            slot.MaxLife = slot.Life = 0.18f;
            slot.Ring.enabled = true;
        }

        /// <summary>AOE ground scorch: flat quad decal, alpha fades over life.
        /// diameterWorld is world units (sim radius * 2 * ViewWorld.Scale).
        /// Pool of 4 - nova(8s cd) + pulse(4s cd) can't exceed it in play.</summary>
        void SpawnScorch(float simX, float simY, float diameterWorld, Color color, float life)
        {
            ref var slot = ref _scorches[_scorchCursor];
            _scorchCursor = (_scorchCursor + 1) % _scorches.Length;
            if (slot.Quad == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                RemovePrimitiveCollider(quad);
                quad.name = "AoeScorch";
                quad.transform.SetParent(transform, false);
                // Flat on the ground, iso-squashed like every ground ring.
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                slot.Quad = quad.transform;
                slot.Material = ViewWorld.MakeUnlit(color, true);   // transparent seed contract
                quad.GetComponent<Renderer>().sharedMaterial = slot.Material;
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            slot.Quad.position = ViewWorld.ToWorld(simX, simY, 0.02f);
            slot.Quad.localScale = new Vector3(diameterWorld, diameterWorld / SimConfig.IsoY, 1f);
            slot.Color = color;
            slot.MaxLife = slot.Life = life;
            slot.Material.color = color;
            slot.Quad.gameObject.SetActive(true);
        }

        void UpdateScorches(float deltaTime) => StepScorchPool(_scorches, deltaTime);

        static void StepScorchPool(Scorch[] pool, float deltaTime)
        {
            for (var i = 0; i < pool.Length; i++)
            {
                ref var scorch = ref pool[i];
                if (scorch.Quad == null || !scorch.Quad.gameObject.activeSelf) continue;
                scorch.Life -= deltaTime;
                if (scorch.Life <= 0f) { scorch.Quad.gameObject.SetActive(false); continue; }
                var faded = scorch.Color;
                faded.a = scorch.Color.a * Mathf.Clamp01(scorch.Life / scorch.MaxLife);
                scorch.Material.color = faded;
            }
        }

        /// <summary>Bolt streak: 2-point line from the player toward the nearest
        /// living enemy (mirrors the sim's bolt targeting); facing-direction
        /// fallback at full range. 0.16 s fade.</summary>
        void FireBoltStreak(ISimSnapshot sim)
        {
            if (_boltStreak == null)
            {
                var streakObject = new GameObject("BoltStreak");
                streakObject.transform.SetParent(transform, false);
                _boltStreak = streakObject.AddComponent<LineRenderer>();
                _boltStreak.positionCount = 2;
                _boltStreak.useWorldSpace = true;
                _boltStreak.startWidth = 0.07f;
                _boltStreak.endWidth = 0.015f;
                _boltStreakMaterial = ViewWorld.MakeAdditive(new Color(0.75f, 0.55f, 1f, 0.9f));
                _boltStreak.sharedMaterial = _boltStreakMaterial;
                _boltStreak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var player = sim.Player;
            const float BoltRange = 420f;   // HackSpec.BoltRange (view copy — decoration)
            var bestSq = BoltRange * BoltRange;
            float targetX = player.X + player.Facing * BoltRange, targetY = player.Y;
            var enemies = sim.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.Dead) continue;
                var dx = e.X - player.X;
                var dy = (e.Y - player.Y) * SimConfig.IsoY;
                var dSq = dx * dx + dy * dy;
                if (dSq >= bestSq) continue;
                bestSq = dSq;
                targetX = e.X;
                targetY = e.Y;
            }
            _boltStreak.SetPosition(0, ViewWorld.ToWorld(player.X, player.Y, 1.1f));
            _boltStreak.SetPosition(1, ViewWorld.ToWorld(targetX, targetY, 0.9f));
            _boltStreakTime = 0.16f;
            _boltStreak.enabled = true;
            // V3: violet pierce sparks at the streak's landing point.
            if (_boltSparks != null)
            {
                _boltSparks.transform.position = ViewWorld.ToWorld(targetX, targetY, 0.8f);
                _boltSparks.Emit(ViewPrefs.ReducedMotion ? 7 : 14);
            }
        }

        void UpdateBoltStreak(float deltaTime)
        {
            if (_boltStreak == null || !_boltStreak.enabled) return;
            _boltStreakTime -= deltaTime;
            if (_boltStreakTime <= 0f) { _boltStreak.enabled = false; return; }
            var c = _boltStreakMaterial.color;
            c.a = 0.9f * Mathf.Clamp01(_boltStreakTime / 0.16f);
            _boltStreakMaterial.color = c;
        }

        void UpdateBursts(float deltaTime)
        {
            _sparkBudget = 0;   // §C3 per-frame spawn budget resets here
            StepRingPool(_bursts, deltaTime);
            StepRingPool(_sparks, deltaTime);
            StepWarningPool(_waveWarnings, deltaTime);   // §W contracting rings
            StepShardPool(_shards, deltaTime);           // §S1 cracks + eruptions
            UpdateScorches(deltaTime);
            UpdateBoltStreak(deltaTime);
        }

        static void StepRingPool(Burst[] pool, float deltaTime)
        {
            for (var i = 0; i < pool.Length; i++)
            {
                ref var burst = ref pool[i];
                if (burst.Ring == null || !burst.Ring.enabled) continue;
                burst.Life -= deltaTime;
                if (burst.Life <= 0f) { burst.Ring.enabled = false; continue; }
                var progress = 1f - burst.Life / burst.MaxLife;
                var radius = burst.MaxRadius * progress;
                for (var s = 0; s < 28; s++)
                {
                    var angle = (Mathf.PI * 2f * s) / 28f;
                    burst.Ring.SetPosition(s, burst.Center + new Vector3(
                        Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
                }
                var faded = burst.Color;
                faded.a = burst.Color.a * (1f - progress);
                burst.Material.color = faded;
            }
        }

        // --- §S1 shape vocabulary: crack fan + eruption spikes -----------------
        // WHY THESE EXIST. Before this pass, NINE distinct events all called
        // SpawnBurst() — one expanding ring — and were told apart only by
        // colour and radius. Dash (0.56,0.91,1.00) and Ward (0.56,0.85,1.00)
        // differed by 0.06 on one channel: the same effect twice. Meanwhile
        // the skill NAMES each promise a different silhouette. 균열 = a crack
        // (radial fracture), 묘지 파동 = something rising OUT of the ground,
        // 공허 = collapse INWARD. An outward ring states none of those.
        //
        // Survey (.survey/skill-vfx-intensity/) found every source converging
        // on the same order: silhouette first, value contrast second, particle
        // count last — and nobody recommends particle volume as the lever for
        // intensity. So the fix is shape, not more particles: two pooled
        // LineRenderer families that cost the same as the ring they join.
        struct Shard
        {
            public LineRenderer Line;
            public Material Material;
            public float Life, MaxLife;
            public Vector3 Center;
            public Vector3 Direction;   // unit, iso-space
            public float Length, Rise;  // Rise > 0 = vertical eruption
            public Color Color;
            public float Seed;
        }
        // 8 crack arms + 10 spikes: one nova fan (8) and one pulse crown (10)
        // can be live together without either evicting the other.
        readonly Shard[] _shards = new Shard[18];
        int _shardCursor;
        const int ShardSegments = 5;   // 5 points = 4 jagged spans, cheap

        /// <summary>
        /// Radial fracture fan: `arms` jagged lines thrown out from a centre,
        /// each displaced perpendicular to its own axis so the pair reads as
        /// cracked ground rather than a starburst. This is the 균열 (rift)
        /// silhouette — the name's own promise, finally drawn.
        ///
        /// `radiusSim` is SIM units, matching <see cref="SpawnEruptionCrown"/>
        /// and the sim's own radii. Each arm's world length is solved PER
        /// BEARING so its tip lands on the damage edge, because the sim judges
        /// with hypot(dx, dy*IsoY) — an ellipse, not a circle.
        ///
        /// Why the length is computed here instead of encoding the stretch in
        /// the direction vector: SpawnShard NORMALISES direction. Passing a
        /// pre-stretched direction therefore throws the stretch away. Measured
        /// at radius 250: the on-axis pair lands exactly, so the fan looks
        /// correct at a glance, while the off-axis arms OVERSHOOT to 355 —
        /// 1.42x, i.e. exactly IsoY — and promise reach the sim will not
        /// honour. Length is the only channel that survives normalisation.
        /// </summary>
        void SpawnCrackFan(float simX, float simY, Color color, float radiusSim,
                           float life, int arms, float startAngle = 0f)
        {
            for (var a = 0; a < arms; a++)
            {
                var angle = startAngle + (Mathf.PI * 2f * a) / arms;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle) / SimConfig.IsoY;
                var lengthWorld = radiusSim * ViewWorld.Scale * Mathf.Sqrt(cos * cos + sin * sin);
                SpawnShard(simX, simY, color, lengthWorld, life,
                    new Vector3(cos, 0f, sin), rise: 0f);
            }
        }

        /// <summary>
        /// Eruption crown: `count` vertical shards standing up on the rim of a
        /// circle. Reads as the ground OPENING — the 묘지 파동 (grave) promise.
        /// Vertical lines are the one silhouette the flat ring grammar cannot
        /// produce, so this is what separates the field from every other AOE.
        /// </summary>
        void SpawnEruptionCrown(float simX, float simY, Color color, float radiusSim,
                                float riseWorld, float life, int count)
        {
            for (var s = 0; s < count; s++)
            {
                var angle = (Mathf.PI * 2f * s) / count;
                // radiusSim is SIM units (the same space the sim's own radii
                // live in, e.g. the 190 pulse field) because the rim offset is
                // applied BEFORE ToWorld — mixing spaces here would put the
                // crown 100x off. riseWorld is WORLD units: it is added after
                // the conversion, straight up the y axis the iso squash never
                // touches.
                //
                // The /IsoY on y is NOT decoration. The sim judges every AOE
                // with hypot(dx, dy*IsoY) <= radius (CinderSim.IsoWithin), so
                // the true field is an ELLIPSE in sim space, SHORTER in y (the
                // metric multiplies dy by 1.42, so y reaches only R/1.42).
                // A plain circle here would throw the crown OUTSIDE the real
                // damage edge along y (IsoY is 1.42, so y is compressed by the
                // metric, not stretched) — measured 38.5% of the radius past
                // it, promising a hit zone the sim will not honour. This is
                // the same ellipse StepRingPool already draws for every ground
                // ring, so the crown now agrees with both the sim and the
                // existing ring grammar.
                var offsetX = Mathf.Cos(angle) * radiusSim;
                var offsetY = Mathf.Sin(angle) * radiusSim / SimConfig.IsoY;
                SpawnShard(simX + offsetX, simY + offsetY, color, 0f, life,
                    Vector3.up, rise: riseWorld);
            }
        }

        void SpawnShard(float simX, float simY, Color color, float lengthWorld,
                        float life, Vector3 direction, float rise)
        {
            ref var slot = ref _shards[_shardCursor];
            _shardCursor = (_shardCursor + 1) % _shards.Length;
            if (slot.Line == null)
            {
                var host = new GameObject("Shard");
                host.transform.SetParent(transform, false);
                slot.Line = host.AddComponent<LineRenderer>();
                slot.Line.positionCount = ShardSegments;
                slot.Line.useWorldSpace = true;
                slot.Line.widthMultiplier = 0.05f;
                slot.Material = ViewWorld.MakeAdditive(color);
                slot.Line.sharedMaterial = slot.Material;
            }
            slot.Center = ViewWorld.ToWorld(simX, simY, 0.05f);
            slot.Direction = direction.normalized;
            slot.Length = lengthWorld;
            slot.Rise = rise;
            slot.Color = color;
            slot.MaxLife = slot.Life = life;
            // Per-shard seed keeps the jag stable for this shard's whole life
            // (re-randomising per frame would boil, which reads as noise, not
            // fracture) while differing between shards of the same fan.
            slot.Seed = _shardCursor * 12.9898f;
            slot.Line.enabled = true;
        }

        /// <summary>
        /// Shards grow out fast and retract, so the peak silhouette lands on
        /// the impact frame rather than trailing after it — "particles must
        /// dissipate the moment their communication task is finished".
        /// </summary>
        static void StepShardPool(Shard[] pool, float deltaTime)
        {
            for (var i = 0; i < pool.Length; i++)
            {
                ref var shard = ref pool[i];
                if (shard.Line == null || !shard.Line.enabled) continue;
                shard.Life -= deltaTime;
                if (shard.Life <= 0f) { shard.Line.enabled = false; continue; }
                var progress = 1f - shard.Life / shard.MaxLife;
                // Ease-out extend: 0 -> full in the first ~35% of life, so the
                // shape is already complete when the hit registers.
                var extend = Mathf.Clamp01(progress / 0.35f);
                extend = 1f - (1f - extend) * (1f - extend);
                var reach = shard.Rise > 0f ? shard.Rise : shard.Length;
                for (var s = 0; s < ShardSegments; s++)
                {
                    var t = (float)s / (ShardSegments - 1);
                    var along = shard.Direction * (reach * extend * t);
                    // Midpoint-style displacement perpendicular to the axis,
                    // zero at both ends so the shard stays anchored.
                    var jag = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(shard.Seed + t * 9.7f)
                              * reach * 0.16f;
                    var perpendicular = shard.Rise > 0f
                        ? new Vector3(jag, 0f, 0f)
                        : new Vector3(-shard.Direction.z, 0f, shard.Direction.x) * jag;
                    shard.Line.SetPosition(s, shard.Center + along + perpendicular);
                }
                var faded = shard.Color;
                // Hold full value through the first third, then fall away —
                // value contrast is the readability lever, so spend it early.
                faded.a = shard.Color.a * Mathf.Clamp01((1f - progress) * 1.5f);
                shard.Material.color = faded;
            }
        }

        /// <summary>End-of-run cleanup: hazard visuals, pickups, live bursts.</summary>
        public void ClearTransient()
        {
            if (_hazardViews != null)
            {
                for (var i = 0; i < _hazardViews.Length; i++)
                    if (_hazardViews[i].Root != null)
                        Destroy(_hazardViews[i].Root.gameObject);
                _hazardViews = null;
            }
            foreach (var pair in _pickupViews)
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            _pickupViews.Clear();
            for (var i = 0; i < _bursts.Length; i++)
                if (_bursts[i].Ring != null) _bursts[i].Ring.enabled = false;
            for (var i = 0; i < _sparks.Length; i++)
                if (_sparks[i].Ring != null) _sparks[i].Ring.enabled = false;
            for (var i = 0; i < _scorches.Length; i++)
                if (_scorches[i].Quad != null) _scorches[i].Quad.gameObject.SetActive(false);
            for (var i = 0; i < _waveWarnings.Length; i++)   // §W dedicated pool
                if (_waveWarnings[i].Ring != null) _waveWarnings[i].Ring.enabled = false;
            for (var i = 0; i < _shards.Length; i++)         // §S1 cracks/eruptions
                if (_shards[i].Line != null) _shards[i].Line.enabled = false;
            if (_boltStreak != null) _boltStreak.enabled = false;
            // §3.6: the idle arrow must not survive into the lobby; reset the
            // idle accumulator too so the next run starts from a clean 0.
            if (_threatArrow != null) _threatArrow.enabled = false;
            _playerIdleTime = 0f;
            _prevPlayerX = float.NaN;
            // V3 systems: drop live particles so run-end never leaks a 0.7 s
            // ember shower onto the lobby diorama.
            if (_boltSparks != null) _boltSparks.Clear();
            if (_pulseRipple != null) _pulseRipple.Clear();
            if (_novaDebris != null) _novaDebris.Clear();
            if (_aegisFlash != null) _aegisFlash.Clear();
            _pulseNextEmit = 0f;
            if (_novaRing != null) _novaRing.enabled = false;
            _novaTime = 0f;
            if (_pulseRing != null) _pulseRing.enabled = false;
            _pulseTime = 0f;
            for (var i = 0; i < _flying.Count; i++)
                if (_flying[i].View != null) Destroy(_flying[i].View.gameObject);
            _flying.Clear();
            _pickupCollectedFlag = false;
            _pickupLife.Clear();
            if (_corpseRing != null) _corpseRing.enabled = false;
            _corpseTime = 0f;
            if (_channelBeam != null) _channelBeam.enabled = false;
            if (_wardShell != null) _wardShell.SetActive(false);
        }

        /// <summary>
        /// Extraction ceremony (#16), dungeon only: corpse marker blinks for
        /// the corpse TTL; while the channel runs, a beam links player to
        /// corpse and the marker ring shrinks with progress.
        /// </summary>
        public void SyncExtraction(float progress, float target, in PlayerState player)
        {
            var channeling = target > 0f && progress > 0f;
            if (_channelBeam.enabled != channeling)
                _channelBeam.enabled = channeling;
            if (_corpseTime <= 0f)
            {
                if (_corpseRing.enabled) _corpseRing.enabled = false;
                if (_channelBeam.enabled) _channelBeam.enabled = false;
                return;
            }
            _corpseTime -= Time.deltaTime;
            var center = ViewWorld.ToWorld(_corpseX, _corpseY, 0.05f);
            // Ring shrinks as the channel banks seconds; idle = full radius.
            var shrink = channeling ? 1f - Mathf.Clamp01(progress / target) : 1f;
            var radius = 0.9f * Mathf.Max(0.15f, shrink);
            for (var i = 0; i < 28; i++)
            {
                var angle = (Mathf.PI * 2f * i) / 28f;
                _corpseRing.SetPosition(i, center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
            }
            // Cyan blink, urgency rising as the corpse TTL runs out.
            var blink = 0.35f + 0.25f * Mathf.PingPong(Time.time * 3f, 1f);
            var color = _corpseMaterial.color;
            color.a = blink * Mathf.Clamp01(_corpseTime / 3f + 0.4f);
            _corpseMaterial.color = color;
            if (channeling)
            {
                _channelBeam.SetPosition(0, ViewWorld.ToWorld(player.X, player.Y, 0.5f));
                _channelBeam.SetPosition(1, center);
            }
        }

        /// <summary>Campaign only. Builds static visuals once, animates per frame.</summary>
        public void SyncHazards(System.Collections.Generic.IReadOnlyList<HazardState> hazards)
        {
            if (_hazardViews == null)
            {
                _hazardViews = new HazardView[hazards.Count];
                for (var i = 0; i < hazards.Count; i++)
                    _hazardViews[i] = BuildHazardView(hazards[i]);
            }
            for (var i = 0; i < hazards.Count && i < _hazardViews.Length; i++)
            {
                var hazard = hazards[i];
                var view = _hazardViews[i];
                switch (hazard.Kind)
                {
                    case HazardKind.EmberVent:
                    {
                        // Eruption burst (#17): HazardPulse fires for EVERY vent
                        // each cycle boundary with no identity — a CycleT wrap
                        // on this vent is the only per-vent eruption signal.
                        if (hazard.CycleT < view.PrevCycleT)
                            SpawnBurst(hazard.X, hazard.Y,
                                new Color(0.953f, 0.349f, 0.173f, 0.9f), 0.9f, 0.3f);
                        _hazardViews[i].PrevCycleT = hazard.CycleT;

                        // Telegraph: ring brightens and pulses before the burst.
                        var color = view.RingMaterial.color;
                        if (hazard.Telegraphing)
                        {
                            var pulse = 0.55f + 0.45f * Mathf.PingPong(Time.time * 6f, 1f);
                            color.a = pulse;
                            color.r = 1f; color.g = 0.42f; color.b = 0.18f;
                        }
                        else
                        {
                            color.a = 0.22f;
                            color.r = 1f; color.g = 0.6f; color.b = 0.3f;
                        }
                        view.RingMaterial.color = color;
                        // V2 (interview lane, research telegraph rule): the fill
                        // disc grows with time-to-eruption so "how soon" reads at
                        // a glance — answering the telegraph's question, not just
                        // asking it. CycleT runs 0..VentPeriod; eruption at wrap.
                        if (view.FillDisc != null)
                        {
                            var progress = Mathf.Clamp01(hazard.CycleT / CampaignSpec.VentPeriod);
                            var fillRadius = hazard.Radius * ViewWorld.Scale * progress;
                            view.FillDisc.localScale = new Vector3(
                                fillRadius * 2f, 0.010f, fillRadius * 2f / SimConfig.IsoY);
                            var fillColor = view.FillMaterial.color;
                            fillColor.a = hazard.Telegraphing ? 0.5f : 0.16f;
                            view.FillMaterial.color = fillColor;
                        }
                        break;
                    }
                    case HazardKind.RelicAltar:
                    {
                        var ready = hazard.CooldownT <= 0f;
                        var color = view.RingMaterial.color;
                        color.a = ready ? 0.5f : 0.14f;
                        view.RingMaterial.color = color;
                        view.Root.localRotation = Quaternion.Euler(0f, Time.time * 24f, 0f);
                        break;
                    }
                    // ObsidianPillar is static.
                    case HazardKind.TideCurrent:
                    {
                        // Band bed: brighter while the push window is live.
                        // v1.1 retune: raised floor/active alphas — the band
                        // must read as terrain even while idle-telegraphing
                        // (contrast is the failure mode, qa benchmark band 6).
                        var bed = view.BodyMaterial.color;
                        bed.a = hazard.Active ? 0.45f : 0.22f;
                        view.BodyMaterial.color = bed;

                        // Edge lines: telegraph = blink (reduced-motion keeps
                        // them steady — persistent zone markers never strobe).
                        var edge = view.EdgeMaterial.color;
                        if (hazard.Telegraphing && !ViewPrefs.ReducedMotion)
                            edge.a = 0.35f + 0.55f * Mathf.PingPong(Time.time * 7f, 1f);
                        else if (hazard.Telegraphing || hazard.Active)
                            edge.a = 0.8f;
                        else
                            edge.a = 0.35f;
                        view.EdgeMaterial.color = edge;

                        // Chevron row: static direction arrows under reduced
                        // motion; scrolling at the sim push speed while active.
                        var chevron = view.AuxMaterial.color;
                        chevron.a = hazard.Active ? 1f : 0.55f;
                        view.AuxMaterial.color = chevron;
                        if (!ViewPrefs.ReducedMotion && hazard.Active)
                        {
                            // Row repeats every ChevronSpacing — a modular
                            // container shift reads as an endless stream.
                            var scroll = Mathf.Repeat(
                                Time.time * CampaignSpec.CurrentPush * ViewWorld.Scale,
                                ChevronSpacingWorld);
                            // The container is yaw-flipped for -x flow, so the
                            // world shift needs the sign applied outside it.
                            var local = view.Aux.localPosition;
                            local.x = scroll * view.PushSign;
                            view.Aux.localPosition = local;
                        }
                        else
                        {
                            var local = view.Aux.localPosition;
                            local.x = 0f;
                            view.Aux.localPosition = local;
                        }
                        break;
                    }
                    case HazardKind.EmberPylon:
                    {
                        var down = hazard.Hp <= 0f;
                        if (down && !view.Down)
                        {
                            // Fallback for a dropped event frame — OnEvents
                            // (PylonDown) is the primary one-shot path; this
                            // Hp edge keeps the state idempotent either way.
                            TearDownPylon(i, hazard.X, hazard.Y);
                        }
                        if (down) break;

                        // Ember band dims with remaining Hp (PylonHp..0) so shield
                        // strength reads at a glance; aura ring stays put as
                        // the persistent zone marker (reduced-motion safe).
                        var fraction = Mathf.Clamp01(hazard.Hp / CampaignSpec.PylonHp);
                        var band = view.AuxMaterial.color;
                        band.a = Mathf.Lerp(0.25f, 1f, fraction);
                        view.AuxMaterial.color = band;
                        break;
                    }
                    case HazardKind.AshWall:
                    {
                        // Side-agnostic liveness straight from the sim (depth
                        // > 0). The old FrontX > WallEdgeX read was left-wall
                        // only — a right wall IDLES at FrontX 1288 (> 248).
                        var live = hazard.Active;

                        // Signed world offset of the leading edge from this
                        // wall's home edge (root = hazard.X: 248 left / 1288
                        // right). Positive grows right, negative grows left —
                        // one expression serves both sides (v1.1 retune).
                        var frontWorld = (hazard.FrontX - hazard.X) * ViewWorld.Scale;

                        // Telegraph line at the home edge. Blink is the
                        // modulated part; under reduced motion the marker
                        // stays steady (persistent zone marker contract).
                        var edge = view.EdgeMaterial.color;
                        if (hazard.Telegraphing && !ViewPrefs.ReducedMotion)
                            edge.a = 0.3f + 0.6f * Mathf.PingPong(Time.time * 7f, 1f);
                        else if (hazard.Telegraphing || live)
                            edge.a = 0.9f;
                        else
                            edge.a = 0.3f;
                        view.EdgeMaterial.color = edge;

                        // Charcoal overlay covers the swallowed band between
                        // the home edge and FrontX; the ember curtain rides
                        // the leading edge. Reduced motion: boundary line
                        // only — overlay/curtain stay hidden.
                        var showBand = live && !ViewPrefs.ReducedMotion;
                        if (view.Body.gameObject.activeSelf != showBand)
                            view.Body.gameObject.SetActive(showBand);
                        if (view.Aux.gameObject.activeSelf != showBand)
                            view.Aux.gameObject.SetActive(showBand);
                        if (showBand)
                        {
                            view.Body.localScale = new Vector3(
                                Mathf.Abs(frontWorld), WallSpanWorld, 1f);
                            view.Body.localPosition = new Vector3(frontWorld * 0.5f, 0.03f, 0f);
                            view.Aux.localPosition = new Vector3(frontWorld, 0.8f, 0f);
                        }
                        else if (live && ViewPrefs.ReducedMotion)
                        {
                            // The boundary line doubles as the front marker.
                            view.Edge.localPosition = new Vector3(frontWorld, 0.04f, 0f);
                        }
                        if (!live && view.Edge.localPosition.x != 0f)
                            view.Edge.localPosition = new Vector3(0f, 0.04f, 0f);
                        break;
                    }
                }
            }
        }

        HazardView BuildHazardView(in HazardState hazard)
        {
            var view = new HazardView();
            var root = new GameObject($"Hazard-{hazard.Kind}");
            root.transform.SetParent(transform, false);
            root.transform.position = ViewWorld.ToWorld(hazard.X, hazard.Y);
            view.Root = root.transform;
            // Seed with the live phase so the first SyncHazards frame does not
            // read a bogus wrap and mis-fire the eruption burst (#17 risk).
            view.PrevCycleT = hazard.CycleT;

            switch (hazard.Kind)
            {
                case HazardKind.EmberVent:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(disc);
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.012f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.6f, 0.3f, 0.22f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    // V2 imminence fill: inner disc grows 0..radius over the
                    // cycle (research: time-based fill answers "how soon").
                    var fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(fill);
                    fill.transform.SetParent(root.transform, false);
                    fill.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                    fill.transform.localScale = Vector3.zero;
                    view.FillDisc = fill.transform;
                    view.FillMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.42f, 0.18f, 0.16f), true);
                    fill.GetComponent<Renderer>().sharedMaterial = view.FillMaterial;
                    break;
                }
                case HazardKind.ObsidianPillar:
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(pillar);
                    pillar.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    pillar.transform.localScale = new Vector3(r * 2f, 1.1f, r * 2f);
                    pillar.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    var material = ViewWorld.MakeUnlit(new Color(0.12f, 0.1f, 0.2f), false);
                    pillar.GetComponent<Renderer>().sharedMaterial = material;
                    // Faint cyan edge ring at the base for readability.
                    var baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(baseRing);
                    baseRing.transform.SetParent(root.transform, false);
                    baseRing.transform.localScale = new Vector3(r * 2.3f, 0.008f, r * 2.3f);
                    baseRing.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.35f, 0.6f, 0.8f, 0.3f), true);
                    break;
                }
                case HazardKind.RelicAltar:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(disc);
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f, 0.5f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    // Center relic gem.
                    var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    RemovePrimitiveCollider(gem);
                    gem.transform.SetParent(root.transform, false);
                    gem.transform.localScale = Vector3.one * 0.22f;
                    gem.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    gem.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f), false);
                    break;
                }
                case HazardKind.TideCurrent:
                {
                    // Flat rectangular flow band (1040x140 sim). The sim judge
                    // is axis-aligned (NOT iso-weighted — the only rect
                    // hazard), so the bed skips the usual /IsoY squash.
                    var bandW = CampaignSpec.CurrentHalfW * 2f * ViewWorld.Scale;
                    var bandH = CampaignSpec.CurrentHalfH * 2f * ViewWorld.Scale;
                    var bed = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    RemovePrimitiveCollider(bed);
                    bed.transform.SetParent(root.transform, false);
                    bed.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    bed.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                    bed.transform.localScale = new Vector3(bandW, bandH, 1f);
                    bed.GetComponent<Renderer>().shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    view.Body = bed.transform;
                    view.BodyMaterial = ViewWorld.MakeUnlit(
                        new Color(0.247f, 0.659f, 0.784f, 0.22f), true);   // stage accent, v1.1 raised floor
                    bed.GetComponent<Renderer>().sharedMaterial = view.BodyMaterial;

                    // Long edge lines — the telegraph blink surface. Both
                    // share ONE material so the sync pass mutates one color.
                    var edges = new GameObject("Edges");
                    edges.transform.SetParent(root.transform, false);
                    view.Edge = edges.transform;
                    view.EdgeMaterial = ViewWorld.MakeUnlit(
                        new Color(0.42f, 0.85f, 0.95f, 0.35f), true);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        RemovePrimitiveCollider(line);
                        line.transform.SetParent(edges.transform, false);
                        line.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        line.transform.localPosition =
                            new Vector3(0f, 0.02f, side * bandH * 0.5f);
                        line.transform.localScale = new Vector3(bandW, 0.045f, 1f);
                        var lineRenderer = line.GetComponent<Renderer>();
                        lineRenderer.shadowCastingMode =
                            UnityEngine.Rendering.ShadowCastingMode.Off;
                        lineRenderer.sharedMaterial = view.EdgeMaterial;
                    }

                    // Chevron row (direction + scroll carrier). One material
                    // for the whole row; the container yaw-flips for -x flow
                    // and the sync pass slides it by localPosition only.
                    view.PushSign = CurrentPushSign(hazard.X, hazard.Y);
                    var flow = new GameObject("Flow");
                    flow.transform.SetParent(root.transform, false);
                    if (view.PushSign < 0f)
                        flow.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    view.Aux = flow.transform;
                    // v1.1 retune: near-white chevrons — direction must read
                    // against the ash-grey floor even between push windows.
                    view.AuxMaterial = ViewWorld.MakeUnlit(
                        new Color(0.85f, 0.97f, 1f, 0.55f), true);
                    // 8 chevrons cover bandW - spacing; with the +0..spacing
                    // scroll offset the row always stays INSIDE the judged
                    // band (edge lines mark the true boundary — decoration
                    // must never widen the read).
                    for (var c = 0; c < 8; c++)
                    {
                        var cx = -bandW * 0.5f + c * ChevronSpacingWorld;
                        for (var seg = -1; seg <= 1; seg += 2)
                        {
                            var dash = GameObject.CreatePrimitive(PrimitiveType.Quad);
                            RemovePrimitiveCollider(dash);
                            dash.transform.SetParent(flow.transform, false);
                            dash.transform.localRotation =
                                Quaternion.Euler(90f, seg * 40f, 0f);
                            dash.transform.localPosition =
                                new Vector3(cx, 0.025f, seg * 0.09f);
                            dash.transform.localScale = new Vector3(0.34f, 0.05f, 1f);
                            var dashRenderer = dash.GetComponent<Renderer>();
                            dashRenderer.shadowCastingMode =
                                UnityEngine.Rendering.ShadowCastingMode.Off;
                            dashRenderer.sharedMaterial = view.AuxMaterial;
                        }
                    }
                    break;
                }
                case HazardKind.EmberPylon:
                {
                    // Unlit obsidian body, pillar grammar — but destructible:
                    // the ember band advertises "hit me" and dims with Hp.
                    var r = hazard.Radius * ViewWorld.Scale;
                    var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(body);
                    body.transform.SetParent(root.transform, false);
                    body.transform.localScale = new Vector3(r * 2f, 0.9f, r * 2f);
                    body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    view.Body = body.transform;
                    view.BodyMaterial = ViewWorld.MakeUnlit(new Color(0.16f, 0.08f, 0.06f), false);
                    body.GetComponent<Renderer>().sharedMaterial = view.BodyMaterial;

                    // Ember-orange band riding the upper body — child of the
                    // body so the destroyed state hides both in one SetActive.
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(band);
                    band.transform.SetParent(body.transform, false);
                    band.transform.localScale = new Vector3(1.12f, 0.09f, 1.12f);
                    band.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                    view.Aux = band.transform;
                    view.AuxMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.45f, 0.1f, 1f), true);
                    band.GetComponent<Renderer>().sharedMaterial = view.AuxMaterial;

                    // Aura disc, radius CampaignSpec.PylonAuraRadius (280
                    // v1.1 — was 220) —
                    // iso-scaled ellipse like the vent/altar rings. Persistent
                    // zone marker: never pulses (reduced-motion contract).
                    var aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(aura);
                    aura.transform.SetParent(root.transform, false);
                    var auraR = CampaignSpec.PylonAuraRadius * ViewWorld.Scale;
                    aura.transform.localScale = new Vector3(
                        auraR * 2f, 0.008f, auraR * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.5f, 0.2f, 0.10f), true);
                    view.Ring = aura.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;

                    // Scorch disc — hidden until PylonDown, then it is all
                    // that remains (permanent destruction, no respawn).
                    var scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    RemovePrimitiveCollider(scorch);
                    scorch.transform.SetParent(root.transform, false);
                    scorch.transform.localScale = new Vector3(
                        r * 2.6f, 0.006f, r * 2.6f / SimConfig.IsoY);
                    view.FillDisc = scorch.transform;
                    view.FillMaterial = ViewWorld.MakeUnlit(new Color(0.08f, 0.05f, 0.04f, 0.55f), true);
                    scorch.GetComponent<Renderer>().sharedMaterial = view.FillMaterial;
                    scorch.SetActive(false);
                    break;
                }
                case HazardKind.AshWall:
                {
                    // Root sits at this wall's HOME edge (config X: 248 left
                    // / 1288 right, y=ArenaY). The lethal band is y-full in
                    // the sim; visuals span the arena height (WallSpanWorld)
                    // — decoration, not judge. HazardState carries no PushX,
                    // so the side is inferred from the anchor X — build-time
                    // lookup grammar, same reasoning as CurrentPushSign.
                    var fromRight = hazard.X
                        > (CampaignSpec.WallEdgeX + CampaignSpec.WallEdgeRightX) * 0.5f;

                    // Boundary line at the home edge: the telegraph blink
                    // surface and the ONLY visual under reduced motion.
                    // Ember-orange (v1.1 retune): the old pale-ash line sat
                    // grey-on-grey against the echo-throne floor — contrast
                    // is the failure mode (qa benchmark band 6).
                    var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    RemovePrimitiveCollider(line);
                    line.transform.SetParent(root.transform, false);
                    line.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    line.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                    line.transform.localScale = new Vector3(0.06f, WallSpanWorld, 1f);
                    view.Edge = line.transform;
                    view.EdgeMaterial = ViewWorld.MakeUnlit(
                        new Color(1f, 0.55f, 0.18f, 0.30f), true);
                    var lineRenderer = line.GetComponent<Renderer>();
                    lineRenderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    lineRenderer.sharedMaterial = view.EdgeMaterial;

                    // Dark warm-charcoal overlay for the swallowed band
                    // between the home edge and FrontX — scaled every frame
                    // while advancing. Warm-shifted and denser (v1.1) so it
                    // separates from the ash-grey floor: NOT pure
                    // dark-on-grey.
                    var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    RemovePrimitiveCollider(overlay);
                    overlay.transform.SetParent(root.transform, false);
                    overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    view.Body = overlay.transform;
                    view.BodyMaterial = ViewWorld.MakeUnlit(
                        new Color(0.10f, 0.06f, 0.05f, 0.62f), true);
                    var overlayRenderer = overlay.GetComponent<Renderer>();
                    overlayRenderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    overlayRenderer.sharedMaterial = view.BodyMaterial;
                    overlay.SetActive(false);

                    // Vertical curtain sheet riding the leading edge — the
                    // "particle curtain" on the proven quad path (no new
                    // particle systems, no lights; spec §V3 budget). Ember
                    // glow (v1.1): the advancing front is the kill read.
                    var curtain = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    RemovePrimitiveCollider(curtain);
                    curtain.transform.SetParent(root.transform, false);
                    // Left wall, yaw -90: quad normal lands on +x, toward the
                    // arena side the (yaw-0, south, pitched-down) dungeon
                    // camera occupies. Right wall mirrors to yaw +90 (normal
                    // -x): the camera tracks the player, whom the wall pushes
                    // toward the arena side of either home edge, so the
                    // camera x stays on the front-face side. The wrong sign
                    // is backface-culled by the single-sided unlit quad.
                    curtain.transform.localRotation =
                        Quaternion.Euler(0f, fromRight ? 90f : -90f, 0f);
                    curtain.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                    curtain.transform.localScale = new Vector3(WallSpanWorld, 1.6f, 1f);
                    view.Aux = curtain.transform;
                    view.AuxMaterial = ViewWorld.MakeUnlit(
                        new Color(1f, 0.45f, 0.15f, 0.55f), true);
                    var curtainRenderer = curtain.GetComponent<Renderer>();
                    curtainRenderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    curtainRenderer.sharedMaterial = view.AuxMaterial;
                    curtain.SetActive(false);
                    break;
                }
            }
            return view;
        }

        // --- §3.6 idle threat arrow -------------------------------------------
        // Spec says "InputAdapter 이동 벡터 0 감지", but VfxDirector holds no
        // InputAdapter reference and the sim position IS the authoritative
        // idle signal (a blocked input still moves nothing). Deriving idle
        // from the snapshot keeps this View-only and avoids a new dependency.
        LineRenderer _threatArrow;
        Material _threatArrowMaterial;
        float _playerIdleTime;
        float _prevPlayerX = float.NaN, _prevPlayerY;
        const float ThreatArrowDelay = 0.4f;   // spec §3.6
        public void SyncWard(in PlayerState player)
        {
            // Player world position cache — absorption target for #13 and any
            // future player-anchored effect. SyncWard runs every view frame.
            _playerWorld = ViewWorld.ToWorld(player.X, player.Y, 0.4f);
            var active = player.WardTime > 0f;
            if (_wardShell.activeSelf != active)
                _wardShell.SetActive(active);
            if (!active) return;
            _wardShell.transform.position = ViewWorld.ToWorld(player.X, player.Y, 0.85f);
            // Blink during the last 0.5 s.
            if (player.WardTime < 0.5f)
            {
                var on = Mathf.FloorToInt(player.WardTime * 10f) % 2 == 0;
                _wardShell.GetComponent<Renderer>().enabled = on;
            }
            else
            {
                _wardShell.GetComponent<Renderer>().enabled = true;
            }
        }

        /// <summary>§3.6 (#9): after 0.4 s of no player movement, point a short
        /// arrow at the nearest living enemy. Idle is derived from the sim's
        /// own position delta — the authoritative signal, and View-only.
        /// Hidden the instant the player moves or no enemy is alive.</summary>
        public void SyncThreatArrow(in PlayerState player, IReadOnlyList<EnemyState> enemies)
        {
            var moved = float.IsNaN(_prevPlayerX)
                || Mathf.Abs(player.X - _prevPlayerX) > 0.5f
                || Mathf.Abs(player.Y - _prevPlayerY) > 0.5f;
            _prevPlayerX = player.X;
            _prevPlayerY = player.Y;
            _playerIdleTime = moved ? 0f : _playerIdleTime + Time.deltaTime;

            if (_playerIdleTime < ThreatArrowDelay)
            {
                if (_threatArrow != null && _threatArrow.enabled) _threatArrow.enabled = false;
                return;
            }

            // Nearest living enemy, iso-weighted like every other distance
            // check in this file (SimConfig.IsoY).
            var bestSq = float.MaxValue;
            float targetX = 0f, targetY = 0f;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Dead) continue;
                var dx = enemy.X - player.X;
                var dy = (enemy.Y - player.Y) * SimConfig.IsoY;
                var distSq = dx * dx + dy * dy;
                if (distSq >= bestSq) continue;
                bestSq = distSq;
                targetX = enemy.X;
                targetY = enemy.Y;
            }
            if (bestSq == float.MaxValue)
            {
                if (_threatArrow != null && _threatArrow.enabled) _threatArrow.enabled = false;
                return;
            }

            if (_threatArrow == null)
            {
                var host = new GameObject("ThreatArrow");
                host.transform.SetParent(transform, false);
                _threatArrow = host.AddComponent<LineRenderer>();
                _threatArrow.positionCount = 2;
                _threatArrow.useWorldSpace = true;
                _threatArrow.startWidth = 0.07f;
                _threatArrow.endWidth = 0.02f;   // taper reads as a pointer
                _threatArrowMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.83f, 0.45f, 0.7f), true);
                _threatArrow.sharedMaterial = _threatArrowMaterial;
                _threatArrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Short stub near the player, not a full line to the enemy —
            // it must read as a direction hint, never as a targeting laser.
            var origin = ViewWorld.ToWorld(player.X, player.Y, 0.15f);
            var toward = ViewWorld.ToWorld(targetX, targetY, 0.15f);
            var direction = (toward - origin).normalized;
            _threatArrow.SetPosition(0, origin + direction * 0.55f);
            _threatArrow.SetPosition(1, origin + direction * 1.15f);
            // Fade in over the first 0.3 s past the delay so it never pops.
            var ramp = Mathf.Clamp01((_playerIdleTime - ThreatArrowDelay) / 0.3f);
            var color = _threatArrowMaterial.color;
            color.a = 0.7f * ramp * ViewPrefs.MotionScale;
            _threatArrowMaterial.color = color;
            _threatArrow.enabled = true;
        }

        // Icon ids by PickupKind (EmberShard, OilFlask, RelicMote, EquipShard).
        static readonly string[] PickupIcons =
            { "pickup-ember", "pickup-flask", "pickup-relic", "equip-weapon" };
        readonly Material[] _pickupIconMaterials = new Material[4];

        // Last-known Life per pickup id: the sweep needs it to tell a collected
        // pickup (Life still healthy) from an expired one (Life ran out) after
        // the sim already dropped it from the list (#13).
        readonly Dictionary<int, float> _pickupLife = new Dictionary<int, float>(16);

        public void SyncPickups(IReadOnlyList<PickupState> pickups)
        {
            for (var i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (!_pickupViews.TryGetValue(pickup.Id, out var view))
                {
                    view = SpawnPickupIcon(pickup) ?? SpawnGem(pickup);
                    _pickupViews[pickup.Id] = view;
                }
                _pickupLife[pickup.Id] = pickup.Life;
                var bobHeight = 0.25f + Mathf.Sin(pickup.Bob * 3.4f) * 0.07f;
                view.position = ViewWorld.ToWorld(pickup.X, pickup.Y, bobHeight);
                // Material table is non-null exactly when the icon path won -
                // avoids per-frame Object.name string marshalling.
                var kind = (int)pickup.Kind;
                var isIcon = kind >= 0 && kind < _pickupIconMaterials.Length
                    && _pickupIconMaterials[kind] != null;
                if (_camera != null && isIcon)
                    view.rotation = Quaternion.LookRotation(
                        view.position - _camera.transform.position);   // billboard
                else
                    view.rotation = Quaternion.Euler(45f, pickup.Bob * 90f, 45f);
            }

            if (_pickupViews.Count != pickups.Count)
            {
                _stale.Clear();
                foreach (var pair in _pickupViews)
                {
                    var alive = false;
                    for (var i = 0; i < pickups.Count; i++)
                        if (pickups[i].Id == pair.Key) { alive = true; break; }
                    if (!alive) _stale.Add(pair.Key);
                }
                for (var i = 0; i < _stale.Count; i++)
                {
                    var id = _stale[i];
                    var view = _pickupViews[id];
                    // Absorption (#13): collected pickups (event fired and Life
                    // not yet expired) fly to the player instead of vanishing.
                    var collected = _pickupCollectedFlag
                        && _pickupLife.TryGetValue(id, out var life) && life > 0.05f;
                    if (collected && _flying.Count < 8)
                        _flying.Add(new FlyingPickup
                        {
                            View = view,
                            Start = view.position,
                            StartScale = view.localScale,
                            T = 0f,
                        });
                    else
                        Destroy(view.gameObject);
                    _pickupViews.Remove(id);
                    _pickupLife.Remove(id);
                }
            }
            _pickupCollectedFlag = false;
        }

        Transform SpawnGem(PickupState pickup)
        {
            var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemovePrimitiveCollider(gem);
            gem.name = "Pickup";
            gem.transform.localScale = Vector3.one * 0.18f;
            gem.GetComponent<Renderer>().sharedMaterial =
                _pickupMaterials[Mathf.Min((int)pickup.Kind, _pickupMaterials.Length - 1)];
            return gem.transform;
        }

        /// <summary>
        /// Camera-facing quad with the per-kind pickup icon (ember shard, oil
        /// flask, relic mote, equip shard). Falls back to null when the sprite
        /// is missing so the caller keeps the legacy gem cube - an untextured
        /// white quad would be worse than the old art.
        /// </summary>
        Transform SpawnPickupIcon(PickupState pickup)
        {
            var kind = (int)pickup.Kind;
            if (kind < 0 || kind >= PickupIcons.Length) return null;
            if (_camera == null) _camera = Camera.main;
            if (_pickupIconMaterials[kind] == null)
            {
                var icon = Resources.Load<Sprite>("Icons/" + PickupIcons[kind]);
                if (icon == null) return null;
                // No tint: the icons carry their own palette (ember orange /
                // amber / cyan / gold) - a multiply tint would shift them.
                var material = ViewWorld.MakeUnlit(Color.white, true);
                material.SetTexture("_BaseMap", icon.texture);
                _pickupIconMaterials[kind] = material;
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemovePrimitiveCollider(quad);
            quad.name = "PickupIcon";
            quad.transform.localScale = Vector3.one *
                (pickup.Kind == PickupKind.EquipShard ? 0.42f : 0.3f);
            quad.GetComponent<Renderer>().sharedMaterial = _pickupIconMaterials[kind];
            return quad.transform;
        }

        void Update()
        {
            if (_novaTime > 0f)
            {
                _novaTime -= Time.deltaTime;
                var progress = 1f - Mathf.Clamp01(_novaTime / 0.42f);
                var radius = SimConfig.NovaRadius * ViewWorld.Scale * progress;
                var center = ViewWorld.ToWorld(_novaX, _novaY, 0.05f);
                for (var i = 0; i < RingSegments; i++)
                {
                    var angle = (Mathf.PI * 2f * i) / RingSegments;
                    _novaRing.SetPosition(i, center + new Vector3(
                        Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
                }
                var color = _novaMaterial.color;
                color.a = 1f - progress;
                _novaMaterial.color = color;
                if (_novaTime <= 0f) _novaRing.enabled = false;
            }
            UpdateBursts(Time.deltaTime);
            UpdatePulseRing(Time.deltaTime);
            UpdateFlyingPickups(Time.deltaTime);
        }

        /// <summary>
        /// Grave Pulse persistent field ring (#7): fixed radius at the cast
        /// position for the field's 3 s life, alpha pulsing on the same 0.5 s
        /// rhythm as the sim's damage ticks (HackSpec.PulseTickInterval).
        /// </summary>
        void UpdatePulseRing(float deltaTime)
        {
            if (_pulseTime <= 0f) return;
            _pulseTime -= deltaTime;
            if (_pulseTime <= 0f)
            {
                _pulseRing.enabled = false;
                return;
            }
            var radius = 190f * ViewWorld.Scale;   // HackSpec.PulseRadius (view copy)
            var center = ViewWorld.ToWorld(_pulseX, _pulseY, 0.05f);
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = (Mathf.PI * 2f * i) / RingSegments;
                _pulseRing.SetPosition(i, center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
            }
            // 0.5 s alpha pulse resonating with the tick cadence; gentle fade
            // over the last second so the expiry never pops.
            var tickPhase = Mathf.PingPong(_pulseTime * 4f, 1f);
            var endFade = Mathf.Clamp01(_pulseTime);
            var color = _pulseMaterial.color;
            color.a = (0.25f + 0.35f * tickPhase) * endFade;
            _pulseMaterial.color = color;
            // V3: green tick ripple — one ring-edge puff per 0.5 s sim tick,
            // in phase with the damage cadence (PulseTickInterval view copy).
            _pulseNextEmit -= deltaTime;
            if (_pulseNextEmit <= 0f && _pulseRipple != null)
            {
                _pulseNextEmit = 0.5f;
                _pulseRipple.transform.position = center;
                _pulseRipple.Emit(ViewPrefs.ReducedMotion ? 5 : 10);
            }
        }

        /// <summary>Collected pickups fly into the player over 0.22 s (#13).</summary>
        void UpdateFlyingPickups(float deltaTime)
        {
            for (var i = _flying.Count - 1; i >= 0; i--)
            {
                var fly = _flying[i];
                fly.T += deltaTime;
                var t = Mathf.Clamp01(fly.T / 0.22f);
                if (t >= 1f || fly.View == null)
                {
                    if (fly.View != null) Destroy(fly.View.gameObject);
                    _flying.RemoveAt(i);
                    continue;
                }
                var eased = t * t;   // accelerate toward the player
                fly.View.position = Vector3.Lerp(fly.Start, _playerWorld, eased);
                fly.View.localScale = fly.StartScale * (1f - t);
                _flying[i] = fly;
            }
        }
    }
}
