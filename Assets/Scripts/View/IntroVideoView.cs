// Branded boot intro video — replaces the plain engine loading screen with the
// game-concept / brand reel (docs/provenance/intro-video.json).
//
// Contract, mirroring CutsceneView:
//  * one lazily built full-screen ScreenSpaceOverlay canvas, reused;
//  * UNSCALED time only, so nothing in the sim can stall the boot fade;
//  * every failure degrades to "finish immediately" and never throws — a
//    missing/undecodable video must never brick the boot route.
//
// WebGL note: VideoPlayer on WebGL can only stream from a URL, so the clip
// lives in StreamingAssets (relative URL, per the deploy contract) rather than
// in Resources. If the player cannot prepare within PrepareTimeout the view
// gives up and hands control back to the caller.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CinderCourt.View
{
    public sealed class IntroVideoView : MonoBehaviour
    {
        /// <summary>StreamingAssets-relative path of the brand reel.</summary>
        public const string ClipRelativePath = "Video/cinder-court-intro.mp4";

        /// <summary>StreamingAssets-relative path of the concept reel — the
        /// game's own key art in motion (lantern-bearer on the span over the
        /// ember court), generated 2026-08-10. It follows the brand reel on the
        /// boot route so the first thing a new player sees after the logo is
        /// the premise, not a menu.</summary>
        public const string ConceptClipRelativePath = "Video/cinder-court-concept.mp4";

        /// <summary>StreamingAssets-relative path of the threat reel — the boss
        /// key art in motion, third beat of the first-run sequence.
        ///
        /// It lives on the BOOT route on purpose. The two obvious homes were
        /// the boss-entrance beat and the stage-entry cutscene, and both sit
        /// over LIVE PLAY: GameDirector calls _game.Begin BEFORE showing the
        /// stage cutscene, and the boss beat fires mid-fight. Five seconds of
        /// overlay in either place is five seconds of a running sim the player
        /// cannot see. Boot is the one moment nothing runs underneath.</summary>
        public const string ThreatClipRelativePath = "Video/cinder-court-threat.mp4";

        const float FadeOutSeconds = 0.6f;
        const float PrepareTimeout = 4f;    // give up if the browser will not decode
        const float PlayStartTimeout = 2f;  // Play() issued but playback never began
        const float MaxPlaySeconds = 20f;   // hard watchdog, clip is ~6.6 s
        const int SortingOrder = 520;       // above CutsceneView (500)
        const int TextureWidth = 1280;
        const int TextureHeight = 720;

        enum Phase { Idle, Preparing, Playing, FadingOut }

        Canvas _canvas;
        CanvasGroup _group;
        RawImage _surface;
        Text _skipHint;
        VideoPlayer _player;
        RenderTexture _target;

        Phase _phase = Phase.Idle;
        float _phaseElapsed;
        float _fadeRemaining;
        bool _finishedFired;
        bool _playbackObserved;
        // Sequence state. Null when a single clip is playing, which is every
        // path except the boot route.
        string[] _queue;
        int _queueIndex;
        bool _skipRequested;

        /// <summary>Raised once when the intro leaves the screen (completed or skipped).</summary>
        public System.Action OnFinished;

        /// <summary>True while the intro is preparing, playing, or fading out.</summary>
        public bool Active => _phase != Phase.Idle;

        /// <summary>Absolute URL of the boot reel.</summary>
        public static string ClipUrl => UrlFor(ClipRelativePath);

        /// <summary>
        /// Starts the brand intro. Safe to call when the clip is absent — the
        /// view then finishes on the next Step and the caller proceeds.
        /// </summary>
        public void Play() => Play(ClipRelativePath);

        /// <summary>
        /// Plays any StreamingAssets-relative clip through the same surface.
        ///
        /// The boot reel and a story beat need exactly the same machinery —
        /// url-source streaming (the only WebGL option), a prepare timeout, an
        /// error handler, and a finish-immediately fallback when the file is
        /// missing. Rather than a second view that would have to re-earn all
        /// four, this one takes the path. The caller supplies the clip; every
        /// failure mode is already the intro's.
        ///
        /// Deliberately NOT routed through CutsceneView: that view is the run's
        /// loading mask (DefaultHold 2.6 s, Image/Sprite surface) and a video
        /// there would be cut to its first third while making the load it
        /// masks measurably longer.
        /// </summary>
        public void Play(string clipRelativePath)
        {
            EnsureBuilt();

            _finishedFired = false;
            _playbackObserved = false;
            _phaseElapsed = 0f;
            _fadeRemaining = FadeOutSeconds;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _canvas.gameObject.SetActive(true);
            _phase = Phase.Preparing;

            if (_player == null)
            {
                Finish();
                return;
            }

            _player.url = UrlFor(clipRelativePath);
            _player.Prepare();
        }

        /// <summary>Absolute URL for any StreamingAssets-relative clip.</summary>
        public static string UrlFor(string clipRelativePath) =>
            System.IO.Path.Combine(Application.streamingAssetsPath, clipRelativePath);

        /// <summary>
        /// Plays several StreamingAssets clips back to back through the one
        /// surface, raising <see cref="OnFinished"/> ONCE when the last one
        /// leaves the screen.
        ///
        /// The boot route uses this for brand reel -> concept reel. Firing per
        /// clip would break the caller's contract (IntroVideoViewTests pins
        /// exactly-once). A clip that is missing or will not decode ends early
        /// through the existing timeout/error paths and the next one starts,
        /// so a broken file costs its own slot and nothing more.
        /// </summary>
        public void PlaySequence(params string[] clipRelativePaths)
        {
            if (clipRelativePaths == null || clipRelativePaths.Length == 0)
            {
                Play();
                return;
            }
            _queue = clipRelativePaths;
            _queueIndex = 0;
            _skipRequested = false;
            Play(_queue[0]);
        }

        /// <summary>Player-driven skip (any key / tap) — fades out from wherever we are.</summary>
        public void Skip()
        {
            if (_phase == Phase.Idle || _phase == Phase.FadingOut) return;
            // Skip means skip the INTRO, not advance to the next clip: a player
            // who taps through the brand reel does not want the concept reel.
            _skipRequested = true;
            BeginFadeOut();
        }

        /// <summary>Tears the intro down instantly (mode switches, resets).</summary>
        public void Hide()
        {
            _phase = Phase.Idle;
            _phaseElapsed = 0f;
            if (_player != null && _player.isPlaying) _player.Stop();
            if (_canvas != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _canvas.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (_phase == Phase.Idle) return;
            if (AnySkipPressedThisFrame()) Skip();
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>Any-key/tap skip read through the Input System package —
        /// the project switched active input handling, so the legacy
        /// UnityEngine.Input class throws on every access. Every device is
        /// null-guarded because batchmode has no attached devices.</summary>
        static bool AnySkipPressedThisFrame()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>State machine with dt injected so EditMode can drive it
        /// (Time.unscaledDeltaTime reports ~0 in batchmode).</summary>
        internal void Step(float dt)
        {
            if (_phase == Phase.Idle) return;
            _phaseElapsed += dt;

            switch (_phase)
            {
                case Phase.Preparing:
                    if (_player != null && _player.isPrepared)
                    {
                        _phase = Phase.Playing;
                        _phaseElapsed = 0f;
                        _player.Play();
                    }
                    else if (_phaseElapsed >= PrepareTimeout)
                    {
                        Finish();   // undecodable / missing clip — do not block boot
                    }
                    break;

                case Phase.Playing:
                    if (_player == null)
                    {
                        BeginFadeOut();
                        break;
                    }

                    // VideoPlayer.Play() is asynchronous: isPlaying stays false
                    // for a few frames after the call, so "not playing" only
                    // means "finished" once playback was actually observed.
                    if (_player.isPlaying || _player.frame > 0) _playbackObserved = true;

                    // The last frame is the end: a non-looping VideoPlayer can
                    // sit on it with isPlaying still true (observed in the
                    // Editor), which would otherwise hold the intro on screen
                    // until the watchdog.
                    var lastFrame = _player.frameCount > 0
                                    && _player.frame >= (long)_player.frameCount - 1;

                    var ended = lastFrame
                        || (_playbackObserved
                            ? !_player.isPlaying
                            : _phaseElapsed >= PlayStartTimeout);
                    if (ended || _phaseElapsed >= MaxPlaySeconds) BeginFadeOut();
                    break;

                case Phase.FadingOut:
                    _fadeRemaining -= dt;
                    if (_fadeRemaining <= 0f)
                    {
                        Finish();
                        return;
                    }
                    _group.alpha = Mathf.Clamp01(_fadeRemaining / FadeOutSeconds);
                    break;
            }
        }

        void BeginFadeOut()
        {
            _phase = Phase.FadingOut;
            _phaseElapsed = 0f;
            _fadeRemaining = FadeOutSeconds;
        }

        void Finish()
        {
            // Advance BEFORE Hide: Hide deactivates the canvas, and doing that
            // between two clips of one sequence would blink the surface off
            // and straight back on. Play() re-arms the per-clip state itself.
            if (AdvanceQueue()) return;
            Hide();
            if (_finishedFired) return;
            _finishedFired = true;
            OnFinished?.Invoke();
        }

        /// <summary>Starts the next queued clip; false when the queue is spent
        /// or the player skipped out of the whole sequence.</summary>
        bool AdvanceQueue()
        {
            if (_queue == null) return false;
            _queueIndex++;
            if (_skipRequested || _queueIndex >= _queue.Length)
            {
                _queue = null;
                return false;
            }
            Play(_queue[_queueIndex]);
            return true;
        }

        // ------------------------------------------------------------- build --
        void EnsureBuilt()
        {
            if (_canvas != null) return;

            var canvasObject = new GameObject("IntroVideo");
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

            // Opaque backdrop: letterbox bars stay black, never show the scene.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(canvasObject.transform, false);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = Color.black;
            backdropImage.raycastTarget = true;
            Stretch(backdrop.GetComponent<RectTransform>());

            _target = new RenderTexture(TextureWidth, TextureHeight, 0)
            {
                name = "IntroVideoTarget",
            };

            var surface = new GameObject("Surface");
            surface.transform.SetParent(canvasObject.transform, false);
            _surface = surface.AddComponent<RawImage>();
            _surface.texture = _target;
            _surface.raycastTarget = false;
            Stretch(_surface.rectTransform);

            _skipHint = MakeSkipHint(canvasObject.transform);

            _player = canvasObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.Url;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.isLooping = false;
            _player.waitForFirstFrame = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.errorReceived += OnVideoError;
            _player.loopPointReached += OnClipEnded;

            canvasObject.SetActive(false);
        }

        void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[IntroVideo] playback failed, skipping: {message}");
            Finish();
        }

        /// <summary>End of the (non-looping) clip — start the fade immediately
        /// instead of waiting for the polled end conditions in Step.</summary>
        void OnClipEnded(VideoPlayer source)
        {
            if (_phase == Phase.Preparing || _phase == Phase.Playing) BeginFadeOut();
        }

        void OnDestroy()
        {
            if (_player != null)
            {
                _player.errorReceived -= OnVideoError;
                _player.loopPointReached -= OnClipEnded;
            }
            if (_target != null) _target.Release();
        }

        Text MakeSkipHint(Transform parent)
        {
            var font = ViewTypography.ResolveFont();

            var obj = new GameObject("SkipHint");
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            ViewTypography.Configure(text, font, 16, TextAnchor.LowerRight);
            text.text = "아무 키나 눌러 건너뛰기";
            text.color = new Color(0.78f, 0.82f, 0.9f, 0.6f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-28f, 24f);
            rect.sizeDelta = new Vector2(320f, 24f);
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
