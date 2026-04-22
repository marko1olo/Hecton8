using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Abstracts runtime ocean kinematics queries away from a specific ocean backend.
    /// Caller owns all batch buffers; implementations must write into those buffers without allocating.
    /// Supports up to the player controller's 5-point body sampling batch.
    /// </summary>
    public interface IHectonOceanKinematics
    {
        /// <summary>
        /// Provider-selection priority used by the global ocean registry. Higher wins.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// True when the underlying ocean backend can answer collision/flow queries this frame.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Current water level fallback used when fine sampling is unavailable.
        /// </summary>
        float SeaLevel { get; }

        /// <summary>
        /// Samples water height for a caller-owned point batch.
        /// </summary>
        bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights);

        /// <summary>
        /// Samples horizontal/vertical authored surface flow for a caller-owned point batch.
        /// </summary>
        bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows);

        /// <summary>
        /// Samples per-point wave normals plus surface velocity/displacement for a caller-owned point batch.
        /// </summary>
        bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements);
    }
}
