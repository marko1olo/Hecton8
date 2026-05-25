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
    /// Static Vulkan/Metal/mobile shader portability risk scan. It does not mutate shader assets.
    /// </summary>
    internal sealed class ShaderPortabilityRiskValidator : IPreprocessBuildWithReport
    {
        private const string StrictDefine = "HECTON_STRICT_SHADER_PORTABILITY_BUILD";
        private const int MaxFindings = 64;

        private static readonly string[] s_roots =
        {
            "Assets/_Project/Art/Shaders",
            "Assets/_Project/Shaders",
            "Assets/_Project/Scripts"
        };

        public int callbackOrder => -4660;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool strict = HasStrictDefine(report.summary.platform);
            Scan(strict, throwOnStrict: strict);
        }

        [MenuItem("HECTON-8/Platform/Scan Shader Portability Risks")]
        private static void ScanFromMenu()
        {
            Scan(strict: false, throwOnStrict: false);
        }

        [MenuItem("HECTON-8/Platform/Scan Shader Portability Risks Strict")]
        private static void ScanStrictFromMenu()
        {
            Scan(strict: true, throwOnStrict: true);
        }

        private static void Scan(bool strict, bool throwOnStrict)
        {
            StringBuilder findings = new StringBuilder(2048);
            int findingCount = 0;

            for (int rootIndex = 0; rootIndex < s_roots.Length; rootIndex++)
            {
                string root = s_roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(file);
                    if (!IsShaderSourceExtension(extension) || IsArchivedPath(file))
                        continue;

                    findingCount += ScanFile(file, findings, findingCount);
                    if (findingCount >= MaxFindings)
                        break;
                }

                if (findingCount >= MaxFindings)
                    break;
            }

            if (findingCount <= 0)
            {
                Debug.Log("[PLATFORM] Shader portability risk scan found no first-party risk markers.");
                return;
            }

            findings.Insert(0, "[PLATFORM] Shader portability risk scan found " + findingCount + " marker(s):\n");
            if (strict && throwOnStrict)
                throw new BuildFailedException(findings.ToString());

            Debug.LogWarning(findings.ToString() + "\nDefine " + StrictDefine + " to make these findings hard build blockers.");
        }

        private static int ScanFile(string path, StringBuilder findings, int priorCount)
        {
            string[] lines = File.ReadAllLines(path);
            string assetPath = path.Replace(Path.DirectorySeparatorChar, '/');
            int localCount = 0;
            bool computeFile = assetPath.EndsWith(".compute", StringComparison.OrdinalIgnoreCase);
            bool sawGroupBarrier = false;
            bool sawReturn = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = StripLineComment(lines[lineIndex]);
                if (computeFile && ContainsAny(line, "GroupMemoryBarrierWithGroupSync", "AllMemoryBarrierWithGroupSync"))
                {
                    sawGroupBarrier = true;
                    localCount += AppendFinding(findings, priorCount + localCount, assetPath, lineIndex + 1, "compute group barrier needs Vulkan/Deck validation and no divergent early-return path.");
                }

                if (computeFile && line.IndexOf("return", StringComparison.Ordinal) >= 0)
                    sawReturn = true;

                if (ContainsAny(line, "asuint(", "InterlockedAdd", "InterlockedExchange", "<<", ">>"))
                    localCount += AppendFinding(findings, priorCount + localCount, assetPath, lineIndex + 1, "bitwise/atomic shader path needs Vulkan SPIR-V/mobile compiler validation.");

                if (ContainsAny(line, " sin(", "=sin(", "*sin(", "+sin(", "-sin(", " cos(", "=cos(", "*cos(", "+cos(", "-cos("))
                    localCount += AppendFinding(findings, priorCount + localCount, assetPath, lineIndex + 1, "direct sin/cos in shader path; prefer triangle/parabolic approximation or LUT for mobile/Deck hot paths.");
            }

            if (computeFile && sawGroupBarrier && sawReturn)
                localCount += AppendFinding(findings, priorCount + localCount, assetPath, 0, "compute file has both group barriers and returns; audit for divergent barrier deadlock.");

            return localCount;
        }

        private static bool IsShaderSourceExtension(string extension)
        {
            return string.Equals(extension, ".compute", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsArchivedPath(string path)
        {
            string normalized = path.Replace(Path.DirectorySeparatorChar, '/');
            return normalized.IndexOf("/_Archive/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Archive/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string line, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (line.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static int AppendFinding(StringBuilder findings, int findingIndex, string assetPath, int lineNumber, string message)
        {
            if (findingIndex < MaxFindings)
            {
                findings.Append(assetPath);
                if (lineNumber > 0)
                    findings.Append(':').Append(lineNumber);

                findings.Append(" -> ").Append(message).Append('\n');
            }

            return 1;
        }

        private static string StripLineComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }

        private static bool HasStrictDefine(BuildTarget target)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            return defines.IndexOf(StrictDefine, StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
