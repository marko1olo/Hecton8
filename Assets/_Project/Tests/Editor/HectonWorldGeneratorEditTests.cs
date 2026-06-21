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
}
