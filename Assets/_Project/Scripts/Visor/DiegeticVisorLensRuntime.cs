using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    [DisallowMultipleComponent]
    public unsafe sealed class DiegeticVisorLensRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryCapacity = 300;
        private const int CsvBufferBytes = 4096;
        private const int BinaryProbeBytes = 64;
        private const int GpuGlobalsStrideBytes = 64;
        private const float MinimumDeltaTime = 0.0001f;
        private const float BreachPublishCooldownSeconds = 0.35f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_VISOR_SURGEON.bin";
        private const string CsvRelativePath = "visor_properties.csv";
        private const uint DumpMagic = 0x56534C44u;
        private const uint DumpVersion = 1u;
        private const uint VisorBreachLaneHash = 0x56534252u;
        private const uint RuntimeSourceHash = 0x53483635u;
        private const SystemID OwnerSystem = SystemID.Vfx;

        private static readonly BufferID StateBufferId = (BufferID)71020;
        private static readonly BufferID TuningBufferId = (BufferID)71021;
        private static readonly BufferID PhysiologyBufferId = (BufferID)71022;
        private static readonly BufferID EnvironmentBufferId = (BufferID)71023;
        private static readonly BufferID GpuGlobalsBufferId = (BufferID)71024;
        private static readonly BufferID TelemetryRingBufferId = (BufferID)71025;
        private static readonly BufferID TelemetryCursorBufferId = (BufferID)71026;
        private static readonly BufferID CsvByteBufferId = (BufferID)71027;
        private static readonly BufferID BinaryProbeByteBufferId = (BufferID)71028;
        private static readonly BufferID NanFlagBufferId = (BufferID)71029;

        private static readonly int GpuGlobalsNameId = Shader.PropertyToID("HectonDiegeticVisorLensGlobals");
        private static readonly int LensStateId = Shader.PropertyToID("_HectonDiegeticVisorLensState");
        private static readonly int LensParams0Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams0");
        private static readonly int LensParams1Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams1");
        private static readonly int LensParams2Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams2");

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private VaultGenerationHandle<VisorStateDTO> _stateHandle;
        private VaultGenerationHandle<VisorLensTuningDTO> _tuningHandle;
        private VaultGenerationHandle<MockPhysiologySignal> _physiologyHandle;
        private VaultGenerationHandle<MockVisorEnvironmentSignal> _environmentHandle;
        private VaultGenerationHandle<DiegeticVisorLensGpuGlobalsDTO> _gpuGlobalsHandle;
        private VaultGenerationHandle<VisorLensTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<byte> _csvBytesHandle;
        private VaultGenerationHandle<byte> _binaryProbeBytesHandle;
        private VaultGenerationHandle<int> _nanFlagsHandle;
        private GraphicsBuffer _gpuGlobalsBufferA;
        private GraphicsBuffer _gpuGlobalsBufferB;
        private GraphicsBuffer _activeGpuGlobalsBuffer;
        private DiegeticVisorLensGpuGlobalsDTO _lastGpuGlobals;
        private DiegeticVisorLensGpuGlobalsDTO _uploadedGpuGlobals;
        private JobHandle _scheduledHandle;
        private Quaternion _lastCameraRotation;
        private float3 _headAngularVelocity;
        private float _breachCooldown;
        private float _simulationAccumulator;
        private float _pendingRespirationRate;
        private float _pendingHeartRate;
        private float _pendingCoreTemperatureC;
        private float _pendingExternalPressure01;
        private float _pendingWaterTemperatureC;
        private float _pendingSiltDensity01;
        private float _pendingDarkness01;
        private float _pendingCorruption01;
        private float _pendingSurfaceEmergence01;
        private float _pendingWipeCommand01;
        private uint _lastShaderUpdateComputeTimeNs;
        private uint _frameCounter;
        private int _gpuGlobalsWriteIndex;
        private ushort _breachSequence;
        private bool _hasScheduledWork;
        private bool _hasLastCameraRotation;
        private bool _nativeReady;
        private bool _hasGpuGlobals;
        private bool _hasUploadedGpuGlobals;
        private bool _blackBoxDumped;
        private bool _mockDataActive;
        private bool _binaryProbePerformed;
        private bool _forceImmediateSimulation;
        private bool _hasPendingPhysiology;
        private bool _hasPendingExternalPressure;
        private bool _hasPendingEnvironment;
        private bool _pendingMockReset;
        private bool _registeredHotSwapListener;

        private ref VisorStateDTO GetStateRefUnsafe()
        {
            EnsureNativeState();
            return ref GetVaultElementRef(ref _stateHandle, StateBufferId, 1, 0);
        }

        private ref VisorLensTuningDTO GetTuningRefUnsafe()
        {
            EnsureNativeState();
            return ref GetVaultElementRef(ref _tuningHandle, TuningBufferId, 1, 0);
        }

        public bool TryGetPreview(out VisorStateDTO state, out DiegeticVisorLensGpuGlobalsDTO globals, out VisorLensTuningDTO tuning)
        {
            state = default;
            globals = default;
            tuning = default;
            IDataVault vault = _vault;
            if (!_nativeReady ||
                _hasScheduledWork ||
                vault == null ||
                !TryReadVaultArray(vault, in _stateHandle, StateBufferId, 1, out NativeArray<VisorStateDTO> states) ||
                !TryReadVaultArray(vault, in _gpuGlobalsHandle, GpuGlobalsBufferId, 1, out NativeArray<DiegeticVisorLensGpuGlobalsDTO> gpuGlobals) ||
                !TryReadVaultArray(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<VisorLensTuningDTO> tunings))
            {
                return false;
            }

            state = states[0];
            globals = gpuGlobals[0];
            tuning = tunings[0];
            return true;
        }

        /// <summary>
        /// Writes visor scalar state only when no Burst job owns the Vault state buffer.
        /// </summary>
        public bool TryWriteState(in VisorStateDTO state)
        {
            EnsureNativeState();
            if (_hasScheduledWork)
                return false;

            ref VisorStateDTO stateRef = ref GetStateRefUnsafe();
            stateRef.CondensationLevel = Sanitize01(state.CondensationLevel);
            stateRef.WaterDropletIntensity = Sanitize01(state.WaterDropletIntensity);
            stateRef.CrackSeverity = Sanitize01(state.CrackSeverity);
            stateRef.DirtAccumulation = Sanitize01(state.DirtAccumulation);
            _forceImmediateSimulation = true;
            return true;
        }

        /// <summary>
        /// Writes visor tuning only when no Burst job owns the Vault tuning buffer.
        /// </summary>
        public bool TryWriteTuning(in VisorLensTuningDTO tuning)
        {
            EnsureNativeState();
            if (_hasScheduledWork)
                return false;

            GetTuningRefUnsafe() = tuning;
            _forceImmediateSimulation = true;
            return true;
        }

        private void Awake()
        {
            CacheRegistryServicesCold();
            EnsureNativeState();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureNativeState();
            TryRegisterHotSwapListener();
            GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void OnDisable()
        {
            CompleteScheduledWorkForTeardown();
            UploadGpuGlobals();
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            TryUnregisterHotSwapListener();
            ClearGpuGlobals();
            ReleaseGpuBuffer();
            _playerContext = null;
            ReleaseNativeState(_vault, clearVault: true);
        }

        public void Tick(float deltaTime)
        {
            if (!_nativeReady)
            {
                EnsureNativeState();
            }

            float safeDelta = SanitizeDelta(deltaTime);
            _frameCounter++;
            _breachCooldown = math.max(0f, _breachCooldown - safeDelta);

            if (_hasScheduledWork)
            {
                return;
            }

            ApplyPendingMockResetIfNeeded();
            IngestCoreSignals(safeDelta);
            UpdateHeadAngularVelocity(safeDelta);
            float qualityWeight = ResolveQualityWeight();
            _simulationAccumulator = math.min(_simulationAccumulator + safeDelta, 0.5f);
            float simulationInterval = ResolveSimulationInterval(qualityWeight);
            if (!_forceImmediateSimulation && _simulationAccumulator < simulationInterval)
                return;

            float simulationDelta = math.max(_simulationAccumulator, safeDelta);
            _simulationAccumulator = 0f;
            _forceImmediateSimulation = false;
            ScheduleSimulation(simulationDelta, qualityWeight);
        }

        public void LateFrameTick()
        {
            TryFinalizeScheduledWorkNoWait();
            UploadGpuGlobals();
        }

        public void GenerateEmergencyMockVisorData()
        {
            EnsureNativeState();
            if (_hasScheduledWork)
            {
                _pendingMockReset = true;
                _forceImmediateSimulation = true;
                return;
            }

            ApplyEmergencyMockVisorData();
        }

        private void ApplyEmergencyMockVisorData()
        {
            ref VisorStateDTO state = ref GetVaultElementRef(ref _stateHandle, StateBufferId, 1, 0);
            ref VisorLensTuningDTO tuning = ref GetVaultElementRef(ref _tuningHandle, TuningBufferId, 1, 0);
            ref MockPhysiologySignal physiology = ref GetVaultElementRef(ref _physiologyHandle, PhysiologyBufferId, 1, 0);
            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);

            state.CondensationLevel = 0f;
            state.WaterDropletIntensity = 0f;
            state.CrackSeverity = 0f;
            state.DirtAccumulation = 0.04f;

            tuning.FogRate = 0.08f;
            tuning.FogBreathGain = 0.045f;
            tuning.FogColdGain = 0.035f;
            tuning.ClearingRate = 0.22f;
            tuning.DropletDrainSeconds = 5f;
            tuning.DropletGravityStrength = 0.42f;
            tuning.SurfaceWashDrainRate = 0.2f;
            tuning.CrackPressureThreshold = 0.78f;
            tuning.CrackGrowthRate = 0.45f;
            tuning.MaxCrackSeverity = 1f;
            tuning.DirtSiltGain = 0.04f;
            tuning.WipeStrength = 2.4f;
            tuning.ReflectionDarknessGain = 0.64f;
            tuning.AnomalyNoiseGain = 0.32f;
            tuning.LowRefractionQualityCutoff = 0.3f;
            tuning.BiolumReflectionGain = 0.22f;
            tuning.HeartCondensationGain = 0.012f;
            tuning.CoreTempCondensationGain = 0.018f;
            tuning.QualityStaticBlendStart = 0.18f;
            tuning.QualityDynamicBlendEnd = 0.72f;
            tuning.Flags = 0u;
            tuning.Version++;
            tuning._pad0 = 0f;
            tuning._pad1 = 0f;

            physiology.RespirationRate = 12f;
            physiology.HeartRate = 72f;
            physiology.CoreTemperatureC = 37f;
            physiology.BreathSpike01 = 0f;
            physiology.Frame = _frameCounter;
            physiology.Flags = 0u;
            physiology._pad0 = 0f;
            physiology._pad1 = 0f;

            environment.ExternalWaterTemperatureC = 4f;
            environment.ExternalPressure01 = 0f;
            environment.SiltDensity01 = 0f;
            environment.Darkness01 = 0.48f;
            environment.SurfaceEmergence01 = 0f;
            environment.WipeCommand01 = 0f;
            environment.Corruption01 = 0f;
            environment.WaterlineBreach01 = 0f;
            environment.Frame = _frameCounter;
            environment.Flags = 0u;
            environment._pad0 = 0f;
            environment._pad1 = 0f;
            _mockDataActive = true;
            ClearPendingExternalInputs();
        }

        public void InjectMockPhysiology(float respirationRate, float heartRate, float coreTemperatureC)
        {
            EnsureNativeState();
            float safeRespiration = SanitizeRange(respirationRate, 4f, 44f, 12f);
            float safeHeartRate = SanitizeRange(heartRate, 28f, 220f, 72f);
            float safeCoreTemperature = SanitizeRange(coreTemperatureC, 28f, 43f, 37f);
            if (_hasScheduledWork)
            {
                _pendingRespirationRate = safeRespiration;
                _pendingHeartRate = safeHeartRate;
                _pendingCoreTemperatureC = safeCoreTemperature;
                _hasPendingPhysiology = true;
                _forceImmediateSimulation = true;
                return;
            }

            ref MockPhysiologySignal physiology = ref GetVaultElementRef(ref _physiologyHandle, PhysiologyBufferId, 1, 0);
            physiology.RespirationRate = safeRespiration;
            physiology.HeartRate = safeHeartRate;
            physiology.CoreTemperatureC = safeCoreTemperature;
            physiology.Frame = _frameCounter;
            _forceImmediateSimulation = true;
        }

        public void InjectMockExternalPressure(float pressure01)
        {
            EnsureNativeState();
            float safePressure = Sanitize01(pressure01);
            if (_hasScheduledWork)
            {
                _pendingExternalPressure01 = math.max(_pendingExternalPressure01, safePressure);
                _hasPendingExternalPressure = true;
                _forceImmediateSimulation = true;
                return;
            }

            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);
            environment.ExternalPressure01 = safePressure;
            environment.Frame = _frameCounter;
            _forceImmediateSimulation = true;
        }

        public void InjectEnvironment(float waterTemperatureC, float silt01, float darkness01, float corruption01)
        {
            EnsureNativeState();
            float safeWaterTemperature = SanitizeRange(waterTemperatureC, -4f, 38f, 4f);
            float safeSilt = Sanitize01(silt01);
            float safeDarkness = Sanitize01(darkness01);
            float safeCorruption = Sanitize01(corruption01);
            if (_hasScheduledWork)
            {
                _pendingWaterTemperatureC = safeWaterTemperature;
                _pendingSiltDensity01 = math.max(_pendingSiltDensity01, safeSilt);
                _pendingDarkness01 = safeDarkness;
                _pendingCorruption01 = math.max(_pendingCorruption01, safeCorruption);
                _hasPendingEnvironment = true;
                _forceImmediateSimulation = true;
                return;
            }

            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);
            environment.ExternalWaterTemperatureC = safeWaterTemperature;
            environment.SiltDensity01 = math.max(environment.SiltDensity01, safeSilt);
            environment.Darkness01 = safeDarkness;
            environment.Corruption01 = math.max(environment.Corruption01, safeCorruption);
            environment.Frame = _frameCounter;
            _forceImmediateSimulation = true;
        }

        public void NotifySurfaceEmergence(float intensity01)
        {
            EnsureNativeState();
            float safeIntensity = Sanitize01(intensity01);
            if (_hasScheduledWork)
            {
                _pendingSurfaceEmergence01 = math.max(_pendingSurfaceEmergence01, safeIntensity);
                _forceImmediateSimulation = true;
                return;
            }

            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);
            environment.SurfaceEmergence01 = math.max(environment.SurfaceEmergence01, safeIntensity);
            environment.WaterlineBreach01 = math.max(environment.WaterlineBreach01, safeIntensity);
            environment.Frame = _frameCounter;
            _forceImmediateSimulation = true;
        }

        public void RequestWipeVisor(float strength01 = 1f)
        {
            EnsureNativeState();
            float safeStrength = Sanitize01(strength01);
            if (_hasScheduledWork)
            {
                _pendingWipeCommand01 = math.max(_pendingWipeCommand01, safeStrength);
                _forceImmediateSimulation = true;
                return;
            }

            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);
            environment.WipeCommand01 = math.max(environment.WipeCommand01, safeStrength);
            environment.Frame = _frameCounter;
            _forceImmediateSimulation = true;
        }

        public bool TryReloadCsvOverrides()
        {
            EnsureNativeState();
            if (_hasScheduledWork)
                return false;

            string root = Path.Combine(Application.dataPath, "..");
            string path = Path.Combine(root, CsvRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                NativeArray<byte> bytes = OpenVaultArray(ref _csvBytesHandle, CsvByteBufferId, CsvBufferBytes);
                int length = FillByteBufferFromFile(path, bytes);
                ref VisorLensTuningDTO tuning = ref GetVaultElementRef(ref _tuningHandle, TuningBufferId, 1, 0);
                ParseVisorCsv(bytes, length, ref tuning);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void EnsureNativeState()
        {
            if (_nativeReady)
                return;

            IDataVault vault = EnsureVault();
            _vault = vault;
            try
            {
                _stateHandle = AcquireBuffer<VisorStateDTO>(StateBufferId, 1);
                _tuningHandle = AcquireBuffer<VisorLensTuningDTO>(TuningBufferId, 1);
                _physiologyHandle = AcquireBuffer<MockPhysiologySignal>(PhysiologyBufferId, 1);
                _environmentHandle = AcquireBuffer<MockVisorEnvironmentSignal>(EnvironmentBufferId, 1);
                _gpuGlobalsHandle = AcquireBuffer<DiegeticVisorLensGpuGlobalsDTO>(GpuGlobalsBufferId, 1);
                _telemetryHandle = AcquireBuffer<VisorLensTelemetryEntry>(TelemetryRingBufferId, TelemetryCapacity);
                _telemetryCursorHandle = AcquireBuffer<int>(TelemetryCursorBufferId, 1);
                _csvBytesHandle = AcquireBuffer<byte>(CsvByteBufferId, CsvBufferBytes);
                _binaryProbeBytesHandle = AcquireBuffer<byte>(BinaryProbeByteBufferId, BinaryProbeBytes);
                _nanFlagsHandle = AcquireBuffer<int>(NanFlagBufferId, 1);
                ClearNativeBuffersWithMemClear();
                _nativeReady = true;
                PrewarmSignalLanes();
                GenerateEmergencyMockVisorData();
                TryReloadCsvOverrides();
                ProbeColdBinaryPayloads();
                EnsureGpuBuffer();
                ClearGpuGlobals();
                _simulationAccumulator = ResolveSimulationInterval(ResolveQualityWeight());
            }
            catch
            {
                ReleaseNativeState(vault, clearVault: false);
                throw;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteScheduledWorkForTeardown();
                IDataVault previousVault = previousService as IDataVault;
                if (previousVault == null)
                    previousVault = _vault;

                ReleaseNativeState(previousVault, clearVault: false);
                _vault = currentService as IDataVault;
                if (_vault != null)
                    EnsureNativeState();
            }
        }

        private IDataVault EnsureVault()
        {
            if (_vault != null)
                return _vault;

            throw new InvalidOperationException("DiegeticVisorLensRuntime requires GlobalDataVault before boot.");
        }

        private void CacheRegistryServicesCold()
        {
            if (_vault == null)
            {
                _vault = GlobalRegistry.DataVault;
                if (_vault == null && GlobalRegistry.TryGet(out IDataVault vault))
                    _vault = vault;
            }

            if (_playerContext == null)
                _playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
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

        private VaultGenerationHandle<T> AcquireBuffer<T>(BufferID id, int length) where T : struct
        {
            IDataVault vault = EnsureVault();
            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(id, length, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            if (!TryResolveVaultArray(vault, in handle, id, length, out _))
                throw new InvalidOperationException("DiegeticVisorLensRuntime failed to acquire Vault descriptor.");

            return handle;
        }

        private void ReleaseNativeState(IDataVault vault, bool clearVault)
        {
            ReleaseVisorVaultHandles(vault);
            _nativeReady = false;
            _hasGpuGlobals = false;
            _hasUploadedGpuGlobals = false;
            _lastGpuGlobals = default;
            _uploadedGpuGlobals = default;
            if (clearVault)
                _vault = null;
        }

        private void ReleaseVisorVaultHandles(IDataVault vault)
        {
            ReleaseVisorVaultHandle(vault, ref _stateHandle, StateBufferId);
            ReleaseVisorVaultHandle(vault, ref _tuningHandle, TuningBufferId);
            ReleaseVisorVaultHandle(vault, ref _physiologyHandle, PhysiologyBufferId);
            ReleaseVisorVaultHandle(vault, ref _environmentHandle, EnvironmentBufferId);
            ReleaseVisorVaultHandle(vault, ref _gpuGlobalsHandle, GpuGlobalsBufferId);
            ReleaseVisorVaultHandle(vault, ref _telemetryHandle, TelemetryRingBufferId);
            ReleaseVisorVaultHandle(vault, ref _telemetryCursorHandle, TelemetryCursorBufferId);
            ReleaseVisorVaultHandle(vault, ref _csvBytesHandle, CsvByteBufferId);
            ReleaseVisorVaultHandle(vault, ref _binaryProbeBytesHandle, BinaryProbeByteBufferId);
            ReleaseVisorVaultHandle(vault, ref _nanFlagsHandle, NanFlagBufferId);
        }

        private NativeArray<T> OpenVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID id,
            int length) where T : struct
        {
            IDataVault vault = EnsureVault();
            if (!TryResolveVaultArray(vault, in handle, id, length, out NativeArray<T> buffer))
                throw new InvalidOperationException("DiegeticVisorLensRuntime Vault descriptor is unavailable.");

            return buffer;
        }

        private ref T GetVaultElementRef<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID id,
            int length,
            int index) where T : struct
        {
            NativeArray<T> buffer = OpenVaultArray(ref handle, id, length);
            if ((uint)index >= (uint)buffer.Length)
                throw new InvalidOperationException("DiegeticVisorLensRuntime Vault index is out of range.");

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            return ref UnsafeUtility.ArrayElementAsRef<T>(ptr, index);
        }

        private static bool TryResolveVaultArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            int length,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   length > 0 &&
                   IsVisorVaultHandle(in handle, id) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= length;
        }

        private static bool TryReadVaultArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            int length,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   length > 0 &&
                   IsVisorVaultHandle(in handle, id) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= length;
        }

        private static bool IsVisorVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID id) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)id) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVisorVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID id) where T : struct
        {
            if (vault != null && IsVisorVaultHandle(in handle, id))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearNativeBuffersWithMemClear()
        {
            MemClearArray(OpenVaultArray(ref _stateHandle, StateBufferId, 1));
            MemClearArray(OpenVaultArray(ref _tuningHandle, TuningBufferId, 1));
            MemClearArray(OpenVaultArray(ref _physiologyHandle, PhysiologyBufferId, 1));
            MemClearArray(OpenVaultArray(ref _environmentHandle, EnvironmentBufferId, 1));
            MemClearArray(OpenVaultArray(ref _gpuGlobalsHandle, GpuGlobalsBufferId, 1));
            MemClearArray(OpenVaultArray(ref _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity));
            MemClearArray(OpenVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1));
            MemClearArray(OpenVaultArray(ref _csvBytesHandle, CsvByteBufferId, CsvBufferBytes));
            MemClearArray(OpenVaultArray(ref _binaryProbeBytesHandle, BinaryProbeByteBufferId, BinaryProbeBytes));
            MemClearArray(OpenVaultArray(ref _nanFlagsHandle, NanFlagBufferId, 1));
        }

        private static void MemClearArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private void ScheduleSimulation(float deltaTime, float qualityWeight)
        {
            VisorCondensationJob job = new VisorCondensationJob
            {
                State = OpenVaultArray(ref _stateHandle, StateBufferId, 1),
                Tuning = OpenVaultArray(ref _tuningHandle, TuningBufferId, 1),
                Physiology = OpenVaultArray(ref _physiologyHandle, PhysiologyBufferId, 1),
                Environment = OpenVaultArray(ref _environmentHandle, EnvironmentBufferId, 1),
                GpuGlobals = OpenVaultArray(ref _gpuGlobalsHandle, GpuGlobalsBufferId, 1),
                NanFlags = OpenVaultArray(ref _nanFlagsHandle, NanFlagBufferId, 1),
                DeltaTime = deltaTime,
                GlobalQualityWeight = qualityWeight,
                HeadAngularVelocity = _headAngularVelocity,
                Frame = _frameCounter
            };

            _scheduledHandle = job.Schedule();
            _hasScheduledWork = true;
        }

        private bool TryFinalizeScheduledWorkNoWait()
        {
            if (!_hasScheduledWork)
                return true;

            if (!_scheduledHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledHandle))
                return false;

            return FinishScheduledWork();
        }

        private bool CompleteScheduledWorkForTeardown()
        {
            if (!_hasScheduledWork)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true))
                return false;

            return FinishScheduledWork();
        }

        private bool FinishScheduledWork()
        {
            _hasScheduledWork = false;

            NativeArray<VisorStateDTO> states = OpenVaultArray(ref _stateHandle, StateBufferId, 1);
            NativeArray<DiegeticVisorLensGpuGlobalsDTO> globals = OpenVaultArray(ref _gpuGlobalsHandle, GpuGlobalsBufferId, 1);
            NativeArray<int> nanFlagsBuffer = OpenVaultArray(ref _nanFlagsHandle, NanFlagBufferId, 1);
            VisorStateDTO state = states[0];
            _lastGpuGlobals = globals[0];
            _hasGpuGlobals = true;
            int nanFlags = nanFlagsBuffer[0];
            WriteTelemetryFrame(in state, in _lastGpuGlobals, nanFlags);
            if (nanFlags != 0)
            {
                DumpBlackBoxOnce((uint)nanFlags);
                nanFlagsBuffer[0] = 0;
            }

            TryPublishBreach(in state);
            return true;
        }

        private void UploadGpuGlobals()
        {
            if (!_hasGpuGlobals)
                return;

            long uploadStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            bool bufferAvailable = EnsureGpuBuffer();
            bool globalsChanged = !_hasUploadedGpuGlobals || !GpuGlobalsEqual(in _uploadedGpuGlobals, in _lastGpuGlobals);
            if (globalsChanged)
            {
                PublishGpuGlobalVectors(in _lastGpuGlobals);
                if (bufferAvailable)
                {
                    GraphicsBuffer writeBuffer = ResolveNextGpuGlobalsBuffer();
                    NativeArray<DiegeticVisorLensGpuGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<DiegeticVisorLensGpuGlobalsDTO>(0, 1);
                    mapped[0] = _lastGpuGlobals;
                    writeBuffer.UnlockBufferAfterWrite<DiegeticVisorLensGpuGlobalsDTO>(1);
                    _activeGpuGlobalsBuffer = writeBuffer;
                }

                _uploadedGpuGlobals = _lastGpuGlobals;
                _hasUploadedGpuGlobals = true;
            }

            if (bufferAvailable && _activeGpuGlobalsBuffer != null && _activeGpuGlobalsBuffer.IsValid())
                Shader.SetGlobalConstantBuffer(GpuGlobalsNameId, _activeGpuGlobalsBuffer, 0, GpuGlobalsStrideBytes);

            long uploadTicks = System.Diagnostics.Stopwatch.GetTimestamp() - uploadStartTicks;
            _lastShaderUpdateComputeTimeNs = TicksToNanoseconds(uploadTicks);
            PatchLatestTelemetryShaderUpdateNs(_lastShaderUpdateComputeTimeNs);
        }

        private bool EnsureGpuBuffer()
        {
            if (!SystemInfo.supportsSetConstantBuffer)
            {
                ReleaseGpuBuffer();
                return false;
            }

            if (_gpuGlobalsBufferA != null && _gpuGlobalsBufferA.IsValid() &&
                _gpuGlobalsBufferB != null && _gpuGlobalsBufferB.IsValid())
                return true;

            ReleaseGpuBuffer();
            // COLD ALLOC: GraphicsBuffer[2] - ping-pong visor lens scalar CBuffers - owner: DiegeticVisorLensRuntime
            _gpuGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GpuGlobalsStrideBytes);
            _gpuGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GpuGlobalsStrideBytes);
            _hasUploadedGpuGlobals = false;
            return _gpuGlobalsBufferA.IsValid() && _gpuGlobalsBufferB.IsValid();
        }

        private void ReleaseGpuBuffer()
        {
            _gpuGlobalsBufferA?.Release();
            _gpuGlobalsBufferB?.Release();
            _gpuGlobalsBufferA = null;
            _gpuGlobalsBufferB = null;
            _activeGpuGlobalsBuffer = null;
            _hasUploadedGpuGlobals = false;
        }

        private GraphicsBuffer ResolveNextGpuGlobalsBuffer()
        {
            _gpuGlobalsWriteIndex ^= 1;
            return _gpuGlobalsWriteIndex == 0 ? _gpuGlobalsBufferA : _gpuGlobalsBufferB;
        }

        private static void PublishGpuGlobalVectors(in DiegeticVisorLensGpuGlobalsDTO globals)
        {
            Shader.SetGlobalVector(LensStateId, ToVector4(globals.State));
            Shader.SetGlobalVector(LensParams0Id, ToVector4(globals.Params0));
            Shader.SetGlobalVector(LensParams1Id, ToVector4(globals.Params1));
            Shader.SetGlobalVector(LensParams2Id, ToVector4(globals.Params2));
        }

        private void ClearGpuGlobals()
        {
            DiegeticVisorLensGpuGlobalsDTO globals = default;
            globals.Params0.w = 1f;
            globals.Params1.x = 1f;
            PublishGpuGlobalVectors(in globals);
            if (_gpuGlobalsBufferA != null && _gpuGlobalsBufferA.IsValid() &&
                _gpuGlobalsBufferB != null && _gpuGlobalsBufferB.IsValid())
            {
                NativeArray<DiegeticVisorLensGpuGlobalsDTO> mappedA = _gpuGlobalsBufferA.LockBufferForWrite<DiegeticVisorLensGpuGlobalsDTO>(0, 1);
                mappedA[0] = globals;
                _gpuGlobalsBufferA.UnlockBufferAfterWrite<DiegeticVisorLensGpuGlobalsDTO>(1);
                NativeArray<DiegeticVisorLensGpuGlobalsDTO> mappedB = _gpuGlobalsBufferB.LockBufferForWrite<DiegeticVisorLensGpuGlobalsDTO>(0, 1);
                mappedB[0] = globals;
                _gpuGlobalsBufferB.UnlockBufferAfterWrite<DiegeticVisorLensGpuGlobalsDTO>(1);
                _activeGpuGlobalsBuffer = _gpuGlobalsBufferA;
                Shader.SetGlobalConstantBuffer(GpuGlobalsNameId, _activeGpuGlobalsBuffer, 0, GpuGlobalsStrideBytes);
            }

            _uploadedGpuGlobals = globals;
            _hasUploadedGpuGlobals = true;
            _hasGpuGlobals = false;
        }

        private void IngestCoreSignals(float deltaTime)
        {
            ref MockPhysiologySignal physiology = ref GetVaultElementRef(ref _physiologyHandle, PhysiologyBufferId, 1, 0);
            ref MockVisorEnvironmentSignal environment = ref GetVaultElementRef(ref _environmentHandle, EnvironmentBufferId, 1, 0);

            physiology.Frame = _frameCounter;
            environment.Frame = _frameCounter;
            physiology.BreathSpike01 = math.max(0f, physiology.BreathSpike01 - deltaTime * 0.8f);
            environment.Corruption01 = math.max(0f, environment.Corruption01 - deltaTime * 0.12f);
            environment.SiltDensity01 = math.max(0f, environment.SiltDensity01 - deltaTime * 0.08f);
            ApplyPendingExternalInputs(ref physiology, ref environment);

            ReadOnlySpan<PlayerExhaleSignal> exhales = SignalBus<PlayerExhaleSignal>.GetFrameSnapshot();
            if (exhales.Length > 0)
            {
                physiology.BreathSpike01 = 1f;
                physiology.RespirationRate = math.max(SanitizeRange(physiology.RespirationRate, 4f, 44f, 12f), math.min(44f, 16f + exhales.Length * 2f));
                _forceImmediateSimulation = true;
            }

            ReadOnlySpan<PlayerWaterSplashSignal> splashes = SignalBus<PlayerWaterSplashSignal>.GetFrameSnapshot();
            for (int i = 0; i < splashes.Length; i++)
            {
                ref readonly PlayerWaterSplashSignal signal = ref splashes[i];
                float intensity = Sanitize01(signal.Intensity01);
                environment.SurfaceEmergence01 = math.max(environment.SurfaceEmergence01, intensity);
                if (signal.IsSubmerged != 0 || signal.VerticalSpeed < -0.35f)
                {
                    environment.WaterlineBreach01 = math.max(environment.WaterlineBreach01, intensity);
                    _forceImmediateSimulation = true;
                }
            }

            ReadOnlySpan<PlayerFatalPressureSignal> pressureSignals = SignalBus<PlayerFatalPressureSignal>.GetFrameSnapshot();
            for (int i = 0; i < pressureSignals.Length; i++)
            {
                environment.ExternalPressure01 = math.max(environment.ExternalPressure01, Sanitize01(pressureSignals[i].Intensity01));
                _forceImmediateSimulation = true;
            }

            ReadOnlySpan<SystemGlitchSignal> glitches = SignalBus<SystemGlitchSignal>.GetFrameSnapshot();
            for (int i = 0; i < glitches.Length; i++)
            {
                environment.Corruption01 = math.max(environment.Corruption01, Sanitize01(glitches[i].Intensity01));
                _forceImmediateSimulation = true;
            }
        }

        private void ApplyPendingMockResetIfNeeded()
        {
            if (!_pendingMockReset)
                return;

            _pendingMockReset = false;
            ApplyEmergencyMockVisorData();
        }

        private void ApplyPendingExternalInputs(ref MockPhysiologySignal physiology, ref MockVisorEnvironmentSignal environment)
        {
            bool applied = false;
            if (_hasPendingPhysiology)
            {
                physiology.RespirationRate = _pendingRespirationRate;
                physiology.HeartRate = _pendingHeartRate;
                physiology.CoreTemperatureC = _pendingCoreTemperatureC;
                _hasPendingPhysiology = false;
                applied = true;
            }

            if (_hasPendingExternalPressure)
            {
                environment.ExternalPressure01 = math.max(environment.ExternalPressure01, _pendingExternalPressure01);
                _pendingExternalPressure01 = 0f;
                _hasPendingExternalPressure = false;
                applied = true;
            }

            if (_hasPendingEnvironment)
            {
                environment.ExternalWaterTemperatureC = _pendingWaterTemperatureC;
                environment.SiltDensity01 = math.max(environment.SiltDensity01, _pendingSiltDensity01);
                environment.Darkness01 = _pendingDarkness01;
                environment.Corruption01 = math.max(environment.Corruption01, _pendingCorruption01);
                _pendingSiltDensity01 = 0f;
                _pendingCorruption01 = 0f;
                _hasPendingEnvironment = false;
                applied = true;
            }

            if (_pendingSurfaceEmergence01 > 0f)
            {
                environment.SurfaceEmergence01 = math.max(environment.SurfaceEmergence01, _pendingSurfaceEmergence01);
                environment.WaterlineBreach01 = math.max(environment.WaterlineBreach01, _pendingSurfaceEmergence01);
                _pendingSurfaceEmergence01 = 0f;
                applied = true;
            }

            if (_pendingWipeCommand01 > 0f)
            {
                environment.WipeCommand01 = math.max(environment.WipeCommand01, _pendingWipeCommand01);
                _pendingWipeCommand01 = 0f;
                applied = true;
            }

            if (applied)
                _forceImmediateSimulation = true;
        }

        private void ClearPendingExternalInputs()
        {
            _pendingRespirationRate = 0f;
            _pendingHeartRate = 0f;
            _pendingCoreTemperatureC = 0f;
            _pendingExternalPressure01 = 0f;
            _pendingWaterTemperatureC = 0f;
            _pendingSiltDensity01 = 0f;
            _pendingDarkness01 = 0f;
            _pendingCorruption01 = 0f;
            _pendingSurfaceEmergence01 = 0f;
            _pendingWipeCommand01 = 0f;
            _hasPendingPhysiology = false;
            _hasPendingExternalPressure = false;
            _hasPendingEnvironment = false;
            _pendingMockReset = false;
        }

        private void UpdateHeadAngularVelocity(float deltaTime)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
            {
                _headAngularVelocity = math.lerp(_headAngularVelocity, float3.zero, math.saturate(deltaTime * 8f));
                _hasLastCameraRotation = false;
                return;
            }

            Quaternion current = playerCamera.transform.rotation;
            if (!_hasLastCameraRotation || deltaTime <= MinimumDeltaTime)
            {
                _lastCameraRotation = current;
                _hasLastCameraRotation = true;
                _headAngularVelocity = math.lerp(_headAngularVelocity, float3.zero, math.saturate(deltaTime * 8f));
                return;
            }

            Quaternion delta = current * Quaternion.Inverse(_lastCameraRotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            Vector3 angular = axis * (angleDegrees * 0.0174532924f / math.max(deltaTime, MinimumDeltaTime));
            _headAngularVelocity = math.isfinite(angular.x) && math.isfinite(angular.y) && math.isfinite(angular.z)
                ? new float3(angular.x, angular.y, angular.z)
                : float3.zero;
            _lastCameraRotation = current;
        }

        private void TryPublishBreach(in VisorStateDTO state)
        {
            if (state.CrackSeverity <= 0.8f || _breachCooldown > 0f)
                return;

            MockVisorEnvironmentSignal environment = OpenVaultArray(ref _environmentHandle, EnvironmentBufferId, 1)[0];
            VisorBreachSignal signal = default;
            signal.SourceId = RuntimeSourceHash;
            signal.Frame = _frameCounter;
            signal.CrackSeverity01 = Sanitize01(state.CrackSeverity);
            signal.ExternalPressure01 = Sanitize01(environment.ExternalPressure01);
            signal.Condensation01 = Sanitize01(state.CondensationLevel);
            signal.Flags = 1u;
            signal.Sequence = _breachSequence++;
            SignalBus<VisorBreachSignal>.TryPush(in signal);
            _breachCooldown = BreachPublishCooldownSeconds;
        }

        private void WriteTelemetryFrame(in VisorStateDTO state, in DiegeticVisorLensGpuGlobalsDTO globals, int nanFlags)
        {
            NativeArray<VisorLensTelemetryEntry> ring = OpenVaultArray(ref _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity);
            NativeArray<int> cursorBuffer = OpenVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1);
            if (!ring.IsCreated || ring.Length < TelemetryCapacity || !cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                return;

            int cursor = cursorBuffer[0];
            if (cursor < 0 || cursor >= TelemetryCapacity)
                cursor = 0;

            MockPhysiologySignal physiology = OpenVaultArray(ref _physiologyHandle, PhysiologyBufferId, 1)[0];
            MockVisorEnvironmentSignal environment = OpenVaultArray(ref _environmentHandle, EnvironmentBufferId, 1)[0];
            ring[cursor] = new VisorLensTelemetryEntry
            {
                Frame = _frameCounter,
                Flags = (uint)nanFlags,
                Condensation01 = Sanitize01(state.CondensationLevel),
                Droplets01 = Sanitize01(state.WaterDropletIntensity),
                Crack01 = Sanitize01(state.CrackSeverity),
                Dirt01 = Sanitize01(state.DirtAccumulation),
                Quality01 = ResolveQualityWeight(),
                RespirationRate = SanitizeRange(physiology.RespirationRate, 0f, 80f, 0f),
                ExternalPressure01 = Sanitize01(environment.ExternalPressure01),
                SiltDensity01 = Sanitize01(environment.SiltDensity01),
                HeadAngularSpeed = math.length(_headAngularVelocity),
                StateHash = BuildStateHash(in state),
                GpuStateHash = BuildGpuHash(in globals),
                RefractionScale01 = Sanitize01(globals.Params0.w),
                ShaderUpdateComputeTimeNs = _lastShaderUpdateComputeTimeNs,
                Anomaly01 = Sanitize01(environment.Corruption01)
            };

            cursorBuffer[0] = cursor + 1 >= TelemetryCapacity ? 0 : cursor + 1;
        }

        private void PatchLatestTelemetryShaderUpdateNs(uint shaderUpdateComputeTimeNs)
        {
            NativeArray<VisorLensTelemetryEntry> ring = OpenVaultArray(ref _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity);
            NativeArray<int> cursorBuffer = OpenVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1);
            if (!ring.IsCreated || ring.Length < TelemetryCapacity || !cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                return;

            int cursor = cursorBuffer[0] - 1;
            if (cursor < 0)
                cursor = TelemetryCapacity - 1;

            VisorLensTelemetryEntry entry = ring[cursor];
            entry.ShaderUpdateComputeTimeNs = shaderUpdateComputeTimeNs;
            ring[cursor] = entry;
        }

        private void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (_blackBoxDumped)
                return;

            _blackBoxDumped = true;
            try
            {
                NativeArray<VisorLensTelemetryEntry> ring = OpenVaultArray(ref _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity);
                NativeArray<int> cursorBuffer = OpenVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1);
                if (!ring.IsCreated || ring.Length < TelemetryCapacity || !cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                    return;

                string path = Path.Combine(Application.dataPath, "..", DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpVersion);
                    writer.Write(reasonFlags);
                    writer.Write(UnsafeUtility.SizeOf<VisorLensTelemetryEntry>());
                    writer.Write(TelemetryCapacity);
                    int index = cursorBuffer[0];
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        if (index >= TelemetryCapacity)
                            index = 0;

                        WriteTelemetryEntry(writer, ring[index]);
                        index++;
                    }
                }
            }
            catch (Exception)
            {
                _blackBoxDumped = true;
            }
        }

        private void ProbeColdBinaryPayloads()
        {
            if (_binaryProbePerformed)
                return;

            _binaryProbePerformed = true;
            string root = Path.Combine(Application.dataPath, "..");
            if (ProbeFixedBinary(Path.Combine(root, "Docs", "Archive", "Batch005", "Data", "visor_materials_006.h8bin")) ||
                ProbeFixedBinary(Path.Combine(root, "Docs", "Archive", "Batch006", "Data", "visor_materials_006.h8bin")) ||
                ProbeFixedBinary(Path.Combine(root, "Docs", "Archive", "Batch007", "Data", "visor_materials_006.h8bin")) ||
                ProbeFixedBinary(Path.Combine(root, "StreamingAssets", "visor_materials_006.h8bin")))
            {
                _mockDataActive = false;
            }
        }

        private bool ProbeFixedBinary(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                NativeArray<byte> bytes = OpenVaultArray(ref _binaryProbeBytesHandle, BinaryProbeByteBufferId, BinaryProbeBytes);
                int length = FillByteBufferFromFile(path, bytes);
                if (length < 4)
                    return false;

                uint magic = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
                return magic == 0x56534D38u || ReverseBytes(magic) == 0x56534D38u;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int FillByteBufferFromFile(string path, NativeArray<byte> buffer)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long cappedLength = stream.Length;
                if (cappedLength > buffer.Length)
                    cappedLength = buffer.Length;

                int length = (int)cappedLength;
                for (int i = 0; i < length; i++)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        return i;

                    buffer[i] = (byte)value;
                }

                return length;
            }
        }

        private static void ParseVisorCsv(NativeArray<byte> bytes, int length, ref VisorLensTuningDTO tuning)
        {
            int cursor = 0;
            while (cursor < length)
            {
                uint keyHash = 2166136261u;
                while (cursor < length)
                {
                    byte c = bytes[cursor++];
                    if (c == (byte)',' || c == (byte)'=' || c == (byte)'\t')
                        break;

                    if (c == (byte)'\n' || c == (byte)'\r')
                    {
                        keyHash = 0u;
                        break;
                    }

                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);

                    keyHash ^= c;
                    keyHash *= 16777619u;
                }

                if (keyHash == 0u)
                {
                    SkipLine(bytes, length, ref cursor);
                    continue;
                }

                float value = ParseFloat(bytes, length, ref cursor);
                ApplyCsvValue(keyHash, value, ref tuning);
                SkipLine(bytes, length, ref cursor);
            }
        }

        private static void ApplyCsvValue(uint keyHash, float value, ref VisorLensTuningDTO tuning)
        {
            if (!math.isfinite(value))
                return;

            if (keyHash == HashLowerAscii("fog_rate"))
                tuning.FogRate = math.max(0f, value);
            else if (keyHash == HashLowerAscii("fog_breath_gain"))
                tuning.FogBreathGain = math.max(0f, value);
            else if (keyHash == HashLowerAscii("clearing_rate"))
                tuning.ClearingRate = math.max(0f, value);
            else if (keyHash == HashLowerAscii("droplet_drain_seconds"))
                tuning.DropletDrainSeconds = math.max(0.25f, value);
            else if (keyHash == HashLowerAscii("crack_pressure_threshold"))
                tuning.CrackPressureThreshold = math.saturate(value);
            else if (keyHash == HashLowerAscii("crack_growth_rate"))
                tuning.CrackGrowthRate = math.max(0f, value);
            else if (keyHash == HashLowerAscii("dirt_silt_gain"))
                tuning.DirtSiltGain = math.max(0f, value);
            else if (keyHash == HashLowerAscii("wipe_strength"))
                tuning.WipeStrength = math.max(0f, value);
            else if (keyHash == HashLowerAscii("low_refraction_quality_cutoff"))
                tuning.LowRefractionQualityCutoff = math.saturate(value);

            tuning.Version++;
        }

        private static float ParseFloat(NativeArray<byte> bytes, int length, ref int cursor)
        {
            bool negative = false;
            float integer = 0f;
            float fraction = 0f;
            float divisor = 1f;
            bool inFraction = false;

            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c == (byte)'-' && !negative && integer == 0f && fraction == 0f)
                {
                    negative = true;
                    cursor++;
                    continue;
                }

                if (c == (byte)'.')
                {
                    inFraction = true;
                    cursor++;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    break;

                float digit = c - (byte)'0';
                if (inFraction)
                {
                    divisor *= 10f;
                    fraction += digit / divisor;
                }
                else
                {
                    integer = integer * 10f + digit;
                }

                cursor++;
            }

            float value = integer + fraction;
            return negative ? -value : value;
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte c = bytes[cursor++];
                if (c == (byte)'\n')
                    break;
            }
        }

        private static void PrewarmSignalLanes()
        {
            SignalBus<VisorBreachSignal>.Configure(8, maxFrameSignals: 8, lowTierFrameSignals: 8, laneHash: VisorBreachLaneHash);
            SignalBus<VisorBreachSignal>.EnsureInitialized();
            SignalBus<PlayerExhaleSignal>.EnsureInitialized();
            SignalBus<PlayerWaterSplashSignal>.EnsureInitialized();
            SignalBus<PlayerFatalPressureSignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
        }

        private static bool GpuGlobalsEqual(in DiegeticVisorLensGpuGlobalsDTO left, in DiegeticVisorLensGpuGlobalsDTO right)
        {
            return Float4Approximately(left.State, right.State) &&
                   Float4Approximately(left.Params0, right.Params0) &&
                   Float4Approximately(left.Params1, right.Params1) &&
                   Float4Approximately(left.Params2, right.Params2);
        }

        private static bool Float4Approximately(float4 left, float4 right)
        {
            float4 delta = math.abs(left - right);
            return delta.x <= 0.0001f && delta.y <= 0.0001f && delta.z <= 0.0001f && delta.w <= 0.0001f;
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private static uint BuildStateHash(in VisorStateDTO state)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, math.asuint(Sanitize01(state.CondensationLevel)));
            hash = MixHash(hash, math.asuint(Sanitize01(state.WaterDropletIntensity)));
            hash = MixHash(hash, math.asuint(Sanitize01(state.CrackSeverity)));
            hash = MixHash(hash, math.asuint(Sanitize01(state.DirtAccumulation)));
            return hash;
        }

        private static uint BuildGpuHash(in DiegeticVisorLensGpuGlobalsDTO globals)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, math.asuint(globals.State.x));
            hash = MixHash(hash, math.asuint(globals.State.y));
            hash = MixHash(hash, math.asuint(globals.State.z));
            hash = MixHash(hash, math.asuint(globals.State.w));
            hash = MixHash(hash, math.asuint(globals.Params0.x));
            hash = MixHash(hash, math.asuint(globals.Params0.y));
            hash = MixHash(hash, math.asuint(globals.Params0.z));
            hash = MixHash(hash, math.asuint(globals.Params0.w));
            return hash;
        }

        private static uint MixHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static uint HashLowerAscii(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= (uint)c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static float ResolveQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 0.5f;
        }

        private static float ResolveSimulationInterval(float qualityWeight)
        {
            float quality = math.saturate(qualityWeight);
            float dynamicWeight = Smooth01Static((quality - 0.1f) * (1f / 0.9f));
            float hz = math.lerp(5f, 60f, dynamicWeight);
            return math.rcp(math.max(5f, hz));
        }

        private static float Smooth01Static(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float SanitizeDelta(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeRange(float value, float minimum, float maximum, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, VisorLensTelemetryEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.Flags);
            writer.Write(entry.Condensation01);
            writer.Write(entry.Droplets01);
            writer.Write(entry.Crack01);
            writer.Write(entry.Dirt01);
            writer.Write(entry.Quality01);
            writer.Write(entry.RespirationRate);
            writer.Write(entry.ExternalPressure01);
            writer.Write(entry.SiltDensity01);
            writer.Write(entry.HeadAngularSpeed);
            writer.Write(entry.StateHash);
            writer.Write(entry.GpuStateHash);
            writer.Write(entry.RefractionScale01);
            writer.Write(entry.ShaderUpdateComputeTimeNs);
            writer.Write(entry.Anomaly01);
        }

        private static uint TicksToNanoseconds(long ticks)
        {
            if (ticks <= 0)
                return 0u;

            long frequency = System.Diagnostics.Stopwatch.Frequency;
            if (frequency <= 0)
                return 0u;

            long maxSafeTicks = long.MaxValue / 1000000000L;
            if (ticks >= maxSafeTicks)
                return uint.MaxValue;

            long nanoseconds = (ticks * 1000000000L) / frequency;
            if (nanoseconds <= 0)
                return 0u;

            return nanoseconds >= uint.MaxValue ? uint.MaxValue : (uint)nanoseconds;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct VisorCondensationJob : IJob
        {
            [NoAlias] public NativeArray<VisorStateDTO> State;
            [NoAlias, ReadOnly] public NativeArray<VisorLensTuningDTO> Tuning;
            [NoAlias] public NativeArray<MockPhysiologySignal> Physiology;
            [NoAlias] public NativeArray<MockVisorEnvironmentSignal> Environment;
            [NoAlias] public NativeArray<DiegeticVisorLensGpuGlobalsDTO> GpuGlobals;
            [NoAlias] public NativeArray<int> NanFlags;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public float3 HeadAngularVelocity;
            public uint Frame;

            public void Execute()
            {
                float dt = math.max(0f, math.isfinite(DeltaTime) ? DeltaTime : 0f);
                float quality = Sanitize01Job(GlobalQualityWeight);
                VisorStateDTO state = SanitizeState(State[0]);
                VisorLensTuningDTO tuning = SanitizeTuning(Tuning[0]);
                MockPhysiologySignal physiology = SanitizePhysiology(Physiology[0]);
                MockVisorEnvironmentSignal environment = SanitizeEnvironment(Environment[0]);

                uint anomalyHash = math.hash(new uint3(Frame + 17u, math.asuint(environment.Corruption01), math.asuint(state.CrackSeverity)));
                float anomalyNoise = ((anomalyHash & 1023u) * (1f / 1023f) - 0.5f) * environment.Corruption01;
                float coldDrive = math.saturate((18f - environment.ExternalWaterTemperatureC) * (1f / 28f));
                float breathDrive = math.saturate((physiology.RespirationRate - 8f) * (1f / 28f)) * tuning.FogBreathGain;
                breathDrive += physiology.BreathSpike01 * tuning.FogBreathGain * 2.4f;
                float heartDrive = math.saturate((physiology.HeartRate - 72f) * (1f / 108f)) * tuning.HeartCondensationGain;
                float coreDrive = math.saturate((physiology.CoreTemperatureC - 36.5f) * 0.25f) * tuning.CoreTempCondensationGain;
                float fogAdd = (tuning.FogRate * coldDrive + breathDrive + heartDrive + coreDrive + math.max(0f, anomalyNoise) * tuning.AnomalyNoiseGain * 0.08f) * dt;
                state.CondensationLevel = math.saturate(state.CondensationLevel + fogAdd);
                float clearingRate = tuning.ClearingRate * math.lerp(1.4f, 0.72f, quality) * (1f + environment.WipeCommand01 * 2.5f);
                state.CondensationLevel = math.saturate(state.CondensationLevel * math.exp(-clearingRate * dt));

                float dropletSpike = math.max(environment.SurfaceEmergence01, environment.WaterlineBreach01);
                state.WaterDropletIntensity = math.saturate(math.max(state.WaterDropletIntensity, dropletSpike));
                float dropletDrain = dt * math.rcp(math.max(0.25f, tuning.DropletDrainSeconds));
                dropletDrain *= math.lerp(1.35f, 0.78f, quality);
                dropletDrain += environment.WipeCommand01 * tuning.WipeStrength * dt;
                state.WaterDropletIntensity = math.max(0f, state.WaterDropletIntensity - dropletDrain);

                float pressureOver = math.saturate((environment.ExternalPressure01 - tuning.CrackPressureThreshold) * math.rcp(math.max(0.01f, 1f - tuning.CrackPressureThreshold)));
                state.CrackSeverity = math.saturate(state.CrackSeverity + pressureOver * tuning.CrackGrowthRate * dt);
                state.CrackSeverity = math.saturate(state.CrackSeverity + math.max(0f, anomalyNoise) * tuning.AnomalyNoiseGain * dt * 0.18f);
                state.CrackSeverity = math.min(state.CrackSeverity, tuning.MaxCrackSeverity);

                float dirtGain = environment.SiltDensity01 * tuning.DirtSiltGain * dt * (0.35f + state.WaterDropletIntensity * 0.65f);
                state.DirtAccumulation = math.saturate(state.DirtAccumulation + dirtGain);
                state.DirtAccumulation = math.max(0f, state.DirtAccumulation - environment.WipeCommand01 * tuning.WipeStrength * dt);

                float dynamicBlend = Smooth01((quality - tuning.QualityStaticBlendStart) * math.rcp(math.max(0.01f, tuning.QualityDynamicBlendEnd - tuning.QualityStaticBlendStart)));
                float refractionScale = Smooth01((quality - tuning.LowRefractionQualityCutoff) * math.rcp(math.max(0.01f, 1f - tuning.LowRefractionQualityCutoff)));
                float3 angular = math.all(math.isfinite(HeadAngularVelocity)) ? HeadAngularVelocity : float3.zero;
                float headSpeed = math.length(angular);
                float2 gravity = new float2(angular.y * 0.18f + angular.z * 0.05f, -1f - angular.x * 0.08f);
                float gravityLenSq = math.max(0.0001f, math.lengthsq(gravity));
                gravity *= math.rsqrt(gravityLenSq);
                gravity *= tuning.DropletGravityStrength * dynamicBlend * state.WaterDropletIntensity;
                gravity = math.lerp(new float2(0f, -0.08f * state.WaterDropletIntensity), gravity, dynamicBlend);

                float reflection = math.saturate(environment.Darkness01 * tuning.ReflectionDarknessGain * (0.24f + quality * 0.76f));
                reflection = math.saturate(reflection + environment.Corruption01 * tuning.BiolumReflectionGain * 0.35f);
                uint flags = 0u;
                flags |= state.CrackSeverity > 0.8f ? 1u : 0u;
                flags |= refractionScale <= 0.05f ? 2u : 0u;
                flags |= environment.Corruption01 > 0.01f ? 4u : 0u;

                State[0] = state;
                Physiology[0] = new MockPhysiologySignal
                {
                    RespirationRate = physiology.RespirationRate,
                    HeartRate = physiology.HeartRate,
                    CoreTemperatureC = physiology.CoreTemperatureC,
                    BreathSpike01 = physiology.BreathSpike01 * math.exp(-3.2f * dt),
                    Frame = Frame,
                    Flags = physiology.Flags,
                    _pad0 = 0f,
                    _pad1 = 0f
                };
                Environment[0] = new MockVisorEnvironmentSignal
                {
                    ExternalWaterTemperatureC = environment.ExternalWaterTemperatureC,
                    ExternalPressure01 = environment.ExternalPressure01,
                    SiltDensity01 = math.max(0f, environment.SiltDensity01 - dt * 0.035f),
                    Darkness01 = environment.Darkness01,
                    SurfaceEmergence01 = math.max(0f, environment.SurfaceEmergence01 - dt * tuning.SurfaceWashDrainRate),
                    WipeCommand01 = math.max(0f, environment.WipeCommand01 - dt * 2.5f),
                    Corruption01 = math.max(0f, environment.Corruption01 - dt * 0.08f),
                    WaterlineBreach01 = math.max(0f, environment.WaterlineBreach01 - dt * 0.2f),
                    Frame = Frame,
                    Flags = environment.Flags,
                    _pad0 = 0f,
                    _pad1 = 0f
                };

                DiegeticVisorLensGpuGlobalsDTO gpu = new DiegeticVisorLensGpuGlobalsDTO
                {
                    State = new float4(state.CondensationLevel, state.WaterDropletIntensity, state.CrackSeverity, state.DirtAccumulation),
                    Params0 = new float4(gravity.x, gravity.y, reflection, refractionScale),
                    Params1 = new float4(quality, environment.Corruption01, math.max(environment.SurfaceEmergence01, environment.WaterlineBreach01), environment.Darkness01),
                    Params2 = new float4(environment.ExternalPressure01, environment.SiltDensity01, headSpeed, math.asfloat(flags))
                };
                GpuGlobals[0] = gpu;

                if (!FiniteState(state) || !math.all(math.isfinite(gpu.State)) || !math.all(math.isfinite(gpu.Params0)) ||
                    !math.all(math.isfinite(gpu.Params1)) || !math.all(math.isfinite(gpu.Params2)))
                {
                    NanFlags[0] = 1;
                }
            }

            private static VisorStateDTO SanitizeState(VisorStateDTO state)
            {
                state.CondensationLevel = Sanitize01Job(state.CondensationLevel);
                state.WaterDropletIntensity = Sanitize01Job(state.WaterDropletIntensity);
                state.CrackSeverity = Sanitize01Job(state.CrackSeverity);
                state.DirtAccumulation = Sanitize01Job(state.DirtAccumulation);
                return state;
            }

            private static VisorLensTuningDTO SanitizeTuning(VisorLensTuningDTO tuning)
            {
                tuning.FogRate = SanitizeNonNegative(tuning.FogRate, 0.08f);
                tuning.FogBreathGain = SanitizeNonNegative(tuning.FogBreathGain, 0.045f);
                tuning.FogColdGain = SanitizeNonNegative(tuning.FogColdGain, 0.035f);
                tuning.ClearingRate = SanitizeNonNegative(tuning.ClearingRate, 0.22f);
                tuning.DropletDrainSeconds = math.max(0.25f, SanitizeNonNegative(tuning.DropletDrainSeconds, 5f));
                tuning.DropletGravityStrength = SanitizeNonNegative(tuning.DropletGravityStrength, 0.42f);
                tuning.SurfaceWashDrainRate = SanitizeNonNegative(tuning.SurfaceWashDrainRate, 0.2f);
                tuning.CrackPressureThreshold = Sanitize01Job(tuning.CrackPressureThreshold);
                tuning.CrackGrowthRate = SanitizeNonNegative(tuning.CrackGrowthRate, 0.45f);
                tuning.MaxCrackSeverity = math.max(0f, Sanitize01Job(tuning.MaxCrackSeverity));
                tuning.DirtSiltGain = SanitizeNonNegative(tuning.DirtSiltGain, 0.04f);
                tuning.WipeStrength = SanitizeNonNegative(tuning.WipeStrength, 2.4f);
                tuning.ReflectionDarknessGain = SanitizeNonNegative(tuning.ReflectionDarknessGain, 0.64f);
                tuning.AnomalyNoiseGain = SanitizeNonNegative(tuning.AnomalyNoiseGain, 0.32f);
                tuning.LowRefractionQualityCutoff = Sanitize01Job(tuning.LowRefractionQualityCutoff);
                tuning.BiolumReflectionGain = SanitizeNonNegative(tuning.BiolumReflectionGain, 0.22f);
                tuning.HeartCondensationGain = SanitizeNonNegative(tuning.HeartCondensationGain, 0.012f);
                tuning.CoreTempCondensationGain = SanitizeNonNegative(tuning.CoreTempCondensationGain, 0.018f);
                tuning.QualityStaticBlendStart = Sanitize01Job(tuning.QualityStaticBlendStart);
                tuning.QualityDynamicBlendEnd = math.max(tuning.QualityStaticBlendStart + 0.01f, Sanitize01Job(tuning.QualityDynamicBlendEnd));
                return tuning;
            }

            private static MockPhysiologySignal SanitizePhysiology(MockPhysiologySignal signal)
            {
                signal.RespirationRate = SanitizeRangeJob(signal.RespirationRate, 4f, 44f, 12f);
                signal.HeartRate = SanitizeRangeJob(signal.HeartRate, 28f, 220f, 72f);
                signal.CoreTemperatureC = SanitizeRangeJob(signal.CoreTemperatureC, 28f, 43f, 37f);
                signal.BreathSpike01 = Sanitize01Job(signal.BreathSpike01);
                return signal;
            }

            private static MockVisorEnvironmentSignal SanitizeEnvironment(MockVisorEnvironmentSignal signal)
            {
                signal.ExternalWaterTemperatureC = SanitizeRangeJob(signal.ExternalWaterTemperatureC, -4f, 38f, 4f);
                signal.ExternalPressure01 = Sanitize01Job(signal.ExternalPressure01);
                signal.SiltDensity01 = Sanitize01Job(signal.SiltDensity01);
                signal.Darkness01 = Sanitize01Job(signal.Darkness01);
                signal.SurfaceEmergence01 = Sanitize01Job(signal.SurfaceEmergence01);
                signal.WipeCommand01 = Sanitize01Job(signal.WipeCommand01);
                signal.Corruption01 = Sanitize01Job(signal.Corruption01);
                signal.WaterlineBreach01 = Sanitize01Job(signal.WaterlineBreach01);
                return signal;
            }

            private static bool FiniteState(VisorStateDTO state)
            {
                return math.isfinite(state.CondensationLevel) &&
                       math.isfinite(state.WaterDropletIntensity) &&
                       math.isfinite(state.CrackSeverity) &&
                       math.isfinite(state.DirtAccumulation);
            }

            private static float Smooth01(float value)
            {
                float x = math.saturate(value);
                return x * x * (3f - 2f * x);
            }

            private static float Sanitize01Job(float value)
            {
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            private static float SanitizeNonNegative(float value, float fallback)
            {
                return math.isfinite(value) ? math.max(0f, value) : fallback;
            }

            private static float SanitizeRangeJob(float value, float minimum, float maximum, float fallback)
            {
                return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
            }
        }
    }
}
