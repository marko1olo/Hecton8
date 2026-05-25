using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Marks renderers that should restore the dry scene color after the ocean underwater fog pass has already run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonDryVolumeStencilSource : MonoBehaviour
    {
        [Serializable]
        private struct RendererEntry
        {
            [Tooltip("Renderer that writes the dry stencil silhouette.")]
            public Renderer renderer;

            [Tooltip("Cached sub-mesh count so the render feature can draw every visible sub-mesh without allocating.")]
            public int subMeshCount;
        }

        private const int MaxActiveSources = 64;
        private static HectonDryVolumeStencilSource s_activeSource0;
        private static HectonDryVolumeStencilSource s_activeSource1;
        private static HectonDryVolumeStencilSource s_activeSource2;
        private static HectonDryVolumeStencilSource s_activeSource3;
        private static HectonDryVolumeStencilSource s_activeSource4;
        private static HectonDryVolumeStencilSource s_activeSource5;
        private static HectonDryVolumeStencilSource s_activeSource6;
        private static HectonDryVolumeStencilSource s_activeSource7;
        private static HectonDryVolumeStencilSource s_activeSource8;
        private static HectonDryVolumeStencilSource s_activeSource9;
        private static HectonDryVolumeStencilSource s_activeSource10;
        private static HectonDryVolumeStencilSource s_activeSource11;
        private static HectonDryVolumeStencilSource s_activeSource12;
        private static HectonDryVolumeStencilSource s_activeSource13;
        private static HectonDryVolumeStencilSource s_activeSource14;
        private static HectonDryVolumeStencilSource s_activeSource15;
        private static HectonDryVolumeStencilSource s_activeSource16;
        private static HectonDryVolumeStencilSource s_activeSource17;
        private static HectonDryVolumeStencilSource s_activeSource18;
        private static HectonDryVolumeStencilSource s_activeSource19;
        private static HectonDryVolumeStencilSource s_activeSource20;
        private static HectonDryVolumeStencilSource s_activeSource21;
        private static HectonDryVolumeStencilSource s_activeSource22;
        private static HectonDryVolumeStencilSource s_activeSource23;
        private static HectonDryVolumeStencilSource s_activeSource24;
        private static HectonDryVolumeStencilSource s_activeSource25;
        private static HectonDryVolumeStencilSource s_activeSource26;
        private static HectonDryVolumeStencilSource s_activeSource27;
        private static HectonDryVolumeStencilSource s_activeSource28;
        private static HectonDryVolumeStencilSource s_activeSource29;
        private static HectonDryVolumeStencilSource s_activeSource30;
        private static HectonDryVolumeStencilSource s_activeSource31;
        private static HectonDryVolumeStencilSource s_activeSource32;
        private static HectonDryVolumeStencilSource s_activeSource33;
        private static HectonDryVolumeStencilSource s_activeSource34;
        private static HectonDryVolumeStencilSource s_activeSource35;
        private static HectonDryVolumeStencilSource s_activeSource36;
        private static HectonDryVolumeStencilSource s_activeSource37;
        private static HectonDryVolumeStencilSource s_activeSource38;
        private static HectonDryVolumeStencilSource s_activeSource39;
        private static HectonDryVolumeStencilSource s_activeSource40;
        private static HectonDryVolumeStencilSource s_activeSource41;
        private static HectonDryVolumeStencilSource s_activeSource42;
        private static HectonDryVolumeStencilSource s_activeSource43;
        private static HectonDryVolumeStencilSource s_activeSource44;
        private static HectonDryVolumeStencilSource s_activeSource45;
        private static HectonDryVolumeStencilSource s_activeSource46;
        private static HectonDryVolumeStencilSource s_activeSource47;
        private static HectonDryVolumeStencilSource s_activeSource48;
        private static HectonDryVolumeStencilSource s_activeSource49;
        private static HectonDryVolumeStencilSource s_activeSource50;
        private static HectonDryVolumeStencilSource s_activeSource51;
        private static HectonDryVolumeStencilSource s_activeSource52;
        private static HectonDryVolumeStencilSource s_activeSource53;
        private static HectonDryVolumeStencilSource s_activeSource54;
        private static HectonDryVolumeStencilSource s_activeSource55;
        private static HectonDryVolumeStencilSource s_activeSource56;
        private static HectonDryVolumeStencilSource s_activeSource57;
        private static HectonDryVolumeStencilSource s_activeSource58;
        private static HectonDryVolumeStencilSource s_activeSource59;
        private static HectonDryVolumeStencilSource s_activeSource60;
        private static HectonDryVolumeStencilSource s_activeSource61;
        private static HectonDryVolumeStencilSource s_activeSource62;
        private static HectonDryVolumeStencilSource s_activeSource63;
        private static int s_activeSourceCount;

        [Header("── Dry Stencil Sources ──────────────────")]
        [Tooltip("Renderer entries that define the visible dry silhouette. Leave populated from Reset/OnValidate; runtime does not scan the hierarchy.")]
        [SerializeField] private RendererEntry[] rendererEntries = Array.Empty<RendererEntry>();

        /// <summary>
        /// Active registered dry stencil source count.
        /// </summary>
        internal static int ActiveSourceCount => s_activeSourceCount;

        /// <summary>
        /// Number of cached renderer entries owned by this source.
        /// </summary>
        public int EntryCount => rendererEntries != null ? rendererEntries.Length : 0;

        private void OnEnable()
        {
            RegisterActiveSource(this);
        }

        private void OnDisable()
        {
            UnregisterActiveSource(this);
        }

        internal static HectonDryVolumeStencilSource GetActiveSource(int index)
        {
            switch (index)
            {
                case 0: return s_activeSource0;
                case 1: return s_activeSource1;
                case 2: return s_activeSource2;
                case 3: return s_activeSource3;
                case 4: return s_activeSource4;
                case 5: return s_activeSource5;
                case 6: return s_activeSource6;
                case 7: return s_activeSource7;
                case 8: return s_activeSource8;
                case 9: return s_activeSource9;
                case 10: return s_activeSource10;
                case 11: return s_activeSource11;
                case 12: return s_activeSource12;
                case 13: return s_activeSource13;
                case 14: return s_activeSource14;
                case 15: return s_activeSource15;
                case 16: return s_activeSource16;
                case 17: return s_activeSource17;
                case 18: return s_activeSource18;
                case 19: return s_activeSource19;
                case 20: return s_activeSource20;
                case 21: return s_activeSource21;
                case 22: return s_activeSource22;
                case 23: return s_activeSource23;
                case 24: return s_activeSource24;
                case 25: return s_activeSource25;
                case 26: return s_activeSource26;
                case 27: return s_activeSource27;
                case 28: return s_activeSource28;
                case 29: return s_activeSource29;
                case 30: return s_activeSource30;
                case 31: return s_activeSource31;
                case 32: return s_activeSource32;
                case 33: return s_activeSource33;
                case 34: return s_activeSource34;
                case 35: return s_activeSource35;
                case 36: return s_activeSource36;
                case 37: return s_activeSource37;
                case 38: return s_activeSource38;
                case 39: return s_activeSource39;
                case 40: return s_activeSource40;
                case 41: return s_activeSource41;
                case 42: return s_activeSource42;
                case 43: return s_activeSource43;
                case 44: return s_activeSource44;
                case 45: return s_activeSource45;
                case 46: return s_activeSource46;
                case 47: return s_activeSource47;
                case 48: return s_activeSource48;
                case 49: return s_activeSource49;
                case 50: return s_activeSource50;
                case 51: return s_activeSource51;
                case 52: return s_activeSource52;
                case 53: return s_activeSource53;
                case 54: return s_activeSource54;
                case 55: return s_activeSource55;
                case 56: return s_activeSource56;
                case 57: return s_activeSource57;
                case 58: return s_activeSource58;
                case 59: return s_activeSource59;
                case 60: return s_activeSource60;
                case 61: return s_activeSource61;
                case 62: return s_activeSource62;
                case 63: return s_activeSource63;
                default: return null;
            }
        }

        private static void SetActiveSourceSlot(int index, HectonDryVolumeStencilSource source)
        {
            switch (index)
            {
                case 0: s_activeSource0 = source; break;
                case 1: s_activeSource1 = source; break;
                case 2: s_activeSource2 = source; break;
                case 3: s_activeSource3 = source; break;
                case 4: s_activeSource4 = source; break;
                case 5: s_activeSource5 = source; break;
                case 6: s_activeSource6 = source; break;
                case 7: s_activeSource7 = source; break;
                case 8: s_activeSource8 = source; break;
                case 9: s_activeSource9 = source; break;
                case 10: s_activeSource10 = source; break;
                case 11: s_activeSource11 = source; break;
                case 12: s_activeSource12 = source; break;
                case 13: s_activeSource13 = source; break;
                case 14: s_activeSource14 = source; break;
                case 15: s_activeSource15 = source; break;
                case 16: s_activeSource16 = source; break;
                case 17: s_activeSource17 = source; break;
                case 18: s_activeSource18 = source; break;
                case 19: s_activeSource19 = source; break;
                case 20: s_activeSource20 = source; break;
                case 21: s_activeSource21 = source; break;
                case 22: s_activeSource22 = source; break;
                case 23: s_activeSource23 = source; break;
                case 24: s_activeSource24 = source; break;
                case 25: s_activeSource25 = source; break;
                case 26: s_activeSource26 = source; break;
                case 27: s_activeSource27 = source; break;
                case 28: s_activeSource28 = source; break;
                case 29: s_activeSource29 = source; break;
                case 30: s_activeSource30 = source; break;
                case 31: s_activeSource31 = source; break;
                case 32: s_activeSource32 = source; break;
                case 33: s_activeSource33 = source; break;
                case 34: s_activeSource34 = source; break;
                case 35: s_activeSource35 = source; break;
                case 36: s_activeSource36 = source; break;
                case 37: s_activeSource37 = source; break;
                case 38: s_activeSource38 = source; break;
                case 39: s_activeSource39 = source; break;
                case 40: s_activeSource40 = source; break;
                case 41: s_activeSource41 = source; break;
                case 42: s_activeSource42 = source; break;
                case 43: s_activeSource43 = source; break;
                case 44: s_activeSource44 = source; break;
                case 45: s_activeSource45 = source; break;
                case 46: s_activeSource46 = source; break;
                case 47: s_activeSource47 = source; break;
                case 48: s_activeSource48 = source; break;
                case 49: s_activeSource49 = source; break;
                case 50: s_activeSource50 = source; break;
                case 51: s_activeSource51 = source; break;
                case 52: s_activeSource52 = source; break;
                case 53: s_activeSource53 = source; break;
                case 54: s_activeSource54 = source; break;
                case 55: s_activeSource55 = source; break;
                case 56: s_activeSource56 = source; break;
                case 57: s_activeSource57 = source; break;
                case 58: s_activeSource58 = source; break;
                case 59: s_activeSource59 = source; break;
                case 60: s_activeSource60 = source; break;
                case 61: s_activeSource61 = source; break;
                case 62: s_activeSource62 = source; break;
                case 63: s_activeSource63 = source; break;
            }
        }

        private static void RegisterActiveSource(HectonDryVolumeStencilSource source)
        {
            if (source == null)
                return;

            for (int i = 0; i < s_activeSourceCount; i++)
            {
                if (ReferenceEquals(GetActiveSource(i), source))
                    return;
            }

            if (s_activeSourceCount >= MaxActiveSources)
                return;

            SetActiveSourceSlot(s_activeSourceCount, source);
            s_activeSourceCount++;
        }

        private static void UnregisterActiveSource(HectonDryVolumeStencilSource source)
        {
            if (source == null)
                return;

            for (int i = 0; i < s_activeSourceCount; i++)
            {
                if (!ReferenceEquals(GetActiveSource(i), source))
                    continue;

                int lastIndex = s_activeSourceCount - 1;
                SetActiveSourceSlot(i, GetActiveSource(lastIndex));
                SetActiveSourceSlot(lastIndex, null);
                s_activeSourceCount = lastIndex;
                return;
            }
        }

        /// <summary>
        /// Returns a cached renderer entry for the stencil pass.
        /// </summary>
        /// <param name="index">Entry index.</param>
        /// <param name="renderer">Resolved renderer.</param>
        /// <param name="subMeshCount">Cached sub-mesh count.</param>
        /// <returns>True when the entry is valid.</returns>
        public bool TryGetEntry(int index, out Renderer renderer, out int subMeshCount)
        {
            renderer = null;
            subMeshCount = 0;

            if (rendererEntries == null || index < 0 || index >= rendererEntries.Length)
                return false;

            renderer = rendererEntries[index].renderer;
            subMeshCount = Mathf.Max(1, rendererEntries[index].subMeshCount);
            return renderer != null;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            RebuildRendererEntries();
        }

        private void OnValidate()
        {
            RebuildRendererEntries();
        }

        private void RebuildRendererEntries()
        {
            int rendererCount = CountRenderers(transform);
            if (rendererCount <= 0)
            {
                rendererEntries = Array.Empty<RendererEntry>();
                return;
            }

            RendererEntry[] rebuiltEntries = new RendererEntry[rendererCount];
            int writeIndex = 0;
            CollectRendererEntries(transform, rebuiltEntries, ref writeIndex);

            rendererEntries = rebuiltEntries;
            EditorUtility.SetDirty(this);
        }

        private static int CountRenderers(Transform root)
        {
            if (root == null)
                return 0;

            int count = 0;
            if (root.TryGetComponent(out Renderer _))
                count++;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
                count += CountRenderers(root.GetChild(i));

            return count;
        }

        private static void CollectRendererEntries(Transform root, RendererEntry[] entries, ref int writeIndex)
        {
            if (root == null || entries == null || writeIndex >= entries.Length)
                return;

            if (root.TryGetComponent(out Renderer renderer))
            {
                RendererEntry entry = default;
                entry.renderer = renderer;
                entry.subMeshCount = ResolveSubMeshCount(renderer);
                entries[writeIndex] = entry;
                writeIndex++;
            }

            int childCount = root.childCount;
            for (int i = 0; i < childCount && writeIndex < entries.Length; i++)
                CollectRendererEntries(root.GetChild(i), entries, ref writeIndex);
        }

        private static int ResolveSubMeshCount(Renderer renderer)
        {
            if (renderer is MeshRenderer meshRenderer)
            {
                if (meshRenderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                    return Mathf.Max(1, meshFilter.sharedMesh.subMeshCount);
            }
            else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
                return Mathf.Max(1, skinnedMeshRenderer.sharedMesh.subMeshCount);
            }

            return 1;
        }
#endif
    }
}
