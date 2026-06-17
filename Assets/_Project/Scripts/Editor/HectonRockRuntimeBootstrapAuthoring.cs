using System;
using System.Collections.Generic;
using GPUInstancer;
using Hecton8.Core;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class HectonRockRuntimeBootstrapAuthoring
    {
        private const string ManagersRootName = "[MANAGERS]";
        private const string RockRuntimeRootName = "Rock_Runtime";
        private const string FloraFinalRootFolder = WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder;
        private const string KelpMasterShaderPath = "Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader";
        private const string KelpMasterGpuiShaderPath = "Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader";
        private const string CoralMasterShaderPath = "Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader";
        private const string CoralMasterGpuiShaderPath = "Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader";

        private static readonly string[] RockPrefabPaths =
        {
            "Assets/_Project/Prefabs/Nature/Rocks/Forest_Rock_Shelf.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Mossy_Forest_Rock.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock_Formation.prefab",
            "Assets/_Project/Prefabs/Nature/Rocks/Rock_Skala.prefab"
        };

        [MenuItem("Hecton8/Authoring/Rebuild Rock Runtime Stack", priority = 178)]
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
            ProximityColliderSystem proximityColliderSystem = UnityEngine.Object.FindAnyObjectByType<ProximityColliderSystem>();

            List<GameObject> rockPrefabs = EnsureRockPrefabsPrepared();
            List<GameObject> floraPrefabs = EnsureFloraPrefabsPrepared();
            List<GameObject> registeredPrefabs = CollectRegisteredPrefabs(rockPrefabs, floraPrefabs);
            if (registeredPrefabs.Count <= 0)
            {
                runtimeRoot.SetActive(false);
                rockManager.enabled = false;
                Debug.LogWarning("[HectonRockRuntimeBootstrap] GPUI runtime stack was created but disabled. No GPUI-ready rock or flora prefabs were found.");
                EditorSceneManager.MarkSceneDirty(activeScene);
                return;
            }

            runtimeRoot.SetActive(true);
            ConfigureGPUInstancerManager(gpuiManager, registeredPrefabs);
            ConfigureRockManager(rockManager, gpuiManager, proximityColliderSystem, rockPrefabs);
            rockManager.enabled = rockPrefabs.Count > 0;
            EditorUtility.SetDirty(rockManager);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[HectonRockRuntimeBootstrap] Rebuilt GPUI runtime stack. Rocks={rockPrefabs.Count}, Flora={floraPrefabs.Count}, Registered={registeredPrefabs.Count}, RockRuntimeEnabled={rockManager.enabled}.");
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

        private static List<GameObject> CollectRegisteredPrefabs(
            List<GameObject> rockPrefabs,
            List<GameObject> floraPrefabs)
        {
            int initialCapacity = rockPrefabs != null ? rockPrefabs.Count + 32 : 32; // COLD ALLOC: editor-only bootstrap list for combined rock/flora GPUI registration.
            List<GameObject> prefabs = new List<GameObject>(initialCapacity);
            HashSet<string> registeredPaths = new HashSet<string>(initialCapacity, StringComparer.Ordinal);

            AddUniquePrefabs(prefabs, registeredPaths, rockPrefabs);
            AddUniquePrefabs(prefabs, registeredPaths, floraPrefabs);

            return prefabs;
        }

        private static List<GameObject> EnsureFloraPrefabsPrepared()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FloraFinalRootFolder });
            List<GameObject> prefabs = new List<GameObject>(prefabGuids.Length); // COLD ALLOC: one editor bootstrap pass over baked flora prefabs.

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                    continue;

                if (prefabAsset.GetComponentInChildren<MeshRenderer>(true) == null)
                    continue;

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

        private static void AddUniquePrefabs(
            List<GameObject> destination,
            HashSet<string> registeredPaths,
            List<GameObject> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject prefab = source[i];
                if (prefab == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrWhiteSpace(assetPath) || !registeredPaths.Add(assetPath))
                    continue;

                destination.Add(prefab);
            }
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

                    if (TrySwapToFirstPartyFloraGpuiShader(material))
                    {
                        EditorUtility.SetDirty(material);
                        continue;
                    }

                    string shaderAssetPath = AssetDatabase.GetAssetPath(material.shader);
                    if (string.IsNullOrWhiteSpace(shaderAssetPath))
                    {
                        Debug.LogWarning($"[HectonRockRuntimeBootstrap] Skipping GPUI prep for '{prefabAsset.name}'. Shader asset path is empty: {material.shader.name}");
                        allShadersSupported = false;
                        continue;
                    }

                    if (material.shader.name.StartsWith("Shader Graphs/"))
                    {
                        Debug.LogWarning($"[HectonRockRuntimeBootstrap] Skipping GPUI prep for '{prefabAsset.name}'. Shader Graph shader requires manual GPUI Setup: {material.shader.name}");
                        allShadersSupported = false;
                        continue;
                    }

                    bool setupOk;
                    try
                    {
                        setupOk = GPUInstancerAPI.SetupShaderForGPUI(material.shader);
                    }
                    catch (ArgumentException ex)
                    {
                        Debug.LogWarning($"[HectonRockRuntimeBootstrap] GPUI shader prep failed for '{prefabAsset.name}' shader '{material.shader.name}': {ex.Message}");
                        allShadersSupported = false;
                        continue;
                    }

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

        private static bool TrySwapToFirstPartyFloraGpuiShader(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            if (string.Equals(shaderPath, KelpMasterGpuiShaderPath, StringComparison.Ordinal) ||
                string.Equals(shaderPath, CoralMasterGpuiShaderPath, StringComparison.Ordinal))
            {
                return true;
            }

            string gpuiPath = shaderPath switch
            {
                KelpMasterShaderPath => KelpMasterGpuiShaderPath,
                CoralMasterShaderPath => CoralMasterGpuiShaderPath,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(gpuiPath))
                return false;

            Shader gpuiShader = AssetDatabase.LoadAssetAtPath<Shader>(gpuiPath);
            if (gpuiShader == null)
                return false;

            material.shader = gpuiShader;
            return true;
        }

        private static void ConfigureGPUInstancerManager(
            GPUInstancerPrefabManager gpuiManager,
            List<GameObject> prefabAssets)
        {
            SerializedObject so = new SerializedObject(gpuiManager);
            SerializedProperty occlusionCulling = so.FindProperty("isOcclusionCulling");
            if (occlusionCulling != null)
                occlusionCulling.boolValue = false;

            SerializedProperty prefabList = so.FindProperty("prefabList");
            if (prefabList != null)
            {
                prefabList.arraySize = prefabAssets.Count;
                for (int i = 0; i < prefabAssets.Count; i++)
                    prefabList.GetArrayElementAtIndex(i).objectReferenceValue = prefabAssets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            gpuiManager.GeneratePrototypes(false);
            DisablePrototypeOcclusion(gpuiManager);
            RemoveHiZOcclusionGeneratorsFromSceneCameras();
            EditorUtility.SetDirty(gpuiManager);
        }

        private static void DisablePrototypeOcclusion(GPUInstancerPrefabManager gpuiManager)
        {
            if (gpuiManager == null || gpuiManager.prototypeList == null)
                return;

            for (int i = 0; i < gpuiManager.prototypeList.Count; i++)
            {
                GPUInstancerPrefabPrototype prototype = gpuiManager.prototypeList[i] as GPUInstancerPrefabPrototype;
                if (prototype == null)
                    continue;

                prototype.isOcclusionCulling = false;
                EditorUtility.SetDirty(prototype);
            }
        }

        private static void RemoveHiZOcclusionGeneratorsFromSceneCameras()
        {
            Camera[] sceneCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < sceneCameras.Length; i++)
            {
                GPUInstancerHiZOcclusionGenerator hiZ = sceneCameras[i] != null
                    ? sceneCameras[i].GetComponent<GPUInstancerHiZOcclusionGenerator>()
                    : null;

                if (hiZ == null)
                    continue;

                UnityEngine.Object.DestroyImmediate(hiZ);
                EditorUtility.SetDirty(sceneCameras[i]);
            }
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
