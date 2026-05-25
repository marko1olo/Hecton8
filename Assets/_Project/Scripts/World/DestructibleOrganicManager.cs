using System.Runtime.InteropServices;
using System;
using System.Threading;
using Hecton8.Audio;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Scavenging;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Zero-allocation hand IK snap target resolved from the active indirect-flora lanes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct FloraHarvestInteractionPoint
    {
        [FieldOffset(0)]
        public readonly uint InstanceUid;
        [FieldOffset(4)]
        private readonly uint _pad0;
        [FieldOffset(8)]
        public readonly AbsoluteUniversePosition AnchorAup;
        [FieldOffset(56)]
        public readonly Vector3 RuntimePosition;
        [FieldOffset(68)]
        public readonly Vector3 SurfaceNormal;
        [FieldOffset(80)]
        public readonly HarvestableTemplate.MaterialClass MaterialClass;
        [FieldOffset(81)]
        private readonly byte _pad1;
        [FieldOffset(82)]
        private readonly ushort _pad2;
        [FieldOffset(84)]
        public readonly int TemplateIndex;
        [FieldOffset(88)]
        public readonly float BlendWeight;
        [FieldOffset(92)]
        private readonly uint _pad3;

        public FloraHarvestInteractionPoint(
            uint instanceUid,
            AbsoluteUniversePosition anchorAup,
            Vector3 runtimePosition,
            Vector3 surfaceNormal,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            float blendWeight)
        {
            InstanceUid = instanceUid;
            _pad0 = 0u;
            AnchorAup = anchorAup;
            RuntimePosition = runtimePosition;
            SurfaceNormal = surfaceNormal;
            MaterialClass = materialClass;
            _pad1 = 0;
            _pad2 = 0;
            TemplateIndex = templateIndex;
            BlendWeight = blendWeight;
            _pad3 = 0u;
        }
    }

    /// <summary>
    /// Runtime owner for indirect-flora harvest health, destruction, debris, and yield routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)] // Manager order must stay ahead of gameplay consumers that read/wire destruction state.
    public sealed class DestructibleOrganicManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IOrganicToolHitService
    {
        private static int s_x001DirectSignalPushDropCount_DestructibleOrganicManager;

        private static int s_x001DestructibleOrganicManagerSignalPushDropCount;
        private static DestructibleOrganicManager _activeRuntimeInstance;

        private const int DefaultTrackedDestroyedCapacity = 2048;
        private const int DefaultTrackedHealthCapacity = 4096;
        private const int DefaultPendingYieldCapacity = 1024;
        private const int DefaultDropBufferCapacity = 256;
        private const int MaxOrganicDropRecordsPerFrame = 256;
        private const float HiddenInstanceWorldY = -100000f;
        private const float MinimumSearchRadius = 0.8f;
        private const float KelpRadiusBias = 0.65f;
        private const float OrganicBurstVelocityScale = 3f;
        private const float OrganicWiltDurationSeconds = 0.85f;
        private const float OrganicDecompositionDurationSeconds = 10f * 60f;
        private const float MinimumDecomposedHeightScale = 0.05f;
        private const float MinimumDecomposedWidthScale = 0.12f;
        private const float HarvestStatePartialThreshold01 = 0.999f;
        private const float HarvestStateBareThreshold01 = 0.3f;
        private const float MatureSporeGrowthThreshold01 = 0.999f;
        private const float MinimumSporePulseFrequencyHz = 0.01f;
        private const float SporePulsePeakPhase01 = 0.25f;
        private const float SporeShaderPhasePositionX = 0.07f;
        private const float SporeShaderPhasePositionZ = 0.05f;
        private const float InvTwoPi = 0.15915494309189535f;
        private const float SoftBareHealthFloor01 = 0.05f;
        private const float LightStarvationDamagePerSlowTick01 = 0.035f;
        private const float LightStarvationDeathHealth01 = 0.015f;
        private const float AllelopathicBareHealth01 = 0.08f;
        private const float AllelopathicDeathThreshold01 = 0.85f;
        private const float OvergrowthUntouchedSeconds = 3f * 24f * 60f * 60f;
        private const float OvergrowthExpansionMeters = 2f;
        private const int OvergrowthScanBudgetPerSlowTick = 64;
        private const float TitanRootMoundRadiusMeters = 5f;
        private const float TitanRootMoundStrengthMeters = 2.25f;
        private const float TitanRootMoundMatureThreshold01 = 0.999f;
        private const byte FloraRuntimeFlagHasParasite = (byte)HectonVegetationRuntimeFlags.Parasite;
        private const byte FloraRuntimeFlagDead = 1 << 6;
        private const int DefaultCorpseNodeCapacity = 96;
        private const float DefaultCorpseBloodIntensity = 6f;
        private const float CorpseDiseaseActivationSeconds = 120f;
        private const float CorpseDiseaseRadiusMeters = 22f;
        private const float CorpseDiseaseSeverity = 1f;
        private const int MaterialClassCount = 5;
        private const int DearLieMaxDamageSignalsPerFrame = 128;
        private const int DearLieMockDamageSignalCount = 100;
        private const int DearLieMaxResultsPerFrame = DearLieMaxDamageSignalsPerFrame * 2;
        private const int DearLieMaxRegenRecords = 2048;
        private const int DearLieTelemetryFrameCount = 300;
        private const int DearLieSpatialHashCapacity = 8192;
        private const int DearLieJobBatchSize = 64;
        private const float DearLieQueryRadiusMeters = 2.25f;
        private const float DearLieSpatialCellSizeMeters = 3f;
        private const float DearLieRegenerationDelaySeconds = 300f;
        private const float DearLieMinimumMagnitude = 0.001f;
        private const double OrganicClockMaxSeconds = 16777215d;
        private const uint DearLieSignalHashFlora = 0x464C4F52u; // FLOR
        private const uint DearLieSignalHashOrganic = 0x4F524741u; // ORGA
        private const byte DearLieFloraDamageFlag = 1 << 6;
        private const BufferID DearLieSurfaceClaimsBufferId = (BufferID)72980;
        private const BufferID DearLieUnderwaterClaimsBufferId = (BufferID)72981;
        private const BufferID DearLieDamageEventsBufferId = (BufferID)72982;
        private const BufferID DearLieResultsBufferId = (BufferID)72983;
        private const BufferID DearLieCountersBufferId = (BufferID)72984;
        private const BufferID DearLieRegenRecordsBufferId = (BufferID)72985;
        private const BufferID DearLieTelemetryRingBufferId = (BufferID)72986;
        private const BufferID DearLieSurfaceBucketHeadsBufferId = (BufferID)72987;
        private const BufferID DearLieSurfaceBucketNextBufferId = (BufferID)72988;
        private const BufferID DearLieUnderwaterBucketHeadsBufferId = (BufferID)72989;
        private const BufferID DearLieUnderwaterBucketNextBufferId = (BufferID)72990;
        private const int DearLieVaultJobBufferCount = 11;
        private const string NativeMemoryOwner = nameof(DestructibleOrganicManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const Allocator DataVaultExemptOrganicHealthStateAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOrganicDestroyedStateAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOrganicScratchAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOrganicYieldQueueAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOrganicDropAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptHarvestTemplateAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptYieldMaterialAllocator = Allocator.Persistent;

        private enum HarvestState : byte
        {
            Pristine = 0,
            PartiallyHarvested = 1,
            Bare = 2,
            Dead = 3
        }

        private struct CorpseResourceNodeRecord
        {
            public uint NodeId;
            public uint ContaminatedItemHash;
            public int SpeciesId;
            public AbsoluteUniversePosition PositionAup;
            public Vector3 Position;
            public float InitialUnits;
            public float RemainingUnits;
            public float BloodIntensity;
            public float SpawnTime;
            public float ExpireTime;
            public byte Active;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct FloraDestructionEventDTO
        {
            [FieldOffset(0)] public double3 ImpactAUP;
            [FieldOffset(24)] public uint FloraTypeHash;
            [FieldOffset(28)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct FloraDearLieDestructionResult
        {
            [FieldOffset(0)] public Matrix4x4 OriginalMatrix;
            [FieldOffset(64)] public double3 ImpactAUP;
            [FieldOffset(88)] public uint InstanceUid;
            [FieldOffset(92)] public int ActiveIndex;
            [FieldOffset(96)] public uint FloraTypeHash;
            [FieldOffset(100)] public uint MagnitudeBits;
            [FieldOffset(104)] public ushort VfxQuantity;
            [FieldOffset(106)] public byte EmitVfx;
            [FieldOffset(107)] public byte MaterialClass;
            [FieldOffset(108)] private uint _pad0;
            [FieldOffset(112)] private ulong _pad1;
            [FieldOffset(120)] private ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieCounter64
        {
            [FieldOffset(0)] public int Value;
            [FieldOffset(4)] private uint _pad0;
            [FieldOffset(8)] private ulong _pad1;
            [FieldOffset(16)] private ulong _pad2;
            [FieldOffset(24)] private ulong _pad3;
            [FieldOffset(32)] private ulong _pad4;
            [FieldOffset(40)] private ulong _pad5;
            [FieldOffset(48)] private ulong _pad6;
            [FieldOffset(56)] private ulong _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieClaim64
        {
            [FieldOffset(0)] public int Claimed;
            [FieldOffset(4)] private uint _pad0;
            [FieldOffset(8)] private ulong _pad1;
            [FieldOffset(16)] private ulong _pad2;
            [FieldOffset(24)] private ulong _pad3;
            [FieldOffset(32)] private ulong _pad4;
            [FieldOffset(40)] private ulong _pad5;
            [FieldOffset(48)] private ulong _pad6;
            [FieldOffset(56)] private ulong _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct FloraDearLieRegenRecord
        {
            [FieldOffset(0)] public Matrix4x4 OriginalMatrix;
            [FieldOffset(64)] public uint InstanceUid;
            [FieldOffset(68)] public int ActiveIndex;
            [FieldOffset(72)] public float RestoreTimeSeconds;
            [FieldOffset(76)] public float3 RuntimePosition;
            [FieldOffset(88)] public byte Underwater;
            [FieldOffset(89)] private byte _pad0;
            [FieldOffset(90)] private ushort _pad1;
            [FieldOffset(92)] private uint _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieTelemetryEntry
        {
            [FieldOffset(0)] public int FrameIndex;
            [FieldOffset(4)] public int SurfaceCount;
            [FieldOffset(8)] public int UnderwaterCount;
            [FieldOffset(12)] public int DamageSignalCount;
            [FieldOffset(16)] public int DestroyedCount;
            [FieldOffset(20)] public int VfxSignalCount;
            [FieldOffset(24)] public int RegenQueuedCount;
            [FieldOffset(28)] public int RecoveredCount;
            [FieldOffset(32)] public int RejectedSignalCount;
            [FieldOffset(36)] public int NanRejectCount;
            [FieldOffset(40)] public float GlobalQualityWeight;
            [FieldOffset(44)] public uint Hash;
            [FieldOffset(48)] public uint LastInstanceUid;
            [FieldOffset(52)] public byte Flags;
            [FieldOffset(53)] private byte _pad0;
            [FieldOffset(54)] private ushort _pad1;
            [FieldOffset(56)] public float QueryMicroseconds;
            [FieldOffset(60)] private uint _pad2;
        }

        private readonly struct SporeAcousticEvent
        {
            public readonly AbsoluteUniversePosition PositionAup;
            public readonly Vector3 RuntimePosition;
            public readonly AudioClip Clip;
            public readonly float PulseFrequencyHz;
            public readonly float Volume;
            public readonly float Pitch;
            public readonly float SimulationTimeSeconds;
            public readonly float PhaseOffset01;
            public readonly bool HasAup;

            public SporeAcousticEvent(
                AbsoluteUniversePosition positionAup,
                Vector3 runtimePosition,
                AudioClip clip,
                float pulseFrequencyHz,
                float volume,
                float pitch,
                float simulationTimeSeconds,
                float phaseOffset01,
                bool hasAup)
            {
                PositionAup = positionAup;
                RuntimePosition = runtimePosition;
                Clip = clip;
                PulseFrequencyHz = pulseFrequencyHz;
                Volume = volume;
                Pitch = pitch;
                SimulationTimeSeconds = simulationTimeSeconds;
                PhaseOffset01 = phaseOffset01;
                HasAup = hasAup;
            }
        }

        private readonly struct HarvestAudioEvent
        {
            public readonly AbsoluteUniversePosition PositionAup;
            public readonly Vector3 RuntimePosition;
            public readonly AudioClip Clip;
            public readonly float Volume;
            public readonly float Pitch;
            public readonly bool HasAup;

            public HarvestAudioEvent(
                AbsoluteUniversePosition positionAup,
                Vector3 runtimePosition,
                AudioClip clip,
                float volume,
                float pitch,
                bool hasAup)
            {
                PositionAup = positionAup;
                RuntimePosition = runtimePosition;
                Clip = clip;
                Volume = volume;
                Pitch = pitch;
                HasAup = hasAup;
            }
        }

        private void EvaluateAggressiveOvergrowth(float currentTime)
        {
            EvaluateAggressiveOvergrowthInLane(false, currentTime, ref _surfaceOvergrowthScanCursor);
            EvaluateAggressiveOvergrowthInLane(true, currentTime, ref _underwaterOvergrowthScanCursor);
        }

        private void EvaluateAggressiveOvergrowthInLane(bool underwater, float currentTime, ref int cursor)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                !_lastOrganicTouchTimeByInstanceUid.IsCreated ||
                !_overgrownByInstanceUid.IsCreated ||
                count <= 0)
            {
                cursor = 0;
                return;
            }

            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length)))));
            if (safeCount <= 0)
            {
                cursor = 0;
                return;
            }

            cursor = math.clamp(cursor, 0, safeCount - 1);
            int checks = math.min(OvergrowthScanBudgetPerSlowTick, safeCount);
            for (int step = 0; step < checks; step++)
            {
                int activeIndex = (cursor + step) % safeCount;
                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (float)health[activeIndex] <= 0.0001f ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    _overgrownByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
                if (!IsConsumableFloraMaterialClass(materialClass))
                    continue;

                if (!_lastOrganicTouchTimeByInstanceUid.TryGetValue(instanceUid, out float lastTouchTime))
                {
                    _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
                    continue;
                }

                if (currentTime - lastTouchTime < OvergrowthUntouchedSeconds)
                    continue;

                if (!TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents))
                    continue;

                VoxelDynamicNavGridRuntime.EnqueueDynamicObstacleGrowth(
                    navObstacleCenter,
                    navObstacleExtents,
                    OvergrowthExpansionMeters);
                _overgrownByInstanceUid.TryAdd(instanceUid, 1);
                TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }

            cursor = (cursor + checks) % safeCount;
        }

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Authoritative indirect-flora bridge that owns the streamed native instance payloads.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional flora interaction manager used to publish localized tool-impact bend bursts.")]
        private FloraInteractionManager floraInteractionManager;

        [Header("Templates")]
        [SerializeField]
        [Tooltip("Authored harvest templates resolved by material class.")]
        private HarvestableTemplate[] harvestTemplates;

        [Header("Debris")]
        [SerializeField]
        [Tooltip("Burst debris profile used for kelp-family destruction.")]
        private OrganicDebrisProfile kelpDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for coral-family destruction.")]
        private OrganicDebrisProfile coralDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for metallic outcrop destruction.")]
        private OrganicDebrisProfile titaniumDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for surface sargassum destruction.")]
        private OrganicDebrisProfile sargassumDebrisProfile;

        [Header("Harvest Query")]
        [SerializeField, Min(MinimumSearchRadius)]
        [Tooltip("Base world-space radius used when resolving a tool hit against the active indirect flora arrays.")]
        private float hitSearchRadius = 1.25f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Extra world-space radius added when resolving tall kelp silhouettes.")]
        private float kelpHeightTolerance = 0.4f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Radius of the published flora-interaction burst when a tool hits a harvestable instance.")]
        private float interactionBurstRadius = 1.4f;

        [Header("Allelopathy")]
        [SerializeField, Min(1f)]
        [Tooltip("Planar kelp-density cell radius used when evaluating overcrowding-driven allelopathic coral suppression.")]
        private float allelopathicCellRadius = 14f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum macro-kelp count treated as a full cell when evaluating the 95% overcrowding threshold.")]
        private int allelopathicKelpCapacity = 20;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Normalized kelp occupancy threshold above which competing coral in the same cell is forced into decomposition.")]
        private float allelopathicThreshold01 = 0.95f;

        [Header("Harvest Audio")]
        [SerializeField]
        [Tooltip("Organic-impact clip used when a soft flora harvest transition occurs.")]
        private AudioClip organicHarvestClip;

        [SerializeField]
        [Tooltip("Brittle snap/crack clip used when coral-like flora changes harvest state.")]
        private AudioClip brittleHarvestClip;

        [SerializeField]
        [Tooltip("Fibrous tear clip used when kelp- or vine-like flora changes harvest state.")]
        private AudioClip fibrousHarvestClip;

        [SerializeField]
        [Tooltip("Metallic fallback clip used when a flora template routes through the metallic acoustic lane.")]
        private AudioClip metallicHarvestClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base harvest audio volume applied to partial state changes before state-specific scaling.")]
        private float harvestAudioBaseVolume = 0.72f;

        [Header("Spore Acoustics")]
        [SerializeField]
        [Tooltip("Fallback hostile spore pulse clip used when a mature spore flora template has no authored clip.")]
        private AudioClip sporeAcousticFallbackClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fallback volume for mature spore acoustic pulses when the flora template leaves volume at zero.")]
        private float sporeAcousticFallbackVolume = 0.65f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Lower cadence guard for mature spore acoustic pulses. Actual cadence remains locked to 1 / PulseFrequency unless clamped by this value.")]
        private float sporeAcousticMinimumIntervalSeconds = 0.2f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum active flora instances checked per lane each Tick for mature spore acoustic cadence. This keeps large fields bounded.")]
        private int matureSporeAcousticScanBudgetPerTick = 64;
        private const int MaxPendingSporeAcousticEvents = 8;
        private const int MaxPendingHarvestAudioEvents = 8;

        [Header("Dear Lie Destruction")]
        [SerializeField]
        [Tooltip("Editor/runtime smoke hook: one frame generates 100 deterministic SignalBus-equivalent flora damage events around this component.")]
        private bool dearLieGenerateMockDamageBurst;

        [SerializeField]
        [Tooltip("Runtime-local center used by the mock Dear Lie damage generator.")]
        private Vector3 dearLieMockDamageCenter;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Dear Lie spatial damage epsilon in meters. Editor tuning surface; copied into Burst jobs as a scalar.")]
        private float dearLieDamageRadiusEpsilon = DearLieQueryRadiusMeters;

        [SerializeField, Range(5f, 900f)]
        [Tooltip("Visual-only Dear Lie regeneration delay in seconds. Editor tuning surface; copied into the native regen queue as a scalar timestamp.")]
        private float dearLieRegenerationDelaySeconds = DearLieRegenerationDelaySeconds;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("-1 uses HomeostasisBrain.GlobalQualityWeight. 0..1 overrides Dear Lie VFX gating for editor stress tests.")]
        private float dearLieQualityOverride = -1f;

        private NativeArray<uint> _surfaceInstanceUids;
        private NativeArray<uint> _underwaterInstanceUids;
        private NativeArray<byte> _surfaceMaterialClasses;
        private NativeArray<byte> _underwaterMaterialClasses;
        private NativeArray<Unity.Mathematics.half> _surfaceHealth;
        private NativeArray<Unity.Mathematics.half> _underwaterHealth;
        private NativeArray<int> _surfaceDearLieBucketHeads;
        private NativeArray<int> _surfaceDearLieBucketNext;
        private NativeArray<int> _underwaterDearLieBucketHeads;
        private NativeArray<int> _underwaterDearLieBucketNext;
        private NativeArray<FloraDearLieClaim64> _surfaceDearLieClaims;
        private NativeArray<FloraDearLieClaim64> _underwaterDearLieClaims;
        private NativeArray<FloraDestructionEventDTO> _dearLieDamageEvents;
        private NativeArray<FloraDearLieDestructionResult> _dearLieResults;
        private NativeArray<FloraDearLieCounter64> _dearLieCounters;
        private NativeArray<FloraDearLieRegenRecord> _dearLieRegenRecords;
        private NativeArray<FloraDearLieTelemetryEntry> _dearLieTelemetryRing;
        private IDataVault _dearLieVault;
        private VaultGenerationHandle<FloraDearLieClaim64> _surfaceDearLieClaimsHandle;
        private VaultGenerationHandle<FloraDearLieClaim64> _underwaterDearLieClaimsHandle;
        private VaultGenerationHandle<FloraDestructionEventDTO> _dearLieDamageEventsHandle;
        private VaultGenerationHandle<FloraDearLieDestructionResult> _dearLieResultsHandle;
        private VaultGenerationHandle<FloraDearLieCounter64> _dearLieCountersHandle;
        private VaultGenerationHandle<FloraDearLieRegenRecord> _dearLieRegenRecordsHandle;
        private VaultGenerationHandle<FloraDearLieTelemetryEntry> _dearLieTelemetryRingHandle;
        private VaultGenerationHandle<int> _surfaceDearLieBucketHeadsHandle;
        private VaultGenerationHandle<int> _surfaceDearLieBucketNextHandle;
        private VaultGenerationHandle<int> _underwaterDearLieBucketHeadsHandle;
        private VaultGenerationHandle<int> _underwaterDearLieBucketNextHandle;
        private NativeHashMap<uint, Unity.Mathematics.half> _healthByInstanceUid;
        private NativeHashMap<uint, byte> _destroyedByInstanceUid;
        private NativeHashMap<uint, float> _pendingWiltEndTimeByInstanceUid;
        private NativeHashMap<uint, float> _damageVisualProgressByInstanceUid;
        private NativeHashMap<uint, float> _decompositionStartTimeByInstanceUid;
        private NativeHashMap<uint, float> _regrowthProgressByInstanceUid;
        private NativeHashMap<uint, float3> _regrowthPositionByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _maturationScaleByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _maturationYieldByInstanceUid;
        private NativeHashMap<uint, float> _nextSporeAcousticTimeByInstanceUid;
        private NativeHashMap<uint, float2> _baseScaleByInstanceUid;
        private NativeHashMap<uint, byte> _runtimeFlagsByInstanceUid;
        private NativeHashMap<uint, float> _lastOrganicTouchTimeByInstanceUid;
        private NativeHashMap<uint, byte> _overgrownByInstanceUid;
        private NativeHashMap<uint, byte> _rootMoundAppliedByInstanceUid;
        private NativeList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private NativeList<PersistentWorldDeltaRecord> _floraStateOverrideScratch;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHealth01ByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHeightScale01ByInstanceUid;
        private NativeList<DestroyedOrganicEvent> _pendingYieldEvents;
        private NativeArray<DestroyedOrganicEvent> _yieldJobInput;
        private DropBuffer _dropBuffer;
        private NativeArray<HarvestableTemplate.RuntimeDescriptor> _templateDescriptors;
        private NativeArray<HarvestableTemplate.LootRuntimeEntry> _lootEntries;
        private NativeArray<EntropyYieldMaterialLutEntry> _yieldMaterialLut;
        private NativeArray<Vector3> _dropDebugScratch;
        private JobHandle _yieldJobHandle;
        private JobHandle _dearLieJobHandle;
        private int _scheduledYieldCount;
        private int _dearLieScheduledDamageCount;
        private int _dearLieJobScheduleFrame = -1;
        private double _dearLieJobStartTimeSeconds;
        private int _deferredYieldScheduleFrame = -1;
        private int _surfaceRevision = -1;
        private int _underwaterRevision = -1;
        private int _surfaceCount;
        private int _underwaterCount;
        private int _dearLieRegenCount;
        private int _dearLieTelemetryCursor;
        private int _dearLieLastDamageFrame = -1;
        private int _dearLieLastDestroyedCount;
        private int _dearLieLastVfxCount;
        private float _dearLieLastQualityWeight;
        private float _dearLieFallbackQualityWeight = 0.25f;
        private double _organicClockSeconds;
        private Vector3 _dearLieLastImpactRuntimePosition;
        private Vector3 _dearLieLastTargetRuntimePosition;
        private byte _dearLieHasLastDebugHit;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _originShiftListenerRegistered;
        private bool _yieldScheduled;
        private bool _dearLieJobScheduled;
        private bool _dearLieVaultReady;
        private bool _dearLieVaultJobLocksHeld;
        private int _dearLieVaultJobLockCount;

        private NativeArray<Matrix4x4> _surfaceMatrices;
        private NativeArray<HectonVegetationInstanceData> _surfaceMetadata;
        private NativeArray<int> _surfaceTypes;
        private NativeArray<int>.ReadOnly _surfaceSemanticTypes;
        private NativeArray<Matrix4x4> _underwaterMatrices;
        private NativeArray<HectonVegetationInstanceData> _underwaterMetadata;
        private NativeArray<int> _underwaterTypes;
        private NativeArray<int>.ReadOnly _underwaterSemanticTypes;

        private int[] _templateIndexByMaterialClass;
        private int[] _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
        private HarvestableTemplate[] _descriptorHarvestTemplates = Array.Empty<HarvestableTemplate>();
        private byte[] _floraCategoryByDescriptorIndex = Array.Empty<byte>();
        private byte[] _audioMaterialByDescriptorIndex = Array.Empty<byte>();
        private float[] _growthTimeSecondsByDescriptorIndex = Array.Empty<float>();
        private byte[] _sporeAcousticEmitterByDescriptorIndex = Array.Empty<byte>();
        private AudioClip[] _sporeAcousticClipByDescriptorIndex = Array.Empty<AudioClip>();
        private float[] _sporePulseFrequencyByDescriptorIndex = Array.Empty<float>();
        private float[] _sporeAcousticVolumeByDescriptorIndex = Array.Empty<float>();
        private int _surfaceMatureSporeScanCursor;
        private int _underwaterMatureSporeScanCursor;
        private int _surfaceOvergrowthScanCursor;
        private int _underwaterOvergrowthScanCursor;
        private IPlayerInventoryService _playerInventoryService;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private IAudioService _audioService;
        private ISpatialAudioHarvestPlaybackSink _harvestAudioSink;
        // COLD ALLOC: HarvestAudioEvent[8] - bounded VISUAL_SYNC audio queue for harvest transitions - owner: DestructibleOrganicManager
        private readonly HarvestAudioEvent[] _pendingHarvestAudioEvents = new HarvestAudioEvent[MaxPendingHarvestAudioEvents];
        // COLD ALLOC: SporeAcousticEvent[8] - bounded VISUAL_SYNC audio queue for mature spore pulses - owner: DestructibleOrganicManager
        private readonly SporeAcousticEvent[] _pendingSporeAcousticEvents = new SporeAcousticEvent[MaxPendingSporeAcousticEvents];
        private int _pendingHarvestAudioEventCount;
        private int _pendingSporeAcousticEventCount;
        private bool _hotSwapRegistered;
        private bool _organicToolHitServiceRegistered;
        // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
        private CorpseResourceNodeRecord[] _corpseResourceNodes = Array.Empty<CorpseResourceNodeRecord>();
        private int _corpseResourceNodeCount;

        /// <summary>Currently enabled runtime organic entropy owner.</summary>
        public static DestructibleOrganicManager ActiveRuntimeInstance => _activeRuntimeInstance;

        public int DearLieRegenQueueCount => _dearLieRegenCount;
        public int DearLieLastDamageFrame => _dearLieLastDamageFrame;
        public int DearLieLastDestroyedCount => _dearLieLastDestroyedCount;
        public int DearLieLastVfxCount => _dearLieLastVfxCount;
        public int DearLieSurfaceInstanceCount => _surfaceCount;
        public int DearLieUnderwaterInstanceCount => _underwaterCount;
        public float DearLieQualityWeight => _dearLieLastQualityWeight;
        public float DearLieDamageRadiusEpsilon => ResolveDearLieQueryRadius();
        public float DearLieRegenerationDelayTuningSeconds => ResolveDearLieRegenerationDelaySeconds();
        public float DearLieQualityOverride => dearLieQualityOverride;

