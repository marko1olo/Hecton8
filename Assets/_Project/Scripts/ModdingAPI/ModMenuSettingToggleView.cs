using TMPro;
using UnityEngine;
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

        private void Awake()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(HandleValueChanged);
                toggle.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void OnDestroy()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(HandleValueChanged);
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

            if (toggle != null)
                toggle.SetIsOnWithoutNotify(view.BoolValue);
        }

        private void HandleValueChanged(bool value)
        {
            ModSettingsRegistry.TryApplyToggle(_modId, _settingName, value);
        }
    }
}
