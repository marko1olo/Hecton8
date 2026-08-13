using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    [DisallowMultipleComponent]
    public unsafe sealed class DiegeticVisorLensRuntime : MonoBehaviour, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DiegeticVisorLensRuntimeSignalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int CsvBufferBytes = 4096;
        private const int BinaryProbeBytes = 64;
        private const int GpuGlobalsStrideBytes = 64;
        private const float MinimumDeltaTime = 0.0001f;
        private const float BreachPublishCooldownSeconds = 0.35f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1335_DiegeticVisorLens.bin";
#if UNITY_EDITOR
        private const string CsvRelativePath = "visor_properties.csv";
#endif
        private const uint DumpMagic = 0x56534C44u;
        private const uint DumpVersion = 1u;
        private const uint VisorBreachLaneHash = 0x56534252u;
        private const uint RuntimeSourceHash = 0x53483635u;
        private const SystemID OwnerSystem = SystemID.Vfx;

        private static readonly BufferID StateBufferId = BufferID.DiegeticVisorLensRuntime_StateBufferId;
        private static readonly BufferID TuningBufferId = BufferID.DiegeticVisorLensRuntime_TuningBufferId;
        private static readonly BufferID PhysiologyBufferId = BufferID.DiegeticVisorLensRuntime_PhysiologyBufferId;
        private static readonly BufferID EnvironmentBufferId = BufferID.DiegeticVisorLensRuntime_EnvironmentBufferId;
        private static readonly BufferID GpuGlobalsBufferId = BufferID.DiegeticVisorLensRuntime_GpuGlobalsBufferId;
        private static readonly BufferID TelemetryRingBufferId = BufferID.DiegeticVisorLensRuntime_TelemetryRingBufferId;
        private static readonly BufferID TelemetryCursorBufferId = BufferID.DiegeticVisorLensRuntime_TelemetryCursorBufferId;
        private static readonly BufferID CsvByteBufferId = BufferID.DiegeticVisorLensRuntime_CsvByteBufferId;
        private static readonly BufferID BinaryProbeByteBufferId = BufferID.DiegeticVisorLensRuntime_BinaryProbeByteBufferId;
        private static readonly BufferID NanFlagBufferId = BufferID.DiegeticVisorLensRuntime_NanFlagBufferId;

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
        private bool _hasLastCameraRotation;
        private bool _nativeReady;
        private bool _hasGpuGlobals;
        private bool _hasUploadedGpuGlobals;
        private bool _blackBoxDumped;
#pragma warning disable CS0414
        private bool _mockDataActive;