#if UNITY_EDITOR
        public void EditorSetDearLieTuning(float damageRadiusEpsilon, float regenerationDelaySeconds, float qualityOverride)
        {
            dearLieDamageRadiusEpsilon = math.clamp(
                math.select(DearLieQueryRadiusMeters, damageRadiusEpsilon, math.isfinite(damageRadiusEpsilon)),
                0.25f,
                8f);
            dearLieRegenerationDelaySeconds = math.clamp(
                math.select(DearLieRegenerationDelaySeconds, regenerationDelaySeconds, math.isfinite(regenerationDelaySeconds)),
                5f,
                900f);
            dearLieQualityOverride = math.clamp(
                math.select(-1f, qualityOverride, math.isfinite(qualityOverride)),
                -1f,
                1f);
        }

        public void EditorRequestDearLieMockBurst()
        {
            dearLieGenerateMockDamageBurst = true;
        }

        public int EditorCopyDearLieTelemetry(
            Span<int> frameIndices,
            Span<int> destroyedCounts,
            Span<int> vfxCounts,
            Span<int> regenCounts)
        {
            if (!_dearLieTelemetryRing.IsCreated || _dearLieTelemetryRing.Length == 0)
                return 0;

            int copyCount = math.min(_dearLieTelemetryCursor, _dearLieTelemetryRing.Length);
            copyCount = math.min(copyCount, math.min(frameIndices.Length, math.min(destroyedCounts.Length, math.min(vfxCounts.Length, regenCounts.Length))));
            int start = _dearLieTelemetryCursor - copyCount;
            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = (start + i) % _dearLieTelemetryRing.Length;
                if (ringIndex < 0)
                    ringIndex += _dearLieTelemetryRing.Length;

                FloraDearLieTelemetryEntry entry = _dearLieTelemetryRing[ringIndex];
                frameIndices[i] = entry.FrameIndex;
                destroyedCounts[i] = entry.DestroyedCount;
                vfxCounts[i] = entry.VfxSignalCount;
                regenCounts[i] = entry.RegenQueuedCount;
            }

            return copyCount;
        }
