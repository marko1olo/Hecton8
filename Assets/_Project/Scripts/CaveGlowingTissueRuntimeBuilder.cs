using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveGlowingTissueRuntimeBuilder
    {
        private const string TissueRootName = "_GlowingTissue";
        private static readonly string[] _TissueNames = CreateNameCache("Tissue_", 24); // COLD ALLOC: bounded glowing-tissue child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly MaterialPropertyBlock _TissuePropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: shared glowing-tissue block.

        public static void Build(
            Transform parent,
            HectonVoxelVolume volume,
            CavePreset preset,
            GlowingTissueConfig config,
            float globalIntensity)
        {
            if (parent == null || volume == null || config == null || !config.enabled)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds))
                return;

            Transform tissueRoot = GetOrCreateRoot(parent);
            Material tissueMaterial = ResolveTissueMaterial(volume);
            long runtimeSeed = volume.caveKey != 0L ? volume.caveKey : ComputeFallbackSeed(volume.transform.position, preset);
            int tissueCount = ResolveTissueCount(preset, volumeBounds, config, globalIntensity);

            for (int i = 0; i < tissueCount; i++)
            {
                Renderer renderer = CreateOrConfigureTissue(
                    tissueRoot,
                    i,
                    volumeBounds,
                    tissueMaterial,
                    runtimeSeed,
                    config,
                    globalIntensity);
                ApplyTissueVisuals(renderer, config, globalIntensity, runtimeSeed, i);
            }

            DisableUnusedChildren(tissueRoot, tissueCount);
        }

        private static Renderer CreateOrConfigureTissue(
            Transform root,
            int index,
            Bounds volumeBounds,
            Material tissueMaterial,
            long runtimeSeed,
            GlowingTissueConfig config,
            float globalIntensity)
        {
            string name = GetCachedName(index);
            bool ceilingBias = Hash01(runtimeSeed, index, 11) > 0.35f;
            float side = HashSigned(runtimeSeed, index, 17);
            float wallInset = Mathf.Lerp(0.06f, 0.22f, Hash01(runtimeSeed, index, 23));
            float verticalT = ceilingBias
                ? Mathf.Lerp(0.52f, 0.94f, Hash01(runtimeSeed, index, 31))
                : Mathf.Lerp(0.18f, 0.68f, Hash01(runtimeSeed, index, 31));
            float x = volumeBounds.center.x + Mathf.Sign(side) * volumeBounds.extents.x * (1f - wallInset);
            float y = Mathf.Lerp(volumeBounds.min.y, volumeBounds.max.y, verticalT);
            float z = volumeBounds.center.z + HashSigned(runtimeSeed, index, 43) * volumeBounds.extents.z * 0.76f;
            float width = Mathf.Lerp(0.2f, 0.8f, Hash01(runtimeSeed, index, 59)) * Mathf.Lerp(0.85f, 1.2f, globalIntensity);
            float height = Mathf.Lerp(width * 0.55f, width * 1.4f, Hash01(runtimeSeed, index, 71));
            float thickness = Mathf.Lerp(0.08f, 0.24f, Hash01(runtimeSeed, index, 83));
            float yaw = HashSigned(runtimeSeed, index, 97) * 42f;
            float roll = HashSigned(runtimeSeed, index, 109) * 18f;
            float pitch = ceilingBias
                ? Mathf.Lerp(92f, 138f, Hash01(runtimeSeed, index, 127))
                : Mathf.Lerp(-18f, 26f, Hash01(runtimeSeed, index, 127));
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 localScale = new Vector3(width, height, thickness);
            Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);

            if (index < root.childCount)
            {
                Transform existing = root.GetChild(index);
                ActivateTransform(existing);
                return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                    existing.gameObject,
                    PrimitiveType.Quad,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    tissueMaterial);
            }

            return WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                root,
                PrimitiveType.Quad,
                name,
                localPosition,
                localRotation,
                localScale,
                tissueMaterial);
        }

        private static void ApplyTissueVisuals(Renderer renderer, GlowingTissueConfig config, float globalIntensity, long runtimeSeed, int index)
        {
            if (renderer == null || config == null)
                return;

            float glowFactor = Mathf.Lerp(0.45f, 1.25f, Hash01(runtimeSeed, index, 149)) * Mathf.Lerp(0.85f, 1.2f, globalIntensity);
            Color baseColor = Color.Lerp(config.baseColor, config.glowColor, 0.42f);
            Color emission = config.glowColor * glowFactor * Mathf.Lerp(0.35f, 1.35f, config.pulseAmount);
            _TissuePropertyBlock.Clear();
            renderer.GetPropertyBlock(_TissuePropertyBlock);
            _TissuePropertyBlock.SetColor(_BaseColorId, baseColor);
            _TissuePropertyBlock.SetColor(_ColorId, baseColor);
            _TissuePropertyBlock.SetColor(_EmissionColorId, emission);
            renderer.SetPropertyBlock(_TissuePropertyBlock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static int ResolveTissueCount(
            CavePreset preset,
            Bounds volumeBounds,
            GlowingTissueConfig config,
            float globalIntensity)
        {
            int maxCount = Mathf.Clamp(config.maxCount, 0, 24);
            if (maxCount <= 0)
                return 0;

            float complexity = preset != null ? Mathf.Clamp01((preset.maxRooms + preset.maxStructures) / 24f) : 0.45f;
            float verticalSurface = Mathf.Clamp01((volumeBounds.size.x * volumeBounds.size.y + volumeBounds.size.z * volumeBounds.size.y) / 1400f);
            float intensity = Mathf.Clamp(globalIntensity, 0.1f, 1.25f);
            float density = Mathf.Clamp01(config.density);
            return Mathf.Clamp(
                Mathf.RoundToInt(maxCount * Mathf.Max(complexity, verticalSurface) * Mathf.Lerp(0.55f, 1.15f, density) * intensity),
                1,
                maxCount);
        }

        private static Material ResolveTissueMaterial(HectonVoxelVolume volume)
        {
            if (volume != null && volume.TryGetComponent(out MeshRenderer renderer))
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
