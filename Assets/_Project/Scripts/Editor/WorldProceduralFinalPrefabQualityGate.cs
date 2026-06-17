using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class WorldProceduralFinalPrefabQualityGate
    {
        private const string BuiltInPrimitiveMeshGuid = "0000000000000000e000000000000000";

        public static bool UsesUnityBuiltInPrimitiveMesh(GameObject prefab)
        {
            if (prefab == null)
                return false;

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabPath))
                return false;

            return AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath);
        }

        public static bool AssetPathUsesUnityBuiltInPrimitiveMesh(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string absolutePrefabPath = ResolveProjectRelativePath(assetPath);
            if (!File.Exists(absolutePrefabPath))
                return false;

            string prefabText = File.ReadAllText(absolutePrefabPath);
            return prefabText.IndexOf(BuiltInPrimitiveMeshGuid, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveProjectRelativePath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith(@"Assets\", System.StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo dataDirectory = new DirectoryInfo(Application.dataPath);
                DirectoryInfo projectDirectory = dataDirectory.Parent;
                if (projectDirectory != null)
                    return Path.Combine(projectDirectory.FullName, assetPath);
            }

            return Path.GetFullPath(assetPath);
        }

        public static bool AllowLegacyPrimitiveFinalAuthoring(string ownerLabel, string targetFolder)
        {
            Debug.LogError(
                $"[{ownerLabel}] Legacy primitive final authoring is blocked for '{targetFolder}'. "
                + "Production final prefabs must use authored/generated production meshes, real materials, LODs, and proof artifacts.");
            return false;
        }

        public static bool AllowLegacyPrimitiveProductionAuthoring(string ownerLabel, string targetFolder)
        {
            Debug.LogError(
                $"[{ownerLabel}] Legacy primitive production authoring is blocked for '{targetFolder}'. "
                + "Production-visible prefabs must use authored/generated meshes, real materials, LODs, and proof artifacts.");
            return false;
        }
    }
}