#pragma warning restore CS0414
        private bool _binaryProbePerformed;
        private bool _forceImmediateSimulation;
        private bool _hasPendingPhysiology;
        private bool _hasPendingExternalPressure;
        private bool _hasPendingEnvironment;
        private bool _pendingMockReset;
        private bool _registeredHotSwapListener;
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private bool _nativeFaultLogged;
        private bool _coldSupportsSetConstantBuffer;
        private bool _nativeRepairPending;
        private bool _gpuGlobalsBufferPrewarmPending;

        public bool TryGetPreview(out VisorStateDTO state, out DiegeticVisorLensGpuGlobalsDTO globals, out VisorLensTuningDTO tuning)
        {
            state = default;
            globals = default;
            tuning = default;
            IDataVault vault = _vault;
            if (!_nativeReady ||
                vault == null ||
                !TryReadVaultArray(vault, in _stateHandle, StateBufferId, 1, out NativeArray<VisorStateDTO>.ReadOnly states) ||
                !TryReadVaultArray(vault, in _gpuGlobalsHandle, GpuGlobalsBufferId, 1, out NativeArray<DiegeticVisorLensGpuGlobalsDTO>.ReadOnly gpuGlobals) ||
                !TryReadVaultArray(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<VisorLensTuningDTO>.ReadOnly tunings))
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
            if (!_nativeReady)
                return false;

            IDataVault vault = EnsureVault();
            bool stateLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _stateHandle, StateBufferId, 1, out NativeArray<VisorStateDTO> stateBuffer))
                    return false;

                stateLocked = true;
                VisorStateDTO stateRow = stateBuffer[0];
                stateRow.CondensationLevel = Sanitize01(state.CondensationLevel);
                stateRow.WaterDropletIntensity = Sanitize01(state.WaterDropletIntensity);
                stateRow.CrackSeverity = Sanitize01(state.CrackSeverity);
                stateRow.DirtAccumulation = Sanitize01(state.DirtAccumulation);
                stateBuffer[0] = stateRow;
                _forceImmediateSimulation = true;
                return true;
            }
            finally
            {
                if (stateLocked)
                    vault.ReleaseWriteLock(in _stateHandle, OwnerSystem);
            }
        }

        /// <summary>
        /// Writes visor tuning only when no Burst job owns the Vault tuning buffer.
        /// </summary>
        public bool TryWriteTuning(in VisorLensTuningDTO tuning)
        {
            EnsureNativeState();
            if (!_nativeReady)
                return false;

            IDataVault vault = EnsureVault();
            bool tuningLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<VisorLensTuningDTO> tuningBuffer))
                    return false;

                tuningLocked = true;
                tuningBuffer[0] = tuning;
                _forceImmediateSimulation = true;
                return true;
            }
            finally
            {
                if (tuningLocked)
                    vault.ReleaseWriteLock(in _tuningHandle, OwnerSystem);
            }
        }

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            EnsureNativeState();
        }

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            EnsureNativeState();
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            UploadGpuGlobals();
            TryUnregisterLateFrameTickable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            ClearGpuGlobals();
            ReleaseGpuBuffer();
            _playerContext = null;
            ReleaseNativeState(_vault, clearVault: true);
        }

        private void AdvanceVisorSimulation(float deltaTime)
        {
            if (!_nativeReady)
            {
                _nativeRepairPending = true;
                return;
            }

            float safeDelta = SanitizeDelta(deltaTime);
            _frameCounter++;
            _breachCooldown = math.max(0f, _breachCooldown - safeDelta);

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
            AdvanceVisorSimulation(SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            UploadGpuGlobals();
        }

        public void SlowTick()
        {
            if (_nativeRepairPending || !_nativeReady)
            {
                _nativeRepairPending = false;
                CacheRegistryServicesCold();
                EnsureNativeState();
            }

            if (_gpuGlobalsBufferPrewarmPending || (_nativeReady && !HasValidGpuBuffer()))
                PrepareGpuGlobalsBufferCold();
        }

        public void GenerateEmergencyMockVisorData()
        {
            EnsureNativeState();

            ApplyEmergencyMockVisorData();
        }

        private void ApplyEmergencyMockVisorData()
        {
            IDataVault vault = EnsureVault();
            if (!TryReadVisorValue(vault, in _stateHandle, StateBufferId, out VisorStateDTO state) ||
                !TryReadVisorValue(vault, in _tuningHandle, TuningBufferId, out VisorLensTuningDTO tuning) ||
                !TryReadVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, out MockPhysiologySignal physiology) ||
                !TryReadVisorValue(vault, in _environmentHandle, EnvironmentBufferId, out MockVisorEnvironmentSignal environment))
            {
                return;
            }

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

            physiology.RespirationRate = 12f;
            physiology.HeartRate = 72f;
            physiology.CoreTemperatureC = 37f;
            physiology.BreathSpike01 = 0f;
            physiology.Frame = _frameCounter;
            physiology.Flags = 0u;

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

            if (!TryWriteVisorValue(vault, in _stateHandle, StateBufferId, in state) ||
                !TryWriteVisorValue(vault, in _tuningHandle, TuningBufferId, in tuning) ||
                !TryWriteVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, in physiology) ||
                !TryWriteVisorValue(vault, in _environmentHandle, EnvironmentBufferId, in environment))
            {
                return;
            }

            _mockDataActive = true;
            ClearPendingExternalInputs();
        }

        public void InjectMockPhysiology(float respirationRate, float heartRate, float coreTemperatureC)
        {
            EnsureNativeState();
            float safeRespiration = SanitizeRange(respirationRate, 4f, 44f, 12f);
            float safeHeartRate = SanitizeRange(heartRate, 28f, 220f, 72f);
            float safeCoreTemperature = SanitizeRange(coreTemperatureC, 28f, 43f, 37f);

            IDataVault vault = EnsureVault();
            bool physiologyLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _physiologyHandle, PhysiologyBufferId, 1, out NativeArray<MockPhysiologySignal> physiologyBuffer))
                    return;

                physiologyLocked = true;
                MockPhysiologySignal physiology = physiologyBuffer[0];
                physiology.RespirationRate = safeRespiration;
                physiology.HeartRate = safeHeartRate;
                physiology.CoreTemperatureC = safeCoreTemperature;
                physiology.Frame = _frameCounter;
                physiologyBuffer[0] = physiology;
                _forceImmediateSimulation = true;
            }
            finally
            {
                if (physiologyLocked)
                    vault.ReleaseWriteLock(in _physiologyHandle, OwnerSystem);
            }
        }

        public void InjectMockExternalPressure(float pressure01)
        {
            EnsureNativeState();
            float safePressure = Sanitize01(pressure01);

            IDataVault vault = EnsureVault();
            bool environmentLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal> environmentBuffer))
                    return;

                environmentLocked = true;
                MockVisorEnvironmentSignal environment = environmentBuffer[0];
                environment.ExternalPressure01 = safePressure;
                environment.Frame = _frameCounter;
                environmentBuffer[0] = environment;
                _forceImmediateSimulation = true;
            }
            finally
            {
                if (environmentLocked)
                    vault.ReleaseWriteLock(in _environmentHandle, OwnerSystem);
            }
        }

        public void InjectEnvironment(float waterTemperatureC, float silt01, float darkness01, float corruption01)
        {
            EnsureNativeState();
            float safeWaterTemperature = SanitizeRange(waterTemperatureC, -4f, 38f, 4f);
            float safeSilt = Sanitize01(silt01);
            float safeDarkness = Sanitize01(darkness01);
            float safeCorruption = Sanitize01(corruption01);

            IDataVault vault = EnsureVault();
            bool environmentLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal> environmentBuffer))
                    return;

                environmentLocked = true;
                MockVisorEnvironmentSignal environment = environmentBuffer[0];
                environment.ExternalWaterTemperatureC = safeWaterTemperature;
                environment.SiltDensity01 = math.max(environment.SiltDensity01, safeSilt);
                environment.Darkness01 = safeDarkness;
                environment.Corruption01 = math.max(environment.Corruption01, safeCorruption);
                environment.Frame = _frameCounter;
                environmentBuffer[0] = environment;
                _forceImmediateSimulation = true;
            }
            finally
            {
                if (environmentLocked)
                    vault.ReleaseWriteLock(in _environmentHandle, OwnerSystem);
            }
        }

        public void NotifySurfaceEmergence(float intensity01)
        {
            EnsureNativeState();
            float safeIntensity = Sanitize01(intensity01);

            IDataVault vault = EnsureVault();
            bool environmentLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal> environmentBuffer))
                    return;

                environmentLocked = true;
                MockVisorEnvironmentSignal environment = environmentBuffer[0];
                environment.SurfaceEmergence01 = math.max(environment.SurfaceEmergence01, safeIntensity);
                environment.WaterlineBreach01 = math.max(environment.WaterlineBreach01, safeIntensity);
                environment.Frame = _frameCounter;
                environmentBuffer[0] = environment;
                _forceImmediateSimulation = true;
            }
            finally
            {
                if (environmentLocked)
                    vault.ReleaseWriteLock(in _environmentHandle, OwnerSystem);
            }
        }

        public void RequestWipeVisor(float strength01 = 1f)
        {
            EnsureNativeState();
            float safeStrength = Sanitize01(strength01);

            IDataVault vault = EnsureVault();
            bool environmentLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal> environmentBuffer))
                    return;

                environmentLocked = true;
                MockVisorEnvironmentSignal environment = environmentBuffer[0];
                environment.WipeCommand01 = math.max(environment.WipeCommand01, safeStrength);
                environment.Frame = _frameCounter;
                environmentBuffer[0] = environment;
                _forceImmediateSimulation = true;
            }
            finally
            {
                if (environmentLocked)
                    vault.ReleaseWriteLock(in _environmentHandle, OwnerSystem);
            }
        }

