using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ScanResultDTO
    {
        [FieldOffset(0)]
        public double3 AUP;
        [FieldOffset(24)]
        public uint EntityHash;
        [FieldOffset(28)]
        public float Distance;
        [FieldOffset(32)]
        public float ScanProgress;
        [FieldOffset(36)]
        public uint _pad0;
        [FieldOffset(40)]
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ScannableEntityMetadataDTO
    {
        [FieldOffset(0)]
        public uint EntityHash;
        [FieldOffset(4)]
        public float ScanDuration;
        [FieldOffset(8)]
        public uint RequiredToolLevel;
        [FieldOffset(12)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScannerSpatialEntityDTO
    {
        [FieldOffset(0)]
        public double3 AUP;
        [FieldOffset(24)]
        public long SectorHash;
        [FieldOffset(32)]
        public ulong DepletionMask;
        [FieldOffset(40)]
        public uint EntityHash;
        [FieldOffset(44)]
        public float SphereRadius;
        [FieldOffset(48)]
        public uint MetadataIndex;
        [FieldOffset(52)]
        public uint Flags;
        [FieldOffset(56)]
        public uint DepletionWordIndex;
        [FieldOffset(60)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerVfxDTO
    {
        [FieldOffset(0)]
        public float3 HitAUP;
        [FieldOffset(12)]
        public float HitDistance;
        [FieldOffset(16)]
        public float ScanProgress;
        [FieldOffset(20)]
        public uint TargetHash;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        public float BeamScore;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ActiveScanStateDTO
    {
        [FieldOffset(0)]
        public double3 TargetAUP;
        [FieldOffset(24)]
        public double3 LastOriginAUP;
        [FieldOffset(48)]
        public long SectorHash;
        [FieldOffset(56)]
        public ulong DepletionMask;
        [FieldOffset(64)]
        public ulong _pad0;
        [FieldOffset(72)]
        public ulong _pad1;
        [FieldOffset(80)]
        public uint TargetHash;
        [FieldOffset(84)]
        public float Progress01;
        [FieldOffset(88)]
        public float ScanDurationSeconds;
        [FieldOffset(92)]
        public float HoldSeconds;
        [FieldOffset(96)]
        public uint LastFrame;
        [FieldOffset(100)]
        public uint Flags;
        [FieldOffset(104)]
        public uint CompletedHash;
        [FieldOffset(108)]
        public int BestEntityIndex;
        [FieldOffset(112)]
        public uint DepletionWordIndex;
        [FieldOffset(116)]
        public uint MetadataFlags;
        [FieldOffset(120)]
        public float HitDistance;
        [FieldOffset(124)]
        public float BeamScore;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockScannerInputSignal
    {
        [FieldOffset(0)]
        public double3 RayOriginAUP;
        [FieldOffset(24)]
        public float3 RayDirection;
        [FieldOffset(36)]
        public float MaxDistance;
        [FieldOffset(40)]
        public float DeltaTime;
        [FieldOffset(44)]
        public float BeamRadius;
        [FieldOffset(48)]
        public uint ToolHash;
        [FieldOffset(52)]
        public uint Frame;
        [FieldOffset(56)]
        public uint ToolLevel;
        [FieldOffset(60)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockToolTransformSignal
    {
        [FieldOffset(0)]
        public double3 PositionAUP;
        [FieldOffset(24)]
        public float3 ForwardVector;
        [FieldOffset(36)]
        public float MaxDistance;
        [FieldOffset(40)]
        public uint ToolHash;
        [FieldOffset(44)]
        public uint Frame;
        [FieldOffset(48)]
        public uint Flags;
        [FieldOffset(52)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockSdfOcclusionZoneDTO
    {
        [FieldOffset(0)]
        public double3 CenterAUP;
        [FieldOffset(24)]
        public float Radius;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerQueryStatsDTO
    {
        [FieldOffset(0)]
        public int CandidateCount;
        [FieldOffset(4)]
        public int BestEntityIndex;
        [FieldOffset(8)]
        public float BestScore;
        [FieldOffset(12)]
        public uint BestHash;
        [FieldOffset(16)]
        public uint Flags;
        [FieldOffset(20)]
        public uint EstimatedMicroseconds;
        [FieldOffset(24)]
        public uint CellProbeCount;
        [FieldOffset(28)]
        public uint OccludedCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScannerTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 TargetAUP;
        [FieldOffset(24)]
        public ulong _pad0;
        [FieldOffset(32)]
        public uint Frame;
        [FieldOffset(36)]
        public uint TargetHash;
        [FieldOffset(40)]
        public uint Flags;
        [FieldOffset(44)]
        public uint CandidateCount;
        [FieldOffset(48)]
        public uint CompletedCount;
        [FieldOffset(52)]
        public uint EstimatedMicroseconds;
        [FieldOffset(56)]
        public float Progress01;
        [FieldOffset(60)]
        public float HitDistance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct ScannerSettingsDTO
    {
        [FieldOffset(0)]
        public float CellSizeMeters;
        [FieldOffset(4)]
        public float MaxDistanceMeters;
        [FieldOffset(8)]
        public float BeamRadiusMeters;
        [FieldOffset(12)]
        public float BeamMinDot;
        [FieldOffset(16)]
        public float BeamMagnetism;
        [FieldOffset(20)]
        public float ProgressDecayRate;
        [FieldOffset(24)]
        public float QueryBudgetMicroseconds;
        [FieldOffset(28)]
        public float AcousticIntensity01;
        [FieldOffset(32)]
        public float LowTierProgressMultiplier;
        [FieldOffset(36)]
        public float HighTierVfxBias;
        [FieldOffset(40)]
        public float SdfMidpointClearance;
        [FieldOffset(44)]
        public float ScanDurationFallback;
        [FieldOffset(48)]
        public int LowTierCadenceFrames;
        [FieldOffset(52)]
        public int MidTierCadenceFrames;
        [FieldOffset(56)]
        public int HighTierCadenceFrames;
        [FieldOffset(60)]
        public int UltraTierCadenceFrames;
        [FieldOffset(64)]
        public int MaxCandidateCells;
        [FieldOffset(68)]
        public int MaxCandidatesPerCell;
        [FieldOffset(72)]
        public int MaxResults;
        [FieldOffset(76)]
        public int Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScanProgressDTO
    {
        [FieldOffset(0)] public uint TargetHashID;
        [FieldOffset(4)] public float CurrentProgress01;
        [FieldOffset(8)] public float ScanRate;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public double3 ScannerAUP;
        [FieldOffset(40)] public uint LastFrame;
        [FieldOffset(44)] public uint CompletedHash;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerLoreIndexDTO
    {
        [FieldOffset(0)] public uint TargetHashID;
        [FieldOffset(4)] public uint LoreEntryIndex;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public uint ProbeStride;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ScannerEncyclopediaStateDTO
    {
        [FieldOffset(0)] public ulong Mask0;
        [FieldOffset(8)] public ulong Mask1;
        [FieldOffset(16)] public ulong Mask2;
        [FieldOffset(24)] public ulong Mask3;
        [FieldOffset(32)] public ulong Mask4;
        [FieldOffset(40)] public ulong Mask5;
        [FieldOffset(48)] public ulong Mask6;
        [FieldOffset(56)] public ulong Mask7;
        [FieldOffset(64)] public ulong Mask8;
        [FieldOffset(72)] public ulong Mask9;
        [FieldOffset(80)] public ulong Mask10;
        [FieldOffset(88)] public ulong Mask11;
        [FieldOffset(96)] public ulong Mask12;
        [FieldOffset(104)] public ulong Mask13;
        [FieldOffset(112)] public ulong Mask14;
        [FieldOffset(120)] public ulong Mask15;
    }

    public ref struct MockSpatialHashGrid
    {
        public NativeArray<int> BucketHeads;
        public NativeArray<int> BucketNext;
        public NativeArray<ScannerSpatialEntityDTO> Entities;
        public NativeArray<ScannableEntityMetadataDTO> Metadata;
        public NativeArray<MockSdfOcclusionZoneDTO> OcclusionZones;
        public int EntityCount;
        public int MetadataCount;
        public int OcclusionZoneCount;
        public float CellSizeMeters;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockToolTransformSignalJob : IJob
    {
        [NoAlias]
        public NativeArray<MockToolTransformSignal> ToolSignals;
        public double3 PositionAUP;
        public float3 ForwardVector;
        public float MaxDistance;
        public uint ToolHash;
        public uint Frame;
        public uint Flags;

        public void Execute()
        {
            if (!ToolSignals.IsCreated || ToolSignals.Length == 0)
                return;

            ToolSignals[0] = new MockToolTransformSignal
            {
                PositionAUP = PositionAUP,
                ForwardVector = math.normalizesafe(ForwardVector, new float3(0f, 0f, 1f)),
                MaxDistance = math.max(0.1f, MaxDistance),
                ToolHash = ToolHash,
                Frame = Frame,
                Flags = Flags,
                _pad0 = 0u
            };
        }
    }

    public static class ScannerDataMiningTuning
    {
        public static ScannerSettingsDTO Settings = ScannerDataMiningRouter.CreateDefaultSettings();
    }

    [DisallowMultipleComponent]
    public sealed class ScannerDataMiningRouter : MonoBehaviour, IFastTickable, ISlowTickable, ILateFrameTickable, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        public const uint MetadataToolLevelMask = 0x000000FFu;
        public const uint MetadataFlagDepletable = 1u << 8;
        public const uint MetadataFlagFlora = 1u << 9;
        public const uint MetadataFlagFauna = 1u << 10;
        public const uint MetadataFlagDataNode = 1u << 11;
        public const uint MetadataFlagScarcityNode = 1u << 12;
        public const uint StateFlagHasTarget = 1u << 0;
        public const uint StateFlagCompletedThisFrame = 1u << 1;
        public const uint StateFlagOccluded = 1u << 2;
        public const uint VfxFlagHasTarget = 1u << 0;
        public const uint VfxFlagOccluded = 1u << 1;
        public const uint QueryFlagNoCandidate = 1u << 0;
        public const uint QueryFlagNaNInput = 1u << 1;
        public const uint QueryFlagOccluded = 1u << 2;
        public const uint ScanProgressFlagActive = 1u << 0;
        public const uint ScanProgressFlagCompleted = 1u << 1;
        public const uint ScanProgressFlagLostTarget = 1u << 2;
        public const uint LoreIndexFlagOccupied = 1u << 0;
        public const int BlackBoxCapacity = 300;
        public const int DefaultEntityCapacity = 128;
        public const int DefaultSpatialBucketCapacity = 256;
        public const int DefaultResultCapacity = 4;
        public const float DefaultCellSizeMeters = 16f;
        public const string DumpFileName = "Dump_SHINOBU_226.bin";
        public const string H8DumpFileName = "Dump_SHINOBU_226.h8dump";

        private const SystemID OwnerSystemId = SystemID.GameplayTools;
        private const byte ToolAcousticStateScanner = 2;
        private const uint ScannerToolHash = H8Hashes.Items.HydroacousticScannerHash;
        private const uint ScannerAnomalyHash = 0x53434E41u; // SCNA
        private const uint ScannerDumpReasonHash = 0x53444D50u; // SDMP
        private static readonly ulong ScannerQueryMutationGuardMask =
            ScannerMutationGuardBit(BufferID.ShinobuScannerEntities) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerMetadata) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerOcclusionZones) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerSpatialBucketHeads) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerSpatialNext) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerScanResults) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerResultCount) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerQueryStats);
        private static readonly ulong ScannerCompletionMutationGuardMask =
            ScannerQueryMutationGuardMask |
            ScannerMutationGuardBit(BufferID.ShinobuScannerActiveState) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerVfxTarget) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerTelemetryRing) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerScanProgress) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerLoreIndex) |
            ScannerMutationGuardBit(BufferID.ShinobuScannerEncyclopediaState);

        [SerializeField] private bool scanActive = true;
        [SerializeField] private bool seedMockData = true;
        [SerializeField] private int entityCapacity = DefaultEntityCapacity;
        [SerializeField] private int mockEntityCount = 48;
        [SerializeField] private float maxDistanceMeters = 64f;
        [SerializeField] private float beamRadiusMeters = 1.35f;
        [SerializeField] private uint toolLevel = 1u;

        private IDataVault _dataVault;
        private VaultGenerationHandle<ScannerSpatialEntityDTO> _entitiesHandle;
        private VaultGenerationHandle<ScannableEntityMetadataDTO> _metadataHandle;
        private VaultGenerationHandle<MockSdfOcclusionZoneDTO> _occlusionZonesHandle;
        private VaultGenerationHandle<int> _bucketHeadsHandle;
        private VaultGenerationHandle<int> _bucketNextHandle;
        private VaultGenerationHandle<ScanResultDTO> _scanResultsHandle;
        private VaultGenerationHandle<int> _resultCountHandle;
        private VaultGenerationHandle<ActiveScanStateDTO> _activeStateHandle;
        private VaultGenerationHandle<ScannerVfxDTO> _vfxTargetHandle;
        private VaultGenerationHandle<ScannerQueryStatsDTO> _queryStatsHandle;
        private VaultGenerationHandle<ScannerTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<ScannerSettingsDTO> _settingsHandle;
        private VaultGenerationHandle<ScanProgressDTO> _scanProgressHandle;
        private VaultGenerationHandle<ScannerLoreIndexDTO> _loreIndexHandle;
        private VaultGenerationHandle<ScannerEncyclopediaStateDTO> _encyclopediaStateHandle;
        private JobHandle _queryHandle;
        private MockScannerInputSignal _lastInput;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private HectonPlayerMovement _cachedPlayerMovement;
        private float _cachedGlobalQualityWeight = 1f;
        private float _cachedSystemPressure01;
        private int _lastQueryFrame = -1024;
        private int _entityCount;
        private int _telemetryCursor;
        private uint _completionCount;
        private bool _queryScheduled;
        private IDataVault _queryMutationGuardVault;
        private bool _vaultViewsCached;
        private bool _registeredFast;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredCold;
        private bool _hotSwapListenerRegistered;
        private bool _disableCleanupPending;
        private bool _lateTickDormant;
        private bool _dataVaultRebindPending;
        private bool _runtimeStateColdInitRequired;
        private IDataVault _pendingDataVault;

        private static ScannerVfxDTO s_lastVfxTarget;
        private static uint s_lastVfxFrame;

        private ref struct ScannerVaultViews
        {
            public NativeArray<ScannerSpatialEntityDTO> Entities;
            public NativeArray<ScannableEntityMetadataDTO> Metadata;
            public NativeArray<MockSdfOcclusionZoneDTO> OcclusionZones;
            public NativeArray<int> BucketHeads;
            public NativeArray<int> BucketNext;
            public NativeArray<ScanResultDTO> ScanResults;
            public NativeArray<int> ResultCount;
            public NativeArray<ActiveScanStateDTO> ActiveState;
            public NativeArray<ScannerVfxDTO> VfxTarget;
            public NativeArray<ScannerQueryStatsDTO> QueryStats;
            public NativeArray<ScannerTelemetryEntry> Telemetry;
            public NativeArray<ScanProgressDTO> ScanProgress;
            public NativeArray<ScannerLoreIndexDTO> LoreIndex;
            public NativeArray<ScannerEncyclopediaStateDTO> EncyclopediaState;

            public bool HasCoreBuffers =>
                Entities.IsCreated &&
                Metadata.IsCreated &&
                OcclusionZones.IsCreated &&
                BucketHeads.IsCreated &&
                BucketNext.IsCreated &&
                ScanResults.IsCreated &&
                ResultCount.IsCreated &&
                ActiveState.IsCreated &&
                VfxTarget.IsCreated &&
                QueryStats.IsCreated &&
                Telemetry.IsCreated &&
                ScanProgress.IsCreated &&
                LoreIndex.IsCreated &&
                EncyclopediaState.IsCreated;
        }

        public static ScannerSettingsDTO CreateDefaultSettings()
        {
            return new ScannerSettingsDTO
            {
                CellSizeMeters = DefaultCellSizeMeters,
                MaxDistanceMeters = 64f,
                BeamRadiusMeters = 1.35f,
                BeamMinDot = 0.985f,
                BeamMagnetism = 12f,
                ProgressDecayRate = 0.45f,
                QueryBudgetMicroseconds = 100f,
                AcousticIntensity01 = 0.18f,
                LowTierProgressMultiplier = 0.75f,
                HighTierVfxBias = 1.65f,
                SdfMidpointClearance = 0.05f,
                ScanDurationFallback = 1.6f,
                LowTierCadenceFrames = 4,
                MidTierCadenceFrames = 2,
                HighTierCadenceFrames = 1,
                UltraTierCadenceFrames = 1,
                MaxCandidateCells = 81,
                MaxCandidatesPerCell = 8,
                MaxResults = DefaultResultCapacity,
                Flags = 0
            };
        }

        public static bool TryGetLastVfxTarget(out ScannerVfxDTO target, out uint frame)
        {
            target = s_lastVfxTarget;
            frame = s_lastVfxFrame;
            return (target.Flags & VfxFlagHasTarget) != 0u;
        }

        private static uint ResolveSimulationFrame()
        {
            return TimeSliceScheduler.CurrentFrameId;
        }

        private static int ResolveSimulationFrameInt()
        {
            uint frame = ResolveSimulationFrame();
            return frame > int.MaxValue ? int.MaxValue : (int)frame;
        }

        public static bool TryReadVaultSettings(IDataVault vault, out ScannerSettingsDTO settings)
        {
            settings = ScannerDataMiningTuning.Settings;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuScannerSettings, out VaultGenerationHandle<ScannerSettingsDTO> handle) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<ScannerSettingsDTO>.ReadOnly buffer) ||
                buffer.Length == 0)
            {
                return false;
            }

            settings = buffer[0];
            return settings.CellSizeMeters > 0f && math.isfinite(settings.CellSizeMeters);
        }

        public static bool TryWriteVaultSettings(IDataVault vault, in ScannerSettingsDTO settings)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<ScannerSettingsDTO> handle = vault.EnsureGenerationHandle<ScannerSettingsDTO>(
                BufferID.ShinobuScannerSettings,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            bool lockAcquired = false;
            try
            {
                if (handle.BufferID == 0u ||
                    !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out NativeArray<ScannerSettingsDTO> buffer))
                {
                    return false;
                }

                lockAcquired = true;

                if (!buffer.IsCreated || buffer.Length == 0)
                    return false;

                Thread.MemoryBarrier();
                buffer[0] = settings;
                Thread.MemoryBarrier();
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        public void SetScanActive(bool active)
        {
            scanActive = active;
        }

        private void OnEnable()
        {
            _disableCleanupPending = false;
            _lateTickDormant = false;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ScanEvents.EnsureInitializedCold();

            if (!TryInitializeRuntimeState())
                return;
        }

        private bool TryInitializeRuntimeState()
        {
            if (!EnsureVaultState())
            {
                _runtimeStateColdInitRequired = true;
                TryRegisterColdTickLane();
                return false;
            }

            _runtimeStateColdInitRequired = false;

            if (seedMockData)
                SeedMockGridFromPose();

            _cachedGlobalQualityWeight = ResolveGlobalQualityWeight();
            TryRegisterRuntimeTickLanes();

            return true;
        }

        private void OnDisable()
        {
            scanActive = false;
            _disableCleanupPending = true;

            if (_registeredFast)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);

            _registeredFast = false;
            _registeredSlow = false;
            _registeredCold = false;
            TryUnregisterHotSwapListener();

            if (_queryScheduled && !TryFinalizeScheduledQuery())
            {
                TryRegisterLateFrameTickLane();
                _lateTickDormant = false;
                return;
            }

            FinalizeDisableCleanupAndUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            scanActive = false;
            _disableCleanupPending = true;

            if (_registeredFast)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);

            _registeredFast = false;
            _registeredSlow = false;
            _registeredCold = false;
            TryUnregisterHotSwapListener();

            if (_queryScheduled)
            {
                DispatcherJobFence.BeginLateFrameSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _queryHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndLateFrameSwapWindow();
                }

                _queryScheduled = false;
                ReleaseQueryMutationGuard();
            }

            FinalizeDisableCleanupAndUnregisterLateFrame();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_queryScheduled)
                return;

            if (!TryReadVaultViews(out ScannerVaultViews views) ||
                !views.Entities.IsCreated ||
                !views.LoreIndex.IsCreated ||
                !views.EncyclopediaState.IsCreated ||
                views.EncyclopediaState.Length == 0)
            {
                return;
            }

            int count = math.min(math.max(0, _entityCount), views.Entities.Length);
            if (count == 0)
                return;

            ActiveScanStateDTO active = views.ActiveState.IsCreated && views.ActiveState.Length > 0
                ? views.ActiveState[0]
                : default;
            ScannerEncyclopediaStateDTO unlockState = views.EncyclopediaState[0];
            Color previousColor = Gizmos.color;
            for (int i = 0; i < count; i++)
            {
                ScannerSpatialEntityDTO entity = views.Entities[i];
                if (entity.EntityHash == 0u || !math.all(math.isfinite(entity.AUP)))
                    continue;

                uint loreEntryIndex;
                bool hasLoreIndex = TryFindLoreIndex(views.LoreIndex, entity.EntityHash, out loreEntryIndex);
                bool unlocked = hasLoreIndex && IsLoreBitUnlocked(in unlockState, loreEntryIndex);
                bool activeLocked = !unlocked && active.TargetHash == entity.EntityHash && active.Progress01 > 0f;
                Gizmos.color = unlocked
                    ? new Color(0.05f, 0.95f, 0.25f, 0.82f)
                    : activeLocked
                        ? new Color(1f, 0.85f, 0.1f, 0.82f)
                        : new Color(0.08f, 0.45f, 1f, 0.65f);

                float3 local = AupPrecisionMath.LocalDeltaFloat3(
                    entity.AUP,
                    HectonFloatingOrigin.CurrentTotalOffsetDouble,
                    float3.zero);
                Vector3 runtimePosition = new Vector3(local.x, local.y, local.z);
                Gizmos.DrawWireSphere(runtimePosition, math.max(0.1f, entity.SphereRadius));
            }

            Gizmos.color = previousColor;
        }
