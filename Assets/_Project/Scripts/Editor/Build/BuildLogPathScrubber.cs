#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Writes sanitized build artifacts with local developer paths replaced by stable tokens.
    /// </summary>
    public sealed class BuildLogPathScrubber : IPostprocessBuildWithReport
    {
        private const string OutputDirectory = "Library/Hecton8/SanitizedBuildLogs";
        private const string MachinePathToken = "[H8_BUILD_MACHINE]";
        private static readonly Encoding NoBomUtf8 = new UTF8Encoding(false);
        private static readonly string ProjectRoot = NormalizePath(Directory.GetCurrentDirectory());
        private static readonly string UserRoot = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
            WriteSanitizedBuildReport(report);
            WriteSanitizedEditorLog();
        }

        [MenuItem("Tools/Hecton8/Build/Scrub Logs")]
        private static void ScrubLogsFromMenu()
        {
            WriteSanitizedEditorLog();
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return ScrubAbsolutePathFragments(value);
        }

        private static void WriteSanitizedBuildReport(BuildReport report)
        {
            if (report == null)
                return;

            EnsureOutputDirectory();
            StringBuilder builder = new StringBuilder(2048);
            BuildSummary summary = report.summary;
            builder.Append("result=");
            builder.Append(summary.result);
            builder.AppendLine();
            builder.Append("platform=");
            builder.Append(summary.platform);
            builder.AppendLine();
            builder.Append("outputPath=");
            builder.Append(Sanitize(summary.outputPath));
            builder.AppendLine();
            builder.Append("totalSize=");
            builder.Append(summary.totalSize);
            builder.AppendLine();
            builder.Append("totalTime=");
            builder.Append(summary.totalTime);
            builder.AppendLine();

            string outputPath = Path.Combine(OutputDirectory, "BuildReport.sanitized.txt");
            File.WriteAllText(outputPath, builder.ToString(), NoBomUtf8);
        }

        private static void WriteSanitizedEditorLog()
        {
            string logPath = Application.consoleLogPath;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                return;

            EnsureOutputDirectory();
            string sanitized = Sanitize(File.ReadAllText(logPath, Encoding.UTF8));
            string outputPath = Path.Combine(OutputDirectory, "EditorLog.sanitized.txt");
            File.WriteAllText(outputPath, sanitized, NoBomUtf8);
        }

        private static void EnsureOutputDirectory()
        {
            Directory.CreateDirectory(OutputDirectory);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return NormalizeSlashes(Path.GetFullPath(path));
        }

        private static unsafe string ScrubAbsolutePathFragments(string value)
        {
            StringBuilder builder = null;
            int copyStart = 0;
            int index = 0;
            fixed (char* valuePtr = value)
            {
                while (index < value.Length)
                {
                    if (!IsMachinePathStart(valuePtr, value.Length, index) &&
                        !IsAbsolutePathStart(valuePtr, value.Length, index))
                    {
                        index++;
                        continue;
                    }

                    builder ??= new StringBuilder(value.Length);
                    builder.Append(value, copyStart, index - copyStart);
                    builder.Append(MachinePathToken);
                    index = FindPathEnd(valuePtr, value.Length, index);
                    copyStart = index;
                }
            }

            if (builder == null)
                return value;

            builder.Append(value, copyStart, value.Length - copyStart);
            return builder.ToString();
        }

        private static string NormalizeSlashes(string value)
        {
            StringBuilder builder = null;
            int copyStart = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\')
                    continue;

                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copyStart, i - copyStart);
                builder.Append('/');
                copyStart = i + 1;
            }

            if (builder == null)
                return value;

            builder.Append(value, copyStart, value.Length - copyStart);
            return builder.ToString();
        }

        private static unsafe bool IsMachinePathStart(char* value, int length, int index)
        {
            return StartsWith(value, length, index, ProjectRoot) ||
                   StartsWith(value, length, index, UserRoot);
        }

        private static unsafe bool IsAbsolutePathStart(char* value, int length, int index)
        {
            if (index + 2 < length &&
                IsAsciiLetter(value[index]) &&
                value[index + 1] == ':' &&
                IsSlash(value[index + 2]))
            {
                return true;
            }

            return StartsWith(value, length, index, "/Users/") || StartsWith(value, length, index, "/home/");
        }

        private static unsafe int FindPathEnd(char* value, int length, int index)
        {
            while (index < length)
            {
                char c = value[index];
                if (c == '\r' || c == '\n' || c == '\t' || c == '"' || c == '\'')
                    break;

                index++;
            }

            return index;
        }

        private static unsafe bool StartsWith(char* value, int length, int index, string token)
        {
            if (string.IsNullOrEmpty(token) || index + token.Length > length)
                return false;

            for (int tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
            {
                if (value[index + tokenIndex] != token[tokenIndex])
                    return false;
            }

            return true;
        }

        private static bool IsSlash(char value)
        {
            return value == '/' || value == '\\';
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }
    }
}
#endif
