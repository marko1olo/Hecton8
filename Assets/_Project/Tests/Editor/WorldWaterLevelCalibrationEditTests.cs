using System;
using System.IO;
using Hecton8.World;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldWaterLevelCalibrationEditTests
    {
        [Test]
        public void WorldWaterLevelCalibrationAuthoring_MovesCrestRootWithoutTerrainMutation()
        {
            string contractsSource = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "Contracts",
                "WorldWaterLevelCalibrationContracts.cs");
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "WorldWaterLevelCalibrationAuthoring.cs");
            string apply = ExtractMethodBody(source, "public bool TryApplyWaterLevel()");
            string resolveRoot = ExtractMethodBody(source, "private Transform ResolveTargetRoot()");
            string mathSource = ExtractClassBody(contractsSource, "public static class WorldWaterLevelCalibrationMath");

            StringAssert.Contains("public const int DtoBytes = 32;", contractsSource);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = WorldWaterLevelCalibrationMath.DtoBytes)]", contractsSource);
            StringAssert.Contains("public const int DefaultAuthoringSeed = 880031;", contractsSource);
            StringAssert.Contains("public const float MinimumCalibrationTravelMeters = 100f;", contractsSource);
            StringAssert.Contains("public const float DefaultCalibrationTravelMeters = 1000f;", contractsSource);
            StringAssert.Contains("public interface IWorldWaterLevelCalibrationReadModel", contractsSource);
            StringAssert.Contains("public interface IWorldWaterLevelCalibrationWriteModel", contractsSource);
            StringAssert.Contains("public static class WorldWaterLevelCalibrationRuntimeRegistry", contractsSource);
            StringAssert.Contains("DuplicateOwner = 1u << 4", contractsSource);
            StringAssert.Contains("MaxTrackedReadModels = 8", contractsSource);
            StringAssert.Contains("private static readonly IWorldWaterLevelCalibrationReadModel[] s_readModels", contractsSource);
            StringAssert.Contains("public static uint DuplicateOwnerCount", contractsSource);
            StringAssert.Contains("trackedDuplicates + s_untrackedDuplicateOwnerCount", contractsSource);
            StringAssert.Contains("object.ReferenceEquals(s_readModels[i], readModel)", contractsSource);
            StringAssert.Contains("s_readModels[j - 1] = s_readModels[j];", contractsSource);
            StringAssert.Contains("s_untrackedDuplicateOwnerCount--", contractsSource);
            StringAssert.Contains("snapshot.Flags |= (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner;", contractsSource);
            StringAssert.Contains("public static void Reset()", contractsSource);
            StringAssert.Contains("s_untrackedDuplicateOwnerCount = 0u;", contractsSource);
            StringAssert.Contains("TryGetActiveSnapshot", contractsSource);
            StringAssert.Contains("TryApplySavedCalibration", contractsSource);
            StringAssert.Contains("authoringSeed = WorldWaterLevelCalibrationMath.DefaultAuthoringSeed;", source);
            StringAssert.Contains("IWorldWaterLevelCalibrationWriteModel", source);
            StringAssert.Contains("public bool TryApplyWaterLevelCalibration(float waterLevelY, float calibrationTravelMeters, uint sourceHash)", source);
            StringAssert.Contains("sourceHash != localSourceHash", source);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.Register(this);", source);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.Unregister(this);", source);
            StringAssert.Contains("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)", source);
            StringAssert.Contains("private global::Crest.OceanRenderer oceanRenderer;", source);
            StringAssert.Contains("oceanRenderer.Root", resolveRoot);
            StringAssert.Contains("targetRoot.position = new Vector3(rootPosition.x, resolvedWaterLevelY, rootPosition.z);", apply);
            StringAssert.Contains("AppliedToCrestRoot", apply);
            StringAssert.Contains("UsedFallback", apply);
            StringAssert.DoesNotContain("global::Crest", contractsSource);
            StringAssert.DoesNotContain("using UnityEngine", contractsSource);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", contractsSource);
            StringAssert.DoesNotContain("evaluate_height", source);
            StringAssert.DoesNotContain("evaluate_height", contractsSource);
            StringAssert.DoesNotContain("heightmap", source);
            StringAssert.DoesNotContain("heightmap", contractsSource);
            StringAssert.DoesNotContain("TerrainData", source);
            StringAssert.DoesNotContain("TerrainData", contractsSource);
            StringAssert.DoesNotContain("bool ", ExtractStructBody(contractsSource, "public struct WorldWaterLevelCalibrationDTO"));
            StringAssert.Contains("math.clamp(", mathSource);
        }

        [Test]
        public void WorldWaterLevelRuntimeRegistryClearsDuplicateOwnerStateAcrossLifecycle()
        {
            WorldWaterLevelCalibrationRuntimeRegistry.Reset();
            try
            {
                TestWaterLevelReadModel primary = new TestWaterLevelReadModel(42f);
                TestWaterLevelReadModel secondary = new TestWaterLevelReadModel(0f);

                WorldWaterLevelCalibrationRuntimeRegistry.Register(primary);
                WorldWaterLevelCalibrationRuntimeRegistry.Register(primary);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.DuplicateOwnerCount, Is.EqualTo(0u));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out WorldWaterLevelCalibrationDTO snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(42f).Within(0.0001f));
                Assert.That((snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner), Is.EqualTo(0u));

                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryApplySavedCalibration(12.5f, 1000f, 1u), Is.True);
                Assert.That(primary.ApplyCount, Is.EqualTo(1));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(12.5f).Within(0.0001f));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryApplySavedCalibration(18f, 1000f, 99u), Is.False);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(12.5f).Within(0.0001f));

                WorldWaterLevelCalibrationRuntimeRegistry.Register(secondary);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.DuplicateOwnerCount, Is.EqualTo(1u));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(12.5f).Within(0.0001f));
                Assert.That((snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner), Is.Not.EqualTo(0u));

                WorldWaterLevelCalibrationRuntimeRegistry.Unregister(secondary);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.DuplicateOwnerCount, Is.EqualTo(0u));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(12.5f).Within(0.0001f));
                Assert.That((snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner), Is.EqualTo(0u));

                WorldWaterLevelCalibrationRuntimeRegistry.Register(secondary);
                WorldWaterLevelCalibrationRuntimeRegistry.Unregister(primary);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.DuplicateOwnerCount, Is.EqualTo(0u));
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.True);
                Assert.That(snapshot.ResolvedWaterLevelY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That((snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner), Is.EqualTo(0u));

                WorldWaterLevelCalibrationRuntimeRegistry.Unregister(secondary);
                Assert.That(WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot), Is.False);
            }
            finally
            {
                WorldWaterLevelCalibrationRuntimeRegistry.Reset();
            }
        }

        [Test]
        public void UnderwaterVisuals_ResolveWaterLevelReadsPlayerAndCrestBeforeAtmosphereTerrainFluidFallbacks()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string cacheRuntime = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string coldResolve = ExtractMethodBody(source, "private void ResolveRuntimeServiceCachesOnColdCadence()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterLevel()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("private IHectonOceanKinematics _oceanKinematicsProvider;", source);
            StringAssert.Contains("CacheOceanKinematicsRuntimeCold();", cacheRuntime);
            StringAssert.Contains("CacheOceanKinematicsRuntimeCold();", coldResolve);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", serviceReplaced);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", serviceReplaced);
            StringAssert.Contains("ReadCachedOceanKinematicsProvider()", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            AssertTextBefore(resolveWater, "_playerMovement.CurrentWaterSurfaceY", "ReadCachedOceanKinematicsProvider()");
            AssertTextBefore(resolveWater, "ReadCachedOceanKinematicsProvider()", "atmosphereManager.SeaLevelY");
            AssertTextBefore(resolveWater, "atmosphereManager.SeaLevelY", "terrainRuntime.WaterSurfaceLevel");
            AssertTextBefore(resolveWater, "terrainRuntime.WaterSurfaceLevel", "_physicsEngine.WaterLevel");
        }

        [Test]
        public void OceanKinematicsRuntimeService_HotSwapConflictMarkerIsResolvedAndAbortGuardRemains()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "OceanKinematicsRuntimeService.cs");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.DoesNotContain("<<<<<<<", source);
            StringAssert.DoesNotContain("=======", source);
            StringAssert.DoesNotContain(">>>>>>>", source);
            StringAssert.Contains("if (_runtimeOwnerAborted)", serviceReplaced);
            StringAssert.Contains("if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", serviceReplaced);
            AssertTextBefore(serviceReplaced, "if (_runtimeOwnerAborted)", "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)");
        }

        [Test]
        public void CrestProviderSeaLevelRemainsCrestRootSourceForMovableCalibration()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Crest4KinematicsAdapter.cs");
            string seaLevel = ExtractMethodBody(source, "private static float ResolveSeaLevel(global::Crest.OceanRenderer oceanRenderer)");
            string tuning = ExtractMethodBody(source, "public bool TryBuildBurstTuning(");

            StringAssert.Contains("OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(oceanRenderer.Root.position.y)", seaLevel);
            StringAssert.Contains("tuning.OceanSurfaceY = ResolveSeaLevel(oceanRenderer);", tuning);
            StringAssert.DoesNotContain("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(oceanRenderer.Root.position.y)", seaLevel);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
        }

        [Test]
        public void WaterLevelCalibration_BoundaryAndInstallerUseCrestBridgeRoute()
        {
            Assert.That(
                ProjectFileExists("Assets", "_Project", "Scripts", "World", "WorldWaterLevelCalibrationAuthoring.cs"),
                Is.False);

            string coreAsmdef = ReadProjectFile("Assets", "_Project", "Scripts", "Hecton8.Core.asmdef");
            string crestBridgeAsmdef = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Hecton8.Crest.Bridge.asmdef");
            string crestEditorAsmdef = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Editor",
                "Hecton8.Crest.Bridge.Editor.asmdef");
            string installer = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Editor",
                "CrestWorldWaterLevelCalibrationInstaller.cs");
            string oceanPrefab = ReadProjectFile(
                "Assets",
                "_Project",
                "Prefabs",
                "Ocean_Crest.prefab");
            string oceanPrefabMeta = ReadProjectFile(
                "Assets",
                "_Project",
                "Prefabs",
                "Ocean_Crest.prefab.meta");
            string worldScene = ReadProjectFile(
                "Assets",
                "_Project",
                "Scenes",
                "02_HECTON_WORLD.unity");

            StringAssert.DoesNotContain("\"Crest\"", coreAsmdef);
            StringAssert.Contains("\"Crest\"", crestBridgeAsmdef);
            StringAssert.Contains("\"Hecton8.World.Contracts\"", crestBridgeAsmdef);
            StringAssert.Contains("\"Hecton8.World.Contracts\"", crestEditorAsmdef);
            StringAssert.Contains("PrefabUtility.LoadPrefabContents", installer);
            StringAssert.Contains("PrefabUtility.SaveAsPrefabAsset", installer);
            StringAssert.Contains("PrefabUtility.InstantiatePrefab", installer);
            StringAssert.Contains("EditorSceneManager.OpenScene", installer);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.DefaultAuthoringSeed", installer);
            StringAssert.Contains("VerifyPrefabCalibration", installer);
            StringAssert.DoesNotContain("File.WriteAllText", installer);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", installer);
            StringAssert.DoesNotContain("TerrainData", installer);
            StringAssert.Contains("m_EditorClassIdentifier: Crest::Crest.OceanRenderer", oceanPrefab);
            StringAssert.Contains("m_EditorClassIdentifier: Assembly-CSharp::Hecton8.World.WorldWaterLevelCalibrationAuthoring", oceanPrefab);
            StringAssert.Contains("authoringSeed: 880031", oceanPrefab);
            StringAssert.Contains("calibratedWaterLevelY: 14.02", oceanPrefab);
            StringAssert.Contains("fallbackWaterLevelY: 14.02", oceanPrefab);
            StringAssert.Contains("calibrationTravelMeters: 1000", oceanPrefab);
            StringAssert.Contains("oceanRenderer: {fileID: 1881482921440711839}", oceanPrefab);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", oceanPrefab);
            StringAssert.DoesNotContain("TerrainData", oceanPrefab);
            StringAssert.Contains("guid: 0a7f97b6028cb014e80782578e9bf734", oceanPrefabMeta);
            StringAssert.Contains("m_SourcePrefab: {fileID: 100100000, guid: 0a7f97b6028cb014e80782578e9bf734, type: 3}", worldScene);
        }

        [Test]
        public void FloraWaterLevelShaderGlobalsPreferCrestBeforeTerrainFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "FloraInteractionManager.cs");
            string resolveWater = ExtractMethodBody(source, "private float ResolveVegetationWaterLevel(");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TryResolveOceanVegetationWaterLevel(");

            StringAssert.Contains("_VegetationWaterLevelId", source);
            StringAssert.Contains("_oceanKinematicsProvider.SeaLevel", resolveWater);
            StringAssert.Contains("mapMagicRuntime.WaterSurfaceLevel", resolveWater);
            StringAssert.Contains("fluidReadModel.WaterLevel", resolveWater);
            AssertTextBefore(resolveWater, "_oceanKinematicsProvider.SeaLevel", "mapMagicRuntime.WaterSurfaceLevel");
            AssertTextBefore(resolveWater, "mapMagicRuntime.WaterSurfaceLevel", "fluidReadModel.WaterLevel");
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterLevel) > 0.0001f", sanitizeOcean);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
        }

        [Test]
        public void SpatialAudioVirtualDepthUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "SpatialAudioManager.cs");
            string coldCache = ExtractMethodBody(source, "private void RefreshCachedAudioRuntimeServicesCold()");
            string rebound = ExtractMethodBody(source, "private void CacheReboundAudioRuntimeService(");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveVirtualListenerDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveAudioWaterLevelY()");
            string sanitizeWater = ExtractMethodBody(source, "private static bool TryResolveAudioWaterLevel(");

            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.DoesNotContain("_cachedOceanKinematicsProvider", source);
            StringAssert.Contains("IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_cachedOceanKinematicsService = oceanKinematicsService != null && oceanKinematicsService.IsInitialized ? oceanKinematicsService : null;", coldCache);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", shutdown);
            StringAssert.Contains("float seaLevelY = ResolveAudioWaterLevelY();", resolveDepth);
            StringAssert.Contains("provider.SeaLevel", resolveWater);
            StringAssert.Contains("provider.IsAvailable", resolveWater);
            StringAssert.Contains("service.IsInitialized", resolveWater);
            StringAssert.Contains("service.ActiveProvider", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "service.ActiveProvider", "return DefaultSeaLevelY;");
            AssertTextBefore(resolveWater, "provider.IsAvailable", "provider.SeaLevel");
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeWater);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", sanitizeWater);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void GameplayWaterDepthReadersPreferCrestBeforeTerrainFallback()
        {
            string resources = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "ResourceDistributionDirector.cs");
            string ecosystem = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "EcosystemDirector.cs");

            AssertGameplayWaterReader(resources, "private void CacheRegistryServicesCold()", "private void ClearCachedRegistryServices()");
            AssertGameplayWaterReader(ecosystem, "private void CacheColdRegistryReferences()", "private void ShutdownServiceState()");
            AssertSubmarineBallastWaterReader();
            AssertHabitatIntegrityWaterReader();
        }

        [Test]
        public void PDAMapFallbackDepthUsesCrestWaterLevelBeforeHardcodedDefault()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs");
            string coldCache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(source, "private void ClearCachedServices()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");
            string resolveWater = ExtractMethodBody(source, "private double ResolveFallbackSeaLevelY()");
            string sanitizeWater = ExtractMethodBody(source, "private static bool TryResolveSeaLevelY(");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("private const double DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = null;", clear);
            StringAssert.Contains("double seaLevelY = ResolveFallbackSeaLevelY();", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeWater);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", sanitizeWater);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void DamageAndRadiationDepthTelemetryUseCrestWaterLevelBeforeHardcodedFallback()
        {
            AssertDamageRouterWaterDepthReader();
            AssertRadiationTelemetryWaterDepthReader();
        }

        [Test]
        public void SolarPanelOpticalDepthUsesCrestWaterLevelBeforeAuthoredFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "SolarPanel.cs");
            string reboundRef = ExtractMethodBody(source, "public void OnGlobalRegistryServiceRebound(");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string register = ExtractMethodBody(source, "private void RegisterLeaderLanes()");
            string unregister = ExtractMethodBody(source, "private void UnregisterLeaderLanes()");
            string buildConditions = ExtractMethodBody(source, "private SolarConditionsDTO BuildConditions(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveSolarSeaLevelY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", reboundRef);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", reboundRef);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", register);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", unregister);
            StringAssert.Contains("float baseSeaLevelY = ResolveSolarSeaLevelY();", buildConditions);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(seaLevelRuntimeY)", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "seaLevelRuntimeY");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void RandomEventMeteorWaterImpactsUseCrestWaterLevelBeforeCinematicFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "RandomEventSystem.cs");
            string coldCache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string waterImpact = ExtractMethodBody(source, "private void TryPublishMeteorWaterImpact(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveCurrentSeaLevelY()");

            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", onDisable);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", onDestroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("float seaLevelY = ResolveCurrentSeaLevelY();", waterImpact);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return MeteorWaterPlaneY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return MeteorWaterPlaneY;");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void SaveIdentityPersistsWaterCalibrationSnapshotAndSanitizesLoad()
        {
            string saveData = ReadProjectFile("Assets", "_Project", "Scripts", "SaveData.cs");
            string saveManager = ReadProjectFile("Assets", "_Project", "Scripts", "SaveManager.cs");
            string codec = ReadProjectFile("Assets", "_Project", "Scripts", "SaveBinaryPayloadCodec.cs");
            string migration = ReadProjectFile("Assets", "_Project", "Scripts", "SaveDataMigration.cs");

            string savePopulate = ExtractMethodBody(saveManager, "private static void StampProceduralTerrainIdentity(");
            string saveValidate = ExtractMethodBody(saveManager, "private static void ValidateProceduralTerrainIdentity(");
            string saveRestore = ExtractMethodBody(saveManager, "private static bool TryRestoreWaterCalibrationFromSave(");
            string activeWater = ExtractMethodBody(saveManager, "private static bool TryResolveActiveWaterCalibration(");
            string codecSanitize = ExtractMethodBody(codec, "private static ProceduralTerrainIdentityDTO SanitizeProceduralTerrainIdentity(");
            string migrationSanitize = ExtractMethodBody(migration, "private static bool EnsureProceduralTerrainIdentity(");

            StringAssert.Contains("public const uint FlagsWaterCalibrationPresent = 1u << 1;", saveData);
            StringAssert.Contains("public float selectedWaterLevelY;", saveData);
            StringAssert.Contains("public float waterCalibrationTravelMeters;", saveData);
            StringAssert.Contains("public uint waterCalibrationSourceHash;", saveData);
            StringAssert.Contains("public uint terrainProviderFlags;", saveData);
            StringAssert.Contains("public int heightCacheRevision;", saveData);
            StringAssert.Contains("public uint terrainEntityHash;", saveData);
            StringAssert.Contains("public uint surfaceMaterialContractVersion;", saveData);
            StringAssert.Contains("public uint mesoDetailContractVersion;", saveData);
            StringAssert.Contains("public uint detailEligibilityContractVersion;", saveData);
            StringAssert.Contains("public uint mesoParamsHash;", saveData);
            StringAssert.Contains("TryResolveActiveWaterCalibration(out WorldWaterLevelCalibrationDTO waterSnapshot)", savePopulate);
            StringAssert.Contains("identity.selectedWaterLevelY = waterSnapshot.ResolvedWaterLevelY;", savePopulate);
            StringAssert.Contains("identity.waterCalibrationTravelMeters = waterSnapshot.CalibrationTravelMeters;", savePopulate);
            StringAssert.Contains("identity.waterCalibrationSourceHash = waterSnapshot.SourceHash;", savePopulate);
            StringAssert.Contains("identity.terrainProviderFlags = terrainIdentity.Flags;", savePopulate);
            StringAssert.Contains("identity.heightCacheRevision = math.max(0, terrainIdentity.CacheRevision);", savePopulate);
            StringAssert.Contains("identity.terrainEntityHash = terrainIdentity.TerrainEntityHash;", savePopulate);
            StringAssert.Contains("identity.surfaceMaterialContractVersion = WorldTerrainSurfaceMaterialResolver.ContractVersion;", savePopulate);
            StringAssert.Contains("identity.mesoDetailContractVersion = WorldTerrainMesoDetailFields.ContractVersion;", savePopulate);
            StringAssert.Contains("identity.detailEligibilityContractVersion = WorldTerrainDetailContracts.ContractVersion;", savePopulate);
            StringAssert.Contains("BuildTerrainMesoParamsHash(", savePopulate);
            StringAssert.Contains("FlagsWaterCalibrationPresent", savePopulate);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out snapshot)", activeWater);
            StringAssert.Contains("TryRestoreWaterCalibrationFromSave(data);", saveManager);
            AssertTextBefore(saveManager, "TryRestoreWaterCalibrationFromSave(data);", "ValidateProceduralTerrainIdentity(data);");
            StringAssert.Contains("FlagsWaterCalibrationPresent", saveRestore);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.TryResolveWaterLevelY", saveRestore);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot", saveRestore);
            StringAssert.Contains("activeSnapshot.SourceHash != saved.waterCalibrationSourceHash", saveRestore);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.TryApplySavedCalibration", saveRestore);
            StringAssert.Contains("hasSavedWaterCalibration", saveValidate);
            StringAssert.Contains("waterMismatch = true;", saveValidate);
            StringAssert.Contains("waterSnapshot.SourceHash != saved.waterCalibrationSourceHash", saveValidate);
            StringAssert.Contains("waterSnapshot.ResolvedWaterLevelY - saved.selectedWaterLevelY", saveValidate);
            StringAssert.Contains("saved.surfaceMaterialContractVersion != WorldTerrainSurfaceMaterialResolver.ContractVersion", saveValidate);
            StringAssert.Contains("saved.mesoDetailContractVersion != WorldTerrainMesoDetailFields.ContractVersion", saveValidate);
            StringAssert.Contains("saved.detailEligibilityContractVersion != WorldTerrainDetailContracts.ContractVersion", saveValidate);
            StringAssert.Contains("saved.mesoParamsHash != expectedMesoParamsHash", saveValidate);
            StringAssert.Contains("saved.heightCacheRevision != expected.CacheRevision", saveValidate);
            StringAssert.Contains("saved.terrainEntityHash != expected.TerrainEntityHash", saveValidate);
            AssertSaveWaterSanitizer(codecSanitize);
            AssertSaveWaterSanitizer(migrationSanitize);
            StringAssert.Contains("FlagsTerrainHeightPayloadPresent", codecSanitize);
            StringAssert.Contains("FlagsTerrainMesoContractsPresent", codecSanitize);
            StringAssert.Contains("FlagsTerrainHeightPayloadPresent", migrationSanitize);
            StringAssert.Contains("FlagsTerrainMesoContractsPresent", migrationSanitize);
        }

        [Test]
        public void VisorDepthFallbacksReadPublishedUnderwaterWaterLevelGlobal()
        {
            string underwater = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");

            StringAssert.Contains("_HectonNoirFogStratification", underwater);
            StringAssert.Contains("Shader.SetGlobalVector(_HectonNoirFogStratificationId", underwater);
            StringAssert.Contains("new Vector4(waterLevel,", underwater);
            StringAssert.Contains("HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionRuntime", underwater);
            StringAssert.Contains("new Vector4(waterLevel, 1f, 1f, 0f)", underwater);
            StringAssert.DoesNotContain("new Vector4(0f, 1f, 1f, 0f)", underwater);

            AssertVisorWaterGlobalReader(
                ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorUberPostFeature.cs"),
                "private static float ResolveCameraDepthFromProductionSeaLevel(Camera renderCamera)");
            AssertVisorWaterGlobalReader(
                ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonNoirDepthFogFeature.cs"),
                "private static float ResolveCameraDepthFromProductionSeaLevel(Camera renderCamera)");
            AssertVisorWaterGlobalReader(
                ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVolumetricParticulateFogFeature.cs"),
                "private static float ResolveCameraDepthFromProductionSeaLevel(float3 cameraPosition)");
        }

        [Test]
        public void ShaderGlobalWaterExtinctionFallbackUsesCalibrationWaterLevelInsteadOfZeroPlane()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "HectonShaderGlobalDataVaultBridge.cs");
            string resetStatic = ExtractMethodBody(source, "private static void ResetStaticState()");
            string publishRuntime = ExtractMethodBody(source, "public static void PublishWaterExtinctionRuntime(");
            string resetGlobals = ExtractMethodBody(source, "public static void ResetWaterExtinctionGlobals()");
            string analyticalFallback = ExtractMethodBody(source, "public static void PublishWaterExtinctionAnalyticalFallback()");
            string resolveSurface = ExtractMethodBody(source, "private static float ResolveWaterExtinctionSurfaceY(");
            string resolveFallback = ExtractMethodBody(source, "private static float ResolveFallbackWaterExtinctionSurfaceY()");

            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("CreateFloat4(WorldWaterLevelCalibrationMath.DefaultWaterLevelY, 1f, 1f, 0f)", resetStatic);
            StringAssert.Contains("value.x = ResolveWaterExtinctionSurfaceY(runtimeVector.x);", publishRuntime);
            StringAssert.Contains("math.abs(requestedSurfaceY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", resolveSurface);
            StringAssert.Contains("ResolveFallbackWaterExtinctionSurfaceY()", resetGlobals);
            StringAssert.Contains("ResolveFallbackWaterExtinctionSurfaceY()", analyticalFallback);
            StringAssert.Contains("WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out WorldWaterLevelCalibrationDTO snapshot)", resolveFallback);
            StringAssert.Contains("snapshot.ResolvedWaterLevelY", resolveFallback);
            StringAssert.Contains("return WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", resolveFallback);
            StringAssert.DoesNotContain("CreateVector4(0f, 1f, 1f, 0f)", resetGlobals);
            StringAssert.DoesNotContain("CreateVector4(0f, 1f, 1f, 1f)", analyticalFallback);
        }

        [Test]
        public void OceanSinglePassRuntimePublishesCrestWaterLevelBeforeTerrainFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "OceanSinglePass",
                "OceanSinglePassRuntime.cs");
            string coldCache = ExtractMethodBody(source, "private void CacheColdServices()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownOwnedState()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterSurfaceAupY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterSurfaceAupY(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", onDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", shutdown);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("TryResolveOceanWaterSurfaceAupY", resolveWater);
            StringAssert.Contains("terrainProvider.WaterSurfaceLevel", resolveWater);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            AssertTextBefore(resolveWater, "TryResolveOceanWaterSurfaceAupY", "terrainProvider.WaterSurfaceLevel");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void WaterOpticsRuntimeUsesCrestSurfaceForAbsorptionScatteringDepthLimits()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "WaterOptics",
                "WaterOpticsRuntime.cs");
            string onShutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveLocal = ExtractMethodBody(source, "private float ResolveLocalSurfaceY()");
            string resolveSurface = ExtractMethodBody(source, "private float ResolveOceanSurfaceWorldY()");
            string buildDto = ExtractMethodBody(source, "private static WaterOpticsDTO BuildWaterOpticsDto(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", source);
            StringAssert.Contains("_oceanKinematicsService = null;", onShutdown);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("float surfaceWorldY = ResolveOceanSurfaceWorldY();", resolveLocal);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveSurface);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveSurface);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveSurface);
            StringAssert.Contains("SanitizeCrestOceanSurfaceWorldY(oceanKinematics.SeaLevel)", resolveSurface);
            StringAssert.Contains("SanitizeOceanSurfaceWorldY(_oceanSurfaceWorldY)", resolveSurface);
            StringAssert.Contains("math.isfinite(localSurfaceY) ? localSurfaceY : DefaultOceanSurfaceWorldY", buildDto);
            AssertTextBefore(resolveSurface, "oceanKinematics.SeaLevel", "_oceanSurfaceWorldY");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveSurface);
            StringAssert.DoesNotContain("TerrainData", resolveSurface);
        }

        [Test]
        public void WorldChunkResidencyBiomeDepthUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "WorldChunkResidencyManager.cs");
            string coldCache = ExtractMethodBody(source, "private void RefreshColdServiceCache()");
            string clear = ExtractMethodBody(source, "private void ClearColdServiceCache()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string selectBiome = ExtractMethodBody(source, "private unsafe bool TrySelectBiomeRecordForChunk(");
            string resolveDepth = ExtractMethodBody(source, "private double ResolveChunkDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveChunkSeaLevelY()");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("double depthMeters = ResolveChunkDepthMeters(in centerAup);", selectBiome);
            StringAssert.Contains("ResolveChunkSeaLevelY() - centerY", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("DefaultSeaLevelY - centerY", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void AbyssalThermalDamagePacketsUseCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "AbyssalThermalManager.cs");
            string coldCache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string fallbackDamage = ExtractMethodBody(source, "private void ApplyThermalOwnerFallbackDamage(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveDamageDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveDamageSeaLevelY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", onDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", onDestroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("Depth = ResolveDamageDepthMeters(positionWS)", fallbackDamage);
            StringAssert.Contains("ResolveDamageSeaLevelY() - positionWS.y", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("DefaultSeaLevelY - positionWS.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void SargassumPresentationAndCutMaskUseCrestWaterLevelBeforeTerrainFallback()
        {
            string boids = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "SargassumMicroFaunaBoids.cs");
            string boidsCold = ExtractMethodBody(boids, "private void RefreshColdRegistryDependencies()");
            string boidsDisable = ExtractMethodBody(boids, "private void OnDisable()");
            string boidsDestroy = ExtractMethodBody(boids, "private void OnDestroy()");
            string boidsRebound = ExtractMethodBody(boids, "public void OnGlobalRegistryServiceReplaced(");
            string syncWater = ExtractMethodBody(boids, "private void SyncWaterSurfaceLevelFromRuntime()");
            string resolveBoidsOcean = ExtractMethodBody(boids, "private bool TryResolveOceanWaterLevel(");
            string sanitizeBoidsOcean = ExtractMethodBody(boids, "private static bool TryResolveOceanWaterLevel(");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", boids);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", boidsCold);
            StringAssert.Contains("_oceanKinematicsService = null;", boidsDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", boidsDestroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", boidsRebound);
            StringAssert.Contains("SyncWaterSurfaceLevelFromRuntime();", boidsRebound);
            StringAssert.Contains("TryResolveOceanWaterLevel", syncWater);
            StringAssert.Contains("bridge.WaterSurfaceLevel", syncWater);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveBoidsOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveBoidsOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveBoidsOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveBoidsOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeBoidsOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterLevel) > 0.0001f", sanitizeBoidsOcean);
            AssertTextBefore(syncWater, "TryResolveOceanWaterLevel", "bridge.WaterSurfaceLevel");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", syncWater);
            StringAssert.DoesNotContain("TerrainData", syncWater);

            string cut = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "SargassumCutManager.cs");
            string cutCold = ExtractMethodBody(cut, "private void CacheRegistryServicesCold()");
            string cutDisable = ExtractMethodBody(cut, "private void OnDisable()");
            string cutDestroy = ExtractMethodBody(cut, "private void OnDestroy()");
            string cutRebound = ExtractMethodBody(cut, "public void OnGlobalRegistryServiceReplaced(");
            string resolveMask = ExtractMethodBody(cut, "private float ResolveMaskWaterLevel(");
            string resolveCutOcean = ExtractMethodBody(cut, "private bool TryResolveOceanWaterLevel(");
            string sanitizeCutOcean = ExtractMethodBody(cut, "private static bool TryResolveOceanWaterLevel(");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", cut);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", cutCold);
            StringAssert.Contains("_oceanKinematicsService = null;", cutDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", cutDestroy);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", cutRebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", cutRebound);
            StringAssert.Contains("TryResolveOceanWaterLevel", resolveMask);
            StringAssert.Contains("terrainBridge.WaterSurfaceLevel", resolveMask);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveCutOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveCutOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveCutOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveCutOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeCutOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterLevel) > 0.0001f", sanitizeCutOcean);
            AssertTextBefore(resolveMask, "TryResolveOceanWaterLevel", "terrainBridge.WaterSurfaceLevel");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveMask);
            StringAssert.DoesNotContain("TerrainData", resolveMask);
        }

        [Test]
        public void MapMagicVegetationBridgeStreamingUsesCrestWaterLevelBeforeTerrainFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "HectonMapMagicVegetationBridge.cs");
            string tick = ExtractMethodBody(source, "public void Tick(float dt)");
            string coldRefresh = ExtractMethodBody(source, "private void RefreshColdRuntimeDependencies()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");
            string syncRuntime = ExtractMethodBody(source, "private void SyncWaterSurfaceLevelFromRuntime()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterLevel(");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TryResolveOceanWaterLevel(");
            string syncTerrain = ExtractMethodBody(source, "private void SyncWaterSurfaceLevelFromTerrainBridge()");
            string applyLevel = ExtractMethodBody(source, "private void ApplyResolvedWaterLevel(");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("SyncWaterSurfaceLevelFromRuntime();", tick);
            StringAssert.Contains("CacheOceanKinematicsService(GlobalRegistry.OceanKinematics);", coldRefresh);
            StringAssert.Contains("CacheOceanKinematicsService(null);", disable);
            StringAssert.Contains("CacheOceanKinematicsService(null);", destroy);
            StringAssert.Contains("TryResolveOceanWaterLevel", syncRuntime);
            StringAssert.Contains("SyncWaterSurfaceLevelFromTerrainBridge", syncRuntime);
            AssertTextBefore(syncRuntime, "TryResolveOceanWaterLevel", "SyncWaterSurfaceLevelFromTerrainBridge");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterLevel) > 0.0001f", sanitizeOcean);
            StringAssert.Contains("bridge.WaterSurfaceLevel", syncTerrain);
            StringAssert.Contains("ApplyResolvedWaterLevel(resolvedWaterLevel);", syncTerrain);
            StringAssert.Contains("WaterLevelResyncEpsilonMeters", applyLevel);
            StringAssert.Contains("CancelAllChunkBuildJobs();", applyLevel);
            StringAssert.Contains("ClearChunkPayloadCache();", applyLevel);
            StringAssert.Contains("_activeSetDirty = true;", applyLevel);
            StringAssert.Contains("_activeBufferRebuildRequested = true;", applyLevel);
            StringAssert.Contains("_startupResidencyPending = true;", applyLevel);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("CacheOceanKinematicsService(currentService as IHectonOceanKinematicsService);", rebound);
            AssertTextBefore(rebound, "case GlobalRegistryServiceSlot.OceanKinematics:", "case GlobalRegistryServiceSlot.MapMagicRuntime:");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", syncRuntime);
            StringAssert.DoesNotContain("TerrainData", syncRuntime);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", applyLevel);
            StringAssert.DoesNotContain("TerrainData", applyLevel);
        }

        [Test]
        public void BiolumDepthDarknessUsesCrestWaterLevelSnapshotBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "VFX",
                "Bioluminescence",
                "BiolumPulseSyncRuntime.cs");
            string refresh = ExtractMethodBody(source, "private void RefreshCachedRegistryServices()");
            string rebound = ExtractMethodBody(source, "private void ApplyBiolumRegistryServiceRebind(");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string dispose = ExtractMethodBody(source, "public void Dispose()");
            string mockLighting = ExtractMethodBody(source, "private BiolumPulseStateDTO BuildMockLightingPulseState(");
            string schedule = ExtractMethodBody(source, "private void ScheduleStateJob(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveBiolumSeaLevelAupY()");
            string sanitizeWater = ExtractMethodBody(source, "private static bool TryResolveSeaLevelAupY(");
            string resolveDarkness = ExtractMethodBody(source, "private static float ResolveDarknessScalar(");
            string job = ExtractStructBody(source, "private unsafe struct AdvanceBiolumPhasesJob");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", refresh);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_oceanKinematicsService = null;", onDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", dispose);
            StringAssert.Contains("ResolveBiolumSeaLevelAupY()", mockLighting);
            StringAssert.Contains("SeaLevelAupY = ResolveBiolumSeaLevelAupY()", schedule);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelAupY;", resolveWater);
            StringAssert.Contains("private static float ResolveAupDepthMeters(double3 aupReference, double seaLevelAupY)", source);
            StringAssert.Contains("seaLevelAupY - aupReference.y", source);
            StringAssert.Contains("double seaLevelAupY", resolveDarkness);
            StringAssert.Contains("public double SeaLevelAupY;", job);
            StringAssert.Contains("ResolveDarknessScalar(weather, DarknessActivationThreshold, AupReference, SeaLevelAupY)", job);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelAupY;");
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeWater);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", sanitizeWater);
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - aupReference.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void AtlasSignalRevealDepthUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "AtlasSignal",
                "AtlasSignalSystem.cs");
            string cache = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string clear = ExtractMethodBody(source, "private void ClearRuntimeDependencies()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveCurrentSeaLevelAupY()");
            string sanitizeWater = ExtractMethodBody(source, "private static bool TryResolveSeaLevelAupY(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = Hecton8.Core.GlobalRegistry.OceanKinematics;", cache);
            StringAssert.Contains("_oceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveCurrentSeaLevelAupY() - absoluteY", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelAupY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelAupY;");
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeWater);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", sanitizeWater);
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - absoluteY", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void StructuralIntegrityPressureUsesCrestWaterLevelBeforeAuthoredFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Habitat",
                "Deformation",
                "Runtime",
                "StructuralIntegrityCalculatorRuntime.cs");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string tick = ExtractMethodBody(source, "public void Tick(float deltaTime)");
            string defaultTuning = ExtractMethodBody(source, "private void WriteDefaultTuning(NativeArray<StructuralTuningDTO> tuning)");
            string mockStress = ExtractMethodBody(source, "private bool GenerateEmergencyMockStressData()");
            string resolveRuntime = ExtractMethodBody(source, "private double3 ResolveRuntimeSeaLevelAup()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelAupY(");
            string refresh = ExtractMethodBody(source, "private void RefreshRuntimeSeaLevelTuningIfChanged()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", cache);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("RefreshRuntimeSeaLevelTuningIfChanged();", rebound);
            StringAssert.Contains("RefreshRuntimeSeaLevelTuningIfChanged();", tick);
            AssertTextBefore(tick, "RefreshRuntimeSeaLevelTuningIfChanged();", "ResolveSimulationQualityWeight()");
            StringAssert.Contains("double3 runtimeSeaLevelAup = ResolveRuntimeSeaLevelAup();", defaultTuning);
            StringAssert.Contains("SeaLevelAup = ResolveSeaLevelAup(seaLevelAup)", defaultTuning);
            StringAssert.Contains("sanitized.SeaLevelAup = runtimeSeaLevelAup;", defaultTuning);
            StringAssert.Contains("SeaLevelAup = ResolveRuntimeSeaLevelAup()", mockStress);
            StringAssert.Contains("current.SeaLevelAup = ResolveRuntimeSeaLevelAup();", source);
            StringAssert.Contains("TryResolveOceanSeaLevelAupY", resolveRuntime);
            StringAssert.Contains("ResolveSeaLevelAup(seaLevelAup)", resolveRuntime);
            StringAssert.Contains("return resolved;", resolveRuntime);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            StringAssert.Contains("_jobScheduled != 0", refresh);
            StringAssert.Contains("TryAcquireStructuralMutationGuard()", refresh);
            StringAssert.Contains("updated.SeaLevelAup = resolvedSeaLevelAup;", refresh);
            AssertTextBefore(resolveRuntime, "ResolveSeaLevelAup(seaLevelAup)", "TryResolveOceanSeaLevelAupY");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveRuntime);
            StringAssert.DoesNotContain("TerrainData", resolveRuntime);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void SubmarineFluidDynamicsExternalPressureDepthUsesCrestWaterLevelBeforeAtmosphereFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "SubmarineFluidDynamics.cs");
            string clear = ExtractMethodBody(source, "private void ClearRuntimeServiceCaches()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveExternalDepthMeters()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");
            string validateSeaLevel = ExtractMethodBody(source, "private static bool TryResolveExternalSeaLevelY(");

            StringAssert.Contains("private IHectonOceanKinematics _oceanKinematics;", source);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematics = oceanKinematicsService != null ? oceanKinematicsService.ActiveProvider : null;", rebound);
            StringAssert.Contains("_oceanKinematics = null;", clear);
            StringAssert.Contains("TryResolveOceanSeaLevelY", resolveDepth);
            StringAssert.Contains("atmosphereManager.SeaLevelY", resolveDepth);
            AssertTextBefore(resolveDepth, "TryResolveOceanSeaLevelY", "atmosphereManager.SeaLevelY");
            StringAssert.Contains("ResolveOceanKinematicsProvider()", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", validateSeaLevel);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", validateSeaLevel);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveDepth);
            StringAssert.DoesNotContain("TerrainData", resolveDepth);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void CelestialDeepCullDepthFallbackUsesCrestWaterLevelBeforeDefault()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "HectonCelestialEngine.cs");
            string cold = ExtractMethodBody(source, "private void RefreshColdRuntimeDependencies()");
            string clear = ExtractMethodBody(source, "private void ClearCelestialTruthReadCache()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveProductionDepthFromRuntimeY(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveProductionSeaLevelY()");

            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", cold);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveProductionSeaLevelY() - runtimeY", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("TryResolveOceanReactorSeaLevelAupY(oceanKinematics.SeaLevel, out double seaLevelAupY)", resolveWater);
            StringAssert.Contains("return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;");
            StringAssert.DoesNotContain("OceanSurfaceAtmosphereConstants.DefaultSeaLevel - runtimeY", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void SkyFollowSeaLevelLockUsesCrestWaterLevelBeforeAtmosphereFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "SkySystemFollowCamera.cs");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveSeaLevelY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", cache);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", disable);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("TryResolveOceanSeaLevelY", resolveWater);
            StringAssert.Contains("resolvedAtmosphere.SeaLevelY", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanSeaLevelY", "resolvedAtmosphere.SeaLevelY");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void GIRelayDepthPaletteUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Lighting",
                "HectonGIRelaySystem.cs");
            string refresh = ExtractMethodBody(source, "private void RefreshColdRuntimeDependencies()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownRuntime()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveDepthMetersAbsolute()");
            string resolveWater = ExtractMethodBody(source, "private double ResolveGIRelaySeaLevelAupY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", refresh);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", shutdown);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveGIRelaySeaLevelAupY() - absolute.y", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelAupY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelAupY;");
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - absolute.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void AppliedLoreWorldImpactDepthUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Data",
                "Monolith",
                "H8AppliedLoreRuntime.cs");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDepth01(");
            string resolveWater = ExtractMethodBody(source, "private static double ResolveCurrentSeaLevelY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("ResolveCurrentSeaLevelY() - absoluteY", resolveDepth);
            StringAssert.Contains("GlobalRegistry.OceanKinematics", resolveWater);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("DefaultSeaLevelY - absoluteY", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void ShinobuPhysiologyPressureUsesCrestWaterLevelBeforeSerializedFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physiology",
                "ShinobuPhysiologyRuntime.cs");
            string rebind = ExtractMethodBody(source, "private void RebindColdServices()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string environment = ExtractMethodBody(source, "private void WriteEnvironmentSeed(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveRuntimeSeaLevelAupY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematics;", source);
            StringAssert.Contains("_oceanKinematics = GlobalRegistry.OceanKinematics;", rebind);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematics = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveRuntimeSeaLevelAupY()", environment);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return ResolveSeaLevelAupY(seaLevelAupY);", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return ResolveSeaLevelAupY(seaLevelAupY);");
            StringAssert.DoesNotContain("ResolveSeaLevelAupY(seaLevelAupY);", environment);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void ShinobuSuitIntegrityPressureUsesCrestWaterLevelBeforeSerializedFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physiology",
                "ShinobuSuitIntegrityRuntime.cs");
            string rebind = ExtractMethodBody(source, "private void RebindColdServices()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string mock = ExtractMethodBody(source, "public bool GenerateMockHydrostaticPressureData()");
            string resolveWater = ExtractMethodBody(source, "private double ResolveRuntimeSeaLevelAupY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematics;", source);
            StringAssert.Contains("_oceanKinematics = GlobalRegistry.OceanKinematics;", rebind);
            StringAssert.Contains("_oceanKinematics = null;", disable);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematics = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveRuntimeSeaLevelAupY()", slowTick);
            StringAssert.Contains("ResolveRuntimeSeaLevelAupY()", mock);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return ResolveSeaLevelAupY(seaLevelAupY);", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return ResolveSeaLevelAupY(seaLevelAupY);");
            StringAssert.DoesNotContain("ResolveSeaLevelAupY(seaLevelAupY);", slowTick);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void VehicleComponentDamageDepthUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Vehicles",
                "VehicleComponentDamageRuntime.cs");
            string enable = ExtractMethodBody(source, "private void OnEnable()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string fixedTick = ExtractMethodBody(source, "public void FixedTick(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveVehicleSeaLevelAupY()");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDepthMeters(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.DoesNotContain("[SerializeField, Min(0f)] private float externalWaterlineRuntimeY", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", enable);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveDepthMeters(rootAup, ResolveVehicleSeaLevelAupY())", fixedTick);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelAupY;", resolveWater);
            StringAssert.Contains("seaLevelAupY - rootAup.y", resolveDepth);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelAupY;");
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - rootAup.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void PlayerBuilderIntegrityDepthPressureUsesCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "PlayerBuilder.cs");
            string bind = ExtractMethodBody(source, "private void BindRuntimeReferences()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "protected override void OnToolRegistryServiceReplaced(");
            string estimate = ExtractMethodBody(source, "private float EstimateDepthPressure(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveBuilderSeaLevelAupY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", source);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", bind);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", destroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_integrityValidationDirty = true;", rebound);
            StringAssert.Contains("RefreshActiveBuildReadiness();", rebound);
            StringAssert.Contains("ResolveBuilderSeaLevelAupY() - pivotAup.y", estimate);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelAupY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelAupY;");
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - pivotAup.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void ShinobuMetabolismTelemetryDepthUsesCrestWaterLevelSnapshotBeforeJobFallback()
        {
            string runtime = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physiology",
                "ShinobuMetabolismRuntime.cs");
            string jobs = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physiology",
                "ShinobuMetabolismJobs.cs");
            string rebind = ExtractMethodBody(runtime, "private void RebindColdServices()");
            string disable = ExtractMethodBody(runtime, "private void OnDisable()");
            string dispose = ExtractMethodBody(runtime, "public void Dispose()");
            string rebound = ExtractMethodBody(runtime, "public void OnGlobalRegistryServiceReplaced(");
            string slowTick = ExtractMethodBody(runtime, "public void SlowTick()");
            string resolveWater = ExtractMethodBody(runtime, "private double ResolveMetabolismSeaLevelAupY()");
            string jobMath = ExtractMethodBody(jobs, "public static float ResolveSeaLevelDepthMeters(");
            string jobStruct = ExtractStructBody(jobs, "public unsafe struct MetabolicIntegrationJob");
            string detailTelemetry = ExtractMethodBody(jobs, "private void WriteDetailTelemetry(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", runtime);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematics;", runtime);
            StringAssert.Contains("_oceanKinematics = GlobalRegistry.OceanKinematics;", rebind);
            StringAssert.Contains("_oceanKinematics = null;", disable);
            StringAssert.Contains("_oceanKinematics = null;", dispose);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematics = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("integrationJob.SeaLevelAupY = ResolveMetabolismSeaLevelAupY();", slowTick);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", resolveWater);
            StringAssert.Contains("public double SeaLevelAupY;", jobStruct);
            StringAssert.Contains("ResolveSeaLevelDepthMeters(playerAup.y, SeaLevelAupY)", detailTelemetry);
            StringAssert.Contains("resolvedSeaLevelAupY - absoluteY", jobMath);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;");
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - absoluteY", jobs);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void ReactorThermalMeltdownSignalsUseCrestWaterLevelSnapshotBeforeJobFallback()
        {
            string solver = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Thermodynamics",
                "AbyssalThermodynamicsSolver.cs");
            string bridge = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Thermodynamics",
                "AbyssalThermodynamicsSolver.ReactorBridge.cs");
            string jobs = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Thermodynamics",
                "ReactorThermalGridJobs.cs");
            string enable = ExtractMethodBody(solver, "private void OnEnable()");
            string disable = ExtractMethodBody(solver, "private void OnDisable()");
            string destroy = ExtractMethodBody(solver, "private void OnDestroy()");
            string rebound = ExtractMethodBody(solver, "public void OnGlobalRegistryServiceReplaced(");
            string scheduleBridge = ExtractMethodBody(bridge, "private JobHandle ScheduleReactorThermalLink(");
            string resolveWater = ExtractMethodBody(bridge, "private double ResolveReactorSeaLevelAupY()");
            string jobMath = ExtractMethodBody(jobs, "public static float ResolveSeaLevelDepthMeters(");
            string publishJob = ExtractStructBody(jobs, "public unsafe struct PublishNuclearReactorMeltdownSignalsJob");
            string baseSignal = ExtractMethodBody(jobs, "private bool EnqueueBaseCompromised(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", solver);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", solver);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", enable);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", destroy);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("using Hecton8.Core.Contracts;", bridge);
            StringAssert.Contains("publishJob.SeaLevelAupY = ResolveReactorSeaLevelAupY();", scheduleBridge);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", resolveWater);
            StringAssert.Contains("public double SeaLevelAupY;", publishJob);
            StringAssert.Contains("ResolveSeaLevelDepthMeters(aup, SeaLevelAupY)", baseSignal);
            StringAssert.Contains("resolvedSeaLevelAupY - aup.y", jobMath);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return OceanSurfaceAtmosphereConstants.DefaultSeaLevel;");
            StringAssert.DoesNotContain("DefaultSeaLevelAupY - aup.y", jobs);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void HabitatGraphStressDepthUsesCrestWaterLevelBeforeAtmosphereFallback()
        {
            string construction = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "ConstructionManager.cs");
            string habitatGraph = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Construction",
                "HabitatGraphManager.cs");
            string coldCache = ExtractMethodBody(construction, "private void CacheRegistryServicesCold()");
            string bind = ExtractMethodBody(construction, "private void BindConstructionRuntimeServices()");
            string clear = ExtractMethodBody(construction, "private void ClearCachedRegistryServices()");
            string rebound = ExtractMethodBody(construction, "public void OnGlobalRegistryServiceReplaced(");
            string dispose = ExtractMethodBody(habitatGraph, "public void Dispose()");
            string setRuntime = ExtractMethodBody(habitatGraph, "internal void SetRuntimeServices(");
            string setOcean = ExtractMethodBody(habitatGraph, "internal void SetOceanKinematicsService(");
            string resolveWater = ExtractMethodBody(habitatGraph, "private float ResolveRuntimeSeaLevelY()");
            string resolveDepth = ExtractMethodBody(habitatGraph, "private float ResolveRuntimeDepthMeters(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", construction);
            StringAssert.Contains("private IHectonOceanKinematicsService _cachedOceanKinematicsService;", construction);
            StringAssert.Contains("_cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_habitatGraphManager?.SetRuntimeServices(", bind);
            StringAssert.Contains("_cachedOceanKinematicsService,", bind);
            AssertTextBefore(bind, "_cachedOceanKinematicsService", "_cachedAtmosphereReadModel");
            StringAssert.Contains("_habitatGraphManager?.SetRuntimeServices(null, null, null, null, null);", clear);
            StringAssert.Contains("_cachedOceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_habitatGraphManager?.SetOceanKinematicsService(_cachedOceanKinematicsService);", rebound);

            StringAssert.Contains("using Hecton8.Core.Contracts;", habitatGraph);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", habitatGraph);
            StringAssert.Contains("IHectonOceanKinematicsService oceanKinematicsService,", setRuntime);
            StringAssert.Contains("_oceanKinematicsService = oceanKinematicsService;", setRuntime);
            StringAssert.Contains("_oceanKinematicsService = oceanKinematicsService;", setOcean);
            StringAssert.Contains("_oceanKinematicsService = null;", dispose);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("atmosphereReadModel.SeaLevelY", resolveWater);
            StringAssert.Contains("return math.max(0f, _runtimeSeaLevelY - runtimePosition.y);", resolveDepth);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "atmosphereReadModel.SeaLevelY");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", construction);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", construction);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void HabitatFluidIncursionIngressUsesCrestWaterlineBeforeSerializedFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "HabitatFluidIncursionDirector.cs");
            string enable = ExtractMethodBody(source, "private void OnEnable()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string fixedTick = ExtractMethodBody(source, "public void FixedTick(");
            string resolveAup = ExtractMethodBody(source, "private FluidAup48 ResolveExternalWaterlineAup()");
            string resolveWater = ExtractMethodBody(source, "private float ResolveExternalWaterlineRuntimeY(");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterlineRuntimeY(");
            string sanitize = ExtractMethodBody(source, "private static float SanitizeExternalWaterlineRuntimeY(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", enable);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)", rebound);
            StringAssert.Contains("CompleteScheduledSimulationForAuthoritativeWrite();", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ExternalWaterlineAup = ResolveExternalWaterlineAup()", fixedTick);
            StringAssert.Contains("ResolveExternalWaterlineRuntimeY(externalWaterlineRuntimeY)", resolveAup);
            StringAssert.Contains("TryResolveOceanWaterlineRuntimeY(out float oceanWaterlineY)", resolveWater);
            StringAssert.Contains("SanitizeExternalWaterlineRuntimeY(candidateWaterlineY)", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanWaterlineRuntimeY", "SanitizeExternalWaterlineRuntimeY");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TrySanitizeOceanExternalWaterlineRuntimeY(oceanKinematics.SeaLevel, out waterlineY)", resolveOcean);
            StringAssert.Contains("DefaultExternalWaterlineRuntimeY", sanitize);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveAup);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveAup);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void StormPropagationDepthAttenuationUsesCrestWaterLevelBeforeWeatherFallback()
        {
            string runtime = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Atmosphere",
                "StormPropagation",
                "ShinobuStormPropagationRuntime.cs");
            string jobs = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Atmosphere",
                "StormPropagation",
                "ShinobuStormPropagationJobs.cs");
            string disable = ExtractMethodBody(runtime, "private void OnDisable()");
            string dispose = ExtractMethodBody(runtime, "public void Dispose()");
            string refresh = ExtractMethodBody(runtime, "private void RefreshCachedRegistryServices()");
            string rebind = ExtractMethodBody(runtime, "private void ApplyRegistryServiceRebind(");
            string schedule = ExtractMethodBody(runtime, "private void SchedulePropagationJobs(");
            string resolveAup = ExtractMethodBody(runtime, "private double3 ResolveSeaLevelAupDouble(");
            string resolveWater = ExtractMethodBody(runtime, "private float ResolveRuntimeSeaLevelLocalY(");
            string resolveOcean = ExtractMethodBody(runtime, "private bool TryResolveOceanSeaLevelLocalY(");
            string sanitize = ExtractMethodBody(runtime, "private static bool TryResolveSeaLevelLocalY(");
            string attenuationJob = ExtractStructBody(jobs, "public struct CalculateStormAttenuationJob");

            StringAssert.Contains("using Hecton8.Core.Contracts;", runtime);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", runtime);
            StringAssert.Contains("ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.OceanKinematics, GlobalRegistry.OceanKinematics);", refresh);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebind);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebind);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", dispose);
            StringAssert.Contains("_lastSeaLevelAup = ResolveSeaLevelAupDouble(_lastOriginFallbackAup, in weatherRow, weatherAvailable);", schedule);
            StringAssert.Contains("SeaLevelAup = _lastSeaLevelAup", schedule);
            StringAssert.Contains("float seaLevelLocal = ResolveRuntimeSeaLevelLocalY(in weather, weatherAvailable);", resolveAup);
            StringAssert.Contains("TryResolveOceanSeaLevelLocalY(out float oceanSeaLevelLocal)", resolveWater);
            StringAssert.Contains("weather.SurfaceScalars.x", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanSeaLevelLocalY", "weather.SurfaceScalars.x");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TryResolveOceanSeaLevelLocalY(oceanKinematics.SeaLevel, out seaLevelLocal)", resolveOcean);
            StringAssert.Contains("math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.Contains("public double3 SeaLevelAup;", attenuationJob);
            StringAssert.Contains("ShinobuStormPropagationMath.ResolveDepthMeters(SampleAup, SeaLevelAup)", attenuationJob);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveAup);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveAup);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void SomaticKinematicsSurfaceBuoyancyUsesCrestWaterLevelBeforeVaultTuningFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "SomaticKinematicsRuntime.cs");
            string fixedTick = ExtractMethodBody(source, "public void FixedTick(");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string unregister = ExtractMethodBody(source, "private void UnregisterRuntime()");
            string rebind = ExtractMethodBody(source, "private void RebindServices()");
            string refresh = ExtractMethodBody(source, "private void RefreshLocalTuningSeaLevelFromOcean()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeRuntimeSeaLevelY(");
            string surfaceBuoyancy = ExtractMethodBody(source, "private void ApplySurfaceBuoyancy(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", rebind);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_oceanKinematicsService = null;", unregister);
            StringAssert.Contains("RefreshLocalTuningSeaLevelFromOcean();", fixedTick);
            StringAssert.Contains("Tuning = _localScratch.Tuning", fixedTick);
            AssertTextBefore(fixedTick, "RefreshLocalTuningSeaLevelFromOcean();", "SomaticKinematicsJob job");
            StringAssert.Contains("TryResolveOceanSeaLevelY(out float seaLevelY)", refresh);
            StringAssert.Contains("tuning.SeaLevelY = seaLevelY;", refresh);
            StringAssert.Contains("_localScratch.Tuning[0] = tuning;", refresh);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY)", resolveOcean);
            StringAssert.Contains("math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.Contains("float aboveSurface = local.y - tuning.SeaLevelY;", surfaceBuoyancy);
            StringAssert.Contains("float chestDepth = tuning.SeaLevelY - (local.y + tuning.ChestOffsetY);", surfaceBuoyancy);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", refresh);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", refresh);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void AnalyticalGerstnerWaveSamplesUseCrestWaterLevelBeforeTuningFallbackWithoutProviderLoop()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Buoyancy",
                "AnalyticalGerstnerWaveRuntime.cs");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string prepare = ExtractMethodBody(source, "private GerstnerWaveTuningDTO PrepareTuning(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveRuntimeSeaLevelY(");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeRuntimeSeaLevelY(");
            string refresh = ExtractMethodBody(source, "private void RefreshColdDependencies()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", refresh);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", destroy);
            StringAssert.Contains("tuning.SeaLevelY = ResolveRuntimeSeaLevelY(tuning.SeaLevelY);", prepare);
            StringAssert.Contains("TryResolveOceanSeaLevelY(out float seaLevelY)", resolveWater);
            StringAssert.Contains("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(fallbackSeaLevelY)", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanSeaLevelY", "AnalyticalGerstnerWaveConstants.ResolveSeaLevelY");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY)", resolveOcean);
            StringAssert.Contains("math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.DoesNotContain("RegisterProvider", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void AsyncBuoyancyReadbackShaderUsesCrestWaterLevelBeforeSerializedFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Buoyancy",
                "AsyncReadback",
                "AsyncBuoyancyReadbackRuntime.cs");
            string enable = ExtractMethodBody(source, "private void OnEnable()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string dispatch = ExtractMethodBody(source, "private ReadbackDispatchStatus DispatchGpuReadback()");
            string resolveWater = ExtractMethodBody(source, "private float ResolveRuntimeSeaLevelY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TrySanitizeOceanRuntimeSeaLevelY(");
            string sanitizeFallback = ExtractMethodBody(source, "private static bool TrySanitizeFallbackSeaLevelY(");
            string asmdef = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Buoyancy",
                "AsyncReadback",
                "Hecton8.Physics.Buoyancy.Runtime.asmdef");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("private float seaLevel = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("seaLevel = SanitizeFallbackSeaLevelY(seaLevel);", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", enable);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, ResolveRuntimeSeaLevelY());", dispatch);
            StringAssert.Contains("TryResolveOceanSeaLevelY(out float resolvedSeaLevelY)", resolveWater);
            StringAssert.Contains("SanitizeFallbackSeaLevelY(seaLevel)", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanSeaLevelY", "SanitizeFallbackSeaLevelY(seaLevel)");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out resolvedSeaLevelY)", resolveOcean);
            StringAssert.DoesNotContain("math.abs(value) > 0.0001f", sanitizeOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.Contains("math.abs(value) > 0.0001f", sanitizeFallback);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeFallback);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.DefaultWaterLevelY", sanitizeFallback);
            StringAssert.Contains("\"Hecton8.World.Contracts\"", asmdef);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void ExosuitCrushPressureUsesCrestWaterLevelBeforeMockDepthFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Exosuit",
                "ExosuitKinematicsRuntime.cs");
            string enable = ExtractMethodBody(source, "private void OnEnable()");
            string teardown = ExtractMethodBody(source, "private void FinishDisableTeardown()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string buildCrush = ExtractMethodBody(source, "private MockCrushDepthSignal BuildCrushDepth(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveExosuitDepthMeters()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanDepthMeters(");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeRuntimeSeaLevelY(");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_cachedTransform = transform;", enable);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", enable);
            StringAssert.Contains("_oceanKinematicsService = null;", teardown);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("crush.DepthMeters = ResolveExosuitDepthMeters();", buildCrush);
            StringAssert.Contains("crush.ExternalPressure01 = math.saturate(crush.DepthMeters", buildCrush);
            StringAssert.Contains("TryResolveOceanDepthMeters(out float depthMeters)", resolveDepth);
            StringAssert.Contains("_mockDepthMeters", resolveDepth);
            AssertTextBefore(resolveDepth, "TryResolveOceanDepthMeters", "_mockDepthMeters");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out float seaLevelY)", resolveOcean);
            StringAssert.Contains("float resolvedDepthMeters = seaLevelY - cachedTransform.position.y;", resolveOcean);
            StringAssert.Contains("depthMeters = math.max(0f, resolvedDepthMeters);", resolveOcean);
            StringAssert.Contains("math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveDepth);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveDepth);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
        }

        [Test]
        public void FluidEnginePublishesCrestWaterLevelToSurfaceUiShadersAndGpuBeforeSerializedFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonFluidEngine.cs");

            string cache = ExtractMethodBody(source, "private void CacheFluidRuntimeServicesCold()");
            string clear = ExtractMethodBody(source, "private void ClearCachedFluidRuntimeServices()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string fixedTick = ExtractMethodBody(source, "public void FixedTick(");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveCinematic = ExtractMethodBody(source, "private float ResolveCinematicWaterLevelY(");
            string readPublished = ExtractMethodBody(source, "private float ReadPublishedCurrentWaterLevelY()");
            string resolveBase = ExtractMethodBody(source, "private float ResolveBaseWaterLevelY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterLevelY(");
            string sanitize = ExtractMethodBody(source, "private static bool TryResolveRuntimeWaterLevelY(");
            string flush = ExtractMethodBody(source, "private void FlushCurrentWaterLevelUniform()");
            string ensureOceanAdapter = ExtractMethodBody(source, "private bool EnsureOceanAdapterVaultBootHandles()");
            string resetOceanAdapter = ExtractMethodBody(source, "private void ResetOceanAdapterVaultRoute()");
            string publishOceanAdapter = ExtractMethodBody(source, "private void PublishOceanAdapterGlobalWaterLevel(");

            StringAssert.Contains("using OceanAdapterVaultRoute = Hecton8.Environment.Fluids.OceanAdapterVaultRoute;", source);
            StringAssert.Contains("private OceanAdapterVaultHandles _oceanAdapterVaultHandles;", source);
            StringAssert.Contains("private bool _oceanAdapterVaultHandlesReady;", source);
            StringAssert.Contains("private bool _oceanAdapterVaultBootAttempted;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("[SerializeField] private float waterLevel = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("get => ResolveBaseWaterLevelY();", source);
            StringAssert.Contains("EnsureOceanAdapterVaultBootHandles();", awake);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", cache);
            StringAssert.Contains("_oceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("PublishCurrentWaterLevelUniform();", rebound);
            StringAssert.Contains("ResetOceanAdapterVaultRoute();", rebound);
            StringAssert.Contains("EnsureOceanAdapterVaultBootHandles();", rebound);

            StringAssert.Contains("PublishOceanAdapterGlobalWaterLevel(cinematicWaterLevel);", fixedTick);
            AssertTextBefore(fixedTick, "PublishOceanAdapterGlobalWaterLevel(cinematicWaterLevel);", "if (!TryDrainScheduledBuoyancyJob())");
            StringAssert.Contains("ResolveBaseWaterLevelY()", resolveCinematic);
            StringAssert.DoesNotContain("waterLevel,", resolveCinematic);
            StringAssert.Contains("ResolveBaseWaterLevelY()", readPublished);
            StringAssert.Contains("TryResolveOceanWaterLevelY(out float oceanWaterLevelY)", resolveBase);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.ResolveFallbackWaterLevelY(waterLevel)", resolveBase);
            AssertTextBefore(resolveBase, "TryResolveOceanWaterLevelY", "WorldWaterLevelCalibrationMath.ResolveFallbackWaterLevelY");

            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TryResolveRuntimeWaterLevelY(oceanKinematics.SeaLevel, out waterLevelY)", resolveOcean);
            StringAssert.Contains("math.abs(candidateWaterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.Contains("UIStateStore.WriteValue(UIValueSlotId.WaterSurfaceY", flush);
            StringAssert.Contains("Shader.SetGlobalFloat(_CurrentWaterLevelId, cinematicWaterLevel);", flush);
            StringAssert.Contains("Shader.SetGlobalFloat(_CurrentWaterLevelYId, cinematicWaterLevel);", flush);
            StringAssert.Contains("_oceanAdapterVaultBootAttempted = true;", ensureOceanAdapter);
            StringAssert.Contains("OceanAdapterVaultRoute.TryAcquireBootHandles(vault, out _oceanAdapterVaultHandles)", ensureOceanAdapter);
            StringAssert.Contains("_oceanAdapterVaultHandles = default;", resetOceanAdapter);
            StringAssert.Contains("_oceanAdapterVaultBootAttempted = false;", resetOceanAdapter);
            StringAssert.Contains("OceanAdapterVaultRoute.TryPublishWaterLevel(", publishOceanAdapter);
            StringAssert.Contains("ResolveFluidAdvectionQualityWeight()", publishOceanAdapter);
            StringAssert.Contains("Hecton8.Core.SystemDispatcher.CurrentFrameId", publishOceanAdapter);

            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveBase);
            StringAssert.DoesNotContain("TerrainData", resolveBase);
            StringAssert.DoesNotContain("_terrainBridge", resolveBase);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveOcean);
            StringAssert.DoesNotContain("TerrainData", resolveOcean);
            StringAssert.DoesNotContain("_terrainBridge", resolveOcean);
        }

        [Test]
        public void BuoyancyObjectEditorGizmoUsesFluidThenCrestWaterLevelBeforeDefaultFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "BuoyancyObject.cs");

            string gizmo = ExtractMethodBody(source, "private void OnDrawGizmosSelected()");
            string resolveWater = ExtractMethodBody(source, "private static float ResolveEditorWaterSurfaceY(");
            string sanitize = ExtractMethodBody(source, "private static bool TryResolveRuntimeWaterLevelY(");

            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("float waterY = ResolveEditorWaterSurfaceY(fluidSurface);", gizmo);
            StringAssert.DoesNotContain("5000f", gizmo);
            StringAssert.Contains("fluidSurface.WaterLevel", resolveWater);
            StringAssert.Contains("GlobalRegistry.OceanKinematics", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("TryResolveRuntimeWaterLevelY(oceanKinematics.SeaLevel, out float oceanWaterY)", resolveWater);
            StringAssert.Contains("return WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "fluidSurface.WaterLevel", "GlobalRegistry.OceanKinematics");
            AssertTextBefore(resolveWater, "GlobalRegistry.OceanKinematics", "WorldWaterLevelCalibrationMath.DefaultWaterLevelY");
            StringAssert.Contains("math.abs(candidateWaterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void AtmosphereUnderwaterFogDepthUsesCrestWaterLevelBeforePlayerFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonAtmosphereManager.cs");

            string cache = ExtractMethodBody(source, "private void CacheRegistryRuntimeReferences()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string syncWater = ExtractMethodBody(source, "private void SyncWaterSurfaceFromPlayerMovement()");
            string resolveWater = ExtractMethodBody(source, "private float ResolveSeaLevelY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanSeaLevelY(");
            string sanitize = ExtractMethodBody(source, "private static bool TryResolveRuntimeWaterSurfaceY(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepth()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", cache);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", destroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("SyncWaterSurfaceFromPlayerMovement();", rebound);

            StringAssert.Contains("TryResolveOceanSeaLevelY(out float oceanSeaLevelY)", syncWater);
            StringAssert.Contains("_waterSurfaceY = oceanSeaLevelY;", syncWater);
            AssertTextBefore(syncWater, "TryResolveOceanSeaLevelY", "TryResolveMovementRuntimeState");
            StringAssert.Contains("TryResolveRuntimeWaterSurfaceY(movementWaterSurfaceY, out float resolvedMovementWaterSurfaceY)", syncWater);
            StringAssert.Contains("TryResolveRuntimeWaterSurfaceY(_playerMovement.CurrentWaterSurfaceY, out float playerWaterSurfaceY)", syncWater);
            StringAssert.Contains("TryResolveOceanSeaLevelY(out float oceanSeaLevelY)", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanSeaLevelY", "TryResolveMovementRuntimeState");
            StringAssert.Contains("TryResolveRuntimeWaterSurfaceY(movementWaterSurfaceY, out float resolvedMovementWaterSurfaceY)", resolveWater);
            StringAssert.Contains("TryResolveRuntimeWaterSurfaceY(_playerMovement.CurrentWaterSurfaceY, out float playerWaterSurfaceY)", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TryResolveRuntimeWaterSurfaceY(oceanKinematics.SeaLevel, out seaLevelY)", resolveOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitize);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", sanitize);
            StringAssert.DoesNotContain("SanitizeWaterSurfaceY(_playerMovement.CurrentWaterSurfaceY)", source);
            StringAssert.Contains("ResolveSeaLevelY() - _playerCameraTransform.position.y", resolveDepth);
            StringAssert.Contains("ResolveSeaLevelY() - _playerTransform.position.y", resolveDepth);
            StringAssert.DoesNotContain("_waterSurfaceY - _playerCameraTransform.position.y", resolveDepth);
            StringAssert.DoesNotContain("_waterSurfaceY - _playerTransform.position.y", resolveDepth);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
            StringAssert.DoesNotContain("_proceduralFieldSampler", resolveWater);
        }

        [Test]
        public void HydrodynamicKccRuntimeUsesCrestWaterLevelBeforeSerializedFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "KCC", "HydrodynamicKccRuntime.cs");

            string enable = ExtractMethodBody(source, "private void OnEnable()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string updateTuning = ExtractMethodBody(source, "private HydrodynamicKccTuningDTO UpdateTuningSnapshot(");
            string defaultTuning = ExtractMethodBody(source, "private HydrodynamicKccTuningDTO DefaultTuning()");
            string fallbackSanitize = ExtractMethodBody(source, "public static float ResolveWaterSurfaceY(");
            string runtimeSanitize = ExtractMethodBody(source, "public static float ResolveRuntimeWaterSurfaceY(");
            string resolveRuntime = ExtractMethodBody(source, "private float ResolveRuntimeWaterSurfaceY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterSurfaceY(out float waterSurfaceY)");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TryResolveOceanWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("public const float DefaultWaterSurfaceY = Hecton8.World.WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("CacheOceanKinematicsRuntimeCold();", enable);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", source);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("tuning.WaterSurfaceY = ResolveRuntimeWaterSurfaceY();", updateTuning);
            StringAssert.Contains("WaterSurfaceY = ResolveRuntimeWaterSurfaceY(),", defaultTuning);
            StringAssert.Contains("HydrodynamicKccMath.ResolveRuntimeWaterSurfaceY(Tuning.WaterSurfaceY)", source);
            StringAssert.Contains("HydrodynamicKccMath.ResolveWaterSurfaceY(_waterSurfaceY)", resolveRuntime);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out waterSurfaceY)", resolveOcean);
            StringAssert.Contains("math.abs(candidateWaterSurfaceY) > MinDenominator", fallbackSanitize);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", fallbackSanitize);
            StringAssert.DoesNotContain("math.abs(candidateWaterSurfaceY) > MinDenominator", runtimeSanitize);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", runtimeSanitize);
            StringAssert.DoesNotContain("math.abs(candidateWaterSurfaceY) > MinDenominator", sanitizeOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
            StringAssert.DoesNotContain("TerrainData", resolveRuntime);
        }

        [Test]
        public void PlayerCriticalImpactAudioUsesCrestWaterLevelBeforeFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "PlayerCriticalProceduralAudioRenderer.cs");

            string coldCache = ExtractMethodBody(source, "private void CacheColdRegistryReferences()");
            string cacheService = ExtractMethodBody(source, "private void CacheRegistryServiceReference(");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");
            string resolveWater = ExtractMethodBody(source, "private float ResolveKineticImpactWaterlineY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanKineticImpactWaterlineY(");
            string runtimeSanitize = ExtractMethodBody(source, "private static bool TryResolveRuntimeKineticImpactWaterlineY(");
            string fallbackSanitize = ExtractMethodBody(source, "private static bool TryResolveKineticImpactWaterlineY(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveAbsoluteDepthMeters()");

            StringAssert.Contains("private const float KineticImpactDefaultWaterlineY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", cacheService);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", cacheService);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", destroy);
            StringAssert.Contains("TryResolveRuntimeKineticImpactWaterlineY(playerMovement.CurrentWaterSurfaceY, out float playerWaterlineY)", resolveWater);
            StringAssert.Contains("TryResolveOceanKineticImpactWaterlineY(out float oceanWaterlineY)", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveRuntimeKineticImpactWaterlineY(playerMovement.CurrentWaterSurfaceY", "TryResolveOceanKineticImpactWaterlineY");
            AssertTextBefore(resolveWater, "TryResolveOceanKineticImpactWaterlineY", "return KineticImpactDefaultWaterlineY;");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("TryResolveRuntimeKineticImpactWaterlineY(oceanKinematics.SeaLevel, out waterlineY)", resolveOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterlineY) > 0.0001f", runtimeSanitize);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", runtimeSanitize);
            StringAssert.Contains("math.abs(candidateWaterlineY) > 0.0001f", fallbackSanitize);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", fallbackSanitize);
            StringAssert.Contains("ResolveKineticImpactWaterlineY() + originAup.ToAbsoluteDouble3().y", resolveDepth);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        [Test]
        public void StreamingAndScatterBudgetDepthFallbackUseCrestWaterLevelBeforeTerrainBridge()
        {
            string streaming = ReadProjectFile("Assets", "_Project", "Scripts", "WorldStreamingDirector.cs");
            string scatter = ReadProjectFile("Assets", "_Project", "Scripts", "ScatterBudgetController.cs");

            AssertBudgetWaterlineDepthReader(
                streaming,
                "private void ResolveReferences()",
                "private void OnDisable()",
                "private void OnDestroy()",
                "ApplyStreamingProfile(force: true);");
            AssertBudgetWaterlineDepthReader(
                scatter,
                "private void RefreshColdReferences(bool force)",
                "private void OnDisable()",
                "private void OnDestroy()",
                "ApplyCurrentBudget(force: true);");
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static bool ProjectFileExists(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.Exists(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractClassBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractStructBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractBalancedBody(string source, int brace, string signature)
        {
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Could not extract body for " + signature);
            return string.Empty;
        }

        private static void AssertTextBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThanOrEqualTo(0), second);
            Assert.That(firstIndex, Is.LessThan(secondIndex), first + " before " + second);
        }

        private static void AssertBudgetWaterlineDepthReader(
            string source,
            string coldCacheSignature,
            string disableSignature,
            string destroySignature,
            string expectedReapplyCall)
        {
            string coldCache = ExtractMethodBody(source, coldCacheSignature);
            string disable = ExtractMethodBody(source, disableSignature);
            string destroy = ExtractMethodBody(source, destroySignature);
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private bool TryResolveCurrentDepth(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterSurfaceLevel()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterSurfaceLevel(");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TryResolveOceanWaterSurfaceLevel(");

            StringAssert.Contains("DefaultWaterSurfaceLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", disable);
            StringAssert.Contains("_oceanKinematicsService = null;", destroy);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains(expectedReapplyCall, rebound, "Depth budget owner must re-apply after waterline replacement.");
            StringAssert.Contains("depth = Mathf.Max(0f, ResolveWaterSurfaceLevel() - playerTransform.position.y);", resolveDepth);
            StringAssert.DoesNotContain("playerTransform == null || mapMagicBridge == null", resolveDepth);
            StringAssert.Contains("TryResolveOceanWaterSurfaceLevel(out float oceanWaterSurfaceLevel)", resolveWater);
            StringAssert.Contains("mapMagicBridge.WaterSurfaceLevel", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanWaterSurfaceLevel", "mapMagicBridge.WaterSurfaceLevel");
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertGameplayWaterReader(string source, string coldCacheSignature, string shutdownSignature)
        {
            string coldCache = ExtractMethodBody(source, coldCacheSignature);
            string shutdown = ExtractMethodBody(source, shutdownSignature);
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterSurfaceLevel()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterSurfaceLevel(");
            string sanitizeOcean = ExtractMethodBody(source, "private static bool TryResolveOceanWaterSurfaceLevel(");

            StringAssert.Contains("IHectonOceanKinematicsService", source);
            StringAssert.Contains("GlobalRegistry.OceanKinematics", coldCache);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            Assert.That(
                shutdown.IndexOf("_oceanKinematicsService = null;", StringComparison.Ordinal) >= 0 ||
                shutdown.IndexOf("_cachedOceanKinematicsService = null;", StringComparison.Ordinal) >= 0,
                Is.True,
                "ocean kinematics service cache must be cleared on shutdown");
            StringAssert.Contains("TryResolveOceanWaterSurfaceLevel", resolveWater);
            StringAssert.Contains("IsInitialized", resolveOcean);
            StringAssert.Contains("ActiveProvider", resolveOcean);
            StringAssert.Contains("IsAvailable", resolveOcean);
            StringAssert.Contains(".SeaLevel", resolveOcean);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", sanitizeOcean);
            StringAssert.DoesNotContain("math.abs(candidateWaterSurfaceLevel) > 0.0001f", sanitizeOcean);
            StringAssert.Contains("MapMagicBridge", resolveWater);
            AssertTextBefore(resolveWater, "TryResolveOceanWaterSurfaceLevel", "MapMagicBridge");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertSubmarineBallastWaterReader()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "SubmarineAutoLevelBallastController.cs");
            string register = ExtractMethodBody(source, "private void RegisterRuntime()");
            string unregister = ExtractMethodBody(source, "private void UnregisterRuntime()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveFallbackExternalDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveFallbackSeaLevelY()");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", register);
            StringAssert.Contains("GlobalRegistryServiceSlot.OceanKinematics", rebound);
            StringAssert.Contains("_oceanKinematicsService = null;", unregister);
            StringAssert.Contains("float seaLevelY = ResolveFallbackSeaLevelY();", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertHabitatIntegrityWaterReader()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "HabitatIntegrityManager.cs");
            string coldCache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string clear = ExtractMethodBody(source, "private void ClearCachedRegistryServices()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveDepthMeters()");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterSurfaceLevelY()");
            string resolveOcean = ExtractMethodBody(source, "private bool TryResolveOceanWaterSurfaceLevel(");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", clear);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("float seaLevelY = ResolveWaterSurfaceLevelY();", resolveDepth);
            StringAssert.Contains("TryResolveOceanWaterSurfaceLevel", resolveWater);
            StringAssert.Contains("terrainProvider.WaterSurfaceLevel", resolveWater);
            StringAssert.Contains("_atmosphereRuntime.SeaLevelY", resolveWater);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveOcean);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveOcean);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveOcean);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveOcean);
            AssertTextBefore(resolveWater, "TryResolveOceanWaterSurfaceLevel", "terrainProvider.WaterSurfaceLevel");
            AssertTextBefore(resolveWater, "terrainProvider.WaterSurfaceLevel", "_atmosphereRuntime.SeaLevelY");
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertDamageRouterWaterDepthReader()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "Hazards",
                "DamageRouter.cs");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private static float ResolveSeaLevelY()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("ResolveSeaLevelY() - impactPointWorld.y", resolveDepth);
            StringAssert.Contains("GlobalRegistry.OceanKinematics", resolveWater);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("DefaultSeaLevelY - impactPointWorld.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertRadiationTelemetryWaterDepthReader()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Gameplay",
                "RadiationHazardGrid.cs");
            string coldCache = ExtractMethodBody(source, "private void RefreshColdRegistryReferences()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters(");
            string resolveWater = ExtractMethodBody(source, "private double ResolveTelemetrySeaLevelY()");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("_oceanKinematicsService = GlobalRegistry.OceanKinematics;", coldCache);
            StringAssert.Contains("_oceanKinematicsService = null;", onDisable);
            StringAssert.Contains("_oceanKinematicsService = null;", onDestroy);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", rebound);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", rebound);
            StringAssert.Contains("ResolveTelemetrySeaLevelY() - playerAbsolute.y", resolveDepth);
            StringAssert.Contains("oceanKinematicsService.IsInitialized", resolveWater);
            StringAssert.Contains("oceanKinematicsService.ActiveProvider", resolveWater);
            StringAssert.Contains("oceanKinematics.IsAvailable", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            AssertTextBefore(resolveWater, "oceanKinematics.SeaLevel", "return DefaultSeaLevelY;");
            StringAssert.DoesNotContain("DefaultSeaLevelY - playerAbsolute.y", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertVisorWaterGlobalReader(string source, string cameraDepthSignature)
        {
            string resolveDepth = ExtractMethodBody(source, cameraDepthSignature);
            string resolveWater = ExtractMethodBody(source, "private static float ResolveProductionSeaLevelY()");
            string publishedGuard = ExtractMethodBody(source, "private static bool IsPublishedNoirFogStratification(");

            StringAssert.Contains("NoirFogStratificationId = Shader.PropertyToID(\"_HectonNoirFogStratification\")", source);
            StringAssert.Contains("using Hecton8.World;", source);
            StringAssert.Contains("float seaLevelY = ResolveProductionSeaLevelY();", resolveDepth);
            StringAssert.Contains("Shader.GetGlobalVector(ShaderConstants.NoirFogStratificationId)", resolveWater);
            StringAssert.Contains("fogStratification.x", resolveWater);
            StringAssert.Contains("IsPublishedNoirFogStratification(in fogStratification)", resolveWater);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", resolveWater);
            StringAssert.DoesNotContain("math.abs(waterLevelY) > 0.0001f", resolveWater);
            StringAssert.DoesNotContain("math.abs(waterLevelY) <= 1000f", resolveWater);
            StringAssert.Contains("return DefaultSeaLevelY;", resolveWater);
            StringAssert.Contains("math.isfinite(fogStratification.y)", publishedGuard);
            StringAssert.Contains("math.isfinite(fogStratification.z)", publishedGuard);
            StringAssert.Contains("math.isfinite(fogStratification.w)", publishedGuard);
            StringAssert.Contains("math.abs(fogStratification.y) > 0.000001f", publishedGuard);
            StringAssert.Contains("math.abs(fogStratification.z) > 0.000001f", publishedGuard);
            StringAssert.Contains("math.abs(fogStratification.w) > 0.000001f", publishedGuard);
            StringAssert.DoesNotContain("math.abs(fogStratification.x)", publishedGuard);
            AssertTextBefore(resolveDepth, "ResolveProductionSeaLevelY()", "math.max(0f, seaLevelY -");
            StringAssert.DoesNotContain("DefaultSeaLevelY -", resolveDepth);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", resolveWater);
            StringAssert.DoesNotContain("TerrainData", resolveWater);
        }

        private static void AssertSaveWaterSanitizer(string source)
        {
            StringAssert.Contains("selectedWaterLevelY", source);
            StringAssert.Contains("waterCalibrationTravelMeters", source);
            StringAssert.Contains("waterCalibrationSourceHash", source);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", source);
            StringAssert.Contains("FlagsWaterCalibrationPresent", source);
            StringAssert.Contains("waterCalibrationSourceHash == 0u", source);
        }

        private sealed class TestWaterLevelReadModel : IWorldWaterLevelCalibrationWriteModel
        {
            private float _waterLevelY;
            private readonly uint _sourceHash;

            internal int ApplyCount { get; private set; }

            internal TestWaterLevelReadModel(float waterLevelY, uint sourceHash = 1u)
            {
                _waterLevelY = waterLevelY;
                _sourceHash = sourceHash;
            }

            public bool TryGetWaterLevelCalibrationSnapshot(out WorldWaterLevelCalibrationDTO snapshot)
            {
                snapshot = WorldWaterLevelCalibrationMath.BuildSnapshot(
                    _waterLevelY,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    WorldWaterLevelCalibrationMath.DefaultCalibrationTravelMeters,
                    (uint)WorldWaterLevelCalibrationMath.DefaultAuthoringSeed,
                    0u,
                    _sourceHash);
                return true;
            }

            public bool TryApplyWaterLevelCalibration(float waterLevelY, float calibrationTravelMeters, uint sourceHash)
            {
                if (sourceHash != 0u && sourceHash != _sourceHash)
                    return false;

                if (!WorldWaterLevelCalibrationMath.TryResolveWaterLevelY(
                        waterLevelY,
                        WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                        calibrationTravelMeters,
                        out float resolvedWaterLevelY))
                {
                    return false;
                }

                _waterLevelY = resolvedWaterLevelY;
                ApplyCount++;
                return true;
            }
        }
    }
}
