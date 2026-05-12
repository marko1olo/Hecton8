#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - SpaceEngineResearchSmokeTester.cs
// Editor-only guard for SpaceEngine 0.9.9 atmosphere/scale research artifacts.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class SpaceEngineResearchSmokeTester
    {
        private const string MenuPath = "Hecton/Validation/Validate SpaceEngine Research";
        private const string StressMenuPath = "Hecton/Validation/Stress SpaceEngine Research";
        private const string OutputRelativePath = "Library/SpaceEngineResearchSmokeTester.json";
        private const string SpaceEngineRootEnvironmentVariable = "HECTON_SPACEENGINE_ROOT";
        private const int StressPassCount = 3;

        [MenuItem(MenuPath, priority = 262)]
        public static void RunMenuItem()
        {
            bool passed = Run(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);
        }

        [MenuItem(StressMenuPath, priority = 263)]
        public static void RunStressMenuItem()
        {
            bool passed = RunStress(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);
        }

        public static void RunBatch()
        {
            bool passed = RunStress(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);

            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string json)
        {
            string projectRoot = ResolveProjectRoot();
            string spaceEngineRoot = ResolveSpaceEngineRoot();
            SpaceEngineResearchAuditResult result = SpaceEngineResearchAudit.Execute(projectRoot, spaceEngineRoot);
            SpaceEngineResearchTelemetryReporter.PublishIfFailed(result);
            json = SpaceEngineResearchJsonWriter.Write(result);
            SpaceEngineResearchJsonWriter.TryWriteArtifact(projectRoot, json);
            return result.Passed;
        }

        public static bool RunStress(out string json)
        {
            string projectRoot = ResolveProjectRoot();
            string spaceEngineRoot = ResolveSpaceEngineRoot();
            SpaceEngineResearchStressResult result =
                SpaceEngineResearchStressRunner.Execute(projectRoot, spaceEngineRoot, StressPassCount);

            SpaceEngineResearchTelemetryReporter.PublishIfFailed(result.FinalAudit);
            json = SpaceEngineResearchJsonWriter.WriteStress(result);
            SpaceEngineResearchJsonWriter.TryWriteArtifact(projectRoot, json);
            return result.Passed;
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            return dataDirectory == null ? Directory.GetCurrentDirectory() : dataDirectory.FullName;
        }

        private static string ResolveSpaceEngineRoot()
        {
            string configuredRoot = global::System.Environment.GetEnvironmentVariable(SpaceEngineRootEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredRoot))
                return configuredRoot;

#if UNITY_EDITOR_WIN
            return Path.Combine("C:" + Path.DirectorySeparatorChar, "GOG Games", "SpaceEngine");
#else
            return string.Empty;
#endif
        }

        internal static string ResolveOutputPath(string projectRoot)
        {
            return Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    internal static class SpaceEngineResearchAudit
    {
        private const int MaxReportLineCount = 800;
        private const int ExpectedShaderEntryCount = 137;
        private const int ExpectedEncryptedShaderEntryCount = 137;
        private const string ResearchReportRelativePath = "Docs/SPACE_ENGINE_RESEARCH/ATMOSPHERE_AND_SCALE_099.md";
        private const string ReferenceKernelRelativeFolder = "Docs/SPACE_ENGINE_RESEARCH/ReferenceKernels";

        private static readonly string[] RequiredReportAnchors =
        {
            "MINING COMPLETE",
            "PENDING VERIFICATION",
            "Raw GLSL status",
            "ReferenceKernels",
            "Regression Model",
            "Hot Path Impact",
            "Failure Modes"
        };

        private static readonly string[] RequiredKernelAnchors =
        {
            "H8SingleScatterLow",
            "H8GasBandBakeJob",
            "H8RingShadowTransmittance",
            "H8LogDepth01",
            "H8TwistedDiskUv"
        };

        private static readonly string[] ExpectedShaderEntries =
        {
            "atmo_transm.glsl",
            "tg_gasgiant_color.glsl",
            "tg_gasgiant_height.glsl",
            "rings_raymarch.glsl",
            "emu_double.glh",
            "einstein.glsl"
        };

        private static readonly string[] ExpectedAtmosphereEntries =
        {
            "atmospheres.cfg",
            "Earth.atm",
            "Jupiter.atm",
            "Neptune.atm"
        };

        private static readonly string[] ExpectedCatalogEntries =
        {
            "planets/SolarSys.sc",
            "planets/SpaceEngine.sc"
        };

        private static readonly string[] EditorValidationRelativeFiles =
        {
            "Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs",
            "Assets/_Project/Scripts/Editor/SpaceEngineResearchJsonWriter.cs",
            "Assets/_Project/Scripts/Editor/SpaceEngineResearchContracts.cs"
        };

        public static SpaceEngineResearchAuditResult Execute(string projectRoot, string spaceEngineRoot)
        {
            SpaceEngineResearchAuditResult result = new SpaceEngineResearchAuditResult
            {
                ProjectRoot = projectRoot,
                SpaceEngineRoot = spaceEngineRoot,
                MaxReportLineCount = MaxReportLineCount
            };

            AuditReport(projectRoot, result);
            AuditReferenceKernels(projectRoot, result);
            AuditArchives(spaceEngineRoot, result);
            AuditRecentScope(projectRoot, result);
            AuditEditorValidationFiles(projectRoot, result);

            result.Passed = result.FailureCount == 0;
            return result;
        }

        private static void AuditReport(string projectRoot, SpaceEngineResearchAuditResult result)
        {
            string reportPath = Path.Combine(projectRoot, ResearchReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            result.ReportPath = reportPath;

            if (!File.Exists(reportPath))
            {
                AddFailure(result, "Research report missing: " + reportPath);
                return;
            }

            string[] lines = File.ReadAllLines(reportPath);
            result.ReportLineCount = lines.Length;
            if (lines.Length > MaxReportLineCount)
                AddFailure(result, "Research report exceeds decomposition limit: " + lines.Length + " > " + MaxReportLineCount);

            string text = File.ReadAllText(reportPath);
            for (int i = 0; i < RequiredReportAnchors.Length; i++)
            {
                if (text.IndexOf(RequiredReportAnchors[i], StringComparison.Ordinal) < 0)
                    AddFailure(result, "Report anchor missing: " + RequiredReportAnchors[i]);
            }
        }

        private static void AuditReferenceKernels(string projectRoot, SpaceEngineResearchAuditResult result)
        {
            string folder = Path.Combine(projectRoot, ReferenceKernelRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            result.ReferenceKernelFolder = folder;

            if (!Directory.Exists(folder))
            {
                AddFailure(result, "Reference kernel folder missing: " + folder);
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            result.ReferenceKernelFileCount = files.Length;

            StringBuilder builder = new StringBuilder(4096);
            for (int i = 0; i < files.Length; i++)
                builder.Append(File.ReadAllText(files[i]));

            string text = builder.ToString();
            for (int i = 0; i < RequiredKernelAnchors.Length; i++)
            {
                if (text.IndexOf(RequiredKernelAnchors[i], StringComparison.Ordinal) < 0)
                    AddFailure(result, "Reference kernel anchor missing: " + RequiredKernelAnchors[i]);
            }
        }

        private static void AuditEditorValidationFiles(string projectRoot, SpaceEngineResearchAuditResult result)
        {
            int fileCount = 0;
            int maxLineCount = 0;

            for (int i = 0; i < EditorValidationRelativeFiles.Length; i++)
            {
                string path = Path.Combine(
                    projectRoot,
                    EditorValidationRelativeFiles[i].Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(path))
                {
                    AddFailure(result, "Editor validation file missing: " + EditorValidationRelativeFiles[i]);
                    continue;
                }

                int lineCount = File.ReadAllLines(path).Length;
                fileCount++;
                if (lineCount > maxLineCount)
                    maxLineCount = lineCount;
                if (lineCount > MaxReportLineCount)
                    AddFailure(result, "Editor validation file exceeds decomposition limit: " + EditorValidationRelativeFiles[i]);
            }

            result.EditorValidationFileCount = fileCount;
            result.MaxEditorValidationLineCount = maxLineCount;
        }

        private static void AuditArchives(string spaceEngineRoot, SpaceEngineResearchAuditResult result)
        {
            string shaderPak = Path.Combine(spaceEngineRoot, "data", "shaders", "Shaders.pak");
            string atmospherePak = Path.Combine(spaceEngineRoot, "data", "models", "atmospheres", "Atmospheres.pak");
            string catalogPak = Path.Combine(spaceEngineRoot, "data", "catalogs", "Catalogs.pak");

            result.ShaderPak = SpaceEngineZipCentralDirectoryProbe.Probe(shaderPak, ExpectedShaderEntries);
            result.AtmospherePak = SpaceEngineZipCentralDirectoryProbe.Probe(atmospherePak, ExpectedAtmosphereEntries);
            result.CatalogPak = SpaceEngineZipCentralDirectoryProbe.Probe(catalogPak, ExpectedCatalogEntries);

            if (!result.ShaderPak.Exists)
            {
                AddFailure(result, "Shader archive missing: " + shaderPak);
            }
            else
            {
                if (result.ShaderPak.EntryCount != ExpectedShaderEntryCount)
                    AddFailure(result, "Shader archive entry count changed: " + result.ShaderPak.EntryCount);
                if (result.ShaderPak.EncryptedEntryCount != ExpectedEncryptedShaderEntryCount)
                    AddFailure(result, "Shader archive encrypted count changed: " + result.ShaderPak.EncryptedEntryCount);
                if (result.ShaderPak.ExpectedMissingCount != 0)
                    AddFailure(result, "Shader archive expected entries missing: " + result.ShaderPak.ExpectedMissingCount);

                result.NoPasswordProbeStatus =
                    result.ShaderPak.EncryptedEntryCount > 0
                        ? "BLOCKED_BY_ENCRYPTED_ZIP_FLAGS_NO_PASSWORD_BYPASS"
                        : "NO_ENCRYPTION_FLAG_DETECTED";
            }

            if (!result.AtmospherePak.Exists)
            {
                AddFailure(result, "Atmosphere archive missing: " + atmospherePak);
            }
            else
            {
                if (result.AtmospherePak.EncryptedEntryCount != 0)
                    AddFailure(result, "Atmosphere archive unexpectedly encrypted: " + result.AtmospherePak.EncryptedEntryCount);
                if (result.AtmospherePak.ExpectedMissingCount != 0)
                    AddFailure(result, "Atmosphere archive expected entries missing: " + result.AtmospherePak.ExpectedMissingCount);
            }

            if (!result.CatalogPak.Exists)
            {
                AddFailure(result, "Catalog archive missing: " + catalogPak);
            }
            else
            {
                if (result.CatalogPak.EncryptedEntryCount != 0)
                    AddFailure(result, "Catalog archive unexpectedly encrypted: " + result.CatalogPak.EncryptedEntryCount);
                if (result.CatalogPak.ExpectedMissingCount != 0)
                    AddFailure(result, "Catalog archive expected entries missing: " + result.CatalogPak.ExpectedMissingCount);
            }
        }

        private static void AuditRecentScope(string projectRoot, SpaceEngineResearchAuditResult result)
        {
            string researchFolder = Path.Combine(projectRoot, "Docs/SPACE_ENGINE_RESEARCH".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(researchFolder))
            {
                AddFailure(result, "Research folder missing during self-audit: " + researchFolder);
                return;
            }

            string[] files = Directory.GetFiles(researchFolder, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (!IsOwnedResearchAuditFile(researchFolder, files[i]))
                    continue;

                string extension = Path.GetExtension(files[i]);
                if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text = File.ReadAllText(files[i]);
                result.NativeCollectionTokenCount += CountToken(text, "NativeArray") + CountToken(text, "NativeList") + CountToken(text, "NativeQueue");
                result.JobBarrierTokenCount += CountToken(text, ".Complete()") + CountToken(text, ".Run()");
                result.StaticInstanceTokenCount += CountToken(text, "private static") + CountToken(text, "DontDestroyOnLoad");
                result.HotPathStringTokenCount += CountToken(text, ".ToString()") + CountToken(text, "string.Format") + CountToken(text, "$\"");
            }

            result.RecentScopeRuntimeCsFileCount = 0;
            result.RecentScopeRuntimeNativeCollectionCount = 0;
        }

        private static bool IsOwnedResearchAuditFile(string researchFolder, string path)
        {
            string relative = path.Substring(researchFolder.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            return relative.StartsWith("ReferenceKernels/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(relative, "ATMOSPHERE_AND_SCALE_099.md", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(relative, "OMEGA_AUTONOMY_AUDIT.md", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(relative, "SpaceEngineResearchSmokeTester.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int next = text.IndexOf(token, index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                count++;
                index = next + token.Length;
            }

            return count;
        }

        private static void AddFailure(SpaceEngineResearchAuditResult result, string failure)
        {
            result.FailureCount++;
            if (result.Failures.Count < 32)
                result.Failures.Add(failure);
        }
    }

    internal static class SpaceEngineZipCentralDirectoryProbe
    {
        private const uint EndOfCentralDirectorySignature = 0x06054B50u;
        private const uint CentralDirectoryHeaderSignature = 0x02014B50u;
        private const int MaxEocdSearchBytes = 66000;

        public static SpaceEngineZipProbeResult Probe(string path, string[] expectedEntries)
        {
            SpaceEngineZipProbeResult result = new SpaceEngineZipProbeResult
            {
                Path = path,
                ExpectedEntryCount = expectedEntries.Length
            };

            if (!File.Exists(path))
                return result;

            result.Exists = true;
            using (FileStream stream = File.OpenRead(path))
            {
                long tailLengthLong = Math.Min(stream.Length, MaxEocdSearchBytes);
                int tailLength = (int)tailLengthLong;
                byte[] tail = new byte[tailLength];
                stream.Seek(stream.Length - tailLength, SeekOrigin.Begin);
                ReadExact(stream, tail, 0, tail.Length);

                int eocd = FindEndOfCentralDirectory(tail);
                if (eocd < 0)
                {
                    result.ParseError = "EOCD_NOT_FOUND";
                    return result;
                }

                uint centralDirectorySize = ReadUInt32(tail, eocd + 12);
                uint centralDirectoryOffset = ReadUInt32(tail, eocd + 16);
                if (centralDirectorySize == 0u || centralDirectoryOffset >= stream.Length)
                {
                    result.ParseError = "INVALID_CENTRAL_DIRECTORY";
                    return result;
                }

                if (centralDirectorySize > int.MaxValue)
                {
                    result.ParseError = "CENTRAL_DIRECTORY_TOO_LARGE";
                    return result;
                }

                byte[] centralDirectory = new byte[(int)centralDirectorySize];
                stream.Seek(centralDirectoryOffset, SeekOrigin.Begin);
                ReadExact(stream, centralDirectory, 0, centralDirectory.Length);
                ParseCentralDirectory(centralDirectory, expectedEntries, result);
            }

            result.ExpectedMissingCount = expectedEntries.Length - result.ExpectedFoundCount;
            return result;
        }

        private static void ParseCentralDirectory(
            byte[] centralDirectory,
            string[] expectedEntries,
            SpaceEngineZipProbeResult result)
        {
            int offset = 0;
            while (offset + 46 <= centralDirectory.Length)
            {
                uint signature = ReadUInt32(centralDirectory, offset);
                if (signature != CentralDirectoryHeaderSignature)
                {
                    result.ParseError = "CENTRAL_DIRECTORY_HEADER_MISMATCH";
                    return;
                }

                ushort flags = ReadUInt16(centralDirectory, offset + 8);
                ushort method = ReadUInt16(centralDirectory, offset + 10);
                ushort nameLength = ReadUInt16(centralDirectory, offset + 28);
                ushort extraLength = ReadUInt16(centralDirectory, offset + 30);
                ushort commentLength = ReadUInt16(centralDirectory, offset + 32);

                if (offset + 46 + nameLength + extraLength + commentLength > centralDirectory.Length)
                {
                    result.ParseError = "CENTRAL_DIRECTORY_ENTRY_OVERRUN";
                    return;
                }

                string name = Encoding.UTF8.GetString(centralDirectory, offset + 46, nameLength);
                result.EntryCount++;
                if ((flags & 0x0001) != 0)
                    result.EncryptedEntryCount++;
                if (method == 0)
                    result.StoredEntryCount++;
                else
                    result.CompressedEntryCount++;

                for (int i = 0; i < expectedEntries.Length; i++)
                {
                    if (string.Equals(name, expectedEntries[i], StringComparison.OrdinalIgnoreCase))
                    {
                        result.ExpectedFoundCount++;
                        break;
                    }
                }

                offset += 46 + nameLength + extraLength + commentLength;
            }
        }

        private static int FindEndOfCentralDirectory(byte[] tail)
        {
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (ReadUInt32(tail, i) == EndOfCentralDirectorySignature)
                    return i;
            }

            return -1;
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read <= 0)
                    throw new EndOfStreamException();

                totalRead += read;
            }
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] |
                          (buffer[offset + 1] << 8) |
                          (buffer[offset + 2] << 16) |
                          (buffer[offset + 3] << 24));
        }
    }

    internal static class SpaceEngineResearchStressRunner
    {
        public static SpaceEngineResearchStressResult Execute(string projectRoot, string spaceEngineRoot, int passCount)
        {
            SpaceEngineResearchStressResult result = new SpaceEngineResearchStressResult
            {
                ProjectRoot = projectRoot,
                SpaceEngineRoot = spaceEngineRoot,
                PassCount = Math.Max(1, passCount)
            };

            SpaceEngineResearchAuditResult baseline = null;
            SpaceEngineResearchAuditResult latest = null;
            for (int i = 0; i < result.PassCount; i++)
            {
                latest = SpaceEngineResearchAudit.Execute(projectRoot, spaceEngineRoot);
                if (!latest.Passed)
                    AddFailure(result, "Audit pass failed: " + i);

                if (baseline == null)
                {
                    baseline = latest;
                    continue;
                }

                if (!HasStableCounts(baseline, latest))
                    AddFailure(result, "Audit counts changed between stress passes: " + i);
            }

            result.FinalAudit = latest;
            result.Passed = result.FailureCount == 0 && latest != null && latest.Passed;
            return result;
        }

        private static bool HasStableCounts(SpaceEngineResearchAuditResult baseline, SpaceEngineResearchAuditResult current)
        {
            return baseline.ReportLineCount == current.ReportLineCount &&
                   baseline.ReferenceKernelFileCount == current.ReferenceKernelFileCount &&
                   baseline.ShaderPak.EntryCount == current.ShaderPak.EntryCount &&
                   baseline.ShaderPak.EncryptedEntryCount == current.ShaderPak.EncryptedEntryCount &&
                   baseline.AtmospherePak.EncryptedEntryCount == current.AtmospherePak.EncryptedEntryCount &&
                   baseline.CatalogPak.EncryptedEntryCount == current.CatalogPak.EncryptedEntryCount &&
                   baseline.FailureCount == current.FailureCount;
        }

        private static void AddFailure(SpaceEngineResearchStressResult result, string failure)
        {
            result.FailureCount++;
            if (result.Failures.Count < 32)
                result.Failures.Add(failure);
        }
    }

    internal static class SpaceEngineResearchTelemetryReporter
    {
        private const uint SpaceEngineResearchFailureHash = 0x5E099001u;
        private const uint SpaceEngineResearchContextHash = 0xA7F00599u;

        public static void PublishIfFailed(SpaceEngineResearchAuditResult result)
        {
            if (result == null || result.Passed)
                return;

            result.TelemetryWarningRequested = true;
            result.TelemetryRuntimeEligible = Application.isPlaying;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SpaceEngineResearchFailureHash,
                SpaceEngineResearchContextHash,
                result.FailureCount);
        }
    }

}
#endif
