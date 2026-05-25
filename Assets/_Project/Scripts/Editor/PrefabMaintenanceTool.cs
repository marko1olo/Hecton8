using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class PrefabMaintenanceTool
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ReserializePlayerMenuPath = "Tools/HECTON-8/Maintenance/Reserialize Player Prefab";
        private static readonly List<Camera> s_CameraScratch = new List<Camera>(8);
        private static readonly List<Light> s_LightScratch = new List<Light>(16);
        private static readonly List<Component> s_ComponentScratch = new List<Component>(128);

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
                s_CameraScratch.Clear();
                prefabRoot.GetComponentsInChildren(true, s_CameraScratch);
                for (int i = 0; i < s_CameraScratch.Count; i++)
                {
                    EditorUtility.SetDirty(s_CameraScratch[i]);
                    dirtyCount++;
                }

                s_LightScratch.Clear();
                prefabRoot.GetComponentsInChildren(true, s_LightScratch);
                for (int i = 0; i < s_LightScratch.Count; i++)
                {
                    EditorUtility.SetDirty(s_LightScratch[i]);
                    dirtyCount++;
                }

                s_ComponentScratch.Clear();
                prefabRoot.GetComponentsInChildren(true, s_ComponentScratch);
                for (int i = 0; i < s_ComponentScratch.Count; i++)
                {
                    Component component = s_ComponentScratch[i];
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
