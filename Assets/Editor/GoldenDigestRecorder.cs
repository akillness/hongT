using System.Globalization;
using System.IO;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    /// <summary>
    /// Records the 15 golden digest rows (R1-R3 + new stages) under the UNITY
    /// runtime — kiter bot, 1800 ticks, same row format as
    /// DungeonGoldenDigestTests. Standalone dotnet digests are NOT comparable
    /// (ARM64 FMA drift in low-order float bits); Unity goldens are the shipping
    /// truth. Usage:
    ///   UNITY_BIN=... bash tools/unity_batch.sh method CinderCourt.EditorTools.GoldenDigestRecorder.Record
    /// </summary>
    public static class GoldenDigestRecorder
    {
        const int Ticks = 1800;

        public static void Record()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Golden digest rows — Unity runtime recording");
            sb.AppendLine();

            var arena = HackConfig.Arena();
            sb.AppendLine(Run("arena-hack", new CinderSim(in arena)));
            sb.AppendLine(Run("arena-frozen", new CinderSim()));
            var prologue = HackConfig.Prologue();
            sb.AppendLine(Run("prologue", new CinderSim(in prologue)));

            foreach (var entry in StageCatalog.Entries)
            {
                if (!HackConfig.TryDungeon(entry.SimAnchorId, default, EquipTiers.Of(2, 1, 3), (string)null, 0, out var config))
                {
                    sb.AppendLine(entry.Id + "|RESOLVE-FAIL");
                    continue;
                }
                if (entry.HazardOverride != null) config.Hazards = entry.HazardOverride;
                sb.AppendLine(Run(entry.Id, new CinderSim(in config)));
            }

            foreach (var id in new[] { CampaignStages.CinderSpan, CampaignStages.AbyssChancel, CampaignStages.EchoThrone })
            {
                if (CampaignStages.TryGet(id, 2, 1, 3, out var classic))
                    sb.AppendLine(Run("classic-" + id, new CinderSim(in classic)));
            }

            var path = "_workspace/current/qa/golden-rows-unity.md";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("GoldenDigestRecorder: wrote " + path);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static string Run(string label, CinderSim sim)
        {
            for (var t = 0; t < Ticks; t++)
            {
                sim.Tick(Kiter(sim));
            }
            var d = sim.Digest;
            return string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                label, d.Score, d.Wave, d.Kills, d.Relics,
                d.HealthRemaining.ToString("R", CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(d.Reason) ? "(running)" : d.Reason,
                sim.Player.X.ToString("R", CultureInfo.InvariantCulture),
                sim.Player.Y.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Mirror of CampaignSimTests.BotInput — keep byte-identical. This copy
        /// exists because Assembly-CSharp-Editor cannot reference the test asmdef
        /// (autoReferenced:false). AUTHORITY IS THE TEST: DungeonGoldenDigestTests
        /// recomputes every row with the real BotInput, so any skew between the two
        /// bots fails the gate — never re-pin from this recorder without a green
        /// DungeonGoldenDigestTests run agreeing with it.
        /// </summary>
        static SimInput Kiter(CinderSim sim)
        {
            float px = sim.Player.X, py = sim.Player.Y;
            float bestD2 = float.MaxValue, dx = 0f, dy = 0f;
            var enemies = sim.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Dead) continue;
                float ex = enemy.X - px, ey = (enemy.Y - py) * SimConfig.IsoY;
                var d2 = ex * ex + ey * ey;
                if (d2 < bestD2) { bestD2 = d2; dx = enemy.X - px; dy = enemy.Y - py; }
            }
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };
            if (bestD2 < float.MaxValue)
            {
                var d = System.MathF.Sqrt(bestD2);
                var len = System.MathF.Max(0.001f, System.MathF.Sqrt(dx * dx + dy * dy));
                if (d < 120f) { input.MoveX = -dx / len; input.MoveY = -dy / len; }
                else if (d > 150f) { input.MoveX = dx / len; input.MoveY = dy / len; }
            }
            return input;
        }
    }
}
