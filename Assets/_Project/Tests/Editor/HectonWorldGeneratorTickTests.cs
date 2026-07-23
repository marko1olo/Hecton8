using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Unity.Mathematics;
using Hecton8.Core;

public class HectonWorldGeneratorTickTests
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
    public void Tick_WhenNotInitialized_ReturnsEarlyWithoutUpdatingLastChunk()
    {
        // Setup initial chunk state
        var expectedLastChunk = new int2(int.MinValue, int.MinValue);
        var lastChunkField = typeof(HectonWorldGenerator).GetField("_lastChunk", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensure IsInitialized returns false by leaving _registeredWorldSeedProvider as false
        var registeredField = typeof(HectonWorldGenerator).GetField("_registeredWorldSeedProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        registeredField.SetValue(_worldGenerator, false);

        // Ensure streaming is set to true so if it DID bypass initialization, it would attempt streaming.
        var streamingField = typeof(HectonWorldGenerator).GetField("_streaming", BindingFlags.NonPublic | BindingFlags.Instance);
        streamingField.SetValue(_worldGenerator, true);

        // Act
        _worldGenerator.Tick(0.1f);

        // Assert
        var actualLastChunk = (int2)lastChunkField.GetValue(_worldGenerator);
        Assert.AreEqual(expectedLastChunk, actualLastChunk, "Tick should have returned early and not updated _lastChunk.");
    }

    [Test]
    public void Tick_WhenInitializedAndStreaming_WithNoViewer_ReturnsWithoutUpdatingLastChunk()
    {
        // Setup initial chunk state
        var expectedLastChunk = new int2(int.MinValue, int.MinValue);
        var lastChunkField = typeof(HectonWorldGenerator).GetField("_lastChunk", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensure IsInitialized returns true
        var registeredField = typeof(HectonWorldGenerator).GetField("_registeredWorldSeedProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        registeredField.SetValue(_worldGenerator, true);

        // Ensure streaming is set to true so it proceeds to try and resolve viewer AUP.
        var streamingField = typeof(HectonWorldGenerator).GetField("_streaming", BindingFlags.NonPublic | BindingFlags.Instance);
        streamingField.SetValue(_worldGenerator, true);

        // _playerRuntimeContext is null by default, so TryResolveViewerAup returns false.
        // It should gracefully return out of UpdateStreaming without throwing an exception.
        // This is explicitly checking that no exception is thrown, meaning it behaves safely
        // when the context isn't resolved.
        Assert.DoesNotThrow(() => _worldGenerator.Tick(0.1f));

        // And verify that _lastChunk didn't change because TryResolveViewerAup returned false.
        var actualLastChunk = (int2)lastChunkField.GetValue(_worldGenerator);
        Assert.AreEqual(expectedLastChunk, actualLastChunk);
    }
}
