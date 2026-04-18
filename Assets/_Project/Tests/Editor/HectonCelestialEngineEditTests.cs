using System.Reflection;
using Hecton8.Celestial;
using NUnit.Framework;
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

        LogAssert.Expect(LogType.Error, "[HectonCelestialEngine] Sun Light is not assigned!");
        LogAssert.Expect(LogType.Error, "[HectonCelestialEngine] Aegir Transform is not assigned!");
        LogAssert.Expect(LogType.Warning, "[HectonCelestialEngine] Player not assigned, using SceneBootstrap player transform.");
        LogAssert.Expect(LogType.Warning, "[HectonCelestialEngine] Sky Material is not assigned!");

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
    public void ResolveSkyColorsReadsLiveSkyMaterialAsSourceOfTruth()
    {
        Color expectedZenith = new Color(0.12f, 0.34f, 0.56f, 1f);
        Color expectedHorizon = new Color(0.65f, 0.43f, 0.21f, 1f);
        Color expectedNadir = new Color(0.07f, 0.08f, 0.11f, 1f);

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

        _skyMaterial.SetColor("_SkyColorZenith", baseZenith);
        _skyMaterial.SetColor("_SkyColorHorizon", baseHorizon);
        _skyMaterial.SetColor("_SkyColorNadir", baseNadir);

        InvokePrivateMethod("UpdateSkyboxBlend", -24f);

        object[] args = { null, null, null };
        InvokePrivateMethod("ResolveSkyColors", args);

        Color resolvedZenith = (Color)args[0];
        Color resolvedHorizon = (Color)args[1];

        Assert.That(resolvedZenith.maxColorComponent, Is.LessThan(baseZenith.maxColorComponent));
        Assert.That(resolvedHorizon.maxColorComponent, Is.LessThan(baseHorizon.maxColorComponent));
        Assert.That(resolvedHorizon.grayscale, Is.LessThan(resolvedZenith.grayscale));
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
