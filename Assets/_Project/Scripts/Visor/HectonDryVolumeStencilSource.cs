using System;
using System.Collections.Generic;
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

        // COLD ALLOC: List<HectonDryVolumeStencilSource>[64] - active dry-volume stencil owners registry - owner: HectonDryVolumeStencilSource
        private static readonly List<HectonDryVolumeStencilSource> s_ActiveSources = new List<HectonDryVolumeStencilSource>(64);

        [Header("── Dry Stencil Sources ──────────────────")]
        [Tooltip("Renderer entries that define the visible dry silhouette. Leave populated from Reset/OnValidate; runtime does not scan the hierarchy.")]
        [SerializeField] private RendererEntry[] rendererEntries = Array.Empty<RendererEntry>();

        /// <summary>
        /// Active registered dry stencil sources.
        /// </summary>
        internal static IReadOnlyList<HectonDryVolumeStencilSource> ActiveSources => s_ActiveSources;

        /// <summary>
        /// Number of cached renderer entries owned by this source.
        /// </summary>
        public int EntryCount => rendererEntries != null ? rendererEntries.Length : 0;

        private void OnEnable()
        {
            if (!s_ActiveSources.Contains(this))
                s_ActiveSources.Add(this);
        }

        private void OnDisable()
        {
            s_ActiveSources.Remove(this);
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
        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(16);

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
            s_RendererScratch.Clear();
            GetComponentsInChildren(true, s_RendererScratch);
            if (s_RendererScratch.Count == 0)
            {
                rendererEntries = Array.Empty<RendererEntry>();
                return;
            }

            RendererEntry[] rebuiltEntries = new RendererEntry[s_RendererScratch.Count];
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                rebuiltEntries[i] = new RendererEntry
                {
                    renderer = renderer,
                    subMeshCount = ResolveSubMeshCount(renderer)
                };
            }

            s_RendererScratch.Clear();
            rendererEntries = rebuiltEntries;
            EditorUtility.SetDirty(this);
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
