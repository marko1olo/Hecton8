using System;
using System.Buffers.Binary;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;
using StructuralIntegrityStateDTO = Hecton8.Habitat.Deformation.IntegrityStateDTO;

namespace Hecton8.Construction
{
    public sealed unsafe partial class BulkheadContainmentRuntime
    {
        private static readonly int GlobalHatchLockStatesId = Shader.PropertyToID("_GlobalHatchLockStates");
        private static readonly int GlobalHatchLockParamsId = Shader.PropertyToID("_GlobalHatchLockParams");
        private const ulong HatchTelemetryDumpMutationGuardMask =
            (1UL << ((int)BufferID.Shinobu343HatchTelemetryRing & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchTelemetryCursor & 31));

        [SerializeField, Range(0.05f, 3f)] private float safePressureDifferentialATM = HatchLockConstants.DefaultSafePressureDifferentialATM;
        [SerializeField, Range(0.01f, 0.95f)] private float structuralJamThreshold01 = HatchLockConstants.DefaultStructuralJamThreshold01;
        [SerializeField, Range(0.1f, 5f)] private float catastrophicPressureDifferentialATM = HatchLockConstants.DefaultCatastrophicPressureDifferentialATM;
        [SerializeField] private bool generateMockHatchPressure;
        [SerializeField] private bool uploadHatchShaderBuffer = true;

        private VaultGenerationHandle<HatchStateDTO> _hatchStatesHandle;
        private VaultGenerationHandle<HatchTelemetryEntry> _hatchTelemetryHandle;
        private VaultGenerationHandle<uint> _hatchTelemetryCursorHandle;
        private VaultGenerationHandle<HatchTuningDTO> _hatchTuningHandle;
        private VaultGenerationHandle<HatchHardwareProfileDTO> _hatchProfilesHandle;
        private VaultGenerationHandle<byte> _hatchCsvScratchHandle;
        private VaultGenerationHandle<FluidCompartmentDTO> _hatchMockFluidCompartmentsHandle;
        private VaultGenerationHandle<FluidCompartmentDTO> _hatchFluidCompartmentsHandle;
        private VaultGenerationHandle<StructuralIntegrityStateDTO> _hatchStructuralStatesHandle;
        private bool _hatchLayoutChecked;
        private bool _hatchLayoutValid;
        private bool _hatchDefaultsInitialized;
        private bool _hatchProfileCsvLoaded;
        private bool _hatchProfileCsvLoadAttempted;
        private bool _hatchShaderUploadDirty = true;
        private int _hatchProfileRowCount;
        private int _hatchActiveCount;
        private float _hatchPressureAccumulator;
        private float _lastHatchScheduleMicroseconds;
        private uint _lastHatchTelemetryFrame;
        private uint _lastHatchPressureLockedCount;
        private uint _lastHatchJammedCount;
        private uint _lastHatchCatastrophicCount;
        private float _lastHatchMaxPressureDifferentialATM;
        private float _lastHatchAveragePressureDifferentialATM;
        private uint _lastHatchDumpedTelemetryCursor;
        private uint _lastHatchDumpAttemptTelemetryCursor;
        private uint _lastHatchShaderUploadHash;
        private int _lastHatchShaderUploadCount;
        private byte _hatchShaderWriteBufferSlot;
        private byte _hatchShaderReadBufferSlot;
        private bool _hatchShaderHasValidReadBuffer;
        private bool _hatchShaderGlobalsActive;
        private long _hatchProfilesCsvLastWriteTicks;
        private string _hatchDumpPath;
        private string _hatchProfilesCsvPath;
        private GraphicsBuffer _hatchShaderStateBufferA;
        private GraphicsBuffer _hatchShaderStateBufferB;

        public static bool TryReadHatchEditorState(
            out int activeCount,
            out int pressureLockedCount,
            out int jammedCount,
            out int catastrophicFloodCount,
            out float safePressure,
            out float structuralJamThreshold,
            out float catastrophicPressure,
            out float maxPressureDifferential,
            out float averagePressureDifferential,
            out float lastScheduleMicroseconds,
            out uint telemetryFrame)
        {
            activeCount = 0;
            pressureLockedCount = 0;
            jammedCount = 0;
            catastrophicFloodCount = 0;
            safePressure = HatchLockConstants.DefaultSafePressureDifferentialATM;
            structuralJamThreshold = HatchLockConstants.DefaultStructuralJamThreshold01;
            catastrophicPressure = HatchLockConstants.DefaultCatastrophicPressureDifferentialATM;
            maxPressureDifferential = 0f;
            averagePressureDifferential = 0f;
            lastScheduleMicroseconds = 0f;
            telemetryFrame = 0u;

            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            activeCount = runtime._hatchActiveCount;
            pressureLockedCount = (int)runtime._lastHatchPressureLockedCount;
            jammedCount = (int)runtime._lastHatchJammedCount;
            catastrophicFloodCount = (int)runtime._lastHatchCatastrophicCount;
            safePressure = runtime.safePressureDifferentialATM;
            structuralJamThreshold = runtime.structuralJamThreshold01;
            catastrophicPressure = runtime.catastrophicPressureDifferentialATM;
            if (runtime._hatchDefaultsInitialized &&
                runtime.Resolve(in runtime._hatchTuningHandle, BufferID.Shinobu343HatchTuning, out NativeArray<HatchTuningDTO> tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0)
            {
                HatchTuningDTO row = tuning[0];
                safePressure = row.SafePressureDifferentialATM;
                structuralJamThreshold = row.StructuralJamThreshold01;
                catastrophicPressure = row.CatastrophicPressureDifferentialATM;
            }

            maxPressureDifferential = runtime._lastHatchMaxPressureDifferentialATM;
            averagePressureDifferential = runtime._lastHatchAveragePressureDifferentialATM;
            lastScheduleMicroseconds = runtime._lastHatchScheduleMicroseconds;
            telemetryFrame = runtime._lastHatchTelemetryFrame;
            return runtime._vaultInitialized;
        }

        public static bool TryApplyHatchEditorTuning(float safePressure, float structuralJamThreshold, float catastrophicPressure)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            runtime.safePressureDifferentialATM = math.max(0.05f, HatchLockMath.SanitizePositive(safePressure, HatchLockConstants.DefaultSafePressureDifferentialATM));
            runtime.structuralJamThreshold01 = HatchLockMath.Sanitize01(structuralJamThreshold, HatchLockConstants.DefaultStructuralJamThreshold01);
            runtime.catastrophicPressureDifferentialATM = math.max(
                runtime.safePressureDifferentialATM,
                HatchLockMath.SanitizePositive(catastrophicPressure, HatchLockConstants.DefaultCatastrophicPressureDifferentialATM));
            runtime.TryWriteHatchTuningRow();
            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadHatchProfilesFromCsvBytes(ReadOnlySpan<byte> csv)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureHatchLockVaultState(vault, runtime.ResolveHatchCapacity(), allowDefaultProfileLoad: false))
                return false;

