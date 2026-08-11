using System.IO;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class GameFlowAgentBridgeTests
    {
        GameObject _root;
        InputAdapter _input;
        GameFlowAgentBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("GameFlowAgentBridgeTests");
            _input = _root.AddComponent<InputAdapter>();
            _bridge = _root.AddComponent<GameFlowAgentBridge>();
            _bridge.BindForTests(null, _input);
        }

        [TearDown]
        public void TearDown()
        {
            GameFlowAgentBridge.ClearEditorActions();
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void ObservationJson_ContainsRequiredSurvivorLikeStateAndHonestStatus()
        {
            var sim = new CinderSim(HackConfig.Prologue());
            var json = new StringBuilder();

            GameFlowAgentBridge.AppendObservation(json, sim, 1.25f);
            var text = json.ToString();

            StringAssert.Contains("\"api_version\":\"gameflow_standard_v2\"", text);
            StringAssert.Contains("\"player\":{", text);
            StringAssert.Contains("\"hp\":100", text);
            StringAssert.Contains("\"max_hp\":100", text);
            StringAssert.Contains("\"level\":1", text);
            StringAssert.Contains("\"exp\":0", text);
            StringAssert.Contains("\"position\":{", text);
            StringAssert.Contains("\"world\":{", text);
            StringAssert.Contains("\"elapsed\":1.25", text);
            StringAssert.Contains("\"enemy_count\":0", text);
            StringAssert.Contains("\"current_phase\":\"running\"", text);
            StringAssert.Contains("\"wave\":1", text);
            StringAssert.Contains("\"combat\":{", text);
            StringAssert.Contains("\"kills\":0", text);
            StringAssert.Contains("\"score\":0", text);
            StringAssert.Contains("\"resources\":{", text);
            StringAssert.Contains("\"exp_orbs\":0", text);
            StringAssert.Contains("\"pickups\":0", text);
            StringAssert.Contains("\"charge\":100", text);
            StringAssert.Contains("\"upgrade\":{", text);
            StringAssert.Contains("\"is_selecting_upgrade\":false", text);
            StringAssert.Contains("\"options\":[]", text);
            StringAssert.Contains("\"boss\":{", text);
            StringAssert.Contains("\"exists\":false", text);
            StringAssert.Contains("\"status\":{", text);
            StringAssert.Contains("\"done\":false", text);
            StringAssert.Contains("\"success\":false", text);
            StringAssert.Contains("\"failed\":false", text);
            StringAssert.Contains("\"reason\":\"running\"", text);
        }

        [Test]
        public void Bridge_RunsBeforeGameViewSoActionsCanLandInTheSameRenderFrame()
        {
            var order = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(
                typeof(GameFlowAgentBridge), typeof(DefaultExecutionOrder));

            Assert.That(order, Is.Not.Null);
            Assert.That(order.order, Is.EqualTo(-1000));
        }

        [Test]
        public void AgentActions_MapThroughInputAdapterSample()
        {
            _bridge.ApplyAgentAction("MOVE_RIGHT");
            var move = _input.Sample();
            Assert.That(move.MoveX, Is.GreaterThan(0.9f));
            Assert.That(move.MoveY, Is.EqualTo(0f));

            _bridge.ApplyAgentAction("WAIT");
            var wait = _input.Sample();
            Assert.That(wait.MoveX, Is.EqualTo(0f));
            Assert.That(wait.MoveY, Is.EqualTo(0f));

            _bridge.ApplyAgentAction("ATTACK");
            var attack = _input.Sample();
            Assert.That(attack.AttackQueued, Is.True);
            _input.ClearLatches();

            _bridge.ApplyAgentAction("CHOOSE_UPGRADE_2");
            var growth = _input.Sample();
            Assert.That(growth.GrowthChoice, Is.EqualTo(2));
            _input.ClearLatches();

            _bridge.ApplyAgentAction("RESET");
            var reset = _input.Sample();
            Assert.That(reset.RestartQueued, Is.True);
        }

        [TestCase(CampaignSpec.StageClearReason)]
        [TestCase(HackSpec.PrologueClearReason)]
        [TestCase(HackSpec.TrainingClearReason)]
        public void TerminalClearReasons_AreReportedAsSuccess(string reason)
        {
            Assert.That(GameFlowAgentBridge.IsTerminalSuccessReason(reason), Is.True);
        }

        [TestCase("running")]
        [TestCase("overrun")]
        [TestCase("")]
        public void NonClearReasons_AreNotReportedAsSuccess(string reason)
        {
            Assert.That(GameFlowAgentBridge.IsTerminalSuccessReason(reason), Is.False);
        }

        [Test]
        public void WebGlEntryPoints_DeclareTheirSharedStateDependency()
        {
            var path = Path.Combine(Application.dataPath, "Plugins/WebGL/gameflow_agent.jslib");
            var text = File.ReadAllText(path);

            StringAssert.Contains(
                "GFAB_SetObservation__deps: [\"$GameFlowAgentBridgeState\"]", text);
            StringAssert.Contains(
                "GFAB_GetActionCount__deps: [\"$GameFlowAgentBridgeState\"]", text);
            StringAssert.Contains(
                "GFAB_PopAction__deps: [\"$GameFlowAgentBridgeState\"]", text);
        }

        [Test]
        public void WebGlScenarioList_UsesTheStandardScenarioIdField()
        {
            var path = Path.Combine(Application.dataPath, "Plugins/WebGL/gameflow_agent.jslib");
            var text = File.ReadAllText(path);

            StringAssert.Contains("scenario_id: \"early_core_loop\"", text);
            StringAssert.Contains("scenario_id: \"first_upgrade\"", text);
            StringAssert.Contains("scenario_id: \"enemy_pressure\"", text);
            StringAssert.Contains("scenario_id: \"boss_phase\"", text);
            StringAssert.Contains("reason: \"scenario_repairer_not_implemented\"", text);
            StringAssert.Contains("reason: \"scenario_loader_not_implemented\"", text);
        }

        [Test]
        public void WebGlDevelopmentShadowProbe_UsesASeparateNonGameplayApi()
        {
            var jsPath = Path.Combine(Application.dataPath, "Plugins/WebGL/gameflow_agent.jslib");
            var js = File.ReadAllText(jsPath);
            var bridgePath = Path.Combine(Application.dataPath, "Scripts/View/GameFlowAgentBridge.cs");
            var bridge = File.ReadAllText(bridgePath);

            StringAssert.Contains("_debugShadowReceiver: function (enabled)", js);
            StringAssert.Contains("_debugFreezeStage: function (enabled)", js);
            StringAssert.Contains(
                "enabled ? \"SHADOW_RECEIVER_ON\" : \"SHADOW_RECEIVER_OFF\"", js);
            StringAssert.Contains("#if DEVELOPMENT_BUILD || UNITY_EDITOR", bridge);
            StringAssert.Contains("case \"SHADOW_RECEIVER_OFF\":", bridge);
            StringAssert.Contains("case \"SHADOW_RECEIVER_ON\":", bridge);
            StringAssert.Contains("case \"SHADOW_CAPTURE_FREEZE\":", bridge);
            StringAssert.Contains("case \"SHADOW_CAPTURE_UNFREEZE\":", bridge);
            var gameView = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts/View/GameView.cs"));
            StringAssert.Contains("GameFlowAgentBridge.DiagnosticCaptureFrozen", gameView);
        }
    }
}
