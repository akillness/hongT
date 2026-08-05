// Local-storage obfuscation for the player-supplied Gemini key (spec §Lane K).
//
// HONESTY CONTRACT: this is OBFUSCATION, not security. A static GitHub Pages
// deploy has no server, so any client-side scheme ships its own inverse; the
// device-derived AES key only raises the bar from "read PlayerPrefs" to "run
// code on the victim's device" — at which point the key was lost anyway. UI
// copy must therefore say 난독화 저장, never 암호화/안전 (spec non-goal).
//
// Format: "enc1:" + base64(IV[16] ‖ AES-CBC ciphertext). Key = SHA-256 of
// SystemInfo.deviceUniqueIdentifier + fixed salt. WebGL note: the identifier
// is browser-fingerprint-derived and can differ across browsers/updates —
// Unprotect then returns null, the caller clears the stored value, and the
// player is asked to re-enter the key (graceful loss, never a wedge).
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CinderCourt.View
{
    public static class KeyVault
    {
        const string Prefix = "enc1:";
        const string Salt = "cinder-court-keyvault-v1";

        static byte[] _deviceKey;
        static byte[] DeviceKey
        {
            get
            {
                if (_deviceKey == null)
                {
                    using var sha = SHA256.Create();
                    _deviceKey = sha.ComputeHash(
                        Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier + Salt));
                }
                return _deviceKey;
            }
        }

        /// <summary>Obfuscate for local storage. Empty/null passes through.</summary>
        public static string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            try
            {
                using var aes = Aes.Create();
                aes.Key = DeviceKey;
                aes.GenerateIV();
                using var encryptor = aes.CreateEncryptor();
                var plain = Encoding.UTF8.GetBytes(plaintext);
                var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
                var packed = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, packed, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, packed, aes.IV.Length, cipher.Length);
                return Prefix + Convert.ToBase64String(packed);
            }
            catch
            {
                // Crypto unavailable (exotic platform): store plaintext rather
                // than lose the feature — the format marker keeps this honest.
                return plaintext;
            }
        }

        /// <summary>
        /// Reverse of <see cref="Protect"/>. Legacy plaintext (no marker)
        /// passes through unchanged so pre-obfuscation saves keep working;
        /// a marked value that fails to decrypt (tampered, device changed)
        /// returns null — callers treat that as "no key, ask again".
        /// </summary>
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return stored;
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;
            try
            {
                var packed = Convert.FromBase64String(stored.Substring(Prefix.Length));
                if (packed.Length <= 16) return null;
                using var aes = Aes.Create();
                aes.Key = DeviceKey;
                var iv = new byte[16];
                Buffer.BlockCopy(packed, 0, iv, 0, 16);
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                var plain = decryptor.TransformFinalBlock(packed, 16, packed.Length - 16);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>True when the stored string carries the obfuscation marker.</summary>
        public static bool IsProtected(string stored)
            => !string.IsNullOrEmpty(stored)
               && stored.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
