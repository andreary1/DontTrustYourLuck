using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation
{
    /// <summary>
    /// Sprint 0 smoke test: proves that UI input reaches the player in a Web build.
    /// Contains no gameplay rules; remove once the real HUD exists.
    /// </summary>
    public sealed class SmokeTestButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Text _label;

        private int _clickCount;

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            _clickCount++;
            if (_label != null)
            {
                _label.text = $"Clicked {_clickCount}x";
            }

            Debug.Log($"[SmokeTest] Button clicked {_clickCount} time(s).");
        }
    }
}
