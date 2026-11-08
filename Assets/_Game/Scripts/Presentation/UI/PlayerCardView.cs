using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>One player card in the players panel.</summary>
    public sealed class PlayerCardView : MonoBehaviour
    {
        [SerializeField] private Image swatch;
        [SerializeField] private Image highlight;
        [SerializeField] private Image background;
        [SerializeField] private Text nameText;
        [SerializeField] private Text positionText;
        [SerializeField] private Text statusText;

        public void Bind(Image swatchImage, Image highlightImage, Image backgroundImage, Text name, Text position, Text status)
        {
            swatch = swatchImage; highlight = highlightImage; background = backgroundImage;
            nameText = name; positionText = position; statusText = status;
        }

        public bool IsHighlighted => highlight != null && highlight.enabled;
        public string StatusText => statusText.text;
        public string PositionText => positionText.text;

        public void Show(HudText text, PlayerDisplay player, bool isCurrent)
        {
            gameObject.SetActive(true);
            swatch.color = player.Color;
            nameText.text = player.IsBot ? string.Format(text.botTag, player.Name) : player.Name;
            positionText.text = string.Format(text.tileValue, player.Position);
            highlight.enabled = isCurrent;

            switch (player.Status)
            {
                case PlayerDisplayStatus.Eliminated:
                    statusText.text = text.statusEliminated;
                    statusText.color = HudPalette.Danger;
                    background.color = HudPalette.CardDisabled;
                    break;
                case PlayerDisplayStatus.Winner:
                    statusText.text = text.statusWinner;
                    statusText.color = HudPalette.LuckyDark;
                    background.color = HudPalette.CardWinner;
                    break;
                default:
                    statusText.text = text.statusActive;
                    statusText.color = HudPalette.Ink;
                    background.color = HudPalette.Card;
                    break;
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
