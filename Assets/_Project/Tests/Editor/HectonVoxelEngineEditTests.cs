using NUnit.Framework;
using UnityEngine;
using Hecton8.Core.Memory;
using NSubstitute;
using Unity.Collections;

public class HectonVoxelEngineEditTests
{
    [TearDown]
    public void TearDown()
    {
        HectonVoxelEngine.Shutdown();
    }

    [Test]
    public void Initialize_NullVault_ReturnsEarly()
    {
        HectonVoxelEngine.Initialize(null);

        // We can assert that the engine is not ready by checking the read lease
        var handle = HectonVoxelEngine.GetReadLease(0);
        Assert.IsFalse(handle.IsCreated);
    }

    [Test]
    public void Initialize_SameVaultRepeated_ReturnsEarly()
    {
        var vault = Substitute.For<IDataVault>();

        // This is mainly a code path hit coverage test.
        // It should avoid re-initializing if called twice with same vault
        HectonVoxelEngine.Initialize(vault);
        HectonVoxelEngine.Initialize(vault);

        var handle = HectonVoxelEngine.GetReadLease(0);
        Assert.IsFalse(handle.IsCreated); // since mock didn't return a valid table
    }
}
