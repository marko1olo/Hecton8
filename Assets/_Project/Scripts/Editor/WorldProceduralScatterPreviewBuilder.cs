using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralScatterPreviewBuilder
    {
        private static readonly List<GameObject> s_sceneRoots = new List<GameObject>(8);
        private static readonly List<WorldProceduralScatterDirector> s_scatterDirectors = new List<WorldProceduralScatterDirector>(2);

        [MenuItem("Hecton/Authoring/Rebuild Procedural Scatter Preview", priority = 180)]
        public static void RebuildProceduralScatterPreview()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] No active loaded scene.");
                return;
            }

            WorldProceduralScatterDirector director = FindScatterDirector(activeScene);
            if (director == null)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] WorldProceduralScatterDirector not found in scene.");
                return;
            }

            director.RebuildScatterPreview();
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[WorldProceduralScatterPreviewBuilder] Procedural scatter preview rebuilt.");
        }

        [MenuItem("Hecton/Authoring/Clear Procedural Scatter Preview", priority = 181)]
        public static void ClearProceduralScatterPreview()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] No active loaded scene.");
                return;
            }

            WorldProceduralScatterDirector director = FindScatterDirector(activeScene);
            if (director == null)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] WorldProceduralScatterDirector not found in scene.");
                return;
            }

            director.ClearScatterPreview();
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[WorldProceduralScatterPreviewBuilder] Procedural scatter preview cleared.");
        }

        private static WorldProceduralScatterDirector FindScatterDirector(Scene scene)
        {
            s_sceneRoots.Clear();
            s_scatterDirectors.Clear();
            if (s_sceneRoots.Capacity < scene.rootCount)
                s_sceneRoots.Capacity = scene.rootCount;

            scene.GetRootGameObjects(s_sceneRoots);

            for (int i = 0; i < s_sceneRoots.Count; i++)
            {
                GameObject root = s_sceneRoots[i];
                if (root == null)
                    continue;

                root.GetComponentsInChildren<WorldProceduralScatterDirector>(true, s_scatterDirectors);
                if (s_scatterDirectors.Count <= 0)
                    continue;

                WorldProceduralScatterDirector director = s_scatterDirectors[0];
                s_sceneRoots.Clear();
                s_scatterDirectors.Clear();
                return director;
            }

            s_sceneRoots.Clear();
            s_scatterDirectors.Clear();
            return null;
        }
    }
}