#endif

        public void FastTick(float deltaTime)
        {
            if (_runtimeStateColdInitRequired)
                return;

            if (_queryScheduled)
                return;

            ScannerSettingsDTO settings = ResolveCurrentSettings();
            settings.MaxDistanceMeters = math.max(0.1f, maxDistanceMeters > 0f ? maxDistanceMeters : settings.MaxDistanceMeters);
            settings.BeamRadiusMeters = math.max(0.05f, beamRadiusMeters > 0f ? beamRadiusMeters : settings.BeamRadiusMeters);

            int frame = ResolveSimulationFrameInt();
            int cadence = ResolveQueryCadenceFrames(_cachedGlobalQualityWeight, _cachedSystemPressure01, in settings);
            if (frame - _lastQueryFrame < cadence)
                return;

            IDataVault vault = _dataVault;
            if (!TryAcquireQueryMutationGuard(vault))
                return;

            bool scheduled = false;
            try
            {
                if (!TryReadVaultViews(out ScannerVaultViews views))
                    return;

                settings = ResolveCurrentSettings();
                settings.MaxDistanceMeters = math.max(0.1f, maxDistanceMeters > 0f ? maxDistanceMeters : settings.MaxDistanceMeters);
                settings.BeamRadiusMeters = math.max(0.05f, beamRadiusMeters > 0f ? beamRadiusMeters : settings.BeamRadiusMeters);
                _lastQueryFrame = frame;
                _lastInput = BuildInputSignal(deltaTime, frame, in settings);
                views.ResultCount[0] = 0;
                if (views.ScanResults.Length > 0)
                    views.ScanResults[0] = default;

                _queryHandle = new ScannerSpatialQueryJob
                {
                    Entities = views.Entities,
                    Metadata = views.Metadata,
                    OcclusionZones = views.OcclusionZones,
                    BucketHeads = views.BucketHeads,
                    BucketNext = views.BucketNext,
                    Results = views.ScanResults,
                    ResultCount = views.ResultCount,
                    QueryStats = views.QueryStats,
                    Input = _lastInput,
                    Settings = settings,
                    EntityCount = math.min(views.Entities.Length, math.max(0, _entityCount)),
                    MetadataCount = views.Metadata.Length,
                    OcclusionZoneCount = views.OcclusionZones.Length
                }.Schedule();
                H8Memory.RegisterActiveJob(OwnerSystemId, _queryHandle);
                _queryScheduled = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseQueryMutationGuard();
            }
        }

        public void LateFrameTick()
        {
            if (_lateTickDormant)
                return;

            if (_disableCleanupPending)
            {
                if (_queryScheduled && !TryFinalizeScheduledQuery())
                    return;

                FinalizeDisableCleanupHot();
                return;
            }

            if (!_queryScheduled)
                return;

            if (!TryFinalizeScheduledQuery())
                return;

            ProcessCompletedQuery(_lastInput.DeltaTime);
            TryApplyPendingDataVaultRebind();
        }

        public void SlowTick()
        {
            if (_runtimeStateColdInitRequired)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null || !playerContext.IsInitialized)
            {
                _cachedPlayerMovement = null;
            }
            else if (_cachedPlayerMovement == null)
            {
                _cachedPlayerMovement = playerContext.PlayerMovement;
            }

            _cachedGlobalQualityWeight = ResolveGlobalQualityWeight();
            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (healthSignals.Length > 0)
                _cachedSystemPressure01 = math.saturate(healthSignals[healthSignals.Length - 1].Pressure01);
        }

        public void ColdTick()
        {
            if (!_runtimeStateColdInitRequired ||
                _disableCleanupPending ||
                !isActiveAndEnabled ||
                _queryScheduled ||
                _queryMutationGuardVault != null)
            {
                return;
            }

            TryInitializeRuntimeState();
        }

        private bool TryFinalizeScheduledQuery()
        {
            if (!_queryScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _queryHandle))
                return false;

            try
            {
                _queryScheduled = false;
            }
            finally
            {
                ReleaseQueryMutationGuard();
            }
            return true;
        }

        private void FinalizeDisableCleanupHot()
        {
            ReleaseQueryMutationGuard();

            _disableCleanupPending = false;
            _lateTickDormant = _registeredLate;
            ReleaseHandlesOnly();
        }

        private void FinalizeDisableCleanupAndUnregisterLateFrame()
        {
            FinalizeDisableCleanupHot();
            if (!_registeredLate)
                return;

            UnregisterLateFrameTickLane();
        }

        private MockScannerInputSignal BuildInputSignal(float deltaTime, int frame, in ScannerSettingsDTO settings)
        {
            bool hasPose = TryResolveScannerPose(out double3 origin, out float3 direction);
            return new MockScannerInputSignal
            {
                RayOriginAUP = origin,
                RayDirection = direction,
                MaxDistance = settings.MaxDistanceMeters,
                DeltaTime = math.max(0f, deltaTime),
                BeamRadius = settings.BeamRadiusMeters,
                ToolHash = ScannerToolHash,
                Frame = unchecked((uint)frame),
                ToolLevel = toolLevel,
                Flags = scanActive && hasPose ? 1u : 0u
            };
        }

        private bool TryResolveScannerPose(out double3 originAup, out float3 forward)
        {
            originAup = default;
            forward = new float3(0f, 0f, 1f);

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite() &&
                    math.all(math.isfinite(snapshot.RuntimePosition)))
                {
                    if (!TryNormalizeScannerForward(snapshot.Forward, out forward))
                        return false;

                    originAup = snapshot.Aup.ToAbsoluteDouble3();
                    return math.all(math.isfinite(originAup)) && math.all(math.isfinite(forward));
                }

                HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                    _cachedPlayerMovement = playerMovement;
            }

            return false;
        }

        private bool TryResolveCachedPlayerAup(out double3 originAup)
        {
            originAup = default;
            HectonPlayerMovement cachedPlayerMovement = _cachedPlayerMovement;
            if (cachedPlayerMovement == null)
                return false;

            AbsoluteUniversePosition playerAup = cachedPlayerMovement.CurrentAup;
            if (!playerAup.IsFinite())
                return false;

            originAup = playerAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAup));
        }

        private static float3 ResolveScannerRight(float3 forward)
        {
            float3 safeForward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            float3 up = math.abs(safeForward.y) > 0.95f ? new float3(0f, 0f, 1f) : new float3(0f, 1f, 0f);
            return math.normalizesafe(math.cross(up, safeForward), new float3(1f, 0f, 0f));
        }

        private static bool TryNormalizeScannerForward(float3 candidate, out float3 forward)
        {
            forward = default;
            if (!math.all(math.isfinite(candidate)))
                return false;

            float lengthSq = math.lengthsq(candidate);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return false;

            forward = candidate * math.rsqrt(math.max(lengthSq, 0.0001f));
            return math.all(math.isfinite(forward));
        }

        private void ProcessCompletedQuery(float deltaTime)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireScannerMutationGuard(vault, ScannerCompletionMutationGuardMask))
                return;

            bool shouldDumpAnomaly = false;
            uint anomalyScalar = 0u;

            try
            {
                if (!TryReadVaultViews(out ScannerVaultViews views))
                    return;

                ScannerSettingsDTO settings = ResolveCurrentSettings();
                ScannerQueryStatsDTO stats = views.QueryStats.Length > 0 ? views.QueryStats[0] : default;
                int resultCount = views.ResultCount.Length > 0 ? views.ResultCount[0] : 0;

                unsafe
                {
                    ref ActiveScanStateDTO state = ref GetActiveStateRef(views.ActiveState);
                    if (resultCount > 0 && views.ScanResults.Length > 0)
                    {
                        ScanResultDTO result = views.ScanResults[0];
                        float scanDuration = ResolveScanDuration(stats.BestEntityIndex, views.Entities, views.Metadata, in settings);

                        ScannerScanProgression.Solve(ref state, ref result, stats.BestEntityIndex, scanDuration, deltaTime, _lastInput.Frame, _cachedGlobalQualityWeight, in settings);
                        state.LastOriginAUP = _lastInput.RayOriginAUP;
                        state.BeamScore = stats.BestScore;
                        if ((stats.Flags & QueryFlagOccluded) != 0u)
                            state.Flags |= StateFlagOccluded;
                        CopyEntityStateToActive(ref state, stats.BestEntityIndex, views.Entities);
                        views.ScanResults[0] = result;
                        WriteVfxTarget(in result, in state, in stats, views.VfxTarget);
                        RouteProgressSignals(in result, in state, in settings);
                        WriteTelemetry(in result, in state, in stats, views.Telemetry);
                        RouteCompletionIfNeeded(in result, ref state, in settings, views);
                    }
                    else
                    {
                        ScannerScanProgression.Decay(ref state, deltaTime, in settings);
                        WriteEmptyVfxTarget(in state, in stats, views.VfxTarget);
                        ScanResultDTO emptyResult = default;
                        WriteTelemetry(in emptyResult, in state, in stats, views.Telemetry);
                    }
                }

                if ((stats.Flags & QueryFlagNaNInput) != 0u ||
                    stats.EstimatedMicroseconds > math.max(1u, (uint)settings.QueryBudgetMicroseconds))
                {
                    shouldDumpAnomaly = true;
                    anomalyScalar = stats.EstimatedMicroseconds;
                }
            }
            finally
            {
                ReleaseScannerMutationGuard(vault, ScannerCompletionMutationGuardMask);
            }

            if (!shouldDumpAnomaly)
                return;

            DumpTelemetryRing();
            PublishDumpAnomaly(anomalyScalar);
        }

        private float ResolveScanDuration(
            int entityIndex,
            NativeArray<ScannerSpatialEntityDTO> entities,
            NativeArray<ScannableEntityMetadataDTO> metadata,
            in ScannerSettingsDTO settings)
        {
            if ((uint)entityIndex < (uint)entities.Length)
            {
                ScannerSpatialEntityDTO entity = entities[entityIndex];
                if (entity.MetadataIndex < metadata.Length)
                {
                    ScannableEntityMetadataDTO entry = metadata[(int)entity.MetadataIndex];
                    if (math.isfinite(entry.ScanDuration) && entry.ScanDuration > 0.01f)
                        return entry.ScanDuration;
                }
            }

            return math.max(0.01f, settings.ScanDurationFallback);
        }

        private void CopyEntityStateToActive(ref ActiveScanStateDTO state, int entityIndex, NativeArray<ScannerSpatialEntityDTO> entities)
        {
            if ((uint)entityIndex >= (uint)entities.Length)
                return;

            ScannerSpatialEntityDTO entity = entities[entityIndex];
            state.SectorHash = entity.SectorHash;
            state.DepletionMask = entity.DepletionMask;
            state.DepletionWordIndex = entity.DepletionWordIndex;
            state.MetadataFlags = entity.Flags;
        }

        private void RouteProgressSignals(in ScanResultDTO result, in ActiveScanStateDTO state, in ScannerSettingsDTO settings)
        {
            if (result.EntityHash == 0u || (state.Flags & StateFlagHasTarget) == 0u)
                return;

            ToolAcousticSignal acousticSignal = new ToolAcousticSignal
            {
                ToolHash = ScannerToolHash,
                TargetHash = result.EntityHash,
                Progress01 = math.saturate(result.ScanProgress),
                PitchScale = 0.9f + math.saturate(result.ScanProgress) * 0.25f,
                Intensity01 = math.saturate(settings.AcousticIntensity01),
                Frame = ResolveSimulationFrame(),
                State = ToolAcousticStateScanner,
                Flags = 0
            };
            SignalBus<ToolAcousticSignal>.TryPushTracked(in acousticSignal, ref _signalPushDropCount);
        }

        private void RouteCompletionIfNeeded(
            in ScanResultDTO result,
            ref ActiveScanStateDTO state,
            in ScannerSettingsDTO settings,
            ScannerVaultViews views)
        {
            if ((state.Flags & StateFlagCompletedThisFrame) == 0u || result.EntityHash == 0u)
                return;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(result.AUP);
            uint frame = ResolveSimulationFrame();
            SignalBus<EncyclopediaUnlockSignal>.TryPushTracked(new EncyclopediaUnlockSignal
            {
                EntityHash = result.EntityHash,
                SourceHash = ScannerToolHash,
                Frame = frame,
                ScanId = result.EntityHash,
                Kind = (byte)ScanEntryKind.Scannable,
                Flags = 0,
                RequiredToolLevel = (ushort)math.min(ushort.MaxValue, toolLevel),
                Reserved0 = 0u,
                Reserved1 = 0UL
            }, ref _signalPushDropCount);
            ScanCompleteSignal scanComplete = new ScanCompleteSignal
            {
                PositionAup = aup,
                EntryHash = result.EntityHash,
                ScanId = result.EntityHash,
                SourceId = ScannerToolHash,
                ReconKind = (byte)ScanEntryKind.Scannable,
                Flags = 0
            };
            SignalBus<ScanCompleteSignal>.TryPushTracked(in scanComplete, ref _signalPushDropCount);
            LoreFragmentScannedSignal loreFragment = new LoreFragmentScannedSignal
            {
                PositionAup = aup,
                Hash = result.EntityHash,
                Frame = frame,
                SourceId = ScannerToolHash,
                Flags = (byte)(LoreFragmentScannedSignal.FlagPairedScanComplete | LoreFragmentScannedSignal.FlagHasAup)
            };
            SignalBus<LoreFragmentScannedSignal>.TryPushTracked(in loreFragment, ref _signalPushDropCount);
            ScanEvents.TryRaiseEntryDiscovered(result.EntityHash, result.EntityHash, 0u, 0u, ScanEntryKind.Scannable);

            if ((state.MetadataFlags & MetadataFlagDepletable) != 0u)
            {
                SignalBus<EntityDepletedSignal>.TryPushTracked(new EntityDepletedSignal
                {
                    EntityHash = result.EntityHash,
                    SourceHash = ScannerToolHash,
                    Frame = frame,
                    WordIndex = (ushort)math.min(ushort.MaxValue, state.DepletionWordIndex),
                    Operation = 1,
                    Flags = 0,
                    SectorHash = state.SectorHash,
                    DepletionMask = state.DepletionMask
                }, ref _signalPushDropCount);
                ResourceDepletionDeltaSignal depletionDelta = new ResourceDepletionDeltaSignal
                {
                    SectorHash = state.SectorHash,
                    DepletionMask = state.DepletionMask,
                    OreHash = result.EntityHash,
                    Frame = frame,
                    WordIndex = (ushort)math.min(ushort.MaxValue, state.DepletionWordIndex),
                    Operation = 1,
                    Flags = 0
                };
                SignalBus<ResourceDepletionDeltaSignal>.TryPushTracked(in depletionDelta, ref _signalPushDropCount);
            }

            SignalBus<AcousticPingSignal>.TryPushTracked(new AcousticPingSignal
            {
                PositionAup = aup,
                RadiusMeters = math.max(1f, result.Distance * 0.25f),
                Intensity01 = math.saturate(settings.AcousticIntensity01),
                SourceId = ScannerToolHash,
                Channel = AcousticPingSignal.ChannelActiveSonar,
                Flags = AcousticPingSignal.FlagActiveSonar
            }, ref _signalPushDropCount);

            TryEvaluateCompletionScalar(in result, in state, views);
            _completionCount++;
            state.Flags &= ~StateFlagCompletedThisFrame;
        }

        private bool TryEvaluateCompletionScalar(
            in ScanResultDTO result,
            in ActiveScanStateDTO state,
            ScannerVaultViews views)
        {
            if (!views.ScanProgress.IsCreated ||
                views.ScanProgress.Length == 0 ||
                !views.LoreIndex.IsCreated ||
                !views.EncyclopediaState.IsCreated ||
                views.EncyclopediaState.Length == 0 ||
                !views.Telemetry.IsCreated)
            {
                return false;
            }

            uint frame = ResolveSimulationFrame();
            views.ScanProgress[0] = new ScanProgressDTO
            {
                TargetHashID = result.EntityHash,
                CurrentProgress01 = math.saturate(result.ScanProgress),
                ScanRate = 0f,
                Flags = ScanProgressFlagActive | ScanProgressFlagCompleted,
                ScannerAUP = state.LastOriginAUP,
                LastFrame = frame,
                CompletedHash = result.EntityHash
            };

            new UpdateScanProgressJob
            {
                Progress = views.ScanProgress,
                TargetHashID = result.EntityHash,
                ScannerAUP = state.LastOriginAUP,
                ScanRate = 0f,
                SimulationTickDelta = 0f,
                Frame = frame
            }.Execute();
            new EvaluateScanCompletionJob
            {
                Progress = views.ScanProgress,
                LoreIndex = views.LoreIndex,
                EncyclopediaState = views.EncyclopediaState,
                Telemetry = views.Telemetry,
                Frame = frame,
                CompletionCount = _completionCount + 1u
            }.Execute();
            return true;
        }

        private void WriteVfxTarget(
            in ScanResultDTO result,
            in ActiveScanStateDTO state,
            in ScannerQueryStatsDTO stats,
            NativeArray<ScannerVfxDTO> vfxTarget)
        {
            if (!vfxTarget.IsCreated || vfxTarget.Length == 0)
                return;

            ScannerVfxDTO vfx = default;
            vfx.HitAUP = AupPrecisionMath.LocalDeltaFloat3(result.AUP, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
            vfx.HitDistance = result.Distance;
            vfx.ScanProgress = result.ScanProgress;
            vfx.TargetHash = result.EntityHash;
            vfx.Flags = VfxFlagHasTarget;
            vfx.BeamScore = stats.BestScore;
            if ((state.Flags & StateFlagOccluded) != 0u)
                vfx.Flags |= VfxFlagOccluded;

            vfxTarget[0] = vfx;
            s_lastVfxTarget = vfx;
            s_lastVfxFrame = ResolveSimulationFrame();
            ScannerShaderGlobals.Publish(in vfx, _cachedGlobalQualityWeight, _cachedSystemPressure01);
        }

        private void WriteEmptyVfxTarget(
            in ActiveScanStateDTO state,
            in ScannerQueryStatsDTO stats,
            NativeArray<ScannerVfxDTO> vfxTarget)
        {
            if (!vfxTarget.IsCreated || vfxTarget.Length == 0)
                return;

            ScannerVfxDTO vfx = default;
            vfx.ScanProgress = state.Progress01;
            vfx.TargetHash = state.TargetHash;
            vfx.Flags = 0u;
            vfx.BeamScore = stats.BestScore;
            vfxTarget[0] = vfx;
            s_lastVfxTarget = vfx;
            s_lastVfxFrame = ResolveSimulationFrame();
            ScannerShaderGlobals.Publish(in vfx, _cachedGlobalQualityWeight, _cachedSystemPressure01);
        }

        private void WriteTelemetry(
            in ScanResultDTO result,
            in ActiveScanStateDTO state,
            in ScannerQueryStatsDTO stats,
            NativeArray<ScannerTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int index = _telemetryCursor;
            _telemetryCursor = (_telemetryCursor + 1) % telemetry.Length;
            telemetry[index] = new ScannerTelemetryEntry
            {
                TargetAUP = state.TargetAUP,
                _pad0 = 0UL,
                Frame = ResolveSimulationFrame(),
                TargetHash = state.TargetHash,
                Flags = state.Flags | stats.Flags,
                CandidateCount = (uint)math.max(0, stats.CandidateCount),
                CompletedCount = _completionCount,
                EstimatedMicroseconds = stats.EstimatedMicroseconds,
                Progress01 = state.Progress01,
                HitDistance = result.Distance
            };
        }

        private void PublishDumpAnomaly(uint scalar)
        {
            SignalBus<AnomalySignal>.TryPushTracked(new AnomalySignal
            {
                SystemHash = ScannerToolHash,
                AnomalyHash = ScannerDumpReasonHash,
                Scalar = scalar,
                Frame = ResolveSimulationFrame(),
                Severity = 2,
                Flags = 0
            }, ref _signalPushDropCount);

            SignalBus<CrashTelemetrySignal>.TryPushTracked(new CrashTelemetrySignal
            {
                SystemHash = ScannerToolHash,
                ReasonHash = ScannerAnomalyHash,
                Frame = ResolveSimulationFrame(),
                ExitCode = 0,
                NativeAllocationCount = 0,
                NativeTrackedBytesMb = 0f,
                Severity = 1,
                Flags = 0
            }, ref _signalPushDropCount);
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ReleaseHandlesOnly();
                return false;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _dataVault = vault;
            int safeEntityCapacity = math.clamp(entityCapacity, 8, 4096);
            int safeBucketCapacity = ResolveSpatialBucketCapacity(safeEntityCapacity);
            int safeResultCapacity = math.clamp(ScannerDataMiningTuning.Settings.MaxResults, 1, 16);

            _entitiesHandle = vault.EnsureGenerationHandle<ScannerSpatialEntityDTO>(
                BufferID.ShinobuScannerEntities,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _metadataHandle = vault.EnsureGenerationHandle<ScannableEntityMetadataDTO>(
                BufferID.ShinobuScannerMetadata,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _occlusionZonesHandle = vault.EnsureGenerationHandle<MockSdfOcclusionZoneDTO>(
                BufferID.ShinobuScannerOcclusionZones,
                8,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _bucketHeadsHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuScannerSpatialBucketHeads,
                safeBucketCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _bucketNextHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuScannerSpatialNext,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _scanResultsHandle = vault.EnsureGenerationHandle<ScanResultDTO>(
                BufferID.ShinobuScannerScanResults,
                safeResultCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _resultCountHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuScannerResultCount,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _activeStateHandle = vault.EnsureGenerationHandle<ActiveScanStateDTO>(
                BufferID.ShinobuScannerActiveState,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _vfxTargetHandle = vault.EnsureGenerationHandle<ScannerVfxDTO>(
                BufferID.ShinobuScannerVfxTarget,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _queryStatsHandle = vault.EnsureGenerationHandle<ScannerQueryStatsDTO>(
                BufferID.ShinobuScannerQueryStats,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<ScannerTelemetryEntry>(
                BufferID.ShinobuScannerTelemetryRing,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _settingsHandle = vault.EnsureGenerationHandle<ScannerSettingsDTO>(
                BufferID.ShinobuScannerSettings,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _scanProgressHandle = vault.EnsureGenerationHandle<ScanProgressDTO>(
                BufferID.ShinobuScannerScanProgress,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _loreIndexHandle = vault.EnsureGenerationHandle<ScannerLoreIndexDTO>(
                BufferID.ShinobuScannerLoreIndex,
                safeEntityCapacity << 1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _encyclopediaStateHandle = vault.EnsureGenerationHandle<ScannerEncyclopediaStateDTO>(
                BufferID.ShinobuScannerEncyclopediaState,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!TryAcquireScannerMutationGuard(vault, ScannerQueryMutationGuardMask))
                return false;

            try
            {
                if (!TryRefreshVaultViewsCold(out ScannerVaultViews views))
                {
                    ReleaseHandlesOnly();
                    return false;
                }

                ScannerSpatialHash.ClearBuckets(views.BucketHeads, views.BucketNext);
            }
            finally
            {
                ReleaseScannerMutationGuard(vault, ScannerQueryMutationGuardMask);
            }

            if (!TryReadVaultSettings(vault, out _))
                TryWriteVaultSettings(vault, ScannerDataMiningTuning.Settings);
            _entityCount = 0;
            return true;
        }

        private static int ResolveSpatialBucketCapacity(int capacity)
        {
            int target = math.clamp(capacity << 1, DefaultSpatialBucketCapacity, 8192);
            int bucketCapacity = 1;
            while (bucketCapacity < target)
                bucketCapacity <<= 1;
            return bucketCapacity;
        }

        private bool TryReadVaultViews(out ScannerVaultViews views)
        {
            views = default;
            return _vaultViewsCached && TryResolveVaultViews(out views);
        }

        private bool TryRefreshVaultViewsCold(out ScannerVaultViews views)
        {
            bool resolved = TryResolveVaultViews(out views);
            _vaultViewsCached = resolved;
            return resolved;
        }

        private bool TryResolveVaultViews(out ScannerVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryResolveHandle(in _entitiesHandle, out views.Entities) ||
                !vault.TryResolveHandle(in _metadataHandle, out views.Metadata) ||
                !vault.TryResolveHandle(in _occlusionZonesHandle, out views.OcclusionZones) ||
                !vault.TryResolveHandle(in _bucketHeadsHandle, out views.BucketHeads) ||
                !vault.TryResolveHandle(in _bucketNextHandle, out views.BucketNext) ||
                !vault.TryResolveHandle(in _scanResultsHandle, out views.ScanResults) ||
                !vault.TryResolveHandle(in _resultCountHandle, out views.ResultCount) ||
                !vault.TryResolveHandle(in _activeStateHandle, out views.ActiveState) ||
                !vault.TryResolveHandle(in _vfxTargetHandle, out views.VfxTarget) ||
                !vault.TryResolveHandle(in _queryStatsHandle, out views.QueryStats) ||
                !vault.TryResolveHandle(in _telemetryHandle, out views.Telemetry) ||
                !vault.TryResolveHandle(in _scanProgressHandle, out views.ScanProgress) ||
                !vault.TryResolveHandle(in _loreIndexHandle, out views.LoreIndex) ||
                !vault.TryResolveHandle(in _encyclopediaStateHandle, out views.EncyclopediaState) ||
                vault.IsCompactionFenceActive ||
                !views.HasCoreBuffers)
            {
                views = default;
                _vaultViewsCached = false;
                return false;
            }

            return true;
        }

        private void ReleaseHandlesOnly()
        {
            ReleaseQueryMutationGuard();
            _entitiesHandle = default;
            _metadataHandle = default;
            _occlusionZonesHandle = default;
            _bucketHeadsHandle = default;
            _bucketNextHandle = default;
            _scanResultsHandle = default;
            _resultCountHandle = default;
            _activeStateHandle = default;
            _vfxTargetHandle = default;
            _queryStatsHandle = default;
            _telemetryHandle = default;
            _settingsHandle = default;
            _scanProgressHandle = default;
            _loreIndexHandle = default;
            _encyclopediaStateHandle = default;
            _vaultViewsCached = false;
            _dataVault = null;
            _pendingDataVault = null;
            _dataVaultRebindPending = false;
            _entityCount = 0;
            _telemetryCursor = 0;
            _completionCount = 0u;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    RebindDispatcherTickLanes(currentService);
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            CachePlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _cachedPlayerContext = playerContext;
            _cachedPlayerMovement = playerContext != null && playerContext.IsInitialized
                ? playerContext.PlayerMovement
                : null;
        }

        private void RebindDataVault(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            if (_queryScheduled || _queryMutationGuardVault != null)
            {
                _pendingDataVault = nextVault;
                _dataVaultRebindPending = true;
                return;
            }

            ApplyDataVaultRebind(nextVault);
        }

        private void TryApplyPendingDataVaultRebind()
        {
            if (!_dataVaultRebindPending || _queryScheduled || _queryMutationGuardVault != null)
                return;

            IDataVault nextVault = _pendingDataVault;
            _pendingDataVault = null;
            _dataVaultRebindPending = false;
            ApplyPendingDataVaultRebindAfterVisualFence(nextVault);
        }

        private void ApplyDataVaultRebind(IDataVault nextVault)
        {
            ReleaseHandlesOnly();
            _dataVault = nextVault;
            _runtimeStateColdInitRequired = false;

            if (nextVault != null && isActiveAndEnabled && !_disableCleanupPending)
                TryInitializeRuntimeState();
        }

        private void ApplyPendingDataVaultRebindAfterVisualFence(IDataVault nextVault)
        {
            ReleaseHandlesOnly();
            _dataVault = nextVault;
            _runtimeStateColdInitRequired = nextVault != null && isActiveAndEnabled && !_disableCleanupPending;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegisterRuntimeTickLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFast)
                _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            TryRegisterLateFrameTickLane();
            TryRegisterColdTickLane();
        }

        private void TryRegisterColdTickLane()
        {
            if (_registeredCold || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterLateFrameTickLane()
        {
            if (_registeredLate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void UnregisterRuntimeTickLanes(bool includeLateFrame)
        {
            if (_registeredFast)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);

            _registeredFast = false;
            _registeredSlow = false;
            _registeredCold = false;

            if (includeLateFrame)
                UnregisterLateFrameTickLane();
        }

        private void UnregisterLateFrameTickLane()
        {
            if (!_registeredLate)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLate = false;
            _lateTickDormant = false;
        }

        private void RebindDispatcherTickLanes(object currentService)
        {
            bool hadLateFrameRoute = _registeredLate || _queryScheduled || _disableCleanupPending;
            UnregisterRuntimeTickLanes(includeLateFrame: true);

            if (currentService == null || !isActiveAndEnabled)
                return;

            if (_disableCleanupPending)
            {
                if (hadLateFrameRoute)
                {
                    TryRegisterLateFrameTickLane();
                    _lateTickDormant = false;
                }
                return;
            }

            if (_runtimeStateColdInitRequired)
            {
                TryRegisterColdTickLane();
                return;
            }

            TryRegisterRuntimeTickLanes();
        }

        private bool TryAcquireQueryMutationGuard(IDataVault vault)
        {
            if (_queryMutationGuardVault != null)
                return false;

            if (!TryAcquireScannerMutationGuard(vault, ScannerQueryMutationGuardMask))
                return false;

            _queryMutationGuardVault = vault;
            return true;
        }

        private void ReleaseQueryMutationGuard()
        {
            IDataVault vault = _queryMutationGuardVault;
            if (vault == null)
                return;

            _queryMutationGuardVault = null;
            ReleaseScannerMutationGuard(vault, ScannerQueryMutationGuardMask);
        }

        private static bool TryAcquireScannerMutationGuard(IDataVault vault, ulong mask)
        {
            return vault != null && !vault.IsCompactionFenceActive && vault.TryAcquireMutationGuard(mask);
        }

        private static void ReleaseScannerMutationGuard(IDataVault vault, ulong mask)
        {
            vault?.ReleaseMutationGuard(mask);
        }

        private static ulong ScannerMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private ScannerSettingsDTO ResolveCurrentSettings()
        {
            if (TryReadVaultSettings(_dataVault, out ScannerSettingsDTO settings))
                return settings;

            return ScannerDataMiningTuning.Settings;
        }

        private void SeedMockGridFromPose()
        {
            if (!TryResolveScannerPose(out double3 origin, out float3 forward))
            {
                if (!TryResolveCachedPlayerAup(out origin))
                    origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                forward = new float3(0f, 0f, 1f);
            }

            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            float3 right = ResolveScannerRight(forward);
            TryReadVaultSettings(_dataVault, out ScannerSettingsDTO settings);

            IDataVault vault = _dataVault;
            if (!TryAcquireScannerMutationGuard(vault, ScannerCompletionMutationGuardMask))
                return;

            try
            {
                if (!TryReadVaultViews(out ScannerVaultViews views))
                    return;

                int count = math.clamp(mockEntityCount, 1, views.Entities.Length);
                new GenerateMockScannableTargetsJob
                {
                    BucketHeads = views.BucketHeads,
                    BucketNext = views.BucketNext,
                    Entities = views.Entities,
                    Metadata = views.Metadata,
                    LoreIndex = views.LoreIndex,
                    OriginAUP = origin,
                    Forward = forward,
                    Right = right,
                    CellSizeMeters = settings.CellSizeMeters,
                    Count = count
                }.Execute();
                _entityCount = count;
                if (!views.OcclusionZones.IsCreated || views.OcclusionZones.Length <= 0)
                    return;

                views.OcclusionZones[0] = new MockSdfOcclusionZoneDTO
                {
                    CenterAUP = origin + new double3(right * 11f + forward * 32f),
                    Radius = 2.5f,
                    Flags = 1u
                };
            }
            finally
            {
                ReleaseScannerMutationGuard(vault, ScannerCompletionMutationGuardMask);
            }
        }

        private void DumpTelemetryRing()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<ScannerTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return;
            }

            DumpTelemetryRing(telemetry);
        }

        private void DumpTelemetryRing(NativeArray<ScannerTelemetryEntry>.ReadOnly telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            DumpTelemetryRing(telemetry, ResolveDumpPath(DumpFileName));
            DumpTelemetryRing(telemetry, ResolveDumpPath(H8DumpFileName));
        }

        private void DumpTelemetryRing(NativeArray<ScannerTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            DumpTelemetryRing(telemetry, ResolveDumpPath(DumpFileName));
            DumpTelemetryRing(telemetry, ResolveDumpPath(H8DumpFileName));
        }

        private static string ResolveDumpPath(string fileName)
        {
            string projectRoot = Application.isPlaying || !string.IsNullOrEmpty(Application.dataPath)
                ? Path.GetDirectoryName(Application.dataPath)
                : Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot ?? Directory.GetCurrentDirectory(), "Docs", "AgentLogs", fileName);
        }

        public static unsafe void DumpTelemetryRing(NativeArray<ScannerTelemetryEntry> telemetry, string path)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(path))
                return;

            int byteCount = UnsafeUtility.SizeOf<ScannerTelemetryEntry>() * telemetry.Length;
            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount);
        }

        public static unsafe void DumpTelemetryRing(NativeArray<ScannerTelemetryEntry>.ReadOnly telemetry, string path)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(path))
                return;

            int byteCount = UnsafeUtility.SizeOf<ScannerTelemetryEntry>() * telemetry.Length;
            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount);
        }

        public static unsafe ref ActiveScanStateDTO GetActiveStateRef(NativeArray<ActiveScanStateDTO> states)
        {
            void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            return ref ((ActiveScanStateDTO*)pointer)[0];
        }

        public static unsafe void ResetActiveState(ref ActiveScanStateDTO state)
        {
            UnsafeUtility.MemClear(UnsafeUtility.AddressOf(ref state), UnsafeUtility.SizeOf<ActiveScanStateDTO>());
        }

        public static bool ValidateScanProgressLayout(
            out int sizeBytes,
            out int targetHashOffset,
            out int progressOffset,
            out int scanRateOffset,
            out int flagsOffset,
            out int scannerAupOffset,
            out int completedHashOffset)
        {
            sizeBytes = UnsafeUtility.SizeOf<ScanProgressDTO>();
            targetHashOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.TargetHashID)).ToInt32();
            progressOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.CurrentProgress01)).ToInt32();
            scanRateOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.ScanRate)).ToInt32();
            flagsOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.Flags)).ToInt32();
            scannerAupOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.ScannerAUP)).ToInt32();
            completedHashOffset = Marshal.OffsetOf<ScanProgressDTO>(nameof(ScanProgressDTO.CompletedHash)).ToInt32();
            return sizeBytes == 64 &&
                   targetHashOffset == 0 &&
                   progressOffset == 4 &&
                   scanRateOffset == 8 &&
                   flagsOffset == 12 &&
                   scannerAupOffset == 16 &&
                   completedHashOffset == 44;
        }

        public static int ResolveQueryCadenceFrames(float globalQualityWeight, float pressure01, in ScannerSettingsDTO settings)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float pressure = math.saturate(math.isfinite(pressure01) ? pressure01 : 0f);
            float qualityCurve = quality * quality * (3f - 2f * quality);
            float pressureCurve = pressure * pressure * (3f - 2f * pressure);
            float lowCadence = math.max(1f, settings.LowTierCadenceFrames);
            float ultraCadence = math.max(1f, settings.UltraTierCadenceFrames);
            float baseCadence = math.lerp(lowCadence, ultraCadence, qualityCurve);
            float pressureMultiplier = math.lerp(1f, 3f, pressureCurve);
            return math.clamp((int)math.ceil(baseCadence * pressureMultiplier), 1, 16);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        public static void FillMockSpatialHash(
            NativeArray<int> bucketHeads,
            NativeArray<int> bucketNext,
            NativeArray<ScannerSpatialEntityDTO> entities,
            NativeArray<ScannableEntityMetadataDTO> metadata,
            double3 origin,
            float3 forward,
            float3 right,
            float cellSizeMeters,
            int count)
        {
            FillMockSpatialHash(bucketHeads, bucketNext, entities, metadata, default, origin, forward, right, cellSizeMeters, count);
        }

        public static void FillMockSpatialHash(
            NativeArray<int> bucketHeads,
            NativeArray<int> bucketNext,
            NativeArray<ScannerSpatialEntityDTO> entities,
            NativeArray<ScannableEntityMetadataDTO> metadata,
            NativeArray<ScannerLoreIndexDTO> loreIndex,
            double3 origin,
            float3 forward,
            float3 right,
            float cellSizeMeters,
            int count)
        {
            if (!bucketHeads.IsCreated || !bucketNext.IsCreated || !entities.IsCreated || !metadata.IsCreated)
                return;

            ScannerSpatialHash.ClearBuckets(bucketHeads, bucketNext);
            ClearLoreIndex(loreIndex);
            int safeCount = math.min(count, math.min(entities.Length, metadata.Length));
            float safeCell = math.max(1f, cellSizeMeters);
            float3 up = new float3(0f, 1f, 0f);
            for (int i = 0; i < safeCount; i++)
            {
                float lateral = ((i % 7) - 3) * 1.45f;
                float vertical = (((i / 7) % 3) - 1) * 0.75f;
                float distance = 6f + (i % 16) * 3.2f;
                double3 aup = origin + new double3(forward * distance + right * lateral + up * vertical);
                uint hash = 0x53430000u | (uint)(i + 1);
                uint flags = (i & 1) == 0 ? MetadataFlagFlora : MetadataFlagFauna;
                if ((i % 11) == 0)
                    flags |= MetadataFlagScarcityNode | MetadataFlagDepletable;

                entities[i] = new ScannerSpatialEntityDTO
                {
                    AUP = aup,
                    EntityHash = hash,
                    SphereRadius = 0.8f + (i % 5) * 0.2f,
                    MetadataIndex = (uint)i,
                    Flags = flags,
                    SectorHash = ScannerSpatialHash.HashSector64(aup, 64f),
                    DepletionMask = 1UL << (i & 63),
                    DepletionWordIndex = (uint)(i >> 6),
                    _pad0 = 0u
                };

                metadata[i] = new ScannableEntityMetadataDTO
                {
                    EntityHash = hash,
                    ScanDuration = 1.1f + (i % 6) * 0.15f,
                    RequiredToolLevel = 1u,
                    _pad0 = 0u
                };

                int key = ScannerSpatialHash.CellKey(aup, safeCell);
                ScannerSpatialHash.InsertBucket(bucketHeads, bucketNext, key, i);
                InsertLoreIndex(loreIndex, hash, (uint)i);
            }
        }

        public static void ClearLoreIndex(NativeArray<ScannerLoreIndexDTO> loreIndex)
        {
            if (!loreIndex.IsCreated)
                return;

            for (int i = 0; i < loreIndex.Length; i++)
                loreIndex[i] = default;
        }

        public static bool InsertLoreIndex(NativeArray<ScannerLoreIndexDTO> loreIndex, uint targetHash, uint loreEntryIndex)
        {
            if (!loreIndex.IsCreated || loreIndex.Length == 0 || targetHash == 0u)
                return false;

            int start = ScannerSpatialHash.BucketIndex(unchecked((int)targetHash), loreIndex.Length);
            for (int probe = 0; probe < loreIndex.Length; probe++)
            {
                int index = start + probe;
                if (index >= loreIndex.Length)
                    index -= loreIndex.Length;

                ScannerLoreIndexDTO entry = loreIndex[index];
                if (entry.TargetHashID != 0u && entry.TargetHashID != targetHash)
                    continue;

                entry.TargetHashID = targetHash;
                entry.LoreEntryIndex = loreEntryIndex;
                entry.Flags = LoreIndexFlagOccupied;
                entry.SourceHash = ScannerToolHash;
                entry.ProbeStride = (uint)probe;
                loreIndex[index] = entry;
                return true;
            }

            return false;
        }

        public static bool TryFindLoreIndex(
            NativeArray<ScannerLoreIndexDTO> loreIndex,
            uint targetHash,
            out uint loreEntryIndex)
        {
            loreEntryIndex = 0u;
            if (!loreIndex.IsCreated || loreIndex.Length == 0 || targetHash == 0u)
                return false;

            int start = ScannerSpatialHash.BucketIndex(unchecked((int)targetHash), loreIndex.Length);
            for (int probe = 0; probe < loreIndex.Length; probe++)
            {
                int index = start + probe;
                if (index >= loreIndex.Length)
                    index -= loreIndex.Length;

                ScannerLoreIndexDTO entry = loreIndex[index];
                if (entry.TargetHashID == targetHash)
                {
                    loreEntryIndex = entry.LoreEntryIndex;
                    return true;
                }

                if (entry.TargetHashID == 0u)
                    return false;
            }

            return false;
        }

        public static bool IsLoreBitUnlocked(in ScannerEncyclopediaStateDTO state, uint loreEntryIndex)
        {
            int bitIndex = (int)(loreEntryIndex & 1023u);
            ulong bitMask = 1UL << (bitIndex & 63);
            switch (bitIndex >> 6)
            {
                case 0: return (state.Mask0 & bitMask) != 0UL;
                case 1: return (state.Mask1 & bitMask) != 0UL;
                case 2: return (state.Mask2 & bitMask) != 0UL;
                case 3: return (state.Mask3 & bitMask) != 0UL;
                case 4: return (state.Mask4 & bitMask) != 0UL;
                case 5: return (state.Mask5 & bitMask) != 0UL;
                case 6: return (state.Mask6 & bitMask) != 0UL;
                case 7: return (state.Mask7 & bitMask) != 0UL;
                case 8: return (state.Mask8 & bitMask) != 0UL;
                case 9: return (state.Mask9 & bitMask) != 0UL;
                case 10: return (state.Mask10 & bitMask) != 0UL;
                case 11: return (state.Mask11 & bitMask) != 0UL;
                case 12: return (state.Mask12 & bitMask) != 0UL;
                case 13: return (state.Mask13 & bitMask) != 0UL;
                case 14: return (state.Mask14 & bitMask) != 0UL;
                default: return (state.Mask15 & bitMask) != 0UL;
            }
        }

