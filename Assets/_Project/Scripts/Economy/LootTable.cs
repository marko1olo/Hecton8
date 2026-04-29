namespace Hecton8.Economy
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Compact normalized loot-table record shared by resource nodes, outcrops, and fauna drops.
    /// </summary>
    [Serializable]
    public struct LootTableEntry
    {
        [Tooltip("Stable shared item hash for the dropped result.")]
        public int ItemHashId;
        [Tooltip("Minimum stack count produced when this entry resolves.")]
        public ushort MinCount;
        [Tooltip("Maximum stack count produced when this entry resolves.")]
        public ushort MaxCount;
        [Tooltip("Relative weighted-selection value. Zero or below disables the entry.")]
        public float DropWeight;

        public bool IsValid => ItemHashId != 0 && MaxCount >= MinCount && DropWeight > 0f;
    }

    /// <summary>
    /// Authoring asset for deterministic weighted drop sets.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable_", menuName = "Hecton8/Economy/Loot Table", order = 125)]
    public sealed class LootTable : ScriptableObject
    {
        [Header("── Entries ─────────────────────────────────")]
        [Tooltip("Weighted drop entries used by outcrops, fauna, and salvage breakouts.")]
        [SerializeField] private LootTableEntry[] entries = Array.Empty<LootTableEntry>();

        public LootTableEntry[] Entries => entries;

        public int EntryCount => entries != null ? entries.Length : 0;

        public float ComputeTotalWeight()
        {
            if (entries == null || entries.Length == 0)
                return 0f;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].IsValid)
                    continue;

                totalWeight += Mathf.Max(0f, entries[i].DropWeight);
            }

            return totalWeight;
        }
    }
}