            if (!TryAcquireWriteLane(vault, in runtime._hatchProfilesHandle, BufferID.Shinobu343HatchProfiles, 1, out NativeArray<HatchHardwareProfileDTO> profiles))
                return false;

            int parsed;
            try
            {
                parsed = ParseHatchProfiles(csv, profiles);
                if (parsed <= 0)
                    return false;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtime._hatchProfilesHandle, OwnerSystemId);
            }

            runtime._hatchProfileRowCount = parsed;
            runtime._hatchProfileCsvLoaded = true;
            runtime._hatchProfileCsvLoadAttempted = true;
            runtime.TryWriteHatchTuningRow();
            return true;
        }

        public static bool TryLoadHatchProfilesFromCsvFile(string path)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null || string.IsNullOrEmpty(path))
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureHatchLockVaultState(vault, runtime.ResolveHatchCapacity(), allowDefaultProfileLoad: false))
                return false;

            return runtime.TryApplyHatchProfilesCsvFile(vault, path, forceReload: true);
        }
#endif

        private void InitializeHatchLockColdPaths()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _hatchDumpPath = Path.GetFullPath(Path.Combine(projectRoot, "Docs/AgentLogs/Dump_1306_Construction_Hatch.bin"));
            if (string.IsNullOrEmpty(_hatchProfilesCsvPath))
                _hatchProfilesCsvPath = Path.GetFullPath(Path.Combine(projectRoot, "Data/Physics/hatch_hardware_profiles.csv"));
            SignalBus<MovementAcousticSignal>.EnsureInitialized();
        }

        private int ResolveHatchCapacity()
        {
            return math.clamp(bulkheadCapacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
        }

        private bool EnsureHatchLockVaultState(IDataVault vault, int capacity)
        {
            return EnsureHatchLockVaultState(vault, capacity, allowDefaultProfileLoad: true);
        }

        private bool EnsureHatchLockVaultState(IDataVault vault, int capacity, bool allowDefaultProfileLoad)
        {
            if (vault == null || !EnsureHatchLayoutValid())
                return false;

            int safeCapacity = math.clamp(capacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
            if (!IsBulkheadVaultHandle(in _hatchStatesHandle, BufferID.Shinobu343HatchStates))
                _hatchStatesHandle = vault.EnsureGenerationHandle<HatchStateDTO>(BufferID.Shinobu343HatchStates, safeCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing))
                _hatchTelemetryHandle = vault.EnsureGenerationHandle<HatchTelemetryEntry>(BufferID.Shinobu343HatchTelemetryRing, HatchLockConstants.TelemetryFrameCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor))
                _hatchTelemetryCursorHandle = vault.EnsureGenerationHandle<uint>(BufferID.Shinobu343HatchTelemetryCursor, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchTuningHandle, BufferID.Shinobu343HatchTuning))
                _hatchTuningHandle = vault.EnsureGenerationHandle<HatchTuningDTO>(BufferID.Shinobu343HatchTuning, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles))
                _hatchProfilesHandle = vault.EnsureGenerationHandle<HatchHardwareProfileDTO>(BufferID.Shinobu343HatchProfiles, HatchLockConstants.ProfileCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchCsvScratchHandle, BufferID.Shinobu343HatchCsvScratch))
                _hatchCsvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.Shinobu343HatchCsvScratch, HatchLockConstants.CsvScratchBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (!IsBulkheadVaultHandle(in _hatchMockFluidCompartmentsHandle, BufferID.Shinobu343HatchMockFluidCompartments))
                _hatchMockFluidCompartmentsHandle = vault.EnsureGenerationHandle<FluidCompartmentDTO>(
                    BufferID.Shinobu343HatchMockFluidCompartments,
                    safeCapacity * HatchLockConstants.MockFluidRowsPerHatch,
                    OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return RefreshHatchLockVaultState(vault, safeCapacity, allowDefaultProfileLoad);
        }

        private bool RefreshHatchLockVaultState(IDataVault vault, int capacity)
        {
            return RefreshHatchLockVaultState(vault, capacity, allowDefaultProfileLoad: true);
        }

        private bool RefreshHatchLockVaultState(IDataVault vault, int capacity, bool allowDefaultProfileLoad)
        {
            if (vault == null || !EnsureHatchLayoutValid())
                return false;

            int safeCapacity = math.clamp(capacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
            if (!Resolve(in _hatchStatesHandle, BufferID.Shinobu343HatchStates, out NativeArray<HatchStateDTO> hatches) ||
                !Resolve(in _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing, out NativeArray<HatchTelemetryEntry> telemetry) ||
                !Resolve(in _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles, out NativeArray<HatchHardwareProfileDTO> profiles) ||
                !Resolve(in _hatchMockFluidCompartmentsHandle, BufferID.Shinobu343HatchMockFluidCompartments, out NativeArray<FluidCompartmentDTO> mockFluid))
            {
                return false;
            }

            if (!hatches.IsCreated ||
                !telemetry.IsCreated ||
                !profiles.IsCreated ||
                !mockFluid.IsCreated ||
                hatches.Length <= 0 ||
                telemetry.Length <= 0 ||
                profiles.Length <= 0 ||
                mockFluid.Length <= 0)
            {
                return false;
            }

            TryBindHatchExternalHandles(vault);
            bool cursorLocked = false;
            bool tuningLocked = false;
            try
            {
                if (!_hatchDefaultsInitialized)
                {
                    if (!TryAcquireWriteLane(vault, in _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor, 1, out NativeArray<uint> cursor))
                        return false;
                    cursorLocked = true;
                    cursor[0] = 0u;
                    _hatchDefaultsInitialized = true;
                }

                if (!TryAcquireWriteLane(vault, in _hatchTuningHandle, BufferID.Shinobu343HatchTuning, 1, out NativeArray<HatchTuningDTO> tuning))
                    return false;
                tuningLocked = true;
                WriteHatchTuningRow(tuning, profiles, _hatchProfileRowCount, safeCapacity, ResolveBulkheadQualityWeight());
            }
            finally
            {
                if (tuningLocked)
                    vault.ReleaseWriteLock(in _hatchTuningHandle, OwnerSystemId);
                if (cursorLocked)
                    vault.ReleaseWriteLock(in _hatchTelemetryCursorHandle, OwnerSystemId);
            }
#if UNITY_EDITOR
            if (allowDefaultProfileLoad && !_hatchProfileCsvLoaded && !_hatchProfileCsvLoadAttempted)
            {
                _hatchProfileCsvLoadAttempted = true;
                TryApplyHatchProfilesCsvFile(vault, _hatchProfilesCsvPath, forceReload: false);
            }
#endif

            return true;
        }

        private void TryBindHatchExternalHandles(IDataVault vault)
        {
            if (vault == null)
            {
                _hatchFluidCompartmentsHandle = default;
                _hatchStructuralStatesHandle = default;
                return;
            }

            if (!vault.TryGetGenerationHandle(BufferID.ShinobuFluidCompartmentFront, out _hatchFluidCompartmentsHandle))
                _hatchFluidCompartmentsHandle = default;
            if (!vault.TryGetGenerationHandle(BufferID.StructuralIntegrityStates, out _hatchStructuralStatesHandle))
                _hatchStructuralStatesHandle = default;
        }

        private bool TryWriteHatchTuningRow()
        {
            IDataVault vault = ResolveVault();
            if (!TryAcquireWriteLane(vault, in _hatchTuningHandle, BufferID.Shinobu343HatchTuning, 1, out NativeArray<HatchTuningDTO> tuning))
            {
                return false;
            }

            try
            {
                NativeArray<HatchHardwareProfileDTO> profiles = default;
                Resolve(in _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles, out profiles);
                WriteHatchTuningRow(tuning, profiles, _hatchProfileRowCount, ResolveHatchCapacity(), ResolveBulkheadQualityWeight());
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _hatchTuningHandle, OwnerSystemId);
            }
        }

        private void WriteHatchTuningRow(
            NativeArray<HatchTuningDTO> tuning,
            NativeArray<HatchHardwareProfileDTO> profiles,
            int profileCount,
            int capacity,
            float quality)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            float q = HatchLockMath.Sanitize01(quality, 0f);
            float safePressure = math.max(0.05f, HatchLockMath.SanitizePositive(safePressureDifferentialATM, HatchLockConstants.DefaultSafePressureDifferentialATM));
            float structuralJam = HatchLockMath.Sanitize01(structuralJamThreshold01, HatchLockConstants.DefaultStructuralJamThreshold01);
            float catastrophicPressure = math.max(safePressure, HatchLockMath.SanitizePositive(catastrophicPressureDifferentialATM, HatchLockConstants.DefaultCatastrophicPressureDifferentialATM));
            uint flags = uploadHatchShaderBuffer ? HatchTuningFlags.ShaderUploadEnabled : HatchTuningFlags.None;
            ApplyHardwareProfileEnvelope(profiles, profileCount, ref safePressure, ref structuralJam, ref catastrophicPressure, ref flags);
            tuning[0] = new HatchTuningDTO
            {
                SafePressureDifferentialATM = safePressure,
                StructuralJamThreshold01 = structuralJam,
                CatastrophicPressureDifferentialATM = math.max(safePressure, catastrophicPressure),
                GlobalQualityWeight = q,
                TickIntervalSeconds = HatchLockMath.ResolveAuthorityTickIntervalSeconds(),
                ActiveCount = (uint)math.clamp(_hatchActiveCount, 0, capacity),
                Flags = flags
            };
        }

        private static void ApplyHardwareProfileEnvelope(
            NativeArray<HatchHardwareProfileDTO> profiles,
            int profileCount,
            ref float safePressure,
            ref float structuralJam,
            ref float catastrophicPressure,
            ref uint flags)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || profileCount <= 0)
                return;

            int count = math.min(profileCount, profiles.Length);
            for (int i = 0; i < count; i++)
            {
                HatchHardwareProfileDTO profile = profiles[i];
                if (profile.ProfileHash == 0u)
                    continue;

                float profileSafe = math.max(0.05f, HatchLockMath.SanitizePositive(profile.SafePressureDifferentialATM, safePressure));
                float profileJam = HatchLockMath.Sanitize01(profile.StructuralJamThreshold01, structuralJam);
                float profileCatastrophic = math.max(profileSafe, HatchLockMath.SanitizePositive(profile.CatastrophicPressureDifferentialATM, catastrophicPressure));
                safePressure = math.min(safePressure, profileSafe);
                structuralJam = math.max(structuralJam, profileJam);
                catastrophicPressure = math.min(catastrophicPressure, profileCatastrophic);
                flags |= HatchTuningFlags.HardwareProfileEnvelopeApplied;
            }

            catastrophicPressure = math.max(safePressure, catastrophicPressure);
        }

        private JobHandle ScheduleHatchLockPipeline(
            IDataVault vault,
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<float> moduleIntegrity,
            int count,
            uint frame,
            float deltaSeconds,
            float quality,
            JobHandle dependency)
        {
            if (vault == null ||
                count <= 0 ||
                !states.IsCreated ||
                !aups.IsCreated ||
                !moduleIntegrity.IsCreated ||
                !_vaultInitialized ||
                !EnsureHatchLayoutValid() ||
                !Resolve(in _hatchStatesHandle, BufferID.Shinobu343HatchStates, out NativeArray<HatchStateDTO> hatches) ||
                !Resolve(in _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing, out NativeArray<HatchTelemetryEntry> telemetry) ||
                !Resolve(in _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor, out NativeArray<uint> telemetryCursor) ||
                !Resolve(in _hatchTuningHandle, BufferID.Shinobu343HatchTuning, out NativeArray<HatchTuningDTO> tuning) ||
                !Resolve(in _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles, out NativeArray<HatchHardwareProfileDTO> profiles))
            {
                return dependency;
            }

            if (!hatches.IsCreated ||
                !telemetry.IsCreated ||
                !telemetryCursor.IsCreated ||
                !tuning.IsCreated ||
                !profiles.IsCreated ||
                hatches.Length <= 0 ||
                telemetry.Length <= 0 ||
                telemetryCursor.Length <= 0 ||
                tuning.Length <= 0 ||
                profiles.Length <= 0)
            {
                return dependency;
            }

            int activeCount = math.min(count, math.min(hatches.Length, math.min(aups.Length, moduleIntegrity.Length)));
            if (activeCount <= 0)
                return dependency;

            float q = HatchLockMath.Sanitize01(quality, 0f);
            float tickInterval = HatchLockMath.ResolveAuthorityTickIntervalSeconds();
            float dt = math.clamp(HatchLockMath.SanitizePositive(deltaSeconds, LockedSimulationTickDeltaSeconds), 0.0001f, SimulationAuthorityDeltaCeilingSeconds);
            float accumulated = _hatchPressureAccumulator + dt;
            _hatchPressureAccumulator = math.isfinite(accumulated) ? math.min(accumulated, SimulationAuthorityDeltaCeilingSeconds) : tickInterval;
            if (_hatchPressureAccumulator < tickInterval)
                return dependency;

            _hatchPressureAccumulator = 0f;
            _hatchActiveCount = activeCount;
            WriteHatchTuningRow(tuning, profiles, _hatchProfileRowCount, activeCount, q);

            long start = Stopwatch.GetTimestamp();
            SyncHatchRowsFromBulkheadsJob syncJob = new SyncHatchRowsFromBulkheadsJob
            {
                Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(hatches),
                Bulkheads = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                Count = activeCount
            };
            JobHandle handle = syncJob.Schedule(activeCount, 32, dependency);
            TrackScheduledSimulationJob(handle);

            NativeArray<FluidCompartmentDTO> fluidCompartments = default;
            bool useMockFluid = generateMockHatchPressure;
            if (!useMockFluid &&
                IsVaultHandleForBuffer(in _hatchFluidCompartmentsHandle, BufferID.ShinobuFluidCompartmentFront) &&
                TryLockOptionalBulkheadJobPin(BufferID.ShinobuFluidCompartmentFront, BulkheadJobPinHatchFluidFront) &&
                vault.TryReadHandle(in _hatchFluidCompartmentsHandle, out fluidCompartments) &&
                fluidCompartments.IsCreated &&
                fluidCompartments.Length > 0)
            {
                useMockFluid = false;
            }
            else
            {
                ReleaseOptionalBulkheadJobPin(BufferID.ShinobuFluidCompartmentFront, BulkheadJobPinHatchFluidFront);
                useMockFluid = generateMockHatchPressure;
            }

            if (useMockFluid &&
                Resolve(in _hatchMockFluidCompartmentsHandle, BufferID.Shinobu343HatchMockFluidCompartments, out NativeArray<FluidCompartmentDTO> mockFluid) &&
                mockFluid.IsCreated &&
                mockFluid.Length >= activeCount * HatchLockConstants.MockFluidRowsPerHatch)
            {
                GenerateMockHatchPressureJob mockJob = new GenerateMockHatchPressureJob
                {
                    Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(hatches),
                    MockCompartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(mockFluid),
                    HatchCount = activeCount,
                    CompartmentCount = mockFluid.Length,
                    Frame = frame,
                    Seed = MockSeed
                };
                handle = mockJob.Schedule(activeCount, 32, handle);
                TrackScheduledSimulationJob(handle);
                fluidCompartments = mockFluid;
            }

            uint telemetryFlags = 0u;
            if (!fluidCompartments.IsCreated || fluidCompartments.Length <= 0)
            {
                telemetryFlags |= HatchTelemetryFlags.MissingCompartment | HatchTelemetryFlags.ScheduleTimeOnly;
                MarkHatchFluidUnavailableJob unavailableJob = new MarkHatchFluidUnavailableJob
                {
                    Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(hatches),
                    Bulkheads = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                    Count = activeCount
                };
                handle = unavailableJob.Schedule(activeCount, 32, handle);
                TrackScheduledSimulationJob(handle);
                return ScheduleHatchTelemetryJob(handle, hatches, telemetry, telemetryCursor, activeCount, frame, q, tickInterval, start, telemetryFlags);
            }

            EvaluateHatchPressureJob pressureJob = new EvaluateHatchPressureJob
            {
                Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(hatches),
                Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(fluidCompartments),
                HatchCount = activeCount,
                CompartmentCount = fluidCompartments.Length
            };
            handle = pressureJob.Schedule(activeCount, 32, handle);
            TrackScheduledSimulationJob(handle);

            StructuralIntegrityStateDTO* structuralPtr = null;
            int structuralCount = 0;
            if (IsVaultHandleForBuffer(in _hatchStructuralStatesHandle, BufferID.StructuralIntegrityStates) &&
                TryLockOptionalBulkheadJobPin(BufferID.StructuralIntegrityStates, BulkheadJobPinHatchStructural) &&
                vault.TryReadHandle(in _hatchStructuralStatesHandle, out NativeArray<StructuralIntegrityStateDTO> structuralStates) &&
                structuralStates.IsCreated &&
                structuralStates.Length > 0)
            {
                structuralPtr = (StructuralIntegrityStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(structuralStates);
                structuralCount = structuralStates.Length;
            }
            else
            {
                ReleaseOptionalBulkheadJobPin(BufferID.StructuralIntegrityStates, BulkheadJobPinHatchStructural);
            }

            HatchTuningDTO hatchTuning = tuning[0];
            UpdateHatchFsmJob fsmJob = new UpdateHatchFsmJob
            {
                Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(hatches),
                Bulkheads = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                ModuleIntegrity01 = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(moduleIntegrity),
                StructuralStates = structuralPtr,
                HatchAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(aups),
                AcousticWriter = SignalBus<MovementAcousticSignal>.ParallelWriter,
                AcousticWriterBudget = SignalBus<MovementAcousticSignal>.ParallelWriterBudget,
                Count = activeCount,
                ModuleIntegrityCount = moduleIntegrity.Length,
                StructuralStateCount = structuralCount,
                SafePressureDifferentialATM = hatchTuning.SafePressureDifferentialATM,
                StructuralJamThreshold01 = hatchTuning.StructuralJamThreshold01,
                CatastrophicPressureDifferentialATM = hatchTuning.CatastrophicPressureDifferentialATM,
                AcousticAuthorityWeight = HatchLockConstants.AuthoritativeQualityWeight,
                Frame = frame,
                EmitAcousticSignals = 1
            };
            handle = fsmJob.Schedule(activeCount, 32, handle);
            TrackScheduledSimulationJob(handle);
            return ScheduleHatchTelemetryJob(handle, hatches, telemetry, telemetryCursor, activeCount, frame, q, tickInterval, start, telemetryFlags);
        }

        private JobHandle ScheduleHatchTelemetryJob(
            JobHandle dependency,
            NativeArray<HatchStateDTO> hatches,
            NativeArray<HatchTelemetryEntry> telemetry,
            NativeArray<uint> cursor,
            int count,
            uint frame,
            float q,
            float tickInterval,
            long scheduleStart,
            uint flags)
        {
            if (!hatches.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                hatches.Length <= 0 ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                return dependency;
            }

            float scheduleMicroseconds = ElapsedMicroseconds(scheduleStart);
            _lastHatchScheduleMicroseconds = scheduleMicroseconds;
            RecordHatchTelemetryJob telemetryJob = new RecordHatchTelemetryJob
            {
                Hatches = (HatchStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(hatches),
                Telemetry = (HatchTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                Cursor = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(cursor),
                Count = count,
                TelemetryCount = telemetry.Length,
                Frame = frame,
                GlobalQualityWeight = q,
                TickIntervalSeconds = tickInterval,
                LastScheduleMicroseconds = scheduleMicroseconds,
                ExtraFlags = flags | HatchTelemetryFlags.ScheduleTimeOnly
            };
            JobHandle handle = telemetryJob.Schedule(dependency);
            TrackScheduledSimulationJob(handle);
            return handle;
        }

        private void VisualSyncHatchLocks(IDataVault vault)
        {
            DumpHatchBlackBoxIfRequested(vault);
            if (!uploadHatchShaderBuffer || _hatchActiveCount <= 0)
            {
                DisableHatchShaderGlobals();
                return;
            }

            if (vault == null ||
                !AreHatchGraphicsBuffersReady() ||
                !Resolve(in _hatchStatesHandle, BufferID.Shinobu343HatchStates, out NativeArray<HatchStateDTO> hatches) ||
                !Resolve(in _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing, out NativeArray<HatchTelemetryEntry> telemetry) ||
                !Resolve(in _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor, out NativeArray<uint> cursor))
            {
                DisableHatchShaderGlobals();
                return;
            }

            if (!hatches.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                hatches.Length <= 0 ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0 ||
                cursor[0] == 0u)
            {
                DisableHatchShaderGlobals();
                return;
            }

            uint readCursor = cursor[0] - 1u;
            HatchTelemetryEntry entry = telemetry[(int)(readCursor % (uint)telemetry.Length)];
            _lastHatchTelemetryFrame = entry.Frame;
            _lastHatchPressureLockedCount = entry.PressureLockedCount;
            _lastHatchJammedCount = entry.JammedCount;
            _lastHatchCatastrophicCount = entry.CatastrophicFloodCount;
            _lastHatchMaxPressureDifferentialATM = entry.MaxPressureDifferentialATM;
            _lastHatchAveragePressureDifferentialATM = entry.AveragePressureDifferentialATM;

            int uploadCount = math.clamp(_hatchActiveCount, 1, math.min(hatches.Length, HatchLockConstants.ShaderUploadCapacity));
            bool shouldUpload = !_hatchShaderHasValidReadBuffer ||
                                _hatchShaderUploadDirty ||
                                _lastHatchShaderUploadCount != uploadCount ||
                                _lastHatchShaderUploadHash != entry.StateHash;
            if (shouldUpload)
            {
                GraphicsBuffer writeBuffer = GetHatchShaderStateBuffer(_hatchShaderWriteBufferSlot);
                if (UploadNativeArray(writeBuffer, hatches, uploadCount))
                {
                    _hatchShaderReadBufferSlot = _hatchShaderWriteBufferSlot;
                    _hatchShaderWriteBufferSlot = (byte)(1 - _hatchShaderWriteBufferSlot);
                    _lastHatchShaderUploadCount = uploadCount;
                    _lastHatchShaderUploadHash = entry.StateHash;
                    _hatchShaderHasValidReadBuffer = true;
                    _hatchShaderUploadDirty = false;
                }
            }

            GraphicsBuffer readBuffer = _hatchShaderHasValidReadBuffer ? GetHatchShaderStateBuffer(_hatchShaderReadBufferSlot) : null;
            if (readBuffer == null)
            {
                DisableHatchShaderGlobals();
                return;
            }

            Shader.SetGlobalBuffer(GlobalHatchLockStatesId, readBuffer);
            Shader.SetGlobalVector(
                GlobalHatchLockParamsId,
                new Vector4(uploadCount, uploadHatchShaderBuffer ? 1f : 0f, entry.MaxPressureDifferentialATM, entry.AveragePressureDifferentialATM));
            _hatchShaderGlobalsActive = true;
        }

        private bool AreHatchGraphicsBuffersReady()
        {
            int stride = UnsafeUtility.SizeOf<HatchStateDTO>();
            int count = HatchLockConstants.ShaderUploadCapacity;
            return IsGraphicsBufferValid(_hatchShaderStateBufferA, count, stride) &&
                   IsGraphicsBufferValid(_hatchShaderStateBufferB, count, stride);
        }

        private bool EnsureHatchGraphicsBuffers()
        {
            int stride = UnsafeUtility.SizeOf<HatchStateDTO>();
            int count = HatchLockConstants.ShaderUploadCapacity;
            if (IsGraphicsBufferValid(_hatchShaderStateBufferA, count, stride) &&
                IsGraphicsBufferValid(_hatchShaderStateBufferB, count, stride))
            {
                return true;
            }

            ReleaseHatchGraphicsBuffers();
            try
            {
                _hatchShaderStateBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, count, stride);
                _hatchShaderStateBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, count, stride);
                _hatchShaderWriteBufferSlot = 0;
                _hatchShaderReadBufferSlot = 0;
                _hatchShaderHasValidReadBuffer = false;
                _hatchShaderUploadDirty = true;
                return _hatchShaderStateBufferA != null && _hatchShaderStateBufferB != null;
            }
            catch (Exception)
            {
                ReleaseHatchGraphicsBuffers();
                return false;
            }
        }

        private GraphicsBuffer GetHatchShaderStateBuffer(byte slot)
        {
            return slot == 0 ? _hatchShaderStateBufferA : _hatchShaderStateBufferB;
        }

        private void DisableHatchShaderGlobals()
        {
            if (!_hatchShaderGlobalsActive)
                return;

            Shader.SetGlobalVector(GlobalHatchLockParamsId, Vector4.zero);
            _hatchShaderGlobalsActive = false;
            _hatchShaderHasValidReadBuffer = false;
            _hatchShaderUploadDirty = true;
        }

        private void ReleaseHatchGraphicsBuffers()
        {
            DisableHatchShaderGlobals();
            if (_hatchShaderStateBufferA != null)
            {
                _hatchShaderStateBufferA.Release();
                _hatchShaderStateBufferA = null;
            }

            if (_hatchShaderStateBufferB != null)
            {
                _hatchShaderStateBufferB.Release();
                _hatchShaderStateBufferB = null;
            }

            _hatchShaderHasValidReadBuffer = false;
            _hatchShaderGlobalsActive = false;
            _hatchShaderUploadDirty = true;
        }

        private void DumpHatchBlackBoxIfRequested(IDataVault vault)
        {
            if (vault == null ||
                _hatchTelemetryHandle.Generation == 0u ||
                _hatchTelemetryCursorHandle.Generation == 0u ||
                !vault.TryAcquireMutationGuard(HatchTelemetryDumpMutationGuardMask))
            {
                return;
            }

            try
            {
                if (!IsBulkheadVaultHandle(in _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing) ||
                    !IsBulkheadVaultHandle(in _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor) ||
                    !vault.TryResolveHandle(in _hatchTelemetryHandle, out NativeArray<HatchTelemetryEntry> telemetry) ||
                    !vault.TryResolveHandle(in _hatchTelemetryCursorHandle, out NativeArray<uint> cursor) ||
                    !telemetry.IsCreated ||
                    !cursor.IsCreated ||
                    telemetry.Length <= 0 ||
                    cursor.Length <= 0 ||
                    cursor[0] == 0u)
                {
                    return;
                }

                uint cursorValue = cursor[0];
                if (cursorValue == _lastHatchDumpedTelemetryCursor ||
                    cursorValue == _lastHatchDumpAttemptTelemetryCursor)
                    return;

                HatchTelemetryEntry entry = telemetry[(int)((cursorValue - 1u) % (uint)telemetry.Length)];
                if ((entry.Flags & HatchTelemetryFlags.DumpRequested) == 0u)
                    return;

                _lastHatchDumpAttemptTelemetryCursor = cursorValue;
                if (TryDumpHatchBlackBox(telemetry, cursorValue))
                    _lastHatchDumpedTelemetryCursor = cursorValue;
            }
            finally
            {
                vault.ReleaseMutationGuard(HatchTelemetryDumpMutationGuardMask);
            }
        }

        private bool TryDumpHatchBlackBox(NativeArray<HatchTelemetryEntry> telemetry, uint cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(_hatchDumpPath))
                return false;

            const int telemetryDumpEntryBytes = 64;
            if (UnsafeUtility.SizeOf<HatchTelemetryEntry>() != telemetryDumpEntryBytes)
                return false;

            string dumpDirectory = Path.GetDirectoryName(_hatchDumpPath);
            if (string.IsNullOrEmpty(dumpDirectory))
                return false;

            try
            {
                Directory.CreateDirectory(dumpDirectory);
                using FileStream stream = new FileStream(_hatchDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> header = stackalloc byte[16];
                WriteUInt(header, 0, HatchLockConstants.DumpMagic);
                WriteUInt(header, 4, cursor);
                WriteUInt(header, 8, (uint)telemetry.Length);
                WriteUInt(header, 12, telemetryDumpEntryBytes);
                stream.Write(header);

                Span<byte> entryBytes = stackalloc byte[telemetryDumpEntryBytes];
                for (int i = 0; i < telemetry.Length; i++)
                {
                    HatchTelemetryEntry entry = telemetry[i];
                    WriteHatchTelemetryEntry(entryBytes, in entry);
                    stream.Write(entryBytes);
                }

                return true;
            }
            catch (Exception ex) when (IsColdStorageException(ex))
            {
                return false;
            }
        }

        private static void WriteHatchTelemetryEntry(Span<byte> entryBytes, in HatchTelemetryEntry entry)
        {
            entryBytes.Clear();
            WriteUInt(entryBytes, 0, entry.Frame);
            WriteUInt(entryBytes, 4, entry.ActiveCount);
            WriteUInt(entryBytes, 8, entry.PressureLockedCount);
            WriteUInt(entryBytes, 12, entry.JammedCount);
            WriteUInt(entryBytes, 16, entry.CatastrophicFloodCount);
            WriteFloat(entryBytes, 20, entry.MaxPressureDifferentialATM);
            WriteFloat(entryBytes, 24, entry.AveragePressureDifferentialATM);
            WriteFloat(entryBytes, 28, entry.LastScheduleMicroseconds);
            WriteUInt(entryBytes, 32, entry.StateHash);
            WriteUInt(entryBytes, 36, entry.LastFaultRoomHash);
            WriteUInt(entryBytes, 40, entry.Flags);
            WriteFloat(entryBytes, 44, entry.GlobalQualityWeight);
            WriteFloat(entryBytes, 48, entry.TickIntervalSeconds);
            WriteUInt(entryBytes, 52, entry.EvaluatedCount);
            WriteUInt(entryBytes, 56, entry.Reserved0);
            WriteUInt(entryBytes, 60, entry.Reserved1);
        }

#if UNITY_EDITOR
        private bool TryApplyHatchProfilesCsvFile(IDataVault vault, string path, bool forceReload)
        {
            if (vault == null || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryAcquireWriteLane(vault, in _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles, 1, out NativeArray<HatchHardwareProfileDTO> profiles))
                return false;

            bool scratchLocked = false;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0L || info.Length > HatchLockConstants.CsvScratchBytes)
                    return false;
                if (!forceReload && _hatchProfileCsvLoaded && info.LastWriteTimeUtc.Ticks == _hatchProfilesCsvLastWriteTicks)
                    return false;

                if (!TryAcquireWriteLane(vault, in _hatchCsvScratchHandle, BufferID.Shinobu343HatchCsvScratch, 1, out NativeArray<byte> scratch))
                    return false;

                scratchLocked = true;

                int byteCount = (int)info.Length;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int totalRead = 0;
                while (totalRead < byteCount)
                {
                    Span<byte> span = new Span<byte>(scratchPtr + totalRead, byteCount - totalRead);
                    int read = stream.Read(span);
                    if (read <= 0)
                        break;
                    totalRead += read;
                }

                int parsed = totalRead == byteCount ? ParseHatchProfiles(new ReadOnlySpan<byte>(scratchPtr, totalRead), profiles) : 0;
                if (parsed <= 0)
                    return false;

                _hatchProfileRowCount = parsed;
                _hatchProfileCsvLoaded = true;
                _hatchProfileCsvLoadAttempted = true;
                _hatchProfilesCsvLastWriteTicks = info.LastWriteTimeUtc.Ticks;
                TryWriteHatchTuningRow();
                return true;
            }
            catch (Exception ex) when (IsColdStorageException(ex))
            {
                return false;
            }
            finally
            {
                if (scratchLocked)
                    vault.ReleaseWriteLock(in _hatchCsvScratchHandle, OwnerSystemId);
                vault.ReleaseWriteLock(in _hatchProfilesHandle, OwnerSystemId);
            }
        }

        private static int ParseHatchProfiles(ReadOnlySpan<byte> csv, NativeArray<HatchHardwareProfileDTO> profiles)
        {
            int count = 0;
            int index = 0;
            while (index < csv.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> line = SliceNextLine(csv, ref index);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;
                if (StartsWithAscii(line, "profile"))
                    continue;

                HatchHardwareProfileDTO profile = default;
                int column = 0;
                int cellIndex = 0;
                bool rowValid = true;
                while (cellIndex <= line.Length)
                {
                    ReadOnlySpan<byte> cell = SliceNextCell(line, ref cellIndex);
                    switch (column)
                    {
                        case 0: profile.ProfileHash = HashAscii(cell); break;
                        case 1: rowValid &= TryParseFloat(cell, out profile.SafePressureDifferentialATM); break;
                        case 2: rowValid &= TryParseFloat(cell, out profile.StructuralJamThreshold01); break;
                        case 3: rowValid &= TryParseFloat(cell, out profile.CatastrophicPressureDifferentialATM); break;
                        case 4: rowValid &= TryParseFloat(cell, out profile.ManualBreakFloodScalar); break;
                        case 5: rowValid &= TryParseFloat(cell, out profile.VisualPulseHz); break;
                        case 6: profile.Flags = HashAscii(cell); break;
                    }
                    column++;
                }

                if (!rowValid || column < 6 || profile.ProfileHash == 0u)
                    continue;

                profile.SafePressureDifferentialATM = math.max(0.05f, HatchLockMath.SanitizePositive(profile.SafePressureDifferentialATM, HatchLockConstants.DefaultSafePressureDifferentialATM));
                profile.StructuralJamThreshold01 = HatchLockMath.Sanitize01(profile.StructuralJamThreshold01, HatchLockConstants.DefaultStructuralJamThreshold01);
                profile.CatastrophicPressureDifferentialATM = math.max(profile.SafePressureDifferentialATM, HatchLockMath.SanitizePositive(profile.CatastrophicPressureDifferentialATM, HatchLockConstants.DefaultCatastrophicPressureDifferentialATM));
                profile.ManualBreakFloodScalar = math.max(0.01f, HatchLockMath.SanitizePositive(profile.ManualBreakFloodScalar, 1f));
                profile.VisualPulseHz = math.max(0.01f, HatchLockMath.SanitizePositive(profile.VisualPulseHz, 1f));
                profiles[count++] = profile;
            }

            return count;
        }
