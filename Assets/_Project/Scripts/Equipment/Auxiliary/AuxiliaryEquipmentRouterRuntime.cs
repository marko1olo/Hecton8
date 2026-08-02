using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Equipment.Auxiliary
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Equipment/Auxiliary Equipment Router Runtime")]
    public sealed class AuxiliaryEquipmentRouterRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int JobBatchSize = 64;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_229.bin";
#if UNITY_EDITOR
        private const string ProfilesCsvFileName = "auxiliary_equipment_profiles.csv";
#endif
        private static readonly double s_timestampToMicroseconds = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
        private static readonly ulong RuntimeMutationGuardMask =
            MutationGuardBit(AuxiliaryEquipmentVaultIds.Deployments) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.States) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.TetherAnchors) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.ActiveCount) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.RouteCounters) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.VfxMatrices) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.TelemetryRing) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.TelemetryCursor) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.ActiveEquipmentState);
        private static readonly ulong TuningMutationGuardMask = MutationGuardBit(AuxiliaryEquipmentVaultIds.Tuning);
        private static readonly ulong ProfileImportMutationGuardMask =
            MutationGuardBit(AuxiliaryEquipmentVaultIds.Profiles) |
            MutationGuardBit(AuxiliaryEquipmentVaultIds.Tuning);

        [SerializeField, Range(64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries)]
        private int deploymentCapacity = AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries;

        [SerializeField] private bool registerWithDispatcher = true;
        [SerializeField] private bool seedMockDataOnColdBoot;

        private static AuxiliaryEquipmentRouterRuntime s_activeRuntime;

        private IDataVault _dataVault;
        private VaultGenerationHandle<DeployedAuxiliaryDTO> _deploymentsHandle;
        private VaultGenerationHandle<AuxiliaryStateDTO> _statesHandle;
        private VaultGenerationHandle<AuxiliaryTetherAnchorDTO> _tetherAnchorsHandle;
        private VaultGenerationHandle<int> _activeCountHandle;
        private VaultGenerationHandle<AuxiliaryTuningDTO> _tuningHandle;
        private VaultGenerationHandle<AuxiliaryRouteCounterDTO> _routeCountersHandle;
        private VaultGenerationHandle<AuxiliaryVfxMatrixDTO> _vfxMatricesHandle;
        private VaultGenerationHandle<AuxiliaryTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<AuxiliaryProfileDTO> _profilesHandle;
        private VaultGenerationHandle<AuxiliaryActiveEquipmentDTO> _activeEquipmentHandle;

        private GraphicsBuffer _vfxGpuBufferA;
        private GraphicsBuffer _vfxGpuBufferB;
        private GraphicsBuffer _vfxGpuReadBuffer;
        private JobHandle _pendingHandle;
        private long _pendingStartTicks;
        private double3 _lastCameraAup;
        private double3 _lastUploadedVfxCameraAup;
        private float _lastCadenceHz = AuxiliaryEquipmentConstants.MaximumCadenceHz;
        private float _lastQualityWeight = 1f;
        private float _lastUploadedVfxQualityWeight;
        private int _lastVfxUploadCount;
        private uint _lastVfxUploadHash;
        private uint _frameIndex;
        private int _vfxGpuWriteIndex;
        private bool _jobActive;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _buffersReady;
        private bool _signalLanesReady;
        private bool _mockSeeded;
        private bool _dumpWritten;
        private bool _profilesLoaded;
        private bool _vfxUploadValid;
        private bool _runtimeGuardHeld;
        private IDataVault _runtimeGuardVault;
        private AuxiliaryProfileLoadResult _lastProfileLoadResult;

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        public static bool TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime)
        {
            runtime = s_activeRuntime;
            return runtime != null && runtime.isActiveAndEnabled;
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            aup = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(aup));
        }

        public static bool TryDeployFlare(Vector3 runtimePosition, float lifetimeSeconds = -1f)
        {
            if (!TryResolveAupDoubleFromRuntimeOrigin(runtimePosition, out double3 aup))
                return false;

            return TryDeployFlareAup(aup, lifetimeSeconds);
        }

        public static bool TryDeployFlareAup(double3 aup, float lifetimeSeconds = -1f)
        {
            return TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) &&
                   runtime.TryDeployAup(AuxiliaryEquipmentConstants.FlarePrefabHash, aup, lifetimeSeconds, 0f, default, false);
        }

        public static bool TryCancelFlare(Vector3 runtimePosition, float radiusMeters = 2f)
        {
            if (!TryResolveAupDoubleFromRuntimeOrigin(runtimePosition, out double3 aup))
                return false;

            return TryCancelNearestAup(AuxiliaryEquipmentConstants.FlarePrefabHash, aup, radiusMeters);
        }

        public static bool TryDeploySensorPing(Vector3 runtimePosition, float lifetimeSeconds = -1f, float maxRadiusMeters = -1f)
        {
            if (!TryResolveAupDoubleFromRuntimeOrigin(runtimePosition, out double3 aup))
                return false;

            return TryDeploySensorPingAup(aup, lifetimeSeconds, maxRadiusMeters);
        }

        public static bool TryDeploySensorPingAup(double3 aup, float lifetimeSeconds = -1f, float maxRadiusMeters = -1f)
        {
            return TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) &&
                   runtime.TryDeployAup(AuxiliaryEquipmentConstants.SensorPingPrefabHash, aup, lifetimeSeconds, maxRadiusMeters, default, false);
        }

        public static bool TryDeployGravityTether(Vector3 projectilePosition, Vector3 anchorPosition, float lifetimeSeconds = -1f)
        {
            if (!TryResolveAupDoubleFromRuntimeOrigin(projectilePosition, out double3 projectileAup) ||
                !TryResolveAupDoubleFromRuntimeOrigin(anchorPosition, out double3 anchorAup))
            {
                return false;
            }

            return TryDeployGravityTetherAup(projectileAup, anchorAup, lifetimeSeconds);
        }

        public static bool TryDeployGravityTetherAup(double3 projectileAup, double3 anchorAup, float lifetimeSeconds = -1f)
        {
            if (!math.all(math.isfinite(projectileAup)) ||
                !math.all(math.isfinite(anchorAup)) ||
                !TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime))
            {
                return false;
            }

            return runtime.TryDeployAup(AuxiliaryEquipmentConstants.GravityTetherPrefabHash, projectileAup, lifetimeSeconds, 0f, anchorAup, true);
        }

        public static bool TryCancelGravityTether(Vector3 runtimePosition, float radiusMeters = 2f)
        {
            if (!TryResolveAupDoubleFromRuntimeOrigin(runtimePosition, out double3 aup))
                return false;

            return TryCancelNearestAup(AuxiliaryEquipmentConstants.GravityTetherPrefabHash, aup, radiusMeters);
        }

        public static bool TryCancelNearestAup(uint prefabHash, double3 aup, float radiusMeters)
        {
            return TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) &&
                   runtime.TryCancelNearest(prefabHash, aup, radiusMeters);
        }

        public static bool TryReadNearestRemainingLifetime(uint prefabHash, Vector3 runtimePosition, float radiusMeters, out float remainingLifetime)
        {
            remainingLifetime = 0f;
            if (!TryResolveAupDoubleFromRuntimeOrigin(runtimePosition, out double3 aup))
                return false;

            return TryReadNearestRemainingLifetimeAup(prefabHash, aup, radiusMeters, out remainingLifetime);
        }

        public static bool TryReadNearestRemainingLifetimeAup(uint prefabHash, double3 aup, float radiusMeters, out float remainingLifetime)
        {
            remainingLifetime = 0f;
            return TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) &&
                   runtime.TryFindNearestRemaining(prefabHash, aup, radiusMeters, out remainingLifetime);
        }

        public static bool TryReadVfxGraphicsBuffer(out GraphicsBuffer buffer, out int activeCount)
        {
            buffer = null;
            activeCount = 0;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                runtime._vfxGpuReadBuffer == null ||
                !runtime._vfxGpuReadBuffer.IsValid())
            {
                return false;
            }

            buffer = runtime._vfxGpuReadBuffer;
            activeCount = runtime._lastVfxUploadCount;
            return activeCount > 0;
        }

        public static bool TrySetPresentationCameraAup(double3 cameraAup)
        {
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) || !math.all(math.isfinite(cameraAup)))
                return false;

            runtime._lastCameraAup = cameraAup;
            return true;
        }

        public static bool TryReadTelemetry(out AuxiliaryTelemetryEntry latest)
        {
            latest = default;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                !runtime.TryResolveExistingViews(out AuxiliaryVaultViews views) ||
                !views.TelemetryRing.IsCreated ||
                !views.TelemetryCursor.IsCreated ||
                views.TelemetryRing.Length == 0 ||
                views.TelemetryCursor.Length == 0)
            {
                return false;
            }

            int cursor = views.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = 0;
            latest = views.TelemetryRing[cursor % views.TelemetryRing.Length];
            return true;
        }

        public static bool TryReadDeployments(out NativeArray<DeployedAuxiliaryDTO>.ReadOnly deployments, out int activeCount)
        {
            deployments = default;
            activeCount = 0;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                runtime._jobActive ||
                !runtime.TryResolveExistingViews(out AuxiliaryVaultViews views) ||
                !views.Deployments.IsCreated)
            {
                return false;
            }

            deployments = views.Deployments.AsReadOnly();
            activeCount = runtime.ResolveActiveBound(views);
            return true;
        }

        public static bool TryReadTuning(out AuxiliaryTuningDTO tuning)
        {
            tuning = default;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                !runtime.TryResolveExistingViews(out AuxiliaryVaultViews views) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length == 0)
            {
                return false;
            }

            tuning = views.Tuning[0];
            return true;
        }

        public static bool TryWriteTuning(in AuxiliaryTuningDTO tuning)
        {
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                runtime._jobActive ||
                !runtime.TryLockTuningBuffer())
            {
                return false;
            }

            try
            {
                IDataVault vault = runtime._dataVault;
                if (vault == null ||
                    !IsHandleCreated(in runtime._tuningHandle) ||
                    !vault.TryResolveHandle(in runtime._tuningHandle, out NativeArray<AuxiliaryTuningDTO> tuningBuffer) ||
                    !tuningBuffer.IsCreated ||
                    tuningBuffer.Length == 0)
                {
                    return false;
                }

                tuningBuffer[0] = tuning;
                return true;
            }
            finally
            {
                runtime.UnlockTuningBuffer();
            }
        }

        public bool GenerateMockDeployments()
        {
            if (_jobActive || !EnsureRuntimeReady())
                return false;

            if (!TryLockRuntimeBuffers())
                return false;

            bool scheduled = false;
            try
            {
                if (!TryResolveExistingViews(out AuxiliaryVaultViews views))
                    return false;

                AuxiliaryTuningDTO tuning = ResolveTuning(views);
                _lastQualityWeight = ResolveQualityWeight(tuning);
                _lastCadenceHz = AuxiliaryEquipmentMath.ResolveCadenceHz(_lastQualityWeight, in tuning);
                _lastCameraAup = ResolveCameraAup();
                double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                int mockCount = math.min(AuxiliaryEquipmentConstants.MockDeploymentCount, deploymentCapacity);
                // Host owns ActiveCount write — keeps Burst jobs free of Deployments/ActiveCount pair.
                if (views.ActiveCount.IsCreated && views.ActiveCount.Length > 0)
                    views.ActiveCount[0] = mockCount;

                GenerateMockAuxiliaryDeploymentsJob job = new GenerateMockAuxiliaryDeploymentsJob
                {
                    Deployments = views.Deployments,
                    States = views.States,
                    TetherAnchors = views.TetherAnchors,
                    ActiveEquipment = views.ActiveEquipment,
                    RouteCounters = views.RouteCounters,
                    VfxMatrices = views.VfxMatrices,
                    Tuning = tuning,
                    OriginAup = origin,
                    RequestedCount = mockCount,
                    FrameIndex = _frameIndex
                };

                StageAuxiliaryVFXJob vfxJob = new StageAuxiliaryVFXJob
                {
                    Deployments = views.Deployments,
                    States = views.States,
                    ActiveCount = mockCount,
                    VfxMatrices = views.VfxMatrices,
                    CameraAup = _lastCameraAup,
                    GlobalQualityWeight = _lastQualityWeight,
                    VfxScale = tuning.VfxScale
                };


                _pendingStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                JobHandle mockHandle = job.Schedule(deploymentCapacity, JobBatchSize);
                _pendingHandle = vfxJob.Schedule(deploymentCapacity, JobBatchSize, mockHandle);
                H8Memory.RegisterActiveJob(SystemID.GameplayTools, _pendingHandle);
                _jobActive = true;
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    UnlockRuntimeBuffers();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            s_activeRuntime = this;
            InitializeService(GlobalRegistry.DataVault);
            TryRegisterHotSwapListener();
            TryRegisterDispatcherTicks();
        }

        private void OnDisable()
        {
            ShutdownForLifecycle();
        }

        private void OnDestroy()
        {
            ShutdownForLifecycle();
        }

        private void ShutdownForLifecycle()
        {
            CompletePendingJobForTeardown();
            TryUnregisterHotSwapListener();
            TryUnregisterDispatcherTicks();

            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            ReleaseOwnedVaultHandles();
            ReleaseGraphicsBuffer(ref _vfxGpuBufferA);
            ReleaseGraphicsBuffer(ref _vfxGpuBufferB);
            _vfxGpuReadBuffer = null;
            _lastVfxUploadCount = 0;
            _lastVfxUploadHash = 0u;
            _vfxUploadValid = false;
            _buffersReady = false;
            _profilesLoaded = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterDispatcherTicks();

                if (currentService != null)
                    TryRegisterDispatcherTicks();

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault && currentService != null)
            {
                CompletePendingJobForTeardown();
                ReleaseOwnedVaultHandles();
                _buffersReady = false;
                _profilesLoaded = false;
                InitializeService(currentService as IDataVault);
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryRegisterDispatcherTicks()
        {
            if (!registerWithDispatcher || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            TryRegisterUpdateTick();
            TryRegisterLateFrameTick();
        }

        private void TryRegisterUpdateTick()
        {
            if (_registeredUpdate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        public void InitializeService(IDataVault dataVault)
        {
            if (!Application.isPlaying)
                return;

            if (dataVault != null)
                _dataVault = dataVault;

            EnsureSignalLanes();
            EnsureRuntimeReady();
        }

        public void Tick(float deltaTime)
        {
            if (_jobActive || !_buffersReady)
                return;

            if (seedMockDataOnColdBoot && !_mockSeeded)
            {
                _mockSeeded = GenerateMockDeployments();
                return;
            }

            if (!TryLockRuntimeBuffers())
                return;

            bool keepRuntimeGuard = false;
            try
            {
                if (!TryResolveExistingViews(out AuxiliaryVaultViews views))
                    return;

                AuxiliaryTuningDTO tuning = ResolveTuning(views);
                _lastQualityWeight = ResolveQualityWeight(tuning);
                _lastCadenceHz = AuxiliaryEquipmentMath.ResolveCadenceHz(_lastQualityWeight, in tuning);
                _lastCameraAup = ResolveCameraAup();

                int activeBound = ResolveActiveBound(views);
                UpdateDeployedAuxiliaryJob updateJob = new UpdateDeployedAuxiliaryJob
                {
                    Deployments = views.Deployments,
                    States = views.States,
                    TetherAnchors = views.TetherAnchors,
                    ActiveEquipment = views.ActiveEquipment,
                    RouteCounters = views.RouteCounters,
                    // Scalar bound — UpdateDeployedAuxiliaryJob.ActiveCount is int (not vault NativeArray).
                    // Avoids Burst aliasing when vault resolve returns a same-pointer view pair.
                    ActiveCount = activeBound,
                    FlareWriter = SignalBus<AuxiliaryFlareLightSignal>.OpenParallelWriter(),

                    FlareWriterBudget = SignalBus<AuxiliaryFlareLightSignal>.ParallelWriterBudget,
                    SonarWriter = SignalBus<AuxiliarySonarRequestSignal>.OpenParallelWriter(),
                    SonarWriterBudget = SignalBus<AuxiliarySonarRequestSignal>.ParallelWriterBudget,
                    TetherWriter = SignalBus<AuxiliaryTetherConnectionSignal>.OpenParallelWriter(),
                    TetherWriterBudget = SignalBus<AuxiliaryTetherConnectionSignal>.ParallelWriterBudget,
                    Tuning = tuning,
                    FrameIndex = _frameIndex,
                    SimulationDeltaTime = deltaTime,
                    GlobalQualityWeight = _lastQualityWeight
                };

                StageAuxiliaryVFXJob vfxJob = new StageAuxiliaryVFXJob
                {
                    Deployments = views.Deployments,
                    States = views.States,
                    ActiveCount = activeBound,
                    VfxMatrices = views.VfxMatrices,
                    CameraAup = _lastCameraAup,
                    GlobalQualityWeight = _lastQualityWeight,
                    VfxScale = tuning.VfxScale
                };

                _pendingStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                JobHandle updateHandle = updateJob.Schedule(deploymentCapacity, JobBatchSize);

                _pendingHandle = vfxJob.Schedule(deploymentCapacity, JobBatchSize, updateHandle);
                H8Memory.RegisterActiveJob(SystemID.GameplayTools, _pendingHandle);
                _jobActive = true;
                keepRuntimeGuard = true;
            }
            finally
            {
                if (!keepRuntimeGuard)
                    UnlockRuntimeBuffers();
            }
        }

        public void LateFrameTick()
        {
            TryFinalizePendingJobNoWait();
        }

        private bool TryDeployAup(uint prefabHash, double3 aup, float lifetimeSeconds, float scalar0, double3 tetherAnchorAup, bool hasTetherAnchor)
        {
            if (_jobActive ||
                !_buffersReady ||
                !math.all(math.isfinite(aup)) ||
                (hasTetherAnchor && !math.all(math.isfinite(tetherAnchorAup))))
            {
                return false;
            }

            if (!TryLockRuntimeBuffers())
                return false;

            try
            {
                if (!TryResolveExistingViews(out AuxiliaryVaultViews views))
                    return false;

                AuxiliaryTuningDTO tuning = ResolveTuning(views);
                float authoredLifetime = lifetimeSeconds > 0f && math.isfinite(lifetimeSeconds)
                    ? lifetimeSeconds
                    : AuxiliaryEquipmentMath.ResolveBaseLifetime(prefabHash, in tuning);
                float baseLifetime = AuxiliaryEquipmentMath.SanitizePositive(authoredLifetime, AuxiliaryEquipmentMath.ResolveBaseLifetime(prefabHash, in tuning));
                float scalar = scalar0 > 0f && math.isfinite(scalar0) ? scalar0 : ResolveDefaultScalar(prefabHash, in tuning);
                int slot = FindDeploymentSlot(views.Deployments, ResolveActiveBound(views));
                if (slot < 0)
                    return false;

                DeployedAuxiliaryDTO deployment = default;
                deployment.AUP_Position = aup;
                deployment.PrefabHashID = prefabHash;
                deployment.RemainingLifetime = baseLifetime;
                views.Deployments[slot] = deployment;
                views.States[slot] = new AuxiliaryStateDTO
                {
                    BaseLifetime = baseLifetime,
                    Scalar0 = scalar,
                    AccumulatedDelta = 0f,
                    Flags = AuxiliaryEquipmentMath.ResolveKindFlags(prefabHash)
                };
                if ((uint)slot < (uint)views.TetherAnchors.Length)
                {
                    views.TetherAnchors[slot] = hasTetherAnchor
                        ? new AuxiliaryTetherAnchorDTO
                        {
                            AnchorAup = tetherAnchorAup,
                            Flags = AuxiliaryEquipmentFlags.Active | AuxiliaryEquipmentFlags.GravityTether
                        }
                        : default;
                }

                if (views.ActiveCount.IsCreated && views.ActiveCount.Length > 0 && views.ActiveCount[0] <= slot)
                    views.ActiveCount[0] = slot + 1;

                return true;
            }
            finally
            {
                UnlockRuntimeBuffers();
            }
        }

        private bool TryCancelNearest(uint prefabHash, double3 aup, float radiusMeters)
        {
            if (_jobActive || !_buffersReady || prefabHash == 0u || !math.all(math.isfinite(aup)))
                return false;

            if (!TryLockRuntimeBuffers())
                return false;

            try
            {
                if (!TryResolveExistingViews(out AuxiliaryVaultViews views))
                    return false;

                double radius = AuxiliaryEquipmentMath.SanitizePositive(radiusMeters, 0.01f);
                double bestSq = radius * radius;
                int best = -1;
                int length = ResolveActiveBound(views);
                for (int i = 0; i < length; i++)
                {
                    DeployedAuxiliaryDTO deployment = views.Deployments[i];
                    if (deployment.PrefabHashID != prefabHash || deployment.RemainingLifetime <= 0f)
                        continue;

                    double3 delta = deployment.AUP_Position - aup;
                    double sq = math.dot(delta, delta);
                    if (math.isfinite(sq) && sq <= bestSq)
                    {
                        bestSq = sq;
                        best = i;
                    }
                }

                if (best < 0)
                    return false;

                views.Deployments[best] = default;
                if ((uint)best < (uint)views.States.Length)
                    views.States[best] = default;
                if ((uint)best < (uint)views.TetherAnchors.Length)
                    views.TetherAnchors[best] = default;
                if ((uint)best < (uint)views.ActiveEquipment.Length)
                    views.ActiveEquipment[best] = default;

                CompactActiveCount(views);
                return true;
            }
            finally
            {
                UnlockRuntimeBuffers();
            }
        }

        private bool TryFindNearestRemaining(uint prefabHash, double3 aup, float radiusMeters, out float remainingLifetime)
        {
            remainingLifetime = 0f;
            if (_jobActive || !_buffersReady || prefabHash == 0u || !math.all(math.isfinite(aup)) || !TryResolveExistingViews(out AuxiliaryVaultViews views))
                return false;

            double radius = AuxiliaryEquipmentMath.SanitizePositive(radiusMeters, 0.01f);
            double bestSq = radius * radius;
            float bestLifetime = 0f;
            int length = ResolveActiveBound(views);
            for (int i = 0; i < length; i++)
            {
                DeployedAuxiliaryDTO deployment = views.Deployments[i];
                if (deployment.PrefabHashID != prefabHash || deployment.RemainingLifetime <= 0f)
                    continue;

                double3 delta = deployment.AUP_Position - aup;
                double sq = math.dot(delta, delta);
                if (math.isfinite(sq) && sq <= bestSq)
                {
                    bestSq = sq;
                    bestLifetime = deployment.RemainingLifetime;
                }
            }

            remainingLifetime = bestLifetime;
            return bestLifetime > 0f;
        }

        private void CompactActiveCount(AuxiliaryVaultViews views)
        {
            if (!views.ActiveCount.IsCreated || views.ActiveCount.Length == 0 || !views.Deployments.IsCreated)
                return;

            int count = math.min(math.max(views.ActiveCount[0], 0), views.Deployments.Length);
            while (count > 0)
            {
                DeployedAuxiliaryDTO tail = views.Deployments[count - 1];
                if (tail.PrefabHashID != 0u && tail.RemainingLifetime > 0f)
                    break;
                count--;
            }

            views.ActiveCount[0] = count;
        }

        private int FindDeploymentSlot(NativeArray<DeployedAuxiliaryDTO> deployments, int activeBound)
        {
            int initializedLength = math.min(math.max(activeBound, 0), math.min(deploymentCapacity, deployments.Length));
            for (int i = 0; i < initializedLength; i++)
            {
                DeployedAuxiliaryDTO deployment = deployments[i];
                if (deployment.PrefabHashID == 0u || deployment.RemainingLifetime <= 0f)
                    return i;
            }

            int capacity = math.min(deploymentCapacity, deployments.Length);
            return initializedLength < capacity ? initializedLength : -1;
        }

        private int ResolveActiveBound(in AuxiliaryVaultViews views)
        {
            if (!views.ActiveCount.IsCreated || views.ActiveCount.Length == 0 || !views.Deployments.IsCreated)
                return 0;

            return math.min(math.clamp(views.ActiveCount[0], 0, views.Deployments.Length), deploymentCapacity);
        }

        private bool TryFinalizePendingJobNoWait()
        {
            if (!_jobActive)
                return true;

            if (!_pendingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            FinalizeCompletedPendingJob();
            return true;
        }

        private void CompletePendingJobForTeardown()
        {
            if (!_jobActive)
                return;

            ForceCompletePendingJobInPostSimulationWindow();
            FinalizeCompletedPendingJob();
        }

        private void ForceCompletePendingJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void FinalizeCompletedPendingJob()
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _pendingStartTicks;
            float cpuMicroseconds = (float)(elapsedTicks * s_timestampToMicroseconds);
            _jobActive = false;
            _frameIndex = _frameIndex == uint.MaxValue ? 1u : _frameIndex + 1u;

            try
            {
                if (TryResolveExistingViews(out AuxiliaryVaultViews views))
                {
                    CompactActiveCount(views);
                    UploadVfxMatricesToGpu(views);
                    RecordAuxiliaryTelemetryPass telemetryPass = new RecordAuxiliaryTelemetryPass
                    {
                        Deployments = views.Deployments,
                        RouteCounters = views.RouteCounters,
                        TelemetryRing = views.TelemetryRing,
                        TelemetryCursor = views.TelemetryCursor,
                        ActiveCount = views.ActiveCount,
                        FrameIndex = _frameIndex,
                        EffectiveCadenceHz = _lastCadenceHz,
                        CpuMicroseconds = cpuMicroseconds,
                        GlobalQualityWeight = _lastQualityWeight,
                        LaneDroppedSignals = ResolveLaneDroppedSignals(),
                        LaneCorruptedSignals = ResolveLaneCorruptedSignals(),
                        LanePeakQueuedSignals = ResolveLanePeakQueuedSignals()
                    };
                    telemetryPass.Execute();
                    if (cpuMicroseconds > AuxiliaryEquipmentConstants.FaultDumpThresholdMicroseconds ||
                        TryLatestTelemetryHasFault(views.TelemetryRing, views.TelemetryCursor))
                    {
                        TryDumpTelemetry(views.TelemetryRing);
                    }
                }
            }
            finally
            {
                UnlockRuntimeBuffers();
            }
        }

        private bool EnsureRuntimeReady()
        {
            if (_dataVault == null)
                return false;

            EnsureSignalLanes();
            if (_buffersReady)
                return TryResolveExistingViews(out _);

            bool ok = TryAcquireViews(out AuxiliaryVaultViews views);
            if (!ok)
                return false;

            TryLoadProfilesCold(views);
            EnsureVfxGraphicsBuffer(math.clamp(deploymentCapacity, 64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries));
            _buffersReady = true;
            return true;
        }

        private bool EnsureVfxGraphicsBuffer(int capacity)
        {
            if (IsValidVfxBuffer(_vfxGpuBufferA, capacity) &&
                IsValidVfxBuffer(_vfxGpuBufferB, capacity))
            {
                return true;
            }

            ReleaseGraphicsBuffer(ref _vfxGpuBufferA);
            ReleaseGraphicsBuffer(ref _vfxGpuBufferB);
            _vfxGpuReadBuffer = null;
            _lastVfxUploadCount = 0;
            _lastVfxUploadHash = 0u;
            _vfxUploadValid = false;
            _vfxGpuWriteIndex = 0;

            _vfxGpuBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AuxiliaryVfxMatrixDTO>(capacity);
            _vfxGpuBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AuxiliaryVfxMatrixDTO>(capacity);
            return IsValidVfxBuffer(_vfxGpuBufferA, capacity) &&
                   IsValidVfxBuffer(_vfxGpuBufferB, capacity);
        }

        private void UploadVfxMatricesToGpu(in AuxiliaryVaultViews views)
        {
            int capacity = math.clamp(deploymentCapacity, 64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries);
            if (!views.VfxMatrices.IsCreated ||
                !IsValidVfxBuffer(_vfxGpuBufferA, capacity) ||
                !IsValidVfxBuffer(_vfxGpuBufferB, capacity))
            {
                _lastVfxUploadCount = 0;
                return;
            }

            int count = ResolveActiveBound(views);
            if (count <= 0)
            {
                _vfxGpuReadBuffer = null;
                _lastVfxUploadCount = 0;
                _vfxUploadValid = false;
                return;
            }

            int previousUploadCount = _lastVfxUploadCount;
            uint snapshotHash = ResolveVfxSnapshotHash(views.Deployments, count);
            if (_vfxUploadValid &&
                previousUploadCount == count &&
                _lastVfxUploadHash == snapshotHash &&
                _lastUploadedVfxQualityWeight == _lastQualityWeight &&
                math.all(_lastUploadedVfxCameraAup == _lastCameraAup))
            {
                _lastVfxUploadCount = count;
                return;
            }

            GraphicsBuffer target = _vfxGpuWriteIndex == 0 ? _vfxGpuBufferA : _vfxGpuBufferB;
            GraphicsBufferUploadUtility.UploadNativeArray(target, views.VfxMatrices, count);
            _vfxGpuReadBuffer = target;
            _vfxGpuWriteIndex ^= 1;
            _lastVfxUploadCount = math.min(count, target.count);
            _lastVfxUploadHash = snapshotHash;
            _lastUploadedVfxCameraAup = _lastCameraAup;
            _lastUploadedVfxQualityWeight = _lastQualityWeight;
            _vfxUploadValid = true;
        }

        private static bool IsValidVfxBuffer(GraphicsBuffer buffer, int capacity)
        {
            return buffer != null &&
                   buffer.IsValid() &&
                   buffer.count >= capacity &&
                   buffer.stride == UnsafeUtility.SizeOf<AuxiliaryVfxMatrixDTO>();
        }

        private static uint ResolveVfxSnapshotHash(NativeArray<DeployedAuxiliaryDTO> deployments, int count)
        {
            if (!deployments.IsCreated || count <= 0)
                return 0u;

            uint hash = 2166136261u;
            int length = math.min(count, deployments.Length);
            for (int i = 0; i < length; i++)
            {
                DeployedAuxiliaryDTO deployment = deployments[i];
                hash = (hash ^ (uint)i) * 16777619u;
                if (deployment.PrefabHashID == 0u ||
                    deployment.RemainingLifetime <= 0f ||
                    !math.all(math.isfinite(deployment.AUP_Position)))
                {
                    hash = (hash ^ 0x9E3779B9u) * 16777619u;
                    continue;
                }

                hash = (hash ^ deployment.PrefabHashID) * 16777619u;
                hash = (hash ^ math.hash(deployment.AUP_Position)) * 16777619u;
            }

            return hash;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private bool TryLoadProfilesCold(in AuxiliaryVaultViews views)
        {
            if (_profilesLoaded)
                return true;
            if (!views.Profiles.IsCreated || views.Profiles.Length == 0 || !views.Tuning.IsCreated || views.Tuning.Length == 0)
                return false;

            AuxiliaryProfileLoadResult result = default;
            bool parsed = false;
            Span<AuxiliaryProfileDTO> profileScratch = stackalloc AuxiliaryProfileDTO[AuxiliaryEquipmentConstants.ProfileCapacity];
#if UNITY_EDITOR
            Span<byte> csvScratch = stackalloc byte[AuxiliaryEquipmentConstants.CsvScratchBytes];
            string path = Path.Combine(Application.dataPath, "_SourceData", "Equipment", "Auxiliary", ProfilesCsvFileName);
            int byteCount = TryReadProfilesFileIntoScratch(path, csvScratch);
            if (byteCount > 0)
            {
                parsed = AuxiliaryEquipmentProfilesCsvParser.TryParseProfilesCsv(
                    csvScratch.Slice(0, byteCount),
                    profileScratch,
                    out AuxiliaryCsvParseResult csvResult);
                result = csvResult.ToProfileLoadResult();
            }
#endif

            if (!parsed)
            {
                SeedFallbackProfiles(profileScratch, ResolveTuning(views), out result);
                parsed = true;
            }

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(ProfileImportMutationGuardMask))
                return false;

            try
            {
                if (!views.Profiles.IsCreated ||
                    views.Profiles.Length == 0 ||
                    !views.Tuning.IsCreated ||
                    views.Tuning.Length == 0)
                {
                    return false;
                }

                CommitProfileScratch(profileScratch, result.ParsedRows, views.Profiles);
                ClearProfileTail(views.Profiles, result.ParsedRows);
                ApplyProfilesToTuning(views.Profiles, result.ParsedRows, views.Tuning);
                _lastProfileLoadResult = result;
                _profilesLoaded = parsed;
                return parsed;
            }
            finally
            {
                vault.ReleaseMutationGuard(ProfileImportMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        private static int TryReadProfilesFileIntoScratch(string path, Span<byte> scratch)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || scratch.Length == 0)
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int limit = stream.Length > scratch.Length ? scratch.Length : (int)stream.Length;
                    if (limit <= 0)
                        return 0;

                    int total = 0;
                    while (total < limit)
                    {
                        int read = stream.Read(scratch.Slice(total, limit - total));
                        if (read <= 0)
                            break;

                        total += read;
                    }

                    return total;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
#endif

        private static void CommitProfileScratch(
            ReadOnlySpan<AuxiliaryProfileDTO> source,
            int parsedRows,
            NativeArray<AuxiliaryProfileDTO> profiles)
        {
            if (!profiles.IsCreated)
                return;

            int count = math.clamp(parsedRows, 0, math.min(source.Length, profiles.Length));
            for (int i = 0; i < count; i++)
                profiles[i] = source[i];
        }

        private static void ClearProfileTail(NativeArray<AuxiliaryProfileDTO> profiles, int parsedRows)
        {
            if (!profiles.IsCreated)
                return;

            int start = math.clamp(parsedRows, 0, profiles.Length);
            for (int i = start; i < profiles.Length; i++)
                profiles[i] = default;
        }

        private static void SeedFallbackProfiles(
            Span<AuxiliaryProfileDTO> profiles,
            in AuxiliaryTuningDTO tuning,
            out AuxiliaryProfileLoadResult result)
        {
            result = default;
            if (profiles.Length == 0)
            {
                result.FaultFlags = AuxiliaryEquipmentFlags.Faulted;
                return;
            }

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int count = 0;
            WriteFallbackProfile(
                profiles,
                ref count,
                0xA11CF1A9u,
                AuxiliaryEquipmentConstants.FlarePrefabHash,
                AuxiliaryEquipmentMath.SanitizePositive(tuning.FlareBaseLifetime, 60f),
                AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.FlareIntensity, 3f),
                AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.FlareRange, 15f));
            WriteFallbackProfile(
                profiles,
                ref count,
                0xA11C51A7u,
                AuxiliaryEquipmentConstants.SensorPingPrefabHash,
                AuxiliaryEquipmentMath.SanitizePositive(tuning.PingBaseLifetime, 8f),
                AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.PingMaxRadius, 96f),
                AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.PingExpansionRate, 24f));
            WriteFallbackProfile(
                profiles,
                ref count,
                0xA11C7E77u,
                AuxiliaryEquipmentConstants.GravityTetherPrefabHash,
                AuxiliaryEquipmentMath.SanitizePositive(tuning.TetherBaseLifetime, 12f),
                AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.TetherMaxDistance, 60f),
                0f);

            result.ParsedRows = count;
            result.LastProfileHash = count > 0 ? profiles[count - 1].ProfileHash : 0u;
            result.FaultFlags = count > 0 ? 0u : AuxiliaryEquipmentFlags.Faulted;
        }

        private static void WriteFallbackProfile(
            Span<AuxiliaryProfileDTO> profiles,
            ref int count,
            uint profileHash,
            uint prefabHash,
            float lifetime,
            float scalar0,
            float scalar1)
        {
            if ((uint)count >= (uint)profiles.Length)
                return;

            profiles[count] = new AuxiliaryProfileDTO
            {
                ProfileHash = profileHash,
                PrefabHashID = prefabHash,
                Lifetime = lifetime,
                Scalar0 = scalar0,
                Scalar1 = scalar1,
                Flags = AuxiliaryEquipmentMath.ResolveKindFlags(prefabHash)
            };
            count++;
        }

        private static void ApplyProfilesToTuning(
            NativeArray<AuxiliaryProfileDTO> profiles,
            int profileCount,
            NativeArray<AuxiliaryTuningDTO> tuningBuffer)
        {
            if (!profiles.IsCreated || !tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            AuxiliaryTuningDTO tuning = tuningBuffer[0];
            int count = math.clamp(profileCount, 0, profiles.Length);
            for (int i = 0; i < count; i++)
            {
                AuxiliaryProfileDTO profile = profiles[i];
                if (profile.PrefabHashID == AuxiliaryEquipmentConstants.FlarePrefabHash)
                {
                    tuning.FlareBaseLifetime = AuxiliaryEquipmentMath.SanitizePositive(profile.Lifetime, tuning.FlareBaseLifetime);
                    tuning.FlareIntensity = AuxiliaryEquipmentMath.SanitizeNonNegative(profile.Scalar0, tuning.FlareIntensity);
                    tuning.FlareRange = AuxiliaryEquipmentMath.SanitizeNonNegative(profile.Scalar1, tuning.FlareRange);
                }
                else if (profile.PrefabHashID == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                {
                    tuning.PingBaseLifetime = AuxiliaryEquipmentMath.SanitizePositive(profile.Lifetime, tuning.PingBaseLifetime);
                    tuning.PingMaxRadius = AuxiliaryEquipmentMath.SanitizeNonNegative(profile.Scalar0, tuning.PingMaxRadius);
                    tuning.PingExpansionRate = AuxiliaryEquipmentMath.SanitizeNonNegative(profile.Scalar1, tuning.PingExpansionRate);
                }
                else if (profile.PrefabHashID == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                {
                    tuning.TetherBaseLifetime = AuxiliaryEquipmentMath.SanitizePositive(profile.Lifetime, tuning.TetherBaseLifetime);
                    tuning.TetherMaxDistance = AuxiliaryEquipmentMath.SanitizeNonNegative(profile.Scalar0, tuning.TetherMaxDistance);
                }
            }

            tuningBuffer[0] = tuning;
        }

        private void EnsureSignalLanes()
        {
            if (_signalLanesReady)
                return;
            if (_dataVault == null)
                return;

            const int maxAuxiliarySignalsPerFrame = AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries;
            const int lowTierFlareSignalsPerFrame = 64;
            const int lowTierSonarSignalsPerFrame = 32;
            const int lowTierTetherSignalsPerFrame = 16;
            SignalBus<AuxiliaryFlareLightSignal>.Configure(
                expectedCapacity: maxAuxiliarySignalsPerFrame,
                maxFrameSignals: maxAuxiliarySignalsPerFrame,
                lowTierFrameSignals: lowTierFlareSignalsPerFrame,
                laneHash: AuxiliaryEquipmentConstants.FlareLightLaneHash);
            SignalBus<AuxiliaryFlareLightSignal>.EnsureInitialized();
            SignalBus<AuxiliarySonarRequestSignal>.Configure(
                expectedCapacity: maxAuxiliarySignalsPerFrame,
                maxFrameSignals: maxAuxiliarySignalsPerFrame,
                lowTierFrameSignals: lowTierSonarSignalsPerFrame,
                laneHash: AuxiliaryEquipmentConstants.SensorPingLaneHash);
            SignalBus<AuxiliarySonarRequestSignal>.EnsureInitialized();
            SignalBus<AuxiliaryTetherConnectionSignal>.Configure(
                expectedCapacity: maxAuxiliarySignalsPerFrame,
                maxFrameSignals: maxAuxiliarySignalsPerFrame,
                lowTierFrameSignals: lowTierTetherSignalsPerFrame,
                laneHash: AuxiliaryEquipmentConstants.TetherLaneHash);
            SignalBus<AuxiliaryTetherConnectionSignal>.EnsureInitialized();
            _signalLanesReady = true;
        }

        private bool TryAcquireViews(out AuxiliaryVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int capacity = math.clamp(deploymentCapacity, 64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries);
            return AcquireOrRefresh(vault, ref _deploymentsHandle, AuxiliaryEquipmentVaultIds.Deployments, capacity, NativeArrayOptions.UninitializedMemory, out views.Deployments) &&
                   AcquireOrRefresh(vault, ref _statesHandle, AuxiliaryEquipmentVaultIds.States, capacity, NativeArrayOptions.UninitializedMemory, out views.States) &&
                   AcquireOrRefresh(vault, ref _tetherAnchorsHandle, AuxiliaryEquipmentVaultIds.TetherAnchors, capacity, NativeArrayOptions.UninitializedMemory, out views.TetherAnchors) &&
                   AcquireOrRefresh(vault, ref _activeCountHandle, AuxiliaryEquipmentVaultIds.ActiveCount, 1, NativeArrayOptions.ClearMemory, out views.ActiveCount) &&
                   AcquireOrRefresh(vault, ref _tuningHandle, AuxiliaryEquipmentVaultIds.Tuning, 1, NativeArrayOptions.ClearMemory, out views.Tuning) &&
                   AcquireOrRefresh(vault, ref _routeCountersHandle, AuxiliaryEquipmentVaultIds.RouteCounters, capacity, NativeArrayOptions.UninitializedMemory, out views.RouteCounters) &&
                   AcquireOrRefresh(vault, ref _vfxMatricesHandle, AuxiliaryEquipmentVaultIds.VfxMatrices, capacity, NativeArrayOptions.UninitializedMemory, out views.VfxMatrices) &&
                   AcquireOrRefresh(vault, ref _telemetryRingHandle, AuxiliaryEquipmentVaultIds.TelemetryRing, AuxiliaryEquipmentConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out views.TelemetryRing) &&
                   AcquireOrRefresh(vault, ref _telemetryCursorHandle, AuxiliaryEquipmentVaultIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory, out views.TelemetryCursor) &&
                   AcquireOrRefresh(vault, ref _profilesHandle, AuxiliaryEquipmentVaultIds.Profiles, AuxiliaryEquipmentConstants.ProfileCapacity, NativeArrayOptions.UninitializedMemory, out views.Profiles) &&
                   AcquireOrRefresh(vault, ref _activeEquipmentHandle, AuxiliaryEquipmentVaultIds.ActiveEquipmentState, capacity, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipment);
        }

        private bool TryResolveExistingViews(out AuxiliaryVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int capacity = math.clamp(deploymentCapacity, 64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries);
            return TryResolveExisting(vault, in _deploymentsHandle, capacity, out views.Deployments) &&
                   TryResolveExisting(vault, in _statesHandle, capacity, out views.States) &&
                   TryResolveExisting(vault, in _tetherAnchorsHandle, capacity, out views.TetherAnchors) &&
                   TryResolveExisting(vault, in _activeCountHandle, 1, out views.ActiveCount) &&
                   TryResolveExisting(vault, in _tuningHandle, 1, out views.Tuning) &&
                   TryResolveExisting(vault, in _routeCountersHandle, capacity, out views.RouteCounters) &&
                   TryResolveExisting(vault, in _vfxMatricesHandle, capacity, out views.VfxMatrices) &&
                   TryResolveExisting(vault, in _telemetryRingHandle, AuxiliaryEquipmentConstants.TelemetryFrameCount, out views.TelemetryRing) &&
                   TryResolveExisting(vault, in _telemetryCursorHandle, 1, out views.TelemetryCursor) &&
                   TryResolveExisting(vault, in _profilesHandle, AuxiliaryEquipmentConstants.ProfileCapacity, out views.Profiles) &&
                   TryResolveExisting(vault, in _activeEquipmentHandle, capacity, out views.ActiveEquipment);
        }

        private static bool AcquireOrRefresh<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.GameplayTools, options);
            return IsHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryResolveExisting<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return IsHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryLockRuntimeBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_runtimeGuardHeld)
                return true;
            if (!vault.TryAcquireMutationGuard(RuntimeMutationGuardMask))
                return false;

            _runtimeGuardHeld = true;
            _runtimeGuardVault = vault;
            return true;
        }

        private bool TryLockTuningBuffer()
        {
            IDataVault vault = _dataVault;
            return vault != null && vault.TryAcquireMutationGuard(TuningMutationGuardMask);
        }

        private void UnlockTuningBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(TuningMutationGuardMask);
        }

        private void UnlockRuntimeBuffers()
        {
            if (!_runtimeGuardHeld)
                return;

            IDataVault vault = _runtimeGuardVault;
            _runtimeGuardHeld = false;
            _runtimeGuardVault = null;
            vault?.ReleaseMutationGuard(RuntimeMutationGuardMask);
        }

        private void ReleaseOwnedVaultHandles()
        {
            UnlockRuntimeBuffers();

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _runtimeGuardVault = null;
                return;
            }

            ReleaseHandle(vault, ref _deploymentsHandle);
            ReleaseHandle(vault, ref _statesHandle);
            ReleaseHandle(vault, ref _tetherAnchorsHandle);
            ReleaseHandle(vault, ref _activeCountHandle);
            ReleaseHandle(vault, ref _tuningHandle);
            ReleaseHandle(vault, ref _routeCountersHandle);
            ReleaseHandle(vault, ref _vfxMatricesHandle);
            ReleaseHandle(vault, ref _telemetryRingHandle);
            ReleaseHandle(vault, ref _telemetryCursorHandle);
            ReleaseHandle(vault, ref _profilesHandle);
            ReleaseHandle(vault, ref _activeEquipmentHandle);
            _runtimeGuardVault = null;
        }

        private static void ReleaseHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (!IsHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private AuxiliaryTuningDTO ResolveTuning(in AuxiliaryVaultViews views)
        {
            if (views.Tuning.IsCreated && views.Tuning.Length > 0)
            {
                AuxiliaryTuningDTO tuning = views.Tuning[0];
                if (tuning.FlareBaseLifetime > 0f)
                    return tuning;
            }

            return AuxiliaryTuningDTO.CreateDefault(ResolveQualityWeight(default));
        }

        private static float ResolveDefaultScalar(uint prefabHash, in AuxiliaryTuningDTO tuning)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.FlarePrefabHash)
                return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.FlareIntensity, 3f);
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.PingMaxRadius, 96f);
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.TetherMaxDistance, 60f);
            return 0f;
        }

        private static float ResolveQualityWeight(AuxiliaryTuningDTO tuning)
        {
            if ((tuning.Flags & AuxiliaryTuningFlags.OverrideGlobalQualityWeight) != 0u)
                return AuxiliaryEquipmentMath.Sanitize01(tuning.GlobalQualityWeight, 1f);

            float global = SignalBusRegistry.GlobalQualityWeight01;
            return math.saturate(math.select(1f, global, math.isfinite(global)));
        }

        private double3 ResolveCameraAup()
        {
            return _lastCameraAup;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool TryLatestTelemetryHasFault(NativeArray<AuxiliaryTelemetryEntry> telemetry, NativeArray<int> cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || !cursor.IsCreated || cursor.Length == 0)
                return false;

            int latest = cursor[0] - 1;
            if (latest < 0)
                return false;

            return (telemetry[latest % telemetry.Length].FaultFlags & AuxiliaryEquipmentFlags.Faulted) != 0u;
        }

        private static uint ResolveLaneDroppedSignals()
        {
            return SaturatingAdd(
                ToNonNegativeUInt(SignalBus<AuxiliaryFlareLightSignal>.DroppedLastFlush),
                ToNonNegativeUInt(SignalBus<AuxiliarySonarRequestSignal>.DroppedLastFlush),
                ToNonNegativeUInt(SignalBus<AuxiliaryTetherConnectionSignal>.DroppedLastFlush));
        }

        private static uint ResolveLaneCorruptedSignals()
        {
            return SaturatingAdd(
                ToNonNegativeUInt(SignalBus<AuxiliaryFlareLightSignal>.CorruptedSignalTotal),
                ToNonNegativeUInt(SignalBus<AuxiliarySonarRequestSignal>.CorruptedSignalTotal),
                ToNonNegativeUInt(SignalBus<AuxiliaryTetherConnectionSignal>.CorruptedSignalTotal));
        }

        private static uint ResolveLanePeakQueuedSignals()
        {
            return SaturatingAdd(
                ToNonNegativeUInt(SignalBus<AuxiliaryFlareLightSignal>.PeakQueuedLastFlush),
                ToNonNegativeUInt(SignalBus<AuxiliarySonarRequestSignal>.PeakQueuedLastFlush),
                ToNonNegativeUInt(SignalBus<AuxiliaryTetherConnectionSignal>.PeakQueuedLastFlush));
        }

        private static uint ToNonNegativeUInt(int value)
        {
            return value <= 0 ? 0u : (uint)value;
        }

        private static uint SaturatingAdd(uint a, uint b, uint c)
        {
            ulong sum = (ulong)a + b + c;
            return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
        }

        private unsafe void TryDumpTelemetry(NativeArray<AuxiliaryTelemetryEntry> telemetry)
        {
            if (_dumpWritten || !telemetry.IsCreated || telemetry.Length == 0)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<AuxiliaryTelemetryEntry>();
                const string dumpPayloadLabel = "AuxiliaryTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(AuxiliaryEquipmentRouterRuntime),
                    dumpPayloadLabel);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, source, byteCount);

                _dumpWritten = NativeFaultDumpWriter.TryWriteAll(DumpPath, payload, byteCount);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                const string dumpPayloadLabel = "AuxiliaryTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(AuxiliaryEquipmentRouterRuntime),
                    dumpPayloadLabel);
            }
        }

        private ref struct AuxiliaryVaultViews
        {
            public NativeArray<DeployedAuxiliaryDTO> Deployments;
            public NativeArray<AuxiliaryStateDTO> States;
            public NativeArray<AuxiliaryTetherAnchorDTO> TetherAnchors;
            public NativeArray<int> ActiveCount;
            public NativeArray<AuxiliaryTuningDTO> Tuning;
            public NativeArray<AuxiliaryRouteCounterDTO> RouteCounters;
            public NativeArray<AuxiliaryVfxMatrixDTO> VfxMatrices;
            public NativeArray<AuxiliaryTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
            public NativeArray<AuxiliaryProfileDTO> Profiles;
            public NativeArray<AuxiliaryActiveEquipmentDTO> ActiveEquipment;
        }
    }
}
