using UnityEditor;
using UnityEngine;
using Hecton8.AI;

namespace Hecton8.AI.Editor
{
    public static class FaunaColliderValidator
    {
        private const string SearchRoot = "Assets/_Project";
        private const float CapsuleThreshold = 1.2f;

        [MenuItem("Hecton/Validation/Fauna Collider Validator", priority = 241)]
        public static void ValidateFaunaPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SearchRoot });
            int prefabCount = 0;
            int strippedMeshColliderCount = 0;
            int strippedPrimitiveHitboxCount = 0;
            int fittedPrimitiveCount = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null || prefabAsset.GetComponentInChildren<FaunaBrain>(true) == null)
                    continue;

                prefabCount++;
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    if (prefabRoot == null)
                        continue;

                    FaunaBrain[] brains = prefabRoot.GetComponentsInChildren<FaunaBrain>(true);
                    bool dirty = false;
                    for (int brainIndex = 0; brainIndex < brains.Length; brainIndex++)
                    {
                        FaunaBrain brain = brains[brainIndex];
                        if (brain == null)
                            continue;

                        dirty |= StripMeshColliders(brain.transform, ref strippedMeshColliderCount);
                        dirty |= StripRedundantPrimitiveHitboxColliders(brain.transform, ref strippedPrimitiveHitboxCount);
                        dirty |= EnsurePrimitiveCollider(brain.gameObject, ref fittedPrimitiveCount);
                    }

                    if (dirty)
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FaunaColliderValidator] Prefabs={prefabCount} MeshCollidersStripped={strippedMeshColliderCount} PrimitiveHitboxesStripped={strippedPrimitiveHitboxCount} PrimitiveFits={fittedPrimitiveCount}");
        }

        private static bool StripMeshColliders(Transform root, ref int strippedMeshColliderCount)
        {
            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
            bool dirty = false;
            for (int i = 0; i < meshColliders.Length; i++)
            {
                MeshCollider meshCollider = meshColliders[i];
                if (meshCollider == null)
                    continue;

                Object.DestroyImmediate(meshCollider, true);
                strippedMeshColliderCount++;
                dirty = true;
            }

            return dirty;
        }

        private static bool StripRedundantPrimitiveHitboxColliders(Transform root, ref int strippedPrimitiveHitboxCount)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool dirty = false;
            bool keptRootPrimitive = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider is MeshCollider)
                    continue;

                bool isRoot = collider.transform == root;
                bool isPrimitive = collider is CapsuleCollider || collider is SphereCollider || collider is BoxCollider;
                if (!isPrimitive)
                    continue;

                if (isRoot && !keptRootPrimitive)
                {
                    keptRootPrimitive = true;
                    continue;
                }

                bool explicitDamageHitbox =
                    IsDamageHitboxName(collider.transform.name) ||
                    IsDamageHitboxName(collider.gameObject.name) ||
                    IsDamageHitboxLayer(collider.gameObject.layer) ||
                    HasDamageHitboxComponent(collider.gameObject);
                if (!isRoot && explicitDamageHitbox)
                {
                    Object.DestroyImmediate(collider, true);
                    strippedPrimitiveHitboxCount++;
                    dirty = true;
                    continue;
                }

                if (isRoot)
                {
                    Object.DestroyImmediate(collider, true);
                    strippedPrimitiveHitboxCount++;
                    dirty = true;
                }
            }

            return dirty;
        }

        private static bool IsDamageHitboxName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lower = value.ToLowerInvariant();
            return lower.Contains("hit") ||
                   lower.Contains("hurt") ||
                   lower.Contains("weak") ||
                   lower.Contains("armor") ||
                   lower.Contains("armour") ||
                   lower.Contains("damage");
        }

        private static bool IsDamageHitboxLayer(int layer)
        {
            string layerName = LayerMask.LayerToName(layer);
            return IsDamageHitboxName(layerName);
        }

        private static bool HasDamageHitboxComponent(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                if (IsDamageHitboxName(component.GetType().Name))
                    return true;
            }

            return false;
        }

        private static bool EnsurePrimitiveCollider(GameObject root, ref int fittedPrimitiveCount)
        {
            if (root == null)
                return false;

            Collider[] existingColliders = root.GetComponents<Collider>();
            for (int i = 0; i < existingColliders.Length; i++)
            {
                Collider collider = existingColliders[i];
                if (collider is CapsuleCollider || collider is SphereCollider || collider is BoxCollider)
                    return false;
            }

            if (!TryCalculateRendererBounds(root.transform, out Bounds bounds))
                return false;

            Vector3 size = bounds.size;
            Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
            int dominantAxis = ResolveDominantAxis(size);
            float largestExtent = dominantAxis == 0 ? size.x : (dominantAxis == 1 ? size.y : size.z);
            float secondaryA = dominantAxis == 0 ? size.y : size.x;
            float secondaryB = dominantAxis == 2 ? size.y : size.z;
            float radius = Mathf.Max(secondaryA, secondaryB) * 0.5f;

            if (largestExtent >= Mathf.Max(secondaryA, secondaryB) * CapsuleThreshold)
            {
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = localCenter;
                capsule.direction = dominantAxis;
                capsule.radius = Mathf.Max(0.05f, radius);
                capsule.height = Mathf.Max(capsule.radius * 2f, largestExtent);
            }
            else
            {
                SphereCollider sphere = root.AddComponent<SphereCollider>();
                sphere.center = localCenter;
                sphere.radius = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f);
            }

            fittedPrimitiveCount++;
            return true;
        }

        private static int ResolveDominantAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return 0;

            if (size.y >= size.z)
                return 1;

            return 2;
        }

        private static bool TryCalculateRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }
}