#endif

        private bool EnsureHatchLayoutValid()
        {
            if (_hatchLayoutChecked)
                return _hatchLayoutValid;

            _hatchLayoutChecked = true;
            _hatchLayoutValid = HatchLockLayoutGuard.ValidateLayout();
            return _hatchLayoutValid;
        }

        private void ReleaseHatchLockVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _hatchStatesHandle, BufferID.Shinobu343HatchStates);
                ReleaseVaultHandle(vault, ref _hatchTelemetryHandle, BufferID.Shinobu343HatchTelemetryRing);
                ReleaseVaultHandle(vault, ref _hatchTelemetryCursorHandle, BufferID.Shinobu343HatchTelemetryCursor);
                ReleaseVaultHandle(vault, ref _hatchTuningHandle, BufferID.Shinobu343HatchTuning);
                ReleaseVaultHandle(vault, ref _hatchProfilesHandle, BufferID.Shinobu343HatchProfiles);
                ReleaseVaultHandle(vault, ref _hatchCsvScratchHandle, BufferID.Shinobu343HatchCsvScratch);
                ReleaseVaultHandle(vault, ref _hatchMockFluidCompartmentsHandle, BufferID.Shinobu343HatchMockFluidCompartments);
            }

            _hatchStatesHandle = default;
            _hatchTelemetryHandle = default;
            _hatchTelemetryCursorHandle = default;
            _hatchTuningHandle = default;
            _hatchProfilesHandle = default;
            _hatchCsvScratchHandle = default;
            _hatchMockFluidCompartmentsHandle = default;
            _hatchFluidCompartmentsHandle = default;
            _hatchStructuralStatesHandle = default;
        }

        private void ResetHatchLockRuntimeState()
        {
            _hatchDefaultsInitialized = false;
            _hatchActiveCount = 0;
            _hatchPressureAccumulator = 0f;
            _lastHatchScheduleMicroseconds = 0f;
            _lastHatchTelemetryFrame = 0u;
            _lastHatchPressureLockedCount = 0u;
            _lastHatchJammedCount = 0u;
            _lastHatchCatastrophicCount = 0u;
            _lastHatchMaxPressureDifferentialATM = 0f;
            _lastHatchAveragePressureDifferentialATM = 0f;
            _lastHatchDumpedTelemetryCursor = 0u;
            _lastHatchDumpAttemptTelemetryCursor = 0u;
            _hatchShaderUploadDirty = true;
            _hatchProfileCsvLoaded = false;
            _hatchProfileCsvLoadAttempted = false;
            _hatchProfileRowCount = 0;
            _hatchProfilesCsvLastWriteTicks = 0L;
        }

