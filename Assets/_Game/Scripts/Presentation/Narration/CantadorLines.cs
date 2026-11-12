using System;

namespace DontTrustYourLuck.Presentation.Narration
{
    /// <summary>
    /// Narration text (docs/NARRATIVE.md, draft v0.1). Serializable so it can be tuned in the inspector or moved
    /// into a ScriptableObject later. Couplets use " / " between verses (shown as a line break).
    /// Placeholders: {lucky}, {unlucky}, {player}, {players}.
    /// </summary>
    [Serializable]
    public sealed class CantadorLines
    {
        public string speaker = "THE CANTADOR";

        public string introTitle = "THE CANTADOR'S DECK";
        public string introNext = "Next";
        public string introBegin = "Begin";
        public string introSkip = "Skip";
        public string storyButton = "Story";

        public string[] introStanzas =
        {
            "Come closer, friends, and gather 'round —\nthe Cantador has come to town,\nwith one old deck of printed beasts\nwhose ink has faded soft and brown.\nEach night he shuffles, each night he sings,\nand one card wins the lucky crown.",
            "He draws a number from the deck\nand calls it out for all to hear:\n\"Land exactly on this tile —\nnot one step far, not one step near.\"\nBut every beast believes the same:\n\"My luck is good, I've naught to fear.\"",
            "Above the square, on ragged wings,\nold Azar the vulture flies.\nEach round he picks a different tile\nto darken with his hungry eyes.\nStep in his shadow, little card,\nand there your lucky story dies.",
            "So roll your dice — you'll get a pair,\nbut only one will take you through.\nYour luck can lie, your friends will cheat;\nthe choosing is the part that's true.\nThe deck is shuffled, the candle's lit:\ndon't trust your luck. Trust what you do."
        };

        public string[] luckyDrawn =
        {
            "The card is drawn, the crowd goes still — / reach {lucky} exactly, if you will.",
            "One number shines above the rest: / land on {lucky} and you're the best.",
            "Not one too many, not one too few — / {lucky} is waiting there for you.",
            "The candle's lit, the deck is cut: / it's {lucky} tonight — and nothing but."
        };

        public string[] azarLands =
        {
            "Azar is circling, low and slow — / he's over {unlucky}. Watch below!",
            "Flap, flap, flap — the vulture's here, / and {unlucky}'s the tile to fear.",
            "He's changed his mind, he's changed his perch: / now {unlucky} is where he'll search.",
            "The wind turns cold, the square goes grey — / keep clear of {unlucky} today.",
            "A shadow falls on {unlucky} — beware: / the tiles beside it get their share."
        };

        public string[] elimination =
        {
            "Poor {player} stepped into the shade — / back to the deck, the card will fade.",
            "Rip! goes the paper, sad but true: / {player}, your lucky night is through.",
            "{player} trusted luck — that's not allowed; / the vulture takes them from the crowd."
        };

        public string[] shieldSaved =
        {
            "A charm, a prayer, a lucky thread — / {player} lives to roll instead!",
            "The vulture dove but missed his meal: / {player}'s charm was the real deal."
        };

        public string[] bump =
        {
            "Watch your elbows, watch your feet — / {player} bumps them down the street!",
            "Make way, make way, the fair is packed — / {player} shoves, and they fall back."
        };

        public string[] lastCycle =
        {
            "The candle's low, the wick is thin — / this turn decides who's closest in.",
            "One last flicker, one last throw — / where you stand is what you'll show."
        };

        public string[] pointExactLucky =
        {
            "Exactly {lucky}! Not one step more — / {player} takes the point. Hear them roar!",
            "Spot on the card the Cantador drew: / a point for {player}, through and through."
        };

        public string[] pointLastSurvivor =
        {
            "When all the rest were torn away, / {player} still stood — the point's their pay."
        };

        public string[] pointClosest =
        {
            "The candle's out and no one hit — / {player} came closest; that is it."
        };

        public string[] pointClosestTie =
        {
            "The candle's out — a tie, what luck! / {players} share the point they struck."
        };

        public string[] noSurvivors =
        {
            "The vulture feasted, not one stands — / the deck goes back in the Cantador's hands."
        };

        public string[] champion =
        {
            "Print the card and ring the bell — / {player} played their luck too well!",
            "The fair is done, the deck is set: / {player}'s the luckiest card yet."
        };

        /// <summary>Quick match ending with several winners (cycle cap tie).</summary>
        public string[] championShared =
        {
            "The candle's out — a tie, what luck! / {players} share the crown they struck."
        };

        /// <summary>Fixed closing line after a champion line.</summary>
        public string moral = "...but luck had nothing to do with it.";

        /// <summary>Joins names for {players}.</summary>
        public string namesJoin = " & ";

        public string[] For(CantadorCue cue)
        {
            switch (cue)
            {
                case CantadorCue.LuckyDrawn: return luckyDrawn;
                case CantadorCue.AzarLands: return azarLands;
                case CantadorCue.Elimination: return elimination;
                case CantadorCue.ShieldSaved: return shieldSaved;
                case CantadorCue.Bump: return bump;
                case CantadorCue.LastCycle: return lastCycle;
                case CantadorCue.PointExactLucky: return pointExactLucky;
                case CantadorCue.PointLastSurvivor: return pointLastSurvivor;
                case CantadorCue.PointClosest: return pointClosest;
                case CantadorCue.PointClosestTie: return pointClosestTie;
                case CantadorCue.NoSurvivors: return noSurvivors;
                case CantadorCue.Champion: return champion;
                case CantadorCue.ChampionShared: return championShared;
                default: return null;
            }
        }
    }
}
