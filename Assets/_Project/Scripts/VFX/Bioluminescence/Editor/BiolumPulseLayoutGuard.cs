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
        static BiolumPulseLayoutGuard()
        {
            ValidateOrThrow();
        }

        internal static void ValidateOrThrow()
        {
            AssertSize<GlowStateDTO>(16);
            AssertSize<SyncPulseDTO>(32);
            AssertSize<MockWeatherSignal>(16);
            AssertSize<BiolumPulseStateDTO>(64);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group1_Params), 0);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group2_Params), 16);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group3_Params), 32);
            AssertOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group4_Params), 48);
            AssertSize<BiolumSpeciesTuningDTO>(24);
            AssertSize<MockPredatorProximitySignal>(64);
            AssertSize<MockCombatDamageSignal>(64);
            AssertSize<BiolumPulseSyncRuntime.BiolumPulseTelemetryEntry>(32);
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
