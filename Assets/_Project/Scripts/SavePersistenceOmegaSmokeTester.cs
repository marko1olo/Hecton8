#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Dev
{
    public static class SavePersistenceOmegaSmokeTester
    {
        private const string ArtifactRelativePath = "CodexArtifacts/save-persistence-omega-smoke.json";
        private const string NativeMemoryOwner = nameof(SavePersistenceOmegaSmokeTester);
        private const string BoundsProbeLabel = "omegaBoundsProbes";
        private const string BoundsProbeResultsLabel = "omegaBoundsProbeResults";

        [MenuItem("Hecton8/Dev/Run Save Persistence Omega Smoke")]
        private static void RunMenuSmokeTest()
        {
            RunSmokeAndWriteArtifact();
        }

        public static void RunBatchModeSmokeTest()
        {
            bool pass = RunSmokeAndWriteArtifact();
            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool RunSmokeAndWriteArtifact()
        {
            bool subtractionBoundsValidPass = SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                128L,
                32,
                64L,
                160L);
            bool subtractionBoundsOverflowPass = !SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                long.MaxValue - 4L,
                16,
                64L,
                long.MaxValue);
            bool modSectorPrefixPass = HasModPayloadPrefix(SaveBinaryStorage.ComputeModPayloadSectorHash(0xC0DEC0DEu, 12345L));
            bool burstSubtractionBoundsStressPass = TryRunBurstSubtractionBoundsStress();
            bool nativeSentinelSourceAuditPass = TryRunNativeSentinelSourceAudit();
            bool runtimeBarrierSourceAuditPass = TryRunRuntimeBarrierSourceAudit();
            bool staticResidueSourceAuditPass = TryRunStaticResidueSourceAudit();
            bool purgeSourceAuditPass = TryRunPurgeSourceAudit();
            bool hotPathStringSourceAuditPass = TryRunHotPathStringSourceAudit();
            bool indexedBoundsDecompositionAuditPass = TryRunIndexedBoundsDecompositionAudit();
            bool pass = subtractionBoundsValidPass &&
                        subtractionBoundsOverflowPass &&
                        modSectorPrefixPass &&
                        burstSubtractionBoundsStressPass &&
                        nativeSentinelSourceAuditPass &&
                        runtimeBarrierSourceAuditPass &&
                        staticResidueSourceAuditPass &&
                        purgeSourceAuditPass &&
                        hotPathStringSourceAuditPass &&
                        indexedBoundsDecompositionAuditPass;

            WriteArtifact(
                pass,
                subtractionBoundsValidPass,
                subtractionBoundsOverflowPass,
                modSectorPrefixPass,
                burstSubtractionBoundsStressPass,
                nativeSentinelSourceAuditPass,
                runtimeBarrierSourceAuditPass,
                staticResidueSourceAuditPass,
                purgeSourceAuditPass,
                hotPathStringSourceAuditPass,
                indexedBoundsDecompositionAuditPass);

            if (pass)
                Debug.Log("[SavePersistenceOmegaSmokeTester] PASS artifact=CodexArtifacts/save-persistence-omega-smoke.json");
            else
                Debug.LogError("[SavePersistenceOmegaSmokeTester] FAIL artifact=CodexArtifacts/save-persistence-omega-smoke.json");

            return pass;
        }

        private static bool TryRunBurstSubtractionBoundsStress()
        {
            NativeArray<IndexedSectorBoundsProbe> probes = default;
            NativeArray<byte> results = default;

            try
            {
                probes = new NativeArray<IndexedSectorBoundsProbe>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                results = new NativeArray<byte>(probes.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(probes, NativeMemoryOwner, BoundsProbeLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(results, NativeMemoryOwner, BoundsProbeResultsLabel, NativeAllocationLifetime.TempJob);

                probes[0] = new IndexedSectorBoundsProbe { ByteOffset = 128L, CompressedSize = 32, MinimumByteOffset = 64L, FileLength = 160L, ExpectedValid = 1 };
                probes[1] = new IndexedSectorBoundsProbe { ByteOffset = long.MaxValue - 4L, CompressedSize = 16, MinimumByteOffset = 64L, FileLength = long.MaxValue, ExpectedValid = 0 };
                probes[2] = new IndexedSectorBoundsProbe { ByteOffset = 4096L, CompressedSize = 1024, MinimumByteOffset = 4096L, FileLength = 5120L, ExpectedValid = 1 };
                probes[3] = new IndexedSectorBoundsProbe { ByteOffset = 4096L, CompressedSize = 0, MinimumByteOffset = 4096L, FileLength = 5120L, ExpectedValid = 0 };
                probes[4] = new IndexedSectorBoundsProbe { ByteOffset = 4096L, CompressedSize = -1, MinimumByteOffset = 4096L, FileLength = 5120L, ExpectedValid = 0 };
                probes[5] = new IndexedSectorBoundsProbe { ByteOffset = 63L, CompressedSize = 1, MinimumByteOffset = 64L, FileLength = 5120L, ExpectedValid = 0 };
                probes[6] = new IndexedSectorBoundsProbe { ByteOffset = 4096L, CompressedSize = 512, MinimumByteOffset = 4096L, FileLength = 4095L, ExpectedValid = 0 };
                probes[7] = new IndexedSectorBoundsProbe { ByteOffset = long.MaxValue - 8192L, CompressedSize = 4096, MinimumByteOffset = 64L, FileLength = long.MaxValue - 4096L, ExpectedValid = 1 };

                JobHandle handle = new ValidateIndexedSectorBoundsProbeJob
                {
                    Probes = probes,
                    Results = results
                }.Schedule(probes.Length, 4);
                JobHandle.ScheduleBatchedJobs();
                // COLD SYNC JOB: editor-only smoke waits for a deterministic artifact; runtime save paths must not Complete here.
                handle.Complete();

                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] != 1)
                        return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[SavePersistenceOmegaSmokeTester] Burst bounds stress failed: " + exception.Message);
                return false;
            }
            finally
            {
                if (results.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(results);
                    results.Dispose();
                }

                if (probes.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(probes);
                    probes.Dispose();
                }
            }
        }

        private static bool TryRunNativeSentinelSourceAudit()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string persistentRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string smokeTester = ReadProjectFile("Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs");

            return ContainsAll(
                       saveManager,
                       "RegisterTransientNativeArray(packedQuestStateSnapshot",
                       "DisposeNativeArray(ref packedQuestStateSnapshot",
                       "NativeAllocationLifetime.TransientArena") &&
                   ContainsAll(
                       saveBinaryStorage,
                       "RegisterNativeMemorySentinel();",
                       "UnregisterNativeMemorySentinel();",
                       "NativeAllocationLifetime.TempJob") &&
                   ContainsNone(
                       saveBinaryStorage,
                       "EntityStateWrite" + "Dictionary" + "ScratchLabel",
                       "Dictionary" + "Scratch") &&
                   ContainsAll(
                       persistentRegistry,
                       "IndexedSectorPagingDesiredHashesLabel",
                       "IndexedSectorPagingLoadedRecordsLabel",
                       "NativeMemorySentinel.RegisterNativeArray",
                       "NativeMemorySentinel.RegisterNativeList",
                       "NativeAllocationLifetime.TransientArena") &&
                   ContainsAll(
                       smokeTester,
                       "NativeMemorySentinel.RegisterNativeArray(probes",
                       "NativeMemorySentinel.RegisterNativeArray(results",
                       "NativeMemorySentinel.UnregisterNativeArray(probes",
                       "NativeMemorySentinel.UnregisterNativeArray(results");
        }

        private static bool TryRunRuntimeBarrierSourceAudit()
        {
            return ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs"), ".Complete(", ".Run(") &&
                   ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs"), ".Complete(", ".Run(") &&
                   ContainsNone(ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"), ".Complete(", ".Run(") &&
                   ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs"), ".Complete(", ".Run(");
        }

        private static bool TryRunStaticResidueSourceAudit()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string persistentRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string worldGenerator = ReadProjectFile("Assets/_Project/Scripts/HectonWorldGenerator.cs");
            string globalRegistry = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistry.cs");

            return ContainsNone(saveManager, "DontDestroyOnLoad", "ActiveRuntimeInstance", "private static SaveManager", "static SaveManager _instance") &&
                   ContainsNone(persistentRegistry, "DontDestroyOnLoad", "private static PersistentWorldRegistry", "static PersistentWorldRegistry _instance") &&
                   ContainsNone(worldGenerator, "DontDestroyOnLoad", "ActiveRuntimeInstance") &&
                   ContainsAll(globalRegistry, "WorldSeedProvider", "RegisterWorldSeedProvider", "UnregisterWorldSeedProvider");
        }

        private static bool TryRunPurgeSourceAudit()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");

            return ContainsNone(
                saveManager,
                "TrySchedule" + "Predictive" + "IndexedSectorPrewarm",
                "Run" + "Predictive" + "IndexedSectorPrewarmAsync",
                "RunIndexedSaveDefragAsync",
                "WaitForIndexedSaveMaintenanceIdleAsync") &&
                   ContainsNone(
                       saveBinaryStorage,
                       "TryDefragment" + "IndexedPersistentWorldSectors",
                       "TryPrewarm" + "IndexedPersistentWorldSector",
                       "LZ4" + "CompressHigh",
                       "ComputeIndexed" + "SectorChecksum(");
        }

        private static bool TryRunHotPathStringSourceAudit()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string persistentRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");

            return MethodBodyContainsNone(saveManager, "public void Tick(float deltaTime)", "$\"", ".ToString(", "string.Format", "Debug.Log") &&
                   MethodBodyContainsNone(persistentRegistry, "public void Tick(float dt)", "$\"", ".ToString(", "string.Format", "Debug.Log") &&
                   MethodBodyContainsNone(persistentRegistry, "public void LateFrameTick()", "$\"", ".ToString(", "string.Format", "Debug.Log") &&
                   MethodBodyContainsNone(persistentRegistry, "public void SlowTick()", "$\"", ".ToString(", "string.Format", "Debug.Log");
        }

        private static bool TryRunIndexedBoundsDecompositionAudit()
        {
            string boundsMath = ReadProjectFile("Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");

            return ContainsAll(
                       boundsMath,
                       "internal static class SaveIndexedSectorBoundsMath",
                       "byteOffset <= fileLength - compressedSize",
                       "[BurstCompile]",
                       "ValidateIndexedSectorBoundsProbeJob") &&
                   ContainsAll(
                       saveBinaryStorage,
                       "SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds");
        }

        private static bool HasModPayloadPrefix(long sectorHash)
        {
            const ulong prefix = 0x4D50000000000000UL;
            const ulong mask = 0xFFFF000000000000UL;
            return (((ulong)sectorHash) & mask) == prefix;
        }

        private static void WriteArtifact(
            bool pass,
            bool subtractionBoundsValidPass,
            bool subtractionBoundsOverflowPass,
            bool modSectorPrefixPass,
            bool burstSubtractionBoundsStressPass,
            bool nativeSentinelSourceAuditPass,
            bool runtimeBarrierSourceAuditPass,
            bool staticResidueSourceAuditPass,
            bool purgeSourceAuditPass,
            bool hotPathStringSourceAuditPass,
            bool indexedBoundsDecompositionAuditPass)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string absolutePath = Path.Combine(projectRoot, ArtifactRelativePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(1024);
            builder.Append('{');
            AppendJsonField(builder, "tester", "SavePersistenceOmegaSmokeTester", trailingComma: true);
            AppendJsonField(builder, "utc", DateTime.UtcNow.ToString("O"), trailingComma: true);
            AppendJsonField(builder, "pass", pass, trailingComma: true);
            AppendJsonField(builder, "subtractionBoundsValidPass", subtractionBoundsValidPass, trailingComma: true);
            AppendJsonField(builder, "subtractionBoundsOverflowPass", subtractionBoundsOverflowPass, trailingComma: true);
            AppendJsonField(builder, "modSectorPrefixPass", modSectorPrefixPass, trailingComma: true);
            AppendJsonField(builder, "burstSubtractionBoundsStressPass", burstSubtractionBoundsStressPass, trailingComma: true);
            AppendJsonField(builder, "nativeSentinelSourceAuditPass", nativeSentinelSourceAuditPass, trailingComma: true);
            AppendJsonField(builder, "runtimeBarrierSourceAuditPass", runtimeBarrierSourceAuditPass, trailingComma: true);
            AppendJsonField(builder, "staticResidueSourceAuditPass", staticResidueSourceAuditPass, trailingComma: true);
            AppendJsonField(builder, "purgeSourceAuditPass", purgeSourceAuditPass, trailingComma: true);
            AppendJsonField(builder, "hotPathStringSourceAuditPass", hotPathStringSourceAuditPass, trailingComma: true);
            AppendJsonField(builder, "indexedBoundsDecompositionAuditPass", indexedBoundsDecompositionAuditPass, trailingComma: true);
            AppendJsonField(builder, "burstBoundsProbeCount", 8, trailingComma: false);
            builder.Append('}');
            File.WriteAllText(absolutePath, builder.ToString());
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }

        private static bool ContainsAll(string source, params string[] needles)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            for (int i = 0; i < needles.Length; i++)
            {
                if (!source.Contains(needles[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool ContainsNone(string source, params string[] needles)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            for (int i = 0; i < needles.Length; i++)
            {
                if (source.Contains(needles[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool MethodBodyContainsNone(string source, string signature, params string[] needles)
        {
            return TryExtractMethodBody(source, signature, out string methodBody) &&
                   ContainsNone(methodBody, needles);
        }

        private static bool TryExtractMethodBody(string source, string signature, out string methodBody)
        {
            methodBody = string.Empty;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(signature))
                return false;

            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return false;

            int openBraceIndex = source.IndexOf('{', signatureIndex);
            if (openBraceIndex < 0)
                return false;

            int depth = 0;
            for (int i = openBraceIndex; i < source.Length; i++)
            {
                char current = source[i];
                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current != '}')
                    continue;

                depth--;
                if (depth != 0)
                    continue;

                methodBody = source.Substring(openBraceIndex, i - openBraceIndex + 1);
                return true;
            }

            return false;
        }

        private static void AppendJsonField(StringBuilder builder, string key, string value, bool trailingComma)
        {
            builder.Append('"').Append(key).Append("\":\"").Append(value).Append('"');
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonField(StringBuilder builder, string key, bool value, bool trailingComma)
        {
            builder.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonField(StringBuilder builder, string key, long value, bool trailingComma)
        {
            builder.Append('"').Append(key).Append("\":").Append(value);
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonField(StringBuilder builder, string key, int value, bool trailingComma)
        {
            builder.Append('"').Append(key).Append("\":").Append(value);
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonVector(StringBuilder builder, string key, int3 value, bool trailingComma)
        {
            builder.Append('"').Append(key).Append("\":[")
                .Append(value.x).Append(',')
                .Append(value.y).Append(',')
                .Append(value.z).Append(']');
            if (trailingComma)
                builder.Append(',');
        }
    }
}
#endif
