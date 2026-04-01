using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    public readonly struct WorldGenerativeGeologyRequest
    {
        public WorldGenerativeGeologyRequest(
            long runtimeKey,
            int stableHash,
            WorldPrefabFamilyProfile family,
            WorldGenerativeGeologyProfile profile,
            bool finalVariantActive,
            float slopeDegrees,
            float curvature,
            float caveProximity,
            float ridgeSignal,
            float canyonSignal,
            float compositionPotential,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float worldScale)
        {
            RuntimeKey = runtimeKey;
            StableHash = stableHash;
            Family = family;
            Profile = profile;
            FinalVariantActive = finalVariantActive;
            SlopeDegrees = slopeDegrees;
            Curvature = curvature;
            CaveProximity = caveProximity;
            RidgeSignal = ridgeSignal;
            CanyonSignal = canyonSignal;
            CompositionPotential = compositionPotential;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            WorldScale = worldScale;
        }

        public long RuntimeKey { get; }
        public int StableHash { get; }
        public WorldPrefabFamilyProfile Family { get; }
        public WorldGenerativeGeologyProfile Profile { get; }
        public bool FinalVariantActive { get; }
        public float SlopeDegrees { get; }
        public float Curvature { get; }
        public float CaveProximity { get; }
        public float RidgeSignal { get; }
        public float CanyonSignal { get; }
        public float CompositionPotential { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public float WorldScale { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologyBinding : MonoBehaviour
    {
        [SerializeField] private long runtimeKey;
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string geologyProfileId = "geology.generic";
        [SerializeField] private string generatorMode = "Disabled";
        [SerializeField] private string archetype = "ComplexRock";
        [SerializeField] private string composition = "SingleFeature";
        [SerializeField] private string terrainSeam = "None";
        [SerializeField] private string caveBlend = "None";
        [SerializeField] private int lodCount;
        [SerializeField] private bool finalVariantActive;
        [SerializeField] private float slopeDegrees;
        [SerializeField] private float curvature;
        [SerializeField] private float caveProximity;
        [SerializeField] private float ridgeSignal;
        [SerializeField] private float canyonSignal;
        [SerializeField] private float compositionPotential;
        [SerializeField] private float seamBlendRadius;
        [SerializeField] private float suggestedTerrainRaise;
        [SerializeField] private float suggestedTerrainCut;
        [SerializeField] private int suggestedDebrisCount;

        public long RuntimeKey => runtimeKey;
        public string FamilyId => familyId;
        public string GeologyProfileId => geologyProfileId;
        public string GeneratorModeLabel => generatorMode;
        public string ArchetypeLabel => archetype;
        public string CompositionLabel => composition;
        public int LodCount => lodCount;
        public bool FinalVariantActive => finalVariantActive;
        public float SlopeDegrees => slopeDegrees;
        public float Curvature => curvature;
        public float CaveProximity => caveProximity;
        public float RidgeSignal => ridgeSignal;
        public float CanyonSignal => canyonSignal;
        public float CompositionPotential => compositionPotential;
        public float SeamBlendRadius => seamBlendRadius;
        public float SuggestedTerrainRaise => suggestedTerrainRaise;
        public float SuggestedTerrainCut => suggestedTerrainCut;
        public int SuggestedDebrisCount => suggestedDebrisCount;

        public WorldGenerativeGeologyProfile.ShapeArchetype Archetype
        {
            get
            {
                return Enum.TryParse(archetype, out WorldGenerativeGeologyProfile.ShapeArchetype resolvedArchetype)
                    ? resolvedArchetype
                    : WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock;
            }
        }

        public WorldGenerativeGeologyProfile.TerrainSeamMode TerrainSeamMode
        {
            get
            {
                return Enum.TryParse(terrainSeam, out WorldGenerativeGeologyProfile.TerrainSeamMode resolvedMode)
                    ? resolvedMode
                    : WorldGenerativeGeologyProfile.TerrainSeamMode.None;
            }
        }

        public WorldGenerativeGeologyProfile.CaveBlendMode CaveBlendMode
        {
            get
            {
                return Enum.TryParse(caveBlend, out WorldGenerativeGeologyProfile.CaveBlendMode resolvedMode)
                    ? resolvedMode
                    : WorldGenerativeGeologyProfile.CaveBlendMode.None;
            }
        }

        public void Configure(
            WorldGenerativeGeologyRequest request,
            string resolvedComposition,
            float resolvedBlendRadius,
            float resolvedTerrainRaise,
            float resolvedTerrainCut,
            int resolvedDebrisCount,
            int resolvedLodCount)
        {
            runtimeKey = request.RuntimeKey;
            familyId = request.Family != null ? request.Family.familyId : "world.family.generic";
            geologyProfileId = request.Profile != null ? request.Profile.profileId : "geology.generic";
            generatorMode = request.Profile != null ? request.Profile.generatorMode.ToString() : "Disabled";
            archetype = request.Profile != null ? request.Profile.shapeArchetype.ToString() : "ComplexRock";
            composition = string.IsNullOrWhiteSpace(resolvedComposition) ? "SingleFeature" : resolvedComposition;
            terrainSeam = request.Profile != null ? request.Profile.terrainSeamMode.ToString() : "None";
            caveBlend = request.Profile != null ? request.Profile.caveBlendMode.ToString() : "None";
            lodCount = resolvedLodCount;
            finalVariantActive = request.FinalVariantActive;
            slopeDegrees = request.SlopeDegrees;
            curvature = request.Curvature;
            caveProximity = request.CaveProximity;
            ridgeSignal = request.RidgeSignal;
            canyonSignal = request.CanyonSignal;
            compositionPotential = request.CompositionPotential;
            seamBlendRadius = resolvedBlendRadius;
            suggestedTerrainRaise = resolvedTerrainRaise;
            suggestedTerrainCut = resolvedTerrainCut;
            suggestedDebrisCount = resolvedDebrisCount;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologyService : MonoBehaviour
    {
        [DisallowMultipleComponent]
        private sealed class GeneratedRuntimeState : MonoBehaviour
        {
            [SerializeField] private int buildSignature;

            public int BuildSignature => buildSignature;

            public void Configure(int signature)
            {
                buildSignature = signature;
            }
        }

        private const string GeneratedRootName = "__GENERATED_GEOLOGY";

        [Header("Fallback Generation")]
        [SerializeField] private bool allowEditorGeneration = true;
        [SerializeField] private float primitiveThickness = 1.6f;
        [SerializeField] private float debrisScale = 0.28f;

        public bool TryApplyGeneratedGeology(GameObject host, in WorldGenerativeGeologyRequest request)
        {
            if (host == null || request.Profile == null || !request.Profile.IsEnabled)
                return false;

            if (!Application.isPlaying && !allowEditorGeneration)
                return false;

            string resolvedComposition = ResolveComposition(request);
            int lodCount = Mathf.Clamp(request.Profile.lodCount, 1, 3);
            float blendRadius = Mathf.Max(0.5f, request.Profile.seamBlendRadius * Mathf.Max(0.25f, request.WorldScale));
            float terrainRaise = request.Profile.terrainRaiseMeters * Mathf.Clamp01(request.RidgeSignal + request.CompositionPotential * 0.25f);
            float terrainCut = request.Profile.terrainCutMeters * Mathf.Clamp01(request.CaveProximity + request.CanyonSignal * 0.35f);
            int debrisCount = request.Profile.ResolveDebrisCount(request.StableHash);
            int buildSignature = ComputeBuildSignature(
                request,
                resolvedComposition,
                blendRadius,
                terrainRaise,
                terrainCut,
                debrisCount,
                lodCount);

            Transform generatedRoot = GetOrCreateGeneratedRoot(host.transform);
            GeneratedRuntimeState runtimeState = generatedRoot.GetComponent<GeneratedRuntimeState>();
            WorldGenerativeGeologyBinding binding = host.GetComponent<WorldGenerativeGeologyBinding>();
            if (runtimeState != null && runtimeState.BuildSignature == buildSignature && binding != null)
                return true;

            ClearGeneratedRoot(generatedRoot);
            StripHostPrimitiveVisuals(host);

            LODGroup lodGroup = generatedRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = generatedRoot.gameObject.AddComponent<LODGroup>();

            List<LOD> lods = new List<LOD>(lodCount);
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                Transform lodRoot = new GameObject($"LOD{lodIndex}").transform;
                lodRoot.SetParent(generatedRoot, false);
                Renderer[] renderers = BuildCompositionLod(lodRoot, request, resolvedComposition, lodIndex, debrisCount);
                float transitionHeight = ResolveLodScreenHeight(request.Profile, lodIndex, lodCount);
                lods.Add(new LOD(transitionHeight, renderers));
            }

            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();

            if (binding == null)
                binding = host.AddComponent<WorldGenerativeGeologyBinding>();

            binding.Configure(
                request,
                resolvedComposition,
                blendRadius,
                terrainRaise,
                terrainCut,
                debrisCount,
                lodCount);

            if (runtimeState == null)
                runtimeState = generatedRoot.gameObject.AddComponent<GeneratedRuntimeState>();

            runtimeState.Configure(buildSignature);

            return true;
        }

        public void ClearGeneratedGeology(GameObject host)
        {
            if (host == null)
                return;

            Transform generatedRoot = host.transform.Find(GeneratedRootName);
            if (generatedRoot == null)
                return;

            DestroyGeneratedObject(generatedRoot.gameObject);
        }

        private Renderer[] BuildCompositionLod(
            Transform lodRoot,
            in WorldGenerativeGeologyRequest request,
            string composition,
            int lodIndex,
            int debrisCount)
        {
            List<Renderer> renderers = new List<Renderer>(12);
            float lodScale = Mathf.Lerp(1f, 0.7f, lodIndex / 2f);

            switch (request.Profile.shapeArchetype)
            {
                case WorldGenerativeGeologyProfile.ShapeArchetype.Arch:
                case WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge:
                    BuildArch(renderers, lodRoot, request, composition, lodScale, lodIndex);
                    break;

                case WorldGenerativeGeologyProfile.ShapeArchetype.Canopy:
                    BuildCanopy(renderers, lodRoot, request, composition, lodScale, lodIndex);
                    break;

                default:
                    BuildRockPack(renderers, lodRoot, request, composition, lodScale, lodIndex);
                    break;
            }

            if (lodIndex == 0 && request.Profile.terrainSeamMode != WorldGenerativeGeologyProfile.TerrainSeamMode.None)
                BuildDebris(renderers, lodRoot, request, debrisCount);

            return renderers.ToArray();
        }

        private void BuildArch(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex)
        {
            float width = Mathf.Lerp(10f, 5f, lodIndex / 2f) * request.WorldScale;
            float height = Mathf.Lerp(7f, 4f, lodIndex / 2f) * request.WorldScale;
            float thickness = primitiveThickness * request.WorldScale * lodScale;

            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(-width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness));
            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness));
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, 6f, lodIndex / 2f)), new Vector3(width, thickness, thickness * 1.1f));

            if (composition == "ContextPack" && lodIndex == 0)
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.55f, width * 0.22f), Quaternion.Euler(0f, 24f, -14f), new Vector3(width * 0.42f, thickness * 0.8f, thickness));
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.42f, -width * 0.24f), Quaternion.Euler(0f, -22f, 10f), new Vector3(width * 0.36f, thickness * 0.75f, thickness));
            }
        }

        private void BuildCanopy(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex)
        {
            float span = Mathf.Lerp(12f, 6f, lodIndex / 2f) * request.WorldScale;
            float shelfThickness = primitiveThickness * request.WorldScale * lodScale;
            float height = Mathf.Lerp(4.5f, 2.2f, lodIndex / 2f) * request.WorldScale;

            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(0f, height * 0.65f, 0f), Quaternion.identity, new Vector3(shelfThickness * 1.1f, height, shelfThickness * 1.1f));
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 18f, request.CanyonSignal * 14f), new Vector3(span, shelfThickness, span * 0.55f));

            if (composition != "SingleFeature")
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(span * 0.18f, height * 0.82f, span * 0.16f), Quaternion.Euler(0f, -16f, 8f), new Vector3(span * 0.56f, shelfThickness * 0.8f, span * 0.28f));
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(-span * 0.22f, height * 0.72f, -span * 0.2f), Quaternion.Euler(0f, 24f, -10f), new Vector3(span * 0.42f, shelfThickness * 0.75f, span * 0.22f));
            }
        }

        private void BuildRockPack(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex)
        {
            float baseScale = Mathf.Lerp(6f, 3f, lodIndex / 2f) * request.WorldScale;
            CreatePrimitive(renderers, root, PrimitiveType.Sphere, new Vector3(0f, baseScale * 0.45f, 0f), Quaternion.identity, Vector3.one * baseScale);
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(baseScale * 0.36f, baseScale * 0.52f, -baseScale * 0.18f), Quaternion.Euler(18f, 22f, 12f), new Vector3(baseScale * 0.9f, baseScale * 0.45f, baseScale * 0.64f));

            if (composition != "SingleFeature")
            {
                CreatePrimitive(renderers, root, PrimitiveType.Sphere, new Vector3(-baseScale * 0.42f, baseScale * 0.34f, baseScale * 0.24f), Quaternion.identity, Vector3.one * (baseScale * 0.7f));
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, baseScale * 0.92f, 0f), Quaternion.Euler(-8f, 32f, 20f), new Vector3(baseScale * 0.55f, baseScale * 0.18f, baseScale * 0.42f));
            }
        }

        private void BuildDebris(List<Renderer> renderers, Transform root, in WorldGenerativeGeologyRequest request, int debrisCount)
        {
            float radius = Mathf.Max(2f, request.Profile.seamBlendRadius * 0.22f) * request.WorldScale;
            int count = Mathf.Max(0, debrisCount);
            for (int i = 0; i < count; i++)
            {
                float angle = ((i + 1) / (float)(count + 1)) * 360f + (request.StableHash % 37);
                float distance = Mathf.Lerp(radius * 0.2f, radius, (i + 1) / (float)(count + 1));
                Vector3 localPos = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * distance);
                localPos.y = debrisScale * request.WorldScale;
                float scale = Mathf.Lerp(0.35f, 1f, ((i % 3) + 1) / 3f) * debrisScale * request.WorldScale * 4f;
                PrimitiveType primitive = (i % 2 == 0) ? PrimitiveType.Sphere : PrimitiveType.Cube;
                CreatePrimitive(renderers, root, primitive, localPos, Quaternion.Euler(11f * i, angle, 17f), Vector3.one * scale);
            }
        }

        private static float ResolveLodScreenHeight(WorldGenerativeGeologyProfile profile, int lodIndex, int lodCount)
        {
            Vector3 heights = profile != null ? profile.lodScreenHeights : new Vector3(0.65f, 0.28f, 0.08f);
            return lodIndex switch
            {
                0 => Mathf.Clamp01(heights.x),
                1 => lodCount > 1 ? Mathf.Clamp01(heights.y) : 0.01f,
                _ => Mathf.Clamp01(heights.z)
            };
        }

        private string ResolveComposition(in WorldGenerativeGeologyRequest request)
        {
            if (request.Profile == null)
                return "SingleFeature";

            if (request.Profile.PreferContextPack(request.CompositionPotential))
                return "ContextPack";

            return request.Profile.compositionMode == WorldGenerativeGeologyProfile.CompositionMode.PairedFeature
                ? "PairedFeature"
                : "SingleFeature";
        }

        private static int ComputeBuildSignature(
            in WorldGenerativeGeologyRequest request,
            string resolvedComposition,
            float blendRadius,
            float terrainRaise,
            float terrainCut,
            int debrisCount,
            int lodCount)
        {
            unchecked
            {
                int hash = (int)request.RuntimeKey;
                hash = (hash * 397) ^ request.StableHash;
                hash = (hash * 397) ^ (request.Family != null ? request.Family.familyId.GetHashCode() : 0);
                hash = (hash * 397) ^ (request.Profile != null ? request.Profile.profileId.GetHashCode() : 0);
                hash = (hash * 397) ^ (resolvedComposition != null ? resolvedComposition.GetHashCode() : 0);
                hash = (hash * 397) ^ lodCount;
                hash = (hash * 397) ^ Mathf.RoundToInt(request.WorldScale * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(blendRadius * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(terrainRaise * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(terrainCut * 100f);
                hash = (hash * 397) ^ debrisCount;
                hash = (hash * 397) ^ (request.FinalVariantActive ? 1 : 0);
                return hash;
            }
        }

        private static Transform GetOrCreateGeneratedRoot(Transform host)
        {
            Transform existing = host.Find(GeneratedRootName);
            if (existing != null)
                return existing;

            Transform created = new GameObject(GeneratedRootName).transform;
            created.SetParent(host, false);
            return created;
        }

        private static void ClearGeneratedRoot(Transform generatedRoot)
        {
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                DestroyGeneratedObject(generatedRoot.GetChild(i).gameObject);

            LODGroup lodGroup = generatedRoot.GetComponent<LODGroup>();
            if (lodGroup != null)
                DestroyGeneratedObject(lodGroup);
        }

        private void CreatePrimitive(
            List<Renderer> renderers,
            Transform parent,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = primitiveType.ToString();
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                DestroyGeneratedObject(collider);

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
                renderers.Add(renderer);
        }

        private static void StripHostPrimitiveVisuals(GameObject host)
        {
            if (host == null || host.transform.childCount > 1)
                return;

            MeshRenderer renderer = host.GetComponent<MeshRenderer>();
            MeshFilter filter = host.GetComponent<MeshFilter>();
            Collider collider = host.GetComponent<Collider>();

            if (collider != null)
                DestroyGeneratedObject(collider);

            if (renderer != null)
                DestroyGeneratedObject(renderer);

            if (filter != null)
                DestroyGeneratedObject(filter);
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
