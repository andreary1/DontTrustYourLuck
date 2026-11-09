using System.Collections.Generic;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>
    /// Plays sound effects through a small fixed pool of 2D AudioSources (no allocation per sound) and owns
    /// the volume settings. Scene component referenced explicitly (no global singleton).
    ///
    /// Web: browsers keep the audio context suspended until the first user gesture; Unity Web resumes it
    /// automatically on the first click/key, so nothing here depends on autoplay. Sounds requested before
    /// that are simply inaudible.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SoundEffectLibrary library;
        [Tooltip("Simultaneous effect voices. The oldest voice is reused when all are busy.")]
        [Range(1, 12)] [SerializeField] private int voices = 6;
        [Tooltip("Persist volume settings in PlayerPrefs (off in tests).")]
        [SerializeField] private bool persistSettings = true;

        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly Dictionary<SfxId, float> _lastPlayed = new Dictionary<SfxId, float>();
        private int _next;
        private AudioSettingsModel _settings;

        public SoundEffectLibrary Library => library;
        public AudioSettingsModel Settings => _settings ??= CreateSettings();

        /// <summary>Raised for every cue actually played (tests and debug overlays).</summary>
        public event System.Action<SfxId> Played;

        public int PlayedCount { get; private set; }

        public void Configure(SoundEffectLibrary effectLibrary, bool persist)
        {
            library = effectLibrary;
            persistSettings = persist;
        }

        private AudioSettingsModel CreateSettings() =>
            new AudioSettingsModel(persistSettings ? new PlayerPrefsAudioSettingsStore() : null);

        private void Awake()
        {
            _ = Settings;
            EnsureVoices();
        }

        private void EnsureVoices()
        {
            while (_sources.Count < voices)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // 2D: the camera is fixed, positional audio adds nothing here
                _sources.Add(source);
            }
        }

        /// <summary>Plays a cue if the library has clips for it. Returns false when skipped.</summary>
        public bool Play(SfxId id, float volumeScale = 1f)
        {
            if (id == SfxId.None || library == null || !library.TryGet(id, out var entry)) return false;

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out float last) && now - last < entry.cooldown) return false;
            _lastPlayed[id] = now;

            PlayedCount++;
            Played?.Invoke(id);

            float volume = Settings.EffectiveSfx * entry.volume * volumeScale;
            if (volume <= 0.0001f) return true; // muted: counted as handled, nothing to play

            EnsureVoices();
            var source = NextVoice();
            var clip = entry.clips.Length == 1 ? entry.clips[0] : entry.clips[Random.Range(0, entry.clips.Length)];
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter); // cosmetic randomness only
            source.PlayOneShot(clip, volume);
            return true;
        }

        private AudioSource NextVoice()
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                int index = (_next + i) % _sources.Count;
                if (_sources[index].isPlaying) continue;
                _next = (index + 1) % _sources.Count;
                return _sources[index];
            }
            var oldest = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            oldest.Stop();
            return oldest;
        }

        /// <summary>Stops all effects (restart).</summary>
        public void StopAll()
        {
            foreach (var s in _sources)
            {
                if (s != null) s.Stop();
            }
        }
    }
}
