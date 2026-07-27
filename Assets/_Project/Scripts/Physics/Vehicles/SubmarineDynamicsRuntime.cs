using System;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics.Vehicles
{
    public sealed partial class SubmarineDynamicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IColdTickable, ILateFrameTickable, ISlowTickable, IVehicleCommandSignalListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_SubmarineDynamicsRuntime;

        private const int MockSignalCapacity = 64;
        private const int SurvivalMockSignalCapacity = 8;
        private const long MaxCsvOverrideBytes = 4096L;
        private const uint HashBaseMassKg = 0xA5F7F6FCu;
        private const uint HashDragScale = 0x681E390Eu;
        private const uint HashPidP = 0x9D6F7115u;
        private const uint HashPidI = 0x946F62EAu;
        private const uint HashPidD = 0x896F5199u;
        private const uint HashGyroStrength = 0x3FE75EBEu;
        private const uint HashHullVolumeM3 = 0xB0DE050Eu;
        private const uint HashHullLengthM = 0x88BFE97Bu;
        private const uint HashHullRadiusM = 0x379DD5DDu;
        private const uint HashAddedMassMultiplier = 0x0EA616D0u;
        private const uint HashFloodVolumeScalar = 0xE0BF9C4Fu;
        private const uint HashTargetDepthM = 0xA4492116u;
        private static readonly ulong SimulationCoreMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicStates) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicControls) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicPidStates) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicMassProperties) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicForces) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicTelemetry) |
            VaultMutationGuardBit(BufferID.Shinobu251AddedMassProfiles) |
            VaultMutationGuardBit(BufferID.Shinobu251HydrodynamicsTelemetry) |
            VaultMutationGuardBit(BufferID.Shinobu251HullProfiles) |
            VaultMutationGuardBit(BufferID.Shinobu251AddedMassTuning) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicDragLut) |
            VaultMutationGuardBit(BufferID.SubmarineKinematicConfig);
        private static readonly ulong BootConfigMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicConfig);
        private static readonly ulong BootDragLutMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicDragLut);
        private static readonly ulong BootStateMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicStates);
        private static readonly ulong BootControlMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicControls);
        private static readonly ulong BootMassMutationGuardMask =
            VaultMutationGuardBit(BufferID.SubmarineKinematicMassProperties);
        private static readonly ulong BootHullProfileMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu251HullProfiles);
        private const uint HashMaxThrustN = 0x6DDC6935u;
        private const uint HashBallastLiftN = 0xDBC90E8Du;
        private const uint HashSloshSpring = 0x3466D6C8u;
        private const uint HashSloshDamping = 0x96934799u;
        private const uint CavitationSourceId = SubmarineDynamicsConstants.SourceHashAddedMass; // AM25
        private const uint EmergencyProfileFallbackWarningHash = 0x534D4B50u; // SMKP - submarine mock profile
        private const uint SubmarineDynamicsFaultEventHash = 0x53444654u; // SDFT
        private const uint SubmarineDynamicsFaultDumpHash = 0x53444450u; // SDDP
        private const uint SubmarineGyroFaultEventHash = 0x47334654u; // G3FT
        private const uint SubmarineGyroFaultDumpHash = 0x47334450u; // G3DP

        [Header("Vault Lane")]
        [SerializeField, Range(1, SubmarineDynamicsConstants.MaxVehicles)] private int vehicleCapacity = 1;
        [SerializeField] private Transform visualRoot;

        [Header("Authored Hull Data")]
        [Tooltip("Row id in Data/Balance/SubmarineHull.csv, baked into Data Monolith section 14. " +
                 "Boot reads mass and drag from that record instead of the serialized fallbacks below. " +
                 "Blank disables the static-data route and boots on the fallback profile.")]
        [SerializeField] private string hullPartId = "starter_sub_hull";

        [Header("Mock Profile")]
        [SerializeField] private bool enableMockSignals;
        [SerializeField, Min(1f)] private float baseMassKg = 18000f;
        [SerializeField, Min(1f)] private float hullVolumeM3 = 22f;
        [SerializeField, Min(0f)] private float targetDepthMeters = 35f;
        [SerializeField, Range(0f, 1f)] private float defaultThrottle01;
        [SerializeField, Range(0f, 1f)] private float defaultBallast01 = 0.5f;
        [SerializeField] private Vector3 centerOfBuoyancyLocal = new Vector3(0f, 0.75f, 0f);
        [SerializeField] private Vector3 mockFloodLocal = new Vector3(0f, -0.25f, -3.2f);

        [Header("Forces")]
        [SerializeField, Min(0f)] private float maxThrustN = 52000f;
        [SerializeField, Min(0f)] private float maxTorqueNm = 18000f;
        [SerializeField, Min(0f)] private float ballastLiftN = 140000f;
        [SerializeField, Min(0f)] private float ballastFillRatePerSec = 0.2f;
        [SerializeField, Min(0f)] private float ballastVentRatePerSec = 0.3f;
        [SerializeField, Min(0f)] private float dragScale = 1f;

        [Header("Stability")]
        [SerializeField, Min(0f)] private float pidP = 9000f;
        [SerializeField, Min(0f)] private float pidI = 1200f;
        [SerializeField, Min(0f)] private float pidD = 6400f;
        [SerializeField, Min(0f)] private float gyroStrength = 45000f;
        [SerializeField, Min(0f)] private float gyroDamping = 9000f;
        [SerializeField, Min(0f)] private float sloshSpring = 8f;
        [SerializeField, Min(0f)] private float sloshDamping = 2.5f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<SubmarineKinematicState> _stateHandle;
        private VaultGenerationHandle<SubmarineKinematicControl> _controlHandle;
        private VaultGenerationHandle<SubmarinePidState> _pidHandle;
        private VaultGenerationHandle<SubmarineMassProperties> _massHandle;
        private VaultGenerationHandle<SubmarineForceAccumulator> _forceHandle;
        private VaultGenerationHandle<SubmarineKinematicTelemetry> _telemetryHandle;
        private VaultGenerationHandle<AddedMassProfileDTO> _addedMassHandle;
        private VaultGenerationHandle<SubmarineHydrodynamicsTelemetry> _hydrodynamicsTelemetryHandle;
        private VaultGenerationHandle<SubmarineHullProfileDTO> _hullProfileHandle;
        private VaultGenerationHandle<SubmarineAddedMassTuningDTO> _addedMassTuningHandle;
        private VaultGenerationHandle<SubmarineKinematicConfig> _configHandle;
        private VaultGenerationHandle<float> _dragLutHandle;
        private VaultGenerationHandle<VehicleDamageStateDTO> _vehicleDamageStateReadHandle;
        private JobHandle _integratorHandle;
        private bool _integratorPending;
        private bool _buffersLocked;
        private IDataVault _simulationGuardVault;
        private ulong _simulationGuardMask;
        private bool _buffersReady;
        private bool _reportedEmergencyProfileFallback;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredCold;
        private bool _registeredLateFrame;
        private bool _registeredSlow;
        private bool _registeredHotSwapListener;
        private bool _dumpWritten;
        private bool _coreBlackboxWarmed;
        private bool _hasPendingVehicleCommand;
        private int _droppedSignalCount;
        private uint _frameCounter;
        private long _hydrodynamicsScheduleTicks;
        private int _commandTargetInstanceId;
        private int _visualCommandTargetInstanceId;
        private uint _primaryVehicleEntityHash;
        private float _fluidDensityMultiplier = 1f;
        private VehicleCommandSignal _pendingVehicleCommand;
#if UNITY_EDITOR
        private long _csvLastWriteTicks;
        private long _hullProfilesCsvLastWriteTicks;
        private string _projectRoot;
        private string _csvPath;
        private string _hullProfilesCsvPath;
#endif

        public int DroppedSignalCount => _droppedSignalCount;

        public static bool TryGetLatest(out SubmarineDynamicsRuntime runtime)
        {
            runtime = _latest;
            return runtime != null && runtime._buffersReady;
        }

        public static bool TryGetActiveGyroRouteForEntity(uint entityHash)
        {
            SubmarineDynamicsRuntime runtime = _latest;
            return entityHash != 0u &&
                   runtime != null &&
                   runtime._buffersReady &&
                   runtime.MatchesGyroRouteTarget(entityHash);
        }

        private static SubmarineDynamicsRuntime _latest;

        private void OnEnable()
        {
            _latest = this;
            _droppedSignalCount = 0;
#if UNITY_EDITOR
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "sub_physics_overrides.csv");
            _hullProfilesCsvPath = Path.Combine(_projectRoot, "Data", "Physics", "vehicle_hull_profiles.csv");
            InitializeGyroRuntimePaths();
#endif
            EnsureSignalLanes();
            RefreshCommandTargetIds();
            VehicleCommandSignalBus.Register(this);
            TryRegisterHotSwapListener();
            CacheDataVaultCold();
            EnsureVaultBuffers();
            WarmCoreBlackboxRoute();
#if UNITY_EDITOR
            TryApplyCsvOverrides();
            TryApplyHullProfilesCsv();
            TryApplyGyroProfilesCsv();
