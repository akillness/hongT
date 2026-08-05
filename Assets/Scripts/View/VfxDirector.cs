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
            public Renderer Ring;       // vent telegraph / altar glow
            public Material RingMaterial;
            public float PrevCycleT;    // eruption wrap detection (#17)
            public Transform FillDisc;  // V2: imminence fill (vent only)
            public Material FillMaterial;
        }
        HazardView[] _hazardViews;

        void Awake()
        {
            var ringObject = new GameObject("NovaRing");
            ringObject.transform.SetParent(transform, false);
            _novaRing = ringObject.AddComponent<LineRenderer>();
            _novaRing.loop = true;
            _novaRing.positionCount = RingSegments;
            _novaRing.widthMultiplier = 0.09f;
            _novaRing.useWorldSpace = true;
            _novaMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.62f, 0.25f, 1f), true);
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
            _pulseMaterial = ViewWorld.MakeUnlit(new Color(0.953f, 0.349f, 0.173f, 0.6f), true);
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
            Destroy(_wardShell.GetComponent<Collider>());
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
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // PROVEN seed path (MakeUnlit) — URP Particles shader would be
            // variant-stripped on WebGL (zero material references in build).
            renderer.sharedMaterial = ViewWorld.MakeUnlit(color, true);
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
            }
            // --- dungeon kit one-shots (v0.2) --------------------------------
            if ((events & SimEvents.DashUsed) != 0)
                SpawnBurst(sim.Player.X, sim.Player.Y, new Color(0.56f, 0.91f, 1f, 0.8f), 0.32f, 0.24f);
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
            }
            // Pickup absorption (#13): tells the next SyncPickups sweep that a
            // vanished pickup was collected (vs expired) this tick batch.
            if ((events & SimEvents.PickupCollected) != 0)
                _pickupCollectedFlag = true;
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
                slot.Material = ViewWorld.MakeUnlit(color, true);
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
                slot.Material = ViewWorld.MakeUnlit(Color.white, true);
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
        /// Pool of 4 — nova(8s cd) + pulse(4s cd) can't exceed it in play.</summary>
        void SpawnScorch(float simX, float simY, float diameterWorld, Color color, float life)
        {
            ref var slot = ref _scorches[_scorchCursor];
            _scorchCursor = (_scorchCursor + 1) % _scorches.Length;
            if (slot.Quad == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
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

        void UpdateScorches(float deltaTime)
        {
            for (var i = 0; i < _scorches.Length; i++)
            {
                ref var scorch = ref _scorches[i];
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
                _boltStreakMaterial = ViewWorld.MakeUnlit(new Color(0.75f, 0.55f, 1f, 0.9f), true);
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
            if (_boltStreak != null) _boltStreak.enabled = false;
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
                    Destroy(disc.GetComponent<Collider>());
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.012f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.6f, 0.3f, 0.22f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    // V2 imminence fill: inner disc grows 0..radius over the
                    // cycle (research: time-based fill answers "how soon").
                    var fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(fill.GetComponent<Collider>());
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
                    Destroy(pillar.GetComponent<Collider>());
                    pillar.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    pillar.transform.localScale = new Vector3(r * 2f, 1.1f, r * 2f);
                    pillar.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    var material = ViewWorld.MakeUnlit(new Color(0.12f, 0.1f, 0.2f), false);
                    pillar.GetComponent<Renderer>().sharedMaterial = material;
                    // Faint cyan edge ring at the base for readability.
                    var baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(baseRing.GetComponent<Collider>());
                    baseRing.transform.SetParent(root.transform, false);
                    baseRing.transform.localScale = new Vector3(r * 2.3f, 0.008f, r * 2.3f);
                    baseRing.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.35f, 0.6f, 0.8f, 0.3f), true);
                    break;
                }
                case HazardKind.RelicAltar:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(disc.GetComponent<Collider>());
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f, 0.5f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    // Center relic gem.
                    var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(gem.GetComponent<Collider>());
                    gem.transform.SetParent(root.transform, false);
                    gem.transform.localScale = Vector3.one * 0.22f;
                    gem.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    gem.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f), false);
                    break;
                }
            }
            return view;
        }

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
            Destroy(gem.GetComponent<Collider>());
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
            Destroy(quad.GetComponent<Collider>());
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
