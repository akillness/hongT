// Floating damage numbers (presentation spec #6). World-space TextMesh pool,
// 16 slots, oldest-evict on overflow. Steady-state allocation-free: amounts
// are integer-rounded and memoized in a string cache keyed by value.
using System.Collections.Generic;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class DamageNumberPool : MonoBehaviour
    {
        const int PoolSize = 16;
        const float Lifetime = 0.6f;
        const float RiseSpeed = 1.2f;

        static readonly Color NormalColor = new Color(0.91f, 0.9f, 0.95f);   // ink #e8e6f2
        static readonly Color FinisherColor = new Color(0.87f, 0.78f, 0.41f); // gold #ddc869

        readonly TextMesh[] _texts = new TextMesh[PoolSize];
        readonly float[] _lives = new float[PoolSize];
        readonly Color[] _colors = new Color[PoolSize];
        readonly Dictionary<int, string> _strings = new Dictionary<int, string>(64);
        Camera _camera;
        Font _font;

        void Awake()
        {
            // Same font contract as HudView: bundled Hangul-capable font with
            // the builtin as editor fallback (WebGL has no OS font fallback).
            _font = Resources.Load<Font>("Fonts/HudKorean");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (var i = 0; i < PoolSize; i++)
            {
                var slot = new GameObject("DamageNumber");
                slot.transform.SetParent(transform, false);
                var text = slot.AddComponent<TextMesh>();
                text.font = _font;
                text.fontSize = 46;
                text.characterSize = 0.045f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                var renderer = slot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                slot.SetActive(false);
                _texts[i] = text;
                _lives[i] = 0f;
            }
        }

        /// <summary>Spawn a number above sim (x, y). Finisher ticks render gold.</summary>
        public void Show(float simX, float simY, float amount, bool finisher)
        {
            var rounded = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (!_strings.TryGetValue(rounded, out var label))
            {
                label = rounded.ToString();     // once per distinct value
                _strings[rounded] = label;
            }

            // Free slot first, else evict the oldest (lowest remaining life).
            var slot = -1;
            var oldestLife = float.MaxValue;
            for (var i = 0; i < PoolSize; i++)
            {
                if (_lives[i] <= 0f) { slot = i; break; }
                if (_lives[i] < oldestLife) { oldestLife = _lives[i]; slot = i; }
            }

            var text = _texts[slot];
            text.text = label;
            _colors[slot] = finisher ? FinisherColor : NormalColor;
            text.color = _colors[slot];
            text.transform.position = ViewWorld.ToWorld(simX, simY, 1.9f);
            text.gameObject.SetActive(true);
            _lives[slot] = Lifetime;
        }

        /// <summary>Hide everything immediately (run teardown).</summary>
        public void Clear()
        {
            for (var i = 0; i < PoolSize; i++)
            {
                _lives[i] = 0f;
                if (_texts[i] != null && _texts[i].gameObject.activeSelf)
                    _texts[i].gameObject.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            var rotation = _camera.transform.rotation;
            var dt = Time.deltaTime;
            for (var i = 0; i < PoolSize; i++)
            {
                if (_lives[i] <= 0f) continue;
                _lives[i] -= dt;
                var text = _texts[i];
                if (_lives[i] <= 0f)
                {
                    text.gameObject.SetActive(false);
                    continue;
                }
                text.transform.position += new Vector3(0f, RiseSpeed * dt, 0f);
                text.transform.rotation = rotation;   // billboard
                var color = _colors[i];
                color.a = Mathf.Clamp01(_lives[i] / (Lifetime * 0.35f));
                text.color = color;
            }
        }
    }
}