#if UNITY_EDITOR
        public bool TryReloadCsvOverrides()
        {
            EnsureNativeState();

            string root = Path.Combine(Application.dataPath, "..");
            string path = Path.Combine(root, CsvRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                Span<byte> scratch = stackalloc byte[CsvBufferBytes];
                int length = FillSpanFromFile(path, scratch);
                if (length <= 0)
                    return false;

                ReadOnlySpan<byte> csv = scratch.Slice(0, length);
                IDataVault vault = EnsureVault();
                if (!TryReadVaultArray(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<VisorLensTuningDTO>.ReadOnly tuningRead))
                    return false;

                VisorLensTuningDTO tuning = tuningRead[0];
                ParseVisorCsv(csv, ref tuning);

                if (!TryWriteParsedCsvTuning(vault, in tuning))
                    return false;

                TryMirrorCsvBytes(vault, in csv);
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
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private bool TryWriteParsedCsvTuning(IDataVault vault, in VisorLensTuningDTO tuning)
        {
            bool tuningLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<VisorLensTuningDTO> tuningBuffer))
                    return false;

                tuningLocked = true;
                tuningBuffer[0] = tuning;
                return true;
            }
            finally
            {
                if (tuningLocked)
                    vault.ReleaseWriteLock(in _tuningHandle, OwnerSystem);
            }
        }

        private void TryMirrorCsvBytes(IDataVault vault, in ReadOnlySpan<byte> csv)
        {
            bool csvLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _csvBytesHandle, CsvByteBufferId, CsvBufferBytes, out NativeArray<byte> bytes))
                    return;

                csvLocked = true;
                int length = math.min(csv.Length, bytes.Length);
                for (int i = 0; i < length; i++)
                    bytes[i] = csv[i];
            }
            finally
            {
                if (csvLocked)
                    vault.ReleaseWriteLock(in _csvBytesHandle, OwnerSystem);
            }
        }
