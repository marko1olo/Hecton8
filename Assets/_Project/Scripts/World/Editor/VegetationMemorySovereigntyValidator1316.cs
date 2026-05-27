#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.World.Editor
{
    [InitializeOnLoad]
    public static class VegetationMemorySovereigntyValidator1316
    {
        private const uint FailureLayout = 1u << 0;

        static VegetationMemorySovereigntyValidator1316()
        {
            ValidateLayoutsOrThrow();
        }

        [MenuItem("HECTON-8/Vegetation/Run Memory Sovereignty Validator 1316")]
        public static void RunMenu()
        {
            ValidateLayoutsOrThrow();
            H8Debug.Log("[1316] Vegetation memory sovereignty layout validator passed.");
        }

        private static void ValidateLayoutsOrThrow()
        {
            uint failureFlags = 0u;

            AssertExplicit<VegetationMemoryTelemetryEntry>(
                VegetationMemorySovereigntyConstants.TelemetryEntryStrideBytes,
                ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.StateHash), 0, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.BufferId), 8, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Generation), 12, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Frame), 16, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.ExpectedLength), 20, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.ActualLength), 24, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.CulledInstances), 28, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.JobMicroseconds), 32, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.QualityWeight), 36, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.FailureCode), 40, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Phase), 42, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Flags), 44, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Position), 48, ref failureFlags);
            AssertOffset<VegetationMemoryTelemetryEntry>(nameof(VegetationMemoryTelemetryEntry.Reserved0), 60, ref failureFlags);

            AssertExplicit<VegetationMemoryHashPair>(
                VegetationMemorySovereigntyConstants.HashPairStrideBytes,
                ref failureFlags);
            AssertOffset<VegetationMemoryHashPair>(nameof(VegetationMemoryHashPair.Key), 0, ref failureFlags);
            AssertOffset<VegetationMemoryHashPair>(nameof(VegetationMemoryHashPair.Value), 4, ref failureFlags);

            AssertExplicit<VegetationMemoryCounter>(
                VegetationMemorySovereigntyConstants.CounterStrideBytes,
                ref failureFlags);
            AssertOffset<VegetationMemoryCounter>(nameof(VegetationMemoryCounter.Count), 0, ref failureFlags);
            AssertOffset<VegetationMemoryCounter>(nameof(VegetationMemoryCounter.Capacity), 4, ref failureFlags);
            AssertOffset<VegetationMemoryCounter>(nameof(VegetationMemoryCounter.Generation), 8, ref failureFlags);
            AssertOffset<VegetationMemoryCounter>(nameof(VegetationMemoryCounter.Flags), 12, ref failureFlags);

            Type chunkSliceMoveRecord = typeof(HectonMapMagicVegetationBridge).GetNestedType(
                "ChunkSliceMoveRecord",
                BindingFlags.NonPublic);
            AssertExplicit(chunkSliceMoveRecord, 16, ref failureFlags);
            AssertOffset(chunkSliceMoveRecord, "SourceOffset", 0, ref failureFlags);
            AssertOffset(chunkSliceMoveRecord, "DestinationOffset", 4, ref failureFlags);
            AssertOffset(chunkSliceMoveRecord, "Count", 8, ref failureFlags);

            Type activeAggregateCopyRecord = typeof(HectonMapMagicVegetationBridge).GetNestedType(
                "ActiveAggregateCopyRecord",
                BindingFlags.NonPublic);
            AssertExplicit(activeAggregateCopyRecord, 16, ref failureFlags);
            AssertOffset(activeAggregateCopyRecord, "SourceOffset", 0, ref failureFlags);
            AssertOffset(activeAggregateCopyRecord, "DestinationOffset", 4, ref failureFlags);
            AssertOffset(activeAggregateCopyRecord, "Count", 8, ref failureFlags);
            AssertOffset(activeAggregateCopyRecord, "PoolSet", 12, ref failureFlags);

            AssertExplicit<HectonMapMagicVegetationBridge.MegaWreckStreamSection>(
                64,
                ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("WreckId", 0, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("SectionSeed", 4, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("SectionX", 8, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("SectionZ", 12, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("WorldCenter", 16, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("WorldSize", 28, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("LocalCenter", 40, ref failureFlags);
            AssertOffset<HectonMapMagicVegetationBridge.MegaWreckStreamSection>("LocalSize", 52, ref failureFlags);

            Type predatorFearNodeSnapshot = typeof(HectonMapMagicVegetationBridge).GetNestedType(
                "PredatorFearNodeSnapshot",
                BindingFlags.NonPublic);
            AssertExplicit(predatorFearNodeSnapshot, 32, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "Position", 0, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "Radius", 12, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "Weight", 16, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "SpeciesId", 20, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "Padding", 24, ref failureFlags);
            AssertOffset(predatorFearNodeSnapshot, "_pad0", 28, ref failureFlags);

            if (failureFlags != 0u)
                throw new FatalArchitectureException("1316 vegetation memory DTO layout violation.");
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureFlags)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }

        private static void AssertExplicit(Type type, int expectedSize, ref uint failureFlags)
        {
            if (type == null)
            {
                failureFlags |= FailureLayout;
                return;
            }

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

        private static void AssertOffset(Type type, string fieldName, int expectedOffset, ref uint failureFlags)
        {
            if (type == null)
            {
                failureFlags |= FailureLayout;
                return;
            }

            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }
    }
}
#endif