#endif

            TryRegisterRuntimeLanes();
        }

        private void OnDisable()
        {
            CompleteIntegratorForLifecycle();
            VehicleCommandSignalBus.Unregister(this);
            DumpBlackBoxIfFaulted();
            DumpGyroBlackBoxIfFaulted();
            DisposeGyroRuntime();

            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();
            _coreBlackboxWarmed = false;
            if (ReferenceEquals(_latest, this))
                _latest = null;
        }

        private void OnDestroy()
        {
            CompleteIntegratorForLifecycle();
            DisposeGyroRuntime();
            ReleaseOwnedVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = null;
            _buffersReady = false;

            if (ReferenceEquals(_latest, this))
                _latest = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterRuntimeLanes();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterRuntimeLanes();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVaultForLifecycle(currentService as IDataVault);
            if (isActiveAndEnabled && _dataVault != null)
            {
                EnsureVaultBuffers();
                WarmCoreBlackboxRoute();
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_integratorPending)
                return;

            if (!_buffersReady)
                return;

            if (!LockSimulationBuffers())
                return;

            if (!TryResolveArrays(
                    out NativeArray<SubmarineKinematicState> states,
                    out NativeArray<SubmarineKinematicControl> controls,
                    out NativeArray<SubmarinePidState> pidStates,
                    out NativeArray<SubmarineMassProperties> masses,
                    out NativeArray<SubmarineForceAccumulator> forces,
                    out NativeArray<SubmarineKinematicTelemetry> telemetry,
                    out NativeArray<AddedMassProfileDTO> addedMassProfiles,
                    out NativeArray<SubmarineHydrodynamicsTelemetry> hydrodynamicsTelemetry,
                    out NativeArray<SubmarineHullProfileDTO> hullProfiles,
                    out NativeArray<SubmarineAddedMassTuningDTO> addedMassTuning,
                    out NativeArray<SubmarineKinematicConfig> configs,
                    out NativeArray<float> dragLut))
            {
                _buffersReady = false;
                UnlockSimulationBuffers();
                return;
            }

            if (!TryApplyPreScheduleSignals(controls, masses, forces, configs, fixedDeltaTime, out SubmarineKinematicConfig frameConfig))
            {
                UnlockSimulationBuffers();
                return;
            }

            uint frame = ++_frameCounter;
            float quality = ResolveMathLodQualityWeight();
            _hydrodynamicsScheduleTicks = Stopwatch.GetTimestamp();
            if (enableMockSignals)
                TryPushMockFloodSignal(frame);

            CalculateAddedMassTensorJob addedMassJob = new CalculateAddedMassTensorJob
            {
                States = states,
                MassProperties = masses,
                Config = frameConfig,
                HullProfiles = hullProfiles,
                Tuning = addedMassTuning,
                AddedMassProfiles = addedMassProfiles,
                HydrodynamicsTelemetry = hydrodynamicsTelemetry,
                GlobalQualityWeight = quality,
                Frame = frame,
                VehicleCount = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles)
            };
            JobHandle addedMassHandle = addedMassJob.Schedule(addedMassJob.VehicleCount, SubmarineDynamicsConstants.IntegratorBatchSize);
            JobHandle gyroHandle = ScheduleGyroPipeline(
                states,
                forces,
                addedMassProfiles,
                addedMassTuning,
                fixedDeltaTime,
                quality,
                frame,
                addedMassHandle);

            Submarine6DIntegratorJob integratorJob = new Submarine6DIntegratorJob
            {
                States = states,
                Controls = controls,
                PidStates = pidStates,
                MassProperties = masses,
                Forces = forces,
                Telemetry = telemetry,
                AddedMassProfiles = addedMassProfiles,
                Tuning = addedMassTuning,
                Config = frameConfig,
                DragLut = dragLut,
                CavitationWriter = SignalBus<CavitationAcousticSignal>.ParallelWriter,
                CavitationWriterBudget = SignalBus<CavitationAcousticSignal>.ParallelWriterBudget,
                FixedDeltaTime = fixedDeltaTime,
                GlobalQualityWeight = quality,
                Frame = frame,
                VehicleCount = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles)
            };

            _integratorHandle = integratorJob.Schedule(integratorJob.VehicleCount, SubmarineDynamicsConstants.IntegratorBatchSize, gyroHandle);
            H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _integratorHandle);
            _integratorPending = true;
        }

        private void TryPushMockFloodSignal(uint frame)
        {
            const float probability = 1f / 16f;
            uint hash = MixFrameHash(0x5EED110Bu, frame);
            if (Hash01(hash) > probability)
                return;

            MockFloodSignal signal = default;
            signal.LocalCompartment = (float3)(mockFloodLocal);
            signal.WaterMassKg = 1200f;
            signal.FillRatio01 = math.saturate(signal.WaterMassKg / 4000f);
            signal.Frame = frame;
            signal.Flags = 1;
            if (!SignalBus<MockFloodSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SubmarineDynamicsRuntime))
                IncrementDroppedSignalCount();
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_integratorPending)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _integratorHandle))
                return;

            _integratorPending = false;
            PatchHydrodynamicsElapsedMicros(ResolveElapsedMicros(_hydrodynamicsScheduleTicks));
            PatchGyroElapsedMicros(ResolveElapsedMicros(_gyroScheduleTicks));
            UnlockSimulationBuffers();
            DrainCavitationSignals();
            bool faulted = DumpBlackBoxIfFaulted();
            bool gyroFaulted = DumpGyroBlackBoxIfFaulted();
            if (!faulted && !gyroFaulted)
                RecordVaultSovereigntyTelemetry(0u);
        }

        public void LateFrameTick()
        {
            if (_integratorPending || !_buffersReady || _dataVault == null)
                return;

            if (!TryReadOnlyVaultHandle(in _stateHandle, out NativeArray<SubmarineKinematicState>.ReadOnly states) || states.Length == 0)
                return;

            SubmarineKinematicState state = states[0];
            Transform target = visualRoot != null ? visualRoot : transform;
            Quaternion rotation = new Quaternion(state.Rotation.value.x, state.Rotation.value.y, state.Rotation.value.z, state.Rotation.value.w);
            target.SetPositionAndRotation(new Vector3(state.LocalPosition.x, state.LocalPosition.y, state.LocalPosition.z), rotation);
            SyncGyroVisualBuffer();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform target = visualRoot != null ? visualRoot : transform;
            float3 axisScale = ResolveEditorTensorAxisScale();
            Vector3 origin = target.position;
            Quaternion rotation = target.rotation;
            TryResolveEditorKinematicPose(ref origin, ref rotation);
            Vector3 forward = rotation * (Vector3.forward * axisScale.z);
            Vector3 right = rotation * (Vector3.right * axisScale.x);
            Vector3 up = rotation * (Vector3.up * axisScale.y);

            Gizmos.color = new Color(0.15f, 0.55f, 1f, 0.85f);
            Gizmos.DrawLine(origin - forward, origin + forward);
            Gizmos.color = new Color(0.95f, 0.95f, 0.25f, 0.85f);
            Gizmos.DrawLine(origin - right, origin + right);
            Gizmos.color = new Color(0.25f, 1f, 0.65f, 0.85f);
            Gizmos.DrawLine(origin - up, origin + up);
            Gizmos.color = new Color(0.15f, 0.55f, 1f, 0.18f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(origin, rotation, new Vector3(axisScale.x, axisScale.y, axisScale.z));
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.matrix = previousMatrix;
            DrawGyroDebugGizmos();
        }

        private bool TryResolveEditorKinematicPose(ref Vector3 origin, ref Quaternion rotation)
        {
            if (!_buffersReady || _integratorPending || _buffersLocked || _dataVault == null)
                return false;

            if (!TryReadOnlyVaultHandle(in _stateHandle, out NativeArray<SubmarineKinematicState>.ReadOnly states) || states.Length == 0)
                return false;

            SubmarineKinematicState state = states[0];
            if (!math.all(math.isfinite(state.LocalPosition)) || !math.all(math.isfinite(state.Rotation.value)))
                return false;

            origin = new Vector3(state.LocalPosition.x, state.LocalPosition.y, state.LocalPosition.z);
            quaternion safeRotation = SubmarineAddedMassMath.NormalizeSafe(state.Rotation);
            rotation = new Quaternion(safeRotation.value.x, safeRotation.value.y, safeRotation.value.z, safeRotation.value.w);
            return true;
        }

        private float3 ResolveEditorTensorAxisScale()
        {
            float fallbackLength;
            float fallbackRadius;
            if (!_buffersReady || _integratorPending || _buffersLocked || _dataVault == null)
            {
                SubmarineAddedMassMath.ResolveHullAxes(math.max(1f, hullVolumeM3), out fallbackLength, out fallbackRadius);
                return new float3(fallbackRadius, fallbackRadius, fallbackLength * 0.5f);
            }

            if (!TryReadOnlyVaultHandle(in _addedMassHandle, out NativeArray<AddedMassProfileDTO>.ReadOnly profiles) || profiles.Length == 0)
            {
                SubmarineAddedMassMath.ResolveHullAxes(math.max(1f, hullVolumeM3), out fallbackLength, out fallbackRadius);
                return new float3(fallbackRadius, fallbackRadius, fallbackLength * 0.5f);
            }

            AddedMassProfileDTO profile = profiles[0];
            float3 diag = SubmarineAddedMassMath.ExtractDiagonal(in profile.LinearAddedMass);
            if (!math.all(math.isfinite(diag)) || math.any(diag <= 0.0001f))
            {
                SubmarineAddedMassMath.ResolveHullAxes(math.max(1f, hullVolumeM3), out fallbackLength, out fallbackRadius);
                return new float3(fallbackRadius, fallbackRadius, fallbackLength * 0.5f);
            }

            float maxDiag = math.max(diag.x, math.max(diag.y, diag.z));
            float3 normalizedSq = diag * math.rcp(math.max(maxDiag, 0.0001f));
            float3 normalized = normalizedSq * math.rsqrt(math.max(normalizedSq, new float3(0.0001f)));
            SubmarineAddedMassMath.ResolveHullAxes(math.max(1f, hullVolumeM3), out fallbackLength, out fallbackRadius);
            float baseScale = math.clamp(math.max(fallbackRadius, fallbackLength * 0.5f), 1f, 24f);
            return math.max(new float3(0.25f), normalized * baseScale);
        }
#endif

        public void SlowTick()
        {
            if (!_buffersReady)
                return;
        }

        public void ColdTick()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            EnsureDataVault();
            if (!_integratorPending && !_buffersLocked)
                EnsureVaultBuffers();
            TryRefreshVehicleDamageStateReadHandle();
            RefreshCommandTargetIds();
        }

        public void OnVehicleCommandSignal(in VehicleCommandSignal signal)
        {
            int target = signal.TargetInstanceId;
            if (target == 0)
                return;

            if (_commandTargetInstanceId != 0 &&
                target != _commandTargetInstanceId &&
                (_visualCommandTargetInstanceId == 0 || target != _visualCommandTargetInstanceId))
            {
                return;
            }

            _pendingVehicleCommand = signal;
            _hasPendingVehicleCommand = true;
        }

        private bool EnsureDataVault()
        {
            return _dataVault != null;
        }

        private void CacheDataVaultCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredCold)
                _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredCold = false;
            _registeredLateFrame = false;
            _registeredSlow = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ClearVaultHandles()
        {
            _stateHandle = default;
            _controlHandle = default;
            _pidHandle = default;
            _massHandle = default;
            _forceHandle = default;
            _telemetryHandle = default;
            _addedMassHandle = default;
            _hydrodynamicsTelemetryHandle = default;
            _hullProfileHandle = default;
            _addedMassTuningHandle = default;
            _configHandle = default;
            _dragLutHandle = default;
            _vehicleDamageStateReadHandle = default;
            ClearGyroVaultHandles();
        }

        private void CompleteIntegratorForLifecycle()
        {
            if (_integratorPending)
            {
                DispatcherJobFence.BeginPostFixedSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _integratorHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostFixedSwapWindow();
                }
            }

            _integratorPending = false;
            UnlockSimulationBuffers();
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            CompleteIntegratorForLifecycle();
            ReleaseOwnedVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = nextVault;
            _buffersReady = false;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _stateHandle);
            ReleaseOwnedVaultHandle(vault, ref _controlHandle);
            ReleaseOwnedVaultHandle(vault, ref _pidHandle);
            ReleaseOwnedVaultHandle(vault, ref _massHandle);
            ReleaseOwnedVaultHandle(vault, ref _forceHandle);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle);
            ReleaseOwnedVaultHandle(vault, ref _addedMassHandle);
            ReleaseOwnedVaultHandle(vault, ref _hydrodynamicsTelemetryHandle);
            ReleaseOwnedVaultHandle(vault, ref _hullProfileHandle);
            ReleaseOwnedVaultHandle(vault, ref _addedMassTuningHandle);
            ReleaseOwnedVaultHandle(vault, ref _configHandle);
            ReleaseOwnedVaultHandle(vault, ref _dragLutHandle);
            ReleaseGyroVaultHandles(vault);
        }

        private static void ReleaseOwnedVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.VehiclesPhysics)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private bool TryOpenVaultHandleForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return _dataVault != null && IsGenerationHandleCreated(in handle) && _dataVault.TryResolveHandle(in handle, out buffer);
        }

        private bool TryReadOnlyVaultHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return _dataVault != null && IsGenerationHandleCreated(in handle) && _dataVault.TryReadOnlyHandle(in handle, out buffer);
        }

        private bool TryAcquireVaultWriteLock<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !IsGenerationHandleCreated(in handle) ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.VehiclesPhysics, out buffer))
                {
                    return false;
                }

                lockAcquired = true;
                return buffer.IsCreated;
            }
            finally
            {
                if (lockAcquired && !buffer.IsCreated)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.VehiclesPhysics);
                    buffer = default;
                }
            }
        }

        private void ReleaseVaultWriteLock<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsGenerationHandleCreated(in handle))
                vault.ReleaseWriteLock(in handle, SystemID.VehiclesPhysics);
        }

        private static bool IsGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool IsVehiclesPhysicsHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics &&
                   handle.Generation != 0u;
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private static ulong GetSimulationMutationGuardMask()
        {
            return SimulationCoreMutationGuardMask | GyroScheduleMutationGuardMask;
        }

        private bool EnsureVaultBuffers()
        {
            if (!EnsureDataVault())
                return false;

            if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                return false;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            _stateHandle = _dataVault.EnsureGenerationHandle<SubmarineKinematicState>(BufferID.SubmarineKinematicStates, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _controlHandle = _dataVault.EnsureGenerationHandle<SubmarineKinematicControl>(BufferID.SubmarineKinematicControls, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _pidHandle = _dataVault.EnsureGenerationHandle<SubmarinePidState>(BufferID.SubmarineKinematicPidStates, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _massHandle = _dataVault.EnsureGenerationHandle<SubmarineMassProperties>(BufferID.SubmarineKinematicMassProperties, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _forceHandle = _dataVault.EnsureGenerationHandle<SubmarineForceAccumulator>(BufferID.SubmarineKinematicForces, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _dataVault.EnsureGenerationHandle<SubmarineKinematicTelemetry>(BufferID.SubmarineKinematicTelemetry, capacity * SubmarineDynamicsConstants.BlackBoxFrames, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _addedMassHandle = _dataVault.EnsureGenerationHandle<AddedMassProfileDTO>(BufferID.Shinobu251AddedMassProfiles, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _hydrodynamicsTelemetryHandle = _dataVault.EnsureGenerationHandle<SubmarineHydrodynamicsTelemetry>(BufferID.Shinobu251HydrodynamicsTelemetry, capacity * SubmarineDynamicsConstants.BlackBoxFrames, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _hullProfileHandle = _dataVault.EnsureGenerationHandle<SubmarineHullProfileDTO>(BufferID.Shinobu251HullProfiles, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _addedMassTuningHandle = _dataVault.EnsureGenerationHandle<SubmarineAddedMassTuningDTO>(BufferID.Shinobu251AddedMassTuning, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _configHandle = _dataVault.EnsureGenerationHandle<SubmarineKinematicConfig>(BufferID.SubmarineKinematicConfig, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _dragLutHandle = _dataVault.EnsureGenerationHandle<float>(BufferID.SubmarineKinematicDragLut, SubmarineDynamicsConstants.DragLutSamples, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            if (!EnsureGyroVaultBuffers(capacity))
            {
                _buffersReady = false;
                return false;
            }

            if (!IsGenerationHandleCreated(in _stateHandle) || !IsGenerationHandleCreated(in _controlHandle) || !IsGenerationHandleCreated(in _pidHandle) ||
                !IsGenerationHandleCreated(in _massHandle) || !IsGenerationHandleCreated(in _forceHandle) || !IsGenerationHandleCreated(in _telemetryHandle) ||
                !IsGenerationHandleCreated(in _addedMassHandle) || !IsGenerationHandleCreated(in _hydrodynamicsTelemetryHandle) || !IsGenerationHandleCreated(in _hullProfileHandle) ||
                !IsGenerationHandleCreated(in _addedMassTuningHandle) ||
                !IsGenerationHandleCreated(in _configHandle) || !IsGenerationHandleCreated(in _dragLutHandle))
            {
                _buffersReady = false;
                return false;
            }

            if (!TryReadOnlyVaultHandle(in _configHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly configRead) ||
                !TryReadOnlyVaultHandle(in _addedMassTuningHandle, out NativeArray<SubmarineAddedMassTuningDTO>.ReadOnly tuningRead) ||
                configRead.Length == 0 ||
                tuningRead.Length == 0)
            {
                return false;
            }

            if (tuningRead[0].SourceHash == 0u && !TryInitializeAddedMassTuning())
                return false;

            if (configRead[0].SourceHash == 0u && !TryInitializeBootProfiles())
            {
                return false;
            }

            TryRefreshVehicleDamageStateReadHandle();
            _buffersReady = true;
            return true;
        }

        private bool TryInitializeAddedMassTuning()
        {
            if (!TryAcquireVaultWriteLock(in _addedMassTuningHandle, out NativeArray<SubmarineAddedMassTuningDTO> addedMassTuning))
                return false;

            try
            {
                if (addedMassTuning.Length == 0)
                    return false;

                if (addedMassTuning[0].SourceHash == 0u)
                    addedMassTuning[0] = SubmarineAddedMassMath.DefaultTuning();

                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _addedMassTuningHandle);
            }
        }

        private bool TryInitializeBootProfiles()
        {
            if (!TryReadOnlyVaultHandle(in _configHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly configRead) ||
                configRead.Length == 0)
            {
                return false;
            }

            if (configRead[0].SourceHash != 0u)
                return true;

            SubmarineKinematicConfig config = BuildDefaultConfig();
            Span<float> dragLutScratch = stackalloc float[SubmarineDynamicsConstants.DragLutSamples];

            // The baked Data Monolith is the sanctioned route (AGENTS.md Data-Driven Configuration
            // Rule: ScriptableObject facade -> baked .h8bin -> unmanaged DTO), and it is the only
            // route that reaches a player build. Try it first. TryLoadLegacyProfiles below reads
            // loose files out of Docs/Archive and is Editor-only, so it stays a fallback for
            // projects where section 14 is empty rather than the primary source.
            bool profilesLoaded = TryApplyStaticHullConstants(ref config, dragLutScratch);
#if UNITY_EDITOR
            if (!profilesLoaded)
                profilesLoaded = TryLoadLegacyProfiles(ref config, dragLutScratch);
#endif
            if (!profilesLoaded)
            {
                GenerateEmergencyMockProfiles(ref config, dragLutScratch);
                ReportEmergencyProfileFallbackOnce();
            }

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            Span<SubmarineKinematicState> stateScratch = stackalloc SubmarineKinematicState[SubmarineDynamicsConstants.MaxVehicles];
            Span<SubmarineKinematicControl> controlScratch = stackalloc SubmarineKinematicControl[SubmarineDynamicsConstants.MaxVehicles];
            Span<SubmarineMassProperties> massScratch = stackalloc SubmarineMassProperties[SubmarineDynamicsConstants.MaxVehicles];
            Span<SubmarineHullProfileDTO> hullScratch = stackalloc SubmarineHullProfileDTO[SubmarineDynamicsConstants.MaxVehicles];
            InitializeVehicleDefaults(stateScratch, controlScratch, massScratch, hullScratch, capacity, in config);

            if (!TryCommitBootDragLut(dragLutScratch) ||
                !TryCommitBootStates(stateScratch, capacity) ||
                !TryCommitBootControls(controlScratch, capacity) ||
                !TryCommitBootMasses(massScratch, capacity) ||
                !TryCommitBootHullProfiles(hullScratch, capacity))
            {
                return false;
            }

            return TryCommitBootConfig(in config);
        }

        private bool TryCommitBootConfig(in SubmarineKinematicConfig config)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootConfigMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _configHandle, out NativeArray<SubmarineKinematicConfig> configs) ||
                    configs.Length == 0)
                {
                    return false;
                }

                configs[0] = config;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootConfigMutationGuardMask);
            }
        }

        private bool TryCommitBootDragLut(ReadOnlySpan<float> dragLutScratch)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootDragLutMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _dragLutHandle, out NativeArray<float> dragLut) ||
                    dragLut.Length == 0)
                {
                    return false;
                }

                int count = math.min(dragLut.Length, dragLutScratch.Length);
                for (int i = 0; i < count; i++)
                    dragLut[i] = dragLutScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootDragLutMutationGuardMask);
            }
        }

        private bool TryCommitBootStates(ReadOnlySpan<SubmarineKinematicState> stateScratch, int capacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootStateMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _stateHandle, out NativeArray<SubmarineKinematicState> states) ||
                    states.Length == 0)
                {
                    return false;
                }

                int count = math.min(math.min(states.Length, stateScratch.Length), capacity);
                for (int i = 0; i < count; i++)
                    states[i] = stateScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootStateMutationGuardMask);
            }
        }

        private bool TryCommitBootControls(ReadOnlySpan<SubmarineKinematicControl> controlScratch, int capacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootControlMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _controlHandle, out NativeArray<SubmarineKinematicControl> controls) ||
                    controls.Length == 0)
                {
                    return false;
                }

                int count = math.min(math.min(controls.Length, controlScratch.Length), capacity);
                for (int i = 0; i < count; i++)
                    controls[i] = controlScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootControlMutationGuardMask);
            }
        }

        private bool TryCommitBootMasses(ReadOnlySpan<SubmarineMassProperties> massScratch, int capacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootMassMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _massHandle, out NativeArray<SubmarineMassProperties> masses) ||
                    masses.Length == 0)
                {
                    return false;
                }

                int count = math.min(math.min(masses.Length, massScratch.Length), capacity);
                for (int i = 0; i < count; i++)
                    masses[i] = massScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootMassMutationGuardMask);
            }
        }

        private bool TryCommitBootHullProfiles(ReadOnlySpan<SubmarineHullProfileDTO> hullScratch, int capacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootHullProfileMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _hullProfileHandle, out NativeArray<SubmarineHullProfileDTO> hullProfiles) ||
                    hullProfiles.Length == 0)
                {
                    return false;
                }

                int count = math.min(math.min(hullProfiles.Length, hullScratch.Length), capacity);
                for (int i = 0; i < count; i++)
                    hullProfiles[i] = hullScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootHullProfileMutationGuardMask);
            }
        }

        private bool TryResolveArrays(
            out NativeArray<SubmarineKinematicState> states,
            out NativeArray<SubmarineKinematicControl> controls,
            out NativeArray<SubmarinePidState> pidStates,
            out NativeArray<SubmarineMassProperties> masses,
            out NativeArray<SubmarineForceAccumulator> forces,
            out NativeArray<SubmarineKinematicTelemetry> telemetry,
            out NativeArray<AddedMassProfileDTO> addedMassProfiles,
            out NativeArray<SubmarineHydrodynamicsTelemetry> hydrodynamicsTelemetry,
            out NativeArray<SubmarineHullProfileDTO> hullProfiles,
            out NativeArray<SubmarineAddedMassTuningDTO> addedMassTuning,
            out NativeArray<SubmarineKinematicConfig> configs,
            out NativeArray<float> dragLut)
        {
            states = default;
            controls = default;
            pidStates = default;
            masses = default;
            forces = default;
            telemetry = default;
            addedMassProfiles = default;
            hydrodynamicsTelemetry = default;
            hullProfiles = default;
            addedMassTuning = default;
            configs = default;
            dragLut = default;

            return TryOpenVaultHandleForOwner(in _stateHandle, out states) &&
                   TryOpenVaultHandleForOwner(in _controlHandle, out controls) &&
                   TryOpenVaultHandleForOwner(in _pidHandle, out pidStates) &&
                   TryOpenVaultHandleForOwner(in _massHandle, out masses) &&
                   TryOpenVaultHandleForOwner(in _forceHandle, out forces) &&
                   TryOpenVaultHandleForOwner(in _telemetryHandle, out telemetry) &&
                   TryOpenVaultHandleForOwner(in _addedMassHandle, out addedMassProfiles) &&
                   TryOpenVaultHandleForOwner(in _hydrodynamicsTelemetryHandle, out hydrodynamicsTelemetry) &&
                   TryOpenVaultHandleForOwner(in _hullProfileHandle, out hullProfiles) &&
                   TryOpenVaultHandleForOwner(in _addedMassTuningHandle, out addedMassTuning) &&
                   TryOpenVaultHandleForOwner(in _configHandle, out configs) &&
                   TryOpenVaultHandleForOwner(in _dragLutHandle, out dragLut);
        }

        private bool TryApplyPreScheduleSignals(
            NativeArray<SubmarineKinematicControl> controls,
            NativeArray<SubmarineMassProperties> masses,
            NativeArray<SubmarineForceAccumulator> forces,
            NativeArray<SubmarineKinematicConfig> configs,
            float fixedDeltaTime,
            out SubmarineKinematicConfig frameConfig)
        {
            frameConfig = default;
            if (controls.Length == 0 ||
                masses.Length == 0 ||
                forces.Length == 0 ||
                configs.Length == 0)
            {
                _buffersReady = false;
                return false;
            }

            VehicleCommandSignalBus.FlushPending();
            ConsumeSignals(controls, masses, forces, configs, fixedDeltaTime);
            frameConfig = configs[0];
            return true;
        }

        private void TryRefreshVehicleDamageStateReadHandle()
        {
            if (_dataVault == null || IsGenerationHandleCreated(in _vehicleDamageStateReadHandle))
                return;

            _dataVault.TryGetGenerationHandle(VehicleDamageConstants.StateReadBuffer, out _vehicleDamageStateReadHandle);
        }

        public bool TryReadAddedMassTuning(out SubmarineAddedMassTuningDTO tuning)
        {
            tuning = default;
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryReadOnlyVaultHandle(in _addedMassTuningHandle, out NativeArray<SubmarineAddedMassTuningDTO>.ReadOnly tuningRows) ||
                tuningRows.Length == 0)
            {
                return false;
            }

            tuning = SubmarineAddedMassMath.SanitizeTuning(tuningRows[0]);
            return true;
        }

        public bool TryWriteAddedMassTuning(in SubmarineAddedMassTuningDTO tuning)
        {
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryAcquireVaultWriteLock(in _addedMassTuningHandle, out NativeArray<SubmarineAddedMassTuningDTO> tuningRows))
                return false;

            try
            {
                if (tuningRows.Length == 0)
                {
                    return false;
                }

                tuningRows[0] = SubmarineAddedMassMath.SanitizeTuning(in tuning);
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _addedMassTuningHandle);
            }
        }

        public bool TryReadLatestHydrodynamicsTelemetry(out SubmarineHydrodynamicsTelemetry telemetry)
        {
            telemetry = default;
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            if (!TryReadOnlyVaultHandle(in _hydrodynamicsTelemetryHandle, out NativeArray<SubmarineHydrodynamicsTelemetry>.ReadOnly telemetryRows) ||
                telemetryRows.Length < capacity * SubmarineDynamicsConstants.BlackBoxFrames)
            {
                return false;
            }

            int latest = (int)(_frameCounter % SubmarineDynamicsConstants.BlackBoxFrames);
            telemetry = telemetryRows[latest];
            return telemetry.Frame != 0u;
        }

        private void ConsumeSignals(
            NativeArray<SubmarineKinematicControl> controls,
            NativeArray<SubmarineMassProperties> masses,
            NativeArray<SubmarineForceAccumulator> forces,
            NativeArray<SubmarineKinematicConfig> configs,
            float fixedDeltaTime)
        {
            SubmarineKinematicControl control = controls[0];
            SubmarineMassProperties mass = masses[0];
            SubmarineForceAccumulator force = forces[0];
            SubmarineKinematicConfig config = configs[0];

            control.TargetDepthMeters = targetDepthMeters;
            control.Throttle01 = defaultThrottle01;
            control.BallastCommand01 = Hecton8.PureLogic.Systems.BallastTankController.Calculate(control.BallastCommand01, defaultBallast01, ballastFillRatePerSec, ballastVentRatePerSec, fixedDeltaTime);
            control.ThrustLocal = new float3(0f, 0f, 1f);
            control.TorqueLocal = float3.zero;

            if (_hasPendingVehicleCommand)
            {
                VehicleCommandSignal command = _pendingVehicleCommand;
                _hasPendingVehicleCommand = false;
                float pitch = math.clamp(command.Pitch, -1f, 1f);
                float yaw = math.clamp(command.Yaw, -1f, 1f);
                control.Throttle01 = math.saturate(command.Throttle);
                control.TorqueLocal = new float3(-pitch, yaw, 0f);
                if ((command.Flags & (byte)VehicleCommandSignalFlags.BallastBlow) != 0 ||
                    math.abs(command.BallastDelta) > 0.0001f)
                {
                    control.BallastCommand01 = math.saturate(control.BallastCommand01 + command.BallastDelta);
                }
            }

            ReadOnlySpan<InventoryChangedSignal> inventorySignals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < inventorySignals.Length; i++)
            {
                InventoryChangedSignal signal = inventorySignals[i];
                mass.CargoMassKg = math.max(0f, signal.TotalMassKg);
                mass.CargoCenterLocal = new float3(0f, -0.2f, config.CargoForwardMeters);
                control.CargoMassKg = mass.CargoMassKg;
            }

            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            byte flags = config.Flags;
            flags &= unchecked((byte)~SubmarineDynamicsConstants.ConfigFlagThermalDilation);
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthIndexSignal signal = healthSignals[i];
                if (signal.State >= SystemHealthIndexSignal.StateCritical || signal.Pressure01 >= config.TickDilationPressure01)
                    flags |= SubmarineDynamicsConstants.ConfigFlagThermalDilation;
            }

            config.Flags = flags;

            ReadOnlySpan<FluidDensityChangedSignal> densitySignals = SignalBus<FluidDensityChangedSignal>.GetFrameSnapshot();
            int densityCount = math.min(densitySignals.Length, MockSignalCapacity);
            for (int i = 0; i < densityCount; i++)
            {
                FluidDensityChangedSignal densitySignal = densitySignals[i];
                _fluidDensityMultiplier = math.isfinite(densitySignal.DensityMultiplier)
                    ? math.clamp(densitySignal.DensityMultiplier, 0.75f, 1.35f)
                    : 1f;
            }

            config.FluidDensityKgPerM3 = MockFluidDensityGenerator.ResolveBaseDensityKgPerM3(_fluidDensityMultiplier);

            ReadOnlySpan<SubmarineFloodStateSignal> floodSignals = SignalBus<SubmarineFloodStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < floodSignals.Length; i++)
            {
                SubmarineFloodStateSignal signal = floodSignals[i];
                mass.FloodMassKg = math.max(0f, signal.TotalWaterMassKg);
                mass.FloodCenterLocal = signal.DynamicCenterOfMassLocal;
                control.FloodWaterMassKg = mass.FloodMassKg;
            }

            ReadOnlySpan<MockFloodSignal> mockFloodSignals = SignalBus<MockFloodSignal>.GetFrameSnapshot();
            int mockFloodCount = math.min(mockFloodSignals.Length, MockSignalCapacity);
            for (int i = 0; i < mockFloodCount; i++)
            {
                MockFloodSignal mockFlood = mockFloodSignals[i];
                mass.FloodMassKg = math.max(0f, mockFlood.WaterMassKg);
                mass.FloodCenterLocal = mockFlood.LocalCompartment;
                control.FloodWaterMassKg = mass.FloodMassKg;
            }

            ReadOnlySpan<DeferredSubmarineImpactSignal> impactSignals = SignalBus<DeferredSubmarineImpactSignal>.GetFrameSnapshot();
            for (int i = 0; i < impactSignals.Length; i++)
            {
                DeferredSubmarineImpactSignal signal = impactSignals[i];
                float impulse = ResolveDeferredImpactImpulse(signal.Magnitude, signal.TraumaLevel, signal.IntegrityDelta, in mass, in config);
                ApplyImpactSignal(
                    ref force,
                    signal.LocalPoint,
                    ResolveFallbackImpactNormalLocal(signal.LocalPoint),
                    impulse,
                    _frameCounter,
                    signal.TraumaLevel,
                    normalIsLocal: true);
            }

            ReadOnlySpan<MockImpactSignal> mockImpactSignals = SignalBus<MockImpactSignal>.GetFrameSnapshot();
            int mockImpactCount = math.min(mockImpactSignals.Length, MockSignalCapacity);
            for (int i = 0; i < mockImpactCount; i++)
            {
                MockImpactSignal mockImpact = mockImpactSignals[i];
                ApplyImpactSignal(ref force, mockImpact.LocalPoint, mockImpact.NormalWorld, mockImpact.Magnitude, mockImpact.Frame, mockImpact.TraumaLevel, normalIsLocal: false);
            }

            ApplyVehicleComponentDamageState(ref control, ref mass, ref config);

            controls[0] = control;
            masses[0] = mass;
            forces[0] = force;
            configs[0] = config;
        }

        private void ApplyVehicleComponentDamageState(
            ref SubmarineKinematicControl control,
            ref SubmarineMassProperties mass,
            ref SubmarineKinematicConfig config)
        {
            if (_dataVault == null)
                return;

            if (!IsGenerationHandleCreated(in _vehicleDamageStateReadHandle))
                return;

            if (!TryReadOnlyVaultHandle(in _vehicleDamageStateReadHandle, out NativeArray<VehicleDamageStateDTO>.ReadOnly damageStates) ||
                damageStates.Length <= 0)
            {
                return;
            }

            VehicleDamageStateDTO state = damageStates[0];
            if ((state.Flags & VehicleDamageConstants.StateFlagInitialized) == 0u)
                return;

            config.MaxThrustN = math.max(0f, maxThrustN) * math.saturate(state.MaxThrustScalar);
            config.DragScale = math.max(0.01f, dragScale) * math.max(1f, state.DragScalar);
            mass.FloodMassKg = math.max(mass.FloodMassKg, math.max(0f, state.FloodWaterMassKg));
            control.FloodWaterMassKg = mass.FloodMassKg;

            float sensor01 = math.saturate(state.SensorScalar);
            config.CavitationThreshold = math.max(0.05f, config.CavitationThreshold * math.lerp(0.72f, 1f, sensor01));
        }

        private static void ApplyImpactSignal(
            ref SubmarineForceAccumulator force,
            float3 localPoint,
            float3 normalWorld,
            float magnitude,
            uint frame,
            byte traumaLevel,
            bool normalIsLocal)
        {
            float safeMagnitude = math.max(0f, magnitude);
            if (safeMagnitude >= force.ImpactMagnitude)
            {
                force.ImpactPointLocal = localPoint;
                force.ImpactNormalWorld = SubmarineDynamicsSimdMath.NormalizeOrFallback(normalWorld, new float3(0f, 0f, -1f));
                if (normalIsLocal)
                    force.Flags |= SubmarineDynamicsConstants.ForceFlagImpactNormalLocal;
                else
                    force.Flags &= ~SubmarineDynamicsConstants.ForceFlagImpactNormalLocal;
            }

            force.ImpactMagnitude = math.max(force.ImpactMagnitude, safeMagnitude);
            force.Flags |= SubmarineDynamicsConstants.ForceFlagImpact;
            force.Frame = frame;
        }

        private static float ResolveDeferredImpactImpulse(
            float relativeSpeedMetersPerSecond,
            byte traumaLevel,
            byte integrityDelta,
            in SubmarineMassProperties mass,
            in SubmarineKinematicConfig config)
        {
            float dryMass = math.max(1f, math.max(mass.BaseMassKg, config.BaseMassKg));
            int clampedTrauma = math.min((int)traumaLevel, 8);
            int clampedIntegrityDelta = math.min((int)integrityDelta, 32);
            float severity = 0.08f + (clampedTrauma * 0.07f) + (clampedIntegrityDelta * 0.005f);
            return math.clamp(math.max(0f, relativeSpeedMetersPerSecond) * dryMass * severity, 0f, 260000f);
        }

        private static float3 ResolveFallbackImpactNormalLocal(float3 localPoint)
        {
            float3 safePoint = math.all(math.isfinite(localPoint)) ? localPoint : new float3(0f, 0f, 1f);
            return SubmarineDynamicsSimdMath.NormalizeOrFallback(-safePoint, new float3(0f, 0f, -1f));
        }

        private bool LockSimulationBuffers()
        {
            if (_buffersLocked || _dataVault == null)
                return _buffersLocked;

            bool locked = false;
            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            if (!TryAcquireSimulationBufferGuard(GetSimulationMutationGuardMask()))
                return false;

            try
            {
                if (!TryValidateSimulationBuffer(in _stateHandle, BufferID.SubmarineKinematicStates, capacity) ||
                    !TryValidateSimulationBuffer(in _controlHandle, BufferID.SubmarineKinematicControls, capacity) ||
                    !TryValidateSimulationBuffer(in _pidHandle, BufferID.SubmarineKinematicPidStates, capacity) ||
                    !TryValidateSimulationBuffer(in _massHandle, BufferID.SubmarineKinematicMassProperties, capacity) ||
                    !TryValidateSimulationBuffer(in _forceHandle, BufferID.SubmarineKinematicForces, capacity) ||
                    !TryValidateSimulationBuffer(in _telemetryHandle, BufferID.SubmarineKinematicTelemetry, capacity * SubmarineDynamicsConstants.BlackBoxFrames) ||
                    !TryValidateSimulationBuffer(in _addedMassHandle, BufferID.Shinobu251AddedMassProfiles, capacity) ||
                    !TryValidateSimulationBuffer(in _hydrodynamicsTelemetryHandle, BufferID.Shinobu251HydrodynamicsTelemetry, capacity * SubmarineDynamicsConstants.BlackBoxFrames) ||
                    !TryValidateSimulationBuffer(in _configHandle, BufferID.SubmarineKinematicConfig, 1) ||
                    !TryValidateSimulationBuffer(in _hullProfileHandle, BufferID.Shinobu251HullProfiles, capacity) ||
                    !TryValidateSimulationBuffer(in _addedMassTuningHandle, BufferID.Shinobu251AddedMassTuning, 1) ||
                    !TryValidateSimulationBuffer(in _dragLutHandle, BufferID.SubmarineKinematicDragLut, SubmarineDynamicsConstants.DragLutSamples) ||
                    !ValidateGyroScheduleBuffers(capacity))
                {
                    return false;
                }

                _buffersLocked = true;
                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                {
                    _buffersLocked = true;
                    UnlockSimulationBuffers();
                }
            }
        }

        private bool TryAcquireSimulationBufferGuard(ulong guardMask)
        {
            IDataVault vault = _dataVault;
            if (_buffersLocked ||
                _simulationGuardMask != 0UL ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(guardMask))
            {
                return false;
            }

            _simulationGuardVault = vault;
            _simulationGuardMask = guardMask;
            return true;
        }

        private bool TryValidateSimulationBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsVehiclesPhysicsHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.Length >= requiredLength;
        }

        private void UnlockSimulationBuffers()
        {
            if (!_buffersLocked && _simulationGuardMask == 0UL)
                return;

            IDataVault vault = _simulationGuardVault;
            ulong guardMask = _simulationGuardMask;
            _simulationGuardVault = null;
            _simulationGuardMask = 0UL;
            _buffersLocked = false;
            vault?.ReleaseMutationGuard(guardMask);
        }

        // Editor-only, and it has to be: it resolves repo-relative paths off _projectRoot, which is
        // itself editor-only state, and reads loose files out of Docs/Archive. Its only call site
        // (in TryInitializeBootProfiles) is guarded too. df5d5fc14 unguarded this along with the
        // rest of the over-extended region, which was an over-correction - the boot chain below
        // genuinely had to come out of the guard, this method did not.
