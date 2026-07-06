using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class TerrainChunkSignalContractEditTests
    {
        [Test]
        public void TerrainChunkGeneratedSignal_UsesNativeHeightPayloadRevisionWhenAvailable()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "MapMagicBridge.cs");
            string source = File.ReadAllText(path);
            int publishIndex = source.IndexOf("private static bool TryPublishTerrainChunkGenerated", StringComparison.Ordinal);
            Assert.That(publishIndex, Is.GreaterThanOrEqualTo(0));

            int helperIndex = source.IndexOf("TryResolveQuantizedPayloadForSnapshot", publishIndex, StringComparison.Ordinal);
            int revisionIndex = source.IndexOf("cacheRevision = payload.CacheRevision", publishIndex, StringComparison.Ordinal);
            int flagIndex = source.IndexOf("TerrainChunkGeneratedFlagHeightPayloadResolved", publishIndex, StringComparison.Ordinal);
            int staleLiteralIndex = source.IndexOf("CacheRevision = 0", publishIndex, StringComparison.Ordinal);

            Assert.That(helperIndex, Is.GreaterThan(publishIndex));
            Assert.That(revisionIndex, Is.GreaterThan(publishIndex));
            Assert.That(flagIndex, Is.GreaterThan(publishIndex));
            Assert.That(staleLiteralIndex, Is.LessThan(0));
        }

        [Test]
        public void TerrainTileCacheReadAccessor_DoesNotTouchLruByDefault()
        {
            string root = Directory.GetCurrentDirectory();
            string residencyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationTileCacheResidency.cs");
            string residency = File.ReadAllText(residencyPath);
            Assert.That(residency.Contains("bool touchAccess = false"), Is.True);
            Assert.That(residency.Contains("if (touchAccess)") &&
                        residency.Contains("TouchTileCacheState(state);"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            int texturePayloadIndex = bridge.IndexOf("public bool TryGetActiveHeightTexturePayload", StringComparison.Ordinal);
            int samplePayloadIndex = bridge.IndexOf("public bool TryGetActiveHeightSamplePayload", StringComparison.Ordinal);
            Assert.That(texturePayloadIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(samplePayloadIndex, Is.GreaterThan(texturePayloadIndex));
            string texturePayloadBlock = bridge.Substring(texturePayloadIndex, samplePayloadIndex - texturePayloadIndex);
            Assert.That(texturePayloadBlock.Contains("TouchTileCacheState(state);"), Is.False);
        }

        [Test]
        public void MapMagicVegetationBridge_DoesNotRepairDependenciesFromRuntimePhases()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string source = File.ReadAllText(path);

            int slowTickIndex = source.IndexOf("public void SlowTick()", StringComparison.Ordinal);
            int lateFrameIndex = source.IndexOf("public void LateFrameTick()", slowTickIndex, StringComparison.Ordinal);
            int tileAppliedIndex = source.IndexOf("void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileApplied", StringComparison.Ordinal);
            int logIndex = source.IndexOf("private void LogNativePoolFragmentationIfDue", tileAppliedIndex, StringComparison.Ordinal);

            Assert.That(slowTickIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(lateFrameIndex, Is.GreaterThan(slowTickIndex));
            Assert.That(tileAppliedIndex, Is.GreaterThan(lateFrameIndex));
            Assert.That(logIndex, Is.GreaterThan(tileAppliedIndex));

            string slowTickBlock = source.Substring(slowTickIndex, lateFrameIndex - slowTickIndex);
            string tileEventBlock = source.Substring(tileAppliedIndex, logIndex - tileAppliedIndex);
            Assert.That(slowTickBlock.Contains("RefreshColdRuntimeDependencies("), Is.False);
            Assert.That(slowTickBlock.Contains("ResolveRuntimeDependencies("), Is.False);
            Assert.That(slowTickBlock.Contains("WorldRuntimeReferenceUtility."), Is.False);
            Assert.That(tileEventBlock.Contains("RefreshColdRuntimeDependencies("), Is.False);
            Assert.That(tileEventBlock.Contains("ResolveRuntimeDependencies("), Is.False);
            Assert.That(tileEventBlock.Contains("WorldRuntimeReferenceUtility."), Is.False);

            Assert.That(source.Contains("private void RefreshColdRuntimeDependencies()"), Is.True);
            Assert.That(source.Contains("private void ResolveRuntimeDependencies()"), Is.False);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.Player:"), Is.True);
            Assert.That(source.Contains("CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);"), Is.True);
            Assert.That(source.Contains("ClearPlayerRuntimeContext(previousService as IPlayerRuntimeContext);"), Is.True);
        }

        [Test]
        public void TerrainSeamQualityInjection_DoesNotUseRuntimeReflectionOrBoxing()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "WorldGenerativeGeologyTerrainSeamApplier.cs");
            string source = File.ReadAllText(path);
            Assert.That(source.Contains("using System.Reflection;"), Is.False);
            Assert.That(source.Contains("FieldInfo"), Is.False);
            Assert.That(source.Contains(".SetValue("), Is.False);
            Assert.That(source.Contains("object boxed"), Is.False);
            Assert.That(source.Contains("job.GlobalQualityWeight = math.saturate(globalQualityWeight);"), Is.True);
            Assert.That(source.Contains("job.GlobalQualityWeightValid = 1;"), Is.True);
        }

        [Test]
        public void MapMagicReadAccessors_DoNotMutateTerrainTileCacheOrPollRegistry()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string source = File.ReadAllText(path);

            int findIndex = source.IndexOf("private Terrain FindTerrainAt", StringComparison.Ordinal);
            int ownerPhaseIndex = source.IndexOf("private void UpdateLastResolvedTerrainTileOwnerPhase()", StringComparison.Ordinal);
            Assert.That(findIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(ownerPhaseIndex, Is.GreaterThan(findIndex));

            string findBlock = source.Substring(findIndex, ownerPhaseIndex - findIndex);
            Assert.That(findBlock.Contains("_lastResolvedTerrainTile = tile;"), Is.False);
            Assert.That(ownerPhaseIndex, Is.GreaterThan(0));
            Assert.That(source.Contains("HectonMapMagicVegetationBridge.ActiveRuntimeInstance"), Is.False);
            Assert.That(source.Contains("private HectonMapMagicVegetationBridge _cachedVegetationBridge;"), Is.True);
        }

        [Test]
        public void MapMagicBiomeAlphaCache_IsPrewarmedByOwnerPhaseOnly()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("allowCacheRefresh: false"), Is.True);
            Assert.That(source.Contains("private void PrewarmBiomeAlphaTextureCacheOwnerPhase()"), Is.True);
            Assert.That(source.Contains("allowCacheRefresh: true"), Is.True);
        }

        [Test]
        public void MapMagicBridgeActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "MapMagicBridge.cs");
            string bridge = File.ReadAllText(bridgePath);

            Assert.That(bridge.Contains("private static MapMagicBridge s_activeRuntimeInstance;"), Is.True);
            Assert.That(bridge.Contains("internal static MapMagicBridge ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(bridge.Contains("return s_activeRuntimeInstance;"), Is.True);
            Assert.That(bridge.Contains("return GlobalRegistry.MapMagic;"), Is.False);
            Assert.That(bridge.Contains("protected void PublishActiveRuntimeInstance()"), Is.True);
            Assert.That(bridge.Contains("protected void ClearActiveRuntimeInstance()"), Is.True);

            string runtimePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string runtime = File.ReadAllText(runtimePath);
            Assert.That(runtime.Contains("MapMagicBridge current = ActiveRuntimeInstance;"), Is.True);
            Assert.That(runtime.Contains("PublishActiveRuntimeInstance();"), Is.True);
            Assert.That(runtime.Contains("ClearActiveRuntimeInstance();"), Is.True);

            string utilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string utility = File.ReadAllText(utilityPath);
            int resolveIndex = utility.IndexOf("public static bool TryResolveMapMagicBridge", StringComparison.Ordinal);
            int vegetationIndex = utility.IndexOf("public static bool TryResolveHectonMapMagicVegetationBridge", resolveIndex, StringComparison.Ordinal);
            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(vegetationIndex, Is.GreaterThan(resolveIndex));
            string resolveBlock = utility.Substring(resolveIndex, vegetationIndex - resolveIndex);
            Assert.That(resolveBlock.Contains("MapMagicBridge.ActiveRuntimeInstance"), Is.True);
            Assert.That(resolveBlock.Contains("TryResolveLiveCachedActiveRuntime(ref target, ref _CachedMapMagicBridge, MapMagicBridge.ActiveRuntimeInstance)"), Is.True);
            Assert.That(resolveBlock.Contains("if (IsLiveBehaviour(_CachedMapMagicBridge))"), Is.False);
            Assert.That(resolveBlock.Contains("GlobalRegistry.MapMagic"), Is.False);

            string seamPath = Path.Combine(root, "Assets", "_Project", "Scripts", "VoxelSeamDirector.cs");
            string seam = File.ReadAllText(seamPath);
            Assert.That(seam.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge)"), Is.True);
            Assert.That(seam.Contains("MapMagicBridge.Instance"), Is.False);
            Assert.That(seam.Contains("MapMagicBridge mapMagicBridge = GlobalRegistry.MapMagic;"), Is.False);

            string vegetationPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string vegetation = File.ReadAllText(vegetationPath);
            Assert.That(vegetation.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(vegetation.Contains("mapMagicBridge = GlobalRegistry.MapMagic;"), Is.False);

            string orePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "Resources", "ProceduralOreSpawner.cs");
            string ore = File.ReadAllText(orePath);
            Assert.That(ore.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(ore.Contains("mapMagicBridge = GlobalRegistry.MapMagic;"), Is.False);
            Assert.That(ore.Contains("_terrainProvider = GlobalRegistry.Terrain;"), Is.False);

            string outpostPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs");
            string outpost = File.ReadAllText(outpostPath);
            Assert.That(outpost.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);"), Is.True);
            Assert.That(outpost.Contains("_cachedMapMagicBridge = GlobalRegistry.MapMagic;"), Is.False);
            Assert.That(outpost.Contains("global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)"), Is.True);
            Assert.That(outpost.Contains("GlobalRegistry.WorldSeedProvider"), Is.False);

            string biomeMatrixPath = Path.Combine(root, "Assets", "_Project", "Scripts", "BiomeMatrixDirector.cs");
            string biomeMatrix = File.ReadAllText(biomeMatrixPath);
            Assert.That(biomeMatrix.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge)"), Is.True);
            Assert.That(biomeMatrix.Contains("_resolvedTerrainProvider ??= GlobalRegistry.Terrain;"), Is.False);

            string ecosystemPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string ecosystem = File.ReadAllText(ecosystemPath);
            Assert.That(ecosystem.Contains("GlobalRegistry.MapMagic"), Is.False);
            Assert.That(ecosystem.Contains("GlobalRegistry.ResourceDistribution"), Is.False);
            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);"), Is.True);
            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(ecosystem.Contains("MapMagicBridge.Instance"), Is.False);
            Assert.That(ecosystem.Contains("ResourceDistributionDirector resourceDistribution = _cachedResourceDistribution;"), Is.True);
            Assert.That(ecosystem.Contains("ResourceDistributionDirector resourceDistribution = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void BiomeMatrixRuntimeEvaluation_DoesNotRepairPlayerReferences()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "BiomeMatrixDirector.cs");
            string source = File.ReadAllText(path);

            int evaluateIndex = source.IndexOf("private void EvaluateMatrix(bool forcePublish)", StringComparison.Ordinal);
            int resolveReferencesIndex = source.IndexOf("private void ResolveReferences()", evaluateIndex, StringComparison.Ordinal);
            Assert.That(evaluateIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolveReferencesIndex, Is.GreaterThan(evaluateIndex));

            string evaluateBlock = source.Substring(evaluateIndex, resolveReferencesIndex - evaluateIndex);
            int editorGuardIndex = evaluateBlock.IndexOf("if (!Application.isPlaying)", StringComparison.Ordinal);
            int resolveCallIndex = evaluateBlock.IndexOf("ResolveReferences();", StringComparison.Ordinal);
            Assert.That(editorGuardIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolveCallIndex, Is.GreaterThan(editorGuardIndex));
            Assert.That(evaluateBlock.IndexOf("ResolveReferences();", resolveCallIndex + 1, StringComparison.Ordinal), Is.LessThan(0));

            Assert.That(source.Contains("RebindPlayerRuntimeContext(previousService, currentService);"), Is.True);
            Assert.That(source.Contains("private void RebindPlayerRuntimeContext(object previousService, object currentService)"), Is.True);
            Assert.That(source.Contains("if (playerContext.PlayerTransform != null)"), Is.True);
            Assert.That(source.Contains("if (playerContext.PlayerMovement != null)"), Is.True);
        }

        [Test]
        public void MapMagicSplatColorReadPath_UsesCachedTerrainLayers()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string source = File.ReadAllText(path);

            int splatIndex = source.IndexOf("public override bool TryGetTerrainSplatColor(float x, float z", StringComparison.Ordinal);
            int sampleHeightIndex = source.IndexOf("public override float SampleHeightAUP", splatIndex, StringComparison.Ordinal);
            Assert.That(splatIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(sampleHeightIndex, Is.GreaterThan(splatIndex));

            string splatBlock = source.Substring(splatIndex, sampleHeightIndex - splatIndex);
            Assert.That(splatBlock.Contains("terrainData.terrainLayers"), Is.False);
            Assert.That(splatBlock.Contains("TryGetCachedBiomeTerrainLayers(terrainData, totalLayers"), Is.True);
            Assert.That(source.Contains("private bool TryGetCachedBiomeTerrainLayers("), Is.True);
            Assert.That(source.Contains("TerrainLayer[] sourceLayers = terrainData.terrainLayers;"), Is.True);
        }

        [Test]
        public void BiomeWeightMapBake_BindsProductionTerrainControlTexture()
        {
            string root = Directory.GetCurrentDirectory();
            string pipelinePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "BiomeWeightMapBaker", "Editor", "BiomeWeightMapBakePipeline.cs");
            string constantsPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "BiomeWeightMapBaker", "Editor", "BiomeWeightMapBakeJobs.cs");
            string pipeline = File.ReadAllText(pipelinePath);
            string constants = File.ReadAllText(constantsPath);

            Assert.That(pipeline.Contains("ProductionTerrainMaterialPath = \"Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat\""), Is.True);
            Assert.That(pipeline.Contains("TerrainControlTextureProperty = \"_TerrainControlRGBA\""), Is.True);
            Assert.That(pipeline.Contains("TryBindControlTextureToProductionTerrainMaterial(outputPath)"), Is.True);
            Assert.That(pipeline.Contains("material.SetTexture(TerrainControlTextureProperty, texture);"), Is.True);
            Assert.That(pipeline.Contains("EditorUtility.SetDirty(material);"), Is.True);
            Assert.That(constants.Contains("WarningMaterialBindingFailed"), Is.True);
        }

        [Test]
        public void TerrainMaster_EmptyControlMapUsesSlopeWaterlineFallback()
        {
            string root = Directory.GetCurrentDirectory();
            string shaderPath = Path.Combine(root, "Assets", "_Project", "Art", "Shaders", "TerrainMaster.shader");
            string materialPath = Path.Combine(root, "Assets", "_Project", "Art", "Materials", "World", "MAT_H8TerrainLit_BasaltSediment_1428.mat");
            string shader = File.ReadAllText(shaderPath);
            string material = File.ReadAllText(materialPath);

            Assert.That(shader.Contains("void HectonResolveMissingControlWeights("), Is.True);
            Assert.That(shader.Contains("_FallbackWaterlineY"), Is.True);
            Assert.That(shader.Contains("if (hasControl < 0.5h)"), Is.True);
            Assert.That(shader.Contains("HectonResolveMissingControlWeights(IN.positionWS, IN.normalWS, screenIgn, rockWeight, sandWeight, siltWeight, sedimentSource);"), Is.True);
            Assert.That(shader.Contains("half sedimentSource = control.a;"), Is.True);
            Assert.That(shader.Contains("controlSand * hasControl + (1.0h - hasControl)"), Is.False);

            Assert.That(material.Contains("- _FallbackWaterlineY: 14.02"), Is.True);
            Assert.That(material.Contains("- _FallbackShoreWidth:"), Is.True);
            Assert.That(material.Contains("- _FallbackHeightRange:"), Is.True);
        }

        [Test]
        public void ProductionMapMagicTerrainRoute_RemainsEnabledAndMaterialBound()
        {
            string root = Directory.GetCurrentDirectory();
            string scenePath = Path.Combine(root, "Assets", "_Project", "Scenes", "02_HECTON_WORLD.unity");
            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string scene = File.ReadAllText(scenePath);
            string bridge = File.ReadAllText(bridgePath);

            int mapMagicIdentifier = scene.IndexOf("m_EditorClassIdentifier: MapMagic::MapMagic.Core.MapMagicObject", StringComparison.Ordinal);
            Assert.That(mapMagicIdentifier, Is.GreaterThanOrEqualTo(0));
            int blockStart = scene.LastIndexOf("--- !u!114", mapMagicIdentifier, StringComparison.Ordinal);
            int blockEnd = scene.IndexOf("\n--- ", mapMagicIdentifier, StringComparison.Ordinal);
            Assert.That(blockStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(blockEnd, Is.GreaterThan(mapMagicIdentifier));
            string mapMagicBlock = scene.Substring(blockStart, blockEnd - blockStart);

            Assert.That(mapMagicBlock.Contains("m_Enabled: 1"), Is.True);
            Assert.That(mapMagicBlock.Contains("material: {fileID: 2100000, guid: 3d0eae0538ca3ef40bf13fcd5a586ead, type: 2}"), Is.True);
            Assert.That(bridge.Contains("mapMagicObject.enabled = true;"), Is.True);
            Assert.That(bridge.Contains("mapMagicObject.draftsInPlaymode = true;"), Is.True);
            Assert.That(bridge.Contains("mapMagicObject.enabled = false;"), Is.False);
        }

        [Test]
        public void WorldStreamingDirector_RebindsMapMagicLifecycleAndKeepsNewDraftTiles()
        {
            string root = Directory.GetCurrentDirectory();
            string directorPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldStreamingDirector.cs");
            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string referenceUtilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string director = File.ReadAllText(directorPath);
            string bridge = File.ReadAllText(bridgePath);
            string referenceUtility = File.ReadAllText(referenceUtilityPath);

            Assert.That(director.Contains("case GlobalRegistryServiceSlot.Player:"), Is.True);
            Assert.That(director.Contains("case GlobalRegistryServiceSlot.MapMagicRuntime:"), Is.True);
            Assert.That(director.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(director.Contains("RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);"), Is.True);
            Assert.That(director.Contains("isActiveAndEnabled && currentService != null"), Is.True);
            Assert.That(director.Contains("InvalidateStreamingProfileState();"), Is.True);
            Assert.That(director.Contains("private bool ConfigureMapMagicTerrainTopology(bool force)"), Is.True);
            Assert.That(director.Contains("bool topologyChanged = ConfigureMapMagicTerrainTopology(force);"), Is.True);
            Assert.That(director.Contains("force |= topologyChanged;"), Is.True);
            Assert.That(director.Contains("_terrainStreamingTopologyDirty"), Is.True);
            Assert.That(director.Contains("_lastTerrainDraftResolution"), Is.True);
            Assert.That(director.Contains("mapMagicBridge == null || !mapMagicBridge.isActiveAndEnabled"), Is.True);
            Assert.That(director.Contains("biomeSamplerCache == null || !biomeSamplerCache.isActiveAndEnabled"), Is.True);
            Assert.That(director.Contains("scatterBudgetController == null || !scatterBudgetController.isActiveAndEnabled"), Is.True);
            Assert.That(director.Contains("worldSliceDirector == null || !worldSliceDirector.isActiveAndEnabled"), Is.True);

            Assert.That(referenceUtility.Contains("public static void InvalidateMapMagicBridgeCache(MapMagicBridge instance)"), Is.True);
            Assert.That(referenceUtility.Contains("target = null;\r\n            MapMagicBridge active = MapMagicBridge.ActiveRuntimeInstance") ||
                        referenceUtility.Contains("target = null;\n            MapMagicBridge active = MapMagicBridge.ActiveRuntimeInstance"), Is.True);
            Assert.That(bridge.Contains("WorldRuntimeReferenceUtility.InvalidateMapMagicBridgeCache(this);"), Is.True);
            Assert.That(bridge.Contains("mapMagicObject.draftsInPlaymode && tile.draft == null"), Is.True);
            Assert.That(bridge.Contains("tile.draft = CreateRuntimeDetailLevel(tile, isDraft: true);"), Is.True);
            Assert.That(bridge.Contains("keep newly streamed tiles on the runtime draft continuum"), Is.True);
        }

        [Test]
        public void VisibleWorldTerrainRuntimeConsumers_RebindThroughLiveTerrainRuntime()
        {
            string root = Directory.GetCurrentDirectory();
            string scatterPath = Path.Combine(root, "Assets", "_Project", "Scripts", "ScatterBudgetController.cs");
            string vegetationPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string sargassumPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string underwaterPath = Path.Combine(root, "Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string floraPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs");
            string ecosystemPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string playerSpawnerPath = Path.Combine(root, "Assets", "_Project", "Scripts", "HectonPlayerSpawner.cs");
            string fluidEnginePath = Path.Combine(root, "Assets", "_Project", "Scripts", "HectonFluidEngine.cs");
            string buoyancyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "BuoyancyObject.cs");
            string biomeSamplerPath = Path.Combine(root, "Assets", "_Project", "Scripts", "BiomeSamplerCache.cs");
            string fieldSamplerPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldProceduralFieldSampler.cs");
            string footstepAudioPath = Path.Combine(root, "Assets", "_Project", "Scripts", "PlayerFootstepAudio.cs");
            string criticalAudioPath = Path.Combine(root, "Assets", "_Project", "Scripts", "Audio", "PlayerCriticalProceduralAudioRenderer.cs");
            string sargassumCutPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCutManager.cs");
            string drillPath = Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Mining", "DeployableSdfDrillRuntime.cs");
            string wreckPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ProceduralWreckGenerator.cs");
            string voxelSeamPath = Path.Combine(root, "Assets", "_Project", "Scripts", "VoxelSeamDirector.cs");
            string scatter = File.ReadAllText(scatterPath);
            string vegetation = File.ReadAllText(vegetationPath);
            string sargassum = File.ReadAllText(sargassumPath);
            string underwater = File.ReadAllText(underwaterPath);
            string flora = File.ReadAllText(floraPath);
            string ecosystem = File.ReadAllText(ecosystemPath);
            string playerSpawner = File.ReadAllText(playerSpawnerPath);
            string fluidEngine = File.ReadAllText(fluidEnginePath);
            string buoyancy = File.ReadAllText(buoyancyPath);
            string biomeSampler = File.ReadAllText(biomeSamplerPath);
            string fieldSampler = File.ReadAllText(fieldSamplerPath);
            string footstepAudio = File.ReadAllText(footstepAudioPath);
            string criticalAudio = File.ReadAllText(criticalAudioPath);
            string sargassumCut = File.ReadAllText(sargassumCutPath);
            string drill = File.ReadAllText(drillPath);
            string wreck = File.ReadAllText(wreckPath);
            string voxelSeam = File.ReadAllText(voxelSeamPath);

            Assert.That(scatter.Contains("serviceSlot == GlobalRegistryServiceSlot.MapMagicRuntime ||"), Is.True);
            Assert.That(scatter.Contains("serviceSlot == GlobalRegistryServiceSlot.TerrainProviderRuntime"), Is.True);
            Assert.That(scatter.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(scatter.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(scatter.Contains("mapMagicBridge = GlobalRegistry.MapMagic"), Is.False);
            Assert.That(scatter.Contains("ApplyCurrentBudget(force: true);"), Is.True);

            Assert.That(vegetation.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(vegetation.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(vegetation.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(vegetation.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(vegetation.Contains("mapMagicBridge = GlobalRegistry.MapMagic"), Is.False);
            Assert.That(vegetation.Contains("MapMagicBridge bridge = mapMagicBridge != null ? mapMagicBridge : GlobalRegistry.MapMagic"), Is.False);

            Assert.That(sargassum.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(sargassum.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);"), Is.True);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(sargassum.Contains("_mapMagicRuntime = GlobalRegistry.MapMagic"), Is.False);
            Assert.That(sargassum.Contains("MapMagicBridge bridge = _mapMagicRuntime != null ? _mapMagicRuntime : GlobalRegistry.MapMagic"), Is.False);

            Assert.That(underwater.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(underwater.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(underwater.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);"), Is.True);
            Assert.That(underwater.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainRuntime)"), Is.True);
            Assert.That(underwater.Contains("_mapMagicRuntime = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(flora.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(flora.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(flora.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);"), Is.True);
            Assert.That(flora.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicRuntime)"), Is.True);
            Assert.That(flora.Contains("_mapMagicRuntime = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(ecosystem.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(ecosystem.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);"), Is.True);
            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(ecosystem.Contains("_cachedMapMagicBridge = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(playerSpawner.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainBridge)"), Is.True);
            Assert.That(playerSpawner.Contains("MapMagicBridge terrainBridge = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(fluidEngine.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagic)"), Is.True);
            Assert.That(fluidEngine.Contains("MapMagicBridge mapMagic = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(buoyancy.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagic)"), Is.True);
            Assert.That(buoyancy.Contains("MapMagicBridge mapMagic = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(biomeSampler.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(biomeSampler.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(biomeSampler.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(biomeSampler.Contains("mapMagicBridge = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(fieldSampler.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(fieldSampler.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(fieldSampler.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);"), Is.True);
            Assert.That(fieldSampler.Contains("mapMagicBridge = GlobalRegistry.MapMagic"), Is.False);
            Assert.That(fieldSampler.Contains("!mapMagicBridge.isActiveAndEnabled"), Is.True);

            Assert.That(footstepAudio.Contains("serviceSlot == GlobalRegistryServiceSlot.TerrainProviderRuntime"), Is.True);
            Assert.That(footstepAudio.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(footstepAudio.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagic);"), Is.True);
            Assert.That(footstepAudio.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(footstepAudio.Contains("_mapMagic = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(criticalAudio.Contains("case GlobalRegistryServiceSlot.TerrainProviderRuntime:"), Is.True);
            Assert.That(criticalAudio.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);
            Assert.That(criticalAudio.Contains("CacheRegistryServiceReference(serviceSlot, previousService, currentService);"), Is.True);
            Assert.That(criticalAudio.Contains("CacheRegistryServiceReference(serviceSlot, currentService);"), Is.False);
            Assert.That(criticalAudio.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicBridge);"), Is.True);
            Assert.That(criticalAudio.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagic)"), Is.True);
            Assert.That(criticalAudio.Contains("_mapMagicBridge = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(sargassumCut.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainBridge)"), Is.True);
            Assert.That(sargassumCut.Contains("MapMagicBridge terrainBridge = GlobalRegistry.MapMagic"), Is.False);

            Assert.That(drill.Contains("case GlobalRegistryServiceSlot.MapMagicRuntime:"), Is.True);
            Assert.That(drill.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagic);"), Is.True);
            Assert.That(drill.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagic)"), Is.True);
            Assert.That(drill.Contains("MapMagicBridge.Instance"), Is.False);

            Assert.That(wreck.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge)"), Is.True);
            Assert.That(wreck.Contains("MapMagicBridge.Instance"), Is.False);

            Assert.That(voxelSeam.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge)"), Is.True);
            Assert.That(voxelSeam.Contains("MapMagicBridge.Instance"), Is.False);
        }

        [Test]
        public void WorldRuntimeReferenceUtility_InvalidatesCachedRuntimeOwnersOnShutdown()
        {
            string root = Directory.GetCurrentDirectory();
            string referenceUtilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string mapMagicBridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string playerRuntimePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "PlayerRuntimeContextService.cs");
            string referenceUtility = File.ReadAllText(referenceUtilityPath);
            string mapMagicBridge = File.ReadAllText(mapMagicBridgePath);
            string playerRuntime = File.ReadAllText(playerRuntimePath);

            Assert.That(referenceUtility.Contains("private static bool IsLiveTransform(Transform transform)"), Is.True);
            Assert.That(referenceUtility.Contains("transform.gameObject.activeInHierarchy"), Is.True);
            Assert.That(referenceUtility.Contains("if (!TryResolveCurrentPlayerTransform(out Transform active))"), Is.True);
            Assert.That(referenceUtility.Contains("if (ReferenceEquals(target, active))"), Is.True);
            Assert.That(referenceUtility.Contains("if (_CachedPlayerTransform != null)"), Is.False);
            Assert.That(referenceUtility.Contains("public static void InvalidatePlayerTransformCache(Transform instance)"), Is.True);
            Assert.That(referenceUtility.Contains("public static void InvalidateMapMagicBridgeCache(MapMagicBridge instance)"), Is.True);
            Assert.That(mapMagicBridge.Contains("WorldRuntimeReferenceUtility.InvalidateMapMagicBridgeCache(this);"), Is.True);
            Assert.That(playerRuntime.Contains("WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(_playerTransform);"), Is.True);
            Assert.That(playerRuntime.Contains("WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(registeredContext.PlayerTransform);"), Is.True);
        }

        [Test]
        public void WorldRuntimeReferenceUtility_RejectsDisabledStreamingOwners()
        {
            string utilityPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string utility = File.ReadAllText(utilityPath);

            Assert.That(utility.Contains("private static bool IsLiveBehaviour(Behaviour behaviour)"), Is.True);
            Assert.That(utility.Contains("return behaviour != null && behaviour.isActiveAndEnabled;"), Is.True);
            Assert.That(utility.Contains("private static bool TryResolveLiveActiveRuntime<T>(ref T target, T active) where T : Behaviour"), Is.True);
            Assert.That(utility.Contains("if (IsLiveBehaviour(target) && ReferenceEquals(target, active))"), Is.True);
            Assert.That(utility.Contains("target = null;"), Is.True);
            Assert.That(utility.Contains("if (!IsLiveBehaviour(active))"), Is.True);
            Assert.That(utility.Contains("private static bool TryResolveLiveCachedActiveRuntime<T>(ref T target, ref T cache, T active) where T : Behaviour"), Is.True);
            Assert.That(utility.Contains("if (!TryResolveLiveActiveRuntime(ref target, active))"), Is.True);
            Assert.That(utility.Contains("cache = null;"), Is.True);

            string[] liveResolverContracts =
            {
                "return TryResolveLiveActiveRuntime(ref target, BiomeSamplerCache.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, ScatterBudgetController.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldSliceDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldZoneDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldContentDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, ProximityColliderSystem.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, BiomeMatrixDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldProceduralStateRegistry.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldProceduralScatterDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, EcosystemDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, ResourceDistributionDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldFaunaSpawnRegistry.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldProceduralFieldSampler.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldProceduralFillDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldCaveDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, FaunaDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologyService.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologySeamExecutionDirector.ActiveRuntimeInstance);",
                "return TryResolveLiveActiveRuntime(ref target, HectonVoxelEngine.ActiveRuntimeInstance);"
            };

            for (int i = 0; i < liveResolverContracts.Length; i++)
                Assert.That(utility.Contains(liveResolverContracts[i]), Is.True, liveResolverContracts[i]);

            Assert.That(utility.Contains("public static bool TryResolveScavengePopulator(ref ScavengePopulator target)"), Is.True);
            Assert.That(utility.Contains("target != null && target.IsRuntimeOwnerUsable"), Is.True);
            Assert.That(utility.Contains("registered != null && registered.IsRuntimeOwnerUsable"), Is.True);
            Assert.That(utility.Contains("public static void InvalidateScavengePopulatorCache(ScavengePopulator instance)"), Is.True);
        }

        [Test]
        public void StreamingRuntimeOwners_ClearActiveInstanceOnDisable()
        {
            string root = Directory.GetCurrentDirectory();
            AssertStreamingOwnerLifecycle(
                File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "BiomeSamplerCache.cs")));
            AssertStreamingOwnerLifecycle(
                File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ScatterBudgetController.cs")));
            AssertStreamingOwnerLifecycle(
                File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldSliceDirector.cs")));
        }

        [Test]
        public void ScavengePopulatorConsumers_ResolveLiveRuntimeOwnerInsteadOfRegistrySlot()
        {
            string root = Directory.GetCurrentDirectory();
            string populator = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ScavengePopulator.cs"));
            string bootstrap = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));
            string voxel = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs"));
            string scatterBudget = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ScatterBudgetController.cs"));
            string scatterOutput = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "HectonScatterOutput.cs"));

            Assert.That(populator.Contains("internal bool IsRuntimeOwnerUsable =>"), Is.True);
            Assert.That(populator.Contains("WorldRuntimeReferenceUtility.InvalidateScavengePopulatorCache(this);"), Is.True);

            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref populator)"), Is.True);
            Assert.That(bootstrap.Contains("ScavengePopulator populator = GlobalRegistry.ScavengePopulator;"), Is.False);
            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref director)"), Is.True);
            Assert.That(bootstrap.Contains("EcosystemDirector director = EcosystemDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref director)"), Is.True);
            Assert.That(bootstrap.Contains("WorldProceduralScatterDirector director = WorldProceduralScatterDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(voxel.Contains("WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref _scavengePopulator);"), Is.True);
            Assert.That(voxel.Contains("_scavengePopulator = GlobalRegistry.ScavengePopulator;"), Is.False);

            Assert.That(scatterBudget.Contains("WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref scavengePopulator);"), Is.True);
            Assert.That(scatterBudget.Contains("scavengePopulator = GlobalRegistry.ScavengePopulator;"), Is.False);
            Assert.That(scatterBudget.Contains("WorldRuntimeReferenceUtility.TryResolveProximityColliderSystem(ref proximityColliderSystem);"), Is.True);
            Assert.That(scatterBudget.Contains("proximityColliderSystem = ProximityColliderSystem.ActiveRuntimeInstance;"), Is.False);

            Assert.That(scatterOutput.Contains("WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref populator)"), Is.True);
            Assert.That(scatterOutput.Contains("ScavengePopulator populator = GlobalRegistry.ScavengePopulator;"), Is.False);
        }

        [Test]
        public void PlayerVisibleWorldOwnerConsumers_ResolveLiveRuntimeOwnersInsteadOfActiveInstanceReads()
        {
            string root = Directory.GetCurrentDirectory();
            string pdaMap = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs"));
            string pdaSpectrum = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "PDASpectrumTab.cs"));
            string firstHour = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "FirstHourDirector.cs"));
            string thermal = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs"));
            string anomalyResourceBinding = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonAnomalyResourceBinding.cs"));
            string sargassum = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs"));
            string floraRegrowth = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraRegrowthDirector.cs"));
            string groundRadar = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "GroundPenetratingRadarRuntime.cs"));
            string underwater = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs"));
            string spatialAudio = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "SpatialAudioManager.cs"));
            string music = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Audio", "HectonMusicDirector.cs"));
            string atlas = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "AtlasSignal", "AtlasSignalSystem.cs"));
            string baseModule = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "BaseModule.cs"));

            Assert.That(pdaMap.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);"), Is.True);
            Assert.That(pdaMap.Contains("BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(pdaSpectrum.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeDirector);"), Is.True);
            Assert.That(pdaSpectrum.Contains("biomeDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(firstHour.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);"), Is.True);
            Assert.That(firstHour.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);"), Is.True);
            Assert.That(firstHour.Contains("_worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(firstHour.Contains("_biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(thermal.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);"), Is.True);
            Assert.That(thermal.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);"), Is.True);
            Assert.That(thermal.Contains("WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref engine);"), Is.True);
            Assert.That(thermal.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref resourceDistributionDirector);"), Is.True);
            Assert.That(thermal.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref director);"), Is.True);
            Assert.That(thermal.Contains("HectonVoxelEngine engine = currentService as HectonVoxelEngine;"), Is.True);
            Assert.That(thermal.Contains("biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(thermal.Contains("worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(thermal.Contains("HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;"), Is.False);
            Assert.That(thermal.Contains("ResourceDistributionDirector.ActiveRuntimeInstance"), Is.False);

            Assert.That(anomalyResourceBinding.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref director)"), Is.True);
            Assert.That(anomalyResourceBinding.Contains("ResourceDistributionDirector.ActiveRuntimeInstance"), Is.False);

            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);"), Is.True);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);"), Is.True);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector)"), Is.True);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref _proceduralScatterDirector)"), Is.True);
            Assert.That(sargassum.Contains("MapMagicBridge currentMapMagic = currentService as MapMagicBridge;"), Is.True);
            Assert.That(sargassum.Contains("WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref currentMapMagic)"), Is.True);
            Assert.That(sargassum.Contains("_biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(sargassum.Contains("_worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(sargassum.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(sargassum.Contains("_proceduralScatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(sargassum.Contains("currentService is MapMagicBridge currentMapMagic && currentMapMagic.isActiveAndEnabled"), Is.False);

            Assert.That(floraRegrowth.Contains("WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref scatterDirector)"), Is.True);
            Assert.That(floraRegrowth.Contains("WorldProceduralScatterDirector scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(groundRadar.Contains("WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector)"), Is.True);
            Assert.That(groundRadar.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(underwater.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);"), Is.True);
            Assert.That(underwater.Contains("biomeMatrixDirector != null && biomeMatrixDirector.isActiveAndEnabled"), Is.True);
            Assert.That(underwater.Contains("biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(spatialAudio.Contains("WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(ref _worldCaveDirector);"), Is.True);
            Assert.That(spatialAudio.Contains("_worldCaveDirector = WorldCaveDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(music.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref runtimeWorldZoneDirector);"), Is.True);
            Assert.That(music.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref director);"), Is.True);
            Assert.That(music.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref director);"), Is.True);
            Assert.That(music.Contains("CacheRuntimeWorldZoneDirectorCold(WorldZoneDirector.ActiveRuntimeInstance);"), Is.False);

            Assert.That(atlas.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);"), Is.True);
            Assert.That(atlas.Contains("BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(baseModule.Contains("WorldRuntimeReferenceUtility.TryResolveWorldFaunaSpawnRegistry(ref registry);"), Is.True);
            Assert.That(baseModule.Contains("WorldFaunaSpawnRegistry registry = WorldFaunaSpawnRegistry.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void DestructibleOrganicManagerConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string utility = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs"));
            string manager = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "DestructibleOrganicManager.cs"));
            string ecosystem = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs"));
            string flora = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs"));
            string chemical = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ChemicalInfluenceGrid.cs"));
            string transport = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "MountablePlayerTransport.cs"));

            Assert.That(utility.Contains("public static bool TryResolveDestructibleOrganicManager(ref DestructibleOrganicManager target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, DestructibleOrganicManager.ActiveRuntimeInstance)"), Is.True);

            int onDisableIndex = manager.IndexOf("private void OnDisable()", StringComparison.Ordinal);
            int unregisterIndex = manager.IndexOf("TryUnregisterTickLanes();", onDisableIndex, StringComparison.Ordinal);
            Assert.That(onDisableIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(unregisterIndex, Is.GreaterThan(onDisableIndex));
            string onDisableBlock = manager.Substring(onDisableIndex, unregisterIndex - onDisableIndex);
            Assert.That(onDisableBlock.Contains("if (ReferenceEquals(_activeRuntimeInstance, this))"), Is.True);
            Assert.That(onDisableBlock.Contains("_activeRuntimeInstance = null;"), Is.True);

            Assert.That(ecosystem.Contains("TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);"), Is.True);
            Assert.That(ecosystem.Contains("DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(flora.Contains("WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref activeManager);"), Is.True);
            Assert.That(flora.Contains("WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref _destructibleOrganicManager);"), Is.True);
            Assert.That(flora.Contains("DestructibleOrganicManager.ActiveRuntimeInstance"), Is.False);

            Assert.That(chemical.Contains("WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref organicManager);"), Is.True);
            Assert.That(chemical.Contains("DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(transport.Contains("WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref organicManager);"), Is.True);
            Assert.That(transport.Contains("WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref destructibleOrganicManager);"), Is.True);
            Assert.That(transport.Contains("DestructibleOrganicManager.ActiveRuntimeInstance"), Is.False);
        }

        [Test]
        public void AbyssalThermalManagerConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string utility = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs"));
            string ecosystem = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs"));
            string volcanic = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VolcanicUpdraftDirector.cs"));
            string thermal = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs"));
            string bioCable = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "BioCableIK.cs"));

            Assert.That(utility.Contains("public static bool TryResolveAbyssalThermalManager(ref AbyssalThermalManager target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, AbyssalThermalManager.ActiveRuntimeInstance)"), Is.True);

            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref thermalManager);"), Is.True);
            Assert.That(ecosystem.Contains("AbyssalThermalManager thermalManager = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(volcanic.Contains("WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref thermalManager);"), Is.True);
            Assert.That(volcanic.Contains("_thermodynamicsService = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(thermal.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(thermal.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(thermal.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(thermal.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(thermal.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(thermal.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(thermal.Contains("_objectPoolService = currentService as IObjectPoolService;"), Is.False);
            Assert.That(thermal.Contains("_objectPoolService = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(thermal.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);

            Assert.That(bioCable.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(bioCable.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.True);
            Assert.That(bioCable.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(bioCable.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(bioCable.Contains("ObjectPoolManager.TryResolvePoolForInstance(_sparkPooledInstance, pool, out IObjectPoolService ownerPool)"), Is.True);
            Assert.That(bioCable.Contains("CacheObjectPoolServiceCold(null);"), Is.True);
            Assert.That(bioCable.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);
            Assert.That(bioCable.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
        }

        [Test]
        public void HectonDirectorAIConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string directorSource = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "HectonDirectorAI.cs"));
            string bootstrap = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));
            string ecosystem = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs"));
            string flora = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs"));
            string telemetry = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "CrashTelemetryBuffer.cs"));

            Assert.That(directorSource.Contains("internal static bool TryResolveActiveRuntime(ref HectonDirectorAI target)"), Is.True);
            Assert.That(directorSource.Contains("!active._encounterDirectorServiceRegistered"), Is.True);

            Assert.That(bootstrap.Contains("HectonDirectorAI.TryResolveActiveRuntime(ref director);"), Is.True);
            Assert.That(bootstrap.Contains("HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;"), Is.False);

            Assert.That(ecosystem.Contains("HectonDirectorAI.TryResolveActiveRuntime(ref director);"), Is.True);
            Assert.That(ecosystem.Contains("HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;"), Is.False);

            Assert.That(flora.Contains("HectonDirectorAI.TryResolveActiveRuntime(ref director);"), Is.True);
            Assert.That(flora.Contains("HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;"), Is.False);

            Assert.That(telemetry.Contains("HectonDirectorAI.TryResolveActiveRuntime(ref director);"), Is.True);
            Assert.That(telemetry.Contains("HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void FloraInteractionManagerConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string utility = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs"));
            string bootstrap = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));
            string baseModule = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "BaseModule.cs"));
            string vehicleMotor = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "VehicleMotor.cs"));
            string constructionManager = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ConstructionManager.cs"));
            string baseDegradation = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Construction", "BaseDegradationSystem.cs"));
            string submarineFluid = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "SubmarineFluidDynamics.cs"));
            string tuner = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Editor", "FloraSwayTunerWindow.cs"));

            Assert.That(utility.Contains("public static bool TryResolveFloraInteractionManager(ref FloraInteractionManager target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, FloraInteractionManager.ActiveRuntimeInstance)"), Is.True);

            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref manager);"), Is.True);
            Assert.That(bootstrap.Contains("FloraInteractionManager manager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(baseModule.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref floraInteractionManager);"), Is.True);
            Assert.That(baseModule.Contains("FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(vehicleMotor.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref floraInteractionManager);"), Is.True);
            Assert.That(vehicleMotor.Contains("FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(constructionManager.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref floraInteractionManager);"), Is.True);
            Assert.That(constructionManager.Contains("FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(baseDegradation.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref floraInteractionManager);"), Is.True);
            Assert.That(baseDegradation.Contains("FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(submarineFluid.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref floraInteractionManager);"), Is.True);
            Assert.That(submarineFluid.Contains("FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);

            Assert.That(tuner.Contains("WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref _target);"), Is.True);
            Assert.That(tuner.Contains("_target = FloraInteractionManager.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void HectonUnderwaterVisualsConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string utility = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs"));
            string flora = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs"));
            string debugUi = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "HectonSystemsDebugUI.cs"));
            string playerContext = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "PlayerRuntimeContextService.cs"));
            string sensory = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "PlayerSensoryManager.cs"));

            Assert.That(utility.Contains("public static bool TryResolveHectonUnderwaterVisuals(ref HectonUnderwaterVisuals target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, HectonUnderwaterVisuals.ActiveRuntimeInstance)"), Is.True);

            Assert.That(flora.Contains("WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref underwaterVisuals);"), Is.True);
            Assert.That(flora.Contains("HectonUnderwaterVisuals underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;"), Is.False);

            Assert.That(debugUi.Contains("WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref _resolvedUnderwaterVisuals);"), Is.True);
            Assert.That(debugUi.Contains("_resolvedUnderwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;"), Is.False);

            Assert.That(playerContext.Contains("WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref _underwaterVisuals);"), Is.True);
            Assert.That(playerContext.Contains("_underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;"), Is.False);

            Assert.That(sensory.Contains("WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref _underwaterVisuals);"), Is.True);
            Assert.That(sensory.Contains("_underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void WorldReadabilityBootstrap_ResolvesLiveRuntimeOwners()
        {
            string root = Directory.GetCurrentDirectory();
            string utility = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs"));
            string bootstrap = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldReadabilityRuntimeBootstrap.cs"));
            string relayAuthoring = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Editor", "RelayRouteAuthoringUtility.cs"));

            Assert.That(utility.Contains("public static bool TryResolveWorldReadabilityDirector(ref WorldReadabilityDirector target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, WorldReadabilityDirector.ActiveRuntimeInstance)"), Is.True);
            Assert.That(utility.Contains("public static bool TryResolveEmergencyServiceRelayDirector(ref EmergencyServiceRelayDirector target)"), Is.True);
            Assert.That(utility.Contains("TryResolveLiveActiveRuntime(ref target, EmergencyServiceRelayDirector.ActiveRuntimeInstance)"), Is.True);

            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveWorldReadabilityDirector(ref existingDirector);"), Is.True);
            Assert.That(bootstrap.Contains("WorldReadabilityDirector existingDirector = WorldReadabilityDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(bootstrap.Contains("IDepthZoneReadModel runtimeDepthZoneReadModel = GlobalRegistry.DepthZoneReadModel;"), Is.True);
            Assert.That(bootstrap.Contains("runtimeWorldZoneDirector == null &&"), Is.True);
            Assert.That(bootstrap.Contains("runtimeBiomeMatrixDirector == null &&"), Is.True);
            Assert.That(bootstrap.Contains("runtimeDepthZoneReadModel == null"), Is.True);
            Assert.That(bootstrap.Contains("runtimeWorldZoneDirector == null || runtimeBiomeMatrixDirector == null"), Is.False);

            Assert.That(bootstrap.Contains("WorldRuntimeReferenceUtility.TryResolveEmergencyServiceRelayDirector(ref existingDirector);"), Is.True);
            Assert.That(bootstrap.Contains("EmergencyServiceRelayDirector existingDirector = EmergencyServiceRelayDirector.ActiveRuntimeInstance;"), Is.False);

            Assert.That(relayAuthoring.Contains("WorldRuntimeReferenceUtility.TryResolveEmergencyServiceRelayDirector(ref director);"), Is.True);
            Assert.That(relayAuthoring.Contains("EmergencyServiceRelayDirector director = EmergencyServiceRelayDirector.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void SuitHudConsumers_ResolveLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string hud = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs"));

            Assert.That(hud.Contains("internal static bool TryResolveActiveRuntime(ref SuitHUDV4CanvasOverlay target)"), Is.True);
            Assert.That(hud.Contains("active == null || !active.isActiveAndEnabled"), Is.True);

            string[] consumerPaths =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "AcousticEcholocationTranslator.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "ARWaypointOverlay.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "FontStreamingManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "FakeRadarBlipController.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "HectonSubmarineOsDisplay.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "HectonOSBootManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "PDADeathMemoryDump.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "RelayHUDRuntimeBootstrap.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "ShaderCompassRibbon.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "SonarHoloCompass.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "SubtitleManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "VFX", "CameraJuiceSystem.cs")
            };

            for (int i = 0; i < consumerPaths.Length; i++)
            {
                string consumer = File.ReadAllText(consumerPaths[i]);
                Assert.That(consumer.Contains("SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref "), Is.True, consumerPaths[i]);
                Assert.That(consumer.Contains("SuitHUDV4CanvasOverlay.ActiveRuntimeInstance"), Is.False, consumerPaths[i]);
            }
        }

        [Test]
        public void PlayerPdaContextBridge_ResolvesLiveRuntimeOwner()
        {
            string root = Directory.GetCurrentDirectory();
            string pda = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "PlayerPDA.cs"));
            string context = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "PlayerRuntimeContextService.cs"));

            Assert.That(pda.Contains("internal static bool TryResolveActiveRuntime(ref PlayerPDA target)"), Is.True);
            Assert.That(pda.Contains("active == null || !active.isActiveAndEnabled"), Is.True);
            Assert.That(context.Contains("PlayerPDA.TryResolveActiveRuntime(ref _playerPda);"), Is.True);
            Assert.That(context.Contains("_playerPda = PlayerPDA.ActiveRuntimeInstance;"), Is.False);
        }

        [Test]
        public void ObjectPoolAndPrefabRegistryConsumers_ResolveLiveRuntimeOwners()
        {
            string root = Directory.GetCurrentDirectory();

            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ObjectPoolManager.cs"));
            Assert.That(content.Contains("internal static bool TryResolveActiveRuntime(ref ObjectPoolManager target)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager registered = GlobalRegistry.ObjectPoolRuntimeMirror;"), Is.True);
            Assert.That(content.Contains("internal static bool IsRuntimeOwnerUsableForRegistry(ObjectPoolManager runtime)"), Is.True);
            Assert.That(content.Contains("if (!IsRuntimeOwnerUsable(ActiveRuntimeInstance))"), Is.True);
            Assert.That(content.Contains("if (_serviceRegistered)\r\n                ActiveRuntimeInstance = this;") ||
                        content.Contains("if (_serviceRegistered)\n                ActiveRuntimeInstance = this;"), Is.True);
            Assert.That(content.Contains("if (ActiveRuntimeInstance == null)"), Is.False);
            Assert.That(content.Contains("internal static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.True);
            Assert.That(content.Contains("internal static bool TryResolvePoolForInstance("), Is.True);
            Assert.That(content.Contains("internal static void DespawnOrDeactivate(GameObject instance, IObjectPoolService preferredPool)"), Is.True);
            Assert.That(content.Contains("!runtime._serviceShuttingDown"), Is.True);
            Assert.That(content.Contains("ClearRuntimeMirrorIfOwnedBy(registered);"), Is.True);
            Assert.That(content.Contains("if (ReferenceEquals(runtime, null))"), Is.True);
            Assert.That(content.Contains("GlobalRegistry.UnregisterObjectPoolService(runtime);"), Is.True);
            Assert.That(content.Contains("marker.Initialize(this, prefabId"), Is.True);
            Assert.That(content.Contains("private ObjectPoolManager _owner;"), Is.True);
            Assert.That(content.Contains("public ObjectPoolManager Owner => _owner;"), Is.True);
            Assert.That(content.Contains("_owner = owner;"), Is.True);
            Assert.That(content.Contains("if (_owner == null)"), Is.False);
            Assert.That(content.Contains("private void DespawnNowOrDestroy()"), Is.True);
            Assert.That(content.Contains("private bool TryResolveOwningPool(out ObjectPoolManager pool)"), Is.True);
            Assert.That(content.Contains("owner.CanDespawnWithoutDestroy(gameObject)"), Is.True);
            Assert.That(content.Contains("active.CanDespawnWithoutDestroy(gameObject)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.Instance"), Is.False);
            Assert.That(systemDispatcher.Contains("IObjectPoolService content = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("PrefabRegistry.TryResolveActiveRuntime(ref registry)"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "PrefabRegistry.cs"));
            Assert.That(content.Contains("internal static bool TryResolveActiveRuntime(ref PrefabRegistry target)"), Is.True);
            Assert.That(bootstrap.Contains("PrefabRegistry.TryResolveActiveRuntime(ref content)"), Is.True);
            Assert.That(scatter.Contains("PrefabRegistry.TryResolveActiveRuntime(ref content)"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "GlobalRegistry.cs"));
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_objectPool)"), Is.True);
            Assert.That(content.Contains("public static IObjectPoolService ObjectPoolService => ObjectPool;"), Is.True);
            Assert.That(content.Contains("internal static ObjectPoolManager ObjectPoolRuntimeMirror => _objectPool;"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "SystemDispatcher.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_objectPool = currentService as IObjectPoolService;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "ThreadSafeCommandQueue.cs"));
            Assert.That(content.Contains("BindObjectPoolServiceCold(null);"), Is.True);
            Assert.That(content.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.False);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("Volatile.Write(ref _objectPool, GlobalRegistry.ObjectPoolService);"), Is.False);
            Assert.That(content.Contains("ObjectPoolManager.TryResolvePoolForInstance(instance, pool, out IObjectPoolService ownerPool)"), Is.True);
            Assert.That(content.Contains("ownerPool.Despawn(instance, Mathf.Max(0f, command.FloatValue));"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref objectPoolManager);"), Is.True);
            Assert.That(content.Contains("PrefabRegistry.TryResolveActiveRuntime(ref registry)"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Audio", "HectonMusicDirector.cs"));
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "RuntimeInstanceId.cs"));
            Assert.That(content.Contains("PrefabRegistry.TryResolveActiveRuntime(ref registry)"), Is.True);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ResourceNode.cs"));
            Assert.That(content.Contains("private static void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("IObjectPoolService pool = s_objectPool;"), Is.False);
            Assert.That(content.Contains("s_objectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("s_objectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            }
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "HarvestableOutcrop.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "HarvestablePlant.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "OxygenPlant.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "OxygenBubble.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonVoxelStreamingBridge.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ResourceDistributionDirector.cs")));
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("currentService as IObjectPoolService"), Is.False);
            Assert.That(content.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _objectPoolManager"), Is.False);
            Assert.That(content.Contains("_objectPoolManager = currentService"), Is.False);
            Assert.That(content.Contains("_objectPoolManager = GlobalRegistry"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ImpostorSystem.cs"));
            Assert.That(content.Contains("public IObjectPoolService BillboardPoolOwner;"), Is.True);
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.False);
            Assert.That(content.Contains("private static void DespawnBillboardOrDeactivate(IObjectPoolService pool, GameObject billboardObject)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("instance.BillboardPoolOwner = pool;"), Is.True);
            Assert.That(content.Contains("DespawnBillboardOrDeactivate(instance.BillboardPoolOwner, instance.BillboardObject);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.DespawnOrDeactivate(billboardObject, pool);"), Is.True);
            Assert.That(content.Contains("_objectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_objectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _objectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "Outposts", "MarauderOutpostGenerationService.cs"));
            Assert.That(content.Contains("private IObjectPoolService[] _spawnedInteractableOwners;"), Is.True);
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.True);
            Assert.That(content.Contains("private static bool TryResolvePoolForInstance("), Is.True);
            Assert.That(content.Contains("private static void DespawnInteractableProxyOrDeactivate(IObjectPoolService pool, GameObject instance)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("_spawnedInteractableOwners[i] = instance != null ? pool : null;"), Is.True);
            Assert.That(content.Contains("DespawnInteractableProxyOrDeactivate(ownerPool, instance);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.PoolItemMarker marker"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref active)"), Is.True);
            Assert.That(content.Contains("ownerPool.Despawn(instance);"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            }
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "FaunaDirector.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ProceduralWreckGenerator.cs")));
            AssertResourceObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCollapseChunk.cs")));
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "PlayerToolManager.cs"));
            AssertResourceObjectPoolConsumerUsesLiveResolver(content);
            Assert.That(content.Contains("private bool TryResolvePoolForInstance("), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolvePoolForInstance(instance, preferredPool, out pool)"), Is.True);
            Assert.That(content.Contains("preferredManager.CanDespawnWithoutDestroy(instance)"), Is.False);
            Assert.That(content.Contains("pool.CanDespawnWithoutDestroy(instance)"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ConstructionManager.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "BaseModule.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = Hecton8.Core.GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "PlayerBuilder.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("return TryResolveCachedObjectPool(out pool);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "BeaconNetworkSystem.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "ProximityColliderSystem.cs"));
            Assert.That(content.Contains("private void CacheObjectPool(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPool(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPool(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("pool.CanDespawnWithoutDestroy(colliderObject)"), Is.True);
            Assert.That(content.Contains("CacheObjectPool(currentService as IObjectPoolService);"), Is.False);
            Assert.That(content.Contains("CacheObjectPool(GlobalRegistry.ObjectPoolService);"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _objectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "UIParticleEffect.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_uiEffectPool)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolvePoolForInstance(instance, pool, out IObjectPoolService ownerPool)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Narrative", "ProceduralLoreDirector.cs"));
            Assert.That(content.Contains("private bool CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)"), Is.False);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.DespawnOrDeactivate(instance, pool);"), Is.True);
            Assert.That(content.Contains("_objectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _objectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "MantaEmergencyWreck.cs"));
            Assert.That(content.Contains("private static void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("s_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("s_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService poolManager = s_cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "MantaScooter.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService poolManager = _cachedObjectPool;"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "FloraProjectile.cs"));
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)"), Is.True);
            Assert.That(content.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
            }
            {
                string content = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "RandomEventSystem.cs"));
            Assert.That(content.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(content.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(content.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(content.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(content.Contains("_cachedObjectPool = currentService as IObjectPoolService;"), Is.False);
            Assert.That(content.Contains("_cachedObjectPool = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(content.Contains("IObjectPoolService pool = _cachedObjectPool;"), Is.False);
            }
            AssertStaticObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "HectonItem.cs")));
            AssertStaticObjectPoolConsumerUsesLiveResolver(File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Items", "PickupItem.cs")));
        }

        [Test]
        public void H8PrefabRegistryBridge_RevalidatesRuntimeOwnerAtBindAndFlush()
        {
            string root = Directory.GetCurrentDirectory();
            string bridgeRoot = Path.Combine(root, "Assets", "_Project", "Scripts", "Core", "Bridge");
            string binder = File.ReadAllText(Path.Combine(bridgeRoot, "H8PrefabRegistryRuntimeBinder.cs"));
            string scheduler = File.ReadAllText(Path.Combine(bridgeRoot, "H8BridgeLiveSyncScheduler.cs"));
            string registry = File.ReadAllText(Path.Combine(bridgeRoot, "H8PrefabRegistry.cs"));
            string editorWindow = File.ReadAllText(Path.Combine(bridgeRoot, "Editor", "H8PrefabRegistryWindow.cs"));

            Assert.That(binder.Contains("public static PrefabRegistry ResolveRuntimeRegistryForBinding()"), Is.True);
            Assert.That(binder.Contains("return PrefabRegistry.TryResolveActiveRuntime(ref runtimeRegistry) ? runtimeRegistry : null;"), Is.True);
            Assert.That(binder.Contains("if (!PrefabRegistry.TryResolveActiveRuntime(ref runtimeRegistry))"), Is.True);
            Assert.That(binder.Contains("runtimeRegistry = null;"), Is.True);
            Assert.That(binder.Contains("GlobalRegistry.PrefabRegistryRuntime"), Is.False);
            Assert.That(binder.Contains("H8BridgeLiveSyncScheduler.RequestPrefabBind(registry, vault, runtimeRegistry);"), Is.True);
            Assert.That(binder.Contains("allowRuntimeRegistration: allowBufferGrowth"), Is.True);
            Assert.That(binder.Contains("allowRuntimeRegistration: runtimeRegistry != null"), Is.True);
            Assert.That(binder.Contains("if (runtimePrefabId <= 0)"), Is.True);

            Assert.That(scheduler.Contains("PrefabRegistry runtimeRegistry = request.RuntimeRegistry;"), Is.True);
            Assert.That(scheduler.Contains("!PrefabRegistry.TryResolveActiveRuntime(ref runtimeRegistry)"), Is.True);
            Assert.That(scheduler.Contains("H8PrefabRegistryRuntimeBinder.BindExistingBuffers(request.Registry, request.Vault, runtimeRegistry);"), Is.True);
            Assert.That(scheduler.Contains("GlobalRegistry.PrefabRegistryRuntime"), Is.False);

            Assert.That(registry.Contains("H8PrefabRegistryRuntimeBinder.ResolveRuntimeRegistryForBinding()"), Is.True);
            Assert.That(registry.Contains("GlobalRegistry.PrefabRegistryRuntime"), Is.False);
            Assert.That(editorWindow.Contains("H8PrefabRegistryRuntimeBinder.ResolveRuntimeRegistryForBinding()"), Is.True);
            Assert.That(editorWindow.Contains("GlobalRegistry.PrefabRegistryRuntime"), Is.False);

            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            foreach (string scriptPath in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (scriptPath.EndsWith("PrefabRegistry.cs", StringComparison.Ordinal))
                    continue;

                string script = File.ReadAllText(scriptPath);
                Assert.That(script.Contains("GlobalRegistry.PrefabRegistryRuntime"), Is.False, scriptPath);
            }
        }

        private static void AssertResourceObjectPoolConsumerUsesLiveResolver(string source)
        {
            Assert.That(source.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(source.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(source.Contains("currentService as IObjectPoolService"), Is.False);
            Assert.That(source.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
            Assert.That(source.Contains("IObjectPoolService pool = _objectPool"), Is.False);
            Assert.That(source.Contains("_objectPool = currentService"), Is.False);
            Assert.That(source.Contains("_objectPool = GlobalRegistry"), Is.False);
        }

        private static void AssertStaticObjectPoolConsumerUsesLiveResolver(string source)
        {
            Assert.That(source.Contains("private static void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(source.Contains("private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(source.Contains("currentService as IObjectPoolService"), Is.False);
            Assert.That(source.Contains("GlobalRegistry.ObjectPoolService"), Is.False);
            Assert.That(source.Contains("IObjectPoolService pool = s_objectPool"), Is.False);
            Assert.That(source.Contains("s_objectPool = currentService"), Is.False);
            Assert.That(source.Contains("s_objectPool = GlobalRegistry"), Is.False);
        }

        [Test]
        public void RuntimeOwnerConsumers_DoNotBypassLiveResolverWithActiveInstanceAssignments()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts");
            string[] blockedAssignments =
            {
                "= BiomeMatrixDirector.ActiveRuntimeInstance",
                "= WorldZoneDirector.ActiveRuntimeInstance",
                "= WorldContentDirector.ActiveRuntimeInstance",
                "= ProximityColliderSystem.ActiveRuntimeInstance",
                "= WorldProceduralStateRegistry.ActiveRuntimeInstance",
                "= WorldFaunaSpawnRegistry.ActiveRuntimeInstance",
                "= WorldProceduralFieldSampler.ActiveRuntimeInstance",
                "= WorldProceduralFillDirector.ActiveRuntimeInstance",
                "= WorldCaveDirector.ActiveRuntimeInstance",
                "= FaunaDirector.ActiveRuntimeInstance",
                "= WorldGenerativeGeologyService.ActiveRuntimeInstance",
                "= WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance",
                "= WorldGenerativeGeologySeamExecutionDirector.ActiveRuntimeInstance",
                "= HectonVoxelEngine.ActiveRuntimeInstance",
                "= DestructibleOrganicManager.ActiveRuntimeInstance",
                "= AbyssalThermalManager.ActiveRuntimeInstance",
                "= HectonDirectorAI.ActiveRuntimeInstance",
                "= FloraInteractionManager.ActiveRuntimeInstance",
                "= HectonUnderwaterVisuals.ActiveRuntimeInstance",
                "= WorldReadabilityDirector.ActiveRuntimeInstance",
                "= EmergencyServiceRelayDirector.ActiveRuntimeInstance",
                "= SuitHUDV4CanvasOverlay.ActiveRuntimeInstance",
                "= HectonMusicDirectorAnchor.ActiveRuntimeInstance",
                "= PlayerPDA.ActiveRuntimeInstance",
                "= AccessibilitySettings.ActiveRuntimeInstance",
                "= InputDispatcher.ActiveRuntimeInstance",
                "= RebindingManager.ActiveRuntimeInstance",
                "= ObjectPoolManager.ActiveRuntimeInstance",
                "= PrefabRegistry.ActiveRuntimeInstance"
            };

            foreach (string scriptPath in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string script = File.ReadAllText(scriptPath);
                foreach (string blockedAssignment in blockedAssignments)
                    Assert.That(script.Contains(blockedAssignment), Is.False, scriptPath + " " + blockedAssignment);
            }
        }

        [Test]
        public void MapMagicGeneratorSeeds_UseWorldGeneratorSnapshotInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string worldGeneratorPath = Path.Combine(root, "Assets", "_Project", "Scripts", "HectonWorldGenerator.cs");
            string worldGenerator = File.ReadAllText(worldGeneratorPath);
            Assert.That(worldGenerator.Contains("internal static bool TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)"), Is.True);
            Assert.That(worldGenerator.Contains("private static HectonWorldGenerator s_activeWorldSeedProvider;"), Is.True);
            Assert.That(worldGenerator.Contains("PublishActiveRuntimeWorldSeed();"), Is.True);
            Assert.That(worldGenerator.Contains("ClearActiveRuntimeWorldSeed();"), Is.True);

            string sandboxPath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "HectonSandboxAbyssalShelfMapMagicNode.cs");
            string sandbox = File.ReadAllText(sandboxPath);
            Assert.That(sandbox.Contains("global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)"), Is.True);
            Assert.That(sandbox.Contains("GlobalRegistry.WorldSeedProvider"), Is.False);

            string spaceEnginePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "HectonSpaceEngine098MapMagicNodes.cs");
            string spaceEngine = File.ReadAllText(spaceEnginePath);
            Assert.That(spaceEngine.Contains("global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)"), Is.True);
            Assert.That(spaceEngine.Contains("GlobalRegistry.WorldSeedProvider"), Is.False);
        }

        [Test]
        public void MapMagicRockOutputFinalize_DoesNotUseListStagingOrToArrayCopies()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "HectonRockOutput.cs");
            string source = File.ReadAllText(path);
            int finalizeIndex = source.IndexOf("public static void Finalize(TileData data, StopToken stop)", StringComparison.Ordinal);
            int applyDataIndex = source.IndexOf("public class HectonRockApplyData", finalizeIndex, StringComparison.Ordinal);
            Assert.That(finalizeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(applyDataIndex, Is.GreaterThan(finalizeIndex));
            string finalizeBlock = source.Substring(finalizeIndex, applyDataIndex - finalizeIndex);

            Assert.That(source.Contains("private struct LayerBuildState"), Is.True);
            Assert.That(finalizeBlock.Contains("Dictionary<int, LayerBuildState> layerBuildStates"), Is.True);
            Assert.That(finalizeBlock.Contains("new List<Matrix4x4>"), Is.False);
            Assert.That(finalizeBlock.Contains("Dictionary<int, List<Matrix4x4>>"), Is.False);
            Assert.That(finalizeBlock.Contains(".ToArray()"), Is.False);
            Assert.That(finalizeBlock.Contains("Array.Empty<Matrix4x4>()"), Is.False);
            Assert.That(finalizeBlock.Contains("int activeLayerCount = 0;"), Is.True);
            Assert.That(finalizeBlock.Contains("if (kvp.Value.Count > 0)"), Is.True);
            Assert.That(finalizeBlock.Contains("layerMatrices = new Dictionary<int, Matrix4x4[]>(activeLayerCount)"), Is.True);

            int applyIndex = source.IndexOf("public void Apply(UnityEngine.Terrain terrain)", applyDataIndex, StringComparison.Ordinal);
            int resolutionIndex = source.IndexOf("public int Resolution", applyIndex, StringComparison.Ordinal);
            Assert.That(applyIndex, Is.GreaterThan(applyDataIndex));
            Assert.That(resolutionIndex, Is.GreaterThan(applyIndex));
            string applyBlock = source.Substring(applyIndex, resolutionIndex - applyIndex);
            Assert.That(applyBlock.Contains("HectonRockManager manager = GlobalRegistry.RockManager;"), Is.True);
            Assert.That(applyBlock.Contains("HectonRockManager manager = HectonRockManager.Instance;"), Is.False);
            int unregisterIndex = applyBlock.IndexOf("manager.UnregisterChunk(chunkCoord);", StringComparison.Ordinal);
            int nullCheckIndex = applyBlock.IndexOf("if (layerMatrices == null || layerMatrices.Count == 0)", StringComparison.Ordinal);
            Assert.That(unregisterIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nullCheckIndex, Is.GreaterThan(unregisterIndex));

            int clearIndex = source.IndexOf("public override void ClearApplied(TileData data, UnityEngine.Terrain terrain)", resolutionIndex, StringComparison.Ordinal);
            Assert.That(clearIndex, Is.GreaterThan(resolutionIndex));
            string clearBlock = source.Substring(clearIndex);
            Assert.That(clearBlock.Contains("HectonRockManager manager = GlobalRegistry.RockManager;"), Is.True);
            Assert.That(clearBlock.Contains("HectonRockManager manager = HectonRockManager.Instance;"), Is.False);
        }

        [Test]
        public void HectonRockManagerRegisterChunk_FailsClosedForUnconfiguredLayers()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "HectonRockManager.cs");
            string source = File.ReadAllText(path);
            int registerIndex = source.IndexOf("public void RegisterChunk(int layerId, Vector2Int chunkCoord, Matrix4x4[] matrices)", StringComparison.Ordinal);
            int unregisterIndex = source.IndexOf("public void UnregisterChunk(Vector2Int chunkCoord)", registerIndex, StringComparison.Ordinal);
            Assert.That(registerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(unregisterIndex, Is.GreaterThan(registerIndex));
            string registerBlock = source.Substring(registerIndex, unregisterIndex - registerIndex);

            Assert.That(source.Contains("private bool _unknownLayerLogged;"), Is.True);
            Assert.That(source.Contains("private void LogUnknownRockLayer(int layerId)"), Is.True);
            Assert.That(registerBlock.Contains("_prototypeLookup == null"), Is.True);
            Assert.That(registerBlock.Contains("!_prototypeLookup.ContainsKey(layerId)"), Is.True);
            Assert.That(registerBlock.Contains("LogUnknownRockLayer(layerId);"), Is.True);
            Assert.That(registerBlock.Contains("new Dictionary<Vector2Int, Matrix4x4[]>(64)"), Is.False);
            Assert.That(registerBlock.Contains("new Matrix4x4[_instanceCapacity]"), Is.False);
            Assert.That(registerBlock.Contains("LogMissingLayerBuffer(layerId);"), Is.True);
        }

        [Test]
        public void WorldProceduralScatterDirector_RockManagerDependencyUsesOwnerLocalInstance()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs");
            string source = File.ReadAllText(path);
            int resolveIndex = source.IndexOf("private void ResolveReferences()", StringComparison.Ordinal);
            int nextIndex = source.IndexOf("public void OnGlobalRegistryServiceReplaced", resolveIndex, StringComparison.Ordinal);
            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextIndex, Is.GreaterThan(resolveIndex));

            string resolveBlock = source.Substring(resolveIndex, nextIndex - resolveIndex);
            Assert.That(resolveBlock.Contains("HectonRockManager rockManager = HectonRockManager.Instance;"), Is.True);
            Assert.That(resolveBlock.Contains("HectonRockManager rockManager = GlobalRegistry.RockManager;"), Is.False);
        }

        [Test]
        public void VegetationTileMaskBuild_ResolvesAlphamapPixelDataOncePerLayer()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("private struct TerrainLayerMaskSampler"), Is.True);
            Assert.That(source.Contains("TerrainLayerMaskSampler sandSampler = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Sand);"), Is.True);
            Assert.That(source.Contains("SampleTerrainLayerMask(in sandSampler, writeIndex)"), Is.True);

            int sampleIndex = source.IndexOf("private static float SampleTerrainLayerMask(in TerrainLayerMaskSampler sampler", StringComparison.Ordinal);
            int signatureIndex = source.IndexOf("private static void CaptureTileCacheSignature", sampleIndex, StringComparison.Ordinal);
            Assert.That(sampleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(signatureIndex, Is.GreaterThan(sampleIndex));

            string sampleBlock = source.Substring(sampleIndex, signatureIndex - sampleIndex);
            Assert.That(sampleBlock.Contains("GetPixelData<Color32>"), Is.False);
        }

        [Test]
        public void VoxelDensityPipeline_SanitizesNonFiniteDensityBeforeMeshStages()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("ScratchLaneDensityFaultFlags"), Is.True);
            Assert.That(source.Contains("densityFaultFlags[0] = 1;"), Is.True);
            Assert.That(source.Contains("densityFaultFlags = densityFaultFlags"), Is.True);
            Assert.That(source.Contains("ReportVoxelInvalidDensityField();"), Is.True);
            Assert.That(source.Contains("finalDensityValue = math.select(0f, finalDensityValue, math.isfinite(finalDensityValue));"), Is.True);
            Assert.That(source.Contains("smoothDensityValue = math.select(0f, smoothDensityValue, math.isfinite(smoothDensityValue));"), Is.True);
            Assert.That(source.Contains("source = math.select(0f, source, math.isfinite(source));"), Is.True);
        }

        [Test]
        public void TerrainChunkPager_DoesNotDefaultProductionLoadsToMockPayloads()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerRuntime.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("private bool forceMockDiskIo = false;"), Is.True);
            Assert.That(source.Contains("private bool ResolveForceMockDiskIo()"), Is.True);
            Assert.That(source.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD"), Is.True);
            Assert.That(source.Contains("return false;"), Is.True);

            string constantsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerTypes.cs");
            string constantsSource = File.ReadAllText(constantsPath);
            Assert.That(constantsSource.Contains("public const uint RequestFlagsMask = RequestFlagForceMock;"), Is.True);
            Assert.That(constantsSource.Contains("tuning.Flags &= TerrainChunkPagerConstants.RequestFlagsMask;"), Is.True);
            int createDefaultIndex = constantsSource.IndexOf("public static TerrainChunkPagerTuningDTO CreateDefault()", StringComparison.Ordinal);
            Assert.That(createDefaultIndex, Is.GreaterThanOrEqualTo(0));
            string createDefaultBlock = constantsSource.Substring(createDefaultIndex);
            Assert.That(createDefaultBlock.Contains("tuning.Flags = 0u;"), Is.True);
            Assert.That(createDefaultBlock.Contains("tuning.Flags = TerrainChunkPagerConstants.RequestFlagMock;"), Is.False);
        }

        [Test]
        public void TerrainChunkPager_WorkerLifecycleFailsClosedAndCleansDeferredShutdown()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerRuntime.cs");
            string source = File.ReadAllText(path);

            string initializeBlock = ExtractSourceBlock(source, "private void Initialize()", "private void Shutdown()");
            Assert.That(initializeBlock.Contains("if (!StartWorker())"), Is.True);
            Assert.That(initializeBlock.Contains("ReleaseNativeState();"), Is.True);
            Assert.That(initializeBlock.IndexOf("if (!StartWorker())", StringComparison.Ordinal), Is.LessThan(initializeBlock.IndexOf("RegisterDispatcher();", StringComparison.Ordinal)));

            string deferredShutdownBlock = ExtractSourceBlock(source, "private void TryReleaseDeferredShutdownState()", "private void RegisterDispatcher()");
            Assert.That(deferredShutdownBlock.Contains("if (!StopWorker())"), Is.True);
            Assert.That(deferredShutdownBlock.IndexOf("if (!StopWorker())", StringComparison.Ordinal), Is.LessThan(deferredShutdownBlock.IndexOf("ReleaseNativeState();", StringComparison.Ordinal)));

            string rebindBlock = ExtractSourceBlock(source, "private void RebindDataVaultForLifecycle(", "private void AllocateNativeState()");
            Assert.That(rebindBlock.Contains("if (!StartWorker())"), Is.True);
            Assert.That(rebindBlock.Contains("UnregisterDispatcher(keepVisualSyncForDeferredShutdown: false);"), Is.True);
            Assert.That(rebindBlock.Contains("ReleaseNativeState();"), Is.True);
            Assert.That(rebindBlock.Contains("_initialized = 0;"), Is.True);

            string startWorkerBlock = ExtractSourceBlock(source, "private bool StartWorker()", "private bool StopWorker()");
            Assert.That(startWorkerBlock.Contains("catch (Exception)"), Is.True);
            Assert.That(startWorkerBlock.Contains("_faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;"), Is.True);
            Assert.That(startWorkerBlock.Contains("Volatile.Write(ref _workerRunning, 0);"), Is.True);
            Assert.That(startWorkerBlock.Contains("Volatile.Write(ref _workerThreadActive, 0);"), Is.True);
            Assert.That(startWorkerBlock.Contains("Volatile.Write(ref _workerHeartbeatTimestamp, 0L);"), Is.True);
            Assert.That(startWorkerBlock.Contains("_workerWake = wake;"), Is.True);
            Assert.That(startWorkerBlock.Contains("_workerThread = thread;"), Is.True);
            Assert.That(startWorkerBlock.Contains("thread.Start();"), Is.True);
            Assert.That(startWorkerBlock.Contains("return false;"), Is.True);

            string stopWorkerBlock = ExtractSourceBlock(source, "private bool StopWorker()", "private static bool TryJoinWorkerNoThrow");
            Assert.That(stopWorkerBlock.Contains("SignalWorkerWakeNoThrow(wake);"), Is.True);
            Assert.That(stopWorkerBlock.Contains("TryJoinWorkerNoThrow(thread)"), Is.True);
            Assert.That(stopWorkerBlock.Contains("DisposeWorkerWakeNoThrow(_workerWake);"), Is.True);
            Assert.That(stopWorkerBlock.Contains("Volatile.Write(ref _workerHeartbeatTimestamp, 0L);"), Is.True);
            Assert.That(stopWorkerBlock.Contains("thread.Join(2000)"), Is.False);

            string joinBlock = ExtractSourceBlock(source, "private static bool TryJoinWorkerNoThrow", "private static void SignalWorkerWakeNoThrow");
            Assert.That(joinBlock.Contains("ReferenceEquals(Thread.CurrentThread, thread)"), Is.True);
            Assert.That(joinBlock.Contains("thread.Join(WorkerShutdownWaitMilliseconds);"), Is.True);
            Assert.That(joinBlock.Contains("return !thread.IsAlive;"), Is.True);
            Assert.That(joinBlock.Contains("catch (Exception)"), Is.True);
        }

        [Test]
        public void TerrainChunkPager_RejectsUnknownChunkHeaderVersionsAndFlags()
        {
            string root = Directory.GetCurrentDirectory();
            string constantsPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerTypes.cs");
            string constantsSource = File.ReadAllText(constantsPath);
            Assert.That(constantsSource.Contains("public const uint FileVersion = 1u;"), Is.True);
            Assert.That(constantsSource.Contains("public const uint FileFlagsMask = 0u;"), Is.True);

            string runtimePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerRuntime.cs");
            string runtimeSource = File.ReadAllText(runtimePath);
            int validateIndex = runtimeSource.IndexOf("private static bool TryValidateChunkHeader", StringComparison.Ordinal);
            Assert.That(validateIndex, Is.GreaterThanOrEqualTo(0));

            string validateBlock = runtimeSource.Substring(validateIndex);
            Assert.That(validateBlock.Contains("header.Version != TerrainChunkPagerConstants.FileVersion"), Is.True);
            Assert.That(validateBlock.Contains("(header.Flags & ~TerrainChunkPagerConstants.FileFlagsMask) != 0u"), Is.True);
            Assert.That(validateBlock.Contains("header.Version == 0u"), Is.False);
        }

        [Test]
        public void TerrainStreamingTelemetryDumpPaths_AreOwnerSpecific()
        {
            string root = Directory.GetCurrentDirectory();
            string pagerPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "TerrainChunkPagerRuntime.cs");
            string pagerSource = File.ReadAllText(pagerPath);
            Assert.That(pagerSource, Does.Contain("Dump_1305_TerrainChunkPager.bin"));
            Assert.That(pagerSource, Does.Contain("private const int DumpHeaderBytes = 32;"));
            Assert.That(pagerSource, Does.Contain("private const uint DumpLayoutHash = 0x44504354u;"));
            Assert.That(pagerSource, Does.Contain("NativeFaultDumpWriter.CreateTransientPayload("));
            Assert.That(pagerSource, Does.Contain("WriteUInt32(header, 24, DumpLayoutHash);"));
            Assert.That(pagerSource, Does.Contain("WriteUInt32(header, 28, 0u);"));
            Assert.That(pagerSource, Does.Contain("NativeFaultDumpWriter.DisposeTransientPayload("));
            Assert.That(pagerSource, Does.Not.Contain("Dump_1305_Streaming.bin"));

            string residencyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");
            string residencySource = File.ReadAllText(residencyPath);
            Assert.That(residencySource, Does.Contain("Dump_1305_WorldChunkResidency.bin"));
            Assert.That(residencySource, Does.Contain("Dump_1305_WorldChunkResidency_Backpressure.bin"));
            Assert.That(residencySource, Does.Contain("Dump_1305_WorldChunkResidency_HLOD.bin"));
            Assert.That(residencySource, Does.Contain("private const int WorldChunkResidencyDumpHeaderBytes = 32;"));
            Assert.That(residencySource, Does.Contain("private const uint WorldChunkResidencyDumpLayoutHash = 0x44524357u;"));
            Assert.That(residencySource, Does.Contain("DumpTelemetryToPath(DumpRelativePath, telemetryRing, reasonFlags);"));
            Assert.That(residencySource, Does.Contain("WriteUInt64LittleEndian(payload, ref cursor, HectonDumpMagic);"));
            Assert.That(residencySource, Does.Contain("WriteUInt32LittleEndian(payload, ref cursor, WorldChunkResidencyDumpVersion);"));
            Assert.That(residencySource, Does.Contain("WriteUInt32LittleEndian(payload, ref cursor, WorldChunkResidencyDumpLayoutHash);"));
            Assert.That(residencySource, Does.Not.Contain("Dump_1305_Streaming.bin"));
        }

        [Test]
        public void BiomeTransitionReadAccessors_UseActiveRuntimeCachedHandles()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "Biomes", "BiomeTransitionManagerRuntime.cs");
            string source = File.ReadAllText(path);

            int snapshotIndex = source.IndexOf("public static bool TryReadSnapshot", StringComparison.Ordinal);
            int tuningIndex = source.IndexOf("public static bool TryReadTuning", snapshotIndex, StringComparison.Ordinal);
            int selfAuditIndex = source.IndexOf("public static bool TryRunSelfAudit", tuningIndex, StringComparison.Ordinal);
            Assert.That(snapshotIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(tuningIndex, Is.GreaterThan(snapshotIndex));
            Assert.That(selfAuditIndex, Is.GreaterThan(tuningIndex));

            string snapshotBlock = source.Substring(snapshotIndex, tuningIndex - snapshotIndex);
            string tuningBlock = source.Substring(tuningIndex, selfAuditIndex - tuningIndex);
            Assert.That(snapshotBlock.Contains("GlobalRegistry.DataVault"), Is.False);
            Assert.That(tuningBlock.Contains("GlobalRegistry.DataVault"), Is.False);
            Assert.That(snapshotBlock.Contains("BiomeTransitionManagerRuntime active = ActiveRuntimeInstance;"), Is.True);
            Assert.That(tuningBlock.Contains("BiomeTransitionManagerRuntime active = ActiveRuntimeInstance;"), Is.True);
            Assert.That(snapshotBlock.Contains("TryReadBiomeVaultBuffer(active._vault, in active._currentAtmosphereHandle"), Is.True);
            Assert.That(snapshotBlock.Contains("TryReadBiomeVaultBuffer(active._vault, in active._blendMaskHandle"), Is.True);
            Assert.That(snapshotBlock.Contains("TryReadBiomeVaultBuffer(active._vault, in active._countersHandle"), Is.True);
            Assert.That(tuningBlock.Contains("TryReadBiomeVaultBuffer(active._vault, in active._tuningHandle"), Is.True);
            Assert.That(snapshotBlock.Contains("TryReadExistingBiomeVaultBuffer"), Is.False);
            Assert.That(tuningBlock.Contains("TryReadExistingBiomeVaultBuffer"), Is.False);
        }

        [Test]
        public void TerrainHoleMaskBuild_IsScheduledAndFinalizedOutsideSlowTick()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "VegetationTerrainHoleSynchronizer.cs");
            string source = File.ReadAllText(path);

            int scheduleIndex = source.IndexOf("private void TryScheduleTerrainHoleJobs()", StringComparison.Ordinal);
            int finalizeIndex = source.IndexOf("private void FinalizeCompletedTerrainHoleJobs()", scheduleIndex, StringComparison.Ordinal);
            int applyIndex = source.IndexOf("private static void ApplyTerrainHoleMask", finalizeIndex, StringComparison.Ordinal);
            Assert.That(scheduleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(finalizeIndex, Is.GreaterThan(scheduleIndex));
            Assert.That(applyIndex, Is.GreaterThan(finalizeIndex));

            string scheduleBlock = source.Substring(scheduleIndex, finalizeIndex - scheduleIndex);
            string finalizeBlock = source.Substring(finalizeIndex, applyIndex - finalizeIndex);
            Assert.That(scheduleBlock.Contains(".Run(state.TerrainHoleMaskCount)"), Is.False);
            Assert.That(scheduleBlock.Contains(".Schedule(state.TerrainHoleMaskCount, TerrainHoleJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("TerrainHoleTileScheduleBudgetPerSlowTick"), Is.True);
            Assert.That(scheduleBlock.Contains("state.TerrainHolesJobScheduled = true;"), Is.True);
            Assert.That(scheduleBlock.Contains("ApplyTerrainHoleMask(state"), Is.False);
            Assert.That(finalizeBlock.Contains("DispatcherJobSwap.TryComplete(ref state.TerrainHolesJobHandle, forceComplete: false);"), Is.True);
            Assert.That(finalizeBlock.Contains("ApplyTerrainHoleMask(state, state.TerrainHoleMaskJobOutput);"), Is.True);
            Assert.That(finalizeBlock.Contains("ReleaseTerrainHoleJobResources(state);"), Is.True);

            string bridgePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            Assert.That(bridge.Contains("private const int TerrainHoleTileScheduleBudgetPerSlowTick = 1;"), Is.True);
            Assert.That(bridge.Contains("public NativeArray<TerrainHoleRecord> TerrainHoleRecordsJobSnapshot;"), Is.True);
            Assert.That(bridge.Contains("public NativeArray<byte> TerrainHoleMaskJobOutput;"), Is.True);
            Assert.That(bridge.Contains("public IDataVault TerrainHoleMaskJobVault;"), Is.True);
        }

        [Test]
        public void VegetationChunkBuild_IsScheduledAndFinalizedWithoutSlowTickRun()
        {
            string root = Directory.GetCurrentDirectory();
            string residencyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationChunkResidencyDirector.cs");
            string residency = File.ReadAllText(residencyPath);

            int scheduleIndex = residency.IndexOf("private bool ScheduleChunkBuild", StringComparison.Ordinal);
            int finalizeIndex = residency.IndexOf("private int FinalizeCompletedChunkBuilds()", scheduleIndex, StringComparison.Ordinal);
            int isCurrentIndex = residency.IndexOf("private bool IsJobStateCurrent", finalizeIndex, StringComparison.Ordinal);
            Assert.That(scheduleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(finalizeIndex, Is.GreaterThan(scheduleIndex));
            Assert.That(isCurrentIndex, Is.GreaterThan(finalizeIndex));

            string scheduleBlock = residency.Substring(scheduleIndex, finalizeIndex - scheduleIndex);
            string finalizeBlock = residency.Substring(finalizeIndex, isCurrentIndex - finalizeIndex);
            int slotAcquireIndex = scheduleBlock.IndexOf("if (!IsJobStateCurrent(jobState) || !TryAcquireChunkBuildJobSlot(out jobSlot))", StringComparison.Ordinal);
            int firstScheduleCallIndex = scheduleBlock.IndexOf("grassJob.Schedule(grassRecords.Length, DefaultJobBatchSize)", StringComparison.Ordinal);
            Assert.That(scheduleBlock.Contains("grassJob.Run"), Is.False);
            Assert.That(scheduleBlock.Contains("kelpJob.Run"), Is.False);
            Assert.That(scheduleBlock.Contains("floatingJob.Run"), Is.False);
            Assert.That(slotAcquireIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstScheduleCallIndex, Is.GreaterThan(slotAcquireIndex));
            Assert.That(scheduleBlock.Contains("TryAcquireChunkBuildJobSlot(out int jobSlot)"), Is.False);
            Assert.That(scheduleBlock.Contains("grassJob.Schedule(grassRecords.Length, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("kelpJob.Schedule(kelpRecords.Length, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("floatingJob.Schedule(floatingRecords.Length, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("TryCreateTerrainHoleJobSnapshot(out NativeArray<TerrainHoleRecord> terrainHoles)"), Is.True);
            Assert.That(scheduleBlock.Contains("TryPrepareArtificialStructureJobSnapshot(out NativeArray<ArtificialStructureRecord> artificialStructures)"), Is.True);
            Assert.That(scheduleBlock.Contains("NativeArray<byte> threatEchoFlags = default;"), Is.True);
            Assert.That(scheduleBlock.Contains("NativeArray<byte>.Copy(echoFlags, threatEchoFlags, _ecosystemThreatGridCellCount)"), Is.True);
            Assert.That(scheduleBlock.Contains("ThreatEchoFlags = threatEchoFlags"), Is.True);
            Assert.That(scheduleBlock.Contains("_chunkBuildJobs[jobSlot] = new ChunkBuildPendingJob"), Is.True);

            Assert.That(finalizeBlock.Contains("!_chunkBuildJobs[i].Handle.IsCompleted"), Is.True);
            Assert.That(finalizeBlock.Contains("pending.Handle.Complete();"), Is.True);
            Assert.That(finalizeBlock.Contains("BuildChunkPayloadFromJob("), Is.True);
            Assert.That(finalizeBlock.Contains("pending.GrassRecords"), Is.True);
            Assert.That(finalizeBlock.Contains("pending.FloatingRecords"), Is.True);
            Assert.That(finalizeBlock.Contains("pending.KelpRecords"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            Assert.That(bridge.Contains("private struct ChunkBuildPendingJob"), Is.True);
            Assert.That(bridge.Contains("private readonly ChunkBuildPendingJob[] _chunkBuildJobs"), Is.True);
            Assert.That(bridge.Contains("public NativeArray<JobInstanceRecord> GrassRecords;"), Is.False);
            Assert.That(bridge.Contains("NativeArray<JobInstanceRecord> GrassRecords;"), Is.True);
            Assert.That(bridge.Contains("NativeArray<byte> ThreatEchoFlags;"), Is.True);
            Assert.That(bridge.Contains("NativeArray<TerrainHoleRecord> TerrainHoles;"), Is.True);
            Assert.That(bridge.Contains("private bool HasChunkBuildJobsForTile(int tileX, int tileZ)"), Is.True);
            Assert.That(bridge.Contains("private void CompleteAndReleaseChunkBuildJobsForTile(int tileX, int tileZ)"), Is.True);
            Assert.That(bridge.Contains("ReleaseJobRecordArray(ref pending.GrassRecords);"), Is.True);
            Assert.That(bridge.Contains("H8Memory.Release(ref pending.ThreatEchoFlags"), Is.True);
            Assert.That(bridge.Contains("H8Memory.Release(ref pending.ArtificialStructures"), Is.True);

            string tileResidencyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationTileCacheResidency.cs");
            string tileResidency = File.ReadAllText(tileResidencyPath);
            Assert.That(tileResidency.Contains("HasChunkBuildJobsForTile(state.TileX, state.TileZ)"), Is.True);
            Assert.That(tileResidency.Contains("CompleteAndReleaseChunkBuildJobsForTile(state.TileX, state.TileZ);"), Is.True);

            string holeSyncPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationTerrainHoleSynchronizer.cs");
            string holeSync = File.ReadAllText(holeSyncPath);
            Assert.That(holeSync.Contains("CompleteAndReleaseChunkBuildJobsForTile(state.TileX, state.TileZ);"), Is.True);
        }

        [Test]
        public void VegetationFlowFieldBuild_IsScheduledAndPublishedFromLateFrameCompletion()
        {
            string root = Directory.GetCurrentDirectory();
            string flowPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationFlowFieldIntegrator.cs");
            string flow = File.ReadAllText(flowPath);

            int completeIndex = flow.IndexOf("private void CompleteFlowFieldJob(bool forceComplete)", StringComparison.Ordinal);
            int completeEndIndex = flow.IndexOf("private void CompleteThermalGridJob(bool forceComplete)", completeIndex, StringComparison.Ordinal);
            int scheduleIndex = flow.IndexOf("private void ScheduleFlowFieldJob()", StringComparison.Ordinal);
            int scheduleEndIndex = flow.IndexOf("private void ScheduleThermalGridJob()", scheduleIndex, StringComparison.Ordinal);
            Assert.That(completeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(completeEndIndex, Is.GreaterThan(completeIndex));
            Assert.That(scheduleIndex, Is.GreaterThan(completeEndIndex));
            Assert.That(scheduleEndIndex, Is.GreaterThan(scheduleIndex));

            string completeBlock = flow.Substring(completeIndex, completeEndIndex - completeIndex);
            string scheduleBlock = flow.Substring(scheduleIndex, scheduleEndIndex - scheduleIndex);
            Assert.That(scheduleBlock.Contains("job.Run(_ecosystemThreatGridCellCount)"), Is.False);
            Assert.That(scheduleBlock.Contains("job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("_flowFieldJob = new FlowFieldPendingJob"), Is.True);
            Assert.That(scheduleBlock.Contains("FlowOutput = flowOutput"), Is.True);
            Assert.That(scheduleBlock.Contains("_flowFieldScheduled = true;"), Is.True);
            Assert.That(scheduleBlock.Contains("TryPublishVegetationMemorySnapshot"), Is.False);
            Assert.That(completeBlock.Contains("if (!forceComplete && !_flowFieldJob.Handle.IsCompleted)"), Is.True);
            Assert.That(completeBlock.Contains("pending.Handle.Complete();"), Is.True);
            Assert.That(completeBlock.Contains("TryCopyVegetationMemorySnapshot("), Is.True);
            Assert.That(completeBlock.Contains("BufferID.VegetationEcosystemFlowField"), Is.True);
            Assert.That(completeBlock.Contains("ReleaseFlowFieldPendingJob(ref pending);"), Is.True);
            Assert.That(completeBlock.Contains("_flowFieldJob = default;"), Is.True);
            Assert.That(completeBlock.Contains("_flowFieldInitialized = true;"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            Assert.That(bridge.Contains("private struct FlowFieldPendingJob"), Is.True);
            Assert.That(bridge.Contains("private FlowFieldPendingJob _flowFieldJob;"), Is.True);
            Assert.That(flow.Contains("ReleaseFlowFieldPendingJob(ref pending);"), Is.True);
        }

        [Test]
        public void VegetationThermalGridBuild_IsScheduledAndComparesPreviousFlowVolume()
        {
            string root = Directory.GetCurrentDirectory();
            string flowPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationFlowFieldIntegrator.cs");
            string flow = File.ReadAllText(flowPath);

            int completeIndex = flow.IndexOf("private void CompleteThermalGridJob(bool forceComplete)", StringComparison.Ordinal);
            int scheduleIndex = flow.IndexOf("private void ScheduleThermalGridJob()", StringComparison.Ordinal);
            int disposeIndex = flow.IndexOf("private static void ReleaseThermalGridPendingJob", completeIndex, StringComparison.Ordinal);
            int nextIndex = flow.IndexOf("private WeatherRuntimeSnapshot ResolveWeatherSnapshot()", scheduleIndex, StringComparison.Ordinal);
            Assert.That(completeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(scheduleIndex, Is.GreaterThan(completeIndex));
            Assert.That(disposeIndex, Is.GreaterThan(completeIndex));
            Assert.That(disposeIndex, Is.LessThan(scheduleIndex));
            Assert.That(nextIndex, Is.GreaterThan(scheduleIndex));

            string completeBlock = flow.Substring(completeIndex, disposeIndex - completeIndex);
            string scheduleBlock = flow.Substring(scheduleIndex, nextIndex - scheduleIndex);
            string disposeBlock = flow.Substring(disposeIndex, scheduleIndex - disposeIndex);
            Assert.That(scheduleBlock.Contains("job.Run(_abyssalThermalGridCellCount)"), Is.False);
            Assert.That(scheduleBlock.Contains("flowVolumeJob.Run(_abyssalThermalGridCellCount)"), Is.False);
            Assert.That(scheduleBlock.Contains("job.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("flowVolumeJob.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize, thermalHandle)"), Is.True);
            Assert.That(scheduleBlock.Contains("_thermalGridJob = new ThermalGridPendingJob"), Is.True);
            Assert.That(scheduleBlock.Contains("FlowVolumeOutput = flowVolumeOutput"), Is.True);
            Assert.That(scheduleBlock.Contains("TryPublishVegetationMemorySnapshot"), Is.False);

            Assert.That(completeBlock.Contains("if (!forceComplete && !_thermalGridJob.Handle.IsCompleted)"), Is.True);
            Assert.That(completeBlock.Contains("pending.PreviousFlowVolumeSnapshot"), Is.True);
            Assert.That(completeBlock.Contains("pending.FlowVolumeOutput,"), Is.True);
            Assert.That(completeBlock.Contains("currentFlowVolume,\r\n                                                 currentFlowVolume"), Is.False);
            Assert.That(completeBlock.Contains("BufferID.VegetationAbyssalThermalGrid"), Is.True);
            Assert.That(completeBlock.Contains("BufferID.VegetationAbyssalFlowVolume"), Is.True);
            Assert.That(completeBlock.Contains("ReleaseThermalGridPendingJob(ref pending);"), Is.True);
            Assert.That(disposeBlock.Contains("H8Memory.Release(ref pending.ThreatChunks"), Is.True);
            Assert.That(disposeBlock.Contains("H8Memory.Release(ref pending.FlowVolumeOutput"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            Assert.That(bridge.Contains("private struct ThermalGridPendingJob"), Is.True);
            Assert.That(bridge.Contains("private ThermalGridPendingJob _thermalGridJob;"), Is.True);
            Assert.That(flow.Contains("ReleaseThermalGridPendingJob(ref pending);"), Is.True);
        }

        [Test]
        public void VegetationThreatPropagationBuild_IsScheduledBeforeFlowAndPublishedFromCompletion()
        {
            string root = Directory.GetCurrentDirectory();
            string flowPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationFlowFieldIntegrator.cs");
            string flow = File.ReadAllText(flowPath);

            int completeIndex = flow.IndexOf("private void CompleteThreatPropagationJob(bool forceComplete)", StringComparison.Ordinal);
            int scheduleIndex = flow.IndexOf("private void ScheduleThreatPropagationJob()", StringComparison.Ordinal);
            int disposeIndex = flow.IndexOf("private static void ReleaseThreatPropagationPendingJob", completeIndex, StringComparison.Ordinal);
            int nextIndex = flow.IndexOf("private bool EnsureFlowFieldBuffers()", scheduleIndex, StringComparison.Ordinal);
            Assert.That(completeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(scheduleIndex, Is.GreaterThan(completeIndex));
            Assert.That(disposeIndex, Is.GreaterThan(completeIndex));
            Assert.That(disposeIndex, Is.LessThan(scheduleIndex));
            Assert.That(nextIndex, Is.GreaterThan(scheduleIndex));

            string completeBlock = flow.Substring(completeIndex, disposeIndex - completeIndex);
            string scheduleBlock = flow.Substring(scheduleIndex, nextIndex - scheduleIndex);
            string disposeBlock = flow.Substring(disposeIndex, scheduleIndex - disposeIndex);
            Assert.That(scheduleBlock.Contains("job.Run(_ecosystemThreatGridCellCount)"), Is.False);
            Assert.That(scheduleBlock.Contains("voxelJob.Run(_ecosystemThreatVoxelCellCount)"), Is.False);
            Assert.That(scheduleBlock.Contains("job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize)"), Is.True);
            Assert.That(scheduleBlock.Contains("voxelJob.Schedule(_ecosystemThreatVoxelCellCount, DefaultJobBatchSize, threatHandle)"), Is.True);
            Assert.That(scheduleBlock.Contains("_threatPropagationJob = new ThreatPropagationPendingJob"), Is.True);
            Assert.That(scheduleBlock.Contains("_threatPropagationScheduled = true;"), Is.True);
            Assert.That(scheduleBlock.Contains("TryPublishVegetationMemorySnapshot"), Is.False);

            Assert.That(completeBlock.Contains("if (!forceComplete && !_threatPropagationJob.Handle.IsCompleted)"), Is.True);
            Assert.That(completeBlock.Contains("pending.PreviousEchoFlags"), Is.True);
            Assert.That(completeBlock.Contains("InvalidateChunksForNewPermanentEchoes(pending.PreviousEchoFlags, pending.EchoOutput)"), Is.True);
            Assert.That(completeBlock.Contains("BufferID.VegetationEcosystemThreatGrid"), Is.True);
            Assert.That(completeBlock.Contains("BufferID.VegetationEcosystemThreatVoxel"), Is.True);
            Assert.That(completeBlock.Contains("BufferID.VegetationEcosystemThreatEcho"), Is.True);
            Assert.That(completeBlock.Contains("ReleaseThreatPropagationPendingJob(ref pending);"), Is.True);
            Assert.That(disposeBlock.Contains("H8Memory.Release(ref pending.ThreatChunks"), Is.True);
            Assert.That(disposeBlock.Contains("H8Memory.Release(ref pending.VoxelOutput"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            Assert.That(bridge.Contains("private struct ThreatPropagationPendingJob"), Is.True);
            Assert.That(bridge.Contains("private ThreatPropagationPendingJob _threatPropagationJob;"), Is.True);
            Assert.That(flow.Contains("ReleaseThreatPropagationPendingJob(ref pending);"), Is.True);
            Assert.That(flow.Contains("if (_threatPropagationScheduled || _flowFieldScheduled || _abyssalThermalGridScheduled)"), Is.True);
            Assert.That(flow.Contains("if (_abyssalThermalGridScheduled || _threatPropagationScheduled || _flowFieldScheduled)"), Is.True);
        }

        [Test]
        public void VegetationNativePoolDefrag_IsRuntimeDormantAndDoesNotRunPoolCopy()
        {
            string root = Directory.GetCurrentDirectory();
            string poolPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VegetationMemoryPool.cs");
            string pool = File.ReadAllText(poolPath);

            Assert.That(pool.Contains("private static bool RuntimeNativePoolDefragEnabled => false;"), Is.True);

            int tryScheduleIndex = pool.IndexOf("private void TryScheduleNativePoolDefrag()", StringComparison.Ordinal);
            int completeIndex = pool.IndexOf("private void CompleteNativePoolDefragIfReady", tryScheduleIndex, StringComparison.Ordinal);
            int scheduleIndex = pool.IndexOf("private JobHandle SchedulePoolDefrag", completeIndex, StringComparison.Ordinal);
            int applyIndex = pool.IndexOf("private void ApplyPoolDefragOffsets", scheduleIndex, StringComparison.Ordinal);
            Assert.That(tryScheduleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(completeIndex, Is.GreaterThan(tryScheduleIndex));
            Assert.That(scheduleIndex, Is.GreaterThan(completeIndex));
            Assert.That(applyIndex, Is.GreaterThan(scheduleIndex));

            string tryScheduleBlock = pool.Substring(tryScheduleIndex, completeIndex - tryScheduleIndex);
            string scheduleBlock = pool.Substring(scheduleIndex, applyIndex - scheduleIndex);
            Assert.That(tryScheduleBlock.Contains("if (!RuntimeNativePoolDefragEnabled)"), Is.True);
            Assert.That(tryScheduleBlock.Contains("_idleNativePoolTimer = 0f;"), Is.True);
            Assert.That(tryScheduleBlock.Contains("return;"), Is.True);
            Assert.That(tryScheduleBlock.Contains("BuildPoolDefragPlan"), Is.False);
            Assert.That(tryScheduleBlock.Contains("SchedulePoolDefrag"), Is.False);
            Assert.That(tryScheduleBlock.Contains("VegetationMemoryTelemetryCode.DefragScheduled"), Is.False);
            Assert.That(scheduleBlock.Contains(".Run()"), Is.False);
            Assert.That(scheduleBlock.Contains("TryAcquireChunkPoolWriteView"), Is.False);
            Assert.That(scheduleBlock.Contains("H8Memory.Release(ref moves"), Is.True);

            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);
            int initializeIndex = bridge.IndexOf("private void InitializeChunkPools()", StringComparison.Ordinal);
            int disposeIndex = bridge.IndexOf("private void DisposeChunkPools()", initializeIndex, StringComparison.Ordinal);
            Assert.That(initializeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(disposeIndex, Is.GreaterThan(initializeIndex));
            string initializeBlock = bridge.Substring(initializeIndex, disposeIndex - initializeIndex);
            Assert.That(initializeBlock.Contains("EnsurePoolDefragStagingCapacity"), Is.False);
        }

        [Test]
        public void VoxelActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("static HectonVoxelEngine s_activeRuntimeInstance;"), Is.True);
            Assert.That(source.Contains("internal static HectonVoxelEngine ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(source.Contains("internal static HectonVoxelEngine ActiveRuntimeInstance => GlobalRegistry.VoxelEngine;"), Is.False);
            Assert.That(source.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(source.Contains("s_activeRuntimeInstance = null;"), Is.True);
            Assert.That(source.Contains("if (ReferenceEquals(GlobalRegistry.VoxelEngine, this))"), Is.True);

            string wreckPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "ProceduralWreckGenerator.cs");
            string wreck = File.ReadAllText(wreckPath);
            Assert.That(wreck.Contains("WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref _voxelEngine);"), Is.True);
            Assert.That(wreck.Contains("_voxelEngine = GlobalRegistry.VoxelEngine;"), Is.False);
        }

        [Test]
        public void VoxelEngineRuntimePoolAndLodRoutes_DoNotPollRegistryInActivePaths()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("IVoxelSonarSdfReadLeaseModel, IGlobalRegistryHotSwapListener"), Is.True);
            Assert.That(source.Contains("IObjectPoolService _objectPoolService;"), Is.True);
            Assert.That(source.Contains("IPhysicsService _physicsService;"), Is.True);
            Assert.That(source.Contains("IVramPressureReadModel _vramPressureReadModel;"), Is.True);
            Assert.That(source.Contains("bool _registeredRuntimeServiceHotSwapListener;"), Is.True);
            Assert.That(source.Contains("CacheRuntimeServicesCold();"), Is.True);
            Assert.That(source.Contains("TryRegisterRuntimeServiceHotSwapListener();"), Is.True);
            Assert.That(source.Contains("if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)"), Is.True);
            Assert.That(source.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Physics)"), Is.True);
            Assert.That(source.Contains("if (serviceSlot == GlobalRegistryServiceSlot.VRAMPressureRuntime)"), Is.True);
            Assert.That(source.Contains("void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(source.Contains("bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(source.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(source.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(source.Contains("_objectPoolService = currentService as IObjectPoolService;"), Is.False);
            Assert.That(source.Contains("_objectPoolService = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(source.Contains("_physicsService = currentService as IPhysicsService;"), Is.True);
            Assert.That(source.Contains("_vramPressureReadModel = currentService as IVramPressureReadModel;"), Is.True);

            int despawnIndex = source.IndexOf("public void DespawnVolume(GameObject volume)", StringComparison.Ordinal);
            int purgeIndex = source.IndexOf("public void PurgeNullVolumes()", despawnIndex, StringComparison.Ordinal);
            Assert.That(despawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(purgeIndex, Is.GreaterThan(despawnIndex));
            string despawnBlock = source.Substring(despawnIndex, purgeIndex - despawnIndex);
            Assert.That(despawnBlock.Contains("TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(despawnBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);
            Assert.That(despawnBlock.Contains("GlobalRegistry.ObjectPoolService"), Is.False);

            int clearIndex = source.IndexOf("public void ClearAllVolumes()", StringComparison.Ordinal);
            int activeCountIndex = source.IndexOf("public int ActiveVolumeCount", clearIndex, StringComparison.Ordinal);
            Assert.That(clearIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(activeCountIndex, Is.GreaterThan(clearIndex));
            string clearBlock = source.Substring(clearIndex, activeCountIndex - clearIndex);
            Assert.That(clearBlock.Contains("TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(clearBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);
            Assert.That(clearBlock.Contains("GlobalRegistry.ObjectPoolService"), Is.False);

            int spawnIndex = source.IndexOf("GameObject SpawnVolume()", StringComparison.Ordinal);
            int weldedMeshIndex = source.IndexOf("Mesh BuildWeldedMeshNative", spawnIndex, StringComparison.Ordinal);
            Assert.That(spawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(weldedMeshIndex, Is.GreaterThan(spawnIndex));
            string spawnBlock = source.Substring(spawnIndex, weldedMeshIndex - spawnIndex);
            Assert.That(spawnBlock.Contains("TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(spawnBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);
            Assert.That(spawnBlock.Contains("GlobalRegistry.ObjectPoolService"), Is.False);

            int budgetIndex = source.IndexOf("static void RecordVoxelRebuildBudget", StringComparison.Ordinal);
            int spatialIndex = source.IndexOf("bool BuildNodeSpatialBuckets", budgetIndex, StringComparison.Ordinal);
            Assert.That(budgetIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(spatialIndex, Is.GreaterThan(budgetIndex));
            string budgetBlock = source.Substring(budgetIndex, spatialIndex - budgetIndex);
            Assert.That(budgetBlock.Contains("LODSystemManager lodSystem = LODSystemManager.Instance;"), Is.True);
            Assert.That(budgetBlock.Contains("GlobalRegistry.LODSystem"), Is.False);

            int dampenerIndex = source.IndexOf("private static void ApplyPredictiveVoxelProxyDampener", StringComparison.Ordinal);
            int deferredIndex = source.IndexOf("private static void EnqueueDeferredVoxelPhysicsBakeTeardown", dampenerIndex, StringComparison.Ordinal);
            Assert.That(dampenerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(deferredIndex, Is.GreaterThan(dampenerIndex));
            string dampenerBlock = source.Substring(dampenerIndex, deferredIndex - dampenerIndex);
            Assert.That(dampenerBlock.Contains("IPhysicsService physics = activeEngine != null ? activeEngine._physicsService : null;"), Is.True);
            Assert.That(dampenerBlock.Contains("GlobalRegistry.Physics"), Is.False);

            int colliderFakeIndex = source.IndexOf("private static bool ShouldUseCinematicColliderFake", StringComparison.Ordinal);
            int playerAupIndex = source.IndexOf("private static bool TryResolvePlayerAup", colliderFakeIndex, StringComparison.Ordinal);
            Assert.That(colliderFakeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(playerAupIndex, Is.GreaterThan(colliderFakeIndex));
            string colliderFakeBlock = source.Substring(colliderFakeIndex, playerAupIndex - colliderFakeIndex);
            Assert.That(colliderFakeBlock.Contains("IVramPressureReadModel pressureMonitor = activeEngine != null ? activeEngine._vramPressureReadModel : null;"), Is.True);
            Assert.That(colliderFakeBlock.Contains("GlobalRegistry.VRAMPressureReadModel"), Is.False);
        }

        [Test]
        public void GroundRadarOreAndVoxelDependencies_UseOwnerLocalRuntimeRoutes()
        {
            string root = Directory.GetCurrentDirectory();
            string orePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "Resources", "ProceduralOreSpawner.cs");
            string ore = File.ReadAllText(orePath);
            Assert.That(ore.Contains("private static ProceduralOreSpawner s_activeRuntimeInstance;"), Is.True);
            Assert.That(ore.Contains("internal static ProceduralOreSpawner ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(ore.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(ore.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(ore.Contains("ReferenceEquals(GlobalRegistry.WorldResourceSpawner, this)"), Is.True);

            string utilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string utility = File.ReadAllText(utilityPath);
            int resolverIndex = utility.IndexOf("public static bool TryResolveWorldResourceSpawnerReadModel", StringComparison.Ordinal);
            int mapMagicIndex = utility.IndexOf("public static bool TryResolveMapMagicBridge", resolverIndex, StringComparison.Ordinal);
            Assert.That(resolverIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(mapMagicIndex, Is.GreaterThan(resolverIndex));
            string resolverBlock = utility.Substring(resolverIndex, mapMagicIndex - resolverIndex);
            Assert.That(resolverBlock.Contains("ProceduralOreSpawner.ActiveRuntimeInstance"), Is.True);
            Assert.That(resolverBlock.Contains("GlobalRegistry.WorldResourceSpawner"), Is.False);

            string gprPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "GroundPenetratingRadarRuntime.cs");
            string gpr = File.ReadAllText(gprPath);
            Assert.That(gpr.Contains("CacheOreReadModelFromOwnerRoute();"), Is.True);
            Assert.That(gpr.Contains("WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel"), Is.True);
            Assert.That(gpr.Contains("if (!CacheOreReadModelFromOwnerRoute())"), Is.True);
            Assert.That(gpr.Contains("WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector)"), Is.True);
            Assert.That(gpr.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(gpr.Contains("_worldResourceSpawnerReadModel = GlobalRegistry.WorldResourceSpawner;"), Is.False);
            Assert.That(gpr.Contains("WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine)"), Is.True);

            int cacheIndex = gpr.IndexOf("private bool CacheOreReadModelFromOwnerRoute()", StringComparison.Ordinal);
            int frameIndex = gpr.IndexOf("private uint AdvanceRadarFrameId()", cacheIndex, StringComparison.Ordinal);
            Assert.That(cacheIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(frameIndex, Is.GreaterThan(cacheIndex));
            string cacheBlock = gpr.Substring(cacheIndex, frameIndex - cacheIndex);
            Assert.That(cacheBlock.Contains("if (_worldResourceSpawnerReadModel != null)"), Is.False);
            Assert.That(cacheBlock.Contains("_worldResourceSpawnerCommandModel = null;"), Is.True);

            int serviceSlotIndex = gpr.IndexOf("case GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime:", StringComparison.Ordinal);
            int nextSlotIndex = gpr.IndexOf("return;", serviceSlotIndex, StringComparison.Ordinal);
            Assert.That(serviceSlotIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextSlotIndex, Is.GreaterThan(serviceSlotIndex));
            string serviceSlotBlock = gpr.Substring(serviceSlotIndex, nextSlotIndex - serviceSlotIndex);
            Assert.That(serviceSlotBlock.Contains("WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel("), Is.True);
            Assert.That(serviceSlotBlock.Contains("_worldResourceSpawnerCommandModel = null;"), Is.True);
            Assert.That(gpr.Contains("_voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;"), Is.False);
            Assert.That(gpr.Contains("_ecosystemDirector = GlobalRegistry.EcosystemDirector;"), Is.False);
        }

        [Test]
        public void MapMagicVegetationActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string bridge = File.ReadAllText(bridgePath);

            Assert.That(bridge.Contains("private static HectonMapMagicVegetationBridge s_activeRuntimeInstance;"), Is.True);
            Assert.That(bridge.Contains("internal static HectonMapMagicVegetationBridge ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(bridge.Contains("internal static HectonMapMagicVegetationBridge ActiveRuntimeInstance => GlobalRegistry.MapMagicVegetation;"), Is.False);
            Assert.That(bridge.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(bridge.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(bridge.Contains("ReferenceEquals(GlobalRegistry.MapMagicVegetation, this)"), Is.True);

            string utilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldRuntimeReferenceUtility.cs");
            string utility = File.ReadAllText(utilityPath);
            Assert.That(utility.Contains("HectonMapMagicVegetationBridge.ActiveRuntimeInstance"), Is.True);
            Assert.That(utility.Contains("HectonMapMagicVegetationBridge registered = GlobalRegistry.MapMagicVegetation;"), Is.False);
            Assert.That(utility.Contains("public static void InvalidateHectonMapMagicVegetationBridgeCache(HectonMapMagicVegetationBridge instance)"), Is.True);
            int resolveIndex = utility.IndexOf("public static bool TryResolveHectonMapMagicVegetationBridge", StringComparison.Ordinal);
            int invalidateIndex = utility.IndexOf("public static void InvalidateHectonMapMagicVegetationBridgeCache", resolveIndex, StringComparison.Ordinal);
            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(invalidateIndex, Is.GreaterThan(resolveIndex));
            string resolveBlock = utility.Substring(resolveIndex, invalidateIndex - resolveIndex);
            Assert.That(resolveBlock.Contains("TryResolveLiveCachedActiveRuntime(ref target, ref _CachedVegetationBridge, HectonMapMagicVegetationBridge.ActiveRuntimeInstance)"), Is.True);
            Assert.That(resolveBlock.Contains("if (IsLiveBehaviour(_CachedVegetationBridge))"), Is.False);
            Assert.That(resolveBlock.Contains("GlobalRegistry.MapMagicVegetation"), Is.False);
            Assert.That(bridge.Contains("WorldRuntimeReferenceUtility.InvalidateHectonMapMagicVegetationBridgeCache(null);"), Is.True);
            Assert.That(bridge.Contains("WorldRuntimeReferenceUtility.InvalidateHectonMapMagicVegetationBridgeCache(this);"), Is.True);

            string runtimeBridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string runtimeBridge = File.ReadAllText(runtimeBridgePath);
            Assert.That(runtimeBridge.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);"), Is.True);
            Assert.That(runtimeBridge.Contains("_cachedVegetationBridge = GlobalRegistry.MapMagicVegetation;"), Is.False);

            string microFaunaPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string microFauna = File.ReadAllText(microFaunaPath);
            Assert.That(microFauna.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);"), Is.True);
            Assert.That(microFauna.Contains("_mapMagicVegetationBridge = GlobalRegistry.MapMagicVegetation;"), Is.False);
            Assert.That(microFauna.Contains("WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector)"), Is.True);
            Assert.That(microFauna.Contains("WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref _proceduralScatterDirector)"), Is.True);
            Assert.That(microFauna.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);"), Is.True);
            Assert.That(microFauna.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(microFauna.Contains("_proceduralScatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(microFauna.Contains("_biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(microFauna.Contains("_ecosystemDirector = GlobalRegistry.EcosystemDirector;"), Is.False);
            Assert.That(microFauna.Contains("_proceduralScatterDirector = GlobalRegistry.ProceduralScatter;"), Is.False);
            Assert.That(microFauna.Contains("_biomeMatrixDirector = GlobalRegistry.BiomeMatrix;"), Is.False);

            string migrationPath = Path.Combine(root, "Assets", "_Project", "Scripts", "Ecosystem", "MigrationDirector.cs");
            string migration = File.ReadAllText(migrationPath);
            Assert.That(migration.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);"), Is.True);
            Assert.That(migration.Contains("_mapMagicVegetationBridge = GlobalRegistry.MapMagicVegetation;"), Is.False);

            string[] vegetationConsumerPaths =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "GPUScatterDirector.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumGlobalDragManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "SeamGapDitherRenderer.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Construction", "DroneFleetManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "HectonVoxelVolume.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "HectonDirectorAI.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "LocalizationManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "TetherManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VoxelDynamicNavGridRuntimeLifecycle.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Audio", "PlayerCriticalProceduralAudioRenderer.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Construction", "BaseModuleNavModifier.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "EncounterDirector.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "HectonPlayerSpawner.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "MountablePlayerTransport.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "SaveManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "SubmarineElectrolysisModule.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "SubmarineFluidDynamics.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "AcousticEcholocationTranslator.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "PDAIntrusionManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "PDADataLogTab.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AcousticOcclusionUtility.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCutManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "VFX", "HectonMarineSnowRenderer.cs")
            };

            foreach (string consumerPath in vegetationConsumerPaths)
            {
                string consumer = File.ReadAllText(consumerPath);
                Assert.That(consumer.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge"), Is.True, consumerPath);
                Assert.That(consumer.Contains("GlobalRegistry.MapMagicVegetation"), Is.False, consumerPath);
            }

            string floraInteraction = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs"));
            Assert.That(floraInteraction.Contains("_vegetationBridgeResolveRequested = _vegetationBridge == null;"), Is.True);

            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            foreach (string scriptPath in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (scriptPath.EndsWith(Path.Combine("World", "HectonMapMagicVegetationBridge.cs"), StringComparison.Ordinal))
                    continue;
                if (scriptPath.EndsWith("WorldRuntimeReferenceUtility.cs", StringComparison.Ordinal))
                    continue;

                string script = File.ReadAllText(scriptPath);
                Assert.That(script.Contains("GlobalRegistry.MapMagicVegetation"), Is.False, scriptPath);
                Assert.That(script.Contains("HectonMapMagicVegetationBridge.ActiveRuntimeInstance"), Is.False, scriptPath);
                AssertVegetationBridgeServiceCastsAreResolverValidated(script, scriptPath);
            }
        }

        [Test]
        public void ResourceDistributionActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string directorPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ResourceDistributionDirector.cs");
            string director = File.ReadAllText(directorPath);

            Assert.That(director.Contains("private static ResourceDistributionDirector s_activeRuntimeInstance;"), Is.True);
            Assert.That(director.Contains("internal static ResourceDistributionDirector ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(director.Contains("internal static ResourceDistributionDirector ActiveRuntimeInstance => GlobalRegistry.ResourceDistribution;"), Is.False);
            Assert.That(director.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(director.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(director.Contains("ReferenceEquals(GlobalRegistry.ResourceDistribution, this)"), Is.True);

            string smokePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldGenRegistrySmokeTester.cs");
            string smoke = File.ReadAllText(smokePath);
            int resourceIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.ResourceDistribution]", StringComparison.Ordinal);
            int terrainSeamIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.TerrainSeam]", resourceIndex, StringComparison.Ordinal);
            Assert.That(resourceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(terrainSeamIndex, Is.GreaterThan(resourceIndex));
            string resourceSmokeBlock = smoke.Substring(resourceIndex, terrainSeamIndex - resourceIndex);
            Assert.That(resourceSmokeBlock.Contains("Object.DestroyImmediate(resourceObject);"), Is.True);
            Assert.That(resourceSmokeBlock.Contains("GlobalRegistry.UnregisterResourceDistribution(resourceDistribution);"), Is.False);

            string voxelEnginePath = Path.Combine(root, "Assets", "_Project", "Scripts", "HectonVoxelEngine.cs");
            string voxelEngine = File.ReadAllText(voxelEnginePath);
            Assert.That(voxelEngine.Contains("ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(voxelEngine.Contains("ResourceDistributionDirector director = GlobalRegistry.ResourceDistribution;"), Is.False);

            string anomalyBindingPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonAnomalyResourceBinding.cs");
            string anomalyBinding = File.ReadAllText(anomalyBindingPath);
            Assert.That(anomalyBinding.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref director)"), Is.True);
            Assert.That(anomalyBinding.Contains("ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.False);
            Assert.That(anomalyBinding.Contains("ResourceDistributionDirector director = GlobalRegistry.ResourceDistribution;"), Is.False);
        }

        [Test]
        public void ResourceDistributionRuntimeRoutes_DoNotRepairWorldgenDependencies()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "ResourceDistributionDirector.cs");
            string source = File.ReadAllText(path);

            int slowTickIndex = source.IndexOf("public void SlowTick()", StringComparison.Ordinal);
            int lateFrameIndex = source.IndexOf("public void LateFrameTick()", slowTickIndex, StringComparison.Ordinal);
            int thermalFaceIndex = source.IndexOf("private Vector3 ResolveThermalDiamondVoxelFacePosition", StringComparison.Ordinal);
            int tickMeteorIndex = source.IndexOf("private void TickMeteorImpacts", thermalFaceIndex, StringComparison.Ordinal);
            int meteorCraterIndex = source.IndexOf("private bool TryApplyMeteorImpactCrater", StringComparison.Ordinal);
            int radiationIndex = source.IndexOf("private void RegisterMeteoriteRadiationHazard", meteorCraterIndex, StringComparison.Ordinal);

            Assert.That(slowTickIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(lateFrameIndex, Is.GreaterThan(slowTickIndex));
            Assert.That(thermalFaceIndex, Is.GreaterThan(lateFrameIndex));
            Assert.That(tickMeteorIndex, Is.GreaterThan(thermalFaceIndex));
            Assert.That(meteorCraterIndex, Is.GreaterThan(thermalFaceIndex));
            Assert.That(radiationIndex, Is.GreaterThan(meteorCraterIndex));

            string slowTickBlock = source.Substring(slowTickIndex, lateFrameIndex - slowTickIndex);
            string thermalFaceBlock = source.Substring(thermalFaceIndex, tickMeteorIndex - thermalFaceIndex);
            string meteorCraterBlock = source.Substring(meteorCraterIndex, radiationIndex - meteorCraterIndex);

            Assert.That(slowTickBlock.Contains("TryResolveRuntimeDependencies("), Is.False);
            Assert.That(slowTickBlock.Contains("WorldRuntimeReferenceUtility."), Is.False);
            Assert.That(thermalFaceBlock.Contains("WorldRuntimeReferenceUtility."), Is.False);
            Assert.That(meteorCraterBlock.Contains("WorldRuntimeReferenceUtility."), Is.False);

            Assert.That(source.Contains("private void CacheWorldgenRuntimeReferencesCold()"), Is.True);
            Assert.That(source.Contains("private bool HasRuntimeDependencies()"), Is.True);
            Assert.That(source.Contains("private bool TryResolveRuntimeDependencies()"), Is.False);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.MapMagicRuntime:"), Is.True);
            Assert.That(source.Contains("RebindMapMagicRuntime(previousService, currentService);"), Is.True);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:"), Is.True);
            Assert.That(source.Contains("RebindVegetationRuntime(previousService, currentService);"), Is.True);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.VoxelEngineRuntime:"), Is.True);
            Assert.That(source.Contains("RebindVoxelEngineRuntime(previousService, currentService);"), Is.True);
        }

        [Test]
        public void SpawnZoneSdfForensics_TracksFaultDumpPayload()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "SpawnZoneSdfValidation.cs");
            string source = File.ReadAllText(path);
            string dumpBlock = ExtractSourceBlock(
                source,
                "public static unsafe void WriteTelemetryDump(",
                "    public sealed partial class ResourceDistributionDirector");

            Assert.That(source.Contains("private const string DumpPayloadLabel = \"spawnZoneSdfTelemetryDumpPayload\";"), Is.True);
            Assert.That(dumpBlock.Contains("NativeFaultDumpWriter.CreateTransientPayload("), Is.True);
            Assert.That(dumpBlock.Contains("nameof(SpawnZoneSdfForensics)"), Is.True);
            Assert.That(dumpBlock.Contains("NativeArrayOptions.ClearMemory"), Is.True);
            Assert.That(dumpBlock.Contains("NativeFaultDumpWriter.DisposeTransientPayload("), Is.True);
            Assert.That(dumpBlock.Contains("new NativeArray<byte>(totalBytes"), Is.False);
        }

        [Test]
        public void SargassumRuntimeManagers_UseOwnerLocalPointersForWorldConsumers()
        {
            string root = Directory.GetCurrentDirectory();
            string dragPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumGlobalDragManager.cs");
            string drag = File.ReadAllText(dragPath);
            Assert.That(drag.Contains("private static SargassumGlobalDragManager s_activeRuntimeInstance;"), Is.True);
            Assert.That(drag.Contains("public static SargassumGlobalDragManager Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(drag.Contains("public static SargassumGlobalDragManager Instance => GlobalRegistry.SargassumDrag;"), Is.False);
            Assert.That(drag.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(drag.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(drag.Contains("_cutManager = SargassumCutManager.Instance;"), Is.True);
            Assert.That(drag.Contains("_cutManager = GlobalRegistry.SargassumCut;"), Is.False);
            Assert.That(drag.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(drag.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(drag.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(drag.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(drag.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(drag.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(drag.Contains("_objectPoolService = currentService as IObjectPoolService;"), Is.False);
            Assert.That(drag.Contains("_objectPoolService = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(drag.Contains("IObjectPoolService poolManager = _objectPoolService;"), Is.False);

            string cutPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCutManager.cs");
            string cut = File.ReadAllText(cutPath);
            Assert.That(cut.Contains("private static SargassumCutManager s_activeRuntimeInstance;"), Is.True);
            Assert.That(cut.Contains("public static SargassumCutManager Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(cut.Contains("public static SargassumCutManager Instance => GlobalRegistry.SargassumCut;"), Is.False);
            Assert.That(cut.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(cut.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);

            string[] worldConsumers =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCrestDampingController.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumDebrisParticleSystem.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumCollapseChunk.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AbyssalFluidDecalManager.cs")
            };

            for (int i = 0; i < worldConsumers.Length; i++)
            {
                string consumer = File.ReadAllText(worldConsumers[i]);
                Assert.That(consumer.Contains("GlobalRegistry.SargassumDrag"), Is.False, worldConsumers[i]);
                Assert.That(consumer.Contains("GlobalRegistry.SargassumCut"), Is.False, worldConsumers[i]);
            }
        }

        [Test]
        public void WorldLodManagers_UseOwnerLocalInstancesForRuntimeConsumers()
        {
            string root = Directory.GetCurrentDirectory();
            string cullingPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "CullingManager.cs");
            string culling = File.ReadAllText(cullingPath);
            Assert.That(culling.Contains("private static CullingManager s_activeRuntimeInstance;"), Is.True);
            Assert.That(culling.Contains("public static CullingManager Instance =>"), Is.False);
            Assert.That(culling.Contains("GlobalRegistry.RegisterCullingRuntime(this);"), Is.True);
            Assert.That(culling.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(culling.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);

            string impostorPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ImpostorSystem.cs");
            string impostor = File.ReadAllText(impostorPath);
            Assert.That(impostor.Contains("private static ImpostorSystem s_activeRuntimeInstance;"), Is.True);
            Assert.That(impostor.Contains("public static ImpostorSystem Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(impostor.Contains("public static ImpostorSystem Instance => GlobalRegistry.Impostors;"), Is.False);
            Assert.That(impostor.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(impostor.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);

            string proxyPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldProceduralProxyInstance.cs");
            string proxy = File.ReadAllText(proxyPath);
            Assert.That(proxy.Contains("CullingManager manager = GlobalRegistry.Culling;"), Is.True);
            Assert.That(proxy.Contains("CullingManager manager = CullingManager.Instance;"), Is.False);

            string bootstrapPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldLODSceneBootstrap.cs");
            string bootstrap = File.ReadAllText(bootstrapPath);
            Assert.That(bootstrap.Contains("_cullingManager ??= GlobalRegistry.Culling;"), Is.True);
            Assert.That(bootstrap.Contains("_cullingManager ??= CullingManager.Instance;"), Is.False);
            Assert.That(bootstrap.Contains("_authoringScenePath = NormalizeSceneIdentifier(authoringScene.path);"), Is.True);
            Assert.That(bootstrap.Contains("_authoringSceneName = NormalizeSceneIdentifier(authoringScene.name);"), Is.True);
            Assert.That(bootstrap.Contains("string authoringScenePath = NormalizeSceneIdentifier(_authoringScenePath);"), Is.True);
            Assert.That(bootstrap.Contains("if (authoringScenePath.Length != 0)"), Is.True);
            Assert.That(bootstrap.Contains("Scene sceneByPath = SceneManager.GetSceneByPath(authoringScenePath);"), Is.True);
            Assert.That(bootstrap.Contains("string authoringSceneName = NormalizeSceneIdentifier(_authoringSceneName);"), Is.True);
            Assert.That(bootstrap.Contains("if (authoringSceneName.Length != 0)"), Is.True);
            Assert.That(bootstrap.Contains("Scene sceneByName = SceneManager.GetSceneByName(authoringSceneName);"), Is.True);
            Assert.That(bootstrap.Contains("return string.IsNullOrWhiteSpace(sceneIdentifier) ? string.Empty : sceneIdentifier.Trim();"), Is.True);
            Assert.That(bootstrap.Contains("string.IsNullOrEmpty(_authoringScenePath)"), Is.False);
            Assert.That(bootstrap.Contains("string.IsNullOrEmpty(_authoringSceneName)"), Is.False);

            string lodPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "LODSystemManager.cs");
            string lod = File.ReadAllText(lodPath);
            Assert.That(lod.Contains("_impostorSystem = ImpostorSystem.Instance;"), Is.True);
            Assert.That(lod.Contains("_impostorSystem = GlobalRegistry.Impostors;"), Is.False);
        }

        [Test]
        public void WorldPersistentAndStrainOwners_UseOwnerLocalInstancesForRuntimeConsumers()
        {
            string root = Directory.GetCurrentDirectory();
            string persistentPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "PersistentWorldRegistry.cs");
            string persistent = File.ReadAllText(persistentPath);

            Assert.That(persistent.Contains("private static PersistentWorldRegistry s_activeRuntimeInstance;"), Is.True);
            Assert.That(persistent.Contains("public static PersistentWorldRegistry Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(persistent.Contains("public static PersistentWorldRegistry Instance => GlobalRegistry.PersistentWorldRegistry;"), Is.False);
            Assert.That(persistent.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(persistent.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);

            string strainPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EnvironmentalStrainManager.cs");
            string strain = File.ReadAllText(strainPath);
            Assert.That(strain.Contains("private static EnvironmentalStrainManager s_activeRuntimeInstance;"), Is.True);
            Assert.That(strain.Contains("public static EnvironmentalStrainManager Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(strain.Contains("public static EnvironmentalStrainManager Instance => GlobalRegistry.EnvironmentalStrain;"), Is.False);
            Assert.That(strain.Contains("EnvironmentalStrainManager registered = s_activeRuntimeInstance;"), Is.True);

            string ecosystemPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string ecosystem = File.ReadAllText(ecosystemPath);
            Assert.That(ecosystem.Contains("EnvironmentalStrainManager.Instance?.AccumulatePredationStrain"), Is.True);
            Assert.That(ecosystem.Contains("PersistentWorldRegistry.Instance"), Is.True);
            Assert.That(ecosystem.Contains("WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref thermalManager);"), Is.True);
            Assert.That(ecosystem.Contains("AbyssalThermalManager thermalManager = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.False);
            Assert.That(ecosystem.Contains("GlobalRegistry.PersistentWorldRegistry"), Is.False);
            Assert.That(ecosystem.Contains("GlobalRegistry.EnvironmentalStrain?.AccumulatePredationStrain"), Is.False);
            Assert.That(ecosystem.Contains("AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;"), Is.False);

            string thermalPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string thermal = File.ReadAllText(thermalPath);
            Assert.That(thermal.Contains("private static AbyssalThermalManager s_activeRuntimeInstance;"), Is.True);
            Assert.That(thermal.Contains("internal static AbyssalThermalManager ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(thermal.Contains("AbyssalThermalManager registeredThermodynamics = s_activeRuntimeInstance;"), Is.True);
            Assert.That(thermal.Contains("internal static AbyssalThermalManager ActiveRuntimeInstance => GlobalRegistry.Thermodynamics;"), Is.False);
            Assert.That(thermal.Contains("PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;"), Is.True);
            Assert.That(thermal.Contains("PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;"), Is.False);

            string volcanicPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "VolcanicUpdraftDirector.cs");
            string volcanic = File.ReadAllText(volcanicPath);
            Assert.That(volcanic.Contains("WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref thermalManager);"), Is.True);
            Assert.That(volcanic.Contains("_thermodynamicsService = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.False);
            Assert.That(volcanic.Contains("_thermodynamicsService = GlobalRegistry.ThermodynamicsService;"), Is.False);

            string pollutionPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "BasePollutionManager.cs");
            string pollution = File.ReadAllText(pollutionPath);
            Assert.That(pollution.Contains("CacheEnvironmentalStrain(GlobalRegistry.EnvironmentalStrainIndustrialSink);"), Is.True);
            Assert.That(pollution.Contains("CacheEnvironmentalStrain(EnvironmentalStrainManager.Instance);"), Is.False);

            string[] persistentConsumers =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "DestructibleOrganicManager.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "ResourceDistributionDirector.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "World", "FloraRegrowthDirector.cs")
            };

            for (int i = 0; i < persistentConsumers.Length; i++)
            {
                string consumer = File.ReadAllText(persistentConsumers[i]);
                Assert.That(consumer.Contains("PersistentWorldRegistry.Instance"), Is.True, persistentConsumers[i]);
                Assert.That(consumer.Contains("GlobalRegistry.PersistentWorldRegistry"), Is.False, persistentConsumers[i]);
            }
        }

        [Test]
        public void GeologyTerrainOwnersActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string seamPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldGenerativeGeologyTerrainSeamApplier.cs");
            string seam = File.ReadAllText(seamPath);

            Assert.That(seam.Contains("private static WorldGenerativeGeologyTerrainSeamApplier s_activeRuntimeInstance;"), Is.True);
            Assert.That(seam.Contains("internal static WorldGenerativeGeologyTerrainSeamApplier ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(seam.Contains("internal static WorldGenerativeGeologyTerrainSeamApplier ActiveRuntimeInstance => GlobalRegistry.GeologyTerrainSeam;"), Is.False);
            Assert.That(seam.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(seam.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(seam.Contains("ReferenceEquals(GlobalRegistry.GeologyTerrainSeam, this)"), Is.True);

            string voxelBridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldGenerativeGeologyVoxelBridgeDirector.cs");
            string voxelBridge = File.ReadAllText(voxelBridgePath);
            Assert.That(voxelBridge.Contains("private static WorldGenerativeGeologyVoxelBridgeDirector s_activeRuntimeInstance;"), Is.True);
            Assert.That(voxelBridge.Contains("internal static WorldGenerativeGeologyVoxelBridgeDirector ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(voxelBridge.Contains("internal static WorldGenerativeGeologyVoxelBridgeDirector ActiveRuntimeInstance => GlobalRegistry.GeologyVoxelBridge;"), Is.False);
            Assert.That(voxelBridge.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(voxelBridge.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(voxelBridge.Contains("ReferenceEquals(GlobalRegistry.GeologyVoxelBridge, this)"), Is.True);
            Assert.That(voxelBridge.Contains("WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref _thermalManager);"), Is.True);
            Assert.That(voxelBridge.Contains("_thermalManager = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.False);
            Assert.That(voxelBridge.Contains("_thermalManager = GlobalRegistry.Thermodynamics;"), Is.False);
            Assert.That(voxelBridge.Contains("_persistentWorldRegistry = PersistentWorldRegistry.Instance;"), Is.True);
            Assert.That(voxelBridge.Contains("_persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;"), Is.False);
            Assert.That(voxelBridge.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref _resourceDistributionDirector);"), Is.True);
            Assert.That(voxelBridge.Contains("WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref resourceDirector)"), Is.True);
            Assert.That(voxelBridge.Contains("_resourceDistributionDirector = currentService as ResourceDistributionDirector;"), Is.False);
            Assert.That(voxelBridge.Contains("_resourceDistributionDirector = GlobalRegistry.ResourceDistribution;"), Is.False);
            Assert.That(voxelBridge.Contains("private void CacheObjectPoolService(ObjectPoolManager candidate)"), Is.True);
            Assert.That(voxelBridge.Contains("private bool TryResolveCachedObjectPool(out IObjectPoolService pool)"), Is.True);
            Assert.That(voxelBridge.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);"), Is.True);
            Assert.That(voxelBridge.Contains("CacheObjectPoolService(null);"), Is.True);
            Assert.That(voxelBridge.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)"), Is.True);
            Assert.That(voxelBridge.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)"), Is.True);
            Assert.That(voxelBridge.Contains("_objectPoolService = currentService as IObjectPoolService;"), Is.False);
            Assert.That(voxelBridge.Contains("_objectPoolService = GlobalRegistry.ObjectPoolService;"), Is.False);
            Assert.That(voxelBridge.Contains("IObjectPoolService pool = _objectPoolService;"), Is.False);
            Assert.That(voxelBridge.Contains("private struct PendingRequestState"), Is.True);
            Assert.That(voxelBridge.Contains("private sealed class PendingRequestState"), Is.False);
            Assert.That(voxelBridge.Contains("public int Sequence;"), Is.True);
            Assert.That(voxelBridge.Contains("return new PendingRequestState"), Is.False);
            Assert.That(voxelBridge.Contains("CancellationTokenSource.CreateLinkedTokenSource"), Is.False);
            Assert.That(voxelBridge.Contains("IsPendingRequestActive(request.runtimeKey, signature, sequence)"), Is.True);
            Assert.That(voxelBridge.Contains("new Dictionary<long, GameObject>(RuntimeKeySetCapacity)"), Is.True);
            Assert.That(voxelBridge.Contains("new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(RuntimeKeySetCapacity)"), Is.True);
            Assert.That(voxelBridge.Contains("new List<long>(RuntimeKeySetCapacity)"), Is.True);
            Assert.That(voxelBridge.Contains("new List<WorldGenerativeGeologyVoxelBlendRequest>(RuntimeKeySetCapacity)"), Is.True);
            Assert.That(voxelBridge.Contains("new Dictionary<long, GameObject>(32)"), Is.False);
            Assert.That(voxelBridge.Contains("new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(64)"), Is.False);
            Assert.That(voxelBridge.Contains("new List<long>(32)"), Is.False);
            Assert.That(voxelBridge.Contains("new List<WorldGenerativeGeologyVoxelBlendRequest>(64)"), Is.False);
            Assert.That(voxelBridge.Contains("AddCandidateRequest(request);"), Is.True);
            Assert.That(voxelBridge.Contains("_sortedRequests.Count < RuntimeKeySetCapacity"), Is.True);
            Assert.That(voxelBridge.Contains("int configuredMax = Mathf.Clamp(maxRuntimeVolumes, 1, RuntimeKeySetCapacity);"), Is.True);
            Assert.That(voxelBridge.Contains("private void EnsureFallbackGenerationPreset()"), Is.True);
            Assert.That(voxelBridge.Contains("EnsureFallbackGenerationPreset();"), Is.True);

            int resolvePresetIndex = voxelBridge.IndexOf("private CavePreset ResolveGenerationPreset()", StringComparison.Ordinal);
            int ensurePresetIndex = voxelBridge.IndexOf("private void EnsureFallbackGenerationPreset()", StringComparison.Ordinal);
            Assert.That(resolvePresetIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(ensurePresetIndex, Is.GreaterThan(resolvePresetIndex));

            string resolvePresetBlock = voxelBridge.Substring(resolvePresetIndex, ensurePresetIndex - resolvePresetIndex);
            Assert.That(resolvePresetBlock.Contains("CavePresetLibrary.Create(CavePresetType.Grotto)"), Is.False);

            string smokePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldGenRegistrySmokeTester.cs");
            string smoke = File.ReadAllText(smokePath);
            int terrainSeamIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.TerrainSeam]", StringComparison.Ordinal);
            int voxelBridgeIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.VoxelBridge]", terrainSeamIndex, StringComparison.Ordinal);
            int worldGenIndex = smoke.IndexOf("if (GlobalRegistry.WorldGen == null)", voxelBridgeIndex, StringComparison.Ordinal);
            Assert.That(terrainSeamIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(voxelBridgeIndex, Is.GreaterThan(terrainSeamIndex));
            Assert.That(worldGenIndex, Is.GreaterThan(voxelBridgeIndex));

            string terrainSeamSmokeBlock = smoke.Substring(terrainSeamIndex, voxelBridgeIndex - terrainSeamIndex);
            string voxelBridgeSmokeBlock = smoke.Substring(voxelBridgeIndex, worldGenIndex - voxelBridgeIndex);
            Assert.That(terrainSeamSmokeBlock.Contains("Object.DestroyImmediate(terrainSeamObject);"), Is.True);
            Assert.That(terrainSeamSmokeBlock.Contains("GlobalRegistry.UnregisterGeologyTerrainSeamRuntime(terrainSeam);"), Is.False);
            Assert.That(voxelBridgeSmokeBlock.Contains("Object.DestroyImmediate(voxelBridgeObject);"), Is.True);
            Assert.That(voxelBridgeSmokeBlock.Contains("GlobalRegistry.UnregisterGeologyVoxelBridgeRuntime(voxelBridge);"), Is.False);
        }

        [Test]
        public void VoxelStreamingBridge_UsesColdFadeMaterialPoolInsteadOfRuntimeMaterialClone()
        {
            string root = Directory.GetCurrentDirectory();
            string bridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonVoxelStreamingBridge.cs");
            string bridge = File.ReadAllText(bridgePath);

            Assert.That(bridge.Contains("private readonly Material[] _chunkFadeMaterialPool = new Material[MaxChunkFadeStateCapacity]"), Is.True);
            Assert.That(bridge.Contains("private readonly bool[] _chunkFadeMaterialPoolInUse = new bool[MaxChunkFadeStateCapacity]"), Is.True);
            Assert.That(bridge.Contains("EnsureChunkFadeMaterialPool();"), Is.True);
            Assert.That(bridge.Contains("TryAcquireChunkFadeMaterial(material, out Material runtimeMaterial, out int runtimeMaterialPoolIndex)"), Is.True);
            Assert.That(bridge.Contains("ReleaseChunkFadeMaterial(state.RuntimeMaterialPoolIndex, state.RuntimeMaterial);"), Is.True);
            Assert.That(bridge.Contains("ReleaseChunkFadeMaterialPool();"), Is.True);
            Assert.That(bridge.Contains("private void RefreshColdReferences()"), Is.True);
            Assert.That(bridge.Contains("private void ResolveReferences()"), Is.False);

            int registerIndex = bridge.IndexOf("private void RegisterChunkFadeImmediate", StringComparison.Ordinal);
            int tickIndex = bridge.IndexOf("private void TickChunkFade", registerIndex, StringComparison.Ordinal);
            Assert.That(registerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(tickIndex, Is.GreaterThan(registerIndex));

            string registerBlock = bridge.Substring(registerIndex, tickIndex - registerIndex);
            Assert.That(registerBlock.Contains("new Material(material)"), Is.False);
            Assert.That(registerBlock.Contains("Destroy(runtimeMaterial)"), Is.False);

            int tickRouteIndex = bridge.IndexOf("public void Tick(float dt)", StringComparison.Ordinal);
            int lateFrameIndex = bridge.IndexOf("public void LateFrameTick()", tickRouteIndex, StringComparison.Ordinal);
            int slowTickIndex = bridge.IndexOf("public void SlowTick()", lateFrameIndex, StringComparison.Ordinal);
            int spawnIndex = bridge.IndexOf("private async Awaitable SpawnCaveAsync", slowTickIndex, StringComparison.Ordinal);
            int awakeIndex = bridge.IndexOf("private void Awake()", StringComparison.Ordinal);
            int enableIndex = bridge.IndexOf("private void OnEnable()", awakeIndex, StringComparison.Ordinal);
            Assert.That(awakeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(enableIndex, Is.GreaterThan(awakeIndex));
            Assert.That(tickRouteIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(lateFrameIndex, Is.GreaterThan(tickRouteIndex));
            Assert.That(slowTickIndex, Is.GreaterThan(lateFrameIndex));
            Assert.That(spawnIndex, Is.GreaterThan(slowTickIndex));

            string awakeBlock = bridge.Substring(awakeIndex, enableIndex - awakeIndex);
            string tickBlock = bridge.Substring(tickRouteIndex, lateFrameIndex - tickRouteIndex);
            string slowTickBlock = bridge.Substring(slowTickIndex, spawnIndex - slowTickIndex);
            Assert.That(awakeBlock.Contains("RefreshColdReferences();"), Is.False);
            Assert.That(tickBlock.Contains("RefreshColdReferences();"), Is.False);
            Assert.That(slowTickBlock.Contains("RefreshColdReferences();"), Is.False);

            int hotswapIndex = bridge.IndexOf("public void OnGlobalRegistryServiceReplaced", StringComparison.Ordinal);
            int registerHotSwapIndex = bridge.IndexOf("private void TryRegisterHotSwapListener()", hotswapIndex, StringComparison.Ordinal);
            Assert.That(hotswapIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(registerHotSwapIndex, Is.GreaterThan(hotswapIndex));

            string hotswapBlock = bridge.Substring(hotswapIndex, registerHotSwapIndex - hotswapIndex);
            Assert.That(hotswapBlock.Contains("case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:"), Is.True);
            Assert.That(hotswapBlock.Contains("vegetationBridge = currentService as HectonMapMagicVegetationBridge;"), Is.True);
            Assert.That(hotswapBlock.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);"), Is.True);
            Assert.That(hotswapBlock.Contains("case GlobalRegistryServiceSlot.VoxelEngineRuntime:"), Is.True);
            Assert.That(hotswapBlock.Contains("voxelEngine = currentService as HectonVoxelEngine;"), Is.True);
            Assert.That(hotswapBlock.Contains("ReleaseChunkFadeMaterialPool();"), Is.True);
            Assert.That(hotswapBlock.Contains("case GlobalRegistryServiceSlot.Player:"), Is.True);
            Assert.That(hotswapBlock.Contains("playerTransform = playerContext != null ? playerContext.PlayerTransform : null;"), Is.True);
        }

        [Test]
        public void ProceduralWorldOwnersActiveRuntimeInstance_UsesOwnerLocalPointerInsteadOfRegistryPoll()
        {
            string root = Directory.GetCurrentDirectory();
            string fieldPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldProceduralFieldSampler.cs");
            string field = File.ReadAllText(fieldPath);

            Assert.That(field.Contains("private static WorldProceduralFieldSampler s_activeRuntimeInstance;"), Is.True);
            Assert.That(field.Contains("internal static WorldProceduralFieldSampler ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(field.Contains("internal static WorldProceduralFieldSampler ActiveRuntimeInstance => GlobalRegistry.ProceduralFieldSampler;"), Is.False);
            Assert.That(field.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(field.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(field.Contains("ReferenceEquals(GlobalRegistry.ProceduralFieldSampler, this)"), Is.True);
            Assert.That(field.Contains("activeInstance.ClearActiveRuntimeInstance();"), Is.True);

            string scatterPath = Path.Combine(root, "Assets", "_Project", "Scripts", "WorldProceduralScatterDirector.cs");
            string scatter = File.ReadAllText(scatterPath);
            Assert.That(scatter.Contains("private static WorldProceduralScatterDirector s_activeRuntimeInstance;"), Is.True);
            Assert.That(scatter.Contains("internal static WorldProceduralScatterDirector ActiveRuntimeInstance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(scatter.Contains("internal static WorldProceduralScatterDirector ActiveRuntimeInstance => GlobalRegistry.ProceduralScatter;"), Is.False);
            Assert.That(scatter.Contains("s_activeRuntimeInstance = this;"), Is.True);
            Assert.That(scatter.Contains("ReferenceEquals(s_activeRuntimeInstance, this)"), Is.True);
            Assert.That(scatter.Contains("ReferenceEquals(GlobalRegistry.ProceduralScatter, this)"), Is.True);
            Assert.That(scatter.Contains("activeInstance.ClearActiveRuntimeInstance();"), Is.True);

            string smokePath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldGenRegistrySmokeTester.cs");
            string smoke = File.ReadAllText(smokePath);
            int fieldIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.FieldSampler]", StringComparison.Ordinal);
            int resourceIndex = smoke.IndexOf("[WorldGenRegistrySmokeTester.ResourceDistribution]", fieldIndex, StringComparison.Ordinal);
            Assert.That(fieldIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resourceIndex, Is.GreaterThan(fieldIndex));
            string fieldSmokeBlock = smoke.Substring(fieldIndex, resourceIndex - fieldIndex);
            Assert.That(fieldSmokeBlock.Contains("Object.DestroyImmediate(fieldSamplerObject);"), Is.True);
            Assert.That(fieldSmokeBlock.Contains("GlobalRegistry.UnregisterProceduralFieldSampler(fieldSampler);"), Is.False);
        }

        [Test]
        public void TileHeightReadbackFinalization_UsesNativeArrayCopyInsteadOfManagedElementLoop()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "VegetationTileCacheResidency.cs");
            string source = File.ReadAllText(path);

            int finalizeIndex = source.IndexOf("private bool TryFinalizeTileHeightReadback(TileRuntimeState state)", StringComparison.Ordinal);
            int nextMethodIndex = source.IndexOf("private bool CanRefreshThreatSpatialSnapshots()", finalizeIndex, StringComparison.Ordinal);
            Assert.That(finalizeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextMethodIndex, Is.GreaterThan(finalizeIndex));

            string finalizeBlock = source.Substring(finalizeIndex, nextMethodIndex - finalizeIndex);
            Assert.That(finalizeBlock.Contains("NativeArray<ushort>.Copy(readbackData, 0, heightSamples, 0, pendingBuffer.HeightSampleCount);"), Is.True);
            Assert.That(finalizeBlock.Contains("heightSamples[i] = readbackData[i];"), Is.False);
        }

        [Test]
        public void VoxelDynamicNavGrid_DynamicObstacleUpdateIsScheduledAndSingleFlight()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "VoxelDynamicNavGridRuntime.cs");
            string source = File.ReadAllText(path);

            int scheduleIndex = source.IndexOf("internal static void SchedulePendingDynamicObstacleUpdates()", StringComparison.Ordinal);
            int pendingIndex = source.IndexOf("private static bool HasPendingDynamicObstacleUpdate()", scheduleIndex, StringComparison.Ordinal);
            Assert.That(scheduleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(pendingIndex, Is.GreaterThan(scheduleIndex));

            string scheduleBlock = source.Substring(scheduleIndex, pendingIndex - scheduleIndex);
            Assert.That(scheduleBlock.Contains("record.HasPendingDynamicUpdate ||"), Is.True);
            Assert.That(scheduleBlock.Contains("new CopyUShortBufferJob"), Is.True);
            Assert.That(scheduleBlock.Contains("PartialObstacleResetAndStampJob"), Is.True);
            Assert.That(scheduleBlock.Contains("PartialClearanceDilationJob"), Is.True);
            Assert.That(scheduleBlock.Contains(".Run("), Is.False);
            Assert.That(source.Contains("private static void CopyUShortBuffer("), Is.False);
        }

        [Test]
        public void AbyssalThermalMap_JacobiDiffusionIsScheduledNotRun()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string source = File.ReadAllText(path);

            int advanceIndex = source.IndexOf("private void AdvanceThermalMapColdTick(float deltaSeconds)", StringComparison.Ordinal);
            int completeIndex = source.IndexOf("private void CompleteThermalMapJobIfReady", advanceIndex, StringComparison.Ordinal);
            Assert.That(advanceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(completeIndex, Is.GreaterThan(advanceIndex));

            string advanceBlock = source.Substring(advanceIndex, completeIndex - advanceIndex);
            Assert.That(advanceBlock.Contains("_thermalMapJobActive"), Is.True);
            Assert.That(advanceBlock.Contains("job.Schedule(ThermalMapDiffusionSliceCellCount, ThermalMapDiffusionJobBatchSize)"), Is.True);
            Assert.That(advanceBlock.Contains("job.Run(ThermalMapDiffusionSliceCellCount)"), Is.False);
            Assert.That(source.Contains("DispatcherJobSwap.TryComplete(ref _thermalMapJobHandle, forceComplete)"), Is.True);
        }

        [Test]
        public void AbyssalThermalRoomInfiltration_UsesCachedGasDynamicsDependency()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("private IGasDynamicsSolver _gasDynamics;"), Is.True);
            Assert.That(source.Contains("_gasDynamics = GlobalRegistry.GasDynamics;"), Is.True);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.GasDynamicsRuntime:"), Is.True);
            Assert.That(source.Contains("_gasDynamics = currentService as IGasDynamicsSolver;"), Is.True);

            int methodIndex = source.IndexOf("private void ApplyThermalInfiltrationToBaseModules", StringComparison.Ordinal);
            int nextMethodIndex = source.IndexOf("private Vector3 ResolveCrystallizationBoundaryPosition", methodIndex, StringComparison.Ordinal);
            Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextMethodIndex, Is.GreaterThan(methodIndex));

            string methodBlock = source.Substring(methodIndex, nextMethodIndex - methodIndex);
            Assert.That(methodBlock.Contains("IGasDynamicsSolver gasDynamics = _gasDynamics;"), Is.True);
            Assert.That(methodBlock.Contains("GlobalRegistry.GasDynamics"), Is.False);
        }

        [Test]
        public void AbyssalThermalTickRoutes_DoNotResolveDependencies()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string source = File.ReadAllText(path);

            AssertHotThermalBlockIsDependencyPure(source, "public void Tick(float dt)", "public void SlowTick()");
            AssertHotThermalBlockIsDependencyPure(source, "public void SlowTick()", "public bool ReportFlashFreeze(");
            AssertHotThermalBlockIsDependencyPure(source, "public void FixedTick(float fixedDeltaTime)", "private Vector3 ResolvePlayerRuntimePosition(");

            Assert.That(source.Contains("private void RebindPlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)"), Is.True);
            Assert.That(source.Contains("case GlobalRegistryServiceSlot.Player:"), Is.True);
            Assert.That(source.Contains("RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);"), Is.True);
            Assert.That(source.Contains("private void RefreshFluidDecalOwner()"), Is.True);
            Assert.That(source.Contains("private void RefreshPlayerComponentCaches()"), Is.True);
        }

        [Test]
        public void AbyssalThermalFixedDamageRoute_UsesCachedDamageReceivers()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("private IDamageReceiver _playerThermalDamageReceiver;"), Is.True);
            Assert.That(source.Contains("private IDamageReceiver _submarineThermalDamageReceiver;"), Is.True);
            Assert.That(source.Contains("private void RefreshThermalDamageReceiverCaches()"), Is.True);
            Assert.That(source.Contains("private void RebindSubmarineRuntimeContext(ISubmarineRuntimeContext submarineRuntimeContext)"), Is.True);
            Assert.That(source.Contains("RebindSubmarineRuntimeContext(currentService as ISubmarineRuntimeContext);"), Is.True);
            Assert.That(source.Contains("TryResolveDamageReceiver"), Is.False);

            string fixedBlock = ExtractSourceBlock(source, "public void FixedTick(float fixedDeltaTime)", "private Vector3 ResolvePlayerRuntimePosition(");
            Assert.That(fixedBlock.Contains("_playerThermalDamageReceiver"), Is.True);
            Assert.That(fixedBlock.Contains("_submarineThermalDamageReceiver"), Is.True);
            Assert.That(fixedBlock.Contains("GetComponent<IDamageReceiver>"), Is.False);

            string processBlock = ExtractSourceBlock(source, "private bool ProcessThermalGameplayTarget(", "private void QueueBoilingDamage(");
            Assert.That(processBlock.Contains("IDamageReceiver fallbackDamageReceiver"), Is.True);
            Assert.That(processBlock.Contains("FindDamageReceiverInParentsCold"), Is.False);
            Assert.That(processBlock.Contains("GetComponent<IDamageReceiver>"), Is.False);

            string queueBlock = ExtractSourceBlock(source, "private void QueueBoilingDamage(", "private bool TrySampleTemperatureCelsius(");
            Assert.That(queueBlock.Contains("IDamageReceiver fallbackDamageReceiver"), Is.True);
            Assert.That(queueBlock.Contains("ApplyThermalOwnerFallbackDamage("), Is.True);
            Assert.That(queueBlock.Contains("GetComponent<IDamageReceiver>"), Is.False);

            string shockBlock = ExtractSourceBlock(source, "private void EmitThermalShock(", "private static bool TryResolveRegisteredCombatTarget(");
            Assert.That(shockBlock.Contains("IDamageReceiver fallbackDamageReceiver"), Is.True);
            Assert.That(shockBlock.Contains("GetComponent<IDamageReceiver>"), Is.False);

            string coldLookupBlock = ExtractSourceBlock(source, "private static bool FindDamageReceiverInParentsCold(", "private void RefreshFluidDecalOwner()");
            Assert.That(coldLookupBlock.Contains("GetComponent<IDamageReceiver>()"), Is.True);
        }

        [Test]
        public void ChemicalInfluenceStaticReadAccessors_DoNotPublishOrCreateRuntime()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "ChemicalInfluenceGrid.cs");
            string source = File.ReadAllText(path);

            Assert.That(source.Contains("private static bool TryGetReadableRuntime(out ChemicalInfluenceGrid instance)"), Is.True);

            AssertPureChemicalReadBlock(source, "internal static bool TryGetPublishedSnapshot(", "internal static bool TryGetActivePublishedSnapshot(");
            AssertPureChemicalReadBlock(source, "internal static bool TryGetActivePublishedSnapshot(", "internal static bool TryGetPublishedBreadcrumbs(");
            AssertPureChemicalReadBlock(source, "internal static bool TryGetPublishedBreadcrumbs(", "internal static bool TrySampleNormalizedChannels(");
            AssertPureChemicalReadBlock(source, "internal static bool TrySampleNormalizedChannels(", "internal static bool TrySampleScentGrid01(");
            AssertPureChemicalReadBlock(source, "internal static bool TrySampleScentGrid01(", "internal static bool TryFindNearestScentWaypoint(");
            AssertPureChemicalReadBlock(source, "internal static bool TryFindNearestScentWaypoint(", "internal static void QueueBloodScent(");
            AssertPureChemicalReadBlock(source, "public static bool TryGetTuningSnapshot(", "public static bool TrySetTuningFromEditor(");

            int beginAiFrameIndex = source.IndexOf("internal static void BeginAiFrame(int frameId)", StringComparison.Ordinal);
            Assert.That(beginAiFrameIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source.IndexOf("EnsureRuntimeInstance().PublishFrame(frameId);", beginAiFrameIndex, StringComparison.Ordinal), Is.GreaterThan(beginAiFrameIndex));
        }

        private static void AssertPureChemicalReadBlock(string source, string startToken, string endToken)
        {
            int startIndex = source.IndexOf(startToken, StringComparison.Ordinal);
            int endIndex = source.IndexOf(endToken, startIndex + startToken.Length, StringComparison.Ordinal);
            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), startToken);
            Assert.That(endIndex, Is.GreaterThan(startIndex), endToken);

            string block = source.Substring(startIndex, endIndex - startIndex);
            Assert.That(block.Contains("EnsureRuntimeInstance()"), Is.False, startToken);
            Assert.That(block.Contains("PublishFrame("), Is.False, startToken);
            Assert.That(block.Contains("InitializeRuntime()"), Is.False, startToken);
            Assert.That(block.Contains("TryGetReadableRuntime(out ChemicalInfluenceGrid instance)"), Is.True, startToken);
        }

        private static void AssertStreamingOwnerLifecycle(string source)
        {
            int enableIndex = source.IndexOf("private void OnEnable()", StringComparison.Ordinal);
            int disableIndex = source.IndexOf("private void OnDisable()", enableIndex, StringComparison.Ordinal);
            int destroyIndex = source.IndexOf("private void OnDestroy()", disableIndex, StringComparison.Ordinal);
            Assert.That(enableIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(disableIndex, Is.GreaterThan(enableIndex));
            Assert.That(destroyIndex, Is.GreaterThan(disableIndex));

            string enableBlock = source.Substring(enableIndex, disableIndex - enableIndex);
            string disableBlock = source.Substring(disableIndex, destroyIndex - disableIndex);
            Assert.That(enableBlock.Contains("PublishActiveRuntimeInstance();"), Is.True);
            Assert.That(disableBlock.Contains("if (ActiveRuntimeInstance == this)"), Is.True);
            Assert.That(disableBlock.Contains("ClearActiveRuntimeInstance();"), Is.True);
        }

        private static void AssertVegetationBridgeServiceCastsAreResolverValidated(string source, string sourcePath)
        {
            const string castToken = "currentService as HectonMapMagicVegetationBridge";
            const string resolverToken = "WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge";
            int searchIndex = 0;
            while (true)
            {
                int castIndex = source.IndexOf(castToken, searchIndex, StringComparison.Ordinal);
                if (castIndex < 0)
                    return;

                int windowEnd = Math.Min(source.Length, castIndex + 320);
                string window = source.Substring(castIndex, windowEnd - castIndex);
                Assert.That(window.Contains(resolverToken), Is.True, sourcePath);
                searchIndex = castIndex + castToken.Length;
            }
        }

        private static void AssertHotThermalBlockIsDependencyPure(string source, string startToken, string endToken)
        {
            int startIndex = source.IndexOf(startToken, StringComparison.Ordinal);
            int endIndex = source.IndexOf(endToken, startIndex + startToken.Length, StringComparison.Ordinal);
            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), startToken);
            Assert.That(endIndex, Is.GreaterThan(startIndex), endToken);

            string block = source.Substring(startIndex, endIndex - startIndex);
            Assert.That(block.Contains("ResolveDependencies()"), Is.False, startToken);
            Assert.That(block.Contains("TryGetComponent"), Is.False, startToken);
            Assert.That(block.Contains("AddComponent"), Is.False, startToken);
            Assert.That(block.Contains("BootstrapState.TryGetCurrentPlayerTransform"), Is.False, startToken);
        }

        private static string ExtractSourceBlock(string source, string startToken, string endToken)
        {
            int startIndex = source.IndexOf(startToken, StringComparison.Ordinal);
            int endIndex = source.IndexOf(endToken, startIndex + startToken.Length, StringComparison.Ordinal);
            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), startToken);
            Assert.That(endIndex, Is.GreaterThan(startIndex), endToken);
            return source.Substring(startIndex, endIndex - startIndex);
        }
    }
}
