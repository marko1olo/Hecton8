using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Vegetation type consumed by the indirect vegetation shader.
    /// </summary>
    public enum HectonVegetationInstanceType
    {
        Grass = 0,
        GiantKelp = 1,
        Sargassum = 2
    }

    /// <summary>
    /// Per-instance metadata payload consumed by the indirect vegetation shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HectonVegetationInstanceData
    {
        /// <summary>Exact GPU stride in bytes.</summary>
        public const int Stride = 16;

        /// <summary>Vegetation type flag: 0 grass, 1 giant kelp, 2 sargassum.</summary>
        public float Type;

        /// <summary>
        /// Type-specific height control.
        /// Grass/sargassum: normalized short-range variation.
        /// Giant kelp: normalized 0..1 value mapped to 10-20 m length in shader.
        /// </summary>
        public float HeightScale;

        /// <summary>Width multiplier or silhouette variation.</summary>
        public float WidthScale;

        /// <summary>Stable per-instance variation value used for phase/randomization.</summary>
        public float Variation;

        /// <summary>
        /// Creates one per-instance vegetation metadata payload.
        /// </summary>
        /// <param name="type">Vegetation type flag.</param>
        /// <param name="heightScale">Type-specific height parameter.</param>
        /// <param name="widthScale">Width multiplier.</param>
        /// <param name="variation">Stable variation seed in 0..1.</param>
        public HectonVegetationInstanceData(
            HectonVegetationInstanceType type,
            float heightScale,
            float widthScale,
            float variation)
        {
            Type = (float)type;
            HeightScale = heightScale;
            WidthScale = widthScale;
            Variation = variation;
        }
    }

    /// <summary>
    /// External buffer seam for indirect vegetation rendering.
    /// Cartographer or any other producer owns the buffers and lifetime.
    /// </summary>
    public interface IHectonIndirectVegetationBufferSource
    {
        /// <summary>Structured buffer of per-instance matrices.</summary>
        ComputeBuffer InstanceMatrixBuffer { get; }

        /// <summary>Structured buffer of <see cref="HectonVegetationInstanceData"/> payloads.</summary>
        ComputeBuffer InstanceDataBuffer { get; }

        /// <summary>Active instance count available in both buffers.</summary>
        int InstanceCount { get; }

        /// <summary>True when the source provides explicit world-space draw bounds.</summary>
        bool HasExplicitBounds { get; }

        /// <summary>World-space bounds for the indirect draw call when available.</summary>
        Bounds DrawBounds { get; }
    }
}
