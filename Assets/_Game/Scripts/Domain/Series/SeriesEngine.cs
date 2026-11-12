using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Series
{
    /// <summary>
    /// Series of rounds until a champion (R1). Pure C#, deterministic for given random factories.
    /// It creates one fresh <see cref="MatchEngine"/> per round and turns each round's MatchFinishedEvent into
    /// points; it never duplicates movement or card rules.
    ///
    /// Between rounds (decisions D2-D4, D8): new lucky number (new engine), everybody back on tile 0 and active,
    /// hands kept (card ids carried over; cards are only dealt at random in round 1), first player rotates by
    /// ascending id. Scoring (D5, D6): exact lucky, last survivor and the cycle cap each give +1 to every round
    /// winner (a cap tie gives +1 to each tied player). When several players reach PointsToWin with the same top
    /// score, the next round is a sudden-death tiebreaker among them only (D7).
    ///
    /// Randomness: <c>roundRandomFactory</c> (and <c>cardRandomFactory</c> when cards are on) is called once per
    /// round, in that order, right before the round starts. Rejected commands call no factory.
    /// </summary>
    public sealed class SeriesEngine
    {
        private readonly SeriesRules _rules;
        private readonly MatchRulesConfig _roundRules;
        private readonly Func<IRandomSource> _roundRandomFactory;
        private readonly CardCatalog _cardCatalog;
        private readonly Func<IRandomSource> _cardRandomFactory;
        private readonly SeriesState _state = new SeriesState();
        private readonly Dictionary<int, IReadOnlyList<string>> _carriedHands = new Dictionary<int, IReadOnlyList<string>>();

        private MatchEngine _round;

        public SeriesState State => _state;
        public SeriesRules Rules => _rules;
        /// <summary>Match rules of every round (base rules with the series cycle cap).</summary>
        public MatchRulesConfig RoundRules => _roundRules;
        /// <summary>Engine of the current (or last finished) round; null before the series starts.</summary>
        public MatchEngine CurrentRound => _round;
        public bool CardsEnabled => _cardCatalog != null;

        /// <param name="matchRules">Base match rules; MaxRounds is replaced by <see cref="SeriesRules.MaxCyclesPerRound"/>.</param>
        /// <param name="roundRandomFactory">Called once per round for the round's dice/lucky/unlucky stream.</param>
        /// <param name="cardCatalog">Cards of every round; null (with <paramref name="cardRandomFactory"/>) disables cards.</param>
        /// <param name="cardRandomFactory">Called once per round, right after <paramref name="roundRandomFactory"/>.</param>
        public SeriesEngine(SeriesRules rules, MatchRulesConfig matchRules, Func<IRandomSource> roundRandomFactory,
            CardCatalog cardCatalog = null, Func<IRandomSource> cardRandomFactory = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (matchRules == null) throw new ArgumentNullException(nameof(matchRules));
            _roundRandomFactory = roundRandomFactory ?? throw new ArgumentNullException(nameof(roundRandomFactory));
            if ((cardCatalog == null) != (cardRandomFactory == null))
                throw new ArgumentException("Cards need both a catalog and a card random factory.");
            _cardCatalog = cardCatalog;
            _cardRandomFactory = cardRandomFactory;
            _roundRules = rules.CreateRoundRules(matchRules);
        }

        /// <summary>Starts the series and its first round.</summary>
        public CommandResult StartSeries(IReadOnlyList<int> playerIds)
        {
            if (_state.Phase == SeriesPhase.Finished) return CommandResult.Rejected(CommandError.MatchFinished);
            if (_state.Phase != SeriesPhase.NotStarted) return CommandResult.Rejected(CommandError.WrongPhase);
            var error = ValidatePlayers(playerIds, out var ids);
            if (error != CommandError.None) return CommandResult.Rejected(error);

            foreach (int id in ids)
            {
                _state.PlayerIdList.Add(id);
                _state.ScoreMap[id] = 0;
            }

            var events = new List<MatchEvent> { new SeriesStartedEvent(ids.AsReadOnly(), _rules.PointsToWin) };
            BeginRound(ids, false, events);
            return CommandResult.Ok(events);
        }

        /// <summary>Leaves the intermission and starts the next round (presentation calls it after the scoreboard).</summary>
        public CommandResult StartNextRound()
        {
            if (_state.Phase == SeriesPhase.Finished) return CommandResult.Rejected(CommandError.MatchFinished);
            if (_state.Phase != SeriesPhase.Intermission) return CommandResult.Rejected(CommandError.WrongPhase);

            var events = new List<MatchEvent>();
            if (_state.IsTiebreaker) BeginRound(new List<int>(_state.ParticipantList), true, events);
            else BeginRound(new List<int>(_state.PlayerIdList), false, events);
            return CommandResult.Ok(events);
        }

        public CommandResult RollDice(int playerId) => Forward(r => r.RollDice(playerId));

        public CommandResult ChooseDie(int playerId, int dieIndex) => Forward(r => r.ChooseDie(playerId, dieIndex));

        public CommandResult PlayCard(int playerId, string cardInstanceId, int? targetPlayerId = null) =>
            Forward(r => r.PlayCard(playerId, cardInstanceId, targetPlayerId));

        // ------------------------------------------------------------------ internals

        private CommandResult Forward(Func<MatchEngine, CommandResult> command)
        {
            if (_state.Phase == SeriesPhase.Finished) return CommandResult.Rejected(CommandError.MatchFinished);
            if (_state.Phase != SeriesPhase.RoundInProgress) return CommandResult.Rejected(CommandError.WrongPhase);

            var result = command(_round);
            if (!result.Success || !_round.State.IsFinished) return result;

            var events = new List<MatchEvent>(result.Events);
            ScoreRound(events);
            return CommandResult.Ok(events);
        }

        private CommandError ValidatePlayers(IReadOnlyList<int> playerIds, out List<int> ids)
        {
            ids = null;
            if (playerIds == null || playerIds.Count < _roundRules.MinPlayers || playerIds.Count > _roundRules.MaxPlayers)
                return CommandError.InvalidPlayerCount;
            ids = new List<int>(playerIds);
            ids.Sort();
            if (ids[0] <= 0) return CommandError.InvalidPlayerId;
            for (int i = 1; i < ids.Count; i++)
            {
                if (ids[i] == ids[i - 1]) return CommandError.DuplicatePlayerId;
            }
            return CommandError.None;
        }

        private void BeginRound(List<int> participants, bool tiebreaker, List<MatchEvent> events)
        {
            _state.RoundNumber++;
            _state.IsTiebreaker = tiebreaker;
            _state.ParticipantList.Clear();
            _state.ParticipantList.AddRange(participants);

            // D8: the first player rotates every round (ascending id, wrapping).
            int firstPlayer = participants[(_state.RoundNumber - 1) % participants.Count];

            // D4: hands are kept between rounds; round 1 deals random starting hands.
            Dictionary<int, IReadOnlyList<string>> hands = null;
            if (CardsEnabled && _state.RoundNumber > 1)
            {
                hands = new Dictionary<int, IReadOnlyList<string>>();
                foreach (int id in participants)
                {
                    if (_carriedHands.TryGetValue(id, out var carried)) hands[id] = carried;
                }
            }

            var random = _roundRandomFactory();
            var cards = CardsEnabled ? new CardSetup(_cardCatalog, _cardRandomFactory()) : null;
            _round = new MatchEngine(_roundRules, random, cards); // D2/D3: new lucky number, everyone on tile 0

            events.Add(new SeriesRoundStartedEvent(_state.RoundNumber, participants.ToArray(), firstPlayer, tiebreaker));
            var start = _round.StartMatch(participants, new RoundSetup(firstPlayer, hands));
            if (!start.Success)
            {
                // Players were validated and carried hands come from this catalog: this is a programming error.
                throw new InvalidOperationException($"Round {_state.RoundNumber} failed to start: {start.Error}.");
            }
            events.AddRange(start.Events);
            _state.Phase = SeriesPhase.RoundInProgress;
        }

        private void ScoreRound(List<MatchEvent> events)
        {
            var roundState = _round.State;
            _state.LastRoundReason = roundState.EndReason;
            _state.LastScorerList.Clear();
            foreach (int id in roundState.WinnerIds)
            {
                _state.ScoreMap[id] = _state.GetScore(id) + 1;
                _state.LastScorerList.Add(id);
            }

            // Snapshot hands for the next round (eliminated players keep theirs too).
            if (CardsEnabled)
            {
                foreach (var player in roundState.Players)
                {
                    var ids = new List<string>(player.Hand.Count);
                    foreach (var card in player.Hand.Cards) ids.Add(card.CardId);
                    _carriedHands[player.Id] = ids.AsReadOnly();
                }
            }

            events.Add(new SeriesPointsAwardedEvent(_state.RoundNumber, roundState.EndReason,
                _state.LastScorerList.ToArray(), ScoresSnapshot()));

            // Champion check: only the top score among players at or above PointsToWin matters.
            var leaders = new List<int>();
            int best = -1;
            foreach (int id in _state.PlayerIdList)
            {
                int score = _state.GetScore(id);
                if (score < _rules.PointsToWin) continue;
                if (score > best)
                {
                    best = score;
                    leaders.Clear();
                }
                if (score == best) leaders.Add(id);
            }

            if (leaders.Count == 1)
            {
                _state.ChampionId = leaders[0];
                _state.IsTiebreaker = false;
                _state.Phase = SeriesPhase.Finished;
                events.Add(new SeriesWonEvent(leaders[0], _state.RoundNumber, ScoresSnapshot()));
                return;
            }

            if (leaders.Count > 1)
            {
                // D7: next round is a tiebreaker among the tied leaders only.
                _state.IsTiebreaker = true;
                _state.ParticipantList.Clear();
                _state.ParticipantList.AddRange(leaders);
                events.Add(new SeriesTiebreakerEvent(leaders.ToArray()));
            }
            else
            {
                _state.IsTiebreaker = false;
            }
            _state.Phase = SeriesPhase.Intermission;
        }

        private IReadOnlyDictionary<int, int> ScoresSnapshot() => new Dictionary<int, int>(_state.ScoreMap);
    }
}
