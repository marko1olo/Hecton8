using System;
using Hecton8.Inventory;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authored scientific scan contract for staged lore unlocks and milestone rewards.
    /// </summary>
    [CreateAssetMenu(fileName = "ResearchDataTemplate", menuName = "Hecton/Gameplay/Research Data Template", order = 115)]
    public sealed class ResearchDataTemplate : ScriptableObject
    {
        [Header("── Scan Contract ──────────────────")]
        [Tooltip("Total scan duration required before the authored target completes research.")]
        [SerializeField, Min(0.5f)] private float scanDuration = 3f;

        [Tooltip("Packed lore-bit masks unlocked at each research milestone. Expected order: 25%, 50%, 100%.")]
        [SerializeField] private ulong[] loreUnlockChain = Array.Empty<ulong>();

        [Tooltip("Stable item hash used for milestone reward and visor hologram proxy lookup.")]
        [SerializeField] private int rewardItemHash;

        /// <summary>
        /// Total scan duration required to complete this research target.
        /// </summary>
        public float ScanDuration => Mathf.Max(0.5f, scanDuration);

        /// <summary>
        /// Stable item hash used for reward routing and proxy-mesh lookup.
        /// </summary>
        public int RewardItemHash => rewardItemHash;

        /// <summary>
        /// Number of authored lore-unlock milestones in this template.
        /// </summary>
        public int LoreUnlockStageCount => loreUnlockChain != null ? loreUnlockChain.Length : 0;

        /// <summary>
        /// Resolved item-template proxy mesh index for visor hologram rendering.
        /// Returns -1 when no reward hash or template exists.
        /// </summary>
        public int HologramProxyMeshIndex
        {
            get
            {
                if (rewardItemHash == 0 || !ItemTemplateRegistry.TryGetTemplate(rewardItemHash, out ItemTemplate template))
                    return -1;

                return template.ProxyMeshIndex;
            }
        }

        /// <summary>
        /// Resolve the packed lore mask for one authored milestone.
        /// </summary>
        public bool TryGetLoreUnlockMask(int stageIndex, out ulong mask)
        {
            if (loreUnlockChain == null || (uint)stageIndex >= (uint)loreUnlockChain.Length)
            {
                mask = 0UL;
                return false;
            }

            mask = loreUnlockChain[stageIndex];
            return mask != 0UL;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (scanDuration < 0.5f)
                scanDuration = 0.5f;
        }
#endif
    }
}
