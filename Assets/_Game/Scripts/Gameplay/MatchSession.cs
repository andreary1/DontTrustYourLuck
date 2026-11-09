using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Gameplay
{
    /// <summary>Participant setup. Display data (name, color) belongs to presentation.</summary>
    public readonly struct PlayerSlot
    {
        public int Id { get; }
        public bool IsBot { get; }

        public PlayerSlot(int id, bool isBot)
        {
            Id = id;
            IsBot = isBot;
        }
    }

    /// <summary>
    /// Application layer (pure C#): owns the current <see cref="MatchEngine"/>, forwards commands from humans
    /// and bots alike, publishes domain events and handles restart. Input -> Command -> Rules -> State -> Events.
    /// </summary>
    public sealed class MatchSession
    {
        private readonly MatchRulesConfig _rules;
        private readonly Func<IRandomSource> _randomFactory;
        private readonly PlayerSlot[] _slots;
        private readonly int[] _playerIds;
        private readonly IPlayerDecisionProvider _botProvider;
        private readonly CardCatalog _cardCatalog;
        private readonly Func<IRandomSource> _cardRandomFactory;

        private MatchEngine _engine;

        /// <summary>Raised for every domain event, in resolution order.</summary>
        public event Action<MatchEvent> EventPublished;

        /// <summary>Raised when a command is rejected (useful for "invalid action" feedback/logging).</summary>
        public event Action<CommandError> CommandRejected;

        public MatchState State => _engine?.State;
        public MatchEngine Engine => _engine;
        public MatchRulesConfig Rules => _rules;
        public IReadOnlyList<PlayerSlot> Slots => _slots;
        /// <summary>Incremented on every (re)start. Lets presentation drop stale scheduled work.</summary>
        public int MatchNumber { get; private set; }

        /// <param name="randomFactory">Called once per match (start and each restart) to get a fresh random source.</param>
        /// <param name="cardCatalog">Cards available in each match. Null (with <paramref name="cardRandomFactory"/>) disables cards.</param>
        /// <param name="cardRandomFactory">Called once per match, right after <paramref name="randomFactory"/>, for the card draw stream.</param>
        public MatchSession(MatchRulesConfig rules, IReadOnlyList<PlayerSlot> slots, Func<IRandomSource> randomFactory,
            IPlayerDecisionProvider botProvider = null, CardCatalog cardCatalog = null,
            Func<IRandomSource> cardRandomFactory = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            if ((cardCatalog == null) != (cardRandomFactory == null))
                throw new ArgumentException("Cards need both a catalog and a card random factory.");
            _cardCatalog = cardCatalog;
            _cardRandomFactory = cardRandomFactory;
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            _slots = new PlayerSlot[slots.Count];
            _playerIds = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                _slots[i] = slots[i];
                _playerIds[i] = slots[i].Id;
            }
            _botProvider = botProvider ?? new SimpleBotDecisionProvider();
        }

        public bool IsBot(int playerId)
        {
            foreach (var slot in _slots)
            {
                if (slot.Id == playerId) return slot.IsBot;
            }
            return false;
        }

        /// <summary>True when it is a bot's turn and the match is waiting for its action.</summary>
        public bool IsBotTurn =>
            _engine != null && !_engine.State.IsFinished && _engine.State.CurrentPlayerId >= 0 &&
            IsBot(_engine.State.CurrentPlayerId);

        public CommandResult StartMatch()
        {
            if (_engine != null && _engine.State.Phase != MatchPhase.NotStarted && !_engine.State.IsFinished)
                return Reject(CommandError.WrongPhase);
            return StartNewEngine();
        }

        /// <summary>Discards the current match (any phase) and starts a new one with a fresh random source.</summary>
        public CommandResult RestartMatch() => StartNewEngine();

        public CommandResult RollDice(int playerId) =>
            _engine == null ? Reject(CommandError.WrongPhase) : Publish(_engine.RollDice(playerId));

        public CommandResult ChooseDie(int playerId, int dieIndex) =>
            _engine == null ? Reject(CommandError.WrongPhase) : Publish(_engine.ChooseDie(playerId, dieIndex));

        /// <summary>Plays a card (humans now; future bot card policies must use this same command).</summary>
        public CommandResult PlayCard(int playerId, string cardInstanceId, int? targetPlayerId = null) =>
            _engine == null ? Reject(CommandError.WrongPhase) : Publish(_engine.PlayCard(playerId, cardInstanceId, targetPlayerId));

        /// <summary>
        /// Asks the bot provider for a die index. Does not change state; the caller sends the result through
        /// <see cref="ChooseDie"/> like a human would. Returns -1 if it is not a bot's pending choice.
        /// </summary>
        public int GetBotChoice()
        {
            if (!IsBotTurn) return -1;
            var state = _engine.State;
            if (state.Phase != MatchPhase.AwaitingDiceChoice || state.CurrentRoll == null) return -1;
            return _botProvider.ChooseDie(_engine, state.CurrentPlayerId, state.CurrentRoll);
        }

        /// <summary>
        /// Performs the next bot step (roll, or choose) through the public commands. Returns null when there
        /// is nothing for a bot to do. Presentation calls this with delays between steps.
        /// </summary>
        public CommandResult StepBot()
        {
            if (!IsBotTurn) return null;
            var state = _engine.State;
            int id = state.CurrentPlayerId;
            if (state.Phase == MatchPhase.AwaitingRoll) return RollDice(id);
            if (state.Phase == MatchPhase.AwaitingDiceChoice) return ChooseDie(id, GetBotChoice());
            return null;
        }

        private CommandResult StartNewEngine()
        {
            var random = _randomFactory();
            var cards = _cardCatalog != null ? new CardSetup(_cardCatalog, _cardRandomFactory()) : null;
            _engine = new MatchEngine(_rules, random, cards); // fresh state: hands, shields, card limit
            MatchNumber++;
            return Publish(_engine.StartMatch(_playerIds));
        }

        private CommandResult Publish(CommandResult result)
        {
            if (!result.Success)
            {
                CommandRejected?.Invoke(result.Error);
                return result;
            }

            int matchNumber = MatchNumber;
            foreach (var e in result.Events)
            {
                // A handler restarting the match mid-publish must not see stale events.
                if (matchNumber != MatchNumber) break;
                EventPublished?.Invoke(e);
            }
            return result;
        }

        private CommandResult Reject(CommandError error)
        {
            CommandRejected?.Invoke(error);
            return CommandResult.Rejected(error);
        }
    }
}
