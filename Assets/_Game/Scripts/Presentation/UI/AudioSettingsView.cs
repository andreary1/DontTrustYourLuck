using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Small sound button + popover with master/music/effects sliders and mute. Pure view: raises intents and
    /// displays values; the audio router applies them to the settings model.
    /// </summary>
    public sealed class AudioSettingsView : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleLabel;
        [SerializeField] private GameObject popover;
        [SerializeField] private Text titleText;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterLabel;
        [SerializeField] private Text musicLabel;
        [SerializeField] private Text sfxLabel;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Text muteLabel;

        private bool _suppress;
        private HudText _text;

        public event Action<float> MasterChanged;
        public event Action<float> MusicChanged;
        public event Action<float> SfxChanged;
        public event Action<bool> MuteChanged;

        public bool IsOpen => popover != null && popover.activeSelf;
        public Button ToggleButton => toggleButton;
        public Slider MasterSlider => masterSlider;
        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public Toggle MuteToggle => muteToggle;

        public void Bind(Button toggle, Text toggleText, GameObject popoverRoot, Text title, Slider master, Slider music,
            Slider sfx, Text masterText, Text musicText, Text sfxText, Toggle mute, Text muteText)
        {
            toggleButton = toggle; toggleLabel = toggleText; popover = popoverRoot; titleText = title;
            masterSlider = master; musicSlider = music; sfxSlider = sfx;
            masterLabel = masterText; musicLabel = musicText; sfxLabel = sfxText;
            muteToggle = mute; muteLabel = muteText;
        }

        private void Awake()
        {
            toggleButton.onClick.AddListener(TogglePopover);
            masterSlider.onValueChanged.AddListener(v => { if (!_suppress) MasterChanged?.Invoke(v); });
            musicSlider.onValueChanged.AddListener(v => { if (!_suppress) MusicChanged?.Invoke(v); });
            sfxSlider.onValueChanged.AddListener(v => { if (!_suppress) SfxChanged?.Invoke(v); });
            muteToggle.onValueChanged.AddListener(v => { if (!_suppress) MuteChanged?.Invoke(v); });
            popover.SetActive(false);
        }

        private void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveAllListeners();
            if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
            if (musicSlider != null) musicSlider.onValueChanged.RemoveAllListeners();
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
            if (muteToggle != null) muteToggle.onValueChanged.RemoveAllListeners();
        }

        public void ApplyLabels(HudText text)
        {
            _text = text;
            titleText.text = text.audioTitle;
            masterLabel.text = text.audioMaster;
            musicLabel.text = text.audioMusic;
            sfxLabel.text = text.audioSfx;
            muteLabel.text = text.audioMute;
            toggleLabel.text = text.audioButton;
        }

        public void TogglePopover() => popover.SetActive(!popover.activeSelf);

        /// <summary>Displays the current settings without raising change events.</summary>
        public void ShowValues(float master, float music, float sfx, bool muted)
        {
            _suppress = true;
            masterSlider.SetValueWithoutNotify(master);
            musicSlider.SetValueWithoutNotify(music);
            sfxSlider.SetValueWithoutNotify(sfx);
            muteToggle.SetIsOnWithoutNotify(muted);
            if (_text != null) toggleLabel.text = muted ? _text.audioButtonMuted : _text.audioButton;
            _suppress = false;
        }
    }
}
