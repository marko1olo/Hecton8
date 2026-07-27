using System;
using Hecton8.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Connects the marine snow VFX, which was complete except for the two pieces nobody had made.
    ///
    /// Everything else already existed and was already looking for it: HectonUnderwaterVisuals is in
    /// the world scene and resolves the renderer with mainCamera.TryGetComponent, then calls
    /// BindTargetCamera on it. Hecton_MarineSnow.compute and Hecton_MarineSnow.shader both exist.
    /// What was missing was a material bound to that shader - no material in the project used it -
    /// and the renderer component itself, which is absent from all seven scenes. Because the
    /// consumer searches the MAIN CAMERA specifically, the component has to live there.
    ///
    /// The material is created with shader defaults and no textures, which is valid rather than lazy:
    /// _MarineSnowAtlasParams defaults to (8, 8, 0, 0), and the shader reads .z and .w as the normal
    /// and mask atlas weights, so both atlases contribute exactly nothing until someone authors them.
    /// The name says DRAFT because that is what it is - the look is unproven, and calling it final
    /// would be the same fiction as a shader function that returns a plausible constant.
    ///
    /// Report mode first. The world scene is BINARY, so object counts are the only integrity check.
    /// </summary>
    public static class MarineSnowAuthoring
    {
        private const string Marker = "[H8_MARINE_SNOW]";
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_MarineSnow.shader";
        private const string ComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/VFX";
        private const string MaterialAssetPath = MaterialFolder + "/MAT_HectonMarineSnow_DRAFT.mat";
        private const string TargetScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        [MenuItem("Hecton8/VFX/Report Marine Snow Wiring")]
        public static void ReportWiring()
        {
            Run(false);
        }

        [MenuItem("Hecton8/VFX/Wire Marine Snow")]
        public static void ApplyWiring()
        {
            Run(true);
        }

        private static void Run(bool apply)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeAssetPath);
            if (shader == null || compute == null)
            {
                Debug.LogError($"{Marker} ABORT - shader={(shader != null)} compute={(compute != null)}");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            if (material == null)
            {
                if (!apply)
                {
                    Debug.Log($"{Marker} WOULD CREATE material {MaterialAssetPath} (shader defaults, no textures)");
                }
                else
                {
                    if (!AssetDatabase.IsValidFolder(MaterialFolder))
                        AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "VFX");

                    material = new Material(shader) { name = "MAT_HectonMarineSnow_DRAFT" };
                    AssetDatabase.CreateAsset(material, MaterialAssetPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"{Marker} CREATED material {MaterialAssetPath}");
                }
            }
            else
            {
                Debug.Log($"{Marker} material already exists -> {MaterialAssetPath}");
            }

            try
            {
                Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError($"{Marker} INVALID {TargetScene}");
                    return;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                int objects = 0;
                foreach (GameObject root in roots)
                    objects += root.GetComponentsInChildren<Transform>(true).Length;

                Camera mainCamera = FindMainCamera(roots);
                if (mainCamera == null)
                {
                    Debug.LogError($"{Marker} no MainCamera-tagged camera in {TargetScene} - " +
                                   "the consumer searches that camera specifically, so there is nowhere correct to put this");
                    return;
                }

                bool hasRenderer = mainCamera.TryGetComponent(out HectonMarineSnowRenderer renderer);
                Debug.Log($"{Marker} scene roots={roots.Length} objects={objects} " +
                          $"camera='{mainCamera.name}' rendererPresent={hasRenderer}");

                if (!apply)
                {
                    Debug.Log(hasRenderer
                        ? $"{Marker} WOULD verify compute/material assignment on the existing renderer"
                        : $"{Marker} WOULD ADD HectonMarineSnowRenderer to '{mainCamera.name}' with compute + material");
                    return;
                }

                if (!hasRenderer)
                    renderer = mainCamera.gameObject.AddComponent<HectonMarineSnowRenderer>();

                var serialized = new SerializedObject(renderer);
                bool changed = AssignIfNull(serialized, "marineSnowCompute", compute);
                changed |= AssignIfNull(serialized, "marineSnowMaterial", material);
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                if (!hasRenderer || changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"{Marker} SAVED - rendererAdded={!hasRenderer} referencesAssigned={changed}");
                }
                else
                {
                    Debug.Log($"{Marker} already wired, not writing");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Marker} FAILED {TargetScene}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool AssignIfNull(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"{Marker} field '{propertyName}' not found - renamed?");
                return false;
            }

            if (property.objectReferenceValue != null)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static Camera FindMainCamera(GameObject[] roots)
        {
            // Root traversal rather than Camera.main / a Find* API: those tokens are on the
            // project's forbidden list, and Camera.main is unreliable outside play mode anyway.
            foreach (GameObject root in roots)
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera"))
                        return camera;
                }
            }

            return null;
        }
    }
}
