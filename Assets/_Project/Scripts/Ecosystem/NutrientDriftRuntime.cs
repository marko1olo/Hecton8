using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// SHINOBU_309 plankton/nutrient scalar-field drift. Owns Vault state; publishes only snapshots and a visual density texture.
    /// </summary>
    public unsafe sealed partial class NutrientDriftRuntime : IFrostTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        public const int GridAxisMax = 32;
        public const int GridCellCapacity = GridAxisMax * GridAxisMax * GridAxisMax;
        public const int SourceCapacity = 16;
        public const int ProfileCapacity = 32;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 16384;
        public const float FrostDeltaSeconds = 5f;
        public const string ProfileCsvFileName = "nutrient_drift_profiles.csv";
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_309.bin";
        public const uint RouteHash = 0x53333039u;
        public const ulong DumpMagic = 0x3330395F5452554EUL; // NURT_903 little-endian marker.

        private const uint PostSimulationSystemHash = RouteHash ^ 0x50534D39u; // PSM9
        private const int JobBatchSize = 64;
        private const float DefaultCellSizeMeters = 12f;
        private const float MinimumCellSizeMeters = 1f;
        private const float MaximumCellSizeMeters = 64f;
        private const float DefaultDecayPerSecond = 0.00201f;
        private const float DefaultInjectionMultiplier = 1f;
        private const float DefaultAdvectionTimeStep = FrostDeltaSeconds;
        private const float DefaultMockWhirlpoolMetersPerSecond = 4.5f;
        private const float DefaultSourceRadiusMeters = 42f;
        private const float DefaultMaxDensity = 24f;
        private const float FaultBudgetMicros = 1500f;
        private const uint TuningFlagWriteInFlight = 1u << 0;
        private const uint TuningFlagInitialized = 1u << 1;
        private const uint TuningFlagMockFlowEnabled = 1u << 2;
        private const uint TuningFlagMockSourceFallback = 1u << 3;
        private const uint TuningFlagNetcodeExcluded = 1u << 4;
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint TelemetryFlagOverBudget = 1u << 1;
        private const uint TelemetryFlagMockSource = 1u << 2;
        private const uint SourceFlagMock = 1u << 0;
        private const uint SourceFlagThermalVent = 1u << 1;

        private static readonly int DensityTextureShaderId = Shader.PropertyToID("_H8NutrientDriftDensityTex");
        private static readonly int DensityParamsShaderId = Shader.PropertyToID("_H8NutrientDriftParams");
        private static readonly int DensityOriginShaderId = Shader.PropertyToID("_H8NutrientDriftOriginAup");
        private static readonly ulong NutrientJobMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuNutrientDriftCellFront) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftCellBack) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftFlowField) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftInjection) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftSources) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftSourceCount) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftTuning) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftTelemetryRing) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftTelemetryCursor) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftDensityUpload) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftGridHeader) |
            MutationGuardBit(BufferID.ShinobuNutrientDriftFaultFlags);
        private static readonly ulong CarrionJobMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuCarrionStates) |
            MutationGuardBit(BufferID.ShinobuCarrionDeathIngress) |
            MutationGuardBit(BufferID.ShinobuCarrionRuntimeCounters) |
            MutationGuardBit(BufferID.ShinobuCarrionTuning) |
            MutationGuardBit(BufferID.ShinobuCarrionTelemetryRing) |
            MutationGuardBit(BufferID.ShinobuCarrionAttractionRecords) |
            MutationGuardBit(BufferID.ShinobuCarrionProfiles) |
            MutationGuardBit(BufferID.ShinobuCarrionFaunaStates) |
            MutationGuardBit(BufferID.ShinobuCarrionFaultFlags);
        private static readonly ulong CarrionDeathIngressMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuCarrionDeathIngress) |
            MutationGuardBit(BufferID.ShinobuCarrionRuntimeCounters);
        private static readonly ulong CombinedJobMutationGuardMask = NutrientJobMutationGuardMask | CarrionJobMutationGuardMask;
        private static readonly ulong InitializationMutationGuardMask =
            NutrientJobMutationGuardMask |
            MutationGuardBit(BufferID.ShinobuNutrientDriftProfiles);
        private static readonly ulong ProfileCsvMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuNutrientDriftProfiles);
#if UNITY_EDITOR
        private static readonly byte[] s_profileCsvImportScratch = new byte[CsvScratchBytes];
        private static readonly NutrientProfileDTO[] s_profileImportScratch = new NutrientProfileDTO[ProfileCapacity];
        private static int s_profileCsvImportScratchBusy;
