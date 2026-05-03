using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton8.Modding
{
    /// <summary>
    /// Bindable slider row for a mod-owned float setting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModMenuSettingSliderView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text ownerLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private Slider slider;

        private string _modId;
        private string _settingName;
        private UnityAction<float> _cachedValueChangedAction;
        // COLD ALLOC: char[16] — cached slider value formatting buffer — owner: ModMenuSettingSliderView
        private readonly char[] _valueLabelBuffer = new char[16];

        private void Awake()
        {
            _cachedValueChangedAction = HandleValueChanged; // COLD ALLOC: UnityAction<float>[1] - cached mod slider listener - owner: ModMenuSettingSliderView
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(_cachedValueChangedAction);
                slider.onValueChanged.AddListener(_cachedValueChangedAction);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(_cachedValueChangedAction);
        }

        /// <summary>
        /// Binds the view to a mod setting snapshot.
        /// </summary>
        public void Bind(ModSettingView view)
        {
            _modId = view.ModId;
            _settingName = view.SettingName;

            if (label != null)
                label.SetText(view.DisplayName);

            if (ownerLabel != null)
                ownerLabel.SetText(view.ModId);

            if (slider != null)
            {
                slider.minValue = view.MinValue;
                slider.maxValue = view.MaxValue;
                slider.SetValueWithoutNotify(view.FloatValue);
            }

            if (valueLabel != null)
                SetValueLabel(view.FloatValue);
        }

        private void HandleValueChanged(float value)
        {
            if (valueLabel != null)
                SetValueLabel(value);

            ModSettingsRegistry.TryApplySlider(_modId, _settingName, value);
        }

        private void SetValueLabel(float value)
        {
            if (!value.TryFormat(_valueLabelBuffer.AsSpan(), out int charsWritten, "0.##", CultureInfo.InvariantCulture))
            {
                valueLabel.SetText("0");
                return;
            }

            valueLabel.SetCharArray(_valueLabelBuffer, 0, charsWritten);
        }
    }
}
