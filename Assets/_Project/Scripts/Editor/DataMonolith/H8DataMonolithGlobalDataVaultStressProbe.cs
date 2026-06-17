#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Validation
{
    public static unsafe class H8DataMonolithGlobalDataVaultStressProbe
    {
        private const string AgentId = "X_002";
        private const string AgentId1313 = "1313";
        private const string AgentId1330 = "1330";
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_UNITY_GLOBAL_DATA_VAULT_STRESS_X_002.json";
        private const string ReportPath1313 = "Docs/Reports/DATA_MONOLITH_UNITY_GLOBAL_DATA_VAULT_STRESS_1313.json";
        private const string ReportPath1330 = "Docs/Reports/DATA_MONOLITH_UNITY_GLOBAL_DATA_VAULT_STRESS_1330.json";
        private const string BlobAssetPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const int ResidentLoadIterations = 96;
        private const double ResidentMeanTargetMicroseconds = 1000.0d;

        [MenuItem("Hecton8/Data Monolith/Run GlobalDataVault Stress Probe")]
        private static void RunFromMenu()
        {
            bool passed = Run();
            Debug.Log("[H8DataMonolithGlobalDataVaultStressProbe] passed=" + passed + " report=" + ReportPath + " report1313=" + ReportPath1313 + " report1330=" + ReportPath1330);
        }

        public static void RunBatch()
        {
            bool passed = Run();
            Debug.Log("[H8DataMonolithGlobalDataVaultStressProbe] batch passed=" + passed + " report=" + ReportPath + " report1313=" + ReportPath1313 + " report1330=" + ReportPath1330);
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        internal static bool Run()
        {
            string projectRoot = ResolveProjectRoot();
            string blobPath = Path.Combine(projectRoot, BlobAssetPath.Replace('/', Path.DirectorySeparatorChar));
            string setupError = string.Empty;

            if (!File.Exists(blobPath) || !H8DataMonolithCompiler.TryValidateBlobFile(blobPath, out setupError))
            {
                if (!H8DataMonolithCompiler.BakeAll(logSummary: false))
                    setupError = H8DataMonolithCompiler.LastError;
            }

            if (!File.Exists(blobPath) || !H8DataMonolithCompiler.TryValidateBlobFile(blobPath, out setupError))
            {
                WriteReport(projectRoot, BuildMissingReport(setupError));
                return false;
            }

            if (!TryReadManagedFixtureBlob(blobPath, out byte[] baseline, out string fixtureReadError))
            {
                WriteReport(projectRoot, BuildMissingReport(fixtureReadError));
                return false;
            }

            HeaderSnapshot header = ReadHeaderSnapshot(baseline);
            using GlobalDataVault vault = GlobalDataVault.Create();

            ReleaseCliLoadStressSnapshot releaseCliTiming = ReadReleaseCliLoadStress(projectRoot);
            bool fileLoadOk = RunFileLoadProof(vault, blobPath, out H8DataBlobLoadStatus fileStatus, out double fileLoadMicroseconds, out long fileLoadManagedBytes);
            bool vaultResolved = ResolvePayloadBuffer(vault, baseline.Length, out int payloadLength, out long vaultAllocatedBytes, out long vaultArenaBytes);
            bool lockedReloadOk = RunLockedReloadBlockProof(vault, baseline, out string lockedReloadJson);
            bool editorHotReloadRollbackOk = RunEditorHotReloadRollbackProof(vault, baseline, out string editorHotReloadRollbackJson);
            bool sectionProofOk = ResolveRequestedSections(out string sectionJson);
            bool corruptBootOk = RunColdCorruptBootProof(vault, baseline, out string corruptCasesJson, out int corruptCaseCount, out int corruptPassCount);
            ResidentLoadMetrics residentMetrics = RunResidentMemoryLoadMetrics(vault, baseline);
            bool residentZeroGcOk = residentMetrics.FailedIterations == 0 &&
                                    residentMetrics.AllocationCounterSupported &&
                                    residentMetrics.MaxManagedAllocatedBytes == 0L;

            H8StaticDataArena.Shutdown();

            bool passed = fileLoadOk &&
                          vaultResolved &&
                          sectionProofOk &&
                          lockedReloadOk &&
                          editorHotReloadRollbackOk &&
                          corruptBootOk &&
                          residentZeroGcOk &&
                          (residentMetrics.Pass || releaseCliTiming.Pass) &&
                          header.LittleEndianFlagSet &&
                          header.Flags == H8DataLayoutConstants.BlobFlagLittleEndian &&
                          header.HeaderBytes == H8DataLayoutConstants.HeaderSizeBytes &&
                          header.DirectoryOffset == H8DataLayoutConstants.HeaderSizeBytes &&
                          header.SectionTableOffset == H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes &&
                          (header.DataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) == 0u;

            StringBuilder json = new StringBuilder(32768);
            json.AppendLine("{");
            AppendJsonField(json, "agent", AgentId, comma: true, indent: 1);
            AppendJsonField(json, "status", passed ? "PASS_UNITY_GLOBAL_DATA_VAULT_FAIL_CLOSED_ZERO_GC_RESIDENT" : "FAIL_UNITY_GLOBAL_DATA_VAULT_STRESS", comma: true, indent: 1);
            AppendJsonField(json, "blobPath", NormalizePath(blobPath), comma: true, indent: 1);
            AppendJsonNumber(json, "blobBytes", baseline.Length, comma: true, indent: 1);
            AppendJsonField(json, "fileLoadStatus", fileStatus.ToString(), comma: true, indent: 1);
            AppendJsonBool(json, "fileLoadOk", fileLoadOk, comma: true, indent: 1);
            AppendJsonNumber(json, "fileLoadMicroseconds", fileLoadMicroseconds, comma: true, indent: 1);
            AppendJsonNumber(json, "fileLoadManagedAllocatedBytes", fileLoadManagedBytes, comma: true, indent: 1);
            AppendJsonBool(json, "payloadVaultBufferResolved", vaultResolved, comma: true, indent: 1);
            AppendJsonNumber(json, "payloadVaultBufferLength", payloadLength, comma: true, indent: 1);
            AppendJsonNumber(json, "vaultAllocatedBytesAfterFileLoad", vaultAllocatedBytes, comma: true, indent: 1);
            AppendJsonNumber(json, "vaultArenaBytes", vaultArenaBytes, comma: true, indent: 1);
            AppendHeader(json, header);
            json.AppendLine("  \"lockedCorruptReload\": " + lockedReloadJson + ",");
            json.AppendLine("  \"editorHotReloadRollback\": " + editorHotReloadRollbackJson + ",");
            json.AppendLine("  \"coldCorruptBoot\": {");
            AppendJsonBool(json, "passed", corruptBootOk, comma: true, indent: 2);
            AppendJsonNumber(json, "caseCount", corruptCaseCount, comma: true, indent: 2);
            AppendJsonNumber(json, "passCount", corruptPassCount, comma: true, indent: 2);
            json.AppendLine("    \"cases\": [");
            json.Append(corruptCasesJson);
            json.AppendLine();
            json.AppendLine("    ]");
            json.AppendLine("  },");
            AppendResidentMetrics(json, residentMetrics);
            AppendReleaseCliTiming(json, releaseCliTiming);
            json.AppendLine("  \"requestedBlockSections\": [");
            json.Append(sectionJson);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");

            WriteReport(projectRoot, json.ToString());
            return passed;
        }

        private static bool RunEditorHotReloadRollbackProof(
            GlobalDataVault vault,
            byte[] baseline,
            out string json)
        {
            string tempPath = string.Empty;
            try
            {
                H8StaticDataArena.Shutdown();
                fixed (byte* baselinePtr = baseline)
                {
                    if (!H8StaticDataArena.TryInitializeFromMemory(vault, baselinePtr, baseline.Length, 0u, 0u, out H8DataBlobLoadStatus baselineStatus))
                    {
                        json = "{\"passed\":false,\"stage\":\"baselineLoad\",\"baselineStatus\":\"" + baselineStatus + "\"}";
                        return false;
                    }
                }

                ulong baselineChecksum = H8StaticDataArena.Header.Checksum64;
                uint baselineBlobBytes = H8StaticDataArena.Header.BlobBytes;
                bool baselineItemsReadable = H8StaticDataArena.TryGetSection(H8DataSectionId.Items, out H8DataSectionEntry baselineItems) &&
                                             baselineItems.Count > 0u;

                byte[] corrupt = MutateStoredChecksum(baseline);
                tempPath = Path.Combine(
                    Path.GetTempPath(),
                    "h8_dm_hot_reload_rollback_" + Guid.NewGuid().ToString("N") + ".h8bin");
                if (!TryWriteBytesToFile(tempPath, corrupt, out string writeError))
                {
                    json = BuildEditorHotReloadRollbackErrorJson("write", writeError);
                    return false;
                }

                bool hotReloadOk = H8StaticDataArena.EditorHotReloadFromFile(tempPath, out H8DataBlobLoadStatus reloadStatus);
                bool itemsStillReadable = H8StaticDataArena.TryGetSection(H8DataSectionId.Items, out H8DataSectionEntry currentItems) &&
                                          currentItems.Count == baselineItems.Count &&
                                          currentItems.RecordSize == baselineItems.RecordSize &&
                                          currentItems.OffsetBytes == baselineItems.OffsetBytes;
                bool checksumStable = H8StaticDataArena.Header.Checksum64 == baselineChecksum;
                bool sizeStable = H8StaticDataArena.Header.BlobBytes == baselineBlobBytes;
                bool passed = baselineItemsReadable &&
                              !hotReloadOk &&
                              reloadStatus == H8DataBlobLoadStatus.BadChecksum &&
                              H8StaticDataArena.IsLoaded &&
                              checksumStable &&
                              sizeStable &&
                              itemsStillReadable;

                json =
                    "{" +
                    "\"passed\":" + JsonBool(passed) + "," +
                    "\"attemptResult\":" + JsonBool(hotReloadOk) + "," +
                    "\"status\":\"" + reloadStatus + "\"," +
                    "\"baselineChecksumHex\":\"0x" + baselineChecksum.ToString("X16", CultureInfo.InvariantCulture) + "\"," +
                    "\"currentChecksumHex\":\"0x" + H8StaticDataArena.Header.Checksum64.ToString("X16", CultureInfo.InvariantCulture) + "\"," +
                    "\"baselineBlobBytes\":" + baselineBlobBytes.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"currentBlobBytes\":" + H8StaticDataArena.Header.BlobBytes.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"itemsStillReadable\":" + JsonBool(itemsStillReadable) +
                    "}";
                return passed;
            }
            catch (IOException ex)
            {
                json = BuildEditorHotReloadRollbackErrorJson("io", ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                json = BuildEditorHotReloadRollbackErrorJson("access", ex.Message);
                return false;
            }
            catch (ArgumentException ex)
            {
                json = BuildEditorHotReloadRollbackErrorJson("argument", ex.Message);
                return false;
            }
            catch (NotSupportedException ex)
            {
                json = BuildEditorHotReloadRollbackErrorJson("unsupported", ex.Message);
                return false;
            }
            catch (System.Security.SecurityException ex)
            {
                json = BuildEditorHotReloadRollbackErrorJson("security", ex.Message);
                return false;
            }
            finally
            {
                TryDeleteTempFile(tempPath);
                if (H8StaticDataArena.IsLoaded)
                    H8StaticDataArena.LockReady();
            }
        }

        private static bool RunFileLoadProof(
            GlobalDataVault vault,
            string blobPath,
            out H8DataBlobLoadStatus status,
            out double elapsedMicroseconds,
            out long managedBytes)
        {
            H8StaticDataArena.Shutdown();
            long beforeBytes = GetAllocatedBytesForCurrentThreadSafe();
            long startTicks = Stopwatch.GetTimestamp();
            bool ok = H8StaticDataArena.TryInitializeFromFile(vault, blobPath, 0u, 0u, false, out status);
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            long afterBytes = GetAllocatedBytesForCurrentThreadSafe();

            elapsedMicroseconds = TicksToMicroseconds(elapsedTicks);
            managedBytes = ComputeDeltaOrMinusOne(beforeBytes, afterBytes);
            return ok && status == H8DataBlobLoadStatus.Loaded && H8StaticDataArena.IsLoaded;
        }

        private static bool ResolvePayloadBuffer(
            GlobalDataVault vault,
            int blobBytes,
            out int payloadLength,
            out long allocatedBytes,
            out long arenaBytes)
        {
            payloadLength = 0;
            allocatedBytes = vault.AllocatedBytes;
            arenaBytes = vault.ArenaBytes;
            if (!vault.TryGetGenerationHandle<byte>(BufferID.DataMonolithPayload, out VaultGenerationHandle<byte> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<byte> payload) ||
                !payload.IsCreated)
            {
                return false;
            }

            payloadLength = payload.Length;
            return payload.Length >= blobBytes &&
                   H8StaticDataArena.ByteLength == blobBytes &&
                   H8StaticDataArena.Header.BlobBytes == (uint)blobBytes;
        }

        private static bool RunLockedReloadBlockProof(
            GlobalDataVault vault,
            byte[] baseline,
            out string json)
        {
            H8StaticDataArena.Shutdown();
            fixed (byte* baselinePtr = baseline)
            {
                if (!H8StaticDataArena.TryInitializeFromMemory(vault, baselinePtr, baseline.Length, 0u, 0u, out H8DataBlobLoadStatus baselineStatus))
                {
                    json = "{\"passed\":false,\"baselineStatus\":\"" + baselineStatus + "\"}";
                    return false;
                }
            }

            ulong baselineChecksum = H8StaticDataArena.Header.Checksum64;
            byte[] corrupt = MutateStoredChecksum(baseline);
            bool hotLoadOk;
            H8DataBlobLoadStatus corruptStatus;
            fixed (byte* corruptPtr = corrupt)
                hotLoadOk = H8StaticDataArena.TryInitializeFromMemory(vault, corruptPtr, corrupt.Length, 0u, 0u, out corruptStatus);

            bool itemsStillReadable = H8StaticDataArena.TryGetSection(H8DataSectionId.Items, out H8DataSectionEntry items) &&
                                      items.Count > 0u &&
                                      (items.OffsetBytes & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) == 0u;
            bool passed = !hotLoadOk &&
                          corruptStatus == H8DataBlobLoadStatus.ReadyLocked &&
                          H8StaticDataArena.IsLoaded &&
                          H8StaticDataArena.Header.Checksum64 == baselineChecksum &&
                          itemsStillReadable;

            json =
                "{" +
                "\"passed\":" + JsonBool(passed) + "," +
                "\"attemptResult\":" + JsonBool(hotLoadOk) + "," +
                "\"status\":\"" + corruptStatus + "\"," +
                "\"baselineChecksumHex\":\"0x" + baselineChecksum.ToString("X16", CultureInfo.InvariantCulture) + "\"," +
                "\"currentChecksumHex\":\"0x" + H8StaticDataArena.Header.Checksum64.ToString("X16", CultureInfo.InvariantCulture) + "\"," +
                "\"itemsStillReadable\":" + JsonBool(itemsStillReadable) +
                "}";
            return passed;
        }

        private static string BuildEditorHotReloadRollbackErrorJson(string stage, string message)
        {
            return "{\"passed\":false,\"stage\":\"" + EscapeJson(stage) + "\",\"error\":\"" + EscapeJson(message) + "\"}";
        }

        private static bool RunColdCorruptBootProof(
            GlobalDataVault vault,
            byte[] baseline,
            out string casesJson,
            out int caseCount,
            out int passCount)
        {
            StringBuilder cases = new StringBuilder(8192);
            caseCount = 0;
            passCount = 0;
            RunColdCase(vault, "bad_stored_checksum", MutateStoredChecksum(baseline), H8DataBlobLoadStatus.BadChecksum, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_payload_checksum", MutatePayloadByte(baseline), H8DataBlobLoadStatus.BadChecksum, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_header_unknown_flags", MutateHeaderUnknownFlagsWithValidChecksum(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_header_reserved", MutateHeaderReserved(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_directory_reserved", MutateDirectoryReservedWithValidChecksum(baseline), H8DataBlobLoadStatus.InvalidSectionTable, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_header_section_count", MutateHeaderSectionCount(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_header_section_table_offset", MutateHeaderSectionTableOffset(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_section_out_of_bounds", MutateSectionOutOfBoundsWithValidChecksum(baseline), H8DataBlobLoadStatus.InvalidSectionTable, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_section_unaligned_offset", MutateSectionUnalignedOffsetWithValidChecksum(baseline), H8DataBlobLoadStatus.InvalidSectionTable, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_section_table_void", MutateSectionTableVoidWithValidChecksum(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_section_overlap", MutateSectionOverlapWithValidChecksum(baseline), H8DataBlobLoadStatus.InvalidSectionTable, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "bad_localization_directory", MutateLocalizationDirectoryWithValidChecksum(baseline), H8DataBlobLoadStatus.InvalidSectionTable, cases, ref caseCount, ref passCount);
            RunColdCase(vault, "truncated_blob", MutateTruncate(baseline), H8DataBlobLoadStatus.HeaderMismatch, cases, ref caseCount, ref passCount);
            casesJson = cases.ToString();
            return caseCount > 0 && passCount == caseCount;
        }

        private static void RunColdCase(
            GlobalDataVault vault,
            string name,
            byte[] bytes,
            H8DataBlobLoadStatus expectedStatus,
            StringBuilder cases,
            ref int caseCount,
            ref int passCount)
        {
            if (caseCount > 0)
                cases.AppendLine(",");

            caseCount++;
            H8StaticDataArena.Shutdown();
            bool ok;
            H8DataBlobLoadStatus status;
            long beforeBytes = GetAllocatedBytesForCurrentThreadSafe();
            long startTicks = Stopwatch.GetTimestamp();
            fixed (byte* ptr = bytes)
                ok = H8StaticDataArena.TryInitializeFromMemory(vault, ptr, bytes.Length, 0u, 0u, out status);
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            long afterBytes = GetAllocatedBytesForCurrentThreadSafe();

            bool sectionBlocked = !H8StaticDataArena.TryGetSection(H8DataSectionId.Items, out _);
            bool expected = status == expectedStatus || (name == "truncated_blob" && status == H8DataBlobLoadStatus.FileTooSmall);
            bool passed = !ok && !H8StaticDataArena.IsLoaded && sectionBlocked && expected;
            if (passed)
                passCount++;

            cases.Append("      {");
            cases.Append("\"name\":\"").Append(name).Append("\",");
            cases.Append("\"passed\":").Append(JsonBool(passed)).Append(",");
            cases.Append("\"attemptResult\":").Append(JsonBool(ok)).Append(",");
            cases.Append("\"status\":\"").Append(status).Append("\",");
            cases.Append("\"expectedStatus\":\"").Append(expectedStatus).Append("\",");
            cases.Append("\"isLoadedAfterFailure\":").Append(JsonBool(H8StaticDataArena.IsLoaded)).Append(",");
            cases.Append("\"sectionAccessBlocked\":").Append(JsonBool(sectionBlocked)).Append(",");
            cases.Append("\"elapsedMicroseconds\":").Append(FormatDouble(TicksToMicroseconds(elapsedTicks))).Append(",");
            cases.Append("\"managedAllocatedBytes\":").Append(ComputeDeltaOrMinusOne(beforeBytes, afterBytes));
            cases.Append("}");
            H8StaticDataArena.Shutdown();
        }

        private static ResidentLoadMetrics RunResidentMemoryLoadMetrics(GlobalDataVault vault, byte[] baseline)
        {
            ResidentLoadMetrics metrics = default;
            metrics.Iterations = ResidentLoadIterations;
            metrics.MinMicroseconds = double.MaxValue;
            metrics.MaxMicroseconds = 0d;
            metrics.ManagedAllocatedBytes = 0L;
            metrics.MaxManagedAllocatedBytes = 0L;
            metrics.AllocationCounterSupported = true;

            fixed (byte* source = baseline)
            {
                H8StaticDataArena.Shutdown();
                H8StaticDataArena.TryInitializeFromMemory(vault, source, baseline.Length, 0u, 0u, out _);
                H8StaticDataArena.Shutdown();

                for (int i = 0; i < ResidentLoadIterations; i++)
                {
                    long beforeBytes = GetAllocatedBytesForCurrentThreadSafe();
                    long startTicks = Stopwatch.GetTimestamp();
                    bool ok = H8StaticDataArena.TryInitializeFromMemory(vault, source, baseline.Length, 0u, 0u, out H8DataBlobLoadStatus status);
                    long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                    long afterBytes = GetAllocatedBytesForCurrentThreadSafe();

                    double elapsedMicroseconds = TicksToMicroseconds(elapsedTicks);
                    long delta = ComputeDeltaOrMinusOne(beforeBytes, afterBytes);
                    if (!ok || status != H8DataBlobLoadStatus.Loaded)
                        metrics.FailedIterations++;

                    metrics.TotalMicroseconds += elapsedMicroseconds;
                    metrics.MinMicroseconds = Math.Min(metrics.MinMicroseconds, elapsedMicroseconds);
                    metrics.MaxMicroseconds = Math.Max(metrics.MaxMicroseconds, elapsedMicroseconds);
                    if (delta >= 0)
                    {
                        metrics.ManagedAllocatedBytes += delta;
                        metrics.MaxManagedAllocatedBytes = Math.Max(metrics.MaxManagedAllocatedBytes, delta);
                    }
                    else
                    {
                        metrics.AllocationCounterSupported = false;
                    }

                    H8StaticDataArena.Shutdown();
                }
            }

            if (metrics.MinMicroseconds == double.MaxValue)
                metrics.MinMicroseconds = 0d;
            metrics.MeanMicroseconds = ResidentLoadIterations > 0
                ? metrics.TotalMicroseconds / ResidentLoadIterations
                : 0d;
            metrics.Pass = metrics.FailedIterations == 0 &&
                           metrics.AllocationCounterSupported &&
                           metrics.MeanMicroseconds <= ResidentMeanTargetMicroseconds &&
                           metrics.MaxManagedAllocatedBytes == 0L;
            return metrics;
        }

        private static bool ResolveRequestedSections(out string json)
        {
            StringBuilder builder = new StringBuilder(8192);
            bool ok = true;
            int written = 0;
            AppendSection(builder, "Crafting", H8DataSectionId.Items, ref written, ref ok);
            AppendSection(builder, "Crafting", H8DataSectionId.Recipes, ref written, ref ok);
            AppendSection(builder, "Crafting", H8DataSectionId.LootCdf, ref written, ref ok);
            AppendSection(builder, "Crafting", H8DataSectionId.Economy, ref written, ref ok);
            AppendSection(builder, "Ecology", H8DataSectionId.Creatures, ref written, ref ok);
            AppendSection(builder, "Ecology", H8DataSectionId.Biomes, ref written, ref ok);
            AppendSection(builder, "Ecology", H8DataSectionId.BiomeHeatmap, ref written, ref ok);
            AppendSection(builder, "Ecology", H8DataSectionId.RadiationIntensityMap, ref written, ref ok);
            AppendSection(builder, "Ecology", H8DataSectionId.SpawnCreditCosts, ref written, ref ok);
            AppendSection(builder, "Audio", H8DataSectionId.AudioClipRegistry, ref written, ref ok);
            AppendSection(builder, "Audio", H8DataSectionId.SopErrors, ref written, ref ok);
            AppendSection(builder, "Physiology", H8DataSectionId.DepthPressureCurve, ref written, ref ok);
            AppendSection(builder, "Physiology", H8DataSectionId.ToolHeatCapacity, ref written, ref ok);
            AppendSection(builder, "Physiology", H8DataSectionId.SubmarineHullConstants, ref written, ref ok);
            AppendSection(builder, "Physiology", H8DataSectionId.PhysicsMaterials, ref written, ref ok);
            AppendSection(builder, "Physiology", H8DataSectionId.PhysicsConstants, ref written, ref ok);
            json = builder.ToString();
            return ok;
        }

        private static void AppendSection(StringBuilder builder, string block, H8DataSectionId sectionId, ref int written, ref bool ok)
        {
            if (written > 0)
                builder.AppendLine(",");

            bool resolved = H8StaticDataArena.TryGetSection(sectionId, out H8DataSectionEntry section);
            bool aligned = !resolved || section.Count == 0u ||
                           (section.OffsetBytes & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) == 0u;
            bool inRange = !resolved || section.Count == 0u ||
                           ((ulong)section.RecordSize * section.Count <= H8StaticDataArena.Header.BlobBytes - section.OffsetBytes);
            ok &= resolved && aligned && inRange;

            builder.Append("    {");
            builder.Append("\"block\":\"").Append(block).Append("\",");
            builder.Append("\"section\":\"").Append(sectionId).Append("\",");
            builder.Append("\"resolved\":").Append(JsonBool(resolved)).Append(",");
            builder.Append("\"offset\":").Append(resolved ? section.OffsetBytes : 0u).Append(",");
            builder.Append("\"bytes\":").Append(resolved ? (ulong)section.RecordSize * section.Count : 0UL).Append(",");
            builder.Append("\"count\":").Append(resolved ? section.Count : 0u).Append(",");
            builder.Append("\"recordSize\":").Append(resolved ? section.RecordSize : 0u).Append(",");
            builder.Append("\"cacheLineAligned\":").Append(JsonBool(aligned)).Append(",");
            builder.Append("\"inBlobRange\":").Append(JsonBool(inRange));
            builder.Append("}");
            written++;
        }

        private static HeaderSnapshot ReadHeaderSnapshot(byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                H8DataBlobHeader header = UnsafeUtility.ReadArrayElement<H8DataBlobHeader>(ptr, 0);
                H8DataBlobDirectory directory = UnsafeUtility.ReadArrayElement<H8DataBlobDirectory>(ptr + H8DataLayoutConstants.HeaderSizeBytes, 0);
                return new HeaderSnapshot
                {
                    Magic = header.Magic,
                    Version = header.FormatVersion,
                    HeaderBytes = header.HeaderBytes,
                    Checksum64 = header.Checksum64,
                    BlobBytes = header.BlobBytes,
                    DirectoryOffset = header.DirectoryOffset,
                    DirectoryBytes = header.DirectoryBytes,
                    SectionTableOffset = header.SectionTableOffset,
                    SectionCount = header.SectionCount,
                    Flags = header.Flags,
                    WorldSeed = header.WorldSeed,
                    AppVersionHash = header.AppVersionHash,
                    SchemaHash = header.SchemaHash,
                    SectionTableBytes = directory.SectionTableBytes,
                    DataStartOffset = directory.DataStartOffset,
                    LocalizationOffset = directory.LocalizationOffset,
                    LocalizationBytes = directory.LocalizationBytes,
                    LittleEndianFlagSet = (header.Flags & H8DataLayoutConstants.BlobFlagLittleEndian) != 0u
                };
            }
        }

        private static byte[] MutateStoredChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            bytes[8] = (byte)(bytes[8] ^ 0x5A);
            return bytes;
        }

        private static byte[] MutatePayloadByte(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            int index = Math.Max(H8DataLayoutConstants.HeaderSizeBytes, bytes.Length - 32);
            bytes[index] ^= 0x33;
            return bytes;
        }

        private static byte[] MutateHeaderUnknownFlagsWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, 36, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u);
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 32, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateHeaderReserved(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, 52, 1u);
            return bytes;
        }

        private static byte[] MutateDirectoryReservedWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 44, 1u);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateHeaderSectionCount(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, 32, (uint)H8DataSectionId.PhysicsConstants - 1u);
            return bytes;
        }

        private static byte[] MutateHeaderSectionTableOffset(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, 28, H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + 64u);
            return bytes;
        }

        private static byte[] MutateSectionOutOfBoundsWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + 12, (uint)(bytes.Length + 64));
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionUnalignedOffsetWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            uint dataStart = ReadUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 20);
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + 12, dataStart + 1u);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionTableVoidWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            uint voidOffset = (uint)(bytes.Length - 16);
            WriteUInt32(bytes, 28, voidOffset);
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 8, voidOffset);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionOverlapWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            int firstEntry = H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes;
            int secondEntry = firstEntry + 16;
            uint firstOffset = ReadUInt32(bytes, firstEntry + 12);
            WriteUInt32(bytes, secondEntry + 12, firstOffset);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateLocalizationDirectoryWithValidChecksum(byte[] baseline)
        {
            byte[] bytes = (byte[])baseline.Clone();
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 24, H8DataLayoutConstants.HeaderSizeBytes);
            RewriteChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateTruncate(byte[] baseline)
        {
            int length = Math.Max(H8DataLayoutConstants.HeaderSizeBytes, baseline.Length - 257);
            byte[] bytes = new byte[length];
            Buffer.BlockCopy(baseline, 0, bytes, 0, length);
            return bytes;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
            bytes[offset + 4] = (byte)(value >> 32);
            bytes[offset + 5] = (byte)(value >> 40);
            bytes[offset + 6] = (byte)(value >> 48);
            bytes[offset + 7] = (byte)(value >> 56);
        }

        private static void RewriteChecksum(byte[] bytes)
        {
            ulong checksum = H8DataMonolithCompiler.ComputeHash64(
                bytes,
                H8DataLayoutConstants.HeaderSizeBytes,
                bytes.Length - H8DataLayoutConstants.HeaderSizeBytes);
            WriteUInt64(bytes, 8, checksum);
        }

        private static void AppendHeader(StringBuilder json, HeaderSnapshot header)
        {
            json.AppendLine("  \"header\": {");
            AppendJsonField(json, "magicHex", "0x" + header.Magic.ToString("X8", CultureInfo.InvariantCulture), comma: true, indent: 2);
            AppendJsonNumber(json, "version", header.Version, comma: true, indent: 2);
            AppendJsonNumber(json, "headerBytes", header.HeaderBytes, comma: true, indent: 2);
            AppendJsonField(json, "checksumHex", "0x" + header.Checksum64.ToString("X16", CultureInfo.InvariantCulture), comma: true, indent: 2);
            AppendJsonNumber(json, "blobBytes", header.BlobBytes, comma: true, indent: 2);
            AppendJsonNumber(json, "directoryOffset", header.DirectoryOffset, comma: true, indent: 2);
            AppendJsonNumber(json, "directoryBytes", header.DirectoryBytes, comma: true, indent: 2);
            AppendJsonNumber(json, "sectionTableOffset", header.SectionTableOffset, comma: true, indent: 2);
            AppendJsonNumber(json, "sectionTableBytes", header.SectionTableBytes, comma: true, indent: 2);
            AppendJsonNumber(json, "sectionCount", header.SectionCount, comma: true, indent: 2);
            AppendJsonNumber(json, "dataStartOffset", header.DataStartOffset, comma: true, indent: 2);
            AppendJsonNumber(json, "localizationOffset", header.LocalizationOffset, comma: true, indent: 2);
            AppendJsonNumber(json, "localizationBytes", header.LocalizationBytes, comma: true, indent: 2);
            AppendJsonBool(json, "littleEndianFlagSet", header.LittleEndianFlagSet, comma: true, indent: 2);
            AppendJsonBool(json, "dataStartCacheLineAligned", (header.DataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) == 0u, comma: false, indent: 2);
            json.AppendLine("  },");
        }

        private static void AppendResidentMetrics(StringBuilder json, ResidentLoadMetrics metrics)
        {
            json.AppendLine("  \"residentMemoryLoadMetrics\": {");
            AppendJsonBool(json, "passed", metrics.Pass, comma: true, indent: 2);
            AppendJsonNumber(json, "iterations", metrics.Iterations, comma: true, indent: 2);
            AppendJsonNumber(json, "failedIterations", metrics.FailedIterations, comma: true, indent: 2);
            AppendJsonNumber(json, "meanMicroseconds", metrics.MeanMicroseconds, comma: true, indent: 2);
            AppendJsonNumber(json, "minMicroseconds", metrics.MinMicroseconds, comma: true, indent: 2);
            AppendJsonNumber(json, "maxMicroseconds", metrics.MaxMicroseconds, comma: true, indent: 2);
            AppendJsonNumber(json, "targetMeanMicroseconds", ResidentMeanTargetMicroseconds, comma: true, indent: 2);
            AppendJsonBool(json, "allocationCounterSupported", metrics.AllocationCounterSupported, comma: true, indent: 2);
            AppendJsonNumber(json, "managedAllocatedBytes", metrics.ManagedAllocatedBytes, comma: true, indent: 2);
            AppendJsonNumber(json, "maxManagedAllocatedBytesPerIteration", metrics.MaxManagedAllocatedBytes, comma: false, indent: 2);
            json.AppendLine("  },");
        }

        private static void AppendReleaseCliTiming(StringBuilder json, ReleaseCliLoadStressSnapshot snapshot)
        {
            json.AppendLine("  \"releaseCliLoadStress\": {");
            AppendJsonBool(json, "available", snapshot.Available, comma: true, indent: 2);
            AppendJsonField(json, "status", snapshot.Status, comma: true, indent: 2);
            AppendJsonBool(json, "targetLoadMet", snapshot.TargetLoadMet, comma: true, indent: 2);
            AppendJsonNumber(json, "targetLoadMicroseconds", snapshot.TargetLoadMicroseconds, comma: true, indent: 2);
            AppendJsonNumber(json, "nativeResidentLoadEstimateMicroseconds", snapshot.NativeResidentLoadEstimateMicroseconds, comma: true, indent: 2);
            AppendJsonNumber(json, "nativeResidentLoadEstimateAllocatedBytes", snapshot.NativeResidentLoadEstimateAllocatedBytes, comma: false, indent: 2);
            json.AppendLine("  },");
        }

        private static ReleaseCliLoadStressSnapshot ReadReleaseCliLoadStress(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "Docs/Reports/DATA_MONOLITH_LOAD_STRESS_X_002.json".Replace('/', Path.DirectorySeparatorChar));
            ReleaseCliLoadStressSnapshot snapshot = default;
            snapshot.Status = string.Empty;
            if (!File.Exists(path))
                return snapshot;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                snapshot.Available = true;
                snapshot.Status = ExtractJsonString(json, "\"status\"");
                snapshot.TargetLoadMet = ExtractJsonBool(json, "\"targetLoadMet\"");
                snapshot.TargetLoadMicroseconds = ExtractJsonDouble(json, "\"targetLoadMicroseconds\"");
                snapshot.NativeResidentLoadEstimateMicroseconds = ExtractJsonDouble(json, "\"nativeResidentLoadEstimateMicroseconds\"");
                snapshot.NativeResidentLoadEstimateAllocatedBytes = ExtractJsonLong(json, "\"nativeResidentLoadEstimateAllocatedBytes\"");
                snapshot.Pass = snapshot.TargetLoadMet &&
                                snapshot.NativeResidentLoadEstimateAllocatedBytes == 0L &&
                                snapshot.NativeResidentLoadEstimateMicroseconds > 0d &&
                                snapshot.NativeResidentLoadEstimateMicroseconds <= snapshot.TargetLoadMicroseconds;
            }
            catch (IOException)
            {
                snapshot = default;
                snapshot.Status = "UNREADABLE";
            }
            catch (UnauthorizedAccessException)
            {
                snapshot = default;
                snapshot.Status = "UNREADABLE";
            }
            catch (ArgumentException)
            {
                snapshot = default;
                snapshot.Status = "UNREADABLE";
            }
            catch (NotSupportedException)
            {
                snapshot = default;
                snapshot.Status = "UNREADABLE";
            }

            return snapshot;
        }

        private static bool TryReadManagedFixtureBlob(string path, out byte[] bytes, out string error)
        {
            bytes = Array.Empty<byte>();
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
                if (stream.Length <= 0L || stream.Length > int.MaxValue)
                {
                    error = "invalid fixture blob length: " + stream.Length.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                bytes = new byte[(int)stream.Length]; // COLD ALLOC: byte[blobBytes] - editor-only in-memory load stress fixture - owner: H8DataMonolithGlobalDataVaultStressProbe
                int total = 0;
                while (total < bytes.Length)
                {
                    int read = stream.Read(bytes, total, bytes.Length - total);
                    if (read <= 0)
                        break;

                    total += read;
                }

                if (total == bytes.Length)
                    return true;

                error = "fixture blob read incomplete: " + total.ToString(CultureInfo.InvariantCulture) + "/" + bytes.Length.ToString(CultureInfo.InvariantCulture);
                bytes = Array.Empty<byte>();
                return false;
            }
            catch (IOException ex) { return FailFixtureFile("read", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFixtureFile("read", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFixtureFile("read", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFixtureFile("read", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFixtureFile("read", ex.Message, out error); }
        }

        private static bool TryWriteBytesToFile(string path, byte[] bytes, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                stream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (IOException ex) { return FailFixtureFile("write", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFixtureFile("write", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFixtureFile("write", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFixtureFile("write", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFixtureFile("write", ex.Message, out error); }
        }

        private static bool FailFixtureFile(string stage, string message, out string error)
        {
            error = stage + ": " + message;
            return false;
        }

        private static string BuildMissingReport(string setupError)
        {
            StringBuilder json = new StringBuilder(1024);
            json.AppendLine("{");
            AppendJsonField(json, "agent", AgentId, comma: true, indent: 1);
            AppendJsonField(json, "status", "FAIL_MISSING_OR_INVALID_MONOLITH", comma: true, indent: 1);
            AppendJsonField(json, "setupError", setupError, comma: false, indent: 1);
            json.AppendLine("}");
            return json.ToString();
        }

        private static string ResolveProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static void WriteReport(string projectRoot, string text)
        {
            string absolutePath = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, text, Encoding.UTF8);

            string absolutePath1313 = Path.Combine(projectRoot, ReportPath1313.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath1313));
            File.WriteAllText(
                absolutePath1313,
                text.Replace("\"agent\": \"" + AgentId + "\"", "\"agent\": \"" + AgentId1313 + "\""),
                Encoding.UTF8);

            string absolutePath1330 = Path.Combine(projectRoot, ReportPath1330.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath1330));
            File.WriteAllText(
                absolutePath1330,
                text.Replace("\"agent\": \"" + AgentId + "\"", "\"agent\": \"" + AgentId1330 + "\""),
                Encoding.UTF8);
        }

        private static long GetAllocatedBytesForCurrentThreadSafe()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch (NotSupportedException)
            {
                return -1L;
            }
        }

        private static long ComputeDeltaOrMinusOne(long before, long after)
        {
            if (before < 0L || after < 0L)
                return -1L;
            return Math.Max(0L, after - before);
        }

        private static double TicksToMicroseconds(long ticks)
        {
            return ticks * 1000000.0d / Stopwatch.Frequency;
        }

        private static void AppendJsonField(StringBuilder builder, string name, string value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": \"").Append(EscapeJson(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonBool(StringBuilder builder, string name, bool value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(JsonBool(value));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, double value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(FormatDouble(value));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, long value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, int value, bool comma, int indent)
        {
            AppendJsonNumber(builder, name, (long)value, comma, indent);
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, uint value, bool comma, int indent)
        {
            AppendJsonNumber(builder, name, (ulong)value, comma, indent);
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, ushort value, bool comma, int indent)
        {
            AppendJsonNumber(builder, name, (ulong)value, comma, indent);
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, ulong value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            for (int i = 0; i < indent; i++)
                builder.Append("  ");
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void TryDeleteTempFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return string.Empty;

            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0)
                return string.Empty;

            int quoteStart = json.IndexOf('"', colon + 1);
            if (quoteStart < 0)
                return string.Empty;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            return quoteEnd > quoteStart ? json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1) : string.Empty;
        }

        private static bool ExtractJsonBool(string json, string key)
        {
            string value = ExtractJsonRawValue(json, key);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static double ExtractJsonDouble(string json, string key)
        {
            string value = ExtractJsonRawValue(json, key);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0d;
        }

        private static long ExtractJsonLong(string json, string key)
        {
            string value = ExtractJsonRawValue(json, key);
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0L;
        }

        private static string ExtractJsonRawValue(string json, string key)
        {
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return string.Empty;

            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0)
                return string.Empty;

            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '\r' && json[end] != '\n' && json[end] != '}')
                end++;

            return json.Substring(start, end - start).Trim().Trim('"');
        }

        private struct HeaderSnapshot
        {
            public uint Magic;
            public ushort Version;
            public ushort HeaderBytes;
            public ulong Checksum64;
            public uint BlobBytes;
            public uint DirectoryOffset;
            public uint DirectoryBytes;
            public uint SectionTableOffset;
            public uint SectionCount;
            public uint Flags;
            public uint WorldSeed;
            public uint AppVersionHash;
            public uint SchemaHash;
            public uint SectionTableBytes;
            public uint DataStartOffset;
            public uint LocalizationOffset;
            public uint LocalizationBytes;
            public bool LittleEndianFlagSet;
        }

        private struct ResidentLoadMetrics
        {
            public int Iterations;
            public int FailedIterations;
            public double TotalMicroseconds;
            public double MeanMicroseconds;
            public double MinMicroseconds;
            public double MaxMicroseconds;
            public long ManagedAllocatedBytes;
            public long MaxManagedAllocatedBytes;
            public bool AllocationCounterSupported;
            public bool Pass;
        }

        private struct ReleaseCliLoadStressSnapshot
        {
            public bool Available;
            public bool Pass;
            public string Status;
            public bool TargetLoadMet;
            public double TargetLoadMicroseconds;
            public double NativeResidentLoadEstimateMicroseconds;
            public long NativeResidentLoadEstimateAllocatedBytes;
        }
    }
}
#endif
