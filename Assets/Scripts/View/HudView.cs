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
        public System.Action OnRetryStage;

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
        float _rotateHintTimer;

        RectTransform _metersRect, _statsRect, _muteRect;
        RectTransform _healthBackRect, _chargeBackRect;
        RectTransform _novaRect, _wardRect;
        RectTransform _equipRect, _shieldRect;
        RectTransform _skillRowRect, _dashCardRect;
        readonly RectTransform[] _skillCardRects = new RectTransform[4];
        readonly RectTransform[] _comboPipRects = new RectTransform[3];
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
        int _stageClearShownScore = -1, _stageClearShownRelics = -1;
        bool _stageClearPending;
        Image _letterboxTop, _letterboxBottom;
        Text _bossIntroPlate;
        float _bossIntroTimer;
        const float BossIntroDuration = 2.9f; // 0.45 in + 2.0 hold + 0.45 out
        float _maxHealthSeen = SimConfig.PlayerMaxHealth;
        float _recentHazardTime = -999f;
        bool _bossAliveAtDeath;
        readonly float[] _pipPunchTimers = new float[3];
        float _finisherPipTimer;
        RectTransform _bossBarRect;
        float _bossRevealTimer;
        float _bossPhasePunchTimer;

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

            if (FindFirstObjectByType<EventSystem>() == null)
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
            bossIntroRect.anchorMin = bossIntroRect.anchorMax = new Vector2(0.5f, 1f);
            bossIntroRect.pivot = new Vector2(0.5f, 1f);
            bossIntroRect.anchoredPosition = new Vector2(0f, -90f);
            _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            _bossIntroPlate.fontStyle = FontStyle.Bold;

            _stageClearBanner = Label(root, 0, 0, 560, 84, "", 28, TextAnchor.MiddleCenter);
            var clearBannerRect = _stageClearBanner.rectTransform;
            clearBannerRect.anchorMin = clearBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            clearBannerRect.pivot = new Vector2(0.5f, 0.5f);
            clearBannerRect.anchoredPosition = new Vector2(0f, 84f);
            _stageClearBanner.color = new Color(1f, 0.83f, 0.45f, 0f);
            _stageClearBanner.fontStyle = FontStyle.Bold;

            // --- top-left: health + oil -------------------------------------
            var meters = Panel(root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -16), new Vector2(300, 74), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            _metersRect = meters.GetComponent<RectTransform>();
            _healthFill = Bar(meters.transform, 8, -8, 284, 22,
                new Color(0.95f, 0.42f, 0.3f), out _healthText, "체력");
            _chargeFill = Bar(meters.transform, 8, -40, 284, 22,
                new Color(1f, 0.83f, 0.45f), out _chargeText, "기름");
            _healthBackRect = (RectTransform)_healthFill.transform.parent;
            _chargeBackRect = (RectTransform)_chargeFill.transform.parent;

            // --- top-right: wave / score / relics / enemies -------------------
            var stats = Panel(root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -16), new Vector2(240, 108), new Color(0.05f, 0.04f, 0.09f, 0.55f));
            _statsRect = stats.GetComponent<RectTransform>();
            _waveText = Label(stats.transform, 10, -6, 220, 24, "웨이브 1", 18, TextAnchor.MiddleLeft);
            _scoreText = Label(stats.transform, 10, -30, 220, 24, "점수 0", 18, TextAnchor.MiddleLeft);
            _relicText = Label(stats.transform, 10, -54, 220, 24, "유물 0", 18, TextAnchor.MiddleLeft);
            _enemyText = Label(stats.transform, 10, -78, 220, 24, "적 0", 18, TextAnchor.MiddleLeft);

            // --- mute toggle under stats --------------------------------------
            var muteButton = TextButton(root, new Vector2(1, 1), new Vector2(-16, -132),
                new Vector2(240, 34), "소리: 켜짐", 16,
                () => { if (Audio != null) { Audio.ToggleMute(); RefreshMuteLabel(); } });
            _muteLabel = muteButton.GetComponentInChildren<Text>();
            _muteRect = muteButton.GetComponent<RectTransform>();
            RefreshMuteLabel();

            // --- bottom-center: skill cards ------------------------------------
            var novaCard = SkillCard(root, -95, "Q", "잿불 노바",
                () => { if (Input != null) Input.QueueNova(); },
                out _novaCooldownOverlay, out _novaGroup, "skill-nova");
            var wardCard = SkillCard(root, 95, "E", "랜턴 결계",
                () => { if (Input != null) Input.QueueWard(); },
                out _wardCooldownOverlay, out _wardGroup, "skill-ward");
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
            overTitle.color = new Color(1f, 0.55f, 0.4f);
            _finalText = Label(_gameOverPanel.transform, 0, -70, 460, 60, "", 18, TextAnchor.MiddleCenter);
            TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f), new Vector2(0, 26),
                new Vector2(200, 44), "재강하 (R)", 20, RetryRun);
            _gameOverPanel.SetActive(false);

            // --- wave banner (#20) + level toast (#19), raycast-off ----------
            _waveBanner = Label(root, 0, -140, 600, 60, "", 34, TextAnchor.MiddleCenter);
            var bannerRect = _waveBanner.rectTransform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0, -140);
            _waveBanner.color = new Color(0.95f, 0.35f, 0.17f, 0f);
            _waveBanner.fontStyle = FontStyle.Bold;

            _levelToast = Label(root, 0, 170, 480, 34, "", 17, TextAnchor.MiddleCenter);
            var toastRect = _levelToast.rectTransform;
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.anchoredPosition = new Vector2(0, 170);
            _levelToast.color = new Color(0.56f, 0.91f, 1f, 0f);

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
        }

        // =============================================== mobile layout core --
        void Update()
        {
            if (_scaler == null) return;   // Build() not called yet
            // Cheap dirty-check (two int compares + rect compare); RectTransform
            // writes happen only on actual resolution / safe-area changes.
            SyncLayout(false);

            if (_rotateHintTimer > 0f)
            {
                _rotateHintTimer -= Time.deltaTime;
                if (_rotateHintTimer <= 0f) HidePrologueToast();
            }
        }

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
            if (tier == LayoutTier.Phone)
            {
                // Stats drop below the meters (left column), freeing the top
                // right for the compact mute icon. Relic/enemy rows stay live
                // (labels remain 4 — merged strings would allocate per wave).
                _statsRect.anchorMin = _statsRect.anchorMax = new Vector2(0f, 1f);
                _statsRect.pivot = new Vector2(0f, 1f);
                _statsRect.anchoredPosition = new Vector2(16, -98);
                _statsRect.sizeDelta = new Vector2(statsWidth, 108);
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
                _statsRect.sizeDelta = new Vector2(statsWidth, 108);
                _muteRect.anchoredPosition = new Vector2(-16, -132);
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
            var arenaCardSize = new Vector2(150, tier == LayoutTier.Phone ? 92f : 88f);
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
                _loreText.rectTransform.anchoredPosition = new Vector2(0, 118 + arenaLift);

            ApplyDungeonTier(tier);
            ApplyEquipPlacement();
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
            }
            else
            {
                for (var i = 0; i < 4; i++)
                {
                    _skillCardRects[i].sizeDelta = new Vector2(108, 88);
                    _skillCardRects[i].anchoredPosition = new Vector2(-116 + i * 116, 18 + lift);
                }
                _dashCardRect.sizeDelta = new Vector2(110, 88);
                _dashCardRect.anchoredPosition = new Vector2(-232, 18 + lift);
                for (var i = 0; i < 3; i++)
                    _comboPipRects[i].anchoredPosition = new Vector2(-286 + i * 26, 52 + lift);
            }
            // Left-column stack (below meters -16..-90): phone puts stats at
            // -98..-206, so dungeon shield text drops to -252 (equip strip
            // occupies -214..-248); with touch on wider tiers the equip strip
            // holds -98..-132, shield takes -136.
            if (_shieldRect != null)
                _shieldRect.anchoredPosition = tier == LayoutTier.Phone
                    ? new Vector2(16, -252)
                    : _touchActive ? new Vector2(16, -136) : new Vector2(16, -98);
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
            var clearTitle = Label(_stageClearPanel.transform, 0, -18, 480, 36, "구역 정화", 28, TextAnchor.MiddleCenter);
            clearTitle.color = new Color(0.56f, 0.91f, 1f);
            _stageClearText = Label(_stageClearPanel.transform, 0, -74, 480, 60, "", 18, TextAnchor.MiddleCenter);
            TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(-105, 24),
                new Vector2(190, 44), "캠페인으로", 18, ReturnHome);
            TextButton(_stageClearPanel.transform, new Vector2(0.5f, 0f), new Vector2(105, 24),
                new Vector2(190, 44), "재강하 (R)", 18, RetryRun);
            _stageClearPanel.SetActive(false);

            // Campaign game-over also offers the way back to the hub.
            TextButton(_gameOverPanel.transform, new Vector2(0.5f, 0f), new Vector2(0, 76),
                new Vector2(200, 40), "캠페인으로", 16, ReturnHome);
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
            _stageClearShownScore = _stageClearShownRelics = -1;
            _stageClearTimer = 0.9f;
            _stageClearPending = true;
            _stageClearBanner.text = "스테이지 클리어\n점수 0 • 유물 0";
            _stageClearBanner.color = new Color(1f, 0.83f, 0.45f, 0f);
            _stageClearBanner.rectTransform.localScale = Vector3.one;
            _stageClearFlash.color = new Color(1f, 0.83f, 0.45f,
                0.38f * ViewPrefs.MotionScale);
            _stageClearFlash.enabled = true;
        }

        public void ShowBossIntro(string bossName)
        {
            if (_bossIntroPlate == null) return;
            _bossIntroPlate.text = "— " + bossName + " —";
            _bossIntroTimer = BossIntroDuration;
            _letterboxTop.enabled = true;
            _letterboxBottom.enabled = true;
            SetBossIntroState(ViewPrefs.ReducedMotion ? 1f : 0f, 0f);
        }

        public void ResetRunUi()
        {
            _maxHealthSeen = SimConfig.PlayerMaxHealth;
            _lastHealth = -1;
            _recentHazardTime = -999f;
            _bossAliveAtDeath = false;
            _stageClearPending = false;
            _stageClearTimer = 0f;
            if (_stageClearPanel != null) _stageClearPanel.SetActive(false);
            if (_stageClearFlash != null) _stageClearFlash.enabled = false;
            if (_stageClearBanner != null)
            {
                _stageClearBanner.text = string.Empty;
                _stageClearBanner.color = new Color(1f, 0.83f, 0.45f, 0f);
                _stageClearBanner.rectTransform.localScale = Vector3.one;
            }
            _bossIntroTimer = 0f;
            if (_letterboxTop != null) _letterboxTop.enabled = false;
            if (_letterboxBottom != null) _letterboxBottom.enabled = false;
            if (_bossIntroPlate != null) _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_bossBar != null) _bossBar.SetActive(false);
            _lastBossFraction = -1f;
            _lastBossPhase = -1;
            _lastCombo = -1;
            for (var i = 0; i < _comboPipRects.Length; i++)
                if (_comboPipRects[i] != null) _comboPipRects[i].localScale = Vector3.one;
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
            var dungeonOn = _dungeonRoot != null && _dungeonRoot.activeSelf;
            if (_dashTouchRect != null && _dashTouchRect.gameObject.activeSelf != dungeonOn)
                _dashTouchRect.gameObject.SetActive(dungeonOn);
            if (_strikeRect != null)
                _strikeRect.anchoredPosition = new Vector2(-24, dungeonOn ? 150 : 36);
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
                new Vector2(0, 4), new Vector2(560, 10), new Color(0f, 0f, 0f, 0.6f));
            xpBack.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var xpFillObject = new GameObject("XpFill");
            xpFillObject.transform.SetParent(xpBack.transform, false);
            _xpFill = xpFillObject.AddComponent<Image>();
            _xpFill.color = new Color(0.56f, 0.91f, 1f);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
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
                    new Vector2(-286 + i * 26, 52), new Vector2(20, 20),
                    new Color(1f, 1f, 1f, 0.14f));
                pip.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
                _comboPips[i] = pip.GetComponent<Image>();
                _comboPipRects[i] = pip.GetComponent<RectTransform>();
            }

            // --- skill row: dash + Q/E/R/F --------------------------------------
            _skillOverlays = new Image[4];
            _skillGroups = new CanvasGroup[4];
            var dashCard = SkillCard(dungeonRoot, -232, "SHIFT", "질주",
                () => { if (Input != null) Input.QueueDash(); },
                out _dashOverlay, out _, "skill-dash");
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
                    actions[i], out _skillOverlays[i], out _skillGroups[i], icons[i]);
                _skillCardRects[i] = card.GetComponent<RectTransform>();
                _skillCardRects[i].sizeDelta = new Vector2(108, 88);
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
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
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
                new Vector2(6, 4), new Vector2(108, 8), new Color(0f, 0f, 0f, 0.6f));
            extractBack.GetComponent<RectTransform>().pivot = Vector2.zero;
            var extractFillObject = new GameObject("ExtractFill");
            extractFillObject.transform.SetParent(extractBack.transform, false);
            _extractRing = extractFillObject.AddComponent<Image>();
            _extractRing.color = new Color(0.62f, 0.95f, 0.88f);
            _extractRing.type = Image.Type.Filled;
            _extractRing.fillMethod = Image.FillMethod.Horizontal;
            _extractRing.raycastTarget = false;
            var extractRect = extractFillObject.GetComponent<RectTransform>();
            extractRect.anchorMin = Vector2.zero;
            extractRect.anchorMax = Vector2.one;
            extractRect.offsetMin = new Vector2(1, 1);
            extractRect.offsetMax = new Vector2(-1, -1);
            _extractRoot.SetActive(false);

            _shieldRect = _shieldText.rectTransform;
            ApplyLayoutTier();          // grade the fresh dungeon surfaces
            SyncTouchModeSurfaces();    // dash touch button goes live
            ShowRotateHintIfPortrait(); // spec #14: one-time landscape nudge
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
                // Level-up ceremony (#19): skip the first sync (0 -> N seed).
                if (_lastLevel >= 1 && level > _lastLevel)
                {
                    _levelPunchTimer = 0.35f;
                    _xpFlashTimer = 0.4f;
                    _levelToast.text = "레벨 업! 피해 +4% · 최대 체력 +6";
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
                    _bossPhasePip.text = bossPhase >= 2 ? "PHASE II" : "PHASE I";
                    _bossFill.color = bossPhase >= 2
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
                         Vector2 anchored, Vector2 size, Color color)
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
            fill.raycastTarget = false;
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
                              UnityEngine.Events.UnityAction onClick)
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
            card.GetComponent<Image>().raycastTarget = true;   // Button hit surface
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

        /// <summary>Test seam: batchmode has no Touchscreen device, so tests
        /// force-build the touch surfaces Build() gates on hardware.</summary>
        internal void ForceTouchControlsForTest()
        {
            if (!_touchActive) BuildTouchControls(_safeRoot);
            SyncTouchModeSurfaces();
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
            var catchPanel = Panel(root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(260, 260), new Color(0f, 0f, 0f, 0f));
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

            public void OnPointerUp(PointerEventData eventData)
            {
                BaseRect.anchoredPosition = RestCenter;
                NubRect.anchoredPosition = Vector2.zero;
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
                var deathContext = _bossAliveAtDeath
                    ? "보스전에서 밀려났다"
                    : Time.unscaledTime - _recentHazardTime <= 2f
                        ? "위험 지대에 잠식됐다"
                        : "군단에 함락됐다";
                _finalText.text =
                    $"점수 {digest.Score:N0} • 유물 {digest.Relics} • 처치 {digest.Kills}\n" +
                    $"{deathContext} • 웨이브 {digest.Wave} 도달";
                _gameOverPanel.SetActive(true);
            }
            if ((events & SimEvents.WaveStarted) != 0 && _gameOverPanel.activeSelf)
                _gameOverPanel.SetActive(false);

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

            SyncJuice(sim);

            if (_gameOverPanel.activeSelf && sim.Mode != SimMode.GameOver)
            {
                _gameOverPanel.SetActive(false);
                // Restart landed on wave 1 again — reseed the opening lore.
                _loreText.text = LoreBeats[(sim.Wave - 1) % LoreBeats.Length];
                _loreTimer = 6f;
            }
        }

        // Per-frame juice decay: vignette pulse (#9), lantern flicker (#15),
        // cast flash (#10), wave banner (#20), level toast/punch (#19).
        void SyncJuice(ISimSnapshot sim)
        {
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

        void SyncBossIntro()
        {
            if (_bossIntroTimer <= 0f) return;
            _bossIntroTimer -= Time.deltaTime;
            var elapsed = BossIntroDuration - _bossIntroTimer;
            var slide = ViewPrefs.ReducedMotion
                ? 1f
                : elapsed < 0.45f
                    ? Mathf.SmoothStep(0f, 1f, elapsed / 0.45f)
                    : _bossIntroTimer < 0.45f
                        ? Mathf.SmoothStep(0f, 1f, _bossIntroTimer / 0.45f)
                        : 1f;
            var alpha = _bossIntroTimer < 0.2f
                ? Mathf.Clamp01(_bossIntroTimer / 0.2f)
                : Mathf.Clamp01(elapsed / 0.2f);
            SetBossIntroState(slide, alpha);
            if (_bossIntroTimer <= 0f)
            {
                _letterboxTop.enabled = false;
                _letterboxBottom.enabled = false;
                _bossIntroPlate.color = new Color(1f, 0.83f, 0.45f, 0f);
            }
        }

        void SyncStageClearCeremony()
        {
            if (!_stageClearPending) return;
            _stageClearTimer -= Time.deltaTime;
            var elapsed = 0.9f - _stageClearTimer;
            var progress = Mathf.Clamp01(elapsed / 0.8f);
            var score = Mathf.RoundToInt(Mathf.Lerp(0f, _stageClearFinalScore, progress));
            var relics = Mathf.RoundToInt(Mathf.Lerp(0f, _stageClearFinalRelics, progress));
            if (score != _stageClearShownScore || relics != _stageClearShownRelics)
            {
                _stageClearShownScore = score;
                _stageClearShownRelics = relics;
                _stageClearBanner.text = $"스테이지 클리어\n점수 {score:N0} • 유물 {relics}";
            }
            var alpha = _stageClearTimer <= 0.2f
                ? Mathf.Clamp01(_stageClearTimer / 0.2f)
                : Mathf.Clamp01(elapsed / 0.2f);
            _stageClearBanner.color = new Color(1f, 0.83f, 0.45f, alpha);
            var punch = Mathf.Clamp01(elapsed / 0.2f);
            _stageClearBanner.rectTransform.localScale = ViewPrefs.TimeEffectsAllowed
                ? Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, punch))
                : Vector3.one;
            var flashAlpha = Mathf.Clamp01(1f - elapsed / 0.5f) * 0.38f * ViewPrefs.MotionScale;
            if (flashAlpha > 0.001f)
            {
                _stageClearFlash.enabled = true;
                _stageClearFlash.color = new Color(1f, 0.83f, 0.45f, flashAlpha);
            }
            else _stageClearFlash.enabled = false;
            if (_stageClearTimer > 0f) return;

            _stageClearPending = false;
            _stageClearBanner.text = string.Empty;
            _stageClearBanner.color = new Color(1f, 0.83f, 0.45f, 0f);
            _stageClearBanner.rectTransform.localScale = Vector3.one;
            _stageClearText.text =
                $"점수 {_stageClearFinalScore:N0} • 유물 {_stageClearFinalRelics}";
            _stageClearPanel.SetActive(true);
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
                              float maxCooldown, bool affordable)
        {
            overlay.fillAmount = cooldown / maxCooldown;
            group.alpha = affordable ? 1f : 0.45f;
        }
    }
}
