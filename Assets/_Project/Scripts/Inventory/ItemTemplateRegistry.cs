using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Inventory
{
    internal static class ItemTemplateRegistryLayout
    {
        public const int ItemTemplateStrideBytes = 64;
    }

    /// <summary>
    /// Authored category bitmask for contiguous item-template records.
    /// </summary>
    [Flags]
    public enum ItemCategoryMask : uint
    {
        None = 0u,
        Mineral = 1u << 0,
        Biological = 1u << 1,
        Tool = 1u << 2,
        Craft = 1u << 3,
        Food = 1u << 4,
        Tech = 1u << 5
    }

    /// <summary>
    /// Resolves item hashes into the 64-bit inventory/crafting material mask lane.
    /// </summary>
    public static class InventoryMaterialMask
    {
        public const int BitCount = 64;

        public static int ResolveBitIndex(int itemHashId)
        {
            return itemHashId & (BitCount - 1);
        }

        public static int ResolveBitIndex(uint itemHashId)
        {
            return (int)(itemHashId & (BitCount - 1));
        }

        public static ulong ResolveBit(int itemHashId)
        {
            return itemHashId == 0 ? 0UL : 1UL << ResolveBitIndex(itemHashId);
        }

        public static ulong ResolveBit(uint itemHashId)
        {
            return itemHashId == 0u ? 0UL : 1UL << ResolveBitIndex(itemHashId);
        }
    }

    /// <summary>
    /// Immutable item-template record used by SOA inventory/runtime systems.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = ItemTemplateRegistryLayout.ItemTemplateStrideBytes)]
    public struct ItemTemplate
    {
        [FieldOffset(0), SerializeField] private uint hashID;
        [FieldOffset(4), SerializeField] private ItemCategoryMask categoryMask;
        [FieldOffset(8), SerializeField] private float baseDurability;
        [FieldOffset(12), SerializeField] private float wearMultiplier;
        [FieldOffset(16), SerializeField] private uint vulnerabilityMask;
        [FieldOffset(20), SerializeField] private uint blueprintQuestFlagId;
        [FieldOffset(24), SerializeField] private float massKg;
        [FieldOffset(28), SerializeField] private float volumeM3;
        [FieldOffset(32), SerializeField] private ushort maxStackSize;
        [FieldOffset(34), SerializeField] private ushort proxyMeshIndex;
        [FieldOffset(36), SerializeField] private ushort iconAtlasIndex;
        [FieldOffset(38), SerializeField] private ushort hlodSilhouetteIndex;
        [FieldOffset(40), SerializeField] private byte audioMaterialId;
        [FieldOffset(41), SerializeField] private byte physicsMaterialTag;
        [FieldOffset(42), SerializeField] private ushort _reserved0;
        [FieldOffset(44)] private byte _pad00;
        [FieldOffset(45)] private byte _pad01;
        [FieldOffset(46)] private byte _pad02;
        [FieldOffset(47)] private byte _pad03;
        [FieldOffset(48)] private byte _pad04;
        [FieldOffset(49)] private byte _pad05;
        [FieldOffset(50)] private byte _pad06;
        [FieldOffset(51)] private byte _pad07;
        [FieldOffset(52)] private byte _pad08;
        [FieldOffset(53)] private byte _pad09;
        [FieldOffset(54)] private byte _pad10;
        [FieldOffset(55)] private byte _pad11;
        [FieldOffset(56)] private byte _pad12;
        [FieldOffset(57)] private byte _pad13;
        [FieldOffset(58)] private byte _pad14;
        [FieldOffset(59)] private byte _pad15;
        [FieldOffset(60)] private byte _pad16;
        [FieldOffset(61)] private byte _pad17;
        [FieldOffset(62)] private byte _pad18;
        [FieldOffset(63)] private byte _pad19;

        public ItemTemplate(
            uint hashID,
            ItemCategoryMask categoryMask,
            float baseDurability,
            float wearMultiplier,
            ushort maxStackSize,
            ushort proxyMeshIndex,
            ushort iconAtlasIndex,
            ushort hlodSilhouetteIndex,
            uint vulnerabilityMask,
            byte audioMaterialId,
            byte physicsMaterialTag,
            float massKg,
            float volumeM3,
            uint blueprintQuestFlagId = 0u)
        {
            this = default;
            this.hashID = hashID;
            this.categoryMask = categoryMask;
            this.baseDurability = baseDurability;
            this.wearMultiplier = wearMultiplier;
            this.maxStackSize = maxStackSize;
            this.proxyMeshIndex = proxyMeshIndex;
            this.iconAtlasIndex = iconAtlasIndex;
            this.hlodSilhouetteIndex = hlodSilhouetteIndex;
            this.vulnerabilityMask = vulnerabilityMask;
            this.audioMaterialId = audioMaterialId;
            this.physicsMaterialTag = physicsMaterialTag;
            _reserved0 = 0;
            this.blueprintQuestFlagId = blueprintQuestFlagId;
            this.massKg = massKg;
            this.volumeM3 = volumeM3;
        }

        public uint HashID => hashID;
        public ItemCategoryMask CategoryMask => categoryMask;
        public float BaseDurability => baseDurability;
        public float WearMultiplier => wearMultiplier;
        public ushort MaxStackSize => maxStackSize;
        public ushort ProxyMeshIndex => proxyMeshIndex;
        public ushort IconAtlasIndex => iconAtlasIndex;
        public ushort HlodSilhouetteIndex => hlodSilhouetteIndex;
        public uint VulnerabilityMask => vulnerabilityMask;
        public uint BlueprintQuestFlagId => blueprintQuestFlagId;
        public byte AudioMaterialId => audioMaterialId;
        public byte PhysicsMaterialTag => physicsMaterialTag;
        public float MassKg => massKg;
        public float VolumeM3 => volumeM3;
        public bool IsValid => hashID != 0u;

        public bool SupportsCapability(uint capabilityMask)
        {
            return capabilityMask != 0u && (vulnerabilityMask & capabilityMask) != 0u;
        }
    }

    /// <summary>
    /// Authored template asset consumed by bootstrap/runtime registry wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemTemplateRegistry", menuName = "Hecton/Inventory/Item Template Registry", order = 110)]
    public sealed class ItemTemplateRegistryAsset : ScriptableObject
    {
        [SerializeField] private ItemTemplate[] templates = Array.Empty<ItemTemplate>();

        public ItemTemplate[] Templates => templates;

        public void ApplyRuntimeSnapshot()
        {
            ItemTemplateRegistry.Configure(templates);
        }
    }

    /// <summary>
    /// Runtime lookup registry for contiguous item-template records.
    /// </summary>
    public static class ItemTemplateRegistry
    {
        private static ItemTemplate[] s_templates = Array.Empty<ItemTemplate>();
        private static uint s_revision;

        public static bool IsInitialized => s_templates.Length > 0;
        public static int Count => s_templates.Length;
        public static ReadOnlySpan<ItemTemplate> Templates => s_templates;
        public static uint Revision => s_revision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }

        public static void Configure(ItemTemplate[] templates)
        {
            Clear();

            if (templates == null || templates.Length == 0)
            {
                s_templates = Array.Empty<ItemTemplate>();
                return;
            }

            // COLD ALLOC: ItemTemplate[templates.Length] — runtime template snapshot copied from authored registry asset — owner: ItemTemplateRegistry
            s_templates = new ItemTemplate[templates.Length];
            Array.Copy(templates, s_templates, templates.Length);
        }

        public static bool TryGetIndex(uint hashID, out int index)
        {
            index = -1;
            if (hashID == 0u)
                return false;

            for (int i = 0; i < s_templates.Length; i++)
            {
                if (s_templates[i].HashID != hashID)
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        public static bool TryGetIndex(int hashID, out int index)
        {
            return TryGetIndex(unchecked((uint)hashID), out index);
        }

        public static bool TryGetTemplate(uint hashID, out ItemTemplate template)
        {
            template = default;
            if (!TryGetIndex(hashID, out int index) || (uint)index >= (uint)s_templates.Length)
                return false;

            template = s_templates[index];
            return template.IsValid;
        }

        public static bool TryGetTemplate(int hashID, out ItemTemplate template)
        {
            return TryGetTemplate(unchecked((uint)hashID), out template);
        }

        public static bool IsBlueprintViewable(uint hashID)
        {
            return TryGetTemplate(hashID, out ItemTemplate template) &&
                   IsBlueprintViewable(in template);
        }

        public static bool IsBlueprintViewable(int hashID)
        {
            return IsBlueprintViewable(unchecked((uint)hashID));
        }

        public static bool IsBlueprintViewable(in ItemTemplate template)
        {
            if (!template.IsValid)
                return false;

            uint requiredFlag = template.BlueprintQuestFlagId;
            if (requiredFlag == 0u)
                return true;

            IQuestSystem questSystem = GlobalRegistry.QuestSystem;
            return questSystem != null && questSystem.GetFlag(requiredFlag);
        }

        public static void Clear()
        {
            s_templates = Array.Empty<ItemTemplate>();
            unchecked
            {
                s_revision++;
            }
        }
    }
}
