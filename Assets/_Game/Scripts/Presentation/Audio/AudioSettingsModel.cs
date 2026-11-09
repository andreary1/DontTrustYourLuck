using System;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>Persistence of the audio settings (PlayerPrefs in builds; in-memory in tests).</summary>
    public interface IAudioSettingsStore
    {
        bool TryLoad(out float master, out float music, out float sfx, out bool muted);
        void Save(float master, float music, float sfx, bool muted);
    }

    /// <summary>PlayerPrefs store. On Unity Web, PlayerPrefs live in the browser's IndexedDB.</summary>
    public sealed class PlayerPrefsAudioSettingsStore : IAudioSettingsStore
    {
        private const string Prefix = "dtyl.audio.";

        public bool TryLoad(out float master, out float music, out float sfx, out bool muted)
        {
            bool has = PlayerPrefs.HasKey(Prefix + "master");
            master = PlayerPrefs.GetFloat(Prefix + "master", 1f);
            music = PlayerPrefs.GetFloat(Prefix + "music", 0.7f);
            sfx = PlayerPrefs.GetFloat(Prefix + "sfx", 0.9f);
            muted = PlayerPrefs.GetInt(Prefix + "muted", 0) == 1;
            return has;
        }

        public void Save(float master, float music, float sfx, bool muted)
        {
            PlayerPrefs.SetFloat(Prefix + "master", master);
            PlayerPrefs.SetFloat(Prefix + "music", music);
            PlayerPrefs.SetFloat(Prefix + "sfx", sfx);
            PlayerPrefs.SetInt(Prefix + "muted", muted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Volume settings (0..1) and mute, independent of Unity objects so they can be unit tested.
    /// Effective volumes already include master and mute.
    /// </summary>
    public sealed class AudioSettingsModel
    {
        public const float DefaultMaster = 1f;
        public const float DefaultMusic = 0.7f;
        public const float DefaultSfx = 0.9f;

        private readonly IAudioSettingsStore _store;

        public float Master { get; private set; } = DefaultMaster;
        public float Music { get; private set; } = DefaultMusic;
        public float Sfx { get; private set; } = DefaultSfx;
        public bool Muted { get; private set; }

        public float EffectiveMusic => Muted ? 0f : Master * Music;
        public float EffectiveSfx => Muted ? 0f : Master * Sfx;

        /// <summary>Raised after any change (already clamped).</summary>
        public event Action Changed;

        public AudioSettingsModel(IAudioSettingsStore store = null)
        {
            _store = store;
            if (_store != null && _store.TryLoad(out float m, out float mu, out float s, out bool muted))
            {
                Master = Clamp(m); Music = Clamp(mu); Sfx = Clamp(s); Muted = muted;
            }
        }

        public void SetMaster(float value)
        {
            float v = Clamp(value);
            if (Mathf.Approximately(Master, v)) return;
            Master = v;
            Commit();
        }

        public void SetMusic(float value)
        {
            float v = Clamp(value);
            if (Mathf.Approximately(Music, v)) return;
            Music = v;
            Commit();
        }

        public void SetSfx(float value)
        {
            float v = Clamp(value);
            if (Mathf.Approximately(Sfx, v)) return;
            Sfx = v;
            Commit();
        }

        public void SetMuted(bool muted)
        {
            if (Muted == muted) return;
            Muted = muted;
            Commit();
        }

        public void ToggleMute() => SetMuted(!Muted);

        private void Commit()
        {
            _store?.Save(Master, Music, Sfx, Muted);
            Changed?.Invoke();
        }

        private static float Clamp(float v) => float.IsNaN(v) ? 0f : Mathf.Clamp01(v);
    }
}
