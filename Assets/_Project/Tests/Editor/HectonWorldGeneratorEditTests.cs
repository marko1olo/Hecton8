using NUnit.Framework;
using UnityEngine;

public class HectonWorldGeneratorEditTests
{
    private GameObject _go;
    private HectonWorldGenerator _worldGenerator;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestWorldGenerator");
        _worldGenerator = _go.AddComponent<HectonWorldGenerator>();
        _worldGenerator.biomes = new BiomeSettings();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
        {
            Object.DestroyImmediate(_go);
        }
    }

    [Test]
    public void GetBiomeAt_ReturnsValueWithinExpectedRange()
    {
        // Act
        float biomeValue = _worldGenerator.GetBiomeAt(0f, 0f);

        // Assert
        Assert.IsTrue(biomeValue >= 0f && biomeValue <= 1f, $"Expected biome value to be in range [0, 1], but got {biomeValue}");
    }

    [Test]
    public void GetBiomeAt_WithCustomCurve_AppliesCurveCorrectly()
    {
        // Arrange
        float expectedConstantValue = 0.75f;
        AnimationCurve constantCurve = new AnimationCurve();
        constantCurve.AddKey(0f, expectedConstantValue);
        constantCurve.AddKey(1f, expectedConstantValue);

        _worldGenerator.biomes.biomeRemapCurve = constantCurve;

        // Act
        float biomeValue1 = _worldGenerator.GetBiomeAt(100f, 200f);
        float biomeValue2 = _worldGenerator.GetBiomeAt(-50f, 10f);

        // Assert
        Assert.AreEqual(expectedConstantValue, biomeValue1, 0.001f);
        Assert.AreEqual(expectedConstantValue, biomeValue2, 0.001f);
    }

    [Test]
    public void ClearAll_WhenCalled_DestroysAllChildren()
    {
        // Arrange
        var child1 = new GameObject("Child1");
        var child2 = new GameObject("Child2");
        child1.transform.SetParent(_worldGenerator.transform);
        child2.transform.SetParent(_worldGenerator.transform);

        Assert.AreEqual(2, _worldGenerator.transform.childCount);

        // Act
        _worldGenerator.ClearAll();

        // Assert
        Assert.AreEqual(0, _worldGenerator.transform.childCount);
    }

    [Test]
    public void GenerateWorldPreview_WhenNotPlaying_CreatesPreviewObject()
    {
        // Act
        _worldGenerator.GenerateWorldPreview();

        // Assert
        Assert.IsNotNull(_worldGenerator.previewObj);
        Assert.IsNotNull(_worldGenerator.previewObj.GetComponent<MeshFilter>());
        Assert.IsNotNull(_worldGenerator.previewObj.GetComponent<MeshRenderer>());
        Assert.AreEqual(_worldGenerator.transform, _worldGenerator.previewObj.transform.parent);
    }
}
