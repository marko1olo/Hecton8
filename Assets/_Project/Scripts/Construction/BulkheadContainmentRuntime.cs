using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Construction
{
    [DefaultExecutionOrder(-180)]
    public sealed unsafe partial class BulkheadContainmentRuntime : MonoBehaviour, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const float DefaultPlayerRadiusMeters = 0.38f;
        private const float DefaultCloseSpeed = 2.4f;
        private const float DefaultOpenSpeed = 3.0f;
        private const float DefaultOverrideDistance = 3.2f;
        private const float DefaultCatastrophicIntegrity = 0.18f;
        private const float LockedSimulationTickDeltaSeconds = 1f / 60f;
        private const float SimulationAuthorityDeltaCeilingSeconds = 0.2f;
        private const float AuthoritativeQualityWeight = 1f;
        private const uint MockSeed = 0x53484E42u;
        private const SystemID OwnerSystemId = SystemID.Construction;
        private const ulong BulkheadProfileImportMutationGuardMask = 1UL << 58;
        private const ulong BulkheadTelemetryMutationGuardMask = 1UL << 59;
        private const ulong BulkheadRefreshMutationGuardMask = 1UL << 60;
        private const ulong BulkheadJobMutationGuardMask =
            (1UL << ((int)BufferID.Shinobu220BulkheadStates & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadAups & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadPlanes & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadCsrEdges & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadEdgeConductivity & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadFluidFlow & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadModuleIntegrity & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadCollisionResults & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadTelemetryRing & 31)) |
            (1UL << ((int)BufferID.Shinobu220BulkheadTelemetryCursor & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchStates & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchTelemetryRing & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchTelemetryCursor & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchTuning & 31)) |
            (1UL << ((int)BufferID.Shinobu343HatchMockFluidCompartments & 31)) |
            (1UL << ((int)BufferID.ShinobuFluidCompartmentFront & 31)) |
            (1UL << ((int)BufferID.StructuralIntegrityStates & 31));
        private const uint BulkheadJobPinStates = 1u << 0;
        private const uint BulkheadJobPinAups = 1u << 1;
        private const uint BulkheadJobPinPlanes = 1u << 2;
        private const uint BulkheadJobPinCsrEdges = 1u << 3;
        private const uint BulkheadJobPinEdgeConductivity = 1u << 4;
        private const uint BulkheadJobPinFluidFlow = 1u << 5;
        private const uint BulkheadJobPinModuleIntegrity = 1u << 6;
        private const uint BulkheadJobPinCollisionResults = 1u << 7;
        private const uint BulkheadJobPinTelemetry = 1u << 8;
        private const uint BulkheadJobPinTelemetryCursor = 1u << 9;
        private const uint BulkheadJobPinHatchStates = 1u << 10;
        private const uint BulkheadJobPinHatchTelemetry = 1u << 11;
        private const uint BulkheadJobPinHatchTelemetryCursor = 1u << 12;
        private const uint BulkheadJobPinHatchTuning = 1u << 13;
        private const uint BulkheadJobPinHatchMockFluid = 1u << 14;
        private const uint BulkheadJobPinHatchFluidFront = 1u << 15;
        private const uint BulkheadJobPinHatchStructural = 1u << 16;
        private const uint BulkheadRequiredJobPinMask =
            BulkheadJobPinStates |
            BulkheadJobPinAups |
            BulkheadJobPinPlanes |
            BulkheadJobPinCsrEdges |
            BulkheadJobPinEdgeConductivity |
            BulkheadJobPinFluidFlow |
            BulkheadJobPinModuleIntegrity |
            BulkheadJobPinCollisionResults |
            BulkheadJobPinTelemetry |
            BulkheadJobPinTelemetryCursor |
            BulkheadJobPinHatchStates |
            BulkheadJobPinHatchTelemetry |
            BulkheadJobPinHatchTelemetryCursor |
            BulkheadJobPinHatchTuning |
            BulkheadJobPinHatchMockFluid;

        private static readonly int GlobalBulkheadStatesId = Shader.PropertyToID("_GlobalBulkheadStates");
        private static readonly int GlobalBulkheadParamsId = Shader.PropertyToID("_GlobalBulkheadParams");

        private static BulkheadContainmentRuntime s_active;

        [SerializeField, Range(1, BulkheadContainmentConstants.DefaultBulkheadCapacity)]
        private int bulkheadCapacity = BulkheadContainmentConstants.DefaultBulkheadCapacity;
        [SerializeField, Min(0.05f)] private float closeSpeedPerSecond = DefaultCloseSpeed;
        [SerializeField, Min(0.05f)] private float openSpeedPerSecond = DefaultOpenSpeed;
        [SerializeField, Min(0.5f)] private float overrideDistanceMeters = DefaultOverrideDistance;
        [SerializeField, Range(0.01f, 0.99f)] private float catastrophicIntegrity01 = DefaultCatastrophicIntegrity;
        [SerializeField] private bool generateMockBulkheads;
        [SerializeField] private bool uploadShaderBuffer = true;

        private IDataVault _vault;
        private IDataVault _pendingVault;
        private VaultGenerationHandle<BulkheadStateDTO> _statesHandle;
        private VaultGenerationHandle<double3> _aupsHandle;
        private VaultGenerationHandle<BulkheadPlaneDTO> _planesHandle;
        private VaultGenerationHandle<BulkheadCsrEdgeDTO> _csrEdgesHandle;
        private VaultGenerationHandle<float> _edgeConductivityHandle;
        private VaultGenerationHandle<float> _fluidFlowHandle;
        private VaultGenerationHandle<float> _moduleIntegrityHandle;
        private VaultGenerationHandle<BulkheadTuningDTO> _tuningHandle;
        private VaultGenerationHandle<BulkheadTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<uint> _telemetryCursorHandle;
        private VaultGenerationHandle<BulkheadCollisionResultDTO> _collisionResultsHandle;
        private VaultGenerationHandle<BulkheadProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<BulkheadStateDTO> _shaderUploadHandle;
        private VaultGenerationHandle<BulkheadContainmentIntentDTO> _intentRingHandle;
        private VaultGenerationHandle<BulkheadContainmentIntentControlDTO> _intentControlHandle;
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerKinematicStateHandle;
        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredHotSwap;
        private bool _vaultRebindPending;
        private bool _vaultInitialized;
        private bool _defaultsInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _layoutFaultTelemetryWritten;
        private bool _mockGenerated;
        private int _activeCount;
        private uint _lastFrame;
        private float _authorityAccumulator;
        private float _lastScheduleMicroseconds;
        private uint _lastTelemetryFrame;
        private uint _lastTelemetryCollisionEdgeHash;
        private float _lastTelemetryAverageClosure;
        private float _lastTelemetryCollisionDepthMeters;
        private JobHandle _preSimulationHandle;
        private JobHandle _simulationHandle;
        private bool _preSimulationScheduled;
        private bool _simulationScheduled;
        private IDataVault _bulkheadJobPinVault;
        private uint _bulkheadJobPinMask;
        private GraphicsBuffer _shaderStateBufferA;
        private GraphicsBuffer _shaderStateBufferB;
        private uint _lastShaderUploadHash;
        private int _lastShaderUploadCount;
        private byte _shaderWriteBufferSlot;
        private byte _shaderReadBufferSlot;
        private bool _shaderHasValidReadBuffer;
        private bool _shaderGlobalsActive;
        private bool _shaderUploadDirty = true;
        private bool _shutdownStarted;
        private uint _lastDumpedTelemetryCursor;
        private uint _lastDumpAttemptTelemetryCursor;
        private uint _nextPlayerStateHandleBindFrame;
        private string _dumpPath;

        public static bool TryReadEditorState(
            out int activeCount,
            out float quality,
            out float cadenceHz,
            out float lastScheduleMicroseconds,
            out uint telemetryFrame,
            out float averageClosure,
            out uint collisionEdgeHash,
            out float collisionDepthMeters,
            out int shaderUploadCount)
        {
            activeCount = 0;
            quality = 0f;
            cadenceHz = 0f;
            lastScheduleMicroseconds = 0f;
            telemetryFrame = 0u;
            averageClosure = 0f;
            collisionEdgeHash = 0u;
            collisionDepthMeters = 0f;
            shaderUploadCount = 0;
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            activeCount = runtime._activeCount;
            quality = ResolveBulkheadQualityWeight();
            cadenceHz = ResolveAuthorityCadenceHz();
            lastScheduleMicroseconds = runtime._lastScheduleMicroseconds;
            telemetryFrame = runtime._lastTelemetryFrame;
            averageClosure = runtime._lastTelemetryAverageClosure;
            collisionEdgeHash = runtime._lastTelemetryCollisionEdgeHash;
            collisionDepthMeters = runtime._lastTelemetryCollisionDepthMeters;
            shaderUploadCount = runtime._lastShaderUploadCount;
            return true;
        }

        public static bool TryApplyEditorTuning(float closeSpeed, float openSpeed, float overrideDistance, float catastrophicIntegrity)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            runtime.closeSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(closeSpeed, 2f));
            runtime.openSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(openSpeed, 2.5f));
            runtime.overrideDistanceMeters = math.max(0.5f, BulkheadContainmentMath.SanitizePositive(overrideDistance, 3f));
            runtime.catastrophicIntegrity01 = BulkheadContainmentMath.Sanitize01(catastrophicIntegrity, 0.35f);
            runtime.TryWriteTuningRow();
            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadProfilesFromCsvBytes(ReadOnlySpan<byte> csv)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.BootstrapVaultState(vault))
                return false;

            if (!vault.TryAcquireMutationGuard(BulkheadProfileImportMutationGuardMask))
                return false;

            try
            {
                if (!IsBulkheadVaultHandle(in runtime._profilesHandle, BufferID.Shinobu220BulkheadProfiles) ||
                    !vault.TryResolveHandle(in runtime._profilesHandle, out NativeArray<BulkheadProfileDTO> profiles) ||
                    !profiles.IsCreated ||
                    profiles.Length == 0)
                {
                    return false;
                }

                return ParseProfiles(csv, profiles) > 0;
            }
            finally
            {
                vault.ReleaseMutationGuard(BulkheadProfileImportMutationGuardMask);
            }
        }

        public static bool TryLoadProfilesFromCsvFile(string path)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null || string.IsNullOrEmpty(path))
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.BootstrapVaultState(vault))
                return false;

            if (!vault.TryAcquireMutationGuard(BulkheadProfileImportMutationGuardMask))
                return false;

            try
            {
                if (!IsBulkheadVaultHandle(in runtime._profilesHandle, BufferID.Shinobu220BulkheadProfiles) ||
                    !IsBulkheadVaultHandle(in runtime._csvScratchHandle, BufferID.Shinobu220BulkheadCsvScratch) ||
                    !vault.TryResolveHandle(in runtime._profilesHandle, out NativeArray<BulkheadProfileDTO> profiles) ||
                    !vault.TryResolveHandle(in runtime._csvScratchHandle, out NativeArray<byte> scratch) ||
                    !profiles.IsCreated ||
                    !scratch.IsCreated ||
                    profiles.Length == 0 ||
                    scratch.Length == 0)
                {
                    return false;
                }

                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length <= 0L || stream.Length > scratch.Length)
                    return false;

                int byteCount = (int)stream.Length;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                int totalRead = 0;
                while (totalRead < byteCount)
                {
                    Span<byte> destination = new Span<byte>(scratchPtr + totalRead, byteCount - totalRead);
                    int read = stream.Read(destination);
                    if (read <= 0)
                        break;
                    totalRead += read;
                }

                return totalRead == byteCount && ParseProfiles(new ReadOnlySpan<byte>(scratchPtr, totalRead), profiles) > 0;
            }
            catch (Exception ex) when (IsColdStorageException(ex))
            {
                return false;
            }
            finally
            {
                vault.ReleaseMutationGuard(BulkheadProfileImportMutationGuardMask);
            }
        }
