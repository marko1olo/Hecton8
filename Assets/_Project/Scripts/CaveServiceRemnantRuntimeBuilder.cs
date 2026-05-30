using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Caves
{
    internal static class CaveServiceRemnantRuntimeBuilder
    {
        private const string RemnantRootName = "_ServiceRemnants";
        private const int MaxRemnantCount = 12;
        private static readonly string[] _RemnantNames = CreateNameCache("Remnant_", MaxRemnantCount); // COLD ALLOC: bounded remnant child names.
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _RemnantPropertyBlock;

        public static Transform Prewarm(Transform parent)
        {
            if (parent == null)
                return null;

            Transform root = GetOrCreateRoot(parent);
            for (int i = 0; i < MaxRemnantCount; i++)
            {
                if (i < root.childCount)
                {
                    WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                        root.GetChild(i).gameObject,
                        PrimitiveType.Cube,
                        GetCachedName(i),
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
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
                    renderer.gameObject.SetActive(false);
            }

            DisableAll(root);
            return root;
        }

        public static void PrewarmSharedResources()
        {
            _ = GetRemnantPropertyBlock();
        }

        public static void Build(
            Transform parent,
            HectonVoxelVolume volume,
            CavePreset preset,
            ServiceRemnantConfig config,
            float globalIntensity)
        {
            if (parent == null || volume == null || preset == null || config == null || !config.enabled)
                return;

            Transform remnantRoot = GetOrCreateRoot(parent);
            BuildPrepared(remnantRoot, volume, preset, config, globalIntensity, createMissing: true);
        }

        public static void BuildPrepared(
            Transform remnantRoot,
            HectonVoxelVolume volume,
            CavePreset preset,
            ServiceRemnantConfig config,
            float globalIntensity)
        {
            BuildPrepared(remnantRoot, volume, preset, config, globalIntensity, createMissing: false);
        }

        private static void BuildPrepared(
            Transform remnantRoot,
            HectonVoxelVolume volume,
            CavePreset preset,
            ServiceRemnantConfig config,
            float globalIntensity,
            bool createMissing)
        {
            if (remnantRoot == null || volume == null || preset == null || config == null || !config.enabled)
                return;

            if (config.ruinLinkedOnly && !preset.isRuinLinked)
            {
                DisableAll(remnantRoot);
                return;
            }

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds volumeBounds))
                return;

            Material remnantMaterial = ResolveRemnantMaterial(volume);
            long runtimeSeed = volume.caveKey != 0L ? volume.caveKey : ComputeFallbackSeed(volume.transform.position, preset);
            int remnantCount = ResolveRemnantCount(preset, volumeBounds, config, globalIntensity);

            for (int i = 0; i < remnantCount; i++)
            {
                Renderer renderer = CreateOrConfigureRemnant(
                    remnantRoot,
                    i,
                    volumeBounds,
                    remnantMaterial,
                    runtimeSeed,
                    config,
                    globalIntensity,
                    createMissing);
                ApplyRemnantVisuals(renderer, config, runtimeSeed, i);
            }

            DisableUnusedChildren(remnantRoot, remnantCount);
        }

        private static Renderer CreateOrConfigureRemnant(
            Transform root,
            int index,
            Bounds volumeBounds,
            Material remnantMaterial,
            long runtimeSeed,
            ServiceRemnantConfig config,
            float globalIntensity,
            bool createMissing)
        {
            string name = GetCachedName(index);
            bool cylindrical = Hash01(runtimeSeed, index, 11) > 0.45f;
            PrimitiveType primitiveType = cylindrical ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            float intensityT = math.saturate(globalIntensity);
            float x = volumeBounds.center.x + HashSigned(runtimeSeed, index, 17) * volumeBounds.extents.x * 0.62f;
            float z = volumeBounds.center.z + HashSigned(runtimeSeed, index, 23) * volumeBounds.extents.z * 0.62f;
            float y = math.lerp(volumeBounds.min.y, volumeBounds.center.y, math.lerp(0.05f, 0.28f, Hash01(runtimeSeed, index, 31)));
            float width = math.lerp(config.minScale, config.maxScale, Hash01(runtimeSeed, index, 43)) * math.lerp(0.82f, 1.18f, intensityT);
            float height = cylindrical
                ? math.lerp(width * 0.8f, width * 2.4f, Hash01(runtimeSeed, index, 59))
                : math.lerp(width * 0.35f, width * 1.2f, Hash01(runtimeSeed, index, 59));
            float depth = cylindrical
                ? width
                : math.lerp(width * 0.4f, width * 1.8f, Hash01(runtimeSeed, index, 71));
            float yaw = HashSigned(runtimeSeed, index, 83) * 180f;
            float pitch = HashSigned(runtimeSeed, index, 97) * (cylindrical ? 80f : 24f);
            float roll = HashSigned(runtimeSeed, index, 109) * (cylindrical ? 80f : 28f);
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 localScale = new Vector3(width, height, depth);
            Quaternion localRotation = Quaternion.Euler(pitch, yaw, roll);

            if (index < root.childCount)
            {
                Transform existing = root.GetChild(index);
                ActivateTransform(existing);
                return WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot(
                    existing.gameObject,
                    primitiveType,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    remnantMaterial);
            }

            if (!createMissing)
                return null;

            return WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                root,
                primitiveType,
                name,
                localPosition,
                localRotation,
                localScale,
                remnantMaterial);
        }

        private static void ApplyRemnantVisuals(Renderer renderer, ServiceRemnantConfig config, long runtimeSeed, int index)
        {
            if (renderer == null || config == null)
                return;

            float accent = math.lerp(0.18f, 0.82f, Hash01(runtimeSeed, index, 127));
            Color baseColor = Color.Lerp(config.baseColor, config.accentColor, accent * 0.25f);
            Color emission = config.accentColor * (config.accentEmission * accent);
            MaterialPropertyBlock propertyBlock = GetRemnantPropertyBlock();
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(_BaseColorId, baseColor);
            propertyBlock.SetColor(_ColorId, baseColor);
            propertyBlock.SetColor(_EmissionColorId, emission);
            renderer.SetPropertyBlock(propertyBlock);
            bool castShadows = ResolveShadowCasting(renderer);
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
        }

        private static MaterialPropertyBlock GetRemnantPropertyBlock()
        {
            if (_RemnantPropertyBlock != null)
                return _RemnantPropertyBlock;

            // COLD ALLOC: MaterialPropertyBlock[1] — shared service-remnant block — owner: CaveServiceRemnantRuntimeBuilder
            _RemnantPropertyBlock = new MaterialPropertyBlock();
            return _RemnantPropertyBlock;
        }

        private static int ResolveRemnantCount(
            CavePreset preset,
            Bounds volumeBounds,
            ServiceRemnantConfig config,
            float globalIntensity)
        {
            int maxCount = Mathf.Clamp(config.maxCount, 0, MaxRemnantCount);
            if (maxCount <= 0)
                return 0;

            float complexity = Mathf.Clamp01((preset.maxRooms + preset.maxStructures) / 28f);
            float footprint = Mathf.Clamp01((volumeBounds.size.x * volumeBounds.size.z) / 1000f);
            float intensity = Mathf.Clamp(globalIntensity, 0.1f, 1.25f);
            return Mathf.Clamp(
                Mathf.RoundToInt(maxCount * Mathf.Max(complexity, footprint) * intensity),
                1,
                maxCount);
        }

        private static bool ResolveShadowCasting(Renderer renderer)
        {
            if (renderer == null)
                return false;

            Vector3 boundsSize = renderer.bounds.size;
            float maxDimension = Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z));
            return maxDimension >= 0.5f;
        }

        private static Material ResolveRemnantMaterial(HectonVoxelVolume volume)
        {
            MeshRenderer renderer = volume != null ? volume.CachedMeshRenderer : null;
            if (renderer != null)
                return renderer.sharedMaterial;

            return null;
        }

        private static Transform GetOrCreateRoot(Transform parent)
        {
            Transform root = parent.Find(RemnantRootName);
            if (root != null)
            {
                ActivateTransform(root);
                return root;
            }

            GameObject rootObject = new GameObject(RemnantRootName);
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

        private static void DisableAll(Transform root)
        {
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }

            if (root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        private static void ActivateTransform(Transform target)
        {
            if (target != null && !target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
        }

        private static string GetCachedName(int index)
        {
            if ((uint)index < (uint)_RemnantNames.Length)
                return _RemnantNames[index];

            return RemnantRootName;
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
            int presetHash = preset != null ? preset.maxRooms * 313 ^ preset.maxStructures * 41 : 0;
            return ((long)x << 42) ^ ((long)y << 21) ^ (uint)z ^ (uint)presetHash;
        }

        private static float Hash01(long runtimeSeed, int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeSeed * 196613L + index * 161803399L + salt * 92821L);
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
