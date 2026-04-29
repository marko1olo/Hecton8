using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authored xenobiology progression graph driven by completed scientific scans.
    /// </summary>
    [CreateAssetMenu(fileName = "XenoBiologyTree", menuName = "Hecton/Gameplay/Xeno Biology Tree", order = 116)]
    public sealed class XenoBiologyTree : ScriptableObject
    {
        [Serializable]
        public struct Node
        {
            [Tooltip("Stable designer-facing node identifier used for audits and tooling.")]
            [SerializeField] private string nodeId;

            [Tooltip("Bit-packed prerequisite mask referencing previously unlocked node indices.")]
            [SerializeField] private ulong prerequisiteNodeBits;

            [Tooltip("Stable scan entry ID counted toward this node. Example: research.unknown_spore.")]
            [SerializeField] private string requiredScanEntryId;

            [Tooltip("How many completed scans of the required entry are needed before the node resolves.")]
            [SerializeField, Min(1)] private ushort requiredScanCount;

            [Tooltip("Packed lore bits unlocked when this node resolves.")]
            [SerializeField] private ulong loreUnlockBits;

            [Tooltip("Stable reward item hash granted or referenced when the node resolves.")]
            [SerializeField] private int rewardItemHash;

            [Tooltip("Authored quest ID activated when this node resolves.")]
            [SerializeField] private string unlockQuestId;

            public string NodeId => nodeId;
            public ulong PrerequisiteNodeBits => prerequisiteNodeBits;
            public string RequiredScanEntryId => requiredScanEntryId;
            public ushort RequiredScanCount => requiredScanCount;
            public ulong LoreUnlockBits => loreUnlockBits;
            public int RewardItemHash => rewardItemHash;
            public string UnlockQuestId => unlockQuestId;
        }

        [Header("── Biology Nodes ──────────────────")]
        [Tooltip("Ordered xenobiology node bank. Node bit index == array index.")]
        [SerializeField] private Node[] nodes = Array.Empty<Node>();

        /// <summary>Number of authored nodes in this biology tree.</summary>
        public int NodeCount => nodes != null ? nodes.Length : 0;

        /// <summary>Resolve one authored node without exposing the backing array for mutation.</summary>
        public bool TryGetNode(int index, out Node node)
        {
            if (nodes == null || (uint)index >= (uint)nodes.Length)
            {
                node = default;
                return false;
            }

            node = nodes[index];
            return true;
        }
    }
}
