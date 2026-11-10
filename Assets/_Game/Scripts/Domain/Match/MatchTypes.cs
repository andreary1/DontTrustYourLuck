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
        RoundLimit,
        NoSurvivors
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
        InvalidPlayerId,

        // ---- Cards (Sprint 3) ----
        /// <summary>The instance id is not in the acting player's hand (includes other players' cards).</summary>
        CardNotInHand,
        /// <summary>The player already played a card this turn.</summary>
        CardAlreadyPlayedThisTurn,
        /// <summary>The card exists but cannot be played in the current phase (e.g. Lucky Roll before rolling).</summary>
        CardWrongTiming,
        /// <summary>The card needs a target and none was given.</summary>
        TargetRequired,
        /// <summary>Target is unknown, the player themself, not active, or given to a card without target.</summary>
        InvalidTarget,
        /// <summary>The hand is at MaxHandSize.</summary>
        HandFull,
        /// <summary>The card id does not exist in the match catalog.</summary>
        UnknownCard,
        /// <summary>The match was created without a <see cref="Cards.CardSetup"/>.</summary>
        CardsDisabled,
        /// <summary>No effect is registered for the card's effect type (configuration error).</summary>
        CardEffectUnavailable
    }
}
