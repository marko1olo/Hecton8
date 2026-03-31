using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralScatterPreviewBuilder
    {
        [MenuItem("Hecton/Authoring/Rebuild Procedural Scatter Preview", priority = 180)]
        public static void RebuildProceduralScatterPreview()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] No active loaded scene.");
                return;
            }

            WorldProceduralScatterDirector director = Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
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

            WorldProceduralScatterDirector director = Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("[WorldProceduralScatterPreviewBuilder] WorldProceduralScatterDirector not found in scene.");
                return;
            }

            director.ClearScatterPreview();
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[WorldProceduralScatterPreviewBuilder] Procedural scatter preview cleared.");
        }
    }
}
