using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct ScanResultDTO
    {
        public double3 AUP;
        public uint EntityHash;
        public float Distance;
        public float ScanProgress;
        public uint _pad0;
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ScannableEntityMetadataDTO
    {
        public uint EntityHash;
        public float ScanDuration;
        public uint RequiredToolLevel;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ScannerSpatialEntityDTO
    {
        public double3 AUP;
        public long SectorHash;
        public ulong DepletionMask;
        public uint EntityHash;
        public float SphereRadius;
        public uint MetadataIndex;
        public uint Flags;
        public uint DepletionWordIndex;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ScannerVfxDTO
    {
        public float3 HitAUP;
        public float HitDistance;
        public float ScanProgress;
        public uint TargetHash;
        public uint Flags;
        public float BeamScore;
    }

    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct ActiveScanStateDTO
    {
        public double3 TargetAUP;
        public double3 LastOriginAUP;
        public long SectorHash;
        public ulong DepletionMask;
        public ulong _pad0;
        public ulong _pad1;
        public uint TargetHash;
        public float Progress01;
        public float ScanDurationSeconds;
        public float HoldSeconds;
        public uint LastFrame;
        public uint Flags;
        public uint CompletedHash;
        public int BestEntityIndex;
        public uint DepletionWordIndex;
        public uint MetadataFlags;
        public float HitDistance;
        public float BeamScore;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockScannerInputSignal
    {
        public double3 RayOriginAUP;
        public float3 RayDirection;
        public float MaxDistance;
        public float DeltaTime;
        public float BeamRadius;
        public uint ToolHash;
        public uint Frame;
        public uint ToolLevel;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockToolTransformSignal
    {
        public double3 PositionAUP;
        public float3 ForwardVector;
        public float MaxDistance;
        public uint ToolHash;
        public uint Frame;
        public uint Flags;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MockSdfOcclusionZoneDTO
    {
        public double3 CenterAUP;
        public float Radius;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ScannerQueryStatsDTO
    {
        public int CandidateCount;
        public int BestEntityIndex;
        public float BestScore;
        public uint BestHash;
        public uint Flags;
        public uint EstimatedMicroseconds;
        public uint CellProbeCount;
        public uint OccludedCount;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ScannerTelemetryEntry
    {
        public double3 TargetAUP;
        public ulong _pad0;
        public uint Frame;
        public uint TargetHash;
        public uint Flags;
        public uint CandidateCount;
        public uint CompletedCount;
        public uint EstimatedMicroseconds;
        public float Progress01;
        public float HitDistance;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct ScannerSettingsDTO
    {
        public float CellSizeMeters;
        public float MaxDistanceMeters;
        public float BeamRadiusMeters;
        public float BeamMinDot;
        public float BeamMagnetism;
        public float ProgressDecayRate;
        public float QueryBudgetMicroseconds;
        public float AcousticIntensity01;
        public float LowTierProgressMultiplier;
        public float HighTierVfxBias;
        public float SdfMidpointClearance;
        public float ScanDurationFallback;
        public int LowTierCadenceFrames;
        public int MidTierCadenceFrames;
        public int HighTierCadenceFrames;
        public int UltraTierCadenceFrames;
        public int MaxCandidateCells;
        public int MaxCandidatesPerCell;
        public int MaxResults;
        public int Flags;
    }

    public struct MockSpatialHashGrid
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockToolTransformSignalJob : IJob
    {
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
    public sealed class ScannerDataMiningRouter : MonoBehaviour, IFastTickable, ISlowTickable, ILateFrameTickable
    {
        public const uint MetadataToolLevelMask = 0x000000FFu;
        public const uint MetadataFlagDepletable = 1u << 8;
        public const uint MetadataFlagFlora = 1u << 9;
        public const uint MetadataFlagFauna = 1u << 10;
        public const uint MetadataFlagDataNode = 1u << 11;
        public const uint MetadataFlagScarcityNode = 1u << 12;
        public const uint StateFlagHasTarget = 1u << 0;
        public const uint StateFlagCompletedThisFrame = 1u << 1;
        public const uint StateFlagOccluded = 1u << 2;
        public const uint StateFlagLowTier = 1u << 3;
        public const uint VfxFlagHasTarget = 1u << 0;
        public const uint VfxFlagOccluded = 1u << 1;
        public const uint VfxFlagLowTier = 1u << 2;
        public const uint QueryFlagNoCandidate = 1u << 0;
        public const uint QueryFlagNaNInput = 1u << 1;
        public const uint QueryFlagOccluded = 1u << 2;
        public const int BlackBoxCapacity = 300;
        public const int DefaultEntityCapacity = 128;
        public const int DefaultSpatialBucketCapacity = 256;
        public const int DefaultResultCapacity = 4;
        public const float DefaultCellSizeMeters = 16f;
        public const string DumpFileName = "Dump_SHINOBU_24.bin";
        public const string H8DumpFileName = "Dump_SHINOBU_24.h8dump";

        private const SystemID OwnerSystemId = SystemID.GameplayTools;
        private const byte ToolAcousticStateScanner = 2;
        private const uint ScannerToolHash = H8Hashes.Items.HydroacousticScannerHash;
        private const uint ScannerAnomalyHash = 0x53434E41u; // SCNA
        private const uint ScannerDumpReasonHash = 0x53444D50u; // SDMP

        [SerializeField] private bool scanActive = true;
        [SerializeField] private bool seedMockData = true;
        [SerializeField] private int entityCapacity = DefaultEntityCapacity;
        [SerializeField] private int mockEntityCount = 48;
        [SerializeField] private float maxDistanceMeters = 64f;
        [SerializeField] private float beamRadiusMeters = 1.35f;
        [SerializeField] private uint toolLevel = 1u;

        private IDataVault _dataVault;
        private VaultBufferHandle<ScannerSpatialEntityDTO> _entitiesHandle;
        private VaultBufferHandle<ScannableEntityMetadataDTO> _metadataHandle;
        private VaultBufferHandle<MockSdfOcclusionZoneDTO> _occlusionZonesHandle;
        private VaultBufferHandle<int> _bucketHeadsHandle;
        private VaultBufferHandle<int> _bucketNextHandle;
        private VaultBufferHandle<ScanResultDTO> _scanResultsHandle;
        private VaultBufferHandle<int> _resultCountHandle;
        private VaultBufferHandle<ActiveScanStateDTO> _activeStateHandle;
        private VaultBufferHandle<ScannerVfxDTO> _vfxTargetHandle;
        private VaultBufferHandle<ScannerQueryStatsDTO> _queryStatsHandle;
        private VaultBufferHandle<ScannerTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<ScannerSettingsDTO> _settingsHandle;
        private JobHandle _queryHandle;
        private MockScannerInputSignal _lastInput;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private HectonPlayerMovement _cachedPlayerMovement;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private float _cachedSystemPressure01;
        private int _lastQueryFrame = -1024;
        private int _entityCount;
        private int _telemetryCursor;
        private uint _completionCount;
        private bool _queryScheduled;
        private bool _queryBuffersLocked;
        private bool _registeredFast;
        private bool _registeredSlow;
        private bool _registeredLate;

        private static ScannerVfxDTO s_lastVfxTarget;
        private static uint s_lastVfxFrame;

        private struct ScannerVaultViews
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
            public NativeArray<ScannerSettingsDTO> Settings;

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
                Settings.IsCreated;
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

        public static bool TryReadVaultSettings(out ScannerSettingsDTO settings)
        {
            settings = ScannerDataMiningTuning.Settings;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.ShinobuScannerSettings, out VaultBufferHandle<ScannerSettingsDTO> handle) ||
                !handle.IsCreated)
            {
                return false;
            }

            NativeArray<ScannerSettingsDTO> buffer = handle.Resolve(vault);
            if (!buffer.IsCreated || buffer.Length == 0)
                return false;

            settings = buffer[0];
            return settings.CellSizeMeters > 0f && math.isfinite(settings.CellSizeMeters);
        }

        public static bool TryWriteVaultSettings(in ScannerSettingsDTO settings)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            VaultBufferHandle<ScannerSettingsDTO> handle = vault.GetBufferHandle<ScannerSettingsDTO>(
                BufferID.ShinobuScannerSettings,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            NativeArray<ScannerSettingsDTO> buffer = handle.Resolve(vault);
            if (!buffer.IsCreated || buffer.Length == 0)
                return false;

            buffer[0] = settings;
            return true;
        }

        public void SetScanActive(bool active)
        {
            scanActive = active;
        }

        private void OnEnable()
        {
            if (!EnsureVaultState())
                return;

            CachePlayerRuntimeContextCold();
            if (seedMockData)
                SeedMockGridFromTransform();

            _cachedQualityTier = GlobalRegistry.ScalabilityTier;
            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void OnDisable()
        {
            CompleteScheduledQuery(forceComplete: true);
            UnlockQueryBuffers();

            if (_registeredFast)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);

            _registeredFast = false;
            _registeredSlow = false;
            _registeredLate = false;
            ReleaseHandlesOnly();
        }

        public void FastTick(float deltaTime)
        {
            if (!TryResolveVaultViews(out ScannerVaultViews views) || !views.HasCoreBuffers)
                return;

            if (_queryScheduled)
                return;

            ScannerSettingsDTO settings = ResolveCurrentSettings(views.Settings);
            settings.MaxDistanceMeters = math.max(0.1f, maxDistanceMeters > 0f ? maxDistanceMeters : settings.MaxDistanceMeters);
            settings.BeamRadiusMeters = math.max(0.05f, beamRadiusMeters > 0f ? beamRadiusMeters : settings.BeamRadiusMeters);

            int frame = math.max(0, Time.frameCount);
            int cadence = ResolveQueryCadenceFrames(_cachedQualityTier, _cachedSystemPressure01, in settings);
            if (frame - _lastQueryFrame < cadence)
                return;

            _lastQueryFrame = frame;
            _lastInput = BuildInputSignal(deltaTime, frame, in settings);
            views.ResultCount[0] = 0;
            if (views.ScanResults.Length > 0)
                views.ScanResults[0] = default;
            if (!TryLockQueryBuffers(_dataVault))
                return;

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
        }

        public void LateFrameTick()
        {
            if (!_queryScheduled)
                return;

            if (!TryFinalizeScheduledQuery())
                return;

            ProcessCompletedQuery(_lastInput.DeltaTime);
        }

        public void SlowTick()
        {
            if (_cachedPlayerContext == null || !_cachedPlayerContext.IsInitialized)
                CachePlayerRuntimeContextCold();
            else if (_cachedPlayerMovement == null)
                _cachedPlayerMovement = _cachedPlayerContext.PlayerMovement;

            _cachedQualityTier = GlobalRegistry.ScalabilityTier;
            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (healthSignals.Length > 0)
                _cachedSystemPressure01 = math.saturate(healthSignals[healthSignals.Length - 1].Pressure01);
        }

        private bool TryFinalizeScheduledQuery()
        {
            if (!_queryScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _queryHandle))
                return false;

            _queryScheduled = false;
            UnlockQueryBuffers();
            return true;
        }

        private void CompleteScheduledQuery(bool forceComplete)
        {
            if (!_queryScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _queryHandle, forceComplete))
                return;

            _queryScheduled = false;
            UnlockQueryBuffers();
        }

        private MockScannerInputSignal BuildInputSignal(float deltaTime, int frame, in ScannerSettingsDTO settings)
        {
            Vector3 forward = transform.forward;
            float3 direction = math.normalizesafe(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f));
            bool hasOrigin = TryResolveToolOriginAup(out double3 origin);
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
                Flags = scanActive && hasOrigin ? 1u : 0u
            };
        }

        private void CachePlayerRuntimeContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedPlayerMovement = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerMovement : null;
        }

        private bool TryResolveToolOriginAup(out double3 originAup)
        {
            originAup = default;

            Vector3 toolPosition = transform.position;
            float3 toolRuntime = new float3(toolPosition.x, toolPosition.y, toolPosition.z);
            if (!math.all(math.isfinite(toolRuntime)))
                return false;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    MathGuard.IsFinite(in snapshot.Aup) &&
                    math.all(math.isfinite(snapshot.RuntimePosition)))
                {
                    return TryOffsetToolOriginFromObserver(
                        toolRuntime,
                        snapshot.RuntimePosition,
                        in snapshot.Aup,
                        out originAup);
                }

                HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                    _cachedPlayerMovement = playerMovement;
            }

            HectonPlayerMovement cachedPlayerMovement = _cachedPlayerMovement;
            if (cachedPlayerMovement == null)
                return false;

            AbsoluteUniversePosition playerAup = cachedPlayerMovement.CurrentAup;
            if (!MathGuard.IsFinite(in playerAup))
                return false;

            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(playerRuntime)))
                return false;

            return TryOffsetToolOriginFromObserver(
                toolRuntime,
                playerRuntime,
                in playerAup,
                out originAup);
        }

        private static bool TryOffsetToolOriginFromObserver(
            float3 toolRuntime,
            float3 observerRuntime,
            in AbsoluteUniversePosition observerAup,
            out double3 originAup)
        {
            originAup = default;
            double3 localDelta = new double3(
                (double)toolRuntime.x - observerRuntime.x,
                (double)toolRuntime.y - observerRuntime.y,
                (double)toolRuntime.z - observerRuntime.z);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            AbsoluteUniversePosition toolAup = AbsoluteUniversePosition.OffsetMeters(in observerAup, localDelta);
            if (!MathGuard.IsFinite(in toolAup))
                return false;

            originAup = toolAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAup));
        }

        private void ProcessCompletedQuery(float deltaTime)
        {
            if (!TryResolveVaultViews(out ScannerVaultViews views) || !views.HasCoreBuffers)
                return;

            ScannerSettingsDTO settings = ResolveCurrentSettings(views.Settings);
            ScannerQueryStatsDTO stats = views.QueryStats.Length > 0 ? views.QueryStats[0] : default;
            int resultCount = views.ResultCount.Length > 0 ? views.ResultCount[0] : 0;

            unsafe
            {
                ref ActiveScanStateDTO state = ref GetActiveStateRef(views.ActiveState);
                if (resultCount > 0 && views.ScanResults.Length > 0)
                {
                    ScanResultDTO result = views.ScanResults[0];
                    float scanDuration = ResolveScanDuration(stats.BestEntityIndex, views.Entities, views.Metadata, in settings);
                    if (_cachedQualityTier == HectonQualityTier.Unknown ||
                        _cachedQualityTier == HectonQualityTier.Low ||
                        _cachedQualityTier == HectonQualityTier.Mx350)
                    {
                        state.Flags |= StateFlagLowTier;
                    }
                    else
                    {
                        state.Flags &= ~StateFlagLowTier;
                    }

                    ScannerScanProgression.Solve(ref state, ref result, stats.BestEntityIndex, scanDuration, deltaTime, in settings);
                    state.LastOriginAUP = _lastInput.RayOriginAUP;
                    state.BeamScore = stats.BestScore;
                    if ((stats.Flags & QueryFlagOccluded) != 0u)
                        state.Flags |= StateFlagOccluded;
                    CopyEntityStateToActive(ref state, stats.BestEntityIndex, views.Entities);
                    views.ScanResults[0] = result;
                    WriteVfxTarget(in result, in state, in stats, views.VfxTarget);
                    RouteProgressSignals(in result, in state, in settings);
                    RouteCompletionIfNeeded(in result, ref state, in settings);
                    WriteTelemetry(in result, in state, in stats, views.Telemetry);
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
                DumpTelemetryRing(views.Telemetry);
                PublishDumpAnomaly(stats.EstimatedMicroseconds);
            }
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

            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = ScannerToolHash,
                TargetHash = result.EntityHash,
                Progress01 = math.saturate(result.ScanProgress),
                PitchScale = 0.9f + math.saturate(result.ScanProgress) * 0.25f,
                Intensity01 = math.saturate(settings.AcousticIntensity01),
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                State = ToolAcousticStateScanner,
                Flags = 0
            });
        }

        private void RouteCompletionIfNeeded(in ScanResultDTO result, ref ActiveScanStateDTO state, in ScannerSettingsDTO settings)
        {
            if ((state.Flags & StateFlagCompletedThisFrame) == 0u || result.EntityHash == 0u)
                return;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(result.AUP);
            uint frame = unchecked((uint)math.max(0, Time.frameCount));
            SignalBus<EncyclopediaUnlockSignal>.Push(new EncyclopediaUnlockSignal
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
            });
            GlobalSignals.Publish(new ScanCompleteSignal
            {
                PositionAup = aup,
                EntryHash = result.EntityHash,
                ScanId = result.EntityHash,
                SourceId = ScannerToolHash,
                ReconKind = (byte)ScanEntryKind.Scannable,
                Flags = 0
            });
            ScanEvents.RaiseEntryDiscovered(result.EntityHash, result.EntityHash, 0u, 0u, ScanEntryKind.Scannable);

            if ((state.MetadataFlags & MetadataFlagDepletable) != 0u)
            {
                SignalBus<EntityDepletedSignal>.Push(new EntityDepletedSignal
                {
                    EntityHash = result.EntityHash,
                    SourceHash = ScannerToolHash,
                    Frame = frame,
                    WordIndex = (ushort)math.min(ushort.MaxValue, state.DepletionWordIndex),
                    Operation = 1,
                    Flags = 0,
                    SectorHash = state.SectorHash,
                    DepletionMask = state.DepletionMask
                });
                GlobalSignals.Publish(new ResourceDepletionDeltaSignal
                {
                    SectorHash = state.SectorHash,
                    DepletionMask = state.DepletionMask,
                    OreHash = result.EntityHash,
                    Frame = frame,
                    WordIndex = (ushort)math.min(ushort.MaxValue, state.DepletionWordIndex),
                    Operation = 1,
                    Flags = 0
                });
            }

            GlobalSignals.Publish(new AcousticPingSignal
            {
                PositionAup = aup,
                RadiusMeters = math.max(1f, result.Distance * 0.25f),
                Intensity01 = math.saturate(settings.AcousticIntensity01),
                SourceId = ScannerToolHash,
                Channel = AcousticPingSignal.ChannelActiveSonar,
                Flags = AcousticPingSignal.FlagActiveSonar
            });

            _completionCount++;
            state.Flags &= ~StateFlagCompletedThisFrame;
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
            if ((state.Flags & StateFlagLowTier) != 0u)
                vfx.Flags |= VfxFlagLowTier;
            if ((state.Flags & StateFlagOccluded) != 0u)
                vfx.Flags |= VfxFlagOccluded;

            vfxTarget[0] = vfx;
            s_lastVfxTarget = vfx;
            s_lastVfxFrame = unchecked((uint)math.max(0, Time.frameCount));
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
            vfx.Flags = (state.Flags & StateFlagLowTier) != 0u ? VfxFlagLowTier : 0u;
            vfx.BeamScore = stats.BestScore;
            vfxTarget[0] = vfx;
            s_lastVfxTarget = vfx;
            s_lastVfxFrame = unchecked((uint)math.max(0, Time.frameCount));
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
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
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
            GlobalSignals.Publish(new AnomalySignal
            {
                SystemHash = ScannerToolHash,
                AnomalyHash = ScannerDumpReasonHash,
                Scalar = scalar,
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                Severity = 2,
                Flags = 0
            });

            GlobalSignals.Publish(new CrashTelemetrySignal
            {
                SystemHash = ScannerToolHash,
                ReasonHash = ScannerAnomalyHash,
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                ExitCode = 0,
                NativeAllocationCount = 0,
                NativeTrackedBytesMb = 0f,
                Severity = 1,
                Flags = 0
            });
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
            {
                ReleaseHandlesOnly();
                return false;
            }

            _dataVault = vault;
            int safeEntityCapacity = math.clamp(entityCapacity, 8, 4096);
            int safeBucketCapacity = ResolveSpatialBucketCapacity(safeEntityCapacity);
            int safeResultCapacity = math.clamp(ScannerDataMiningTuning.Settings.MaxResults, 1, 16);

            _entitiesHandle = vault.GetBufferHandle<ScannerSpatialEntityDTO>(
                BufferID.ShinobuScannerEntities,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _metadataHandle = vault.GetBufferHandle<ScannableEntityMetadataDTO>(
                BufferID.ShinobuScannerMetadata,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _occlusionZonesHandle = vault.GetBufferHandle<MockSdfOcclusionZoneDTO>(
                BufferID.ShinobuScannerOcclusionZones,
                8,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _bucketHeadsHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuScannerSpatialBucketHeads,
                safeBucketCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _bucketNextHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuScannerSpatialNext,
                safeEntityCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _scanResultsHandle = vault.GetBufferHandle<ScanResultDTO>(
                BufferID.ShinobuScannerScanResults,
                safeResultCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _resultCountHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuScannerResultCount,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _activeStateHandle = vault.GetBufferHandle<ActiveScanStateDTO>(
                BufferID.ShinobuScannerActiveState,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _vfxTargetHandle = vault.GetBufferHandle<ScannerVfxDTO>(
                BufferID.ShinobuScannerVfxTarget,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _queryStatsHandle = vault.GetBufferHandle<ScannerQueryStatsDTO>(
                BufferID.ShinobuScannerQueryStats,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.GetBufferHandle<ScannerTelemetryEntry>(
                BufferID.ShinobuScannerTelemetryRing,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _settingsHandle = vault.GetBufferHandle<ScannerSettingsDTO>(
                BufferID.ShinobuScannerSettings,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!TryResolveVaultViews(out ScannerVaultViews views) || !views.HasCoreBuffers)
            {
                ReleaseHandlesOnly();
                return false;
            }

            ScannerSpatialHash.ClearBuckets(views.BucketHeads, views.BucketNext);
            if (views.Settings[0].CellSizeMeters <= 0f)
                views.Settings[0] = ScannerDataMiningTuning.Settings;
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

        private bool TryResolveVaultViews(out ScannerVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _dataVault = vault;
            views.Entities = _entitiesHandle.Resolve(vault);
            views.Metadata = _metadataHandle.Resolve(vault);
            views.OcclusionZones = _occlusionZonesHandle.Resolve(vault);
            views.BucketHeads = _bucketHeadsHandle.Resolve(vault);
            views.BucketNext = _bucketNextHandle.Resolve(vault);
            views.ScanResults = _scanResultsHandle.Resolve(vault);
            views.ResultCount = _resultCountHandle.Resolve(vault);
            views.ActiveState = _activeStateHandle.Resolve(vault);
            views.VfxTarget = _vfxTargetHandle.Resolve(vault);
            views.QueryStats = _queryStatsHandle.Resolve(vault);
            views.Telemetry = _telemetryHandle.Resolve(vault);
            views.Settings = _settingsHandle.Resolve(vault);
            return views.HasCoreBuffers;
        }

        private void ReleaseHandlesOnly()
        {
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
            _dataVault = null;
            _entityCount = 0;
            _telemetryCursor = 0;
            _completionCount = 0u;
        }

        private bool TryLockQueryBuffers(IDataVault vault)
        {
            if (vault == null || _queryBuffersLocked)
                return false;

            if (!vault.TryLockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId))
                return false;
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerSpatialNext, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerScanResults, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialNext, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerResultCount, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerScanResults, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialNext, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }
            if (!vault.TryLockBuffer(BufferID.ShinobuScannerQueryStats, OwnerSystemId))
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerResultCount, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerScanResults, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialNext, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
                return false;
            }

            _queryBuffersLocked = true;
            return true;
        }

        private void UnlockQueryBuffers()
        {
            if (!_queryBuffersLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScannerQueryStats, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerResultCount, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerScanResults, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialNext, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerSpatialBucketHeads, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerOcclusionZones, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerMetadata, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.ShinobuScannerEntities, OwnerSystemId);
            }

            _queryBuffersLocked = false;
        }

        private ScannerSettingsDTO ResolveCurrentSettings(NativeArray<ScannerSettingsDTO> settingsBuffer)
        {
            if (settingsBuffer.IsCreated && settingsBuffer.Length > 0)
            {
                ScannerSettingsDTO settings = settingsBuffer[0];
                if (settings.CellSizeMeters > 0f && math.isfinite(settings.CellSizeMeters))
                    return settings;
            }

            return ScannerDataMiningTuning.Settings;
        }

        private void SeedMockGridFromTransform()
        {
            if (!TryResolveVaultViews(out ScannerVaultViews views) || !views.HasCoreBuffers)
                return;

            int count = math.clamp(mockEntityCount, 1, views.Entities.Length);
            if (!TryResolveToolOriginAup(out double3 origin))
                return;

            float3 forward = math.normalizesafe(new float3(transform.forward.x, transform.forward.y, transform.forward.z), new float3(0f, 0f, 1f));
            float3 right = math.normalizesafe(new float3(transform.right.x, transform.right.y, transform.right.z), new float3(1f, 0f, 0f));
            ScannerSettingsDTO settings = ResolveCurrentSettings(views.Settings);
            FillMockSpatialHash(
                views.BucketHeads,
                views.BucketNext,
                views.Entities,
                views.Metadata,
                origin,
                forward,
                right,
                settings.CellSizeMeters,
                count);
            _entityCount = count;
            if (views.OcclusionZones.IsCreated && views.OcclusionZones.Length > 0)
            {
                views.OcclusionZones[0] = new MockSdfOcclusionZoneDTO
                {
                    CenterAUP = origin + new double3(right * 11f + forward * 32f),
                    Radius = 2.5f,
                    Flags = 1u
                };
            }
        }

        private void DumpTelemetryRing()
        {
            if (!TryResolveVaultViews(out ScannerVaultViews views) || !views.Telemetry.IsCreated)
                return;

            DumpTelemetryRing(views.Telemetry);
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

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            int byteCount = UnsafeUtility.SizeOf<ScannerTelemetryEntry>() * telemetry.Length;
            byte[] payload = new byte[byteCount];
            fixed (byte* destination = payload)
            {
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                UnsafeUtility.MemCpy(destination, source, byteCount);
            }

            File.WriteAllBytes(path, payload);
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

        public static int ResolveQueryCadenceFrames(HectonQualityTier tier, float pressure01, in ScannerSettingsDTO settings)
        {
            int cadence = tier == HectonQualityTier.Ultra ? settings.UltraTierCadenceFrames :
                tier == HectonQualityTier.High ? settings.HighTierCadenceFrames :
                tier == HectonQualityTier.Mid ? settings.MidTierCadenceFrames :
                settings.LowTierCadenceFrames;

            if (pressure01 > 0.8f)
                cadence <<= 1;

            return math.clamp(cadence, 1, 16);
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
            if (!bucketHeads.IsCreated || !bucketNext.IsCreated || !entities.IsCreated || !metadata.IsCreated)
                return;

            ScannerSpatialHash.ClearBuckets(bucketHeads, bucketNext);
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
            }
        }

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
            float multiplier = (state.Flags & ScannerDataMiningRouter.StateFlagLowTier) != 0u
                ? math.max(0.1f, settings.LowTierProgressMultiplier)
                : 1f;
            state.Progress01 = math.saturate(state.Progress01 + math.max(0f, deltaTime) * multiplier / state.ScanDurationSeconds);
            state.LastFrame = unchecked((uint)math.max(0, Time.frameCount));
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
                uint lowTier = state.Flags & ScannerDataMiningRouter.StateFlagLowTier;
                ScannerDataMiningRouter.ResetActiveState(ref state);
                state.Flags = lowTier;
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ScannerSpatialQueryJob : IJob
    {
        [ReadOnly] public NativeArray<ScannerSpatialEntityDTO> Entities;
        [ReadOnly] public NativeArray<ScannableEntityMetadataDTO> Metadata;
        [ReadOnly] public NativeArray<MockSdfOcclusionZoneDTO> OcclusionZones;
        [ReadOnly] public NativeArray<int> BucketHeads;
        [ReadOnly] public NativeArray<int> BucketNext;
        public NativeArray<ScanResultDTO> Results;
        public NativeArray<int> ResultCount;
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
}

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EncyclopediaUnlockSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ScanId;
        [FieldOffset(16)] public byte Kind;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] public ushort RequiredToolLevel;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EntityDepletedSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort WordIndex;
        [FieldOffset(14)] public byte Operation;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(16)] public long SectorHash;
        [FieldOffset(24)] public ulong DepletionMask;
    }
}
