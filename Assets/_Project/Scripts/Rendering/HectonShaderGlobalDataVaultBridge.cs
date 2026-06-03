using System.Runtime.CompilerServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// DataVault-backed bridge for frame shader globals shared by the noir material path.
    /// </summary>
    public static class HectonShaderGlobalDataVaultBridge
    {
        internal const int BiolumMasterPhaseSlot = 0;
        internal const int AupShiftOffsetSlot = 1;
        internal const int WaterExtinctionRuntimeSlot = 2;
        internal const int WaterExtinctionWeatherSlot = 3;
        internal const int WaterExtinctionParamsSlot = 4;
        internal const int UberNoirRuntimeSlot = 5;
        internal const int UberNoirFeatureMaskSlot = 6;
        internal const int PhysiologyDecompressionSlot = 7;
        internal const int ShaderGlobalsDtoSlot = 8;
        internal const int ShaderGlobalsDtoSlotCount = 3;
        internal const int PhysiologyGasToxicitySlot = 11;
        internal const int DispatcherRuntimeSlotStart = 12;
        internal const int DispatcherRuntimeSlotCount = 7;
        internal const int RespawnDearLieSlot = 19;
        internal const int PowerBrownoutSlot = 20;
        internal const int SuitCrushDearLieSlot = 21;
        internal const int RadiationMutationSlot = 22;
        internal const int ThermalPackedSlotStart = 32;
        internal const int ThermalPackedSlotCount = 8;
        internal const int TelemetrySlotStart = 64;
        internal const int TelemetrySlotCount = 300;
        // Shared with GlobalShaderDispatcher: slots 64-363 are the 300-frame CBuffer blackbox.
        internal const int SlotCount = 512;

        private static readonly bool s_slotMapValid = ValidateSharedSlotMap();
        private const ulong ShaderGlobalStateMutationGuardMask =
            1UL << ((int)BufferID.ShaderGlobalState & 31);

        private static readonly int _BiolumMasterPhaseId = Shader.PropertyToID("_BiolumMasterPhase");
        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _TotalUniverseOffsetId = Shader.PropertyToID("_TotalUniverseOffset");
        private static readonly int _AupShiftOffsetId = Shader.PropertyToID("_AupShiftOffset");
        private static readonly int _AupJitterMaskId = Shader.PropertyToID("_AupJitterMask");
        private static readonly int _ExtinctionLutParamsId = Shader.PropertyToID("_ExtinctionLUTParams");
        private static readonly int _ExtinctionLutRuntimeId = Shader.PropertyToID("_ExtinctionLUTRuntime");
        private static readonly int _ExtinctionLutWeatherParamsId = Shader.PropertyToID("_ExtinctionLUTWeatherParams");
        private static readonly int _HectonUberNoirRuntimeParamsId = Shader.PropertyToID("_HectonUberNoirRuntimeParams");
        private static readonly int _HectonActiveShaderFeatureMaskId = Shader.PropertyToID("_HectonActiveShaderFeatureMask");
        private static readonly int _HectonDcsPhysiologyParamsId = Shader.PropertyToID("_HectonDcsPhysiologyParams");
        private static readonly int _HectonGasToxicityParamsId = Shader.PropertyToID("_HectonGasToxicityParams");
        private static readonly int _HectonSupersaturationScalarId = Shader.PropertyToID("_HectonSupersaturationScalar");
        private static readonly int _HectonNarcosisScalarId = Shader.PropertyToID("_HectonNarcosisScalar");
        private static readonly int _HypoxiaSignalId = Shader.PropertyToID("_HypoxiaSignal");
        private static readonly int _HectonPowerBrownoutParamsId = Shader.PropertyToID("_HectonPowerBrownoutParams");
        private static readonly int _HectonRespawnDearLieParamsId = Shader.PropertyToID("_HectonRespawnDearLieParams");
        private static readonly int _HectonDeathFadeIntensityId = Shader.PropertyToID("_HectonDeathFadeIntensity");
        private static readonly int _HectonSuitCrushDearLieParamsId = Shader.PropertyToID("_HectonSuitCrushDearLieParams");
        private static readonly int _HectonSuitCrushBucklingId = Shader.PropertyToID("_HectonSuitCrushBuckling");
        private static readonly int _HectonRadiationMutationParamsId = Shader.PropertyToID("_HectonRadiationMutationParams");
        private static readonly int _HectonHandRadiationMutation01Id = Shader.PropertyToID("_HectonHandRadiationMutation01");

        private static IDataVault _cachedVault;
        private static VaultGenerationHandle<float4> _slotsHandle;
        private static bool _slotsValidated;
        private static float4 _fallbackBiolumMasterPhase;
        private static float4 _fallbackAupTotalUniverseOffset;
        private static float4 _fallbackAupShiftOffset;
        private static float4 _fallbackWaterExtinctionRuntime;
        private static float4 _fallbackWaterExtinctionWeather;
        private static float4 _fallbackWaterExtinctionParams;
        private static float4 _fallbackUberNoirRuntime;
        private static float4 _fallbackUberNoirFeatureMask;
        private static float4 _fallbackPhysiologyDecompression;
        private static float4 _fallbackPhysiologyGasToxicity;
        private static float4 _fallbackPowerBrownout;
        private static float4 _fallbackRespawnDearLie;
        private static float4 _fallbackSuitCrushDearLie;
        private static float4 _fallbackRadiationMutation;
        private static bool _visualSyncDispatcherActive;
        private static bool _fallbackShaderGlobalsDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedVault = null;
            _slotsHandle = default;
            _slotsValidated = false;
            _fallbackBiolumMasterPhase = default;
            _fallbackAupTotalUniverseOffset = default;
            _fallbackAupShiftOffset = default;
            _fallbackWaterExtinctionRuntime = CreateFloat4(0f, 1f, 1f, 0f);
            _fallbackWaterExtinctionWeather = default;
            _fallbackWaterExtinctionParams = CreateFloat4(1500f, 2.5f, 0f, 0f);
            _fallbackUberNoirRuntime = CreateFloat4(0f, 1f, 0f, 0f);
            _fallbackUberNoirFeatureMask = default;
            _fallbackPhysiologyDecompression = default;
            _fallbackPhysiologyGasToxicity = default;
            _fallbackPowerBrownout = CreateFloat4(1f, 0f, 0f, 0f);
            _fallbackRespawnDearLie = default;
            _fallbackSuitCrushDearLie = default;
            _fallbackRadiationMutation = default;
            _visualSyncDispatcherActive = false;
            _fallbackShaderGlobalsDirty = false;
        }

        internal static void SetVisualSyncDispatcherActive(bool active)
        {
            if (_visualSyncDispatcherActive == active)
                return;

            _visualSyncDispatcherActive = active;
            if (!active)
                MarkFallbackShaderGlobalsDirty();
        }

        public static void PublishBiolumMasterPhase(Vector4 phaseVector)
        {
            _ = WriteReadSlot(
                BiolumMasterPhaseSlot,
                ToFiniteFloat4(phaseVector),
                ref _fallbackBiolumMasterPhase);
            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        public static void PublishAupShaderGlobals(Vector4 totalUniverseOffset, Vector4 aupShiftOffset, float aupJitterMask)
        {
            float4 packedShift = ToFiniteFloat4(aupShiftOffset);
            packedShift.w = math.saturate(aupJitterMask);
            _ = WriteReadSlot(
                AupShiftOffsetSlot,
                packedShift,
                ref _fallbackAupShiftOffset);
            _fallbackAupTotalUniverseOffset = ToFiniteFloat4(totalUniverseOffset);
            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        public static void ResetAupShaderGlobals()
        {
            PublishAupShaderGlobals(Vector4.zero, Vector4.zero, 0f);
        }

        /// <summary>
        /// Publishes water-extinction immutable LUT dimensions and active state through the DataVault-backed shader-global lane.
        /// </summary>
        /// <param name="paramsVector">x=max depth meters, y=max turbidity, z=strength, w=active.</param>
        public static void PublishWaterExtinctionParams(Vector4 paramsVector)
        {
            float4 value = ToFiniteFloat4(paramsVector);
            value.x = math.max(0.001f, value.x);
            value.y = math.max(0.001f, value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                WaterExtinctionParamsSlot,
                value,
                ref _fallbackWaterExtinctionParams);
            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes water-extinction runtime state through the DataVault-backed shader-global lane.
        /// </summary>
        /// <param name="runtimeVector">x=sea surface y, y=turbidity, z=post blend, w=active.</param>
        public static void PublishWaterExtinctionRuntime(Vector4 runtimeVector)
        {
            float4 value = ToFiniteFloat4(runtimeVector);
            value.y = math.max(0f, value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                WaterExtinctionRuntimeSlot,
                value,
                ref _fallbackWaterExtinctionRuntime);
            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes weather-driven water-extinction state through the DataVault-backed shader-global lane.
        /// </summary>
        /// <param name="weatherVector">x=turbidity shift, y=weather intensity, z/w reserved.</param>
        public static void PublishWaterExtinctionWeather(Vector4 weatherVector)
        {
            float4 value = ToFiniteFloat4(weatherVector);
            value.x = math.max(0f, value.x);
            value.y = math.saturate(value.y);
            _ = WriteReadSlot(
                WaterExtinctionWeatherSlot,
                value,
                ref _fallbackWaterExtinctionWeather);
            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Resets water-extinction shader globals to a disabled deterministic fallback.
        /// </summary>
        public static void ResetWaterExtinctionGlobals()
        {
            PublishWaterExtinctionParams(CreateVector4(1500f, 2.5f, 0f, 0f));
            PublishWaterExtinctionRuntime(CreateVector4(0f, 1f, 1f, 0f));
            PublishWaterExtinctionWeather(Vector4.zero);
        }

        /// <summary>
        /// Publishes a deterministic analytical Beer-Lambert fallback while keeping LUT texture sampling disabled.
        /// </summary>
        public static void PublishWaterExtinctionAnalyticalFallback()
        {
            PublishWaterExtinctionParams(CreateVector4(1500f, 2.5f, 1f, 0f));
            PublishWaterExtinctionRuntime(CreateVector4(0f, 1f, 1f, 1f));
            PublishWaterExtinctionWeather(Vector4.zero);
        }

        /// <summary>
        /// Publishes UberNoir runtime feature state through the shared DataVault-backed shader-global lane.
        /// </summary>
        /// <param name="runtimeVector">x=system stress, y=high-cost allowed, z=feature mask, w=visual overkill.</param>
        /// <param name="featureMask">Feature mask mirrored as a scalar for shaders that cannot safely unpack vector state.</param>
        public static void PublishUberNoirRuntime(Vector4 runtimeVector, float featureMask)
        {
            float4 value = ToFiniteFloat4(runtimeVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.clamp(value.z, 0f, 16777215f);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                UberNoirRuntimeSlot,
                value,
                ref _fallbackUberNoirRuntime);

            float safeFeatureMask = math.clamp(
                math.isfinite(featureMask) ? featureMask : 0f,
                0f,
                16777215f);
            _ = WriteReadSlot(
                UberNoirFeatureMaskSlot,
                CreateFloat4(safeFeatureMask, 0f, 0f, 0f),
                ref _fallbackUberNoirFeatureMask);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes scalar physiology discomfort for visor/audio shader fakes; x=supersaturation, y=narcosis, z=ambient atm, w=quality.
        /// </summary>
        public static void PublishPhysiologyDecompression(Vector4 physiologyVector)
        {
            float4 value = ToFiniteFloat4(physiologyVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.max(0f, value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                PhysiologyDecompressionSlot,
                value,
                ref _fallbackPhysiologyDecompression);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes gas-toxicity presentation scalars; x=hypoxia tunnel, y=CNS O2, z=CO2 toxicity, w=quality.
        /// </summary>
        public static void PublishPhysiologyGasToxicity(Vector4 gasVector)
        {
            float4 value = ToFiniteFloat4(gasVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                PhysiologyGasToxicitySlot,
                value,
                ref _fallbackPhysiologyGasToxicity);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes base-grid brownout state once per telemetry tick; x=supply, y=severity, z=phase seconds, w=GlobalQualityWeight.
        /// </summary>
        public static void PublishPowerBrownout(Vector4 brownoutVector)
        {
            float4 value = ToFiniteFloat4(brownoutVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.max(0f, value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                PowerBrownoutSlot,
                value,
                ref _fallbackPowerBrownout);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes the player-death visual cover: x=fade, y=chromatic, z=grain, w=GlobalQualityWeight.
        /// </summary>
        public static void PublishRespawnDearLie(Vector4 dearLieVector)
        {
            PublishRespawnDearLie(AcquireCachedSlotsVaultNoAllocate(), dearLieVector);
        }

        /// <summary>
        /// Publishes the player-death visual cover through a caller-cached Vault route.
        /// </summary>
        public static void PublishRespawnDearLie(IDataVault vault, Vector4 dearLieVector)
        {
            float4 value = ToFiniteFloat4(dearLieVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                vault,
                RespawnDearLieSlot,
                value,
                ref _fallbackRespawnDearLie,
                allowAllocation: false);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes suit/hull pressure presentation scalars; x=buckling, y=overpressure, z=integrity loss, w=GlobalQualityWeight.
        /// </summary>
        public static void PublishSuitCrushDearLie(Vector4 crushVector)
        {
            float4 value = ToFiniteFloat4(crushVector);
            value.x = math.saturate(value.x);
            value.y = math.max(0f, value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                SuitCrushDearLieSlot,
                value,
                ref _fallbackSuitCrushDearLie);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        /// <summary>
        /// Publishes radiation mutation presentation scalars; x=hand displacement severity, y=stamina penalty, z=healing suppression, w=GlobalQualityWeight.
        /// </summary>
        public static void PublishRadiationMutation(Vector4 mutationVector)
        {
            float4 value = ToFiniteFloat4(mutationVector);
            value.x = math.saturate(value.x);
            value.y = math.saturate(value.y);
            value.z = math.saturate(value.z);
            value.w = math.saturate(value.w);
            _ = WriteReadSlot(
                RadiationMutationSlot,
                value,
                ref _fallbackRadiationMutation);

            if (!_visualSyncDispatcherActive)
                MarkFallbackShaderGlobalsDirty();
        }

        internal static void FlushFallbackVisualSync()
        {
            if (_visualSyncDispatcherActive || !_fallbackShaderGlobalsDirty)
                return;

            _fallbackShaderGlobalsDirty = false;
            Vector4 totalUniverseOffset = ToVector4(_fallbackAupTotalUniverseOffset);
            Vector4 aupShiftOffset = ToVector4(_fallbackAupShiftOffset);
            Vector4 uberNoirRuntime = ToVector4(_fallbackUberNoirRuntime);
            Vector4 physiologyDecompression = ToVector4(_fallbackPhysiologyDecompression);
            Vector4 physiologyGasToxicity = ToVector4(_fallbackPhysiologyGasToxicity);
            Vector4 respawnDearLie = ToVector4(_fallbackRespawnDearLie);
            Vector4 suitCrushDearLie = ToVector4(_fallbackSuitCrushDearLie);
            Vector4 radiationMutation = ToVector4(_fallbackRadiationMutation);

            Shader.SetGlobalVector(_BiolumMasterPhaseId, ToVector4(_fallbackBiolumMasterPhase));
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, totalUniverseOffset);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, totalUniverseOffset);
            Shader.SetGlobalVector(_AupShiftOffsetId, aupShiftOffset);
            Shader.SetGlobalFloat(_AupJitterMaskId, math.saturate(_fallbackAupShiftOffset.w));
            Shader.SetGlobalVector(_ExtinctionLutParamsId, ToVector4(_fallbackWaterExtinctionParams));
            Shader.SetGlobalVector(_ExtinctionLutRuntimeId, ToVector4(_fallbackWaterExtinctionRuntime));
            Shader.SetGlobalVector(_ExtinctionLutWeatherParamsId, ToVector4(_fallbackWaterExtinctionWeather));
            Shader.SetGlobalVector(_HectonUberNoirRuntimeParamsId, uberNoirRuntime);
            Shader.SetGlobalFloat(_HectonActiveShaderFeatureMaskId, math.clamp(_fallbackUberNoirFeatureMask.x, 0f, 16777215f));
            Shader.SetGlobalVector(_HectonDcsPhysiologyParamsId, physiologyDecompression);
            Shader.SetGlobalFloat(_HectonSupersaturationScalarId, math.saturate(physiologyDecompression.x));
            Shader.SetGlobalFloat(_HectonNarcosisScalarId, math.saturate(physiologyDecompression.y));
            Shader.SetGlobalVector(_HectonGasToxicityParamsId, physiologyGasToxicity);
            Shader.SetGlobalFloat(_HypoxiaSignalId, math.saturate(physiologyGasToxicity.x));
            Shader.SetGlobalVector(_HectonPowerBrownoutParamsId, ToVector4(_fallbackPowerBrownout));
            Shader.SetGlobalVector(_HectonRespawnDearLieParamsId, respawnDearLie);
            Shader.SetGlobalFloat(_HectonDeathFadeIntensityId, math.saturate(respawnDearLie.x));
            Shader.SetGlobalVector(_HectonSuitCrushDearLieParamsId, suitCrushDearLie);
            Shader.SetGlobalFloat(_HectonSuitCrushBucklingId, math.saturate(suitCrushDearLie.x));
            Shader.SetGlobalVector(_HectonRadiationMutationParamsId, radiationMutation);
            Shader.SetGlobalFloat(_HectonHandRadiationMutation01Id, math.saturate(radiationMutation.x));
        }

        private static void MarkFallbackShaderGlobalsDirty()
        {
            _fallbackShaderGlobalsDirty = true;
        }

        private static float4 WriteReadSlot(int slot, float4 value, ref float4 fallback)
        {
            return WriteReadSlot(
                AcquireCachedSlotsVaultNoAllocate(),
                slot,
                value,
                ref fallback,
                allowAllocation: false);
        }

        private static float4 WriteReadSlot(
            IDataVault vault,
            int slot,
            float4 value,
            ref float4 fallback,
            bool allowAllocation)
        {
            fallback = value;
            if (TryPrepareSlotsVault(vault, allowAllocation) &&
                vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
            {
                try
                {
                    if (IsSlotsHandleOwned(in _slotsHandle) &&
                        vault.TryResolveHandle(in _slotsHandle, out NativeArray<float4> slots) &&
                        slots.IsCreated &&
                        slots.Length >= SlotCount &&
                        slot >= 0 &&
                        slot < slots.Length)
                    {
                        slots[slot] = value;
                        return slots[slot];
                    }

                    InvalidateSlotsCache();
                }
                finally
                {
                    vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
                }
            }

            fallback = value;
            return fallback;
        }

        private static IDataVault AcquireCachedSlotsVaultNoAllocate()
        {
            IDataVault vault = _cachedVault;
            return TryPrepareSlotsVault(vault, allowAllocation: false) ? vault : null;
        }

        internal static bool BindPreparedShaderGlobalSlots(IDataVault vault)
        {
            return TryPrepareSlotsVault(vault, allowAllocation: false);
        }

        private static bool TryPrepareSlotsVault(IDataVault vault, bool allowAllocation)
        {
            if (!s_slotMapValid || vault == null || vault.IsCompactionFenceActive)
                return false;

            if (ReferenceEquals(vault, _cachedVault) &&
                IsSlotsHandleOwned(in _slotsHandle))
            {
                if (!_slotsValidated &&
                    !TryValidatePreparedSlotsGuarded(vault, in _slotsHandle))
                {
                    InvalidateSlotsCache();
                    return false;
                }

                _slotsValidated = true;
                return true;
            }

            if (vault.TryGetGenerationHandle<float4>(BufferID.ShaderGlobalState, out VaultGenerationHandle<float4> existing))
            {
                if (!IsSlotsHandleOwned(in existing))
                    return false;

                if (!TryValidatePreparedSlotsGuarded(vault, in existing))
                    return false;

                _cachedVault = vault;
                _slotsHandle = existing;
                _slotsValidated = true;
                return true;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<float4> allocated = vault.EnsureGenerationHandle<float4>(
                BufferID.ShaderGlobalState,
                SlotCount,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsSlotsHandleOwned(in allocated) ||
                !TryValidatePreparedSlotsGuarded(vault, in allocated))
                return false;

            _cachedVault = vault;
            _slotsHandle = allocated;
            _slotsValidated = true;
            return true;
        }

        private static void InvalidateSlotsCache()
        {
            _cachedVault = null;
            _slotsHandle = default;
            _slotsValidated = false;
        }

        private static bool TryValidatePreparedSlotsGuarded(
            IDataVault vault,
            in VaultGenerationHandle<float4> handle)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsSlotsHandleOwned(in handle) ||
                !vault.TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask))
            {
                return false;
            }

            try
            {
                return vault.TryResolveHandle(in handle, out NativeArray<float4> slots) &&
                       slots.IsCreated &&
                       slots.Length >= SlotCount;
            }
            finally
            {
                vault.ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask);
            }
        }

        internal static bool ValidateSharedSlotMap()
        {
            return SlotCount >= TelemetrySlotStart + TelemetrySlotCount &&
                   IsSlotRangeValid(ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   IsSlotRangeValid(PhysiologyDecompressionSlot, 1) &&
                   IsSlotRangeValid(PhysiologyGasToxicitySlot, 1) &&
                   IsSlotRangeValid(SuitCrushDearLieSlot, 1) &&
                   IsSlotRangeValid(RadiationMutationSlot, 1) &&
                   IsSlotRangeValid(DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   IsSlotRangeValid(ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   IsSlotRangeValid(TelemetrySlotStart, TelemetrySlotCount) &&
                   DispatcherRuntimeSlotStart >= ShaderGlobalsDtoSlot + ShaderGlobalsDtoSlotCount &&
                   PhysiologyDecompressionSlot < ShaderGlobalsDtoSlot &&
                   PhysiologyGasToxicitySlot >= ShaderGlobalsDtoSlot + ShaderGlobalsDtoSlotCount &&
                   PhysiologyGasToxicitySlot < DispatcherRuntimeSlotStart &&
                   RespawnDearLieSlot >= DispatcherRuntimeSlotStart + DispatcherRuntimeSlotCount &&
                   PowerBrownoutSlot > RespawnDearLieSlot &&
                   SuitCrushDearLieSlot > PowerBrownoutSlot &&
                   SuitCrushDearLieSlot < ThermalPackedSlotStart &&
                   RadiationMutationSlot > SuitCrushDearLieSlot &&
                   RadiationMutationSlot < ThermalPackedSlotStart &&
                   PowerBrownoutSlot < ThermalPackedSlotStart &&
                   !SlotInRange(RespawnDearLieSlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(PowerBrownoutSlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(SuitCrushDearLieSlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(RadiationMutationSlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(PhysiologyDecompressionSlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(PhysiologyGasToxicitySlot, ShaderGlobalsDtoSlot, ShaderGlobalsDtoSlotCount) &&
                   !SlotInRange(RespawnDearLieSlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(PowerBrownoutSlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(SuitCrushDearLieSlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(RadiationMutationSlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(PhysiologyDecompressionSlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(PhysiologyGasToxicitySlot, DispatcherRuntimeSlotStart, DispatcherRuntimeSlotCount) &&
                   !SlotInRange(RespawnDearLieSlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(PowerBrownoutSlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(SuitCrushDearLieSlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(RadiationMutationSlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(PhysiologyDecompressionSlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(PhysiologyGasToxicitySlot, ThermalPackedSlotStart, ThermalPackedSlotCount) &&
                   !SlotInRange(RespawnDearLieSlot, TelemetrySlotStart, TelemetrySlotCount) &&
                   !SlotInRange(PowerBrownoutSlot, TelemetrySlotStart, TelemetrySlotCount) &&
                   !SlotInRange(SuitCrushDearLieSlot, TelemetrySlotStart, TelemetrySlotCount) &&
                   !SlotInRange(RadiationMutationSlot, TelemetrySlotStart, TelemetrySlotCount) &&
                   !SlotInRange(PhysiologyDecompressionSlot, TelemetrySlotStart, TelemetrySlotCount) &&
                   !SlotInRange(PhysiologyGasToxicitySlot, TelemetrySlotStart, TelemetrySlotCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlotRangeValid(int start, int count)
        {
            return start >= 0 &&
                   count > 0 &&
                   start <= SlotCount - count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SlotInRange(int slot, int start, int count)
        {
            return slot >= start && slot < start + count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlotsHandleCreated(in VaultGenerationHandle<float4> handle)
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlotsHandleOwned(in VaultGenerationHandle<float4> handle)
        {
            return IsSlotsHandleCreated(in handle) &&
                   handle.BufferID == (uint)BufferID.ShaderGlobalState &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 CreateFloat4(float x, float y, float z, float w)
        {
            float4 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            value.w = w;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 CreateVector4(float x, float y, float z, float w)
        {
            Vector4 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            value.w = w;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 ToFiniteFloat4(Vector4 value)
        {
            float4 packed = default;
            packed.x = value.x;
            packed.y = value.y;
            packed.z = value.z;
            packed.w = value.w;
            return math.all(math.isfinite(packed)) ? packed : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ToVector4(float4 value)
        {
            Vector4 vector = default;
            vector.x = value.x;
            vector.y = value.y;
            vector.z = value.z;
            vector.w = value.w;
            return vector;
        }
    }
}
