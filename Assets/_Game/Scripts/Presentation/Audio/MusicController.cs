using UnityEngine;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>
    /// Background music loop with simple adaptive ducking (unlucky reveal, victory fanfare).
    /// Volume follows <see cref="AudioSettingsModel.EffectiveMusic"/>. Update is a no-op unless a duck is fading.
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

        private AudioSource _source;
        private float _duck = 1f;
        private float _duckUntil;

        public bool IsPlaying => _source != null && _source.isPlaying;
        public float CurrentVolume => _source != null ? _source.volume : 0f;

        private bool _subscribed;

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

        public void Play()
        {
            var clip = musicOverride != null ? musicOverride : audioManager != null && audioManager.Library != null ? audioManager.Library.Music : null;
            if (clip == null) return; // no music asset yet: infrastructure stays silent, no error
            if (_source.clip != clip) _source.clip = clip;
            ApplyVolume();
            if (!_source.isPlaying) _source.Play();
        }

        /// <summary>Temporarily lowers the music (e.g. while the unlucky number is revealed).</summary>
        public void Duck(float seconds)
        {
            _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }

        private void Update()
        {
            bool ducking = Time.unscaledTime < _duckUntil;
            if (!ducking && _duck >= 1f) return; // idle: nothing to fade
            float target = Time.unscaledTime < _duckUntil ? duckLevel : 1f;
            _duck = Mathf.MoveTowards(_duck, target, fadeSpeed * Time.unscaledDeltaTime);
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (_source == null) return;
            float mix = audioManager != null && audioManager.Library != null ? audioManager.Library.MusicVolume : 0.5f;
            float settings = audioManager != null ? audioManager.Settings.EffectiveMusic : 1f;
            _source.volume = settings * mix * _duck;
        }
    }
}
