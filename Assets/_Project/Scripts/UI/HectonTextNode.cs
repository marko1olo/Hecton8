using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Static zero-alloc registry for runtime TMP text nodes.
    /// </summary>
    public static class TMP_TextRegistry
    {
        // COLD ALLOC: List[512] — registered runtime TMP text nodes for staged font swapping — owner: TMP_TextRegistry
        private static readonly List<HectonTextNode> s_nodes = new List<HectonTextNode>(512);

        /// <summary>Current registered TMP node count.</summary>
        public static int Count => s_nodes.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_nodes.Clear();
        }

        /// <summary>Returns the node at the provided registry index.</summary>
        public static HectonTextNode GetNodeAt(int index)
        {
            return index >= 0 && index < s_nodes.Count ? s_nodes[index] : null;
        }

        /// <summary>
        /// Ensures the provided TMP text has a registry node attached.
        /// </summary>
        public static void EnsureRegistered(TMP_Text text)
        {
            if (text == null)
                return;

            if (!text.TryGetComponent(out HectonTextNode _))
                text.gameObject.AddComponent<HectonTextNode>(); // COLD ALLOC: HectonTextNode[1] — TMP registry node for staged font swapping — owner: TMP_TextRegistry
        }

        internal static void Register(HectonTextNode node)
        {
            if (node == null || node.RegistryIndex >= 0)
                return;

            node.RegistryIndex = s_nodes.Count;
            s_nodes.Add(node);
        }

        internal static void Unregister(HectonTextNode node)
        {
            if (node == null)
                return;

            int index = node.RegistryIndex;
            if (index < 0 || index >= s_nodes.Count)
            {
                node.RegistryIndex = -1;
                return;
            }

            int lastIndex = s_nodes.Count - 1;
            HectonTextNode tail = s_nodes[lastIndex];
            s_nodes[index] = tail;
            if (tail != null)
                tail.RegistryIndex = index;

            s_nodes.RemoveAt(lastIndex);
            node.RegistryIndex = -1;
        }
    }

    /// <summary>
    /// Registry node attached to UI TMP text components so font streaming can avoid global scene scans.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class HectonTextNode : MonoBehaviour
    {
        private TMP_Text _text;

        /// <summary>Cached TMP text component.</summary>
        public TMP_Text TextComponent => _text;

        internal int RegistryIndex { get; set; } = -1;

        private void Awake()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            TMP_TextRegistry.Register(this);
        }

        private void OnDestroy()
        {
            TMP_TextRegistry.Unregister(this);
        }
    }
}
