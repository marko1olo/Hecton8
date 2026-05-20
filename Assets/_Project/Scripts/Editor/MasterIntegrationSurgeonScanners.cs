#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor
{
    public struct MasterIntegrationFinding
    {
        public string Scanner;
        public string Path;
        public int Line;
        public string Rule;
        public string Detail;
        public byte Severity;
    }

    public sealed class MasterIntegrationScanResult
    {
        public readonly List<MasterIntegrationFinding> Findings = new List<MasterIntegrationFinding>(256);
        public int FilesScanned;
        public int CriticalCount;
        public int WarningCount;

        public void Add(string scanner, string path, int line, string rule, string detail, byte severity)
        {
            var finding = new MasterIntegrationFinding
            {
                Scanner = scanner,
                Path = path,
                Line = line,
                Rule = rule,
                Detail = detail,
                Severity = severity
            };
            Findings.Add(finding);
            if (severity >= 2)
                CriticalCount++;
            else
                WarningCount++;
        }
    }

    public static class AUP_Compliance_Scanner
    {
        private const string ScannerName = "AUP_Compliance_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/AUP Compliance Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_AUP_Compliance.json", result);
            LogResult(result);
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            string distanceToken = "Vector3" + ".Distance";
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex];
                if (path.EndsWith("HectonFloatingOrigin.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                string method = string.Empty;
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    method = MasterIntegrationSource.UpdateMethodContext(masked, method);
                    if (!MasterIntegrationSource.IsHotMethod(method))
                        continue;

                    if (masked.IndexOf(distanceToken, StringComparison.Ordinal) >= 0 ||
                        masked.IndexOf(".position", StringComparison.Ordinal) >= 0)
                    {
                        result.Add(
                            ScannerName,
                            MasterIntegrationSource.ToProjectPath(path),
                            i + 1,
                            "AUP_WORLD_SPACE_HOT_PATH",
                            "World-space transform/math access inside hot method; route through AUP snapshot or local-sector DTO.",
                            2);
                    }
                }
            }

            return result;
        }

        private static void LogResult(MasterIntegrationScanResult result)
        {
            Debug.Log("SHINOBU_140 AUP scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }
    }

    public static class Vault_Sovereignty_Scanner
    {
        private const string ScannerName = "Vault_Sovereignty_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Vault Sovereignty Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Vault_Sovereignty.json", result);
            Debug.Log("SHINOBU_140 Vault scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            string nativeToken = "new Native" + "Array";
            string disposableToken = "Allocator" + ".Persistent";
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex];
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    bool directNativeAllocation = masked.IndexOf(nativeToken, StringComparison.Ordinal) >= 0;
                    bool persistentAllocator = masked.IndexOf(disposableToken, StringComparison.Ordinal) >= 0;
                    if (!directNativeAllocation && !persistentAllocator)
                        continue;
                    if (masked.IndexOf("DataVault", StringComparison.Ordinal) >= 0 ||
                        masked.IndexOf("VaultBufferHandle", StringComparison.Ordinal) >= 0 ||
                        masked.IndexOf("NativeArrayOptions", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(path),
                        i + 1,
                        "DIRECT_NATIVE_ALLOCATION",
                        "Runtime native memory must be DataVault-owned or explicitly exempted.",
                        2);
                }
            }

            return result;
        }
    }

    public static class Compile_Wall_Scanner
    {
        private const string ScannerName = "Compile_Wall_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Compile Wall Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Compile_Wall.json", result);
            Debug.Log("SHINOBU_140 Compile wall scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string root = MasterIntegrationSource.ProjectRoot;
            string[] files = Directory.Exists(root)
                ? Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories)
                : Array.Empty<string>();
            result.FilesScanned = files.Length;
            for (int i = 0; i < files.Length; i++)
            {
                string text = MasterIntegrationSource.ReadAllTextSafe(files[i]);
                string name = MasterIntegrationSource.ExtractJsonString(text, "name");
                if (string.IsNullOrEmpty(name))
                    continue;

                bool coreAssembly = name.Equals("Hecton8.Core", StringComparison.Ordinal) ||
                                    name.StartsWith("Hecton8.Core.", StringComparison.Ordinal);
                if (!coreAssembly)
                    continue;

                string[] references = MasterIntegrationSource.ExtractJsonArrayStrings(text, "references");
                for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++)
                {
                    string reference = references[referenceIndex];
                    if (!IsForbiddenCoreReference(reference))
                        continue;

                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(files[i]),
                        1,
                        "CORE_RUNTIME_DOMAIN_EDGE",
                        name + " references " + reference + "; move through contracts or EventBus/DataVault surfaces.",
                        2);
                }

                if (text.IndexOf("\"Pack\": 1", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Pack=1", StringComparison.Ordinal) >= 0)
                {
                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(files[i]),
                        1,
                        "PACK1_RUNTIME_LAYOUT",
                        "Runtime assembly metadata contains Pack=1; ARM64 DTOs must use explicit 16/32/64-byte layouts.",
                        2);
                }
            }

            string[] runtimeFiles = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned += runtimeFiles.Length;
            for (int fileIndex = 0; fileIndex < runtimeFiles.Length; fileIndex++)
            {
                string path = runtimeFiles[fileIndex];
                if (!IsCoreRuntimeSource(path))
                    continue;

                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[lineIndex]);
                    string forbidden = ResolveForbiddenCoreNamespace(masked);
                    if (string.IsNullOrEmpty(forbidden))
                        continue;

                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(path),
                        lineIndex + 1,
                        "CORE_SOURCE_DOMAIN_EDGE",
                        "Core source references " + forbidden + "; mirror DTOs through contracts, EventBus, or DataVault handles.",
                        2);
                }
            }

            return result;
        }

        private static bool IsForbiddenCoreReference(string reference)
        {
            if (reference.IndexOf("Contracts", StringComparison.Ordinal) >= 0 ||
                reference.IndexOf("Memory", StringComparison.Ordinal) >= 0 ||
                reference.IndexOf("Scheduling", StringComparison.Ordinal) >= 0 ||
                reference.IndexOf("Bucketing", StringComparison.Ordinal) >= 0 ||
                reference.IndexOf("Time", StringComparison.Ordinal) >= 0 ||
                reference.IndexOf("Persistence", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return reference.StartsWith("Hecton8.World", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.AI", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Graphics", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Audio", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Gameplay", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Physics", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Networking", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Environment", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Inventory", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Narrative", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Power", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Quest", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.SaveSystem", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Visor", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.VFX", StringComparison.Ordinal) ||
                   reference.StartsWith("Hecton8.Systems", StringComparison.Ordinal);
        }

        private static bool IsCoreRuntimeSource(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Assets/_Project/Scripts/Core/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveForbiddenCoreNamespace(string masked)
        {
            if (masked.IndexOf("Hecton8.Networking", StringComparison.Ordinal) >= 0)
                return "Hecton8.Networking";
            if (masked.IndexOf("Hecton8.World", StringComparison.Ordinal) >= 0)
                return "Hecton8.World";
            if (masked.IndexOf("Hecton8.AI", StringComparison.Ordinal) >= 0)
                return "Hecton8.AI";
            if (masked.IndexOf("Hecton8.Systems.AI", StringComparison.Ordinal) >= 0)
                return "Hecton8.Systems.AI";
            if (masked.IndexOf("Hecton8.Graphics", StringComparison.Ordinal) >= 0)
                return "Hecton8.Graphics";
            if (masked.IndexOf("Hecton8.Audio", StringComparison.Ordinal) >= 0)
                return "Hecton8.Audio";
            if (masked.IndexOf("Hecton8.Gameplay", StringComparison.Ordinal) >= 0)
                return "Hecton8.Gameplay";
            if (masked.IndexOf("Hecton8.Physics", StringComparison.Ordinal) >= 0)
                return "Hecton8.Physics";
            if (masked.IndexOf("Hecton8.Atmosphere", StringComparison.Ordinal) >= 0)
                return "Hecton8.Atmosphere";
            if (masked.IndexOf("Hecton8.Celestial", StringComparison.Ordinal) >= 0)
                return "Hecton8.Celestial";
            if (masked.IndexOf("Hecton8.Construction", StringComparison.Ordinal) >= 0)
                return "Hecton8.Construction";
            if (masked.IndexOf("Hecton8.Environment", StringComparison.Ordinal) >= 0)
                return "Hecton8.Environment";
            if (masked.IndexOf("Hecton8.Inventory", StringComparison.Ordinal) >= 0)
                return "Hecton8.Inventory";
            if (masked.IndexOf("Hecton8.Narrative", StringComparison.Ordinal) >= 0)
                return "Hecton8.Narrative";
            if (masked.IndexOf("Hecton8.Optimization", StringComparison.Ordinal) >= 0)
                return "Hecton8.Optimization";
            if (masked.IndexOf("Hecton8.Power", StringComparison.Ordinal) >= 0)
                return "Hecton8.Power";
            if (masked.IndexOf("Hecton8.Quest", StringComparison.Ordinal) >= 0)
                return "Hecton8.Quest";
            if (masked.IndexOf("Hecton8.SaveSystem", StringComparison.Ordinal) >= 0)
                return "Hecton8.SaveSystem";
            if (masked.IndexOf("Hecton8.Visor", StringComparison.Ordinal) >= 0)
                return "Hecton8.Visor";
            if (masked.IndexOf("Hecton8.VFX", StringComparison.Ordinal) >= 0)
                return "Hecton8.VFX";
            return string.Empty;
        }
    }

    public static class Runtime_Struct_Layout_Scanner
    {
        private const string ScannerName = "Runtime_Struct_Layout_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Runtime Struct Layout Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Runtime_Struct_Layout.json", result);
            Debug.Log("SHINOBU_140 runtime struct layout scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex];
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                bool insideStruct = false;
                int structDepth = 0;
                bool structHasManagedReference = false;
                List<int> pendingPropertyLines = new List<int>(8);
                List<int> pendingBoolLines = new List<int>(4);
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    if (masked.IndexOf("StructLayout", StringComparison.Ordinal) >= 0 &&
                        masked.IndexOf("Pack", StringComparison.Ordinal) >= 0)
                    {
                        result.Add(
                            ScannerName,
                            MasterIntegrationSource.ToProjectPath(path),
                            i + 1,
                            "PACKED_RUNTIME_STRUCT",
                            "Runtime structs must not use packed layout; copy file records into aligned DTOs instead.",
                            2);
                    }

                    if (!insideStruct && IsStructDeclaration(masked))
                    {
                        insideStruct = true;
                        structDepth = 0;
                        structHasManagedReference = false;
                        pendingPropertyLines.Clear();
                        pendingBoolLines.Clear();
                    }

                    if (!insideStruct)
                        continue;

                    if (ContainsManagedReferenceField(masked))
                        structHasManagedReference = true;

                    if (LooksLikeAccessorProperty(masked))
                    {
                        pendingPropertyLines.Add(i + 1);
                    }

                    if (ContainsBoolField(masked))
                    {
                        pendingBoolLines.Add(i + 1);
                    }

                    structDepth += CountChar(masked, '{');
                    structDepth -= CountChar(masked, '}');
                    if (structDepth <= 0 && masked.IndexOf('}') >= 0)
                    {
                        if (!structHasManagedReference)
                        {
                            string projectPath = MasterIntegrationSource.ToProjectPath(path);
                            for (int pendingIndex = 0; pendingIndex < pendingPropertyLines.Count; pendingIndex++)
                            {
                                result.Add(
                                    ScannerName,
                                    projectPath,
                                    pendingPropertyLines[pendingIndex],
                                    "STRUCT_PROPERTY_DEFENSIVE_COPY_RISK",
                                    "Runtime structs in native/hot paths must expose raw fields, not C# properties.",
                                    2);
                            }

                            for (int pendingIndex = 0; pendingIndex < pendingBoolLines.Count; pendingIndex++)
                            {
                                result.Add(
                                    ScannerName,
                                    projectPath,
                                    pendingBoolLines[pendingIndex],
                                    "STRUCT_BOOL_FIELD_ARM64_RISK",
                                    "Runtime structs must use byte or bit flags instead of bool fields.",
                                    2);
                            }
                        }

                        insideStruct = false;
                        structHasManagedReference = false;
                        pendingPropertyLines.Clear();
                        pendingBoolLines.Clear();
                    }
                }
            }

            return result;
        }

        private static bool IsStructDeclaration(string masked)
        {
            return masked.IndexOf(" struct ", StringComparison.Ordinal) >= 0 ||
                   masked.TrimStart().StartsWith("struct ", StringComparison.Ordinal) ||
                   masked.IndexOf(" partial struct ", StringComparison.Ordinal) >= 0;
        }

        private static bool LooksLikeAccessorProperty(string masked)
        {
            int braceIndex = masked.IndexOf('{');
            if (braceIndex < 0)
                return false;

            return ContainsAccessorToken(masked, "get;") ||
                   ContainsAccessorToken(masked, "set;");
        }

        private static bool ContainsAccessorToken(string masked, string token)
        {
            int index = masked.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                char previous = index > 0 ? masked[index - 1] : ' ';
                if ((char.IsWhiteSpace(previous) || previous == '{' || previous == ';') &&
                    masked.LastIndexOf('{', index) >= 0)
                {
                    return true;
                }

                index = masked.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool ContainsBoolField(string masked)
        {
            int boolIndex = masked.IndexOf(" bool ", StringComparison.Ordinal);
            int semicolonIndex = masked.IndexOf(';');
            if (boolIndex < 0 ||
                semicolonIndex < 0 ||
                semicolonIndex < boolIndex)
            {
                return false;
            }

            if (masked.IndexOf("=>", StringComparison.Ordinal) >= 0 ||
                masked.IndexOf("get;", StringComparison.Ordinal) >= 0 ||
                masked.IndexOf("set;", StringComparison.Ordinal) >= 0 ||
                masked.IndexOf('(', 0, semicolonIndex) >= 0)
            {
                return false;
            }

            return masked.IndexOf("public ", StringComparison.Ordinal) >= 0 ||
                   masked.IndexOf("internal ", StringComparison.Ordinal) >= 0 ||
                   masked.IndexOf("private ", StringComparison.Ordinal) >= 0 ||
                   masked.IndexOf("protected ", StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsManagedReferenceField(string masked)
        {
            int semicolonIndex = masked.IndexOf(';');
            if (semicolonIndex < 0 ||
                masked.IndexOf('(', 0, semicolonIndex) >= 0)
            {
                return false;
            }

            if (masked.IndexOf("[]", StringComparison.Ordinal) >= 0 ||
                masked.IndexOf("List<", StringComparison.Ordinal) >= 0 ||
                masked.IndexOf("Dictionary<", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            string[] managedTokens =
            {
                "string",
                "object",
                "GameObject",
                "Mesh",
                "Material",
                "Light",
                "Texture",
                "Texture2D",
                "Sprite",
                "AudioClip",
                "AnimationCurve",
                "Transform",
                "Component",
                "Camera",
                "Collider",
                "Rigidbody",
                "MonoBehaviour",
                "ScriptableObject",
                "AssetReference",
                "TMP_Text"
            };

            for (int i = 0; i < managedTokens.Length; i++)
            {
                if (ContainsTypeToken(masked, managedTokens[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsTypeToken(string masked, string token)
        {
            int index = masked.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool leftOk = index == 0 || !IsIdentifierChar(masked[index - 1]);
                int end = index + token.Length;
                bool rightOk = end >= masked.Length || !IsIdentifierChar(masked[end]);
                if (leftOk && rightOk)
                    return true;

                index = masked.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static int CountChar(string text, char needle)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == needle)
                    count++;
            }

            return count;
        }
    }

    public static class Burst_Job_Directive_Scanner
    {
        private const string ScannerName = "Burst_Job_Directive_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Burst Job Directive Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Burst_Job_Directives.json", result);
            Debug.Log("SHINOBU_140 Burst job directive scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex];
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    if (!IsJobDeclaration(masked))
                        continue;

                    string attributeWindow = BuildAttributeWindow(lines, i);
                    if (attributeWindow.IndexOf("BurstCompile", StringComparison.Ordinal) < 0)
                    {
                        result.Add(
                            ScannerName,
                            MasterIntegrationSource.ToProjectPath(path),
                            i + 1,
                            "JOB_MISSING_BURSTCOMPILE",
                            "Job structs must be Burst compiled before they enter dispatcher phase scheduling.",
                            2);
                        continue;
                    }

                    bool hasSyncCompile = attributeWindow.IndexOf("CompileSynchronously", StringComparison.Ordinal) >= 0 &&
                                          attributeWindow.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool hasPrecision = attributeWindow.IndexOf("FloatPrecision", StringComparison.Ordinal) >= 0 &&
                                        attributeWindow.IndexOf("Standard", StringComparison.Ordinal) >= 0;
                    bool hasFastMode = attributeWindow.IndexOf("FloatMode", StringComparison.Ordinal) >= 0 &&
                                       attributeWindow.IndexOf("Fast", StringComparison.Ordinal) >= 0;
                    bool hasDeterministicMode = attributeWindow.IndexOf("FloatMode", StringComparison.Ordinal) >= 0 &&
                                                attributeWindow.IndexOf("Deterministic", StringComparison.Ordinal) >= 0;
                    bool deterministicPath = IsDeterministicBurstPath(path);
                    bool validFloatMode = deterministicPath ? hasDeterministicMode : hasFastMode;
                    if (!hasSyncCompile || !hasPrecision || !validFloatMode)
                    {
                        result.Add(
                            ScannerName,
                            MasterIntegrationSource.ToProjectPath(path),
                            i + 1,
                            "BURST_DIRECTIVE_FLAGS_INCOMPLETE",
                            "Burst jobs require CompileSynchronously=true, FloatPrecision.Standard, and the domain-correct FloatMode.",
                            2);
                    }
                }
            }

            return result;
        }

        private static bool IsJobDeclaration(string masked)
        {
            return masked.IndexOf(':') >= 0 &&
                   masked.IndexOf("IJob", StringComparison.Ordinal) >= 0 &&
                   (masked.IndexOf(" struct ", StringComparison.Ordinal) >= 0 ||
                    masked.IndexOf(" partial struct ", StringComparison.Ordinal) >= 0 ||
                    masked.TrimStart().StartsWith("struct ", StringComparison.Ordinal));
        }

        private static bool IsDeterministicBurstPath(string path)
        {
            return path.IndexOf("Net", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Rollback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Determinism", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Lockstep", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("MemorySentinel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Desync", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Aup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("VaultMemory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("SignalWarden", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildAttributeWindow(string[] lines, int declarationLine)
        {
            int start = Math.Max(0, declarationLine - 8);
            var builder = new StringBuilder(768);
            for (int i = start; i < declarationLine; i++)
            {
                builder.Append(MasterIntegrationSource.MaskCommentsAndStrings(lines[i]));
                builder.Append(' ');
            }

            return builder.ToString();
        }
    }

    public static class Dev_Virtualization_Scanner
    {
        private const string ScannerName = "Dev_Virtualization_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Dev Virtualization Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Dev_Virtualization.json", result);
            Debug.Log("SHINOBU_140 devirtualization scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            HashSet<string> interfaceNames = CollectInterfaceNames(files);
            result.FilesScanned = files.Length;
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(files[fileIndex]);
                string method = string.Empty;
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    method = MasterIntegrationSource.UpdateMethodContext(masked, method);
                    bool interfaceContainer = LooksLikeInterfaceArray(masked, interfaceNames) || LooksLikeInterfaceCollection(masked, interfaceNames);
                    if (!interfaceContainer)
                        continue;

                    byte severity = MasterIntegrationSource.IsHotMethod(method) ? (byte)2 : (byte)1;
                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(files[fileIndex]),
                        i + 1,
                        "INTERFACE_CONTAINER_DEVIRTUALIZATION_RISK",
                        "Arrays or collections of interfaces block Burst/IL2CPP devirtualization; use flat concrete arrays or generic unmanaged constraints.",
                        severity);
                }
            }

            return result;
        }

        private static HashSet<string> CollectInterfaceNames(string[] files)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(files[fileIndex]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    if (TryReadInterfaceName(masked, out string name))
                        names.Add(name);
                }
            }

            return names;
        }

        private static bool TryReadInterfaceName(string masked, out string name)
        {
            name = string.Empty;
            int index = masked.IndexOf("interface ", StringComparison.Ordinal);
            if (index < 0)
                return false;

            int start = index + "interface ".Length;
            while (start < masked.Length && char.IsWhiteSpace(masked[start]))
                start++;

            int end = start;
            while (end < masked.Length && (char.IsLetterOrDigit(masked[end]) || masked[end] == '_'))
                end++;

            if (end <= start)
                return false;

            string candidate = masked.Substring(start, end - start);
            if (candidate.Length < 2 || candidate[0] != 'I')
                return false;

            name = candidate;
            return true;
        }

        private static bool LooksLikeInterfaceArray(string masked, HashSet<string> interfaceNames)
        {
            if (masked.IndexOf("[]", StringComparison.Ordinal) < 0 ||
                masked.IndexOf("where ", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            int arrayIndex = masked.IndexOf("[]", StringComparison.Ordinal);
            while (arrayIndex >= 0)
            {
                int end = arrayIndex - 1;
                while (end >= 0 && char.IsWhiteSpace(masked[end]))
                    end--;

                int start = end;
                while (start >= 0 && (char.IsLetterOrDigit(masked[start]) || masked[start] == '_'))
                    start--;

                if (end >= 0)
                {
                    string candidate = masked.Substring(start + 1, end - start);
                    if (interfaceNames.Contains(candidate))
                        return true;
                }

                arrayIndex = masked.IndexOf("[]", arrayIndex + 2, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool LooksLikeInterfaceCollection(string masked, HashSet<string> interfaceNames)
        {
            if (masked.IndexOf("where ", StringComparison.Ordinal) >= 0)
                return false;

            return ContainsInterfaceGeneric(masked, "List<", interfaceNames) ||
                   ContainsInterfaceGeneric(masked, "IEnumerable<", interfaceNames) ||
                   ContainsInterfaceGeneric(masked, "IReadOnlyList<", interfaceNames) ||
                   ContainsInterfaceGeneric(masked, "NativeArray<", interfaceNames) ||
                   ContainsInterfaceGeneric(masked, "NativeList<", interfaceNames);
        }

        private static bool ContainsInterfaceGeneric(string masked, string token, HashSet<string> interfaceNames)
        {
            int index = masked.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                int start = index + token.Length;
                while (start < masked.Length && char.IsWhiteSpace(masked[start]))
                    start++;

                int end = start;
                while (end < masked.Length && (char.IsLetterOrDigit(masked[end]) || masked[end] == '_'))
                    end++;

                if (end > start)
                {
                    string candidate = masked.Substring(start, end - start);
                    if (interfaceNames.Contains(candidate))
                        return true;
                }

                index = masked.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return false;
        }
    }

    public static class Rollback_Fence_Compliance_Scanner
    {
        private const string ScannerName = "Rollback_Fence_Compliance_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Rollback Fence Compliance Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Rollback_Fence_Compliance.json", result);
            Debug.Log("SHINOBU_140 rollback fence compliance scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;

            string dispatcherPath = Path.Combine(MasterIntegrationSource.ProjectRoot, "Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            string dispatcherText = MasterIntegrationSource.ReadAllTextSafe(dispatcherPath);
            if (dispatcherText.IndexOf("TryFenceRollbackBeforeVisualSync", StringComparison.Ordinal) < 0 ||
                dispatcherText.IndexOf("_masterRollbackFenceThisFrame", StringComparison.Ordinal) < 0 ||
                dispatcherText.IndexOf("RunMasterVisualSyncPhase", StringComparison.Ordinal) < 0)
            {
                result.Add(
                    ScannerName,
                    "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
                    1,
                    "ROLLBACK_VISUAL_FENCE_MISSING",
                    "Dispatcher must read rollback state and skip VISUAL_SYNC on rollback/resimulation frames.",
                    2);
            }

            if (!ContainsToken(files, "RollbackAudioSuppressionDTO"))
            {
                result.Add(
                    ScannerName,
                    "Assets/_Project/Scripts",
                    1,
                    "ROLLBACK_AUDIO_SUPPRESSION_ROUTE_MISSING",
                    "Rollback catch-up must suppress audio presentation through an owned unmanaged route.",
                    2);
            }

            if (!ContainsToken(files, "HeadlessResimulationCommandJob"))
            {
                result.Add(
                    ScannerName,
                    "Assets/_Project/Scripts",
                    1,
                    "HEADLESS_RESIM_COMMAND_ROUTE_MISSING",
                    "Rollback catch-up requires a netcode-owned command route before dispatcher can loop simulation safely.",
                    2);
            }

            if (!ContainsToken(files, "MockTickCommand"))
            {
                result.Add(
                    ScannerName,
                    "Assets/_Project/Scripts",
                    1,
                    "MOCK_TICK_COMMAND_ROUTE_MISSING",
                    "Fallback deterministic resimulation command DTO is absent.",
                    2);
            }

            if (!ContainsRollbackParticleSuppression(files))
            {
                result.Add(
                    ScannerName,
                    "Assets/_Project/Scripts",
                    1,
                    "ROLLBACK_PARTICLE_SUPPRESSION_ROUTE_ABSENT",
                    "Particle suppression has no proven owner route; do not invent a global lane without route-card review.",
                    1);
            }

            return result;
        }

        private static bool ContainsToken(string[] files, string token)
        {
            for (int i = 0; i < files.Length; i++)
            {
                string text = MasterIntegrationSource.ReadAllTextSafe(files[i]);
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsRollbackParticleSuppression(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                string text = MasterIntegrationSource.ReadAllTextSafe(files[i]);
                if (text.IndexOf("Rollback", StringComparison.Ordinal) < 0)
                    continue;
                if (text.IndexOf("Particle", StringComparison.Ordinal) >= 0 &&
                    (text.IndexOf("Suppress", StringComparison.Ordinal) >= 0 ||
                     text.IndexOf("Mute", StringComparison.Ordinal) >= 0 ||
                     text.IndexOf("DisableEmission", StringComparison.Ordinal) >= 0))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class Hot_Registry_Polling_Scanner
    {
        private const string ScannerName = "Hot_Registry_Polling_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Hot Registry Polling Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Hot_Registry_Polling.json", result);
            Debug.Log("SHINOBU_140 hot registry scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            string registryToken = "Global" + "Registry" + ".";
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(files[fileIndex]);
                string method = string.Empty;
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    method = MasterIntegrationSource.UpdateMethodContext(masked, method);
                    if (!MasterIntegrationSource.IsHotMethod(method))
                        continue;
                    if (masked.IndexOf(registryToken, StringComparison.Ordinal) < 0)
                        continue;
                    if (masked.IndexOf("Register", StringComparison.Ordinal) >= 0 ||
                        masked.IndexOf("Unregister", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(files[fileIndex]),
                        i + 1,
                        "HOT_REGISTRY_POLL",
                        "Global authority lookup in hot method; cache at boot or consume dispatcher snapshot.",
                        2);
                }
            }

            return result;
        }
    }

    public static class Mid_Frame_Complete_Scanner
    {
        private const string ScannerName = "Mid_Frame_Complete_Scanner";

        [MenuItem("Hecton8/Audit/SHINOBU 140/Mid Frame Complete Scanner")]
        public static void RunFromMenu()
        {
            MasterIntegrationScanResult result = RunScan();
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Mid_Frame_Complete.json", result);
            Debug.Log("SHINOBU_140 mid-frame complete scan: " + result.CriticalCount.ToString(CultureInfo.InvariantCulture) + " critical findings.");
        }

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            string completeToken = "." + "Complete(";
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(files[fileIndex]);
                string method = string.Empty;
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    method = MasterIntegrationSource.UpdateMethodContext(masked, method);
                    if (masked.IndexOf(completeToken, StringComparison.Ordinal) < 0)
                        continue;
                    if (masked.IndexOf("[BLOCKING_SYNC_POINT]", StringComparison.Ordinal) >= 0)
                        continue;
                    if (!MasterIntegrationSource.IsMidFrameMethod(method))
                        continue;

                    result.Add(
                        ScannerName,
                        MasterIntegrationSource.ToProjectPath(files[fileIndex]),
                        i + 1,
                        "MID_FRAME_JOB_COMPLETE",
                        "Job completion must be a named phase fence or deferred post-sim readback.",
                        2);
                }
            }

            return result;
        }
    }

    public static class Signal_Bus_Topology_Scanner
    {
        private const string ScannerName = "Signal_Bus_Topology_Scanner";

        public static MasterIntegrationScanResult RunScan()
        {
            MasterIntegrationScanResult result = new MasterIntegrationScanResult();
            string[] files = MasterIntegrationSource.EnumerateRuntimeCsFiles();
            result.FilesScanned = files.Length;
            string flushToken = "FlushPre" + "Simulation";
            string clearToken = "ClearPost" + "SimulationSnapshots";
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex];
                string[] lines = MasterIntegrationSource.ReadAllLinesSafe(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string masked = MasterIntegrationSource.MaskCommentsAndStrings(lines[i]);
                    if (masked.IndexOf(flushToken, StringComparison.Ordinal) >= 0 &&
                        !path.EndsWith("SystemDispatcher.cs", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith("GlobalSignals.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(ScannerName, MasterIntegrationSource.ToProjectPath(path), i + 1, "SIGNAL_FLUSH_OUTSIDE_DISPATCHER", "Signal lanes may only flush through dispatcher topology.", 2);
                    }
                    if (masked.IndexOf(clearToken, StringComparison.Ordinal) >= 0 &&
                        !path.EndsWith("SystemDispatcher.cs", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith("GlobalSignals.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(ScannerName, MasterIntegrationSource.ToProjectPath(path), i + 1, "SIGNAL_CLEAR_OUTSIDE_DISPATCHER", "Post-simulation signal snapshot clear is dispatcher-owned.", 2);
                    }
                }
            }

            return result;
        }
    }

    public static class H_Phi_Metric_Aggregator
    {
        [MenuItem("Hecton8/Audit/SHINOBU 140/H-Phi Metric Aggregator")]
        public static void RunFromMenu()
        {
            RunAndWriteFinalScore();
        }

        public static float RunAndWriteFinalScore()
        {
            MasterIntegrationScanResult aup = AUP_Compliance_Scanner.RunScan();
            MasterIntegrationScanResult vault = Vault_Sovereignty_Scanner.RunScan();
            MasterIntegrationScanResult compileWall = Compile_Wall_Scanner.RunScan();
            MasterIntegrationScanResult structLayout = Runtime_Struct_Layout_Scanner.RunScan();
            MasterIntegrationScanResult burst = Burst_Job_Directive_Scanner.RunScan();
            MasterIntegrationScanResult devirtualization = Dev_Virtualization_Scanner.RunScan();
            MasterIntegrationScanResult rollback = Rollback_Fence_Compliance_Scanner.RunScan();
            MasterIntegrationScanResult registry = Hot_Registry_Polling_Scanner.RunScan();
            MasterIntegrationScanResult complete = Mid_Frame_Complete_Scanner.RunScan();
            MasterIntegrationScanResult signal = Signal_Bus_Topology_Scanner.RunScan();

            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_AUP_Compliance.json", aup);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Vault_Sovereignty.json", vault);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Compile_Wall.json", compileWall);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Runtime_Struct_Layout.json", structLayout);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Burst_Job_Directives.json", burst);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Dev_Virtualization.json", devirtualization);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Rollback_Fence_Compliance.json", rollback);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Hot_Registry_Polling.json", registry);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Mid_Frame_Complete.json", complete);
            MasterIntegrationReportWriter.WriteJson("Docs/Reports/SHINOBU_140_Signal_Bus_Topology.json", signal);

            float dataSovereignty = ScoreFromCriticals(vault.CriticalCount);
            float cacheAlignment = (DispatcherTimingLayoutGuard.ValidateLayout() ? 1f : 0f) * ScoreFromCriticals(structLayout.CriticalCount);
            float compileIsolation = ScoreFromCriticals(compileWall.CriticalCount);
            float hotPathDiscipline = ScoreFromCriticals(
                registry.CriticalCount +
                complete.CriticalCount +
                aup.CriticalCount +
                burst.CriticalCount +
                devirtualization.CriticalCount +
                rollback.CriticalCount);
            float signalTopology = ScoreFromCriticals(signal.CriticalCount);
            float hPhi = math.pow(
                math.max(0.000001f, dataSovereignty * cacheAlignment * compileIsolation * hotPathDiscipline * signalTopology),
                0.2f);

            MasterIntegrationReportWriter.WriteHPhi(
                "Docs/Reports/HECTON_PHI_SCORE_FINAL.json",
                hPhi,
                dataSovereignty,
                cacheAlignment,
                compileIsolation,
                hotPathDiscipline,
                signalTopology,
                aup,
                vault,
                compileWall,
                structLayout,
                burst,
                devirtualization,
                rollback,
                registry,
                complete,
                signal);
            Debug.Log("SHINOBU_140 H-Phi static score: " + hPhi.ToString("0.000000", CultureInfo.InvariantCulture));
            return hPhi;
        }

        private static float ScoreFromCriticals(int criticals)
        {
            return 1f / (1f + math.max(0, criticals));
        }
    }

    internal static class MasterIntegrationReportWriter
    {
        public static void WriteJson(string path, MasterIntegrationScanResult result)
        {
            EnsureDirectory(path);
            var builder = new StringBuilder(16384);
            builder.Append("{\n");
            AppendJsonPair(builder, "scanner", Path.GetFileNameWithoutExtension(path), true);
            AppendJsonPair(builder, "filesScanned", result.FilesScanned, true);
            AppendJsonPair(builder, "criticalCount", result.CriticalCount, true);
            AppendJsonPair(builder, "warningCount", result.WarningCount, true);
            builder.Append("  \"findings\": [\n");
            for (int i = 0; i < result.Findings.Count; i++)
            {
                MasterIntegrationFinding finding = result.Findings[i];
                builder.Append("    {");
                AppendJsonInline(builder, "scanner", finding.Scanner, true);
                AppendJsonInline(builder, "path", finding.Path, true);
                AppendJsonInline(builder, "line", finding.Line, true);
                AppendJsonInline(builder, "rule", finding.Rule, true);
                AppendJsonInline(builder, "detail", finding.Detail, true);
                AppendJsonInline(builder, "severity", finding.Severity, false);
                builder.Append("}");
                if (i + 1 < result.Findings.Count)
                    builder.Append(",");
                builder.Append("\n");
            }
            builder.Append("  ]\n");
            builder.Append("}\n");
            File.WriteAllText(Path.Combine(MasterIntegrationSource.ProjectRoot, path), builder.ToString());
        }

        public static void WriteHPhi(
            string path,
            float hPhi,
            float dataSovereignty,
            float cacheAlignment,
            float compileIsolation,
            float hotPathDiscipline,
            float signalTopology,
            MasterIntegrationScanResult aup,
            MasterIntegrationScanResult vault,
            MasterIntegrationScanResult compileWall,
            MasterIntegrationScanResult structLayout,
            MasterIntegrationScanResult burst,
            MasterIntegrationScanResult devirtualization,
            MasterIntegrationScanResult rollback,
            MasterIntegrationScanResult registry,
            MasterIntegrationScanResult complete,
            MasterIntegrationScanResult signal)
        {
            EnsureDirectory(path);
            var builder = new StringBuilder(4096);
            builder.Append("{\n");
            AppendJsonPair(builder, "agent", "SHINOBU_140", true);
            AppendJsonPair(builder, "model", "static_architecture_geometric_mean", true);
            AppendJsonPair(builder, "hPhi", hPhi, true);
            AppendJsonPair(builder, "dataSovereignty", dataSovereignty, true);
            AppendJsonPair(builder, "cacheAlignment", cacheAlignment, true);
            AppendJsonPair(builder, "compileWallIsolation", compileIsolation, true);
            AppendJsonPair(builder, "hotPathDiscipline", hotPathDiscipline, true);
            AppendJsonPair(builder, "signalTopology", signalTopology, true);
            builder.Append("  \"criticalCounts\": {");
            AppendJsonInline(builder, "aup", aup.CriticalCount, true);
            AppendJsonInline(builder, "vault", vault.CriticalCount, true);
            AppendJsonInline(builder, "compileWall", compileWall.CriticalCount, true);
            AppendJsonInline(builder, "runtimeStructLayout", structLayout.CriticalCount, true);
            AppendJsonInline(builder, "burstJobDirectives", burst.CriticalCount, true);
            AppendJsonInline(builder, "devirtualization", devirtualization.CriticalCount, true);
            AppendJsonInline(builder, "rollbackFence", rollback.CriticalCount, true);
            AppendJsonInline(builder, "hotRegistry", registry.CriticalCount, true);
            AppendJsonInline(builder, "midFrameComplete", complete.CriticalCount, true);
            AppendJsonInline(builder, "signalBus", signal.CriticalCount, false);
            builder.Append("}\n");
            builder.Append("}\n");
            File.WriteAllText(Path.Combine(MasterIntegrationSource.ProjectRoot, path), builder.ToString());
        }

        private static void EnsureDirectory(string path)
        {
            string fullPath = Path.Combine(MasterIntegrationSource.ProjectRoot, path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static void AppendJsonPair(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"");
            AppendEscaped(builder, key);
            builder.Append("\": \"");
            AppendEscaped(builder, value);
            builder.Append("\"");
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonPair(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"");
            AppendEscaped(builder, key);
            builder.Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonPair(StringBuilder builder, string key, float value, bool comma)
        {
            builder.Append("  \"");
            AppendEscaped(builder, key);
            builder.Append("\": ");
            builder.Append(value.ToString("0.000000000", CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJsonInline(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("\"");
            AppendEscaped(builder, key);
            builder.Append("\":\"");
            AppendEscaped(builder, value);
            builder.Append("\"");
            if (comma)
                builder.Append(",");
        }

        private static void AppendJsonInline(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("\"");
            AppendEscaped(builder, key);
            builder.Append("\":");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(",");
        }

        private static void AppendEscaped(StringBuilder builder, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                if (c == '\r')
                    continue;
                if (c == '\n')
                {
                    builder.Append("\\n");
                    continue;
                }

                builder.Append(c);
            }
        }
    }

    internal static class MasterIntegrationSource
    {
        public static string ProjectRoot
        {
            get
            {
                string dataPath = Application.dataPath.Replace('\\', '/');
                return Path.GetFullPath(Path.Combine(dataPath, ".."));
            }
        }

        public static string[] EnumerateRuntimeCsFiles()
        {
            string root = Path.Combine(ProjectRoot, "Assets/_Project/Scripts");
            if (!Directory.Exists(root))
                return Array.Empty<string>();

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var runtimeFiles = new List<string>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf("/Generated/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                runtimeFiles.Add(files[i]);
            }

            return runtimeFiles.ToArray();
        }

        public static string[] ReadAllLinesSafe(string path)
        {
            try
            {
                return File.ReadAllLines(path);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        public static string ReadAllTextSafe(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        public static string ToProjectPath(string path)
        {
            string fullRoot = Path.GetFullPath(ProjectRoot).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length + 1);
            return fullPath;
        }

        public static string UpdateMethodContext(string maskedLine, string currentMethod)
        {
            int paren = maskedLine.IndexOf('(');
            if (paren <= 0 || maskedLine.IndexOf(';') >= 0)
                return currentMethod;

            string beforeParen = maskedLine.Substring(0, paren).Trim();
            if (beforeParen.Length == 0)
                return currentMethod;

            int lastSpace = beforeParen.LastIndexOf(' ');
            string candidate = lastSpace >= 0 ? beforeParen.Substring(lastSpace + 1) : beforeParen;
            if (candidate.Length == 0)
                return currentMethod;
            if (candidate == "if" || candidate == "for" || candidate == "while" || candidate == "switch" || candidate == "catch" || candidate == "using")
                return currentMethod;

            return candidate;
        }

        public static bool IsHotMethod(string method)
        {
            return method == "Tick" ||
                   method == "FixedTick" ||
                   method == "Update" ||
                   method == "FixedUpdate" ||
                   method == "LateUpdate" ||
                   method == "PreSimulationTick" ||
                   method == "ScheduleSimulation" ||
                   method == "PostSimulationTick" ||
                   method == "VisualSyncTick" ||
                   method == "LateFrameTick" ||
                   method == "Execute";
        }

        public static bool IsMidFrameMethod(string method)
        {
            return method == "Tick" ||
                   method == "FixedTick" ||
                   method == "Update" ||
                   method == "FixedUpdate" ||
                   method == "PreSimulationTick" ||
                   method == "ScheduleSimulation" ||
                   method == "Execute";
        }

        public static string MaskCommentsAndStrings(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            var builder = new StringBuilder(line.Length);
            bool inString = false;
            char stringChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    break;
                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                    builder.Append(' ');
                    continue;
                }
                if (inString)
                {
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        i++;
                        builder.Append(' ');
                        continue;
                    }
                    if (c == stringChar)
                        inString = false;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        public static string ExtractJsonString(string json, string propertyName)
        {
            string token = "\"" + propertyName + "\"";
            int index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;
            int colon = json.IndexOf(':', index);
            if (colon < 0)
                return string.Empty;
            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0)
                return string.Empty;
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0)
                return string.Empty;
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        public static string[] ExtractJsonArrayStrings(string json, string propertyName)
        {
            string token = "\"" + propertyName + "\"";
            int index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return Array.Empty<string>();
            int open = json.IndexOf('[', index);
            int close = json.IndexOf(']', open + 1);
            if (open < 0 || close < 0)
                return Array.Empty<string>();

            var values = new List<string>(16);
            int cursor = open + 1;
            while (cursor < close)
            {
                int firstQuote = json.IndexOf('"', cursor);
                if (firstQuote < 0 || firstQuote >= close)
                    break;
                int secondQuote = json.IndexOf('"', firstQuote + 1);
                if (secondQuote < 0 || secondQuote > close)
                    break;
                values.Add(json.Substring(firstQuote + 1, secondQuote - firstQuote - 1));
                cursor = secondQuote + 1;
            }

            return values.ToArray();
        }
    }
}
#endif
