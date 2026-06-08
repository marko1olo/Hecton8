using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldProceduralProxyInstance : MonoBehaviour, IPoolable
    {
        private static readonly Dictionary<GameObject, WorldProceduralProxyInstance> s_instanceCache =
            new Dictionary<GameObject, WorldProceduralProxyInstance>(2048);

        private const string ScatterLayerGroundLabel = "Ground";
        private const string ScatterLayerClusterLabel = "Cluster";
        private const string ScatterLayerStructureLabel = "Structure";
        private const string ScatterLayerSpawnLabel = "Spawn";
        private const string FieldSourceNoneLabel = "None";
        private const string FieldSourceMapMagicLabel = "MapMagicHeight";
        private const string FieldSourceSceneProbeLegacyLabel = "SceneProbeLegacy";
        private const string FieldSourceMacroGeologyLabel = "MacroGeologyFallback";
        private const string FieldSourceFallbackLabel = "FallbackSynthetic";
        private const string GeologyArchetypeArchLabel = "Arch";
        private const string GeologyArchetypeCanopyLabel = "Canopy";
        private const string GeologyArchetypeComplexRockLabel = "ComplexRock";
        private const string GeologyArchetypeArchClusterLabel = "ArchCluster";
        private const string GeologyArchetypeReefPackLabel = "ReefPack";
        private const string GeologyArchetypeCaveBridgeLabel = "CaveBridge";

        [Header("Identity")]
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string familyLabel = "Generic World Family";
        [SerializeField] private string ruleId = "procedural.rule.generic";
        [SerializeField] private string ruleLabel = "Generic Procedural Rule";

        [Header("Source")]
        [SerializeField] private string zoneId = "zone.generic";
        [SerializeField] private string socketId = "socket.generic";
        [SerializeField] private WorldContentSocket.ContentKind socketKind = WorldContentSocket.ContentKind.Generic;
        [SerializeField] private WorldSliceAnchor.SliceState preferredFidelity = WorldSliceAnchor.SliceState.Mid;

        [Header("Variant")]
        [SerializeField] private string variantId = "variant.generic";
        [SerializeField, HideInInspector] private int variantHash;
        [SerializeField] private bool proxyOnly = true;
        [SerializeField] private bool supportsFinalVariant;
        [SerializeField] private bool finalVariantActive;
        [SerializeField] private int clusterIndex;
        [SerializeField] private int instanceIndex;

        [Header("Field Scatter")]
        [SerializeField] private long runtimeKey;
        [SerializeField] private WorldStreamingLayer streamingLayer = WorldStreamingLayer.Flora;
        [SerializeField] private string placementSource = "Socket";
        [SerializeField] private string scatterLayer = "Ground";
        [SerializeField] private string fieldSource = "None";
        [SerializeField] private float seafloorHeight;
        [SerializeField] private float depthMeters;
        [SerializeField] private float slopeDegrees;
        [SerializeField] private float curvature;
        [SerializeField] private float caveProximity;
        [SerializeField] private float ridgeSignal;
        [SerializeField] private float canyonSignal;
        [SerializeField] private float compositionPotential;
        [SerializeField] private string heatmapChannel = "None";
        [SerializeField] private float heatmapValue;
        [SerializeField] private string sourceBiomeMatrix = "None";
        [SerializeField] private string sourceBiomeFamily = "None";
        [SerializeField] private string sourceWaterPattern = "None";
        [SerializeField] private string sourceBiomeContext = "None";
        [SerializeField] private bool usesGenerativeGeology;
        [SerializeField] private bool collisionEnabled = true;
        [SerializeField] private string geologyProfileId = "None";
        [SerializeField] private string geologyArchetype = "None";
        [SerializeField] private int cellX;
        [SerializeField] private int cellZ;
        [SerializeField] private int chunkX;
        [SerializeField] private int chunkZ;
        [SerializeField] private bool hasMacroZone;
        [SerializeField] private int macroZoneX;
        [SerializeField] private int macroZoneZ;
        [SerializeField, HideInInspector] private int scatterSyncSignature;
        [SerializeField, HideInInspector] private bool generatedGeologyApplied;

        private LODSystemManager _lodSystemManager;
        private CullingManager _cullingManager;
        private bool _cullingRegistered;
        private bool _poolManaged;
        private Transform _generatedGeologyRoot;
        private bool _runtimeComponentCacheReady;
        private bool _runtimeComponentCacheDirty = true;
        private bool _optimizationRegistrationDirty = true;
        private bool _collisionStateDirty = true;

        // COLD ALLOC: List<LODGroup>[4] — runtime child LOD scan buffer — owner: WorldProceduralProxyInstance
        private readonly List<LODGroup> _lodGroupBuffer = new List<LODGroup>(4);
        // COLD ALLOC: List<LODGroup>[4] — currently registered LOD groups — owner: WorldProceduralProxyInstance
        private readonly List<LODGroup> _registeredLodGroups = new List<LODGroup>(4);
        // COLD ALLOC: List<Collider>[8] - scatter collider toggle buffer for no-collision decor policy - owner: WorldProceduralProxyInstance
        private readonly List<Collider> _colliderBuffer = new List<Collider>(8);

        public string ActiveVariantId => variantId;
        public int ActiveVariantHash => variantHash;
        public bool IsFinalVariantActive => finalVariantActive;
        public bool SupportsFinalVariant => supportsFinalVariant;
        public long RuntimeKey => runtimeKey;
        public string FamilyId => familyId;
        public bool IsPoolManaged => _poolManaged;
        public string GeologyProfileId => geologyProfileId;
        public string GeologyArchetype => geologyArchetype;
        public bool UsesGenerativeGeology => usesGenerativeGeology;
        public WorldStreamingLayer ActiveStreamingLayer => streamingLayer;
        public string PlacementSource => placementSource;
        public bool HasPendingOptimizationRegistration =>
            _optimizationRegistrationDirty || _runtimeComponentCacheDirty || _collisionStateDirty;
        public WorldChunkCoordinate ChunkCoord => new WorldChunkCoordinate(chunkX, chunkZ);
        public bool HasMacroZone => hasMacroZone;
        public WorldMacroZoneCoordinate MacroZoneCoord => new WorldMacroZoneCoordinate(macroZoneX, macroZoneZ);
        public float DepthMeters => depthMeters;
        public float SlopeDegrees => slopeDegrees;
        public float Curvature => curvature;
        public float CaveProximity => caveProximity;
        public float RidgeSignal => ridgeSignal;
        public float CanyonSignal => canyonSignal;
        public float CompositionPotential => compositionPotential;
        public bool IsGeneratedGeologyApplied => generatedGeologyApplied;
        public Transform CachedGeneratedGeologyRoot => _generatedGeologyRoot;

        public static bool TryGetCached(GameObject instance, out WorldProceduralProxyInstance proxy)
        {
            if (instance != null &&
                s_instanceCache.TryGetValue(instance, out proxy) &&
                proxy != null)
            {
                return true;
            }

            proxy = null;
            return false;
        }

        private void Awake()
        {
            s_instanceCache[gameObject] = this;
            PrepareGeneratedGeologyRuntimeCold();
            RefreshRuntimeComponentCachesCold();
            MarkOptimizationRegistrationDirty(componentTopologyChanged: false);
        }

        private void OnEnable()
        {
            s_instanceCache[gameObject] = this;
            PrepareGeneratedGeologyRuntimeCold();
            MarkOptimizationRegistrationDirty(componentTopologyChanged: false);
        }

        private void OnDisable()
        {
            RemoveFromInstanceCache();
            UnregisterOptimizationRegistration();
        }

        private void OnDestroy()
        {
            RemoveFromInstanceCache();
            UnregisterOptimizationRegistration();
        }

        private void RemoveFromInstanceCache()
        {
            GameObject owner = gameObject;
            if (owner == null)
                return;

            if (s_instanceCache.TryGetValue(owner, out WorldProceduralProxyInstance cached) &&
                ReferenceEquals(cached, this))
            {
                s_instanceCache.Remove(owner);
            }
        }

        private static string ResolveScatterLayerLabel(WorldPrefabFamilyProfile family)
        {
            WorldPrefabFamilyProfile.ScatterLayer scatterLayer = family != null
                ? family.scatterLayer
                : WorldPrefabFamilyProfile.ScatterLayer.Ground;
            return scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => ScatterLayerClusterLabel,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => ScatterLayerStructureLabel,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => ScatterLayerSpawnLabel,
                _ => ScatterLayerGroundLabel
            };
        }

        private static string ResolveFieldSourceLabel(WorldProceduralFieldSampler.SeafloorSource source)
        {
            return source switch
            {
                WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight => FieldSourceMapMagicLabel,
                WorldProceduralFieldSampler.SeafloorSource.SceneProbeLegacy => FieldSourceSceneProbeLegacyLabel,
                WorldProceduralFieldSampler.SeafloorSource.MacroGeologyFallback => FieldSourceMacroGeologyLabel,
                WorldProceduralFieldSampler.SeafloorSource.FallbackSynthetic => FieldSourceFallbackLabel,
                _ => FieldSourceNoneLabel
            };
        }

        private static string ResolveGeologyArchetypeLabel(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.generativeGeologyProfile == null)
                return FieldSourceNoneLabel;

            return family.generativeGeologyProfile.shapeArchetype switch
            {
                WorldGenerativeGeologyProfile.ShapeArchetype.Arch => GeologyArchetypeArchLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.Canopy => GeologyArchetypeCanopyLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.ArchCluster => GeologyArchetypeArchClusterLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.ReefPack => GeologyArchetypeReefPackLabel,
                WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge => GeologyArchetypeCaveBridgeLabel,
                _ => GeologyArchetypeComplexRockLabel
            };
        }

        public void Configure(
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            string configuredVariantId,
            bool configuredProxyOnly,
            int configuredClusterIndex,
            int configuredInstanceIndex)
        {
            familyId = family != null ? family.familyId : "world.family.generic";
            familyLabel = family != null ? family.familyLabel : "Generic World Family";
            ruleId = rule != null ? rule.ruleId : "procedural.rule.generic";
            ruleLabel = rule != null ? rule.ruleLabel : "Generic Procedural Rule";
            zoneId = zone != null ? zone.ZoneId : "zone.generic";
            socketId = socket != null ? socket.SocketId : "socket.generic";
            socketKind = socket != null ? socket.Kind : WorldContentSocket.ContentKind.Generic;
            preferredFidelity = family != null ? family.defaultFidelity : WorldSliceAnchor.SliceState.Mid;
            variantId = string.IsNullOrWhiteSpace(configuredVariantId) ? "variant.generic" : configuredVariantId;
            variantHash = string.IsNullOrWhiteSpace(variantId) ? 0 : Hecton.Localization.LocHash.Compute(variantId);
            proxyOnly = configuredProxyOnly;
            supportsFinalVariant = false;
            finalVariantActive = false;
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            runtimeKey = 0L;
            streamingLayer = family != null ? family.ResolveStreamingLayer() : WorldStreamingLayer.Flora;
            placementSource = "Socket";
            scatterLayer = ResolveScatterLayerLabel(family);
            fieldSource = FieldSourceNoneLabel;
            seafloorHeight = socket != null ? socket.transform.position.y : 0f;
            depthMeters = 0f;
            slopeDegrees = 0f;
            curvature = 0f;
            caveProximity = 0f;
            ridgeSignal = 0f;
            canyonSignal = 0f;
            compositionPotential = 0f;
            heatmapChannel = "None";
            heatmapValue = 0f;
            sourceBiomeMatrix = "None";
            sourceBiomeFamily = "None";
            sourceWaterPattern = "None";
            sourceBiomeContext = "None";
            usesGenerativeGeology = family != null && family.UsesGenerativeGeology();
            if (!Application.isPlaying)
                PrepareGeneratedGeologyRuntimeCold();
            collisionEnabled = true;
            geologyProfileId = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.profileId : "None";
            geologyArchetype = ResolveGeologyArchetypeLabel(family);
            cellX = 0;
            cellZ = 0;
            chunkX = 0;
            chunkZ = 0;
            hasMacroZone = false;
            macroZoneX = 0;
            macroZoneZ = 0;
            MarkOptimizationRegistrationDirty(componentTopologyChanged: true);
        }

        private void PrepareGeneratedGeologyRuntimeCold()
        {
            if (!usesGenerativeGeology || _generatedGeologyRoot != null)
                return;

            if (WorldGenerativeGeologyService.TryPrepareRuntimeShellCold(gameObject, out Transform generatedRoot))
                _generatedGeologyRoot = generatedRoot;
        }

        public void ConfigureScatter(
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            WorldZoneAnchor zone,
            string configuredVariantId,
            bool configuredProxyOnly,
            int configuredClusterIndex,
            int configuredInstanceIndex,
            string configuredHeatmapChannel,
            float configuredHeatmapValue,
            WorldProceduralFieldSampler.SeafloorSource configuredFieldSource,
            float configuredSeafloorHeight,
            float configuredDepthMeters,
            float configuredSlopeDegrees,
            float configuredCurvature,
            float configuredCaveProximity,
            float configuredRidgeSignal,
            float configuredCanyonSignal,
            float configuredCompositionPotential,
            string configuredBiomeMatrix,
            string configuredBiomeFamily,
            string configuredWaterPattern,
            string configuredBiomeContext,
            int configuredCellX,
            int configuredCellZ,
            long configuredRuntimeKey = 0L,
            WorldStreamingLayer configuredStreamingLayer = WorldStreamingLayer.Flora,
            WorldChunkCoordinate configuredChunkCoord = default,
            bool configuredHasMacroZone = false,
            WorldMacroZoneCoordinate configuredMacroZoneCoord = default,
            bool configuredSupportsFinalVariant = false,
            bool configuredFinalVariantActive = false,
            bool configuredCollisionEnabled = true)
        {
            familyId = family != null ? family.familyId : "world.family.generic";
            familyLabel = family != null ? family.familyLabel : "Generic World Family";
            ruleId = rule != null ? rule.ruleId : "procedural.rule.generic";
            ruleLabel = rule != null ? rule.ruleLabel : "Generic Procedural Rule";
            zoneId = zone != null ? zone.ZoneId : "zone.generic";
            socketId = "scatter.field";
            socketKind = rule != null ? rule.GetScatterContentKind() : WorldContentSocket.ContentKind.Generic;
            preferredFidelity = family != null ? family.defaultFidelity : WorldSliceAnchor.SliceState.Mid;
            variantId = string.IsNullOrWhiteSpace(configuredVariantId) ? "variant.generic" : configuredVariantId;
            variantHash = string.IsNullOrWhiteSpace(variantId) ? 0 : Hecton.Localization.LocHash.Compute(variantId);
            proxyOnly = configuredProxyOnly;
            supportsFinalVariant = configuredSupportsFinalVariant;
            finalVariantActive = configuredFinalVariantActive;
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            runtimeKey = configuredRuntimeKey;
            streamingLayer = configuredStreamingLayer;
            placementSource = "FieldScatter";
            scatterLayer = ResolveScatterLayerLabel(family);
            fieldSource = ResolveFieldSourceLabel(configuredFieldSource);
            seafloorHeight = configuredSeafloorHeight;
            depthMeters = configuredDepthMeters;
            slopeDegrees = configuredSlopeDegrees;
            curvature = configuredCurvature;
            caveProximity = configuredCaveProximity;
            ridgeSignal = configuredRidgeSignal;
            canyonSignal = configuredCanyonSignal;
            compositionPotential = configuredCompositionPotential;
            heatmapChannel = string.IsNullOrWhiteSpace(configuredHeatmapChannel) ? "None" : configuredHeatmapChannel;
            heatmapValue = Mathf.Clamp01(configuredHeatmapValue);
            sourceBiomeMatrix = string.IsNullOrWhiteSpace(configuredBiomeMatrix) ? "None" : configuredBiomeMatrix;
            sourceBiomeFamily = string.IsNullOrWhiteSpace(configuredBiomeFamily) ? "None" : configuredBiomeFamily;
            sourceWaterPattern = string.IsNullOrWhiteSpace(configuredWaterPattern) ? "None" : configuredWaterPattern;
            sourceBiomeContext = string.IsNullOrWhiteSpace(configuredBiomeContext) ? "None" : configuredBiomeContext;
            usesGenerativeGeology = family != null && family.UsesGenerativeGeology();
            if (!Application.isPlaying)
                PrepareGeneratedGeologyRuntimeCold();
            collisionEnabled = configuredCollisionEnabled;
            geologyProfileId = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.profileId : "None";
            geologyArchetype = ResolveGeologyArchetypeLabel(family);
            cellX = configuredCellX;
            cellZ = configuredCellZ;
            chunkX = configuredChunkCoord.x;
            chunkZ = configuredChunkCoord.z;
            hasMacroZone = configuredHasMacroZone;
            macroZoneX = configuredMacroZoneCoord.x;
            macroZoneZ = configuredMacroZoneCoord.z;
            MarkOptimizationRegistrationDirty(componentTopologyChanged: true);
        }

        public void SetPoolManaged(bool poolManaged)
        {
            _poolManaged = poolManaged;
        }

        public void SetGeneratedGeologyRoot(Transform generatedRoot)
        {
            _generatedGeologyRoot = generatedRoot;
        }

        public Transform ResolveGeneratedGeologyRoot(string generatedRootName)
        {
            if (_generatedGeologyRoot != null)
                return _generatedGeologyRoot;

            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !string.Equals(child.name, generatedRootName, System.StringComparison.Ordinal))
                    continue;

                _generatedGeologyRoot = child;
                return child;
            }

            return null;
        }

        /// <summary>
        /// Re-applies runtime optimization ownership after a pooled instance becomes active.
        /// </summary>
        public void OnSpawn()
        {
            MarkOptimizationRegistrationDirty(componentTopologyChanged: false);
        }

        /// <summary>
        /// Releases runtime optimization ownership before the pooled instance is deactivated.
        /// </summary>
        public void OnDespawn()
        {
            UnregisterOptimizationRegistration();
        }

        public bool IsScatterSyncCurrent(int syncSignature, bool geologyApplied)
        {
            return scatterSyncSignature == syncSignature && generatedGeologyApplied == geologyApplied;
        }

        public void MarkScatterSync(int syncSignature, bool geologyApplied)
        {
            scatterSyncSignature = syncSignature;
            generatedGeologyApplied = geologyApplied;
            MarkOptimizationRegistrationDirty(componentTopologyChanged: true);
        }

        public void RefreshOptimizationRegistrationCold()
        {
            if (!isActiveAndEnabled)
                return;

            if (_runtimeComponentCacheDirty || !_runtimeComponentCacheReady)
                RefreshRuntimeComponentCachesCold();

            if (_collisionStateDirty)
                ApplyCollisionStateFromCache();

            if (_optimizationRegistrationDirty)
            {
                RefreshLodRegistrationFromCache();
                RefreshCullingRegistrationCold();
                _optimizationRegistrationDirty = false;
            }
        }

        private void MarkOptimizationRegistrationDirty(bool componentTopologyChanged)
        {
            if (componentTopologyChanged)
                _runtimeComponentCacheDirty = true;

            _optimizationRegistrationDirty = true;
            _collisionStateDirty = true;
        }

        private void RefreshRuntimeComponentCachesCold()
        {
            _lodGroupBuffer.Clear();
            _colliderBuffer.Clear();
            gameObject.GetComponentsInChildren(true, _lodGroupBuffer);
            gameObject.GetComponentsInChildren(true, _colliderBuffer);
            _runtimeComponentCacheReady = true;
            _runtimeComponentCacheDirty = false;
        }

        private void ApplyCollisionStateFromCache()
        {
            if (!_runtimeComponentCacheReady)
                return;

            for (int i = 0; i < _colliderBuffer.Count; i++)
            {
                Collider current = _colliderBuffer[i];
                if (current != null)
                    current.enabled = collisionEnabled;
            }

            _collisionStateDirty = false;
        }

        private void RefreshLodRegistrationFromCache()
        {
            LODSystemManager manager = LODSystemManager.Instance;
            if (manager == null)
            {
                UnregisterLodGroups();
                _lodSystemManager = null;
                return;
            }

            if (!ReferenceEquals(_lodSystemManager, manager))
            {
                UnregisterLodGroups();
                _lodSystemManager = manager;
            }

            for (int i = _registeredLodGroups.Count - 1; i >= 0; i--)
            {
                LODGroup registered = _registeredLodGroups[i];
                if (registered != null && _lodGroupBuffer.Contains(registered))
                    continue;

                if (registered != null)
                    _lodSystemManager.UnregisterLODGroup(registered);

                _registeredLodGroups.RemoveAt(i);
            }

            for (int i = 0; i < _lodGroupBuffer.Count; i++)
            {
                LODGroup lodGroup = _lodGroupBuffer[i];
                if (lodGroup == null || _registeredLodGroups.Contains(lodGroup))
                    continue;

                _lodSystemManager.RegisterLODGroup(lodGroup);
                _registeredLodGroups.Add(lodGroup);
            }
        }

        private void RefreshCullingRegistrationCold()
        {
            CullingManager manager = CullingManager.Instance;
            if (manager == null)
            {
                UnregisterCulling();
                _cullingManager = null;
                return;
            }

            UnregisterCulling();
            _cullingManager = manager;
            int registeredObjectCount = _cullingManager.RegisteredObjectCount;
            _cullingManager.RegisterCullableObject(gameObject);
            _cullingRegistered = _cullingManager.RegisteredObjectCount > registeredObjectCount;
        }

        private void UnregisterOptimizationRegistration()
        {
            UnregisterLodGroups();
            UnregisterCulling();
        }

        private void UnregisterLodGroups()
        {
            if (_lodSystemManager != null)
            {
                for (int i = _registeredLodGroups.Count - 1; i >= 0; i--)
                {
                    LODGroup lodGroup = _registeredLodGroups[i];
                    if (lodGroup != null)
                        _lodSystemManager.UnregisterLODGroup(lodGroup);
                }
            }

            _registeredLodGroups.Clear();
        }

        private void UnregisterCulling()
        {
            if (!_cullingRegistered)
                return;

            if (_cullingManager != null)
                _cullingManager.UnregisterCullableObject(gameObject);

            _cullingRegistered = false;
        }
    }
}
