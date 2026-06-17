#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Fails Apple-family builds when first-party shader sources rely on half precision without an explicit waiver.
    /// </summary>
    internal sealed class MetalShaderPrecisionAuditor : IPreprocessBuildWithReport
    {
        private const string ProjectShaderRoot = "Assets/_Project";
        private const string WaiverToken = "HECTON_METAL_HALF_OK";
        private const string HalfToken = "half";

        public int callbackOrder => -4610;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsAppleFamilyBuild(report.summary.platform))
                return;

            string violation = FindFirstHalfPrecisionViolation();
            if (!string.IsNullOrEmpty(violation))
                throw new BuildFailedException(violation);
        }

        [MenuItem("Hecton8/Platform/Audit Metal Shader Precision")]
        private static void AuditFromMenu()
        {
            string violation = FindFirstHalfPrecisionViolation();
            if (string.IsNullOrEmpty(violation))
            {
                Debug.Log("[PLATFORM] Metal shader precision audit passed.");
                return;
            }

            throw new BuildFailedException(violation);
        }

        private static string FindFirstHalfPrecisionViolation()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { ProjectShaderRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsShaderSource(assetPath))
                    continue;

                if (TryFindHalfToken(assetPath, out int lineNumber))
                {
                    return "Metal shader precision blocker: '" +
                           assetPath +
                           "' line " +
                           lineNumber +
                           " uses half precision without " +
                           WaiverToken +
                           ".";
                }
            }

            return null;
        }

        private static bool TryFindHalfToken(string assetPath, out int lineNumber)
        {
            lineNumber = 0;
            int currentLineNumber = 0;
            foreach (string line in File.ReadLines(assetPath))
            {
                currentLineNumber++;
                if (line.IndexOf(WaiverToken, StringComparison.Ordinal) >= 0)
                    continue;

                int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                int scanLength = commentIndex >= 0 ? commentIndex : line.Length;
                if (ContainsHalfToken(line, scanLength))
                {
                    lineNumber = currentLineNumber;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsHalfToken(string line, int scanLength)
        {
            int cursor = 0;
            while (cursor < scanLength)
            {
                int index = line.IndexOf(HalfToken, cursor, scanLength - cursor, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int end = index + HalfToken.Length;
                bool leftBoundary = index == 0 || !IsIdentifierChar(line[index - 1]);
                bool rightBoundary = end >= scanLength || !IsIdentifierChar(line[end]);
                if (leftBoundary && rightBoundary)
                    return true;

                cursor = end;
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= '0' && value <= '9') ||
                   value == '_';
        }

        private static bool IsShaderSource(string assetPath)
        {
            return assetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
                   assetPath.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase) ||
                   assetPath.EndsWith(".compute", StringComparison.OrdinalIgnoreCase) ||
                   assetPath.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAppleFamilyBuild(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX ||
                   target == BuildTarget.iOS ||
                   target == BuildTarget.tvOS;
        }
    }
}
#endif
