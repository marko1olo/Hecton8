using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveWallGrowthRuntimeBuilder
    {
        private const string WallGrowthRootName = "_WallGrowth";
        private static readonly string[] _GrowthNames = CreateNameCache("Growth_", 18); // COLD ALLOC: bounded wall-growth child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _GrowthPropertyBlock;

        public static void Build(
            Transform parent,
            HectonVoxelVolume volume,
            CavePreset preset,
            WallGrowthConfig config,
            float globalIntensity)
        {
            if (parent == null || volume == null || config == null || !config.enabled)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds))
                return;

            Transform growthRoot = GetOrCreateRoot(parent);
            Material growthMaterial = ResolveGrowthMaterial(volume);
            int growthCount = ResolveGrowthCount(preset, volumeBounds, config, globalIntensity);

            for (int i = 0; i < growthCount; i++)
            {
                Renderer growthRenderer = CreateOrConfigureGrowth(
                    growthRoot,
                    i,
                    volumeBounds,
                    growthMaterial,
                    config,
                    globalIntensity,
                    volume.caveKey != 0L ? volume.caveKey : ComputeFallbackSeed(volume.transform.position, preset));
                ApplyGrowthVisuals(growthRenderer, config, globalIntensity);
            }

            DisableUnusedChildren(growthRoot, growthCount);
        }

        private static Renderer CreateOrConfigureGrowth(
            Transform root,
            int index,
            Bounds volumeBounds,
            Material growthMaterial,
            WallGrowthConfig config,
            float globalIntensity,
            long runtimeSeed)
        {
            string name = GetCachedName(index);
            bool ceilingBias = Hash01(runtimeSeed, index, 11) > 0.55f;
            float side = HashSigned(runtimeSeed, index, 17);
            float wallInset = Mathf.Lerp(0.14f, 0.32f, Hash01(runtimeSeed, index, 23));
            float forwardOffset = HashSigned(runtimeSeed, index, 31) * volumeBounds.extents.z * 0.72f;
            float verticalT = ceilingBias
                ? Mathf.Lerp(0.62f, 0.94f, Hash01(runtimeSeed, index, 43))
                : Mathf.Lerp(0.18f, 0.74f, Hash01(runtimeSeed, index, 43));
            float x = volumeBounds.center.x + Mathf.Sign(side) * volumeBounds.extents.x * (1f - wallInset);
            float y = Mathf.Lerp(volumeBounds.min.y, volumeBounds.max.y, verticalT);
            float z = volumeBounds.center.z + forwardOffset;
            float length = Mathf.Lerp(0.8f, 2.8f, Hash01(runtimeSeed, index, 59)) * Mathf.Lerp(0.8f, 1.15f, globalIntensity);
            float radius = Mathf.Lerp(0.12f, 0.42f, Hash01(runtimeSeed, index, 71)) * Mathf.Lerp(0.8f, 1.1f, config.swayAmount + 0.2f);
            float yaw = HashSigned(runtimeSeed, index, 83) * 40f;
            float roll = HashSigned(runtimeSeed, index, 97) * Mathf.Lerp(8f, 26f, config.swayAmount);
            float pitch = ceilingBias
                ? Mathf.Lerp(100f, 150f, Hash01(runtimeSeed, index, 109))
                : Mathf.Lerp(-20f, 30f, Hash01(runtimeSeed, index, 109));
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 localScale = new Vector3(radius, length, radius);
            Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);

            if (index < root.childCount)
            {
                Transform existing = root.GetChild(index);
                ActivateTransform(existing);
                return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                    existing.gameObject,
                    PrimitiveType.Capsule,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    growthMaterial);
            }

            return WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                root,
                PrimitiveType.Capsule,
                name,
                localPosition,
                localRotation,
                localScale,
                growthMaterial);
        }

        private static void ApplyGrowthVisuals(Renderer renderer, WallGrowthConfig config, float globalIntensity)
        {
            if (renderer == null || config == null)
                return;

            Color baseColor = Color.Lerp(new Color(0.14f, 0.18f, 0.16f, 1f), config.growthColor, Mathf.Clamp01(0.55f + config.pulseAmount * 0.35f));
            Color emission = config.growthColor * Mathf.Lerp(0.15f, 1.4f, Mathf.Clamp01(config.pulseAmount * globalIntensity));
            MaterialPropertyBlock propertyBlock = GetGrowthPropertyBlock();
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(_BaseColorId, baseColor);
            propertyBlock.SetColor(_ColorId, baseColor);
            propertyBlock.SetColor(_EmissionColorId, emission);
            renderer.SetPropertyBlock(propertyBlock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static MaterialPropertyBlock GetGrowthPropertyBlock()
        {
            if (_GrowthPropertyBlock != null)
                return _GrowthPropertyBlock;

            // COLD ALLOC: MaterialPropertyBlock[1] — shared growth tint block — owner: CaveWallGrowthRuntimeBuilder
            _GrowthPropertyBlock = new MaterialPropertyBlock();
            return _GrowthPropertyBlock;
        }

        private static int ResolveGrowthCount(
            CavePreset preset,
            Bounds volumeBounds,
            WallGrowthConfig config,
            float globalIntensity)
        {
            float complexity = 0.45f;
            if (preset != null)
                complexity = Mathf.Clamp01((preset.maxRooms + preset.maxStructures) / 24f);

            float surfaceFactor = Mathf.Clamp01(
                (volumeBounds.size.x * volumeBounds.size.y + volumeBounds.size.z * volumeBounds.size.y) / 1200f);
            float intensity = Mathf.Clamp(globalIntensity, 0.1f, 1.25f);
            float swayBias = Mathf.Lerp(0.65f, 1.15f, config.swayAmount);
            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(4f, 16f, Mathf.Max(complexity, surfaceFactor)) * swayBias * intensity),
                3,
                18);
        }

        private static Material ResolveGrowthMaterial(HectonVoxelVolume volume)
        {
            if (volume != null && volume.TryGetComponent(out MeshRenderer renderer))
                return renderer.sharedMaterial;

            return null;
        }

        private static Transform GetOrCreateRoot(Transform parent)
        {
            Transform root = parent.Find(WallGrowthRootName);
            if (root != null)
            {
                ActivateTransform(root);
                return root;
            }

            GameObject rootObject = new GameObject(WallGrowthRootName);
            root = rootObject.transform;
            root.SetParent(parent, false);
            return root;
        }

        private static void DisableUnusedChildren(Transform root, int usedChildCount)
        {
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
            if ((uint)index < (uint)_GrowthNames.Length)
                return _GrowthNames[index];

            return WallGrowthRootName;
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
            int presetHash = preset != null ? preset.maxRooms * 131 ^ preset.maxStructures * 29 : 0;
            return ((long)x << 42) ^ ((long)y << 21) ^ (uint)z ^ (uint)presetHash;
        }

        private static float Hash01(long runtimeSeed, int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeSeed * 1664525L + index * 1013904223L + salt * 92821L);
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