#endif

        private void Awake()
        {
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, "Docs/AgentLogs/Dump_1306_Construction_Bulkhead.bin"));
            InitializeHatchLockColdPaths();
        }

        private void OnEnable()
        {
            _shutdownStarted = false;
            s_active = this;
            RequestDataVaultRebind(GlobalRegistry.DataVault);
            RegisterDispatcherPhases();
            TryRegisterHotSwapListener();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private void OnDisable()
        {
            ShutdownRuntime(forceCompletePendingJobs: true);
        }

        private void ShutdownRuntime(bool forceCompletePendingJobs)
        {
            if (_shutdownStarted && !_vaultRebindPending)
                return;

            _shutdownStarted = true;
            Application.quitting -= ShutdownActive;
            TryUnregisterHotSwapListener();
            UnregisterDispatcherPhases();
            if (forceCompletePendingJobs)
                DrainScheduledJobsForTeardown();
            RequestDataVaultRebind(null);
            _vaultInitialized = false;
            _defaultsInitialized = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private static void ShutdownActive()
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime != null)
                runtime.ShutdownRuntime(forceCompletePendingJobs: true);
        }

        private void RegisterDispatcherPhases()
        {
            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }
            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }
            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }
            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RequestDataVaultRebind(currentService is IDataVault currentVault ? currentVault : null);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RequestDataVaultRebind(currentService is IDataVault currentVault ? currentVault : null);
        }

        private void RequestDataVaultRebind(IDataVault currentVault)
        {
            if (!_vaultRebindPending && ReferenceEquals(_vault, currentVault))
            {
                BulkheadContainmentIntentBus.BindDataVault(_vault != null && BootstrapVaultState(_vault) ? _vault : null);
                return;
            }

            BulkheadContainmentIntentBus.BindDataVault(null);
            DisableShaderGlobals();
            ReleaseGraphicsBuffers();
            _pendingVault = currentVault;
            _vaultRebindPending = true;
            ResetVaultRuntimeState(clearScheduledFlags: false);
            TryFlushPendingDataVaultRebind();
        }

        private bool TryFlushPendingDataVaultRebind()
        {
            if (!_vaultRebindPending)
                return true;

            if (!TryFinalizeBulkheadJobsNoWait())
                return false;

            if (!_preSimulationHandle.IsCompleted || !_simulationHandle.IsCompleted)
                return false;

            IDataVault pendingVault = _pendingVault;
            _pendingVault = null;
            _vaultRebindPending = false;
            ReleaseVaultHandles();
            _vault = pendingVault;
            _preSimulationHandle = default;
            _simulationHandle = default;
            ResetVaultRuntimeState(clearScheduledFlags: true);
            BulkheadContainmentIntentBus.BindDataVault(_vault != null && BootstrapVaultState(_vault) ? _vault : null);
            return true;
        }

        private void DrainScheduledJobsForTeardown()
        {
            if (_preSimulationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _preSimulationHandle, forceComplete: true);
                _preSimulationScheduled = false;
            }

            if (_simulationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
                _simulationScheduled = false;
            }

            ReleaseBulkheadJobPins();
        }

        private void ResetVaultRuntimeState(bool clearScheduledFlags)
        {
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _mockGenerated = false;
            _activeCount = 0;
            _authorityAccumulator = 0f;
            _lastFrame = 0u;
            _lastTelemetryFrame = 0u;
            _lastTelemetryCollisionEdgeHash = 0u;
            _lastTelemetryAverageClosure = 0f;
            _lastTelemetryCollisionDepthMeters = 0f;
            _lastDumpedTelemetryCursor = 0u;
            _lastDumpAttemptTelemetryCursor = 0u;
            _layoutFaultTelemetryWritten = false;
            _playerKinematicStateHandle = default;
            _nextPlayerStateHandleBindFrame = 0u;
            ResetHatchLockRuntimeState();
            if (clearScheduledFlags)
            {
                _preSimulationScheduled = false;
                _simulationScheduled = false;
            }
            _shaderUploadDirty = true;
        }

        private IDataVault ResolveVault()
        {
            return _vaultRebindPending ? null : _vault;
        }

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !IsBulkheadVaultHandle(in handle, bufferId))
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer);
        }

        private bool Read<T>(in VaultGenerationHandle<T> handle, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !IsBulkheadVaultHandle(in handle, bufferId))
            {
                buffer = default;
                return false;
            }

            return vault.TryReadHandle(in handle, out buffer);
        }

        private bool EnsureLayoutValid(IDataVault vault)
        {
            if (!_layoutChecked)
            {
                _layoutValid = BulkheadStateLayoutGuard.ValidateLayout();
                _layoutChecked = true;
            }

            if (_layoutValid)
            {
                _layoutFaultTelemetryWritten = false;
                return true;
            }

            RecordLayoutFaultTelemetry(vault);
            return false;
        }

        private void RecordLayoutFaultTelemetry(IDataVault vault)
        {
            if (_layoutFaultTelemetryWritten ||
                vault == null ||
                !IsBulkheadVaultHandle(in _telemetryHandle, BufferID.Shinobu220BulkheadTelemetryRing) ||
                !IsBulkheadVaultHandle(in _telemetryCursorHandle, BufferID.Shinobu220BulkheadTelemetryCursor) ||
                !vault.TryAcquireMutationGuard(BulkheadTelemetryMutationGuardMask))
            {
                return;
            }

            try
            {
                if (!vault.TryResolveHandle(in _telemetryHandle, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                    !vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<uint> cursor) ||
                    !telemetry.IsCreated ||
                    !cursor.IsCreated ||
                    telemetry.Length == 0 ||
                    cursor.Length == 0)
                {
                    return;
                }

                uint writeCursor = cursor[0];
                int telemetryIndex = (int)(writeCursor % (uint)telemetry.Length);
                telemetry[telemetryIndex] = new BulkheadTelemetryEntry
                {
                    Frame = _lastFrame,
                    ActiveCount = 0u,
                    SealedCount = 0u,
                    JammedCount = 0u,
                    AverageClosure = 0f,
                    AuthorityCadenceHz = 0f,
                    GlobalQualityWeight = AuthoritativeQualityWeight,
                    LastScheduleMicroseconds = 0f,
                    StateHash = 0x4C41594Fu,
                    CollisionEdgeHash = 0u,
                    CollisionDepthMeters = 0f,
                    Flags = BulkheadTelemetryFlags.NonFinite |
                            BulkheadTelemetryFlags.DumpRequested |
                            BulkheadTelemetryFlags.ScheduleTimeOnly
                };
                cursor[0] = unchecked(writeCursor + 1u);
                _layoutFaultTelemetryWritten = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BulkheadTelemetryMutationGuardMask);
            }
        }

        private static bool TryAcquireWriteLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsBulkheadVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            vault.ReleaseWriteLock(in handle, OwnerSystemId);
            buffer = default;
            return false;
        }

        private bool TryFinalizeBulkheadJobsNoWait()
        {
            bool preDone = true;
            bool simulationDone = true;
            if (_preSimulationScheduled)
            {
                preDone = DispatcherJobFence.TryFinalizeCompleted(ref _preSimulationHandle);
                if (preDone)
                    _preSimulationScheduled = false;
            }

            if (_simulationScheduled)
            {
                simulationDone = DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle);
                if (simulationDone)
                    _simulationScheduled = false;
            }

            if (preDone && simulationDone)
                ReleaseBulkheadJobPins();

            return preDone && simulationDone;
        }

        private bool TryEnsureBulkheadJobPins(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (_bulkheadJobPinVault != null)
                return ReferenceEquals(_bulkheadJobPinVault, vault);

            if (!vault.TryAcquireMutationGuard(BulkheadJobMutationGuardMask))
                return false;

            _bulkheadJobPinVault = vault;
            _bulkheadJobPinMask = BulkheadRequiredJobPinMask;
            return true;
        }

        private bool TryLockOptionalBulkheadJobPin(BufferID bufferId, uint bit)
        {
            IDataVault vault = _bulkheadJobPinVault;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if ((_bulkheadJobPinMask & bit) != 0u)
                return true;

            _bulkheadJobPinMask |= bit;
            return true;
        }

        private void ReleaseOptionalBulkheadJobPin(BufferID bufferId, uint bit)
        {
            IDataVault vault = _bulkheadJobPinVault;
            if (vault == null || (_bulkheadJobPinMask & bit) == 0u)
                return;

            _bulkheadJobPinMask &= ~bit;
        }

        private void ReleaseBulkheadJobPins()
        {
            IDataVault vault = _bulkheadJobPinVault;
            uint mask = _bulkheadJobPinMask;
            _bulkheadJobPinVault = null;
            _bulkheadJobPinMask = 0u;
            ReleaseBulkheadJobPins(vault, mask);
        }

        private static void ReleaseBulkheadJobPins(IDataVault vault, uint mask)
        {
            if (vault == null || mask == 0u)
                return;

            vault.ReleaseMutationGuard(BulkheadJobMutationGuardMask);
        }

        private void ReleaseVaultHandles()
        {
            ReleaseBulkheadJobPins();
            IDataVault vault = _vault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _statesHandle, BufferID.Shinobu220BulkheadStates);
                ReleaseVaultHandle(vault, ref _aupsHandle, BufferID.Shinobu220BulkheadAups);
                ReleaseVaultHandle(vault, ref _planesHandle, BufferID.Shinobu220BulkheadPlanes);
                ReleaseVaultHandle(vault, ref _csrEdgesHandle, BufferID.Shinobu220BulkheadCsrEdges);
                ReleaseVaultHandle(vault, ref _edgeConductivityHandle, BufferID.Shinobu220BulkheadEdgeConductivity);
                ReleaseVaultHandle(vault, ref _fluidFlowHandle, BufferID.Shinobu220BulkheadFluidFlow);
                ReleaseVaultHandle(vault, ref _moduleIntegrityHandle, BufferID.Shinobu220BulkheadModuleIntegrity);
                ReleaseVaultHandle(vault, ref _tuningHandle, BufferID.Shinobu220BulkheadTuning);
                ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.Shinobu220BulkheadTelemetryRing);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.Shinobu220BulkheadTelemetryCursor);
                ReleaseVaultHandle(vault, ref _collisionResultsHandle, BufferID.Shinobu220BulkheadCollisionResults);
                ReleaseVaultHandle(vault, ref _profilesHandle, BufferID.Shinobu220BulkheadProfiles);
                ReleaseVaultHandle(vault, ref _csvScratchHandle, BufferID.Shinobu220BulkheadCsvScratch);
                ReleaseVaultHandle(vault, ref _shaderUploadHandle, BufferID.Shinobu220BulkheadShaderUpload);
                ReleaseVaultHandle(vault, ref _intentRingHandle, BufferID.Shinobu220BulkheadIntentRing);
                ReleaseVaultHandle(vault, ref _intentControlHandle, BufferID.Shinobu220BulkheadIntentControl);
                ReleaseHatchLockVaultHandles(vault);
            }
            else
            {
                ReleaseHatchLockVaultHandles(null);
            }

            _statesHandle = default;
            _aupsHandle = default;
            _planesHandle = default;
            _csrEdgesHandle = default;
            _edgeConductivityHandle = default;
            _fluidFlowHandle = default;
            _moduleIntegrityHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _collisionResultsHandle = default;
            _profilesHandle = default;
            _csvScratchHandle = default;
            _shaderUploadHandle = default;
            _intentRingHandle = default;
            _intentControlHandle = default;
            _playerKinematicStateHandle = default;
            _nextPlayerStateHandleBindFrame = 0u;
            _vault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsBulkheadVaultHandle(in handle, bufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private static bool IsBulkheadVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool IsVaultHandleForBuffer<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }

        private bool BootstrapVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!EnsureLayoutValid(vault))
                return false;

            int capacity = math.clamp(bulkheadCapacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
            if (!_vaultInitialized)
            {
                _statesHandle = vault.EnsureGenerationHandle<BulkheadStateDTO>(BufferID.Shinobu220BulkheadStates, capacity, OwnerSystemId);
                _aupsHandle = vault.EnsureGenerationHandle<double3>(BufferID.Shinobu220BulkheadAups, capacity, OwnerSystemId);
                _planesHandle = vault.EnsureGenerationHandle<BulkheadPlaneDTO>(BufferID.Shinobu220BulkheadPlanes, capacity, OwnerSystemId);
                _csrEdgesHandle = vault.EnsureGenerationHandle<BulkheadCsrEdgeDTO>(BufferID.Shinobu220BulkheadCsrEdges, capacity, OwnerSystemId);
                _edgeConductivityHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu220BulkheadEdgeConductivity, capacity, OwnerSystemId);
                _fluidFlowHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu220BulkheadFluidFlow, capacity, OwnerSystemId);
                _moduleIntegrityHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu220BulkheadModuleIntegrity, capacity, OwnerSystemId);
                _tuningHandle = vault.EnsureGenerationHandle<BulkheadTuningDTO>(BufferID.Shinobu220BulkheadTuning, 1, OwnerSystemId);
                _telemetryHandle = vault.EnsureGenerationHandle<BulkheadTelemetryEntry>(BufferID.Shinobu220BulkheadTelemetryRing, BulkheadContainmentConstants.TelemetryFrameCount, OwnerSystemId);
                _telemetryCursorHandle = vault.EnsureGenerationHandle<uint>(BufferID.Shinobu220BulkheadTelemetryCursor, 1, OwnerSystemId);
                _collisionResultsHandle = vault.EnsureGenerationHandle<BulkheadCollisionResultDTO>(BufferID.Shinobu220BulkheadCollisionResults, 1, OwnerSystemId);
                _profilesHandle = vault.EnsureGenerationHandle<BulkheadProfileDTO>(BufferID.Shinobu220BulkheadProfiles, BulkheadContainmentConstants.ProfileCapacity, OwnerSystemId);
                _csvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.Shinobu220BulkheadCsvScratch, 8192, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                _shaderUploadHandle = vault.EnsureGenerationHandle<BulkheadStateDTO>(BufferID.Shinobu220BulkheadShaderUpload, BulkheadContainmentConstants.ShaderUploadCapacity, OwnerSystemId);
                _intentRingHandle = vault.EnsureGenerationHandle<BulkheadContainmentIntentDTO>(BufferID.Shinobu220BulkheadIntentRing, BulkheadContainmentIntentBus.IntentCapacity, OwnerSystemId);
                _intentControlHandle = vault.EnsureGenerationHandle<BulkheadContainmentIntentControlDTO>(BufferID.Shinobu220BulkheadIntentControl, 1, OwnerSystemId);
                if (!EnsureHatchLockVaultState(vault, capacity))
                    return false;
                if (uploadHatchShaderBuffer && !EnsureHatchGraphicsBuffers())
                    uploadHatchShaderBuffer = false;
                _vaultInitialized = true;
            }

            TryBindPlayerKinematicStateHandle(vault);
            return RefreshVaultState(vault);
        }

        private bool RefreshVaultState(IDataVault vault)
        {
            return RefreshVaultState(vault, refreshHatchLocks: true);
        }

        private bool RefreshVaultState(IDataVault vault, bool refreshHatchLocks)
        {
            if (vault == null || !_vaultInitialized)
                return false;

            if (!EnsureLayoutValid(vault))
                return false;

            int capacity = math.clamp(bulkheadCapacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
            if (!Read(in _statesHandle, BufferID.Shinobu220BulkheadStates, out NativeArray<BulkheadStateDTO> states) ||
                !Read(in _aupsHandle, BufferID.Shinobu220BulkheadAups, out NativeArray<double3> aups) ||
                !Read(in _planesHandle, BufferID.Shinobu220BulkheadPlanes, out NativeArray<BulkheadPlaneDTO> planes) ||
                !Read(in _csrEdgesHandle, BufferID.Shinobu220BulkheadCsrEdges, out NativeArray<BulkheadCsrEdgeDTO> csrEdges))
            {
                return false;
            }

            if (!states.IsCreated || !aups.IsCreated || !planes.IsCreated || !csrEdges.IsCreated ||
                states.Length <= 0 || aups.Length <= 0 || planes.Length <= 0 || csrEdges.Length <= 0 ||
                !vault.TryAcquireMutationGuard(BulkheadRefreshMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!Resolve(in _edgeConductivityHandle, BufferID.Shinobu220BulkheadEdgeConductivity, out NativeArray<float> conductivity) ||
                    !Resolve(in _fluidFlowHandle, BufferID.Shinobu220BulkheadFluidFlow, out NativeArray<float> fluidFlow) ||
                    !Resolve(in _moduleIntegrityHandle, BufferID.Shinobu220BulkheadModuleIntegrity, out NativeArray<float> moduleIntegrity) ||
                    !Resolve(in _tuningHandle, BufferID.Shinobu220BulkheadTuning, out NativeArray<BulkheadTuningDTO> tuning) ||
                    !conductivity.IsCreated ||
                    !fluidFlow.IsCreated ||
                    !moduleIntegrity.IsCreated ||
                    !tuning.IsCreated ||
                    conductivity.Length == 0 ||
                    fluidFlow.Length == 0 ||
                    moduleIntegrity.Length == 0 ||
                    tuning.Length == 0)
                {
                    return false;
                }

                if (!_defaultsInitialized)
                {
                    int scalarCount = math.min(conductivity.Length, math.min(fluidFlow.Length, moduleIntegrity.Length));
                    for (int i = 0; i < scalarCount; i++)
                    {
                        conductivity[i] = 1f;
                        fluidFlow[i] = 1f;
                        moduleIntegrity[i] = 1f;
                    }

                    _defaultsInitialized = true;
                }

                float q = ResolveBulkheadQualityWeight();
                WriteTuningRow(tuning, capacity, q);
            }
            finally
            {
                vault.ReleaseMutationGuard(BulkheadRefreshMutationGuardMask);
            }

            return !refreshHatchLocks || RefreshHatchLockVaultState(vault, capacity, allowDefaultProfileLoad: false);
        }

        private bool TryWriteTuningRow()
        {
            IDataVault vault = ResolveVault();
            if (!TryAcquireWriteLane(vault, in _tuningHandle, BufferID.Shinobu220BulkheadTuning, 1, out NativeArray<BulkheadTuningDTO> tuning))
            {
                return false;
            }

            try
            {
                int capacity = math.clamp(bulkheadCapacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
                float q = ResolveBulkheadQualityWeight();
                WriteTuningRow(tuning, capacity, q);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, OwnerSystemId);
            }
        }

        private void WriteTuningRow(NativeArray<BulkheadTuningDTO> tuning, int capacity, float quality)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            float q = BulkheadContainmentMath.Sanitize01(quality, 0f);
            tuning[0] = new BulkheadTuningDTO
            {
                CloseSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(closeSpeedPerSecond, 2f)),
                OpenSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(openSpeedPerSecond, 2.5f)),
                OverrideDistanceMeters = math.max(0.5f, BulkheadContainmentMath.SanitizePositive(overrideDistanceMeters, 3f)),
                CatastrophicIntegrity01 = BulkheadContainmentMath.Sanitize01(catastrophicIntegrity01, 0.35f),
                GlobalQualityWeight = q,
                AuthorityCadenceHz = ResolveAuthorityCadenceHz(),
                ActiveCount = (uint)math.clamp(_activeCount, 0, capacity),
                Flags = uploadShaderBuffer ? 1u : 0u
            };
        }

        private JobHandle ScheduleMockDataIfRequired(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            JobHandle dependency)
        {
            if ((!generateMockBulkheads && !generateMockHatchPressure) ||
                _mockGenerated ||
                _activeCount > 0 ||
                !states.IsCreated ||
                !aups.IsCreated ||
                !planes.IsCreated ||
                !csrEdges.IsCreated)
            {
                return dependency;
            }

            int count = math.min(8, math.min(states.Length, math.min(aups.Length, math.min(planes.Length, csrEdges.Length))));
            if (count <= 0)
                return dependency;

            GenerateMockBulkheadsJob job = new GenerateMockBulkheadsJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                Aups = (double3*)NativeArrayUnsafeUtility.GetUnsafePtr(aups),
                Planes = (BulkheadPlaneDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(planes),
                CsrEdges = (BulkheadCsrEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(csrEdges),
                Count = count,
                OriginAup = double3.zero,
                Seed = MockSeed
            };
            JobHandle handle = job.Schedule(count, 32, dependency);
            TrackScheduledSimulationJob(handle);
            _activeCount = math.max(_activeCount, count);
            _mockGenerated = true;
            return handle;
        }

        private static int CreatedLength<T>(NativeArray<T> buffer) where T : struct
        {
            return buffer.IsCreated && buffer.Length > 0 ? buffer.Length : 0;
        }

        private static int ResolveBulkheadWritableCount(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            NativeArray<float> moduleIntegrity)
        {
            int count = math.min(CreatedLength(states), CreatedLength(aups));
            count = math.min(count, CreatedLength(planes));
            count = math.min(count, CreatedLength(csrEdges));
            return math.min(count, CreatedLength(moduleIntegrity));
        }

        private static int ResolveCollisionLaneCount(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes)
        {
            return math.min(CreatedLength(states), math.min(CreatedLength(aups), CreatedLength(planes)));
        }

        private static int ResolveSimulationMutationCount(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            NativeArray<float> conductivity,
            NativeArray<float> fluidFlow,
            NativeArray<float> moduleIntegrity)
        {
            int count = math.min(CreatedLength(states), CreatedLength(csrEdges));
            count = math.min(count, CreatedLength(conductivity));
            count = math.min(count, CreatedLength(fluidFlow));
            return math.min(count, CreatedLength(moduleIntegrity));
        }

        private bool ApplyAirlockBulkheadStateIntent(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            NativeArray<float> moduleIntegrity,
            int writableCount,
            uint edgeHash,
            bool locked,
            double3 center,
            float3 normal,
            float widthMeters,
            float heightMeters,
            float parentIntegrity01,
            uint siblingNodeHash)
        {
            if (edgeHash == 0u ||
                !math.all(math.isfinite(center)) ||
                !math.all(math.isfinite(normal)) ||
                !math.isfinite(widthMeters) ||
                !math.isfinite(heightMeters) ||
                !math.isfinite(parentIntegrity01))
            {
                return false;
            }

            int slot = FindOrAllocateSlot(states, edgeHash, writableCount);
            if (slot < 0)
                return false;

            BulkheadStateDTO state = states[slot];
            state.EdgeHashID = edgeHash;
            state.AssociatedLock = locked ? 1u : 0u;
            state.SiblingNodeHash = siblingNodeHash;
            state.Flags |= BulkheadStateFlags.Active;
            states[slot] = state;
            aups[slot] = center;
            if ((uint)slot < (uint)moduleIntegrity.Length)
                moduleIntegrity[slot] = BulkheadContainmentMath.Sanitize01(parentIntegrity01, 1f);
            planes[slot] = new BulkheadPlaneDTO
            {
                CenterAup = center,
                Normal = BulkheadContainmentMath.SafeNormal(normal, new float3(0f, 0f, 1f)),
                WidthMeters = BulkheadContainmentMath.SanitizePositive(widthMeters, 2.6f),
                HeightMeters = BulkheadContainmentMath.SanitizePositive(heightMeters, 3.2f),
                HalfThicknessMeters = 0.18f,
                EdgeHashID = edgeHash,
                Flags = BulkheadStateFlags.Active,
                IntegrityIndex = (uint)slot,
                Reserved = 0u
            };
            csrEdges[slot] = new BulkheadCsrEdgeDTO
            {
                EdgeHashID = edgeHash,
                ConductivityIndex = slot,
                FluidFlowIndex = slot,
                OpenConductivity = 1f,
                OpenFluidFlow = 1f,
                IntegrityIndex = slot,
                Flags = BulkheadStateFlags.Active
            };
            _activeCount = math.max(_activeCount, slot + 1);
            return true;
        }

        private void ConsumePublishedIntents(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            NativeArray<float> moduleIntegrity,
            uint currentFrame)
        {
            int writableCount = ResolveBulkheadWritableCount(states, aups, planes, csrEdges, moduleIntegrity);
            if (writableCount <= 0)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null || !vault.TryAcquireMutationGuard(BulkheadContainmentIntentBus.IntentMutationGuardMask))
                return;

            try
            {
                if (!IsBulkheadVaultHandle(in _intentRingHandle, BufferID.Shinobu220BulkheadIntentRing) ||
                    !IsBulkheadVaultHandle(in _intentControlHandle, BufferID.Shinobu220BulkheadIntentControl) ||
                    !vault.TryResolveHandle(in _intentRingHandle, out NativeArray<BulkheadContainmentIntentDTO> intents) ||
                    !vault.TryResolveHandle(in _intentControlHandle, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows) ||
                    !intents.IsCreated ||
                    !controlRows.IsCreated ||
                    intents.Length == 0 ||
                    controlRows.Length == 0)
                {
                    return;
                }

                BulkheadContainmentIntentControlDTO control = controlRows[0];
                uint write = control.WriteCursor;
                uint read = control.ReadCursor;
                if (write == read)
                    return;

                uint capacity = control.Capacity == 0u
                    ? (uint)math.min(BulkheadContainmentIntentBus.IntentCapacity, intents.Length)
                    : math.min(control.Capacity, (uint)intents.Length);
                if (capacity == 0u)
                    return;

                uint pending = math.min(write - read, capacity);
                for (uint offset = 0u; offset < pending; offset++)
                {
                    BulkheadContainmentIntentDTO intent = intents[(int)((read + offset) % capacity)];
                    if ((intent.Flags & BulkheadContainmentIntentFlags.Valid) == 0u ||
                        (intent.Flags & BulkheadContainmentIntentFlags.NonFinite) != 0u ||
                        !IsIntentFrameAccepted(intent.Frame, currentFrame))
                    {
                        continue;
                    }

                    ApplyAirlockBulkheadStateIntent(
                        states,
                        aups,
                        planes,
                        csrEdges,
                        moduleIntegrity,
                        writableCount,
                        intent.EdgeHashID,
                        (intent.Flags & BulkheadContainmentIntentFlags.Locked) != 0u,
                        intent.CenterAup,
                        intent.Normal,
                        intent.WidthMeters,
                        intent.HeightMeters,
                        intent.ParentIntegrity01,
                        intent.SiblingNodeHash);
                }

                control.ReadCursor = write;
                controlRows[0] = control;
            }
            finally
            {
                vault.ReleaseMutationGuard(BulkheadContainmentIntentBus.IntentMutationGuardMask);
            }
        }

        private static bool IsIntentFrameAccepted(uint intentFrame, uint currentFrame)
        {
            if (intentFrame == 0u || currentFrame == 0u)
                return true;

            if (intentFrame > currentFrame)
                return false;

            return currentFrame - intentFrame <= BulkheadContainmentConstants.MaxIntentAgeFrames;
        }

        private static bool ContainsBulkheadOverrideSignal(
            NativeArray<InteractionUiSignal>.ReadOnly signals,
            int signalCount)
        {
            if (!signals.IsCreated || signalCount <= 0)
                return false;

            int count = math.min(signalCount, signals.Length);
            for (int i = 0; i < count; i++)
            {
                InteractionUiSignal signal = signals[i];
                if (signal.State != 0 &&
                    signal.ToolHash == BulkheadContainmentConstants.OverrideToolHash)
                {
                    return true;
                }
            }

            return false;
        }

        private int FindOrAllocateSlot(NativeArray<BulkheadStateDTO> states, uint edgeHash, int slotLimit)
        {
            int count = math.min(states.Length, math.max(0, slotLimit));
            int firstFree = -1;
            for (int i = 0; i < count; i++)
            {
                BulkheadStateDTO state = states[i];
                if (state.EdgeHashID == edgeHash)
                    return i;
                if (firstFree < 0 && state.EdgeHashID == 0u)
                    firstFree = i;
            }

            return firstFree;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!TryFinalizeBulkheadJobsNoWait())
                return;

            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
            {
                _preSimulationScheduled = false;
                return;
            }

            _preSimulationHandle = default;
            _preSimulationScheduled = false;

            IDataVault vault = ResolveVault();
            if (vault == null || !RefreshVaultState(vault))
                return;

            if (!TryEnsureBulkheadJobPins(vault))
                return;

            bool keepPins = false;
            try
            {
                if (!Resolve(in _statesHandle, BufferID.Shinobu220BulkheadStates, out NativeArray<BulkheadStateDTO> states) ||
                    !Resolve(in _aupsHandle, BufferID.Shinobu220BulkheadAups, out NativeArray<double3> aups) ||
                    !Resolve(in _planesHandle, BufferID.Shinobu220BulkheadPlanes, out NativeArray<BulkheadPlaneDTO> planes) ||
                    !Resolve(in _csrEdgesHandle, BufferID.Shinobu220BulkheadCsrEdges, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                    !Resolve(in _moduleIntegrityHandle, BufferID.Shinobu220BulkheadModuleIntegrity, out NativeArray<float> moduleIntegrity) ||
                    !Resolve(in _collisionResultsHandle, BufferID.Shinobu220BulkheadCollisionResults, out NativeArray<BulkheadCollisionResultDTO> collisions))
                {
                    return;
                }
                if (!states.IsCreated || !aups.IsCreated || !planes.IsCreated || !csrEdges.IsCreated ||
                    !moduleIntegrity.IsCreated || !collisions.IsCreated)
                    return;
                if (collisions.Length <= 0)
                    return;

                ConsumePublishedIntents(states, aups, planes, csrEdges, moduleIntegrity, timing.FrameId);
                int count = math.clamp(_activeCount, 0, ResolveCollisionLaneCount(states, aups, planes));
                if (count <= 0)
                {
                    collisions[0] = new BulkheadCollisionResultDTO
                    {
                        Frame = timing.FrameId
                    };
                    return;
                }

                if (!TryAcquirePlayerState(vault, timing.FrameId, out double3 playerAup, out float3 velocity))
                {
                    collisions[0] = new BulkheadCollisionResultDTO
                    {
                        Frame = timing.FrameId
                    };
                    return;
                }

                float simulationTickDelta = ResolveSimulationTickDelta(in timing);
                double3 predictedAup = playerAup + (double3)(velocity * simulationTickDelta);
                JobHandle dependency = _simulationScheduled ? _simulationHandle : default;

                NativeArray<InteractionUiSignal>.ReadOnly signals = SignalBus<InteractionUiSignal>.GetFrameSnapshotArray();
                int signalCount = signals.IsCreated ? signals.Length : 0;
                if (ContainsBulkheadOverrideSignal(signals, signalCount))
                {
                    ProcessDoorOverrideJob overrideJob = new ProcessDoorOverrideJob
                    {
                        Signals = signals,
                        States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                        Aups = (double3*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(aups),
                        SignalCount = signalCount,
                        StateCount = count,
                        PlayerAup = playerAup,
                        OverrideDistanceMeters = overrideDistanceMeters
                    };
                    dependency = overrideJob.Schedule(dependency);
                    TrackScheduledPreSimulationJob(dependency);
                    keepPins = true;
                }

                EvaluateDoorCollisionsJob collisionJob = new EvaluateDoorCollisionsJob
                {
                    States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                    Planes = (BulkheadPlaneDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(planes),
                    Result = (BulkheadCollisionResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(collisions),
                    Count = count,
                    PlayerStartAup = playerAup,
                    PlayerEndAup = predictedAup,
                    PlayerRadiusMeters = DefaultPlayerRadiusMeters,
                    Frame = timing.FrameId
                };
                JobHandle preSimulationHandle = collisionJob.Schedule(dependency);
                TrackScheduledPreSimulationJob(preSimulationHandle);
                keepPins = true;
            }
            finally
            {
                if (!keepPins && !_simulationScheduled && !_preSimulationScheduled)
                    ReleaseBulkheadJobPins();
            }
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            JobHandle dependency = _preSimulationScheduled ? JobHandle.CombineDependencies(dependsOn, _preSimulationHandle) : dependsOn;
            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
                return dependency;

            IDataVault vault = ResolveVault();
            bool pinsAlreadyHeld = _bulkheadJobPinVault != null;
            if (vault == null || (!pinsAlreadyHeld && !RefreshVaultState(vault)))
                return dependency;
            if (!TryEnsureBulkheadJobPins(vault))
                return dependency;

            try
            {
            float q = ResolveBulkheadQualityWeight();
            float cadenceHz = ResolveAuthorityCadenceHz();
            float dt = ResolveSimulationTickDelta(in timing);
            _lastFrame = context.Frame;
            float accumulated = BulkheadContainmentMath.SanitizeNonNegative(_authorityAccumulator, 0f) + dt;
            _authorityAccumulator = math.isfinite(accumulated) && accumulated < SimulationAuthorityDeltaCeilingSeconds
                ? accumulated
                : SimulationAuthorityDeltaCeilingSeconds;
            float safeCadenceHz = BulkheadContainmentMath.SanitizePositive(cadenceHz, 5f);
            float period = 1f / (safeCadenceHz < 1f ? 1f : safeCadenceHz);

            if (!Resolve(in _statesHandle, BufferID.Shinobu220BulkheadStates, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _collisionResultsHandle, BufferID.Shinobu220BulkheadCollisionResults, out NativeArray<BulkheadCollisionResultDTO> collisions) ||
                !Resolve(in _telemetryHandle, BufferID.Shinobu220BulkheadTelemetryRing, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                !Resolve(in _telemetryCursorHandle, BufferID.Shinobu220BulkheadTelemetryCursor, out NativeArray<uint> cursor))
            {
                return FailSimulationSchedule(dependency);
            }
            if (!states.IsCreated ||
                !collisions.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                states.Length <= 0 ||
                collisions.Length <= 0 ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                return FailSimulationSchedule(dependency);
            }

            if (generateMockBulkheads && !_mockGenerated && _activeCount <= 0)
            {
                if (!Resolve(in _aupsHandle, BufferID.Shinobu220BulkheadAups, out NativeArray<double3> mockAups) ||
                    !Resolve(in _planesHandle, BufferID.Shinobu220BulkheadPlanes, out NativeArray<BulkheadPlaneDTO> planes) ||
                    !Resolve(in _csrEdgesHandle, BufferID.Shinobu220BulkheadCsrEdges, out NativeArray<BulkheadCsrEdgeDTO> mockCsrEdges) ||
                    !mockAups.IsCreated ||
                    !planes.IsCreated ||
                    !mockCsrEdges.IsCreated ||
                    mockAups.Length <= 0 ||
                    planes.Length <= 0 ||
                    mockCsrEdges.Length <= 0)
                {
                    return FailSimulationSchedule(dependency);
                }

                dependency = ScheduleMockDataIfRequired(states, mockAups, planes, mockCsrEdges, dependency);
            }

            int count = math.clamp(_activeCount, 0, CreatedLength(states));
            if (count <= 0)
            {
                if (_preSimulationScheduled)
                {
                    long telemetryStart = Stopwatch.GetTimestamp();
                    return ScheduleTelemetryJob(
                        dependency,
                        states,
                        collisions,
                        telemetry,
                        cursor,
                        0,
                        context.Frame,
                        q,
                        cadenceHz,
                        telemetryStart,
                        BulkheadTelemetryFlags.ScheduleTimeOnly);
                }

                RecordEmptyTelemetryFrame(telemetry, cursor, collisions, context.Frame, q, cadenceHz);
                return FailSimulationSchedule(dependency);
            }

            if (_authorityAccumulator < period)
            {
                long telemetryStart = Stopwatch.GetTimestamp();
                return ScheduleTelemetryJob(
                    dependency,
                    states,
                    collisions,
                    telemetry,
                    cursor,
                    count,
                    context.Frame,
                    q,
                    cadenceHz,
                    telemetryStart,
                    BulkheadTelemetryFlags.ScheduleTimeOnly);
            }

            if (!Resolve(in _csrEdgesHandle, BufferID.Shinobu220BulkheadCsrEdges, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                !Resolve(in _aupsHandle, BufferID.Shinobu220BulkheadAups, out NativeArray<double3> aups) ||
                !Resolve(in _edgeConductivityHandle, BufferID.Shinobu220BulkheadEdgeConductivity, out NativeArray<float> conductivity) ||
                !Resolve(in _fluidFlowHandle, BufferID.Shinobu220BulkheadFluidFlow, out NativeArray<float> fluidFlow) ||
                !Resolve(in _moduleIntegrityHandle, BufferID.Shinobu220BulkheadModuleIntegrity, out NativeArray<float> moduleIntegrity))
            {
                long telemetryStart = Stopwatch.GetTimestamp();
                return ScheduleTelemetryJob(
                    dependency,
                    states,
                    collisions,
                    telemetry,
                    cursor,
                    count,
                    context.Frame,
                    q,
                    cadenceHz,
                    telemetryStart,
                    BulkheadTelemetryFlags.ScheduleTimeOnly);
            }
            if (!csrEdges.IsCreated || !aups.IsCreated || !conductivity.IsCreated || !fluidFlow.IsCreated || !moduleIntegrity.IsCreated)
            {
                long telemetryStart = Stopwatch.GetTimestamp();
                return ScheduleTelemetryJob(
                    dependency,
                    states,
                    collisions,
                    telemetry,
                    cursor,
                    count,
                    context.Frame,
                    q,
                    cadenceHz,
                    telemetryStart,
                    BulkheadTelemetryFlags.ScheduleTimeOnly);
            }

            count = math.min(count, ResolveSimulationMutationCount(states, csrEdges, conductivity, fluidFlow, moduleIntegrity));
            count = math.min(count, CreatedLength(aups));
            if (count <= 0)
            {
                long telemetryStart = Stopwatch.GetTimestamp();
                return ScheduleTelemetryJob(
                    dependency,
                    states,
                    collisions,
                    telemetry,
                    cursor,
                    0,
                    context.Frame,
                    q,
                    cadenceHz,
                    telemetryStart,
                    BulkheadTelemetryFlags.ScheduleTimeOnly);
            }

            float authorityDelta = _authorityAccumulator < SimulationAuthorityDeltaCeilingSeconds
                ? _authorityAccumulator
                : SimulationAuthorityDeltaCeilingSeconds;
            _authorityAccumulator = 0f;

            long start = Stopwatch.GetTimestamp();
            JobHandle handle = ScheduleHatchLockPipeline(
                vault,
                states,
                aups,
                moduleIntegrity,
                count,
                context.Frame,
                authorityDelta,
                q,
                dependency);

            UpdateBulkheadClosureJob updateJob = new UpdateBulkheadClosureJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                Count = count,
                DeltaSeconds = authorityDelta,
                CloseSpeedPerSecond = closeSpeedPerSecond,
                OpenSpeedPerSecond = openSpeedPerSecond
            };
            handle = updateJob.Schedule(count, 32, handle);
            TrackScheduledSimulationJob(handle);

            ApplyCatastrophicDoorDamageJob damageJob = new ApplyCatastrophicDoorDamageJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                CsrEdges = (BulkheadCsrEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csrEdges),
                ParentModuleIntegrity01 = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(moduleIntegrity),
                Count = count,
                IntegrityCount = moduleIntegrity.Length,
                CatastrophicIntegrity01 = catastrophicIntegrity01
            };
            handle = damageJob.Schedule(count, 32, handle);
            TrackScheduledSimulationJob(handle);

            ApplyBulkheadLockJob lockJob = new ApplyBulkheadLockJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                CsrEdges = (BulkheadCsrEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csrEdges),
                EdgeConductivity = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(conductivity),
                EdgeFluidFlow = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidFlow),
                Count = count,
                ConductivityCount = conductivity.Length,
                FluidFlowCount = fluidFlow.Length
            };
            handle = lockJob.Schedule(count, 32, handle);
            TrackScheduledSimulationJob(handle);

            return ScheduleTelemetryJob(
                handle,
                states,
                collisions,
                telemetry,
                cursor,
                count,
                context.Frame,
                q,
                cadenceHz,
                start,
                BulkheadTelemetryFlags.ScheduleTimeOnly);
            }
            finally
            {
                if (!_preSimulationScheduled && !_simulationScheduled)
                    ReleaseBulkheadJobPins();
            }
        }

        private JobHandle FailSimulationSchedule(JobHandle dependency)
        {
            if (!_preSimulationScheduled && !_simulationScheduled)
                ReleaseBulkheadJobPins();
            return dependency;
        }

        private void TrackScheduledSimulationJob(JobHandle handle)
        {
            _simulationHandle = handle;
            _simulationScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Construction, handle);
        }

        private void TrackScheduledPreSimulationJob(JobHandle handle)
        {
            _preSimulationHandle = handle;
            _preSimulationScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Construction, handle);
        }

        private JobHandle ScheduleTelemetryJob(
            JobHandle dependency,
            NativeArray<BulkheadStateDTO> states,
            NativeArray<BulkheadCollisionResultDTO> collisions,
            NativeArray<BulkheadTelemetryEntry> telemetry,
            NativeArray<uint> cursor,
            int count,
            uint frame,
            float q,
            float cadenceHz,
            long scheduleStart,
            uint flags)
        {
            if (!states.IsCreated ||
                !collisions.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                states.Length <= 0 ||
                collisions.Length <= 0 ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                return dependency;
            }

            RecordBulkheadTelemetryJob telemetryJob = new RecordBulkheadTelemetryJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                CollisionResult = (BulkheadCollisionResultDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(collisions),
                Telemetry = (BulkheadTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                Cursor = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(cursor),
                Count = count,
                TelemetryCount = telemetry.Length,
                Frame = frame,
                GlobalQualityWeight = q,
                AuthorityCadenceHz = cadenceHz,
                LastScheduleMicroseconds = BulkheadContainmentMath.SanitizePositive(_lastScheduleMicroseconds, 0f),
                Flags = flags
            };
            JobHandle handle = telemetryJob.Schedule(dependency);
            _lastScheduleMicroseconds = ElapsedMicroseconds(scheduleStart);
            TrackScheduledSimulationJob(handle);
            return handle;
        }

        private void RecordEmptyTelemetryFrame(
            NativeArray<BulkheadTelemetryEntry> telemetry,
            NativeArray<uint> cursor,
            NativeArray<BulkheadCollisionResultDTO> collisions,
            uint frame,
            float q,
            float cadenceHz)
        {
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0)
                return;

            int telemetryIndex = (int)(cursor[0] % (uint)telemetry.Length);
            BulkheadCollisionResultDTO collision = collisions.IsCreated && collisions.Length > 0 ? collisions[0] : default;
            uint flags = BulkheadTelemetryFlags.ScheduleTimeOnly;
            if ((collision.Flags & BulkheadCollisionFlags.NonFinite) != 0u ||
                !math.isfinite(collision.DepthMeters) ||
                !math.all(math.isfinite(collision.Normal)))
            {
                flags |= BulkheadTelemetryFlags.NonFinite | BulkheadTelemetryFlags.DumpRequested;
            }

            float collisionDepth = BulkheadContainmentMath.SanitizePositive(collision.DepthMeters, 0f);
            telemetry[telemetryIndex] = new BulkheadTelemetryEntry
            {
                Frame = frame,
                ActiveCount = 0u,
                SealedCount = 0u,
                JammedCount = 0u,
                AverageClosure = 0f,
                AuthorityCadenceHz = BulkheadContainmentMath.SanitizePositive(cadenceHz, 5f),
                GlobalQualityWeight = BulkheadContainmentMath.Sanitize01(q, 0f),
                LastScheduleMicroseconds = BulkheadContainmentMath.SanitizePositive(_lastScheduleMicroseconds, 0f),
                StateHash = 2166136261u,
                CollisionEdgeHash = collision.EdgeHashID,
                CollisionDepthMeters = collisionDepth,
                Flags = flags
            };
            cursor[0] = unchecked(cursor[0] + 1u);
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            TryFinalizeBulkheadJobsNoWait();
            TryFlushPendingDataVaultRebind();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (!TryFinalizeBulkheadJobsNoWait())
            {
                DisableShaderGlobals();
                DisableHatchShaderGlobals();
                return;
            }

            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
            {
                DisableShaderGlobals();
                return;
            }

            IDataVault vault = ResolveVault();
            DumpBlackBoxIfRequested(vault);
            VisualSyncHatchLocks(vault);

            if (!uploadShaderBuffer)
            {
                DisableShaderGlobals();
                return;
            }

            if (_activeCount <= 0)
            {
                DisableShaderGlobals();
                return;
            }

            if (vault == null || !RefreshVaultState(vault, refreshHatchLocks: false) || !EnsureGraphicsBuffers())
            {
                DisableShaderGlobals();
                return;
            }

            if (!Read(in _statesHandle, BufferID.Shinobu220BulkheadStates, out NativeArray<BulkheadStateDTO> states) ||
                !Read(in _telemetryHandle, BufferID.Shinobu220BulkheadTelemetryRing, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                !Read(in _telemetryCursorHandle, BufferID.Shinobu220BulkheadTelemetryCursor, out NativeArray<uint> cursor))
            {
                DisableShaderGlobals();
                return;
            }
            if (!states.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                states.Length <= 0 ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                DisableShaderGlobals();
                return;
            }

            uint readCursor = cursor[0] == 0u ? 0u : cursor[0] - 1u;
            BulkheadTelemetryEntry entry = telemetry[(int)(readCursor % (uint)telemetry.Length)];
            _lastTelemetryFrame = entry.Frame;
            _lastTelemetryAverageClosure = entry.AverageClosure;
            _lastTelemetryCollisionEdgeHash = entry.CollisionEdgeHash;
            _lastTelemetryCollisionDepthMeters = entry.CollisionDepthMeters;
            int uploadCount = math.clamp(_activeCount <= 0 ? 1 : _activeCount, 1, math.min(states.Length, BulkheadContainmentConstants.ShaderUploadCapacity));
            uint stateHash = entry.StateHash;
            bool shouldUpload = !_shaderHasValidReadBuffer ||
                                _shaderUploadDirty ||
                                _lastShaderUploadCount != uploadCount ||
                                _lastShaderUploadHash != stateHash;
            if (shouldUpload)
            {
                GraphicsBuffer writeBuffer = GetShaderStateBuffer(_shaderWriteBufferSlot);
                if (UploadNativeArray(writeBuffer, states, uploadCount))
                {
                    _shaderReadBufferSlot = _shaderWriteBufferSlot;
                    _shaderWriteBufferSlot = (byte)(1 - _shaderWriteBufferSlot);
                    _lastShaderUploadCount = uploadCount;
                    _lastShaderUploadHash = stateHash;
                    _shaderHasValidReadBuffer = true;
                    _shaderUploadDirty = false;
                }
            }

            GraphicsBuffer readBuffer = _shaderHasValidReadBuffer ? GetShaderStateBuffer(_shaderReadBufferSlot) : null;
            if (readBuffer == null)
            {
                DisableShaderGlobals();
                return;
            }

            Shader.SetGlobalBuffer(GlobalBulkheadStatesId, readBuffer);
            float q = ResolveBulkheadQualityWeight();
            Shader.SetGlobalVector(GlobalBulkheadParamsId, new Vector4(uploadCount, uploadShaderBuffer ? 1f : 0f, _lastFrame, q));
            _shaderGlobalsActive = true;

        }

        private void DumpBlackBoxIfRequested(IDataVault vault)
        {
            if (vault == null ||
                _telemetryHandle.Generation == 0u ||
                _telemetryCursorHandle.Generation == 0u)
            {
                return;
            }

            if (!IsBulkheadVaultHandle(in _telemetryHandle, BufferID.Shinobu220BulkheadTelemetryRing) ||
                !IsBulkheadVaultHandle(in _telemetryCursorHandle, BufferID.Shinobu220BulkheadTelemetryCursor) ||
                !vault.TryResolveHandle(in _telemetryHandle, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                !vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<uint> cursor) ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0 ||
                cursor[0] == 0u)
            {
                return;
            }

            uint cursorValue = cursor[0];
            if (cursorValue == _lastDumpedTelemetryCursor ||
                cursorValue == _lastDumpAttemptTelemetryCursor)
                return;

            BulkheadTelemetryEntry entry = telemetry[(int)((cursorValue - 1u) % (uint)telemetry.Length)];
            if ((entry.Flags & BulkheadTelemetryFlags.DumpRequested) != 0u)
            {
                _lastDumpAttemptTelemetryCursor = cursorValue;
                if (TryDumpBlackBox(telemetry, cursorValue))
                    _lastDumpedTelemetryCursor = cursorValue;
            }
        }

        private bool TryAcquirePlayerState(IDataVault vault, uint currentFrame, out double3 playerAup, out float3 velocity)
        {
            playerAup = double3.zero;
            velocity = float3.zero;
            if (!TryAcquirePlayerKinematicStateBuffer(vault, currentFrame, out NativeArray<LockstepPlayerKinematicState>.ReadOnly playerStates))
            {
                return false;
            }

            LockstepPlayerKinematicState player = playerStates[0];
            if (!IsPlayerFrameFresh(player.Frame, currentFrame))
                return false;

            playerAup = BulkheadContainmentMath.ToAbsoluteDouble3(in player);
            velocity = player.Velocity;
            return math.all(math.isfinite(playerAup)) && math.all(math.isfinite(velocity));
        }

        private bool TryAcquirePlayerKinematicStateBuffer(
            IDataVault vault,
            uint currentFrame,
            out NativeArray<LockstepPlayerKinematicState>.ReadOnly playerStates)
        {
            playerStates = default;
            if (vault == null)
                return false;

            if (IsVaultHandleForBuffer(in _playerKinematicStateHandle, BufferID.PlayerKinematicState) &&
                vault.TryReadOnlyHandle(in _playerKinematicStateHandle, out playerStates) &&
                playerStates.Length > 0)
            {
                return true;
            }

            if (currentFrame == 0u)
            {
                if (_nextPlayerStateHandleBindFrame != 0u)
                    return false;
                _nextPlayerStateHandleBindFrame = 16u;
            }
            else
            {
                if (currentFrame < _nextPlayerStateHandleBindFrame)
                    return false;
                _nextPlayerStateHandleBindFrame = unchecked(currentFrame + 16u);
            }

            return TryBindPlayerKinematicStateHandle(vault) &&
                   IsVaultHandleForBuffer(in _playerKinematicStateHandle, BufferID.PlayerKinematicState) &&
                   vault.TryReadOnlyHandle(in _playerKinematicStateHandle, out playerStates) &&
                   playerStates.Length > 0;
        }

        private bool TryBindPlayerKinematicStateHandle(IDataVault vault)
        {
            if (vault == null)
            {
                _playerKinematicStateHandle = default;
                return false;
            }

            if (!vault.TryGetGenerationHandle(
                    BufferID.PlayerKinematicState,
                    out VaultGenerationHandle<LockstepPlayerKinematicState> handle))
            {
                _playerKinematicStateHandle = default;
                return false;
            }

            _playerKinematicStateHandle = handle;
            return true;
        }

        private static bool IsPlayerFrameFresh(uint playerFrame, uint currentFrame)
        {
            if (playerFrame == 0u || currentFrame == 0u || playerFrame > currentFrame)
                return false;

            return currentFrame - playerFrame <= 1u;
        }

        private static float ResolveAuthorityCadenceHz()
        {
            float weight = BulkheadContainmentMath.Sanitize01(AuthoritativeQualityWeight, 1f);
            return math.lerp(5f, 30f, weight * weight);
        }

        private static float ResolveBulkheadQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return MathLodApproximation.SaturateFinite(quality, AuthoritativeQualityWeight);
        }

        private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = timing.FixedDelta;
            return math.isfinite(fixedDelta) && fixedDelta > 0f
                ? math.clamp(fixedDelta, 0.0001f, 0.05f)
                : LockedSimulationTickDeltaSeconds;
        }

        private bool EnsureGraphicsBuffers()
        {
            int stride = UnsafeUtility.SizeOf<BulkheadStateDTO>();
            int count = BulkheadContainmentConstants.ShaderUploadCapacity;
            if (IsGraphicsBufferValid(_shaderStateBufferA, count, stride) &&
                IsGraphicsBufferValid(_shaderStateBufferB, count, stride))
            {
                return true;
            }

            ReleaseGraphicsBuffers();
            try
            {
                _shaderStateBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    count,
                    stride);
                _shaderStateBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    count,
                    stride);
                _shaderWriteBufferSlot = 0;
                _shaderReadBufferSlot = 0;
                _shaderHasValidReadBuffer = false;
                _shaderUploadDirty = true;
                return _shaderStateBufferA != null && _shaderStateBufferB != null;
            }
            catch (Exception)
            {
                ReleaseGraphicsBuffers();
                return false;
            }
        }

        private static bool IsGraphicsBufferValid(GraphicsBuffer buffer, int count, int stride)
        {
            return buffer != null && buffer.count == count && buffer.stride == stride;
        }

        private GraphicsBuffer GetShaderStateBuffer(byte slot)
        {
            return slot == 0 ? _shaderStateBufferA : _shaderStateBufferB;
        }

        private void DisableShaderGlobals()
        {
            if (!_shaderGlobalsActive)
                return;

            Shader.SetGlobalVector(GlobalBulkheadParamsId, Vector4.zero);
            _shaderGlobalsActive = false;
            _shaderHasValidReadBuffer = false;
            _shaderUploadDirty = true;
        }

        private void ReleaseGraphicsBuffers()
        {
            DisableShaderGlobals();
            ReleaseHatchGraphicsBuffers();

            if (_shaderStateBufferA != null)
            {
                _shaderStateBufferA.Release();
                _shaderStateBufferA = null;
            }

            if (_shaderStateBufferB != null)
            {
                _shaderStateBufferB.Release();
                _shaderStateBufferB = null;
            }

            _shaderHasValidReadBuffer = false;
            _shaderGlobalsActive = false;
            _shaderUploadDirty = true;
        }

        private static bool UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            if (destination == null || !source.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<T>())
                return false;

            bool locked = false;
            bool copied = false;
            bool unlocked = false;
            try
            {
                NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
                locked = true;
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<T>());
                copied = true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (locked)
                    unlocked = TryUnlockBufferAfterWrite<T>(destination, safeCount);
            }

            return copied && unlocked;
        }

        private static bool TryUnlockBufferAfterWrite<T>(GraphicsBuffer destination, int count) where T : struct
        {
            try
            {
                destination.UnlockBufferAfterWrite<T>(count);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryDumpBlackBox(NativeArray<BulkheadTelemetryEntry> telemetry, uint cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(_dumpPath))
                return false;

            const int telemetryDumpEntryBytes = 64;
            int entrySize = UnsafeUtility.SizeOf<BulkheadTelemetryEntry>();
            if (entrySize != telemetryDumpEntryBytes)
                return false;

            string dumpDirectory = Path.GetDirectoryName(_dumpPath);
            if (string.IsNullOrEmpty(dumpDirectory))
                return false;

            try
            {
                Directory.CreateDirectory(dumpDirectory);
                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> header = stackalloc byte[16];
                WriteUInt(header, 0, 0x53483232u);
                WriteUInt(header, 4, cursor);
                WriteUInt(header, 8, (uint)telemetry.Length);
                WriteUInt(header, 12, (uint)entrySize);
                stream.Write(header);

                Span<byte> entryBytes = stackalloc byte[telemetryDumpEntryBytes];
                for (int i = 0; i < telemetry.Length; i++)
                {
                    BulkheadTelemetryEntry entry = telemetry[i];
                    WriteTelemetryEntry(entryBytes, in entry);
                    stream.Write(entryBytes);
                }

                return true;
            }
            catch (Exception ex) when (IsColdStorageException(ex))
            {
                return false;
            }
        }

        private static bool IsColdStorageException(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is ArgumentException ||
                   ex is NotSupportedException;
        }

        private static void WriteUInt(Span<byte> destination, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
        }

        private static void WriteULong(Span<byte> destination, int offset, ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
        }

        private static void WriteFloat(Span<byte> destination, int offset, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), math.asuint(value));
        }

        private static void WriteTelemetryEntry(Span<byte> entryBytes, in BulkheadTelemetryEntry entry)
        {
            entryBytes.Clear();
            WriteUInt(entryBytes, 0, entry.Frame);
            WriteUInt(entryBytes, 4, entry.ActiveCount);
            WriteUInt(entryBytes, 8, entry.SealedCount);
            WriteUInt(entryBytes, 12, entry.JammedCount);
            WriteFloat(entryBytes, 16, entry.AverageClosure);
            WriteFloat(entryBytes, 20, entry.AuthorityCadenceHz);
            WriteFloat(entryBytes, 24, entry.GlobalQualityWeight);
            WriteFloat(entryBytes, 28, entry.LastScheduleMicroseconds);
            WriteUInt(entryBytes, 32, entry.StateHash);
            WriteUInt(entryBytes, 36, entry.CollisionEdgeHash);
            WriteFloat(entryBytes, 40, entry.CollisionDepthMeters);
            WriteUInt(entryBytes, 44, entry.Flags);
            WriteULong(entryBytes, 48, entry.Reserved0);
            WriteULong(entryBytes, 56, entry.Reserved1);
        }

        private static float ElapsedMicroseconds(long startTimestamp)
        {
            long delta = Stopwatch.GetTimestamp() - startTimestamp;
            return (float)(delta * 1000000.0 / Stopwatch.Frequency);
        }

