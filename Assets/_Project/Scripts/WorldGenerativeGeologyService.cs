using System;
using System.Collections.Generic;
using Hecton8.Core;
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
            FinalVariantActive = finalVariantActive ? (byte)1 : (byte)0;
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

        public readonly long RuntimeKey;
        public readonly int StableHash;
        public readonly WorldPrefabFamilyProfile Family;
        public readonly WorldGenerativeGeologyProfile Profile;
        public readonly byte FinalVariantActive;
        public readonly float SlopeDegrees;
        public readonly float Curvature;
        public readonly float CaveProximity;
        public readonly float RidgeSignal;
        public readonly float CanyonSignal;
        public readonly float CompositionPotential;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion WorldRotation;
        public readonly float WorldScale;
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
        private const string ArchetypeOpenTrenchLabel = "OpenTrench";
        private const string TerrainSeamNoneLabel = "None";
        private const string TerrainSeamHeightBlendLabel = "HeightBlend";
        private const string TerrainSeamSdfBlendLabel = "SdfBlend";
        private const string TerrainSeamDebrisBridgeLabel = "DebrisBridge";
        private const string TerrainSeamCarveAndDebrisLabel = "CarveAndDebris";
        private const string CaveBlendNoneLabel = "None";
        private const string CaveBlendProbeOnlyLabel = "ProbeOnly";
        private const string CaveBlendSdfBlendLabel = "SdfBlend";
        private const string CaveBlendCarvePortalLabel = "CarvePortal";
        private const int BindingRegistryCapacity = 256;
        private const int StaleBindingIndexCapacity = 256;

        // COLD ALLOC: List<WorldGenerativeGeologyBinding>[256] - loaded geology binding registry including inactive editor bindings - owner: WorldGenerativeGeologyBinding
        private static readonly List<WorldGenerativeGeologyBinding> _knownBindings = new List<WorldGenerativeGeologyBinding>(BindingRegistryCapacity);
        private static readonly List<WorldGenerativeGeologyBinding> _activeBindings = new List<WorldGenerativeGeologyBinding>(BindingRegistryCapacity);
        private static readonly List<int> _staleBindingIndexBuffer = new List<int>(StaleBindingIndexCapacity);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _knownBindings.Clear();
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
        private Renderer _cachedSeamRenderer;

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
        internal Material CachedSeamMaterial => _cachedSeamRenderer != null ? _cachedSeamRenderer.sharedMaterial : null;
        public static int ActiveBindingCount => _activeBindings.Count;

        private bool HasConfiguredRuntimeKey => runtimeKey != 0L;

        internal void InjectDynamicState(long newKey, string arch)
        {
            runtimeKey = newKey;
            archetype = arch;
            if (HasConfiguredRuntimeKey)
                RegisterActiveBinding(this);
        }

        public WorldGenerativeGeologyProfile.ShapeArchetype Archetype
        {
            get
            {
                return ResolveShapeArchetype(archetype);
            }
        }

        public WorldGenerativeGeologyProfile.TerrainSeamMode TerrainSeamMode
        {
            get
            {
                return ResolveTerrainSeamMode(terrainSeam);
            }
        }

        public WorldGenerativeGeologyProfile.CaveBlendMode CaveBlendMode
        {
            get
            {
                return ResolveCaveBlendMode(caveBlend);
            }
        }

        private void Awake()
        {
            CacheReferences();
            RegisterKnownBinding(this);
        }

        private void OnEnable()
        {
            CacheReferences();
            RegisterKnownBinding(this);
            if (HasConfiguredRuntimeKey)
                RegisterActiveBinding(this);
        }

        private void OnDisable()
        {
            UnregisterActiveBinding(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveBinding(this);
            UnregisterKnownBinding(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RegisterKnownBinding(this);
        }
#endif

        private void CacheReferences()
        {
            if (!TryGetComponent(out _cachedProxyInstance))
                _cachedProxyInstance = null;

            if (!TryGetComponent(out _cachedSeamRenderer))
                _cachedSeamRenderer = ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
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
                    if (_staleBindingIndexBuffer.Count < _staleBindingIndexBuffer.Capacity)
                        _staleBindingIndexBuffer.Add(i);
                    continue;
                }

                if (destination.Count >= destination.Capacity)
                    break;

                destination.Add(binding);
            }

            TrimStaleBindings(_activeBindings);
        }

        public static void CopyKnownBindingsTo(List<WorldGenerativeGeologyBinding> destination, bool includeInactive)
        {
            if (destination == null)
                return;

            destination.Clear();
            _staleBindingIndexBuffer.Clear();
            for (int i = 0; i < _knownBindings.Count; i++)
            {
                WorldGenerativeGeologyBinding binding = _knownBindings[i];
                if (binding == null)
                {
                    if (_staleBindingIndexBuffer.Count < _staleBindingIndexBuffer.Capacity)
                        _staleBindingIndexBuffer.Add(i);
                    continue;
                }

                if (!includeInactive && !binding.isActiveAndEnabled)
                    continue;

                if (destination.Count >= destination.Capacity)
                    break;

                destination.Add(binding);
            }

            TrimStaleBindings(_knownBindings);
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
                    if (_staleBindingIndexBuffer.Count < _staleBindingIndexBuffer.Capacity)
                        _staleBindingIndexBuffer.Add(i);
                    continue;
                }

                if (candidate.runtimeKey != runtimeKey)
                    continue;

                binding = candidate;
                TrimStaleBindings(_activeBindings);
                return true;
            }

            TrimStaleBindings(_activeBindings);
            return false;
        }

        private static void RegisterKnownBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null || _knownBindings.Contains(binding))
                return;

            if (_knownBindings.Count >= BindingRegistryCapacity)
                return;

            _knownBindings.Add(binding);
        }

        private static void UnregisterKnownBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null)
                return;

            _knownBindings.Remove(binding);
        }

        private static void RegisterActiveBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null || _activeBindings.Contains(binding))
                return;

            if (_activeBindings.Count >= BindingRegistryCapacity)
                return;

            _activeBindings.Add(binding);
        }

        private static void UnregisterActiveBinding(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null)
                return;

            _activeBindings.Remove(binding);
        }

        private static void TrimStaleBindings(List<WorldGenerativeGeologyBinding> bindings)
        {
            if (bindings == null)
            {
                _staleBindingIndexBuffer.Clear();
                return;
            }

            for (int i = _staleBindingIndexBuffer.Count - 1; i >= 0; i--)
            {
                int index = _staleBindingIndexBuffer[i];
                if (index < 0 || index >= bindings.Count)
                    continue;

                bindings.RemoveAt(index);
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

        private static WorldGenerativeGeologyProfile.ShapeArchetype ResolveShapeArchetype(string value)
        {
            if (string.Equals(value, ArchetypeArchLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.Arch;
            if (string.Equals(value, ArchetypeCanopyLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.Canopy;
            if (string.Equals(value, ArchetypeArchClusterLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.ArchCluster;
            if (string.Equals(value, ArchetypeReefPackLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.ReefPack;
            if (string.Equals(value, ArchetypeCaveBridgeLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge;
            if (string.Equals(value, ArchetypeOpenTrenchLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.ShapeArchetype.OpenTrench;

            return WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock;
        }

        private static WorldGenerativeGeologyProfile.TerrainSeamMode ResolveTerrainSeamMode(string value)
        {
            if (string.Equals(value, TerrainSeamHeightBlendLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.TerrainSeamMode.HeightBlend;
            if (string.Equals(value, TerrainSeamSdfBlendLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.TerrainSeamMode.SdfBlend;
            if (string.Equals(value, TerrainSeamDebrisBridgeLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.TerrainSeamMode.DebrisBridge;
            if (string.Equals(value, TerrainSeamCarveAndDebrisLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.TerrainSeamMode.CarveAndDebris;

            return WorldGenerativeGeologyProfile.TerrainSeamMode.None;
        }

        private static WorldGenerativeGeologyProfile.CaveBlendMode ResolveCaveBlendMode(string value)
        {
            if (string.Equals(value, CaveBlendProbeOnlyLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.CaveBlendMode.ProbeOnly;
            if (string.Equals(value, CaveBlendSdfBlendLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.CaveBlendMode.SdfBlend;
            if (string.Equals(value, CaveBlendCarvePortalLabel, StringComparison.Ordinal))
                return WorldGenerativeGeologyProfile.CaveBlendMode.CarvePortal;

            return WorldGenerativeGeologyProfile.CaveBlendMode.None;
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
            ApplyConfiguration(
                request,
                resolvedComposition,
                resolvedBlendRadius,
                resolvedTerrainRaise,
                resolvedTerrainCut,
                resolvedDebrisCount,
                resolvedLodCount);
        }

        public void ConfigureHot(
            WorldGenerativeGeologyRequest request,
            string resolvedComposition,
            float resolvedBlendRadius,
            float resolvedTerrainRaise,
            float resolvedTerrainCut,
            int resolvedDebrisCount,
            int resolvedLodCount)
        {
            ApplyConfiguration(
                request,
                resolvedComposition,
                resolvedBlendRadius,
                resolvedTerrainRaise,
                resolvedTerrainCut,
                resolvedDebrisCount,
                resolvedLodCount);
        }

        private void ApplyConfiguration(
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
            generatorMode = ResolveGeneratorModeLabel(request.Profile);
            archetype = ResolveArchetypeLabel(request.Profile);
            composition = string.IsNullOrWhiteSpace(resolvedComposition) ? "SingleFeature" : resolvedComposition;
            terrainSeam = ResolveTerrainSeamLabel(request.Profile);
            caveBlend = ResolveCaveBlendLabel(request.Profile);
            lodCount = resolvedLodCount;
            finalVariantActive = request.FinalVariantActive != 0;
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
            if (isActiveAndEnabled && HasConfiguredRuntimeKey)
                RegisterActiveBinding(this);
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
            GeneratedRuntimeState.ResetStaticState();
        }

        [DisallowMultipleComponent]
        private sealed class GeneratedRuntimeState : MonoBehaviour
        {
            private const int PreparedLodCapacity = 3;

            private static readonly Dictionary<GameObject, GeneratedRuntimeState> _preparedRuntimeStates =
                new Dictionary<GameObject, GeneratedRuntimeState>(256);

            [SerializeField] private int buildSignature;
            [SerializeField] private int rendererCount;

            private bool _registeredInGlobalCounters;
            private LODGroup _lodGroup;
            private WorldGenerativeGeologyBinding _binding;
            private LOD[] _lodArrayCache = _EmptyLods;
            private LOD[] _lod1ArrayCache;
            private LOD[] _lod2ArrayCache;
            private LOD[] _lod3ArrayCache;
            private Renderer[] _lod0RendererCache = System.Array.Empty<Renderer>();
            private Renderer[] _lod1RendererCache = System.Array.Empty<Renderer>();
            private Renderer[] _lod2RendererCache = System.Array.Empty<Renderer>();
            private Renderer[][] _lod0PreparedRendererCaches;
            private Renderer[][] _lod1PreparedRendererCaches;
            private Renderer[][] _lod2PreparedRendererCaches;
            private Transform[] _preparedLodRoots;
            private GameObject[][] _preparedPrimitiveObjects;
            private MeshFilter[][] _preparedPrimitiveFilters;
            private MeshRenderer[][] _preparedPrimitiveRenderers;

            public int BuildSignature => buildSignature;
            public int RendererCount => rendererCount;
            public LODGroup LodGroup => _lodGroup;
            public WorldGenerativeGeologyBinding Binding => _binding;

            public static void ResetStaticState()
            {
                _preparedRuntimeStates.Clear();
            }

            public static bool TryGetPrepared(Transform generatedRoot, out GeneratedRuntimeState runtimeState)
            {
                if (generatedRoot != null &&
                    _preparedRuntimeStates.TryGetValue(generatedRoot.gameObject, out runtimeState) &&
                    runtimeState != null)
                {
                    return true;
                }

                runtimeState = null;
                return false;
            }

            private void Awake()
            {
                _preparedRuntimeStates[gameObject] = this;
            }

            private void OnEnable()
            {
                _preparedRuntimeStates[gameObject] = this;

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

            private void OnDestroy()
            {
                GameObject owner = gameObject;
                if (owner == null)
                    return;

                if (_preparedRuntimeStates.TryGetValue(owner, out GeneratedRuntimeState state) &&
                    ReferenceEquals(state, this))
                {
                    _preparedRuntimeStates.Remove(owner);
                }
            }

            public void PrepareCold(
                LODGroup lodGroup,
                WorldGenerativeGeologyBinding binding,
                int maxLodCount,
                int maxRendererCount)
            {
                _lodGroup = lodGroup;
                _binding = binding;

                int lodCapacity = Mathf.Clamp(maxLodCount, 1, PreparedLodCapacity);
                int rendererCapacity = Mathf.Max(1, maxRendererCount);
                if (lodCapacity >= 1 && (_lod1ArrayCache == null || _lod1ArrayCache.Length != 1))
                    _lod1ArrayCache = new LOD[1];
                if (lodCapacity >= 2 && (_lod2ArrayCache == null || _lod2ArrayCache.Length != 2))
                    _lod2ArrayCache = new LOD[2];
                if (lodCapacity >= 3 && (_lod3ArrayCache == null || _lod3ArrayCache.Length != 3))
                    _lod3ArrayCache = new LOD[3];

                if (lodCapacity >= 1)
                    EnsurePreparedRendererCaches(ref _lod0PreparedRendererCaches, rendererCapacity);
                if (lodCapacity >= 2)
                    EnsurePreparedRendererCaches(ref _lod1PreparedRendererCaches, rendererCapacity);
                if (lodCapacity >= 3)
                    EnsurePreparedRendererCaches(ref _lod2PreparedRendererCaches, rendererCapacity);

                if (_preparedLodRoots == null || _preparedLodRoots.Length != PreparedLodCapacity)
                    _preparedLodRoots = new Transform[PreparedLodCapacity];

                EnsurePreparedPrimitiveCaches(lodCapacity, rendererCapacity);
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

            public bool TryGetPreparedLodArray(int lodCount, out LOD[] lods)
            {
                switch (lodCount)
                {
                    case 1:
                        lods = _lod1ArrayCache;
                        return lods != null && lods.Length == 1;
                    case 2:
                        lods = _lod2ArrayCache;
                        return lods != null && lods.Length == 2;
                    case 3:
                        lods = _lod3ArrayCache;
                        return lods != null && lods.Length == 3;
                    default:
                        lods = null;
                        return false;
                }
            }

            public bool TryCopyPreparedRendererArray(
                int lodIndex,
                List<Renderer> source,
                out Renderer[] renderers)
            {
                int rendererCountForLod = source != null ? source.Count : 0;
                if (rendererCountForLod <= 0)
                {
                    renderers = System.Array.Empty<Renderer>();
                    return true;
                }

                Renderer[][] caches = lodIndex switch
                {
                    0 => _lod0PreparedRendererCaches,
                    1 => _lod1PreparedRendererCaches,
                    _ => _lod2PreparedRendererCaches
                };

                if (caches == null ||
                    rendererCountForLod >= caches.Length ||
                    caches[rendererCountForLod] == null)
                {
                    renderers = null;
                    return false;
                }

                renderers = caches[rendererCountForLod];
                for (int i = 0; i < rendererCountForLod; i++)
                    renderers[i] = source[i];

                return true;
            }

            public void CachePreparedPrimitiveCold(
                int lodIndex,
                int primitiveIndex,
                GameObject primitive,
                MeshFilter filter,
                MeshRenderer renderer)
            {
                if ((uint)lodIndex >= (uint)PreparedLodCapacity ||
                    primitiveIndex < 0 ||
                    _preparedPrimitiveObjects == null ||
                    lodIndex >= _preparedPrimitiveObjects.Length ||
                    _preparedPrimitiveObjects[lodIndex] == null ||
                    (uint)primitiveIndex >= (uint)_preparedPrimitiveObjects[lodIndex].Length)
                {
                    return;
                }

                _preparedPrimitiveObjects[lodIndex][primitiveIndex] = primitive;
                _preparedPrimitiveFilters[lodIndex][primitiveIndex] = filter;
                _preparedPrimitiveRenderers[lodIndex][primitiveIndex] = renderer;
            }

            public void CachePreparedLodRootCold(int lodIndex, Transform lodRoot)
            {
                if ((uint)lodIndex >= (uint)PreparedLodCapacity ||
                    _preparedLodRoots == null)
                {
                    return;
                }

                _preparedLodRoots[lodIndex] = lodRoot;
            }

            public bool TryGetPreparedLodRootHot(int lodIndex, out Transform lodRoot)
            {
                lodRoot = null;
                if ((uint)lodIndex >= (uint)PreparedLodCapacity ||
                    _preparedLodRoots == null ||
                    (uint)lodIndex >= (uint)_preparedLodRoots.Length)
                {
                    return false;
                }

                lodRoot = _preparedLodRoots[lodIndex];
                return lodRoot != null;
            }

            public void DisableUnusedPreparedLodRootsHot(int activeLodCount)
            {
                if (_preparedLodRoots == null)
                    return;

                for (int i = 0; i < _preparedLodRoots.Length; i++)
                {
                    Transform lodRoot = _preparedLodRoots[i];
                    if (lodRoot == null)
                        continue;

                    bool keepActive = i < activeLodCount;
                    GameObject lodObject = lodRoot.gameObject;
                    if (lodObject.activeSelf != keepActive)
                        lodObject.SetActive(keepActive);
                }
            }

            public void DisableUnusedPreparedPrimitivesHot(int lodIndex, int activePrimitiveCount)
            {
                if ((uint)lodIndex >= (uint)PreparedLodCapacity ||
                    _preparedPrimitiveObjects == null ||
                    lodIndex >= _preparedPrimitiveObjects.Length ||
                    _preparedPrimitiveObjects[lodIndex] == null)
                {
                    return;
                }

                GameObject[] primitives = _preparedPrimitiveObjects[lodIndex];
                for (int i = 0; i < primitives.Length; i++)
                {
                    GameObject primitive = primitives[i];
                    if (primitive == null)
                        continue;

                    bool keepActive = i < activePrimitiveCount;
                    if (primitive.activeSelf != keepActive)
                        primitive.SetActive(keepActive);
                }
            }

            public bool TryGetPreparedPrimitiveHot(
                int lodIndex,
                int primitiveIndex,
                out GameObject primitive,
                out MeshFilter filter,
                out MeshRenderer renderer)
            {
                primitive = null;
                filter = null;
                renderer = null;

                if ((uint)lodIndex >= (uint)PreparedLodCapacity ||
                    primitiveIndex < 0 ||
                    _preparedPrimitiveObjects == null ||
                    _preparedPrimitiveFilters == null ||
                    _preparedPrimitiveRenderers == null ||
                    lodIndex >= _preparedPrimitiveObjects.Length ||
                    lodIndex >= _preparedPrimitiveFilters.Length ||
                    lodIndex >= _preparedPrimitiveRenderers.Length ||
                    _preparedPrimitiveObjects[lodIndex] == null ||
                    _preparedPrimitiveFilters[lodIndex] == null ||
                    _preparedPrimitiveRenderers[lodIndex] == null ||
                    (uint)primitiveIndex >= (uint)_preparedPrimitiveObjects[lodIndex].Length ||
                    (uint)primitiveIndex >= (uint)_preparedPrimitiveFilters[lodIndex].Length ||
                    (uint)primitiveIndex >= (uint)_preparedPrimitiveRenderers[lodIndex].Length)
                {
                    return false;
                }

                primitive = _preparedPrimitiveObjects[lodIndex][primitiveIndex];
                filter = _preparedPrimitiveFilters[lodIndex][primitiveIndex];
                renderer = _preparedPrimitiveRenderers[lodIndex][primitiveIndex];
                return primitive != null && filter != null && renderer != null;
            }

            private static Renderer[] EnsureRendererArray(ref Renderer[] cache, int requiredCount)
            {
                if (cache == null || cache.Length != requiredCount)
                    cache = new Renderer[requiredCount];

                return cache;
            }

            private static void EnsurePreparedRendererCaches(ref Renderer[][] caches, int maxRendererCount)
            {
                int requiredLength = maxRendererCount + 1;
                if (caches == null || caches.Length != requiredLength)
                    caches = new Renderer[requiredLength][];

                for (int count = 1; count < requiredLength; count++)
                {
                    if (caches[count] == null || caches[count].Length != count)
                        caches[count] = new Renderer[count];
                }
            }

            private void EnsurePreparedPrimitiveCaches(int lodCapacity, int maxPrimitiveCount)
            {
                EnsurePreparedPrimitiveMatrix(ref _preparedPrimitiveObjects, lodCapacity, maxPrimitiveCount);
                EnsurePreparedPrimitiveMatrix(ref _preparedPrimitiveFilters, lodCapacity, maxPrimitiveCount);
                EnsurePreparedPrimitiveMatrix(ref _preparedPrimitiveRenderers, lodCapacity, maxPrimitiveCount);
            }

            private static void EnsurePreparedPrimitiveMatrix<T>(ref T[][] matrix, int lodCapacity, int maxPrimitiveCount)
            {
                int lodLength = Mathf.Clamp(lodCapacity, 1, PreparedLodCapacity);
                int primitiveLength = Mathf.Max(1, maxPrimitiveCount);
                if (matrix == null || matrix.Length != PreparedLodCapacity)
                    matrix = new T[PreparedLodCapacity][];

                for (int lodIndex = 0; lodIndex < lodLength; lodIndex++)
                {
                    if (matrix[lodIndex] == null || matrix[lodIndex].Length != primitiveLength)
                        matrix[lodIndex] = new T[primitiveLength];
                }
            }
        }

        private const string GeneratedRootName = "__GENERATED_GEOLOGY";
        private const string CompositionSingleFeature = "SingleFeature";
        private const string CompositionPairedFeature = "PairedFeature";
        private const string CompositionContextPack = "ContextPack";
        private const int GeneratedRuntimeLodCapacity = 3;
        private const int GeneratedRuntimePrimitiveCapacityPerLod = 16;
        private static readonly LOD[] _EmptyLods = new LOD[0];

        [Header("Fallback Generation")]
        [SerializeField] private bool allowEditorGeneration = true;
        [SerializeField] private Material authoredRuntimePrimitiveMaterial;
        [SerializeField] private Mesh authoredRuntimeSphereMesh;
        [SerializeField] private Mesh authoredRuntimeCapsuleMesh;
        [SerializeField] private Mesh authoredRuntimeCylinderMesh;
        [SerializeField] private Mesh authoredRuntimeCubeMesh;
        [SerializeField] private Mesh authoredRuntimePlaneMesh;
        [SerializeField] private Mesh authoredRuntimeQuadMesh;
        [SerializeField] private float primitiveThickness = 1.6f;
        [SerializeField] private float debrisScale = 0.28f;

        private readonly List<Renderer> _rendererBuildBuffer = new List<Renderer>(24);
        private bool _allowStructuralPrimitiveBuild = true;

        public static int ActiveGeneratedRootCount => Mathf.Max(0, _activeGeneratedRootCount);
        public static int ActiveGeneratedRendererCount => Mathf.Max(0, _activeGeneratedRendererCount);

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            EnsureRuntimePrimitiveMaterialRegistered();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public static bool TryPrepareRuntimeShellCold(GameObject host, out Transform generatedRoot)
        {
            generatedRoot = null;
            if (host == null)
                return false;

            if (ActiveRuntimeInstance == null || !ActiveRuntimeInstance.EnsureRuntimePrimitiveMaterialRegistered())
                return false;

            WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources();
            generatedRoot = GetOrCreateGeneratedRoot(host.transform);
            if (!generatedRoot.TryGetComponent(out LODGroup lodGroup))
                lodGroup = generatedRoot.gameObject.AddComponent<LODGroup>();

            if (!generatedRoot.TryGetComponent(out GeneratedRuntimeState runtimeState))
                runtimeState = generatedRoot.gameObject.AddComponent<GeneratedRuntimeState>();

            if (!host.TryGetComponent(out WorldGenerativeGeologyBinding binding))
                binding = host.AddComponent<WorldGenerativeGeologyBinding>();

            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            runtimeState.PrepareCold(
                lodGroup,
                binding,
                GeneratedRuntimeLodCapacity,
                GeneratedRuntimePrimitiveCapacityPerLod);

            for (int lodIndex = 0; lodIndex < GeneratedRuntimeLodCapacity; lodIndex++)
            {
                Transform lodRoot = GetOrCreateLodRoot(generatedRoot, lodIndex);
                runtimeState.CachePreparedLodRootCold(lodIndex, lodRoot);
                while (lodRoot.childCount < GeneratedRuntimePrimitiveCapacityPerLod)
                {
                    WorldGeneratedPrimitiveFactory.CreateCachedPrimitiveShell(
                        lodRoot,
                        "PrimitiveShell_" + lodRoot.childCount);
                }

                for (int primitiveIndex = 0; primitiveIndex < GeneratedRuntimePrimitiveCapacityPerLod; primitiveIndex++)
                {
                    GameObject primitive = lodRoot.GetChild(primitiveIndex).gameObject;
                    if (WorldGeneratedPrimitiveFactory.TryResolvePrimitiveComponentsCold(
                            primitive,
                            out MeshFilter filter,
                            out MeshRenderer renderer))
                    {
                        runtimeState.CachePreparedPrimitiveCold(lodIndex, primitiveIndex, primitive, filter, renderer);
                    }
                }

                DisableUnusedPrimitiveChildren(lodRoot, 0);
            }

            DisableUnusedLodRoots(generatedRoot, 0);
            if (generatedRoot.gameObject.activeSelf)
                generatedRoot.gameObject.SetActive(false);

            return true;
        }

        public bool TryApplyGeneratedGeology(GameObject host, in WorldGenerativeGeologyRequest request)
        {
            if (host == null || request.Profile == null || !request.Profile.IsEnabled)
                return false;

            if (!EnsureRuntimePrimitiveMaterialRegistered())
                return false;

            if (!Application.isPlaying && !allowEditorGeneration)
                return false;

            if (Application.isPlaying)
                return TryApplyGeneratedGeologyHot(host, request);

            bool finalVariantRequested = request.FinalVariantActive != 0;
            float visualQualityWeight = ResolveGlobalQualityWeight();
            string resolvedComposition = ResolveQualityComposition(
                ResolveComposition(request),
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);

            int lodCount = ResolvePresentationLodCount(
                request.Profile,
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);
            float blendRadius = Mathf.Max(0.5f, request.Profile.seamBlendRadius * Mathf.Max(0.25f, request.WorldScale));
            float terrainRaise = request.Profile.terrainRaiseMeters * Mathf.Clamp01(request.RidgeSignal + request.CompositionPotential * 0.25f);
            float terrainCut = request.Profile.terrainCutMeters * Mathf.Clamp01(request.CaveProximity + request.CanyonSignal * 0.35f);
            int debrisCount = ResolvePresentationDebrisCount(
                request.Profile,
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);
            int buildSignature = ComputeBuildSignature(
                request,
                resolvedComposition,
                blendRadius,
                terrainRaise,
                terrainCut,
                debrisCount,
                lodCount);

            bool runtimeHotPath = Application.isPlaying;
            Transform generatedRoot;
            GeneratedRuntimeState runtimeState;
            LODGroup lodGroup;
            WorldGenerativeGeologyBinding binding;
            if (runtimeHotPath)
            {
                if (!WorldProceduralProxyInstance.TryGetCached(host, out WorldProceduralProxyInstance proxy) ||
                    !TryResolvePreparedRuntimeStateHot(proxy, out generatedRoot, out runtimeState, out lodGroup, out binding))
                {
                    return false;
                }

                ActivateTransform(generatedRoot);
            }
            else
            {
                generatedRoot = GetOrCreateGeneratedRoot(host.transform);
                generatedRoot.TryGetComponent(out runtimeState);
                host.TryGetComponent(out binding);
                if (!generatedRoot.TryGetComponent(out lodGroup))
                    lodGroup = generatedRoot.gameObject.AddComponent<LODGroup>();

                if (runtimeState == null)
                    runtimeState = generatedRoot.gameObject.AddComponent<GeneratedRuntimeState>();

                if (binding == null)
                    binding = host.AddComponent<WorldGenerativeGeologyBinding>();

                runtimeState.PrepareCold(
                    lodGroup,
                    binding,
                    GeneratedRuntimeLodCapacity,
                    GeneratedRuntimePrimitiveCapacityPerLod);
            }

            if (runtimeState != null && runtimeState.BuildSignature == buildSignature && binding != null)
                return true;

            if (!runtimeHotPath)
                StripHostPrimitiveVisuals(host);

            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            LOD[] lodArray;
            if (runtimeHotPath)
            {
                if (!runtimeState.TryGetPreparedLodArray(lodCount, out lodArray))
                    return false;
            }
            else
            {
                lodArray = runtimeState.GetOrCreateLodArray(lodCount);
            }

            int totalRendererCount = 0;
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                Transform lodRoot;
                if (runtimeHotPath)
                {
                    if (!runtimeState.TryGetPreparedLodRootHot(lodIndex, out lodRoot))
                        return false;
                }
                else
                {
                    lodRoot = GetOrCreateLodRoot(generatedRoot, lodIndex);
                }

                Renderer[] renderers = BuildCompositionLod(
                    runtimeState,
                    lodRoot,
                    request,
                    resolvedComposition,
                    lodIndex,
                    debrisCount,
                    allowStructuralChanges: !runtimeHotPath);
                if (renderers == null)
                    return false;

                float transitionHeight = ResolveLodScreenHeight(request.Profile, lodIndex, lodCount);
                lodArray[lodIndex] = new LOD(transitionHeight, renderers);
                totalRendererCount += renderers.Length;
            }

            DisableUnusedLodRoots(generatedRoot, lodCount);
            lodGroup.SetLODs(lodArray);
            lodGroup.RecalculateBounds();

            if (runtimeHotPath)
            {
                binding.ConfigureHot(
                    request,
                    resolvedComposition,
                    blendRadius,
                    terrainRaise,
                    terrainCut,
                    debrisCount,
                    lodCount);
            }
            else
            {
                binding.Configure(
                    request,
                    resolvedComposition,
                    blendRadius,
                    terrainRaise,
                    terrainCut,
                    debrisCount,
                    lodCount);
            }

            runtimeState.Configure(buildSignature, totalRendererCount);

            return true;
        }

        public bool TryApplyGeneratedGeologyHot(GameObject host, in WorldGenerativeGeologyRequest request)
        {
            return WorldProceduralProxyInstance.TryGetCached(host, out WorldProceduralProxyInstance proxy) &&
                TryApplyPreparedGeneratedGeologyHot(proxy, request);
        }

        public bool TryApplyPreparedGeneratedGeologyHot(WorldProceduralProxyInstance proxy, in WorldGenerativeGeologyRequest request)
        {
            if (proxy == null ||
                request.Profile == null ||
                !request.Profile.IsEnabled ||
                !TryResolvePreparedRuntimeStateHot(proxy, out Transform generatedRoot, out GeneratedRuntimeState runtimeState, out LODGroup lodGroup, out WorldGenerativeGeologyBinding binding))
            {
                return false;
            }

            if (!EnsureRuntimePrimitiveMaterialRegistered())
                return false;

            bool finalVariantRequested = request.FinalVariantActive != 0;
            float visualQualityWeight = ResolveGlobalQualityWeight();
            string resolvedComposition = ResolveQualityComposition(
                ResolveComposition(request),
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);
            int lodCount = ResolvePresentationLodCount(
                request.Profile,
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);
            float blendRadius = Mathf.Max(0.5f, request.Profile.seamBlendRadius * Mathf.Max(0.25f, request.WorldScale));
            float terrainRaise = request.Profile.terrainRaiseMeters * Mathf.Clamp01(request.RidgeSignal + request.CompositionPotential * 0.25f);
            float terrainCut = request.Profile.terrainCutMeters * Mathf.Clamp01(request.CaveProximity + request.CanyonSignal * 0.35f);
            int debrisCount = ResolvePresentationDebrisCount(
                request.Profile,
                request.StableHash,
                finalVariantRequested,
                visualQualityWeight);
            int buildSignature = ComputeBuildSignature(
                request,
                resolvedComposition,
                blendRadius,
                terrainRaise,
                terrainCut,
                debrisCount,
                lodCount);

            if (runtimeState.BuildSignature == buildSignature)
                return true;

            if (!runtimeState.TryGetPreparedLodArray(lodCount, out LOD[] lodArray))
                return false;

            ActivateTransform(generatedRoot);
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            int totalRendererCount = 0;
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                if (!runtimeState.TryGetPreparedLodRootHot(lodIndex, out Transform lodRoot))
                    return false;

                Renderer[] renderers = BuildCompositionLodHot(
                    runtimeState,
                    lodRoot,
                    request,
                    resolvedComposition,
                    lodIndex,
                    debrisCount);
                if (renderers == null)
                    return false;

                float transitionHeight = ResolveLodScreenHeight(request.Profile, lodIndex, lodCount);
                lodArray[lodIndex] = new LOD(transitionHeight, renderers);
                totalRendererCount += renderers.Length;
            }

            runtimeState.DisableUnusedPreparedLodRootsHot(lodCount);
            lodGroup.SetLODs(lodArray);
            lodGroup.RecalculateBounds();

            binding.ConfigureHot(
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

        private bool EnsureRuntimePrimitiveMaterialRegistered()
        {
            if (authoredRuntimePrimitiveMaterial == null ||
                !WorldGeneratedPrimitiveFactory.RegisterDefaultPrimitiveMaterialCold(authoredRuntimePrimitiveMaterial))
            {
                return false;
            }

            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Sphere, authoredRuntimeSphereMesh);
            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Capsule, authoredRuntimeCapsuleMesh);
            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Cylinder, authoredRuntimeCylinderMesh);
            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Cube, authoredRuntimeCubeMesh);
            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Plane, authoredRuntimePlaneMesh);
            RegisterAuthoredPrimitiveMeshCold(PrimitiveType.Quad, authoredRuntimeQuadMesh);
            return true;
        }

        private void RegisterAuthoredPrimitiveMeshCold(PrimitiveType primitiveType, Mesh mesh)
        {
            if (mesh == null)
                return;

            WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(
                primitiveType,
                mesh,
                authoredRuntimePrimitiveMaterial);
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

        private static bool TryResolvePreparedRuntimeStateHot(
            WorldProceduralProxyInstance proxy,
            out Transform generatedRoot,
            out GeneratedRuntimeState runtimeState,
            out LODGroup lodGroup,
            out WorldGenerativeGeologyBinding binding)
        {
            generatedRoot = null;
            runtimeState = null;
            lodGroup = null;
            binding = null;

            if (proxy == null)
            {
                return false;
            }

            generatedRoot = proxy.CachedGeneratedGeologyRoot;
            if (generatedRoot == null ||
                !GeneratedRuntimeState.TryGetPrepared(generatedRoot, out runtimeState) ||
                runtimeState == null)
            {
                return false;
            }

            lodGroup = runtimeState.LodGroup;
            binding = runtimeState.Binding;
            return lodGroup != null && binding != null;
        }

        private Renderer[] BuildCompositionLod(
            GeneratedRuntimeState runtimeState,
            Transform lodRoot,
            in WorldGenerativeGeologyRequest request,
            string composition,
            int lodIndex,
            int debrisCount,
            bool allowStructuralChanges)
        {
            _rendererBuildBuffer.Clear();
            ActivateTransform(lodRoot);
            int primitiveIndex = 0;
            float lodScale = Mathf.Lerp(1f, 0.7f, lodIndex / 2f);
            bool previousStructuralBuild = _allowStructuralPrimitiveBuild;
            _allowStructuralPrimitiveBuild = allowStructuralChanges;

            try
            {
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
            }
            finally
            {
                _allowStructuralPrimitiveBuild = previousStructuralBuild;
            }

            DisableUnusedPrimitiveChildren(lodRoot, primitiveIndex);
            if (!allowStructuralChanges && primitiveIndex > lodRoot.childCount)
                return null;

            int rendererCount = _rendererBuildBuffer.Count;
            if (rendererCount == 0)
                return allowStructuralChanges ? System.Array.Empty<Renderer>() : null;

            if (!allowStructuralChanges)
            {
                if (!runtimeState.TryCopyPreparedRendererArray(lodIndex, _rendererBuildBuffer, out Renderer[] preparedRenderers))
                    return null;

                return preparedRenderers;
            }

            Renderer[] renderers = runtimeState.GetOrCreateRendererArray(lodIndex, rendererCount);
            _rendererBuildBuffer.CopyTo(renderers);
            return renderers;
        }

        private Renderer[] BuildCompositionLodHot(
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
                    BuildArchHot(runtimeState, _rendererBuildBuffer, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;

                case WorldGenerativeGeologyProfile.ShapeArchetype.Canopy:
                    BuildCanopyHot(runtimeState, _rendererBuildBuffer, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;

                default:
                    BuildRockPackHot(runtimeState, _rendererBuildBuffer, request, composition, lodScale, lodIndex, ref primitiveIndex);
                    break;
            }

            if (lodIndex == 0 && request.Profile.terrainSeamMode != WorldGenerativeGeologyProfile.TerrainSeamMode.None)
                BuildDebrisHot(runtimeState, _rendererBuildBuffer, lodIndex, request, debrisCount, ref primitiveIndex);

            runtimeState.DisableUnusedPreparedPrimitivesHot(lodIndex, primitiveIndex);
            if (primitiveIndex > GeneratedRuntimePrimitiveCapacityPerLod || _rendererBuildBuffer.Count == 0)
                return null;

            return runtimeState.TryCopyPreparedRendererArray(lodIndex, _rendererBuildBuffer, out Renderer[] renderers)
                ? renderers
                : null;
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

            if (composition == CompositionContextPack && lodIndex == 0)
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.55f, width * 0.22f), Quaternion.Euler(0f, 24f, -14f), new Vector3(width * 0.42f, thickness * 0.8f, thickness), ref primitiveIndex);
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, height * 0.42f, -width * 0.24f), Quaternion.Euler(0f, -22f, 10f), new Vector3(width * 0.36f, thickness * 0.75f, thickness), ref primitiveIndex);
            }
        }

        private void BuildArchHot(
            GeneratedRuntimeState runtimeState,
            List<Renderer> renderers,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float width = Mathf.Lerp(10f, 5f, lodIndex / 2f) * request.WorldScale;
            float height = Mathf.Lerp(7f, 4f, lodIndex / 2f) * request.WorldScale;
            float thickness = primitiveThickness * request.WorldScale * lodScale;

            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cylinder, new Vector3(-width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness), ref primitiveIndex);
            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cylinder, new Vector3(width * 0.4f, height * 0.45f, 0f), Quaternion.identity, new Vector3(thickness, height * 0.45f, thickness), ref primitiveIndex);
            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, 6f, lodIndex / 2f)), new Vector3(width, thickness, thickness * 1.1f), ref primitiveIndex);

            if (composition == CompositionContextPack && lodIndex == 0)
            {
                CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(0f, height * 0.55f, width * 0.22f), Quaternion.Euler(0f, 24f, -14f), new Vector3(width * 0.42f, thickness * 0.8f, thickness), ref primitiveIndex);
                CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(0f, height * 0.42f, -width * 0.24f), Quaternion.Euler(0f, -22f, 10f), new Vector3(width * 0.36f, thickness * 0.75f, thickness), ref primitiveIndex);
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

            if (composition != CompositionSingleFeature)
            {
                CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(span * 0.18f, height * 0.82f, span * 0.16f), Quaternion.Euler(0f, -16f, 8f), new Vector3(span * 0.56f, shelfThickness * 0.8f, span * 0.28f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(-span * 0.22f, height * 0.72f, -span * 0.2f), Quaternion.Euler(0f, 24f, -10f), new Vector3(span * 0.42f, shelfThickness * 0.75f, span * 0.22f), ref primitiveIndex);
            }
        }

        private void BuildCanopyHot(
            GeneratedRuntimeState runtimeState,
            List<Renderer> renderers,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float span = Mathf.Lerp(12f, 6f, lodIndex / 2f) * request.WorldScale;
            float shelfThickness = primitiveThickness * request.WorldScale * lodScale;
            float height = Mathf.Lerp(4.5f, 2.2f, lodIndex / 2f) * request.WorldScale;

            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cylinder, new Vector3(0f, height * 0.65f, 0f), Quaternion.identity, new Vector3(shelfThickness * 1.1f, height, shelfThickness * 1.1f), ref primitiveIndex);
            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(0f, height, 0f), Quaternion.Euler(0f, 18f, request.CanyonSignal * 14f), new Vector3(span, shelfThickness, span * 0.55f), ref primitiveIndex);

            if (composition != CompositionSingleFeature)
            {
                CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(span * 0.18f, height * 0.82f, span * 0.16f), Quaternion.Euler(0f, -16f, 8f), new Vector3(span * 0.56f, shelfThickness * 0.8f, span * 0.28f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(-span * 0.22f, height * 0.72f, -span * 0.2f), Quaternion.Euler(0f, 24f, -10f), new Vector3(span * 0.42f, shelfThickness * 0.75f, span * 0.22f), ref primitiveIndex);
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

            if (composition != CompositionSingleFeature)
            {
                CreatePrimitive(renderers, root, PrimitiveType.Sphere, new Vector3(-baseScale * 0.42f, baseScale * 0.34f, baseScale * 0.24f), Quaternion.identity, Vector3.one * (baseScale * 0.7f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitive(renderers, root, PrimitiveType.Cube, new Vector3(0f, baseScale * 0.92f, 0f), Quaternion.Euler(-8f, 32f, 20f), new Vector3(baseScale * 0.55f, baseScale * 0.18f, baseScale * 0.42f), ref primitiveIndex);
            }
        }

        private void BuildRockPackHot(
            GeneratedRuntimeState runtimeState,
            List<Renderer> renderers,
            in WorldGenerativeGeologyRequest request,
            string composition,
            float lodScale,
            int lodIndex,
            ref int primitiveIndex)
        {
            float baseScale = Mathf.Lerp(6f, 3f, lodIndex / 2f) * request.WorldScale;
            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Sphere, new Vector3(0f, baseScale * 0.45f, 0f), Quaternion.identity, Vector3.one * baseScale, ref primitiveIndex);
            CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(baseScale * 0.36f, baseScale * 0.52f, -baseScale * 0.18f), Quaternion.Euler(18f, 22f, 12f), new Vector3(baseScale * 0.9f, baseScale * 0.45f, baseScale * 0.64f), ref primitiveIndex);

            if (composition != CompositionSingleFeature)
            {
                CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Sphere, new Vector3(-baseScale * 0.42f, baseScale * 0.34f, baseScale * 0.24f), Quaternion.identity, Vector3.one * (baseScale * 0.7f), ref primitiveIndex);
                if (lodIndex == 0)
                    CreatePrimitiveHot(runtimeState, renderers, lodIndex, PrimitiveType.Cube, new Vector3(0f, baseScale * 0.92f, 0f), Quaternion.Euler(-8f, 32f, 20f), new Vector3(baseScale * 0.55f, baseScale * 0.18f, baseScale * 0.42f), ref primitiveIndex);
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

        private void BuildDebrisHot(
            GeneratedRuntimeState runtimeState,
            List<Renderer> renderers,
            int lodIndex,
            in WorldGenerativeGeologyRequest request,
            int debrisCount,
            ref int primitiveIndex)
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
                CreatePrimitiveHot(runtimeState, renderers, lodIndex, primitive, localPos, Quaternion.Euler(11f * i, angle, 17f), Vector3.one * scale, ref primitiveIndex);
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
                return CompositionSingleFeature;

            if (request.Profile.PreferContextPack(request.CompositionPotential))
                return CompositionContextPack;

            return request.Profile.compositionMode == WorldGenerativeGeologyProfile.CompositionMode.PairedFeature
                ? CompositionPairedFeature
                : CompositionSingleFeature;
        }

        private static string ResolveQualityComposition(
            string authoredComposition,
            int stableHash,
            bool finalVariantRequested,
            float visualQualityWeight)
        {
            if (string.Equals(authoredComposition, CompositionSingleFeature, StringComparison.Ordinal))
                return CompositionSingleFeature;

            float detailPressure = ResolveVisualDetailPressure(finalVariantRequested, visualQualityWeight);
            float stableChance = ResolveStableHash01(stableHash ^ 0x6A09E667);

            if (string.Equals(authoredComposition, CompositionContextPack, StringComparison.Ordinal))
            {
                float contextProbability = Mathf.Lerp(0.02f, 0.92f, detailPressure);
                if (stableChance <= contextProbability)
                    return CompositionContextPack;

                float pairedProbability = Mathf.Clamp01(contextProbability + Mathf.Lerp(0.18f, 0.07f, detailPressure));
                return stableChance <= pairedProbability ? CompositionPairedFeature : CompositionSingleFeature;
            }

            if (string.Equals(authoredComposition, CompositionPairedFeature, StringComparison.Ordinal))
            {
                float pairedProbability = Mathf.Lerp(0.05f, 0.95f, detailPressure);
                return stableChance <= pairedProbability ? CompositionPairedFeature : CompositionSingleFeature;
            }

            return CompositionSingleFeature;
        }

        private static int ResolvePresentationLodCount(
            WorldGenerativeGeologyProfile profile,
            int stableHash,
            bool finalVariantRequested,
            float visualQualityWeight)
        {
            int configuredMax = Mathf.Clamp(profile != null ? profile.lodCount : 1, 1, 3);
            if (configuredMax <= 1)
                return 1;

            float detailPressure = ResolveVisualDetailPressure(finalVariantRequested, visualQualityWeight);
            float dither = ResolveStableHash01(stableHash ^ unchecked((int)0xBB67AE85)) - 0.5f;
            int resolved = Mathf.RoundToInt(Mathf.Lerp(1f, configuredMax, detailPressure) + (dither * 0.34f));
            return Mathf.Clamp(resolved, 1, configuredMax);
        }

        private static int ResolvePresentationDebrisCount(
            WorldGenerativeGeologyProfile profile,
            int stableHash,
            bool finalVariantRequested,
            float visualQualityWeight)
        {
            if (profile == null)
                return 0;

            int configured = Mathf.Max(0, profile.ResolveDebrisCount(stableHash));
            if (configured <= 0)
                return 0;

            int survivalCount = profile.terrainSeamMode == WorldGenerativeGeologyProfile.TerrainSeamMode.None ? 0 : 1;
            float detailPressure = ResolveVisualDetailPressure(finalVariantRequested, visualQualityWeight);
            float dither = ResolveStableHash01(stableHash ^ 0x3C6EF372) - 0.5f;
            int resolved = Mathf.RoundToInt(Mathf.Lerp(survivalCount, configured, detailPressure) + (dither * 0.6f));
            return Mathf.Clamp(resolved, survivalCount, configured);
        }

        private static float ResolveVisualDetailPressure(bool finalVariantRequested, float visualQualityWeight)
        {
            float quality = SmoothQualityWeight(visualQualityWeight);
            float finalVariantBias = finalVariantRequested ? 0.18f : 0f;
            return Mathf.Clamp01((quality * 0.82f) + finalVariantBias);
        }

        private static float SmoothQualityWeight(float visualQualityWeight)
        {
            float weight = Mathf.Clamp01(visualQualityWeight);
            return weight * weight * (3f - (2f * weight));
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return float.IsNaN(weight) || float.IsInfinity(weight) ? 1f : Mathf.Clamp01(weight);
        }

        private static float ResolveStableHash01(int stableHash)
        {
            unchecked
            {
                uint h = (uint)stableHash;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                h *= 0x846ca68b;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) * (1f / 16777215f);
            }
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
                hash = (hash * 397) ^ (request.Family != null ? Hecton.Localization.LocHash.Compute(request.Family.familyId) : 0);
                hash = (hash * 397) ^ (request.Profile != null ? Hecton.Localization.LocHash.Compute(request.Profile.profileId) : 0);
                hash = (hash * 397) ^ Hecton.Localization.LocHash.Compute(resolvedComposition);
                hash = (hash * 397) ^ lodCount;
                hash = (hash * 397) ^ Mathf.RoundToInt(request.WorldScale * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(blendRadius * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(terrainRaise * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(terrainCut * 100f);
                hash = (hash * 397) ^ debrisCount;
                hash = (hash * 397) ^ (request.FinalVariantActive != 0 ? 1 : 0);
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

            int value = 0;
            for (int i = 3; i < name.Length; i++)
            {
                char c = name[i];
                if (c < '0' || c > '9')
                    return false;

                value = (value * 10) + (c - '0');
            }

            lodIndex = value;
            return true;
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
                renderer = _allowStructuralPrimitiveBuild
                    ? WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                        existing,
                        primitiveType,
                        WorldGeneratedPrimitiveFactory.GetPrimitiveName(primitiveType),
                        localPosition,
                        localRotation,
                        localScale)
                    : WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot(
                        existing,
                        primitiveType,
                        WorldGeneratedPrimitiveFactory.GetPrimitiveName(primitiveType),
                        localPosition,
                        localRotation,
                        localScale);
            }
            else if (_allowStructuralPrimitiveBuild)
            {
                renderer = WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    parent,
                    primitiveType,
                    WorldGeneratedPrimitiveFactory.GetPrimitiveName(primitiveType),
                    localPosition,
                    localRotation,
                    localScale);
            }
            else
            {
                renderer = null;
            }

            primitiveIndex++;
            if (renderer != null)
                renderers.Add(renderer);
        }

        private static void CreatePrimitiveHot(
            GeneratedRuntimeState runtimeState,
            List<Renderer> renderers,
            int lodIndex,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            ref int primitiveIndex)
        {
            Renderer renderer = null;
            if (runtimeState != null &&
                runtimeState.TryGetPreparedPrimitiveHot(
                    lodIndex,
                    primitiveIndex,
                    out GameObject existing,
                    out MeshFilter filter,
                    out MeshRenderer meshRenderer))
            {
                ActivateTransform(existing.transform);
                renderer = WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualCachedHot(
                    existing,
                    filter,
                    meshRenderer,
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

            host.TryGetComponent(out MeshRenderer renderer);
            host.TryGetComponent(out MeshFilter filter);
            host.TryGetComponent(out Collider collider);

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
