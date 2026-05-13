using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class ScreenSpaceLightShaftPrefabRepair
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string RuntimeTypeName = "Hecton8.Lighting.Shafts.ScreenSpaceLightShaftRuntime, Hecton8.Lighting.Shafts";
        private const string SourceTypeName = "Hecton8.Lighting.Shafts.ScreenSpaceLightShaftSource, Hecton8.Lighting.Shafts";
        private const string FlashlightObjectName = "DiveLamp_Light";
        private const string ShallowSunObjectName = "Underwater_ShallowSunBeam";

        public static void Run()
        {
            Type runtimeType = Type.GetType(RuntimeTypeName);
            Type sourceType = Type.GetType(SourceTypeName);
            if (runtimeType == null || sourceType == null)
                throw new InvalidOperationException("Light shaft types are not compiled.");

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
                throw new InvalidOperationException("Failed to load Player prefab.");

            try
            {
                int removed = RemoveVlbComponents(prefabRoot);
                int added = EnsureLightShaftComponents(prefabRoot, runtimeType, sourceType);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                Debug.Log("[ScreenSpaceLightShaftPrefabRepair] removedVlb=" + removed + " addedShaftComponents=" + added);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static int RemoveVlbComponents(GameObject prefabRoot)
        {
            int removed = 0;
            Component[] components = prefabRoot.GetComponentsInChildren<Component>(true);
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                string fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName) && fullName.StartsWith("VLB.", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                    removed++;
                }
            }

            return removed;
        }

        private static int EnsureLightShaftComponents(GameObject prefabRoot, Type runtimeType, Type sourceType)
        {
            int added = 0;
            if (prefabRoot.GetComponent(runtimeType) == null)
            {
                prefabRoot.AddComponent(runtimeType);
                added++;
            }

            added += EnsureSourceOnNamedLight(prefabRoot, FlashlightObjectName, sourceType);
            added += EnsureSourceOnNamedLight(prefabRoot, ShallowSunObjectName, sourceType);
            return added;
        }

        private static int EnsureSourceOnNamedLight(GameObject prefabRoot, string objectName, Type sourceType)
        {
            Transform target = FindChildByName(prefabRoot.transform, objectName);
            if (target == null || target.GetComponent<Light>() == null || target.GetComponent(sourceType) != null)
                return 0;

            target.gameObject.AddComponent(sourceType);
            return 1;
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root == null)
                return null;

            if (root.name == objectName)
                return root;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