#if UNITY_EDITOR
        private bool TryLoadLegacyProfiles(ref SubmarineKinematicConfig config, Span<float> dragLut)
        {
            try
            {
                bool massLoaded = TryReadMassProfile(Path.Combine(_projectRoot, "Docs", "Archive", "submarine_mass_profiles.h8bin"), ref config);
                bool dragLoaded = TryReadDragProfile(Path.Combine(_projectRoot, "Docs", "Archive", "hydro_drag_constants.bin"), dragLut);

                if (!massLoaded)
                    massLoaded = TryReadMassProfile(Path.Combine(_projectRoot, "Assets", "StreamingAssets", "submarine_mass_profiles.h8bin"), ref config);
                if (!dragLoaded)
                    dragLoaded = TryReadDragProfile(Path.Combine(_projectRoot, "Assets", "StreamingAssets", "hydro_drag_constants.bin"), dragLut);
                if (!massLoaded)
                    massLoaded = TryReadMassProfile(Path.Combine(_projectRoot, "StreamingAssets", "submarine_mass_profiles.h8bin"), ref config);
                if (!dragLoaded)
                    dragLoaded = TryReadDragProfile(Path.Combine(_projectRoot, "StreamingAssets", "hydro_drag_constants.bin"), dragLut);

                if (!massLoaded && !dragLoaded)
                    return false;

                if (!dragLoaded)
                    FillDefaultDragLut(dragLut);

                config.SourceHash = SubmarineDynamicsConstants.SourceHashLegacy;
                config.Flags |= SubmarineDynamicsConstants.ConfigFlagLegacyProfile;
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
#endif

        private static bool TryReadMassProfile(string path, ref SubmarineKinematicConfig config)
        {
            if (!File.Exists(path))
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))
            {
                if (stream.Length < 48L)
                    return false;

                uint magic = ReadUInt32At(stream, 0L);
                if (magic != 0x4D425553u && magic != 0x5342554Du)
                    return false;

                float mass = ReadFloatAt(stream, 16L);
                float volume = ReadFloatAt(stream, 20L);
                float drag = ReadFloatAt(stream, 24L);
                float gyro = ReadFloatAt(stream, 28L);
                if (!math.isfinite(mass) || mass <= 1f || !math.isfinite(volume) || volume <= 1f)
                    return false;

                config.BaseMassKg = mass;
                config.HullVolumeM3 = volume;
                if (math.isfinite(drag) && drag > 0f)
                    config.DragScale = drag;
                if (math.isfinite(gyro) && gyro > 0f)
                    config.GyroStrength = gyro;
                return true;
            }
        }

        private static bool TryReadDragProfile(string path, Span<float> dragLut)
        {
            if (!File.Exists(path))
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))
            {
                if (stream.Length < 16L + (SubmarineDynamicsConstants.DragLutSamples * 4L))
                    return false;

                for (int i = 0; i < SubmarineDynamicsConstants.DragLutSamples; i++)
                {
                    float value = ReadFloatAt(stream, 16L + (i * 4L));
                    dragLut[i] = math.isfinite(value) && value > 0f ? value : 1f;
                }

                return true;
            }
        }

        private void GenerateEmergencyMockProfiles(ref SubmarineKinematicConfig config, Span<float> dragLut)
        {
            config.SourceHash = SubmarineDynamicsConstants.SourceHashMock;
            FillDefaultDragLut(dragLut);
        }

        /// <summary>
        /// Announces that submarine kinematics booted on the emergency fallback profile instead of an
        /// authored one, once per runtime.
        ///
        /// Every real profile source is Editor-only: `TryLoadLegacyProfiles` is inside the
        /// `#if UNITY_EDITOR` at its declaration and its call site, and `TryApplyCsvOverrides` /
        /// `TryApplyHullProfilesCsv` sit inside the `#if UNITY_EDITOR` spanning lines 1586-2237 with
        /// their call sites guarded too. So `SourceHashCsv` and `SourceHashLegacy` are unreachable in
        /// a player build and this fallback is the shipped path - the drag LUT is
        /// `FillDefaultDragLut`'s hardcoded `0.42 + 2.2*t*t` quadratic and the mass properties are
        /// whatever `BuildDefaultConfig` hardcodes.
        ///
        /// Nothing read `SourceHashMock` anywhere in the project, and every consumer of
        /// `config.SourceHash` tests only `== 0u` / `!= 0u` - presence, never provenance - so this
        /// was silent. Physics is a critical runtime system and must not fail over to fallback data
        /// without a telemetry route.
        ///
        /// This does not fix the missing data route. Authored profiles have to reach the player
        /// through the baked binary lane the data doctrine already prescribes (CSV/SO/Editor facade
        /// to validated `.h8bin`, no runtime CSV parsing); until that exists this at least stops the
        /// failure being invisible.
        /// </summary>
        private void ReportEmergencyProfileFallbackOnce()
        {
            if (_reportedEmergencyProfileFallback)
                return;

            _reportedEmergencyProfileFallback = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                EmergencyProfileFallbackWarningHash,
                SubmarineDynamicsConstants.SourceHashMock,
                SubmarineDynamicsConstants.DragLutSamples);
        }

        private SubmarineKinematicConfig BuildDefaultConfig()
        {
            SubmarineKinematicConfig config = default;
            Vector3 origin = transform.position;
            config.LocalOriginAup = new double3(origin.x, origin.y, origin.z);
            config.BaseMassKg = math.max(1f, baseMassKg);
            config.HullVolumeM3 = math.max(1f, hullVolumeM3);
            config.FluidDensityKgPerM3 = MockFluidDensityGenerator.DefaultSeawaterDensityKgPerM3;
            config.DragScale = math.max(0.01f, dragScale);
            config.PidP = pidP;
            config.PidI = pidI;
            config.PidD = pidD;
            config.PidIntegralLimit = 25f;
            config.GyroStrength = gyroStrength;
            config.GyroDamping = gyroDamping;
            config.MaxThrustN = maxThrustN;
            config.MaxTorqueNm = maxTorqueNm;
            config.CavitationDepthMeters = 6f;
            config.CavitationThreshold = 0.28f;
            config.SloshSpring = sloshSpring;
            config.SloshDamping = sloshDamping;
            config.FloodComGain = 0.65f;
            config.CargoForwardMeters = 2.8f;
            config.TickDilationPressure01 = 0.72f;
            config.SourceHash = SubmarineDynamicsConstants.SourceHashMock;
            config.MockFloodLocal = (float3)(mockFloodLocal);
            return config;
        }

        private void InitializeVehicleDefaults(
            Span<SubmarineKinematicState> states,
            Span<SubmarineKinematicControl> controls,
            Span<SubmarineMassProperties> masses,
            Span<SubmarineHullProfileDTO> hullProfiles,
            int capacity,
            in SubmarineKinematicConfig config)
        {
            SubmarineAddedMassMath.ResolveHullAxes(config.HullVolumeM3, out float lengthMeters, out float radiusMeters);
            for (int i = 0; i < capacity; i++)
            {
                SubmarineKinematicState state = states[i];
                state.Aup = config.LocalOriginAup;
                state.Rotation = quaternion.identity;
                state.CenterOfBuoyancyLocal = (float3)(centerOfBuoyancyLocal);
                state.InertiaTensor = math.float3(28000f, 92000f, 92000f);
                state.TotalMassKg = config.BaseMassKg;
                state.EntityId = ResolveVehicleEntityHashForIndex(i);
                states[i] = state;

                SubmarineKinematicControl control = controls[i];
                control.ThrustLocal = math.float3(0f, 0f, 1f);
                control.TargetDepthMeters = targetDepthMeters;
                control.Throttle01 = defaultThrottle01;
                control.BallastCommand01 = defaultBallast01;
                controls[i] = control;

                SubmarineMassProperties mass = masses[i];
                mass.PivotAup = config.LocalOriginAup;
                mass.BaseCenterOfMassLocal = float3.zero;
                mass.FloodCenterLocal = config.MockFloodLocal;
                mass.CargoCenterLocal = math.float3(0f, -0.2f, config.CargoForwardMeters);
                mass.CenterOfMassLocal = float3.zero;
                mass.CenterOfBuoyancyLocal = (float3)(centerOfBuoyancyLocal);
                mass.BaseMassKg = config.BaseMassKg;
                masses[i] = mass;

                if ((uint)i < (uint)hullProfiles.Length)
                {
                    SubmarineHullProfileDTO hull = default;
                    hull.ProfileHash = SubmarineDynamicsConstants.SourceHashAddedMass ^ (uint)i;
                    hull.BaseMassKg = config.BaseMassKg;
                    hull.HullVolumeM3 = config.HullVolumeM3;
                    hull.LengthMeters = lengthMeters;
                    hull.RadiusMeters = radiusMeters;
                    hull.AddedMassMultiplier = 1f;
                    hull.CenterOfBuoyancyLocal = mass.CenterOfBuoyancyLocal;
                    hull.CenterOfMassLocal = mass.CenterOfMassLocal;
                    hull.FloodVolumeScalar = 1f;
                    hullProfiles[i] = hull;
                }
            }
        }

        private static void FillDefaultDragLut(Span<float> dragLut)
        {
            // 0.42 is not an arbitrary constant: it is exactly the authored DragScalar of
            // starter_sub_hull in Data/Balance/SubmarineHull.csv. The hardcode is a frozen snapshot
            // of authored data, same as baseMassKg = 18000f matching the authored MassKg. Kept as the
            // fallback base for callers with no static-data record.
            FillDragLutFromBase(dragLut, 0.42f);
        }

        /// <summary>
        /// Fills the drag LUT with the project's quadratic ramp over a supplied base drag.
        /// </summary>
        private static void FillDragLutFromBase(Span<float> dragLut, float baseDrag)
        {
            float safeBase = math.isfinite(baseDrag) && baseDrag > 0f ? baseDrag : 0.42f;
            for (int i = 0; i < dragLut.Length; i++)
            {
                float t = i / (float)math.max(1, dragLut.Length - 1);
                dragLut[i] = safeBase + (2.2f * t * t);
            }
        }

        /// <summary>
        /// Applies the authored hull record from Data Monolith section 14 to the boot config.
        /// Returns false when the arena is not resident, the id is blank, the row is absent, or the
        /// values fail validation - in which case the caller falls through to its existing route.
        /// </summary>
        /// <remarks>
        /// Only two fields are mapped, and each is justified by an exact numeric match with the
        /// hardcode it replaces: <c>MassKg</c> -> <c>BaseMassKg</c> (serialized default is 18000f,
        /// the authored value) and <c>DragScalar</c> -> the drag LUT base (hardcoded 0.42f, the
        /// authored value).
        ///
        /// Deliberately NOT mapped:
        /// - <c>BuoyancyScalar</c> (0.96). There is no defensible relation to <c>HullVolumeM3</c>.
        ///   Reading it as a displaced/weight ratio implies 16.83 m3 at 1027 kg/m3, but the
        ///   serialized <c>hullVolumeM3</c> is 22f, which implies a ratio of 1.255. The two readings
        ///   disagree, so the field's meaning is unresolved and guessing it would change buoyancy
        ///   truth on an inference.
        /// - <c>CrushDepthMeters</c> (1200). <see cref="SubmarineKinematicConfig"/> has no crush
        ///   depth; the live owner is HectonPlayerMovement's serialized band, which disagrees at
        ///   1000/1450. Reconciling those is a design decision, not a wiring one.
        /// - <c>IntegrityCap</c> (100). No field on this config consumes it.
        /// </remarks>
        private bool TryApplyStaticHullConstants(ref SubmarineKinematicConfig config, Span<float> dragLut)
        {
            if (string.IsNullOrWhiteSpace(hullPartId))
                return false;

            // Same hash the bake side uses (H8DataMonolithCompiler.ParseHull -> ComputeFnv1A32), so
            // ids resolve identically. AsSpan keeps this allocation-free; boot is cold either way.
            uint partHash = Hecton8.Data.H8DataHash.ComputeFnv1A32(hullPartId.AsSpan());
            if (partHash == 0u)
                return false;

            if (!Hecton8.Data.H8StaticDataArena.TryFindSubmarineHullConstants(
                    partHash, out Hecton8.Data.H8SubmarineHullConstantRecord record))
            {
                return false;
            }

            if (!math.isfinite(record.MassKg) || record.MassKg <= 0f ||
                !math.isfinite(record.DragScalar) || record.DragScalar <= 0f)
            {
                return false;
            }

            config.BaseMassKg = math.max(1f, record.MassKg);
            config.SourceHash = SubmarineDynamicsConstants.SourceHashStaticData;
            FillDragLutFromBase(dragLut, record.DragScalar);
            return true;
        }

        // Editor-only for a real reason, unlike the outer guard that used to wrap the whole tail of
        // this class: this block parses CSV. TOOL_Designer_Facades_CSV_Binary_Bridge.txt forbids CSV
        // parsing and File.ReadAllText from gameplay runtime paths, so the designer override
        // convenience stays out of player builds. Keep this guard tight around the CSV path only -
        // widening it again would take the runtime boot methods below out of the player build.
