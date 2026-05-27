#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.Editor
{
    [InitializeOnLoad]
    internal static class ProceduralWreckGeneratorMemorySovereigntyValidator1328
    {
        private const string TargetPath = "Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs";
        private const string RegistryPath = "Assets/_Project/Scripts/World/WreckMaterialRegistry.cs";
        private const string MemoryContractPath = "Assets/_Project/Scripts/Core/Memory/H8Memory.cs";
        private const string ReportPath = "Docs/AgentLogs/Report_1328_WreckGenerator.json";
        private const uint FailureNativeFields = 1u << 0;
        private const uint FailureDumpRoute = 1u << 1;
        private const uint FailureVaultIds = 1u << 2;
        private const uint FailureLayout = 1u << 3;
        private const uint FailureWidePadding = 1u << 4;
        private const string ForbiddenPersistentNativePattern =
            @"private\s+(NativeArray|NativeList|NativeQueue|NativeParallel|UnsafeList|UnsafeHash|UnsafeQueue)[^;]+;";
        private const string ForbiddenWidePaddingPattern =
            @"\[FieldOffset\([0-9]+\)\]\s+private\s+(?:ushort|uint|ulong|short|int|long)\s+_(?:pad|reserved|runtimeReserved)[A-Za-z0-9_]*\s*;";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        static ProceduralWreckGeneratorMemorySovereigntyValidator1328()
        {
            ValidateLayoutsOrThrow();
        }

        [MenuItem("Hecton8/Validation/Agent 1328/Wreck Generator Memory Sovereignty")]
        private static void RunMenu()
        {
            bool passed = RunValidation(Application.dataPath);
            if (!passed)
                throw new FatalArchitectureException("1328 wreck generator memory sovereignty validator failed.");
        }

        internal static bool RunValidation(string dataPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(dataPath, ".."));
            string absoluteTarget = Path.Combine(projectRoot, TargetPath.Replace('/', Path.DirectorySeparatorChar));
            string absoluteRegistry = Path.Combine(projectRoot, RegistryPath.Replace('/', Path.DirectorySeparatorChar));
            string absoluteMemoryContract = Path.Combine(projectRoot, MemoryContractPath.Replace('/', Path.DirectorySeparatorChar));
            string absoluteReport = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absoluteTarget) ||
                !File.Exists(absoluteRegistry) ||
                !File.Exists(absoluteMemoryContract))
            {
                return false;
            }

            string source = File.ReadAllText(absoluteTarget, Utf8NoBom);
            string registrySource = File.ReadAllText(absoluteRegistry, Utf8NoBom);
            string memoryContractSource = File.ReadAllText(absoluteMemoryContract, Utf8NoBom);
            int forbiddenCount =
                Regex.Matches(source, ForbiddenPersistentNativePattern).Count +
                Regex.Matches(registrySource, ForbiddenPersistentNativePattern).Count;
            bool hasExpectedDumpPath = source.IndexOf("Docs/AgentLogs/Dump_1328_WreckGenerator.bin", StringComparison.Ordinal) >= 0;
            bool hasVaultIds =
                source.IndexOf("WreckGeneratorTelemetryEntriesBufferId = BufferID.WreckGeneratorTelemetryEntries", StringComparison.Ordinal) >= 0 &&
                registrySource.IndexOf("WreckBrgBatchMetadataBufferId = BufferID.WreckBrgBatchMetadata", StringComparison.Ordinal) >= 0 &&
                memoryContractSource.IndexOf("WreckGeneratorTelemetryEntries = 132812", StringComparison.Ordinal) >= 0 &&
                memoryContractSource.IndexOf("WreckBrgBatchMetadata = 132816", StringComparison.Ordinal) >= 0 &&
                (int)BufferID.WreckGeneratorTelemetryEntries == 132812 &&
                (int)BufferID.WreckBrgBatchMetadata == 132816;
            uint failureMask = ValidateLayouts();
            if (forbiddenCount != 0)
                failureMask |= FailureNativeFields;
            if (Regex.IsMatch(source, ForbiddenWidePaddingPattern) ||
                Regex.IsMatch(registrySource, ForbiddenWidePaddingPattern))
            {
                failureMask |= FailureWidePadding;
            }
            if (!hasExpectedDumpPath)
                failureMask |= FailureDumpRoute;
            if (!hasVaultIds)
                failureMask |= FailureVaultIds;

            string hash = ComputeSha256(absoluteTarget);
            string registryHash = ComputeSha256(absoluteRegistry);
            string memoryContractHash = ComputeSha256(absoluteMemoryContract);
            string report = BuildReportJson(hash, registryHash, memoryContractHash, forbiddenCount, hasExpectedDumpPath, hasVaultIds, failureMask);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReport));
            File.WriteAllText(absoluteReport, report, Utf8NoBom);
            return failureMask == 0u;
        }

        private static void ValidateLayoutsOrThrow()
        {
            uint failureMask = ValidateLayouts();
            if (failureMask != 0u)
                throw new FatalArchitectureException("1328 wreck generator DTO layout violation mask=" + failureMask);
        }

        private static uint ValidateLayouts()
        {
            uint failureMask = 0u;

            AssertExplicit<WreckGridCell>(16, ref failureMask);
            AssertOffset<WreckGridCell>(nameof(WreckGridCell.Entropy), 0, ref failureMask);
            AssertOffset<WreckGridCell>(nameof(WreckGridCell.PossibleModuleMask), 4, ref failureMask);
            AssertOffset<WreckGridCell>(nameof(WreckGridCell.CollapsedModuleId), 6, ref failureMask);
            AssertOffset<WreckGridCell>(nameof(WreckGridCell.SocketConstraints), 7, ref failureMask);

            AssertExplicit<WreckModuleRuntimeDefinition>(64, ref failureMask);
            AssertOffset<WreckModuleRuntimeDefinition>(nameof(WreckModuleRuntimeDefinition.BoundsCenter), 0, ref failureMask);
            AssertOffset<WreckModuleRuntimeDefinition>(nameof(WreckModuleRuntimeDefinition.BoundsSize), 12, ref failureMask);
            AssertOffset<WreckModuleRuntimeDefinition>(nameof(WreckModuleRuntimeDefinition.NorthSocket), 24, ref failureMask);
            AssertOffset<WreckModuleRuntimeDefinition>(nameof(WreckModuleRuntimeDefinition.DrawCallPriority), 40, ref failureMask);

            AssertExplicit<WreckModulePlacement>(64, ref failureMask);
            AssertOffset<WreckModulePlacement>(nameof(WreckModulePlacement.Rotation), 0, ref failureMask);
            AssertOffset<WreckModulePlacement>(nameof(WreckModulePlacement.Position), 16, ref failureMask);
            AssertOffset<WreckModulePlacement>(nameof(WreckModulePlacement.MortonIndex), 52, ref failureMask);
            AssertOffset<WreckModulePlacement>(nameof(WreckModulePlacement.ModuleId), 56, ref failureMask);

            AssertExplicit<WreckMergedVertex>(64, ref failureMask);
            AssertOffset<WreckMergedVertex>(nameof(WreckMergedVertex.Position), 0, ref failureMask);
            AssertOffset<WreckMergedVertex>(nameof(WreckMergedVertex.Normal), 12, ref failureMask);
            AssertOffset<WreckMergedVertex>(nameof(WreckMergedVertex.UV), 24, ref failureMask);
            AssertOffset<WreckMergedVertex>(nameof(WreckMergedVertex.Color), 32, ref failureMask);

            AssertExplicit<WreckLootRecord>(64, ref failureMask);
            AssertOffset<WreckLootRecord>(nameof(WreckLootRecord.ItemHashId), 0, ref failureMask);
            AssertOffset<WreckLootRecord>(nameof(WreckLootRecord.StableDropHash), 4, ref failureMask);
            AssertOffset<WreckLootRecord>(nameof(WreckLootRecord.Flags), 8, ref failureMask);
            AssertOffset<WreckLootRecord>(nameof(WreckLootRecord.MinQuantity), 12, ref failureMask);

            AssertExplicit<WreckDebrisRecord>(64, ref failureMask);
            AssertOffset<WreckDebrisRecord>(nameof(WreckDebrisRecord.Position), 0, ref failureMask);
            AssertOffset<WreckDebrisRecord>(nameof(WreckDebrisRecord.StableId), 32, ref failureMask);
            AssertOffset<WreckDebrisRecord>(nameof(WreckDebrisRecord.Quantity), 44, ref failureMask);
            AssertOffset<WreckDebrisRecord>(nameof(WreckDebrisRecord.Flags), 46, ref failureMask);

            AssertExplicit<WreckDebrisCluster>(64, ref failureMask);
            AssertOffset<WreckDebrisCluster>(nameof(WreckDebrisCluster.Center), 0, ref failureMask);
            AssertOffset<WreckDebrisCluster>(nameof(WreckDebrisCluster.Extents), 12, ref failureMask);
            AssertOffset<WreckDebrisCluster>(nameof(WreckDebrisCluster.Visible), 32, ref failureMask);

            AssertExplicit<WreckArtifactRecord>(64, ref failureMask);
            AssertOffset<WreckArtifactRecord>(nameof(WreckArtifactRecord.EntryHash), 0, ref failureMask);
            AssertOffset<WreckArtifactRecord>(nameof(WreckArtifactRecord.Position), 4, ref failureMask);
            AssertOffset<WreckArtifactRecord>(nameof(WreckArtifactRecord.StableId), 20, ref failureMask);
            AssertOffset<WreckArtifactRecord>(nameof(WreckArtifactRecord.State), 30, ref failureMask);

            AssertExplicit<WreckScorchDecalRecord>(64, ref failureMask);
            AssertOffset<WreckScorchDecalRecord>(nameof(WreckScorchDecalRecord.Position), 0, ref failureMask);
            AssertOffset<WreckScorchDecalRecord>(nameof(WreckScorchDecalRecord.StableId), 32, ref failureMask);
            AssertOffset<WreckScorchDecalRecord>(nameof(WreckScorchDecalRecord.ModuleId), 36, ref failureMask);

            AssertExplicit<WreckBurialCutRecord>(64, ref failureMask);
            AssertOffset<WreckBurialCutRecord>(nameof(WreckBurialCutRecord.AbsoluteCenter), 0, ref failureMask);
            AssertOffset<WreckBurialCutRecord>(nameof(WreckBurialCutRecord.HalfExtents), 24, ref failureMask);
            AssertOffset<WreckBurialCutRecord>(nameof(WreckBurialCutRecord.StableId), 40, ref failureMask);
            AssertOffset<WreckBurialCutRecord>(nameof(WreckBurialCutRecord.MaterialId), 44, ref failureMask);

            AssertExplicit<WreckTelemetryEntry>(64, ref failureMask);
            AssertOffset<WreckTelemetryEntry>(nameof(WreckTelemetryEntry.FrameIndex), 0, ref failureMask);
            AssertOffset<WreckTelemetryEntry>(nameof(WreckTelemetryEntry.Position), 16, ref failureMask);
            AssertOffset<WreckTelemetryEntry>(nameof(WreckTelemetryEntry.Value1), 48, ref failureMask);

            return failureMask;
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureMask)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureMask |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureMask)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureMask |= FailureLayout;
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("X2"));

            return builder.ToString();
        }

        private static string BuildReportJson(
            string hash,
            string registryHash,
            string memoryContractHash,
            int forbiddenCount,
            bool hasExpectedDumpPath,
            bool hasVaultIds,
            uint failureMask)
        {
            return "{\n" +
                   "  \"agentId\": 1328,\n" +
                   "  \"target\": \"Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs\",\n" +
                   "  \"targetSha256\": \"" + hash + "\",\n" +
                   "  \"registry\": \"Assets/_Project/Scripts/World/WreckMaterialRegistry.cs\",\n" +
                   "  \"registrySha256\": \"" + registryHash + "\",\n" +
                   "  \"memoryContract\": \"Assets/_Project/Scripts/Core/Memory/H8Memory.cs\",\n" +
                   "  \"memoryContractSha256\": \"" + memoryContractHash + "\",\n" +
                   "  \"postMigrationPersistentNativeFieldPatternHits\": " + forbiddenCount + ",\n" +
                   "  \"hasExpectedDumpPath\": " + ToJsonBool(hasExpectedDumpPath) + ",\n" +
                   "  \"hasVaultBufferIds\": " + ToJsonBool(hasVaultIds) + ",\n" +
                   "  \"layoutFailureMask\": " + failureMask + ",\n" +
                   "  \"compileStatus\": \"not executed by validator\",\n" +
                   "  \"runtimeProofStatus\": \"not claimed\"\n" +
                   "}\n";
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
#endif
