using System;
using DontTrustYourLuck.Presentation.Characters;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Match
{
    /// <summary>Inspector setup for one seat: display name, color, controller (human or bot) and animal.</summary>
    [Serializable]
    public sealed class ParticipantConfig
    {
        public string displayName = "Player";
        public Color color = Color.white;
        public bool isBot;
        [Tooltip("Animal shown on the board and in the HUD (optional: without it the piece is a blank card).")]
        public CharacterDefinitionAsset character;

        public ParticipantConfig() { }

        public ParticipantConfig(string name, Color pieceColor, bool bot, CharacterDefinitionAsset characterAsset = null)
        {
            displayName = name;
            color = pieceColor;
            isBot = bot;
            character = characterAsset;
        }
    }
}
