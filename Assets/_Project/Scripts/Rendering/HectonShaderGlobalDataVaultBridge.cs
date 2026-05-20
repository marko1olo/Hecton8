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
        internal const int PowerBrownoutSlot = 8;
        internal const int RespawnDearLieSlot = 19;
        // Shared with GlobalShaderDispatcher: slots 64-363 are the 300-frame CBuffer blackbox.
        internal const int SlotCount = 512;

        private static readonly int _BiolumMasterPhaseId = Shader.PropertyToID("_BiolumMasterPhase");
        private static readonly int _GlobalBiolumPhaseId = Shader.PropertyToID("_GlobalBiolumPhase");
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
        private static readonly int _HectonSupersaturationScalarId = Shader.PropertyToID("_HectonSupersaturationScalar");
        private static readonly int _HectonNarcosisScalarId = Shader.PropertyToID("_HectonNarcosisScalar");
        private static readonly int _HectonPowerBrownoutParamsId = Shader.PropertyToID("_HectonPowerBrownoutParams");
        private static readonly int _HectonRespawnDearLieParamsId = Shader.PropertyToID("_HectonRespawnDearLieParams");
        private static readonly int _HectonDeathFadeIntensityId = Shader.PropertyToID("_HectonDeathFadeIntensity");

        private static IDataVault _cachedVault;
        private static uint _cachedVaultGeneration;
        private static VaultGenerationHandle<float4> _slotsHandle;
        private static float4 _fallbackBiolumMasterPhase;
        private static float4 _fallbackAupShiftOffset;
        private static float4 _fallbackWaterExtinctionRuntime;
        private static float4 _fallbackWaterExtinctionWeather;
        private static float4 _fallbackWaterExtinctionParams;
        private static float4 _fallbackUberNoirRuntime;
        private static float4 _fallbackUberNoirFeatureMask;
        private static float4 _fallbackPhysiologyDecompression;
        private static float4 _fallbackPowerBrownout;
        private static float4 _fallbackRespawnDearLie;
        private static bool _visualSyncDispatcherActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedVault = null;
            _cachedVaultGeneration = 0u;
            _slotsHandle = default;
            _fallbackBiolumMasterPhase = default;
            _fallbackAupShiftOffset = default;
            _fallbackWaterExtinctionRuntime = CreateFloat4(0f, 1f, 1f, 0f);
            _fallbackWaterExtinctionWeather = default;
            _fallbackWaterExtinctionParams = CreateFloat4(1500f, 2.5f, 0f, 0f);
            _fallbackUberNoirRuntime = CreateFloat4(0f, 1f, 0f, 0f);
            _fallbackUberNoirFeatureMask = default;
            _fallbackPhysiologyDecompression = default;
            _fallbackPowerBrownout = CreateFloat4(1f, 0f, 0f, 0f);
            _fallbackRespawnDearLie = default;
            _visualSyncDispatcherActive = false;
        }

        internal static void SetVisualSyncDispatcherActive(bool active)
        {
            _visualSyncDispatcherActive = active;
        }

        public static void PublishBiolumMasterPhase(Vector4 phaseVector)
        {
            float4 storedPhase = WriteReadSlot(
                BiolumMasterPhaseSlot,
                ToFiniteFloat4(phaseVector),
                ref _fallbackBiolumMasterPhase);
            Vector4 bridgedPhase = ToVector4(storedPhase);
            if (!_visualSyncDispatcherActive)
            {
                Shader.SetGlobalVector(_BiolumMasterPhaseId, bridgedPhase);
                Shader.SetGlobalFloat(_GlobalBiolumPhaseId, bridgedPhase.x);
            }
        }

        public static void PublishAupShaderGlobals(Vector4 totalUniverseOffset, Vector4 aupShiftOffset, float aupJitterMask)
        {
            float4 packedShift = ToFiniteFloat4(aupShiftOffset);
            packedShift.w = math.saturate(aupJitterMask);
            float4 storedShift = WriteReadSlot(
                AupShiftOffsetSlot,
                packedShift,
                ref _fallbackAupShiftOffset);
            Vector4 bridgedTotal = ToVector4(ToFiniteFloat4(totalUniverseOffset));
            Vector4 bridgedShift = ToVector4(storedShift);
            if (!_visualSyncDispatcherActive)
            {
                Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, bridgedTotal);
                Shader.SetGlobalVector(_TotalUniverseOffsetId, bridgedTotal);
                Shader.SetGlobalVector(_AupShiftOffsetId, bridgedShift);
                Shader.SetGlobalFloat(_AupJitterMaskId, math.saturate(aupJitterMask));
            }
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
            float4 stored = WriteReadSlot(
                WaterExtinctionParamsSlot,
                value,
                ref _fallbackWaterExtinctionParams);
            if (!_visualSyncDispatcherActive)
                Shader.SetGlobalVector(_ExtinctionLutParamsId, ToVector4(stored));
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
            float4 stored = WriteReadSlot(
                WaterExtinctionRuntimeSlot,
                value,
                ref _fallbackWaterExtinctionRuntime);
            if (!_visualSyncDispatcherActive)
                Shader.SetGlobalVector(_ExtinctionLutRuntimeId, ToVector4(stored));
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
            float4 stored = WriteReadSlot(
                WaterExtinctionWeatherSlot,
                value,
                ref _fallbackWaterExtinctionWeather);
            if (!_visualSyncDispatcherActive)
                Shader.SetGlobalVector(_ExtinctionLutWeatherParamsId, ToVector4(stored));
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
            float4 storedRuntime = WriteReadSlot(
                UberNoirRuntimeSlot,
                value,
                ref _fallbackUberNoirRuntime);

            float safeFeatureMask = math.clamp(
                math.isfinite(featureMask) ? featureMask : 0f,
                0f,
                16777215f);
            float4 storedMask = WriteReadSlot(
                UberNoirFeatureMaskSlot,
                CreateFloat4(safeFeatureMask, 0f, 0f, 0f),
                ref _fallbackUberNoirFeatureMask);

            if (!_visualSyncDispatcherActive)
            {
                Shader.SetGlobalVector(_HectonUberNoirRuntimeParamsId, ToVector4(storedRuntime));
                Shader.SetGlobalFloat(_HectonActiveShaderFeatureMaskId, storedMask.x);
            }
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
            float4 stored = WriteReadSlot(
                PhysiologyDecompressionSlot,
                value,
                ref _fallbackPhysiologyDecompression);

            if (!_visualSyncDispatcherActive)
            {
                Vector4 vector = ToVector4(stored);
                Shader.SetGlobalVector(_HectonDcsPhysiologyParamsId, vector);
                Shader.SetGlobalFloat(_HectonSupersaturationScalarId, vector.x);
                Shader.SetGlobalFloat(_HectonNarcosisScalarId, vector.y);
            }
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
            float4 stored = WriteReadSlot(
                PowerBrownoutSlot,
                value,
                ref _fallbackPowerBrownout);

            if (!_visualSyncDispatcherActive)
                Shader.SetGlobalVector(_HectonPowerBrownoutParamsId, ToVector4(stored));
        }

        /// <summary>
        /// Publishes the player-death visual cover: x=fade, y=chromatic, z=grain, w=GlobalQualityWeight.
        /// </summary>
        public static void PublishRespawnDearLie(Vector4 dearLieVector)
        {
            PublishRespawnDearLie(ResolveSlotsVault(), dearLieVector);
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
            float4 stored = WriteReadSlot(
                vault,
                RespawnDearLieSlot,
                value,
                ref _fallbackRespawnDearLie);

            if (!_visualSyncDispatcherActive)
            {
                Vector4 vector = ToVector4(stored);
                Shader.SetGlobalVector(_HectonRespawnDearLieParamsId, vector);
                Shader.SetGlobalFloat(_HectonDeathFadeIntensityId, vector.x);
            }
        }

        private static float4 WriteReadSlot(int slot, float4 value, ref float4 fallback)
        {
            return WriteReadSlot(ResolveSlotsVault(), slot, value, ref fallback);
        }

        private static float4 WriteReadSlot(IDataVault vault, int slot, float4 value, ref float4 fallback)
        {
            if (TryPrepareSlotsVault(vault) &&
                vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
            {
                try
                {
                    if (vault.TryResolveHandle(in _slotsHandle, out NativeArray<float4> slots) &&
                        slots.IsCreated &&
                        slot >= 0 &&
                        slot < slots.Length)
                    {
                        slots[slot] = value;
                        return slots[slot];
                    }
                }
                finally
                {
                    vault.TryUnlockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability);
                }
            }

            fallback = value;
            return fallback;
        }

        private static IDataVault ResolveSlotsVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return TryPrepareSlotsVault(vault) ? vault : null;
        }

        private static bool TryPrepareSlotsVault(IDataVault vault)
        {
            if (vault == null)
                return false;

            uint generation = vault.VaultGenerationID;
            if (ReferenceEquals(vault, _cachedVault) &&
                _cachedVaultGeneration == generation &&
                IsSlotsHandleCreated(in _slotsHandle) &&
                vault.TryResolveHandle(in _slotsHandle, out NativeArray<float4> cachedSlots) &&
                cachedSlots.IsCreated &&
                cachedSlots.Length >= SlotCount)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<float4>(BufferID.ShaderGlobalState, out VaultGenerationHandle<float4> existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<float4> existingSlots) &&
                existingSlots.IsCreated &&
                existingSlots.Length >= SlotCount)
            {
                _cachedVault = vault;
                _cachedVaultGeneration = generation;
                _slotsHandle = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<float4> allocated = vault.GetGenerationHandle<float4>(
                BufferID.ShaderGlobalState,
                SlotCount,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!vault.TryResolveHandle(in allocated, out NativeArray<float4> allocatedSlots) ||
                !allocatedSlots.IsCreated ||
                allocatedSlots.Length < SlotCount)
                return false;

            _cachedVault = vault;
            _cachedVaultGeneration = generation;
            _slotsHandle = allocated;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlotsHandleCreated(in VaultGenerationHandle<float4> handle)
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
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