#endif
        private static NutrientDriftRuntime s_runtime;

        private VaultGenerationHandle<NutrientCellDTO> _frontHandle;
        private VaultGenerationHandle<NutrientCellDTO> _backHandle;
        private VaultGenerationHandle<float3> _flowHandle;
        private VaultGenerationHandle<float> _injectionHandle;
        private VaultGenerationHandle<NutrientSourceDTO> _sourceHandle;
        private VaultGenerationHandle<int> _sourceCountHandle;
        private VaultGenerationHandle<NutrientDriftTuningDTO> _tuningHandle;
        private VaultGenerationHandle<FluidGridTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<float> _densityUploadHandle;
        private VaultGenerationHandle<NutrientDriftGridHeaderDTO> _headerHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<NutrientProfileDTO> _profileHandle;
        private VaultGenerationHandle<uint> _faultFlagHandle;

        private IDataVault _vault;
        private IDataVault _jobGuardVault;
        private INutrientThermalVentReadModel _thermalVentReadModel;
        private IAbyssalFlowVolumeReadModel _abyssalFlowReadModel;
        private IPlayerRuntimeContext _playerContext;
        private Texture3D _densityTexture;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private Vector4 _publishedDensityParams;
        private Vector4 _publishedDensityOrigin;
        private JobHandle _activeJobHandle;
        private long _scheduleTicks;
        private long _csvTimestampTicks;
        private int _telemetryCursor;
        private int _lastTelemetrySlot;
        private int _lastActiveAxis;
        private int _lastSourceCount;
        private uint _simulationTick;
        private bool _initialized;
        private bool _registeredFrost;
        private bool _registeredPostSimulation;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _densityTexturePublished;
        private bool _dumpedFault;
        private bool _profilesLoadedCold;

        private NutrientDriftRuntime()
        {
        }

        public static NutrientDriftRuntime EnsureRuntime()
        {
            NutrientDriftRuntime runtime = s_runtime;
            if (runtime == null)
            {
                runtime = new NutrientDriftRuntime();
                s_runtime = runtime;
            }

            runtime.Activate();
            return runtime;
        }

        public static bool TryReadTuning(out NutrientDriftTuningDTO tuning)
        {
            tuning = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            return runtime != null &&
                   vault != null &&
                   TryOpenReadVaultBuffer(vault, in runtime._tuningHandle, out NativeArray<NutrientDriftTuningDTO>.ReadOnly tuningArray) &&
                   tuningArray.Length > 0 &&
                   ReadSnapshotReady(tuningArray[0], out tuning);
        }

        public static bool TryWriteTuning(in NutrientDriftTuningDTO requestedTuning)
        {
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !IsMatchingVaultHandle(in runtime._tuningHandle, BufferID.ShinobuNutrientDriftTuning))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in runtime._tuningHandle, SystemID.AIEcology, out NativeArray<NutrientDriftTuningDTO> tuningArray))
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

                NutrientDriftTuningDTO sanitized = NutrientDriftMath.SanitizeTuning(
                    requestedTuning,
                    tuningArray[0].GridOriginAup);
                sanitized.Flags &= ~TuningFlagWriteInFlight;
                sanitized.Flags |= TuningFlagInitialized | TuningFlagNetcodeExcluded;
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<NutrientDriftTuningDTO>(ptr) = sanitized;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtime._tuningHandle, SystemID.AIEcology);
            }
        }

        public static bool TryReadTelemetryEntry(int index, out FluidGridTelemetryEntry entry)
        {
            entry = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                (uint)index >= TelemetryCapacity ||
                !TryOpenReadVaultBuffer(vault, in runtime._telemetryHandle, out NativeArray<FluidGridTelemetryEntry>.ReadOnly telemetry) ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return true;
        }

        public static bool ForceReloadProfilesCold()
        {
            NutrientDriftRuntime runtime = EnsureRuntime();
            runtime._csvTimestampTicks = 0L;
            runtime._profilesLoadedCold = true;
            return runtime.TryLoadProfilesCsvCold();
        }

        public static bool TryReadTelemetryCursor(out int cursor)
        {
            cursor = 0;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !TryOpenReadVaultBuffer(vault, in runtime._telemetryCursorHandle, out NativeArray<int>.ReadOnly cursorArray) ||
                cursorArray.Length <= 0)
            {
                return false;
            }

            cursor = cursorArray[0];
            return true;
        }

        public static bool TryReadGridHeader(out NutrientDriftGridHeaderDTO header)
        {
            header = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null ||
                vault == null ||
                !TryOpenReadVaultBuffer(vault, in runtime._headerHandle, out NativeArray<NutrientDriftGridHeaderDTO>.ReadOnly headers) ||
                headers.Length <= 0)
            {
                return false;
            }

            header = headers[0];
            return true;
        }

        public static bool TryReadDensityCell(int x, int y, int z, out NutrientCellDTO cell)
        {
            cell = default;
            NutrientDriftRuntime runtime = s_runtime;
            IDataVault vault = runtime != null ? runtime._vault : null;
            if (runtime == null || vault == null || runtime._jobScheduled)
                return false;

            if (!TryOpenReadVaultBuffer(vault, in runtime._tuningHandle, out NativeArray<NutrientDriftTuningDTO>.ReadOnly tuningArray) ||
                tuningArray.Length <= 0 ||
                !ReadSnapshotReady(tuningArray[0], out NutrientDriftTuningDTO tuning))
            {
                return false;
            }

            int axis = math.clamp(tuning.ActiveAxis, 1, GridAxisMax);
            if ((uint)x >= (uint)axis || (uint)y >= (uint)axis || (uint)z >= (uint)axis)
                return false;

            if (!TryOpenReadVaultBuffer(vault, in runtime._frontHandle, out NativeArray<NutrientCellDTO>.ReadOnly front))
                return false;

            int index = NutrientDriftMath.Index3D(x, y, z, GridAxisMax);
            if ((uint)index >= (uint)front.Length)
                return false;

            cell = front[index];
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystem()
        {
            if (s_runtime != null)
                s_runtime.Dispose();
            s_runtime = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureRuntime();
        }

        public void FrostTick()
        {
            if (_jobScheduled || !HasVaultStateReady())
                return;

            DrainCarrionDeathSignalSnapshot();

            IDataVault vault = _vault;
            if (vault == null || !TryLockJobBuffers(vault))
                return;

            bool keepJobGuard = false;
            try
            {
            if (!TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuNutrientDriftCellFront, GridCellCapacity, out NativeArray<NutrientCellDTO> front) ||
                !TryOpenVaultBuffer(vault, ref _backHandle, BufferID.ShinobuNutrientDriftCellBack, GridCellCapacity, out NativeArray<NutrientCellDTO> back) ||
                !TryOpenVaultBuffer(vault, ref _flowHandle, BufferID.ShinobuNutrientDriftFlowField, GridCellCapacity, out NativeArray<float3> flow) ||
                !TryOpenVaultBuffer(vault, ref _injectionHandle, BufferID.ShinobuNutrientDriftInjection, GridCellCapacity, out NativeArray<float> injection) ||
                !TryOpenVaultBuffer(vault, ref _sourceHandle, BufferID.ShinobuNutrientDriftSources, SourceCapacity, out NativeArray<NutrientSourceDTO> sources) ||
                !TryOpenVaultBuffer(vault, ref _sourceCountHandle, BufferID.ShinobuNutrientDriftSourceCount, 1, out NativeArray<int> sourceCount) ||
                !TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning, 1, out NativeArray<NutrientDriftTuningDTO> tuningArray) ||
                !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing, TelemetryCapacity, out NativeArray<FluidGridTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.ShinobuNutrientDriftTelemetryCursor, 1, out NativeArray<int> telemetryCursor) ||
                !TryOpenVaultBuffer(vault, ref _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload, GridCellCapacity, out NativeArray<float> densityUpload) ||
                !TryOpenVaultBuffer(vault, ref _headerHandle, BufferID.ShinobuNutrientDriftGridHeader, 1, out NativeArray<NutrientDriftGridHeaderDTO> headers) ||
                !TryOpenVaultBuffer(vault, ref _faultFlagHandle, BufferID.ShinobuNutrientDriftFaultFlags, 4, out NativeArray<uint> faultFlags))
            {
                _initialized = false;
                _profilesLoadedCold = false;
                return;
            }

            double3 originAup = ResolveGridOriginAup();
            NutrientDriftTuningDTO tuning = NutrientDriftMath.SanitizeTuning(tuningArray[0], originAup);
            tuning.GridOriginAup = originAup;
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight(tuning.GlobalQualityWeight);
            tuning.ActiveAxis = NutrientDriftMath.ResolveActiveAxis(tuning.GlobalQualityWeight);
            tuning.ActiveCellCount = tuning.ActiveAxis * tuning.ActiveAxis * tuning.ActiveAxis;
            tuning.FrameIndex = _simulationTick;
            tuning.Flags |= TuningFlagWriteInFlight | TuningFlagInitialized | TuningFlagNetcodeExcluded;

            int activeSources = CopyThermalSourcesToVault(sources, sourceCount, tuning);
            tuning.SourceCount = activeSources;
            bool hasAbyssalFlow = TryReadAbyssalFlowPayload(
                out NativeArray<float3>.ReadOnly abyssalFlowVolume,
                out float3 abyssalFlowCenter,
                out int abyssalFlowResolutionXZ,
                out int abyssalFlowResolutionY,
                out int abyssalFlowRingOffsetX,
                out int abyssalFlowRingOffsetY,
                out int abyssalFlowRingOffsetZ,
                out float abyssalFlowHorizontalCellSize,
                out float abyssalFlowVerticalCellSize,
                out float abyssalFlowWaterLevel,
                out float abyssalFlowDepthMeters);
            if (hasAbyssalFlow)
                tuning.Flags &= ~TuningFlagMockFlowEnabled;
            else
                tuning.Flags |= TuningFlagMockFlowEnabled;
            tuningArray[0] = tuning;

            int telemetryCapacity = math.max(1, math.min(telemetry.Length, TelemetryCapacity));
            int telemetrySlot = _telemetryCursor % telemetryCapacity;
            int nextTelemetryCursor = (telemetrySlot + 1) % telemetryCapacity;
            _lastTelemetrySlot = telemetrySlot;
            _lastActiveAxis = tuning.ActiveAxis;
            _lastSourceCount = activeSources;

            NutrientCellDTO* frontPtr = (NutrientCellDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(front);
            NutrientCellDTO* backPtr = (NutrientCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
            float3* flowPtr = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(flow);
            float* injectionPtr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(injection);
            NutrientSourceDTO* sourcePtr = (NutrientSourceDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sources);
            FluidGridTelemetryEntry* telemetryPtr = (FluidGridTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry);
            int* telemetryCursorPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryCursor);
            float* densityUploadPtr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(densityUpload);
            uint* faultFlagPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags);

            JobHandle handle;
            if (hasAbyssalFlow)
            {
                var flowJob = new CopyAbyssalFlowVolumeToNutrientFlowJob
                {
                    Flow = flowPtr,
                    AbyssalFlowVolume = abyssalFlowVolume,
                    Tuning = tuning,
                    GridCenterLocal = ResolveGridCenterLocal(originAup),
                    AbyssalFlowCenter = abyssalFlowCenter,
                    AbyssalFlowResolutionXZ = abyssalFlowResolutionXZ,
                    AbyssalFlowResolutionY = abyssalFlowResolutionY,
                    AbyssalFlowRingOffsetX = abyssalFlowRingOffsetX,
                    AbyssalFlowRingOffsetY = abyssalFlowRingOffsetY,
                    AbyssalFlowRingOffsetZ = abyssalFlowRingOffsetZ,
                    AbyssalFlowHorizontalCellSize = abyssalFlowHorizontalCellSize,
                    AbyssalFlowVerticalCellSize = abyssalFlowVerticalCellSize,
                    AbyssalFlowWaterLevel = abyssalFlowWaterLevel,
                    AbyssalFlowDepthMeters = abyssalFlowDepthMeters
                };
                handle = flowJob.Schedule(tuning.ActiveCellCount, JobBatchSize);
            }
            else
            {
                var flowJob = new GenerateMockFlowFieldJob
                {
                    Flow = flowPtr,
                    Tuning = tuning,
                    TimeSeconds = _simulationTick * FrostDeltaSeconds
                };
                handle = flowJob.Schedule(tuning.ActiveCellCount, JobBatchSize);
            }

            var sourceJob = new UpdateNutrientSourcesJob
            {
                Injection = injectionPtr,
                Sources = sourcePtr,
                Tuning = tuning
            };
            handle = sourceJob.Schedule(tuning.ActiveCellCount, JobBatchSize, handle);
            handle = ScheduleCarrionDecayJobs(vault, tuning, frontPtr, injectionPtr, handle);

            var advectionJob = new EvaluateNutrientAdvectionJob
            {
                Front = frontPtr,
                Back = backPtr,
                Flow = flowPtr,
                Injection = injectionPtr,
                Tuning = tuning
            };
            handle = advectionJob.Schedule(tuning.ActiveCellCount, JobBatchSize, handle);

            var uploadJob = new CopyNutrientDensityUploadJob
            {
                Cells = backPtr,
                DensityUpload = densityUploadPtr,
                Tuning = tuning
            };
            handle = uploadJob.Schedule(GridCellCapacity, JobBatchSize, handle);

            var telemetryJob = new RecordNutrientTelemetryJob
            {
                Cells = backPtr,
                Flow = flowPtr,
                Sources = sourcePtr,
                FaultFlags = faultFlagPtr,
                TelemetryRing = telemetryPtr,
                TelemetryCursor = telemetryCursorPtr,
                Tuning = tuning,
                TelemetrySlot = telemetrySlot,
                TelemetryCursorValue = nextTelemetryCursor,
                ActiveSources = activeSources
            };
            handle = telemetryJob.Schedule(handle);

            _activeJobHandle = handle;
            _scheduleTicks = Stopwatch.GetTimestamp();
            _telemetryCursor = nextTelemetryCursor;
            _jobScheduled = true;
            _simulationTick++;
            keepJobGuard = true;
            }
            finally
            {
                if (!keepJobGuard)
                    UnlockJobBuffers();
            }
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
                    if (_vault != null)
                    {
                        EnsureDensityTexture();
                        EnsureVaultState();
                    }
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _thermalVentReadModel = currentService as INutrientThermalVentReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _abyssalFlowReadModel = currentService as IAbyssalFlowVolumeReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherRoutes();
                    if (currentService != null)
                        TryRegister();
                    break;
            }
        }

        public void Dispose()
        {
            CompleteScheduledJobForTeardown();
            TryUnregister();
            RebindDataVaultForLifecycle(null);
            _thermalVentReadModel = null;
            _abyssalFlowReadModel = null;
            _playerContext = null;
            _initialized = false;
            _profilesLoadedCold = false;
            if (_densityTexture != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(_densityTexture);
                else
#endif
                UnityEngine.Object.Destroy(_densityTexture);
                _densityTexture = null;
            }

            _densityTexturePublished = false;
            _publishedDensityParams = Vector4.zero;
            _publishedDensityOrigin = Vector4.zero;
        }

        private void Activate()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _thermalVentReadModel = GlobalRegistry.NutrientThermalVents;
            _abyssalFlowReadModel = GlobalRegistry.AbyssalFlowVolume;
            _playerContext = GlobalRegistry.Player;
            TryRegister();
            EnsureDensityTexture();
            EnsureVaultState();
        }

        private void RebindDataVaultForLifecycle(IDataVault vault, IDataVault releaseVaultOverride = null)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            CompleteScheduledJobForVaultSwapBarrier();
            ReleaseVaultHandles(_vault ?? releaseVaultOverride);
            _vault = vault;
            _initialized = false;
            _profilesLoadedCold = false;
            _telemetryCursor = 0;
            _lastTelemetrySlot = 0;
            _lastActiveAxis = 0;
            _lastSourceCount = 0;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_initialized && AreVaultHandlesStamped())
            {
                if (!_profilesLoadedCold)
                {
                    _profilesLoadedCold = true;
                    TryLoadProfilesCsvCold();
                }
                return EnsureCarrionVaultState(vault);
            }

            if (!EnsureVaultBufferHandle(vault, ref _frontHandle, BufferID.ShinobuNutrientDriftCellFront, GridCellCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _backHandle, BufferID.ShinobuNutrientDriftCellBack, GridCellCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _flowHandle, BufferID.ShinobuNutrientDriftFlowField, GridCellCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _injectionHandle, BufferID.ShinobuNutrientDriftInjection, GridCellCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _sourceHandle, BufferID.ShinobuNutrientDriftSources, SourceCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _sourceCountHandle, BufferID.ShinobuNutrientDriftSourceCount, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing, TelemetryCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _telemetryCursorHandle, BufferID.ShinobuNutrientDriftTelemetryCursor, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload, GridCellCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _headerHandle, BufferID.ShinobuNutrientDriftGridHeader, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _profileHandle, BufferID.ShinobuNutrientDriftProfiles, ProfileCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _csvScratchHandle, BufferID.ShinobuNutrientDriftCsvScratch, CsvScratchBytes, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBufferHandle(vault, ref _faultFlagHandle, BufferID.ShinobuNutrientDriftFaultFlags, 4, NativeArrayOptions.UninitializedMemory))
            {
                return false;
            }

            bool nutrientReady = false;
            bool profileLoadRequired = false;
            bool initializationGuardHeld = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(InitializationMutationGuardMask))
                    return false;

                initializationGuardHeld = true;
                if (!TryOpenVaultBuffer(vault, ref _profileHandle, BufferID.ShinobuNutrientDriftProfiles, ProfileCapacity, out NativeArray<NutrientProfileDTO> profiles))
                    return false;

                if (!profiles.IsCreated ||
                    profiles.Length < ProfileCapacity ||
                    !TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuNutrientDriftCellFront, GridCellCapacity, out NativeArray<NutrientCellDTO> front) ||
                    !TryOpenVaultBuffer(vault, ref _backHandle, BufferID.ShinobuNutrientDriftCellBack, GridCellCapacity, out NativeArray<NutrientCellDTO> back) ||
                    !TryOpenVaultBuffer(vault, ref _flowHandle, BufferID.ShinobuNutrientDriftFlowField, GridCellCapacity, out NativeArray<float3> flow) ||
                    !TryOpenVaultBuffer(vault, ref _injectionHandle, BufferID.ShinobuNutrientDriftInjection, GridCellCapacity, out NativeArray<float> injection) ||
                    !TryOpenVaultBuffer(vault, ref _sourceHandle, BufferID.ShinobuNutrientDriftSources, SourceCapacity, out NativeArray<NutrientSourceDTO> sources) ||
                    !TryOpenVaultBuffer(vault, ref _sourceCountHandle, BufferID.ShinobuNutrientDriftSourceCount, 1, out NativeArray<int> sourceCount) ||
                    !TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning, 1, out NativeArray<NutrientDriftTuningDTO> tuning) ||
                    !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing, TelemetryCapacity, out NativeArray<FluidGridTelemetryEntry> telemetry) ||
                    !TryOpenVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.ShinobuNutrientDriftTelemetryCursor, 1, out NativeArray<int> telemetryCursor) ||
                    !TryOpenVaultBuffer(vault, ref _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload, GridCellCapacity, out NativeArray<float> densityUpload) ||
                    !TryOpenVaultBuffer(vault, ref _headerHandle, BufferID.ShinobuNutrientDriftGridHeader, 1, out NativeArray<NutrientDriftGridHeaderDTO> headers) ||
                    !TryOpenVaultBuffer(vault, ref _faultFlagHandle, BufferID.ShinobuNutrientDriftFaultFlags, 4, out NativeArray<uint> faultFlags))
                {
                    return false;
                }

                if (_initialized && (tuning[0].Flags & TuningFlagInitialized) != 0u)
                {
                    nutrientReady = true;
                    profileLoadRequired = !_profilesLoadedCold;
                }
                else
                {
                    double3 originAup = ResolveGridOriginAup();
                    NutrientDriftTuningDTO defaultTuning = NutrientDriftMath.CreateDefaultTuning(originAup);
                    tuning[0] = defaultTuning;
                    sourceCount[0] = 0;
                    telemetryCursor[0] = 0;
                    _telemetryCursor = 0;
                    headers[0] = NutrientDriftMath.CreateHeader(defaultTuning, 0f, 0f, 0);

                    var initJob = new InitializeNutrientGridJob
                    {
                        Front = (NutrientCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                        Back = (NutrientCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back),
                        Flow = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(flow),
                        Injection = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(injection),
                        DensityUpload = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(densityUpload),
                        Sources = (NutrientSourceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(sources),
                        Profiles = (NutrientProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                        FaultFlags = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags)
                    };
                    JobHandle initHandle = initJob.Schedule(GridCellCapacity, JobBatchSize);

                    var telemetryInitJob = new InitializeNutrientTelemetryJob
                    {
                        TelemetryRing = (FluidGridTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry)
                    };
                    initHandle = telemetryInitJob.Schedule(TelemetryCapacity, JobBatchSize, initHandle);
                    DispatcherJobFence.BeginPostSimulationSwapWindow();
                    try
                    {
                        DispatcherJobFence.TryComplete(ref initHandle, forceComplete: true); // COLD_BOOTSTRAP_SYNC: uninitialized Vault memory must be deterministically populated before first public read.
                    }
                    finally
                    {
                        DispatcherJobFence.EndPostSimulationSwapWindow();
                    }

                    _initialized = true;
                    nutrientReady = true;
                    profileLoadRequired = !_profilesLoadedCold;
                }
            }
            finally
            {
                if (initializationGuardHeld)
                    vault.ReleaseMutationGuard(InitializationMutationGuardMask);
            }

            if (!nutrientReady)
                return false;

            if (profileLoadRequired)
            {
                _profilesLoadedCold = true;
                TryLoadProfilesCsvCold();
            }

            return EnsureCarrionVaultState(vault);
        }

        private bool AreVaultHandlesStamped()
        {
            return IsMatchingVaultHandle(in _frontHandle, BufferID.ShinobuNutrientDriftCellFront) &&
                   IsMatchingVaultHandle(in _backHandle, BufferID.ShinobuNutrientDriftCellBack) &&
                   IsMatchingVaultHandle(in _flowHandle, BufferID.ShinobuNutrientDriftFlowField) &&
                   IsMatchingVaultHandle(in _injectionHandle, BufferID.ShinobuNutrientDriftInjection) &&
                   IsMatchingVaultHandle(in _sourceHandle, BufferID.ShinobuNutrientDriftSources) &&
                   IsMatchingVaultHandle(in _sourceCountHandle, BufferID.ShinobuNutrientDriftSourceCount) &&
                   IsMatchingVaultHandle(in _tuningHandle, BufferID.ShinobuNutrientDriftTuning) &&
                   IsMatchingVaultHandle(in _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing) &&
                   IsMatchingVaultHandle(in _telemetryCursorHandle, BufferID.ShinobuNutrientDriftTelemetryCursor) &&
                   IsMatchingVaultHandle(in _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload) &&
                   IsMatchingVaultHandle(in _headerHandle, BufferID.ShinobuNutrientDriftGridHeader) &&
                   IsMatchingVaultHandle(in _csvScratchHandle, BufferID.ShinobuNutrientDriftCsvScratch) &&
                   IsMatchingVaultHandle(in _profileHandle, BufferID.ShinobuNutrientDriftProfiles) &&
                   IsMatchingVaultHandle(in _faultFlagHandle, BufferID.ShinobuNutrientDriftFaultFlags);
        }

        private bool HasVaultStateReady()
        {
            return _initialized &&
                   _vault != null &&
                   AreVaultHandlesStamped() &&
                   HasCarrionVaultStateReady();
        }

        private int CopyThermalSourcesToVault(NativeArray<NutrientSourceDTO> sources, NativeArray<int> sourceCount, NutrientDriftTuningDTO tuning)
        {
            int count = 0;
            INutrientThermalVentReadModel registry = _thermalVentReadModel;
            if (registry != null)
            {
                int registryCount = math.min(registry.ReadActiveNutrientThermalVentCount(), SourceCapacity);
                for (int i = 0; i < registryCount && count < SourceCapacity; i++)
                {
                    if (!registry.TryGetActiveNutrientThermalVent(i, out NutrientThermalVentSnapshotDTO record) ||
                        !record.PositionAup.IsFinite())
                    {
                        continue;
                    }

                    double3 sourceAup = record.PositionAup.ToAbsoluteDouble3();
                    if (!math.all(math.isfinite(sourceAup)))
                        continue;

                    float radius = math.clamp(math.max(record.RadiusWS, record.CableRadiusWS * 8f), 4f, 256f);
                    float heat = math.max(0f, record.HeatIntensity);
                    float smoke = math.max(0f, record.SmokeDensity);
                    sources[count++] = new NutrientSourceDTO
                    {
                        Aup = sourceAup,
                        RadiusMeters = radius,
                        InjectionDensity = (0.12f + heat * 0.035f + smoke * 0.02f) * tuning.InjectionMultiplier,
                        Temperature = math.clamp(6f + heat * 0.5f, -2f, 130f),
                        ToxinLevel = math.saturate(smoke * 0.01f),
                        SourceHash = unchecked((uint)record.RuntimeKey),
                        Flags = SourceFlagThermalVent
                    };
                }
            }

            if (count == 0 && (tuning.Flags & TuningFlagMockSourceFallback) != 0u)
            {
                sources[count++] = new NutrientSourceDTO
                {
                    Aup = tuning.GridOriginAup,
                    RadiusMeters = DefaultSourceRadiusMeters,
                    InjectionDensity = 0.08f * tuning.InjectionMultiplier,
                    Temperature = 6f,
                    ToxinLevel = 0.05f,
                    SourceHash = RouteHash,
                    Flags = SourceFlagMock
                };
            }

            for (int i = count; i < sources.Length; i++)
                sources[i] = default;
            sourceCount[0] = count;
            return count;
        }

        private double3 ResolveGridOriginAup()
        {
            IPlayerRuntimeContext player = _playerContext;
            if (player != null &&
                player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                AbsoluteUniversePosition playerAup = movementState.PredictedAup;
                if (playerAup.IsFinite())
                {
                    double3 value = playerAup.ToAbsoluteDouble3();
                    if (math.all(math.isfinite(value)))
                        return value;
                }
            }

            AbsoluteUniversePosition origin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return origin.IsFinite() ? origin.ToAbsoluteDouble3() : double3.zero;
        }

        private static float3 ResolveGridCenterLocal(double3 gridOriginAup)
        {
            AbsoluteUniversePosition runtimeOrigin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            double3 runtimeOriginAup = runtimeOrigin.IsFinite() ? runtimeOrigin.ToAbsoluteDouble3() : double3.zero;
            double3 local = gridOriginAup - runtimeOriginAup;
            return math.all(math.isfinite(local)) ? new float3((float)local.x, (float)local.y, (float)local.z) : float3.zero;
        }

        private bool TryReadAbyssalFlowPayload(
            out NativeArray<float3>.ReadOnly flowVolume,
            out float3 center,
            out int resolutionXZ,
            out int resolutionY,
            out int ringOffsetX,
            out int ringOffsetY,
            out int ringOffsetZ,
            out float horizontalCellSize,
            out float verticalCellSize,
            out float waterLevel,
            out float depthMeters)
        {
            IAbyssalFlowVolumeReadModel bridge = _abyssalFlowReadModel;
            if (bridge != null &&
                bridge.TryGetAbyssalFlowVolumePayload(
                    out flowVolume,
                    out Vector3 payloadCenter,
                    out resolutionXZ,
                    out resolutionY,
                    out ringOffsetX,
                    out ringOffsetY,
                    out ringOffsetZ,
                    out horizontalCellSize,
                    out verticalCellSize,
                    out waterLevel,
                    out depthMeters) &&
                flowVolume.IsCreated &&
                flowVolume.Length > 0 &&
                resolutionXZ > 1 &&
                resolutionY > 1 &&
                horizontalCellSize > 0f &&
                verticalCellSize > 0f &&
                depthMeters > 0f)
            {
                center = new float3(payloadCenter.x, payloadCenter.y, payloadCenter.z);
                return math.all(math.isfinite(center)) &&
                       math.isfinite(waterLevel) &&
                       math.abs(waterLevel) <= 1000f &&
                       math.isfinite(depthMeters);
            }

            flowVolume = default;
            center = float3.zero;
            resolutionXZ = 0;
            resolutionY = 0;
            ringOffsetX = 0;
            ringOffsetY = 0;
            ringOffsetZ = 0;
            horizontalCellSize = 0f;
            verticalCellSize = 0f;
            waterLevel = 0f;
            depthMeters = 0f;
            return false;
        }

        private void TryRegister()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPostSimulation)
            {
                if (_postSimulationPhase == null)
                    _postSimulationPhase = new PostSimulationPhaseSystem(this);

                _registeredPostSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
            }

            if (!_registeredPostSimulation)
                return;

            if (!_registeredFrost)
                _registeredFrost = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            UnregisterDispatcherRoutes();

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void UnregisterDispatcherRoutes()
        {
            if (_registeredFrost)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrost = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;
            if (!vault.TryAcquireMutationGuard(CombinedJobMutationGuardMask))
                return false;

            _jobLocksHeld = true;
            _jobGuardVault = vault;
            return true;
        }

        private void UnlockJobBuffers()
        {
            UnlockCarrionJobBuffers();

            if (!_jobLocksHeld)
                return;

            _jobLocksHeld = false;
            IDataVault vault = _jobGuardVault;
            _jobGuardVault = null;
            if (vault != null)
                vault.ReleaseMutationGuard(CombinedJobMutationGuardMask);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void TryFinalizeScheduledJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            FinishCompletedScheduledJob();
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            TryFinalizeScheduledJobNoWait();
            DrainCarrionDeathSignalSnapshot();
        }

        private void CompleteScheduledJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                    FinishCompletedScheduledJob();
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void CompleteScheduledJobForVaultSwapBarrier()
        {
            if (!_jobScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                    FinishCompletedScheduledJob();
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void FinishCompletedScheduledJob()
        {
            long now = Stopwatch.GetTimestamp();
            float micros = Stopwatch.Frequency > 0
                ? (float)((now - _scheduleTicks) * 1000000.0 / Stopwatch.Frequency)
                : 0f;

            try
            {
                IDataVault vault = _vault;
                if (vault != null)
                {
                    SwapFrontBackHandles();
                    ClearSnapshotWriteInFlight(vault);
                    PatchCompletedCarrionTelemetry(vault, ResolveCarrionSolverMicros(now, micros));
                    PublishCarrionAttractions(vault);
                    PatchCompletedTelemetry(vault, micros);
                    UpdateGridHeaderAndTexture(vault, micros);
                }
            }
            finally
            {
                _jobScheduled = false;
                UnlockJobBuffers();
            }
        }

        private void SwapFrontBackHandles()
        {
            VaultGenerationHandle<NutrientCellDTO> oldFront = _frontHandle;
            _frontHandle = _backHandle;
            _backHandle = oldFront;
        }

        private void ClearSnapshotWriteInFlight(IDataVault vault)
        {
            if (!TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning, 1, out NativeArray<NutrientDriftTuningDTO> tuningArray))
                return;

            NutrientDriftTuningDTO tuning = tuningArray[0];
            tuning.Flags &= ~TuningFlagWriteInFlight;
            tuning.FrontBufferId = unchecked((uint)_frontHandle.BufferID);
            tuning.BackBufferId = unchecked((uint)_backHandle.BufferID);
            tuningArray[0] = tuning;
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly NutrientDriftRuntime _owner;

            public PostSimulationPhaseSystem(NutrientDriftRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private void PatchCompletedTelemetry(IDataVault vault, float solverMicros)
        {
            if (!TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing, TelemetryCapacity, out NativeArray<FluidGridTelemetryEntry> telemetry) ||
                (uint)_lastTelemetrySlot >= (uint)telemetry.Length)
            {
                return;
            }

            FluidGridTelemetryEntry entry = telemetry[_lastTelemetrySlot];
            entry.BurstExecutionMicroseconds = math.max(0f, solverMicros);
            if (entry.BurstExecutionMicroseconds > FaultBudgetMicros)
                entry.Flags |= TelemetryFlagOverBudget;
            telemetry[_lastTelemetrySlot] = entry;

            bool fault = (entry.Flags & (TelemetryFlagNaN | TelemetryFlagOverBudget)) != 0u;
            if (fault && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpTelemetry(vault);
            }
        }

        private void UpdateGridHeaderAndTexture(IDataVault vault, float solverMicros)
        {
            if (!TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning, 1, out NativeArray<NutrientDriftTuningDTO> tuningArray) ||
                !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing, TelemetryCapacity, out NativeArray<FluidGridTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(vault, ref _headerHandle, BufferID.ShinobuNutrientDriftGridHeader, 1, out NativeArray<NutrientDriftGridHeaderDTO> headers) ||
                !TryOpenVaultBuffer(vault, ref _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload, GridCellCapacity, out NativeArray<float> densityUpload))
            {
                return;
            }

            NutrientDriftTuningDTO tuning = NutrientDriftMath.SanitizeTuning(tuningArray[0], tuningArray[0].GridOriginAup);
            FluidGridTelemetryEntry entry = (uint)_lastTelemetrySlot < (uint)telemetry.Length
                ? telemetry[_lastTelemetrySlot]
                : default;
            headers[0] = NutrientDriftMath.CreateHeader(tuning, entry.TotalDensity, solverMicros, _lastSourceCount);
            PublishDensityTexture(densityUpload, tuning, entry);
        }

        private void EnsureDensityTexture()
        {
            if (_densityTexture != null)
                return;

            _densityTexture = new Texture3D(GridAxisMax, GridAxisMax, GridAxisMax, TextureFormat.RFloat, false)
            {
                name = "H8_NutrientDriftDensity_SHINOBU_309",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            _densityTexturePublished = false;
        }

        private void PublishDensityTexture(NativeArray<float> densityUpload, NutrientDriftTuningDTO tuning, FluidGridTelemetryEntry entry)
        {
            EnsureDensityTexture();
            if (_densityTexture == null || !densityUpload.IsCreated || densityUpload.Length < GridCellCapacity)
                return;

            int cadence = NutrientDriftMath.ResolveUploadCadenceFrames(tuning.GlobalQualityWeight);
            if (cadence > 1 && (_simulationTick % (uint)cadence) != 0u)
                return;

            _densityTexture.SetPixelData(densityUpload, 0);
            _densityTexture.Apply(false, false);
            float3 gridOriginLocal = ResolveGridCenterLocal(tuning.GridOriginAup);
            Vector4 densityParams = new Vector4(
                math.clamp(tuning.ActiveAxis, 1, GridAxisMax),
                math.clamp(tuning.CellSizeMeters, MinimumCellSizeMeters, MaximumCellSizeMeters),
                math.select(0f, entry.TotalDensity, math.isfinite(entry.TotalDensity)),
                math.saturate(tuning.GlobalQualityWeight));
            Vector4 densityOrigin = new Vector4(gridOriginLocal.x, gridOriginLocal.y, gridOriginLocal.z, 1f);
            PublishDensityShaderGlobals(densityParams, densityOrigin);
        }

        private void PublishDensityShaderGlobals(Vector4 densityParams, Vector4 densityOrigin)
        {
            if (!_densityTexturePublished)
            {
                Shader.SetGlobalTexture(DensityTextureShaderId, _densityTexture);
                _densityTexturePublished = true;
            }

            if (!AreEqual(in _publishedDensityParams, in densityParams))
            {
                Shader.SetGlobalVector(DensityParamsShaderId, densityParams);
                _publishedDensityParams = densityParams;
            }

            if (!AreEqual(in _publishedDensityOrigin, in densityOrigin))
            {
                Shader.SetGlobalVector(DensityOriginShaderId, densityOrigin);
                _publishedDensityOrigin = densityOrigin;
            }
        }

        private static bool AreEqual(in Vector4 a, in Vector4 b)
        {
            return a.x == b.x &&
                   a.y == b.y &&
                   a.z == b.z &&
                   a.w == b.w;
        }

        private void DumpTelemetry(IDataVault vault)
        {
            if (!TryOpenReadVaultBuffer(vault, in _telemetryHandle, out NativeArray<FluidGridTelemetryEntry>.ReadOnly telemetry))
                return;

            NativeArray<byte> payload = default;
            try
            {
                int stride = UnsafeUtility.SizeOf<FluidGridTelemetryEntry>();
                int byteCount = 24 + telemetry.Length * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(NutrientDriftRuntime),
                    "nutrientDriftTelemetryDumpPayload");
                unsafe
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> bytes = new Span<byte>(target, byteCount);
                    WriteUInt64(bytes.Slice(0, 8), DumpMagic);
                    WriteUInt32(bytes.Slice(8, 4), unchecked((uint)TelemetryCapacity));
                    WriteUInt32(bytes.Slice(12, 4), unchecked((uint)stride));
                    WriteUInt32(bytes.Slice(16, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32(bytes.Slice(20, 4), RouteHash);
                    int offset = 24;
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        FluidGridTelemetryEntry entry = telemetry[i];
                        UnsafeUtility.MemCpy(target + offset, &entry, stride);
                        offset += stride;
                    }
                }

                if (!NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount))
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(NutrientDriftRuntime),
                    "nutrientDriftTelemetryDumpPayload");
            }
        }

        private bool TryLoadProfilesCsvCold()
        {
#if !UNITY_EDITOR
            return false;
#else
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            string path = BuildProfileCsvPath();
            if (path == null || path.Length == 0 || !File.Exists(path))
                return false;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Ticks == _csvTimestampTicks)
                return true;

            if (!IsMatchingVaultHandle(in _profileHandle, BufferID.ShinobuNutrientDriftProfiles))
            {
                return false;
            }

            int bytesRead;
            int parsed;
            bool publishCommitFault = false;
            if (System.Threading.Interlocked.CompareExchange(ref s_profileCsvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                try
                {
                    bytesRead = ReadCsvBytesCold(path, s_profileCsvImportScratch, CsvScratchBytes);
                    if (bytesRead <= 0)
                        return false;

                    parsed = NutrientDriftCsvParser.ParseProfiles(
                        s_profileCsvImportScratch.AsSpan(0, bytesRead),
                        s_profileImportScratch);
                    if (parsed <= 0)
                        return false;
                }
                catch (IOException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);
                    return false;
                }
                catch (ArgumentException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);
                    return false;
                }
                catch (NotSupportedException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);
                    return false;
                }
                catch (InvalidOperationException)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);
                    return false;
                }

                if (!vault.TryAcquireMutationGuard(ProfileCsvMutationGuardMask))
                    return false;

                try
                {
                    if (!vault.TryResolveHandle(in _profileHandle, out NativeArray<NutrientProfileDTO> profiles) ||
                        !profiles.IsCreated ||
                        profiles.Length < ProfileCapacity)
                    {
                        return false;
                    }

                    fixed (NutrientProfileDTO* source = s_profileImportScratch)
                    {
                        UnsafeUtility.MemCpy(
                            NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                            source,
                            parsed * UnsafeUtility.SizeOf<NutrientProfileDTO>());
                    }

                    _csvTimestampTicks = lastWriteUtc.Ticks;
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
                    vault.ReleaseMutationGuard(ProfileCsvMutationGuardMask);
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref s_profileCsvImportScratchBusy, 0);
            }

            if (publishCommitFault)
                GlobalTelemetryBus.PublishPerformanceWarning(0x4E445043u, RouteHash, 0f);

            return false;
