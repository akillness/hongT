// Persistence v2 (spec §11). Extends the v0.1 campaign key with meta-growth:
// stats, relics, roster, active companion, prologue flag. Backwards
// compatible — a v0.1 blob ({"cleared":[...],"equipment":{...}}) loads with
// every new field at its default. Fixed-shape micro-parsing, mirroring
// WebGLStorage.ReadCampaign (no external JSON dependency).
using System.Text;

namespace CinderCourt.View
{
    /// <summary>Full lobby meta state, one struct, all fields explicit.</summary>
    public struct CampaignData
    {
        // v3 clear progression (six catalog stages, bits 0-5).
        public int ClearedMask;
        public int Weapon, Lantern, Cloak;          // equipment tiers T0-T5

        // v2 fields (spec §11).
        public int Attack, Vitality, Swiftness;     // allocated points, cap 10 each
        public int Points;                          // unspent stat points
        public int Relics;                          // meta currency
        public string[] Roster;                     // companion ids, never null after Load
        public string Active;                       // active companion id, "" = none
        public bool PrologueDone;
    }

    public static class CampaignStore
    {
        const string Key = "abyssal-lantern:unity:campaign";
        static readonly StringBuilder Builder = new StringBuilder(512);
        static readonly string[] EmptyRoster = new string[0];

        public static CampaignData Load()
        {
            var data = default(CampaignData);
            data.Roster = EmptyRoster;
            data.Active = "";
            var raw = WebGLStorage.GetString(Key);
            if (string.IsNullOrEmpty(raw)) return data;

            // v3 clear progression takes precedence over the legacy cleared ids.
            // The fixed-shape integer parser returns zero for missing/negative values.
            var hasClearedMask = raw.IndexOf("\"clearedMask\":", System.StringComparison.Ordinal) >= 0;
            if (hasClearedMask)
            {
                data.ClearedMask = ExtractInt(raw, "\"clearedMask\":") & 0x3F;
            }
            else
            {
                // Legacy ids may also appear in roster; scope this scan to cleared only.
                var cleared = Section(raw, "\"cleared\":[", ']');
                if (cleared.Contains("\"cinder-span\"")) data.ClearedMask |= 1 << 0;
                if (cleared.Contains("\"abyss-chancel\"")) data.ClearedMask |= 1 << 2;
                if (cleared.Contains("\"echo-throne\"")) data.ClearedMask |= 1 << 4;
            }

            data.Weapon = ExtractInt(raw, "\"weapon\":");
            data.Lantern = ExtractInt(raw, "\"lantern\":");
            data.Cloak = ExtractInt(raw, "\"cloak\":");

            // v2 fields — absent markers all fall back to 0/""/false (v0.1 blob).
            data.Attack = ExtractInt(raw, "\"attack\":");
            data.Vitality = ExtractInt(raw, "\"vitality\":");
            data.Swiftness = ExtractInt(raw, "\"swiftness\":");
            data.Points = ExtractInt(raw, "\"points\":");
            data.Relics = ExtractInt(raw, "\"relics\":");
            data.Roster = ExtractStrings(Section(raw, "\"roster\":[", ']'));
            data.Active = ExtractString(raw, "\"active\":\"");
            data.PrologueDone = raw.Contains("\"prologueDone\":true");
            return data;
        }

        public static void Save(in CampaignData data)
        {
            Builder.Length = 0;
            Builder.Append("{\"clearedMask\":").Append(data.ClearedMask & 0x3F)
                .Append(",\"equipment\":{\"weapon\":").Append(data.Weapon)
                .Append(",\"lantern\":").Append(data.Lantern)
                .Append(",\"cloak\":").Append(data.Cloak)
                .Append("},\"stats\":{\"attack\":").Append(data.Attack)
                .Append(",\"vitality\":").Append(data.Vitality)
                .Append(",\"swiftness\":").Append(data.Swiftness)
                .Append(",\"points\":").Append(data.Points)
                .Append("},\"relics\":").Append(data.Relics)
                .Append(",\"roster\":[");
            var roster = data.Roster;
            if (roster != null)
            {
                for (var i = 0; i < roster.Length; i++)
                {
                    if (i > 0) Builder.Append(',');
                    Builder.Append('"').Append(roster[i]).Append('"');
                }
            }
            Builder.Append("],\"active\":\"").Append(data.Active ?? "")
                .Append("\",\"prologueDone\":").Append(data.PrologueDone ? "true" : "false")
                .Append('}');
            WebGLStorage.SetString(Key, Builder.ToString());
        }

        // ------------------------------------------------------------ parsing --
        /// <summary>Substring from after <paramref name="open"/> to the first
        /// <paramref name="close"/>; "" when the marker is absent.</summary>
        static string Section(string raw, string open, char close)
        {
            var start = raw.IndexOf(open, System.StringComparison.Ordinal);
            if (start < 0) return "";
            start += open.Length;
            var end = raw.IndexOf(close, start);
            if (end < 0) return "";
            return raw.Substring(start, end - start);
        }

        /// <summary>All double-quoted tokens inside an array body.</summary>
        static string[] ExtractStrings(string body)
        {
            if (string.IsNullOrEmpty(body)) return EmptyRoster;
            var count = 0;
            for (var i = 0; i < body.Length; i++)
                if (body[i] == '"') count++;
            count /= 2;
            if (count == 0) return EmptyRoster;
            var result = new string[count];
            var index = 0;
            var cursor = 0;
            while (index < count)
            {
                var open = body.IndexOf('"', cursor);
                var close = body.IndexOf('"', open + 1);
                if (open < 0 || close < 0) break;
                result[index++] = body.Substring(open + 1, close - open - 1);
                cursor = close + 1;
            }
            return result;
        }

        static string ExtractString(string raw, string marker)
        {
            var start = raw.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0) return "";
            start += marker.Length;
            var end = raw.IndexOf('"', start);
            if (end < 0) return "";
            return raw.Substring(start, end - start);
        }

        static int ExtractInt(string raw, string marker)
        {
            var index = raw.IndexOf(marker, System.StringComparison.Ordinal);
            if (index < 0) return 0;
            index += marker.Length;
            var value = 0;
            while (index < raw.Length && char.IsDigit(raw[index]))
            {
                value = value * 10 + (raw[index] - '0');
                index++;
            }
            return value;
        }
    }
}
