// §Lane V4 quality gate — URP post processing (bloom + vignette) behind a
// RUNTIME frame watchdog.
//
// The spec's rule for V4 is "프로파일 수치 첨부 없이 PASS 불가": post ships only
// against a measured p95 under 16.7 ms. Two things make a build-time number
// insufficient on its own:
//
//   1. The measurement that produced "desktop p95 10.0 ms" was taken against a
//      profile whose Bloom and Vignette serialized as {fileID: 0} — post was
//      inert, so that number measured a build WITHOUT the effects. SceneBuilder
//      fixed the sub-asset parenting; the old figure does not carry over.
//   2. WebGL has no device tier to look up. The same build runs on a desktop
//      with headroom and on a phone browser with none, and Application
//      .isMobilePlatform does not separate them reliably in a browser.
//
// So the gate is measured where it matters — in the frame loop, on the actual
// device — and degrades itself. Downgrade is ONE-WAY within a session: a gate
// that re-enables at the first calm stretch re-enters the stall it just left
// and oscillates, which reads worse than plainly not having the effect.
//
// p95 without a sort: "p95 frame time exceeds the budget" is the same statement
// as "more than 5% of the frames in the window exceed the budget", so the
// watchdog keeps a ring buffer of over-budget FLAGS and one running count. That
// is O(1) per frame and allocates nothing after Awake — a watchdog that sorts
// 120 floats every frame would be measuring its own overhead.
//
// Decoration only: nothing in the sim, the input path or the digest reads any
// of this.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CinderCourt.View
{
    [RequireComponent(typeof(Camera))]
    public sealed class PostFxGate : MonoBehaviour
    {
        /// <summary>What the watchdog has concluded so far. Readable for
        /// reporting; the orchestrator's smoke pass reads it off the log line.</summary>
        public enum Status
        {
            /// <summary>Platform tier ruled post out before any measurement.</summary>
            OffByPlatform,
            /// <summary>Warm-up or an unfilled window — no verdict yet.</summary>
            Measuring,
            /// <summary>Window is full and p95 sits inside the budget.</summary>
            Holding,
            /// <summary>p95 breached the budget for the hold window; post is off.</summary>
            Degraded,
        }

        /// <summary>Exactly one watchdog owns an admitted frame in a stage
        /// epoch. PostFx measures the combined scene first; only a persistent
        /// post-off decision transfers ownership to the shadow ladder.</summary>
        public enum MeasurementOwner
        {
            None,
            PostFx,
            Shadow,
        }

        // ---- [TARGET] watchdog parameters ----------------------------------
        /// <summary>Spec §V4 gate: 60 Hz frame budget.</summary>
        public const float FrameBudgetSeconds = 1f / 60f;

        /// <summary>Window length. 120 frames ≈ 2 s at 60 Hz: long enough that a
        /// single GC hitch cannot carry it, short enough that a genuinely
        /// over-budget stretch is caught inside one wave rather than one run.</summary>
        public const int WindowFrames = 120;

        /// <summary>p95 ⇒ at most 5% of the window may exceed the budget. With
        /// WindowFrames 120 the trip point is 7 frames (6 is exactly 5%).</summary>
        public const float OverBudgetFraction = 0.05f;

        /// <summary>Frames ignored after enable. Scene build, shader warm-up and
        /// the first StaticBatchingUtility.Combine all land here and none of them
        /// is steady-state frame cost.</summary>
        public const float WarmupSeconds = 3f;

        /// <summary>How long the breach must persist before post is dropped. A
        /// breach that clears inside this window was a hitch, not a tier.</summary>
        public const float HoldSeconds = 1.5f;

        /// <summary>Samples above this are DISCARDED rather than counted. A
        /// backgrounded browser tab, an alt-tab, or a synchronous asset load
        /// produce multi-second deltas that say nothing about render cost, and
        /// counting them would degrade every build that ever lost focus.</summary>
        public const float StallCeilingSeconds = 0.5f;

        /// <summary>Trip count derived from the two constants above.</summary>
        public static int OverBudgetTrip => (int)(WindowFrames * OverBudgetFraction) + 1;

        // ---- state ----------------------------------------------------------
        /// <summary>Live verdict. Static so a report or a debug overlay can read
        /// it without holding the component; there is exactly one game camera.</summary>
        public static Status Current { get; private set; } = Status.Measuring;

        /// <summary>Over-budget frames currently inside the window.</summary>
        public static int OverBudgetInWindow { get; private set; }

        /// <summary>Frames actually admitted to the window (stalls excluded).</summary>
        public static int SamplesInWindow { get; private set; }

        /// <summary>One-line watchdog state for logs and debug overlays. Built
        /// only on a transition — never per frame.</summary>
        public static string DebugLine { get; private set; } = "postfx: measuring";

        public static int StageEpoch { get; private set; }
        public static MeasurementOwner CurrentMeasurementOwner { get; private set; }

        static PostFxGate _instance;
        UniversalAdditionalCameraData _data;
        bool[] _over;              // ring of over-budget flags
        int _cursor;
        float _warmup;
        float _breachHeld;
        bool _stageWantsPost;
        bool _platformAllows = true;

        void Awake() => Initialize();

        void Initialize()
        {
            _instance = this;
            _data = GetComponent<UniversalAdditionalCameraData>();
            if (_data == null) return;
            _over = new bool[WindowFrames];
            _warmup = WarmupSeconds;
            if (Application.isMobilePlatform)
            {
                // Unmeasured tier: the spec's rule is degrade, not
                // ship-and-hope, so this one stays a static decision.
                _platformAllows = false;
                SetStatus(Status.OffByPlatform, "postfx: off (mobile tier)");
            }
            Apply();
        }

        internal void InitializeForTests() => Initialize();

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// §V4 scope: post is DUNGEON-ONLY. The volume is global in the scene, so
        /// without this the lobby, the arena and the prologue would inherit bloom
        /// and vignette the moment the profile's sub-asset fix made them render —
        /// and the arena/prologue framing is contract-frozen, so silently
        /// restyling them is not this lane's change to make.
        ///
        /// Called from GameDirector.SetStageEnvironment, which already runs on
        /// exactly the dungeon-enter / everything-else split.
        /// </summary>
        public static void SetStageActive(bool dungeon)
        {
            if (_instance == null) return;
            if (!dungeon)
            {
                if (!_instance._stageWantsPost
                    && CurrentMeasurementOwner == MeasurementOwner.None)
                    return;
                _instance._stageWantsPost = false;
                CurrentMeasurementOwner = MeasurementOwner.None;
                _instance.ResetWindow();
                _instance.Apply();
                return;
            }

            _instance._stageWantsPost = true;
            StageEpoch++;
            _instance.ResetWindow();

            // Holding belongs to the stage that produced it and must never be
            // reused after a workload transition. Session-latched/capability
            // decisions remain one-way and start a shadow-owned epoch instead.
            if (!_instance._platformAllows || Current == Status.OffByPlatform)
            {
                CurrentMeasurementOwner = MeasurementOwner.Shadow;
            }
            else if (Current == Status.Degraded)
            {
                CurrentMeasurementOwner = MeasurementOwner.Shadow;
            }
            else
            {
                SetStatus(Status.Measuring, "postfx: measuring");
                CurrentMeasurementOwner = MeasurementOwner.PostFx;
            }
            _instance.Apply();
            if (CurrentMeasurementOwner == MeasurementOwner.Shadow)
                StageShadowPolicy.BeginShadowMeasurementEpoch(StageEpoch);
        }

        void ResetWindow()
        {
            _warmup = WarmupSeconds;
            _breachHeld = 0f;
            _cursor = 0;
            OverBudgetInWindow = 0;
            SamplesInWindow = 0;
            if (_over != null) System.Array.Clear(_over, 0, _over.Length);
        }

        void Apply()
        {
            if (_data == null) return;
            _data.renderPostProcessing =
                _platformAllows && _stageWantsPost && Current != Status.Degraded;
        }

        void Update()
        {
            // Measure only while post is actually rendering: a window sampled
            // with the effects off would report headroom that the effects then
            // spend, which is the exact failure the pre-existing "desktop p95
            // 10.0 ms" figure already made once.
            if (_data == null || !_platformAllows || !_stageWantsPost
                || Current == Status.Degraded) return;

            var delta = Time.unscaledDeltaTime;
            if (delta <= 0f || delta > StallCeilingSeconds) return;   // not render cost

            if (_warmup > 0f)
            {
                _warmup -= delta;
                return;
            }

            // Ring push: evict the outgoing flag from the count before writing.
            if (SamplesInWindow >= WindowFrames && _over[_cursor]) OverBudgetInWindow--;
            var over = delta > FrameBudgetSeconds;
            _over[_cursor] = over;
            if (over) OverBudgetInWindow++;
            _cursor = _cursor + 1 >= WindowFrames ? 0 : _cursor + 1;
            if (SamplesInWindow < WindowFrames) SamplesInWindow++;

            // No verdict until the window is genuinely full.
            if (SamplesInWindow < WindowFrames)
            {
                if (Current != Status.Measuring) SetStatus(Status.Measuring, "postfx: measuring");
                return;
            }

            if (OverBudgetInWindow >= OverBudgetTrip)
            {
                _breachHeld += delta;
                if (_breachHeld >= HoldSeconds) Degrade();
                return;
            }

            _breachHeld = 0f;
            if (Current != Status.Holding)
                SetStatus(Status.Holding,
                    $"postfx: holding ({OverBudgetInWindow}/{WindowFrames} over "
                    + $"{FrameBudgetSeconds * 1000f:F1} ms, trip {OverBudgetTrip})");
        }

        void Degrade()
        {
            SetStatus(Status.Degraded, string.Empty);
            Apply();
            SetStatus(Status.Degraded,
                $"postfx: DEGRADED — {OverBudgetInWindow}/{WindowFrames} frames over "
                + $"{FrameBudgetSeconds * 1000f:F1} ms held {HoldSeconds:F1} s; "
                + "bloom+vignette disabled for this session");
            CurrentMeasurementOwner = MeasurementOwner.Shadow;
            StageShadowPolicy.BeginShadowMeasurementEpoch(StageEpoch);
            Debug.Log(DebugLine);
        }

        static void SetStatus(Status status, string line)
        {
            Current = status;
            DebugLine = line;
        }

        /// <summary>
        /// Test/harness seam: clears the static verdict so one fixture's
        /// measurement cannot decide the next one's. NOT called by the game —
        /// the degrade is one-way within a session by design.
        /// </summary>
        internal static void ResetWatchdogForTests()
        {
            Current = Status.Measuring;
            OverBudgetInWindow = 0;
            SamplesInWindow = 0;
            DebugLine = "postfx: measuring";
            StageEpoch = 0;
            CurrentMeasurementOwner = MeasurementOwner.None;
            if (_instance != null)
            {
                _instance._platformAllows = true;
                _instance._stageWantsPost = false;
                _instance.ResetWindow();
                _instance.Apply();
            }
        }

        internal static void SeedPersistentStateForTests(Status status)
        {
            Current = status;
            if (_instance == null) return;
            _instance._platformAllows = status != Status.OffByPlatform;
            _instance.ResetWindow();
            _instance.Apply();
        }

        /// <summary>
        /// Pure decision function, exercisable without a camera or a frame loop:
        /// does a window with this many over-budget frames breach the p95 gate?
        /// </summary>
        internal static bool WindowBreaches(int overBudgetFrames, int samples)
            => samples >= WindowFrames && overBudgetFrames >= OverBudgetTrip;
    }
}
