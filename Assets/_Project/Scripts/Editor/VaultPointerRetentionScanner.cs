#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Memory.Editor
{
    internal static class VaultPointerRetentionScanner
    {
        private const string StrictEnvVar = "HECTON_VAULT_POINTER_AUDIT_STRICT";
        private const string ReportPath = "Docs/AgentLogs/VaultPointerAudit_SHINOBU_202.md";
        private const int MaxReportRows = 256;

        [InitializeOnLoadMethod]
        private static void RunStrictGateWhenRequested()
        {
            if (!string.Equals(System.Environment.GetEnvironmentVariable(StrictEnvVar), "1", StringComparison.Ordinal))
                return;

            if (!RunAudit(writeReport: true))
                throw new InvalidOperationException("SHINOBU_202 vault pointer retention audit failed. See Docs/AgentLogs/VaultPointerAudit_SHINOBU_202.md.");
        }

        [MenuItem("Hecton8/Memory/Run Vault Pointer Retention Audit")]
        private static void RunMenuAudit()
        {
            bool clean = RunAudit(writeReport: true);
            if (clean)
                Debug.Log("SHINOBU_202 vault pointer retention audit found no runtime pointer-retention hits.");
            else
                Debug.LogError("SHINOBU_202 vault pointer retention audit found runtime pointer-retention hits. See Docs/AgentLogs/VaultPointerAudit_SHINOBU_202.md.");
        }

        internal static bool RunAudit(bool writeReport)
        {
            string projectRoot = ResolveProjectRoot();
            string scriptRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);

            int persistentNativeFields = 0;
            int legacyHandleFields = 0;
            int rawPointerLeases = 0;
            int rowsWritten = 0;
            StreamWriter writer = null;
            try
            {
                if (writeReport)
                {
                    writer = new StreamWriter(Path.Combine(projectRoot, ReportPath), false, Encoding.UTF8);
                    writer.WriteLine("# VaultPointerAudit_SHINOBU_202");
                    writer.WriteLine();
                    writer.WriteLine("Policy: runtime managers may persist pointer-free VaultGenerationHandle<T> descriptors only. Cached NativeArray, NativeSlice, raw pointer fields, legacy VaultBufferHandle<T> fields, `.ptr`, and ResolvePointer routes are migration debt.");
                    writer.WriteLine();
                    writer.WriteLine("| File | Line | Violation |");
                    writer.WriteLine("|---|---:|---|");
                }

                foreach (string file in Directory.EnumerateFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
                {
                    if (IsEditorPath(file))
                        continue;

                    string relative = ToProjectRelative(projectRoot, file);
                    int lineNumber = 0;
                    foreach (string line in File.ReadLines(file))
                    {
                        lineNumber++;
                        string trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//", StringComparison.Ordinal))
                            continue;

                        if (ContainsPersistentNativeField(trimmed))
                        {
                            persistentNativeFields++;
                            WriteRow(writer, relative, lineNumber, "persistent NativeArray/NativeSlice/raw pointer field", ref rowsWritten);
                        }

                        if (ContainsLegacyVaultHandleField(trimmed))
                        {
                            legacyHandleFields++;
                            WriteRow(writer, relative, lineNumber, "legacy VaultBufferHandle<T> field; migrate to VaultGenerationHandle<T>", ref rowsWritten);
                        }

                        if (ContainsRawPointerLease(trimmed))
                        {
                            rawPointerLeases++;
                            WriteRow(writer, relative, lineNumber, "raw Vault pointer lease route", ref rowsWritten);
                        }
                    }
                }

                if (writer != null)
                {
                    writer.WriteLine();
                    writer.WriteLine("## Summary");
                    writer.WriteLine();
                    writer.WriteLine("- Persistent NativeArray/NativeSlice/raw pointer fields: " + persistentNativeFields);
                    writer.WriteLine("- Legacy VaultBufferHandle<T> fields: " + legacyHandleFields);
                    writer.WriteLine("- Raw pointer lease routes: " + rawPointerLeases);
                    writer.WriteLine("- Rows shown: " + rowsWritten);
                    writer.WriteLine();
                    writer.WriteLine("Acceptance gate: CI may set `HECTON_VAULT_POINTER_AUDIT_STRICT=1` to convert this audit into an editor-load failure.");
                }
            }
            finally
            {
                if (writer != null)
                    writer.Dispose();
            }

            return persistentNativeFields == 0 && legacyHandleFields == 0 && rawPointerLeases == 0;
        }

        private static bool ContainsPersistentNativeField(string line)
        {
            if (!ContainsFieldScope(line))
                return false;

            return line.Contains("NativeArray<") ||
                   line.Contains("NativeSlice<") ||
                   line.Contains("void* ") ||
                   line.Contains("* _");
        }

        private static bool ContainsLegacyVaultHandleField(string line)
        {
            return ContainsFieldScope(line) &&
                   line.Contains("VaultBufferHandle<") &&
                   !line.Contains("VaultGenerationHandle<");
        }

        private static bool ContainsRawPointerLease(string line)
        {
            return line.Contains(".ptr") ||
                   line.Contains("ResolvePointer(");
        }

        private static bool ContainsFieldScope(string line)
        {
            return line.Contains("private ") ||
                   line.Contains("protected ") ||
                   line.Contains("internal ") ||
                   line.Contains("static ");
        }

        private static void WriteRow(StreamWriter writer, string relativePath, int lineNumber, string violation, ref int rowsWritten)
        {
            if (writer == null || rowsWritten >= MaxReportRows)
                return;

            writer.WriteLine("| `" + relativePath + "` | " + lineNumber + " | " + violation + " |");
            rowsWritten++;
        }

        private static bool IsEditorPath(string path)
        {
            return path.IndexOf(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(Path.AltDirectorySeparatorChar + "Editor" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo assetsDirectory = new DirectoryInfo(Application.dataPath);
            return assetsDirectory.Parent != null ? assetsDirectory.Parent.FullName : Directory.GetCurrentDirectory();
        }

        private static string ToProjectRelative(string projectRoot, string file)
        {
            if (file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                int start = projectRoot.Length;
                if (file.Length > start && (file[start] == Path.DirectorySeparatorChar || file[start] == Path.AltDirectorySeparatorChar))
                    start++;

                return file.Substring(start).Replace('\\', '/');
            }

            return file.Replace('\\', '/');
        }
    }
}
#endif