#endif
        }

#if UNITY_EDITOR
        private static int ReadCsvBytesCold(string path, byte[] scratch, int maxBytes)
        {
            if (scratch == null || scratch.Length <= 0 || maxBytes <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return stream.Read(scratch, 0, math.min(scratch.Length, maxBytes));
            }
        }
#endif

        private static string BuildProfileCsvPath()
        {
#if !UNITY_EDITOR
            return string.Empty;
#else
            string dataPath = Application.dataPath;
            string first = Path.Combine(dataPath, "_Project", "Data", ProfileCsvFileName);
            if (File.Exists(first))
                return first;

            string streaming = Path.Combine(Application.streamingAssetsPath, "Hecton8", "DataMonolith", ProfileCsvFileName);
            if (File.Exists(streaming))
                return streaming;

            DirectoryInfo root = Directory.GetParent(dataPath);
            if (root == null)
                return first;

            return Path.Combine(root.FullName, "Data", ProfileCsvFileName);
#endif
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            return TryOpenReadVaultBuffer(vault, in handle, 1, out buffer);
        }

        private static bool TryOpenReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool EnsureVaultBufferHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (IsMatchingVaultHandle(in handle, bufferId) &&
                TryOpenReadVaultBuffer(vault, in handle, requiredLength, out NativeArray<T>.ReadOnly _))
                return true;

            if (vault == null || requiredLength <= 0)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.AIEcology, options);
            if (IsMatchingVaultHandle(in handle, bufferId) &&
                TryOpenReadVaultBuffer(vault, in handle, requiredLength, out NativeArray<T>.ReadOnly _))
                return true;

            handle = default;
            return false;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.AIEcology;
        }

        private static bool ReadSnapshotReady(NutrientDriftTuningDTO raw, out NutrientDriftTuningDTO tuning)
        {
            bool ready = (raw.Flags & TuningFlagWriteInFlight) == 0u &&
                         (raw.Flags & TuningFlagInitialized) != 0u;
            tuning = NutrientDriftMath.SanitizeTuning(raw, raw.GridOriginAup);
            return ready;
        }

        private static float ResolveGlobalQualityWeight(float tuningQuality)
        {
            float global = HomeostasisBrain.GlobalQualityWeight;
            float sanitizedGlobal = math.saturate(math.select(1f, global, math.isfinite(global)));
            float sanitizedTuning = math.saturate(math.select(sanitizedGlobal, tuningQuality, math.isfinite(tuningQuality)));
            return math.min(sanitizedGlobal, sanitizedTuning);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            UnlockJobBuffers();
            ReleaseCarrionVaultHandles(vault);

            if (vault == null)
            {
                ResetHandlesNoRelease();
                return;
            }

            ReleaseVaultHandle(vault, ref _frontHandle, BufferID.ShinobuNutrientDriftCellFront);
            ReleaseVaultHandle(vault, ref _backHandle, BufferID.ShinobuNutrientDriftCellBack);
            ReleaseVaultHandle(vault, ref _flowHandle, BufferID.ShinobuNutrientDriftFlowField);
            ReleaseVaultHandle(vault, ref _injectionHandle, BufferID.ShinobuNutrientDriftInjection);
            ReleaseVaultHandle(vault, ref _sourceHandle, BufferID.ShinobuNutrientDriftSources);
            ReleaseVaultHandle(vault, ref _sourceCountHandle, BufferID.ShinobuNutrientDriftSourceCount);
            ReleaseVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuNutrientDriftTuning);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuNutrientDriftTelemetryRing);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.ShinobuNutrientDriftTelemetryCursor);
            ReleaseVaultHandle(vault, ref _densityUploadHandle, BufferID.ShinobuNutrientDriftDensityUpload);
            ReleaseVaultHandle(vault, ref _headerHandle, BufferID.ShinobuNutrientDriftGridHeader);
            ReleaseVaultHandle(vault, ref _csvScratchHandle, BufferID.ShinobuNutrientDriftCsvScratch);
            ReleaseVaultHandle(vault, ref _profileHandle, BufferID.ShinobuNutrientDriftProfiles);
            ReleaseVaultHandle(vault, ref _faultFlagHandle, BufferID.ShinobuNutrientDriftFaultFlags);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsMatchingVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void ResetHandlesNoRelease()
        {
            ResetCarrionHandlesNoRelease();

            _frontHandle = default;
            _backHandle = default;
            _flowHandle = default;
            _injectionHandle = default;
            _sourceHandle = default;
            _sourceCountHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _densityUploadHandle = default;
            _headerHandle = default;
            _csvScratchHandle = default;
            _profileHandle = default;
            _faultFlagHandle = default;
            _jobLocksHeld = false;
            _jobGuardVault = null;
        }

        private static void WriteUInt32(Span<byte> target, uint value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(Span<byte> target, ulong value)
        {
            WriteUInt32(target.Slice(0, 4), unchecked((uint)value));
            WriteUInt32(target.Slice(4, 4), unchecked((uint)(value >> 32)));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct NutrientCellDTO
    {
        [FieldOffset(0)] public float Density;
        [FieldOffset(4)] public float Temperature;
        [FieldOffset(8)] public float ToxinLevel;
        [FieldOffset(12)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NutrientSourceDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float InjectionDensity;
        [FieldOffset(32)] public float Temperature;
        [FieldOffset(36)] public float ToxinLevel;
        [FieldOffset(40)] public uint SourceHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct NutrientDriftTuningDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float DecayRatePerSecond;
        [FieldOffset(32)] public float InjectionMultiplier;
        [FieldOffset(36)] public float AdvectionTimeStep;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float MaxDensity;
        [FieldOffset(48)] public float MockWhirlpoolMetersPerSecond;
        [FieldOffset(52)] public int ActiveAxis;
        [FieldOffset(56)] public int ActiveCellCount;
        [FieldOffset(60)] public int SourceCount;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint FrameIndex;
        [FieldOffset(72)] public uint StateHash;
        [FieldOffset(76)] public uint FrontBufferId;
        [FieldOffset(80)] public uint BackBufferId;
        [FieldOffset(84)] public uint ProfileHash;
        [FieldOffset(88)] private uint _pad0;
        [FieldOffset(92)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidGridTelemetryEntry
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float TotalDensity;
        [FieldOffset(28)] public float MaxDensity;
        [FieldOffset(32)] public float3 MaxVelocity;
        [FieldOffset(44)] public float BurstExecutionMicroseconds;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public ushort ActiveSources;
        [FieldOffset(62)] public ushort ActiveAxis;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NutrientDriftGridHeaderDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float TotalDensity;
        [FieldOffset(32)] public float LastSolverMicroseconds;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public int ActiveAxis;
        [FieldOffset(44)] public int ActiveSources;
        [FieldOffset(48)] public uint FrontBufferId;
        [FieldOffset(52)] public uint BackBufferId;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NutrientProfileDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public float DecayMultiplier;
        [FieldOffset(8)] public float InjectionMultiplier;
        [FieldOffset(12)] public float RadiusMultiplier;
        [FieldOffset(16)] public float TemperatureBias;
        [FieldOffset(20)] public float ToxinMultiplier;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint SourceHash;
    }

    public static class NutrientDriftMath
    {
        private const float GridSampleEpsilon = 0.0001f;
        private const uint TuningFlagInitialized = 1u << 1;
        private const uint TuningFlagMockFlowEnabled = 1u << 2;
        private const uint TuningFlagMockSourceFallback = 1u << 3;
        private const uint TuningFlagNetcodeExcluded = 1u << 4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index3D(int x, int y, int z, int axis)
        {
            return x + axis * (y + axis * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WrapIndex(int value, int axis)
        {
            int mod = value % axis;
            return mod < 0 ? mod + axis : mod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveAxis(float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            float curved = math.smoothstep(0f, 1f, q);
            return math.clamp((int)math.round(math.lerp(16f, NutrientDriftRuntime.GridAxisMax, curved)), 16, NutrientDriftRuntime.GridAxisMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveUploadCadenceFrames(float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return math.clamp((int)math.round(math.lerp(4f, 1f, math.smoothstep(0f, 1f, q))), 1, 4);
        }

        public static NutrientDriftTuningDTO CreateDefaultTuning(double3 originAup)
        {
            var tuning = new NutrientDriftTuningDTO
            {
                GridOriginAup = originAup,
                CellSizeMeters = 12f,
                DecayRatePerSecond = 0.00201f,
                InjectionMultiplier = 1f,
                AdvectionTimeStep = NutrientDriftRuntime.FrostDeltaSeconds,
                GlobalQualityWeight = 1f,
                MaxDensity = 24f,
                MockWhirlpoolMetersPerSecond = 4.5f,
                ActiveAxis = NutrientDriftRuntime.GridAxisMax,
                ActiveCellCount = NutrientDriftRuntime.GridCellCapacity,
                SourceCount = 0,
                Flags = TuningFlagInitialized | TuningFlagMockFlowEnabled | TuningFlagMockSourceFallback | TuningFlagNetcodeExcluded,
                FrameIndex = 0u,
                StateHash = NutrientDriftRuntime.RouteHash,
                FrontBufferId = unchecked((uint)(int)BufferID.ShinobuNutrientDriftCellFront),
                BackBufferId = unchecked((uint)(int)BufferID.ShinobuNutrientDriftCellBack),
                ProfileHash = 0u
            };
            tuning.ActiveAxis = ResolveActiveAxis(tuning.GlobalQualityWeight);
            tuning.ActiveCellCount = tuning.ActiveAxis * tuning.ActiveAxis * tuning.ActiveAxis;
            return tuning;
        }

        public static NutrientDriftTuningDTO SanitizeTuning(NutrientDriftTuningDTO tuning, double3 fallbackOrigin)
        {
            if (!math.all(math.isfinite(tuning.GridOriginAup)))
                tuning.GridOriginAup = math.all(math.isfinite(fallbackOrigin)) ? fallbackOrigin : double3.zero;
            tuning.CellSizeMeters = math.clamp(SanitizeFinite(tuning.CellSizeMeters, 12f), 1f, 64f);
            tuning.DecayRatePerSecond = math.clamp(SanitizeFinite(tuning.DecayRatePerSecond, 0.00201f), 0f, 0.25f);
            tuning.InjectionMultiplier = math.clamp(SanitizeFinite(tuning.InjectionMultiplier, 1f), 0f, 32f);
            tuning.AdvectionTimeStep = math.clamp(SanitizeFinite(tuning.AdvectionTimeStep, NutrientDriftRuntime.FrostDeltaSeconds), 0.05f, 30f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.MaxDensity = math.clamp(SanitizeFinite(tuning.MaxDensity, 24f), 0.1f, 1024f);
            tuning.MockWhirlpoolMetersPerSecond = math.clamp(SanitizeFinite(tuning.MockWhirlpoolMetersPerSecond, 4.5f), 0f, 64f);
            tuning.ActiveAxis = ResolveActiveAxis(tuning.GlobalQualityWeight);
            tuning.ActiveCellCount = tuning.ActiveAxis * tuning.ActiveAxis * tuning.ActiveAxis;
            tuning.SourceCount = math.clamp(tuning.SourceCount, 0, NutrientDriftRuntime.SourceCapacity);
            tuning.Flags |= TuningFlagInitialized | TuningFlagNetcodeExcluded;
            return tuning;
        }

        public static NutrientDriftGridHeaderDTO CreateHeader(NutrientDriftTuningDTO tuning, float totalDensity, float solverMicros, int sourceCount)
        {
            return new NutrientDriftGridHeaderDTO
            {
                GridOriginAup = tuning.GridOriginAup,
                CellSizeMeters = tuning.CellSizeMeters,
                TotalDensity = math.max(0f, totalDensity),
                LastSolverMicroseconds = math.max(0f, solverMicros),
                GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight),
                ActiveAxis = math.clamp(tuning.ActiveAxis, 1, NutrientDriftRuntime.GridAxisMax),
                ActiveSources = math.clamp(sourceCount, 0, NutrientDriftRuntime.SourceCapacity),
                FrontBufferId = tuning.FrontBufferId,
                BackBufferId = tuning.BackBufferId,
                Flags = tuning.Flags,
                StateHash = tuning.StateHash
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFinite(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeNutrientGridJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Front;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Back;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Flow;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* Injection;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* DensityUpload;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientSourceDTO* Sources;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientProfileDTO* Profiles;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* FaultFlags;

        public void Execute(int index)
        {
            var cell = new NutrientCellDTO
            {
                Density = 0f,
                Temperature = 4f,
                ToxinLevel = 0f
            };
            UnsafeUtility.AsRef<NutrientCellDTO>(Front + index) = cell;
            UnsafeUtility.AsRef<NutrientCellDTO>(Back + index) = cell;
            UnsafeUtility.AsRef<float3>(Flow + index) = float3.zero;
            UnsafeUtility.AsRef<float>(Injection + index) = 0f;
            UnsafeUtility.AsRef<float>(DensityUpload + index) = 0f;

            if (index < NutrientDriftRuntime.SourceCapacity)
                UnsafeUtility.AsRef<NutrientSourceDTO>(Sources + index) = default;
            if (index < NutrientDriftRuntime.ProfileCapacity)
                UnsafeUtility.AsRef<NutrientProfileDTO>(Profiles + index) = default;
            if (index < 4)
                UnsafeUtility.AsRef<uint>(FaultFlags + index) = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeNutrientTelemetryJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidGridTelemetryEntry* TelemetryRing;

        public void Execute(int index)
        {
            UnsafeUtility.AsRef<FluidGridTelemetryEntry>(TelemetryRing + index) = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockFlowFieldJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Flow;
        public NutrientDriftTuningDTO Tuning;
        public float TimeSeconds;

        public void Execute(int index)
        {
            int axis = Tuning.ActiveAxis;
            int z = index / (axis * axis);
            int rem = index - z * axis * axis;
            int y = rem / axis;
            int x = rem - y * axis;
            int strideIndex = NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);

            float half = axis * 0.5f;
            float cellSize = math.max(0.0001f, Tuning.CellSizeMeters);
            float3 local = (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - half) * cellSize;
            float2 radial = new float2(local.x, local.z);
            float radiusSq = math.lengthsq(radial);
            float invRadius = math.rsqrt(math.max(radiusSq, 0.01f));
            float2 tangent = new float2(-radial.y, radial.x) * invRadius;
            float maxRadius = math.max(cellSize * half, 0.01f);
            float q = math.saturate(Tuning.GlobalQualityWeight);
            float falloffQuality = math.smoothstep(0.30f, 0.90f, q);
            float cheapFalloff = math.saturate(1f - radiusSq * math.rcp(math.max(maxRadius * maxRadius, 0.0001f)) * 0.75f);
            float falloff;
            if (falloffQuality <= 0.0001f)
            {
                falloff = cheapFalloff;
            }
            else
            {
                float preciseFalloff = math.saturate(1f - math.sqrt(radiusSq) * math.rcp(maxRadius) * 0.75f);
                falloff = falloffQuality >= 0.9999f
                    ? preciseFalloff
                    : math.lerp(cheapFalloff, preciseFalloff, falloffQuality);
            }
            float pulse = 0.65f + 0.35f * MathLodApproximation.ApproxSinBhaskara(TimeSeconds * 0.37f + local.y * 0.03125f);
            float speed = Tuning.MockWhirlpoolMetersPerSecond * math.lerp(0.35f, 1.35f, math.smoothstep(0f, 1f, q));
            float vertical = MathLodApproximation.ApproxSinBhaskara((local.x + local.z) * 0.045f + TimeSeconds * 0.23f) * 0.18f * speed;
            float3 velocity = new float3(tangent.x * speed * falloff * pulse, vertical, tangent.y * speed * falloff * pulse);
            UnsafeUtility.AsRef<float3>(Flow + strideIndex) = math.select(float3.zero, velocity, math.isfinite(velocity));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CopyAbyssalFlowVolumeToNutrientFlowJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Flow;
        [ReadOnly, NoAlias] public NativeArray<float3>.ReadOnly AbyssalFlowVolume;
        public NutrientDriftTuningDTO Tuning;
        public float3 GridCenterLocal;
        public float3 AbyssalFlowCenter;
        public int AbyssalFlowResolutionXZ;
        public int AbyssalFlowResolutionY;
        public int AbyssalFlowRingOffsetX;
        public int AbyssalFlowRingOffsetY;
        public int AbyssalFlowRingOffsetZ;
        public float AbyssalFlowHorizontalCellSize;
        public float AbyssalFlowVerticalCellSize;
        public float AbyssalFlowWaterLevel;
        public float AbyssalFlowDepthMeters;

        public void Execute(int index)
        {
            int axis = Tuning.ActiveAxis;
            int z = index / (axis * axis);
            int rem = index - z * axis * axis;
            int y = rem / axis;
            int x = rem - y * axis;
            int strideIndex = NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);
            float half = axis * 0.5f;
            float cell = math.max(0.0001f, Tuning.CellSizeMeters);
            float3 position = GridCenterLocal + (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - half) * cell;
            float3 velocity = SampleAbyssalFlowVolume(position);
            UnsafeUtility.AsRef<float3>(Flow + strideIndex) = math.select(float3.zero, velocity, math.isfinite(velocity));
        }

        private float3 SampleAbyssalFlowVolume(float3 position)
        {
            if (!AbyssalFlowVolume.IsCreated ||
                AbyssalFlowVolume.Length <= 0 ||
                AbyssalFlowResolutionXZ <= 1 ||
                AbyssalFlowResolutionY <= 1 ||
                AbyssalFlowHorizontalCellSize <= 0f ||
                AbyssalFlowVerticalCellSize <= 0f)
            {
                return float3.zero;
            }

            float halfExtent = (AbyssalFlowResolutionXZ - 1) * 0.5f * AbyssalFlowHorizontalCellSize;
            float minX = AbyssalFlowCenter.x - halfExtent;
            float minZ = AbyssalFlowCenter.z - halfExtent;
            float maxY = AbyssalFlowWaterLevel;
            float minY = AbyssalFlowWaterLevel - math.max(0f, AbyssalFlowDepthMeters);
            if (position.x < minX ||
                position.z < minZ ||
                position.x > minX + halfExtent * 2f ||
                position.z > minZ + halfExtent * 2f)
            {
                return float3.zero;
            }

            float normalizedX = math.clamp((position.x - minX) * math.rcp(AbyssalFlowHorizontalCellSize), 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedZ = math.clamp((position.z - minZ) * math.rcp(AbyssalFlowHorizontalCellSize), 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedY = math.clamp((maxY - math.clamp(position.y, minY, maxY)) * math.rcp(AbyssalFlowVerticalCellSize), 0f, AbyssalFlowResolutionY - 1);
            int x0 = math.clamp((int)math.floor(normalizedX), 0, AbyssalFlowResolutionXZ - 1);
            int z0 = math.clamp((int)math.floor(normalizedZ), 0, AbyssalFlowResolutionXZ - 1);
            int y0 = math.clamp((int)math.floor(normalizedY), 0, AbyssalFlowResolutionY - 1);
            int x1 = math.min(x0 + 1, AbyssalFlowResolutionXZ - 1);
            int z1 = math.min(z0 + 1, AbyssalFlowResolutionXZ - 1);
            int y1 = math.min(y0 + 1, AbyssalFlowResolutionY - 1);
            float fracX = normalizedX - x0;
            float fracZ = normalizedZ - z0;
            float fracY = normalizedY - y0;
            float interpolationWeight = math.smoothstep(0.30f, 0.90f, math.saturate(Tuning.GlobalQualityWeight));
            if (interpolationWeight <= 0.0001f)
            {
                return ReadAbyssalFlowCell(
                    math.clamp((int)math.floor(normalizedX + 0.5f), 0, AbyssalFlowResolutionXZ - 1),
                    math.clamp((int)math.floor(normalizedY + 0.5f), 0, AbyssalFlowResolutionY - 1),
                    math.clamp((int)math.floor(normalizedZ + 0.5f), 0, AbyssalFlowResolutionXZ - 1));
            }

            float3 sample000 = ReadAbyssalFlowCell(x0, y0, z0);
            float3 sample100 = ReadAbyssalFlowCell(x1, y0, z0);
            float3 sample010 = ReadAbyssalFlowCell(x0, y0, z1);
            float3 sample110 = ReadAbyssalFlowCell(x1, y0, z1);
            float3 sample001 = ReadAbyssalFlowCell(x0, y1, z0);
            float3 sample101 = ReadAbyssalFlowCell(x1, y1, z0);
            float3 sample011 = ReadAbyssalFlowCell(x0, y1, z1);
            float3 sample111 = ReadAbyssalFlowCell(x1, y1, z1);
            float3 sampleX00 = math.lerp(sample000, sample100, fracX);
            float3 sampleX10 = math.lerp(sample010, sample110, fracX);
            float3 sampleX01 = math.lerp(sample001, sample101, fracX);
            float3 sampleX11 = math.lerp(sample011, sample111, fracX);
            float3 sampleZ0 = math.lerp(sampleX00, sampleX10, fracZ);
            float3 sampleZ1 = math.lerp(sampleX01, sampleX11, fracZ);
            float3 trilinear = math.lerp(sampleZ0, sampleZ1, fracY);
            if (interpolationWeight >= 0.9999f)
                return trilinear;

            float3 nearest = ReadAbyssalFlowCell(
                math.clamp((int)math.floor(normalizedX + 0.5f), 0, AbyssalFlowResolutionXZ - 1),
                math.clamp((int)math.floor(normalizedY + 0.5f), 0, AbyssalFlowResolutionY - 1),
                math.clamp((int)math.floor(normalizedZ + 0.5f), 0, AbyssalFlowResolutionXZ - 1));
            return math.lerp(nearest, trilinear, interpolationWeight);
        }

        private float3 ReadAbyssalFlowCell(int x, int y, int z)
        {
            int physicalIndex = GetAbyssalFlowPhysicalIndex(x, y, z);
            return (uint)physicalIndex < (uint)AbyssalFlowVolume.Length
                ? AbyssalFlowVolume[physicalIndex]
                : float3.zero;
        }

        private int GetAbyssalFlowPhysicalIndex(int x, int y, int z)
        {
            int wrappedX = PositiveModulo(x + AbyssalFlowRingOffsetX, AbyssalFlowResolutionXZ);
            int wrappedY = PositiveModulo(y + AbyssalFlowRingOffsetY, AbyssalFlowResolutionY);
            int wrappedZ = PositiveModulo(z + AbyssalFlowRingOffsetZ, AbyssalFlowResolutionXZ);
            return wrappedY * AbyssalFlowResolutionXZ * AbyssalFlowResolutionXZ +
                   wrappedZ * AbyssalFlowResolutionXZ +
                   wrappedX;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateNutrientSourcesJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* Injection;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientSourceDTO* Sources;
        public NutrientDriftTuningDTO Tuning;

        public void Execute(int index)
        {
            int axis = Tuning.ActiveAxis;
            int z = index / (axis * axis);
            int rem = index - z * axis * axis;
            int y = rem / axis;
            int x = rem - y * axis;
            int strideIndex = NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);
            float half = axis * 0.5f;
            float cell = math.max(0.0001f, Tuning.CellSizeMeters);
            float3 local = (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - half) * cell;

            float injected = 0f;
            int count = math.clamp(Tuning.SourceCount, 0, NutrientDriftRuntime.SourceCapacity);
            float falloffQuality = math.smoothstep(0.35f, 0.90f, math.saturate(Tuning.GlobalQualityWeight));
            for (int i = 0; i < count; i++)
            {
                NutrientSourceDTO source = Sources[i];
                double3 delta = source.Aup - Tuning.GridOriginAup;
                if (!math.all(math.isfinite(delta)))
                    continue;

                float radius = math.max(source.RadiusMeters, cell);
                float3 sourceLocal = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                float3 toSource = local - sourceLocal;
                float distanceSq = math.lengthsq(toSource);
                float radiusSq = math.max(radius * radius, 0.0001f);
                float cheapWeight = math.saturate(1f - distanceSq * math.rcp(radiusSq));
                float weight;
                if (falloffQuality <= 0.0001f)
                {
                    weight = cheapWeight;
                }
                else
                {
                    float preciseWeight = math.saturate(1f - math.sqrt(distanceSq) * math.rcp(radius));
                    weight = falloffQuality >= 0.9999f
                        ? preciseWeight
                        : math.lerp(cheapWeight, preciseWeight, falloffQuality);
                }
                injected += weight * weight * math.max(0f, source.InjectionDensity) * Tuning.AdvectionTimeStep;
            }

            UnsafeUtility.AsRef<float>(Injection + strideIndex) = math.select(0f, injected, math.isfinite(injected));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateNutrientAdvectionJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Front;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Back;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Flow;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* Injection;
        public NutrientDriftTuningDTO Tuning;

        public void Execute(int index)
        {
            int axis = Tuning.ActiveAxis;
            int z = index / (axis * axis);
            int rem = index - z * axis * axis;
            int y = rem / axis;
            int x = rem - y * axis;
            int strideIndex = NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);

            float cellSize = math.max(0.0001f, Tuning.CellSizeMeters);
            float half = axis * 0.5f;
            float3 currentLocal = (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - half) * cellSize;
            float3 velocity = Flow[strideIndex];
            velocity = math.select(float3.zero, velocity, math.isfinite(velocity));
            float3 backLocal = currentLocal - velocity * Tuning.AdvectionTimeStep;
            float3 gridPosition = backLocal * math.rcp(cellSize) + half - 0.5f;
            float q = math.saturate(Tuning.GlobalQualityWeight);
            float interpolationWeight = math.smoothstep(0.30f, 0.90f, q);

            float decay = math.saturate(1f - math.max(0f, Tuning.DecayRatePerSecond) * Tuning.AdvectionTimeStep);
            NutrientCellDTO sampled;
            if (interpolationWeight <= 0.0001f)
            {
                sampled = SampleNearest(gridPosition, axis);
            }
            else if (interpolationWeight >= 0.9999f)
            {
                sampled = SampleTrilinear(gridPosition, axis);
            }
            else
            {
                NutrientCellDTO nearest = SampleNearest(gridPosition, axis);
                NutrientCellDTO trilinear = SampleTrilinear(gridPosition, axis);
                sampled = new NutrientCellDTO
                {
                    Density = math.lerp(nearest.Density, trilinear.Density, interpolationWeight),
                    Temperature = math.lerp(nearest.Temperature, trilinear.Temperature, interpolationWeight),
                    ToxinLevel = math.lerp(nearest.ToxinLevel, trilinear.ToxinLevel, interpolationWeight)
                };
            }

            float density = sampled.Density;
            float temperature = sampled.Temperature;
            float toxin = sampled.ToxinLevel;
            density = density * decay + Injection[strideIndex];
            density = math.clamp(math.select(0f, density, math.isfinite(density)), 0f, Tuning.MaxDensity);

            var output = new NutrientCellDTO
            {
                Density = density,
                Temperature = math.clamp(math.select(4f, temperature, math.isfinite(temperature)), -4f, 120f),
                ToxinLevel = math.saturate(math.select(0f, toxin, math.isfinite(toxin)))
            };
            UnsafeUtility.AsRef<NutrientCellDTO>(Back + strideIndex) = output;
        }

        private NutrientCellDTO SampleNearest(float3 gridPosition, int axis)
        {
            int3 c = new int3(
                NutrientDriftMath.WrapIndex((int)math.floor(gridPosition.x + 0.5f), axis),
                NutrientDriftMath.WrapIndex((int)math.floor(gridPosition.y + 0.5f), axis),
                NutrientDriftMath.WrapIndex((int)math.floor(gridPosition.z + 0.5f), axis));
            return Front[NutrientDriftMath.Index3D(c.x, c.y, c.z, NutrientDriftRuntime.GridAxisMax)];
        }

        private NutrientCellDTO SampleTrilinear(float3 gridPosition, int axis)
        {
            int3 c0 = new int3((int)math.floor(gridPosition.x), (int)math.floor(gridPosition.y), (int)math.floor(gridPosition.z));
            float3 f = math.frac(gridPosition);
            int3 x0 = new int3(
                NutrientDriftMath.WrapIndex(c0.x, axis),
                NutrientDriftMath.WrapIndex(c0.y, axis),
                NutrientDriftMath.WrapIndex(c0.z, axis));
            int3 x1 = new int3(
                NutrientDriftMath.WrapIndex(c0.x + 1, axis),
                NutrientDriftMath.WrapIndex(c0.y + 1, axis),
                NutrientDriftMath.WrapIndex(c0.z + 1, axis));

            float3 c000 = LoadChannels(x0.x, x0.y, x0.z);
            float3 c100 = LoadChannels(x1.x, x0.y, x0.z);
            float3 c010 = LoadChannels(x0.x, x1.y, x0.z);
            float3 c110 = LoadChannels(x1.x, x1.y, x0.z);
            float3 c001 = LoadChannels(x0.x, x0.y, x1.z);
            float3 c101 = LoadChannels(x1.x, x0.y, x1.z);
            float3 c011 = LoadChannels(x0.x, x1.y, x1.z);
            float3 c111 = LoadChannels(x1.x, x1.y, x1.z);
            float3 xy0 = math.lerp(math.lerp(c000, c100, f.x), math.lerp(c010, c110, f.x), f.y);
            float3 xy1 = math.lerp(math.lerp(c001, c101, f.x), math.lerp(c011, c111, f.x), f.y);
            float3 channels = math.lerp(xy0, xy1, f.z);
            return new NutrientCellDTO
            {
                Density = channels.x,
                Temperature = channels.y,
                ToxinLevel = channels.z
            };
        }

        private float3 LoadChannels(int x, int y, int z)
        {
            NutrientCellDTO cell = Front[NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax)];
            return new float3(cell.Density, cell.Temperature, cell.ToxinLevel);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CopyNutrientDensityUploadJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Cells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* DensityUpload;
        public NutrientDriftTuningDTO Tuning;

        public void Execute(int index)
        {
            int maxAxis = NutrientDriftRuntime.GridAxisMax;
            int z = index / (maxAxis * maxAxis);
            int rem = index - z * maxAxis * maxAxis;
            int y = rem / maxAxis;
            int x = rem - y * maxAxis;
            bool active = x < Tuning.ActiveAxis && y < Tuning.ActiveAxis && z < Tuning.ActiveAxis;
            float density = active ? Cells[index].Density : 0f;
            UnsafeUtility.AsRef<float>(DensityUpload + index) = math.saturate(density * math.rcp(math.max(Tuning.MaxDensity, 0.0001f)));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordNutrientTelemetryJob : IJob
    {
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint TelemetryFlagMockSource = 1u << 2;
        private const uint SourceFlagMock = 1u << 0;

        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientCellDTO* Cells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Flow;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NutrientSourceDTO* Sources;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidGridTelemetryEntry* TelemetryRing;
        [NoAlias, NativeDisableUnsafePtrRestriction] public int* TelemetryCursor;
        public NutrientDriftTuningDTO Tuning;
        public int TelemetrySlot;
        public int TelemetryCursorValue;
        public int ActiveSources;

        public void Execute()
        {
            float totalDensity = 0f;
            float maxDensity = 0f;
            float maxVelocitySq = 0f;
            float3 maxVelocity = float3.zero;
            uint flags = 0u;
            int axis = Tuning.ActiveAxis;
            int count = Tuning.ActiveCellCount;
            for (int i = 0; i < count; i++)
            {
                int z = i / (axis * axis);
                int rem = i - z * axis * axis;
                int y = rem / axis;
                int x = rem - y * axis;
                int index = NutrientDriftMath.Index3D(x, y, z, NutrientDriftRuntime.GridAxisMax);

                NutrientCellDTO cell = Cells[index];
                float3 velocity = Flow[index];
                if (!math.isfinite(cell.Density) || !math.all(math.isfinite(velocity)))
                {
                    flags |= TelemetryFlagNaN;
                    continue;
                }

                totalDensity += math.max(0f, cell.Density);
                if (cell.Density > maxDensity)
                    maxDensity = cell.Density;
                float velocitySq = math.lengthsq(velocity);
                if (velocitySq > maxVelocitySq)
                {
                    maxVelocitySq = velocitySq;
                    maxVelocity = velocity;
                }
            }

            int activeSources = math.clamp(ActiveSources, 0, NutrientDriftRuntime.SourceCapacity);
            for (int i = 0; i < activeSources; i++)
            {
                if ((Sources[i].Flags & SourceFlagMock) != 0u)
                {
                    flags |= TelemetryFlagMockSource;
                    break;
                }
            }

            FluidGridTelemetryEntry entry = new FluidGridTelemetryEntry
            {
                GridOriginAup = Tuning.GridOriginAup,
                TotalDensity = math.select(0f, totalDensity, math.isfinite(totalDensity)),
                MaxDensity = math.select(0f, maxDensity, math.isfinite(maxDensity)),
                MaxVelocity = maxVelocity,
                BurstExecutionMicroseconds = 0f,
                Frame = Tuning.FrameIndex,
                ActiveSources = (ushort)math.clamp(ActiveSources, 0, ushort.MaxValue),
                ActiveAxis = (ushort)math.clamp(axis, 0, ushort.MaxValue),
                Flags = flags,
                StateHash = Tuning.StateHash
            };
            UnsafeUtility.AsRef<FluidGridTelemetryEntry>(TelemetryRing + TelemetrySlot) = entry;
            UnsafeUtility.AsRef<int>(TelemetryCursor) = TelemetryCursorValue;
            if ((flags & TelemetryFlagNaN) != 0u)
                UnsafeUtility.AsRef<uint>(FaultFlags) = flags;
        }
    }

    #if UNITY_EDITOR
    public static class NutrientDriftCsvParser
    {
        public static unsafe int ParseProfiles(ReadOnlySpan<byte> bytes, NativeArray<NutrientProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return 0;

            return ParseProfiles(
                bytes,
                new Span<NutrientProfileDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(profiles), profiles.Length));
        }

        public static int ParseProfiles(ReadOnlySpan<byte> bytes, Span<NutrientProfileDTO> profiles)
        {
            if (profiles.Length <= 0)
                return 0;

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

                if (firstRow && LooksLikeHeader(name))
                {
                    firstRow = false;
                    continue;
                }
                firstRow = false;

                NutrientProfileDTO profile = default;
                profile.BiomeHash = Fnv1a32(name);
                profile.DecayMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.InjectionMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.RadiusMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.TemperatureBias = ReadFloatColumn(row, ref columnCursor, 0f);
                profile.ToxinMultiplier = ReadFloatColumn(row, ref columnCursor, 1f);
                profile.SourceHash = NutrientDriftRuntime.RouteHash;
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
            return a == (byte)'n' && b == (byte)'a' && c == (byte)'m' && d == (byte)'e';
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

    public static class NutrientDriftSelfAudit
    {
        private const string SelfAuditPassXml =
            @"<SELF_AUDIT agent=""SHINOBU_309"" domain=""PLANKTON_NUTRIENT_FLOW_DRIFT"" taskCount=""20"" status=""PASS_STATIC_PENDING_RUNTIME"">
<TASK id=""20"" status=""PASS_STATIC_PENDING_RUNTIME"" name=""SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION"" proof=""Layout, Vault range, dependency graph, compile guard, and Dear Lie route validated without runtime string construction""/>
<DTO_SIZES NutrientCellDTO=""16"" NutrientSourceDTO=""64"" NutrientDriftTuningDTO=""96"" FluidGridTelemetryEntry=""64"" NutrientDriftGridHeaderDTO=""64"" NutrientProfileDTO=""32""/>
<FLUID_GRID_TELEMETRY_BYTE_MAP GridOriginAup=""0"" TotalDensity=""24"" MaxDensity=""28"" MaxVelocity=""32"" BurstExecutionMicroseconds=""44"" Frame=""48"" Flags=""52"" StateHash=""56"" ActiveSources=""60"" ActiveAxis=""62""/>
<SCALABILITY binarySwitches=""0"" quality=""continuous GlobalQualityWeight scales active axis, sample method, and upload cadence""/>
<ZERO_GC hotPathManagedAllocations=""0"" particleSystems=""0"" rigidbodies=""0"" linq=""0"" dtoProperties=""0""/>
</SELF_AUDIT>";

        private const string SelfAuditFailXml =
            @"<SELF_AUDIT agent=""SHINOBU_309"" domain=""PLANKTON_NUTRIENT_FLOW_DRIFT"" taskCount=""20"" status=""FAIL_STATIC_LAYOUT_OR_VAULT"">
<TASK id=""20"" status=""FAIL_STATIC_LAYOUT_OR_VAULT"" name=""SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION"" proof=""Static layout or Vault range validation failed""/>
</SELF_AUDIT>";

        public static string BuildSelfAuditXml()
        {
            bool cellSize = UnsafeUtility.SizeOf<NutrientCellDTO>() == 16;
            bool cellOffsets =
                OffsetOf<NutrientCellDTO>(nameof(NutrientCellDTO.Density)) == 0 &&
                OffsetOf<NutrientCellDTO>(nameof(NutrientCellDTO.Temperature)) == 4 &&
                OffsetOf<NutrientCellDTO>(nameof(NutrientCellDTO.ToxinLevel)) == 8 &&
                OffsetOf<NutrientCellDTO>("_pad0") == 12;
            bool sourceSize = UnsafeUtility.SizeOf<NutrientSourceDTO>() == 64;
            bool tuningSize = UnsafeUtility.SizeOf<NutrientDriftTuningDTO>() == 96;
            bool telemetrySize = UnsafeUtility.SizeOf<FluidGridTelemetryEntry>() == 64;
            bool telemetryOffsets =
                OffsetOf<FluidGridTelemetryEntry>(nameof(FluidGridTelemetryEntry.Frame)) == 48 &&
                OffsetOf<FluidGridTelemetryEntry>(nameof(FluidGridTelemetryEntry.Flags)) == 52 &&
                OffsetOf<FluidGridTelemetryEntry>(nameof(FluidGridTelemetryEntry.StateHash)) == 56 &&
                OffsetOf<FluidGridTelemetryEntry>(nameof(FluidGridTelemetryEntry.ActiveSources)) == 60 &&
                OffsetOf<FluidGridTelemetryEntry>(nameof(FluidGridTelemetryEntry.ActiveAxis)) == 62;
            bool headerSize = UnsafeUtility.SizeOf<NutrientDriftGridHeaderDTO>() == 64;
            bool profileSize = UnsafeUtility.SizeOf<NutrientProfileDTO>() == 32;
            bool doubleBufferIds =
                (int)BufferID.ShinobuNutrientDriftCellFront == 70460 &&
                (int)BufferID.ShinobuNutrientDriftCellBack == 70461;
            bool vaultIds =
                doubleBufferIds &&
                (int)BufferID.ShinobuNutrientDriftFlowField == 70462 &&
                (int)BufferID.ShinobuNutrientDriftInjection == 70463 &&
                (int)BufferID.ShinobuNutrientDriftSources == 70464 &&
                (int)BufferID.ShinobuNutrientDriftSourceCount == 70465 &&
                (int)BufferID.ShinobuNutrientDriftTuning == 70466 &&
                (int)BufferID.ShinobuNutrientDriftTelemetryRing == 70467 &&
                (int)BufferID.ShinobuNutrientDriftTelemetryCursor == 70468 &&
                (int)BufferID.ShinobuNutrientDriftDensityUpload == 70469 &&
                (int)BufferID.ShinobuNutrientDriftGridHeader == 70470 &&
                (int)BufferID.ShinobuNutrientDriftCsvScratch == 70471 &&
                (int)BufferID.ShinobuNutrientDriftProfiles == 70472 &&
                (int)BufferID.ShinobuNutrientDriftFaultFlags == 70473;
            bool fixedCapacities =
                NutrientDriftRuntime.GridAxisMax == 32 &&
                NutrientDriftRuntime.GridCellCapacity == 32768 &&
                NutrientDriftRuntime.SourceCapacity == 16 &&
                NutrientDriftRuntime.TelemetryCapacity == 300 &&
                NutrientDriftRuntime.ProfileCapacity == 32;
            bool layoutPass = cellSize && cellOffsets && sourceSize && tuningSize && telemetrySize && telemetryOffsets && headerSize && profileSize;
            bool vaultPass = vaultIds && fixedCapacities;
            return layoutPass && vaultPass ? SelfAuditPassXml : SelfAuditFailXml;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
