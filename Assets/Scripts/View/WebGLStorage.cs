// Browser bridge: run digest, campaign progress (shared key with the static
// campaign.html hub), URL query params, same-tab navigation.
// WebGL uses storage.jslib; editor/standalone fall back to PlayerPrefs.
// jslib string returns use Unity's documented malloc+stringToUTF8 pattern;
// the marshaller converts char* to string (tiny one-shot boot-time leak is
// acceptable and standard).
using System.Globalization;
using System.Text;
using CinderCourt.Sim;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CinderCourt.View
{
    public static class WebGLStorage
    {
        const string DigestKey = "abyssal-lantern:cinder-court:last-run";
        const string CampaignKey = "abyssal-lantern:unity:campaign";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CinderStorageSet(string key, string json);
        [DllImport("__Internal")] static extern string CinderStorageGet(string key);
        [DllImport("__Internal")] static extern string CinderQueryParam(string name);
        [DllImport("__Internal")] static extern void CinderNavigate(string url);
#endif

        static readonly StringBuilder Builder = new StringBuilder(512);

        public static void WriteRunDigest(RunDigest digest)
        {
            Builder.Length = 0;
            Builder.Append("{\"route\":\"cinder-court\",\"score\":").Append(digest.Score)
                .Append(",\"wave\":").Append(digest.Wave)
                .Append(",\"kills\":").Append(digest.Kills)
                .Append(",\"relics\":").Append(digest.Relics)
                .Append(",\"health\":").Append(digest.HealthRemaining.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(",\"reason\":\"").Append(digest.Reason ?? "overrun")
                .Append("\",\"engine\":\"unity\"}");
            Set(DigestKey, Builder.ToString());
        }

        /// <summary>?name= value, or "" outside WebGL / when absent.</summary>
        public static string QueryParam(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CinderQueryParam(name) ?? "";
#else
            return "";
#endif
        }

        public static void Navigate(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CinderNavigate(url);
#else
            Debug.Log($"[WebGLStorage] navigate (noop outside WebGL): {url}");
#endif
        }

        // --- campaign progress ------------------------------------------------
        // Shape (shared with web/campaign.html):
        // {"cleared":["cinder-span"],"equipment":{"weapon":1,"lantern":0,"cloak":2}}

        public struct CampaignProgress
        {
            public bool CinderSpanCleared, AbyssChancelCleared, EchoThroneCleared;
            public int Weapon, Lantern, Cloak;
        }

        public static CampaignProgress ReadCampaign()
        {
            var raw = Get(CampaignKey);
            var progress = default(CampaignProgress);
            if (string.IsNullOrEmpty(raw)) return progress;
            // Tolerant micro-parse (fixed shape, no external JSON dependency).
            progress.CinderSpanCleared = raw.Contains("\"cinder-span\"");
            progress.AbyssChancelCleared = raw.Contains("\"abyss-chancel\"");
            progress.EchoThroneCleared = raw.Contains("\"echo-throne\"");
            progress.Weapon = ExtractInt(raw, "\"weapon\":");
            progress.Lantern = ExtractInt(raw, "\"lantern\":");
            progress.Cloak = ExtractInt(raw, "\"cloak\":");
            return progress;
        }

        public static void WriteCampaign(in CampaignProgress progress)
        {
            Builder.Length = 0;
            Builder.Append("{\"cleared\":[");
            var first = true;
            if (progress.CinderSpanCleared) { Builder.Append("\"cinder-span\""); first = false; }
            if (progress.AbyssChancelCleared) { if (!first) Builder.Append(','); Builder.Append("\"abyss-chancel\""); first = false; }
            if (progress.EchoThroneCleared) { if (!first) Builder.Append(','); Builder.Append("\"echo-throne\""); }
            Builder.Append("],\"equipment\":{\"weapon\":").Append(progress.Weapon)
                .Append(",\"lantern\":").Append(progress.Lantern)
                .Append(",\"cloak\":").Append(progress.Cloak).Append("}}");
            Set(CampaignKey, Builder.ToString());
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

        static void Set(string key, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CinderStorageSet(key, json);
#else
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
#endif
        }

        static string Get(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CinderStorageGet(key);
#else
            return PlayerPrefs.GetString(key, "");
#endif
        }
    }
}
