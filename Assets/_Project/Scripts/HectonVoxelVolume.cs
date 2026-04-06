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
        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        private void OnDestroy()
        {
            // Cleanup if needed
        }
    }
}