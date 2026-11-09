using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>Presentation sound cues. Never referenced by the domain.</summary>
    public enum SfxId
    {
        None = 0,
        // Dice
        DiceRoll = 10,
        DiceStop = 11,
        DieChoose = 12,
        // Board
        PieceStep = 20,
        PieceLand = 21,
        // Cards
        CardSelect = 30,
        CardPlay = 31,
        CardDiscard = 32,
        ShieldUp = 33,
        ShieldBlock = 34,
        Swap = 35,
        Retreat = 36,
        // Luck
        LuckyChime = 40,
        Victory = 41,
        // Danger
        DangerReveal = 50,
        Elimination = 51,
        // Interface
        UiClick = 60,
        UiConfirm = 61,
        UiError = 62,
        Restart = 63,
        TurnChange = 64,
    }

    [Serializable]
    public sealed class SoundEffectEntry
    {
        public SfxId id;
        [Tooltip("One is picked at random (variation without extra code).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.3f)] public float pitchJitter = 0.04f;
        [Tooltip("Repeated requests closer than this are dropped (prevents stacking the same sound).")]
        [Min(0f)] public float cooldown = 0.04f;
    }

    /// <summary>Designer-authored mapping from cue to clips and mix values. Static data only.</summary>
    [CreateAssetMenu(fileName = "SoundEffectLibrary", menuName = "Don't Trust Your Luck/Sound Effect Library")]
    public sealed class SoundEffectLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundEffectEntry> entries = new List<SoundEffectEntry>();
        [SerializeField] private AudioClip music;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.55f;

        private Dictionary<SfxId, SoundEffectEntry> _lookup;

        public IReadOnlyList<SoundEffectEntry> Entries => entries;
        public AudioClip Music => music;
        public float MusicVolume => musicVolume;

        public bool TryGet(SfxId id, out SoundEffectEntry entry)
        {
            if (_lookup == null || _lookup.Count != entries.Count) BuildLookup();
            return _lookup.TryGetValue(id, out entry) && entry.clips != null && entry.clips.Length > 0;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<SfxId, SoundEffectEntry>();
            foreach (var e in entries)
            {
                if (e != null && e.id != SfxId.None) _lookup[e.id] = e; // last wins
            }
        }

        private void OnValidate() => _lookup = null;

        /// <summary>Editor setup (Sprint 4 builder).</summary>
        public void Configure(List<SoundEffectEntry> effectEntries, AudioClip musicClip, float musicMix)
        {
            entries = effectEntries ?? new List<SoundEffectEntry>();
            music = musicClip;
            musicVolume = Mathf.Clamp01(musicMix);
            _lookup = null;
        }
    }
}
