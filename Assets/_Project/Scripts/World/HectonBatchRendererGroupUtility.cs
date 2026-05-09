using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Shared BRG helpers for first-party world renderers.
    /// Keeps native culling-output allocation and plane tests out of individual owners.
    /// </summary>
    internal static class HectonBatchRendererGroupUtility
    {
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BuildMatrixVisibilityMaskJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly] public NativeArray<float4> CullingPlanes;
            public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public int PlaneCount;
            public bool EnableCpuCulling;
            public float3 GlobalOffset;
            public float RadiusScale;
            public float MinRadius;

            public void Execute(int index)
            {
                if (index >= InstanceCount)
                    return;

                if (!EnableCpuCulling)
                {
                    VisibilityMask[index] = 1;
                    return;
                }

                Matrix4x4 instanceMatrix = Matrices[index];
                float3 center = new float3(instanceMatrix.m03, instanceMatrix.m13, instanceMatrix.m23) + GlobalOffset;
                float3 axisX = new float3(instanceMatrix.m00, instanceMatrix.m10, instanceMatrix.m20);
                float3 axisY = new float3(instanceMatrix.m01, instanceMatrix.m11, instanceMatrix.m21);
                float3 axisZ = new float3(instanceMatrix.m02, instanceMatrix.m12, instanceMatrix.m22);
                float radius = math.max(
                    MinRadius,
                    math.max(
                        math.length(axisX),
                        math.max(math.length(axisY), math.length(axisZ))) * RadiusScale);

                for (int planeIndex = 0; planeIndex < PlaneCount; planeIndex++)
                {
                    float4 plane = CullingPlanes[planeIndex];
                    if (math.dot(plane.xyz, center) + plane.w < -radius)
                    {
                        VisibilityMask[index] = 0;
                        return;
                    }
                }

                VisibilityMask[index] = 1;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct FinalizeSingleDrawCommandOutputJob : IJob
        {
            [ReadOnly] public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public BatchID BatchId;
            public BatchMeshID MeshId;
            public BatchMaterialID MaterialId;
            public int Layer;
            public int SubMeshIndex;
            public ShadowCastingMode ShadowCastingMode;
            public bool ReceiveShadows;
            public MotionVectorGenerationMode MotionMode;
            [NativeDisableUnsafePtrRestriction] public int* VisibleInstances;
            [NativeDisableUnsafePtrRestriction] public BatchDrawCommand* DrawCommands;
            [NativeDisableUnsafePtrRestriction] public BatchDrawRange* DrawRanges;
            [NativeDisableUnsafePtrRestriction] public BatchCullingOutputDrawCommands* OutputCommands;

            public void Execute()
            {
                int visibleCount = 0;
                for (int instanceIndex = 0; instanceIndex < InstanceCount; instanceIndex++)
                {
                    if (VisibilityMask[instanceIndex] == 0)
                        continue;

                    VisibleInstances[visibleCount] = instanceIndex;
                    visibleCount++;
                }

                int drawCommandCount = visibleCount > 0 ? 1 : 0;
                if (drawCommandCount > 0)
                {
                    DrawCommands[0] = new BatchDrawCommand
                    {
                        flags = BatchDrawCommandFlags.None,
                        visibleOffset = 0u,
                        visibleCount = (uint)visibleCount,
                        batchID = BatchId,
                        materialID = MaterialId,
                        splitVisibilityMask = ushort.MaxValue,
                        lightmapIndex = ushort.MaxValue,
                        sortingPosition = 0,
                        meshID = MeshId,
                        submeshIndex = (ushort)math.max(0, SubMeshIndex)
                    };

                    DrawRanges[0] = new BatchDrawRange
                    {
                        drawCommandsBegin = 0u,
                        drawCommandsCount = 1u,
                        drawCommandsType = BatchDrawCommandType.Direct,
                        filterSettings = new BatchFilterSettings
                        {
                            renderingLayerMask = HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue,
                            rendererPriority = 0,
                            layer = (byte)math.clamp(Layer, byte.MinValue, byte.MaxValue),
                            shadowCastingMode = ShadowCastingMode,
                            receiveShadows = ReceiveShadows,
                            motionMode = MotionMode,
                            staticShadowCaster = false,
                            allDepthSorted = false
                        }
                    };
                }

                *OutputCommands = new BatchCullingOutputDrawCommands
                {
                    visibleInstances = VisibleInstances,
                    visibleInstanceCount = visibleCount,
                    drawCommands = DrawCommands,
                    drawCommandCount = drawCommandCount,
                    drawRanges = DrawRanges,
                    drawRangeCount = drawCommandCount
                };
            }
        }

        /// <summary>
        /// Allocates direct-draw BRG output storage for one callback.
        /// Unity owns the TempJob memory after the callback returns.
        /// </summary>
        public static unsafe BatchCullingOutputDrawCommands AllocateDirectDrawOutput(
            int visibleInstanceCount,
            int drawCommandCount,
            int drawRangeCount)
        {
            BatchCullingOutputDrawCommands output = default;
            output.visibleInstanceCount = visibleInstanceCount;
            output.drawCommandCount = drawCommandCount;
            output.drawRangeCount = drawRangeCount;

            if (visibleInstanceCount > 0)
            {
                output.visibleInstances = (int*)UnsafeUtility.Malloc(
                    sizeof(int) * visibleInstanceCount,
                    UnsafeUtility.AlignOf<int>(),
                    Allocator.TempJob);
            }

            if (drawCommandCount > 0)
            {
                output.drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(
                    UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommandCount,
                    UnsafeUtility.AlignOf<BatchDrawCommand>(),
                    Allocator.TempJob);
            }

            if (drawRangeCount > 0)
            {
                output.drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(
                    UnsafeUtility.SizeOf<BatchDrawRange>() * drawRangeCount,
                    UnsafeUtility.AlignOf<BatchDrawRange>(),
                    Allocator.TempJob);
            }

            return output;
        }

        /// <summary>
        /// Writes the direct-draw output into the BRG callback payload.
        /// </summary>
        public static void WriteDirectDrawOutput(BatchCullingOutput cullingOutput, BatchCullingOutputDrawCommands output)
        {
            cullingOutput.drawCommands[0] = output;
        }

        /// <summary>
        /// Writes a single direct draw where every bound instance is visible after a coarse bounds pass.
        /// </summary>
        public static unsafe void WriteAllVisibleSingleDrawOutput(
            BatchCullingOutput cullingOutput,
            int instanceCount,
            BatchID batchId,
            BatchMeshID meshId,
            BatchMaterialID materialId,
            int layer,
            int subMeshIndex,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            MotionVectorGenerationMode motionMode)
        {
            if (instanceCount <= 0)
            {
                WriteDirectDrawOutput(cullingOutput, AllocateDirectDrawOutput(0, 0, 0));
                return;
            }

            BatchCullingOutputDrawCommands output = AllocateDirectDrawOutput(instanceCount, 1, 1);
            for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
                output.visibleInstances[instanceIndex] = instanceIndex;

            output.drawCommands[0] = new BatchDrawCommand
            {
                flags = BatchDrawCommandFlags.None,
                visibleOffset = 0u,
                visibleCount = (uint)instanceCount,
                batchID = batchId,
                materialID = materialId,
                splitVisibilityMask = ushort.MaxValue,
                lightmapIndex = ushort.MaxValue,
                sortingPosition = 0,
                meshID = meshId,
                submeshIndex = (ushort)math.max(0, subMeshIndex)
            };

            output.drawRanges[0] = CreateDirectDrawRange(
                0u,
                layer,
                shadowCastingMode,
                receiveShadows,
                motionMode);

            WriteDirectDrawOutput(cullingOutput, output);
        }

        /// <summary>
        /// Returns a writable pointer for job-owned BRG output.
        /// </summary>
        public static unsafe BatchCullingOutputDrawCommands* GetDirectDrawOutputPointer(BatchCullingOutput cullingOutput)
        {
            return (BatchCullingOutputDrawCommands*)NativeArrayUnsafeUtility.GetUnsafePtr(cullingOutput.drawCommands);
        }

        /// <summary>
        /// Returns true when a sphere intersects the current culling planes.
        /// </summary>
        public static bool IsSphereVisible(NativeArray<Plane> cullingPlanes, Vector3 center, float radius)
        {
            int planeCount = cullingPlanes.IsCreated ? cullingPlanes.Length : 0;
            float centerX = center.x;
            float centerY = center.y;
            float centerZ = center.z;
            float negativeRadius = -radius;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Plane plane = cullingPlanes[planeIndex];
                Vector3 normal = plane.normal;
                float signedDistance =
                    (normal.x * centerX) +
                    (normal.y * centerY) +
                    (normal.z * centerZ) +
                    plane.distance;
                if (signedDistance < negativeRadius)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true when the conservative bounds sphere is visible to the current culling planes.
        /// </summary>
        public static bool IsBoundsVisible(NativeArray<Plane> cullingPlanes, Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            float maxAxis = math.cmax(math.abs(new float3(extents.x, extents.y, extents.z)));
            float radius = maxAxis * 1.7320508f;
            return IsSphereVisible(cullingPlanes, bounds.center, radius);
        }

        /// <summary>
        /// Creates one direct-draw range descriptor for the supplied filter settings.
        /// </summary>
        public static BatchDrawRange CreateDirectDrawRange(
            uint drawCommandIndex,
            int layer,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            MotionVectorGenerationMode motionMode)
        {
            return new BatchDrawRange
            {
                drawCommandsBegin = drawCommandIndex,
                drawCommandsCount = 1u,
                drawCommandsType = BatchDrawCommandType.Direct,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue,
                    rendererPriority = 0,
                    layer = (byte)Mathf.Clamp(layer, byte.MinValue, byte.MaxValue),
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = receiveShadows,
                    motionMode = motionMode,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };
        }

        /// <summary>
        /// Creates a tiny structured buffer suitable for BRG batch registration.
        /// </summary>
        public static GraphicsBuffer CreateBatchHandleBuffer()
        {
            return GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(1);
        }
    }
}
