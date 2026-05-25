using UnityEngine;

namespace Hecton8.World
{
    internal static class WorldGeneratedPrimitiveFactory
    {
        private static readonly Mesh[] _CachedMeshes = new Mesh[6];
        private static readonly Material[] _CachedMaterials = new Material[6];
        private static readonly string[] _CachedNames =
        {
            "Sphere",
            "Capsule",
            "Cylinder",
            "Cube",
            "Plane",
            "Quad"
        };

        public static Renderer CreatePrimitiveVisual(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial = null)
        {
            if (!TryGetPrimitiveResources(primitiveType, out Mesh mesh, out Material defaultMaterial))
                return null;

            GameObject primitive = new GameObject(string.IsNullOrEmpty(name) ? GetPrimitiveName(primitiveType) : name);
            primitive.transform.SetParent(parent, false);
            return ConfigurePrimitiveVisual(primitive, primitiveType, name, localPosition, localRotation, localScale, overrideMaterial, mesh, defaultMaterial);
        }

        public static Renderer ConfigurePrimitiveVisual(
            GameObject primitive,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial = null)
        {
            if (primitive == null || !TryGetPrimitiveResources(primitiveType, out Mesh mesh, out Material defaultMaterial))
                return null;

            return ConfigurePrimitiveVisual(primitive, primitiveType, name, localPosition, localRotation, localScale, overrideMaterial, mesh, defaultMaterial);
        }

        public static string GetPrimitiveName(PrimitiveType primitiveType)
        {
            int index = (int)primitiveType;
            if ((uint)index >= (uint)_CachedNames.Length)
                return "Primitive";

            return _CachedNames[index];
        }

        private static bool TryGetPrimitiveResources(PrimitiveType primitiveType, out Mesh mesh, out Material material)
        {
            int index = (int)primitiveType;
            if ((uint)index >= (uint)_CachedMeshes.Length)
            {
                mesh = null;
                material = null;
                return false;
            }

            mesh = _CachedMeshes[index];
            material = _CachedMaterials[index];
            if (mesh != null)
                return true;

            GameObject temp = GameObject.CreatePrimitive(primitiveType);
            temp.TryGetComponent(out MeshFilter filter);
            temp.TryGetComponent(out MeshRenderer renderer);
            mesh = filter != null ? filter.sharedMesh : null;
            material = renderer != null ? renderer.sharedMaterial : null;
            _CachedMeshes[index] = mesh;
            _CachedMaterials[index] = material;

            if (Application.isPlaying)
                Object.Destroy(temp);
            else
                Object.DestroyImmediate(temp);

            return mesh != null;
        }

        private static Renderer ConfigurePrimitiveVisual(
            GameObject primitive,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial,
            Mesh mesh,
            Material defaultMaterial)
        {
            primitive.name = string.IsNullOrEmpty(name) ? GetPrimitiveName(primitiveType) : name;

            Transform primitiveTransform = primitive.transform;
            primitiveTransform.localPosition = localPosition;
            primitiveTransform.localRotation = localRotation;
            primitiveTransform.localScale = localScale;

            if (!primitive.TryGetComponent(out MeshFilter filter))
                filter = primitive.AddComponent<MeshFilter>();

            if (!primitive.TryGetComponent(out MeshRenderer renderer))
                renderer = primitive.AddComponent<MeshRenderer>();

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : defaultMaterial;
            return renderer;
        }
    }
}
