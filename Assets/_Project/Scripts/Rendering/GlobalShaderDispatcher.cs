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
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

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
        private const double ShaderTimeModuloSeconds = 3600.0;
        private static readonly double s_stopwatchTicksToMicroseconds = 1000000.0 / Stopwatch.Frequency;
        private const float DispatchBudgetMicroseconds = 100f;
        private const ulong ShaderGlobalStateMutationGuardMask =
            1UL << ((int)BufferID.ShaderGlobalState & 31);
        private const ulong ThermalSourceReadGuardMask =
            (1UL << ((int)BufferID.SubmarineFluidExteriorThermalCenters & 31)) |
            (1UL << ((int)BufferID.SubmarineFluidExteriorThermalTemperatures & 31)) |
            (1UL << ((int)BufferID.SubmarineFluidExteriorThermalLifetimes & 31));
        private const uint TelemetryFlagVaultUnavailable = 1u << 2;
        private const uint TelemetryDumpMagic = 0x47534844u; // GSHD
        private const uint TelemetryDumpVersion = 1u;
        private const int TelemetryDumpHeaderBytes = 32;
        private const int TelemetryDumpEntryBytes = 16;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_GLOBAL_SHADER_DISPATCHER.bin";
        private const uint PhysiologyVisualHoldFrames = 24u;
#if UNITY_EDITOR
        private const int CsvScratchBytes = 4096;
        private const string CsvOverrideFileName = "shader_globals_override.csv";
#endif

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
        private static readonly int _HectonSuitCrushDearLieParamsId = Shader.PropertyToID("_HectonSuitCrushDearLieParams");
        private static readonly int _HectonSuitCrushBucklingId = Shader.PropertyToID("_HectonSuitCrushBuckling");
        private static readonly int _HectonRadiationMutationParamsId = Shader.PropertyToID("_HectonRadiationMutationParams");
        private static readonly int _HectonHandRadiationMutation01Id = Shader.PropertyToID("_HectonHandRadiationMutation01");
        private static readonly int _HectonDcsPhysiologyParamsId = Shader.PropertyToID("_HectonDcsPhysiologyParams");
        private static readonly int _HectonGasToxicityParamsId = Shader.PropertyToID("_HectonGasToxicityParams");
        private static readonly int _HectonSupersaturationScalarId = Shader.PropertyToID("_HectonSupersaturationScalar");
        private static readonly int _HectonNarcosisScalarId = Shader.PropertyToID("_HectonNarcosisScalar");
        private static readonly int _HypoxiaSignalId = Shader.PropertyToID("_HypoxiaSignal");

        private static GlobalShaderDispatcher s_instance;
        private static IDataVault s_cachedVault;
        private static VaultGenerationHandle<float4> s_shaderSlotsHandle;
        private static bool s_shaderSlotsValidated;
        private static uint s_lastDecompressionVisualSignalFrame;
        private static uint s_lastGasToxicityVisualSignalFrame;
#if UNITY_EDITOR
        private static string s_csvPath;
        private static byte[] s_csvScratch;
        private static long s_csvLastWriteTicks;
        private static bool s_editorReloadHooked;
#endif
        private static bool s_manualOverrideActive;
#if UNITY_EDITOR
        private static bool s_csvOverrideActive;
#endif
        private static float4 s_manualFogColorDensity;
        private static float4 s_manualCausticFlow;
#if UNITY_EDITOR
        private static float4 s_csvFogColorDensity;
        private static float4 s_csvCausticFlow;
#endif

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
        private int _lastSurvivalPressureBucket = int.MinValue;
        private Vector4 _lastWakeParams = Vector4.zero;
        private Vector4 _lastThermalParams = Vector4.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (s_instance != null)
                s_instance.ReleaseGpuBuffersForLifecycleReset();

            s_instance = null;
            s_cachedVault = null;
            s_shaderSlotsHandle = default;
            s_shaderSlotsValidated = false;
            s_lastDecompressionVisualSignalFrame = 0u;
            s_lastGasToxicityVisualSignalFrame = 0u;
#if UNITY_EDITOR
            s_csvPath = null;
            s_csvScratch = null;
            s_csvLastWriteTicks = 0L;
#endif
            s_manualOverrideActive = false;
#if UNITY_EDITOR
            s_csvOverrideActive = false;
            s_editorReloadHooked = false;
#endif
            s_manualFogColorDensity = default;
            s_manualCausticFlow = default;
#if UNITY_EDITOR
            s_csvFogColorDensity = default;
            s_csvCausticFlow = default;
#endif
        }

#if UNITY_EDITOR
        private static void EnsureEditorReloadHook()
        {
            if (s_editorReloadHooked)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            s_editorReloadHooked = true;
        }

        private static void HandleBeforeAssemblyReload()
        {
            GlobalShaderDispatcher instance = s_instance;
            if (instance == null)
                return;

            instance.ReleaseGpuBuffersForLifecycleReset();
        }
#endif

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
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            EnsureEditorReloadHook();
#endif
            if (s_instance != null && !ReferenceEquals(s_instance, this))
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwapListener();
            if (EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: true))
                RunBinaryGraveyardProbeCold(vault);
            EnsureGpuBuffers();
#if UNITY_EDITOR
            TryLoadCsvOverridesCold();
