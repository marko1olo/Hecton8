using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveSedimentShelfRuntimeBuilder
    {
        private const string ShelfRootName = "_SedimentShelves";
        private static readonly string[] _ShelfNames = CreateNameCache("Shelf_", 20); // COLD ALLOC: bounded shelf child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _ShelfPropertyBlock;

        public static void Build(
            Transform parent,
            HectonVoxelVolume volume,
            CavePreset preset,
            SedimentShelfConfig config,
            float globalIntensity)
        {
            if (parent == null || volume == null || config == null || !config.enabled)
                return;

            Transform shelfRoot = GetOrCreateShelfRoot(parent);
            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds))
            {
                DisableUnusedChildren(shelfRoot, 0);
                return;
            }

            int shelfCount = ResolveShelfCount(config, preset, volumeBounds, globalIntensity);
            if (shelfCount <= 0)
            {
                DisableUnusedChildren(shelfRoot, 0);
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
                Vector3 localPosition = new Vector3(
                    volumeBounds.center.x + Mathf.Cos(angleRadians) * radiusX * radial,
                    floorY + Hash01(runtimeSeed, i, 97) * Mathf.Max(0.1f, thickness * 0.25f),
                    volumeBounds.center.z + Mathf.Sin(angleRadians) * radiusZ * radial);
                Vector3 localScale = new Vector3(width, thickness, depth);
                Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);
                Renderer shelfRenderer = CreateOrConfigureShelf(
                    shelfRoot,
                    i,
                    localPosition,
                    localRotation,
                    localScale,
                    shelfMaterial);
                ApplyShelfVisuals(shelfRenderer, config);
            }

            DisableUnusedChildren(shelfRoot, shelfCount);
        }

        private static Renderer CreateOrConfigureShelf(
            Transform root,
            int shelfIndex,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            string name = GetCachedName(shelfIndex);
            if (shelfIndex < root.childCount)
            {
                Transform existing = root.GetChild(shelfIndex);
                ActivateTransform(existing);
                return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                    existing.gameObject,
                    PrimitiveType.Cube,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    material);
            }

            return WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                root,
                PrimitiveType.Cube,
                name,
                localPosition,
                localRotation,
                localScale,
                material);
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
            int maxCount = Mathf.Clamp(config.maxCount, 0, 20);
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
            if (volume != null && volume.TryGetComponent(out MeshRenderer renderer))
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
