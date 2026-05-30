using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveSedimentShelfRuntimeBuilder
    {
        public const int RuntimeCapacity = 20;
        private const string ShelfRootName = "_SedimentShelves";
        private const int MaxShelfCount = RuntimeCapacity;
        private static readonly string[] _ShelfNames = CreateNameCache("Shelf_", MaxShelfCount); // COLD ALLOC: bounded shelf child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _ShelfPropertyBlock;

        public static Transform Prewarm(Transform parent)
        {
            return Prewarm(parent, null, null, null);
        }

        public static Transform Prewarm(
            Transform parent,
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers)
        {
            if (parent == null)
                return null;

            Transform root = GetOrCreateShelfRoot(parent);
            for (int i = 0; i < MaxShelfCount; i++)
            {
                if (i < root.childCount)
                {
                    GameObject primitiveObject = root.GetChild(i).gameObject;
                    WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                        primitiveObject,
                        PrimitiveType.Cube,
                        GetCachedName(i),
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
                    CachePrimitiveCold(i, primitiveObject, primitiveObjects, primitiveFilters, primitiveRenderers);
                    continue;
                }

                Renderer renderer = WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    root,
                    PrimitiveType.Cube,
                    GetCachedName(i),
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                if (renderer != null)
                {
                    CachePrimitiveCold(i, renderer.gameObject, primitiveObjects, primitiveFilters, primitiveRenderers);
                    renderer.gameObject.SetActive(false);
                }
            }

            DisableUnusedChildren(root, 0);
            return root;
        }

        public static void PrewarmSharedResources()
        {
            _ = GetShelfPropertyBlock();
        }

        public static void BuildPreparedCachedHot(
            Transform shelfRoot,
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            HectonVoxelVolume volume,
            CavePreset preset,
            SedimentShelfConfig config,
            float globalIntensity)
        {
            if (shelfRoot == null || volume == null || config == null || !config.enabled)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds))
            {
                DisableUnusedCachedPrimitives(primitiveObjects, 0);
                return;
            }

            int shelfCount = ResolveShelfCount(config, preset, volumeBounds, globalIntensity);
            if (shelfCount <= 0)
            {
                DisableUnusedCachedPrimitives(primitiveObjects, 0);
                return;
            }

            Material shelfMaterial = ResolveShelfMaterial(volume);
            float minScale = Mathf.Max(0.4f, Mathf.Min(config.scaleRange.x, config.scaleRange.y));
            float maxScale = Mathf.Max(minScale, Mathf.Max(config.scaleRange.x, config.scaleRange.y));
            float radiusX = Mathf.Max(1.25f, volumeBounds.extents.x * 0.58f);
            float radiusZ = Mathf.Max(1.25f, volumeBounds.extents.z * 0.58f);
            float floorY = volumeBounds.min.y + Mathf.Clamp(config.floorOffset, 0f, 2f);
            long runtimeSeed = volume.caveKey != 0L
                ? volume.caveKey
                : ComputeFallbackSeed(volume.transform.position, preset);
            ActivateTransform(shelfRoot);

            for (int i = 0; i < shelfCount; i++)
            {
                float angle = Hash01(runtimeSeed, i, 11) * 360f;
                float angleRadians = angle * Mathf.Deg2Rad;
                float radial = math.lerp(0.18f, 0.92f, Hash01(runtimeSeed, i, 17));
                float width = math.lerp(minScale, maxScale, Hash01(runtimeSeed, i, 23));
                float depth = math.lerp(minScale * 0.42f, maxScale * 0.78f, Hash01(runtimeSeed, i, 31));
                float thickness = math.lerp(0.18f, 0.52f, Hash01(runtimeSeed, i, 43));
                float yaw = angle + HashSigned(runtimeSeed, i, 59) * 36f;
                float roll = HashSigned(runtimeSeed, i, 71) * 10f;
                float pitch = HashSigned(runtimeSeed, i, 83) * 6f;
                float sine = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angleRadians);
                float cosine = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(angleRadians);
                Vector3 localPosition = new Vector3(
                    volumeBounds.center.x + cosine * radiusX * radial,
                    floorY + Hash01(runtimeSeed, i, 97) * Mathf.Max(0.1f, thickness * 0.25f),
                    volumeBounds.center.z + sine * radiusZ * radial);
                Vector3 localScale = new Vector3(width, thickness, depth);
                Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);
                Renderer shelfRenderer = CreateOrConfigureShelfCachedHot(
                    primitiveObjects,
                    primitiveFilters,
                    primitiveRenderers,
                    i,
                    localPosition,
                    localRotation,
                    localScale,
                    shelfMaterial);
                ApplyShelfVisuals(shelfRenderer, config);
            }

            DisableUnusedCachedPrimitives(primitiveObjects, shelfCount);
        }

        private static Renderer CreateOrConfigureShelfCachedHot(
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            int shelfIndex,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            if (primitiveObjects == null ||
                primitiveFilters == null ||
                primitiveRenderers == null ||
                (uint)shelfIndex >= (uint)primitiveObjects.Length ||
                (uint)shelfIndex >= (uint)primitiveFilters.Length ||
                (uint)shelfIndex >= (uint)primitiveRenderers.Length)
            {
                return null;
            }

            GameObject primitiveObject = primitiveObjects[shelfIndex];
            MeshFilter filter = primitiveFilters[shelfIndex];
            MeshRenderer renderer = primitiveRenderers[shelfIndex];
            if (primitiveObject == null || filter == null || renderer == null)
                return null;

            if (!primitiveObject.activeSelf)
                primitiveObject.SetActive(true);

            return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualCachedHot(
                primitiveObject,
                filter,
                renderer,
                PrimitiveType.Cube,
                GetCachedName(shelfIndex),
                localPosition,
                localRotation,
                localScale,
                material);
        }

        private static void CachePrimitiveCold(
            int index,
            GameObject primitiveObject,
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers)
        {
            if (primitiveObject == null ||
                primitiveObjects == null ||
                primitiveFilters == null ||
                primitiveRenderers == null ||
                (uint)index >= (uint)primitiveObjects.Length ||
                (uint)index >= (uint)primitiveFilters.Length ||
                (uint)index >= (uint)primitiveRenderers.Length)
            {
                return;
            }

            if (!WorldGeneratedPrimitiveFactory.TryResolvePrimitiveComponentsCold(primitiveObject, out MeshFilter filter, out MeshRenderer renderer))
                return;

            primitiveObjects[index] = primitiveObject;
            primitiveFilters[index] = filter;
            primitiveRenderers[index] = renderer;
        }

        private static void DisableUnusedCachedPrimitives(GameObject[] primitiveObjects, int usedChildCount)
        {
            if (primitiveObjects == null)
                return;

            for (int i = usedChildCount; i < primitiveObjects.Length; i++)
            {
                GameObject primitiveObject = primitiveObjects[i];
                if (primitiveObject != null && primitiveObject.activeSelf)
                    primitiveObject.SetActive(false);
            }
        }

        private static void ApplyShelfVisuals(Renderer renderer, SedimentShelfConfig config)
        {
            if (renderer == null || config == null)
                return;

            Color shelfColor = config.tint;
            shelfColor.a = Mathf.Clamp01(config.opacity);
            MaterialPropertyBlock propertyBlock = GetShelfPropertyBlock();
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(_BaseColorId, shelfColor);
            propertyBlock.SetColor(_ColorId, shelfColor);
            renderer.SetPropertyBlock(propertyBlock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static MaterialPropertyBlock GetShelfPropertyBlock()
        {
            if (_ShelfPropertyBlock != null)
                return _ShelfPropertyBlock;

            // COLD ALLOC: MaterialPropertyBlock[1] — shared shelf tint block — owner: CaveSedimentShelfRuntimeBuilder
            _ShelfPropertyBlock = new MaterialPropertyBlock();
            return _ShelfPropertyBlock;
        }

        private static int ResolveShelfCount(
            SedimentShelfConfig config,
            CavePreset preset,
            Bounds volumeBounds,
            float globalIntensity)
        {
            int maxCount = Mathf.Clamp(config.maxCount, 0, MaxShelfCount);
            if (maxCount <= 0)
                return 0;

            float complexity = 0.5f;
            if (preset != null)
                complexity = Mathf.Clamp01((preset.maxRooms + preset.maxStructures) / 24f);

            float footprintFactor = Mathf.Clamp01((volumeBounds.size.x * volumeBounds.size.z) / 900f);
            float intensity = Mathf.Clamp(globalIntensity, 0.1f, 1.25f);
            float density = Mathf.Max(complexity, footprintFactor);
            return Mathf.Clamp(
                Mathf.RoundToInt(maxCount * math.lerp(0.35f, 1f, density) * intensity),
                1,
                maxCount);
        }

        private static Material ResolveShelfMaterial(HectonVoxelVolume volume)
        {
            MeshRenderer renderer = volume != null ? volume.CachedMeshRenderer : null;
            if (renderer != null)
                return renderer.sharedMaterial;

            return null;
        }

        private static Transform GetOrCreateShelfRoot(Transform parent)
        {
            Transform shelfRoot = parent.Find(ShelfRootName);
            if (shelfRoot != null)
            {
                ActivateTransform(shelfRoot);
                return shelfRoot;
            }

            GameObject shelfRootObject = new GameObject(ShelfRootName);
            shelfRoot = shelfRootObject.transform;
            shelfRoot.SetParent(parent, false);
            return shelfRoot;
        }

        private static void DisableUnusedChildren(Transform root, int usedChildCount)
        {
            if (root == null)
                return;

            for (int i = usedChildCount; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private static void ActivateTransform(Transform target)
        {
            if (target != null && !target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
        }

        private static string GetCachedName(int index)
        {
            if ((uint)index < (uint)_ShelfNames.Length)
                return _ShelfNames[index];

            return ShelfRootName;
        }

        private static string[] CreateNameCache(string prefix, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = prefix + i;

            return names;
        }

        private static long ComputeFallbackSeed(Vector3 position, CavePreset preset)
        {
            int x = Mathf.FloorToInt(position.x * 0.1f);
            int y = Mathf.FloorToInt(position.y * 0.1f);
            int z = Mathf.FloorToInt(position.z * 0.1f);
            int presetHash = preset != null ? preset.maxRooms * 397 ^ preset.maxStructures * 17 : 0;
            return ((long)x << 42) ^ ((long)y << 21) ^ (uint)z ^ (uint)presetHash;
        }

        private static float Hash01(long runtimeSeed, int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeSeed * 1103515245L + index * 92821L + salt * 486187739L);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static float HashSigned(long runtimeSeed, int index, int salt)
        {
            return Hash01(runtimeSeed, index, salt) * 2f - 1f;
        }
    }
}
