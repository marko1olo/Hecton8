using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class PrefabMaintenanceTool
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ReserializePlayerMenuPath = "Tools/HECTON-8/Maintenance/Reserialize Player Prefab";

        [MenuItem(ReserializePlayerMenuPath)]
        private static void ReserializePlayerPrefab()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[PrefabMaintenanceTool] Player prefab reserialize skipped because the editor is compiling or updating.");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("[PrefabMaintenanceTool] Failed to load Player prefab contents.");
                return;
            }

            int dirtyCount = 0;

            try
            {
                Camera[] cameras = prefabRoot.GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cameras.Length; i++)
                {
                    EditorUtility.SetDirty(cameras[i]);
                    dirtyCount++;
                }

                Light[] lights = prefabRoot.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    EditorUtility.SetDirty(lights[i]);
                    dirtyCount++;
                }

                Component[] components = prefabRoot.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                        continue;

                    string fullName = component.GetType().FullName;
                    if (string.IsNullOrEmpty(fullName))
                        continue;

                    if (fullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData" ||
                        fullName == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData")
                    {
                        EditorUtility.SetDirty(component);
                        dirtyCount++;
                    }
                }

                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            Debug.Log("[PrefabMaintenanceTool] Reserialized Player prefab. dirtyObjects=" + dirtyCount);
        }
    }
}
