// ============================================================================
// HECTON-8 — PDAUpgradeSlot.cs
// UI element for a tool upgrade slot inside PDAToolDetailPanel.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class PDAUpgradeSlot : MonoBehaviour
    {
        [SerializeField] private Image upgradeIcon;
        [SerializeField] private Button removeButton;

        public void SetUpgrade(uint upgradeHash)
        {
            if (upgradeIcon != null)
            {
                upgradeIcon.gameObject.SetActive(true);
                // Assign appropriate icon based on hash, usually via a catalog lookup
            }

            if (removeButton != null)
            {
                removeButton.gameObject.SetActive(true);
            }
        }

        public void SetEmpty()
        {
            if (upgradeIcon != null)
            {
                upgradeIcon.gameObject.SetActive(false);
            }

            if (removeButton != null)
            {
                removeButton.gameObject.SetActive(false);
            }
        }

        public void OnRemoveClicked()
        {
            // Propagate removal intent to PDAToolDetailPanel
        }
    }
}
