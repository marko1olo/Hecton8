using System;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class WorldProceduralFloraProxyShapeBuilder
    {
        public static bool TryBuild(string rootName, Vector3 scale, Material material, out GameObject root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(rootName))
                return false;

            if (rootName.StartsWith("family_kelp_tall__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpTallProxy(root.transform, scale, material, rootName.EndsWith("__lean", StringComparison.Ordinal));
                return true;
            }

            if (rootName.StartsWith("family_kelp_patch_dense__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpPatchProxy(root.transform, scale, material, rootName.EndsWith("__grove", StringComparison.Ordinal));
                return true;
            }

            if (rootName.StartsWith("family_kelp_canopy__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpCanopyProxy(root.transform, scale, material, rootName.EndsWith("__frond", StringComparison.Ordinal));
                return true;
            }

            return false;
        }

        private static void BuildKelpTallProxy(Transform root, Vector3 scale, Material material, bool leaning)
        {
            Quaternion stipeRotation = leaning
                ? Quaternion.Euler(0f, 0f, 16f)
                : Quaternion.identity;
            Vector3 stipePosition = leaning
                ? new Vector3(0.14f, 1.9f, 0f)
                : new Vector3(0f, 1.9f, 0f);

            AddPrimitiveChild(root, PrimitiveType.Cylinder, stipePosition, scale, material, stipeRotation);
            AddKelpBaseRibs(root, material, leaning ? 0.06f : 0f, 0f, scale);
            AddKelpBasalBlades(root, material, new Vector3(0f, 0.65f, 0f), 1.05f);
            AddKelpMidFronds(root, material, new Vector3(leaning ? 0.12f : 0f, 2.3f, 0f), 1f, leaning ? 22f : 0f);
        }

        private static void BuildKelpPatchProxy(Transform root, Vector3 scale, Material material, bool grove)
        {
            float clusterScale = grove ? 1.08f : 0.92f;
            AddKelpStalkWithFronds(root, material, new Vector3(0f, 0f, 0f), scale * clusterScale, 0f, 1.05f, true);
            AddKelpStalkWithFronds(root, material, new Vector3(-0.92f, 0f, 0.48f), scale * 0.82f, -10f, 0.86f, false);
            AddKelpStalkWithFronds(root, material, new Vector3(0.86f, 0f, -0.44f), scale * 0.76f, 14f, 0.78f, false);

            if (grove)
            {
                AddKelpStalkWithFronds(root, material, new Vector3(-0.28f, 0f, -0.94f), scale * 0.7f, -18f, 0.72f, false);
                AddKelpStalkWithFronds(root, material, new Vector3(0.54f, 0f, 0.88f), scale * 0.68f, 21f, 0.7f, false);
            }
        }

        private static void BuildKelpCanopyProxy(Transform root, Vector3 scale, Material material, bool frondOnly)
        {
            if (!frondOnly)
            {
                AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(0f, 2.2f, 0f), scale, material, Quaternion.identity);
                AddKelpBaseRibs(root, material, 0f, 0f, scale);
            }

            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(0f, 4.5f, 0f),
                new Vector3(scale.x * 2.8f, scale.y * 0.08f, scale.z * 0.44f),
                material,
                Quaternion.Euler(0f, 0f, 8f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(-0.78f, 4.25f, 0.12f),
                new Vector3(scale.x * 2f, scale.y * 0.08f, scale.z * 0.36f),
                material,
                Quaternion.Euler(0f, 24f, -24f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(0.92f, 4.1f, -0.1f),
                new Vector3(scale.x * 1.9f, scale.y * 0.08f, scale.z * 0.34f),
                material,
                Quaternion.Euler(0f, -18f, 22f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0.22f, 4.2f, 0f),
                new Vector3(scale.x * 0.34f, scale.y * 0.14f, scale.z * 0.34f),
                material,
                Quaternion.identity);

            if (frondOnly)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0f, 2.8f, 0f),
                    new Vector3(scale.x * 1.45f, scale.y * 0.08f, scale.z * 0.24f),
                    material,
                    Quaternion.Euler(0f, 12f, 54f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0.34f, 3.2f, 0f),
                    new Vector3(scale.x * 1.2f, scale.y * 0.08f, scale.z * 0.22f),
                    material,
                    Quaternion.Euler(0f, -8f, 34f));
            }
        }

        private static void AddKelpStalkWithFronds(
            Transform root,
            Material material,
            Vector3 baseOffset,
            Vector3 stipeScale,
            float zRotation,
            float frondScale,
            bool addBasalBlades)
        {
            Quaternion stipeRotation = Mathf.Abs(zRotation) > 0.01f
                ? Quaternion.Euler(0f, 0f, zRotation)
                : Quaternion.identity;
            AddPrimitiveChild(root, PrimitiveType.Cylinder, baseOffset + new Vector3(0f, stipeScale.y * 0.5f, 0f), stipeScale, material, stipeRotation);
            AddKelpBaseRibs(root, material, baseOffset.x, baseOffset.z, stipeScale);

            if (addBasalBlades)
                AddKelpBasalBlades(root, material, baseOffset + new Vector3(0f, 0.45f, 0f), frondScale);

            AddKelpMidFronds(root, material, baseOffset + new Vector3(0f, stipeScale.y * 0.64f, 0f), frondScale, zRotation);
        }

        private static void AddKelpBaseRibs(Transform root, Material material, float centerX, float centerZ, Vector3 stipeScale)
        {
            Vector3 ribScale = new Vector3(stipeScale.x * 0.38f, Mathf.Max(0.22f, stipeScale.y * 0.16f), stipeScale.z * 0.38f);
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX - stipeScale.x * 0.36f, ribScale.y * 0.55f, centerZ), ribScale, material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX + stipeScale.x * 0.36f, ribScale.y * 0.5f, centerZ - stipeScale.z * 0.08f), ribScale, material, Quaternion.Euler(0f, 18f, 0f));
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX, ribScale.y * 0.45f, centerZ + stipeScale.z * 0.34f), ribScale * 0.9f, material, Quaternion.Euler(0f, -24f, 0f));
        }

        private static void AddKelpBasalBlades(Transform root, Material material, Vector3 center, float scale)
        {
            Vector3 bladeScale = new Vector3(1.1f * scale, 0.08f, 0.22f * scale);
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(-0.42f * scale, 0.12f, 0f), bladeScale, material, Quaternion.Euler(0f, 16f, 42f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.45f * scale, 0.18f, 0.08f), bladeScale * 0.94f, material, Quaternion.Euler(0f, -18f, -36f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.04f, 0.16f, -0.32f * scale), bladeScale * 0.88f, material, Quaternion.Euler(18f, 0f, 18f));
        }

        private static void AddKelpMidFronds(Transform root, Material material, Vector3 center, float scale, float tiltZ)
        {
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(-0.54f * scale, 0.58f * scale, 0f), new Vector3(1.5f * scale, 0.08f, 0.2f * scale), material, Quaternion.Euler(0f, 8f, -58f + tiltZ * 0.4f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.62f * scale, 0.88f * scale, -0.06f), new Vector3(1.7f * scale, 0.08f, 0.2f * scale), material, Quaternion.Euler(0f, -10f, 52f + tiltZ * 0.35f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.1f, 1.34f * scale, 0.08f), new Vector3(1.38f * scale, 0.08f, 0.18f * scale), material, Quaternion.Euler(0f, 4f, 74f + tiltZ * 0.25f));
            AddPrimitiveChild(root, PrimitiveType.Sphere, center + new Vector3(-0.08f, 0.92f * scale, 0f), new Vector3(0.16f * scale, 0.16f * scale, 0.16f * scale), material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Sphere, center + new Vector3(0.2f * scale, 1.26f * scale, 0f), new Vector3(0.14f * scale, 0.14f * scale, 0.14f * scale), material, Quaternion.identity);
        }

        private static void AddPrimitiveChild(Transform parent, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = primitive.ToString();
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;

            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }
    }
}
