#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Editor-time byte layout validator for GPU boid structs mirrored into HLSL.
    /// Any mismatch here is treated as a hard failure before play mode or batch import continues.
    /// </summary>
    internal static class BoidStructValidator
    {
        private const string MenuPath = "Hecton/Validation/World/Validate Boid Struct Layout";
        private const string LogPrefix = "[BoidStructValidator]";
        private const int ExpectedBoidDataSizeBytes = 32;
        private const int ExpectedBoidDataMarshalSizeBytes = 32;
        private const int ExpectedBoidDataAlignmentBytes = 4;
        private const int ExpectedPositionOffsetBytes = 0;
        private const int ExpectedVelocityOffsetBytes = 12;
        private const int ExpectedPanicOffsetBytes = 24;
        private const int ExpectedStateFlagsOffsetBytes = 28;

        [MenuItem(MenuPath, priority = 193)]
        private static void RunFromMenu()
        {
            ValidateBoidDataLayout(logSummary: true, throwOnFailure: false);
        }

        internal static void RunBatchValidation()
        {
            ValidateBoidDataLayout(logSummary: true, throwOnFailure: true);
        }

        private static bool ValidateBoidDataLayout(bool logSummary, bool throwOnFailure)
        {
            int unsafeSize = UnsafeUtility.SizeOf<SargassumMicroFaunaBoids.BoidData>();
            int marshalSize = Marshal.SizeOf<SargassumMicroFaunaBoids.BoidData>();
            int alignment = UnsafeUtility.AlignOf<SargassumMicroFaunaBoids.BoidData>();
            int positionOffset = Marshal.OffsetOf<SargassumMicroFaunaBoids.BoidData>(nameof(SargassumMicroFaunaBoids.BoidData.Position)).ToInt32();
            int velocityOffset = Marshal.OffsetOf<SargassumMicroFaunaBoids.BoidData>(nameof(SargassumMicroFaunaBoids.BoidData.Velocity)).ToInt32();
            int panicOffset = Marshal.OffsetOf<SargassumMicroFaunaBoids.BoidData>(nameof(SargassumMicroFaunaBoids.BoidData.Panic)).ToInt32();
            int stateFlagsOffset = Marshal.OffsetOf<SargassumMicroFaunaBoids.BoidData>(nameof(SargassumMicroFaunaBoids.BoidData.StateFlags)).ToInt32();

            string failureMessage = null;
            ValidateExact(nameof(unsafeSize), unsafeSize, ExpectedBoidDataSizeBytes, ref failureMessage);
            ValidateExact(nameof(marshalSize), marshalSize, ExpectedBoidDataMarshalSizeBytes, ref failureMessage);
            ValidateExact(nameof(alignment), alignment, ExpectedBoidDataAlignmentBytes, ref failureMessage);
            ValidateExact(nameof(positionOffset), positionOffset, ExpectedPositionOffsetBytes, ref failureMessage);
            ValidateExact(nameof(velocityOffset), velocityOffset, ExpectedVelocityOffsetBytes, ref failureMessage);
            ValidateExact(nameof(panicOffset), panicOffset, ExpectedPanicOffsetBytes, ref failureMessage);
            ValidateExact(nameof(stateFlagsOffset), stateFlagsOffset, ExpectedStateFlagsOffsetBytes, ref failureMessage);

            if (failureMessage != null)
            {
                Debug.LogError(failureMessage);
                if (throwOnFailure)
                    throw new InvalidOperationException(failureMessage);

                return false;
            }

            if (logSummary)
            {
                Hecton8.Core.H8Debug.Log(
                    $"{LogPrefix} BoidData layout validated. size={unsafeSize}, marshalSize={marshalSize}, " +
                    $"alignment={alignment}, positionOffset={positionOffset}, velocityOffset={velocityOffset}, " +
                    $"panicOffset={panicOffset}, stateFlagsOffset={stateFlagsOffset}.");
            }

            return true;
        }

        private static void ValidateExact(string label, int actual, int expected, ref string failureMessage)
        {
            if (actual == expected || failureMessage != null)
                return;

            failureMessage =
                $"{LogPrefix} BoidData layout validation failed for '{label}'. " +
                $"actual={actual}, expected={expected}. HLSL contract is float3 Position, float3 Velocity, float Panic, uint StateFlags.";
        }
    }
}
#endif
