// ============================================================================
// HECTON-8 — PDAToolDetailPanel.cs
// Right side detail panel for the selected tool inside PDAToolsTab.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class PDAToolDetailPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI toolNameText;
        [SerializeField] private TextMeshProUGUI tierBadgeText;
        [SerializeField] private Image durabilityBar;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Transform upgradeSlotsContainer;

        public void SetTool(uint itemHash)
        {
            UpdateUI(itemHash);
        }

        public void UpdateUI(uint itemHash)
        {
            if (toolNameText != null) toolNameText.text = $"Tool {itemHash}";
            if (tierBadgeText != null) tierBadgeText.text = "TIER 1";
            if (durabilityBar != null)
            {
                durabilityBar.fillAmount = 1f;
                durabilityBar.color = Color.green;
            }
            if (statsText != null) statsText.text = "EFFICIENCY: 100%\nSPEED: 1.0\nENERGY: 10/s";
        }
    }
}
