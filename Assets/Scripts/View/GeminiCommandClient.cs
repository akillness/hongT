// Optional Gemini fallback for free-form companion orders the local parser
// cannot classify. SECURITY CONTRACT (static GitHub Pages, no server):
// - The API key is NEVER baked into the build or committed. It enters at
//   runtime only (console command "키 <key>" or ?gemini= URL param) and lives
//   in PlayerPrefs on the player's own machine.
// - Classification output is a single intent word; it funnels into the same
//   deterministic SimInput latches as a keystroke. Latency shifts WHEN the
//   latch is set, never WHAT the simulation does with it.
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CinderCourt.View
{
    public static class GeminiCommandClient
    {
        private const string KeyPref = "al:gemini-key";
        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        public static bool HasKey => !string.IsNullOrEmpty(LoadKey());

        public static void StoreKey(string key)
        {
            // Obfuscated at rest (spec §Lane K, KeyVault honesty contract).
            PlayerPrefs.SetString(KeyPref, KeyVault.Protect(key ?? ""));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Stored key → usable plaintext. Three paths (spec §Lane K):
        /// obfuscated value decrypts; legacy PLAINTEXT value is adopted and
        /// re-saved obfuscated (one-time migration); a marked value that no
        /// longer decrypts (device changed, tampering) is CLEARED so HasKey
        /// turns false and the console shows the re-entry hint — graceful
        /// loss, never a wedge.
        /// </summary>
        internal static string LoadKey()
        {
            var stored = PlayerPrefs.GetString(KeyPref, "");
            if (string.IsNullOrEmpty(stored)) return "";
            var key = KeyVault.Unprotect(stored);
            if (key == null)
            {
                PlayerPrefs.DeleteKey(KeyPref);
                PlayerPrefs.Save();
                return "";
            }
            if (!KeyVault.IsProtected(stored))
            {
                // Legacy plaintext save — upgrade in place.
                PlayerPrefs.SetString(KeyPref, KeyVault.Protect(key));
                PlayerPrefs.Save();
            }
            return key;
        }

        /// <summary>WebGL convenience: adopt #gemini=KEY once. FRAGMENT, not
        /// query — fragments never leave the browser (queries hit GitHub Pages'
        /// request logs). History still remembers it: stripping is the player's
        /// job (documented in the console help toast).</summary>
        public static void AdoptUrlKeyIfPresent()
        {
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return;
            var marker = url.IndexOf("#gemini=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return;
            var start = marker + "#gemini=".Length;
            var end = url.IndexOfAny(new[] { '&', '#' }, start);
            var key = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            if (key.Length > 8) StoreKey(Uri.UnescapeDataString(key));
        }

        /// <summary>One-shot classification coroutine. Calls back with the parsed
        /// intent (Unknown on any failure — caller shows honest feedback and the
        /// game keeps running; a dead network can never wedge input).</summary>
        public static IEnumerator Classify(string text, Action<CompanionCommandIntent> done)
        {
            var key = LoadKey();
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(text))
            {
                done(CompanionCommandIntent.Unknown);
                yield break;
            }

            // Plain-text contract: exactly one vocabulary word comes back.
            var prompt =
                "You classify a Korean or English game command for a summoned guardian. " +
                "Reply with EXACTLY one word from this list and nothing else: " +
                "FocusAttack, Defend, Recall, PickupInfo, SkillBolt, SkillPulse, SkillNova, SkillAegis, SkillDash, Unknown. " +
                "FocusAttack = order the guardian to hold position and keep attacking enemies. " +
                "Defend/Recall = order the guardian back to escort the player. " +
                "PickupInfo = asking the guardian to fetch items. " +
                "Skill* = the player wants to cast that skill (bolt=화살, pulse=파동, nova=노바/폭발, aegis=결계/방패, dash=질주). " +
                "Command: " + text;

            var body = "{\"contents\":[{\"parts\":[{\"text\":\"" + Escape(prompt) + "\"}]}]," +
                       "\"generationConfig\":{\"maxOutputTokens\":8,\"temperature\":0}}";

            using var request = new UnityWebRequest(Endpoint + "?key=" + UnityWebRequest.EscapeURL(key), "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 6;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                done(CompanionCommandIntent.Unknown);
                yield break;
            }
            done(CompanionCommandParser.FromIntentWord(ExtractFirstText(request.downloadHandler.text)));
        }

        /// <summary>Pulls candidates[0].content.parts[0].text without a JSON lib —
        /// the reply is one word, so a scan for the first "text" field suffices.</summary>
        internal static string ExtractFirstText(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var marker = json.IndexOf("\"text\"", StringComparison.Ordinal);
            if (marker < 0) return null;
            var colon = json.IndexOf(':', marker + 6);
            if (colon < 0) return null;
            var open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;
            var builder = new StringBuilder(16);
            for (var i = open + 1; i < json.Length && builder.Length < 64; i++)
            {
                var c = json[i];
                if (c == '\\') { i++; continue; }   // skip escapes — intent words have none
                if (c == '"') break;
                builder.Append(c);
            }
            return builder.ToString();
        }

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
