// SimEvents -> one-shot cues, plus looping BGM bed and lore ambience.
// User directive 2026-08-04: SFX + BGM only, NO voice narration.
//
// Voice pool + pitch jitter (improvement-brainstorm.md TOP 1): a single
// AudioSource replayed every cue overlaps identical waveforms in phase, so a
// combo's rapid `hit` retriggers read as one buzzing tone. A small round-robin
// pool spreads concurrent one-shots across voices and a deterministic pitch
// jitter (±6%) de-phases repeats so a burst sounds like many strikes, not one.
// WebGL contract: AudioSource.pitch is supported but MUST stay positive
// (docs.unity3d.com/Manual/webgl-audio.html); the jitter range [0.94,1.06]
// never crosses zero. `priority` is a no-op on WebGL, so voice spreading is the
// only way to keep a loud cue from stealing a quiet one.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class AudioDirector : MonoBehaviour
    {
        const string MuteKey = "abyssal-lantern:cinder-court:muted";
        // Enough voices for the loudest realistic frame (nova hit + finisher +
        // kill + pickup) without letting round-robin wrap onto a still-ringing
        // voice mid-burst. WebGL has no hardware voice cap, so this is only
        // about phase spreading, not a channel budget.
        const int VoiceCount = 6;
        // ±6% keeps every retrigger recognizably the same cue while breaking the
        // in-phase overlap that makes repeats buzz. Stays > 0 for WebGL.
        const float PitchJitter = 0.06f;

        readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        int _voiceCursor;
        // View-only presentation RNG. NOT the sim — pitch never feeds a tick, so
        // seeding it deterministically keeps EditMode assertions reproducible
        // without touching the frozen deterministic-sim contract.
        uint _pitchRng = 0x9E3779B9u;

        AudioSource _bgm;
        AudioClip _strike, _hit, _kill, _nova, _ward, _pickup, _wave, _gameover, _lore;
        AudioClip _bgmLegacy;
        AudioClip _click, _footstep;
        // Graded loot cues + the toast whoosh. All three are optional: a build
        // without them falls back to cue-pickup (grades) or stays silent (toast),
        // which is the same null-safe contract PlayClick/PlayFootstep keep.
        AudioClip _lootFine, _lootEpic, _toast;
        readonly Dictionary<string, AudioClip> _bgmByContext =
            new Dictionary<string, AudioClip>(4);
        bool _muted;

        public bool Muted => _muted;

        void Awake()
        {
            for (var i = 0; i < VoiceCount; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                _voices[i] = voice;
            }

            _strike = Resources.Load<AudioClip>("Audio/cue-strike");
            _hit = Resources.Load<AudioClip>("Audio/cue-hit");
            _kill = Resources.Load<AudioClip>("Audio/cue-kill");
            _nova = Resources.Load<AudioClip>("Audio/cue-nova");
            _ward = Resources.Load<AudioClip>("Audio/cue-ward");
            _pickup = Resources.Load<AudioClip>("Audio/cue-pickup");
            _wave = Resources.Load<AudioClip>("Audio/cue-wave");
            _gameover = Resources.Load<AudioClip>("Audio/cue-gameover");
            _lore = Resources.Load<AudioClip>("Audio/cue-lore");
            _click = Resources.Load<AudioClip>("Audio/cue-click");
            _footstep = Resources.Load<AudioClip>("Audio/cue-footstep");
            _lootFine = Resources.Load<AudioClip>("Audio/cue-loot-fine");
            _lootEpic = Resources.Load<AudioClip>("Audio/cue-loot-epic");
            _toast = Resources.Load<AudioClip>("Audio/cue-toast");

            // W12 (seed §2, 2026-08-08): per-context BGM tracks generated via
            // the ElevenLabs Music API (Audio/bgm-{intro,lobby,loading,stage}).
            // The legacy cue-bgm bed stays as the universal fallback so a build
            // missing the new tracks keeps its music.
            _bgmLegacy = Resources.Load<AudioClip>("Audio/cue-bgm");
            var bgmClip = _bgmLegacy;
            if (bgmClip != null)
            {
                _bgm = gameObject.AddComponent<AudioSource>();
                _bgm.clip = bgmClip;
                _bgm.loop = true;
                _bgm.volume = 0.35f;
                _bgm.playOnAwake = false;
            }

            _muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            ApplyMute();
        }

        void ApplyMute()
        {
            if (_bgm != null)
            {
                if (_muted && _bgm.isPlaying) _bgm.Pause();
                else if (!_muted && !_bgm.isPlaying) _bgm.Play();
            }
        }

        /// <summary>W12: UI button click. Null-safe — silent until the cue
        /// asset ships.</summary>
        public void PlayClick() => Play(_click, 0.8f);

        /// <summary>W12: movement footstep tick. The caller owns the cadence
        /// (view-side distance accumulator); this just voices it.</summary>
        public void PlayFootstep() => Play(_footstep, 0.45f);

        /// <summary>Graded acquisition cue for ONE collected pickup.
        ///
        /// Owned by the caller, not by <see cref="OnEvents"/>: SimEvents carries a
        /// bare PickupCollected flag with no grade and no count, so a frame that
        /// swept up three drops raised one flag. GameView diffs the published
        /// pickup list and calls this once per pickup with the grade that pickup
        /// actually carried, which is why PickupCollected/EquipDropped no longer
        /// voice themselves in OnEvents — that would double-play.
        ///
        /// Missing grade cues fall back to cue-pickup, so a build without the
        /// generated assets keeps the acquisition audible at Basic.</summary>
        public void PlayLootCue(LootGrade grade)
        {
            var clip = grade == LootGrade.Epic ? _lootEpic
                     : grade == LootGrade.Fine ? _lootFine
                     : null;
            Play(clip != null ? clip : _pickup);
        }

        /// <summary>The toast's own arrival whoosh. Deliberately quiet: it lands
        /// on the same frame as PlayLootCue and must sit under it, never mask it.
        /// Silent until the cue asset ships.</summary>
        public void PlayToastCue() => Play(_toast, 0.45f);

        /// <summary>W12: swap the looping BGM bed for a context ("intro",
        /// "lobby", "loading", "stage"). Loads Audio/bgm-{context} once and
        /// caches it; a missing track falls back to the legacy cue-bgm bed.
        /// Same-clip calls are no-ops so state transitions can call this
        /// unconditionally without restarting the loop.</summary>
        public void SetBgmContext(string context)
        {
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(context))
            {
                if (!_bgmByContext.TryGetValue(context, out clip))
                {
                    clip = Resources.Load<AudioClip>("Audio/bgm-" + context);
                    _bgmByContext[context] = clip;   // cache misses too
                }
            }
            if (clip == null) clip = _bgmLegacy;
            if (clip == null) return;   // no music shipped at all

            if (_bgm == null)
            {
                // Legacy bed was absent in Awake but a context track exists.
                _bgm = gameObject.AddComponent<AudioSource>();
                _bgm.loop = true;
                _bgm.volume = 0.35f;
                _bgm.playOnAwake = false;
            }
            if (_bgm.clip == clip) { ApplyMute(); return; }
            var wasPlaying = _bgm.isPlaying;
            _bgm.Stop();
            _bgm.clip = clip;
            if (!_muted && (wasPlaying || !_bgm.isPlaying)) _bgm.Play();
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            PlayerPrefs.SetInt(MuteKey, _muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMute();
        }

        /// <summary>Deterministic view-only jitter in [1-PitchJitter, 1+PitchJitter],
        /// advancing an xorshift32 state. Pure given the state, so EditMode can
        /// assert its bounds and that consecutive draws differ. Never touches the
        /// sim RNG or any tick input.</summary>
        internal static float NextPitch(ref uint state)
        {
            // xorshift32 — full-period, cheap, no allocation.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            var unit = (state & 0xFFFFFFu) / (float)0x1000000; // [0,1)
            return 1f + (unit * 2f - 1f) * PitchJitter;
        }

        void Play(AudioClip clip, float volume = 1f)
        {
            if (_muted || clip == null || _voices[0] == null) return;
            var voice = _voices[_voiceCursor];
            _voiceCursor = (_voiceCursor + 1) % VoiceCount;   // round-robin
            voice.pitch = NextPitch(ref _pitchRng);
            voice.PlayOneShot(clip, volume);   // overlap allowed, no trimming
        }

        public void OnEvents(SimEvents events)
        {
            if ((events & SimEvents.PlayerStruck) != 0) Play(_strike);
            if ((events & SimEvents.EnemyHit) != 0) Play(_hit);
            if ((events & SimEvents.EnemyKilled) != 0) Play(_kill);
            if ((events & SimEvents.NovaCast) != 0) Play(_nova);
            if ((events & SimEvents.WardCast) != 0) Play(_ward);
            // PickupCollected is NOT voiced here: the flag carries no grade and
            // no count, so it cannot choose between cue-pickup/fine/epic. GameView
            // resolves both from the published pickup list and calls PlayLootCue
            // once per collected item.
            if ((events & SimEvents.WaveStarted) != 0)
            {
                Play(_wave);
                Play(_lore, 0.5f);   // ambient texture under the lore line
            }
            if ((events & SimEvents.GameOver) != 0) Play(_gameover);
            // Campaign events reuse existing cues (no extra generation needed):
            if ((events & SimEvents.HazardPulse) != 0) Play(_hit, 0.6f);
            if ((events & SimEvents.AltarBlessing) != 0) Play(_ward, 0.7f);
            // EquipDropped likewise routes through PlayLootCue (a shard pickup
            // raises PickupCollected AND EquipDropped in the same tick — voicing
            // both here played cue-pickup twice for one item).
            if ((events & SimEvents.StageCleared) != 0)
            {
                Play(_wave);          // triumphant horn
                Play(_pickup, 0.9f);  // sparkle on top
            }
            // Dungeon kit events (presentation spec #18): volume variations of
            // the existing 8 clips — interim contract until dedicated cues land.
            if ((events & SimEvents.DashUsed) != 0) Play(_strike, 0.5f);
            if ((events & SimEvents.BoltCast) != 0) Play(_nova, 0.45f);
            if ((events & SimEvents.PulseCast) != 0) Play(_ward, 0.6f);
            if ((events & SimEvents.LevelUp) != 0)
            {
                Play(_pickup, 1f);
                Play(_wave, 0.4f);    // fanfare layer
            }
            if ((events & SimEvents.EliteDown) != 0) Play(_kill, 1f);
            if ((events & SimEvents.ExtractionComplete) != 0) Play(_ward, 0.9f);
            if ((events & SimEvents.BossPhase2) != 0) Play(_gameover, 0.35f);   // low menace
            if ((events & SimEvents.ComboFinisher) != 0) Play(_kill, 0.7f);
            if ((events & SimEvents.BossSpawned) != 0) Play(_wave, 0.9f);
        }
    }
}
