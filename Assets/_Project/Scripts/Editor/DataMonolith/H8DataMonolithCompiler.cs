#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorValidation
{
    /// <summary>
    /// Editor compiler that bakes authored CSV/JSON source data into one Data Monolith blob.
    /// </summary>
    public static unsafe class H8DataMonolithCompiler
    {
        internal const string SourceFolder = "Assets/_SourceData/DataMonolith";
        internal const string BalanceSourceFolder = "Data/Balance";
        internal const string OutputAssetPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const string AppliedLoreImporterPath = "Tools/AppliedLoreImporter.py";
        private const string AppliedLoreRouteCardExporterPath = "Tools/AppliedLoreRouteCardExporter.py";
        private const string MenuPath = "Hecton8/Data Monolith/Bake Static Data";
        private const int InitialBlobCapacity = 128 * 1024;
        private const int Utf8ScratchBytes = 16384;
        private const int LocalizationPoolExpectedValueCapacity = 65536;
        private const int LocalizationPoolInitialByteCapacity = 8 * 1024 * 1024;
        private const string TempOutputSuffix = ".tmp";
        private const string BackupOutputSuffix = ".bak";
        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;

        internal static string LastError;

        private static readonly H8DataSectionId[] SectionOrder =
        {
            H8DataSectionId.Items,
            H8DataSectionId.Creatures,
            H8DataSectionId.Biomes,
            H8DataSectionId.Recipes,
            H8DataSectionId.BiomeHeatmap,
            H8DataSectionId.QuestNodes,
            H8DataSectionId.QuestEdges,
            H8DataSectionId.LootCdf,
            H8DataSectionId.VoxelMaterials,
            H8DataSectionId.AudioClipRegistry,
            H8DataSectionId.VfxScalars,
            H8DataSectionId.DepthPressureCurve,
            H8DataSectionId.ToolHeatCapacity,
            H8DataSectionId.SubmarineHullConstants,
            H8DataSectionId.NarrativeTriggers,
            H8DataSectionId.PhysicsMaterials,
            H8DataSectionId.GhostModules,
            H8DataSectionId.RadiationIntensityMap,
            H8DataSectionId.SpawnCreditCosts,
            H8DataSectionId.LightAttenuationCurve,
            H8DataSectionId.SopErrors,
            H8DataSectionId.HudLayouts,
            H8DataSectionId.LocalizationUtf8,
            H8DataSectionId.SectorPageDirectory,
            H8DataSectionId.Economy,
            H8DataSectionId.PhysicsConstants,
            H8DataSectionId.AppliedLorePackets,
            H8DataSectionId.AppliedLoreRoutes
        };

        [MenuItem(MenuPath)]
        public static void BakeFromMenu()
        {
            BakeAll(logSummary: true);
        }

        public static void BakeFromCommandLine()
        {
            bool baked = BakeAll(logSummary: true);
            string validationError = string.Empty;
            bool valid = baked && TryValidateOutputBlob(out validationError);
            if (!valid && string.IsNullOrEmpty(LastError))
                LastError = validationError;

            int exitCode = valid ? 0 : 1;
            if (!valid)
                Debug.LogError("[H8DataMonolithCompiler] Batch bake failed: " + LastError);

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        internal static bool BakeAll(bool logSummary)
        {
            LastError = string.Empty;
            try
            {
                if (!H8DataLayoutAudit.ValidateBlittableSizes())
                {
                    LastError = "Blittable layout audit failed.";
                    Debug.LogError("[H8DataMonolithCompiler] " + LastError + " Bake aborted.");
                    return false;
                }

                EnsureLittleEndianEditorHost();
                Directory.CreateDirectory(SourceFolder);
                Directory.CreateDirectory(BalanceSourceFolder);
                Directory.CreateDirectory(Path.GetDirectoryName(OutputAssetPath));

                if (!TryRunAppliedLoreImporter(out string importerSummary))
                {
                    LastError = importerSummary;
                    Debug.LogError("[H8DataMonolithCompiler] " + LastError + " Bake aborted.");
                    return false;
                }

                if (!TryRunAppliedLoreRouteCardExporter(out string routeExporterSummary))
                {
                    LastError = routeExporterSummary;
                    Debug.LogError("[H8DataMonolithCompiler] " + LastError + " Bake aborted.");
                    return false;
                }

                LocalizationPool localizationPool = new LocalizationPool();
                DataSet dataSet = BuildDataSetFromSources(localizationPool, out _, out _);
                FinalizeGeneratedTables(dataSet);
                ValidateProductionSectionCoverage(dataSet);
                ValidateCrossReferences(dataSet);

                byte[] blob = BuildBlob(dataSet, localizationPool);
                if (!TryWriteValidatedBlob(blob, out string validationError))
                {
                    LastError = validationError;
                    Debug.LogError("[H8DataMonolithCompiler] " + LastError + " Bake aborted.");
                    return false;
                }

                AssetDatabase.ImportAsset(OutputAssetPath, ImportAssetOptions.ForceUpdate);

                H8DataMonolithHotReloadSocket.NotifyBake(OutputAssetPath);
                if (logSummary)
                {
                    Debug.Log(
                        "[H8DataMonolithCompiler] Baked Data Monolith: bytes=" +
                        blob.Length +
                        ", items=" +
                        dataSet.Items.Count +
                        ", creatures=" +
                        dataSet.Creatures.Count +
                        ", biomes=" +
                        dataSet.Biomes.Count +
                        ", economy=" +
                        dataSet.Economy.Count +
                        ", physics=" +
                        dataSet.PhysicsConstants.Count +
                        ", appliedLore=" +
                        dataSet.AppliedLorePackets.Count +
                        ", appliedLoreRoutes=" +
                        dataSet.AppliedLoreRoutes.Count +
                        ", sections=" +
                        SectionOrder.Length +
                        ", import=" +
                        importerSummary +
                        ", routes=" +
                        routeExporterSummary +
                        ".");
                }

                return true;
            }
            catch (IOException ex) { return FailBake(ex); }
            catch (UnauthorizedAccessException ex) { return FailBake(ex); }
            catch (ArgumentException ex) { return FailBake(ex); }
            catch (InvalidOperationException ex) { return FailBake(ex); }
            catch (FormatException ex) { return FailBake(ex); }
            catch (OverflowException ex) { return FailBake(ex); }
            catch (NotSupportedException ex) { return FailBake(ex); }
            catch (System.Security.SecurityException ex) { return FailBake(ex); }
        }

        private static bool FailBake(Exception ex)
        {
            LastError = ex.Message;
            Debug.LogException(ex);
            return false;
        }

        internal static bool TryAnalyzeProductionCoverage(out string report, out int missingCount)
        {
            try
            {
                if (!TryRunAppliedLoreImporter(out string importerSummary))
                {
                    missingCount = -1;
                    report = "applied-lore-import-failed: " + importerSummary;
                    return false;
                }

                if (!TryRunAppliedLoreRouteCardExporter(out string routeExporterSummary))
                {
                    missingCount = -1;
                    report = "applied-lore-route-export-failed: " + routeExporterSummary;
                    return false;
                }

                LocalizationPool localizationPool = new LocalizationPool();
                DataSet dataSet = BuildDataSetFromSources(localizationPool, out int csvFileCount, out int jsonFileCount);
                FinalizeGeneratedTables(dataSet);
                ValidateCrossReferences(dataSet);
                string coverageError = BuildProductionCoverageError(dataSet, out missingCount);
                report = BuildProductionCoverageReport(dataSet, csvFileCount, jsonFileCount, missingCount, coverageError);
                if (!string.IsNullOrEmpty(importerSummary))
                    report += "applied-lore-import=" + importerSummary + System.Environment.NewLine;
                if (!string.IsNullOrEmpty(routeExporterSummary))
                    report += "applied-lore-routes=" + routeExporterSummary + System.Environment.NewLine;
                return missingCount == 0;
            }
            catch (IOException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (UnauthorizedAccessException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (ArgumentException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (InvalidOperationException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (FormatException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (OverflowException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (NotSupportedException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
            catch (System.Security.SecurityException ex) { return FailCoverageAnalysis(ex, out report, out missingCount); }
        }

        private static bool FailCoverageAnalysis(Exception ex, out string report, out int missingCount)
        {
            missingCount = -1;
            report = "coverage-analysis-failed: " + ex.Message;
            return false;
        }

        private static bool TryRunAppliedLoreImporter(out string summary)
        {
            return TryRunPythonProjectTool(AppliedLoreImporterPath, "Applied lore importer", out summary);
        }

        private static bool TryRunAppliedLoreRouteCardExporter(out string summary)
        {
            return TryRunPythonProjectTool(AppliedLoreRouteCardExporterPath, "Applied lore route-card exporter", out summary);
        }

        private static bool TryRunPythonProjectTool(string relativeToolPath, string label, out string summary)
        {
            summary = string.Empty;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string toolPath = Path.Combine(projectRoot, relativeToolPath);
            if (!File.Exists(toolPath))
            {
                summary = label + " missing: " + toolPath;
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = QuoteArg(toolPath) + " --root " + QuoteArg(projectRoot),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                summary = label + " failed to start.";
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            summary = output.Trim();
            if (process.ExitCode == 0)
                return true;

            summary = label + " exit=" + process.ExitCode + " stdout=" + output.Trim() + " stderr=" + error.Trim();
            return false;
        }

        private static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static DataSet BuildDataSetFromSources(LocalizationPool localizationPool, out int csvFileCount, out int jsonFileCount)
        {
            DataSet dataSet = new DataSet();

            string[] csvFiles = CollectSourceFiles("*.csv");
            Array.Sort(csvFiles, StringComparer.OrdinalIgnoreCase);
            csvFileCount = csvFiles.Length;
            CsvFileRows[] csvSources = ReadCsvSourcesParallel(csvFiles);
            for (int i = 0; i < csvSources.Length; i++)
                ParseCsv(csvSources[i], dataSet, localizationPool);

            string[] jsonFiles = CollectSourceFiles("*.json");
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
            jsonFileCount = jsonFiles.Length;
            for (int i = 0; i < jsonFiles.Length; i++)
                ParseJson(jsonFiles[i], dataSet, localizationPool);

            return dataSet;
        }

        internal static bool IsSourcePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   !IsGeneratedBalancePath(assetPath) &&
                   (IsUnderAbsoluteRoot(assetPath, SourceFolder) ||
                    IsUnderAbsoluteRoot(assetPath, BalanceSourceFolder));
        }

        internal static bool TryValidateOutputBlob(out string error, bool updateLastError = true)
        {
            bool valid = TryValidateBlobFile(OutputAssetPath, out error);
            if (updateLastError)
                LastError = valid ? string.Empty : error;

            return valid;
        }

        private static string[] CollectSourceFiles(string searchPattern)
        {
            List<string> files = new List<string>(128); // COLD ALLOC: List<string>[source file count] - editor-only source enumeration - owner: H8DataMonolithCompiler
            AppendSourceFiles(files, SourceFolder, searchPattern);
            AppendSourceFiles(files, BalanceSourceFolder, searchPattern);
            return files.ToArray();
        }

        private static CsvFileRows[] ReadCsvSourcesParallel(string[] csvFiles)
        {
            CsvFileRows[] results = new CsvFileRows[csvFiles.Length]; // COLD ALLOC: CsvFileRows[source file count] - editor-only parallel CSV import results - owner: H8DataMonolithCompiler
            if (csvFiles.Length == 0)
                return results;

            int workerCount = Math.Min(csvFiles.Length, Math.Max(1, System.Environment.ProcessorCount - 1));
            Task[] workers = new Task[workerCount]; // COLD ALLOC: Task[bounded worker count] - editor-only CSV import workers - owner: H8DataMonolithCompiler
            int nextIndex = -1;
            for (int i = 0; i < workerCount; i++)
            {
                workers[i] = Task.Run(() =>
                {
                    while (true)
                    {
                        int workerIndex = Interlocked.Increment(ref nextIndex);
                        if (workerIndex >= csvFiles.Length)
                            break;

                        string path = csvFiles[workerIndex];
                        results[workerIndex] = new CsvFileRows(path, ReadCsvRows(path));
                    }
                });
            }

            Task.WaitAll(workers);
            return results;
        }

        private static void AppendSourceFiles(List<string> files, string relativeFolder, string searchPattern)
        {
            string absoluteFolder = Path.GetFullPath(relativeFolder);
            if (!Directory.Exists(absoluteFolder))
                return;

            string[] discovered = Directory.GetFiles(absoluteFolder, searchPattern, SearchOption.AllDirectories);
            for (int i = 0; i < discovered.Length; i++)
            {
                if (!IsGeneratedBalancePath(discovered[i]))
                    files.Add(discovered[i]);
            }
        }

        internal static bool IsGeneratedBalancePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return IsUnderAbsoluteRoot(path, Path.Combine(BalanceSourceFolder, "Baked")) ||
                   IsUnderAbsoluteRoot(path, Path.Combine(BalanceSourceFolder, "Schemas"));
        }

        private static bool IsUnderAbsoluteRoot(string path, string relativeRoot)
        {
            string normalizedPath = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
            string normalizedRoot = Path.GetFullPath(relativeRoot).Replace('\\', '/').TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryWriteValidatedBlob(byte[] blob, out string error)
        {
            error = string.Empty;
            string uniqueSuffix = "." +
                                  Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                                  "." +
                                  DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            string outputPath = string.Empty;
            string tempPath = string.Empty;
            string backupPath = string.Empty;

            try
            {
                outputPath = Path.GetFullPath(OutputAssetPath);
                tempPath = outputPath + uniqueSuffix + TempOutputSuffix;
                backupPath = outputPath + uniqueSuffix + BackupOutputSuffix;
                TryDeleteFile(Path.GetFullPath(OutputAssetPath + TempOutputSuffix));
                TryDeleteFile(Path.GetFullPath(OutputAssetPath + BackupOutputSuffix));
                TryDeleteStalePromoteFiles(outputPath);
                TryDeleteFile(tempPath);
                TryDeleteFile(backupPath);
                if (TryFileExists(outputPath) &&
                    TryValidateBlobFile(outputPath, out _) &&
                    TryFileEqualsBytes(outputPath, blob, out _))
                {
                    return true;
                }

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Write(blob, 0, blob.Length);

                if (!TryValidateBlobFile(tempPath, out error))
                {
                    TryDeleteFile(tempPath);
                    return false;
                }

                if (TryFileExists(outputPath) &&
                    TryValidateBlobFile(outputPath, out _) &&
                    TryFilesEqual(outputPath, tempPath, out _))
                {
                    TryDeleteFile(tempPath);
                    TryDeleteFile(backupPath);
                    return true;
                }

                if (!TryPromoteValidatedBlob(outputPath, tempPath, backupPath, out error))
                {
                    TryDeleteFile(tempPath);
                    TryDeleteFile(backupPath);
                    return false;
                }

                if (!TryValidateBlobFile(outputPath, out error))
                    return false;

                return true;
            }
            catch (IOException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
            catch (UnauthorizedAccessException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
            catch (ArgumentException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
            catch (InvalidOperationException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
            catch (NotSupportedException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
            catch (System.Security.SecurityException ex) { return FailAtomicOutputWrite(ex, tempPath, backupPath, out error); }
        }

        private static bool FailAtomicOutputWrite(Exception ex, string tempPath, string backupPath, out string error)
        {
            error = "Atomic output write failed: " + ex.Message;
            TryDeleteFile(tempPath);
            TryDeleteFile(backupPath);
            return false;
        }

        private static bool TryPromoteValidatedBlob(string outputPath, string tempPath, string backupPath, out string error)
        {
            error = string.Empty;
            if (!TryFileExists(outputPath))
            {
                return TryPromoteNewOutput(tempPath, outputPath, out error);
            }

            try
            {
                Exception lastReplaceException = null;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    try
                    {
                        TryDeleteFile(backupPath);
                        PrepareWritableFile(outputPath);
                        PrepareWritableFile(tempPath);
                        File.Replace(tempPath, outputPath, backupPath, true);
                        TryDeleteFile(backupPath);
                        return true;
                    }
                    catch (IOException ex)
                    {
                        lastReplaceException = ex;
                        Thread.Sleep(15 * (attempt + 1));
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        lastReplaceException = ex;
                        Thread.Sleep(15 * (attempt + 1));
                    }
                    catch (ArgumentException ex)
                    {
                        return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
                    }
                    catch (NotSupportedException ex)
                    {
                        return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
                    }
                    catch (System.Security.SecurityException ex)
                    {
                        return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
                    }
                }

                return TryPromoteAfterReplaceFailure(
                    lastReplaceException ?? new IOException("File.Replace failed without a captured exception."),
                    outputPath,
                    tempPath,
                    backupPath,
                    out error);
            }
            catch (IOException ex)
            {
                return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
            }
            catch (UnauthorizedAccessException ex)
            {
                return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
            }
            catch (ArgumentException ex)
            {
                return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
            }
            catch (NotSupportedException ex)
            {
                return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
            }
            catch (System.Security.SecurityException ex)
            {
                return TryPromoteAfterReplaceFailure(ex, outputPath, tempPath, backupPath, out error);
            }
        }

        private static bool TryPromoteNewOutput(string tempPath, string outputPath, out string error)
        {
            error = string.Empty;
            try
            {
                PrepareWritableFile(tempPath);
                File.Move(tempPath, outputPath);
                return true;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryPromoteAfterReplaceFailure(
            Exception ex,
            string outputPath,
            string tempPath,
            string backupPath,
            out string error)
        {
            error = string.Empty;

            if (TryPromoteWithNativeReplace(outputPath, tempPath, backupPath, out string nativeError))
                return true;

            if (TryPromoteWithRecoverableMove(outputPath, tempPath, backupPath, out string moveError))
                return true;

            if (TryPromoteWithValidatedCopy(outputPath, tempPath, backupPath, out string copyError))
                return true;

            error = "Atomic output promote failed: File.Replace=" + ex.GetType().Name + ": " + ex.Message + "; " + nativeError;
            if (!string.IsNullOrEmpty(moveError))
                error += "; RecoverableMove=" + moveError;
            if (!string.IsNullOrEmpty(copyError))
                error += "; ValidatedCopy=" + copyError;
            return false;
        }

        private static bool TryPromoteWithNativeReplace(string outputPath, string tempPath, string backupPath, out string error)
        {
            error = string.Empty;
            if (System.Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                error = "native replace unavailable on platform " + System.Environment.OSVersion.Platform;
                return false;
            }

            try
            {
                TryDeleteFile(backupPath);
                File.Copy(outputPath, backupPath, true);
            }
            catch (IOException ex)
            {
                error = "backup copy failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = "backup copy failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (ArgumentException ex)
            {
                error = "backup copy failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (NotSupportedException ex)
            {
                error = "backup copy failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (System.Security.SecurityException ex)
            {
                error = "backup copy failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            try
            {
                PrepareWritableFile(outputPath);
                PrepareWritableFile(tempPath);
                if (MoveFileEx(tempPath, outputPath, MoveFileReplaceExisting | MoveFileWriteThrough))
                {
                    TryDeleteFile(backupPath);
                    return true;
                }

                int moveError = Marshal.GetLastWin32Error();
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    MoveFileEx(backupPath, outputPath, MoveFileReplaceExisting | MoveFileWriteThrough);

                error = "MoveFileExW failed with error " + moveError.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryPromoteWithRecoverableMove(string outputPath, string tempPath, string backupPath, out string error)
        {
            error = string.Empty;
            try
            {
                if (!TryFileExists(backupPath))
                    File.Copy(outputPath, backupPath, true);

                PrepareWritableFile(outputPath);
                PrepareWritableFile(tempPath);
                File.Delete(outputPath);
                File.Move(tempPath, outputPath);
                TryDeleteFile(backupPath);
                return true;
            }
            catch (IOException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    TryRestoreMovedBackup(outputPath, backupPath, ref error);

                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    TryRestoreMovedBackup(outputPath, backupPath, ref error);

                return false;
            }
            catch (ArgumentException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    TryRestoreMovedBackup(outputPath, backupPath, ref error);

                return false;
            }
            catch (NotSupportedException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    TryRestoreMovedBackup(outputPath, backupPath, ref error);

                return false;
            }
            catch (System.Security.SecurityException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                if (!TryFileExists(outputPath) && TryFileExists(backupPath))
                    TryRestoreMovedBackup(outputPath, backupPath, ref error);

                return false;
            }
        }

        private static void TryRestoreMovedBackup(string outputPath, string backupPath, ref string error)
        {
            try
            {
                File.Move(backupPath, outputPath);
            }
            catch (IOException restoreEx)
            {
                error += "; restore failed: " + restoreEx.GetType().Name + ": " + restoreEx.Message;
            }
            catch (UnauthorizedAccessException restoreEx)
            {
                error += "; restore failed: " + restoreEx.GetType().Name + ": " + restoreEx.Message;
            }
            catch (ArgumentException restoreEx)
            {
                error += "; restore failed: " + restoreEx.GetType().Name + ": " + restoreEx.Message;
            }
            catch (NotSupportedException restoreEx)
            {
                error += "; restore failed: " + restoreEx.GetType().Name + ": " + restoreEx.Message;
            }
            catch (System.Security.SecurityException restoreEx)
            {
                error += "; restore failed: " + restoreEx.GetType().Name + ": " + restoreEx.Message;
            }
        }

        private static bool TryPromoteWithValidatedCopy(string outputPath, string tempPath, string backupPath, out string error)
        {
            error = string.Empty;
            try
            {
                if (!TryFileExists(backupPath))
                    File.Copy(outputPath, backupPath, true);

                PrepareWritableFile(outputPath);
                PrepareWritableFile(tempPath);
                File.Copy(tempPath, outputPath, true);
                if (!TryValidateBlobFile(outputPath, out string validationError))
                {
                    TryRestoreBackup(outputPath, backupPath, out string restoreError);
                    error = "post-copy validation failed: " + validationError;
                    if (!string.IsNullOrEmpty(restoreError))
                        error += "; restore failed: " + restoreError;
                    return false;
                }

                TryDeleteFile(tempPath);
                TryDeleteFile(backupPath);
                return true;
            }
            catch (IOException ex)
            {
                TryRestoreBackup(outputPath, backupPath, out string restoreError);
                error = ex.GetType().Name + ": " + ex.Message;
                if (!string.IsNullOrEmpty(restoreError))
                    error += "; restore failed: " + restoreError;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                TryRestoreBackup(outputPath, backupPath, out string restoreError);
                error = ex.GetType().Name + ": " + ex.Message;
                if (!string.IsNullOrEmpty(restoreError))
                    error += "; restore failed: " + restoreError;
                return false;
            }
            catch (ArgumentException ex) { return FailValidatedCopy(ex, outputPath, backupPath, out error); }
            catch (NotSupportedException ex) { return FailValidatedCopy(ex, outputPath, backupPath, out error); }
            catch (System.Security.SecurityException ex) { return FailValidatedCopy(ex, outputPath, backupPath, out error); }
        }

        private static bool FailValidatedCopy(Exception ex, string outputPath, string backupPath, out string error)
        {
            TryRestoreBackup(outputPath, backupPath, out string restoreError);
            error = ex.GetType().Name + ": " + ex.Message;
            if (!string.IsNullOrEmpty(restoreError))
                error += "; restore failed: " + restoreError;
            return false;
        }

        private static bool TryRestoreBackup(string outputPath, string backupPath, out string error)
        {
            error = string.Empty;
            if (!TryFileExists(backupPath))
                return false;

            try
            {
                PrepareWritableFile(outputPath);
                File.Copy(backupPath, outputPath, true);
                return true;
            }
            catch (IOException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryFilesEqual(string leftPath, string rightPath, out string error)
        {
            error = string.Empty;
            try
            {
                if (!TryGetFileLength(leftPath, out long leftLength, out error))
                    return false;

                if (!TryGetFileLength(rightPath, out long rightLength, out error))
                    return false;

                if (leftLength != rightLength)
                    return false;

                byte[] leftBuffer = new byte[8192];
                byte[] rightBuffer = new byte[8192];
                using FileStream left = new FileStream(leftPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using FileStream right = new FileStream(rightPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                while (true)
                {
                    int leftRead = left.Read(leftBuffer, 0, leftBuffer.Length);
                    int rightRead = right.Read(rightBuffer, 0, rightBuffer.Length);
                    if (leftRead != rightRead)
                        return false;

                    if (leftRead == 0)
                        return true;

                    for (int i = 0; i < leftRead; i++)
                    {
                        if (leftBuffer[i] != rightBuffer[i])
                            return false;
                    }
                }
            }
            catch (IOException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryFileEqualsBytes(string path, byte[] bytes, out string error)
        {
            error = string.Empty;
            try
            {
                if (!TryGetFileLength(path, out long length, out error))
                    return false;

                if (length != bytes.Length)
                    return false;

                byte[] buffer = new byte[8192];
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(buffer, 0, Math.Min(buffer.Length, bytes.Length - offset));
                    if (read <= 0)
                        return false;

                    for (int i = 0; i < read; i++)
                    {
                        if (buffer[i] != bytes[offset + i])
                            return false;
                    }

                    offset += read;
                }

                return true;
            }
            catch (IOException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryGetFileLength(string path, out long length, out string error)
        {
            length = 0L;
            error = string.Empty;
            try
            {
                length = new FileInfo(path).Length;
                return true;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool FailFileOperation(Exception ex, out string error)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }

        private static void PrepareWritableFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (!File.Exists(path))
                    return;

                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
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

        private static void TryDeleteStalePromoteFiles(string outputPath)
        {
            string[] candidates;
            try
            {
                string directory = Path.GetDirectoryName(outputPath);
                string fileName = Path.GetFileName(outputPath);
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
                    return;

                candidates = Directory.GetFiles(directory, fileName + "*");
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (NotSupportedException)
            {
                return;
            }
            catch (System.Security.SecurityException)
            {
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                try
                {
                    if (string.Equals(Path.GetFullPath(candidate), outputPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string candidateName = Path.GetFileName(candidate);
                    if (!candidateName.StartsWith(Path.GetFileName(outputPath) + ".", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (candidate.EndsWith(TempOutputSuffix, StringComparison.OrdinalIgnoreCase) ||
                        candidate.EndsWith(BackupOutputSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteFile(candidate);
                    }
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
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (!File.Exists(path))
                        return;

                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

                    File.Delete(path);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(20 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(20 * (attempt + 1));
                }
                catch (ArgumentException)
                {
                    return;
                }
                catch (NotSupportedException)
                {
                    return;
                }
                catch (System.Security.SecurityException)
                {
                    return;
                }

                if (!TryFileExists(path))
                    return;
            }
        }

        private static bool TryFileExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                return File.Exists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

        internal static bool TryValidateBlobFile(string path, out string error)
        {
            error = string.Empty;
            if (!TryFileExists(path))
            {
                error = "Missing Data Monolith output: " + path;
                return false;
            }

            if (!TryGetFileLength(path, out long fileLength, out error))
                return false;

            if (fileLength < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                error = "Data Monolith is too small: " + fileLength + " bytes.";
                return false;
            }

            if (fileLength > int.MaxValue || fileLength > uint.MaxValue)
            {
                error = "Data Monolith is too large for the current runtime contract: " + fileLength + " bytes.";
                return false;
            }

            if ((fileLength & (H8DataLayoutConstants.SectionAlignmentBytes - 1)) != 0)
            {
                error = "Data Monolith file length is not 64-byte aligned: " + fileLength + " bytes.";
                return false;
            }

            int sectionTableBytes = SectionOrder.Length * UnsafeUtility.SizeOf<H8DataSectionEntry>();
            int prefixBytes = H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + sectionTableBytes;
            byte[] prefix = new byte[prefixBytes]; // COLD ALLOC: byte[prefixBytes] - editor validation reads fixed h8bin header/directory/table only - owner: H8DataMonolithCompiler
            if (!TryReadExactRange(path, 0L, prefix, prefix.Length, out error))
                return false;

            if (!ValidateBlobPrefix(prefix, fileLength, out ulong checksum, out error))
                return false;

            if (!TryComputeFileHash64(
                    path,
                    H8DataLayoutConstants.HeaderSizeBytes,
                    fileLength - H8DataLayoutConstants.HeaderSizeBytes,
                    out ulong computedChecksum,
                    out error))
            {
                return false;
            }

            if (checksum == computedChecksum)
                return true;

            error = "XXHash3 checksum mismatch: stored=0x" + checksum.ToString("X16") + " computed=0x" + computedChecksum.ToString("X16");
            return false;
        }

        private static bool TryReadExact(string path, byte[] bytes, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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

                error = "Data Monolith read was incomplete: " + total + "/" + bytes.Length + " bytes.";
                return false;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool TryReadExactRange(string path, long offset, byte[] bytes, int count, out string error)
        {
            error = string.Empty;
            if (offset < 0L || count < 0 || count > bytes.Length)
            {
                error = "Invalid Data Monolith read range: offset=" + offset + " count=" + count + " buffer=" + bytes.Length;
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < offset + count)
                {
                    error = "Data Monolith read range exceeds file length: offset=" + offset + " count=" + count + " length=" + stream.Length;
                    return false;
                }

                stream.Position = offset;
                int total = 0;
                while (total < count)
                {
                    int read = stream.Read(bytes, total, count - total);
                    if (read <= 0)
                        break;

                    total += read;
                }

                if (total == count)
                    return true;

                error = "Data Monolith range read was incomplete: " + total + "/" + count + " bytes.";
                return false;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        internal static unsafe bool TryComputeFileHash64(string path, long offset, long count, out ulong hash, out string error)
        {
            hash = 0UL;
            error = string.Empty;
            if (offset < 0L || count < 0L)
            {
                error = "Invalid Data Monolith hash range: offset=" + offset + " count=" + count;
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
                if (stream.Length < offset + count)
                {
                    error = "Data Monolith hash range exceeds file length: offset=" + offset + " count=" + count + " length=" + stream.Length;
                    return false;
                }

                stream.Position = offset;
                byte[] scratch = new byte[64 * 1024]; // COLD ALLOC: byte[65536] - bounded editor streaming hash scratch - owner: H8DataMonolithCompiler
                xxHash3.StreamingState state = new xxHash3.StreamingState(true);
                long remaining = count;
                while (remaining > 0L)
                {
                    int requested = remaining > scratch.Length ? scratch.Length : (int)remaining;
                    int read = stream.Read(scratch, 0, requested);
                    if (read <= 0)
                    {
                        error = "Data Monolith hash read was incomplete: remaining=" + remaining;
                        return false;
                    }

                    fixed (byte* ptr = scratch)
                        state.Update(ptr, read);

                    remaining -= read;
                }

                uint2 digest = state.DigestHash64();
                hash = ((ulong)digest.y << 32) | digest.x;
                return true;
            }
            catch (IOException ex) { return FailFileOperation(ex, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileOperation(ex, out error); }
            catch (ArgumentException ex) { return FailFileOperation(ex, out error); }
            catch (NotSupportedException ex) { return FailFileOperation(ex, out error); }
            catch (System.Security.SecurityException ex) { return FailFileOperation(ex, out error); }
        }

        private static bool ValidateBlobBytes(byte[] bytes, out string error)
        {
            if (!ValidateBlobPrefix(bytes, bytes.Length, out ulong checksum, out error))
                return false;

            ulong computedChecksum = ComputeHash64(bytes, H8DataLayoutConstants.HeaderSizeBytes, bytes.Length - H8DataLayoutConstants.HeaderSizeBytes);
            if (checksum == computedChecksum)
                return true;

            error = "XXHash3 checksum mismatch: stored=0x" + checksum.ToString("X16") + " computed=0x" + computedChecksum.ToString("X16");
            return false;
        }

        private static bool ValidateBlobPrefix(byte[] bytes, long blobLength, out ulong checksum, out string error)
        {
            error = string.Empty;
            checksum = 0UL;
            uint headerMagic = ReadUInt32(bytes, 0);
            ushort headerVersion = ReadUInt16(bytes, 4);
            ushort headerBytes = ReadUInt16(bytes, 6);
            checksum = ReadUInt64(bytes, 8);
            uint headerBlobBytes = ReadUInt32(bytes, 16);
            uint headerDirectoryOffset = ReadUInt32(bytes, 20);
            uint headerDirectoryBytes = ReadUInt32(bytes, 24);
            uint headerSectionTableOffset = ReadUInt32(bytes, 28);
            uint headerSectionCount = ReadUInt32(bytes, 32);
            uint headerFlags = ReadUInt32(bytes, 36);
            uint headerWorldSeed = ReadUInt32(bytes, 40);
            uint headerAppVersionHash = ReadUInt32(bytes, 44);
            uint headerSchemaHash = ReadUInt32(bytes, 48);
            uint headerReserved0 = ReadUInt32(bytes, 52);
            uint headerReserved1 = ReadUInt32(bytes, 56);
            uint headerReserved2 = ReadUInt32(bytes, 60);
            if (headerMagic != H8DataLayoutConstants.BlobMagic)
            {
                error = "Header magic mismatch: 0x" + headerMagic.ToString("X8");
                return false;
            }

            if (headerVersion != H8DataLayoutConstants.FormatVersion)
            {
                error = "Header version mismatch: " + headerVersion;
                return false;
            }

            if (headerBytes != H8DataLayoutConstants.HeaderSizeMarker)
            {
                error = "Header byte-count mismatch: " + headerBytes;
                return false;
            }

            uint expectedDirectoryOffset = H8DataLayoutConstants.HeaderSizeBytes;
            uint expectedDirectoryBytes = H8DataLayoutConstants.DirectorySizeBytes;
            uint expectedSectionTableOffset = expectedDirectoryOffset + expectedDirectoryBytes;
            uint expectedSectionTableBytes = (uint)(SectionOrder.Length * UnsafeUtility.SizeOf<H8DataSectionEntry>());
            if (headerBlobBytes != blobLength ||
                headerDirectoryOffset != expectedDirectoryOffset ||
                headerDirectoryBytes != expectedDirectoryBytes ||
                headerSectionTableOffset != expectedSectionTableOffset ||
                headerSectionCount != SectionOrder.Length ||
                headerFlags != H8DataLayoutConstants.BlobFlagLittleEndian ||
                headerSchemaHash != H8DataLayoutConstants.SchemaHash ||
                headerReserved0 != 0u ||
                headerReserved1 != 0u ||
                headerReserved2 != 0u)
            {
                error = "Header schema range mismatch: blob=" + headerBlobBytes +
                        " dirOffset=" + headerDirectoryOffset +
                        " dirBytes=" + headerDirectoryBytes +
                        " tableOffset=" + headerSectionTableOffset +
                        " sections=" + headerSectionCount +
                        " flags=0x" + headerFlags.ToString("X8") +
                        " schema=0x" + headerSchemaHash.ToString("X8") +
                        " reserved=" + headerReserved0 + "/" + headerReserved1 + "/" + headerReserved2;
                return false;
            }

            int directoryOffset = H8DataLayoutConstants.HeaderSizeBytes;
            uint directoryMagic = ReadUInt32(bytes, directoryOffset);
            ushort directoryVersion = ReadUInt16(bytes, directoryOffset + 4);
            ushort sectionCount = ReadUInt16(bytes, directoryOffset + 6);
            uint sectionTableOffset = ReadUInt32(bytes, directoryOffset + 8);
            uint sectionTableBytes = ReadUInt32(bytes, directoryOffset + 12);
            uint blobBytes = ReadUInt32(bytes, directoryOffset + 16);
            uint dataStartOffset = ReadUInt32(bytes, directoryOffset + 20);
            uint localizationOffset = ReadUInt32(bytes, directoryOffset + 24);
            uint localizationBytes = ReadUInt32(bytes, directoryOffset + 28);
            uint directoryFlags = ReadUInt32(bytes, directoryOffset + 32);
            uint directoryWorldSeed = ReadUInt32(bytes, directoryOffset + 36);
            uint directoryAppVersionHash = ReadUInt32(bytes, directoryOffset + 40);
            uint directoryReserved0 = ReadUInt32(bytes, directoryOffset + 44);
            uint directoryReserved1 = ReadUInt32(bytes, directoryOffset + 48);
            uint directoryReserved2 = ReadUInt32(bytes, directoryOffset + 52);
            uint directoryReserved3 = ReadUInt32(bytes, directoryOffset + 56);
            uint directoryReserved4 = ReadUInt32(bytes, directoryOffset + 60);
            if (directoryMagic != H8DataLayoutConstants.BlobMagic)
            {
                error = "Directory magic mismatch: 0x" + directoryMagic.ToString("X8");
                return false;
            }

            if (directoryVersion != H8DataLayoutConstants.FormatVersion)
            {
                error = "Directory version mismatch: " + directoryVersion;
                return false;
            }

            if (sectionCount != SectionOrder.Length)
            {
                error = "Section count mismatch: " + sectionCount + " expected " + SectionOrder.Length;
                return false;
            }

            if (sectionTableOffset != expectedSectionTableOffset || sectionTableBytes != expectedSectionTableBytes)
            {
                error = "Section table range mismatch: offset=" + sectionTableOffset + " bytes=" + sectionTableBytes;
                return false;
            }

            if (directoryFlags != headerFlags ||
                directoryWorldSeed != headerWorldSeed ||
                directoryAppVersionHash != headerAppVersionHash ||
                directoryReserved0 != 0u ||
                directoryReserved1 != 0u ||
                directoryReserved2 != 0u ||
                directoryReserved3 != 0u ||
                directoryReserved4 != 0u)
            {
                error = "Header/directory identity mismatch: flags=0x" + directoryFlags.ToString("X8") +
                        " worldSeed=" + directoryWorldSeed +
                        " appHash=0x" + directoryAppVersionHash.ToString("X8") +
                        " reserved=" + directoryReserved0 + "/" + directoryReserved1 + "/" + directoryReserved2 + "/" + directoryReserved3 + "/" + directoryReserved4;
                return false;
            }

            if (blobBytes != blobLength)
            {
                error = "Directory blob byte-count mismatch: " + blobBytes + " expected " + blobLength;
                return false;
            }

            uint expectedDataStartOffset = AlignUp(sectionTableOffset + sectionTableBytes, (uint)H8DataLayoutConstants.SectionAlignmentBytes);
            if (dataStartOffset != expectedDataStartOffset ||
                (dataStartOffset & ((uint)H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
            {
                error = "Data start offset mismatch or alignment failure: " + dataStartOffset;
                return false;
            }

            long sectionTableEnd = (long)sectionTableOffset + sectionTableBytes;
            if (sectionTableEnd > bytes.Length)
            {
                error = "Section table exceeds blob length.";
                return false;
            }

            bool sawLocalization = false;
            ulong expectedSectionOffset = dataStartOffset;
            for (int i = 0; i < SectionOrder.Length; i++)
            {
                int entryOffset = (int)sectionTableOffset + (i * UnsafeUtility.SizeOf<H8DataSectionEntry>());
                uint sectionId = ReadUInt32(bytes, entryOffset);
                uint recordSize = ReadUInt32(bytes, entryOffset + 4);
                uint count = ReadUInt32(bytes, entryOffset + 8);
                uint offset = ReadUInt32(bytes, entryOffset + 12);
                H8DataSectionId expectedId = SectionOrder[i];
                if (sectionId != (uint)expectedId)
                {
                    error = "Section order mismatch at index " + i + ": got=" + sectionId + " expected=" + (uint)expectedId;
                    return false;
                }

                uint expectedRecordSize = GetExpectedRecordSize(expectedId);
                if (recordSize != expectedRecordSize)
                {
                    error = expectedId + " record size mismatch: " + recordSize + " expected " + expectedRecordSize;
                    return false;
                }

                if (count == 0u)
                {
                    if (offset != 0u)
                    {
                        error = expectedId + " empty section has non-zero offset: " + offset;
                        return false;
                    }

                    continue;
                }

                if ((offset & ((uint)H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
                {
                    error = expectedId + " section offset is not " + H8DataLayoutConstants.SectionAlignmentBytes + "-byte aligned: " + offset;
                    return false;
                }

                if (offset < dataStartOffset)
                {
                    error = expectedId + " section offset overlaps the fixed header/directory/table area: " + offset;
                    return false;
                }

                ulong sectionBytes = (ulong)recordSize * count;
                if ((ulong)offset + sectionBytes > (ulong)blobLength)
                {
                    error = expectedId + " section range exceeds blob length.";
                    return false;
                }

                if ((ulong)offset != expectedSectionOffset)
                {
                    error = expectedId + " section offset is not canonical: got=" + offset + " expected=" + expectedSectionOffset;
                    return false;
                }

                expectedSectionOffset = AlignUp((ulong)offset + sectionBytes, (uint)H8DataLayoutConstants.SectionAlignmentBytes);
                if (expectedSectionOffset > (ulong)blobLength + (uint)H8DataLayoutConstants.SectionAlignmentBytes)
                {
                    error = expectedId + " canonical section cursor overflow.";
                    return false;
                }

                if (expectedId == H8DataSectionId.LocalizationUtf8)
                {
                    sawLocalization = true;
                    if (localizationOffset != offset || localizationBytes != count)
                    {
                        error = "Directory localization range does not match section table.";
                        return false;
                    }
                }
            }

            if (localizationBytes > 0u && !sawLocalization)
            {
                error = "Directory declares localization bytes but section table has no localization range.";
                return false;
            }

            return true;
        }

        private static uint GetExpectedRecordSize(H8DataSectionId sectionId)
        {
            uint recordSize = H8DataLayoutAudit.GetExpectedRecordSize(sectionId);
            if (recordSize == 0u)
                throw new ArgumentOutOfRangeException(nameof(sectionId), sectionId, null);

            return recordSize;
        }

        private static byte[] BuildBlob(DataSet dataSet, LocalizationPool localizationPool)
        {
            H8DataSectionEntry[] entries = new H8DataSectionEntry[SectionOrder.Length]; // COLD ALLOC: H8DataSectionEntry[section count] - editor-only section table patch scratch - owner: H8DataMonolithCompiler
            using MemoryStream stream = new MemoryStream(InitialBlobCapacity);
            int sectionTableOffset = H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes;
            int sectionTableBytes = SectionOrder.Length * UnsafeUtility.SizeOf<H8DataSectionEntry>();
            WriteZeros(stream, sectionTableOffset + sectionTableBytes);
            AlignSection(stream);

            for (int i = 0; i < SectionOrder.Length; i++)
            {
                H8DataSectionId sectionId = SectionOrder[i];
                switch (sectionId)
                {
                    case H8DataSectionId.Items:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Items, H8DataLayoutConstants.ItemRecordSize);
                        break;
                    case H8DataSectionId.Creatures:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Creatures, H8DataLayoutConstants.CreatureTraitRecordSize);
                        break;
                    case H8DataSectionId.Biomes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Biomes, H8DataLayoutConstants.BiomeRecordSize);
                        break;
                    case H8DataSectionId.Recipes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Recipes, UnsafeUtility.SizeOf<H8RecipeRecord>());
                        break;
                    case H8DataSectionId.BiomeHeatmap:
                        entries[i] = AppendSection(stream, sectionId, dataSet.BiomeHeatmap, UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>());
                        break;
                    case H8DataSectionId.QuestNodes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.QuestNodes, UnsafeUtility.SizeOf<H8QuestNodeRecord>());
                        break;
                    case H8DataSectionId.QuestEdges:
                        entries[i] = AppendSection(stream, sectionId, dataSet.QuestEdges, UnsafeUtility.SizeOf<H8QuestEdgeRecord>());
                        break;
                    case H8DataSectionId.LootCdf:
                        entries[i] = AppendSection(stream, sectionId, dataSet.LootCdf, UnsafeUtility.SizeOf<H8LootCdfRecord>());
                        break;
                    case H8DataSectionId.VoxelMaterials:
                        entries[i] = AppendSection(stream, sectionId, dataSet.VoxelMaterials, UnsafeUtility.SizeOf<H8VoxelMaterialRecord>());
                        break;
                    case H8DataSectionId.AudioClipRegistry:
                        entries[i] = AppendSection(stream, sectionId, dataSet.AudioClips, UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>());
                        break;
                    case H8DataSectionId.VfxScalars:
                        entries[i] = AppendSection(stream, sectionId, dataSet.VfxScalars, UnsafeUtility.SizeOf<H8VfxScalarRecord>());
                        break;
                    case H8DataSectionId.DepthPressureCurve:
                        entries[i] = AppendSection(stream, sectionId, dataSet.DepthPressureCurve, UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>());
                        break;
                    case H8DataSectionId.ToolHeatCapacity:
                        entries[i] = AppendSection(stream, sectionId, dataSet.ToolHeat, UnsafeUtility.SizeOf<H8ToolHeatCapacityRecord>());
                        break;
                    case H8DataSectionId.SubmarineHullConstants:
                        entries[i] = AppendSection(stream, sectionId, dataSet.HullConstants, UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>());
                        break;
                    case H8DataSectionId.NarrativeTriggers:
                        entries[i] = AppendSection(stream, sectionId, dataSet.NarrativeTriggers, UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>());
                        break;
                    case H8DataSectionId.PhysicsMaterials:
                        entries[i] = AppendSection(stream, sectionId, dataSet.PhysicsMaterials, UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>());
                        break;
                    case H8DataSectionId.GhostModules:
                        entries[i] = AppendSection(stream, sectionId, dataSet.GhostModules, UnsafeUtility.SizeOf<H8GhostModuleRecord>());
                        break;
                    case H8DataSectionId.RadiationIntensityMap:
                        entries[i] = AppendSection(stream, sectionId, dataSet.RadiationCells, UnsafeUtility.SizeOf<H8RadiationIntensityCellRecord>());
                        break;
                    case H8DataSectionId.SpawnCreditCosts:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SpawnCredits, UnsafeUtility.SizeOf<H8SpawnCreditCostRecord>());
                        break;
                    case H8DataSectionId.LightAttenuationCurve:
                        entries[i] = AppendSection(stream, sectionId, dataSet.LightAttenuationCurve, UnsafeUtility.SizeOf<H8LightAttenuationSampleRecord>());
                        break;
                    case H8DataSectionId.SopErrors:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SopErrors, UnsafeUtility.SizeOf<H8SopErrorRecord>());
                        break;
                    case H8DataSectionId.HudLayouts:
                        entries[i] = AppendSection(stream, sectionId, dataSet.HudLayouts, UnsafeUtility.SizeOf<H8HudLayoutRecord>());
                        break;
                    case H8DataSectionId.LocalizationUtf8:
                        entries[i] = AppendLocalizationSection(stream, localizationPool);
                        break;
                    case H8DataSectionId.SectorPageDirectory:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SectorPages, UnsafeUtility.SizeOf<H8SectorPageRecord>());
                        break;
                    case H8DataSectionId.Economy:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Economy, H8DataLayoutConstants.EconomyRecordSize);
                        break;
                    case H8DataSectionId.PhysicsConstants:
                        entries[i] = AppendSection(stream, sectionId, dataSet.PhysicsConstants, H8DataLayoutConstants.PhysicsConstantsRecordSize);
                        break;
                    case H8DataSectionId.AppliedLorePackets:
                        entries[i] = AppendSection(stream, sectionId, dataSet.AppliedLorePackets, H8DataLayoutConstants.AppliedLorePacketRecordSize);
                        break;
                    case H8DataSectionId.AppliedLoreRoutes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.AppliedLoreRoutes, H8DataLayoutConstants.AppliedLoreRouteRecordSize);
                        break;
                }
            }

            AlignSection(stream);

            uint appVersionHash = H8DataHash.ComputeFnv1A32(Application.version.AsSpan());
            H8DataBlobDirectory directory = new H8DataBlobDirectory
            {
                Magic = H8DataLayoutConstants.BlobMagic,
                FormatVersion = H8DataLayoutConstants.FormatVersion,
                SectionCount = (ushort)SectionOrder.Length,
                SectionTableOffset = (uint)sectionTableOffset,
                SectionTableBytes = (uint)sectionTableBytes,
                BlobBytes = (uint)stream.Length,
                DataStartOffset = AlignUp((uint)(sectionTableOffset + sectionTableBytes), (uint)H8DataLayoutConstants.SectionAlignmentBytes),
                Flags = H8DataLayoutConstants.BlobFlagLittleEndian,
                WorldSeed = 0u,
                AppVersionHash = appVersionHash
            };

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].SectionId == (uint)H8DataSectionId.LocalizationUtf8)
                {
                    directory.LocalizationOffset = entries[i].OffsetBytes;
                    directory.LocalizationBytes = entries[i].Count;
                    break;
                }
            }

            long previousPosition = stream.Position;
            stream.Position = H8DataLayoutConstants.HeaderSizeBytes;
            WriteDirectory(stream, in directory);
            stream.Position = sectionTableOffset;
            for (int i = 0; i < entries.Length; i++)
                WriteSectionEntry(stream, in entries[i]);
            stream.Position = previousPosition;

            byte[] blob = stream.ToArray();
            H8DataBlobHeader header = new H8DataBlobHeader
            {
                Magic = H8DataLayoutConstants.BlobMagic,
                FormatVersion = H8DataLayoutConstants.FormatVersion,
                HeaderBytes = H8DataLayoutConstants.HeaderSizeMarker,
                Checksum64 = ComputeHash64(blob, H8DataLayoutConstants.HeaderSizeBytes, blob.Length - H8DataLayoutConstants.HeaderSizeBytes),
                BlobBytes = (uint)blob.Length,
                DirectoryOffset = H8DataLayoutConstants.HeaderSizeBytes,
                DirectoryBytes = H8DataLayoutConstants.DirectorySizeBytes,
                SectionTableOffset = (uint)sectionTableOffset,
                SectionCount = (uint)SectionOrder.Length,
                Flags = H8DataLayoutConstants.BlobFlagLittleEndian,
                WorldSeed = directory.WorldSeed,
                AppVersionHash = directory.AppVersionHash,
                SchemaHash = H8DataLayoutConstants.SchemaHash
            };

            WriteHeader(blob, in header);

            return blob;
        }

        private static void FinalizeGeneratedTables(DataSet dataSet)
        {
            dataSet.Items.Sort(CompareItemRecords);
            for (int i = 0; i < dataSet.Items.Count; i++)
            {
                H8ItemRecord record = dataSet.Items[i];
                record.RecordIndex = (uint)i;
                dataSet.Items[i] = record;
                if (i > 0 && dataSet.Items[i - 1].HashId == record.HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate item hash detected: 0x" + record.HashId.ToString("X8"));
            }

            dataSet.Creatures.Sort(CompareCreatureRecords);
            for (int i = 0; i < dataSet.Creatures.Count; i++)
            {
                H8CreatureTraitRecord record = dataSet.Creatures[i];
                record.RecordIndex = (uint)i;
                dataSet.Creatures[i] = record;
                if (i > 0 && dataSet.Creatures[i - 1].SpeciesHash == record.SpeciesHash)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate creature hash detected: 0x" + record.SpeciesHash.ToString("X8"));
            }

            dataSet.Biomes.Sort(CompareBiomeRecords);
            for (int i = 0; i < dataSet.Biomes.Count; i++)
            {
                H8BiomeRecord record = dataSet.Biomes[i];
                record.RecordIndex = (uint)i;
                dataSet.Biomes[i] = record;
                if (i > 0 && dataSet.Biomes[i - 1].BiomeHash == record.BiomeHash)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate biome hash detected: 0x" + record.BiomeHash.ToString("X8"));
            }

            if (dataSet.DepthPressureCurve.Count == 0)
                GenerateDepthPressureCurve(dataSet.DepthPressureCurve);

            if (dataSet.LightAttenuationCurve.Count == 0)
                GenerateLightAttenuationCurve(dataSet.LightAttenuationCurve);

            NormalizeBiomeHeatmap(dataSet);
            RebuildLootCdf(dataSet);
            dataSet.VoxelMaterials.Sort(CompareVoxelMaterialRecords);
            dataSet.AudioClips.Sort(CompareAudioClipRecords);
            dataSet.HullConstants.Sort(CompareHullConstantRecords);
            dataSet.PhysicsMaterials.Sort(ComparePhysicsMaterialRecords);
            dataSet.Economy.Sort(CompareEconomyRecords);
            dataSet.PhysicsConstants.Sort(ComparePhysicsConstantsRecords);
            dataSet.AppliedLorePackets.Sort(CompareAppliedLoreRecords);
            dataSet.AppliedLoreRoutes.Sort(CompareAppliedLoreRouteRecords);

            for (int i = 1; i < dataSet.Economy.Count; i++)
                if (dataSet.Economy[i - 1].HashId == dataSet.Economy[i].HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate economy hash detected: 0x" + dataSet.Economy[i].HashId.ToString("X8"));

            for (int i = 1; i < dataSet.PhysicsConstants.Count; i++)
                if (dataSet.PhysicsConstants[i - 1].HashId == dataSet.PhysicsConstants[i].HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate physics constant hash detected: 0x" + dataSet.PhysicsConstants[i].HashId.ToString("X8"));

            for (int i = 0; i < dataSet.AppliedLorePackets.Count; i++)
            {
                H8AppliedLorePacketRecord record = dataSet.AppliedLorePackets[i];
                record.RecordIndex = (uint)i;
                dataSet.AppliedLorePackets[i] = record;
                if (i > 0 &&
                    dataSet.AppliedLorePackets[i - 1].PacketHash == record.PacketHash &&
                    dataSet.AppliedLorePackets[i - 1].LocaleHash == record.LocaleHash)
                {
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate applied lore packet/locale detected: packet=0x" + record.PacketHash.ToString("X8") + " locale=0x" + record.LocaleHash.ToString("X8"));
                }
            }

            for (int i = 0; i < dataSet.AppliedLoreRoutes.Count; i++)
            {
                H8AppliedLoreRouteRecord record = dataSet.AppliedLoreRoutes[i];
                record.RecordIndex = (uint)i;
                dataSet.AppliedLoreRoutes[i] = record;
                if (i > 0 && dataSet.AppliedLoreRoutes[i - 1].RouteCardHash == record.RouteCardHash)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate applied lore route detected: route=0x" + record.RouteCardHash.ToString("X8"));
            }
        }

        private static void ValidateCrossReferences(DataSet dataSet)
        {
            ValidateRequiredRecordIdentities(dataSet);
            ValidateUniqueProductionHashes(dataSet);
            ValidateProductionNumericRanges(dataSet);

            HashSet<uint> itemHashes = new HashSet<uint>(dataSet.Items.Count); // COLD ALLOC: HashSet<uint>[item count] - editor-only cross-reference validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.Items.Count; i++)
                itemHashes.Add(dataSet.Items[i].HashId);

            HashSet<uint> creatureHashes = new HashSet<uint>(dataSet.Creatures.Count); // COLD ALLOC: HashSet<uint>[creature count] - editor-only semantic validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.Creatures.Count; i++)
                creatureHashes.Add(dataSet.Creatures[i].SpeciesHash);

            HashSet<uint> biomeHashes = new HashSet<uint>(dataSet.Biomes.Count); // COLD ALLOC: HashSet<uint>[biome count] - editor-only semantic validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.Biomes.Count; i++)
                biomeHashes.Add(dataSet.Biomes[i].BiomeHash);

            HashSet<uint> recipeOutputHashes = new HashSet<uint>(dataSet.Recipes.Count); // COLD ALLOC: HashSet<uint>[recipe count] - editor-only semantic validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.Recipes.Count; i++)
                recipeOutputHashes.Add(dataSet.Recipes[i].OutputHash);

            HashSet<uint> voxelMaterialHashes = new HashSet<uint>(dataSet.VoxelMaterials.Count); // COLD ALLOC: HashSet<uint>[voxel material count] - editor-only semantic validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.VoxelMaterials.Count; i++)
                voxelMaterialHashes.Add(dataSet.VoxelMaterials[i].VoxelHash);

            HashSet<uint> physicsSurfaceHashes = new HashSet<uint>(dataSet.PhysicsMaterials.Count); // COLD ALLOC: HashSet<uint>[physics material count] - editor-only semantic validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.PhysicsMaterials.Count; i++)
                physicsSurfaceHashes.Add(dataSet.PhysicsMaterials[i].SurfaceHash);

            HashSet<uint> appliedLorePacketHashes = new HashSet<uint>(dataSet.AppliedLorePackets.Count); // COLD ALLOC: HashSet<uint>[applied lore packet count] - editor-only route validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.AppliedLorePackets.Count; i++)
                appliedLorePacketHashes.Add(dataSet.AppliedLorePackets[i].PacketHash);

            for (int i = 0; i < dataSet.RawItemRows.Count; i++)
                ValidatePackedItemReferences(dataSet.RawItemRows[i], "item.recipe", "recipe", Get(dataSet.RawItemRows[i], "recipe", string.Empty), itemHashes);

            for (int i = 0; i < dataSet.RawRecipeRows.Count; i++)
                ValidateRecipeItemReferences(dataSet.RawRecipeRows[i], itemHashes);

            for (int i = 0; i < dataSet.RawLootRows.Count; i++)
            {
                ValidateOptionalItemReference(dataSet.RawLootRows[i], "loot.item_id", "item_id", itemHashes);
                ValidateOptionalItemReference(dataSet.RawLootRows[i], "loot.item", "item", itemHashes);
            }

            for (int i = 0; i < dataSet.RawEconomyRows.Count; i++)
                ValidateEconomyItemReferences(dataSet.RawEconomyRows[i], itemHashes);

            ValidateSemanticRecordReferences(dataSet, itemHashes, creatureHashes, biomeHashes, recipeOutputHashes, voxelMaterialHashes, physicsSurfaceHashes, appliedLorePacketHashes);
        }

        private static void ValidateRequiredRecordIdentities(DataSet dataSet)
        {
            for (int i = 0; i < dataSet.Items.Count; i++)
                RequireNonZeroRecordValue(dataSet.Items[i].HashId, "Items", i, "HashId");

            for (int i = 0; i < dataSet.Creatures.Count; i++)
                RequireNonZeroRecordValue(dataSet.Creatures[i].SpeciesHash, "Creatures", i, "SpeciesHash");

            for (int i = 0; i < dataSet.Biomes.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.Biomes[i].BiomeHash, "Biomes", i, "BiomeHash");
                RequireNonZeroRecordValue(dataSet.Biomes[i].SurfaceId, "Biomes", i, "SurfaceId");
            }

            for (int i = 0; i < dataSet.Recipes.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.Recipes[i].OutputHash, "Recipes", i, "OutputHash");
                RequireNonZeroRecordValue(dataSet.Recipes[i].IngredientCount, "Recipes", i, "IngredientCount");
            }

            for (int i = 0; i < dataSet.LootCdf.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.LootCdf[i].TableHash, "LootCdf", i, "TableHash");
                RequireNonZeroRecordValue(dataSet.LootCdf[i].ItemHash, "LootCdf", i, "ItemHash");
                RequireNonZeroRecordValue(dataSet.LootCdf[i].CumulativeWeight, "LootCdf", i, "CumulativeWeight");
                RequireNonZeroRecordValue(dataSet.LootCdf[i].TotalWeight, "LootCdf", i, "TotalWeight");
            }

            for (int i = 0; i < dataSet.VoxelMaterials.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.VoxelMaterials[i].VoxelHash, "VoxelMaterials", i, "VoxelHash");
                RequireNonZeroRecordValue(dataSet.VoxelMaterials[i].YieldHash, "VoxelMaterials", i, "YieldHash");
                RequireNonZeroRecordValue(dataSet.VoxelMaterials[i].SurfaceId, "VoxelMaterials", i, "SurfaceId");
            }

            for (int i = 0; i < dataSet.AudioClips.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.AudioClips[i].EventHash, "AudioClipRegistry", i, "EventHash");
                RequireNonZeroRecordValue(dataSet.AudioClips[i].AddressableKeyUtf8ByteLength, "AudioClipRegistry", i, "AddressableKeyUtf8ByteLength");
            }

            for (int i = 0; i < dataSet.VfxScalars.Count; i++)
                RequireNonZeroRecordValue(dataSet.VfxScalars[i].EffectHash, "VfxScalars", i, "EffectHash");

            for (int i = 0; i < dataSet.ToolHeat.Count; i++)
                RequireNonZeroRecordValue(dataSet.ToolHeat[i].ToolHash, "ToolHeatCapacity", i, "ToolHash");

            for (int i = 0; i < dataSet.HullConstants.Count; i++)
                RequireNonZeroRecordValue(dataSet.HullConstants[i].PartHash, "SubmarineHullConstants", i, "PartHash");

            for (int i = 0; i < dataSet.PhysicsMaterials.Count; i++)
                RequireNonZeroRecordValue(dataSet.PhysicsMaterials[i].SurfaceHash, "PhysicsMaterials", i, "SurfaceHash");

            for (int i = 0; i < dataSet.GhostModules.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.GhostModules[i].ModuleHash, "GhostModules", i, "ModuleHash");
                RequireNonZeroRecordValue(dataSet.GhostModules[i].RecipeHash, "GhostModules", i, "RecipeHash");
            }

            for (int i = 0; i < dataSet.SpawnCredits.Count; i++)
                RequireNonZeroRecordValue(dataSet.SpawnCredits[i].EntityHash, "SpawnCreditCosts", i, "EntityHash");

            for (int i = 0; i < dataSet.SopErrors.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.SopErrors[i].ErrorHash, "SopErrors", i, "ErrorHash");
                RequireNonZeroRecordValue(dataSet.SopErrors[i].MessageUtf8ByteLength, "SopErrors", i, "MessageUtf8ByteLength");
            }

            for (int i = 0; i < dataSet.HudLayouts.Count; i++)
                RequireNonZeroRecordValue(dataSet.HudLayouts[i].ElementHash, "HudLayouts", i, "ElementHash");

            for (int i = 0; i < dataSet.SectorPages.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.SectorPages[i].SectorHash, "SectorPageDirectory", i, "SectorHash");
                RequireNonZeroRecordValue(dataSet.SectorPages[i].BiomeHash, "SectorPageDirectory", i, "BiomeHash");
            }

            for (int i = 0; i < dataSet.Economy.Count; i++)
                RequireNonZeroRecordValue(dataSet.Economy[i].HashId, "Economy", i, "HashId");

            for (int i = 0; i < dataSet.PhysicsConstants.Count; i++)
                RequireNonZeroRecordValue(dataSet.PhysicsConstants[i].HashId, "PhysicsConstants", i, "HashId");

            for (int i = 0; i < dataSet.AppliedLorePackets.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.AppliedLorePackets[i].PacketHash, "AppliedLorePackets", i, "PacketHash");
                RequireNonZeroRecordValue(dataSet.AppliedLorePackets[i].LocaleHash, "AppliedLorePackets", i, "LocaleHash");
                RequireNonZeroRecordValue(dataSet.AppliedLorePackets[i].TitleUtf8ByteLength, "AppliedLorePackets", i, "TitleUtf8ByteLength");
                RequireNonZeroRecordValue(dataSet.AppliedLorePackets[i].WikiUtf8ByteLength, "AppliedLorePackets", i, "WikiUtf8ByteLength");
            }

            for (int i = 0; i < dataSet.AppliedLoreRoutes.Count; i++)
            {
                RequireNonZeroRecordValue(dataSet.AppliedLoreRoutes[i].RouteCardHash, "AppliedLoreRoutes", i, "RouteCardHash");
                RequireNonZeroRecordValue(dataSet.AppliedLoreRoutes[i].PhaseHash, "AppliedLoreRoutes", i, "PhaseHash");
                RequireNonZeroRecordValue(dataSet.AppliedLoreRoutes[i].PrimarySurfaceMask, "AppliedLoreRoutes", i, "PrimarySurfaceMask");
                RequireNonZeroRecordValue(dataSet.AppliedLoreRoutes[i].EndingPressureHash, "AppliedLoreRoutes", i, "EndingPressureHash");
                RequireNonZeroRecordValue(dataSet.AppliedLoreRoutes[i].PacketCount, "AppliedLoreRoutes", i, "PacketCount");
            }
        }

        private static void ValidateUniqueProductionHashes(DataSet dataSet)
        {
            int uniqueHashCapacity = Math.Max(
                Math.Max(Math.Max(dataSet.VoxelMaterials.Count, dataSet.AudioClips.Count), Math.Max(dataSet.VfxScalars.Count, dataSet.ToolHeat.Count)),
                Math.Max(Math.Max(dataSet.HullConstants.Count, dataSet.PhysicsMaterials.Count), Math.Max(dataSet.GhostModules.Count, dataSet.AppliedLoreRoutes.Count)));
            uniqueHashCapacity = Math.Max(
                uniqueHashCapacity,
                Math.Max(Math.Max(dataSet.SpawnCredits.Count, dataSet.SopErrors.Count), Math.Max(dataSet.HudLayouts.Count, dataSet.SectorPages.Count)));
            HashSet<uint> seen = new HashSet<uint>(uniqueHashCapacity); // COLD ALLOC: HashSet<uint>[section max row count] - editor-only duplicate validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.VoxelMaterials.Count; i++)
                RequireUniqueHash(seen, dataSet.VoxelMaterials[i].VoxelHash, "VoxelMaterials", i, "VoxelHash");

            seen.Clear();
            for (int i = 0; i < dataSet.AudioClips.Count; i++)
                RequireUniqueHash(seen, dataSet.AudioClips[i].EventHash, "AudioClipRegistry", i, "EventHash");

            seen.Clear();
            for (int i = 0; i < dataSet.VfxScalars.Count; i++)
                RequireUniqueHash(seen, dataSet.VfxScalars[i].EffectHash, "VfxScalars", i, "EffectHash");

            seen.Clear();
            for (int i = 0; i < dataSet.ToolHeat.Count; i++)
                RequireUniqueHash(seen, dataSet.ToolHeat[i].ToolHash, "ToolHeatCapacity", i, "ToolHash");

            seen.Clear();
            for (int i = 0; i < dataSet.HullConstants.Count; i++)
                RequireUniqueHash(seen, dataSet.HullConstants[i].PartHash, "SubmarineHullConstants", i, "PartHash");

            seen.Clear();
            for (int i = 0; i < dataSet.PhysicsMaterials.Count; i++)
                RequireUniqueHash(seen, dataSet.PhysicsMaterials[i].SurfaceHash, "PhysicsMaterials", i, "SurfaceHash");

            seen.Clear();
            for (int i = 0; i < dataSet.GhostModules.Count; i++)
                RequireUniqueHash(seen, dataSet.GhostModules[i].ModuleHash, "GhostModules", i, "ModuleHash");

            seen.Clear();
            for (int i = 0; i < dataSet.SpawnCredits.Count; i++)
                RequireUniqueHash(seen, dataSet.SpawnCredits[i].EntityHash, "SpawnCreditCosts", i, "EntityHash");

            seen.Clear();
            for (int i = 0; i < dataSet.SopErrors.Count; i++)
                RequireUniqueHash(seen, dataSet.SopErrors[i].ErrorHash, "SopErrors", i, "ErrorHash");

            seen.Clear();
            for (int i = 0; i < dataSet.HudLayouts.Count; i++)
                RequireUniqueHash(seen, dataSet.HudLayouts[i].ElementHash, "HudLayouts", i, "ElementHash");

            seen.Clear();
            for (int i = 0; i < dataSet.SectorPages.Count; i++)
                RequireUniqueHash(seen, dataSet.SectorPages[i].SectorHash, "SectorPageDirectory", i, "SectorHash");

            seen.Clear();
            for (int i = 0; i < dataSet.AppliedLoreRoutes.Count; i++)
                RequireUniqueHash(seen, dataSet.AppliedLoreRoutes[i].RouteCardHash, "AppliedLoreRoutes", i, "RouteCardHash");
        }

        private static void ValidateProductionNumericRanges(DataSet dataSet)
        {
            for (int i = 0; i < dataSet.Items.Count; i++)
            {
                H8ItemRecord item = dataSet.Items[i];
                RequireNonZeroRecordValue(item.MaxStack, "Items", i, "MaxStack");
                RequirePositiveRecordValue(item.MassKg, "Items", i, "MassKg");
                RequirePositiveRecordValue(item.VolumeM3, "Items", i, "VolumeM3");
                RequirePositiveRecordValue(item.BaseQuality, "Items", i, "BaseQuality");
                RequireNonNegativeRecordValue(item.HeatCapacity, "Items", i, "HeatCapacity");
                RequireNonNegativeRecordValue(item.AccessFrequency, "Items", i, "AccessFrequency");
            }

            for (int i = 0; i < dataSet.Creatures.Count; i++)
            {
                H8CreatureGenomeTraitBlock genome = dataSet.Creatures[i].Genome;
                RequireNonNegativeRecordValue(genome.Aggression, "Creatures", i, "Genome.Aggression");
                RequirePositiveRecordValue(genome.Metabolism, "Creatures", i, "Genome.Metabolism");
                RequirePositiveRecordValue(genome.MaxHealth, "Creatures", i, "Genome.MaxHealth");
                RequirePositiveRecordValue(genome.CruiseSpeed, "Creatures", i, "Genome.CruiseSpeed");
                RequirePositiveRecordValue(genome.BurstSpeed, "Creatures", i, "Genome.BurstSpeed");
                RequirePositiveRecordValue(genome.SpawnCreditCost, "Creatures", i, "Genome.SpawnCreditCost");
                RequireDepthRange(genome.PressureMinMeters, genome.PressureMaxMeters, "Creatures", i, "Genome.PressureRange");
            }

            for (int i = 0; i < dataSet.Biomes.Count; i++)
            {
                H8BiomeRecord biome = dataSet.Biomes[i];
                RequireDepthRange(biome.MinDepthMeters, biome.MaxDepthMeters, "Biomes", i, "DepthRange");
                RequireFiniteRecordValue(biome.TemperatureCelsius, "Biomes", i, "TemperatureCelsius");
                RequirePositiveRecordValue(biome.PressureScalar, "Biomes", i, "PressureScalar");
                RequireNonNegativeRecordValue(biome.FogDensity, "Biomes", i, "FogDensity");
                RequireNonNegativeRecordValue(biome.LightScatterR, "Biomes", i, "LightScatterR");
                RequireNonNegativeRecordValue(biome.LightScatterG, "Biomes", i, "LightScatterG");
                RequireNonNegativeRecordValue(biome.LightScatterB, "Biomes", i, "LightScatterB");
            }

            for (int i = 0; i < dataSet.Recipes.Count; i++)
            {
                RequirePositiveRecordValue(dataSet.Recipes[i].CraftSeconds, "Recipes", i, "CraftSeconds");
                RequireNonZeroRecordValue(dataSet.Recipes[i].OutputCount, "Recipes", i, "OutputCount");
            }

            for (int i = 0; i < dataSet.VoxelMaterials.Count; i++)
            {
                RequirePositiveRecordValue(dataSet.VoxelMaterials[i].Hardness, "VoxelMaterials", i, "Hardness");
                RequirePositiveRecordValue(dataSet.VoxelMaterials[i].MeltingPointCelsius, "VoxelMaterials", i, "MeltingPointCelsius");
                RequirePositiveRecordValue(dataSet.VoxelMaterials[i].Density, "VoxelMaterials", i, "Density");
            }

            for (int i = 0; i < dataSet.AudioClips.Count; i++)
                RequireNonZeroRecordValue(dataSet.AudioClips[i].BankHash, "AudioClipRegistry", i, "BankHash");

            for (int i = 0; i < dataSet.VfxScalars.Count; i++)
            {
                H8VfxScalarRecord vfx = dataSet.VfxScalars[i];
                RequireNonNegativeRecordValue(vfx.EmissionRate, "VfxScalars", i, "EmissionRate");
                RequireNonNegativeRecordValue(vfx.ColorR, "VfxScalars", i, "ColorR");
                RequireNonNegativeRecordValue(vfx.ColorG, "VfxScalars", i, "ColorG");
                RequireNonNegativeRecordValue(vfx.ColorB, "VfxScalars", i, "ColorB");
                RequireNonNegativeRecordValue(vfx.ColorA, "VfxScalars", i, "ColorA");
                RequireNonNegativeRecordValue(vfx.Intensity, "VfxScalars", i, "Intensity");
            }

            for (int i = 0; i < dataSet.ToolHeat.Count; i++)
            {
                RequirePositiveRecordValue(dataSet.ToolHeat[i].HeatCapacity, "ToolHeatCapacity", i, "HeatCapacity");
                RequirePositiveRecordValue(dataSet.ToolHeat[i].MaxSafeTemperature, "ToolHeatCapacity", i, "MaxSafeTemperature");
            }

            for (int i = 0; i < dataSet.HullConstants.Count; i++)
            {
                H8SubmarineHullConstantRecord hull = dataSet.HullConstants[i];
                RequirePositiveRecordValue(hull.MassKg, "SubmarineHullConstants", i, "MassKg");
                RequireNonNegativeRecordValue(hull.DragScalar, "SubmarineHullConstants", i, "DragScalar");
                RequireFiniteRecordValue(hull.BuoyancyScalar, "SubmarineHullConstants", i, "BuoyancyScalar");
                RequirePositiveRecordValue(hull.CrushDepthMeters, "SubmarineHullConstants", i, "CrushDepthMeters");
                RequirePositiveRecordValue(hull.IntegrityCap, "SubmarineHullConstants", i, "IntegrityCap");
            }

            for (int i = 0; i < dataSet.PhysicsMaterials.Count; i++)
            {
                RequireNonNegativeRecordValue(dataSet.PhysicsMaterials[i].Friction, "PhysicsMaterials", i, "Friction");
                RequireNonNegativeRecordValue(dataSet.PhysicsMaterials[i].Restitution, "PhysicsMaterials", i, "Restitution");
            }

            for (int i = 0; i < dataSet.GhostModules.Count; i++)
            {
                H8GhostModuleRecord module = dataSet.GhostModules[i];
                RequireFiniteRecordValue(module.SnapOffsetX, "GhostModules", i, "SnapOffsetX");
                RequireFiniteRecordValue(module.SnapOffsetY, "GhostModules", i, "SnapOffsetY");
                RequireFiniteRecordValue(module.SnapOffsetZ, "GhostModules", i, "SnapOffsetZ");
                RequireNonNegativeRecordValue(module.PowerRequirement, "GhostModules", i, "PowerRequirement");
                RequirePositiveRecordValue(module.BuildCostScalar, "GhostModules", i, "BuildCostScalar");
            }

            for (int i = 0; i < dataSet.SpawnCredits.Count; i++)
                RequirePositiveRecordValue(dataSet.SpawnCredits[i].CreditCost, "SpawnCreditCosts", i, "CreditCost");

            for (int i = 0; i < dataSet.HudLayouts.Count; i++)
            {
                H8HudLayoutRecord hud = dataSet.HudLayouts[i];
                RequireFiniteRecordValue(hud.M00, "HudLayouts", i, "M00");
                RequireFiniteRecordValue(hud.M01, "HudLayouts", i, "M01");
                RequireFiniteRecordValue(hud.M02, "HudLayouts", i, "M02");
                RequireFiniteRecordValue(hud.M03, "HudLayouts", i, "M03");
                RequireFiniteRecordValue(hud.M10, "HudLayouts", i, "M10");
                RequireFiniteRecordValue(hud.M11, "HudLayouts", i, "M11");
                RequireFiniteRecordValue(hud.M12, "HudLayouts", i, "M12");
                RequireFiniteRecordValue(hud.M13, "HudLayouts", i, "M13");
                RequireFiniteRecordValue(hud.M20, "HudLayouts", i, "M20");
                RequireFiniteRecordValue(hud.M21, "HudLayouts", i, "M21");
                RequireFiniteRecordValue(hud.M22, "HudLayouts", i, "M22");
                RequireFiniteRecordValue(hud.M23, "HudLayouts", i, "M23");
                RequireFiniteRecordValue(hud.M30, "HudLayouts", i, "M30");
                RequireFiniteRecordValue(hud.M31, "HudLayouts", i, "M31");
            }

            for (int i = 0; i < dataSet.SectorPages.Count; i++)
            {
                RequireAupRecordValue(dataSet.SectorPages[i].AupX, "SectorPageDirectory", i, "AupX");
                RequireAupRecordValue(dataSet.SectorPages[i].AupZ, "SectorPageDirectory", i, "AupZ");
            }

            for (int i = 0; i < dataSet.Economy.Count; i++)
            {
                H8EconomyRecord economy = dataSet.Economy[i];
                RequireNonNegativeRecordValue(economy.BasePrice, "Economy", i, "BasePrice");
                RequireNonNegativeRecordValue(economy.Scarcity01, "Economy", i, "Scarcity01");
                RequireNonNegativeRecordValue(economy.Demand01, "Economy", i, "Demand01");
                RequireNonNegativeRecordValue(economy.SupplyRefreshSeconds, "Economy", i, "SupplyRefreshSeconds");
                RequireNonNegativeRecordValue(economy.AccessFrequency, "Economy", i, "AccessFrequency");
            }

            for (int i = 0; i < dataSet.PhysicsConstants.Count; i++)
            {
                H8PhysicsConstantsRecord physics = dataSet.PhysicsConstants[i];
                RequirePositiveRecordValue(physics.MassKg, "PhysicsConstants", i, "MassKg");
                RequireNonNegativeRecordValue(physics.AddedMass, "PhysicsConstants", i, "AddedMass");
                RequireNonNegativeRecordValue(physics.LinearDrag, "PhysicsConstants", i, "LinearDrag");
                RequireFiniteRecordValue(physics.Buoyancy, "PhysicsConstants", i, "Buoyancy");
                RequirePositiveRecordValue(physics.CrushDepthM, "PhysicsConstants", i, "CrushDepthM");
                RequirePositiveRecordValue(physics.AupSectorSizeMeters, "PhysicsConstants", i, "AupSectorSizeMeters");
                RequirePositiveRecordValue(physics.MaxWorldBoundsMeters, "PhysicsConstants", i, "MaxWorldBoundsMeters");
                RequireNonNegativeRecordValue(physics.AccessFrequency, "PhysicsConstants", i, "AccessFrequency");
            }

            for (int i = 0; i < dataSet.AppliedLoreRoutes.Count; i++)
            {
                H8AppliedLoreRouteRecord route = dataSet.AppliedLoreRoutes[i];
                RequireDepthRange(route.DepthMinMeters, route.DepthMaxMeters, "AppliedLoreRoutes", i, "DepthRange");
                if (route.PacketCount > H8DataLayoutConstants.AppliedLoreRoutePacketCapacity)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] AppliedLoreRoutes packet count exceeds capacity: record_index=" + i);
                if (route.RequiredPacketCount > H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] AppliedLoreRoutes prerequisite count exceeds capacity: record_index=" + i);
            }
        }

        private static void ValidateSemanticRecordReferences(
            DataSet dataSet,
            HashSet<uint> itemHashes,
            HashSet<uint> creatureHashes,
            HashSet<uint> biomeHashes,
            HashSet<uint> recipeOutputHashes,
            HashSet<uint> voxelMaterialHashes,
            HashSet<uint> physicsSurfaceHashes,
            HashSet<uint> appliedLorePacketHashes)
        {
            for (int i = 0; i < dataSet.Biomes.Count; i++)
                RequireHashReference(dataSet.Biomes[i].SurfaceId, voxelMaterialHashes, "Biomes", i, "SurfaceId", "VoxelMaterials.VoxelHash");

            for (int i = 0; i < dataSet.BiomeHeatmap.Count; i++)
                RequireHashReference(dataSet.BiomeHeatmap[i].BiomeHash, biomeHashes, "BiomeHeatmap", i, "BiomeHash", "Biomes.BiomeHash");

            for (int i = 0; i < dataSet.VoxelMaterials.Count; i++)
            {
                RequireHashReference(dataSet.VoxelMaterials[i].YieldHash, itemHashes, "VoxelMaterials", i, "YieldHash", "Items.HashId");
                RequireHashReference(dataSet.VoxelMaterials[i].SurfaceId, physicsSurfaceHashes, "VoxelMaterials", i, "SurfaceId", "PhysicsMaterials.SurfaceHash");
            }

            for (int i = 0; i < dataSet.GhostModules.Count; i++)
                RequireHashReference(dataSet.GhostModules[i].RecipeHash, recipeOutputHashes, "GhostModules", i, "RecipeHash", "Recipes.OutputHash");

            for (int i = 0; i < dataSet.SpawnCredits.Count; i++)
                RequireHashReference(dataSet.SpawnCredits[i].EntityHash, creatureHashes, "SpawnCreditCosts", i, "EntityHash", "Creatures.SpeciesHash");

            for (int i = 0; i < dataSet.SectorPages.Count; i++)
                RequireHashReference(dataSet.SectorPages[i].BiomeHash, biomeHashes, "SectorPageDirectory", i, "BiomeHash", "Biomes.BiomeHash");

            for (int i = 0; i < dataSet.AppliedLoreRoutes.Count; i++)
                ValidateAppliedLoreRoutePacketReferences(dataSet.AppliedLoreRoutes[i], i, appliedLorePacketHashes);

            ValidateAppliedLoreRoutePacketOwnership(dataSet.AppliedLoreRoutes);
            ValidateAppliedLorePrerequisiteGraph(dataSet.AppliedLoreRoutes);
        }

        private static void ValidateAppliedLoreRoutePacketReferences(H8AppliedLoreRouteRecord route, int recordIndex, HashSet<uint> appliedLorePacketHashes)
        {
            uint packetCount = math.min(route.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            for (uint i = 0u; i < packetCount; i++)
                RequireHashReference(GetAppliedLoreRoutePacketHash(in route, i), appliedLorePacketHashes, "AppliedLoreRoutes", recordIndex, "PacketHash" + i, "AppliedLorePackets.PacketHash");

            uint requiredCount = math.min(route.RequiredPacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
            for (uint i = 0u; i < requiredCount; i++)
                RequireHashReference(GetAppliedLoreRouteRequiredPacketHash(in route, i), appliedLorePacketHashes, "AppliedLoreRoutes", recordIndex, "RequiredPacketHash" + i, "AppliedLorePackets.PacketHash");
        }

        private static void ValidateAppliedLoreRoutePacketOwnership(List<H8AppliedLoreRouteRecord> routes)
        {
            int ownerCapacity = Math.Max(
                32,
                routes.Count * H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            Dictionary<uint, uint> ownerByPacket = new Dictionary<uint, uint>(ownerCapacity);

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                H8AppliedLoreRouteRecord route = routes[routeIndex];
                uint packetCount = math.min(route.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
                for (uint packetIndex = 0u; packetIndex < packetCount; packetIndex++)
                {
                    uint packetHash = GetAppliedLoreRoutePacketHash(in route, packetIndex);
                    if (packetHash == 0u)
                        continue;

                    if (ownerByPacket.TryGetValue(packetHash, out uint ownerRouteHash))
                    {
                        throw new InvalidOperationException(
                            "[H8DataMonolithCompiler] FatalArchitectureException: Applied lore packet has multiple route owners: packet=0x" +
                            packetHash.ToString("X8") +
                            ", owner_route=0x" +
                            ownerRouteHash.ToString("X8") +
                            ", duplicate_route=0x" +
                            route.RouteCardHash.ToString("X8"));
                    }

                    ownerByPacket.Add(packetHash, route.RouteCardHash);
                }
            }
        }

        private static void ValidateAppliedLorePrerequisiteGraph(List<H8AppliedLoreRouteRecord> routes)
        {
            Dictionary<uint, List<uint>> prerequisitesByPacket =
                new Dictionary<uint, List<uint>>(Math.Max(32, routes.Count * 2));

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                H8AppliedLoreRouteRecord route = routes[routeIndex];
                uint packetCount = math.min(route.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
                uint requiredCount = math.min(route.RequiredPacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
                for (uint packetIndex = 0u; packetIndex < packetCount; packetIndex++)
                {
                    uint packetHash = GetAppliedLoreRoutePacketHash(in route, packetIndex);
                    if (packetHash == 0u)
                        continue;

                    for (uint requiredIndex = 0u; requiredIndex < requiredCount; requiredIndex++)
                    {
                        uint requiredHash = GetAppliedLoreRouteRequiredPacketHash(in route, requiredIndex);
                        if (requiredHash == 0u)
                            continue;

                        AddAppliedLorePrerequisiteEdge(prerequisitesByPacket, packetHash, requiredHash);
                    }
                }
            }

            int prerequisiteNodeCapacity = Math.Max(32, prerequisitesByPacket.Count);
            HashSet<uint> visiting = new HashSet<uint>(prerequisiteNodeCapacity);
            HashSet<uint> visited = new HashSet<uint>(prerequisiteNodeCapacity);
            foreach (uint packetHash in prerequisitesByPacket.Keys)
                ValidateAppliedLorePrerequisiteNode(packetHash, prerequisitesByPacket, visiting, visited);
        }

        private static void AddAppliedLorePrerequisiteEdge(
            Dictionary<uint, List<uint>> prerequisitesByPacket,
            uint packetHash,
            uint requiredHash)
        {
            if (!prerequisitesByPacket.TryGetValue(packetHash, out List<uint> prerequisites))
            {
                prerequisites = new List<uint>(H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
                prerequisitesByPacket.Add(packetHash, prerequisites);
            }

            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (prerequisites[i] == requiredHash)
                    return;
            }

            prerequisites.Add(requiredHash);
        }

        private static void ValidateAppliedLorePrerequisiteNode(
            uint packetHash,
            Dictionary<uint, List<uint>> prerequisitesByPacket,
            HashSet<uint> visiting,
            HashSet<uint> visited)
        {
            if (visited.Contains(packetHash))
                return;

            if (!visiting.Add(packetHash))
            {
                throw new InvalidOperationException(
                    "[H8DataMonolithCompiler] FatalArchitectureException: Applied lore prerequisite cycle detected at packet=0x" +
                    packetHash.ToString("X8"));
            }

            if (prerequisitesByPacket.TryGetValue(packetHash, out List<uint> prerequisites))
            {
                for (int i = 0; i < prerequisites.Count; i++)
                {
                    uint requiredHash = prerequisites[i];
                    if (requiredHash == packetHash)
                    {
                        throw new InvalidOperationException(
                            "[H8DataMonolithCompiler] FatalArchitectureException: Applied lore self-prerequisite detected at packet=0x" +
                            packetHash.ToString("X8"));
                    }

                    ValidateAppliedLorePrerequisiteNode(requiredHash, prerequisitesByPacket, visiting, visited);
                }
            }

            visiting.Remove(packetHash);
            visited.Add(packetHash);
        }

        private static uint GetAppliedLoreRoutePacketHash(in H8AppliedLoreRouteRecord route, uint index)
        {
            switch (index)
            {
                case 0u: return route.PacketHash0;
                case 1u: return route.PacketHash1;
                case 2u: return route.PacketHash2;
                case 3u: return route.PacketHash3;
                case 4u: return route.PacketHash4;
                case 5u: return route.PacketHash5;
                case 6u: return route.PacketHash6;
                case 7u: return route.PacketHash7;
                default: return 0u;
            }
        }

        private static uint GetAppliedLoreRouteRequiredPacketHash(in H8AppliedLoreRouteRecord route, uint index)
        {
            switch (index)
            {
                case 0u: return route.RequiredPacketHash0;
                case 1u: return route.RequiredPacketHash1;
                case 2u: return route.RequiredPacketHash2;
                case 3u: return route.RequiredPacketHash3;
                default: return 0u;
            }
        }

        private static void RequireNonZeroRecordValue(uint value, string sectionName, int recordIndex, string fieldName)
        {
            if (value != 0u)
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Invalid production static-data row: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                " resolved to zero. Authored IDs and required references must be non-empty.");
        }

        private static void RequireHashReference(uint hash, HashSet<uint> allowedHashes, string sectionName, int recordIndex, string fieldName, string targetSection)
        {
            if (allowedHashes.Contains(hash))
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Broken production static-data reference: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", hash=0x" +
                hash.ToString("X8") +
                ", target=" +
                targetSection);
        }

        private static void RequireUniqueHash(HashSet<uint> seen, uint hash, string sectionName, int recordIndex, string fieldName)
        {
            if (seen.Add(hash))
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Duplicate production static-data hash: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", hash=0x" +
                hash.ToString("X8"));
        }

        private static void RequireFiniteRecordValue(float value, string sectionName, int recordIndex, string fieldName)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value))
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Non-finite production static-data value: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", value=" +
                value.ToString(CultureInfo.InvariantCulture));
        }

        private static void RequirePositiveRecordValue(float value, string sectionName, int recordIndex, string fieldName)
        {
            RequireFiniteRecordValue(value, sectionName, recordIndex, fieldName);
            if (value > 0f)
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Non-positive production static-data value: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", value=" +
                value.ToString(CultureInfo.InvariantCulture));
        }

        private static void RequireNonNegativeRecordValue(float value, string sectionName, int recordIndex, string fieldName)
        {
            RequireFiniteRecordValue(value, sectionName, recordIndex, fieldName);
            if (value >= 0f)
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Negative production static-data value: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", value=" +
                value.ToString(CultureInfo.InvariantCulture));
        }

        private static void RequireDepthRange(float minDepth, float maxDepth, string sectionName, int recordIndex, string fieldName)
        {
            RequireFiniteRecordValue(minDepth, sectionName, recordIndex, fieldName + ".Min");
            RequireFiniteRecordValue(maxDepth, sectionName, recordIndex, fieldName + ".Max");
            if (maxDepth >= minDepth)
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Invalid depth range: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", min=" +
                minDepth.ToString(CultureInfo.InvariantCulture) +
                ", max=" +
                maxDepth.ToString(CultureInfo.InvariantCulture));
        }

        private static void RequireAupRecordValue(long value, string sectionName, int recordIndex, string fieldName)
        {
            if (value >= -100000L && value <= 100000L)
                return;

            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] AUP coordinate outside 100km bounds: section=" +
                sectionName +
                ", record_index=" +
                recordIndex +
                ", field=" +
                fieldName +
                ", value=" +
                value.ToString(CultureInfo.InvariantCulture));
        }

        private static void ValidateProductionSectionCoverage(DataSet dataSet)
        {
            string coverageError = BuildProductionCoverageError(dataSet, out int missingCount);
            if (missingCount == 0)
                return;

            throw new InvalidOperationException("[H8DataMonolithCompiler] Production static-data coverage gate failed. " + coverageError);
        }

        private static string BuildProductionCoverageError(DataSet dataSet, out int missingCount)
        {
            StringBuilder missing = new StringBuilder(256); // COLD ALLOC: StringBuilder[coverage failure text] - editor-only production gate - owner: H8DataMonolithCompiler
            missingCount = 0;

            AppendMissingSection(missing, ref missingCount, "Items", dataSet.Items.Count);
            AppendMissingSection(missing, ref missingCount, "Creatures", dataSet.Creatures.Count);
            AppendMissingSection(missing, ref missingCount, "Biomes", dataSet.Biomes.Count);
            AppendMissingSection(missing, ref missingCount, "Recipes", dataSet.Recipes.Count);
            AppendMissingSection(missing, ref missingCount, "LootCdf", dataSet.LootCdf.Count);
            AppendMissingSection(missing, ref missingCount, "VoxelMaterials", dataSet.VoxelMaterials.Count);
            AppendMissingSection(missing, ref missingCount, "AudioClipRegistry", dataSet.AudioClips.Count);
            AppendMissingSection(missing, ref missingCount, "VfxScalars", dataSet.VfxScalars.Count);
            AppendMissingSection(missing, ref missingCount, "DepthPressureCurve", dataSet.DepthPressureCurve.Count);
            AppendMissingSection(missing, ref missingCount, "ToolHeatCapacity", dataSet.ToolHeat.Count);
            AppendMissingSection(missing, ref missingCount, "SubmarineHullConstants", dataSet.HullConstants.Count);
            AppendMissingSection(missing, ref missingCount, "PhysicsMaterials", dataSet.PhysicsMaterials.Count);
            AppendMissingSection(missing, ref missingCount, "GhostModules", dataSet.GhostModules.Count);
            AppendMissingSection(missing, ref missingCount, "SpawnCreditCosts", dataSet.SpawnCredits.Count);
            AppendMissingSection(missing, ref missingCount, "LightAttenuationCurve", dataSet.LightAttenuationCurve.Count);
            AppendMissingSection(missing, ref missingCount, "SopErrors", dataSet.SopErrors.Count);
            AppendMissingSection(missing, ref missingCount, "HudLayouts", dataSet.HudLayouts.Count);
            AppendMissingSection(missing, ref missingCount, "SectorPageDirectory", dataSet.SectorPages.Count);
            AppendMissingSection(missing, ref missingCount, "Economy", dataSet.Economy.Count);
            AppendMissingSection(missing, ref missingCount, "PhysicsConstants", dataSet.PhysicsConstants.Count);
            AppendMissingSection(missing, ref missingCount, "AppliedLorePackets", dataSet.AppliedLorePackets.Count);
            AppendMissingSection(missing, ref missingCount, "AppliedLoreRoutes", dataSet.AppliedLoreRoutes.Count);

            AppendMissingExactCountSection(missing, ref missingCount, "BiomeHeatmap", dataSet.BiomeHeatmap.Count, 256 * 256);

            if (missingCount == 0)
                return string.Empty;

            return
                "Missing required sections: " +
                missing +
                ". A structurally valid sparse static_data.h8bin is not production payload proof.";
        }

        private static string BuildProductionCoverageReport(DataSet dataSet, int csvFileCount, int jsonFileCount, int missingCount, string coverageError)
        {
            StringBuilder builder = new StringBuilder(1024); // COLD ALLOC: StringBuilder[coverage report] - editor-only facade - owner: H8DataMonolithCompiler
            builder.Append("source-csv-files=").Append(csvFileCount).AppendLine();
            builder.Append("source-json-files=").Append(jsonFileCount).AppendLine();
            AppendCoverageCount(builder, "Items", dataSet.Items.Count);
            AppendCoverageCount(builder, "Creatures", dataSet.Creatures.Count);
            AppendCoverageCount(builder, "Biomes", dataSet.Biomes.Count);
            AppendCoverageCount(builder, "Recipes", dataSet.Recipes.Count);
            AppendCoverageCount(builder, "BiomeHeatmap", dataSet.BiomeHeatmap.Count);
            AppendCoverageCount(builder, "LootCdf", dataSet.LootCdf.Count);
            AppendCoverageCount(builder, "VoxelMaterials", dataSet.VoxelMaterials.Count);
            AppendCoverageCount(builder, "AudioClipRegistry", dataSet.AudioClips.Count);
            AppendCoverageCount(builder, "VfxScalars", dataSet.VfxScalars.Count);
            AppendCoverageCount(builder, "DepthPressureCurve", dataSet.DepthPressureCurve.Count);
            AppendCoverageCount(builder, "ToolHeatCapacity", dataSet.ToolHeat.Count);
            AppendCoverageCount(builder, "SubmarineHullConstants", dataSet.HullConstants.Count);
            AppendCoverageCount(builder, "PhysicsMaterials", dataSet.PhysicsMaterials.Count);
            AppendCoverageCount(builder, "GhostModules", dataSet.GhostModules.Count);
            AppendCoverageCount(builder, "SpawnCreditCosts", dataSet.SpawnCredits.Count);
            AppendCoverageCount(builder, "LightAttenuationCurve", dataSet.LightAttenuationCurve.Count);
            AppendCoverageCount(builder, "SopErrors", dataSet.SopErrors.Count);
            AppendCoverageCount(builder, "HudLayouts", dataSet.HudLayouts.Count);
            AppendCoverageCount(builder, "SectorPageDirectory", dataSet.SectorPages.Count);
            AppendCoverageCount(builder, "Economy", dataSet.Economy.Count);
            AppendCoverageCount(builder, "PhysicsConstants", dataSet.PhysicsConstants.Count);
            AppendCoverageCount(builder, "AppliedLorePackets", dataSet.AppliedLorePackets.Count);
            AppendCoverageCount(builder, "AppliedLoreRoutes", dataSet.AppliedLoreRoutes.Count);
            builder.Append("production-coverage=").Append(missingCount == 0 ? "PASS" : "FAIL").Append(" missing=").Append(missingCount).AppendLine();
            if (!string.IsNullOrEmpty(coverageError))
                builder.Append("coverage-error=").Append(coverageError).AppendLine();

            return builder.ToString();
        }

        private static void AppendCoverageCount(StringBuilder builder, string sectionName, int rowCount)
        {
            builder.Append(sectionName).Append('=').Append(rowCount).AppendLine();
        }

        private static void AppendMissingSection(StringBuilder builder, ref int missingCount, string sectionName, int rowCount)
        {
            if (rowCount > 0)
                return;

            if (missingCount > 0)
                builder.Append(", ");

            builder.Append(sectionName);
            missingCount++;
        }

        private static void AppendMissingExactCountSection(StringBuilder builder, ref int missingCount, string sectionName, int rowCount, int expectedCount)
        {
            if (rowCount == expectedCount)
                return;

            if (missingCount > 0)
                builder.Append(", ");

            builder.Append(sectionName);
            builder.Append("[expected=");
            builder.Append(expectedCount);
            builder.Append(", actual=");
            builder.Append(rowCount);
            builder.Append(']');
            missingCount++;
        }

        private static void ValidateRecipeItemReferences(CsvRow row, HashSet<uint> itemHashes)
        {
            ValidateOptionalItemReference(row, "recipe.output", "output", itemHashes);
            ValidateOptionalItemReference(row, "recipe.output_id", "output_id", itemHashes);
            ValidatePackedItemReferences(row, "recipe.ingredients", "ingredients", Get(row, "ingredients", string.Empty), itemHashes);
            ValidatePackedItemReferences(row, "recipe.recipe", "recipe", Get(row, "recipe", string.Empty), itemHashes);
        }

        private static void ValidateEconomyItemReferences(CsvRow row, HashSet<uint> itemHashes)
        {
            ValidateOptionalItemReference(row, "economy.item_id", "item_id", itemHashes);
            ValidateOptionalItemReference(row, "economy.item", "item", itemHashes);
            ValidateOptionalItemReference(row, "economy.output_id", "output_id", itemHashes);
            ValidateOptionalItemReference(row, "economy.output", "output", itemHashes);
            ValidateOptionalItemReference(row, "economy.recipe_output_id", "recipe_output_id", itemHashes);
            ValidateOptionalItemReference(row, "economy.recipe_output", "recipe_output", itemHashes);
            ValidatePackedItemReferences(row, "economy.ingredients", "ingredients", Get(row, "ingredients", string.Empty), itemHashes);
            ValidatePackedItemReferences(row, "economy.ingredient_ids", "ingredient_ids", Get(row, "ingredient_ids", string.Empty), itemHashes);
            ValidatePackedItemReferences(row, "economy.recipe", "recipe", Get(row, "recipe", string.Empty), itemHashes);
            ValidatePackedItemReferences(row, "economy.recipe_items", "recipe_items", Get(row, "recipe_items", string.Empty), itemHashes);
        }

        private static void ValidateOptionalItemReference(CsvRow row, string owner, string fieldName, HashSet<uint> itemHashes)
        {
            string itemId = Get(row, fieldName, string.Empty);
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            uint hash = Hash(itemId);
            if (hash != 0u && !itemHashes.Contains(hash))
                ThrowBrokenReference(row, owner, fieldName, itemId, hash, -1);
        }

        private static void ValidatePackedItemReferences(CsvRow row, string owner, string fieldName, string packedIds, HashSet<uint> itemHashes)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return;

            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            int tokenIndex = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                uint hash = Hash(token);
                if (hash != 0u && !itemHashes.Contains(hash))
                    ThrowBrokenReference(row, owner, fieldName, token.ToString(), hash, tokenIndex);

                tokenIndex++;
            }
        }

        private static void ThrowBrokenReference(CsvRow row, string owner, string fieldName, string authoredValue, uint hash, int tokenIndex)
        {
            string tokenText = tokenIndex >= 0 ? ", token_index=" + tokenIndex : string.Empty;
            throw new InvalidOperationException(
                "[H8DataMonolithCompiler] Broken static-data cross-reference: owner=" +
                owner +
                ", " +
                FormatRowLocation(row) +
                ", field=" +
                fieldName +
                tokenText +
                ", value=" +
                authoredValue +
                ", hash=0x" +
                hash.ToString("X8"));
        }

        private static string FormatRowLocation(CsvRow row)
        {
            if (row == null)
                return "file=<unknown>";

            string location = "file=" + (string.IsNullOrEmpty(row.AbsolutePath) ? "<unknown>" : row.AbsolutePath);
            if (row.LineNumber > 0)
                location += ", line=" + row.LineNumber;
            if (row.SourceIndex >= 0)
                location += ", source_index=" + row.SourceIndex;

            return location;
        }

        private static void ParseCsv(CsvFileRows source, DataSet dataSet, LocalizationPool localizationPool)
        {
            string tableName = Path.GetFileNameWithoutExtension(source.AbsolutePath).ToLowerInvariant();
            if (!IsRecognizedCsvTable(tableName))
            {
                if (IsAllowedExternalBalanceCsv(source.AbsolutePath, tableName))
                    return;

                throw new InvalidOperationException(
                    "[H8DataMonolithCompiler] Unknown static-data CSV table '" +
                    tableName +
                    "' at " +
                    source.AbsolutePath +
                    ". Move non-monolith source data outside the Data Monolith source roots or add an explicit parser route.");
            }

            for (int i = 0; i < source.Rows.Count; i++)
            {
                ValidateCsvRowHashes(source.AbsolutePath, source.Rows[i].LineNumber, source.Rows[i], requireHashPairs: false);
                ParseRow(tableName, source.Rows[i], dataSet, localizationPool);
            }
        }

        private static bool IsRecognizedCsvTable(string tableName)
        {
            switch (tableName)
            {
                case "items":
                case "item":
                case "fauna":
                case "creatures":
                case "creature_traits":
                case "genome":
                case "economy":
                case "physics":
                case "physics_constants":
                case "biomes":
                case "recipes":
                case "biome_heatmap":
                case "biomeheatmap":
                case "quest_nodes":
                case "questnodes":
                case "quest_edges":
                case "questedges":
                case "loot":
                case "loot_cdf":
                case "lootcdf":
                case "voxel_materials":
                case "voxelmaterials":
                case "audio":
                case "audio_registry":
                case "audioregistry":
                case "vfx":
                case "vfx_scalars":
                case "vfxscalars":
                case "tool_heat":
                case "toolheat":
                case "hull":
                case "submarine_hull":
                case "submarinehull":
                case "narrative_triggers":
                case "narrativetriggers":
                case "physics_materials":
                case "physicsmaterials":
                case "ghost_modules":
                case "ghostmodules":
                case "radiation":
                case "radiation_map":
                case "radiationmap":
                case "spawn_credits":
                case "spawncredits":
                case "spawn_credit_costs":
                case "spawncreditcosts":
                case "sop_errors":
                case "soperrors":
                case "hud_layout":
                case "hudlayout":
                case "sector_pages":
                case "sectorpages":
                case "applied_lore_packets":
                case "appliedlorepackets":
                case "applied_lore_route_cards":
                case "appliedloreroutecards":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAllowedExternalBalanceCsv(string absolutePath, string tableName)
        {
            if (!IsUnderAbsoluteRoot(absolutePath, BalanceSourceFolder))
                return false;

            switch (tableName)
            {
                case "armor_penetration_matrix":
                case "btree_tuning_profiles":
                    return true;
                default:
                    return false;
            }
        }

        private static void ParseJson(string absolutePath, DataSet dataSet, LocalizationPool localizationPool)
        {
            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            JsonRoot root = JsonUtility.FromJson<JsonRoot>(json);
            if (root == null)
                return;

            if (root.items != null)
            {
                for (int i = 0; i < root.items.Length; i++)
                {
                    JsonItem item = root.items[i];
                    dataSet.RawItemRows.Add(ToJsonItemReferenceRow(absolutePath, i, item));
                    dataSet.Items.Add(ToItemRecord(item, localizationPool));
                }
            }

            if (root.creatures != null)
                for (int i = 0; i < root.creatures.Length; i++)
                    dataSet.Creatures.Add(ToCreatureRecord(root.creatures[i], localizationPool));

            if (root.biomes != null)
                for (int i = 0; i < root.biomes.Length; i++)
                    dataSet.Biomes.Add(ToBiomeRecord(root.biomes[i], localizationPool));

            if (root.recipes != null)
            {
                for (int i = 0; i < root.recipes.Length; i++)
                {
                    JsonRecipe recipe = root.recipes[i];
                    dataSet.RawRecipeRows.Add(ToJsonRecipeReferenceRow(absolutePath, i, recipe));
                    dataSet.Recipes.Add(ToRecipeRecord(recipe));
                }
            }
        }

        private static void ParseRow(string tableName, CsvRow row, DataSet dataSet, LocalizationPool localizationPool)
        {
            switch (tableName)
            {
                case "items":
                case "item":
                    dataSet.RawItemRows.Add(row);
                    dataSet.Items.Add(ParseItem(row, localizationPool));
                    break;
                case "fauna":
                case "creatures":
                case "creature_traits":
                case "genome":
                    dataSet.Creatures.Add(ParseCreature(row, localizationPool));
                    break;
                case "economy":
                    dataSet.RawEconomyRows.Add(row);
                    dataSet.Economy.Add(ParseEconomy(row, localizationPool));
                    break;
                case "physics":
                case "physics_constants":
                    dataSet.PhysicsConstants.Add(ParsePhysicsConstants(row, localizationPool));
                    break;
                case "biomes":
                    dataSet.Biomes.Add(ParseBiome(row, localizationPool));
                    break;
                case "recipes":
                    dataSet.RawRecipeRows.Add(row);
                    dataSet.Recipes.Add(ParseRecipe(row));
                    break;
                case "biome_heatmap":
                case "biomeheatmap":
                    dataSet.BiomeHeatmap.Add(ParseBiomeHeatmapCell(row));
                    break;
                case "quest_nodes":
                case "questnodes":
                    dataSet.QuestNodes.Add(ParseQuestNode(row));
                    break;
                case "quest_edges":
                case "questedges":
                    dataSet.QuestEdges.Add(ParseQuestEdge(row));
                    break;
                case "loot":
                case "loot_cdf":
                case "lootcdf":
                    dataSet.RawLootRows.Add(row);
                    break;
                case "voxel_materials":
                case "voxelmaterials":
                    dataSet.VoxelMaterials.Add(ParseVoxelMaterial(row));
                    break;
                case "audio":
                case "audio_registry":
                case "audioregistry":
                    dataSet.AudioClips.Add(ParseAudio(row, localizationPool));
                    break;
                case "vfx":
                case "vfx_scalars":
                case "vfxscalars":
                    dataSet.VfxScalars.Add(ParseVfx(row));
                    break;
                case "tool_heat":
                case "toolheat":
                    dataSet.ToolHeat.Add(ParseToolHeat(row));
                    break;
                case "hull":
                case "submarine_hull":
                case "submarinehull":
                    dataSet.HullConstants.Add(ParseHull(row));
                    break;
                case "narrative_triggers":
                case "narrativetriggers":
                    dataSet.NarrativeTriggers.Add(ParseNarrativeTrigger(row));
                    break;
                case "physics_materials":
                case "physicsmaterials":
                    dataSet.PhysicsMaterials.Add(ParsePhysicsMaterial(row));
                    break;
                case "ghost_modules":
                case "ghostmodules":
                    dataSet.GhostModules.Add(ParseGhostModule(row, localizationPool));
                    break;
                case "radiation":
                case "radiation_map":
                case "radiationmap":
                    dataSet.RadiationCells.Add(ParseRadiation(row));
                    break;
                case "spawn_credits":
                case "spawncredits":
                case "spawn_credit_costs":
                case "spawncreditcosts":
                    dataSet.SpawnCredits.Add(ParseSpawnCredit(row));
                    break;
                case "sop_errors":
                case "soperrors":
                    dataSet.SopErrors.Add(ParseSopError(row, localizationPool));
                    break;
                case "hud_layout":
                case "hudlayout":
                    dataSet.HudLayouts.Add(ParseHudLayout(row));
                    break;
                case "sector_pages":
                case "sectorpages":
                    dataSet.SectorPages.Add(ParseSectorPage(row));
                    break;
                case "applied_lore_packets":
                case "appliedlorepackets":
                    dataSet.AppliedLorePackets.Add(ParseAppliedLorePacket(row, localizationPool));
                    break;
                case "applied_lore_route_cards":
                case "appliedloreroutecards":
                    dataSet.AppliedLoreRoutes.Add(ParseAppliedLoreRoute(row));
                    break;
            }
        }

        private static H8AppliedLoreRouteRecord ParseAppliedLoreRoute(CsvRow row)
        {
            uint packetHash0;
            uint packetHash1;
            uint packetHash2;
            uint packetHash3;
            uint packetHash4;
            uint packetHash5;
            uint packetHash6;
            uint packetHash7;
            uint packetCount = ParseAppliedLoreRoutePacketHashes(
                Get(row, "packet_ids", string.Empty),
                out packetHash0,
                out packetHash1,
                out packetHash2,
                out packetHash3,
                out packetHash4,
                out packetHash5,
                out packetHash6,
                out packetHash7);

            uint requiredHash0;
            uint requiredHash1;
            uint requiredHash2;
            uint requiredHash3;
            uint requiredCount = ParseAppliedLoreRoutePrerequisiteHashes(
                Get(row, "required_packet_ids", string.Empty),
                out requiredHash0,
                out requiredHash1,
                out requiredHash2,
                out requiredHash3);

            return new H8AppliedLoreRouteRecord
            {
                RouteCardHash = Hash(Get(row, "route_card_id", string.Empty)),
                PhaseHash = Hash(Get(row, "phase_id", string.Empty)),
                DepthMinMeters = ParseFloat(row, "depth_min_m", 0f),
                DepthMaxMeters = ParseFloat(row, "depth_max_m", 0f),
                PrimarySurfaceMask = ParseUInt(row, "primary_surface_mask", 0u),
                EndingPressureHash = Hash(Get(row, "ending_pressure", string.Empty)),
                PacketCount = packetCount,
                RequiredPacketCount = requiredCount,
                PacketHash0 = packetHash0,
                PacketHash1 = packetHash1,
                PacketHash2 = packetHash2,
                PacketHash3 = packetHash3,
                PacketHash4 = packetHash4,
                PacketHash5 = packetHash5,
                PacketHash6 = packetHash6,
                PacketHash7 = packetHash7,
                RequiredPacketHash0 = requiredHash0,
                RequiredPacketHash1 = requiredHash1,
                RequiredPacketHash2 = requiredHash2,
                RequiredPacketHash3 = requiredHash3,
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8AppliedLorePacketRecord ParseAppliedLorePacket(CsvRow row, LocalizationPool localizationPool)
        {
            string packetId = Get(row, "packet_id", string.Empty);
            string locale = Get(row, "locale", "en_US");
            string title = Get(row, "title", packetId);
            string scanner = Get(row, "scanner", string.Empty);
            string terminal = Get(row, "terminal", string.Empty);
            string audio = Get(row, "audio", string.Empty);
            string wiki = Get(row, "in_game_wiki", string.Empty);
            string site = Get(row, "external_site", string.Empty);
            string fieldNote = Get(row, "field_note", string.Empty);

            uint titleOffset = localizationPool.Add(title, out int titleBytes);
            uint scannerOffset = localizationPool.Add(scanner, out int scannerBytes);
            uint terminalOffset = localizationPool.Add(terminal, out int terminalBytes);
            uint audioOffset = localizationPool.Add(audio, out int audioBytes);
            uint wikiOffset = localizationPool.Add(wiki, out int wikiBytes);
            uint siteOffset = localizationPool.Add(site, out int siteBytes);
            uint fieldNoteOffset = localizationPool.Add(fieldNote, out int fieldNoteBytes);
            uint poiHash0 = 0u;
            uint poiHash1 = 0u;
            uint biomeHash0 = 0u;
            uint biomeHash1 = 0u;
            ParseFirstTwoHashList(Get(row, "poi_tags", string.Empty), out poiHash0, out poiHash1);
            ParseFirstTwoHashList(Get(row, "biome_tags", string.Empty), out biomeHash0, out biomeHash1);

            return new H8AppliedLorePacketRecord
            {
                PacketHash = Hash(packetId),
                LocaleHash = Hash(locale),
                ArticleHash = Hash(Get(row, "article_id", string.Empty)),
                UnlockHash = Hash(Get(row, "unlock_id", string.Empty)),
                SurfaceMask = ParseUInt(row, "surface_mask", 0x7Fu),
                ReleaseSetHash = Hash(Get(row, "release_set_id", string.Empty)),
                TitleUtf8Offset = titleOffset,
                ScannerUtf8Offset = scannerOffset,
                TerminalUtf8Offset = terminalOffset,
                AudioUtf8Offset = audioOffset,
                WikiUtf8Offset = wikiOffset,
                SiteUtf8Offset = siteOffset,
                FieldNoteUtf8Offset = fieldNoteOffset,
                TitleUtf8ByteLength = (uint)titleBytes,
                ScannerUtf8ByteLength = (uint)scannerBytes,
                TerminalUtf8ByteLength = (uint)terminalBytes,
                AudioUtf8ByteLength = (uint)audioBytes,
                WikiUtf8ByteLength = (uint)wikiBytes,
                SiteUtf8ByteLength = (uint)siteBytes,
                FieldNoteUtf8ByteLength = (uint)fieldNoteBytes,
                PoiTagHash0 = poiHash0,
                PoiTagHash1 = poiHash1,
                BiomeTagHash0 = biomeHash0,
                BiomeTagHash1 = biomeHash1,
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8ItemRecord ParseItem(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "item_id", string.Empty));
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            int ingredientCount = AddRecipeMask(Get(row, "recipe", string.Empty), ref mask0, ref mask1);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);

            return new H8ItemRecord
            {
                HashId = Hash(id),
                CategoryHash = Hash(Get(row, "category", Get(row, "categoryid", string.Empty))),
                Flags = ParseUInt(row, "flags", 0u),
                MaxStack = (ushort)Mathf.Clamp(ParseInt(row, "max_stack", ParseInt(row, "stackmax", 1)), 0, ushort.MaxValue),
                RecipeIngredientCount = (ushort)Mathf.Clamp(ingredientCount, 0, ushort.MaxValue),
                RecipeMask0 = mask0,
                RecipeMask1 = mask1,
                MassKg = ParseFloat(row, "mass_kg", ParseFloat(row, "masskg", 1f)),
                VolumeM3 = ParseFloat(row, "volume_m3", 0.001f),
                BaseQuality = ParseFloat(row, "quality", 1f),
                HeatCapacity = ParseFloat(row, "heat_capacity", 0f),
                YieldHash = Hash(Get(row, "yield_id", string.Empty)),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                Cost = ParseUInt(row, "cost", 0u),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f)
            };
        }

        private static H8CreatureTraitRecord ParseCreature(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "species_id", string.Empty));
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            float swimSpeed = ParseFloat(row, "swimspeed", ParseFloat(row, "cruise_speed", 1f));
            float turnRate = ParseFloat(row, "turnrate", ParseFloat(row, "metabolism", 1f));
            float aggression = ParseFloat(row, "aggression01", ParseFloat(row, "aggression", 0f));
            float fleeDistance = ParseFloat(row, "fleedistancem", ParseFloat(row, "max_depth", 0f));
            float biolumIntensity = ParseFloat(row, "biolumintensity", 0f);
            return new H8CreatureTraitRecord
            {
                SpeciesHash = Hash(id),
                MateMask = ParseUInt(row, "mate_mask", 0u),
                BiomeMask = ParseUInt(row, "biome_mask", 0u),
                Flags = ParseUInt(row, "flags", 0u),
                Genome = new H8CreatureGenomeTraitBlock
                {
                    Aggression = aggression,
                    Metabolism = turnRate,
                    MaxHealth = ParseFloat(row, "max_health", 1f),
                    CruiseSpeed = swimSpeed,
                    BurstSpeed = ParseFloat(row, "burst_speed", Mathf.Max(swimSpeed * 1.35f, swimSpeed)),
                    SpawnCreditCost = ParseFloat(row, "spawn_credit", ParseFloat(row, "accessfrequency", Mathf.Max(1f, biolumIntensity))),
                    PressureMinMeters = ParseFloat(row, "min_depth", 0f),
                    PressureMaxMeters = ParseFloat(row, "max_depth", fleeDistance)
                },
                DisplayNameUtf8Offset = displayNameOffset,
                LootTableHash = Hash(Get(row, "loot_table", string.Empty)),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8EconomyRecord ParseEconomy(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);
            return new H8EconomyRecord
            {
                HashId = Hash(id),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                BasePrice = ParseFloat(row, "baseprice", ParseFloat(row, "base_price", 0f)),
                Scarcity01 = Saturate(ParseFloat(row, "scarcity01", ParseFloat(row, "scarcity", 0f))),
                Demand01 = Saturate(ParseFloat(row, "demand01", ParseFloat(row, "demand", 0f))),
                SupplyRefreshSeconds = ParseFloat(row, "supplyrefreshseconds", ParseFloat(row, "supply_refresh_seconds", 0f)),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f),
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8PhysicsConstantsRecord ParsePhysicsConstants(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);
            return new H8PhysicsConstantsRecord
            {
                HashId = Hash(id),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                MassKg = ParseFloat(row, "masskg", ParseFloat(row, "mass_kg", 0f)),
                AddedMass = ParseFloat(row, "addedmass", ParseFloat(row, "added_mass", 0f)),
                LinearDrag = ParseFloat(row, "lineardrag", ParseFloat(row, "linear_drag", 0f)),
                Buoyancy = ParseFloat(row, "buoyancy", 0f),
                CrushDepthM = ParseFloat(row, "crushdepthm", ParseFloat(row, "crush_depth_m", ParseFloat(row, "crush_depth", 0f))),
                AupSectorSizeMeters = ParseFloat(row, "aupsectorsizemeters", ParseFloat(row, "aup_sector_size_meters", 1000f)),
                MaxWorldBoundsMeters = ParseFloat(row, "maxworldboundsmeters", ParseFloat(row, "max_world_bounds_meters", 100000f)),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8BiomeRecord ParseBiome(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "biome_id", string.Empty));
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            return new H8BiomeRecord
            {
                BiomeHash = Hash(id),
                Flags = ParseUInt(row, "flags", 0u),
                SurfaceId = Hash(Get(row, "surface_id", string.Empty)),
                MinDepthMeters = ParseFloat(row, "min_depth", 0f),
                MaxDepthMeters = ParseFloat(row, "max_depth", 0f),
                TemperatureCelsius = ParseFloat(row, "temperature_c", 2f),
                PressureScalar = ParseFloat(row, "pressure_scalar", 1f),
                FogDensity = ParseFloat(row, "fog_density", 0f),
                LightScatterR = ParseFloat(row, "scatter_r", 0.08f),
                LightScatterG = ParseFloat(row, "scatter_g", 0.18f),
                LightScatterB = ParseFloat(row, "scatter_b", 0.24f),
                DisplayNameUtf8Offset = displayNameOffset,
                HeatmapId = Hash(Get(row, "heatmap_id", string.Empty)),
                RadiationFieldHash = Hash(Get(row, "radiation_id", string.Empty)),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RecipeRecord ParseRecipe(CsvRow row)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            string ingredients = Get(row, "ingredients", Get(row, "recipe", string.Empty));
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            int count = AddRecipeMaskAndHashes(ingredients, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
            return new H8RecipeRecord
            {
                OutputHash = Hash(Get(row, "output", Get(row, "output_id", string.Empty))),
                StationHash = Hash(Get(row, "station", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u),
                IngredientCount = (uint)count,
                IngredientMask0 = mask0,
                IngredientMask1 = mask1,
                IngredientHash0 = h0,
                IngredientHash1 = h1,
                IngredientHash2 = h2,
                IngredientHash3 = h3,
                CraftSeconds = ParseFloat(row, "craft_seconds", 1f),
                OutputCount = ParseUInt(row, "output_count", 1u)
            };
        }

        private static H8BiomeHeatmapCellRecord ParseBiomeHeatmapCell(CsvRow row)
        {
            return new H8BiomeHeatmapCellRecord
            {
                BiomeHash = Hash(Get(row, "biome_id", string.Empty)),
                X = (ushort)Mathf.Clamp(ParseInt(row, "x", 0), 0, 255),
                Y = (ushort)Mathf.Clamp(ParseInt(row, "y", 0), 0, 255)
            };
        }

        private static H8QuestNodeRecord ParseQuestNode(CsvRow row)
        {
            uint mask0 = 0u;
            uint mask1 = 0u;
            uint mask2 = 0u;
            uint mask3 = 0u;
            AddRecipeMask(Get(row, "required_flags", string.Empty), ref mask0, ref mask1, ref mask2, ref mask3);
            return new H8QuestNodeRecord
            {
                NodeHash = Hash(Get(row, "id", string.Empty)),
                CompletionFlagId = ParseUInt(row, "completion_flag", 0u),
                FirstEdgeIndex = ParseUInt(row, "first_edge", 0u),
                EdgeCount = (ushort)Mathf.Clamp(ParseInt(row, "edge_count", 0), 0, ushort.MaxValue),
                NodeType = (ushort)Mathf.Clamp(ParseInt(row, "node_type", 0), 0, ushort.MaxValue),
                RequiredMask0 = mask0,
                RequiredMask1 = mask1,
                RequiredMask2 = mask2,
                RequiredMask3 = mask3
            };
        }

        private static H8QuestEdgeRecord ParseQuestEdge(CsvRow row)
        {
            return new H8QuestEdgeRecord
            {
                FromNodeHash = Hash(Get(row, "from", string.Empty)),
                ToNodeHash = Hash(Get(row, "to", string.Empty)),
                GateFlagId = ParseUInt(row, "gate_flag", 0u)
            };
        }

        private static H8VoxelMaterialRecord ParseVoxelMaterial(CsvRow row)
        {
            return new H8VoxelMaterialRecord
            {
                VoxelHash = Hash(Get(row, "id", string.Empty)),
                YieldHash = Hash(Get(row, "yield_id", string.Empty)),
                Hardness = ParseFloat(row, "hardness", 1f),
                MeltingPointCelsius = ParseFloat(row, "melting_point_c", 1000f),
                Density = ParseFloat(row, "density", 1f),
                SurfaceId = Hash(Get(row, "surface_id", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8AudioClipRegistryRecord ParseAudio(CsvRow row, LocalizationPool localizationPool)
        {
            string addressableKey = Get(row, "addressable_key", string.Empty);
            uint addressableOffset = localizationPool.Add(addressableKey, out int addressableBytes);
            return new H8AudioClipRegistryRecord
            {
                EventHash = Hash(Get(row, "event_id", Get(row, "id", string.Empty))),
                AddressableKeyUtf8Offset = addressableOffset,
                BankHash = Hash(Get(row, "bank", string.Empty)),
                AddressableKeyUtf8ByteLength = (uint)addressableBytes
            };
        }

        private static H8VfxScalarRecord ParseVfx(CsvRow row)
        {
            return new H8VfxScalarRecord
            {
                EffectHash = Hash(Get(row, "id", string.Empty)),
                EmissionRate = ParseFloat(row, "emission_rate", 0f),
                ColorR = ParseFloat(row, "r", 1f),
                ColorG = ParseFloat(row, "g", 1f),
                ColorB = ParseFloat(row, "b", 1f),
                ColorA = ParseFloat(row, "a", 1f),
                Intensity = ParseFloat(row, "intensity", 1f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8ToolHeatCapacityRecord ParseToolHeat(CsvRow row)
        {
            return new H8ToolHeatCapacityRecord
            {
                ToolHash = Hash(Get(row, "id", string.Empty)),
                HeatCapacity = ParseFloat(row, "heat_capacity", 0f),
                MaxSafeTemperature = ParseFloat(row, "max_safe_temperature", 100f)
            };
        }

        private static H8SubmarineHullConstantRecord ParseHull(CsvRow row)
        {
            return new H8SubmarineHullConstantRecord
            {
                PartHash = Hash(Get(row, "id", string.Empty)),
                MassKg = ParseFloat(row, "mass_kg", 1f),
                DragScalar = ParseFloat(row, "drag", 1f),
                BuoyancyScalar = ParseFloat(row, "buoyancy", 1f),
                CrushDepthMeters = ParseFloat(row, "crush_depth", 0f),
                IntegrityCap = ParseFloat(row, "integrity_cap", 1f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8NarrativeTriggerRecord ParseNarrativeTrigger(CsvRow row)
        {
            return new H8NarrativeTriggerRecord
            {
                TriggerHash = Hash(Get(row, "id", string.Empty)),
                AupX = ParseDouble(row, "aup_x", 0d),
                AupY = ParseDouble(row, "aup_y", 0d),
                AupZ = ParseDouble(row, "aup_z", 0d),
                RadiusMeters = ParseFloat(row, "radius", 1f)
            };
        }

        private static H8PhysicsMaterialRecord ParsePhysicsMaterial(CsvRow row)
        {
            return new H8PhysicsMaterialRecord
            {
                SurfaceHash = Hash(Get(row, "id", string.Empty)),
                Friction = ParseFloat(row, "friction", 0.5f),
                Restitution = ParseFloat(row, "restitution", 0f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8GhostModuleRecord ParseGhostModule(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            return new H8GhostModuleRecord
            {
                ModuleHash = Hash(id),
                Flags = ParseUInt(row, "flags", 0u),
                SnapOffsetX = ParseFloat(row, "snap_x", 0f),
                SnapOffsetY = ParseFloat(row, "snap_y", 0f),
                SnapOffsetZ = ParseFloat(row, "snap_z", 0f),
                PowerRequirement = ParseFloat(row, "power", 0f),
                BuildCostScalar = ParseFloat(row, "build_cost", 1f),
                RecipeHash = Hash(Get(row, "recipe_id", string.Empty)),
                DisplayNameUtf8Offset = displayNameOffset,
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RadiationIntensityCellRecord ParseRadiation(CsvRow row)
        {
            return new H8RadiationIntensityCellRecord
            {
                CellHash = Hash(Get(row, "id", string.Empty)),
                IntensitySv = ParseFloat(row, "intensity_sv", 0f),
                FalloffMeters = ParseFloat(row, "falloff", 1f)
            };
        }

        private static H8SpawnCreditCostRecord ParseSpawnCredit(CsvRow row)
        {
            return new H8SpawnCreditCostRecord
            {
                EntityHash = Hash(Get(row, "id", string.Empty)),
                CreditCost = ParseFloat(row, "credit_cost", 1f),
                DirectorMask = ParseUInt(row, "director_mask", 0u)
            };
        }

        private static H8SopErrorRecord ParseSopError(CsvRow row, LocalizationPool localizationPool)
        {
            string message = Get(row, "message", string.Empty);
            uint messageOffset = localizationPool.Add(message, out int messageBytes);
            return new H8SopErrorRecord
            {
                ErrorHash = Hash(Get(row, "id", string.Empty)),
                MessageUtf8Offset = messageOffset,
                Severity = ParseUInt(row, "severity", 0u),
                MessageUtf8ByteLength = (uint)messageBytes
            };
        }

        private static H8HudLayoutRecord ParseHudLayout(CsvRow row)
        {
            return new H8HudLayoutRecord
            {
                ElementHash = Hash(Get(row, "id", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u),
                M00 = ParseFloat(row, "m00", 1f),
                M01 = ParseFloat(row, "m01", 0f),
                M02 = ParseFloat(row, "m02", 0f),
                M03 = ParseFloat(row, "m03", 0f),
                M10 = ParseFloat(row, "m10", 0f),
                M11 = ParseFloat(row, "m11", 1f),
                M12 = ParseFloat(row, "m12", 0f),
                M13 = ParseFloat(row, "m13", 0f),
                M20 = ParseFloat(row, "m20", 0f),
                M21 = ParseFloat(row, "m21", 0f),
                M22 = ParseFloat(row, "m22", 1f),
                M23 = ParseFloat(row, "m23", 0f),
                M30 = ParseFloat(row, "m30", 0f),
                M31 = ParseFloat(row, "m31", 0f)
            };
        }

        private static H8SectorPageRecord ParseSectorPage(CsvRow row)
        {
            return new H8SectorPageRecord
            {
                SectorHash = Hash(Get(row, "sector_id", Get(row, "id", string.Empty))),
                BiomeHash = Hash(Get(row, "biome_id", string.Empty)),
                FileOffsetBytes = ParseUInt(row, "file_offset", 0u),
                ByteCount = ParseUInt(row, "byte_count", 0u),
                AupX = ParseLong(row, "aup_x", 0L),
                AupZ = ParseLong(row, "aup_z", 0L)
            };
        }

        private static void RebuildLootCdf(DataSet dataSet)
        {
            dataSet.LootCdf.Clear();
            dataSet.RawLootRows.Sort(CompareLootRows);
            uint activeTable = 0u;
            uint cumulative = 0u;
            uint tableTotal = 0u;
            int tableStart = 0;

            for (int i = 0; i < dataSet.RawLootRows.Count; i++)
            {
                CsvRow row = dataSet.RawLootRows[i];
                uint tableHash = Hash(Get(row, "table_id", Get(row, "table", string.Empty)));
                if (i == 0 || tableHash != activeTable)
                {
                    PatchLootTableTotal(dataSet.LootCdf, tableStart, dataSet.LootCdf.Count, tableTotal);
                    activeTable = tableHash;
                    cumulative = 0u;
                    tableTotal = 0u;
                    tableStart = dataSet.LootCdf.Count;
                }

                uint weight = ParseUInt(row, "weight", 0u);
                cumulative += weight;
                tableTotal += weight;
                dataSet.LootCdf.Add(new H8LootCdfRecord
                {
                    TableHash = tableHash,
                    ItemHash = Hash(Get(row, "item_id", Get(row, "item", string.Empty))),
                    CumulativeWeight = cumulative,
                    TotalWeight = tableTotal
                });
            }

            PatchLootTableTotal(dataSet.LootCdf, tableStart, dataSet.LootCdf.Count, tableTotal);
        }

        private static void PatchLootTableTotal(List<H8LootCdfRecord> records, int start, int end, uint total)
        {
            for (int i = start; i < end; i++)
            {
                H8LootCdfRecord record = records[i];
                record.TotalWeight = total;
                records[i] = record;
            }
        }

        private static void NormalizeBiomeHeatmap(DataSet dataSet)
        {
            uint fallbackBiomeHash = dataSet.Biomes.Count > 0 ? dataSet.Biomes[0].BiomeHash : 0u;
            H8BiomeHeatmapCellRecord[] cells = new H8BiomeHeatmapCellRecord[256 * 256]; // COLD ALLOC: H8BiomeHeatmapCellRecord[65536] - editor-only heatmap normalization scratch - owner: H8DataMonolithCompiler
            for (int y = 0; y < 256; y++)
            {
                int rowOffset = y * 256;
                for (int x = 0; x < 256; x++)
                {
                    cells[rowOffset + x] = new H8BiomeHeatmapCellRecord
                    {
                        BiomeHash = fallbackBiomeHash,
                        X = (ushort)x,
                        Y = (ushort)y
                    };
                }
            }

            for (int i = 0; i < dataSet.BiomeHeatmap.Count; i++)
            {
                H8BiomeHeatmapCellRecord source = dataSet.BiomeHeatmap[i];
                int index = (source.Y * 256) + source.X;
                if ((uint)index < (uint)cells.Length)
                    cells[index] = source;
            }

            dataSet.BiomeHeatmap.Clear();
            for (int i = 0; i < cells.Length; i++)
                dataSet.BiomeHeatmap.Add(cells[i]);
        }

        private static void GenerateDepthPressureCurve(List<H8DepthPressureSampleRecord> records)
        {
            for (int i = 0; i < 256; i++)
            {
                float depth = (5000f / 255f) * i;
                records.Add(new H8DepthPressureSampleRecord
                {
                    DepthMeters = depth,
                    PressureAtmospheres = 1f + (depth * 0.1f),
                    Normalized = depth / 5000f
                });
            }
        }

        private static void GenerateLightAttenuationCurve(List<H8LightAttenuationSampleRecord> records)
        {
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                float depth = t * 5000f;
                records.Add(new H8LightAttenuationSampleRecord
                {
                    DepthMeters = depth,
                    FogDensity = 0.008f + (t * 0.06f),
                    ScatterR = Mathf.Lerp(0.08f, 0.01f, t),
                    ScatterG = Mathf.Lerp(0.18f, 0.04f, t),
                    ScatterB = Mathf.Lerp(0.28f, 0.09f, t),
                    Absorption = 0.02f + (t * 0.18f)
                });
            }
        }

        private static H8DataSectionEntry AppendSection<T>(MemoryStream stream, H8DataSectionId sectionId, List<T> records, int recordSize)
            where T : unmanaged
        {
            AlignSection(stream);
            uint offset = records.Count > 0 ? (uint)stream.Position : 0u;
            for (int i = 0; i < records.Count; i++)
                WriteStruct(stream, records[i]);

            return new H8DataSectionEntry
            {
                SectionId = (uint)sectionId,
                RecordSize = (uint)recordSize,
                Count = (uint)records.Count,
                OffsetBytes = offset
            };
        }

        private static H8DataSectionEntry AppendLocalizationSection(MemoryStream stream, LocalizationPool localizationPool)
        {
            AlignSection(stream);
            byte[] bytes = localizationPool.ToArray();
            uint offset = bytes.Length > 0 ? (uint)stream.Position : 0u;
            stream.Write(bytes, 0, bytes.Length);
            return new H8DataSectionEntry
            {
                SectionId = (uint)H8DataSectionId.LocalizationUtf8,
                RecordSize = 1u,
                Count = (uint)bytes.Length,
                OffsetBytes = offset
            };
        }

        private static void AlignSection(MemoryStream stream)
        {
            long alignment = H8DataLayoutConstants.SectionAlignmentBytes;
            long aligned = (stream.Position + (alignment - 1L)) & ~(alignment - 1L);
            while (stream.Position < aligned)
                stream.WriteByte(0);
        }

        private static uint AlignUp(uint value, uint alignment)
        {
            return (value + (alignment - 1u)) & ~(alignment - 1u);
        }

        private static ulong AlignUp(ulong value, uint alignment)
        {
            ulong mask = alignment - 1UL;
            return (value + mask) & ~mask;
        }

        private static void WriteZeros(MemoryStream stream, int count)
        {
            for (int i = 0; i < count; i++)
                stream.WriteByte(0);
        }

        private static void EnsureLittleEndianEditorHost()
        {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("[H8DataMonolithCompiler] Big-endian editor hosts are not allowed to emit static_data.h8bin without explicit per-record byte swapping.");
        }

        private static void WriteHeader(byte[] blob, in H8DataBlobHeader header)
        {
            WriteUInt32(blob, 0, header.Magic);
            WriteUInt16(blob, 4, header.FormatVersion);
            WriteUInt16(blob, 6, header.HeaderBytes);
            WriteUInt64(blob, 8, header.Checksum64);
            WriteUInt32(blob, 16, header.BlobBytes);
            WriteUInt32(blob, 20, header.DirectoryOffset);
            WriteUInt32(blob, 24, header.DirectoryBytes);
            WriteUInt32(blob, 28, header.SectionTableOffset);
            WriteUInt32(blob, 32, header.SectionCount);
            WriteUInt32(blob, 36, header.Flags);
            WriteUInt32(blob, 40, header.WorldSeed);
            WriteUInt32(blob, 44, header.AppVersionHash);
            WriteUInt32(blob, 48, header.SchemaHash);
            WriteUInt32(blob, 52, header.Reserved0);
            WriteUInt32(blob, 56, header.Reserved1);
            WriteUInt32(blob, 60, header.Reserved2);
        }

        private static void WriteDirectory(MemoryStream stream, in H8DataBlobDirectory directory)
        {
            WriteUInt32(stream, directory.Magic);
            WriteUInt16(stream, directory.FormatVersion);
            WriteUInt16(stream, directory.SectionCount);
            WriteUInt32(stream, directory.SectionTableOffset);
            WriteUInt32(stream, directory.SectionTableBytes);
            WriteUInt32(stream, directory.BlobBytes);
            WriteUInt32(stream, directory.DataStartOffset);
            WriteUInt32(stream, directory.LocalizationOffset);
            WriteUInt32(stream, directory.LocalizationBytes);
            WriteUInt32(stream, directory.Flags);
            WriteUInt32(stream, directory.WorldSeed);
            WriteUInt32(stream, directory.AppVersionHash);
            WriteUInt32(stream, directory.Reserved0);
            WriteUInt32(stream, directory.Reserved1);
            WriteUInt32(stream, directory.Reserved2);
            WriteUInt32(stream, directory.Reserved3);
            WriteUInt32(stream, directory.Reserved4);
        }

        private static void WriteSectionEntry(MemoryStream stream, in H8DataSectionEntry entry)
        {
            WriteUInt32(stream, entry.SectionId);
            WriteUInt32(stream, entry.RecordSize);
            WriteUInt32(stream, entry.Count);
            WriteUInt32(stream, entry.OffsetBytes);
        }

        private static void WriteUInt16(MemoryStream stream, ushort value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        private static void WriteUInt32(MemoryStream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
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

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                   ((uint)bytes[offset + 1] << 8) |
                   ((uint)bytes[offset + 2] << 16) |
                   ((uint)bytes[offset + 3] << 24);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return (ulong)bytes[offset] |
                   ((ulong)bytes[offset + 1] << 8) |
                   ((ulong)bytes[offset + 2] << 16) |
                   ((ulong)bytes[offset + 3] << 24) |
                   ((ulong)bytes[offset + 4] << 32) |
                   ((ulong)bytes[offset + 5] << 40) |
                   ((ulong)bytes[offset + 6] << 48) |
                   ((ulong)bytes[offset + 7] << 56);
        }

        private static void WriteStruct<T>(MemoryStream stream, T value)
            where T : unmanaged
        {
            EnsureLittleEndianEditorHost();
            int size = UnsafeUtility.SizeOf<T>();
            if (size > 256)
                throw new InvalidOperationException("[H8DataMonolithCompiler] Record struct exceeds stack emission scratch limit: " + typeof(T).Name);

            Span<byte> scratch = stackalloc byte[size];
            fixed (byte* ptr = scratch)
            {
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
            }

            stream.Write(scratch);
        }

        internal static ulong ComputeHash64(byte[] bytes, int offset, int count)
        {
            fixed (byte* ptr = bytes)
            {
                uint2 hash = xxHash3.Hash64(ptr + offset, count);
                return ((ulong)hash.y << 32) | hash.x;
            }
        }

        private static List<CsvRow> ReadCsvRows(string absolutePath)
        {
            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            List<CsvRow> rows = new List<CsvRow>(Math.Max(0, lines.Length - 1)); // COLD ALLOC: List<CsvRow>[csv row count] - editor-only source data import - owner: H8DataMonolithCompiler
            if (lines.Length <= 1)
                return rows;

            string[] headers = SplitCsvLine(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] values = SplitCsvLine(lines[i]);
                CsvRow row = new CsvRow(absolutePath, i + 1, -1, headers.Length);
                if (values.Length != headers.Length)
                {
                    throw new InvalidOperationException(
                        "[H8DataMonolithCompiler] CSV column count mismatch: file=" +
                        absolutePath +
                        ", line=" +
                        (i + 1).ToString(CultureInfo.InvariantCulture) +
                        ", headers=" +
                        headers.Length.ToString(CultureInfo.InvariantCulture) +
                        ", values=" +
                        values.Length.ToString(CultureInfo.InvariantCulture));
                }

                for (int j = 0; j < headers.Length; j++)
                    row.Fields[headers[j].Trim()] = values[j].Trim();
                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            List<string> values = new List<string>(16); // COLD ALLOC: List<string>[csv column count] - editor-only CSV parser scratch - owner: H8DataMonolithCompiler
            StringBuilder builder = new StringBuilder(128); // COLD ALLOC: StringBuilder[128] - editor-only CSV cell parser scratch - owner: H8DataMonolithCompiler
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }

                    continue;
                }

                if (c == ',' && !quoted)
                {
                    values.Add(builder.ToString());
                    builder.Length = 0;
                    continue;
                }

                builder.Append(c);
            }

            values.Add(builder.ToString());
            return values.ToArray();
        }

        private static void ValidateCsvRowHashes(string absolutePath, int lineNumber, CsvRow row, bool requireHashPairs)
        {
            int validatedPairs = 0;
            int idFieldCount = 0;
            foreach (KeyValuePair<string, string> field in row.Fields)
            {
                if (!IsAuthoredIdField(field.Key, field.Value, out string hashField))
                    continue;

                idFieldCount++;
                if (!row.Fields.TryGetValue(hashField, out string expectedHashText) ||
                    string.IsNullOrWhiteSpace(expectedHashText))
                {
                    if (requireHashPairs)
                        ThrowMissingCsvHash(absolutePath, lineNumber, field.Key, hashField);
                    continue;
                }

                uint expectedHash = H8DataHash.ComputeFnv1A32(field.Value.AsSpan());
                if (!TryParseUIntFlexible(expectedHashText, out uint authoredHash) || authoredHash != expectedHash)
                    ThrowCsvHashMismatch(absolutePath, lineNumber, field.Key, field.Value, hashField, expectedHashText, expectedHash);

                validatedPairs++;
            }

            if (requireHashPairs && idFieldCount > 0 && validatedPairs == 0)
                ThrowMissingCsvHash(absolutePath, lineNumber, "id", "hash32");
        }

        private static bool IsAuthoredIdField(string fieldName, string value, out string hashField)
        {
            hashField = string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(value))
                return false;

            if (fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            {
                hashField = fieldName.Substring(0, fieldName.Length - 3) + "_hash32";
                return true;
            }

            if (string.Equals(fieldName, "id", StringComparison.OrdinalIgnoreCase))
            {
                hashField = "hash32";
                return true;
            }

            if (string.Equals(fieldName, "output", StringComparison.OrdinalIgnoreCase))
            {
                hashField = "output_hash32";
                return true;
            }

            return false;
        }

        private static bool TryParseUIntFlexible(string value, out uint parsed)
        {
            parsed = 0u;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private static bool IsBalanceSourceFile(string absolutePath)
        {
            string balanceRoot = Path.GetFullPath(BalanceSourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(absolutePath);
            return normalizedPath.StartsWith(balanceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(balanceRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void ThrowMissingCsvHash(string absolutePath, int lineNumber, string idField, string hashField)
        {
            throw new InvalidOperationException(
                "[SIGNAL_AUTHORITY_VALIDATOR] CSV row missing FNV-1a hash pair. file=" +
                absolutePath +
                ", line=" +
                lineNumber +
                ", id_field=" +
                idField +
                ", expected_hash_field=" +
                hashField);
        }

        private static void ThrowCsvHashMismatch(
            string absolutePath,
            int lineNumber,
            string idField,
            string idValue,
            string hashField,
            string authoredHash,
            uint expectedHash)
        {
            throw new InvalidOperationException(
                "[SIGNAL_AUTHORITY_VALIDATOR] CSV FNV-1a hash mismatch. file=" +
                absolutePath +
                ", line=" +
                lineNumber +
                ", id_field=" +
                idField +
                ", id=" +
                idValue +
                ", hash_field=" +
                hashField +
                ", authored=" +
                authoredHash +
                ", expected=" +
                expectedHash);
        }

        private static int AddRecipeMask(string packedIds, ref uint mask0, ref uint mask1, ref uint mask2, ref uint mask3)
        {
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            return AddRecipeMaskAndHashes(packedIds, ref mask0, ref mask1, ref mask2, ref mask3, ref h0, ref h1, ref h2, ref h3);
        }

        private static int AddRecipeMask(string packedIds, ref ulong mask0, ref ulong mask1)
        {
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            return AddRecipeMaskAndHashes(packedIds, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
        }

        private static int AddRecipeMaskAndHashes(
            string packedIds,
            ref uint mask0,
            ref uint mask1,
            ref uint mask2,
            ref uint mask3,
            ref uint h0,
            ref uint h1,
            ref uint h2,
            ref uint h3)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0;

            int count = 0;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                uint hash = Hash(token);
                H8DataHash.AddHashToRecipeMask(hash, ref mask0, ref mask1, ref mask2, ref mask3);
                switch (count)
                {
                    case 0:
                        h0 = hash;
                        break;
                    case 1:
                        h1 = hash;
                        break;
                    case 2:
                        h2 = hash;
                        break;
                    case 3:
                        h3 = hash;
                        break;
                }

                count++;
            }

            return count;
        }

        private static int AddRecipeMaskAndHashes(
            string packedIds,
            ref ulong mask0,
            ref ulong mask1,
            ref uint h0,
            ref uint h1,
            ref uint h2,
            ref uint h3)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0;

            int count = 0;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                uint hash = Hash(token);
                H8DataHash.AddHashToRecipeMask(hash, ref mask0, ref mask1);
                switch (count)
                {
                    case 0:
                        h0 = hash;
                        break;
                    case 1:
                        h1 = hash;
                        break;
                    case 2:
                        h2 = hash;
                        break;
                    case 3:
                        h3 = hash;
                        break;
                }

                count++;
            }

            return count;
        }

        private static void ParseFirstTwoHashList(string packedIds, out uint hash0, out uint hash1)
        {
            hash0 = 0u;
            hash1 = 0u;
            if (string.IsNullOrWhiteSpace(packedIds))
                return;

            int count = 0;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length && count < 2)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                if (count == 0)
                    hash0 = Hash(token);
                else
                    hash1 = Hash(token);

                count++;
            }
        }

        private static uint ParseAppliedLoreRoutePacketHashes(
            string packedIds,
            out uint hash0,
            out uint hash1,
            out uint hash2,
            out uint hash3,
            out uint hash4,
            out uint hash5,
            out uint hash6,
            out uint hash7)
        {
            hash0 = 0u;
            hash1 = 0u;
            hash2 = 0u;
            hash3 = 0u;
            hash4 = 0u;
            hash5 = 0u;
            hash6 = 0u;
            hash7 = 0u;
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0u;

            uint count = 0u;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                if (count >= H8DataLayoutConstants.AppliedLoreRoutePacketCapacity)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Applied lore route packet list exceeds capacity=" + H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);

                uint hash = Hash(token);
                switch (count)
                {
                    case 0u: hash0 = hash; break;
                    case 1u: hash1 = hash; break;
                    case 2u: hash2 = hash; break;
                    case 3u: hash3 = hash; break;
                    case 4u: hash4 = hash; break;
                    case 5u: hash5 = hash; break;
                    case 6u: hash6 = hash; break;
                    case 7u: hash7 = hash; break;
                }

                count++;
            }

            return count;
        }

        private static uint ParseAppliedLoreRoutePrerequisiteHashes(
            string packedIds,
            out uint hash0,
            out uint hash1,
            out uint hash2,
            out uint hash3)
        {
            hash0 = 0u;
            hash1 = 0u;
            hash2 = 0u;
            hash3 = 0u;
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0u;

            uint count = 0u;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                if (count >= H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Applied lore route prerequisite list exceeds capacity=" + H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);

                uint hash = Hash(token);
                switch (count)
                {
                    case 0u: hash0 = hash; break;
                    case 1u: hash1 = hash; break;
                    case 2u: hash2 = hash; break;
                    case 3u: hash3 = hash; break;
                }

                count++;
            }

            return count;
        }

        private static string Get(CsvRow row, string key, string fallback)
        {
            return row.Fields.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static uint Hash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? 0u : H8DataHash.ComputeFnv1A32(value.AsSpan());
        }

        private static uint Hash(ReadOnlySpan<char> value)
        {
            return value.Length == 0 ? 0u : H8DataHash.ComputeFnv1A32(value);
        }

        private static ReadOnlySpan<char> TrimAscii(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= ' ')
                start++;
            while (end >= start && value[end] <= ' ')
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, (end - start) + 1);
        }

        private static uint ParseUInt(CsvRow row, string key, uint fallback)
        {
            string value = Get(row, key, string.Empty);
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            {
                return hex;
            }

            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : fallback;
        }

        private static int ParseInt(CsvRow row, string key, int fallback)
        {
            return int.TryParse(Get(row, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static long ParseLong(CsvRow row, string key, long fallback)
        {
            return long.TryParse(Get(row, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : fallback;
        }

        private static float ParseFloat(CsvRow row, string key, float fallback)
        {
            return float.TryParse(Get(row, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }

        private static double ParseDouble(CsvRow row, string key, double fallback)
        {
            return double.TryParse(Get(row, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
        }

        private static float Saturate(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static int CompareItemRecords(H8ItemRecord left, H8ItemRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static int CompareCreatureRecords(H8CreatureTraitRecord left, H8CreatureTraitRecord right)
        {
            return left.SpeciesHash.CompareTo(right.SpeciesHash);
        }

        private static int CompareBiomeRecords(H8BiomeRecord left, H8BiomeRecord right)
        {
            return left.BiomeHash.CompareTo(right.BiomeHash);
        }

        private static int CompareLootRows(CsvRow left, CsvRow right)
        {
            int tableCompare = Hash(Get(left, "table_id", Get(left, "table", string.Empty))).CompareTo(
                Hash(Get(right, "table_id", Get(right, "table", string.Empty))));
            if (tableCompare != 0)
                return tableCompare;

            return Hash(Get(left, "item_id", Get(left, "item", string.Empty))).CompareTo(
                Hash(Get(right, "item_id", Get(right, "item", string.Empty))));
        }

        private static int CompareVoxelMaterialRecords(H8VoxelMaterialRecord left, H8VoxelMaterialRecord right)
        {
            return left.VoxelHash.CompareTo(right.VoxelHash);
        }

        private static int CompareAudioClipRecords(H8AudioClipRegistryRecord left, H8AudioClipRegistryRecord right)
        {
            return left.EventHash.CompareTo(right.EventHash);
        }

        private static int CompareHullConstantRecords(H8SubmarineHullConstantRecord left, H8SubmarineHullConstantRecord right)
        {
            return left.PartHash.CompareTo(right.PartHash);
        }

        private static int ComparePhysicsMaterialRecords(H8PhysicsMaterialRecord left, H8PhysicsMaterialRecord right)
        {
            return left.SurfaceHash.CompareTo(right.SurfaceHash);
        }

        private static int CompareEconomyRecords(H8EconomyRecord left, H8EconomyRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static int ComparePhysicsConstantsRecords(H8PhysicsConstantsRecord left, H8PhysicsConstantsRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static int CompareAppliedLoreRecords(H8AppliedLorePacketRecord left, H8AppliedLorePacketRecord right)
        {
            int packetCompare = left.PacketHash.CompareTo(right.PacketHash);
            return packetCompare != 0 ? packetCompare : left.LocaleHash.CompareTo(right.LocaleHash);
        }

        private static int CompareAppliedLoreRouteRecords(H8AppliedLoreRouteRecord left, H8AppliedLoreRouteRecord right)
        {
            return left.RouteCardHash.CompareTo(right.RouteCardHash);
        }

        private static H8ItemRecord ToItemRecord(JsonItem item, LocalizationPool localizationPool)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            int ingredientCount = AddRecipeMask(item.recipe, ref mask0, ref mask1);
            uint nameOffset = localizationPool.Add(item.name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(item.description, out int descriptionBytes);
            return new H8ItemRecord
            {
                HashId = Hash(item.id),
                CategoryHash = Hash(item.category),
                Flags = item.flags,
                MaxStack = (ushort)Mathf.Clamp(item.maxStack, 0, ushort.MaxValue),
                RecipeIngredientCount = (ushort)Mathf.Clamp(ingredientCount, 0, ushort.MaxValue),
                RecipeMask0 = mask0,
                RecipeMask1 = mask1,
                MassKg = item.massKg,
                VolumeM3 = item.volumeM3,
                BaseQuality = item.quality,
                HeatCapacity = item.heatCapacity,
                YieldHash = Hash(item.yieldId),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes
            };
        }

        private static H8CreatureTraitRecord ToCreatureRecord(JsonCreature creature, LocalizationPool localizationPool)
        {
            uint displayNameOffset = localizationPool.Add(creature.name, out int displayNameBytes);
            return new H8CreatureTraitRecord
            {
                SpeciesHash = Hash(creature.id),
                MateMask = creature.mateMask,
                BiomeMask = creature.biomeMask,
                Flags = creature.flags,
                Genome = new H8CreatureGenomeTraitBlock
                {
                    Aggression = creature.aggression,
                    Metabolism = creature.metabolism,
                    MaxHealth = creature.maxHealth,
                    CruiseSpeed = creature.cruiseSpeed,
                    BurstSpeed = creature.burstSpeed,
                    SpawnCreditCost = creature.spawnCredit,
                    PressureMinMeters = creature.minDepth,
                    PressureMaxMeters = creature.maxDepth
                },
                DisplayNameUtf8Offset = displayNameOffset,
                LootTableHash = Hash(creature.lootTable),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8BiomeRecord ToBiomeRecord(JsonBiome biome, LocalizationPool localizationPool)
        {
            uint displayNameOffset = localizationPool.Add(biome.name, out int displayNameBytes);
            return new H8BiomeRecord
            {
                BiomeHash = Hash(biome.id),
                Flags = biome.flags,
                SurfaceId = Hash(biome.surfaceId),
                MinDepthMeters = biome.minDepth,
                MaxDepthMeters = biome.maxDepth,
                TemperatureCelsius = biome.temperatureC,
                PressureScalar = biome.pressureScalar,
                FogDensity = biome.fogDensity,
                LightScatterR = biome.scatterR,
                LightScatterG = biome.scatterG,
                LightScatterB = biome.scatterB,
                DisplayNameUtf8Offset = displayNameOffset,
                HeatmapId = Hash(biome.heatmapId),
                RadiationFieldHash = Hash(biome.radiationId),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RecipeRecord ToRecipeRecord(JsonRecipe recipe)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            int count = AddRecipeMaskAndHashes(recipe.ingredients, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
            return new H8RecipeRecord
            {
                OutputHash = Hash(recipe.output),
                StationHash = Hash(recipe.station),
                Flags = recipe.flags,
                IngredientCount = (uint)count,
                IngredientMask0 = mask0,
                IngredientMask1 = mask1,
                IngredientHash0 = h0,
                IngredientHash1 = h1,
                IngredientHash2 = h2,
                IngredientHash3 = h3,
                CraftSeconds = recipe.craftSeconds,
                OutputCount = recipe.outputCount == 0u ? 1u : recipe.outputCount
            };
        }

        private static CsvRow ToJsonItemReferenceRow(string absolutePath, int sourceIndex, JsonItem item)
        {
            CsvRow row = new CsvRow(absolutePath, 0, sourceIndex, 4);
            row.Fields["id"] = item.id;
            row.Fields["recipe"] = item.recipe;
            return row;
        }

        private static CsvRow ToJsonRecipeReferenceRow(string absolutePath, int sourceIndex, JsonRecipe recipe)
        {
            CsvRow row = new CsvRow(absolutePath, 0, sourceIndex, 4);
            row.Fields["output"] = recipe.output;
            row.Fields["station"] = recipe.station;
            row.Fields["ingredients"] = recipe.ingredients;
            return row;
        }

        private sealed class LocalizationPool
        {
            private readonly Dictionary<string, uint> _offsetByValue = new Dictionary<string, uint>(LocalizationPoolExpectedValueCapacity, StringComparer.Ordinal); // COLD ALLOC: Dictionary<string,uint>[source loc count] - editor-only localization pool de-duplication - owner: H8DataMonolithCompiler
            private readonly MemoryStream _bytes = new MemoryStream(LocalizationPoolInitialByteCapacity); // COLD ALLOC: MemoryStream[source UTF-8 block] - editor-only UTF-8 string block writer - owner: H8DataMonolithCompiler
            private readonly byte[] _scratch = new byte[Utf8ScratchBytes]; // COLD ALLOC: byte[16KB] - editor-only UTF-8 encoding scratch - owner: H8DataMonolithCompiler

            internal uint Add(string value)
            {
                return Add(value, out _);
            }

            internal uint Add(string value, out int byteCount)
            {
                if (string.IsNullOrEmpty(value))
                {
                    byteCount = 0;
                    return uint.MaxValue;
                }

                byteCount = Encoding.UTF8.GetByteCount(value);
                if (byteCount > _scratch.Length)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] UTF-8 localization entry exceeds scratch capacity: bytes=" + byteCount);

                if (_offsetByValue.TryGetValue(value, out uint offset))
                    return offset;

                if (_bytes.Position > uint.MaxValue)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] UTF-8 localization pool exceeded 4GB.");

                offset = (uint)_bytes.Position;
                int written = Encoding.UTF8.GetBytes(value, 0, value.Length, _scratch, 0);
                _bytes.Write(_scratch, 0, written);
                _bytes.WriteByte(0);
                _offsetByValue[value] = offset;
                return offset;
            }

            internal byte[] ToArray()
            {
                return _bytes.ToArray();
            }
        }

        private sealed class CsvRow
        {
            internal readonly Dictionary<string, string> Fields;
            internal readonly string AbsolutePath;
            internal readonly int LineNumber;
            internal readonly int SourceIndex;

            internal CsvRow(string absolutePath, int lineNumber, int sourceIndex, int fieldCapacity)
            {
                Fields = new Dictionary<string, string>(Math.Max(4, fieldCapacity), StringComparer.OrdinalIgnoreCase);
                AbsolutePath = absolutePath;
                LineNumber = lineNumber;
                SourceIndex = sourceIndex;
            }
        }

        private sealed class CsvFileRows
        {
            internal readonly string AbsolutePath;
            internal readonly List<CsvRow> Rows;

            internal CsvFileRows(string absolutePath, List<CsvRow> rows)
            {
                AbsolutePath = absolutePath;
                Rows = rows;
            }
        }

        private sealed class DataSet
        {
            internal readonly List<H8ItemRecord> Items = new List<H8ItemRecord>(256);
            internal readonly List<CsvRow> RawItemRows = new List<CsvRow>(256);
            internal readonly List<H8CreatureTraitRecord> Creatures = new List<H8CreatureTraitRecord>(128);
            internal readonly List<H8BiomeRecord> Biomes = new List<H8BiomeRecord>(64);
            internal readonly List<H8RecipeRecord> Recipes = new List<H8RecipeRecord>(256);
            internal readonly List<CsvRow> RawRecipeRows = new List<CsvRow>(256);
            internal readonly List<H8BiomeHeatmapCellRecord> BiomeHeatmap = new List<H8BiomeHeatmapCellRecord>(1024);
            internal readonly List<H8QuestNodeRecord> QuestNodes = new List<H8QuestNodeRecord>(128);
            internal readonly List<H8QuestEdgeRecord> QuestEdges = new List<H8QuestEdgeRecord>(256);
            internal readonly List<H8LootCdfRecord> LootCdf = new List<H8LootCdfRecord>(256);
            internal readonly List<CsvRow> RawLootRows = new List<CsvRow>(256);
            internal readonly List<CsvRow> RawEconomyRows = new List<CsvRow>(128);
            internal readonly List<H8VoxelMaterialRecord> VoxelMaterials = new List<H8VoxelMaterialRecord>(128);
            internal readonly List<H8AudioClipRegistryRecord> AudioClips = new List<H8AudioClipRegistryRecord>(256);
            internal readonly List<H8VfxScalarRecord> VfxScalars = new List<H8VfxScalarRecord>(128);
            internal readonly List<H8DepthPressureSampleRecord> DepthPressureCurve = new List<H8DepthPressureSampleRecord>(256);
            internal readonly List<H8ToolHeatCapacityRecord> ToolHeat = new List<H8ToolHeatCapacityRecord>(64);
            internal readonly List<H8SubmarineHullConstantRecord> HullConstants = new List<H8SubmarineHullConstantRecord>(64);
            internal readonly List<H8NarrativeTriggerRecord> NarrativeTriggers = new List<H8NarrativeTriggerRecord>(256);
            internal readonly List<H8PhysicsMaterialRecord> PhysicsMaterials = new List<H8PhysicsMaterialRecord>(64);
            internal readonly List<H8GhostModuleRecord> GhostModules = new List<H8GhostModuleRecord>(128);
            internal readonly List<H8RadiationIntensityCellRecord> RadiationCells = new List<H8RadiationIntensityCellRecord>(256);
            internal readonly List<H8SpawnCreditCostRecord> SpawnCredits = new List<H8SpawnCreditCostRecord>(128);
            internal readonly List<H8LightAttenuationSampleRecord> LightAttenuationCurve = new List<H8LightAttenuationSampleRecord>(256);
            internal readonly List<H8SopErrorRecord> SopErrors = new List<H8SopErrorRecord>(128);
            internal readonly List<H8HudLayoutRecord> HudLayouts = new List<H8HudLayoutRecord>(64);
            internal readonly List<H8SectorPageRecord> SectorPages = new List<H8SectorPageRecord>(64);
            internal readonly List<H8EconomyRecord> Economy = new List<H8EconomyRecord>(128);
            internal readonly List<H8PhysicsConstantsRecord> PhysicsConstants = new List<H8PhysicsConstantsRecord>(64);
            internal readonly List<H8AppliedLorePacketRecord> AppliedLorePackets = new List<H8AppliedLorePacketRecord>(8192);
            internal readonly List<H8AppliedLoreRouteRecord> AppliedLoreRoutes = new List<H8AppliedLoreRouteRecord>(512);
        }

        // Assigned by Unity JsonUtility during editor/offline bake.
#pragma warning disable CS0649
        [Serializable] private sealed class JsonRoot { public JsonItem[] items; public JsonCreature[] creatures; public JsonBiome[] biomes; public JsonRecipe[] recipes; }
        [Serializable] private sealed class JsonItem { public string id; public string category; public uint flags; public int maxStack = 1; public string recipe; public float massKg = 1f; public float volumeM3 = 0.001f; public float quality = 1f; public float heatCapacity; public string yieldId; public string name; public string description; }
        [Serializable] private sealed class JsonCreature { public string id; public uint mateMask; public uint biomeMask; public uint flags; public float aggression; public float metabolism = 1f; public float maxHealth = 1f; public float cruiseSpeed = 1f; public float burstSpeed = 1f; public float spawnCredit = 1f; public string name; public string lootTable; public float minDepth; public float maxDepth; }
        [Serializable] private sealed class JsonBiome { public string id; public uint flags; public string surfaceId; public float minDepth; public float maxDepth; public float temperatureC = 2f; public float pressureScalar = 1f; public float fogDensity; public float scatterR = 0.08f; public float scatterG = 0.18f; public float scatterB = 0.24f; public string name; public string heatmapId; public string radiationId; }
        [Serializable] private sealed class JsonRecipe { public string output; public string station; public uint flags; public string ingredients; public float craftSeconds = 1f; public uint outputCount = 1u; }
#pragma warning restore CS0649
    }

    internal sealed class H8DataMonolithBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -9100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!H8DataMonolithCompiler.BakeAll(logSummary: false))
            {
                throw new BuildFailedException(
                    "[H8DataMonolith] Prebuild bake failed: " +
                    H8DataMonolithCompiler.LastError);
            }

            if (!H8DataMonolithCompiler.TryValidateOutputBlob(out string error))
                throw new BuildFailedException("[H8DataMonolith] Prebuild validation failed: " + error);
        }
    }

    internal sealed class H8DataMonolithSourceWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!TouchesSourceData(importedAssets) &&
                !TouchesSourceData(deletedAssets) &&
                !TouchesSourceData(movedAssets) &&
                !TouchesSourceData(movedFromAssetPaths))
            {
                return;
            }

            H8DataMonolithFileSystemWatcher.RequestBake();
        }

        private static bool TouchesSourceData(string[] paths)
        {
            if (paths == null)
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                if (IsGeneratedAppliedLoreSourcePath(paths[i]))
                    continue;

                if (H8DataMonolithCompiler.IsSourcePath(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool IsGeneratedAppliedLoreSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalized = path.Replace('\\', '/');
            return normalized.EndsWith("/Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv", StringComparison.OrdinalIgnoreCase);
        }
    }

    [InitializeOnLoad]
    internal static class H8DataMonolithFileSystemWatcher
    {
        private const double AutoBakeDebounceSeconds = 0.75d;
        private static FileSystemWatcher _sourceWatcher;
        private static FileSystemWatcher _balanceWatcher;
        private static int _pendingBake;
        private static int _bakeInProgress;
        private static long _lastSourceChangeTicks;

        static H8DataMonolithFileSystemWatcher()
        {
            EditorApplication.update -= DrainPendingBake;
            EditorApplication.update += DrainPendingBake;
            StartWatcher();
        }

        private static void StartWatcher()
        {
            StopWatcher();
            _sourceWatcher = StartWatcherFor(Path.GetFullPath(H8DataMonolithCompiler.SourceFolder));
            _balanceWatcher = StartWatcherFor(Path.GetFullPath("Data/Balance"));
        }

        internal static void RequestBake()
        {
            Interlocked.Exchange(ref _lastSourceChangeTicks, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _pendingBake, 1);
        }

        private static FileSystemWatcher StartWatcherFor(string absoluteSourceFolder)
        {
            Directory.CreateDirectory(absoluteSourceFolder);
            FileSystemWatcher watcher = new FileSystemWatcher(absoluteSourceFolder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed += HandleSourceChanged;
            watcher.Created += HandleSourceChanged;
            watcher.Deleted += HandleSourceChanged;
            watcher.Renamed += HandleSourceRenamed;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private static void StopWatcher()
        {
            StopWatcher(ref _sourceWatcher);
            StopWatcher(ref _balanceWatcher);
        }

        private static void StopWatcher(ref FileSystemWatcher watcher)
        {
            if (watcher == null)
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleSourceChanged;
            watcher.Created -= HandleSourceChanged;
            watcher.Deleted -= HandleSourceChanged;
            watcher.Renamed -= HandleSourceRenamed;
            watcher.Dispose();
            watcher = null;
        }

        private static void HandleSourceChanged(object sender, FileSystemEventArgs args)
        {
            if (IsDataSourcePath(args.FullPath))
                RequestBake();
        }

        private static void HandleSourceRenamed(object sender, RenamedEventArgs args)
        {
            if (IsDataSourcePath(args.FullPath) || IsDataSourcePath(args.OldFullPath))
                RequestBake();
        }

        private static bool IsDataSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return !H8DataMonolithCompiler.IsGeneratedBalancePath(path) &&
                   (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static void DrainPendingBake()
        {
            if (Volatile.Read(ref _pendingBake) == 0)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling)
                return;

            long lastChangeTicks = Volatile.Read(ref _lastSourceChangeTicks);
            long elapsedTicks = Stopwatch.GetTimestamp() - lastChangeTicks;
            long requiredTicks = (long)(Stopwatch.Frequency * AutoBakeDebounceSeconds);
            if (elapsedTicks < requiredTicks)
                return;

            if (Interlocked.CompareExchange(ref _bakeInProgress, 1, 0) != 0)
                return;

            try
            {
                if (Interlocked.Exchange(ref _pendingBake, 0) != 0)
                    H8DataMonolithCompiler.BakeAll(logSummary: false);
            }
            finally
            {
                Interlocked.Exchange(ref _bakeInProgress, 0);
            }
        }
    }

    [InitializeOnLoad]
    internal static class H8DataMonolithHotReloadSocket
    {
        private const int Port = 48088;
        private const string ReloadPrefix = "RELOAD ";
        private const int MaxReloadPacketChars = 1024;
        private static readonly object QueueLock = new object();
        private static TcpListener _listener;
        private static Thread _thread;
        private static string _pendingPath;
        private static int _running;

        static H8DataMonolithHotReloadSocket()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= DrainMainThread;
            EditorApplication.update += DrainMainThread;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;

            if (EditorApplication.isPlaying)
                Start();
        }

        internal static void NotifyBake(string outputAssetPath)
        {
            if (!EditorApplication.isPlaying || !H8StaticDataArena.IsLoaded)
                return;

            string absolutePath = Path.GetFullPath(outputAssetPath);
            if (IsAllowedReloadPath(absolutePath))
                QueueReload(absolutePath);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                Start();
            else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Stop();
        }

        private static void Start()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return;

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start(4);
                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "H8.DataMonolith.HotReload"
                };
                _thread.Start();
            }
            catch (SocketException ex)
            {
                HandleStartFailure(ex);
            }
            catch (ObjectDisposedException ex)
            {
                HandleStartFailure(ex);
            }
            catch (InvalidOperationException ex)
            {
                HandleStartFailure(ex);
            }
            catch (ThreadStateException ex)
            {
                HandleStartFailure(ex);
            }
            catch (System.Security.SecurityException ex)
            {
                HandleStartFailure(ex);
            }
        }

        private static void HandleStartFailure(Exception ex)
        {
            Interlocked.Exchange(ref _running, 0);
            try
            {
                _listener?.Stop();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            _listener = null;
            _thread = null;
            Debug.LogWarning("[H8DataMonolithHotReloadSocket] Socket bridge unavailable: " + ex.Message);
        }

        private static void Stop()
        {
            Interlocked.Exchange(ref _running, 0);
            try
            {
                _listener?.Stop();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            _listener = null;
            _thread = null;
            lock (QueueLock)
                _pendingPath = null;
        }

        private static void ListenLoop()
        {
            while (Volatile.Read(ref _running) != 0)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, false))
                    {
                        string line = reader.ReadLine();
                        if (!string.IsNullOrEmpty(line) &&
                            line.Length <= MaxReloadPacketChars &&
                            line.StartsWith(ReloadPrefix, StringComparison.Ordinal))
                        {
                            string path = line.Substring(ReloadPrefix.Length);
                            if (IsAllowedReloadPath(path))
                                QueueReload(Path.GetFullPath(path));
                        }
                    }
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (IOException ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
                catch (ArgumentException ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
                catch (System.Security.SecurityException ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
            }
        }

        private static void QueueReload(string absolutePath)
        {
            lock (QueueLock)
                _pendingPath = absolutePath;
        }

        private static bool IsAllowedReloadPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string expectedPath = Path.GetFullPath(H8DataMonolithCompiler.OutputAssetPath);
                return string.Equals(fullPath, expectedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        private static void DrainMainThread()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (!H8StaticDataArena.IsLoaded)
            {
                lock (QueueLock)
                    _pendingPath = null;
                return;
            }

            string path;
            lock (QueueLock)
            {
                path = _pendingPath;
                _pendingPath = null;
            }

            if (string.IsNullOrEmpty(path))
                return;

            if (!H8StaticDataArena.EditorHotReloadFromFile(path, out H8DataBlobLoadStatus status))
                Debug.LogWarning("[H8DataMonolithHotReloadSocket] Hot reload failed: " + status);
        }
    }
}
#endif
