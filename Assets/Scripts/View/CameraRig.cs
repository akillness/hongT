// Keeps the arena framed across aspect ratios and applies small hit/nova
// shakes. Reference aspect 3:2 (original 1536x1024 court).
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        const float BaseFov = 32f;
        const float ReferenceAspect = 1.5f;

        Camera _camera;
        Vector3 _basePosition;
        Quaternion _baseRotation;
        float _shakeTime;
        float _shakeDuration;
        float _shakeAmplitude;
        float _lastAspect;

        void Start()
        {
            _camera = Camera.main;
            if (_camera == null) return;
            _basePosition = _camera.transform.position;
            _baseRotation = _camera.transform.rotation;
            ApplyAspect(true);
        }

        void ApplyAspect(bool force)
        {
            var aspect = _camera.aspect;
            if (!force && Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;
            // Narrower than 3:2 -> widen FOV so the horizontal arena still fits.
            var widen = Mathf.Clamp(ReferenceAspect / Mathf.Max(0.5f, aspect), 1f, 2f);
            _camera.fieldOfView = BaseFov * widen;
        }

        public void OnEvents(SimEvents events)
        {
            if ((events & SimEvents.NovaCast) != 0) Shake(0.2f, 0.06f);
            else if ((events & SimEvents.PlayerDamaged) != 0) Shake(0.12f, 0.045f);
        }

        void Shake(float duration, float amplitude)
        {
            _shakeDuration = duration;
            _shakeTime = duration;
            _shakeAmplitude = amplitude;
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            ApplyAspect(false);
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.deltaTime;
                var falloff = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
                var offset = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 37f, 0.3f) - 0.5f),
                    (Mathf.PerlinNoise(0.7f, Time.time * 41f) - 0.5f),
                    0f) * (2f * _shakeAmplitude * falloff);
                _camera.transform.SetPositionAndRotation(_basePosition + offset, _baseRotation);
            }
            else if (_camera.transform.position != _basePosition)
            {
                _camera.transform.SetPositionAndRotation(_basePosition, _baseRotation);
            }
        }
    }
}
