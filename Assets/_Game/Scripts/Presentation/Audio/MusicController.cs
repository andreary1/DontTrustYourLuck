using UnityEngine;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>
    /// Background music with simple adaptive ducking (unlucky reveal, victory fanfare).
    /// Plays the library's soundtrack playlist in shuffled order (each track once per round, short fade between
    /// tracks); falls back to looping the single Music clip when the playlist is empty or an override is set.
    /// Volume follows <see cref="AudioSettingsModel.EffectiveMusic"/>. Update only does float checks while playing.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicController : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private AudioClip musicOverride;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Music level while ducked (fraction of the normal level).")]
        [Range(0f, 1f)] [SerializeField] private float duckLevel = 0.3f;
        [Min(0.01f)] [SerializeField] private float fadeSpeed = 3f;
        [Tooltip("Fade-out at the end of each playlist track before the next one starts.")]
        [Min(0.05f)] [SerializeField] private float trackFadeOutSeconds = 1.5f;
        [Min(0.01f)] [SerializeField] private float trackFadeInSeconds = 0.15f;
        [Tooltip("0 = different shuffle every session; any other value gives a reproducible order.")]
        [SerializeField] private int shuffleSeed;

        private AudioSource _source;
        private float _duck = 1f;
        private float _duckUntil;
        private bool _subscribed;

        private MusicPlaylistOrder _order;
        private bool _playlistMode;
        private float _trackFade = 1f;
        private bool _fadingOut;
        private float _trackStartedAt;

        public bool IsPlaying => _source != null && _source.isPlaying;
        public float CurrentVolume => _source != null ? _source.volume : 0f;
        public AudioClip CurrentClip => _source != null ? _source.clip : null;

        public void Configure(AudioManager manager) => audioManager = manager;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (audioManager != null) audioManager.Settings.Changed -= ApplyVolume;
        }

        private void Start()
        {
            Subscribe(); // the manager may have been configured after OnEnable (tests, builders)
            if (playOnStart) Play();
        }

        private void Subscribe()
        {
            if (_subscribed || audioManager == null) return;
            _subscribed = true;
            audioManager.Settings.Changed += ApplyVolume;
        }

        /// <summary>Starts the music. Keeps the current track when it is already playing (e.g. on restart).</summary>
        public void Play()
        {
            var library = audioManager != null ? audioManager.Library : null;
            var playlist = library != null ? library.MusicPlaylist : null;

            if (musicOverride == null && playlist != null && playlist.Count > 0)
            {
                if (_playlistMode && _source.isPlaying) return;
                _playlistMode = true;
                if (_order == null || _order.Count != playlist.Count)
                    _order = new MusicPlaylistOrder(playlist.Count, shuffleSeed != 0 ? shuffleSeed : System.Environment.TickCount);
                StartTrack(playlist[_order.Next()], playlist.Count == 1);
                return;
            }

            _playlistMode = false;
            var clip = musicOverride != null ? musicOverride : library != null ? library.Music : null;
            if (clip == null) return; // no music asset yet: infrastructure stays silent, no error
            _source.loop = true;
            _trackFade = 1f;
            if (_source.clip != clip) _source.clip = clip;
            ApplyVolume();
            if (!_source.isPlaying) _source.Play();
        }

        private void StartTrack(AudioClip clip, bool loop)
        {
            _source.Stop();
            _source.clip = clip;
            _source.loop = loop;
            _fadingOut = false;
            _trackFade = 0f; // short fade-in avoids a click at the loop point
            _trackStartedAt = Time.unscaledTime;
            ApplyVolume();
            if (clip != null) _source.Play();
        }

        /// <summary>Temporarily lowers the music (e.g. while the unlucky number is revealed).</summary>
        public void Duck(float seconds)
        {
            _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }

        private void Update()
        {
            bool changed = false;

            bool ducking = Time.unscaledTime < _duckUntil;
            if (ducking || _duck < 1f)
            {
                _duck = Mathf.MoveTowards(_duck, ducking ? duckLevel : 1f, fadeSpeed * Time.unscaledDeltaTime);
                changed = true;
            }

            if (_playlistMode && _source.clip != null) changed |= UpdatePlaylist();

            if (changed) ApplyVolume();
        }

        private bool UpdatePlaylist()
        {
            var clip = _source.clip;
            float dt = Time.unscaledDeltaTime;

            if (!_fadingOut && !_source.loop)
            {
                bool nearEnd = _source.isPlaying && clip.length - _source.time <= trackFadeOutSeconds;
                // Stopped by itself after playing for a while (the clip ended between frames).
                bool ended = !_source.isPlaying && Time.unscaledTime - _trackStartedAt > clip.length * 0.5f;
                if (nearEnd || ended) _fadingOut = true;
            }

            if (_fadingOut)
            {
                _trackFade = Mathf.MoveTowards(_trackFade, 0f, dt / trackFadeOutSeconds);
                if (_trackFade <= 0f || !_source.isPlaying)
                {
                    var playlist = audioManager != null && audioManager.Library != null ? audioManager.Library.MusicPlaylist : null;
                    if (playlist == null || playlist.Count == 0 || _order == null) { _playlistMode = false; return true; }
                    StartTrack(playlist[_order.Next()], playlist.Count == 1);
                }
                return true;
            }

            if (_trackFade < 1f)
            {
                _trackFade = Mathf.MoveTowards(_trackFade, 1f, dt / trackFadeInSeconds);
                return true;
            }
            return false;
        }

        private void ApplyVolume()
        {
            if (_source == null) return;
            float mix = audioManager != null && audioManager.Library != null ? audioManager.Library.MusicVolume : 0.5f;
            float settings = audioManager != null ? audioManager.Settings.EffectiveMusic : 1f;
            _source.volume = settings * mix * _duck * _trackFade;
        }
    }
}
