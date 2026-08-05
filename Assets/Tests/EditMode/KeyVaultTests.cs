// Lane K contract: the Gemini key is OBFUSCATED at rest (never plaintext in
// PlayerPrefs), legacy plaintext saves migrate in place, and a value that no
// longer decrypts clears itself so the console asks again instead of wedging.
using NUnit.Framework;
using UnityEngine;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class KeyVaultTests
    {
        const string Pref = "al:gemini-key";
        const string SampleKey = "AIzaSyTest-0123456789abcdefg";

        [TearDown]
        public void ClearPref()
        {
            PlayerPrefs.DeleteKey(Pref);
            PlayerPrefs.Save();
        }

        [Test]
        public void Protect_MarksAndHidesPlaintext()
        {
            var stored = KeyVault.Protect(SampleKey);
            Assert.That(KeyVault.IsProtected(stored), Is.True, "marker missing");
            Assert.That(stored, Does.Not.Contain("AIza"), "plaintext leaked into stored form");
        }

        [Test]
        public void Roundtrip_RestoresOriginal()
        {
            Assert.That(KeyVault.Unprotect(KeyVault.Protect(SampleKey)), Is.EqualTo(SampleKey));
        }

        [Test]
        public void Unprotect_PassesLegacyPlaintextThrough()
        {
            Assert.That(KeyVault.Unprotect(SampleKey), Is.EqualTo(SampleKey),
                "legacy (unmarked) values must load unchanged");
        }

        [Test]
        public void Unprotect_TamperedCiphertext_ReturnsNull()
        {
            var stored = KeyVault.Protect(SampleKey);
            var tampered = stored.Substring(0, stored.Length - 4) + "AAA=";
            Assert.That(KeyVault.Unprotect(tampered), Is.Null);
            Assert.That(KeyVault.Unprotect("enc1:not-base64!!"), Is.Null);
        }

        [Test]
        public void Unprotect_EmptyAndNull_AreSafe()
        {
            Assert.That(KeyVault.Unprotect(null), Is.Null);
            Assert.That(KeyVault.Unprotect(""), Is.Empty);
        }

        [Test]
        public void StoreKey_NeverWritesPlaintextToPrefs()
        {
            GeminiCommandClient.StoreKey(SampleKey);
            var raw = PlayerPrefs.GetString(Pref, "");
            Assert.That(KeyVault.IsProtected(raw), Is.True);
            Assert.That(raw, Does.Not.Contain("AIza"));
            Assert.That(GeminiCommandClient.LoadKey(), Is.EqualTo(SampleKey));
            Assert.That(GeminiCommandClient.HasKey, Is.True);
        }

        [Test]
        public void LoadKey_MigratesLegacyPlaintextInPlace()
        {
            PlayerPrefs.SetString(Pref, SampleKey);   // pre-obfuscation save
            PlayerPrefs.Save();
            Assert.That(GeminiCommandClient.LoadKey(), Is.EqualTo(SampleKey));
            var upgraded = PlayerPrefs.GetString(Pref, "");
            Assert.That(KeyVault.IsProtected(upgraded), Is.True, "legacy value not upgraded");
            Assert.That(upgraded, Does.Not.Contain("AIza"));
        }

        [Test]
        public void LoadKey_UndecryptableValue_ClearsAndAsksAgain()
        {
            PlayerPrefs.SetString(Pref, "enc1:dGFtcGVyZWQtbm9uc2Vuc2UtZGF0YQ==");
            PlayerPrefs.Save();
            Assert.That(GeminiCommandClient.LoadKey(), Is.Empty);
            Assert.That(PlayerPrefs.HasKey(Pref), Is.False, "dead value must clear itself");
            Assert.That(GeminiCommandClient.HasKey, Is.False);
        }
    }
}
