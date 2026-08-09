using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    /// <summary>
    /// Unity-runtime recorder for the cycle-9 G2/G3 matrix in QA Test Plan §1/§2.1.
    /// This is an evidence producer, not a gate evaluator: it preserves N=20 for every
    /// matchup cell and writes determinism replays as non-denominator rows.
    ///
    /// Usage:
    ///   bash tools/unity_batch.sh method CinderCourt.EditorTools.Cycle9CombatEvidenceRecorder.Record
    /// </summary>
    public static class Cycle9CombatEvidenceRecorder
    {
        const string SchemaVersion = "cycle9-g2-g3-v2";
        const string RunId = "20260808-achilles-quality";
        const string OutputDirectory = "_workspace/current/qa/cycle9-g2-g3";
        const string ExecutionKind = "scripted-headless-editor";
        const string Invocation = "bash tools/unity_batch.sh method CinderCourt.EditorTools.Cycle9CombatEvidenceRecorder.Record";
        const int TicksPerSecond = 60;
        const int MaxTicks = 5 * 60 * TicksPerSecond;
        const int SchedulesPerCell = 20;
        const int DeterminismReplayCount = 10;
        const int ReferenceLoadoutCount = 3;
        const int LoadoutsPerArchetype = ReferenceLoadoutCount + 1;
        const int DifficultyCount = 4;
        const int EventBitCount = 27;
        const string DamageSourceWarning = "damage_source_not_observable_sim_exposes_health_delta_and_PlayerDamaged_only";
        const string PairEvWarning = "pair_ev_not_observable_no_per-source_enemy-damage_ledger";
        const string LoopReentryWarning = "loop_reentry_not_observable_in_headless_sim_run";
        const string A7ScopeWarning = "A7_stage_and_spend_order_not_observable_combat_growth_choice_only";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        static readonly char[] CsvSpecials = { ',', '"', '\r', '\n' };

        static readonly Difficulty[] Difficulties =
        {
            Difficulty.Story,
            Difficulty.Normal,
            Difficulty.Hard,
            Difficulty.Nightmare,
        };

        static readonly InputSchedule[] Schedules =
        {
            new InputSchedule("S01",  1.0000f,  0.0000f,  0, 11, 43, 0,  71, 181, 120, 1),
            new InputSchedule("S02",  0.9511f,  0.3090f,  3, 13, 47, 1,  79, 193, 132, 2),
            new InputSchedule("S03",  0.8090f,  0.5878f,  5, 17, 53, 2,  83, 211, 144, 3),
            new InputSchedule("S04",  0.5878f,  0.8090f,  7, 19, 59, 3,  89, 223, 156, 1),
            new InputSchedule("S05",  0.3090f,  0.9511f, 11, 23, 61, 0,  97, 239, 168, 2),
            new InputSchedule("S06",  0.0000f,  1.0000f, 13, 29, 67, 1, 101, 251, 180, 3),
            new InputSchedule("S07", -0.3090f,  0.9511f, 17, 31, 71, 2, 103, 263, 192, 1),
            new InputSchedule("S08", -0.5878f,  0.8090f, 19, 37, 73, 3, 107, 277, 204, 2),
            new InputSchedule("S09", -0.8090f,  0.5878f, 23, 41, 79, 0, 109, 281, 216, 3),
            new InputSchedule("S10", -0.9511f,  0.3090f, 29, 43, 83, 1, 113, 293, 228, 1),
            new InputSchedule("S11", -1.0000f,  0.0000f, 31, 47, 89, 2, 127, 307, 240, 2),
            new InputSchedule("S12", -0.9511f, -0.3090f, 37, 53, 97, 3, 131, 311, 252, 3),
            new InputSchedule("S13", -0.8090f, -0.5878f, 41, 59,101, 0, 137, 331, 264, 1),
            new InputSchedule("S14", -0.5878f, -0.8090f, 43, 61,103, 1, 139, 347, 276, 2),
            new InputSchedule("S15", -0.3090f, -0.9511f, 47, 67,107, 2, 149, 349, 288, 3),
            new InputSchedule("S16",  0.0000f, -1.0000f, 53, 71,109, 3, 151, 367, 300, 1),
            new InputSchedule("S17",  0.3090f, -0.9511f, 59, 73,113, 0, 157, 373, 312, 2),
            new InputSchedule("S18",  0.5878f, -0.8090f, 61, 79,127, 1, 163, 383, 324, 3),
            new InputSchedule("S19",  0.8090f, -0.5878f, 67, 83,131, 2, 167, 397, 336, 1),
            new InputSchedule("S20",  0.9511f, -0.3090f, 71, 89,137, 3, 173, 401, 348, 2),
        };

        static readonly ArchetypePlan[] Archetypes =
        {
            new ArchetypePlan("A1", "combo-rusher", "C9-G3-A1-combo-rusher", "scripted-A1",
                Build("A1-declared", "archetype", MetaStats.Of(6, 2, 2), EquipTiers.Of(5, 1, 3),
                    SigilLoadout.Of(SigilKind.Ignition, SigilFace.B, SigilKind.Executioner, SigilFace.B))),
            new ArchetypePlan("A2", "kiter-dodger", "C9-G3-A2-kiter-dodger", "scripted-A2",
                Build("A2-declared", "archetype", MetaStats.Of(2, 3, 5), EquipTiers.Of(3, 3, 5),
                    SigilLoadout.Of(SigilKind.Countercurrent, SigilFace.A, SigilKind.Witness, SigilFace.A))),
            new ArchetypePlan("A3", "defensive-turtle", "C9-G3-A3-defensive-turtle", "scripted-A3",
                Build("A3-declared", "archetype", MetaStats.Of(1, 7, 2), EquipTiers.Of(2, 3, 5),
                    SigilLoadout.Of(SigilKind.Verdict, SigilFace.A, SigilKind.Executioner, SigilFace.A))),
            new ArchetypePlan("A4", "skill-economist", "C9-G3-A4-skill-economist", "scripted-A4",
                Build("A4-declared", "archetype", MetaStats.Of(4, 2, 4), EquipTiers.Of(3, 5, 2),
                    SigilLoadout.Of(SigilKind.Ignition, SigilFace.B, SigilKind.Witness, SigilFace.B))),
            new ArchetypePlan("A5", "companion-commander", "C9-G3-A5-companion-commander", "scripted-A5",
                Build("A5-declared", "archetype", MetaStats.Of(3, 4, 3), EquipTiers.Of(3, 3, 4),
                    SigilLoadout.Of(SigilKind.Verdict, SigilFace.B, SigilKind.Countercurrent, SigilFace.B),
                    new[] { "ember-cohort-echo", "shade-echo", "possessed-echo" },
                    (1 << (int)EnemyVisual.EmberCohort) | (1 << (int)EnemyVisual.Shade) | (1 << (int)EnemyVisual.Possessed))),
            new ArchetypePlan("A6", "low-APM-casual", "C9-G3-A6-low-APM-casual", "scripted-A6",
                Build("A6-declared", "archetype", MetaStats.Of(2, 6, 2), EquipTiers.Of(2, 2, 5),
                    SigilLoadout.Of(SigilKind.Countercurrent, SigilFace.A, SigilKind.Ignition, SigilFace.A))),
            new ArchetypePlan("A7", "growth-optimizer", "C9-G3-A7-combat-growth-choice-sensitivity", "scripted-A7",
                Build("A7-declared", "archetype", MetaStats.Of(5, 3, 2), EquipTiers.Of(4, 2, 4),
                    SigilLoadout.Of(SigilKind.Witness, SigilFace.B, SigilKind.Verdict, SigilFace.B))),
        };

        public static void Record()
        {
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                ValidateProtocolTables();

                DateTime started = DateTime.UtcNow;
                string startedUtc = started.ToString("O", Invariant);
                string executionId = started.ToString("yyyyMMddTHHmmssfffZ", Invariant)
                    + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                string rawPath = OutputDirectory + "/raw-" + executionId + ".csv";
                string summaryPath = OutputDirectory + "/summary-" + executionId + ".json";
                string recorderLogPath = OutputDirectory + "/recorder-" + executionId + ".log";
                string runMetaPath = OutputDirectory + "/run-meta-" + executionId + ".json";
                string sourceIdentity = ComputeSourceIdentity();
                string candidateId = "cycle9-unity-source-" + sourceIdentity.Substring(0, 16);
                string buildId = "unity-editor-" + Application.unityVersion + "-" + sourceIdentity.Substring(0, 16);
                string gitSha;
                bool gitDirty;
                ReadGitIdentity(out gitSha, out gitDirty);
                int expectedCells = Archetypes.Length * StageCatalog.Entries.Count * DifficultyCount * LoadoutsPerArchetype;
                int expectedBaseRows = expectedCells * SchedulesPerCell;

                long baseRows = 0;
                long replayRows = 0;
                long replayCandidates = 0;
                long replayMismatches = 0;
                int cellsWritten = 0;
                DateTime ended;

                using (var raw = NewWriter(rawPath))
                using (var summary = NewWriter(summaryPath))
                {
                    WriteRawHeader(raw);
                    WriteSummaryPreamble(summary, executionId, startedUtc, candidateId, buildId,
                        sourceIdentity, gitSha, gitDirty, rawPath, summaryPath, expectedCells, expectedBaseRows);

                    bool firstCell = true;
                    for (int archetypeIndex = 0; archetypeIndex < Archetypes.Length; archetypeIndex += 1)
                    {
                        ArchetypePlan archetype = Archetypes[archetypeIndex];
                        LoadoutPlan[] loadouts = LoadoutsFor(archetype);
                        for (int stageIndex = 0; stageIndex < StageCatalog.Entries.Count; stageIndex += 1)
                        {
                            StageEntry stage = StageCatalog.Entries[stageIndex];
                            for (int difficultyIndex = 0; difficultyIndex < Difficulties.Length; difficultyIndex += 1)
                            {
                                Difficulty difficulty = Difficulties[difficultyIndex];
                                for (int loadoutIndex = 0; loadoutIndex < loadouts.Length; loadoutIndex += 1)
                                {
                                    LoadoutPlan loadout = loadouts[loadoutIndex];
                                    var baselines = new RunResult[SchedulesPerCell];
                                    var inputDigests = new HashSet<string>(StringComparer.Ordinal);
                                    int clears = 0;
                                    int defeats = 0;
                                    int timeouts = 0;

                                    for (int scheduleIndex = 0; scheduleIndex < Schedules.Length; scheduleIndex += 1)
                                    {
                                        RunResult result = Run(stage, difficulty, archetype, loadout, Schedules[scheduleIndex]);
                                        baselines[scheduleIndex] = result;
                                        inputDigests.Add(result.InputDigest);
                                        if (result.Outcome == "clear") clears += 1;
                                        else if (result.Outcome == "defeat") defeats += 1;
                                        else timeouts += 1;
                                        WriteRawRow(raw, executionId, startedUtc, candidateId, buildId,
                                            stage, difficulty, archetype, loadout, Schedules[scheduleIndex],
                                            result, "base", 0, true, string.Empty, string.Empty, string.Empty);
                                        baseRows += 1;
                                    }
                                    if (inputDigests.Count != SchedulesPerCell)
                                    {
                                        throw new InvalidOperationException(BaselineKey(archetype, stage, difficulty, loadout, Schedules[0])
                                            + ": expected 20 unique actual input digests, observed " + inputDigests.Count);
                                    }

                                    bool bandEdgeCell = clears == 9 || clears == 11;
                                    int cellReplayCandidates = 0;
                                    int cellReplayRows = 0;
                                    int cellReplayMismatches = 0;
                                    for (int scheduleIndex = 0; scheduleIndex < Schedules.Length; scheduleIndex += 1)
                                    {
                                        RunResult baseline = baselines[scheduleIndex];
                                        if (baseline.Outcome == "clear" && !bandEdgeCell) continue;

                                        cellReplayCandidates += 1;
                                        replayCandidates += 1;
                                        for (int replayIndex = 1; replayIndex <= DeterminismReplayCount; replayIndex += 1)
                                        {
                                            RunResult replay = Run(stage, difficulty, archetype, loadout, Schedules[scheduleIndex]);
                                            bool equal = replay.DeterministicallyEquals(baseline);
                                            string firstDiffTick = equal ? string.Empty : replay.FirstDifferenceTick(baseline).ToString(Invariant);
                                            if (!equal)
                                            {
                                                cellReplayMismatches += 1;
                                                replayMismatches += 1;
                                            }
                                            WriteRawRow(raw, executionId, startedUtc, candidateId, buildId,
                                                stage, difficulty, archetype, loadout, Schedules[scheduleIndex],
                                                replay, "determinism-replay", replayIndex, false,
                                                BaselineKey(archetype, stage, difficulty, loadout, Schedules[scheduleIndex]),
                                                equal ? "true" : "false", firstDiffTick);
                                            replayRows += 1;
                                            cellReplayRows += 1;
                                        }
                                    }

                                    WriteCellSummary(summary, ref firstCell, archetype, stage, difficulty, loadout,
                                        clears, defeats, timeouts, inputDigests.Count, bandEdgeCell, cellReplayCandidates,
                                        cellReplayRows, cellReplayMismatches);
                                    cellsWritten += 1;
                                    if ((cellsWritten % 25) == 0)
                                    {
                                        raw.Flush();
                                        summary.Flush();
                                        Debug.Log("Cycle9CombatEvidenceRecorder: cells " + cellsWritten + "/" + expectedCells);
                                    }
                                }
                            }
                        }
                    }

                    ended = DateTime.UtcNow;
                    WriteSummaryTrailer(summary, ended.ToString("O", Invariant), cellsWritten, baseRows,
                        replayCandidates, replayRows, replayMismatches, expectedCells, expectedBaseRows);
                }

                string recorderLog = "schema_version=" + SchemaVersion + "\n"
                    + "execution_id=" + executionId + "\n"
                    + "started_utc=" + startedUtc + "\n"
                    + "ended_utc=" + ended.ToString("O", Invariant) + "\n"
                    + "command=" + Invocation + "\n"
                    + "raw_path=" + rawPath + "\n"
                    + "summary_path=" + summaryPath + "\n"
                    + "base_rows=" + baseRows.ToString(Invariant) + "\n"
                    + "replay_rows=" + replayRows.ToString(Invariant) + "\n"
                    + "replay_mismatches=" + replayMismatches.ToString(Invariant) + "\n";
                File.WriteAllText(recorderLogPath, recorderLog, Utf8NoBom);
                string rawSha = FileSha256(rawPath);
                string summarySha = FileSha256(summaryPath);
                string recorderLogSha = FileSha256(recorderLogPath);
                WriteRunMeta(runMetaPath, executionId, startedUtc, ended.ToString("O", Invariant),
                    candidateId, buildId, sourceIdentity, gitSha, gitDirty, rawPath, rawSha,
                    summaryPath, summarySha, recorderLogPath, recorderLogSha, baseRows, replayRows);

                Debug.Log("Cycle9CombatEvidenceRecorder: wrote " + rawPath + ", " + summaryPath
                    + " and " + runMetaPath + " (base=" + baseRows + ", replay=" + replayRows
                    + ", mismatches=" + replayMismatches + ")");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Cycle9CombatEvidenceRecorder: " + error);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }
                throw;
            }
        }

        static RunResult Run(
            in StageEntry stage,
            Difficulty difficulty,
            ArchetypePlan archetype,
            in LoadoutPlan loadout,
            in InputSchedule schedule)
        {
            HackConfig config;
            if (!HackConfig.TryDungeon(stage.SimAnchorId, loadout.Meta, loadout.Equip,
                    loadout.Companions, loadout.RosterMask, out config))
            {
                throw new InvalidOperationException("Could not resolve StageCatalog anchor " + stage.SimAnchorId);
            }
            if (stage.HazardOverride != null) config.Hazards = stage.HazardOverride;
            config.Sigils = loadout.Sigils;
            config.Difficulty = difficulty;

            // Shipping truth: GameView constructs dungeon runs with this exact opt-in table.
            var sim = new CinderSim(in config, GameView.DungeonProgression);
            var result = new RunResult();
            result.Outcome = "timeout";
            result.RelicsStart = sim.Relics;
            result.MinHealth = sim.Player.Health;
            result.BossSpawnTick = -1;
            result.BossTtkTicks = -1;
            result.BossTtkStatus = "not-reached";
            result.WaveTtks = new StringBuilder(160);
            result.EventCounts = new int[EventBitCount];
            result.TraceCheckpoints = new List<ulong>(MaxTicks + 1);
            result.HazardFingerprint = HazardFingerprint(config.Hazards);

            int currentWave = sim.Wave;
            int waveStartTick = 0;
            bool waveRecorded = false;
            bool bossSeen = false;
            var trace = TraceHash.Create(true);
            var inputTrace = TraceHash.Create(true);
            HashSnapshot(sim, 0, ref trace);
            trace.CommitCheckpoint();
            result.TraceCheckpoints.Add(trace.Value);

            for (int tick = 1; tick <= MaxTicks; tick += 1)
            {
                SimInput input = PolicyInput(archetype, schedule, sim, tick - 1);
                HashInput(input, tick - 1, ref inputTrace);
                inputTrace.CommitCheckpoint();
                float healthBefore = sim.Player.Health;
                sim.Tick(in input);
                float healthAfter = sim.Player.Health;
                SimEvents events = sim.Events;

                if (healthAfter < healthBefore) result.PlayerDamageTotal += healthBefore - healthAfter;
                if ((events & SimEvents.PlayerDamaged) != 0) result.PlayerDamageEvents += 1;
                if ((events & SimEvents.HazardPulse) != 0) result.HazardPulseEvents += 1;
                if (healthAfter < result.MinHealth) result.MinHealth = healthAfter;
                CountEvents(events, result);
                CountHazardSignals(sim, ref result);

                if ((events & SimEvents.BossSpawned) != 0 && result.BossSpawnTick < 0)
                {
                    result.BossSpawnTick = tick;
                    bossSeen = true;
                }
                if (sim.BossPhase > result.MaxBossPhase) result.MaxBossPhase = sim.BossPhase;
                if ((events & SimEvents.BossPhase2) != 0) result.BossPhaseEvents += 1;
                if (bossSeen && !sim.BossAlive && result.BossTtkTicks < 0)
                {
                    result.BossTtkTicks = tick - result.BossSpawnTick;
                    result.BossTtkStatus = "complete";
                }

                if ((events & SimEvents.WaveStarted) != 0)
                {
                    currentWave = sim.Wave;
                    waveStartTick = tick;
                    waveRecorded = false;
                }
                if (!waveRecorded && sim.Mode == SimMode.WaveClear)
                {
                    AppendWaveTtk(result.WaveTtks, currentWave, waveStartTick, tick, "wave-clear");
                    waveRecorded = true;
                }

                HashSnapshot(sim, tick, ref trace);
                trace.CommitCheckpoint();
                result.TraceCheckpoints.Add(trace.Value);
                if (sim.StageCleared || (events & SimEvents.StageCleared) != 0
                    || string.Equals(sim.Digest.Reason, CampaignSpec.StageClearReason, StringComparison.Ordinal))
                {
                    if (!waveRecorded)
                        AppendWaveTtk(result.WaveTtks, currentWave, waveStartTick, tick, "stage-clear");
                    result.Outcome = "clear";
                    result.OutcomeTick = tick;
                    break;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    if (!waveRecorded)
                        AppendWaveTtk(result.WaveTtks, currentWave, waveStartTick, tick, "censored-defeat");
                    result.Outcome = "defeat";
                    result.OutcomeTick = tick;
                    break;
                }
                if (tick == MaxTicks)
                {
                    if (!waveRecorded)
                        AppendWaveTtk(result.WaveTtks, currentWave, waveStartTick, tick, "censored-timeout");
                    result.Outcome = "timeout";
                    result.OutcomeTick = tick;
                }
            }

            if (bossSeen && result.BossTtkTicks < 0)
                result.BossTtkStatus = result.Outcome == "timeout" ? "censored-timeout" : "censored-defeat";
            RunDigest digest = sim.Digest;
            result.StoppedAtTick = result.OutcomeTick;
            result.RelicsEnd = digest.Relics;
            result.RelicDelta = result.RelicsEnd - result.RelicsStart;
            result.Score = digest.Score;
            result.Wave = digest.Wave;
            result.Kills = digest.Kills;
            result.HealthRemaining = digest.HealthRemaining;
            result.Reason = digest.Reason ?? string.Empty;
            result.FinalDigest = DigestString(in digest);
            result.TraceDigest = trace.FinishHex();
            result.InputDigest = inputTrace.FinishHex();
            result.WaveTtkTicks = result.WaveTtks.ToString();
            result.EventCountSummary = EventCountsString(result.EventCounts);
            return result;
        }

        static SimInput PolicyInput(ArchetypePlan archetype, in InputSchedule schedule, CinderSim sim, int tick)
        {
            float dx;
            float dy;
            float distance;
            float routeX;
            float routeY;
            NearestEnemy(sim, out dx, out dy, out distance);
            ScheduledRoute(in schedule, tick, out routeX, out routeY);
            var input = default(SimInput);
            bool engaged = tick >= schedule.EngageTick;
            int actionTick = tick - schedule.EngageTick;

            switch (archetype.Id)
            {
                case "A1":
                    Move(ref input, dx + routeX * 0.28f, dy + routeY * 0.28f);
                    // 30..36 ticks always lands after the longest 0.42 s swing and inside
                    // the 0.9 s link window. AttackHeld remains false so charge state never
                    // suppresses the next edge; the input trace therefore exercises chains.
                    int comboPeriod = 30 + schedule.AttackPeriod % 7;
                    if (engaged && actionTick % comboPeriod == 0) input.AttackQueued = true;
                    if (engaged && actionTick % schedule.DashPeriod == 0) input.DashQueued = true;
                    if (engaged && actionTick % schedule.SkillPeriod == 0) QueueSkill(ref input, schedule.SkillOrder, tick, schedule.SkillPeriod);
                    break;

                case "A2":
                    if (distance < 135f) Move(ref input, -dx + routeX * 0.35f, -dy + routeY * 0.35f);
                    else if (distance > 235f) Move(ref input, dx + routeX * 0.35f, dy + routeY * 0.35f);
                    else Move(ref input, routeX, routeY);
                    if (engaged && actionTick % (schedule.AttackPeriod + 17) == 0) input.AttackQueued = true;
                    if (engaged && actionTick % schedule.SkillPeriod == 0) QueueSkill(ref input, schedule.SkillOrder + 1, tick, schedule.SkillPeriod);
                    if (engaged && distance < 105f && actionTick % schedule.DashPeriod == 0) input.DashQueued = true;
                    break;

                case "A3":
                    if (distance < 105f) Move(ref input, -dx + routeX * 0.2f, -dy + routeY * 0.2f);
                    else Move(ref input, routeX * 0.45f, routeY * 0.45f);
                    if (engaged && actionTick % (schedule.AttackPeriod + 31) == 0) input.AttackQueued = true;
                    if (engaged && actionTick % schedule.SkillPeriod == 0) input.WardQueued = true;
                    if (engaged && distance < 85f && actionTick % (schedule.DashPeriod + 29) == 0) input.DashQueued = true;
                    break;

                case "A4":
                    if (distance > 175f) Move(ref input, dx + routeX * 0.25f, dy + routeY * 0.25f);
                    else if (distance < 115f) Move(ref input, -dx + routeX * 0.25f, -dy + routeY * 0.25f);
                    else Move(ref input, routeX, routeY);
                    if (engaged && actionTick % schedule.SkillPeriod == 0) QueueSkill(ref input, schedule.SkillOrder + 2, tick, schedule.SkillPeriod);
                    if (engaged && actionTick % (schedule.AttackPeriod * 7) == 0) input.AttackQueued = true;
                    break;

                case "A5":
                    if (distance > 190f) Move(ref input, dx + routeX * 0.3f, dy + routeY * 0.3f);
                    else Move(ref input, routeX, routeY);
                    if (engaged && actionTick % schedule.CommandPeriod == 0) input.CompanionSkillQueued = true;
                    if (engaged && actionTick % (schedule.CommandPeriod * 2) == schedule.CommandPeriod) input.CompanionHoldQueued = true;
                    if (engaged && actionTick % (schedule.CommandPeriod * 2) == 0) input.CompanionRecallQueued = true;
                    if (engaged && actionTick % (schedule.AttackPeriod + 41) == 0) input.AttackQueued = true;
                    break;

                case "A6":
                    // One action-decision boundary every >=30 ticks; held movement changes only
                    // at the schedule's declared route boundary.
                    int decisionPeriod = Math.Max(30, schedule.AttackPeriod + 19);
                    Move(ref input, routeX, routeY);
                    if (engaged && actionTick % decisionPeriod == 0)
                    {
                        int decision = tick / decisionPeriod;
                        if ((decision & 3) == 3) input.WardQueued = true;
                        else if ((decision & 3) == 2) input.DashQueued = true;
                        else input.AttackQueued = true;
                    }
                    break;

                default: // A7 growth optimizer
                    if (distance > 155f) Move(ref input, dx + routeX * 0.3f, dy + routeY * 0.3f);
                    else Move(ref input, routeX, routeY);
                    if (engaged && actionTick % schedule.AttackPeriod == 0) input.AttackQueued = true;
                    if (engaged && actionTick % schedule.SkillPeriod == 0) QueueSkill(ref input, schedule.SkillOrder + 3, tick, schedule.SkillPeriod);
                    if (sim.GrowthOfferOpen) input.GrowthChoice = schedule.GrowthChoice;
                    break;
            }
            return input;
        }

        static void ScheduledRoute(in InputSchedule schedule, int tick, out float routeX, out float routeY)
        {
            int quarter = (tick / schedule.RoutePeriod) & 3;
            if (quarter == 0)
            {
                routeX = schedule.RouteX;
                routeY = schedule.RouteY;
            }
            else if (quarter == 1)
            {
                routeX = -schedule.RouteY;
                routeY = schedule.RouteX;
            }
            else if (quarter == 2)
            {
                routeX = -schedule.RouteX;
                routeY = -schedule.RouteY;
            }
            else
            {
                routeX = schedule.RouteY;
                routeY = -schedule.RouteX;
            }
        }

        static void QueueSkill(ref SimInput input, int orderOffset, int tick, int period)
        {
            int slot = (tick / Math.Max(1, period) + orderOffset) & 3;
            if (slot == 0) input.BoltQueued = true;
            else if (slot == 1) input.PulseQueued = true;
            else if (slot == 2) input.NovaQueued = true;
            else input.WardQueued = true;
        }

        static void NearestEnemy(CinderSim sim, out float dx, out float dy, out float distance)
        {
            float px = sim.Player.X;
            float py = sim.Player.Y;
            float best = float.MaxValue;
            dx = 0f;
            dy = 0f;
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead) continue;
                float ex = enemy.X - px;
                float ey = enemy.Y - py;
                float isoY = ey * SimConfig.IsoY;
                float d2 = ex * ex + isoY * isoY;
                if (d2 >= best) continue;
                best = d2;
                dx = ex;
                dy = ey;
            }
            distance = best == float.MaxValue ? float.MaxValue : MathF.Sqrt(best);
        }

        static void Move(ref SimInput input, float x, float y)
        {
            float length = MathF.Sqrt(x * x + y * y);
            if (length <= 0.0001f) return;
            input.MoveX = x / length;
            input.MoveY = y / length;
        }

        static void CountHazardSignals(CinderSim sim, ref RunResult result)
        {
            bool activeOverlap = false;
            bool telegraphOverlap = false;
            IReadOnlyList<HazardState> hazards = sim.Hazards;
            float px = sim.Player.X;
            float py = sim.Player.Y;
            for (int index = 0; index < hazards.Count; index += 1)
            {
                HazardState hazard = hazards[index];
                bool overlaps;
                if (hazard.Kind == HazardKind.TideCurrent)
                {
                    overlaps = MathF.Abs(px - hazard.X) <= CampaignSpec.CurrentHalfW
                        && MathF.Abs(py - hazard.Y) <= CampaignSpec.CurrentHalfH;
                }
                else if (hazard.Kind == HazardKind.AshWall)
                {
                    overlaps = hazard.Active
                        && ((hazard.FrontX >= SimConfig.ArenaX && px <= hazard.FrontX)
                            || (hazard.FrontX < SimConfig.ArenaX && px >= hazard.FrontX));
                }
                else
                {
                    float dx = px - hazard.X;
                    float dy = (py - hazard.Y) * SimConfig.IsoY;
                    overlaps = dx * dx + dy * dy <= hazard.Radius * hazard.Radius;
                }
                if (!overlaps) continue;
                if (hazard.Active) activeOverlap = true;
                if (hazard.Telegraphing) telegraphOverlap = true;
            }
            if (activeOverlap) result.HazardActiveOverlapTicks += 1;
            if (telegraphOverlap) result.HazardTelegraphOverlapTicks += 1;
        }
        static void CountEvents(SimEvents events, RunResult result)
        {
            int mask = (int)events;
            if (mask != 0) result.EventTicks += 1;
            result.EventMaskOr |= mask;
            for (int bit = 0; bit < EventBitCount; bit += 1)
            {
                if ((mask & (1 << bit)) != 0) result.EventCounts[bit] += 1;
            }
        }

        static string EventCountsString(int[] counts)
        {
            var builder = new StringBuilder(192);
            for (int bit = 0; bit < counts.Length; bit += 1)
            {
                if (counts[bit] == 0) continue;
                if (builder.Length > 0) builder.Append(';');
                string name = Enum.GetName(typeof(SimEvents), (SimEvents)(1 << bit));
                builder.Append(name ?? ("bit" + bit.ToString(Invariant)))
                    .Append(':').Append(counts[bit].ToString(Invariant));
            }
            return builder.ToString();
        }

        static void HashSnapshot(CinderSim sim, int tick, ref TraceHash hash)
        {
            hash.AddInt(tick);
            hash.AddInt((int)sim.Mode);
            hash.AddInt(sim.Wave);
            hash.AddInt(sim.Score);
            hash.AddInt(sim.Kills);
            hash.AddInt(sim.Relics);
            hash.AddFloat(sim.Charge);
            hash.AddFloat(sim.ChargeProgress);
            hash.AddFloat(sim.NovaCooldown);
            hash.AddFloat(sim.WardCooldown);
            hash.AddFloat(sim.NovaFlash);
            hash.AddInt(sim.PendingSpawns);
            hash.AddInt(sim.LivingEnemies);
            HashPlayer(sim.Player, ref hash);
            hash.AddInt((int)sim.Events);
            hash.AddFloat(sim.NovaX);
            hash.AddFloat(sim.NovaY);

            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            hash.AddInt(enemies.Count);
            for (int index = 0; index < enemies.Count; index += 1) HashEnemy(enemies[index], ref hash);
            IReadOnlyList<PickupState> pickups = sim.Pickups;
            hash.AddInt(pickups.Count);
            for (int index = 0; index < pickups.Count; index += 1) HashPickup(pickups[index], ref hash);

            hash.AddString(sim.StageId);
            hash.AddBool(sim.BossAlive);
            hash.AddBool(sim.StageCleared);
            IReadOnlyList<HazardState> hazards = sim.Hazards;
            hash.AddInt(hazards.Count);
            for (int index = 0; index < hazards.Count; index += 1) HashHazard(hazards[index], ref hash);
            hash.AddInt(sim.WeaponRank);
            hash.AddInt(sim.LanternRank);
            hash.AddInt(sim.CloakRank);

            hash.AddInt((int)sim.HackMode);
            hash.AddInt((int)sim.RunDifficulty);
            hash.AddInt(sim.Level);
            hash.AddInt(sim.Xp);
            hash.AddInt(sim.XpNext);
            hash.AddInt(sim.ComboIndex);
            hash.AddFloat(sim.DashCooldown);
            hash.AddInt(sim.SkillCooldowns.Count);
            for (int index = 0; index < sim.SkillCooldowns.Count; index += 1) hash.AddFloat(sim.SkillCooldowns[index]);
            hash.AddFloat(sim.Shield);
            hash.AddInt(sim.ElitesAlive);
            hash.AddFloat(sim.ExtractionProgress);
            hash.AddFloat(sim.ExtractionTarget);
            hash.AddInt((int)sim.CompanionBehavior);
            hash.AddInt(sim.CompanionCount);
            for (int slot = 0; slot < sim.CompanionCount; slot += 1)
            {
                hash.AddFloat(sim.CompanionXAt(slot));
                hash.AddFloat(sim.CompanionYAt(slot));
                hash.AddBool(sim.CompanionAttackingAt(slot));
                hash.AddInt((int)sim.CompanionBehaviorAt(slot));
                hash.AddInt(sim.CompanionFacingAt(slot));
                hash.AddBool(sim.CompanionEngagedAt(slot));
                hash.AddInt(sim.CompanionTargetIdAt(slot));
                hash.AddInt((int)sim.CompanionSkillIdAt(slot));
                hash.AddFloat(sim.CompanionSkillCooldownAt(slot));
                hash.AddBool(sim.CompanionSkillCastingAt(slot));
            }
            hash.AddFloat(sim.BossHp);
            hash.AddFloat(sim.BossMaxHp);
            hash.AddInt(sim.BossPhase);
            hash.AddInt(sim.RosterMask);
            hash.AddFloat(sim.Momentum);
            hash.AddInt(sim.MomentumTier);
            hash.AddFloat(sim.MomentumDamageMultiplier);

            hash.AddBool(sim.GrowthOfferOpen);
            hash.AddFloat(sim.GrowthOfferTime);
            hash.AddInt((int)sim.LastGrowthChoice);
            hash.AddInt(sim.GrowthAttack);
            hash.AddInt(sim.GrowthVitality);
            hash.AddInt(sim.GrowthSwiftness);

            hash.AddFloat(sim.PlayerDamage);
            hash.AddFloat(sim.PlayerMaxHealth);
            hash.AddFloat(sim.PlayerSpeed);
            hash.AddFloat(sim.LanternRegenPerSecond);
            hash.AddFloat(sim.ExtractionBonus);
            hash.AddFloat(sim.BaseDamage);
            hash.AddFloat(sim.BaseMaxHealth);
            hash.AddFloat(sim.BaseSpeed);
            hash.AddFloat(sim.BaseLanternRegen);
            hash.AddInt(sim.MetaAttack);
            hash.AddInt(sim.MetaVitality);
            hash.AddInt(sim.MetaSwiftness);

            hash.AddBool(sim.AdaptiveWavesActive);
            hash.AddBool(sim.GradedLootActive);
            hash.AddInt(sim.DifficultyBand);
            hash.AddInt(sim.WaveBudget);
            hash.AddInt(sim.WaveEliteAllowance);
            hash.AddInt(sim.WaveHitsTaken);
            hash.AddFloat(sim.WaveElapsedSeconds);
            hash.AddInt(sim.FinePity);
            hash.AddInt(sim.EpicPity);
            hash.AddInt((int)sim.LastLootGrade);
            hash.AddInt(sim.PickupGrades.Count);
            for (int index = 0; index < sim.PickupGrades.Count; index += 1) hash.AddInt((int)sim.PickupGrades[index]);
            hash.AddFloat(sim.BoundsHalfWidth);
            hash.AddFloat(sim.BoundsHalfHeight);
            hash.AddBool(sim.ExpandedBoundsActive);
            hash.AddBool(sim.BossVarietyActive);
            hash.AddInt((int)sim.BossArchetype);
            hash.AddInt(sim.BossPhaseCount);
            hash.AddFloat(sim.BossTelegraphSeconds);

            hash.AddInt(sim.EmberRestRoomIndex);
            hash.AddBool(sim.EmberRestOpen);
            hash.AddInt(sim.EmberRestSeed);
            HashOffer(sim.EmberRestOffer0, ref hash);
            HashOffer(sim.EmberRestOffer1, ref hash);
            HashOffer(sim.EmberRestOffer2, ref hash);
            HashOffer(sim.SelectedPreparation, ref hash);
            HashOffer(sim.AppliedPreparationInput, ref hash);
            hash.AddInt(sim.CompanionFacing);
            hash.AddFloat(sim.PerilRemaining);
            hash.AddFloat(sim.SurgeRemaining);
            hash.AddInt(sim.PerilUsed);
            hash.AddFloat(sim.TrainingElapsed);
            hash.AddInt(sim.TrainingHits);

            RunDigest digest = sim.Digest;
            hash.AddInt(digest.Score);
            hash.AddInt(digest.Wave);
            hash.AddInt(digest.Kills);
            hash.AddInt(digest.Relics);
            hash.AddFloat(digest.HealthRemaining);
            hash.AddString(digest.Reason);
        }

        static void HashInput(in SimInput input, int tick, ref TraceHash hash)
        {
            hash.AddInt(tick);
            hash.AddFloat(input.MoveX);
            hash.AddFloat(input.MoveY);
            hash.AddBool(input.AttackQueued);
            hash.AddBool(input.NovaQueued);
            hash.AddBool(input.WardQueued);
            hash.AddBool(input.RestartQueued);
            hash.AddBool(input.DashQueued);
            hash.AddBool(input.BoltQueued);
            hash.AddBool(input.PulseQueued);
            hash.AddBool(input.CompanionHoldQueued);
            hash.AddBool(input.CompanionRecallQueued);
            hash.AddBool(input.CompanionSkillQueued);
            hash.AddBool(input.AttackHeld);
            hash.AddInt(input.GrowthChoice);
        }

        static void HashPlayer(in PlayerState state, ref TraceHash hash)
        {
            hash.AddFloat(state.X); hash.AddFloat(state.Y); hash.AddInt(state.Facing);
            hash.AddFloat(state.Health); hash.AddFloat(state.AttackCooldown); hash.AddFloat(state.DamageCooldown);
            hash.AddFloat(state.WardTime); hash.AddBool(state.Moving); hash.AddInt((int)state.Action);
            hash.AddFloat(state.ActionTime); hash.AddInt(state.AttackId);
        }

        static void HashEnemy(in EnemyState state, ref TraceHash hash)
        {
            hash.AddInt(state.Id); hash.AddInt((int)state.Visual); hash.AddFloat(state.X); hash.AddFloat(state.Y);
            hash.AddInt(state.Facing); hash.AddFloat(state.Health); hash.AddFloat(state.MaxHealth); hash.AddBool(state.Dead);
            hash.AddFloat(state.FadeTime); hash.AddInt((int)state.Action); hash.AddFloat(state.ActionTime);
            hash.AddBool(state.IsBoss); hash.AddFloat(state.Scale);
        }

        static void HashPickup(in PickupState state, ref TraceHash hash)
        {
            hash.AddInt(state.Id); hash.AddInt((int)state.Kind); hash.AddFloat(state.X); hash.AddFloat(state.Y);
            hash.AddFloat(state.Life); hash.AddFloat(state.Bob);
        }

        static void HashHazard(in HazardState state, ref TraceHash hash)
        {
            hash.AddInt((int)state.Kind); hash.AddFloat(state.X); hash.AddFloat(state.Y); hash.AddFloat(state.Radius);
            hash.AddFloat(state.CycleT); hash.AddBool(state.Telegraphing); hash.AddFloat(state.CooldownT);
            hash.AddBool(state.Active); hash.AddFloat(state.FrontX); hash.AddFloat(state.Hp);
        }

        static void HashOffer(in PreparationOffer offer, ref TraceHash hash)
        {
            hash.AddInt((int)offer.Kind); hash.AddInt(offer.Variant); hash.AddInt(offer.Magnitude);
        }

        static void WriteRawHeader(StreamWriter writer)
        {
            writer.WriteLine("schema_version,execution_id,measured_at_utc,run_id,candidate_id,build_id,actor_id,execution_kind,policy_id,archetype_id,archetype,evaluation_scope,stage,sim_anchor,difficulty,loadout_id,loadout_kind,meta_attack,meta_vitality,meta_swiftness,weapon_tier,lantern_tier,cloak_tier,sigil_slot0,sigil_face0,sigil_slot1,sigil_face1,companions,roster_mask,pact,preparation_kind,preparation_variant,preparation_magnitude,progression,fixed_step_seconds,hazard_fingerprint,input_script_id,input_schedule,input_schedule_fingerprint,input_digest,row_kind,replay_index,counts_toward_n,replay_of,outcome,outcome_tick,stopped_at_tick,wave_ttk_trace,boss_spawn_tick,boss_ttk_ticks,boss_ttk_status,boss_phase_events,max_boss_phase,player_damage_total,player_damage_events,damage_source,hazard_damage_share,pair_ev,hazard_pulse_events,hazard_active_overlap_ticks,hazard_telegraph_overlap_ticks,relic_start,relic_end,relic_delta,score,wave,kills,health_remaining,reason,final_digest,event_ticks,event_mask_or,event_counts,snapshot_count,trace_algorithm,trace_digest,deterministic_equal,first_diff_tick,loop_reentry,g2_claim_scope,protocol_warnings");
        }

        static void WriteRawRow(
            StreamWriter writer,
            string executionId,
            string measuredAtUtc,
            string candidateId,
            string buildId,
            in StageEntry stage,
            Difficulty difficulty,
            ArchetypePlan archetype,
            in LoadoutPlan loadout,
            in InputSchedule schedule,
            RunResult result,
            string rowKind,
            int replayIndex,
            bool countsTowardN,
            string replayOf,
            string deterministicEqual,
            string firstDiffTick)
        {
            bool first = true;
            Csv(writer, SchemaVersion, ref first); Csv(writer, executionId, ref first);
            Csv(writer, measuredAtUtc, ref first); Csv(writer, RunId, ref first);
            Csv(writer, candidateId, ref first); Csv(writer, buildId, ref first);
            Csv(writer, archetype.ActorId, ref first); Csv(writer, ExecutionKind, ref first);
            Csv(writer, archetype.PolicyId, ref first); Csv(writer, archetype.Id, ref first);
            Csv(writer, archetype.Name, ref first); Csv(writer, EvaluationScope(archetype, loadout), ref first);
            Csv(writer, stage.Id, ref first); Csv(writer, stage.SimAnchorId, ref first);
            Csv(writer, DifficultySpec.IdOf(difficulty), ref first);
            Csv(writer, loadout.Id, ref first); Csv(writer, loadout.Kind, ref first);
            Csv(writer, loadout.Meta.Attack.ToString(Invariant), ref first);
            Csv(writer, loadout.Meta.Vitality.ToString(Invariant), ref first);
            Csv(writer, loadout.Meta.Swiftness.ToString(Invariant), ref first);
            Csv(writer, loadout.Equip.Weapon.ToString(Invariant), ref first);
            Csv(writer, loadout.Equip.Lantern.ToString(Invariant), ref first);
            Csv(writer, loadout.Equip.Cloak.ToString(Invariant), ref first);
            Csv(writer, loadout.Sigils.Slot0.ToString(), ref first); Csv(writer, loadout.Sigils.Face0.ToString(), ref first);
            Csv(writer, loadout.Sigils.Slot1.ToString(), ref first); Csv(writer, loadout.Sigils.Face1.ToString(), ref first);
            Csv(writer, JoinCompanions(loadout.Companions), ref first);
            Csv(writer, loadout.RosterMask.ToString(Invariant), ref first);
            Csv(writer, "false", ref first); Csv(writer, PreparationOfferKind.None.ToString(), ref first);
            Csv(writer, "0", ref first); Csv(writer, "0", ref first);
            Csv(writer, ProgressionDescription(), ref first); Csv(writer, R(SimConfig.FixedStep), ref first);
            Csv(writer, result.HazardFingerprint, ref first);
            Csv(writer, schedule.Id, ref first); Csv(writer, schedule.Description, ref first);
            Csv(writer, schedule.Fingerprint, ref first); Csv(writer, result.InputDigest, ref first);
            Csv(writer, rowKind, ref first); Csv(writer, replayIndex.ToString(Invariant), ref first);
            Csv(writer, countsTowardN ? "true" : "false", ref first); Csv(writer, replayOf, ref first);
            Csv(writer, result.Outcome, ref first); Csv(writer, result.OutcomeTick.ToString(Invariant), ref first);
            Csv(writer, result.StoppedAtTick.ToString(Invariant), ref first); Csv(writer, result.WaveTtkTicks, ref first);
            Csv(writer, result.BossSpawnTick.ToString(Invariant), ref first); Csv(writer, result.BossTtkTicks.ToString(Invariant), ref first);
            Csv(writer, result.BossTtkStatus, ref first);
            Csv(writer, result.BossPhaseEvents.ToString(Invariant), ref first); Csv(writer, result.MaxBossPhase.ToString(Invariant), ref first);
            Csv(writer, R(result.PlayerDamageTotal), ref first); Csv(writer, result.PlayerDamageEvents.ToString(Invariant), ref first);
            Csv(writer, "UNKNOWN-unattributed", ref first); Csv(writer, "UNKNOWN", ref first); Csv(writer, "UNKNOWN", ref first);
            Csv(writer, result.HazardPulseEvents.ToString(Invariant), ref first);
            Csv(writer, result.HazardActiveOverlapTicks.ToString(Invariant), ref first);
            Csv(writer, result.HazardTelegraphOverlapTicks.ToString(Invariant), ref first);
            Csv(writer, result.RelicsStart.ToString(Invariant), ref first); Csv(writer, result.RelicsEnd.ToString(Invariant), ref first);
            Csv(writer, result.RelicDelta.ToString(Invariant), ref first); Csv(writer, result.Score.ToString(Invariant), ref first);
            Csv(writer, result.Wave.ToString(Invariant), ref first); Csv(writer, result.Kills.ToString(Invariant), ref first);
            Csv(writer, R(result.HealthRemaining), ref first); Csv(writer, result.Reason, ref first);
            Csv(writer, result.FinalDigest, ref first); Csv(writer, result.EventTicks.ToString(Invariant), ref first);
            Csv(writer, result.EventMaskOr.ToString("x8", Invariant), ref first); Csv(writer, result.EventCountSummary, ref first);
            Csv(writer, result.TraceCheckpoints.Count.ToString(Invariant), ref first); Csv(writer, "SHA-256", ref first);
            Csv(writer, result.TraceDigest, ref first); Csv(writer, deterministicEqual, ref first);
            Csv(writer, firstDiffTick, ref first); Csv(writer, "UNKNOWN-sim-only", ref first);
            Csv(writer, "clear-rate;censored-TTK;G3-declared-build;determinism (damage-source/hazard-share/pair-EV UNKNOWN-FIX)", ref first);
            Csv(writer, ProtocolWarnings(archetype), ref first);
            writer.WriteLine();
        }

        static void WriteSummaryPreamble(
            StreamWriter writer,
            string executionId,
            string startedUtc,
            string candidateId,
            string buildId,
            string sourceIdentity,
            string gitSha,
            bool gitDirty,
            string rawPath,
            string summaryPath,
            int expectedCells,
            int expectedBaseRows)
        {
            writer.Write("{\n  \"schema_version\": "); Json(writer, SchemaVersion);
            writer.Write(",\n  \"execution_id\": "); Json(writer, executionId);
            writer.Write(",\n  \"execution_started_utc\": "); Json(writer, startedUtc);
            writer.Write(",\n  \"command\": "); Json(writer, Invocation);
            writer.Write(",\n  \"run_id\": "); Json(writer, RunId);
            writer.Write(",\n  \"candidate_id\": "); Json(writer, candidateId);
            writer.Write(",\n  \"build_id\": "); Json(writer, buildId);
            writer.Write(",\n  \"actor_id\": \"cycle9-combat-evidence-recorder\"");
            writer.Write(",\n  \"execution_kind\": "); Json(writer, ExecutionKind);
            writer.Write(",\n  \"source_sha256\": "); Json(writer, sourceIdentity);
            writer.Write(",\n  \"git_sha\": "); Json(writer, gitSha);
            writer.Write(",\n  \"git_dirty\": " + (gitDirty ? "true" : "false"));
            writer.Write(",\n  \"unity_version\": "); Json(writer, Application.unityVersion);
            writer.Write(",\n  \"raw_path\": "); Json(writer, rawPath);
            writer.Write(",\n  \"summary_path\": "); Json(writer, summaryPath);
            writer.Write(",\n  \"runtime_authority\": \"Unity batch/EditMode; CinderSim + GameView.DungeonProgression\"");
            writer.Write(",\n  \"gate_verdict\": \"not_evaluated\"");
            writer.Write(",\n  \"packet_scope\": [\"clear-rate\",\"censored wave/boss TTK\",\"G3 declared-build sensitivity\",\"determinism\"]");
            writer.Write(",\n  \"g3_viability_scope\": \"declared archetype build cells only; reference loadouts are sensitivity-only; A7 full stage/spend-order strategy is UNKNOWN/FIX\"");
            writer.Write(",\n  \"g2_subclaims\": {\"clear_rate\":\"measured\",\"ttk\":\"measured_with_censoring\",\"damage_source_hazard_share\":\"UNKNOWN/FIX\",\"pair_ev\":\"UNKNOWN/FIX\"}");
            writer.Write(",\n  \"no_hidden_rng\": true");
            writer.Write(",\n  \"pact\": false");
            writer.Write(",\n  \"preparation\": \"None\"");
            writer.Write(",\n  \"progression\": "); Json(writer, ProgressionDescription());
            writer.Write(",\n  \"fixed_step_seconds\": "); Json(writer, R(SimConfig.FixedStep));
            writer.Write(",\n  \"max_ticks\": " + MaxTicks.ToString(Invariant));
            writer.Write(",\n  \"max_seconds\": 300");
            writer.Write(",\n  \"base_n_per_cell\": " + SchedulesPerCell.ToString(Invariant));
            writer.Write(",\n  \"determinism_trace\": \"SHA-256 over cumulative canonical FNV-1a checkpoints for every published snapshot/event; per-tick checkpoints identify first divergence\"");
            writer.Write(",\n  \"determinism_replays_per_selected_schedule\": " + DeterminismReplayCount.ToString(Invariant));
            writer.Write(",\n  \"determinism_selection_rule\": \"outcome != clear OR cell clear count is 9 or 11 (45%/55% band edge); replays never count toward N\"");
            writer.Write(",\n  \"expected_cells\": " + expectedCells.ToString(Invariant));
            writer.Write(",\n  \"expected_base_rows\": " + expectedBaseRows.ToString(Invariant));
            writer.Write(",\n  \"protocol_warnings\": ["); Json(writer, DamageSourceWarning); writer.Write(", ");
            Json(writer, PairEvWarning); writer.Write(", "); Json(writer, LoopReentryWarning); writer.Write(", ");
            Json(writer, A7ScopeWarning); writer.Write("]");
            writer.Write(",\n  \"unobservable_fields\": {\n    \"damage_source\": \"CinderSim publishes health delta and PlayerDamaged, but no causal source ledger\",\n    \"hazard_damage_share\": \"UNKNOWN/FIX without causal damage source\",\n    \"pair_ev\": \"UNKNOWN/FIX without per-source enemy damage and risk ledger\",\n    \"loop_reentry\": \"requires View/persistence lifecycle outside one headless simulation\",\n    \"A7_stage_spend_order\": \"requires persistent campaign/economy routing outside isolated combat\"\n  }");
            WriteScheduleSummary(writer);
            WriteBuildSummary(writer);
            writer.Write(",\n  \"cells\": [\n");
        }

        static void WriteScheduleSummary(StreamWriter writer)
        {
            writer.Write(",\n  \"input_schedules\": [\n");
            for (int index = 0; index < Schedules.Length; index += 1)
            {
                if (index > 0) writer.Write(",\n");
                InputSchedule schedule = Schedules[index];
                writer.Write("    {\"id\":"); Json(writer, schedule.Id);
                writer.Write(",\"definition\":"); Json(writer, schedule.Description);
                writer.Write(",\"fingerprint\":"); Json(writer, schedule.Fingerprint);
                writer.Write("}");
            }
            writer.Write("\n  ]");
        }

        static void WriteBuildSummary(StreamWriter writer)
        {
            writer.Write(",\n  \"declared_archetype_builds\": [\n");
            for (int index = 0; index < Archetypes.Length; index += 1)
            {
                if (index > 0) writer.Write(",\n");
                ArchetypePlan archetype = Archetypes[index];
                LoadoutPlan build = archetype.DeclaredBuild;
                writer.Write("    {\"archetype_id\":"); Json(writer, archetype.Id);
                writer.Write(",\"archetype\":"); Json(writer, archetype.Name);
                writer.Write(",\"loadout_id\":"); Json(writer, build.Id);
                writer.Write(",\"meta\":[" + build.Meta.Attack.ToString(Invariant) + "," + build.Meta.Vitality.ToString(Invariant) + "," + build.Meta.Swiftness.ToString(Invariant) + "]");
                writer.Write(",\"equipment\":[" + build.Equip.Weapon.ToString(Invariant) + "," + build.Equip.Lantern.ToString(Invariant) + "," + build.Equip.Cloak.ToString(Invariant) + "]");
                writer.Write(",\"sigils\":"); Json(writer, SigilDescription(build.Sigils));
                writer.Write(",\"companions\":"); Json(writer, JoinCompanions(build.Companions));
                writer.Write(",\"roster_mask\":" + build.RosterMask.ToString(Invariant));
                writer.Write("}");
            }
            writer.Write("\n  ]");
        }

        static void WriteCellSummary(
            StreamWriter writer,
            ref bool firstCell,
            ArchetypePlan archetype,
            in StageEntry stage,
            Difficulty difficulty,
            in LoadoutPlan loadout,
            int clears,
            int defeats,
            int timeouts,
            int uniqueInputDigests,
            bool bandEdge,
            int replayCandidates,
            int replayRows,
            int replayMismatches)
        {
            if (!firstCell) writer.Write(",\n");
            firstCell = false;
            writer.Write("    {\"archetype_id\":"); Json(writer, archetype.Id);
            writer.Write(",\"archetype\":"); Json(writer, archetype.Name);
            writer.Write(",\"policy_id\":"); Json(writer, archetype.PolicyId);
            writer.Write(",\"actor_id\":"); Json(writer, archetype.ActorId);
            writer.Write(",\"evaluation_scope\":"); Json(writer, EvaluationScope(archetype, loadout));
            writer.Write(",\"g3_viability_eligible\":" + (loadout.Kind == "archetype" && archetype.Id != "A7" ? "true" : "false"));
            writer.Write(",\"stage\":"); Json(writer, stage.Id);
            writer.Write(",\"difficulty\":"); Json(writer, DifficultySpec.IdOf(difficulty));
            writer.Write(",\"loadout_id\":"); Json(writer, loadout.Id);
            writer.Write(",\"n\":20,\"unique_input_digests\":" + uniqueInputDigests.ToString(Invariant));
            writer.Write(",\"clears\":" + clears.ToString(Invariant));
            writer.Write(",\"defeats\":" + defeats.ToString(Invariant));
            writer.Write(",\"timeouts\":" + timeouts.ToString(Invariant));
            writer.Write(",\"clear_rate\":" + (clears / 20.0).ToString("R", Invariant));
            writer.Write(",\"band_edge_cell\":" + (bandEdge ? "true" : "false"));
            writer.Write(",\"replay_candidates\":" + replayCandidates.ToString(Invariant));
            writer.Write(",\"replay_rows\":" + replayRows.ToString(Invariant));
            writer.Write(",\"replay_mismatches\":" + replayMismatches.ToString(Invariant));
            writer.Write("}");
        }

        static void WriteSummaryTrailer(
            StreamWriter writer,
            string endedUtc,
            int cellsWritten,
            long baseRows,
            long replayCandidates,
            long replayRows,
            long replayMismatches,
            int expectedCells,
            int expectedBaseRows)
        {
            writer.Write("\n  ],\n  \"execution_ended_utc\": "); Json(writer, endedUtc);
            writer.Write(",\n  \"actual_cells\": " + cellsWritten.ToString(Invariant));
            writer.Write(",\n  \"actual_base_rows\": " + baseRows.ToString(Invariant));
            writer.Write(",\n  \"actual_replay_candidates\": " + replayCandidates.ToString(Invariant));
            writer.Write(",\n  \"actual_replay_rows\": " + replayRows.ToString(Invariant));
            writer.Write(",\n  \"actual_raw_rows\": " + (baseRows + replayRows).ToString(Invariant));
            writer.Write(",\n  \"replay_mismatches\": " + replayMismatches.ToString(Invariant));
            writer.Write(",\n  \"base_row_count_exact\": " + (baseRows == expectedBaseRows ? "true" : "false"));
            writer.Write(",\n  \"cell_count_exact\": " + (cellsWritten == expectedCells ? "true" : "false"));
            writer.Write("\n}\n");
        }

        static LoadoutPlan[] LoadoutsFor(ArchetypePlan archetype)
        {
            return new[]
            {
                Build("fresh-0-0-0", "reference", default, EquipTiers.Of(0, 0, 0), default),
                Build("contested-2-1-3", "reference", default, EquipTiers.Of(2, 1, 3), default),
                Build("capped-5-5-5", "reference", default, EquipTiers.Of(5, 5, 5), default),
                archetype.DeclaredBuild,
            };
        }

        static LoadoutPlan Build(
            string id,
            string kind,
            MetaStats meta,
            EquipTiers equip,
            SigilLoadout sigils,
            string[] companions = null,
            int rosterMask = 0)
        {
            return new LoadoutPlan(id, kind, meta, equip, sigils,
                companions ?? Array.Empty<string>(), rosterMask);
        }

        static void ValidateProtocolTables()
        {
            if (StageCatalog.Entries.Count != 9)
                throw new InvalidOperationException("Protocol requires exactly nine logical StageCatalog entries; observed " + StageCatalog.Entries.Count);
            if (Archetypes.Length != 7)
                throw new InvalidOperationException("Protocol requires seven archetypes; observed " + Archetypes.Length);
            if (Schedules.Length != SchedulesPerCell)
                throw new InvalidOperationException("Protocol requires twenty schedules; observed " + Schedules.Length);

            var scheduleIds = new HashSet<string>(StringComparer.Ordinal);
            var scheduleFingerprints = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Schedules.Length; index += 1)
            {
                if (!scheduleIds.Add(Schedules[index].Id))
                    throw new InvalidOperationException("Duplicate input schedule id " + Schedules[index].Id);
                if (!scheduleFingerprints.Add(Schedules[index].Fingerprint))
                    throw new InvalidOperationException("Duplicate input schedule definition " + Schedules[index].Id);
            }
        }

        static string ComputeSourceIdentity()
        {
            string[] files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            using (SHA256 sha = SHA256.Create())
            using (var sink = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                var buffer = new byte[81920];
                for (int index = 0; index < files.Length; index += 1)
                {
                    byte[] path = Utf8NoBom.GetBytes(files[index].Replace('\\', '/'));
                    sink.Write(path, 0, path.Length);
                    sink.WriteByte(0);
                    using (var input = new FileStream(files[index], FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) sink.Write(buffer, 0, read);
                    }
                    sink.WriteByte(0xff);
                }
                HashOptionalFile("Packages/manifest.json", sink, buffer);
                HashOptionalFile("ProjectSettings/ProjectVersion.txt", sink, buffer);
                sink.FlushFinalBlock();
                return ToHex(sha.Hash, 64);
            }
        }

        static void HashOptionalFile(string path, Stream sink, byte[] buffer)
        {
            if (!File.Exists(path)) return;
            byte[] name = Utf8NoBom.GetBytes(path);
            sink.Write(name, 0, name.Length);
            sink.WriteByte(0);
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length))
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) sink.Write(buffer, 0, read);
            }
            sink.WriteByte(0xff);
        }

        static string ToHex(byte[] bytes, int characterCount)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index += 1) builder.Append(bytes[index].ToString("x2", Invariant));
            return builder.ToString(0, Math.Min(characterCount, builder.Length));
        }
        static string FileSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return ToHex(sha.ComputeHash(input), 64);
            }
        }

        static string GitSha()
        {
            string environmentSha = Environment.GetEnvironmentVariable("GIT_COMMIT");
            if (!string.IsNullOrEmpty(environmentSha)) return environmentSha;
            return RunGit("rev-parse HEAD");
        }

        static bool GitDirty()
        {
            string status = RunGit("status --porcelain --untracked-files=no");
            return !string.IsNullOrEmpty(status);
        }

        static string RunGit(string arguments)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    return process.ExitCode == 0 ? output : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        static string ProgressionDescription()
        {
            return "GameView.DungeonProgression=Everything;adaptive_waves=true;graded_loot=true";
        }

        static string EvaluationScope(ArchetypePlan archetype, in LoadoutPlan loadout)
        {
            if (loadout.Kind != "archetype") return "reference-loadout-sensitivity-only";
            return archetype.Id == "A7"
                ? "declared-build-combat-growth-choice-sensitivity-only;stage/spend-order=UNKNOWN/FIX"
                : "declared-archetype-build-G3-viability";
        }

        static string ProtocolWarnings(ArchetypePlan archetype)
        {
            string warnings = DamageSourceWarning + ";" + PairEvWarning + ";" + LoopReentryWarning;
            return archetype.Id == "A7" ? warnings + ";" + A7ScopeWarning : warnings;
        }

        static string FirstTraceDifference(IReadOnlyList<ulong> baseline, IReadOnlyList<ulong> replay)
        {
            int common = Math.Min(baseline.Count, replay.Count);
            for (int tick = 0; tick < common; tick += 1)
            {
                if (baseline[tick] != replay[tick]) return tick.ToString(Invariant);
            }
            return baseline.Count == replay.Count ? string.Empty : common.ToString(Invariant);
        }
        static void ReadGitIdentity(out string sha, out bool dirty)
        {
            sha = GitSha();
            dirty = GitDirty();
        }

        static void WriteRunMeta(
            string path,
            string executionId,
            string startedUtc,
            string endedUtc,
            string candidateId,
            string buildId,
            string sourceIdentity,
            string gitSha,
            bool gitDirty,
            string rawPath,
            string rawSha,
            string summaryPath,
            string summarySha,
            string recorderLogPath,
            string recorderLogSha,
            long baseRows,
            long replayRows)
        {
            using (var writer = NewWriter(path))
            {
                writer.Write("{\n  \"schema_version\": "); Json(writer, SchemaVersion);
                writer.Write(",\n  \"execution_id\": "); Json(writer, executionId);
                writer.Write(",\n  \"started_utc\": "); Json(writer, startedUtc);
                writer.Write(",\n  \"ended_utc\": "); Json(writer, endedUtc);
                writer.Write(",\n  \"command\": "); Json(writer, Invocation);
                writer.Write(",\n  \"run_id\": "); Json(writer, RunId);
                writer.Write(",\n  \"candidate_id\": "); Json(writer, candidateId);
                writer.Write(",\n  \"build_id\": "); Json(writer, buildId);
                writer.Write(",\n  \"actor_id\": \"cycle9-combat-evidence-recorder\"");
                writer.Write(",\n  \"execution_kind\": "); Json(writer, ExecutionKind);
                writer.Write(",\n  \"unity_version\": "); Json(writer, Application.unityVersion);
                writer.Write(",\n  \"source_sha256\": "); Json(writer, sourceIdentity);
                writer.Write(",\n  \"git_sha\": "); Json(writer, gitSha);
                writer.Write(",\n  \"git_dirty\": " + (gitDirty ? "true" : "false"));
                writer.Write(",\n  \"artifacts\": [");
                WriteArtifactMeta(writer, rawPath, rawSha, baseRows + replayRows);
                writer.Write(",");
                WriteArtifactMeta(writer, summaryPath, summarySha, 1);
                writer.Write(",");
                WriteArtifactMeta(writer, recorderLogPath, recorderLogSha, 1);
                writer.Write("]\n}\n");
            }
        }

        static void WriteArtifactMeta(StreamWriter writer, string path, string sha256, long rows)
        {
            writer.Write("\n    {\"path\":"); Json(writer, path);
            writer.Write(",\"sha256\":"); Json(writer, sha256);
            writer.Write(",\"rows\":" + rows.ToString(Invariant) + "}");
        }

        static string HazardFingerprint(HazardConfig[] hazards)
        {
            var hash = TraceHash.Create();
            int count = hazards == null ? 0 : hazards.Length;
            hash.AddInt(count);
            for (int index = 0; index < count; index += 1)
            {
                HazardConfig hazard = hazards[index];
                hash.AddInt((int)hazard.Kind);
                hash.AddFloat(hazard.X); hash.AddFloat(hazard.Y); hash.AddFloat(hazard.Radius);
                hash.AddFloat(hazard.Phase); hash.AddFloat(hazard.HalfW); hash.AddFloat(hazard.HalfH);
                hash.AddFloat(hazard.PushX); hash.AddFloat(hazard.PushY); hash.AddFloat(hazard.Hp);
            }
            return hash.Hex;
        }

        static string BaselineKey(
            ArchetypePlan archetype,
            in StageEntry stage,
            Difficulty difficulty,
            in LoadoutPlan loadout,
            in InputSchedule schedule)
        {
            return archetype.Id + "/" + stage.Id + "/" + DifficultySpec.IdOf(difficulty) + "/" + loadout.Id + "/" + schedule.Id;
        }

        static void AppendWaveTtk(StringBuilder builder, int wave, int ticks)
        {
            AppendWaveTtk(builder, wave, 0, ticks, "legacy-complete");
        }

        static void AppendWaveTtk(
            StringBuilder builder,
            int wave,
            int startTick,
            int endTick,
            string termination)
        {
            if (builder.Length > 0) builder.Append(';');
            builder.Append("wave=").Append(wave.ToString(Invariant))
                .Append(":start=").Append(startTick.ToString(Invariant))
                .Append(":end=").Append(endTick.ToString(Invariant))
                .Append(":duration=").Append((endTick - startTick).ToString(Invariant))
                .Append(":termination=").Append(termination);
        }

        static string DigestString(in RunDigest digest)
        {
            return digest.Score.ToString(Invariant) + "|" + digest.Wave.ToString(Invariant) + "|"
                + digest.Kills.ToString(Invariant) + "|" + digest.Relics.ToString(Invariant) + "|"
                + R(digest.HealthRemaining) + "|" + (digest.Reason ?? string.Empty);
        }

        static string JoinCompanions(string[] companions)
        {
            return companions == null || companions.Length == 0 ? string.Empty : string.Join(";", companions);
        }

        static string SigilDescription(in SigilLoadout sigils)
        {
            return sigils.Slot0 + ":" + sigils.Face0 + ";" + sigils.Slot1 + ":" + sigils.Face1;
        }

        static string R(float value) => value.ToString("R", Invariant);

        static StreamWriter NewWriter(string path)
        {
            return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536), Utf8NoBom, 65536);
        }

        static void Csv(StreamWriter writer, string value, ref bool first)
        {
            if (!first) writer.Write(',');
            first = false;
            value = value ?? string.Empty;
            bool quoted = value.IndexOfAny(CsvSpecials) >= 0;
            if (!quoted)
            {
                writer.Write(value);
                return;
            }
            writer.Write('"');
            for (int index = 0; index < value.Length; index += 1)
            {
                char c = value[index];
                if (c == '"') writer.Write("\"\"");
                else writer.Write(c);
            }
            writer.Write('"');
        }

        static void Json(StreamWriter writer, string value)
        {
            writer.Write('"');
            value = value ?? string.Empty;
            for (int index = 0; index < value.Length; index += 1)
            {
                char c = value[index];
                switch (c)
                {
                    case '"': writer.Write("\\\""); break;
                    case '\\': writer.Write("\\\\"); break;
                    case '\b': writer.Write("\\b"); break;
                    case '\f': writer.Write("\\f"); break;
                    case '\n': writer.Write("\\n"); break;
                    case '\r': writer.Write("\\r"); break;
                    case '\t': writer.Write("\\t"); break;
                    default:
                        if (c < 0x20) writer.Write("\\u" + ((int)c).ToString("x4", Invariant));
                        else writer.Write(c);
                        break;
                }
            }
            writer.Write('"');
        }

        sealed class ArchetypePlan
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string PolicyId;
            public readonly string ActorId;
            public readonly LoadoutPlan DeclaredBuild;

            public ArchetypePlan(string id, string name, string policyId, string actorId, LoadoutPlan declaredBuild)
            {
                Id = id;
                Name = name;
                PolicyId = policyId;
                ActorId = actorId;
                DeclaredBuild = declaredBuild;
            }
        }

        readonly struct LoadoutPlan
        {
            public readonly string Id;
            public readonly string Kind;
            public readonly MetaStats Meta;
            public readonly EquipTiers Equip;
            public readonly SigilLoadout Sigils;
            public readonly string[] Companions;
            public readonly int RosterMask;

            public LoadoutPlan(
                string id, string kind, MetaStats meta, EquipTiers equip,
                SigilLoadout sigils, string[] companions, int rosterMask)
            {
                Id = id;
                Kind = kind;
                Meta = meta;
                Equip = equip;
                Sigils = sigils;
                Companions = companions;
                RosterMask = rosterMask;
            }
        }

        readonly struct InputSchedule
        {
            public readonly string Id;
            public readonly float RouteX;
            public readonly float RouteY;
            public readonly int EngageTick;
            public readonly int AttackPeriod;
            public readonly int SkillPeriod;
            public readonly int SkillOrder;
            public readonly int DashPeriod;
            public readonly int CommandPeriod;
            public readonly int RoutePeriod;
            public readonly int GrowthChoice;
            public readonly string Description;
            public readonly string Fingerprint;

            public InputSchedule(
                string id, float routeX, float routeY, int engageTick, int attackPeriod,
                int skillPeriod, int skillOrder, int dashPeriod, int commandPeriod,
                int routePeriod, int growthChoice)
            {
                Id = id;
                RouteX = routeX;
                RouteY = routeY;
                EngageTick = engageTick;
                AttackPeriod = attackPeriod;
                SkillPeriod = skillPeriod;
                SkillOrder = skillOrder;
                DashPeriod = dashPeriod;
                CommandPeriod = commandPeriod;
                RoutePeriod = routePeriod;
                GrowthChoice = growthChoice;
                Description = "route=" + R(routeX) + ":" + R(routeY)
                    + ";engage=" + engageTick.ToString(Invariant)
                    + ";attack=" + attackPeriod.ToString(Invariant)
                    + ";skill=" + skillPeriod.ToString(Invariant) + ":" + skillOrder.ToString(Invariant)
                    + ";dash=" + dashPeriod.ToString(Invariant)
                    + ";command=" + commandPeriod.ToString(Invariant)
                    + ";route_period=" + routePeriod.ToString(Invariant)
                    + ";growth=" + growthChoice.ToString(Invariant);
                var hash = TraceHash.Create();
                hash.AddString(Description);
                Fingerprint = hash.Hex;
            }
        }

        sealed class RunResult
        {
            public string Outcome;
            public int OutcomeTick;
            public int StoppedAtTick;
            public StringBuilder WaveTtks;
            public string WaveTtkTicks;
            public int BossSpawnTick;
            public int BossTtkTicks;
            public string BossTtkStatus;
            public int BossPhaseEvents;
            public int MaxBossPhase;
            public float PlayerDamageTotal;
            public int PlayerDamageEvents;
            public float MinHealth;
            public int HazardPulseEvents;
            public int HazardActiveOverlapTicks;
            public int HazardTelegraphOverlapTicks;
            public int RelicsStart;
            public int RelicsEnd;
            public int RelicDelta;
            public int Score;
            public int Wave;
            public int Kills;
            public float HealthRemaining;
            public string Reason;
            public string FinalDigest;
            public string TraceDigest;
            public string InputDigest;
            public string HazardFingerprint;
            public int EventTicks;
            public int EventMaskOr;
            public int[] EventCounts;
            public string EventCountSummary;
            public List<ulong> TraceCheckpoints;

            public bool DeterministicallyEquals(RunResult baseline)
            {
                return string.Equals(Outcome, baseline.Outcome, StringComparison.Ordinal)
                    && OutcomeTick == baseline.OutcomeTick
                    && string.Equals(InputDigest, baseline.InputDigest, StringComparison.Ordinal)
                    && string.Equals(TraceDigest, baseline.TraceDigest, StringComparison.Ordinal)
                    && string.Equals(FinalDigest, baseline.FinalDigest, StringComparison.Ordinal);
            }

            public int FirstDifferenceTick(RunResult baseline)
            {
                string tick = Cycle9CombatEvidenceRecorder.FirstTraceDifference(
                    baseline.TraceCheckpoints, TraceCheckpoints);
                return string.IsNullOrEmpty(tick) ? -1 : int.Parse(tick, Invariant);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int;
        }

        struct TraceHash
        {
            const ulong Offset = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;
            ulong _value;
            IncrementalHash _cryptographic;
            byte[] _scratch;

            public static TraceHash Create(bool cryptographic = false)
            {
                return new TraceHash
                {
                    _value = Offset,
                    _cryptographic = cryptographic ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null,
                    _scratch = cryptographic ? new byte[8] : null,
                };
            }

            public string Hex => _value.ToString("x16", Invariant);
            public ulong Value => _value;

            public void CommitCheckpoint()
            {
                if (_cryptographic == null) return;
                for (int shift = 0; shift < 64; shift += 8)
                {
                    _scratch[shift / 8] = (byte)(_value >> shift);
                }
                _cryptographic.AppendData(_scratch, 0, 8);
            }

            public string FinalizeDigest()
            {
                if (_cryptographic == null) return Hex;
                byte[] digest = _cryptographic.GetHashAndReset();
                _cryptographic.Dispose();
                _cryptographic = null;
                return ToHex(digest, 64);
            }

            public string FinishHex()
            {
                return FinalizeDigest();
            }

            public void AddBool(bool value) => AddInt(value ? 1 : 0);

            public void AddFloat(float value)
            {
                var bits = new FloatBits { Float = value };
                AddInt(bits.Int);
            }

            public void AddInt(int value)
            {
                unchecked
                {
                    AddByte((byte)value);
                    AddByte((byte)(value >> 8));
                    AddByte((byte)(value >> 16));
                    AddByte((byte)(value >> 24));
                }
            }

            public void AddString(string value)
            {
                if (value == null)
                {
                    AddInt(-1);
                    return;
                }
                AddInt(value.Length);
                for (int index = 0; index < value.Length; index += 1)
                {
                    char c = value[index];
                    AddByte((byte)c);
                    AddByte((byte)(c >> 8));
                }
            }

            void AddByte(byte value)
            {
                unchecked
                {
                    _value ^= value;
                    _value *= Prime;
                }
                // A cryptographic trace appends the cumulative canonical FNV state once
                // per input/snapshot through CommitCheckpoint, not once per scalar byte.
            }
        }
    }
}
