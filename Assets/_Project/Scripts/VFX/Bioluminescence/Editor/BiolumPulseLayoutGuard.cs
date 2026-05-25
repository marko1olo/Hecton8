#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.VFX.Bioluminescence;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.VFX.Bioluminescence.Editor
{
    [InitializeOnLoad]
    internal static class BiolumPulseLayoutGuard
    {
        private const int GlowStateStrideBytes = 16;
        private const int SyncPulseStrideBytes = 32;
        private const int MockWeatherSignalStrideBytes = 16;
        private const int BiolumPulseStateStrideBytes = 64;
        private const int BiolumSpeciesTuningStrideBytes = 24;
        private const int MockPredatorProximityStrideBytes = 64;
        private const int MockCombatDamageStrideBytes = 64;
        private const int BiolumPulseTelemetryStrideBytes = 32;

        static BiolumPulseLayoutGuard()
        {
            ValidateOrThrow();
        }

        internal static void ValidateOrThrow()
        {
            AssertSize<GlowStateDTO>(GlowStateStrideBytes);
            AssertSize<SyncPulseDTO>(SyncPulseStrideBytes);
            AssertSize<MockWeatherSignal>(MockWeatherSignalStrideBytes);
            AssertSize<BiolumPulseStateDTO>(BiolumPulseStateStrideBytes);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group1_Params), 0);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group2_Params), 16);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group3_Params), 32);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group4_Params), 48);
            AssertSize<BiolumSpeciesTuningDTO>(BiolumSpeciesTuningStrideBytes);
            AssertSize<MockPredatorProximitySignal>(MockPredatorProximityStrideBytes);
            AssertSize<MockCombatDamageSignal>(MockCombatDamageStrideBytes);
            AssertSize<BiolumPulseSyncRuntime.BiolumPulseTelemetryEntry>(BiolumPulseTelemetryStrideBytes);
        }

        private static void AssertSize<T>(int expectedBytes) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException($"{typeof(T).Name} size changed: {actualBytes} bytes, expected {expectedBytes}.");
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffsetBytes) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"{typeof(T).Name}.{fieldName} is missing.");

            int actualOffsetBytes = UnsafeUtility.GetFieldOffset(field);
            if (actualOffsetBytes != expectedOffsetBytes)
                throw new InvalidOperationException($"{typeof(T).Name}.{fieldName} offset changed: {actualOffsetBytes}, expected {expectedOffsetBytes}.");
        }
    }
}
#endif
