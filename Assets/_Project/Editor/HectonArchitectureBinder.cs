using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hecton8.Graphics;
using Hecton8.Graphics.Authoring;
using Hecton8.Celestial;
using System.IO;

namespace Hecton8.EditorTools
{
    public static class HectonArchitectureBinder
    {
        public static void BindVisualsOrchestrator020()
        {
            Debug.Log("[Architect] Binding Visuals Orchestrator to 020_RENDER_SANDBOX...");
            var scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Ensure Facade exists
            string facadePath = "Assets/_Project/Settings/VisualTuningFacade.asset";
            if (!Directory.Exists("Assets/_Project/Settings"))
            {
                Directory.CreateDirectory("Assets/_Project/Settings");
            }
            
            var facade = AssetDatabase.LoadAssetAtPath<VisualTuningFacadeSO>(facadePath);
            if (facade == null)
            {
                facade = ScriptableObject.CreateInstance<VisualTuningFacadeSO>();
                AssetDatabase.CreateAsset(facade, facadePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Architect] Created VisualTuningFacadeSO at " + facadePath);
            }

            // 2. Add Orchestrator to scene
            var existing = Object.FindAnyObjectByType<HectonVisualsOrchestrator>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject("VisualsOrchestrator");
            var orchestrator = go.AddComponent<HectonVisualsOrchestrator>();

            // 3. Bind properties
            var so = new SerializedObject(orchestrator);
            var ceProp = so.FindProperty("_celestialEngine");
            var celestial = Object.FindAnyObjectByType<HectonCelestialEngine>();
            if (celestial != null)
            {
                ceProp.objectReferenceValue = celestial;
            }

            var matProp = so.FindProperty("_oceanMaterial");
            string matPath = "Assets/Crest/Crest/Materials/Ocean.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                matProp.objectReferenceValue = mat;
            }

            so.ApplyModifiedProperties();

            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Architect] Bound orchestrator. Scene saved: {saved}");

            // 4. Force Bake
            string OutputPath = "Assets/StreamingAssets/Hecton8/DataMonolith/visual_tuning.h8bin";
            var dir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            VisualTuningState state = facade.BakeToUnmanaged();
            int size = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<VisualTuningState>();
            byte[] buffer = new byte[size];
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    Unity.Collections.LowLevel.Unsafe.UnsafeUtility.CopyStructureToPtr(ref state, ptr);
                }
            }
            File.WriteAllBytes(OutputPath, buffer);
            AssetDatabase.Refresh();
            Debug.Log($"[Architect] Baked tuning state to {OutputPath}. Ready for screenshot.");

            EditorApplication.Exit(0);
        }
    }
}
