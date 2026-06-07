using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveWallGrowthRuntimeBuilder
    {
        public const int RuntimeCapacity = 18;
        private const string WallGrowthRootName = "_WallGrowth";
        private const int MaxGrowthCount = RuntimeCapacity;
        private static readonly string[] _GrowthNames = CreateNameCache("Growth_", MaxGrowthCount); // COLD ALLOC: bounded wall-growth child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _GrowthPropertyBlock;

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

            Transform root = GetOrCreateRoot(parent);
            for (int i = 0; i < MaxGrowthCount; i++)
            {
                if (i < root.childCount)
                {
                    GameObject primitiveObject = root.GetChild(i).gameObject;
                    WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                        primitiveObject,
                        PrimitiveType.Capsule,
                        GetCachedName(i),
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
                    CachePrimitiveCold(i, primitiveObject, primitiveObjects, primitiveFilters, primitiveRenderers);
                    continue;
                }

                Renderer renderer = WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    root,
                    PrimitiveType.Capsule,
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
            _ = GetGrowthPropertyBlock();
        }

        public static void BuildPreparedCachedHot(
            Transform growthRoot,
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            HectonVoxelVolume volume,
            CavePreset preset,
            WallGrowthConfig config,
            float globalIntensity)
        {
            if (growthRoot == null || volume == null || config == null || !config.enabled)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds) ||
                !CaveDressingRuntimeSanitizer.IsFinite(volumeBounds))
            {
                DisableUnusedCachedPrimitives(primitiveObjects, 0);
                return;
            }

            Material growthMaterial = ResolveGrowthMaterial(volume);
            float safeGlobalIntensity = CaveDressingRuntimeSanitizer.ClampFinite(
                globalIntensity,
                1f,
                0f,
                CaveDressingRuntimeSanitizer.MaxGlobalIntensity);
            int growthCount = ResolveGrowthCount(preset, volumeBounds, config, safeGlobalIntensity);
            ActivateTransform(growthRoot);

            for (int i = 0; i < growthCount; i++)
            {
                Renderer growthRenderer = CreateOrConfigureGrowthCachedHot(
                    primitiveObjects,
                    primitiveFilters,
                    primitiveRenderers,
                    i,
                    volumeBounds,
                    growthMaterial,
                    config,
                    safeGlobalIntensity,
                    volume.caveKey != 0L ? volume.caveKey : ComputeFallbackSeed(CaveDressingRuntimeSanitizer.SeedPosition(volume.transform.position), preset));
                ApplyGrowthVisuals(growthRenderer, config, safeGlobalIntensity);
            }

            DisableUnusedCachedPrimitives(primitiveObjects, growthCount);
        }

        private static Renderer CreateOrConfigureGrowthCachedHot(
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            int index,
            Bounds volumeBounds,
            Material growthMaterial,
            WallGrowthConfig config,
            float globalIntensity,
            long runtimeSeed)
        {
            if (primitiveObjects == null ||
                primitiveFilters == null ||
                primitiveRenderers == null ||
                (uint)index >= (uint)primitiveObjects.Length ||
                (uint)index >= (uint)primitiveFilters.Length ||
                (uint)index >= (uint)primitiveRenderers.Length)
            {
                return null;
            }

            GameObject primitiveObject = primitiveObjects[index];
            MeshFilter filter = primitiveFilters[index];
            MeshRenderer renderer = primitiveRenderers[index];
            if (primitiveObject == null || filter == null || renderer == null)
            {
                if (primitiveObject != null && primitiveObject.activeSelf)
                    primitiveObject.SetActive(false);
                return null;
            }

            bool ceilingBias = Hash01(runtimeSeed, index, 11) > 0.55f;
            float side = HashSigned(runtimeSeed, index, 17);
            float intensityT = CaveDressingRuntimeSanitizer.SaturateFinite(globalIntensity, 1f);
            float swayT = CaveDressingRuntimeSanitizer.SaturateFinite(config.swayAmount, 0.3f);
            float wallInset = math.lerp(0.14f, 0.32f, Hash01(runtimeSeed, index, 23));
            float forwardOffset = HashSigned(runtimeSeed, index, 31) * volumeBounds.extents.z * 0.72f;
            float verticalT = ceilingBias
                ? math.lerp(0.62f, 0.94f, Hash01(runtimeSeed, index, 43))
                : math.lerp(0.18f, 0.74f, Hash01(runtimeSeed, index, 43));
            float x = volumeBounds.center.x + Mathf.Sign(side) * volumeBounds.extents.x * (1f - wallInset);
            float y = math.lerp(volumeBounds.min.y, volumeBounds.max.y, verticalT);
            float z = volumeBounds.center.z + forwardOffset;
            float length = math.lerp(0.8f, 2.8f, Hash01(runtimeSeed, index, 59)) * math.lerp(0.8f, 1.15f, intensityT);
            float radius = math.lerp(0.12f, 0.42f, Hash01(runtimeSeed, index, 71)) * math.lerp(0.8f, 1.1f, CaveDressingRuntimeSanitizer.SaturateFinite(config.swayAmount + 0.2f, 0.5f));
            float yaw = HashSigned(runtimeSeed, index, 83) * 40f;
            float roll = HashSigned(runtimeSeed, index, 97) * math.lerp(8f, 26f, swayT);
            float pitch = ceilingBias
                ? math.lerp(100f, 150f, Hash01(runtimeSeed, index, 109))
                : math.lerp(-20f, 30f, Hash01(runtimeSeed, index, 109));
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 localScale = new Vector3(radius, length, radius);
            if (!CaveDressingRuntimeSanitizer.IsFinite(localPosition) ||
                !CaveDressingRuntimeSanitizer.IsFinite(localScale))
            {
                if (primitiveObject.activeSelf)
                    primitiveObject.SetActive(false);
                return null;
            }

            Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);

            if (!primitiveObject.activeSelf)
                primitiveObject.SetActive(true);

            return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualCachedHot(
                primitiveObject,
                filter,
                renderer,
                PrimitiveType.Capsule,
                GetCachedName(index),
                localPosition,
                localRotation,
                localScale,
                growthMaterial);
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

        private static void ApplyGrowthVisuals(Renderer renderer, WallGrowthConfig config, float globalIntensity)
        {
            if (renderer == null || config == null)
                return;

            Color growthColor = CaveDressingRuntimeSanitizer.SanitizeColor(config.growthColor, new Color(0.4f, 0.8f, 0.6f, 1f));
            float pulse = CaveDressingRuntimeSanitizer.SaturateFinite(config.pulseAmount, 0.2f);
            float intensity = CaveDressingRuntimeSanitizer.ClampFinite(
                globalIntensity,
                1f,
                0f,
                CaveDressingRuntimeSanitizer.MaxGlobalIntensity);
            Color baseColor = Color.Lerp(new Color(0.14f, 0.18f, 0.16f, 1f), growthColor, math.saturate(0.55f + pulse * 0.35f));
            Color emission = growthColor * math.lerp(0.15f, 1.4f, math.saturate(pulse * intensity));
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

            if (!CaveDressingRuntimeSanitizer.IsFinite(volumeBounds))
                return 0;

            float surfaceFactor = Mathf.Clamp01(
                (volumeBounds.size.x * volumeBounds.size.y + volumeBounds.size.z * volumeBounds.size.y) / 1200f);
            float intensity = CaveDressingRuntimeSanitizer.ClampFinite(globalIntensity, 0.1f, 0.1f, CaveDressingRuntimeSanitizer.MaxGlobalIntensity);
            float swayBias = math.lerp(0.65f, 1.15f, CaveDressingRuntimeSanitizer.SaturateFinite(config.swayAmount, 0.3f));
            return Mathf.Clamp(
                Mathf.RoundToInt(math.lerp(4f, 16f, Mathf.Max(complexity, surfaceFactor)) * swayBias * intensity),
                3,
                MaxGrowthCount);
        }

        private static Material ResolveGrowthMaterial(HectonVoxelVolume volume)
        {
            MeshRenderer renderer = volume != null ? volume.CachedMeshRenderer : null;
            if (renderer != null)
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
