using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.VoxelSurfaceNets
{
    public struct VoxelSurfaceGpuUploadState
    {
        public GraphicsBuffer VertexBuffer;
        public GraphicsBuffer IndexBuffer;
        public GraphicsBuffer IndirectArgsBuffer;
        public int VertexCount;
        public int IndexCount;
        public uint ChunkHash;
        public uint Version;
        public int BufferSet;
    }

    public sealed unsafe class VoxelSurfaceNetsGpuUploadDispatcher : IDisposable
    {
        private GraphicsBuffer _vertexFront;
        private GraphicsBuffer _vertexBack;
        private GraphicsBuffer _indexFront;
        private GraphicsBuffer _indexBack;
        private GraphicsBuffer _indirectArgs;
        private int _activeSet;
        private int _maxVertices;
        private int _maxIndices;
        private bool _uploadInFlight;
        private NativeArray<VoxelVertexDTO> _lockedVertices;
        private NativeArray<uint> _lockedIndices;
        private NativeArray<VoxelSurfaceIndirectArgsDTO> _lockedIndirectArgs;
        private GraphicsBuffer _pendingVertexBuffer;
        private GraphicsBuffer _pendingIndexBuffer;
        private JobHandle _pendingUploadDependency;
        private int _pendingUploadSet;
        private int _pendingChunkIndex;
        private int _pendingVertexCount;
        private int _pendingIndexCount;
        private uint _pendingChunkHash;
        private uint _pendingVersion;
        private bool _releaseRequested;

        public bool Initialize(int maxVertices, int maxIndices)
        {
            int vertexCapacity = math.clamp(maxVertices, 1, VoxelSurfaceNetsConstants.MaxVertices);
            int indexCapacity = math.clamp(maxIndices, 1, VoxelSurfaceNetsConstants.MaxIndices);
            if (_vertexFront != null && _maxVertices == vertexCapacity && _maxIndices == indexCapacity)
                return true;

            if (_releaseRequested && !TryRelease())
                return false;

            if (!TryRelease())
                return false;

            _vertexFront = CreateLockBuffer(GraphicsBuffer.Target.Structured, vertexCapacity, UnsafeUtility.SizeOf<VoxelVertexDTO>());
            _vertexBack = CreateLockBuffer(GraphicsBuffer.Target.Structured, vertexCapacity, UnsafeUtility.SizeOf<VoxelVertexDTO>());
            _indexFront = CreateLockBuffer(GraphicsBuffer.Target.Index, indexCapacity, UnsafeUtility.SizeOf<uint>());
            _indexBack = CreateLockBuffer(GraphicsBuffer.Target.Index, indexCapacity, UnsafeUtility.SizeOf<uint>());
            _indirectArgs = CreateLockBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, UnsafeUtility.SizeOf<VoxelSurfaceIndirectArgsDTO>());
            _activeSet = 0;
            _maxVertices = vertexCapacity;
            _maxIndices = indexCapacity;
            return true;
        }

        private static GraphicsBuffer CreateLockBuffer(GraphicsBuffer.Target target, int count, int stride)
        {
            return new GraphicsBuffer(
                target,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
        }

        public bool IsInitialized()
        {
            return _vertexFront != null &&
                   _vertexBack != null &&
                   _indexFront != null &&
                   _indexBack != null &&
                   _indirectArgs != null;
        }

        public bool TryUpload(
            VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            out VoxelSurfaceGpuUploadState uploadState)
        {
            uploadState = default;
            return false;
        }

        public bool TryBeginUpload(
            VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            JobHandle inputDependency,
            out JobHandle uploadDependency,
            out VoxelSurfaceGpuUploadState uploadState)
        {
            uploadState = default;
            uploadDependency = inputDependency;
            if (_releaseRequested)
            {
                TryRelease();
                return false;
            }

            if (!IsInitialized() ||
                _uploadInFlight ||
                !buffers.Vertices.IsCreated ||
                !buffers.Indices.IsCreated ||
                !buffers.IndirectArgs.IsCreated ||
                !buffers.States.IsCreated ||
                (uint)chunkIndex >= (uint)buffers.States.Length)
            {
                return false;
            }

            ChunkMeshingStateDTO state = buffers.States[chunkIndex];
            if (state.Stage != (byte)VoxelMeshingStage.ReadyForUpload ||
                state.VertexCount <= 0 ||
                state.IndexCount <= 0)
            {
                return false;
            }

            if (state.VertexCount > buffers.Vertices.Length ||
                state.VertexCount > _maxVertices ||
                state.IndexCount > buffers.Indices.Length ||
                state.IndexCount > _maxIndices)
            {
                state.Stage = (byte)VoxelMeshingStage.Fault;
                state.Flags = (byte)(state.Flags | VoxelMeshingFlags.CapacityClamped);
                buffers.States[chunkIndex] = state;
                return false;
            }

            int vertexCount = state.VertexCount;
            int indexCount = state.IndexCount;
            if (vertexCount <= 0 || indexCount <= 0)
                return false;

            GraphicsBuffer vertexBuffer = _activeSet == 0 ? _vertexBack : _vertexFront;
            GraphicsBuffer indexBuffer = _activeSet == 0 ? _indexBack : _indexFront;
            int uploadSet = 1 - _activeSet;

            bool vertexLocked = false;
            bool indexLocked = false;
            bool indirectArgsLocked = false;
            try
            {
                _lockedVertices = vertexBuffer.LockBufferForWrite<VoxelVertexDTO>(0, vertexCount);
                vertexLocked = true;
                _lockedIndices = indexBuffer.LockBufferForWrite<uint>(0, indexCount);
                indexLocked = true;
                _lockedIndirectArgs = _indirectArgs.LockBufferForWrite<VoxelSurfaceIndirectArgsDTO>(0, 1);
                indirectArgsLocked = true;

                VoxelSurfaceGpuUploadCopyJob copyJob = default;
                copyJob.SourceVertices = buffers.Vertices;
                copyJob.SourceIndices = buffers.Indices;
                copyJob.SourceIndirectArgs = buffers.IndirectArgs;
                copyJob.DestinationVertices = _lockedVertices;
                copyJob.DestinationIndices = _lockedIndices;
                copyJob.DestinationIndirectArgs = _lockedIndirectArgs;
                copyJob.VertexCount = vertexCount;
                copyJob.IndexCount = indexCount;
                uploadDependency = copyJob.Schedule(inputDependency);
                _pendingUploadDependency = uploadDependency;
            }
            catch
            {
                if (indirectArgsLocked && _indirectArgs != null)
                    _indirectArgs.UnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>(1);

                if (indexLocked && indexBuffer != null)
                    indexBuffer.UnlockBufferAfterWrite<uint>(indexCount);

                if (vertexLocked && vertexBuffer != null)
                    vertexBuffer.UnlockBufferAfterWrite<VoxelVertexDTO>(vertexCount);

                _lockedVertices = default;
                _lockedIndices = default;
                _lockedIndirectArgs = default;
                state.Stage = (byte)VoxelMeshingStage.Fault;
                buffers.States[chunkIndex] = state;
                uploadDependency = inputDependency;
                return false;
            }

            state.Stage = (byte)VoxelMeshingStage.Uploading;
            buffers.States[chunkIndex] = state;

            uploadState.VertexBuffer = vertexBuffer;
            uploadState.IndexBuffer = indexBuffer;
            uploadState.IndirectArgsBuffer = _indirectArgs;
            uploadState.VertexCount = vertexCount;
            uploadState.IndexCount = indexCount;
            uploadState.ChunkHash = state.ChunkHash;
            uploadState.Version = state.Version;
            uploadState.BufferSet = uploadSet;

            _pendingVertexBuffer = vertexBuffer;
            _pendingIndexBuffer = indexBuffer;
            _pendingUploadSet = uploadSet;
            _pendingChunkIndex = chunkIndex;
            _pendingVertexCount = vertexCount;
            _pendingIndexCount = indexCount;
            _pendingChunkHash = state.ChunkHash;
            _pendingVersion = state.Version;
            _uploadInFlight = true;
            return true;
        }

        public bool TryFinalizeUpload(
            VoxelSurfaceNetsVaultBuffers buffers,
            JobHandle uploadDependency,
            out VoxelSurfaceGpuUploadState uploadState)
        {
            uploadState = default;
            if (!_uploadInFlight ||
                !uploadDependency.IsCompleted ||
                !_pendingUploadDependency.IsCompleted)
            {
                return false;
            }

            _pendingUploadDependency.Complete();

            UnlockPendingUploadBuffers();

            _activeSet = _pendingUploadSet;
            if (buffers.States.IsCreated && (uint)_pendingChunkIndex < (uint)buffers.States.Length)
            {
                ChunkMeshingStateDTO state = buffers.States[_pendingChunkIndex];
                if (state.ChunkHash == _pendingChunkHash && state.Version == _pendingVersion)
                {
                    state.Stage = (byte)VoxelMeshingStage.Uploaded;
                    state.Flags = (byte)(state.Flags & ~VoxelMeshingFlags.Dirty);
                    buffers.States[_pendingChunkIndex] = state;
                }
            }

            uploadState.VertexBuffer = _pendingVertexBuffer;
            uploadState.IndexBuffer = _pendingIndexBuffer;
            uploadState.IndirectArgsBuffer = _indirectArgs;
            uploadState.VertexCount = _pendingVertexCount;
            uploadState.IndexCount = _pendingIndexCount;
            uploadState.ChunkHash = _pendingChunkHash;
            uploadState.Version = _pendingVersion;
            uploadState.BufferSet = _pendingUploadSet;
            ClearPendingUploadState();
            if (_releaseRequested)
            {
                uploadState = default;
                TryRelease();
                return false;
            }

            return true;
        }

        public void Release()
        {
            _releaseRequested = true;
            TryRelease();
        }

        public bool TryRelease()
        {
            if (_uploadInFlight)
            {
                if (!_pendingUploadDependency.IsCompleted)
                {
                    _releaseRequested = true;
                    return false;
                }

                _pendingUploadDependency.Complete();
                UnlockPendingUploadBuffers();
                ClearPendingUploadState();
            }

            if (_vertexFront != null)
            {
                _vertexFront.Release();
                _vertexFront = null;
            }

            if (_vertexBack != null)
            {
                _vertexBack.Release();
                _vertexBack = null;
            }

            if (_indexFront != null)
            {
                _indexFront.Release();
                _indexFront = null;
            }

            if (_indexBack != null)
            {
                _indexBack.Release();
                _indexBack = null;
            }

            if (_indirectArgs != null)
            {
                _indirectArgs.Release();
                _indirectArgs = null;
            }

            _activeSet = 0;
            _maxVertices = 0;
            _maxIndices = 0;
            ClearPendingUploadState();
            _releaseRequested = false;
            return true;
        }

        public void Dispose()
        {
            Release();
        }

        private void ClearPendingUploadState()
        {
            _lockedVertices = default;
            _lockedIndices = default;
            _lockedIndirectArgs = default;
            _pendingVertexBuffer = null;
            _pendingIndexBuffer = null;
            _pendingUploadDependency = default;
            _pendingUploadSet = 0;
            _pendingChunkIndex = -1;
            _pendingVertexCount = 0;
            _pendingIndexCount = 0;
            _pendingChunkHash = 0u;
            _pendingVersion = 0u;
            _uploadInFlight = false;
        }

        private void UnlockPendingUploadBuffers()
        {
            if (_pendingVertexBuffer != null && _pendingVertexCount > 0)
                _pendingVertexBuffer.UnlockBufferAfterWrite<VoxelVertexDTO>(_pendingVertexCount);

            if (_pendingIndexBuffer != null && _pendingIndexCount > 0)
                _pendingIndexBuffer.UnlockBufferAfterWrite<uint>(_pendingIndexCount);

            if (_indirectArgs != null)
                _indirectArgs.UnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>(1);
        }
    }
}
