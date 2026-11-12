using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Domain.Series;

namespace DontTrustYourLuck.Presentation.Narration
{
    public enum CantadorCue
    {
        LuckyDrawn,
        AzarLands,
        Elimination,
        ShieldSaved,
        Bump,
        LastCycle,
        PointExactLucky,
        PointLastSurvivor,
        PointClosest,
        PointClosestTie,
        NoSurvivors,
        Champion,
        ChampionShared
    }

    /// <summary>One formatted line ready to show. Higher priority lines win over lower ones in the box queue.</summary>
    public readonly struct CantadorLine
    {
        public CantadorCue Cue { get; }
        public string Text { get; }
        public int Priority { get; }
        public bool IsFinale => Cue == CantadorCue.Champion || Cue == CantadorCue.ChampionShared;

        public CantadorLine(CantadorCue cue, string text, int priority)
        {
            Cue = cue;
            Text = text;
            Priority = priority;
        }
    }

    /// <summary>
    /// Pure narration logic (no Unity): domain event -> Cantador line. Reacts only to resolved events and never
    /// affects rules. Uses its own presentation RNG, separate from the match RNG, so rules stay reproducible.
    /// </summary>
    public sealed class CantadorNarrator
    {
        private readonly CantadorLines _lines;
        private readonly Func<int, string> _nameOf;
        private readonly Random _random;
        private readonly Dictionary<CantadorCue, int> _lastPick = new Dictionary<CantadorCue, int>();
        private readonly List<string> _names = new List<string>(4);
        private int _lucky;
        private int _lastBumper = -1;

        /// <summary>Series mode: round endings award points and the champion comes from SeriesWonEvent.</summary>
        public bool SeriesMode { get; set; }

        /// <summary>Cycle cap of the current round; the cycle that reaches it gets the "last cycle" line. 0 = unknown.</summary>
        public int MaxCycles { get; set; }

        public CantadorNarrator(CantadorLines lines, Func<int, string> nameOf, int seed)
        {
            _lines = lines ?? throw new ArgumentNullException(nameof(lines));
            _nameOf = nameOf ?? (id => "Player " + id);
            _random = new Random(seed);
        }

        public bool TryNarrate(MatchEvent e, out CantadorLine line)
        {
            line = default;
            switch (e)
            {
                case MatchStartedEvent started:
                    _lucky = started.LuckyNumber;
                    _lastBumper = -1;
                    return Make(CantadorCue.LuckyDrawn, 1, -1, null, 0, out line);

                case UnluckyNumberDrawnEvent unlucky:
                    if (MaxCycles > 0 && unlucky.RoundNumber >= MaxCycles)
                        return Make(CantadorCue.LastCycle, 1, -1, null, unlucky.UnluckyNumber, out line);
                    return Make(CantadorCue.AzarLands, 1, -1, null, unlucky.UnluckyNumber, out line);

                case TurnChangedEvent _:
                    _lastBumper = -1;
                    return false;

                case PlayerEliminatedEvent eliminated:
                    return Make(CantadorCue.Elimination, 2, eliminated.PlayerId, null, 0, out line);

                case ShieldConsumedEvent shield:
                    if (!shield.BlockedElimination) return false;
                    return Make(CantadorCue.ShieldSaved, 1, shield.PlayerId, null, 0, out line);

                case PlayerBumpedEvent bumped:
                    // One bump can push several victims: narrate the bumper once per turn.
                    if (bumped.BumperId == _lastBumper) return false;
                    _lastBumper = bumped.BumperId;
                    return Make(CantadorCue.Bump, 0, bumped.BumperId, null, 0, out line);

                case MatchFinishedEvent finished:
                    return Finished(finished, out line);

                case SeriesWonEvent won:
                    return Make(CantadorCue.Champion, 4, won.ChampionId, null, 0, out line);
            }
            return false;
        }

        private bool Finished(MatchFinishedEvent finished, out CantadorLine line)
        {
            var winners = finished.WinnerIds;
            int count = winners != null ? winners.Count : 0;
            int first = count > 0 ? winners[0] : -1;

            if (finished.Reason == MatchEndReason.NoSurvivors || count == 0)
                return Make(CantadorCue.NoSurvivors, 3, -1, null, 0, out line);

            if (!SeriesMode)
            {
                return count == 1
                    ? Make(CantadorCue.Champion, 4, first, null, 0, out line)
                    : Make(CantadorCue.ChampionShared, 4, -1, winners, 0, out line);
            }

            switch (finished.Reason)
            {
                case MatchEndReason.ExactLucky: return Make(CantadorCue.PointExactLucky, 3, first, null, 0, out line);
                case MatchEndReason.LastSurvivor: return Make(CantadorCue.PointLastSurvivor, 3, first, null, 0, out line);
                case MatchEndReason.RoundLimit:
                    return count == 1
                        ? Make(CantadorCue.PointClosest, 3, first, null, 0, out line)
                        : Make(CantadorCue.PointClosestTie, 3, -1, winners, 0, out line);
            }
            line = default;
            return false;
        }

        private bool Make(CantadorCue cue, int priority, int playerId, IReadOnlyList<int> players, int unlucky, out CantadorLine line)
        {
            line = default;
            string template = Pick(cue);
            if (string.IsNullOrEmpty(template)) return false;

            string text = Format(template, playerId, players, unlucky);
            if ((cue == CantadorCue.Champion || cue == CantadorCue.ChampionShared) && !string.IsNullOrEmpty(_lines.moral))
                text += "\n<i>" + _lines.moral + "</i>";
            line = new CantadorLine(cue, text, priority);
            return true;
        }

        /// <summary>Random line of the cue's pool, never the same as the previous pick of that cue.</summary>
        private string Pick(CantadorCue cue)
        {
            var pool = _lines.For(cue);
            if (pool == null || pool.Length == 0) return null;
            int index = 0;
            if (pool.Length > 1)
            {
                int last = _lastPick.TryGetValue(cue, out int l) ? l : -1;
                index = _random.Next(last >= 0 ? pool.Length - 1 : pool.Length);
                if (last >= 0 && index >= last) index++; // skip the previous pick
            }
            _lastPick[cue] = index;
            return pool[index];
        }

        private string Format(string template, int playerId, IReadOnlyList<int> players, int unlucky)
        {
            string text = template.Replace(" / ", "\n");
            text = text.Replace("{lucky}", _lucky.ToString());
            text = text.Replace("{unlucky}", unlucky.ToString());
            if (playerId >= 0) text = text.Replace("{player}", _nameOf(playerId));
            if (players != null)
            {
                _names.Clear();
                foreach (int id in players) _names.Add(_nameOf(id));
                text = text.Replace("{players}", string.Join(_lines.namesJoin, _names));
            }
            return text;
        }
    }
}
