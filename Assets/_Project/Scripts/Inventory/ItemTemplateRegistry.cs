using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Inventory
{
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 44)]
    public struct ItemTemplate
    {
        [SerializeField] private uint hashID;
        [SerializeField] private ItemCategoryMask categoryMask;
        [SerializeField] private float baseDurability;
        [SerializeField] private float wearMultiplier;
        [SerializeField] private ushort maxStackSize;
        [SerializeField] private ushort proxyMeshIndex;
        [SerializeField] private ushort iconAtlasIndex;
        [SerializeField] private ushort hlodSilhouetteIndex;
        [SerializeField] private uint vulnerabilityMask;
        [SerializeField] private byte audioMaterialId;
        [SerializeField] private byte physicsMaterialTag;
        [SerializeField] private ushort _reserved0;
        [SerializeField] private uint blueprintQuestFlagId;
        [SerializeField] private float massKg;
        [SerializeField] private float volumeM3;

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
        private static NativeHashMap<uint, int> s_hashToIndex;
        private static ItemTemplate[] s_templates = Array.Empty<ItemTemplate>();
        private static uint s_revision;

        public static bool IsInitialized => s_hashToIndex.IsCreated && s_templates.Length > 0;
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
            s_hashToIndex = new NativeHashMap<uint, int>(templates.Length, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeHashMap(
                s_hashToIndex,
                nameof(ItemTemplateRegistry),
                nameof(s_hashToIndex),
                NativeAllocationLifetime.Session);

            for (int index = 0; index < s_templates.Length; index++)
            {
                ItemTemplate template = s_templates[index];
                if (!template.IsValid)
                    continue;

                s_hashToIndex[template.HashID] = index;
            }
        }

        public static bool TryGetIndex(uint hashID, out int index)
        {
            index = -1;
            return hashID != 0u &&
                   s_hashToIndex.IsCreated &&
                   s_hashToIndex.TryGetValue(hashID, out index);
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
            if (s_hashToIndex.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ItemTemplateRegistry), nameof(s_hashToIndex));
                s_hashToIndex.Dispose();
                s_hashToIndex = default;
            }

            s_templates = Array.Empty<ItemTemplate>();
            unchecked
            {
                s_revision++;
            }
        }
    }
}
