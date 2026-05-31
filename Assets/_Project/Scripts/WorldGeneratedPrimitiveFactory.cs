using System.Collections.Generic;
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
        private static readonly Dictionary<int, PrimitiveRuntimeState> _RuntimeStates = new Dictionary<int, PrimitiveRuntimeState>(2048); // COLD ALLOC: generated primitive component cache for visual-sync reuse.

        private struct PrimitiveRuntimeState
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        public static void PrewarmPrimitiveResources()
        {
            TryGetPrimitiveResources(PrimitiveType.Sphere, out _, out _);
            TryGetPrimitiveResources(PrimitiveType.Capsule, out _, out _);
            TryGetPrimitiveResources(PrimitiveType.Cylinder, out _, out _);
            TryGetPrimitiveResources(PrimitiveType.Cube, out _, out _);
            TryGetPrimitiveResources(PrimitiveType.Plane, out _, out _);
            TryGetPrimitiveResources(PrimitiveType.Quad, out _, out _);
        }

        public static Renderer ConfigurePrimitiveVisualHot(
            GameObject primitive,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial = null)
        {
            if (primitive == null ||
                !TryGetPrimitiveResourcesHot(primitiveType, out Mesh mesh, out Material defaultMaterial) ||
                !_RuntimeStates.TryGetValue(ResolvePrimitiveKey(primitive), out PrimitiveRuntimeState state) ||
                state.Filter == null ||
                state.Renderer == null)
            {
                return null;
            }

            return ConfigurePrimitiveVisualHotNoRename(primitive, localPosition, localRotation, localScale, overrideMaterial, mesh, defaultMaterial, state.Filter, state.Renderer);
        }

        public static Renderer ConfigurePrimitiveVisualCachedHot(
            GameObject primitive,
            MeshFilter filter,
            MeshRenderer renderer,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial = null)
        {
            if (primitive == null ||
                filter == null ||
                renderer == null ||
                !TryGetPrimitiveResourcesHot(primitiveType, out Mesh mesh, out Material defaultMaterial))
            {
                return null;
            }

            return ConfigurePrimitiveVisualHotNoRename(primitive, localPosition, localRotation, localScale, overrideMaterial, mesh, defaultMaterial, filter, renderer);
        }

        public static GameObject CreateCachedPrimitiveShell(Transform parent, string name)
        {
            return CreateCachedPrimitiveShell(parent, name, out _, out _);
        }

        public static GameObject CreateCachedPrimitiveShell(Transform parent, string name, out MeshFilter filter, out MeshRenderer renderer)
        {
            GameObject primitive = new GameObject(string.IsNullOrEmpty(name) ? "PrimitiveShell" : name);
            primitive.transform.SetParent(parent, false);
            filter = primitive.AddComponent<MeshFilter>();
            renderer = primitive.AddComponent<MeshRenderer>();
            RegisterPrimitiveRuntimeState(primitive, filter, renderer);
            primitive.SetActive(false);
            return primitive;
        }

        public static bool TryResolvePrimitiveComponentsCold(GameObject primitive, out MeshFilter filter, out MeshRenderer renderer)
        {
            filter = null;
            renderer = null;
            if (primitive == null)
                return false;

            if (!primitive.TryGetComponent(out filter))
                filter = primitive.AddComponent<MeshFilter>();

            if (!primitive.TryGetComponent(out renderer))
                renderer = primitive.AddComponent<MeshRenderer>();

            RegisterPrimitiveRuntimeState(primitive, filter, renderer);
            return filter != null && renderer != null;
        }

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

        private static bool TryGetPrimitiveResourcesHot(PrimitiveType primitiveType, out Mesh mesh, out Material material)
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
            return mesh != null;
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

        private static void RegisterPrimitiveRuntimeState(GameObject primitive, MeshFilter filter, MeshRenderer renderer)
        {
            if (primitive == null || filter == null || renderer == null)
                return;

            _RuntimeStates[ResolvePrimitiveKey(primitive)] = new PrimitiveRuntimeState
            {
                Filter = filter,
                Renderer = renderer
            };
        }

        private static int ResolvePrimitiveKey(GameObject primitive)
        {
            return unchecked((int)EntityId.ToULong(primitive.GetEntityId()));
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
            if (!primitive.TryGetComponent(out MeshFilter filter))
                filter = primitive.AddComponent<MeshFilter>();

            if (!primitive.TryGetComponent(out MeshRenderer renderer))
                renderer = primitive.AddComponent<MeshRenderer>();

            RegisterPrimitiveRuntimeState(primitive, filter, renderer);
            return ConfigurePrimitiveVisual(primitive, primitiveType, name, localPosition, localRotation, localScale, overrideMaterial, mesh, defaultMaterial, filter, renderer);
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
            Material defaultMaterial,
            MeshFilter filter,
            MeshRenderer renderer)
        {
            primitive.name = string.IsNullOrEmpty(name) ? GetPrimitiveName(primitiveType) : name;

            Transform primitiveTransform = primitive.transform;
            primitiveTransform.localPosition = localPosition;
            primitiveTransform.localRotation = localRotation;
            primitiveTransform.localScale = localScale;

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : defaultMaterial;
            return renderer;
        }

        private static Renderer ConfigurePrimitiveVisualHotNoRename(
            GameObject primitive,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material overrideMaterial,
            Mesh mesh,
            Material defaultMaterial,
            MeshFilter filter,
            MeshRenderer renderer)
        {
            Transform primitiveTransform = primitive.transform;
            primitiveTransform.localPosition = localPosition;
            primitiveTransform.localRotation = localRotation;
            primitiveTransform.localScale = localScale;

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : defaultMaterial;
            return renderer;
        }
    }
}
