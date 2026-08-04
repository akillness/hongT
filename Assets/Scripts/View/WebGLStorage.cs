// Run digest persistence. WebGL: real browser localStorage via storage.jslib
// (same key as the original page). Editor / standalone: PlayerPrefs fallback.
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
        [DllImport("__Internal")]
        static extern void CinderStorageSet(string key, string json);
#endif

        static readonly StringBuilder Builder = new StringBuilder(256);

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
            var json = Builder.ToString();
#if UNITY_WEBGL && !UNITY_EDITOR
            CinderStorageSet(DigestKey, json);
#else
            PlayerPrefs.SetString(DigestKey, json);
            PlayerPrefs.Save();
#endif
        }
    }
}
