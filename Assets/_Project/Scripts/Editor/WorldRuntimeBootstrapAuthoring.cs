using Hecton8.Core;
using Hecton8.World;
using Hecton8.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldRuntimeBootstrapAuthoring
    {
        private const string RuntimePrefabFolder = "Assets/_Project/Prefabs/WorldRuntime";
        private const string ColliderProxyPrefabPath = RuntimePrefabFolder + "/PFB_ProximityColliderProxy.prefab";
        private const string ManagersRootName = "[MANAGERS]";

        [MenuItem("Hecton/Authoring/Rebuild World Runtime Stack", priority = 177)]
        public static void RebuildWorldRuntimeStack()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(RuntimePrefabFolder);

            GameObject colliderPrefab = CreateOrUpdateColliderProxyPrefab();
            if (colliderPrefab == null)
            {
                Debug.LogError("[WorldRuntimeBootstrap] Failed to create collider proxy prefab.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldRuntimeBootstrap] No active loaded scene.");
                return;
            }

            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            Transform playerTransform = player != null ? player.transform : null;
            Rigidbody playerBody = player != null ? player.GetComponent<Rigidbody>() : null;

            MapMagicBridge bridge = FindSceneObjectIncludingInactive<MapMagicBridge>();
            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            ObjectPoolManager objectPoolManager = FindSceneObjectIncludingInactive<ObjectPoolManager>();

            BiomeSamplerCache biomeCache = GetOrAddComponent<BiomeSamplerCache>(managersRoot);
            ScatterBudgetController scatterBudgetController = GetOrAddComponent<ScatterBudgetController>(managersRoot);
            WorldStreamingDirector streamingDirector = GetOrAddComponent<WorldStreamingDirector>(managersRoot);
            WorldSliceDirector sliceDirector = GetOrAddComponent<WorldSliceDirector>(managersRoot);
            WorldInterestDirector interestDirector = GetOrAddComponent<WorldInterestDirector>(managersRoot);
            ProximityColliderSystem proximityColliderSystem = GetOrAddComponent<ProximityColliderSystem>(managersRoot);

            ConfigureBiomeSamplerCache(biomeCache, bridge, playerTransform);
            ConfigureProximityColliderSystem(proximityColliderSystem, playerTransform, colliderPrefab);
            ConfigureScatterBudgetController(
                scatterBudgetController,
                playerTransform,
                bridge,
                scavengePopulator,
                proximityColliderSystem,
                biomeCache);
            ConfigureWorldStreamingDirector(
                streamingDirector,
                playerTransform,
                playerBody,
                bridge,
                biomeCache,
                scatterBudgetController);
            ConfigureWorldSliceDirector(sliceDirector, playerTransform);
            ConfigureWorldInterestDirector(interestDirector, playerTransform, scatterBudgetController);
            ConfigureSceneSlices();
            ConfigureSceneInterestAnchors();

            if (objectPoolManager != null)
                EnsureWarmupPreset(objectPoolManager, colliderPrefab, 192);
            else
                Debug.LogWarning("[WorldRuntimeBootstrap] ObjectPoolManager not found. Collider proxy warmup was skipped.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[WorldRuntimeBootstrap] World runtime stack rebuilt.");
        }

        private static void ConfigureBiomeSamplerCache(
            BiomeSamplerCache biomeCache,
            MapMagicBridge bridge,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(biomeCache);
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biomeCache);
        }

        private static void ConfigureProximityColliderSystem(
            ProximityColliderSystem proximityColliderSystem,
            Transform playerTransform,
            GameObject colliderPrefab)
        {
            SerializedObject so = new SerializedObject(proximityColliderSystem);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("colliderPrefab").objectReferenceValue = colliderPrefab;
            so.FindProperty("activateRadius").floatValue = 42f;
            so.FindProperty("deactivateRadius").floatValue = 48f;
            so.FindProperty("maxOperationsPerTick").intValue = 64;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(proximityColliderSystem);
        }

        private static void ConfigureScatterBudgetController(
            ScatterBudgetController controller,
            Transform playerTransform,
            MapMagicBridge bridge,
            ScavengePopulator scavengePopulator,
            ProximityColliderSystem proximityColliderSystem,
            BiomeSamplerCache biomeCache)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("scavengePopulator").objectReferenceValue = scavengePopulator;
            so.FindProperty("proximityColliderSystem").objectReferenceValue = proximityColliderSystem;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureWorldStreamingDirector(
            WorldStreamingDirector director,
            Transform playerTransform,
            Rigidbody playerBody,
            MapMagicBridge bridge,
            BiomeSamplerCache biomeCache,
            ScatterBudgetController scatterBudgetController)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("playerRigidbody").objectReferenceValue = playerBody;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            so.FindProperty("scatterBudgetController").objectReferenceValue = scatterBudgetController;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldSliceDirector(
            WorldSliceDirector director,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldInterestDirector(
            WorldInterestDirector director,
            Transform playerTransform,
            ScatterBudgetController scatterBudgetController)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("scatterBudgetController").objectReferenceValue = scatterBudgetController;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureSceneSlices()
        {
            ConfigureResourceFieldSlice();
            ConfigureFabricationOutpostSlice();
            ConfigureFabricationTrialSlice();
            ConfigureToolStagingSlice();
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps", 68f, 128f, 18f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps", 72f, 138f, 18f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps", 84f, 154f, 20f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_CombatContacts", 70f, 134f, 18f);
        }

        private static void ConfigureSceneInterestAnchors()
        {
            ConfigureInterestAnchor(
                "--- WORLD ---/Resource_FieldSources",
                WorldInterestAnchor.InterestKind.ResourceField,
                78f,
                190f,
                1.18f,
                1.16f,
                1.1f,
                1.08f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Fabrication_Outpost",
                WorldInterestAnchor.InterestKind.Fabrication,
                72f,
                165f,
                1.08f,
                1.04f,
                1.16f,
                1.12f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange",
                WorldInterestAnchor.InterestKind.ToolRange,
                95f,
                220f,
                1.24f,
                1.22f,
                1.18f,
                1.18f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps",
                WorldInterestAnchor.InterestKind.Construction,
                56f,
                132f,
                1.1f,
                1.08f,
                1.12f,
                1.12f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps",
                WorldInterestAnchor.InterestKind.Power,
                60f,
                140f,
                1.12f,
                1.1f,
                1.14f,
                1.12f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps",
                WorldInterestAnchor.InterestKind.ProgressionHub,
                72f,
                164f,
                1.18f,
                1.14f,
                1.14f,
                1.12f);
        }

        private static void ConfigureResourceFieldSlice()
        {
            GameObject root = GameObject.Find("--- WORLD ---/Resource_FieldSources");
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 180f;
            so.FindProperty("midDistance").floatValue = 320f;
            so.FindProperty("hysteresisPadding").floatValue = 28f;
            AssignChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureFabricationOutpostSlice()
        {
            GameObject root = GameObject.Find("--- WORLD ---/Fabrication_Outpost");
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 120f;
            so.FindProperty("midDistance").floatValue = 260f;
            so.FindProperty("hysteresisPadding").floatValue = 24f;
            ClearObjectArray(so.FindProperty("nearOnlyRoots"));
            AssignChildrenToRoots(so.FindProperty("midAndNearRoots"), root.transform);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureFabricationTrialSlice()
        {
            GameObject root = GameObject.Find("Fabrication_Trial");
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 100f;
            so.FindProperty("midDistance").floatValue = 210f;
            so.FindProperty("hysteresisPadding").floatValue = 22f;
            AssignChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureToolStagingSlice()
        {
            GameObject root = GameObject.Find("Tool_Staging");
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 110f;
            so.FindProperty("midDistance").floatValue = 190f;
            so.FindProperty("hysteresisPadding").floatValue = 20f;
            AssignChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));

            SerializedProperty nearBehaviours = so.FindProperty("nearOnlyBehaviours");
            nearBehaviours.arraySize = 1;
            nearBehaviours.GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<ToolStagingSpawner>();

            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureToolTrialLaneSlice(
            string lanePath,
            float nearDistance,
            float midDistance,
            float hysteresisPadding)
        {
            GameObject root = GameObject.Find(lanePath);
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = nearDistance;
            so.FindProperty("midDistance").floatValue = midDistance;
            so.FindProperty("hysteresisPadding").floatValue = hysteresisPadding;
            AssignChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureInterestAnchor(
            string objectPath,
            WorldInterestAnchor.InterestKind kind,
            float fullRadius,
            float falloffRadius,
            float scavengeScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            GameObject root = GameObject.Find(objectPath);
            if (root == null)
                return;

            WorldInterestAnchor anchor = GetOrAddComponent<WorldInterestAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("interestKind").enumValueIndex = (int)kind;
            so.FindProperty("fullInfluenceRadius").floatValue = fullRadius;
            so.FindProperty("falloffRadius").floatValue = falloffRadius;
            so.FindProperty("scavengeRadiusScale").floatValue = scavengeScale;
            so.FindProperty("spawnScale").floatValue = spawnScale;
            so.FindProperty("colliderRadiusScale").floatValue = colliderRadiusScale;
            so.FindProperty("colliderOpsScale").floatValue = colliderOpsScale;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void AssignChildrenToRoots(SerializedProperty arrayProperty, Transform parent)
        {
            if (arrayProperty == null)
                return;

            arrayProperty.arraySize = parent.childCount;
            for (int i = 0; i < parent.childCount; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = parent.GetChild(i).gameObject;
            }
        }

        private static void ClearObjectArray(SerializedProperty arrayProperty)
        {
            if (arrayProperty != null)
                arrayProperty.arraySize = 0;
        }

        private static void ClearBehaviourArray(SerializedProperty arrayProperty)
        {
            if (arrayProperty != null)
                arrayProperty.arraySize = 0;
        }

        private static void EnsureWarmupPreset(ObjectPoolManager objectPoolManager, GameObject prefab, int count)
        {
            SerializedObject so = new SerializedObject(objectPoolManager);
            SerializedProperty presets = so.FindProperty("warmupPresets");
            if (presets == null)
                return;

            for (int i = 0; i < presets.arraySize; i++)
            {
                SerializedProperty entry = presets.GetArrayElementAtIndex(i);
                SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
                SerializedProperty countProp = entry.FindPropertyRelative("count");
                if (prefabProp == null || countProp == null)
                    continue;

                if (prefabProp.objectReferenceValue == prefab)
                {
                    countProp.intValue = Mathf.Max(countProp.intValue, count);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(objectPoolManager);
                    return;
                }
            }

            int newIndex = presets.arraySize;
            presets.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = presets.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            newEntry.FindPropertyRelative("count").intValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(objectPoolManager);
        }

        private static GameObject CreateOrUpdateColliderProxyPrefab()
        {
            GameObject root = new GameObject("PFB_ProximityColliderProxy");
            root.layer = 0;
            root.tag = "Untagged";

            BoxCollider boxCollider = root.AddComponent<BoxCollider>();
            boxCollider.center = new Vector3(0f, 0.15f, 0f);
            boxCollider.size = new Vector3(2.8f, 2.4f, 2.8f);
            boxCollider.isTrigger = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ColliderProxyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();

            return component;
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] split = folderPath.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);

                current = next;
            }
        }
    }
}
