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
            Assert.That(resolveBlock.Contains("GlobalRegistry.MapMagic"), Is.False);

            string seamPath = Path.Combine(root, "Assets", "_Project", "Scripts", "VoxelSeamDirector.cs");
            string seam = File.ReadAllText(seamPath);
            Assert.That(seam.Contains("MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;"), Is.True);
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
            Assert.That(ecosystem.Contains("MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;"), Is.True);
            Assert.That(ecosystem.Contains("MapMagicBridge bridge = MapMagicBridge.Instance;"), Is.True);
            Assert.That(ecosystem.Contains("ResourceDistributionDirector resourceDistribution = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.True);
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
            Assert.That(applyBlock.Contains("HectonRockManager manager = HectonRockManager.Instance;"), Is.True);
            Assert.That(applyBlock.Contains("HectonRockManager manager = GlobalRegistry.RockManager;"), Is.False);
            int unregisterIndex = applyBlock.IndexOf("manager.UnregisterChunk(chunkCoord);", StringComparison.Ordinal);
            int nullCheckIndex = applyBlock.IndexOf("if (layerMatrices == null || layerMatrices.Count == 0)", StringComparison.Ordinal);
            Assert.That(unregisterIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(nullCheckIndex, Is.GreaterThan(unregisterIndex));

            int clearIndex = source.IndexOf("public override void ClearApplied(TileData data, UnityEngine.Terrain terrain)", resolutionIndex, StringComparison.Ordinal);
            Assert.That(clearIndex, Is.GreaterThan(resolutionIndex));
            string clearBlock = source.Substring(clearIndex);
            Assert.That(clearBlock.Contains("HectonRockManager manager = HectonRockManager.Instance;"), Is.True);
            Assert.That(clearBlock.Contains("HectonRockManager manager = GlobalRegistry.RockManager;"), Is.False);
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
            Assert.That(source.Contains("_objectPoolService = currentService as IObjectPoolService;"), Is.True);
            Assert.That(source.Contains("_physicsService = currentService as IPhysicsService;"), Is.True);
            Assert.That(source.Contains("_vramPressureReadModel = currentService as IVramPressureReadModel;"), Is.True);

            int despawnIndex = source.IndexOf("public void DespawnVolume(GameObject volume)", StringComparison.Ordinal);
            int purgeIndex = source.IndexOf("public void PurgeNullVolumes()", despawnIndex, StringComparison.Ordinal);
            Assert.That(despawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(purgeIndex, Is.GreaterThan(despawnIndex));
            string despawnBlock = source.Substring(despawnIndex, purgeIndex - despawnIndex);
            Assert.That(despawnBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.True);
            Assert.That(despawnBlock.Contains("GlobalRegistry.ObjectPoolService"), Is.False);

            int clearIndex = source.IndexOf("public void ClearAllVolumes()", StringComparison.Ordinal);
            int activeCountIndex = source.IndexOf("public int ActiveVolumeCount", clearIndex, StringComparison.Ordinal);
            Assert.That(clearIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(activeCountIndex, Is.GreaterThan(clearIndex));
            string clearBlock = source.Substring(clearIndex, activeCountIndex - clearIndex);
            Assert.That(clearBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.True);
            Assert.That(clearBlock.Contains("GlobalRegistry.ObjectPoolService"), Is.False);

            int spawnIndex = source.IndexOf("GameObject SpawnVolume()", StringComparison.Ordinal);
            int weldedMeshIndex = source.IndexOf("Mesh BuildWeldedMeshNative", spawnIndex, StringComparison.Ordinal);
            Assert.That(spawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(weldedMeshIndex, Is.GreaterThan(spawnIndex));
            string spawnBlock = source.Substring(spawnIndex, weldedMeshIndex - spawnIndex);
            Assert.That(spawnBlock.Contains("IObjectPoolService pool = _objectPoolService;"), Is.True);
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
            Assert.That(gpr.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.True);
            Assert.That(gpr.Contains("_worldResourceSpawnerReadModel = GlobalRegistry.WorldResourceSpawner;"), Is.False);
            Assert.That(gpr.Contains("WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine)"), Is.True);
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

            string runtimeBridgePath = Path.Combine(root, "Assets", "_Project", "Scripts", "Plugins", "MapMagic", "MapMagicRuntimeBridge.cs");
            string runtimeBridge = File.ReadAllText(runtimeBridgePath);
            Assert.That(runtimeBridge.Contains("_cachedVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;"), Is.True);

            string microFaunaPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string microFauna = File.ReadAllText(microFaunaPath);
            Assert.That(microFauna.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);"), Is.True);
            Assert.That(microFauna.Contains("_mapMagicVegetationBridge = GlobalRegistry.MapMagicVegetation;"), Is.False);
            Assert.That(microFauna.Contains("_ecosystemDirector = EcosystemDirector.ActiveRuntimeInstance;"), Is.True);
            Assert.That(microFauna.Contains("_proceduralScatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;"), Is.True);
            Assert.That(microFauna.Contains("_biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;"), Is.True);
            Assert.That(microFauna.Contains("_ecosystemDirector = GlobalRegistry.EcosystemDirector;"), Is.False);
            Assert.That(microFauna.Contains("_proceduralScatterDirector = GlobalRegistry.ProceduralScatter;"), Is.False);
            Assert.That(microFauna.Contains("_biomeMatrixDirector = GlobalRegistry.BiomeMatrix;"), Is.False);

            string migrationPath = Path.Combine(root, "Assets", "_Project", "Scripts", "Ecosystem", "MigrationDirector.cs");
            string migration = File.ReadAllText(migrationPath);
            Assert.That(migration.Contains("WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);"), Is.True);
            Assert.That(migration.Contains("_mapMagicVegetationBridge = GlobalRegistry.MapMagicVegetation;"), Is.False);
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
            Assert.That(voxelEngine.Contains("ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.True);
            Assert.That(voxelEngine.Contains("ResourceDistributionDirector director = GlobalRegistry.ResourceDistribution;"), Is.False);

            string anomalyBindingPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "HectonAnomalyResourceBinding.cs");
            string anomalyBinding = File.ReadAllText(anomalyBindingPath);
            Assert.That(anomalyBinding.Contains("ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;"), Is.True);
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
            Assert.That(culling.Contains("public static CullingManager Instance => s_activeRuntimeInstance;"), Is.True);
            Assert.That(culling.Contains("public static CullingManager Instance => GlobalRegistry.Culling;"), Is.False);
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
            Assert.That(proxy.Contains("CullingManager manager = CullingManager.Instance;"), Is.True);
            Assert.That(proxy.Contains("CullingManager manager = GlobalRegistry.Culling;"), Is.False);

            string bootstrapPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "WorldLODSceneBootstrap.cs");
            string bootstrap = File.ReadAllText(bootstrapPath);
            Assert.That(bootstrap.Contains("_cullingManager ??= CullingManager.Instance;"), Is.True);
            Assert.That(bootstrap.Contains("_cullingManager ??= GlobalRegistry.Culling;"), Is.False);

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
            Assert.That(ecosystem.Contains("AbyssalThermalManager thermalManager = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.True);
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
            Assert.That(volcanic.Contains("_thermodynamicsService = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.True);
            Assert.That(volcanic.Contains("_thermodynamicsService = GlobalRegistry.ThermodynamicsService;"), Is.False);

            string pollutionPath = Path.Combine(root, "Assets", "_Project", "Scripts", "World", "BasePollutionManager.cs");
            string pollution = File.ReadAllText(pollutionPath);
            Assert.That(pollution.Contains("CacheEnvironmentalStrain(EnvironmentalStrainManager.Instance);"), Is.True);
            Assert.That(pollution.Contains("CacheEnvironmentalStrain(GlobalRegistry.EnvironmentalStrainIndustrialSink);"), Is.False);

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
            Assert.That(voxelBridge.Contains("_thermalManager = AbyssalThermalManager.ActiveRuntimeInstance;"), Is.True);
            Assert.That(voxelBridge.Contains("_thermalManager = GlobalRegistry.Thermodynamics;"), Is.False);
            Assert.That(voxelBridge.Contains("_persistentWorldRegistry = PersistentWorldRegistry.Instance;"), Is.True);
            Assert.That(voxelBridge.Contains("_persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;"), Is.False);
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