#if UNITY_EDITOR
        private static int ParseProfiles(ReadOnlySpan<byte> csv, NativeArray<BulkheadProfileDTO> profiles)
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

                BulkheadProfileDTO profile = default;
                int column = 0;
                int cellIndex = 0;
                bool rowValid = true;
                while (cellIndex <= line.Length)
                {
                    ReadOnlySpan<byte> cell = SliceNextCell(line, ref cellIndex);
                    switch (column)
                    {
                        case 0: profile.ProfileHash = HashAscii(cell); break;
                        case 1: rowValid &= TryParseFloat(cell, out profile.CloseSpeedPerSecond); break;
                        case 2: rowValid &= TryParseFloat(cell, out profile.OpenSpeedPerSecond); break;
                        case 3: rowValid &= TryParseFloat(cell, out profile.OverrideDistanceMeters); break;
                        case 4: rowValid &= TryParseFloat(cell, out profile.CatastrophicIntegrity01); break;
                        case 5: rowValid &= TryParseFloat(cell, out profile.WidthMeters); break;
                        case 6: rowValid &= TryParseFloat(cell, out profile.HeightMeters); break;
                        case 7: profile.Flags = HashAscii(cell); break;
                    }
                    column++;
                }

                if (!rowValid || column < 7 || profile.ProfileHash == 0u)
                    continue;
                profile.CloseSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(profile.CloseSpeedPerSecond, 2f));
                profile.OpenSpeedPerSecond = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(profile.OpenSpeedPerSecond, 2.5f));
                profile.OverrideDistanceMeters = math.max(0.5f, BulkheadContainmentMath.SanitizePositive(profile.OverrideDistanceMeters, 3f));
                profile.CatastrophicIntegrity01 = BulkheadContainmentMath.Sanitize01(profile.CatastrophicIntegrity01, 0.35f);
                profile.WidthMeters = math.max(0.25f, BulkheadContainmentMath.SanitizePositive(profile.WidthMeters, 2.6f));
                profile.HeightMeters = math.max(0.25f, BulkheadContainmentMath.SanitizePositive(profile.HeightMeters, 3.2f));
                profiles[count++] = profile;
            }

            return count;
        }
