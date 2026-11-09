using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>
    /// Domain event (as played back by the MatchDirector) -> presentation handler -> sound cue.
    /// The domain never calls audio; this router only listens to presentation events (so sounds stay in sync
    /// with the animations) and to HUD intents (clicks). It also binds the audio settings view to the model.
    /// </summary>
    public sealed class AudioEventRouter : MonoBehaviour
    {
        [SerializeField] private MatchDirector director;
        [SerializeField] private GameHUD hud;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private MusicController music;
        [Min(0f)] [SerializeField] private float unluckyDuckSeconds = 1.2f;
        [Min(0f)] [SerializeField] private float victoryDuckSeconds = 3.5f;

        private readonly List<SfxId> _buffer = new List<SfxId>(4);
        private readonly Dictionary<int, int> _hopsLeft = new Dictionary<int, int>();
        private readonly List<Button> _clickButtons = new List<Button>();
        private bool _subscribed;

        public void Configure(MatchDirector matchDirector, GameHUD gameHud, AudioManager manager, MusicController musicController)
        {
            director = matchDirector; hud = gameHud; audioManager = manager; music = musicController;
        }

        private void OnEnable() => Subscribe();

        private void Start()
        {
            Subscribe(); // references may have been configured after OnEnable (tests, builders)
            if (hud != null)
            {
                // Generic click for every HUD button; specific cues layer on top through events.
                foreach (var button in hud.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(OnAnyButton);
                    _clickButtons.Add(button);
                }
            }
            RefreshSettingsView();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            foreach (var b in _clickButtons)
            {
                if (b != null) b.onClick.RemoveListener(OnAnyButton);
            }
            _clickButtons.Clear();
        }

        private void Subscribe()
        {
            if (_subscribed || director == null || audioManager == null) return;
            _subscribed = true;
            director.EventPresenting += OnEventPresenting;
            director.PieceHopped += OnPieceHopped;
            director.CommandRejected += OnCommandRejected;
            if (hud != null)
            {
                hud.CardSlotClicked += OnCardSlotClicked;
                hud.CardCancelRequested += OnCardCancel;
                hud.PlayAgainRequested += OnPlayAgain;
                if (hud.DicePanel != null) hud.DicePanel.RollAnimationFinished += OnRollFinished;
                foreach (var view in hud.AudioSettingsViews)
                {
                    view.MasterChanged += audioManager.Settings.SetMaster;
                    view.MusicChanged += audioManager.Settings.SetMusic;
                    view.SfxChanged += audioManager.Settings.SetSfx;
                    view.MuteChanged += audioManager.Settings.SetMuted;
                }
            }
            audioManager.Settings.Changed += RefreshSettingsView;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (director != null)
            {
                director.EventPresenting -= OnEventPresenting;
                director.PieceHopped -= OnPieceHopped;
                director.CommandRejected -= OnCommandRejected;
            }
            if (hud != null)
            {
                hud.CardSlotClicked -= OnCardSlotClicked;
                hud.CardCancelRequested -= OnCardCancel;
                hud.PlayAgainRequested -= OnPlayAgain;
                if (hud.DicePanel != null) hud.DicePanel.RollAnimationFinished -= OnRollFinished;
                if (audioManager != null)
                foreach (var view in hud.AudioSettingsViews)
                {
                    view.MasterChanged -= audioManager.Settings.SetMaster;
                    view.MusicChanged -= audioManager.Settings.SetMusic;
                    view.SfxChanged -= audioManager.Settings.SetSfx;
                    view.MuteChanged -= audioManager.Settings.SetMuted;
                }
            }
            if (audioManager != null) audioManager.Settings.Changed -= RefreshSettingsView;
        }

        private void RefreshSettingsView()
        {
            if (hud == null || audioManager == null) return;
            var s = audioManager.Settings;
            foreach (var view in hud.AudioSettingsViews) view.ShowValues(s.Master, s.Music, s.Sfx, s.Muted);
        }

        // ------------------------------------------------------------ match events

        private void OnEventPresenting(MatchEvent e)
        {
            switch (e)
            {
                case PlayerMovedEvent moved:
                    _hopsLeft[moved.PlayerId] = moved.Movement.Path.Count;
                    break;
                case PlayerRetreatedEvent retreated:
                    _hopsLeft[retreated.PlayerId] = retreated.Steps;
                    break;
                case UnluckyNumberDrawnEvent _:
                    if (music != null) music.Duck(unluckyDuckSeconds);
                    break;
                case PlayerWonEvent _:
                    if (music != null) music.Duck(victoryDuckSeconds);
                    break;
                case MatchStartedEvent _:
                    _hopsLeft.Clear();
                    if (music != null) music.Play();
                    break;
            }

            _buffer.Clear();
            MapEvent(e, _buffer);
            foreach (var id in _buffer) audioManager.Play(id);
        }

        private void OnPieceHopped(int playerId, int tile)
        {
            int left = _hopsLeft.TryGetValue(playerId, out int n) ? n - 1 : 0;
            _hopsLeft[playerId] = left;
            audioManager.Play(left <= 0 ? SfxId.PieceLand : SfxId.PieceStep);
        }

        private void OnCommandRejected(CommandError error) => audioManager.Play(SfxId.UiError);
        private void OnCardSlotClicked(int slot) => audioManager.Play(SfxId.CardSelect);
        private void OnCardCancel() => audioManager.Play(SfxId.CardDiscard);
        private void OnPlayAgain() => audioManager.Play(SfxId.Restart);
        private void OnRollFinished() => audioManager.Play(SfxId.DiceStop);
        private void OnAnyButton() => audioManager.Play(SfxId.UiClick);

        /// <summary>
        /// Pure mapping from a domain event to sound cues (no Unity state), unit tested.
        /// Movement steps are not mapped here: they follow the hop animation (<see cref="MatchDirector.PieceHopped"/>).
        /// </summary>
        public static void MapEvent(MatchEvent e, List<SfxId> output)
        {
            switch (e)
            {
                case MatchStartedEvent _: output.Add(SfxId.LuckyChime); break;
                case UnluckyNumberDrawnEvent _: output.Add(SfxId.DangerReveal); break;
                case TurnChangedEvent _: output.Add(SfxId.TurnChange); break;
                case DiceRolledEvent _: output.Add(SfxId.DiceRoll); break;
                case DiceRerolledEvent _: output.Add(SfxId.DiceRoll); break;
                case DieSelectedEvent _: output.Add(SfxId.DieChoose); break;
                case PlayerEliminatedEvent _: output.Add(SfxId.Elimination); break;
                case PlayerWonEvent _: output.Add(SfxId.Victory); break;
                case CardPlayedEvent _: output.Add(SfxId.CardPlay); break;
                case PlayerPositionsSwappedEvent _: output.Add(SfxId.Swap); break;
                case PlayerRetreatedEvent _: output.Add(SfxId.Retreat); break;
                case ShieldActivatedEvent _: output.Add(SfxId.ShieldUp); break;
                case ShieldConsumedEvent shield:
                    if (shield.BlockedElimination) output.Add(SfxId.ShieldBlock);
                    break;
                case CardDrawnEvent drawn:
                    if (drawn.Source == CardDrawSource.Granted) output.Add(SfxId.CardSelect);
                    break;
            }
        }
    }
}
