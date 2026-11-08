using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private List<PlayerCardView> cards = new List<PlayerCardView>();

        public IReadOnlyList<PlayerCardView> Cards => cards;

        public void Bind(Text titleText, List<PlayerCardView> cardViews)
        {
            title = titleText;
            cards = cardViews;
        }

        public void ApplyLabels(HudText text) => title.text = text.playersTitle;

        public void Show(HudText text, IReadOnlyList<PlayerDisplay> players, int currentPlayerId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (i < players.Count) cards[i].Show(text, players[i], players[i].Id == currentPlayerId);
                else cards[i].Hide();
            }
        }
    }
}