#endif

        private static ReadOnlySpan<byte> SliceNextLine(ReadOnlySpan<byte> csv, ref int index)
        {
            int start = index;
            while (index < csv.Length && csv[index] != (byte)'\n' && csv[index] != (byte)'\r')
                index++;
            ReadOnlySpan<byte> line = csv.Slice(start, index - start);
            while (index < csv.Length && (csv[index] == (byte)'\n' || csv[index] == (byte)'\r'))
                index++;
            return Trim(line);
        }

        private static ReadOnlySpan<byte> SliceNextCell(ReadOnlySpan<byte> line, ref int index)
        {
            int start = index;
            while (index < line.Length && line[index] != (byte)',')
                index++;
            ReadOnlySpan<byte> cell = line.Slice(start, index - start);
            index++;
            return Trim(cell);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return end >= start ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> value, string prefix)
        {
            if (value.Length < prefix.Length)
                return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                byte a = value[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a += 32;
                if (a != (byte)prefix[i])
                    return false;
            }
            return true;
        }

        private static uint HashAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-' || value[index] == (byte)'+')
            {
                sign = value[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            float whole = 0f;
            bool hasDigit = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                hasDigit = true;
                whole = whole * 10f + (value[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10f + (value[index] - (byte)'0');
                    scale *= 10f;
                    index++;
                }
            }

            if (!hasDigit || index != value.Length)
                return false;

            result = sign * (whole + fraction / scale);
            return math.isfinite(result);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_vaultInitialized ||
                !Read(in _statesHandle, BufferID.Shinobu220BulkheadStates, out NativeArray<BulkheadStateDTO> states) ||
                !Read(in _planesHandle, BufferID.Shinobu220BulkheadPlanes, out NativeArray<BulkheadPlaneDTO> planes) ||
                !states.IsCreated ||
                !planes.IsCreated)
            {
                return;
            }

            int count = math.clamp(_activeCount, 0, math.min(states.Length, planes.Length));
            for (int i = 0; i < count; i++)
            {
                BulkheadStateDTO state = states[i];
                if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                    continue;

                BulkheadPlaneDTO plane = planes[i];
                if (!math.all(math.isfinite(plane.CenterAup)))
                    continue;

                float3 local = AupPrecisionMath.LocalDeltaFloat3(plane.CenterAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
                if (!math.all(math.isfinite(local)))
                    continue;

                float3 normal3 = BulkheadContainmentMath.SafeNormal(plane.Normal, new float3(0f, 0f, 1f));
                float width = math.max(0.25f, BulkheadContainmentMath.SanitizePositive(plane.WidthMeters, 2.6f));
                float height = math.max(0.25f, BulkheadContainmentMath.SanitizePositive(plane.HeightMeters, 3.2f));
                float thickness = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(plane.HalfThicknessMeters, 0.18f)) * 2f;
                Vector3 center = new Vector3(local.x, local.y, local.z);
                Vector3 normal = new Vector3(normal3.x, normal3.y, normal3.z);
                float closure = BulkheadContainmentMath.Sanitize01(state.ClosureProgress, 0f);
                if (closure >= 0.95f)
                    Gizmos.color = new Color(1f, 0.05f, 0.02f, 0.8f);
                else if (closure >= 0.5f)
                    Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.65f);
                else
                    Gizmos.color = new Color(0.1f, 0.95f, 0.35f, 0.45f);

                Gizmos.DrawRay(center, normal * 0.75f);
                Gizmos.DrawWireCube(center, new Vector3(width, height, thickness));
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(center, normal * 1.25f);
            }

            DrawHatchLockGizmos();
        }
