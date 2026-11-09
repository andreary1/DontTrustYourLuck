using UnityEngine;

namespace DontTrustYourLuck.Presentation.Feedback
{
    /// <summary>
    /// Cosmetic idle motion for a few markers (active player, lucky sun, unlucky eye): bobs, spins and pulses
    /// a child so the parent can be positioned freely by other code. Only a handful exist in the scene.
    /// </summary>
    public sealed class BobAndSpin : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 2.4f;
        [SerializeField] private float spinDegreesPerSecond = 0f;
        [SerializeField] private float pulseAmount = 0.06f;
        [SerializeField] private float pulseSpeed = 2f;

        private Vector3 _baseScale = Vector3.one;
        private float _spin;

        public void Configure(Transform child, float bob, float spin, float pulse, float speed)
        {
            target = child; bobHeight = bob; spinDegreesPerSecond = spin; pulseAmount = pulse; pulseSpeed = speed;
            _baseScale = child != null ? child.localScale : Vector3.one;
        }

        private void Awake()
        {
            if (target != null) _baseScale = target.localScale;
        }

        private void Update()
        {
            if (target == null) return;
            float t = Time.time;
            target.localPosition = new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobHeight, 0f);
            if (spinDegreesPerSecond != 0f)
            {
                _spin = (_spin + spinDegreesPerSecond * Time.deltaTime) % 360f;
                target.localRotation = Quaternion.Euler(0f, _spin, 0f);
            }
            if (pulseAmount > 0f) target.localScale = _baseScale * (1f + Mathf.Sin(t * pulseSpeed) * pulseAmount);
        }
    }
}
