using System.Collections.Generic;
using Hecton8.Construction;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class BaseModulePrefabIntegrityEnforcer
    {
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/Construction/Final";

        [MenuItem("Hecton8/Validation/Enforce Base Module Prefab Integrity", priority = 216)]
        public static void EnforceBaseModulePrefabIntegrity()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FinalPrefabFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                    continue;

                try
                {
                    if (!prefabRoot.TryGetComponent(out BaseModule baseModule))
                        continue;

                    bool dirty = RemoveMeshColliders(prefabRoot);
                    dirty |= EnsurePrimitiveColliderCoverage(prefabRoot, out BoxCollider[] boxes, out CapsuleCollider[] capsules);

                    if (!prefabRoot.TryGetComponent(out BaseModuleNavModifier navModifier))
                    {
                        navModifier = prefabRoot.AddComponent<BaseModuleNavModifier>();
                        dirty = true;
                    }

                    navModifier.ConfigureColliderSources(boxes, capsules);
                    EditorUtility.SetDirty(navModifier);
                    dirty = true;

                    if (dirty)
                    {
                        EditorUtility.SetDirty(prefabRoot);
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool RemoveMeshColliders(GameObject prefabRoot)
        {
            bool dirty = false;
            MeshCollider[] meshColliders = prefabRoot.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                if (meshColliders[i] == null)
                    continue;

                Object.DestroyImmediate(meshColliders[i], true);
                dirty = true;
            }

            return dirty;
        }

        private static bool EnsurePrimitiveColliderCoverage(GameObject prefabRoot, out BoxCollider[] boxes, out CapsuleCollider[] capsules)
        {
            bool dirty = false;
            List<BoxCollider> boxList = new List<BoxCollider>(8); // COLD ALLOC: List<BoxCollider>[8] - prefab primitive-collider staging during editor validation - owner: BaseModulePrefabIntegrityEnforcer
            List<CapsuleCollider> capsuleList = new List<CapsuleCollider>(8); // COLD ALLOC: List<CapsuleCollider>[8] - prefab primitive-collider staging during editor validation - owner: BaseModulePrefabIntegrityEnforcer

            CollectPrimitiveColliders(prefabRoot, boxList, capsuleList);
            if (boxList.Count == 0 && capsuleList.Count == 0 && TryBuildFallbackBounds(prefabRoot, out Bounds bounds))
            {
                if (!prefabRoot.TryGetComponent(out BoxCollider fallback))
                    fallback = prefabRoot.AddComponent<BoxCollider>();

                fallback.isTrigger = false;
                fallback.center = prefabRoot.transform.InverseTransformPoint(bounds.center);
                fallback.size = bounds.size;
                dirty = true;

                boxList.Clear();
                capsuleList.Clear();
                CollectPrimitiveColliders(prefabRoot, boxList, capsuleList);
            }

            boxes = boxList.ToArray();
            capsules = capsuleList.ToArray();
            return dirty;
        }

        private static void CollectPrimitiveColliders(GameObject prefabRoot, List<BoxCollider> boxList, List<CapsuleCollider> capsuleList)
        {
            BoxCollider[] allBoxes = prefabRoot.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                BoxCollider box = allBoxes[i];
                if (box != null && !box.isTrigger)
                    boxList.Add(box);
            }

            CapsuleCollider[] allCapsules = prefabRoot.GetComponentsInChildren<CapsuleCollider>(true);
            for (int i = 0; i < allCapsules.Length; i++)
            {
                CapsuleCollider capsule = allCapsules[i];
                if (capsule != null && !capsule.isTrigger)
                    capsuleList.Add(capsule);
            }
        }

        private static bool TryBuildFallbackBounds(GameObject prefabRoot, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return initialized;
        }
    }
}
