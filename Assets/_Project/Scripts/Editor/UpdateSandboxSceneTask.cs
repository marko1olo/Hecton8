using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class UpdateSandboxSceneTask
    {
        [InitializeOnLoadMethod]
        private static void Run()
        {
            return;
            if (SessionState.GetBool("UpdateSandboxSceneTaskRun", false)) return;
            SessionState.SetBool("UpdateSandboxSceneTaskRun", true);

            EditorApplication.delayCall += () => {
                try
                {
                    string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
                    Scene activeScene = EditorSceneManager.GetActiveScene();
                    if (activeScene.path != scenePath)
                    {
                        EditorSceneManager.OpenScene(scenePath);
                    }

                    var mapMagicObject = UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
                    if (mapMagicObject == null)
                    {
                        Debug.LogError("MapMagicObject not found!");
                        return;
                    }

                    string graphPath = "Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset";
                    var newGraph = AssetDatabase.LoadAssetAtPath<MapMagic.Nodes.Graph>(graphPath);

                    if (newGraph == null)
                    {
                        Debug.LogError("Failed to load the new graph asset.");
                        return;
                    }

                    mapMagicObject.graph = newGraph;
                    mapMagicObject.draftsInEditor = true;

                    EditorUtility.SetDirty(mapMagicObject);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    EditorSceneManager.SaveOpenScenes();

                    Debug.Log("Scene updated successfully with new procedural graph.");
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            };
        }
    }
}
