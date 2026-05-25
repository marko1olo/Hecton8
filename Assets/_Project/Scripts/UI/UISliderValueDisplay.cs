using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;

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
        private UnityAction<float> _cachedValueChangedAction;
        private float _cachedValue = float.MinValue;
        private char[] _resolvedTemplateChars;
        private int _resolvedTemplateLength;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            TryGetComponent(out _slider);
            _cachedValueChangedAction = OnValueChanged; // COLD ALLOC: UnityAction<float>[1] - cached slider value listener - owner: UISliderValueDisplay
            RebuildTemplateCache();
        }

        private void OnEnable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(_cachedValueChangedAction);
                UpdateDisplay(_slider.value);
            }
        }

        private void OnDisable()
        {
            if (_slider != null)
                _slider.onValueChanged.RemoveListener(_cachedValueChangedAction);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildTemplateCache();
        }
#endif

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
            LocNumericBuffer.Write(new System.ReadOnlySpan<char>(_resolvedTemplateChars, 0, _resolvedTemplateLength), LocNumericArg.Float(displayValue), out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            valueText.SetCharArray(buffer, 0, safeLength);
        }

        private void RebuildTemplateCache()
        {
            string safeFormat = string.IsNullOrEmpty(format) ? "{0:F0}" : format;
            string safeSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : suffix;
            int requiredLength = safeFormat.Length + safeSuffix.Length;
            if (_resolvedTemplateChars == null || _resolvedTemplateChars.Length < requiredLength)
                _resolvedTemplateChars = new char[requiredLength]; // COLD ALLOC: template char cache only when inspector format/suffix grows.

            int cursor = 0;
            for (int i = 0; i < safeFormat.Length; i++)
                _resolvedTemplateChars[cursor++] = safeFormat[i];

            for (int i = 0; i < safeSuffix.Length; i++)
                _resolvedTemplateChars[cursor++] = safeSuffix[i];

            _resolvedTemplateLength = cursor;
        }
    }
}
