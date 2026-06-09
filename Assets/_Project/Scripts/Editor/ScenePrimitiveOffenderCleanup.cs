using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Automates the staged cleanup of primitive/slab offender MeshRenderers in the production scene.
    /// </summary>
    public static class ScenePrimitiveOffenderCleanup
    {
        private const string ProductionWorldScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        private static readonly string[] OffenderNames =
        {
            "H8_DEPTH_LOW_SHELF_1428",
            "H8_WORLD_LOW_WATER_OCCLUSION_00_1428",
            "H8_WORLD_LOW_WATER_OCCLUSION_01_1428",
            "H8_WORLD_LOW_WATER_OCCLUSION_02_1428",
            "H8_WORLD_LOW_WATER_OCCLUSION_03_1428",
            "H8_DEPTH_CEILING_OCCLUSION_1428",
            "NOIR_UPPER_PRESSURE_LID",
            "NOIR_LEFT_VIGNETTE_SLAB",
            "NOIR_RIGHT_VIGNETTE_SLAB"
        };

        [MenuItem("HECTON-8/Perform Primitive Offender Cleanup")]
        public static void RunMenu()
        {
            RunCleanup();
        }

        public static void RunCleanup()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.path != ProductionWorldScene)
            {
                currentScene = EditorSceneManager.OpenScene(ProductionWorldScene, OpenSceneMode.Single);
            }

            GameObject[] rootObjects = currentScene.GetRootGameObjects();
            int disabledCount = 0;

            foreach (string name in OffenderNames)
            {
                GameObject obj = null;
                foreach (GameObject root in rootObjects)
                {
                    obj = FindGameObjectInHierarchy(root.transform, name);
                    if (obj != null)
                        break;
                }

                if (obj != null)
                {
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        if (renderer.enabled)
                        {
                            renderer.enabled = false;
                            disabledCount++;
                            Debug.Log($"[Cleanup] Disabled MeshRenderer on offender object: {name}");
                        }
                        else
                        {
                            Debug.Log($"[Cleanup] MeshRenderer on offender object: {name} was already disabled.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Cleanup] Offender object: {name} found, but has no MeshRenderer component.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Cleanup] Offender object not found in scene: {name}");
                }
            }

            if (disabledCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(currentScene);
                bool saveSuccess = EditorSceneManager.SaveScene(currentScene);
                Debug.Log($"[Cleanup] Scene saved successfully: {saveSuccess}");
            }
            else
            {
                Debug.Log("[Cleanup] No active primitive offenders needed disabling.");
            }
        }

        private static GameObject FindGameObjectInHierarchy(Transform root, string name)
        {
            if (root.name == name)
                return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject result = FindGameObjectInHierarchy(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