#endif

        private void EnsureNativeState()
        {
            if (_nativeReady)
                return;

            IDataVault vault = EnsureVault();
            if (vault == null)
            {
                ReportNativeFaultClosed();
                return;
            }

            _vault = vault;
            try
            {
                if (!TryAcquireBuffer(StateBufferId, 1, out _stateHandle) ||
                    !TryAcquireBuffer(TuningBufferId, 1, out _tuningHandle) ||
                    !TryAcquireBuffer(PhysiologyBufferId, 1, out _physiologyHandle) ||
                    !TryAcquireBuffer(EnvironmentBufferId, 1, out _environmentHandle) ||
                    !TryAcquireBuffer(GpuGlobalsBufferId, 1, out _gpuGlobalsHandle) ||
                    !TryAcquireBuffer(TelemetryRingBufferId, TelemetryCapacity, out _telemetryHandle) ||
                    !TryAcquireBuffer(TelemetryCursorBufferId, 1, out _telemetryCursorHandle) ||
                    !TryAcquireBuffer(CsvByteBufferId, CsvBufferBytes, out _csvBytesHandle) ||
                    !TryAcquireBuffer(BinaryProbeByteBufferId, BinaryProbeBytes, out _binaryProbeBytesHandle) ||
                    !TryAcquireBuffer(NanFlagBufferId, 1, out _nanFlagsHandle))
                {
                    ReleaseNativeState(vault, clearVault: false);
                    ReportNativeFaultClosed();
                    return;
                }

                ClearNativeBuffersWithMemClear();
                _nativeReady = true;
                _nativeFaultLogged = false;
                PrewarmSignalLanes();
                GenerateEmergencyMockVisorData();
#if UNITY_EDITOR
                TryReloadCsvOverrides();
#endif
                ProbeColdBinaryPayloads();
                RequestGpuGlobalsBufferPrewarm();
                ClearGpuGlobals();
                _simulationAccumulator = ResolveSimulationInterval(ResolveQualityWeight());
            }
            catch (ObjectDisposedException)
            {
                ReleaseNativeState(vault, clearVault: false);
                ReportNativeFaultClosed();
            }
            catch (InvalidOperationException)
            {
                ReleaseNativeState(vault, clearVault: false);
                ReportNativeFaultClosed();
            }
            catch (ArgumentException)
            {
                ReleaseNativeState(vault, clearVault: false);
                ReportNativeFaultClosed();
            }
            catch (NotSupportedException)
            {
                ReleaseNativeState(vault, clearVault: false);
                ReportNativeFaultClosed();
            }
            catch (UnityException)
            {
                ReleaseNativeState(vault, clearVault: false);
                ReportNativeFaultClosed();
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
                IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                RebindDataVaultForLifecycle(nextVault);
                if (_vault != null)
                    EnsureNativeState();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTickable();
                TryUnregisterLateFrameTickable();
                if (currentService != null)
                {
                    TryRegisterSlowTickable();
                    TryRegisterLateFrameTickable();
                }
            }
        }

        private IDataVault EnsureVault()
        {
            if (_vault != null)
                return _vault;

            return null;
        }

        private void CacheRegistryServicesCold()
        {
            if (_vault == null)
                RebindDataVaultForLifecycle(GlobalRegistry.DataVault);

            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            ReleaseNativeState(_vault, clearVault: false);
            _vault = nextVault;
            ResetNativeEpochState();
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

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private bool TryAcquireBuffer<T>(BufferID id, int length, out VaultGenerationHandle<T> handle) where T : unmanaged
        {
            handle = default;
            IDataVault vault = EnsureVault();
            if (vault == null)
                return false;

            handle = vault.EnsureGenerationHandle<T>(id, length, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            return TryReadVaultArray(vault, in handle, id, length, out NativeArray<T>.ReadOnly _);
        }

        private void ReleaseNativeState(IDataVault vault, bool clearVault)
        {
            ReleaseVisorVaultHandles(vault);
            ResetNativeEpochState();
            if (clearVault)
                _vault = null;
        }

        private void ResetNativeEpochState()
        {
            _nativeReady = false;
            _hasGpuGlobals = false;
            _hasUploadedGpuGlobals = false;
            _nativeRepairPending = false;
            _gpuGlobalsBufferPrewarmPending = false;
            _lastGpuGlobals = default;
            _uploadedGpuGlobals = default;
            _blackBoxDumped = false;
            _binaryProbePerformed = false;
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

        private void ReportNativeFaultClosed()
        {
            if (_nativeFaultLogged)
                return;

            _nativeFaultLogged = true;
            H8Debug.LogError("DIEGETIC_VISOR_LENS_NATIVE_FAIL_CLOSED");
        }

        private static bool TryReadVaultArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            int length,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   length > 0 &&
                   IsVisorVaultHandle(in handle, id) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= length;
        }

        private static bool TryAcquireVisorWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            int length,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                length <= 0 ||
                !IsVisorVaultHandle(in handle, id) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < length)
                {
                    buffer = default;
                    return false;
                }

                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static bool TryReadVisorValue<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            out T value) where T : unmanaged
        {
            value = default;
            if (!TryReadVaultArray(vault, in handle, id, 1, out NativeArray<T>.ReadOnly buffer))
                return false;

            value = buffer[0];
            return true;
        }

        private static bool TryWriteVisorValue<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            in T value) where T : unmanaged
        {
            if (!TryAcquireVisorWriteBuffer(vault, in handle, id, 1, out NativeArray<T> buffer))
                return false;

            try
            {
                T copy = value;
                buffer[0] = copy;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static bool IsVisorVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID id) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)id) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVisorVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID id) where T : unmanaged
        {
            if (vault != null && IsVisorVaultHandle(in handle, id))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearNativeBuffersWithMemClear()
        {
            IDataVault vault = EnsureVault();
            ClearNativeBufferWithWriteLock(vault, in _stateHandle, StateBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _tuningHandle, TuningBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _physiologyHandle, PhysiologyBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _environmentHandle, EnvironmentBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _gpuGlobalsHandle, GpuGlobalsBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity);
            ClearNativeBufferWithWriteLock(vault, in _telemetryCursorHandle, TelemetryCursorBufferId, 1);
            ClearNativeBufferWithWriteLock(vault, in _csvBytesHandle, CsvByteBufferId, CsvBufferBytes);
            ClearNativeBufferWithWriteLock(vault, in _binaryProbeBytesHandle, BinaryProbeByteBufferId, BinaryProbeBytes);
            ClearNativeBufferWithWriteLock(vault, in _nanFlagsHandle, NanFlagBufferId, 1);
        }

        private void ClearNativeBufferWithWriteLock<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID id,
            int length) where T : unmanaged
        {
            bool locked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in handle, id, length, out NativeArray<T> buffer))
                {
                    _nativeReady = false;
                    ReportNativeFaultClosed();
                    return;
                }

                locked = true;
                MemClearArray(buffer);
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static void MemClearArray<T>(NativeArray<T> array) where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private void ScheduleSimulation(float deltaTime, float qualityWeight)
        {
            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryReadVisorValue(vault, in _stateHandle, StateBufferId, out VisorStateDTO state) ||
                !TryReadVisorValue(vault, in _tuningHandle, TuningBufferId, out VisorLensTuningDTO tuning) ||
                !TryReadVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, out MockPhysiologySignal physiology) ||
                !TryReadVisorValue(vault, in _environmentHandle, EnvironmentBufferId, out MockVisorEnvironmentSignal environment) ||
                !TryReadVisorValue(vault, in _nanFlagsHandle, NanFlagBufferId, out int nanFlags))
            {
                return;
            }

            VisorCondensationEvaluator evaluator = default;
            evaluator.State = state;
            evaluator.Tuning = tuning;
            evaluator.Physiology = physiology;
            evaluator.Environment = environment;
            evaluator.NanFlags = nanFlags;
            evaluator.DeltaTime = deltaTime;
            evaluator.GlobalQualityWeight = qualityWeight;
            evaluator.HeadAngularVelocity = _headAngularVelocity;
            evaluator.Frame = _frameCounter;
            evaluator.Execute();

            state = evaluator.State;
            physiology = evaluator.Physiology;
            environment = evaluator.Environment;
            DiegeticVisorLensGpuGlobalsDTO gpuGlobals = evaluator.GpuGlobals;
            nanFlags = evaluator.NanFlags;

            if (!TryWriteVisorValue(vault, in _stateHandle, StateBufferId, in state) ||
                !TryWriteVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, in physiology) ||
                !TryWriteVisorValue(vault, in _environmentHandle, EnvironmentBufferId, in environment) ||
                !TryWriteVisorValue(vault, in _gpuGlobalsHandle, GpuGlobalsBufferId, in gpuGlobals))
            {
                return;
            }

            if (nanFlags != 0)
            {
                int clearedNanFlags = 0;
                if (!TryWriteVisorValue(vault, in _nanFlagsHandle, NanFlagBufferId, in clearedNanFlags))
                    return;
            }

            _lastGpuGlobals = gpuGlobals;
            _hasGpuGlobals = true;

            WriteTelemetryFrame(in state, in _lastGpuGlobals, nanFlags);
            if (nanFlags != 0)
                DumpBlackBoxOnce((uint)nanFlags);

            TryPublishBreach(in state);
        }

        private void UploadGpuGlobals()
        {
            if (!_hasGpuGlobals)
                return;

            long uploadStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            bool bufferAvailable = HasValidGpuBuffer();
            bool globalsChanged = !_hasUploadedGpuGlobals || !GpuGlobalsEqual(in _uploadedGpuGlobals, in _lastGpuGlobals);
            if (globalsChanged)
            {
                PublishGpuGlobalVectors(in _lastGpuGlobals);
                if (bufferAvailable)
                {
                    GraphicsBuffer writeBuffer = ResolveNextGpuGlobalsBuffer();
                    if (TryWriteGpuGlobalsBuffer(writeBuffer, in _lastGpuGlobals))
                        _activeGpuGlobalsBuffer = writeBuffer;
                    else
                        bufferAvailable = false;
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

        private bool HasValidGpuBuffer()
        {
            return _gpuGlobalsBufferA != null && _gpuGlobalsBufferA.IsValid() &&
                   _gpuGlobalsBufferB != null && _gpuGlobalsBufferB.IsValid();
        }

        private bool EnsureGpuBuffer()
        {
            if (!_coldSupportsSetConstantBuffer)
            {
                ReleaseGpuBuffer();
                return false;
            }

            if (HasValidGpuBuffer())
                return true;

            ReleaseGpuBuffer();
            try
            {
                // COLD ALLOC: GraphicsBuffer[2] - ping-pong visor lens scalar CBuffers - owner: DiegeticVisorLensRuntime.
                _gpuGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GpuGlobalsStrideBytes);
                _gpuGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GpuGlobalsStrideBytes);
                _hasUploadedGpuGlobals = false;
                return HasValidGpuBuffer();
            }
            catch (ObjectDisposedException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (InvalidOperationException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (ArgumentException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (NotSupportedException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (UnityException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
        }

        private void RequestGpuGlobalsBufferPrewarm()
        {
            if (!_coldSupportsSetConstantBuffer)
            {
                _gpuGlobalsBufferPrewarmPending = false;
                ReleaseGpuBuffer();
                return;
            }

            _gpuGlobalsBufferPrewarmPending = !HasValidGpuBuffer();
        }

        private void PrepareGpuGlobalsBufferCold()
        {
            _gpuGlobalsBufferPrewarmPending = false;
            EnsureGpuBuffer();
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

        private bool TryWriteGpuGlobalsBuffer(GraphicsBuffer buffer, in DiegeticVisorLensGpuGlobalsDTO globals)
        {
            if (buffer == null || !buffer.IsValid())
                return false;

            try
            {
                NativeArray<DiegeticVisorLensGpuGlobalsDTO> mapped = buffer.LockBufferForWrite<DiegeticVisorLensGpuGlobalsDTO>(0, 1);
                try
                {
                    mapped[0] = globals;
                }
                finally
                {
                    buffer.UnlockBufferAfterWrite<DiegeticVisorLensGpuGlobalsDTO>(1);
                }

                return true;
            }
            catch (ObjectDisposedException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (InvalidOperationException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (ArgumentException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (NotSupportedException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
            catch (UnityException)
            {
                ReleaseGpuBuffer();
                ReportNativeFaultClosed();
                return false;
            }
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
                bool clearedA = TryWriteGpuGlobalsBuffer(_gpuGlobalsBufferA, in globals);
                bool clearedB = clearedA && TryWriteGpuGlobalsBuffer(_gpuGlobalsBufferB, in globals);
                if (clearedB && _gpuGlobalsBufferA != null && _gpuGlobalsBufferA.IsValid())
                {
                    _activeGpuGlobalsBuffer = _gpuGlobalsBufferA;
                    Shader.SetGlobalConstantBuffer(GpuGlobalsNameId, _activeGpuGlobalsBuffer, 0, GpuGlobalsStrideBytes);
                }
            }

            _uploadedGpuGlobals = globals;
            _hasUploadedGpuGlobals = true;
            _hasGpuGlobals = false;
        }

        private void IngestCoreSignals(float deltaTime)
        {
            IDataVault vault = EnsureVault();
            if (!TryReadVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, out MockPhysiologySignal physiology) ||
                !TryReadVisorValue(vault, in _environmentHandle, EnvironmentBufferId, out MockVisorEnvironmentSignal environment))
            {
                return;
            }

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

            if (!TryWriteVisorValue(vault, in _physiologyHandle, PhysiologyBufferId, in physiology) ||
                !TryWriteVisorValue(vault, in _environmentHandle, EnvironmentBufferId, in environment))
            {
                return;
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
                ? math.float3(angular.x, angular.y, angular.z)
                : float3.zero;
            _lastCameraRotation = current;
        }

        private void TryPublishBreach(in VisorStateDTO state)
        {
            if (state.CrackSeverity <= 0.8f || _breachCooldown > 0f)
                return;

            IDataVault vault = EnsureVault();
            if (!TryReadVaultArray(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal>.ReadOnly environments))
                return;

            MockVisorEnvironmentSignal environment = environments[0];
            VisorBreachSignal signal = default;
            signal.SourceId = RuntimeSourceHash;
            signal.Frame = _frameCounter;
            signal.CrackSeverity01 = Sanitize01(state.CrackSeverity);
            signal.ExternalPressure01 = Sanitize01(environment.ExternalPressure01);
            signal.Condensation01 = Sanitize01(state.CondensationLevel);
            signal.Flags = 1u;
            signal.Sequence = _breachSequence++;
            SignalBus<VisorBreachSignal>.TryPushTracked(in signal, ref s_x001DiegeticVisorLensRuntimeSignalPushDropCount);
            _breachCooldown = BreachPublishCooldownSeconds;
        }

        private void WriteTelemetryFrame(in VisorStateDTO state, in DiegeticVisorLensGpuGlobalsDTO globals, int nanFlags)
        {
            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryReadVaultArray(vault, in _physiologyHandle, PhysiologyBufferId, 1, out NativeArray<MockPhysiologySignal>.ReadOnly physiologies) ||
                !TryReadVaultArray(vault, in _environmentHandle, EnvironmentBufferId, 1, out NativeArray<MockVisorEnvironmentSignal>.ReadOnly environments))
                return;

            if (physiologies.Length <= 0 || environments.Length <= 0)
                return;

            MockPhysiologySignal physiology = physiologies[0];
            MockVisorEnvironmentSignal environment = environments[0];
            VisorLensTelemetryEntry entry = default;
            entry.Frame = _frameCounter;
            entry.Flags = (uint)nanFlags;
            entry.Condensation01 = Sanitize01(state.CondensationLevel);
            entry.Droplets01 = Sanitize01(state.WaterDropletIntensity);
            entry.Crack01 = Sanitize01(state.CrackSeverity);
            entry.Dirt01 = Sanitize01(state.DirtAccumulation);
            entry.Quality01 = ResolveQualityWeight();
            entry.RespirationRate = SanitizeRange(physiology.RespirationRate, 0f, 80f, 0f);
            entry.ExternalPressure01 = Sanitize01(environment.ExternalPressure01);
            entry.SiltDensity01 = Sanitize01(environment.SiltDensity01);
            entry.HeadAngularSpeed = math.length(_headAngularVelocity);
            entry.StateHash = BuildStateHash(in state);
            entry.GpuStateHash = BuildGpuHash(in globals);
            entry.RefractionScale01 = Sanitize01(globals.Params0.w);
            entry.ShaderUpdateComputeTimeNs = _lastShaderUpdateComputeTimeNs;
            entry.Anomaly01 = Sanitize01(environment.Corruption01);

            if (!TryAdvanceTelemetryCursor(vault, out int cursor))
                return;

            TryWriteTelemetryEntry(vault, cursor, in entry);
        }

        private bool TryAdvanceTelemetryCursor(IDataVault vault, out int cursor)
        {
            cursor = 0;
            bool cursorLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _telemetryCursorHandle, TelemetryCursorBufferId, 1, out NativeArray<int> cursorBuffer))
                    return false;
                cursorLocked = true;

                if (!cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                    return false;

                cursor = cursorBuffer[0];
                if (cursor < 0 || cursor >= TelemetryCapacity)
                    cursor = 0;

                cursorBuffer[0] = cursor + 1 >= TelemetryCapacity ? 0 : cursor + 1;
                return true;
            }
            finally
            {
                if (cursorLocked)
                    vault.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystem);
            }
        }

        private bool TryWriteTelemetryEntry(IDataVault vault, int cursor, in VisorLensTelemetryEntry entry)
        {
            bool ringLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<VisorLensTelemetryEntry> ring))
                    return false;

                ringLocked = true;
                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return false;

                ring[cursor] = entry;
                return true;
            }
            finally
            {
                if (ringLocked)
                    vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystem);
            }
        }

        private void PatchLatestTelemetryShaderUpdateNs(uint shaderUpdateComputeTimeNs)
        {
            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryReadVisorValue(vault, in _telemetryCursorHandle, TelemetryCursorBufferId, out int cursor))
                return;

            cursor--;
            if (cursor < 0)
                cursor = TelemetryCapacity - 1;

            bool ringLocked = false;
            try
            {
                if (!TryAcquireVisorWriteBuffer(vault, in _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<VisorLensTelemetryEntry> ring))
                    return;
                ringLocked = true;

                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return;

                VisorLensTelemetryEntry entry = ring[cursor];
                entry.ShaderUpdateComputeTimeNs = shaderUpdateComputeTimeNs;
                ring[cursor] = entry;
            }
            finally
            {
                if (ringLocked)
                    vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystem);
            }
        }

        private void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (_blackBoxDumped)
                return;

            NativeArray<byte> payload = default;
            try
            {
                if (!TryReadTelemetryDumpCursor(out int index))
                    return;

                const int headerBytes = 20;
                int rowBytes = DiegeticVisorLensLayout.VisorLensTelemetryEntryStrideBytes;
                int byteCount = headerBytes + TelemetryCapacity * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(DiegeticVisorLensRuntime),
                    "diegeticVisorLensBlackBoxPayload");
                unsafe
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> header = new Span<byte>(target, headerBytes);
                    WriteTelemetryDumpHeader(header, reasonFlags);
                    int offset = headerBytes;
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        if (index >= TelemetryCapacity)
                            index = 0;

                        if (!TryReadTelemetryDumpEntry(index, out VisorLensTelemetryEntry entry))
                            return;

                        Span<byte> entryBytes = new Span<byte>(target + offset, rowBytes);
                        WriteTelemetryEntry(entryBytes, in entry);
                        offset += rowBytes;
                        index++;
                    }
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount);
            }
            catch (IOException)
            {
                _blackBoxDumped = false;
            }
            catch (UnauthorizedAccessException)
            {
                _blackBoxDumped = false;
            }
            catch (ObjectDisposedException)
            {
                _blackBoxDumped = false;
            }
            catch (InvalidOperationException)
            {
                _blackBoxDumped = false;
            }
            catch (ArgumentException)
            {
                _blackBoxDumped = false;
            }
            catch (NotSupportedException)
            {
                _blackBoxDumped = false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(DiegeticVisorLensRuntime),
                    "diegeticVisorLensBlackBoxPayload");
            }
        }

        private bool TryReadTelemetryDumpCursor(out int cursor)
        {
            cursor = 0;
            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryReadVisorValue(vault, in _telemetryCursorHandle, TelemetryCursorBufferId, out cursor))
                return false;

            if (cursor < 0 || cursor >= TelemetryCapacity)
                cursor = 0;

            return true;
        }

        private bool TryReadTelemetryDumpEntry(int index, out VisorLensTelemetryEntry entry)
        {
            entry = default;
            if ((uint)index >= (uint)TelemetryCapacity)
                return false;

            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryReadVaultArray(vault, in _telemetryHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<VisorLensTelemetryEntry>.ReadOnly ring))
                return false;

            entry = ring[index];
            return true;
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

            IDataVault vault = null;
            bool probeLocked = false;
            try
            {
                Span<byte> scratch = stackalloc byte[BinaryProbeBytes];
                int length = FillSpanFromFile(path, scratch);
                if (length < 4)
                    return false;

                vault = EnsureVault();
                if (!TryAcquireVisorWriteBuffer(vault, in _binaryProbeBytesHandle, BinaryProbeByteBufferId, BinaryProbeBytes, out NativeArray<byte> bytes))
                    return false;

                probeLocked = true;
                for (int i = 0; i < length; i++)
                    bytes[i] = scratch[i];

                uint magic = (uint)(scratch[0] | (scratch[1] << 8) | (scratch[2] << 16) | (scratch[3] << 24));
                return magic == 0x56534D38u || ReverseBytes(magic) == 0x56534D38u;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            finally
            {
                if (probeLocked)
                    vault.ReleaseWriteLock(in _binaryProbeBytesHandle, OwnerSystem);
            }
        }

        private static int FillSpanFromFile(string path, Span<byte> buffer)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long cappedLength = stream.Length;
                if (cappedLength > buffer.Length)
                    cappedLength = buffer.Length;

                int length = (int)cappedLength;
                int total = 0;
                while (total < length)
                {
                    int read = stream.Read(buffer.Slice(total, length - total));
                    if (read <= 0)
                        return total;

                    total += read;
                }

                return length;
            }
        }

