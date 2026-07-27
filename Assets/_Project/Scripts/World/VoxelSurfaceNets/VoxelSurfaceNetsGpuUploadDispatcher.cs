using System;
using Hecton8.Core;
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
        private VoxelSurfaceNetsGpuUploadSourceLease _pendingSourceLease;

        public bool Initialize(int maxVertices, int maxIndices)
        {
            int vertexCapacity = math.clamp(maxVertices, 1, VoxelSurfaceNetsConstants.MaxVertices);
            int indexCapacity = math.clamp(maxIndices, 1, VoxelSurfaceNetsConstants.MaxIndices);
            if (IsInitialized() && _maxVertices == vertexCapacity && _maxIndices == indexCapacity)
                return true;

            if (_releaseRequested && !TryRelease())
                return false;

            if (!TryRelease())
                return false;

            _vertexFront = CreateLockBuffer(GraphicsBuffer.Target.Structured, vertexCapacity, UnsafeUtility.SizeOf<VoxelVertexDTO>());
            _vertexBack = CreateLockBuffer(GraphicsBuffer.Target.Structured, vertexCapacity, UnsafeUtility.SizeOf<VoxelVertexDTO>());
            _indexFront = CreateLockBuffer(GraphicsBuffer.Target.Index, indexCapacity, UnsafeUtility.SizeOf<uint>());
            _indexBack = CreateLockBuffer(GraphicsBuffer.Target.Index, indexCapacity, UnsafeUtility.SizeOf<uint>());
            _indirectArgs = CreateLockBuffer(GraphicsBuffer.Target.IndirectArguments, 1, UnsafeUtility.SizeOf<VoxelSurfaceIndirectArgsDTO>());
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

        private static bool IsGraphicsBufferReady(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid();
        }

        public bool IsInitialized()
        {
            return IsGraphicsBufferReady(_vertexFront) &&
                   IsGraphicsBufferReady(_vertexBack) &&
                   IsGraphicsBufferReady(_indexFront) &&
                   IsGraphicsBufferReady(_indexBack) &&
                   IsGraphicsBufferReady(_indirectArgs);
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

            if (!IsInitialized())
            {
                TryRelease();
                return false;
            }

            if (_uploadInFlight)
                return false;

            if (!VoxelSurfaceNetsVault.TryResolveStatesOwnerView(in buffers, out NativeArray<ChunkMeshingStateDTO> states))
                return false;

            VoxelSurfaceNetsGpuUploadSourceLease sourceLease = default;
            bool sourceLeaseHeld = false;
            try
            {
                if (!states.IsCreated || (uint)chunkIndex >= (uint)states.Length)
                    return false;

                ChunkMeshingStateDTO state = states[chunkIndex];
                if (state.Stage != (byte)VoxelMeshingStage.ReadyForUpload ||
                    state.VertexCount <= 0 ||
                    state.IndexCount <= 0)
                {
                    return false;
                }

                int vertexCount = state.VertexCount;
                int indexCount = state.IndexCount;
                if (vertexCount <= 0 || indexCount <= 0)
                    return false;

                if (vertexCount > _maxVertices || indexCount > _maxIndices)
                {
                    state.Stage = (byte)VoxelMeshingStage.Fault;
                    state.Flags = (byte)(state.Flags | VoxelMeshingFlags.CapacityClamped);
                    states[chunkIndex] = state;
                    return false;
                }

                GraphicsBuffer vertexBuffer = _activeSet == 0 ? _vertexBack : _vertexFront;
                GraphicsBuffer indexBuffer = _activeSet == 0 ? _indexBack : _indexFront;
                int uploadSet = 1 - _activeSet;
                if (!IsGraphicsBufferReady(vertexBuffer) ||
                    !IsGraphicsBufferReady(indexBuffer) ||
                    !IsGraphicsBufferReady(_indirectArgs))
                {
                    state.Stage = (byte)VoxelMeshingStage.Fault;
                    state.Flags = (byte)(state.Flags | VoxelMeshingFlags.GpuResourceInvalid);
                    states[chunkIndex] = state;
                    _releaseRequested = true;
                    TryRelease();
                    return false;
                }

                if (!VoxelSurfaceNetsVault.TryAcquireGpuUploadSourceLease(
                        in buffers,
                        vertexCount,
                        indexCount,
                        out sourceLease,
                        out NativeArray<VoxelVertexDTO> sourceVertices,
                        out NativeArray<uint> sourceIndices,
                        out NativeArray<VoxelSurfaceIndirectArgsDTO> sourceIndirectArgs))
                {
                    state.Stage = (byte)VoxelMeshingStage.Fault;
                    state.Flags = (byte)(state.Flags | VoxelMeshingFlags.GpuResourceInvalid);
                    states[chunkIndex] = state;
                    return false;
                }

                sourceLeaseHeld = true;
                long uploadBytes =
                    GraphicsBufferUploadUtility.EstimateUploadBytes<VoxelVertexDTO>(vertexCount) +
                    GraphicsBufferUploadUtility.EstimateUploadBytes<uint>(indexCount) +
                    GraphicsBufferUploadUtility.EstimateUploadBytes<VoxelSurfaceIndirectArgsDTO>(1);
                if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                    return false;

                bool vertexLocked = false;
                bool indexLocked = false;
                bool indirectArgsLocked = false;
                bool uploadScheduled = false;
                try
                {
                    NativeArray<VoxelVertexDTO> lockedVertices = vertexBuffer.LockBufferForWrite<VoxelVertexDTO>(0, vertexCount);
                    vertexLocked = true;
                    NativeArray<uint> lockedIndices = indexBuffer.LockBufferForWrite<uint>(0, indexCount);
                    indexLocked = true;
                    NativeArray<VoxelSurfaceIndirectArgsDTO> lockedIndirectArgs = _indirectArgs.LockBufferForWrite<VoxelSurfaceIndirectArgsDTO>(0, 1);
                    indirectArgsLocked = true;

                    VoxelSurfaceGpuUploadCopyJob copyJob = default;
                    copyJob.SourceVertices = sourceVertices;
                    copyJob.SourceIndices = sourceIndices;
                    copyJob.SourceIndirectArgs = sourceIndirectArgs;
                    copyJob.DestinationVertices = lockedVertices;
                    copyJob.DestinationIndices = lockedIndices;
                    copyJob.DestinationIndirectArgs = lockedIndirectArgs;
                    copyJob.VertexCount = vertexCount;
                    copyJob.IndexCount = indexCount;
                    uploadDependency = copyJob.Schedule(inputDependency);
                    _pendingUploadDependency = uploadDependency;
                    uploadScheduled = true;
                }
                catch
                {
                    if (indirectArgsLocked && IsGraphicsBufferReady(_indirectArgs))
                        TryUnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>(_indirectArgs, 1);

                    if (indexLocked && IsGraphicsBufferReady(indexBuffer))
                        TryUnlockBufferAfterWrite<uint>(indexBuffer, indexCount);

                    if (vertexLocked && IsGraphicsBufferReady(vertexBuffer))
                        TryUnlockBufferAfterWrite<VoxelVertexDTO>(vertexBuffer, vertexCount);

                    state.Stage = (byte)VoxelMeshingStage.Fault;
                    states[chunkIndex] = state;
                    uploadDependency = inputDependency;
                    return false;
                }
                finally
                {
                    if (uploadScheduled)
                        GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                    else
                        GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
                }

                state.Stage = (byte)VoxelMeshingStage.Uploading;
                states[chunkIndex] = state;

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
                _pendingSourceLease = sourceLease;
                sourceLeaseHeld = false;
                _pendingUploadSet = uploadSet;
                _pendingChunkIndex = chunkIndex;
                _pendingVertexCount = vertexCount;
                _pendingIndexCount = indexCount;
                _pendingChunkHash = state.ChunkHash;
                _pendingVersion = state.Version;
                _uploadInFlight = true;
                return true;
            }
            finally
            {
                if (sourceLeaseHeld)
                    VoxelSurfaceNetsVault.ReleaseGpuUploadSourceLease(ref sourceLease);
            }
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

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingUploadDependency))
                return false;

            bool pendingUploadResourcesReady = ArePendingUploadBuffersReady();

            UnlockPendingUploadBuffers();
            ReleasePendingSourceLease();
            if (!pendingUploadResourcesReady)
            {
                MarkPendingChunkFault(buffers, VoxelMeshingFlags.GpuResourceInvalid);
                _releaseRequested = true;
                ClearPendingUploadState();
                TryRelease();
                return false;
            }

            _activeSet = _pendingUploadSet;
            if (!VoxelSurfaceNetsVault.TryResolveStatesOwnerView(in buffers, out NativeArray<ChunkMeshingStateDTO> states))
            {
                ClearPendingUploadState();
                return false;
            }

            if (states.IsCreated && (uint)_pendingChunkIndex < (uint)states.Length)
            {
                ChunkMeshingStateDTO state = states[_pendingChunkIndex];
                if (state.ChunkHash == _pendingChunkHash && state.Version == _pendingVersion)
                {
                    state.Stage = (byte)VoxelMeshingStage.Uploaded;
                    state.Flags = (byte)(state.Flags & ~VoxelMeshingFlags.Dirty);
                    states[_pendingChunkIndex] = state;
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

                if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingUploadDependency))
                {
                    _releaseRequested = true;
                    return false;
                }

                UnlockPendingUploadBuffers();
                ReleasePendingSourceLease();
                ClearPendingUploadState();
            }

            ReleaseGraphicsBuffer(ref _vertexFront);
            ReleaseGraphicsBuffer(ref _vertexBack);
            ReleaseGraphicsBuffer(ref _indexFront);
            ReleaseGraphicsBuffer(ref _indexBack);
            ReleaseGraphicsBuffer(ref _indirectArgs);

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
            ReleasePendingSourceLease();
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

        private void ReleasePendingSourceLease()
        {
            if (_pendingSourceLease.IsCreated())
                VoxelSurfaceNetsVault.ReleaseGpuUploadSourceLease(ref _pendingSourceLease);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            if (buffer.IsValid())
                buffer.Release();

            buffer = null;
        }

        private bool ArePendingUploadBuffersReady()
        {
            return IsGraphicsBufferReady(_pendingVertexBuffer) &&
                   IsGraphicsBufferReady(_pendingIndexBuffer) &&
                   IsGraphicsBufferReady(_indirectArgs);
        }

        private void MarkPendingChunkFault(VoxelSurfaceNetsVaultBuffers buffers, byte flags)
        {
            if (!VoxelSurfaceNetsVault.TryResolveStatesOwnerView(in buffers, out NativeArray<ChunkMeshingStateDTO> states))
                return;

            if (!states.IsCreated || (uint)_pendingChunkIndex >= (uint)states.Length)
                return;

            ChunkMeshingStateDTO state = states[_pendingChunkIndex];
            if (state.ChunkHash != _pendingChunkHash || state.Version != _pendingVersion)
                return;

            state.Stage = (byte)VoxelMeshingStage.Fault;
            state.Flags = (byte)(state.Flags | flags);
            states[_pendingChunkIndex] = state;
        }

        private void UnlockPendingUploadBuffers()
        {
            if (IsGraphicsBufferReady(_pendingVertexBuffer) && _pendingVertexCount > 0)
                TryUnlockBufferAfterWrite<VoxelVertexDTO>(_pendingVertexBuffer, _pendingVertexCount);

            if (IsGraphicsBufferReady(_pendingIndexBuffer) && _pendingIndexCount > 0)
                TryUnlockBufferAfterWrite<uint>(_pendingIndexBuffer, _pendingIndexCount);

            if (IsGraphicsBufferReady(_indirectArgs))
                TryUnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>(_indirectArgs, 1);
        }

        private static bool TryUnlockBufferAfterWrite<T>(GraphicsBuffer buffer, int count) where T : struct
        {
            try
            {
                buffer.UnlockBufferAfterWrite<T>(count);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
