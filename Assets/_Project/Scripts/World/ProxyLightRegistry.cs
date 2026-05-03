using System.Runtime.InteropServices;
using Hecton8.Core;
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

    [StructLayout(LayoutKind.Sequential)]
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
                LastUpdateUnscaledTime = math.max(0f, unscaledTimeSeconds),
                Flags = (uint)(ProxyLightFlags.Visible | ProxyLightFlags.Powered | ProxyLightFlags.UiPanel),
                Type = (byte)ProxyLightType.Panel,
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

        private static NativeParallelHashMap<int, ProxyLightData> _lightsByKey;
        private static NativeArray<int> _keys;
        private static int _keyCount;

        public static bool IsInitialized => _lightsByKey.IsCreated && _keys.IsCreated;

        public static int RegisteredCount => _keyCount;

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
            _keys = new NativeArray<int>(MaxProxyLights, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[128] - proxy light key iteration buffer - owner: ProxyLightRegistry
            NativeMemorySentinel.RegisterNativeParallelHashMap(_lightsByKey, nameof(ProxyLightRegistry), nameof(_lightsByKey), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_keys, nameof(ProxyLightRegistry), nameof(_keys), NativeAllocationLifetime.Session);
            _keyCount = 0;
        }

        public static bool RegisterOrUpdate(int key, in ProxyLightData data)
        {
            if (key == 0 || !IsValid(in data))
                return false;

            EnsureInitialized();
            bool existed = _lightsByKey.ContainsKey(key);
            if (!existed)
            {
                if (_keyCount >= MaxProxyLights)
                    return false;

                _keys[_keyCount++] = key;
            }
            else
            {
                _lightsByKey.Remove(key);
            }

            return _lightsByKey.TryAdd(key, data);
        }

        public static void Unregister(int key)
        {
            if (!IsInitialized || key == 0)
                return;

            if (!_lightsByKey.Remove(key))
                return;

            for (int i = 0; i < _keyCount; i++)
            {
                if (_keys[i] != key)
                    continue;

                int lastIndex = _keyCount - 1;
                _keys[i] = _keys[lastIndex];
                _keys[lastIndex] = 0;
                _keyCount = lastIndex;
                return;
            }
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
            float maxDistanceSq = math.max(0.01f, maxDistanceMeters * maxDistanceMeters);
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
                float rangeSq = math.max(maxDistanceSq, light.RangeMeters * light.RangeMeters);
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
            for (int i = 0; i < _keyCount; i++)
                _keys[i] = 0;

            _keyCount = 0;
        }

        public static void Shutdown()
        {
            if (_lightsByKey.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(ProxyLightRegistry), nameof(_lightsByKey));
                _lightsByKey.Dispose();
            }

            if (_keys.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_keys);
                _keys.Dispose();
            }

            _lightsByKey = default;
            _keys = default;
            _keyCount = 0;
        }

        private static bool IsValid(in ProxyLightData data)
        {
            return data.Intensity > MinimumVisibleIntensity &&
                   data.RangeMeters > 0f &&
                   math.all(math.isfinite(data.RuntimePosition)) &&
                   math.all(math.isfinite(data.ColorLinear)) &&
                   math.all(math.isfinite(data.Forward));
        }
    }
}
