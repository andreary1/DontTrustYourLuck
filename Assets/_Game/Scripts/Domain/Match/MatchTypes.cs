namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>
    /// Match lifecycle (TDD §6.1). Commands are resolved synchronously, so RoundStart, ResolvingAction and
    /// TurnEnd are transient: they are observable while a command runs (e.g. from event handlers), but
    /// between commands the phase is always NotStarted, AwaitingRoll, AwaitingDiceChoice or Finished.
    /// </summary>
    public enum MatchPhase
    {
        NotStarted,
        RoundStart,
        AwaitingRoll,
        AwaitingDiceChoice,
        ResolvingAction,
        TurnEnd,
        Finished
    }

    public enum PlayerStatus
    {
        Active,
        Eliminated,
        Winner
    }

    /// <summary>Why the match ended (TDD §9.2.1).</summary>
    public enum MatchEndReason
    {
        None,
        /// <summary>A player ended a move exactly on the lucky number.</summary>
        ExactLucky,
        /// <summary>Every other player was eliminated.</summary>
        LastSurvivor,
        /// <summary>MaxRounds completed; active players with the highest position win (ties share).</summary>
        RoundLimit
    }

    public enum EliminationReason
    {
        UnluckyTile
    }

    /// <summary>Why a command was rejected. A rejected command never changes state nor consumes randomness.</summary>
    public enum CommandError
    {
        None,
        WrongPhase,
        MatchFinished,
        NotYourTurn,
        UnknownPlayer,
        InvalidDieIndex,
        InvalidPlayerCount,
        DuplicatePlayerId,
        InvalidPlayerId
    }
}
