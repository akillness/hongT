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
        }

        public void SyncEnemy(in EnemyState state)
        {
            Apply(state.X, state.Y, state.Facing, state.Action,
                  state.MaxHealth > 0f ? state.Health / state.MaxHealth : 0f,
                  state.Scale, state.Dead, state.FadeTime, false);
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
                    if (_animator != null && _animator.isActiveAndEnabled)
                        _animator.SetInteger(ActionParam, (int)ActorAction.Die);
                }
                // Shrink fade (0.34 s) — cheaper than URP transparent conversion.
                var f = Mathf.Clamp01(fadeTime / SimConfig.EnemyFade);
                transform.localScale = Vector3.one * (_baseScale * (0.4f + 0.6f * f));
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
                var pulse = Mathf.Clamp01(_flashTime / 0.13f);
                _block.SetColor(BaseColorId, Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), pulse));
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
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.Rebind();
                _animator.SetInteger(ActionParam, (int)ActorAction.Idle);
            }
        }
    }
}