#endif
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            EnsureEditorReloadHook();
#endif
            if (s_instance != null && !ReferenceEquals(s_instance, this))
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheRegistryServicesCold(forceRefresh: false);
            TryRegisterHotSwapListener();
            if (EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: true))
                RunBinaryGraveyardProbeCold(vault);
            EnsureGpuBuffers();
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
            RebindDataVaultForLifecycle(null);
            _resolutionScaler = null;
            _dispatchTelemetryFrame = 0u;
        }

        private void ReleaseGpuBuffersForLifecycleReset()
        {
            ReleaseGraphicsBuffer(ref _thermalAnomalyBuffer);
            ReleaseGraphicsBuffer(ref _emptyFloat4Buffer);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            ReleaseGpuBuffersForLifecycleReset();
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
                    RebindDataVaultForLifecycle(currentService as IDataVault);
                    if (isActiveAndEnabled &&
                        EnsureShaderGlobalSlotsRuntime(out IDataVault vault, allowAllocation: true))
                    {
                        RunBinaryGraveyardProbeCold(vault);
                    }
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _resolutionScaler = currentService as IResolutionScalerService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrameTickable();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterLateFrameTickable();

                    break;
            }
        }

        public void LateFrameTick()
        {
            long startTicks = Stopwatch.GetTimestamp();
            HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false);

            if (!ValidateLayouts())
            {
                DumpTelemetry(1u);
                return;
            }

            if (!TryResolveShaderGlobalSlotsRuntime(out IDataVault vault))
                return;

            GenerateEmergencyMockShaderGlobalsNoIo(vault);

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            _shaderTime += deltaTime;
            if (_shaderTime >= ShaderTimeModuloSeconds)
                _shaderTime -= Math.Floor(_shaderTime / ShaderTimeModuloSeconds) * ShaderTimeModuloSeconds;

            float globalQualityWeight01 = ResolveGlobalQualityWeight01();
            float survivalPressureFloor01 = ResolveSurvivalPressureFloor01(globalQualityWeight01);
            float survivalPressureWeight01 = ResolveSurvivalPressureWeight01(globalQualityWeight01, survivalPressureFloor01);
            RefreshQualityTelemetry(globalQualityWeight01, survivalPressureWeight01);

            float shaderTime = (float)_shaderTime;
            Span<float4> thermalPackedSlots = stackalloc float4[ThermalAnomalyCapacity];
            int thermalCount = BuildThermalPackedSnapshot(vault, thermalPackedSlots, shaderTime);
            float sectorPhase = ResolveSectorPhase();
            Vector4 preparedAupOffset = ResolveAupOffset();
            Vector4 preparedResolution = ResolveResolutionState();
            Vector4 preparedHazard = ResolveHazardPulse(shaderTime);
            PreparedMockGlobalSlots preparedMockSlots = BuildMockGlobalDataPayload(
                survivalPressureWeight01,
                shaderTime,
                sectorPhase);
            PreparedPhysiologyVisualPayloads preparedPhysiologyPayloads =
                PreparePhysiologyVisualPayloads(globalQualityWeight01);

            if (!vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
                return;

            ShaderGlobalsDTO dto;
            Vector4 ambient;
            Vector4 extinction;
            Vector4 aupOffset;
            Vector4 resolution;
            Vector4 hazard;
            Vector4 biomePalette;
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
            Vector4 suitCrushDearLie;
            Vector4 radiationMutation;
            Vector4 physiologyDecompression;
            Vector4 physiologyGasToxicity;
            float uberNoirFeatureMask;
            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return;

                CopyMockGlobalDataSlots(slots, in preparedMockSlots);

                ref ShaderGlobalsDTO dtoRef = ref ResolveShaderGlobalsRef(slots);
                aupOffset = preparedAupOffset;
                resolution = preparedResolution;
                hazard = preparedHazard;
                CopyThermalPackedSlots(slots, thermalPackedSlots);

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
                suitCrushDearLie = ToVector4(slots[HectonShaderGlobalDataVaultBridge.SuitCrushDearLieSlot]);
                radiationMutation = ToVector4(slots[HectonShaderGlobalDataVaultBridge.RadiationMutationSlot]);
                ResolvePhysiologyVisualPayloads(
                    slots,
                    in preparedPhysiologyPayloads,
                    out physiologyDecompression,
                    out physiologyGasToxicity);
                uberNoirFeatureMask = math.clamp(slots[HectonShaderGlobalDataVaultBridge.UberNoirFeatureMaskSlot].x, 0f, 16777215f);
            }
            finally
            {
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }

            thermalParams = UploadThermalBuffer(thermalPackedSlots, thermalCount);
            Vector4 wakeParams = UploadDynamicWakeBuffers(survivalPressureWeight01);
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
                suitCrushDearLie,
                radiationMutation,
                physiologyDecompression,
                physiologyGasToxicity,
                uberNoirFeatureMask,
                globalQualityWeight01,
                survivalPressureWeight01);

            float dispatchMicroseconds = (float)((Stopwatch.GetTimestamp() - startTicks) * s_stopwatchTicksToMicroseconds);
            RecordTelemetry(vault, dispatchMicroseconds, (uint)_activeKeywordCount, 0u);
            if (dispatchMicroseconds > DispatchBudgetMicroseconds)
                DumpTelemetry(2u);
        }

        public static bool TryReadEditorTuning(out UberNoirGlobalTuning tuning)
        {
            tuning = default;
            Span<float4> slots = stackalloc float4[CausticRuntimeSlot - ShaderGlobalsDtoSlot + 1];
            if (!TryCopyCachedShaderGlobalSlots(ShaderGlobalsDtoSlot, slots))
                return false;

            float4 fogColorDensity = slots[0];
            float4 flowMagnitude = slots[1];
            float4 caustic = slots[CausticRuntimeSlot - ShaderGlobalsDtoSlot];
            tuning.FogColor = ToVector4(fogColorDensity);
            tuning.FlowVector = new Vector3(flowMagnitude.x, flowMagnitude.y, flowMagnitude.z);
            tuning.FogDensity = fogColorDensity.w;
            tuning.CausticSpeed = caustic.x;
            tuning.FlowMagnitude = flowMagnitude.w;
            return true;
        }

        public static bool TryWriteEditorTuning(in UberNoirGlobalTuning tuning)
        {
            if (!EnsureShaderGlobalSlots(out IDataVault vault, allowAllocation: true))
                return false;

            if (!vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
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
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }
        }

        public static void ClearEditorOverrides()
        {
            s_manualOverrideActive = false;
        }

        public static bool TryGetEditorGlobalFlow(out Vector4 flow)
        {
            flow = Vector4.zero;
            Span<float4> slots = stackalloc float4[1];
            if (!TryCopyCachedShaderGlobalSlots(ShaderGlobalsDtoSlot + 1, slots))
                return false;

            float4 flowMagnitude = slots[0];
            flow = new Vector4(flowMagnitude.x, flowMagnitude.y, flowMagnitude.z, flowMagnitude.w);
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
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
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
                RebindDataVaultForLifecycle(GlobalRegistry.DataVault);

            if (forceRefresh || _resolutionScaler == null)
                _resolutionScaler = GlobalRegistry.ResolutionScaler;
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            InvalidateShaderGlobalSlotCache();
            _telemetryCursor = 0;
            _dispatchTelemetryFrame = 0u;
            _binaryProbeCompleted = false;
            _generatedEmergencyGlobals = false;
            _dumpedOverBudget = false;
        }

        private bool EnsureShaderGlobalSlotsRuntime(out IDataVault vault, bool allowAllocation)
        {
            vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return EnsureShaderGlobalSlots(vault, allowAllocation);
        }

        private bool TryResolveShaderGlobalSlotsRuntime(out IDataVault vault)
        {
            vault = _vault;
            return TryResolvePreparedShaderGlobalSlots(vault);
        }

        private static bool EnsureShaderGlobalSlots(out IDataVault vault, bool allowAllocation)
        {
            vault = GlobalRegistry.DataVault;
            return EnsureShaderGlobalSlots(vault, allowAllocation);
        }

        private static bool EnsureShaderGlobalSlots(IDataVault vault, bool allowAllocation)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (TryResolvePreparedShaderGlobalSlots(vault))
                return true;

            if (!allowAllocation || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<float4> allocated = vault.EnsureGenerationHandle<float4>(
                BufferID.ShaderGlobalState,
                RequiredShaderGlobalSlots,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsShaderSlotsHandle(in allocated) ||
                !TryValidateShaderGlobalSlotsGuarded(vault, in allocated))
                return false;

            s_shaderSlotsHandle = allocated;
            s_cachedVault = vault;
            s_shaderSlotsValidated = true;
            HectonShaderGlobalDataVaultBridge.BindPreparedShaderGlobalSlots(vault);
            return true;
        }

        private static bool TryResolvePreparedShaderGlobalSlots(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (ReferenceEquals(vault, s_cachedVault) &&
                IsShaderSlotsHandle(in s_shaderSlotsHandle))
            {
                if (!s_shaderSlotsValidated &&
                    !TryValidateShaderGlobalSlotsGuarded(vault, in s_shaderSlotsHandle))
                {
                    InvalidateShaderGlobalSlotCache();
                    return false;
                }

                s_shaderSlotsValidated = true;
                HectonShaderGlobalDataVaultBridge.BindPreparedShaderGlobalSlots(vault);
                return true;
            }

            if (vault.TryGetGenerationHandle<float4>(BufferID.ShaderGlobalState, out VaultGenerationHandle<float4> existing) &&
                IsShaderSlotsHandle(in existing))
            {
                if (!TryValidateShaderGlobalSlotsGuarded(vault, in existing))
                    return false;

                s_shaderSlotsHandle = existing;
                s_cachedVault = vault;
                s_shaderSlotsValidated = true;
                HectonShaderGlobalDataVaultBridge.BindPreparedShaderGlobalSlots(vault);
                return true;
            }

            return false;
        }

        private static void InvalidateShaderGlobalSlotCache()
        {
            s_cachedVault = null;
            s_shaderSlotsHandle = default;
            s_shaderSlotsValidated = false;
        }

        private static bool TryValidateShaderGlobalSlotsGuarded(
            IDataVault vault,
            in VaultGenerationHandle<float4> handle)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsShaderSlotsHandle(in handle) ||
                !vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
            {
                return false;
            }

            try
            {
                return vault.TryResolveHandle(in handle, out NativeArray<float4> slots) &&
                       slots.IsCreated &&
                       slots.Length >= RequiredShaderGlobalSlots;
            }
            finally
            {
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }
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

            bool resolved = TryResolveShaderSlotsHandle(vault, in s_shaderSlotsHandle, out slots);
            if (!resolved)
                InvalidateShaderGlobalSlotCache();

            return resolved;
        }

        private static bool TryCopyCachedShaderGlobalSlots(int startSlot, Span<float4> destination)
        {
            if (destination.Length <= 0 ||
                startSlot < 0 ||
                startSlot > RequiredShaderGlobalSlots - destination.Length)
            {
                return false;
            }

            IDataVault vault = s_cachedVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsShaderSlotsHandle(in s_shaderSlotsHandle) ||
                !vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!vault.TryReadOnlyHandle(in s_shaderSlotsHandle, out NativeArray<float4>.ReadOnly slots) ||
                    slots.Length < startSlot + destination.Length)
                {
                    InvalidateShaderGlobalSlotCache();
                    return false;
                }

                for (int i = 0; i < destination.Length; i++)
                    destination[i] = slots[startSlot + i];

                s_shaderSlotsValidated = true;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }
        }

        private static bool TryResolveShaderSlotsHandle(
            IDataVault vault,
            in VaultGenerationHandle<float4> handle,
            out NativeArray<float4> slots)
        {
            slots = default;
            return vault != null &&
                   IsShaderSlotsHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out slots) &&
                   slots.IsCreated &&
                   slots.Length >= RequiredShaderGlobalSlots;
        }

        private static bool IsShaderSlotsHandle(in VaultGenerationHandle<float4> handle)
        {
            return handle.BufferID == (uint)BufferID.ShaderGlobalState &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability;
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
                !vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
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
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
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

        private PreparedMockGlobalSlots BuildMockGlobalDataPayload(float survivalPressureWeight01, float shaderTime, float sectorPhase)
        {
            MockGlobalShaderDataKernel kernel = new MockGlobalShaderDataKernel
            {
                ShaderTime = shaderTime,
                SectorPhase = sectorPhase,
                SurvivalPressureWeight01 = math.saturate(survivalPressureWeight01),
                GeneratedEmergencyGlobals = _generatedEmergencyGlobals ? 1 : 0,
                ManualOverrideActive = s_manualOverrideActive ? 1 : 0,
#if UNITY_EDITOR
                CsvOverrideActive = s_csvOverrideActive ? 1 : 0,
#else
                CsvOverrideActive = 0,
#endif
                ManualFogColorDensity = s_manualFogColorDensity,
                ManualCausticFlow = s_manualCausticFlow,
#if UNITY_EDITOR
                CsvFogColorDensity = s_csvFogColorDensity,
                CsvCausticFlow = s_csvCausticFlow
#else
                CsvFogColorDensity = default,
                CsvCausticFlow = default
#endif
            };
            // Tiny shader-slot kernel runs inline to avoid a same-frame schedule/readback loop.
            kernel.ExecuteInline();
            return kernel.PreparedSlots;
        }

        private static void CopyMockGlobalDataSlots(NativeArray<float4> slots, in PreparedMockGlobalSlots preparedSlots)
        {
            if (!slots.IsCreated || slots.Length < RequiredShaderGlobalSlots)
                return;

            slots[ShaderGlobalsDtoSlot] = preparedSlots.FogColorDensity;
            slots[ShaderGlobalsDtoSlot + 1] = preparedSlots.FlowVectorMagnitude;
            slots[ShaderGlobalsDtoSlot + 2] = preparedSlots.Time;
            slots[MockWeatherSlot] = preparedSlots.MockWeather;
            slots[AmbientSlot] = preparedSlots.Ambient;
            slots[CausticRuntimeSlot] = preparedSlots.CausticRuntime;
            slots[ExtinctionCoefficientsSlot] = preparedSlots.ExtinctionCoefficients;
        }

        private static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<ShaderGlobalsDTO>() == ShaderGlobalsDTO.SizeBytes &&
                   UnsafeUtility.SizeOf<MockWeatherState>() == MockWeatherState.SizeBytes &&
                   UnsafeUtility.SizeOf<UberNoirGlobalTuning>() == UberNoirGlobalTuning.SizeBytes &&
                   UnsafeUtility.SizeOf<PreparedPhysiologyVisualPayloads>() == PreparedPhysiologyVisualPayloads.SizeBytes &&
                   (PreparedPhysiologyVisualPayloads.SizeBytes & 7) == 0 &&
                   ShaderGlobalsDTO.SizeBytes == HectonShaderGlobalDataVaultBridge.ShaderGlobalsDtoSlotCount * UnsafeUtility.SizeOf<float4>() &&
                   ShaderGlobalsDtoSlot % 1 == 0 &&
                   (ShaderGlobalsDtoSlot * UnsafeUtility.SizeOf<float4>()) % 16 == 0 &&
                   MockWeatherSlot == HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotStart &&
                   HazardSlot == HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotStart + HectonShaderGlobalDataVaultBridge.DispatcherRuntimeSlotCount - 1 &&
                   HectonShaderGlobalDataVaultBridge.ValidateSharedSlotMap() &&
                   HectonShaderGlobalDataVaultBridge.PowerBrownoutSlot < TelemetrySlotStart &&
                   HectonShaderGlobalDataVaultBridge.RespawnDearLieSlot < TelemetrySlotStart &&
                   HectonShaderGlobalDataVaultBridge.SuitCrushDearLieSlot < TelemetrySlotStart &&
                   HectonShaderGlobalDataVaultBridge.RadiationMutationSlot < TelemetrySlotStart &&
                   TelemetrySlotStart + TelemetryCapacity <= RequiredShaderGlobalSlots;
        }

        private Vector4 UploadDynamicWakeBuffers(float survivalPressureWeight01)
        {
            survivalPressureWeight01 = math.saturate(survivalPressureWeight01);
            Vector4 fallbackParams = new Vector4(0f, survivalPressureWeight01, 0f, 0f);
            _lastWakeParams = fallbackParams;
            return _lastWakeParams;
        }

        private int BuildThermalPackedSnapshot(IDataVault vault, Span<float4> packedSlots, float shaderTime)
        {
            if (vault == null ||
                packedSlots.Length < ThermalAnomalyCapacity ||
                !vault.TryGetGenerationHandle<float3>(BufferID.SubmarineFluidExteriorThermalCenters, out VaultGenerationHandle<float3> centersHandle) ||
                !IsThermalSourceHandleOwned(in centersHandle, BufferID.SubmarineFluidExteriorThermalCenters) ||
                !vault.TryGetGenerationHandle<float>(BufferID.SubmarineFluidExteriorThermalTemperatures, out VaultGenerationHandle<float> temperaturesHandle) ||
                !IsThermalSourceHandleOwned(in temperaturesHandle, BufferID.SubmarineFluidExteriorThermalTemperatures) ||
                !vault.TryGetGenerationHandle<float>(BufferID.SubmarineFluidExteriorThermalLifetimes, out VaultGenerationHandle<float> lifetimesHandle) ||
                !IsThermalSourceHandleOwned(in lifetimesHandle, BufferID.SubmarineFluidExteriorThermalLifetimes))
            {
                return WriteMockThermalPackedSlot(packedSlots, shaderTime);
            }

            Span<float3> centerSnapshot = stackalloc float3[ThermalAnomalyCapacity];
            Span<float> temperatureSnapshot = stackalloc float[ThermalAnomalyCapacity];
            Span<float> lifetimeSnapshot = stackalloc float[ThermalAnomalyCapacity];
            int count = 0;
            bool copiedSnapshot = false;

            if (!vault.TryAcquireMutationGuard(ThermalSourceReadGuardMask))
                return WriteMockThermalPackedSlot(packedSlots, shaderTime);

            try
            {
                if (!vault.TryReadOnlyHandle(in centersHandle, out NativeArray<float3>.ReadOnly centers) ||
                    !vault.TryReadOnlyHandle(in temperaturesHandle, out NativeArray<float>.ReadOnly temperatures) ||
                    !vault.TryReadOnlyHandle(in lifetimesHandle, out NativeArray<float>.ReadOnly lifetimes) ||
                    !centers.IsCreated ||
                    !temperatures.IsCreated ||
                    !lifetimes.IsCreated)
                {
                    count = 0;
                }
                else
                {
                    count = math.min(ThermalAnomalyCapacity, math.min(centers.Length, math.min(temperatures.Length, lifetimes.Length)));
                    for (int i = 0; i < count; i++)
                    {
                        centerSnapshot[i] = centers[i];
                        temperatureSnapshot[i] = temperatures[i];
                        lifetimeSnapshot[i] = lifetimes[i];
                    }

                    copiedSnapshot = true;
                }
            }
            finally
            {
                vault.ReleaseMutationGuard(ThermalSourceReadGuardMask);
            }

            if (!copiedSnapshot)
                return WriteMockThermalPackedSlot(packedSlots, shaderTime);

            int active = 0;
            for (int i = 0; i < count; i++)
            {
                float lifetime = math.max(0f, lifetimeSnapshot[i]);
                float temperature = math.max(0f, temperatureSnapshot[i]);
                float intensity = lifetime > 0f ? math.saturate((temperature - 18f) * 0.02f) : 0f;
                float3 center = math.all(math.isfinite(centerSnapshot[i])) ? centerSnapshot[i] : float3.zero;
                packedSlots[i] = new float4(center, intensity);
                if (intensity > 0.001f)
                    active++;
            }

            for (int i = count; i < ThermalAnomalyCapacity; i++)
                packedSlots[i] = default;

            return active;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsThermalSourceHandleOwned<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics;
        }

        private static int WriteMockThermalPackedSlot(Span<float4> packedSlots, float shaderTime)
        {
            if (packedSlots.Length < ThermalAnomalyCapacity)
                return 0;

            packedSlots[0] = new float4(
                MathLodApproximation.ApproxSinBhaskara(shaderTime * 0.31f) * 12f,
                -6f,
                MathLodApproximation.ApproxCosBhaskara(shaderTime * 0.27f) * 12f,
                0.22f);
            for (int i = 1; i < ThermalAnomalyCapacity; i++)
                packedSlots[i] = default;
            return 1;
        }

        private static void CopyThermalPackedSlots(NativeArray<float4> slots, ReadOnlySpan<float4> packedSlots)
        {
            if (!slots.IsCreated || packedSlots.Length < ThermalAnomalyCapacity)
                return;

            for (int i = 0; i < ThermalAnomalyCapacity; i++)
                slots[ThermalPackedSlotStart + i] = packedSlots[i];
        }

        private Vector4 UploadThermalBuffer(ReadOnlySpan<float4> packedSlots, int thermalCount)
        {
            if (!EnsureGpuBuffers() || packedSlots.Length < ThermalAnomalyCapacity)
            {
                _lastThermalParams = Vector4.zero;
                return _lastThermalParams;
            }

            int uploadCount = math.min(ThermalAnomalyCapacity, math.max(1, thermalCount));
            UploadFloat4Range(_thermalAnomalyBuffer, packedSlots, uploadCount);
            _lastThermalParams = new Vector4(uploadCount, thermalCount, thermalCount > 0 ? 1f : 0f, 0f);
            return _lastThermalParams;
        }

        private static void UploadFloat4Range(GraphicsBuffer destination, ReadOnlySpan<float4> source, int count)
        {
            if (destination == null || !destination.IsValid() || source.Length <= 0 || count <= 0)
                return;

            NativeArray<float4> target = destination.LockBufferForWrite<float4>(0, count);
            try
            {
                int safeCount = math.min(count, math.min(source.Length, target.Length));
                for (int i = 0; i < safeCount; i++)
                    target[i] = source[i];
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
            Vector4 suitCrushDearLie,
            Vector4 radiationMutation,
            Vector4 physiologyDecompression,
            Vector4 physiologyGasToxicity,
            float uberNoirFeatureMask,
            float globalQualityWeight01,
            float survivalPressureWeight01)
        {
            Vector4 fogColor = ToVector4(dto.FogColor);
            Vector4 flow = new Vector4(dto.FlowVector.x, dto.FlowVector.y, dto.FlowVector.z, dto.FlowMagnitude);

            Shader.SetGlobalVector(_FogColorId, fogColor);
            Shader.SetGlobalFloat(_FogDensityId, fogColor.w);
            Shader.SetGlobalVector(_AmbientLightId, ambient);
            Shader.SetGlobalVector(_GlobalFlowVectorId, flow);
            Shader.SetGlobalVector(_H8GlobalFlowId, flow);
            Shader.SetGlobalFloat(_H8ShaderTimeId, dto.GlobalTime);
            Shader.SetGlobalVector(_WorldOriginOffsetId, aupOffset);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, aupOffset);
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, aupOffset);
            Shader.SetGlobalVector(_AupShiftOffsetId, aupShiftOffset);
            Shader.SetGlobalFloat(_AupJitterMaskId, aupJitterMask);
            Shader.SetGlobalFloat(_ResolutionScaleId, resolution.x);
            Shader.SetGlobalVector(_ResolutionScaleParamsId, resolution);
            Shader.SetGlobalFloat(_HazardPulseIntensityId, hazard.x);
            Shader.SetGlobalVector(_HazardPulseParamsId, hazard);
            Shader.SetGlobalVector(_ExtinctionCoefficientsId, extinction);
            Shader.SetGlobalVector(_ExtinctionLutParamsId, extinctionLutParams);
            Shader.SetGlobalVector(_ExtinctionLutRuntimeId, extinctionLutRuntime);
            Shader.SetGlobalVector(_ExtinctionLutWeatherParamsId, extinctionLutWeather);
            Shader.SetGlobalVector(_BiomePaletteId, biomePalette);
            Shader.SetGlobalVector(_HardwareTierParamsId, new Vector4(
                math.saturate(globalQualityWeight01),
                math.saturate(survivalPressureWeight01),
                _generatedEmergencyGlobals ? 1f : 0f,
                _activeKeywordCount));
            Shader.SetGlobalVector(_BiolumMasterPhaseId, biolumMasterPhase);
            Shader.SetGlobalVector(_HectonUberNoirRuntimeParamsId, uberNoirRuntime);
            Shader.SetGlobalFloat(_HectonActiveShaderFeatureMaskId, uberNoirFeatureMask);
            Shader.SetGlobalVector(_HectonPowerBrownoutParamsId, powerBrownout);
            Shader.SetGlobalVector(_HectonRespawnDearLieParamsId, respawnDearLie);
            Shader.SetGlobalFloat(_HectonDeathFadeIntensityId, math.saturate(respawnDearLie.x));
            Shader.SetGlobalVector(_HectonSuitCrushDearLieParamsId, suitCrushDearLie);
            Shader.SetGlobalFloat(_HectonSuitCrushBucklingId, math.saturate(suitCrushDearLie.x));
            Shader.SetGlobalVector(_HectonRadiationMutationParamsId, radiationMutation);
            Shader.SetGlobalFloat(_HectonHandRadiationMutation01Id, math.saturate(radiationMutation.x));
            Shader.SetGlobalVector(_HectonDcsPhysiologyParamsId, physiologyDecompression);
            Shader.SetGlobalFloat(_HectonSupersaturationScalarId, math.saturate(physiologyDecompression.x));
            Shader.SetGlobalFloat(_HectonNarcosisScalarId, math.saturate(physiologyDecompression.y));
            Shader.SetGlobalVector(_HectonGasToxicityParamsId, physiologyGasToxicity);
            Shader.SetGlobalFloat(_HypoxiaSignalId, math.saturate(physiologyGasToxicity.x));
            Shader.SetGlobalVector(_DynamicWakeParamsId, wakeParams);
            Shader.SetGlobalVector(_ThermalAnomalyParamsId, thermalParams);

            GraphicsBuffer wakeBuffer = _emptyFloat4Buffer;
            GraphicsBuffer wakeVectorBuffer = _emptyFloat4Buffer;
            if (wakeBuffer != null && wakeBuffer.IsValid())
                Shader.SetGlobalBuffer(_DynamicWakesId, wakeBuffer);
            if (wakeVectorBuffer != null && wakeVectorBuffer.IsValid())
                Shader.SetGlobalBuffer(_DynamicWakeVectorsId, wakeVectorBuffer);
            if (_thermalAnomalyBuffer != null && _thermalAnomalyBuffer.IsValid())
                Shader.SetGlobalBuffer(_ThermalAnomaliesId, _thermalAnomalyBuffer);

            Texture extinctionTexture = LutArrayResolver.ExtinctionTexture;
            if (extinctionTexture != null)
            {
                Shader.SetGlobalTexture(_OpticalExtinctionLutId, extinctionTexture);
                Shader.SetGlobalTexture(_ExtinctionLutId, extinctionTexture);
            }

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
                float fallbackOverkill = ResolveVisualOverkillWeight01(quality);
                float overkill = math.saturate(math.isfinite(state.VisualOverkill01) ? state.VisualOverkill01 : fallbackOverkill);
                return new Vector4(current > 0f ? current : 1f, target > 0f ? target : current, stress, overkill);
            }

            float fallbackStress = math.saturate(HomeostasisBrain.SystemHealthIndex01);
            float quality01 = ResolveGlobalQualityWeight01();
            float overkill01 = ResolveVisualOverkillWeight01(quality01);
            return new Vector4(1f, 1f, fallbackStress, overkill01);
        }

        private static float ResolveVisualOverkillWeight01(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 0f);
            float cubicBias = quality * quality * math.lerp(0.5f, 1f, quality);
            return Smooth01(cubicBias);
        }

        private float ResolveGlobalQualityWeight01()
        {
            IResolutionScalerService scaler = _resolutionScaler;
            if (scaler != null && scaler.TryGetScaleState(out ResolutionScaleState state))
                return math.saturate(math.isfinite(state.GlobalQualityWeight01) ? state.GlobalQualityWeight01 : 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveSurvivalPressureWeight01(float qualityWeight01, float survivalPressureFloor01)
        {
            survivalPressureFloor01 = math.saturate(survivalPressureFloor01);
            float fallbackQuality = math.lerp(1f, 0.35f, survivalPressureFloor01);
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : fallbackQuality);
            float weight = 1f - Smooth01(math.saturate((quality - 0.18f) * 1.2195122f));
            return math.max(weight, survivalPressureFloor01);
        }

        private static float ResolveSurvivalPressureFloor01(float qualityWeight01)
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
                ? exposure * (0.55f + (0.45f * MathLodApproximation.ApproxSinBhaskara(shaderTime * 6.2831853f * 0.72f)))
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
                float signalExposure = ResolveRadiationSignalExposure01(in signal);
                exposure = math.max(exposure, signalExposure);
            }

            return exposure;
        }

        private static float ResolveRadiationSignalExposure01(in RadiationDoseSignal signal)
        {
            float intensity01 = math.saturate(math.select(0f, signal.Intensity01, math.isfinite(signal.Intensity01)));
            float dose01 = RadiationDoseSignal.DoseToUnit01(signal.Dose);
            return math.max(intensity01, dose01);
        }

        private float ResolveSectorPhase()
        {
            double3 offset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            int sx = (int)Math.Floor(offset.x / 256.0);
            int sz = (int)Math.Floor(offset.z / 256.0);
            uint hash = (uint)(sx * 73856093) ^ (uint)(sz * 19349663) ^ 0x9E3779B9u;
            return (hash & 1023u) * (6.2831853f / 1024f);
        }

        private void RefreshQualityTelemetry(float globalQualityWeight01, float survivalPressureWeight01)
        {
            byte qualityByte = EncodeQualityWeightByte(globalQualityWeight01);
            int survivalPressureBucket = (int)math.round(math.saturate(survivalPressureWeight01) * 255f);
            if (_lastGlobalQualityByte == qualityByte && _lastSurvivalPressureBucket == survivalPressureBucket)
                return;

            _lastGlobalQualityByte = qualityByte;
            _lastSurvivalPressureBucket = survivalPressureBucket;
            _activeKeywordCount = 0;
        }

        private static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            return (byte)math.round(quality * 255f);
        }

        private void RecordTelemetry(IDataVault vault, float dispatchMicroseconds, uint keywordCount, uint flags)
        {
            if (!TryResolveShaderGlobalSlotsRuntime(out IDataVault currentVault))
                return;

            if (!ReferenceEquals(vault, currentVault))
                vault = currentVault;

            int cursor = _telemetryCursor;
            if ((uint)cursor >= TelemetryCapacity)
                cursor = 0;
            int nextCursor = cursor + 1;
            if (nextCursor >= TelemetryCapacity)
                nextCursor = 0;
            int slot = TelemetrySlotStart + cursor;
            uint frame = unchecked(_dispatchTelemetryFrame + 1u);
            if (frame == 0u)
                frame = 1u;
            float4 telemetryEntry = new float4(frame, dispatchMicroseconds, keywordCount, flags);

            if (vault == null || !vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
                return;

            try
            {
                if (!TryResolveShaderGlobalSlotsLocked(vault, out NativeArray<float4> slots))
                    return;

                if (!slots.IsCreated || slots.Length < TelemetrySlotStart + TelemetryCapacity)
                    return;

                slots[slot] = telemetryEntry;
                _dispatchTelemetryFrame = frame;
                _telemetryCursor = nextCursor;
            }
            finally
            {
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            if (_dumpedOverBudget && reasonFlags == 2u)
                return;

            _dumpedOverBudget = true;

            Span<float4> telemetrySnapshot = stackalloc float4[TelemetryCapacity];
            telemetrySnapshot.Clear();
            int telemetryCursor = _telemetryCursor;
            bool copiedTelemetry = TryCopyCachedShaderGlobalSlots(TelemetrySlotStart, telemetrySnapshot);
            if (copiedTelemetry)
            {
                telemetryCursor = _telemetryCursor;
            }

            if (!copiedTelemetry)
                reasonFlags |= TelemetryFlagVaultUnavailable;

            WriteTelemetryDump(telemetrySnapshot, telemetryCursor, reasonFlags);
        }

        private static unsafe void WriteTelemetryDump(
            ReadOnlySpan<float4> telemetrySnapshot,
            int telemetryCursor,
            uint reasonFlags)
        {
            int count = math.min(telemetrySnapshot.Length, TelemetryCapacity);
            if (count <= 0)
                return;

            int byteCount = TelemetryDumpHeaderBytes + count * TelemetryDumpEntryBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(GlobalShaderDispatcher),
                "GlobalShaderDispatcherTelemetryDumpPayload");
            try
            {
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, TelemetryDumpMagic);
                WriteUInt32LittleEndian(target, 4, TelemetryDumpVersion);
                WriteUInt32LittleEndian(target, 8, reasonFlags);
                WriteInt32LittleEndian(target, 12, telemetryCursor);
                WriteInt32LittleEndian(target, 16, count);
                WriteInt32LittleEndian(target, 20, TelemetryDumpEntryBytes);
                WriteUInt32LittleEndian(target, 24, (uint)RequiredShaderGlobalSlots);
                WriteUInt32LittleEndian(target, 28, 0u);

                int cursor = TelemetryDumpHeaderBytes;
                for (int i = 0; i < count; i++)
                    WriteFloat4LittleEndian(target, ref cursor, telemetrySnapshot[i]);

                NativeFaultDumpWriter.TryWriteAll(TelemetryDumpPath, payload, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(GlobalShaderDispatcher),
                    "GlobalShaderDispatcherTelemetryDumpPayload");
            }
        }

        private static unsafe void WriteFloat4LittleEndian(byte* destination, ref int cursor, float4 value)
        {
            WriteFloatLittleEndian(destination, ref cursor, value.x);
            WriteFloatLittleEndian(destination, ref cursor, value.y);
            WriteFloatLittleEndian(destination, ref cursor, value.z);
            WriteFloatLittleEndian(destination, ref cursor, value.w);
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, cursor, math.asuint(value));
            cursor += 4;
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

