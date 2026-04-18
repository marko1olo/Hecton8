using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
        LogAssert.Expect(LogType.Error, "[HectonCelestialEngine] Aegir Transform is not assigned!");

        _engine = _gameObject.AddComponent<HectonCelestialEngine>();

        Shader skyShader = Shader.Find("HECTON/Sky/Hecton_AlienSky_Master");
        Assert.IsNotNull(skyShader, "Expected HECTON/Sky/Hecton_AlienSky_Master shader to exist.");

        _skyMaterial = new Material(skyShader);
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
    public void ResolveSkyColorsReadsScriptProfilesAsSourceOfTruth()
    {
        Color expectedZenith = new Color(0.12f, 0.34f, 0.56f, 1f);
        Color expectedHorizon = new Color(0.65f, 0.43f, 0.21f, 1f);
        Color expectedNadir = new Color(0.07f, 0.08f, 0.11f, 1f);

        SetPrivateField("_dayProfile", SkyColorProfile.Default(expectedZenith, expectedHorizon, expectedNadir));
        SetPrivateField("_horizonZenithBlend", 0f);
        SetPrivateField("_horizonBrightnessScale", 1f);
        SetPrivateField("_currentSunAngle", 35f);
        SetPrivateField("_smoothedOcclusionFactor", 0f);

        _skyMaterial.SetColor("_SkyColorZenith", expectedZenith);
        _skyMaterial.SetColor("_SkyColorHorizon", expectedHorizon);
        _skyMaterial.SetColor("_SkyColorNadir", expectedNadir);

        object[] args = { null, null, null };
        InvokePrivateMethod("ResolveSkyColors", args);

        Assert.That((Color)args[0], Is.EqualTo(expectedZenith));
        Assert.That((Color)args[1], Is.EqualTo(expectedHorizon));
        Assert.That((Color)args[2], Is.EqualTo(expectedNadir));
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
        Assert.That(resolvedHorizon.grayscale, Is.LessThan(resolvedZenith.grayscale));
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
    public void ApplyBestVisualDefaultsSetsNightInscatterFloorAndReadableDayHaze()
    {
        _engine.ApplyBestVisualDefaults();

        float nightExposure = (float)GetPrivateField(_engine, "nightAtmosphereExposure");
        float horizonDensity = (float)GetPrivateField(_engine, "horizonDensity");
        float zenithTransparency = (float)GetPrivateField(_engine, "zenithTransparency");
        AnimationCurve dayDensity = (AnimationCurve)GetPrivateField(_engine, "dayAtmosphereDensity");

        Assert.That(nightExposure, Is.EqualTo(0.001f).Within(0.0001f));
        Assert.That(horizonDensity, Is.EqualTo(1.1f).Within(0.0001f));
        Assert.That(zenithTransparency, Is.EqualTo(0.84f).Within(0.0001f));
        Assert.That(dayDensity.Evaluate(0f), Is.LessThan(0.7f));
        Assert.That(dayDensity.Evaluate(0f), Is.GreaterThan(dayDensity.Evaluate(1f)));
    }

    [Test]
    public void SkySystemFollowCameraUsesFallbackSeaLevelWhenOceanIsUnavailable()
    {
        GameObject followObject = new GameObject("SkyFollowTest");

        try
        {
            SkySystemFollowCamera followCamera = followObject.AddComponent<SkySystemFollowCamera>();
            SetPrivateField(followCamera, "lockToSeaLevel", true);
            SetPrivateField(followCamera, "fallbackSeaLevelY", 4900f);
            SetPrivateField(followCamera, "positionOffset", new Vector3(0f, 12f, 0f));

            float lockedY = (float)InvokePrivateMethod(followCamera, "ResolveLockedY");
            Assert.That(lockedY, Is.EqualTo(4912f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(followObject);
        }
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
            InvokePrivateMethod(body, "LateUpdate");

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
            _engine.Tick(0f);

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
}
