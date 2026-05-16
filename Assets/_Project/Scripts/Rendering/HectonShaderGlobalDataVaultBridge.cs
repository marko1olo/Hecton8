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
        private const int BiolumMasterPhaseSlot = 0;
        private const int AupShiftOffsetSlot = 1;
        private const int WaterExtinctionRuntimeSlot = 2;
        private const int WaterExtinctionWeatherSlot = 3;
        private const int SlotCount = 4;

        private static readonly int _BiolumMasterPhaseId = Shader.PropertyToID("_BiolumMasterPhase");
        private static readonly int _GlobalBiolumPhaseId = Shader.PropertyToID("_GlobalBiolumPhase");
        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _TotalUniverseOffsetId = Shader.PropertyToID("_TotalUniverseOffset");
        private static readonly int _AupShiftOffsetId = Shader.PropertyToID("_AupShiftOffset");
        private static readonly int _AupJitterMaskId = Shader.PropertyToID("_AupJitterMask");
        private static readonly int _ExtinctionLutRuntimeId = Shader.PropertyToID("_ExtinctionLUTRuntime");
        private static readonly int _ExtinctionLutWeatherParamsId = Shader.PropertyToID("_ExtinctionLUTWeatherParams");

        private static IDataVault _cachedVault;
        private static uint _cachedVaultGeneration;
        private static NativeArray<float4> _slots;
        private static float4 _fallbackBiolumMasterPhase;
        private static float4 _fallbackAupShiftOffset;
        private static float4 _fallbackWaterExtinctionRuntime;
        private static float4 _fallbackWaterExtinctionWeather;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedVault = null;
            _cachedVaultGeneration = 0u;
            _slots = default;
            _fallbackBiolumMasterPhase = default;
            _fallbackAupShiftOffset = default;
            _fallbackWaterExtinctionRuntime = new float4(0f, 1f, 1f, 0f);
            _fallbackWaterExtinctionWeather = default;
        }

        public static void PublishBiolumMasterPhase(Vector4 phaseVector)
        {
            float4 storedPhase = WriteReadSlot(
                BiolumMasterPhaseSlot,
                ToFiniteFloat4(phaseVector),
                ref _fallbackBiolumMasterPhase);
            Vector4 bridgedPhase = ToVector4(storedPhase);
            Shader.SetGlobalVector(_BiolumMasterPhaseId, bridgedPhase);
            Shader.SetGlobalFloat(_GlobalBiolumPhaseId, bridgedPhase.x);
        }

        public static void PublishAupShaderGlobals(Vector4 totalUniverseOffset, Vector4 aupShiftOffset, float aupJitterMask)
        {
            float4 storedShift = WriteReadSlot(
                AupShiftOffsetSlot,
                ToFiniteFloat4(aupShiftOffset),
                ref _fallbackAupShiftOffset);
            Vector4 bridgedTotal = ToVector4(ToFiniteFloat4(totalUniverseOffset));
            Vector4 bridgedShift = ToVector4(storedShift);
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, bridgedTotal);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, bridgedTotal);
            Shader.SetGlobalVector(_AupShiftOffsetId, bridgedShift);
            Shader.SetGlobalFloat(_AupJitterMaskId, math.saturate(aupJitterMask));
        }

        public static void ResetAupShaderGlobals()
        {
            PublishAupShaderGlobals(Vector4.zero, Vector4.zero, 0f);
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
            Shader.SetGlobalVector(_ExtinctionLutWeatherParamsId, ToVector4(stored));
        }

        /// <summary>
        /// Resets water-extinction shader globals to a disabled deterministic fallback.
        /// </summary>
        public static void ResetWaterExtinctionGlobals()
        {
            PublishWaterExtinctionRuntime(new Vector4(0f, 1f, 1f, 0f));
            PublishWaterExtinctionWeather(Vector4.zero);
        }

        private static float4 WriteReadSlot(int slot, float4 value, ref float4 fallback)
        {
            if (TryResolveSlots(out NativeArray<float4> slots))
            {
                slots[slot] = value;
                return slots[slot];
            }

            fallback = value;
            return fallback;
        }

        private static bool TryResolveSlots(out NativeArray<float4> slots)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                slots = default;
                return false;
            }

            uint generation = vault.VaultGenerationID;
            if (ReferenceEquals(vault, _cachedVault) &&
                _cachedVaultGeneration == generation &&
                _slots.IsCreated &&
                _slots.Length >= SlotCount)
            {
                slots = _slots;
                return true;
            }

            if (vault.TryGetBuffer(BufferID.ShaderGlobalState, out NativeArray<float4> existing) &&
                existing.IsCreated &&
                existing.Length >= SlotCount)
            {
                _cachedVault = vault;
                _cachedVaultGeneration = generation;
                _slots = existing;
                slots = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                slots = default;
                return false;
            }

            NativeArray<float4> allocated = vault.GetBuffer<float4>(
                BufferID.ShaderGlobalState,
                SlotCount,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!allocated.IsCreated || allocated.Length < SlotCount)
            {
                slots = default;
                return false;
            }

            _cachedVault = vault;
            _cachedVaultGeneration = generation;
            _slots = allocated;
            slots = allocated;
            return true;
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
