using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Editor-only machine-code purity gate for job layout, hot-path managed math, and dictionary foreach drift.
    /// </summary>
    public sealed class MachineCodePurityPrebuildScanner : IPreprocessBuildWithReport
    {
        private const string RuntimeSourceRoot = "Assets/_Project/Scripts";
        private const string StrictDefine = "HECTON_STRICT_MACHINE_CODE_BUILD";
        private const int MaxFindings = 64;

        private static readonly Regex s_jobStructRegex = new Regex(
            @"\bstruct\s+[A-Za-z_][A-Za-z0-9_<>]*\s*:\s*(?:[^{}\r\n,]*,\s*)?IJob(?:ParallelFor|ParallelForTransform)?\b",
            RegexOptions.Compiled);

        private static readonly Regex s_hotMemberRegex = new Regex(
            @"\b(?:Execute|Tick|FixedTick|PostFixedTick|Update|LateUpdate|FixedUpdate)\s*\(",
            RegexOptions.Compiled);

        public int callbackOrder => -930;

        public void OnPreprocessBuild(BuildReport report)
        {
            Scan(strictBuild: HasStrictDefine());
        }

        [MenuItem("Tools/Hecton8/Compliance/Scan Machine-Code Purity")]
        private static void ScanFromMenu()
        {
            Scan(strictBuild: false);
        }

        [MenuItem("Tools/Hecton8/Compliance/Scan Machine-Code Purity Strict")]
        private static void ScanStrictFromMenu()
        {
            Scan(strictBuild: true);
        }

        private static void Scan(bool strictBuild)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), RuntimeSourceRoot);
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            StringBuilder findings = new StringBuilder(2048);
            int count = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string assetPath = ToAssetPath(file);
                if (IsEditorSource(assetPath))
                    continue;

                count += ScanFile(file, assetPath, findings, count);
            }

            if (count == 0)
            {
                Debug.Log("[MachineCodePurityPrebuildScanner] No machine-code purity findings.");
                return;
            }

            findings.Insert(0, "[MachineCodePurityPrebuildScanner] Findings:\n");
            if (strictBuild)
                throw new BuildFailedException(findings.ToString());

            Debug.LogWarning(findings.ToString());
        }

        private static int ScanFile(string absolutePath, string assetPath, StringBuilder findings, int priorCount)
        {
            string[] lines = File.ReadAllLines(absolutePath);
            int local = 0;
            bool inHotMember = false;
            int hotStartDepth = 0;
            int braceDepth = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = StripLineComment(lines[lineIndex]);
                int depthBefore = braceDepth;
                if (s_jobStructRegex.IsMatch(line) && !HasPaddedStructLayout(lines, lineIndex))
                    local += AppendFinding(findings, priorCount + local, assetPath, lineIndex + 1, "IJob struct lacks explicit StructLayout Pack/Size alignment.");

                if (s_hotMemberRegex.IsMatch(line))
                {
                    inHotMember = true;
                    hotStartDepth = depthBefore;
                }

                if (inHotMember && line.IndexOf("Mathf.", StringComparison.Ordinal) >= 0)
                    local += AppendFinding(findings, priorCount + local, assetPath, lineIndex + 1, "Mathf used inside hot member; use Unity.Mathematics math.");

                if (IsGameplaySource(assetPath) &&
                    line.IndexOf("foreach", StringComparison.Ordinal) >= 0 &&
                    (line.IndexOf("Dictionary", StringComparison.Ordinal) >= 0 || line.IndexOf("KeyValuePair", StringComparison.Ordinal) >= 0))
                {
                    local += AppendFinding(findings, priorCount + local, assetPath, lineIndex + 1, "foreach over Dictionary/KeyValuePair in Gameplay.");
                }

                braceDepth += CountChar(line, '{') - CountChar(line, '}');
                if (inHotMember && braceDepth <= hotStartDepth && depthBefore > hotStartDepth)
                    inHotMember = false;
            }

            return local;
        }

        private static bool HasPaddedStructLayout(string[] lines, int structLineIndex)
        {
            int start = Math.Max(0, structLineIndex - 4);
            for (int i = start; i < structLineIndex; i++)
            {
                string line = lines[i];
                if (line.IndexOf("StructLayout", StringComparison.Ordinal) < 0)
                    continue;

                if (line.IndexOf("Pack = 16", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("Size = 16", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("Size = 32", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("Size = 64", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int AppendFinding(StringBuilder builder, int findingIndex, string assetPath, int lineNumber, string message)
        {
            if (findingIndex < MaxFindings)
                builder.Append(assetPath).Append(':').Append(lineNumber).Append(" -> ").Append(message).Append('\n');

            return 1;
        }

        private static bool HasStrictDefine()
        {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            return defines.IndexOf(StrictDefine, StringComparison.Ordinal) >= 0;
        }

        private static bool IsEditorSource(string assetPath)
        {
            return assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGameplaySource(string assetPath)
        {
            return assetPath.IndexOf("/Gameplay/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StripLineComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
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
