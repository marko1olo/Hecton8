namespace Hecton8.Inventory
{
    using Hecton.Localization;
    using Hecton8.Items;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    /// <summary>
    /// Passive prefab metadata for world loot/container item identity.
    /// Runtime inventory truth stays in SOA item tables; this component is cold identity only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Inventory/Item Node Data")]
    public sealed class ItemNodeData : MonoBehaviour
    {
        [SerializeField] private int itemHashId;
        [SerializeField] private float baseWeightKg = 0.05f;
        [SerializeField] private float baseVolumeM3 = 0.0005f;
        [SerializeField] private ushort stackCapacity = 1;
        [SerializeField] private ushort flags;
        [SerializeField] private byte category;
        [SerializeField] private byte resourceFamily;

        public int ItemHashId => itemHashId;
        public uint ItemHashU32 => unchecked((uint)itemHashId);
        public float BaseWeightKg => baseWeightKg;
        public float BaseVolumeM3 => baseVolumeM3;
        public ushort StackCapacity => stackCapacity;
        public ushort Flags => flags;
        public byte Category => category;
        public byte ResourceFamily => resourceFamily;
        public bool IsValid =>
            itemHashId != 0 &&
            IsFinite(baseWeightKg) &&
            baseWeightKg > 0f &&
            IsFinite(baseVolumeM3) &&
            baseVolumeM3 > 0f &&
            stackCapacity != 0;

        public bool TryBuildStackLimit(out InventoryStackLimitDTO stackLimit)
        {
            stackLimit = default;
            if (!IsValid)
                return false;

            stackLimit.ItemHashID = unchecked((uint)itemHashId);
            stackLimit.MaxStack = stackCapacity;
            stackLimit.Flags = flags;
            stackLimit.Reserved0 = 0u;
            return true;
        }

        public bool TryBuildPhysicalConstants(out ItemPhysicalConstantsDTO constants)
        {
            constants = default;
            if (!IsValid)
                return false;

            constants.ItemHash = unchecked((uint)itemHashId);
            constants.MassKg = baseWeightKg;
            constants.VolumeLiters = baseVolumeM3 * 1000f;
            constants.MaxStack = stackCapacity;
            constants.BaseDurability01 = 1f;
            constants.Flags = flags;
            constants.Reserved0 = 0u;
            constants.Reserved1 = 0u;
            return true;
        }

        public static bool ValidateStackLimitDtoLayout()
        {
            int size = UnsafeUtility.SizeOf<InventoryStackLimitDTO>();
            return size == 16 && (size & 7) == 0;
        }

        public static bool ValidatePhysicalConstantsDtoLayout()
        {
            int size = UnsafeUtility.SizeOf<ItemPhysicalConstantsDTO>();
            return size == 32 && (size & 7) == 0;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            if (!IsFinite(baseWeightKg) || baseWeightKg < 0.05f)
                baseWeightKg = 0.05f;

            if (!IsFinite(baseVolumeM3) || baseVolumeM3 < 0.0005f)
                baseVolumeM3 = 0.0005f;

            if (stackCapacity == 0)
                stackCapacity = 1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(ItemData itemData, ushort authoredFlags)
        {
            if (itemData == null)
            {
                ConfigureEditorBake(0, 0.05f, 0.0005f, 1, 0, 0, authoredFlags);
                return;
            }

            int stableHash = itemData.PersistentHashId != 0
                ? itemData.PersistentHashId
                : LocHash.Compute(itemData.PersistentId);
            ConfigureEditorBake(
                stableHash,
                itemData.MassKg,
                itemData.VolumeM3,
                (ushort)Mathf.Clamp(itemData.maxStack, 1, ushort.MaxValue),
                (byte)itemData.category,
                (byte)itemData.resourceFamily,
                authoredFlags);
        }

        public void ConfigureEditorBake(
            int authoredItemHashId,
            float authoredBaseWeightKg,
            float authoredBaseVolumeM3,
            ushort authoredStackCapacity,
            byte authoredCategory,
            byte authoredResourceFamily,
            ushort authoredFlags)
        {
            itemHashId = authoredItemHashId;
            baseWeightKg = authoredBaseWeightKg;
            baseVolumeM3 = authoredBaseVolumeM3;
            stackCapacity = authoredStackCapacity;
            category = authoredCategory;
            resourceFamily = authoredResourceFamily;
            flags = authoredFlags;
            SanitizeSerializedState();
        }
#endif
    }
}
