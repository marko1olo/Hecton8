using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveGlowingTissueRuntimeBuilder
    {
        public const int RuntimeCapacity = 24;
        private const string TissueRootName = "_GlowingTissue";
        private const int MaxTissueCount = RuntimeCapacity;
        private static readonly string[] _TissueNames = CreateNameCache("Tissue_", MaxTissueCount); // COLD ALLOC: bounded glowing-tissue child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _TissuePropertyBlock;

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
            for (int i = 0; i < MaxTissueCount; i++)
            {
                if (i < root.childCount)
                {
                    GameObject primitiveObject = root.GetChild(i).gameObject;
                    WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                        primitiveObject,
                        PrimitiveType.Quad,
                        GetCachedName(i),
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
                    CachePrimitiveCold(i, primitiveObject, primitiveObjects, primitiveFilters, primitiveRenderers);
                    continue;
                }

                Renderer renderer = WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    root,
                    PrimitiveType.Quad,
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
            _ = GetTissuePropertyBlock();
        }

        public static void BuildPreparedCachedHot(
            Transform tissueRoot,
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            HectonVoxelVolume volume,
            CavePreset preset,
            GlowingTissueConfig config,
            float globalIntensity)
        {
            if (tissueRoot == null || volume == null || config == null || !config.enabled)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds) ||
                !CaveDressingRuntimeSanitizer.IsFinite(volumeBounds))
            {
                DisableUnusedCachedPrimitives(primitiveObjects, 0);
                return;
            }

            Material tissueMaterial = ResolveTissueMaterial(volume);
            float safeGlobalIntensity = CaveDressingRuntimeSanitizer.ClampFinite(
                globalIntensity,
                1f,
                0f,
                CaveDressingRuntimeSanitizer.MaxGlobalIntensity);
            long runtimeSeed = volume.caveKey != 0L ? volume.caveKey : ComputeFallbackSeed(CaveDressingRuntimeSanitizer.SeedPosition(volume.transform.position), preset);
            int tissueCount = ResolveTissueCount(preset, volumeBounds, config, safeGlobalIntensity);
            ActivateTransform(tissueRoot);

            for (int i = 0; i < tissueCount; i++)
            {
                Renderer renderer = CreateOrConfigureTissueCachedHot(
                    primitiveObjects,
                    primitiveFilters,
                    primitiveRenderers,
                    i,
                    volumeBounds,
                    tissueMaterial,
                    runtimeSeed,
                    config,
                    safeGlobalIntensity);
                ApplyTissueVisuals(renderer, config, safeGlobalIntensity, runtimeSeed, i);
            }

            DisableUnusedCachedPrimitives(primitiveObjects, tissueCount);
        }

        private static Renderer CreateOrConfigureTissueCachedHot(
            GameObject[] primitiveObjects,
            MeshFilter[] primitiveFilters,
            MeshRenderer[] primitiveRenderers,
            int index,
            Bounds volumeBounds,
            Material tissueMaterial,
            long runtimeSeed,
            GlowingTissueConfig config,
            float globalIntensity)
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

            bool ceilingBias = Hash01(runtimeSeed, index, 11) > 0.35f;
            float side = HashSigned(runtimeSeed, index, 17);
            float intensityT = CaveDressingRuntimeSanitizer.SaturateFinite(globalIntensity, 1f);
            float wallInset = math.lerp(0.06f, 0.22f, Hash01(runtimeSeed, index, 23));
            float verticalT = ceilingBias
                ? math.lerp(0.52f, 0.94f, Hash01(runtimeSeed, index, 31))
                : math.lerp(0.18f, 0.68f, Hash01(runtimeSeed, index, 31));
            float x = volumeBounds.center.x + Mathf.Sign(side) * volumeBounds.extents.x * (1f - wallInset);
            float y = math.lerp(volumeBounds.min.y, volumeBounds.max.y, verticalT);
            float z = volumeBounds.center.z + HashSigned(runtimeSeed, index, 43) * volumeBounds.extents.z * 0.76f;
            float width = math.lerp(0.2f, 0.8f, Hash01(runtimeSeed, index, 59)) * math.lerp(0.85f, 1.2f, intensityT);
            float height = math.lerp(width * 0.55f, width * 1.4f, Hash01(runtimeSeed, index, 71));
            float thickness = math.lerp(0.08f, 0.24f, Hash01(runtimeSeed, index, 83));
            float yaw = HashSigned(runtimeSeed, index, 97) * 42f;
            float roll = HashSigned(runtimeSeed, index, 109) * 18f;
            float pitch = ceilingBias
                ? math.lerp(92f, 138f, Hash01(runtimeSeed, index, 127))
                : math.lerp(-18f, 26f, Hash01(runtimeSeed, index, 127));
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 localScale = new Vector3(width, height, thickness);
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
                PrimitiveType.Quad,
                GetCachedName(index),
                localPosition,
                localRotation,
                localScale,
                tissueMaterial);
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

        private static void ApplyTissueVisuals(Renderer renderer, GlowingTissueConfig config, float globalIntensity, long runtimeSeed, int index)
        {
            if (renderer == null || config == null)
                return;

            float intensityT = CaveDressingRuntimeSanitizer.SaturateFinite(globalIntensity, 1f);
            float glowFactor = math.lerp(0.45f, 1.25f, Hash01(runtimeSeed, index, 149)) * math.lerp(0.85f, 1.2f, intensityT);
            Color tissueBaseColor = CaveDressingRuntimeSanitizer.SanitizeColor(config.baseColor, new Color(0.12f, 0.2f, 0.16f, 1f));
            Color tissueGlowColor = CaveDressingRuntimeSanitizer.SanitizeColor(config.glowColor, new Color(0.22f, 0.95f, 0.86f, 1f));
            Color baseColor = Color.Lerp(tissueBaseColor, tissueGlowColor, 0.42f);
            Color emission = tissueGlowColor * glowFactor * math.lerp(0.35f, 1.35f, CaveDressingRuntimeSanitizer.SaturateFinite(config.pulseAmount, 0.3f));
            MaterialPropertyBlock propertyBlock = GetTissuePropertyBlock();
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(_BaseColorId, baseColor);
            propertyBlock.SetColor(_ColorId, baseColor);
            propertyBlock.SetColor(_EmissionColorId, emission);
            renderer.SetPropertyBlock(propertyBlock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static MaterialPropertyBlock GetTissuePropertyBlock()
        {
            if (_TissuePropertyBlock != null)
                return _TissuePropertyBlock;

            // COLD ALLOC: MaterialPropertyBlock[1] — shared glowing-tissue block — owner: CaveGlowingTissueRuntimeBuilder
            _TissuePropertyBlock = new MaterialPropertyBlock();
            return _TissuePropertyBlock;
        }

        private static int ResolveTissueCount(
            CavePreset preset,
            Bounds volumeBounds,
            GlowingTissueConfig config,
            float globalIntensity)
        {
            int maxCount = Mathf.Clamp(config.maxCount, 0, MaxTissueCount);
            if (maxCount <= 0)
                return 0;

            if (!CaveDressingRuntimeSanitizer.IsFinite(volumeBounds))
                return 0;

            float complexity = preset != null ? Mathf.Clamp01((preset.maxRooms + preset.maxStructures) / 24f) : 0.45f;
            float verticalSurface = Mathf.Clamp01((volumeBounds.size.x * volumeBounds.size.y + volumeBounds.size.z * volumeBounds.size.y) / 1400f);
            float intensity = CaveDressingRuntimeSanitizer.ClampFinite(globalIntensity, 0.1f, 0.1f, CaveDressingRuntimeSanitizer.MaxGlobalIntensity);
            float density = CaveDressingRuntimeSanitizer.SaturateFinite(config.density, 0.5f);
            return Mathf.Clamp(
                Mathf.RoundToInt(maxCount * Mathf.Max(complexity, verticalSurface) * math.lerp(0.55f, 1.15f, density) * intensity),
                1,
                maxCount);
        }

        private static Material ResolveTissueMaterial(HectonVoxelVolume volume)
        {
            MeshRenderer renderer = volume != null ? volume.CachedMeshRenderer : null;
            if (renderer != null)
                return renderer.sharedMaterial;

            return null;
        }

        private static Transform GetOrCreateRoot(Transform parent)
        {
            Transform root = parent.Find(TissueRootName);
            if (root != null)
            {
                ActivateTransform(root);
                return root;
            }

            GameObject rootObject = new GameObject(TissueRootName);
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
            if ((uint)index < (uint)_TissueNames.Length)
                return _TissueNames[index];

            return TissueRootName;
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
            int presetHash = preset != null ? preset.maxRooms * 211 ^ preset.maxStructures * 37 : 0;
            return ((long)x << 42) ^ ((long)y << 21) ^ (uint)z ^ (uint)presetHash;
        }

        private static float Hash01(long runtimeSeed, int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeSeed * 747796405L + index * 2891336453L + salt * 92821L);
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
