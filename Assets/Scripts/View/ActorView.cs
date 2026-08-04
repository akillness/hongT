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
        bool _eliteTint;                      // gold pulse marker (spec #14)
        Color _flashColor = PlayerFlashColor;
        TrailRenderer _swingTrail;            // player-only (spec #8)
        // 16-direction display yaw (§M1): previous sim position, NaN = unseeded.
        float _prevSimX = float.NaN, _prevSimY = float.NaN;
        float _equipGlow;                     // §P2 rank glow (player only)
        int _comboTier = -1;                  // §C1 trail tier cache
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
            if (hit) _flashColor = EnemyFlashColor;
            Apply(state.X, state.Y, state.Facing, state.Action,
                  state.MaxHealth > 0f ? state.Health / state.MaxHealth : 0f,
                  state.Scale, state.Dead, state.FadeTime, hit);
            return damage;
        }

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

        /// <summary>§C1: combo-tier weapon trail — hits 1/2 ember (1x/1.5x width),
        /// finisher gold (2x). Pure decoration; hit windows stay sim-owned.</summary>
        public void SetComboTier(int tier)
        {
            if (_swingTrail == null || tier == _comboTier) return;
            _comboTier = tier;
            var c = tier >= 2 ? EliteGold : new Color(0.953f, 0.349f, 0.173f);
            _swingTrail.startWidth = 0.06f * (tier <= 0 ? 1f : tier == 1 ? 1.5f : 2f);
            _swingTrail.startColor = new Color(c.r, c.g, c.b, 0.85f);
            _swingTrail.endColor = new Color(c.r, c.g, c.b, 0f);
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

            if (action != _lastAction && _animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger(ActionParam, (int)action);
                _lastAction = action;
            }

            if (hitFlash) { _flashTime = 0.13f; _flashDuration = 0.13f; }
            if (_flashTime > 0f)
            {
                _flashTime -= Time.deltaTime;
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
            _gazeYaw = float.NaN;
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
