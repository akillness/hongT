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

        // Campaign persistence moved to CampaignStore (v2, spec §11) — the
        // single writer of the campaign key. This class keeps only raw access.

        // --- raw string access (v2 stores, e.g. CampaignStore) -----------------

        /// <summary>Raw localStorage read; "" when absent.</summary>
        public static string GetString(string key) => Get(key);

        /// <summary>Raw localStorage write (PlayerPrefs outside WebGL).</summary>
        public static void SetString(string key, string json) => Set(key, json);

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
