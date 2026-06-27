using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using MapMagic.Nodes;

namespace Hecton8.Editor
{
    public static class CreateSandboxV2
    {
        [MenuItem("Hecton8/Tests/Create Sandbox V2")]
        public static void Execute()
        {
            // Create a brand new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create MapMagic GameObject INACTIVE to avoid Awake running before graph is assigned
            GameObject mapMagicGO = new GameObject("MapMagic");
            mapMagicGO.SetActive(false);
            var mm = mapMagicGO.AddComponent<MapMagicObject>();

            // Configure MapMagic properties as requested
            mm.tileSize = new Den.Tools.Vector2D(500, 500);
            mm.tileResolution = MapMagicObject.Resolution._513;
            mm.globals.height = 4000;
            mm.graph = AssetDatabase.LoadAssetAtPath<Graph>("Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset");

            if (mm.graph == null)
                Debug.LogWarning("[CreateSandboxV2] Graph asset not found at expected path — MapMagic will have no generators.");

            // Now activate — Awake/OnEnable will fire with graph already set
            mapMagicGO.SetActive(true);

            // Save the scene
            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";
            bool success = EditorSceneManager.SaveScene(scene, scenePath);
            
            if (success)
            {
                Debug.Log($"[CreateSandboxV2] Successfully created and saved sterile sandbox at {scenePath}");
            }
            else
            {
                Debug.LogError($"[CreateSandboxV2] Failed to save sterile sandbox at {scenePath}");
            }
        }
    }
}
