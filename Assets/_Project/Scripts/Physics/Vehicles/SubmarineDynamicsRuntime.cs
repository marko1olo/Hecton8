using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics.Vehicles
{
    public sealed class SubmarineDynamicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, ISlowTickable, IVehicleCommandSignalListener
    {
        private const int MockSignalCapacity = 64;
        private const int LowTierMockSignalCapacity = 8;
        private const long MaxCsvOverrideBytes = 4096L;
        private const uint HashBaseMassKg = 0xA5F7F6FCu;
        private const uint HashDragScale = 0x681E390Eu;
        private const uint HashPidP = 0x9D6F7115u;
        private const uint HashPidI = 0x946F62EAu;
        private const uint HashPidD = 0x896F5199u;
        private const uint HashGyroStrength = 0x3FE75EBEu;
        private const uint HashTargetDepthM = 0xA4492116u;
        private const uint HashMaxThrustN = 0x6DDC6935u;
        private const uint HashBallastLiftN = 0xDBC90E8Du;
        private const uint HashSloshSpring = 0x3466D6C8u;
        private const uint HashSloshDamping = 0x96934799u;
        private const uint CavitationSourceId = 0x534B3131u; // SK11

        [Header("Vault Lane")]
        [SerializeField, Range(1, SubmarineDynamicsConstants.MaxVehicles)] private int vehicleCapacity = 1;
        [SerializeField] private Transform visualRoot;

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
        private VaultBufferHandle<SubmarineKinematicState> _stateHandle;
        private VaultBufferHandle<SubmarineKinematicControl> _controlHandle;
        private VaultBufferHandle<SubmarinePidState> _pidHandle;
        private VaultBufferHandle<SubmarineMassProperties> _massHandle;
        private VaultBufferHandle<SubmarineForceAccumulator> _forceHandle;
        private VaultBufferHandle<SubmarineKinematicTelemetry> _telemetryHandle;
        private VaultBufferHandle<SubmarineKinematicConfig> _configHandle;
        private VaultBufferHandle<float> _dragLutHandle;
        private VaultBufferHandle<VehicleDamageStateDTO> _vehicleDamageStateReadHandle;
        private JobHandle _integratorHandle;
        private bool _integratorPending;
        private bool _buffersLocked;
        private bool _buffersReady;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _registeredSlow;
        private bool _dumpWritten;
        private bool _hasPendingVehicleCommand;
        private uint _frameCounter;
        private int _commandTargetInstanceId;
        private int _visualCommandTargetInstanceId;
        private int _fluidDensitySignalSequence;
        private float _fluidDensityMultiplier = 1f;
        private VehicleCommandSignal _pendingVehicleCommand;
        private long _csvLastWriteTicks;
        private string _projectRoot;
        private string _csvPath;

        private void OnEnable()
        {
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "sub_physics_overrides.csv");
            EnsureSignalLanes();
            RefreshCommandTargetIds();
            VehicleCommandSignalBus.Register(this);
            ResolveDataVault();
            EnsureVaultBuffers();

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            if (_integratorPending)
                DispatcherJobFence.TryComplete(ref _integratorHandle, forceComplete: true);

            _integratorPending = false;
            VehicleCommandSignalBus.Unregister(this);
            UnlockSimulationBuffers();
            DumpBlackBoxIfFaulted();

            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredLateFrame = false;
            _registeredSlow = false;
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
                    out NativeArray<SubmarineKinematicConfig> configs,
                    out NativeArray<float> dragLut))
            {
                _buffersReady = false;
                UnlockSimulationBuffers();
                return;
            }

            VehicleCommandSignalBus.FlushPending();
            ConsumeSignals(controls, masses, forces, configs);

            uint frame = ++_frameCounter;
            JobHandle seedHandle = default;
            if (enableMockSignals)
            {
                MockFloodSignalSeederJob mockFloodJob = new MockFloodSignalSeederJob
                {
                    FloodWriter = SignalBus<MockFloodSignal>.ParallelWriter,
                    Frame = frame,
                    Seed = 0x5EED110Bu,
                    LocalCompartment = ToFloat3(mockFloodLocal),
                    MassKg = 1200f
                };

                seedHandle = mockFloodJob.Schedule();
            }

            Submarine6DIntegratorJob integratorJob = new Submarine6DIntegratorJob
            {
                States = states,
                Controls = controls,
                PidStates = pidStates,
                MassProperties = masses,
                Forces = forces,
                Telemetry = telemetry,
                Configs = configs,
                DragLut = dragLut,
                CavitationWriter = SignalBus<CavitationAcousticSignal>.ParallelWriter,
                FixedDeltaTime = fixedDeltaTime,
                GlobalQualityWeight = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f),
                Frame = frame,
                VehicleCount = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles)
            };

            _integratorHandle = integratorJob.Schedule(integratorJob.VehicleCount, SubmarineDynamicsConstants.IntegratorBatchSize, seedHandle);
            _integratorHandle = VolcanicUpdraftVault.ScheduleSubmarineInjection(
                _dataVault,
                states,
                forces,
                configs,
                _integratorHandle,
                fixedDeltaTime,
                frame,
                integratorJob.VehicleCount);
            H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _integratorHandle);
            _integratorPending = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_integratorPending)
                return;

            if (!DispatcherJobFence.TryComplete(ref _integratorHandle, forceComplete: false))
                return;

            _integratorPending = false;
            UnlockSimulationBuffers();
            DrainCavitationSignals();
            bool faulted = DumpBlackBoxIfFaulted();
            if (!faulted)
                RecordVaultSovereigntyTelemetry(0u);
        }

        public void LateFrameTick()
        {
            if (_integratorPending || !_buffersReady || _dataVault == null)
                return;

            if (!_dataVault.ResolveBuffer(ref _stateHandle) || !_stateHandle.IsCreated)
                return;

            SubmarineKinematicState state = _stateHandle.GetElementAsReadOnlyRef(_dataVault, 0);
            Transform target = visualRoot != null ? visualRoot : transform;
            Quaternion rotation = new Quaternion(state.Rotation.value.x, state.Rotation.value.y, state.Rotation.value.z, state.Rotation.value.w);
            target.SetPositionAndRotation(new Vector3(state.LocalPosition.x, state.LocalPosition.y, state.LocalPosition.z), rotation);
        }

        public void SlowTick()
        {
            ResolveDataVault();
            if (!_integratorPending && !_buffersLocked)
                EnsureVaultBuffers();
            RefreshCommandTargetIds();
            TryApplyCsvOverrides();
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

        private bool ResolveDataVault()
        {
            if (_dataVault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                _dataVault = latest;

            return _dataVault != null;
        }

        private bool EnsureVaultBuffers()
        {
            if (!ResolveDataVault())
                return false;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            _stateHandle = _dataVault.GetBufferHandle<SubmarineKinematicState>(BufferID.SubmarineKinematicStates, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _controlHandle = _dataVault.GetBufferHandle<SubmarineKinematicControl>(BufferID.SubmarineKinematicControls, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _pidHandle = _dataVault.GetBufferHandle<SubmarinePidState>(BufferID.SubmarineKinematicPidStates, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _massHandle = _dataVault.GetBufferHandle<SubmarineMassProperties>(BufferID.SubmarineKinematicMassProperties, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _forceHandle = _dataVault.GetBufferHandle<SubmarineForceAccumulator>(BufferID.SubmarineKinematicForces, capacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _dataVault.GetBufferHandle<SubmarineKinematicTelemetry>(BufferID.SubmarineKinematicTelemetry, capacity * SubmarineDynamicsConstants.BlackBoxFrames, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _configHandle = _dataVault.GetBufferHandle<SubmarineKinematicConfig>(BufferID.SubmarineKinematicConfig, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _dragLutHandle = _dataVault.GetBufferHandle<float>(BufferID.SubmarineKinematicDragLut, SubmarineDynamicsConstants.DragLutSamples, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);

            if (!_stateHandle.IsCreated || !_controlHandle.IsCreated || !_pidHandle.IsCreated ||
                !_massHandle.IsCreated || !_forceHandle.IsCreated || !_telemetryHandle.IsCreated ||
                !_configHandle.IsCreated || !_dragLutHandle.IsCreated)
            {
                _buffersReady = false;
                return false;
            }

            NativeArray<SubmarineKinematicConfig> configs = _configHandle.Resolve(_dataVault);
            NativeArray<float> dragLut = _dragLutHandle.Resolve(_dataVault);
            NativeArray<SubmarineKinematicState> states = _stateHandle.Resolve(_dataVault);
            NativeArray<SubmarineKinematicControl> controls = _controlHandle.Resolve(_dataVault);
            NativeArray<SubmarineMassProperties> masses = _massHandle.Resolve(_dataVault);
            if (!configs.IsCreated || !dragLut.IsCreated || !states.IsCreated || !controls.IsCreated || !masses.IsCreated)
                return false;

            if (configs[0].SourceHash == 0u)
            {
                if (!TryLoadLegacyProfiles(configs, dragLut))
                    GenerateEmergencyMockProfiles(configs, dragLut);

                InitializeVehicleDefaults(states, controls, masses, configs[0]);
            }

            _buffersReady = true;
            return true;
        }

        private bool TryResolveArrays(
            out NativeArray<SubmarineKinematicState> states,
            out NativeArray<SubmarineKinematicControl> controls,
            out NativeArray<SubmarinePidState> pidStates,
            out NativeArray<SubmarineMassProperties> masses,
            out NativeArray<SubmarineForceAccumulator> forces,
            out NativeArray<SubmarineKinematicTelemetry> telemetry,
            out NativeArray<SubmarineKinematicConfig> configs,
            out NativeArray<float> dragLut)
        {
            states = _stateHandle.Resolve(_dataVault);
            controls = _controlHandle.Resolve(_dataVault);
            pidStates = _pidHandle.Resolve(_dataVault);
            masses = _massHandle.Resolve(_dataVault);
            forces = _forceHandle.Resolve(_dataVault);
            telemetry = _telemetryHandle.Resolve(_dataVault);
            configs = _configHandle.Resolve(_dataVault);
            dragLut = _dragLutHandle.Resolve(_dataVault);
            return states.IsCreated && controls.IsCreated && pidStates.IsCreated && masses.IsCreated &&
                   forces.IsCreated && telemetry.IsCreated && configs.IsCreated && dragLut.IsCreated;
        }

        private void ConsumeSignals(
            NativeArray<SubmarineKinematicControl> controls,
            NativeArray<SubmarineMassProperties> masses,
            NativeArray<SubmarineForceAccumulator> forces,
            NativeArray<SubmarineKinematicConfig> configs)
        {
            SubmarineKinematicControl control = controls[0];
            SubmarineMassProperties mass = masses[0];
            SubmarineForceAccumulator force = forces[0];
            SubmarineKinematicConfig config = configs[0];

            control.TargetDepthMeters = targetDepthMeters;
            control.Throttle01 = defaultThrottle01;
            control.BallastCommand01 = defaultBallast01;
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
                if ((((VehicleCommandSignalFlags)command.Flags) & VehicleCommandSignalFlags.BallastBlow) != 0 ||
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

            if (GlobalSignals.TryGetLatestFluidDensityChangedSignal(out FluidDensityChangedSignal densitySignal, out int densitySequence) &&
                densitySequence != _fluidDensitySignalSequence)
            {
                _fluidDensitySignalSequence = densitySequence;
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

            if (!_vehicleDamageStateReadHandle.IsCreated &&
                !_dataVault.TryGetBufferHandle(VehicleDamageConstants.StateReadBuffer, out _vehicleDamageStateReadHandle))
            {
                return;
            }

            if (!_dataVault.ResolveBuffer(ref _vehicleDamageStateReadHandle) ||
                !_vehicleDamageStateReadHandle.IsCreated ||
                _vehicleDamageStateReadHandle.Length <= 0)
            {
                return;
            }

            VehicleDamageStateDTO state = _vehicleDamageStateReadHandle.GetElementAsReadOnlyRef(_dataVault, 0);
            if ((state.Flags & VehicleDamageConstants.StateFlagInitialized) == 0u)
                return;

            config.MaxThrustN = math.max(0f, maxThrustN) * math.saturate(state.MaxThrustScalar);
            config.BallastLiftN = math.max(0f, ballastLiftN) * math.saturate(state.BuoyancyScalar);
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
                force.ImpactNormalWorld = math.normalizesafe(normalWorld, new float3(0f, 0f, -1f));
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
            return math.normalizesafe(-safePoint, new float3(0f, 0f, -1f));
        }

        private bool LockSimulationBuffers()
        {
            if (_buffersLocked || _dataVault == null)
                return _buffersLocked;

            if (!_dataVault.TryLockBuffer(BufferID.SubmarineKinematicStates, SystemID.VehiclesPhysics))
                return false;
            if (!_dataVault.TryLockBuffer(BufferID.SubmarineKinematicControls, SystemID.VehiclesPhysics))
            {
                _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicStates, SystemID.VehiclesPhysics);
                return false;
            }
            if (!_dataVault.TryLockBuffer(BufferID.SubmarineKinematicPidStates, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(BufferID.SubmarineKinematicMassProperties, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(BufferID.SubmarineKinematicForces, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(BufferID.SubmarineKinematicTelemetry, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(BufferID.SubmarineKinematicDragLut, SystemID.VehiclesPhysics))
            {
                _buffersLocked = true;
                UnlockSimulationBuffers();
                return false;
            }

            _buffersLocked = true;
            return true;
        }

        private void UnlockSimulationBuffers()
        {
            if (!_buffersLocked || _dataVault == null)
                return;

            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicStates, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicControls, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicPidStates, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicMassProperties, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicForces, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicTelemetry, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicDragLut, SystemID.VehiclesPhysics);
            _buffersLocked = false;
        }

        private bool TryLoadLegacyProfiles(NativeArray<SubmarineKinematicConfig> configs, NativeArray<float> dragLut)
        {
            try
            {
                SubmarineKinematicConfig config = BuildDefaultConfig();
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
                configs[0] = config;
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

        private static bool TryReadDragProfile(string path, NativeArray<float> dragLut)
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

        private void GenerateEmergencyMockProfiles(NativeArray<SubmarineKinematicConfig> configs, NativeArray<float> dragLut)
        {
            SubmarineKinematicConfig config = BuildDefaultConfig();
            config.SourceHash = SubmarineDynamicsConstants.SourceHashMock;
            configs[0] = config;
            FillDefaultDragLut(dragLut);
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
            config.BallastLiftN = ballastLiftN;
            config.CavitationDepthMeters = 6f;
            config.CavitationThreshold = 0.28f;
            config.SloshSpring = sloshSpring;
            config.SloshDamping = sloshDamping;
            config.FloodComGain = 0.65f;
            config.CargoForwardMeters = 2.8f;
            config.TickDilationPressure01 = 0.72f;
            config.SourceHash = SubmarineDynamicsConstants.SourceHashMock;
            config.HardwareTier = 1;
            config.MockFloodLocal = ToFloat3(mockFloodLocal);
            return config;
        }

        private void InitializeVehicleDefaults(
            NativeArray<SubmarineKinematicState> states,
            NativeArray<SubmarineKinematicControl> controls,
            NativeArray<SubmarineMassProperties> masses,
            in SubmarineKinematicConfig config)
        {
            int capacity = math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            for (int i = 0; i < capacity; i++)
            {
                SubmarineKinematicState state = states[i];
                state.Aup = config.LocalOriginAup;
                state.Rotation = quaternion.identity;
                state.CenterOfBuoyancyLocal = ToFloat3(centerOfBuoyancyLocal);
                state.InertiaTensor = new float3(28000f, 92000f, 92000f);
                state.TotalMassKg = config.BaseMassKg;
                state.EntityId = (uint)i;
                states[i] = state;

                SubmarineKinematicControl control = controls[i];
                control.ThrustLocal = new float3(0f, 0f, 1f);
                control.TargetDepthMeters = targetDepthMeters;
                control.Throttle01 = defaultThrottle01;
                control.BallastCommand01 = defaultBallast01;
                controls[i] = control;

                SubmarineMassProperties mass = masses[i];
                mass.PivotAup = config.LocalOriginAup;
                mass.BaseCenterOfMassLocal = float3.zero;
                mass.FloodCenterLocal = config.MockFloodLocal;
                mass.CargoCenterLocal = new float3(0f, -0.2f, config.CargoForwardMeters);
                mass.CenterOfMassLocal = float3.zero;
                mass.CenterOfBuoyancyLocal = ToFloat3(centerOfBuoyancyLocal);
                mass.BaseMassKg = config.BaseMassKg;
                masses[i] = mass;
            }
        }

        private static void FillDefaultDragLut(NativeArray<float> dragLut)
        {
            for (int i = 0; i < dragLut.Length; i++)
            {
                float t = i / (float)math.max(1, dragLut.Length - 1);
                dragLut[i] = 0.42f + (2.2f * t * t);
            }
        }

        private bool TryApplyCsvOverrides()
        {
            if (!_buffersReady || _dataVault == null || _integratorPending || _buffersLocked)
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

            bool controlsLocked = false;
            bool configLocked = false;

            try
            {
                if (!_dataVault.TryLockBuffer(BufferID.SubmarineKinematicControls, SystemID.VehiclesPhysics))
                    return false;
                controlsLocked = true;

                if (!_dataVault.TryLockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics))
                    return false;
                configLocked = true;

                NativeArray<SubmarineKinematicConfig> configs = _configHandle.Resolve(_dataVault);
                NativeArray<SubmarineKinematicControl> controls = _controlHandle.Resolve(_dataVault);
                if (!configs.IsCreated || !controls.IsCreated)
                    return false;

                SubmarineKinematicConfig config = configs[0];
                SubmarineKinematicControl control = controls[0];

                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))
                {
                    if (stream.Length <= 0L || stream.Length > MaxCsvOverrideBytes)
                        return false;

                    ParseOverrideStream(stream, ref config, ref control);
                }

                config.SourceHash = SubmarineDynamicsConstants.SourceHashCsv;
                config.Flags |= SubmarineDynamicsConstants.ConfigFlagCsvOverride;
                configs[0] = config;
                controls[0] = control;
                _csvLastWriteTicks = ticks;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (configLocked)
                    _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics);
                if (controlsLocked)
                    _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicControls, SystemID.VehiclesPhysics);
            }
        }

        private void ParseOverrideStream(FileStream stream, ref SubmarineKinematicConfig config, ref SubmarineKinematicControl control)
        {
            uint keyHash = 2166136261u;
            bool keyActive = false;
            bool readingValue = false;
            bool negative = false;
            bool fractional = false;
            float value = 0f;
            float fractionScale = 0.1f;

            while (true)
            {
                int raw = stream.ReadByte();
                bool end = raw < 0;
                byte c = end ? (byte)'\n' : (byte)raw;
                if (c == (byte)',' && !readingValue)
                {
                    readingValue = true;
                    continue;
                }

                bool lineEnd = c == (byte)'\n' || c == (byte)'\r';
                if (lineEnd || end)
                {
                    if (keyActive && readingValue)
                        ApplyOverride(keyHash, negative ? -value : value, ref config, ref control);

                    keyHash = 2166136261u;
                    keyActive = false;
                    readingValue = false;
                    negative = false;
                    fractional = false;
                    value = 0f;
                    fractionScale = 0.1f;
                    if (end)
                        break;

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
        }

        private void ApplyOverride(uint keyHash, float value, ref SubmarineKinematicConfig config, ref SubmarineKinematicControl control)
        {
            switch (keyHash)
            {
                case HashBaseMassKg:
                    config.BaseMassKg = math.max(1f, value);
                    baseMassKg = config.BaseMassKg;
                    break;
                case HashDragScale:
                    config.DragScale = math.max(0.01f, value);
                    dragScale = config.DragScale;
                    break;
                case HashPidP:
                    config.PidP = math.max(0f, value);
                    pidP = config.PidP;
                    break;
                case HashPidI:
                    config.PidI = math.max(0f, value);
                    pidI = config.PidI;
                    break;
                case HashPidD:
                    config.PidD = math.max(0f, value);
                    pidD = config.PidD;
                    break;
                case HashGyroStrength:
                    config.GyroStrength = math.max(0f, value);
                    gyroStrength = config.GyroStrength;
                    break;
                case HashTargetDepthM:
                    control.TargetDepthMeters = math.max(0f, value);
                    targetDepthMeters = control.TargetDepthMeters;
                    break;
                case HashMaxThrustN:
                    config.MaxThrustN = math.max(0f, value);
                    maxThrustN = config.MaxThrustN;
                    break;
                case HashBallastLiftN:
                    config.BallastLiftN = math.max(0f, value);
                    ballastLiftN = config.BallastLiftN;
                    break;
                case HashSloshSpring:
                    config.SloshSpring = math.max(0f, value);
                    sloshSpring = config.SloshSpring;
                    break;
                case HashSloshDamping:
                    config.SloshDamping = math.max(0f, value);
                    sloshDamping = config.SloshDamping;
                    break;
            }
        }

        private bool DumpBlackBoxIfFaulted()
        {
            if (_dumpWritten || _dataVault == null || !_stateHandle.IsCreated)
                return false;

            NativeArray<SubmarineKinematicState> states = _stateHandle.Resolve(_dataVault);
            if (!states.IsCreated || states.Length == 0)
                return false;

            bool fatal = false;
            int capacity = math.min(states.Length, math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles));
            for (int i = 0; i < capacity; i++)
            {
                if ((states[i].Flags & SubmarineDynamicsConstants.StateFlagFatalNan) != 0u)
                {
                    fatal = true;
                    break;
                }
            }

            if (!fatal)
                return false;

            RecordVaultSovereigntyTelemetry(VaultSovereigntyTelemetry.FaultFlag);
            VaultSovereigntyTelemetry.TryDump(_dataVault, _projectRoot);

            NativeArray<SubmarineKinematicTelemetry> telemetry = _telemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return true;

            string logRoot = Path.Combine(_projectRoot, "Docs", "AgentLogs");
            try
            {
                Directory.CreateDirectory(logRoot);
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }

            string h8DumpPath = Path.Combine(logRoot, "Dump_SHINOBU_11.h8dump");
            string legacyBinPath = Path.Combine(logRoot, "Dump_SUB_KINEMATICS.bin");
            if (!TryWriteBlackBoxDump(h8DumpPath, telemetry))
                return true;

            TryWriteBlackBoxDump(legacyBinPath, telemetry);
            _dumpWritten = true;
            return true;
        }

        private void RecordVaultSovereigntyTelemetry(uint flags)
        {
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
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

        private static int ResolveVaultTelemetryStride(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float inverse = 1f - quality;
            return math.clamp(1 + (int)math.floor(inverse * 3.333334f), 1, 4);
        }

        private static bool TryWriteBlackBoxDump(string path, NativeArray<SubmarineKinematicTelemetry> telemetry)
        {
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(0x4B425553u);
                    writer.Write(telemetry.Length);
                    writer.Write(SubmarineDynamicsConstants.BlackBoxFrames);
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        SubmarineKinematicTelemetry entry = telemetry[i];
                        writer.Write(entry.Aup.x);
                        writer.Write(entry.Aup.y);
                        writer.Write(entry.Aup.z);
                        writer.Write(entry.LinearVelocity.x);
                        writer.Write(entry.LinearVelocity.y);
                        writer.Write(entry.LinearVelocity.z);
                        writer.Write(entry.AngularVelocity.x);
                        writer.Write(entry.AngularVelocity.y);
                        writer.Write(entry.AngularVelocity.z);
                        writer.Write(entry.CenterOfMassLocal.x);
                        writer.Write(entry.CenterOfMassLocal.y);
                        writer.Write(entry.CenterOfMassLocal.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Frame);
                        writer.Write(entry.StateHash);
                    }
                }

                return true;
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

        private static void EnsureSignalLanes()
        {
            SignalBus<MockFloodSignal>.Configure(MockSignalCapacity, MockSignalCapacity, LowTierMockSignalCapacity, 0x4D464C44u);
            SignalBus<MockImpactSignal>.Configure(MockSignalCapacity, MockSignalCapacity, LowTierMockSignalCapacity, 0x4D494D50u);
            SignalBus<CavitationAcousticSignal>.Configure(MockSignalCapacity, MockSignalCapacity, LowTierMockSignalCapacity, 0x43564156u);
            SignalBus<MockFloodSignal>.EnsureInitialized();
            SignalBus<MockImpactSignal>.EnsureInitialized();
            SignalBus<CavitationAcousticSignal>.EnsureInitialized();
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
                GlobalSignals.Publish(in ping);
            }
        }

        private bool TryReadConfigForSignalBridge(out SubmarineKinematicConfig config)
        {
            config = default;
            if (_dataVault == null ||
                !_buffersReady ||
                !_dataVault.ResolveBuffer(ref _configHandle) ||
                !_configHandle.IsCreated ||
                _configHandle.Length <= 0)
            {
                return false;
            }

            config = _configHandle.GetElementAsReadOnlyRef(_dataVault, 0);
            return true;
        }

        private void RefreshCommandTargetIds()
        {
            _commandTargetInstanceId = unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
            _visualCommandTargetInstanceId = visualRoot != null
                ? unchecked((int)EntityId.ToULong(visualRoot.gameObject.GetEntityId()))
                : 0;
        }

        private static uint ReadUInt32At(FileStream stream, long offset)
        {
            stream.Position = offset;
            int b0 = stream.ReadByte();
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            int b3 = stream.ReadByte();
            if ((b0 | b1 | b2 | b3) < 0)
                throw new EndOfStreamException();

            return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
        }

        private static float ReadFloatAt(FileStream stream, long offset)
        {
            return math.asfloat(ReadUInt32At(stream, offset));
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
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
