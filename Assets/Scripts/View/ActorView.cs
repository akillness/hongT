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
                Destroy(instance.GetComponent<Collider>());
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

        void BuildHealthBar()
        {
            _healthRoot = new GameObject("HealthBar").transform;
            _healthRoot.SetParent(transform, false);
            _healthRoot.localPosition = new Vector3(0f, 2.05f, 0f);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(_healthRoot, false);
            back.transform.localScale = new Vector3(0.74f, 0.085f, 1f);
            back.GetComponent<Renderer>().sharedMaterial =
                ViewWorld.MakeUnlit(new Color(0.05f, 0.04f, 0.09f, 0.9f), true);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fill.GetComponent<Collider>());
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

        /// <summary>Companion follower (spec §4): position + attack pose, no bars.</summary>
        public void SyncCompanion(float simX, float simY, bool attacking)
        {
            var facing = simX >= _companionLastX ? 1 : -1;
            _companionLastX = simX;
            Apply(simX, simY, facing,
                  attacking ? ActorAction.Attack : ActorAction.Move,
                  1f, 0.92f, false, 0f, false);
        }

        /// <summary>Elite marker (spec #14): pulsing gold tint through the
        /// shared MaterialPropertyBlock path. Cleared by ResetForPool.</summary>
        public void SetEliteTint(bool on) => _eliteTint = on;

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
            _targetYaw = facing >= 0 ? 90f : 270f;
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

            if (hitFlash) _flashTime = 0.13f;
            if (_flashTime > 0f)
            {
                _flashTime -= Time.deltaTime;
                if (_flashTime > 0f)
                {
                    var pulse = Mathf.Clamp01(_flashTime / 0.13f);
                    _block.SetColor(BaseColorId, Color.Lerp(Color.white, _flashColor, pulse));
                }
                else
                {
                    // Flash over: drop the override so prefab tints survive.
                    _block.Clear();
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
            _eliteTint = false;
            _flashColor = PlayerFlashColor;
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
