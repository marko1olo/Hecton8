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
            // TerrainData.size.y for every tile in this scene. 4000 m is preserved EXACTLY - it is not the
            // canonical vertical span and it is not being corrected here.
            //
            // WorldVerticalExtentMath.DefaultVerticalSpanMeters (Scripts/World/WorldVerticalExtentContracts.cs)
            // is 7000 m, and HectonSandboxAbyssalShelfMapMagicNode.Generate warns at runtime whenever
            // globals.height != HighWorldY - LowWorldY, because the difference uniformly compresses every
            // slope, cliff, shelf-break and trench in the scene - here by 7000/4000 = 1.75x.
            //
            // Raising this to 7000 would change generated geometry in 020_RENDER_SANDBOX_V2, and which of
            // the two numbers is right is the owner's vertical-extent decision. So the value stays and only
            // its home moved: it is recorded next to the canonical span, where the contradiction is visible.
            mm.globals.height = Hecton8.World.WorldVerticalExtentMath.SandboxV2AuthoredTerrainHeightMeters;
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
