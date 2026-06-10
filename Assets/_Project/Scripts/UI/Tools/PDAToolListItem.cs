// ============================================================================
// HECTON-8 — PDAToolListItem.cs
// UI element for a tool inside PDAToolsTab.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class PDAToolListItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image durabilityBar;
        [SerializeField] private GameObject brokenIndicator;

        private uint _itemHash;
        private int _inventoryIndex;

        public void SetTool(uint itemHash, int inventoryIndex)
        {
            _itemHash = itemHash;
            _inventoryIndex = inventoryIndex;

            if (nameText != null) nameText.text = $"Tool {_itemHash}";
            if (durabilityBar != null) durabilityBar.fillAmount = 1f;
            if (brokenIndicator != null) brokenIndicator.SetActive(false);
        }

        public void OnClick()
        {
            // Propagate click to detail panel
        }
    }
}
