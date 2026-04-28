using System;
using System.Runtime.InteropServices;
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
    /// Immutable item-template record used by SOA inventory/runtime systems.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
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

        public uint HashID => hashID;
        public ItemCategoryMask CategoryMask => categoryMask;
        public float BaseDurability => baseDurability;
        public float WearMultiplier => wearMultiplier;
        public ushort MaxStackSize => maxStackSize;
        public ushort ProxyMeshIndex => proxyMeshIndex;
        public ushort IconAtlasIndex => iconAtlasIndex;
        public ushort HlodSilhouetteIndex => hlodSilhouetteIndex;
        public bool IsValid => hashID != 0u;
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

        public static bool IsInitialized => s_hashToIndex.IsCreated && s_templates.Length > 0;
        public static int Count => s_templates.Length;
        public static ReadOnlySpan<ItemTemplate> Templates => s_templates;

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

        public static void Clear()
        {
            if (s_hashToIndex.IsCreated)
            {
                s_hashToIndex.Dispose();
                s_hashToIndex = default;
            }

            s_templates = Array.Empty<ItemTemplate>();
        }
    }
}
