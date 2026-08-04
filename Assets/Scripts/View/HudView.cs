// Runtime-generated Screen Space Overlay HUD (uGUI). Korean labels preserved
// from the original page. Text updates only on value change (no per-frame
// string allocation).
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed class HudView : MonoBehaviour
    {
        static readonly string[] LoreBeats =
        {
            "잿불 법정은 군단이 그 기름을 용광로로 바꾸기 전까지 성유물고였다.",
            "잿불 군단의 몸은 비어 있다. 그 안에서 타는 것은 훔쳐온 랜턴 기름이다.",
            "당신이 줍는 유물 조각 하나하나가 심연이 지우려 한 이름이다.",
            "파수꾼의 결계는 갑옷이 아니다. 어둠이 읽지 못하도록 봉인된 기억이다.",
            "더 깊은 군단은 이미 타오르며 온다. 보내지기 전에 불붙여진 것이다.",
            "랜턴은 심연을 죽이지 않는다. 다만 심연이 셈을 끝내지 못하게 막을 뿐이다.",
        };

        public InputAdapter Input;
        public AudioDirector Audio;

        Font _font;
        Image _healthFill, _chargeFill;
        Text _healthText, _chargeText, _waveText, _scoreText, _relicText, _enemyText;
        Text _loreText, _finalText;
        Image _novaCooldownOverlay, _wardCooldownOverlay;
        CanvasGroup _novaGroup, _wardGroup;
        GameObject _gameOverPanel;
        Text _muteLabel;

        // --- campaign extensions (primitive-typed; driven by GameView) -------
        GameObject _stageBanner;
        Text _stageBannerText;
        GameObject _equipPanel;
        Text _equipText;
        GameObject _stageClearPanel;
        Text _stageClearText;
        string _campaignStageName;
        int _campaignTotalWaves;
        int _lastEquipHash = -1;

        int _lastHealth = -1, _lastCharge = -1, _lastWave = -1, _lastScore = -1,
            _lastRelics = -1, _lastEnemies = -1;
        float _loreTimer;

        public void Build()
        {
            // Subset OTF (NanumBarunGothic, OFL) — LegacyRuntime.ttf has no
            // Hangul glyphs and WebGL has no OS font fallback.
            _font = Resources.Load<Font>("Fonts/HudKorean");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;   // single authoritative HUD canvas reference
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }

            var root = canvasObject.transform;

            // --- top-left: health + oil -------------------------------------
            var meters = Panel(root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -16), new Vector2(300, 74), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            _healthFill = Bar(meters.transform, 8, -8, 284, 22,
                new Color(0.95f, 0.42f, 0.3f), out _healthText, "체력");
            _chargeFill = Bar(meters.transform, 8, -40, 284, 22,
                new Color(1f, 0.83f, 0.45f), out _chargeText, "기름");

            // --- top-right: wave / score / relics / enemies -------------------
            var stats = Panel(root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -16), new Vector2(240, 108), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            _waveText = Label(stats.transform, 10, -6, 220, 24, "웨이브 1", 18, TextAnchor.MiddleLeft);
            _scoreText = Label(stats.transform, 10, -30, 220, 24, "점수 0", 18, TextAnchor.MiddleLeft);
            _relicText = Label(stats.transform, 10, -54, 220, 24, "유물 0", 18, TextAnchor.MiddleLeft);
            _enemyText = Label(stats.transform, 10, -78, 220, 24, "적 0", 18, TextAnchor.MiddleLeft);

            // --- mute toggle under stats --------------------------------------
            var muteButton = TextButton(root, new Vector2(1, 1), new Vector2(-16, -132),
                new Vector2(240, 34), "소리: 켜짐", 16,
                () => { if (Audio != null) { Audio.ToggleMute(); RefreshMuteLabel(); } });
            _muteLabel = muteButton.GetComponentInChildren<Text>();
            RefreshMuteLabel();

            // --- bottom-center: skill cards ------------------------------------
            var novaCard = SkillCard(root, -95, "Q", "잿불 노바",
                () => { if (Input != null) Input.QueueNova(); },
                out _novaCooldownOverlay, out _novaGroup, "skill-nova");
            var wardCard = SkillCard(root, 95, "E", "랜턴 결계",
                () => { if (Input != null) Input.QueueWard(); },
                out _wardCooldownOverlay, out _wardGroup, "skill-ward");

            // --- lore line above skill cards ------------------------------------
            _loreText = Label(root, 0, 0, 900, 30, "", 17, TextAnchor.MiddleCenter);
            var loreRect = _loreText.rectTransform;
            loreRect.anchorMin = loreRect.anchorMax = new Vector2(0.5f, 0f);
            loreRect.pivot = new Vector2(0.5f, 0f);
            loreRect.anchoredPosition = new Vector2(0, 118);
            _loreText.color = new Color(0.75f, 0.82f, 1f, 0.85f);

            // --- game over panel -------------------------------------------------
            _gameOverPanel = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(460, 220), new Color(0.03f, 0.02f, 0.06f, 0.92f));
            var overTitle = Label(_gameOverPanel.transform, 0, -18, 460, 34, "잿불 법정 함락", 26, TextAnchor.MiddleCenter);
            overTitle.color = new Color(1f, 0.55f, 0.4f);
            _finalText = Label(_gameOverPanel.transform, 0, -70, 460, 60, "", 18, TextAnchor.MiddleCenter);
            TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f), new Vector2(0, 26),
                new Vector2(200, 44), "재점화 (R)", 20,
                () => { if (Input != null) Input.QueueRestart(); });
            _gameOverPanel.SetActive(false);

            // --- touch controls: mobile platforms, plus touch-only devices
            // whose UA hides mobility (iPadOS desktop-mode Safari reports no
            // iPad UA -> isMobilePlatform false). Headless desktop Chrome is
            // excluded because it still reports a Mouse device. ---
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current != null;
            var mouse = UnityEngine.InputSystem.Mouse.current != null;
            if (Application.isMobilePlatform || (touchscreen && !mouse))
                BuildTouchControls(root);

            // Wave 1 fires no WaveStarted event (original rings the cue from
            // wave 2), so seed the opening lore line here.
            _loreText.text = LoreBeats[0];
            _loreTimer = 6f;
        }

        /// <summary>
        /// Single-scene v0.2: panels return to the lobby STATE via this callback
        /// (set by GameDirector). Page navigation is the legacy fallback.
        /// </summary>
        public System.Action OnReturnHome;

        void ReturnHome()
        {
            if (OnReturnHome != null) OnReturnHome();
            else WebGLStorage.Navigate("index.html");
        }

        /// <summary>
        /// Called by GameView once when the run is a campaign stage. Adds the
        /// stage banner, equipment strip, and stage-clear panel; retitles the
        /// game-over panel with a "캠페인으로" back link.
        /// </summary>
        public void EnableCampaignUi(string stageName, int totalWaves)
        {
            _campaignStageName = stageName;
            _campaignTotalWaves = totalWaves;
            var root = _canvas.transform;

            _stageBanner = Panel(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -14), new Vector2(360, 40), new Color(0.05f, 0.04f, 0.09f, 0.62f));
            var bannerRect = _stageBanner.GetComponent<RectTransform>();
            bannerRect.pivot = new Vector2(0.5f, 1f);
            _stageBannerText = Label(_stageBanner.transform, 0, 0, 360, 40, stageName, 17, TextAnchor.MiddleCenter);
            var bannerTextRect = _stageBannerText.rectTransform;
            bannerTextRect.anchorMin = Vector2.zero;
            bannerTextRect.anchorMax = Vector2.one;
            bannerTextRect.sizeDelta = Vector2.zero;
            bannerTextRect.anchoredPosition = Vector2.zero;
            _stageBannerText.color = new Color(1f, 0.83f, 0.45f);

            _equipPanel = Panel(root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16, 16), new Vector2(240, 34), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            var equipRect = _equipPanel.GetComponent<RectTransform>();
            equipRect.pivot = new Vector2(0f, 0f);
            _equipText = Label(_equipPanel.transform, 8, 0, 226, 34, "", 14, TextAnchor.MiddleLeft);
            var equipTextRect = _equipText.rectTransform;
            equipTextRect.anchorMin = Vector2.zero;
            equipTextRect.anchorMax = Vector2.one;
            equipTextRect.sizeDelta = Vector2.zero;
            equipTextRect.anchoredPosition = new Vector2(8, 0);

            _stageClearPanel = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480, 240), new Color(0.02f, 0.05f, 0.06f, 0.94f));
            var clearTitle = Label(_stageClearPanel.transform, 0, -18, 480, 36, "구역 정화", 28, TextAnchor.MiddleCenter);
            clearTitle.color = new Color(0.56f, 0.91f, 1f);
            _stageClearText = Label(_stageClearPanel.transform, 0, -74, 480, 60, "", 18, TextAnchor.MiddleCenter);
            TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(-105, 24),
                new Vector2(190, 44), "캠페인으로", 18, ReturnHome);
            TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(105, 24),
                new Vector2(190, 44), "재강하 (R)", 18,
                () => { if (Input != null) Input.QueueRestart(); });
            _stageClearPanel.SetActive(false);

            // Campaign game-over also offers the way back to the hub.
            TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f), new Vector2(0, 76),
                new Vector2(200, 40), "캠페인으로", 16, ReturnHome);
        }

        /// <summary>Campaign per-frame extras (equipment ranks, banner wave).</summary>
        int _lastBannerHash = -1;
        public void SyncCampaign(int wave, bool bossAlive, int weapon, int lantern, int cloak)
        {
            var bannerHash = wave * 4 + (bossAlive ? 1 : 0);
            if (_stageBannerText != null && bannerHash != _lastBannerHash)
            {
                _lastBannerHash = bannerHash;
                _stageBannerText.text = bossAlive
                    ? $"{_campaignStageName} — 경계 보스"
                    : $"{_campaignStageName} — 웨이브 {Mathf.Min(wave, _campaignTotalWaves)}/{_campaignTotalWaves}";
            }
            var equipHash = weapon * 100 + lantern * 10 + cloak;
            if (_equipText != null && equipHash != _lastEquipHash)
            {
                _lastEquipHash = equipHash;
                _equipText.text = $"무기 {weapon} • 랜턴 {lantern} • 망토 {cloak}";
            }
        }

        public void ShowStageClear(RunDigest digest)
        {
            if (_stageClearPanel == null) return;
            _stageClearText.text = $"점수 {digest.Score:N0} • 처치 {digest.Kills} • 유물 {digest.Relics}";
            _stageClearPanel.SetActive(true);
        }

        // ------------------------------------------------- v0.2 visibility --
        Canvas _canvas;
        GameObject _prologueToast;
        Text _prologueToastText;
        static readonly string[] PrologueSteps =
        {
            "이동 — W A S D 또는 방향키",
            "타격 — Space",
            "기름 게이지를 보라. 초당 +7, 처치당 +6.",
            "웨이브를 비우면 다음 군단이 온다.",
        };

        /// <summary>Whole combat HUD on/off (lobby hides it).</summary>
        public void SetHudVisible(bool visible)
        {
            if (_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvas != null && _canvas.gameObject.activeSelf != visible)
                _canvas.gameObject.SetActive(visible);
            if (!visible)
            {
                if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
                if (_stageClearPanel != null) _stageClearPanel.SetActive(false);
                HidePrologueToast();
            }
        }

        /// <summary>Campaign/dungeon-only surfaces toggle for arena runs.</summary>
        public void SetCampaignSurfacesVisible(bool visible)
        {
            if (_stageBanner != null) _stageBanner.SetActive(visible);
            if (_equipPanel != null) _equipPanel.SetActive(visible);
            if (_dungeonRoot != null) _dungeonRoot.SetActive(visible);
            // Arena's own 2-card row is the inverse (prologue hides both rows).
            SetArenaCardsVisible(!visible && !_prologueMode);
        }

        bool _prologueMode;

        /// <summary>Prologue has NO skills (spec §1): hide both skill rows.</summary>
        public void SetPrologueMode(bool on)
        {
            _prologueMode = on;
            if (on)
            {
                SetArenaCardsVisible(false);
                if (_dungeonRoot != null) _dungeonRoot.SetActive(false);
            }
        }

        void SetArenaCardsVisible(bool visible)
        {
            if (_novaGroup != null) _novaGroup.gameObject.SetActive(visible);
            if (_wardGroup != null) _wardGroup.gameObject.SetActive(visible);
        }

        /// <summary>Prologue tutorial toast (spec §1). step -1 hides.</summary>
        public void ShowPrologueToast(int step)
        {
            if (step < 0 || step >= PrologueSteps.Length) { HidePrologueToast(); return; }
            if (_prologueToast == null)
            {
                var root = _canvas.transform;
                _prologueToast = Panel(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -70), new Vector2(520, 44), new Color(0.02f, 0.05f, 0.06f, 0.85f));
                _prologueToast.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
                _prologueToastText = Label(_prologueToast.transform, 0, 0, 520, 44, "", 17, TextAnchor.MiddleCenter);
                var toastRect = _prologueToastText.rectTransform;
                toastRect.anchorMin = Vector2.zero;
                toastRect.anchorMax = Vector2.one;
                toastRect.sizeDelta = Vector2.zero;
                toastRect.anchoredPosition = Vector2.zero;
                _prologueToastText.color = new Color(0.62f, 0.95f, 0.88f);
            }
            _prologueToast.SetActive(true);
            _prologueToastText.text = PrologueSteps[step];
        }

        public void HidePrologueToast()
        {
            if (_prologueToast != null) _prologueToast.SetActive(false);
        }

        // =================================================== dungeon HUD (v0.2) --
        GameObject _dungeonRoot;
        Image _xpFill;
        Text _levelText;
        Image[] _comboPips;
        Image[] _skillOverlays;         // bolt, pulse, nova(R), ward(F)
        CanvasGroup[] _skillGroups;
        Image _dashOverlay;
        GameObject _bossBar;
        Image _bossFill;
        Text _bossName;
        Text _bossPhasePip;
        Image _extractRing;
        GameObject _extractRoot;
        Text _shieldText;
        int _lastLevel = -1, _lastCombo = -1, _lastBossPhase = -1;
        float _lastXpFraction = -1f, _lastBossFraction = -1f;
        int _lastShield = -1;
        static readonly float[] SkillMaxCooldowns = { 6.5f, 4f, 8f, 12f };
        static readonly float[] SkillCosts = { 25f, 30f, 45f, 30f };

        /// <summary>Dungeon combat HUD (spec §2, §7): XP, combo, 4 skills, dash,
        /// boss bar, extraction channel. Replaces the 2-card arena skill row.</summary>
        public void EnableDungeonUi(string bossDisplayName)
        {
            var root = _canvas.transform;
            _dungeonRoot = new GameObject("DungeonHud");
            _dungeonRoot.transform.SetParent(root, false);
            var stretch = _dungeonRoot.AddComponent<RectTransform>();
            stretch.anchorMin = Vector2.zero;
            stretch.anchorMax = Vector2.one;
            stretch.offsetMin = Vector2.zero;
            stretch.offsetMax = Vector2.zero;
            var dungeonRoot = _dungeonRoot.transform;

            // Hide the arena 2-card row (Q/E) — dungeon uses its own 4+dash row.
            if (_novaGroup != null) _novaGroup.gameObject.SetActive(false);
            if (_wardGroup != null) _wardGroup.gameObject.SetActive(false);

            // --- XP bar (bottom edge) + level ---------------------------------
            var xpBack = Panel(dungeonRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 4), new Vector2(560, 10), new Color(0f, 0f, 0f, 0.6f));
            xpBack.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var xpFillObject = new GameObject("XpFill");
            xpFillObject.transform.SetParent(xpBack.transform, false);
            _xpFill = xpFillObject.AddComponent<Image>();
            _xpFill.color = new Color(0.56f, 0.91f, 1f);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            var xpRect = xpFillObject.GetComponent<RectTransform>();
            xpRect.anchorMin = Vector2.zero;
            xpRect.anchorMax = Vector2.one;
            xpRect.offsetMin = new Vector2(1, 1);
            xpRect.offsetMax = new Vector2(-1, -1);
            _levelText = Label(dungeonRoot, 0, 0, 120, 24, "Lv 1", 15, TextAnchor.MiddleCenter);
            var levelRect = _levelText.rectTransform;
            levelRect.anchorMin = levelRect.anchorMax = new Vector2(0.5f, 0f);
            levelRect.pivot = new Vector2(0.5f, 0f);
            levelRect.anchoredPosition = new Vector2(-360, 4);
            _levelText.color = new Color(0.56f, 0.91f, 1f);

            // --- combo pips (left of skill row) --------------------------------
            _comboPips = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var pip = Panel(dungeonRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-286 + i * 26, 52), new Vector2(20, 20),
                    new Color(1f, 1f, 1f, 0.14f));
                pip.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
                _comboPips[i] = pip.GetComponent<Image>();
            }

            // --- skill row: dash + Q/E/R/F --------------------------------------
            _skillOverlays = new Image[4];
            _skillGroups = new CanvasGroup[4];
            var dashCard = SkillCard(dungeonRoot, -232, "SHIFT", "질주",
                () => { if (Input != null) Input.QueueDash(); },
                out _dashOverlay, out _, "skill-dash");
            dashCard.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 88);
            var labels = new[] { ("Q", "균열 화살"), ("E", "묘지 파동"), ("R", "잿불 노바"), ("F", "공허 방패") };
            var icons = new[] { "skill-bolt", "skill-pulse", "skill-nova", "skill-aegis" };
            var actions = new UnityEngine.Events.UnityAction[]
            {
                () => { if (Input != null) Input.QueueBolt(); },
                () => { if (Input != null) Input.QueuePulse(); },
                () => { if (Input != null) Input.QueueNova(); },
                () => { if (Input != null) Input.QueueWard(); },
            };
            for (var i = 0; i < 4; i++)
            {
                var card = SkillCard(dungeonRoot, -116 + i * 116, labels[i].Item1, labels[i].Item2,
                    actions[i], out _skillOverlays[i], out _skillGroups[i], icons[i]);
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(108, 88);
            }

            // --- shield readout ---------------------------------------------------
            _shieldText = Label(dungeonRoot, 0, 0, 200, 24, "", 15, TextAnchor.MiddleLeft);
            var shieldRect = _shieldText.rectTransform;
            shieldRect.anchorMin = shieldRect.anchorMax = new Vector2(0f, 1f);
            shieldRect.pivot = new Vector2(0f, 1f);
            shieldRect.anchoredPosition = new Vector2(16, -98);
            _shieldText.color = new Color(0.56f, 0.85f, 1f);

            // --- boss bar (top center, hidden until boss) --------------------------
            _bossBar = Panel(dungeonRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -58), new Vector2(520, 46), new Color(0.05f, 0.02f, 0.05f, 0.8f));
            _bossBar.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            _bossName = Label(_bossBar.transform, 10, -2, 400, 20, bossDisplayName, 14, TextAnchor.MiddleLeft);
            _bossName.color = new Color(1f, 0.55f, 0.4f);
            _bossPhasePip = Label(_bossBar.transform, 0, -2, 500, 20, "", 13, TextAnchor.MiddleRight);
            _bossPhasePip.color = new Color(1f, 0.83f, 0.45f);
            var bossBack = Panel(_bossBar.transform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(8, 6), new Vector2(504, 14), new Color(0f, 0f, 0f, 0.65f));
            bossBack.GetComponent<RectTransform>().pivot = Vector2.zero;
            var bossFillObject = new GameObject("BossFill");
            bossFillObject.transform.SetParent(bossBack.transform, false);
            _bossFill = bossFillObject.AddComponent<Image>();
            _bossFill.color = new Color(0.95f, 0.3f, 0.32f);
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            var bossFillRect = bossFillObject.GetComponent<RectTransform>();
            bossFillRect.anchorMin = Vector2.zero;
            bossFillRect.anchorMax = Vector2.one;
            bossFillRect.offsetMin = new Vector2(1, 1);
            bossFillRect.offsetMax = new Vector2(-1, -1);
            _bossBar.SetActive(false);

            // --- extraction channel ring (center-low) -------------------------------
            _extractRoot = Panel(dungeonRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -120), new Vector2(120, 26), new Color(0.02f, 0.05f, 0.06f, 0.8f));
            Label(_extractRoot.transform, 0, 0, 120, 12, "추출", 11, TextAnchor.MiddleCenter);
            var extractBack = Panel(_extractRoot.transform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(6, 4), new Vector2(108, 8), new Color(0f, 0f, 0f, 0.6f));
            extractBack.GetComponent<RectTransform>().pivot = Vector2.zero;
            var extractFillObject = new GameObject("ExtractFill");
            extractFillObject.transform.SetParent(extractBack.transform, false);
            _extractRing = extractFillObject.AddComponent<Image>();
            _extractRing.color = new Color(0.62f, 0.95f, 0.88f);
            _extractRing.type = Image.Type.Filled;
            _extractRing.fillMethod = Image.FillMethod.Horizontal;
            var extractRect = extractFillObject.GetComponent<RectTransform>();
            extractRect.anchorMin = Vector2.zero;
            extractRect.anchorMax = Vector2.one;
            extractRect.offsetMin = new Vector2(1, 1);
            extractRect.offsetMax = new Vector2(-1, -1);
            _extractRoot.SetActive(false);
        }

        /// <summary>Per-frame dungeon sync (IHackSnapshot surface, primitives only).</summary>
        public void SyncDungeon(
            int level, int xp, int xpNext, int comboIndex,
            float dashCooldown, IReadOnlyList<float> skillCooldowns, float shield,
            float extractionProgress, float extractionTarget,
            float bossHp, float bossMaxHp, int bossPhase, float charge)
        {
            if (_dungeonRoot == null) return;

            if (level != _lastLevel)
            {
                _lastLevel = level;
                _levelText.text = $"Lv {level}";
            }
            var xpFraction = xpNext > 0 ? Mathf.Clamp01((float)xp / xpNext) : 1f;
            if (!Mathf.Approximately(xpFraction, _lastXpFraction))
            {
                _lastXpFraction = xpFraction;
                _xpFill.fillAmount = xpFraction;
            }

            if (comboIndex != _lastCombo)
            {
                _lastCombo = comboIndex;
                for (var i = 0; i < 3; i++)
                    _comboPips[i].color = i < comboIndex
                        ? new Color(1f, 0.83f, 0.45f, 0.95f)
                        : new Color(1f, 1f, 1f, 0.14f);
            }

            _dashOverlay.fillAmount = Mathf.Clamp01(dashCooldown / 1.6f);
            if (skillCooldowns != null && skillCooldowns.Count >= 4)
            {
                for (var i = 0; i < 4; i++)
                {
                    _skillOverlays[i].fillAmount = Mathf.Clamp01(skillCooldowns[i] / SkillMaxCooldowns[i]);
                    _skillGroups[i].alpha = charge >= SkillCosts[i] ? 1f : 0.45f;
                }
            }

            var shieldShown = shield > 0f ? Mathf.CeilToInt(shield) : 0;
            if (shieldShown != _lastShield)
            {
                _lastShield = shieldShown;
                _shieldText.text = shieldShown > 0 ? $"방패 {shieldShown}" : "";
            }

            var bossVisible = bossMaxHp > 0f && bossHp > 0f;
            if (_bossBar.activeSelf != bossVisible) _bossBar.SetActive(bossVisible);
            if (bossVisible)
            {
                var bossFraction = Mathf.Clamp01(bossHp / bossMaxHp);
                if (!Mathf.Approximately(bossFraction, _lastBossFraction))
                {
                    _lastBossFraction = bossFraction;
                    _bossFill.fillAmount = bossFraction;
                }
                if (bossPhase != _lastBossPhase)
                {
                    _lastBossPhase = bossPhase;
                    _bossPhasePip.text = bossPhase >= 2 ? "PHASE II" : "PHASE I";
                }
            }

            var channeling = extractionTarget > 0f;
            if (_extractRoot.activeSelf != channeling) _extractRoot.SetActive(channeling);
            if (channeling)
                _extractRing.fillAmount = Mathf.Clamp01(extractionProgress / extractionTarget);
        }

        void RefreshMuteLabel()
        {
            if (_muteLabel != null && Audio != null)
                _muteLabel.text = Audio.Muted ? "소리: 꺼짐" : "소리: 켜짐";
        }

        // ------------------------------------------------------------- factory --
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

        Image Bar(Transform parent, float x, float y, float width, float height,
                  Color fillColor, out Text valueText, string label)
        {
            var back = Panel(parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, y), new Vector2(width, height), new Color(0f, 0f, 0f, 0.55f));
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(back.transform, false);
            var fill = fillObject.AddComponent<Image>();
            fill.color = fillColor;
            var rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            valueText = Label(back.transform, 6, 0, width - 12, height, label, 14, TextAnchor.MiddleLeft);
            valueText.rectTransform.anchoredPosition = new Vector2(6, 0);
            return fill;
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
                              UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = Panel(parent, anchor, anchor, anchored, size,
                new Color(0.16f, 0.13f, 0.24f, 0.9f));
            // 9-slice ember plate (release skin). Falls back to the flat fill
            // when the sprite is absent so the HUD never regresses to quads.
            var plate = Resources.Load<Sprite>("Icons/ui-button");
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
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return buttonObject;
        }

        GameObject SkillCard(Transform parent, float offsetX, string key, string label,
                             UnityEngine.Events.UnityAction onClick,
                             out Image cooldownOverlay, out CanvasGroup group,
                             string iconId = null)
        {
            var card = Panel(parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(offsetX, 18), new Vector2(150, 88), new Color(0.1f, 0.08f, 0.18f, 0.85f));
            var rect = card.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            group = card.AddComponent<CanvasGroup>();
            var button = card.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            // Icon backdrop sits between the panel fill and the text rows so the
            // cooldown overlay (created last, full-stretch) still darkens it.
            if (iconId != null)
            {
                var sprite = Resources.Load<Sprite>("Icons/" + iconId);
                if (sprite != null)   // missing sprite would render a white quad
                {
                    var iconObject = new GameObject("Icon");
                    iconObject.transform.SetParent(card.transform, false);
                    var icon = iconObject.AddComponent<Image>();
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    icon.color = new Color(1f, 1f, 1f, 0.34f);  // art, not signal
                    var iconRect = iconObject.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(0f, -2f);
                    iconRect.sizeDelta = new Vector2(56f, 56f);
                }
            }
            var keyText = Label(card.transform, 0, -6, 150, 26, key, 20, TextAnchor.MiddleCenter);
            keyText.color = new Color(1f, 0.83f, 0.45f);
            Label(card.transform, 0, -34, 150, 24, label, 16, TextAnchor.MiddleCenter);

            var overlayObject = new GameObject("Cooldown");
            overlayObject.transform.SetParent(card.transform, false);
            cooldownOverlay = overlayObject.AddComponent<Image>();
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            cooldownOverlay.fillOrigin = (int)Image.OriginVertical.Top;
            cooldownOverlay.raycastTarget = false;
            var overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            return card;
        }

        void BuildTouchControls(Transform root)
        {
            // Left: D-pad.
            var pad = Panel(root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(24, 24), new Vector2(190, 190), new Color(0f, 0f, 0f, 0.25f));
            TouchButton(pad.transform, new Vector2(65, 128), "▲", state => Input.TouchUp = state);
            TouchButton(pad.transform, new Vector2(65, 8), "▼", state => Input.TouchDown = state);
            TouchButton(pad.transform, new Vector2(6, 68), "◀", state => Input.TouchLeft = state);
            TouchButton(pad.transform, new Vector2(124, 68), "▶", state => Input.TouchRight = state);

            // Right: strike.
            var strike = Panel(root, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-24, 36), new Vector2(110, 110), new Color(0.8f, 0.4f, 0.25f, 0.5f));
            var strikeRect = strike.GetComponent<RectTransform>();
            strikeRect.pivot = new Vector2(1, 0);
            var touch = strike.AddComponent<TouchHold>();
            touch.OnStateChanged = state => { if (state) Input.QueueAttack(); };
            Label(strike.transform, 0, 0, 110, 110, "타격", 20, TextAnchor.MiddleCenter);
        }

        void TouchButton(Transform parent, Vector2 position, string glyph,
                         System.Action<bool> setter)
        {
            var buttonObject = Panel(parent, Vector2.zero, Vector2.zero, position,
                new Vector2(58, 58), new Color(1f, 1f, 1f, 0.14f));
            var touch = buttonObject.AddComponent<TouchHold>();
            touch.OnStateChanged = setter;
            Label(buttonObject.transform, 0, 0, 58, 58, glyph, 22, TextAnchor.MiddleCenter);
        }

        sealed class TouchHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public System.Action<bool> OnStateChanged;
            public void OnPointerDown(PointerEventData _) => OnStateChanged?.Invoke(true);
            public void OnPointerUp(PointerEventData _) => OnStateChanged?.Invoke(false);
            public void OnPointerExit(PointerEventData _) => OnStateChanged?.Invoke(false);
        }

        // ------------------------------------------------------------- sync --
        public void OnEvents(SimEvents events, ISimSnapshot sim)
        {
            if ((events & SimEvents.WaveStarted) != 0)
            {
                _loreText.text = LoreBeats[(sim.Wave - 1) % LoreBeats.Length];
                _loreTimer = 6f;
            }
            if ((events & SimEvents.GameOver) != 0)
            {
                var digest = sim.Digest;
                _finalText.text = $"점수 {digest.Score:N0} • 웨이브 {digest.Wave} • 유물 {digest.Relics} • 처치 {digest.Kills}";
                _gameOverPanel.SetActive(true);
            }
            if ((events & SimEvents.WaveStarted) != 0 && _gameOverPanel.activeSelf)
                _gameOverPanel.SetActive(false);
        }

        public void Sync(ISimSnapshot sim)
        {
            var health = Mathf.CeilToInt(sim.Player.Health);
            if (health != _lastHealth)
            {
                _lastHealth = health;
                _healthFill.fillAmount = sim.Player.Health / SimConfig.PlayerMaxHealth;
                _healthText.text = $"체력 {health}";
            }
            var charge = Mathf.FloorToInt(sim.Charge);
            if (charge != _lastCharge)
            {
                _lastCharge = charge;
                _chargeFill.fillAmount = sim.Charge / SimConfig.LanternMax;
                _chargeText.text = $"기름 {charge}";
            }
            if (sim.Wave != _lastWave)
            {
                _lastWave = sim.Wave;
                _waveText.text = $"웨이브 {sim.Wave}";
            }
            if (sim.Score != _lastScore)
            {
                _lastScore = sim.Score;
                _scoreText.text = $"점수 {sim.Score:N0}";
            }
            if (sim.Relics != _lastRelics)
            {
                _lastRelics = sim.Relics;
                _relicText.text = $"유물 {sim.Relics}";
            }
            if (sim.LivingEnemies != _lastEnemies)
            {
                _lastEnemies = sim.LivingEnemies;
                _enemyText.text = $"적 {sim.LivingEnemies}";
            }

            SyncSkill(_novaCooldownOverlay, _novaGroup, sim.NovaCooldown,
                SimConfig.NovaCooldown, sim.Charge >= SimConfig.NovaCost);
            SyncSkill(_wardCooldownOverlay, _wardGroup, sim.WardCooldown,
                SimConfig.WardCooldown, sim.Charge >= SimConfig.WardCost);

            if (_loreTimer > 0f)
            {
                _loreTimer -= Time.deltaTime;
                if (_loreTimer <= 0f) _loreText.text = string.Empty;
            }

            if (_gameOverPanel.activeSelf && sim.Mode != SimMode.GameOver)
            {
                _gameOverPanel.SetActive(false);
                // Restart landed on wave 1 again — reseed the opening lore.
                _loreText.text = LoreBeats[(sim.Wave - 1) % LoreBeats.Length];
                _loreTimer = 6f;
            }
        }

        static void SyncSkill(Image overlay, CanvasGroup group, float cooldown,
                              float maxCooldown, bool affordable)
        {
            overlay.fillAmount = cooldown / maxCooldown;
            group.alpha = affordable ? 1f : 0.45f;
        }
    }
}
