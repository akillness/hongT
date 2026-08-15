using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class Cycle9GrowthEvidenceTests
    {
        private const string RunId = "20260808-achilles-quality";
        private const string HarnessVersion = "cycle9-g5-unity-editmode-v3";
        private const int SessionLimit = 21;
        private const int DungeonTickLimit = 60 * 300;
        private const int PacifistTickLimit = 60 * 120;
        private const float KiterRetreatBelow = SimConfig.PlayerAttackRange * 0.75f;
        private const float KiterApproachAbove = SimConfig.PlayerAttackRange - 10f;

        private static readonly string[] PilotIds =
        {
            "melee-rusher",
            "kiter",
            "skill-spammer",
            "companion-commander",
            "pacifist-dodger",
        };

        private static readonly string[] RouteIds = { "A", "B", "C", "D", "E" };
        private static readonly string[] SlotIds = { "weapon", "lantern", "cloak" };

        [Test]
        public void BankDungeonClear_MatchesLiveSettlementRules_AcrossFirstPactAndRepeatClears()
        {
            var firstStage = StageCatalog.Entries[0];
            Assert.That(firstStage.CompanionReward, Is.Not.Null.And.Not.Empty,
                "the roster/activation proof needs a catalog stage with a first-clear companion");

            var rankSim = RunSettledDungeon(
                firstStage,
                EquipTiers.Of(4, 3, ProgressionGuide.EquipCap),
                pact: false,
                pilotId: "kiter");
            Assert.That(rankSim.StageCleared, Is.True, "the settlement proof must use a real cleared CinderSim");

            var data = FreshCampaign();
            data.Weapon = 1;
            data.Lantern = 4;
            data.Cloak = 2;
            var snapshot = (ICampaignSnapshot)rankSim;

            GameDirector.BankDungeonClear(ref data, firstStage.Id, false, rankSim);

            Assert.That(StageCatalog.IsCleared(in data, in firstStage), Is.True);
            Assert.That(data.Points, Is.EqualTo(3), "a first clear grants the first-clear point component");
            Assert.That(data.Relics, Is.EqualTo(rankSim.Relics),
                "an unpacted clear banks the live run digest exactly once");
            Assert.That(data.Weapon, Is.EqualTo(Mathf.Max(1, snapshot.WeaponRank)));
            Assert.That(data.Lantern, Is.EqualTo(Mathf.Max(4, snapshot.LanternRank)),
                "a lower run rank must not reduce a higher persisted rank");
            Assert.That(data.Cloak, Is.EqualTo(Mathf.Max(2, snapshot.CloakRank)));
            Assert.That(data.Roster, Does.Contain(firstStage.CompanionReward));
            Assert.That(data.Active, Is.EqualTo(firstStage.CompanionReward));
            Assert.That(data.ActiveSlots, Is.EqualTo(new[] { firstStage.CompanionReward }),
                "the first companion reward activates only when no active slot exists");

            int rosterCount = data.Roster.Length;
            int relicsBeforeRepeat = data.Relics;
            GameDirector.BankDungeonClear(ref data, firstStage.Id, true, rankSim);

            Assert.That(data.Points, Is.EqualTo(5), "a repeat clear grants two points, not the first-clear three");
            Assert.That(data.Relics - relicsBeforeRepeat,
                Is.EqualTo(rankSim.Relics * GameDirector.PactRelicMultiplier),
                "pact multiplies only the live run relic component");
            Assert.That(data.Roster.Length, Is.EqualTo(rosterCount), "repeat clear must not duplicate roster entries");
            Assert.That(data.ActiveSlots, Is.EqualTo(new[] { firstStage.CompanionReward }));

            Assert.That(StageCatalog.TryGet("cinder-sluice", out var rewardStage), Is.True);
            int liveFirstClearBonus = InvokeFirstClearRelicBonus(rewardStage.Id);
            Assert.That(liveFirstClearBonus, Is.GreaterThan(0),
                "the bonus proof needs a stage whose live first-clear authority pays relics");
            var rewardSim = RunSettledDungeon(
                rewardStage,
                EquipTiers.Of(ProgressionGuide.EquipCap, ProgressionGuide.EquipCap, ProgressionGuide.EquipCap),
                pact: true,
                pilotId: "kiter");
            Assert.That(rewardSim.StageCleared, Is.True);

            var rewardData = FreshCampaign();
            GameDirector.BankDungeonClear(ref rewardData, rewardStage.Id, true, rewardSim);
            Assert.That(rewardData.Relics,
                Is.EqualTo(rewardSim.Relics * GameDirector.PactRelicMultiplier + liveFirstClearBonus),
                "the first-clear bonus remains single while pact doubles only run relics");

            int firstSettlement = rewardData.Relics;
            GameDirector.BankDungeonClear(ref rewardData, rewardStage.Id, false, rewardSim);
            Assert.That(rewardData.Relics - firstSettlement, Is.EqualTo(rewardSim.Relics),
                "repeat clear banks the live run component and never repeats the first-clear bonus");
            Assert.That(rewardData.Points, Is.EqualTo(5));
        }

        [Test]
        [Explicit("Requires candidate-bound Cycle 9 G5 evidence inputs; invoke by full test name.")]
        public void GenerateCanonicalG5Evidence_WritesFivePilotsByFiveRoutes_WithoutGateVerdict()
        {
            ProtocolInputs inputs = RequireProtocolInputs();
            string outputDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "_workspace", "current", "qa", "cycle9-g5"));
            string rawPath = Path.Combine(outputDirectory, "raw-session-rows.jsonl");
            string summaryPath = Path.Combine(outputDirectory, "summary.json");
            string masteryPath = Path.Combine(outputDirectory, "mastery-events.jsonl");
            string manifestPath = Path.Combine(outputDirectory, "evidence-manifest.json");
            string campaignKey = CampaignStorageKey();
            bool campaignExisted = PlayerPrefs.HasKey(campaignKey);
            string originalCampaign = PlayerPrefs.GetString(campaignKey, string.Empty);

            var rows = new List<EvidenceRow>();
            var masteryEvents = new List<MasteryEvent>();
            var pilotSummaries = new List<PilotSummary>();
            var seenCells = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (string pilotId in PilotIds)
                {
                    var routeResults = new List<RouteSummary>();
                    foreach (string routeId in RouteIds)
                    {
                        CampaignData data = SaveReload(FreshCampaign());
                        Assert.That(data.PrologueDone, Is.True,
                            "the first counted leg requires completed prologue in the persisted save");
                        Assert.That(inputs.spend_proof_sha256, Is.Not.Empty,
                            "the imported shipped-UI reachability proof must bind every lobby spend claim");

                        if (routeId == "E")
                        {
                            CampaignData beforeMastery = data;
                            int masteryRelics = CompleteActualTrainingMastery(ref data);
                            data = SaveReload(data);
                            masteryEvents.Add(BuildMasteryEvent(
                                inputs, pilotId, routeId, in beforeMastery, in data, masteryRelics));
                        }

                        int firstT5Session = 0;
                        int farmCursor = 0;
                        for (int sessionIndex = 1; sessionIndex <= SessionLimit; sessionIndex += 1)
                        {
                            CampaignData before = data;
                            bool allStagesClearedBefore = ProgressionGuide.ClearedTotal(in data)
                                == StageCatalog.Entries.Count;
                            StageEntry stage = SelectStage(in data, farmCursor);
                            bool wasAlreadyCleared = StageCatalog.IsCleared(in data, in stage);
                            bool pactArmed = RouteUsesPact(routeId) && wasAlreadyCleared;
                            CinderSim sim = BuildDungeonSim(in data, in stage, pilotId, pactArmed);
                            var pilotState = new PilotState();
                            RunObservation observation = RunToSettlement(sim, pilotId, pilotState);

                            Assert.That(observation.reason,
                                Is.EqualTo(CampaignSpec.StageClearReason).Or.EqualTo("defeat"),
                                $"{pilotId}/{routeId}/session-{sessionIndex} must settle by clear or defeat");

                            bool cleared = observation.reason == CampaignSpec.StageClearReason;
                            string routeAction = RouteAction(routeId, in stage, cleared);
                            bool spendOpportunity = routeAction == "lobby";
                            int pactMultiplier = cleared && pactArmed ? GameDirector.PactRelicMultiplier : 1;
                            int firstClearRelics = 0;
                            List<RankTransition> rankTransitions = observation.rank_transitions;

                            if (cleared)
                            {
                                int relicsBeforeBank = data.Relics;
                                GameDirector.BankDungeonClear(ref data, stage.Id, pactArmed, sim);
                                firstClearRelics = data.Relics - relicsBeforeBank - sim.Relics * pactMultiplier;
                                Assert.That(firstClearRelics, Is.GreaterThanOrEqualTo(0));
                                AssertBankedRanksMatchEvents(in before, in data, rankTransitions);
                            }
                            else
                            {
                                data.Relics += sim.Relics;
                                MergeRosterThroughLiveAuthority(ref data, ((IHackSnapshot)sim).RosterMask);
                                Assert.That(data.Weapon, Is.EqualTo(before.Weapon));
                                Assert.That(data.Lantern, Is.EqualTo(before.Lantern));
                                Assert.That(data.Cloak, Is.EqualTo(before.Cloak),
                                    "defeat banks relics but never direct rank steps");
                                Assert.That(rankTransitions, Is.Empty,
                                    "a defeated run cannot export an equipment-drop transition");
                            }

                            data = SaveReload(data);
                            CampaignData afterSettlement = data;
                            var purchases = new List<EquipmentPurchase>();
                            if (spendOpportunity)
                            {
                                BuyCheapestUntilBlocked(ref data, purchases);
                                data = SaveReload(data);
                            }

                            AdvanceFarmCursor(
                                ref farmCursor, allStagesClearedBefore, in stage, cleared);
                            bool t5Reached = IsT5(in data);
                            EvidenceRow row = BuildRow(
                                inputs,
                                pilotId,
                                routeId,
                                sessionIndex,
                                in before,
                                in afterSettlement,
                                in data,
                                in stage,
                                sim,
                                observation,
                                routeAction,
                                pactArmed,
                                pactMultiplier,
                                !wasAlreadyCleared && cleared,
                                firstClearRelics,
                                spendOpportunity,
                                rankTransitions,
                                purchases,
                                t5Reached);
                            AssertEvidenceRow(row);
                            rows.Add(row);

                            if (t5Reached)
                            {
                                firstT5Session = sessionIndex;
                                break;
                            }
                        }

                        string cell = pilotId + "/" + routeId;
                        Assert.That(seenCells.Add(cell), Is.True,
                            "every pilot-route cell must be unique");
                        routeResults.Add(new RouteSummary
                        {
                            route_id = routeId,
                            n_t5 = firstT5Session > 0 && firstT5Session <= 20
                                ? firstT5Session.ToString(CultureInfo.InvariantCulture)
                                : ">20",
                            first_t5_session_observed = firstT5Session,
                            sessions_recorded = CountRows(rows, pilotId, routeId),
                        });
                    }

                    pilotSummaries.Add(SummarizePilot(pilotId, routeResults));
                }

                Assert.That(seenCells.Count, Is.EqualTo(PilotIds.Length * RouteIds.Length));
                Assert.That(rows, Is.Not.Empty);
                Assert.That(rows.Exists(row => row.identity.pilot_id == "melee-rusher"), Is.True);
                Assert.That(rows.Exists(row => row.identity.pilot_id == "pacifist-dodger"), Is.True);
                Assert.That(rows.TrueForAll(row => row.fairness.paid_offer_visible == false
                    && row.fairness.paid_power_applied == false), Is.True);
                Assert.That(masteryEvents.Count, Is.EqualTo(PilotIds.Length),
                    "route E has one separately timestamped mastery occurrence per pilot");

                var summary = new EvidenceSummary
                {
                    run_id = RunId,
                    candidate_id = inputs.candidate_id,
                    build_id = inputs.build_id,
                    source_full_hash = inputs.source_full_hash,
                    harness_version = HarnessVersion,
                    unity_version = Application.unityVersion,
                    stage = "Stage 2 / Phase 2a",
                    status = "MEASURED_NOT_ADJUDICATED",
                    scripted_pilots_are_human_evidence = false,
                    raw_rows_path = "_workspace/current/qa/cycle9-g5/raw-session-rows.jsonl",
                    mastery_events_path = "_workspace/current/qa/cycle9-g5/mastery-events.jsonl",
                    row_count = rows.Count,
                    pilot_summaries = pilotSummaries.ToArray(),
                    protocol_deviations = Array.Empty<string>(),
                    unknown_fields = Array.Empty<string>(),
                    unresolved_route_semantics = new[]
                    {
                        "The frozen protocol defines replay catalog order after nine clears but not a defeat cursor rule; this harness repeats a defeated replay and advances only after a clear.",
                    },
                };

                string summaryJson = JsonUtility.ToJson(summary, true);
                Assert.That(summaryJson, Does.Not.Contain("\"status\": \"PASS\""));
                WriteEvidenceAtomically(
                    outputDirectory, rawPath, summaryPath, masteryPath, manifestPath,
                    rows, masteryEvents, summaryJson, inputs);

                Assert.That(File.ReadAllLines(rawPath).Length, Is.EqualTo(rows.Count));
                Assert.That(File.ReadAllLines(masteryPath).Length, Is.EqualTo(masteryEvents.Count));
                Assert.That(File.Exists(manifestPath), Is.True);
            }
            finally
            {
                if (campaignExisted) PlayerPrefs.SetString(campaignKey, originalCampaign);
                else PlayerPrefs.DeleteKey(campaignKey);
                PlayerPrefs.Save();
            }
        }

        private static CampaignData FreshCampaign()
        {
            return new CampaignData
            {
                PrologueDone = true,
                Roster = Array.Empty<string>(),
                Active = string.Empty,
                ActiveSlots = Array.Empty<string>(),
            };
        }

        private static StageEntry SelectStage(in CampaignData data, int farmCursor)
        {
            if (ProgressionGuide.ClearedTotal(in data) < StageCatalog.Entries.Count)
            {
                GuideTarget target = ProgressionGuide.NextTarget(in data);
                Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.Stage),
                    "before nine clears the live progression guide must own stage choice");
                Assert.That(target.Index, Is.InRange(0, StageCatalog.Entries.Count - 1));
                return StageCatalog.Entries[target.Index];
            }

            return StageCatalog.Entries[farmCursor];
        }

        private static void AdvanceFarmCursor(
            ref int farmCursor,
            bool allStagesClearedBefore,
            in StageEntry stage,
            bool cleared)
        {
            if (!allStagesClearedBefore || !cleared) return;
            Assert.That(stage.CatalogIndex, Is.EqualTo(farmCursor),
                "post-nine replay ordering must remain catalog ordered");
            farmCursor = (farmCursor + 1) % StageCatalog.Entries.Count;
        }

        private static CinderSim BuildDungeonSim(
            in CampaignData data,
            in StageEntry stage,
            string pilotId,
            bool pactArmed)
        {
            string[] companions = data.ActiveSlots ?? Array.Empty<string>();

            Assert.That(HackConfig.TryDungeon(
                    stage.SimAnchorId,
                    MetaStats.Of(data.Attack, data.Vitality, data.Swiftness),
                    EquipTiers.Of(data.Weapon, data.Lantern, data.Cloak),
                    companions,
                    RosterMaskThroughLiveAuthority(data.Roster),
                    out var config),
                Is.True,
                "every catalog entry must resolve through its shipped Sim anchor");
            config.Hazards = pactArmed ? StageCatalog.PactFor(stage.Id) : stage.HazardOverride;
            config.Difficulty = Difficulty.Normal;
            return new CinderSim(in config, GameView.DungeonProgression);
        }

        private static CinderSim RunSettledDungeon(
            StageEntry stage,
            EquipTiers tiers,
            bool pact,
            string pilotId)
        {
            Assert.That(HackConfig.TryDungeon(
                    stage.SimAnchorId,
                    MetaStats.Of(10, 10, 10),
                    tiers,
                    Array.Empty<string>(),
                    0,
                    out var config),
                Is.True);
            config.Hazards = pact ? StageCatalog.PactFor(stage.Id) : stage.HazardOverride;
            config.Difficulty = Difficulty.Normal;
            var sim = new CinderSim(in config, GameView.DungeonProgression);
            RunObservation observation = RunToSettlement(sim, pilotId, new PilotState());
            Assert.That(observation.reason, Is.EqualTo(CampaignSpec.StageClearReason));
            return sim;
        }

        private static RunObservation RunToSettlement(CinderSim sim, string pilotId, PilotState state)
        {
            int limit = pilotId == "pacifist-dodger" ? PacifistTickLimit : DungeonTickLimit;
            var activations = new List<ActivationRow>();
            var rankTransitions = new List<RankTransition>();
            var equipPickupIds = new int[SimConfig.EnemyCap];
            var equipPickupGrades = new LootGrade[SimConfig.EnemyCap];
            float maxHealth = sim.Player.Health;
            for (int tick = 1; tick <= limit; tick += 1)
            {
                float healthBefore = sim.Player.Health;
                int weaponBefore = sim.WeaponRank;
                int lanternBefore = sim.LanternRank;
                int cloakBefore = sim.CloakRank;
                int equipPickupCount = SnapshotEquipPickups(
                    sim, equipPickupIds, equipPickupGrades);
                SimInput input = InputForPilot(pilotId, sim, tick, state);
                sim.Tick(in input);
                if ((sim.Events & SimEvents.EquipDropped) != 0)
                {
                    if ((sim.Events & SimEvents.StageCleared) != 0)
                    {
                        string grade = LootGradeSpec.BossGrade.ToString().ToLowerInvariant();
                        CaptureSimRankTransition(
                            rankTransitions, "weapon", weaponBefore, sim.WeaponRank,
                            "stage-boss-drop", grade);
                        CaptureSimRankTransition(
                            rankTransitions, "lantern", lanternBefore, sim.LanternRank,
                            "stage-boss-drop", grade);
                        CaptureSimRankTransition(
                            rankTransitions, "cloak", cloakBefore, sim.CloakRank,
                            "stage-boss-drop", grade);
                    }
                    else
                    {
                        CaptureCollectedPickupTransitions(
                            sim,
                            equipPickupIds,
                            equipPickupGrades,
                            equipPickupCount,
                            weaponBefore,
                            lanternBefore,
                            cloakBefore,
                            rankTransitions);
                    }
                }
                if ((sim.Events & SimEvents.PerilOpened) != 0)
                {
                    activations.Add(new ActivationRow
                    {
                        tick = tick,
                        clause = "peril-window",
                        health_before = healthBefore,
                        max_health = maxHealth,
                        isolated_reversal = false,
                    });
                }
                if ((sim.Events & SimEvents.StageCleared) != 0 || sim.StageCleared)
                {
                    return new RunObservation
                    {
                        reason = CampaignSpec.StageClearReason,
                        outcome = "clear",
                        ticks = tick,
                        activations = activations,
                        rank_transitions = rankTransitions,
                    };
                }
                if ((sim.Events & SimEvents.GameOver) != 0 || sim.Mode == SimMode.GameOver)
                {
                    return new RunObservation
                    {
                        reason = "defeat",
                        outcome = "defeat",
                        ticks = tick,
                        activations = activations,
                        rank_transitions = rankTransitions,
                    };
                }
            }

            Assert.Fail($"{pilotId} did not settle within its declared input-policy cap ({limit} ticks)");
            return default;
        }

        private static SimInput InputForPilot(string pilotId, CinderSim sim, int tick, PilotState state)
        {
            switch (pilotId)
            {
                case "melee-rusher": return MeleeRusherInput(sim);
                case "kiter": return KiterBandInput(sim, KiterRetreatBelow, KiterApproachAbove);
                case "skill-spammer": return SkillSpammerInput(sim);
                case "companion-commander": return CompanionCommanderInput(sim, state);
                case "pacifist-dodger": return PacifistDodgerInput(sim, state);
                default: throw new ArgumentOutOfRangeException(nameof(pilotId), pilotId, null);
            }
        }

        private static SimInput MeleeRusherInput(CinderSim sim)
        {
            if (!TryNearestEnemy(sim, out float dx, out float dy, out float isoDistance))
                return default;
            float length = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
            return new SimInput
            {
                MoveX = dx / length,
                MoveY = dy / length,
                AttackQueued = true,
                DashQueued = isoDistance > 150f,
            };
        }

        private static SimInput SkillSpammerInput(CinderSim sim)
        {
            const float anchorRadius = 60f;
            float dx = SimConfig.ArenaX - sim.Player.X;
            float dy = SimConfig.ArenaY - sim.Player.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            var input = new SimInput
            {
                NovaQueued = true,
                BoltQueued = true,
                PulseQueued = true,
                WardQueued = true,
                AttackQueued = sim.Charge >= SimConfig.LanternMax,
            };
            if (distance > anchorRadius)
            {
                input.MoveX = dx / MathF.Max(0.001f, distance);
                input.MoveY = dy / MathF.Max(0.001f, distance);
            }
            return input;
        }

        private static SimInput CompanionCommanderInput(CinderSim sim, PilotState state)
        {
            SimInput input = KiterBandInput(sim, KiterRetreatBelow, KiterApproachAbove);
            if (sim.CompanionCount == 0) return input;

            bool anchorInRange = TryActiveGimmickAnchor(sim, out float anchorX, out float anchorY)
                && IsoDistance(sim.Player.X, sim.Player.Y, anchorX, anchorY) <= 200f;
            if (anchorInRange != state.companionHold)
            {
                input.CompanionHoldQueued = anchorInRange;
                input.CompanionRecallQueued = !anchorInRange;
                state.companionHold = anchorInRange;
            }
            return input;
        }

        private static bool TryActiveGimmickAnchor(
            CinderSim sim,
            out float anchorX,
            out float anchorY)
        {
            IReadOnlyList<HazardState> hazards = ((ICampaignSnapshot)sim).Hazards;
            for (int index = 0; index < hazards.Count; index += 1)
            {
                HazardState hazard = hazards[index];
                if (!hazard.Telegraphing && !hazard.Active && hazard.Hp <= 0f) continue;
                anchorX = hazard.Kind == HazardKind.AshWall ? hazard.FrontX : hazard.X;
                anchorY = hazard.Y;
                return true;
            }
            anchorX = 0f;
            anchorY = 0f;
            return false;
        }

        private static SimInput KiterBandInput(CinderSim sim, float retreatBelow, float approachAbove)
        {
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };
            if (!TryNearestEnemy(sim, out float dx, out float dy, out float isoDistance)) return input;
            float length = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
            if (isoDistance < retreatBelow)
            {
                input.MoveX = -dx / length;
                input.MoveY = -dy / length;
            }
            else if (isoDistance > approachAbove)
            {
                input.MoveX = dx / length;
                input.MoveY = dy / length;
            }
            return input;
        }

        private static SimInput PacifistDodgerInput(CinderSim sim, PilotState state)
        {
            float[,] waypoints =
            {
                { 348f, 434f },
                { 1188f, 434f },
                { 1188f, 774f },
                { 348f, 774f },
            };
            float targetX = waypoints[state.waypoint, 0];
            float targetY = waypoints[state.waypoint, 1];
            float dx = targetX - sim.Player.X;
            float dy = targetY - sim.Player.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance < 24f)
            {
                state.waypoint = (state.waypoint + 1) % waypoints.GetLength(0);
                targetX = waypoints[state.waypoint, 0];
                targetY = waypoints[state.waypoint, 1];
                dx = targetX - sim.Player.X;
                dy = targetY - sim.Player.Y;
                distance = MathF.Sqrt(dx * dx + dy * dy);
            }

            bool covered = TelegraphCoversPlayer(sim);
            var input = new SimInput
            {
                MoveX = dx / MathF.Max(0.001f, distance),
                MoveY = dy / MathF.Max(0.001f, distance),
                DashQueued = covered && !state.telegraphCoveredLastTick,
            };
            state.telegraphCoveredLastTick = covered;
            return input;
        }

        private static bool TelegraphCoversPlayer(CinderSim sim)
        {
            IReadOnlyList<HazardState> hazards = ((ICampaignSnapshot)sim).Hazards;
            for (int index = 0; index < hazards.Count; index += 1)
            {
                HazardState hazard = hazards[index];
                if (!hazard.Telegraphing) continue;
                float dx = sim.Player.X - hazard.X;
                float dy = sim.Player.Y - hazard.Y;
                if (hazard.Kind == HazardKind.TideCurrent)
                {
                    if (MathF.Abs(dx) <= CampaignSpec.CurrentHalfW
                        && MathF.Abs(dy) <= CampaignSpec.CurrentHalfH)
                        return true;
                    continue;
                }
                if (hazard.Kind == HazardKind.AshWall)
                {
                    bool fromLeft = hazard.X <= SimConfig.ArenaX;
                    bool insideSweep = fromLeft
                        ? sim.Player.X >= hazard.X && sim.Player.X <= CampaignSpec.WallEdgeRightX
                        : sim.Player.X <= hazard.X && sim.Player.X >= CampaignSpec.WallEdgeX;
                    if (insideSweep) return true;
                    continue;
                }
                if (hazard.Radius > 0f
                    && IsoDistance(sim.Player.X, sim.Player.Y, hazard.X, hazard.Y)
                        <= hazard.Radius)
                    return true;
            }
            return false;
        }

        private static float IsoDistance(float fromX, float fromY, float toX, float toY)
        {
            float dx = toX - fromX;
            float dy = (toY - fromY) * SimConfig.IsoY;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static bool TryNearestEnemy(
            CinderSim sim,
            out float dx,
            out float dy,
            out float isoDistance)
        {
            dx = 0f;
            dy = 0f;
            float bestSquared = float.MaxValue;
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead) continue;
                float candidateX = enemy.X - sim.Player.X;
                float candidateY = enemy.Y - sim.Player.Y;
                float isoY = candidateY * SimConfig.IsoY;
                float squared = candidateX * candidateX + isoY * isoY;
                if (squared >= bestSquared) continue;
                bestSquared = squared;
                dx = candidateX;
                dy = candidateY;
            }
            isoDistance = bestSquared < float.MaxValue ? MathF.Sqrt(bestSquared) : 0f;
            return bestSquared < float.MaxValue;
        }

        private static bool RouteUsesPact(string routeId) => routeId == "C" || routeId == "D";

        private static string RouteAction(string routeId, in StageEntry stage, bool cleared)
        {
            if (routeId != "B" && routeId != "D") return "lobby";
            if (!cleared) return "direct-retry";
            return stage.CatalogIndex + 1 < StageCatalog.Entries.Count
                ? "ember-rest"
                : "lobby";
        }

        private static void AssertBankedRanksMatchEvents(
            in CampaignData before,
            in CampaignData after,
            List<RankTransition> transitions)
        {
            int weapon = FinalTransitionTier("weapon", before.Weapon, transitions);
            int lantern = FinalTransitionTier("lantern", before.Lantern, transitions);
            int cloak = FinalTransitionTier("cloak", before.Cloak, transitions);
            Assert.That(after.Weapon, Is.EqualTo(weapon));
            Assert.That(after.Lantern, Is.EqualTo(lantern));
            Assert.That(after.Cloak, Is.EqualTo(cloak));
        }

        private static int FinalTransitionTier(
            string slot,
            int initial,
            List<RankTransition> transitions)
        {
            int tier = initial;
            for (int index = 0; index < transitions.Count; index += 1)
                if (transitions[index].slot == slot)
                    tier = Math.Max(tier, transitions[index].to);
            return tier;
        }

        private static void CaptureSimRankTransition(
            List<RankTransition> transitions,
            string slot,
            int from,
            int to,
            string source,
            string grade)
        {
            if (to <= from) return;
            transitions.Add(new RankTransition
            {
                slot = slot,
                from = from,
                to = to,
                source = source,
                grade = grade,
            });
        }

        private static int SnapshotEquipPickups(
            CinderSim sim,
            int[] ids,
            LootGrade[] grades)
        {
            IReadOnlyList<PickupState> pickups = sim.Pickups;
            IReadOnlyList<LootGrade> pickupGrades = sim.PickupGrades;
            Assert.That(pickupGrades.Count, Is.EqualTo(pickups.Count));
            int count = 0;
            for (int index = 0; index < pickups.Count; index += 1)
            {
                if (pickups[index].Kind != PickupKind.EquipShard) continue;
                ids[count] = pickups[index].Id;
                grades[count] = pickupGrades[index];
                count += 1;
            }
            return count;
        }

        private static void CaptureCollectedPickupTransitions(
            CinderSim sim,
            int[] ids,
            LootGrade[] grades,
            int count,
            int weaponBefore,
            int lanternBefore,
            int cloakBefore,
            List<RankTransition> transitions)
        {
            string slot;
            int from;
            int actualTo;
            if (sim.WeaponRank > weaponBefore)
            {
                slot = "weapon";
                from = weaponBefore;
                actualTo = sim.WeaponRank;
            }
            else if (sim.LanternRank > lanternBefore)
            {
                slot = "lantern";
                from = lanternBefore;
                actualTo = sim.LanternRank;
            }
            else if (sim.CloakRank > cloakBefore)
            {
                slot = "cloak";
                from = cloakBefore;
                actualTo = sim.CloakRank;
            }
            else
            {
                return;
            }

            int current = from;
            for (int index = 0; index < count && current < actualTo; index += 1)
            {
                if (PickupStillLive(sim, ids[index])) continue;
                int steps = LootGradeSpec.GradeRankSteps[(int)grades[index]];
                int next = Math.Min(actualTo, Math.Min(ProgressionGuide.EquipCap, current + steps));
                CaptureSimRankTransition(
                    transitions,
                    slot,
                    current,
                    next,
                    "equip-shard-pickup",
                    grades[index].ToString().ToLowerInvariant());
                current = next;
            }
            if (current < actualTo)
            {
                LootGrade sameTickGrade = sim.LastLootGrade;
                int grantedSteps = LootGradeSpec.GradeRankSteps[(int)sameTickGrade];
                Assert.That(actualTo - current, Is.LessThanOrEqualTo(grantedSteps),
                    "the only transition absent from the pre-tick pickup snapshot is a shard spawned and collected on this tick");
                CaptureSimRankTransition(
                    transitions,
                    slot,
                    current,
                    actualTo,
                    "equip-shard-pickup-same-tick-spawn",
                    sameTickGrade.ToString().ToLowerInvariant());
                current = actualTo;
            }
            Assert.That(current, Is.EqualTo(actualTo),
                "every bankable rank change must reconcile to a removed live equip shard and its actual grade");
        }

        private static bool PickupStillLive(CinderSim sim, int pickupId)
        {
            IReadOnlyList<PickupState> pickups = sim.Pickups;
            for (int index = 0; index < pickups.Count; index += 1)
                if (pickups[index].Id == pickupId)
                    return true;
            return false;
        }

        private static void BuyCheapestUntilBlocked(
            ref CampaignData data,
            List<EquipmentPurchase> purchases)
        {
            while (TryCheapestSlot(in data, out string slot))
            {
                int from = TierOf(in data, slot);
                int expectedCost = ProgressionGuide.EquipCosts[from];
                int relicsBefore = data.Relics;
                Assert.That(GameDirector.TryBuyEquip(ref data, slot), Is.True);
                purchases.Add(new EquipmentPurchase
                {
                    slot = slot,
                    from = from,
                    to = TierOf(in data, slot),
                    cost = relicsBefore - data.Relics,
                });
                Assert.That(relicsBefore - data.Relics, Is.EqualTo(expectedCost),
                    "the evidence fixture must debit through the live purchase authority");
            }
        }

        private static bool TryCheapestSlot(in CampaignData data, out string slot)
        {
            slot = null;
            int cheapest = int.MaxValue;
            for (int index = 0; index < SlotIds.Length; index += 1)
            {
                string candidate = SlotIds[index];
                int tier = TierOf(in data, candidate);
                if (tier >= ProgressionGuide.EquipCap) continue;
                int cost = ProgressionGuide.EquipCosts[tier];
                if (data.Relics < cost || cost >= cheapest) continue;
                cheapest = cost;
                slot = candidate;
            }
            return slot != null;
        }

        private static int TierOf(in CampaignData data, string slot)
        {
            switch (slot)
            {
                case "weapon": return data.Weapon;
                case "lantern": return data.Lantern;
                default: return data.Cloak;
            }
        }

        private static bool IsT5(in CampaignData data)
        {
            return data.Weapon == ProgressionGuide.EquipCap
                && data.Lantern == ProgressionGuide.EquipCap
                && data.Cloak == ProgressionGuide.EquipCap;
        }

        private static int CompleteActualTrainingMastery(ref CampaignData data)
        {
            int topTier = HackSpec.TrainingTiers - 1;
            for (int index = 0; index < TrainingTrials.Ids.Length - 1; index += 1)
                Assert.That(CampaignStore.RecordTrial(ref data, index, topTier), Is.True);

            int lastTrial = TrainingTrials.Ids.Length - 1;
            Assert.That(HackConfig.TryTraining(
                    TrainingTrials.Ids[lastTrial],
                    topTier,
                    MetaStats.Of(10, 10, 10),
                    EquipTiers.Of(ProgressionGuide.EquipCap, ProgressionGuide.EquipCap, ProgressionGuide.EquipCap),
                    out var trainingConfig),
                Is.True);
            var trainingSim = new CinderSim(in trainingConfig, GameView.DungeonProgression);
            var state = new PilotState();
            bool cleared = false;
            for (int tick = 0; tick < PacifistTickLimit; tick += 1)
            {
                SimInput input = PacifistDodgerInput(trainingSim, state);
                trainingSim.Tick(in input);
                if ((trainingSim.Events & SimEvents.StageCleared) != 0 || trainingSim.StageCleared)
                {
                    cleared = true;
                    break;
                }
                if (trainingSim.Mode == SimMode.GameOver) break;
            }
            Assert.That(cleared, Is.True,
                "route E's excluded training leg must really clear before the live persistence callback is invoked");

            int relicsBefore = data.Relics;
            var host = new GameObject("Cycle9GrowthEvidence-MasteryHost");
            try
            {
                var director = host.AddComponent<GameDirector>();
                SetPrivateField(director, "_data", data);
                SetPrivateField(director, "_trialIndex", lastTrial);
                SetPrivateField(director, "_trialTier", topTier);
                MethodInfo persist = typeof(GameDirector).GetMethod(
                    "PersistTrialClear", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(persist, Is.Not.Null, "the evidence route must execute the live mastery callback");
                persist.Invoke(director, null);
                data = (CampaignData)GetPrivateField(director, "_data");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            int granted = data.Relics - relicsBefore;
            Assert.That(granted, Is.EqualTo(HackSpec.TrainingMasteryRelics));
            Assert.That(data.TrainingMasteryClaimed, Is.True);
            return granted;
        }

        private static int InvokeFirstClearRelicBonus(string stageId)
        {
            MethodInfo method = typeof(GameDirector).GetMethod(
                "FirstClearRelicBonus", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(null, new object[] { stageId });
        }

        private static int RosterMaskThroughLiveAuthority(string[] roster)
        {
            MethodInfo method = typeof(GameDirector).GetMethod(
                "RosterMaskOf", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(null, new object[] { roster });
        }

        private static void MergeRosterThroughLiveAuthority(ref CampaignData data, int rosterMask)
        {
            MethodInfo method = typeof(GameDirector).GetMethod(
                "MergeRoster", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { data, rosterMask };
            method.Invoke(null, arguments);
            data = (CampaignData)arguments[0];
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static ProtocolInputs RequireProtocolInputs()
        {
            string candidateId = RequiredEnvironment("CINDER_CYCLE9_G5_CANDIDATE_ID");
            string buildId = RequiredEnvironment("CINDER_CYCLE9_G5_BUILD_ID");
            string sourceFullHash = RequiredEnvironment("CINDER_CYCLE9_G5_SOURCE_FULL_HASH");
            Assert.That(sourceFullHash, Does.Match("^[0-9a-fA-F]{40,64}$"),
                "source identity must be a full hexadecimal revision hash");

            string spendProofPath = Path.GetFullPath(
                RequiredEnvironment("CINDER_CYCLE9_G5_SPEND_REACHABILITY_EVIDENCE"));
            string seedEvidencePath = Path.GetFullPath(
                RequiredEnvironment("CINDER_CYCLE9_G5_SEED_EVIDENCE"));
            Assert.That(File.Exists(spendProofPath), Is.True,
                "a candidate-bound shipped-UI spend reachability proof is required before generation");
            Assert.That(File.Exists(seedEvidencePath), Is.True,
                "stable seed evidence is required before generation");
            string proofText = File.ReadAllText(spendProofPath);
            Assert.That(proofText, Does.Contain(candidateId));
            Assert.That(proofText, Does.Contain(buildId),
                "the spend reachability artifact must name the exact candidate/build");

            string openS1Raw = RequiredEnvironment("CINDER_CYCLE9_G5_OPEN_S1_COUNT");
            Assert.That(int.TryParse(
                    openS1Raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int openS1Count),
                Is.True);
            Assert.That(openS1Count, Is.Zero,
                "evidence generation is blocked while any S1 defect is open");

            return new ProtocolInputs
            {
                candidate_id = candidateId,
                build_id = buildId,
                source_full_hash = sourceFullHash.ToLowerInvariant(),
                spend_proof_path = ProjectRelative(spendProofPath),
                spend_proof_sha256 = Sha256File(spendProofPath),
                seed_evidence_path = ProjectRelative(seedEvidencePath),
                seed_evidence_sha256 = Sha256File(seedEvidencePath),
            };
        }

        private static string RequiredEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            Assert.That(value, Is.Not.Null.And.Not.Empty,
                $"{name} is a required evidence-generation precondition");
            Assert.That(value, Does.Not.Contain("UNKNOWN"));
            return value;
        }

        private static string CampaignStorageKey()
        {
            FieldInfo field = typeof(CampaignStore).GetField(
                "Key", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetRawConstantValue();
        }

        private static CampaignData SaveReload(in CampaignData expected)
        {
            CampaignStore.Save(in expected);
            CampaignData loaded = CampaignStore.Load();
            AssertPersistentStateEqual(in expected, in loaded);
            return loaded;
        }

        private static void AssertPersistentStateEqual(
            in CampaignData expected,
            in CampaignData actual)
        {
            Assert.That(actual.PrologueDone, Is.EqualTo(expected.PrologueDone));
            Assert.That(actual.ClearedMask, Is.EqualTo(expected.ClearedMask));
            Assert.That(actual.Weapon, Is.EqualTo(expected.Weapon));
            Assert.That(actual.Lantern, Is.EqualTo(expected.Lantern));
            Assert.That(actual.Cloak, Is.EqualTo(expected.Cloak));
            Assert.That(actual.Relics, Is.EqualTo(expected.Relics));
            Assert.That(actual.Points, Is.EqualTo(expected.Points));
            Assert.That(actual.TrialTiers, Is.EqualTo(expected.TrialTiers));
            Assert.That(actual.TrainingMasteryClaimed, Is.EqualTo(expected.TrainingMasteryClaimed));
            Assert.That(actual.SigilsOwned, Is.EqualTo(expected.SigilsOwned));
            Assert.That(actual.SigilSlot0, Is.EqualTo(expected.SigilSlot0));
            Assert.That(actual.SigilSlot1, Is.EqualTo(expected.SigilSlot1));
            Assert.That(actual.Active ?? string.Empty, Is.EqualTo(expected.Active ?? string.Empty));
            Assert.That(actual.Roster ?? Array.Empty<string>(),
                Is.EqualTo(expected.Roster ?? Array.Empty<string>()));
            Assert.That(actual.ActiveSlots ?? Array.Empty<string>(),
                Is.EqualTo(expected.ActiveSlots ?? Array.Empty<string>()));
        }

        private static MasteryEvent BuildMasteryEvent(
            ProtocolInputs inputs,
            string pilotId,
            string routeId,
            in CampaignData before,
            in CampaignData after,
            int granted)
        {
            Assert.That(routeId, Is.EqualTo("E"));
            Assert.That(after.TrainingMasteryClaimed, Is.True);
            Assert.That(after.Relics - before.Relics, Is.EqualTo(granted));
            return new MasteryEvent
            {
                run_id = RunId,
                candidate_id = inputs.candidate_id,
                build_id = inputs.build_id,
                source_full_hash = inputs.source_full_hash,
                pilot_id = pilotId,
                route_id = routeId,
                occurrence = "before-session-1",
                occurred_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                mode = GameMode.Training.ToString(),
                relics_before = before.Relics,
                training_mastery_relics = granted,
                relics_after = after.Relics,
                training_mastery_claimed = after.TrainingMasteryClaimed,
                persisted_before_first_dungeon_session = true,
                protocol_deviations = Array.Empty<string>(),
                unknown_fields = Array.Empty<string>(),
                evidence_classification = "scripted-deterministic-not-human-evidence",
            };
        }

        private static PolicyCell[] BuildPolicyCells()
        {
            var cells = new List<PolicyCell>(PilotIds.Length * RouteIds.Length);
            foreach (string pilotId in PilotIds)
            {
                foreach (string routeId in RouteIds)
                {
                    cells.Add(new PolicyCell
                    {
                        pilot_id = pilotId,
                        route_id = routeId,
                        policy_id = InputScriptId(pilotId),
                        policy_sha256 = InputPolicySha256(pilotId),
                        reference_rank_grid =
                            "persisted-campaign-recurrence;fresh-start-W0-L0-C0",
                        difficulty = Difficulty.Normal.ToString(),
                        starting_state = routeId == "E"
                            ? "fresh-campaign;mastery+2-persisted-before-session-1"
                            : "fresh-campaign",
                    });
                }
            }
            Assert.That(cells.Count, Is.EqualTo(25));
            return cells.ToArray();
        }

        private static void WriteEvidenceAtomically(
            string outputDirectory,
            string rawPath,
            string summaryPath,
            string masteryPath,
            string manifestPath,
            List<EvidenceRow> rows,
            List<MasteryEvent> masteryEvents,
            string summaryJson,
            ProtocolInputs inputs)
        {
            Directory.CreateDirectory(outputDirectory);
            string[] destinations = { rawPath, summaryPath, masteryPath, manifestPath };
            for (int index = 0; index < destinations.Length; index += 1)
            {
                Assert.That(File.Exists(destinations[index]), Is.False,
                    "cycle evidence is immutable; use a new candidate/build identity instead of overwriting an existing artifact");
            }
            string suffix = ".tmp-" + Guid.NewGuid().ToString("N");
            string rawTemp = rawPath + suffix;
            string summaryTemp = summaryPath + suffix;
            string masteryTemp = masteryPath + suffix;
            string manifestTemp = manifestPath + suffix;
            try
            {
                WriteJsonLines(rawTemp, rows);
                File.WriteAllText(summaryTemp, summaryJson + Environment.NewLine);
                WriteJsonLines(masteryTemp, masteryEvents);
                var manifest = new EvidenceManifest
                {
                    run_id = RunId,
                    candidate_id = inputs.candidate_id,
                    build_id = inputs.build_id,
                    source_full_hash = inputs.source_full_hash,
                    harness_version = HarnessVersion,
                    unity_version = Application.unityVersion,
                    generated_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    seed_evidence_path = inputs.seed_evidence_path,
                    seed_evidence_sha256 = inputs.seed_evidence_sha256,
                    spend_reachability_evidence_path = inputs.spend_proof_path,
                    spend_reachability_evidence_sha256 = inputs.spend_proof_sha256,
                    raw_rows_sha256 = Sha256File(rawTemp),
                    summary_sha256 = Sha256File(summaryTemp),
                    mastery_events_sha256 = Sha256File(masteryTemp),
                    row_count = rows.Count,
                    mastery_event_count = masteryEvents.Count,
                    policy_cells = BuildPolicyCells(),
                    open_s1_count = 0,
                    status = "MEASURED_NOT_ADJUDICATED",
                };
                string manifestJson = JsonUtility.ToJson(manifest, true);
                Assert.That(manifestJson, Does.Not.Contain("\"status\": \"PASS\""));
                File.WriteAllText(manifestTemp, manifestJson + Environment.NewLine);

                MoveIntoPlace(rawTemp, rawPath);
                MoveIntoPlace(summaryTemp, summaryPath);
                MoveIntoPlace(masteryTemp, masteryPath);
                MoveIntoPlace(manifestTemp, manifestPath);
            }
            finally
            {
                DeleteIfExists(rawTemp);
                DeleteIfExists(summaryTemp);
                DeleteIfExists(masteryTemp);
                DeleteIfExists(manifestTemp);
            }
        }

        private static void WriteJsonLines<T>(string path, List<T> values)
        {
            var lines = new string[values.Count];
            for (int index = 0; index < values.Count; index += 1)
                lines[index] = JsonUtility.ToJson(values[index], false);
            File.WriteAllLines(path, lines);
        }

        private static void MoveIntoPlace(string source, string destination)
        {
            File.Move(source, destination);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static string ProjectRelative(string absolutePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return absolutePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? absolutePath.Substring(root.Length + 1).Replace('\\', '/')
                : absolutePath;
        }

        private static string Sha256File(string path)
        {
            return Sha256(File.ReadAllBytes(path));
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index += 1)
                    builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static EvidenceRow BuildRow(
            ProtocolInputs inputs,
            string pilotId,
            string routeId,
            int sessionIndex,
            in CampaignData before,
            in CampaignData afterSettlement,
            in CampaignData afterPurchases,
            in StageEntry stage,
            CinderSim sim,
            RunObservation observation,
            string routeAction,
            bool pactArmed,
            int pactMultiplier,
            bool firstClear,
            int firstClearRelics,
            bool spendOpportunity,
            List<RankTransition> rankTransitions,
            List<EquipmentPurchase> purchases,
            bool t5Reached)
        {
            return new EvidenceRow
            {
                identity = new Identity
                {
                    run_id = RunId,
                    candidate_id = inputs.candidate_id,
                    build_id = inputs.build_id,
                    source_full_hash = inputs.source_full_hash,
                    harness_version = HarnessVersion,
                    unity_version = Application.unityVersion,
                    policy_id = InputScriptId(pilotId),
                    policy_sha256 = InputPolicySha256(pilotId),
                    pilot_id = pilotId,
                    route_id = routeId,
                    session_index = sessionIndex,
                },
                eligibility = new Eligibility
                {
                    prologue_done = before.PrologueDone,
                    spend_surface_reachable = true,
                    spend_reachability_evidence_path = inputs.spend_proof_path,
                    spend_reachability_evidence_sha256 = inputs.spend_proof_sha256,
                    mode = GameMode.Dungeon.ToString(),
                    settlement_reason = observation.reason,
                    spend_opportunity_reached = spendOpportunity,
                },
                execution = new Execution
                {
                    stage = "Stage 2 / Phase 2a",
                    stage_id = stage.Id,
                    sim_anchor_id = stage.SimAnchorId,
                    difficulty = Difficulty.Normal.ToString(),
                    loadout = LoadoutId(in before),
                    sigil_loadout = before.SigilSlot0 + "/" + before.SigilSlot1,
                    input_script_id = InputScriptId(pilotId),
                    input_policy_sha256 = InputPolicySha256(pilotId),
                    route_action = routeAction,
                    outcome = observation.outcome,
                    terminal_tick = observation.ticks,
                    elapsed_seconds = observation.ticks * SimConfig.FixedStep,
                },
                progression_before = StateOf(in before),
                progression_after_settlement = StateOf(in afterSettlement),
                reward_components = new RewardComponents
                {
                    stage_id = stage.Id,
                    run_relics = sim.Relics,
                    pact_armed = pactArmed,
                    pact_multiplier = pactMultiplier,
                    first_clear = firstClear,
                    first_clear_relics = firstClearRelics,
                    training_mastery_relics = 0,
                    rank_transitions = rankTransitions.ToArray(),
                },
                spend = new Spend
                {
                    equipment_purchases = purchases.ToArray(),
                    sigil_relics_spent = 0,
                },
                progression_after = StateOf(in afterPurchases, t5Reached),
                fairness = new Fairness
                {
                    paid_offer_visible = false,
                    paid_power_applied = false,
                },
                comeback = new Comeback
                {
                    activations = observation.activations.Count,
                    activation_rows = observation.activations.ToArray(),
                },
                protocol_deviations = Array.Empty<string>(),
                unknown_fields = Array.Empty<string>(),
                evidence_classification = "scripted-deterministic-not-human-evidence",
            };
        }

        private static ProgressionState StateOf(in CampaignData data, bool t5Reached = false)
        {
            return new ProgressionState
            {
                relics = data.Relics,
                weapon_tier = data.Weapon,
                lantern_tier = data.Lantern,
                cloak_tier = data.Cloak,
                cleared_mask = data.ClearedMask,
                sigils_owned = data.SigilsOwned,
                sigil_slot_0 = data.SigilSlot0,
                sigil_slot_1 = data.SigilSlot1,
                roster = data.Roster ?? Array.Empty<string>(),
                active_companion = data.Active ?? string.Empty,
                active_slots = data.ActiveSlots ?? Array.Empty<string>(),
                training_mastery_claimed = data.TrainingMasteryClaimed,
                t5_reached = t5Reached,
            };
        }

        private static string LoadoutId(in CampaignData data)
        {
            string activeSlots = data.ActiveSlots == null
                ? string.Empty
                : string.Join(",", data.ActiveSlots);
            return string.Format(
                CultureInfo.InvariantCulture,
                "campaign-persisted-W{0}-L{1}-C{2}-active:{3}-slots:{4}",
                data.Weapon,
                data.Lantern,
                data.Cloak,
                data.Active ?? string.Empty,
                activeSlots);
        }

        private static string InputScriptId(string pilotId)
        {
            return "cycle9-g5/" + pilotId + "-v3";
        }

        private static string InputPolicySha256(string pilotId)
        {
            string definition;
            switch (pilotId)
            {
                case "melee-rusher":
                    definition = "nearest-enemy;move-toward;attack-always;dash-when-distance>150";
                    break;
                case "kiter":
                    definition = "nearest-enemy;retreat<PlayerAttackRange*0.75;approach>PlayerAttackRange-10;attack+nova+ward";
                    break;
                case "skill-spammer":
                    definition = "arena-anchor-60;nova+bolt+pulse+ward;attack-at-max-charge";
                    break;
                case "companion-commander":
                    definition = "kiter-PlayerAttackRange*0.75-to-PlayerAttackRange-10;hold-at-active-gimmick<=200;recall-otherwise";
                    break;
                case "pacifist-dodger":
                    definition = "no-attacks;four-waypoint-loop;dash-on-first-covering-telegraph";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pilotId), pilotId, null);
            }
            return Sha256(Encoding.UTF8.GetBytes(definition));
        }

        private static void AssertEvidenceRow(EvidenceRow row)
        {
            Assert.That(row.identity.run_id, Is.EqualTo(RunId));
            Assert.That(row.identity.candidate_id, Is.Not.Null.And.Not.Empty);
            Assert.That(row.identity.build_id, Is.Not.Null.And.Not.Empty);
            Assert.That(row.identity.source_full_hash, Has.Length.GreaterThanOrEqualTo(40));
            Assert.That(row.identity.policy_sha256, Has.Length.EqualTo(64));
            Assert.That(PilotIds, Does.Contain(row.identity.pilot_id));
            Assert.That(RouteIds, Does.Contain(row.identity.route_id));
            Assert.That(row.identity.session_index, Is.InRange(1, SessionLimit));
            Assert.That(row.eligibility.prologue_done, Is.True);
            Assert.That(row.eligibility.spend_surface_reachable, Is.True);
            Assert.That(row.eligibility.spend_reachability_evidence_sha256, Has.Length.EqualTo(64));
            Assert.That(row.eligibility.mode, Is.EqualTo(GameMode.Dungeon.ToString()));
            Assert.That(row.execution.stage_id, Is.EqualTo(row.reward_components.stage_id));
            Assert.That(row.execution.sim_anchor_id, Is.Not.Null.And.Not.Empty);
            Assert.That(row.execution.input_policy_sha256, Is.EqualTo(row.identity.policy_sha256));
            Assert.That(row.reward_components.run_relics, Is.GreaterThanOrEqualTo(0));
            Assert.That(row.reward_components.pact_multiplier,
                Is.EqualTo(1).Or.EqualTo(GameDirector.PactRelicMultiplier));
            for (int index = 0; index < row.reward_components.rank_transitions.Length; index += 1)
            {
                RankTransition transition = row.reward_components.rank_transitions[index];
                Assert.That(transition.source, Is.Not.Null.And.Not.Empty);
                Assert.That(transition.grade, Is.Not.Null.And.Not.Empty);
                Assert.That(transition.to, Is.GreaterThan(transition.from));
            }
            Assert.That(row.spend.sigil_relics_spent, Is.Zero);
            Assert.That(row.progression_after.relics, Is.GreaterThanOrEqualTo(0));
            Assert.That(row.protocol_deviations, Is.Empty);
            Assert.That(row.unknown_fields, Is.Empty);
            Assert.That(row.evidence_classification, Does.Contain("not-human-evidence"));
        }

        private static int CountRows(List<EvidenceRow> rows, string pilotId, string routeId)
        {
            int count = 0;
            for (int index = 0; index < rows.Count; index += 1)
                if (rows[index].identity.pilot_id == pilotId && rows[index].identity.route_id == routeId)
                    count += 1;
            return count;
        }

        private static PilotSummary SummarizePilot(string pilotId, List<RouteSummary> routes)
        {
            var ordered = new List<int>(routes.Count);
            for (int index = 0; index < routes.Count; index += 1)
            {
                int observed = routes[index].first_t5_session_observed;
                ordered.Add(observed > 0 && observed <= 20 ? observed : int.MaxValue);
            }
            ordered.Sort();
            return new PilotSummary
            {
                pilot_id = pilotId,
                routes = routes.ToArray(),
                minimum_n_t5 = DisplayCensored(ordered[0]),
                median_n_t5 = DisplayCensored(ordered[ordered.Count / 2]),
                maximum_n_t5 = DisplayCensored(ordered[ordered.Count - 1]),
            };
        }

        private static string DisplayCensored(int value)
        {
            return value == int.MaxValue ? ">20" : value.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class PilotState
        {
            public int waypoint;
            public bool telegraphCoveredLastTick;
            public bool companionHold;
        }

        private struct RunObservation
        {
            public string reason;
            public string outcome;
            public int ticks;
            public List<ActivationRow> activations;
            public List<RankTransition> rank_transitions;
        }

        [Serializable]
        private sealed class EvidenceRow
        {
            public Identity identity;
            public Eligibility eligibility;
            public Execution execution;
            public ProgressionState progression_before;
            public ProgressionState progression_after_settlement;
            public RewardComponents reward_components;
            public Spend spend;
            public ProgressionState progression_after;
            public Fairness fairness;
            public Comeback comeback;
            public string[] protocol_deviations;
            public string[] unknown_fields;
            public string evidence_classification;
        }

        [Serializable]
        private sealed class Identity
        {
            public string run_id;
            public string candidate_id;
            public string build_id;
            public string source_full_hash;
            public string harness_version;
            public string unity_version;
            public string policy_id;
            public string policy_sha256;
            public string pilot_id;
            public string route_id;
            public int session_index;
        }

        [Serializable]
        private sealed class Eligibility
        {
            public bool prologue_done;
            public bool spend_surface_reachable;
            public string spend_reachability_evidence_path;
            public string spend_reachability_evidence_sha256;
            public string mode;
            public string settlement_reason;
            public bool spend_opportunity_reached;
        }

        [Serializable]
        private sealed class Execution
        {
            public string stage;
            public string stage_id;
            public string sim_anchor_id;
            public string difficulty;
            public string loadout;
            public string sigil_loadout;
            public string input_script_id;
            public string input_policy_sha256;
            public string route_action;
            public string outcome;
            public int terminal_tick;
            public float elapsed_seconds;
        }

        [Serializable]
        private sealed class ProgressionState
        {
            public int relics;
            public int weapon_tier;
            public int lantern_tier;
            public int cloak_tier;
            public int cleared_mask;
            public int sigils_owned;
            public int sigil_slot_0;
            public int sigil_slot_1;
            public string[] roster;
            public string active_companion;
            public string[] active_slots;
            public bool training_mastery_claimed;
            public bool t5_reached;
        }

        [Serializable]
        private sealed class RewardComponents
        {
            public string stage_id;
            public int run_relics;
            public bool pact_armed;
            public int pact_multiplier;
            public bool first_clear;
            public int first_clear_relics;
            public int training_mastery_relics;
            public RankTransition[] rank_transitions;
        }

        [Serializable]
        private sealed class RankTransition
        {
            public string slot;
            public int from;
            public int to;
            public string source;
            public string grade;
        }

        [Serializable]
        private sealed class Spend
        {
            public EquipmentPurchase[] equipment_purchases;
            public int sigil_relics_spent;
        }

        [Serializable]
        private sealed class EquipmentPurchase
        {
            public string slot;
            public int from;
            public int to;
            public int cost;
        }

        [Serializable]
        private sealed class Fairness
        {
            public bool paid_offer_visible;
            public bool paid_power_applied;
        }

        [Serializable]
        private sealed class Comeback
        {
            public int activations;
            public ActivationRow[] activation_rows;
        }

        [Serializable]
        private sealed class ActivationRow
        {
            public int tick;
            public string clause;
            public float health_before;
            public float max_health;
            public bool isolated_reversal;
        }

        [Serializable]
        private sealed class EvidenceSummary
        {
            public string run_id;
            public string candidate_id;
            public string build_id;
            public string source_full_hash;
            public string harness_version;
            public string unity_version;
            public string stage;
            public string status;
            public bool scripted_pilots_are_human_evidence;
            public string raw_rows_path;
            public string mastery_events_path;
            public int row_count;
            public PilotSummary[] pilot_summaries;
            public string[] protocol_deviations;
            public string[] unknown_fields;
            public string[] unresolved_route_semantics;
        }

        private sealed class ProtocolInputs
        {
            public string candidate_id;
            public string build_id;
            public string source_full_hash;
            public string spend_proof_path;
            public string spend_proof_sha256;
            public string seed_evidence_path;
            public string seed_evidence_sha256;
        }

        [Serializable]
        private sealed class MasteryEvent
        {
            public string run_id;
            public string candidate_id;
            public string build_id;
            public string source_full_hash;
            public string pilot_id;
            public string route_id;
            public string occurrence;
            public string occurred_at_utc;
            public string mode;
            public int relics_before;
            public int training_mastery_relics;
            public int relics_after;
            public bool training_mastery_claimed;
            public bool persisted_before_first_dungeon_session;
            public string[] protocol_deviations;
            public string[] unknown_fields;
            public string evidence_classification;
        }

        [Serializable]
        private sealed class EvidenceManifest
        {
            public string run_id;
            public string candidate_id;
            public string build_id;
            public string source_full_hash;
            public string harness_version;
            public string unity_version;
            public string generated_at_utc;
            public string seed_evidence_path;
            public string seed_evidence_sha256;
            public string spend_reachability_evidence_path;
            public string spend_reachability_evidence_sha256;
            public string raw_rows_sha256;
            public string summary_sha256;
            public string mastery_events_sha256;
            public int row_count;
            public int mastery_event_count;
            public PolicyCell[] policy_cells;
            public int open_s1_count;
            public string status;
        }


        [Serializable]
        private sealed class PolicyCell
        {
            public string pilot_id;
            public string route_id;
            public string policy_id;
            public string policy_sha256;
            public string reference_rank_grid;
            public string difficulty;
            public string starting_state;
        }
        [Serializable]
        private sealed class PilotSummary
        {
            public string pilot_id;
            public RouteSummary[] routes;
            public string minimum_n_t5;
            public string median_n_t5;
            public string maximum_n_t5;
        }

        [Serializable]
        private sealed class RouteSummary
        {
            public string route_id;
            public string n_t5;
            public int first_t5_session_observed;
            public int sessions_recorded;
        }
    }
}
