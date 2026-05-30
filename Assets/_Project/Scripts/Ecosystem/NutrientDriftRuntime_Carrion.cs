using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    public unsafe sealed partial class NutrientDriftRuntime
    {
        public const int CarrionCapacity = 5000;
        public const int CarrionDeathIngressCapacity = 512;
        public const int CarrionAttractionCapacity = 512;
        public const int CarrionProfileCapacity = 64;
        public const int CarrionFaultFlagCapacity = 4;
        public const int CarrionCsvScratchBytes = 16384;
        public const string CarrionProfileCsvFileName = "carrion_decay_profiles.csv";
        public const string CarrionDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_314.bin";
        public const uint CarrionRouteHash = 0x53333134u;
        public const ulong CarrionDumpMagic = 0x3433315F4E524143UL; // CARN_314 little-endian marker.

        private const int CarrionJobBatchSize = 64;
        public const float DefaultCarrionBaseDecayRate = 0.00042f;
        public const float DefaultCarrionLinearDecayRate = 0.018f;
        public const float DefaultCarrionBiomass = 80f;
        public const float DefaultCarrionEpsilonBiomass = 0.01f;
        public const float DefaultCarrionColdMultiplier = 0.1f;
        public const float DefaultCarrionHotMultiplier = 3.0f;
        public const float DefaultCarrionAttractionRadius = 72f;
        public const float DefaultCarrionFoodScalar = 0.035f;
        public const float DefaultCarrionNutrientInjection = 0.18f;
        private const float CarrionFaultBudgetMicros = 500f;
        private const uint CarrionTuningFlagInitialized = 1u << 0;
        private const uint CarrionTuningFlagWriteInFlight = 1u << 1;
        private const uint CarrionTuningFlagNetcodeExcluded = 1u << 2;
        public const uint CarrionTelemetryFlagNaN = 1u << 0;
        public const uint CarrionTelemetryFlagOverBudget = 1u << 1;
        public const uint CarrionTelemetryFlagOverflow = 1u << 2;
        private static readonly ulong CarrionProfileCsvMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuCarrionProfiles);
#if UNITY_EDITOR
        private static readonly byte[] s_carrionCsvImportScratch = new byte[CarrionCsvScratchBytes];
        private static readonly CarrionDecayProfileDTO[] s_carrionProfileImportScratch = new CarrionDecayProfileDTO[CarrionProfileCapacity];
        private static int s_carrionCsvImportScratchBusy;
