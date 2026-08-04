using Hecton8.World.VoxelSurfaceNets;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Burst-compatible DOD structure owning the persistent scratch buffers used to upload a chunk's
    /// collider mesh. Extracted out of the <c>HectonVoxelEngine</c> MonoBehaviour so the source fill
    /// runs in Burst-compiled jobs over native memory instead of managed per-chunk loops.
    ///
    /// Owns two <c>Allocator.Persistent</c> buffers (one <see cref="float3"/> positions, one
    /// <c>int</c> indices). <see cref="EnsureCapacity"/> grows each buffer monotonically via
    /// <c>math.ceilpow2</c> to its high-water mark and never shrinks, so the streaming bake path
    /// performs zero per-chunk allocations (no GC) once warmed.
    ///
    /// INVARIANT (mirrors the original field contract): the native buffers are filled by the jobs and
    /// then handed to a Unity <see cref="Mesh"/> with NO await in between. Continuations on the main
    /// thread only interleave at await points, so a second chunk iteration can never observe a
    /// half-filled buffer. Do not insert an await between the fill and <see cref="ApplyToMesh"/>
    /// without giving each in-flight bake its own instance.
    /// </summary>
    public struct ColliderBakePositionScratch : System.IDisposable
    {
        // Native source-of-truth buffers, handed to the Burst fill jobs.
        private NativeArray<float3> _positions;
        private NativeArray<int> _indices;

        // Legacy managed bridges have been purged (Zero-copy mandate).
        // Mesh.SetVertexBufferData accepts NativeArray<float3> directly.

        private int _positionCapacity;
        private int _indexCapacity;
        private bool _isCreated;

        /// <summary>True once <see cref="Initialize"/> has allocated the persistent buffers.</summary>
        public bool IsCreated => _isCreated;

        /// <summary>Allocated native capacity of the positions buffer.</summary>
        public int PositionCapacity => _positionCapacity;

        /// <summary>Allocated native capacity of the indices buffer.</summary>
        public int IndexCapacity => _indexCapacity;

        /// <summary>
        /// Allocates both persistent buffers (and their managed upload bridges) at the given
        /// capacities, rounded up to powers of two. Idempotent: safe to call on an already-initialized
        /// instance, in which case it is a no-op.
        /// </summary>
        public void Initialize(int positionCapacity, int indexCapacity)
        {
            if (_isCreated)
                return;

            _positionCapacity = math.max(1, math.ceilpow2(math.max(1, positionCapacity)));
            _indexCapacity = math.max(1, math.ceilpow2(math.max(1, indexCapacity)));

            _positions = new NativeArray<float3>(_positionCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _indices = new NativeArray<int>(_indexCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Managed array bridges removed

            _isCreated = true;
        }

        /// <summary>
        /// Grows the persistent buffers to cover <paramref name="vertCount"/> positions and
        /// <paramref name="indexCount"/> indices, each to the next power of two. Monotonic: buffers are
        /// never shrunk, and an already-sufficient capacity is a no-op. Must be called before running
        /// the fill jobs for a chunk whose count exceeds the current capacity.
        /// </summary>
        public void EnsureCapacity(int vertCount, int indexCount)
        {
            if (!_isCreated)
            {
                Initialize(vertCount, indexCount);
                return;
            }

            if (vertCount > _positionCapacity)
            {
                int newCapacity = math.max(1, math.ceilpow2(math.max(1, vertCount)));
                NativeArray<float3> resized = new NativeArray<float3>(newCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<float3>.Copy(_positions, resized, math.min(_positionCapacity, newCapacity));
                _positions.Dispose();
                _positions = resized;
                _positionCapacity = newCapacity;
                _positionCapacity = newCapacity;
            }

            if (indexCount > _indexCapacity)
            {
                int newCapacity = math.max(1, math.ceilpow2(math.max(1, indexCount)));
                NativeArray<int> resized = new NativeArray<int>(newCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<int>.Copy(_indices, resized, math.min(_indexCapacity, newCapacity));
                _indices.Dispose();
                _indices = resized;
                _indexCapacity = newCapacity;
                _indexCapacity = newCapacity;
            }
        }

        /// <summary>
        /// Fills the positions native buffer from a contiguous prefix of
        /// <paramref name="colliderVertices"/> using a Burst <c>IJobParallelFor</c>. Returns the job
        /// handle. The caller is responsible for scheduling (<c>JobHandle.Schedule</c>) and completing
        /// the returned handle before calling <see cref="ApplyToMesh"/>.
        /// </summary>
        public JobHandle FillPositions(NativeArray<VoxelVertexDTO> colliderVertices, int vertCount, JobHandle dependsOn = default)
        {
            FillColliderPositionsJob job = new FillColliderPositionsJob
            {
                Source = colliderVertices,
                Dest = _positions
            };
            return job.Schedule(vertCount, 64, dependsOn);
        }

        /// <summary>
        /// Fills the indices native buffer from a contiguous prefix of <paramref name="colliderIndices"/>
        /// using a Burst <c>IJobParallelFor</c>. Returns the job handle. The caller is responsible for
        /// scheduling and completing the returned handle before calling <see cref="ApplyToMesh"/>.
        /// </summary>
        public JobHandle FillIndices(NativeArray<uint> colliderIndices, int indexCount, JobHandle dependsOn = default)
        {
            FillColliderIndicesJob job = new FillColliderIndicesJob
            {
                Source = colliderIndices,
                Dest = _indices
            };
            return job.Schedule(indexCount, 64, dependsOn);
        }

        /// <summary>
        /// Hands the native data to a Unity <see cref="Mesh"/> via zero-copy modern APIs
        /// <c>SetVertexBufferData</c> and <c>SetIndexBufferData</c>.
        /// Must be preceded by a completed <see cref="FillPositions"/> / <see cref="FillIndices"/> 
        /// with no intervening await.
        /// </summary>
        public void ApplyToMesh(Mesh mesh, int vertCount, int indexCount)
        {
            if (!_isCreated)
                return;

            if (mesh == null)
                return;

            if (vertCount > _positions.Length || indexCount > _indices.Length)
                return;

            mesh.Clear(false);

            // Allocate and push vertex buffer (Zero-copy native to native)
            mesh.SetVertexBufferParams(vertCount, new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3));
            mesh.SetVertexBufferData(_positions, 0, 0, vertCount, 0, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

            // Allocate and push index buffer (Zero-copy native to native)
            mesh.SetIndexBufferParams(indexCount, UnityEngine.Rendering.IndexFormat.UInt32);
            mesh.SetIndexBufferData(_indices, 0, 0, indexCount, 0, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

            // Rebuild submesh descriptor
            mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
        }

        /// <summary>
        /// Convenience that mirrors the original synchronous bake flow: ensures capacity, fills both
        /// native buffers from their sources via Burst jobs, completes them, and uploads to
        /// <paramref name="mesh"/> — all with no await between fill and upload. Any <paramref name="dependsOn"/>
        /// (e.g. the extraction/bake job handle from the vault) is chained ahead of the fill jobs.
        /// </summary>
        public void FillAndApplyToMesh(
            Mesh mesh,
            NativeArray<VoxelVertexDTO> colliderVertices,
            NativeArray<uint> colliderIndices,
            int vertCount,
            int indexCount,
            JobHandle dependsOn = default)
        {
            // EnsureCapacity lazy-initializes the persistent buffers on first use, so this
            // convenience path works for a default-initialized struct field (e.g. freshly awakened
            // MonoBehaviour component) without a separate Awake-time Initialize call.
            EnsureCapacity(vertCount, indexCount);

            if (!_isCreated)
                return;

            JobHandle fillHandle = FillPositions(colliderVertices, vertCount, dependsOn);
            fillHandle = JobHandle.CombineDependencies(fillHandle, FillIndices(colliderIndices, indexCount, dependsOn));
            fillHandle.Complete();

            ApplyToMesh(mesh, vertCount, indexCount);
        }

        /// <summary>Releases both persistent buffers and the managed bridges. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isCreated)
            {
                if (_positions.IsCreated)
                    _positions.Dispose();
                if (_indices.IsCreated)
                    _indices.Dispose();

                _positions = default;
                _indices = default;
                _positionCapacity = 0;
                _positionCapacity = 0;
                _indexCapacity = 0;
                _isCreated = false;
            }
        }

        /// <summary>
        /// Burst job that fills the positions native buffer from a <see cref="VoxelVertexDTO"/>
        /// source. <see cref="VoxelVertexDTO.Position"/> is a <c>float3</c> at field offset 0.
        /// </summary>
        [BurstCompile]
        private struct FillColliderPositionsJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<VoxelVertexDTO> Source;

            [WriteOnly]
            public NativeArray<float3> Dest;

            public void Execute(int index)
            {
                Dest[index] = Source[index].Position;
            }
        }

        /// <summary>
        /// Burst job that fills the indices native buffer from a <c>NativeArray&lt;uint&gt;</c> source,
        /// converting each element to <c>int</c>.
        /// </summary>
        [BurstCompile]
        private struct FillColliderIndicesJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<uint> Source;

            [WriteOnly]
            public NativeArray<int> Dest;

            public void Execute(int index)
            {
                Dest[index] = (int)Source[index];
            }
        }
    }
}
