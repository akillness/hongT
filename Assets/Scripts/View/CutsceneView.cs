// Loading-screen cutscene overlay (spec §8 story presentation extension).
// A single full-screen ScreenSpaceOverlay canvas built lazily and reused for
// every stage entry: it covers the screen with the pre-rendered scene image
// (Resources/Scenes/*), stamps the stage kicker/title and the watcher
// narration line on top, then fades out — acting as the run's loading screen
// while GameView.Begin initializes the fresh simulation underneath.
//
// Rendering follows the HudView / SpeechBubbleView factory grammar (no scene
// asset dependency beyond the sprite + Korean subset font). Timing runs on
// UNSCALED time so hit-stop / slow-mo on the frame Begin() fires can never
// stall the fade. No per-frame allocation once built.
using UnityEngine;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed class CutsceneView : MonoBehaviour
    {
        const float DefaultHold = 2.6f;
        const float FadeInSeconds = 0.35f;
        const float FadeOutSeconds = 0.5f;
        const int SortingOrder = 500;   // above the HUD (0) and every juice overlay

        Font _font;
        Canvas _canvas;
        CanvasGroup _group;
        Image _backdrop;
        Image _scene;
        Text _kickerText;
        Text _titleText;
        Text _narrationText;

        float _holdRemaining;
        float _fadeInRemaining;
        float _fadeOutRemaining;
        bool _active;

        /// <summary>True while the loading cutscene is fading in, holding, or fading out.</summary>
        public bool Active => _active;

        /// <summary>
        /// Shows the loading cutscene for <paramref name="spriteName"/>
        /// (Resources/Scenes/&lt;spriteName&gt;) with the stage kicker/title and a
        /// narration line, replacing any cutscene currently on screen. A missing
        /// sprite degrades to the dark backdrop + text (never throws).
        /// </summary>
        public void Show(string spriteName, string kicker, string title, string narration,
                         float holdOverride = 0f)
        {
            EnsureBuilt();

            var sprite = string.IsNullOrEmpty(spriteName)
                ? null
                : Resources.Load<Sprite>("Scenes/" + spriteName);
            _scene.sprite = sprite;
            _scene.enabled = sprite != null;

            _kickerText.text = kicker ?? string.Empty;
            _titleText.text = title ?? string.Empty;
            _narrationText.text = narration ?? string.Empty;

            _holdRemaining = holdOverride > 0f ? holdOverride : DefaultHold;
            _fadeInRemaining = FadeInSeconds;
            _fadeOutRemaining = FadeOutSeconds;
            _active = true;
            _group.alpha = 0f;
            _group.blocksRaycasts = true;   // swallow taps while the screen is covered
            _canvas.gameObject.SetActive(true);
        }

        /// <summary>Clears the cutscene instantly (mode exits, higher-priority resets).</summary>
        public void Hide()
        {
            _active = false;
            _holdRemaining = 0f;
            _fadeInRemaining = 0f;
            _fadeOutRemaining = 0f;
            if (_canvas != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _canvas.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!_active) return;
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>Fade state machine, dt injected so EditMode can drive it
        /// (Time.unscaledDeltaTime reports ~0 in batchmode).</summary>
        internal void Step(float dt)
        {
            if (!_active) return;

            if (_fadeInRemaining > 0f)
            {
                _fadeInRemaining -= dt;
                _group.alpha = FadeInSeconds > 0f
                    ? Mathf.Clamp01(1f - _fadeInRemaining / FadeInSeconds)
                    : 1f;
                if (_fadeInRemaining > 0f) return;
                _group.alpha = 1f;
            }

            if (_holdRemaining > 0f)
            {
                _holdRemaining -= dt;
                return;
            }

            _fadeOutRemaining -= dt;
            if (_fadeOutRemaining <= 0f)
            {
                Hide();
                return;
            }
            _group.alpha = Mathf.Clamp01(_fadeOutRemaining / FadeOutSeconds);
        }

        // ------------------------------------------------------------- build --
        void EnsureBuilt()
        {
            if (_canvas != null) return;

            // Same subset font contract as HudView: LegacyRuntime.ttf carries no
            // Hangul glyphs and WebGL has no OS font fallback.
            _font = ViewTypography.ResolveFont();

            var canvasObject = new GameObject("Cutscene");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            _group = canvasObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Opaque black backdrop (letterbox + safety net behind the image).
            _backdrop = FullScreenImage(canvasObject.transform, "Backdrop");
            _backdrop.color = Color.black;
            _backdrop.raycastTarget = true;

            // Scene image, fitted to cover the backdrop.
            _scene = FullScreenImage(canvasObject.transform, "Scene");
            _scene.color = Color.white;
            _scene.preserveAspect = true;
            _scene.raycastTarget = false;

            // Bottom scrim so text reads over any bright frame region.
            var scrim = new GameObject("Scrim");
            scrim.transform.SetParent(canvasObject.transform, false);
            var scrimImage = scrim.AddComponent<Image>();
            scrimImage.color = new Color(0.02f, 0.02f, 0.05f, 0.72f);
            scrimImage.raycastTarget = false;
            var scrimRect = scrim.GetComponent<RectTransform>();
            scrimRect.anchorMin = new Vector2(0f, 0f);
            scrimRect.anchorMax = new Vector2(1f, 0.32f);
            scrimRect.offsetMin = Vector2.zero;
            scrimRect.offsetMax = Vector2.zero;

            _kickerText = MakeLabel(canvasObject.transform, "Kicker", 18, TextAnchor.LowerCenter,
                FontStyle.Bold, new Color(0.87f, 0.78f, 0.41f));
            Anchor(_kickerText.rectTransform, 0.5f, 0.22f, new Vector2(0, 0), new Vector2(1000, 26));

            _titleText = MakeLabel(canvasObject.transform, "Title", 40, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Color(0.97f, 0.94f, 0.9f));
            Anchor(_titleText.rectTransform, 0.5f, 0.14f, new Vector2(0, 0), new Vector2(1100, 56));

            _narrationText = MakeLabel(canvasObject.transform, "Narration", 20, TextAnchor.UpperCenter,
                FontStyle.Normal, new Color(0.75f, 0.82f, 1f, 0.92f));
            Anchor(_narrationText.rectTransform, 0.5f, 0.055f, new Vector2(0, 0), new Vector2(980, 60));
            _narrationText.horizontalOverflow = HorizontalWrapMode.Wrap;

            canvasObject.SetActive(false);
        }

        static Image FullScreenImage(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        Text MakeLabel(Transform parent, string name, int size, TextAnchor anchor,
                       FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            ViewTypography.Configure(text, _font, size, anchor);
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Anchor(RectTransform rect, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(ax, ay);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }
    }
}