#if UNITY_EDITOR
        private static void ParseVisorCsv(ReadOnlySpan<byte> bytes, ref VisorLensTuningDTO tuning)
        {
            int length = bytes.Length;
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
                    SkipLine(bytes, ref cursor);
                    continue;
                }

                float value = ParseFloat(bytes, ref cursor);
                ApplyCsvValue(keyHash, value, ref tuning);
                SkipLine(bytes, ref cursor);
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

        private static float ParseFloat(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int length = bytes.Length;
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

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int length = bytes.Length;
            while (cursor < length)
            {
                byte c = bytes[cursor++];
                if (c == (byte)'\n')
                    break;
            }
        }
#endif

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
            Vector4 vector = default;
            vector.x = value.x;
            vector.y = value.y;
            vector.z = value.z;
            vector.w = value.w;
            return vector;
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

#if UNITY_EDITOR
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
#endif

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

        private static void WriteTelemetryDumpHeader(Span<byte> destination, uint reasonFlags)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), DumpMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), DumpVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), reasonFlags);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), DiegeticVisorLensLayout.VisorLensTelemetryEntryStrideBytes);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16, 4), TelemetryCapacity);
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in VisorLensTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.Condensation01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.Droplets01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.Crack01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.Dirt01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.Quality01);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.RespirationRate);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.ExternalPressure01);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.SiltDensity01);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.HeadAngularSpeed);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(48, 4), entry.GpuStateHash);
            WriteFloatLittleEndian(destination.Slice(52, 4), entry.RefractionScale01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(56, 4), entry.ShaderUpdateComputeTimeNs);
            WriteFloatLittleEndian(destination.Slice(60, 4), entry.Anomaly01);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
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

        private ref struct VisorCondensationEvaluator
        {
            public VisorStateDTO State;
            public VisorLensTuningDTO Tuning;
            public MockPhysiologySignal Physiology;
            public MockVisorEnvironmentSignal Environment;
            public DiegeticVisorLensGpuGlobalsDTO GpuGlobals;
            public int NanFlags;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public float3 HeadAngularVelocity;
            public uint Frame;

            public void Execute()
            {
                float dt = math.max(0f, math.isfinite(DeltaTime) ? DeltaTime : 0f);
                float quality = Sanitize01Job(GlobalQualityWeight);
                VisorStateDTO state = SanitizeState(State);
                VisorLensTuningDTO tuning = SanitizeTuning(Tuning);
                MockPhysiologySignal physiology = SanitizePhysiology(Physiology);
                MockVisorEnvironmentSignal environment = SanitizeEnvironment(Environment);

                uint anomalyHash = math.hash(math.uint3(Frame + 17u, math.asuint(environment.Corruption01), math.asuint(state.CrackSeverity)));
                float anomalyNoise = ((anomalyHash & 1023u) * (1f / 1023f) - 0.5f) * environment.Corruption01;
                float coldDrive = math.saturate((18f - environment.ExternalWaterTemperatureC) * (1f / 28f));
                float breathDrive = math.saturate((physiology.RespirationRate - 8f) * (1f / 28f)) * tuning.FogBreathGain;
                breathDrive += physiology.BreathSpike01 * tuning.FogBreathGain * 2.4f;
                float heartDrive = math.saturate((physiology.HeartRate - 72f) * (1f / 108f)) * tuning.HeartCondensationGain;
                float coreDrive = math.saturate((physiology.CoreTemperatureC - 36.5f) * 0.25f) * tuning.CoreTempCondensationGain;
                float fogAdd = (tuning.FogRate * coldDrive + breathDrive + heartDrive + coreDrive + math.max(0f, anomalyNoise) * tuning.AnomalyNoiseGain * 0.08f) * dt;
                state.CondensationLevel = math.saturate(state.CondensationLevel + fogAdd);
                float clearingRate = tuning.ClearingRate * math.lerp(1.4f, 0.72f, quality) * (1f + environment.WipeCommand01 * 2.5f);
                state.CondensationLevel = math.saturate(state.CondensationLevel * MathLodApproximation.ApproxExpNegPade33Wide40(clearingRate * dt));

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
                float2 gravity = math.float2(angular.y * 0.18f + angular.z * 0.05f, -1f - angular.x * 0.08f);
                float gravityLenSq = math.max(0.0001f, math.lengthsq(gravity));
                gravity *= math.rsqrt(gravityLenSq);
                gravity *= tuning.DropletGravityStrength * dynamicBlend * state.WaterDropletIntensity;
                gravity = math.lerp(math.float2(0f, -0.08f * state.WaterDropletIntensity), gravity, dynamicBlend);

                float reflection = math.saturate(environment.Darkness01 * tuning.ReflectionDarknessGain * (0.24f + quality * 0.76f));
                reflection = math.saturate(reflection + environment.Corruption01 * tuning.BiolumReflectionGain * 0.35f);
                uint flags = 0u;
                flags |= state.CrackSeverity > 0.8f ? 1u : 0u;
                flags |= refractionScale <= 0.05f ? 2u : 0u;
                flags |= environment.Corruption01 > 0.01f ? 4u : 0u;

                State = state;
                MockPhysiologySignal physiologySignal = default;
                physiologySignal.RespirationRate = physiology.RespirationRate;
                physiologySignal.HeartRate = physiology.HeartRate;
                physiologySignal.CoreTemperatureC = physiology.CoreTemperatureC;
                physiologySignal.BreathSpike01 = physiology.BreathSpike01 * MathLodApproximation.ApproxExpNegPade33Wide40(3.2f * dt);
                physiologySignal.Frame = Frame;
                physiologySignal.Flags = physiology.Flags;
                Physiology = physiologySignal;

                MockVisorEnvironmentSignal environmentSignal = default;
                environmentSignal.ExternalWaterTemperatureC = environment.ExternalWaterTemperatureC;
                environmentSignal.ExternalPressure01 = environment.ExternalPressure01;
                environmentSignal.SiltDensity01 = math.max(0f, environment.SiltDensity01 - dt * 0.035f);
                environmentSignal.Darkness01 = environment.Darkness01;
                environmentSignal.SurfaceEmergence01 = math.max(0f, environment.SurfaceEmergence01 - dt * tuning.SurfaceWashDrainRate);
                environmentSignal.WipeCommand01 = math.max(0f, environment.WipeCommand01 - dt * 2.5f);
                environmentSignal.Corruption01 = math.max(0f, environment.Corruption01 - dt * 0.08f);
                environmentSignal.WaterlineBreach01 = math.max(0f, environment.WaterlineBreach01 - dt * 0.2f);
                environmentSignal.Frame = Frame;
                environmentSignal.Flags = environment.Flags;
                Environment = environmentSignal;

                DiegeticVisorLensGpuGlobalsDTO gpu = default;
                gpu.State = math.float4(state.CondensationLevel, state.WaterDropletIntensity, state.CrackSeverity, state.DirtAccumulation);
                gpu.Params0 = math.float4(gravity.x, gravity.y, reflection, refractionScale);
                gpu.Params1 = math.float4(quality, environment.Corruption01, math.max(environment.SurfaceEmergence01, environment.WaterlineBreach01), environment.Darkness01);
                gpu.Params2 = math.float4(environment.ExternalPressure01, environment.SiltDensity01, headSpeed, math.asfloat(flags));
                GpuGlobals = gpu;

                if (!FiniteState(state) || !math.all(math.isfinite(gpu.State)) || !math.all(math.isfinite(gpu.Params0)) ||
                    !math.all(math.isfinite(gpu.Params1)) || !math.all(math.isfinite(gpu.Params2)))
                {
                    NanFlags = 1;
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
