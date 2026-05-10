#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Repairs the named rock prefabs and loaded scene instances whose LOD tables drifted into cross-prefab renderer ownership.
    /// </summary>
    internal static class HectonLodGroupConflictResolver
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Fix Rock LODGroup Conflicts";
        private const string LogPrefix = "[HectonLodGroupConflictResolver]";

        private static readonly string[] s_TargetPrefabPaths =
        {
            "Assets/_Project/Prefabs/Nature/GOTOVYE PREFABY KAMNEY/ENV_ Skala2.prefab",
            "Assets/_Project/Prefabs/Nature/GOTOVYE PREFABY KAMNEY/ENV_ Bolder 2.prefab"
        };

        private static readonly string[] s_TargetRootNames =
        {
            "ENV_ Skala2",
            "ENV_ Bolder 2"
        };

        [MenuItem(MenuPath, priority = 183)]
        private static void RunFromMenu()
        {
            int prefabFixCount = RepairTargetPrefabs();
            int loadedSceneFixCount = RepairLoadedSceneInstances();
            Debug.Log($"{LogPrefix} prefabFixes={prefabFixCount}, loadedSceneFixes={loadedSceneFixCount}.");
        }

        internal static int RepairTargetPrefabs()
        {
            int fixCount = 0;

            for (int prefabIndex = 0; prefabIndex < s_TargetPrefabPaths.Length; prefabIndex++)
            {
                string prefabPath = s_TargetPrefabPaths[prefabIndex];
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                    continue;

                try
                {
                    LODGroup lodGroup = prefabRoot.GetComponent<LODGroup>();
                    if (lodGroup == null)
                        continue;

                    if (!RepairLodGroup(lodGroup))
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    fixCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return fixCount;
        }

        internal static int RepairLoadedSceneInstances()
        {
            int fixCount = 0;
            LODGroup[] lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>();

            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (!IsSceneTarget(lodGroup))
                    continue;

                if (!RepairLodGroup(lodGroup))
                    continue;

                PrefabUtility.RecordPrefabInstancePropertyModifications(lodGroup);
                EditorUtility.SetDirty(lodGroup);
                fixCount++;
            }

            return fixCount;
        }

        private static bool RepairLodGroup(LODGroup lodGroup)
        {
            if (lodGroup == null)
                return false;

            Transform root = lodGroup.transform;
            LOD[] lods = lodGroup.GetLODs();
            bool changed = false;

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer expectedRenderer = FindExpectedRenderer(root, lodIndex);
                if (expectedRenderer == null)
                    continue;

                Renderer currentRenderer = lods[lodIndex].renderers != null && lods[lodIndex].renderers.Length > 0
                    ? lods[lodIndex].renderers[0]
                    : null;

                bool invalidRendererOwner = currentRenderer != null && !currentRenderer.transform.IsChildOf(root);
                bool missingRenderer = currentRenderer == null;
                bool wrongRenderer = currentRenderer != expectedRenderer;
                bool wrongRendererCount = lods[lodIndex].renderers == null || lods[lodIndex].renderers.Length != 1;
                if (!invalidRendererOwner && !missingRenderer && !wrongRenderer && !wrongRendererCount)
                    continue;

                lods[lodIndex].renderers = new[] { expectedRenderer };
                changed = true;
            }

            if (!changed)
                return false;

            lodGroup.SetLODs(lods);
            EditorUtility.SetDirty(lodGroup);
            return true;
        }

        private static Renderer FindExpectedRenderer(Transform root, int lodIndex)
        {
            if (root == null)
                return null;

            string expectedName = $"LOD{lodIndex}";
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer.gameObject.name != expectedName)
                    continue;

                return renderer;
            }

            Debug.LogWarning($"{LogPrefix} Missing expected renderer '{expectedName}' under '{root.name}'.");
            return null;
        }

        private static bool IsSceneTarget(LODGroup lodGroup)
        {
            if (lodGroup == null || EditorUtility.IsPersistent(lodGroup))
                return false;

            string rootName = lodGroup.gameObject.name;
            for (int nameIndex = 0; nameIndex < s_TargetRootNames.Length; nameIndex++)
            {
                if (string.Equals(rootName, s_TargetRootNames[nameIndex], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
#endif
