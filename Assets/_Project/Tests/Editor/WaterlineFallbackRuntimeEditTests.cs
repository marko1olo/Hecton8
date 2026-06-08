using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class WaterlineFallbackRuntimeEditTests
    {
        [Test]
        public void EmergencyMockOceanKinematicsAdapter_DefaultsToProductionSeaLevel()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Environment",
                "Fluids",
                "EmergencyMockOceanKinematicsAdapter.cs");

            Assert.That(source, Does.Contain("private const float DefaultSeaLevel = 14.02f;"));
            Assert.That(source, Does.Contain("GenerateEmergencyMockOceanAdapter(float seaLevel = DefaultSeaLevel)"));
            Assert.That(source, Does.Contain("_seaLevel = ResolveSeaLevel(seaLevel);"));
            Assert.That(source, Does.Contain("private static float ResolveSeaLevel(float seaLevel)"));
            Assert.That(source, Does.Contain("math.abs(seaLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("GenerateEmergencyMockOceanAdapter(float seaLevel = 0f)"));
            Assert.That(source, Does.Not.Contain("math.select(0f, seaLevel"));
        }

        [Test]
        public void WaterOpticsRuntime_UsesProductionWaterlineForMissingWorldSurfaceFallbacks()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "WaterOptics",
                "WaterOpticsRuntime.cs");

            Assert.That(source, Does.Contain("private const float DefaultOceanSurfaceWorldY = 14.02f;"));
            Assert.That(source, Does.Contain("private float _oceanSurfaceWorldY = DefaultOceanSurfaceWorldY;"));
            Assert.That(source, Does.Contain("_oceanSurfaceWorldY = SanitizeOceanSurfaceWorldY(oceanSurfaceWorldY);"));
            Assert.That(source, Does.Contain("math.isfinite(localSurfaceY) ? localSurfaceY : DefaultOceanSurfaceWorldY"));
            Assert.That(source, Does.Contain("float surfaceWorldY = SanitizeOceanSurfaceWorldY(_oceanSurfaceWorldY);"));
            Assert.That(source, Does.Contain("private static float SanitizeOceanSurfaceWorldY(float value)"));
            Assert.That(source, Does.Contain("math.abs(value) <= 1000f"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private float _oceanSurfaceWorldY;"));
            Assert.That(source, Does.Not.Contain("math.isfinite(localSurfaceY) ? localSurfaceY : 0f"));
        }

        [Test]
        public void AbyssalOpticsTuner_DefaultsSurfaceSliderToProductionWaterline()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "WaterOptics",
                "Editor",
                "AbyssalOpticsTunerWindow.cs");

            Assert.That(source, Does.Contain("private const float DefaultOceanSurfaceWorldY = 14.02f;"));
            Assert.That(source, Does.Contain("Slider(\"Ocean Surface Y\", -2500f, 2500f, DefaultOceanSurfaceWorldY)"));
            Assert.That(source, Does.Not.Contain("Slider(\"Ocean Surface Y\", -2500f, 2500f, 0f)"));
        }

        [Test]
        public void GlobalPhysicsStateManager_RejectsStaleZeroCachedWaterlineAcrossDomainReload()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "GlobalPhysicsStateManager.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("_cachedWaterLevelBaseY = DefaultWaterLevelY;"));
            Assert.That(source, Does.Contain("_cachedCurrentWaterLevelY = DefaultWaterLevelY;"));
            Assert.That(source, Does.Contain("float resolvedBaseWaterLevelY = ResolveWaterLevelY(baseWaterLevelY);"));
            Assert.That(source, Does.Contain("math.abs(_cachedWaterLevelBaseY - resolvedBaseWaterLevelY) <= 0.0001f"));
            Assert.That(source, Does.Contain("float resolvedWaterLevelY = resolvedBaseWaterLevelY;"));
            Assert.That(source, Does.Contain("_cachedWaterLevelBaseY = resolvedBaseWaterLevelY;"));
            Assert.That(source, Does.Contain("private static float ResolveWaterLevelY(float candidateWaterLevelY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevelY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevelY) <= 1000f"));
            Assert.That(source, Does.Not.Contain("_cachedCurrentWaterLevelY = 0f;"));
            Assert.That(source, Does.Not.Contain("float resolvedWaterLevelY = baseWaterLevelY;"));
            Assert.That(source, Does.Not.Contain("_cachedWaterLevelBaseY = baseWaterLevelY;"));
        }

        [Test]
        public void HabitatFluidIncursionDirector_RejectsStaleZeroExternalWaterlineBeforeIngressJob()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "HabitatFluidIncursionDirector.cs");

            Assert.That(source, Does.Contain("private const float DefaultExternalWaterlineRuntimeY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;"));
            Assert.That(source, Does.Contain("[SerializeField, Min(0f)] private float externalWaterlineRuntimeY = DefaultExternalWaterlineRuntimeY;"));
            Assert.That(source, Does.Contain("ExternalWaterlineAup = ResolveExternalWaterlineAup()"));
            Assert.That(source, Does.Contain("new double3(0d, ResolveExternalWaterlineRuntimeY(externalWaterlineRuntimeY), 0d)"));
            Assert.That(source, Does.Contain("private static float ResolveExternalWaterlineRuntimeY(float candidateWaterlineY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterlineY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterlineY) <= 1000f"));
            Assert.That(source, Does.Contain(": DefaultExternalWaterlineRuntimeY;"));
            Assert.That(source, Does.Not.Contain("[SerializeField, Min(0f)] private float externalWaterlineRuntimeY = 0f;"));
            Assert.That(source, Does.Not.Contain("new double3(0d, externalWaterlineRuntimeY, 0d)"));
        }

        [Test]
        public void ProductionSceneAndPrefabBindings_DoNotOverrideWaterlineDefaultsWithStaleOriginValues()
        {
            string playerPrefab = ReadProjectFile("Assets", "_Project", "Prefabs", "Player.prefab");
            string skyPrefab = ReadProjectFile("Assets", "_Project", "Prefabs", "Sky_System.prefab");
            string worldScene = ReadProjectFile("Assets", "_Project", "Scenes", "02_HECTON_WORLD.unity");

            Assert.That(playerPrefab, Does.Contain("waterSurfaceY: 14.02"));
            Assert.That(skyPrefab, Does.Contain("fallbackSeaLevelY: 14.02"));
            Assert.That(worldScene, Does.Contain("waterLevelFallback: 14.02"));
            Assert.That(worldScene, Does.Contain("waterSurfaceLevel: 14.02"));
            Assert.That(worldScene, Does.Contain("_waterSurfaceY: 14.02"));
            Assert.That(playerPrefab, Does.Not.Contain("waterSurfaceY: 4900"));
            Assert.That(skyPrefab, Does.Not.Contain("fallbackSeaLevelY: 0"));
            Assert.That(skyPrefab, Does.Not.Contain("fallbackSeaLevelY: 4900"));
            Assert.That(worldScene, Does.Not.Contain("waterLevelFallback: 0"));
            Assert.That(worldScene, Does.Not.Contain("waterLevelFallback: 4900"));
            Assert.That(worldScene, Does.Not.Contain("waterSurfaceLevel: 0"));
            Assert.That(worldScene, Does.Not.Contain("waterSurfaceLevel: 4900"));
            Assert.That(worldScene, Does.Not.Contain("_waterSurfaceY: 0"));
            Assert.That(worldScene, Does.Not.Contain("_waterSurfaceY: 4900"));
        }

        [Test]
        public void KccEditorProofRoutes_SeedProductionWaterline()
        {
            string smoke = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "KCC",
                "Editor",
                "Shinobu355KccSmokeEditorFacade.cs");
            string replay = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "KCC",
                "Editor",
                "ReplayDeterminismValidator1626.cs");

            Assert.That(smoke, Does.Contain("private const float ProductionWaterSurfaceY = 14.02f;"));
            Assert.That(smoke, Does.Contain("WaterSurfaceY = ProductionWaterSurfaceY,"));
            Assert.That(replay, Does.Contain("private const float ProductionWaterSurfaceY = 14.02f;"));
            Assert.That(replay, Does.Contain("tuning.WaterSurfaceY = ProductionWaterSurfaceY;"));
            Assert.That(smoke, Does.Not.Contain("WaterSurfaceY = 0f,"));
            Assert.That(replay, Does.Not.Contain("tuning.WaterSurfaceY = 0f;"));
        }

        [Test]
        public void HydrodynamicKccRuntime_RejectsStaleZeroWaterlineInMathAndTuning()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "KCC",
                "HydrodynamicKccRuntime.cs");

            Assert.That(source, Does.Contain("public const float DefaultWaterSurfaceY = 14.02f;"));
            Assert.That(source, Does.Contain("public static float ResolveWaterSurfaceY(float candidateWaterSurfaceY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) > MinDenominator"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) <= 1000f"));
            Assert.That(source, Does.Contain("float waterSurfaceY = HydrodynamicKccMath.ResolveWaterSurfaceY(Tuning.WaterSurfaceY);"));
            Assert.That(source, Does.Contain("internal const float DefaultWaterSurfaceY = HydrodynamicKccMath.DefaultWaterSurfaceY;"));
            Assert.That(source, Does.Contain("tuning.WaterSurfaceY = HydrodynamicKccMath.ResolveWaterSurfaceY(tuning.WaterSurfaceY);"));
            Assert.That(source, Does.Not.Contain("float waterSurfaceY = math.isfinite(Tuning.WaterSurfaceY) ? Tuning.WaterSurfaceY"));
            Assert.That(source, Does.Not.Contain("tuning.WaterSurfaceY = math.isfinite(tuning.WaterSurfaceY) ? tuning.WaterSurfaceY : DefaultWaterSurfaceY;"));
        }

        [Test]
        public void HectonBoidController_RejectsStaleZeroWaterlineForSpawnComputeAndGizmos()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "HectonBoidController.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceY = 14.02f;"));
            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceY()"));
            Assert.That(source, Does.Contain("math.abs(waterSurfaceY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(waterSurfaceY) <= 1000f"));
            Assert.That(source, Does.Contain("float safeWaterSurfaceY = ResolveWaterSurfaceY();"));
            Assert.That(source, Does.Contain("position.y = Mathf.Clamp(position.y, center.y - boundsSize.y, safeWaterSurfaceY - 2f);"));
            Assert.That(source, Does.Contain("cs.SetFloat(ShaderProps.WaterSurfaceY, safeWaterSurfaceY);"));
            Assert.That(source, Does.Contain("float maxCenterY = safeWaterSurfaceY - safeBoundsSize.y;"));
            Assert.That(source, Does.Contain("new Vector3(boundsCenter.x, ResolveWaterSurfaceY(), boundsCenter.z)"));
            Assert.That(source, Does.Not.Contain("ClampFinite(waterSurfaceY, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, DefaultWaterSurfaceY)"));
            Assert.That(source, Does.Not.Contain("position.y = Mathf.Clamp(position.y, center.y - boundsSize.y, waterSurfaceY - 2f);"));
            Assert.That(source, Does.Not.Contain("new Vector3(boundsCenter.x, waterSurfaceY, boundsCenter.z)"));
        }

        [Test]
        public void HectonFluidEngine_RejectsStaleZeroWaterlineBeforePublishingSurface()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "HectonFluidEngine.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceY = 14.02f;"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) <= 1000f"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceY(terrainProvider.WaterSurfaceLevel, out float terrainWaterSurfaceY)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceY(mapMagic.WaterSurfaceLevel, out float mapMagicWaterSurfaceY)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceY(fallbackWaterLevel, out float safeFallbackWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceY(referenceWaterLevel, out float resolvedReferenceWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceY(candidateWaterLevel, out float safeCandidateWaterLevel)"));
            Assert.That(source, Does.Not.Contain("math.isfinite(terrainProvider.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("math.isfinite(mapMagic.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("float safeReference = math.isfinite(referenceWaterLevel) ? referenceWaterLevel : DefaultWaterSurfaceY;"));
            Assert.That(source, Does.Not.Contain("return candidateWaterLevel;"));
        }

        [Test]
        public void BuoyancyObjectEditorGizmo_RejectsStaleZeroWaterline()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "BuoyancyObject.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceY = 14.02f;"));
            Assert.That(source, Does.Contain("private static bool TryResolveEditorWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) <= 1000f"));
            Assert.That(source, Does.Contain("TryResolveEditorWaterSurfaceY(fluidSurface.WaterLevel, out float fluidWaterSurfaceY)"));
            Assert.That(source, Does.Contain("TryResolveEditorWaterSurfaceY(terrainProvider.WaterSurfaceLevel, out float terrainWaterSurfaceY)"));
            Assert.That(source, Does.Contain("TryResolveEditorWaterSurfaceY(mapMagic.WaterSurfaceLevel, out float mapMagicWaterSurfaceY)"));
            Assert.That(source, Does.Not.Contain("fluidSurface != null && math.isfinite(fluidSurface.WaterLevel)"));
            Assert.That(source, Does.Not.Contain("terrainProvider != null && math.isfinite(terrainProvider.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("mapMagic != null && math.isfinite(mapMagic.WaterSurfaceLevel)"));
        }

        [Test]
        public void OceanSinglePassRuntime_RejectsStaleZeroTerrainWaterline()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Rendering",
                "OceanSinglePass",
                "OceanSinglePassRuntime.cs");

            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceAupY()"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceAupY(terrainProvider.WaterSurfaceLevel, out float terrainWaterSurfaceY)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterSurfaceAupY(float candidateWaterSurfaceY, out float waterSurfaceY)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceY) <= 1000f"));
            Assert.That(source, Does.Contain("waterSurfaceY = OceanSinglePassConstants.DefaultSeaLevelMeters;"));
            Assert.That(source, Does.Not.Contain("terrainProvider != null && math.isfinite(terrainProvider.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("return terrainProvider.WaterSurfaceLevel;"));
        }

        [Test]
        public void UnderwaterVisuals_RejectsStaleZeroVisualWaterlineFallback()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "HectonUnderwaterVisuals.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevelFallback = 14.02f;"));
            Assert.That(source, Does.Contain("[SerializeField] private float waterLevelFallback = DefaultWaterLevelFallback;"));
            Assert.That(source, Does.Contain("SetWaterLevelFallback(float y) => waterLevelFallback = SanitizeVisualWaterLevel(y, DefaultWaterLevelFallback);"));
            Assert.That(source, Does.Contain("float terrainWaterLevel = SanitizeVisualWaterLevel(waterLevelFallback, DefaultWaterLevelFallback);"));
            Assert.That(source, Does.Contain("float waterLevel = SanitizeVisualWaterLevel(waterLevelFallback, DefaultWaterLevelFallback);"));
            Assert.That(source, Does.Contain("TryResolveVisualWaterLevel(atmosphereSeaLevel, out float resolvedAtmosphereSeaLevel)"));
            Assert.That(source, Does.Contain("TryResolveVisualWaterLevel(terrainRuntime.WaterSurfaceLevel, out float mapMagicWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveVisualWaterLevel(fluidWaterLevel, out float resolvedFluidWaterLevel)"));
            Assert.That(source, Does.Contain("math.abs(resolvedFluidWaterLevel - terrainWaterLevel) <= 128f"));
            Assert.That(source, Does.Contain("private static bool TryResolveVisualWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = DefaultWaterLevelFallback;"));
            Assert.That(source, Does.Not.Contain("if (math.isfinite(atmosphereSeaLevel))"));
            Assert.That(source, Does.Not.Contain("return math.isfinite(fallbackWaterLevel) ? fallbackWaterLevel : 0f;"));
            Assert.That(source, Does.Not.Contain("[SerializeField] private float waterLevelFallback = 14.02f;"));
        }

        [Test]
        public void FloraInteractionManager_RejectsStaleZeroWaterlineAcrossVegetationProviders()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "FloraInteractionManager.cs");

            Assert.That(source, Does.Contain("private const float DefaultVegetationWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("TryResolveVegetationWaterLevel(mapMagicRuntime.WaterSurfaceLevel, out float mapMagicWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveVegetationWaterLevel(_oceanKinematicsProvider.SeaLevel, out float oceanWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveVegetationWaterLevel(fluidReadModel.WaterLevel, out float fluidWaterLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveVegetationWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = DefaultVegetationWaterLevel;"));
            Assert.That(source, Does.Not.Contain("math.isfinite(mapMagicRuntime.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("math.isfinite(_oceanKinematicsProvider.SeaLevel)"));
            Assert.That(source, Does.Not.Contain("math.isfinite(fluidReadModel.WaterLevel)"));
        }

        [Test]
        public void EcosystemDirector_RejectsStaleZeroWaterlineBeforeDepthSolves()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "EcosystemDirector.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(bridge.WaterSurfaceLevel, out float bridgeWaterSurfaceLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterSurfaceLevel = DefaultWaterSurfaceLevelY;"));
            Assert.That(source, Does.Not.Contain("return bridge.WaterSurfaceLevel;"));
        }

        [Test]
        public void HectonMapMagicVegetationBridge_RejectsStaleZeroWaterlineBeforeKelpBinding()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "HectonMapMagicVegetationBridge.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("private float waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(bridgedWaterLevel, out float resolvedWaterLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Not.Contain("if (!math.isfinite(bridgedWaterLevel))"));
        }

        [Test]
        public void SargassumMicroFaunaBoids_RejectsStaleZeroWaterlineBeforeGpuSimulationBand()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "SargassumMicroFaunaBoids.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("private float waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(bridgedWaterLevel, out float resolvedWaterLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = TryResolveWaterLevel(waterLevel, out float resolvedWaterLevel)"));
            Assert.That(source, Does.Contain("waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Not.Contain("private float waterLevel = 14.02f;"));
            Assert.That(source, Does.Not.Contain("if (math.isfinite(bridgedWaterLevel))"));
            Assert.That(source, Does.Not.Contain("waterLevel = ClampMinFinite(waterLevel, 0f);"));
        }

        [Test]
        public void ResourceDistributionDirector_RejectsStaleZeroWaterlineBeforeResourceDepthSolves()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "ResourceDistributionDirector.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceLevel()"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(bridge.WaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterSurfaceLevel = DefaultWaterSurfaceLevelY;"));
            Assert.That(source, Does.Not.Contain("float waterSurface = mapMagicBridge.WaterSurfaceLevel;"));
            Assert.That(source, Does.Not.Contain("if (!math.isfinite(waterSurface))"));
        }

        [Test]
        public void SargassumCutManager_RejectsStaleZeroWaterlineBeforeMaskStamping()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "SargassumCutManager.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("private float ResolveMaskWaterLevel(float fallbackY)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(mapMagicVegetationBridge.ActiveSurfaceDrawBounds.center.y, out float vegetationWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(terrainBridge.WaterSurfaceLevel, out float terrainWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(fallbackY, out float fallbackWaterLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Not.Contain("terrainBridge != null && math.isfinite(terrainBridge.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("return terrainBridge.WaterSurfaceLevel;"));
            Assert.That(source, Does.Not.Contain("return Mathf.Max(DefaultWaterLevel, fallbackY);"));
        }

        [Test]
        public void HectonPlayerSpawner_RejectsStaleZeroWaterlineBeforeSpawnWindowRefresh()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "HectonPlayerSpawner.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(terrainBridge.WaterSurfaceLevel, out float terrainWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(waterLevel, out float resolvedPreviousWaterLevel)"));
            Assert.That(source, Does.Contain("waterLevel = terrainWaterLevel;"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("!math.isfinite(terrainBridge.WaterSurfaceLevel)"));
            Assert.That(source, Does.Not.Contain("waterLevel = terrainBridge.WaterSurfaceLevel;"));
        }

        [Test]
        public void WorldStreamingDirector_RejectsStaleZeroWaterlineBeforeDepthLod()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "WorldStreamingDirector.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("depth = Mathf.Max(0f, ResolveWaterSurfaceLevel() - playerTransform.position.y);"));
            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceLevel()"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(mapMagicBridge.WaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y"));
        }

        [Test]
        public void FaunaDirector_RejectsStaleZeroWaterlineBeforeSpawnHeightValidation()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "FaunaDirector.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("float waterSurfaceLevel = ResolveWaterSurfaceLevel(bridge);"));
            Assert.That(source, Does.Contain("if (spawnY >= waterSurfaceLevel || spawnY <= bottomHeight)"));
            Assert.That(source, Does.Contain("private static float ResolveWaterSurfaceLevel(ITerrainProvider terrainProvider)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(terrainProvider.WaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("spawnY >= bridge.WaterSurfaceLevel"));
        }

        [Test]
        public void WorldProceduralFieldSampler_RejectsStaleZeroWaterlineBeforeJobInputs()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "WorldProceduralFieldSampler.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceLevel(float fallbackWaterSurfaceLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(mapMagicBridge.WaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(fallbackWaterSurfaceLevel, out float fallbackSurfaceLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Contain("float fallbackSurface = ResolveWaterSurfaceLevel(math.max(position.y + 120f, 120f));"));
            Assert.That(source, Does.Not.Contain("? mapMagicBridge.WaterSurfaceLevel"));
        }

        [Test]
        public void OceanAdapterVaultRoute_RejectsStaleZeroWaterlineBeforePublishingGlobalDto()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Environment",
                "Fluids",
                "OceanAdapterVaultRoute.cs");

            Assert.That(source, Does.Contain("public const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("row.WaterLevel = ResolveWaterLevel(waterLevel);"));
            Assert.That(source, Does.Contain("private static float ResolveWaterLevel(float candidateWaterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("row.WaterLevel = math.select(0f, waterLevel, math.isfinite(waterLevel));"));
        }

        [Test]
        public void HectonCrestOceanDepthCacheBootstrap_RejectsStaleZeroWaterlineBeforeDepthCacheAlignment()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "HectonCrestOceanDepthCacheBootstrap.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterLevel = 14.02f;"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(seaLevel, out baseWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(bridgedWaterLevel, out float resolvedBridgedWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(oceanRenderer.transform.position.y, out float rendererWaterLevel)"));
            Assert.That(source, Does.Contain("TryResolveWaterLevel(transform.position.y, out float ownerWaterLevel)"));
            Assert.That(source, Does.Contain("private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("math.abs(candidateWaterLevel) <= 1000f"));
            Assert.That(source, Does.Contain("waterLevel = DefaultWaterLevel;"));
            Assert.That(source, Does.Not.Contain("if (IsFinite(seaLevel))"));
            Assert.That(source, Does.Not.Contain("if (IsFinite(bridgedWaterLevel))"));
            Assert.That(source, Does.Not.Contain("return oceanRenderer != null ? oceanRenderer.transform.position.y : transform.position.y;"));
        }

        [Test]
        public void ScatterBudgetController_RejectsStaleZeroWaterlineBeforeDepthBudgets()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "ScatterBudgetController.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevelY = 14.02f;"));
            Assert.That(source, Does.Contain("depth = Mathf.Max(0f, ResolveWaterSurfaceLevel() - playerTransform.position.y);"));
            Assert.That(source, Does.Contain("private float ResolveWaterSurfaceLevel()"));
            Assert.That(source, Does.Contain("TryResolveWaterSurfaceLevel(mapMagicBridge.WaterSurfaceLevel, out float waterSurfaceLevel)"));
            Assert.That(source, Does.Contain("Mathf.Abs(candidateWaterSurfaceLevel) > 0.0001f"));
            Assert.That(source, Does.Contain("Mathf.Abs(candidateWaterSurfaceLevel) <= 1000f"));
            Assert.That(source, Does.Not.Contain("mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y"));
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(parts);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, path));
        }
    }
}
