#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class H8RejectedVisualPassSceneCleaner
    {
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string ManifestPath = "Docs/DEPRECATED/RejectedVisualPasses/20260608_scene_cleanup/H8RejectedVisualPassSceneCleanup.txt";
        private const string RejectedFreshRoot = "H8_CODEX_WATER_SKY_FIRST_PASS_20260608";
        private const string DeprecatedWaterSkyPrefix = "DEPRECATED_WATER_SKY_20260608__";

        public static void CleanAndExit()
        {
            int exitCode = 0;
            try
            {
                CleanScene();
            }
            catch (Exception exception)
            {
                exitCode = 1;
                Debug.LogException(exception);
                WriteManifest(new[] { "status=FAILED", "error=" + exception });
            }

            EditorApplication.Exit(exitCode);
        }

        private static void CleanScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            List<string> deleted = new List<string>(64);
            List<string> kept = new List<string>(64);

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                string name = root.name;
                if (name == RejectedFreshRoot || name.StartsWith(DeprecatedWaterSkyPrefix, StringComparison.Ordinal))
                {
                    deleted.Add(name);
                    UnityEngine.Object.DestroyImmediate(root);
                }
                else if (name.Contains("WATER_SKY", StringComparison.Ordinal) ||
                         name.Contains("CODEX", StringComparison.Ordinal) ||
                         name.Contains("Codex", StringComparison.Ordinal))
                {
                    kept.Add(name);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            List<string> lines = new List<string>(128)
            {
                "status=OK",
                "scene=" + ScenePath,
                "policy=delete proven rejected fresh visual root plus inactive deprecated water/sky roots only",
                "deleted_count=" + deleted.Count
            };
            for (int i = 0; i < deleted.Count; i++)
                lines.Add("deleted=" + deleted[i]);
            lines.Add("kept_review_count=" + kept.Count);
            for (int i = 0; i < kept.Count; i++)
                lines.Add("kept_for_manual_review=" + kept[i]);

            WriteManifest(lines);
            Debug.Log("[H8RejectedVisualPassSceneCleaner] deleted=" + deleted.Count + " scene=" + ScenePath);
        }

        private static void WriteManifest(IEnumerable<string> lines)
        {
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllLines(absolute, lines, new UTF8Encoding(false));
        }
    }
}
#endif