#endif

        private sealed class PreSimulationPhaseSystem : PhaseSystemBase
        {
            public PreSimulationPhaseSystem(BulkheadContainmentRuntime owner) : base(owner, DispatcherPhase.PreSimulation, BulkheadContainmentConstants.PreSimulationHash) { }
            public override void PreSimulationTick(in DispatcherTimingDTO timing) { Owner.PreSimulationTick(in timing); }
        }

        private sealed class SimulationPhaseSystem : PhaseSystemBase
        {
            public SimulationPhaseSystem(BulkheadContainmentRuntime owner) : base(owner, DispatcherPhase.Simulation, BulkheadContainmentConstants.SimulationHash) { }
            public override JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return Owner.ScheduleSimulation(in timing, in context, dependsOn);
            }
        }

        private sealed class PostSimulationPhaseSystem : PhaseSystemBase
        {
            public PostSimulationPhaseSystem(BulkheadContainmentRuntime owner) : base(owner, DispatcherPhase.PostSimulation, BulkheadContainmentConstants.PostSimulationHash) { }
            public override void PostSimulationTick(in DispatcherTimingDTO timing) { Owner.PostSimulationTick(in timing); }
        }

        private sealed class VisualSyncPhaseSystem : PhaseSystemBase
        {
            public VisualSyncPhaseSystem(BulkheadContainmentRuntime owner) : base(owner, DispatcherPhase.VisualSync, BulkheadContainmentConstants.VisualSyncHash) { }
            public override void VisualSyncTick(in DispatcherTimingDTO timing) { Owner.VisualSyncTick(in timing); }
        }

        private abstract class PhaseSystemBase : IDispatcherSystem
        {
            protected readonly BulkheadContainmentRuntime Owner;
            private readonly DispatcherPhase _phase;
            private readonly uint _hash;

            protected PhaseSystemBase(BulkheadContainmentRuntime owner, DispatcherPhase phase, uint hash)
            {
                Owner = owner;
                _phase = phase;
                _hash = hash;
            }

            public uint GetSystemIdHash() { return _hash; }
            public DispatcherPhase GetDispatcherPhase() { return _phase; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public virtual void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public virtual JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public virtual void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public virtual void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }
    }
}
