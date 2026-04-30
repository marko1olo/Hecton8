#if UNITY_EDITOR
using System.Collections.Generic;
using Hecton8.Core;
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
            ResourceDistributionDirector director = Object.FindAnyObjectByType<ResourceDistributionDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                GameObject managersRoot = GameObject.Find(ManagersRootName);
                if (managersRoot == null)
                    managersRoot = new GameObject(ManagersRootName);

                director = managersRoot.GetComponent<ResourceDistributionDirector>();
                if (director == null)
                    director = Undo.AddComponent<ResourceDistributionDirector>(managersRoot);
            }

            SerializedObject serializedDirector = new SerializedObject(director);
            AssignObject(serializedDirector, "playerTransform", ResolveSceneObject<Transform>("Player"));
            AssignObject(serializedDirector, "mapMagicBridge", Object.FindAnyObjectByType<MapMagicBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "vegetationBridge", Object.FindAnyObjectByType<HectonMapMagicVegetationBridge>(FindObjectsInactive.Include));
            AssignObject(serializedDirector, "voxelEngine", Object.FindAnyObjectByType<HectonVoxelEngine>(FindObjectsInactive.Include));
            AssignTemplates(serializedDirector.FindProperty("resourceTemplates"));
            AssignObject(serializedDirector, "thermalDiamondTemplate", AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(ThermalDiamondTemplateAssetPath));
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(director);
            if (director.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Selection.activeObject = director.gameObject;
            Debug.Log("[ResourceDistributionBootstrap] Director installed and scene marked dirty. Auto-save intentionally not performed.", director.gameObject);
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
    }
}
#endif
