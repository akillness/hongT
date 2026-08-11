// One actor (player / enemy / boss). Maps sim state to transform, Animator,
// billboarded health bar, and death fade. No per-frame allocations.
using System.Collections.Generic;
using System.Text;
using CinderCourt.Sim;
using UnityEngine;


namespace CinderCourt.View
{
    public sealed class ActorView : MonoBehaviour
    {
        static readonly int ActionParam = Animator.StringToHash("action");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Animator _animator;
        Transform _model;
        Transform _healthRoot;
        Renderer _healthFill;
        MaterialPropertyBlock _block;
        Renderer[] _renderers;
        Camera _camera;

        readonly struct ShadowCasterRecord
        {
            public readonly Renderer Renderer;
            public readonly Transform Root;
            public readonly string RootKind;

            public ShadowCasterRecord(Renderer renderer, Transform root, string rootKind)
            {
                Renderer = renderer;
                Root = root;
                RootKind = rootKind;
            }
        }

        static int _nextShadowActorId;
        readonly List<ShadowCasterRecord> _shadowCasters =
            new List<ShadowCasterRecord>(8);
        int _shadowActorId;
        bool _usesFallback;

        ActorAction _lastAction = (ActorAction)(-1);
        float _targetYaw;
        float _currentYaw;
        float _baseScale = 1f;
        float _healthFraction = 1f;
        float _flashTime;
        bool _dead;

        // --- presentation additions (presentation-impact-spec) ---------------
        static readonly Color PlayerFlashColor = new Color(1f, 0.35f, 0.3f);
        static readonly Color EnemyFlashColor = new Color(1f, 0.45f, 0.2f);   // ember tone
        static readonly Color EliteGold = new Color(1f, 0.78f, 0.25f);
        // Retune R2: pylon-aura shield read. Slight >1 blue lifts it toward
        // additive — a rim pass would need a new material (PROHIBITED: the
        // single-material MPB BaseColor path is the proven grammar here).
        static readonly Color ShieldCyan = new Color(0.45f, 1f, 1.15f);
        float _lastHealth = float.MaxValue;   // enemy health-delta cache (spec #5)
        float _deathPop;                      // kill pop timer (spec #4)
        Color _elementTint;                   // §K3 skill-element hit color (a=0 off)
        bool _eliteTint;                      // gold pulse marker (spec #14)
        bool _shieldTint;                     // R2 pylon-aura cyan (GameView judges)
        bool _shieldApplied;                  // falling-edge restore latch
        Color _flashColor = PlayerFlashColor;
        TrailRenderer _swingTrail;            // player-only (spec #8)
        // 16-direction display yaw (§M1): previous sim position, NaN = unseeded.
        float _prevSimX = float.NaN, _prevSimY = float.NaN;
        float _equipGlow;                     // §P2 rank glow (player only)
        int _comboTier = -1;                  // §C1 trail tier cache
        int _lastActionValue = -1;            // §M resolved animator value
        // §M View-only animator substates. These continue past the ActorAction
        // enum on purpose: the sim never emits them, the View resolves them.
        // They MUST match the row order in CharacterImportPipeline.Clips, which
        // ClipTableTests pins.
        const int Attack2Value = 11, Attack3Value = 12, CastValue = 13;
        // §M: the roar clip's readable length. Long enough that the entrance
        // registers, short enough that the boss is not a free target — it can
        // still turn and attack the instant its AI decides to.
        //
        // PUBLIC because the IMPORT fits the clip to it: CharacterImportPipeline
        // .ClipTrims trims Mutant Roaring to f8-34 (1.083 s, measured peak f21)
        // so the roar plays at speed 1 and ENDS as the window closes.
        //
        // It did not always work that way. Until 2026-08-09 this comment
        // claimed the `show` state's SPEED was fitted, and no such fit existed
        // anywhere — no trim row, no baked m_Speed, no PoseValueForClip entry.
        // The clip imported whole (5.42 s) into 1.1 s, so the entrance showed
        // roughly its first fifth and cut mid-bellow. The comment described an
        // intention; the pipeline never implemented it. (§4i: when a comment
        // and the code are two sources for one fact, they drift — the fix was
        // to make the pipeline true, then say what it does.)
        public const float RoarDuration = 1.1f;
        /// <summary>§M/#4 cast pose window. Same contract as RoarDuration: the
        /// cast clip is TRIMMED to fit (f23-30 = 0.292 s, measured peak f27),
        /// not retimed — a speed fit would need ~9x and read as a twitch.</summary>
        public const float CastPoseDuration = 0.30f;
        float _castPoseTime;                  // §M/#4 cast pose window
        float _knockbackTime;                 // §M launch reaction window
        float _roarTime;                      // §M boss entrance roar window
        // A pose window gets its FULL duration: the sync that ARMS it must not
        // immediately spend a frame delta against it — the same invariant the hit
        // flash below already keeps ("the frame that arms it must not immediately
        // spend a delta against it"). Without it the 0.18 s combo knockback armed
        // on a 0.2 s frame is already dead when ResolveActionValue reads it, so the
        // launch reaction silently depends on frame length instead of on the sim.
        bool _castPoseArmed, _knockbackArmed, _roarArmed;

        float _flashDuration = 0.13f;         // flash fade denominator
        float _gazeYaw = float.NaN;           // G1 combat gaze yaw (companion)
        // --- §M2 swing pacing ------------------------------------------------
        // The sim holds an attack pose for a FIXED window (arena 5 frames @
        // 12 fps = 0.417 s; dungeon HackSpec.ComboSwing = 0.30/0.30/0.42) and
        // drops it the instant that window closes. The authored mixamo swings
        // are ~1 s long, so at animator speed 1 the clip is cut at ~35% — the
        // arm winds up and the pose is yanked back to idle before the weapon
        // ever travels. Measured in the editor (prologue, 2026-02-04): every
        // swing ended at normalizedTime 0.10-0.35. Scaling the animator to
        // clipLength / window plays the WHOLE arc inside the window the sim
        // actually holds, which is what makes the swing readable at all.
        //
        // Mirrors CinderSim's private AttackClipFrames(5)/AttackClipFps(12).
        // SwingWindowMirrorsSimTests pins it against a real sim run, so a sim
        // change fails a test instead of silently mistiming every swing.
        internal const float ArenaSwingSeconds = 5f / 12f;
        // Sanity rails: a clip wildly out of scale with the window must not
        // produce a strobing or frozen pose.
        internal const float MinPoseSpeed = 0.5f, MaxPoseSpeed = 4f;
        // Authored clip seconds per attack pose value, read once from the
        // controller. Empty on a rig with no controller — pose speed stays 1.
        readonly Dictionary<int, float> _poseClipSeconds = new Dictionary<int, float>(4);


        // Original: depth scale 0.62..1.0 by screen y. NOT applied here — real
        // 3D perspective replaces it (docs/SIM_SPEC.md coordinate contract).

        /// <summary>
        /// Uniform scale applied to EVERY actor (player, companions, enemies,
        /// bosses) on top of its authored base scale. Restored to authored size
        /// after independent visual comparison showed 0.90x did not improve the
        /// board-scale combat silhouette over baseline. Applied once at Create
        /// so per-frame scale math (death pop, boss 1.6x) keeps its existing
        /// relationships.
        /// Actor prefabs are authored in world units, so this is independent of
        /// ViewWorld.Scale (which grew the floor by 25% in the same change).
        /// </summary>
        public const float GlobalScale = 1.00f;

