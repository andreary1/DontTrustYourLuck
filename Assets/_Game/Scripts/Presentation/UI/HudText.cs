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

        [Header("Player card")]
        public string tileValue = "Tile {0}";
        public string statusActive = "Active";
        public string statusEliminated = "Eliminated";
        public string statusWinner = "Winner";
        public string botTag = "{0} (Bot)";

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

        [Header("Result screen")]
        public string resultSingleWinner = "{0} wins!";
        public string resultSharedWinners = "{0} share the win!";
        public string winnerJoin = " & ";
        public string reasonExactLucky = "Landed exactly on the lucky number {0}.";
        public string reasonLastSurvivor = "Last player standing.";
        public string reasonRoundLimit = "Round limit reached: closest to the lucky number {0}.";
    }
}
