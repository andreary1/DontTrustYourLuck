using System;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Match
{
    /// <summary>Inspector setup for one seat: display name, color and controller (human or bot).</summary>
    [Serializable]
    public sealed class ParticipantConfig
    {
        public string displayName = "Player";
        public Color color = Color.white;
        public bool isBot;

        public ParticipantConfig() { }

        public ParticipantConfig(string name, Color pieceColor, bool bot)
        {
            displayName = name;
            color = pieceColor;
            isBot = bot;
        }
    }
}
