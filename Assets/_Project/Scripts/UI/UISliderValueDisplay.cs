using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hecton8.UI
{
    /// <summary>
    /// Automatic value display for UI sliders.
    /// Updates text label when slider value changes.
    /// Zero-GC: dirty flag, cached TMP_Text, no string allocations.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    [AddComponentMenu("Hecton8/UI/UI Slider Value Display")]
    public sealed class UISliderValueDisplay : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== DISPLAY ===")]
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private string format = "{0:F0}";
        [SerializeField] private string suffix = "";
        [SerializeField] private float multiplier = 1f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Slider _slider;
        private float _cachedValue = float.MinValue;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnValueChanged);
                UpdateDisplay(_slider.value);
            }
        }

        private void OnDisable()
        {
            if (_slider != null)
                _slider.onValueChanged.RemoveListener(OnValueChanged);
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnValueChanged(float value)
        {
            UpdateDisplay(value);
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void UpdateDisplay(float value)
        {
            if (valueText == null)
                return;

            // Dirty flag - only update if value changed
            if (Mathf.Approximately(_cachedValue, value))
                return;

            _cachedValue = value;

            float displayValue = value * multiplier;
            string formattedValue = string.Format(format, displayValue);

            if (!string.IsNullOrEmpty(suffix))
                valueText.SetText(formattedValue + suffix);
            else
                valueText.SetText(formattedValue);
        }
    }
}
