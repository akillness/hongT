// SimEvents -> one-shot cues, plus looping BGM bed and lore ambience.
// User directive 2026-08-04: SFX + BGM only, NO voice narration.
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class AudioDirector : MonoBehaviour
    {
        const string MuteKey = "abyssal-lantern:cinder-court:muted";

        AudioSource _oneShot;
        AudioSource _bgm;
        AudioClip _strike, _hit, _kill, _nova, _ward, _pickup, _wave, _gameover, _lore;
        bool _muted;

        public bool Muted => _muted;

        void Awake()
        {
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;

            _strike = Resources.Load<AudioClip>("Audio/cue-strike");
            _hit = Resources.Load<AudioClip>("Audio/cue-hit");
            _kill = Resources.Load<AudioClip>("Audio/cue-kill");
            _nova = Resources.Load<AudioClip>("Audio/cue-nova");
            _ward = Resources.Load<AudioClip>("Audio/cue-ward");
            _pickup = Resources.Load<AudioClip>("Audio/cue-pickup");
            _wave = Resources.Load<AudioClip>("Audio/cue-wave");
            _gameover = Resources.Load<AudioClip>("Audio/cue-gameover");
            _lore = Resources.Load<AudioClip>("Audio/cue-lore");

            var bgmClip = Resources.Load<AudioClip>("Audio/cue-bgm");
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

        public void ToggleMute()
        {
            _muted = !_muted;
            PlayerPrefs.SetInt(MuteKey, _muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMute();
        }

        void Play(AudioClip clip, float volume = 1f)
        {
            if (_muted || clip == null || _oneShot == null) return;
            _oneShot.PlayOneShot(clip, volume);   // overlap allowed, no trimming
        }

        public void OnEvents(SimEvents events)
        {
            if ((events & SimEvents.PlayerStruck) != 0) Play(_strike);
            if ((events & SimEvents.EnemyHit) != 0) Play(_hit);
            if ((events & SimEvents.EnemyKilled) != 0) Play(_kill);
            if ((events & SimEvents.NovaCast) != 0) Play(_nova);
            if ((events & SimEvents.WardCast) != 0) Play(_ward);
            if ((events & SimEvents.PickupCollected) != 0) Play(_pickup);
            if ((events & SimEvents.WaveStarted) != 0)
            {
                Play(_wave);
                Play(_lore, 0.5f);   // ambient texture under the lore line
            }
            if ((events & SimEvents.GameOver) != 0) Play(_gameover);
            // Campaign events reuse existing cues (no extra generation needed):
            if ((events & SimEvents.HazardPulse) != 0) Play(_hit, 0.6f);
            if ((events & SimEvents.AltarBlessing) != 0) Play(_ward, 0.7f);
            if ((events & SimEvents.EquipDropped) != 0) Play(_pickup, 0.8f);
            if ((events & SimEvents.StageCleared) != 0)
            {
                Play(_wave);          // triumphant horn
                Play(_pickup, 0.9f);  // sparkle on top
            }
        }
    }
}
