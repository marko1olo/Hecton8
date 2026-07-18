using System.Threading.Tasks;
using MapMagic.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor.Diagnostics
{
    public static class FixMapMagicHeightTask
    {
        [MenuItem("Hecton8/Diagnostics/Fix MapMagic Height")]
        public static async void RunFix()
        {
            await Execute();
        }

        public static Task Execute()
        {
            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            MapMagicObject mapMagicObj = Object.FindAnyObjectByType<MapMagicObject>();
            if (mapMagicObj != null)
            {
                Debug.Log($"[FixMapMagicHeightTask] Found MapMagicObject. Current Height: {mapMagicObj.globals.height}, Position: {mapMagicObj.transform.position}");
                
                // Fix height to 12000
                mapMagicObj.globals.height = 12000f;
                
                // Shift down so that normalized 0 = -10000
                Vector3 pos = mapMagicObj.transform.position;
                pos.y = -10000f;
                mapMagicObj.transform.position = pos;
                
                EditorUtility.SetDirty(mapMagicObj);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[FixMapMagicHeightTask] Successfully updated MapMagicObject. New Height: {mapMagicObj.globals.height}, Position: {mapMagicObj.transform.position}");
            }
            else
            {
                Debug.LogError("[FixMapMagicHeightTask] Could not find MapMagicObject in the scene.");
            }

            return Task.CompletedTask;
        }

        // Headless entry point
        public static void ExecuteHeadless()
        {
            var task = Execute();
            task.Wait();
            EditorApplication.Exit(0);
        }
    }
}
