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
// in Resources. Real failures arrive on errorReceived and finish immediately;
// PrepareTimeout is only the backstop for a prepare that neither completes nor
// errors, and it is deliberately long because that case is a slow download, not
// a broken clip. Either way the view gives control back to the caller.
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

        /// <summary>StreamingAssets-relative act cinematics, played from the
        /// lobby when a clear ends an act (catalog index 2 / 5 / 8).
        ///
        /// Each is that act's CLOSING stage art in motion — the chancel going
        /// quiet, the verdict settling, the march collapsing — so the beat
        /// reads as the place the player just left rather than a new one.
        ///
        /// Lobby, not the clear itself: the victory card is still up when
        /// StageCleared fires and the scene is still live. EnterLobby runs
        /// after _game.EndRun(), which is the same "nothing underneath" test
        /// the boot reels had to pass.</summary>
        public const string Act1ClipRelativePath = "Video/cinder-court-act1.mp4";
        public const string Act2ClipRelativePath = "Video/cinder-court-act2.mp4";
        public const string Act3ClipRelativePath = "Video/cinder-court-act3.mp4";

        const float FadeOutSeconds = 0.6f;

        /// <summary>
        /// How long to wait for <c>VideoPlayer.Prepare()</c>. RAISED 4 -> 20 s.
        ///
        /// 4 s made the intro INTERMITTENT, and the mechanism is that it was a
        /// wall-clock race against a download. On WebGL the clip streams from a URL
        /// (StreamingAssets, ~1.6 MB for the boot reel); a warm browser cache wins
        /// that race and a cold one loses it, so the same build played the reel or
        /// skipped it depending on nothing the player could see or control.
        ///
        /// The distinction this constant was being asked to make — "broken" versus
        /// "slow" — is not a distinction a stopwatch can draw. A genuinely broken
        /// clip (404, undecodable container) reports through <c>errorReceived</c>,
        /// which calls Finish immediately and is unaffected by this value. What is
        /// left for the timeout is only the case where prepare neither completes nor
        /// errors, and there the right answer is to keep waiting: the skip hint is on
        /// screen the whole time, so a player who does not want to wait has an exit.
        /// </summary>
        internal const float PrepareTimeout = 20f;

        /// <summary>
        /// Play() issued but playback never began. RAISED 2 -> 6 s, same reasoning
        /// one stage later: by this point the data is prepared, so 6 s is generous
        /// for a first decode rather than a race, and a stall here still lands on the
        /// fade rather than hanging.
        /// </summary>
        internal const float PlayStartTimeout = 6f;

        /// <summary>
        /// Hard watchdog. RAISED 20 -> 30 s so it stays a backstop and not a second
        /// timeout: the longest clip is ~6.6 s, and 20 could be reached legitimately
        /// by a slow start plus a full playthrough now that PlayStartTimeout is 6.
        /// </summary>
        internal const float MaxPlaySeconds = 30f;

        /// <summary>
        /// How long playback must stay stopped before the polled path calls it an
        /// ending. Covers a rebuffer without letting a real end linger.
        /// </summary>
        internal const float StallGraceSeconds = 4f;

        /// <summary>
        /// Web browsers throttle or suspend background tabs. The first frame
        /// after focus returns can therefore carry a very large unscaled delta;
        /// counting that one frame verbatim would consume the prepare/play
        /// watchdog and skip a healthy clip before the browser can resume it.
        /// Watchdogs advance in bounded foreground-sized steps instead.
        /// </summary>
        internal const float MaxWatchdogStep = 0.25f;

        /// <summary>Time tolerance for the polled end-of-media fallback.</summary>
        internal const double MediaEndTolerance = 0.05d;

        /// <summary>Fade duration, published so tests can derive their own budgets.</summary>
        internal const float FadeOutSecondsForTest = FadeOutSeconds;
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
        float _notPlayingFor;
        // Sequence state. Null when a single clip is playing, which is every
        // path except the boot route.
        Beat[] _queue;
        int _queueIndex;
        bool _skipRequested;
        // Watcher caption under the picture. Built with the surface, hidden
        // whenever a beat carries no line.
        Text _narration;

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
        public void Play(string clipRelativePath) => Play(clipRelativePath, null);

        /// <summary>
        /// Unambiguous one-argument entry point for engine message bridges.
        /// Unity SendMessage cannot reliably choose between Play() and
        /// Play(string), so browser automation and other string-only bridges
        /// use this name while ordinary C# callers keep the overloads above.
        /// </summary>
        public void PlayClip(string clipRelativePath) => Play(clipRelativePath, null);

        /// <summary>
        /// Plays a clip with an optional narration caption beneath it.
        ///
        /// The caption is what makes a generated reel a SCENE rather than
        /// footage: the watcher narrates every stage opening in second person
        /// (StoryCatalog.StageStart), and the boot reels were the only story
        /// beats in the game carrying no voice at all.
        ///
        /// Text, not audio. WebGL browsers block autoplay of media WITH an
        /// audio track until a user gesture, and the boot reel starts before
        /// any click — a narration track would be dropped by Safari with
        /// nothing to show for it. A caption always lands.
        /// </summary>
        public void Play(string clipRelativePath, string narration)
        {
            CancelPlayback();
            StartClip(clipRelativePath, narration);
        }

        /// <summary>Starts one clip without discarding the sequence that owns it.</summary>
        void StartClip(string clipRelativePath, string narration)
        {
            EnsureBuilt();

            // A single VideoPlayer is intentionally reused, but its previous
            // URL decoder is not. Stop also cancels an outstanding Prepare;
            // without it, a late error from the old request can arrive while
            // the next clip is already preparing and advance the new queue.
            _phase = Phase.Idle;
            if (_player != null) _player.Stop();

            _finishedFired = false;
            _playbackObserved = false;
            _notPlayingFor = 0f;
            _phaseElapsed = 0f;
            _fadeRemaining = FadeOutSeconds;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _canvas.gameObject.SetActive(true);
            _phase = Phase.Preparing;
            SetNarration(narration);

            if (_player == null)
            {
                Finish();
                return;
            }

            _player.url = UrlFor(clipRelativePath);
            _player.Prepare();
        }

        /// <summary>Shows or clears the caption. Empty hides the row rather
        /// than leaving a blank strip over the picture.</summary>
        void SetNarration(string narration)
        {
            if (_narration == null) return;
            var has = !string.IsNullOrEmpty(narration);
            _narration.text = has ? narration : string.Empty;
            _narration.gameObject.SetActive(has);
        }

        /// <summary>Absolute URL for any StreamingAssets-relative clip.</summary>
        public static string UrlFor(string clipRelativePath) =>
            System.IO.Path.Combine(Application.streamingAssetsPath, clipRelativePath);

        /// <summary>One reel of a boot sequence: the clip and the line the
        /// watcher speaks over it. A null or empty caption means picture
        /// only — the brand logo has nothing to narrate.</summary>
        public readonly struct Beat
        {
            public readonly string Clip;
            public readonly string Narration;
            public Beat(string clip, string narration = null)
            {
                Clip = clip;
                Narration = narration;
            }
        }

        /// <summary>
        /// Plays several beats back to back through the one surface, raising
        /// <see cref="OnFinished"/> ONCE when the last one leaves the screen.
        ///
        /// Firing per clip would break the caller's contract
        /// (IntroVideoViewTests pins exactly-once). A clip that is missing or
        /// will not decode ends early through the existing timeout/error paths
        /// and the next one starts, so a broken file costs its own slot and
        /// nothing more.
        /// </summary>
        public void PlaySequence(params Beat[] beats)
        {
            if (beats == null || beats.Length == 0)
            {
                Play();
                return;
            }
            CancelPlayback();
            _queue = beats;
            _queueIndex = 0;
            _skipRequested = false;
            StartClip(_queue[0].Clip, _queue[0].Narration);
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
            CancelPlayback();
        }

        /// <summary>
        /// Silently cancels both the decoder and its logical queue. Phase is
        /// cleared before Stop so any platform callback already queued for the
        /// old URL is ignored by the phase guards below.
        /// </summary>
        void CancelPlayback()
        {
            _phase = Phase.Idle;
            _phaseElapsed = 0f;
            _playbackObserved = false;
            _notPlayingFor = 0f;
            _queue = null;
            _queueIndex = 0;
            _skipRequested = false;
            if (_player != null) _player.Stop();
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
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) dt = 0f;
            else if (dt > MaxWatchdogStep) dt = MaxWatchdogStep;
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

                    // A SINGLE not-playing sample is not an ending. On WebGL the
                    // clip is streamed, frameCount is commonly 0 (unknown for a
                    // stream, so lastFrame above can never fire), and a mid-clip
                    // rebuffer drops isPlaying for a moment. Treating that instant as
                    // "finished" cuts the reel off wherever the network happened to
                    // stutter — the second half of the same intermittency the
                    // PrepareTimeout raise addresses, one phase later.
                    //
                    // loopPointReached is the authoritative end for a non-looping clip
                    // and already calls BeginFadeOut, so this polled path only has to
                    // catch the case where that callback never arrives. Requiring the
                    // stall to persist keeps it as a fallback instead of a hair trigger.
                    if (_playbackObserved && !_player.isPlaying) _notPlayingFor += dt;
                    else _notPlayingFor = 0f;

                    var mediaEnded = IsAtMediaEnd(_player.time, _player.length);
                    var ended = lastFrame || mediaEnded
                        || (_playbackObserved
                            ? _notPlayingFor >= StallGraceSeconds
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
            CancelPlayback();
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
            StartClip(_queue[_queueIndex].Clip, _queue[_queueIndex].Narration);
            return true;
        }

        internal static bool IsAtMediaEnd(double time, double length)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) ||
                double.IsNaN(length) || double.IsInfinity(length) || length <= 0d)
                return false;
            return time >= System.Math.Max(0d, length - MediaEndTolerance);
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
            _narration = MakeNarration(canvasObject.transform);

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
            if (source != _player || (_phase != Phase.Preparing && _phase != Phase.Playing))
                return;
            Debug.LogWarning($"[IntroVideo] playback failed, skipping: {message}");
            Finish();
        }

        /// <summary>End of the (non-looping) clip — start the fade immediately
        /// instead of waiting for the polled end conditions in Step.</summary>
        void OnClipEnded(VideoPlayer source)
        {
            if (source == _player && _phase == Phase.Playing)
                BeginFadeOut();
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

        /// <summary>Watcher caption row, bottom-centre above the skip hint.
        ///
        /// Wrapping is ON and the row is wide but shallow: these lines are one
        /// sentence each, and a caption that silently overflows its rect is the
        /// same class of defect as the guidance tab that ran 238u past its
        /// panel (CLAUDE.md §4m) — the string reads correct and only the
        /// geometry is wrong.</summary>
        Text MakeNarration(Transform parent)
        {
            var font = ViewTypography.ResolveFont();

            var obj = new GameObject("Narration");
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            ViewTypography.Configure(text, font, 22, TextAnchor.LowerCenter);
            text.color = new Color(0.93f, 0.9f, 0.84f, 0.94f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 74f);
            rect.sizeDelta = new Vector2(860f, 64f);
            obj.SetActive(false);
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
