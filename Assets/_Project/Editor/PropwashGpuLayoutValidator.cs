#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.VFX;
using Hecton8.VFX.Wakes;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class PropwashGpuLayoutValidator
    {
        static PropwashGpuLayoutValidator()
        {
            Validate();
        }

        [MenuItem("HECTON-8/Rendering/Validate Propwash GPU Layouts")]
        public static void Validate()
        {
            if (!PropwashGpuContracts.ValidateRuntimeLayouts())
                throw new InvalidOperationException("SHINOBU_237 propwash runtime layout mismatch.");

            AssertSize<PropwashEventDTO>(PropwashGpuContracts.EventStrideBytes);
            AssertOffset<PropwashEventDTO>(nameof(PropwashEventDTO.LocalPosition), 0);
            AssertOffset<PropwashEventDTO>(nameof(PropwashEventDTO.ThrustVector), 12);
            AssertOffset<PropwashEventDTO>(nameof(PropwashEventDTO.Intensity), 24);
            AssertOffset<PropwashEventDTO>(nameof(PropwashEventDTO.Radius), 28);
            AssertSize<KinematicWakeSourceDTO>(PropwashGpuContracts.KinematicSourceStrideBytes);
            AssertSize<WakeSource>(PropwashGpuContracts.WakeSourceStrideBytes);
            AssertSize<PropwashRingCursorDTO>(PropwashGpuContracts.RingCursorStrideBytes);
            AssertSize<PropwashTelemetryEntry>(PropwashGpuContracts.TelemetryEntryStrideBytes);
            AssertSize<PropwashGpuTuningDTO>(PropwashGpuContracts.TuningStrideBytes);
            AssertSize<PropwashWakeProfileDTO>(PropwashGpuContracts.WakeProfileStrideBytes);
            AssertOffset<PropwashWakeProfileDTO>(nameof(PropwashWakeProfileDTO.EngineHash), 0);
            AssertOffset<PropwashWakeProfileDTO>(nameof(PropwashWakeProfileDTO.EmissionRate), 8);
            AssertOffset<PropwashWakeProfileDTO>(nameof(PropwashWakeProfileDTO.CurlFrequency), 44);
            AssertOffset<PropwashWakeProfileDTO>(nameof(PropwashWakeProfileDTO.Reserved2), 60);
        }

        private static void AssertSize<T>(int expectedBytes) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException(typeof(T).Name + " size " + actualBytes + " != " + expectedBytes);
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset " + actualOffset + " != " + expectedOffset);
        }
    }
}
#endif
