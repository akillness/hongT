// Optional Gemini planner for free-form companion orders the local keyword scan
// cannot classify. SECURITY CONTRACT (static GitHub Pages, no server):
// - The API key is NEVER baked into the build or committed. It enters at
//   runtime only (console command "키 <key>" or a #gemini= URL FRAGMENT — never
//   a query, which GitHub Pages would log) and lives in PlayerPrefs on the
//   player's own machine.
// - The reply is an ORDERED list of words from a closed vocabulary. Each one
//   funnels into the same deterministic SimInput latch a keystroke sets, and
//   CommandSequenceRunner spends them one FINISHED game event at a time.
//   Latency and ordering shift WHEN a latch is set, never WHAT the simulation
//   does with it.
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
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";

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

        /// <summary>
        /// Free-form order -> ORDERED plan (command agent). The single-intent
        /// classifier this file used to expose is gone: a plan of one step IS
        /// that classification, and two prompts drifting apart is exactly how a
        /// vocabulary rots.
        ///
        /// Same security contract as before — runtime key only, and the reply is
        /// still nothing but words from a closed vocabulary that funnel into the
        /// deterministic latches a keystroke sets. The model shifts WHEN a latch
        /// is set and in WHAT ORDER, never what the simulation does with it.
        ///
        /// Calls back with (plan, failure). Failure is a short Korean reason the
        /// console shows verbatim — "요청 실패 429" is worth ten "해석 실패"s
        /// when the key's quota is what actually died.
        /// </summary>
        public static IEnumerator Plan(string text, Action<CommandPlan, string> done)
        {
            var key = LoadKey();
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(text))
            {
                done(CommandPlan.Empty, "키 없음");
                yield break;
            }

            // JSON contract. responseMimeType keeps prose out of the payload;
            // CommandPlanParser.ParseJson still unwraps fences/prose because a
            // model that ignores the mime type must not wedge the console.
            var prompt =
                "You convert a Korean or English game order into an ORDERED action sequence " +
                "for a 2.5D hack-and-slash guardian game. Reply with JSON ONLY: " +
                "{\"summary\":\"<=16 Korean chars\",\"steps\":[{\"do\":\"<word>\",\"say\":\"<=12 Korean chars\",\"sec\":<number, Wait only>}]}. " +
                "Allowed do words: FocusAttack, Defend, Recall, PickupInfo, CompanionSkill, " +
                "SkillBolt, SkillPulse, SkillNova, SkillAegis, SkillDash, Wait. " +
                "FocusAttack = guardian chases and engages nearby enemies. " +
                "Defend = guardian holds its current spot. Recall = guardian returns to the player. " +
                "CompanionSkill = guardian casts its OWN signature skill (특기/필살기). " +
                "Skill* = the PLAYER casts it (bolt=화살, pulse=파동, nova=노바/폭발, aegis=결계/방패, dash=질주). " +
                "PickupInfo = asking the guardian to fetch items. " +
                "Wait = pure delay, needs sec. " +
                "Order matters: each step starts only after the previous one finishes. " +
                "Use at most " + CommandPlan.MaxSteps + " steps, fewer when fewer will do. " +
                "Drop anything the vocabulary cannot express instead of inventing a word. " +
                "Command: " + text;

            var body = "{\"contents\":[{\"parts\":[{\"text\":\"" + Escape(prompt) + "\"}]}]," +
                       "\"generationConfig\":{\"maxOutputTokens\":512,\"temperature\":0," +
                       "\"responseMimeType\":\"application/json\"}}";

            using var request = new UnityWebRequest(Endpoint + "?key=" + UnityWebRequest.EscapeURL(key), "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            // A sequence is more tokens than one word; 12 s still fails fast
            // enough that the player is not left staring at "해석 중…".
            request.timeout = 12;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                var status = (int)request.responseCode;
                done(CommandPlan.Empty, status > 0 ? "요청 실패 " + status : "네트워크 실패");
                yield break;
            }

            var payload = ExtractFirstText(request.downloadHandler.text, PlanPayloadLimit);
            var plan = CommandPlanParser.ParseJson(payload);
            done(plan, plan.IsEmpty ? "해석 실패" : null);
        }

        /// <summary>Plan payloads are JSON objects, not single words — but still
        /// bounded, so a runaway reply can never be walked in full.</summary>
        const int PlanPayloadLimit = 2048;

        /// <summary>Pulls candidates[0].content.parts[0].text without a JSON lib.
        /// Escapes are DECODED, not skipped: an intent word has none, but a plan
        /// payload is JSON inside JSON and every one of its quotes arrives as
        /// \" — dropping them would hand the parser a broken document.</summary>
        internal static string ExtractFirstText(string json, int limit = 64)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var marker = json.IndexOf("\"text\"", StringComparison.Ordinal);
            if (marker < 0) return null;
            var colon = json.IndexOf(':', marker + 6);
            if (colon < 0) return null;
            var open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;
            var builder = new StringBuilder(Math.Min(limit, 128));
            for (var i = open + 1; i < json.Length && builder.Length < limit; i++)
            {
                var c = json[i];
                if (c == '"') break;
                if (c != '\\') { builder.Append(c); continue; }
                if (++i >= json.Length) break;
                var escape = json[i];
                switch (escape)
                {
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case 'r': builder.Append('\r'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'u':
                        if (i + 4 >= json.Length) return builder.ToString();
                        var code = 0;
                        var valid = true;
                        for (var k = 1; k <= 4; k++)
                        {
                            var digit = HexValue(json[i + k]);
                            if (digit < 0) { valid = false; break; }
                            code = code * 16 + digit;
                        }
                        if (!valid) return builder.ToString();
                        i += 4;
                        builder.Append((char)code);
                        break;
                    default: builder.Append(escape); break;   // " \ /
                }
            }
            return builder.ToString();
        }

        static int HexValue(char c)
            => c >= '0' && c <= '9' ? c - '0'
                : c >= 'a' && c <= 'f' ? c - 'a' + 10
                : c >= 'A' && c <= 'F' ? c - 'A' + 10
                : -1;

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