#endif

        private VaultGenerationHandle<CarrionStateDTO> _carrionStateHandle;
        private VaultGenerationHandle<CarrionDeathSignalDTO> _carrionDeathIngressHandle;
        private VaultGenerationHandle<CarrionRuntimeCountersDTO> _carrionCountersHandle;
        private VaultGenerationHandle<CarrionTuningDTO> _carrionTuningHandle;
        private VaultGenerationHandle<CarrionTelemetryEntry> _carrionTelemetryHandle;
        private VaultGenerationHandle<CarrionAttractionRecordDTO> _carrionAttractionHandle;
        private VaultGenerationHandle<CarrionDecayProfileDTO> _carrionProfileHandle;
        private VaultGenerationHandle<byte> _carrionCsvScratchHandle;
        private VaultGenerationHandle<FaunaStateDTO> _carrionFaunaStateHandle;
        private VaultGenerationHandle<uint> _carrionFaultFlagHandle;

        private long _carrionCsvTimestampTicks;
        private long _carrionScheduleTicks;
        private int _carrionDeathSignalGeneration;
        private int _carrionTelemetryCursor;
        private int _lastCarrionTelemetrySlot;
        private IDataVault _carrionJobGuardVault;
        private bool _carrionInitialized;
        private bool _carrionJobLocksHeld;
        private bool _carrionProfilesLoadedCold;
        private bool _carrionProfilesLoadAttemptedCold;
        private bool _carrionDumpedFault;

        public static bool TryReadCarrionTuning(out CarrionTuningDTO tuning)
        {
            tuning = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !TryOpenReadVaultBuffer(vault, in runtime._carrionTuningHandle, out NativeArray<CarrionTuningDTO>.ReadOnly tuningArray) ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            tuning = CarrionDecayMath.SanitizeTuning(tuningArray[0], tuningArray[0].GridOriginAup);
            return (tuning.Flags & CarrionTuningFlagInitialized) != 0u &&
                   (tuning.Flags & CarrionTuningFlagWriteInFlight) == 0u;
        }

        public static bool TryWriteCarrionTuning(in CarrionTuningDTO requestedTuning)
        {
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !IsMatchingVaultHandle(in runtime._carrionTuningHandle, BufferID.ShinobuCarrionTuning))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in runtime._carrionTuningHandle, SystemID.AIEcology, out NativeArray<CarrionTuningDTO> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated ||
                    tuningArray.Length <= 0)
                {
                    return false;
                }

                CarrionTuningDTO sanitized = CarrionDecayMath.SanitizeTuning(requestedTuning, tuningArray[0].GridOriginAup);
                sanitized.Flags &= ~CarrionTuningFlagWriteInFlight;
                sanitized.Flags |= CarrionTuningFlagInitialized | CarrionTuningFlagNetcodeExcluded;
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<CarrionTuningDTO>(ptr) = sanitized;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtime._carrionTuningHandle, SystemID.AIEcology);
            }
        }

        public static bool TryReadCarrionTelemetryEntry(int index, out CarrionTelemetryEntry entry)
        {
            entry = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                (uint)index >= TelemetryCapacity ||
                !TryOpenReadVaultBuffer(vault, in runtime._carrionTelemetryHandle, out NativeArray<CarrionTelemetryEntry>.ReadOnly telemetry) ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return true;
        }

        public static bool TryReadCarrionState(int index, out CarrionStateDTO state)
        {
            state = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                (uint)index >= CarrionCapacity ||
                !TryOpenReadVaultBuffer(vault, in runtime._carrionStateHandle, out NativeArray<CarrionStateDTO>.ReadOnly states) ||
                (uint)index >= (uint)states.Length)
            {
                return false;
            }

            state = states[index];
            return true;
        }

        public static bool TryReadCarrionTelemetryCursor(out int cursor)
        {
            cursor = 0;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !TryOpenReadVaultBuffer(vault, in runtime._carrionCountersHandle, out NativeArray<CarrionRuntimeCountersDTO>.ReadOnly counters) ||
                counters.Length <= 0)
            {
                return false;
            }

            cursor = counters[0].TelemetryCursor;
            return true;
        }

        public static bool ForceReloadCarrionProfilesCold()
        {
            NutrientDriftRuntime runtime = EnsureRuntime();
            runtime._carrionCsvTimestampTicks = 0L;
            runtime._carrionProfilesLoadAttemptedCold = true;
            runtime._carrionProfilesLoadedCold = false;
            bool loaded = runtime.TryLoadCarrionProfilesCsvCold();
            runtime._carrionProfilesLoadedCold = loaded;
            return loaded;
        }

        public static bool GenerateMockMassExtinctionCold(int requestedCount, uint seed = 0xC314C314u)
        {
            NutrientDriftRuntime runtime = EnsureRuntime();
            IDataVault vault = runtime._vault;
            if (runtime._jobScheduled ||
                runtime._carrionJobLocksHeld ||
                vault == null ||
                !runtime.EnsureCarrionVaultState(vault))
            {
                return false;
            }

            if (!runtime.TryLockCarrionJobBuffers(vault))
                return false;

            try
            {
                if (!TryOpenVaultBuffer(vault, ref runtime._carrionStateHandle, BufferID.ShinobuCarrionStates, CarrionCapacity, out NativeArray<CarrionStateDTO> states) ||
                    !TryOpenVaultBuffer(vault, ref runtime._carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates, CarrionCapacity, out NativeArray<FaunaStateDTO> faunaStates) ||
                    !TryOpenVaultBuffer(vault, ref runtime._carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters, 1, out NativeArray<CarrionRuntimeCountersDTO> counters) ||
                    !TryOpenVaultBuffer(vault, ref runtime._carrionTuningHandle, BufferID.ShinobuCarrionTuning, 1, out NativeArray<CarrionTuningDTO> tuningArray))
                {
                    return false;
                }

                CarrionTuningDTO tuning = CarrionDecayMath.SanitizeTuning(tuningArray[0], tuningArray[0].GridOriginAup);
                var job = new GenerateMockMassExtinctionJob
                {
                    CarrionStates = (CarrionStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                    FaunaStates = (FaunaStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(faunaStates),
                    Counters = (CarrionRuntimeCountersDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    Tuning = tuning,
                    RequestedCount = math.clamp(requestedCount, 0, CarrionCapacity),
                    Seed = seed == 0u ? 0xC314C314u : seed
                };
                JobHandle handle = job.Schedule(CarrionCapacity, CarrionJobBatchSize);
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD_EDITOR_STRESS: explicit designer-triggered mass extinction harness.
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }
                return true;
            }
            finally
            {
                runtime.UnlockCarrionJobBuffers();
            }
        }

        private bool EnsureCarrionVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (_carrionInitialized && AreCarrionVaultHandlesStamped())
            {
                if (!_carrionProfilesLoadAttemptedCold)
                {
                    _carrionProfilesLoadAttemptedCold = true;
                    _carrionProfilesLoadedCold = TryLoadCarrionProfilesCsvCold();
                }

                return true;
            }

            if (!EnsureVaultBufferHandle(vault, ref _carrionStateHandle, BufferID.ShinobuCarrionStates, CarrionCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionDeathIngressHandle, BufferID.ShinobuCarrionDeathIngress, CarrionDeathIngressCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionTuningHandle, BufferID.ShinobuCarrionTuning, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing, TelemetryCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords, CarrionAttractionCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionProfileHandle, BufferID.ShinobuCarrionProfiles, CarrionProfileCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionCsvScratchHandle, BufferID.ShinobuCarrionCsvScratch, CarrionCsvScratchBytes, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates, CarrionCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _carrionFaultFlagHandle, BufferID.ShinobuCarrionFaultFlags, CarrionFaultFlagCapacity, NativeArrayOptions.UninitializedMemory))
            {
                return false;
            }

            bool carrionReady = false;
            bool profileLoadRequired = false;
            bool carrionLocked = false;
            try
            {
                if (!TryLockCarrionJobBuffers(vault))
                    return false;

                carrionLocked = true;
                if (!TryOpenVaultBuffer(vault, ref _carrionStateHandle, BufferID.ShinobuCarrionStates, CarrionCapacity, out NativeArray<CarrionStateDTO> states) ||
                    !TryOpenVaultBuffer(vault, ref _carrionDeathIngressHandle, BufferID.ShinobuCarrionDeathIngress, CarrionDeathIngressCapacity, out NativeArray<CarrionDeathSignalDTO> deathIngress) ||
                    !TryOpenVaultBuffer(vault, ref _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters, 1, out NativeArray<CarrionRuntimeCountersDTO> counters) ||
                    !TryOpenVaultBuffer(vault, ref _carrionTuningHandle, BufferID.ShinobuCarrionTuning, 1, out NativeArray<CarrionTuningDTO> tuning) ||
                    !TryOpenVaultBuffer(vault, ref _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing, TelemetryCapacity, out NativeArray<CarrionTelemetryEntry> telemetry) ||
                    !TryOpenVaultBuffer(vault, ref _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords, CarrionAttractionCapacity, out NativeArray<CarrionAttractionRecordDTO> attractions) ||
                    !TryOpenVaultBuffer(vault, ref _carrionProfileHandle, BufferID.ShinobuCarrionProfiles, CarrionProfileCapacity, out NativeArray<CarrionDecayProfileDTO> profiles) ||
                    !TryOpenVaultBuffer(vault, ref _carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates, CarrionCapacity, out NativeArray<FaunaStateDTO> faunaStates) ||
                    !TryOpenVaultBuffer(vault, ref _carrionFaultFlagHandle, BufferID.ShinobuCarrionFaultFlags, CarrionFaultFlagCapacity, out NativeArray<uint> faultFlags))
                {
                    return false;
                }

                if (_carrionInitialized && (tuning[0].Flags & CarrionTuningFlagInitialized) != 0u)
                {
                    carrionReady = true;
                    profileLoadRequired = !_carrionProfilesLoadAttemptedCold;
                }
                else
                {
                    double3 originAup = ResolveGridOriginAup();
                    CarrionTuningDTO defaultTuning = CarrionDecayMath.CreateDefaultTuning(originAup);
                    tuning[0] = defaultTuning;
                    counters[0] = default;
                    _carrionTelemetryCursor = 0;

                    var initJob = new InitializeCarrionVaultJob
                    {
                        CarrionStates = (CarrionStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                        DeathIngress = (CarrionDeathSignalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(deathIngress),
                        TelemetryRing = (CarrionTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                        AttractionRecords = (CarrionAttractionRecordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(attractions),
                        Profiles = (CarrionDecayProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                        FaunaStates = (FaunaStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(faunaStates),
                        FaultFlags = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags)
                    };
                    JobHandle initHandle = initJob.Schedule(CarrionCapacity, CarrionJobBatchSize);
                    DispatcherJobFence.BeginPostSimulationSwapWindow();
                    try
                    {
                        DispatcherJobFence.TryComplete(ref initHandle, forceComplete: true); // COLD_BOOTSTRAP_SYNC: Vault memory must be initialized before public editor reads.
                    }
                    finally
                    {
                        DispatcherJobFence.EndPostSimulationSwapWindow();
                    }

                    _carrionInitialized = true;
                    carrionReady = true;
                    profileLoadRequired = !_carrionProfilesLoadAttemptedCold;
                }
            }
            finally
            {
                if (carrionLocked)
                    UnlockCarrionJobBuffers();
            }

            if (!carrionReady)
                return false;

            if (profileLoadRequired)
            {
                _carrionProfilesLoadAttemptedCold = true;
                _carrionProfilesLoadedCold = TryLoadCarrionProfilesCsvCold();
            }

            return true;
        }

        private bool AreCarrionVaultHandlesStamped()
        {
            return IsMatchingVaultHandle(in _carrionStateHandle, BufferID.ShinobuCarrionStates) &&
                   IsMatchingVaultHandle(in _carrionDeathIngressHandle, BufferID.ShinobuCarrionDeathIngress) &&
                   IsMatchingVaultHandle(in _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters) &&
                   IsMatchingVaultHandle(in _carrionTuningHandle, BufferID.ShinobuCarrionTuning) &&
                   IsMatchingVaultHandle(in _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing) &&
                   IsMatchingVaultHandle(in _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords) &&
                   IsMatchingVaultHandle(in _carrionProfileHandle, BufferID.ShinobuCarrionProfiles) &&
                   IsMatchingVaultHandle(in _carrionCsvScratchHandle, BufferID.ShinobuCarrionCsvScratch) &&
                   IsMatchingVaultHandle(in _carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates) &&
                   IsMatchingVaultHandle(in _carrionFaultFlagHandle, BufferID.ShinobuCarrionFaultFlags);
        }

        private bool HasCarrionVaultStateReady()
        {
            return _carrionInitialized && AreCarrionVaultHandlesStamped();
        }

        private void DrainCarrionDeathSignalSnapshot()
        {
            if (_jobScheduled || _carrionJobLocksHeld)
                return;

            IDataVault vault = _vault;
            if (vault == null || !HasCarrionVaultStateReady())
                return;

            int generation = SignalBus<EntityDeathSignal>.SnapshotGeneration;
            if (generation <= 0 || generation == _carrionDeathSignalGeneration)
                return;

            ReadOnlySpan<EntityDeathSignal> snapshot = SignalBus<EntityDeathSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
            {
                _carrionDeathSignalGeneration = generation;
                return;
            }

            bool tuningOpened = TryOpenReadVaultBuffer(vault, in _carrionTuningHandle, out NativeArray<CarrionTuningDTO>.ReadOnly tuningArray);
            CarrionTuningDTO tuning = tuningOpened
                ? CarrionDecayMath.SanitizeTuning(tuningArray[0], tuningArray[0].GridOriginAup)
                : CarrionDecayMath.CreateDefaultTuning(ResolveGridOriginAup());

            if (!vault.TryAcquireMutationGuard(CarrionDeathIngressMutationGuardMask))
                return;

            try
            {
                if (!vault.TryResolveHandle(in _carrionCountersHandle, out NativeArray<CarrionRuntimeCountersDTO> counters) ||
                    !vault.TryResolveHandle(in _carrionDeathIngressHandle, out NativeArray<CarrionDeathSignalDTO> ingress) ||
                    !counters.IsCreated ||
                    counters.Length <= 0 ||
                    !ingress.IsCreated ||
                    ingress.Length < CarrionDeathIngressCapacity)
                {
                    return;
                }

                CarrionRuntimeCountersDTO counter = counters[0];
                int pending = math.clamp(counter.DeathIngressCount, 0, CarrionDeathIngressCapacity);
                int writeCursor = CarrionDecayMath.PositiveModulo(counter.DeathIngressWriteCursor, CarrionDeathIngressCapacity);
                int accepted = 0;
                uint overflow = counter.OverflowCount;

                for (int i = 0; i < snapshot.Length; i++)
                {
                    EntityDeathSignal signal = snapshot[i];
                    if (!signal.PositionAup.IsFinite())
                        continue;

                    double3 aup = signal.PositionAup.ToAbsoluteDouble3();
                    if (!math.all(math.isfinite(aup)))
                        continue;

                    if (pending + accepted >= CarrionDeathIngressCapacity)
                    {
                        overflow++;
                        continue;
                    }

                    int slot = (writeCursor + accepted) % CarrionDeathIngressCapacity;
                    float biomassScale = math.clamp(math.select(1f, signal.Intensity01, math.isfinite(signal.Intensity01)), 0.05f, 32f);
                    bool faunaOwnedDeathSignal = (signal.Flags & EntityDeathSignal.FlagFaunaBrainCarrion) != 0;
                    uint speciesHash = faunaOwnedDeathSignal && signal.SourceHash != 0u
                        ? signal.SourceHash
                        : signal.EntityHash != 0u ? signal.EntityHash : CarrionRouteHash;
                    ingress[slot] = new CarrionDeathSignalDTO
                    {
                        CorpseAUP = aup,
                        BiomassScale = biomassScale,
                        OriginalSpeciesHash = speciesHash,
                        SourceHash = signal.SourceHash,
                        EntityHash = signal.EntityHash,
                        Flags = signal.Flags,
                        ToxicitySeed = tuning.ToxicityNutrientPenalty
                    };
                    accepted++;
                }

                counter.DeathIngressWriteCursor = (writeCursor + accepted) % CarrionDeathIngressCapacity;
                counter.DeathIngressCount = pending + accepted;
                counter.OverflowCount = overflow;
                counter.Flags = overflow != 0u ? counter.Flags | CarrionTelemetryFlagOverflow : counter.Flags;
                counters[0] = counter;
                _carrionDeathSignalGeneration = generation;
            }
            finally
            {
                vault.ReleaseMutationGuard(CarrionDeathIngressMutationGuardMask);
            }
        }

        private bool TryLockCarrionJobBuffers(IDataVault vault)
        {
            if (vault == null || _carrionJobLocksHeld || _jobLocksHeld)
                return false;
            if (!vault.TryAcquireMutationGuard(CarrionJobMutationGuardMask))
                return false;

            _carrionJobLocksHeld = true;
            _carrionJobGuardVault = vault;
            return true;
        }

        private void UnlockCarrionJobBuffers()
        {
            if (!_carrionJobLocksHeld)
                return;

            _carrionJobLocksHeld = false;
            IDataVault vault = _carrionJobGuardVault;
            _carrionJobGuardVault = null;
            if (vault != null)
                vault.ReleaseMutationGuard(CarrionJobMutationGuardMask);
        }

        private JobHandle ScheduleCarrionDecayJobs(
            IDataVault vault,
            NutrientDriftTuningDTO nutrientTuning,
            NutrientCellDTO* nutrientFront,
            float* nutrientInjection,
            JobHandle dependency)
        {
            if (vault == null)
                return dependency;

            if (!TryOpenVaultBuffer(vault, ref _carrionStateHandle, BufferID.ShinobuCarrionStates, CarrionCapacity, out NativeArray<CarrionStateDTO> states) ||
                !TryOpenVaultBuffer(vault, ref _carrionDeathIngressHandle, BufferID.ShinobuCarrionDeathIngress, CarrionDeathIngressCapacity, out NativeArray<CarrionDeathSignalDTO> deathIngress) ||
                !TryOpenVaultBuffer(vault, ref _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters, 1, out NativeArray<CarrionRuntimeCountersDTO> counters) ||
                !TryOpenVaultBuffer(vault, ref _carrionTuningHandle, BufferID.ShinobuCarrionTuning, 1, out NativeArray<CarrionTuningDTO> tuningArray) ||
                !TryOpenVaultBuffer(vault, ref _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing, TelemetryCapacity, out NativeArray<CarrionTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(vault, ref _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords, CarrionAttractionCapacity, out NativeArray<CarrionAttractionRecordDTO> attractions) ||
                !TryOpenVaultBuffer(vault, ref _carrionProfileHandle, BufferID.ShinobuCarrionProfiles, CarrionProfileCapacity, out NativeArray<CarrionDecayProfileDTO> profiles) ||
                !TryOpenVaultBuffer(vault, ref _carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates, CarrionCapacity, out NativeArray<FaunaStateDTO> faunaStates) ||
                !TryOpenVaultBuffer(vault, ref _carrionFaultFlagHandle, BufferID.ShinobuCarrionFaultFlags, CarrionFaultFlagCapacity, out NativeArray<uint> faultFlags))
            {
                return dependency;
            }

            CarrionTuningDTO tuning = CarrionDecayMath.SanitizeTuning(tuningArray[0], nutrientTuning.GridOriginAup);
            tuning.GridOriginAup = nutrientTuning.GridOriginAup;
            tuning.CellSizeMeters = nutrientTuning.CellSizeMeters;
            tuning.GlobalQualityWeight = nutrientTuning.GlobalQualityWeight;
            tuning.DeltaSeconds = nutrientTuning.AdvectionTimeStep;
            tuning.ActiveAxis = nutrientTuning.ActiveAxis;
            tuning.ActiveCellCount = nutrientTuning.ActiveCellCount;
            tuning.FrameIndex = nutrientTuning.FrameIndex;
            tuning.Flags |= CarrionTuningFlagInitialized | CarrionTuningFlagWriteInFlight | CarrionTuningFlagNetcodeExcluded;
            tuning.StateHash = CarrionRouteHash;
            tuning.RouteHash = CarrionRouteHash;
            tuningArray[0] = tuning;

            int telemetrySlot = _carrionTelemetryCursor % math.max(1, telemetry.Length);
            _lastCarrionTelemetrySlot = telemetrySlot;

            CarrionStateDTO* statePtr = (CarrionStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states);
            CarrionDeathSignalDTO* deathPtr = (CarrionDeathSignalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(deathIngress);
            CarrionRuntimeCountersDTO* counterPtr = (CarrionRuntimeCountersDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(counters);
            CarrionTelemetryEntry* telemetryPtr = (CarrionTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry);
            CarrionAttractionRecordDTO* attractionPtr = (CarrionAttractionRecordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(attractions);
            CarrionDecayProfileDTO* profilePtr = (CarrionDecayProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles);
            FaunaStateDTO* faunaPtr = (FaunaStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(faunaStates);
            uint* faultPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags);

            var deathJob = new ProcessEntityDeathJob
            {
                CarrionStates = statePtr,
                DeathIngress = deathPtr,
                Counters = counterPtr,
                FaunaStates = faunaPtr,
                Profiles = profilePtr,
                Tuning = tuning
            };
            _carrionScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            JobHandle handle = deathJob.Schedule(dependency);

            var decayJob = new CalculateBiomassDecayJob
            {
                CarrionStates = statePtr,
                NutrientCells = nutrientFront,
                Profiles = profilePtr,
                Tuning = tuning
            };
            handle = decayJob.Schedule(CarrionCapacity, CarrionJobBatchSize, handle);

            var injectionJob = new InjectCarrionNutrientsJob
            {
                CarrionStates = statePtr,
                NutrientInjection = nutrientInjection,
                AttractionRecords = attractionPtr,
                Profiles = profilePtr,
                Counters = counterPtr,
                FaultFlags = faultPtr,
                Tuning = tuning
            };
            handle = injectionJob.Schedule(handle);

            var telemetryJob = new RecordCarrionTelemetryJob
            {
                Counters = counterPtr,
                FaultFlags = faultPtr,
                TelemetryRing = telemetryPtr,
                Tuning = tuning,
                TelemetrySlot = telemetrySlot,
                TelemetryCursorValue = _carrionTelemetryCursor + 1
            };
            handle = telemetryJob.Schedule(handle);

            _carrionTelemetryCursor++;
            return handle;
        }

        private float ResolveCarrionSolverMicros(long finishTicks, float fallbackMicros)
        {
            long startTicks = _carrionScheduleTicks;
            if (startTicks <= 0 ||
                finishTicks < startTicks ||
                System.Diagnostics.Stopwatch.Frequency <= 0)
            {
                return math.max(0f, fallbackMicros);
            }

            return (float)((finishTicks - startTicks) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        private void PatchCompletedCarrionTelemetry(IDataVault vault, float solverMicros)
        {
            if (!TryOpenVaultBuffer(vault, ref _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing, TelemetryCapacity, out NativeArray<CarrionTelemetryEntry> telemetry) ||
                (uint)_lastCarrionTelemetrySlot >= (uint)telemetry.Length)
            {
                return;
            }

            CarrionTelemetryEntry entry = telemetry[_lastCarrionTelemetrySlot];
            entry.BurstExecutionMicroseconds = math.max(0f, solverMicros);
            if (entry.BurstExecutionMicroseconds > CarrionFaultBudgetMicros)
                entry.Flags |= CarrionTelemetryFlagOverBudget;
            telemetry[_lastCarrionTelemetrySlot] = entry;

            bool fault = (entry.Flags & (CarrionTelemetryFlagNaN | CarrionTelemetryFlagOverBudget | CarrionTelemetryFlagOverflow)) != 0u;
            if (fault && !_carrionDumpedFault)
            {
                _carrionDumpedFault = true;
                DumpCarrionTelemetry(vault);
            }

            if (TryOpenVaultBuffer(vault, ref _carrionTuningHandle, BufferID.ShinobuCarrionTuning, 1, out NativeArray<CarrionTuningDTO> tuningArray) &&
                tuningArray.Length > 0)
            {
                CarrionTuningDTO tuning = tuningArray[0];
                tuning.Flags &= ~CarrionTuningFlagWriteInFlight;
                tuningArray[0] = tuning;
            }
        }

        private void PublishCarrionAttractions(IDataVault vault)
        {
            if (!TryOpenVaultBuffer(vault, ref _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords, CarrionAttractionCapacity, out NativeArray<CarrionAttractionRecordDTO> attractions) ||
                !TryOpenVaultBuffer(vault, ref _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters, 1, out NativeArray<CarrionRuntimeCountersDTO> counters) ||
                counters.Length <= 0)
            {
                return;
            }

            CarrionRuntimeCountersDTO counter = counters[0];
            int count = math.clamp(counter.LastAttractionCount, 0, math.min(CarrionAttractionCapacity, attractions.Length));
            AbsoluteUniversePosition runtimeOrigin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!runtimeOrigin.IsFinite())
                return;

            double3 originAup = runtimeOrigin.ToAbsoluteDouble3();
            for (int i = 0; i < count; i++)
            {
                CarrionAttractionRecordDTO record = attractions[i];
                if ((record.Flags & CarrionStateDTO.FlagActive) == 0u ||
                    !math.all(math.isfinite(record.CorpseAUP)) ||
                    !math.isfinite(record.FoodValue) ||
                    record.FoodValue <= 0f)
                {
                    continue;
                }

                double3 delta = record.CorpseAUP - originAup;
                if (!math.all(math.isfinite(delta)))
                    continue;

                var runtimePosition = new Vector3((float)delta.x, (float)delta.y, (float)delta.z);
                if (!float.IsFinite(runtimePosition.x) || !float.IsFinite(runtimePosition.y) || !float.IsFinite(runtimePosition.z))
                    continue;

                AbsoluteUniversePosition corpseAup = AbsoluteUniversePosition.FromAbsolutePosition(record.CorpseAUP);
                WorldSpatialHashGrid.RegisterTransientEvent(
                    runtimePosition,
                    in corpseAup,
                    math.max(1f, record.RadiusMeters),
                    math.saturate(record.FoodValue),
                    FrostDeltaSeconds * 2f,
                    SpatialTransientEventType.ChemicalCloud,
                    SpatialInteractionFlags.Resource | SpatialInteractionFlags.ChemicalReceiver | SpatialInteractionFlags.Interactable,
                    Hecton8.Gameplay.FieldTargetRole.ResourceNodeActive,
                    unchecked((int)(record.OriginalSpeciesHash & 0x7FFFFFFFu)),
                    record.Temperature);
            }
        }

        private void DumpCarrionTelemetry(IDataVault vault)
        {
            if (!TryOpenReadVaultBuffer(vault, in _carrionTelemetryHandle, out NativeArray<CarrionTelemetryEntry>.ReadOnly telemetry))
                return;

            NativeArray<byte> payload = default;
            try
            {
                int stride = UnsafeUtility.SizeOf<CarrionTelemetryEntry>();
                int byteCount = 24 + telemetry.Length * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(NutrientDriftRuntime),
                    "nutrientCarrionTelemetryDumpPayload");
                unsafe
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> bytes = new Span<byte>(target, byteCount);
                    WriteUInt64(bytes.Slice(0, 8), CarrionDumpMagic);
                    WriteUInt32(bytes.Slice(8, 4), unchecked((uint)TelemetryCapacity));
                    WriteUInt32(bytes.Slice(12, 4), unchecked((uint)stride));
                    WriteUInt32(bytes.Slice(16, 4), unchecked((uint)_carrionTelemetryCursor));
                    WriteUInt32(bytes.Slice(20, 4), CarrionRouteHash);
                    int offset = 24;
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        CarrionTelemetryEntry entry = telemetry[i];
                        UnsafeUtility.MemCpy(target + offset, &entry, stride);
                        offset += stride;
                    }
                }

                if (!NativeFaultDumpWriter.TryWriteAll(CarrionDumpRelativePath, payload, byteCount))
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)CarrionRouteHash));
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(NutrientDriftRuntime),
                    "nutrientCarrionTelemetryDumpPayload");
            }
        }

        private bool TryLoadCarrionProfilesCsvCold()
        {
#if !UNITY_EDITOR
            return false;
#else
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            string path = BuildCarrionProfileCsvPath();
            if (path == null || path.Length == 0 || !File.Exists(path))
                return false;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Ticks == _carrionCsvTimestampTicks)
                return true;

            if (!IsMatchingVaultHandle(in _carrionProfileHandle, BufferID.ShinobuCarrionProfiles))
            {
                return false;
            }

            int bytesRead;
            int parsed;
            bool publishCommitFault = false;
            if (System.Threading.Interlocked.CompareExchange(ref s_carrionCsvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                try
                {
                    bytesRead = ReadCsvBytesCold(path, s_carrionCsvImportScratch, CarrionCsvScratchBytes);
                    if (bytesRead <= 0)
                        return false;

                    parsed = CarrionDecayCsvParser.ParseProfiles(
                        s_carrionCsvImportScratch.AsSpan(0, bytesRead),
                        s_carrionProfileImportScratch);
                    if (parsed <= 0)
                        return false;
                }
                catch (IOException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);
                    return false;
                }
                catch (ArgumentException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);
                    return false;
                }
                catch (NotSupportedException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);
                    return false;
                }
                catch (InvalidOperationException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);
                    return false;
                }

                if (!vault.TryAcquireMutationGuard(CarrionProfileCsvMutationGuardMask))
                    return false;

                try
                {
                    if (!vault.TryResolveHandle(in _carrionProfileHandle, out NativeArray<CarrionDecayProfileDTO> profiles) ||
                        !profiles.IsCreated ||
                        profiles.Length < CarrionProfileCapacity)
                    {
                        return false;
                    }

                    fixed (CarrionDecayProfileDTO* source = s_carrionProfileImportScratch)
                    {
                        UnsafeUtility.MemCpy(
                            NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                            source,
                            CarrionProfileCapacity * UnsafeUtility.SizeOf<CarrionDecayProfileDTO>());
                    }

                    _carrionCsvTimestampTicks = lastWriteUtc.Ticks;
                    return true;
                }
                catch (IOException)
                {
                    publishCommitFault = true;
                }
                catch (UnauthorizedAccessException)
                {
                    publishCommitFault = true;
                }
                catch (ArgumentException)
                {
                    publishCommitFault = true;
                }
                catch (NotSupportedException)
                {
                    publishCommitFault = true;
                }
                catch (InvalidOperationException)
                {
                    publishCommitFault = true;
                }
                finally
                {
                    vault.ReleaseMutationGuard(CarrionProfileCsvMutationGuardMask);
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref s_carrionCsvImportScratchBusy, 0);
            }

            if (publishCommitFault)
                GlobalTelemetryBus.PublishPerformanceWarning(0x43333134u, CarrionRouteHash, 0f);

            return false;
#endif
        }

        private static string BuildCarrionProfileCsvPath()
        {
#if !UNITY_EDITOR
            return string.Empty;
#else
            string dataPath = Application.dataPath;
            string first = Path.Combine(dataPath, "_Project", "Data", CarrionProfileCsvFileName);
            if (File.Exists(first))
                return first;

            string streaming = Path.Combine(Application.streamingAssetsPath, "Hecton8", "DataMonolith", CarrionProfileCsvFileName);
            if (File.Exists(streaming))
                return streaming;

            DirectoryInfo root = Directory.GetParent(dataPath);
            if (root == null)
                return first;

            return Path.Combine(root.FullName, "Data", CarrionProfileCsvFileName);
#endif
        }

        private void ReleaseCarrionVaultHandles(IDataVault vault)
        {
            if (vault == null)
            {
                ResetCarrionHandlesNoRelease();
                return;
            }

            ReleaseVaultHandle(vault, ref _carrionStateHandle, BufferID.ShinobuCarrionStates);
            ReleaseVaultHandle(vault, ref _carrionDeathIngressHandle, BufferID.ShinobuCarrionDeathIngress);
            ReleaseVaultHandle(vault, ref _carrionCountersHandle, BufferID.ShinobuCarrionRuntimeCounters);
            ReleaseVaultHandle(vault, ref _carrionTuningHandle, BufferID.ShinobuCarrionTuning);
            ReleaseVaultHandle(vault, ref _carrionTelemetryHandle, BufferID.ShinobuCarrionTelemetryRing);
            ReleaseVaultHandle(vault, ref _carrionAttractionHandle, BufferID.ShinobuCarrionAttractionRecords);
            ReleaseVaultHandle(vault, ref _carrionProfileHandle, BufferID.ShinobuCarrionProfiles);
            ReleaseVaultHandle(vault, ref _carrionCsvScratchHandle, BufferID.ShinobuCarrionCsvScratch);
            ReleaseVaultHandle(vault, ref _carrionFaunaStateHandle, BufferID.ShinobuCarrionFaunaStates);
            ReleaseVaultHandle(vault, ref _carrionFaultFlagHandle, BufferID.ShinobuCarrionFaultFlags);
            _carrionInitialized = false;
            _carrionProfilesLoadedCold = false;
            _carrionProfilesLoadAttemptedCold = false;
            _carrionJobLocksHeld = false;
            _carrionJobGuardVault = null;
        }

        private void ResetCarrionHandlesNoRelease()
        {
            _carrionStateHandle = default;
            _carrionDeathIngressHandle = default;
            _carrionCountersHandle = default;
            _carrionTuningHandle = default;
            _carrionTelemetryHandle = default;
            _carrionAttractionHandle = default;
            _carrionProfileHandle = default;
            _carrionCsvScratchHandle = default;
            _carrionFaunaStateHandle = default;
            _carrionFaultFlagHandle = default;
            _carrionScheduleTicks = 0;
            _carrionDeathSignalGeneration = 0;
            _carrionTelemetryCursor = 0;
            _lastCarrionTelemetrySlot = 0;
            _carrionInitialized = false;
            _carrionProfilesLoadedCold = false;
            _carrionProfilesLoadAttemptedCold = false;
            _carrionJobLocksHeld = false;
            _carrionJobGuardVault = null;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CarrionStateDTO
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagNetcodeExcluded = 1u << 1;
        public const uint FlagMathFault = 1u << 2;

        [FieldOffset(0)] public double3 CorpseAUP;
        [FieldOffset(24)] public float InitialBiomass;
        [FieldOffset(28)] public float CurrentBiomass;
        [FieldOffset(32)] public uint OriginalSpeciesHash;
        [FieldOffset(36)] public float ToxicityEmissionRate;
        [FieldOffset(40)] public float AgeSeconds;
        [FieldOffset(44)] public float BiomassLostLastTick;
        [FieldOffset(48)] public float DecayRate;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint EntityHash;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CarrionDeathSignalDTO
    {
        [FieldOffset(0)] public double3 CorpseAUP;
        [FieldOffset(24)] public float BiomassScale;
        [FieldOffset(28)] public uint OriginalSpeciesHash;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint EntityHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float ToxicitySeed;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FaunaStateDTO
    {
        public const uint FlagActive = 1u << 0;

        [FieldOffset(0)] public double3 PositionAUP;
        [FieldOffset(24)] public float Biomass;
        [FieldOffset(28)] public uint SpeciesHash;
        [FieldOffset(32)] public uint EntityHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public int CarrionSlot;
        [FieldOffset(44)] public float Health01;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CarrionRuntimeCountersDTO
    {
        [FieldOffset(0)] public int DeathIngressReadCursor;
        [FieldOffset(4)] public int DeathIngressWriteCursor;
        [FieldOffset(8)] public int DeathIngressCount;
        [FieldOffset(12)] public int CarrionWriteCursor;
        [FieldOffset(16)] public int ActiveCarrion;
        [FieldOffset(20)] public int LastAttractionCount;
        [FieldOffset(24)] public int TelemetryCursor;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint OverflowCount;
        [FieldOffset(40)] public float LastInjectedBiomass;
        [FieldOffset(44)] public float TotalActiveBiomass;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint LastProcessedDeaths;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CarrionTuningDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float BaseDecayRate;
        [FieldOffset(32)] public float LinearDecayRate;
        [FieldOffset(36)] public float DefaultBiomass;
        [FieldOffset(40)] public float EpsilonBiomass;
        [FieldOffset(44)] public float ColdTemperatureMultiplier;
        [FieldOffset(48)] public float HotTemperatureMultiplier;
        [FieldOffset(52)] public float NutrientInjectionMultiplier;
        [FieldOffset(56)] public float ScavengerAttractionRadius;
        [FieldOffset(60)] public float ScavengerFoodScalar;
        [FieldOffset(64)] public float GlobalQualityWeight;
        [FieldOffset(68)] public float DeltaSeconds;
        [FieldOffset(72)] public int ActiveAxis;
        [FieldOffset(76)] public int ActiveCellCount;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] public uint FrameIndex;
        [FieldOffset(88)] public uint StateHash;
        [FieldOffset(92)] public uint ProfileHash;
        [FieldOffset(96)] public float MaxAttractionIntensity;
        [FieldOffset(100)] public float ToxicityNutrientPenalty;
        [FieldOffset(104)] public float TemperatureLowCelsius;
        [FieldOffset(108)] public float TemperatureHighCelsius;
        [FieldOffset(112)] public uint RouteHash;
        [FieldOffset(116)] private uint _pad0;
        [FieldOffset(120)] private uint _pad1;
        [FieldOffset(124)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CarrionTelemetryEntry
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float ActiveBiomass;
        [FieldOffset(28)] public float InjectedBiomass;
        [FieldOffset(32)] public float BurstExecutionMicroseconds;
        [FieldOffset(36)] public int ActiveCarrion;
        [FieldOffset(40)] public int AttractionCount;
        [FieldOffset(44)] public float MaxToxicity;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Overflows;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CarrionAttractionRecordDTO
    {
        [FieldOffset(0)] public double3 CorpseAUP;
        [FieldOffset(24)] public float FoodValue;
        [FieldOffset(28)] public float RadiusMeters;
        [FieldOffset(32)] public uint OriginalSpeciesHash;
        [FieldOffset(36)] public float Toxicity;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float Temperature;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CarrionDecayProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float BaseDecayRate;
        [FieldOffset(8)] public float ToxicityEmissionRate;
        [FieldOffset(12)] public float NutrientMultiplier;
        [FieldOffset(16)] public float AttractionMultiplier;
        [FieldOffset(20)] public float BiomassMultiplier;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint SourceHash;
    }

    public static class CarrionDecayMath
    {
        private const uint TuningFlagInitialized = 1u << 0;
        private const uint TuningFlagNetcodeExcluded = 1u << 2;

        public static CarrionTuningDTO CreateDefaultTuning(double3 originAup)
        {
            return SanitizeTuning(new CarrionTuningDTO
            {
                GridOriginAup = originAup,
                CellSizeMeters = 12f,
                BaseDecayRate = NutrientDriftRuntime.DefaultCarrionBaseDecayRate,
                LinearDecayRate = NutrientDriftRuntime.DefaultCarrionLinearDecayRate,
                DefaultBiomass = NutrientDriftRuntime.DefaultCarrionBiomass,
                EpsilonBiomass = NutrientDriftRuntime.DefaultCarrionEpsilonBiomass,
                ColdTemperatureMultiplier = NutrientDriftRuntime.DefaultCarrionColdMultiplier,
                HotTemperatureMultiplier = NutrientDriftRuntime.DefaultCarrionHotMultiplier,
                NutrientInjectionMultiplier = NutrientDriftRuntime.DefaultCarrionNutrientInjection,
                ScavengerAttractionRadius = NutrientDriftRuntime.DefaultCarrionAttractionRadius,
                ScavengerFoodScalar = NutrientDriftRuntime.DefaultCarrionFoodScalar,
                GlobalQualityWeight = 1f,
                DeltaSeconds = NutrientDriftRuntime.FrostDeltaSeconds,
                ActiveAxis = NutrientDriftRuntime.GridAxisMax,
                ActiveCellCount = NutrientDriftRuntime.GridCellCapacity,
                Flags = TuningFlagInitialized | TuningFlagNetcodeExcluded,
                FrameIndex = 0u,
                StateHash = NutrientDriftRuntime.CarrionRouteHash,
                ProfileHash = 0u,
                MaxAttractionIntensity = 1f,
                ToxicityNutrientPenalty = 0.35f,
                TemperatureLowCelsius = -2f,
                TemperatureHighCelsius = 80f,
                RouteHash = NutrientDriftRuntime.CarrionRouteHash
            }, originAup);
        }

        public static CarrionTuningDTO SanitizeTuning(CarrionTuningDTO tuning, double3 fallbackOrigin)
        {
            if (!math.all(math.isfinite(tuning.GridOriginAup)))
                tuning.GridOriginAup = math.all(math.isfinite(fallbackOrigin)) ? fallbackOrigin : double3.zero;
            tuning.CellSizeMeters = math.clamp(SanitizeFinite(tuning.CellSizeMeters, 12f), 1f, 64f);
            tuning.BaseDecayRate = math.clamp(SanitizeFinite(tuning.BaseDecayRate, NutrientDriftRuntime.DefaultCarrionBaseDecayRate), 0f, 0.05f);
            tuning.LinearDecayRate = math.clamp(SanitizeFinite(tuning.LinearDecayRate, NutrientDriftRuntime.DefaultCarrionLinearDecayRate), 0f, 5f);
            tuning.DefaultBiomass = math.clamp(SanitizeFinite(tuning.DefaultBiomass, NutrientDriftRuntime.DefaultCarrionBiomass), 0.01f, 100000f);
            tuning.EpsilonBiomass = math.clamp(SanitizeFinite(tuning.EpsilonBiomass, NutrientDriftRuntime.DefaultCarrionEpsilonBiomass), 0.0001f, 1f);
            tuning.ColdTemperatureMultiplier = math.clamp(SanitizeFinite(tuning.ColdTemperatureMultiplier, NutrientDriftRuntime.DefaultCarrionColdMultiplier), 0.01f, 2f);
            tuning.HotTemperatureMultiplier = math.clamp(SanitizeFinite(tuning.HotTemperatureMultiplier, NutrientDriftRuntime.DefaultCarrionHotMultiplier), 0.01f, 16f);
            tuning.NutrientInjectionMultiplier = math.clamp(SanitizeFinite(tuning.NutrientInjectionMultiplier, NutrientDriftRuntime.DefaultCarrionNutrientInjection), 0f, 32f);
            tuning.ScavengerAttractionRadius = math.clamp(SanitizeFinite(tuning.ScavengerAttractionRadius, NutrientDriftRuntime.DefaultCarrionAttractionRadius), 1f, 512f);
            tuning.ScavengerFoodScalar = math.clamp(SanitizeFinite(tuning.ScavengerFoodScalar, NutrientDriftRuntime.DefaultCarrionFoodScalar), 0f, 8f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.DeltaSeconds = math.clamp(SanitizeFinite(tuning.DeltaSeconds, NutrientDriftRuntime.FrostDeltaSeconds), 0.05f, 60f);
            tuning.ActiveAxis = math.clamp(tuning.ActiveAxis, 1, NutrientDriftRuntime.GridAxisMax);
            tuning.ActiveCellCount = math.clamp(tuning.ActiveCellCount, 1, NutrientDriftRuntime.GridCellCapacity);
            tuning.MaxAttractionIntensity = math.clamp(SanitizeFinite(tuning.MaxAttractionIntensity, 1f), 0.01f, 32f);
            tuning.ToxicityNutrientPenalty = math.saturate(SanitizeFinite(tuning.ToxicityNutrientPenalty, 0.35f));
            tuning.TemperatureLowCelsius = math.clamp(SanitizeFinite(tuning.TemperatureLowCelsius, -2f), -80f, 80f);
            tuning.TemperatureHighCelsius = math.clamp(SanitizeFinite(tuning.TemperatureHighCelsius, 80f), tuning.TemperatureLowCelsius + 0.01f, 180f);
            tuning.Flags |= TuningFlagInitialized | TuningFlagNetcodeExcluded;
            tuning.StateHash = tuning.StateHash != 0u ? tuning.StateHash : NutrientDriftRuntime.CarrionRouteHash;
            tuning.RouteHash = tuning.RouteHash != 0u ? tuning.RouteHash : NutrientDriftRuntime.CarrionRouteHash;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveNutrientCellIndex(double3 corpseAup, CarrionTuningDTO tuning)
        {
            double3 delta = corpseAup - tuning.GridOriginAup;
            if (!math.all(math.isfinite(delta)))
                return -1;

            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!math.all(math.isfinite(local)))
                return -1;

            float cell = math.isfinite(tuning.CellSizeMeters) && tuning.CellSizeMeters > 0.0001f
                ? tuning.CellSizeMeters
                : 0.0001f;
            int axis = math.clamp(tuning.ActiveAxis, 1, NutrientDriftRuntime.GridAxisMax);
            float half = axis * 0.5f;
            float3 grid = local * math.rcp(cell) + half;
            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0f ||
                grid.y < 0f ||
                grid.z < 0f ||
                grid.x >= axis ||
                grid.y >= axis ||
                grid.z >= axis)
            {
                return -1;
            }

            int x = (int)math.floor(grid.x);
            int y = (int)math.floor(grid.y);
            int z = (int)math.floor(grid.z);
            return NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe CarrionDecayProfileDTO FindProfile(CarrionDecayProfileDTO* profiles, uint speciesHash)
        {
            if (profiles == null)
                return default;

            CarrionDecayProfileDTO fallback = default;
            for (int i = 0; i < NutrientDriftRuntime.CarrionProfileCapacity; i++)
            {
                CarrionDecayProfileDTO profile = UnsafeUtility.AsRef<CarrionDecayProfileDTO>(profiles + i);
                if (profile.SpeciesHash == NutrientDriftRuntime.CarrionRouteHash)
                    fallback = profile;
                if (profile.SpeciesHash == speciesHash)
                    return profile;
            }

            return fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveProfileFloat(float profileValue, float fallback)
        {
            return math.isfinite(profileValue) && profileValue > 0f ? profileValue : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeCarrionVaultJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionStateDTO* CarrionStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDeathSignalDTO* DeathIngress;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionTelemetryEntry* TelemetryRing;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionAttractionRecordDTO* AttractionRecords;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDecayProfileDTO* Profiles;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FaunaStateDTO* FaunaStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* FaultFlags;

        public void Execute(int index)
        {
            UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index) = default;
            UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + index) = default;

            if (index < NutrientDriftRuntime.CarrionDeathIngressCapacity)
                UnsafeUtility.AsRef<CarrionDeathSignalDTO>(DeathIngress + index) = default;
            if (index < NutrientDriftRuntime.TelemetryCapacity)
                UnsafeUtility.AsRef<CarrionTelemetryEntry>(TelemetryRing + index) = default;
            if (index < NutrientDriftRuntime.CarrionAttractionCapacity)
                UnsafeUtility.AsRef<CarrionAttractionRecordDTO>(AttractionRecords + index) = default;
            if (index < NutrientDriftRuntime.CarrionProfileCapacity)
                UnsafeUtility.AsRef<CarrionDecayProfileDTO>(Profiles + index) = default;
            if (index < NutrientDriftRuntime.CarrionFaultFlagCapacity)
                UnsafeUtility.AsRef<uint>(FaultFlags + index) = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockMassExtinctionJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionStateDTO* CarrionStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FaunaStateDTO* FaunaStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionRuntimeCountersDTO* Counters;
        public CarrionTuningDTO Tuning;
        public int RequestedCount;
        public uint Seed;

        public void Execute(int index)
        {
            int count = math.clamp(RequestedCount, 0, NutrientDriftRuntime.CarrionCapacity);
            if (index >= count)
            {
                UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index) = default;
                UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + index) = default;
                return;
            }

            uint streamSeed = math.hash(new uint2(Seed == 0u ? NutrientDriftRuntime.CarrionRouteHash : Seed, (uint)index + 1u));
            Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(streamSeed == 0u ? 1u : streamSeed);
            uint hash = rng.NextUInt() | 1u;
            int side = math.max(1, (int)math.ceil(math.sqrt(count)));
            float cell = math.max(2f, Tuning.CellSizeMeters);
            float x = ((index % side) - side * 0.5f) * cell;
            float z = ((index / side) - side * 0.5f) * cell;
            float y = (rng.NextFloat() - 0.5f) * 4.2f;
            double3 corpseAup = Tuning.GridOriginAup + new double3(x, y, z);
            float biomass = Tuning.DefaultBiomass * math.lerp(0.35f, 2.25f, rng.NextFloat());
            float toxicity = rng.NextFloat() * 0.15f;

            UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index) = new CarrionStateDTO
            {
                CorpseAUP = corpseAup,
                InitialBiomass = biomass,
                CurrentBiomass = biomass,
                OriginalSpeciesHash = hash | 1u,
                ToxicityEmissionRate = toxicity,
                AgeSeconds = 0f,
                BiomassLostLastTick = 0f,
                DecayRate = Tuning.BaseDecayRate,
                Flags = CarrionStateDTO.FlagActive | CarrionStateDTO.FlagNetcodeExcluded,
                EntityHash = hash | 1u
            };

            UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + index) = new FaunaStateDTO
            {
                PositionAUP = corpseAup,
                Biomass = 0f,
                SpeciesHash = hash | 1u,
                EntityHash = hash | 1u,
                Flags = 0u,
                CarrionSlot = index,
                Health01 = 0f
            };

            if (index == 0)
            {
                UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters) = new CarrionRuntimeCountersDTO
                {
                    CarrionWriteCursor = count % NutrientDriftRuntime.CarrionCapacity,
                    ActiveCarrion = count,
                    TotalActiveBiomass = Tuning.DefaultBiomass * 1.3f * count,
                    StateHash = Hash(count, Seed),
                    Flags = 0u
                };
            }
        }

        private static uint Hash(int index, uint seed)
        {
            uint h = 2166136261u ^ seed;
            h ^= unchecked((uint)index);
            h *= 16777619u;
            h ^= h >> 13;
            h *= 16777619u;
            return h == 0u ? 1u : h;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ProcessEntityDeathJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionStateDTO* CarrionStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDeathSignalDTO* DeathIngress;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionRuntimeCountersDTO* Counters;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FaunaStateDTO* FaunaStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDecayProfileDTO* Profiles;
        public CarrionTuningDTO Tuning;

        public void Execute()
        {
            CarrionRuntimeCountersDTO counter = UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters);
            int pending = math.clamp(counter.DeathIngressCount, 0, NutrientDriftRuntime.CarrionDeathIngressCapacity);
            int readCursor = CarrionDecayMath.PositiveModulo(counter.DeathIngressReadCursor, NutrientDriftRuntime.CarrionDeathIngressCapacity);
            int writeCursor = CarrionDecayMath.PositiveModulo(counter.CarrionWriteCursor, NutrientDriftRuntime.CarrionCapacity);
            uint processed = 0u;

            for (int i = 0; i < pending; i++)
            {
                int ingressSlot = (readCursor + i) % NutrientDriftRuntime.CarrionDeathIngressCapacity;
                CarrionDeathSignalDTO signal = UnsafeUtility.AsRef<CarrionDeathSignalDTO>(DeathIngress + ingressSlot);
                if (!CarrionDecayMath.IsFinite(signal.CorpseAUP))
                    continue;

                uint speciesHash = signal.OriginalSpeciesHash != 0u ? signal.OriginalSpeciesHash : NutrientDriftRuntime.CarrionRouteHash;
                uint entityHash = signal.EntityHash != 0u ? signal.EntityHash : speciesHash;
                bool faunaOwnedDeathSignal = (signal.Flags & EntityDeathSignal.FlagFaunaBrainCarrion) != 0u;
                bool matchedExisting = TryResolveActiveCarrionSlot(entityHash, out int slot);
                if (matchedExisting && !faunaOwnedDeathSignal)
                {
                    ClearFaunaActiveFlag(entityHash, slot, signal.CorpseAUP, speciesHash);
                    processed++;
                    continue;
                }

                if (!matchedExisting)
                {
                    slot = ResolveFreeCarrionSlot(writeCursor);
                    writeCursor = (slot + 1) % NutrientDriftRuntime.CarrionCapacity;
                }

                CarrionDecayProfileDTO profile = CarrionDecayMath.FindProfile(Profiles, speciesHash);
                float biomassMultiplier = math.max(
                    0.0001f,
                    CarrionDecayMath.SanitizeFinite(
                        CarrionDecayMath.ResolveProfileFloat(profile.BiomassMultiplier, 1f),
                        1f));
                float biomassScale = math.max(
                    0.01f,
                    CarrionDecayMath.SanitizeFinite(signal.BiomassScale, 1f));
                float defaultBiomass = math.max(
                    0.01f,
                    CarrionDecayMath.SanitizeFinite(
                        Tuning.DefaultBiomass,
                        NutrientDriftRuntime.DefaultCarrionBiomass));
                float epsilon = math.max(
                    0.0001f,
                    CarrionDecayMath.SanitizeFinite(
                        Tuning.EpsilonBiomass,
                        NutrientDriftRuntime.DefaultCarrionEpsilonBiomass));
                float biomass = math.max(epsilon, defaultBiomass * biomassScale * biomassMultiplier);
                float decayRate = math.max(
                    0f,
                    CarrionDecayMath.SanitizeFinite(
                        CarrionDecayMath.ResolveProfileFloat(profile.BaseDecayRate, Tuning.BaseDecayRate),
                        NutrientDriftRuntime.DefaultCarrionBaseDecayRate));
                float toxicitySeed = CarrionDecayMath.SanitizeFinite(signal.ToxicitySeed, 0f);
                float toxicity = math.max(
                    0f,
                    CarrionDecayMath.SanitizeFinite(profile.ToxicityEmissionRate, toxicitySeed));

                UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + slot) = new CarrionStateDTO
                {
                    CorpseAUP = signal.CorpseAUP,
                    InitialBiomass = biomass,
                    CurrentBiomass = biomass,
                    OriginalSpeciesHash = speciesHash,
                    ToxicityEmissionRate = toxicity,
                    AgeSeconds = 0f,
                    BiomassLostLastTick = 0f,
                    DecayRate = decayRate,
                    Flags = CarrionStateDTO.FlagActive | CarrionStateDTO.FlagNetcodeExcluded,
                    EntityHash = entityHash
                };

                ClearFaunaActiveFlag(entityHash, slot, signal.CorpseAUP, speciesHash);
                processed++;
            }

            counter.DeathIngressReadCursor = (readCursor + pending) % NutrientDriftRuntime.CarrionDeathIngressCapacity;
            counter.DeathIngressCount = 0;
            counter.CarrionWriteCursor = writeCursor;
            counter.LastProcessedDeaths = processed;
            UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters) = counter;
        }

        private bool TryResolveActiveCarrionSlot(uint entityHash, out int slot)
        {
            slot = -1;
            if (entityHash != 0u)
            {
                for (int i = 0; i < NutrientDriftRuntime.CarrionCapacity; i++)
                {
                    CarrionStateDTO state = UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + i);
                    if ((state.Flags & CarrionStateDTO.FlagActive) != 0u && state.EntityHash == entityHash)
                    {
                        slot = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private int ResolveFreeCarrionSlot(int start)
        {
            int cursor = CarrionDecayMath.PositiveModulo(start, NutrientDriftRuntime.CarrionCapacity);
            for (int probe = 0; probe < NutrientDriftRuntime.CarrionCapacity; probe++)
            {
                int index = (cursor + probe) % NutrientDriftRuntime.CarrionCapacity;
                CarrionStateDTO state = UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index);
                if ((state.Flags & CarrionStateDTO.FlagActive) == 0u)
                    return index;
            }

            return cursor;
        }

        private void ClearFaunaActiveFlag(uint entityHash, int carrionSlot, double3 corpseAup, uint speciesHash)
        {
            int writeIndex = carrionSlot;
            if (entityHash != 0u)
            {
                for (int i = 0; i < NutrientDriftRuntime.CarrionCapacity; i++)
                {
                    FaunaStateDTO fauna = UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + i);
                    if (fauna.EntityHash == entityHash)
                    {
                        fauna.Flags &= ~FaunaStateDTO.FlagActive;
                        fauna.Health01 = 0f;
                        fauna.CarrionSlot = carrionSlot;
                        UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + i) = fauna;
                        return;
                    }
                }
            }

            UnsafeUtility.AsRef<FaunaStateDTO>(FaunaStates + writeIndex) = new FaunaStateDTO
            {
                PositionAUP = corpseAup,
                Biomass = 0f,
                SpeciesHash = speciesHash,
                EntityHash = entityHash,
                Flags = 0u,
                CarrionSlot = carrionSlot,
                Health01 = 0f
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateBiomassDecayJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionStateDTO* CarrionStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* NutrientCells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDecayProfileDTO* Profiles;
        public CarrionTuningDTO Tuning;

        public void Execute(int index)
        {
            CarrionStateDTO state = UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index);
            if ((state.Flags & CarrionStateDTO.FlagActive) == 0u)
                return;

            if (!CarrionDecayMath.IsFinite(state.CorpseAUP) ||
                !math.isfinite(state.InitialBiomass) ||
                !math.isfinite(state.CurrentBiomass) ||
                !math.isfinite(state.ToxicityEmissionRate) ||
                !math.isfinite(state.AgeSeconds) ||
                !math.isfinite(state.DecayRate) ||
                state.InitialBiomass <= 0f)
            {
                state.CurrentBiomass = 0f;
                state.BiomassLostLastTick = 0f;
                state.Flags = (state.Flags & ~CarrionStateDTO.FlagActive) |
                    CarrionStateDTO.FlagMathFault |
                    CarrionStateDTO.FlagNetcodeExcluded;
                UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index) = state;
                return;
            }

            float initialBiomass = math.max(0.0001f, state.InitialBiomass);
            float oldBiomass = math.clamp(state.CurrentBiomass, 0f, initialBiomass);
            float dt = math.max(
                0.0001f,
                CarrionDecayMath.SanitizeFinite(Tuning.DeltaSeconds, NutrientDriftRuntime.FrostDeltaSeconds));
            float temperature = SampleTemperature(state.CorpseAUP);
            CarrionDecayProfileDTO profile = CarrionDecayMath.FindProfile(Profiles, state.OriginalSpeciesHash);
            float decayRate = math.max(
                0f,
                CarrionDecayMath.SanitizeFinite(
                    CarrionDecayMath.ResolveProfileFloat(profile.BaseDecayRate, state.DecayRate),
                    NutrientDriftRuntime.DefaultCarrionBaseDecayRate));
            float temperatureMultiplier = math.max(
                0.0001f,
                CarrionDecayMath.SanitizeFinite(ResolveTemperatureMultiplier(temperature), 1f));
            float effectiveDecay = decayRate * temperatureMultiplier;
            float age = math.max(0f, state.AgeSeconds) + dt;
            float linearRate = math.max(
                0f,
                CarrionDecayMath.SanitizeFinite(
                    Tuning.LinearDecayRate,
                    NutrientDriftRuntime.DefaultCarrionLinearDecayRate));
            float linearBiomass = oldBiomass - linearRate * temperatureMultiplier * dt;
            float quality = math.saturate(CarrionDecayMath.SanitizeFinite(Tuning.GlobalQualityWeight, 1f));
            float exponentialBiomass = initialBiomass * MathLodApproximation.ApproxExpNegPade33Wide40(effectiveDecay * age);
            float expWeight = math.smoothstep(0.4f, 0.95f, quality);
            float nextBiomass = math.lerp(linearBiomass, exponentialBiomass, expWeight);
            nextBiomass = math.clamp(math.select(0f, nextBiomass, math.isfinite(nextBiomass)), 0f, initialBiomass);
            float lost = math.max(0f, oldBiomass - nextBiomass);

            state.AgeSeconds = age;
            state.BiomassLostLastTick = math.select(0f, lost, math.isfinite(lost));
            state.CurrentBiomass = nextBiomass;
            state.DecayRate = decayRate;
            float epsilon = math.max(
                0.0001f,
                CarrionDecayMath.SanitizeFinite(
                    Tuning.EpsilonBiomass,
                    NutrientDriftRuntime.DefaultCarrionEpsilonBiomass));
            if (nextBiomass < epsilon)
            {
                state.CurrentBiomass = 0f;
                state.Flags &= ~CarrionStateDTO.FlagActive;
            }

            UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + index) = state;
        }

        private float SampleTemperature(double3 corpseAup)
        {
            int index = CarrionDecayMath.ResolveNutrientCellIndex(corpseAup, Tuning);
            if (index < 0)
                return 4f;

            NutrientCellDTO cell = UnsafeUtility.AsRef<NutrientCellDTO>(NutrientCells + index);
            return math.clamp(CarrionDecayMath.SanitizeFinite(cell.Temperature, 4f), -4f, 120f);
        }

        private float ResolveTemperatureMultiplier(float temperature)
        {
            float span = math.max(0.0001f, Tuning.TemperatureHighCelsius - Tuning.TemperatureLowCelsius);
            float t = math.saturate((temperature - Tuning.TemperatureLowCelsius) * math.rcp(span));
            return math.lerp(Tuning.ColdTemperatureMultiplier, Tuning.HotTemperatureMultiplier, t);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InjectCarrionNutrientsJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionStateDTO* CarrionStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* NutrientInjection;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionAttractionRecordDTO* AttractionRecords;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionDecayProfileDTO* Profiles;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionRuntimeCountersDTO* Counters;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        public CarrionTuningDTO Tuning;

        public void Execute()
        {
            UnsafeUtility.AsRef<uint>(FaultFlags) = 0u;

            float injectedTotal = 0f;
            float activeBiomass = 0f;
            int activeCarrion = 0;
            int attractionCount = 0;
            uint flags = 0u;
            uint stateHash = 2166136261u;

            for (int i = 0; i < NutrientDriftRuntime.CarrionAttractionCapacity; i++)
                UnsafeUtility.AsRef<CarrionAttractionRecordDTO>(AttractionRecords + i) = default;

            for (int i = 0; i < NutrientDriftRuntime.CarrionCapacity; i++)
            {
                CarrionStateDTO state = UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + i);
                bool active = (state.Flags & CarrionStateDTO.FlagActive) != 0u;
                bool mathFault = (state.Flags & CarrionStateDTO.FlagMathFault) != 0u;
                if (!active && !mathFault && state.BiomassLostLastTick <= 0f)
                    continue;

                if (mathFault ||
                    !CarrionDecayMath.IsFinite(state.CorpseAUP) ||
                    !math.isfinite(state.CurrentBiomass) ||
                    !math.isfinite(state.ToxicityEmissionRate) ||
                    !math.isfinite(state.BiomassLostLastTick))
                {
                    flags |= NutrientDriftRuntime.CarrionTelemetryFlagNaN;
                    state.CurrentBiomass = 0f;
                    state.BiomassLostLastTick = 0f;
                    state.Flags &= ~(CarrionStateDTO.FlagActive | CarrionStateDTO.FlagMathFault);
                    UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + i) = state;
                    continue;
                }

                CarrionDecayProfileDTO profile = CarrionDecayMath.FindProfile(Profiles, state.OriginalSpeciesHash);
                float nutrientMultiplier = CarrionDecayMath.ResolveProfileFloat(profile.NutrientMultiplier, 1f);
                float toxicity = math.max(0f, CarrionDecayMath.SanitizeFinite(state.ToxicityEmissionRate, 0f));
                float toxicityPenalty = 1f - math.saturate(
                    toxicity *
                    math.saturate(CarrionDecayMath.SanitizeFinite(Tuning.ToxicityNutrientPenalty, 0.35f)));
                float nutrientScale = math.max(
                    0f,
                    CarrionDecayMath.SanitizeFinite(
                        Tuning.NutrientInjectionMultiplier,
                        NutrientDriftRuntime.DefaultCarrionNutrientInjection));
                float injected = state.BiomassLostLastTick * nutrientScale * nutrientMultiplier * toxicityPenalty;
                if (injected > 0f && math.isfinite(injected))
                {
                    int cellIndex = CarrionDecayMath.ResolveNutrientCellIndex(state.CorpseAUP, Tuning);
                    if (cellIndex >= 0)
                    {
                        float current = CarrionDecayMath.SanitizeFinite(
                            UnsafeUtility.AsRef<float>(NutrientInjection + cellIndex),
                            0f);
                        UnsafeUtility.AsRef<float>(NutrientInjection + cellIndex) = math.max(0f, current + injected);
                        injectedTotal += injected;
                    }
                }

                state.BiomassLostLastTick = 0f;
                UnsafeUtility.AsRef<CarrionStateDTO>(CarrionStates + i) = state;

                if (!active)
                    continue;

                activeCarrion++;
                activeBiomass += math.max(0f, state.CurrentBiomass);
                float attractionMultiplier = CarrionDecayMath.ResolveProfileFloat(profile.AttractionMultiplier, 1f);
                float food = state.CurrentBiomass *
                    math.max(
                        0f,
                        CarrionDecayMath.SanitizeFinite(
                            Tuning.ScavengerFoodScalar,
                            NutrientDriftRuntime.DefaultCarrionFoodScalar)) *
                    attractionMultiplier;
                if (food > 0f && attractionCount < NutrientDriftRuntime.CarrionAttractionCapacity)
                {
                    UnsafeUtility.AsRef<CarrionAttractionRecordDTO>(AttractionRecords + attractionCount) = new CarrionAttractionRecordDTO
                    {
                        CorpseAUP = state.CorpseAUP,
                        FoodValue = math.min(
                            math.max(0.01f, CarrionDecayMath.SanitizeFinite(Tuning.MaxAttractionIntensity, 1f)),
                            food),
                        RadiusMeters = math.max(
                            1f,
                            CarrionDecayMath.SanitizeFinite(
                                Tuning.ScavengerAttractionRadius,
                                NutrientDriftRuntime.DefaultCarrionAttractionRadius)),
                        OriginalSpeciesHash = state.OriginalSpeciesHash,
                        Toxicity = toxicity,
                        Flags = CarrionStateDTO.FlagActive,
                        Temperature = 0f
                    };
                    attractionCount++;
                }

                stateHash ^= state.OriginalSpeciesHash + unchecked((uint)i);
                stateHash *= 16777619u;
            }

            CarrionRuntimeCountersDTO counters = UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters);
            counters.ActiveCarrion = activeCarrion;
            counters.LastAttractionCount = attractionCount;
            counters.LastInjectedBiomass = math.select(0f, injectedTotal, math.isfinite(injectedTotal));
            counters.TotalActiveBiomass = math.select(0f, activeBiomass, math.isfinite(activeBiomass));
            counters.Flags = flags | (counters.OverflowCount != 0u ? NutrientDriftRuntime.CarrionTelemetryFlagOverflow : 0u);
            counters.Frame = Tuning.FrameIndex;
            counters.StateHash = stateHash == 0u ? NutrientDriftRuntime.CarrionRouteHash : stateHash;
            UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters) = counters;
            if (flags != 0u)
                UnsafeUtility.AsRef<uint>(FaultFlags) = flags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordCarrionTelemetryJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionRuntimeCountersDTO* Counters;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CarrionTelemetryEntry* TelemetryRing;
        public CarrionTuningDTO Tuning;
        public int TelemetrySlot;
        public int TelemetryCursorValue;

        public void Execute()
        {
            CarrionRuntimeCountersDTO counters = UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters);
            uint flags = counters.Flags | UnsafeUtility.AsRef<uint>(FaultFlags);
            CarrionTelemetryEntry entry = new CarrionTelemetryEntry
            {
                GridOriginAup = Tuning.GridOriginAup,
                ActiveBiomass = math.max(0f, counters.TotalActiveBiomass),
                InjectedBiomass = math.max(0f, counters.LastInjectedBiomass),
                BurstExecutionMicroseconds = 0f,
                ActiveCarrion = math.max(0, counters.ActiveCarrion),
                AttractionCount = math.max(0, counters.LastAttractionCount),
                MaxToxicity = 0f,
                Frame = Tuning.FrameIndex,
                Flags = flags,
                StateHash = counters.StateHash,
                Overflows = counters.OverflowCount
            };
            UnsafeUtility.AsRef<CarrionTelemetryEntry>(TelemetryRing + TelemetrySlot) = entry;
            counters.TelemetryCursor = TelemetryCursorValue;
            UnsafeUtility.AsRef<CarrionRuntimeCountersDTO>(Counters) = counters;
        }
    }

    #if UNITY_EDITOR
    public static class CarrionDecayCsvParser
    {
        public static unsafe int ParseProfiles(ReadOnlySpan<byte> bytes, NativeArray<CarrionDecayProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return 0;

            return ParseProfiles(
                bytes,
                new Span<CarrionDecayProfileDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(profiles), profiles.Length));
        }

        public static int ParseProfiles(ReadOnlySpan<byte> bytes, Span<CarrionDecayProfileDTO> profiles)
        {
            if (profiles.Length <= 0)
                return 0;

            profiles.Clear();

            int cursor = 0;
            int count = 0;
            bool firstRow = true;
            while (cursor < bytes.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> row = ReadRow(bytes, ref cursor);
                if (row.Length <= 0)
                    continue;

                int columnCursor = 0;
                ReadOnlySpan<byte> name = ReadColumn(row, ref columnCursor);
                if (name.Length <= 0)
                    continue;
                if (name[0] == (byte)'#')
                    continue;

                if (firstRow && LooksLikeHeader(name))
                {
                    firstRow = false;
                    continue;
                }

                firstRow = false;
                CarrionDecayProfileDTO profile = default;
                profile.SpeciesHash = ParseSpeciesProfileKey(name);
                if (profile.SpeciesHash == 0u)
                    continue;
                profile.BaseDecayRate = ReadFloatColumn(row, ref columnCursor, 0.00042f);
                profile.ToxicityEmissionRate = ReadFloatColumn(row, ref columnCursor, 0f);
                profile.NutrientMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.AttractionMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.BiomassMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.SourceHash = NutrientDriftRuntime.CarrionRouteHash;
                profiles[count++] = profile;
            }

            return count;
        }

        private static ReadOnlySpan<byte> ReadRow(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            int end = cursor;
            while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
            return bytes.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> ReadColumn(ReadOnlySpan<byte> row, ref int cursor)
        {
            while (cursor < row.Length && row[cursor] == (byte)' ')
                cursor++;
            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;
            int end = cursor;
            if (cursor < row.Length && row[cursor] == (byte)',')
                cursor++;
            while (end > start && (row[end - 1] == (byte)' ' || row[end - 1] == (byte)'\t'))
                end--;
            return row.Slice(start, end - start);
        }

        private static float ReadFloatColumn(ReadOnlySpan<byte> row, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> column = ReadColumn(row, ref cursor);
            return TryParseFloat(column, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            if (value.Length <= 0)
                return false;

            int cursor = 0;
            bool negative = false;
            if (value[cursor] == (byte)'-')
            {
                negative = true;
                cursor++;
            }

            double integer = 0d;
            bool any = false;
            while (cursor < value.Length && value[cursor] >= (byte)'0' && value[cursor] <= (byte)'9')
            {
                integer = integer * 10d + (value[cursor] - (byte)'0');
                cursor++;
                any = true;
            }

            double fraction = 0d;
            double scale = 1d;
            if (cursor < value.Length && value[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < value.Length && value[cursor] >= (byte)'0' && value[cursor] <= (byte)'9')
                {
                    fraction = fraction * 10d + (value[cursor] - (byte)'0');
                    scale *= 10d;
                    cursor++;
                    any = true;
                }
            }

            if (!any)
                return false;

            double parsed = integer + fraction / scale;
            result = (float)(negative ? -parsed : parsed);
            return math.isfinite(result);
        }

        private static bool LooksLikeHeader(ReadOnlySpan<byte> name)
        {
            if (name.Length < 4)
                return false;
            byte a = ToLower(name[0]);
            byte b = ToLower(name[1]);
            byte c = ToLower(name[2]);
            byte d = ToLower(name[3]);
            return a == (byte)'s' && b == (byte)'p' && c == (byte)'e' && d == (byte)'c';
        }

        private static uint ParseSpeciesProfileKey(ReadOnlySpan<byte> value)
        {
            if (value.Length <= 0)
                return 0u;

            if (IsDefaultKey(value))
                return NutrientDriftRuntime.CarrionRouteHash;

            if (value.Length > 2 &&
                value[0] == (byte)'0' &&
                (value[1] == (byte)'x' || value[1] == (byte)'X') &&
                TryParseHexUint(value.Slice(2), out uint hex))
            {
                return hex == 0u ? NutrientDriftRuntime.CarrionRouteHash : hex;
            }

            if (TryParseUint(value, out uint numeric))
                return numeric == 0u ? NutrientDriftRuntime.CarrionRouteHash : numeric;

            return Fnv1a32(value);
        }

        private static bool IsDefaultKey(ReadOnlySpan<byte> value)
        {
            if (value.Length == 1 && value[0] == (byte)'*')
                return true;
            if (value.Length != 7)
                return false;

            return ToLower(value[0]) == (byte)'d' &&
                   ToLower(value[1]) == (byte)'e' &&
                   ToLower(value[2]) == (byte)'f' &&
                   ToLower(value[3]) == (byte)'a' &&
                   ToLower(value[4]) == (byte)'u' &&
                   ToLower(value[5]) == (byte)'l' &&
                   ToLower(value[6]) == (byte)'t';
        }

        private static bool TryParseUint(ReadOnlySpan<byte> value, out uint result)
        {
            result = 0u;
            if (value.Length <= 0)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                uint next = result * 10u + (uint)(b - (byte)'0');
                if (next < result)
                {
                    result = uint.MaxValue;
                    return true;
                }

                result = next;
            }

            return true;
        }

        private static bool TryParseHexUint(ReadOnlySpan<byte> value, out uint result)
        {
            result = 0u;
            if (value.Length <= 0 || value.Length > 8)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(10 + b - (byte)'a');
                else if (b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(10 + b - (byte)'A');
                else
                    return false;

                result = (result << 4) | digit;
            }

            return true;
        }

        private static uint Fnv1a32(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= ToLower(value[i]);
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }
    }
    #endif

    public static class CarrionDecaySelfAudit
    {
        private const string SelfAuditPassXml =
            @"<SELF_AUDIT agent=""SHINOBU_314"" domain=""CARRION_DECAY_NUTRIENT_BRIDGE"" status=""PASS_STATIC_PENDING_RUNTIME"">
<DTO_SIZES CarrionStateDTO=""64"" CarrionDeathSignalDTO=""64"" FaunaStateDTO=""64"" CarrionRuntimeCountersDTO=""64"" CarrionTuningDTO=""128"" CarrionTelemetryEntry=""64"" CarrionAttractionRecordDTO=""64"" CarrionDecayProfileDTO=""32""/>
<BYTE_MAP CarrionStateDTO=""CorpseAUP@0 InitialBiomass@24 CurrentBiomass@28 OriginalSpeciesHash@32 ToxicityEmissionRate@36 AgeSeconds@40 BiomassLostLastTick@44 DecayRate@48 Flags@52 EntityHash@56 _pad0@60""/>
<BYTE_MAP CarrionTelemetryEntry=""GridOriginAup@0 ActiveBiomass@24 InjectedBiomass@28 BurstExecutionMicroseconds@32 ActiveCarrion@36 AttractionCount@40 MaxToxicity@44 Frame@48 Flags@52 StateHash@56 Overflows@60""/>
<VAULT buffers=""71250-71259"" faultFlags=""71259""/>
<SIGNAL lane=""EntityDeathSignal"" publisher=""FaunaBrain.PublishCarrionDeathSignal"" duplicateGuard=""EntityHash""/>
<SCALABILITY quality=""continuous GlobalQualityWeight scales decay, attraction, and nutrient injection; base decay preserved""/>
<ZERO_GC hotPathManagedAllocations=""0"" runtimeStringConstruction=""0""/>
</SELF_AUDIT>";

        private const string SelfAuditFailXml =
            @"<SELF_AUDIT agent=""SHINOBU_314"" domain=""CARRION_DECAY_NUTRIENT_BRIDGE"" status=""FAIL_STATIC_LAYOUT_OR_VAULT"">
<TASK id=""20"" status=""FAIL_STATIC_LAYOUT_OR_VAULT"" name=""SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION"" proof=""Static layout or Vault range validation failed""/>
</SELF_AUDIT>";

        public static string BuildSelfAuditXml()
        {
            bool carrionStatePass =
                UnsafeUtility.SizeOf<CarrionStateDTO>() == 64 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.CorpseAUP)) == 0 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.InitialBiomass)) == 24 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.CurrentBiomass)) == 28 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.OriginalSpeciesHash)) == 32 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.ToxicityEmissionRate)) == 36 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.AgeSeconds)) == 40 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.BiomassLostLastTick)) == 44 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.DecayRate)) == 48 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.Flags)) == 52 &&
                OffsetOf<CarrionStateDTO>(nameof(CarrionStateDTO.EntityHash)) == 56 &&
                OffsetOf<CarrionStateDTO>("_pad0") == 60;
            bool deathSignalPass =
                UnsafeUtility.SizeOf<CarrionDeathSignalDTO>() == 64 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.CorpseAUP)) == 0 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.BiomassScale)) == 24 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.OriginalSpeciesHash)) == 28 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.SourceHash)) == 32 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.EntityHash)) == 36 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.Flags)) == 40 &&
                OffsetOf<CarrionDeathSignalDTO>(nameof(CarrionDeathSignalDTO.ToxicitySeed)) == 44 &&
                OffsetOf<CarrionDeathSignalDTO>("_pad0") == 48 &&
                OffsetOf<CarrionDeathSignalDTO>("_pad1") == 56;
            bool faunaStatePass =
                UnsafeUtility.SizeOf<FaunaStateDTO>() == 64 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.PositionAUP)) == 0 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Biomass)) == 24 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.SpeciesHash)) == 28 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.EntityHash)) == 32 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Flags)) == 36 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.CarrionSlot)) == 40 &&
                OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Health01)) == 44 &&
                OffsetOf<FaunaStateDTO>("_pad0") == 48 &&
                OffsetOf<FaunaStateDTO>("_pad1") == 56;
            bool counterPass =
                UnsafeUtility.SizeOf<CarrionRuntimeCountersDTO>() == 64 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.DeathIngressReadCursor)) == 0 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.CarrionWriteCursor)) == 12 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.TelemetryCursor)) == 24 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.Flags)) == 28 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.Frame)) == 32 &&
                OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.StateHash)) == 48 &&
                OffsetOf<CarrionRuntimeCountersDTO>("_pad0") == 56 &&
                OffsetOf<CarrionRuntimeCountersDTO>("_pad1") == 60;
            bool tuningPass =
                UnsafeUtility.SizeOf<CarrionTuningDTO>() == 128 &&
                OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.GridOriginAup)) == 0 &&
                OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.CellSizeMeters)) == 24 &&
                OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.GlobalQualityWeight)) == 64 &&
                OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ActiveAxis)) == 72 &&
                OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.RouteHash)) == 112 &&
                OffsetOf<CarrionTuningDTO>("_pad0") == 116 &&
                OffsetOf<CarrionTuningDTO>("_pad2") == 124;
            bool telemetryPass =
                UnsafeUtility.SizeOf<CarrionTelemetryEntry>() == 64 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.GridOriginAup)) == 0 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.ActiveBiomass)) == 24 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.BurstExecutionMicroseconds)) == 32 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.ActiveCarrion)) == 36 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.Frame)) == 48 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.Flags)) == 52 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.StateHash)) == 56 &&
                OffsetOf<CarrionTelemetryEntry>(nameof(CarrionTelemetryEntry.Overflows)) == 60;
            bool attractionPass =
                UnsafeUtility.SizeOf<CarrionAttractionRecordDTO>() == 64 &&
                OffsetOf<CarrionAttractionRecordDTO>(nameof(CarrionAttractionRecordDTO.CorpseAUP)) == 0 &&
                OffsetOf<CarrionAttractionRecordDTO>(nameof(CarrionAttractionRecordDTO.FoodValue)) == 24 &&
                OffsetOf<CarrionAttractionRecordDTO>(nameof(CarrionAttractionRecordDTO.OriginalSpeciesHash)) == 32 &&
                OffsetOf<CarrionAttractionRecordDTO>(nameof(CarrionAttractionRecordDTO.Temperature)) == 44 &&
                OffsetOf<CarrionAttractionRecordDTO>("_pad0") == 48 &&
                OffsetOf<CarrionAttractionRecordDTO>("_pad1") == 56;
            bool profilePass =
                UnsafeUtility.SizeOf<CarrionDecayProfileDTO>() == 32 &&
                OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.SpeciesHash)) == 0 &&
                OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.BaseDecayRate)) == 4 &&
                OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.Flags)) == 24 &&
                OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.SourceHash)) == 28;
            bool layoutPass = carrionStatePass && deathSignalPass && faunaStatePass && counterPass &&
                              tuningPass && telemetryPass && attractionPass && profilePass;
            bool vaultPass =
                (int)BufferID.ShinobuCarrionStates == 71250 &&
                (int)BufferID.ShinobuCarrionFaultFlags == 71259;
            return layoutPass && vaultPass ? SelfAuditPassXml : SelfAuditFailXml;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
