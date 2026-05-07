using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Shader-driven vegetation state lane consumed by the indirect vegetation runtime.
    /// </summary>
    public enum HectonVegetationRuntimeState : byte
    {
        Idle = 0,
        Agitated = 1,
        Dying = 2
    }

    /// <summary>
    /// Stable runtime flags written into the indirect vegetation metadata payload.
    /// </summary>
    [System.Flags]
    public enum HectonVegetationRuntimeFlags : byte
    {
        None = 0,
        Parasite = 1 << 0,
        PlayerContact = 1 << 1,
        CascadeActive = 1 << 2,
        AllelopathicRelease = 1 << 3,
        Dead = 1 << 6
    }

    /// <summary>
    /// Compact shader-visible flora genetics byte. Bit layout is fixed by the biodiversity rendering contract.
    /// </summary>
    [System.Flags]
    public enum HectonVegetationGeneticTraits : byte
    {
        None = 0,
        Poisonous = 1 << 0,
        Edible = 1 << 1,
        EmitsLight = 1 << 2
    }

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
    [StructLayout(LayoutKind.Sequential, Size = Stride)]
    public struct HectonVegetationInstanceData
    {
        /// <summary>Exact GPU stride in bytes.</summary>
        public const int Stride = 64;
        public const float RuntimeStateIdle = (float)HectonVegetationRuntimeState.Idle;
        public const float RuntimeStateAgitated = (float)HectonVegetationRuntimeState.Agitated;
        public const float RuntimeStateDying = (float)HectonVegetationRuntimeState.Dying;

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

        /// <summary>Resolved flora-template index authored by the vegetation bridge. -1 when no template matched.</summary>
        public float TemplateIndex;

        /// <summary>Zero-state runtime animation lane: 0 idle, 1 agitated, 2 dying.</summary>
        public float RuntimeState;

        /// <summary>Explicit runtime flags consumed by shaders and gameplay instead of overloading variation bits.</summary>
        public float RuntimeFlags;

        /// <summary>Per-instance bioluminescence pulse frequency in Hertz.</summary>
        public float PulseFrequency;

        /// <summary>Per-instance bioluminescence color in linear space. Alpha stores emission intensity.</summary>
        public Vector4 BioluminescenceColor;

        /// <summary>Per-instance VAT sway speed multiplier stamped from flora authoring.</summary>
        public float SwaySpeed;

        /// <summary>Per-instance VAT bend amplitude multiplier stamped from flora authoring.</summary>
        public float BendAmplitude;

        /// <summary>Normalized health lane consumed by harvest visuals and emissive dimming.</summary>
        public float HealthNormalized;

        /// <summary>Optional cultivation growth lane. Zero means legacy/default mature when no cultivation data is authored.</summary>
        public float Reserved0;

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
            float variation,
            float templateIndex,
            float runtimeState,
            float runtimeFlags,
            float pulseFrequency,
            Vector4 bioluminescenceColor,
            float swaySpeed,
            float bendAmplitude,
            float healthNormalized,
            float reserved0)
        {
            Type = (float)type;
            HeightScale = heightScale;
            WidthScale = widthScale;
            Variation = variation;
            TemplateIndex = templateIndex;
            RuntimeState = runtimeState;
            RuntimeFlags = runtimeFlags;
            PulseFrequency = Mathf.Max(0.01f, pulseFrequency);
            BioluminescenceColor = bioluminescenceColor;
            SwaySpeed = swaySpeed;
            BendAmplitude = bendAmplitude;
            HealthNormalized = Mathf.Clamp01(healthNormalized);
            Reserved0 = Mathf.Clamp(reserved0, 0f, 1f);
        }

        /// <summary>
        /// Creates one legacy metadata payload using the widened runtime layout defaults.
        /// </summary>
        public HectonVegetationInstanceData(
            HectonVegetationInstanceType type,
            float heightScale,
            float widthScale,
            float variation)
            : this(
                type,
                heightScale,
                widthScale,
                variation,
                -1f,
                RuntimeStateIdle,
                0f,
                0f,
                Vector4.zero,
                1f,
                1f,
                1f,
                0f)
        {
        }
    }

    /// <summary>
    /// Shared bit-packing helpers for vegetation runtime flags consumed by gameplay and shaders.
    /// </summary>
    internal static class HectonVegetationRuntimeFlagEncoding
    {
        internal const byte BiomeLayerBitShift = 4;
        internal const byte BiomeLayerBitMask = 0x30;
        internal const byte BiomeLayerValueMask = 0x03;
        internal const int RuntimeFlagValueMask = 0xFF;
        internal const int GeneticTraitBitShift = 8;
        internal const int GeneticTraitValueMask = 0xFF;
        internal const int PackedRuntimeAndTraitMask = 0xFFFF;

        internal static float Encode(byte biomeLayer, byte runtimeFlags)
        {
            return Encode(biomeLayer, runtimeFlags, 0);
        }

        internal static float Encode(byte biomeLayer, byte runtimeFlags, byte geneticTraits)
        {
            byte packedFlags = MergeBiomeLayer(runtimeFlags, biomeLayer);
            return packedFlags | ((geneticTraits & GeneticTraitValueMask) << GeneticTraitBitShift);
        }

        internal static float WithRuntimeFlags(float existingPackedValue, byte runtimeFlags)
        {
            byte biomeLayer = ExtractBiomeLayer(existingPackedValue);
            byte geneticTraits = ExtractGeneticTraits(existingPackedValue);
            return Encode(biomeLayer, runtimeFlags, geneticTraits);
        }

        internal static byte MergeBiomeLayer(byte runtimeFlags, byte biomeLayer)
        {
            byte sanitizedBiomeLayer = (byte)(biomeLayer & BiomeLayerValueMask);
            return (byte)((runtimeFlags & ~BiomeLayerBitMask) | (sanitizedBiomeLayer << BiomeLayerBitShift));
        }

        internal static byte ExtractBiomeLayer(float runtimeFlags)
        {
            return (byte)((ExtractPackedFlags(runtimeFlags) & BiomeLayerBitMask) >> BiomeLayerBitShift);
        }

        internal static byte ExtractGeneticTraits(float runtimeFlags)
        {
            return (byte)((ExtractPackedRuntimeAndTraits(runtimeFlags) >> GeneticTraitBitShift) & GeneticTraitValueMask);
        }

        internal static bool HasGeneticTrait(float runtimeFlags, HectonVegetationGeneticTraits trait)
        {
            return (ExtractGeneticTraits(runtimeFlags) & (byte)trait) != 0;
        }

        internal static byte ExtractPackedFlags(float runtimeFlags)
        {
            return (byte)(ExtractPackedRuntimeAndTraits(runtimeFlags) & RuntimeFlagValueMask);
        }

        internal static int ExtractPackedRuntimeAndTraits(float runtimeFlags)
        {
            int roundedValue = Mathf.RoundToInt(runtimeFlags);
            if (roundedValue <= 0)
                return 0;

            return roundedValue > PackedRuntimeAndTraitMask ? PackedRuntimeAndTraitMask : roundedValue;
        }
    }

    /// <summary>
    /// External buffer seam for indirect vegetation rendering.
    /// Cartographer or any other producer owns the buffers and lifetime.
    /// </summary>
    public interface IHectonIndirectVegetationBufferSource
    {
        /// <summary>Structured buffer of per-instance matrices.</summary>
        GraphicsBuffer InstanceMatrixBuffer { get; }

        /// <summary>Structured buffer of <see cref="HectonVegetationInstanceData"/> payloads.</summary>
        GraphicsBuffer InstanceDataBuffer { get; }

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
