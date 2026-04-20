using TMPro;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Simple bindable view for one mod row in the settings mod list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModMenuModEntryView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text reasonLabel;

        /// <summary>
        /// Applies the provided runtime descriptor to the row.
        /// </summary>
        public void Bind(ModRuntimeInfo info)
        {
            if (nameLabel != null)
                nameLabel.SetText(string.IsNullOrWhiteSpace(info.Metadata.Name) ? info.Metadata.Id : info.Metadata.Name);

            if (versionLabel != null)
                versionLabel.SetText(string.IsNullOrWhiteSpace(info.Metadata.Version) ? "0.0.0" : info.Metadata.Version);

            if (statusLabel != null)
                statusLabel.SetText(info.Status == ModLoadStatus.Active ? "Active" : "Disabled");

            if (reasonLabel != null)
                reasonLabel.SetText(string.IsNullOrWhiteSpace(info.StatusMessage) ? string.Empty : info.StatusMessage);
        }
    }
}
