#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Scavenging;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    internal static class ResourceDistributionBootstrapAuthoring
    {
        private const string ManagersRootName = "__WORLD_MANAGERS";
        private const string ProductionWorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string ResourceNodeTemplateFolder = "Assets/_Project/Data/Scavenging/ResourceNodes";
        private const string RuntimeOrePrefabFolder = "Assets/_Project/Prefabs/Resources/Nodes";
        private const string RuntimeResourceMeshFolder = "Assets/_Project/Art/Meshes/Resources";
        private const string RuntimeOreMeshPath = RuntimeResourceMeshFolder + "/MESH_Runtime_OreNodule.asset";
        private const string RuntimeMagmaVentMeshPath = RuntimeResourceMeshFolder + "/MESH_Runtime_MagmaVentMarker.asset";
        private const string RuntimeOreMaterialPath = "Assets/_Project/Art/Materials/Resources/Mat_Runtime_OreGeneric.mat";
        private const string RuntimeMagmaVentMaterialPath = "Assets/_Project/Art/Materials/Resources/Mat_Runtime_MagmaVent.mat";
        private const string RuntimeOrePrefabPath = RuntimeOrePrefabFolder + "/PFB_Ore_Generic.prefab";
        private const string RuntimeMagmaVentPrefabPath = RuntimeOrePrefabFolder + "/PFB_Ore_MagmaVentMarker.prefab";
        private const string ThermalDiamondTemplateAssetPath = "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_ThermalDiamond.asset";
        private const string VoidGlassMeteoriteTemplateAssetPath = "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_VoidGlassMeteorite.asset";
        private const string PressureCarbonTemplateAssetPath = "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CarbonGraphiteNodule.asset";
        private const string PressureDiamondTemplateAssetPath = "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_PressureDiamond.asset";

        private static readonly string[] TemplateAssetPaths =
        {
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_TitaniumScrap.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SilicaShardCluster.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SilverVein.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SulfurVentClump.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_FiberKelpStand.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_HydrocarbonResinPod.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_MembraneTissueBloom.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_GoldVein.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CobaltAlloyNodule.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_RareEarthDustBed.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_ThermalGelPocket.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_NickelVein.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_LithiumCrystalCluster.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_AbyssalCrystalSpire.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_DeepMantleGeode.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_TitaniumBasaltMass.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_XenonOmegaVentCache.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_Silicon7BGlassVein.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_AegiriumCrustNodule.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_BrineIsotopeGeode.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CarbonGraphiteNodule.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CrystallizedOsmium.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_PressureDiamond.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_ThermalDiamond.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_ToxicSulfurDeposit.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_VoidGlassMeteorite.asset"
        };

        [MenuItem("Hecton8/World/Install Resource Distribution Director", priority = 230)]
        private static void Install()
        {
            if (!EnsureProductionWorldSceneLoaded())
                return;

            ResourceDistributionDirector director = Object.FindAnyObjectByType<ResourceDistributionDirector>(FindObjectsInactive.Include);
            GameObject managersRoot = director != null ? director.gameObject : EnsureManagersRoot();
            if (director == null)
            {
                if (!managersRoot.TryGetComponent(out ResourceDistributionDirector existingDirector))
                    director = Undo.AddComponent<ResourceDistributionDirector>(managersRoot);
                else
                    director = existingDirector;
            }

            GameObject orePrefab = CreateOrUpdateOreNodePrefab();
            GameObject magmaVentPrefab = CreateOrUpdateMagmaVentPrefab();
            EnsureScavengingLootOracleHost(managersRoot);

            SerializedObject serializedDirector = new SerializedObject(director);
            // Runtime player authority is spawned/registered after scene load; keep this empty so the director follows the live registry owner.
            AssignObject(serializedDirector, "playerTransform", null);
            AssignObject(serializedDirector, "mapMagicBridge", Object.FindAnyObjectByType<MapMagicBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "vegetationBridge", Object.FindAnyObjectByType<HectonMapMagicVegetationBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "voxelEngine", Object.FindAnyObjectByType<HectonVoxelEngine>(FindObjectsInactive.Include));
            AssignTemplates(serializedDirector.FindProperty("resourceTemplates"));
            AssignObject(serializedDirector, "thermalDiamondTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(ThermalDiamondTemplateAssetPath));
            AssignObject(serializedDirector, "voidGlassMeteoriteTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(VoidGlassMeteoriteTemplateAssetPath));
            AssignObject(serializedDirector, "pressureCarbonTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(PressureCarbonTemplateAssetPath));
            AssignObject(serializedDirector, "pressureDiamondTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(PressureDiamondTemplateAssetPath));
            AssignObject(serializedDirector, "_authoredOrePrefab", orePrefab);
            AssignObject(serializedDirector, "_authoredMagmaVentPrefab", magmaVentPrefab);
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(director);
            if (director.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            if (!Application.isBatchMode)
                Selection.activeObject = director.gameObject;
            Debug.Log("[ResourceDistributionBootstrap] RESULT: PASS — Director installed, generated resource assets saved, and scene marked dirty. Scene auto-save intentionally not performed.", director.gameObject);
        }

        private static bool EnsureProductionWorldSceneLoaded()
        {
            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            if (string.Equals(activeScene.path, ProductionWorldScenePath, StringComparison.OrdinalIgnoreCase))
                return true;

            string activeLabel = string.IsNullOrWhiteSpace(activeScene.path)
                ? activeScene.name
                : activeScene.path;

            // -executeMethod / CI: never open DisplayDialog; auto-load production world.
            bool batch = Application.isBatchMode;
            if (!batch)
            {
                if (!EditorUtility.DisplayDialog(
                        "Install Resource Distribution Director",
                        $"ResourceDistributionDirector must be installed in {ProductionWorldScenePath}.\n\nCurrent scene: {activeLabel}\n\nLoad the production world scene now?",
                        "Load World Scene",
                        "Cancel"))
                {
                    return false;
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return false;
            }
            else
            {
                Debug.Log("[ResourceDistributionBootstrap] Batchmode: auto-loading " + ProductionWorldScenePath);
            }

            if (!System.IO.File.Exists(ProductionWorldScenePath) &&
                !System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ProductionWorldScenePath)))
            {
                // Soft FAIL under -quit: LogError + exit 0.
                Debug.LogError("[ResourceDistributionBootstrap] RESULT: FAIL — Missing scene: " + ProductionWorldScenePath);
                if (batch)
                    return false;
            }

            UnityEngine.SceneManagement.Scene openedScene = EditorSceneManager.OpenScene(
                ProductionWorldScenePath,
                OpenSceneMode.Single);
            bool ok = openedScene.IsValid() &&
                   string.Equals(openedScene.path, ProductionWorldScenePath, StringComparison.OrdinalIgnoreCase);
            if (!ok)
                Debug.LogError("[ResourceDistributionBootstrap] RESULT: FAIL — Could not open " + ProductionWorldScenePath);
            else if (batch)
                Debug.Log("[ResourceDistributionBootstrap] RESULT: PASS — Opened " + ProductionWorldScenePath);
            return ok;
        }

        private static GameObject EnsureManagersRoot()
        {
            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot != null)
                return managersRoot;

            managersRoot = new GameObject(ManagersRootName);
            Undo.RegisterCreatedObjectUndo(managersRoot, "Create world managers root");
            return managersRoot;
        }

        private static void EnsureScavengingLootOracleHost(GameObject managersRoot)
        {
            ScavengingLootOracleRuntime oracle = Object.FindAnyObjectByType<ScavengingLootOracleRuntime>(FindObjectsInactive.Include);
            if (oracle != null || managersRoot == null)
                return;

            Undo.AddComponent<ScavengingLootOracleRuntime>(managersRoot);
        }

        private static GameObject CreateOrUpdateOreNodePrefab()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Meshes");
            EnsureFolder(RuntimeResourceMeshFolder);
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Resources");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Resources");
            EnsureFolder(RuntimeOrePrefabFolder);

            Mesh mesh = CreateOrUpdateOreNoduleMesh(RuntimeOreMeshPath);
            Material material = CreateOrUpdateMaterial(RuntimeOreMaterialPath, new Color(0.78f, 0.47f, 0.22f, 1f));
            GameObject root = new GameObject("PFB_Ore_Generic");
            root.name = "PFB_Ore_Generic";
            root.layer = HectonLayerMasks.Default;
            root.transform.localScale = Vector3.one;
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            BoxCollider boxCollider = root.AddComponent<BoxCollider>();
            boxCollider.isTrigger = false;

            SphereCollider sphereCollider = root.AddComponent<SphereCollider>();
            sphereCollider.enabled = false;
            sphereCollider.isTrigger = false;
            sphereCollider.radius = 0.5f;

            ResourceNode node = root.AddComponent<ResourceNode>();
            SerializedObject serializedNode = new SerializedObject(node);
            serializedNode.FindProperty("autoGenerateId").boolValue = true;
            serializedNode.FindProperty("lootLifetime").floatValue = 90f;
            serializedNode.FindProperty("scatterRadius").floatValue = 0.22f;
            serializedNode.FindProperty("scatterForce").floatValue = 1.4f;
            serializedNode.FindProperty("upwardBias").floatValue = 0.7f;
            serializedNode.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                return SavePrefabAssetOrThrow(root, RuntimeOrePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateOrUpdateMagmaVentPrefab()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Meshes");
            EnsureFolder(RuntimeResourceMeshFolder);
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Resources");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Resources");
            EnsureFolder(RuntimeOrePrefabFolder);

            Mesh mesh = CreateOrUpdateMagmaVentMesh(RuntimeMagmaVentMeshPath);
            Material material = CreateOrUpdateMaterial(RuntimeMagmaVentMaterialPath, new Color(1f, 0.42f, 0.12f, 0.72f));
            GameObject root = new GameObject("PFB_Ore_MagmaVentMarker");
            root.name = "PFB_Ore_MagmaVentMarker";
            root.layer = HectonLayerMasks.Default;
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            try
            {
                return SavePrefabAssetOrThrow(root, RuntimeMagmaVentPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh CreateOrUpdateOreNoduleMesh(string path)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "MESH_Runtime_OreNodule" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            Vector3[] vertices =
            {
                new Vector3(-0.06f, 0.58f, 0.08f),
                new Vector3(0.62f, 0.04f, 0.00f),
                new Vector3(0.38f, 0.11f, 0.52f),
                new Vector3(-0.04f, -0.03f, 0.68f),
                new Vector3(-0.48f, 0.08f, 0.44f),
                new Vector3(-0.66f, -0.05f, -0.03f),
                new Vector3(-0.35f, 0.02f, -0.56f),
                new Vector3(0.09f, -0.08f, -0.64f),
                new Vector3(0.50f, 0.07f, -0.36f),
                new Vector3(0.02f, -0.50f, -0.04f)
            };

            int[] triangles = new int[48];
            int writeIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                int current = 1 + i;
                int next = 1 + ((i + 1) & 7);
                triangles[writeIndex++] = 0;
                triangles[writeIndex++] = next;
                triangles[writeIndex++] = current;
                triangles[writeIndex++] = 9;
                triangles[writeIndex++] = current;
                triangles[writeIndex++] = next;
            }

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].z + 0.5f);

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh CreateOrUpdateMagmaVentMesh(string path)
        {
            const int SegmentCount = 16;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "MESH_Runtime_MagmaVentMarker" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            Vector3[] vertices = new Vector3[(SegmentCount * 2) + 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int topCenterIndex = SegmentCount * 2;
            int bottomCenterIndex = topCenterIndex + 1;
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / SegmentCount;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                vertices[i] = new Vector3(cos * 0.54f, 0f, sin * 0.54f);
                vertices[SegmentCount + i] = new Vector3(cos * 0.18f, 1f, sin * 0.18f);
                uvs[i] = new Vector2((float)i / SegmentCount, 0f);
                uvs[SegmentCount + i] = new Vector2((float)i / SegmentCount, 1f);
            }

            vertices[topCenterIndex] = new Vector3(0f, 1f, 0f);
            vertices[bottomCenterIndex] = Vector3.zero;
            uvs[topCenterIndex] = new Vector2(0.5f, 1f);
            uvs[bottomCenterIndex] = new Vector2(0.5f, 0f);

            int[] triangles = new int[SegmentCount * 12];
            int writeIndex = 0;
            for (int i = 0; i < SegmentCount; i++)
            {
                int next = (i + 1) % SegmentCount;
                int bottomCurrent = i;
                int bottomNext = next;
                int topCurrent = SegmentCount + i;
                int topNext = SegmentCount + next;

                triangles[writeIndex++] = bottomCurrent;
                triangles[writeIndex++] = topCurrent;
                triangles[writeIndex++] = topNext;
                triangles[writeIndex++] = bottomCurrent;
                triangles[writeIndex++] = topNext;
                triangles[writeIndex++] = bottomNext;

                triangles[writeIndex++] = topCenterIndex;
                triangles[writeIndex++] = topCurrent;
                triangles[writeIndex++] = topNext;

                triangles[writeIndex++] = bottomCenterIndex;
                triangles[writeIndex++] = bottomNext;
                triangles[writeIndex++] = bottomCurrent;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = ResolveVisibleRuntimeShader();
                if (shader == null)
                    throw new InvalidOperationException("No visible runtime shader found for generated resource distribution material.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader ResolveVisibleRuntimeShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ??
                   Shader.Find("Standard") ??
                   Shader.Find("Sprites/Default");
        }

        private static void AssignTemplates(SerializedProperty templatesProperty)
        {
            if (templatesProperty == null || !templatesProperty.isArray)
                return;

            string[] discoveredGuids = AssetDatabase.FindAssets("t:ResourceNodeTemplate", new[] { ResourceNodeTemplateFolder });
            // COLD ALLOC: List<ResourceNodeTemplate>[TemplateAssetPaths.Length + discoveredGuids.Length] — editor bootstrap template load buffer — owner: ResourceDistributionBootstrapAuthoring
            List<ResourceNodeTemplate> templates = new List<ResourceNodeTemplate>(TemplateAssetPaths.Length + discoveredGuids.Length);
            HashSet<string> assignedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < TemplateAssetPaths.Length; i++)
                AddTemplateIfAvailable(templates, assignedPaths, TemplateAssetPaths[i]);

            List<string> discoveredPaths = new List<string>(discoveredGuids.Length);
            for (int i = 0; i < discoveredGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(discoveredGuids[i]);
                if (!string.IsNullOrWhiteSpace(assetPath))
                    discoveredPaths.Add(assetPath);
            }

            discoveredPaths.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < discoveredPaths.Count; i++)
                AddTemplateIfAvailable(templates, assignedPaths, discoveredPaths[i]);

            templatesProperty.arraySize = templates.Count;
            for (int i = 0; i < templates.Count; i++)
                templatesProperty.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
        }

        private static void AddTemplateIfAvailable(
            List<ResourceNodeTemplate> templates,
            HashSet<string> assignedPaths,
            string assetPath)
        {
            if (templates == null ||
                assignedPaths == null ||
                string.IsNullOrWhiteSpace(assetPath) ||
                !assignedPaths.Add(assetPath))
            {
                return;
            }

            ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(assetPath);
            if (template != null)
                templates.Add(template);
        }

        private static void AssignObject(SerializedObject target, string propertyName, Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static GameObject SavePrefabAssetOrThrow(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
                throw new InvalidOperationException($"Failed to save generated resource prefab at '{path}'.");

            return prefab;
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
#endif
