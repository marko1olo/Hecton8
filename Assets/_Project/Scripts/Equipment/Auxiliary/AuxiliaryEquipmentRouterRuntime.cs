using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Equipment.Auxiliary
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Equipment/Auxiliary Equipment Router Runtime")]
    public sealed class AuxiliaryEquipmentRouterRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable
    {
        private const int JobBatchSize = 64;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_229.bin";
        private static readonly double s_timestampToMicroseconds = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;

        [SerializeField, Range(64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries)]
        private int deploymentCapacity = AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries;

        [SerializeField] private bool registerWithDispatcher = true;
        [SerializeField] private bool seedMockDataOnColdBoot;

        private static AuxiliaryEquipmentRouterRuntime s_activeRuntime;

        private IDataVault _dataVault;
        private VaultGenerationHandle<DeployedAuxiliaryDTO> _deploymentsHandle;
        private VaultGenerationHandle<AuxiliaryStateDTO> _statesHandle;
        private VaultGenerationHandle<int> _activeCountHandle;
        private VaultGenerationHandle<AuxiliaryTuningDTO> _tuningHandle;
        private VaultGenerationHandle<AuxiliaryRouteCounterDTO> _routeCountersHandle;
        private VaultGenerationHandle<AuxiliaryVfxMatrixDTO> _vfxMatricesHandle;
        private VaultGenerationHandle<AuxiliaryTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<AuxiliaryProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<ActiveEquipmentDTO> _activeEquipmentHandle;

        private JobHandle _pendingHandle;
        private long _pendingStartTicks;
        private double3 _lastCameraAup;
        private double3 _lastTetherAnchorAup;
        private float _lastCadenceHz = AuxiliaryEquipmentConstants.MaximumCadenceHz;
        private float _lastQualityWeight = 1f;
        private uint _frameIndex;
        private bool _jobActive;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _buffersReady;
        private bool _signalLanesReady;
        private bool _mockSeeded;
        private bool _dumpWritten;

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

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            aup = resolvedAup.ToAbsoluteDouble3();
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
                   runtime.TryDeployAup(AuxiliaryEquipmentConstants.FlarePrefabHash, aup, lifetimeSeconds, 0f);
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
                   runtime.TryDeployAup(AuxiliaryEquipmentConstants.SensorPingPrefabHash, aup, lifetimeSeconds, maxRadiusMeters);
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
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime))
                return false;

            runtime._lastTetherAnchorAup = anchorAup;
            return runtime.TryDeployAup(AuxiliaryEquipmentConstants.GravityTetherPrefabHash, projectileAup, lifetimeSeconds, 0f);
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
                !runtime.TryResolveViews(out AuxiliaryVaultViews views) ||
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

        public static bool TryReadDeployments(out NativeArray<DeployedAuxiliaryDTO> deployments, out int activeCount)
        {
            deployments = default;
            activeCount = 0;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                !runtime.TryResolveViews(out AuxiliaryVaultViews views) ||
                !views.Deployments.IsCreated)
            {
                return false;
            }

            deployments = views.Deployments;
            activeCount = runtime.ResolveActiveBound(views);
            return true;
        }

        public static bool TryReadTuning(out AuxiliaryTuningDTO tuning)
        {
            tuning = default;
            if (!TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime) ||
                !runtime.TryResolveViews(out AuxiliaryVaultViews views) ||
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
                !runtime.TryResolveViews(out AuxiliaryVaultViews views) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length == 0)
            {
                return false;
            }

            views.Tuning[0] = tuning;
            return true;
        }

        public bool GenerateMockDeployments()
        {
            if (_jobActive || !EnsureRuntimeReady())
                return false;

            if (!TryResolveViews(out AuxiliaryVaultViews views) || !TryLockRuntimeBuffers())
                return false;

            try
            {
                AuxiliaryTuningDTO tuning = ResolveTuning(views);
                double3 origin = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
                GenerateMockAuxiliaryDeploymentsJob job = new GenerateMockAuxiliaryDeploymentsJob
                {
                    Deployments = views.Deployments,
                    States = views.States,
                    ActiveEquipment = views.ActiveEquipment,
                    ActiveCount = views.ActiveCount,
                    Tuning = tuning,
                    OriginAup = origin,
                    RequestedCount = math.min(AuxiliaryEquipmentConstants.MockDeploymentCount, deploymentCapacity),
                    FrameIndex = _frameIndex
                };

                JobHandle mockHandle = job.Schedule(deploymentCapacity, JobBatchSize);
                H8Memory.RegisterActiveJob(SystemID.GameplayTools, mockHandle);
                // COLD SYNC JOB: cold mock deployments must exist before the first auxiliary runtime tick consumes them.
                DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true);
                return true;
            }
            finally
            {
                UnlockRuntimeBuffers();
            }
        }

        private void OnEnable()
        {
            s_activeRuntime = this;
            _dataVault = GlobalRegistry.DataVault;
            EnsureSignalLanes();
            EnsureRuntimeReady();
            if (!registerWithDispatcher)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            CompletePendingJobForTeardown();
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

            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            ReleaseOwnedVaultHandles();
            _buffersReady = false;
        }

        public void Tick(float deltaTime)
        {
            if (_jobActive || !EnsureRuntimeReady())
                return;

            if (seedMockDataOnColdBoot && !_mockSeeded)
            {
                _mockSeeded = GenerateMockDeployments();
                return;
            }

            if (!TryResolveViews(out AuxiliaryVaultViews views) || !TryLockRuntimeBuffers())
                return;

            AuxiliaryTuningDTO tuning = ResolveTuning(views);
            _lastQualityWeight = ResolveQualityWeight(tuning);
            _lastCadenceHz = AuxiliaryEquipmentMath.ResolveCadenceHz(_lastQualityWeight, in tuning);
            _lastCameraAup = ResolveCameraAup();

            UpdateDeployedAuxiliaryJob updateJob = new UpdateDeployedAuxiliaryJob
            {
                Deployments = views.Deployments,
                States = views.States,
                ActiveEquipment = views.ActiveEquipment,
                RouteCounters = views.RouteCounters,
                ActiveCount = views.ActiveCount,
                FlareWriter = SignalBus<AuxiliaryFlareLightSignal>.ParallelWriter,
                SonarWriter = SignalBus<AuxiliarySonarRequestSignal>.ParallelWriter,
                TetherWriter = SignalBus<AuxiliaryTetherConnectionSignal>.ParallelWriter,
                Tuning = tuning,
                TetherAnchorAup = _lastTetherAnchorAup,
                FrameIndex = _frameIndex,
                SimulationDeltaTime = deltaTime,
                GlobalQualityWeight = _lastQualityWeight
            };

            StageAuxiliaryVFXJob vfxJob = new StageAuxiliaryVFXJob
            {
                Deployments = views.Deployments,
                States = views.States,
                ActiveCount = views.ActiveCount,
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
        }

        public void LateFrameTick()
        {
            TryFinalizePendingJobNoWait();
        }

        private bool TryDeployAup(uint prefabHash, double3 aup, float lifetimeSeconds, float scalar0)
        {
            if (_jobActive || !math.all(math.isfinite(aup)) || !EnsureRuntimeReady() || !TryResolveViews(out AuxiliaryVaultViews views))
                return false;

            if (!TryLockRuntimeBuffers())
                return false;

            try
            {
                AuxiliaryTuningDTO tuning = ResolveTuning(views);
                float baseLifetime = lifetimeSeconds > 0f ? lifetimeSeconds : AuxiliaryEquipmentMath.ResolveBaseLifetime(prefabHash, in tuning);
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
            if (_jobActive || prefabHash == 0u || !math.all(math.isfinite(aup)) || !EnsureRuntimeReady() || !TryResolveViews(out AuxiliaryVaultViews views))
                return false;

            if (!TryLockRuntimeBuffers())
                return false;

            try
            {
                double radius = math.max(0.01f, radiusMeters);
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

            DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
            FinalizeCompletedPendingJob();
        }

        private void FinalizeCompletedPendingJob()
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _pendingStartTicks;
            float cpuMicroseconds = (float)(elapsedTicks * s_timestampToMicroseconds);
            _jobActive = false;
            _frameIndex = _frameIndex == uint.MaxValue ? 1u : _frameIndex + 1u;

            if (TryResolveViews(out AuxiliaryVaultViews views))
            {
                CompactActiveCount(views);
                RecordAuxiliaryTelemetryJob telemetryJob = new RecordAuxiliaryTelemetryJob
                {
                    Deployments = views.Deployments,
                    RouteCounters = views.RouteCounters,
                    TelemetryRing = views.TelemetryRing,
                    TelemetryCursor = views.TelemetryCursor,
                    ActiveCount = views.ActiveCount,
                    FrameIndex = _frameIndex,
                    EffectiveCadenceHz = _lastCadenceHz,
                    CpuMicroseconds = cpuMicroseconds,
                    GlobalQualityWeight = _lastQualityWeight
                };
                telemetryJob.Execute();
                if (cpuMicroseconds > AuxiliaryEquipmentConstants.FaultDumpThresholdMicroseconds ||
                    TryLatestTelemetryHasFault(views.TelemetryRing, views.TelemetryCursor))
                {
                    TryDumpTelemetry(views.TelemetryRing);
                }
            }

            UnlockRuntimeBuffers();
        }

        private bool EnsureRuntimeReady()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_dataVault == null)
                return false;

            EnsureSignalLanes();
            if (_buffersReady && TryResolveViews(out _))
                return true;

            bool ok = TryResolveViews(out AuxiliaryVaultViews views);
            if (!ok)
                return false;

            if (views.Tuning.IsCreated && views.Tuning.Length > 0 && views.Tuning[0].FlareBaseLifetime <= 0f)
                views.Tuning[0] = AuxiliaryTuningDTO.CreateDefault(ResolveQualityWeight(default));

            _buffersReady = true;
            return true;
        }

        private void EnsureSignalLanes()
        {
            if (_signalLanesReady)
                return;
            if (GlobalRegistry.DataVault == null && !GlobalDataVault.TryGetLatestCreated(out _))
                return;

            SignalBus<AuxiliaryFlareLightSignal>.Configure(256, 2048, 64, AuxiliaryEquipmentConstants.FlareLightLaneHash);
            SignalBus<AuxiliarySonarRequestSignal>.Configure(256, 2048, 32, AuxiliaryEquipmentConstants.SensorPingLaneHash);
            SignalBus<AuxiliaryTetherConnectionSignal>.Configure(128, 1024, 16, AuxiliaryEquipmentConstants.TetherLaneHash);
            SignalBus<AuxiliaryFlareLightSignal>.EnsureInitialized();
            SignalBus<AuxiliarySonarRequestSignal>.EnsureInitialized();
            SignalBus<AuxiliaryTetherConnectionSignal>.EnsureInitialized();
            _signalLanesReady = true;
        }

        private bool TryResolveViews(out AuxiliaryVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int capacity = math.clamp(deploymentCapacity, 64, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries);
            return TryResolveOrAcquire(vault, ref _deploymentsHandle, AuxiliaryEquipmentVaultIds.Deployments, capacity, NativeArrayOptions.UninitializedMemory, out views.Deployments) &&
                   TryResolveOrAcquire(vault, ref _statesHandle, AuxiliaryEquipmentVaultIds.States, capacity, NativeArrayOptions.UninitializedMemory, out views.States) &&
                   TryResolveOrAcquire(vault, ref _activeCountHandle, AuxiliaryEquipmentVaultIds.ActiveCount, 1, NativeArrayOptions.ClearMemory, out views.ActiveCount) &&
                   TryResolveOrAcquire(vault, ref _tuningHandle, AuxiliaryEquipmentVaultIds.Tuning, 1, NativeArrayOptions.ClearMemory, out views.Tuning) &&
                   TryResolveOrAcquire(vault, ref _routeCountersHandle, AuxiliaryEquipmentVaultIds.RouteCounters, capacity, NativeArrayOptions.UninitializedMemory, out views.RouteCounters) &&
                   TryResolveOrAcquire(vault, ref _vfxMatricesHandle, AuxiliaryEquipmentVaultIds.VfxMatrices, capacity, NativeArrayOptions.UninitializedMemory, out views.VfxMatrices) &&
                   TryResolveOrAcquire(vault, ref _telemetryRingHandle, AuxiliaryEquipmentVaultIds.TelemetryRing, AuxiliaryEquipmentConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out views.TelemetryRing) &&
                   TryResolveOrAcquire(vault, ref _telemetryCursorHandle, AuxiliaryEquipmentVaultIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory, out views.TelemetryCursor) &&
                   TryResolveOrAcquire(vault, ref _profilesHandle, AuxiliaryEquipmentVaultIds.Profiles, AuxiliaryEquipmentConstants.ProfileCapacity, NativeArrayOptions.UninitializedMemory, out views.Profiles) &&
                   TryResolveOrAcquire(vault, ref _csvScratchHandle, AuxiliaryEquipmentVaultIds.CsvScratch, AuxiliaryEquipmentConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out views.CsvScratch) &&
                   TryResolveOrAcquire(vault, ref _activeEquipmentHandle, AuxiliaryEquipmentVaultIds.ActiveEquipmentState, capacity, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipment);
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, SystemID.GameplayTools, options);
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

            if (!vault.TryLockBuffer(AuxiliaryEquipmentVaultIds.Deployments, SystemID.GameplayTools) ||
                !vault.TryLockBuffer(AuxiliaryEquipmentVaultIds.States, SystemID.GameplayTools) ||
                !vault.TryLockBuffer(AuxiliaryEquipmentVaultIds.RouteCounters, SystemID.GameplayTools) ||
                !vault.TryLockBuffer(AuxiliaryEquipmentVaultIds.VfxMatrices, SystemID.GameplayTools) ||
                !vault.TryLockBuffer(AuxiliaryEquipmentVaultIds.ActiveEquipmentState, SystemID.GameplayTools))
            {
                UnlockRuntimeBuffers();
                return false;
            }

            return true;
        }

        private void UnlockRuntimeBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            vault.TryUnlockBuffer(AuxiliaryEquipmentVaultIds.Deployments, SystemID.GameplayTools);
            vault.TryUnlockBuffer(AuxiliaryEquipmentVaultIds.States, SystemID.GameplayTools);
            vault.TryUnlockBuffer(AuxiliaryEquipmentVaultIds.RouteCounters, SystemID.GameplayTools);
            vault.TryUnlockBuffer(AuxiliaryEquipmentVaultIds.VfxMatrices, SystemID.GameplayTools);
            vault.TryUnlockBuffer(AuxiliaryEquipmentVaultIds.ActiveEquipmentState, SystemID.GameplayTools);
        }

        private void ReleaseOwnedVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseHandle(vault, ref _deploymentsHandle);
            ReleaseHandle(vault, ref _statesHandle);
            ReleaseHandle(vault, ref _activeCountHandle);
            ReleaseHandle(vault, ref _tuningHandle);
            ReleaseHandle(vault, ref _routeCountersHandle);
            ReleaseHandle(vault, ref _vfxMatricesHandle);
            ReleaseHandle(vault, ref _telemetryRingHandle);
            ReleaseHandle(vault, ref _telemetryCursorHandle);
            ReleaseHandle(vault, ref _profilesHandle);
            ReleaseHandle(vault, ref _csvScratchHandle);
            _activeEquipmentHandle = default;
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
                return tuning.FlareIntensity;
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return tuning.PingMaxRadius;
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return tuning.TetherMaxDistance;
            return 0f;
        }

        private static float ResolveQualityWeight(AuxiliaryTuningDTO tuning)
        {
            float overrideWeight = tuning.GlobalQualityWeight;
            if (math.isfinite(overrideWeight) && overrideWeight > 0f)
                return math.saturate(overrideWeight);

            float global = HomeostasisBrain.GlobalQualityWeight;
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

        private unsafe void TryDumpTelemetry(NativeArray<AuxiliaryTelemetryEntry> telemetry)
        {
            if (_dumpWritten || !telemetry.IsCreated || telemetry.Length == 0)
                return;

            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, DumpPath.Replace('/', Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<AuxiliaryTelemetryEntry>();
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                }

                _dumpWritten = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private struct AuxiliaryVaultViews
        {
            public NativeArray<DeployedAuxiliaryDTO> Deployments;
            public NativeArray<AuxiliaryStateDTO> States;
            public NativeArray<int> ActiveCount;
            public NativeArray<AuxiliaryTuningDTO> Tuning;
            public NativeArray<AuxiliaryRouteCounterDTO> RouteCounters;
            public NativeArray<AuxiliaryVfxMatrixDTO> VfxMatrices;
            public NativeArray<AuxiliaryTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
            public NativeArray<AuxiliaryProfileDTO> Profiles;
            public NativeArray<byte> CsvScratch;
            public NativeArray<ActiveEquipmentDTO> ActiveEquipment;
        }
    }
}
