#if UNITY_EDITOR
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.EditorTools;
using Hecton8.Scavenging;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class ResourceDistributionBootstrapAuthoring
    {
        private const string ManagersRootName = "__WORLD_MANAGERS";
        private const string RuntimeOrePrefabFolder = "Assets/_Project/Prefabs/Resources/Nodes";
        private const string RuntimeOreMaterialPath = "Assets/_Project/Art/Materials/Resources/Mat_Runtime_OreGeneric.mat";
        private const string RuntimeMagmaVentMaterialPath = "Assets/_Project/Art/Materials/Resources/Mat_Runtime_MagmaVent.mat";
        private const string RuntimeOrePrefabPath = RuntimeOrePrefabFolder + "/PFB_Ore_Generic.prefab";
        private const string RuntimeMagmaVentPrefabPath = RuntimeOrePrefabFolder + "/PFB_Ore_MagmaVentMarker.prefab";
        private const string ThermalDiamondTemplateAssetPath = "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_ThermalDiamond.asset";

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
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_AegiriumCrustNodule.asset"
        };

        [MenuItem("HECTON-8/World/Install Resource Distribution Director", priority = 230)]
        private static void Install()
        {
            if (!WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveProductionAuthoring(
                    nameof(ResourceDistributionBootstrapAuthoring),
                    RuntimeOrePrefabFolder))
                return;

            ResourceDistributionDirector director = Object.FindAnyObjectByType<ResourceDistributionDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                GameObject managersRoot = EnsureManagersRoot();

                if (!managersRoot.TryGetComponent(out ResourceDistributionDirector existingDirector))
                    director = Undo.AddComponent<ResourceDistributionDirector>(managersRoot);
                else
                    director = existingDirector;
            }

            GameObject orePrefab = CreateOrUpdateOreNodePrefab();
            GameObject magmaVentPrefab = CreateOrUpdateMagmaVentPrefab();
            EnsureScavengingLootOracleHost(EnsureManagersRoot());

            SerializedObject serializedDirector = new SerializedObject(director);
            AssignObject(serializedDirector, "playerTransform", ResolveSceneObject<Transform>("Player"));
            AssignObject(serializedDirector, "mapMagicBridge", Object.FindAnyObjectByType<MapMagicBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "vegetationBridge", Object.FindAnyObjectByType<HectonMapMagicVegetationBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "voxelEngine", Object.FindAnyObjectByType<HectonVoxelEngine>(FindObjectsInactive.Include));
            AssignTemplates(serializedDirector.FindProperty("resourceTemplates"));
            AssignObject(serializedDirector, "thermalDiamondTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(ThermalDiamondTemplateAssetPath));
            AssignObject(serializedDirector, "_authoredOrePrefab", orePrefab);
            AssignObject(serializedDirector, "_authoredMagmaVentPrefab", magmaVentPrefab);
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(director);
            if (director.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Selection.activeObject = director.gameObject;
            Debug.Log("[ResourceDistributionBootstrap] Director installed and scene marked dirty. Auto-save intentionally not performed.", director.gameObject);
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
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Resources");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Resources");
            EnsureFolder(RuntimeOrePrefabFolder);

            Material material = CreateOrUpdateMaterial(RuntimeOreMaterialPath, new Color(0.78f, 0.47f, 0.22f, 1f));
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PFB_Ore_Generic";
            root.transform.localScale = Vector3.one;
            if (root.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            SphereCollider sphereCollider = root.AddComponent<SphereCollider>();
            sphereCollider.enabled = false;
            sphereCollider.radius = 0.5f;

            ResourceNode node = root.AddComponent<ResourceNode>();
            SerializedObject serializedNode = new SerializedObject(node);
            serializedNode.FindProperty("autoGenerateId").boolValue = true;
            serializedNode.FindProperty("lootLifetime").floatValue = 90f;
            serializedNode.FindProperty("scatterRadius").floatValue = 0.22f;
            serializedNode.FindProperty("scatterForce").floatValue = 1.4f;
            serializedNode.FindProperty("upwardBias").floatValue = 0.7f;
            serializedNode.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RuntimeOrePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrUpdateMagmaVentPrefab()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Resources");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Resources");
            EnsureFolder(RuntimeOrePrefabFolder);

            Material material = CreateOrUpdateMaterial(RuntimeMagmaVentMaterialPath, new Color(1f, 0.42f, 0.12f, 0.72f));
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "PFB_Ore_MagmaVentMarker";
            if (root.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RuntimeMagmaVentPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

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

        private static void AssignTemplates(SerializedProperty templatesProperty)
        {
            if (templatesProperty == null || !templatesProperty.isArray)
                return;

            // COLD ALLOC: List<ResourceNodeTemplate>[TemplateAssetPaths.Length] — editor bootstrap template load buffer — owner: ResourceDistributionBootstrapAuthoring
            List<ResourceNodeTemplate> templates = new List<ResourceNodeTemplate>(TemplateAssetPaths.Length);
            for (int i = 0; i < TemplateAssetPaths.Length; i++)
            {
                ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(TemplateAssetPaths[i]);
                if (template != null)
                    templates.Add(template);
            }

            templatesProperty.arraySize = templates.Count;
            for (int i = 0; i < templates.Count; i++)
                templatesProperty.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
        }

        private static void AssignObject(SerializedObject target, string propertyName, Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static T ResolveSceneObject<T>(string name) where T : Component
        {
            T[] candidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate != null && candidate.name == name)
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
#endif
