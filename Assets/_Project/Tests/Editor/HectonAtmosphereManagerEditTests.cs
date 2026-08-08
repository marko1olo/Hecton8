using NUnit.Framework;
using UnityEngine;
using Hecton8.Atmosphere;
using Hecton8.Core;
using System.Reflection;

[TestFixture]
public class HectonAtmosphereManagerEditTests
{
    private GameObject _atmosphereObject;
    private HectonAtmosphereManager _manager;
    private GameObject _sunObject;
    private Light _sunLight;

    [SetUp]
    public void Setup()
    {
        _atmosphereObject = new GameObject("AtmosphereManagerTestHost");
        _manager = _atmosphereObject.AddComponent<HectonAtmosphereManager>();

        _sunObject = new GameObject("SunTestHost");
        _sunLight = _sunObject.AddComponent<Light>();
        _sunLight.type = LightType.Directional;
    }

    [TearDown]
    public void Teardown()
    {
        if (_atmosphereObject != null)
            Object.DestroyImmediate(_atmosphereObject);
        if (_sunObject != null)
            Object.DestroyImmediate(_sunObject);
        AtmosphereDirector.SetSkybox(null);
    }

    [Test]
    public void AtmosphereDirector_RenderSettingsFacade_SynchronizesWithState()
    {
        Material testMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
        try
        {
            // Act
            bool setResult = AtmosphereDirector.SetSkybox(testMat);

            // Assert - test facade methods
            Assert.That(setResult, Is.True);
            Assert.That(AtmosphereDirector.Skybox, Is.EqualTo(testMat));
            Assert.That(AtmosphereDirector.IsSkybox(testMat), Is.True);
            Assert.That(RenderSettings.skybox, Is.EqualTo(testMat));

            // Assert - test bridge implementation directly
            IAtmosphereRenderSettingsBridge bridge = _manager;
            Assert.That(bridge.Skybox, Is.EqualTo(testMat));

            // Redundant sets should return false
            Assert.That(AtmosphereDirector.SetSkybox(testMat), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(testMat);
        }
    }

    [Test]
    public void StateMachine_InterpolatesState_BasedOnTimeOfDay()
    {
        // Force the sun field via reflection to test time of day state changes
        SetField(_manager, "_sunLight", _sunLight);

        // Setup state machine test inputs
        _manager.SetTimeOfDay(0.5f); // Noon

        // Invoke the internal state resolution directly to verify state machine behavior
        var state = InvokeMethod<EnvironmentState>(_manager, "ResolveState");

        // State should evaluate to daytime, not underwater
        Assert.That(state.Daytime, Is.True);
        Assert.That(state.Underwater, Is.False);

        // Test midnight scenario
        _manager.SetTimeOfDay(0.0f);
        state = InvokeMethod<EnvironmentState>(_manager, "ResolveState");
        Assert.That(state.Daytime, Is.False);
    }

    [Test]
    public void SetOrbitalInclination_MaintainsBounds()
    {
        _manager.SetOrbitalInclination(-10f);
        Assert.That(GetField<float>(_manager, "_orbitalInclination"), Is.EqualTo(0f));

        _manager.SetOrbitalInclination(100f);
        Assert.That(GetField<float>(_manager, "_orbitalInclination"), Is.EqualTo(90f));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType()}");
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType()}");
        return (T)field.GetValue(target);
    }

    private static T InvokeMethod<T>(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType()}");
        return (T)method.Invoke(target, null);
    }
}
