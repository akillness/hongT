// EDITOR ONLY. Adopts GEMINI_API_KEY from the untracked `.env.game-audio` at the
// project root into PlayerPrefs, so the command console's planner works in Play
// mode without retyping "키 <API키>" every session.
//
// This can never ship and never leaks:
//   - Assets/Editor is stripped from every player build (WebGL included), so no
//     build artifact ever contains this code path.
//   - `.env.game-audio` is gitignored (.gitignore:114 `.env*`), so the key is
//     not in the repository either.
//   - The runtime contract in GeminiCommandClient is unchanged: the key still
//     only ever lives obfuscated in PlayerPrefs on this machine (KeyVault).
// The key itself is NEVER logged — only whether one was found.
using System;
using System.IO;
using CinderCourt.View;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    [InitializeOnLoad]
    public static class GeminiDevKey
    {
        const string EnvFileName = ".env.game-audio";
        const string EnvKeyName = "GEMINI_API_KEY";

        static GeminiDevKey()
        {
            // Only when nothing is stored yet. A key typed into the console by
            // hand outranks the file; re-adopting on every domain reload would
            // silently undo that choice.
            if (GeminiCommandClient.HasKey) return;
            TryAdopt(verbose: false);
        }

        [MenuItem("CinderCourt/Dev/Load Gemini Key From .env.game-audio")]
        public static void ReloadFromMenu() => TryAdopt(verbose: true);

        static bool TryAdopt(bool verbose)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), EnvFileName);
            if (!File.Exists(path))
            {
                if (verbose)
                    Debug.LogWarning($"[GeminiDevKey] {EnvFileName} 없음 — 콘솔에서 '키 <API키>'로 등록하세요.");
                return false;
            }

            string key;
            try
            {
                key = ReadKey(path);
            }
            catch (IOException error)
            {
                Debug.LogWarning($"[GeminiDevKey] {EnvFileName} 읽기 실패: {error.Message}");
                return false;
            }

            // GeminiCommandClient.StoreKey accepts anything; the console's own
            // guard is >8 chars, so match it rather than storing a stub that
            // would make HasKey lie.
            if (string.IsNullOrEmpty(key) || key.Length <= 8)
            {
                if (verbose)
                    Debug.LogWarning($"[GeminiDevKey] {EnvFileName}에 쓸 만한 {EnvKeyName} 값이 없습니다.");
                return false;
            }

            GeminiCommandClient.StoreKey(key);
            Debug.Log($"[GeminiDevKey] {EnvFileName}의 {EnvKeyName}를 에디터 PlayerPrefs에 저장했습니다 " +
                      "(난독화 저장 · 빌드에는 포함되지 않음).");
            return true;
        }

        static string ReadKey(string path)
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var split = line.IndexOf('=');
                if (split <= 0) continue;
                var name = line.Substring(0, split).Trim();
                if (!string.Equals(name, EnvKeyName, StringComparison.OrdinalIgnoreCase)) continue;
                return line.Substring(split + 1).Trim().Trim('"', '\'');
            }
            return null;
        }
    }
}
