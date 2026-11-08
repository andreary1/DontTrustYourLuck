using System.Collections.Generic;

namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>Logical player. Only the rules layer (this assembly) mutates it.</summary>
    public sealed class PlayerState
    {
        public int Id { get; }
        /// <summary>Logical tile index, always in [0, LuckyNumber]. Source of truth for piece placement.</summary>
        public int Position { get; internal set; }
        public PlayerStatus Status { get; internal set; }

        public bool IsActive => Status == PlayerStatus.Active;

        internal PlayerState(int id)
        {
            Id = id;
            Position = 0;
            Status = PlayerStatus.Active;
        }

        public override string ToString() => $"P{Id}@{Position} ({Status})";
    }

    /// <summary>
    /// Mutable runtime state of one match. Read-only for other assemblies; mutated only by <see cref="MatchEngine"/>.
    /// A restart creates a brand new engine and state; nothing is reused.
    /// </summary>
    public sealed class MatchState
    {
        internal readonly List<PlayerState> PlayerList = new List<PlayerState>();
        internal readonly List<int> WinnerList = new List<int>();

        public MatchPhase Phase { get; internal set; } = MatchPhase.NotStarted;
        /// <summary>1-based round number; 0 before the match starts.</summary>
        public int RoundNumber { get; internal set; }
        /// <summary>Id of the player whose turn it is; -1 when no turn is in progress.</summary>
        public int CurrentPlayerId { get; internal set; } = -1;
        public int LuckyNumber { get; internal set; }
        public int UnluckyNumber { get; internal set; }
        /// <summary>Pending roll awaiting a choice; null otherwise. Kept until ChooseDie resolves.</summary>
        public DiceRoll CurrentRoll { get; internal set; }
        public MatchEndReason EndReason { get; internal set; } = MatchEndReason.None;

        /// <summary>Players in turn order (ascending Id).</summary>
        public IReadOnlyList<PlayerState> Players => PlayerList;
        /// <summary>Winner ids (several only on a RoundLimit tie). Empty until the match finishes.</summary>
        public IReadOnlyList<int> WinnerIds => WinnerList;

        public bool IsFinished => Phase == MatchPhase.Finished;

        public PlayerState GetPlayer(int playerId)
        {
            foreach (var p in PlayerList)
            {
                if (p.Id == playerId) return p;
            }
            return null;
        }

        public int ActivePlayerCount
        {
            get
            {
                int count = 0;
                foreach (var p in PlayerList)
                {
                    if (p.IsActive) count++;
                }
                return count;
            }
        }
    }
}
