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
    /// Build gate for Unity main-thread APIs inside code regions that move execution to a worker thread.
    /// </summary>
    public sealed class ThreadAffinityPrebuildScanner : IPreprocessBuildWithReport
    {
        private const string RuntimeSourceRoot = "Assets/_Project/Scripts";
        private const string AutoFixReportRelativePath = "Logs/ThreadAffinityAutoFixPreview.txt";
        private const int MaxReportedFindings = 64;

        private static readonly string[] _backgroundMarkers =
        {
            "Task.Run",
            "Awaitable.BackgroundThread",
            "Awaitable.BackgroundThreadAsync"
        };

        private static readonly string[] _mainThreadMarkers =
        {
            "Awaitable.MainThread",
            "Awaitable.MainThreadAsync"
        };

        private static readonly string[] _forbiddenNeedles =
        {
            "Time.",
            "UnityEngine.Object.",
            "Debug.Log",
            "UnityEngine.Debug.Log"
        };

        public int callbackOrder => -950;

        public void OnPreprocessBuild(BuildReport report)
        {
            ScanOrThrow();
        }

        [MenuItem("Tools/Hecton8/Compliance/Scan Thread Affinity")]
        private static void ScanFromMenu()
        {
            ScanOrThrow();
            Debug.Log("[ThreadAffinityPrebuildScanner] No forbidden Unity main-thread API use found in background regions.");
        }

        [MenuItem("Tools/Hecton8/Compliance/Thread Affinity Auto-Fix Preview")]
        private static void WriteAutoFixPreviewFromMenu()
        {
            StringBuilder builder = new StringBuilder(2048);
            int findingCount = ScanAll(builder, includeFixHints: true);
            if (findingCount <= 0)
                builder.AppendLine("[ThreadAffinityPrebuildScanner] No forbidden Unity main-thread API use found in background regions.");

            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), AutoFixReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, builder.ToString());
            Debug.Log("[ThreadAffinityPrebuildScanner] Thread-affinity auto-fix preview written: " + reportPath);
        }

        private static void ScanOrThrow()
        {
            StringBuilder builder = new StringBuilder(1024);
            int findingCount = ScanAll(builder, includeFixHints: false);
            if (findingCount <= 0)
                return;

            builder.Insert(0, "[ThreadAffinityPrebuildScanner] Build blocked. Forbidden Unity API used in background execution region.\n");
            throw new BuildFailedException(builder.ToString());
        }

        private static int ScanAll(StringBuilder builder, bool includeFixHints)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), RuntimeSourceRoot);
            if (!Directory.Exists(root))
                return 0;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            int findingCount = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string normalized = ToAssetPath(file);
                if (IsEditorSource(normalized))
                    continue;

                findingCount += ScanFile(file, normalized, builder, findingCount, includeFixHints);
            }

            return findingCount;
        }

        private static int ScanFile(string absolutePath, string assetPath, StringBuilder builder, int priorFindings, bool includeFixHints)
        {
            string[] lines = File.ReadAllLines(absolutePath);
            bool backgroundRegion = false;
            bool blockComment = false;
            int backgroundStartDepth = 0;
            int braceDepth = 0;
            int localFindings = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string code = StripCommentsAndStrings(lines[lineIndex], ref blockComment);
                int depthBeforeLine = braceDepth;
                bool entersBackground = ContainsAny(code, _backgroundMarkers);
                bool singleLineTaskRun = entersBackground && IsSingleLineTaskRun(code);
                if (entersBackground)
                {
                    backgroundRegion = true;
                    backgroundStartDepth = depthBeforeLine;
                }

                if (backgroundRegion)
                    localFindings += ScanForbiddenNeedles(assetPath, lineIndex + 1, code, builder, priorFindings + localFindings, includeFixHints);

                braceDepth += CountChar(code, '{') - CountChar(code, '}');

                if (singleLineTaskRun)
                {
                    backgroundRegion = false;
                    continue;
                }

                if (backgroundRegion && ContainsAny(code, _mainThreadMarkers) && !entersBackground)
                {
                    backgroundRegion = false;
                    continue;
                }

                if (backgroundRegion &&
                    braceDepth <= backgroundStartDepth &&
                    depthBeforeLine > backgroundStartDepth &&
                    !entersBackground)
                {
                    backgroundRegion = false;
                }
            }

            return localFindings;
        }

        private static int ScanForbiddenNeedles(string assetPath, int lineNumber, string code, StringBuilder builder, int findingIndex, bool includeFixHints)
        {
            int findings = 0;
            for (int i = 0; i < _forbiddenNeedles.Length; i++)
            {
                string needle = _forbiddenNeedles[i];
                if (!ContainsForbiddenNeedle(code, needle))
                    continue;

                if (findingIndex + findings < MaxReportedFindings)
                {
                    builder.Append(assetPath).Append(':').Append(lineNumber).Append(" -> ").Append(needle).Append('\n');
                    if (includeFixHints)
                        AppendAutoFixHint(builder, needle);
                }

                findings++;
            }

            return findings;
        }

        private static void AppendAutoFixHint(StringBuilder builder, string needle)
        {
            if (needle == "Time.")
            {
                builder.Append("    auto-fix: capture Time.frameCount/Time.unscaledTime on the main thread into a readonly context struct, then pass that struct into the background pipeline.\n");
                return;
            }

            if (needle == "UnityEngine.Object.")
            {
                builder.Append("    auto-fix: resolve UnityEngine.Object references to instance IDs or immutable DTO fields on the main thread before entering the background region.\n");
                return;
            }

            builder.Append("    auto-fix: move logging to the main thread or wrap it in an editor/development-only queue drained after Awaitable.MainThreadAsync.\n");
        }

        private static bool ContainsForbiddenNeedle(string code, string needle)
        {
            if (needle == "Time.")
                return ContainsUnityTimeToken(code);

            return code.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsUnityTimeToken(string code)
        {
            int searchStart = 0;
            while (searchStart < code.Length)
            {
                int index = code.IndexOf("Time.", searchStart, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                if (index == 0 || !IsIdentifierOrMemberChar(code[index - 1]))
                    return true;

                searchStart = index + 5;
            }

            return false;
        }

        private static bool IsSingleLineTaskRun(string code)
        {
            return code.IndexOf("Task.Run", StringComparison.Ordinal) >= 0 &&
                   CountChar(code, '{') == CountChar(code, '}');
        }

        private static bool IsIdentifierOrMemberChar(char value)
        {
            return value == '_' || value == '.' || (value >= '0' && value <= '9') || (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        private static string StripCommentsAndStrings(string line, ref bool blockComment)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            char[] buffer = line.ToCharArray();
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (blockComment)
                {
                    if (i + 1 < buffer.Length && buffer[i] == '*' && buffer[i + 1] == '/')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        blockComment = false;
                        continue;
                    }

                    buffer[i] = ' ';
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && i + 1 < buffer.Length && buffer[i] == '"' && buffer[i + 1] == '"')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (!verbatimString && buffer[i] == '\\')
                    {
                        buffer[i] = ' ';
                        if (i + 1 < buffer.Length)
                            buffer[++i] = ' ';
                        continue;
                    }

                    if (buffer[i] == '"')
                    {
                        buffer[i] = ' ';
                        stringLiteral = false;
                        verbatimString = false;
                        continue;
                    }

                    buffer[i] = ' ';
                    continue;
                }

                if (charLiteral)
                {
                    if (buffer[i] == '\\')
                    {
                        buffer[i] = ' ';
                        if (i + 1 < buffer.Length)
                            buffer[++i] = ' ';
                        continue;
                    }

                    if (buffer[i] == '\'')
                    {
                        buffer[i] = ' ';
                        charLiteral = false;
                        continue;
                    }

                    buffer[i] = ' ';
                    continue;
                }

                if (i + 1 < buffer.Length && buffer[i] == '/' && buffer[i + 1] == '/')
                {
                    BlankRange(buffer, i, buffer.Length - i);
                    break;
                }

                if (i + 1 < buffer.Length && buffer[i] == '/' && buffer[i + 1] == '*')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (buffer[i] == '"' || (i + 1 < buffer.Length && buffer[i] == '@' && buffer[i + 1] == '"'))
                {
                    if (buffer[i] == '@')
                    {
                        buffer[i] = ' ';
                        i++;
                        verbatimString = true;
                    }

                    buffer[i] = ' ';
                    stringLiteral = true;
                    continue;
                }

                if (buffer[i] == '\'')
                {
                    buffer[i] = ' ';
                    charLiteral = true;
                }
            }

            return new string(buffer);
        }

        private static bool ContainsAny(string value, string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (value.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static int CountChar(string value, char target)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == target)
                    count++;
            }

            return count;
        }

        private static void BlankRange(char[] buffer, int startIndex, int length)
        {
            int end = startIndex + length;
            for (int i = startIndex; i < end; i++)
                buffer[i] = ' ';
        }

        private static bool IsEditorSource(string assetPath)
        {
            return assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string relative = absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : absolutePath;

            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