#if UNITY_EDITOR
        public static bool TryApplyLoreIndexCsvLine(
            ReadOnlySpan<byte> line,
            NativeArray<ScannerLoreIndexDTO> loreIndex,
            out uint targetHash,
            out uint loreEntryIndex)
        {
            targetHash = 0u;
            loreEntryIndex = 0u;
            if (line.Length == 0 || !loreIndex.IsCreated)
                return false;

            int cursor = 0;
            if (!TryParseHashOrToken(line, ref cursor, out targetHash))
                return false;

            SkipCsvSeparators(line, ref cursor);
            if (!TryParseUnsigned(line, ref cursor, out loreEntryIndex))
                return false;

            return InsertLoreIndex(loreIndex, targetHash, loreEntryIndex);
        }
#endif

        public static uint ComputeFnv1a32Ascii(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash = (hash ^ value) * 16777619u;
            }

            return hash == 0u ? 2166136261u : hash;
        }

#if UNITY_EDITOR
        public static bool TryApplyCsvOverrideLine(
            ReadOnlySpan<char> line,
            NativeArray<ScannableEntityMetadataDTO> metadata,
            int metadataCount,
            out uint entityHash,
            out float scanDurationSeconds)
        {
            entityHash = 0u;
            scanDurationSeconds = 0f;
            if (line.Length == 0 || !metadata.IsCreated)
                return false;

            int cursor = 0;
            if (!TryParseUnsigned(line, ref cursor, out entityHash))
                return false;
            SkipCsvSeparators(line, ref cursor);
            if (!TryParsePositiveFloat(line, ref cursor, out scanDurationSeconds))
                return false;

            int count = math.min(math.max(0, metadataCount), metadata.Length);
            for (int i = 0; i < count; i++)
            {
                ScannableEntityMetadataDTO entry = metadata[i];
                if (entry.EntityHash != entityHash)
                    continue;

                entry.ScanDuration = scanDurationSeconds;
                metadata[i] = entry;
                return true;
            }

            return false;
        }
