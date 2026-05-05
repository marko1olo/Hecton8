// Status: REFERENCE
// Standalone smoke tester for Docs/SPACE_ENGINE_RESEARCH.
// Runs outside Unity; the Unity Editor telemetry hook lives in Assets/_Project/Scripts/Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class SpaceEngineResearchSmokeTester
{
    private const int MaxReportLineCount = 800;
    private const int ExpectedShaderEntryCount = 137;
    private const int ExpectedEncryptedShaderEntryCount = 137;
    private const int StressPassCount = 3;

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

    public static int Main(string[] args)
    {
        string projectRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : ResolveProjectRoot();
        string spaceEngineRoot = args.Length > 1 ? args[1] : @"C:\GOG Games\SpaceEngine";
        StressResult stress = RunStress(projectRoot, spaceEngineRoot, StressPassCount);
        string json = JsonWriter.WriteStress(stress);
        Console.WriteLine(json);

        string outputPath = Path.Combine(projectRoot, "Docs", "SPACE_ENGINE_RESEARCH", "SpaceEngineResearchSmokeTester.json");
        File.WriteAllText(outputPath, json, Encoding.UTF8);
        return stress.Passed ? 0 : 1;
    }

    private static string ResolveProjectRoot()
    {
        string current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "AGENTS.md")) && Directory.Exists(Path.Combine(current, "Assets")))
            return current;

        DirectoryInfo? directory = new DirectoryInfo(current);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return current;
    }

    private static StressResult RunStress(string projectRoot, string spaceEngineRoot, int passCount)
    {
        StressResult result = new StressResult
        {
            ProjectRoot = projectRoot,
            SpaceEngineRoot = spaceEngineRoot,
            PassCount = Math.Max(1, passCount)
        };

        AuditResult? baseline = null;
        AuditResult? latest = null;
        for (int i = 0; i < result.PassCount; i++)
        {
            latest = ExecuteAudit(projectRoot, spaceEngineRoot);
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

    private static AuditResult ExecuteAudit(string projectRoot, string spaceEngineRoot)
    {
        AuditResult result = new AuditResult
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

    private static void AuditReport(string projectRoot, AuditResult result)
    {
        string reportPath = Path.Combine(projectRoot, "Docs", "SPACE_ENGINE_RESEARCH", "ATMOSPHERE_AND_SCALE_099.md");
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

    private static void AuditReferenceKernels(string projectRoot, AuditResult result)
    {
        string folder = Path.Combine(projectRoot, "Docs", "SPACE_ENGINE_RESEARCH", "ReferenceKernels");
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

    private static void AuditEditorValidationFiles(string projectRoot, AuditResult result)
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

    private static void AuditArchives(string spaceEngineRoot, AuditResult result)
    {
        result.ShaderPak = ZipProbe.Probe(Path.Combine(spaceEngineRoot, "data", "shaders", "Shaders.pak"), ExpectedShaderEntries);
        result.AtmospherePak = ZipProbe.Probe(Path.Combine(spaceEngineRoot, "data", "models", "atmospheres", "Atmospheres.pak"), ExpectedAtmosphereEntries);
        result.CatalogPak = ZipProbe.Probe(Path.Combine(spaceEngineRoot, "data", "catalogs", "Catalogs.pak"), ExpectedCatalogEntries);

        if (!result.ShaderPak.Exists)
        {
            AddFailure(result, "Shader archive missing: " + result.ShaderPak.Path);
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
            AddFailure(result, "Atmosphere archive missing: " + result.AtmospherePak.Path);
        else if (result.AtmospherePak.EncryptedEntryCount != 0 || result.AtmospherePak.ExpectedMissingCount != 0)
            AddFailure(result, "Atmosphere archive probe failed.");

        if (!result.CatalogPak.Exists)
            AddFailure(result, "Catalog archive missing: " + result.CatalogPak.Path);
        else if (result.CatalogPak.EncryptedEntryCount != 0 || result.CatalogPak.ExpectedMissingCount != 0)
            AddFailure(result, "Catalog archive probe failed.");
    }

    private static void AuditRecentScope(string projectRoot, AuditResult result)
    {
        string folder = Path.Combine(projectRoot, "Docs", "SPACE_ENGINE_RESEARCH");
        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            if (!IsOwnedResearchAuditFile(folder, files[i]))
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
    }

    private static bool IsOwnedResearchAuditFile(string folder, string path)
    {
        string relative = path.Substring(folder.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
        return relative.StartsWith("ReferenceKernels/", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(relative, "ATMOSPHERE_AND_SCALE_099.md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(relative, "OMEGA_AUTONOMY_AUDIT.md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(relative, "SpaceEngineResearchSmokeTester.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasStableCounts(AuditResult baseline, AuditResult current)
    {
        return baseline.ReportLineCount == current.ReportLineCount &&
               baseline.ReferenceKernelFileCount == current.ReferenceKernelFileCount &&
               baseline.ShaderPak.EntryCount == current.ShaderPak.EntryCount &&
               baseline.ShaderPak.EncryptedEntryCount == current.ShaderPak.EncryptedEntryCount &&
               baseline.AtmospherePak.EncryptedEntryCount == current.AtmospherePak.EncryptedEntryCount &&
               baseline.CatalogPak.EncryptedEntryCount == current.CatalogPak.EncryptedEntryCount &&
               baseline.FailureCount == current.FailureCount;
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

    private static void AddFailure(AuditResult result, string failure)
    {
        result.FailureCount++;
        if (result.Failures.Count < 32)
            result.Failures.Add(failure);
    }

    private static void AddFailure(StressResult result, string failure)
    {
        result.FailureCount++;
        if (result.Failures.Count < 32)
            result.Failures.Add(failure);
    }

    private sealed class AuditResult
    {
        public readonly List<string> Failures = new List<string>(32);
        public string ProjectRoot = string.Empty;
        public string SpaceEngineRoot = string.Empty;
        public string ReportPath = string.Empty;
        public string ReferenceKernelFolder = string.Empty;
        public string NoPasswordProbeStatus = string.Empty;
        public bool Passed;
        public int ReportLineCount;
        public int MaxReportLineCount;
        public int ReferenceKernelFileCount;
        public int EditorValidationFileCount;
        public int MaxEditorValidationLineCount;
        public int NativeCollectionTokenCount;
        public int JobBarrierTokenCount;
        public int StaticInstanceTokenCount;
        public int HotPathStringTokenCount;
        public int FailureCount;
        public ZipProbeResult ShaderPak;
        public ZipProbeResult AtmospherePak;
        public ZipProbeResult CatalogPak;
    }

    private sealed class StressResult
    {
        public readonly List<string> Failures = new List<string>(32);
        public string ProjectRoot = string.Empty;
        public string SpaceEngineRoot = string.Empty;
        public bool Passed;
        public int PassCount;
        public int FailureCount;
        public AuditResult? FinalAudit;
    }

    private struct ZipProbeResult
    {
        public string Path;
        public string ParseError;
        public bool Exists;
        public int EntryCount;
        public int EncryptedEntryCount;
        public int CompressedEntryCount;
        public int StoredEntryCount;
        public int ExpectedEntryCount;
        public int ExpectedFoundCount;
        public int ExpectedMissingCount;
    }

    private static class ZipProbe
    {
        private const uint EndOfCentralDirectorySignature = 0x06054B50u;
        private const uint CentralDirectoryHeaderSignature = 0x02014B50u;
        private const int MaxEocdSearchBytes = 66000;

        public static ZipProbeResult Probe(string path, string[] expectedEntries)
        {
            ZipProbeResult result = new ZipProbeResult { Path = path, ExpectedEntryCount = expectedEntries.Length };
            if (!File.Exists(path))
                return result;

            result.Exists = true;
            using FileStream stream = File.OpenRead(path);
            int tailLength = (int)Math.Min(stream.Length, MaxEocdSearchBytes);
            byte[] tail = new byte[tailLength];
            stream.Seek(stream.Length - tailLength, SeekOrigin.Begin);
            ReadExact(stream, tail, tail.Length);

            int eocd = FindEndOfCentralDirectory(tail);
            if (eocd < 0)
            {
                result.ParseError = "EOCD_NOT_FOUND";
                return result;
            }

            uint centralDirectorySize = ReadUInt32(tail, eocd + 12);
            uint centralDirectoryOffset = ReadUInt32(tail, eocd + 16);
            if (centralDirectorySize == 0u || centralDirectoryOffset >= stream.Length || centralDirectorySize > int.MaxValue)
            {
                result.ParseError = "INVALID_CENTRAL_DIRECTORY";
                return result;
            }

            byte[] centralDirectory = new byte[(int)centralDirectorySize];
            stream.Seek(centralDirectoryOffset, SeekOrigin.Begin);
            ReadExact(stream, centralDirectory, centralDirectory.Length);
            ParseCentralDirectory(centralDirectory, expectedEntries, ref result);
            result.ExpectedMissingCount = expectedEntries.Length - result.ExpectedFoundCount;
            return result;
        }

        private static void ParseCentralDirectory(byte[] centralDirectory, string[] expectedEntries, ref ZipProbeResult result)
        {
            int offset = 0;
            while (offset + 46 <= centralDirectory.Length)
            {
                if (ReadUInt32(centralDirectory, offset) != CentralDirectoryHeaderSignature)
                {
                    result.ParseError = "CENTRAL_DIRECTORY_HEADER_MISMATCH";
                    return;
                }

                ushort flags = ReadUInt16(centralDirectory, offset + 8);
                ushort method = ReadUInt16(centralDirectory, offset + 10);
                ushort nameLength = ReadUInt16(centralDirectory, offset + 28);
                ushort extraLength = ReadUInt16(centralDirectory, offset + 30);
                ushort commentLength = ReadUInt16(centralDirectory, offset + 32);
                int nextOffset = offset + 46 + nameLength + extraLength + commentLength;
                if (nextOffset > centralDirectory.Length)
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

                offset = nextOffset;
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

        private static void ReadExact(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
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

    private static class JsonWriter
    {
        public static string WriteStress(StressResult result)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append('{');
            Prop(builder, "status", result.Passed ? "OMEGA_VERIFIED" : "FAILED");
            builder.Append(',');
            Prop(builder, "projectRoot", result.ProjectRoot);
            builder.Append(',');
            Prop(builder, "spaceEngineRoot", result.SpaceEngineRoot);
            builder.Append(',');
            Prop(builder, "passCount", result.PassCount);
            builder.Append(',');
            Prop(builder, "failureCount", result.FailureCount);
            builder.Append(',');
            ArrayProp(builder, "failures", result.Failures);
            builder.Append(',');
            builder.Append("\"finalAudit\":");
            Audit(builder, result.FinalAudit);
            builder.Append('}');
            return builder.ToString();
        }

        private static void Audit(StringBuilder builder, AuditResult? result)
        {
            if (result == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            Prop(builder, "status", result.Passed ? "PASS" : "FAIL");
            builder.Append(',');
            Prop(builder, "reportLineCount", result.ReportLineCount);
            builder.Append(',');
            Prop(builder, "maxReportLineCount", result.MaxReportLineCount);
            builder.Append(',');
            Prop(builder, "referenceKernelFileCount", result.ReferenceKernelFileCount);
            builder.Append(',');
            Prop(builder, "editorValidationFileCount", result.EditorValidationFileCount);
            builder.Append(',');
            Prop(builder, "maxEditorValidationLineCount", result.MaxEditorValidationLineCount);
            builder.Append(',');
            Prop(builder, "noPasswordProbeStatus", result.NoPasswordProbeStatus);
            builder.Append(',');
            Prop(builder, "nativeCollectionTokenCount", result.NativeCollectionTokenCount);
            builder.Append(',');
            Prop(builder, "jobBarrierTokenCount", result.JobBarrierTokenCount);
            builder.Append(',');
            Prop(builder, "staticInstanceTokenCount", result.StaticInstanceTokenCount);
            builder.Append(',');
            Prop(builder, "hotPathStringTokenCount", result.HotPathStringTokenCount);
            builder.Append(',');
            Prop(builder, "failureCount", result.FailureCount);
            builder.Append(',');
            ArrayProp(builder, "failures", result.Failures);
            builder.Append(',');
            builder.Append("\"shaderPak\":");
            Zip(builder, result.ShaderPak);
            builder.Append(',');
            builder.Append("\"atmospherePak\":");
            Zip(builder, result.AtmospherePak);
            builder.Append(',');
            builder.Append("\"catalogPak\":");
            Zip(builder, result.CatalogPak);
            builder.Append('}');
        }

        private static void Zip(StringBuilder builder, ZipProbeResult result)
        {
            builder.Append('{');
            Prop(builder, "exists", result.Exists);
            builder.Append(',');
            Prop(builder, "entryCount", result.EntryCount);
            builder.Append(',');
            Prop(builder, "encryptedEntryCount", result.EncryptedEntryCount);
            builder.Append(',');
            Prop(builder, "expectedFoundCount", result.ExpectedFoundCount);
            builder.Append(',');
            Prop(builder, "expectedMissingCount", result.ExpectedMissingCount);
            builder.Append(',');
            Prop(builder, "parseError", result.ParseError);
            builder.Append('}');
        }

        private static void Prop(StringBuilder builder, string name, string value)
        {
            builder.Append('"').Append(name).Append("\":\"");
            Esc(builder, value);
            builder.Append('"');
        }

        private static void Prop(StringBuilder builder, string name, int value)
        {
            builder.Append('"').Append(name).Append("\":").Append(value);
        }

        private static void Prop(StringBuilder builder, string name, bool value)
        {
            builder.Append('"').Append(name).Append("\":").Append(value ? "true" : "false");
        }

        private static void ArrayProp(StringBuilder builder, string name, List<string> values)
        {
            builder.Append('"').Append(name).Append("\":[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append('"');
                Esc(builder, values[i]);
                builder.Append('"');
            }

            builder.Append(']');
        }

        private static void Esc(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                    builder.Append("\\\\");
                else if (c == '"')
                    builder.Append("\\\"");
                else if (c == '\r')
                    builder.Append("\\r");
                else if (c == '\n')
                    builder.Append("\\n");
                else
                    builder.Append(c);
            }
        }
    }
}
