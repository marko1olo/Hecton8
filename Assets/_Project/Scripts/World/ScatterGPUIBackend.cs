using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GraphicsBuffer-only indirect scatter submission backend. Draw payloads bind on the authored material before submission.
    /// </summary>
    internal sealed class ScatterGPUIBackend : IDisposable
    {
        private GraphicsBuffer _instanceBufferA;
        private GraphicsBuffer _instanceBufferB;
        private GraphicsBuffer _activeInstanceBuffer;
        private GraphicsBuffer _argsBuffer;
        private int _instanceCapacity;
        private int _instanceUploadBufferIndex;
        private Mesh _argsUploadMesh;
        private int _argsUploadInstanceCount = -1;

        public GraphicsBuffer InstanceBuffer => _activeInstanceBuffer;

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

            if (_instanceBufferA != null &&
                _instanceBufferB != null &&
                _instanceCapacity >= requiredCapacity)
            {
                if (_activeInstanceBuffer == null)
                    _activeInstanceBuffer = _instanceBufferA;
                return true;
            }

            ReleaseBuffer(ref _instanceBufferA);
            ReleaseBuffer(ref _instanceBufferB);
            _instanceCapacity = Mathf.NextPowerOfTwo(requiredCapacity);
            _instanceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>(_instanceCapacity); // COLD ALLOC: GraphicsBuffer[nextCapacity] A - scatter instance payload buffer - owner: ScatterGPUIBackend
            _instanceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>(_instanceCapacity); // COLD ALLOC: GraphicsBuffer[nextCapacity] B - scatter instance payload buffer - owner: ScatterGPUIBackend
            _activeInstanceBuffer = _instanceBufferA;
            _instanceUploadBufferIndex = 0;
            return _instanceBufferA != null && _instanceBufferB != null;
        }

        public bool Upload<T>(NativeArray<T> source, int count) where T : struct
        {
            if (!source.IsCreated || count <= 0)
                return false;

            GraphicsBuffer writeBuffer = _instanceUploadBufferIndex == 0 ? _instanceBufferA : _instanceBufferB;
            if (writeBuffer == null)
                return false;

            int safeCount = math.min(math.max(0, count), math.min(source.Length, writeBuffer.count));
            long uploadBytes = GraphicsBufferUploadUtility.EstimateUploadBytes<T>(safeCount);
            if (!GraphicsBufferUploadUtility.CanUploadBytesThisFrame(uploadBytes))
            {
                GraphicsBufferUploadUtility.RecordManualUploadDeferred();
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, source, safeCount);
            _activeInstanceBuffer = writeBuffer;
            _instanceUploadBufferIndex ^= 1;
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
                try
                {
                    argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = mesh.GetIndexCount(0),
                        instanceCount = (uint)instanceCount,
                        startIndex = mesh.GetIndexStart(0),
                        baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                        startInstance = 0u
                    };
                }
                finally
                {
                    _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
                }
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
            ReleaseBuffer(ref _instanceBufferA);
            ReleaseBuffer(ref _instanceBufferB);
            ReleaseBuffer(ref _argsBuffer);
            _activeInstanceBuffer = null;
            _instanceCapacity = 0;
            _instanceUploadBufferIndex = 0;
            _argsUploadMesh = null;
            _argsUploadInstanceCount = -1;
        }

        private void EnsureArgsBuffer()
        {
            if (_argsBuffer != null)
                return;

            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
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
