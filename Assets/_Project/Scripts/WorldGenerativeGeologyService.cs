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
        private const string GeneratorModeDisabledLabel = "Disabled";
        private const string GeneratorModeNeuralPreferredLabel = "NeuralPreferred";
        private const string GeneratorModeHeuristicSdfFallbackLabel = "HeuristicSdfFallback";
        private const string ArchetypeArchLabel = "Arch";
        private const string ArchetypeCanopyLabel = "Canopy";
        private const string ArchetypeComplexRockLabel = "ComplexRock";
        private const string ArchetypeArchClusterLabel = "ArchCluster";
        private const string ArchetypeReefPackLabel = "ReefPack";
        private const string ArchetypeCaveBridgeLabel = "CaveBridge";
        private const string TerrainSeamNoneLabel = "None";
        private const string TerrainSeamHeightBlendLabel = "HeightBlend";
        private const string TerrainSeamSdfBlendLabel = "SdfBlend";
        private const string TerrainSeamDebrisBridgeLabel = "DebrisBridge";
        private const string TerrainSeamCarveAndDebrisLabel = "CarveAndDebris";
        private const string CaveBlendNoneLabel = "None";
        private const string CaveBlendProbeOnlyLabel = "ProbeOnly";
        private const string CaveBlendSdfBlendLabel = "SdfBlend";
        private const string CaveBlendCarvePortalLabel = "CarvePortal";

        private static readonly List<WorldGenerativeGeologyBinding> _activeBindings = new List<WorldGenerativeGeologyBinding>(256);
        private static readonly List<int> _staleBindingIndexBuffer = new List<int>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeBindings.Clear();
            _staleBindingIndexBuffer.Clear();
        }

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

        private WorldProceduralProxyInstance _cachedProxyInstance;

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
        internal WorldProceduralProxyInstance CachedProxyInstance => _cachedProxyInstance;
        internal long CachedProxyRuntimeKey => _cachedProxyInstance != null ? _cachedProxyInstance.RuntimeKey : 0L;
        public static int ActiveBindingCount => _activeBindings.Count;

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

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            RegisterActiveBinding(this);
        }

        private void OnDisable()
        {
            UnregisterActiveBinding(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveBinding(this);
        }

        private void CacheReferences()
        {
            if (!TryGetComponent(out _cachedProxyInstance))
                _cachedProxyInstance = null;
        }

        public static void CopyActiveBindingsTo(List<WorldGenerativeGeologyBinding> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            _staleBindingIndexBuffer.Clear();
            for (int i = 0; i < _activeBindings.Count; i++)
            {
                WorldGenerativeGeologyBinding binding = _activeBindings[i];
                if (binding == null || !binding.isActiveAndEnabled)
                {
                    _staleBindingIndexBuffer.Add(i);
                    continue;
                }

                destination.Add(binding);
            }

            TrimStaleActiveBindings();
        }

        public static bool TryGetActiveBinding(long runtimeKey, out WorldGenerativeGeologyBinding binding)
        {
            binding = null;
            if (runtimeKey == 0L)
                return false;

            _staleBindingIndexBuffer.Clear();
            for (int i = 0; i < _activeBindings.Count; i++)
            {
                WorldGenerativeGeologyBinding candidate = _activeBindings[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    _staleBindingIndexBuffer.Add(i);
                    continue;
                }

                if (candidate.runtimeKey != runtimeKey)
                    continue;

                binding = candidate;
                TrimStaleActiveBindings();
                return true;
            }

            TrimStaleActiveBindings();
            return false;
        }

        private static void RegisterActiveBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null || _activeBindings.Contains(binding))
                return;

            _activeBindings.Add(binding);
        }

        private static void UnregisterActiveBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null)
                return;

            _activeBindings.Remove(binding);
        }

        private static void TrimStaleActiveBindings()
        {
            for (int i = _staleBindingIndexBuffer.Count - 1; i >= 0; i--)
            {
                int index = _staleBindingIndexBuffer[i];
                if (index < 0 || index >= _activeBindings.Count)
                    continue;

                _activeBindings.RemoveAt(index);
            }

            _staleBindingIndexBuffer.Clear();
        }

        private static string ResolveGeneratorModeLabel(WorldGenerativeGeologyProfile profile)
        {
            if (profile == null)
                return GeneratorModeDisabledLabel;

            return profile.generatorMode switch
            {
                WorldGenerativeGeologyProfile.GeneratorMode.NeuralPreferred => GeneratorModeNeuralPreferredLabel,
                WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback => GeneratorModeHeuristicSdfFallbackLabel,
                _ => GeneratorModeDisabledLabel
            };
        }

        private static string ResolveArchetypeLabel(WorldGenerativeGeologyProfile profile)
        {
            if (profile == null)
                return ArchetypeComplexRockLabel;

            return profile.shapeArchetype switch
            {
                WorldGenerativeGeologyProfile.ShapeArchetype.Arch => ArchetypeArchLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.Canopy => ArchetypeCanopyLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.ArchCluster => ArchetypeArchClusterLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.ReefPack => ArchetypeReefPackLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge => ArchetypeCaveBridgeLabel,
                _ => ArchetypeComplexRockLabel
            };
        }

        private static string ResolveTerrainSeamLabel(WorldGenerativeGeologyProfile profile)
        {
            if (profile == null)
                return TerrainSeamNoneLabel;

            return profile.terrainSeamMode switch
            {
                WorldGenerativeGeologyProfile.TerrainSeamMode.HeightBlend => TerrainSeamHeightBlendLabel,
                WorldGenerativeGeologyProfile.TerrainSeamMode.SdfBlend => TerrainSeamSdfBlendLabel,
                WorldGenerativeGeologyProfile.TerrainSeamMode.DebrisBridge => TerrainSeamDebrisBridgeLabel,
                WorldGenerativeGeologyProfile.TerrainSeamMode.CarveAndDebris => TerrainSeamCarveAndDebrisLabel,
                _ => TerrainSeamNoneLabel
            };
        }

        private static string ResolveCaveBlendLabel(WorldGenerativeGeologyProfile profile)
        {
            if (profile == null)
                return CaveBlendNoneLabel;

            return profile.caveBlendMode switch
            {
                WorldGenerativeGeologyProfile.CaveBlendMode.ProbeOnly => CaveBlendProbeOnlyLabel,
                WorldGenerativeGeologyProfile.CaveBlendMode.SdfBlend => CaveBlendSdfBlendLabel,
                WorldGenerativeGeologyProfile.CaveBlendMode.CarvePortal => CaveBlendCarvePortalLabel,
                _ => CaveBlendNoneLabel
            };
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
            CacheReferences();
            runtimeKey = request.RuntimeKey;
            familyId = request.Family != null ? request.Family.familyId : "world.family.generic";
            geologyProfileId = request.Profile != null ? request.Profile.profileId : "geology.generic";
            generatorMode = ResolveGeneratorModeLabel(request.Profile);
            archetype = ResolveArchetypeLabel(request.Profile);
            composition = string.IsNullOrWhiteSpace(resolvedComposition) ? "SingleFeature" : resolvedComposition;
            terrainSeam = ResolveTerrainSeamLabel(request.Profile);
            caveBlend = ResolveCaveBlendLabel(request.Profile);
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
        private static int _activeGeneratedRootCount;
        private static int _activeGeneratedRendererCount;
        internal static WorldGenerativeGeologyService ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeGeneratedRootCount = 0;
            _activeGeneratedRendererCount = 0;
            ActiveRuntimeInstance = null;
        }

        [DisallowMultipleComponent]
        private sealed class GeneratedRuntimeState : MonoBehaviour
        {
            [SerializeField] private int buildSignature;
            [SerializeField] private int rendererCount;

            private bool _registeredInGlobalCounters;
            private LOD[] _lodArrayCache = _EmptyLods;
            private Renderer[] _lod0RendererCache = System.Array.Empty<Renderer>();
            private Renderer[] _lod1RendererCache = System.Array.Empty<Renderer>();
            private Renderer[] _lod2RendererCache = System.Array.Empty<Renderer>();

            public int BuildSignature => buildSignature;
            public int RendererCount => rendererCount;

            private void OnEnable()
            {
                if (_registeredInGlobalCounters)
                    return;

                _registeredInGlobalCounters = true;
                _activeGeneratedRootCount++;
                _activeGeneratedRendererCount += Mathf.Max(0, rendererCount);
            }

            private void OnDisable()
            {
                if (!_registeredInGlobalCounters)
                    return;

                _registeredInGlobalCounters = false;
                _activeGeneratedRootCount = Mathf.Max(0, _activeGeneratedRootCount - 1);
                _activeGeneratedRendererCount = Mathf.Max(0, _activeGeneratedRendererCount - Mathf.Max(0, rendererCount));
            }

            public void Configure(int signature, int configuredRendererCount)
            {
                if (_registeredInGlobalCounters)
                {
                    _activeGeneratedRendererCount -= Mathf.Max(0, rendererCount);
                    _activeGeneratedRendererCount += Mathf.Max(0, configuredRendererCount);
                    if (_activeGeneratedRendererCount < 0)
                        _activeGeneratedRendererCount = 0;
                }

                buildSignature = signature;
                rendererCount = configuredRendererCount;
            }

            public LOD[] GetOrCreateLodArray(int lodCount)
            {
                if (lodCount <= 0)
                {
                    _lodArrayCache = _EmptyLods;
                    return _lodArrayCache;
                }

                if (_lodArrayCache == null || _lodArrayCache.Length != lodCount)
                    _lodArrayCache = new LOD[lodCount];

                return _lodArrayCache;
            }

            public Renderer[] GetOrCreateRendererArray(int lodIndex, int rendererCountForLod)
            {
                if (rendererCountForLod <= 0)
                    return System.Array.Empty<Renderer>();

                switch (lodIndex)
                {
                    case 0:
                        return EnsureRendererArray(ref _lod0RendererCache, rendererCountForLod);
                    case 1:
                        return EnsureRendererArray(ref _lod1RendererCache, rendererCountForLod);
                    default:
                        return EnsureRendererArray(ref _lod2RendererCache, rendererCountForLod);
                }
            }

            private static Renderer[] EnsureRendererArray(ref Renderer[] cache, int requiredCount)
            {
                if (cache == null || cache.Length != requiredCount)
                    cache = new Renderer[requiredCount];

                return cache;
            }
        }

        private const string GeneratedRootName = "__GENERATED_GEOLOGY";
        private static readonly LOD[] _EmptyLods = new LOD[0];

        [Header("Fallback Generation")]
        [SerializeField] private bool allowEditorGeneration = true;
        [SerializeField] private float primitiveThickness = 1.6f;
        [SerializeField] private float debrisScale = 0.28f;

        private readonly List<Renderer> _rendererBuildBuffer = new List<Renderer>(24);

        public static int ActiveGeneratedRootCount => Mathf.Max(0, _activeGeneratedRootCount);
        public static int ActiveGeneratedRendererCount => Mathf.Max(0, _activeGeneratedRendererCount);

        private void Awake()
        {
            ActiveRuntimeInstance = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public bool TryApplyGeneratedGeology(GameObject host, in WorldGenerativeGeologyRequest request)
        {
            if (host == null || request.Profile == null || !request.Profile.IsEnabled)
                return false;

            if (!Application.isPlaying && !allowEditorGeneration)
                return false;

            bool useFullDetail = request.FinalVariantActive;
            string resolvedComposition = ResolveComposition(request);
            if (!useFullDetail)
                resolvedComposition = "SingleFeature";

            int lodCount = Mathf.Clamp(request.Profile.lodCount, 1, useFullDetail ? 3 : 2);
            float blendRadius = Mathf.Max(0.5f, request.Profile.seamBlendRadius * Mathf.Max(0.25f, request.WorldScale));
            float terrainRaise = request.Profile.terrainRaiseMeters * Mathf.Clamp01(request.RidgeSignal + request.CompositionPotential * 0.25f);
            float terrainCut = request.Profile.terrainCutMeters * Mathf.Clamp01(request.CaveProximity + request.CanyonSignal * 0.35f);
            int debrisCount = useFullDetail ? request.Profile.ResolveDebrisCount(request.StableHash) : 0;
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

            StripHostPrimitiveVisuals(host);

            LODGroup lodGroup = generatedRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = generatedRoot.gameObject.AddComponent<LODGroup>();

            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            if (runtimeState == null)
                runtimeState = generatedRoot.gameObject.AddComponent<GeneratedRuntimeState>();

            LOD[] lodArray = runtimeState.GetOrCreateLodArray(lodCount);
            int totalRendererCount = 0;
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                Transform lodRoot = GetOrCreateLodRoot(generatedRoot, lodIndex);
                Renderer[] renderers = BuildCompositionLod(runtimeState, lodRoot, request, resolvedComposition, lodIndex, debrisCount);
                float transitionHeight = ResolveLodScreenHeight(request.Profile, lodIndex, lodCount);
                lodArray[lodIndex] = new LOD(transitionHeight, renderers);
                totalRendererCount += renderers.Length;
            }

            DisableUnusedLodRoots(generatedRoot, lodCount);
            lodGroup.SetLODs(lodArray);
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

            runtimeState.Configure(buildSignature, totalRendererCount);

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
            GeneratedRuntimeState runtimeState,
            Transform lodRoot,
            in WorldGenerativeGeologyRequest request,
            string composition,
            int lodIndex,
            int debrisCount)
        {
            _rendererBuildBuffer.Clear();
            ActivateTransform(lodRoot);
            int primitiveIndex = 0;
            float lodScale = Mathf.Lerp(1f, 0.7f, lodIndex / 2f);

            switch (request.Profile.shapeArchetype)
            {
                case WorldGenerativeGeologyProfile.ShapeArchetype.Arch:
                case WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge:
                    BuildArch(_rendererBuildBuffer, lodRoot, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;

                case WorldGenerativeGeologyProfile.ShapeArchetype.Canopy:
                    BuildCanopy(_rendererBuildBuffer, lodRoot, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;

                default:
                    BuildRockPack(_rendererBuildBuffer, lodRoot, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;
            }

            if (lodIndex == 0 && request.Profile.terrainSeamMode != WorldGenerativeGeologyProfile.TerrainSeamMode.None)
                BuildDebris(_rendererBuildBuffer, lodRoot, request, debrisCount, ref primitiveIndex);

            DisableUnusedPrimitiveChildren(lodRoot, primitiveIndex);

            int rendererCount = _rendererBuildBuffer.Count;
            if (rendererCount == 0)
                return System.Array.Empty<Renderer>();

            Renderer[] renderers = runtimeState.GetOrCreateRendererArray(lodIndex, rendererCount);
            _rendererBuildBuffer.CopyTo(renderers);
            return renderers;
        }

        private void BuildArch(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float width = Mathf.Lerp(10f, 5f, lodIndex / 2f) * request.WorldScale;
            float height = Mathf.Lerp(7f, 4f, lodIndex / 2f) * request.WorldScale;
            float thickness = primitiveThickness * request.WorldScale * lodScale;

            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(-width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness), ref primitiveIndex);
            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness), ref primitiveIndex);
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, 6f, lodIndex / 2f)), new Vector3(width, thickness, thickness * 1.1f), ref primitiveIndex);

            if (composition == "ContextPack" && lodIndex == 0)
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.55f, width * 0.22f), Quaternion.Euler(0f, 24f, -14f), new Vector3(width * 0.42f, thickness * 0.8f, thickness), ref primitiveIndex);
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.42f, -width * 0.24f), Quaternion.Euler(0f, -22f, 10f), new Vector3(width * 0.36f, thickness * 0.75f, thickness), ref primitiveIndex);
            }
        }

        private void BuildCanopy(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float span = Mathf.Lerp(12f, 6f, lodIndex / 2f) * request.WorldScale;
            float shelfThickness = primitiveThickness * request.WorldScale * lodScale;
            float height = Mathf.Lerp(4.5f, 2.2f, lodIndex / 2f) * request.WorldScale;

            CreatePrimitive(renderers, root, PrimitiveType.Cylinder, new Vector3(0f, height * 0.65f, 0f), Quaternion.identity, new Vector3(shelfThickness * 1.1f, height, shelfThickness * 1.1f), ref primitiveIndex);
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 18f, request.CanyonSignal * 14f), new Vector3(span, shelfThickness, span * 0.55f), ref primitiveIndex);

            if (composition != "SingleFeature")
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(span * 0.18f, height * 0.82f, span * 0.16f), Quaternion.Euler(0f, -16f, 8f), new Vector3(span * 0.56f, shelfThickness * 0.8f, span * 0.28f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(-span * 0.22f, height * 0.72f, -span * 0.2f), Quaternion.Euler(0f, 24f, -10f), new Vector3(span * 0.42f, shelfThickness * 0.75f, span * 0.22f), ref primitiveIndex);
            }
        }

        private void BuildRockPack(
            List<Renderer> renderers,
            Transform root,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float baseScale = Mathf.Lerp(6f, 3f, lodIndex / 2f) * request.WorldScale;
            CreatePrimitive(renderers, root, PrimitiveType.Sphere, new Vector3(0f, baseScale * 0.45f, 0f), Quaternion.identity, Vector3.one * baseScale, ref primitiveIndex);
            CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(baseScale * 0.36f, baseScale * 0.52f, -baseScale * 0.18f), Quaternion.Euler(18f, 22f, 12f), new Vector3(baseScale * 0.9f, baseScale * 0.45f, baseScale * 0.64f), ref primitiveIndex);

            if (composition != "SingleFeature")
            {
                CreatePrimitive(renderers, root, PrimitiveType.Sphere, new Vector3(-baseScale * 0.42f, baseScale * 0.34f, baseScale * 0.24f), Quaternion.identity, Vector3.one * (baseScale * 0.7f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, baseScale * 0.92f, 0f), Quaternion.Euler(-8f, 32f, 20f), new Vector3(baseScale * 0.55f, baseScale * 0.18f, baseScale * 0.42f), ref primitiveIndex);
            }
        }

        private void BuildDebris(List<Renderer> renderers, Transform root, in WorldGenerativeGeologyRequest request, int debrisCount, ref int primitiveIndex)
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
                CreatePrimitive(renderers, root, primitive, localPos, Quaternion.Euler(11f * i, angle, 17f), Vector3.one * scale, ref primitiveIndex);
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
            {
                ActivateTransform(existing);
                return existing;
            }

            Transform created = new GameObject(GeneratedRootName).transform;
            created.SetParent(host, false);
            return created;
        }

        private static Transform GetOrCreateLodRoot(Transform generatedRoot, int lodIndex)
        {
            string lodName = $"LOD{lodIndex}";
            for (int i = 0; i < generatedRoot.childCount; i++)
            {
                Transform child = generatedRoot.GetChild(i);
                if (child != null && child.name == lodName)
                {
                    ActivateTransform(child);
                    return child;
                }
            }

            Transform created = new GameObject(lodName).transform;
            created.SetParent(generatedRoot, false);
            return created;
        }

        private static void DisableUnusedLodRoots(Transform generatedRoot, int activeLodCount)
        {
            for (int i = 0; i < generatedRoot.childCount; i++)
            {
                Transform child = generatedRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("LOD"))
                    continue;

                bool keepActive = TryParseLodIndex(child.name, out int lodIndex) && lodIndex < activeLodCount;
                if (child.gameObject.activeSelf != keepActive)
                    child.gameObject.SetActive(keepActive);
            }
        }

        private static void DisableUnusedPrimitiveChildren(Transform lodRoot, int activePrimitiveCount)
        {
            for (int i = 0; i < lodRoot.childCount; i++)
            {
                Transform child = lodRoot.GetChild(i);
                if (child == null)
                    continue;

                bool keepActive = i < activePrimitiveCount;
                if (child.gameObject.activeSelf != keepActive)
                    child.gameObject.SetActive(keepActive);
            }
        }

        private static bool TryParseLodIndex(string name, out int lodIndex)
        {
            lodIndex = -1;
            if (string.IsNullOrEmpty(name) || name.Length <= 3 || !name.StartsWith("LOD"))
                return false;

            return int.TryParse(name.Substring(3), out lodIndex);
        }

        private static void ActivateTransform(Transform target)
        {
            if (target != null && !target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
        }


        private void CreatePrimitive(
            List<Renderer> renderers,
            Transform parent,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            ref int primitiveIndex)
        {
            Renderer renderer;
            if (primitiveIndex < parent.childCount)
            {
                GameObject existing = parent.GetChild(primitiveIndex).gameObject;
                ActivateTransform(existing.transform);
                renderer = WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                    existing,
                    primitiveType,
                    WorldGeneratedPrimitiveFactory.GetPrimitiveName(primitiveType),
                    localPosition,
                    localRotation,
                    localScale);
            }
            else
            {
                renderer = WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    parent,
                    primitiveType,
                    WorldGeneratedPrimitiveFactory.GetPrimitiveName(primitiveType),
                    localPosition,
                    localRotation,
                    localScale);
            }

            primitiveIndex++;
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
