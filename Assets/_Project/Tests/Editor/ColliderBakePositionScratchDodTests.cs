using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Physics;
using Hecton8.World.VoxelSurfaceNets;

namespace Hecton8.Physics.Tests
{
    /// <summary>
    /// Edit-mode verification of the DOD ColliderBakePositionScratch extraction. Exercises the
    /// persistent buffer growth, the Burst fill jobs (float3 position copy + uint->int index copy),
    /// and the length-bounded Mesh hand-off path via the public API (EnsureCapacity / FillPositions /
    /// FillIndices / ApplyToMesh / Dispose).
    /// </summary>
    public class ColliderBakePositionScratchDodTests
    {
        [Test]
        public void FillJobs_PopulatePositionsAndIndices_AndApplyToMesh_Correctly()
        {
            var scratch = new ColliderBakePositionScratch();
            int vertCount = 8;
            int indexCount = 12;

            var vertices = new NativeArray<VoxelVertexDTO>(vertCount, Allocator.TempJob);
            var indices = new NativeArray<uint>(indexCount, Allocator.TempJob);
            for (int i = 0; i < vertCount; i++)
                vertices[i] = new VoxelVertexDTO { Position = new float3(i, i * 2, i * 3) };
            for (int i = 0; i < indexCount; i++)
                indices[i] = (uint)(i * 2);

            var mesh = new Mesh();

            try
            {
                scratch.EnsureCapacity(vertCount, indexCount);

                JobHandle h = scratch.FillPositions(vertices, vertCount);
                h = JobHandle.CombineDependencies(h, scratch.FillIndices(indices, indexCount));
                h.Complete();

                scratch.ApplyToMesh(mesh, vertCount, indexCount);

                // Read back from the Mesh: vertices are the float3 copies, triangles the uint->int copies.
                var meshVertices = mesh.vertices;
                var meshIndices = mesh.triangles;
                Assert.AreEqual(vertCount, meshVertices.Length);
                Assert.AreEqual(indexCount, meshIndices.Length);

                for (int i = 0; i < vertCount; i++)
                {
                    Assert.AreEqual(new Vector3(i, i * 2, i * 3), meshVertices[i], 1e-5f,
                        $"Vertex {i} must match the source VoxelVertexDTO.");
                }
                for (int i = 0; i < indexCount; i++)
                {
                    Assert.AreEqual((int)(i * 2), meshIndices[i], $"Triangle index {i} must be the uint->int copy.");
                }
            }
            finally
            {
                vertices.Dispose();
                indices.Dispose();
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
                scratch.Dispose();
            }
        }

        [Test]
        public void EnsureCapacity_GrowsMonotonically_And_NeverShrinks()
        {
            var scratch = new ColliderBakePositionScratch();
            try
            {
                scratch.EnsureCapacity(5, 9);
                Assert.IsTrue(scratch.PositionCapacity >= 5);
                Assert.IsTrue(scratch.IndexCapacity >= 9);

                int posCap = scratch.PositionCapacity;
                int idxCap = scratch.IndexCapacity;

                // Force a grow past the current capacity.
                scratch.EnsureCapacity(64, 128);
                Assert.IsTrue(scratch.PositionCapacity >= 64, "Capacity must grow to cover the new count.");
                Assert.IsTrue(scratch.IndexCapacity >= 128, "Capacity must grow to cover the new count.");

                // Requesting a smaller size must not shrink the buffers.
                scratch.EnsureCapacity(2, 3);
                Assert.GreaterOrEqual(scratch.PositionCapacity, posCap, "Capacity must never shrink.");
                Assert.GreaterOrEqual(scratch.IndexCapacity, idxCap, "Capacity must never shrink.");
            }
            finally
            {
                scratch.Dispose();
            }
        }

        [Test]
        public void Dispose_Is_Idempotent()
        {
            var scratch = new ColliderBakePositionScratch();
            scratch.EnsureCapacity(4, 6);
            Assert.IsTrue(scratch.IsCreated, "After EnsureCapacity the scratch must report created.");
            scratch.Dispose();
            Assert.IsFalse(scratch.IsCreated, "After Dispose the scratch must not report created.");
            // Second dispose must be a safe no-op (no double-free).
            scratch.Dispose();
        }
    }
}
