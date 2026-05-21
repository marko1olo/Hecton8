using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ShaderGlobalsDTO
    {
        public const int SizeBytes = 48;

        [FieldOffset(0)]
        public float4 FogColor;
        [FieldOffset(16)]
        public float3 FlowVector;
        [FieldOffset(28)]
        public float FlowMagnitude;
        [FieldOffset(32)]
        public float GlobalTime;
        [FieldOffset(36)]
        public float _pad0;
        [FieldOffset(40)]
        public float _pad1;
        [FieldOffset(44)]
        public float _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockWeatherState
    {
        public const int SizeBytes = 16;

        [FieldOffset(0)]
        public float Storm01;
        [FieldOffset(4)]
        public float Turbidity01;
        [FieldOffset(8)]
        public float Heat01;
        [FieldOffset(12)]
        public float BiomeBlend01;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct UberNoirGlobalTuning
    {
        public const int SizeBytes = 48;

        [FieldOffset(0)]
        public Vector4 FogColor;
        [FieldOffset(16)]
        public Vector3 FlowVector;
        [FieldOffset(28)]
        public float FogDensity;
        [FieldOffset(32)]
        public float CausticSpeed;
        [FieldOffset(36)]
        public float FlowMagnitude;
        [FieldOffset(40)]
        public float _pad0;
        [FieldOffset(44)]
        public float _pad1;
    }

    /// <summary>
    /// DataVault-to-URP bridge for frame-wide noir shader state.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Rendering/Global Shader Dispatcher")]
    public sealed unsafe class GlobalShaderDispatcher : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int RequiredShaderGlobalSlots = HectonShaderGlobalDataVaultBridge.SlotCount;
        private const int ShaderGlobalsDtoSlot = HectonShaderGlobalDataVaultBridge.ShaderGlobalsDtoSlot;
        private const int MockWeatherSlot = 12;
        private const int AmbientSlot = 13;
        private const int CausticRuntimeSlot = 14;
        private const int ExtinctionCoefficientsSlot = 15;
        private const int AupOffsetSlot = 16;
        private const int ResolutionSlot = 17;
        private const int HazardSlot = 18;
        private const int ThermalPackedSlotStart = HectonShaderGlobalDataVaultBridge.ThermalPackedSlotStart;
        private const int ThermalAnomalyCapacity = HectonShaderGlobalDataVaultBridge.ThermalPackedSlotCount;
        private const int TelemetrySlotStart = HectonShaderGlobalDataVaultBridge.TelemetrySlotStart;
        private const int TelemetryCapacity = HectonShaderGlobalDataVaultBridge.TelemetrySlotCount;
        private const int CsvScratchBytes = 4096;
        private const double ShaderTimeModuloSeconds = 3600.0;
        private static readonly double s_stopwatchTicksToMicroseconds = 1000000.0 / Stopwatch.Frequency;
        private const float DispatchBudgetMicroseconds = 100f;
        private const uint DumpMagic = 0x43424652u; // CBFR
        private const string DumpFileName = "Dump_CBUFFER_DISPATCH.bin";
        private const string DumpH8DumpFileName = "Dump_CBUFFER_DISPATCH.h8dump";
        private const string CsvOverrideFileName = "shader_globals_override.csv";

        private static readonly int _FogColorId = Shader.PropertyToID("_H8FogColor");
        private static readonly int _FogDensityId = Shader.PropertyToID("_H8FogDensity");
        private static readonly int _AmbientLightId = Shader.PropertyToID("_H8AmbientLightColor");
        private static readonly int _GlobalFlowVectorId = Shader.PropertyToID("_GlobalFlowVector");
        private static readonly int _H8GlobalFlowId = Shader.PropertyToID("_H8GlobalFlow");
        private static readonly int _H8ShaderTimeId = Shader.PropertyToID("_H8ShaderTime");
        private static readonly int _WorldOriginOffsetId = Shader.PropertyToID("_WorldOriginOffset");
        private static readonly int _TotalUniverseOffsetId = Shader.PropertyToID("_TotalUniverseOffset");
        private static readonly int _ResolutionScaleId = Shader.PropertyToID("_ResolutionScale");
        private static readonly int _ResolutionScaleParamsId = Shader.PropertyToID("_H8ResolutionScaleParams");
        private static readonly int _DynamicWakesId = Shader.PropertyToID("_DynamicWakes");
        private static readonly int _DynamicWakeVectorsId = Shader.PropertyToID("_DynamicWakeVectors");
        private static readonly int _DynamicWakeParamsId = Shader.PropertyToID("_DynamicWakeParams");
        private static readonly int _ThermalAnomaliesId = Shader.PropertyToID("_H8ThermalAnomalies");
        private static readonly int _ThermalAnomalyParamsId = Shader.PropertyToID("_H8ThermalAnomalyParams");
        private static readonly int _OpticalExtinctionLutId = Shader.PropertyToID("_Optical_Extinction_LUT");
        private static readonly int _ExtinctionLutId = Shader.PropertyToID("_ExtinctionLUT");
        private static readonly int _ExtinctionCoefficientsId = Shader.PropertyToID("_H8ExtinctionCoefficients");
        private static readonly int _HazardPulseIntensityId = Shader.PropertyToID("_HazardPulseIntensity");
        private static readonly int _HazardPulseParamsId = Shader.PropertyToID("_H8HazardPulseParams");
        private static readonly int _HardwareTierParamsId = Shader.PropertyToID("_H8HardwareTierParams");
        private static readonly int _BiomePaletteId = Shader.PropertyToID("_H8BiomePalette");
        private static readonly int _BiolumMasterPhaseId = Shader.PropertyToID("_BiolumMasterPhase");
        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _AupShiftOffsetId = Shader.PropertyToID("_AupShiftOffset");
        private static readonly int _AupJitterMaskId = Shader.PropertyToID("_AupJitterMask");
        private static readonly int _ExtinctionLutParamsId = Shader.PropertyToID("_ExtinctionLUTParams");
        private static readonly int _ExtinctionLutRuntimeId = Shader.PropertyToID("_ExtinctionLUTRuntime");
        private static readonly int _ExtinctionLutWeatherParamsId = Shader.PropertyToID("_ExtinctionLUTWeatherParams");
        private static readonly int _HectonUberNoirRuntimeParamsId = Shader.PropertyToID("_HectonUberNoirRuntimeParams");
        private static readonly int _HectonActiveShaderFeatureMaskId = Shader.PropertyToID("_HectonActiveShaderFeatureMask");
        private static readonly int _HectonPowerBrownoutParamsId = Shader.PropertyToID("_HectonPowerBrownoutParams");
        private static readonly int _HectonRespawnDearLieParamsId = Shader.PropertyToID("_HectonRespawnDearLieParams");
        private static readonly int _HectonDeathFadeIntensityId = Shader.PropertyToID("_HectonDeathFadeIntensity");
        private static readonly int _HectonDcsPhysiologyParamsId = Shader.PropertyToID("_HectonDcsPhysiologyParams");
        private static readonly int _HectonGasToxicityParamsId = Shader.PropertyToID("_HectonGasToxicityParams");
        private static readonly int _HectonSupersaturationScalarId = Shader.PropertyToID("_HectonSupersaturationScalar");
        private static readonly int _HectonNarcosisScalarId = Shader.PropertyToID("_HectonNarcosisScalar");
        private static readonly int _HypoxiaSignalId = Shader.PropertyToID("_HypoxiaSignal");

        private static GlobalShaderDispatcher s_instance;
        private static CommandBuffer s_commandBuffer;
        private static IDataVault s_cachedVault;
        private static VaultGenerationHandle<float4> s_shaderSlotsHandle;
        private static string s_csvPath;
        private static byte[] s_csvScratch;
        private static long s_csvLastWriteTicks;
        private static bool s_manualOverrideActive;
        private static bool s_csvOverrideActive;
        private static float4 s_manualFogColorDensity;
        private static float4 s_manualCausticFlow;
        private static float4 s_csvFogColorDensity;
        private static float4 s_csvCausticFlow;

        private GraphicsBuffer _thermalAnomalyBuffer;
        private GraphicsBuffer _emptyFloat4Buffer;
        private IDataVault _vault;
        private IResolutionScalerService _resolutionScaler;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private bool _binaryProbeCompleted;
        private bool _generatedEmergencyGlobals;
        private bool _dumpedOverBudget;
        private double _shaderTime;
        private int _telemetryCursor;
        private uint _dispatchTelemetryFrame;
        private int _activeKeywordCount;
        private byte _lastGlobalQualityByte = byte.MaxValue;
        private int _lastLowTierWeightBucket = int.MinValue;
        private Vector4 _lastWakeParams = Vector4.zero;
        private Vector4 _lastThermalParams = Vector4.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_instance = null;
            if (s_commandBuffer != null)
            {
                s_commandBuffer.Release();
                s_commandBuffer = null;
            }

            s_cachedVault = null;
            s_shaderSlotsHandle = default;
            s_csvPath = null;
            s_csvScratch = null;
            s_csvLastWriteTicks = 0L;
            s_manualOverrideActive = false;
            s_csvOverrideActive = false;
            s_manualFogColorDensity = default;
            s_manualCausticFlow = default;
            s_csvFogColorDensity = default;
            s_csvCausticFlow = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying || s_instance != null)
                return;

            GameObject host = new GameObject("H8_GlobalShaderDispatcher"); // COLD ALLOC: GameObject[1] - scene shader dispatch host - owner: GlobalShaderDispatcher
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<GlobalShaderDispatcher>();
        }

        private void Awake()
        {
            if (s_instance != null && !ReferenceEquals(s_instance, this))
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwapListener();
            EnsureCommandBuffer();
            if (EnsureShaderGlobalSlotsRuntime(out IDataVault vault))
                RunBinaryGraveyardProbeCold(vault);
            EnsureGpuBuffers();
            TryLoadCsvOverridesCold();
        }

        private void OnEnable()
        {
            if (s_instance != null && !ReferenceEquals(s_instance, this))
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheRegistryServicesCold(forceRefresh: false);
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(s_instance, this))
                HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false);

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            TryUnregisterHotSwapListener();
            ReleaseGraphicsBuffer(ref _thermalAnomalyBuffer);
            ReleaseGraphicsBuffer(ref _emptyFloat4Buffer);
            _vault = null;
            _resolutionScaler = null;
            _dispatchTelemetryFrame = 0u;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_instance, this))
            {
                HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false);
                s_instance = null;
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _vault = currentService as IDataVault;
                    InvalidateShaderGlobalSlotCache();
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _resolutionScaler = currentService as IResolutionScalerService;
                    break;
            }
        }

        public void LateFrameTick()
        {
            long startTicks = Stopwatch.GetTimestamp();
            EnsureCommandBuffer();
            if (!ValidateLayouts())
            {
                DumpTelemetry(1u);
                return;
            }

            if (!EnsureShaderGlobalSlotsRuntime(out IDataVault vault))
                return;

            GenerateEmergencyMockShaderGlobalsNoIo(vault);

            float deltaTime = math.max(0f, Time.unscaledDeltaTime);
            _shaderTime += deltaTime;
            if (_shaderTime >= ShaderTimeModuloSeconds)
                _shaderTime -= Math.Floor(_shaderTime / ShaderTimeModuloSeconds) * ShaderTimeModuloSeconds;

            float globalQualityWeight01 = ResolveGlobalQualityWeight01();
            float lowTierFloor01 = ResolveLowTierFloor01(globalQualityWeight01);
            float lowTierWeight01 = ResolveLowTierWeight01(globalQualityWeight01, lowTierFloor01);
            RefreshQualityTelemetry(globalQualityWeight01, lowTierWeight01);

            if (!vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
                return;

            ShaderGlobalsDTO dto;
            Vector4 ambient;
            Vector4 extinction;
            Vector4 aupOffset;
            Vector4 resolution;
            Vector4 hazard;
            Vector4 biomePalette;
            int thermalCount;
            Vector4 thermalParams;
            Vector4 biolumMasterPhase;
            Vector4 aupShiftOffset;
            float aupJitterMask;
            Vector4 extinctionLutParams;
            Vector4 extinctionLutRuntime;
            Vector4 extinctionLutWeather;
            Vector4 uberNoirRuntime;
            Vector4 powerBrownout;
            Vector4 respawnDearLie;
            Vector4 physiologyDecompression;
            Vector4 physiologyGasToxicity;
            float uberNoirFeatureMask;
            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return;

                RunMockGlobalDataKernel(slots, lowTierWeight01, (float)_shaderTime, ResolveSectorPhase());

                ref ShaderGlobalsDTO dtoRef = ref ResolveShaderGlobalsRef(slots);
                aupOffset = ResolveAupOffset();
                resolution = ResolveResolutionState();
                hazard = ResolveHazardPulse((float)_shaderTime);
                thermalCount = UpdateThermalPackedSlots(vault, slots, (float)_shaderTime);
                thermalParams = UploadThermalBuffer(slots, thermalCount);

                slots[AupOffsetSlot] = ToFloat4(aupOffset);
                slots[ResolutionSlot] = ToFloat4(resolution);
                slots[HazardSlot] = ToFloat4(hazard);

                dto = dtoRef;
                ambient = ToVector4(slots[AmbientSlot]);
                extinction = ToVector4(slots[ExtinctionCoefficientsSlot]);
                biomePalette = new Vector4(dto.FogColor.x, dto.FogColor.y, dto.FogColor.z, slots[MockWeatherSlot].w);
                biolumMasterPhase = ToVector4(slots[HectonShaderGlobalDataVaultBridge.BiolumMasterPhaseSlot]);
                float4 aupShiftPacked = slots[HectonShaderGlobalDataVaultBridge.AupShiftOffsetSlot];
                aupShiftOffset = ToVector4(aupShiftPacked);
                aupJitterMask = math.saturate(aupShiftPacked.w);
                extinctionLutParams = ToVector4(slots[HectonShaderGlobalDataVaultBridge.WaterExtinctionParamsSlot]);
                extinctionLutRuntime = ToVector4(slots[HectonShaderGlobalDataVaultBridge.WaterExtinctionRuntimeSlot]);
                extinctionLutWeather = ToVector4(slots[HectonShaderGlobalDataVaultBridge.WaterExtinctionWeatherSlot]);
                uberNoirRuntime = ToVector4(slots[HectonShaderGlobalDataVaultBridge.UberNoirRuntimeSlot]);
                powerBrownout = SanitizePowerBrownoutVector(
                    ToVector4(slots[HectonShaderGlobalDataVaultBridge.PowerBrownoutSlot]),
                    globalQualityWeight01);
                respawnDearLie = ToVector4(slots[HectonShaderGlobalDataVaultBridge.RespawnDearLieSlot]);
                ResolvePhysiologyVisualPayloads(
                    slots,
                    globalQualityWeight01,
                    out physiologyDecompression,
                    out physiologyGasToxicity);
                uberNoirFeatureMask = math.clamp(slots[HectonShaderGlobalDataVaultBridge.UberNoirFeatureMaskSlot].x, 0f, 16777215f);
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
            }

            Vector4 wakeParams = UploadDynamicWakeBuffers(lowTierWeight01);
            ExecuteGlobalDispatch(
                in dto,
                ambient,
                extinction,
                aupOffset,
                resolution,
                hazard,
                biomePalette,
                wakeParams,
                thermalParams,
                biolumMasterPhase,
                aupShiftOffset,
                aupJitterMask,
                extinctionLutParams,
                extinctionLutRuntime,
                extinctionLutWeather,
                uberNoirRuntime,
                powerBrownout,
                respawnDearLie,
                physiologyDecompression,
                physiologyGasToxicity,
                uberNoirFeatureMask,
                globalQualityWeight01,
                lowTierWeight01);

            float dispatchMicroseconds = (float)((Stopwatch.GetTimestamp() - startTicks) * s_stopwatchTicksToMicroseconds);
            RecordTelemetry(vault, dispatchMicroseconds, (uint)_activeKeywordCount, 0u);
            if (dispatchMicroseconds > DispatchBudgetMicroseconds)
                DumpTelemetry(2u);
        }

        public static bool TryReadEditorTuning(out UberNoirGlobalTuning tuning)
        {
            tuning = default;
            if (!TryResolveCachedShaderGlobalSlots(out NativeArray<float4> slots))
                return false;

            ref readonly ShaderGlobalsDTO dto = ref ResolveShaderGlobalsReadonlyRef(slots);
            float4 caustic = slots[CausticRuntimeSlot];
            tuning.FogColor = ToVector4(dto.FogColor);
            tuning.FlowVector = new Vector3(dto.FlowVector.x, dto.FlowVector.y, dto.FlowVector.z);
            tuning.FogDensity = dto.FogColor.w;
            tuning.CausticSpeed = caustic.x;
            tuning.FlowMagnitude = dto.FlowMagnitude;
            return true;
        }

        public static bool TryWriteEditorTuning(in UberNoirGlobalTuning tuning)
        {
            if (!EnsureShaderGlobalSlots(out IDataVault vault))
                return false;

            if (!vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
                return false;

            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return false;

                float fogDensity = math.max(0f, tuning.FogDensity);
                float flowMagnitude = math.max(0f, tuning.FlowMagnitude);
                float3 flow = ToFiniteFloat3(tuning.FlowVector);
                flow = NormalizeOrDefault(flow, new float3(1f, 0f, 0f));

                ref ShaderGlobalsDTO dto = ref ResolveShaderGlobalsRef(slots);
                dto.FogColor = new float4(
                    math.saturate(tuning.FogColor.x),
                    math.saturate(tuning.FogColor.y),
                    math.saturate(tuning.FogColor.z),
                    fogDensity);
                dto.FlowVector = flow;
                dto.FlowMagnitude = flowMagnitude;
                slots[CausticRuntimeSlot] = new float4(math.max(0f, tuning.CausticSpeed), 1f, slots[MockWeatherSlot].w, 0f);

                s_manualOverrideActive = true;
                s_manualFogColorDensity = dto.FogColor;
                s_manualCausticFlow = new float4(math.max(0f, tuning.CausticSpeed), flowMagnitude, flow.x, flow.z);
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
            }
        }

        public static void ClearEditorOverrides()
        {
            s_manualOverrideActive = false;
        }

        public static bool TryGetEditorGlobalFlow(out Vector4 flow)
        {
            flow = Vector4.zero;
            if (!TryResolveCachedShaderGlobalSlots(out NativeArray<float4> slots))
                return false;

            ref readonly ShaderGlobalsDTO dto = ref ResolveShaderGlobalsReadonlyRef(slots);
            flow = new Vector4(dto.FlowVector.x, dto.FlowVector.y, dto.FlowVector.z, dto.FlowMagnitude);
            return true;
        }

        public static bool TryGetGizmoWake(int index, out Vector4 wake, out Vector4 vector)
        {
            wake = Vector4.zero;
            vector = Vector4.zero;
            return false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold(bool forceRefresh)
        {
            if (forceRefresh || _vault == null)
                _vault = GlobalRegistry.DataVault;

            if (forceRefresh || _resolutionScaler == null)
                _resolutionScaler = GlobalRegistry.ResolutionScaler;
        }

        private bool EnsureShaderGlobalSlotsRuntime(out IDataVault vault)
        {
            vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return EnsureShaderGlobalSlots(vault);
        }

        private static bool EnsureShaderGlobalSlots(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            return EnsureShaderGlobalSlots(vault);
        }

        private static bool EnsureShaderGlobalSlots(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (ReferenceEquals(vault, s_cachedVault) &&
                TryResolveShaderSlotsHandle(vault, in s_shaderSlotsHandle, out _))
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<float4>(BufferID.ShaderGlobalState, out VaultGenerationHandle<float4> existing) &&
                TryResolveShaderSlotsHandle(vault, in existing, out _))
            {
                s_shaderSlotsHandle = existing;
                s_cachedVault = vault;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<float4> allocated = vault.GetGenerationHandle<float4>(
                BufferID.ShaderGlobalState,
                RequiredShaderGlobalSlots,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!TryResolveShaderSlotsHandle(vault, in allocated, out _))
                return false;

            s_shaderSlotsHandle = allocated;
            s_cachedVault = vault;
            return true;
        }

        private static void InvalidateShaderGlobalSlotCache()
        {
            s_cachedVault = null;
            s_shaderSlotsHandle = default;
        }

        private static bool TryResolveShaderGlobalSlotsLocked(IDataVault vault, out NativeArray<float4> slots)
        {
            slots = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !ReferenceEquals(vault, s_cachedVault))
            {
                return false;
            }

            return TryResolveShaderSlotsHandle(vault, in s_shaderSlotsHandle, out slots);
        }

        private static bool TryResolveCachedShaderGlobalSlots(out NativeArray<float4> slots)
        {
            slots = default;
            IDataVault vault = s_cachedVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   TryResolveShaderSlotsHandle(vault, in s_shaderSlotsHandle, out slots);
        }

        private static bool TryResolveShaderSlotsHandle(
            IDataVault vault,
            in VaultGenerationHandle<float4> handle,
            out NativeArray<float4> slots)
        {
            slots = default;
            return vault != null &&
                   handle.BufferID == (uint)BufferID.ShaderGlobalState &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   vault.TryResolveHandle(in handle, out slots) &&
                   slots.IsCreated &&
                   slots.Length >= RequiredShaderGlobalSlots;
        }

        private static void EnsureCommandBuffer()
        {
            if (s_commandBuffer != null)
                return;

            s_commandBuffer = new CommandBuffer // COLD ALLOC: CommandBuffer[1] - frame global shader upload - owner: GlobalShaderDispatcher
            {
                name = "H8 Global Shader Variables"
            };
        }

        private bool EnsureGpuBuffers()
        {
            if (_emptyFloat4Buffer == null || !_emptyFloat4Buffer.IsValid())
                _emptyFloat4Buffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1); // COLD ALLOC: GraphicsBuffer[1] - empty global buffer sentinel - owner: GlobalShaderDispatcher
            if (_thermalAnomalyBuffer == null || !_thermalAnomalyBuffer.IsValid())
                _thermalAnomalyBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(ThermalAnomalyCapacity); // COLD ALLOC: GraphicsBuffer[8] - thermal anomaly payload - owner: GlobalShaderDispatcher

            return _emptyFloat4Buffer != null && _emptyFloat4Buffer.IsValid() &&
                   _thermalAnomalyBuffer != null && _thermalAnomalyBuffer.IsValid();
        }

        private void RunBinaryGraveyardProbeCold(IDataVault vault)
        {
            if (_binaryProbeCompleted)
                return;

            if (TryFindLegacyShaderConstants())
            {
                _binaryProbeCompleted = true;
                return;
            }

            GenerateEmergencyMockShaderGlobalsNoIo(vault);
        }

        private void GenerateEmergencyMockShaderGlobalsNoIo(IDataVault vault)
        {
            if (_binaryProbeCompleted ||
                vault == null ||
                !vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
            {
                return;
            }

            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return;

                GenerateEmergencyMockShaderGlobals(slots);
                _binaryProbeCompleted = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
            }
        }

        private static bool TryFindLegacyShaderConstants()
        {
            string projectRoot = BuildProjectRootPathCold();
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            string archive = Path.Combine(projectRoot, "Docs", "Archive");
            string streaming = Application.streamingAssetsPath;
            return File.Exists(Path.Combine(archive, "global_shader_constants.h8bin")) ||
                   File.Exists(Path.Combine(archive, "lighting_palettes_007.bin")) ||
                   (!string.IsNullOrEmpty(streaming) &&
                    (File.Exists(Path.Combine(streaming, "global_shader_constants.h8bin")) ||
                     File.Exists(Path.Combine(streaming, "lighting_palettes_007.bin"))));
        }

        private void GenerateEmergencyMockShaderGlobals(NativeArray<float4> slots)
        {
            if (!slots.IsCreated || slots.Length < RequiredShaderGlobalSlots)
                return;

            _generatedEmergencyGlobals = true;
            slots[ShaderGlobalsDtoSlot] = new float4(0.018f, 0.065f, 0.09f, 0.024f);
            slots[ShaderGlobalsDtoSlot + 1] = new float4(0.78f, 0.04f, -0.62f, 0.35f);
            slots[ShaderGlobalsDtoSlot + 2] = new float4(0f, 0f, 0f, 0f);
            slots[MockWeatherSlot] = new float4(0.15f, 0.35f, 0.1f, 0f);
            slots[AmbientSlot] = new float4(0.025f, 0.075f, 0.085f, 0.024f);
            slots[CausticRuntimeSlot] = new float4(0.18f, 0.45f, 0f, 1f);
            slots[ExtinctionCoefficientsSlot] = new float4(0.624f, 0.0434f, 0.0106f, 0.024f);
        }

        private void RunMockGlobalDataKernel(NativeArray<float4> slots, float lowTierWeight01, float shaderTime, float sectorPhase)
        {
            MockGlobalShaderDataKernel kernel = new MockGlobalShaderDataKernel
            {
                Slots = slots,
                ShaderTime = shaderTime,
                SectorPhase = sectorPhase,
                LowTierWeight01 = math.saturate(lowTierWeight01),
                GeneratedEmergencyGlobals = _generatedEmergencyGlobals ? 1 : 0,
                ManualOverrideActive = s_manualOverrideActive ? 1 : 0,
                CsvOverrideActive = s_csvOverrideActive ? 1 : 0,
                ManualFogColorDensity = s_manualFogColorDensity,
                ManualCausticFlow = s_manualCausticFlow,
                CsvFogColorDensity = s_csvFogColorDensity,
                CsvCausticFlow = s_csvCausticFlow
            };
            // Tiny shader-slot kernel runs inline to avoid a same-frame schedule/readback loop.
            kernel.ExecuteInline();
        }

        private static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<ShaderGlobalsDTO>() == ShaderGlobalsDTO.SizeBytes &&
                   UnsafeUtility.SizeOf<MockWeatherState>() == MockWeatherState.SizeBytes &&
                   UnsafeUtility.SizeOf<UberNoirGlobalTuning>() == UberNoirGlobalTuning.SizeBytes &&
                   ShaderGlobalsDTO.SizeBytes == HectonShaderGlobalDataVaultBridge.ShaderGlobalsDtoSlotCount * UnsafeUtility.SizeOf<float4>() &&
                   ShaderGlobalsDtoSlot % 1 == 0 &&
                   (ShaderGlobalsDtoSlot * UnsafeUtility.SizeOf<float4>()) % 16 == 0 &&
                   MockWeatherSlot == HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotStart &&
                   HazardSlot == HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotStart + HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotCount - 1 &&
                   HectonShaderGlobalDataVaultBridge.ValidateSharedSlotMap() &&
                   HectonShaderGlobalDataVaultBridge.PowerBrownoutSlot < TelemetrySlotStart &&
                   HectonShaderGlobalDataVaultBridge.RespawnDearLieSlot < TelemetrySlotStart &&
                   TelemetrySlotStart + TelemetryCapacity <= RequiredShaderGlobalSlots;
        }

        private Vector4 UploadDynamicWakeBuffers(float lowTierWeight01)
        {
            lowTierWeight01 = math.saturate(lowTierWeight01);
            Vector4 fallbackParams = new Vector4(0f, lowTierWeight01, 0f, 0f);
            _lastWakeParams = fallbackParams;
            return _lastWakeParams;
        }

        private int UpdateThermalPackedSlots(IDataVault vault, NativeArray<float4> slots, float shaderTime)
        {
            if (vault == null ||
                !slots.IsCreated ||
                !vault.TryGetGenerationHandle<float3>(BufferID.SubmarineFluidExteriorThermalCenters, out VaultGenerationHandle<float3> centersHandle) ||
                !IsThermalSourceHandleOwned(in centersHandle, BufferID.SubmarineFluidExteriorThermalCenters) ||
                !vault.TryGetGenerationHandle<float>(BufferID.SubmarineFluidExteriorThermalTemperatures, out VaultGenerationHandle<float> temperaturesHandle) ||
                !IsThermalSourceHandleOwned(in temperaturesHandle, BufferID.SubmarineFluidExteriorThermalTemperatures) ||
                !vault.TryGetGenerationHandle<float>(BufferID.SubmarineFluidExteriorThermalLifetimes, out VaultGenerationHandle<float> lifetimesHandle) ||
                !IsThermalSourceHandleOwned(in lifetimesHandle, BufferID.SubmarineFluidExteriorThermalLifetimes))
            {
                return WriteMockThermalPackedSlot(slots, shaderTime);
            }

            bool centersLocked = false;
            bool temperaturesLocked = false;
            bool lifetimesLocked = false;
            try
            {
                centersLocked = vault.TryLockBuffer(BufferID.SubmarineFluidExteriorThermalCenters, SystemID.GraphicsScalability);
                if (!centersLocked)
                    return WriteMockThermalPackedSlot(slots, shaderTime);

                temperaturesLocked = vault.TryLockBuffer(BufferID.SubmarineFluidExteriorThermalTemperatures, SystemID.GraphicsScalability);
                if (!temperaturesLocked)
                    return WriteMockThermalPackedSlot(slots, shaderTime);

                lifetimesLocked = vault.TryLockBuffer(BufferID.SubmarineFluidExteriorThermalLifetimes, SystemID.GraphicsScalability);
                if (!lifetimesLocked)
                    return WriteMockThermalPackedSlot(slots, shaderTime);

                if (!vault.TryResolveHandle(in centersHandle, out NativeArray<float3> centers) ||
                    !vault.TryResolveHandle(in temperaturesHandle, out NativeArray<float> temperatures) ||
                    !vault.TryResolveHandle(in lifetimesHandle, out NativeArray<float> lifetimes) ||
                    !centers.IsCreated ||
                    !temperatures.IsCreated ||
                    !lifetimes.IsCreated)
                {
                    return WriteMockThermalPackedSlot(slots, shaderTime);
                }

                int count = math.min(ThermalAnomalyCapacity, math.min(centers.Length, math.min(temperatures.Length, lifetimes.Length)));
                int active = 0;
                for (int i = 0; i < count; i++)
                {
                    float lifetime = math.max(0f, lifetimes[i]);
                    float temperature = math.max(0f, temperatures[i]);
                    float intensity = lifetime > 0f ? math.saturate((temperature - 18f) * 0.02f) : 0f;
                    float3 center = math.all(math.isfinite(centers[i])) ? centers[i] : float3.zero;
                    slots[ThermalPackedSlotStart + i] = new float4(center, intensity);
                    if (intensity > 0.001f)
                        active++;
                }

                for (int i = count; i < ThermalAnomalyCapacity; i++)
                    slots[ThermalPackedSlotStart + i] = default;

                return active;
            }
            finally
            {
                if (lifetimesLocked)
                    vault.TryUnlockBuffer(BufferID.SubmarineFluidExteriorThermalLifetimes, SystemID.GraphicsScalability);
                if (temperaturesLocked)
                    vault.TryUnlockBuffer(BufferID.SubmarineFluidExteriorThermalTemperatures, SystemID.GraphicsScalability);
                if (centersLocked)
                    vault.TryUnlockBuffer(BufferID.SubmarineFluidExteriorThermalCenters, SystemID.GraphicsScalability);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsThermalSourceHandleOwned<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics;
        }

        private static int WriteMockThermalPackedSlot(NativeArray<float4> slots, float shaderTime)
        {
            if (!slots.IsCreated || slots.Length <= ThermalPackedSlotStart)
                return 0;

            slots[ThermalPackedSlotStart] = new float4(
                math.sin(shaderTime * 0.31f) * 12f,
                -6f,
                math.cos(shaderTime * 0.27f) * 12f,
                0.22f);
            for (int i = 1; i < ThermalAnomalyCapacity; i++)
                slots[ThermalPackedSlotStart + i] = default;
            return 1;
        }

        private Vector4 UploadThermalBuffer(NativeArray<float4> slots, int thermalCount)
        {
            if (!EnsureGpuBuffers() || !slots.IsCreated)
            {
                _lastThermalParams = Vector4.zero;
                return _lastThermalParams;
            }

            int uploadCount = math.min(ThermalAnomalyCapacity, math.max(1, thermalCount));
            UploadFloat4Range(_thermalAnomalyBuffer, slots, ThermalPackedSlotStart, uploadCount);
            _lastThermalParams = new Vector4(uploadCount, thermalCount, thermalCount > 0 ? 1f : 0f, 0f);
            return _lastThermalParams;
        }

        private static void UploadFloat4Range(GraphicsBuffer destination, NativeArray<float4> source, int sourceStart, int count)
        {
            if (destination == null || !destination.IsValid() || !source.IsCreated || count <= 0)
                return;

            NativeArray<float4> target = destination.LockBufferForWrite<float4>(0, count);
            try
            {
                void* src = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + (sourceStart * UnsafeUtility.SizeOf<float4>());
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(target);
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<float4>());
            }
            finally
            {
                destination.UnlockBufferAfterWrite<float4>(count);
            }
        }

        private void ExecuteGlobalDispatch(
            in ShaderGlobalsDTO dto,
            Vector4 ambient,
            Vector4 extinction,
            Vector4 aupOffset,
            Vector4 resolution,
            Vector4 hazard,
            Vector4 biomePalette,
            Vector4 wakeParams,
            Vector4 thermalParams,
            Vector4 biolumMasterPhase,
            Vector4 aupShiftOffset,
            float aupJitterMask,
            Vector4 extinctionLutParams,
            Vector4 extinctionLutRuntime,
            Vector4 extinctionLutWeather,
            Vector4 uberNoirRuntime,
            Vector4 powerBrownout,
            Vector4 respawnDearLie,
            Vector4 physiologyDecompression,
            Vector4 physiologyGasToxicity,
            float uberNoirFeatureMask,
            float globalQualityWeight01,
            float lowTierWeight01)
        {
            CommandBuffer cmd = s_commandBuffer;
            cmd.Clear();
            Vector4 fogColor = ToVector4(dto.FogColor);
            Vector4 flow = new Vector4(dto.FlowVector.x, dto.FlowVector.y, dto.FlowVector.z, dto.FlowMagnitude);

            cmd.SetGlobalVector(_FogColorId, fogColor);
            cmd.SetGlobalFloat(_FogDensityId, fogColor.w);
            cmd.SetGlobalVector(_AmbientLightId, ambient);
            cmd.SetGlobalVector(_GlobalFlowVectorId, flow);
            cmd.SetGlobalVector(_H8GlobalFlowId, flow);
            cmd.SetGlobalFloat(_H8ShaderTimeId, dto.GlobalTime);
            cmd.SetGlobalVector(_WorldOriginOffsetId, aupOffset);
            cmd.SetGlobalVector(_TotalUniverseOffsetId, aupOffset);
            cmd.SetGlobalVector(_HectonFloatingOriginOffsetId, aupOffset);
            cmd.SetGlobalVector(_AupShiftOffsetId, aupShiftOffset);
            cmd.SetGlobalFloat(_AupJitterMaskId, aupJitterMask);
            cmd.SetGlobalFloat(_ResolutionScaleId, resolution.x);
            cmd.SetGlobalVector(_ResolutionScaleParamsId, resolution);
            cmd.SetGlobalFloat(_HazardPulseIntensityId, hazard.x);
            cmd.SetGlobalVector(_HazardPulseParamsId, hazard);
            cmd.SetGlobalVector(_ExtinctionCoefficientsId, extinction);
            cmd.SetGlobalVector(_ExtinctionLutParamsId, extinctionLutParams);
            cmd.SetGlobalVector(_ExtinctionLutRuntimeId, extinctionLutRuntime);
            cmd.SetGlobalVector(_ExtinctionLutWeatherParamsId, extinctionLutWeather);
            cmd.SetGlobalVector(_BiomePaletteId, biomePalette);
            cmd.SetGlobalVector(_HardwareTierParamsId, new Vector4(
                math.saturate(globalQualityWeight01),
                math.saturate(lowTierWeight01),
                _generatedEmergencyGlobals ? 1f : 0f,
                _activeKeywordCount));
            cmd.SetGlobalVector(_BiolumMasterPhaseId, biolumMasterPhase);
            cmd.SetGlobalVector(_HectonUberNoirRuntimeParamsId, uberNoirRuntime);
            cmd.SetGlobalFloat(_HectonActiveShaderFeatureMaskId, uberNoirFeatureMask);
            cmd.SetGlobalVector(_HectonPowerBrownoutParamsId, powerBrownout);
            cmd.SetGlobalVector(_HectonRespawnDearLieParamsId, respawnDearLie);
            cmd.SetGlobalFloat(_HectonDeathFadeIntensityId, math.saturate(respawnDearLie.x));
            cmd.SetGlobalVector(_HectonDcsPhysiologyParamsId, physiologyDecompression);
            cmd.SetGlobalFloat(_HectonSupersaturationScalarId, math.saturate(physiologyDecompression.x));
            cmd.SetGlobalFloat(_HectonNarcosisScalarId, math.saturate(physiologyDecompression.y));
            cmd.SetGlobalVector(_HectonGasToxicityParamsId, physiologyGasToxicity);
            cmd.SetGlobalFloat(_HypoxiaSignalId, math.saturate(physiologyGasToxicity.x));
            cmd.SetGlobalVector(_DynamicWakeParamsId, wakeParams);
            cmd.SetGlobalVector(_ThermalAnomalyParamsId, thermalParams);

            GraphicsBuffer wakeBuffer = _emptyFloat4Buffer;
            GraphicsBuffer wakeVectorBuffer = _emptyFloat4Buffer;
            if (wakeBuffer != null && wakeBuffer.IsValid())
                cmd.SetGlobalBuffer(_DynamicWakesId, wakeBuffer);
            if (wakeVectorBuffer != null && wakeVectorBuffer.IsValid())
                cmd.SetGlobalBuffer(_DynamicWakeVectorsId, wakeVectorBuffer);
            if (_thermalAnomalyBuffer != null && _thermalAnomalyBuffer.IsValid())
                cmd.SetGlobalBuffer(_ThermalAnomaliesId, _thermalAnomalyBuffer);

            Texture extinctionTexture = LutArrayResolver.ExtinctionTexture;
            if (extinctionTexture != null)
            {
                cmd.SetGlobalTexture(_OpticalExtinctionLutId, extinctionTexture);
                cmd.SetGlobalTexture(_ExtinctionLutId, extinctionTexture);
            }

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(true);
        }

        private Vector4 ResolveAupOffset()
        {
            double3 offset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return new Vector4(
                SafeFloat(offset.x),
                SafeFloat(offset.y),
                SafeFloat(offset.z),
                HectonFloatingOrigin.CurrentShiftSequence);
        }

        private Vector4 ResolveResolutionState()
        {
            IResolutionScalerService scaler = _resolutionScaler;
            if (scaler != null && scaler.TryGetScaleState(out ResolutionScaleState state))
            {
                float current = math.saturate(math.isfinite(state.CurrentRenderScale01) ? state.CurrentRenderScale01 : 1f);
                float target = math.saturate(math.isfinite(state.TargetRenderScale01) ? state.TargetRenderScale01 : current);
                float stress = math.saturate(math.isfinite(state.SystemStress01) ? state.SystemStress01 : 0f);
                float quality = math.saturate(math.isfinite(state.GlobalQualityWeight01) ? state.GlobalQualityWeight01 : 1f);
                float fallbackOverkill = Smooth01(math.saturate((quality - 0.78f) * 4.5454545f));
                float overkill = math.saturate(math.isfinite(state.VisualOverkill01) ? state.VisualOverkill01 : fallbackOverkill);
                return new Vector4(current > 0f ? current : 1f, target > 0f ? target : current, stress, overkill);
            }

            float fallbackStress = math.saturate(HomeostasisBrain.SystemHealthIndex01);
            float quality01 = ResolveGlobalQualityWeight01();
            float overkill01 = Smooth01(math.saturate((quality01 - 0.78f) * 4.5454545f));
            return new Vector4(1f, 1f, fallbackStress, overkill01);
        }

        private float ResolveGlobalQualityWeight01()
        {
            IResolutionScalerService scaler = _resolutionScaler;
            if (scaler != null && scaler.TryGetScaleState(out ResolutionScaleState state))
                return math.saturate(math.isfinite(state.GlobalQualityWeight01) ? state.GlobalQualityWeight01 : 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveLowTierWeight01(float qualityWeight01, float lowTierFloor01)
        {
            lowTierFloor01 = math.saturate(lowTierFloor01);
            float fallbackQuality = math.lerp(1f, 0.35f, lowTierFloor01);
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : fallbackQuality);
            float weight = 1f - Smooth01(math.saturate((quality - 0.18f) * 1.2195122f));
            return math.max(weight, lowTierFloor01);
        }

        private static float ResolveLowTierFloor01(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float survivalPressure01 = 1f - Smooth01(math.saturate((quality - 0.12f) * 1.1363636f));
            return 0.25f * survivalPressure01;
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - 2f * value);
        }

        private Vector4 ResolveHazardPulse(float shaderTime)
        {
            float exposure = ResolveRadiationExposureFromSignals();
            if (exposure <= 0.001f)
            {
                float systemStress = math.saturate(HomeostasisBrain.SystemHealthIndex01);
                exposure = systemStress > 0.85f ? math.saturate((systemStress - 0.85f) * 6.666667f) : 0f;
            }

            float pulse = exposure > 0.001f
                ? exposure * (0.55f + (0.45f * math.sin(shaderTime * 6.2831853f * 0.72f)))
                : 0f;
            return new Vector4(math.saturate(pulse), exposure, shaderTime, 0f);
        }

        private static float ResolveRadiationExposureFromSignals()
        {
            float exposure = 0f;
            ReadOnlySpan<RadiationDoseSignal> snapshot = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                RadiationDoseSignal signal = snapshot[i];
                float signalExposure = math.saturate(math.max(signal.Intensity01, signal.Dose));
                exposure = math.max(exposure, signalExposure);
            }

            return exposure;
        }

        private float ResolveSectorPhase()
        {
            double3 offset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            int sx = (int)Math.Floor(offset.x / 256.0);
            int sz = (int)Math.Floor(offset.z / 256.0);
            uint hash = (uint)(sx * 73856093) ^ (uint)(sz * 19349663) ^ 0x9E3779B9u;
            return (hash & 1023u) * (6.2831853f / 1024f);
        }

        private void RefreshQualityTelemetry(float globalQualityWeight01, float lowTierWeight01)
        {
            byte qualityByte = EncodeQualityWeightByte(globalQualityWeight01);
            int lowTierWeightBucket = (int)math.round(math.saturate(lowTierWeight01) * 255f);
            if (_lastGlobalQualityByte == qualityByte && _lastLowTierWeightBucket == lowTierWeightBucket)
                return;

            _lastGlobalQualityByte = qualityByte;
            _lastLowTierWeightBucket = lowTierWeightBucket;
            _activeKeywordCount = 0;
        }

        private static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            return (byte)math.round(quality * 255f);
        }

        private void RecordTelemetry(IDataVault vault, float dispatchMicroseconds, uint keywordCount, uint flags)
        {
            if (!EnsureShaderGlobalSlotsRuntime(out IDataVault currentVault))
                return;

            if (!ReferenceEquals(vault, currentVault))
                vault = currentVault;

            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
                return;

            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return;

                if (!slots.IsCreated || slots.Length < TelemetrySlotStart + TelemetryCapacity)
                    return;

                int slot = TelemetrySlotStart + _telemetryCursor;
                uint frame = unchecked(_dispatchTelemetryFrame + 1u);
                if (frame == 0u)
                    frame = 1u;
                _dispatchTelemetryFrame = frame;
                slots[slot] = new float4(frame, dispatchMicroseconds, keywordCount, flags);
                _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
            }
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            if (_dumpedOverBudget && reasonFlags == 2u)
                return;

            _dumpedOverBudget = true;
            string projectRoot = BuildProjectRootPathCold();
            if (string.IsNullOrEmpty(projectRoot))
                return;

            if (!EnsureShaderGlobalSlotsRuntime(out IDataVault vault))
                return;

            string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Span<float4> telemetrySnapshot = stackalloc float4[TelemetryCapacity];
            int telemetryCursor = 0;
            if (!vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
                return;

            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots) ||
                    !slots.IsCreated ||
                    slots.Length < TelemetrySlotStart + TelemetryCapacity)
                {
                    return;
                }

                for (int i = 0; i < TelemetryCapacity; i++)
                    telemetrySnapshot[i] = slots[TelemetrySlotStart + i];
                telemetryCursor = _telemetryCursor;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
            }

            TryWriteTelemetryDump(directory, DumpFileName, telemetrySnapshot, telemetryCursor, reasonFlags);
            TryWriteTelemetryDump(directory, DumpH8DumpFileName, telemetrySnapshot, telemetryCursor, reasonFlags);
        }

        private static void TryWriteTelemetryDump(string directory, string fileName, ReadOnlySpan<float4> telemetrySnapshot, int telemetryCursor, uint reasonFlags)
        {
            string path = Path.Combine(directory, fileName);
            try
            {
                Directory.CreateDirectory(directory);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(DumpMagic);
                writer.Write(TelemetryCapacity);
                writer.Write(telemetryCursor);
                writer.Write(reasonFlags);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    float4 entry = telemetrySnapshot[i];
                    writer.Write(entry.x);
                    writer.Write(entry.y);
                    writer.Write(entry.z);
                    writer.Write(entry.w);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[GlobalShaderDispatcher] Failed CBuffer telemetry dump: " + exception.Message);
            }
        }

        private void TryLoadCsvOverridesCold()
        {
            if (string.IsNullOrEmpty(s_csvPath))
            {
                string root = BuildProjectRootPathCold();
                s_csvPath = string.IsNullOrEmpty(root) ? CsvOverrideFileName : Path.Combine(root, CsvOverrideFileName);
            }

            try
            {
                if (!File.Exists(s_csvPath))
                {
                    s_csvOverrideActive = false;
                    return;
                }

                long ticks = File.GetLastWriteTimeUtc(s_csvPath).Ticks;
                if (ticks == s_csvLastWriteTicks)
                    return;

                s_csvLastWriteTicks = ticks;
                byte[] scratch = AcquireCsvScratchCold();
                using FileStream stream = new FileStream(s_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int length = stream.Read(scratch, 0, scratch.Length);
                if (TryParseCsvOverride(scratch, length, out float4 fogColorDensity, out float4 causticFlow))
                {
                    s_csvFogColorDensity = fogColorDensity;
                    s_csvCausticFlow = causticFlow;
                    s_csvOverrideActive = true;
                }
            }
            catch (Exception exception)
            {
                s_csvOverrideActive = false;
                UnityEngine.Debug.LogWarning("[GlobalShaderDispatcher] CSV override parse failed: " + exception.Message);
            }
        }

        private static byte[] AcquireCsvScratchCold()
        {
            if (s_csvScratch == null)
                s_csvScratch = new byte[CsvScratchBytes]; // COLD ALLOC: byte[4096] - CSV override read window - owner: GlobalShaderDispatcher

            return s_csvScratch;
        }

        private static bool TryParseCsvOverride(byte[] bytes, int length, out float4 fogColorDensity, out float4 causticFlow)
        {
            fogColorDensity = default;
            causticFlow = default;
            float v0;
            float v1;
            float v2;
            float v3;
            float v4;
            float v5;
            int cursor = 0;
            if (!TryReadNextFloat(bytes, length, ref cursor, out v0) ||
                !TryReadNextFloat(bytes, length, ref cursor, out v1) ||
                !TryReadNextFloat(bytes, length, ref cursor, out v2) ||
                !TryReadNextFloat(bytes, length, ref cursor, out v3) ||
                !TryReadNextFloat(bytes, length, ref cursor, out v4) ||
                !TryReadNextFloat(bytes, length, ref cursor, out v5))
            {
                return false;
            }

            fogColorDensity = new float4(math.saturate(v1), math.saturate(v2), math.saturate(v3), math.max(0f, v0));
            causticFlow = new float4(math.max(0f, v4), math.max(0f, v5), 1f, 0f);
            return true;
        }

        private static bool TryReadNextFloat(byte[] bytes, int length, ref int cursor, out float value)
        {
            value = 0f;
            while (cursor < length && !IsFloatStart(bytes[cursor]))
                cursor++;
            if (cursor >= length)
                return false;

            int sign = 1;
            if (bytes[cursor] == (byte)'-' || bytes[cursor] == (byte)'+')
            {
                sign = bytes[cursor] == (byte)'-' ? -1 : 1;
                cursor++;
            }

            double integer = 0d;
            bool any = false;
            while (cursor < length && IsDigit(bytes[cursor]))
            {
                any = true;
                integer = (integer * 10d) + (bytes[cursor] - (byte)'0');
                cursor++;
            }

            double fraction = 0d;
            double scale = 1d;
            if (cursor < length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < length && IsDigit(bytes[cursor]))
                {
                    any = true;
                    fraction = (fraction * 10d) + (bytes[cursor] - (byte)'0');
                    scale *= 10d;
                    cursor++;
                }
            }

            if (!any)
                return false;

            double result = sign * (integer + (fraction / scale));
            if (cursor < length && (bytes[cursor] == (byte)'e' || bytes[cursor] == (byte)'E'))
            {
                cursor++;
                int exponentSign = 1;
                if (cursor < length && (bytes[cursor] == (byte)'-' || bytes[cursor] == (byte)'+'))
                {
                    exponentSign = bytes[cursor] == (byte)'-' ? -1 : 1;
                    cursor++;
                }

                int exponent = 0;
                bool exponentAny = false;
                while (cursor < length && IsDigit(bytes[cursor]))
                {
                    exponentAny = true;
                    exponent = (exponent * 10) + (bytes[cursor] - (byte)'0');
                    cursor++;
                }

                if (exponentAny)
                    result *= Math.Pow(10d, exponent * exponentSign);
            }

            value = math.isfinite((float)result) ? (float)result : 0f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFloatStart(byte value)
        {
            return IsDigit(value) || value == (byte)'-' || value == (byte)'+' || value == (byte)'.';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        private static string BuildProjectRootPathCold()
        {
            string dataPath = Application.dataPath;
            return string.IsNullOrEmpty(dataPath) ? null : Path.GetFullPath(Path.Combine(dataPath, ".."));
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref ShaderGlobalsDTO ResolveShaderGlobalsRef(NativeArray<float4> slots)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(slots);
            return ref UnsafeUtility.AsRef<ShaderGlobalsDTO>((byte*)basePtr + (ShaderGlobalsDtoSlot * UnsafeUtility.SizeOf<float4>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref readonly ShaderGlobalsDTO ResolveShaderGlobalsReadonlyRef(NativeArray<float4> slots)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(slots);
            return ref UnsafeUtility.AsRef<ShaderGlobalsDTO>((byte*)basePtr + (ShaderGlobalsDtoSlot * UnsafeUtility.SizeOf<float4>()));
        }

        private static void ResolvePhysiologyVisualPayloads(
            NativeArray<float4> slots,
            float globalQualityWeight01,
            out Vector4 decompression,
            out Vector4 gasToxicity)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight01) ? globalQualityWeight01 : 1f);
            float4 decompressionPayload = slots[HectonShaderGlobalDataVaultBridge.PhysiologyDecompressionSlot];
            float4 gasPayload = slots[HectonShaderGlobalDataVaultBridge.PhysiologyGasToxicitySlot];

            SanitizePhysiologyVisualPayloads(ref decompressionPayload, ref gasPayload, quality);

            ReadOnlySpan<PhysiologyStateSignal> signals = SignalBus<PhysiologyStateSignal>.GetFrameSnapshot();
            if (signals.Length > 0)
            {
                for (int i = 0; i < signals.Length; i++)
                {
                    PhysiologyStateSignal signal = signals[i];
                    ApplyPhysiologyVisualSignal(in signal, quality, ref decompressionPayload, ref gasPayload);
                }
            }

            if (GlobalSignals.TryGetLatestPhysiologyStateSignal(out PhysiologyStateSignal latest, out int sequence) && sequence > 0)
                ApplyPhysiologyVisualSignal(in latest, quality, ref decompressionPayload, ref gasPayload);

            decompressionPayload.w = quality;
            gasPayload.w = quality;
            slots[HectonShaderGlobalDataVaultBridge.PhysiologyDecompressionSlot] = decompressionPayload;
            slots[HectonShaderGlobalDataVaultBridge.PhysiologyGasToxicitySlot] = gasPayload;
            decompression = ToVector4(decompressionPayload);
            gasToxicity = ToVector4(gasPayload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyPhysiologyVisualSignal(
            in PhysiologyStateSignal signal,
            float quality,
            ref float4 decompressionPayload,
            ref float4 gasPayload)
        {
            if (signal.Cause == PhysiologyStateSignal.CauseDecompression)
            {
                decompressionPayload.x = math.saturate(signal.Supersaturation01);
                decompressionPayload.y = math.saturate(signal.Narcosis01);
                decompressionPayload.z = math.max(0f, math.isfinite(signal.AmbientPressureAtm) ? signal.AmbientPressureAtm : 0f);
                decompressionPayload.w = quality;
            }
            else if (signal.SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology &&
                     signal.Cause == PhysiologyStateSignal.CauseGasToxicity)
            {
                gasPayload.x = math.saturate(signal.Supersaturation01);
                gasPayload.y = signal.GasCnsSeverity * (1f / 255f);
                gasPayload.z = signal.GasCarbonDioxideSeverity * (1f / 255f);
                gasPayload.w = quality;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SanitizePhysiologyVisualPayloads(ref float4 decompressionPayload, ref float4 gasPayload, float quality)
        {
            decompressionPayload.x = math.saturate(math.isfinite(decompressionPayload.x) ? decompressionPayload.x : 0f);
            decompressionPayload.y = math.saturate(math.isfinite(decompressionPayload.y) ? decompressionPayload.y : 0f);
            decompressionPayload.z = math.max(0f, math.isfinite(decompressionPayload.z) ? decompressionPayload.z : 0f);
            decompressionPayload.w = math.saturate(math.isfinite(decompressionPayload.w) ? decompressionPayload.w : quality);
            gasPayload.x = math.saturate(math.isfinite(gasPayload.x) ? gasPayload.x : 0f);
            gasPayload.y = math.saturate(math.isfinite(gasPayload.y) ? gasPayload.y : 0f);
            gasPayload.z = math.saturate(math.isfinite(gasPayload.z) ? gasPayload.z : 0f);
            gasPayload.w = math.saturate(math.isfinite(gasPayload.w) ? gasPayload.w : quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 SanitizePowerBrownoutVector(Vector4 value, float fallbackQuality01)
        {
            float fallbackQuality = math.saturate(math.isfinite(fallbackQuality01) ? fallbackQuality01 : 1f);
            bool uninitialized =
                math.isfinite(value.x) &&
                math.isfinite(value.y) &&
                math.isfinite(value.z) &&
                math.isfinite(value.w) &&
                math.abs(value.x) + math.abs(value.y) + math.abs(value.z) + math.abs(value.w) <= 0.0001f;
            if (uninitialized)
            {
                Vector4 fallback = default;
                fallback.x = 1f;
                fallback.w = fallbackQuality;
                return fallback;
            }

            float supply = math.saturate(math.isfinite(value.x) ? value.x : 1f);
            float severity = math.saturate(math.isfinite(value.y) ? value.y : 0f);
            float phase = math.max(0f, math.isfinite(value.z) ? value.z : 0f);
            float quality = math.saturate(math.isfinite(value.w) ? value.w : fallbackQuality);
            Vector4 result = default;
            result.x = supply;
            result.y = severity;
            result.z = phase;
            result.w = quality;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 ToFloat4(Vector4 value)
        {
            return new float4(value.x, value.y, value.z, value.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFiniteFloat3(Vector3 value)
        {
            float3 result = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(result)) ? result : new float3(1f, 0f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeFloat(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0f;
            return (float)math.clamp(value, -1000000.0, 1000000.0);
        }

        private struct MockGlobalShaderDataKernel
        {
            public NativeArray<float4> Slots;
            public float ShaderTime;
            public float SectorPhase;
            public float LowTierWeight01;
            public int GeneratedEmergencyGlobals;
            public int ManualOverrideActive;
            public int CsvOverrideActive;
            public float4 ManualFogColorDensity;
            public float4 ManualCausticFlow;
            public float4 CsvFogColorDensity;
            public float4 CsvCausticFlow;

            public void ExecuteInline()
            {
                float storm = 0.5f + (0.5f * math.sin((ShaderTime * 0.071f) + SectorPhase));
                float turbidity = math.saturate(0.28f + (storm * 0.44f));
                float heat = 0.5f + (0.5f * math.sin((ShaderTime * 0.119f) + 1.7f + SectorPhase));
                float biome = 0.5f - (0.5f * math.cos(math.fmod(ShaderTime, 5f) * 1.2566371f));

                float4 fogA = new float4(0.012f, 0.045f, 0.066f, 0.018f + (0.018f * turbidity));
                float4 fogB = new float4(0.052f, 0.018f, 0.035f, 0.028f + (0.012f * heat));
                float smoothBiome = biome * biome * (3f - (2f * biome));
                float4 fogColorDensity = math.lerp(fogA, fogB, smoothBiome);
                float causticSpeed = 0.16f + (0.24f * (1f - storm));
                float3 flowVector = new float3(
                    math.sin((ShaderTime * 0.037f) + SectorPhase),
                    0.04f * math.sin(ShaderTime * 0.021f),
                    math.cos((ShaderTime * 0.041f) + SectorPhase));
                float flowLengthSq = math.lengthsq(flowVector);
                flowVector = math.isfinite(flowLengthSq) && flowLengthSq > 0.0001f
                    ? flowVector * math.rsqrt(math.max(flowLengthSq, 0.0001f))
                    : new float3(1f, 0f, 0f);
                float lowTierWeight = math.saturate(LowTierWeight01);
                float lowFlowMagnitude = 0.32f + (0.18f * storm);
                float highFlowMagnitude = 0.78f + (0.38f * storm);
                float flowMagnitude = math.lerp(highFlowMagnitude, lowFlowMagnitude, lowTierWeight);

                if (CsvOverrideActive != 0)
                {
                    fogColorDensity = CsvFogColorDensity;
                    causticSpeed = CsvCausticFlow.x;
                    flowMagnitude = CsvCausticFlow.y;
                }

                if (ManualOverrideActive != 0)
                {
                    fogColorDensity = ManualFogColorDensity;
                    causticSpeed = ManualCausticFlow.x;
                    flowMagnitude = ManualCausticFlow.y;
                    float3 manualFlow = new float3(ManualCausticFlow.z, 0f, ManualCausticFlow.w);
                    float manualFlowLengthSq = math.lengthsq(manualFlow);
                    flowVector = math.isfinite(manualFlowLengthSq) && manualFlowLengthSq > 0.0001f
                        ? manualFlow * math.rsqrt(math.max(manualFlowLengthSq, 0.0001f))
                        : flowVector;
                }

                if (GeneratedEmergencyGlobals != 0)
                    fogColorDensity.w = math.max(fogColorDensity.w, 0.018f);

                Slots[ShaderGlobalsDtoSlot] = fogColorDensity;
                Slots[ShaderGlobalsDtoSlot + 1] = new float4(flowVector, flowMagnitude);
                Slots[ShaderGlobalsDtoSlot + 2] = new float4(ShaderTime, 0f, 0f, 0f);
                Slots[MockWeatherSlot] = new float4(storm, turbidity, heat, smoothBiome);
                Slots[AmbientSlot] = new float4(fogColorDensity.xyz * (0.55f + (0.25f * (1f - storm))), fogColorDensity.w);
                Slots[CausticRuntimeSlot] = new float4(causticSpeed, math.lerp(0.75f * (1f - storm), 0.12f, lowTierWeight), smoothBiome, storm);
                Slots[ExtinctionCoefficientsSlot] = new float4(0.624f, 0.0434f * (1f + turbidity), 0.0106f * (1f + (turbidity * 0.5f)), fogColorDensity.w);
            }
        }
    }
}
