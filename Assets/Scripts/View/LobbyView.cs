// Lobby UI (spec §9). Pure code-generated uGUI over the live 3D backdrop —
// no asset dependencies, factory style cloned from HudView. LobbyView owns
// only the 2D panels; 3D staging lives elsewhere. All dynamic values update
// through Refresh(CampaignData) (text/state only — no re-instantiation).
//
// Style tokens (original web lobby): panel rgba(5,4,9,0.72), border line
// rgba(128,216,255,0.34), accents cyan #2CADD6 / ember #F3592C / gold
// #DDC869, eyebrow pattern (EN kicker above, KR title below).
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CinderCourt.View
{
    /// <summary>User intents raised by the lobby. Wired by GameDirector.</summary>
    public struct LobbyCallbacks
    {
        /// <summary>"prologue" or a logical campaign stage id from <see cref="StageCatalog"/>.</summary>
        public System.Action<string> OnSortie;
        /// <summary>"attack" | "vitality" | "swiftness".</summary>
        public System.Action<string> OnAllocateStat;
        /// <summary>"weapon" | "lantern" | "cloak".</summary>
        public System.Action<string> OnBuyEquip;
        /// <summary>Companion id, or "" for none.</summary>
        public System.Action<string> OnSelectCompanion;
    }

    public sealed class LobbyView : MonoBehaviour
    {
        // --- style tokens (original web lobby) --------------------------------
        static readonly Color PanelColor = new Color(5f / 255f, 4f / 255f, 9f / 255f, 0.72f);
        static readonly Color BorderColor = new Color(128f / 255f, 216f / 255f, 255f / 255f, 0.34f);
        static readonly Color Cyan = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f);
        static readonly Color Ember = new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f);
        static readonly Color Gold = new Color(0xDD / 255f, 0xC8 / 255f, 0x69 / 255f);
        static readonly Color InkDim = new Color(0.62f, 0.66f, 0.8f);
        static readonly Color Lock = new Color(0.42f, 0.45f, 0.58f);
        static readonly Color ButtonBack = new Color(0.16f, 0.13f, 0.24f, 0.9f);
        static readonly Color ButtonActive = new Color(0.32f, 0.28f, 0.16f, 0.95f);


        const int StatCap = 10;
        const int EquipCap = 5;
        static readonly string[] StatIds = { "attack", "vitality", "swiftness" };
        static readonly string[] StatNames = { "공격", "체력", "이속" };
        static readonly string[] EquipIds = { "weapon", "lantern", "cloak" };
        static readonly string[] EquipNames = { "무기", "랜턴", "망토" };
        static readonly int[] EquipCosts = { 2, 4, 7, 11, 16 };  // relics for T(i)->T(i+1)

        // v1.3 M2: tier narrative names (court vocabulary — worldview.md
        // '메타 서사 (v1.3)' is the single source). [slot][tier], slot order
        // = EquipIds, T0..T5. Internal so the catalog test can glyph-audit.
        internal static readonly string[][] EquipTierNames =
        {
            new[] { "잿날", "담금날", "벼림날", "선고날", "심판날", "판결인" },
            new[] { "잿등", "밀랍등", "서약등", "기록등", "증언등", "진실등" },
            new[] { "잿천", "무명포", "증인포", "기록포", "선고포", "집행포" },
        };

        // Roster slots are pre-built for every obtainable companion (boss
        // unlocks + elite extraction echoes) so Refresh never re-instantiates.
        static readonly string[] CompanionIds =
            { "ember-cohort", "shade-echo", "possessed-echo", "scout-echo", "ember-cohort-echo" };
        static readonly string[] CompanionNames =
            { "잿불 사도", "그림자 메아리", "홀린 자 메아리", "정찰꾼 메아리", "잿불 메아리" };
        // v1.3 M4: one-line identity epithets (court grammar, worldview.md
        // '메타 서사'): origin function of each echo, indexes CompanionIds.
        static readonly string[] CompanionEpithets =
            { "첫 서약의 증인", "성당의 메아리", "왕좌의 메아리", "행진의 메아리", "정예의 잿불" };

        LobbyCallbacks _callbacks;
        Font _font;
        GameObject _root;

        // --- mobile layout (mobile-layout spec #1/#8) --------------------------
        CanvasScaler _scaler;
        RectTransform _sortieRect, _sanctumRect;
        int _lastScreenWidth = -1, _lastScreenHeight = -1;
        bool _stacked;

        // Top bar.
        Text _relicText, _pointText;

        // Sortie cards.
        Text _prologueStatus;  Text _prologueButtonLabel;
        readonly Text[] _stageStatus = new Text[StageCatalog.Entries.Count];
        readonly Button[] _stageButtons = new Button[StageCatalog.Entries.Count];
        readonly CanvasGroup[] _stageGroups = new CanvasGroup[StageCatalog.Entries.Count];
        readonly Text[] _stageSubLabels = new Text[StageCatalog.Entries.Count];
        // v1.3 M3b: 서약 toggles — one per stage card, revealed when cleared.
        readonly Text[] _pactLabels = new Text[StageCatalog.Entries.Count];
        readonly Image[] _pactBackgrounds = new Image[StageCatalog.Entries.Count];
        readonly GameObject[] _pactButtons = new GameObject[StageCatalog.Entries.Count];
        // Session-only opt-in state (meta-fun-pass-spec §세이브 스키마: NEVER
        // saved — re-armed per session, Hades-heat opt-in grammar).
        readonly System.Collections.Generic.Dictionary<string, bool> _pactArmed =
            new System.Collections.Generic.Dictionary<string, bool>();
        // cycle2 B3: prologue card border lines pulse ember until PrologueDone.
        Image[] _prologueBorder;
        bool _prologueGuide;

        // Tabs.
        GameObject[] _tabContents;
        Image[] _tabBackgrounds;

        // Growth rows.
        readonly Text[] _statValues = new Text[3];
        readonly Button[] _statButtons = new Button[3];
        readonly CanvasGroup[] _statGroups = new CanvasGroup[3];
        Text _pointsLeftText;
        // v1.3 M1: per-stat derived-value lines + honest 3-value summary.
        readonly Text[] _statDerived = new Text[3];
        Text _growthSummary;
        // cycle2 B4: 모션 약함 toggle label (ViewPrefs-backed).
        Text _motionLabel;

        // Equipment rows.
        readonly Text[] _equipValues = new Text[3];
        readonly Text[] _equipButtonLabels = new Text[3];
        readonly Button[] _equipButtons = new Button[3];
        readonly CanvasGroup[] _equipGroups = new CanvasGroup[3];
        // v1.3 M2: per-slot rank/effect lines ("T3 • 공격 +18%").
        readonly Text[] _equipDerived = new Text[3];

        // Legion grid (index 0 = "none", 1.. = CompanionIds).
        readonly Text[] _rosterLabels = new Text[CompanionIds.Length + 1];
        readonly Image[] _rosterBackgrounds = new Image[CompanionIds.Length + 1];
        readonly Button[] _rosterButtons = new Button[CompanionIds.Length + 1];

        public void Build(CampaignData data, LobbyCallbacks callbacks)
        {
            _callbacks = callbacks;
            _font = Resources.Load<Font>("Fonts/HudKorean");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Lobby");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;   // orientation-driven, see ApplyLobbyTier
            _scaler = scaler;
            canvasObject.AddComponent<GraphicRaycaster>();
            _root = canvasObject;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }

            var root = canvasObject.transform;
            BuildTopBar(root);
            BuildSortiePanel(root);
            BuildSanctumPanel(root);
            SelectTab(0);
            ApplyLobbyTier(true);
            Refresh(data);
        }

        /// <summary>Re-render balances/card states. Text + interactable only —
        /// never re-instantiates.</summary>
        public void Refresh(CampaignData data)
        {
            _relicText.text = $"유물 {data.Relics}";
            _pointText.text = $"포인트 {data.Points}";

            // --- sortie: prologue gates everything, stages unlock in order ----
            _prologueStatus.text = data.PrologueDone ? "재훈련 가능" : "필수 훈련";
            _prologueStatus.color = data.PrologueDone ? Gold : Ember;
            _prologueButtonLabel.text = data.PrologueDone ? "재훈련" : "점화 훈련";
            // cycle2 B3: first-run guide — ember border pulse until done.
            SetPrologueGuide(!data.PrologueDone);

            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                var cleared = StageCatalog.IsCleared(in data, in entry);
                var unlocked = StageCatalog.IsUnlocked(in data, in entry);
                _stageStatus[i].text = cleared ? "정화 완료" : unlocked ? "강하 가능" : "잠김";
                _stageStatus[i].color = cleared ? Gold : unlocked ? Cyan : Lock;
                _stageButtons[i].interactable = unlocked;
                _stageGroups[i].alpha = unlocked ? 1f : 0.45f;

                // v1.3 M3b: 서약 toggle appears once the stage is cleared. The
                // cleared card drops its redeemed '보상:' tail at the same
                // moment, so the toggle never covers live reward text.
                _pactButtons[i].SetActive(cleared);
                if (cleared)
                {
                    _stageSubLabels[i].text = entry.Epithet;
                    SyncPactVisual(i, IsPactArmed(entry.Id));
                }
            }

            _pointsLeftText.text = $"남은 포인트 {data.Points}";
            // v1.3 M1: real effective values through the sim's OWN derived-stat
            // properties (§5/§6 closed forms live in HackConfig; the view
            // composes no formula, so mirror drift is structurally impossible).
            // Probe configs are stack structs — no allocation.
            var probe = Probe(in data, 0, 0, 0);
            var attackDelta = Probe(in data, 1, 0, 0).PlayerDamage - probe.PlayerDamage;
            var vitalityDelta = Probe(in data, 0, 1, 0).PlayerMaxHealth - probe.PlayerMaxHealth;
            var swiftnessDelta = Probe(in data, 0, 0, 1).PlayerSpeed - probe.PlayerSpeed;
            _statDerived[0].text = data.Attack >= StatCap
                ? $"공격력 {probe.PlayerDamage:F1} • 숙련"
                : $"공격력 {probe.PlayerDamage:F1} (+{attackDelta:F1})";
            _statDerived[1].text = data.Vitality >= StatCap
                ? $"최대 체력 {probe.PlayerMaxHealth:F0} • 숙련"
                : $"최대 체력 {probe.PlayerMaxHealth:F0} (+{vitalityDelta:F0})";
            _statDerived[2].text = data.Swiftness >= StatCap
                ? $"이동 {probe.PlayerSpeed:F0} • 숙련"
                : $"이동 {probe.PlayerSpeed:F0} (+{swiftnessDelta:F1})";
            // Honest 3-value summary — no invented aggregate score (spec M1).
            _growthSummary.text =
                $"공격력 {probe.PlayerDamage:F1} • 최대 체력 {probe.PlayerMaxHealth:F0} • 이동 {probe.PlayerSpeed:F0}";
            for (var i = 0; i < 3; i++)
            {
                var value = i == 0 ? data.Attack : i == 1 ? data.Vitality : data.Swiftness;
                _statValues[i].text = $"{value}/{StatCap}";
                var can = data.Points > 0 && value < StatCap;
                _statButtons[i].interactable = can;
                _statGroups[i].alpha = can ? 1f : 0.45f;
            }
            RefreshMotionLabel();

            // v1.3 M2: tier narrative + real rank effect + buy delta, all from
            // the same probe properties (weapon→PlayerDamage, lantern→
            // LanternRegenPerSecond, cloak→PlayerMaxHealth). The rank line's
            // percentage is a single frozen constant scaled by rank — the only
            // spot the view touches a sim constant directly, never composed.
            for (var i = 0; i < 3; i++)
            {
                var tier = i == 0 ? data.Weapon : i == 1 ? data.Lantern : data.Cloak;
                _equipValues[i].text = $"T{tier}/T{EquipCap}";
                _equipDerived[i].text = EquipRankLine(i, tier);
                var maxed = tier >= EquipCap;
                var cost = maxed ? 0 : EquipCosts[tier];
                _equipButtonLabels[i].text = maxed ? "만렙" : BuyLine(in data, i, cost);
                var can = !maxed && data.Relics >= cost;
                _equipButtons[i].interactable = can;
                _equipGroups[i].alpha = can ? 1f : 0.45f;
            }

            // --- legion ----------------------------------------------------------
            var noneActive = string.IsNullOrEmpty(data.Active);
            _rosterLabels[0].color = noneActive ? Gold : InkDim;
            _rosterBackgrounds[0].color = noneActive ? ButtonActive : ButtonBack;
            for (var i = 0; i < CompanionIds.Length; i++)
            {
                var owned = RosterContains(data.Roster, CompanionIds[i]);
                var active = owned && data.Active == CompanionIds[i];
                _rosterLabels[i + 1].text = owned ? CompanionNames[i] : $"{CompanionNames[i]} (미보유)";
                _rosterLabels[i + 1].color = active ? Gold : owned ? Cyan : Lock;
                _rosterBackgrounds[i + 1].color = active ? ButtonActive : ButtonBack;
                _rosterButtons[i + 1].interactable = owned;
            }
        }

        void RefreshMotionLabel()
        {
            if (_motionLabel == null) return;
            _motionLabel.text = ViewPrefs.ReducedMotion ? "모션: 약함" : "모션: 보통";
            _motionLabel.color = ViewPrefs.ReducedMotion ? Gold : Cyan;
        }
        // ------------------------------------------------- v1.3 meta helpers --

        /// <summary>
        /// Inert probe config carrying the lobby's meta at an offset: the four
        /// derived-stat properties (§5/§6) are pure functions of MetaStats/
        /// EquipTiers, so a stack-allocated HackConfig IS the mirror — the view
        /// never re-composes 58×(1+…) itself. Deltas = probe(x+1) − probe(x);
        /// the properties clamp internally, so a cap probe simply flattens
        /// (the UI shows 숙련 instead of a delta at the cap).
        /// </summary>
        static HackConfig Probe(in CampaignData data, int attackPlus, int vitalityPlus, int swiftnessPlus)
            => new HackConfig
            {
                MetaStats = MetaStats.Of(
                    data.Attack + attackPlus, data.Vitality + vitalityPlus, data.Swiftness + swiftnessPlus),
                EquipTiers = EquipTiers.Of(data.Weapon, data.Lantern, data.Cloak),
            };

        /// <summary>
        /// M2 rank line: "판결인 T5 • 공격 +30%". The percentage is the frozen
        /// per-rank constant scaled by rank — read, not composed (the composed
        /// truth lives in the probe properties above).
        /// </summary>
        static string EquipRankLine(int slot, int tier)
        {
            var name = EquipTierNames[slot][tier];
            switch (slot)
            {
                case 0: return $"{name} • 공격 +{CampaignSpec.WeaponDamagePerRank * tier * 100f:F0}%";
                case 1: return $"{name} • 재생 +{CampaignSpec.LanternRegenPerRank * tier * 100f:F0}%";
                default: return $"{name} • 체력 +{CampaignSpec.CloakHealthPerRank * tier:F0}";
            }
        }

        /// <summary>
        /// M2 buy line: "유물 7 → 공격 +4.5" — the REAL next-tier delta from the
        /// probe properties (equip contribution composes with allocated stats,
        /// so the number is what the player will actually gain).
        /// </summary>
        static string BuyLine(in CampaignData data, int slot, int cost)
        {
            var current = Probe(in data, 0, 0, 0);
            var next = current;
            switch (slot)
            {
                case 0: next.EquipTiers.Weapon++; break;
                case 1: next.EquipTiers.Lantern++; break;
                default: next.EquipTiers.Cloak++; break;
            }
            switch (slot)
            {
                case 0: return $"유물 {cost} → 공격 +{next.PlayerDamage - current.PlayerDamage:F1}";
                case 1: return $"유물 {cost} → 재생 +{next.LanternRegenPerSecond - current.LanternRegenPerSecond:F2}";
                default: return $"유물 {cost} → 체력 +{next.PlayerMaxHealth - current.PlayerMaxHealth:F0}";
            }
        }

        /// <summary>M3b: is the verdict pact armed for a stage this session?
        /// Read by GameDirector when routing a sortie. Session-only state —
        /// never persisted (meta-fun-pass-spec §세이브 스키마).</summary>
        public bool IsPactArmed(string stageId)
            => _pactArmed.TryGetValue(stageId, out var armed) && armed;

        void TogglePact(int index)
        {
            var id = StageCatalog.Entries[index].Id;
            var armed = !IsPactArmed(id);
            _pactArmed[id] = armed;
            SyncPactVisual(index, armed);
        }

        /// <summary>Armed = ember accent (risk grammar) on the stateful flat
        /// fill; off = the plain roster/tab idle look.</summary>
        void SyncPactVisual(int index, bool armed)
        {
            // "•" (U+2022) — NanumBarunGothic lacks U+2713; bullet is already in the subset.
            _pactLabels[index].text = armed ? "서약 •" : "서약";
            _pactLabels[index].color = armed ? Ember : InkDim;
            _pactBackgrounds[index].color = armed
                ? new Color(Ember.r, Ember.g, Ember.b, 0.22f)
                : ButtonBack;
        }


        /// <summary>cycle2 B3: toggle the first-run pulse; restores the
        /// border token color when the guide stops.</summary>
        void SetPrologueGuide(bool on)
        {
            if (_prologueGuide == on) return;
            _prologueGuide = on;
            if (on || _prologueBorder == null) return;
            for (var i = 0; i < _prologueBorder.Length; i++)
                _prologueBorder[i].color = BorderColor;
        }

        public void Show() { if (_root != null) _root.SetActive(true); }
        public void Hide() { if (_root != null) _root.SetActive(false); }

        // =============================================== mobile layout core --
        void Update()
        {
            // Resolution dirty-check only (two int compares, no alloc).
            if (_root == null || !_root.activeSelf) return;
            ApplyLobbyTier(false);

            // cycle2 B3: first-run guide — prologue border pulses ember.
            // PingPong 0→1 over 1.2s round trip, SmoothStep ease, alpha
            // 0.35→0.9. Color writes only — no layout, no allocation.
            if (_prologueGuide && _prologueBorder != null)
            {
                var phase = Mathf.SmoothStep(0f, 1f,
                    Mathf.PingPong(Time.unscaledTime * (2f / 1.2f), 1f));
                var pulse = new Color(Ember.r, Ember.g, Ember.b,
                    Mathf.Lerp(0.35f, 0.9f, phase));
                for (var i = 0; i < _prologueBorder.Length; i++)
                    _prologueBorder[i].color = pulse;
            }
        }

        /// <summary>Spec #8: SORTIE(392, right) + SANCTUM(400, left) need
        /// 840 u; below that effective width the panels stack into a single
        /// full-width column (SORTIE on top). Also flips the orientation
        /// match (spec #1: portrait 0.35 / landscape 0.5).</summary>
        void ApplyLobbyTier(bool force)
        {
            var width = Screen.width;
            var height = Screen.height;
            if (!force && width == _lastScreenWidth && height == _lastScreenHeight)
                return;
            _lastScreenWidth = width;
            _lastScreenHeight = height;

            var match = width < height ? 0.35f : 0.5f;
            _scaler.matchWidthOrHeight = match;

            // Same effective-width formula the scaler applies (its cached
            // scaleFactor is one frame stale on the resize frame).
            var logWidth = Mathf.Log(width / 1280f, 2f);
            var logHeight = Mathf.Log(height / 720f, 2f);
            var scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
            var effectiveWidth = width / Mathf.Max(0.0001f, scale);

            var stack = effectiveWidth < 850f;   // 840 u content + margin
            if (!force && stack == _stacked) return;
            _stacked = stack;

            if (stack)
            {
                // Single column: both panels stretch to full width. Cards and
                // rows inside anchor to their panel's top/edges, so only the
                // parents move (spec #8 risk note).
                _sortieRect.anchorMin = new Vector2(0f, 1f);
                _sortieRect.anchorMax = new Vector2(1f, 1f);
                _sortieRect.pivot = new Vector2(0.5f, 1f);
                _sortieRect.anchoredPosition = new Vector2(0, -72);
                _sortieRect.sizeDelta = new Vector2(-32, 620);

                _sanctumRect.anchorMin = new Vector2(0f, 1f);
                _sanctumRect.anchorMax = new Vector2(1f, 1f);
                _sanctumRect.pivot = new Vector2(0.5f, 1f);
                _sanctumRect.anchoredPosition = new Vector2(0, -708);
                _sanctumRect.sizeDelta = new Vector2(-32, 560);
            }
            else
            {
                _sortieRect.anchorMin = new Vector2(1f, 1f);
                _sortieRect.anchorMax = new Vector2(1f, 1f);
                _sortieRect.pivot = new Vector2(1f, 1f);
                _sortieRect.anchoredPosition = new Vector2(-16, -72);
                _sortieRect.sizeDelta = new Vector2(392, 620);

                _sanctumRect.anchorMin = new Vector2(0f, 1f);
                _sanctumRect.anchorMax = new Vector2(0f, 1f);
                _sanctumRect.pivot = new Vector2(0f, 1f);
                _sanctumRect.anchoredPosition = new Vector2(16, -72);
                _sanctumRect.sizeDelta = new Vector2(400, 560);
            }
        }

        static bool RosterContains(string[] roster, string id)
        {
            if (roster == null) return false;
            for (var i = 0; i < roster.Length; i++)
                if (roster[i] == id) return true;
            return false;
        }

        static string CompanionNameFor(string id)
        {
            for (var i = 0; i < CompanionIds.Length; i++)
                if (CompanionIds[i] == id) return CompanionNames[i];
            return id;
        }

        // ---------------------------------------------------------------- top --
        void BuildTopBar(Transform root)
        {
            var bar = Panel(root, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, PanelColor);
            var rect = bar.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0, -56);
            rect.offsetMax = new Vector2(0, 0);
            Border(bar.transform, false);

            var title = Label(bar.transform, 20, -6, 620, 26,
                "ABYSSAL LANTERN — 심연 강하", 20, TextAnchor.MiddleLeft);
            title.color = Gold;
            var kicker = Label(bar.transform, 20, -32, 620, 18,
                "SINGLE SCENE • LIVE BACKDROP", 11, TextAnchor.MiddleLeft);
            kicker.color = InkDim;

            _relicText = Label(bar.transform, -320, -6, 140, 44, "유물 0", 18, TextAnchor.MiddleRight);
            AnchorTopRight(_relicText.rectTransform);
            _relicText.color = Gold;
            _pointText = Label(bar.transform, -170, -6, 140, 44, "포인트 0", 18, TextAnchor.MiddleRight);
            AnchorTopRight(_pointText.rectTransform);
            _pointText.color = Cyan;

            var badge = Panel(bar.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-20, 0), new Vector2(78, 26), new Color(Ember.r, Ember.g, Ember.b, 0.22f));
            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.pivot = new Vector2(1f, 0.5f);
            var badgeText = Label(badge.transform, 0, 0, 78, 26, "v0.2.0", 13, TextAnchor.MiddleCenter);
            Stretch(badgeText.rectTransform);
            badgeText.color = Ember;
        }

        // ------------------------------------------------------------- sortie --
        void BuildSortiePanel(Transform root)
        {
            var panel = Panel(root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -72), new Vector2(392, 620), PanelColor);
            _sortieRect = panel.GetComponent<RectTransform>();
            _sortieRect.pivot = new Vector2(1f, 1f);
            Border(panel.transform, true);

            Eyebrow(panel.transform, 16, -12, "SORTIE", "출정");

            // Prologue card (sole entry until cleared; retrainable after).
            var prologue = Card(panel.transform, -60, 100);
            // cycle2 B3: capture the card's 4 border lines NOW (before other
            // children exist) so the first-run guide can pulse them ember.
            var prologueLines = prologue.GetComponentsInChildren<Image>();
            _prologueBorder = new Image[prologueLines.Length - 1];
            var borderCount = 0;
            for (var b = 0; b < prologueLines.Length; b++)
                if (prologueLines[b].gameObject != prologue)
                    _prologueBorder[borderCount++] = prologueLines[b];
            Eyebrow(prologue.transform, 12, -10, "PROLOGUE", "등불 점화 훈련");
            var prologueSub = Label(prologue.transform, 12, -62, 220, 18,
                "2D 디펜스 • 웨이브 3 • 스킬 없음", 12, TextAnchor.MiddleLeft);
            prologueSub.color = InkDim;
            _prologueStatus = Label(prologue.transform, -12, -10, 120, 20, "", 13, TextAnchor.MiddleRight);
            AnchorTopRight(_prologueStatus.rectTransform);
            var prologueButton = TextButton(prologue.transform, new Vector2(1, 0), new Vector2(-12, 10),
                new Vector2(112, 44), "점화 훈련", 15,
                () => _callbacks.OnSortie?.Invoke("prologue"));
            prologueButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            _prologueButtonLabel = prologueButton.GetComponentInChildren<Text>();

            // Cycle-2: nine logical stages no longer fit the fixed panel at
            // the 70 u card pitch (9*70+174 > 620), and compressing the pitch
            // would sink the 강하 button below the 44 CSS px touch floor
            // (HudLayoutTests contract). The list scrolls instead: pitch,
            // card height and every touch target keep their audited sizes.
            var stageViewport = new GameObject("StageScroll");
            stageViewport.transform.SetParent(panel.transform, false);
            var viewportImage = stageViewport.AddComponent<Image>();
            viewportImage.color = Color.clear;      // drag surface, invisible
            viewportImage.raycastTarget = true;      // ScrollRect needs the hits
            stageViewport.AddComponent<RectMask2D>();
            var viewportRect = stageViewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.offsetMin = new Vector2(0f, 12f);
            viewportRect.offsetMax = new Vector2(0f, -174f);   // below prologue card

            var stageContent = new GameObject("StageContent");
            stageContent.transform.SetParent(stageViewport.transform, false);
            var contentRect = stageContent.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            // 9 cards at the 70 u pitch + trailing margin.
            contentRect.sizeDelta = new Vector2(0f, StageCatalog.Entries.Count * 70f + 8f);

            var scroll = stageViewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // Nine logical stages share the same compact card grammar.
            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                var card = Card(stageContent.transform, -6 - i * 70, 68);
                Eyebrow(card.transform, 12, -6, entry.Kicker, entry.Title);
                var glyphSprite = Resources.Load<Sprite>("Icons/" + entry.HazardIcon);
                if (glyphSprite != null)
                {
                    var glyphObject = new GameObject("HazardGlyph");
                    glyphObject.transform.SetParent(card.transform, false);
                    var glyph = glyphObject.AddComponent<Image>();
                    glyph.sprite = glyphSprite;
                    glyph.preserveAspect = true;
                    glyph.raycastTarget = false;
                    var glyphRect = glyphObject.GetComponent<RectTransform>();
                    glyphRect.anchorMin = new Vector2(0, 1);
                    glyphRect.anchorMax = new Vector2(0, 1);
                    glyphRect.pivot = new Vector2(0, 1);
                    glyphRect.anchoredPosition = new Vector2(12f, -44f);
                    glyphRect.sizeDelta = new Vector2(24f, 24f);
                }
                var rewardText = string.IsNullOrEmpty(entry.CompanionReward)
                    ? "동행 없음"
                    : CompanionNameFor(entry.CompanionReward);
                // Fun-pass v1.2: gimmick epithet leads the reward line (spec
                // "기존 보상 라인 문법에 기믹 별칭 추가"). Merged instead of a new
                // row: the 68 u card has no vertical slack between the reward
                // band (-44..-60) and the 강하 button (bottom 6..34), and a new
                // row would break the audited pitch/44 px touch floor. The
                // hazard glyph at (12,-44) already sits directly left of this
                // label, so it doubles as the epithet's gimmick marker.
                var sub = Label(card.transform, 34, -44, 220, 16,
                    $"{entry.Epithet} • 보상: {rewardText}", 10, TextAnchor.MiddleLeft);
                sub.color = Gold;
                _stageSubLabels[i] = sub;
                _stageStatus[i] = Label(card.transform, -12, -8, 100, 18, "잠김", 11, TextAnchor.MiddleRight);
                AnchorTopRight(_stageStatus[i].rectTransform);

                var button = TextButton(card.transform, new Vector2(1, 0), new Vector2(-12, 6),
                    new Vector2(84, 28), "강하", 13,
                    () => _callbacks.OnSortie?.Invoke(entry.Id));
                button.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
                _stageButtons[i] = button.GetComponent<Button>();

                // v1.3 M3b: 서약 toggle — left of 강하, same audited 28 u height
                // (card grammar; 28 u ≥ the 강하 button's own touch-floor
                // audit at phone scale). Flat fill (plated:false): armed state
                // drives Image.color, the stateful-button grammar tabs/roster
                // use. Hidden until the stage is cleared (Refresh), so the
                // reward text it would cover is gone by the time it appears
                // (cleared cards drop the redeemed '보상:' tail).
                var pactIndex = i;
                var pact = TextButton(card.transform, new Vector2(1, 0), new Vector2(-104, 6),
                    new Vector2(84, 28), "서약", 12,
                    () => TogglePact(pactIndex), plated: false);
                pact.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
                _pactButtons[i] = pact;
                _pactBackgrounds[i] = pact.GetComponent<Image>();
                _pactLabels[i] = pact.GetComponentInChildren<Text>();
                pact.SetActive(false);   // revealed by Refresh on clear

                _stageGroups[i] = card.AddComponent<CanvasGroup>();
            }
        }

        // ------------------------------------------------------------ sanctum --
        void BuildSanctumPanel(Transform root)
        {
            var panel = Panel(root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -72), new Vector2(400, 560), PanelColor);
            _sanctumRect = panel.GetComponent<RectTransform>();
            Border(panel.transform, true);

            Eyebrow(panel.transform, 16, -12, "SANCTUM", "성소 정비");

            // Segmented tab buttons.
            string[] tabNames = { "성장", "장비", "군단" };
            _tabContents = new GameObject[3];
            _tabBackgrounds = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var tabIndex = i;
                var tab = TextButton(panel.transform, new Vector2(0, 1),
                    new Vector2(16 + i * 124, -60), new Vector2(120, 44), tabNames[i], 16,
                    () => SelectTab(tabIndex), plated: false);
                _tabBackgrounds[i] = tab.GetComponent<Image>();
            }

            _tabContents[0] = BuildGrowthTab(panel.transform);
            _tabContents[1] = BuildEquipTab(panel.transform);
            _tabContents[2] = BuildLegionTab(panel.transform);
        }

        GameObject TabContent(Transform parent)
        {
            var content = new GameObject("TabContent");
            content.transform.SetParent(parent, false);
            var rect = content.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, -116);
            return content;
        }

        GameObject BuildGrowthTab(Transform parent)
        {
            var content = TabContent(parent);
            _pointsLeftText = Label(content.transform, 16, -6, 360, 22, "남은 포인트 0", 15, TextAnchor.MiddleLeft);
            _pointsLeftText.color = Cyan;

            for (var i = 0; i < 3; i++)
            {
                var row = Panel(content.transform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(16, -36 - i * 72), new Vector2(368, 64), new Color(1f, 1f, 1f, 0.04f));
                RowIcon(row.transform, "stat-" + StatIds[i]);
                Label(row.transform, 52, -8, 120, 24, StatNames[i], 17, TextAnchor.MiddleLeft);
                // v1.3 M1: the static "+3%/pt" line becomes the live derived
                // line ("공격력 75.4 (+2.2)") — content owned by Refresh.
                _statDerived[i] = Label(row.transform, 52, -34, 200, 18, "", 11, TextAnchor.MiddleLeft);
                _statDerived[i].color = Gold;
                _statValues[i] = Label(row.transform, 170, 0, 90, 64, "0/10", 17, TextAnchor.MiddleCenter);
                _statValues[i].color = Cyan;

                var statId = StatIds[i];
                var plus = TextButton(row.transform, new Vector2(1, 0.5f), new Vector2(-10, 0),
                    new Vector2(52, 44), "+", 22,
                    () => _callbacks.OnAllocateStat?.Invoke(statId));
                var plusRect = plus.GetComponent<RectTransform>();
                plusRect.pivot = new Vector2(1f, 0.5f);
                _statButtons[i] = plus.GetComponent<Button>();
                _statGroups[i] = plus.AddComponent<CanvasGroup>();
            }

            var hint = Label(content.transform, 16, -258, 360, 36,
                "공격 +3%/pt • 체력 +8HP/pt • 이속 +2%/pt (캡 10)\n던전 강하에만 적용된다.", 11, TextAnchor.UpperLeft);
            hint.color = InkDim;

            // v1.3 M1: honest bottom summary — the three effective values,
            // no invented aggregate score (spec: 허수 지표 금지).
            _growthSummary = Label(content.transform, 16, -300, 360, 22, "", 12, TextAnchor.MiddleLeft);
            _growthSummary.color = Gold;

            var motionButton = TextButton(content.transform, new Vector2(0, 1),
                new Vector2(16, -344), new Vector2(368, 92), "모션: 보통", 15,
                () =>
                {
                    ViewPrefs.ReducedMotion = !ViewPrefs.ReducedMotion;
                    RefreshMotionLabel();
                });
            _motionLabel = motionButton.GetComponentInChildren<Text>();
            RefreshMotionLabel();
            return content;
        }

        GameObject BuildEquipTab(Transform parent)
        {
            var content = TabContent(parent);
            var hint = Label(content.transform, 16, -6, 360, 22, "유물로 장비를 강화한다 (T0-T5)", 13, TextAnchor.MiddleLeft);
            hint.color = InkDim;

            for (var i = 0; i < 3; i++)
            {
                var row = Panel(content.transform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(16, -36 - i * 72), new Vector2(368, 64), new Color(1f, 1f, 1f, 0.04f));
                RowIcon(row.transform, "equip-" + EquipIds[i]);
                Label(row.transform, 52, -8, 120, 24, EquipNames[i], 17, TextAnchor.MiddleLeft);
                // v1.3 M2: tier narrative + real rank effect ("판결인 T5 •
                // 공격 +30%") — content owned by Refresh.
                _equipDerived[i] = Label(row.transform, 52, -34, 200, 18, "", 11, TextAnchor.MiddleLeft);
                _equipDerived[i].color = Gold;
                _equipValues[i] = Label(row.transform, 150, 0, 90, 64, "T0/T5", 16, TextAnchor.MiddleCenter);
                _equipValues[i].color = Cyan;

                var slotId = EquipIds[i];
                var buy = TextButton(row.transform, new Vector2(1, 0.5f), new Vector2(-10, 0),
                    new Vector2(136, 44), "구매 (2 유물)", 13,
                    () => _callbacks.OnBuyEquip?.Invoke(slotId));
                var buyRect = buy.GetComponent<RectTransform>();
                buyRect.pivot = new Vector2(1f, 0.5f);
                _equipButtons[i] = buy.GetComponent<Button>();
                _equipButtonLabels[i] = buy.GetComponentInChildren<Text>();
                _equipGroups[i] = buy.AddComponent<CanvasGroup>();
            }

            var costs = Label(content.transform, 16, -258, 360, 22,
                "티어 비용: 2 • 4 • 7 • 11 • 16 유물", 11, TextAnchor.MiddleLeft);
            costs.color = InkDim;
            return content;
        }

        GameObject BuildLegionTab(Transform parent)
        {
            var content = TabContent(parent);
            var hint = Label(content.transform, 16, -6, 360, 22, "던전에 동행할 동료 1체 선택", 13, TextAnchor.MiddleLeft);
            hint.color = InkDim;

            // Slot 0: none. Slots 1..: every obtainable companion (pre-built,
            // Refresh flips owned/active state only).
            for (var slot = 0; slot < CompanionIds.Length + 1; slot++)
            {
                var column = slot % 2;
                var rowIndex = slot / 2;
                var label = slot == 0 ? "없음" : CompanionNames[slot - 1];
                var id = slot == 0 ? "" : CompanionIds[slot - 1];
                var button = TextButton(content.transform, new Vector2(0, 1),
                    new Vector2(16 + column * 188, -36 - rowIndex * 56),
                    new Vector2(180, 48), label, 14,
                    () => _callbacks.OnSelectCompanion?.Invoke(id), plated: false);
                _rosterBackgrounds[slot] = button.GetComponent<Image>();
                _rosterButtons[slot] = button.GetComponent<Button>();
                _rosterLabels[slot] = button.GetComponentInChildren<Text>();

                // v1.3 M4: one-line identity epithet under the name (stage-
                // epithet grammar). Name keeps the top band so the state
                // colors (Refresh) stay legible; epithet is static text —
                // built once, never touched again. Slot 0 (없음) has none.
                if (slot > 0)
                {
                    _rosterLabels[slot].rectTransform.offsetMin = new Vector2(0f, 14f);
                    var epithet = Label(button.transform, 0, 0, 180, 16,
                        CompanionEpithets[slot - 1], 9, TextAnchor.MiddleCenter);
                    epithet.color = InkDim;
                    var epithetRect = epithet.rectTransform;
                    epithetRect.anchorMin = new Vector2(0f, 0f);
                    epithetRect.anchorMax = new Vector2(1f, 0f);
                    epithetRect.pivot = new Vector2(0.5f, 0f);
                    epithetRect.anchoredPosition = new Vector2(0f, 4f);
                    epithetRect.sizeDelta = new Vector2(0f, 14f);
                }
            }

            var note = Label(content.transform, 16, -224, 360, 36,
                "보스 첫 처치•정예 추출로 로스터가 늘어난다.\n동료는 플레이어 피해의 60%로 지원한다.", 11, TextAnchor.UpperLeft);
            note.color = InkDim;
            return content;
        }

        void SelectTab(int index)
        {
            for (var i = 0; i < 3; i++)
            {
                _tabContents[i].SetActive(i == index);
                _tabBackgrounds[i].color = i == index ? ButtonActive : ButtonBack;
            }
        }

        // ------------------------------------------------------------- factory --
        // Cloned from HudView (uGUI, runtime-generated, no assets).
        GameObject Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                         Vector2 anchored, Vector2 size, Color color)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
            return panel;
        }

        /// <summary>36px sprite at the row's left edge; no-op when missing.</summary>
        void RowIcon(Transform row, string iconId)
        {
            var sprite = Resources.Load<Sprite>("Icons/" + iconId);
            if (sprite == null) return;   // Image without sprite = white quad
            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(row, false);
            var icon = iconObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, 0f);
            rect.sizeDelta = new Vector2(36f, 36f);
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
            text.color = new Color(0.92f, 0.94f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }

        GameObject TextButton(Transform parent, Vector2 anchor, Vector2 anchored,
                              Vector2 size, string label, int fontSize,
                              UnityEngine.Events.UnityAction onClick,
                              bool plated = true)
        {
            var buttonObject = Panel(parent, anchor, anchor, anchored, size, ButtonBack);
            buttonObject.GetComponent<Image>().raycastTarget = true;
            // 9-slice ember plate for stateless action buttons. Stateful groups
            // (tabs, roster) keep the flat fill because Refresh/SelectTab drive
            // Image.color as the state signal - a sprite would multiply-tint.
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
            var text = Label(buttonObject.transform, 0, 0, size.x, size.y, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return buttonObject;
        }

        /// <summary>Card: inner panel with the original border-line token.</summary>
        GameObject Card(Transform parent, float y, float height)
        {
            var card = Panel(parent, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.03f));
            var rect = card.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12, y - height);
            rect.offsetMax = new Vector2(-12, y);
            Border(card.transform, true);
            return card;
        }

        /// <summary>Original eyebrow pattern: EN kicker above, KR title below.</summary>
        void Eyebrow(Transform parent, float x, float y, string kicker, string title)
        {
            var kickerText = Label(parent, x, y, 260, 14, kicker, 10, TextAnchor.MiddleLeft);
            kickerText.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
            var titleText = Label(parent, x, y - 14, 260, 26, title, 18, TextAnchor.MiddleLeft);
            titleText.color = new Color(0.92f, 0.94f, 1f);
        }

        /// <summary>1px border lines. full=false draws only the bottom edge
        /// (top-bar underline).</summary>
        void Border(Transform parent, bool full)
        {
            Line(parent, new Vector2(0, 0), new Vector2(1, 0));          // bottom
            if (!full) return;
            Line(parent, new Vector2(0, 1), new Vector2(1, 1));          // top
            LineVertical(parent, 0);
            LineVertical(parent, 1);
        }

        void Line(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent, false);
            var image = line.AddComponent<Image>();
            image.color = BorderColor;
            image.raycastTarget = false;
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 1);
        }

        void LineVertical(Transform parent, float anchorX)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent, false);
            var image = line.AddComponent<Image>();
            image.color = BorderColor;
            image.raycastTarget = false;
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorX, 0);
            rect.anchorMax = new Vector2(anchorX, 1);
            rect.pivot = new Vector2(anchorX, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1, 0);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        static void AnchorTopRight(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
        }
    }
}