#if UNITY_EDITOR
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
                Hecton8.Core.H8Debug.LogWarning("[GlobalShaderDispatcher] CSV override parse failed: " + exception.Message);
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
                    result *= Pow10Int(exponent * exponentSign);
            }

            value = math.isfinite((float)result) ? (float)result : 0f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Pow10Int(int exponent)
        {
            int clamped = math.clamp(exponent, -38, 38);
            double scale = 1d;
            int magnitude = math.abs(clamped);
            for (int i = 0; i < magnitude; i++)
                scale *= 10d;
            return clamped < 0 ? 1d / scale : scale;
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
#endif

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

        private static PreparedPhysiologyVisualPayloads PreparePhysiologyVisualPayloads(float globalQualityWeight01)
        {
            PreparedPhysiologyVisualPayloads prepared = default;
            float quality = math.saturate(math.isfinite(globalQualityWeight01) ? globalQualityWeight01 : 1f);
            uint currentFrame = SystemDispatcher.CurrentFrameId;

            prepared.Quality = quality;
            prepared.CurrentFrame = currentFrame;
            prepared.LastDecompressionFrame = s_lastDecompressionVisualSignalFrame;
            prepared.LastGasFrame = s_lastGasToxicityVisualSignalFrame;

            ReadOnlySpan<PhysiologyStateSignal> signals = SignalBus<PhysiologyStateSignal>.GetFrameSnapshot();
            if (signals.Length > 0)
            {
                for (int i = 0; i < signals.Length; i++)
                {
                    PhysiologyStateSignal signal = signals[i];
                    ApplyPhysiologyVisualSignal(in signal, ref prepared);
                }
            }

            return prepared;
        }

        private static void ResolvePhysiologyVisualPayloads(
            NativeArray<float4> slots,
            in PreparedPhysiologyVisualPayloads prepared,
            out Vector4 decompression,
            out Vector4 gasToxicity)
        {
            float4 decompressionPayload = slots[HectonShaderGlobalDataVaultBridge.PhysiologyDecompressionSlot];
            float4 gasPayload = slots[HectonShaderGlobalDataVaultBridge.PhysiologyGasToxicitySlot];

            SanitizePhysiologyVisualPayloads(ref decompressionPayload, ref gasPayload, prepared.Quality);
            ApplyPreparedPhysiologyVisuals(in prepared, ref decompressionPayload, ref gasPayload);
            ClearExpiredPhysiologyVisuals(in prepared, ref decompressionPayload, ref gasPayload);

            decompressionPayload.w = prepared.Quality;
            gasPayload.w = prepared.Quality;
            slots[HectonShaderGlobalDataVaultBridge.PhysiologyDecompressionSlot] = decompressionPayload;
            slots[HectonShaderGlobalDataVaultBridge.PhysiologyGasToxicitySlot] = gasPayload;
            decompression = ToVector4(decompressionPayload);
            gasToxicity = ToVector4(gasPayload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyPhysiologyVisualSignal(
            in PhysiologyStateSignal signal,
            ref PreparedPhysiologyVisualPayloads prepared)
        {
            if (!IsFreshPhysiologyVisualSignal(in signal, prepared.CurrentFrame))
                return;

            if (signal.SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology &&
                signal.Cause == PhysiologyStateSignal.CauseDecompression)
            {
                prepared.DecompressionSignal = new float4(
                    math.saturate(signal.Supersaturation01),
                    math.saturate(signal.Narcosis01),
                    math.max(0f, math.isfinite(signal.AmbientPressureAtm) ? signal.AmbientPressureAtm : 0f),
                    prepared.Quality);
                prepared.LastDecompressionFrame = signal.Frame;
                prepared.HasDecompressionSignal = 1;
            }
            else if (signal.SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology &&
                     signal.Cause == PhysiologyStateSignal.CauseGasToxicity)
            {
                prepared.GasSignal = new float4(
                    math.saturate(signal.Supersaturation01),
                    signal.GasCnsSeverity * (1f / 255f),
                    signal.GasCarbonDioxideSeverity * (1f / 255f),
                    prepared.Quality);
                prepared.LastGasFrame = signal.Frame;
                prepared.HasGasSignal = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFreshPhysiologyVisualSignal(in PhysiologyStateSignal signal, uint currentFrame)
        {
            if (signal.SourceHash != PhysiologyStateSignal.SourceShinobuPhysiology)
                return false;

            uint signalFrame = signal.Frame;
            if (currentFrame < signalFrame)
                return false;

            return (currentFrame - signalFrame) <= PhysiologyVisualHoldFrames;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyPreparedPhysiologyVisuals(
            in PreparedPhysiologyVisualPayloads prepared,
            ref float4 decompressionPayload,
            ref float4 gasPayload)
        {
            if (prepared.HasDecompressionSignal != 0)
            {
                decompressionPayload = prepared.DecompressionSignal;
                s_lastDecompressionVisualSignalFrame = prepared.LastDecompressionFrame;
            }

            if (prepared.HasGasSignal != 0)
            {
                gasPayload = prepared.GasSignal;
                s_lastGasToxicityVisualSignalFrame = prepared.LastGasFrame;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClearExpiredPhysiologyVisuals(
            in PreparedPhysiologyVisualPayloads prepared,
            ref float4 decompressionPayload,
            ref float4 gasPayload)
        {
            if (IsPhysiologyVisualExpired(prepared.LastDecompressionFrame, prepared.CurrentFrame))
            {
                decompressionPayload.x = 0f;
                decompressionPayload.y = 0f;
                decompressionPayload.z = 0f;
            }

            if (IsPhysiologyVisualExpired(prepared.LastGasFrame, prepared.CurrentFrame))
            {
                gasPayload.x = 0f;
                gasPayload.y = 0f;
                gasPayload.z = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPhysiologyVisualExpired(uint lastSignalFrame, uint currentFrame)
        {
            if (currentFrame < lastSignalFrame)
                return true;

            return (currentFrame - lastSignalFrame) > PhysiologyVisualHoldFrames;
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

        private struct PreparedMockGlobalSlots
        {
            public float4 FogColorDensity;
            public float4 FlowVectorMagnitude;
            public float4 Time;
            public float4 MockWeather;
            public float4 Ambient;
            public float4 CausticRuntime;
            public float4 ExtinctionCoefficients;
        }

        [StructLayout(LayoutKind.Sequential, Size = 56)]
        private struct PreparedPhysiologyVisualPayloads
        {
            public const int SizeBytes = 56;

            public float4 DecompressionSignal;
            public float4 GasSignal;
            public float Quality;
            public uint CurrentFrame;
            public uint LastDecompressionFrame;
            public uint LastGasFrame;
            public byte HasDecompressionSignal;
            public byte HasGasSignal;
        }

        private ref struct MockGlobalShaderDataKernel
        {
            public PreparedMockGlobalSlots PreparedSlots;
            public float ShaderTime;
            public float SectorPhase;
            public float SurvivalPressureWeight01;
            public int GeneratedEmergencyGlobals;
            public int ManualOverrideActive;
            public int CsvOverrideActive;
            public float4 ManualFogColorDensity;
            public float4 ManualCausticFlow;
            public float4 CsvFogColorDensity;
            public float4 CsvCausticFlow;

            public void ExecuteInline()
            {
                float storm = 0.5f + (0.5f * MathLodApproximation.ApproxSinBhaskara((ShaderTime * 0.071f) + SectorPhase));
                float turbidity = math.saturate(0.28f + (storm * 0.44f));
                float heat = 0.5f + (0.5f * MathLodApproximation.ApproxSinBhaskara((ShaderTime * 0.119f) + 1.7f + SectorPhase));
                float biome = 0.5f - (0.5f * MathLodApproximation.ApproxCosBhaskara(math.fmod(ShaderTime, 5f) * 1.2566371f));

                float4 fogA = new float4(0.012f, 0.045f, 0.066f, 0.018f + (0.018f * turbidity));
                float4 fogB = new float4(0.052f, 0.018f, 0.035f, 0.028f + (0.012f * heat));
                float smoothBiome = biome * biome * (3f - (2f * biome));
                float4 fogColorDensity = math.lerp(fogA, fogB, smoothBiome);
                float causticSpeed = 0.16f + (0.24f * (1f - storm));
                float3 flowVector = new float3(
                    MathLodApproximation.ApproxSinBhaskara((ShaderTime * 0.037f) + SectorPhase),
                    0.04f * MathLodApproximation.ApproxSinBhaskara(ShaderTime * 0.021f),
                    MathLodApproximation.ApproxCosBhaskara((ShaderTime * 0.041f) + SectorPhase));
                float flowLengthSq = math.lengthsq(flowVector);
                flowVector = math.isfinite(flowLengthSq) && flowLengthSq > 0.0001f
                    ? flowVector * math.rsqrt(math.max(flowLengthSq, 0.0001f))
                    : new float3(1f, 0f, 0f);
                float survivalPressureWeight = math.saturate(SurvivalPressureWeight01);
                float survivalFlowMagnitude = 0.32f + (0.18f * storm);
                float overkillFlowMagnitude = 0.78f + (0.38f * storm);
                float flowMagnitude = math.lerp(overkillFlowMagnitude, survivalFlowMagnitude, survivalPressureWeight);

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

                PreparedSlots.FogColorDensity = fogColorDensity;
                PreparedSlots.FlowVectorMagnitude = new float4(flowVector, flowMagnitude);
                PreparedSlots.Time = new float4(ShaderTime, 0f, 0f, 0f);
                PreparedSlots.MockWeather = new float4(storm, turbidity, heat, smoothBiome);
                PreparedSlots.Ambient = new float4(fogColorDensity.xyz * (0.55f + (0.25f * (1f - storm))), fogColorDensity.w);
                PreparedSlots.CausticRuntime = new float4(causticSpeed, math.lerp(0.75f * (1f - storm), 0.12f, survivalPressureWeight), smoothBiome, storm);
                PreparedSlots.ExtinctionCoefficients = new float4(0.624f, 0.0434f * (1f + turbidity), 0.0106f * (1f + (turbidity * 0.5f)), fogColorDensity.w);
            }
        }
    }
}