#if UNITY_EDITOR
        private void DrawHatchLockGizmos()
        {
            if (!_vaultInitialized ||
                !Resolve(in _hatchStatesHandle, BufferID.Shinobu343HatchStates, out NativeArray<HatchStateDTO> hatches) ||
                !Resolve(in _aupsHandle, BufferID.Shinobu220BulkheadAups, out NativeArray<double3> aups) ||
                !hatches.IsCreated ||
                !aups.IsCreated)
            {
                return;
            }

            int count = math.clamp(_hatchActiveCount, 0, math.min(hatches.Length, aups.Length));
            for (int i = 0; i < count; i++)
            {
                HatchStateDTO hatch = hatches[i];
                if ((hatch.FsmStateMask & HatchFsmStateMask.Active) == 0u)
                    continue;

                double3 centerAup = aups[i];
                if (!math.all(math.isfinite(centerAup)))
                    continue;

                float3 local = AupPrecisionMath.LocalDeltaFloat3(centerAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
                if (!math.all(math.isfinite(local)))
                    continue;

                if ((hatch.FsmStateMask & HatchFsmStateMask.StructurallyJammed) != 0u)
                    Gizmos.color = new Color(1f, 0.02f, 0.02f, 0.95f);
                else if ((hatch.FsmStateMask & HatchFsmStateMask.PressureLocked) != 0u)
                    Gizmos.color = new Color(1f, 0.82f, 0.05f, 0.95f);
                else
                    Gizmos.color = new Color(0.05f, 1f, 0.35f, 0.75f);

                Vector3 center = new Vector3(local.x, local.y, local.z);
                const float radius = 0.65f;
                Gizmos.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius);
                Gizmos.DrawLine(center + Vector3.down * radius, center + Vector3.up * radius);
                Gizmos.DrawLine(center + Vector3.back * radius, center + Vector3.forward * radius);
            }
        }
#endif
    }
}
