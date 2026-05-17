using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GraphicsBuffer-only indirect scatter submission backend. No MaterialPropertyBlock is used.
    /// </summary>
    internal sealed class ScatterGPUIBackend : IDisposable
    {
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _argsBuffer;
        private int _instanceCapacity;
        private Mesh _argsUploadMesh;
        private int _argsUploadInstanceCount = -1;

        public GraphicsBuffer InstanceBuffer => _instanceBuffer;

        public GraphicsBuffer ArgsBuffer => _argsBuffer;

        public static Matrix4x4 BuildOriginRelativeMatrix(Vector3 absolutePosition, Quaternion rotation, float scale)
        {
            AbsoluteUniversePosition target = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                absolutePosition.x,
                absolutePosition.y,
                absolutePosition.z));
            AbsoluteUniversePosition origin = AbsoluteUniversePosition.FromAbsolutePosition(HectonFloatingOrigin.CurrentTotalOffsetDouble);
            float3 originRelative = AUPMath.ResolveCameraRelative(in target, in origin);
            return Matrix4x4.TRS(
                new Vector3(originRelative.x, originRelative.y, originRelative.z),
                rotation,
                Vector3.one * scale);
        }

        public bool EnsureInstanceBuffer<T>(int requiredCapacity) where T : struct
        {
            if (requiredCapacity <= 0)
                return false;

            if (_instanceBuffer != null && _instanceCapacity >= requiredCapacity)
                return true;

            ReleaseBuffer(ref _instanceBuffer);
            _instanceCapacity = Mathf.NextPowerOfTwo(requiredCapacity);
            _instanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>(_instanceCapacity); // COLD ALLOC: GraphicsBuffer[nextCapacity] - scatter instance payload buffer - owner: ScatterGPUIBackend
            return _instanceBuffer != null;
        }

        public bool Upload<T>(NativeArray<T> source, int count) where T : struct
        {
            if (_instanceBuffer == null || !source.IsCreated || count <= 0)
                return false;

            GraphicsBufferUploadUtility.UploadNativeArray(_instanceBuffer, source, count);
            return true;
        }

        public bool BindInstanceBuffer(Material material, int propertyId)
        {
            if (material == null || _instanceBuffer == null)
                return false;

            material.SetBuffer(propertyId, _instanceBuffer);
            return true;
        }

        public bool SubmitIndirect(Mesh mesh, Material material, Bounds bounds, int instanceCount, int layer)
        {
            if (mesh == null || material == null || instanceCount <= 0)
                return false;

            EnsureArgsBuffer();
            if (_argsBuffer == null)
                return false;

            if (_argsUploadMesh != mesh || _argsUploadInstanceCount != instanceCount)
            {
                NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                    _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(0),
                    instanceCount = (uint)instanceCount,
                    startIndex = mesh.GetIndexStart(0),
                    baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                    startInstance = 0u
                };
                _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
                _argsUploadMesh = mesh;
                _argsUploadInstanceCount = instanceCount;
            }

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = bounds,
                layer = layer,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _argsBuffer, 1, 0);
            return true;
        }

        public void Dispose()
        {
            ReleaseBuffer(ref _instanceBuffer);
            ReleaseBuffer(ref _argsBuffer);
            _instanceCapacity = 0;
            _argsUploadMesh = null;
            _argsUploadInstanceCount = -1;
        }

        private void EnsureArgsBuffer()
        {
            if (_argsBuffer != null)
                return;

            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - scatter indirect draw args - owner: ScatterGPUIBackend
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
