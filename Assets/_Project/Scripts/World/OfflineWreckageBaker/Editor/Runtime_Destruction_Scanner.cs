using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    public static class Runtime_Destruction_Scanner
    {
        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Combat",
            "Assets/_Project/Scripts/Gameplay/Combat",
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/Habitat",
            "Assets/_Project/Scripts/Vehicles",
            "Assets/_Project/Scripts/World"
        };

        private static readonly string[] s_forbiddenPatterns =
        {
            "sharedMesh.vertices",
            ".mesh.vertices",
            "new Mesh(",
            "Mesh.AllocateWritableMeshData",
            "ApplyAndDisposeWritableMeshData(",
            "SetVertices(",
            "SetTriangles(",
            "SetIndices(",
            "SetNormals(",
            "SetTangents(",
            "SetUVs(",
            "SetColors(",
            "SetVertexBufferData(",
            "SetIndexBufferData(",
            "CombineMeshes(",
            ".triangles",
            "RecalculateNormals(",
            "AddBlendShapeFrame",
            "SkinnedMeshRenderer",
            "Shatter(",
            "ShatterMesh",
            "FractureMesh",
            "FractureShard",
            "ProceduralFracture",
            "AddComponent<Rigidbody>",
            "Instantiate("
        };

        [MenuItem("Hecton8/Wreckage Forge/Scan Runtime Destruction")]
        public static void ScanMenu()
        {
            int findings = ScanFindings(Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length));
            Debug.Log("Runtime destruction scan findings: " + findings);
        }

        public static int ScanFindings(string projectRoot)
        {
            int findingCount = 0;
            for (int rootIndex = 0; rootIndex < s_roots.Length; rootIndex++)
            {
                string root = Path.Combine(projectRoot, s_roots[rootIndex]);
                if (!Directory.Exists(root))
                    continue;

                foreach (string discoveredFile in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string file = discoveredFile.Replace('\\', '/');
                    if (file.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    findingCount += ScanFileFindings(file);
                }
            }

            return findingCount;
        }

        private static int ScanFileFindings(string file)
        {
            int findingCount = 0;
            int preprocessorDepth = 0;
            int editorOnlySkipDepth = -1;
            foreach (string line in File.ReadLines(file))
            {
                string trimmed = line.TrimStart();
                if (TryConsumePreprocessor(trimmed, ref preprocessorDepth, ref editorOnlySkipDepth))
                    continue;

                if (editorOnlySkipDepth >= 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                findingCount += CountForbiddenPatterns(line);
            }

            return findingCount;
        }

        private static bool TryConsumePreprocessor(string trimmed, ref int preprocessorDepth, ref int editorOnlySkipDepth)
        {
            if (trimmed.StartsWith("#if", StringComparison.Ordinal))
            {
                preprocessorDepth++;
                if (editorOnlySkipDepth < 0 && IsEditorOnlyCondition(trimmed, 3))
                    editorOnlySkipDepth = preprocessorDepth;

                return true;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (editorOnlySkipDepth == preprocessorDepth)
                    editorOnlySkipDepth = -1;
                else if (editorOnlySkipDepth < 0 && IsEditorOnlyCondition(trimmed, 5))
                    editorOnlySkipDepth = preprocessorDepth;

                return true;
            }

            if (trimmed.StartsWith("#else", StringComparison.Ordinal))
            {
                if (editorOnlySkipDepth == preprocessorDepth)
                    editorOnlySkipDepth = -1;

                return true;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
            {
                if (editorOnlySkipDepth == preprocessorDepth)
                    editorOnlySkipDepth = -1;

                if (preprocessorDepth > 0)
                    preprocessorDepth--;

                return true;
            }

            return false;
        }

        private static bool IsEditorOnlyCondition(string directive, int conditionOffset)
        {
            if (directive.Length <= conditionOffset)
                return false;

            string condition = directive.Substring(conditionOffset);
            return condition.IndexOf("UNITY_EDITOR", StringComparison.Ordinal) >= 0 &&
                   condition.IndexOf("DEVELOPMENT_BUILD", StringComparison.Ordinal) < 0 &&
                   condition.IndexOf("||", StringComparison.Ordinal) < 0 &&
                   condition.IndexOf("!", StringComparison.Ordinal) < 0;
        }

        private static int CountForbiddenPatterns(string line)
        {
            int findingCount = 0;
            for (int patternIndex = 0; patternIndex < s_forbiddenPatterns.Length; patternIndex++)
            {
                string pattern = s_forbiddenPatterns[patternIndex];
                int offset = line.IndexOf(pattern, StringComparison.Ordinal);
                while (offset >= 0)
                {
                    findingCount++;
                    offset = line.IndexOf(pattern, offset + pattern.Length, StringComparison.Ordinal);
                }
            }

            return findingCount;
        }
    }
}
