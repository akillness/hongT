// AMENDMENT #9 — the in-run codex (design/ingame-guidance-spec.md).
//
// Two tabs behind one left-stack button:
//   내 수치  — what the player's numbers ARE right now, and where they came from.
//   게임설명 — the 23 guidance entries, re-readable, unseen ones locked.
//
// AMENDMENT #10 renamed that second tab from 기록. The old name described a
// LOG — "what I have seen" — and a player looking for the rules does not look
// under a log. The tab holds how the game is played, how a run progresses, and
// the win/lose conditions, so it is named for that. Same defect class as the
// Ammonomicon note in design/trend-survey/progression-navigation.md: a record
// of what you saw is not a guide to what you have not.
//
// Why the stats tab exists at all: the lobby shows run-START values
// (LobbyView.cs:320-331, computed from HackConfig). The sim recomputes all four
// during a run in ApplyLevelStats() (CinderSim.cs:2666) as levels, extractions
// and growth choices land, and until IDerivedStatSnapshot existed nothing could
// read the result. A player at level 8 with two extractions could not learn
// their own attack power from anywhere in the game.
//
// The view does NOT recompute the product. It prints the sim's number and
// labels the factors that produced it. There is no second multiplication to
// drift, which is the point (§4e, §4i).
using System.Collections.Generic;
using System.Text;
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed partial class HudView
    {
        // ---------------------------------------------------------- state --
        GameObject _codexPanel, _codexStatsTab, _codexGuidanceTab;
        GameObject _codexTabStats, _codexTabGuidance;
        RectTransform _codexPanelRect;
        readonly Text[] _codexStatValues = new Text[4];
        readonly Text[] _codexStatBreakdowns = new Text[4];
        Text _codexHeader;
        readonly List<Text> _codexGuidanceBodies = new List<Text>();
        readonly List<Text> _codexGuidanceTitles = new List<Text>();
        readonly List<int> _codexGuidanceBits = new List<int>();
        /// <summary>Group index per built row, parallel to the two lists above.
        /// Indexes <see cref="GuidanceCatalog.GroupOrder"/>.</summary>
        readonly List<int> _codexGuidanceRowGroup = new List<int>();
        /// <summary>One container per group; exactly one is active at a time.</summary>
        readonly List<GameObject> _codexGroupPages = new List<GameObject>();
        readonly List<GameObject> _codexGroupChips = new List<GameObject>();
        readonly List<Text> _codexGroupChipCounts = new List<Text>();
        int _codexOpenGroup;
        bool _codexOpen, _codexShowingGuidance, _codexLatched;
        CodexStats _codexStats;

        /// <summary>
        /// Seen-predicate for the guidance tab. A predicate, NOT the record:
        /// the codex is a re-read surface and must not be able to set a bit.
        /// Marking on browse would silently suppress a pause card the player
        /// never actually received. Supplied by GameDirector, which owns the save.
        /// </summary>
        public System.Func<int, bool> CodexEntrySeen;

        const float CodexW = 620f, CodexH = 440f;

        // AMENDMENT #10 — the 게임설명 tab pages by group.
        //
        // Measured, not chosen: with wrap on and real glyph widths, showing all
        // 23 bodies at once needs 386 u in the best column count (3) against a
        // 300 u body. Two columns need 508, four need 380. Every arrangement
        // overflows, so the tab shows ONE group at a time. Worst group is 조작
        // at 155 u, which clears the 201.8 u body with 47 u to spare.
        const int CodexGroupCount = 5;
        /// <summary>44 CSS px at the worst measured phone scale (0.488 px/u).
        /// Chips are this size at EVERY tier: the body has 47 u of slack at the
        /// worst group, so a per-tier chip size would buy nothing and add a
        /// second geometry that only one tier ever exercises (§4f).</summary>
        const float CodexChipSide = 90.2f;
        /// <summary>Body rows start below the chip row, in TAB-LOCAL coordinates
        /// (the tab container is already parked at -92 inside the panel, so the
        /// panel offset must not be subtracted twice).</summary>
        const float CodexBodyTop = -(CodexChipSide + 8f);       // -98.2 tab-local
        /// <summary>Tab body height: 300 u tab minus the chip row. 201.8 u.</summary>
        const float CodexBodyH = 300f + CodexBodyTop;
        const float CodexRowGap = 5f, CodexTitleH = 15f;
        /// <summary>Minimum body height, and the rect width probe uses before
        /// measuring. NOT a line height: uGUI reports 11.0 for one line and
        /// 21.0 for two at font 9, so a line steps 10 and the first carries 1 of
        /// padding. Layout uses the measurement, never a multiple of this.</summary>
        const float CodexLineH = 11f;
        /// <summary>Two columns at 266 u of body width.
        ///
        /// Measured with the shipping font, deepest column against the 300 u tab:
        /// 2 columns 440 u, 3 columns 329 u, 4 columns 294 u. Four FITS, by 6 u —
        /// and it is still refused. At 121 u every hazard body wraps to three
        /// lines, and 2% of margin is one balance edit from overflowing (a
        /// constant going 2.4 -> 12.4 adds a character, a character adds a line,
        /// a line adds 10 u). Paging one group into two wide columns leaves 47 u
        /// and keeps most bodies on a single line.
        ///
        /// An earlier analytic estimate put these at 508/386/380 and called four
        /// columns impossible; it was 14% high on glyph widths. The decision did
        /// not change but its reason did, so the reason recorded here is the
        /// measured one.</summary>
        const int CodexBodyColumns = 2;

        /// <summary>The numbers, frozen at the frame the codex opened.</summary>
        struct CodexStats
        {
            public float Damage, MaxHealth, Speed, Regen;
            public float BaseDamage, BaseMaxHealth, BaseSpeed, BaseRegen;
            public float ExtractionBonus;
            public int Level, GrowthAttack, GrowthVitality, GrowthSwiftness;
            public int MetaAttack, MetaVitality, MetaSwiftness;
            public int WeaponRank, LanternRank, CloakRank;
        }

        // ------------------------------------------------------ open/close --
        void OpenCodex()
        {
            if (_codexOpen) return;
            // The abandon modal is the other surface that holds this run, and
            // the two must not stack. Found in the cycle-7 browser smoke: the
            // modal is 480x200 with a raycast blocker, so it stops taps that
            // land ON it and nothing else — the 정보 button sits outside that
            // rect and stayed live. The codex then opened over a live modal,
            // and one dismiss press had two owners.
            //
            // Pausing was never wrong (GuidancePaused ORs both), which is why
            // this survived AMENDMENT #9: every automated check that could have
            // seen it was asking about the pause, not about what was on screen.
            CloseAbandonModal();
            EnsureCodexPanel();
            _codexOpen = true;
            // Cleared here, filled by the next Sync. OpenCodex is a button
            // onClick — a parameterless UnityAction — so it has no sim to read.
            // The latch lives where the sim already arrives, once per frame.
            _codexLatched = false;
            _codexPanel.SetActive(true);
            // Every surface that holds a run releases the touch controls:
            // guidance card, abandon modal, game-over and stage-clear panels
            // all do it. Taps landing on a sim that cannot tick are worse than
            // no controls at all.
            SetTouchCombatControlsVisible(false);
            RefreshCodexGuidance();
            ShowCodexTab(guidance: false);
        }

        /// <summary>Closes the codex and releases the run.</summary>
        public void CloseCodex()
        {
            if (!_codexOpen) return;
            _codexOpen = false;
            _codexLatched = false;
            if (_codexPanel != null) _codexPanel.SetActive(false);
            SetTouchCombatControlsVisible(true);
        }

        /// <summary>
        /// True when a press should close the codex. Any key, tap or click —
        /// except one landing on a tab button or a group chip, because
        /// navigating the panel is the one interaction the panel owns.
        /// </summary>
        bool CodexPressDismisses(Vector2 screenPoint, bool positional)
        {
            if (!positional) return true;
            return !PointOverCodexTab(screenPoint);
        }

        /// <summary>
        /// Tab buttons AND group chips. The chips joined this predicate in the
        /// same edit that created them: a chip that closes the panel instead of
        /// paging it would make the 게임설명 tab openable but not navigable, and
        /// the failure would look like "the codex closes randomly".
        /// </summary>
        bool PointOverCodexTab(Vector2 screenPoint)
        {
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            if (Hit(_codexTabStats) || Hit(_codexTabGuidance)) return true;
            for (var i = 0; i < _codexGroupChips.Count; i++)
                if (Hit(_codexGroupChips[i])) return true;
            return false;

            bool Hit(GameObject tab) =>
                tab != null && tab.activeInHierarchy && RectTransformUtility
                    .RectangleContainsScreenPoint((RectTransform)tab.transform, screenPoint, camera);
        }

        // ------------------------------------------------------ test seams --
        internal void OpenCodexForTest() => OpenCodex();
        internal bool CodexOpenForTest => _codexOpen;
        internal bool CodexShowingGuidanceForTest => _codexShowingGuidance;
        internal RectTransform CodexRectForTest => _codexPanelRect;
        internal RectTransform CodexButtonRectForTest =>
            _codexButton == null ? null : (RectTransform)_codexButton.transform;
        internal void ShowCodexTabForTest(bool guidance) => ShowCodexTab(guidance);
        internal string CodexStatValueForTest(int i) =>
            _codexStatValues[i] == null ? null : _codexStatValues[i].text;
        internal string CodexStatBreakdownForTest(int i) =>
            _codexStatBreakdowns[i] == null ? null : _codexStatBreakdowns[i].text;
        internal IReadOnlyList<Text> CodexGuidanceBodiesForTest => _codexGuidanceBodies;
        internal IReadOnlyList<int> CodexGuidanceBitsForTest => _codexGuidanceBits;
        internal IReadOnlyList<Text> CodexGuidanceTitlesForTest => _codexGuidanceTitles;
        internal IReadOnlyList<int> CodexGuidanceRowGroupForTest => _codexGuidanceRowGroup;
        internal IReadOnlyList<GameObject> CodexGroupChipsForTest => _codexGroupChips;
        internal int CodexOpenGroupForTest => _codexOpenGroup;
        internal void ShowCodexGroupForTest(int group) => ShowCodexGroup(group);
        internal string CodexChipCountForTest(int g) =>
            _codexGroupChipCounts[g] == null ? null : _codexGroupChipCounts[g].text;
        /// <summary>Body area a row must stay inside, in tab-local units.
        /// Exposed so a test asserts against the SAME number the layout used
        /// rather than a second copy that can drift (§4i).</summary>
        internal static float CodexBodyTopForTest => CodexBodyTop;
        internal static float CodexBodyHeightForTest => CodexBodyH;
        internal static float CodexChipSideForTest => CodexChipSide;
        /// <summary>Floor a body rect is never shorter than. Exposed so a test
        /// can assert the row-height contract (height EQUALS the measurement,
        /// clamped at this floor) without retyping the number (§4i).</summary>
        internal static float CodexMinRowHeightForTest => CodexLineH;

        /// <summary>
        /// Latches the stat values on the first Sync after opening.
        ///
        /// It has to be a latch rather than a per-frame read, and the reason is
        /// subtle: at timeScale 0 the sim cannot tick, so a per-frame read
        /// would be ACCIDENTALLY correct and no test could tell the two apart.
        /// Latching makes "frozen" a property something can fail.
        /// </summary>
        internal void SyncCodex(ISimSnapshot sim)
        {
            if (!_codexOpen || _codexLatched || sim == null) return;
            if (!(sim is IDerivedStatSnapshot derived)) return;
            var growth = sim as IGrowthChoiceSnapshot;
            _codexStats = new CodexStats
            {
                Damage = derived.PlayerDamage,
                MaxHealth = derived.PlayerMaxHealth,
                Speed = derived.PlayerSpeed,
                Regen = derived.LanternRegenPerSecond,
                BaseDamage = derived.BaseDamage,
                BaseMaxHealth = derived.BaseMaxHealth,
                BaseSpeed = derived.BaseSpeed,
                BaseRegen = derived.BaseLanternRegen,
                ExtractionBonus = derived.ExtractionBonus,
                Level = sim is IHackSnapshot hack ? hack.Level : 1,
                GrowthAttack = growth != null ? growth.GrowthAttack : 0,
                GrowthVitality = growth != null ? growth.GrowthVitality : 0,
                GrowthSwiftness = growth != null ? growth.GrowthSwiftness : 0,
                MetaAttack = derived.MetaAttack,
                MetaVitality = derived.MetaVitality,
                MetaSwiftness = derived.MetaSwiftness,
                WeaponRank = derived.WeaponRank,
                LanternRank = derived.LanternRank,
                CloakRank = derived.CloakRank,
            };
            _codexLatched = true;
            RefreshCodexStats();
        }

        // ------------------------------------------------------ stats copy --
        static readonly StringBuilder CodexBuilder = new StringBuilder(128);
        // Five: the damage row is 특성 x 무기 x 레벨 x 추출 x 성장, the longest chain.
        readonly string[] _codexLabels = new string[5];
        readonly float[] _codexAmounts = new float[5];
        readonly bool[] _codexIsMul = new bool[5];

        /// <summary>
        /// One breakdown line. <paramref name="baseValue"/> and the factors
        /// only EXPLAIN the sim's number — this never recomputes it.
        ///
        /// The operator is PER FACTOR, not per row, because oil regen mixes
        /// them: the lantern rank multiplies (HackTypes.cs:284-285) while the
        /// level curve adds (CinderSim.cs:2679). An earlier draft called that
        /// "mixed shape" and folded the rank into the shown base — which made
        /// that one row read `7.6 ← 7.6` and teach nothing. Two operators on a
        /// line is a smaller cost than a line with no content.
        ///
        /// A zero-contribution factor is omitted entirely: at level 1 with no
        /// growth and no extraction the line is the base term alone. `× 1.00`
        /// teaches nothing and costs a line of reading.
        /// </summary>
        string Breakdown(float baseValue, int count)
        {
            CodexBuilder.Length = 0;
            CodexBuilder.Append(baseValue.ToString("0.#"));
            for (var i = 0; i < count; i++)
            {
                var mul = _codexIsMul[i];
                CodexBuilder.Append(mul ? " × " : " + ");
                CodexBuilder.Append(_codexLabels[i]);
                CodexBuilder.Append("(+");
                if (mul)
                {
                    CodexBuilder.Append((_codexAmounts[i] * 100f).ToString("0"));
                    CodexBuilder.Append('%');
                }
                else CodexBuilder.Append(_codexAmounts[i].ToString("0.#"));
                CodexBuilder.Append(')');
            }
            return CodexBuilder.ToString();
        }

        void RefreshCodexStats()
        {
            if (_codexStatValues[0] == null) return;
            var s = _codexStats;
            var levels = s.Level - 1;
            var levelLabel = "레벨" + s.Level;

            // Every row unfolds to the SIM CONSTANT, not to the run-start base.
            //
            // _baseDamage already has meta points and equip ranks folded in
            // (CinderSim.cs:2631), so a breakdown that started there would read
            // "72.8 comes from 72.8" on a fresh run — true, and useless. The
            // player spent those points; the line owes them the term. Measured
            // in the browser before this was fixed: all four rows printed their
            // own value twice.
            //
            // Coefficients are read from the constant they describe, never
            // retyped, so a balance change moves the lesson with it (§4j).

            // 공격력 — multiplicative all the way down (SimTypes.cs:177,
            // HackTypes.cs:270-272, CinderSim.cs:2673-2676).
            var n = 0;
            n = Mul(n, "특성" + s.MetaAttack, HackSpec.AttackPerPoint * s.MetaAttack);
            n = Mul(n, "무기" + s.WeaponRank, CampaignSpec.WeaponDamagePerRank * s.WeaponRank);
            n = Mul(n, levelLabel, HackSpec.LevelDamageBonus * levels);
            n = Mul(n, "추출", s.ExtractionBonus);
            n = Mul(n, "성장" + s.GrowthAttack, HackSpec.GrowthAttackBonus * s.GrowthAttack);
            _codexStatValues[0].text = "공격력 " + s.Damage.ToString("0.0");
            _codexStatBreakdowns[0].text = Breakdown(SimConfig.PlayerDamage, n);

            // 최대 체력 — ADDITIVE (SimTypes.cs:175, HackTypes.cs:275-277,
            // CinderSim.cs:2677-2678).
            n = 0;
            n = Add(n, "특성" + s.MetaVitality, HackSpec.VitalityHealthPerPoint * s.MetaVitality);
            n = Add(n, "망토" + s.CloakRank, CampaignSpec.CloakHealthPerRank * s.CloakRank);
            n = Add(n, levelLabel, HackSpec.LevelHealthBonus * levels);
            n = Add(n, "성장" + s.GrowthVitality, HackSpec.GrowthVitalityHealth * s.GrowthVitality);
            _codexStatValues[1].text = "최대 체력 " + s.MaxHealth.ToString("0");
            _codexStatBreakdowns[1].text = Breakdown(SimConfig.PlayerMaxHealth, n);

            // 이동 — multiplicative (SimTypes.cs:176, HackTypes.cs:280-281,
            // CinderSim.cs:2680-2681).
            n = 0;
            n = Mul(n, "특성" + s.MetaSwiftness, HackSpec.SwiftnessSpeedPerPoint * s.MetaSwiftness);
            n = Mul(n, "성장" + s.GrowthSwiftness, HackSpec.GrowthSwiftnessSpeed * s.GrowthSwiftness);
            _codexStatValues[2].text = "이동 " + s.Speed.ToString("0");
            _codexStatBreakdowns[2].text = Breakdown(SimConfig.PlayerSpeed, n);

            // 기름 재생 — MIXED, and the only row that is. The lantern rank
            // multiplies (HackTypes.cs:284-285), the level curve adds
            // (CinderSim.cs:2679). Printed in that order so the line evaluates
            // left to right: 7 × 랜턴1(+8%) + 레벨8(+2.1) = 9.7. An earlier
            // draft folded the rank into the base to keep one operator per row
            // and produced `7.6 ← 7.6` — a line with no content is worse than
            // a line with two operators.
            n = 0;
            n = Mul(n, "랜턴" + s.LanternRank, CampaignSpec.LanternRegenPerRank * s.LanternRank);
            n = Add(n, levelLabel, HackSpec.LevelRegenBonus * levels);
            _codexStatValues[3].text = "기름 재생 " + s.Regen.ToString("0.0");
            _codexStatBreakdowns[3].text = Breakdown(SimConfig.LanternRegenPerSecond, n);
        }

        int Mul(int count, string label, float amount) => Push(count, label, amount, true);
        int Add(int count, string label, float amount) => Push(count, label, amount, false);

        /// <summary>Appends one factor, dropping it when it contributes
        /// nothing. Returns the new count.</summary>
        int Push(int count, string label, float amount, bool multiplicative)
        {
            if (Mathf.Abs(amount) < 0.0005f || count >= _codexLabels.Length) return count;
            _codexLabels[count] = label;
            _codexAmounts[count] = amount;
            _codexIsMul[count] = multiplicative;
            return count + 1;
        }

        // --------------------------------------------------- guidance copy --
        void RefreshCodexGuidance()
        {
            if (_codexGuidanceBodies.Count == 0) return;
            var seenCount = 0;
            for (var i = 0; i < _codexGuidanceBits.Count; i++)
            {
                var bit = _codexGuidanceBits[i];
                var seen = CodexEntrySeen != null && CodexEntrySeen(bit);
                if (seen) seenCount++;
                // Unseen entries keep their title and lose their body. The
                // player learns that something remains without learning what it
                // is: a hazard description for a stage they have not reached is
                // a spoiler; its title alone is not.
                _codexGuidanceBodies[i].text = seen
                    ? GuidanceCatalog.Entries[bit].BodyFor(_touchActive)
                    : "잠김";
                _codexGuidanceBodies[i].color = seen
                    ? new Color(0.72f, 0.76f, 0.86f)
                    : new Color(0.42f, 0.45f, 0.58f);
            }
            if (_codexHeader != null)
                _codexHeader.text = seenCount + " / " + GuidanceCatalog.Count;
            RefreshCodexChipCounts();
            LayoutCodexGuidance();
        }

        /// <summary>Per-group `seen/total` on each chip. Progress stays visible
        /// for the four groups that are currently folded away.</summary>
        void RefreshCodexChipCounts()
        {
            for (var g = 0; g < _codexGroupChipCounts.Count; g++)
            {
                var entries = GuidanceCatalog.ByGroup(GuidanceCatalog.GroupOrder[g]);
                var seen = 0;
                for (var i = 0; i < entries.Length; i++)
                    if (CodexEntrySeen != null && CodexEntrySeen(entries[i].Bit)) seen++;
                _codexGroupChipCounts[g].text = seen + "/" + entries.Length;
            }
        }

        /// <summary>
        /// Places every row from its MEASURED wrapped height.
        ///
        /// This runs after the text is set, and it has to: the same row is
        /// "잠김" when locked and up to three wrapped lines when not, so a
        /// height fixed at build time is wrong for one of those two states. The
        /// old layout used a static 26 u pitch against a 14 u body rect and an
        /// Overflow wrap mode, which is how 분출구 came to render 128 u past its
        /// own column and land on top of 이동 (1,459 u² measured).
        ///
        /// Every group is laid out, not just the open one, so a hidden row still
        /// holds a real position inside the panel — the containment test then
        /// means something for all 23 rows instead of the 6 on screen.
        /// </summary>
        void LayoutCodexGuidance()
        {
            if (_codexGuidanceBodies.Count == 0) return;
            var colW = (CodexW - 56f - 16f * (CodexBodyColumns - 1)) / CodexBodyColumns;
            var y = new float[CodexBodyColumns];
            var lastGroup = -1;

            for (var i = 0; i < _codexGuidanceBodies.Count; i++)
            {
                var group = _codexGuidanceRowGroup[i];
                if (group != lastGroup)
                {
                    for (var c = 0; c < CodexBodyColumns; c++) y[c] = CodexBodyTop;
                    lastGroup = group;
                }
                var col = 0;
                for (var c = 1; c < CodexBodyColumns; c++) if (y[c] > y[col]) col = c;
                var x = col * (colW + 16f);

                var title = _codexGuidanceTitles[i];
                var body = _codexGuidanceBodies[i];
                title.rectTransform.anchoredPosition = new Vector2(x + 8f, y[col]);
                title.rectTransform.sizeDelta = new Vector2(colW - 8f, CodexTitleH);

                // Width first, THEN preferredHeight: uGUI measures the wrap
                // against the rect it currently has, so asking for the height
                // before the width is set reads the wrap of the old width.
                //
                // The measurement IS the height. An earlier draft divided it by
                // a line-height constant and re-multiplied, which happened to
                // agree — uGUI returns 11.0 for one line and 21.0 for two, so
                // the real line step is 10 plus 1 of padding, and a constant of
                // 11 only survived because the ceiling rounded the error away.
                // Two sources for one fact, agreeing by luck (§4i). Now there is
                // one source, and it is the thing being measured.
                var bodyRect = body.rectTransform;
                bodyRect.sizeDelta = new Vector2(colW - 8f, CodexLineH);
                var h = Mathf.Max(CodexLineH, body.preferredHeight);
                bodyRect.anchoredPosition = new Vector2(x + 8f, y[col] - CodexTitleH);
                bodyRect.sizeDelta = new Vector2(colW - 8f, h);

                y[col] -= CodexTitleH + h + CodexRowGap;
            }
        }

        /// <summary>Opens one group and folds the other four. Exactly one is
        /// open — that constraint is what keeps the body inside its budget.</summary>
        void ShowCodexGroup(int group)
        {
            _codexOpenGroup = Mathf.Clamp(group, 0, CodexGroupCount - 1);
            for (var g = 0; g < _codexGroupPages.Count; g++)
                _codexGroupPages[g].SetActive(g == _codexOpenGroup);
            for (var g = 0; g < _codexGroupChips.Count; g++)
                SetCodexChipActive(g, g == _codexOpenGroup);
        }

        void ShowCodexTab(bool guidance)
        {
            _codexShowingGuidance = guidance;
            if (_codexStatsTab != null) _codexStatsTab.SetActive(!guidance);
            if (_codexGuidanceTab != null) _codexGuidanceTab.SetActive(guidance);
        }

        // -------------------------------------------------------- builder --
        /// <summary>
        /// Lazy: nothing here costs anything until a player opens the codex.
        /// The guidance tab alone is 23 rows and most runs never open it.
        /// </summary>
        void EnsureCodexPanel()
        {
            if (_codexPanel != null || _safeRoot == null) return;
            var root = (Transform)_safeRoot;

            _codexPanel = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(CodexW, CodexH),
                new Color(0.02f, 0.04f, 0.07f, 0.96f));
            // Modal backdrop, same reasoning as the guidance card: while this
            // holds the run, taps must not reach the combat HUD underneath.
            _codexPanel.GetComponent<Image>().raycastTarget = true;
            _codexPanelRect = _codexPanel.GetComponent<RectTransform>();

            // Rows against the 440 u body, top-down. Label() measures y DOWN
            // from the parent's top-left, so a row at y spans [440+y-h, 440+y].
            var kicker = Label(_codexPanel.transform, 28, -16, 240, 18,
                "CODEX", 11, TextAnchor.MiddleLeft);
            // Spectral cyan (#2CADD6) at 0.85 — the worldview's memory token,
            // the same one the guidance card and the lobby eyebrows use.
            kicker.color = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f, 0.85f);

            _codexHeader = Label(_codexPanel.transform, CodexW - 268, -16, 240, 18,
                "", 11, TextAnchor.MiddleRight);
            _codexHeader.color = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f, 0.85f);

            _codexTabStats = TextButton(_codexPanel.transform, new Vector2(0f, 1f),
                new Vector2(28, -44), new Vector2(130, 40), "내 수치", 16,
                () => ShowCodexTab(guidance: false));
            _codexTabStats.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            _codexTabGuidance = TextButton(_codexPanel.transform, new Vector2(0f, 1f),
                new Vector2(166, -44), new Vector2(130, 40), "게임설명", 16,
                () => ShowCodexTab(guidance: true));
            _codexTabGuidance.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

            BuildCodexStatsTab();
            BuildCodexGuidanceTab();

            var hint = Label(_codexPanel.transform, 28, -CodexH + 34, CodexW - 56, 20,
                "아무 키나 눌러 닫기", 13, TextAnchor.MiddleCenter);
            hint.color = new Color(0.62f, 0.66f, 0.8f);

            _codexPanel.SetActive(false);
        }

        void BuildCodexStatsTab()
        {
            _codexStatsTab = Panel(_codexPanel.transform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(28, -92),
                new Vector2(CodexW - 56, 300), new Color(0f, 0f, 0f, 0f));
            _codexStatsTab.GetComponent<Image>().raycastTarget = false;
            var tabRect = _codexStatsTab.GetComponent<RectTransform>();
            tabRect.pivot = new Vector2(0f, 1f);

            // Four rows of 72 u: icon 44, value line, breakdown beneath. The
            // breakdown answers "why is it that number", so it is subordinate
            // typography rather than a second headline.
            var icons = new[] { "stat-attack", "stat-vitality", "stat-swiftness", "equip-lantern" };
            for (var i = 0; i < 4; i++)
            {
                var y = -i * 72f;
                var iconObject = new GameObject("CodexIcon" + i);
                iconObject.transform.SetParent(_codexStatsTab.transform, false);
                var image = iconObject.AddComponent<Image>();
                var sprite = IconSprites.Load(icons[i]);
                // A missing sprite renders as a white quad; disabling the Image
                // is the honest fallback.
                if (sprite != null) image.sprite = sprite;
                else image.enabled = false;
                image.raycastTarget = false;
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.sizeDelta = new Vector2(44, 44);
                iconRect.anchoredPosition = new Vector2(0, y - 4);

                _codexStatValues[i] = Label(_codexStatsTab.transform, 58, y,
                    CodexW - 116, 26, "", 19, TextAnchor.MiddleLeft);
                _codexStatValues[i].color = new Color(0.94f, 0.95f, 1f);

                _codexStatBreakdowns[i] = Label(_codexStatsTab.transform, 58, y - 26,
                    CodexW - 116, 24, "", 13, TextAnchor.MiddleLeft);
                _codexStatBreakdowns[i].color = new Color(0.62f, 0.66f, 0.8f);
            }
        }

        void BuildCodexGuidanceTab()
        {
            _codexGuidanceTab = Panel(_codexPanel.transform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(28, -92),
                new Vector2(CodexW - 56, 300), new Color(0f, 0f, 0f, 0f));
            _codexGuidanceTab.GetComponent<Image>().raycastTarget = false;
            var tabRect = _codexGuidanceTab.GetComponent<RectTransform>();
            tabRect.pivot = new Vector2(0f, 1f);

            // AMENDMENT #10 — five chips, one open group.
            //
            // The previous layout put all 23 entries on screen at once in three
            // columns at a fixed 26 u pitch, with bodies inheriting Label()'s
            // Overflow wrap mode. 분출구 rendered 128 u past its column onto 이동
            // (104 x 14 = 1,459 u², reproduced analytically before the fix).
            //
            // Turning wrap on alone does not close it: 분출구 needs two lines
            // (22 u) against a 14 u rect and a 26 u pitch, so the horizontal
            // overlap becomes a 9 u vertical one against the next title. Once
            // rows are as tall as their text, no column count fits — 2 needs
            // 508 u, 3 needs 386 u, 4 needs 380 u, against a 300 u body.
            //
            // So the tab pages. Worst group (조작, 9 entries) is 155 u against
            // the 201.8 u that survives the chip row.
            //
            // Still not a ScrollRect, for the original reason: any tap outside a
            // tab dismisses the codex, so a drag surface inside it would close
            // the panel it is trying to scroll. Chips are exempted the same way
            // the tab buttons are (PointOverCodexTab).
            var chipPitch = (CodexW - 56f - CodexChipSide) / (CodexGroupCount - 1);
            for (var g = 0; g < CodexGroupCount; g++)
            {
                var group = GuidanceCatalog.GroupOrder[g];
                var index = g;   // captured per iteration, not the loop variable
                var chip = TextButton(_codexGuidanceTab.transform, new Vector2(0f, 1f),
                    new Vector2(g * chipPitch, 0f),
                    new Vector2(CodexChipSide, CodexChipSide),
                    string.Empty, 12, () => ShowCodexGroup(index));
                chip.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

                // Icon + name + count, stacked. The icons are the ones the
                // catalog already assigns to each group — no new art, and the
                // same glyph the lobby uses for that hazard family.
                CodexChipIcon(chip.transform, GuidanceCatalog.GroupIcon(group));
                var name = Label(chip.transform, 0f, -46f, CodexChipSide, 16f,
                    GuidanceCatalog.GroupTitle(group), 13, TextAnchor.MiddleCenter);
                name.color = new Color(0.94f, 0.95f, 1f);
                var count = Label(chip.transform, 0f, -64f, CodexChipSide, 14f,
                    string.Empty, 11, TextAnchor.MiddleCenter);
                count.color = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f);

                _codexGroupChips.Add(chip);
                _codexGroupChipCounts.Add(count);
            }

            // One page per group. Rows are created here and POSITIONED later by
            // LayoutCodexGuidance, which can only run once the text is known.
            for (var g = 0; g < CodexGroupCount; g++)
            {
                var entries = GuidanceCatalog.ByGroup(GuidanceCatalog.GroupOrder[g]);
                var page = Panel(_codexGuidanceTab.transform, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), Vector2.zero,
                    new Vector2(CodexW - 56f, 300f), new Color(0f, 0f, 0f, 0f));
                page.GetComponent<Image>().raycastTarget = false;
                page.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
                _codexGroupPages.Add(page);

                for (var i = 0; i < entries.Length; i++)
                {
                    var title = Label(page.transform, 0f, 0f, 100f, CodexTitleH,
                        entries[i].Title, 11, TextAnchor.MiddleLeft);
                    title.color = new Color(0.88f, 0.9f, 0.96f);
                    var body = Label(page.transform, 0f, 0f, 100f, CodexLineH,
                        string.Empty, 9, TextAnchor.UpperLeft);
                    // The fix for the reported overlap. Every other multi-line
                    // label in the HUD sets this explicitly (HudView.cs:1090,
                    // :1455); this one inherited Label()'s Overflow default and
                    // was the only body text in the game allowed to run past its
                    // own rect.
                    body.horizontalOverflow = HorizontalWrapMode.Wrap;
                    body.verticalOverflow = VerticalWrapMode.Overflow;

                    _codexGuidanceTitles.Add(title);
                    _codexGuidanceBodies.Add(body);
                    _codexGuidanceBits.Add(entries[i].Bit);
                    _codexGuidanceRowGroup.Add(g);
                }
            }

            // Hazards first, and the catalog says why: "they are the ones that
            // kill a player who does not know them" (GuidanceCatalog.GroupOrder).
            ShowCodexGroup(0);
            _codexGuidanceTab.SetActive(false);
        }

        /// <summary>Chip glyph. Absent sprite disables the Image rather than
        /// letting uGUI draw a white quad (§4k's quieter sibling).</summary>
        void CodexChipIcon(Transform parent, string iconId)
        {
            var iconObject = new GameObject("ChipIcon");
            iconObject.transform.SetParent(parent, false);
            var image = iconObject.AddComponent<Image>();
            var sprite = IconSprites.Load(iconId);
            if (sprite != null) { image.sprite = sprite; image.preserveAspect = true; }
            else image.enabled = false;
            image.raycastTarget = false;
            var rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = new Vector2(0f, -6f);
        }

        /// <summary>
        /// Open chip reads as pressed by SWAPPING THE PLATE, not by tinting it.
        ///
        /// The first version tinted: white for open, 0.62 grey for shut. In the
        /// browser the two were nearly indistinguishable, because tinting
        /// multiplies an already dark navy plate and the ember border carries
        /// most of the read. LobbyView hit this exact wall and wrote the answer
        /// down (LobbyView.cs:1539-1553) — swap the sprite, leave colour alone.
        /// Twelve lobby buttons were excluded from state feedback by that same
        /// art limitation before it was solved there; this is the solution, not
        /// a second attempt at the problem.
        /// </summary>
        void SetCodexChipActive(int index, bool active)
        {
            var image = _codexGroupChips[index].GetComponent<Image>();
            var sprite = Resources.Load<Sprite>(
                active ? "Icons/ui-button-active" : "Icons/ui-button");
            if (sprite == null)
            {
                // No plate art: fall back to the tint so state is still carried.
                image.color = active
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(0.62f, 0.66f, 0.78f, 0.85f);
                return;
            }
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
    }
}
