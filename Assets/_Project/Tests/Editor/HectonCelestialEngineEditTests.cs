using System;
using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Celestial;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class HectonCelestialEngineEditTests
{
    private GameObject _gameObject;
    private GameObject _mainCameraObject;
    private HectonCelestialEngine _engine;
    private Material _skyMaterial;

    [SetUp]
    public void SetUp()
    {
        _mainCameraObject = new GameObject("TestMainCamera");
        _mainCameraObject.tag = "MainCamera";
        _mainCameraObject.AddComponent<Camera>();

        _gameObject = new GameObject("CelestialEngineTestGO");

        _engine = _gameObject.AddComponent<HectonCelestialEngine>();

        Shader skyShader = Shader.Find("HECTON/Sky/Hecton_AlienSky_Master");
        Assert.IsNotNull(skyShader, "Expected HECTON/Sky/Hecton_AlienSky_Master shader to exist.");

        _skyMaterial = new Material(skyShader);
        _skyMaterial.name = "Mat_HectonSky_Test";
        SetPrivateField("_skyMaterial", _skyMaterial);
        SetPrivateField("twilightStartAngle", 8f);
        SetPrivateField("twilightEndAngle", -5f);
        SetPrivateField("_smoothedOcclusionFactor", 0f);
        SetPrivateField("_currentSunAngle", -45f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_skyMaterial != null)
            Object.DestroyImmediate(_skyMaterial);

        if (_mainCameraObject != null)
            Object.DestroyImmediate(_mainCameraObject);

        if (_gameObject != null)
            Object.DestroyImmediate(_gameObject);
    }

    [Test]
    public void CustomSkyBlendAndStarsUpdateWithoutLegacySkyboxMaterial()
    {
        InvokePrivateMethod("UpdateSkyboxBlend", -10f);
        InvokePrivateMethod("UpdateStarIntensity", -10f);
        InvokePrivateMethod("UpdateSkyMaterial");

        Assert.That(_engine.DayNightBlend, Is.GreaterThan(0.99f));
        Assert.That(_engine.StarIntensity, Is.GreaterThan(0.99f));
        Assert.That(_skyMaterial.GetFloat("_NightBlend"), Is.GreaterThan(0.99f));
        Assert.That(_skyMaterial.GetFloat("_StarIntensity"), Is.GreaterThan(0.99f));
    }

    [Test]
    public void CelestialAtmosphereProfileWeightsFavorExpectedProfileBands()
    {
        object[] noonArgs = { 35f, 0f, 0f, 0f };
        InvokePrivateMethod("EvaluateCelestialAtmosphereProfileWeights", noonArgs);
        Assert.That((float)noonArgs[1], Is.GreaterThan(0.99f));
        Assert.That((float)noonArgs[2], Is.LessThan(0.01f));
        Assert.That((float)noonArgs[3], Is.LessThan(0.01f));

        object[] horizonArgs = { 0f, 0f, 0f, 0f };
        InvokePrivateMethod("EvaluateCelestialAtmosphereProfileWeights", horizonArgs);
        Assert.That((float)horizonArgs[2], Is.GreaterThan((float)horizonArgs[1]));
        Assert.That((float)horizonArgs[2], Is.GreaterThan((float)horizonArgs[3]));

        object[] nightArgs = { -24f, 0f, 0f, 0f };
        InvokePrivateMethod("EvaluateCelestialAtmosphereProfileWeights", nightArgs);
        Assert.That((float)nightArgs[3], Is.GreaterThan(0.99f));
        Assert.That((float)nightArgs[1], Is.LessThan(0.01f));
        Assert.That((float)nightArgs[2], Is.LessThan(0.01f));

        object[] belowHorizonArgs = { -10f, 0f, 0f, 0f };
        InvokePrivateMethod("EvaluateCelestialAtmosphereProfileWeights", belowHorizonArgs);
        Assert.That((float)belowHorizonArgs[1], Is.LessThan(0.01f));
        Assert.That((float)belowHorizonArgs[3], Is.GreaterThan((float)belowHorizonArgs[2]));
    }

    [Test]
    public void ResolveSkyColorsAppliesScriptProfilesAndReadabilityFloors()
    {
        Color profileZenith = new Color(0.12f, 0.34f, 0.56f, 1f);
        Color profileHorizon = new Color(0.65f, 0.43f, 0.21f, 1f);
        Color profileNadir = new Color(0.07f, 0.08f, 0.11f, 1f);

        SetPrivateField("_dayProfile", SkyColorProfile.Default(profileZenith, profileHorizon, profileNadir));
        SetPrivateField("_horizonZenithBlend", 0f);
        SetPrivateField("_horizonBrightnessScale", 1f);
        SetPrivateField("_currentSunAngle", 35f);
        SetPrivateField("_smoothedOcclusionFactor", 0f);

        _skyMaterial.SetColor("_SkyColorZenith", profileZenith);
        _skyMaterial.SetColor("_SkyColorHorizon", profileHorizon);
        _skyMaterial.SetColor("_SkyColorNadir", profileNadir);

        object[] args = { null, null, null };
        InvokePrivateMethod("ResolveSkyColors", args);

        Assert.That((Color)args[0], Is.EqualTo(new Color(0.16f, 0.34f, 0.56f, 1f)));
        Color resolvedHorizon = (Color)args[1];
        Assert.That(resolvedHorizon.r, Is.EqualTo(0.601f).Within(0.001f));
        Assert.That(resolvedHorizon.g, Is.EqualTo(0.460f).Within(0.001f));
        Assert.That(resolvedHorizon.b, Is.EqualTo(0.500f).Within(0.001f));
        Assert.That(resolvedHorizon.a, Is.EqualTo(profileHorizon.a).Within(0.0001f));
        Assert.That((Color)args[2], Is.EqualTo(new Color(0.11f, 0.165f, 0.19f, 1f)));
    }

    [Test]
    public void ResolveSkyColorsDarkensHorizonAtNightWithoutInversion()
    {
        Color baseZenith = new Color(0.12f, 0.28f, 0.62f, 1f);
        Color baseHorizon = new Color(0.42f, 0.56f, 0.74f, 1f);
        Color baseNadir = new Color(0.03f, 0.05f, 0.12f, 1f);

        SetPrivateField("_dayProfile", SkyColorProfile.Default(baseZenith, baseHorizon, baseNadir));
        SetPrivateField("_nightProfile", SkyColorProfile.Default(
            new Color(0.02f, 0.04f, 0.08f, 1f),
            new Color(0.08f, 0.09f, 0.14f, 1f),
            new Color(0.01f, 0.015f, 0.03f, 1f)));
        SetPrivateField("_horizonZenithBlend", 0f);
        SetPrivateField("_horizonBrightnessScale", 1f);
        SetPrivateField("_currentSunAngle", -24f);
        SetPrivateField("_smoothedOcclusionFactor", 0f);

        object[] args = { null, null, null };
        InvokePrivateMethod("ResolveSkyColors", args);

        Color resolvedZenith = (Color)args[0];
        Color resolvedHorizon = (Color)args[1];

        Assert.That(resolvedZenith.maxColorComponent, Is.LessThan(baseZenith.maxColorComponent));
        Assert.That(resolvedHorizon.maxColorComponent, Is.LessThan(baseHorizon.maxColorComponent));
        Assert.That(resolvedHorizon.grayscale, Is.GreaterThan(resolvedZenith.grayscale));
    }

    [Test]
    public void ApplySkyMaterialPropertiesPushesScriptOwnedSkyStateIntoMaterial()
    {
        Color resolvedZenith = new Color(0.11f, 0.22f, 0.44f, 1f);
        Color resolvedHorizon = new Color(0.55f, 0.33f, 0.2f, 1f);
        Color resolvedNadir = new Color(0.04f, 0.05f, 0.08f, 1f);

        SetPrivateField("_resolvedSkyZenith", resolvedZenith);
        SetPrivateField("_resolvedSkyHorizon", resolvedHorizon);
        SetPrivateField("_resolvedSkyNadir", resolvedNadir);
        SetPrivateField("_currentBlend", 0.42f);
        SetPrivateField("_currentStarIntensity", 0.35f);
        SetPrivateField("_smoothedOcclusionFactor", 0.18f);
        SetPrivateField("_gameTime", 12.5f);
        SetPrivateField("_atmosphereTransmittanceWeight", 0.67f);
        SetPrivateField("_atmosphereInscatterWeight", 1.12f);
        SetPrivateField("_currentBacklitFactor", 0.5f);
        SetPrivateField("_sunsetProfile", SkyColorProfile.Default(resolvedZenith, resolvedHorizon, resolvedNadir));

        InvokePrivateMethod(
            "ApplySkyMaterialProperties",
            _skyMaterial,
            0.25f,
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 0f, 1f, 0f));

        Assert.That(_skyMaterial.GetColor("_SkyColorZenith"), Is.EqualTo(resolvedZenith));
        Assert.That(_skyMaterial.GetColor("_SkyColorHorizon"), Is.EqualTo(resolvedHorizon));
        Assert.That(_skyMaterial.GetColor("_SkyColorNadir"), Is.EqualTo(resolvedNadir));
        Assert.That(_skyMaterial.GetFloat("_AtmosphereTransmittanceWeight"), Is.EqualTo(0.67f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_AtmosphereInscatterWeight"), Is.EqualTo(1.12f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_AegirGlowIntensity"), Is.EqualTo(0.56f).Within(0.0001f));
    }

    [Test]
    public void UpdateMoonMaterialOverridesUsesMoonSpecificAtmosphereMultipliers()
    {
        Shader moonShader = Shader.Find("HECTON/Celestial/Hecton_CelestialMoon");
        Assert.IsNotNull(moonShader, "Expected HECTON/Celestial/Hecton_CelestialMoon shader to exist.");

        GameObject moonObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Material moonMaterial = new Material(moonShader);
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        try
        {
            Renderer renderer = moonObject.GetComponent<Renderer>();
            renderer.sharedMaterial = moonMaterial;

            SetPrivateField("_moonMPB", propertyBlock);
            SetPrivateField("_atmosphereTransmittanceWeight", 0.92f);
            SetPrivateField("_atmosphereInscatterWeight", 0.78f);
            SetPrivateField("_moonAtmosphereTransmittanceMultiplier", 0.78f);
            SetPrivateField("_moonAtmosphereInscatterMultiplier", 0.42f);

            var moonRenderers = (System.Collections.Generic.List<Renderer>)GetPrivateField(_engine, "_moonRenderers");
            moonRenderers.Clear();
            moonRenderers.Add(renderer);

            InvokePrivateMethod("UpdateMoonMaterialOverrides");

            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(
                propertyBlock.GetFloat(Shader.PropertyToID("_AtmosphereTransmittanceWeight")),
                Is.EqualTo(0.7176f).Within(0.0001f));
            Assert.That(
                propertyBlock.GetFloat(Shader.PropertyToID("_AtmosphereInscatterWeight")),
                Is.EqualTo(0.3276f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(moonMaterial);
            Object.DestroyImmediate(moonObject);
        }
    }

    [Test]
    public void ApplyBestVisualDefaultsSetsNightInscatterFloorAndReadableDayHaze()
    {
        _engine.ApplyBestVisualDefaults();

        float nightExposure = (float)GetPrivateField(_engine, "nightAtmosphereExposure");
        float horizonDensity = (float)GetPrivateField(_engine, "horizonDensity");
        float zenithTransparency = (float)GetPrivateField(_engine, "zenithTransparency");
        float mistShelfIntensity = (float)GetPrivateField(_engine, "_surfaceHorizonMistShelfIntensity");
        float mistShelfHeight = (float)GetPrivateField(_engine, "_surfaceHorizonMistShelfHeight");
        float mistShelfSoftness = (float)GetPrivateField(_engine, "_surfaceHorizonMistShelfSoftness");
        AnimationCurve dayDensity = (AnimationCurve)GetPrivateField(_engine, "dayAtmosphereDensity");

        Assert.That(nightExposure, Is.EqualTo(0.001f).Within(0.0001f));
        Assert.That(horizonDensity, Is.EqualTo(1.1f).Within(0.0001f));
        Assert.That(zenithTransparency, Is.EqualTo(0.84f).Within(0.0001f));
        Assert.That(mistShelfIntensity, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(mistShelfHeight, Is.EqualTo(0.16f).Within(0.0001f));
        Assert.That(mistShelfSoftness, Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(dayDensity.Evaluate(0f), Is.LessThan(0.7f));
        Assert.That(dayDensity.Evaluate(0f), Is.GreaterThan(dayDensity.Evaluate(1f)));
    }

    [Test]
    public void BuildSurfaceAtmosphericLightingStateHonorsManualFogColorOverride()
    {
        Color manualFog = new Color(0.64f, 0.66f, 0.68f, 1f);

        SetPrivateField("_resolvedSkyZenith", new Color(0.08f, 0.14f, 0.24f, 1f));
        SetPrivateField("_resolvedSkyHorizon", new Color(0.16f, 0.21f, 0.3f, 1f));
        SetPrivateField("_resolvedSkyNadir", new Color(0.03f, 0.04f, 0.06f, 1f));
        SetPrivateField("_currentSunAngle", 24f);
        SetPrivateField("_surfaceFogManualColor", manualFog);
        SetPrivateField("_surfaceFogManualColorBlend", 1f);
        SetPrivateField("_surfaceFogSkyColorInfluence", 0f);
        SetPrivateField("_surfaceFogAmbientColorInfluence", 0f);
        SetPrivateField("_surfaceHazeSkyTintInfluence", 0f);

        AtmosphericLightingState state =
            (AtmosphericLightingState)InvokePrivateMethod("BuildSurfaceAtmosphericLightingState");

        Assert.That(state.IsValid, Is.True);
        Assert.That(state.FogColor.r, Is.EqualTo(manualFog.r).Within(0.03f));
        Assert.That(state.FogColor.g, Is.EqualTo(manualFog.g).Within(0.03f));
        Assert.That(state.FogColor.b, Is.EqualTo(manualFog.b).Within(0.03f));
        Assert.That(state.HorizonHazeColor.grayscale, Is.EqualTo(state.FogColor.grayscale).Within(0.06f));
    }

    [Test]
    public void ApplySkyMaterialHazePropertiesUsesAtmosphericLightingStateAsSingleSourceOfTruth()
    {
        AtmosphericLightingState state = new AtmosphericLightingState
        {
            IsValid = 1,
            FogDensity = 0.0024f,
            FogColor = new Color(0.42f, 0.45f, 0.5f, 1f),
            AmbientEquatorColor = new Color(0.31f, 0.34f, 0.38f, 1f),
            SkyZenithColor = new Color(0.08f, 0.14f, 0.24f, 1f),
            SkyHorizonColor = new Color(0.26f, 0.28f, 0.34f, 1f),
            HorizonHazeIntensity = 0.37f,
            HorizonHazeFalloff = 2.15f,
            HorizonHazeSunTintStrength = 0.29f,
            HorizonHazeColor = new Color(0.44f, 0.47f, 0.52f, 1f),
            HorizonMistShelfIntensity = 0.48f,
            HorizonMistShelfHeight = 0.17f,
            HorizonMistShelfSoftness = 0.09f
        };

        SetPrivateField("_surfaceAtmosphericLightingState", state);

        InvokePrivateMethod("ApplySkyMaterialHazeProperties", _skyMaterial);

        Assert.That(_skyMaterial.GetFloat("_HazeIntensity"), Is.EqualTo(0.37f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_HazeFalloff"), Is.EqualTo(2.15f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_HazeSunTintStrength"), Is.EqualTo(0.29f).Within(0.0001f));
        Assert.That(_skyMaterial.GetColor("_HazeColor"), Is.EqualTo(state.HorizonHazeColor));
        Assert.That(_skyMaterial.GetFloat("_HorizonMistShelfIntensity"), Is.EqualTo(0.48f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_HorizonMistShelfHeight"), Is.EqualTo(0.17f).Within(0.0001f));
        Assert.That(_skyMaterial.GetFloat("_HorizonMistShelfSoftness"), Is.EqualTo(0.09f).Within(0.0001f));
    }

    [Test]
    public void SkySystemFollowCameraRejectsStalePositiveFallbackSeaLevelWhenOceanIsUnavailable()
    {
        GameObject followObject = new GameObject("SkyFollowTest");

        try
        {
            SkySystemFollowCamera followCamera = followObject.AddComponent<SkySystemFollowCamera>();
            SetPrivateField(followCamera, "lockToSeaLevel", true);
            SetPrivateField(followCamera, "fallbackSeaLevelY", 4900f);
            SetPrivateField(followCamera, "positionOffset", new Vector3(0f, 12f, 0f));

            float lockedY = (float)InvokePrivateMethod(followCamera, "ResolveLockedY");
            Assert.That(lockedY, Is.EqualTo(OceanSurfaceAtmosphereConstants.DefaultSeaLevel + 12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(followObject);
        }
    }

    [Test]
    public void SkySystemFollowCameraSanitizesStaleZeroFallbackSeaLevel()
    {
        GameObject followObject = new GameObject("SkyFollowFallbackSanitizeTest");

        try
        {
            SkySystemFollowCamera followCamera = followObject.AddComponent<SkySystemFollowCamera>();
            SetPrivateField(followCamera, "lockToSeaLevel", true);
            SetPrivateField(followCamera, "playerMovement", null);
            SetPrivateField(followCamera, "atmosphereManager", null);
            SetPrivateField(followCamera, "_cachedAtmosphereReadModel", null);
            SetPrivateField(followCamera, "fallbackSeaLevelY", 0f);
            SetPrivateField(followCamera, "positionOffset", new Vector3(0f, 12f, 0f));

            float lockedY = (float)InvokePrivateMethod(followCamera, "ResolveLockedY");
            Assert.That(lockedY, Is.EqualTo(OceanSurfaceAtmosphereConstants.DefaultSeaLevel + 12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(followObject);
        }
    }

    [Test]
    public void SkySystemFollowCameraRejectsStaleZeroLiveSeaLevelOwners()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "SkySystemFollowCamera.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("TryResolveSeaLevelY(movementSurfaceY, out float movementSeaLevelY)", source);
        StringAssert.Contains("TryResolveOceanSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY)", source);
        StringAssert.Contains("TryResolveSeaLevelY(atmosphereSeaLevelY, out float resolvedSeaLevelY)", source);
        StringAssert.Contains("TryResolveSeaLevelY(fallbackSeaLevelY, out float seaLevelY)", source);
        StringAssert.Contains("private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
        StringAssert.Contains("private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
        StringAssert.Contains("CacheAtmosphereReadModel(currentService as IAtmosphereReadModel)", source);
        StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext)", source);
        StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerContext();", source);
        StringAssert.Contains("IsAtmosphereReadModelUsable", source);
        StringAssert.Contains("IsPlayerContextUsable", source);
        StringAssert.Contains("IsPlayerMovementUsable", source);
        StringAssert.Contains("IsExplicitFollowCameraUsable(runtimeCamera)", source);
        StringAssert.Contains("if (IsAtmosphereReadModelUsable(atmosphereManager))", source);
        StringAssert.Contains("_cachedAtmosphereReadModel = null;", source);
        StringAssert.Contains("_cachedPlayerContext = null;", source);
        StringAssert.Contains("playerMovement = null;", source);
        StringAssert.Contains("readModel is Behaviour behaviour", source);
        StringAssert.Contains("playerContext is Behaviour behaviour", source);
        StringAssert.Contains("return movement != null && movement.isActiveAndEnabled;", source);
        StringAssert.Contains("math.abs(candidateSeaLevelY) > 0.0001f", source);
        StringAssert.Contains("Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", source);
        StringAssert.Contains("seaLevelY = DefaultSeaLevelY;", source);
        StringAssert.DoesNotContain("if (math.isfinite(movementSurfaceY))", source);
        StringAssert.DoesNotContain("if (math.isfinite(atmosphereSeaLevelY))", source);
        StringAssert.DoesNotContain("return math.isfinite(fallbackSeaLevelY)", source);
        StringAssert.DoesNotContain("_cachedAtmosphereReadModel = currentService as IAtmosphereReadModel;", source);
        StringAssert.DoesNotContain("_cachedPlayerContext = currentService as IPlayerRuntimeContext;", source);
    }

    [Test]
    public void SkySystemFollowCameraPrefersSceneViewCameraOverCachedGameplayCameraInEditMode()
    {
        GameObject followObject = new GameObject("SkyFollowEditModeTest");
        GameObject cachedCameraObject = new GameObject("CachedGameplayCamera");
        SceneView sceneView = null;

        try
        {
            Camera cachedCamera = cachedCameraObject.AddComponent<Camera>();
            cachedCamera.transform.position = new Vector3(900f, 900f, 900f);

            sceneView = EditorWindow.CreateWindow<SceneView>("CodexSceneViewTest");
            sceneView.ShowUtility();
            sceneView.Focus();
            sceneView.camera.transform.position = new Vector3(12f, 34f, 56f);

            SkySystemFollowCamera followCamera = followObject.AddComponent<SkySystemFollowCamera>();
            SetPrivateField(followCamera, "_cachedResolvedCamera", cachedCamera);
            SetPrivateField(followCamera, "followVerticalPosition", true);
            SetPrivateField(followCamera, "positionOffset", Vector3.zero);

            Camera resolvedCamera = (Camera)InvokePrivateMethod(followCamera, "ResolveTargetCamera");

            Assert.That(SceneView.lastActiveSceneView, Is.Not.Null);
            Assert.That(resolvedCamera, Is.EqualTo(SceneView.lastActiveSceneView.camera));
            Assert.That(resolvedCamera.transform.position, Is.EqualTo(new Vector3(12f, 34f, 56f)));
        }
        finally
        {
            if (sceneView != null)
                sceneView.Close();

            Object.DestroyImmediate(cachedCameraObject);
            Object.DestroyImmediate(followObject);
        }
    }

    [Test]
    public void SkySystemFollowCameraAppliesSceneViewFollowOnEnableInEditMode()
    {
        GameObject followObject = new GameObject("SkyFollowOnEnableEditModeTest");
        SceneView sceneView = null;

        try
        {
            sceneView = EditorWindow.CreateWindow<SceneView>("CodexSceneViewOnEnableTest");
            sceneView.ShowUtility();
            sceneView.Focus();

            Vector3 sceneCameraPosition = new Vector3(22f, 44f, 66f);
            sceneView.camera.transform.position = sceneCameraPosition;

            followObject.AddComponent<SkySystemFollowCamera>();

            Assert.That(followObject.transform.position.x, Is.EqualTo(sceneCameraPosition.x).Within(0.01f));
            Assert.That(followObject.transform.position.y, Is.EqualTo(sceneCameraPosition.y).Within(0.01f));
            Assert.That(followObject.transform.position.z, Is.EqualTo(sceneCameraPosition.z).Within(0.01f));
        }
        finally
        {
            if (sceneView != null)
                sceneView.Close();

            Object.DestroyImmediate(followObject);
        }
    }

    [Test]
    public void SkySystemFollowCameraRuntimeRouteDoesNotSceneScanCameras()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "SkySystemFollowCamera.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.DoesNotContain("Camera.GetAllCameras", source);
        StringAssert.DoesNotContain("ResolveTaggedRuntimeMainCamera", source);
        StringAssert.Contains("Camera cachedPlayerCamera = TryResolveCachedPlayerCamera();", source);
        StringAssert.Contains("HectonPlayerMovement movement = playerContext.PlayerMovement;", source);
    }

    [Test]
    public void CelestialEngineRejectsStalePlayerContextForSkyPlacement()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("CachePlayerContext(Hecton8.Core.GlobalRegistry.Player);", source);
        StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext);", source);
        StringAssert.Contains("private IPlayerRuntimeContext ResolveCachedPlayerContext()", source);
        StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", source);
        StringAssert.Contains("playerContext is Behaviour behaviour", source);
        StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolveCachedPlayerContext();", source);
        StringAssert.DoesNotContain("_cachedPlayerContext = currentService as IPlayerRuntimeContext;", source);
        StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", source);
    }

    [Test]
    public void ObserverRelativeCelestialBodyRejectsStaleRuntimeOwnersForSkyPlacement()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "ObserverRelativeCelestialBody.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext);", source);
        StringAssert.Contains("CachePlayerContext(Hecton8.Core.GlobalRegistry.Player);", source);
        StringAssert.Contains("CacheAtmosphereManager(currentService as HectonAtmosphereManager, registeredOwner: true);", source);
        StringAssert.Contains("CacheAtmosphereManager(Hecton8.Core.GlobalRegistry.Atmosphere, registeredOwner: true);", source);
        StringAssert.Contains("private IPlayerRuntimeContext ResolvePlayerContext()", source);
        StringAssert.Contains("private HectonAtmosphereManager ResolveAtmosphereManagerForRead()", source);
        StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", source);
        StringAssert.Contains("private static bool IsAtmosphereManagerUsable(HectonAtmosphereManager manager)", source);
        StringAssert.Contains("private static bool IsPlayerCameraUsable(Camera camera)", source);
        StringAssert.Contains("private static bool IsTransformUsable(Transform target)", source);
        StringAssert.Contains("ClearRuntimeRegistryReferences();", source);
        StringAssert.Contains("playerContext is Behaviour behaviour", source);
        StringAssert.DoesNotContain("_cachedPlayerContext = currentService as IPlayerRuntimeContext;", source);
        StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", source);
    }

    [Test]
    public void ObserverRelativeCelestialBodyPrefersParentLocalDirectionCapture()
    {
        GameObject parentObject = new GameObject("SkyRig");
        GameObject observerObject = new GameObject("Observer");
        GameObject bodyObject = new GameObject("AegirBody");

        try
        {
            bodyObject.transform.SetParent(parentObject.transform, false);
            bodyObject.transform.localPosition = new Vector3(10f, 2f, 5f);
            observerObject.transform.position = new Vector3(5000f, 7000f, -2000f);

            ObserverRelativeCelestialBody body = bodyObject.AddComponent<ObserverRelativeCelestialBody>();
            SetPrivateField(body, "observerTransform", observerObject.transform);
            SetPrivateField(body, "captureInitialDirectionOnEnable", true);

            InvokePrivateMethod(body, "TryCaptureInitialDirection");

            Vector3 expectedDirection = bodyObject.transform.localPosition.normalized;
            Assert.That(Vector3.Dot(body.CurrentDirection, expectedDirection), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(bodyObject);
            Object.DestroyImmediate(observerObject);
            Object.DestroyImmediate(parentObject);
        }
    }

    [Test]
    public void ObserverRelativeCelestialBodyCapturesParentDirectionOnEnableInEditMode()
    {
        GameObject parentObject = new GameObject("SkyRigOnEnable");
        GameObject bodyObject = new GameObject("AegirBodyOnEnable");

        try
        {
            bodyObject.transform.SetParent(parentObject.transform, false);
            bodyObject.transform.localPosition = new Vector3(6f, 3f, 2f);

            ObserverRelativeCelestialBody body = bodyObject.AddComponent<ObserverRelativeCelestialBody>();

            Vector3 expectedDirection = bodyObject.transform.localPosition.normalized;
            Assert.That(Vector3.Dot(body.CurrentDirection, expectedDirection), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(bodyObject);
            Object.DestroyImmediate(parentObject);
        }
    }

    [Test]
    public void ObserverRelativeCelestialBodyCurrentDirectionDoesNotCacheReferences()
    {
        GameObject parentObject = new GameObject("OrbitParent");
        GameObject childObject = new GameObject("OrbitChild");

        try
        {
            parentObject.transform.position = new Vector3(3f, 2f, 7f);
            parentObject.AddComponent<ObserverRelativeCelestialBody>();

            ObserverRelativeCelestialBody child = childObject.AddComponent<ObserverRelativeCelestialBody>();
            SetPrivateField(child, "placementMode", ObserverRelativeCelestialBody.CelestialPlacementMode.OrbitAroundParent);
            SetPrivateField(child, "parentBodyTransform", parentObject.transform);
            SetPrivateField(child, "captureInitialDirectionOnEnable", false);
            SetPrivateField(child, "fixedDirection", Vector3.forward);

            Assert.That(GetPrivateField(child, "_parentObserverRelativeBody"), Is.Null);

            _ = child.CurrentDirection;

            Assert.That(GetPrivateField(child, "_parentObserverRelativeBody"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(childObject);
            Object.DestroyImmediate(parentObject);
        }
    }

    [Test]
    public void FirmamentMemoryBudgetResolvesContinuouslyBetweenSurvivalAndOverkill()
    {
        float survival = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveFirmamentMemoryBudget01",
            2048);
        float middle = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveFirmamentMemoryBudget01",
            7168);
        float overkill = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveFirmamentMemoryBudget01",
            12288);

        Assert.That(survival, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(middle, Is.GreaterThan(0.49f).And.LessThan(0.51f));
        Assert.That(overkill, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void FirmamentPowerOfTwoFloorNeverExceedsContinuousBudget()
    {
        int underTwoK = (int)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolvePowerOfTwoFloor",
            1800);
        int exactFourK = (int)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolvePowerOfTwoFloor",
            4096);
        int betweenFourAndEightK = (int)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolvePowerOfTwoFloor",
            6200);

        Assert.That(underTwoK, Is.EqualTo(1024));
        Assert.That(exactFourK, Is.EqualTo(4096));
        Assert.That(betweenFourAndEightK, Is.EqualTo(4096));
    }

    [Test]
    public void CelestialLightReadabilityQualityMapsProjectQualityTiers()
    {
        float surface = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            0,
            7);
        float abyss = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            1,
            7);
        float orbit = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            2,
            7);
        float quest = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            3,
            7);
        float handheld = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            4,
            7);
        float compactPc = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            5,
            7);
        float leviathan = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            6,
            7);
        float unknownClamped = (float)InvokePrivateStaticMethod(
            typeof(HectonCelestialEngine),
            "ResolveCelestialQualityFromUnityTier",
            200,
            9);

        Assert.That(surface, Is.EqualTo(0.72f).Within(0.0001f));
        Assert.That(abyss, Is.EqualTo(0.55f).Within(0.0001f));
        Assert.That(orbit, Is.EqualTo(0.90f).Within(0.0001f));
        Assert.That(quest, Is.EqualTo(0.64f).Within(0.0001f));
        Assert.That(handheld, Is.EqualTo(0.58f).Within(0.0001f));
        Assert.That(compactPc, Is.EqualTo(0.76f).Within(0.0001f));
        Assert.That(leviathan, Is.EqualTo(1.00f).Within(0.0001f));
        Assert.That(unknownClamped, Is.InRange(0.55f, 1f));
    }

    [Test]
    public void SurfaceWeatherDirectorResetBindsOwnedVfxRigInEditMode()
    {
        GameObject directorObject = new GameObject("SurfaceWeatherDirectorResetTest");
        GameObject rigObject = new GameObject("SurfaceWeatherVfxRig");

        try
        {
            HectonSurfaceWeatherDirector director = directorObject.AddComponent<HectonSurfaceWeatherDirector>();
            rigObject.transform.SetParent(directorObject.transform, false);
            LogAssert.Expect(
                LogType.Log,
                "[SurfaceWeatherVfxRig] Missing authored LineRenderer. Lightning presentation disabled until authoredBoltRenderer is assigned.");
            SurfaceWeatherVfxRig rig = rigObject.AddComponent<SurfaceWeatherVfxRig>();

            SetPrivateField(director, "weatherVfxRig", null);
            InvokePrivateMethod(director, "Reset");

            Assert.That(GetPrivateField(director, "weatherVfxRig"), Is.SameAs(rig));
        }
        finally
        {
            Object.DestroyImmediate(rigObject);
            Object.DestroyImmediate(directorObject);
        }
    }

    [Test]
    public void SurfaceWeatherDirectorOnValidateClampsSuppressionDepthInEditMode()
    {
        GameObject directorObject = new GameObject("SurfaceWeatherDirectorValidateTest");

        try
        {
            HectonSurfaceWeatherDirector director = directorObject.AddComponent<HectonSurfaceWeatherDirector>();
            SetPrivateField(director, "surfaceActivationDepth", 9f);
            SetPrivateField(director, "surfaceSuppressionDepth", 3f);

            InvokePrivateMethod(director, "OnValidate");

            Assert.That(GetPrivateField(director, "surfaceSuppressionDepth"), Is.EqualTo(9f));
        }
        finally
        {
            Object.DestroyImmediate(directorObject);
        }
    }

    [Test]
    public void ObserverRelativeCelestialBodyFixedDirectionFollowsObserverOneToOneWithConstantOffset()
    {
        GameObject observerObject = new GameObject("Observer");
        GameObject bodyObject = new GameObject("AegirBody");

        try
        {
            ObserverRelativeCelestialBody body = bodyObject.AddComponent<ObserverRelativeCelestialBody>();
            SetPrivateField(body, "observerTransform", observerObject.transform);
            SetPrivateField(body, "captureInitialDirectionOnEnable", false);
            SetPrivateField(body, "fixedDirection", new Vector3(0.92f, 0.04f, 0.31f));
            SetPrivateField(body, "fixedVerticalOffset", 0.0564f);
            SetPrivateField(body, "anchorDistance", 21000f);

            observerObject.transform.position = new Vector3(120f, 4900f, -45f);
            InvokePrivateMethod(body, "ApplyPlacement");

            Vector3 baseDirection = new Vector3(0.92f, 0.04f, 0.31f).normalized;
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(baseDirection, Vector3.up).normalized;
            Vector3 expectedOffsetDirection = (horizontalDirection + (Vector3.up * 0.0564f)).normalized;
            Vector3 expectedWorldPosition = observerObject.transform.position + (expectedOffsetDirection * 50000f);

            Assert.That(bodyObject.transform.position.x, Is.EqualTo(expectedWorldPosition.x).Within(0.01f));
            Assert.That(bodyObject.transform.position.y, Is.EqualTo(expectedWorldPosition.y).Within(0.01f));
            Assert.That(bodyObject.transform.position.z, Is.EqualTo(expectedWorldPosition.z).Within(0.01f));

            Vector3 previousOffset = bodyObject.transform.position - observerObject.transform.position;

            observerObject.transform.position = new Vector3(120f, 6000f, -45f);
            body.Tick(0f);

            Vector3 nextOffset = bodyObject.transform.position - observerObject.transform.position;
            Assert.That(nextOffset.x, Is.EqualTo(previousOffset.x).Within(0.01f));
            Assert.That(nextOffset.y, Is.EqualTo(previousOffset.y).Within(0.01f));
            Assert.That(nextOffset.z, Is.EqualTo(previousOffset.z).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(observerObject);
            Object.DestroyImmediate(bodyObject);
        }
    }

    [Test]
    public void AtmosphereManagerSyncEditorPreviewFromSunTransformUpdatesTimeOfDay()
    {
        GameObject sunObject = new GameObject("EditorSun");
        GameObject atmosphereObject = new GameObject("AtmosphereManager");

        try
        {
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            HectonAtmosphereManager atmosphereManager = atmosphereObject.AddComponent<HectonAtmosphereManager>();
            SetPrivateField(atmosphereManager, "_sunLight", sunLight);
            SetPrivateField(atmosphereManager, "_sunOrbitalYAngle", 0f);
            SetPrivateField(atmosphereManager, "_orbitalInclination", 0f);
            SetPrivateField(atmosphereManager, "_cycleDuration", 3600f);

            sunLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sunLight.transform.hasChanged = true;

            bool changed = atmosphereManager.SyncEditorPreviewFromSunTransform();

            Assert.That(changed, Is.True);
            Assert.That(atmosphereManager.SunAngle, Is.EqualTo(90f).Within(0.01f));
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.25f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(atmosphereObject);
            Object.DestroyImmediate(sunObject);
        }
    }

    [Test]
    public void AtmosphereManagerSetTimeOfDayRefreshesSunPresentationAndRejectsBadData()
    {
        GameObject sunObject = new GameObject("RuntimeSun");
        GameObject atmosphereObject = new GameObject("AtmosphereManager");
        int timeOfDayShaderId = Shader.PropertyToID("_HectonTimeOfDay01");
        float previousTimeOfDayGlobal = Shader.GetGlobalFloat(timeOfDayShaderId);

        try
        {
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            HectonAtmosphereManager atmosphereManager = atmosphereObject.AddComponent<HectonAtmosphereManager>();
            SetPrivateField(atmosphereManager, "_sunLight", sunLight);
            SetPrivateField(atmosphereManager, "_sunOrbitalYAngle", 0f);
            SetPrivateField(atmosphereManager, "_orbitalInclination", 0f);
            SetPrivateField(atmosphereManager, "_cycleDuration", 3600f);

            Assert.That(atmosphereManager.TrySetTimeOfDay(0.25f), Is.True);

            Vector3 validForward = sunLight.transform.forward;
            Assert.That(atmosphereManager.SunAngle, Is.EqualTo(90f).Within(0.01f));
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(Vector3.Angle(Vector3.forward, validForward), Is.GreaterThan(30f));
            Assert.That(Shader.GetGlobalFloat(timeOfDayShaderId), Is.EqualTo(0.25f).Within(0.001f));

            Assert.That(atmosphereManager.TrySetTimeOfDay(float.NaN), Is.False);
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(Vector3.Angle(validForward, sunLight.transform.forward), Is.LessThan(0.01f));
            Assert.That(Shader.GetGlobalFloat(timeOfDayShaderId), Is.EqualTo(0.25f).Within(0.001f));
        }
        finally
        {
            Shader.SetGlobalFloat(timeOfDayShaderId, previousTimeOfDayGlobal);
            Object.DestroyImmediate(atmosphereObject);
            Object.DestroyImmediate(sunObject);
        }
    }

    [Test]
    public void AtmosphereManagerSaveLoadPersistsTimeOfDayAndIgnoresBadPayload()
    {
        GameObject sunObject = new GameObject("RuntimeSun");
        GameObject atmosphereObject = new GameObject("AtmosphereManager");

        try
        {
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            HectonAtmosphereManager atmosphereManager = atmosphereObject.AddComponent<HectonAtmosphereManager>();
            SetPrivateField(atmosphereManager, "_sunLight", sunLight);
            SetPrivateField(atmosphereManager, "_sunOrbitalYAngle", 0f);
            SetPrivateField(atmosphereManager, "_orbitalInclination", 0f);
            SetPrivateField(atmosphereManager, "_cycleDuration", 3600f);

            Assert.That(atmosphereManager.TrySetTimeOfDay(0.62f), Is.True);
            SaveData saved = SaveData.CreateNew(0d);
            atmosphereManager.PopulateSaveData(saved);

            Assert.That(saved.celestialLightPhaseSerialized, Is.True);
            Assert.That(saved.celestialLightTimeOfDay01, Is.EqualTo(0.62f).Within(0.001f));

            Assert.That(atmosphereManager.TrySetTimeOfDay(0.1f), Is.True);
            LogAssert.Expect(LogType.Warning, "[HectonAtmosphere] Celestial light phase save payload restored through atmosphere fallback: celestial owner unavailable.");
            atmosphereManager.LoadFromSaveData(saved);
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.62f).Within(0.001f));

            SaveData missing = SaveData.CreateNew(0d);
            missing.celestialLightPhaseSerialized = false;
            missing.celestialLightTimeOfDay01 = 0.9f;
            atmosphereManager.LoadFromSaveData(missing);
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.62f).Within(0.001f));

            SaveData bad = SaveData.CreateNew(0d);
            bad.celestialLightPhaseSerialized = true;
            bad.celestialLightTimeOfDay01 = float.NaN;
            LogAssert.Expect(LogType.Warning, "[HectonAtmosphere] Celestial light phase save payload ignored: non-finite time-of-day.");
            atmosphereManager.LoadFromSaveData(bad);
            Assert.That(atmosphereManager.TimeOfDay, Is.EqualTo(0.62f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(atmosphereObject);
            Object.DestroyImmediate(sunObject);
        }
    }

    [Test]
    public void RuntimeTimeOfDayBridgeRefreshesAtmosphereAndCelestialReadability()
    {
        string atmospherePath = Path.Combine("Assets", "_Project", "Scripts", "HectonAtmosphereManager.cs");
        string celestialPath = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string saveDataPath = Path.Combine("Assets", "_Project", "Scripts", "SaveData.cs");
        string saveCodecPath = Path.Combine("Assets", "_Project", "Scripts", "SaveBinaryPayloadCodec.cs");
        string saveMigrationPath = Path.Combine("Assets", "_Project", "Scripts", "SaveDataMigration.cs");
        string atmosphereSource = File.ReadAllText(atmospherePath).Replace("\r\n", "\n");
        string celestialSource = File.ReadAllText(celestialPath).Replace("\r\n", "\n");
        string saveDataSource = File.ReadAllText(saveDataPath).Replace("\r\n", "\n");
        string saveCodecSource = File.ReadAllText(saveCodecPath).Replace("\r\n", "\n");
        string saveMigrationSource = File.ReadAllText(saveMigrationPath).Replace("\r\n", "\n");

        StringAssert.Contains("ISaveable", atmosphereSource);
        StringAssert.Contains("public int SavePriority                => 4;", atmosphereSource);
        StringAssert.Contains("public int LoadPriority                => 4;", atmosphereSource);
        StringAssert.Contains("TryRegisterSaveParticipant();", atmosphereSource);
        StringAssert.Contains("TryUnregisterSaveParticipant();", atmosphereSource);
        StringAssert.Contains("case GlobalRegistryServiceSlot.Save:", atmosphereSource);
        StringAssert.Contains("public void PopulateSaveData(SaveData data)", atmosphereSource);
        StringAssert.Contains("data.celestialLightPhaseSerialized = true;", atmosphereSource);
        StringAssert.Contains("data.celestialLightTimeOfDay01 = Mathf.Repeat(timeOfDay01, 1f);", atmosphereSource);
        StringAssert.Contains("public void LoadFromSaveData(SaveData data)", atmosphereSource);
        StringAssert.Contains("!data.celestialLightPhaseSerialized", atmosphereSource);
        StringAssert.Contains("_CelestialLightPhaseLoadWarningHash", atmosphereSource);
        StringAssert.Contains("_CelestialLightPhaseLoadNonFiniteContextHash", atmosphereSource);
        StringAssert.Contains("_CelestialLightPhaseLoadOwnerFallbackContextHash", atmosphereSource);
        StringAssert.Contains("_CelestialLightPhaseLoadRejectedContextHash", atmosphereSource);
        StringAssert.Contains("celestial.RecordCelestialLightPhaseLoadNonFinite(data.celestialLightTimeOfDay01);", atmosphereSource);
        StringAssert.Contains("celestial.RecordCelestialLightPhaseLoadFallback(timeOfDay01);", atmosphereSource);
        StringAssert.Contains("celestial.RecordCelestialLightPhaseLoadRejected(timeOfDay01);", atmosphereSource);
        StringAssert.Contains("ReportCelestialLightPhaseLoadWarning(\n                    \"ignored: non-finite time-of-day\"", atmosphereSource);
        StringAssert.Contains("ReportCelestialLightPhaseLoadWarning(\n                    \"restored through atmosphere fallback: celestial owner unavailable\"", atmosphereSource);
        StringAssert.Contains("ReportCelestialLightPhaseLoadWarning(\n                \"ignored: runtime owner rejected phase\"", atmosphereSource);
        StringAssert.Contains("PublishCelestialLightPhaseLoadWarningTelemetry(contextHash);", atmosphereSource);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(\n                _CelestialLightPhaseLoadWarningHash", atmosphereSource);
        StringAssert.Contains("celestial.TryApplyRuntimeTimeOfDay01(timeOfDay01)", atmosphereSource);
        StringAssert.Contains("public bool TrySetTimeOfDay(float normalized)", atmosphereSource);
        StringAssert.Contains("if (!math.isfinite(normalized))", atmosphereSource);
        StringAssert.Contains("ApplyTimeOfDayImmediate(math.saturate(normalized));", atmosphereSource);
        StringAssert.Contains("RotateSun();", atmosphereSource);
        StringAssert.Contains("EnvironmentState resolvedState = ResolveState();", atmosphereSource);
        StringAssert.Contains("_transitionProgress = 1f;", atmosphereSource);
        StringAssert.Contains("QueueGiantAbyssLight();", atmosphereSource);
        StringAssert.Contains("QueueAbyssAtmospherePresentation();", atmosphereSource);
        StringAssert.Contains("FlushLateFramePresentation();", atmosphereSource);

        StringAssert.Contains("case GlobalRegistryServiceSlot.AtmosphereRuntime:", celestialSource);
        StringAssert.Contains("CacheAtmosphereManager(GlobalRegistry.Atmosphere);", celestialSource);
        StringAssert.Contains("private HectonAtmosphereManager ResolveAtmosphereManagerForRead()", celestialSource);
        StringAssert.Contains("private static bool IsAtmosphereManagerUsable(HectonAtmosphereManager atmosphereManager)", celestialSource);
        StringAssert.Contains("HectonAtmosphereManager registeredAtmosphere = GlobalRegistry.Atmosphere;", celestialSource);
        StringAssert.Contains("_atmosphereManager = IsAtmosphereManagerUsable(registeredAtmosphere)", celestialSource);
        StringAssert.Contains("HectonAtmosphereManager atmosphereManager = ResolveAtmosphereManagerForRead();", celestialSource);
        StringAssert.Contains("public bool TryApplyRuntimeTimeOfDay01(float timeOfDay01)", celestialSource);
        StringAssert.Contains("if (!math.isfinite(timeOfDay01))", celestialSource);
        StringAssert.Contains("WriteCelestialLightPhasePersistenceBlackBox(timeOfDay01, CelestialBlackBoxFlagSavePhaseNonFinite);", celestialSource);
        StringAssert.Contains("float normalizedTimeOfDay01 = math.saturate(timeOfDay01);", celestialSource);
        StringAssert.Contains("atmosphereManager.TrySetTimeOfDay(normalizedTimeOfDay01)", celestialSource);
        StringAssert.Contains("WriteCelestialLightPhasePersistenceBlackBox(normalizedTimeOfDay01, CelestialBlackBoxFlagSavePhaseRejected);", celestialSource);
        StringAssert.Contains("RefreshCelestialRuntimeAfterTimeOfDayRestore();", celestialSource);
        StringAssert.Contains("private void RefreshCelestialRuntimeAfterTimeOfDayRestore()", celestialSource);
        StringAssert.Contains("UpdateSunPosition(0f);", celestialSource);
        StringAssert.Contains("RefreshCelestialRuntimeSnapshotSunDirectionAfterTimeOfDayRestore();", celestialSource);
        StringAssert.Contains("PublishCelestialRuntimeSnapshot(publishGlobalSnapshot: true);", celestialSource);
        StringAssert.Contains("PublishGlobalTimeSyncSignal(in snapshot);", celestialSource);
        StringAssert.Contains("PublishCelestialLightReadabilitySnapshot(_currentDepthMeters);", celestialSource);
        StringAssert.Contains("FlushCelestialRuntimeSnapshotShaderGlobals();", celestialSource);
        StringAssert.Contains("PublishCelestialRuntimeSnapshot(publishGlobalSnapshot: true);\n            PublishCelestialLightReadabilitySnapshot(_currentDepthMeters);\n            FlushCelestialRuntimeSnapshotShaderGlobals();", celestialSource);
        StringAssert.Contains("TryRaiseCelestialSunAngleChanged(_currentSunAngle);", celestialSource);
        StringAssert.Contains("private void RefreshCelestialRuntimeSnapshotSunDirectionAfterTimeOfDayRestore()", celestialSource);
        StringAssert.Contains("snapshot.SunDirection = NormalizeVisualRsqrt(_resolvedSunDirection", celestialSource);
        StringAssert.Contains("snapshot.Sequence = _celestialRuntimeSequence + 1u;", celestialSource);
        StringAssert.Contains("CelestialBlackBoxFlagSavePhaseRestored = 1u << 27", celestialSource);
        StringAssert.Contains("CelestialBlackBoxFlagSavePhaseFallback = 1u << 28", celestialSource);
        StringAssert.Contains("CelestialBlackBoxFlagSavePhaseRejected = 1u << 29", celestialSource);
        StringAssert.Contains("CelestialBlackBoxFlagSavePhaseNonFinite = 1u << 30", celestialSource);
        StringAssert.Contains("WriteCelestialLightPhasePersistenceBlackBox(normalizedTimeOfDay01, CelestialBlackBoxFlagSavePhaseRestored);", celestialSource);
        StringAssert.Contains("internal void RecordCelestialLightPhaseLoadFallback(float timeOfDay01)", celestialSource);
        StringAssert.Contains("internal void RecordCelestialLightPhaseLoadRejected(float timeOfDay01)", celestialSource);
        StringAssert.Contains("internal void RecordCelestialLightPhaseLoadNonFinite(float timeOfDay01)", celestialSource);
        StringAssert.Contains("private void WriteCelestialLightPhasePersistenceBlackBox(float timeOfDay01, uint persistenceFlag)", celestialSource);
        StringAssert.DoesNotContain("_atmosphereManager.TimeOfDay", celestialSource);

        StringAssert.Contains("CelestialLightPhasePersistenceVersion = 84", saveDataSource);
        StringAssert.Contains("ProceduralTerrainIdentityContractPersistenceVersion = 85", saveDataSource);
        StringAssert.Contains("CurrentVersion = ProceduralTerrainIdentityContractPersistenceVersion", saveDataSource);
        StringAssert.Contains("public bool celestialLightPhaseSerialized;", saveDataSource);
        StringAssert.Contains("public float celestialLightTimeOfDay01 = CelestialLightTimeOfDayDefault;", saveDataSource);
        StringAssert.Contains("CelestialLightPhaseSaveVersion = SaveData.CelestialLightPhasePersistenceVersion", saveCodecSource);
        StringAssert.Contains("WriteCelestialLightPhase(ref writer, data)", saveCodecSource);
        StringAssert.Contains("ReadCelestialLightPhase(ref reader, data.version, data)", saveCodecSource);
        StringAssert.Contains("if (saveDataVersion < CelestialLightPhaseSaveVersion)", saveCodecSource);
        StringAssert.Contains("SanitizeCelestialLightPhase(data);", saveCodecSource);
        StringAssert.Contains("EnsureCelestialLightPhase(data, sourceVersion, steps)", saveMigrationSource);
        StringAssert.Contains("celestial light phase state repaired", saveMigrationSource);
    }

    [Test]
    public void CelestialEngineConsumesAtmosphereDirtySignalAfterManualSunMove()
    {
        GameObject sunObject = new GameObject("EditorSun");
        GameObject atmosphereObject = new GameObject("AtmosphereManager");

        try
        {
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            HectonAtmosphereManager atmosphereManager = atmosphereObject.AddComponent<HectonAtmosphereManager>();
            SetPrivateField(atmosphereManager, "_sunLight", sunLight);
            SetPrivateField(atmosphereManager, "_sunOrbitalYAngle", 0f);
            SetPrivateField(atmosphereManager, "_orbitalInclination", 0f);

            SetPrivateField("_atmosphereManager", atmosphereManager);
            SetPrivateField("sunLight", sunLight);
            SetPrivateField("_smoothedOcclusionFactor", 0f);

            sunLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sunLight.transform.hasChanged = true;

            Assert.That(atmosphereManager.SyncEditorPreviewFromSunTransform(), Is.True);

            bool consumed = (bool)InvokePrivateMethod("TryConsumeEditorSunTransformChange");
            InvokePrivateMethod("RunCelestialTimeline", 0f);

            Assert.That(consumed, Is.True);
            Assert.That(_engine.DayNightBlend, Is.LessThan(0.01f));
            Assert.That(_skyMaterial.GetFloat("_NightBlend"), Is.LessThan(0.01f));
            Assert.That(_skyMaterial.GetFloat("_SunElevation"), Is.GreaterThan(0.99f));
        }
        finally
        {
            Object.DestroyImmediate(atmosphereObject);
            Object.DestroyImmediate(sunObject);
        }
    }

    [Test]
    public void ClearCelestialRuntimeSnapshotDoesNotPublishGlobalSnapshotInEditMode()
    {
        CelestialRuntimeSnapshot originalSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;
        uint originalSequence = GlobalRegistry.CelestialRuntimeSnapshotSequence;
        CelestialRuntimeSnapshot seededSnapshot = new CelestialRuntimeSnapshot
        {
            AbsoluteUniverseTime = 123.0,
            Flags = (uint)CelestialRuntimeFlags.Valid,
            Sequence = 73u
        };

        try
        {
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialRuntimeSnapshot", seededSnapshot);
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialRuntimeSnapshotSequence", unchecked((int)seededSnapshot.Sequence));

            InvokePrivateMethod("ClearCelestialRuntimeSnapshot");

            Assert.That(GlobalRegistry.CelestialRuntimeSnapshotSequence, Is.EqualTo(seededSnapshot.Sequence));
            Assert.That(GlobalRegistry.CelestialRuntimeSnapshot.Sequence, Is.EqualTo(seededSnapshot.Sequence));
            Assert.That(GlobalRegistry.CelestialRuntimeSnapshot.Flags, Is.EqualTo((uint)CelestialRuntimeFlags.Valid));
        }
        finally
        {
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialRuntimeSnapshot", originalSnapshot);
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialRuntimeSnapshotSequence", unchecked((int)originalSequence));
        }
    }

    [Test]
    public void CelestialLightReadabilityReadModelTracksPublishAndClear()
    {
        CelestialLightReadabilitySnapshot originalSnapshot = GlobalRegistry.CelestialLightReadabilitySnapshot;
        uint originalSequence = GlobalRegistry.CelestialLightReadabilitySequence;
        CelestialLightReadabilitySnapshot seededSnapshot = new CelestialLightReadabilitySnapshot
        {
            DepthMeters = 250f,
            UnderwaterVisibilityMeters = 36f,
            AmbientReadability01 = 0.42f,
            Flags = (uint)CelestialLightReadabilityFlags.Valid,
            DepthStratum = (uint)CelestialLightDepthStratum.Mesophotic100To500Meters,
            Sequence = 91u
        };

        try
        {
            InvokePrivateStaticMethod(
                typeof(GlobalRegistry),
                "PublishCelestialLightReadabilitySnapshot",
                seededSnapshot);

            ICelestialLightReadabilityReadModel readModel = GlobalRegistry.CelestialLightReadabilityReadModel;
            Assert.That(GlobalRegistry.CelestialLightReadabilitySequence, Is.EqualTo(seededSnapshot.Sequence));
            Assert.That(GlobalRegistry.CelestialLightReadabilitySnapshot.Sequence, Is.EqualTo(seededSnapshot.Sequence));
            Assert.That(readModel.LightReadabilitySequence, Is.EqualTo(seededSnapshot.Sequence));
            Assert.That(readModel.LightReadabilitySnapshot.DepthMeters, Is.EqualTo(250f).Within(0.001f));
            Assert.That(readModel.LightReadabilitySnapshot.DepthStratum, Is.EqualTo((uint)CelestialLightDepthStratum.Mesophotic100To500Meters));

            CelestialLightReadabilitySnapshot emptySnapshot = default;
            InvokePrivateStaticMethod(
                typeof(GlobalRegistry),
                "PublishCelestialLightReadabilitySnapshot",
                emptySnapshot);

            Assert.That(GlobalRegistry.CelestialLightReadabilitySequence, Is.EqualTo(0u));
            Assert.That(GlobalRegistry.CelestialLightReadabilitySnapshot.Flags, Is.EqualTo(0u));
            Assert.That(readModel.LightReadabilitySequence, Is.EqualTo(0u));
            Assert.That(readModel.LightReadabilitySnapshot.Flags, Is.EqualTo(0u));
        }
        finally
        {
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialLightReadabilitySnapshot", originalSnapshot);
            SetPrivateStaticField(typeof(GlobalRegistry), "_celestialLightReadabilitySequence", unchecked((int)originalSequence));
        }
    }

    [Test]
    public void CelestialEngineOnDisableUnregistersHotSwapBeforeClearingRuntimeCaches()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        int start = source.IndexOf("private void OnDisable()", System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        int end = source.IndexOf("private void OnDestroy()", start, System.StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start));
        string onDisable = source.Substring(start, end - start);

        StringAssert.Contains("TryUnregisterHotSwapListener();", onDisable);
        StringAssert.Contains("ClearCelestialTruthReadCache();", onDisable);
        Assert.That(
            onDisable.IndexOf("TryUnregisterHotSwapListener();", System.StringComparison.Ordinal),
            Is.LessThan(onDisable.IndexOf("ClearCelestialTruthReadCache();", System.StringComparison.Ordinal)));
    }

    [Test]
    public void CelestialEngineClearsAegirMaterialCacheAfterTextureRestoreOnTeardown()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        int disableStart = source.IndexOf("private void OnDisable()", System.StringComparison.Ordinal);
        Assert.That(disableStart, Is.GreaterThanOrEqualTo(0));
        int destroyStart = source.IndexOf("private void OnDestroy()", disableStart, System.StringComparison.Ordinal);
        Assert.That(destroyStart, Is.GreaterThan(disableStart));
        int refreshStart = source.IndexOf("private void RefreshColdRuntimeDependencies()", destroyStart, System.StringComparison.Ordinal);
        Assert.That(refreshStart, Is.GreaterThan(destroyStart));
        string onDisable = source.Substring(disableStart, destroyStart - disableStart);
        string onDestroy = source.Substring(destroyStart, refreshStart - destroyStart);

        StringAssert.Contains("RestoreCelestialTextureDefaults();", onDisable);
        StringAssert.Contains("ClearAegirMaterialRuntimeCache();", onDisable);
        StringAssert.Contains("RestoreCelestialTextureDefaults();", onDestroy);
        StringAssert.Contains("ClearAegirMaterialRuntimeCache();", onDestroy);
        Assert.That(
            onDisable.IndexOf("RestoreCelestialTextureDefaults();", System.StringComparison.Ordinal),
            Is.LessThan(onDisable.IndexOf("ClearAegirMaterialRuntimeCache();", System.StringComparison.Ordinal)));
        Assert.That(
            onDestroy.IndexOf("RestoreCelestialTextureDefaults();", System.StringComparison.Ordinal),
            Is.LessThan(onDestroy.IndexOf("ClearAegirMaterialRuntimeCache();", System.StringComparison.Ordinal)));

        int clearStart = source.IndexOf("private void ClearAegirMaterialRuntimeCache()", System.StringComparison.Ordinal);
        Assert.That(clearStart, Is.GreaterThanOrEqualTo(0));
        int clearEnd = source.IndexOf("private static Texture GetMaterialTexture", clearStart, System.StringComparison.Ordinal);
        Assert.That(clearEnd, Is.GreaterThan(clearStart));
        string clearBody = source.Substring(clearStart, clearEnd - clearStart);
        StringAssert.Contains("aegirRenderer.SetPropertyBlock(null);", clearBody);
        StringAssert.Contains("_aegirMPB.Clear();", clearBody);
        StringAssert.Contains("_aegirSharedMaterial = null;", clearBody);
        StringAssert.Contains("_aegirMainTexDefault = null;", clearBody);
        StringAssert.Contains("_aegirDetailTexDefault = null;", clearBody);
        StringAssert.Contains("_aegirEmissionMapDefault = null;", clearBody);
        StringAssert.Contains("_aegirCelestialOcclusionTexDefault = null;", clearBody);
    }

    [Test]
    public void CelestialEnginePublishesAegirSkyProjectionGlobalsFromRuntimeSnapshot()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("AegirSkyProjectionProfile", source);
        StringAssert.Contains("PublishAegirSkyProjectionGlobals(aegirDirection);", source);
        StringAssert.Contains("Shader.SetGlobalVector(\n                _ID_H8AegirPlanetCenterRadius", source);
        StringAssert.Contains("Shader.SetGlobalVector(\n                _ID_H8AegirSunDirection", source);
        StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirFlowPhaseValid, 1f);", source);
        StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission());", source);
        StringAssert.Contains("ResolveAegirSkyProjectionQuality01", source);
        StringAssert.Contains("return math.max(math.saturate(profile.minimumQuality), quality);", source);
        StringAssert.Contains("ResolveAegirSkyProjectionVisibility01", source);
        StringAssert.Contains("ResolveAegirSkyProjectionStormEmission", source);
        StringAssert.Contains("_AegirStormEmissionInvalidWarningHash", source);
        StringAssert.Contains("AegirStormEmissionWarningCooldownFrames", source);
        StringAssert.Contains("ReportAegirStormEmissionInvalidIfNeeded", source);
        StringAssert.Contains("ClearAegirSkyProjectionGlobals();", source);
        StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirStormEmission, 1f);", source);
    }

    [Test]
    public void CelestialEnginePublishesAegirWaterConsumerGlobalsFromRuntimeSnapshot()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("PublishOceanCelestialProjectionGlobals(aegirDirection);", source);
        StringAssert.Contains("private void PublishOceanCelestialProjectionGlobals(Vector4 aegirDirection)", source);
        StringAssert.Contains("_ID_HectonEclipseWaterShadowParams", source);
        StringAssert.Contains("_ID_HectonEclipseWaterShadowDirection", source);
        StringAssert.Contains("_ID_HectonRingCausticsParams", source);
        StringAssert.Contains("_ID_HectonRingCausticsDirection", source);
        StringAssert.Contains("ResolveAupOceanShadowCenterRuntimeXZ(planarDirection, travel - radius)", source);
        StringAssert.Contains("TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)", source);
        StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonEclipseWaterShadowParams, Vector4.zero);", source);
        StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonEclipseWaterShadowDirection, Vector4.zero);", source);
        StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonRingCausticsParams, Vector4.zero);", source);
        StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonRingCausticsDirection, Vector4.zero);", source);
    }

    [Test]
    public void CelestialEngineClearSurfaceWeatherOverrideImmediatelyRestoresAegirMaterialsInPlayMode()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        int start = source.IndexOf("internal void ClearSurfaceWeatherOverride()", System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        int end = source.IndexOf("public float SunElevation", start, System.StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start));
        string clearBody = source.Substring(start, end - start);

        StringAssert.Contains("_surfaceWeatherOverrideActive = false;", clearBody);
        StringAssert.Contains("_surfaceWeatherFogOverrideActive = false;", clearBody);
        StringAssert.Contains("_surfaceWeatherStormEmissionMultiplier = 1f;", clearBody);
        StringAssert.Contains("if (Application.isPlaying)", clearBody);
        StringAssert.Contains("UpdateSkyMaterial();", clearBody);
        StringAssert.Contains("UpdateAegirMaterial();", clearBody);
        Assert.That(
            clearBody.IndexOf("_surfaceWeatherStormEmissionMultiplier = 1f;", System.StringComparison.Ordinal),
            Is.LessThan(clearBody.IndexOf("UpdateAegirMaterial();", System.StringComparison.Ordinal)));
    }

    [Test]
    public void CelestialEngineDuplicateRuntimeOwnerDoesNotClearActiveProjectionGlobals()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("TryClaimCelestialRuntimeAuthority()", source);
        StringAssert.Contains("DisableDuplicateCelestialPresentation()", source);
        StringAssert.Contains("PublishAegirPresentationWarning(_AegirDuplicateOwnerWarningHash, 1f);", source);
        StringAssert.Contains("bool shouldClearRuntimeSnapshot = !Application.isPlaying || _hasCelestialRuntimeAuthority;", source);
        StringAssert.Contains("if (shouldClearRuntimeSnapshot)\n                ClearCelestialRuntimeSnapshot();", source);
        StringAssert.Contains("ReleaseCelestialRuntimeAuthority();", source);
    }

    [Test]
    public void CelestialEnginePublishesAegirPresentationFailureTelemetry()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("_AegirPresentationContextHash", source);
        StringAssert.Contains("_AegirDuplicateOwnerWarningHash", source);
        StringAssert.Contains("_AegirMissingMaterialWarningHash", source);
        StringAssert.Contains("_AegirMissingBandTextureWarningHash", source);
        StringAssert.Contains("private static void PublishAegirPresentationWarning(uint warningHash, float scalarValue)", source);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(\n                warningHash,\n                _AegirPresentationContextHash,\n                scalarValue);", source);
        StringAssert.Contains("PublishAegirPresentationWarning(_AegirMissingMaterialWarningHash, 1f);", source);
        StringAssert.Contains("PublishAegirPresentationWarning(_AegirMissingBandTextureWarningHash, 1f);", source);
    }

    [Test]
    public void CelestialEngineReportsBoundedCelestialEventBackpressure()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("_CelestialEventDropWarningHash", source);
        StringAssert.Contains("TryRaiseCelestialSunAngleChanged(_currentSunAngle);", source);
        StringAssert.Contains("TryRaiseCelestialPlanetPhaseChanged(_currentPhase);", source);
        StringAssert.Contains("TryRaiseCelestialEclipseStarted();", source);
        StringAssert.Contains("TryRaiseCelestialEclipseEnded();", source);
        StringAssert.Contains("ReportCelestialEventDropIfBackpressured(_CelestialSunAngleEventHash);", source);
        StringAssert.Contains("ReportCelestialEventDropIfBackpressured(_CelestialPlanetPhaseEventHash);", source);
        StringAssert.Contains("ReportCelestialEventDropIfBackpressured(_CelestialEclipseStartedEventHash);", source);
        StringAssert.Contains("ReportCelestialEventDropIfBackpressured(_CelestialEclipseEndedEventHash);", source);
        StringAssert.Contains("if (!Application.isPlaying || CelestialEvents.PendingCount <= 0)", source);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(\n                _CelestialEventDropWarningHash", source);
    }

    [Test]
    public void CelestialEngineReportsCelestialTruthFallbackTelemetry()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("private enum CelestialTruthReadFailure : byte", source);
        StringAssert.Contains("_CelestialTruthFallbackWarningHash", source);
        StringAssert.Contains("_CelestialTruthMissingContextHash", source);
        StringAssert.Contains("_CelestialTruthInvalidStateContextHash", source);
        StringAssert.Contains("_CelestialTruthInvalidSnapshotContextHash", source);
        StringAssert.Contains("ReportCelestialTruthFallbackIfNeeded(failure);", source);
        StringAssert.Contains("ReportCelestialTruthFallbackIfNeeded(CelestialTruthReadFailure.InvalidSnapshot);", source);
        StringAssert.Contains("failure = CelestialTruthReadFailure.MissingVaultOrHandle;", source);
        StringAssert.Contains("failure = CelestialTruthReadFailure.InvalidState;", source);
        StringAssert.Contains("failure = CelestialTruthReadFailure.InvalidSnapshot;", source);
        StringAssert.Contains("ResolveCelestialTruthFailureContextHash(failure)", source);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(\n                _CelestialTruthFallbackWarningHash", source);
    }

    [Test]
    public void CelestialRuntimeSnapshotGlobalPublishHasSingleRuntimeOwner()
    {
        string root = Directory.GetCurrentDirectory();
        string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
        string[] sources = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        foreach (string sourcePath in sources)
        {
            string normalizedPath = sourcePath.Replace('\\', '/');
            string source = File.ReadAllText(sourcePath);
            if (!source.Contains("PublishCelestialRuntimeSnapshot("))
                continue;

            bool allowed =
                normalizedPath.EndsWith("/Assets/_Project/Scripts/HectonCelestialEngine.cs", StringComparison.Ordinal) ||
                normalizedPath.EndsWith("/Assets/_Project/Scripts/Core/GlobalRegistry.cs", StringComparison.Ordinal);
            Assert.That(allowed, Is.True, "Unexpected global CelestialRuntimeSnapshot writer: " + normalizedPath);
        }
    }

    [Test]
    public void CelestialLightReadabilityGlobalPublishHasSingleRuntimeOwner()
    {
        string root = Directory.GetCurrentDirectory();
        string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
        string[] sources = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        foreach (string sourcePath in sources)
        {
            string normalizedPath = sourcePath.Replace('\\', '/');
            string source = File.ReadAllText(sourcePath);
            if (!source.Contains("PublishCelestialLightReadabilitySnapshot("))
                continue;

            bool allowed =
                normalizedPath.EndsWith("/Assets/_Project/Scripts/HectonCelestialEngine.cs", StringComparison.Ordinal) ||
                normalizedPath.EndsWith("/Assets/_Project/Scripts/Core/GlobalRegistry.cs", StringComparison.Ordinal);
            Assert.That(allowed, Is.True, "Unexpected global CelestialLightReadabilitySnapshot writer: " + normalizedPath);
        }
    }

    [Test]
    public void CelestialEventsModelListenerLifecycleAndDispatchFailures()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("_deferredRegisterListeners", source);
        StringAssert.Contains("_deferredUnregisterListeners", source);
        StringAssert.Contains("QueueDeferredRegister(listener);", source);
        StringAssert.Contains("QueueDeferredUnregister(listener);", source);
        StringAssert.Contains("ApplyDeferredListenerMutations();", source);
        StringAssert.Contains("DrainQueuedEvents();", source);
        StringAssert.Contains("private static void DispatchToListener(ICelestialEventListener listener, in CelestialEventPayload payload)", source);
        StringAssert.Contains("ReportQueueOverflow(eventType);", source);
        StringAssert.Contains("ReportDuplicateListenerRegistration();", source);
        StringAssert.Contains("ReportListenerRejected();", source);
        StringAssert.Contains("ReportListenerDispatchException();", source);
        StringAssert.Contains("ReportUnregisterMiss();", source);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(\n                _QueueOverflowWarningHash", source);
        StringAssert.Contains("Hecton8.Core.H8Debug.LogException(exception);", source);
    }

    [Test]
    public void CelestialEventsReentrantUnregisterRegisterKeepsListenerAlive()
    {
        ResetCelestialEventsForTests();
        try
        {
            TestCelestialEventListener listener = new TestCelestialEventListener();
            listener.SunAngleCallback = (self, angle) =>
            {
                CelestialEvents.Unregister(self);
                CelestialEvents.Register(self);
            };

            CelestialEvents.Register(listener);
            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(12f));
            CelestialEvents.FlushPending();

            Assert.That(listener.SunAngleCount, Is.EqualTo(1));
            Assert.That(listener.LastSunAngle, Is.EqualTo(12f).Within(0.001f));

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(24f));
            CelestialEvents.FlushPending();

            Assert.That(listener.SunAngleCount, Is.EqualTo(2));
            Assert.That(listener.LastSunAngle, Is.EqualTo(24f).Within(0.001f));
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(0));
        }
        finally
        {
            ResetCelestialEventsForTests();
        }
    }

    [Test]
    public void CelestialEventsDeferredUnregisterPreventsStaleCallbackInSameDispatch()
    {
        ResetCelestialEventsForTests();
        try
        {
            TestCelestialEventListener staleListener = new TestCelestialEventListener();
            TestCelestialEventListener removingListener = new TestCelestialEventListener();
            removingListener.SunAngleCallback = (_, __) => CelestialEvents.Unregister(staleListener);

            CelestialEvents.Register(staleListener);
            CelestialEvents.Register(removingListener);

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(5f));
            CelestialEvents.FlushPending();

            Assert.That(removingListener.SunAngleCount, Is.EqualTo(1));
            Assert.That(staleListener.SunAngleCount, Is.EqualTo(0));

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(6f));
            CelestialEvents.FlushPending();

            Assert.That(removingListener.SunAngleCount, Is.EqualTo(2));
            Assert.That(staleListener.SunAngleCount, Is.EqualTo(0));
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(0));
        }
        finally
        {
            ResetCelestialEventsForTests();
        }
    }

    [Test]
    public void CelestialEventsDeferredRegisterStartsOnNextPayloadOnly()
    {
        ResetCelestialEventsForTests();
        try
        {
            TestCelestialEventListener child = new TestCelestialEventListener();
            TestCelestialEventListener parent = new TestCelestialEventListener();
            parent.SunAngleCallback = (_, __) => CelestialEvents.Register(child);

            CelestialEvents.Register(parent);

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(1f));
            CelestialEvents.FlushPending();

            Assert.That(parent.SunAngleCount, Is.EqualTo(1));
            Assert.That(child.SunAngleCount, Is.EqualTo(0));

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(2f));
            CelestialEvents.FlushPending();

            Assert.That(parent.SunAngleCount, Is.EqualTo(2));
            Assert.That(child.SunAngleCount, Is.EqualTo(1));
            Assert.That(child.LastSunAngle, Is.EqualTo(2f).Within(0.001f));
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(0));
        }
        finally
        {
            ResetCelestialEventsForTests();
        }
    }

    [Test]
    public void CelestialEventsBoundedQueueRejectsOverflowAndDrainsAcceptedPayloads()
    {
        ResetCelestialEventsForTests();
        try
        {
            TestCelestialEventListener listener = new TestCelestialEventListener();
            CelestialEvents.Register(listener);

            for (int i = 0; i < 8; i++)
                Assert.IsTrue(CelestialEvents.TryRaiseEclipseStarted(), $"Expected payload {i} to fit the bounded celestial event queue.");

            Assert.IsFalse(CelestialEvents.TryRaiseEclipseStarted());
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(8));

            CelestialEvents.FlushPending();

            Assert.That(listener.EclipseStartedCount, Is.EqualTo(8));
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(0));
        }
        finally
        {
            ResetCelestialEventsForTests();
        }
    }

    [Test]
    public void CelestialEventsListenerExceptionDoesNotBlockOtherListeners()
    {
        ResetCelestialEventsForTests();
        bool previousIgnore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            TestCelestialEventListener throwingListener = new TestCelestialEventListener();
            throwingListener.ThrowOnSunAngle = true;
            TestCelestialEventListener healthyListener = new TestCelestialEventListener();

            CelestialEvents.Register(throwingListener);
            CelestialEvents.Register(healthyListener);

            Assert.IsTrue(CelestialEvents.TryRaiseSunAngleChanged(64f));
            CelestialEvents.FlushPending();

            Assert.That(throwingListener.SunAngleCount, Is.EqualTo(1));
            Assert.That(healthyListener.SunAngleCount, Is.EqualTo(1));
            Assert.That(healthyListener.LastSunAngle, Is.EqualTo(64f).Within(0.001f));
            Assert.That(CelestialEvents.PendingCount, Is.EqualTo(0));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = previousIgnore;
            ResetCelestialEventsForTests();
        }
    }

    [Test]
    public void AegirSkyAndImpostorShadersConsumeRuntimeProjectionContract()
    {
        string skyPath = Path.Combine("Assets", "_Project", "Art", "Shaders", "Sky", "Hecton_AegirSky.shader");
        string impostorPath = Path.Combine("Assets", "_Project", "Art", "Shaders", "H8_AegirGasGiantImpostor_1428.shader");
        string skySource = File.ReadAllText(skyPath).Replace("\r\n", "\n");
        string impostorSource = File.ReadAllText(impostorPath).Replace("\r\n", "\n");

        StringAssert.Contains("_H8AegirSunDirection", skySource);
        StringAssert.Contains("float _H8AegirStormEmission;", skySource);
        StringAssert.Contains("float AegirStormEmission()", skySource);
        StringAssert.Contains("clamp(_H8AegirStormEmission, 0.0, 4.0)", skySource);
        StringAssert.Contains("systemVisibility = saturate(1.0 - _H8AegirSunDirection.w);", skySource);
        StringAssert.Contains("ringAlpha = ringMask * gap * (0.28 + quality * 0.42) * _RingOpacity * systemVisibility;", skySource);
        StringAssert.Contains("bool RaySphere(", skySource);
        StringAssert.Contains("bool RayRingPlane(", skySource);
        StringAssert.Contains("float RingShadow(", skySource);
        StringAssert.Contains("SAMPLE_TEXTURE2D(_AegirBandTex", skySource);
        StringAssert.Contains("float hardTerminator = smoothstep(-0.08, 0.18, ndotl);", skySource);
        StringAssert.Contains("float limbDarken = lerp(1.0, 0.58", skySource);
        StringAssert.Contains("color += _AtmosphereTint.rgb * scatter;", skySource);
        StringAssert.Contains("float3 planetColor = DrawAegir", skySource);
        StringAssert.Contains("stormBand * cloudTexture * 0.15 * stormEmission", skySource);
        StringAssert.Contains("bands += float3(0.095, 0.052, 0.022) * stormSignal * stormEmission", skySource);

        StringAssert.Contains("_PlanetPhase", impostorSource);
        StringAssert.Contains("_H8GlobalQualityWeight", impostorSource);
        StringAssert.Contains("_H8AegirSunDirection", impostorSource);
        StringAssert.Contains("_HectonCelestialLightReadability0", impostorSource);
        StringAssert.Contains("TEXTURE2D(_MainTex);", impostorSource);
        StringAssert.Contains("TEXTURE2D(_DetailTex);", impostorSource);
        StringAssert.Contains("TEXTURE2D(_StormTex);", impostorSource);
        StringAssert.Contains("_StormEmission (\"Runtime Storm Emission\"", impostorSource);
        StringAssert.Contains("half _StormEmission;", impostorSource);
        StringAssert.Contains("_WarmTint.rgb * stormMask * _StormStrength * _StormEmission", impostorSource);
        StringAssert.Contains("baseUv.x = frac(baseUv.x + _Rotation + _GlobalRotation + syncTime * _AutoRotationSpeed);", impostorSource);
        StringAssert.Contains("half phase = smoothstep(_PhaseCenter - _PhaseSoftness", impostorSource);
        StringAssert.Contains("half limbDarken = lerp(1.0h, 0.58h, pow(limb, 1.25h));", impostorSource);
        StringAssert.Contains("_HectonCelestialLightReadability0.w / 112.0", impostorSource);
        StringAssert.Contains("horizonVeil = 1.0h - smoothstep(", impostorSource);
        StringAssert.Contains("color = lerp(color, veilColor, atmosphereMask);", impostorSource);
        StringAssert.Contains("systemVisibility = min(systemVisibility", impostorSource);
    }

    [Test]
    public void AegirSkyMaterialUsesCanonicalBandTextureInsteadOfStormMask()
    {
        string skyMaterialPath = Path.Combine("Assets", "_Project", "Art", "Materials", "Sky", "MAT_AegirSky_Master.mat");
        string skyMaterialSource = File.ReadAllText(skyMaterialPath).Replace("\r\n", "\n");
        string skyBandGuid = ReadMaterialTextureGuid(skyMaterialSource, "_AegirBandTex");

        Assert.That(ReadMaterialShaderGuid(skyMaterialSource), Is.EqualTo("6a3f1601ae9165f4a001000000000001"));
        Assert.That(skyBandGuid, Is.EqualTo("6c173d4e1a858b34ca1b7e5610aae988"));
        Assert.That(skyBandGuid, Is.Not.EqualTo("d9d11072e85a2b54cacd11eaad6614a8"));
    }

    [Test]
    public void AegirGasMaterialUsesCanonicalBandDetailStormTextureSet()
    {
        string gasMaterialPath = Path.Combine("Assets", "_Project", "Art", "Materials", "Celestial", "MAT_AegirGasGiant_Impostor_1428.mat");
        string gasMaterialSource = File.ReadAllText(gasMaterialPath).Replace("\r\n", "\n");

        Assert.That(ReadMaterialShaderGuid(gasMaterialSource), Is.EqualTo("0661c64fe7dfd77469f3bd686cbc254e"));
        Assert.That(ReadMaterialTextureGuid(gasMaterialSource, "_MainTex"), Is.EqualTo("6c173d4e1a858b34ca1b7e5610aae988"));
        Assert.That(ReadMaterialTextureGuid(gasMaterialSource, "_DetailTex"), Is.EqualTo("e1aefa60ab4517644bb884257440872b"));
        Assert.That(ReadMaterialTextureGuid(gasMaterialSource, "_StormTex"), Is.EqualTo("d9d11072e85a2b54cacd11eaad6614a8"));
        StringAssert.Contains("- _StormEmission: 1", gasMaterialSource);
    }

    [Test]
    public void AegirSourceValidatorGuardsScenePrefabAndProductFaceBindingDrift()
    {
        string validatorPath = Path.Combine("Assets", "_Project", "Scripts", "Editor", "AegirGasGiantSourceValidator.cs");
        string productFacePath = Path.Combine("Assets", "_Project", "Scripts", "Editor", "ProductFaceSkyOceanSourceValidator.cs");
        string validatorSource = File.ReadAllText(validatorPath).Replace("\r\n", "\n");
        string productFaceSource = File.ReadAllText(productFacePath).Replace("\r\n", "\n");

        StringAssert.Contains("Aegir Gas Giant Source Contract", validatorSource);
        StringAssert.Contains("ValidateTextureImport", validatorSource);
        StringAssert.Contains("streamingMipmaps", validatorSource);
        StringAssert.Contains("isReadable", validatorSource);
        StringAssert.Contains("maxTextureSize", validatorSource);
        StringAssert.Contains("ValidateMaterialFloat(gasMaterial, \"_StormEmission\", 1f", validatorSource);
        StringAssert.Contains("EnsureMaterialFloat(GasGiantMaterialPath, \"_StormEmission\", 1f);", validatorSource);
        StringAssert.Contains("private static int EnsureMaterialFloat", validatorSource);
        StringAssert.Contains("ValidateRuntimeSourceContracts(report);", validatorSource);
        StringAssert.Contains("CheckedSourceCount", validatorSource);
        StringAssert.Contains("BadRuntimeSourceContract", validatorSource);
        StringAssert.Contains("ResolveAegirSkyProjectionStormEmission()", validatorSource);
        StringAssert.Contains("_AegirStormEmissionInvalidWarningHash", validatorSource);
        StringAssert.Contains("AegirStormEmissionWarningCooldownFrames", validatorSource);
        StringAssert.Contains("ReportAegirStormEmissionInvalidIfNeeded", validatorSource);
        StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission());", validatorSource);
        StringAssert.Contains("Shader.SetGlobalFloat(_aegirStormEmissionId, 1f);", validatorSource);
        StringAssert.Contains("RestoreCelestialTextureDefaults();", validatorSource);
        StringAssert.Contains("ClearAegirMaterialRuntimeCache();", validatorSource);
        StringAssert.Contains("aegirRenderer.SetPropertyBlock(null);", validatorSource);
        StringAssert.Contains("_aegirSharedMaterial = null;", validatorSource);
        StringAssert.Contains("PublishOceanCelestialProjectionGlobals(aegirDirection)", validatorSource);
        StringAssert.Contains("_ID_HectonEclipseWaterShadowParams", validatorSource);
        StringAssert.Contains("_ID_HectonEclipseWaterShadowDirection", validatorSource);
        StringAssert.Contains("_ID_HectonRingCausticsParams", validatorSource);
        StringAssert.Contains("_ID_HectonRingCausticsDirection", validatorSource);
        StringAssert.Contains("ResolveAupOceanShadowCenterRuntimeXZ", validatorSource);
        StringAssert.Contains("TryResolvePlayerAup", validatorSource);
        StringAssert.Contains("stormBand * cloudTexture * 0.15 * stormEmission", validatorSource);
        StringAssert.Contains("stormEmissionMultiplier", validatorSource);
        StringAssert.Contains("weather-driven storm emission", validatorSource);
        StringAssert.Contains("ValidateOrbitSceneOverrides", validatorSource);
        StringAssert.Contains("CountOccurrences(source, sourcePrefabToken)", validatorSource);
        StringAssert.Contains("production Aegir prefab instances. Keep one celestial renderer/source owner.", validatorSource);
        StringAssert.Contains("RepairPrefabSource", validatorSource);
        StringAssert.Contains("RepairOrbitSceneFromMenu", validatorSource);
        StringAssert.Contains("RevertAegirScenePrefabOverrides", validatorSource);
        StringAssert.Contains("PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);", validatorSource);
        StringAssert.Contains("UnityBuiltInPrimitiveMeshGuid", validatorSource);
        StringAssert.Contains("9bafceacd557491409f6134514063ff4", validatorSource);
        StringAssert.Contains("0000000000000000e000000000000000", validatorSource);
        StringAssert.Contains("ValidateAegirGasGiantSource(report);", productFaceSource);
    }

    [Test]
    public void ApplyTuningState_ClampsPlanetCenterRadiusAndAppliesSunProperties()
    {
        GameObject engineObj = new GameObject("TestEngine");
        HectonCelestialEngine engine = engineObj.AddComponent<HectonCelestialEngine>();

        GameObject sunObj = new GameObject("SunLight");
        Light sunLight = sunObj.AddComponent<Light>();

        System.Type nestedProfileType = engine.GetType().GetNestedType("AegirSkyProjectionProfile", BindingFlags.NonPublic);
        object defaultProfile = nestedProfileType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        SetPrivateField(engine, "aegirSkyProjection", defaultProfile);
        SetPrivateField(engine, "sunLight", sunLight);

        engine.ApplyTuningState(150f, 2.5f, Color.red);

        object profileObj = GetPrivateField(engine, "aegirSkyProjection");
        System.Type profileType = profileObj.GetType();
        FieldInfo radiusField = profileType.GetField("fallbackAngularRadius");
        float radius = (float)radiusField.GetValue(profileObj);

        // 150 / 100 = 1.5, clamped to 0.05 - 0.65 -> 0.65
        Assert.That(radius, Is.EqualTo(0.65f).Within(0.0001f));

        Assert.That(sunLight.intensity, Is.EqualTo(2.5f));
        Assert.That(sunLight.color, Is.EqualTo(Color.red));

        Object.DestroyImmediate(engineObj);
        Object.DestroyImmediate(sunObj);
    }

    [Test]
    public void SeismicWaveMathRejectsFarFiniteAupBeforeFloatCast()
    {
        SeismicSignal signal = CreateRadialSeismicSignal();
        signal.EpicenterAUP = new double3(0d, 0d, 0d);
        signal.CurrentRadiusMeters = 256f;
        signal.PWaveRadiusMeters = 256f;
        signal.SWaveRadiusMeters = 256f;

        float3 displacement = SeismicWaveMath.CalculateSeismicDisplacement(
            new double3(1.0e40d, 0d, 0d),
            in signal,
            0.05d,
            1f);

        Assert.That(math.all(math.isfinite(displacement)), Is.True);
        Assert.That(math.lengthsq(displacement), Is.EqualTo(0f));
    }

    [Test]
    public void SeismicWaveMathKeepsNearWaveFiniteAfterAupSubtract()
    {
        SeismicSignal signal = CreateRadialSeismicSignal();
        signal.EpicenterAUP = new double3(1000000000000d, -2000d, 1000000000000d);
        signal.CurrentRadiusMeters = 128f;
        signal.PWaveRadiusMeters = 128f;
        signal.SWaveRadiusMeters = 128f;

        float3 displacement = SeismicWaveMath.CalculateSeismicDisplacement(
            signal.EpicenterAUP + new double3(128d, 0d, 0d),
            in signal,
            0.05d,
            1f);

        Assert.That(math.all(math.isfinite(displacement)), Is.True);
        Assert.That(math.lengthsq(displacement), Is.GreaterThan(0.00001f));
    }

    private static SeismicSignal CreateRadialSeismicSignal()
    {
        return new SeismicSignal
        {
            Flags = SeismicSignal.FlagRadialWave,
            Intensity01 = 1f,
            MagnitudeRichter = 8f,
            PWaveAmplitude01 = 1f,
            SWaveAmplitude01 = 0f,
            Sequence = 3,
            EventTypeHash = 0x13A7u
        };
    }

    private static string ReadMaterialTextureGuid(string source, string propertyName)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(propertyName))
            return string.Empty;

        int propertyIndex = source.IndexOf("- " + propertyName + ":", System.StringComparison.Ordinal);
        if (propertyIndex < 0)
            return string.Empty;

        int textureIndex = source.IndexOf("m_Texture:", propertyIndex, System.StringComparison.Ordinal);
        if (textureIndex < 0)
            return string.Empty;

        int guidIndex = source.IndexOf("guid:", textureIndex, System.StringComparison.Ordinal);
        if (guidIndex < 0)
            return string.Empty;

        guidIndex += "guid:".Length;
        while (guidIndex < source.Length && source[guidIndex] == ' ')
            guidIndex++;

        int guidEnd = source.IndexOf(",", guidIndex, System.StringComparison.Ordinal);
        if (guidEnd < 0)
            return string.Empty;

        return source.Substring(guidIndex, guidEnd - guidIndex).Trim();
    }

    private static string ReadMaterialShaderGuid(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        int shaderIndex = source.IndexOf("m_Shader:", System.StringComparison.Ordinal);
        if (shaderIndex < 0)
            return string.Empty;

        int guidIndex = source.IndexOf("guid:", shaderIndex, System.StringComparison.Ordinal);
        if (guidIndex < 0)
            return string.Empty;

        guidIndex += "guid:".Length;
        while (guidIndex < source.Length && source[guidIndex] == ' ')
            guidIndex++;

        int guidEnd = source.IndexOf(",", guidIndex, System.StringComparison.Ordinal);
        if (guidEnd < 0)
            return string.Empty;

        return source.Substring(guidIndex, guidEnd - guidIndex).Trim();
    }

    private void SetPrivateField(string fieldName, object value)
    {
        SetPrivateField(_engine, fieldName, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        return field.GetValue(target);
    }

    private static void SetPrivateStaticField(System.Type targetType, string fieldName, object value)
    {
        FieldInfo field = targetType.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(field, $"Expected private static field '{fieldName}' to exist.");
        field.SetValue(null, value);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        return InvokePrivateMethod(_engine, methodName, args);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method, $"Expected private method '{methodName}' to exist.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivateStaticMethod(System.Type targetType, string methodName, params object[] args)
    {
        MethodInfo method = targetType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(method, $"Expected private static method '{methodName}' to exist.");
        return method.Invoke(null, args);
    }

    private static void ResetCelestialEventsForTests()
    {
        InvokePrivateStaticMethod(typeof(CelestialEvents), "ResetStaticState");
    }

    private sealed class TestCelestialEventListener : ICelestialEventListener
    {
        public int EclipseStartedCount;
        public int EclipseEndedCount;
        public int SunAngleCount;
        public int PlanetPhaseCount;
        public float LastSunAngle;
        public float LastPlanetPhase;
        public bool ThrowOnSunAngle;
        public System.Action<TestCelestialEventListener, float> SunAngleCallback;

        public void OnCelestialEclipseStarted()
        {
            EclipseStartedCount++;
        }

        public void OnCelestialEclipseEnded()
        {
            EclipseEndedCount++;
        }

        public void OnCelestialSunAngleChanged(float angleDegrees)
        {
            SunAngleCount++;
            LastSunAngle = angleDegrees;
            SunAngleCallback?.Invoke(this, angleDegrees);
            if (ThrowOnSunAngle)
                throw new System.InvalidOperationException("Synthetic celestial listener failure.");
        }

        public void OnCelestialPlanetPhaseChanged(float phase)
        {
            PlanetPhaseCount++;
            LastPlanetPhase = phase;
        }
    }
}
