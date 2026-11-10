using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Short scrolling log of the latest messages (newest last).</summary>
    public sealed class MessagePanelView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [Min(1)] [SerializeField] private int maxLines = 7;

        private readonly Queue<string> _lines = new Queue<string>();
        private readonly StringBuilder _sb = new StringBuilder();

        public int LineCount => _lines.Count;
        public string LastMessage { get; private set; } = string.Empty;

        public void Bind(Text titleText, Text bodyText)
        {
            title = titleText;
            body = bodyText;
        }

        private bool _fitted;

        public void ApplyLabels(HudText text)
        {
            title.text = text.messagesTitle;
            FitBody();
        }

        /// <summary>
        /// Keeps the log inside its paper frame: a small inset off the dashed border, and long (wrapped) lines
        /// shrink the text instead of climbing over the title.
        /// </summary>
        private void FitBody()
        {
            if (_fitted || body == null) return;
            _fitted = true;
            var rt = body.rectTransform;
            rt.offsetMin += new Vector2(8f, 0f);
            rt.offsetMax -= new Vector2(4f, 0f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
            body.resizeTextMaxSize = body.fontSize;
            body.resizeTextMinSize = Mathf.Min(12, body.fontSize);
        }

        public void Add(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            LastMessage = message;
            _lines.Enqueue(message);
            while (_lines.Count > maxLines) _lines.Dequeue();
            Render();
        }

        public void Clear()
        {
            _lines.Clear();
            LastMessage = string.Empty;
            Render();
        }

        private void Render()
        {
            _sb.Clear();
            foreach (var line in _lines)
            {
                if (_sb.Length > 0) _sb.Append('\n');
                _sb.Append(line);
            }
            body.text = _sb.ToString();
        }
    }
}
