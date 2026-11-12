using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Top bar: lucky, unlucky, round and current player.</summary>
    public sealed class MatchInfoView : MonoBehaviour
    {
        [SerializeField] private Text luckyLabel;
        [SerializeField] private Text luckyValue;
        [SerializeField] private Text unluckyLabel;
        [SerializeField] private Text unluckyValue;
        [SerializeField] private Text roundLabel;
        [SerializeField] private Text roundValue;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Text turnValue;
        [SerializeField] private Image turnSwatch;

        public void Bind(Text luckyL, Text luckyV, Text unluckyL, Text unluckyV, Text roundL, Text roundV,
            Text turnL, Text turnV, Image swatch)
        {
            luckyLabel = luckyL; luckyValue = luckyV;
            unluckyLabel = unluckyL; unluckyValue = unluckyV;
            roundLabel = roundL; roundValue = roundV;
            turnLabel = turnL; turnValue = turnV;
            turnSwatch = swatch;
        }

        public void ApplyLabels(HudText text)
        {
            luckyLabel.text = text.luckyLabel;
            unluckyLabel.text = text.unluckyLabel;
            // R1 vocabulary: this counter is the turn cycle; "round" now names a whole race to the lucky number.
            roundLabel.text = string.IsNullOrEmpty(text.cycleLabel) ? text.roundLabel : text.cycleLabel;
            turnLabel.text = text.turnLabel;
        }

        public void Show(HudText text, int lucky, int unlucky, int round, int maxRounds, string turnName, Color turnColor) =>
            Show(text, lucky, unlucky > 0 ? unlucky.ToString() : null, round, maxRounds, turnName, turnColor);

        /// <param name="unluckyText">Unlucky number or zone ("12-14"); null or empty shows the empty value.</param>
        public void Show(HudText text, int lucky, string unluckyText, int round, int maxRounds, string turnName, Color turnColor)
        {
            luckyValue.text = lucky > 0 ? lucky.ToString() : text.emptyValue;
            unluckyValue.text = string.IsNullOrEmpty(unluckyText) ? text.emptyValue : unluckyText;
            roundValue.text = round > 0 ? string.Format(text.roundValue, round, maxRounds) : text.emptyValue;
            turnValue.text = string.IsNullOrEmpty(turnName) ? text.emptyValue : turnName;
            if (turnSwatch != null)
            {
                turnSwatch.enabled = !string.IsNullOrEmpty(turnName);
                turnSwatch.color = turnColor;
            }
        }

        public string LuckyText => luckyValue.text;
        public string UnluckyText => unluckyValue.text;
        public string RoundText => roundValue.text;
        public string TurnText => turnValue.text;
    }
}
