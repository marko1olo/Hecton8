using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton8.Modding
{
    /// <summary>
    /// Bindable toggle row for a mod-owned boolean setting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModMenuSettingToggleView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text ownerLabel;
        [SerializeField] private Toggle toggle;

        private string _modId;
        private string _settingName;
        private UnityAction<bool> _cachedValueChangedAction;

        private void Awake()
        {
            _cachedValueChangedAction = HandleValueChanged; // COLD ALLOC: UnityAction<bool>[1] - cached mod toggle listener - owner: ModMenuSettingToggleView
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(_cachedValueChangedAction);
                toggle.onValueChanged.AddListener(_cachedValueChangedAction);
            }
        }

        private void OnDestroy()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(_cachedValueChangedAction);
        }

        /// <summary>
        /// Binds the view to a mod setting snapshot.
        /// </summary>
        public void Bind(ModSettingView view)
        {
            _modId = view.ModId;
            _settingName = view.SettingName;

            if (label != null)
                Hecton8.UI.TmpTextNoAlloc.Set(label, view.DisplayName);

            if (ownerLabel != null)
                Hecton8.UI.TmpTextNoAlloc.Set(ownerLabel, view.ModId);

            if (toggle != null)
                toggle.SetIsOnWithoutNotify(view.BoolValue);
        }

        private void HandleValueChanged(bool value)
        {
            ModSettingsRegistry.TryApplyToggle(_modId, _settingName, value);
        }
    }
}
