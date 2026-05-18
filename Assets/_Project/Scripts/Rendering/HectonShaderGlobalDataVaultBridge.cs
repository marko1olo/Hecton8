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

        private static IDataVault _cachedVault;
        private static uint _cachedVaultGeneration;
        private static VaultBufferHandle<float4> _slotsHandle;
        private static float4 _fallbackBiolumMasterPhase;
        private static float4 _fallbackAupShiftOffset;
        private static float4 _fallbackWaterExtinctionRuntime;
        private static float4 _fallbackWaterExtinctionWeather;
        private static float4 _fallbackWaterExtinctionParams;
        private static float4 _fallbackUberNoirRuntime;
        private static float4 _fallbackUberNoirFeatureMask;
        private static bool _visualSyncDispatcherActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedVault = null;
            _cachedVaultGeneration = 0u;
            _slotsHandle = default;
            _fallbackBiolumMasterPhase = default;
            _fallbackAupShiftOffset = default;
            _fallbackWaterExtinctionRuntime = new float4(0f, 1f, 1f, 0f);
            _fallbackWaterExtinctionWeather = default;
            _fallbackWaterExtinctionParams = new float4(1500f, 2.5f, 0f, 0f);
            _fallbackUberNoirRuntime = new float4(0f, 1f, 0f, 0f);
            _fallbackUberNoirFeatureMask = default;
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
            PublishWaterExtinctionParams(new Vector4(1500f, 2.5f, 0f, 0f));
            PublishWaterExtinctionRuntime(new Vector4(0f, 1f, 1f, 0f));
            PublishWaterExtinctionWeather(Vector4.zero);
        }

        /// <summary>
        /// Publishes a deterministic analytical Beer-Lambert fallback while keeping LUT texture sampling disabled.
        /// </summary>
        public static void PublishWaterExtinctionAnalyticalFallback()
        {
            PublishWaterExtinctionParams(new Vector4(1500f, 2.5f, 1f, 0f));
            PublishWaterExtinctionRuntime(new Vector4(0f, 1f, 1f, 1f));
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
                new float4(safeFeatureMask, 0f, 0f, 0f),
                ref _fallbackUberNoirFeatureMask);

            if (!_visualSyncDispatcherActive)
            {
                Shader.SetGlobalVector(_HectonUberNoirRuntimeParamsId, ToVector4(storedRuntime));
                Shader.SetGlobalFloat(_HectonActiveShaderFeatureMaskId, storedMask.x);
            }
        }

        private static float4 WriteReadSlot(int slot, float4 value, ref float4 fallback)
        {
            IDataVault vault = ResolveSlotsVault();
            if (vault != null && vault.TryLockBuffer(BufferID.ShaderGlobalState, SystemID.GraphicsScalability))
            {
                try
                {
                    var slots = _slotsHandle.Resolve(vault);
                    if (slots.IsCreated && slot >= 0 && slot < slots.Length)
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
            if (vault == null)
                return null;

            uint generation = vault.VaultGenerationID;
            if (ReferenceEquals(vault, _cachedVault) &&
                _cachedVaultGeneration == generation &&
                _slotsHandle.IsCreated &&
                _slotsHandle.Length >= SlotCount)
            {
                return vault;
            }

            if (vault.TryGetBufferHandle(BufferID.ShaderGlobalState, out VaultBufferHandle<float4> existing) &&
                existing.IsCreated &&
                existing.Length >= SlotCount)
            {
                _cachedVault = vault;
                _cachedVaultGeneration = generation;
                _slotsHandle = existing;
                return vault;
            }

            if (vault.IsAllocationLocked)
                return null;

            VaultBufferHandle<float4> allocated = vault.GetBufferHandle<float4>(
                BufferID.ShaderGlobalState,
                SlotCount,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!allocated.IsCreated || allocated.Length < SlotCount)
                return null;

            _cachedVault = vault;
            _cachedVaultGeneration = generation;
            _slotsHandle = allocated;
            return vault;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 ToFiniteFloat4(Vector4 value)
        {
            float4 packed = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(packed)) ? packed : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }
    }
}
