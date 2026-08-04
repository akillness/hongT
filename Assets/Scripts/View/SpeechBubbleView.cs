// World-space speech bubble ported from the original SpeechBubbleDirector
// (spec §8). Pool of exactly one: a single world-space Canvas is built lazily
// and reused for every line; Show() replaces whatever is on screen (priority
// arbitration story>ambient is the caller's responsibility — a higher-priority
// arrival simply calls Show again and clears the lower one).
//
// Rendering: billboard quad 1.2 world units above the tracked anchor —
// translucent panel + speaker label + wrapped body text, all code-generated
// with the HudView factory style (no asset dependencies). LateUpdate aligns
// the canvas to the camera, follows the anchor, counts the hold down, and
// fades out over 0.3 s. No per-frame allocation.
using UnityEngine;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        // Hold formula (original): clamp(1.5 + 0.058 * chars, 2.2, 5.2) s.
        const float HoldBase = 1.5f;
        const float HoldPerChar = 0.058f;
        const float HoldMin = 2.2f;
        const float HoldMax = 5.2f;
        const float FadeSeconds = 0.3f;
        const float AnchorLift = 1.2f;        // world units above the anchor

        // Canvas geometry (canvas px; WorldScale maps px -> world units).
        const float WorldScale = 0.006f;
        const float PanelWidth = 460f;
        const float PadX = 16f;
        const float PadTop = 10f;
        const float PadBottom = 12f;
        const float SpeakerHeight = 24f;
        const float SpeakerGap = 4f;

        // Speaker palette (original tints). Boss houses share ember.
        static readonly Color EmberColor = new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f);   // #f3592c
        static readonly Color WardenColor = new Color(0x8F / 255f, 0xE9 / 255f, 0xFF / 255f);  // #8fe9ff
        static readonly Color WatcherColor = new Color(0xDD / 255f, 0xC8 / 255f, 0x69 / 255f); // #ddc869

        Font _font;
        Camera _camera;
        RectTransform _canvasRect;
        CanvasGroup _group;
        Text _speakerText;
        Text _bodyText;
        RectTransform _bodyRect;

        Vector3 _anchor;
        float _holdRemaining;
        float _fadeRemaining;
        bool _active;

        /// <summary>True while a bubble is visible (holding or fading out).</summary>
        public bool Active => _active;

        /// <summary>
        /// Shows a line above <paramref name="worldAnchor"/>, replacing any
        /// bubble currently displayed. Hold defaults to the original formula
        /// clamp(1.5 + 0.058 * chars, 2.2..5.2) s unless a positive override
        /// is supplied.
        /// </summary>
        public void Show(string speaker, string text, Vector3 worldAnchor,
                         float holdSecondsOverride = 0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }
            EnsureBuilt();

            _speakerText.text = speaker ?? string.Empty;
            _speakerText.color = SpeakerColor(speaker);
            _bodyText.text = text;

            // Measure the wrapped body against its fixed width, then grow the
            // panel upward from the anchor (pivot bottom-center).
            var bodyHeight = Mathf.Max(24f, _bodyText.preferredHeight);
            _bodyRect.sizeDelta = new Vector2(PanelWidth - PadX * 2f, bodyHeight);
            var panelHeight = PadTop + SpeakerHeight + SpeakerGap + bodyHeight + PadBottom;
            _canvasRect.sizeDelta = new Vector2(PanelWidth, panelHeight);

            _anchor = worldAnchor;
            _holdRemaining = holdSecondsOverride > 0f
                ? holdSecondsOverride
                : Mathf.Clamp(HoldBase + HoldPerChar * text.Length, HoldMin, HoldMax);
            _fadeRemaining = FadeSeconds;
            _group.alpha = 1f;
            _active = true;
            _canvasRect.gameObject.SetActive(true);
            Place();   // position immediately — no one-frame pop at the stale spot
        }

        /// <summary>Per-frame anchor update while the speaker moves.</summary>
        public void Track(Vector3 worldAnchor)
        {
            _anchor = worldAnchor;
        }

        /// <summary>Clears the bubble instantly (mode exits, higher-priority resets).</summary>
        public void Hide()
        {
            _active = false;
            _holdRemaining = 0f;
            if (_canvasRect != null) _canvasRect.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!_active) return;

            if (_holdRemaining > 0f)
            {
                _holdRemaining -= Time.deltaTime;
            }
            else
            {
                _fadeRemaining -= Time.deltaTime;
                if (_fadeRemaining <= 0f)
                {
                    Hide();
                    return;
                }
                _group.alpha = _fadeRemaining / FadeSeconds;
            }

            Place();
        }

        void Place()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            // Screen-aligned billboard hovering above the anchor.
            _canvasRect.SetPositionAndRotation(
                _anchor + Vector3.up * AnchorLift,
                _camera.transform.rotation);
        }

        // ------------------------------------------------------------- build --
        void EnsureBuilt()
        {
            if (_canvasRect != null) return;

            // Same subset font contract as HudView: LegacyRuntime.ttf has no
            // Hangul glyphs, so the Korean subset is required on WebGL.
            _font = Resources.Load<Font>("Fonts/HudKorean");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("SpeechBubble");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _group = canvasObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(PanelWidth, 96f);
            _canvasRect.pivot = new Vector2(0.5f, 0f);   // grows upward from anchor
            _canvasRect.localScale = new Vector3(WorldScale, WorldScale, WorldScale);

            // Translucent backdrop filling the canvas.
            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.04f, 0.09f, 0.78f);
            panelImage.raycastTarget = false;
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            _speakerText = MakeText(canvasObject.transform, "Speaker", 18,
                TextAnchor.MiddleLeft, HorizontalWrapMode.Overflow);
            var speakerRect = _speakerText.rectTransform;
            speakerRect.anchoredPosition = new Vector2(PadX, -PadTop);
            speakerRect.sizeDelta = new Vector2(PanelWidth - PadX * 2f, SpeakerHeight);

            _bodyText = MakeText(canvasObject.transform, "Body", 17,
                TextAnchor.UpperLeft, HorizontalWrapMode.Wrap);
            _bodyText.color = new Color(0.92f, 0.94f, 1f);
            _bodyRect = _bodyText.rectTransform;
            _bodyRect.anchoredPosition = new Vector2(PadX, -(PadTop + SpeakerHeight + SpeakerGap));
            _bodyRect.sizeDelta = new Vector2(PanelWidth - PadX * 2f, 48f);

            canvasObject.SetActive(false);
        }

        Text MakeText(Transform parent, string name, int size, TextAnchor anchor,
                      HorizontalWrapMode wrap)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            return text;
        }

        static Color SpeakerColor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return WatcherColor;
            // Boss houses: CINDER WARDEN / VEIL TACTICIAN / GATE SOVEREIGN.
            if (speaker.StartsWith("CINDER", System.StringComparison.Ordinal) ||
                speaker.StartsWith("VEIL", System.StringComparison.Ordinal) ||
                speaker.StartsWith("GATE", System.StringComparison.Ordinal))
                return EmberColor;
            if (speaker.StartsWith("DUSK", System.StringComparison.Ordinal))
                return WardenColor;
            return WatcherColor;   // watcher narration and anything ambient
        }
    }
}