        public static ActorView Create(GameObject prefab, Color fallbackColor, float baseScale)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab);
            }
            else
            {
                // Capsule fallback keeps the game playable before prefabs land.
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                RemovePrimitiveCollider(instance);
                instance.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                var renderer = instance.GetComponent<Renderer>();
                renderer.sharedMaterial = ViewWorld.MakeUnlit(fallbackColor, false);
            }
            var root = new GameObject("Actor");
            instance.transform.SetParent(root.transform, false);
            var view = root.AddComponent<ActorView>();
            view._shadowActorId = ++_nextShadowActorId;
            view._usesFallback = prefab == null;
            view._model = instance.transform;
            view._animator = instance.GetComponentInChildren<Animator>();
            view._renderers = instance.GetComponentsInChildren<Renderer>(true);
            view.CaptureBaseShadowCasters();
            view._block = new MaterialPropertyBlock();
            view._baseScale = baseScale * GlobalScale;
            view._camera = Camera.main;
            view.BuildHealthBar();
            view.CachePoseClipSeconds();
            StageShadowPolicy.RegisterActor(view);

            return view;
        }
        static void RemovePrimitiveCollider(GameObject primitive)
        {
            var collider = primitive.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }


        void BuildHealthBar()
        {
            _healthRoot = new GameObject("HealthBar").transform;
            _healthRoot.SetParent(transform, false);
            _healthRoot.localPosition = new Vector3(0f, 2.05f, 0f);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemovePrimitiveCollider(back);
            back.transform.SetParent(_healthRoot, false);
            back.transform.localScale = new Vector3(0.74f, 0.085f, 1f);
            var backRenderer = back.GetComponent<Renderer>();
            backRenderer.sharedMaterial =
                ViewWorld.MakeUnlit(new Color(0.05f, 0.04f, 0.09f, 0.9f), true);
            StageShadowPolicy.ConfigureExcludedRenderer(backRenderer);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemovePrimitiveCollider(fill);
            fill.transform.SetParent(_healthRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            fill.transform.localScale = new Vector3(0.7f, 0.055f, 1f);
            _healthFill = fill.GetComponent<Renderer>();
            _healthFill.sharedMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.6f, 0.32f), false);
            StageShadowPolicy.ConfigureExcludedRenderer(_healthFill);
        }

        void CaptureBaseShadowCasters()
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (!StageShadowPolicy.TryConfigureCaster(renderer)) continue;
                RegisterShadowCaster(renderer, _model, "body");
            }
            StageShadowPolicy.NotifyCasterBoundsChanged();
        }

        void RegisterShadowRoot(Transform root, string rootKind)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!StageShadowPolicy.TryConfigureCaster(renderer)) continue;
                RegisterShadowCaster(renderer, root, rootKind);
            }
            StageShadowPolicy.NotifyCasterBoundsChanged();
        }

        void RegisterShadowCaster(Renderer renderer, Transform root, string rootKind)
        {
            for (var i = 0; i < _shadowCasters.Count; i++)
                if (_shadowCasters[i].Renderer == renderer) return;
            _shadowCasters.Add(new ShadowCasterRecord(renderer, root, rootKind));
        }

        void UnregisterShadowRoot(string rootKind)
        {
            for (var i = _shadowCasters.Count - 1; i >= 0; i--)
            {
                var record = _shadowCasters[i];
                if (record.RootKind != rootKind) continue;
                StageShadowPolicy.ConfigureExcludedRenderer(record.Renderer);
                _shadowCasters.RemoveAt(i);
            }
            StageShadowPolicy.NotifyCasterBoundsChanged();
        }

        static void DestroyOwnedObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <param name="simDelta">Sim seconds actually advanced this frame
        /// (steps × FixedStep). NOT Time.deltaTime — see the launch gate below.
        /// 0 means no tick ran, so nothing moved and nothing can be inferred.</param>
        public void SyncPlayer(in PlayerState state, float simDelta = 0f)
        {
            // §M: the player can now be launched (phase-3 boss slam). The sim
            // keeps that state private — PlayerState is frozen — so the View
            // infers it from velocity, the same trick already used for
            // enemies.
            //
            // The gate is a BAND, not a floor. Measured px/s:
            //   walk 218 | slam 577 | dash 864 | retreat finisher 4440
            //   | run reset 30000+
            // A bare floor would fire on the retreat finisher — a one-frame
            // 74 px reposition during the player's OWN swing — and flash the
            // "I got hit" pose on an escape move. Anything above 1500 px/s is
            // a teleport (a finisher step or a run reset), never a launch.
            // The dash is excluded outright because Avoid owns its pose.
            //
            // THE DENOMINATOR IS SIM TIME, NOT RENDER TIME. The step above is
            // produced by whole 1/60 s ticks, and GameView runs 0 or 1 of them
            // on a frame shorter than the fixed step. Dividing a 1-tick step by
            // a shorter Time.deltaTime reports a speed inflated by
            // (1/60)/deltaTime: at 120 fps a plain 218 px/s walk reads 436 px/s
            // and lands inside this band, so ordinary walking played `bighit`
            // for as long as the player held a direction. Measured in the
            // editor at ~120 fps (prologue, 2026-02-04): sim=Move but
            // param=4/bighit for 1.9 s straight.
            if (!float.IsNaN(_prevSimX) && simDelta > 0f
                && state.Action != ActorAction.Avoid)
            {
                var stepX = state.X - _prevSimX;
                var stepY = state.Y - _prevSimY;
                var speed = Mathf.Sqrt(stepX * stepX + stepY * stepY) / simDelta;
                if (speed > 400f && speed < 1500f)
                {
                    _knockbackTime = HackSpec.BossSlamKnockbackTime;
                    _knockbackArmed = true;
                }

            }

            // NOTE: _prevSimX/Y are deliberately NOT written here. Apply's
            // 16-direction yaw block owns them, and writing first would hand
            // it a zero delta — facing would freeze on every frame.
            Apply(state.X, state.Y, state.Facing, state.Action,
                  state.Health / SimConfig.PlayerMaxHealth, 1f, false, 0f,
                  state.DamageCooldown > SimConfig.PlayerHitGrace - 0.16f);
            // Swing trail (spec #8): union of the arena (0.167-0.333) and
            // dungeon combo (0.10-0.30) active windows — pure decoration,
            // hit judgement stays in the sim.
            if (_swingTrail != null)
                _swingTrail.emitting = state.Action == ActorAction.Attack
                    && state.ActionTime >= 0.10f && state.ActionTime < 0.34f;
            // §V1: arm the convergence on the LEADING EDGE of a swing frame, so
            // one swing gets one glow however many frames it spans. Critical
            // counts — it is the same swing, graded.
            var swinging = state.Action == ActorAction.Attack
                        || state.Action == ActorAction.Critical;
            if (swinging)
            {
                if (!_swingGlowArmed)
                {
                    _swingGlowArmed = true;
                    ArmCastGlow(SwingGlowColor, SwingGlowSeconds);
                }
            }
            else _swingGlowArmed = false;
            UpdateCastGlow(Time.deltaTime);   // §V1 convergence step
            UpdateAfterimages(Time.deltaTime);   // dash ghosts (vfx survey)
        }

        /// <summary>
        /// Returns the damage taken this frame (spec #5/#6). The sim never
        /// exposes per-enemy DidDamage, so a health drop between frames IS the
        /// hit signal. First sync after pooling never counts as a hit.
        /// </summary>
        /// <param name="simDelta">Sim seconds actually advanced this frame.
        /// Same contract as <see cref="SyncPlayer"/>: render time would inflate
        /// the launch speed on any frame shorter than the fixed step.</param>
        public float SyncEnemy(in EnemyState state, float simDelta = 0f)
        {
            var damage = 0f;
            if (_lastHealth < float.MaxValue && state.Health < _lastHealth - 0.01f)
                damage = _lastHealth - state.Health;
            _lastHealth = state.Health;
            var hit = damage > 0f && !state.Dead;
            // §K3: a skill's element owns the hit color while its window is
            // live (GameView sets it), so the mesh itself reports WHAT hit it.
            if (hit) _flashColor = _elementTint.a > 0f ? _elementTint : EnemyFlashColor;
            // §M knockback: the sim launches 120 px over 0.18 s (~667 px/s) but
            // publishes no flag, so a hit frame that ALSO moves far faster than
            // a chase is the launch signal. Gate on VELOCITY, not step size:
            // GameView batch-runs ticks on slow frames, so at 20 fps a plain
            // chase step is 128 px/s * 50 ms = 6.4 px and a fixed px gate would
            // fire BigHit on every hit exactly when the game is already
            // struggling. 300 px/s sits between chase (<=128) and launch (~667)
            // — but ONLY when the divisor is the sim time that produced the
            // step. See SyncPlayer: dividing by Time.deltaTime inflates it by
            // (1/60)/deltaTime and a 128 px/s chase clears 300 above 140 fps.
            if (hit && !float.IsNaN(_prevSimX) && simDelta > 0f)
            {
                var stepX = state.X - _prevSimX;
                var stepY = state.Y - _prevSimY;
                var speed = Mathf.Sqrt(stepX * stepX + stepY * stepY) / simDelta;
                if (speed > 300f)
                {
                    _knockbackTime = HackSpec.ComboKnockbackTime;
                    _knockbackArmed = true;
                }

            }

            Apply(state.X, state.Y, state.Facing, state.Action,
                  state.MaxHealth > 0f ? state.Health / state.MaxHealth : 0f,
                  state.Scale, state.Dead, state.FadeTime, hit);
            return damage;
        }

        /// <summary>§K3: sets the color a skill's damage flashes on this mesh
        /// (alpha 0 clears back to the default ember hit tone).</summary>
        public void SetElementTint(Color tint) => _elementTint = tint;

        /// <summary>True while a hit/pickup flash owns this actor's MPB. The
        /// boss catalog tint is reapplied every frame AFTER SyncEnemy, so it
        /// must yield during the flash or a boss can never show a hit color
        /// (§K3: the element flash matters most on the boss).</summary>
        internal bool FlashLive => _flashTime > 0f;

        /// <summary>§M: true while the launch window inferred by SyncPlayer /
        /// SyncEnemy is open, i.e. while ResolveActionValue will pose BigHit
        /// instead of the sim's own action. The inference is a velocity
        /// heuristic over the sim step, so this is the seam that pins it:
        /// ordinary locomotion must never open this window.</summary>
        internal bool KnockbackLive => _knockbackTime > 0f;


        float _companionLastX;
        float _companionLastY;
        bool _companionSampled;
        float _companionMoveHold;

        /// <summary>Smallest per-step companion displacement that counts as
        /// locomotion, in world px. Real motion is ~4 px/step at companion
        /// speed, so this only rejects float noise (~160 ulps at arena scale).
        /// </summary>
        internal const float CompanionMoveEpsilon = 0.01f;

        /// <summary>Sim seconds a locomotion pose survives after the last
        /// moving step (~3 fixed steps at 60 Hz - descriptive, not an identity:
        /// 3f * FixedStep is 0.050000004f, which is not bit-equal to 0.05f).
        /// It is a settle tail, not a strobe guard: the strobe is handled by
        /// decaying on SIM delta (see <see cref="AdvanceCompanionMoveHold"/>).
        /// AMENDMENT #18's wander dwell is 0.35 s, so 0.30 s of it still reads
        /// as a genuine pause.
        /// </summary>
        internal const float CompanionMoveHoldSeconds = 0.05f;

        /// <summary>True when this step's companion displacement is real
        /// motion rather than float noise. Pure: the caller owns the hold
        /// window.</summary>
        internal static bool CompanionMoved(float deltaX, float deltaY)
            => deltaX * deltaX + deltaY * deltaY
               > CompanionMoveEpsilon * CompanionMoveEpsilon;

        /// <summary>Advances the locomotion-pose hold window. Pure.
        ///
        /// <paramref name="simDelta"/> is GameView's <c>steps x FixedStep</c>,
        /// NEVER Time.deltaTime - the same rule the launch heuristics follow
        /// (GameView §_simDelta). SyncViews runs per RENDER frame while the sim
        /// advances 0 or 1 fixed steps, so above 60 fps most frames re-read an
        /// UNCHANGED position. Decaying on render time would expire the hold
        /// during those frames and strobe the pose Move/Idle at the
        /// render-vs-sim beat frequency; decaying on sim time makes a zero-step
        /// frame decay by exactly zero, so the pose simply holds. That is why
        /// this is structural rather than a tuned constant.</summary>
        internal static float AdvanceCompanionMoveHold(float hold, bool moved, float simDelta)
            => moved ? CompanionMoveHoldSeconds : Mathf.Max(0f, hold - simDelta);

        /// <summary>The sim's ±1 facing, gated to the swing frame. Pure.
        ///
        /// <c>CompanionFacingAt</c> is ±1 ALWAYS - never 0 (HackTypes §D6.5;
        /// every write is <c>_player.Facing</c> or <c>targetDeltaX > 0f ? 1 : -1</c>).
        /// So <c>attackFacing != 0</c> can NEVER stand in for "is swinging". It
        /// did, and both yaw fallbacks below went unreachable: companions
        /// hard-snapped to 90°/270° forever and GameView's 16-direction gaze was
        /// computed then discarded. Gating on <paramref name="attacking"/> - the
        /// flag that actually means it - is the fix, and it lives HERE rather
        /// than at the call site so no caller can forget it.</summary>
        internal static int ResolveCompanionSwingFacing(bool attacking, int simFacing)
            => attacking ? simFacing : 0;

        /// <summary>Companion pose selection, pure so it can be pinned
        /// exhaustively (same idiom as <see cref="ResolveActionValue"/>).
        ///
        /// The pose reads MOVEMENT, not player proximity. Before AMENDMENT #18
        /// a no-target companion inside the follow band was genuinely parked, so
        /// GameView could infer Idle from "close to the player". #18's idle route
        /// walks 24 px legs entirely inside that band (ComfortRadius 128 px vs
        /// the 120 px inference), and the stale inference then posed a walking
        /// body as Idle - the companion slid across the floor with no walk
        /// cycle. The same inference also froze a companion hard-following a
        /// moving player inside the band.
        ///
        /// Priority: a swing outranks everything (the strike must show), then
        /// locomotion, then the combat gaze stance - a companion holding a
        /// target between strikes reads as ready, not asleep.</summary>
        internal static ActorAction ResolveCompanionAction(
            bool moving, bool attacking, bool hasGaze)
        {
            if (attacking) return ActorAction.Attack;
            if (moving || hasGaze) return ActorAction.Move;
            return ActorAction.Idle;
        }

        /// <summary>Companion follower (§4 + G1 combat gaze): position from the
        /// sim; pose/facing prioritized combat-first. attackFacing wins while
        /// the strike shows; combatFacing (nearest enemy in range) holds the
        /// gaze between strikes; movement dir is the peace-time fallback; a
        /// stationary companion with no target rests in Idle.
        ///
        /// <paramref name="attackFacing"/> is the sim's RAW ±1 facing - pass
        /// <c>CompanionFacingAt(slot)</c> unconditionally. Gating it on the swing
        /// is this method's job, not the caller's
        /// (<see cref="ResolveCompanionSwingFacing"/>), so the gaze and
        /// movement-yaw fallbacks stay reachable by construction.</summary>
        public void SyncCompanion(float simX, float simY, int attackFacing, bool attacking,
                                  float gazeYaw, float simDelta)
        {
            var swingFacing = ResolveCompanionSwingFacing(attacking, attackFacing);
            // No previous sample means no travel direction yet: default to +1
            // rather than comparing against a pooled predecessor's seat (or the
            // world origin, which only reads +1 because arena X is positive).
            var moveFacing = !_companionSampled || simX >= _companionLastX ? 1 : -1;
            var facing = swingFacing != 0 ? swingFacing : moveFacing;
            // G1(c): an in-range enemy owns the yaw even while the body keeps
            // following the player - without this, M1's movement-delta yaw
            // wins during Move and the companion stares at its travel path.
            // Full 16-direction angle (M1's 22.5° grammar), not ±1 snap;
            // the attack frame keeps the sim's authoritative ±1 facing.
            _gazeYaw = swingFacing != 0
                ? (swingFacing > 0 ? 90f : 270f)
                : gazeYaw;
            var moved = _companionSampled
                && CompanionMoved(simX - _companionLastX, simY - _companionLastY);
            _companionMoveHold = AdvanceCompanionMoveHold(_companionMoveHold, moved, simDelta);
            _companionLastX = simX;
            _companionLastY = simY;
            _companionSampled = true;
            var action = ResolveCompanionAction(
                _companionMoveHold > 0f, attacking, !float.IsNaN(gazeYaw));
            Apply(simX, simY, facing, action, 1f, 0.92f, false, 0f, false);
        }

        /// <summary>Elite marker (spec #14): pulsing gold tint through the
        /// shared MaterialPropertyBlock path. Cleared by ResetForPool.</summary>
        public void SetEliteTint(bool on) => _eliteTint = on;

        /// <summary>Retune R2: cyan shield tint while a live pylon aura covers
        /// this enemy. GameView judges coverage per frame (sim-mirrored iso
        /// test); steady tint — persistent state marker, reduced-motion safe
        /// by construction (it never pulses).</summary>
        public void SetShieldTint(bool on) => _shieldTint = on;

        /// <summary>True while the shield tint owns this actor's MPB — the
        /// boss catalog tint yields to it (same contract as FlashLive).</summary>
        internal bool ShieldLive => _shieldTint;

        /// <summary>§P2: equip-rank glow from the three T0-T5 ranks (0..15 sum).
        /// BaseColor modulation — the proven MPB path (elite tint / hit flash);
        /// URP emission needs the _EMISSION keyword MPB cannot set. Whole-body
        /// tint until P1 lands part-split renderers.</summary>
        public void SetEquipRanks(int weapon, int lantern, int cloak)
            => _equipGlow = Mathf.Clamp01((weapon + lantern + cloak) / 15f) * 0.45f;

        /// <summary>§P2: gold pickup flash on EquipDropped — shared flash path
        /// with its own duration; gold end-point keeps the lerp visible and
        /// reads as "rank up" against the ember glow.</summary>
        public void FlashEquip()
        {
            _flashColor = EliteGold;
            _flashTime = _flashDuration = 0.4f;
        }

        // --- §Lane P: rank-tier bone-socket props ---------------------------
        // Tier bands: T0-1 none / T2-3 basic / T4-5 fine. Prefabs live in
        // Resources/Props/equip-<slot>-<band>; sources are the two RETAINED
        // Abyssal-Surge prop meshes + one authored cloak (≤800 tris each).
        // Bone lookup reuses the swing-trail precedent; a non-humanoid rig
        // gets no props — §P2 whole-body tint stays the floor.
        static readonly string[] PropSlots = { "weapon", "lantern", "cloak" };
        static readonly HumanBodyBones[] PropBones =
            { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.Chest };
        readonly GameObject[] _equipProps = new GameObject[3];
        readonly int[] _equipPropBand = { 0, 0, 0 };   // 0 none / 1 basic / 2 fine
        string _weaponArchetype;   // W14: "dagger" / "bow" / "hammer" / null = legacy

        /// <summary>W14: select the weapon silhouette family. The archetype
        /// prefab (Props/equip-weapon-{archetype}-{band}) is preferred and the
        /// legacy equip-weapon-{band} mesh remains the fallback, so a build
        /// without the new FBX props keeps its current look. Resets the weapon
        /// slot so the next AttachEquipProps resolves the new family.</summary>
        public void SetWeaponArchetype(string archetype)
        {
            if (_weaponArchetype == archetype) return;
            _weaponArchetype = archetype;
            _equipPropBand[0] = 0;
            if (_equipProps[0] != null)
            {
                UnregisterShadowRoot(EquipmentRootKind(0));
                DestroyOwnedObject(_equipProps[0]);
                _equipProps[0] = null;
            }
        }

        static int PropBand(int tier) => tier >= 4 ? 2 : tier >= 2 ? 1 : 0;

        /// <summary>§Lane P: attach/refresh socket props for the three ranks.
        /// Idempotent per band — repeated calls with the same ranks are free;
        /// a rank-up mid-run swaps the prop the moment the sim rank changes
        /// (EquipDropped 즉시 반영, spec acceptance option b).</summary>
        public void AttachEquipProps(int weapon, int lantern, int cloak)
        {
            if (_animator == null || !_animator.isHuman) return;
            for (var slot = 0; slot < 3; slot++)
            {
                var tier = slot == 0 ? weapon : slot == 1 ? lantern : cloak;
                var band = PropBand(tier);
                if (band == _equipPropBand[slot]) continue;
                _equipPropBand[slot] = band;
                if (_equipProps[slot] != null)
                {
                    UnregisterShadowRoot(EquipmentRootKind(slot));
                    DestroyOwnedObject(_equipProps[slot]);
                    _equipProps[slot] = null;
                }
                if (band == 0) continue;
                var bone = _animator.GetBoneTransform(PropBones[slot]);
                if (bone == null) continue;
                var stage = band == 2 ? "fine" : "basic";
                GameObject prefab = null;
                if (slot == 0 && !string.IsNullOrEmpty(_weaponArchetype))
                    prefab = Resources.Load<GameObject>(
                        $"Props/equip-weapon-{_weaponArchetype}-{stage}");
                if (prefab == null)
                    prefab = Resources.Load<GameObject>(
                        $"Props/equip-{PropSlots[slot]}-{stage}");
                if (prefab == null) continue;   // asset missing -> tint floor
                var prop = Instantiate(prefab, bone, false);
                prop.name = $"EquipProp-{PropSlots[slot]}";
                ApplyPropPose(prop.transform, slot);
                _equipProps[slot] = prop;
                RegisterShadowRoot(prop.transform, EquipmentRootKind(slot));
            }
        }

        static string EquipmentRootKind(int slot) => "equipment:" + PropSlots[slot];

        /// <summary>Socket-space pose per slot (meshes are normalized by
        /// tools/blender/convert_equip_props.py: weapon grip at origin blade
        /// +Y, lantern top at origin body -Y, cloak top edge at origin).</summary>
        static void ApplyPropPose(Transform prop, int slot)
        {
            switch (slot)
            {
                case 0:   // weapon in the right palm, blade along the fingers
                    prop.localPosition = new Vector3(0.03f, 0.04f, 0f);
                    prop.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    break;
                case 1:   // lantern hangs under the left hand
                    prop.localPosition = new Vector3(0.02f, 0.02f, 0f);
                    prop.localRotation = Quaternion.Euler(0f, 0f, 180f);
                    break;
                default:  // cloak pinned high on the chest, sheet down the back
                    prop.localPosition = new Vector3(0f, 0.12f, -0.07f);
                    prop.localRotation = Quaternion.Euler(12f, 180f, 0f);
                    break;
            }
        }

        /// <summary>Pool hygiene: drop attached props with the actor reset.</summary>
        public void ClearEquipProps()
        {
            for (var slot = 0; slot < 3; slot++)
            {
                UnregisterShadowRoot(EquipmentRootKind(slot));
                DestroyOwnedObject(_equipProps[slot]);
                _equipProps[slot] = null;
                _equipPropBand[slot] = 0;
            }
        }

        /// <summary>§C1: combo-tier weapon trail — hits 1/2 ember (1x/1.5x width),
        /// finisher gold (2x). Pure decoration; hit windows stay sim-owned.
        /// §M/#9: the tier ALSO selects the per-swing attack pose, so it is
        /// recorded even when this actor has no trail — gating the bookkeeping
        /// on the trail would silently disable combo poses on any rig that
        /// never called EnableSwingTrail.</summary>
        public void SetComboTier(int tier)
        {
            if (tier == _comboTier) return;
            _comboTier = tier;
            if (_swingTrail == null) return;   // pose recorded; styling is trail-only
            var c = tier >= 2 ? EliteGold : new Color(0.953f, 0.349f, 0.173f);
            _swingTrail.startWidth = 0.06f * (tier <= 0 ? 1f : tier == 1 ? 1.5f : 2f);
            _swingTrail.startColor = new Color(c.r, c.g, c.b, 0.85f);
            _swingTrail.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        /// <summary>§M pose selection, pure so it can be pinned exhaustively.
        /// The sim emits one <see cref="ActorAction.Attack"/> per swing and has
        /// no cast action at all (ActorAction is a frozen sim type), so the View
        /// resolves both from state it already owns and continues the animator's
        /// integer past the enum: 11/12/13 = attack2/attack3/cast.
        ///
        /// Priority is deliberate. A combo swing outranks a cast window, and the
        /// cast pose speaks ONLY for an idle body: a reaction the sim asserted
        /// (hit, dodge, block, death) or locomotion must never be masked by
        /// decoration.
        ///
        /// §M knockback: the sim applies 120 px over 0.18 s on combo finishers
        /// and nova (HackSpec.ComboKnockbackDistance/AshNovaKnockback) but never
        /// sets BigHit, so the authored clip was dead while its driver was live.
        /// A live knockback outranks everything except death — being launched is
        /// the strongest thing happening to that body.</summary>
        internal static int ResolveActionValue(
            ActorAction action, int comboTier, bool castPoseLive, bool knockbackLive = false,
            bool roarLive = false)
        {
            if (action == ActorAction.Die) return (int)ActorAction.Die;
            if (knockbackLive) return (int)ActorAction.BigHit;
            // §M: the boss entrance roar. Sits under knockback (a boss launched
            // mid-roar should read as launched) but over locomotion, because a
            // roaring boss that walks looks like neither. Idle-only, same rule
            // as the cast pose: a boss already swinging keeps its swing.
            if (roarLive && (action == ActorAction.Idle || action == ActorAction.Move
                || action == ActorAction.Run))
                return (int)ActorAction.Show;
            if (action == ActorAction.Attack && comboTier > 0)
                return comboTier == 1 ? Attack2Value : Attack3Value;
            if (castPoseLive && action == ActorAction.Idle) return CastValue;
            return (int)action;
        }

        /// <summary>§M2: reads the authored length of every attack pose clip
        /// off the controller once, so the per-frame path only does a lookup.
        /// Clip names are the action names the import pipeline writes
        /// (CharacterImportPipeline.ReimportClips sets take.name = action).
        /// </summary>
        void CachePoseClipSeconds()
        {
            _poseClipSeconds.Clear();
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            var clips = _animator.runtimeAnimatorController.animationClips;
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.length <= 0f) continue;
                var value = PoseValueForClip(clip.name);
                if (value >= 0) _poseClipSeconds[value] = clip.length;
            }
        }

        /// <summary>Animator value a clip name poses, or -1 when the clip is
        /// not an attack pose (only swings are time-scaled: locomotion loops
        /// and reaction clips must keep their authored pace).</summary>
        internal static int PoseValueForClip(string clipName)
        {
            if (clipName == "attack") return (int)ActorAction.Attack;
            if (clipName == "critical") return (int)ActorAction.Critical;
            if (clipName == "attack2") return Attack2Value;
            if (clipName == "attack3") return Attack3Value;
            return -1;
        }

        /// <summary>§M2: sim seconds the swing pose is held. Arena/prologue run
        /// the fixed 5-frame attack clip; the dungeon combo runs
        /// HackSpec.ComboSwing per chain index, and GameView keeps the tier
        /// current BEFORE the pose resolves (§M/#9), so the tier IS the index
        /// of the swing on screen. comboTier &lt; 0 means "not a dungeon run".
        /// </summary>
        internal static float SwingWindowSeconds(int comboTier)
        {
            if (comboTier < 0) return ArenaSwingSeconds;
            var index = Mathf.Clamp(comboTier, 0, HackSpec.ComboLength - 1);
            return HackSpec.ComboSwing[index];
        }

        /// <summary>§M2: animator speed that fits <paramref name="actionValue"/>'s
        /// authored clip into the sim's swing window. 1 for every non-swing
        /// pose, and 1 whenever the clip length is unknown.</summary>
        internal static float PoseSpeed(float clipSeconds, float windowSeconds)
        {
            if (clipSeconds <= 0f || windowSeconds <= 0f) return 1f;
            return Mathf.Clamp(clipSeconds / windowSeconds, MinPoseSpeed, MaxPoseSpeed);
        }

        float ResolvePoseSpeed(int actionValue)
            => _poseClipSeconds.TryGetValue(actionValue, out var clipSeconds)
                ? PoseSpeed(clipSeconds, SwingWindowSeconds(_comboTier))
                : 1f;


        /// <summary>§M: starts the entrance roar window. Called from the
        /// BossSpawned event, which is where the intro letterbox already
        /// triggers — the sim never poses this, because a sim-side Show would
        /// be overwritten by the AI on the very next tick.</summary>
        public void PlayRoar()
        {
            _roarTime = RoarDuration;
            _roarArmed = true;

        }

        /// <summary>
        /// Player-only weapon trail (spec #8). Prefers the humanoid right hand
        /// bone; falls back to a model-root offset on non-humanoid rigs.
        /// </summary>
        public void EnableSwingTrail()
        {
            if (_swingTrail != null) return;
            Transform anchor = null;
            if (_animator != null && _animator.isHuman)
                anchor = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            var host = new GameObject("SwingTrail");
            host.transform.SetParent(anchor != null ? anchor : _model, false);
            if (anchor == null)
                host.transform.localPosition = new Vector3(0.35f, 1.05f, 0f);
            _swingTrail = host.AddComponent<TrailRenderer>();
            _swingTrail.time = 0.18f;
            _swingTrail.startWidth = 0.06f;
            _swingTrail.endWidth = 0f;
            _swingTrail.minVertexDistance = 0.02f;
            _swingTrail.sharedMaterial = ViewWorld.MakeUnlit(Color.white, true);
            _swingTrail.startColor = new Color(0.953f, 0.349f, 0.173f, 0.85f); // ember #f3592c
            _swingTrail.endColor = new Color(0.953f, 0.349f, 0.173f, 0f);
            _swingTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _swingTrail.receiveShadows = false;
            _swingTrail.emitting = false;
        }

        // --- dash afterimages (vfx survey: hack-and-slash dash flair) --------
        // Three world-frozen ghosts baked from the skinned mesh, spawned
        // ~55 ms apart along the dash path, additive ember fade over 0.28 s.
        // Player-only decoration; capsule fallbacks (no SkinnedMeshRenderer)
        // and non-triggered actors pay nothing.
        const int GhostCount = 3;
        const float GhostLife = 0.28f;
        const float GhostInterval = 0.055f;
        static readonly Color GhostColor = new Color(0.953f, 0.349f, 0.173f, 0.55f);
        SkinnedMeshRenderer _ghostSource;
        readonly Mesh[] _ghostMeshes = new Mesh[GhostCount];
        readonly Transform[] _ghosts = new Transform[GhostCount];
        readonly Material[] _ghostMaterials = new Material[GhostCount];
        readonly float[] _ghostLives = new float[GhostCount];
        int _ghostsPending;
        float _ghostSpawnCooldown;

        /// <summary>Begin a dash afterimage trail (DashUsed event). Safe no-op
        /// on rigs without a skinned mesh (capsule fallback).</summary>
        public void TriggerAfterimages()
        {
            if (_ghostSource == null)
                _ghostSource = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_ghostSource == null) return;
            _ghostsPending = GhostCount;
            _ghostSpawnCooldown = 0f;   // first ghost this frame — dash is short
        }

        void UpdateAfterimages(float deltaTime)
        {
            if (_ghostsPending > 0)
            {
                _ghostSpawnCooldown -= deltaTime;
                if (_ghostSpawnCooldown <= 0f)
                {
                    SpawnGhost();
                    _ghostsPending--;
                    _ghostSpawnCooldown = GhostInterval;
                }
            }
            for (var i = 0; i < GhostCount; i++)
            {
                if (_ghostLives[i] <= 0f) continue;
                _ghostLives[i] -= deltaTime;
                if (_ghostLives[i] <= 0f)
                {
                    if (_ghosts[i] != null) _ghosts[i].gameObject.SetActive(false);
                    continue;
                }
                var color = GhostColor;
                color.a = GhostColor.a * (_ghostLives[i] / GhostLife) * ViewPrefs.MotionScale;
                _ghostMaterials[i].color = color;
            }
        }

        void SpawnGhost()
        {
            // Oldest slot (smallest remaining life) is recycled.
            var slot = 0;
            for (var i = 1; i < GhostCount; i++)
                if (_ghostLives[i] < _ghostLives[slot]) slot = i;
            if (_ghosts[slot] == null)
            {
                var host = new GameObject("DashGhost");
                _ghostMeshes[slot] = new Mesh();
                host.AddComponent<MeshFilter>().sharedMesh = _ghostMeshes[slot];
                var renderer = host.AddComponent<MeshRenderer>();
                _ghostMaterials[slot] = ViewWorld.MakeAdditive(GhostColor);
                renderer.sharedMaterial = _ghostMaterials[slot];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _ghosts[slot] = host.transform;   // world-frozen: no parent
            }
            _ghostSource.BakeMesh(_ghostMeshes[slot], true);
            var source = _ghostSource.transform;
            _ghosts[slot].SetPositionAndRotation(source.position, source.rotation);
            _ghosts[slot].localScale = Vector3.one;
            _ghosts[slot].gameObject.SetActive(true);
            _ghostLives[slot] = GhostLife;
            _ghostMaterials[slot].color = GhostColor;
        }

        void ClearAfterimages()
        {
            _ghostsPending = 0;
            for (var i = 0; i < GhostCount; i++)
            {
                _ghostLives[i] = 0f;
                if (_ghosts[i] != null) _ghosts[i].gameObject.SetActive(false);
            }
        }

        internal bool UsesFallbackForShadowDiagnostics => _usesFallback;
        internal int RegisteredShadowCasterCount => _shadowCasters.Count;

        internal Renderer RegisteredShadowCasterAt(int index)
            => _shadowCasters[index].Renderer;

        internal string RegisteredShadowCasterKeyAt(int index)
        {
            var record = _shadowCasters[index];
            return ShadowCasterKey(record.Renderer, record.Root, record.RootKind);
        }

        internal bool ShadowCasterSetsMatch()
        {
            var eligible = new HashSet<string>();
            var liveBody = _model != null
                ? _model.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            for (var i = 0; i < liveBody.Length; i++)
            {
                var renderer = liveBody[i];
                if (!StageShadowPolicy.IsEligibleCaster(renderer)
                    || IsEquipmentRenderer(renderer)
                    || (_castGlow != null
                        && renderer.transform.IsChildOf(_castGlow)))
                    continue;
                if (!eligible.Add(ShadowCasterKey(renderer, _model, "body"))) return false;
            }

            for (var slot = 0; slot < _equipProps.Length; slot++)
            {
                var prop = _equipProps[slot];
                if (prop == null) continue;
                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (!StageShadowPolicy.IsEligibleCaster(renderer)) continue;
                    if (!eligible.Add(ShadowCasterKey(
                            renderer, prop.transform, EquipmentRootKind(slot))))
                        return false;
                }
            }

            var registered = new HashSet<string>();
            for (var i = 0; i < _shadowCasters.Count; i++)
            {
                var record = _shadowCasters[i];
                var renderer = record.Renderer;
                if (renderer == null
                    || renderer.shadowCastingMode
                        != UnityEngine.Rendering.ShadowCastingMode.On
                    || renderer.receiveShadows
                    || renderer.renderingLayerMask
                        != StageShadowPolicy.ActorRenderingLayerMask)
                    return false;
                if (!registered.Add(ShadowCasterKey(
                        renderer, record.Root, record.RootKind)))
                    return false;
            }
            return eligible.SetEquals(registered);
        }

        bool IsEquipmentRenderer(Renderer renderer)
        {
            for (var slot = 0; slot < _equipProps.Length; slot++)
            {
                var prop = _equipProps[slot];
                if (prop != null && renderer.transform.IsChildOf(prop.transform)) return true;
            }
            return false;
        }

        internal void AccumulateShadowCasterExtents(
            ref float maximumHeight,
            ref float maximumHorizontalRadius)
        {
            var actorOrigin = transform.position;
            for (var i = 0; i < _shadowCasters.Count; i++)
            {
                var renderer = _shadowCasters[i].Renderer;
                if (renderer == null || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                    continue;
                var bounds = renderer.bounds;
                var centerOffset = bounds.center - actorOrigin;
                maximumHeight = Mathf.Max(
                    maximumHeight,
                    Mathf.Abs(centerOffset.y) + bounds.extents.y);
                maximumHorizontalRadius = Mathf.Max(
                    maximumHorizontalRadius,
                    Mathf.Max(
                        Mathf.Abs(centerOffset.x) + bounds.extents.x,
                        Mathf.Abs(centerOffset.z) + bounds.extents.z));
            }
        }

        internal void AccumulateShadowCasterDistance(Camera camera, ref float maximumDistance)
        {
            if (camera == null) return;
            var cameraPosition = camera.transform.position;
            for (var i = 0; i < _shadowCasters.Count; i++)
            {
                var renderer = _shadowCasters[i].Renderer;
                if (renderer == null || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                    continue;
                var bounds = renderer.bounds;
                for (var x = 0; x <= 1; x++)
                for (var y = 0; y <= 1; y++)
                for (var z = 0; z <= 1; z++)
                {
                    var corner = new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z);
                    maximumDistance = Mathf.Max(
                        maximumDistance, Vector3.Distance(cameraPosition, corner));
                }
            }
        }

        string ShadowCasterKey(Renderer renderer, Transform registeredRoot, string rootKind)
        {
            if (renderer == null) return $"{_shadowActorId}|{rootKind}|<destroyed>";
            var mesh = SharedMesh(renderer);
            return $"{_shadowActorId}|{rootKind}|"
                + $"{HierarchyIndexPath(renderer.transform, registeredRoot)}|"
                + $"{renderer.GetType().Name}:{RendererComponentIndex(renderer)}|"
                + (mesh != null
                    ? mesh.name + ":" + EntityId.ToULong(mesh.GetEntityId())
                    : "<no-mesh>");
        }

        static Mesh SharedMesh(Renderer renderer)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) return skinned.sharedMesh;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        static int RendererComponentIndex(Renderer renderer)
        {
            var renderers = renderer.GetComponents<Renderer>();
            var type = renderer.GetType();
            var index = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].GetType() != type) continue;
                if (renderers[i] == renderer) return index;
                index++;
            }
            return -1;
        }

        static string HierarchyIndexPath(Transform target, Transform registeredRoot)
        {
            if (target == registeredRoot) return ".";
            var path = new StringBuilder(32);
            var cursor = target;
            while (cursor != null && cursor != registeredRoot)
            {
                if (path.Length > 0) path.Insert(0, '/');
                path.Insert(0, cursor.GetSiblingIndex());
                cursor = cursor.parent;
            }
            return cursor == registeredRoot ? path.ToString() : "<outside>";
        }

        void OnEnable() => StageShadowPolicy.RegisterActor(this);

        void OnDisable() => StageShadowPolicy.UnregisterActor(this);

        void OnDestroy()
        {
            StageShadowPolicy.UnregisterActor(this);
            _shadowCasters.Clear();
            // Ghosts are unparented (world-frozen) — scene teardown must not
            // leak them, their baked meshes, or their cloned materials.
            for (var i = 0; i < GhostCount; i++)
            {
                if (_ghosts[i] != null) Destroy(_ghosts[i].gameObject);
                if (_ghostMeshes[i] != null) Destroy(_ghostMeshes[i]);
                if (_ghostMaterials[i] != null) Destroy(_ghostMaterials[i]);
            }
        }

        // --- §Lane V1: cast-sync hand glow ----------------------------------
        // A small unlit sphere on the right hand that converges (scale 1→0.35)
        // over the pre-release window, then pops off at emission. Decoration
        // ONLY: the sim's action frames are the authority — this reads them,
        // never gates them. Player-only, created lazily with the swing trail's
        // bone-lookup precedent; non-humanoid rigs simply never show it.
        Transform _castGlow;
        Material _castGlowMaterial;
        float _castGlowTime, _castGlowDuration;

        /// <summary>Begin the convergence glow (call at cast events). Color
        /// follows the skill's element; duration matches the visual pre-release
        /// beat (0.12 s per spec §V1).</summary>
        public void FlashCastGlow(Color color, float duration = 0.12f)
        {
            ArmCastGlow(color, duration);
            // §M/#4 cast pose: the sim has no cast action (ActorAction frozen),
            // so the View holds a short pose window off the same cast event that
            // drives the glow. Deliberately a touch longer than the glow so the
            // body reads as "casting" rather than twitching.
            //
            // Hoisted OUT of ArmCastGlow's early returns (2026-08-08, §V1): the
            // pose is the §M contract and the glow is decoration, so a
            // non-humanoid rig or a reduced-motion player must still get the
            // pose. Under the old nesting a missing RightHand bone silently
            // cancelled the pose too.
            _castPoseTime = Mathf.Max(_castPoseTime, 0.30f);
            _castPoseArmed = true;
        }

        /// <summary>
        /// §V1 swing beat: the ATTACK-frame half of the amendment. The sim's
        /// Attack/Critical frame opens at ActionTime 0 and its hit window opens
        /// at 0.10 (the swing trail reads the same numbers), so a convergence
        /// that runs out at 0.10 pops exactly as the swing goes live —
        /// "수렴 0.12s → 방출" with the release edge on the sim's own frame.
        ///
        /// Deliberately NOT FlashCastGlow: that one also arms the 0.30 s cast
        /// pose, and a cast pose armed by every swing would outlive the swing
        /// and hold the caster stance through the recovery. §V1 is decoration
        /// in front of the sim's frames, never a second pose authority.
        /// </summary>
        internal const float SwingGlowSeconds = 0.10f;
        static readonly Color SwingGlowColor = new Color(0.953f, 0.349f, 0.173f, 0.85f);
        bool _swingGlowArmed;

        void ArmCastGlow(Color color, float duration)
        {
            // Accessibility (§A5 grammar, ViewPrefs.ReducedMotion): a converging,
            // brightening sphere is exactly the class of motion the pref exists
            // to suppress. The dash-ghost precedent in GameView gates the same
            // way. Judgement is unaffected — this is decoration end to end.
            if (ViewPrefs.ReducedMotion) return;
            if (_castGlow == null)
            {
                Transform anchor = null;
                if (_animator != null && _animator.isHuman)
                    anchor = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (anchor == null) return;   // tint floor for non-humanoid rigs
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                RemovePrimitiveCollider(glow);
                glow.name = "CastGlow";
                glow.transform.SetParent(anchor, false);
                glow.transform.localPosition = new Vector3(0.02f, 0.05f, 0f);
                _castGlowMaterial = ViewWorld.MakeUnlit(color, true);
                var renderer = glow.GetComponent<Renderer>();
                renderer.sharedMaterial = _castGlowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _castGlow = glow.transform;
            }
            _castGlowDuration = _castGlowTime = Mathf.Max(0.05f, duration);
            var c = color; c.a = 0.85f;
            _castGlowMaterial.color = c;
            _castGlow.gameObject.SetActive(true);
        }

        void UpdateCastGlow(float deltaTime)
        {
            if (_castGlow == null || !_castGlow.gameObject.activeSelf) return;
            _castGlowTime -= deltaTime;
            if (_castGlowTime <= 0f)
            {
                _castGlow.gameObject.SetActive(false);
                return;
            }
            // Converge: 0.16 -> 0.055 world units as the release approaches.
            var progress = 1f - _castGlowTime / _castGlowDuration;
            _castGlow.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.055f, progress);
            var color = _castGlowMaterial.color;
            color.a = 0.85f * (0.55f + 0.45f * progress);   // brighten inward
            _castGlowMaterial.color = color;
        }

        void Apply(float simX, float simY, int facing, ActorAction action,
                   float healthFraction, float scale, bool dead, float fadeTime,
                   bool hitFlash)
        {
            transform.position = ViewWorld.ToWorld(simX, simY);

            // Original flips the sprite; in 3D we rotate the model yaw.
            // §M1: display yaw follows the movement delta snapped to 16
            // directions (22.5°) so joystick diagonals read correctly. Attack
            // frames snap to the sim's authoritative ±1 facing — the forward
            // arc (dx*facing >= -18) stays visually honest. The sim's Facing
            // contract is untouched; this is presentation only.
            if (!float.IsNaN(_gazeYaw))
            {
                _targetYaw = _gazeYaw;   // G1 combat gaze (16-dir, M1 grammar)
            }
            else if (action == ActorAction.Attack || action == ActorAction.Critical
                || float.IsNaN(_prevSimX))
            {
                _targetYaw = facing >= 0 ? 90f : 270f;
            }
            else
            {
                var deltaX = simX - _prevSimX;
                var deltaY = simY - _prevSimY;
                // 0.25 sq-units gate: real 60 Hz move steps are ~3.6 u, float
                // noise and tick-less frames stay below it (idle keeps yaw).
                if (deltaX * deltaX + deltaY * deltaY > 0.25f)
                {
                    // Sim +x → world +x (yaw 90°); sim +y is screen-down → world -z (yaw 180°).
                    var yaw = Mathf.Atan2(deltaX, -deltaY) * Mathf.Rad2Deg;
                    _targetYaw = Mathf.Round(yaw / 22.5f) * 22.5f;
                }
            }
            _prevSimX = simX;
            _prevSimY = simY;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, _targetYaw, 720f * Time.deltaTime);
            _model.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            if (dead)
            {
                if (!_dead)
                {
                    _dead = true;
                    _deathPop = 0.09f;   // kill pop (spec #4)
                    // Death returns below WITHOUT reaching the flash decay, so a
                    // flash live on the killing blow would never expire. Release
                    // it here: FlashLive must not stay true through the fade, or
                    // the boss catalog tint (which yields to it, §K3) is
                    // suppressed for the whole death animation.
                    _flashTime = 0f;
                    if (_animator != null && _animator.isActiveAndEnabled)
                    {
                        _animator.SetInteger(ActionParam, (int)ActorAction.Die);
                        _animator.speed = 1f;   // §M2: death never inherits swing pacing
                    }

                }
                // Shrink fade (0.34 s) — cheaper than URP transparent conversion.
                // Kill pop: brief 1.18x punch on the death frame, damped by the
                // actor scale so the 1.6x boss does not balloon (spec #4).
                var f = Mathf.Clamp01(fadeTime / SimConfig.EnemyFade);
                var pop = 1f + (0.18f / Mathf.Max(1f, scale)) * Mathf.Clamp01(_deathPop / 0.09f);
                _deathPop -= Time.deltaTime;
                transform.localScale = Vector3.one * (_baseScale * (0.4f + 0.6f * f) * pop);
                if (_healthRoot.gameObject.activeSelf) _healthRoot.gameObject.SetActive(false);
                return;
            }
            _dead = false;
            transform.localScale = Vector3.one * (_baseScale * scale);

            // §M/#9 + #4: pose selection is pure — extracted so it can be pinned
            // exhaustively instead of inferred from screenshots.
            // Arm-then-decay would burn the arming frame's delta out of a window
            // that has not been shown yet, so each window skips exactly one decay:
            // the sync that opened it. Same rule as the hit flash below.
            if (_castPoseArmed) _castPoseArmed = false;
            else if (_castPoseTime > 0f) _castPoseTime -= Time.deltaTime;
            if (_knockbackArmed) _knockbackArmed = false;
            else if (_knockbackTime > 0f) _knockbackTime -= Time.deltaTime;
            if (_roarArmed) _roarArmed = false;
            else if (_roarTime > 0f) _roarTime -= Time.deltaTime;

            var actionValue = ResolveActionValue(
                action, _comboTier, _castPoseTime > 0f, _knockbackTime > 0f, _roarTime > 0f);
            if (actionValue != _lastActionValue && _animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger(ActionParam, actionValue);
                // §M2: swings are time-scaled into the sim's window; every
                // other pose runs at its authored pace. Assigned on the SAME
                // edge as the value, so leaving a swing restores speed 1.
                _animator.speed = ResolvePoseSpeed(actionValue);
                _lastActionValue = actionValue;
                _lastAction = action;
            }


            // A flash gets its FULL duration: the frame that arms it must not
            // immediately spend a delta against it. On a long frame (100 ms vs
            // the 130 ms flash) the old arm-then-decay order burned most of the
            // first frame, making a hit read as a faint smear — and it made
            // FlashLive depend on frame length right after a hit.
            var flashOwnedBlock = _flashTime > 0f;
            if (hitFlash) { _flashTime = 0.13f; _flashDuration = 0.13f; }
            else if (flashOwnedBlock) _flashTime -= Time.deltaTime;
            // Enter whenever the flash owns the block THIS frame - including the
            // frame it expires on, which still has to restore the resting state.
            if (hitFlash || flashOwnedBlock)
            {
                if (_flashTime > 0f)
                {
                    var pulse = Mathf.Clamp01(_flashTime / _flashDuration);
                    _block.SetColor(BaseColorId, Color.Lerp(Color.white, _flashColor, pulse));
                }
                else
                {
                    // Flash over: restore the resting state in the SAME frame
                    // (Clear alone would blink the rank glow off for 1 frame).
                    _block.Clear();
                    if (_equipGlow > 0f)
                        _block.SetColor(BaseColorId,
                            Color.Lerp(Color.white, EliteGold, _equipGlow));
                }
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
                _shieldApplied = false;   // flash write overwrote any cyan
            }
            else if (_shieldTint)
            {
                // Retune R2 shield read — steady cyan, outranks elite gold:
                // "-60% damage taken" is a live tactical fact, the elite
                // marker is permanent and returns the frame coverage ends.
                _block.SetColor(BaseColorId, ShieldCyan);
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
                _shieldApplied = true;
            }
            else if (_eliteTint)
            {
                // Elite gold tint pulse (spec #14) — 1.2 s brightness cycle.
                var glow = 0.85f + 0.3f * Mathf.PingPong(Time.time * 0.83f, 1f);
                _block.SetColor(BaseColorId, new Color(
                    EliteGold.r * glow, EliteGold.g * glow, EliteGold.b * glow));
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
                _shieldApplied = false;
            }
            else if (_equipGlow > 0f)
            {
                // §P2 rank glow: whole-body ember-gold ramp (single material
                // per character — part-split needs P1), 0.8 s soft pulse.
                // Priority: hit flash > shield cyan > elite gold > rank glow.
                var pulse = 0.9f + 0.1f * Mathf.PingPong(Time.time * 2.5f, 1f);
                _block.SetColor(BaseColorId,
                    Color.Lerp(Color.white, EliteGold, _equipGlow * pulse));
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
                _shieldApplied = false;
            }
            else if (_shieldApplied)
            {
                // Shield falling edge with no other block owner: restore the
                // resting state once, or the last cyan write sticks forever
                // (MPBs persist until overwritten).
                _block.Clear();
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
                _shieldApplied = false;
            }

            if (!Mathf.Approximately(healthFraction, _healthFraction))
            {
                _healthFraction = healthFraction;
                var fillScale = _healthFill.transform.localScale;
                fillScale.x = 0.7f * Mathf.Clamp01(healthFraction);
                _healthFill.transform.localScale = fillScale;
                var offset = _healthFill.transform.localPosition;
                offset.x = -0.35f * (1f - Mathf.Clamp01(healthFraction));
                _healthFill.transform.localPosition = new Vector3(offset.x, 0f, -0.001f);
            }
            var wantBar = healthFraction < 0.999f;
            if (_healthRoot.gameObject.activeSelf != wantBar)
                _healthRoot.gameObject.SetActive(wantBar);
        }

        void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            if (_healthRoot != null && _healthRoot.gameObject.activeSelf)
                _healthRoot.rotation = _camera.transform.rotation;
        }

        public void ResetForPool()
        {
            _lastAction = (ActorAction)(-1);
            _dead = false;
            _flashTime = 0f;
            _healthFraction = 1f;
            _lastHealth = float.MaxValue;
            _deathPop = 0f;
            _prevSimX = float.NaN;
            _prevSimY = float.NaN;
            // A pooled companion never inherits the previous tenant's seat: the
            // stale sample reads as one enormous first-frame stride, and the
            // facing flip at SyncCompanion's first line reads it too.
            _companionLastX = 0f;
            _companionLastY = 0f;
            _companionSampled = false;
            _companionMoveHold = 0f;
            _eliteTint = false;
            _shieldTint = false;      // R2: pooled actors never keep a shield read
            _shieldApplied = false;   // pool reset below rewrites the block anyway
            _flashColor = PlayerFlashColor;
            _flashDuration = 0.13f;
            _equipGlow = 0f;
            _comboTier = -1;
            _castPoseTime = 0f;       // §M: pooled actors never keep a cast pose
            _knockbackTime = 0f;      // §M: nor a launch reaction
            _roarTime = 0f;           // §M: nor an entrance roar
            // ...nor a pending "skip one decay" grant from the previous tenant.
            _castPoseArmed = false;
            _knockbackArmed = false;
            _roarArmed = false;
            // §V1: a tenant pooled MID-SWING would otherwise leave the edge
            // latch set, and the next tenant's first swing would show no glow.
            _swingGlowArmed = false;
            if (_castGlow != null) _castGlow.gameObject.SetActive(false);

            _lastActionValue = -1;    // force the next Apply to re-issue the pose
            _elementTint = default;   // §K3: pooled actors never keep a skill color
            _gazeYaw = float.NaN;
            ClearAfterimages();   // pooled actors never keep dash ghosts
            ClearEquipProps();   // §Lane P: pooled actors never keep props
            if (_castGlow != null) _castGlow.gameObject.SetActive(false);   // §V1
            if (_block != null && _renderers != null)
            {
                _block.Clear();
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
            }
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.Rebind();
                _animator.SetInteger(ActionParam, (int)ActorAction.Idle);
                _animator.speed = 1f;   // §M2: a pooled actor never inherits swing pacing
            }

        }
    }
}
