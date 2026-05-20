using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Graphics.Culling
{
    /// <summary>
    /// Compute-backed procedural instance culler for generated flora/manual-BRG data.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-88)]
    public sealed class InstanceCullingService : MonoBehaviour, IInstanceCullingService
    {
        private const int DefaultCapacity = 100000;
        private const int TelemetryCapacity = 300;
        private const int IndirectArgsCount = 5;
        private const int MatrixStride = 64;
        private const int TelemetryReadbackStride = 3;
        private const int OverloadVisibleThreshold = 50000;
        private const float DefaultCullDistanceMeters = 200f;
        private const float LowTierCullDistanceMeters = 100f;
        private const float VramDownsampleThresholdMb = 1600f;
        private const uint TelemetryInvalidStateFlag = 1u << 0;
        private const uint TelemetryOverloadFlag = 1u << 1;
        private const uint TelemetryAupShiftFlag = 1u << 2;
        private const uint TelemetryDispatchFlag = 1u << 3;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_HLOD_INSTANCE_CULLING.bin";

        private static readonly int _AllInstancesId = Shader.PropertyToID("_HectonAllInstances");
        private static readonly int _VisibleInstancesId = Shader.PropertyToID("_HectonVisibleInstances");
        private static readonly int _InstanceCountId = Shader.PropertyToID("_HectonInstanceCount");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonCameraPosition");
        private static readonly int _CameraForwardId = Shader.PropertyToID("_HectonCameraForward");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonViewProjection");
        private static readonly int _Plane0Id = Shader.PropertyToID("_HectonFrustumPlane0");
        private static readonly int _Plane1Id = Shader.PropertyToID("_HectonFrustumPlane1");
        private static readonly int _Plane2Id = Shader.PropertyToID("_HectonFrustumPlane2");
        private static readonly int _Plane3Id = Shader.PropertyToID("_HectonFrustumPlane3");
        private static readonly int _Plane4Id = Shader.PropertyToID("_HectonFrustumPlane4");
        private static readonly int _Plane5Id = Shader.PropertyToID("_HectonFrustumPlane5");
        private static readonly int _BoundsRadiusId = Shader.PropertyToID("_HectonBoundsRadius");
        private static readonly int _CullDistanceId = Shader.PropertyToID("_HectonCullDistanceMeters");
        private static readonly int _QualityTierId = Shader.PropertyToID("_HectonQualityTier");
        private static readonly int _VramUsedMbId = Shader.PropertyToID("_HectonVramUsedMb");
        private static readonly int _FlagsId = Shader.PropertyToID("_HectonCullingFlags");
        private static readonly int _VoxelSdfTextureId = Shader.PropertyToID("_HectonVoxelSdfTexture3D");
        private static readonly int _VoxelSdfOriginId = Shader.PropertyToID("_HectonVoxelSdfOrigin");
        private static readonly int _VoxelSdfInvSizeId = Shader.PropertyToID("_HectonVoxelSdfInvSize");

        [Header("Compute")]
        [SerializeField]
        [Tooltip("Compute shader containing the CullInstances kernel.")]
        private ComputeShader _computeShader;

        [SerializeField, Min(1)]
        [Tooltip("Maximum procedural instance count kept in persistent append buffers.")]
        private int _capacity = DefaultCapacity;

        [SerializeField, Min(0.01f)]
        [Tooltip("Fallback bounding sphere radius when the matrix does not carry one in m31.")]
        private float _defaultBoundsRadius = 1f;

        [SerializeField]
        [Tooltip("Issue delayed AsyncGPUReadback of indirect args for black-box telemetry only.")]
        private bool _enableTelemetryReadback = true;

        private ComputeShader _activeComputeShader;
        private GraphicsBuffer _visibleInstancesBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private NativeArray<uint> _indirectArgsReadback;
        private NativeArray<InstanceCullingTelemetryEntry> _telemetryRing;
        private Action<AsyncGPUReadbackRequest> _cachedReadbackCallback;
        private InstanceCullingCameraPositionSignal _cameraPosition;
        private InstanceCullingCameraFrustumSignal _cameraFrustum;
        private Texture _voxelSdfTexture;
        private Vector3 _voxelSdfOrigin;
        private Vector3 _voxelSdfSize = Vector3.one;
        private int _kernel = -1;
        private int _threadGroupSize = 64;
        private int _lastSourceInstanceCount;
        private int _lastVisibleInstanceCount;
        private int _lastCulledInstanceCount;
        private int _telemetryWriteIndex;
        private int _telemetryReadIndex;
        private int _telemetryQueuedCount;
        private int _readbackPending;
        private int _lastArgs0 = -1;
        private int _lastArgs2 = -1;
        private int _lastArgs3 = -1;
        private int _lastArgs4 = -1;
        private uint _lastTelemetryFrame;
        private uint _lastShiftFrameId;
        private float _lastCullDistance;
        private float _lastVramUsedMb;
        private uint _lastFlags;
        private bool _voxelSdfEnabled;
        private bool _dumpedInvalidState;

        /// <inheritdoc />
        public bool IsAvailable =>
            _activeComputeShader != null &&
            _kernel >= 0 &&
            _visibleInstancesBuffer != null &&
            _indirectArgsBuffer != null;

        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <inheritdoc />
        public int ThreadGroupSize => _threadGroupSize;

        /// <inheritdoc />
        public GraphicsBuffer VisibleInstancesBuffer => _visibleInstancesBuffer;

        /// <inheritdoc />
        public GraphicsBuffer IndirectArgsBuffer => _indirectArgsBuffer;

        /// <inheritdoc />
        public int LastVisibleInstanceCount => _lastVisibleInstanceCount;

        /// <inheritdoc />
        public int LastCulledInstanceCount => _lastCulledInstanceCount;

        private void Awake()
        {
            _cachedReadbackCallback = OnIndirectArgsReadback;
            if (_computeShader != null)
                Configure(_computeShader, _capacity);
        }

        private void OnEnable()
        {
            if (_visibleInstancesBuffer == null && _activeComputeShader != null)
                Configure(_activeComputeShader, _capacity);
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        /// <inheritdoc />
        public void Configure(ComputeShader computeShader, int capacity)
        {
            _activeComputeShader = computeShader;
            _capacity = math.max(1, capacity);
            _kernel = _activeComputeShader != null ? _activeComputeShader.FindKernel("CullInstances") : -1;
            if (_kernel >= 0)
            {
                _activeComputeShader.GetKernelThreadGroupSizes(_kernel, out uint groupX, out _, out _);
                _threadGroupSize = math.max(1, (int)groupX);
            }
            else
            {
                _threadGroupSize = 64;
            }

            EnsureResources();
        }

        /// <inheritdoc />
        public void ConsumeCameraPositionSignal(in InstanceCullingCameraPositionSignal signal)
        {
            _cameraPosition = signal;
        }

        /// <inheritdoc />
        public void ConsumeCameraFrustumSignal(in InstanceCullingCameraFrustumSignal signal)
        {
            _cameraFrustum = signal;
        }

        /// <inheritdoc />
        public void SetVoxelSdf(Texture voxelSdfTexture, Vector3 origin, Vector3 size, bool enabled)
        {
            _voxelSdfTexture = voxelSdfTexture;
            _voxelSdfOrigin = origin;
            _voxelSdfSize = new Vector3(
                Mathf.Max(0.001f, size.x),
                Mathf.Max(0.001f, size.y),
                Mathf.Max(0.001f, size.z));
            _voxelSdfEnabled = enabled && voxelSdfTexture != null;
        }

        /// <inheritdoc />
        public bool Dispatch(in InstanceCullingDispatchDescriptor descriptor)
        {
            if (!ValidateDispatch(in descriptor))
                return false;

            int instanceCount = math.min(descriptor.InstanceCount, _capacity);
            float cullDistance = ResolveCullDistance(in descriptor);
            uint flags = (uint)descriptor.Flags;
            if (descriptor.QualityTier == InstanceCullingQualityTier.Low)
                flags |= (uint)InstanceCullingDispatchFlags.LowTierDistance;
            if (descriptor.VramUsedMb > VramDownsampleThresholdMb)
                flags |= (uint)InstanceCullingDispatchFlags.VramDownsample;
            if (_voxelSdfEnabled)
                flags |= (uint)InstanceCullingDispatchFlags.VoxelSdfCull;

            _lastSourceInstanceCount = instanceCount;
            _lastCullDistance = cullDistance;
            _lastVramUsedMb = descriptor.VramUsedMb;
            _lastFlags = flags | TelemetryDispatchFlag;

            EnsureIndirectArgs(in descriptor.IndirectArgs);
            _visibleInstancesBuffer.SetCounterValue(0u);

            _activeComputeShader.SetBuffer(_kernel, _AllInstancesId, descriptor.AllInstancesBuffer);
            _activeComputeShader.SetBuffer(_kernel, _VisibleInstancesId, _visibleInstancesBuffer);
            _activeComputeShader.SetInt(_InstanceCountId, instanceCount);
            _activeComputeShader.SetVector(_CameraPositionId, _cameraPosition.Position);
            _activeComputeShader.SetVector(_CameraForwardId, _cameraPosition.Forward);
            _activeComputeShader.SetMatrix(_ViewProjectionId, _cameraFrustum.ViewProjection);
            _activeComputeShader.SetVector(_Plane0Id, _cameraFrustum.Plane0);
            _activeComputeShader.SetVector(_Plane1Id, _cameraFrustum.Plane1);
            _activeComputeShader.SetVector(_Plane2Id, _cameraFrustum.Plane2);
            _activeComputeShader.SetVector(_Plane3Id, _cameraFrustum.Plane3);
            _activeComputeShader.SetVector(_Plane4Id, _cameraFrustum.Plane4);
            _activeComputeShader.SetVector(_Plane5Id, _cameraFrustum.Plane5);
            _activeComputeShader.SetFloat(_BoundsRadiusId, math.max(0.001f, descriptor.BoundsRadius > 0f ? descriptor.BoundsRadius : _defaultBoundsRadius));
            _activeComputeShader.SetFloat(_CullDistanceId, cullDistance);
            _activeComputeShader.SetInt(_QualityTierId, (int)descriptor.QualityTier);
            _activeComputeShader.SetFloat(_VramUsedMbId, descriptor.VramUsedMb);
            _activeComputeShader.SetInt(_FlagsId, unchecked((int)flags));
            _activeComputeShader.SetVector(_VoxelSdfOriginId, _voxelSdfOrigin);
            _activeComputeShader.SetVector(
                _VoxelSdfInvSizeId,
                new Vector3(1f / _voxelSdfSize.x, 1f / _voxelSdfSize.y, 1f / _voxelSdfSize.z));
            if (_voxelSdfTexture != null)
                _activeComputeShader.SetTexture(_kernel, _VoxelSdfTextureId, _voxelSdfTexture);

            int dispatchGroups = (instanceCount + _threadGroupSize - 1) / _threadGroupSize;
            _activeComputeShader.Dispatch(_kernel, dispatchGroups, 1, 1);
            GraphicsBuffer.CopyCount(_visibleInstancesBuffer, _indirectArgsBuffer, sizeof(uint));
            TryRequestTelemetryReadback();
            return true;
        }

        /// <inheritdoc />
        public bool ApplyAupShift(GraphicsBuffer allInstancesBuffer, int instanceCount, Vector3 shiftMeters, uint shiftFrameId)
        {
            if (allInstancesBuffer == null || instanceCount <= 0 || allInstancesBuffer.count <= 0)
                return false;

            float3 shift = new float3(shiftMeters.x, shiftMeters.y, shiftMeters.z);
            if (!math.all(math.isfinite(shift)) || math.lengthsq(shift) <= 0.000001f)
                return false;

            int safeCount = math.min(instanceCount, allInstancesBuffer.count);
            NativeArray<Matrix4x4> matrices = allInstancesBuffer.LockBufferForWrite<Matrix4x4>(0, safeCount);
            JobHandle handle = new ApplyAupShiftJob
            {
                Matrices = matrices,
                ShiftMeters = shift
            }.Schedule(safeCount, _threadGroupSize);
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            allInstancesBuffer.UnlockBufferAfterWrite<Matrix4x4>(safeCount);

            _lastShiftFrameId = shiftFrameId;
            WriteTelemetry(
                unchecked((uint)Time.frameCount),
                _lastSourceInstanceCount,
                _lastVisibleInstanceCount,
                math.max(0, _lastSourceInstanceCount - _lastVisibleInstanceCount),
                _lastFlags | TelemetryAupShiftFlag,
                _lastCullDistance,
                _lastVramUsedMb);
            return true;
        }

        /// <inheritdoc />
        public bool TryConsumeTelemetry(out InstanceCullingTelemetry telemetry)
        {
            if (_telemetryQueuedCount <= 0 || !_telemetryRing.IsCreated)
            {
                telemetry = default;
                return false;
            }

            InstanceCullingTelemetryEntry entry = _telemetryRing[_telemetryReadIndex];
            _telemetryReadIndex++;
            if (_telemetryReadIndex >= TelemetryCapacity)
                _telemetryReadIndex = 0;
            _telemetryQueuedCount--;

            telemetry = new InstanceCullingTelemetry
            {
                Frame = entry.Frame,
                SourceInstances = entry.SourceInstances,
                VisibleInstances = entry.VisibleInstances,
                CulledInstances = entry.CulledInstances,
                Flags = entry.Flags,
                CullDistanceMeters = entry.CullDistanceMeters,
                VramUsedMb = entry.VramUsedMb
            };
            return true;
        }

        /// <inheritdoc />
        public void ReleaseResources()
        {
            _readbackPending = 0;
            ReleaseBuffer(ref _visibleInstancesBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);

            if (_indirectArgsReadback.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_indirectArgsReadback);
                _indirectArgsReadback.Dispose();
                _indirectArgsReadback = default;
            }

            if (_telemetryRing.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetryRing);
                _telemetryRing.Dispose();
                _telemetryRing = default;
            }

            _telemetryWriteIndex = 0;
            _telemetryReadIndex = 0;
            _telemetryQueuedCount = 0;
            _lastArgs0 = -1;
            _lastArgs2 = -1;
            _lastArgs3 = -1;
            _lastArgs4 = -1;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseResources();
        }

        private bool ValidateDispatch(in InstanceCullingDispatchDescriptor descriptor)
        {
            if (!IsAvailable || descriptor.AllInstancesBuffer == null || descriptor.InstanceCount <= 0)
            {
                WriteInvalidTelemetry();
                return false;
            }

            if (descriptor.AllInstancesBuffer.stride != MatrixStride || _visibleInstancesBuffer.stride != MatrixStride)
            {
                WriteInvalidTelemetry();
                return false;
            }

            Vector3 cameraPosition = _cameraPosition.Position;
            Vector3 cameraForward = _cameraPosition.Forward;
            float3 position = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
            float3 forward = new float3(cameraForward.x, cameraForward.y, cameraForward.z);
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(forward)))
            {
                WriteInvalidTelemetry();
                return false;
            }

            return true;
        }

        private float ResolveCullDistance(in InstanceCullingDispatchDescriptor descriptor)
        {
            float requested = descriptor.MaxCullDistanceMeters > 0f
                ? descriptor.MaxCullDistanceMeters
                : (descriptor.QualityTier == InstanceCullingQualityTier.Low ? LowTierCullDistanceMeters : DefaultCullDistanceMeters);
            return math.max(0.01f, requested);
        }

        private void EnsureResources()
        {
            if (_capacity <= 0)
                _capacity = DefaultCapacity;

            if (_visibleInstancesBuffer == null || _visibleInstancesBuffer.count != _capacity)
            {
                ReleaseBuffer(ref _visibleInstancesBuffer);
                _visibleInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, _capacity, MatrixStride); // COLD ALLOC: GraphicsBuffer[capacity] - append visible matrices - owner: InstanceCullingService
            }

            if (_indirectArgsBuffer == null)
                _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, IndirectArgsCount, sizeof(uint)); // COLD ALLOC: GraphicsBuffer[5] - indirect args written by CopyCount - owner: InstanceCullingService

            if (!_indirectArgsReadback.IsCreated)
            {
                _indirectArgsReadback = new NativeArray<uint>(IndirectArgsCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[5] - delayed indirect args telemetry readback - owner: InstanceCullingService
                NativeMemorySentinel.RegisterNativeArray(_indirectArgsReadback, nameof(InstanceCullingService), nameof(_indirectArgsReadback), NativeAllocationLifetime.Scene);
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<InstanceCullingTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<InstanceCullingTelemetryEntry>[300] - culling black-box ring - owner: InstanceCullingService
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, nameof(InstanceCullingService), nameof(_telemetryRing), NativeAllocationLifetime.Scene);
            }
        }

        private void EnsureIndirectArgs(in InstanceCullingIndirectArgs args)
        {
            int args0 = unchecked((int)args.IndexCountPerInstance);
            int args2 = unchecked((int)args.StartIndex);
            int args3 = unchecked((int)args.BaseVertexIndex);
            int args4 = unchecked((int)args.StartInstance);
            if (_lastArgs0 == args0 && _lastArgs2 == args2 && _lastArgs3 == args3 && _lastArgs4 == args4)
                return;

            NativeArray<uint> indirectArgs = _indirectArgsBuffer.LockBufferForWrite<uint>(0, IndirectArgsCount);
            indirectArgs[0] = args.IndexCountPerInstance;
            indirectArgs[1] = 0u;
            indirectArgs[2] = args.StartIndex;
            indirectArgs[3] = args.BaseVertexIndex;
            indirectArgs[4] = args.StartInstance;
            _indirectArgsBuffer.UnlockBufferAfterWrite<uint>(IndirectArgsCount);
            _lastArgs0 = args0;
            _lastArgs2 = args2;
            _lastArgs3 = args3;
            _lastArgs4 = args4;
        }

        private void TryRequestTelemetryReadback()
        {
            if (!_enableTelemetryReadback || _readbackPending != 0 || !_indirectArgsReadback.IsCreated)
                return;

            int frame = Time.frameCount;
            if (frame % TelemetryReadbackStride != 0)
                return;

            _readbackPending = 1;
            AsyncGPUReadback.RequestIntoNativeArray(ref _indirectArgsReadback, _indirectArgsBuffer, _cachedReadbackCallback);
        }

        private void OnIndirectArgsReadback(AsyncGPUReadbackRequest request)
        {
            _readbackPending = 0;
            if (request.hasError || !_indirectArgsReadback.IsCreated || _indirectArgsReadback.Length < 2)
            {
                WriteInvalidTelemetry();
                return;
            }

            int visible = math.max(0, unchecked((int)_indirectArgsReadback[1]));
            int culled = math.max(0, _lastSourceInstanceCount - visible);
            _lastVisibleInstanceCount = visible;
            _lastCulledInstanceCount = culled;
            uint flags = _lastFlags;
            if (visible > OverloadVisibleThreshold)
                flags |= TelemetryOverloadFlag;
            WriteTelemetry(
                unchecked((uint)Time.frameCount),
                _lastSourceInstanceCount,
                visible,
                culled,
                flags,
                _lastCullDistance,
                _lastVramUsedMb);
        }

        private void WriteInvalidTelemetry()
        {
            WriteTelemetry(
                unchecked((uint)Time.frameCount),
                _lastSourceInstanceCount,
                _lastVisibleInstanceCount,
                _lastCulledInstanceCount,
                _lastFlags | TelemetryInvalidStateFlag,
                _lastCullDistance,
                _lastVramUsedMb);

            if (!_dumpedInvalidState)
                DumpBlackBox();
        }

        private void WriteTelemetry(
            uint frame,
            int sourceInstances,
            int visibleInstances,
            int culledInstances,
            uint flags,
            float cullDistanceMeters,
            float vramUsedMb)
        {
            if (!_telemetryRing.IsCreated)
                return;

            uint stateHash = 2166136261u;
            stateHash = MixHash(stateHash, (uint)math.max(0, sourceInstances));
            stateHash = MixHash(stateHash, (uint)math.max(0, visibleInstances));
            stateHash = MixHash(stateHash, (uint)math.max(0, culledInstances));
            stateHash = MixHash(stateHash, _lastShiftFrameId);
            stateHash = MixHash(stateHash, flags);

            _telemetryRing[_telemetryWriteIndex] = new InstanceCullingTelemetryEntry
            {
                Frame = frame,
                SourceInstances = math.max(0, sourceInstances),
                VisibleInstances = math.max(0, visibleInstances),
                CulledInstances = math.max(0, culledInstances),
                Flags = flags,
                CullDistanceMeters = math.isfinite(cullDistanceMeters) ? cullDistanceMeters : 0f,
                VramUsedMb = math.isfinite(vramUsedMb) ? vramUsedMb : 0f,
                StateHash = stateHash,
                ShiftFrameId = _lastShiftFrameId
            };
            _lastTelemetryFrame = frame;
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= TelemetryCapacity)
                _telemetryWriteIndex = 0;

            if (_telemetryQueuedCount < TelemetryCapacity)
            {
                _telemetryQueuedCount++;
            }
            else
            {
                _telemetryReadIndex++;
                if (_telemetryReadIndex >= TelemetryCapacity)
                    _telemetryReadIndex = 0;
            }

            if ((flags & TelemetryInvalidStateFlag) != 0u && !_dumpedInvalidState)
                DumpBlackBox();
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private void DumpBlackBox()
        {
            if (_dumpedInvalidState || !_telemetryRing.IsCreated)
                return;

            _dumpedInvalidState = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryWriteIndex);
                writer.Write(_lastTelemetryFrame);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    InstanceCullingTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.SourceInstances);
                    writer.Write(entry.VisibleInstances);
                    writer.Write(entry.CulledInstances);
                    writer.Write(entry.Flags);
                    writer.Write(entry.CullDistanceMeters);
                    writer.Write(entry.VramUsedMb);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.ShiftFrameId);
                }
            }
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct ApplyAupShiftJob : IJobParallelFor
        {
            public NativeArray<Matrix4x4> Matrices;
            public float3 ShiftMeters;

            public void Execute(int index)
            {
                Matrix4x4 matrix = Matrices[index];
                float3 position = new float3(matrix.m03, matrix.m13, matrix.m23);
                position += ShiftMeters;
                if (!math.all(math.isfinite(position)))
                    position = float3.zero;
                matrix.m03 = position.x;
                matrix.m13 = position.y;
                matrix.m23 = position.z;
                Matrices[index] = matrix;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct InstanceCullingTelemetryEntry
        {
            public uint Frame;
            public int SourceInstances;
            public int VisibleInstances;
            public int CulledInstances;
            public uint Flags;
            public float CullDistanceMeters;
            public float VramUsedMb;
            public uint StateHash;
            public uint ShiftFrameId;
            public uint Padding0;
        }
    }
}
