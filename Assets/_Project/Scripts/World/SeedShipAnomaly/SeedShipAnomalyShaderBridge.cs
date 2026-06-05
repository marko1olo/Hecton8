using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.SeedShipAnomaly
{
    public static class SeedShipAnomalyShaderBridge
    {
        private const int SeedShipAnomalySlot = 7;
        private const int RequiredShaderSlots = 512;
        private static readonly int _SeedShipAnomalyParamsId = Shader.PropertyToID("_SeedShipAnomalyParams");
        private static readonly int _SeedShipUniverseOffsetNoiseId = Shader.PropertyToID("_SeedShipUniverseOffsetNoise");

        private static VaultGenerationHandle<float4> _shaderSlotsHandle;
        private static IDataVault _cachedVault;

        public static void Publish(IDataVault vault, in AnomalyFieldDTO field, in AnomalyGlobalScalarsDTO globals)
        {
            float4 payload = new float4(
                math.saturate(globals.Corruption01),
                math.saturate(globals.UniverseOffsetNoise01),
                math.saturate(globals.HeatSource01),
                math.saturate(globals.RadarJam01));

            if (vault != null && TryEnsureShaderSlots(vault))
            {
                VaultGenerationHandle<float4> shaderSlotsHandle = _shaderSlotsHandle;
                if (!vault.TryAcquireWriteLock(in shaderSlotsHandle, SystemID.EndgameAnomaly, out NativeArray<float4> slots))
                {
                    InvalidateShaderSlotCache();
                }
                else
                {
                    try
                    {
                        if (slots.IsCreated && (uint)SeedShipAnomalySlot < (uint)slots.Length)
                            slots[SeedShipAnomalySlot] = payload;
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in shaderSlotsHandle, SystemID.EndgameAnomaly);
                    }
                }
            }

            Shader.SetGlobalVector(_SeedShipAnomalyParamsId, new Vector4(payload.x, payload.y, payload.z, payload.w));
            Shader.SetGlobalFloat(_SeedShipUniverseOffsetNoiseId, payload.y);
        }

        private static bool TryEnsureShaderSlots(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (ReferenceEquals(vault, _cachedVault) &&
                IsVaultGenerationHandleCreated(in _shaderSlotsHandle) &&
                vault.TryResolveHandle(in _shaderSlotsHandle, out NativeArray<float4> slots))
            {
                return slots.IsCreated && slots.Length >= RequiredShaderSlots;
            }

            if (vault.TryGetGenerationHandle(BufferID.ShaderGlobalState, out _shaderSlotsHandle) &&
                vault.TryResolveHandle(in _shaderSlotsHandle, out slots) &&
                slots.IsCreated &&
                slots.Length >= RequiredShaderSlots)
            {
                _cachedVault = vault;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            _shaderSlotsHandle = vault.EnsureGenerationHandle<float4>(
                BufferID.ShaderGlobalState,
                RequiredShaderSlots,
                SystemID.EndgameAnomaly,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultGenerationHandleCreated(in _shaderSlotsHandle) ||
                !vault.TryResolveHandle(in _shaderSlotsHandle, out slots) ||
                !slots.IsCreated ||
                slots.Length < RequiredShaderSlots)
            {
                _shaderSlotsHandle = default;
                return false;
            }

            _cachedVault = vault;
            return true;
        }

        private static void InvalidateShaderSlotCache()
        {
            _cachedVault = null;
            _shaderSlotsHandle = default;
        }

        private static bool IsVaultGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }
    }
}
