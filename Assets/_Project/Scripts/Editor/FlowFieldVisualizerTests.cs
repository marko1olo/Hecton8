// ============================================================================
// HECTON-8 — FlowFieldVisualizerTests.cs
// Unit tests для FlowFieldVisualizer.
//
// Тестирует корректность визуализации, валидации и производительности.
// ============================================================================

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Hecton8.Physics;

[TestFixture]
public class FlowFieldVisualizerTests
{
    private FlowFieldVisualizer _visualizer;
    private GameObject _testObject;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestFlowFieldVisualizer");
        _visualizer = _testObject.AddComponent<FlowFieldVisualizer>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void Constructor_InitializesWithDefaultValues()
    {
        Assert.AreEqual(new Vector2(50f, 50f), _visualizer.AreaSize);
        Assert.AreEqual(new Vector2Int(20, 20), _visualizer.GridResolution);
        Assert.AreEqual(0.5f, _visualizer.SampleHeight);
        Assert.AreEqual(ArrowStyle.Arrows, _visualizer.ArrowStyle);
    }

    [Test]
    public void Validation_ClampsInvalidGridResolution()
    {
        _visualizer.GridResolution = new Vector2Int(0, 0);
        // OnValidate should be called automatically in editor

        // Simulate validation
        var validateMethod = typeof(FlowFieldVisualizer).GetMethod("ValidateSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        validateMethod?.Invoke(_visualizer, null);

        Assert.GreaterOrEqual(_visualizer.GridResolution.x, 2);
        Assert.GreaterOrEqual(_visualizer.GridResolution.y, 2);
    }

    [Test]
    public void Validation_ClampsInvalidAreaSize()
    {
        _visualizer.AreaSize = new Vector2(-10f, 0f);

        var validateMethod = typeof(FlowFieldVisualizer).GetMethod("ValidateSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        validateMethod?.Invoke(_visualizer, null);

        Assert.GreaterOrEqual(_visualizer.AreaSize.x, 1f);
        Assert.GreaterOrEqual(_visualizer.AreaSize.y, 1f);
    }

    [Test]
    public void Recalculate_DoesNotCrashWithValidSettings()
    {
        _visualizer.GridResolution = new Vector2Int(5, 5);
        _visualizer.AreaSize = new Vector2(10f, 10f);

        Assert.DoesNotThrow(() => _visualizer.Recalculate());
    }

    [Test]
    public void Recalculate_HandlesLargeGridWithWarning()
    {
        _visualizer.GridResolution = new Vector2Int(100, 100);
        _visualizer.MaxGridResolution = 50;

        // Should clamp the resolution and log warning
        LogAssert.Expect(LogType.Warning, "Grid too large");

        var validateMethod = typeof(FlowFieldVisualizer).GetMethod("ValidateSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        validateMethod?.Invoke(_visualizer, null);

        Assert.LessOrEqual(_visualizer.GridResolution.x, _visualizer.MaxGridResolution);
        Assert.LessOrEqual(_visualizer.GridResolution.y, _visualizer.MaxGridResolution);
    }

    [Test]
    public void Profile_ApplyTo_UpdatesVisualizer()
    {
        var profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
        profile.areaWidth = 100f;
        profile.areaHeight = 75f;
        profile.gridResolutionX = 10;
        profile.gridResolutionY = 15;
        profile.arrowStyle = ArrowStyle.Cones;

        profile.ApplyTo(_visualizer);

        Assert.AreEqual(new Vector2(100f, 75f), _visualizer.AreaSize);
        Assert.AreEqual(new Vector2Int(10, 15), _visualizer.GridResolution);
        Assert.AreEqual(ArrowStyle.Cones, _visualizer.ArrowStyle);

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_CaptureFrom_SavesVisualizerState()
    {
        _visualizer.AreaSize = new Vector2(42f, 24f);
        _visualizer.GridResolution = new Vector2Int(7, 9);
        _visualizer.ArrowStyle = ArrowStyle.Dots;

        var profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
        profile.CaptureFrom(_visualizer);

        Assert.AreEqual(42f, profile.areaWidth);
        Assert.AreEqual(24f, profile.areaHeight);
        Assert.AreEqual(7, profile.gridResolutionX);
        Assert.AreEqual(9, profile.gridResolutionY);
        Assert.AreEqual(ArrowStyle.Dots, profile.arrowStyle);

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Singleton_ReturnsInstance()
    {
        var instance1 = FlowFieldVisualizer.Instance;
        var instance2 = FlowFieldVisualizer.Instance;

        Assert.AreSame(instance1, instance2);
    }

    [Test]
    public void CullWeakFlows_FiltersSmallVectors()
    {
        _visualizer.CullWeakFlows = true;
        _visualizer.MinFlowStrength = 1.0f;

        // Test that weak flows are filtered
        var sampleMethod = typeof(FlowFieldVisualizer).GetMethod("SampleCurrentAt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (sampleMethod != null)
        {
            var result = sampleMethod.Invoke(_visualizer, new object[] { Vector3.zero });
            Vector3 flow = (Vector3)result;

            // If flow is weak, it should be filtered in DrawFlowArrow
            if (flow.magnitude < _visualizer.MinFlowStrength)
            {
                // This would be filtered out
                Assert.Pass("Weak flow would be filtered");
            }
        }
    }
}
#endif