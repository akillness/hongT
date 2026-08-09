using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameFlowAgentBridge : MonoBehaviour
    {
        const float PublishInterval = 0.1f;
        const float ActionHoldSeconds = 0.28f;
        const int MaxActionsPerFrame = 8;
        const int ActionBufferSize = 64;

        static readonly byte[] ActionBuffer = new byte[ActionBufferSize];
#if !UNITY_WEBGL || UNITY_EDITOR
        static readonly Queue<string> EditorActions = new Queue<string>();
#endif

        readonly StringBuilder _json = new StringBuilder(2048);
        GameView _game;
        InputAdapter _input;
        ICinderSim _activeSim;
        float _publishTimer;
        float _elapsed;
        float _movementUntil;
        bool _holdingMovement;
        bool _actionAppliedThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameFlowAgentBridge>() != null)
                return;
            var host = new GameObject("GameFlowAgentBridge");
            DontDestroyOnLoad(host);
            host.AddComponent<GameFlowAgentBridge>();
        }

        void Update()
        {
            BindIfNeeded();
            // Run before the default-order GameView.Update so the real input
            // sampling path can consume browser actions in this render frame.
            _actionAppliedThisFrame = PollActions();
            ReleaseExpiredMovement();
        }

        void LateUpdate()
        {
            TrackRunClock();

            _publishTimer -= Time.unscaledDeltaTime;
            if (_actionAppliedThisFrame)
            {
                // GameView.Update has now sampled the queued action and advanced
                // the authoritative sim when a fixed tick was due. Publish the
                // freshest available state without the old extra-render-frame wait.
                _actionAppliedThisFrame = false;
                _publishTimer = PublishInterval;
                PublishObservation();
                return;
            }
            if (_publishTimer > 0f) return;
            _publishTimer = PublishInterval;
            PublishObservation();
        }

        void BindIfNeeded()
        {
            if (_game == null)
                _game = FindAnyObjectByType<GameView>();
            if (_input == null)
                _input = _game != null && _game.Input != null
                    ? _game.Input
                    : FindAnyObjectByType<InputAdapter>();
        }

        void TrackRunClock()
        {
            var sim = _game != null ? _game.Sim : null;
            if (!ReferenceEquals(sim, _activeSim))
            {
                _activeSim = sim;
                _elapsed = 0f;
            }

            if (sim != null && (sim.Mode == SimMode.Running || sim.Mode == SimMode.WaveClear))
                _elapsed += Mathf.Min(Time.deltaTime, SimConfig.MaxFrameDelta);
        }

        bool PollActions()
        {
            var count = GetActionCount();
            var applied = false;
            for (var i = 0; i < count && i < MaxActionsPerFrame; i++)
            {
                var action = PopAction();
                if (string.IsNullOrEmpty(action)) continue;
                ApplyAgentAction(action);
                applied = true;
            }
            return applied;
        }

        void ReleaseExpiredMovement()
        {
            if (!_holdingMovement || _input == null || Time.unscaledTime < _movementUntil)
                return;
            _input.ClearTouchState();
            _holdingMovement = false;
        }

        void PublishObservation()
        {
            PublishObservationJson(BuildObservationJson());
        }

        internal string BuildObservationJson()
        {
            _json.Length = 0;
            AppendObservation(_json, _game != null ? _game.Sim : null, _elapsed);
            return _json.ToString();
        }

        internal void BindForTests(GameView game, InputAdapter input)
        {
            _game = game;
            _input = input;
        }

        internal void ApplyAgentAction(string action)
        {
            if (_input == null)
                BindIfNeeded();
            if (_input == null || string.IsNullOrEmpty(action))
                return;

            switch (action.Trim().ToUpperInvariant())
            {
                case "MOVE_UP":
                    HoldMovement(0f, -1f);
                    break;
                case "MOVE_DOWN":
                    HoldMovement(0f, 1f);
                    break;
                case "MOVE_LEFT":
                    HoldMovement(-1f, 0f);
                    break;
                case "MOVE_RIGHT":
                    HoldMovement(1f, 0f);
                    break;
                case "MOVE_UP_LEFT":
                    HoldMovement(-1f, -1f);
                    break;
                case "MOVE_UP_RIGHT":
                    HoldMovement(1f, -1f);
                    break;
                case "MOVE_DOWN_LEFT":
                    HoldMovement(-1f, 1f);
                    break;
                case "MOVE_DOWN_RIGHT":
                    HoldMovement(1f, 1f);
                    break;
                case "ATTACK":
                    _input.QueueAttack();
                    break;
                case "CHOOSE_UPGRADE_1":
                    _input.QueueGrowthChoice(1);
                    break;
                case "CHOOSE_UPGRADE_2":
                    _input.QueueGrowthChoice(2);
                    break;
                case "CHOOSE_UPGRADE_3":
                    _input.QueueGrowthChoice(3);
                    break;
                case "PICK_UP":
                case "COLLECT_EXP":
                    SteerToNearestPickup();
                    break;
                case "RESET":
                    _elapsed = 0f;
                    _input.QueueRestart();
                    break;
                case "WAIT":
                    _input.ClearTouchState();
                    _holdingMovement = false;
                    break;
            }
        }

        void HoldMovement(float x, float y)
        {
            _input.TouchMoveX = Mathf.Clamp(x, -1f, 1f);
            _input.TouchMoveY = Mathf.Clamp(y, -1f, 1f);
            _input.TouchLeft = _input.TouchRight = _input.TouchUp = _input.TouchDown = false;
            _movementUntil = Time.unscaledTime + ActionHoldSeconds;
            _holdingMovement = true;
        }

        void SteerToNearestPickup()
        {
            var sim = _game != null ? _game.Sim : null;
            if (sim == null || sim.Pickups == null || sim.Pickups.Count == 0)
            {
                _input.ClearTouchState();
                _holdingMovement = false;
                return;
            }

            var player = sim.Player;
            var best = sim.Pickups[0];
            var bestDistance = float.MaxValue;
            for (var i = 0; i < sim.Pickups.Count; i++)
            {
                var pickup = sim.Pickups[i];
                var dx = pickup.X - player.X;
                var dy = pickup.Y - player.Y;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                best = pickup;
                bestDistance = distance;
            }

            var vx = best.X - player.X;
            var vy = best.Y - player.Y;
            var mag = Mathf.Sqrt(vx * vx + vy * vy);
            if (mag < 0.001f)
            {
                _input.ClearTouchState();
                _holdingMovement = false;
                return;
            }
            HoldMovement(vx / mag, vy / mag);
        }

        internal static void AppendObservation(StringBuilder json, ICinderSim sim, float elapsed)
        {
            json.Append('{');
            AppendStringField(json, "api_version", "gameflow_standard_v2"); json.Append(',');
            AppendStringField(json, "game_type", "survivor_like"); json.Append(',');
            AppendPlayer(json, sim); json.Append(',');
            AppendWorld(json, sim, elapsed); json.Append(',');
            AppendCombat(json, sim); json.Append(',');
            AppendResources(json, sim); json.Append(',');
            AppendUpgrade(json, sim); json.Append(',');
            AppendBoss(json, sim); json.Append(',');
            AppendStatus(json, sim);
            json.Append('}');
        }

        static void AppendPlayer(StringBuilder json, ICinderSim sim)
        {
            var player = sim != null ? sim.Player : default;
            var hack = sim as IHackSnapshot;
            var derived = sim as IDerivedStatSnapshot;
            var maxHp = derived != null ? derived.PlayerMaxHealth :
                Mathf.Max(SimConfig.PlayerMaxHealth, player.Health);
            json.Append("\"player\":{");
            AppendNumberField(json, "hp", player.Health); json.Append(',');
            AppendNumberField(json, "max_hp", maxHp); json.Append(',');
            AppendIntField(json, "level", hack != null ? hack.Level : 1); json.Append(',');
            AppendIntField(json, "exp", hack != null ? hack.Xp : 0); json.Append(',');
            json.Append("\"position\":{");
            AppendNumberField(json, "x", player.X); json.Append(',');
            AppendNumberField(json, "y", player.Y);
            json.Append("}}");
        }

        static void AppendWorld(StringBuilder json, ICinderSim sim, float elapsed)
        {
            json.Append("\"world\":{");
            AppendNumberField(json, "elapsed", elapsed); json.Append(',');
            AppendIntField(json, "enemy_count", sim != null ? sim.LivingEnemies : 0); json.Append(',');
            AppendStringField(json, "current_phase", CurrentPhase(sim)); json.Append(',');
            AppendIntField(json, "wave", sim != null ? sim.Wave : 0);
            json.Append('}');
        }

        static void AppendCombat(StringBuilder json, ICinderSim sim)
        {
            json.Append("\"combat\":{");
            AppendIntField(json, "kills", sim != null ? sim.Kills : 0); json.Append(',');
            AppendIntField(json, "score", sim != null ? sim.Score : 0);
            json.Append('}');
        }

        static void AppendResources(StringBuilder json, ICinderSim sim)
        {
            var pickups = sim != null && sim.Pickups != null ? sim.Pickups.Count : 0;
            json.Append("\"resources\":{");
            AppendIntField(json, "exp_orbs", 0); json.Append(',');
            AppendIntField(json, "pickups", pickups); json.Append(',');
            AppendNumberField(json, "charge", sim != null ? sim.Charge : 0f);
            json.Append('}');
        }

        static void AppendUpgrade(StringBuilder json, ICinderSim sim)
        {
            var growth = sim as IGrowthChoiceSnapshot;
            var open = growth != null && growth.GrowthOfferOpen;
            json.Append("\"upgrade\":{");
            AppendBoolField(json, "is_selecting_upgrade", open); json.Append(',');
            json.Append("\"options\":");
            if (open)
                json.Append("[\"Attack\",\"Vitality\",\"Swiftness\"]");
            else
                json.Append("[]");
            json.Append('}');
        }

        static void AppendBoss(StringBuilder json, ICinderSim sim)
        {
            var hack = sim as IHackSnapshot;
            var campaign = sim as ICampaignSnapshot;
            var progression = sim as IDungeonProgressionSnapshot;
            var exists = hack != null && hack.BossHp > 0f && hack.BossMaxHp > 0f;
            if (!exists && campaign != null)
                exists = campaign.BossAlive;
            json.Append("\"boss\":{");
            AppendBoolField(json, "exists", exists); json.Append(',');
            AppendNumberField(json, "hp", hack != null ? hack.BossHp : 0f); json.Append(',');
            AppendNumberField(json, "max_hp", hack != null ? hack.BossMaxHp : 0f); json.Append(',');
            AppendIntField(json, "phase", hack != null ? hack.BossPhase : 0); json.Append(',');
            AppendIntField(json, "phase_count", progression != null ? progression.BossPhaseCount : 0);
            json.Append('}');
        }

        static void AppendStatus(StringBuilder json, ICinderSim sim)
        {
            var success = IsSuccess(sim);
            var failed = sim != null && sim.Mode == SimMode.GameOver && !success;
            var done = success || failed;
            json.Append("\"status\":{");
            AppendBoolField(json, "done", done); json.Append(',');
            AppendBoolField(json, "success", success); json.Append(',');
            AppendBoolField(json, "failed", failed); json.Append(',');
            AppendStringField(json, "reason", StatusReason(sim, success, failed));
            json.Append('}');
        }

        static string CurrentPhase(ICinderSim sim)
        {
            if (sim == null) return "loading";
            if (IsSuccess(sim)) return "success";
            if (sim.Mode == SimMode.GameOver) return "failed";
            var growth = sim as IGrowthChoiceSnapshot;
            if (growth != null && growth.GrowthOfferOpen) return "upgrade";
            var hack = sim as IHackSnapshot;
            if (hack != null && hack.BossHp > 0f) return "boss";
            if (sim.Mode == SimMode.WaveClear) return "wave_clear";
            return "running";
        }

        static bool IsSuccess(ICinderSim sim)
        {
            if (sim == null) return false;
            var campaign = sim as ICampaignSnapshot;
            if (campaign != null && campaign.StageCleared) return true;
            return IsTerminalSuccessReason(sim.Digest.Reason);
        }

        internal static bool IsTerminalSuccessReason(string reason)
            => reason == CampaignSpec.StageClearReason
                || reason == HackSpec.PrologueClearReason
                || reason == HackSpec.TrainingClearReason;

        static string StatusReason(ICinderSim sim, bool success, bool failed)
        {
            if (sim == null) return "loading";
            var reason = sim.Digest.Reason;
            if (!string.IsNullOrEmpty(reason)) return reason;
            if (success) return "clear";
            if (failed) return "terminal";
            return "running";
        }

        static void AppendStringField(StringBuilder json, string key, string value)
        {
            AppendString(json, key);
            json.Append(':');
            AppendString(json, value);
        }

        static void AppendIntField(StringBuilder json, string key, int value)
        {
            AppendString(json, key);
            json.Append(':');
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        static void AppendNumberField(StringBuilder json, string key, float value)
        {
            AppendString(json, key);
            json.Append(':');
            json.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void AppendBoolField(StringBuilder json, string key, bool value)
        {
            AppendString(json, key);
            json.Append(':');
            json.Append(value ? "true" : "false");
        }

        static void AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    switch (c)
                    {
                        case '\\': json.Append("\\\\"); break;
                        case '"': json.Append("\\\""); break;
                        case '\n': json.Append("\\n"); break;
                        case '\r': json.Append("\\r"); break;
                        case '\t': json.Append("\\t"); break;
                        default:
                            if (c < 32)
                                json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                json.Append(c);
                            break;
                    }
                }
            }
            json.Append('"');
        }

        static int GetActionCount()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GFAB_GetActionCount();
#else
            return EditorActions.Count;
#endif
        }

        static string PopAction()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var bytes = GFAB_PopAction(ActionBuffer, ActionBuffer.Length);
            return bytes > 0 ? Encoding.ASCII.GetString(ActionBuffer, 0, bytes) : string.Empty;
#else
            return EditorActions.Count > 0 ? EditorActions.Dequeue() : string.Empty;
#endif
        }

        static void PublishObservationJson(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GFAB_SetObservation(json);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        internal static void EnqueueEditorAction(string action) => EditorActions.Enqueue(action);
        internal static void ClearEditorActions() => EditorActions.Clear();
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GFAB_SetObservation(string json);
        [DllImport("__Internal")] static extern int GFAB_GetActionCount();
        [DllImport("__Internal")] static extern int GFAB_PopAction(byte[] buffer, int bufferSize);
#endif
    }
}
