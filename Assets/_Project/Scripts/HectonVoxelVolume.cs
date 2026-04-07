// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelVolume.cs — Project HECTON-8 Voxel Volume Component         ║
// ║  Unity 6 | Simple component for cave volumes                             ║
// ║  v1.0 — Basic volume marker                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Simple component attached to generated cave volume GameObjects.
    /// Provides a way to identify and manage cave volumes in the scene.
    /// </summary>
    public class HectonVoxelVolume : MonoBehaviour
    {
        private const string CaveDressingRootName = "_CaveDressing";
        private const string EntranceQualityRootName = "_EntranceQualityZone";
        private const string EntranceMarkersRootName = "_EntranceMarkers";

        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        /// <summary>
        /// Resets cave-owned runtime children so pooled volumes do not leak
        /// previous cave dressing or entrance readability state into the next build.
        /// </summary>
        public void PrepareForReuse()
        {
            caveKey = 0L;
            generationPosition = Vector3.zero;
            preset = null;

            ToggleChildRoot(CaveDressingRootName, false);
            ToggleChildRoot(EntranceQualityRootName, false);
            ToggleChildRoot(EntranceMarkersRootName, false);
        }

        /// <summary>
        /// Ensures a named direct child root exists and is active.
        /// Reused by cave readability/detail systems to avoid duplicate runtime roots.
        /// </summary>
        public Transform GetOrCreateRuntimeRoot(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return null;

            Transform child = transform.Find(childName);
            if (child != null)
            {
                if (!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
            return child;
        }

        private void ToggleChildRoot(string childName, bool active)
        {
            if (string.IsNullOrEmpty(childName))
                return;

            Transform child = transform.Find(childName);
            if (child == null || child.gameObject.activeSelf == active)
                return;

            child.gameObject.SetActive(active);
        }

        private void OnDestroy()
        {
            // Cleanup if needed
        }
    }
}
