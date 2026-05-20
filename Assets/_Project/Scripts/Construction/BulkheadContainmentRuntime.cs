using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Construction
{
    [DefaultExecutionOrder(-180)]
    public sealed unsafe class BulkheadContainmentRuntime : MonoBehaviour, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const float DefaultPlayerRadiusMeters = 0.38f;
        private const float DefaultCloseSpeed = 2.4f;
        private const float DefaultOpenSpeed = 3.0f;
        private const float DefaultOverrideDistance = 3.2f;
        private const float DefaultCatastrophicIntegrity = 0.18f;
        private const uint MockSeed = 0x53484E42u;
        private const SystemID OwnerSystemId = SystemID.Construction;

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
        private bool _mockGenerated;
        private int _activeCount;
        private uint _lastFrame;
        private float _authorityAccumulator;
        private float _lastScheduleMicroseconds;
        private JobHandle _preSimulationHandle;
        private JobHandle _simulationHandle;
        private bool _preSimulationScheduled;
        private bool _simulationScheduled;
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
        private string _dumpPath;

        public static bool TryPublishAirlockBulkheadState(
            uint edgeHash,
            bool locked,
            in AbsoluteUniversePosition centerAup,
            float3 normal,
            float widthMeters,
            float heightMeters,
            uint siblingNodeHash)
        {
            if (!Application.isPlaying || edgeHash == 0u)
                return false;

            return BulkheadContainmentIntentBus.TryWriteAirlockBulkheadIntent(
                edgeHash,
                locked,
                BulkheadContainmentMath.ToAbsoluteDouble3(in centerAup),
                normal,
                widthMeters,
                heightMeters,
                1f,
                siblingNodeHash,
                0u);
        }

        public static bool TryReadEditorState(out int activeCount, out float quality, out float cadenceHz, out float lastScheduleMicroseconds)
        {
            activeCount = 0;
            quality = 0f;
            cadenceHz = 0f;
            lastScheduleMicroseconds = 0f;
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            activeCount = runtime._activeCount;
            quality = HomeostasisBrain.GlobalQualityWeight;
            cadenceHz = runtime.ResolveAuthorityCadenceHz(quality);
            lastScheduleMicroseconds = runtime._lastScheduleMicroseconds;
            return true;
        }

        public static bool TryApplyEditorTuning(float closeSpeed, float openSpeed, float overrideDistance, float catastrophicIntegrity)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            runtime.closeSpeedPerSecond = math.max(0.05f, closeSpeed);
            runtime.openSpeedPerSecond = math.max(0.05f, openSpeed);
            runtime.overrideDistanceMeters = math.max(0.5f, overrideDistance);
            runtime.catastrophicIntegrity01 = math.saturate(catastrophicIntegrity);
            return true;
        }

        public static bool TryLoadProfilesFromCsvBytes(ReadOnlySpan<byte> csv)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultState(vault))
                return false;

            if (!runtime.Resolve(in runtime._profilesHandle, out NativeArray<BulkheadProfileDTO> profiles))
                return false;
            return profiles.IsCreated && ParseProfiles(csv, profiles) > 0;
        }

        public static bool TryLoadProfilesFromCsvFile(string path)
        {
            BulkheadContainmentRuntime runtime = s_active;
            if (runtime == null || string.IsNullOrEmpty(path))
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultState(vault))
                return false;

            if (!runtime.Resolve(in runtime._profilesHandle, out NativeArray<BulkheadProfileDTO> profiles) ||
                !runtime.Resolve(in runtime._csvScratchHandle, out NativeArray<byte> scratch) ||
                !profiles.IsCreated ||
                !scratch.IsCreated ||
                profiles.Length <= 0 ||
                scratch.Length <= 0)
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

        private void Awake()
        {
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, "Docs/AgentLogs/Dump_SHINOBU_220.bin"));
        }

        private void OnEnable()
        {
            _shutdownStarted = false;
            s_active = this;
            _vault = GlobalRegistry.DataVault;
            BulkheadContainmentIntentBus.BindDataVault(_vault);
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
                RequestDataVaultRebind(currentService as IDataVault);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RequestDataVaultRebind(currentService as IDataVault);
        }

        private void RequestDataVaultRebind(IDataVault currentVault)
        {
            if (!_vaultRebindPending && ReferenceEquals(_vault, currentVault))
            {
                BulkheadContainmentIntentBus.BindDataVault(_vault);
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

            if (!_preSimulationHandle.IsCompleted || !_simulationHandle.IsCompleted)
                return false;

            IDataVault pendingVault = _pendingVault;
            _pendingVault = null;
            _vaultRebindPending = false;
            ReleaseVaultHandles();
            _vault = pendingVault;
            BulkheadContainmentIntentBus.BindDataVault(_vault);
            _preSimulationHandle = default;
            _simulationHandle = default;
            ResetVaultRuntimeState(clearScheduledFlags: true);
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
        }

        private void ResetVaultRuntimeState(bool clearScheduledFlags)
        {
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _mockGenerated = false;
            _activeCount = 0;
            _authorityAccumulator = 0f;
            _lastFrame = 0u;
            _lastDumpedTelemetryCursor = 0u;
            if (clearScheduledFlags)
            {
                _preSimulationScheduled = false;
                _simulationScheduled = false;
            }
            _shaderUploadDirty = true;
        }

        private IDataVault ResolveVault()
        {
            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
                return null;

            return _vault;
        }

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveVault();
            if (vault == null || handle.Generation == 0u)
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer);
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _statesHandle);
                ReleaseVaultHandle(vault, ref _aupsHandle);
                ReleaseVaultHandle(vault, ref _planesHandle);
                ReleaseVaultHandle(vault, ref _csrEdgesHandle);
                ReleaseVaultHandle(vault, ref _edgeConductivityHandle);
                ReleaseVaultHandle(vault, ref _fluidFlowHandle);
                ReleaseVaultHandle(vault, ref _moduleIntegrityHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _telemetryHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _collisionResultsHandle);
                ReleaseVaultHandle(vault, ref _profilesHandle);
                ReleaseVaultHandle(vault, ref _csvScratchHandle);
                ReleaseVaultHandle(vault, ref _shaderUploadHandle);
                ReleaseVaultHandle(vault, ref _intentRingHandle);
                ReleaseVaultHandle(vault, ref _intentControlHandle);
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
            _vault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!_layoutChecked)
            {
                _layoutValid = BulkheadStateLayoutGuard.ValidateLayout();
                _layoutChecked = true;
            }

            if (!_layoutValid)
                throw new FatalArchitectureException("SHINOBU_220 BulkheadStateDTO layout mismatch.");

            int capacity = math.clamp(bulkheadCapacity, 1, BulkheadContainmentConstants.DefaultBulkheadCapacity);
            if (!_vaultInitialized)
            {
                _statesHandle = vault.GetGenerationHandle<BulkheadStateDTO>(BufferID.Shinobu220BulkheadStates, capacity, OwnerSystemId);
                _aupsHandle = vault.GetGenerationHandle<double3>(BufferID.Shinobu220BulkheadAups, capacity, OwnerSystemId);
                _planesHandle = vault.GetGenerationHandle<BulkheadPlaneDTO>(BufferID.Shinobu220BulkheadPlanes, capacity, OwnerSystemId);
                _csrEdgesHandle = vault.GetGenerationHandle<BulkheadCsrEdgeDTO>(BufferID.Shinobu220BulkheadCsrEdges, capacity, OwnerSystemId);
                _edgeConductivityHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu220BulkheadEdgeConductivity, capacity, OwnerSystemId);
                _fluidFlowHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu220BulkheadFluidFlow, capacity, OwnerSystemId);
                _moduleIntegrityHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu220BulkheadModuleIntegrity, capacity, OwnerSystemId);
                _tuningHandle = vault.GetGenerationHandle<BulkheadTuningDTO>(BufferID.Shinobu220BulkheadTuning, 1, OwnerSystemId);
                _telemetryHandle = vault.GetGenerationHandle<BulkheadTelemetryEntry>(BufferID.Shinobu220BulkheadTelemetryRing, BulkheadContainmentConstants.TelemetryFrameCount, OwnerSystemId);
                _telemetryCursorHandle = vault.GetGenerationHandle<uint>(BufferID.Shinobu220BulkheadTelemetryCursor, 1, OwnerSystemId);
                _collisionResultsHandle = vault.GetGenerationHandle<BulkheadCollisionResultDTO>(BufferID.Shinobu220BulkheadCollisionResults, 1, OwnerSystemId);
                _profilesHandle = vault.GetGenerationHandle<BulkheadProfileDTO>(BufferID.Shinobu220BulkheadProfiles, BulkheadContainmentConstants.ProfileCapacity, OwnerSystemId);
                _csvScratchHandle = vault.GetGenerationHandle<byte>(BufferID.Shinobu220BulkheadCsvScratch, 8192, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                _shaderUploadHandle = vault.GetGenerationHandle<BulkheadStateDTO>(BufferID.Shinobu220BulkheadShaderUpload, BulkheadContainmentConstants.ShaderUploadCapacity, OwnerSystemId);
                _intentRingHandle = vault.GetGenerationHandle<BulkheadContainmentIntentDTO>(BufferID.Shinobu220BulkheadIntentRing, BulkheadContainmentIntentBus.IntentCapacity, OwnerSystemId);
                _intentControlHandle = vault.GetGenerationHandle<BulkheadContainmentIntentControlDTO>(BufferID.Shinobu220BulkheadIntentControl, 1, OwnerSystemId);
                _vaultInitialized = true;
            }

            if (!Resolve(in _statesHandle, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _aupsHandle, out NativeArray<double3> aups) ||
                !Resolve(in _planesHandle, out NativeArray<BulkheadPlaneDTO> planes) ||
                !Resolve(in _csrEdgesHandle, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                !Resolve(in _edgeConductivityHandle, out NativeArray<float> conductivity) ||
                !Resolve(in _fluidFlowHandle, out NativeArray<float> fluidFlow) ||
                !Resolve(in _moduleIntegrityHandle, out NativeArray<float> moduleIntegrity) ||
                !Resolve(in _tuningHandle, out NativeArray<BulkheadTuningDTO> tuning))
            {
                return false;
            }

            if (!states.IsCreated || !aups.IsCreated || !planes.IsCreated || !csrEdges.IsCreated ||
                !conductivity.IsCreated || !fluidFlow.IsCreated || !moduleIntegrity.IsCreated || !tuning.IsCreated)
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

            float q = HomeostasisBrain.GlobalQualityWeight;
            tuning[0] = new BulkheadTuningDTO
            {
                CloseSpeedPerSecond = math.max(0.05f, closeSpeedPerSecond),
                OpenSpeedPerSecond = math.max(0.05f, openSpeedPerSecond),
                OverrideDistanceMeters = math.max(0.5f, overrideDistanceMeters),
                CatastrophicIntegrity01 = math.saturate(catastrophicIntegrity01),
                GlobalQualityWeight = q,
                AuthorityCadenceHz = ResolveAuthorityCadenceHz(q),
                ActiveCount = (uint)math.clamp(_activeCount, 0, capacity),
                Flags = uploadShaderBuffer ? 1u : 0u
            };

            return true;
        }

        private JobHandle ScheduleMockDataIfRequired(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            JobHandle dependency)
        {
            if (!generateMockBulkheads ||
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
            _activeCount = math.max(_activeCount, count);
            _mockGenerated = true;
            return handle;
        }

        private bool ApplyAirlockBulkheadStateIntent(
            NativeArray<BulkheadStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<BulkheadPlaneDTO> planes,
            NativeArray<BulkheadCsrEdgeDTO> csrEdges,
            NativeArray<float> moduleIntegrity,
            uint edgeHash,
            bool locked,
            double3 center,
            float3 normal,
            float widthMeters,
            float heightMeters,
            float parentIntegrity01,
            uint siblingNodeHash)
        {
            int slot = FindOrAllocateSlot(states, edgeHash);
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
                WidthMeters = math.max(0.25f, widthMeters),
                HeightMeters = math.max(0.25f, heightMeters),
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
            NativeArray<float> moduleIntegrity)
        {
            if (!Resolve(in _intentRingHandle, out NativeArray<BulkheadContainmentIntentDTO> intents) ||
                !Resolve(in _intentControlHandle, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows) ||
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
                    (intent.Flags & BulkheadContainmentIntentFlags.NonFinite) != 0u)
                {
                    continue;
                }

                ApplyAirlockBulkheadStateIntent(
                    states,
                    aups,
                    planes,
                    csrEdges,
                    moduleIntegrity,
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

        private int FindOrAllocateSlot(NativeArray<BulkheadStateDTO> states, uint edgeHash)
        {
            int firstFree = -1;
            for (int i = 0; i < states.Length; i++)
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
            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
            {
                _preSimulationScheduled = false;
                return;
            }

            _preSimulationHandle = default;
            _preSimulationScheduled = false;

            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return;

            if (!Resolve(in _statesHandle, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _aupsHandle, out NativeArray<double3> aups) ||
                !Resolve(in _planesHandle, out NativeArray<BulkheadPlaneDTO> planes) ||
                !Resolve(in _csrEdgesHandle, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                !Resolve(in _moduleIntegrityHandle, out NativeArray<float> moduleIntegrity) ||
                !Resolve(in _collisionResultsHandle, out NativeArray<BulkheadCollisionResultDTO> collisions))
            {
                return;
            }
            if (!states.IsCreated || !aups.IsCreated || !planes.IsCreated || !csrEdges.IsCreated ||
                !moduleIntegrity.IsCreated || !collisions.IsCreated)
                return;
            if (collisions.Length <= 0)
                return;

            ConsumePublishedIntents(states, aups, planes, csrEdges, moduleIntegrity);
            int count = math.min(_activeCount, states.Length);
            if (count <= 0)
            {
                collisions[0] = default;
                return;
            }

            TryResolvePlayerState(vault, out double3 playerAup, out float3 velocity, out uint frame);
            double3 predictedAup = playerAup + (double3)(velocity * math.max(0f, timing.FrameDelta));
            JobHandle dependency = _simulationScheduled ? _simulationHandle : default;

            if (vault.TryGetBuffer(BufferID.InteractionSignalQueue, out NativeArray<InteractionUiSignal> signals) &&
                signals.IsCreated && signals.Length > 0)
            {
                ProcessDoorOverrideJob overrideJob = new ProcessDoorOverrideJob
                {
                    Signals = (InteractionUiSignal*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(signals),
                    States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                    Aups = (double3*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(aups),
                    SignalCount = math.min(signals.Length, 32),
                    StateCount = count,
                    PlayerAup = playerAup,
                    OverrideDistanceMeters = overrideDistanceMeters
                };
                dependency = overrideJob.Schedule(dependency);
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
                Frame = frame
            };
            _preSimulationHandle = collisionJob.Schedule(dependency);
            _preSimulationScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Construction, _preSimulationHandle);
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            JobHandle dependency = _preSimulationScheduled ? JobHandle.CombineDependencies(dependsOn, _preSimulationHandle) : dependsOn;
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return dependency;

            float q = HomeostasisBrain.GlobalQualityWeight;
            float cadenceHz = ResolveAuthorityCadenceHz(q);
            float dt = math.max(0f, timing.FrameDelta);
            _lastFrame = context.Frame;
            _authorityAccumulator += dt;
            float period = 1f / math.max(1f, cadenceHz);

            if (!Resolve(in _statesHandle, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _collisionResultsHandle, out NativeArray<BulkheadCollisionResultDTO> collisions) ||
                !Resolve(in _telemetryHandle, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                !Resolve(in _telemetryCursorHandle, out NativeArray<uint> cursor))
            {
                return dependency;
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
                return dependency;
            }

            if (generateMockBulkheads && !_mockGenerated && _activeCount <= 0)
            {
                if (!Resolve(in _aupsHandle, out NativeArray<double3> aups) ||
                    !Resolve(in _planesHandle, out NativeArray<BulkheadPlaneDTO> planes) ||
                    !Resolve(in _csrEdgesHandle, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                    !aups.IsCreated ||
                    !planes.IsCreated ||
                    !csrEdges.IsCreated ||
                    aups.Length <= 0 ||
                    planes.Length <= 0 ||
                    csrEdges.Length <= 0)
                {
                    return dependency;
                }

                dependency = ScheduleMockDataIfRequired(states, aups, planes, csrEdges, dependency);
            }

            int count = math.min(_activeCount, states.Length);
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
                return dependency;
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

            if (!Resolve(in _csrEdgesHandle, out NativeArray<BulkheadCsrEdgeDTO> csrEdges) ||
                !Resolve(in _edgeConductivityHandle, out NativeArray<float> conductivity) ||
                !Resolve(in _fluidFlowHandle, out NativeArray<float> fluidFlow) ||
                !Resolve(in _moduleIntegrityHandle, out NativeArray<float> moduleIntegrity))
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
            if (!csrEdges.IsCreated || !conductivity.IsCreated || !fluidFlow.IsCreated || !moduleIntegrity.IsCreated)
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

            float authorityDelta = math.min(_authorityAccumulator, 0.2f);
            _authorityAccumulator = 0f;

            long start = Stopwatch.GetTimestamp();
            UpdateBulkheadClosureJob updateJob = new UpdateBulkheadClosureJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                Count = count,
                DeltaSeconds = authorityDelta,
                CloseSpeedPerSecond = closeSpeedPerSecond,
                OpenSpeedPerSecond = openSpeedPerSecond,
                GlobalQualityWeight = q
            };
            JobHandle handle = updateJob.Schedule(count, 32, dependency);

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

            ApplyBulkheadLockJob lockJob = new ApplyBulkheadLockJob
            {
                States = (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                CsrEdges = (BulkheadCsrEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csrEdges),
                EdgeConductivity = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(conductivity),
                EdgeFluidFlow = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidFlow),
                Count = count,
                EdgeScalarCount = conductivity.Length
            };
            handle = lockJob.Schedule(count, 32, handle);

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
                LastScheduleMicroseconds = _lastScheduleMicroseconds,
                Flags = flags
            };
            JobHandle handle = telemetryJob.Schedule(dependency);
            _lastScheduleMicroseconds = ElapsedMicroseconds(scheduleStart);
            _simulationHandle = handle;
            _simulationScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Construction, handle);
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
            telemetry[telemetryIndex] = new BulkheadTelemetryEntry
            {
                Frame = frame,
                ActiveCount = 0u,
                SealedCount = 0u,
                JammedCount = 0u,
                AverageClosure = 0f,
                AuthorityCadenceHz = cadenceHz,
                GlobalQualityWeight = BulkheadContainmentMath.Sanitize01(q, 0f),
                LastScheduleMicroseconds = _lastScheduleMicroseconds,
                StateHash = 2166136261u,
                CollisionEdgeHash = collision.EdgeHashID,
                CollisionDepthMeters = collision.DepthMeters,
                Flags = BulkheadTelemetryFlags.ScheduleTimeOnly
            };
            cursor[0] = unchecked(cursor[0] + 1u);
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            _preSimulationScheduled = false;
            _simulationScheduled = false;
            TryFlushPendingDataVaultRebind();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_vaultRebindPending && !TryFlushPendingDataVaultRebind())
            {
                DisableShaderGlobals();
                return;
            }

            IDataVault vault = ResolveVault();
            DumpBlackBoxIfRequested(vault);

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

            if (vault == null || !EnsureVaultState(vault) || !EnsureGraphicsBuffers())
            {
                DisableShaderGlobals();
                return;
            }

            if (!Resolve(in _statesHandle, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _telemetryHandle, out NativeArray<BulkheadTelemetryEntry> telemetry) ||
                !Resolve(in _telemetryCursorHandle, out NativeArray<uint> cursor))
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
            float q = HomeostasisBrain.GlobalQualityWeight;
            Shader.SetGlobalVector(GlobalBulkheadParamsId, new Vector4(uploadCount, uploadShaderBuffer ? 1f : 0f, _lastFrame, q));
            _shaderGlobalsActive = true;

        }

        private void DumpBlackBoxIfRequested(IDataVault vault)
        {
            if (vault == null ||
                _telemetryHandle.Generation == 0u ||
                _telemetryCursorHandle.Generation == 0u ||
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
            if (cursorValue == _lastDumpedTelemetryCursor)
                return;

            BulkheadTelemetryEntry entry = telemetry[(int)((cursorValue - 1u) % (uint)telemetry.Length)];
            if ((entry.Flags & BulkheadTelemetryFlags.DumpRequested) != 0u)
            {
                DumpBlackBox(telemetry, cursorValue);
                _lastDumpedTelemetryCursor = cursorValue;
            }
        }

        private bool TryResolvePlayerState(IDataVault vault, out double3 playerAup, out float3 velocity, out uint frame)
        {
            playerAup = double3.zero;
            velocity = float3.zero;
            frame = _lastFrame;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.PlayerKinematicState, out NativeArray<LockstepPlayerKinematicState> playerStates) ||
                !playerStates.IsCreated ||
                playerStates.Length == 0)
            {
                return false;
            }

            LockstepPlayerKinematicState player = playerStates[0];
            playerAup = BulkheadContainmentMath.ToAbsoluteDouble3(in player);
            velocity = player.Velocity;
            frame = player.Frame;
            return true;
        }

        private float ResolveAuthorityCadenceHz(float q)
        {
            float weight = math.saturate(q);
            return math.lerp(5f, 30f, weight * weight);
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

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<T>());
            destination.UnlockBufferAfterWrite<T>(safeCount);
            return true;
        }

        private void DumpBlackBox(NativeArray<BulkheadTelemetryEntry> telemetry, uint cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(_dumpPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));
            using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            Span<byte> header = stackalloc byte[16];
            WriteUInt(header, 0, 0x53483232u);
            WriteUInt(header, 4, cursor);
            WriteUInt(header, 8, (uint)telemetry.Length);
            WriteUInt(header, 12, (uint)UnsafeUtility.SizeOf<BulkheadTelemetryEntry>());
            stream.Write(header);

            Span<byte> entryBytes = stackalloc byte[64];
            for (int i = 0; i < telemetry.Length; i++)
            {
                BulkheadTelemetryEntry entry = telemetry[i];
                WriteTelemetryEntry(entryBytes, in entry);
                stream.Write(entryBytes);
            }
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

        private static int ParseProfiles(ReadOnlySpan<byte> csv, NativeArray<BulkheadProfileDTO> profiles)
        {
            int count = 0;
            int index = 0;
            while (index < csv.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(csv, ref index);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;
                if (StartsWithAscii(line, "profile"))
                    continue;

                BulkheadProfileDTO profile = default;
                int column = 0;
                int cellIndex = 0;
                while (cellIndex <= line.Length)
                {
                    ReadOnlySpan<byte> cell = ReadCell(line, ref cellIndex);
                    switch (column)
                    {
                        case 0: profile.ProfileHash = HashAscii(cell); break;
                        case 1: TryParseFloat(cell, out profile.CloseSpeedPerSecond); break;
                        case 2: TryParseFloat(cell, out profile.OpenSpeedPerSecond); break;
                        case 3: TryParseFloat(cell, out profile.OverrideDistanceMeters); break;
                        case 4: TryParseFloat(cell, out profile.CatastrophicIntegrity01); break;
                        case 5: TryParseFloat(cell, out profile.WidthMeters); break;
                        case 6: TryParseFloat(cell, out profile.HeightMeters); break;
                        case 7: profile.Flags = HashAscii(cell); break;
                    }
                    column++;
                }

                if (profile.ProfileHash == 0u)
                    continue;
                profiles[count++] = profile;
            }

            return count;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> csv, ref int index)
        {
            int start = index;
            while (index < csv.Length && csv[index] != (byte)'\n' && csv[index] != (byte)'\r')
                index++;
            ReadOnlySpan<byte> line = csv.Slice(start, index - start);
            while (index < csv.Length && (csv[index] == (byte)'\n' || csv[index] == (byte)'\r'))
                index++;
            return Trim(line);
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int index)
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
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float whole = 0f;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
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
                    fraction = fraction * 10f + (value[index] - (byte)'0');
                    scale *= 10f;
                    index++;
                }
            }

            result = sign * (whole + fraction / scale);
            return math.isfinite(result);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_vaultInitialized ||
                !Resolve(in _statesHandle, out NativeArray<BulkheadStateDTO> states) ||
                !Resolve(in _planesHandle, out NativeArray<BulkheadPlaneDTO> planes) ||
                !states.IsCreated ||
                !planes.IsCreated)
            {
                return;
            }

            int count = math.min(_activeCount, math.min(states.Length, planes.Length));
            for (int i = 0; i < count; i++)
            {
                BulkheadStateDTO state = states[i];
                if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                    continue;

                BulkheadPlaneDTO plane = planes[i];
                float3 local = AupPrecisionMath.LocalDeltaFloat3(plane.CenterAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
                Vector3 center = new Vector3(local.x, local.y, local.z);
                Vector3 normal = new Vector3(plane.Normal.x, plane.Normal.y, plane.Normal.z);
                float closure = math.saturate(state.ClosureProgress);
                if (closure >= 0.95f)
                    Gizmos.color = new Color(1f, 0.05f, 0.02f, 0.8f);
                else if (closure >= 0.5f)
                    Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.65f);
                else
                    Gizmos.color = new Color(0.1f, 0.95f, 0.35f, 0.45f);

                Gizmos.DrawRay(center, normal * 0.75f);
                Gizmos.DrawWireCube(center, new Vector3(plane.WidthMeters, plane.HeightMeters, plane.HalfThicknessMeters * 2f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(center, normal * 1.25f);
            }
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
