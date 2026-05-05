using System.Collections.Generic;
using Hecton.Localization;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Immutable snapshot of one registered TMP text owner.
    /// </summary>
    public readonly struct TMP_TextEntry
    {
        public TMP_TextEntry(TMP_Text text, int hierarchyHash, int localizationKeyHash, LocLayer layer, bool isUserInput)
        {
            Text = text;
            HierarchyHash = hierarchyHash;
            LocalizationKeyHash = localizationKeyHash;
            Layer = layer;
            IsUserInput = isUserInput;
        }

        public TMP_Text Text { get; }
        public int HierarchyHash { get; }
        public int LocalizationKeyHash { get; }
        public LocLayer Layer { get; }
        public bool IsUserInput { get; }
        public bool HasLocalizationKey => LocalizationKeyHash != 0;
    }

    /// <summary>
    /// Zero-allocation TMP registry keyed by baked hierarchy hashes.
    /// </summary>
    public static class TMP_TextRegistry
    {
        // COLD ALLOC: HectonTextNode[512] — TMP registry node backing store — owner: TMP_TextRegistry
        private static HectonTextNode[] s_nodes = new HectonTextNode[512];
        // COLD ALLOC: TMP_TextEntry[512] — TMP registry entry backing store — owner: TMP_TextRegistry
        private static TMP_TextEntry[] s_entries = new TMP_TextEntry[512];
        // COLD ALLOC: Dictionary[512] — hierarchy hash to TMP registry index map — owner: TMP_TextRegistry
        private static readonly Dictionary<int, int> s_indicesByHash = new Dictionary<int, int>(512);
        private static int s_count;
        private static TMP_FontAsset s_terminalFontAsset;

        /// <summary>
        /// Active entry count.
        /// </summary>
        public static int Count => s_count;

        /// <summary>
        /// Terminal/BIOS font cached by the registry for zero-scan HUD font swaps.
        /// </summary>
        public static TMP_FontAsset TerminalFontAsset => s_terminalFontAsset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_count; i++)
            {
                s_nodes[i] = null;
                s_entries[i] = default;
            }

            s_count = 0;
            s_indicesByHash.Clear();
            s_terminalFontAsset = null;
        }

        /// <summary>
        /// Cache the terminal font variant before BIOS mode needs to swap registered HUD labels.
        /// </summary>
        public static TMP_FontAsset PrewarmTerminalFont(TMP_FontAsset preferredTerminalFont)
        {
            if (LocalizedFontResolver.IsFontReady(preferredTerminalFont))
            {
                s_terminalFontAsset = preferredTerminalFont;
                return s_terminalFontAsset;
            }

            s_terminalFontAsset = LocalizedFontResolver.ResolveBiosFallbackFont();
            return s_terminalFontAsset;
        }

        /// <summary>
        /// Return the node at the requested dense registry index.
        /// </summary>
        public static HectonTextNode GetNodeAt(int index)
        {
            return index >= 0 && index < s_count ? s_nodes[index] : null;
        }

        /// <summary>
        /// Return the entry snapshot at the requested dense registry index.
        /// </summary>
        public static TMP_TextEntry GetEntryAt(int index)
        {
            return index >= 0 && index < s_count ? s_entries[index] : default;
        }

        /// <summary>
        /// O(1) lookup by deterministic hierarchy hash.
        /// </summary>
        public static bool TryGetText(int hierarchyHash, out TMP_Text text)
        {
            if (hierarchyHash != 0 &&
                s_indicesByHash.TryGetValue(hierarchyHash, out int index) &&
                index >= 0 &&
                index < s_count)
            {
                text = s_entries[index].Text;
                return text != null;
            }

            text = null;
            return false;
        }

        /// <summary>
        /// Ensure the supplied TMP owner has a registry node.
        /// </summary>
        public static void EnsureRegistered(TMP_Text text)
        {
            if (text == null)
                return;

            if (!text.TryGetComponent(out HectonTextNode _))
                text.gameObject.AddComponent<HectonTextNode>(); // COLD ALLOC: HectonTextNode[1] — TMP registry node for staged font swap ownership — owner: TMP_TextRegistry
        }

        /// <summary>
        /// Update registry metadata for a TMP text owner after runtime creation.
        /// </summary>
        public static void SetMetadata(TMP_Text text, int localizationKeyHash, LocLayer layer, bool isUserInput = false)
        {
            if (text == null)
                return;

            EnsureRegistered(text);
            if (!text.TryGetComponent(out HectonTextNode node))
                return;

            node.SetMetadata(localizationKeyHash, layer, isUserInput);
        }

        internal static void Register(HectonTextNode node)
        {
            if (node == null || node.RegistryIndex >= 0)
                return;

            EnsureCapacity(s_count + 1);
            int index = s_count;
            s_count++;

            node.RegistryIndex = index;
            s_nodes[index] = node;
            TMP_TextEntry entry = BuildEntry(node);
            s_entries[index] = entry;
            if (entry.HierarchyHash != 0)
                s_indicesByHash[entry.HierarchyHash] = index;
        }

        internal static void Unregister(HectonTextNode node)
        {
            if (node == null)
                return;

            int index = node.RegistryIndex;
            if (index < 0 || index >= s_count)
            {
                node.RegistryIndex = -1;
                return;
            }

            int lastIndex = s_count - 1;
            TMP_TextEntry removedEntry = s_entries[index];
            if (removedEntry.HierarchyHash != 0 &&
                s_indicesByHash.TryGetValue(removedEntry.HierarchyHash, out int mappedIndex) &&
                mappedIndex == index)
            {
                s_indicesByHash.Remove(removedEntry.HierarchyHash);
            }

            HectonTextNode tailNode = s_nodes[lastIndex];
            TMP_TextEntry tailEntry = s_entries[lastIndex];

            s_nodes[lastIndex] = null;
            s_entries[lastIndex] = default;
            s_count = lastIndex;
            node.RegistryIndex = -1;

            if (index == lastIndex)
                return;

            s_nodes[index] = tailNode;
            s_entries[index] = tailEntry;
            if (tailNode != null)
                tailNode.RegistryIndex = index;

            if (tailEntry.HierarchyHash != 0)
                s_indicesByHash[tailEntry.HierarchyHash] = index;
        }

        internal static void Refresh(HectonTextNode node)
        {
            if (node == null)
                return;

            int index = node.RegistryIndex;
            if (index < 0 || index >= s_count)
                return;

            TMP_TextEntry previousEntry = s_entries[index];
            if (previousEntry.HierarchyHash != 0 &&
                s_indicesByHash.TryGetValue(previousEntry.HierarchyHash, out int mappedIndex) &&
                mappedIndex == index)
            {
                s_indicesByHash.Remove(previousEntry.HierarchyHash);
            }

            TMP_TextEntry entry = BuildEntry(node);
            s_entries[index] = entry;
            if (entry.HierarchyHash != 0)
                s_indicesByHash[entry.HierarchyHash] = index;
        }

        private static TMP_TextEntry BuildEntry(HectonTextNode node)
        {
            TMP_Text text = node.TextComponent;
            return new TMP_TextEntry(
                text,
                node.HierarchyHash,
                node.LocalizationKeyHash,
                node.Layer,
                node.IsUserInput);
        }

        private static void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= s_nodes.Length)
                return;

            int newCapacity = Mathf.Max(requiredCapacity, s_nodes.Length << 1);
            HectonTextNode[] resizedNodes = new HectonTextNode[newCapacity]; // COLD ALLOC: HectonTextNode[newCapacity] — expanded TMP registry node store — owner: TMP_TextRegistry
            TMP_TextEntry[] resizedEntries = new TMP_TextEntry[newCapacity]; // COLD ALLOC: TMP_TextEntry[newCapacity] — expanded TMP registry entry store — owner: TMP_TextRegistry
            for (int i = 0; i < s_count; i++)
            {
                resizedNodes[i] = s_nodes[i];
                resizedEntries[i] = s_entries[i];
            }

            s_nodes = resizedNodes;
            s_entries = resizedEntries;
        }
    }
}
