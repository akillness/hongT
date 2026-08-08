// Tab meta screen (W7). A full-screen lobby surface: top tab bar, a left
// category rail, one large detail card in the middle, three stat columns under
// it, and a key-hint band at the bottom.
//
// The layout PRINCIPLE is borrowed from the reference action-RPG meta screen
// (_workspace/current/intake/reference-ui-ocr.txt s3): tabs on top, the chosen
// item stated large with its grade directly beneath, and its numbers split into
// three readable columns instead of one long list. Nothing is copied — the
// vocabulary, palette and every number here are this game's own.
//
// Scope is the LOBBY only (seed decision D6). The combat HUD is untouched: this
// canvas is built on the lobby object, sorts above it, and can only be opened
// while the lobby is on screen.
//
// Every number shown is read from the sim's own derived-stat properties through
// a stack HackConfig probe — the same seam LobbyView uses — so the meta screen
// can never drift into a second, prettier version of the balance contract.
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

        internal const int TabEquip = 0, TabSigil = 1, TabMap = 2, TabControls = 3;
        internal const int TabCount = 4;
        static readonly string[] TabNames = { "장비", "각인", "지도", "조작" };
        static readonly string[] TabKickers = { "EQUIPMENT", "SIGILS", "MAP", "CONTROLS" };

        /// <summary>Rarity ladder for equipment tiers T0..T5. Deliberately a
        /// SHARED ladder rather than per-slot: the reference screen's one useful
        /// idea is that a grade word is comparable across item kinds, which the
        /// existing per-slot narrative names (LobbyView.EquipTierNames) are not.
        /// Those names still supply the item's own title.</summary>
        internal static readonly string[] GradeNames =
            { "평범", "단련", "정예", "희귀", "영웅", "전설" };
        static readonly Color[] GradeColors =
        {
            new Color(0.62f, 0.66f, 0.8f),      // 평범 — dim ink
            new Color(0.72f, 0.78f, 0.86f),     // 단련
            new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f),  // 정예 — cyan
            new Color(0.56f, 0.60f, 1f),        // 희귀
            new Color(0xDD / 255f, 0xC8 / 255f, 0x69 / 255f),  // 영웅 — gold
            new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f),  // 전설 — ember
        };

        static readonly string[] EquipNames = { "무기", "랜턴", "망토" };
        static readonly string[] EquipRailKickers = { "BLADE", "LANTERN", "CLOAK" };

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
        CampaignData _data;
        CampaignMapView _map;
        System.Action _onClosed;

        GameObject[] _tabContents;
        Image[] _tabPlates;
        Text[] _tabLabels;
        int _tab = TabEquip;

        // Equipment tab.
        Image[] _equipRailPlates;
        Text[] _equipRailLabels;
        Text[] _equipRailTiers;
        int _equipRow;
        Text _detailTitle, _detailGrade, _detailKicker, _detailNote;
        Text[] _statColumnTitles;
        Text[] _statColumnBodies;

        // Sigil tab.
        Image[] _sigilRailPlates;
        Text[] _sigilRailLabels;
        int _sigilRow;
        Text _sigilTitle, _sigilGrade, _sigilBody;

        Text _relicText, _pointText, _hintText;

        public bool IsOpen => _root != null && _root.activeSelf;
        internal int ActiveTab => _tab;
        internal int SelectedEquipRow => _equipRow;
        internal CampaignMapView Map => _map;

        /// <summary>Builds the whole screen once, hidden. Called from the lobby
        /// so the meta screen shares its font and its data snapshot.</summary>
        public void Build(Font font, in CampaignData data, System.Action onClosed = null)
        {
            _font = font;
            _data = data;
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
            _tabContents[TabEquip] = BuildEquipTab(root);
            _tabContents[TabSigil] = BuildSigilTab(root);
            _tabContents[TabMap] = BuildMapTab(root);
            _tabContents[TabControls] = BuildControlsTab(root);

            SelectTab(TabEquip);
            Refresh(data);
            _root.SetActive(false);
        }

        public void Show(in CampaignData data) => Show(in data, _tab);

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
            _data = data;
            if (_root == null) return;

            _relicText.text = $"유물 {data.Relics}";
            _pointText.text = $"포인트 {data.Points}";

            for (var i = 0; i < EquipNames.Length; i++)
            {
                var tier = TierOf(in data, i);
                _equipRailTiers[i].text = $"T{tier}";
                _equipRailTiers[i].color = GradeColors[Mathf.Clamp(tier, 0, GradeColors.Length - 1)];
                var selected = i == _equipRow;
                _equipRailLabels[i].color = selected ? Gold : InkDim;
                PlateStateful(_equipRailPlates[i], selected);
            }
            RefreshEquipDetail();

            for (var i = 0; i < LobbyView.SigilOrder.Length; i++)
            {
                var owned = (data.SigilsOwned & (1 << (int)LobbyView.SigilOrder[i])) != 0;
                var selected = i == _sigilRow;
                _sigilRailLabels[i].color = selected ? Gold : owned ? Cyan : InkDim;
                PlateStateful(_sigilRailPlates[i], selected);
            }
            RefreshSigilDetail();

            _map?.Refresh(in data);
        }

        void Update()
        {
            if (!IsOpen) return;
            _map?.Tick(Time.unscaledTime);

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            // Keyboard parity with the pointer path: every control on this screen
            // is reachable without a mouse, and ESC always gets the player out.
            if (keyboard.escapeKey.wasPressedThisFrame) { Hide(); return; }
            if (keyboard.tabKey.wasPressedThisFrame) SelectTab((_tab + 1) % TabCount);
            if (keyboard.downArrowKey.wasPressedThisFrame) MoveRow(1);
            if (keyboard.upArrowKey.wasPressedThisFrame) MoveRow(-1);
        }

        void MoveRow(int delta)
        {
            if (_tab == TabEquip)
                SelectEquipRow(Wrap(_equipRow + delta, EquipNames.Length));
            else if (_tab == TabSigil)
                SelectSigilRow(Wrap(_sigilRow + delta, LobbyView.SigilOrder.Length));
        }

        static int Wrap(int value, int count) => (value % count + count) % count;

        void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, TabCount - 1);
            for (var i = 0; i < TabCount; i++)
            {
                _tabContents[i].SetActive(i == _tab);
                PlateStateful(_tabPlates[i], i == _tab);
                _tabLabels[i].color = i == _tab ? Gold : InkDim;
            }
            _hintText.text = HintFor(_tab);
        }

        static string HintFor(int tab)
            => tab == TabEquip || tab == TabSigil
                ? "탭 전환 Tab • 선택 위•아래 • 닫기 ESC • 구매는 성소 정비에서"
                : "탭 전환 Tab • 닫기 ESC";

        void SelectEquipRow(int row)
        {
            _equipRow = Mathf.Clamp(row, 0, EquipNames.Length - 1);
            Refresh(_data);
        }

        void SelectSigilRow(int row)
        {
            _sigilRow = Mathf.Clamp(row, 0, LobbyView.SigilOrder.Length - 1);
            Refresh(_data);
        }

        // ------------------------------------------------------------ tab bar --
        void BuildTabBar(Transform root)
        {
            var bar = Panelled(root, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.05f));
            var rect = bar.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0, -74);
            rect.offsetMax = Vector2.zero;
            BottomLine(bar.transform);

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
            rect.offsetMax = new Vector2(0f, -74f);
            return content;
        }

        // ----------------------------------------------------------- equipment --
        GameObject BuildEquipTab(Transform parent)
        {
            var content = TabContent(parent);

            // Left rail: the item categories, the reference's one genuinely
            // useful structural idea (a stable spine so the detail card is the
            // only thing that changes when you move).
            _equipRailPlates = new Image[EquipNames.Length];
            _equipRailLabels = new Text[EquipNames.Length];
            _equipRailTiers = new Text[EquipNames.Length];
            for (var i = 0; i < EquipNames.Length; i++)
            {
                var index = i;
                var button = TextButton(content.transform, new Vector2(0, 1),
                    new Vector2(24, -24 - i * 76), new Vector2(196, 64), "", 18,
                    () => SelectEquipRow(index), plated: false);
                _equipRailPlates[i] = button.GetComponent<Image>();
                _equipRailLabels[i] = button.GetComponentInChildren<Text>();
                _equipRailLabels[i].text = EquipNames[i];
                _equipRailLabels[i].alignment = TextAnchor.MiddleLeft;
                _equipRailLabels[i].rectTransform.offsetMin = new Vector2(16f, 12f);

                var kicker = Label(button.transform, 16, -42, 120, 14, EquipRailKickers[i], 9,
                    TextAnchor.MiddleLeft);
                kicker.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
                _equipRailTiers[i] = Label(button.transform, -16, -20, 60, 24, "T0", 18,
                    TextAnchor.MiddleRight);
                AnchorTopRight(_equipRailTiers[i].rectTransform);
            }

            var detail = Panelled(content.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(244, -24), new Vector2(700, 250), Panel);
            FullBorder(detail.transform);
            _detailKicker = Label(detail.transform, 24, -18, 400, 18, "", 10, TextAnchor.MiddleLeft);
            _detailKicker.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
            _detailTitle = Label(detail.transform, 24, -36, 640, 44, "", 30, TextAnchor.MiddleLeft);
            _detailTitle.color = Ink;
            // Grade sits DIRECTLY under the name, the reference screen's clearest
            // relationship: the word that tells you how good it is never floats
            // away from the thing it grades.
            _detailGrade = Label(detail.transform, 24, -82, 400, 26, "", 17, TextAnchor.MiddleLeft);
            _detailNote = Label(detail.transform, 24, -112, 640, 22, "", 13, TextAnchor.MiddleLeft);
            _detailNote.color = InkDim;

            // Three stat columns, evenly divided across the detail card.
            _statColumnTitles = new Text[3];
            _statColumnBodies = new Text[3];
            string[] columnTitles = { "피해", "생존", "기름" };
            for (var c = 0; c < 3; c++)
            {
                var x = 24 + c * 226;
                _statColumnTitles[c] = Label(detail.transform, x, -146, 210, 20,
                    columnTitles[c], 14, TextAnchor.MiddleLeft);
                _statColumnTitles[c].color = Gold;
                _statColumnBodies[c] = Label(detail.transform, x, -168, 210, 72, "", 12,
                    TextAnchor.UpperLeft);
                _statColumnBodies[c].color = Ink;
            }

            var footnote = Label(content.transform, 244, -286, 700, 40,
                "수치는 시뮬레이션이 실제로 쓰는 값이다. 구매•강화는 로비 성소 정비 탭에서 한다.",
                12, TextAnchor.UpperLeft);
            footnote.color = InkDim;
            return content;
        }

        void RefreshEquipDetail()
        {
            var slot = _equipRow;
            var tier = TierOf(in _data, slot);
            var probe = Probe(in _data);

            _detailKicker.text = EquipRailKickers[slot];
            _detailTitle.text = LobbyView.EquipTierNames[slot][tier];
            _detailGrade.text = $"{GradeNames[tier]} • T{tier}/T5";
            _detailGrade.color = GradeColors[Mathf.Clamp(tier, 0, GradeColors.Length - 1)];
            // Cap and price both from ProgressionGuide — the one place that
            // owns them. GameDirector.EquipCosts is an alias to this and used to
            // be the read path; going through it made the meta screen's price
            // depend on the director's, when neither owns it.
            _detailNote.text = tier >= ProgressionGuide.EquipCap
                ? "최고 등급 — 이 슬롯은 더 오를 곳이 없다."
                : $"다음 등급까지 유물 {ProgressionGuide.EquipCosts[tier]}";

            _statColumnBodies[0].text =
                $"공격력 {probe.PlayerDamage:F1}\n" +
                $"무기 등급 보정 +{CampaignSpec.WeaponDamagePerRank * _data.Weapon * 100f:F0}%\n" +
                $"할당 공격 {_data.Attack}/10";
            _statColumnBodies[1].text =
                $"최대 체력 {probe.PlayerMaxHealth:F0}\n" +
                $"망토 등급 보정 +{CampaignSpec.CloakHealthPerRank * _data.Cloak:F0}\n" +
                $"이동 {probe.PlayerSpeed:F0}";
            _statColumnBodies[2].text =
                $"재생 {probe.LanternRegenPerSecond:F2}/초\n" +
                $"랜턴 등급 보정 +{CampaignSpec.LanternRegenPerRank * _data.Lantern * 100f:F0}%\n" +
                $"할당 이속 {_data.Swiftness}/10";
        }

        // --------------------------------------------------------------- sigils --
        GameObject BuildSigilTab(Transform parent)
        {
            var content = TabContent(parent);
            _sigilRailPlates = new Image[LobbyView.SigilOrder.Length];
            _sigilRailLabels = new Text[LobbyView.SigilOrder.Length];
            for (var i = 0; i < LobbyView.SigilOrder.Length; i++)
            {
                var index = i;
                var button = TextButton(content.transform, new Vector2(0, 1),
                    new Vector2(24, -24 - i * 60), new Vector2(196, 52), "", 16,
                    () => SelectSigilRow(index), plated: false);
                _sigilRailPlates[i] = button.GetComponent<Image>();
                _sigilRailLabels[i] = button.GetComponentInChildren<Text>();
                _sigilRailLabels[i].text = LobbyView.SigilNames[i];
                _sigilRailLabels[i].alignment = TextAnchor.MiddleLeft;
                _sigilRailLabels[i].rectTransform.offsetMin = new Vector2(16f, 0f);
            }

            var detail = Panelled(content.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(244, -24), new Vector2(700, 250), Panel);
            FullBorder(detail.transform);
            _sigilTitle = Label(detail.transform, 24, -24, 640, 44, "", 28, TextAnchor.MiddleLeft);
            _sigilGrade = Label(detail.transform, 24, -70, 400, 26, "", 16, TextAnchor.MiddleLeft);
            _sigilBody = Label(detail.transform, 24, -104, 640, 120, "", 13, TextAnchor.UpperLeft);
            _sigilBody.color = Ink;
            return content;
        }

        void RefreshSigilDetail()
        {
            var row = _sigilRow;
            var kind = (int)LobbyView.SigilOrder[row];
            var owned = (_data.SigilsOwned & (1 << kind)) != 0;
            var slotted = _data.SigilSlot0 == kind || _data.SigilSlot1 == kind;
            var face = (_data.SigilFaces & (1 << kind)) != 0 ? 1 : 0;

            _sigilTitle.text = LobbyView.SigilNames[row];
            _sigilTitle.color = owned ? Ink : InkDim;
            _sigilGrade.text = slotted ? "장착 중" : owned ? "보유" : $"미보유 • 유물 {LobbyView.SigilCost}";
            _sigilGrade.color = slotted ? Ember : owned ? Cyan : InkDim;
            _sigilBody.text =
                $"기믹 • {LobbyView.SigilGimmicks[row]}\n" +
                $"{LobbyView.SigilFaceNames[row][0]} • {LobbyView.SigilFaceEffects[row][0]}\n" +
                $"{LobbyView.SigilFaceNames[row][1]} • {LobbyView.SigilFaceEffects[row][1]}\n" +
                (owned
                    ? $"현재 면 • {LobbyView.SigilFaceNames[row][face]}"
                    : "성소 정비 각인 탭에서 해금한다.");
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

        // ------------------------------------------------------------- helpers --
        static int TierOf(in CampaignData data, int slot)
            => slot == 0 ? data.Weapon : slot == 1 ? data.Lantern : data.Cloak;

        /// <summary>Inert probe carrying this save's meta. Identical seam to
        /// LobbyView.Probe: the derived-stat properties ARE the mirror, so this
        /// screen composes no formula of its own.</summary>
        static HackConfig Probe(in CampaignData data)
            => new HackConfig
            {
                MetaStats = MetaStats.Of(data.Attack, data.Vitality, data.Swiftness),
                EquipTiers = EquipTiers.Of(data.Weapon, data.Lantern, data.Cloak),
            };

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
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
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
