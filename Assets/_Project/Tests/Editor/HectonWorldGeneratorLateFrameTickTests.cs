using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

public class HectonWorldGeneratorLateFrameTickTests
{
    private GameObject _go;
    private HectonWorldGenerator _worldGenerator;
    private object _pendingChunk;

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
        if (_pendingChunk != null)
        {
            var pendingChunkType = _worldGenerator.GetType().GetNestedType("PendingChunk", BindingFlags.NonPublic);
            var verts = (NativeArray<Vector3>)pendingChunkType.GetField("verts").GetValue(_pendingChunk);
            if (verts.IsCreated) verts.Dispose();
            var norms = (NativeArray<Vector3>)pendingChunkType.GetField("norms").GetValue(_pendingChunk);
            if (norms.IsCreated) norms.Dispose();
            var uvs = (NativeArray<Vector2>)pendingChunkType.GetField("uvs").GetValue(_pendingChunk);
            if (uvs.IsCreated) uvs.Dispose();
            var cols = (NativeArray<Color>)pendingChunkType.GetField("cols").GetValue(_pendingChunk);
            if (cols.IsCreated) cols.Dispose();
            var caveV = (NativeArray<float>)pendingChunkType.GetField("caveV").GetValue(_pendingChunk);
            if (caveV.IsCreated) caveV.Dispose();
            var caveB = (NativeArray<byte>)pendingChunkType.GetField("caveB").GetValue(_pendingChunk);
            if (caveB.IsCreated) caveB.Dispose();
            var biomeV = (NativeArray<float>)pendingChunkType.GetField("biomeV").GetValue(_pendingChunk);
            if (biomeV.IsCreated) biomeV.Dispose();
        }

        if (_go != null)
        {
            Object.DestroyImmediate(_go);
        }
    }

    [Test]
    public void LateFrameTick_WhenNotInitialized_DoesNothing()
    {
        // Arrange
        SetPrivateField(_worldGenerator, "_registeredWorldSeedProvider", false);

        var pendingChunksStore = GetPrivateField(_worldGenerator, "_pendingChunks");
        var countMethod = pendingChunksStore.GetType().GetProperty("Count").GetGetMethod();
        int initialCount = (int)countMethod.Invoke(pendingChunksStore, null);

        var pendingRendererDisables = (List<Renderer>)GetPrivateField(_worldGenerator, "_pendingRendererDisables");

        var rendererGo = new GameObject("Renderer");
        var renderer = rendererGo.AddComponent<MeshRenderer>();
        renderer.enabled = true;
        pendingRendererDisables.Add(renderer);

        // Act
        _worldGenerator.LateFrameTick();

        // Assert
        int finalCount = (int)countMethod.Invoke(pendingChunksStore, null);
        Assert.AreEqual(initialCount, finalCount, "Pending chunk count should not change when not initialized.");
        Assert.IsTrue(renderer.enabled, "Pending renderer disables should not be flushed when not initialized.");
        Assert.AreEqual(1, pendingRendererDisables.Count, "Pending renderer disables list should not be cleared.");

        Object.DestroyImmediate(rendererGo);
    }

    [Test]
    public void LateFrameTick_WhenInitialized_CallsExpectedMethods()
    {
        // Arrange
        SetPrivateField(_worldGenerator, "_registeredWorldSeedProvider", true);

        var pendingRendererDisables = (List<Renderer>)GetPrivateField(_worldGenerator, "_pendingRendererDisables");

        var rendererGo = new GameObject("Renderer");
        var renderer = rendererGo.AddComponent<MeshRenderer>();
        renderer.enabled = true;
        pendingRendererDisables.Add(renderer);

        SetPrivateField(_worldGenerator, "_pendingChunkOverflowDisposeActive", true);
        JobHandle handle = default(JobHandle);
        SetPrivateField(_worldGenerator, "_pendingChunkOverflowDisposeHandle", handle);

        // Act
        _worldGenerator.LateFrameTick();

        // Assert
        Assert.IsFalse(renderer.enabled, "Pending renderer disables should be flushed (enabled set to false).");
        Assert.AreEqual(0, pendingRendererDisables.Count, "Pending renderer disables list should be cleared.");

        bool disposeActive = (bool)GetPrivateField(_worldGenerator, "_pendingChunkOverflowDisposeActive");
        Assert.IsFalse(disposeActive, "DrainPendingChunkOverflowDisposals should have cleared the active flag for a default handle.");

        Object.DestroyImmediate(rendererGo);
    }

    [Test]
    public void LateFrameTick_ProcessPendingChunks_RemovesCompletedCanceledChunks()
    {
        // Arrange
        SetPrivateField(_worldGenerator, "_registeredWorldSeedProvider", true);

        var pendingChunksStore = GetPrivateField(_worldGenerator, "_pendingChunks");

        var pendingChunkType = _worldGenerator.GetType().GetNestedType("PendingChunk", BindingFlags.NonPublic);
        _pendingChunk = System.Activator.CreateInstance(pendingChunkType);

        pendingChunkType.GetField("cancelRequested").SetValue(_pendingChunk, (byte)1);

        pendingChunkType.GetField("verts").SetValue(_pendingChunk, new NativeArray<Vector3>(1, Allocator.Persistent));
        pendingChunkType.GetField("norms").SetValue(_pendingChunk, new NativeArray<Vector3>(1, Allocator.Persistent));
        pendingChunkType.GetField("uvs").SetValue(_pendingChunk, new NativeArray<Vector2>(1, Allocator.Persistent));
        pendingChunkType.GetField("cols").SetValue(_pendingChunk, new NativeArray<Color>(1, Allocator.Persistent));
        pendingChunkType.GetField("caveV").SetValue(_pendingChunk, new NativeArray<float>(1, Allocator.Persistent));
        pendingChunkType.GetField("caveB").SetValue(_pendingChunk, new NativeArray<byte>(1, Allocator.Persistent));
        pendingChunkType.GetField("biomeV").SetValue(_pendingChunk, new NativeArray<float>(1, Allocator.Persistent));

        var tryAddMethod = pendingChunksStore.GetType().GetMethod("TryAdd");
        tryAddMethod.Invoke(pendingChunksStore, new object[] { _pendingChunk });

        var countMethod = pendingChunksStore.GetType().GetProperty("Count").GetGetMethod();
        int countBefore = (int)countMethod.Invoke(pendingChunksStore, null);
        Assert.AreEqual(1, countBefore, "Should have 1 pending chunk.");

        // Act
        _worldGenerator.LateFrameTick();

        // Assert
        int countAfter = (int)countMethod.Invoke(pendingChunksStore, null);
        Assert.AreEqual(0, countAfter, "ProcessPendingChunks should have removed the completed canceled chunk.");
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(obj, value);
    }

    private object GetPrivateField(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field.GetValue(obj);
    }
}
