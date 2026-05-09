using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Power;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [System.Flags]
    internal enum ProxyLightFlags : uint
    {
        None = 0,
        Visible = 1u << 0,
        Powered = 1u << 1,
        UiPanel = 1u << 2
    }

    internal enum ProxyLightType : byte
    {
        Point = 0,
        Panel = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ProxyLightData
    {
        public AbsoluteUniversePosition PositionAup;
        public float3 RuntimePosition;
        public float RangeMeters;
        public float3 ColorLinear;
        public float Intensity;
        public float3 Forward;
        public float SpotCosine;
        public float ShadowPhase01;
        public float PowerFlicker01;
        public float OxygenStress01;
        public float LastUpdateUnscaledTime;
        public uint Flags;
        public byte Type;
        public byte Lod;
        private ushort _reserved;

        public static ProxyLightData CreateUiPanel(
            in AbsoluteUniversePosition positionAup,
            float3 runtimePosition,
            float3 forward,
            Color colorLinear,
            float rangeMeters,
            float intensity,
            float shadowPhase01,
            float powerFlicker01,
            float oxygenStress01,
            float unscaledTimeSeconds)
        {
            return new ProxyLightData
            {
                PositionAup = positionAup,
                RuntimePosition = runtimePosition,
                RangeMeters = math.max(0.01f, rangeMeters),
                ColorLinear = new float3(colorLinear.r, colorLinear.g, colorLinear.b),
                Intensity = math.saturate(intensity),
                Forward = math.normalizesafe(forward, new float3(0f, 0f, 1f)),
                SpotCosine = 0f,
                ShadowPhase01 = math.saturate(shadowPhase01),
                PowerFlicker01 = math.saturate(powerFlicker01),
                OxygenStress01 = math.saturate(oxygenStress01),
                LastUpdateUnscaledTime = math.max(0f, unscaledTimeSeconds),
                Flags = (uint)(ProxyLightFlags.Visible | ProxyLightFlags.Powered | ProxyLightFlags.UiPanel),
                Type = (byte)ProxyLightType.Panel,
                Lod = 0
            };
        }

        public static ProxyLightData CreateTransientPoint(
            in AbsoluteUniversePosition positionAup,
            float3 runtimePosition,
            Color colorLinear,
            float rangeMeters,
            float intensity,
            float unscaledTimeSeconds)
        {
            return new ProxyLightData
            {
                PositionAup = positionAup,
                RuntimePosition = runtimePosition,
                RangeMeters = math.max(0.01f, rangeMeters),
                ColorLinear = new float3(colorLinear.r, colorLinear.g, colorLinear.b),
                Intensity = math.saturate(intensity),
                Forward = new float3(0f, 0f, 1f),
                SpotCosine = 0f,
                ShadowPhase01 = 0f,
                PowerFlicker01 = 1f,
                OxygenStress01 = 0f,
                LastUpdateUnscaledTime = math.max(0f, unscaledTimeSeconds),
                Flags = (uint)(ProxyLightFlags.Visible | ProxyLightFlags.Powered),
                Type = (byte)ProxyLightType.Point,
                Lod = 0
            };
        }
    }

    /// <summary>
    /// Native registry for diegetic UI and gameplay systems that need lightweight proxy light data.
    /// </summary>
    internal static class ProxyLightRegistry
    {
        private const int MaxProxyLights = 128;
        private const float MinimumVisibleIntensity = 0.0001f;
        private const float BrownoutIntensityFloor = 0.14f;
        private const float BrownoutIntensityCeiling = 0.72f;
        private const float BrownoutFlickerFrequency = 47.3f;
        private const float BrownoutBiasPadeK = 0.32f;
        private const float TwoPi = 6.28318530718f;

        private static NativeParallelHashMap<int, ProxyLightData> _lightsByKey;
        private static NativeParallelHashMap<int, int> _slotByKey;
        private static NativeArray<int> _keys;
        private static NativeQueue<int> _freeProxyLightSlots;
        private static int _keyCount;
        private static int _registeredCount;

        public static bool IsInitialized =>
            _lightsByKey.IsCreated &&
            _slotByKey.IsCreated &&
            _keys.IsCreated &&
            _freeProxyLightSlots.IsCreated;

        public static int RegisteredCount => _registeredCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void EnsureInitialized()
        {
            if (IsInitialized)
                return;

            Shutdown();
            _lightsByKey = new NativeParallelHashMap<int, ProxyLightData>(MaxProxyLights, Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<int,ProxyLightData>[128] - proxy light registry storage - owner: ProxyLightRegistry
            _slotByKey = new NativeParallelHashMap<int, int>(MaxProxyLights, Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<int,int>[128] - proxy light key-to-slot recycling map - owner: ProxyLightRegistry
            _keys = new NativeArray<int>(MaxProxyLights, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[128] - proxy light key iteration buffer - owner: ProxyLightRegistry
            _freeProxyLightSlots = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[128] - O(1) recycled proxy light slot IDs - owner: ProxyLightRegistry
            NativeMemorySentinel.RegisterNativeParallelHashMap(_lightsByKey, nameof(ProxyLightRegistry), nameof(_lightsByKey), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_slotByKey, nameof(ProxyLightRegistry), nameof(_slotByKey), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_keys, nameof(ProxyLightRegistry), nameof(_keys), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeQueue(_freeProxyLightSlots, MaxProxyLights, nameof(ProxyLightRegistry), nameof(_freeProxyLightSlots), NativeAllocationLifetime.Session);
            PrewarmFreeProxyLightSlots();
            _keyCount = 0;
            _registeredCount = 0;
        }

        public static bool RegisterOrUpdate(int key, in ProxyLightData data)
        {
            if (key == 0 || !IsValid(in data))
                return false;

            ProxyLightData resolvedData = data;
            ApplyPoweredPanelModulation(ref resolvedData);
            if (!IsValid(in resolvedData))
                return false;

            EnsureInitialized();
            bool existed = _slotByKey.TryGetValue(key, out int slot);
            if (!existed)
            {
                if (!TryAcquireProxyLightSlot(out slot))
                    return false;

                _keys[slot] = key;
                if (!_slotByKey.TryAdd(key, slot))
                {
                    _keys[slot] = 0;
                    _freeProxyLightSlots.Enqueue(slot);
                    return false;
                }
            }
            else
            {
                _lightsByKey.Remove(key);
            }

            if (_lightsByKey.TryAdd(key, resolvedData))
            {
                if (!existed)
                    _registeredCount++;

                return true;
            }

            if (!existed)
            {
                _slotByKey.Remove(key);
                _keys[slot] = 0;
                _freeProxyLightSlots.Enqueue(slot);
            }

            return false;
        }

        public static void Unregister(int key)
        {
            if (!IsInitialized || key == 0)
                return;

            if (!_slotByKey.TryGetValue(key, out int slot))
            {
                _lightsByKey.Remove(key);
                return;
            }

            if (!_lightsByKey.Remove(key))
                return;

            _slotByKey.Remove(key);
            if ((uint)slot < MaxProxyLights)
            {
                _keys[slot] = 0;
                _freeProxyLightSlots.Enqueue(slot);
            }

            _registeredCount = math.max(0, _registeredCount - 1);
        }

        public static int GetVisibleLightsBatch(
            in AbsoluteUniversePosition viewerAup,
            float3 viewerForward,
            float maxDistanceMeters,
            float minimumForwardDot,
            NativeArray<ProxyLightData> output)
        {
            if (!IsInitialized || !output.IsCreated || output.Length == 0)
                return 0;

            float3 safeForward = math.normalizesafe(viewerForward);
            bool useForwardGate = math.lengthsq(safeForward) > 0.0001f && minimumForwardDot > -1f;
            float safeMaxDistance = math.isfinite(maxDistanceMeters) && maxDistanceMeters > 0f
                ? maxDistanceMeters
                : 0.01f;
            float maxDistanceSq = math.max(0.0001f, safeMaxDistance * safeMaxDistance);
            int visibleCount = 0;

            for (int i = 0; i < _keyCount && visibleCount < output.Length; i++)
            {
                int key = _keys[i];
                if (!_lightsByKey.TryGetValue(key, out ProxyLightData light))
                    continue;

                if ((light.Flags & (uint)ProxyLightFlags.Visible) == 0u ||
                    (light.Flags & (uint)ProxyLightFlags.Powered) == 0u ||
                    light.Intensity <= MinimumVisibleIntensity)
                {
                    continue;
                }

                float3 cameraRelative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in light.PositionAup, in viewerAup);
                float distanceSq = math.lengthsq(cameraRelative);
                float lightRange = math.isfinite(light.RangeMeters) && light.RangeMeters > 0f
                    ? light.RangeMeters
                    : 0.01f;
                float rangeSq = math.min(maxDistanceSq, math.max(0.0001f, lightRange * lightRange));
                if (distanceSq > rangeSq)
                    continue;

                if (useForwardGate)
                {
                    float3 direction = math.normalizesafe(cameraRelative);
                    if (math.dot(direction, safeForward) < minimumForwardDot)
                        continue;
                }

                output[visibleCount++] = light;
            }

            return visibleCount;
        }

        public static bool TryGet(int key, out ProxyLightData data)
        {
            data = default;
            return IsInitialized && key != 0 && _lightsByKey.TryGetValue(key, out data);
        }

        public static void Clear()
        {
            if (!IsInitialized)
                return;

            _lightsByKey.Clear();
            _slotByKey.Clear();
            while (_freeProxyLightSlots.TryDequeue(out _))
            {
            }

            for (int i = 0; i < _keyCount; i++)
                _keys[i] = 0;

            _keyCount = 0;
            _registeredCount = 0;
        }

        public static void Shutdown()
        {
            if (_lightsByKey.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(ProxyLightRegistry), nameof(_lightsByKey));
                _lightsByKey.Dispose();
            }

            if (_slotByKey.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(ProxyLightRegistry), nameof(_slotByKey));
                _slotByKey.Dispose();
            }

            if (_keys.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_keys);
                _keys.Dispose();
            }

            if (_freeProxyLightSlots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProxyLightRegistry), nameof(_freeProxyLightSlots));
                _freeProxyLightSlots.Dispose();
            }

            _lightsByKey = default;
            _slotByKey = default;
            _keys = default;
            _freeProxyLightSlots = default;
            _keyCount = 0;
            _registeredCount = 0;
        }

        private static void PrewarmFreeProxyLightSlots()
        {
            if (!_freeProxyLightSlots.IsCreated)
                return;

            for (int i = 0; i < MaxProxyLights; i++)
                _freeProxyLightSlots.Enqueue(default);

            while (_freeProxyLightSlots.TryDequeue(out _))
            {
            }
        }

        private static bool TryAcquireProxyLightSlot(out int slot)
        {
            while (_freeProxyLightSlots.TryDequeue(out int recycledSlot))
            {
                if ((uint)recycledSlot < MaxProxyLights && _keys[recycledSlot] == 0)
                {
                    slot = recycledSlot;
                    return true;
                }
            }

            if (_keyCount >= MaxProxyLights)
            {
                slot = -1;
                return false;
            }

            slot = _keyCount++;
            return true;
        }

        private static bool IsValid(in ProxyLightData data)
        {
            return data.Intensity > MinimumVisibleIntensity &&
                   data.RangeMeters > 0f &&
                   math.all(math.isfinite(data.RuntimePosition)) &&
                   math.all(math.isfinite(data.ColorLinear)) &&
                   math.all(math.isfinite(data.Forward));
        }

        private static void ApplyPoweredPanelModulation(ref ProxyLightData data)
        {
            if ((data.Flags & (uint)ProxyLightFlags.Powered) == 0u ||
                (data.Flags & (uint)ProxyLightFlags.UiPanel) == 0u ||
                !TryResolvePowerGridBrownout(out bool brownoutActive, out float supplyRatio) ||
                !brownoutActive)
            {
                return;
            }

            float phase = (data.LastUpdateUnscaledTime * BrownoutFlickerFrequency) + (data.ShadowPhase01 * TwoPi);
            float flickerWave = math.abs(math.sin(phase) * math.sin((phase * 0.37f) + 1.618f));
            float brownoutFlicker01 = FastBrownoutBias01(flickerWave);
            float supplyScalar = math.lerp(0.55f, 1f, math.saturate(supplyRatio));
            float intensityScalar = math.lerp(BrownoutIntensityFloor, BrownoutIntensityCeiling, brownoutFlicker01) * supplyScalar;
            data.Intensity = math.saturate(data.Intensity * intensityScalar);
            data.PowerFlicker01 = math.saturate(data.PowerFlicker01 * intensityScalar);
        }

        private static float FastBrownoutBias01(float value)
        {
            float x = math.saturate(value);
            float denominator = x + (BrownoutBiasPadeK * (1f - x));
            return denominator > 0.000001f ? x / denominator : 0f;
        }

        private static bool TryResolvePowerGridBrownout(out bool brownoutActive, out float supplyRatio)
        {
            brownoutActive = false;
            supplyRatio = 1f;

            int gridCount = PowerGridManager.RuntimeGridCount;
            if (gridCount > 0)
            {
                for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
                {
                    PowerGrid grid = PowerGridManager.GetRuntimeGridAt(gridIndex);
                    if (grid == null)
                        continue;

                    supplyRatio = math.min(supplyRatio, math.saturate(grid.SupplyRatio));
                    brownoutActive |= grid.BrownoutTier != LogisticsBrownoutTier.None ||
                                      grid.IsBatteryEmergencyReserveActive ||
                                      grid.HasPowerDeficit;
                }

                return true;
            }

            IPowerGridService powerGrid = GlobalRegistry.PowerGrid;
            if (powerGrid == null)
                return false;

            supplyRatio = powerGrid.TotalConsumption > 0.0001f
                ? math.saturate(powerGrid.TotalGeneration / powerGrid.TotalConsumption)
                : 1f;
            BatteryRuntimeSnapshot batterySnapshot = powerGrid.BatterySnapshot;
            brownoutActive = supplyRatio < 0.85f || batterySnapshot.EmergencyReserveActive;
            return true;
        }
    }
}
