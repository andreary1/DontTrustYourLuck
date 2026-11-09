using System;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Every player-facing HUD string in one serialized table (string.Format templates), so a future
    /// localization pass only swaps this data. Domain events never carry text.
    /// </summary>
    [Serializable]
    public sealed class HudText
    {
        [Header("Labels")]
        public string luckyLabel = "LUCKY";
        public string unluckyLabel = "UNLUCKY";
        public string roundLabel = "ROUND";
        public string turnLabel = "TURN";
        public string roundValue = "{0} / {1}";
        public string emptyValue = "-";
        public string rollButton = "Roll Dice";
        public string chooseButton = "Choose {0}";
        public string dieA = "A";
        public string dieB = "B";
        public string playAgainButton = "Play Again";
        public string playersTitle = "PLAYERS";
        public string messagesTitle = "LOG";
        public string gameTitle = "Don't Trust Your Luck!";
        public string gameSubtitle = "Fortaleza";

        [Header("Dice destination preview (Sprint 4)")]
        public string destinationTile = "-> {0}";
        public string destinationLucky = "-> {0} LUCKY!";
        public string destinationUnlucky = "-> {0} DANGER!";

        [Header("Banners (Sprint 4)")]
        public string bannerTurn = "{0}'s turn";
        public string bannerTurnBot = "{0} (Bot)";
        public string bannerUnlucky = "Unlucky number: {0}";
        public string bannerUnluckySub = "Do not end your move there!";
        public string bannerCard = "{0}";
        public string bannerCardSub = "played by {0}";
        public string bannerEliminated = "{0} is out!";
        public string bannerShieldBlocked = "Shield saved {0}!";

        [Header("Audio settings (Sprint 4)")]
        public string audioButton = "Sound";
        public string audioButtonMuted = "Muted";
        public string audioTitle = "SOUND";
        public string audioMaster = "Master";
        public string audioMusic = "Music";
        public string audioSfx = "Effects";
        public string audioMute = "Mute all";

        [Header("Player card")]
        public string tileValue = "Tile {0}";
        public string statusActive = "Active";
        public string statusEliminated = "Eliminated";
        public string statusWinner = "Winner";
        public string statusShielded = "Shielded";
        public string botTag = "{0} (Bot)";

        [Header("Cards")]
        public string cardsTitle = "CARDS";
        public string cardsTitleOwner = "{0}'s cards";
        public string noCards = "No cards in hand.";
        public string playCardButton = "Play card";
        public string cancelCardButton = "Cancel";
        public string hintPickCard = "Pick a card (one per turn).";
        public string hintPickTarget = "{0}: pick a player to swap with.";
        public string hintConfirm = "Play {0}?";
        public string hintCardUsed = "Card already used this turn.";
        public string hintNoPlayable = "No card can be played now.";
        public string hintBotHand = "{0} plays with dice only.";
        public string hintWaiting = "";

        [Header("Messages")]
        public string matchStarted = "The lucky number is {0}!";
        public string roundStarted = "Round {0} started!";
        public string unluckyDrawn = "The unlucky number is {0}!";
        public string turnChanged = "{0}'s turn.";
        public string diceRolled = "{0} rolled {1} and {2}.";
        public string dieSelected = "{0} chose {1}.";
        public string playerMoved = "{0} moved to tile {1}.";
        public string playerMovedRebound = "{0} bounced back to tile {1}.";
        public string playerEliminated = "{0} landed on {1} and is eliminated!";
        public string playerWon = "{0} wins!";
        public string botThinking = "{0} is thinking...";
        public string luckyRollActivated = "{0}: Lucky Roll activated!";
        public string diceRerolled = "New dice: {0} and {1}.";
        public string positionsSwapped = "{0} swapped positions with {1}!";
        public string playerRetreated = "{0} moved back {1} tiles!";
        public string playerRetreatedNone = "{0} is already at the start: no tiles to move back.";
        public string shieldActivated = "{0} activated Shield!";
        public string shieldBlocked = "Shield blocked elimination! {0} is safe on tile {1}.";
        public string shieldExpired = "{0}'s Shield expired.";
        public string cardGranted = "{0} received {1}.";

        [Header("Result screen")]
        public string resultSingleWinner = "{0} wins!";
        public string resultSharedWinners = "{0} share the win!";
        public string winnerJoin = " & ";
        public string reasonExactLucky = "Landed exactly on the lucky number {0}.";
        public string reasonLastSurvivor = "Last player standing.";
        public string reasonRoundLimit = "Round limit reached: closest to the lucky number {0}.";
    }
}
