#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Audio.Editor
{
    [InitializeOnLoad]
    public static class AudioMemorySovereigntyValidator1320
    {
        private const uint FailureLayout = 1u << 0;
        private const BindingFlags NestedFlags = BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static AudioMemorySovereigntyValidator1320()
        {
            ValidateLayoutsOrThrow();
        }

        [MenuItem("HECTON-8/Audio/Run Memory Sovereignty Validator 1320")]
        public static void RunMenu()
        {
            ValidateLayoutsOrThrow();
            H8Debug.Log("[1320] Procedural audio memory sovereignty validator passed.");
        }

        public static void ValidateLayoutsOrThrow()
        {
            uint failureFlags = 0u;

            AssertExplicit("GranularAudioTelemetryEntry", 64, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "SampleIndex", 0, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "Stress01", 4, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "StressDerivative01", 8, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "Depth01", 12, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "Impact01", 16, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "MixedSample", 20, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "PeakImpactEnergyJoules", 24, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "ActiveVoices", 28, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "VoiceLimit", 32, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "ActiveEchoTaps", 36, ref failureFlags);
            AssertOffset("GranularAudioTelemetryEntry", "Flags", 40, ref failureFlags);
            AssertPaddingRange("GranularAudioTelemetryEntry", 44, 20, ref failureFlags);

            AssertExplicit("PrologueAudioTransitionTelemetryEntry", 64, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Frame", 0, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Sequence", 4, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "DspFlags", 8, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "UniverseVelocityMetersPerSecond", 12, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Heat01", 16, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "LowPassCutoffHz", 20, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "LfeGain01", 24, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "GranularStress01", 28, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "SplashdownGain01", 32, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "PortalBlend01", 36, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "AudioLowPassCutoffHz", 40, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "SplashdownSamplesRemaining", 44, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Stage", 48, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Flags", 49, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "QualityTier", 50, ref failureFlags);
            AssertOffset("PrologueAudioTransitionTelemetryEntry", "Reserved", 51, ref failureFlags);
            AssertPaddingRange("PrologueAudioTransitionTelemetryEntry", 52, 12, ref failureFlags);

            AssertExplicit("AudioSynthesisTelemetryEntry", 64, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "StopwatchTicks", 0, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "Frame", 8, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "BufferId", 12, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "SystemId", 16, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "ExpectedGeneration", 20, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "ActualGeneration", 24, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "Flags", 28, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "ActivePolyphony", 32, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "VoiceLimit", 36, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "DspMicroseconds", 40, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "GlobalQualityWeight", 44, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "FailureCode", 48, ref failureFlags);
            AssertOffset("AudioSynthesisTelemetryEntry", "UnderrunCount", 52, ref failureFlags);
            AssertPaddingRange("AudioSynthesisTelemetryEntry", 56, 8, ref failureFlags);

            AssertExplicit("AudioParameterSnapshotCacheLinePad", 64, ref failureFlags);
            AssertOffset("AudioParameterSnapshotCacheLinePad", "_frontFence", 0, ref failureFlags);
            AssertOffset("AudioParameterSnapshotCacheLinePad", "_rearFence", 8, ref failureFlags);
            AssertPaddingRange("AudioParameterSnapshotCacheLinePad", 16, 48, ref failureFlags);

            if (failureFlags != 0u)
                throw new FatalArchitectureException("1320 procedural audio DTO layout violation.");
        }

        private static void AssertExplicit(string nestedTypeName, int expectedSize, ref uint failureFlags)
        {
            Type type = ResolveNestedType(nestedTypeName);
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            int size = Marshal.SizeOf(type);
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset(string nestedTypeName, string fieldName, int expectedOffset, ref uint failureFlags)
        {
            Type type = ResolveNestedType(nestedTypeName);
            FieldInfo field = type.GetField(fieldName, FieldFlags);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }

        private static void AssertPaddingRange(string nestedTypeName, int firstOffset, int count, ref uint failureFlags)
        {
            for (int i = 0; i < count; i++)
                AssertOffset(nestedTypeName, "_pad" + i, firstOffset + i, ref failureFlags);
        }

        private static Type ResolveNestedType(string nestedTypeName)
        {
            return typeof(PlayerCriticalProceduralAudioRenderer).GetNestedType(nestedTypeName, NestedFlags) ??
                   throw new FatalArchitectureException("1320 missing procedural audio nested DTO: " + nestedTypeName);
        }
    }
}
#endif