#endif

        private static void SkipCsvSeparators(ReadOnlySpan<char> line, ref int cursor)
        {
            while (cursor < line.Length)
            {
                char c = line[cursor];
                if (c != ',' && c != ';' && c != ' ' && c != '\t')
                    break;
                cursor++;
            }
        }

        private static void SkipCsvSeparators(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c != (byte)',' && c != (byte)';' && c != (byte)' ' && c != (byte)'\t')
                    break;
                cursor++;
            }
        }

        private static bool TryParseHashOrToken(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipCsvSeparators(line, ref cursor);
            int start = cursor;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c == (byte)',' || c == (byte)';' || c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r' || c == (byte)'\n')
                    break;
                cursor++;
            }

            int end = cursor;
            while (end > start && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\t'))
                end--;

            if (end <= start)
                return false;

            ReadOnlySpan<byte> token = line.Slice(start, end - start);
            int tokenCursor = 0;
            if (TryParseUnsigned(token, ref tokenCursor, out uint numeric) && tokenCursor >= token.Length)
            {
                value = numeric;
                return value != 0u;
            }

            value = ComputeFnv1a32Ascii(token);
            return value != 0u;
        }

        private static bool TryParseUnsigned(ReadOnlySpan<char> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipCsvSeparators(line, ref cursor);
            bool hex = cursor + 1 < line.Length &&
                       line[cursor] == '0' &&
                       (line[cursor + 1] == 'x' || line[cursor + 1] == 'X');
            if (hex)
                cursor += 2;

            bool any = false;
            while (cursor < line.Length)
            {
                char c = line[cursor];
                uint digit;
                if (c >= '0' && c <= '9')
                    digit = (uint)(c - '0');
                else if (hex && c >= 'a' && c <= 'f')
                    digit = (uint)(10 + c - 'a');
                else if (hex && c >= 'A' && c <= 'F')
                    digit = (uint)(10 + c - 'A');
                else
                    break;

                uint multiplier = hex ? 16u : 10u;
                value = value * multiplier + digit;
                any = true;
                cursor++;
            }

            return any;
        }

        private static bool TryParseUnsigned(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipCsvSeparators(line, ref cursor);
            bool hex = cursor + 1 < line.Length &&
                       line[cursor] == (byte)'0' &&
                       (line[cursor + 1] == (byte)'x' || line[cursor + 1] == (byte)'X');
            if (hex)
                cursor += 2;

            bool any = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(10 + c - (byte)'a');
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(10 + c - (byte)'A');
                else
                    break;

                uint multiplier = hex ? 16u : 10u;
                value = value * multiplier + digit;
                any = true;
                cursor++;
            }

            return any;
        }

        private static bool TryParsePositiveFloat(ReadOnlySpan<char> line, ref int cursor, out float value)
        {
            value = 0f;
            SkipCsvSeparators(line, ref cursor);
            double accumulator = 0d;
            bool any = false;
            while (cursor < line.Length)
            {
                char c = line[cursor];
                if (c < '0' || c > '9')
                    break;

                accumulator = accumulator * 10d + (c - '0');
                any = true;
                cursor++;
            }

            if (cursor < line.Length && line[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < line.Length)
                {
                    char c = line[cursor];
                    if (c < '0' || c > '9')
                        break;

                    accumulator += (c - '0') * scale;
                    scale *= 0.1d;
                    any = true;
                    cursor++;
                }
            }

            value = (float)accumulator;
            return any && math.isfinite(value) && value > 0f;
        }
    }

    public static class ScannerSpatialHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellKey(double3 aup, float cellSizeMeters)
        {
            float safeCell = math.max(1f, cellSizeMeters);
            int3 cell = new int3(
                (int)math.floor(aup.x / safeCell),
                (int)math.floor(aup.y / safeCell),
                (int)math.floor(aup.z / safeCell));
            return HashCell(cell);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashCell(int3 cell)
        {
            unchecked
            {
                uint x = (uint)cell.x * 73856093u;
                uint y = (uint)cell.y * 19349663u;
                uint z = (uint)cell.z * 83492791u;
                return (int)(x ^ y ^ z);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BucketIndex(int key, int bucketCount)
        {
            int safeCount = math.max(1, bucketCount);
            uint unsignedKey = (uint)key;
            if ((safeCount & (safeCount - 1)) == 0)
                return (int)(unsignedKey & (uint)(safeCount - 1));

            return (int)(unsignedKey % (uint)safeCount);
        }

        public static void ClearBuckets(NativeArray<int> bucketHeads, NativeArray<int> bucketNext)
        {
            if (bucketHeads.IsCreated)
            {
                for (int i = 0; i < bucketHeads.Length; i++)
                    bucketHeads[i] = -1;
            }

            if (bucketNext.IsCreated)
            {
                for (int i = 0; i < bucketNext.Length; i++)
                    bucketNext[i] = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertBucket(NativeArray<int> bucketHeads, NativeArray<int> bucketNext, int key, int entityIndex)
        {
            if (!bucketHeads.IsCreated || !bucketNext.IsCreated ||
                bucketHeads.Length == 0 ||
                (uint)entityIndex >= (uint)bucketNext.Length)
            {
                return;
            }

            int bucket = BucketIndex(key, bucketHeads.Length);
            bucketNext[entityIndex] = bucketHeads[bucket];
            bucketHeads[bucket] = entityIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long HashSector64(double3 aup, float sectorSizeMeters)
        {
            double safeSector = math.max(1f, sectorSizeMeters);
            long x = (long)math.floor(aup.x / safeSector);
            long y = (long)math.floor(aup.y / safeSector);
            long z = (long)math.floor(aup.z / safeSector);
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = (hash ^ (ulong)x) * 1099511628211UL;
                hash = (hash ^ (ulong)y) * 1099511628211UL;
                hash = (hash ^ (ulong)z) * 1099511628211UL;
                return (long)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRaySphere(
            double3 originAUP,
            float3 direction,
            double3 centerAUP,
            float radius,
            float maxDistance,
            out float distance,
            out float frontDot)
        {
            distance = 0f;
            frontDot = 0f;
            double3 deltaDouble = centerAUP - originAUP;
            float3 delta = (float3)deltaDouble;
            float lenSq = math.lengthsq(delta);
            if (!math.isfinite(lenSq) || lenSq <= 0.000001f)
                return false;

            float projection = math.dot(delta, direction);
            frontDot = projection * math.rsqrt(lenSq);
            float c = lenSq - radius * radius;
            float discriminant = projection * projection - c;
            if (discriminant < 0f)
                return false;

            float root = math.sqrt(discriminant);
            float t = projection - root;
            if (t < 0f)
                t = projection + root;

            if (t < 0f || t > maxDistance)
                return false;

            distance = t;
            return math.isfinite(distance);
        }
    }

    public static class ScannerScanProgression
    {
        public static void Solve(
            ref ActiveScanStateDTO state,
            ref ScanResultDTO result,
            int bestEntityIndex,
            float scanDurationSeconds,
            float deltaTime,
            uint frame,
            float globalQualityWeight,
            in ScannerSettingsDTO settings)
        {
            if (result.EntityHash == 0u)
            {
                Decay(ref state, deltaTime, in settings);
                return;
            }

            if (state.TargetHash != result.EntityHash)
            {
                state.Progress01 = 0f;
                state.CompletedHash = 0u;
            }

            state.TargetHash = result.EntityHash;
            state.TargetAUP = result.AUP;
            state.ScanDurationSeconds = math.max(0.01f, scanDurationSeconds);
            state.HitDistance = result.Distance;
            state.BestEntityIndex = bestEntityIndex;
            state.Flags |= ScannerDataMiningRouter.StateFlagHasTarget;
            state.Flags &= ~ScannerDataMiningRouter.StateFlagOccluded;
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float qualityCurve = quality * quality * (3f - 2f * quality);
            float multiplier = math.lerp(math.max(0.1f, settings.LowTierProgressMultiplier), 1f, qualityCurve);
            state.Progress01 = math.saturate(state.Progress01 + math.max(0f, deltaTime) * multiplier / state.ScanDurationSeconds);
            state.LastFrame = frame;
            if (state.Progress01 >= 1f && state.CompletedHash != result.EntityHash)
            {
                state.CompletedHash = result.EntityHash;
                state.Flags |= ScannerDataMiningRouter.StateFlagCompletedThisFrame;
            }

            result.ScanProgress = state.Progress01;
        }

        public static void Decay(ref ActiveScanStateDTO state, float deltaTime, in ScannerSettingsDTO settings)
        {
            state.Progress01 = math.max(0f, state.Progress01 - math.max(0f, deltaTime) * math.max(0f, settings.ProgressDecayRate));
            if (state.Progress01 <= 0f)
            {
                ScannerDataMiningRouter.ResetActiveState(ref state);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockScannableTargetsJob : IJob
    {
        [NoAlias] public NativeArray<int> BucketHeads;
        [NoAlias] public NativeArray<int> BucketNext;
        [NoAlias] public NativeArray<ScannerSpatialEntityDTO> Entities;
        [NoAlias] public NativeArray<ScannableEntityMetadataDTO> Metadata;
        [NoAlias] public NativeArray<ScannerLoreIndexDTO> LoreIndex;
        public double3 OriginAUP;
        public float3 Forward;
        public float3 Right;
        public float CellSizeMeters;
        public int Count;

        public void Execute()
        {
            ScannerDataMiningRouter.FillMockSpatialHash(
                BucketHeads,
                BucketNext,
                Entities,
                Metadata,
                LoreIndex,
                OriginAUP,
                math.normalizesafe(Forward, new float3(0f, 0f, 1f)),
                math.normalizesafe(Right, new float3(1f, 0f, 0f)),
                CellSizeMeters,
                Count);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct UpdateScanProgressJob : IJob
    {
        [NoAlias] public NativeArray<ScanProgressDTO> Progress;
        public uint TargetHashID;
        public double3 ScannerAUP;
        public float ScanRate;
        public float SimulationTickDelta;
        public uint Frame;

        public void Execute()
        {
            if (!Progress.IsCreated || Progress.Length == 0)
                return;

            ScanProgressDTO progress = Progress[0];
            float delta = math.max(0f, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0f);
            float rate = math.max(0f, math.isfinite(ScanRate) ? ScanRate : 0f);
            if (TargetHashID == 0u)
            {
                progress.CurrentProgress01 = math.max(0f, progress.CurrentProgress01 - delta * math.max(rate, 0.25f));
                progress.Flags = ScannerDataMiningRouter.ScanProgressFlagLostTarget;
                progress.LastFrame = Frame;
                Progress[0] = progress;
                return;
            }

            if (progress.TargetHashID != TargetHashID)
            {
                progress.CurrentProgress01 = 0f;
                progress.CompletedHash = 0u;
            }

            progress.TargetHashID = TargetHashID;
            progress.ScannerAUP = math.all(math.isfinite(ScannerAUP)) ? ScannerAUP : default;
            progress.ScanRate = rate;
            progress.LastFrame = Frame;
            progress.Flags = ScannerDataMiningRouter.ScanProgressFlagActive;
            progress.CurrentProgress01 = math.saturate(progress.CurrentProgress01 + delta * rate);
            if (progress.CurrentProgress01 >= 1f)
            {
                progress.CompletedHash = TargetHashID;
                progress.Flags |= ScannerDataMiningRouter.ScanProgressFlagCompleted;
            }

            Progress[0] = progress;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateScanCompletionJob : IJob
    {
        [NoAlias] public NativeArray<ScanProgressDTO> Progress;
        [ReadOnly, NoAlias] public NativeArray<ScannerLoreIndexDTO> LoreIndex;
        [NoAlias] public NativeArray<ScannerEncyclopediaStateDTO> EncyclopediaState;
        [NoAlias] public NativeArray<ScannerTelemetryEntry> Telemetry;
        public uint Frame;
        public uint CompletionCount;

        public void Execute()
        {
            if (!Progress.IsCreated || Progress.Length == 0)
                return;

            ScanProgressDTO progress = Progress[0];
            if ((progress.Flags & ScannerDataMiningRouter.ScanProgressFlagCompleted) == 0u ||
                progress.TargetHashID == 0u ||
                !EncyclopediaState.IsCreated ||
                EncyclopediaState.Length == 0)
            {
                return;
            }

            uint loreEntryIndex;
            if (!ScannerDataMiningRouter.TryFindLoreIndex(LoreIndex, progress.TargetHashID, out loreEntryIndex))
                loreEntryIndex = progress.TargetHashID & 1023u;

            int bitIndex = (int)(loreEntryIndex & 1023u);
            int wordIndex = bitIndex >> 6;
            ulong bitMask = 1UL << (bitIndex & 63);
            ulong* masks = (ulong*)NativeArrayUnsafeUtility.GetUnsafePtr(EncyclopediaState);
            AtomicOr(masks, wordIndex, bitMask);
            progress.CompletedHash = progress.TargetHashID;
            Progress[0] = progress;

            if (Telemetry.IsCreated && Telemetry.Length > 0)
            {
                int telemetryIndex = (int)(Frame % (uint)Telemetry.Length);
                Telemetry[telemetryIndex] = new ScannerTelemetryEntry
                {
                    TargetAUP = progress.ScannerAUP,
                    _pad0 = 0UL,
                    Frame = Frame,
                    TargetHash = progress.TargetHashID,
                    Flags = progress.Flags,
                    CandidateCount = 0u,
                    CompletedCount = CompletionCount,
                    EstimatedMicroseconds = 1u,
                    Progress01 = progress.CurrentProgress01,
                    HitDistance = 0f
                };
            }
        }

        private static bool AtomicOr(ulong* words, int wordIndex, ulong bitMask)
        {
            long* signedWords = (long*)words;
            ref long signedWord = ref UnsafeUtility.AsRef<long>(signedWords + wordIndex);
            long signedBit = unchecked((long)bitMask);
            while (true)
            {
                long before = Interlocked.CompareExchange(ref signedWord, 0L, 0L);
                long after = before | signedBit;
                if (before == after)
                    return false;

                if (Interlocked.CompareExchange(ref signedWord, after, before) == before)
                    return true;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AcquireScanTargetJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ScannerSpatialEntityDTO> Entities;
        [ReadOnly, NoAlias] public NativeArray<ScannableEntityMetadataDTO> Metadata;
        [ReadOnly, NoAlias] public NativeArray<MockSdfOcclusionZoneDTO> OcclusionZones;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
        [NoAlias] public NativeArray<ScanResultDTO> Results;
        [NoAlias] public NativeArray<int> ResultCount;
        [NoAlias] public NativeArray<ScannerQueryStatsDTO> QueryStats;
        [NoAlias] public NativeArray<ScanProgressDTO> Progress;
        public MockScannerInputSignal Input;
        public ScannerSettingsDTO Settings;
        public int EntityCount;
        public int MetadataCount;
        public int OcclusionZoneCount;

        public void Execute()
        {
            new ScannerSpatialQueryJob
            {
                Entities = Entities,
                Metadata = Metadata,
                OcclusionZones = OcclusionZones,
                BucketHeads = BucketHeads,
                BucketNext = BucketNext,
                Results = Results,
                ResultCount = ResultCount,
                QueryStats = QueryStats,
                Input = Input,
                Settings = Settings,
                EntityCount = EntityCount,
                MetadataCount = MetadataCount,
                OcclusionZoneCount = OcclusionZoneCount
            }.Execute();

            if (!Progress.IsCreated || Progress.Length == 0 || !Results.IsCreated || Results.Length == 0)
                return;

            if (!ResultCount.IsCreated || ResultCount.Length == 0 || ResultCount[0] <= 0)
                return;

            ScanResultDTO result = Results[0];
            ScanProgressDTO progress = Progress[0];
            progress.TargetHashID = result.EntityHash;
            progress.ScannerAUP = Input.RayOriginAUP;
            progress.LastFrame = Input.Frame;
            progress.Flags |= ScannerDataMiningRouter.ScanProgressFlagActive;
            Progress[0] = progress;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ScannerSpatialQueryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ScannerSpatialEntityDTO> Entities;
        [ReadOnly, NoAlias] public NativeArray<ScannableEntityMetadataDTO> Metadata;
        [ReadOnly, NoAlias] public NativeArray<MockSdfOcclusionZoneDTO> OcclusionZones;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
        [WriteOnly, NoAlias]
        public NativeArray<ScanResultDTO> Results;
        [WriteOnly, NoAlias]
        public NativeArray<int> ResultCount;
        [WriteOnly, NoAlias]
        public NativeArray<ScannerQueryStatsDTO> QueryStats;
        public MockScannerInputSignal Input;
        public ScannerSettingsDTO Settings;
        public int EntityCount;
        public int MetadataCount;
        public int OcclusionZoneCount;

        public void Execute()
        {
            if (ResultCount.IsCreated && ResultCount.Length > 0)
                ResultCount[0] = 0;
            if (Results.IsCreated && Results.Length > 0)
                Results[0] = default;

            ScannerQueryStatsDTO stats = default;
            stats.BestEntityIndex = -1;
            float3 direction = math.normalizesafe(Input.RayDirection, new float3(0f, 0f, 1f));
            bool invalidInput = (Input.Flags & 1u) == 0u ||
                                !math.all(math.isfinite(Input.RayOriginAUP)) ||
                                !math.all(math.isfinite(direction)) ||
                                !math.isfinite(Input.MaxDistance) ||
                                !BucketHeads.IsCreated ||
                                !BucketNext.IsCreated ||
                                BucketHeads.Length == 0;
            if (invalidInput)
            {
                stats.Flags = ScannerDataMiningRouter.QueryFlagNaNInput | ScannerDataMiningRouter.QueryFlagNoCandidate;
                WriteStats(stats);
                return;
            }

            float maxDistance = math.max(0.1f, Input.MaxDistance);
            float cellSize = math.max(1f, Settings.CellSizeMeters);
            int maxCells = math.clamp(Settings.MaxCandidateCells, 1, 81);
            int maxPerCell = math.clamp(Settings.MaxCandidatesPerCell, 1, 64);
            float beamRadius = math.max(0.01f, Input.BeamRadius);
            float minDot = math.saturate(Settings.BeamMinDot);
            float bestScore = float.MaxValue;
            ScanResultDTO best = default;
            int bestIndex = -1;
            double3 origin = Input.RayOriginAUP;

            int visitedCells = 0;
            for (int anchorIndex = 0; anchorIndex < 3 && visitedCells < maxCells; anchorIndex++)
            {
                double distanceScale = anchorIndex == 0 ? 0d : anchorIndex == 1 ? 0.5d : 1d;
                double3 anchor = origin + new double3(direction * (float)(maxDistance * distanceScale));
                int3 cell = new int3(
                    (int)math.floor(anchor.x / cellSize),
                    (int)math.floor(anchor.y / cellSize),
                    (int)math.floor(anchor.z / cellSize));

                for (int z = -1; z <= 1 && visitedCells < maxCells; z++)
                for (int y = -1; y <= 1 && visitedCells < maxCells; y++)
                for (int x = -1; x <= 1 && visitedCells < maxCells; x++)
                {
                    visitedCells++;
                    int key = ScannerSpatialHash.HashCell(cell + new int3(x, y, z));
                    int bucket = ScannerSpatialHash.BucketIndex(key, BucketHeads.Length);
                    int entityIndex = BucketHeads[bucket];
                    int perCell = 0;
                    while (entityIndex >= 0)
                    {
                        if (perCell++ >= maxPerCell)
                            break;
                        int currentIndex = entityIndex;
                        if ((uint)currentIndex >= (uint)EntityCount ||
                            (uint)currentIndex >= (uint)Entities.Length ||
                            (uint)currentIndex >= (uint)BucketNext.Length)
                        {
                            break;
                        }

                        entityIndex = BucketNext[currentIndex];
                        stats.CandidateCount++;
                        ScannerSpatialEntityDTO entity = Entities[currentIndex];
                        if (entity.EntityHash == 0u)
                            continue;

                        if (!PassesToolRequirement(in entity))
                            continue;

                        float radius = math.max(0.01f, entity.SphereRadius);
                        if (!ScannerSpatialHash.TryRaySphere(origin, direction, entity.AUP, radius + beamRadius, maxDistance, out float hitDistance, out float frontDot))
                            continue;

                        if (frontDot < minDot)
                            continue;

                        if (IsOccludedByDearLieSdf(origin, direction, hitDistance, radius))
                        {
                            stats.OccludedCount++;
                            stats.Flags |= ScannerDataMiningRouter.QueryFlagOccluded;
                            continue;
                        }

                        float normalizedDistance = hitDistance / math.max(0.001f, maxDistance);
                        float score = hitDistance + (1f - frontDot) * Settings.BeamMagnetism + normalizedDistance * 0.25f;
                        if (score >= bestScore)
                            continue;

                        bestScore = score;
                        bestIndex = currentIndex;
                        best = new ScanResultDTO
                        {
                            AUP = entity.AUP,
                            EntityHash = entity.EntityHash,
                            Distance = hitDistance,
                            ScanProgress = 0f,
                            _pad0 = 0u,
                            _pad1 = 0UL
                        };
                    }
                }
            }

            stats.CellProbeCount = (uint)visitedCells;
            stats.BestEntityIndex = bestIndex;
            stats.BestScore = bestScore == float.MaxValue ? 0f : bestScore;
            stats.BestHash = best.EntityHash;
            stats.EstimatedMicroseconds = (uint)math.min(ushort.MaxValue, stats.CellProbeCount + (uint)stats.CandidateCount * 2u);

            if (best.EntityHash != 0u && Results.IsCreated && Results.Length > 0)
            {
                Results[0] = best;
                if (ResultCount.IsCreated && ResultCount.Length > 0)
                    ResultCount[0] = 1;
            }
            else
            {
                stats.Flags |= ScannerDataMiningRouter.QueryFlagNoCandidate;
            }

            WriteStats(stats);
        }

        private bool PassesToolRequirement(in ScannerSpatialEntityDTO entity)
        {
            if (entity.MetadataIndex < (uint)MetadataCount && entity.MetadataIndex < (uint)Metadata.Length)
            {
                ScannableEntityMetadataDTO metadata = Metadata[(int)entity.MetadataIndex];
                if (metadata.RequiredToolLevel > Input.ToolLevel)
                    return false;
            }

            return true;
        }

        private bool IsOccludedByDearLieSdf(double3 origin, float3 direction, float hitDistance, float targetRadius)
        {
            int count = math.min(math.max(0, OcclusionZoneCount), OcclusionZones.Length);
            if (count <= 0)
                return false;

            double3 midpoint = origin + new double3(direction * (hitDistance * 0.5f));
            for (int i = 0; i < count; i++)
            {
                MockSdfOcclusionZoneDTO zone = OcclusionZones[i];
                if ((zone.Flags & 1u) == 0u || zone.Radius <= 0f)
                    continue;

                float3 delta = AupPrecisionMath.LocalDeltaFloat3(midpoint, zone.CenterAUP, float3.zero);
                float sdf = math.length(delta) - zone.Radius + Settings.SdfMidpointClearance + targetRadius * 0.05f;
                if (sdf < 0f)
                    return true;
            }

            return false;
        }

        private void WriteStats(in ScannerQueryStatsDTO stats)
        {
            if (QueryStats.IsCreated && QueryStats.Length > 0)
                QueryStats[0] = stats;
        }
    }

    public static class ScannerShaderGlobals
    {
        private static readonly int ScannerHudParamsId = Shader.PropertyToID("_H8ScannerHudParams");
        private static readonly int ScannerHudHashId = Shader.PropertyToID("_H8ScannerHudHash");

        public static void Publish(in ScannerVfxDTO vfx, float globalQualityWeight, float pressure01)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float pressure = math.saturate(math.isfinite(pressure01) ? pressure01 : 0f);
            float qualityCurve = quality * quality * (3f - 2f * quality);
            float refreshHz = math.lerp(5f, 60f, qualityCurve);
            float ditherComplexity = math.lerp(1f, 8f, qualityCurve) * math.lerp(1f, 0.35f, pressure);
            Shader.SetGlobalVector(
                ScannerHudParamsId,
                new Vector4(math.saturate(vfx.ScanProgress), quality, refreshHz, ditherComplexity));
            Shader.SetGlobalVector(
                ScannerHudHashId,
                new Vector4(vfx.TargetHash, vfx.Flags, vfx.BeamScore, vfx.HitDistance));
        }
    }
}