#endif

        internal bool RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            return RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, 0u);
        }

        internal bool RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return false;

            return RegisterCorpseResourceNode(in positionAup, worldPosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        internal bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            return RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, 0u);
        }

        internal bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            Vector3 runtimePosition = positionAup.ToRuntimeFloat3();
            return RegisterCorpseResourceNode(in positionAup, runtimePosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        private bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0 || capacityUnits <= 0f)
                return false;

            int writeIndex = -1;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                if (_corpseResourceNodes[i].Active != 0)
                    continue;

                writeIndex = i;
                break;
            }

            if (writeIndex < 0)
                writeIndex = FindWeakestCorpseNodeIndex();

            if (writeIndex < 0)
                return false;

            float initialUnits = Mathf.Max(0.25f, capacityUnits);
            float currentTime = ResolveOrganicClockSeconds();
            CorpseResourceNodeRecord record = new CorpseResourceNodeRecord
            {
                NodeId = (uint)(PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in positionAup) & uint.MaxValue),
                ContaminatedItemHash = contaminatedItemHash,
                SpeciesId = speciesId,
                PositionAup = positionAup,
                Position = worldPosition,
                InitialUnits = initialUnits,
                RemainingUnits = initialUnits,
                BloodIntensity = DefaultCorpseBloodIntensity,
                SpawnTime = currentTime,
                ExpireTime = currentTime + OrganicDecompositionDurationSeconds,
                Active = 1
            };
            _corpseResourceNodes[writeIndex] = record;
            if (writeIndex >= _corpseResourceNodeCount)
                _corpseResourceNodeCount = writeIndex + 1;

            ChemicalInfluenceGrid.QueueBloodScent(worldPosition, record.BloodIntensity);
            return true;
        }

        internal bool TryResolveNearestCorpseResourceNode(Vector3 worldPosition, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
            {
                corpsePosition = default;
                corpseNodeId = 0u;
                return false;
            }

            return TryResolveNearestCorpseResourceNode(in queryAup, searchRadius, out corpsePosition, out corpseNodeId);
        }

        internal bool TryResolveNearestCorpseResourceNode(in AbsoluteUniversePosition queryAup, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double bestDistanceSq = (double)searchRadius * searchRadius;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                corpsePosition = record.Position;
                corpseNodeId = record.NodeId;
            }

            return corpseNodeId != 0u;
        }

        internal bool TryConsumeCorpseResourceNode(uint corpseNodeId, float consumeUnits)
        {
            if (corpseNodeId == 0u || consumeUnits <= 0f || _corpseResourceNodes == null)
                return false;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != corpseNodeId)
                    continue;

                record.RemainingUnits = Mathf.Max(0f, record.RemainingUnits - consumeUnits);
                if (record.RemainingUnits <= 0.001f)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                }
                else
                {
                    record.BloodIntensity = math.lerp(0.35f, DefaultCorpseBloodIntensity, ResolveCorpseCapacityFraction01(in record));
                }

                _corpseResourceNodes[i] = record;
                TrimTrailingCorpseNodes();
                return true;
            }

            return false;
        }

        internal bool TryResolveCorpseContaminatedItemHash(uint corpseNodeId, out uint itemHash)
        {
            itemHash = 0u;
            if (corpseNodeId == 0u || _corpseResourceNodes == null)
                return false;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != corpseNodeId)
                    continue;

                itemHash = record.ContaminatedItemHash;
                return itemHash != 0u;
            }

            return false;
        }

        internal float ResolveCorpseSpawnInfluence01(Vector3 worldPosition, float searchRadius)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
                return 0f;

            return ResolveCorpseSpawnInfluence01(in queryAup, searchRadius);
        }

        internal float ResolveCorpseSpawnInfluence01(in AbsoluteUniversePosition queryAup, float searchRadius)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0 || searchRadius <= 0f)
                return 0f;

            double maxDistanceSq = (double)searchRadius * searchRadius;
            float bestInfluence01 = 0f;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > maxDistanceSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / maxDistanceSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                float influence01 = distance01 * mass01;
                if (influence01 > bestInfluence01)
                    bestInfluence01 = influence01;
            }

            return bestInfluence01;
        }

        internal bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double radiusSq = (double)CorpseDiseaseRadiusMeters * CorpseDiseaseRadiusMeters;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 ||
                    record.RemainingUnits <= 0f ||
                    currentTimeSeconds - record.SpawnTime < CorpseDiseaseActivationSeconds)
                {
                    continue;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > radiusSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / radiusSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                severity01 = Mathf.Max(severity01, distance01 * mass01 * CorpseDiseaseSeverity);
                sourcePosition = record.Position;
            }

            return severity01 > 0.001f;
        }

        private void Awake()
        {
            _activeRuntimeInstance = this;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            hitSearchRadius = Mathf.Max(MinimumSearchRadius, hitSearchRadius);
            kelpHeightTolerance = Mathf.Max(0.05f, kelpHeightTolerance);
            interactionBurstRadius = Mathf.Max(0.05f, interactionBurstRadius);
            allelopathicCellRadius = Mathf.Max(1f, allelopathicCellRadius);
            allelopathicKelpCapacity = Mathf.Max(1, allelopathicKelpCapacity);
            allelopathicThreshold01 = Mathf.Clamp(allelopathicThreshold01, 0.5f, 1f);
            harvestAudioBaseVolume = Mathf.Clamp01(harvestAudioBaseVolume);
            sporeAcousticFallbackVolume = Mathf.Clamp01(sporeAcousticFallbackVolume);
            sporeAcousticMinimumIntervalSeconds = Mathf.Max(0.05f, sporeAcousticMinimumIntervalSeconds);
            matureSporeAcousticScanBudgetPerTick = Mathf.Max(1, matureSporeAcousticScanBudgetPerTick);

            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persistent per-instance harvest health state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _healthByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,byte>[2048] - persistent destroyed flora tombstone set keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _destroyedByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active wilt-to-hide timers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _pendingWiltEndTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent partial-damage wilt progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _damageVisualProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent decomposition start time keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _decompositionStartTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active flora regrowth progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float3>[2048] - flora regrowth position overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthPositionByInstanceUid = new NativeHashMap<uint, float3>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicDestroyedStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - live flora maturation scale multipliers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _maturationScaleByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - live flora maturation resource-yield multipliers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _maturationYieldByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[4096] - mature spore acoustic cadence keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _nextSporeAcousticTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float2>[4096] - baseline height/width scales keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _baseScaleByInstanceUid = new NativeHashMap<uint, float2>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - runtime flora bit-mask flags keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _runtimeFlagsByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,float>[4096] - untouched flora clock keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _lastOrganicTouchTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - macro-flora overgrowth obstacle state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _overgrownByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - one-shot Titan root SDF mound state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _rootMoundAppliedByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - destroyed flora tombstone restore scratch - owner: DestructibleOrganicManager
            _destroyedFloraScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedDestroyedCapacity, DataVaultExemptOrganicScratchAllocator);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[4096] - partial flora-state restore scratch - owner: DestructibleOrganicManager
            _floraStateOverrideScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicScratchAllocator);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora health overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHealth01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora height overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHeightScale01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, DataVaultExemptOrganicHealthStateAllocator);
            // COLD ALLOC: NativeList<DestroyedOrganicEvent>[128] - pending entropy yield event queue - owner: DestructibleOrganicManager
            _pendingYieldEvents = new NativeList<DestroyedOrganicEvent>(DefaultPendingYieldCapacity, DataVaultExemptOrganicYieldQueueAllocator);
            _dropBuffer = new DropBuffer(DefaultDropBufferCapacity, DataVaultExemptOrganicDropAllocator);
            // COLD ALLOC: Vector3[1] - bounded debug scratch for future runtime diagnostics - owner: DestructibleOrganicManager
            _dropDebugScratch = new NativeArray<Vector3>(1, DataVaultExemptOrganicDropAllocator, NativeArrayOptions.ClearMemory);
            // VAULT ROUTE: Dear Lie transient visual lanes are acquired from GlobalDataVault during OnEnable; Awake keeps no private Dear Lie native-container ownership.
            RegisterNativeMemorySentinel();
            // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
            _corpseResourceNodes = new CorpseResourceNodeRecord[DefaultCorpseNodeCapacity];
            _corpseResourceNodeCount = 0;

            BuildTemplateCaches();
            BuildYieldMaterialLut();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheRegistryServicesCold();
            TryBootstrapDearLieVault(clearExisting: true);
            TryRegisterHotSwapListener();
            RegisterOriginShiftListener();
            TryRegisterOrganicToolHitService();

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
            {
                _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_slowTickRegistered)
            {
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_lateFrameTickRegistered)
            {
                _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
            BuildFloraTemplateHarvestMap();
            RefreshActiveCachesIfNeeded(force: true);
        }

        private void OnDisable()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameTickRegistered = false;
            }

            CompleteDearLieJobIfNeeded(ResolveOrganicClockSeconds());
            CompleteYieldJobIfNeeded();
            UnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregisterOrganicToolHitService();
            ReleaseDearLieVaultBuffers(_dearLieVault);
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;

            CompleteDearLieJobIfNeeded(ResolveOrganicClockSeconds());
            CompleteYieldJobIfNeeded();
            UnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregisterOrganicToolHitService();
            ReleaseDearLieVaultBuffers(_dearLieVault);
            ClearCachedRegistryServices();
            DisposeNativeArray(ref _surfaceInstanceUids);
            DisposeNativeArray(ref _underwaterInstanceUids);
            DisposeNativeArray(ref _surfaceMaterialClasses);
            DisposeNativeArray(ref _underwaterMaterialClasses);
            DisposeNativeArray(ref _surfaceHealth);
            DisposeNativeArray(ref _underwaterHealth);
            DisposeNativeArray(ref _yieldJobInput);
            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            DisposeNativeArray(ref _yieldMaterialLut);
            DisposeNativeArray(ref _dropDebugScratch);

            if (_dropBuffer.IsCreated)
                _dropBuffer.Dispose();

            DisposeNativeHashMap(ref _healthByInstanceUid, nameof(_healthByInstanceUid));
            DisposeNativeHashMap(ref _destroyedByInstanceUid, nameof(_destroyedByInstanceUid));
            DisposeNativeHashMap(ref _persistedHealth01ByInstanceUid, nameof(_persistedHealth01ByInstanceUid));
            DisposeNativeHashMap(ref _persistedHeightScale01ByInstanceUid, nameof(_persistedHeightScale01ByInstanceUid));
            DisposeNativeHashMap(ref _pendingWiltEndTimeByInstanceUid, nameof(_pendingWiltEndTimeByInstanceUid));
            DisposeNativeHashMap(ref _damageVisualProgressByInstanceUid, nameof(_damageVisualProgressByInstanceUid));
            DisposeNativeHashMap(ref _maturationScaleByInstanceUid, nameof(_maturationScaleByInstanceUid));
            DisposeNativeHashMap(ref _maturationYieldByInstanceUid, nameof(_maturationYieldByInstanceUid));
            DisposeNativeHashMap(ref _nextSporeAcousticTimeByInstanceUid, nameof(_nextSporeAcousticTimeByInstanceUid));
            DisposeNativeHashMap(ref _decompositionStartTimeByInstanceUid, nameof(_decompositionStartTimeByInstanceUid));
            DisposeNativeHashMap(ref _regrowthProgressByInstanceUid, nameof(_regrowthProgressByInstanceUid));
            DisposeNativeHashMap(ref _regrowthPositionByInstanceUid, nameof(_regrowthPositionByInstanceUid));
            DisposeNativeHashMap(ref _baseScaleByInstanceUid, nameof(_baseScaleByInstanceUid));
            DisposeNativeHashMap(ref _runtimeFlagsByInstanceUid, nameof(_runtimeFlagsByInstanceUid));
            DisposeNativeHashMap(ref _lastOrganicTouchTimeByInstanceUid, nameof(_lastOrganicTouchTimeByInstanceUid));
            DisposeNativeHashMap(ref _overgrownByInstanceUid, nameof(_overgrownByInstanceUid));
            DisposeNativeHashMap(ref _rootMoundAppliedByInstanceUid, nameof(_rootMoundAppliedByInstanceUid));
            DisposeNativeList(ref _destroyedFloraScratch, nameof(_destroyedFloraScratch));
            DisposeNativeList(ref _floraStateOverrideScratch, nameof(_floraStateOverrideScratch));
            DisposeNativeList(ref _pendingYieldEvents, nameof(_pendingYieldEvents));
        }

        /// <summary>
        /// Rebuilds live corpse attractor runtime caches from authoritative Absolute Universe Positions after a committed origin shift.
        /// </summary>
        /// <param name="shiftData">Committed floating-origin shift data.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                float3 runtimePosition = AUPMath.ToRuntimeFloat3(in record.PositionAup, committedOriginOffset);
                record.Position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                _corpseResourceNodes[i] = record;
            }
        }

        private void RegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault currentVault = currentService as IDataVault;
                    if (currentVault != null && ReferenceEquals(_dearLieVault, currentVault))
                    {
                        TryBootstrapDearLieVault(clearExisting: false);
                        break;
                    }

                    if (_dearLieVault != null)
                        ReleaseDearLieVaultBuffers(_dearLieVault);

                    _dearLieVault = currentVault;
                    _dearLieVaultReady = false;
                    if (_dearLieVault != null)
                        TryBootstrapDearLieVault(clearExisting: true);
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterOrganicToolHitService()
        {
            if (_organicToolHitServiceRegistered || !Application.isPlaying)
                return;

            IOrganicToolHitService registered = GlobalRegistry.OrganicToolHits;
            if (registered != null && !ReferenceEquals(registered, this))
                return;

            GlobalRegistry.RegisterOrganicToolHitService(this);
            _organicToolHitServiceRegistered = ReferenceEquals(GlobalRegistry.OrganicToolHits, this);
        }

        private void TryUnregisterOrganicToolHitService()
        {
            if (!_organicToolHitServiceRegistered)
                return;

            GlobalRegistry.UnregisterOrganicToolHitService(this);
            _organicToolHitServiceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _dearLieVault = GlobalRegistry.DataVault;
            CacheAudioService(Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance);
            CacheDearLieFallbackQualityWeightCold();
        }

        private bool TryBootstrapDearLieVault(bool clearExisting)
        {
            IDataVault vault = _dearLieVault;
            if (vault == null)
            {
                _dearLieVaultReady = false;
                return false;
            }

            _dearLieVaultReady = EnsureDearLieVaultBuffers(vault, clearExisting) && TryResolveDearLieVaultBuffers(vault);
            if (clearExisting && _dearLieVaultReady)
                ClearDearLieVaultRuntimeState();

            return _dearLieVaultReady;
        }

        private bool EnsureDearLieVaultBuffers(IDataVault vault, bool clearExisting)
        {
            if (vault == null)
                return false;

            NativeArrayOptions fixedOptions = clearExisting ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory;
            return EnsureDearLieVaultBuffer(vault, DearLieSurfaceClaimsBufferId, DearLieSpatialHashCapacity, ref _surfaceDearLieClaimsHandle, out _surfaceDearLieClaims, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieUnderwaterClaimsBufferId, DearLieSpatialHashCapacity, ref _underwaterDearLieClaimsHandle, out _underwaterDearLieClaims, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieDamageEventsBufferId, DearLieMaxDamageSignalsPerFrame, ref _dearLieDamageEventsHandle, out _dearLieDamageEvents, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieResultsBufferId, DearLieMaxResultsPerFrame, ref _dearLieResultsHandle, out _dearLieResults, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieCountersBufferId, 8, ref _dearLieCountersHandle, out _dearLieCounters, NativeArrayOptions.ClearMemory) &&
                   EnsureDearLieVaultBuffer(vault, DearLieRegenRecordsBufferId, DearLieMaxRegenRecords, ref _dearLieRegenRecordsHandle, out _dearLieRegenRecords, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieTelemetryRingBufferId, DearLieTelemetryFrameCount, ref _dearLieTelemetryRingHandle, out _dearLieTelemetryRing, NativeArrayOptions.ClearMemory) &&
                   EnsureDearLieVaultBuffer(vault, DearLieSurfaceBucketHeadsBufferId, DearLieSpatialHashCapacity, ref _surfaceDearLieBucketHeadsHandle, out _surfaceDearLieBucketHeads, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieSurfaceBucketNextBufferId, DearLieSpatialHashCapacity, ref _surfaceDearLieBucketNextHandle, out _surfaceDearLieBucketNext, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieUnderwaterBucketHeadsBufferId, DearLieSpatialHashCapacity, ref _underwaterDearLieBucketHeadsHandle, out _underwaterDearLieBucketHeads, fixedOptions) &&
                   EnsureDearLieVaultBuffer(vault, DearLieUnderwaterBucketNextBufferId, DearLieSpatialHashCapacity, ref _underwaterDearLieBucketNextHandle, out _underwaterDearLieBucketNext, fixedOptions);
        }

        private bool TryResolveDearLieVaultBuffers(IDataVault vault)
        {
            return TryResolveDearLieVaultBuffer(vault, in _surfaceDearLieClaimsHandle, DearLieSpatialHashCapacity, out _surfaceDearLieClaims) &&
                   TryResolveDearLieVaultBuffer(vault, in _underwaterDearLieClaimsHandle, DearLieSpatialHashCapacity, out _underwaterDearLieClaims) &&
                   TryResolveDearLieVaultBuffer(vault, in _dearLieDamageEventsHandle, DearLieMaxDamageSignalsPerFrame, out _dearLieDamageEvents) &&
                   TryResolveDearLieVaultBuffer(vault, in _dearLieResultsHandle, DearLieMaxResultsPerFrame, out _dearLieResults) &&
                   TryResolveDearLieVaultBuffer(vault, in _dearLieCountersHandle, 8, out _dearLieCounters) &&
                   TryResolveDearLieVaultBuffer(vault, in _dearLieRegenRecordsHandle, DearLieMaxRegenRecords, out _dearLieRegenRecords) &&
                   TryResolveDearLieVaultBuffer(vault, in _dearLieTelemetryRingHandle, DearLieTelemetryFrameCount, out _dearLieTelemetryRing) &&
                   TryResolveDearLieVaultBuffer(vault, in _surfaceDearLieBucketHeadsHandle, DearLieSpatialHashCapacity, out _surfaceDearLieBucketHeads) &&
                   TryResolveDearLieVaultBuffer(vault, in _surfaceDearLieBucketNextHandle, DearLieSpatialHashCapacity, out _surfaceDearLieBucketNext) &&
                   TryResolveDearLieVaultBuffer(vault, in _underwaterDearLieBucketHeadsHandle, DearLieSpatialHashCapacity, out _underwaterDearLieBucketHeads) &&
                   TryResolveDearLieVaultBuffer(vault, in _underwaterDearLieBucketNextHandle, DearLieSpatialHashCapacity, out _underwaterDearLieBucketNext);
        }

        private bool EnsureDearLieVaultLaneCapacity(bool underwater, int requiredCount)
        {
            if (requiredCount <= 0)
                return true;

            IDataVault vault = _dearLieVault;
            if (vault == null || !_dearLieVaultReady || _dearLieJobScheduled)
                return false;

            int requiredCapacity = math.max(DearLieSpatialHashCapacity, math.ceilpow2(requiredCount));
            if (underwater)
            {
                return EnsureDearLieVaultBuffer(vault, DearLieUnderwaterClaimsBufferId, requiredCapacity, ref _underwaterDearLieClaimsHandle, out _underwaterDearLieClaims, NativeArrayOptions.UninitializedMemory) &&
                       EnsureDearLieVaultBuffer(vault, DearLieUnderwaterBucketHeadsBufferId, requiredCapacity, ref _underwaterDearLieBucketHeadsHandle, out _underwaterDearLieBucketHeads, NativeArrayOptions.UninitializedMemory) &&
                       EnsureDearLieVaultBuffer(vault, DearLieUnderwaterBucketNextBufferId, requiredCapacity, ref _underwaterDearLieBucketNextHandle, out _underwaterDearLieBucketNext, NativeArrayOptions.UninitializedMemory);
            }

            return EnsureDearLieVaultBuffer(vault, DearLieSurfaceClaimsBufferId, requiredCapacity, ref _surfaceDearLieClaimsHandle, out _surfaceDearLieClaims, NativeArrayOptions.UninitializedMemory) &&
                   EnsureDearLieVaultBuffer(vault, DearLieSurfaceBucketHeadsBufferId, requiredCapacity, ref _surfaceDearLieBucketHeadsHandle, out _surfaceDearLieBucketHeads, NativeArrayOptions.UninitializedMemory) &&
                   EnsureDearLieVaultBuffer(vault, DearLieSurfaceBucketNextBufferId, requiredCapacity, ref _surfaceDearLieBucketNextHandle, out _surfaceDearLieBucketNext, NativeArrayOptions.UninitializedMemory);
        }

        private static bool EnsureDearLieVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer,
            NativeArrayOptions options) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.FloraGenomics, options);
            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryResolveDearLieVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ClearDearLieVaultRuntimeState()
        {
            ClearDearLieCounters();
            if (_dearLieTelemetryRing.IsCreated)
            {
                for (int i = 0; i < _dearLieTelemetryRing.Length; i++)
                    _dearLieTelemetryRing[i] = default;
            }

            if (_surfaceDearLieBucketHeads.IsCreated)
            {
                for (int i = 0; i < _surfaceDearLieBucketHeads.Length; i++)
                    _surfaceDearLieBucketHeads[i] = -1;
            }

            if (_underwaterDearLieBucketHeads.IsCreated)
            {
                for (int i = 0; i < _underwaterDearLieBucketHeads.Length; i++)
                    _underwaterDearLieBucketHeads[i] = -1;
            }

            _dearLieRegenCount = 0;
            _dearLieTelemetryCursor = 0;
        }

        private bool TryLockDearLieVaultJobBuffers()
        {
            IDataVault vault = _dearLieVault;
            if (vault == null || _dearLieVaultJobLocksHeld || _dearLieVaultJobLockCount != 0)
                return false;

            int lockedCount = 0;
            for (int i = 0; i < DearLieVaultJobBufferCount; i++)
            {
                BufferID bufferId = GetDearLieVaultJobBufferId(i);
                if (!vault.TryLockBuffer(bufferId, SystemID.FloraGenomics))
                {
                    UnlockDearLieVaultJobBuffers(vault, lockedCount);
                    return false;
                }

                lockedCount++;
            }

            _dearLieVaultJobLockCount = lockedCount;
            _dearLieVaultJobLocksHeld = true;
            return true;
        }

        private void UnlockDearLieVaultJobBuffers()
        {
            IDataVault vault = _dearLieVault;
            if (vault != null && _dearLieVaultJobLockCount > 0)
                UnlockDearLieVaultJobBuffers(vault, _dearLieVaultJobLockCount);

            _dearLieVaultJobLockCount = 0;
            _dearLieVaultJobLocksHeld = false;
        }

        private static void UnlockDearLieVaultJobBuffers(IDataVault vault, int lockedCount)
        {
            for (int i = lockedCount - 1; i >= 0; i--)
            {
                BufferID bufferId = GetDearLieVaultJobBufferId(i);
                if (bufferId != default)
                    vault.TryUnlockBuffer(bufferId, SystemID.FloraGenomics);
            }
        }

        private static BufferID GetDearLieVaultJobBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return DearLieSurfaceClaimsBufferId;
                case 1:
                    return DearLieUnderwaterClaimsBufferId;
                case 2:
                    return DearLieDamageEventsBufferId;
                case 3:
                    return DearLieResultsBufferId;
                case 4:
                    return DearLieCountersBufferId;
                case 5:
                    return DearLieRegenRecordsBufferId;
                case 6:
                    return DearLieTelemetryRingBufferId;
                case 7:
                    return DearLieSurfaceBucketHeadsBufferId;
                case 8:
                    return DearLieSurfaceBucketNextBufferId;
                case 9:
                    return DearLieUnderwaterBucketHeadsBufferId;
                case 10:
                    return DearLieUnderwaterBucketNextBufferId;
                default:
                    return default;
            }
        }

        private void ReleaseDearLieVaultBuffers(IDataVault vault)
        {
            if (_dearLieJobScheduled)
                CompleteDearLieJobIfNeeded(ResolveOrganicClockSeconds(), force: true);

            if (_dearLieVaultJobLocksHeld)
                UnlockDearLieVaultJobBuffers();

            if (vault != null)
            {
                ReleaseDearLieVaultBuffer(vault, ref _surfaceDearLieClaimsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _underwaterDearLieClaimsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _dearLieDamageEventsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _dearLieResultsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _dearLieCountersHandle);
                ReleaseDearLieVaultBuffer(vault, ref _dearLieRegenRecordsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _dearLieTelemetryRingHandle);
                ReleaseDearLieVaultBuffer(vault, ref _surfaceDearLieBucketHeadsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _surfaceDearLieBucketNextHandle);
                ReleaseDearLieVaultBuffer(vault, ref _underwaterDearLieBucketHeadsHandle);
                ReleaseDearLieVaultBuffer(vault, ref _underwaterDearLieBucketNextHandle);
            }

            _surfaceDearLieClaims = default;
            _underwaterDearLieClaims = default;
            _dearLieDamageEvents = default;
            _dearLieResults = default;
            _dearLieCounters = default;
            _dearLieRegenRecords = default;
            _dearLieTelemetryRing = default;
            _surfaceDearLieBucketHeads = default;
            _surfaceDearLieBucketNext = default;
            _underwaterDearLieBucketHeads = default;
            _underwaterDearLieBucketNext = default;
            _dearLieVault = null;
            _dearLieVaultReady = false;
            _dearLieVaultJobLocksHeld = false;
            _dearLieVaultJobLockCount = 0;
        }

        private static void ReleaseDearLieVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void CacheDearLieFallbackQualityWeightCold()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _dearLieFallbackQualityWeight = math.saturate(math.isfinite(quality) ? quality : 0.5f);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = audioService != null && audioService.IsInitialized ? audioService : null;
            _harvestAudioSink = _audioService as ISpatialAudioHarvestPlaybackSink;
        }

        private void ClearCachedRegistryServices()
        {
            _playerInventoryService = null;
            _persistentWorldRegistry = null;
            _audioService = null;
            _harvestAudioSink = null;
        }

        private void AdvanceOrganicClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            double nextTime = _organicClockSeconds + deltaTime;
            _organicClockSeconds = nextTime >= 0d && nextTime < OrganicClockMaxSeconds
                ? nextTime
                : OrganicClockMaxSeconds;
        }

        private float ResolveOrganicClockSeconds()
        {
            double currentTime = _organicClockSeconds;
            if (!(currentTime > 0d))
                return 0f;

            return currentTime < OrganicClockMaxSeconds
                ? (float)currentTime
                : (float)OrganicClockMaxSeconds;
        }

        /// <summary>
        /// Processes pending entropy jobs and drop routing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            AdvanceOrganicClock(deltaTime);
            float currentTime = ResolveOrganicClockSeconds();
            if (_dearLieJobScheduled)
                return;

            RefreshActiveCachesIfNeeded(force: false);
            ProcessDearLieDestructionSignals(currentTime);
            if (_dearLieJobScheduled)
                return;

            ProcessDearLieRegeneration(currentTime);
            UpdateDecompositionVisuals(currentTime);
            UpdateRegrowthVisuals();
            UpdateMatureSporeAcoustics(currentTime);
            UpdateDamageVisuals(currentTime);
            UpdateWiltInstances(currentTime);
            bool dropBufferDrained = !_yieldScheduled && DrainDropBuffer();
            if (dropBufferDrained && _deferredYieldScheduleFrame < 0)
                ScheduleYieldJobIfNeeded();

            VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
        }

        public void LateFrameTick()
        {
            DispatcherJobSwap.BeginLateFrameSwapWindow();
            try
            {
                CompleteDearLieJobIfNeeded(ResolveOrganicClockSeconds(), force: false);
                CompleteYieldJobIfNeeded(force: false);
            }
            finally
            {
                DispatcherJobSwap.EndLateFrameSwapWindow();
            }

            FlushPendingHarvestAudioEvents();
            FlushPendingSporeAcousticEvents();

            if (_yieldScheduled)
                return;

            bool dropBufferDrained = DrainDropBuffer();
            if (dropBufferDrained &&
                _deferredYieldScheduleFrame >= 0 &&
                Time.frameCount >= _deferredYieldScheduleFrame)
            {
                _deferredYieldScheduleFrame = -1;
                ScheduleYieldJobIfNeeded();
                VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
            }
        }

        /// <summary>
        /// Restores destroyed flora tombstones from persistence and re-applies active suppression after world paging.
        /// </summary>
        public void SlowTick()
        {
            if (_dearLieJobScheduled)
                return;

            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
            RefreshActiveCachesIfNeeded(force: true);
            float currentTime = ResolveOrganicClockSeconds();
            RefreshCorpseResourceNodes(currentTime);
            EvaluateAllelopathicRelease();
            EvaluateAggressiveOvergrowth(currentTime);
        }

        private void ProcessDearLieDestructionSignals(float currentTime)
        {
            if (_dearLieJobScheduled)
                return;

            if (!_dearLieVaultReady ||
                !_dearLieDamageEvents.IsCreated ||
                !_dearLieResults.IsCreated ||
                !_dearLieCounters.IsCreated ||
                !_surfaceDearLieBucketHeads.IsCreated ||
                !_surfaceDearLieBucketNext.IsCreated ||
                !_underwaterDearLieBucketHeads.IsCreated ||
                !_underwaterDearLieBucketNext.IsCreated)
            {
                return;
            }

            if (!HasAnyAuthoritativeDearLieDamageSignal() && !dearLieGenerateMockDamageBurst)
            {
                RecordDearLieTelemetry(Time.frameCount, 0, 0, 0, 0, 0, 0, 0f, 0u, 0);
                return;
            }

            if (!TryLockDearLieVaultJobBuffers())
            {
                RecordDearLieTelemetry(Time.frameCount, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            ClearDearLieCounters();

            int damageCount = StageDearLieDamageEvents(out JobHandle stageHandle);
            if (damageCount <= 0)
            {
                int rejectedOnlyCount = math.max(0, ReadDearLieCounter(4));
                int nanOnlyCount = math.max(0, ReadDearLieCounter(5));
                RecordDearLieTelemetry(Time.frameCount, 0, 0, 0, 0, rejectedOnlyCount, nanOnlyCount, 0f, 0u, nanOnlyCount > 0 ? (byte)1 : (byte)0);
                if (nanOnlyCount > 0)
                    DumpDearLieTelemetry();
                UnlockDearLieVaultJobBuffers();
                return;
            }

            JobHandle surfaceHandle = ScheduleDearLieLane(false, damageCount, stageHandle);
            JobHandle underwaterHandle = ScheduleDearLieLane(true, damageCount, stageHandle);
            _dearLieJobHandle = JobHandle.CombineDependencies(stageHandle, JobHandle.CombineDependencies(surfaceHandle, underwaterHandle));
            _dearLieScheduledDamageCount = damageCount;
            _dearLieJobScheduleFrame = Time.frameCount;
            _dearLieJobStartTimeSeconds = Time.realtimeSinceStartupAsDouble;
            _dearLieJobScheduled = true;
        }

        private static bool HasAnyAuthoritativeDearLieDamageSignal()
        {
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < signalCount; i++)
            {
                if ((signals[i].Flags & CombatDamageSignal.VisualOnlyFlag) == 0)
                    return true;
            }

            return false;
        }

        private void ClearDearLieCounters()
        {
            if (!_dearLieCounters.IsCreated)
                return;

            for (int i = 0; i < _dearLieCounters.Length; i++)
                _dearLieCounters[i] = default;
        }

        private int ReadDearLieCounter(int index)
        {
            if (!_dearLieCounters.IsCreated || (uint)index >= (uint)_dearLieCounters.Length)
                return 0;

            return _dearLieCounters[index].Value;
        }

        private void WriteDearLieCounter(int index, int value)
        {
            if (!_dearLieCounters.IsCreated || (uint)index >= (uint)_dearLieCounters.Length)
                return;

            FloraDearLieCounter64 counter = _dearLieCounters[index];
            counter.Value = value;
            _dearLieCounters[index] = counter;
        }

        private bool CompleteDearLieJobIfNeeded(float currentTime, bool force = true)
        {
            if (!_dearLieJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _dearLieJobHandle, force))
                return false;

            _dearLieJobScheduled = false;
            bool sameFrameCompletion = _dearLieJobScheduleFrame == Time.frameCount;
            float queryMicroseconds = 0f;
            if (_dearLieJobStartTimeSeconds > 0d)
            {
                double elapsedSeconds = Time.realtimeSinceStartupAsDouble - _dearLieJobStartTimeSeconds;
                if (elapsedSeconds > 0d && math.isfinite(elapsedSeconds))
                    queryMicroseconds = (float)math.min(1000000d, elapsedSeconds * 1000000d);
            }

            _dearLieJobScheduleFrame = -1;
            _dearLieJobStartTimeSeconds = 0d;
            int damageCount = math.max(0, _dearLieScheduledDamageCount);
            _dearLieScheduledDamageCount = 0;
            try
            {
                int destroyedCount = ApplyDearLieDestructionResults(currentTime, out uint lastInstanceUid, out int vfxCount);
                _dearLieLastDamageFrame = Time.frameCount;
                _dearLieLastDestroyedCount = destroyedCount;
                _dearLieLastVfxCount = vfxCount;

                int overflowCount = math.max(0, ReadDearLieCounter(6));
                int rejectedCount = math.max(0, ReadDearLieCounter(4)) + overflowCount;
                int nanRejectCount = math.max(0, ReadDearLieCounter(5));
                byte flags = 0;
                if (nanRejectCount > 0)
                    flags |= 1;
                if (destroyedCount > 0)
                    flags |= 2;
                if (sameFrameCompletion && queryMicroseconds > 500f)
                    flags |= 8;
                if (overflowCount > 0)
                    flags |= 16;
                RecordDearLieTelemetry(Time.frameCount, damageCount, destroyedCount, vfxCount, 0, rejectedCount, nanRejectCount, queryMicroseconds, lastInstanceUid, flags);
                if (nanRejectCount > 0 || overflowCount > 0 || (sameFrameCompletion && queryMicroseconds > 500f))
                    DumpDearLieTelemetry();
            }
            finally
            {
                UnlockDearLieVaultJobBuffers();
            }

            return true;
        }

        private int StageDearLieDamageEvents(out JobHandle stageHandle)
        {
            stageHandle = default;
            int writeCount = 0;
            int rejectedCount = 0;
            int nanRejectCount = 0;
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < signalCount && writeCount < DearLieMaxDamageSignalsPerFrame; i++)
            {
                CombatDamageSignal signal = signals[i];
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (!TryBuildDearLieEvent(in signal, out FloraDestructionEventDTO dearLieEvent, ref nanRejectCount))
                {
                    rejectedCount++;
                    continue;
                }

                _dearLieDamageEvents[writeCount++] = dearLieEvent;
            }

            if (dearLieGenerateMockDamageBurst && writeCount < DearLieMaxDamageSignalsPerFrame)
            {
                dearLieGenerateMockDamageBurst = false;
                int mockCount = math.min(DearLieMockDamageSignalCount, DearLieMaxDamageSignalsPerFrame - writeCount);
                double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                double3 centerAup = originAup + new double3(dearLieMockDamageCenter.x, dearLieMockDamageCenter.y, dearLieMockDamageCenter.z);
                var mockJob = new GenerateMockFloraDamageJob
                {
                    Events = _dearLieDamageEvents,
                    Offset = writeCount,
                    Count = mockCount,
                    CenterAUP = centerAup,
                    Seed = unchecked((uint)(Time.frameCount * 268 + 0x51A268u))
                };
                stageHandle = mockJob.Schedule(mockCount, DearLieJobBatchSize);
                writeCount += mockCount;
            }

            WriteDearLieCounter(4, rejectedCount);
            WriteDearLieCounter(5, nanRejectCount);

            return writeCount;
        }

        private static bool TryBuildDearLieEvent(
            in CombatDamageSignal signal,
            out FloraDestructionEventDTO dearLieEvent,
            ref int nanRejectCount)
        {
            dearLieEvent = default;
            if (!CombatDamageSignalCodec.IsFiniteAup(signal.ImpactAup) ||
                !math.isfinite(signal.Magnitude) ||
                !math.all(math.isfinite(signal.Direction)))
            {
                nanRejectCount++;
                return false;
            }

            if (signal.Magnitude <= DearLieMinimumMagnitude)
                return false;

            if ((signal.Flags & CombatDamageSignal.LegacyMirrorFlag) != 0)
                return false;

            bool explicitFloraRoute = (signal.Flags & DearLieFloraDamageFlag) != 0;
            bool areaRoute = signal.TargetId == 0 && (signal.TargetHash == 0u || signal.TargetHash == DearLieSignalHashFlora || signal.TargetHash == DearLieSignalHashOrganic);
            if (!explicitFloraRoute && !areaRoute)
                return false;

            dearLieEvent = new FloraDestructionEventDTO
            {
                ImpactAUP = signal.ImpactAup,
                FloraTypeHash = signal.TargetHash == 0u ? DearLieSignalHashFlora : signal.TargetHash,
                _pad0 = math.asuint(math.saturate(signal.Magnitude))
            };
            return true;
        }

        private JobHandle ScheduleDearLieLane(bool underwater, int damageCount, JobHandle inputDependency)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<FloraDearLieClaim64> claims = underwater ? _underwaterDearLieClaims : _surfaceDearLieClaims;
            NativeArray<int> bucketHeads = underwater ? _underwaterDearLieBucketHeads : _surfaceDearLieBucketHeads;
            NativeArray<int> bucketNext = underwater ? _underwaterDearLieBucketNext : _surfaceDearLieBucketNext;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                !claims.IsCreated ||
                !bucketHeads.IsCreated ||
                !bucketNext.IsCreated ||
                count <= 0)
            {
                return default;
            }

            int safeCount = math.min(count, math.min(matrices.Length, math.min(metadata.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, math.min(health.Length, math.min(claims.Length, bucketNext.Length)))))));
            if (safeCount <= 0)
                return default;

            float qualityWeight = ResolveDearLieGlobalQualityWeight();
            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            var clearJob = new ClearDearLieClaimsJob
            {
                Claims = claims,
                Count = safeCount
            };
            JobHandle clearHandle = clearJob.Schedule(safeCount, DearLieJobBatchSize, inputDependency);
            var clearBucketsJob = new ClearDearLieBucketsJob
            {
                BucketHeads = bucketHeads,
                Count = bucketHeads.Length
            };
            JobHandle clearBucketsHandle = clearBucketsJob.Schedule(bucketHeads.Length, DearLieJobBatchSize, inputDependency);
            JobHandle clearAllHandle = JobHandle.CombineDependencies(clearHandle, clearBucketsHandle);

            var buildJob = new BuildDearLieSpatialHashJob
            {
                Matrices = matrices,
                InstanceUids = instanceUids,
                Health = health,
                BucketHeads = bucketHeads,
                BucketNext = bucketNext,
                Count = safeCount,
                BucketCount = bucketHeads.Length,
                RuntimeOriginAUP = originAup,
                CellSizeMeters = DearLieSpatialCellSizeMeters
            };
            JobHandle buildHandle = buildJob.Schedule(safeCount, DearLieJobBatchSize, clearAllHandle);

            var resolveJob = new ResolveDearLieDamageJob
            {
                Matrices = matrices,
                Metadata = metadata,
                InstanceUids = instanceUids,
                MaterialClasses = materialClasses,
                Health = health,
                Claims = claims,
                Events = _dearLieDamageEvents,
                Results = _dearLieResults,
                Counters = _dearLieCounters,
                BucketHeads = bucketHeads,
                BucketNext = bucketNext,
                Count = safeCount,
                BucketCount = bucketHeads.Length,
                EventCount = math.min(damageCount, DearLieMaxDamageSignalsPerFrame),
                RuntimeOriginAUP = originAup,
                CellSizeMeters = DearLieSpatialCellSizeMeters,
                QueryRadiusMeters = ResolveDearLieQueryRadius(),
                GlobalQualityWeight = qualityWeight,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                LaneSalt = underwater ? 0xA2680002u : 0xA2680001u
            };
            return resolveJob.Schedule(damageCount, 1, buildHandle);
        }

        private int ApplyDearLieDestructionResults(float currentTime, out uint lastInstanceUid, out int vfxCount)
        {
            lastInstanceUid = 0u;
            vfxCount = 0;
            if (!_dearLieResults.IsCreated ||
                !_dearLieCounters.IsCreated ||
                !_destroyedByInstanceUid.IsCreated)
            {
                return 0;
            }

            int resultCount = math.min(math.max(0, ReadDearLieCounter(0)), _dearLieResults.Length);
            int appliedCount = 0;
            for (int i = 0; i < resultCount; i++)
            {
                FloraDearLieDestructionResult result = _dearLieResults[i];
                uint instanceUid = result.InstanceUid;
                if (instanceUid == 0u || _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                if (!TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
                    continue;

                NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                    continue;

                Vector3 runtimePosition = ExtractTranslation(result.OriginalMatrix);
                if (!IsFiniteVector(runtimePosition))
                    runtimePosition = ExtractTranslation(matrices[activeIndex]);

                Vector3 impactRuntimePosition = (Vector3)AbsoluteUniversePosition.FromAbsolutePosition(result.ImpactAUP).ToRuntimeFloat3();
                if (IsFiniteVector(impactRuntimePosition) && IsFiniteVector(runtimePosition))
                {
                    _dearLieLastImpactRuntimePosition = impactRuntimePosition;
                    _dearLieLastTargetRuntimePosition = runtimePosition;
                    _dearLieHasLastDebugHit = 1;
                }

                _destroyedByInstanceUid.TryAdd(instanceUid, 1);
                if (_healthByInstanceUid.IsCreated)
                {
                    _healthByInstanceUid.Remove(instanceUid);
                    _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
                }

                ClearOrganicLifecycleState(instanceUid);
                PrimeDecompositionState(instanceUid, currentTime);
                SetLaneHealth(underwater, activeIndex, 0f);
                if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                if (_damageVisualProgressByInstanceUid.IsCreated)
                    _damageVisualProgressByInstanceUid.Remove(instanceUid);

                byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
                ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
                QueueDearLieRegeneration(instanceUid, underwater, activeIndex, runtimePosition, currentTime + ResolveDearLieRegenerationDelaySeconds(), in result.OriginalMatrix);
                if (TryPublishDearLieDebris(in result))
                    vfxCount++;
                appliedCount++;
                lastInstanceUid = instanceUid;
            }

            return appliedCount;
        }

        private static bool TryPublishDearLieDebris(in FloraDearLieDestructionResult result)
        {
            if (result.EmitVfx == 0 || result.InstanceUid == 0u || result.VfxQuantity == 0)
                return false;

            float intensity = math.saturate(math.asfloat(result.MagnitudeBits));
            if (!math.isfinite(intensity) || intensity <= 0f || !CombatDamageSignalCodec.IsFiniteAup(result.ImpactAUP))
                return false;

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(result.ImpactAUP),
                SpeciesHash = result.FloraTypeHash ^ ((uint)result.MaterialClass * 2246822519u),
                SourceEntityId = result.InstanceUid ^ 0x7F4A7C15u,
                Intensity01 = intensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = result.VfxQuantity
            };
            return SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_DestructibleOrganicManager);
        }

        private void QueueDearLieRegeneration(uint instanceUid, bool underwater, int activeIndex, Vector3 runtimePosition, float restoreTimeSeconds, in Matrix4x4 originalMatrix)
        {
            if (!_dearLieRegenRecords.IsCreated || instanceUid == 0u || _dearLieRegenCount >= _dearLieRegenRecords.Length)
                return;

            _dearLieRegenRecords[_dearLieRegenCount++] = new FloraDearLieRegenRecord
            {
                OriginalMatrix = originalMatrix,
                InstanceUid = instanceUid,
                ActiveIndex = activeIndex,
                RestoreTimeSeconds = restoreTimeSeconds,
                RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                Underwater = underwater ? (byte)1 : (byte)0
            };
        }

        private void ProcessDearLieRegeneration(float currentTime)
        {
            if (!_dearLieRegenRecords.IsCreated || _dearLieRegenCount <= 0)
                return;

            int recoveredCount = 0;
            for (int i = _dearLieRegenCount - 1; i >= 0; i--)
            {
                FloraDearLieRegenRecord record = _dearLieRegenRecords[i];
                if (record.InstanceUid == 0u || currentTime < record.RestoreTimeSeconds)
                    continue;

                Vector3 runtimePosition = new Vector3(record.RuntimePosition.x, record.RuntimePosition.y, record.RuntimePosition.z);
                if (!IsFiniteVector(runtimePosition) && IsFiniteMatrix(in record.OriginalMatrix))
                    runtimePosition = ExtractTranslation(record.OriginalMatrix);

                if (!IsFiniteVector(runtimePosition) &&
                    TryResolveActiveInstanceByUid(record.InstanceUid, out bool underwater, out int activeIndex, out _) &&
                    TryResolveMatrixForLane(underwater, activeIndex, out Matrix4x4 matrix))
                {
                    runtimePosition = ExtractTranslation(matrix);
                }

                TryRestoreDearLieOriginalMatrix(in record);

                if (IsFiniteVector(runtimePosition) && TrySetRegrowthProgress(record.InstanceUid, runtimePosition, 1f))
                    recoveredCount++;

                int lastIndex = _dearLieRegenCount - 1;
                _dearLieRegenRecords[i] = _dearLieRegenRecords[lastIndex];
                _dearLieRegenRecords[lastIndex] = default;
                _dearLieRegenCount = lastIndex;
            }

            if (recoveredCount > 0)
                RecordDearLieTelemetry(Time.frameCount, 0, 0, 0, recoveredCount, 0, 0, 0f, 0u, 4);
        }

        private bool TryRestoreDearLieOriginalMatrix(in FloraDearLieRegenRecord record)
        {
            if (record.InstanceUid == 0u || !IsFiniteMatrix(in record.OriginalMatrix))
                return false;

            bool recordUnderwater = record.Underwater != 0;
            bool underwater = recordUnderwater;
            int activeIndex = record.ActiveIndex;
            if (!TryResolveActiveInstanceByUid(record.InstanceUid, out underwater, out activeIndex, out _))
            {
                underwater = recordUnderwater;
                activeIndex = record.ActiveIndex;
                NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
                int count = underwater ? _underwaterCount : _surfaceCount;
                if (!instanceUids.IsCreated ||
                    activeIndex < 0 ||
                    activeIndex >= count ||
                    activeIndex >= instanceUids.Length ||
                    instanceUids[activeIndex] != record.InstanceUid)
                {
                    return false;
                }
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return false;

            matrices[activeIndex] = record.OriginalMatrix;
            return true;
        }

        private bool TryResolveMatrixForLane(bool underwater, int activeIndex, out Matrix4x4 matrix)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
            {
                matrix = default;
                return false;
            }

            matrix = matrices[activeIndex];
            return true;
        }

        private void RecordDearLieTelemetry(
            int frameIndex,
            int damageSignalCount,
            int destroyedCount,
            int vfxSignalCount,
            int recoveredCount,
            int rejectedSignalCount,
            int nanRejectCount,
            float queryMicroseconds,
            uint lastInstanceUid,
            byte flags)
        {
            if (!_dearLieTelemetryRing.IsCreated || _dearLieTelemetryRing.Length == 0)
                return;

            int index = _dearLieTelemetryCursor++ % _dearLieTelemetryRing.Length;
            float qualityWeight = ResolveDearLieGlobalQualityWeight();
            _dearLieLastQualityWeight = qualityWeight;
            uint hash = 2166136261u;
            hash = MixDearLieHash(hash, (uint)frameIndex);
            hash = MixDearLieHash(hash, (uint)damageSignalCount);
            hash = MixDearLieHash(hash, (uint)destroyedCount);
            hash = MixDearLieHash(hash, lastInstanceUid);
            hash = MixDearLieHash(hash, math.asuint(math.select(0f, queryMicroseconds, math.isfinite(queryMicroseconds))));
            _dearLieTelemetryRing[index] = new FloraDearLieTelemetryEntry
            {
                FrameIndex = frameIndex,
                SurfaceCount = _surfaceCount,
                UnderwaterCount = _underwaterCount,
                DamageSignalCount = damageSignalCount,
                DestroyedCount = destroyedCount,
                VfxSignalCount = vfxSignalCount,
                RegenQueuedCount = _dearLieRegenCount,
                RecoveredCount = recoveredCount,
                RejectedSignalCount = rejectedSignalCount,
                NanRejectCount = nanRejectCount,
                GlobalQualityWeight = qualityWeight,
                Hash = hash,
                LastInstanceUid = lastInstanceUid,
                Flags = flags,
                QueryMicroseconds = math.select(0f, queryMicroseconds, math.isfinite(queryMicroseconds))
            };
        }

        private unsafe void DumpDearLieTelemetry()
        {
            if (!_dearLieTelemetryRing.IsCreated)
                return;

            string path = global::System.IO.Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_SHINOBU_268.bin");
            try
            {
                using (global::System.IO.FileStream stream = new global::System.IO.FileStream(path, global::System.IO.FileMode.Create, global::System.IO.FileAccess.Write, global::System.IO.FileShare.Read))
                {
                    int stride = UnsafeUtility.SizeOf<FloraDearLieTelemetryEntry>();
                    byte* scratchPtr = stackalloc byte[stride];
                    for (int i = 0; i < _dearLieTelemetryRing.Length; i++)
                    {
                        FloraDearLieTelemetryEntry entry = _dearLieTelemetryRing[i];
                        UnsafeUtility.MemCpy(scratchPtr, UnsafeUtility.AddressOf(ref entry), stride);
                        for (int byteIndex = 0; byteIndex < stride; byteIndex++)
                            stream.WriteByte(scratchPtr[byteIndex]);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private float ResolveDearLieGlobalQualityWeight()
        {
            if (math.isfinite(dearLieQualityOverride) && dearLieQualityOverride >= 0f)
                return math.saturate(dearLieQualityOverride);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(weight))
                weight = _dearLieFallbackQualityWeight;

            return math.saturate(weight);
        }

        private float ResolveDearLieQueryRadius()
        {
            return math.clamp(
                math.select(DearLieQueryRadiusMeters, dearLieDamageRadiusEpsilon, math.isfinite(dearLieDamageRadiusEpsilon)),
                0.25f,
                8f);
        }

        private float ResolveDearLieRegenerationDelaySeconds()
        {
            return math.clamp(
                math.select(DearLieRegenerationDelaySeconds, dearLieRegenerationDelaySeconds, math.isfinite(dearLieRegenerationDelaySeconds)),
                5f,
                900f);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static uint MixDearLieHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static int ComputeDearLieCellHash(double3 positionAup, double cellSizeMeters)
        {
            double safeCellSize = math.max(0.25d, cellSizeMeters);
            long x = (long)math.floor(positionAup.x / safeCellSize);
            long y = (long)math.floor(positionAup.y / safeCellSize);
            long z = (long)math.floor(positionAup.z / safeCellSize);
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (ulong)x) * 1099511628211UL;
            hash = (hash ^ (ulong)y) * 1099511628211UL;
            hash = (hash ^ (ulong)z) * 1099511628211UL;
            return unchecked((int)(hash ^ (hash >> 32)));
        }

        private static int ComputeDearLieCellHash(long x, long y, long z)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (ulong)x) * 1099511628211UL;
            hash = (hash ^ (ulong)y) * 1099511628211UL;
            hash = (hash ^ (ulong)z) * 1099511628211UL;
            return unchecked((int)(hash ^ (hash >> 32)));
        }

        private static double3 ExtractMatrixTranslationDouble(Matrix4x4 matrix)
        {
            return new double3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearDearLieClaimsJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1: Claims is a Vault-backed 64-byte claim array sized to the visible flora lane before scheduling; each worker writes only its own index during the clear pass.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2: The clear pass is chained before spatial hash build and resolve jobs, so no concurrent writer reads stale claim state while this pass runs.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3: NativeDisableParallelForRestriction is required because the same claim array is later used for atomic CompareExchange claims; the owner holds Vault job locks until dispatcher completion.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDearLieClaim64> Claims;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || index >= Claims.Length)
                    return;

                Claims[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearDearLieBucketsJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketHeads;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || index >= BucketHeads.Length)
                    return;

                BucketHeads[index] = -1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct BuildDearLieSpatialHashJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly, NoAlias] public NativeArray<uint> InstanceUids;
            [ReadOnly, NoAlias] public NativeArray<Unity.Mathematics.half> Health;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketHeads;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketNext;
            public int Count;
            public int BucketCount;
            public double3 RuntimeOriginAUP;
            public double CellSizeMeters;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count ||
                    index >= Matrices.Length ||
                    index >= InstanceUids.Length ||
                    index >= Health.Length ||
                    index >= BucketNext.Length)
                {
                    return;
                }

                int bucketCount = math.min(BucketCount, BucketHeads.Length);
                if (bucketCount <= 0 || (bucketCount & (bucketCount - 1)) != 0)
                    return;

                BucketNext[index] = -1;

                if (InstanceUids[index] == 0u || (float)Health[index] <= 0.0001f)
                    return;

                Matrix4x4 matrix = Matrices[index];
                double3 positionAup = RuntimeOriginAUP + ExtractMatrixTranslationDouble(matrix);
                if (!math.all(math.isfinite(positionAup)))
                    return;

                int hash = ComputeDearLieCellHash(positionAup, CellSizeMeters);
                int bucketIndex = (int)((uint)hash & (uint)(bucketCount - 1));
                int* heads = (int*)BucketHeads.GetUnsafePtr();
                int* next = (int*)BucketNext.GetUnsafePtr();
                int oldHead = Interlocked.Exchange(ref heads[bucketIndex], index);
                next[index] = oldHead;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ResolveDearLieDamageJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1: Matrices, Metadata, Health, Claims, Results, Counters, Events, and bucket arrays are distinct native lanes; Dear Lie transient lanes are Vault-backed and locked while jobs hold pointers.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2: Cross-worker mutation is limited to atomic claim slots and 64-byte padded counters; result rows are allocated by atomic counter and have 128-byte stride to avoid cache-line overlap.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3: The job emits only staged result rows. SignalBus publication happens after DispatcherJobSwap completion in the owner phase, avoiding legacy writer lifetime races.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> Matrices;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<HectonVegetationInstanceData> Metadata;
            [ReadOnly, NoAlias] public NativeArray<uint> InstanceUids;
            [ReadOnly, NoAlias] public NativeArray<byte> MaterialClasses;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<Unity.Mathematics.half> Health;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDearLieClaim64> Claims;
            [ReadOnly, NoAlias] public NativeArray<FloraDestructionEventDTO> Events;
            // SAFETY_JUSTIFICATION_PARAGRAPH_4: Results and Counters are intentionally shared by surface/underwater resolve jobs; rows are claimed only by atomic 64-byte padded counters.
            // SAFETY_JUSTIFICATION_PARAGRAPH_5: Each result row is 128 bytes and written once by the worker that owns the returned atomic index; readers are fenced by DispatcherJobSwap.
            // SAFETY_JUSTIFICATION_PARAGRAPH_6: Native container safety is disabled only on these two cross-lane aggregation buffers, not on lane source arrays or flat spatial buckets.
            [NoAlias, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<FloraDearLieDestructionResult> Results;
            [NoAlias, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<FloraDearLieCounter64> Counters;
            [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
            [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
            public int Count;
            public int BucketCount;
            public int EventCount;
            public double3 RuntimeOriginAUP;
            public double CellSizeMeters;
            public float QueryRadiusMeters;
            public float GlobalQualityWeight;
            public uint Frame;
            public uint LaneSalt;

            public void Execute(int eventIndex)
            {
                if ((uint)eventIndex >= (uint)EventCount || eventIndex >= Events.Length)
                    return;

                FloraDestructionEventDTO damageEvent = Events[eventIndex];
                float magnitude01 = math.asfloat(damageEvent._pad0);
                if (!math.all(math.isfinite(damageEvent.ImpactAUP)) || !math.isfinite(magnitude01))
                {
                    IncrementCounter(5);
                    return;
                }

                double safeCellSize = math.max(0.25d, CellSizeMeters);
                double3 cellPosition = damageEvent.ImpactAUP / safeCellSize;
                long baseX = (long)math.floor(cellPosition.x);
                long baseY = (long)math.floor(cellPosition.y);
                long baseZ = (long)math.floor(cellPosition.z);
                float queryRadius = math.max(0.05f, QueryRadiusMeters);
                float queryRadiusSq = queryRadius * queryRadius;
                int bestIndex = -1;
                float bestDistanceSq = queryRadiusSq;
                int bucketCount = math.min(BucketCount, BucketHeads.Length);
                if (bucketCount <= 0 || (bucketCount & (bucketCount - 1)) != 0 || !BucketNext.IsCreated)
                    return;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int cellHash = ComputeDearLieCellHash(baseX + dx, baseY + dy, baseZ + dz);
                            int bucketIndex = (int)((uint)cellHash & (uint)(bucketCount - 1));
                            int candidateIndex = BucketHeads[bucketIndex];
                            int guard = 0;
                            while (candidateIndex >= 0 && guard++ < Count)
                            {
                                int currentIndex = candidateIndex;
                                candidateIndex = currentIndex < BucketNext.Length ? BucketNext[currentIndex] : -1;
                                if ((uint)currentIndex >= (uint)Count ||
                                    currentIndex >= Matrices.Length ||
                                    currentIndex >= InstanceUids.Length ||
                                    currentIndex >= Health.Length)
                                {
                                    continue;
                                }

                                if (InstanceUids[currentIndex] == 0u || (float)Health[currentIndex] <= 0.0001f)
                                    continue;

                                double3 candidateAup = RuntimeOriginAUP + ExtractMatrixTranslationDouble(Matrices[currentIndex]);
                                if (!math.all(math.isfinite(candidateAup)))
                                    continue;

                                double3 localDeltaAup = candidateAup - damageEvent.ImpactAUP;
                                float3 localDelta = new float3((float)localDeltaAup.x, (float)localDeltaAup.y, (float)localDeltaAup.z);
                                if (!math.all(math.isfinite(localDelta)))
                                    continue;

                                float distanceSq = math.lengthsq(localDelta);
                                if (distanceSq < bestDistanceSq)
                                {
                                    bestDistanceSq = distanceSq;
                                    bestIndex = currentIndex;
                                }
                            }
                        }
                    }
                }

                if (bestIndex < 0 || !TryClaim(bestIndex))
                    return;

                uint instanceUid = InstanceUids[bestIndex];
                int resultIndex = IncrementCounter(0) - 1;
                if ((uint)resultIndex >= (uint)Results.Length)
                {
                    IncrementCounter(6);
                    return;
                }

                Matrix4x4* matrixPtr = (Matrix4x4*)Matrices.GetUnsafePtr();
                Unity.Mathematics.half* healthPtr = (Unity.Mathematics.half*)Health.GetUnsafePtr();
                ref Matrix4x4 matrixRef = ref UnsafeUtility.AsRef<Matrix4x4>(matrixPtr + bestIndex);
                ref Unity.Mathematics.half healthRef = ref UnsafeUtility.AsRef<Unity.Mathematics.half>(healthPtr + bestIndex);
                Matrix4x4 originalMatrix = matrixRef;
                ScaleMatrixColumnsToZero(ref matrixRef);
                healthRef = (Unity.Mathematics.half)0f;

                if (bestIndex < Metadata.Length)
                {
                    HectonVegetationInstanceData* metadataPtr = (HectonVegetationInstanceData*)Metadata.GetUnsafePtr();
                    ref HectonVegetationInstanceData data = ref UnsafeUtility.AsRef<HectonVegetationInstanceData>(metadataPtr + bestIndex);
                    data.HeightScale = 0f;
                    data.WidthScale = 0f;
                    data.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
                    data.RuntimeFlags = FloraRuntimeFlagDead;
                    data.HealthNormalized = 0f;
                    data.Reserved0 = -1f;
                }

                ResolveDebrisEmission(in damageEvent, instanceUid, bestIndex, out byte emitVfx, out ushort vfxQuantity, out byte materialClass);
                Results[resultIndex] = new FloraDearLieDestructionResult
                {
                    OriginalMatrix = originalMatrix,
                    ImpactAUP = damageEvent.ImpactAUP,
                    InstanceUid = instanceUid,
                    ActiveIndex = bestIndex,
                    FloraTypeHash = damageEvent.FloraTypeHash,
                    MagnitudeBits = damageEvent._pad0,
                    VfxQuantity = vfxQuantity,
                    EmitVfx = emitVfx,
                    MaterialClass = materialClass
                };
            }

            private bool TryClaim(int index)
            {
                if ((uint)index >= (uint)Claims.Length)
                    return false;

                FloraDearLieClaim64* claimPtr = (FloraDearLieClaim64*)Claims.GetUnsafePtr();
                return Interlocked.CompareExchange(ref claimPtr[index].Claimed, 1, 0) == 0;
            }

            private int IncrementCounter(int index)
            {
                if ((uint)index >= (uint)Counters.Length)
                    return 0;

                FloraDearLieCounter64* ptr = (FloraDearLieCounter64*)Counters.GetUnsafePtr();
                return Interlocked.Increment(ref ptr[index].Value);
            }

            private void ResolveDebrisEmission(in FloraDestructionEventDTO damageEvent, uint instanceUid, int activeIndex, out byte emitVfx, out ushort quantity, out byte materialClass)
            {
                emitVfx = 0;
                quantity = 0;
                float q = math.saturate(GlobalQualityWeight);
                float intensity = math.saturate(math.asfloat(damageEvent._pad0));
                materialClass = activeIndex >= 0 && activeIndex < MaterialClasses.Length ? MaterialClasses[activeIndex] : (byte)0;
                float emissionProbability = math.saturate((0.12f + (q * 0.88f)) * math.max(0.2f, intensity));
                uint hash = instanceUid ^ Frame ^ LaneSalt ^ ((uint)materialClass * 2654435761u);
                if (Hash01(hash) > emissionProbability)
                    return;

                quantity = (ushort)math.clamp((int)math.round(math.lerp(1f, 24f, SmoothStep01(q)) * math.max(0.25f, intensity)), 1, 64);
                emitVfx = 1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockFloraDamageJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDestructionEventDTO> Events;
            public int Offset;
            public int Count;
            public double3 CenterAUP;
            public uint Seed;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || Offset + index >= Events.Length)
                    return;

                uint h = Seed ^ ((uint)index * 747796405u);
                float angle = Hash01(h) * 6.28318530718f;
                float radius = math.sqrt(Hash01(h ^ 0x9E3779B9u)) * 7f;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                double3 offset = new double3(cos * radius, math.lerp(-0.8f, 0.8f, Hash01(h ^ 0x85EBCA6Bu)), sin * radius);
                Events[Offset + index] = new FloraDestructionEventDTO
                {
                    ImpactAUP = CenterAUP + offset,
                    FloraTypeHash = DearLieSignalHashFlora,
                    _pad0 = math.asuint(math.lerp(0.35f, 1f, Hash01(h ^ 0xC2B2AE35u)))
                };
            }
        }

        /// <summary>
        /// Applies one tool hit against the nearest active harvestable indirect-flora instance.
        /// </summary>
        public bool TryApplyToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (_dearLieJobScheduled ||
                deliveredDamage <= 0f ||
                vegetationBridge == null ||
                _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                hitPoint,
                Mathf.Max(hitSearchRadius, interactionBurstRadius),
                toolCapabilityMask,
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out Matrix4x4 instanceMatrix,
                out Vector3 instancePosition))
            {
                return false;
            }

            if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                return false;

            float baseHealth = Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth);
            float currentHealth = GetLaneHealth(underwater, activeIndex);
            float toolResistance = math.max(0.01f, _templateDescriptors[templateIndex].ToolResistance);
            float nextHealth = Mathf.Max(0f, currentHealth - (deliveredDamage / toolResistance));
            float previousNormalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float previousHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, previousNormalizedHealth);
            HarvestState previousHarvestState = ResolveHarvestState(templateIndex, baseHealth, currentHealth, previousHeightScale);
            float nextHeightScale = ResolveNormalizedHeightScale(templateIndex, baseHealth, nextHealth);
            HarvestState nextHarvestState = nextHealth > 0.0001f
                ? ResolveHarvestState(templateIndex, baseHealth, nextHealth, nextHeightScale)
                : HarvestState.Dead;
            if (ShouldDetonateDefensiveSporeBurst(templateIndex, toolCapabilityMask))
            {
                floraInteractionManager?.RegisterDefensiveSporeBurst(instancePosition, Mathf.Max(0.35f, normalizedPower));
                nextHealth = 0f;
                nextHeightScale = ResolveNormalizedHeightScale(templateIndex, baseHealth, nextHealth);
                nextHarvestState = HarvestState.Dead;
            }

            SetLaneHealth(underwater, activeIndex, nextHealth);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)nextHealth);
            float currentTime = ResolveOrganicClockSeconds();
            MarkOrganicTouched(instanceUid, currentTime);

            PublishExternalInteraction(hitPoint, direction * Mathf.Max(0.25f, normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius);
            bool harvestStateChanged = previousHarvestState != nextHarvestState;
            ApplyDamageVisualState(instanceUid, underwater, activeIndex, templateIndex, baseHealth, nextHealth, nextHeightScale, harvestStateChanged, currentTime);
            if (harvestStateChanged)
                DispatchHarvestAudioTransition(instanceUid, templateIndex, previousHarvestState, nextHarvestState, instancePosition);

            if (nextHealth > 0.0001f)
            {
                PersistFloraStateOverride(instanceUid, templateIndex, instancePosition, underwater, activeIndex, baseHealth, nextHealth);
                return true;
            }

            DestroyResolvedInstance(
                underwater,
                activeIndex,
                instanceUid,
                materialClass,
                templateIndex,
                instanceMatrix,
                instancePosition,
                hitPoint,
                hitNormal,
                normalizedPower);
            return true;
        }

        /// <summary>
        /// Consumes one nearby flora instance without spawning debris or loot, using the passive decomposition/tombstone path.
        /// </summary>
        internal bool TryConsumeFloraAtPosition(Vector3 worldPosition, float searchRadius, out uint instanceUid)
        {
            instanceUid = 0u;
            if (_dearLieJobScheduled || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                worldPosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                0u,
                out bool underwater,
                out int activeIndex,
                out instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition))
            {
                return false;
            }

            if (materialClass == HarvestableTemplate.MaterialClass.None ||
                (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                instanceUid = 0u;
                return false;
            }

            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
            PublishExternalInteraction(instancePosition, Vector3.up * 0.15f, interactionBurstRadius);
            return true;
        }

        /// <summary>
        /// Applies non-harvest decomposition to any active indirect flora intersecting a newly placed construction envelope.
        /// </summary>
        internal int ApplyConstructionDecomposition(Vector3 runtimePosition, float radiusMeters)
        {
            if (_dearLieJobScheduled || !math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return 0;

            RefreshActiveCachesIfNeeded(force: false);
            double3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(runtimePosition);
            double radiusSq = (double)radiusMeters * radiusMeters;
            int decomposedCount = 0;
            decomposedCount += ApplyConstructionDecompositionInLane(false, universePosition, radiusSq);
            decomposedCount += ApplyConstructionDecompositionInLane(true, universePosition, radiusSq);
            return decomposedCount;
        }

        /// <summary>
        /// Instantly tombstones active consumable flora inside a persistent chemical dead zone.
        /// </summary>
        internal int ApplyDefoliantDeadZone(Vector3 runtimePosition, float radiusMeters)
        {
            if (_dearLieJobScheduled || !math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return 0;

            RefreshActiveCachesIfNeeded(force: false);
            double3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(runtimePosition);
            double radiusSq = (double)radiusMeters * radiusMeters;
            int killedCount = 0;
            killedCount += ApplyDefoliantDeadZoneInLane(false, universePosition, radiusSq);
            killedCount += ApplyDefoliantDeadZoneInLane(true, universePosition, radiusSq);
            return killedCount;
        }

        private bool ShouldDetonateDefensiveSporeBurst(int templateIndex, uint toolCapabilityMask)
        {
            if (floraInteractionManager == null || templateIndex < 0)
                return false;

            uint burstTriggerMask = (uint)FloraDataTemplate.VulnerabilityMask.Cut | (uint)FloraDataTemplate.VulnerabilityMask.Drill;
            return (toolCapabilityMask & burstTriggerMask) != 0u &&
                   floraInteractionManager.IsDefensiveSporeBurstTemplateIndex(templateIndex);
        }

        private void EvaluateAllelopathicRelease()
        {
            NativeArray<Matrix4x4> matrices = _underwaterMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = _underwaterMetadata;
            NativeArray<int> types = _underwaterTypes;
            NativeArray<int>.ReadOnly semanticTypes = _underwaterSemanticTypes;
            NativeArray<uint> instanceUids = _underwaterInstanceUids;
            NativeArray<byte> materialClasses = _underwaterMaterialClasses;
            int count = _underwaterCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return;
            }

            float radiusSq = allelopathicCellRadius * allelopathicCellRadius;
            int overcrowdingThreshold = Mathf.Max(1, Mathf.CeilToInt(allelopathicKelpCapacity * allelopathicThreshold01));
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));

            for (int coralIndex = 0; coralIndex < safeCount; coralIndex++)
            {
                uint instanceUid = instanceUids[coralIndex];
                if (instanceUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    metadata[coralIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f)
                {
                    continue;
                }

                HectonMapMagicVegetationBridge.VegetationSemanticType semanticType =
                    (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticTypes[coralIndex];
                if (!HectonMapMagicVegetationBridge.IsColonyCoralSemanticType(semanticType))
                    continue;

                Vector3 coralPosition = ExtractTranslation(matrices[coralIndex]);
                int kelpCount = 0;
                for (int kelpIndex = 0; kelpIndex < safeCount; kelpIndex++)
                {
                    if (types[kelpIndex] != (int)HectonVegetationInstanceType.GiantKelp ||
                        metadata[kelpIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                        math.abs(metadata[kelpIndex].HeightScale) <= 0.0001f)
                    {
                        continue;
                    }

                    Vector3 kelpPosition = ExtractTranslation(matrices[kelpIndex]);
                    Vector2 planarDelta = new Vector2(kelpPosition.x - coralPosition.x, kelpPosition.z - coralPosition.z);
                    if (planarDelta.sqrMagnitude > radiusSq)
                        continue;

                    kelpCount++;
                    if (kelpCount < overcrowdingThreshold)
                        continue;

                    HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[coralIndex];
                    int templateIndex = ResolveTemplateIndex(metadata[coralIndex], materialClass);
                    ApplyPassiveDecomposition(true, coralIndex, instanceUid, materialClass, templateIndex, coralPosition);
                    break;
                }
            }
        }

        private void BuildTemplateCaches()
        {
            // COLD ALLOC: int[MaterialClassCount] - material-class to template-index lookup table - owner: DestructibleOrganicManager
            _templateIndexByMaterialClass = new int[MaterialClassCount];
            for (int i = 0; i < _templateIndexByMaterialClass.Length; i++)
                _templateIndexByMaterialClass[i] = -1;

            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            bool hasFloraTemplates = floraTemplates != null && floraTemplates.Length > 0;
            int validTemplateCount = 0;
            int totalLootEntries = 0;

            if (hasFloraTemplates)
            {
                for (int i = 0; i < floraTemplates.Length; i++)
                {
                    FloraDataTemplate floraTemplate = floraTemplates[i];
                    HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                    if (floraTemplate == null || template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }
            else if (harvestTemplates != null)
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }

            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            _templateDescriptors = new NativeArray<HarvestableTemplate.RuntimeDescriptor>(
                math.max(1, validTemplateCount),
                DataVaultExemptHarvestTemplateAllocator,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: RuntimeDescriptor[templateCount] - flora-resolved harvest runtime table - owner: DestructibleOrganicManager
            _lootEntries = new NativeArray<HarvestableTemplate.LootRuntimeEntry>(
                math.max(1, totalLootEntries),
                DataVaultExemptHarvestTemplateAllocator,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: LootRuntimeEntry[totalLootEntries] - flattened harvest loot runtime table - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(_templateDescriptors, NativeMemoryOwner, nameof(_templateDescriptors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_lootEntries, NativeMemoryOwner, nameof(_lootEntries), NativeMemoryLifetime);
            _descriptorHarvestTemplates = new HarvestableTemplate[math.max(1, validTemplateCount)]; // COLD ALLOC: HarvestableTemplate[templateCount] - descriptor-to-authoring lookup for flora template harvest routing - owner: DestructibleOrganicManager
            _floraCategoryByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - flora-category cache used by harvest-state thresholds - owner: DestructibleOrganicManager
            _audioMaterialByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - flora audio-material routing cache used by harvest-state audio dispatch - owner: DestructibleOrganicManager
            _growthTimeSecondsByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - authored flora growth durations - owner: DestructibleOrganicManager
            _sporeAcousticEmitterByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - mature spore acoustic emitter flags - owner: DestructibleOrganicManager
            _sporeAcousticClipByDescriptorIndex = new AudioClip[math.max(1, validTemplateCount)]; // COLD ALLOC: AudioClip[templateCount] - mature spore acoustic clip refs - owner: DestructibleOrganicManager
            _sporePulseFrequencyByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - mature spore pulse cadence copied from VAT authoring - owner: DestructibleOrganicManager
            _sporeAcousticVolumeByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - mature spore acoustic volume per descriptor - owner: DestructibleOrganicManager
            _harvestDescriptorIndexByFloraTemplateIndex = hasFloraTemplates
                ? new int[floraTemplates.Length]
                : Array.Empty<int>(); // COLD ALLOC: int[floraTemplates.Length] - flora-template to descriptor mapping - owner: DestructibleOrganicManager

            if (hasFloraTemplates)
            {
                for (int i = 0; i < _harvestDescriptorIndexByFloraTemplateIndex.Length; i++)
                    _harvestDescriptorIndexByFloraTemplateIndex[i] = -1;
            }

            if (validTemplateCount <= 0)
                return;

            int descriptorWriteIndex = 0;
            int lootWriteIndex = 0;
            NativeList<HarvestableTemplate.LootRuntimeEntry> lootScratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(math.max(1, totalLootEntries), Allocator.Temp);
            try
            {
                if (hasFloraTemplates)
                {
                    for (int i = 0; i < floraTemplates.Length; i++)
                    {
                        FloraDataTemplate floraTemplate = floraTemplates[i];
                        HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                        if (floraTemplate == null || template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < _lootEntries.Length; lootIndex++)
                        {
                            _lootEntries[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        if (descriptorWriteIndex >= _templateDescriptors.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        FloraDataTemplate.RuntimeDescriptor floraRuntimeDescriptor = floraTemplate.BuildRuntimeDescriptor();
                        descriptor.StableHashId = floraRuntimeDescriptor.StableHashId;
                        descriptor.BaseHealth = floraTemplate.MaxHealth;
                        _templateDescriptors[descriptorWriteIndex] = descriptor;
                        _descriptorHarvestTemplates[descriptorWriteIndex] = template;
                        _floraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)floraTemplate.Category;
                        _audioMaterialByDescriptorIndex[descriptorWriteIndex] = floraTemplate.AudioMaterialID;
                        _growthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = floraTemplate.GrowthTimeSeconds;
                        _sporeAcousticEmitterByDescriptorIndex[descriptorWriteIndex] = floraTemplate.EmitsMatureSporeAcoustic ? (byte)1 : (byte)0;
                        _sporeAcousticClipByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticClip;
                        _sporePulseFrequencyByDescriptorIndex[descriptorWriteIndex] = floraTemplate.PulseFrequency;
                        _sporeAcousticVolumeByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticVolume;
                        _harvestDescriptorIndexByFloraTemplateIndex[i] = descriptorWriteIndex;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length && _templateIndexByMaterialClass[materialIndex] < 0)
                            _templateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
                else if (harvestTemplates != null)
                {
                    for (int i = 0; i < harvestTemplates.Length; i++)
                    {
                        HarvestableTemplate template = harvestTemplates[i];
                        if (template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < _lootEntries.Length; lootIndex++)
                        {
                            _lootEntries[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        if (descriptorWriteIndex >= _templateDescriptors.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        _templateDescriptors[descriptorWriteIndex] = descriptor;
                        _descriptorHarvestTemplates[descriptorWriteIndex] = template;
                        _floraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)InferCategoryFromMaterialClass(template.TemplateMaterialClass);
                        _growthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = 480f;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length && _templateIndexByMaterialClass[materialIndex] < 0)
                            _templateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
            }
            finally
            {
                if (lootScratch.IsCreated)
                    lootScratch.Dispose();
            }
        }

        private int ApplyConstructionDecompositionInLane(bool underwater, double3 centerUniversePosition, double radiusSq)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int decomposedCount = 0;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (materialClass == HarvestableTemplate.MaterialClass.None)
                    continue;

                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                double distanceSq = ResolveConstructionDistanceSq(centerUniversePosition, rootPosition, metadata[i], types[i]);
                if (distanceSq > radiusSq)
                    continue;

                int templateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                ApplyPassiveDecomposition(underwater, i, instanceUid, materialClass, templateIndex, rootPosition);
                decomposedCount++;
            }

            return decomposedCount;
        }

        private int ApplyDefoliantDeadZoneInLane(bool underwater, double3 centerUniversePosition, double radiusSq)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int killedCount = 0;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass))
                    continue;

                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                if (math.lengthsq(ToDouble3(rootPosition) - centerUniversePosition) > radiusSq)
                    continue;

                int templateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                ApplyPassiveDecomposition(underwater, i, instanceUid, materialClass, templateIndex, rootPosition);
                PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds() - OrganicDecompositionDurationSeconds);
                ApplyDecompositionToLaneInstance(underwater, i, instanceUid, 1f);
                killedCount++;
            }

            return killedCount;
        }

        private void BuildFloraTemplateHarvestMap()
        {
            if (_harvestDescriptorIndexByFloraTemplateIndex != null && _harvestDescriptorIndexByFloraTemplateIndex.Length > 0)
                return;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            FloraDataTemplate[] floraTemplateAssets = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            if (floraTemplateAssets == null || floraTemplateAssets.Length == 0)
            {
                _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
                return;
            }

            // COLD ALLOC: int[floraTemplateAssets.Length] - flora-template to harvest-descriptor lookup for instance-specific loot routing - owner: DestructibleOrganicManager
            int[] mapping = new int[floraTemplateAssets.Length];
            for (int i = 0; i < mapping.Length; i++)
                mapping[i] = -1;

            for (int i = 0; i < floraTemplateAssets.Length; i++)
            {
                FloraDataTemplate floraTemplate = floraTemplateAssets[i];
                HarvestableTemplate harvestTemplate = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                if (harvestTemplate == null || _descriptorHarvestTemplates == null)
                    continue;

                for (int descriptorIndex = 0; descriptorIndex < _descriptorHarvestTemplates.Length; descriptorIndex++)
                {
                    if (_descriptorHarvestTemplates[descriptorIndex] != harvestTemplate)
                        continue;

                    mapping[i] = descriptorIndex;
                    break;
                }
            }

            _harvestDescriptorIndexByFloraTemplateIndex = mapping;
        }

        private static FloraDataTemplate.FloraCategory InferCategoryFromMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            switch (materialClass)
            {
                case HarvestableTemplate.MaterialClass.Kelp:
                    return FloraDataTemplate.FloraCategory.HarvestableKelp;
                case HarvestableTemplate.MaterialClass.Coral:
                case HarvestableTemplate.MaterialClass.TitaniumOutcrop:
                    return FloraDataTemplate.FloraCategory.HardCoral;
                case HarvestableTemplate.MaterialClass.Sargassum:
                    return FloraDataTemplate.FloraCategory.GiantSargassum;
                default:
                    return FloraDataTemplate.FloraCategory.MicroGrass;
            }
        }

        private void BuildYieldMaterialLut()
        {
            DisposeNativeArray(ref _yieldMaterialLut);
            _yieldMaterialLut = new NativeArray<EntropyYieldMaterialLutEntry>(
                math.max(1, MaterialClassCount),
                DataVaultExemptYieldMaterialAllocator,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: EntropyYieldMaterialLutEntry[MaterialClassCount] - deterministic density/unit-mass lookup for burst flora yield - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(_yieldMaterialLut, NativeMemoryOwner, nameof(_yieldMaterialLut), NativeMemoryLifetime);

            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.None, 1000f, 1f, 0.5f, 0f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Kelp, 460f, 1.2f, 0.58f, 0.08f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Coral, 1320f, 2.5f, 0.65f, 0.16f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.TitaniumOutcrop, 4480f, 4.5f, 0.78f, 0.22f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Sargassum, 310f, 1.0f, 0.52f, 0.05f);
        }

        private void WriteYieldMaterialLut(
            HarvestableTemplate.MaterialClass materialClass,
            float densityKgPerM3,
            float unitItemMassKg,
            float minimumRecovery,
            float qualityBias)
        {
            if (!_yieldMaterialLut.IsCreated)
                return;

            int materialIndex = (int)materialClass;
            if (materialIndex < 0 || materialIndex >= _yieldMaterialLut.Length)
                return;

            _yieldMaterialLut[materialIndex] = new EntropyYieldMaterialLutEntry
            {
                DensityKgPerM3 = Mathf.Max(0.01f, densityKgPerM3),
                UnitItemMassKg = Mathf.Max(0.01f, unitItemMassKg),
                MinimumRecovery = Mathf.Clamp01(minimumRecovery),
                QualityBias = Mathf.Clamp01(qualityBias)
            };
        }

        private static int CountTemplateLootEntries(HarvestableTemplate template)
        {
            NativeList<HarvestableTemplate.LootRuntimeEntry> scratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(32, Allocator.Temp);
            try
            {
                return template != null ? template.CopyLootTableNonAlloc(scratch) : 0;
            }
            finally
            {
                if (scratch.IsCreated)
                    scratch.Dispose();
            }
        }

        private void RefreshActiveCachesIfNeeded(bool force)
        {
            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return;

            if (force || _surfaceRevision != vegetationBridge.ActiveSurfaceAggregateRevision)
                SyncLane(false);

            if (force || _underwaterRevision != vegetationBridge.ActiveUnderwaterAggregateRevision)
                SyncLane(true);
        }

        private void SyncLane(bool underwater)
        {
            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            NativeArray<int>.ReadOnly semanticTypes;
            int count;
            int semanticCount;
            bool hasNativePayload;
            bool hasSemanticPayload;
            if (underwater)
            {
                hasNativePayload = vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount);
            }
            else
            {
                hasNativePayload = vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            }

            if (!hasNativePayload || !hasSemanticPayload || count <= 0 || semanticCount < count)
            {
                if (underwater)
                {
                    _underwaterMatrices = default;
                    _underwaterMetadata = default;
                    _underwaterTypes = default;
                    _underwaterSemanticTypes = default;
                    _underwaterCount = 0;
                    _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
                }
                else
                {
                    _surfaceMatrices = default;
                    _surfaceMetadata = default;
                    _surfaceTypes = default;
                    _surfaceSemanticTypes = default;
                    _surfaceCount = 0;
                    _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
                }

                return;
            }

            NativeArray<uint> instanceUids = underwater
                ? EnsureLaneCapacity(ref _underwaterInstanceUids, count, nameof(_underwaterInstanceUids))
                : EnsureLaneCapacity(ref _surfaceInstanceUids, count, nameof(_surfaceInstanceUids));
            NativeArray<byte> materialClasses = underwater
                ? EnsureLaneCapacity(ref _underwaterMaterialClasses, count, nameof(_underwaterMaterialClasses))
                : EnsureLaneCapacity(ref _surfaceMaterialClasses, count, nameof(_surfaceMaterialClasses));
            NativeArray<Unity.Mathematics.half> health = underwater
                ? EnsureLaneCapacity(ref _underwaterHealth, count, nameof(_underwaterHealth))
                : EnsureLaneCapacity(ref _surfaceHealth, count, nameof(_surfaceHealth));
            if (underwater)
            {
                EnsureDearLieVaultLaneCapacity(true, count);
            }
            else
            {
                EnsureDearLieVaultLaneCapacity(false, count);
            }
            float currentTime = ResolveOrganicClockSeconds();

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = ComputeStableInstanceUid(matrices[i], metadata[i], types[i], semanticTypes[i]);
                HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(types[i], semanticTypes[i]);
                int templateIndex = ResolveTemplateIndex(metadata[i], fallbackMaterialClass);
                HarvestableTemplate.MaterialClass materialClass = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? (HarvestableTemplate.MaterialClass)_templateDescriptors[templateIndex].MaterialClassId
                    : fallbackMaterialClass;
                instanceUids[i] = instanceUid;
                materialClasses[i] = (byte)materialClass;
                CacheBaseScale(instanceUid, metadata[i]);
                PrimeUntouchedClock(instanceUid, currentTime);
                byte runtimeFlags = ResolveRuntimeFlags(instanceUid, materialClass, semanticTypes[i], metadata[i].RuntimeFlags);
                ApplyRuntimeFlags(ref metadata, i, runtimeFlags);
                SetRuntimeState(ref metadata, i, HectonVegetationInstanceData.RuntimeStateIdle);
                float defaultHealth = templateIndex >= 0 ? _templateDescriptors[templateIndex].BaseHealth : 0f;
                float resolvedHealth = defaultHealth;
                bool hasPersistedFloraState = TryResolvePersistedFloraState(instanceUid, out float persistedHealth01, out float persistedHeightScale01);
                bool isDestroyed = _destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid);
                bool isDefoliantSuppressed = !isDestroyed &&
                                             IsConsumableFloraMaterialClass(materialClass) &&
                                             ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZoneAbsolute(ExtractTranslation(matrices[i]));
                if (isDefoliantSuppressed)
                {
                    RegisterDefoliantDestroyedInstance(instanceUid, templateIndex, ExtractTranslation(matrices[i]));
                    ApplyRuntimeFlags(ref metadata, i, MarkDeadRuntimeFlag(instanceUid));
                    isDestroyed = true;
                }
                float regrowthProgress = 0f;
                bool isRegrowing = _regrowthProgressByInstanceUid.IsCreated &&
                                   _regrowthProgressByInstanceUid.TryGetValue(instanceUid, out regrowthProgress);
                if (_healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half savedHealth))
                    resolvedHealth = math.max(0f, (float)savedHealth);
                else if (hasPersistedFloraState && templateIndex >= 0)
                    resolvedHealth = Mathf.Max(0f, defaultHealth * Mathf.Clamp01(persistedHealth01));

                if (_healthByInstanceUid.IsCreated)
                {
                    _healthByInstanceUid.Remove(instanceUid);
                    _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resolvedHealth);
                }

                health[i] = (Unity.Mathematics.half)resolvedHealth;
                if (isRegrowing)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);

                    ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, regrowthProgress);
                }
                else if (isDestroyed || resolvedHealth <= 0.0001f)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                    ApplyDearLieMatrixScaleZero(ref matrices, i);
                    ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
                }
                else if (templateIndex >= 0 && resolvedHealth < defaultHealth)
                {
                    float damage01 = ResolveDamageProgress(defaultHealth, resolvedHealth);
                    UpdateDamageProgressCache(instanceUid, damage01);
                    float normalizedHeightScale = hasPersistedFloraState
                        ? Mathf.Clamp01(persistedHeightScale01)
                        : ResolveNormalizedHeightScale(templateIndex, defaultHealth, resolvedHealth);
                    ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, templateIndex, persistedHealth01, normalizedHeightScale, damage01, currentTime);
                }
                else if (_damageVisualProgressByInstanceUid.IsCreated)
                {
                    _damageVisualProgressByInstanceUid.Remove(instanceUid);
                }

                if (!isRegrowing && !isDestroyed && resolvedHealth > 0.0001f)
                {
                    float maturationScale = ResolveMaturationScaleMultiplier(instanceUid);
                    if (maturationScale < 0.9999f)
                        ApplyMaturationVisualToLaneInstance(underwater, i, instanceUid, ResolveMaturationYieldMultiplier(instanceUid), maturationScale);
                }
            }

            if (underwater)
            {
                _underwaterMatrices = matrices;
                _underwaterMetadata = metadata;
                _underwaterTypes = types;
                _underwaterSemanticTypes = semanticTypes;
                _underwaterCount = count;
                _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
            }
            else
            {
                _surfaceMatrices = matrices;
                _surfaceMetadata = metadata;
                _surfaceTypes = types;
                _surfaceSemanticTypes = semanticTypes;
                _surfaceCount = count;
                _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
            }
        }

        private void SyncDestroyedFloraFromPersistence()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || !_destroyedFloraScratch.IsCreated || !_destroyedByInstanceUid.IsCreated)
                return;

            _destroyedByInstanceUid.Clear();
            _destroyedFloraScratch.Clear();
            registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _destroyedFloraScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if (ResolveDescriptorIndexByPersistentIdHash(record.ItemPersistentIdHash) < 0)
                {
                    registry.TryClearDestroyedFlora(record.InstanceUid);
                    continue;
                }

                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid))
                    continue;

                _destroyedByInstanceUid.TryAdd(record.InstanceUid, 1);
                ClearOrganicLifecycleState(record.InstanceUid);
                PrimeDecompositionState(record.InstanceUid, ResolveOrganicClockSeconds() - OrganicDecompositionDurationSeconds);
                _healthByInstanceUid.Remove(record.InstanceUid);
                _healthByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)0f);
            }
        }

        private void SyncFloraStateOverridesFromPersistence()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null ||
                !_floraStateOverrideScratch.IsCreated ||
                !_persistedHealth01ByInstanceUid.IsCreated ||
                !_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                return;
            }

            _floraStateOverrideScratch.Clear();
            _persistedHealth01ByInstanceUid.Clear();
            _persistedHeightScale01ByInstanceUid.Clear();
            registry.CopyFloraStateOverrideDeltas(_floraStateOverrideScratch);
            for (int i = 0; i < _floraStateOverrideScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _floraStateOverrideScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if ((_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(record.InstanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid)))
                {
                    continue;
                }

                int descriptorIndex = ResolveDescriptorIndexByPersistentIdHash(record.ItemPersistentIdHash);
                if (descriptorIndex < 0)
                {
                    registry.TryClearFloraStateOverride(record.InstanceUid);
                    continue;
                }

                PersistentWorldRegistry.UnpackFloraStateOverride(record.Quantity, out float persistedHealth01, out byte persistedHarvestState);
                float normalizedHealth = math.saturate(persistedHealth01);
                float normalizedHeightScale = ResolveNormalizedHeightScaleFromHarvestState(
                    descriptorIndex,
                    normalizedHealth,
                    ResolvePersistedHarvestState(persistedHarvestState));
                _persistedHealth01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)normalizedHealth);
                _persistedHeightScale01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)math.saturate(normalizedHeightScale));
            }
        }

        private void CompleteYieldJobIfNeeded(bool force = true)
        {
            if (!_yieldScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _yieldJobHandle, force))
                return;

            _yieldScheduled = false;
            _scheduledYieldCount = 0;
        }

        private void ScheduleYieldJobIfNeeded()
        {
            if (_yieldScheduled ||
                !_pendingYieldEvents.IsCreated ||
                _pendingYieldEvents.Length <= 0 ||
                !_dropBuffer.IsCreated ||
                !_dropBuffer.IsEmpty ||
                !_yieldMaterialLut.IsCreated)
                return;

            int pendingCount = _pendingYieldEvents.Length;
            int eventCount = math.min(pendingCount, math.min(_dropBuffer.Capacity, MaxOrganicDropRecordsPerFrame));
            EnsureNativeCapacity(ref _yieldJobInput, eventCount, nameof(_yieldJobInput));
            for (int i = 0; i < eventCount; i++)
            {
                _yieldJobInput[i] = _pendingYieldEvents[i];
            }

            VoxelDynamicNavGridRuntime.EnqueueDestroyedOrganicEvents(_yieldJobInput, eventCount);

            int remainderCount = pendingCount - eventCount;
            if (remainderCount > 0)
            {
                for (int i = 0; i < remainderCount; i++)
                    _pendingYieldEvents[i] = _pendingYieldEvents[eventCount + i];

                _pendingYieldEvents.ResizeUninitialized(remainderCount);
                _deferredYieldScheduleFrame = math.max(_deferredYieldScheduleFrame, Time.frameCount + 1);
            }
            else
            {
                _pendingYieldEvents.Clear();
                _deferredYieldScheduleFrame = -1;
            }

            _scheduledYieldCount = eventCount;
            _yieldJobHandle = _dropBuffer.ScheduleEntropyYieldJob(
                _yieldJobInput,
                _templateDescriptors,
                _lootEntries,
                _yieldMaterialLut,
                eventCount,
                8);
            _yieldScheduled = true;
        }

        private bool DrainDropBuffer()
        {
            if (!_dropBuffer.IsCreated)
                return true;

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            PlayerInventory playerInventory = playerInventoryService != null ? playerInventoryService.Inventory : null;
            Hecton8.SaveSystem.ItemCatalog itemCatalog = playerInventory != null ? playerInventory.ItemCatalog : null;
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            int remainingBudget = math.min(_dropBuffer.Capacity, MaxOrganicDropRecordsPerFrame);
            while (remainingBudget-- > 0 && _dropBuffer.TryDequeue(out ItemDropData drop))
            {
                if (drop.ItemHashId == 0 || drop.Quantity == 0)
                    continue;

                int rejectedQuantity = drop.Quantity;
                if (playerInventory != null)
                {
                    PlayerInventory.ScavengeAttemptResult result =
                        playerInventory.ScavengeAttempt(drop.ItemHashId, drop.Quantity, playerInventory.transform);
                    rejectedQuantity = result.RejectedQuantity;
                }

                if (rejectedQuantity > 0 && registry != null && itemCatalog != null)
                {
                    Vector3 runtimePosition = new Vector3(drop.Position.x, drop.Position.y, drop.Position.z);
                    registry.TryRegisterDroppedItem(drop.ItemHashId, itemCatalog, rejectedQuantity, runtimePosition);
                }
            }

            return _dropBuffer.IsEmpty;
        }

        private void RefreshCorpseResourceNodes(float currentTime)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                if (record.RemainingUnits <= 0f || currentTime >= record.ExpireTime)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                    _corpseResourceNodes[i] = record;
                    continue;
                }

                float normalizedDecay = ResolveCorpseCapacityFraction01(in record);
                float bloodIntensity = math.lerp(0.35f, record.BloodIntensity, normalizedDecay);
                ChemicalInfluenceGrid.QueueBloodScent(record.Position, bloodIntensity);
            }

            TrimTrailingCorpseNodes();
        }

        private static float ResolveCorpseCapacityFraction01(in CorpseResourceNodeRecord record)
        {
            float initialUnits = record.InitialUnits > 0f ? record.InitialUnits : record.RemainingUnits;
            return initialUnits > 0f
                ? Mathf.Clamp01(record.RemainingUnits / initialUnits)
                : 0f;
        }

        private int FindWeakestCorpseNodeIndex()
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0)
                return -1;

            int weakestIndex = -1;
            float weakestScore = float.MaxValue;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                float score = record.Active == 0 ? float.MinValue : record.RemainingUnits;
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                weakestIndex = i;
            }

            return weakestIndex;
        }

        private void TrimTrailingCorpseNodes()
        {
            while (_corpseResourceNodeCount > 0 && _corpseResourceNodes[_corpseResourceNodeCount - 1].Active == 0)
                _corpseResourceNodeCount--;
        }

        internal bool TryResolveNearestConsumableFlora(Vector3 runtimePosition, float searchRadius, out Vector3 floraPosition, out uint instanceUid)
        {
            floraPosition = Vector3.zero;
            instanceUid = 0u;
            if (_dearLieJobScheduled)
                return false;

            float bestDistanceSq = searchRadius * searchRadius;
            bool found = TryResolveNearestConsumableFloraInLane(runtimePosition, true, ref bestDistanceSq, ref floraPosition, ref instanceUid);
            if (TryResolveNearestConsumableFloraInLane(runtimePosition, false, ref bestDistanceSq, ref floraPosition, ref instanceUid))
                found = true;

            return found;
        }

        public bool TryResolveNearestHarvestInteractionPoint(
            Vector3 handRuntimePosition,
            float searchRadius,
            uint toolCapabilityMask,
            out FloraHarvestInteractionPoint interactionPoint)
        {
            interactionPoint = default;
            if (_dearLieJobScheduled)
                return false;

            if (vegetationBridge == null)
                vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (searchRadius <= 0f || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                handRuntimePosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                toolCapabilityMask,
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition))
            {
                return false;
            }

            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return false;
            }

            Vector3 snapPosition = ResolveHarvestSnapPosition(
                handRuntimePosition,
                instancePosition,
                metadata[activeIndex],
                types[activeIndex]);
            Vector3 normal = handRuntimePosition - snapPosition;
            normal = NormalizeVector3Fast(normal, Vector3.up);

            if (!TryResolveAupFromRuntimeOrigin(snapPosition, out AbsoluteUniversePosition snapAup))
                return false;

            interactionPoint = new FloraHarvestInteractionPoint(
                instanceUid,
                snapAup,
                snapPosition,
                normal,
                materialClass,
                templateIndex,
                1f);
            return true;
        }

        internal int CollectNearestConsumableFlora(
            Vector3 runtimePosition,
            float searchRadius,
            uint[] instanceUids,
            Vector3[] positions)
        {
            if (_dearLieJobScheduled || instanceUids == null || positions == null)
                return 0;

            int capacity = math.min(instanceUids.Length, positions.Length);
            if (capacity <= 0)
                return 0;

            for (int i = 0; i < capacity; i++)
            {
                instanceUids[i] = 0u;
                positions[i] = Vector3.zero;
            }

            Span<float> bestDistanceSq = stackalloc float[4];
            int boundedCapacity = math.min(capacity, 4);
            for (int i = 0; i < boundedCapacity; i++)
                bestDistanceSq[i] = float.MaxValue;

            int collectedCount = 0;
            CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, true, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
            CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, false, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
            return collectedCount;
        }

        internal bool AreTrackedFloraDestroyed(uint[] instanceUids, int trackedCount)
        {
            if (instanceUids == null || trackedCount <= 0)
                return false;

            if (!_destroyedByInstanceUid.IsCreated)
                return false;

            int upperBound = math.min(trackedCount, instanceUids.Length);
            bool hasTrackedInstance = false;
            for (int i = 0; i < upperBound; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                hasTrackedInstance = true;
                if (!_destroyedByInstanceUid.ContainsKey(instanceUid))
                    return false;
            }

            return hasTrackedInstance;
        }

        internal bool TryConsumeFlora(uint instanceUid)
        {
            if (_dearLieJobScheduled ||
                instanceUid == 0u ||
                !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
                return false;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= count ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (!IsConsumableFloraMaterialClass(materialClass))
                return false;

            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
            return true;
        }

        private bool TryResolveNearestHarvestTarget(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            out bool underwater,
            out int activeIndex,
            out uint instanceUid,
            out HarvestableTemplate.MaterialClass materialClass,
            out int templateIndex,
            out Matrix4x4 instanceMatrix,
            out Vector3 instancePosition)
        {
            underwater = false;
            activeIndex = -1;
            instanceUid = 0u;
            materialClass = HarvestableTemplate.MaterialClass.None;
            templateIndex = -1;
            instanceMatrix = Matrix4x4.identity;
            instancePosition = Vector3.zero;

            float bestDistanceSq = float.MaxValue;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                toolCapabilityMask,
                true,
                ref bestDistanceSq,
                ref activeIndex,
                ref instanceUid,
                ref materialClass,
                ref templateIndex,
                ref instanceMatrix,
                ref instancePosition))
            {
                underwater = true;
            }

            int surfaceIndex = -1;
            uint surfaceUid = 0u;
            HarvestableTemplate.MaterialClass surfaceMaterial = HarvestableTemplate.MaterialClass.None;
            int surfaceTemplateIndex = -1;
            Matrix4x4 surfaceMatrix = Matrix4x4.identity;
            Vector3 surfacePosition = Vector3.zero;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                toolCapabilityMask,
                false,
                ref bestDistanceSq,
                ref surfaceIndex,
                ref surfaceUid,
                ref surfaceMaterial,
                ref surfaceTemplateIndex,
                ref surfaceMatrix,
                ref surfacePosition))
            {
                underwater = false;
                activeIndex = surfaceIndex;
                instanceUid = surfaceUid;
                materialClass = surfaceMaterial;
                templateIndex = surfaceTemplateIndex;
                instanceMatrix = surfaceMatrix;
                instancePosition = surfacePosition;
            }

            return activeIndex >= 0 && instanceUid != 0u && templateIndex >= 0;
        }

        private static Vector3 ResolveHarvestSnapPosition(
            Vector3 handRuntimePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(math.abs(metadata.HeightScale)));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                return ClosestPointOnSegment(rootPosition, top, handRuntimePosition);
            }

            float height01 = math.saturate(math.abs(metadata.HeightScale));
            float verticalBias = vegetationType == HectonVegetationInstanceType.Sargassum
                ? math.lerp(0.18f, 0.85f, height01)
                : math.lerp(0.12f, 0.65f, height01);
            return rootPosition + Vector3.up * verticalBias;
        }

        private bool TryResolveNearestConsumableFloraInLane(
            Vector3 runtimePosition,
            bool underwater,
            ref float bestDistanceSq,
            ref Vector3 bestPosition,
            ref uint bestInstanceUid)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return false;
            }

            bool found = false;
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (candidateUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(candidateUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(candidateUid)))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestPosition = candidatePosition;
                bestInstanceUid = candidateUid;
                found = true;
            }

            return found;
        }

        private void CollectNearestConsumableFloraInLane(
            Vector3 runtimePosition,
            float searchRadius,
            bool underwater,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (capacity <= 0)
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return;
            }

            float searchRadiusSq = math.max(0.0001f, searchRadius * searchRadius);
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (candidateUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(candidateUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(candidateUid)))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > searchRadiusSq)
                    continue;

                TryInsertConsumableCandidate(
                    candidateUid,
                    candidatePosition,
                    distanceSq,
                    bestInstanceUids,
                    bestPositions,
                    bestDistanceSq,
                    capacity,
                    ref collectedCount);
            }
        }

        private static void TryInsertConsumableCandidate(
            uint candidateUid,
            Vector3 candidatePosition,
            float distanceSq,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (candidateUid == 0u || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == candidateUid)
                    return;
            }

            int insertIndex = -1;
            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == 0u || distanceSq < bestDistanceSq[i])
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
                return;

            for (int i = capacity - 1; i > insertIndex; i--)
            {
                bestInstanceUids[i] = bestInstanceUids[i - 1];
                bestPositions[i] = bestPositions[i - 1];
                bestDistanceSq[i] = bestDistanceSq[i - 1];
            }

            bestInstanceUids[insertIndex] = candidateUid;
            bestPositions[insertIndex] = candidatePosition;
            bestDistanceSq[insertIndex] = distanceSq;
            collectedCount = math.min(collectedCount + 1, capacity);
        }

        private bool TryResolveNearestHarvestTargetInLane(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            bool underwater,
            ref float bestDistanceSq,
            ref int bestIndex,
            ref uint bestUid,
            ref HarvestableTemplate.MaterialClass bestMaterialClass,
            ref int bestTemplateIndex,
            ref Matrix4x4 bestMatrix,
            ref Vector3 bestPosition)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated || !metadata.IsCreated || !types.IsCreated || !instanceUids.IsCreated || !materialClasses.IsCreated || !health.IsCreated || count <= 0)
                return false;

            float searchRadiusSq = searchRadius * searchRadius;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || (float)health[i] <= 0.0001f)
                    continue;

                HectonVegetationInstanceData instanceMetadata = metadata[i];
                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                int templateIndex = ResolveTemplateIndex(instanceMetadata, materialClass);
                if (materialClass == HarvestableTemplate.MaterialClass.None || templateIndex < 0)
                    continue;

                if (!IsToolCompatible(instanceMetadata, toolCapabilityMask))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveHarvestDistanceSq(hitPoint, rootPosition, instanceMetadata, types[i], searchRadiusSq, kelpHeightTolerance);
                if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                found = true;
                bestDistanceSq = distanceSq;
                bestIndex = i;
                bestUid = instanceUid;
                bestMaterialClass = materialClass;
                bestTemplateIndex = templateIndex;
                bestMatrix = matrices[i];
                bestPosition = rootPosition;
            }

            return found;
        }

        private void DestroyResolvedInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower)
        {
            if (!_destroyedByInstanceUid.IsCreated)
                return;

            if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);
            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ClearOrganicLifecycleState(instanceUid);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds());
            SetLaneHealth(underwater, activeIndex, 0f);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            float parentMassKg = ResolveParentMassKg(underwater, activeIndex, materialClass, templateIndex);
            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PublishExternalInteraction(instancePosition, NormalizeVector3Fast(hitNormal, Vector3.up) * (normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius * 1.25f);
            SpawnDebris(materialClass, instanceMatrix, instancePosition, hitPoint, hitNormal, normalizedPower, instanceUid);
            QueueYieldEvent(
                instancePosition,
                normalizedPower,
                instanceUid,
                templateIndex,
                materialClass,
                parentMassKg,
                1f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private void ApplyPassiveDecomposition(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Vector3 instancePosition)
        {
            if (!_destroyedByInstanceUid.IsCreated || instanceUid == 0u || _destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);
            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ClearOrganicLifecycleState(instanceUid);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);
            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds());
            SetLaneHealth(underwater, activeIndex, 0f);
            float parentMassKg = ResolveParentMassKg(underwater, activeIndex, materialClass, templateIndex);
            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);

            QueueYieldEvent(
                instancePosition,
                0.1f,
                instanceUid,
                templateIndex,
                materialClass,
                parentMassKg,
                0f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);
        }

        private void SpawnDebris(
            HarvestableTemplate.MaterialClass materialClass,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower,
            uint instanceUid)
        {
            OrganicDebrisProfile profile = ResolveDebrisProfile(materialClass);
            if (profile == null || !profile.IsValid)
                return;

            float3 spawnPosition = new float3(hitPoint.x, hitPoint.y, hitPoint.z);
            if (!math.all(math.isfinite(spawnPosition)))
                spawnPosition = new float3(instancePosition.x, instancePosition.y, instancePosition.z);

            if (!math.all(math.isfinite(spawnPosition)))
                return;

            Vector3 debrisRuntimePosition = new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z);
            if (!TryResolveAupFromRuntimeOrigin(debrisRuntimePosition, out AbsoluteUniversePosition debrisAup))
                return;

            float safePower = math.isfinite(normalizedPower) ? math.max(0.1f, normalizedPower) : 0.1f;
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = debrisAup,
                SpeciesHash = unchecked((uint)materialClass) ^ 0x4F524741u,
                SourceEntityId = instanceUid ^ 0x7F4A7C15u,
                Intensity01 = math.saturate(safePower),
                DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = 0
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001DestructibleOrganicManagerSignalPushDropCount);
        }

        private void QueueYieldEvent(
            Vector3 instancePosition,
            float normalizedPower,
            uint instanceUid,
            int templateIndex,
            HarvestableTemplate.MaterialClass materialClass,
            float parentMassKg,
            float damage01,
            float3 navObstacleCenter,
            float3 navObstacleExtents)
        {
            if (!_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length >= _pendingYieldEvents.Capacity)
                return;

            _pendingYieldEvents.AddNoResize(new DestroyedOrganicEvent
            {
                Position = new float3(instancePosition.x, instancePosition.y, instancePosition.z),
                NavObstacleCenter = navObstacleCenter,
                NavObstacleExtents = navObstacleExtents,
                ToolPower = Mathf.Max(0.1f, normalizedPower),
                ParentMassKg = parentMassKg <= 0.0001f ? 0f : Mathf.Max(0.05f, parentMassKg),
                Damage01 = Mathf.Clamp01(damage01),
                InstanceUid = instanceUid,
                TemplateIndex = templateIndex,
                MaterialClassId = (int)materialClass
            });
        }

        private void PublishExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            floraInteractionManager?.RegisterExternalInteraction(positionWS, velocityWS, radius);
        }

        private OrganicDebrisProfile ResolveDebrisProfile(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => kelpDebrisProfile,
                HarvestableTemplate.MaterialClass.Coral => coralDebrisProfile,
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => titaniumDebrisProfile,
                HarvestableTemplate.MaterialClass.Sargassum => sargassumDebrisProfile,
                _ => null
            };
        }

        private int ResolveTemplateIndex(HarvestableTemplate.MaterialClass materialClass)
        {
            int materialIndex = (int)materialClass;
            if (_templateIndexByMaterialClass == null || materialIndex < 0 || materialIndex >= _templateIndexByMaterialClass.Length)
                return -1;

            return _templateIndexByMaterialClass[materialIndex];
        }

        private int ResolveTemplateIndex(HectonVegetationInstanceData metadata, HarvestableTemplate.MaterialClass fallbackMaterialClass)
        {
            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (_harvestDescriptorIndexByFloraTemplateIndex != null &&
                floraTemplateIndex >= 0 &&
                floraTemplateIndex < _harvestDescriptorIndexByFloraTemplateIndex.Length)
            {
                int mappedDescriptorIndex = _harvestDescriptorIndexByFloraTemplateIndex[floraTemplateIndex];
                if (mappedDescriptorIndex >= 0)
                    return mappedDescriptorIndex;
            }

            return ResolveTemplateIndex(fallbackMaterialClass);
        }

        private bool IsToolCompatible(HectonVegetationInstanceData metadata, uint toolCapabilityMask)
        {
            if (toolCapabilityMask == 0u || vegetationBridge == null)
                return true;

            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (!vegetationBridge.TryGetFloraTemplateRuntimeDescriptor(floraTemplateIndex, out FloraDataTemplate.RuntimeDescriptor descriptor))
                return true;

            return descriptor.VulnerabilityMask == 0u || (descriptor.VulnerabilityMask & toolCapabilityMask) != 0u;
        }

        private void CacheBaseScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated || instanceUid == 0u || _baseScaleByInstanceUid.ContainsKey(instanceUid))
                return;

            _baseScaleByInstanceUid.TryAdd(
                instanceUid,
                new float2(
                    Mathf.Max(MinimumDecomposedHeightScale, Mathf.Abs(metadata.HeightScale)),
                    Mathf.Max(MinimumDecomposedWidthScale, Mathf.Clamp01(Mathf.Abs(metadata.WidthScale)))));
        }

        private void PrimeUntouchedClock(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated || _lastOrganicTouchTimeByInstanceUid.ContainsKey(instanceUid))
                return;

            _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
        }

        private void MarkOrganicTouched(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated)
                return;

            _lastOrganicTouchTimeByInstanceUid.Remove(instanceUid);
            _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
        }

        private void ClearOrganicLifecycleState(uint instanceUid)
        {
            if (instanceUid == 0u)
                return;

            if (_lastOrganicTouchTimeByInstanceUid.IsCreated)
                _lastOrganicTouchTimeByInstanceUid.Remove(instanceUid);
            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
            if (_rootMoundAppliedByInstanceUid.IsCreated)
                _rootMoundAppliedByInstanceUid.Remove(instanceUid);
        }

        private void RegisterDefoliantDestroyedInstance(uint instanceUid, int templateIndex, Vector3 instancePosition)
        {
            if (instanceUid == 0u || !_destroyedByInstanceUid.IsCreated)
                return;

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds() - OrganicDecompositionDurationSeconds);
            ClearOrganicLifecycleState(instanceUid);

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private byte ResolveRuntimeFlags(uint instanceUid, HarvestableTemplate.MaterialClass materialClass, int semanticType, float existingRuntimeFlags)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);

            if (_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte existingFlags))
                return existingFlags;

            byte resolvedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);
            bool parasiteEligible = materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                                    materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            if (parasiteEligible)
            {
                uint parasiteHash = instanceUid ^ (uint)(semanticType + 17) * 2246822519u;
                if ((parasiteHash & 0x0Fu) <= 1u)
                    resolvedFlags |= FloraRuntimeFlagHasParasite;
            }

            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, resolvedFlags);
            return resolvedFlags;
        }

        private static void ApplyRuntimeFlags(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, byte runtimeFlags)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData flaggedMetadata = metadata[activeIndex];
            flaggedMetadata.RuntimeFlags = HectonVegetationRuntimeFlagEncoding.WithRuntimeFlags(flaggedMetadata.RuntimeFlags, runtimeFlags);
            metadata[activeIndex] = flaggedMetadata;
        }

        private void ApplyRuntimeFlagsToLaneInstance(bool underwater, int activeIndex, byte runtimeFlags)
        {
            if (underwater)
                ApplyRuntimeFlags(ref _underwaterMetadata, activeIndex, runtimeFlags);
            else
                ApplyRuntimeFlags(ref _surfaceMetadata, activeIndex, runtimeFlags);
        }

        private byte MarkDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return 0;

            byte runtimeFlags = 0;
            _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);
            runtimeFlags |= FloraRuntimeFlagDead;
            _runtimeFlagsByInstanceUid.Remove(instanceUid);
            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, runtimeFlags);
            return runtimeFlags;
        }

        private void ClearDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            if (!_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags))
                return;

            runtimeFlags &= unchecked((byte)~FloraRuntimeFlagDead);
            _runtimeFlagsByInstanceUid.Remove(instanceUid);
            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, runtimeFlags);
        }

        private bool TryResolveNavObstacleForLaneInstance(bool underwater, int activeIndex, out float3 center, out float3 extents)
        {
            center = float3.zero;
            extents = float3.zero;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length ||
                activeIndex >= semanticTypes.Length)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds(
                matrices[activeIndex],
                metadata[activeIndex],
                types[activeIndex],
                semanticTypes[activeIndex],
                out center,
                out extents);
        }

        private static void SetRuntimeState(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, float runtimeState)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData stateMetadata = metadata[activeIndex];
            stateMetadata.RuntimeState = runtimeState;
            metadata[activeIndex] = stateMetadata;
        }

        private void PrimeDecompositionState(uint instanceUid, float decompositionStartTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, decompositionStartTime);
        }

        private float ResolveOrPrimeDecompositionProgress(uint instanceUid, float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return 1f;

            if (!_decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out float startTime))
            {
                startTime = currentTime - OrganicDecompositionDurationSeconds;
                _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, startTime);
            }

            return math.saturate((currentTime - startTime) / OrganicDecompositionDurationSeconds);
        }

        private void ApplyDecompositionToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float entropy01)
        {
            if (underwater)
                ApplyDecompositionMetadata(ref _underwaterMetadata, activeIndex, instanceUid, entropy01);
            else
                ApplyDecompositionMetadata(ref _surfaceMetadata, activeIndex, instanceUid, entropy01);
        }

        internal bool TryEvaluateParasiteExposure(Vector3 runtimePosition, out float exposure01)
        {
            exposure01 = 0f;
            if (_dearLieJobScheduled)
                return false;

            float bestExposure = 0f;
            EvaluateParasiteExposureInLane(runtimePosition, false, ref bestExposure);
            EvaluateParasiteExposureInLane(runtimePosition, true, ref bestExposure);
            exposure01 = Mathf.Clamp01(bestExposure);
            return exposure01 > 0.0001f;
        }

        private void ApplyDamageVisualState(
            uint instanceUid,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float baseHealth,
            float currentHealth,
            float transitionHeightScale,
            bool harvestStateChanged,
            float currentTime)
        {
            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            if (damage01 <= 0.0001f && !harvestStateChanged)
                return;

            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = harvestStateChanged
                ? math.saturate(transitionHeightScale)
                : ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            ApplyDamageToLaneInstance(underwater, activeIndex, instanceUid, templateIndex, normalizedHealth, damage01, normalizedHeightScale, currentTime);
        }

        private void UpdateDamageProgressCache(uint instanceUid, float damage01)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (damage01 > 0.0001f)
                _damageVisualProgressByInstanceUid.TryAdd(instanceUid, damage01);
        }

        private static float ResolveDamageProgress(float baseHealth, float currentHealth)
        {
            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            return math.saturate((0.5f - normalizedHealth) * 2f);
        }

        private HarvestState ResolveHarvestState(int templateIndex, float baseHealth, float currentHealth, float normalizedHeightScale)
        {
            if (currentHealth <= 0.0001f || normalizedHeightScale <= 0.0001f)
                return HarvestState.Dead;

            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            if (normalizedHealth >= HarvestStatePartialThreshold01 && normalizedHeightScale >= HarvestStatePartialThreshold01)
                return HarvestState.Pristine;

            float bareThreshold = ResolveBareThreshold01(templateIndex);
            return normalizedHealth <= bareThreshold || normalizedHeightScale <= bareThreshold
                ? HarvestState.Bare
                : HarvestState.PartiallyHarvested;
        }

        private float ResolveNormalizedHeightScale(int templateIndex, float baseHealth, float currentHealth)
        {
            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            HarvestState state = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHealth);
            switch (state)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        private float ResolveBareThreshold01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.42f;
                case FloraDataTemplate.FloraCategory.MicroGrass:
                    return 0.22f;
                default:
                    return HarvestStateBareThreshold01;
            }
        }

        private float ResolvePartialHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.68f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.74f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.90f;
                default:
                    return 0.82f;
            }
        }

        private float ResolveBareHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.18f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.24f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.58f;
                default:
                    return 0.20f;
            }
        }

        private FloraDataTemplate.FloraCategory ResolveDescriptorCategory(int templateIndex)
        {
            if (_floraCategoryByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _floraCategoryByDescriptorIndex.Length)
                return FloraDataTemplate.FloraCategory.MicroGrass;

            return (FloraDataTemplate.FloraCategory)_floraCategoryByDescriptorIndex[templateIndex];
        }

        private static float ResolveHarvestStateRuntimeState(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                case HarvestState.Dead:
                    return HectonVegetationInstanceData.RuntimeStateDying;
                case HarvestState.PartiallyHarvested:
                    return HectonVegetationInstanceData.RuntimeStateAgitated;
                default:
                    return HectonVegetationInstanceData.RuntimeStateIdle;
            }
        }

        private byte ResolveDescriptorAudioMaterialId(int templateIndex)
        {
            if (_audioMaterialByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _audioMaterialByDescriptorIndex.Length)
                return (byte)FloraDataTemplate.AudioMaterialId.Organic;

            return _audioMaterialByDescriptorIndex[templateIndex];
        }

        private float ResolveNormalizedHeightScaleFromHarvestState(int templateIndex, float normalizedHealth, HarvestState harvestState)
        {
            normalizedHealth = Mathf.Clamp01(normalizedHealth);
            switch (harvestState)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        internal bool IsBareHarvestState(byte packedHarvestState)
        {
            return packedHarvestState == (byte)HarvestState.Bare;
        }

        private void DispatchHarvestAudioTransition(
            uint instanceUid,
            int templateIndex,
            HarvestState previousState,
            HarvestState nextState,
            Vector3 instancePosition)
        {
            if (templateIndex < 0 ||
                templateIndex >= _templateDescriptors.Length ||
                instanceUid == 0u ||
                previousState == nextState)
            {
                return;
            }

            AudioClip clip = ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), nextState);
            if (clip == null)
                return;

            float volume = ResolveHarvestAudioVolume(nextState);
            float pitch = ResolveHarvestAudioPitch(nextState);
            if (TryResolveAupFromRuntimeOrigin(instancePosition, out AbsoluteUniversePosition soundAup) &&
                _harvestAudioSink != null)
            {
                QueueHarvestAudioEvent(new HarvestAudioEvent(soundAup, instancePosition, clip, volume, pitch, true));
                return;
            }

            QueueHarvestAudioEvent(new HarvestAudioEvent(default, instancePosition, clip, volume, pitch, false));
        }

        private AudioClip ResolveHarvestAudioClip(byte audioMaterialId, HarvestState harvestState)
        {
            switch ((FloraDataTemplate.AudioMaterialId)audioMaterialId)
            {
                case FloraDataTemplate.AudioMaterialId.Brittle:
                    return brittleHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Fibrous:
                    return fibrousHarvestClip != null ? fibrousHarvestClip : organicHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Metallic:
                    return metallicHarvestClip != null ? metallicHarvestClip : brittleHarvestClip;
                default:
                    return harvestState == HarvestState.PartiallyHarvested && fibrousHarvestClip != null
                        ? fibrousHarvestClip
                        : organicHarvestClip;
            }
        }

        private float ResolveHarvestAudioVolume(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.15f);
                case HarvestState.Dead:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.25f);
                default:
                    return harvestAudioBaseVolume;
            }
        }

        private static float ResolveHarvestAudioPitch(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return 0.9f;
                case HarvestState.Dead:
                    return 0.82f;
                default:
                    return 1f;
            }
        }

        private void TryDispatchMatureSporeAcoustic(
            uint instanceUid,
            float progress01,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float currentTime)
        {
            if (instanceUid == 0u || !_nextSporeAcousticTimeByInstanceUid.IsCreated)
                return;

            if (progress01 < MatureSporeGrowthThreshold01)
            {
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
                return;
            }

            if (!IsMatureSporeAcousticEmitter(templateIndex))
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return;

            AudioClip clip = ResolveMatureSporeAcousticClip(templateIndex);
            if (clip == null)
                return;

            float pulseFrequency = ResolveMatureSporePulseFrequency(templateIndex);
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            float phaseOffset01 = ResolveSporeShaderPhaseOffset01(instancePosition);
            if (!_nextSporeAcousticTimeByInstanceUid.TryGetValue(instanceUid, out float nextAllowedTime))
            {
                nextAllowedTime = ResolveNextSporePulseTime(currentTime, pulseFrequency, phaseOffset01);
                _nextSporeAcousticTimeByInstanceUid.TryAdd(instanceUid, nextAllowedTime);
                if (currentTime < nextAllowedTime)
                    return;
            }
            else if (currentTime < nextAllowedTime)
            {
                return;
            }

            float volume = ResolveMatureSporeAcousticVolume(templateIndex);
            float pitch = ResolveMatureSporeAcousticPitch(pulseFrequency);
            if (TryResolveAupFromRuntimeOrigin(instancePosition, out AbsoluteUniversePosition soundAup))
            {
                SporeAcousticEvent acousticEvent = new SporeAcousticEvent(
                    soundAup,
                    instancePosition,
                    clip,
                    pulseFrequency,
                    volume,
                    pitch,
                    nextAllowedTime,
                    phaseOffset01,
                    true);
                QueueSporeAcousticEvent(in acousticEvent);
            }
            else
            {
                QueueSporeAcousticEvent(new SporeAcousticEvent(
                    default,
                    instancePosition,
                    clip,
                    pulseFrequency,
                    volume,
                    pitch,
                    nextAllowedTime,
                    phaseOffset01,
                    false));
            }

            _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
            _nextSporeAcousticTimeByInstanceUid.TryAdd(instanceUid, ResolveNextSporePulseTime(currentTime + 0.0001f, pulseFrequency, phaseOffset01));
        }

        private void DispatchSporeAcousticEvent(in SporeAcousticEvent acousticEvent)
        {
            ISpatialAudioHarvestPlaybackSink harvestAudioSink = _harvestAudioSink;
            if (harvestAudioSink != null && acousticEvent.HasAup)
            {
                AbsoluteUniversePosition positionAup = acousticEvent.PositionAup;
                harvestAudioSink.PlaySporeEmissionAtAup(
                    in positionAup,
                    acousticEvent.Clip,
                    acousticEvent.PulseFrequencyHz,
                    acousticEvent.SimulationTimeSeconds,
                    acousticEvent.PhaseOffset01,
                    acousticEvent.Volume);
                return;
            }

            _audioService?.PlayAtPoint(
                acousticEvent.Clip,
                acousticEvent.RuntimePosition,
                acousticEvent.Volume,
                acousticEvent.Pitch);
        }

        private void QueueSporeAcousticEvent(in SporeAcousticEvent acousticEvent)
        {
            if (acousticEvent.Clip == null ||
                _pendingSporeAcousticEventCount >= _pendingSporeAcousticEvents.Length)
            {
                return;
            }

            _pendingSporeAcousticEvents[_pendingSporeAcousticEventCount] = acousticEvent;
            _pendingSporeAcousticEventCount++;
        }

        private void QueueHarvestAudioEvent(in HarvestAudioEvent audioEvent)
        {
            if (audioEvent.Clip == null ||
                _pendingHarvestAudioEventCount >= _pendingHarvestAudioEvents.Length)
            {
                return;
            }

            _pendingHarvestAudioEvents[_pendingHarvestAudioEventCount] = audioEvent;
            _pendingHarvestAudioEventCount++;
        }

        private void FlushPendingHarvestAudioEvents()
        {
            int count = _pendingHarvestAudioEventCount;
            if (count <= 0)
                return;

            _pendingHarvestAudioEventCount = 0;
            for (int i = 0; i < count; i++)
            {
                HarvestAudioEvent audioEvent = _pendingHarvestAudioEvents[i];
                _pendingHarvestAudioEvents[i] = default;
                ISpatialAudioHarvestPlaybackSink harvestAudioSink = _harvestAudioSink;
                if (harvestAudioSink != null && audioEvent.HasAup)
                {
                    AbsoluteUniversePosition positionAup = audioEvent.PositionAup;
                    harvestAudioSink.PlayHarvestAtAup(in positionAup, audioEvent.Clip, audioEvent.Volume, audioEvent.Pitch);
                    continue;
                }

                _audioService?.PlayAtPoint(audioEvent.Clip, audioEvent.RuntimePosition, audioEvent.Volume, audioEvent.Pitch);
            }
        }

        private void FlushPendingSporeAcousticEvents()
        {
            int count = _pendingSporeAcousticEventCount;
            if (count <= 0)
                return;

            _pendingSporeAcousticEventCount = 0;
            for (int i = 0; i < count; i++)
            {
                SporeAcousticEvent acousticEvent = _pendingSporeAcousticEvents[i];
                _pendingSporeAcousticEvents[i] = default;
                DispatchSporeAcousticEvent(in acousticEvent);
            }
        }

        private static float ResolveSporeShaderPhaseOffset01(Vector3 instancePosition)
        {
            return math.frac((instancePosition.x * SporeShaderPhasePositionX + instancePosition.z * SporeShaderPhasePositionZ) * InvTwoPi);
        }

        private static float ResolveNextSporePulseTime(float simulationTimeSeconds, float pulseFrequencyHz, float phaseOffset01)
        {
            float safePulseFrequency = math.max(MinimumSporePulseFrequencyHz, pulseFrequencyHz);
            float currentCycle = simulationTimeSeconds * safePulseFrequency + phaseOffset01 - SporePulsePeakPhase01;
            float nextCycle = math.floor(currentCycle) + 1f;
            return (nextCycle + SporePulsePeakPhase01 - phaseOffset01) / safePulseFrequency;
        }

        private static bool IsMatureGrowth(HectonVegetationInstanceData metadata)
        {
            return metadata.Reserved0 <= 0.0001f || metadata.Reserved0 >= MatureSporeGrowthThreshold01;
        }

        private bool IsMatureSporeAcousticEmitter(int templateIndex)
        {
            return _sporeAcousticEmitterByDescriptorIndex != null &&
                   templateIndex >= 0 &&
                   templateIndex < _sporeAcousticEmitterByDescriptorIndex.Length &&
                   _sporeAcousticEmitterByDescriptorIndex[templateIndex] != 0;
        }

        private AudioClip ResolveMatureSporeAcousticClip(int templateIndex)
        {
            if (_sporeAcousticClipByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticClipByDescriptorIndex.Length &&
                _sporeAcousticClipByDescriptorIndex[templateIndex] != null)
            {
                return _sporeAcousticClipByDescriptorIndex[templateIndex];
            }

            return sporeAcousticFallbackClip != null
                ? sporeAcousticFallbackClip
                : ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), HarvestState.PartiallyHarvested);
        }

        private float ResolveMatureSporePulseFrequency(int templateIndex)
        {
            if (_sporePulseFrequencyByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporePulseFrequencyByDescriptorIndex.Length)
            {
                return Mathf.Max(MinimumSporePulseFrequencyHz, _sporePulseFrequencyByDescriptorIndex[templateIndex]);
            }

            return 1f;
        }

        private float ResolveMatureSporeAcousticVolume(int templateIndex)
        {
            if (_sporeAcousticVolumeByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticVolumeByDescriptorIndex.Length &&
                _sporeAcousticVolumeByDescriptorIndex[templateIndex] > 0.0001f)
            {
                return Mathf.Clamp01(_sporeAcousticVolumeByDescriptorIndex[templateIndex]);
            }

            return sporeAcousticFallbackVolume;
        }

        private static float ResolveMatureSporeAcousticPitch(float pulseFrequency)
        {
            return Mathf.Clamp(pulseFrequency, 0.1f, 3f);
        }

        private bool TryResolvePersistedFloraState(uint instanceUid, out float normalizedHealth, out float normalizedHeightScale)
        {
            normalizedHealth = 1f;
            normalizedHeightScale = 1f;
            if (!_persistedHealth01ByInstanceUid.IsCreated || !_persistedHeightScale01ByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasHealth = _persistedHealth01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHealth);
            bool hasHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight);
            if (!hasHealth || !hasHeight)
                return false;

            normalizedHealth = Mathf.Clamp01((float)persistedHealth);
            normalizedHeightScale = Mathf.Clamp01((float)persistedHeight);
            return true;
        }

        private float ResolvePersistedNormalizedHeightScale(uint instanceUid)
        {
            if (!_persistedHeightScale01ByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)persistedHeight);
        }

        private float ResolveRuntimeNormalizedHeightScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale));
            }

            return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale) / math.max(0.0001f, baseScale.x));
        }

        private void ClearPersistedFloraStateOverride(uint instanceUid)
        {
            if (_persistedHealth01ByInstanceUid.IsCreated)
                _persistedHealth01ByInstanceUid.Remove(instanceUid);

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
        }

        private void PersistFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth)
        {
            if (instanceUid == 0u || templateIndex < 0 || templateIndex >= _templateDescriptors.Length)
                return;

            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            HarvestState harvestState = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHeightScale);
            if (PersistentWorldRegistry.IsPristineFloraState(normalizedHealth, normalizedHeightScale))
            {
                _persistentWorldRegistry?.TryClearFloraStateOverride(instanceUid);
                ClearPersistedFloraStateOverride(instanceUid);
                return;
            }

            if (_persistedHealth01ByInstanceUid.IsCreated)
            {
                _persistedHealth01ByInstanceUid.Remove(instanceUid);
                _persistedHealth01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHealth);
            }

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
                _persistedHeightScale01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHeightScale);
            }

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            registry.TryRegisterFloraStateOverride(
                (ulong)(uint)_templateDescriptors[templateIndex].StableHashId,
                instanceUid,
                instancePosition,
                normalizedHealth,
                (byte)harvestState);
        }

        private float ResolveCurrentNormalizedHeightScale(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            float fallbackNormalizedHeightScale)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return Mathf.Clamp01(fallbackNormalizedHeightScale);

            return ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[activeIndex]);
        }

        private float GetLaneHealth(bool underwater, int activeIndex)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            return laneHealth.IsCreated && (uint)activeIndex < (uint)laneHealth.Length ? (float)laneHealth[activeIndex] : 0f;
        }

        private void SetLaneHealth(bool underwater, int activeIndex, float value)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            if (!laneHealth.IsCreated || activeIndex < 0 || activeIndex >= laneHealth.Length)
                return;

            laneHealth[activeIndex] = (Unity.Mathematics.half)Mathf.Max(0f, value);
        }

        private void SuppressActiveInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                SuppressActiveInstance(ref _underwaterMatrices, ref _underwaterMetadata, activeIndex);
            else
                SuppressActiveInstance(ref _surfaceMatrices, ref _surfaceMetadata, activeIndex);
        }

        private void ApplyWiltToLaneInstance(bool underwater, int activeIndex, float wiltEndTime)
        {
            float wiltStartTime = wiltEndTime - OrganicWiltDurationSeconds;
            if (underwater)
                ApplyWiltMetadata(ref _underwaterMetadata, activeIndex, wiltStartTime);
            else
                ApplyWiltMetadata(ref _surfaceMetadata, activeIndex, wiltStartTime);
        }

        private void ApplyDamageToLaneInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float damage01,
            float normalizedHeightScale,
            float currentTime)
        {
            if (underwater)
                ApplyPersistedDamageMetadata(ref _underwaterMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            else
                ApplyPersistedDamageMetadata(ref _surfaceMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
        }

        private void UpdateRegrowthVisuals()
        {
            if (!_regrowthProgressByInstanceUid.IsCreated || _regrowthProgressByInstanceUid.Count <= 0)
                return;

            UpdateRegrowthLane(false);
            UpdateRegrowthLane(true);
        }

        private void UpdateRegrowthLane(bool underwater)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_regrowthProgressByInstanceUid.TryGetValue(instanceUid, out float progress01))
                    continue;

                ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, progress01);
            }
        }

        private void UpdateMatureSporeAcoustics(float currentTime)
        {
            if (!_nextSporeAcousticTimeByInstanceUid.IsCreated ||
                _sporeAcousticEmitterByDescriptorIndex == null ||
                _sporeAcousticEmitterByDescriptorIndex.Length == 0)
            {
                return;
            }

            UpdateMatureSporeAcousticLane(false, currentTime, ref _surfaceMatureSporeScanCursor);
            UpdateMatureSporeAcousticLane(true, currentTime, ref _underwaterMatureSporeScanCursor);
        }

        private void UpdateMatureSporeAcousticLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(metadata.Length, materialClasses.Length)));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = math.min(Mathf.Max(1, matureSporeAcousticScanBudgetPerTick), safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
                {
                    continue;
                }

                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                if (!IsMatureGrowth(instanceData) ||
                    instanceData.HealthNormalized < MatureSporeGrowthThreshold01 ||
                    instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(instanceData.HeightScale) <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = ResolveTemplateIndex(instanceData, (HarvestableTemplate.MaterialClass)materialClasses[activeIndex]);
                if (!IsMatureSporeAcousticEmitter(templateIndex))
                    continue;

                TryDispatchMatureSporeAcoustic(instanceUid, 1f, underwater, activeIndex, templateIndex, currentTime);
            }
        }

        private void UpdateDecompositionVisuals(float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || _decompositionStartTimeByInstanceUid.Count <= 0)
                return;

            UpdateDecompositionLane(false, currentTime);
            UpdateDecompositionLane(true, currentTime);
        }

        private void UpdateDecompositionLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    !_destroyedByInstanceUid.IsCreated ||
                    !_destroyedByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
            }
        }

        private void UpdateDamageVisuals(float currentTime)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || _damageVisualProgressByInstanceUid.Count <= 0)
                return;

            UpdateDamageLane(false, currentTime);
            UpdateDamageLane(true, currentTime);
        }

        private void UpdateDamageLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_pendingWiltEndTimeByInstanceUid.IsCreated && _pendingWiltEndTimeByInstanceUid.ContainsKey(instanceUid)) ||
                    !_damageVisualProgressByInstanceUid.TryGetValue(instanceUid, out float damage01))
                {
                    continue;
                }

                float normalizedHeightScale = ResolvePersistedNormalizedHeightScale(instanceUid);
                if (normalizedHeightScale <= 0.0001f)
                    normalizedHeightScale = ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[i]);

                int templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                    : 1f;
                float normalizedHealth = _healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half trackedHealth)
                    ? Mathf.Clamp01((float)trackedHealth / baseHealth)
                    : Mathf.Clamp01(1f - (damage01 * 0.5f));
                ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            }
        }

        private void UpdateWiltInstances(float currentTime)
        {
            if (!_pendingWiltEndTimeByInstanceUid.IsCreated || _pendingWiltEndTimeByInstanceUid.Count <= 0)
                return;

            UpdateWiltLane(false, currentTime);
            UpdateWiltLane(true, currentTime);
        }

        private void UpdateWiltLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_pendingWiltEndTimeByInstanceUid.TryGetValue(instanceUid, out float wiltEndTime))
                    continue;

                if (currentTime >= wiltEndTime)
                {
                    SuppressActiveInstance(underwater, i);
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                    continue;
                }

                ApplyWiltMetadata(ref metadata, i, wiltEndTime - OrganicWiltDurationSeconds);
            }
        }

        private static void SuppressActiveInstance(
            ref NativeArray<Matrix4x4> matrices,
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex)
        {
            if (!matrices.IsCreated || !metadata.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length || activeIndex >= metadata.Length)
                return;

            Matrix4x4 hiddenMatrix = matrices[activeIndex];
            hiddenMatrix.m03 = 0f;
            hiddenMatrix.m13 = HiddenInstanceWorldY;
            hiddenMatrix.m23 = 0f;
            matrices[activeIndex] = hiddenMatrix;

            HectonVegetationInstanceData hiddenMetadata = metadata[activeIndex];
            hiddenMetadata.Type = 0f;
            hiddenMetadata.HeightScale = 0f;
            hiddenMetadata.WidthScale = 0f;
            hiddenMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateIdle;
            hiddenMetadata.RuntimeFlags = 0f;
            hiddenMetadata.HealthNormalized = 0f;
            hiddenMetadata.Reserved0 = -1f;
            metadata[activeIndex] = hiddenMetadata;
        }

        private void ApplyDearLieMatrixScaleZeroToLaneInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                ApplyDearLieMatrixScaleZero(ref _underwaterMatrices, activeIndex);
            else
                ApplyDearLieMatrixScaleZero(ref _surfaceMatrices, activeIndex);
        }

        private static void ApplyDearLieMatrixScaleZero(ref NativeArray<Matrix4x4> matrices, int activeIndex)
        {
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return;

            Matrix4x4 matrix = matrices[activeIndex];
            ScaleMatrixColumnsToZero(ref matrix);
            matrices[activeIndex] = matrix;
        }

        private static void ScaleMatrixColumnsToZero(ref Matrix4x4 matrix)
        {
            matrix.m00 = 0f;
            matrix.m01 = 0f;
            matrix.m02 = 0f;
            matrix.m10 = 0f;
            matrix.m11 = 0f;
            matrix.m12 = 0f;
            matrix.m20 = 0f;
            matrix.m21 = 0f;
            matrix.m22 = 0f;
        }

        private static void ApplyWiltMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float wiltStartTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData wiltMetadata = metadata[activeIndex];
            wiltMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(wiltMetadata.HeightScale));
            wiltMetadata.WidthScale = Mathf.Max(0.001f, wiltStartTime);
            wiltMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            wiltMetadata.HealthNormalized = 0f;
            metadata[activeIndex] = wiltMetadata;
        }

        private void ApplyDecompositionMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            float entropy01)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            float smoothEntropy = entropy01 * entropy01 * (3f - (2f * entropy01));
            float decompositionStartTime = 0f;
            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out decompositionStartTime);

            HectonVegetationInstanceData decompositionMetadata = metadata[activeIndex];
            decompositionMetadata.HeightScale = -math.lerp(baseScale.x, MinimumDecomposedHeightScale, smoothEntropy);
            decompositionMetadata.WidthScale = -Mathf.Max(0.001f, decompositionStartTime);
            decompositionMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            decompositionMetadata.HealthNormalized = math.lerp(1f, 0f, smoothEntropy);
            decompositionMetadata.Reserved0 = -1f;
            metadata[activeIndex] = decompositionMetadata;
        }

        private static void ApplyDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length || damage01 <= 0.0001f)
                return;

            HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
            damageMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(damageMetadata.HeightScale));
            damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
            damageMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateAgitated;
            damageMetadata.HealthNormalized = Mathf.Clamp01(1f - damage01);
            metadata[activeIndex] = damageMetadata;
        }

        private void ApplyPersistedDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float normalizedHeightScale,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HarvestState harvestState = ResolveHarvestState(
                templateIndex,
                1f,
                Mathf.Clamp01(normalizedHeightScale),
                Mathf.Clamp01(normalizedHeightScale));
            if (_baseScaleByInstanceUid.IsCreated &&
                _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                float clampedHeight01 = math.saturate(normalizedHeightScale);
                harvestState = ResolveHarvestState(templateIndex, baseScale.x, baseScale.x * clampedHeight01, clampedHeight01);
                HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
                damageMetadata.HeightScale = -Mathf.Max(MinimumDecomposedHeightScale, baseScale.x * clampedHeight01);
                damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
                damageMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
                damageMetadata.HealthNormalized = math.saturate(normalizedHealth);
                metadata[activeIndex] = damageMetadata;
                return;
            }

            ApplyDamageMetadata(ref metadata, activeIndex, damage01, currentTime);
            HectonVegetationInstanceData fallbackMetadata = metadata[activeIndex];
            fallbackMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
            fallbackMetadata.HealthNormalized = math.saturate(normalizedHealth);
            metadata[activeIndex] = fallbackMetadata;
        }

        private void EvaluateParasiteExposureInLane(Vector3 runtimePosition, bool underwater, ref float bestExposure)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !laneHealth.IsCreated || !matrices.IsCreated || count <= 0 || !_runtimeFlagsByInstanceUid.IsCreated)
                return;

            const float parasiteRadius = 3.25f;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (float)laneHealth[i] <= 0.0001f ||
                    !_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags) ||
                    (runtimeFlags & FloraRuntimeFlagHasParasite) == 0)
                {
                    continue;
                }

                Vector3 delta = ExtractTranslation(matrices[i]) - runtimePosition;
                float distanceSq = delta.sqrMagnitude;
                float radiusSq = parasiteRadius * parasiteRadius;
                if (distanceSq >= radiusSq)
                    continue;

                float exposure = 1f - math.saturate(distanceSq / radiusSq);
                if (exposure > bestExposure)
                    bestExposure = exposure;
            }
        }

        private bool TryApplyTitanRootMound(bool underwater, int activeIndex, uint instanceUid)
        {
            if (instanceUid == 0u ||
                !_rootMoundAppliedByInstanceUid.IsCreated ||
                _rootMoundAppliedByInstanceUid.ContainsKey(instanceUid))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return false;
            }

            if ((HectonVegetationInstanceType)types[activeIndex] != HectonVegetationInstanceType.GiantKelp)
                return false;

            HectonVegetationInstanceData instanceData = metadata[activeIndex];
            if (math.saturate(instanceData.Reserved0) < TitanRootMoundMatureThreshold01 &&
                math.saturate(instanceData.HealthNormalized) < TitanRootMoundMatureThreshold01 &&
                math.saturate(math.abs(instanceData.HeightScale)) < TitanRootMoundMatureThreshold01)
            {
                return false;
            }

            HectonVoxelEngine voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;
            if (voxelEngine == null)
                return false;

            double3 universePosition = ToDouble3(ExtractTranslation(matrices[activeIndex]));
            Vector3 runtimePosition = HectonMapMagicVegetationBridge.ToRuntimeSpace(universePosition);
            if (!voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) || volume == null)
                return false;

            volume.ApplyOrganicRootMound(runtimePosition, TitanRootMoundRadiusMeters, TitanRootMoundStrengthMeters);
            _rootMoundAppliedByInstanceUid.TryAdd(instanceUid, 1);
            return true;
        }

        internal bool IsMaterialClassRegrowable(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)_templateDescriptors[i].MaterialClassId;
                return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                       materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            }

            return false;
        }

        internal float ResolveGrowthTimeSeconds(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                if (_growthTimeSecondsByDescriptorIndex != null && i < _growthTimeSecondsByDescriptorIndex.Length)
                    return Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[i]);

                return 480f;
            }

            return 480f;
        }

        internal bool TryResolveFloraGrowthDescriptor(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            out uint instanceUid,
            out ulong floraPersistentIdHash,
            out float growthTimeSeconds)
        {
            instanceUid = ComputeStableInstanceUid(matrix, metadata, typeId, semanticType);
            floraPersistentIdHash = 0UL;
            growthTimeSeconds = 0f;

            HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(typeId, semanticType);
            int templateIndex = ResolveTemplateIndex(metadata, fallbackMaterialClass);
            if (templateIndex < 0 || templateIndex >= _templateDescriptors.Length)
                return false;

            floraPersistentIdHash = (ulong)(uint)_templateDescriptors[templateIndex].StableHashId;
            growthTimeSeconds = _growthTimeSecondsByDescriptorIndex != null && templateIndex < _growthTimeSecondsByDescriptorIndex.Length
                ? Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[templateIndex])
                : 480f;
            return floraPersistentIdHash != 0UL;
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01)
        {
            float multiplier = EvaluateMaturationScaleMultiplier(progress01);
            return TrySetMaturationProgress(instanceUid, progress01, multiplier, Mathf.Clamp01(progress01));
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01, float scaleMultiplier, float resourceYieldMultiplier)
        {
            if (_dearLieJobScheduled || instanceUid == 0u || !_maturationScaleByInstanceUid.IsCreated)
                return false;

            float clampedProgress = Mathf.Clamp01(progress01);
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
            resourceYieldMultiplier = clampedProgress < 0.2f ? 0f : Mathf.Clamp01(resourceYieldMultiplier);
            _maturationScaleByInstanceUid.Remove(instanceUid);
            if (scaleMultiplier < 0.9999f)
                _maturationScaleByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)scaleMultiplier);

            if (_maturationYieldByInstanceUid.IsCreated)
            {
                _maturationYieldByInstanceUid.Remove(instanceUid);
                if (resourceYieldMultiplier < 0.9999f)
                    _maturationYieldByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resourceYieldMultiplier);
            }

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                ApplyMaturationVisualToLaneInstance(underwater, activeIndex, instanceUid, clampedProgress, scaleMultiplier);
                TryDispatchMatureSporeAcoustic(instanceUid, clampedProgress, underwater, activeIndex, templateIndex, ResolveOrganicClockSeconds());
                if (clampedProgress >= TitanRootMoundMatureThreshold01)
                    TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }
            else if (clampedProgress < MatureSporeGrowthThreshold01 && _nextSporeAcousticTimeByInstanceUid.IsCreated)
            {
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
            }

            return true;
        }

        bool IOrganicToolHitService.TryApplyOrganicToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            return TryApplyToolHit(hitPoint, hitNormal, direction, deliveredDamage, normalizedPower, toolCapabilityMask);
        }

        bool IOrganicToolHitService.TryApplyAttachedFloraToolHit(
            Vector3 hitPoint,
            float searchRadius,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (!TryResolveNearestConsumableFlora(
                    hitPoint,
                    Mathf.Max(0.0001f, searchRadius),
                    out Vector3 floraPosition,
                    out _))
            {
                return false;
            }

            FloraInteractionManager interactionManager = floraInteractionManager;
            if (interactionManager != null)
            {
                interactionManager.TryApplyModuleParasiteCut(
                    floraPosition,
                    hitNormal,
                    direction,
                    deliveredDamage,
                    normalizedPower,
                    toolCapabilityMask);
            }

            return true;
        }

        internal bool TryApplyLightStarvation(uint instanceUid, float starvation01)
        {
            if (_dearLieJobScheduled ||
                instanceUid == 0u ||
                !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            float clampedStarvation01 = Mathf.Clamp01(starvation01);
            float baseHealth = ResolveBaseHealth(templateIndex);
            float currentHealth = GetLaneHealth(underwater, activeIndex);
            if (currentHealth <= 0.0001f)
                return false;

            float nextHealth = Mathf.Max(0f, currentHealth - (baseHealth * LightStarvationDamagePerSlowTick01 * clampedStarvation01));
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            if (nextHealth <= baseHealth * LightStarvationDeathHealth01)
            {
                ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
                return true;
            }

            float normalizedHealth = Mathf.Clamp01(nextHealth / Mathf.Max(0.0001f, baseHealth));
            float normalizedHeightScale = Mathf.Clamp(
                Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                SoftBareHealthFloor01,
                1f);
            ApplySuppressionState(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                instancePosition,
                baseHealth,
                nextHealth,
                normalizedHealth,
                normalizedHeightScale);
            return true;
        }

        internal bool TryApplyAllelopathicToxinSuppression(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            float toxicity01)
        {
            if (_dearLieJobScheduled ||
                !TryResolveFloraGrowthDescriptor(
                    matrix,
                    metadata,
                    typeId,
                    semanticType,
                    out uint instanceUid,
                    out _,
                    out _) ||
                instanceUid == 0u ||
                !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            float clampedToxicity01 = Mathf.Clamp01(toxicity01);
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            if (clampedToxicity01 >= AllelopathicDeathThreshold01)
            {
                ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
                return true;
            }

            float baseHealth = ResolveBaseHealth(templateIndex);
            float normalizedHealth = math.lerp(ResolveBareThreshold01(templateIndex), AllelopathicBareHealth01, clampedToxicity01);
            float normalizedHeightScale = Mathf.Clamp(
                Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                SoftBareHealthFloor01,
                1f);
            float nextHealth = baseHealth * normalizedHealth;
            ApplySuppressionState(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                instancePosition,
                baseHealth,
                nextHealth,
                normalizedHealth,
                normalizedHeightScale);
            return true;
        }

        internal static float EvaluateMaturationScaleMultiplier(float progress01)
        {
            float clampedProgress = math.saturate(progress01);
            float smoothProgress = clampedProgress * clampedProgress * (3f - (2f * clampedProgress));
            return math.lerp(0.1f, 1f, smoothProgress);
        }

        private float ResolveBaseHealth(int templateIndex)
        {
            return templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
        }

        private void ApplySuppressionState(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            float baseHealth,
            float currentHealth,
            float normalizedHealth,
            float normalizedHeightScale)
        {
            SetLaneHealth(underwater, activeIndex, currentHealth);
            if (_healthByInstanceUid.IsCreated)
            {
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)Mathf.Max(0f, currentHealth));
            }

            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            ApplyDamageToLaneInstance(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                Mathf.Clamp01(normalizedHealth),
                damage01,
                Mathf.Clamp01(normalizedHeightScale),
                ResolveOrganicClockSeconds());
            PersistFloraStateOverride(
                instanceUid,
                templateIndex,
                instancePosition,
                underwater,
                activeIndex,
                baseHealth,
                currentHealth);
        }

        private int ResolveDescriptorIndexByPersistentIdHash(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId == floraPersistentIdHash)
                    return i;
            }

            return -1;
        }

        internal bool HasTemplatePersistentIdHash(ulong floraPersistentIdHash)
        {
            return ResolveDescriptorIndexByPersistentIdHash(floraPersistentIdHash) >= 0;
        }

        private static HarvestState ResolvePersistedHarvestState(byte packedHarvestState)
        {
            if (packedHarvestState > (byte)HarvestState.Dead)
                return HarvestState.PartiallyHarvested;

            return (HarvestState)packedHarvestState;
        }

        internal bool IsTemplateMaterialClass(ulong floraPersistentIdHash, HarvestableTemplate.MaterialClass materialClass)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                return _templateDescriptors[i].MaterialClassId == (byte)materialClass;
            }

            return false;
        }

        internal bool TrySetRegrowthProgress(uint instanceUid, Vector3 runtimePosition, float progress01)
        {
            if (_dearLieJobScheduled ||
                instanceUid == 0u ||
                !_regrowthProgressByInstanceUid.IsCreated ||
                !_regrowthPositionByInstanceUid.IsCreated)
            {
                return false;
            }

            if (ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZone(runtimePosition))
                return false;

            progress01 = math.saturate(progress01);
            if (_destroyedByInstanceUid.IsCreated)
                _destroyedByInstanceUid.Remove(instanceUid);
            ClearDeadRuntimeFlag(instanceUid);
            MarkOrganicTouched(instanceUid, ResolveOrganicClockSeconds());

            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);

            _regrowthProgressByInstanceUid.Remove(instanceUid);
            _regrowthProgressByInstanceUid.TryAdd(instanceUid, progress01);
            _regrowthPositionByInstanceUid.Remove(instanceUid);
            _regrowthPositionByInstanceUid.TryAdd(instanceUid, new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z));

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                byte runtimeFlags = 0;
                if (_runtimeFlagsByInstanceUid.IsCreated)
                    _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);

                ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, progress01);
                float health = ResolveRegrowthHealth(progress01, templateIndex);
                SetLaneHealth(underwater, activeIndex, health);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)health);
                if (progress01 >= TitanRootMoundMatureThreshold01)
                    TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }

            if (progress01 >= 0.9999f)
                FinalizeRegrowth(instanceUid);

            return true;
        }

        private void FinalizeRegrowth(uint instanceUid)
        {
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);

            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            MarkOrganicTouched(instanceUid, ResolveOrganicClockSeconds());

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                    : 1f;
                SetLaneHealth(underwater, activeIndex, baseHealth);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)baseHealth);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, 1f);
                return;
            }

            _healthByInstanceUid.Remove(instanceUid);
        }

        private float ResolveRegrowthHealth(float progress01, int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            return Mathf.Max(0.05f, math.lerp(baseHealth * 0.1f, baseHealth, smoothProgress));
        }

        private float ResolveMaturationScaleMultiplier(uint instanceUid)
        {
            if (!_maturationScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationScaleByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedScale))
            {
                return 1f;
            }

            return Mathf.Clamp((float)storedScale, 0.1f, 1f);
        }

        private float ResolveMaturationYieldMultiplier(uint instanceUid)
        {
            if (!_maturationYieldByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationYieldByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedYield))
            {
                return ResolveMaturationScaleMultiplier(instanceUid);
            }

            return Mathf.Clamp01((float)storedYield);
        }

        private void ApplyMaturationVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01, float scaleMultiplier)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            float clampedScale = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            HectonVegetationInstanceData maturationMetadata = metadata[activeIndex];
            maturationMetadata.Type = types[activeIndex];
            maturationMetadata.HeightScale = baseScale.x * clampedScale;
            maturationMetadata.WidthScale = baseScale.y * clampedScale;
            maturationMetadata.Reserved0 = EncodeAuthoredGrowthAge01(progress01);
            metadata[activeIndex] = maturationMetadata;
        }

        private void ApplyRegrowthVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            if (_regrowthPositionByInstanceUid.IsCreated &&
                _regrowthPositionByInstanceUid.TryGetValue(instanceUid, out float3 regrowthPosition))
            {
                Matrix4x4 visibleMatrix = matrices[activeIndex];
                visibleMatrix.m03 = regrowthPosition.x;
                visibleMatrix.m13 = regrowthPosition.y;
                visibleMatrix.m23 = regrowthPosition.z;
                matrices[activeIndex] = visibleMatrix;
            }

            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            HectonVegetationInstanceData regrowthMetadata = metadata[activeIndex];
            regrowthMetadata.Type = types[activeIndex];
            regrowthMetadata.HeightScale = math.lerp(MinimumDecomposedHeightScale, baseScale.x, smoothProgress);
            regrowthMetadata.WidthScale = math.lerp(MinimumDecomposedWidthScale, baseScale.y, smoothProgress);
            regrowthMetadata.RuntimeState = progress01 >= 0.995f
                ? HectonVegetationInstanceData.RuntimeStateIdle
                : HectonVegetationInstanceData.RuntimeStateAgitated;
            regrowthMetadata.HealthNormalized = Mathf.Clamp01(progress01);
            regrowthMetadata.Reserved0 = EncodeAuthoredGrowthAge01(progress01);
            metadata[activeIndex] = regrowthMetadata;
        }

        private bool TryResolveActiveInstanceByUid(uint instanceUid, out bool underwater, out int activeIndex, out int templateIndex)
        {
            if (TryResolveActiveInstanceByUid(instanceUid, _surfaceInstanceUids, _surfaceCount, _surfaceMaterialClasses, _surfaceMetadata, out activeIndex, out templateIndex))
            {
                underwater = false;
                return true;
            }

            if (TryResolveActiveInstanceByUid(instanceUid, _underwaterInstanceUids, _underwaterCount, _underwaterMaterialClasses, _underwaterMetadata, out activeIndex, out templateIndex))
            {
                underwater = true;
                return true;
            }

            underwater = false;
            activeIndex = -1;
            templateIndex = -1;
            return false;
        }

        private bool TryResolveActiveInstanceByUid(
            uint instanceUid,
            NativeArray<uint> instanceUids,
            int count,
            NativeArray<byte> materialClasses,
            NativeArray<HectonVegetationInstanceData> metadata,
            out int activeIndex,
            out int templateIndex)
        {
            activeIndex = -1;
            templateIndex = -1;
            if (!instanceUids.IsCreated || !materialClasses.IsCreated || !metadata.IsCreated || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (instanceUids[i] != instanceUid)
                    continue;

                activeIndex = i;
                templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                return true;
            }

            return false;
        }

        private float ResolveParentMassKg(
            bool underwater,
            int activeIndex,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float height01 = 1f;
            float width01 = 1f;
            float metadataAge01 = 1f;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (metadata.IsCreated && activeIndex >= 0 && activeIndex < metadata.Length)
            {
                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                height01 = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
                width01 = Mathf.Clamp01(instanceData.WidthScale);
                metadataAge01 = ResolveHarvestAge01(instanceData);
            }

            uint instanceUid = _underwaterInstanceUids.IsCreated || _surfaceInstanceUids.IsCreated
                ? (underwater && activeIndex >= 0 && activeIndex < _underwaterCount ? _underwaterInstanceUids[activeIndex] :
                   !underwater && activeIndex >= 0 && activeIndex < _surfaceCount ? _surfaceInstanceUids[activeIndex] :
                   0u)
                : 0u;
            float maturationMultiplier = ResolveMaturationYieldMultiplier(instanceUid);

            float resolvedMassKg = materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => Mathf.Max(1f, baseHealth * math.lerp(0.28f, 0.52f, height01) * math.lerp(0.9f, 1.15f, width01)),
                HarvestableTemplate.MaterialClass.Coral => Mathf.Max(2f, baseHealth * math.lerp(0.55f, 0.8f, height01)),
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => Mathf.Max(4f, baseHealth * math.lerp(0.82f, 1.08f, height01)),
                HarvestableTemplate.MaterialClass.Sargassum => Mathf.Max(0.75f, baseHealth * math.lerp(0.22f, 0.38f, height01) * math.lerp(0.85f, 1.1f, width01)),
                _ => Mathf.Max(1f, baseHealth * 0.4f)
            };

            float harvestAge01 = metadataAge01 < 0.999f ? metadataAge01 : maturationMultiplier;
            if (harvestAge01 < 0.2f)
                return 0f;

            return resolvedMassKg * harvestAge01;
        }

        private static float ResolveHarvestAge01(in HectonVegetationInstanceData instanceData)
        {
            if (instanceData.Reserved0 < 0f)
                return -1f;

            if (instanceData.Reserved0 > 0.0001f)
                return Mathf.Clamp01(instanceData.Reserved0);

            return 1f;
        }

        private static float EncodeAuthoredGrowthAge01(float progress01)
        {
            float clampedProgress = Mathf.Clamp01(progress01);
            return clampedProgress <= 0.0001f ? 0.0002f : clampedProgress;
        }

        private static HarvestableTemplate.MaterialClass ResolveMaterialClass(int typeId, int semanticType)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            HectonMapMagicVegetationBridge.VegetationSemanticType semantic = (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp)
                return HarvestableTemplate.MaterialClass.Kelp;

            if (vegetationType == HectonVegetationInstanceType.Sargassum || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum)
                return HarvestableTemplate.MaterialClass.Sargassum;

            return HarvestableTemplate.MaterialClass.None;
        }

        private static bool IsConsumableFloraMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                   materialClass == HarvestableTemplate.MaterialClass.Sargassum;
        }

        private static double ResolveConstructionDistanceSq(
            double3 centerUniversePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            double3 rootPositionDouble = ToDouble3(rootPosition);
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                double kelpHeight = math.lerp(10d, 20d, (double)math.saturate(metadata.HeightScale));
                double3 top = rootPositionDouble + new double3(0d, math.max(0.5d, kelpHeight + KelpRadiusBias), 0d);
                double3 closest = ClosestPointOnSegment(rootPositionDouble, top, centerUniversePosition);
                return math.lengthsq(closest - centerUniversePosition);
            }

            return math.lengthsq(rootPositionDouble - centerUniversePosition);
        }

        private static float ResolveHarvestDistanceSq(
            Vector3 hitPoint,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId,
            float fallbackDistanceSq,
            float heightTolerance)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, hitPoint);
                return (closest - hitPoint).sqrMagnitude;
            }

            return Mathf.Min((rootPosition - hitPoint).sqrMagnitude, fallbackDistanceSq + heightTolerance);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSq);
            return start + segment * t;
        }

        private static double3 ClosestPointOnSegment(double3 start, double3 end, double3 point)
        {
            double3 segment = end - start;
            double segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.0001d)
                return start;

            double t = math.clamp(math.dot(point - start, segment) * math.rcp(segmentLengthSq), 0d, 1d);
            return start + segment * t;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteMatrix(in Matrix4x4 matrix)
        {
            return math.isfinite(matrix.m00) &&
                   math.isfinite(matrix.m01) &&
                   math.isfinite(matrix.m02) &&
                   math.isfinite(matrix.m03) &&
                   math.isfinite(matrix.m10) &&
                   math.isfinite(matrix.m11) &&
                   math.isfinite(matrix.m12) &&
                   math.isfinite(matrix.m13) &&
                   math.isfinite(matrix.m20) &&
                   math.isfinite(matrix.m21) &&
                   math.isfinite(matrix.m22) &&
                   math.isfinite(matrix.m23) &&
                   math.isfinite(matrix.m30) &&
                   math.isfinite(matrix.m31) &&
                   math.isfinite(matrix.m32) &&
                   math.isfinite(matrix.m33);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static float ResolveFractionalVariation(float encodedVariation)
        {
            return Mathf.Repeat(encodedVariation, 1f);
        }

        internal static uint ComputeStableInstanceUid(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType)
        {
            int x = Mathf.RoundToInt(matrix.m03 * 100f);
            int y = Mathf.RoundToInt(matrix.m13 * 100f);
            int z = Mathf.RoundToInt(matrix.m23 * 100f);
            uint hx = (uint)x * 73856093u;
            uint hy = (uint)y * 19349663u;
            uint hz = (uint)z * 83492791u;
            uint hv = (uint)Mathf.RoundToInt(ResolveFractionalVariation(metadata.Variation) * 10000f) * 2654435761u;
            uint hs = (uint)(semanticType + 1) * 2246822519u;
            uint ht = (uint)(typeId + 1) * 3266489917u;
            uint mixed = hx ^ hy ^ hz ^ hv ^ hs ^ ht;
            return mixed == 0u ? 1u : mixed;
        }

        private static NativeArray<T> EnsureLaneCapacity<T>(ref NativeArray<T> array, int requiredCount, string label) where T : unmanaged
        {
            EnsureNativeCapacity(ref array, requiredCount, label);
            return array;
        }

        private static void EnsureNativeCapacity<T>(ref NativeArray<T> array, int requiredCount, string label) where T : unmanaged
        {
            if (requiredCount <= 0)
                return;

            if (array.IsCreated && array.Length >= requiredCount)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<T>(requiredCount, DataVaultExemptOrganicScratchAllocator, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<T>[requiredCount] - resized persistent entropy runtime lane - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeHashMap(_healthByInstanceUid, NativeMemoryOwner, nameof(_healthByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_destroyedByInstanceUid, NativeMemoryOwner, nameof(_destroyedByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_pendingWiltEndTimeByInstanceUid, NativeMemoryOwner, nameof(_pendingWiltEndTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_damageVisualProgressByInstanceUid, NativeMemoryOwner, nameof(_damageVisualProgressByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_decompositionStartTimeByInstanceUid, NativeMemoryOwner, nameof(_decompositionStartTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_regrowthProgressByInstanceUid, NativeMemoryOwner, nameof(_regrowthProgressByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_regrowthPositionByInstanceUid, NativeMemoryOwner, nameof(_regrowthPositionByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_maturationScaleByInstanceUid, NativeMemoryOwner, nameof(_maturationScaleByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_maturationYieldByInstanceUid, NativeMemoryOwner, nameof(_maturationYieldByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_nextSporeAcousticTimeByInstanceUid, NativeMemoryOwner, nameof(_nextSporeAcousticTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_baseScaleByInstanceUid, NativeMemoryOwner, nameof(_baseScaleByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_runtimeFlagsByInstanceUid, NativeMemoryOwner, nameof(_runtimeFlagsByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_lastOrganicTouchTimeByInstanceUid, NativeMemoryOwner, nameof(_lastOrganicTouchTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_overgrownByInstanceUid, NativeMemoryOwner, nameof(_overgrownByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_rootMoundAppliedByInstanceUid, NativeMemoryOwner, nameof(_rootMoundAppliedByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_destroyedFloraScratch, NativeMemoryOwner, nameof(_destroyedFloraScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_floraStateOverrideScratch, NativeMemoryOwner, nameof(_floraStateOverrideScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_persistedHealth01ByInstanceUid, NativeMemoryOwner, nameof(_persistedHealth01ByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_persistedHeightScale01ByInstanceUid, NativeMemoryOwner, nameof(_persistedHeightScale01ByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_pendingYieldEvents, NativeMemoryOwner, nameof(_pendingYieldEvents), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_dropDebugScratch, NativeMemoryOwner, nameof(_dropDebugScratch), NativeMemoryLifetime);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.95f, 0.55f, 0.42f);
            Gizmos.DrawWireSphere(dearLieMockDamageCenter, ResolveDearLieQueryRadius());
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireCube(dearLieMockDamageCenter, Vector3.one * DearLieSpatialCellSizeMeters);

            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int sampleCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < sampleCount; i++)
            {
                if ((signals[i].Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                int nanRejectCount = 0;
                if (!TryBuildDearLieEvent(in signals[i], out FloraDestructionEventDTO eventDto, ref nanRejectCount))
                    continue;

                Vector3 impactRuntime = (Vector3)AbsoluteUniversePosition.FromAbsolutePosition(eventDto.ImpactAUP).ToRuntimeFloat3();
                if (!IsFiniteVector(impactRuntime))
                    continue;

                Gizmos.color = new Color(1f, 0.08f, 0.02f, 0.72f);
                Gizmos.DrawWireSphere(impactRuntime, ResolveDearLieQueryRadius());
                break;
            }

            if (_dearLieHasLastDebugHit != 0 &&
                IsFiniteVector(_dearLieLastImpactRuntimePosition) &&
                IsFiniteVector(_dearLieLastTargetRuntimePosition))
            {
                Gizmos.color = new Color(1f, 0.95f, 0.1f, 0.8f);
                Gizmos.DrawLine(_dearLieLastImpactRuntimePosition, _dearLieLastTargetRuntimePosition);
                Gizmos.DrawWireSphere(_dearLieLastTargetRuntimePosition, 0.25f);
            }
        }
#endif

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Dispose();
            list = default;
        }

        private static void DisposeNativeHashMap<TKey, TValue>(ref NativeHashMap<TKey, TValue> map, string label)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(magnitudeSq) : fallback;
        }
    }
}
