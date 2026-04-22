using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
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

    /// <summary>
    /// Immutable native read token for front/back buffered vegetation export.
    /// The producer owns lifetime and the consumer must release the token after upload.
    /// </summary>
    public readonly struct HectonIndirectVegetationNativeReadBuffer
    {
        /// <summary>
        /// Creates one native read token for the indirect vegetation renderer.
        /// </summary>
        public HectonIndirectVegetationNativeReadBuffer(
            NativeArray<Matrix4x4> instanceMatrices,
            NativeArray<HectonVegetationInstanceData> instanceData,
            int instanceCount,
            int bufferIndex,
            JobHandle producerHandle,
            bool hasExplicitBounds,
            Bounds drawBounds)
        {
            InstanceMatrices = instanceMatrices;
            InstanceData = instanceData;
            InstanceCount = instanceCount;
            BufferIndex = bufferIndex;
            ProducerHandle = producerHandle;
            HasExplicitBounds = hasExplicitBounds;
            DrawBounds = drawBounds;
        }

        /// <summary>Native matrix payload exported by the producer.</summary>
        public NativeArray<Matrix4x4> InstanceMatrices { get; }

        /// <summary>Native metadata payload exported by the producer.</summary>
        public NativeArray<HectonVegetationInstanceData> InstanceData { get; }

        /// <summary>Valid entry count in both native arrays.</summary>
        public int InstanceCount { get; }

        /// <summary>Producer-owned front/back buffer index that was acquired for this read.</summary>
        public int BufferIndex { get; }

        /// <summary>Producer job fence that must complete before the renderer reads the arrays.</summary>
        public JobHandle ProducerHandle { get; }

        /// <summary>True when the producer exported explicit world-space bounds.</summary>
        public bool HasExplicitBounds { get; }

        /// <summary>Explicit world-space bounds for the acquired read token.</summary>
        public Bounds DrawBounds { get; }

        /// <summary>True when the token contains enough data for upload.</summary>
        public bool IsValid =>
            InstanceCount > 0 &&
            InstanceMatrices.IsCreated &&
            InstanceData.IsCreated &&
            InstanceMatrices.Length >= InstanceCount &&
            InstanceData.Length >= InstanceCount;
    }

    /// <summary>
    /// Native double-buffer export seam used by the indirect vegetation renderer.
    /// </summary>
    public interface IHectonIndirectVegetationNativeBufferSource
    {
        /// <summary>
        /// Acquires the currently readable front/back native buffer for upload into renderer-owned staging buffers.
        /// </summary>
        bool TryAcquireNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer);

        /// <summary>
        /// Releases a previously acquired read buffer and returns the consumer reader fence to the producer.
        /// </summary>
        void ReleaseNativeReadBuffer(in HectonIndirectVegetationNativeReadBuffer readBuffer, JobHandle readerHandle);
    }
}
