mergeInto(LibraryManager.library, {
  $GameFlowAgentBridgeState: {
    apiVersion: "gameflow_standard_v2",
    gameType: "survivor_like",
    actionQueue: [],
    lastObservation: null,
    observationVersion: 0,
    baselines: {},

    defaultObservation: function () {
      return {
        api_version: "gameflow_standard_v2",
        game_type: "survivor_like",
        player: { hp: 0, max_hp: 0, level: 1, exp: 0, position: { x: 0, y: 0 } },
        world: { elapsed: 0, enemy_count: 0, current_phase: "loading", wave: 0 },
        combat: { kills: 0, score: 0 },
        resources: { exp_orbs: 0, pickups: 0, charge: 0 },
        upgrade: { is_selecting_upgrade: false, options: [] },
        boss: { exists: false, hp: 0, max_hp: 0, phase: 0, phase_count: 0 },
        status: { done: false, success: false, failed: false, reason: "loading" }
      };
    },

    observe: function () {
      return GameFlowAgentBridgeState.lastObservation || GameFlowAgentBridgeState.defaultObservation();
    },

    clone: function (value) {
      return JSON.parse(JSON.stringify(value));
    },

    availableActions: function () {
      var obs = GameFlowAgentBridgeState.observe();
      if (!obs || (obs.status && obs.status.done)) return ["WAIT"];
      var phase = obs.world && obs.world.current_phase;
      if (phase === "loading" || phase === "success" || phase === "failed") return ["WAIT"];
      if (obs.upgrade && obs.upgrade.is_selecting_upgrade) {
        var choices = [];
        var count = obs.upgrade.options ? obs.upgrade.options.length : 3;
        for (var i = 1; i <= Math.min(3, count); i++) choices.push("CHOOSE_UPGRADE_" + i);
        choices.push("WAIT");
        return choices;
      }
      var actions = ["MOVE_UP", "MOVE_DOWN", "MOVE_LEFT", "MOVE_RIGHT", "ATTACK", "WAIT"];
      if (obs.resources && obs.resources.pickups > 0) actions.push("PICK_UP");
      return actions;
    },

    queueAction: function (action) {
      if (typeof action !== "string") throw new Error("GameFlowAgentAPI action must be a string");
      GameFlowAgentBridgeState.actionQueue.push(action);
    },

    waitForObservationAfter: function (version) {
      return new Promise(function (resolve) {
        var startedAt = Date.now();
        var poll = function () {
          if (GameFlowAgentBridgeState.observationVersion > version || Date.now() - startedAt >= 800) {
            resolve(GameFlowAgentBridgeState.clone(GameFlowAgentBridgeState.observe()));
            return;
          }
          setTimeout(poll, 16);
        };
        poll();
      });
    },

    evaluate: function () {
      var obs = GameFlowAgentBridgeState.observe();
      var status = obs.status || {};
      return {
        done: !!status.done,
        success: !!status.success,
        failed: !!status.failed,
        score: obs.combat && typeof obs.combat.score === "number" ? obs.combat.score : 0,
        reason: status.reason || "running"
      };
    },

    listTestScenarios: function () {
      return [
        { scenario_id: "early_core_loop", name: "Early core loop", type: "survivor_like", route: "natural" },
        { scenario_id: "first_upgrade", name: "First upgrade", type: "survivor_like", route: "natural" },
        { scenario_id: "enemy_pressure", name: "Enemy pressure", type: "survivor_like", route: "natural" },
        { scenario_id: "boss_phase", name: "Boss phase", type: "survivor_like", route: "natural" }
      ];
    },

    knownScenario: function (id) {
      return GameFlowAgentBridgeState.listTestScenarios().some(function (scenario) {
        return scenario.scenario_id === id;
      });
    },

    checkScenarioPreconditions: function (id) {
      if (!GameFlowAgentBridgeState.knownScenario(id)) {
        return { ok: false, code: "UNKNOWN_SCENARIO", missing: ["scenario"], rationale: "Unknown survivor_like scenario id." };
      }
      var obs = GameFlowAgentBridgeState.observe();
      var missing = [];
      [
        "player.hp",
        "player.max_hp",
        "player.level",
        "player.exp",
        "player.position.x",
        "player.position.y",
        "world.elapsed",
        "world.enemy_count",
        "world.current_phase",
        "world.wave",
        "combat.kills",
        "combat.score",
        "resources.exp_orbs",
        "resources.pickups",
        "resources.charge",
        "upgrade.is_selecting_upgrade",
        "upgrade.options",
        "boss.exists",
        "boss.hp",
        "boss.phase",
        "status.done",
        "status.success",
        "status.failed",
        "status.reason"
      ].forEach(function (path) {
        if (GameFlowAgentBridgeState.getPath(obs, path) === undefined) missing.push(path);
      });
      if (missing.length) {
        return { ok: false, code: "SCENARIO_PRECONDITION_MISSING", missing: missing };
      }
      GameFlowAgentBridgeState.baselines[id] = GameFlowAgentBridgeState.clone(obs);
      return { ok: true, scenario_id: id, baseline_captured: true, route: "natural" };
    },

    getPath: function (root, path) {
      var value = root;
      var parts = path.split(".");
      for (var i = 0; i < parts.length; i++) {
        if (value === null || value === undefined) return undefined;
        value = value[parts[i]];
      }
      return value;
    },

    repairScenario: function (id, plan) {
      if (!GameFlowAgentBridgeState.knownScenario(id)) return { ok: false, code: "UNKNOWN_SCENARIO" };
      return {
        ok: false,
        code: "REPAIRER_NOT_IMPLEMENTED",
        reason: "scenario_repairer_not_implemented",
        rationale: "This bridge does not fabricate survivor_like state; use the natural route from the real run."
      };
    },

    jumpToScenario: function (id) {
      if (!GameFlowAgentBridgeState.knownScenario(id)) return { ok: false, code: "UNKNOWN_SCENARIO" };
      return {
        ok: false,
        code: "SCENARIO_LOADER_NOT_IMPLEMENTED",
        reason: "scenario_loader_not_implemented",
        rationale: "No coherent Unity scenario initializer is exposed; WAI should reach this node through normal play."
      };
    },

    evaluateScenario: function (id) {
      if (!GameFlowAgentBridgeState.knownScenario(id)) return { scenario_id: id, done: true, success: false, failed: true, code: "UNKNOWN_SCENARIO" };
      var obs = GameFlowAgentBridgeState.observe();
      var base = GameFlowAgentBridgeState.baselines[id] || obs;
      var moved = Math.abs((obs.player.position.x || 0) - (base.player.position.x || 0)) > 1 ||
        Math.abs((obs.player.position.y || 0) - (base.player.position.y || 0)) > 1;
      var verdict = { scenario_id: id, done: false, success: false, failed: false, route: "natural", observed: obs };
      if (obs.status && obs.status.failed) {
        verdict.done = true;
        verdict.failed = true;
        verdict.reason = obs.status.reason || "failed";
        return verdict;
      }
      if (id === "early_core_loop") {
        verdict.success = moved || obs.combat.kills > base.combat.kills || obs.combat.score > base.combat.score || obs.world.elapsed > base.world.elapsed + 1;
      } else if (id === "first_upgrade") {
        verdict.success = !!(obs.upgrade && obs.upgrade.is_selecting_upgrade) || obs.player.level > base.player.level;
      } else if (id === "enemy_pressure") {
        verdict.success = obs.world.enemy_count > 0 || obs.player.hp < base.player.hp;
      } else if (id === "boss_phase") {
        verdict.success = !!(obs.boss && obs.boss.exists) || (obs.boss && obs.boss.phase > (base.boss.phase || 0));
      }
      verdict.done = verdict.success || !!(obs.status && obs.status.done);
      verdict.reason = verdict.success ? "observed_real_state_delta" : "natural_route_not_reached_yet";
      return verdict;
    },

    mount: function () {
      var getGameInfo = function () {
        return {
          api_version: "gameflow_standard_v2",
          title: "Cinder Court",
          game_type: "survivor_like",
          goal: "Survive the stage, defeat the boss, and clear the prologue/training objectives through normal play.",
          controls: {
            move: ["MOVE_UP", "MOVE_DOWN", "MOVE_LEFT", "MOVE_RIGHT"],
            combat: ["ATTACK"],
            upgrade: ["CHOOSE_UPGRADE_1", "CHOOSE_UPGRADE_2", "CHOOSE_UPGRADE_3"],
            utility: ["WAIT", "PICK_UP", "RESET"]
          },
          capabilities: {
            observe: true,
            step: true,
            reset: true,
            dynamic_available_actions: true,
            scenarios: true,
            scenario_jump: false,
            scenario_repair: false
          },
          step_delay_ms: 220
        };
      };
      var bridge = {
        api_version: "gameflow_standard_v2",
        getGameInfo: getGameInfo,
        observe: function () { return GameFlowAgentBridgeState.observe(); },
        availableActions: function () { return GameFlowAgentBridgeState.availableActions(); },
        step: function (action) {
          var legal = GameFlowAgentBridgeState.availableActions();
          if (legal.indexOf(action) < 0) throw new Error("Illegal GameFlow action: " + action);
          var version = GameFlowAgentBridgeState.observationVersion;
          GameFlowAgentBridgeState.queueAction(action);
          return GameFlowAgentBridgeState.waitForObservationAfter(version);
        },
        evaluate: function () { return GameFlowAgentBridgeState.evaluate(); },
        reset: function () {
          var version = GameFlowAgentBridgeState.observationVersion;
          GameFlowAgentBridgeState.queueAction("RESET");
          return GameFlowAgentBridgeState.waitForObservationAfter(version);
        },
        _debugShadowReceiver: function (enabled) {
          var version = GameFlowAgentBridgeState.observationVersion;
          GameFlowAgentBridgeState.queueAction(
            enabled ? "SHADOW_RECEIVER_ON" : "SHADOW_RECEIVER_OFF");
          return GameFlowAgentBridgeState.waitForObservationAfter(version);
        },
        _debugFreezeStage: function (enabled) {
          var version = GameFlowAgentBridgeState.observationVersion;
          GameFlowAgentBridgeState.queueAction(
            enabled ? "SHADOW_CAPTURE_FREEZE" : "SHADOW_CAPTURE_UNFREEZE");
          return GameFlowAgentBridgeState.waitForObservationAfter(version);
        },
        _debugRendererCensus: function () {
          var version = GameFlowAgentBridgeState.observationVersion;
          GameFlowAgentBridgeState.queueAction("RENDERER_CENSUS");
          return GameFlowAgentBridgeState.waitForObservationAfter(version);
        },
        listTestScenarios: function () { return GameFlowAgentBridgeState.listTestScenarios(); },
        checkScenarioPreconditions: function (id) { return GameFlowAgentBridgeState.checkScenarioPreconditions(id); },
        repairScenario: function (id, plan) { return GameFlowAgentBridgeState.repairScenario(id, plan); },
        jumpToScenario: function (id) { return GameFlowAgentBridgeState.jumpToScenario(id); },
        evaluateScenario: function (id) { return GameFlowAgentBridgeState.evaluateScenario(id); }
      };

      window.GameFlowIntegration = bridge;
      window.GameFlowAgentAPI = {
        api_version: "gameflow_standard_v2",
        getGameInfo: getGameInfo,
        observe: bridge.observe,
        availableActions: bridge.availableActions,
        step: bridge.step,
        evaluate: bridge.evaluate,
        reset: bridge.reset,
        _debugShadowReceiver: bridge._debugShadowReceiver,
        _debugFreezeStage: bridge._debugFreezeStage,
        _debugRendererCensus: bridge._debugRendererCensus,
        listTestScenarios: bridge.listTestScenarios,
        checkScenarioPreconditions: bridge.checkScenarioPreconditions,
        repairScenario: bridge.repairScenario,
        jumpToScenario: bridge.jumpToScenario,
        evaluateScenario: bridge.evaluateScenario
      };
    }
  },

  GFAB_SetObservation__deps: ["$GameFlowAgentBridgeState"],
  GFAB_SetObservation: function (jsonPtr) {
    var text = UTF8ToString(jsonPtr);
    try {
      GameFlowAgentBridgeState.lastObservation = JSON.parse(text);
      GameFlowAgentBridgeState.observationVersion += 1;
    } catch (e) {
      GameFlowAgentBridgeState.lastObservation = GameFlowAgentBridgeState.defaultObservation();
      GameFlowAgentBridgeState.lastObservation.status.reason = "invalid_observation_json";
      GameFlowAgentBridgeState.observationVersion += 1;
    }
    GameFlowAgentBridgeState.mount();
  },

  GFAB_GetActionCount__deps: ["$GameFlowAgentBridgeState"],
  GFAB_GetActionCount: function () {
    GameFlowAgentBridgeState.mount();
    return GameFlowAgentBridgeState.actionQueue.length;
  },

  GFAB_PopAction__deps: ["$GameFlowAgentBridgeState"],
  GFAB_PopAction: function (buffer, bufferSize) {
    GameFlowAgentBridgeState.mount();
    if (!GameFlowAgentBridgeState.actionQueue.length || bufferSize <= 0) return 0;
    var action = String(GameFlowAgentBridgeState.actionQueue.shift() || "");
    var bytes = lengthBytesUTF8(action);
    var writeSize = Math.min(bytes + 1, bufferSize);
    stringToUTF8(action, buffer, writeSize);
    return Math.max(0, writeSize - 1);
  }
});