#if UNITY_EDITOR
        private bool TryApplyCsvOverrides()
        {
            IDataVault vault = _dataVault;
            if (!_buffersReady || vault == null || _integratorPending || _buffersLocked)
                return false;

            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return false;

            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (ticks == _csvLastWriteTicks)
                return false;

            if (!TryReadOnlyVaultHandle(in _controlHandle, out NativeArray<SubmarineKinematicControl>.ReadOnly controlRead) ||
                !TryReadOnlyVaultHandle(in _configHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly configRead) ||
                !TryReadOnlyVaultHandle(in _hullProfileHandle, out NativeArray<SubmarineHullProfileDTO>.ReadOnly hullProfileRead) ||
                controlRead.Length == 0 ||
                configRead.Length == 0 ||
                hullProfileRead.Length == 0)
            {
                return false;
            }

            SubmarineKinematicConfig config = configRead[0];
            SubmarineKinematicControl control = controlRead[0];
            SubmarineHullProfileDTO hull = hullProfileRead[0];
            if (hull.ProfileHash == 0u)
            {
                SubmarineAddedMassMath.ResolveHullAxes(config.HullVolumeM3, out float lengthMeters, out float radiusMeters);
                hull.ProfileHash = SubmarineDynamicsConstants.SourceHashAddedMass;
                hull.BaseMassKg = config.BaseMassKg;
                hull.HullVolumeM3 = config.HullVolumeM3;
                hull.LengthMeters = lengthMeters;
                hull.RadiusMeters = radiusMeters;
                hull.AddedMassMultiplier = 1f;
                hull.FloodVolumeScalar = 1f;
            }

            Span<byte> byteScratch = stackalloc byte[(int)MaxCsvOverrideBytes];

            int read;
            try
            {
                using FileStream stream = File.Open(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = stream.Length;
                if (length <= 0L || length > MaxCsvOverrideBytes)
                    return false;

                int expected = (int)length;
                int totalRead = 0;
                while (totalRead < expected)
                {
                    int chunk = stream.Read(byteScratch.Slice(totalRead, expected - totalRead));
                    if (chunk <= 0)
                        break;

                    totalRead += chunk;
                }

                read = totalRead;
                if (read != expected)
                    return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            ParseOverrideBytes(byteScratch.Slice(0, read), ref config, ref control, ref hull);
            config.SourceHash = SubmarineDynamicsConstants.SourceHashCsv;
            config.Flags |= SubmarineDynamicsConstants.ConfigFlagCsvOverride;
            hull.BaseMassKg = config.BaseMassKg;
            hull.HullVolumeM3 = config.HullVolumeM3;
            hull.ProfileHash = SubmarineDynamicsConstants.SourceHashCsv;
            if (!TryCommitCsvOverrideControl(in control) ||
                !TryCommitCsvOverrideHullProfile(in hull) ||
                !TryCommitCsvOverrideConfig(in config))
            {
                return false;
            }

            ApplyCsvOverrideSerializedFields(in config, in control);
            _csvLastWriteTicks = ticks;
            return true;
        }

        private bool TryApplyHullProfilesCsv()
        {
            IDataVault vault = _dataVault;
            if (!_buffersReady || vault == null || _integratorPending || _buffersLocked)
                return false;

            if (string.IsNullOrEmpty(_hullProfilesCsvPath) || !File.Exists(_hullProfilesCsvPath))
                return false;

            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(_hullProfilesCsvPath).Ticks;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (ticks == _hullProfilesCsvLastWriteTicks)
                return false;

            if (!TryReadOnlyVaultHandle(in _configHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly configRead) ||
                configRead.Length == 0)
            {
                return false;
            }

            Span<byte> byteScratch = stackalloc byte[(int)MaxCsvOverrideBytes];
            Span<SubmarineHullProfileDTO> hullProfileScratch = stackalloc SubmarineHullProfileDTO[SubmarineDynamicsConstants.MaxVehicles];

            SubmarineKinematicConfig config = configRead[0];

            int read;
            try
            {
                using FileStream stream = File.Open(_hullProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = stream.Length;
                if (length <= 0L || length > MaxCsvOverrideBytes)
                    return false;

                int expected = (int)length;
                int totalRead = 0;
                while (totalRead < expected)
                {
                    int chunk = stream.Read(byteScratch.Slice(totalRead, expected - totalRead));
                    if (chunk <= 0)
                        break;

                    totalRead += chunk;
                }

                read = totalRead;
                if (read != expected)
                    return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            int rows = ParseHullProfilesCsv(
                byteScratch.Slice(0, read),
                hullProfileScratch,
                ref config);
            if (rows <= 0)
                return false;

            config.SourceHash = SubmarineDynamicsConstants.SourceHashCsv;
            config.Flags |= SubmarineDynamicsConstants.ConfigFlagCsvOverride;
            if (!TryCommitHullProfilesCsv(hullProfileScratch, rows) ||
                !TryCommitHullProfilesConfigCsv(in config))
            {
                return false;
            }

            _hullProfilesCsvLastWriteTicks = ticks;
            return true;
        }

        private bool TryCommitCsvOverrideControl(in SubmarineKinematicControl control)
        {
            if (!TryAcquireVaultWriteLock(in _controlHandle, out NativeArray<SubmarineKinematicControl> controls))
                return false;

            try
            {
                if (controls.Length == 0)
                    return false;

                controls[0] = control;
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _controlHandle);
            }
        }

        private bool TryCommitCsvOverrideConfig(in SubmarineKinematicConfig config)
        {
            if (!TryAcquireVaultWriteLock(in _configHandle, out NativeArray<SubmarineKinematicConfig> configs))
                return false;

            try
            {
                if (configs.Length == 0)
                    return false;

                configs[0] = config;
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _configHandle);
            }
        }

        private bool TryCommitCsvOverrideHullProfile(in SubmarineHullProfileDTO hull)
        {
            if (!TryAcquireVaultWriteLock(in _hullProfileHandle, out NativeArray<SubmarineHullProfileDTO> hullProfiles))
                return false;

            try
            {
                if (hullProfiles.Length == 0)
                    return false;

                hullProfiles[0] = hull;
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _hullProfileHandle);
            }
        }

        private bool TryCommitHullProfilesCsv(ReadOnlySpan<SubmarineHullProfileDTO> hullProfileScratch, int count)
        {
            if (!TryAcquireVaultWriteLock(in _hullProfileHandle, out NativeArray<SubmarineHullProfileDTO> hullProfiles))
                return false;

            try
            {
                if (hullProfiles.Length == 0)
                    return false;

                int copyLength = math.min(math.min(hullProfiles.Length, hullProfileScratch.Length), count);
                for (int i = 0; i < copyLength; i++)
                    hullProfiles[i] = hullProfileScratch[i];
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _hullProfileHandle);
            }
        }

        private bool TryCommitHullProfilesConfigCsv(in SubmarineKinematicConfig config)
        {
            if (!TryAcquireVaultWriteLock(in _configHandle, out NativeArray<SubmarineKinematicConfig> configs))
                return false;

            try
            {
                if (configs.Length == 0)
                    return false;

                configs[0] = config;
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _configHandle);
            }
        }

        private static int ParseHullProfilesCsv(
            ReadOnlySpan<byte> bytes,
            Span<SubmarineHullProfileDTO> hullProfiles,
            ref SubmarineKinematicConfig config)
        {
            int cursor = 0;
            int count = 0;
            while (count < hullProfiles.Length && TryReadCsvLine(bytes, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = TrimAscii(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!TryParseHullProfileLine(line, out SubmarineHullProfileDTO hull))
                    continue;

                hullProfiles[count] = hull;
                if (count == 0)
                {
                    config.BaseMassKg = SubmarineAddedMassMath.SafePositive(hull.BaseMassKg, config.BaseMassKg);
                    config.HullVolumeM3 = SubmarineAddedMassMath.SafePositive(hull.HullVolumeM3, config.HullVolumeM3);
                }

                count++;
            }

            return count;
        }

        private static bool TryParseHullProfileLine(ReadOnlySpan<byte> line, out SubmarineHullProfileDTO hull)
        {
            hull = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = TrimAscii(ReadCsvField(line, ref cursor));
            if (name.Length == 0 || TokenEqualsAsciiLower(name, "name") || TokenEqualsAsciiLower(name, "profile"))
                return false;

            float baseMass = ReadCsvFloat(line, ref cursor, 0f);
            float volume = ReadCsvFloat(line, ref cursor, 0f);
            float length = ReadCsvFloat(line, ref cursor, 0f);
            float radius = ReadCsvFloat(line, ref cursor, 0f);
            float multiplier = ReadCsvFloat(line, ref cursor, 1f);
            float floodScalar = ReadCsvFloat(line, ref cursor, 1f);
            if (!math.isfinite(volume) || volume <= 0.0001f)
                return false;

            SubmarineAddedMassMath.ResolveHullAxes(volume, out float fallbackLength, out float fallbackRadius);
            hull.ProfileHash = HashAsciiLower(name);
            hull.BaseMassKg = SubmarineAddedMassMath.SafePositive(baseMass, volume * 780f);
            hull.HullVolumeM3 = SubmarineAddedMassMath.SafePositive(volume, 1f);
            hull.LengthMeters = SubmarineAddedMassMath.SafePositive(length, fallbackLength);
            hull.RadiusMeters = SubmarineAddedMassMath.SafePositive(radius, fallbackRadius);
            hull.AddedMassMultiplier = math.clamp(SubmarineAddedMassMath.SafePositive(multiplier, 1f), 0.25f, 3f);
            hull.CenterOfBuoyancyLocal = new float3(0f, 0.7f, 0f);
            hull.CenterOfMassLocal = float3.zero;
            hull.Flags = SubmarineDynamicsConstants.ConfigFlagCsvOverride;
            hull.FloodVolumeScalar = math.clamp(math.isfinite(floodScalar) ? floodScalar : 1f, 0f, 2f);
            return hull.ProfileHash != 0u;
        }

        private static bool TryReadCsvLine(ReadOnlySpan<byte> bytes, ref int cursor, out ReadOnlySpan<byte> line)
        {
            if (cursor >= bytes.Length)
            {
                line = ReadOnlySpan<byte>.Empty;
                return false;
            }

            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;

            line = bytes.Slice(start, end - start);
            return true;
        }

        private static ReadOnlySpan<byte> ReadCsvField(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return TrimAscii(line.Slice(start, end - start));
        }

        private static float ReadCsvFloat(ReadOnlySpan<byte> line, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> field = ReadCsvField(line, ref cursor);
            return TryParseAsciiFloat(field, out float value) ? value : fallback;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
                start++;
            while (end >= start && (value[end] == (byte)' ' || value[end] == (byte)'\t'))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            if (value.Length == 0)
                return false;

            bool negative = false;
            bool fractional = false;
            bool digit = false;
            float fractionScale = 0.1f;
            int index = 0;
            if (value[0] == (byte)'-')
            {
                negative = true;
                index = 1;
            }

            for (; index < value.Length; index++)
            {
                byte c = value[index];
                if (c == (byte)'.')
                {
                    if (fractional)
                        return false;

                    fractional = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                digit = true;
                float n = c - (byte)'0';
                if (fractional)
                {
                    parsed += n * fractionScale;
                    fractionScale *= 0.1f;
                }
                else
                {
                    parsed = (parsed * 10f) + n;
                }
            }

            if (!digit)
                return false;

            parsed = negative ? -parsed : parsed;
            return math.isfinite(parsed);
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TokenEqualsAsciiLower(ReadOnlySpan<byte> value, string literal)
        {
            if (value.Length != literal.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                if (c != (byte)literal[i])
                    return false;
            }

            return true;
        }

        private void ParseOverrideBytes(
            ReadOnlySpan<byte> bytes,
            ref SubmarineKinematicConfig config,
            ref SubmarineKinematicControl control,
            ref SubmarineHullProfileDTO hull)
        {
            uint keyHash = 2166136261u;
            bool keyActive = false;
            bool readingValue = false;
            bool negative = false;
            bool fractional = false;
            float value = 0f;
            float fractionScale = 0.1f;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == (byte)',' && !readingValue)
                {
                    readingValue = true;
                    continue;
                }

                bool lineEnd = c == (byte)'\n' || c == (byte)'\r';
                if (lineEnd)
                {
                    if (keyActive && readingValue)
                        ApplyOverride(keyHash, negative ? -value : value, ref config, ref control, ref hull);

                    keyHash = 2166136261u;
                    keyActive = false;
                    readingValue = false;
                    negative = false;
                    fractional = false;
                    value = 0f;
                    fractionScale = 0.1f;
                    continue;
                }

                if (!readingValue)
                {
                    if (c == (byte)' ' || c == (byte)'\t')
                        continue;

                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    keyHash ^= c;
                    keyHash *= 16777619u;
                    keyActive = true;
                    continue;
                }

                if (c == (byte)'-')
                {
                    negative = true;
                    continue;
                }

                if (c == (byte)'.')
                {
                    fractional = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    continue;

                float digit = c - (byte)'0';
                if (fractional)
                {
                    value += digit * fractionScale;
                    fractionScale *= 0.1f;
                }
                else
                {
                    value = (value * 10f) + digit;
                }
            }

            if (keyActive && readingValue)
                ApplyOverride(keyHash, negative ? -value : value, ref config, ref control, ref hull);
        }

        private void ApplyOverride(
            uint keyHash,
            float value,
            ref SubmarineKinematicConfig config,
            ref SubmarineKinematicControl control,
            ref SubmarineHullProfileDTO hull)
        {
            switch (keyHash)
            {
                case HashBaseMassKg:
                    config.BaseMassKg = math.max(1f, value);
                    hull.BaseMassKg = config.BaseMassKg;
                    break;
                case HashHullVolumeM3:
                    config.HullVolumeM3 = math.max(1f, value);
                    hull.HullVolumeM3 = config.HullVolumeM3;
                    if (hull.LengthMeters <= 0f || hull.RadiusMeters <= 0f)
                    {
                        SubmarineAddedMassMath.ResolveHullAxes(hull.HullVolumeM3, out float resolvedLength, out float resolvedRadius);
                        hull.LengthMeters = resolvedLength;
                        hull.RadiusMeters = resolvedRadius;
                    }
                    break;
                case HashHullLengthM:
                    hull.LengthMeters = math.max(0.25f, value);
                    break;
                case HashHullRadiusM:
                    hull.RadiusMeters = math.max(0.05f, value);
                    break;
                case HashAddedMassMultiplier:
                    hull.AddedMassMultiplier = math.clamp(value, 0.25f, 3f);
                    break;
                case HashFloodVolumeScalar:
                    hull.FloodVolumeScalar = math.clamp(value, 0f, 2f);
                    break;
                case HashDragScale:
                    config.DragScale = math.max(0.01f, value);
                    break;
                case HashPidP:
                    config.PidP = math.max(0f, value);
                    break;
                case HashPidI:
                    config.PidI = math.max(0f, value);
                    break;
                case HashPidD:
                    config.PidD = math.max(0f, value);
                    break;
                case HashGyroStrength:
                    config.GyroStrength = math.max(0f, value);
                    break;
                case HashTargetDepthM:
                    control.TargetDepthMeters = math.max(0f, value);
                    break;
                case HashMaxThrustN:
                    config.MaxThrustN = math.max(0f, value);
                    break;
                case HashBallastLiftN:
                    ballastLiftN = math.max(0f, value);
                    break;
                case HashSloshSpring:
                    config.SloshSpring = math.max(0f, value);
                    break;
                case HashSloshDamping:
                    config.SloshDamping = math.max(0f, value);
                    break;
            }
        }

        private void ApplyCsvOverrideSerializedFields(
            in SubmarineKinematicConfig config,
            in SubmarineKinematicControl control)
        {
            baseMassKg = config.BaseMassKg;
            hullVolumeM3 = config.HullVolumeM3;
            dragScale = config.DragScale;
            pidP = config.PidP;
            pidI = config.PidI;
            pidD = config.PidD;
            gyroStrength = config.GyroStrength;
            targetDepthMeters = control.TargetDepthMeters;
            maxThrustN = config.MaxThrustN;
            sloshSpring = config.SloshSpring;
            sloshDamping = config.SloshDamping;
        }
#endif

        private bool DumpBlackBoxIfFaulted()
        {
            if (_dumpWritten || _dataVault == null || !IsGenerationHandleCreated(in _stateHandle))
                return false;

            if (!TryReadOnlyVaultHandle(in _stateHandle, out NativeArray<SubmarineKinematicState>.ReadOnly states) || states.Length == 0)
                return false;

            bool fatal = false;
            SubmarineKinematicState fatalState = default;
            int capacity = math.min(states.Length, math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles));
            for (int i = 0; i < capacity; i++)
            {
                SubmarineKinematicState state = states[i];
                if ((state.Flags & SubmarineDynamicsConstants.StateFlagFatalNan) != 0u)
                {
                    fatal = true;
                    fatalState = state;
                    break;
                }
            }

            if (!fatal)
                return false;

            RecordVaultSovereigntyTelemetry(VaultSovereigntyTelemetry.FaultFlag);
            float scalar = math.lengthsq(fatalState.LinearVelocity);
            bool written = TryDumpCoreBlackbox(SubmarineDynamicsFaultEventHash, scalar, fatalState.EntityId, SubmarineDynamicsFaultDumpHash);
            _dumpWritten |= written;
            return true;
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private bool TryDumpCoreBlackbox(uint eventHash, float scalarValue, uint stateHash, uint dumpHash)
        {
            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return false;

            GlobalTelemetryBus.PushEvent(eventHash, scalarValue, stateHash);
            return GlobalTelemetryBus.TryDumpBlackboxNow(dumpHash);
        }

        private void RecordVaultSovereigntyTelemetry(uint flags)
        {
            float quality = ResolveMathLodQualityWeight();
            VaultSovereigntyTelemetry.TryRecord(
                _dataVault,
                _frameCounter,
                generationMisses: 0,
                strideMultiplier: ResolveVaultTelemetryStride(quality),
                maxMemoryJobUs: 0f,
                globalQualityWeight: quality,
                sourceHash: VaultSovereigntyTelemetry.PhysicsSourceHash,
                flags: flags);
        }

        private static float ResolveMathLodQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private int ResolveVaultTelemetryStride(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float curved = quality * quality * (3f - (2f * quality));
            float target = math.lerp(4f, 1f, curved);
            int lower = math.clamp((int)math.floor(target), 1, 4);
            int upper = math.clamp(lower + 1, 1, 4);
            if (lower == upper)
                return lower;

            float fraction = target - lower;
            uint hash = MixFrameHash(0x56544C4Du, _frameCounter);
            return Hash01(hash) < fraction ? upper : lower;
        }

        private static uint MixFrameHash(uint seed, uint frame)
        {
            uint hash = seed ^ (frame * 747796405u);
            hash = (hash ^ (hash >> 16)) * 2246822519u;
            hash = (hash ^ (hash >> 13)) * 3266489917u;
            return hash ^ (hash >> 16);
        }

        private static float Hash01(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void PatchHydrodynamicsElapsedMicros(float elapsedMicros)
        {
            if (elapsedMicros <= 0f || !math.isfinite(elapsedMicros))
                return;

            if (!TryOpenVaultHandleForOwner(in _hydrodynamicsTelemetryHandle, out NativeArray<SubmarineHydrodynamicsTelemetry> telemetry))
                return;

            int vehicleCount = math.min(math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles), math.max(0, telemetry.Length / SubmarineDynamicsConstants.BlackBoxFrames));
            int local = (int)(_frameCounter % SubmarineDynamicsConstants.BlackBoxFrames);
            for (int i = 0; i < vehicleCount; i++)
            {
                int index = (i * SubmarineDynamicsConstants.BlackBoxFrames) + local;
                if ((uint)index >= (uint)telemetry.Length)
                    continue;

                SubmarineHydrodynamicsTelemetry entry = telemetry[index];
                entry.BurstElapsedUs = elapsedMicros;
                telemetry[index] = entry;
            }
        }

        private static float ResolveElapsedMicros(long startTicks)
        {
            if (startTicks <= 0L || Stopwatch.Frequency <= 0L)
                return 0f;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            if (elapsedTicks <= 0L)
                return 0f;

            double elapsedMicros = elapsedTicks * 1000000.0d / Stopwatch.Frequency;
            if (double.IsNaN(elapsedMicros) || double.IsInfinity(elapsedMicros) || elapsedMicros <= 0.0d)
                return 0f;

            return (float)Math.Min(elapsedMicros, 16777216.0d);
        }

        private static void EnsureSignalLanes()
        {
            SignalBus<MockFloodSignal>.Configure(MockSignalCapacity, MockSignalCapacity, SurvivalMockSignalCapacity, 0x4D464C44u);
            SignalBus<MockFloodSignal>.EnsureInitialized();

            SignalBus<MockImpactSignal>.Configure(MockSignalCapacity, MockSignalCapacity, SurvivalMockSignalCapacity, 0x4D494D50u);
            SignalBus<MockImpactSignal>.EnsureInitialized();

            SignalBus<CavitationAcousticSignal>.Configure(MockSignalCapacity, MockSignalCapacity, SurvivalMockSignalCapacity, 0x43564156u);
            SignalBus<CavitationAcousticSignal>.EnsureInitialized();

            SignalBus<FluidDensityChangedSignal>.Configure(MockSignalCapacity, MockSignalCapacity, SurvivalMockSignalCapacity, 0x46444E53u);
            SignalBus<FluidDensityChangedSignal>.EnsureInitialized();
        }

        private void DrainCavitationSignals()
        {
            bool hasConfig = TryReadConfigForSignalBridge(out SubmarineKinematicConfig config);
            ReadOnlySpan<CavitationAcousticSignal> signals = SignalBus<CavitationAcousticSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, MockSignalCapacity);
            for (int i = 0; i < count; i++)
            {
                CavitationAcousticSignal signal = signals[i];
                if (!hasConfig || signal.Intensity01 <= 0.001f)
                    continue;

                float3 localPosition = SafeFinite(signal.LocalPosition);
                double3 absolute = SafeFinite(config.LocalOriginAup) + new double3(localPosition);

                AcousticPingSignal ping = default;
                ping.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute);
                ping.RadiusMeters = math.clamp(12f + (math.saturate(signal.Intensity01) * 38f), 12f, 50f);
                ping.Intensity01 = math.saturate(signal.Intensity01);
                ping.SourceId = CavitationSourceId;
                ping.Channel = AcousticPingSignal.ChannelMetalStress;
                ping.Flags = 0;
                if (!SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref s_x001DirectSignalPushDropCount_SubmarineDynamicsRuntime))
                    IncrementDroppedSignalCount();
            }
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;
        }

        private bool TryReadConfigForSignalBridge(out SubmarineKinematicConfig config)
        {
            config = default;
            if (_dataVault == null ||
                !_buffersReady ||
                !TryReadOnlyVaultHandle(in _configHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly configs) ||
                configs.Length <= 0)
            {
                return false;
            }

            config = configs[0];
            return true;
        }

        private void RefreshCommandTargetIds()
        {
            _commandTargetInstanceId = unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
            _visualCommandTargetInstanceId = visualRoot != null
                ? unchecked((int)EntityId.ToULong(visualRoot.gameObject.GetEntityId()))
                : 0;
            _primaryVehicleEntityHash = unchecked((uint)_commandTargetInstanceId);
        }

        private uint ResolveVehicleEntityHashForIndex(int index)
        {
            return index == 0 && _primaryVehicleEntityHash != 0u
                ? _primaryVehicleEntityHash
                : (uint)index;
        }

        private bool MatchesGyroRouteTarget(uint entityHash)
        {
            uint commandHash = unchecked((uint)_commandTargetInstanceId);
            uint visualHash = unchecked((uint)_visualCommandTargetInstanceId);
            return entityHash != 0u &&
                   (entityHash == _primaryVehicleEntityHash ||
                    entityHash == commandHash ||
                    (visualHash != 0u && entityHash == visualHash));
        }

        private static uint ReadUInt32At(FileStream stream, long offset)
        {
            stream.Position = offset;
            Span<byte> bytes = stackalloc byte[4];
            int total = 0;
            while (total < bytes.Length)
            {
                int read = stream.Read(bytes.Slice(total));
                if (read <= 0)
                    throw new EndOfStreamException();

                total += read;
            }

            return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }

        private static float ReadFloatAt(FileStream stream, long offset)
        {
            return math.asfloat(ReadUInt32At(stream, offset));
        }

        private static float3 SafeFinite(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        private static double3 SafeFinite(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        private static string ResolveProjectRoot()
        {
            string current = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(current))
                return "C:\\hades\\Hecton8";

            string name = Path.GetFileName(current);
            return string.Equals(name, "Hecton8", StringComparison.OrdinalIgnoreCase)
                ? current
                : Path.Combine(current, "Hecton8");
        }
    }
}
