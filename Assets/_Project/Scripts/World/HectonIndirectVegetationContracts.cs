using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

    public enum HectonFloraSporeEventKind : byte
    {
        MatureToxic = 1,
        DefensiveBurst = 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HectonFloraSporeEvent
    {
        [FieldOffset(0)]
        public AbsoluteUniversePositionBlit PositionAup;
        [FieldOffset(48)]
        public float3 RuntimePosition;
        [FieldOffset(60)]
        public float RadiusMeters;
        [FieldOffset(64)]
        public float Intensity01;
        [FieldOffset(68)]
        public float Age01;
        [FieldOffset(72)]
        public int TemplateIndex;
        [FieldOffset(76)]
        public int ActivePayloadIndex;
        [FieldOffset(80)]
        public uint FrameIndex;
        [FieldOffset(84)]
        public HectonFloraSporeEventKind Kind;
        [FieldOffset(85)]
        public byte Underwater;
        [FieldOffset(86)]
        public ushort Reserved0;
        [FieldOffset(88)]
        private ulong _pad0;
    }

    /// <summary>
    /// NativeQueue-backed flora spore handoff for fog/scatter consumers.
    /// </summary>
    public static class HectonFloraSporeEvents
    {
        public const int PendingEventCapacity = 64;
        private const Allocator DataVaultExemptFloraSporeEventLaneAllocator = Allocator.Persistent;

        private static NativeQueue<HectonFloraSporeEvent> _pendingEvents;
        private static int _pendingEventCount;
        private static int _droppedEventCount;

        public static int PendingCount => _pendingEventCount;
        public static int DroppedEventCount => _droppedEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonFloraSporeEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _pendingEventCount = 0;
            _droppedEventCount = 0;
        }

        public static bool EnqueueMatureToxicSpore(
            Vector3 runtimePosition,
            float radiusMeters,
            float intensity01,
            float age01,
            int templateIndex,
            int activePayloadIndex,
            bool underwater)
        {
            return Enqueue(
                runtimePosition,
                radiusMeters,
                intensity01,
                age01,
                templateIndex,
                activePayloadIndex,
                underwater,
                HectonFloraSporeEventKind.MatureToxic);
        }

        public static bool EnqueueDefensiveSporeBurst(
            Vector3 runtimePosition,
            float radiusMeters,
            float intensity01)
        {
            return Enqueue(
                runtimePosition,
                radiusMeters,
                intensity01,
                1f,
                -1,
                -1,
                true,
                HectonFloraSporeEventKind.DefensiveBurst);
        }

        public static bool TryDequeue(out HectonFloraSporeEvent sporeEvent)
        {
            if (!_pendingEvents.IsCreated || !_pendingEvents.TryDequeue(out sporeEvent))
            {
                sporeEvent = default;
                return false;
            }

            if (_pendingEventCount > 0)
                _pendingEventCount--;

            return true;
        }

        public static void Clear()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }

            _pendingEventCount = 0;
        }

        private static bool Enqueue(
            Vector3 runtimePosition,
            float radiusMeters,
            float intensity01,
            float age01,
            int templateIndex,
            int activePayloadIndex,
            bool underwater,
            HectonFloraSporeEventKind kind)
        {
            if (!IsFinite(runtimePosition) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(intensity01) ||
                !math.isfinite(age01) ||
                radiusMeters <= 0f ||
                intensity01 <= 0f)
            {
                return false;
            }

            if (_pendingEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                return false;
            }

            EnsureInitialized();
            if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition positionAup))
            {
                _droppedEventCount++;
                return false;
            }

            _pendingEvents.Enqueue(new HectonFloraSporeEvent
            {
                PositionAup = AbsoluteUniversePositionBlit.FromAup(in positionAup),
                RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                RadiusMeters = Mathf.Max(0.01f, radiusMeters),
                Intensity01 = Mathf.Clamp01(intensity01),
                Age01 = Mathf.Clamp01(age01),
                TemplateIndex = templateIndex,
                ActivePayloadIndex = activePayloadIndex,
                FrameIndex = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                Kind = kind,
                Underwater = underwater ? (byte)1 : (byte)0,
                Reserved0 = 0
            });
            _pendingEventCount++;
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static void EnsureInitialized()
        {
            if (_pendingEvents.IsCreated)
                return;

            _pendingEvents = new NativeQueue<HectonFloraSporeEvent>(DataVaultExemptFloraSporeEventLaneAllocator); // COLD ALLOC: NativeQueue<HectonFloraSporeEvent>[64] - flora spore event handoff lane for GPU fog/scatter consumers - owner: HectonFloraSporeEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(HectonFloraSporeEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }
    }

    /// <summary>
    /// Per-instance metadata payload consumed by the indirect vegetation shader.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = Stride)]
    public struct HectonVegetationInstanceData
    {
        /// <summary>Exact GPU stride in bytes.</summary>
        public const int Stride = 64;
        public const float PackedPresentationValueMax = 65535f;
        public const float PackedPresentationByteMax = 255f;
        public const float RuntimeStateIdle = (float)HectonVegetationRuntimeState.Idle;
        public const float RuntimeStateAgitated = (float)HectonVegetationRuntimeState.Agitated;
        public const float RuntimeStateDying = (float)HectonVegetationRuntimeState.Dying;

        /// <summary>Vegetation type flag: 0 grass, 1 giant kelp, 2 sargassum.</summary>
        [FieldOffset(0)]
        public float Type;

        /// <summary>
        /// Type-specific height control.
        /// Grass/sargassum: normalized short-range variation.
        /// Giant kelp: normalized 0..1 value mapped to 10-20 m length in shader.
        /// </summary>
        [FieldOffset(4)]
        public float HeightScale;

        /// <summary>Width multiplier or silhouette variation.</summary>
        [FieldOffset(8)]
        public float WidthScale;

        /// <summary>Stable per-instance variation value used for phase/randomization.</summary>
        [FieldOffset(12)]
        public float Variation;

        /// <summary>Resolved flora-template index authored by the vegetation bridge. -1 when no template matched.</summary>
        [FieldOffset(16)]
        public float TemplateIndex;

        /// <summary>Zero-state runtime animation lane: 0 idle, 1 agitated, 2 dying.</summary>
        [FieldOffset(20)]
        public float RuntimeState;

        /// <summary>Explicit runtime flags consumed by shaders and gameplay instead of overloading variation bits.</summary>
        [FieldOffset(24)]
        public float RuntimeFlags;

        /// <summary>Per-instance bioluminescence pulse frequency in Hertz.</summary>
        [FieldOffset(28)]
        public float PulseFrequency;

        /// <summary>Per-instance bioluminescence color in linear space. Alpha packs emission intensity and damage state.</summary>
        [FieldOffset(32)]
        public Vector4 BioluminescenceColor;

        /// <summary>Per-instance VAT sway speed multiplier stamped from flora authoring.</summary>
        [FieldOffset(48)]
        public float SwaySpeed;

        /// <summary>Per-instance VAT bend amplitude multiplier stamped from flora authoring.</summary>
        [FieldOffset(52)]
        public float BendAmplitude;

        /// <summary>Normalized health lane consumed by harvest visuals and emissive dimming.</summary>
        [FieldOffset(56)]
        public float HealthNormalized;

        /// <summary>Optional cultivation growth lane. Negative means culled/harvested; zero means legacy/default mature when no cultivation data is authored.</summary>
        [FieldOffset(60)]
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
            float sanitizedHealth = Mathf.Clamp01(healthNormalized);
            PulseFrequency = Mathf.Max(0.01f, pulseFrequency);
            BioluminescenceColor = PackPresentationPayload(bioluminescenceColor, 1f - sanitizedHealth);
            SwaySpeed = swaySpeed;
            BendAmplitude = bendAmplitude;
            HealthNormalized = sanitizedHealth;
            Reserved0 = Mathf.Clamp(reserved0, -1f, 1f);
        }

        public static Vector4 PackPresentationPayload(Vector4 colorAndIntensity, float damageState)
        {
            return new Vector4(
                Mathf.Clamp01(colorAndIntensity.x),
                Mathf.Clamp01(colorAndIntensity.y),
                Mathf.Clamp01(colorAndIntensity.z),
                PackBiolumDamage(colorAndIntensity.w, damageState));
        }

        public static float PackBiolumDamage(float biolumIntensity, float damageState)
        {
            int biolumByte = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(biolumIntensity) * PackedPresentationByteMax), 0, 255);
            int damageByte = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(damageState) * PackedPresentationByteMax), 0, 255);
            return (biolumByte + damageByte * 256f) / PackedPresentationValueMax;
        }

        public static float UnpackBiolumIntensity(float packedPresentationAlpha)
        {
            int packedValue = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(packedPresentationAlpha) * PackedPresentationValueMax), 0, 65535);
            return (packedValue & 0xFF) / PackedPresentationByteMax;
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
            HasExplicitBoundsFlag = hasExplicitBounds ? (byte)1 : (byte)0;
            DrawBounds = drawBounds;
        }

        /// <summary>Native matrix payload exported by the producer.</summary>
        public readonly NativeArray<Matrix4x4> InstanceMatrices;

        /// <summary>Native metadata payload exported by the producer.</summary>
        public readonly NativeArray<HectonVegetationInstanceData> InstanceData;

        /// <summary>Valid entry count in both native arrays.</summary>
        public readonly int InstanceCount;

        /// <summary>Producer-owned front/back buffer index that was acquired for this read.</summary>
        public readonly int BufferIndex;

        /// <summary>Producer job fence that must complete before the renderer reads the arrays.</summary>
        public readonly JobHandle ProducerHandle;

        /// <summary>Non-zero when the producer exported explicit world-space bounds.</summary>
        public readonly byte HasExplicitBoundsFlag;

        /// <summary>Explicit world-space bounds for the acquired read token.</summary>
        public readonly Bounds DrawBounds;

        /// <summary>True when the token contains enough data for upload.</summary>
        public static bool IsValid(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            return readBuffer.InstanceCount > 0 &&
                   readBuffer.InstanceMatrices.IsCreated &&
                   readBuffer.InstanceData.IsCreated &&
                   readBuffer.InstanceMatrices.Length >= readBuffer.InstanceCount &&
                   readBuffer.InstanceData.Length >= readBuffer.InstanceCount;
        }

        /// <summary>True when the producer exported explicit world-space bounds.</summary>
        public static bool HasExplicitBounds(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            return readBuffer.HasExplicitBoundsFlag != 0;
        }
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
