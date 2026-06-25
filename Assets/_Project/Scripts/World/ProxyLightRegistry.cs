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
        UiPanel = 1u << 2,
        PlayerOwned = 1u << 3,
        BioluminescentPulse = 1u << 4
    }

    internal enum ProxyLightType : byte
    {
        Point = 0,
        Panel = 1
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct ProxyLightData
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)]
        public float3 RuntimePosition;
        [FieldOffset(60)]
        public float RangeMeters;
        [FieldOffset(64)]
        public float3 ColorLinear;
        [FieldOffset(76)]
        public float Intensity;
        [FieldOffset(80)]
        public float3 Forward;
        [FieldOffset(92)]
        public float SpotCosine;
        [FieldOffset(96)]
        public float ShadowPhase01;
        [FieldOffset(100)]
        public float PowerFlicker01;
        [FieldOffset(104)]
        public float OxygenStress01;
        [FieldOffset(108)]
        public float LastUpdateUnscaledTime;
        [FieldOffset(112)]
        public uint Flags;
        [FieldOffset(116)]
        public byte Type;
        [FieldOffset(117)]
        public byte Lod;
        [FieldOffset(118)]
        private ushort _reserved;
        [FieldOffset(120)]
        private ulong _pad0;

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
                Forward = ProxyLightRegistry.ResolveDominantAxisOrDefault(forward, new float3(0f, 0f, 1f)),
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
        private const float ProxyLightMathQualityStart01 = 0.15f;
        private const float ProxyLightMathQualityFull01 = 0.85f;
        private const float ProxyLightMathFarDistanceSq = DistanceMath.HighQualityDistanceSq * 4f;
        // COLD ALLOC: fixed proxy-light registry arrays. O(128) scans are cheaper than persistent native ownership here.
        private static readonly ProxyLightData[] _lights = new ProxyLightData[MaxProxyLights];
        private static readonly int[] _keys = new int[MaxProxyLights];
        private static readonly int[] _freeProxyLightSlots = new int[MaxProxyLights];
        private static int _freeProxyLightSlotCount;
        private static int _keyCount;
        private static int _registeredCount;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

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

            ClearStorage();
            _initialized = true;
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
            int slot = FindSlotByKey(key);
            bool existed = slot >= 0;
            if (!existed)
            {
                if (!TryAcquireProxyLightSlot(out slot))
                    return false;

                _keys[slot] = key;
            }

            _lights[slot] = resolvedData;
            if (!existed)
                _registeredCount++;

            return true;
        }

        public static void Unregister(int key)
        {
            if (!IsInitialized || key == 0)
                return;

            int slot = FindSlotByKey(key);
            if (slot < 0)
                return;

            _keys[slot] = 0;
            _lights[slot] = default;
            ReleaseProxyLightSlot(slot);
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

            float qualityWeight01 = ResolveProxyLightQualityWeight01();
            float3 safeForward = NormalizeProxyLightVector(viewerForward, 0f, qualityWeight01, new float3(0f, 0f, 1f));
            bool useForwardGate = minimumForwardDot > -1f;
            float safeMaxDistance = math.isfinite(maxDistanceMeters) && maxDistanceMeters > 0f
                ? maxDistanceMeters
                : 0.01f;
            float maxDistanceSq = math.max(0.0001f, safeMaxDistance * safeMaxDistance);
            int visibleCount = 0;

            for (int i = 0; i < _keyCount && visibleCount < output.Length; i++)
            {
                int key = _keys[i];
                if (key == 0)
                    continue;

                ProxyLightData light = _lights[i];
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
                    float3 direction = NormalizeProxyLightVector(cameraRelative, distanceSq, qualityWeight01, safeForward);
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
            if (!IsInitialized || key == 0)
                return false;

            int slot = FindSlotByKey(key);
            if (slot < 0)
                return false;

            data = _lights[slot];
            return true;
        }

        public static void Clear()
        {
            if (!IsInitialized)
                return;

            ClearStorage();
        }

        public static void Shutdown()
        {
            ClearStorage();
            _initialized = false;
        }

        private static void ClearStorage()
        {
            for (int i = 0; i < _keyCount; i++)
            {
                _keys[i] = 0;
                _lights[i] = default;
            }

            for (int i = 0; i < _freeProxyLightSlotCount; i++)
                _freeProxyLightSlots[i] = 0;

            _freeProxyLightSlotCount = 0;
            _keyCount = 0;
            _registeredCount = 0;
        }

        private static int FindSlotByKey(int key)
        {
            for (int i = 0; i < _keyCount; i++)
            {
                if (_keys[i] == key)
                    return i;
            }

            return -1;
        }

        private static void ReleaseProxyLightSlot(int slot)
        {
            if ((uint)slot >= MaxProxyLights || _freeProxyLightSlotCount >= MaxProxyLights)
                return;

            _freeProxyLightSlots[_freeProxyLightSlotCount++] = slot;
        }

        private static bool TryAcquireProxyLightSlot(out int slot)
        {
            while (_freeProxyLightSlotCount > 0)
            {
                int recycledSlot = _freeProxyLightSlots[--_freeProxyLightSlotCount];
                _freeProxyLightSlots[_freeProxyLightSlotCount] = 0;
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
            float flickerWave = math.abs(FastTriangleSineSigned(phase) * FastTriangleSineSigned((phase * 0.37f) + 1.618f));
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

        private static float FastTriangleSineSigned(float radians)
        {
            float cycle = math.frac((radians * 0.159154943f) + 0.25f);
            return 1f - math.abs((cycle * 4f) - 2f);
        }

        internal static float3 ResolveDominantAxisOrDefault(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float3 absValue = math.abs(value);
            float maxAxis = math.cmax(absValue);
            if (maxAxis <= 0.000001f)
                return fallback;

            if (absValue.x >= absValue.y && absValue.x >= absValue.z)
                return new float3(value.x < 0f ? -1f : 1f, 0f, 0f);

            if (absValue.y >= absValue.z)
                return new float3(0f, value.y < 0f ? -1f : 1f, 0f);

            return new float3(0f, 0f, value.z < 0f ? -1f : 1f);
        }

        private static float ResolveProxyLightQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1f;
        }

        private static float ResolveProxyLightMathBlend01(float distanceSq, float qualityWeight01)
        {
            float safeDistanceSq = math.isfinite(distanceSq)
                ? math.max(0f, distanceSq)
                : ProxyLightMathFarDistanceSq;
            float nearWeight = 1f - math.saturate(safeDistanceSq * math.rcp(ProxyLightMathFarDistanceSq));
            float qualityWeight = math.smoothstep(
                ProxyLightMathQualityStart01,
                ProxyLightMathQualityFull01,
                math.saturate(qualityWeight01));
            return math.saturate(nearWeight * qualityWeight);
        }

        private static float3 NormalizeProxyLightVector(float3 value, float distanceSq, float qualityWeight01, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            float3 cheap = ResolveDominantAxisOrDefault(value, fallback);
            float blend = ResolveProxyLightMathBlend01(distanceSq, qualityWeight01);
            if (blend <= 0.0001f)
                return cheap;

            float3 exact = value * math.rsqrt(lengthSq);
            if (blend >= 0.9999f)
                return exact;

            float3 mixed = math.lerp(cheap, exact, blend);
            float mixedLengthSq = math.lengthsq(mixed);
            if (!math.isfinite(mixedLengthSq) || mixedLengthSq <= 0.000001f)
                return fallback;

            return mixed * math.rsqrt(mixedLengthSq);
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
            brownoutActive = supplyRatio < 0.85f || batterySnapshot.EmergencyReserveActive != 0;
            return true;
        }
    }
}
