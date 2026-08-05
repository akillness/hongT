// One actor (player / enemy / boss). Maps sim state to transform, Animator,
// billboarded health bar, and death fade. No per-frame allocations.
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
        float _lastHealth = float.MaxValue;   // enemy health-delta cache (spec #5)
        float _deathPop;                      // kill pop timer (spec #4)
        Color _elementTint;                   // §K3 skill-element hit color (a=0 off)
        bool _eliteTint;                      // gold pulse marker (spec #14)
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
        const float RoarDuration = 1.1f;
        float _castPoseTime;                  // §M/#4 cast pose window
        float _knockbackTime;                 // §M launch reaction window
        float _roarTime;                      // §M boss entrance roar window
        float _flashDuration = 0.13f;         // flash fade denominator
        float _gazeYaw = float.NaN;           // G1 combat gaze yaw (companion)

        // Original: depth scale 0.62..1.0 by screen y. NOT applied here — real
        // 3D perspective replaces it (docs/SIM_SPEC.md coordinate contract).

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
            view._model = instance.transform;
            view._animator = instance.GetComponentInChildren<Animator>();
            view._renderers = instance.GetComponentsInChildren<Renderer>();
            view._block = new MaterialPropertyBlock();
            view._baseScale = baseScale;
            view._camera = Camera.main;
            view.BuildHealthBar();
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
            back.GetComponent<Renderer>().sharedMaterial =
                ViewWorld.MakeUnlit(new Color(0.05f, 0.04f, 0.09f, 0.9f), true);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemovePrimitiveCollider(fill);
            fill.transform.SetParent(_healthRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            fill.transform.localScale = new Vector3(0.7f, 0.055f, 1f);
            _healthFill = fill.GetComponent<Renderer>();
            _healthFill.sharedMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.6f, 0.32f), false);
        }

        public void SyncPlayer(in PlayerState state)
        {
            Apply(state.X, state.Y, state.Facing, state.Action,
                  state.Health / SimConfig.PlayerMaxHealth, 1f, false, 0f,
                  state.DamageCooldown > SimConfig.PlayerHitGrace - 0.16f);
            // Swing trail (spec #8): union of the arena (0.167-0.333) and
            // dungeon combo (0.10-0.30) active windows — pure decoration,
            // hit judgement stays in the sim.
            if (_swingTrail != null)
                _swingTrail.emitting = state.Action == ActorAction.Attack
                    && state.ActionTime >= 0.10f && state.ActionTime < 0.34f;
            UpdateCastGlow(Time.deltaTime);   // §V1 convergence step
        }

        /// <summary>
        /// Returns the damage taken this frame (spec #5/#6). The sim never
        /// exposes per-enemy DidDamage, so a health drop between frames IS the
        /// hit signal. First sync after pooling never counts as a hit.
        /// </summary>
        public float SyncEnemy(in EnemyState state)
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
            // at any frame rate.
            if (hit && !float.IsNaN(_prevSimX) && Time.deltaTime > 0f)
            {
                var stepX = state.X - _prevSimX;
                var stepY = state.Y - _prevSimY;
                var speed = Mathf.Sqrt(stepX * stepX + stepY * stepY) / Time.deltaTime;
                if (speed > 300f) _knockbackTime = HackSpec.ComboKnockbackTime;
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

        float _companionLastX;

        /// <summary>Companion follower (§4 + G1 combat gaze): position from the
        /// sim; pose/facing prioritized combat-first. attackFacing wins while
        /// the strike shows; combatFacing (nearest enemy in range) holds the
        /// gaze between strikes; movement dir is the peace-time fallback; near
        /// the player with no target the companion rests in Idle.</summary>
        public void SyncCompanion(float simX, float simY, int attackFacing, bool attacking,
                                  float gazeYaw = float.NaN, bool restIdle = false)
        {
            var moveFacing = simX >= _companionLastX ? 1 : -1;
            var facing = attackFacing != 0 ? attackFacing : moveFacing;
            // G1(c): an in-range enemy owns the yaw even while the body keeps
            // following the player — without this, M1's movement-delta yaw
            // wins during Move and the companion stares at its travel path.
            // Full 16-direction angle (M1's 22.5° grammar), not ±1 snap;
            // the attack frame keeps the sim's authoritative ±1 facing.
            _gazeYaw = attackFacing != 0
                ? (attackFacing > 0 ? 90f : 270f)
                : gazeYaw;
            _companionLastX = simX;
            var action = attacking ? ActorAction.Attack
                : restIdle ? ActorAction.Idle
                : ActorAction.Move;
            Apply(simX, simY, facing, action, 1f, 0.92f, false, 0f, false);
        }

        /// <summary>Elite marker (spec #14): pulsing gold tint through the
        /// shared MaterialPropertyBlock path. Cleared by ResetForPool.</summary>
        public void SetEliteTint(bool on) => _eliteTint = on;

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
                    Destroy(_equipProps[slot]);
                    _equipProps[slot] = null;
                }
                if (band == 0) continue;
                var bone = _animator.GetBoneTransform(PropBones[slot]);
                if (bone == null) continue;
                var prefab = Resources.Load<GameObject>(
                    $"Props/equip-{PropSlots[slot]}-{(band == 2 ? "fine" : "basic")}");
                if (prefab == null) continue;   // asset missing -> tint floor
                var prop = Instantiate(prefab, bone, false);
                prop.name = $"EquipProp-{PropSlots[slot]}";
                ApplyPropPose(prop.transform, slot);
                _equipProps[slot] = prop;
            }
        }

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
                if (_equipProps[slot] != null) Destroy(_equipProps[slot]);
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

        /// <summary>§M: starts the entrance roar window. Called from the
        /// BossSpawned event, which is where the intro letterbox already
        /// triggers — the sim never poses this, because a sim-side Show would
        /// be overwritten by the AI on the very next tick.</summary>
        public void PlayRoar()
        {
            _roarTime = RoarDuration;
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
            if (_castGlow == null)
            {
                Transform anchor = null;
                if (_animator != null && _animator.isHuman)
                    anchor = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (anchor == null) return;   // tint floor for non-humanoid rigs
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider = glow.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
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
            // §M/#4 cast pose: the sim has no cast action (ActorAction frozen),
            // so the View holds a short pose window off the same cast event that
            // drives the glow. Deliberately a touch longer than the glow so the
            // body reads as "casting" rather than twitching.
            _castPoseTime = Mathf.Max(_castPoseTime, 0.30f);
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
                        _animator.SetInteger(ActionParam, (int)ActorAction.Die);
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
            if (_castPoseTime > 0f) _castPoseTime -= Time.deltaTime;
            if (_knockbackTime > 0f) _knockbackTime -= Time.deltaTime;
            if (_roarTime > 0f) _roarTime -= Time.deltaTime;
            var actionValue = ResolveActionValue(
                action, _comboTier, _castPoseTime > 0f, _knockbackTime > 0f, _roarTime > 0f);
            if (actionValue != _lastActionValue && _animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger(ActionParam, actionValue);
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
            }
            else if (_eliteTint)
            {
                // Elite gold tint pulse (spec #14) — 1.2 s brightness cycle.
                var glow = 0.85f + 0.3f * Mathf.PingPong(Time.time * 0.83f, 1f);
                _block.SetColor(BaseColorId, new Color(
                    EliteGold.r * glow, EliteGold.g * glow, EliteGold.b * glow));
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
            }
            else if (_equipGlow > 0f)
            {
                // §P2 rank glow: whole-body ember-gold ramp (single material
                // per character — part-split needs P1), 0.8 s soft pulse.
                // Priority: hit flash > elite gold > rank glow (spec #14 rule).
                var pulse = 0.9f + 0.1f * Mathf.PingPong(Time.time * 2.5f, 1f);
                _block.SetColor(BaseColorId,
                    Color.Lerp(Color.white, EliteGold, _equipGlow * pulse));
                for (var i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
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
            _eliteTint = false;
            _flashColor = PlayerFlashColor;
            _flashDuration = 0.13f;
            _equipGlow = 0f;
            _comboTier = -1;
            _castPoseTime = 0f;       // §M: pooled actors never keep a cast pose
            _knockbackTime = 0f;      // §M: nor a launch reaction
            _roarTime = 0f;           // §M: nor an entrance roar
            _lastActionValue = -1;    // force the next Apply to re-issue the pose
            _elementTint = default;   // §K3: pooled actors never keep a skill color
            _gazeYaw = float.NaN;
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
            }
        }
    }
}
