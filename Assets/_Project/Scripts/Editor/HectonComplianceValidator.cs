#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor compliance gate for HECTON-8 assembly reloads and CI batch compiles.
    /// </summary>
    [InitializeOnLoad]
    internal static class HectonComplianceValidator
    {
        private const string SourceRoot = "Assets/_Project/Scripts";
        private const string CoreAsmdefPath = "Assets/_Project/Scripts/Hecton8.Core.asmdef";
        private const string EnforceEnvironmentVariable = "HECTON_COMPLIANCE_ENFORCE";
        private const int MaxReportedViolations = 128;
        private const string ComplianceFailureMessage =
            "[HectonComplianceValidator] Compliance gate failed. CI must reject this compilation until violations are removed.";
        private const string BurstContractMessage =
            "Burst job contract violation. All IJob structs require [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)].";
        private static readonly string[] ForbiddenCoreReferences =
        {
            "UnityEngine.UI",
            "Unity.TextMeshPro",
            "Crest",
            "WaveHarmonic.Crest",
            "WaveHarmonic.Crest.Shared",
            "MapMagic",
            "Den.Tools",
            "GPUInstancer",
            "VolumetricLightBeam"
        };

        static HectonComplianceValidator()
        {
            EditorApplication.delayCall -= ValidateAfterAssemblyReload;
            EditorApplication.delayCall += ValidateAfterAssemblyReload;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall -= ValidateAfterAssemblyReload;
            EditorApplication.delayCall += ValidateAfterAssemblyReload;
        }

        [MenuItem("Hecton-8/Compliance/Validate Burst Contracts")]
        private static void ValidateBurstFromMenu()
        {
            ComplianceReport report = new ComplianceReport();
            ValidateBurstContracts(report);
            FailIfRequired(report, throwOnFailure: true, reportToConsole: true);
        }

        [MenuItem("Hecton-8/Compliance/Validate CI Gates")]
        private static void ValidateAllFromMenu()
        {
            ValidateAllContracts(throwOnFailure: true, reportToConsole: true);
        }

        private static void ValidateAfterAssemblyReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= ValidateAfterAssemblyReload;
                EditorApplication.delayCall += ValidateAfterAssemblyReload;
                return;
            }

            bool enforce = ShouldEnforceAsBuildGate();
            ValidateAllContracts(throwOnFailure: enforce, reportToConsole: enforce);
        }

        private static void ValidateAllContracts(bool throwOnFailure, bool reportToConsole)
        {
            ComplianceReport report = new ComplianceReport();
            ValidateBurstContracts(report);
            ValidateLayerMaskNameToLayerUsage(report);
            ValidateGameplayLinqUsage(report);
            ValidateCoreAsmdefAcl(report);
            FailIfRequired(report, throwOnFailure, reportToConsole);
        }

        private static void ValidateBurstContracts(ComplianceReport report)
        {
            int violationCount = 0;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Assembly assembly = assemblies[assemblyIndex];
                if (!ShouldScanAssembly(assembly))
                    continue;

                Type[] types = GetTypesSafe(assembly);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null || !type.IsValueType || type.IsEnum || !ImplementsUnityJob(type))
                        continue;

                    if (HasRequiredBurstCompileContract(type))
                        continue;

                    violationCount++;
                    report.Add("BURST001", type.FullName, 0, BurstContractMessage);
                }
            }

            SessionState.SetInt("HectonComplianceValidator.BurstContractViolations", violationCount);
        }

        private static void ValidateLayerMaskNameToLayerUsage(ComplianceReport report)
        {
            int violationCount = 0;
            string[] paths = GetRuntimeScriptPaths();
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex];
                string[] lines = ReadAllLinesSafe(path);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string codeLine = StripLineComment(lines[lineIndex]);
                    if (codeLine.IndexOf("LayerMask.NameToLayer", StringComparison.Ordinal) < 0)
                        continue;

                    string methodName = ResolveContainingMethodName(lines, lineIndex);
                    if (IsAllowedLayerInitializer(methodName))
                        continue;

                    violationCount++;
                    report.Add(
                        "LAYER001",
                        path,
                        lineIndex + 1,
                        "LayerMask.NameToLayer is only allowed in Awake or explicit initialization/cache methods.");
                }
            }

            SessionState.SetInt("HectonComplianceValidator.LayerMaskViolations", violationCount);
        }

        private static void ValidateGameplayLinqUsage(ComplianceReport report)
        {
            int violationCount = 0;
            string[] paths = GetRuntimeScriptPaths();
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex];
                string text = ReadAllTextSafe(path);
                if (text.Length == 0)
                    continue;

                if (text.IndexOf("namespace Hecton8.Gameplay", StringComparison.Ordinal) < 0)
                    continue;

                int usingIndex = text.IndexOf("using System.Linq;", StringComparison.Ordinal);
                if (usingIndex < 0)
                    continue;

                violationCount++;
                report.Add(
                    "LINQ001",
                    path,
                    GetLineNumber(text, usingIndex),
                    "System.Linq is forbidden in Hecton8.Gameplay runtime code.");
            }

            SessionState.SetInt("HectonComplianceValidator.GameplayLinqViolations", violationCount);
        }

        private static void ValidateCoreAsmdefAcl(ComplianceReport report)
        {
            int violationCount = 0;
            string text = ReadAllTextSafe(CoreAsmdefPath);
            if (text.Length == 0)
            {
                report.Add("ASMDEF000", CoreAsmdefPath, 0, "Core asmdef is missing or unreadable.");
                SessionState.SetInt("HectonComplianceValidator.CoreAsmdefViolations", 1);
                return;
            }

            for (int index = 0; index < ForbiddenCoreReferences.Length; index++)
            {
                string reference = ForbiddenCoreReferences[index];
                if (text.IndexOf("\"" + reference + "\"", StringComparison.Ordinal) < 0)
                    continue;

                violationCount++;
                report.Add(
                    "ASMDEF001",
                    CoreAsmdefPath,
                    0,
                    "Hecton8.Core must not reference " + reference + ". Move package-bound code into a bridge assembly.");
            }

            SessionState.SetInt("HectonComplianceValidator.CoreAsmdefViolations", violationCount);
        }

        private static void FailIfRequired(ComplianceReport report, bool throwOnFailure, bool reportToConsole)
        {
            SessionState.SetInt("HectonComplianceValidator.TotalViolations", report.Count);
            if (report.Count == 0)
                return;

            if (!reportToConsole && !throwOnFailure)
                return;

            string message = report.BuildMessage();
            UnityEngine.Debug.LogError(message);
            if (!throwOnFailure)
                return;

            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(1);

            throw new BuildFailedException(message);
        }

        private static bool ShouldScanAssembly(Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();
            string name = assemblyName.Name;
            return name.StartsWith("Hecton8", StringComparison.Ordinal) ||
                   name == "Assembly-CSharp";
        }

        private static bool ShouldEnforceAsBuildGate()
        {
            return UnityEngine.Application.isBatchMode ||
                   string.Equals(global::System.Environment.GetEnvironmentVariable(EnforceEnvironmentVariable), "1", StringComparison.Ordinal);
        }

        private static string[] GetRuntimeScriptPaths()
        {
            if (!Directory.Exists(SourceRoot))
                return Array.Empty<string>();

            string[] paths = Directory.GetFiles(SourceRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            int runtimeCount = 0;
            for (int index = 0; index < paths.Length; index++)
            {
                if (IsRuntimeScriptPath(paths[index]))
                    runtimeCount++;
            }

            if (runtimeCount == paths.Length)
                return paths;

            string[] runtimePaths = new string[runtimeCount];
            int writeIndex = 0;
            for (int index = 0; index < paths.Length; index++)
            {
                if (!IsRuntimeScriptPath(paths[index]))
                    continue;

                runtimePaths[writeIndex] = paths[index];
                writeIndex++;
            }

            return runtimePaths;
        }

        private static bool IsRuntimeScriptPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(SourceRoot + "/", StringComparison.Ordinal) &&
                   normalized.IndexOf("/Editor/", StringComparison.Ordinal) < 0;
        }

        private static string[] ReadAllLinesSafe(string path)
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

        private static string ReadAllTextSafe(string path)
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

        private static string StripLineComment(string line)
        {
            int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        private static string ResolveContainingMethodName(string[] lines, int hitLineIndex)
        {
            int searchStart = Math.Max(0, hitLineIndex - 96);
            for (int index = hitLineIndex; index >= searchStart; index--)
            {
                string line = StripLineComment(lines[index]).Trim();
                if (line.Length == 0 || line[0] == '[')
                    continue;

                int openParen = line.IndexOf('(');
                if (openParen <= 0)
                    continue;

                if (IsControlStatement(line))
                    continue;

                string beforeParen = line.Substring(0, openParen).TrimEnd();
                int lastSpace = beforeParen.LastIndexOf(' ');
                string candidate = lastSpace >= 0 ? beforeParen.Substring(lastSpace + 1) : beforeParen;
                candidate = candidate.Trim();
                if (IsIdentifier(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        private static bool IsControlStatement(string line)
        {
            return line.StartsWith("if ", StringComparison.Ordinal) ||
                   line.StartsWith("if(", StringComparison.Ordinal) ||
                   line.StartsWith("for ", StringComparison.Ordinal) ||
                   line.StartsWith("for(", StringComparison.Ordinal) ||
                   line.StartsWith("foreach ", StringComparison.Ordinal) ||
                   line.StartsWith("foreach(", StringComparison.Ordinal) ||
                   line.StartsWith("while ", StringComparison.Ordinal) ||
                   line.StartsWith("while(", StringComparison.Ordinal) ||
                   line.StartsWith("switch ", StringComparison.Ordinal) ||
                   line.StartsWith("switch(", StringComparison.Ordinal) ||
                   line.StartsWith("catch ", StringComparison.Ordinal) ||
                   line.StartsWith("catch(", StringComparison.Ordinal) ||
                   line.StartsWith("using ", StringComparison.Ordinal) ||
                   line.StartsWith("using(", StringComparison.Ordinal);
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (int index = 1; index < value.Length; index++)
            {
                char c = value[index];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        private static bool IsAllowedLayerInitializer(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return false;

            return methodName == "Awake" ||
                   methodName.StartsWith("Initialize", StringComparison.Ordinal) ||
                   methodName.StartsWith("Initialise", StringComparison.Ordinal) ||
                   methodName.StartsWith("Ensure", StringComparison.Ordinal) ||
                   methodName.StartsWith("Cache", StringComparison.Ordinal) ||
                   methodName.StartsWith("Bootstrap", StringComparison.Ordinal) ||
                   methodName.StartsWith("ResetStaticState", StringComparison.Ordinal);
        }

        private static int GetLineNumber(string text, int index)
        {
            int line = 1;
            for (int charIndex = 0; charIndex < index && charIndex < text.Length; charIndex++)
            {
                if (text[charIndex] == '\n')
                    line++;
            }

            return line;
        }

        private static Type[] GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static bool ImplementsUnityJob(Type type)
        {
            Type[] interfaces = type.GetInterfaces();
            for (int interfaceIndex = 0; interfaceIndex < interfaces.Length; interfaceIndex++)
            {
                string name = interfaces[interfaceIndex].Name;
                if (name == "IJob" ||
                    name == "IJobParallelFor" ||
                    name == "IJobParallelForTransform" ||
                    name == "IJobChunk" ||
                    name == "IJobEntity")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRequiredBurstCompileContract(Type type)
        {
            object[] attributes = type.GetCustomAttributes(inherit: false);
            for (int attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                object attribute = attributes[attributeIndex];
                Type attributeType = attribute.GetType();
                if (attributeType.FullName != "Unity.Burst.BurstCompileAttribute")
                    continue;

                object floatMode = attributeType.GetProperty("FloatMode")?.GetValue(attribute);
                object floatPrecision = attributeType.GetProperty("FloatPrecision")?.GetValue(attribute);
                return string.Equals(floatMode?.ToString(), "Fast", StringComparison.Ordinal) &&
                       string.Equals(floatPrecision?.ToString(), "Standard", StringComparison.Ordinal);
            }

            return false;
        }

        private sealed class ComplianceReport
        {
            private readonly StringBuilder _builder = new StringBuilder(8192);
            private int _hiddenViolationCount;

            public int Count { get; private set; }

            public void Add(string rule, string target, int line, string message)
            {
                Count++;
                if (Count > MaxReportedViolations)
                {
                    _hiddenViolationCount++;
                    return;
                }

                _builder.Append(" - ");
                _builder.Append(rule);
                _builder.Append(": ");
                _builder.Append(target);
                if (line > 0)
                {
                    _builder.Append(':');
                    _builder.Append(line);
                }

                _builder.Append(" - ");
                _builder.Append(message);
                _builder.AppendLine();
            }

            public string BuildMessage()
            {
                StringBuilder message = new StringBuilder(_builder.Length + 256);
                message.Append(ComplianceFailureMessage);
                message.AppendLine();
                message.Append(_builder);
                if (_hiddenViolationCount > 0)
                {
                    message.Append("... additional violations: ");
                    message.Append(_hiddenViolationCount);
                    message.AppendLine();
                }

                return message.ToString();
            }
        }
    }
}
#endif
