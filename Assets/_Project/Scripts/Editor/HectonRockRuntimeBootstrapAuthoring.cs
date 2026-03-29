using System.Collections.Generic;
using GPUInstancer;
using Hecton8.Core;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class HectonRockRuntimeBootstrapAuthoring
    {
        private const string ManagersRootName = "[MANAGERS]";
        private const string RockRuntimeRootName = "Rock_Runtime";

        private static readonly string[] RockPrefabPaths =
        {
            "Assets/_Project/Prefabs/Nature/Rocks/Forest_Rock_Shelf.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Mossy_Forest_Rock.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock_Formation.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Rock_Skala.prefab"
        };

        [MenuItem("Hecton/Authoring/Rebuild Rock Runtime Stack", priority = 178)]
        public static void RebuildRockRuntimeStack()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[HectonRockRuntimeBootstrap] No active loaded scene.");
                return;
            }

            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            GameObject runtimeRoot = GameObject.Find($"{ManagersRootName}/{RockRuntimeRootName}");
            if (runtimeRoot == null)
            {
                runtimeRoot = new GameObject(RockRuntimeRootName);
                runtimeRoot.transform.SetParent(managersRoot.transform);
                runtimeRoot.transform.localPosition = Vector3.zero;
                runtimeRoot.transform.localRotation = Quaternion.identity;
                runtimeRoot.transform.localScale = Vector3.one;
            }

            GPUInstancerPrefabManager gpuiManager = GetOrAddComponent<GPUInstancerPrefabManager>(runtimeRoot);
            HectonRockManager rockManager = GetOrAddComponent<HectonRockManager>(runtimeRoot);
            ProximityColliderSystem proximityColliderSystem = Object.FindAnyObjectByType<ProximityColliderSystem>();

            List<GameObject> prefabAssets = EnsureRockPrefabsPrepared();
            if (prefabAssets.Count <= 0)
            {
                runtimeRoot.SetActive(false);
                Debug.LogWarning("[HectonRockRuntimeBootstrap] Rock runtime stack was created but disabled. Current rock shaders are not GPUI-ready yet.");
                EditorSceneManager.MarkSceneDirty(activeScene);
                return;
            }

            runtimeRoot.SetActive(true);
            ConfigureGPUInstancerManager(gpuiManager, prefabAssets);
            ConfigureRockManager(rockManager, gpuiManager, proximityColliderSystem, prefabAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[HectonRockRuntimeBootstrap] Rebuilt rock runtime stack with {prefabAssets.Count} prefabs.");
        }

        private static List<GameObject> EnsureRockPrefabsPrepared()
        {
            List<GameObject> prefabs = new List<GameObject>(RockPrefabPaths.Length);

            for (int i = 0; i < RockPrefabPaths.Length; i++)
            {
                string path = RockPrefabPaths[i];
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[HectonRockRuntimeBootstrap] Missing rock prefab: {path}");
                    continue;
                }

                GPUInstancerPrefab prefabComponent = prefabAsset.GetComponent<GPUInstancerPrefab>();
                if (prefabComponent == null)
                {
                    GPUInstancerUtility.GeneratePrefabPrototype(prefabAsset, false, true);
                    prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }

                if (!PrepareRockMaterialsForGPUI(prefabAsset))
                    continue;

                prefabs.Add(prefabAsset);
            }

            return prefabs;
        }

        private static bool PrepareRockMaterialsForGPUI(GameObject prefabAsset)
        {
            if (prefabAsset == null)
                return false;

            MeshRenderer[] renderers = prefabAsset.GetComponentsInChildren<MeshRenderer>(true);
            bool allShadersSupported = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                if (materials == null)
                    continue;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null || material.shader == null)
                        continue;

                    if (material.shader.name.StartsWith("Shader Graphs/"))
                    {
                        Debug.LogWarning($"[HectonRockRuntimeBootstrap] Skipping GPUI prep for '{prefabAsset.name}'. Shader Graph shader requires manual GPUI Setup: {material.shader.name}");
                        allShadersSupported = false;
                        continue;
                    }

                    bool setupOk = GPUInstancerAPI.SetupShaderForGPUI(material.shader);
                    if (!setupOk)
                    {
                        allShadersSupported = false;
                        continue;
                    }

                    EditorUtility.SetDirty(material);
                }
            }

            return allShadersSupported;
        }

        private static void ConfigureGPUInstancerManager(
            GPUInstancerPrefabManager gpuiManager,
            List<GameObject> prefabAssets)
        {
            SerializedObject so = new SerializedObject(gpuiManager);
            SerializedProperty prefabList = so.FindProperty("prefabList");
            if (prefabList != null)
            {
                prefabList.arraySize = prefabAssets.Count;
                for (int i = 0; i < prefabAssets.Count; i++)
                    prefabList.GetArrayElementAtIndex(i).objectReferenceValue = prefabAssets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            gpuiManager.GeneratePrototypes(false);
            EditorUtility.SetDirty(gpuiManager);
        }

        private static void ConfigureRockManager(
            HectonRockManager rockManager,
            GPUInstancerPrefabManager gpuiManager,
            ProximityColliderSystem proximityColliderSystem,
            List<GameObject> prefabAssets)
        {
            SerializedObject so = new SerializedObject(rockManager);
            so.FindProperty("gpuiManager").objectReferenceValue = gpuiManager;
            so.FindProperty("proximityColliderSystem").objectReferenceValue = proximityColliderSystem;
            so.FindProperty("maxExpectedInstances").intValue = 160000;

            SerializedProperty rockLayers = so.FindProperty("rockLayers");
            rockLayers.arraySize = prefabAssets.Count;

            for (int i = 0; i < prefabAssets.Count; i++)
            {
                GPUInstancerPrefab gpuiPrefab = prefabAssets[i] != null
                    ? prefabAssets[i].GetComponent<GPUInstancerPrefab>()
                    : null;

                SerializedProperty entry = rockLayers.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("layerId").intValue = i;
                entry.FindPropertyRelative("prefabReference").objectReferenceValue = gpuiPrefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rockManager);
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();

            return component;
        }
    }
}
