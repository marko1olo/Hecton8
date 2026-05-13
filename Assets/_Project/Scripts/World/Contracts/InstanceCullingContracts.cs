using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Hardware-scaled mode for procedural GPU instance culling.
    /// </summary>
    public enum InstanceCullingQualityTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Bitmask toggles for one compute-culling dispatch.
    /// </summary>
    [Flags]
    public enum InstanceCullingDispatchFlags : uint
    {
        None = 0u,
        VoxelSdfCull = 1u << 0,
        VramDownsample = 1u << 1,
        LowTierDistance = 1u << 2,
        CameraStateFromSignals = 1u << 3
    }

    /// <summary>
    /// Camera position payload consumed by culling without polling Camera.main.
    /// </summary>
    public struct CameraPositionSignal
    {
        public Vector3 Position;
        public Vector3 Forward;
        public uint Frame;
        public byte Flags;
    }

    /// <summary>
    /// Packed frustum payload consumed by culling without camera-owned concrete dependencies.
    /// </summary>
    public struct CameraFrustumSignal
    {
        public Matrix4x4 ViewProjection;
        public Vector4 Plane0;
        public Vector4 Plane1;
        public Vector4 Plane2;
        public Vector4 Plane3;
        public Vector4 Plane4;
        public Vector4 Plane5;
        public uint Frame;
        public byte Flags;
    }

    /// <summary>
    /// Complete camera state for a culling dispatch. Planes use float4(normal.xyz, d).
    /// </summary>
    public struct InstanceCullingCameraState
    {
        public CameraPositionSignal Position;
        public CameraFrustumSignal Frustum;
    }

    /// <summary>
    /// Mesh and range metadata needed to rebuild indirect arguments without a CPU readback.
    /// </summary>
    public struct InstanceCullingIndirectArgs
    {
        public uint IndexCountPerInstance;
        public uint StartIndex;
        public uint BaseVertexIndex;
        public uint StartInstance;
    }

    /// <summary>
    /// Caller-owned source data for one procedural instance culling dispatch.
    /// </summary>
    public struct InstanceCullingDispatchDescriptor
    {
        public GraphicsBuffer AllInstancesBuffer;
        public int InstanceCount;
        public float BoundsRadius;
        public float MaxCullDistanceMeters;
        public float VramUsedMb;
        public InstanceCullingQualityTier QualityTier;
        public InstanceCullingDispatchFlags Flags;
        public InstanceCullingIndirectArgs IndirectArgs;
    }

    /// <summary>
    /// Delayed visibility telemetry read back from indirect args.
    /// </summary>
    public struct InstanceCullingTelemetry
    {
        public uint Frame;
        public int SourceInstances;
        public int VisibleInstances;
        public int CulledInstances;
        public uint Flags;
        public float CullDistanceMeters;
        public float VramUsedMb;
    }

    /// <summary>
    /// Service boundary for procedural/manual-BRG GPU instance culling.
    /// Implementations own append-visible buffers and indirect argument buffers.
    /// </summary>
    public interface IInstanceCullingService : IDisposable
    {
        /// <summary>True when compute kernels and persistent buffers are ready.</summary>
        bool IsAvailable { get; }

        /// <summary>Maximum instance count supported by the current buffer allocation.</summary>
        int Capacity { get; }

        /// <summary>Compute shader thread group size queried from the active kernel.</summary>
        int ThreadGroupSize { get; }

        /// <summary>Append buffer containing only visible matrices from the most recent dispatch.</summary>
        GraphicsBuffer VisibleInstancesBuffer { get; }

        /// <summary>Indirect argument buffer whose instance count is populated with GraphicsBuffer.CopyCount.</summary>
        GraphicsBuffer IndirectArgsBuffer { get; }

        /// <summary>Most recent delayed visible count. Rendering must not depend on same-frame availability.</summary>
        int LastVisibleInstanceCount { get; }

        /// <summary>Most recent delayed culled count.</summary>
        int LastCulledInstanceCount { get; }

        /// <summary>Initializes or resizes persistent buffers.</summary>
        void Configure(ComputeShader computeShader, int capacity);

        /// <summary>Consumes camera position signal payload.</summary>
        void ConsumeCameraPositionSignal(in CameraPositionSignal signal);

        /// <summary>Consumes camera frustum signal payload.</summary>
        void ConsumeCameraFrustumSignal(in CameraFrustumSignal signal);

        /// <summary>Sets the optional voxel SDF texture used as the cheap MX350 Hi-Z substitute.</summary>
        void SetVoxelSdf(Texture voxelSdfTexture, Vector3 origin, Vector3 size, bool enabled);

        /// <summary>Dispatches compute culling and writes indirect args through CopyCount.</summary>
        bool Dispatch(in InstanceCullingDispatchDescriptor descriptor);

        /// <summary>Applies a rare AUP rebase offset to a locked matrix buffer before culling.</summary>
        bool ApplyAupShift(GraphicsBuffer allInstancesBuffer, int instanceCount, Vector3 shiftMeters, uint shiftFrameId);

        /// <summary>Returns delayed telemetry packets without allocating.</summary>
        bool TryConsumeTelemetry(out InstanceCullingTelemetry telemetry);

        /// <summary>Releases all persistent GPU/native resources.</summary>
        void ReleaseResources();
    }
}
