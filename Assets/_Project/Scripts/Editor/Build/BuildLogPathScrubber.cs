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
            string outputPath = Path.Combine(OutputDirectory, "EditorLog.sanitized.txt");
            using (StreamReader reader = new StreamReader(logPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            using (StreamWriter writer = new StreamWriter(outputPath, append: false, NoBomUtf8))
            {
                while (!reader.EndOfStream)
                    writer.WriteLine(Sanitize(reader.ReadLine()));
            }
        }

        private static void EnsureOutputDirectory()
        {
            Directory.CreateDirectory(OutputDirectory);
        }

        private static unsafe string ScrubAbsolutePathFragments(string value)
        {
            char[] output = null;
            StringBuilder overflowBuilder = null;
            int outputIndex = 0;
            int copyStart = 0;
            int index = 0;
            bool replaced = false;
            fixed (char* valuePtr = value)
            {
                while (index < value.Length)
                {
                    while (index < value.Length &&
                           valuePtr[index] != ':' &&
                           valuePtr[index] != '/')
                    {
                        index++;
                    }

                    if (index >= value.Length)
                        break;

                    char current = valuePtr[index];
                    int pathStart = -1;
                    if (current == '/')
                    {
                        pathStart = index;
                    }
                    else if (current == ':' &&
                             index > 0 &&
                             index + 1 < value.Length &&
                             IsAsciiLetter(valuePtr[index - 1]) &&
                             IsSlash(valuePtr[index + 1]))
                    {
                        pathStart = index - 1;
                    }
                    else
                    {
                        index++;
                        continue;
                    }

                    if (!IsAbsolutePathStart(valuePtr, value.Length, pathStart))
                    {
                        index++;
                        continue;
                    }

                    replaced = true;
                    output ??= new char[value.Length];
                    if (overflowBuilder != null)
                    {
                        overflowBuilder.Append(value, copyStart, pathStart - copyStart);
                        overflowBuilder.Append(MachinePathToken);
                    }
                    else if (!TryAppend(value, copyStart, pathStart - copyStart, MachinePathToken, ref output, ref outputIndex))
                    {
                        overflowBuilder ??= CreateOverflowBuilder(value, copyStart, output, outputIndex);
                        overflowBuilder.Append(value, copyStart, pathStart - copyStart);
                        overflowBuilder.Append(MachinePathToken);
                    }

                    index = FindPathEnd(valuePtr, value.Length, pathStart);
                    copyStart = index;
                }
            }

            if (!replaced)
                return value;

            if (overflowBuilder != null)
            {
                overflowBuilder.Append(value, copyStart, value.Length - copyStart);
                return overflowBuilder.ToString();
            }

            CopyChars(value, copyStart, value.Length - copyStart, output, ref outputIndex);
            return new string(output, 0, outputIndex);
        }

        private static StringBuilder CreateOverflowBuilder(string value, int copyStart, char[] output, int outputIndex)
        {
            StringBuilder builder = new StringBuilder(value.Length + MachinePathToken.Length);
            if (outputIndex > 0)
                builder.Append(output, 0, outputIndex);
            return builder;
        }

        private static bool TryAppend(
            string source,
            int sourceStart,
            int sourceLength,
            string token,
            ref char[] output,
            ref int outputIndex)
        {
            int required = outputIndex + sourceLength + token.Length;
            if (required > output.Length)
                return false;

            CopyChars(source, sourceStart, sourceLength, output, ref outputIndex);
            CopyChars(token, 0, token.Length, output, ref outputIndex);
            return true;
        }

        private static void CopyChars(string source, int sourceStart, int sourceLength, char[] output, ref int outputIndex)
        {
            int safeLength = Math.Min(sourceLength, source.Length - sourceStart);
            for (int i = 0; i < safeLength; i++)
                output[outputIndex++] = source[sourceStart + i];
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

            if (value[index] != '/')
                return false;

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
