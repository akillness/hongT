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
    // partial: the codex half lives in HudViewCodex.cs. `sealed` is retained —
    // this splits one class across two files, it does not open it to subclasses.
    public sealed partial class HudView : MonoBehaviour
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
        public System.Action OnRetryStage;

        Font _font;
        Image _healthFill, _chargeFill;
        Text _healthText, _chargeText, _waveText, _scoreText, _relicText, _enemyText;
        // AMENDMENT #11 UI: the run's difficulty tier is LOCKED at run creation, so
        // unlike every other stat it never changes mid-run — it is latched once by
        // SetRunDifficulty instead of polled in SyncStats.
        Text _difficultyText;
        Sim.Difficulty _runDifficulty;
        bool _difficultyBadgeShown;
        Text _loreText, _finalText, _retryLabel;
        Image _novaCooldownOverlay, _wardCooldownOverlay;
        Image _novaFrame, _wardFrame;   // HUD atlas: ready-state gold rim swap

        // Input depth §3/§5 surfaces, lazily built on first use.
        GameObject _chargeGauge;
        Image _chargeGaugeFill;
        // AMENDMENT #9 momentum gauge, same lazy-build discipline as the charge bar.
        GameObject _momentumGauge;
        Image _momentumGaugeFill;
        Text _momentumTierLabel;
        int _momentumTierShown = -1;

        GameObject _growthPanel;
        Text _growthTitle, _growthOptions;
        CanvasGroup _novaGroup, _wardGroup;
        GameObject _gameOverPanel, _touchJoystickRoot;
        Text _muteLabel;

        // --- campaign extensions (primitive-typed; driven by GameView) -------
        GameObject _stageBanner;
        Text _stageBannerText;
        GameObject _equipPanel;
        Text _equipText;
        GameObject _stageClearPanel;
        Text _stageClearText, _stageClearRetryLabel, _stageClearTitle;
        int _trialClearHits;
        Text _gameOverTitle;
        string _campaignStageName;
        int _campaignTotalWaves;
        int _lastEquipHash = -1;

        // --- Ember Rest: one reusable next-room preparation panel ------------
        GameObject _emberRestBlocker, _emberRestPanel;
        Text _emberRestRoomText, _emberRestDecisionText;
        readonly Image[] _emberRestOfferCards = new Image[3];
        // W15: optional painted backdrop + its readability scrim. Both stay null
        // when the art is absent, and the panel's own flat fill is the fallback.
        Image _emberRestBackdrop, _emberRestScrim;
        readonly PreparationOffer[] _emberRestOffers = new PreparationOffer[3];
        Button _emberRestContinueButton;
        bool _emberRestDecisionMade;
        int _emberRestSelectedIndex = -1;
        public System.Func<int, bool> OnEmberRestOfferSelected;
        public System.Func<bool> OnEmberRestDeferred;
        public System.Action OnEmberRestContinue;


        int _lastHealth = -1, _lastCharge = -1, _lastWave = -1, _lastScore = -1,
            _lastRelics = -1, _lastEnemies = -1;
        float _loreTimer;

        // --- mobile layout (mobile-layout spec #1-#7, #10, #14) ---------------
        // Tier grades the EFFECTIVE canvas width (Screen.width / scaleFactor).
        // Portrait always classifies as Phone: with the portrait match 0.35
        // (spec #1) the effective width lands ~800 u, yet every measured
        // collision (skill row vs joystick, equip-strip burial) still occurs
        // there — the width thresholds only grade landscape windows.
        internal enum LayoutTier { Full, Compact, Phone }

        const float PhoneMaxWidth = 700f;
        const float CompactMaxWidth = 980f;

        /// <summary>Test seam: tier chosen by the last ApplyLayout pass.</summary>
        internal LayoutTier CurrentTier => _tier;
        /// <summary>Test seam: effective canvas width (Screen.width divided by
        /// the scaler factor) computed by the last ApplyLayout pass.</summary>
        internal float LastEffectiveWidth { get; private set; }

        LayoutTier _tier = LayoutTier.Full;
        CanvasScaler _scaler;
        RectTransform _safeRoot;
        int _lastScreenWidth = -1, _lastScreenHeight = -1;
        Rect _lastSafeArea;
        bool _touchActive;
        /// <summary>Which copy variant guidance should use. Read by
        /// GameDirector — a control lesson that names WASD on a phone is worse
        /// than no lesson.</summary>
        public bool TouchActive => _touchActive;
        bool _touchCombatControlsVisible = true;
        float _rotateHintTimer;

        RectTransform _metersRect, _statsRect, _muteRect;
        RectTransform _healthBackRect, _chargeBackRect;
        RectTransform _novaRect, _wardRect;
        RectTransform _equipRect, _shieldRect;
        RectTransform _skillRowRect, _dashCardRect;
        // AMENDMENT #9 (entry 15): the bottom-centre stack is lift-aware. These
        // two used to carry static y values chosen against the DESKTOP row and
        // were buried the moment touch raised the skill row by 120 u — which is
        // the recommended configuration (ShowRotateHintIfPortrait steers to it).
        RectTransform _levelToastRect, _growthPanelRect;
        readonly RectTransform[] _skillCardRects = new RectTransform[4];
        readonly RectTransform[] _comboPipRects = new RectTransform[3];
        RectTransform _xpBackRect;            // §U1 readout-overlap test seam
        RectTransform _strikeRect, _dashTouchRect;

        // --- HUD juice overlays (presentation spec #9/#10/#15/#19/#20) -------
        Image _vignette;            // low-HP pulse + damage punch
        Image _castFlash;           // skill cast tint flash
        float _castFlashTimer;
        float _castFlashPeak;
        Text _waveBanner;
        float _waveBannerTimer;
        float _levelPunchTimer;
        float _xpFlashTimer;
        Text _levelToast;
        float _levelToastTimer;
        static readonly Color XpBaseColor = new Color(0.56f, 0.91f, 1f);
        // --- cycle2 ceremony / accessibility presentation -------------------
        Image _stageClearFlash;
        Text _stageClearBanner;
        float _stageClearTimer;
        int _stageClearFinalScore, _stageClearFinalRelics;
        bool _stageClearPending;
        const float StageClearDuration = 0.5f;
        static readonly Color StageClearColor = new Color(0.17f, 0.68f, 0.84f);
        Image _letterboxTop, _letterboxBottom;
        Text _bossIntroPlate;
        float _bossIntroTimer;
        Text _speakerLine;                     // §캡처5 speaker subtitle
        float _speakerLineTimer;
        bool _bossIntroActive;
        const float BossIntroDuration = 0.45f;
        float _maxHealthSeen = SimConfig.PlayerMaxHealth;
        float _recentHazardTime = -999f;
        bool _bossAliveAtDeath;
        readonly float[] _pipPunchTimers = new float[3];
        float _finisherPipTimer;
        RectTransform _bossBarRect;
        float _bossRevealTimer;
        float _bossPhasePunchTimer;

        // --- loot acquisition toasts -----------------------------------------
        // Non-blocking "what did I just pick up" popups. Parked on the LEFT edge
        // at vertical centre, which is the one band no combat surface claims:
        // the meters/shield/equip column stops at y -164 from the top, the touch
        // joystick catch box tops out at y 260 from the bottom, and every skill
        // card, boss bar, objective chip and wave banner is horizontally centred
        // (x 370..896 at the 1280 u reference). Measured spans are recorded in
        // _workspace/current/engineering/ui-lane3-loot-toast-map-report.md.
        //
        // Timing/stacking lives in LootToastQueue (pure C#); this side is
        // widgets and colour only, rebuilt from the queue's Revision so a
        // steady-state fade writes colours and nothing else.
        const float LootToastLeftX = 16f;
        const float LootToastTopY = 66f;
        const float LootToastPitch = 40f;
        const float LootToastWidth = 230f;
        const float LootToastHeight = 34f;
        /// <summary>Epic rows render slightly larger. Static, so reduced motion
        /// keeps it — it is a size difference, not an animation.</summary>
        const float LootToastEpicScale = 1.07f;

        readonly LootToastQueue _lootToasts = new LootToastQueue();
        RectTransform[] _lootToastRects;
        Image[] _lootToastPlates;
        Image[] _lootToastPips;
        Text[] _lootToastLabels;
        uint _lootToastRevisionShown = uint.MaxValue;

        /// <summary>Row nouns, indexed by <see cref="LootToastKind"/>.</summary>
        static readonly string[] LootToastNames =
            { "잿불 파편", "랜턴 기름", "유물", "장비 파편" };
        /// <summary>Grade adjectives, indexed by <see cref="LootGrade"/>. Basic
        /// is unqualified on purpose: naming the common case adds a word to every
        /// row and steals the contrast the two rare grades need.</summary>
        static readonly string[] LootGradeNames = { "", "정교한", "전설의" };
        static readonly Color[] LootGradeColors =
        {
            new Color(0.82f, 0.86f, 0.95f),   // Basic — HUD ink, calm
            new Color(0.17f, 0.68f, 0.84f),   // Fine  — cyan, the clear/ward token
            new Color(1f, 0.83f, 0.45f),      // Epic  — gold, the relic token
        };

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
            scaler.matchWidthOrHeight = 0.5f;   // orientation-driven, see SyncLayout
            _scaler = scaler;
            canvasObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }

            // Spec #10: every HUD surface hangs off a safe-area anchored child
            // so notch insets shift the whole overlay (template ships
            // viewport-fit=cover; no-op while safeArea == full screen).
            // --- full-screen juice overlays (BEHIND the safe-area UI) --------
            // Radial gradient generated once; center transparent -> edge alpha.
            var radial = MakeRadialTexture();
            _vignette = Overlay(canvasObject.transform, radial, "Vignette");
            _castFlash = Overlay(canvasObject.transform, radial, "CastFlash");
            _stageClearFlash = Overlay(canvasObject.transform, radial, "StageClearFlash");
            _stageClearFlash.type = Image.Type.Filled;
            _stageClearFlash.fillMethod = Image.FillMethod.Radial360;
            _stageClearFlash.fillOrigin = (int)Image.Origin360.Top;
            _stageClearFlash.fillClockwise = true;
            _stageClearFlash.fillAmount = 0f;

            var safeObject = new GameObject("SafeArea");
            safeObject.transform.SetParent(canvasObject.transform, false);
            _safeRoot = safeObject.AddComponent<RectTransform>();
            _safeRoot.anchorMin = Vector2.zero;
            _safeRoot.anchorMax = Vector2.one;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
            var root = safeObject.transform;
            // Letterbox sits above the regular HUD, but never intercepts input.
            _letterboxTop = Letterbox(canvasObject.transform, true);
            _letterboxBottom = Letterbox(canvasObject.transform, false);
            _bossIntroPlate = Label(canvasObject.transform, 0, -90, 600, 36, "", 22, TextAnchor.MiddleCenter);
            var bossIntroRect = _bossIntroPlate.rectTransform;
            bossIntroRect.anchorMin = bossIntroRect.anchorMax = new Vector2(0.5f, 0.5f);
            bossIntroRect.pivot = new Vector2(0.5f, 0.5f);
            bossIntroRect.anchoredPosition = Vector2.zero;
            _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            _bossIntroPlate.fontStyle = FontStyle.Bold;

            // §캡처5: speaker-prefixed bottom-center subtitle for boss beats.
            // The world-space speech bubble stays the combat grammar; this
            // line only doubles the boss intro/phase text at the screen edge.
            _speakerLine = Label(root, 0, 0, 900, 30, "", 18, TextAnchor.MiddleCenter);
            var speakerRect = _speakerLine.rectTransform;
            speakerRect.anchorMin = speakerRect.anchorMax = new Vector2(0.5f, 0f);
            speakerRect.pivot = new Vector2(0.5f, 0f);
            speakerRect.anchoredPosition = new Vector2(0f, 128f);
            _speakerLine.color = new Color(0.92f, 0.88f, 0.8f, 0f);

            _stageClearBanner = Label(root, 0, 0, 560, 84, "", 28, TextAnchor.MiddleCenter);
            var clearBannerRect = _stageClearBanner.rectTransform;
            clearBannerRect.anchorMin = clearBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            clearBannerRect.pivot = new Vector2(0.5f, 0.5f);
            clearBannerRect.anchoredPosition = Vector2.zero;
            _stageClearBanner.color = new Color(StageClearColor.r, StageClearColor.g, StageClearColor.b, 0f);
            _stageClearBanner.fontStyle = FontStyle.Bold;

            // --- top-left: health + oil -------------------------------------
            var meters = Panel(root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -16), new Vector2(300, 74), new Color(0.05f, 0.04f, 0.09f, 0.55f),
                "hud-meters-panel-bg");
            _metersRect = meters.GetComponent<RectTransform>();
            _healthFill = Bar(meters.transform, 8, -8, 284, 22,
                new Color(0.95f, 0.42f, 0.3f), out _healthText, "체력",
                "hud-hp-bar-frame", "hud-hp-bar-fill");
            _chargeFill = Bar(meters.transform, 8, -40, 284, 22,
                new Color(1f, 0.83f, 0.45f), out _chargeText, "기름",
                "hud-oil-bar-frame", "hud-oil-bar-fill");
            _healthBackRect = (RectTransform)_healthFill.transform.parent;
            _chargeBackRect = (RectTransform)_chargeFill.transform.parent;

            // --- top-right: wave / score / relics / enemies / difficulty ------
            var stats = Panel(root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -16), new Vector2(240, StatsPanelHeight),
                new Color(0.05f, 0.04f, 0.09f, 0.55f),
                "hud-stats-panel-bg");
            _statsRect = stats.GetComponent<RectTransform>();

            _waveText = Label(stats.transform, 10, -6, 220, 24, "웨이브 1", 18, TextAnchor.MiddleLeft);
            _scoreText = Label(stats.transform, 10, -30, 220, 24, "점수 0", 18, TextAnchor.MiddleLeft);
            _relicText = Label(stats.transform, 10, -54, 220, 24, "유물 0", 18, TextAnchor.MiddleLeft);
            _enemyText = Label(stats.transform, 10, -78, 220, 24, "적 0", 18, TextAnchor.MiddleLeft);
            // Row 5 exists from Build so no run-start allocation is needed, but it
            // stays hidden until a run latches a non-baseline tier.
            _difficultyText = Label(stats.transform, 10, -102, 220, 24, "", 18, TextAnchor.MiddleLeft);
            _difficultyText.gameObject.SetActive(false);

            // --- mute toggle under stats --------------------------------------
            var muteButton = TextButton(root, new Vector2(1, 1),
                new Vector2(-16, -MuteTopOffset),
                new Vector2(240, 34), "소리: 켜짐", 16,
                () => { if (Audio != null) { Audio.ToggleMute(); RefreshMuteLabel(); } });
            _muteLabel = muteButton.GetComponentInChildren<Text>();
            _muteRect = muteButton.GetComponent<RectTransform>();
            RefreshMuteLabel();

            // --- bottom-center: skill cards ------------------------------------
            var novaCard = SkillCard(root, -95, "Q", "잿불 노바",
                () => { if (Input != null) Input.QueueNova(); },
                out _novaCooldownOverlay, out _novaGroup, out _novaFrame, "skill-nova");
            var wardCard = SkillCard(root, 95, "E", "랜턴 결계",
                () => { if (Input != null) Input.QueueWard(); },
                out _wardCooldownOverlay, out _wardGroup, out _wardFrame, "skill-ward");

            _novaRect = novaCard.GetComponent<RectTransform>();
            _wardRect = wardCard.GetComponent<RectTransform>();

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
            // Modal backdrop keeps raycast ON PURPOSE: while the panel is up,
            // taps must not leak through to the combat HUD beneath it.
            _gameOverPanel.GetComponent<Image>().raycastTarget = true;
            var overTitle = Label(_gameOverPanel.transform, 0, -18, 460, 34, "잿불 법정 함락", 26, TextAnchor.MiddleCenter);
            _gameOverTitle = overTitle;
            overTitle.color = new Color(1f, 0.55f, 0.4f);
            _finalText = Label(_gameOverPanel.transform, 0, -70, 460, 60, "", 18, TextAnchor.MiddleCenter);
            // AMENDMENT #9 (D2): the retry button moves off centre to make room
            // for the campaign link beside it. EnableCampaignUi used to append
            // that link at y=76, which landed [-34,6] — 26 u inside the death
            // text at [-20,40]. It was clickable the whole time, which is why
            // the report was "I died and STILL cannot leave": the way out was
            // drawn underneath the sentence explaining the death.
            //
            // Both buttons live here, in ONE function, on the retry row — the
            // same two-across layout the stage-clear panel uses.
            //
            // The back link used to be appended by EnableCampaignUi, which runs
            // only for dungeon presentation. That had two costs, and a user hit
            // both: in a dungeon the late-added button landed 26 u inside the
            // death-cause text (D2), and in the PROLOGUE it was never added at
            // all — dying there left one button, 재강하, and no way back to the
            // lobby. Building it here fixes the second by construction and the
            // first by the single-builder rule.
            var backButton = TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f),
                new Vector2(-105, 26), new Vector2(200, 44), "캠페인으로", 18, ReturnHome);
            backButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var retryButton = TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f),
                new Vector2(105, 26), new Vector2(200, 44), "재강하 (R)", 20, RetryRun);
            retryButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            _retryLabel = retryButton.GetComponentInChildren<Text>();
            _gameOverPanel.SetActive(false);

            BuildEmberRestPanel(root);

            // --- wave banner (#20) + level toast (#19), raycast-off ----------
            _waveBanner = Label(root, 0, -140, 600, 60, "", 34, TextAnchor.MiddleCenter);
            var bannerRect = _waveBanner.rectTransform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0, -140);
            _waveBanner.color = new Color(0.95f, 0.35f, 0.17f, 0f);
            _waveBanner.fontStyle = FontStyle.Bold;

            // AMENDMENT #9 (negotiation entry 15): y was a static 170, which put
            // the toast [170,204] entirely inside the growth plate [150,212] —
            // 100% buried, every time. GainXp raises LevelUp and the growth
            // offer in the SAME frame and the 1.4 s toast is fully contained by
            // the 5 s offer, so the two were never NOT overlapping.
            // Both now live on the lift-aware bottom stack (ApplyLayoutTier).
            _levelToast = Label(root, 0, 236, 480, 34, "", 17, TextAnchor.MiddleCenter);
            var toastRect = _levelToast.rectTransform;
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.anchoredPosition = new Vector2(0, 236);
            _levelToastRect = toastRect;
            _levelToast.color = new Color(0.56f, 0.91f, 1f, 0f);

            BuildLootToasts(root);

            // --- touch controls: mobile platforms, plus touch-only devices
            // whose UA hides mobility (iPadOS desktop-mode Safari reports no
            // iPad UA -> isMobilePlatform false). Headless desktop Chrome is
            // excluded because it still reports a Mouse device. ---
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current != null;
            var mouse = UnityEngine.InputSystem.Mouse.current != null;
            if (Application.isMobilePlatform || (touchscreen && !mouse))
                BuildTouchControls(root);

            // Orientation match + layout tier + safe-area (spec #1/#2/#10).
            SyncLayout(true);

            // Wave 1 fires no WaveStarted event (original rings the cue from
            // wave 2), so seed the opening lore line here.
            _loreText.text = LoreBeats[0];
            _loreTimer = 6f;

            // Companion command console: adopt a #gemini= fragment key once
            // (fragments never reach the server — see GeminiCommandClient).
            GeminiCommandClient.AdoptUrlKeyIfPresent();
        }

        // =============================================== mobile layout core --
        void Update()
        {
            if (_scaler == null) return;   // Build() not called yet
            // Cheap dirty-check (two int compares + rect compare); RectTransform
            // writes happen only on actual resolution / safe-area changes.
            SyncLayout(false);

            UpdateCommandConsole();

            // Unscaled: a pickup during hit-stop or the boss slow-mo beat must
            // still fade on wall-clock time, exactly like the console toast.
            SyncLootToasts(Time.unscaledDeltaTime);

            // AMENDMENT #9 — the dismiss poll. MonoBehaviour.Update runs on
            // unscaled wall-clock, so it keeps ticking at timeScale 0; anything
            // driven by Time.deltaTime would not. This is what makes the freeze
            // escapable, and it is the single reason an 8-pause budget is
            // defensible: the survey found the variable is not how often a game
            // stops the player but whether the player can wave it off.
            if (_guidancePaused && AnyDismissInput()) DismissGuidancePause();
            // The codex uses its own poll: a press on a tab button switches
            // tabs rather than closing. Ordered after the guidance card because
            // a card can never be up while the codex is (both freeze the run,
            // and the codex only opens from a live HUD).
            else if (_codexOpen && CodexDismissInput()) CloseCodex();

            if (_rotateHintTimer > 0f)
            {
                _rotateHintTimer -= Time.deltaTime;
                if (_rotateHintTimer <= 0f) HidePrologueToast();
            }
        }

        // ============================================= loot acquisition toasts --

        /// <summary>Builds the toast column once. Every row exists from Build so
        /// a pickup mid-combat allocates no GameObject; rows start inactive and
        /// are shown/hidden by the queue's live count.</summary>
        void BuildLootToasts(Transform root)
        {
            _lootToastRects = new RectTransform[LootToastQueue.Capacity];
            _lootToastPlates = new Image[LootToastQueue.Capacity];
            _lootToastPips = new Image[LootToastQueue.Capacity];
            _lootToastLabels = new Text[LootToastQueue.Capacity];

            for (var i = 0; i < LootToastQueue.Capacity; i++)
            {
                var row = Panel(root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(LootToastLeftX, LootToastTopY - i * LootToastPitch),
                    new Vector2(LootToastWidth, LootToastHeight),
                    new Color(0.03f, 0.04f, 0.08f, 0.72f));
                row.name = "LootToast";
                _lootToastRects[i] = row.GetComponent<RectTransform>();
                _lootToastPlates[i] = row.GetComponent<Image>();

                // Grade pip: a 4 u colour bar on the leading edge. Carries the
                // grade even when the row's text is read at a glance, and stays
                // legible for a player who cannot separate cyan from gold text.
                var pip = Panel(row.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    Vector2.zero, new Vector2(4f, 0f), Color.white);
                var pipRect = pip.GetComponent<RectTransform>();
                pipRect.anchorMin = new Vector2(0f, 0f);
                pipRect.anchorMax = new Vector2(0f, 1f);
                pipRect.pivot = new Vector2(0f, 0.5f);
                pipRect.offsetMin = new Vector2(0f, 0f);
                pipRect.offsetMax = new Vector2(4f, 0f);
                _lootToastPips[i] = pip.GetComponent<Image>();

                var label = Label(row.transform, 14f, 0f,
                    LootToastWidth - 22f, LootToastHeight, "", 15, TextAnchor.MiddleLeft);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                labelRect.offsetMin = new Vector2(14f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);
                _lootToastLabels[i] = label;

                row.SetActive(false);
            }
        }

        /// <summary>Announces one collected pickup. Called by GameView once per
        /// item, with the grade that item actually carried.</summary>
        public void PushLootToast(LootToastKind kind, LootGrade grade)
        {
            if (_lootToastRects == null) return;
            _lootToasts.Push(kind, grade);
            SyncLootToasts(0f);
        }

        /// <summary>Ages the queue and writes the row visuals. Text is rebuilt
        /// only when the queue's Revision moves, so a fade costs colour writes
        /// and nothing else — no per-frame string allocation.</summary>
        void SyncLootToasts(float deltaTime)
        {
            if (_lootToastRects == null) return;
            _lootToasts.Instant = ViewPrefs.ReducedMotion;
            _lootToasts.Tick(deltaTime);

            var rebuildText = _lootToasts.Revision != _lootToastRevisionShown;
            _lootToastRevisionShown = _lootToasts.Revision;

            for (var i = 0; i < LootToastQueue.Capacity; i++)
            {
                var live = i < _lootToasts.Count;
                var row = _lootToastRects[i].gameObject;
                if (row.activeSelf != live) row.SetActive(live);
                if (!live) continue;

                var slot = _lootToasts.SlotAt(i);
                var gradeIndex = (int)slot.Grade;
                if (gradeIndex < 0 || gradeIndex >= LootGradeColors.Length) gradeIndex = 0;
                var tint = LootGradeColors[gradeIndex];

                if (rebuildText)
                {
                    var noun = LootToastNames[(int)slot.Kind];
                    var adjective = LootGradeNames[gradeIndex];
                    var line = adjective.Length == 0 ? noun : adjective + " " + noun;
                    if (slot.Count > 1) line = line + " x" + slot.Count;
                    _lootToastLabels[i].text = line;
                    var scale = slot.Grade == LootGrade.Epic ? LootToastEpicScale : 1f;
                    _lootToastRects[i].localScale = new Vector3(scale, scale, 1f);
                }

                var alpha = _lootToasts.AlphaAt(i);
                _lootToastLabels[i].color = new Color(tint.r, tint.g, tint.b, alpha);
                _lootToastPips[i].color = new Color(tint.r, tint.g, tint.b, alpha);
                _lootToastPlates[i].color = new Color(0.03f, 0.04f, 0.08f, 0.72f * alpha);
            }
        }

        /// <summary>QA/test seam: the line row <paramref name="index"/> is
        /// actually rendering, or "" when that row is not on screen.</summary>
        internal string LootToastReadout(int index)
            => _lootToastRects != null && index >= 0 && index < _lootToasts.Count
                ? _lootToastLabels[index].text
                : string.Empty;

        internal int LootToastCount => _lootToasts.Count;

        /// <summary>Test seam: the rects the toast column is actually occupying
        /// this frame, so the placement claim ("clears every combat surface") is
        /// measured rather than asserted in a comment.</summary>
        internal void CollectActiveLootToastRects(List<RectTransform> into)
        {
            if (_lootToastRects == null) return;
            for (var i = 0; i < _lootToastRects.Length; i++)
                if (_lootToastRects[i] != null && _lootToastRects[i].gameObject.activeSelf)
                    into.Add(_lootToastRects[i]);
        }

        /// <summary>Test seam: the two non-interactive combat readouts the toast
        /// column must never cover. Null while they are not built.</summary>
        internal RectTransform RoomObjectiveRect
            => _roomObjectivePanel == null ? null : (RectTransform)_roomObjectivePanel.transform;
        internal RectTransform BossBarRectForTest => _bossBarRect;

        void SyncLayout(bool force)
        {
            var width = Screen.width;
            var height = Screen.height;
            var safeArea = Screen.safeArea;
            if (!force && width == _lastScreenWidth && height == _lastScreenHeight
                && safeArea == _lastSafeArea)
                return;
            ApplyLayout(width, height, safeArea);
        }

        /// <summary>Layout core with the screen geometry injected. This is the
        /// seam EditMode tests drive directly: Screen.* is read-only and
        /// reports degenerate sizes in batchmode, so SyncLayout stays the
        /// thin Screen-reading caller.</summary>
        internal void ApplyLayout(int width, int height, Rect safeArea)
        {
            _lastScreenWidth = width;
            _lastScreenHeight = height;
            _lastSafeArea = safeArea;

            // Spec #1: portrait relaxes toward width-match so the effective
            // canvas width grows 653 -> ~739 u at 390x844. Full width-match
            // (0) is banned — touch targets would collapse to ~17 CSS px.
            _scaler.matchWidthOrHeight = width < height ? 0.35f : 0.5f;

            // Spec #10: Screen.safeArea insets (WebGL exposes them once the
            // template ships viewport-fit=cover; equal to the full screen
            // elsewhere, making this a no-op). Anchors are in normalized
            // screen space, so the same math serves every scale factor.
            if (width > 0 && height > 0)
            {
                _safeRoot.anchorMin = new Vector2(
                    safeArea.xMin / width, safeArea.yMin / height);
                _safeRoot.anchorMax = new Vector2(
                    safeArea.xMax / width, safeArea.yMax / height);
            }

            ApplyLayoutTier();
        }

        /// <summary>Spec #2/#3/#4: re-anchor HUD panels for the effective
        /// canvas width. Pure RectTransform/fontSize writes — no allocation,
        /// runs only on resolution change.</summary>
        void ApplyLayoutTier()
        {
            // scaleFactor is stale within the frame the resolution changed;
            // derive the effective width from the same formula the scaler
            // uses (log-lerp between width and height match). Geometry comes
            // from the last ApplyLayout pass, never Screen.* directly.
            var width = _lastScreenWidth;
            var height = _lastScreenHeight;
            var match = _scaler.matchWidthOrHeight;
            var logWidth = Mathf.Log(width / 1280f, 2f);
            var logHeight = Mathf.Log(height / 720f, 2f);
            var scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
            var effectiveWidth = width / Mathf.Max(0.0001f, scale);
            LastEffectiveWidth = effectiveWidth;

            // Portrait forces Phone: match 0.35 inflates the effective width
            // to ~800 u at 390x844, but every measured collision still occurs
            // there — width thresholds only grade landscape windows.
            var tier = width < height ? LayoutTier.Phone
                : effectiveWidth < PhoneMaxWidth ? LayoutTier.Phone
                : effectiveWidth < CompactMaxWidth ? LayoutTier.Compact
                : LayoutTier.Full;
            _tier = tier;

            // --- top bars (spec #2) -------------------------------------------
            var compactTop = tier != LayoutTier.Full;
            var metersWidth = compactTop ? 240f : 300f;
            _metersRect.sizeDelta = new Vector2(metersWidth, 74);
            var barWidth = metersWidth - 16f;
            _healthBackRect.sizeDelta = new Vector2(barWidth, 22);
            _chargeBackRect.sizeDelta = new Vector2(barWidth, 22);

            var statsWidth = compactTop ? 200f : 240f;
            var statFont = compactTop ? 15 : 18;
            _waveText.fontSize = statFont;
            _scoreText.fontSize = statFont;
            _relicText.fontSize = statFont;
            _enemyText.fontSize = statFont;
            if (_difficultyText != null) _difficultyText.fontSize = statFont;

            if (tier == LayoutTier.Phone)
            {
                // Stats drop below the meters (left column), freeing the top
                // right for the compact mute icon. Relic/enemy rows stay live
                // (labels remain 4 — merged strings would allocate per wave).
                _statsRect.anchorMin = _statsRect.anchorMax = new Vector2(0f, 1f);
                _statsRect.pivot = new Vector2(0f, 1f);
                _statsRect.anchoredPosition = new Vector2(16, -98);
                _statsRect.sizeDelta = new Vector2(statsWidth, StatsPanelHeight);
                _muteRect.anchoredPosition = new Vector2(-16, -16);
                // Spec #6: 44 CSS px floor at the worst phone scale
                // (0.488 px/u) needs >=90 u — the old 34 u was a 17 px target.
                _muteRect.sizeDelta = new Vector2(120, 92);
                if (_muteLabel != null) _muteLabel.fontSize = 13;
            }
            else
            {
                _statsRect.anchorMin = _statsRect.anchorMax = new Vector2(1f, 1f);
                _statsRect.pivot = new Vector2(1f, 1f);
                _statsRect.anchoredPosition = new Vector2(-16, -16);
                _statsRect.sizeDelta = new Vector2(statsWidth, StatsPanelHeight);
                _muteRect.anchoredPosition = new Vector2(-16, -MuteTopOffset);
                _muteRect.sizeDelta = new Vector2(statsWidth, 34);
                if (_muteLabel != null) _muteLabel.fontSize = 16;
            }

            // --- arena 2-card row (spec #4: 58 u overlap with the joystick) ----
            // Phone + touch shifts the row +63 u right: centered, the nova
            // card's left edge (~228 u on the 799 u worst-case canvas) digs
            // into the 260 u joystick catch box. Height 92 u keeps the cards
            // above the 44 CSS px floor (88 u = 42.9 px at 0.488 px/u).
            var arenaLift = _touchActive ? 120f : 0f;
            var arenaShift = tier == LayoutTier.Phone && _touchActive ? 63f : 0f;
            var arenaCardSize = new Vector2(120, tier == LayoutTier.Phone ? 92f : 76f);
            if (_novaRect != null)
            {
                _novaRect.sizeDelta = arenaCardSize;
                _novaRect.anchoredPosition = new Vector2(-95 + arenaShift, 18 + arenaLift);
            }
            if (_wardRect != null)
            {
                _wardRect.sizeDelta = arenaCardSize;
                _wardRect.anchoredPosition = new Vector2(95 + arenaShift, 18 + arenaLift);
            }
            if (_loreText != null)
            {
                // Phone-tier DUNGEON: the 4-card row occupies y 54..146 and
                // the pips y ~200 — lore at 118 sat straight on the cards
                // (portrait QA finding). Park it above the whole control
                // stack, clear of the speaker line at 232 (span ≈219..245).
                var dungeonPhone = tier == LayoutTier.Phone
                    && _dungeonRoot != null && _dungeonRoot.activeSelf;
                _loreText.rectTransform.anchoredPosition = dungeonPhone
                    ? new Vector2(0, 262 + (_touchActive ? 120f : 0f))
                    : new Vector2(0, 118 + arenaLift);
            }

            ApplyDungeonTier(tier);
            ApplyEquipPlacement();
            // LAST: the stack reads the edges the two calls above just wrote.
            // Ordering is the whole mechanism — placing it earlier would make
            // it assume coordinates instead of measuring them.
            ApplyLeftStackPlacement();
        }

        /// <summary>Spec #3: dungeon skill row per tier. Phone: 4 cards of
        /// 92 u in one raised row + dash card centered beneath (row shifted
        /// right of the joystick catch box when touch is live); the touch
        /// dash/strike buttons own the right edge.</summary>
        void ApplyDungeonTier(LayoutTier tier)
        {
            if (_dungeonRoot == null || _dashCardRect == null) return;
            var lift = _touchActive ? 120f : 0f;
            if (tier == LayoutTier.Phone)
            {
                // Spec #3 phone: dash card bottom band, 4-card row stacked
                // above it. 92 u squares clear the 44 CSS px floor (spec #6:
                // 86x72 was 42/35 px at 0.488 px/u); the +63 u right shift
                // keeps the row's left edge out of the 260 u joystick catch
                // box when touch is live (centered, it dug 32 u into it).
                // Touch lift (+120) clears the strike/dash column vertically.
                var rowShift = _touchActive ? 63f : 0f;
                for (var i = 0; i < 4; i++)
                {
                    _skillCardRects[i].sizeDelta = new Vector2(92, 92);
                    _skillCardRects[i].anchoredPosition =
                        new Vector2(-138 + i * 92 + rowShift, 100 + lift);
                }
                _dashCardRect.sizeDelta = new Vector2(96, 92);
                _dashCardRect.anchoredPosition = new Vector2(rowShift, 4 + lift);
                for (var i = 0; i < 3; i++)
                    _comboPipRects[i].anchoredPosition =
                        new Vector2(-26 + i * 26 + rowShift, 200 + lift);
                // §캡처5: phone stack tops out at pips (200+lift+20) — the
                // speaker line sits above the whole control stack.
                if (_speakerLine != null)
                    _speakerLine.rectTransform.anchoredPosition = new Vector2(0f, 232f + lift);
            }
            else
            {
                for (var i = 0; i < 4; i++)
                {
                    // §U1 compact slots: label row dropped from SkillCard, so
                    // 96x76 keeps icon+keycap legible while shrinking the row
                    // span 574->500 u (user-reported "skill overlay" bulk).
                    _skillCardRects[i].sizeDelta = new Vector2(96, 76);
                    _skillCardRects[i].anchoredPosition = new Vector2(-104 + i * 104, 18 + lift);
                }
                _dashCardRect.sizeDelta = new Vector2(96, 76);
                _dashCardRect.anchoredPosition = new Vector2(-208, 18 + lift);
                // §U1 fix (measured): pips at y=52 sat INSIDE the dash card
                // rect (18..106) — 3 verified collisions. Above the row now.
                for (var i = 0; i < 3; i++)
                    _comboPipRects[i].anchoredPosition = new Vector2(-26 + i * 26, 102 + lift);
                if (_speakerLine != null)
                    _speakerLine.rectTransform.anchoredPosition = new Vector2(0f, 132f + lift);
            }
            ApplyBottomStackPlacement();
            // Left-column stack (below meters -16..-90): phone puts stats at
            // -98..-206, so dungeon shield text drops to -252 (equip strip
            // occupies -214..-248); with touch on wider tiers the equip strip
            // holds -98..-132, shield takes -136.
            if (_shieldRect != null)
                _shieldRect.anchoredPosition = tier == LayoutTier.Phone
                    ? new Vector2(16, -252)
                    : _touchActive ? new Vector2(16, -136) : new Vector2(16, -98);
        }

        /// <summary>
        /// AMENDMENT #9 (negotiation entry 15) — the level-up plate and toast
        /// sit ABOVE the whole bottom-centre control stack, in every tier.
        ///
        /// Both used to carry a static y chosen against one configuration:
        /// the toast at 170 was inside the plate at 150 (100% buried, and
        /// unavoidably so — GainXp raises LevelUp and the growth offer in the
        /// same frame, and the 1.4 s toast is fully contained by the 5 s
        /// offer), and the plate at 150 was measured against the DESKTOP skill
        /// row and lost 81.6% of its height the moment touch lifted that row
        /// by 120 u. Touch landscape is the configuration the rotate hint
        /// steers players into, so the recommended setup was the broken one.
        ///
        /// Anchoring to the speaker line (the existing top of the stack)
        /// instead of to a literal means a future tier change moves these two
        /// with everything else rather than silently burying them again.
        /// </summary>
        void ApplyBottomStackPlacement()
        {
            if (_growthPanelRect == null && _levelToastRect == null) return;
            var lift = _touchActive ? 120f : 0f;
            // Speaker line is the current stack top in both tier branches.
            var speakerY = (_tier == LayoutTier.Phone ? 232f : 132f) + lift;
            const float SpeakerH = 24f, Gap = 14f, PlateH = 62f, Pad = 4f;
            var plateY = speakerY + SpeakerH + Gap;
            if (_growthPanelRect != null)
                _growthPanelRect.anchoredPosition = new Vector2(0f, plateY);
            if (_levelToastRect != null)
                _levelToastRect.anchoredPosition = new Vector2(0f, plateY + PlateH + Pad);
        }

        /// <summary>Spec #4: the campaign equip strip is entombed inside the
        /// joystick box at bottom-left when touch is live — move it under the
        /// meters (phone: under the relocated stats).</summary>
        void ApplyEquipPlacement()
        {
            if (_equipRect == null) return;
            if (_touchActive || _tier == LayoutTier.Phone)
            {
                _equipRect.anchorMin = _equipRect.anchorMax = new Vector2(0f, 1f);
                _equipRect.pivot = new Vector2(0f, 1f);
                _equipRect.anchoredPosition = _tier == LayoutTier.Phone
                    ? new Vector2(16, -214) : new Vector2(16, -98);
            }
            else
            {
                _equipRect.anchorMin = _equipRect.anchorMax = new Vector2(0f, 0f);
                _equipRect.pivot = new Vector2(0f, 0f);
                _equipRect.anchoredPosition = new Vector2(16, 16);
            }
        }

        /// <summary>Spec #14: one-shot "landscape recommended" toast on
        /// portrait dungeon/arena entry. Reuses the prologue toast panel.</summary>
        void ShowRotateHintIfPortrait()
        {
            if (Screen.width >= Screen.height) return;
            if (PlayerPrefs.HasKey("al:rotate-hint")) return;
            PlayerPrefs.SetInt("al:rotate-hint", 1);
            ShowRotateToast();
        }

        void ShowRotateToast()
        {
            // Piggyback on the prologue toast panel; auto-hides via Update.
            ShowPrologueToast(0);
            _prologueToastText.text = "가로 화면을 권장합니다";
            _rotateHintTimer = 2.5f;
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
            var root = (Transform)_safeRoot;

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

            // Default (desktop/full) placement: bottom-left. Touch/phone tiers
            // relocate it under the meters (spec #4 — D-pad burial fix).
            _equipPanel = Panel(root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16, 16), new Vector2(240, 34), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            _equipRect = _equipPanel.GetComponent<RectTransform>();
            _equipRect.pivot = new Vector2(0f, 0f);
            _equipText = Label(_equipPanel.transform, 8, 0, 226, 34, "", 14, TextAnchor.MiddleLeft);
            var equipTextRect = _equipText.rectTransform;
            equipTextRect.anchorMin = Vector2.zero;
            equipTextRect.anchorMax = Vector2.one;
            equipTextRect.sizeDelta = Vector2.zero;
            equipTextRect.anchoredPosition = new Vector2(8, 0);
            ApplyEquipPlacement();

            _stageClearPanel = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480, 240), new Color(0.02f, 0.05f, 0.06f, 0.94f));
            // Modal backdrop: deliberate raycast blocker (see game-over panel).
            _stageClearPanel.GetComponent<Image>().raycastTarget = true;
            // Built with the dungeon wording: PresentationFeedbackTests IDENTIFIES
            // this panel by finding "구역 정화" among its children, so an empty
            // default makes the panel invisible to the audit. A trial swaps the
            // text at reveal time instead (AMENDMENT #10).
            var clearTitle = Label(_stageClearPanel.transform, 0, -18, 480, 36, "구역 정화", 28, TextAnchor.MiddleCenter);
            _stageClearTitle = clearTitle;
            clearTitle.color = new Color(0.56f, 0.91f, 1f);
            _stageClearText = Label(_stageClearPanel.transform, 0, -74, 480, 60, "", 18, TextAnchor.MiddleCenter);
            TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(-105, 24),
                new Vector2(190, 44), "캠페인으로", 18, ReturnHome);
            var stageClearRetryButton = TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(105, 24),
                new Vector2(190, 44), "재강하 (R)", 18, RetryRun);
            _stageClearRetryLabel = stageClearRetryButton.GetComponentInChildren<Text>();
            _stageClearPanel.SetActive(false);

            // The game-over back link is NOT added here any more. It is built
            // with the panel in Build(), so every mode has it — the prologue
            // never called this method and therefore never got an exit.
            ApplyLayoutTier();   // re-grade with the new campaign surfaces
        }

        /// <summary>Campaign per-frame extras (equipment ranks, banner wave).</summary>
        int _lastBannerHash = -1;
        public void SyncCampaign(int wave, bool bossAlive, int weapon, int lantern, int cloak)
        {
            var bannerHash = wave * 4 + (bossAlive ? 1 : 0);
            if (_stageBannerText != null && bannerHash != _lastBannerHash)
            {
                _lastBannerHash = bannerHash;
                // A trial has no wave table (_campaignTotalWaves 0), so the
                // suffix would read "웨이브 0/0" — noise. The name stands alone
                // and the trial banner carries the clock.
                _stageBannerText.text = _campaignTotalWaves <= 0
                    ? _campaignStageName
                    : bossAlive
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

        /// <summary>Refreshes reused campaign surfaces for a direct room handoff.</summary>
        public void RefreshDungeonStage(string stageName, int totalWaves, string bossDisplayName,
                                        bool companionActive)
        {
            _campaignStageName = stageName;
            _campaignTotalWaves = totalWaves;
            _lastBannerHash = -1;
            if (_bossName != null) _bossName.text = bossDisplayName;
            if (_companionHoldButton != null) _companionHoldButton.SetActive(companionActive);
            if (_companionRecallButton != null) _companionRecallButton.SetActive(companionActive);
            if (_companionSkillButton != null) _companionSkillButton.SetActive(companionActive);
            if (_companionStanceLabel != null && !companionActive)
                _companionStanceLabel.gameObject.SetActive(false);
            _lastCompanionSkillTenths = int.MinValue;   // force one relabel on the next sync
            _lastCompanionStanceKey = int.MinValue;      // force one stance relabel too
            _lastRoomObjectiveKey = int.MinValue;        // the next room owns a new objective

        }

        /// <summary>True only while a visible terminal panel can consume the retry shortcut.</summary>
        public bool RetryModalVisible =>
            (_gameOverPanel != null && _gameOverPanel.activeInHierarchy) ||
            (_stageClearPanel != null && _stageClearPanel.activeInHierarchy);

        public void ShowStageClear(RunDigest digest)
        {
            if (_stageClearPanel == null || _stageClearPending || _stageClearPanel.activeSelf)
                return;

            _stageClearFinalScore = digest.Score;
            _stageClearFinalRelics = digest.Relics;
            _trialClearHits = _lastTrialHits;
            _stageClearTimer = StageClearDuration;
            _stageClearPending = true;
            _stageClearBanner.text = _trialStatsHidden ? "시련 완료" : "구역 정화";
            _stageClearBanner.color = new Color(StageClearColor.r, StageClearColor.g, StageClearColor.b, 0f);
            _stageClearBanner.rectTransform.localScale = Vector3.one;
            _stageClearFlash.fillAmount = 0f;
            _stageClearFlash.color = new Color(StageClearColor.r, StageClearColor.g,
                StageClearColor.b, 0.38f * ViewPrefs.MotionScale);
            _stageClearFlash.enabled = true;
        }

        /// <summary>§캡처5: bottom-center speaker subtitle ("포보스: …" grammar,
        /// original text). Boss intro/phase beats only; fades after 3.5 s.</summary>
        public void ShowSpeakerLine(string speaker, string text)
        {
            if (_speakerLine == null) return;
            _speakerLine.text = speaker + ": " + text;
            _speakerLineTimer = 3.5f;
        }

        public void ShowBossIntro(string bossName)
        {
            if (_bossIntroPlate == null) return;
            _bossIntroPlate.text = "— " + bossName + " —";
            _bossIntroTimer = BossIntroDuration;
            _bossIntroActive = true;
            _letterboxTop.enabled = true;
            _letterboxBottom.enabled = true;
            SetBossIntroState(ViewPrefs.ReducedMotion ? 1f : 0f, 0f);
        }


        /// <summary>Whether the reusable Ember Rest panel is currently actionable.</summary>
        internal bool EmberRestVisible => _emberRestBlocker != null && _emberRestBlocker.activeInHierarchy;

        /// <summary>Shows all three deterministic offers published by the active sim.</summary>
        public void ShowEmberRest(IRunPreparationSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.EmberRestOpen) return;
            ShowEmberRest(snapshot.EmberRestRoomIndex, snapshot.EmberRestOffer0,
                snapshot.EmberRestOffer1, snapshot.EmberRestOffer2);
        }

        /// <summary>Test seam for the reusable panel without constructing a simulation.</summary>
        internal void ShowEmberRestForTest(int roomIndex, PreparationOffer offer0,
                                           PreparationOffer offer1, PreparationOffer offer2)
            => ShowEmberRest(roomIndex, offer0, offer1, offer2);

        public void HideEmberRest()
        {
            _emberRestDecisionMade = false;
            _emberRestSelectedIndex = -1;
            if (_emberRestContinueButton != null) _emberRestContinueButton.interactable = false;
            if (_emberRestBlocker != null) _emberRestBlocker.SetActive(false);
        }

        void ShowEmberRest(int roomIndex, PreparationOffer offer0,
                           PreparationOffer offer1, PreparationOffer offer2)
        {
            if (_emberRestBlocker == null) return;
            _emberRestOffers[0] = offer0;
            _emberRestOffers[1] = offer1;
            _emberRestOffers[2] = offer2;
            _emberRestDecisionMade = false;
            _emberRestSelectedIndex = -1;
            _emberRestRoomText.text = $"다음 방 {roomIndex} 준비";
            _emberRestDecisionText.text =
                "준비를 하나 선택하거나 보류하십시오\n다음 방에 적용 (이전 준비 대체)";
            for (var i = 0; i < _emberRestOfferCards.Length; i++)
            {
                if (_emberRestOfferCards[i] == null) continue;
                _emberRestOfferCards[i].color = new Color(0.16f, 0.13f, 0.24f, 0.9f);
                _emberRestOfferCards[i].GetComponentInChildren<Text>().text =
                    EmberRestEffectLabel(_emberRestOffers[i]) + "\n선택";
            }
            _emberRestContinueButton.interactable = false;
            _emberRestBlocker.transform.SetAsLastSibling();
            _emberRestBlocker.SetActive(true);
        }

        void BuildEmberRestPanel(Transform root)
        {
            _emberRestBlocker = Panel(root, Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero, new Color(0f, 0f, 0f, 0.32f));
            _emberRestBlocker.name = "EmberRestBlocker";
            _emberRestBlocker.GetComponent<Image>().raycastTarget = true;
            _emberRestPanel = Panel(_emberRestBlocker.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620, 420), new Color(0.02f, 0.05f, 0.06f, 0.96f));
            _emberRestPanel.name = "EmberRestPanel";
            _emberRestPanel.GetComponent<Image>().raycastTarget = true;
            // Built BEFORE the title/cards/buttons on purpose: uGUI draw order is
            // sibling order, so creating the art and its scrim first is what puts
            // every readable thing on top of them. No sorting call needed, and
            // none can be forgotten later.
            BuildEmberRestBackdrop();
            var title = Label(_emberRestPanel.transform, 0, -18, 620, 34, "잿불 휴식", 26,
                TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.83f, 0.45f);
            _emberRestRoomText = Label(_emberRestPanel.transform, 0, -52, 620, 24, "", 17,
                TextAnchor.MiddleCenter);
            _emberRestRoomText.color = new Color(0.56f, 0.91f, 1f);

            for (var i = 0; i < 3; i++)
            {
                var offerIndex = i;
                var card = TextButton(_emberRestPanel.transform, new Vector2(0f, 1f),
                    new Vector2(20 + i * 200, -88), new Vector2(188, 128), "", 16,
                    () => SelectEmberRestOffer(offerIndex));
                card.name = "EmberRestOffer" + (i + 1);
                _emberRestOfferCards[i] = card.GetComponent<Image>();
            }

            var defer = TextButton(_emberRestPanel.transform, new Vector2(0.5f, 0f),
                new Vector2(-206, 18), new Vector2(196, 92), "준비 보류", 17, DeferEmberRest);
            defer.name = "EmberRestDefer";
            var continueButton = TextButton(_emberRestPanel.transform, new Vector2(0.5f, 0f),
                new Vector2(10, 18), new Vector2(196, 92), "계속", 17, ContinueEmberRest);
            continueButton.name = "EmberRestContinue";
            _emberRestContinueButton = continueButton.GetComponent<Button>();
            _emberRestDecisionText = Label(_emberRestPanel.transform, 0, -236, 620, 40, "", 15,
                TextAnchor.MiddleCenter);
            _emberRestDecisionText.color = new Color(0.92f, 0.94f, 1f);
            _emberRestBlocker.SetActive(false);
        }

        // ------------------------------------------- W15 Ember Rest backdrop --
        // The interlude is the one moment the run stops and the player reads.
        // A painted plate behind it is worth having, but the art is produced on a
        // different lane and may simply not be there — so its ABSENCE is a
        // supported state, not an error. Missing art means the panel renders
        // exactly as it did before this feature existed.

        /// <summary>
        /// Resources id (no extension, no directory) of the Ember Rest backdrop.
        /// This constant is the contract with the asset lane; see the lane report
        /// for the accepted drop paths and the requested aspect.
        /// </summary>
        internal const string EmberRestBackdropId = "ui-ember-rest-bg";

        /// <summary>Scrim opacity over the art. Sized for 15 pt body text on an
        /// unknown painting: the art must still read as art, but no plausible
        /// bright passage may win against the copy on top of it.</summary>
        internal const float EmberRestScrimAlpha = 0.62f;

        /// <summary>True when the painted plate actually loaded. Test seam —
        /// both branches are real shipping states, so the fixture asserts the
        /// one that is live rather than assuming the art exists.</summary>
        internal bool EmberRestBackdropPresent => _emberRestBackdrop != null;

        /// <summary>Live scrim opacity, 0 when there is no art to darken.</summary>
        internal float EmberRestScrimOpacity => _emberRestScrim == null ? 0f : _emberRestScrim.color.a;

        void BuildEmberRestBackdrop()
        {
            var sprite = TryLoadOptionalSprite(EmberRestBackdropId);
            // Fallback IS the previous look: the panel's own near-opaque fill.
            // Nothing below runs, nothing downstream reads these fields without
            // a null check, and no code path can softlock on a missing file.
            if (sprite == null) return;

            _emberRestBackdrop = StretchedChild("EmberRestBackdrop", Color.white);
            _emberRestBackdrop.sprite = sprite;
            _emberRestBackdrop.type = Image.Type.Simple;
            // Deliberately NOT preserveAspect: a letterboxed plate would expose
            // the flat fill along two edges and read as a bug rather than as a
            // frame. The asset lane is given the panel's aspect instead, so the
            // stretch is nominally zero and degrades gently if it is not.
            _emberRestBackdrop.preserveAspect = false;

            _emberRestScrim = StretchedChild("EmberRestScrim",
                new Color(0.02f, 0.03f, 0.05f, EmberRestScrimAlpha));
        }

        /// <summary>Full-bleed child of the Ember Rest panel. Never a raycast
        /// target: the panel underneath already blocks, and a decorative layer
        /// that ate the offer taps would be a softlock.</summary>
        Image StretchedChild(string name, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_emberRestPanel.transform, false);
            var image = child.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return image;
        }

        /// <summary>
        /// Same three-directory search order HudIconIntegration uses
        /// (regenerated → generated → flat), minus its theme material and minus
        /// its not-found warning: this sprite is optional by design, so a miss
        /// is not worth a log line every time the HUD builds.
        /// </summary>
        static Sprite TryLoadOptionalSprite(string iconKey)
        {
            var sprite = Resources.Load<Sprite>($"Icons/regenerated/{iconKey}");
            if (sprite == null) sprite = Resources.Load<Sprite>($"Icons/generated/{iconKey}");
            if (sprite == null) sprite = Resources.Load<Sprite>($"Icons/{iconKey}");
            return sprite;
        }

        void SelectEmberRestOffer(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= _emberRestOffers.Length) return;
            if (OnEmberRestOfferSelected != null && !OnEmberRestOfferSelected(offerIndex))
                return;
            _emberRestDecisionMade = true;
            _emberRestSelectedIndex = offerIndex;
            _emberRestDecisionText.text = "선택됨: " + EmberRestEffectLabel(_emberRestOffers[offerIndex])
                + "\n다음 방에 적용 (이전 준비 대체)";
            UpdateEmberRestSelectionVisuals();
        }

        void DeferEmberRest()
        {
            if (OnEmberRestDeferred != null && !OnEmberRestDeferred()) return;
            _emberRestDecisionMade = true;
            _emberRestSelectedIndex = -1;
            _emberRestDecisionText.text = "준비 보류\n다음 방에 적용 (이전 준비 대체)";
            UpdateEmberRestSelectionVisuals();
        }

        void ContinueEmberRest()
        {
            if (!_emberRestDecisionMade) return;
            OnEmberRestContinue?.Invoke();
        }

        void UpdateEmberRestSelectionVisuals()
        {
            for (var i = 0; i < _emberRestOfferCards.Length; i++)
            {
                if (_emberRestOfferCards[i] == null) continue;
                _emberRestOfferCards[i].color = i == _emberRestSelectedIndex
                    ? new Color(0.28f, 0.52f, 0.46f, 0.96f)
                    : new Color(0.16f, 0.13f, 0.24f, 0.9f);
            }
            _emberRestContinueButton.interactable = true;
        }

        internal static string EmberRestEffectLabel(PreparationOffer offer)
        {
            var magnitude = offer.Magnitude;
            switch (offer.Kind)
            {
                case PreparationOfferKind.Stat:
                    return offer.Variant == 1 ? $"Attack +{magnitude}"
                        : offer.Variant == 2 ? $"Vitality +{magnitude}"
                        : offer.Variant == 3 ? $"Swiftness +{magnitude}" : "Invalid preparation";
                case PreparationOfferKind.SkillRune:
                    return offer.Variant == 1 ? $"Rift Bolt +{10 * magnitude}% damage"
                        : offer.Variant == 2 ? $"Grave Pulse +{10 * magnitude}% tick damage"
                        : offer.Variant == 3 ? $"Ash Nova +{10 * magnitude}% damage" : "Invalid preparation";
                case PreparationOfferKind.GuardianResonance:
                    // AMENDMENT #8: ASCII '-', not U+2212. The source font
                    // NanumBarunGothic has no U+2212, so no amount of font
                    // regeneration can ship it — and gen_hud_font.sh's quote
                    // regex never harvested it, so the coverage check went
                    // green while this label rendered a blank box.
                    return offer.Variant == 1 ? $"Companion cadence -{10 * magnitude}% (min 0.5 s)"
                        : offer.Variant == 2 ? $"Companion range +{20 * magnitude} px"
                        : offer.Variant == 3 ? $"Companion damage +{10 * magnitude}%" : "Invalid preparation";
                default:
                    return "Invalid preparation";
            }
        }
        public void ResetRunUi()
        {
            _maxHealthSeen = SimConfig.PlayerMaxHealth;
            _lastHealth = -1;
            _recentHazardTime = -999f;
            _bossAliveAtDeath = false;
            ResetTransientCeremonies();
            HideEmberRest();
            // Trap guard: a run can end while the console is open (death/clear).
            // Without this, CommandConsoleOpen pins timeScale at 0.2 and
            // TextInputActive keeps the keyboard dead into the lobby.
            CloseCommandConsole(submit: false);
            // AMENDMENT #9 — the same trap, one order of magnitude worse. A run
            // can end while a guidance card is up (a vent kills you on the frame
            // the card explaining vents appears), and GuidancePaused pins
            // timeScale at a hard 0. The console's 0.2 was survivable; 0 freezes
            // Time.deltaTime itself, so nothing would ever run to undo it.
            // Cleared WITHOUT raising OnGuidanceDismissed: the run is over, and
            // marking the bit here would silently consume a lesson the player
            // never got to read.
            if (_guidancePaused)
            {
                _guidancePaused = false;
                _guidancePendingBit = -1;
                if (_guidanceCard != null) _guidanceCard.SetActive(false);
            }
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            SetTouchCombatControlsVisible(true);
            if (_bossBar != null) _bossBar.SetActive(false);
            // Objective chip is room-scoped: clear it on every run entry/retry so a
            // lobby return or an arena sortie can never show the last room's line.
            if (_roomObjectivePanel != null) _roomObjectivePanel.SetActive(false);
            _lastRoomObjectiveKey = int.MinValue;
            // Loot toasts are run-scoped for the same reason: a retry must not
            // open on the last run's final pickup still fading out.
            _lootToasts.Clear();
            SyncLootToasts(0f);

            _lastBossFraction = -1f;
            _lastBossPhase = -1;
            _lastCombo = -1;
            for (var i = 0; i < _comboPipRects.Length; i++)
                if (_comboPipRects[i] != null) _comboPipRects[i].localScale = Vector3.one;
        }

        // ================================================ AMENDMENT #9 =====
        // Guidance: one pause card, one toast line, one codex.
        //
        // SINGLE BUILDER, deliberately. QA's audit of the death-panel overlap
        // found the rule with zero exceptions: a panel built inside one function
        // never overlaps itself, and every band assembled across two or more
        // functions does. The death panel's back link was added by a second
        // function that could not see the first function's rects, and it landed
        // 26 u inside the death-cause text. So the body, the labels and the
        // dismiss hint below are all created here, in one place, measured
        // against each other once.
        GameObject _guidanceCard;
        Text _guidanceKicker, _guidanceTitle, _guidanceBody, _guidanceHint;
        bool _guidancePaused;
        int _guidancePendingBit = -1;
        /// <summary>Raised when the player dismisses a card. GameDirector marks
        /// the bit and saves.</summary>
        public System.Action<int> OnGuidanceDismissed;
        /// <summary>
        /// True while any modal holds the run. GameView reads this to pin
        /// timeScale at 0.
        ///
        /// The abandon modal counts. Without it the player keeps taking damage
        /// while deciding whether to leave — measured in the browser: health
        /// dropped and the damage vignette kept flashing under an open modal.
        /// A confirmation dialog you can die inside is worse than none, because
        /// it punishes the caution it exists to reward.
        ///
        /// Note this is deliberately NOT the flag the dismiss poll reads. Any
        /// key closes a guidance card; only an explicit button closes the
        /// abandon modal, because one of its two answers is destructive.
        /// </summary>
        public bool GuidancePaused =>
            _guidancePaused
            || (_abandonModal != null && _abandonModal.activeSelf)
            || _codexOpen;

        const float GuidanceCardW = 560f, GuidanceCardH = 220f;

        void EnsureGuidanceCard()
        {
            if (_guidanceCard != null || _safeRoot == null) return;
            var root = (Transform)_safeRoot;

            _guidanceCard = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(GuidanceCardW, GuidanceCardH),
                new Color(0.02f, 0.04f, 0.07f, 0.95f));
            // Modal backdrop: while the card holds the run, taps must not reach
            // the combat HUD underneath (same reasoning as the game-over panel).
            _guidanceCard.GetComponent<Image>().raycastTarget = true;

            // Rows measured against the 220 u body, top-down, no gaps under 8 u.
            // Label() measures y DOWN from the parent's top-left, so every row
            // below is [220 + y - h, 220 + y] in panel-local space:
            //   kicker  [186, 204]   title  [148, 182]
            //   body    [ 56, 140]   hint   [ 14,  38]
            _guidanceKicker = Label(_guidanceCard.transform, 28, -16, 504, 18,
                "GUIDANCE", 11, TextAnchor.MiddleLeft);
            // Spectral cyan (#2CADD6) at 0.85 — the worldview's memory/guardian
            // token, the same one the lobby eyebrows use.
            _guidanceKicker.color = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f, 0.85f);

            _guidanceTitle = Label(_guidanceCard.transform, 28, -38, 504, 34,
                "", 24, TextAnchor.MiddleLeft);
            _guidanceTitle.color = new Color(1f, 0.83f, 0.45f);

            _guidanceBody = Label(_guidanceCard.transform, 28, -80, 504, 84,
                "", 17, TextAnchor.UpperLeft);
            _guidanceBody.color = new Color(0.92f, 0.94f, 1f);
            // The only multi-line label on the card — wrap instead of overflow.
            _guidanceBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            // The refusability line. This is the whole reason the card is
            // allowed to exist at eight occurrences: the survey found the
            // variable is not how often a game pauses but whether the player can
            // wave it off (Returnal pauses zero times and was still panned for
            // over-explaining; Into the Breach pauses and draws no complaints).
            _guidanceHint = Label(_guidanceCard.transform, 28, -182, 504, 24,
                "", 13, TextAnchor.MiddleLeft);
            // InkDim (0.62,0.66,0.8) — 8.69:1 on charcoal, clears AA. The
            // dismiss line is the card's most load-bearing sentence; it must not
            // be the least readable one.
            _guidanceHint.color = new Color(0.62f, 0.66f, 0.8f);

            _guidanceCard.SetActive(false);
        }

        /// <summary>
        /// Shows a pause card and freezes the run. Returns false when the entry
        /// is already seen or another card is up — the caller does not have to
        /// track that.
        /// </summary>
        public bool ShowGuidancePause(int bit, string kicker, string title, string body)
        {
            if (_guidancePaused) return false;
            EnsureGuidanceCard();
            if (_guidanceCard == null) return false;
            _guidancePendingBit = bit;
            _guidanceKicker.text = kicker;
            _guidanceTitle.text = title;
            _guidanceBody.text = body;
            _guidanceHint.text = _touchActive ? "화면을 눌러 계속" : "아무 키나 눌러 계속";
            _guidanceCard.SetActive(true);
            _guidancePaused = true;
            SetTouchCombatControlsVisible(false);
            return true;
        }

        /// <summary>
        /// Any key, any tap, any click, any face button. Deliberately the widest
        /// possible net — a dismiss the player has to hunt for is not a dismiss,
        /// and refusability is the entire justification for eight pauses.
        ///
        /// Reads devices directly rather than through InputAdapter: the adapter's
        /// latches are consumed by the sim tick loop, which is frozen while a
        /// card is up, so a press routed through it would either be swallowed or
        /// fire into the sim the instant the freeze lifts.
        /// </summary>
        static bool AnyDismissInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
                return true;
            return false;
        }

        /// <summary>
        /// The codex's dismiss poll. Same net as <see cref="AnyDismissInput"/>,
        /// with one hole in it: a pointer or touch press that lands on a tab
        /// button switches tabs instead of closing. Keyboard and gamepad
        /// presses have no position, so they always close.
        ///
        /// Returns false when the press was positional and hit a tab — the
        /// Button's own onClick has already handled it.
        /// </summary>
        bool CodexDismissInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
                return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return CodexPressDismisses(mouse.position.ReadValue(), positional: true);
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return CodexPressDismisses(touch.primaryTouch.position.ReadValue(), positional: true);
            return false;
        }

        /// <summary>Dismisses the card and releases the run.</summary>
        public void DismissGuidancePause()
        {
            if (!_guidancePaused) return;
            _guidancePaused = false;
            if (_guidanceCard != null) _guidanceCard.SetActive(false);
            SetTouchCombatControlsVisible(true);
            var bit = _guidancePendingBit;
            _guidancePendingBit = -1;
            if (bit >= 0) OnGuidanceDismissed?.Invoke(bit);
        }

        void ResetTransientCeremonies()
        {
            _stageClearPending = false;
            _stageClearTimer = 0f;
            if (_stageClearPanel != null) _stageClearPanel.SetActive(false);
            if (_stageClearFlash != null)
            {
                _stageClearFlash.enabled = false;
                _stageClearFlash.fillAmount = 0f;
                _stageClearFlash.color = new Color(StageClearColor.r, StageClearColor.g,
                    StageClearColor.b, 0f);
            }
            if (_stageClearBanner != null)
            {
                _stageClearBanner.text = string.Empty;
                _stageClearBanner.color = new Color(StageClearColor.r, StageClearColor.g,
                    StageClearColor.b, 0f);
                _stageClearBanner.rectTransform.localScale = Vector3.one;
            }
            _bossIntroTimer = 0f;
            _bossIntroActive = false;
            if (_letterboxTop != null) _letterboxTop.enabled = false;
            if (_letterboxBottom != null) _letterboxBottom.enabled = false;
            if (_bossIntroPlate != null)
            {
                _bossIntroPlate.text = string.Empty;
                _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            }
        }

        void RetryRun()
        {
            if (OnRetryStage != null) OnRetryStage.Invoke();
            else if (Input != null) Input.QueueRestart();
        }

        // ------------------------------------------------- v0.2 visibility --
        Canvas _canvas;
        GameObject _prologueToast;
        Text _prologueToastText;
        static readonly string[] DesktopPrologueSteps =
        {
            "이동 — W A S D 또는 방향키",
            "타격 — Space",
            "기름 게이지를 보라. 초당 +7, 처치당 +6.",
            "웨이브를 비우면 다음 군단이 온다.",
        };

        static readonly string[] TouchPrologueSteps =
        {
            "이동 — 왼쪽 조이스틱 드래그",
            "타격 — 오른쪽 타격 버튼",
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
                HideEmberRest();
                // The left stack belongs to a live run. Left up, it would sit
                // over the lobby offering to forfeit a run that has ended, and
                // a codex whose numbers describe a sim that no longer exists.
                SetLeftStackAvailable(false);
            }
        }

        /// <summary>Campaign/dungeon-only surfaces toggle for arena runs.</summary>
        public void SetCampaignSurfacesVisible(bool visible)
        {
            if (_stageBanner != null) _stageBanner.SetActive(visible);
            if (_equipPanel != null) _equipPanel.SetActive(visible);
            if (_dungeonRoot != null) _dungeonRoot.SetActive(visible);
            ApplyLayoutTier();   // lore/row anchors depend on dungeon-vs-arena
            // Arena's own 2-card row is the inverse (prologue hides both rows).
            SetArenaCardsVisible(!visible && !_prologueMode);
            SyncTouchModeSurfaces();
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
            SyncTouchModeSurfaces();
        }

        /// <summary>Dash touch button + strike height track the dungeon row
        /// (mobile spec #4/#5): dash-by-thumb exists only where dash exists.</summary>
        void SyncTouchModeSurfaces()
        {
            var terminalModalVisible = (_gameOverPanel != null && _gameOverPanel.activeSelf) ||
                                       (_stageClearPanel != null && _stageClearPanel.activeSelf);
            var combatControlsVisible = _touchCombatControlsVisible && !terminalModalVisible;
            if (_touchJoystickRoot != null && _touchJoystickRoot.activeSelf != combatControlsVisible)
                _touchJoystickRoot.SetActive(combatControlsVisible);
            if (_strikeRect != null && _strikeRect.gameObject.activeSelf != combatControlsVisible)
                _strikeRect.gameObject.SetActive(combatControlsVisible);

            var dungeonOn = _dungeonRoot != null && _dungeonRoot.activeSelf;
            var dashVisible = combatControlsVisible && dungeonOn;
            if (_dashTouchRect != null && _dashTouchRect.gameObject.activeSelf != dashVisible)
                _dashTouchRect.gameObject.SetActive(dashVisible);
            if (_strikeRect != null)
                _strikeRect.anchoredPosition = new Vector2(-24, dungeonOn ? 150 : 36);
        }

        void SetTouchCombatControlsVisible(bool visible)
        {
            _touchCombatControlsVisible = visible;
            if (!visible && Input != null)
            {
                Input.ClearTouchState();
                Input.ClearCombatLatches();
            }
            SyncTouchModeSurfaces();
        }


        /// <summary>Test seam: the three pointer targets must disable together
        /// beneath a terminal modal, then restore when the simulation resumes.</summary>
        internal void CollectCombatTouchTargetsForTest(List<RectTransform> into)
        {
            if (_touchJoystickRoot != null) into.Add(_touchJoystickRoot.GetComponent<RectTransform>());
            if (_strikeRect != null) into.Add(_strikeRect);
            if (_dashTouchRect != null) into.Add(_dashTouchRect);
        }

        void SetArenaCardsVisible(bool visible)
        {
            if (_novaGroup != null) _novaGroup.gameObject.SetActive(visible);
            if (_wardGroup != null) _wardGroup.gameObject.SetActive(visible);
        }

        /// <summary>Prologue tutorial toast (spec §1). step -1 hides.</summary>
        public void ShowPrologueToast(int step)
        {
            var steps = _touchActive ? TouchPrologueSteps : DesktopPrologueSteps;
            if (step < 0 || step >= steps.Length) { HidePrologueToast(); return; }
            if (_prologueToast == null)
            {
                var root = (Transform)_safeRoot;
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
            _prologueToastText.text = steps[step];
            // Colour is set per call, not just at build: ShowGuidanceToast
            // reuses this surface and leaves it gold, so a later prologue step
            // would inherit the wrong colour.
            _prologueToastText.color = new Color(0.62f, 0.95f, 0.88f);
        }

        /// <summary>
        /// AMENDMENT #9 toast tier — the 15 entries that do NOT stop the run.
        ///
        /// Reuses the prologue toast surface rather than adding a fourth
        /// top-centre band. QA's audit found every overlap in this HUD came from
        /// a band assembled across two functions, and a new surface at the same
        /// altitude as the wave banner, the surge banner and this toast would be
        /// a fourth chance to make that mistake. The rotate hint already
        /// piggybacks here with an auto-hide timer; this follows that precedent.
        /// </summary>
        public void ShowGuidanceToast(string title, string body)
        {
            ShowPrologueToast(0);            // ensures the surface exists
            if (_prologueToastText == null) return;
            _prologueToastText.text = $"{title} — {body}";
            // Gold: this is a reward-shaped line (you learned something), not a
            // warning. 12.20:1 on charcoal.
            _prologueToastText.color = new Color(0.87f, 0.78f, 0.41f);
            _rotateHintTimer = GuidanceToastSeconds;
        }

        // ------------------------------------------------- abandon (C4) -----
        // The reported gap: once inside a campaign stage there was no way out.
        // The death panel's back link existed but sat 26 u under the death-cause
        // text, so "I died and still could not leave" was an accurate report of
        // what the screen showed.
        //
        // Built in ONE function — body, labels and both buttons — because the
        // death panel's overlap came from exactly the opposite: a body built in
        // Build() and a button appended later by EnableCampaignUi(), where the
        // second function could not see the first one's rects.
        GameObject _abandonButton, _abandonModal, _codexButton;
        /// <summary>Gap between left-stack buttons and the occupant above them.
        /// 8 u is the guidance card's own minimum — same eye, same surface.</summary>
        const float LeftStackGap = 8f;
        Text _abandonModalBody;
        /// <summary>Raised when the player confirms. GameDirector forfeits and
        /// returns to the lobby.</summary>
        public System.Action OnAbandonConfirmed;
        /// <summary>Asked for the relic count the modal must name. Returning it
        /// through a callback keeps HudView out of the economy.</summary>
        public System.Func<int> AbandonRelicsAtRisk;

        void EnsureLeftStackSurfaces()
        {
            if (_abandonButton != null || _safeRoot == null) return;
            var root = (Transform)_safeRoot;

            // AMENDMENT #9 — one function owns this band (§4f).
            //
            // The 포기 button used to place itself at a static (16,-100) in
            // every tier, which collided with three different neighbours
            // depending on tier and touch state (22 u desktop x shield, 32 u
            // landscape x equip strip, 34 u phone x stats panel). All three
            // are pinned RED in HudLayoutTests before this function existed.
            //
            // Both buttons are now placed by ApplyLeftStackPlacement, which
            // reads the live occupant edges instead of assuming them.
            _codexButton = TextButton(root, new Vector2(0f, 1f),
                Vector2.zero, new Vector2(96f, 34f), "정보", 14, OpenCodex,
                iconId: "ui-codex");
            _codexButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            _codexButton.SetActive(false);

            // Deliberately NOT beside the mute toggle: an accidental abandon
            // costs the whole run, so it does not sit in the row a player taps
            // casually. For the same reason it keeps its word next to the glyph.
            _abandonButton = TextButton(root, new Vector2(0f, 1f),
                Vector2.zero, new Vector2(96f, 34f), "포기", 14, OpenAbandonModal,
                iconId: "ui-abandon");
            _abandonButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            _abandonButton.SetActive(false);
            ApplyLeftStackPlacement();

            _abandonModal = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480f, 200f), new Color(0.03f, 0.02f, 0.06f, 0.95f));
            _abandonModal.GetComponent<Image>().raycastTarget = true;

            // Rows against the 200 u body: title [148,182], body [72,136],
            // buttons [20,64]. Buttons split 480 with a 16 u gutter.
            var title = Label(_abandonModal.transform, 24, -18, 432, 34,
                "강하를 포기하는가", 22, TextAnchor.MiddleCenter);
            // Ember #F3592C — 6.11:1, clears AA. The survey's one confirmed
            // abandon-modal example (Slay the Spire) still loses runs because
            // its dialog looks like the safe one next to it, so this one is
            // coloured as a warning and states the loss instead of asking a
            // neutral question.
            title.color = new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f);

            _abandonModalBody = Label(_abandonModal.transform, 24, -64, 432, 64,
                "", 16, TextAnchor.UpperCenter);
            _abandonModalBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _abandonModalBody.color = new Color(0.92f, 0.94f, 1f);

            var back = TextButton(_abandonModal.transform, new Vector2(0.5f, 0f),
                new Vector2(-114f, 20f), new Vector2(212f, 44f), "계속 싸운다", 16,
                CloseAbandonModal);
            back.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var quit = TextButton(_abandonModal.transform, new Vector2(0.5f, 0f),
                new Vector2(114f, 20f), new Vector2(212f, 44f), "포기하고 나간다", 16,
                ConfirmAbandon);
            quit.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

            _abandonModal.SetActive(false);
        }

        /// <summary>Shows or hides the whole left stack — codex and abandon.
        /// Every run mode gets both; the lobby gets neither.</summary>
        public void SetLeftStackAvailable(bool available)
        {
            if (available) EnsureLeftStackSurfaces();
            if (_codexButton != null) _codexButton.SetActive(available);
            if (_abandonButton != null) _abandonButton.SetActive(available);
            if (!available)
            {
                CloseAbandonModal();
                CloseCodex();
            }
            else ApplyLeftStackPlacement();
        }

        /// <summary>
        /// AMENDMENT #9 — places both left-stack buttons against the LIVE
        /// occupant edges, in the arrangement the orientation can afford.
        ///
        /// Vertical in portrait and on the desktop. HORIZONTAL in landscape
        /// with touch, and that is the load-bearing case: a 2-high stack wants
        /// 192 u of vertical room in the one orientation where vertical room is
        /// scarce, and the joystick catch box owns the bottom 260 u. Measured,
        /// a vertical stack clears by +32.6 u at 844x390 with NO browser chrome
        /// and goes NEGATIVE the moment a toolbar appears (-7.1 u at 844x344,
        /// -12.8 u on a Pixel 7). Clamping cannot rescue it: staying below the
        /// occupants and above the joystick become contradictory below 379.8
        /// CSS px. Pairing the buttons drops the requirement to 92 u and moves
        /// that floor to 271.0 — a chrome budget of 119 px instead of 10.
        /// </summary>
        void ApplyLeftStackPlacement()
        {
            if (_codexButton == null || _abandonButton == null) return;
            var codex = (RectTransform)_codexButton.transform;
            var abandon = (RectTransform)_abandonButton.transform;

            // Lowest live edge in the left column. Read, never assumed — the
            // shield and equip strip move per tier and per touch state, and
            // assuming their build coordinates is what produced the 22/32/34 u
            // collisions this function exists to close.
            var occupied = -90f;                      // meters [-90,-16], always live
            if (_statsRect != null && _statsRect.gameObject.activeInHierarchy
                && _statsRect.anchorMin.x < 0.5f)     // left-column only; desktop parks it top-right
                occupied = Mathf.Min(occupied, _statsRect.anchoredPosition.y - _statsRect.sizeDelta.y);
            if (_equipRect != null && _equipRect.gameObject.activeInHierarchy
                && _equipRect.anchorMax.y > 0.5f)     // top-anchored only; desktop parks it bottom-left
                occupied = Mathf.Min(occupied, _equipRect.anchoredPosition.y - _equipRect.sizeDelta.y);
            if (_shieldRect != null && _shieldRect.gameObject.activeInHierarchy)
                occupied = Mathf.Min(occupied, _shieldRect.anchoredPosition.y - _shieldRect.sizeDelta.y);

            var phone = _tier == LayoutTier.Phone;
            var height = phone ? 92f : 34f;
            var width = phone ? 92f : 96f;
            var top = occupied - LeftStackGap;

            // Horizontal whenever the joystick is live and the screen is wide:
            // width is abundant there (216 u needed against ~1500 u) and height
            // is not.
            var horizontal = _touchActive && !phone;
            codex.sizeDelta = new Vector2(width, height);
            abandon.sizeDelta = new Vector2(width, height);
            codex.anchoredPosition = new Vector2(16f, top);
            abandon.anchoredPosition = horizontal
                ? new Vector2(16f + width + LeftStackGap, top)
                : new Vector2(16f, top - height - LeftStackGap);
        }

        void OpenAbandonModal()
        {
            EnsureLeftStackSurfaces();
            // Symmetric with OpenCodex: the two run-holding surfaces are
            // mutually exclusive. This direction matters more than the other —
            // the codex is 620x440 and covers the modal's buttons outright, so
            // stacking here would hide the very choice the modal exists to ask.
            CloseCodex();
            var relics = AbandonRelicsAtRisk != null ? AbandonRelicsAtRisk() : 0;
            // Name the number. A modal that says "are you sure?" and a modal
            // that says "you lose 7 relics" are different safeguards, and only
            // the second one survives muscle memory.
            _abandonModalBody.text = relics > 0
                ? $"이번 강하에서 모은 유물 {relics}개를 잃는다.\n정화 기록도 남지 않는다."
                : "정화 기록이 남지 않는다.\n아직 모은 유물은 없다.";
            _abandonModal.SetActive(true);
            SetTouchCombatControlsVisible(false);
        }

        void CloseAbandonModal()
        {
            if (_abandonModal == null || !_abandonModal.activeSelf) return;
            _abandonModal.SetActive(false);
            SetTouchCombatControlsVisible(true);
        }

        void ConfirmAbandon()
        {
            CloseAbandonModal();
            OnAbandonConfirmed?.Invoke();
        }

        /// <summary>Seconds a guidance toast holds. Long enough to read 33 words
        /// at the survey's 3.3 words/s reading rate, plus a beat.</summary>
        public const float GuidanceToastSeconds = 4.5f;

        /// <summary>Input depth §3: charge gauge. Lazily built like the
        /// prologue toast so it costs nothing until a player first holds the
        /// attack key. raycastTarget stays false — decoration only.</summary>
        void SyncChargeGauge(float progress)
        {
            if (progress <= 0f)
            {
                if (_chargeGauge != null && _chargeGauge.activeSelf) _chargeGauge.SetActive(false);
                return;
            }
            if (_chargeGauge == null)
            {
                var root = (Transform)_safeRoot;
                _chargeGauge = Panel(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 96), new Vector2(220, 8), new Color(0.02f, 0.02f, 0.04f, 0.8f));
                _chargeGauge.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
                var fillObject = new GameObject("ChargeFill");
                fillObject.transform.SetParent(_chargeGauge.transform, false);
                _chargeGaugeFill = fillObject.AddComponent<Image>();
                _chargeGaugeFill.raycastTarget = false;
                AsFilled(_chargeGaugeFill, Image.FillMethod.Horizontal);
                var fillRect = _chargeGaugeFill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.anchoredPosition = Vector2.zero;
            }
            _chargeGauge.SetActive(true);
            _chargeGaugeFill.fillAmount = progress;
            // Ember while building, gold at full — the colour IS the "ready"
            // signal, so the player never has to read the bar's length.
            _chargeGaugeFill.color = progress >= 1f
                ? new Color(0.87f, 0.78f, 0.41f)
                : Color.Lerp(new Color(0.95f, 0.35f, 0.17f, 0.75f),
                             new Color(0.87f, 0.78f, 0.41f), progress * progress);
        }

        /// <summary>AMENDMENT #9: the momentum gauge. Sits directly above the charge bar
        /// and is built lazily on the first tick that has any momentum at all, so an
        /// arena or prologue run — where the gauge can never move — never allocates it.
        /// The TIER, not the raw value, drives the colour and the label: a player needs
        /// to read "am I buffed and by how much", not a percentage.</summary>
        void SyncMomentumGauge(float momentum, int tier, float multiplier)
        {
            if (momentum <= 0f)
            {
                if (_momentumGauge != null && _momentumGauge.activeSelf)
                {
                    _momentumGauge.SetActive(false);
                    _momentumTierShown = -1;
                }
                return;
            }

            if (_momentumGauge == null)
            {
                var root = (Transform)_safeRoot;
                _momentumGauge = Panel(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 112), new Vector2(220, 10), new Color(0.02f, 0.02f, 0.04f, 0.8f));
                _momentumGauge.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

                var fillObject = new GameObject("MomentumFill");
                fillObject.transform.SetParent(_momentumGauge.transform, false);
                _momentumGaugeFill = fillObject.AddComponent<Image>();
                _momentumGaugeFill.raycastTarget = false;
                _momentumGaugeFill.type = Image.Type.Filled;
                _momentumGaugeFill.fillMethod = Image.FillMethod.Horizontal;
                var fillRect = _momentumGaugeFill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.anchoredPosition = Vector2.zero;

                var labelObject = new GameObject("MomentumTier");
                labelObject.transform.SetParent(_momentumGauge.transform, false);
                _momentumTierLabel = labelObject.AddComponent<Text>();
                _momentumTierLabel.font = _font;
                _momentumTierLabel.fontSize = 13;
                _momentumTierLabel.alignment = TextAnchor.MiddleCenter;
                _momentumTierLabel.raycastTarget = false;
                _momentumTierLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                var labelRect = _momentumTierLabel.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 1f);
                labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(220, 18);
                labelRect.anchoredPosition = new Vector2(0, 2);
            }

            _momentumGauge.SetActive(true);
            // The real sim constant, restored: main b97d609 landed HackSpec.MomentumMax
            // with the A9 sim half. A local placeholder stood here only while that
            // half was missing — a view literal would drift from the sim the first
            // time the amendment retunes.
            _momentumGaugeFill.fillAmount = Mathf.Clamp01(momentum / HackSpec.MomentumMax);
            _momentumGaugeFill.color = MomentumTierColor(tier);

            // Relabel only on a tier change — the raw value moves every tick, the tier
            // does not, and rebuilding a Text mesh 60 times a second for an unchanged
            // string is exactly the kind of WebGL cost §1 forbids.
            if (tier != _momentumTierShown)
            {
                _momentumTierShown = tier;
                _momentumTierLabel.text = tier <= 0
                    ? string.Empty
                    : $"기세 x{multiplier:0.00}";
                _momentumTierLabel.color = MomentumTierColor(tier);
            }
        }

        /// <summary>A9: one colour per tier, ascending in heat so the promotion reads at a
        /// glance. Tier 0 is the muted "no buff" steel the bar drains back to.</summary>
        static Color MomentumTierColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.95f, 0.55f, 0.24f);
                case 2: return new Color(0.99f, 0.78f, 0.30f);
                case 3: return new Color(1f, 0.95f, 0.72f);
                default: return new Color(0.42f, 0.48f, 0.62f);
            }
        }


        /// <summary>Input depth §5: the level-up offer. Shows the three keys
        /// and the countdown, so an ignoring player can SEE that waiting is
        /// safe rather than having to learn it.</summary>
        void SyncGrowthOffer(bool open, float secondsLeft)
        {
            if (!open)
            {
                if (_growthPanel != null && _growthPanel.activeSelf) _growthPanel.SetActive(false);
                return;
            }
            if (_growthPanel == null)
            {
                var root = (Transform)_safeRoot;
                // AMENDMENT #9 (entry 15): y 150 was measured against the
                // desktop skill row [18,94] and cleared it by 56 u. Touch lifts
                // that row to [138,214], which swallowed 62 of the card's 76 u
                // — 81.6%, in the configuration the rotate hint recommends.
                // The plate is raycast-off so taps still landed: the same
                // "works but is invisible" signature as the death-panel button.
                _growthPanel = Panel(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 170), new Vector2(440, 62), new Color(0.03f, 0.03f, 0.06f, 0.88f));
                _growthPanelRect = _growthPanel.GetComponent<RectTransform>();
                _growthPanelRect.pivot = new Vector2(0.5f, 0f);
                // Label() measures y DOWN from the parent's top-left, so the
                // original y=+16 put the title 16 u ABOVE the plate's top edge.
                // Pre-existing, and invisible only because the plate itself was
                // buried in the skill row — moving the plate exposed it.
                // Plate is 62 u: title [34,58], options [8,32], both inside.
                _growthTitle = Label(_growthPanel.transform, 0, -4, 440, 24,
                    "", 15, TextAnchor.MiddleCenter);
                _growthTitle.color = new Color(0.87f, 0.78f, 0.41f);
                _growthOptions = Label(_growthPanel.transform, 0, -30, 440, 24,
                    "1 공격  •  2 생명  •  3 민첩", 16, TextAnchor.MiddleCenter);
                _growthOptions.color = new Color(0.72f, 0.86f, 0.95f);
                // Built lazily on the first level-up, which can land AFTER the
                // last ApplyLayoutTier — place it against the current tier now
                // or it keeps the desktop y until the next resolution change.
                ApplyBottomStackPlacement();
            }
            _growthPanel.SetActive(true);
            _growthTitle.text = $"레벨 업 — 강화 선택 ({Mathf.CeilToInt(secondsLeft)})";
        }

        public void HidePrologueToast()
        {
            if (_prologueToast != null) _prologueToast.SetActive(false);
        }

        // ============================================ companion command console --
        // Text orders for the guardian (집중공격/방어/복귀…) + player skill casts.
        // Local keyword parse first; free-form falls through to Gemini when the
        // player has stored a key (runtime only — never in the build). Every
        // resolved intent becomes a deterministic InputAdapter latch.
        GameObject _consoleRoot;
        InputField _consoleField;
        Text _consoleToast;
        float _consoleToastTimer;
        bool _consoleBusy;               // one in-flight Gemini call max
        // New-Input-System-only project (activeInputHandler:1): legacy uGUI
        // InputField reads the OLD Input.inputString/IMGUI stream, which is
        // dead here — so typing never reached the field (only Enter/ESC worked,
        // read straight off Keyboard.current). Feed Keyboard.onTextInput by
        // hand instead (Unity input-system docs: read-keyboard-text-input).
        // The field itself is readOnly so it can NEVER also write: with an IME
        // active the field's own KeyPressed path still saw the committed Hangul
        // syllable and appended a second copy ("한" -> "한한"). One writer only,
        // and it is this buffer.
        System.Action<char> _consoleTextHandler;
        UnityEngine.InputSystem.Keyboard _consoleTextKeyboard;   // exact device we subscribed to
        readonly CommandConsoleBuffer _consoleBuffer = new CommandConsoleBuffer(ConsoleCharacterLimit);
        const int ConsoleCharacterLimit = 60;
        // W11 — WebGL browser IME. emscripten never delivers composition events
        // to the canvas, so on WebGL a hidden <input> takes the keyboard while
        // the console is open and this state machine turns its DOM events into
        // console text. The bridge and the Keyboard.onTextInput mirror are
        // mutually exclusive: exactly one writer per session, same invariant
        // the duplication fix above established.
        CommandConsoleImeComposition _consoleIme;
        CommandConsoleImeComposition ConsoleIme => _consoleIme ??=
            new CommandConsoleImeComposition(_consoleBuffer, ConsoleCharacterLimit);
        bool _consoleImeBridge;      // hidden-input bridge owns this session
        bool _consoleImeDirty;       // pre-edit changed; repaint the field on the next tick
        int _consoleImeClose;        // 0 none, 1 submit, 2 cancel — drained in Update

        /// <summary>GameView caps timeScale at 0.2 while this is true — typing
        /// time, NOT decoration: deliberately outside TimeEffectsAllowed so
        /// reduced-motion players get the same breathing room.</summary>
        public bool CommandConsoleOpen { get; private set; }

        void BuildCommandConsole()
        {
            var root = (Transform)_safeRoot;
            _consoleRoot = Panel(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 300), new Vector2(460, 40), new Color(0.03f, 0.04f, 0.09f, 0.92f));
            _consoleRoot.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            _consoleRoot.GetComponent<Image>().raycastTarget = true;   // field hit surface

            var fieldObject = new GameObject("CommandField");
            fieldObject.transform.SetParent(_consoleRoot.transform, false);
            var fieldRect = fieldObject.AddComponent<RectTransform>();
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = new Vector2(10, 4);
            fieldRect.offsetMax = new Vector2(-10, -4);

            var text = Label(fieldObject.transform, 0, 0, 440, 32, "", 16, TextAnchor.MiddleLeft);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.supportRichText = false;

            var placeholder = Label(fieldObject.transform, 0, 0, 440, 32,
                // The trigger half of the syntax is in the placeholder because a
                // queue nobody knows how to fill is not a feature.
                //
                // Separator is BULLET (U+2022), not MIDDLE DOT (U+00B7). Main's
                // copy used the middle dot and main ships a font that cannot
                // render it — five of its player-visible strings were tofu on
                // WebGL when this merge measured them (difficulty badge,
                // objective lines, companion toast). All were converted.
                //
                // The codepoints are NAMED here, not typed. gen_hud_font.sh
                // harvests every non-ASCII character in this file including
                // comments, so a comment explaining that a glyph is unusable
                // would itself demand that glyph — the first draft of this note
                // did exactly that and turned the font gate red.
                //
                // U+00B7 IS in NanumBarunGothic, which is what makes the trap
                // work: it lives ONLY in the platform-1 Macintosh format-6
                // subtable, 189 entries. fontTools.subset and getBestCmap both
                // read the Unicode subtable, where it is absent — the glyph
                // exists in the file, cannot enter the subset, and renders as a
                // box. A cmap probe that unions every subtable reports it
                // present and is answering a question no part of the toolchain
                // asks. CLAUDE.md §4b was right; a union-probe during this merge
                // briefly said otherwise, and the union-probe was wrong.
                "명령: 집중공격 • 노바 • 결계 / 대기: 셋 잡으면 노바", 15, TextAnchor.MiddleLeft);
            var placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            placeholder.color = new Color(0.65f, 0.68f, 0.78f, 0.55f);

            _consoleField = fieldObject.AddComponent<InputField>();
            _consoleField.textComponent = text;
            _consoleField.placeholder = placeholder;
            _consoleField.characterLimit = ConsoleCharacterLimit;
            _consoleField.lineType = InputField.LineType.SingleLine;
            // readOnly kills the field's OWN writer (InputField.Append/Backspace
            // early-return on readOnly) while ActivateInputField still turns the
            // IME on and the caret stays visible. Without this the IME-committed
            // Hangul syllable was appended twice: once by the field, once by the
            // Keyboard.onTextInput mirror below.
            _consoleField.readOnly = true;


            _consoleToast = Label(root, 0, 346, 560, 28, "", 15, TextAnchor.MiddleCenter);
            var toastRect = _consoleToast.rectTransform;
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.anchoredPosition = new Vector2(0, 346);
            _consoleToast.color = new Color(0.62f, 0.95f, 0.88f, 0f);

            // W10: the parked-order readout. Built with the console because the
            // console is the only thing that can create a queue entry, and it
            // stays hidden until one exists.
            BuildCommandQueuePanel();

            _consoleRoot.SetActive(false);
        }

        public void ToggleCommandConsole()
        {
            if (CommandConsoleOpen) CloseCommandConsole(submit: false);
            else OpenCommandConsole();
        }

        void OpenCommandConsole()
        {
            // Dungeon-only surface: orders need a guardian on the field.
            if (_dungeonRoot == null || !_dungeonRoot.activeSelf) return;
            if (_consoleRoot == null) BuildCommandConsole();
            _consoleRoot.SetActive(true);
            CommandConsoleOpen = true;
            if (Input != null) Input.TextInputActive = true;
            _consoleBuffer.Clear();
            ConsoleIme.Clear();
            _consoleImeDirty = false;
            _consoleImeClose = 0;
            _consoleField.text = string.Empty;
            _consoleField.ActivateInputField();
            // W11 — WebGL first: hand the keyboard to the hidden browser input
            // so an IME has an editable element to compose into. Enter/ESC come
            // back through the bridge in that mode (Unity stops capturing).
            _consoleImeBridge = WebGLHangulIme.Open(OnConsoleImeEvent);
            // New-input-only project: the uGUI InputField can't pull text from
            // the dead legacy Input stream, so we mirror Keyboard.onTextInput
            // into the field ourselves (printable chars + backspace). Skipped
            // when the bridge is live — two writers is the bug we already fixed.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!_consoleImeBridge && keyboard != null && _consoleTextHandler == null)
            {
                _consoleTextHandler = OnConsoleTextInput;
                _consoleTextKeyboard = keyboard;
                keyboard.onTextInput += _consoleTextHandler;
            }
            if (!GeminiCommandClient.HasKey)
                ShowConsoleToast("로컬 명령: 집중공격/방어/복귀/스킬명 • 자유 문장은 '키 <Gemini키>' 등록 후", 3.5f);
        }


        void OnConsoleTextInput(char c)
        {
            if (!CommandConsoleOpen || _consoleField == null) return;
            // Every editing rule (control chars, backspace, 60-char cap,
            // same-frame duplicate suppression) lives in the buffer; the field
            // is a readOnly display of it.
            if (!_consoleBuffer.Feed(c, Time.frameCount)) return;
            _consoleField.text = _consoleBuffer.Text;
            _consoleField.caretPosition = _consoleField.selectionAnchorPosition =
                _consoleField.selectionFocusPosition = _consoleBuffer.Length;
        }



        /// <summary>W11 — one DOM composition event from hangul_ime.jslib.
        /// Fires between Unity frames (the browser's event loop, same thread),
        /// so it only touches the pure-C# composition state; the uGUI field and
        /// the console lifecycle are driven from UpdateCommandConsole.</summary>
        void OnConsoleImeEvent(ConsoleImeEvent kind, string payload)
        {
            if (!CommandConsoleOpen) return;
            var frame = Time.frameCount;
            switch (kind)
            {
                case ConsoleImeEvent.CompositionStart:
                    _consoleImeDirty |= ConsoleIme.BeginComposition();
                    break;
                case ConsoleImeEvent.CompositionUpdate:
                    _consoleImeDirty |= ConsoleIme.UpdateComposition(payload);
                    break;
                case ConsoleImeEvent.CompositionEnd:
                    _consoleImeDirty |= ConsoleIme.EndComposition(payload, frame);
                    break;
                case ConsoleImeEvent.Insert:
                    _consoleImeDirty |= ConsoleIme.Insert(payload, frame);
                    break;
                case ConsoleImeEvent.DeleteBackward:
                    _consoleImeDirty |= ConsoleIme.DeleteBackward(frame);
                    break;
                case ConsoleImeEvent.Submit:
                    _consoleImeClose = 1;
                    break;
                case ConsoleImeEvent.Cancel:
                    // ESC mid-syllable abandons the syllable, not the console —
                    // the player is correcting a typo, not leaving.
                    if (ConsoleIme.IsComposing) _consoleImeDirty |= ConsoleIme.CancelComposition();
                    else _consoleImeClose = 2;
                    break;
            }
        }

        /// <summary>Applies whatever the browser IME did since the last frame.</summary>
        void DrainConsoleIme()
        {
            if (_consoleImeDirty && _consoleField != null)
            {
                _consoleImeDirty = false;
                _consoleField.text = ConsoleIme.Text;
                _consoleField.caretPosition = _consoleField.selectionAnchorPosition =
                    _consoleField.selectionFocusPosition = ConsoleIme.Length;
            }
            if (_consoleImeClose == 0) return;
            var submit = _consoleImeClose == 1;
            _consoleImeClose = 0;
            CloseCommandConsole(submit);
        }

        void CloseCommandConsole(bool submit)
        {
            if (_consoleRoot == null) return;
            // Flush first: a syllable still in the IME belongs to the player who
            // typed it, not to the frame they pressed Enter on.
            var raw = ConsoleIme.Flush(Time.frameCount);   // buffer is the single source of truth
            if (_consoleImeBridge)
            {
                WebGLHangulIme.Close();
                _consoleImeBridge = false;
            }
            _consoleImeDirty = false;
            _consoleImeClose = 0;
            _consoleField.DeactivateInputField();
            // Detach the manual text feed so it never leaks onto other surfaces.
            // Unsubscribe from the EXACT device we subscribed to: if
            // Keyboard.current changed while the console was open, removing the
            // handler from the new device would leave the old subscription live
            // and every character would arrive twice on the next open.
            if (_consoleTextHandler != null)
            {
                if (_consoleTextKeyboard != null)
                    _consoleTextKeyboard.onTextInput -= _consoleTextHandler;
                _consoleTextHandler = null;
                _consoleTextKeyboard = null;
            }
            _consoleBuffer.Clear();
            ConsoleIme.Clear();
            _consoleRoot.SetActive(false);
            CommandConsoleOpen = false;
            if (Input != null) Input.TextInputActive = false;
            if (submit && !string.IsNullOrWhiteSpace(raw)) SubmitCommand(raw.Trim());
        }



        void SubmitCommand(string raw)
        {
            // Key registration: "키 AIza..." / "key AIza..." — stored locally
            // (PlayerPrefs), never in the build. Fragment URL (#gemini=) works too.
            if (raw.StartsWith("키 ") || raw.StartsWith("key ", System.StringComparison.OrdinalIgnoreCase))
            {
                var key = raw.Substring(raw.IndexOf(' ') + 1).Trim();
                if (key.Length > 8)
                {
                    GeminiCommandClient.StoreKey(key);
                    ShowConsoleToast("Gemini 키 저장됨 (이 기기에만 난독화 저장) — 자유 문장 명령 활성화", 3f);
                }
                else ShowConsoleToast("키가 너무 짧습니다", 2f);
                return;
            }

            // Sequence control ("취소"/"중단"/"stop") outranks everything else:
            // it is the one order a player gives while another is still running.
            if (TryHandleAgentControl(raw)) return;

            // W10: an order that names an EVENT ("셋 잡으면 노바") is parked in
            // the work queue instead of firing now. Falls through untouched when
            // the sentence carries no trigger, so a plain order is unaffected.
            if (TryQueueCommand(raw)) return;

            // Local plan first — zero latency, no key, and it reads the sentence
            // in POSITION order, so "노바 쓰고 결계 쳐" is two steps rather than
            // the single rule-priority match the old classifier returned.
            var localPlan = CommandPlanParser.ParseLocal(raw);
            if (!localPlan.IsEmpty)
            {
                StartCommandPlan(localPlan);
                return;
            }
            if (!GeminiCommandClient.HasKey)
            {
                ShowConsoleToast("알 수 없는 명령 — 키워드: 집중공격/방어/복귀/특기/노바/결계/파동/화살/질주", 3f);
                return;
            }
            if (_consoleBusy) { ShowConsoleToast("이전 명령 해석 중…", 1.5f); return; }
            _consoleBusy = true;
            ShowConsoleToast("시퀀스 구성 중…", 1.5f);
            // Free-form sentence -> ordered plan. The runner then spends it one
            // finished event at a time (HudView.CommandAgent.cs).
            StartCoroutine(PlanRemote(raw));
        }

        /// <summary>Intent -> deterministic latch. The reply copy is honest about the
        /// actor: 수호자 orders drive the companion, 시전 lines are the PLAYER's own kit.
        /// AMENDMENT #8 added the one case where the companion itself acts —
        /// CompanionSkill — and its copy names the companion for that reason.
        ///
        /// <paramref name="prefix"/> ("2/4 • ") and <paramref name="detail"/> (the
        /// plan's own rationale for this beat) are what a SEQUENCE step adds. A
        /// typed one-off passes neither and reads exactly as it did before the
        /// command agent existed — one switch, so the latch mapping stays single-
        /// sourced no matter who dispatches it.</summary>
        void ApplyCommandIntent(CompanionCommandIntent intent, string prefix = null, string detail = null)
        {
            if (Input == null) return;
            string copy;
            var seconds = 2f;
            switch (intent)
            {
                case CompanionCommandIntent.FocusAttack:
                    // Follow drives A7 autonomy: the slot pursues the nearest target inside its
                    // leash instead of freezing in place, so "집중공격/싸워" actually chases.
                    Input.QueueCompanionRecall();
                    copy = "수호자: 집중공격 — 근접 표적 추격•교전";
                    seconds = 2.5f;
                    break;
                case CompanionCommandIntent.Defend:
                    // Hold pins the slot to its current spot and defends that zone (Amendment #3),
                    // a distinct tactic from Recall's return-to-side.
                    Input.QueueCompanionHold();
                    copy = "수호자: 방어 태세 — 현재 지점 사수";
                    seconds = 2.5f;
                    break;
                case CompanionCommandIntent.Recall:
                    Input.QueueCompanionRecall();
                    copy = "수호자: 복귀 — 곁으로";
                    break;
                case CompanionCommandIntent.PickupInfo:
                    copy = "수호자는 아이템을 주울 수 없습니다 — 직접 밟아 획득하세요";
                    seconds = 3f;
                    break;
                case CompanionCommandIntent.SkillBolt:
                    Input.QueueBolt(); copy = "균열 화살 시전"; seconds = 1.5f; break;
                case CompanionCommandIntent.SkillPulse:
                    Input.QueuePulse(); copy = "묘지 파동 시전"; seconds = 1.5f; break;
                case CompanionCommandIntent.SkillNova:
                    Input.QueueNova(); copy = "잿불 노바 시전"; seconds = 1.5f; break;
                case CompanionCommandIntent.SkillAegis:
                    Input.QueueWard(); copy = "공허 방패 시전"; seconds = 1.5f; break;
                case CompanionCommandIntent.SkillDash:
                    Input.QueueDash(); copy = "질주"; seconds = 1.5f; break;
                case CompanionCommandIntent.CompanionSkill:
                    // A8.3: one global order; each ready slot casts its OWN skill. A slot
                    // still on cooldown ignores it, so the copy promises "준비된" only.
                    Input.QueueCompanionSkill();
                    copy = "수호자: 준비된 고유 특기 발동";
                    break;
                default:
                    return;
            }
            if (!string.IsNullOrEmpty(detail)) copy += " — " + detail;
            ShowConsoleToast(string.IsNullOrEmpty(prefix) ? copy : prefix + copy, seconds);
        }

        void ShowConsoleToast(string message, float seconds)
        {
            if (_consoleToast == null) return;
            _consoleToast.text = message;
            _consoleToastTimer = seconds;
            var c = _consoleToast.color;
            _consoleToast.color = new Color(c.r, c.g, c.b, 1f);
        }

        void UpdateCommandConsole()
        {
            // W11 — browser IME events landed between frames; apply them before
            // anything reads the console text this tick.
            if (_consoleImeBridge) DrainConsoleIme();
            // Console keys are read OUTSIDE InputAdapter's TextInputActive gate —
            // otherwise Enter/ESC would be swallowed and the player trapped.
            // With the bridge live Unity is not capturing the keyboard at all,
            // so these polls simply never fire and Enter/ESC arrive as bridge
            // events instead.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (CommandConsoleOpen)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame) CloseCommandConsole(submit: false);
                    else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        CloseCommandConsole(submit: true);
                }
                else if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    && _dungeonRoot != null && _dungeonRoot.activeSelf
                    && (_gameOverPanel == null || !_gameOverPanel.activeSelf))
                {
                    OpenCommandConsole();
                }
            }
            // Sequence agent: at most one step signal per frame, applied to the
            // same latches a keystroke sets. Runs whether the console is open or
            // closed — a plan outlives the text field that started it.
            TickCommandAgent();
            if (_consoleToastTimer > 0f)
            {
                _consoleToastTimer -= Time.unscaledDeltaTime;   // survives slow-mo
                if (_consoleToastTimer <= 1f && _consoleToast != null)
                {
                    var c = _consoleToast.color;
                    _consoleToast.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(_consoleToastTimer));
                }
            }
        }

        // =================================================== dungeon HUD (v0.2) --
        GameObject _dungeonRoot;
        Image _xpFill;
        Text _levelText;
        Image[] _comboPips;
        Image[] _skillOverlays;         // bolt, pulse, nova(R), ward(F)
        CanvasGroup[] _skillGroups;
        Image[] _skillFrames;           // HUD atlas: ready-state gold rim swap
        Image _dashOverlay;
        Image _dashFrame;

        GameObject _bossBar;
        Image _bossFill;
        Text _bossName;
        Text _bossPhasePip;
        Image _extractRing;
        GameObject _extractRoot;
        Text _shieldText;
        GameObject _shieldPanel;        // HUD atlas: hidden until shield > 0

        GameObject _companionHoldButton, _companionRecallButton;
        // AMENDMENT #8: commanded signature cast. The label doubles as the cooldown
        // readout — the order is global, so ONE control covers every slot.
        GameObject _companionSkillButton;
        Text _companionSkillLabel;
        int _lastCompanionSkillTenths = int.MinValue;
        // Companion stance readout: names the live behavior so the command console's
        // FocusAttack/Defend/Recall orders read as three DISTINCT sim states, not three
        // toasts over the same motion. Backed only by CompanionBehavior + CompanionEngagedAt
        // (both real snapshot fields) — hold is indefinite in the sim, so there is no timer
        // ring to draw here.
        Text _companionStanceLabel;
        int _lastCompanionStanceKey = int.MinValue;

        // Room objective chip (dungeon-revival spec §"HUD must expose the current room
        // objective"): a contiguous route replaces the lobby return between rooms, so the
        // only place the player can still read "what does THIS room want" is the HUD.
        // Text comes from StageCatalog.RoomObjective; the boss phase recolors it.
        GameObject _roomObjectivePanel;
        Text _roomObjectiveText;
        int _lastRoomObjectiveKey = int.MinValue;

        int _lastLevel = -1, _lastCombo = -1, _lastBossPhase = -1;
        float _lastXpFraction = -1f, _lastBossFraction = -1f;
        int _lastShield = -1;
        static readonly float[] SkillMaxCooldowns = { 6.5f, 4f, 8f, 12f };
        static readonly float[] SkillCosts = { 25f, 30f, 45f, 30f };
        static Sprite _skillFrameNormalSprite, _skillFrameReadySprite;   // ready-state swap cache


        /// <summary>Dungeon combat HUD (spec §2, §7): XP, combo, 4 skills, dash,
        /// boss bar, extraction channel. Replaces the 2-card arena skill row.</summary>
        public void EnableDungeonUi(string bossDisplayName)
        {
            var root = (Transform)_safeRoot;
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
                new Vector2(0, 4), new Vector2(560, 10), new Color(0f, 0f, 0f, 0.6f), "hud-xp-bar-frame");

            _xpBackRect = xpBack.GetComponent<RectTransform>();
            _xpBackRect.pivot = new Vector2(0.5f, 0f);
            var xpFillObject = new GameObject("XpFill");
            xpFillObject.transform.SetParent(xpBack.transform, false);
            _xpFill = xpFillObject.AddComponent<Image>();
            _xpFill.color = new Color(0.56f, 0.91f, 1f);
            // Placeholder only — `hud-xp-bar-fill` is a centred medallion with a
            // 58.6% opaque x-extent, not an edge-to-edge bar fill, and
            // Image.Type.Filled trims to that. See Bar() for the measured table
            // of all five and what the art has to become before it can be used.
            AsFilled(_xpFill, Image.FillMethod.Horizontal);
            _xpFill.raycastTarget = false;   // decorative fill must not eat taps
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
                    new Vector2(-26 + i * 26, 102), new Vector2(20, 20),
                    new Color(1f, 1f, 1f, 0.14f));
                pip.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
                _comboPips[i] = pip.GetComponent<Image>();
                _comboPipRects[i] = pip.GetComponent<RectTransform>();
                // Simple, not Sliced: a round gem art shouldn't be
                // 9-slice-stretched onto a 20x20 square pip — it just
                // fills the square, same as any other icon Image. The
                // existing per-frame Color tint below keeps dimming/
                // lighting it exactly as before, now over real art.
                var pipGem = Resources.Load<Sprite>("Icons/hud-combo-pip-gem");
                if (pipGem != null) _comboPips[i].sprite = pipGem;

            }

            // --- skill row: dash + Q/E/R/F --------------------------------------
            _skillOverlays = new Image[4];
            _skillGroups = new CanvasGroup[4];
            _skillFrames = new Image[4];
            var dashCard = SkillCard(dungeonRoot, -232, "SHIFT", "질주",
                () => { if (Input != null) Input.QueueDash(); },
                out _dashOverlay, out _, out _dashFrame, "skill-dash");

            _dashCardRect = dashCard.GetComponent<RectTransform>();
            _dashCardRect.sizeDelta = new Vector2(110, 88);
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
                    actions[i], out _skillOverlays[i], out _skillGroups[i], out _skillFrames[i], icons[i]);

                _skillCardRects[i] = card.GetComponent<RectTransform>();
                _skillCardRects[i].sizeDelta = new Vector2(108, 88);
            }
            // 92 u is the 44 CSS px floor at the narrowest Phone scaler.
            _companionHoldButton = TextButton(dungeonRoot, new Vector2(1f, 0.5f),
                new Vector2(-16, 104), new Vector2(154, 92), "동료 대기 (G)", 16,
                () => { if (Input != null) Input.QueueCompanionHold(); });
            _companionRecallButton = TextButton(dungeonRoot, new Vector2(1f, 0.5f),
                new Vector2(-16, 4), new Vector2(154, 92), "동료 호출 (H)", 16,
                () => { if (Input != null) Input.QueueCompanionRecall(); });
            _companionSkillButton = TextButton(dungeonRoot, new Vector2(1f, 0.5f),
                new Vector2(-16, -96), new Vector2(154, 92), "동료 특기 (V)", 16,
                () => { if (Input != null) Input.QueueCompanionSkill(); });
            _companionSkillLabel = _companionSkillButton.GetComponentInChildren<Text>();
            _companionSkillLabel = _companionSkillButton.GetComponentInChildren<Text>();

            // Companion stance chip: a non-interactive readout above the order buttons so
            // the console/keys' FocusAttack/Defend/Recall map to a visible sim state. Right-
            // center anchored to ride with the companion button column; raycastTarget stays
            // false (Label default) so it never eats a tap the HudLayout contract guards.
            _companionStanceLabel = Label(dungeonRoot, 0, 0, 154, 24, "", 14, TextAnchor.MiddleCenter);
            var stanceRect = _companionStanceLabel.rectTransform;
            stanceRect.anchorMin = stanceRect.anchorMax = new Vector2(1f, 0.5f);
            stanceRect.pivot = new Vector2(1f, 0.5f);
            stanceRect.anchoredPosition = new Vector2(-16, 196);
            _companionStanceLabel.color = new Color(0.82f, 0.86f, 0.95f);
            _companionStanceLabel.gameObject.SetActive(false);   // no companion, no chip

            // --- room objective chip (top center, under the boss bar band) ------
            // The revived route is contiguous: the player never returns to the lobby
            // between rooms, so the room's own win condition has to stay legible on
            // screen. Parked at y -108 — below the boss bar's -58..-104 band — so it
            // never overlaps the bar when a boss spawns. Non-interactive by
            // construction (Panel raycast off + Label raycast off).
            _roomObjectivePanel = Panel(dungeonRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -108), new Vector2(520, 26), new Color(0.03f, 0.04f, 0.08f, 0.66f));
            _roomObjectiveText = Label(_roomObjectivePanel.transform, 0, 0, 520, 26, "", 14,
                TextAnchor.MiddleCenter);
            var objectiveTextRect = _roomObjectiveText.rectTransform;
            objectiveTextRect.anchorMin = Vector2.zero;
            objectiveTextRect.anchorMax = Vector2.one;
            objectiveTextRect.sizeDelta = Vector2.zero;
            objectiveTextRect.anchoredPosition = Vector2.zero;
            _roomObjectivePanel.SetActive(false);   // no objective, no chip



            // --- shield readout (backed panel, hidden until shield > 0) ---------
            _shieldPanel = Panel(dungeonRoot, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16, -98), new Vector2(190, 28), new Color(0.02f, 0.05f, 0.08f, 0.72f),
                "hud-shield-readout-frame");
            var shieldPanelRect = _shieldPanel.GetComponent<RectTransform>();
            shieldPanelRect.pivot = new Vector2(0f, 1f);
            _shieldText = Label(_shieldPanel.transform, 10, -2, 170, 24, "", 15, TextAnchor.MiddleLeft);
            _shieldText.color = new Color(0.56f, 0.85f, 1f);
            _shieldPanel.SetActive(false);   // matches boss-bar/extraction-ring: no chip until it has a value


            // --- boss bar (top center, hidden until boss) --------------------------
            _bossBar = Panel(dungeonRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -58), new Vector2(520, 46), new Color(0.05f, 0.02f, 0.05f, 0.8f),
                "hud-boss-bar-frame");

            _bossBarRect = _bossBar.GetComponent<RectTransform>();
            _bossBarRect.pivot = new Vector2(0.5f, 1f);
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
            // Placeholder only — `hud-boss-bar-fill` has a 32.4% opaque
            // x-extent. See Bar() for the measured table.
            AsFilled(_bossFill, Image.FillMethod.Horizontal);
            _bossFill.raycastTarget = false;
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
                new Vector2(6, 4), new Vector2(108, 8), new Color(0f, 0f, 0f, 0.6f), "hud-extraction-ring-frame");

            extractBack.GetComponent<RectTransform>().pivot = Vector2.zero;
            var extractFillObject = new GameObject("ExtractFill");
            extractFillObject.transform.SetParent(extractBack.transform, false);
            _extractRing = extractFillObject.AddComponent<Image>();
            _extractRing.color = new Color(0.62f, 0.95f, 0.88f);
            // Placeholder only — `hud-extraction-ring-fill` has a 24.6% opaque
            // x-extent. See Bar() for the measured table.
            AsFilled(_extractRing, Image.FillMethod.Horizontal);
            _extractRing.raycastTarget = false;
            var extractRect = extractFillObject.GetComponent<RectTransform>();
            extractRect.anchorMin = Vector2.zero;
            extractRect.anchorMax = Vector2.one;
            extractRect.offsetMin = new Vector2(1, 1);
            extractRect.offsetMax = new Vector2(-1, -1);
            _extractRoot.SetActive(false);

            _shieldRect = shieldPanelRect;

            ApplyLayoutTier();          // grade the fresh dungeon surfaces
            SyncTouchModeSurfaces();    // dash touch button goes live
            ShowRotateHintIfPortrait(); // spec #14: one-time landscape nudge
        }

        // --- AMENDMENT #10 surge / trial banner ---------------------------------
        Text _surgeBanner;
        Text _trialBanner;
        string _lastSurgeText = "";
        string _lastTrialText = "";
        static readonly Color PerilColor = new Color(1f, 0.42f, 0.32f);
        static readonly Color SurgeColor = new Color(1f, 0.82f, 0.38f);
        bool _trialStatsHidden;
        int _lastTrialHits;

        /// <summary>
        /// Surge windows and the trial clock (AMENDMENT #10). Text only, reusing
        /// the toast band — no new geometry, so the audited HUD rects the layout
        /// tests froze are untouched.
        ///
        /// The banner shows for every player, including one with no sigils: the
        /// window is a readable beat first and a mechanical payoff second, and a
        /// player who cannot yet use it should still learn to recognise it.
        /// </summary>
        public void SyncSurge(float perilRemaining, float surgeRemaining,
                              float trialElapsed, int trialHits, bool trialLive)
        {
            EnsureSurgeBanners();
            if (_surgeBanner == null) return;

            var surgeText = perilRemaining > 0f
                ? $"위기 {perilRemaining:0.0}"
                : (surgeRemaining > 0f ? $"기세 {surgeRemaining:0.0}" : "");
            if (surgeText != _lastSurgeText)
            {
                _lastSurgeText = surgeText;
                _surgeBanner.text = surgeText;
                _surgeBanner.color = perilRemaining > 0f ? PerilColor : SurgeColor;
                _surgeBanner.gameObject.SetActive(surgeText.Length > 0);
            }

            if (!trialLive)
            {
                if (_lastTrialText.Length > 0)
                {
                    _lastTrialText = "";
                    _trialBanner.gameObject.SetActive(false);
                }
                return;
            }

            _lastTrialHits = trialHits;
            var left = Mathf.Max(0f, HackSpec.TrainingSeconds - trialElapsed);
            var trialText = $"남은 {left:0} • 피격 {trialHits}";
            if (trialText != _lastTrialText)
            {
                _lastTrialText = trialText;
                _trialBanner.text = trialText;
                _trialBanner.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Latches the run's mode ONCE at start (AMENDMENT #10). Separate from
        /// the per-frame sync on purpose: the clear ceremony reads this AFTER
        /// the run has ended, so a flag that decays with the run would hand the
        /// trial the dungeon's "구역 정화 • 점수 0 • 유물 0" wording — a line
        /// that reads like a failure for a mode with no score by design.
        ///
        /// A trial has no waves, no spawns and no economy, so 웨이브 / 유물 / 적
        /// are structurally frozen at 1 / 0 / 0 and are hidden rather than shown
        /// as broken values.
        /// </summary>
        public void SetTrialMode(bool training)
        {
            if (training == _trialStatsHidden) return;
            _trialStatsHidden = training;
            if (_waveText != null) _waveText.gameObject.SetActive(!training);
            if (_relicText != null) _relicText.gameObject.SetActive(!training);
            if (_enemyText != null) _enemyText.gameObject.SetActive(!training);
        }

        // --- AMENDMENT #11 UI: in-run difficulty badge -----------------------
        //
        // Why this exists: §16 deliberately LOCKS the tier at run creation so a run
        // reproduces from (config, input sequence). A locked choice the player cannot
        // see is a trap — they pick 악몽 in the lobby, die, and have no on-screen proof
        // of which ruleset killed them. The lobby button is the only place the tier
        // was ever stated, and it is three screens away by then.

        /// <summary>Stats panel height. The difficulty row is a real 5th row, so the
        /// panel grows to hold it and shrinks back when the run is baseline — an
        /// always-tall panel would show 24 u of empty background on every Normal run,
        /// which is most of them.</summary>
        float StatsPanelHeight => _difficultyBadgeShown ? 132f : 108f;

        /// <summary>Top inset of the mute button: it sits 8 u under the stats panel,
        /// so it has to move with the panel rather than repeat a literal.</summary>
        float MuteTopOffset => 16f + StatsPanelHeight + 8f;

        /// <summary>
        /// Baseline runs show nothing. <see cref="Sim.Difficulty.Normal"/> is the
        /// pre-amendment ruleset and is what arena and prologue always run, so a
        /// "난이도 보통" row there would be permanent noise that says nothing. The badge
        /// is a deviation marker: it appears exactly when the rules are not the
        /// default ones.
        /// </summary>
        internal static bool ShowsDifficultyBadge(Sim.Difficulty difficulty)
            => difficulty != Sim.Difficulty.Normal;

        /// <summary>
        /// One HUD line for a tier. Deliberately shorter than the lobby's three-line
        /// spec text (<see cref="LobbyView.DifficultyLabelText"/>): in-run this must
        /// answer "which ruleset am I in" at a glance, not re-teach the multipliers.
        /// The 협동 marker is the one mechanical fact worth carrying, because a group-AI
        /// tier is the one that makes enemies circle instead of charge.
        /// </summary>
        internal static string DifficultyBadgeText(Sim.Difficulty difficulty)
        {
            var name = LobbyView.DifficultyName(difficulty);
            return Sim.DifficultySpec.For(difficulty).GroupAi
                ? $"난이도 {name} • 협동"
                : $"난이도 {name}";
        }

        /// <summary>
        /// Result-screen suffix. A score earned on 악몽 and one earned on 입문 are not
        /// the same number, so the tier has to ride along with it or the run summary
        /// misreports itself. Empty on the baseline tier, which keeps every existing
        /// Normal-run result line byte-identical.
        /// </summary>
        internal static string DifficultyResultSuffix(Sim.Difficulty difficulty)
            => ShowsDifficultyBadge(difficulty)
                ? " • " + LobbyView.DifficultyName(difficulty)
                : string.Empty;

        /// <summary>
        /// Latches the run's tier ONCE at start, like <see cref="SetTrialMode"/>: the
        /// sim cannot change difficulty mid-run, so polling it per frame would be a
        /// lie about how the value behaves.
        /// </summary>
        public void SetRunDifficulty(Sim.Difficulty difficulty)
        {
            _runDifficulty = difficulty;
            var show = ShowsDifficultyBadge(difficulty);
            if (_difficultyText != null)
            {
                _difficultyText.text = show ? DifficultyBadgeText(difficulty) : string.Empty;
                // Gold reads as "this run is off-baseline" everywhere else in the HUD.
                _difficultyText.color = new Color(1f, 0.83f, 0.45f);
                _difficultyText.gameObject.SetActive(show);
            }
            if (show == _difficultyBadgeShown) return;
            _difficultyBadgeShown = show;
            // The panel and the mute button below it both resize; re-run the tier pass
            // rather than duplicating its geometry. Guarded because Build() can latch a
            // tier before the first ApplyLayout has supplied screen geometry.
            if (_statsRect != null && _lastScreenWidth > 0 && _lastScreenHeight > 0)
                ApplyLayoutTier();
        }

        /// <summary>The tier this run was created with (test/diagnostic seam).</summary>
        internal Sim.Difficulty RunDifficulty => _runDifficulty;

        /// <summary>Geometry seams for the layout regression tests: the stats panel and
        /// the mute button move together when the badge row appears.</summary>
        internal RectTransform StatsRectForTest => _statsRect;
        internal RectTransform MuteRectForTest => _muteRect;
        internal Text DifficultyLabelForTest => _difficultyText;


        void EnsureSurgeBanners()
        {
            if (_surgeBanner != null || _safeRoot == null) return;
            // Parent to the SAFE ROOT, not this MonoBehaviour's transform. The
            // HUD canvas is a child object built in EnsureBuilt, so a label hung
            // on the component's own transform sits OUTSIDE the canvas and never
            // draws — exactly what the browser showed: the banner code ran every
            // frame and nothing appeared. No test caught it; only looking did.
            var root = _safeRoot.transform;
            _surgeBanner = Label(root, 0f, 0f, 300f, 30f, "", 22, TextAnchor.MiddleCenter);
            _surgeBanner.name = "SurgeBanner";
            var surgeRect = _surgeBanner.rectTransform;
            surgeRect.anchorMin = new Vector2(0.5f, 1f);
            surgeRect.anchorMax = new Vector2(0.5f, 1f);
            surgeRect.pivot = new Vector2(0.5f, 1f);
            // AMENDMENT #9 (entry 15): y was -150, which put the surge rect
            // [-180,-150] ENTIRELY inside the wave banner [-200,-140] — 100%
            // containment, 9000 u². Both fire on the same beats (a surge opens
            // on a kill threshold and kills are what end waves), so the banner
            // announcing 위기/기세 sat behind the one announcing the wave.
            // 위기/기세 are two of the 23 things this amendment must teach, so
            // the collision had to clear before guidance could point at them.
            surgeRect.anchoredPosition = new Vector2(0f, -208f);
            surgeRect.sizeDelta = new Vector2(300f, 30f);
            _surgeBanner.fontStyle = FontStyle.Bold;
            _surgeBanner.gameObject.SetActive(false);

            _trialBanner = Label(root, 0f, 0f, 300f, 24f, "", 18, TextAnchor.MiddleCenter);
            _trialBanner.name = "TrialBanner";
            var trialRect = _trialBanner.rectTransform;
            trialRect.anchorMin = new Vector2(0.5f, 1f);
            trialRect.anchorMax = new Vector2(0.5f, 1f);
            trialRect.pivot = new Vector2(0.5f, 1f);
            trialRect.anchoredPosition = new Vector2(0f, -60f);
            trialRect.sizeDelta = new Vector2(300f, 24f);
            _trialBanner.color = new Color(0.86f, 0.9f, 1f);
            _trialBanner.gameObject.SetActive(false);
        }

        /// <summary>Per-frame dungeon sync (IHackSnapshot surface, primitives only).</summary>
        /// <summary>
        /// AMENDMENT #8 (A8.5) readout. Primitives only, like every other HUD sync: the
        /// caller reduces the per-slot cooldowns to the SOONEST one, because the command is
        /// global and the player only needs to know when SOMETHING will answer it. Relabels
        /// at tenth-of-a-second granularity so the text is not rebuilt every frame.
        /// </summary>
        public void SyncCompanionSkill(int slots, float soonestCooldown, bool anyReady)
        {
            if (_companionSkillLabel == null) return;
            var tenths = anyReady ? 0 : Mathf.Max(1, Mathf.CeilToInt(soonestCooldown * 10f));
            if (slots <= 0 || tenths == _lastCompanionSkillTenths) return;
            _lastCompanionSkillTenths = tenths;
            _companionSkillLabel.text = anyReady
                ? "동료 특기 (V)"
                : $"동료 특기 {tenths / 10f:0.0}s";
            _companionSkillLabel.color = anyReady
                ? new Color(1f, 0.86f, 0.5f)
                : new Color(0.72f, 0.72f, 0.78f);
        }


        /// <summary>
        /// Companion stance readout. <paramref name="slots"/> &lt;= 0 hides the chip (no
        /// companion in the run). Otherwise it names the live behavior — Hold = "방어 태세"
        /// (pinned zone defense), Follow = "추격" while any slot is engaged else "호위" — so the
        /// three console/keys orders read as distinct sim states. Keyed on (slots, behavior,
        /// engaged) so the text is not rebuilt every frame.
        /// </summary>
        public void SyncCompanionStance(int slots, CompanionBehavior behavior, bool engaged)
        {
            if (_companionStanceLabel == null) return;
            var active = slots > 0;
            if (_companionStanceLabel.gameObject.activeSelf != active)
                _companionStanceLabel.gameObject.SetActive(active);
            if (!active) return;

            // 0 = defend(hold), 1 = pursue(follow+engaged), 2 = escort(follow idle).
            var key = behavior == CompanionBehavior.Hold ? 0 : engaged ? 1 : 2;
            if (key == _lastCompanionStanceKey) return;
            _lastCompanionStanceKey = key;
            switch (key)
            {
                case 0:
                    _companionStanceLabel.text = "동료: 방어 태세";
                    _companionStanceLabel.color = new Color(0.56f, 0.85f, 1f);
                    break;
                case 1:
                    _companionStanceLabel.text = "동료: 추격 교전";
                    _companionStanceLabel.color = new Color(1f, 0.62f, 0.4f);
                    break;
                default:
                    _companionStanceLabel.text = "동료: 호위";
                    _companionStanceLabel.color = new Color(0.82f, 0.86f, 0.95f);
                    break;
            }
        }

        /// <summary>
        /// Room objective readout (dungeon-revival spec). An empty/null
        /// <paramref name="objective"/> hides the chip entirely — that is how arena,
        /// prologue and unknown stage ids opt out rather than showing a stale line
        /// from the previous room. While the room boss is alive the same objective
        /// is re-framed as the final beat and recolored amber, so the player sees
        /// the room's win condition change shape instead of a second HUD element
        /// appearing. Keyed on (objective, bossAlive) so the text is not rebuilt
        /// every frame.
        /// </summary>
        public void SyncRoomObjective(string objective, bool bossAlive)
        {
            if (_roomObjectivePanel == null || _roomObjectiveText == null) return;
            var active = !string.IsNullOrEmpty(objective);
            if (_roomObjectivePanel.activeSelf != active)
                _roomObjectivePanel.SetActive(active);
            if (!active)
            {
                _lastRoomObjectiveKey = int.MinValue;
                return;
            }

            var key = objective.GetHashCode() * 2 + (bossAlive ? 1 : 0);
            if (key == _lastRoomObjectiveKey) return;
            _lastRoomObjectiveKey = key;
            if (bossAlive)
            {
                _roomObjectiveText.text = "최종 목표 • " + objective;
                _roomObjectiveText.color = new Color(1f, 0.83f, 0.45f);
            }
            else
            {
                _roomObjectiveText.text = "목표 • " + objective;
                _roomObjectiveText.color = new Color(0.82f, 0.88f, 0.96f);
            }
        }

        /// <summary>
        /// QA/test seam for the room objective chip: the line the player can
        /// actually read, or "" while the chip is hidden.
        /// </summary>
        public string RoomObjectiveReadout =>
            _roomObjectivePanel != null && _roomObjectivePanel.activeSelf && _roomObjectiveText != null
                ? _roomObjectiveText.text
                : "";



        public void SyncDungeon(
            int level, int xp, int xpNext, int comboIndex,
            float dashCooldown, IReadOnlyList<float> skillCooldowns, float shield,
            float extractionProgress, float extractionTarget,
            float bossHp, float bossMaxHp, int bossPhase, float charge)
        {
            if (_dungeonRoot == null) return;

            if (level != _lastLevel)
            {
                // Level-up ceremony (#19): skip the first sync (0 -> N seed).
                if (_lastLevel >= 1 && level > _lastLevel)
                {
                    _levelPunchTimer = 0.35f;
                    _xpFlashTimer = 0.4f;
                    _levelToast.text = "레벨 업! 피해 +4% • 최대 체력 +6";
                    _levelToastTimer = 1.4f;
                }
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
                var previousCombo = _lastCombo;
                _lastCombo = comboIndex;
                for (var i = 0; i < 3; i++)
                    _comboPips[i].color = i < comboIndex
                        ? new Color(1f, 0.83f, 0.45f, 0.95f)
                        : new Color(1f, 1f, 1f, 0.14f);
                if (previousCombo >= 0 && comboIndex > previousCombo && comboIndex <= 3)
                    _pipPunchTimers[comboIndex - 1] = ViewPrefs.TimeEffectsAllowed ? 0.2f : 0f;
            }

            _dashOverlay.fillAmount = Mathf.Clamp01(dashCooldown / 1.6f);
            ApplySkillCardReadyState(_dashFrame, dashCooldown <= 0.0001f);
            if (skillCooldowns != null && skillCooldowns.Count >= 4)
            {
                for (var i = 0; i < 4; i++)
                {
                    _skillOverlays[i].fillAmount = Mathf.Clamp01(skillCooldowns[i] / SkillMaxCooldowns[i]);
                    _skillGroups[i].alpha = charge >= SkillCosts[i] ? 1f : 0.45f;
                    ApplySkillCardReadyState(_skillFrames[i], skillCooldowns[i] <= 0.0001f);
                }
            }


            var shieldShown = shield > 0f ? Mathf.CeilToInt(shield) : 0;
            if (shieldShown != _lastShield)
            {
                _lastShield = shieldShown;
                _shieldText.text = shieldShown > 0 ? $"방패 {shieldShown}" : "";
                if (_shieldPanel != null) _shieldPanel.SetActive(shieldShown > 0);
            }


            var bossVisible = bossMaxHp > 0f && bossHp > 0f;
            if (_bossBar.activeSelf != bossVisible)
            {
                _bossBar.SetActive(bossVisible);
                if (bossVisible)
                {
                    _bossRevealTimer = ViewPrefs.TimeEffectsAllowed ? 0.4f : 0f;
                    _bossBarRect.anchoredPosition = new Vector2(0f,
                        ViewPrefs.TimeEffectsAllowed ? -98f : -58f);
                }
            }
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
                    // S8-a: three phases now exist, so the pip must name all
                    // three — a P3 boss that still reads "PHASE II" hides the
                    // biggest difficulty signal in the fight.
                    _bossPhasePip.text = bossPhase >= 3 ? "PHASE III"
                        : bossPhase >= 2 ? "PHASE II" : "PHASE I";
                    _bossFill.color = bossPhase >= 3
                        ? new Color(1f, 0.24f, 0.55f)      // P3: violet-red, distinct at a glance
                        : bossPhase >= 2
                            ? new Color(0.95f, 0.3f, 0.32f)
                            : new Color(1f, 0.55f, 0.26f);
                    _bossPhasePunchTimer = ViewPrefs.TimeEffectsAllowed ? 0.1f : 0f;
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

        /// <summary>Full-stretch raycast-off overlay image, initially invisible.</summary>
        static Image Overlay(Transform parent, Texture2D texture, string name)
        {
            var overlayObject = new GameObject(name);
            overlayObject.transform.SetParent(parent, false);
            var image = overlayObject.AddComponent<Image>();
            image.sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;   // fully off when alpha 0 - no overdraw
            var rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }
        Image Letterbox(Transform parent, bool top)
        {
            var bar = Panel(parent,
                top ? new Vector2(0f, 1f) : Vector2.zero,
                top ? new Vector2(1f, 1f) : new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 90f), Color.black);
            var rect = bar.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.anchoredPosition = new Vector2(0f, top ? 90f : -90f);
            var image = bar.GetComponent<Image>();
            image.enabled = false;
            return image;
        }

        /// <summary>128px radial gradient: transparent center, alpha ~0.85 edges.</summary>
        static Texture2D MakeRadialTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - size * 0.5f) / (size * 0.5f);
                    var dy = (y - size * 0.5f) / (size * 0.5f);
                    var edge = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    // Transparent until 45% radius, then ramp — a curve from
                    // r=0 tints the WHOLE frame (flood, not vignette).
                    var ramp = Mathf.InverseLerp(0.45f, 1f, edge);
                    var alpha = Mathf.SmoothStep(0f, 1f, ramp) * 0.85f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // ------------------------------------------------------------- factory --
        GameObject Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                         Vector2 anchored, Vector2 size, Color color, string frameSpriteId = null)
        {
            return Panel(parent, anchorMin, anchorMax, anchored, size, color, out _, frameSpriteId);
        }

        /// <summary>Overload for callers that need the frame overlay's own
        /// Image (e.g. SkillCard's ready-state art swap) — every other call
        /// site keeps using the discarding overload above unchanged.</summary>
        GameObject Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                         Vector2 anchored, Vector2 size, Color color, out Image frameImage,
                         string frameSpriteId = null)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            // Raycast OFF by default (mobile spec: invisible or decorative
            // rects must never eat taps — the joystick corner is dense with
            // labels/fills). Interactive surfaces (TextButton, SkillCard,
            // touch controls) and modal backdrops re-enable it explicitly.
            image.raycastTarget = false;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
            frameImage = ApplyFrameOverlay(panel.transform, frameSpriteId);
            return panel;
        }

        /// <summary>Additive decorative chrome (HUD atlas, see
        /// _workspace/current/design/hud-atlas/): a full-stretch child drawn
        /// ON TOP of the panel's own flat-color Image, never a replacement
        /// for it. That keeps every existing translucent track/backdrop tint
        /// exactly as before when the sprite is absent AND when a fill bar
        /// sits on top of a partially-hollow frame — the flat base still
        /// shows through as the "empty" track. No-op when the sprite hasn't
        /// been generated/imported yet. Returns the created Image (or null)
        /// so callers that need to swap its sprite later (skill-card
        /// ready-state) can keep a reference.
        ///
        /// xp-bar-frame and extraction-ring-frame have no usable 9-slice
        /// border (real on-screen height 8-10 u, see IconImportPipeline's
        /// TryGetFrameBorder note) so they render Type.Simple — a flat
        /// stretch with no border math and therefore no minimum size.</summary>
        static Image ApplyFrameOverlay(Transform parent, string frameSpriteId)
        {
            if (frameSpriteId == null) return null;
            var frame = Resources.Load<Sprite>("Icons/" + frameSpriteId);
            if (frame == null) return null;   // missing sprite keeps the flat-color fallback
            var frameObject = new GameObject("Frame");
            frameObject.transform.SetParent(parent, false);
            var frameImage = frameObject.AddComponent<Image>();
            frameImage.sprite = frame;
            frameImage.type = frameSpriteId == "hud-xp-bar-frame" || frameSpriteId == "hud-extraction-ring-frame"
                ? Image.Type.Simple
                : Image.Type.Sliced;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            var frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            return frameImage;
        }

        /// <summary>
        /// 1x1 opaque white sprite shared by every generated Image. uGUI's
        /// <c>Image.OnPopulateMesh</c> bails to the plain <c>Graphic</c> full-rect
        /// quad when <c>activeSprite</c> is null — the <c>type</c>/<c>fillAmount</c>
        /// switch is never reached. A Filled Image with no sprite therefore
        /// renders permanently full no matter what fillAmount is written to it,
        /// which is exactly how the 체력/기름 meters lost their drain.
        /// </summary>
        static Sprite _fillSprite;

        static Sprite FillSprite()
        {
            if (_fillSprite != null) return _fillSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "HudFillTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                // Procedural and cached in a static: without DontSave a domain
                // reload or playmode exit destroys it while the Images that
                // reference it survive, leaving them sprite-less again — the
                // exact null-activeSprite state this whole helper exists to
                // prevent, but only after a reload, which is worse than never.
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            _fillSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            _fillSprite.name = "HudFillSprite";
            _fillSprite.hideFlags = HideFlags.HideAndDontSave;
            return _fillSprite;
        }

        /// <summary>
        /// The ONLY sanctioned way to make a Filled Image in this HUD.
        ///
        /// Assigning the sprite is not decoration — it is what makes fillAmount
        /// reach the mesh at all (see <see cref="FillSprite"/>). Seven gauges
        /// shipped without it — health, oil, XP, boss HP, extraction ring, skill
        /// cooldowns, charge — every one rendering permanently full while Sync()
        /// dutifully wrote the correct fillAmount into a field nothing read.
        /// Caught in a browser smoke: the death panel showed 체력 0 over a bar
        /// measured at 98% lit pixels (CLAUDE.md §4k, which names this method).
        ///
        /// Callers that also want atlas art assign it AFTER this returns. The
        /// first line here overwrites `sprite`, so the reverse order silently
        /// discards the art and leaves a working-but-flat gauge.
        /// </summary>
        static void AsFilled(Image image, Image.FillMethod method, int origin = 0)
        {
            image.sprite = FillSprite();
            image.type = Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = origin;
            image.preserveAspect = false;
        }


        Image Bar(Transform parent, float x, float y, float width, float height,
                  Color fillColor, out Text valueText, string label,
                  string frameSpriteId = null, string fillSpriteId = null)
        {
            // The frame overlay is added AFTER Fill below (not passed here)
            // so it sits on top of the fill as a bezel instead of being
            // covered by it — see ApplyFrameOverlay's ordering note.
            var back = Panel(parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, y), new Vector2(width, height), new Color(0f, 0f, 0f, 0.55f));
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(back.transform, false);
            var fill = fillObject.AddComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;
            var rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
            // The 1x1 placeholder is the fill, and the `hud-*-bar-fill` atlas
            // art is deliberately NOT applied. Measured, not assumed:
            //
            //   hud-hp-bar-fill        256x64  opaque x-extent 22.7%
            //   hud-oil-bar-fill       256x64  opaque x-extent 12.9%
            //   hud-xp-bar-fill        256x64  opaque x-extent 58.6%
            //   hud-boss-bar-fill      256x38  opaque x-extent 32.4%
            //   hud-extraction-ring    256x64  opaque x-extent 24.6%
            //
            // They are centred medallions on transparent canvases, not
            // edge-to-edge bar fills. Image.Type.Filled trims to the sprite's
            // opaque rect, so a full health bar renders as a blob across the
            // middle 22.7% — HudLayoutTests measured exactly 0.242 the moment
            // this code applied them.
            //
            // Main applied them and shipped correct bars anyway, because main
            // called the fill helper AFTER the Resources.Load and the helper's
            // first line is `image.sprite = FillSprite()`. The art was being
            // discarded on every bar. The ordering looked like the bug; it was
            // the only thing holding the gauges together, and "fixing" it is
            // what surfaced the real one. §4k's family again: the value was
            // right and nothing read it, here the read is right and the value
            // is unusable.
            //
            // Reinstate this when the fill art is regenerated edge-to-edge (a
            // horizontal gradient spanning the full 256, no transparent
            // margins). The frame sprites are unaffected and still applied.
            AsFilled(fill, Image.FillMethod.Horizontal);
            // Sibling order: back (flat track) -> Fill -> Frame -> Label, so
            // the ornate border bezel draws over the fill's edges but the
            // readout text still draws over the bezel and stays legible.
            ApplyFrameOverlay(back.transform, frameSpriteId);
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
            text.raycastTarget = false;   // labels never intercept pointer events
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
                              string iconId = null)
        {
            var buttonObject = Panel(parent, anchor, anchor, anchored, size,
                new Color(0.16f, 0.13f, 0.24f, 0.9f));
            buttonObject.GetComponent<Image>().raycastTarget = true;   // Button hit surface
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
            if (Audio != null) button.onClick.AddListener(Audio.PlayClick);   // W12

            // AMENDMENT #10 — optional glyph, and the label STAYS.
            //
            // Icon-only was considered and rejected for 포기 specifically: one
            // mis-tap costs the whole run, and a door-with-an-arrow is not worth
            // a run to a player who reads it wrong. The icon is recognition; the
            // word is confirmation. Button size does not change, so the touch
            // ratchet table in HudLayoutTests does not move.
            var textInset = 0f;
            if (iconId != null)
            {
                var sprite = Resources.Load<Sprite>("Icons/" + iconId);
                if (sprite != null)   // missing sprite would render a white quad
                {
                    var square = size.y >= size.x - 1f;   // phone tier is 92x92
                    var side = square ? Mathf.Min(40f, size.x - 8f) : Mathf.Min(24f, size.y - 8f);
                    var iconObject = new GameObject("Icon");
                    iconObject.transform.SetParent(buttonObject.transform, false);
                    var icon = iconObject.AddComponent<Image>();
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    var iconRect = iconObject.GetComponent<RectTransform>();
                    iconRect.sizeDelta = new Vector2(side, side);
                    if (square)
                    {
                        // Stacked: glyph over the word.
                        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                        iconRect.pivot = new Vector2(0.5f, 1f);
                        iconRect.anchoredPosition = new Vector2(0f, -8f);
                        textInset = -(side + 8f);   // shove the label below the glyph
                    }
                    else
                    {
                        // Inline: glyph left, word in the remaining width.
                        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                        iconRect.pivot = new Vector2(0f, 0.5f);
                        iconRect.anchoredPosition = new Vector2(6f, 0f);
                        textInset = side + 6f;
                    }
                }
            }
            var text = Label(buttonObject.transform, 0, 0, size.x, size.y, label, fontSize, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            if (textInset > 0f)        // inline: inset the left edge
                rect.offsetMin = new Vector2(textInset, rect.offsetMin.y);
            else if (textInset < 0f)   // stacked: inset the top edge
                rect.offsetMax = new Vector2(rect.offsetMax.x, textInset);
            return buttonObject;
        }

        GameObject SkillCard(Transform parent, float offsetX, string key, string label,
                             UnityEngine.Events.UnityAction onClick,
                             out Image cooldownOverlay, out CanvasGroup group,
                             out Image frameImage, string iconId = null)
        {
            var card = Panel(parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(offsetX, 18), new Vector2(150, 88), new Color(0.1f, 0.08f, 0.18f, 0.85f),
                out frameImage, "hud-skill-card-frame");


            card.GetComponent<Image>().raycastTarget = true;   // Button hit surface
            var rect = card.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            group = card.AddComponent<CanvasGroup>();
            var button = card.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            if (Audio != null) button.onClick.AddListener(Audio.PlayClick);   // W12
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
                    icon.color = new Color(1f, 1f, 1f, 0.55f);  // primary read now (label dropped)
                    var iconRect = iconObject.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(0f, -8f);
                    iconRect.sizeDelta = new Vector2(48f, 48f);
                }
            }
            // §U1 compact slot: keycap top-left + icon as the primary read;
            // the label row is gone (names live in prologue hints/tooltips).
            var keyText = Label(card.transform, 0, -4, 150, 22, key, 17, TextAnchor.MiddleCenter);
            keyText.color = new Color(1f, 0.83f, 0.45f);

            var overlayObject = new GameObject("Cooldown");
            overlayObject.transform.SetParent(card.transform, false);
            cooldownOverlay = overlayObject.AddComponent<Image>();
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            AsFilled(cooldownOverlay, Image.FillMethod.Vertical,
                (int)Image.OriginVertical.Top);
            cooldownOverlay.raycastTarget = false;
            var overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            return card;
        }

        /// <summary>Test seam: batchmode has no Touchscreen device, so tests
        /// force-build the touch surfaces Build() gates on hardware.</summary>
        internal void ForceTouchControlsForTest()
        {
            if (!_touchActive) BuildTouchControls(_safeRoot);
            SyncTouchModeSurfaces();
        }

        /// <summary>Test seams for the left column below the meters. Four
        /// functions write this band (ApplyLayoutTier :469, ApplyDungeonTier
        /// :587, ApplyEquipPlacement :627, EnsureAbandonSurfaces :1374) and
        /// arithmetic done by hand against build coordinates got one of the
        /// overlaps wrong — the shield is read at its build y unless you
        /// notice ApplyDungeonTier moves it. These expose the RESOLVED rects
        /// so the audit measures what ships.</summary>
        internal RectTransform AbandonRectForTest =>
            _abandonButton == null ? null : (RectTransform)_abandonButton.transform;
        // StatsRectForTest lives at :2634 with the badge-row note. Main added it
        // there while this lane added it here; one had to go and the documented
        // one stays.
        internal RectTransform ShieldRectForTest => _shieldRect;
        internal RectTransform EquipRectForTest => _equipRect;
        /// <summary>Whether the abandon modal is on screen. Distinct from the
        /// pause predicate: GuidancePaused ORs the codex in, so it answers
        /// "is the run held" and cannot answer "which surface is holding it".
        /// Cycle-7 needed the second question and had only the first.</summary>
        internal bool AbandonModalActiveForTest =>
            _abandonModal != null && _abandonModal.activeSelf;
        internal void OpenAbandonModalForTest() => OpenAbandonModal();
        /// <summary>
        /// Whether the touch combat controls are meant to be up.
        ///
        /// Exposed because the ORDER of two correct statements is load-bearing
        /// and nothing could see it. OpenCodex calls CloseAbandonModal (which
        /// sets this TRUE) and then sets it FALSE; the end state is right, but
        /// swapping those two lines leaves the joystick live under a panel that
        /// has frozen the sim. SyncTouchModeSurfaces applies the value on the
        /// spot, so the failure is visible on screen and invisible to every
        /// assertion that only reads the final pause state.
        /// </summary>
        internal bool TouchCombatControlsVisibleForTest => _touchCombatControlsVisible;

        /// <summary>Test seam (§U1): the non-interactive dungeon readouts the
        /// skill row must never cover — InteractiveRects() only sees pointer
        /// handlers, so card×readout overlap needs its own assertion set.</summary>
        internal void CollectDungeonReadoutRectsForTest(System.Collections.Generic.List<RectTransform> into)
        {
            if (_xpBackRect != null) into.Add(_xpBackRect);
            if (_levelText != null) into.Add(_levelText.rectTransform);
            for (var i = 0; i < _comboPipRects.Length; i++)
                if (_comboPipRects[i] != null) into.Add(_comboPipRects[i]);
            if (_shieldRect != null) into.Add(_shieldRect);
            if (_speakerLine != null) into.Add(_speakerLine.rectTransform);
        }

        /// <summary>Test seam (§U1): the interactive skill-row rects (4 skills
        /// + dash) for card×readout overlap grading at any tier.</summary>
        internal void CollectSkillRowRectsForTest(System.Collections.Generic.List<RectTransform> into)
        {
            for (var i = 0; i < _skillCardRects.Length; i++)
                if (_skillCardRects[i] != null) into.Add(_skillCardRects[i]);
            if (_dashCardRect != null) into.Add(_dashCardRect);
        }

        void BuildTouchControls(Transform root)
        {
            // Left: floating virtual joystick (mobile spec #7 — replaces the
            // D-pad; arbitrary-angle movement matters because standoff kiting
            // at range 160 vs 76 is the core skill). Catch panel spans the
            // lower-left corner; the base re-centers on press, the nub tracks
            // the drag. Pushed to the BACK of the sibling order so every HUD
            // button (later sibling = topmost raycast) wins over the catch
            // surface.
            //
            // Touch-target floor (spec #6): CSS px per canvas unit equals the
            // CanvasScaler factor divided by devicePixelRatio, and with
            // ScaleWithScreenSize that quotient is DPR-invariant — it is the
            // scale factor of the CSS viewport itself. Worst measured case
            // (390 CSS width portrait, match 0.35) gives 0.488 CSS px/u, so
            // 44 CSS pt needs >=90 u: base 180 u (88 px), strike 110 u
            // (54 px), dash 96 u (47 px) all clear the floor.
            _touchJoystickRoot = Panel(root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(260, 260), new Color(0f, 0f, 0f, 0f));
            var catchPanel = _touchJoystickRoot;
            catchPanel.GetComponent<Image>().raycastTarget = true;   // joystick catch surface
            var joystick = catchPanel.AddComponent<VirtualJoystick>();
            joystick.Input = Input;
            joystick.BaseRect = JoystickSprite(catchPanel.transform, "ui-joystick-base",
                new Vector2(130, 130), 180f, 0.4f);
            joystick.NubRect = JoystickSprite(joystick.BaseRect, "ui-joystick-nub",
                Vector2.zero, 84f, 0.75f);
            // Nub rides the base's center, not its bottom-left corner.
            joystick.NubRect.anchorMin = joystick.NubRect.anchorMax = new Vector2(0.5f, 0.5f);
            joystick.RestCenter = new Vector2(130, 130);
            catchPanel.transform.SetAsFirstSibling();

            // Right: strike (raised to y=150 while the dungeon row exists —
            // fixes the 44 u overlap with the F card, spec #4).
            var strike = Panel(root, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-24, 36), new Vector2(110, 110), new Color(0.8f, 0.4f, 0.25f, 0.5f));
            strike.GetComponent<Image>().raycastTarget = true;   // TouchHold hit surface
            _strikeRect = strike.GetComponent<RectTransform>();
            _strikeRect.pivot = new Vector2(1, 0);
            var touch = strike.AddComponent<TouchHold>();
            touch.OnStateChanged = state => { if (state) Input.QueueAttack(); };
            Label(strike.transform, 0, 0, 110, 110, "타격", 20, TextAnchor.MiddleCenter);

            // Dash button above strike (spec #5): dungeon-only, thumb-reach
            // dash — the SHIFT card sits outside the right-thumb arc. 24 u+
            // gap to strike guards against mis-taps.
            var dash = Panel(root, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-24, 272), new Vector2(96, 96),
                new Color(0.17f, 0.68f, 0.84f, 0.4f));
            dash.GetComponent<Image>().raycastTarget = true;   // TouchHold hit surface
            _dashTouchRect = dash.GetComponent<RectTransform>();
            _dashTouchRect.pivot = new Vector2(1, 0);
            var dashTouch = dash.AddComponent<TouchHold>();
            dashTouch.OnStateChanged = state => { if (state) Input.QueueDash(); };
            Label(dash.transform, 0, 0, 96, 96, "질주", 18, TextAnchor.MiddleCenter);
            dash.SetActive(false);   // SyncTouchModeSurfaces enables in dungeon

            _touchActive = true;
            if (_retryLabel != null) _retryLabel.text = "다시 도전";
            if (_stageClearRetryLabel != null) _stageClearRetryLabel.text = "다시 도전";
            SyncTouchModeSurfaces();
        }

        /// <summary>Joystick art layer; falls back to a translucent disc panel
        /// when the sprite is missing so the control never disappears.</summary>
        RectTransform JoystickSprite(Transform parent, string iconId,
                                     Vector2 center, float size, float alpha)
        {
            var spriteObject = new GameObject(iconId);
            spriteObject.transform.SetParent(parent, false);
            var image = spriteObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>("Icons/" + iconId);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                image.color = new Color(1f, 1f, 1f, 0.12f);
            }
            image.raycastTarget = false;   // catch panel owns the pointer
            var rect = spriteObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        /// <summary>Floating stick. Writes InputAdapter.TouchMoveX/TouchMoveY.
        /// SIGN: SimInput.MoveY is screen-down positive (SimTypes.cs L24), so
        /// dragging the nub UP (uGUI +y) must yield NEGATIVE TouchMoveY —
        /// mirror of the D-pad's "▲ => moveY -= 1". Deadzone 0.15 of the
        /// 60 u throw; above it the direction is re-normalized to length 1
        /// (magnitude is on/off — the sim normalizes the vector anyway).</summary>
        sealed class VirtualJoystick : MonoBehaviour,
            IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            const float Throw = 60f;
            const float Deadzone = 0.15f;

            public InputAdapter Input;
            public RectTransform BaseRect;   // child of the catch panel
            public RectTransform NubRect;    // child of BaseRect, pivot-centered
            public Vector2 RestCenter;

            RectTransform _catchRect;

            void Awake() => _catchRect = (RectTransform)transform;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _catchRect, eventData.position, eventData.pressEventCamera,
                        out var local))
                    BaseRect.anchoredPosition = local;   // float to the press
                Steer(eventData);
            }

            public void OnDrag(PointerEventData eventData) => Steer(eventData);

            public void OnPointerUp(PointerEventData _) => ResetStick();

            void OnDisable() => ResetStick();

            void ResetStick()
            {
                if (BaseRect != null) BaseRect.anchoredPosition = RestCenter;
                if (NubRect != null) NubRect.anchoredPosition = Vector2.zero;
                if (Input != null) { Input.TouchMoveX = 0f; Input.TouchMoveY = 0f; }
            }

            void Steer(PointerEventData eventData)
            {
                if (Input == null) return;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _catchRect, eventData.position, eventData.pressEventCamera,
                        out var local))
                    return;
                var delta = local - BaseRect.anchoredPosition;
                var magnitude = delta.magnitude;
                NubRect.anchoredPosition = magnitude > Throw
                    ? delta * (Throw / magnitude) : delta;
                if (magnitude < Throw * Deadzone)
                {
                    Input.TouchMoveX = 0f;
                    Input.TouchMoveY = 0f;
                    return;
                }
                var direction = delta / magnitude;      // re-normalized
                Input.TouchMoveX = direction.x;
                Input.TouchMoveY = -direction.y;        // screen-down positive
            }
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
            _bossAliveAtDeath = sim is ICampaignSnapshot campaign && campaign.BossAlive;
            // W10: the command queue advances on the SAME per-tick mask the
            // audio and VFX directors read, so a trigger can never disagree with
            // the cue the player just heard.
            ObserveCommandQueueEvents(events);
            if ((events & SimEvents.HazardPulse) != 0)
                _recentHazardTime = Time.unscaledTime;
            if ((events & SimEvents.WaveStarted) != 0)
            {
                _loreText.text = LoreBeats[(sim.Wave - 1) % LoreBeats.Length];
                _loreTimer = 6f;
            }
            if ((events & SimEvents.GameOver) != 0)
            {
                var digest = sim.Digest;
                // A trial has no legion, no waves and no score. The dungeon
                // defeat line ("군단에 함락됐다 • 웨이브 1 도달") names three
                // things that do not exist in a trial, so it reads as a bug.
                if (_trialStatsHidden)
                {
                    if (_gameOverTitle != null) _gameOverTitle.text = "시련 중단";
                    _finalText.text = $"기믹에 쓰러졌다 • 피격 {_lastTrialHits}회";
                }
                else
                {
                    if (_gameOverTitle != null) _gameOverTitle.text = "잿불 법정 함락";
                    var deathContext = _bossAliveAtDeath
                        ? "보스전에서 밀려났다"
                        : Time.unscaledTime - _recentHazardTime <= 2f
                            ? "위험 지대에 잠식됐다"
                            : "군단에 함락됐다";
                    // AMENDMENT #11 UI: the tier rides the summary line. Without it
                    // the score is unreadable as a result — the same number means
                    // different things on 입문 and 악몽.
                    _finalText.text =
                        $"점수 {digest.Score:N0} • 유물 {digest.Relics} • 처치 {digest.Kills}\n" +
                        $"{deathContext} • 웨이브 {digest.Wave} 도달" +
                        DifficultyResultSuffix(_runDifficulty);

                }
                ResetTransientCeremonies();
                _gameOverPanel.SetActive(true);
                SetTouchCombatControlsVisible(false);
                // The left stack belongs to a LIVE run. Measured in the browser:
                // the death panel came up in the arena with 포기 still armed, so
                // the abandon modal opened on top of it — offering to forfeit a
                // run that had already ended. The codex is the same shape of
                // wrong: its numbers describe a sim that is over.
                SetLeftStackAvailable(false);
            }
            if ((events & SimEvents.WaveStarted) != 0 && _gameOverPanel.activeSelf)
            {
                _gameOverPanel.SetActive(false);
                SetTouchCombatControlsVisible(true);
                // A restart re-arms it; PrepareRunUi also re-enables per mode.
                SetLeftStackAvailable(true);
            }

            // --- juice: wave banner (#20) -------------------------------------
            if ((events & SimEvents.WaveStarted) != 0)
            {
                _waveBanner.text = $"웨이브 {sim.Wave}";
                _waveBanner.color = new Color(0.95f, 0.35f, 0.17f, 0f);
                _waveBannerTimer = 1.45f;   // 0.25 punch-in + 1.2 hold/fade
            }
            if ((events & SimEvents.BossSpawned) != 0)
            {
                _waveBanner.text = "보스 웨이브";
                _waveBanner.color = new Color(1f, 0.25f, 0.2f, 0f);
                _waveBannerTimer = 1.8f;
            }
            if ((events & SimEvents.ComboFinisher) != 0)
                _finisherPipTimer = ViewPrefs.TimeEffectsAllowed ? 0.4f : 0f;

            // --- juice: cast screen flash (#10) --------------------------------
            if ((events & SimEvents.NovaCast) != 0)
                StartCastFlash(new Color(0.95f, 0.35f, 0.17f, 0.28f));
            else if ((events & SimEvents.WardCast) != 0)
                StartCastFlash(new Color(0.17f, 0.68f, 0.84f, 0.24f));
            else if ((events & SimEvents.BoltCast) != 0)
                StartCastFlash(new Color(0.62f, 0.42f, 0.95f, 0.20f));
            else if ((events & SimEvents.AltarBlessing) != 0)
                StartCastFlash(new Color(0.87f, 0.78f, 0.41f, 0.26f));

            // --- juice: damage vignette punch (#9) ------------------------------
            if ((events & SimEvents.PlayerDamaged) != 0)
            {
                _vignette.enabled = true;
                _vignette.color = new Color(0.95f, 0.22f, 0.13f,
                    0.6f * ViewPrefs.MotionScale);
            }
        }

        void StartCastFlash(Color color)
        {
            var scaledAlpha = color.a * ViewPrefs.MotionScale;
            if (scaledAlpha <= 0f) return;
            color.a = scaledAlpha;
            _castFlash.color = color;
            _castFlashPeak = scaledAlpha;
            _castFlash.enabled = true;
            _castFlashTimer = 0.09f;
        }

        public void Sync(ISimSnapshot sim)
        {
            var health = Mathf.CeilToInt(sim.Player.Health);
            if (sim.Player.Health > _maxHealthSeen)
                _maxHealthSeen = sim.Player.Health;
            if (health != _lastHealth)
            {
                _lastHealth = health;
                _healthFill.fillAmount = sim.Player.Health / Mathf.Max(1f, _maxHealthSeen);
                _healthText.text = $"체력 {health}";
            }
            // #15 low-health heartbeat is already driven by SyncJuice (sub-35
            // HP pulse, MotionScale-scaled, zero-overdraw idle) — no second
            // threshold here; one source of truth for the survival signal.
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
                SimConfig.NovaCooldown, sim.Charge >= SimConfig.NovaCost, _novaFrame);
            SyncSkill(_wardCooldownOverlay, _wardGroup, sim.WardCooldown,
                SimConfig.WardCooldown, sim.Charge >= SimConfig.WardCost, _wardFrame);


            if (_loreTimer > 0f)
            {
                _loreTimer -= Time.deltaTime;
                if (_loreTimer <= 0f) _loreText.text = string.Empty;
            }

            SyncJuice(sim);
            // AMENDMENT #9: latch the codex's numbers on the first frame after
            // it opens. No-op when closed or already latched.
            SyncCodex(sim);

            if (_gameOverPanel.activeSelf && sim.Mode != SimMode.GameOver)
            {
                _gameOverPanel.SetActive(false);
                SetTouchCombatControlsVisible(true);
                // Restart landed on wave 1 again — reseed the opening lore.
                _loreText.text = LoreBeats[(sim.Wave - 1) % LoreBeats.Length];
                _loreTimer = 6f;
            }
        }

        // Per-frame juice decay: vignette pulse (#9), lantern flicker (#15),
        // cast flash (#10), wave banner (#20), level toast/punch (#19).
        void SyncJuice(ISimSnapshot sim)
        {
            // Input depth §3/§5. Both read additive seams, so a snapshot that
            // predates the amendment simply leaves them hidden.
            if (sim is CinderSim liveSim)
            {
                SyncChargeGauge(liveSim.ChargeProgress);
            }
            if (sim is IHackSnapshot hack)
            {
                // A9 momentum readout, re-enabled. This line was commented out
                // while the VFX lane's view half sat in a tree whose sim knew
                // nothing about momentum; main b97d609 landed the sim half
                // (IHackSnapshot.Momentum / MomentumTier /
                // MomentumDamageMultiplier, HackSpec.MomentumMax), so the seam
                // closes here. Additive as before: a snapshot that predates the
                // amendment reports 0 and the bar simply stays unbuilt.
                SyncMomentumGauge(hack.Momentum, hack.MomentumTier, hack.MomentumDamageMultiplier);
            }
            if (sim is IGrowthChoiceSnapshot growth)
            {
                SyncGrowthOffer(growth.GrowthOfferOpen, growth.GrowthOfferTime);
            }

            // Low-HP vignette: heartbeat pulse under 35 HP, scaled for reduced motion.
            var lowHp = sim.Mode != SimMode.GameOver && sim.Player.Health < 35f;
            var targetAlpha = lowHp
                ? (0.25f + 0.2f * Mathf.Sin(Time.time * 7f)) * ViewPrefs.MotionScale
                : 0f;
            var current = _vignette.color;
            var alpha = Mathf.MoveTowards(current.a, targetAlpha, Time.deltaTime * 1.6f);
            if (alpha > 0.001f)
            {
                _vignette.enabled = true;
                _vignette.color = new Color(0.95f, 0.22f, 0.13f, alpha);
            }
            else if (_vignette.enabled)
            {
                _vignette.enabled = false;   // zero overdraw when idle
            }

            // Cast flash: 90 ms linear fade from the cast-time peak (multiplying
            // the live alpha would compound quadratically and frame-rate-vary).
            if (_castFlashTimer > 0f)
            {
                _castFlashTimer -= Time.deltaTime;
                var flash = _castFlash.color;
                flash.a = _castFlashPeak * Mathf.Clamp01(_castFlashTimer / 0.09f);
                _castFlash.color = flash;
                if (_castFlashTimer <= 0f) _castFlash.enabled = false;
            }

            // Lantern flicker: oil below Nova cost gutters; below 20 warns red.
            if (sim.Charge < SimConfig.NovaCost)
            {
                var flicker = Mathf.PerlinNoise(Time.time * 6f, 0f);
                var baseColor = sim.Charge < 20f
                    ? new Color(0.95f, 0.42f, 0.3f)
                    : new Color(1f, 0.83f, 0.45f);
                _chargeFill.color = Color.Lerp(baseColor * 0.55f, baseColor, 0.55f + 0.45f * flicker);
            }
            else if (_chargeFill.color != new Color(1f, 0.83f, 0.45f))
            {
                _chargeFill.color = new Color(1f, 0.83f, 0.45f);
            }

            // Wave banner: 0.25 s punch-in, hold, last 0.4 s fade.
            if (_waveBannerTimer > 0f)
            {
                _waveBannerTimer -= Time.deltaTime;
                var t = _waveBannerTimer;
                var color = _waveBanner.color;
                float bannerAlpha;
                var punch = Mathf.Clamp01((1.45f - t) / 0.25f);   // 0->1 entry
                _waveBanner.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, punch));
                if (t <= 0.4f) bannerAlpha = Mathf.Clamp01(t / 0.4f);
                else bannerAlpha = punch;
                _waveBanner.color = new Color(color.r, color.g, color.b, bannerAlpha);
                if (t <= 0f) _waveBanner.text = string.Empty;
            }

            // Level toast + Lv punch + XP gold flash.
            if (_levelToastTimer > 0f)
            {
                _levelToastTimer -= Time.deltaTime;
                var color = _levelToast.color;
                var toastAlpha = _levelToastTimer > 1.0f
                    ? Mathf.Clamp01((1.4f - _levelToastTimer) / 0.25f)
                    : Mathf.Clamp01(_levelToastTimer / 0.5f);
                _levelToast.color = new Color(color.r, color.g, color.b, toastAlpha);
                if (_levelToastTimer <= 0f) _levelToast.text = string.Empty;
            }
            if (_levelPunchTimer > 0f && _levelText != null)
            {
                _levelPunchTimer -= Time.deltaTime;
                var scale = Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(_levelPunchTimer / 0.35f));
                _levelText.rectTransform.localScale = Vector3.one * scale;
            }
            if (_xpFlashTimer > 0f && _xpFill != null)
            {
                _xpFlashTimer -= Time.deltaTime;
                _xpFill.color = Color.Lerp(XpBaseColor,
                    new Color(0.87f, 0.78f, 0.41f), Mathf.Clamp01(_xpFlashTimer / 0.4f));
            }
            SyncBossIntro();
            SyncSpeakerLine();
            SyncStageClearCeremony();
            SyncComboPips();
            SyncBossBarMotion();
        }

        void SetBossIntroState(float slide, float alpha)
        {
            _letterboxTop.rectTransform.anchoredPosition = new Vector2(0f, 90f * (1f - slide));
            _letterboxBottom.rectTransform.anchoredPosition = new Vector2(0f, -90f * (1f - slide));
            _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, alpha);
        }

        void SyncSpeakerLine()
        {
            if (_speakerLine == null || _speakerLineTimer <= 0f) return;
            _speakerLineTimer -= Time.deltaTime;
            var alpha = _speakerLineTimer > 3f
                ? Mathf.Clamp01((3.5f - _speakerLineTimer) / 0.5f)   // fade in
                : Mathf.Clamp01(_speakerLineTimer / 0.6f);           // hold/out
            var color = _speakerLine.color;
            color.a = alpha;
            _speakerLine.color = color;
        }

        void SyncBossIntro()
        {
            if (!_bossIntroActive) return;
            _bossIntroTimer -= Time.unscaledDeltaTime;
            var elapsed = BossIntroDuration - _bossIntroTimer;
            const float transition = 0.15f;
            var slide = ViewPrefs.ReducedMotion
                ? 1f
                : elapsed < transition
                    ? Mathf.SmoothStep(0f, 1f, elapsed / transition)
                    : _bossIntroTimer < transition
                        ? Mathf.SmoothStep(0f, 1f, _bossIntroTimer / transition)
                        : 1f;
            var alpha = _bossIntroTimer < transition
                ? Mathf.Clamp01(_bossIntroTimer / transition)
                : Mathf.Clamp01(elapsed / transition);
            SetBossIntroState(slide, alpha);
            if (_bossIntroTimer <= 0f)
            {
                _bossIntroTimer = 0f;
                _bossIntroActive = false;
                _letterboxTop.enabled = false;
                _letterboxBottom.enabled = false;
                _bossIntroPlate.text = string.Empty;
                _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            }
        }

        void SyncStageClearCeremony()
        {
            if (!_stageClearPending) return;
            _stageClearTimer -= Time.unscaledDeltaTime;
            var elapsed = StageClearDuration - _stageClearTimer;
            const float transition = 0.12f;
            var alpha = _stageClearTimer <= transition
                ? Mathf.Clamp01(_stageClearTimer / transition)
                : Mathf.Clamp01(elapsed / transition);
            _stageClearBanner.color = new Color(StageClearColor.r, StageClearColor.g,
                StageClearColor.b, alpha);
            var punch = Mathf.Clamp01(elapsed / transition);
            _stageClearBanner.rectTransform.localScale = ViewPrefs.TimeEffectsAllowed
                ? Vector3.one * Mathf.Lerp(1.18f, 1f, Mathf.SmoothStep(0f, 1f, punch))
                : Vector3.one;
            var pulse = Mathf.Clamp01(elapsed / StageClearDuration);
            _stageClearFlash.fillAmount = pulse;
            var flashAlpha = Mathf.Sin(pulse * Mathf.PI) * 0.38f * ViewPrefs.MotionScale;
            if (flashAlpha > 0.001f)
            {
                _stageClearFlash.enabled = true;
                _stageClearFlash.color = new Color(StageClearColor.r, StageClearColor.g,
                    StageClearColor.b, flashAlpha);
            }
            else _stageClearFlash.enabled = false;
            if (_stageClearTimer > 0f) return;

            _stageClearPending = false;
            _stageClearTimer = 0f;
            _stageClearFlash.fillAmount = 0f;
            _stageClearBanner.text = string.Empty;
            _stageClearBanner.color = new Color(StageClearColor.r, StageClearColor.g,
                StageClearColor.b, 0f);
            _stageClearBanner.rectTransform.localScale = Vector3.one;
            // A trial reports what a trial measures. Score and relics are both
            // structurally 0 there (no spawns), so the dungeon line would read
            // "점수 0 • 유물 0" and imply a failed run instead of a finished one.
            _stageClearText.text = _trialStatsHidden
                ? $"피격 {_trialClearHits}회"
                : $"점수 {_stageClearFinalScore:N0} • 유물 {_stageClearFinalRelics}"
                  + DifficultyResultSuffix(_runDifficulty);

            if (_stageClearTitle != null)
                _stageClearTitle.text = _trialStatsHidden ? "시련 완료" : "구역 정화";
            _stageClearPanel.SetActive(true);
            SetTouchCombatControlsVisible(false);
            // Same reasoning as the death panel: the run is over, so forfeiting
            // it is meaningless and the codex's numbers are a past tense.
            SetLeftStackAvailable(false);
        }

        void SyncComboPips()
        {
            for (var i = 0; i < _pipPunchTimers.Length; i++)
            {
                if (_pipPunchTimers[i] <= 0f) continue;
                _pipPunchTimers[i] -= Time.deltaTime;
                var scale = Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(_pipPunchTimers[i] / 0.2f));
                _comboPipRects[i].localScale = Vector3.one * scale;
                if (_pipPunchTimers[i] <= 0f) _comboPipRects[i].localScale = Vector3.one;
            }

            if (_finisherPipTimer <= 0f || _comboPips == null) return;
            _finisherPipTimer -= Time.deltaTime;
            var flash = Mathf.Clamp01(_finisherPipTimer / 0.4f);
            for (var i = 0; i < _comboPips.Length; i++)
            {
                var baseline = i < _lastCombo
                    ? new Color(1f, 0.83f, 0.45f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.14f);
                _comboPips[i].color = _finisherPipTimer > 0f
                    ? Color.Lerp(baseline, new Color(1f, 0.96f, 0.62f, 1f), flash)
                    : baseline;
            }
        }

        void SyncBossBarMotion()
        {
            if (_bossBarRect == null) return;
            if (_bossRevealTimer > 0f)
            {
                _bossRevealTimer -= Time.deltaTime;
                var reveal = 1f - Mathf.Clamp01(_bossRevealTimer / 0.4f);
                _bossBarRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-98f, -58f, reveal));
            }
            if (_bossPhasePunchTimer <= 0f) return;
            _bossPhasePunchTimer -= Time.deltaTime;
            _bossBarRect.anchoredPosition = new Vector2(
                Mathf.Sin(_bossPhasePunchTimer * 180f) * 6f, -58f);
            if (_bossPhasePunchTimer <= 0f)
                _bossBarRect.anchoredPosition = new Vector2(0f, -58f);
        }

        static void SyncSkill(Image overlay, CanvasGroup group, float cooldown,
                              float maxCooldown, bool affordable, Image frame = null)
        {
            overlay.fillAmount = cooldown / maxCooldown;
            group.alpha = affordable ? 1f : 0.45f;
            ApplySkillCardReadyState(frame, cooldown <= 0.0001f);
        }

        /// <summary>HUD atlas ready-state chrome: swap the card's frame
        /// sprite to the gold-rim variant while its cooldown overlay is
        /// fully retracted (fillAmount 0 = usable now). The dark cooldown
        /// overlay already communicates "not ready" per-pixel as it wipes
        /// down, so this only touches the border art, never fillAmount/alpha
        /// — those keep working exactly as before. Sprites are cached after
        /// first load and the swap is skipped when already applied, so this
        /// costs nothing beyond a null/reference check on every other frame.
        /// No-op for callers with no frame (arena is optional; frame == null
        /// happens if the atlas sprite failed to import).</summary>
        static void ApplySkillCardReadyState(Image frame, bool ready)
        {
            if (frame == null) return;
            if (_skillFrameNormalSprite == null)
                _skillFrameNormalSprite = Resources.Load<Sprite>("Icons/hud-skill-card-frame");
            if (_skillFrameReadySprite == null)
                _skillFrameReadySprite = Resources.Load<Sprite>("Icons/hud-skill-card-frame-ready");
            var target = ready ? _skillFrameReadySprite : _skillFrameNormalSprite;
            if (target != null && frame.sprite != target) frame.sprite = target;
        }
    }
}
