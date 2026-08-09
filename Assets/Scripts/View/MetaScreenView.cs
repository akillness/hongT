// Full-screen lobby reference surface for the campaign map and controls.
// Built on the lobby object so its lifetime stays with the lobby while its
// canvas sorts above the compact map and other lobby panels.
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed class MetaScreenView : MonoBehaviour
    {
        // --- three-token dark-fantasy palette --------------------------------
        static readonly Color Charcoal = new Color(0.035f, 0.038f, 0.062f, 0.97f);
        static readonly Color Panel = new Color(1f, 1f, 1f, 0.035f);
        static readonly Color Border = new Color(128f / 255f, 216f / 255f, 255f / 255f, 0.30f);
        static readonly Color Cyan = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f);
        static readonly Color Ember = new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f);
        static readonly Color Gold = new Color(0xDD / 255f, 0xC8 / 255f, 0x69 / 255f);
        static readonly Color Ink = new Color(0.92f, 0.94f, 1f);
        static readonly Color InkDim = new Color(0.62f, 0.66f, 0.8f);
        static readonly Color ButtonBack = new Color(0.16f, 0.13f, 0.24f, 0.9f);

        internal const int TabMap = 0, TabControls = 1;
        internal const int TabCount = 2;
        static readonly string[] TabNames = { "지도", "조작" };
        static readonly string[] TabKickers = { "MAP", "CONTROLS" };

        /// <summary>Dungeon bindings, transcribed from InputAdapter.ReadKeyboard's
        /// Profile.Dungeon branch. A control screen that guesses is worse than no
        /// control screen, so every row here has a matching latch there.</summary>
        static readonly string[] ControlRows =
        {
            "이동 • WASD 또는 방향키 • 터치는 좌측 조이스틱",
            "공격 • Space 길게 누르면 연타",
            "질주 • Shift",
            "균열 화살 Q • 묘지 파동 E • 잿불 노바 R • 공허 방패 F",
            "동료 방어 태세 G • 동료 복귀 H • 동료 특기 V",
            "명령 콘솔 열기 Enter • 닫기 ESC",
        };

        Font _font;
        GameObject _root;
        CampaignMapView _map;
        System.Action _onClosed;

        GameObject[] _tabContents;
        Image[] _tabPlates;
        Text[] _tabLabels;
        int _tab = TabMap;


        Text _relicText, _pointText, _hintText;

        public bool IsOpen => _root != null && _root.activeSelf;
        internal int ActiveTab => _tab;
        internal CampaignMapView Map => _map;

        CanvasScaler _scaler;
        GameObject _tabBar;
        int _lastScreenWidth, _lastScreenHeight;
        readonly System.Collections.Generic.List<RectTransform> _contentFrames = new();

        /// <summary>Single-row tab bar height. Landscape uses this; portrait
        /// stacks a second row of the same height beneath it.</summary>
        internal const float TabBarHeight = 74f;
        /// <summary>Portrait bar: tabs on row 1, currency on row 2.</summary>
        internal const float TabBarHeightPortrait = TabBarHeight * 2f;

        /// <summary>Test seam: effective canvas width (screen width divided by
        /// the scaler factor) computed by the last ApplyLayout pass. EditMode
        /// cannot resize the editor window, so orientation is pushed in rather
        /// than read from Screen (HudView L386 grammar).</summary>
        internal float LastEffectiveWidth { get; private set; }
        /// <summary>Test seam: true when the last pass chose the portrait
        /// two-row bar.</summary>
        internal bool LastPortrait { get; private set; }

        /// <summary>Builds the map/controls screen hidden.</summary>
        public void Build(Font font, in CampaignData data, System.Action onClosed = null)
        {
            DestroyBuiltRoot();

            _font = font;
            _onClosed = onClosed;

            var canvasObject = new GameObject("MetaScreen");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the lobby (5) and its staging, below the intro reel (520).
            canvas.sortingOrder = 12;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            _scaler = scaler;
            canvasObject.AddComponent<GraphicRaycaster>();
            _root = canvasObject;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }

            // Full-bleed charcoal ground. raycastTarget stays ON so a tap that
            // misses a control cannot fall through to the lobby underneath.
            var backdrop = Panelled(canvasObject.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Charcoal);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().raycastTarget = true;

            var root = canvasObject.transform;
            BuildTabBar(root);
            BuildHintBar(root);

            _tabContents = new GameObject[TabCount];
            _tabContents[TabMap] = BuildMapTab(root);
            _tabContents[TabControls] = BuildControlsTab(root);

            SelectTab(TabMap);
            Refresh(data);
            _root.SetActive(false);
        }
        void DestroyBuiltRoot()
        {
            var builtRoot = _root;
            _root = null;
            _map = null;
            _tabContents = null;
            _tabPlates = null;
            _tab = TabMap;
            _onClosed = null;
            _tabLabels = null;
            _scaler = null;
            _tabBar = null;
            _relicText = null;
            _pointText = null;
            _hintText = null;
            _contentFrames.Clear();
            _lastScreenWidth = 0;
            _lastScreenHeight = 0;
            LastEffectiveWidth = 0f;
            LastPortrait = false;

            if (builtRoot == null) return;
            builtRoot.SetActive(false);
            builtRoot.transform.SetParent(null, false);
            if (Application.isPlaying)
                Destroy(builtRoot);
            else
                DestroyImmediate(builtRoot);
        }

        public void Show(in CampaignData data) => Show(in data, TabMap);

        /// <param name="tab">Which tab to land on. The lobby minimap opens
        /// straight onto <see cref="TabMap"/> — a player who taps a map expects
        /// the map, not whatever tab was open last time.</param>
        public void Show(in CampaignData data, int tab)
        {
            if (_root == null) return;
            SelectTab(tab);
            Refresh(data);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root == null || !_root.activeSelf) return;
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        /// <summary>Re-reads one save into every already-built widget.</summary>
        public void Refresh(in CampaignData data)
        {
            if (_root == null) return;

            _relicText.text = $"유물 {data.Relics}";
            _pointText.text = $"포인트 {data.Points}";
            _map?.Refresh(in data);
        }

        void Update()
        {
            if (!IsOpen) return;
            // Orientation first: a rotation must reflow the bar before anything
            // else reads its rects this frame.
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
                ApplyLayout(Screen.width, Screen.height);
            _map?.Tick(Time.unscaledTime);

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Hide(); return; }
            if (keyboard.tabKey.wasPressedThisFrame) SelectTab((_tab + 1) % TabCount);
        }


        void SelectTab(int index)
        {
            _tab = index >= 0 && index < TabCount ? index : TabMap;
            for (var i = 0; i < TabCount; i++)
            {
                _tabContents[i].SetActive(i == _tab);
                PlateStateful(_tabPlates[i], i == _tab);
                _tabLabels[i].color = i == _tab ? Gold : InkDim;
            }
            _hintText.text = "탭 전환 Tab • 닫기 ESC";
        }




        // ------------------------------------------------------------ tab bar --
        void BuildTabBar(Transform root)
        {
            var bar = Panelled(root, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.05f));
            var rect = bar.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0, -TabBarHeight);
            rect.offsetMax = Vector2.zero;
            BottomLine(bar.transform);
            _tabBar = bar;

            _tabPlates = new Image[TabCount];
            _tabLabels = new Text[TabCount];
            for (var i = 0; i < TabCount; i++)
            {
                var index = i;
                // 132 x 52 clears the 44 CSS px touch floor on both axes at the
                // worst audited phone scale (0.488 px/u -> 64 x 25... height is
                // the known project-wide debt, width is comfortably over).
                var tab = TextButton(bar.transform, new Vector2(0, 1),
                    new Vector2(20 + i * 140, -12), new Vector2(132, 52), "", 17,
                    () => SelectTab(index), plated: false);
                _tabPlates[i] = tab.GetComponent<Image>();
                _tabLabels[i] = tab.GetComponentInChildren<Text>();
                _tabLabels[i].text = TabNames[i];
                _tabLabels[i].rectTransform.offsetMin = new Vector2(0f, 14f);
                var kicker = Label(tab.transform, 0, 0, 132, 14, TabKickers[i], 9,
                    TextAnchor.MiddleCenter);
                kicker.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
                var kickerRect = kicker.rectTransform;
                kickerRect.anchorMin = new Vector2(0f, 0f);
                kickerRect.anchorMax = new Vector2(1f, 0f);
                kickerRect.pivot = new Vector2(0.5f, 0f);
                kickerRect.anchoredPosition = new Vector2(0f, 5f);
                kickerRect.sizeDelta = new Vector2(0f, 14f);
            }

            _relicText = Label(bar.transform, -320, -14, 150, 30, "유물 0", 18, TextAnchor.MiddleRight);
            AnchorTopRight(_relicText.rectTransform);
            _relicText.color = Gold;
            _pointText = Label(bar.transform, -170, -14, 150, 30, "포인트 0", 18, TextAnchor.MiddleRight);
            AnchorTopRight(_pointText.rectTransform);
            _pointText.color = Cyan;

            var close = TextButton(bar.transform, new Vector2(1, 1),
                new Vector2(-16, -12), new Vector2(96, 52), "닫기", 16, Hide);
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
        }

        // ------------------------------------------------- orientation sync --
        /// <summary>Lays the tab bar out for a viewport. Pure function of the
        /// two arguments — no Screen reads — so EditMode can drive it. Portrait
        /// moves the currency readouts to a second row so they cannot overlap
        /// the map and controls tabs.</summary>
        internal void ApplyLayout(int width, int height)
        {
            if (_scaler == null || _tabBar == null) return;
            _lastScreenWidth = width;
            _lastScreenHeight = height;
            var portrait = width < height;
            LastPortrait = portrait;

            // Portrait relaxes toward width-match, same constant and same
            // reason as HudView L395: full width-match (0) is banned because
            // touch targets would collapse to ~17 CSS px.
            _scaler.matchWidthOrHeight = portrait ? 0.35f : 0.5f;

            var reference = _scaler.referenceResolution;
            var scale = Mathf.Pow(width / reference.x, 1f - _scaler.matchWidthOrHeight)
                      * Mathf.Pow(height / reference.y, _scaler.matchWidthOrHeight);
            LastEffectiveWidth = width / Mathf.Max(0.0001f, scale);

            // Row 2 exists only in portrait; the bar grows to hold it.
            var barRect = _tabBar.GetComponent<RectTransform>();
            barRect.offsetMin = new Vector2(0f, portrait ? -TabBarHeightPortrait
                                                         : -TabBarHeight);
            barRect.offsetMax = Vector2.zero;

            // The readouts are top-right anchored, so y is the only thing that
            // has to move: -14 keeps them on the tab row, -14 - TabBarHeight
            // parks them on the row below it.
            var readoutY = portrait ? -14f - TabBarHeight : -14f;
            if (_relicText != null)
                _relicText.rectTransform.anchoredPosition = new Vector2(-320f, readoutY);
            if (_pointText != null)
                _pointText.rectTransform.anchoredPosition = new Vector2(-170f, readoutY);

            // Content must follow the bar or the taller portrait bar covers the
            // top of every tab's first row.
            var contentTop = portrait ? -TabBarHeightPortrait : -TabBarHeight;
            for (var i = 0; i < _contentFrames.Count; i++)
            {
                var frame = _contentFrames[i];
                if (frame == null) continue;   // destroyed by a rebuild
                frame.offsetMax = new Vector2(0f, contentTop);
            }
        }

        void BuildHintBar(Transform root)
        {
            var bar = Panelled(root, new Vector2(0, 0), new Vector2(1, 0),
                Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.05f));
            var rect = bar.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0, 40);
            _hintText = Label(bar.transform, 24, -10, 1000, 22, "", 13, TextAnchor.MiddleLeft);
            _hintText.color = InkDim;
        }

        /// <summary>Content frame every tab lives inside: below the tab bar,
        /// above the hint band.</summary>
        GameObject TabContent(Transform parent)
        {
            var content = new GameObject("TabContent");
            content.transform.SetParent(parent, false);
            var rect = content.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, 40f);
            rect.offsetMax = new Vector2(0f, -TabBarHeight);
            // ApplyLayout pushes these down when the portrait bar grows a second
            // row. Collected here rather than re-found by name so a renamed or
            // reparented frame fails at compile time, not silently at runtime.
            _contentFrames.Add(rect);
            return content;
        }





        // ------------------------------------------------------------------ map --
        GameObject BuildMapTab(Transform parent)
        {
            var content = TabContent(parent);
            _map = new CampaignMapView();
            _map.Build(content.transform, _font, new Vector2(24, -24), new Vector2(920, 340),
                showEpithets: true, labelSize: 12);
            FullBorder(_map.Field);

            var note = Label(content.transform, 24, -380, 920, 40,
                "정화한 구역은 완전히 밝고, 강하 가능한 구역은 반쯤 밝다. 잠긴 구역은 위치만 보인다.",
                12, TextAnchor.UpperLeft);
            note.color = InkDim;
            return content;
        }

        // ------------------------------------------------------------- controls --
        GameObject BuildControlsTab(Transform parent)
        {
            var content = TabContent(parent);
            var title = Label(content.transform, 24, -24, 600, 28, "조작", 22, TextAnchor.MiddleLeft);
            title.color = Gold;
            for (var i = 0; i < ControlRows.Length; i++)
            {
                var row = Panelled(content.transform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(24, -62 - i * 46), new Vector2(700, 40), Panel);
                var text = Label(row.transform, 16, -10, 660, 22, ControlRows[i], 14,
                    TextAnchor.MiddleLeft);
                text.color = Ink;
            }
            var note = Label(content.transform, 24, -62 - ControlRows.Length * 46 - 8, 700, 40,
                "터치 기기에서는 화면 버튼이 같은 동작을 대신한다.", 12, TextAnchor.UpperLeft);
            note.color = InkDim;
            return content;
        }


        static void PlateStateful(Image image, bool active)
        {
            var sprite = Resources.Load<Sprite>(
                active ? "Icons/ui-button-active" : "Icons/ui-button");
            if (sprite == null)
            {
                image.color = active ? new Color(Ember.r, Ember.g, Ember.b, 0.22f) : ButtonBack;
                return;
            }
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        GameObject Panelled(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                            Vector2 anchored, Vector2 size, Color color)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
            return panel;
        }

        Text Label(Transform parent, float x, float y, float width, float height,
                   string content, int size, TextAnchor anchor)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            var text = labelObject.AddComponent<Text>();
            ViewTypography.Configure(text, _font, size, anchor);
            text.text = content;
            text.color = Ink;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }

        GameObject TextButton(Transform parent, Vector2 anchor, Vector2 anchored,
                              Vector2 size, string label, int fontSize,
                              UnityEngine.Events.UnityAction onClick, bool plated = true)
        {
            var buttonObject = Panelled(parent, anchor, anchor, anchored, size, ButtonBack);
            buttonObject.GetComponent<Image>().raycastTarget = true;
            var plate = plated ? Resources.Load<Sprite>("Icons/ui-button") : null;
            if (plate != null)
            {
                var image = buttonObject.GetComponent<Image>();
                image.sprite = plate;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = Label(buttonObject.transform, 0, 0, size.x, size.y, label, fontSize,
                TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return buttonObject;
        }

        void FullBorder(Transform parent)
        {
            EdgeLine(parent, new Vector2(0, 0), new Vector2(1, 0), false);
            EdgeLine(parent, new Vector2(0, 1), new Vector2(1, 1), false);
            EdgeLine(parent, new Vector2(0, 0), new Vector2(0, 1), true);
            EdgeLine(parent, new Vector2(1, 0), new Vector2(1, 1), true);
        }

        void BottomLine(Transform parent) => EdgeLine(parent, new Vector2(0, 0), new Vector2(1, 0), false);

        void EdgeLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool vertical)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent, false);
            var image = line.AddComponent<Image>();
            image.color = Border;
            image.raycastTarget = false;
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = vertical ? new Vector2(anchorMin.x, 0.5f) : new Vector2(0.5f, anchorMin.y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = vertical ? new Vector2(1, 0) : new Vector2(0, 1);
        }

        static void AnchorTopRight(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
        }
    }
}
