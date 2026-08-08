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
        /// <summary>v1.5: unlock a sigil for relics. Arg = SigilKind as int.</summary>
        public System.Action<int> OnBuySigil;
        /// <summary>v1.5: equip/flip/unequip. Args = (SigilKind, SigilFace) as ints.</summary>
        public System.Action<int, int> OnEquipSigil;
        /// <summary>v1.6: enter a training trial. Args = (trial index, tier).</summary>
        public System.Action<int, int> OnStartTrial;
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


        // AMENDMENT #8: caps and prices moved to ProgressionGuide. The badge
        // rule has to compare the same prices the buy buttons charge, and this
        // file used to hold one copy while GameDirector held another — a third
        // reader was the moment to collapse them, not to add a third copy.
        const int StatCap = ProgressionGuide.StatCap;
        static readonly int EquipCap = ProgressionGuide.EquipCap;
        static readonly string[] StatIds = { "attack", "vitality", "swiftness" };
        static readonly string[] StatNames = { "공격", "체력", "이속" };
        static readonly string[] EquipIds = { "weapon", "lantern", "cloak" };
        static readonly string[] EquipNames = { "무기", "랜턴", "망토" };
        static readonly int[] EquipCosts = ProgressionGuide.EquipCosts;

        // --- v1.5 sigils (AMENDMENT #6 · design/sigil-spec.md) ------------------
        const int TabCount = 4;
        /// <summary>Relics to unlock one sigil, once. Internal so the economy
        /// test pins it and a negotiation can move it in one line.</summary>
        internal const int SigilCost = ProgressionGuide.SigilCost;
        /// <summary>Catalog order. Index 0 is deliberately the inert None so the
        /// stored ints are the SigilKind enum values with no remapping.</summary>
        internal static readonly SigilKind[] SigilOrder = ProgressionGuide.SigilOrder;
        static readonly string[] SigilNames = { "역류인", "판결인", "집행인", "점화인", "증언인" };
        /// <summary>Which gimmick each sigil binds — the line that tells the
        /// player WHERE it matters, not just what number moves.</summary>
        static readonly string[] SigilGimmicks = { "해류", "방벽주", "장벽", "분출구", "제단" };
        // Face copy: [kind][face]. A = survive the gimmick, B = turn it on them.
        static readonly string[][] SigilFaceNames =
        {
            new[] { "역류 저항", "와류" },
            new[] { "관통 판결", "파쇄" },
            new[] { "집행 저항", "처형" },
            new[] { "재점화", "연쇄" },
            new[] { "속기", "증폭" },
        };
        static readonly string[][] SigilFaceEffects =
        {
            new[] { "내가 받는 해류 밀기 절반", "적이 받는 해류 밀기 1.5배" },
            new[] { "방벽주 실드 40% → 70%", "방벽주에 주는 피해 2배" },
            new[] { "벽이 나에게 주는 피해 10 → 6", "벽이 적에게 주는 피해 10 → 18" },
            new[] { "분출구에 맞으면 기름 +12", "분출구가 적에게 피해 14" },
            new[] { "제단 채널 1.2초 → 0.8초", "제단 기름 +18 → +30" },
        };

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

        // --- AMENDMENT #8 accordion (design/progression-navigation-spec.md) ---
        /// <summary>Fold-header height. 44 u is the tab strip's height, chosen
        /// so the new controls join an existing debt class instead of setting a
        /// new worst (negotiation entry 12).</summary>
        const float HeaderHeight = 44f;
        const float HeaderPitch = 48f;
        const float GroupGap = 2f;
        /// <summary>Card pitch, frozen since cycle 2. Compressing it is what
        /// would sink the 강하 button under the touch floor.</summary>
        const float CardPitch = 70f;
        const float ContentTop = 6f;
        RectTransform _stageContentRect;
        ScrollRect _stageScroll;
        RectTransform[] _groupRects;
        GameObject[] _groupBodies;
        Image[] _groupHeaderBacks;
        Text[] _groupCounts;
        Text[] _groupCarets;
        int _expandedGroup;
        /// <summary>The player tapped a header this visit, so stop following
        /// the target until they leave the lobby.</summary>
        bool _groupPinned;
        Text _sortieProgress;
        /// <summary>SANCTUM tab badges — one dot per tab, lit by
        /// ProgressionGuide.Badges.</summary>
        Image[] _tabBadges;
        /// <summary>각인 context labels (N11): does this sigil's gimmick appear
        /// in the next target stage?</summary>
        readonly Text[] _sigilContext = new Text[5];

        // Tabs.
        GameObject[] _tabContents;
        Image[] _tabBackgrounds;

        // v1.5 각인 rows (AMENDMENT #6). Built once, Refresh flips state only.
        readonly Text[] _sigilTitles = new Text[5];
        readonly Text[] _sigilEffects = new Text[5];
        readonly GameObject[] _sigilBuyButtons = new GameObject[5];
        readonly Text[] _sigilBuyLabels = new Text[5];
        readonly GameObject[,] _sigilFaceButtons = new GameObject[5, 2];
        readonly Image[,] _sigilFaceBackgrounds = new Image[5, 2];
        readonly Text[,] _sigilFaceLabels = new Text[5, 2];
        Text _sigilFooter;

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
            // v1.6: the repeat visit is no longer a replay of the three waves —
            // it is the training ground below, so the cleared state points there
            // instead of promising the same tutorial again.
            var target = ProgressionGuide.NextTarget(in data);
            var prologueTarget = target.Kind == GuideTargetKind.Prologue;
            _prologueStatus.text = data.PrologueDone ? "훈련장 개방"
                : prologueTarget ? "다음 재판" : "필수 훈련";
            _prologueStatus.color = data.PrologueDone ? Gold : Ember;
            _prologueButtonLabel.text = data.PrologueDone ? "재훈련" : "점화 훈련";
            // cycle2 B3: first-run guide — ember border pulse until done.
            SetPrologueGuide(!data.PrologueDone);
            RefreshTrials(in data);

            // N6: the gauge the save has always been able to answer. 9 bits of
            // ClearedMask, never once counted on screen.
            var clearedTotal = ProgressionGuide.ClearedTotal(in data);
            _sortieProgress.text =
                $"정화 {clearedTotal}/{StageCatalog.Entries.Count}" + TargetSuffix(in data, in target);

            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                var cleared = StageCatalog.IsCleared(in data, in entry);
                var unlocked = StageCatalog.IsUnlocked(in data, in entry);
                // N2 (공개형): the target card says WHICH door is next. It does
                // not say to go through it — every other unlocked 강하 button
                // stays live. The survey found next-best-action recommendation
                // in 0 of 6 comparable titles and Hades excluding it on
                // purpose; what the genre does instead is disclose.
                var isTarget = target.Kind == GuideTargetKind.Stage && target.Index == i;
                _stageStatus[i].text = cleared ? "정화 완료"
                    : isTarget ? "다음 재판" : unlocked ? "강하 가능" : "잠김";
                _stageStatus[i].color = cleared ? Gold : isTarget ? Ember : unlocked ? Cyan : Lock;
                _stageButtons[i].interactable = unlocked;
                _stageGroups[i].alpha = unlocked ? 1f : 0.45f;

                // N3: the reason lives on the sub-line, which keeps leading with
                // the epithet so a locked card never stops previewing its
                // gimmick. Colour follows the line's job: gold while it is
                // advertising a reward, ink-dim while it is explaining a lock.
                var rewardText = string.IsNullOrEmpty(entry.CompanionReward)
                    ? "동행 없음"
                    : CompanionNameFor(entry.CompanionReward);
                _stageSubLabels[i].text = ProgressionGuide.StageSubLine(in data, in entry, rewardText);
                _stageSubLabels[i].color = unlocked ? Gold : InkDim;

                // v1.3 M3b: 서약 toggle appears once the stage is cleared. The
                // cleared card drops its redeemed '보상:' tail at the same
                // moment, so the toggle never covers live reward text.
                _pactButtons[i].SetActive(cleared);
                if (cleared) SyncPactVisual(i, IsPactArmed(entry.Id));
            }

            RefreshGroupHeaders(in data, in target);

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
            PlateStateful(_rosterBackgrounds[0], noneActive);
            for (var i = 0; i < CompanionIds.Length; i++)
            {
                var owned = RosterContains(data.Roster, CompanionIds[i]);
                var active = owned && data.Active == CompanionIds[i];
                _rosterLabels[i + 1].text = owned ? CompanionNames[i] : $"{CompanionNames[i]} (미보유)";
                _rosterLabels[i + 1].color = active ? Gold : owned ? Cyan : Lock;
                PlateStateful(_rosterBackgrounds[i + 1], active);
                _rosterButtons[i + 1].interactable = owned;
            }

            // --- 각인 (v1.5) -------------------------------------------------
            // Locked row shows the price; owned row shows the A/B pair with the
            // equipped face lit. Slot pressure is stated, never hidden: the footer
            // always says how many of the two slots are spent.
            var equipped0 = data.SigilSlot0;
            var equipped1 = data.SigilSlot1;
            var used = (equipped0 != 0 ? 1 : 0) + (equipped1 != 0 ? 1 : 0);
            for (var i = 0; i < SigilOrder.Length; i++)
            {
                var kind = (int)SigilOrder[i];
                var owned = (data.SigilsOwned & (1 << kind)) != 0;
                var slotted = equipped0 == kind || equipped1 == kind;
                var face = (data.SigilFaces & (1 << kind)) != 0 ? 1 : 0;

                _sigilTitles[i].color = slotted ? Gold : owned ? Cyan : Lock;
                _sigilEffects[i].text = owned
                    ? SigilFaceEffects[i][face]
                    : $"{SigilGimmicks[i]} 기믹에 걸리는 각인";

                // N11 context. Reads the target stage's EFFECTIVE hazard table
                // (override, else the frozen sim anchor) — the verdict pact is
                // excluded on purpose: it is a per-visit opt-in, and a label
                // that flipped under a toggle would be advice about a run the
                // player has not chosen yet.
                if (target.Kind == GuideTargetKind.Stage)
                {
                    var live = ProgressionGuide.SigilLiveInTarget(in data, SigilOrder[i], in target);
                    _sigilContext[i].text = live ? "다음 재판 대상" : "휴면";
                    _sigilContext[i].color = live ? Gold : InkDim;
                }
                else
                {
                    _sigilContext[i].text = "대기";
                    _sigilContext[i].color = InkDim;
                }

                _sigilBuyButtons[i].SetActive(!owned);
                if (!owned)
                {
                    var affordable = data.Relics >= SigilCost;
                    _sigilBuyLabels[i].text = $"유물 {SigilCost} → 해금";
                    _sigilBuyLabels[i].color = affordable ? Gold : Lock;
                    _sigilBuyButtons[i].GetComponent<Button>().interactable = affordable;
                }

                for (var f = 0; f < 2; f++)
                {
                    _sigilFaceButtons[i, f].SetActive(owned);
                    if (!owned) continue;
                    var lit = slotted && face == f;
                    _sigilFaceLabels[i, f].text = SigilFaceNames[i][f];
                    _sigilFaceLabels[i, f].color = lit ? Ember : InkDim;
                    _sigilFaceBackgrounds[i, f].color = lit
                        ? new Color(Ember.r, Ember.g, Ember.b, 0.22f)
                        : ButtonBack;
                }
            }
            _sigilFooter.text = used >= SigilLoadout.Slots
                ? $"슬롯 {used}/{SigilLoadout.Slots} — 새로 끼우면 먼저 낀 각인이 빠진다."
                : $"슬롯 {used}/{SigilLoadout.Slots} — 면 전환은 무료다.";

            // N5 badges last: they read the same prices the buy buttons above
            // just charged, so computing them here keeps one evaluation order
            // and no chance of a badge disagreeing with the button under it.
            var badges = ProgressionGuide.Badges(in data);
            _tabBadges[0].gameObject.SetActive(badges.Growth);
            _tabBadges[1].gameObject.SetActive(badges.Equip);
            _tabBadges[2].gameObject.SetActive(badges.Legion);
            _tabBadges[3].gameObject.SetActive(badges.Sigil);
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

        /// <summary>Leaving the lobby drops the fold pin. A header tap is a
        /// decision about this visit; on the next one the accordion goes back to
        /// following the target, which is the whole point of the automatic
        /// follow — a player returning from a clear should find the group that
        /// just changed already open.</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _groupPinned = false;
        }

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
            // N6 gauge. The eyebrow's right shoulder is the only clear span in
            // the panel header — 정화 n/9 plus the target's epithet fits there
            // without touching the prologue card's audited 174 u band below.
            _sortieProgress = Label(panel.transform, -16, -14, 250, 32, "", 11,
                TextAnchor.UpperRight);
            AnchorTopRight(_sortieProgress.rectTransform);
            _sortieProgress.color = Gold;

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
            // v1.6 mastery line. It lives HERE and not on the tier card because
            // the tier card carries three buttons: measured, that leaves 76 u of
            // clear width beside them and no useful Korean string fits — the
            // first build put it there and the browser showed the text running
            // straight under 견습. This card is 100 u tall with ONE button, so
            // the row at -82 has 232 u clear to the button's left edge.
            _trialMasteryLabel = Label(prologue.transform, 12, -82, 230, 16, "", 10,
                TextAnchor.MiddleLeft);
            _prologueStatus = Label(prologue.transform, -12, -10, 120, 20, "", 13, TextAnchor.MiddleRight);
            AnchorTopRight(_prologueStatus.rectTransform);
            var prologueButton = TextButton(prologue.transform, new Vector2(1, 0), new Vector2(-12, 10),
                new Vector2(112, 44), "점화 훈련", 15,
                () => _callbacks.OnSortie?.Invoke("prologue"));
            prologueButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            _prologueButtonLabel = prologueButton.GetComponentInChildren<Text>();

            // AMENDMENT #8 — the flat scroll becomes four folded groups.
            //
            // Cycle-2 added three stages to a fixed panel and paid for it with
            // a scroll: 1058 u of content behind a 434 u viewport, 41.0% of the
            // list visible, no scrollbar, no position cue of any kind. The
            // survey's answer was not "add a scrollbar" — 11 of 11 comparable
            // titles bound the list spatially instead, and a scroll list with
            // no position indicator appeared in exactly zero of them.
            //
            // worldview.md already groups the nine stages into three acts, so
            // the grouping is not an invention: folding to one open act puts
            // 416 u on screen (100%) and folding to the training group puts
            // 626 u (69.3%, two whole trial rows — today it is zero, because
            // the first trial card starts 272 u below the viewport floor).
            //
            // The ScrollRect stays as the training group's overflow. It is no
            // longer the navigation model, only the fallback for the one group
            // that cannot fit.
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
            _stageContentRect = stageContent.AddComponent<RectTransform>();
            _stageContentRect.anchorMin = new Vector2(0f, 1f);
            _stageContentRect.anchorMax = new Vector2(1f, 1f);
            _stageContentRect.pivot = new Vector2(0.5f, 1f);
            _stageContentRect.anchoredPosition = Vector2.zero;
            _stageContentRect.sizeDelta = new Vector2(0f, ContentTop);   // LayoutGroups owns the rest

            _stageScroll = stageViewport.AddComponent<ScrollRect>();
            _stageScroll.content = _stageContentRect;
            _stageScroll.viewport = viewportRect;
            _stageScroll.horizontal = false;
            _stageScroll.vertical = true;
            _stageScroll.movementType = ScrollRect.MovementType.Clamped;
            _stageScroll.scrollSensitivity = 24f;

            _groupRects = new RectTransform[ProgressionGuide.GroupCount];
            _groupBodies = new GameObject[ProgressionGuide.GroupCount];
            _groupHeaderBacks = new Image[ProgressionGuide.GroupCount];
            _groupCounts = new Text[ProgressionGuide.GroupCount];
            _groupCarets = new Text[ProgressionGuide.GroupCount];

            for (var g = 0; g < ProgressionGuide.GroupCount; g++)
            {
                var group = new GameObject("Group" + g);
                group.transform.SetParent(stageContent.transform, false);
                var groupRect = group.AddComponent<RectTransform>();
                groupRect.anchorMin = new Vector2(0f, 1f);
                groupRect.anchorMax = new Vector2(1f, 1f);
                groupRect.pivot = new Vector2(0.5f, 1f);
                groupRect.sizeDelta = Vector2.zero;
                _groupRects[g] = groupRect;

                var training = g == ProgressionGuide.TrainingGroup;
                BuildGroupHeader(group.transform, g,
                    training ? ProgressionGuide.TrainingKicker : ProgressionGuide.ActKickers[g],
                    training ? ProgressionGuide.TrainingTitle : ProgressionGuide.ActTitles[g],
                    training ? Cyan : ActAccent(g));

                var body = new GameObject("Body");
                body.transform.SetParent(group.transform, false);
                var bodyRect = body.AddComponent<RectTransform>();
                bodyRect.anchorMin = new Vector2(0f, 1f);
                bodyRect.anchorMax = new Vector2(1f, 1f);
                bodyRect.pivot = new Vector2(0.5f, 1f);
                bodyRect.anchoredPosition = new Vector2(0f, -HeaderPitch);
                bodyRect.sizeDelta = new Vector2(0f, RowsInGroup(g) * CardPitch);
                _groupBodies[g] = body;

                if (training) BuildTrialCards(body.transform);
                else BuildActStages(body.transform, g);
            }

            SelectGroup(0, pin: false);
        }

        /// <summary>Act accent — the worldview colour language, not a new one:
        /// 기록 = memory = cyan, 증언 = judgement = gold, 집행 = danger = ember.</summary>
        static Color ActAccent(int act) => act == 0 ? Cyan : act == 1 ? Gold : Ember;

        /// <summary>Rows a group shows when open. The training group carries the
        /// shared tier selector plus one card per trial.</summary>
        static int RowsInGroup(int group)
            => group == ProgressionGuide.TrainingGroup
                ? 1 + TrainingTrials.Ids.Length
                : ProgressionGuide.StagesPerAct;

        /// <summary>
        /// A fold header. Stateful button grammar (sprite swap, flat fill) —
        /// the same language the tabs and the roster use, because it carries the
        /// same kind of state: which one of a set is open.
        ///
        /// Measured 179.6 x 21.5 CSS px at the worst phone tier. The height is
        /// the tab strip's existing debt class and the width is the widest
        /// control in the lobby, so this joins the frozen table without setting
        /// a new worst (negotiation entry 12).
        /// </summary>
        void BuildGroupHeader(Transform parent, int group, string kicker, string title, Color accent)
        {
            var header = Panel(parent, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, ButtonBack);
            var rect = header.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, -HeaderHeight);
            rect.offsetMax = new Vector2(-12f, 0f);
            var background = header.GetComponent<Image>();
            background.raycastTarget = true;
            _groupHeaderBacks[group] = background;

            var index = group;
            header.AddComponent<Button>().onClick.AddListener(() => SelectGroup(index, pin: true));

            // Korean title FIRST. Audits and layout tests identify a control by
            // its first Text child, and the thing a player sees on this header
            // is the act name — an English kicker as the audit key would name
            // the control something no player has ever read. Visual order is
            // unchanged: the kicker still sits above, by anchor, not by index.
            var titleText = Label(header.transform, 12, -16, 200, 24, title, 16, TextAnchor.MiddleLeft);
            titleText.color = new Color(0.92f, 0.94f, 1f);
            var kickerText = Label(header.transform, 12, -3, 200, 13, kicker, 9, TextAnchor.MiddleLeft);
            kickerText.color = new Color(accent.r, accent.g, accent.b, 0.85f);

            _groupCounts[group] = Label(header.transform, -34, -12, 62, 20, "", 12,
                TextAnchor.MiddleRight);
            AnchorTopRight(_groupCounts[group].rectTransform);
            _groupCounts[group].color = accent;

            _groupCarets[group] = Label(header.transform, -12, -12, 18, 20, "\u25b6", 12,
                TextAnchor.MiddleRight);
            AnchorTopRight(_groupCarets[group].rectTransform);
            _groupCarets[group].color = InkDim;
        }

        /// <summary>The three stage cards of one act. Card geometry is unchanged
        /// from the flat list — only the parent moved.</summary>
        void BuildActStages(Transform body, int act)
        {
            var first = act * ProgressionGuide.StagesPerAct;
            for (var slot = 0; slot < ProgressionGuide.StagesPerAct; slot++)
            {
                var i = first + slot;
                if (i >= StageCatalog.Entries.Count) break;
                var entry = StageCatalog.Entries[i];
                var card = Card(body, -slot * CardPitch, 68);
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
                // Fun-pass v1.2: gimmick epithet leads the reward line (spec
                // "기존 보상 라인 문법에 기믹 별칭 추가"). Merged instead of a new
                // row: the 68 u card has no vertical slack between the reward
                // band (-44..-60) and the 강하 button (bottom 6..34), and a new
                // row would break the audited pitch/44 px touch floor. The
                // hazard glyph at (12,-44) already sits directly left of this
                // label, so it doubles as the epithet's gimmick marker.
                //
                // AMENDMENT #8 gives this line a third job: on a locked card it
                // carries the lock REASON. That forced the CanvasGroup below.
                // A locked card sits at alpha 0.45, and every lobby colour
                // composited through 0.45 lands under 3.1:1 — gold 3.07, cyan
                // 2.31, the lock grey 1.71. There is no colour that passes, so
                // the fix cannot be a colour. The card stays dimmed because it
                // is not actionable; the sentence explaining WHY it is not
                // actionable is exempted from the dimming and reads at 8.69:1.
                var sub = Label(card.transform, 34, -44, 220, 16, "", 10, TextAnchor.MiddleLeft);
                sub.color = Gold;
                var subGroup = sub.gameObject.AddComponent<CanvasGroup>();
                subGroup.alpha = 1f;
                subGroup.ignoreParentGroups = true;
                subGroup.interactable = false;
                subGroup.blocksRaycasts = false;
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

        /// <summary>
        /// Open one group, close the rest, and re-stack every group's origin.
        ///
        /// Exactly one open group is what makes the height budget hold: the
        /// worst case is the training group at 626 u, and two open groups would
        /// blow past anything the viewport can show. <paramref name="pin"/>
        /// separates a player's tap from the automatic follow — while the
        /// player is in the lobby their choice stands, and leaving the lobby
        /// (Hide) drops the pin so the next visit opens on wherever they are.
        /// </summary>
        void SelectGroup(int group, bool pin)
        {
            if (_groupRects == null) return;
            _expandedGroup = group;
            if (pin) _groupPinned = true;

            var cursor = ContentTop;
            for (var g = 0; g < _groupRects.Length; g++)
            {
                var expanded = g == _expandedGroup;
                _groupBodies[g].SetActive(expanded);
                _groupRects[g].anchoredPosition = new Vector2(0f, -cursor);
                _groupCarets[g].text = expanded ? "\u25bc" : "\u25b6";
                PlateStateful(_groupHeaderBacks[g], expanded);

                var height = HeaderPitch + (expanded ? RowsInGroup(g) * CardPitch : 0f);
                _groupRects[g].sizeDelta = new Vector2(0f, height);
                cursor += height + GroupGap;
            }
            _stageContentRect.sizeDelta = new Vector2(0f, cursor);

            // A fold moves everything below it. Keeping the old scroll offset
            // would leave the group the player just opened partly above the
            // viewport — the one outcome a navigation feature must not produce.
            if (_stageScroll != null) _stageScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Per-group counts, and the automatic follow to the target's group.
        ///
        /// The follow is what makes the fold a navigation aid rather than a
        /// space saving: opening the lobby puts the player's actual position on
        /// screen without a tap. A tap of their own overrides it until they
        /// leave (see <see cref="Hide"/>).
        /// </summary>
        void RefreshGroupHeaders(in CampaignData data, in GuideTarget target)
        {
            if (_groupCounts == null) return;
            for (var act = 0; act < ProgressionGuide.ActCount; act++)
                _groupCounts[act].text =
                    $"{ProgressionGuide.ClearedInAct(in data, act)}/{ProgressionGuide.StagesPerAct}";
            _groupCounts[ProgressionGuide.TrainingGroup].text = data.PrologueDone
                ? $"{ProgressionGuide.MasteredTrials(in data)}/{TrainingTrials.Ids.Length}"
                : "잠김";

            if (!_groupPinned) SelectGroup(ProgressionGuide.GroupOfTarget(in target), pin: false);
        }

        /// <summary>The gauge's tail: which door is next, named by its gimmick.
        /// Disclosure, not instruction — the survey found recommendation in 0 of
        /// 6 comparable titles and disclosure everywhere.</summary>
        static string TargetSuffix(in CampaignData data, in GuideTarget target)
        {
            switch (target.Kind)
            {
                case GuideTargetKind.Prologue:
                    return "\n다음 재판: 점화 훈련";
                case GuideTargetKind.Stage:
                    return "\n다음 재판: " + StageCatalog.Entries[target.Index].Epithet;
                case GuideTargetKind.Trial:
                    return "\n다음 재판: " + TrialNames[target.Index];
                default:
                    return "\n전 구역 정화 완료";
            }
        }

        // ----------------------------------------------------- training ground --
        /// <summary>Trial display names, catalog order (AMENDMENT #7).</summary>
        public static readonly string[] TrialNames =
            { "불씨 시련", "해류 시련", "방벽 시련", "행진 시련", "증언 시련" };
        /// <summary>Tier names — the ladder the survey's T3 archetype asks for.</summary>
        public static readonly string[] TierNames = { "견습", "숙련", "판결" };
        static readonly string[] TrialLessons =
        {
            "예고를 읽고 링 밖으로",
            "순류와 역류의 이동 감각",
            "3기 파괴 순서와 이동선",
            "침식 타이밍 암기",
            "리듬 사이 채널 유지",
        };

        readonly Text[] _trialStatus = new Text[TrainingTrials.Ids.Length];
        readonly Button[] _trialButtons = new Button[TrainingTrials.Ids.Length];
        readonly CanvasGroup[] _trialGroups = new CanvasGroup[TrainingTrials.Ids.Length];
        Button[] _tierButtons = System.Array.Empty<Button>();
        Image[] _tierBackgrounds = System.Array.Empty<Image>();
        Text _trialMasteryLabel;
        int _selectedTier;

        /// <summary>
        /// A shared tier selector card, then one card per trial carrying exactly
        /// one stage-grammar button.
        ///
        /// The first draft put three tier buttons on EVERY trial card. The layout
        /// gate measured them at 28.3 x 13.7 CSS px — narrower than any existing
        /// lobby control (the previous worst was 강하 at 41.0 x 13.7), fifteen of
        /// them at once. A new smallest touch target is a defect, not a frozen-
        /// table update, so the tier choice moved to one shared row and every
        /// button here now matches the audited stage-card button exactly.
        /// </summary>
        void BuildTrialCards(Transform content)
        {
            var tierCard = Card(content, 0f, 68);
            Eyebrow(tierCard.transform, 12, -6, "TIER", "등급 선택");
            // Only 76 u is clear beside three buttons, so this row carries the
            // tier COUNT and nothing else. The mastery line is on the prologue
            // card, which has the width for a sentence.
            var tierHint = Label(tierCard.transform, 12, -44, 76, 16,
                $"{HackSpec.TrainingTiers}단", 10, TextAnchor.MiddleLeft);
            tierHint.color = InkDim;
            _tierButtons = new Button[HackSpec.TrainingTiers];
            _tierBackgrounds = new Image[HackSpec.TrainingTiers];
            for (var tier = 0; tier < HackSpec.TrainingTiers; tier++)
            {
                var tierIndex = tier;
                var button = TextButton(tierCard.transform, new Vector2(1, 0),
                    new Vector2(-12 - (HackSpec.TrainingTiers - 1 - tier) * 92, 6),
                    new Vector2(84, 28), TierNames[tier], 13,
                    () => SelectTier(tierIndex), plated: false);
                button.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
                _tierButtons[tier] = button.GetComponent<Button>();
                _tierBackgrounds[tier] = button.GetComponent<Image>();
            }

            for (var i = 0; i < TrainingTrials.Ids.Length; i++)
            {
                var card = Card(content, -(1 + i) * CardPitch, 68);
                Eyebrow(card.transform, 12, -6, "TRIAL", TrialNames[i]);
                var lesson = Label(card.transform, 12, -44, 230, 16, TrialLessons[i], 10,
                    TextAnchor.MiddleLeft);
                lesson.color = InkDim;
                _trialStatus[i] = Label(card.transform, -12, -8, 110, 18, "잠김", 11,
                    TextAnchor.MiddleRight);
                AnchorTopRight(_trialStatus[i].rectTransform);

                var trialIndex = i;
                var enter = TextButton(card.transform, new Vector2(1, 0), new Vector2(-12, 6),
                    new Vector2(84, 28), "수련", 13,
                    () => _callbacks.OnStartTrial?.Invoke(trialIndex, _selectedTier));
                enter.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
                _trialButtons[i] = enter.GetComponent<Button>();

                _trialGroups[i] = card.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>Session-only tier choice — never persisted, the same grammar
        /// the verdict pact toggle uses (a run-scoped decision, re-made per visit).</summary>
        void SelectTier(int tier)
        {
            _selectedTier = tier;
            for (var t = 0; t < _tierBackgrounds.Length; t++)
            {
                PlateStateful(_tierBackgrounds[t], t == _selectedTier);
            }
        }

        /// <summary>Trial row state. Locked until the prologue is cleared, then
        /// each row shows its best tier and the mastery line states the one-time
        /// grant (negotiation entry 7) instead of implying a repeatable payout.</summary>
        void RefreshTrials(in CampaignData data)
        {
            var open = data.PrologueDone;
            for (var i = 0; i < TrainingTrials.Ids.Length; i++)
            {
                var best = CampaignStore.BestTier(in data, i);
                _trialStatus[i].text = !open
                    ? "잠김"
                    : (best < 0 ? "미도전" : $"최고 {TierNames[best]}");
                _trialStatus[i].color = !open ? Lock : (best < 0 ? InkDim : Gold);
                _trialGroups[i].alpha = open ? 1f : 0.45f;
                _trialButtons[i].interactable = open;
            }
            for (var tier = 0; tier < _tierButtons.Length; tier++)
            {
                _tierButtons[tier].interactable = open;
            }
            SelectTier(_selectedTier);
            if (_trialMasteryLabel != null)
            {
                _trialMasteryLabel.text = data.TrainingMasteryClaimed
                    ? "숙달 보상 수령됨"
                    : $"5시련 판결 완주 → 유물 +{HackSpec.TrainingMasteryRelics} (1회)";
                _trialMasteryLabel.color = data.TrainingMasteryClaimed ? InkDim : Gold;
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

            // Segmented tab buttons. v1.5 adds 각인 as a fourth tab; the strip
            // re-divides the same 400 u panel (4 x 91 u, pitch 95) instead of
            // growing, so the sanctum keeps its audited footprint. Width 91 u
            // = 44.4 CSS px at the worst phone scale, which CLEARS the touch
            // floor on that axis — the 44 u height debt is untouched and stays
            // the designer+pm item it already was (LobbyLayoutTests ratchet).
            string[] tabNames = { "성장", "장비", "군단", "각인" };
            _tabContents = new GameObject[TabCount];
            _tabBackgrounds = new Image[TabCount];
            _tabBadges = new Image[TabCount];
            for (var i = 0; i < TabCount; i++)
            {
                var tabIndex = i;
                var tab = TextButton(panel.transform, new Vector2(0, 1),
                    new Vector2(16 + i * 95, -60), new Vector2(91, 44), tabNames[i], 15,
                    () => SelectTab(tabIndex), plated: false);
                _tabBackgrounds[i] = tab.GetComponent<Image>();

                // N5 badge — a 7 u ember dot in the tab's top-right corner.
                // Deliberately NOT a count and NOT a price: the survey's
                // sharpest failure case is Darkest Dungeon 2's Altar of Hope,
                // where a screen added to answer "no progression" became the
                // source of "grindfest" complaints. A badge that quantifies is
                // a purchase prompt. This one only says a door is open.
                var badge = Panel(tab.transform, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-6, -6), new Vector2(7, 7), Ember);
                badge.name = "Badge";
                badge.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
                _tabBadges[i] = badge.GetComponent<Image>();
                badge.SetActive(false);
            }

            _tabContents[0] = BuildGrowthTab(panel.transform);
            _tabContents[1] = BuildEquipTab(panel.transform);
            _tabContents[2] = BuildLegionTab(panel.transform);
            _tabContents[3] = BuildSigilTab(panel.transform);
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

        /// <summary>
        /// v1.5 각인 tab. One row per sigil: name + bound gimmick, then either a
        /// buy button (locked) or the A/B face pair (owned). Rows are built once;
        /// Refresh only flips text, colour and interactable — the same contract
        /// every other tab keeps.
        /// </summary>
        GameObject BuildSigilTab(Transform parent)
        {
            var content = TabContent(parent);
            var hint = Label(content.transform, 16, -6, 360, 22,
                $"기믹에 걸리는 각인. {SigilLoadout.Slots}개까지 장착", 13, TextAnchor.MiddleLeft);
            hint.color = InkDim;

            for (var i = 0; i < SigilOrder.Length; i++)
            {
                var row = i;
                var y = -32 - i * 74;

                var title = Label(content.transform, 16, y, 130, 18,
                    $"{SigilNames[i]} • {SigilGimmicks[i]}", 13, TextAnchor.MiddleLeft);
                title.color = Gold;
                _sigilTitles[i] = title;

                // N11 — the axis the genre could not have: 0 of 17 surveyed
                // titles mark whether a meta upgrade is live in what comes
                // next, because in all 17 the upgrade is a global constant and
                // the question has no answer. Ours bind to gimmicks: 집행인
                // fires in one stage of nine, 점화인 in nine of nine, and both
                // cost the same 12 relics. Price cannot say that. This can.
                //
                // It says only whether the gimmick appears. It never says buy.
                _sigilContext[i] = Label(content.transform, 150, y, 78, 18, "", 9,
                    TextAnchor.MiddleLeft);
                _sigilContext[i].color = InkDim;

                var effect = Label(content.transform, 16, y - 18, 210, 16, "", 10, TextAnchor.MiddleLeft);
                effect.color = InkDim;
                _sigilEffects[i] = effect;

                // Buy (locked state) — replaced in place by the face pair once owned.
                var buy = TextButton(content.transform, new Vector2(0, 1),
                    new Vector2(232, y - 6), new Vector2(140, 30), "", 12,
                    () => BuySigil(row), plated: false);
                _sigilBuyButtons[i] = buy;
                _sigilBuyLabels[i] = buy.GetComponentInChildren<Text>();

                for (var f = 0; f < 2; f++)
                {
                    var face = f;
                    var button = TextButton(content.transform, new Vector2(0, 1),
                        new Vector2(232 + face * 72, y - 6), new Vector2(68, 30), "", 11,
                        () => ToggleSigil(row, (SigilFace)face), plated: false);
                    _sigilFaceButtons[i, f] = button;
                    _sigilFaceBackgrounds[i, f] = button.GetComponent<Image>();
                    _sigilFaceLabels[i, f] = button.GetComponentInChildren<Text>();
                }
            }

            _sigilFooter = Label(content.transform, 16, -406, 360, 32, "", 11, TextAnchor.UpperLeft);
            _sigilFooter.color = InkDim;
            return content;
        }

        /// <summary>Unlocks a sigil for relics. One-time, no refund path — the
        /// FACE is what stays free to change (spec §형태).</summary>
        void BuySigil(int row)
        {
            _callbacks.OnBuySigil?.Invoke((int)SigilOrder[row]);
        }

        /// <summary>
        /// Equip/unequip, or flip an equipped sigil to its other face. Pressing the
        /// face already showing removes the sigil; pressing the other face swaps to
        /// it. Equipping past the slot limit evicts the oldest — a full loadout must
        /// never silently swallow the tap.
        /// </summary>
        void ToggleSigil(int row, SigilFace face)
        {
            _callbacks.OnEquipSigil?.Invoke((int)SigilOrder[row], (int)face);
        }

        void SelectTab(int index)
        {
            for (var i = 0; i < TabCount; i++)
            {
                _tabContents[i].SetActive(i == index);
                // Sprite swap, not tint — see PlateStateful. Colour stays
                // white so the plate art is not multiplied.
                PlateStateful(_tabBackgrounds[i], i == index);
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
            // HudView.Panel sets this and LobbyView never did, so every lobby
            // panel, card, border line and row has been an invisible raycast
            // target — decoration eating clicks meant for what is under it.
            // Callers that need the click (TextButton) re-enable it explicitly
            // on the very next line, so this is safe to default off.
            image.raycastTarget = false;
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

        /// <summary>Plates a STATEFUL button (tab, roster row) so it joins the
        /// same button language as the action buttons.
        ///
        /// These previously kept a flat fill because the state signal was
        /// Image.color, and tinting a sprite multiplies it — the plate would
        /// go muddy. Swapping the sprite instead carries state without
        /// touching colour, so 12 buttons that were excluded by an art
        /// limitation now match the rest.</summary>
        static void PlateStateful(Image image, bool active)
        {
            var sprite = Resources.Load<Sprite>(
                active ? "Icons/ui-button-active" : "Icons/ui-button");
            if (sprite == null) return;   // fallback: caller's flat tint stands
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
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
