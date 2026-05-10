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

            string sanitized = value;
            if (!string.IsNullOrEmpty(ProjectRoot))
                sanitized = sanitized.Replace(ProjectRoot, "<PROJECT_ROOT>");

            if (!string.IsNullOrEmpty(UserRoot))
                sanitized = sanitized.Replace(UserRoot, "<USER_HOME>");

            return ScrubAbsolutePathFragments(sanitized);
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

            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private static string ScrubAbsolutePathFragments(string value)
        {
            StringBuilder builder = null;
            int copyStart = 0;
            int index = 0;
            while (index < value.Length)
            {
                if (!IsAbsolutePathStart(value, index))
                {
                    index++;
                    continue;
                }

                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copyStart, index - copyStart);
                builder.Append("<ABS_PATH>");
                index = FindPathEnd(value, index);
                copyStart = index;
            }

            if (builder == null)
                return value;

            builder.Append(value, copyStart, value.Length - copyStart);
            return builder.ToString();
        }

        private static bool IsAbsolutePathStart(string value, int index)
        {
            if (index + 2 < value.Length &&
                IsAsciiLetter(value[index]) &&
                value[index + 1] == ':' &&
                IsSlash(value[index + 2]))
            {
                return true;
            }

            return StartsWith(value, index, "/Users/") || StartsWith(value, index, "/home/");
        }

        private static int FindPathEnd(string value, int index)
        {
            while (index < value.Length)
            {
                char c = value[index];
                if (c == '\r' || c == '\n' || c == '\t' || c == '"' || c == '\'')
                    break;

                index++;
            }

            return index;
        }

        private static bool StartsWith(string value, int index, string token)
        {
            if (index + token.Length > value.Length)
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
