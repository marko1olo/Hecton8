using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.ProceduralCoral
{
    public sealed class ProceduralCoralGpuUploadDispatcher : IDisposable
    {
        private static readonly int _CoralMatricesId = Shader.PropertyToID("_H8CoralMatrices");
        private static readonly int _CoralSway0Id = Shader.PropertyToID("_H8CoralSway0");
        private static readonly int _CoralSway1Id = Shader.PropertyToID("_H8CoralSway1");
        private static readonly int _CoralSway2Id = Shader.PropertyToID("_H8CoralSway2");

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private int _capacity;
        private int _writeIndex;
        private int _activeIndex = -1;
        private int _activeInstanceCount;
        private CoralGpuSwayDTO _activeSway;
        private bool _hasActiveSway;

        public bool EnsureGraphicsResources(int requiredCapacity)
        {
            int capacity = math.clamp(requiredCapacity, 1, ProceduralCoralConstants.MaxRenderMatrices);
            if (HasGraphicsResources(capacity))
                return true;

            ReleaseGraphicsResources();
            _capacity = NextPowerOfTwo(capacity);
            try
            {
                // COLD ALLOC: double-buffered coral instance matrix upload; no GameObject hierarchy.
                _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_capacity);
                // COLD ALLOC: double-buffered coral instance matrix upload; no GameObject hierarchy.
                _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_capacity);
                // COLD ALLOC: DrawProceduralIndirect argument buffer.
                _argsBufferA = CreateIndirectArgsBuffer();
                // COLD ALLOC: DrawProceduralIndirect argument buffer.
                _argsBufferB = CreateIndirectArgsBuffer();
            }
            catch (Exception)
            {
                ReleaseGraphicsResources();
                return false;
            }

            _writeIndex = 0;
            _activeIndex = -1;
            _activeInstanceCount = 0;
            return _matrixBufferA != null && _matrixBufferB != null && _argsBufferA != null && _argsBufferB != null;
        }

        public unsafe bool UploadFromVault(
            NativeArray<float4x4> matrices,
            NativeArray<CoralIndirectArgsDTO> indirectArgs,
            NativeArray<CoralGpuSwayDTO> gpuSway,
            bool allowAllocation = false)
        {
            if (!matrices.IsCreated || !indirectArgs.IsCreated || indirectArgs.Length <= 0)
                return false;

            int requested = ResolveRequestedInstanceCount(indirectArgs[0].InstanceCount, matrices.Length);
            int requiredCapacity = math.max(requested, 1);
            if (!HasGraphicsResources(requiredCapacity))
            {
                if (!allowAllocation || !EnsureGraphicsResources(requiredCapacity))
                    return false;
            }

            GraphicsBuffer matrixTarget = _writeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            GraphicsBuffer argsTarget = _writeIndex == 0 ? _argsBufferA : _argsBufferB;
            int writeCount = math.clamp(requested, 0, math.min(_capacity, matrixTarget.count));
            long uploadBytes =
                GraphicsBufferUploadUtility.EstimateUploadBytes<float4x4>(writeCount) +
                GraphicsBufferUploadUtility.EstimateUploadBytes<CoralIndirectArgsDTO>(1);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return false;

            bool uploadCompleted = false;
            try
            {
                if (writeCount > 0)
                {
                    bool matrixLocked = false;
                    try
                    {
                        NativeArray<float4x4> mappedMatrices = matrixTarget.LockBufferForWrite<float4x4>(0, writeCount);
                        matrixLocked = true;
                        void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedMatrices);
                        void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(matrices);
                        UnsafeUtility.MemCpy(dst, src, writeCount * UnsafeUtility.SizeOf<float4x4>());
                    }
                    finally
                    {
                        if (matrixLocked)
                            matrixTarget.UnlockBufferAfterWrite<float4x4>(writeCount);
                    }
                }

                CoralIndirectArgsDTO args = indirectArgs[0];
                args.InstanceCount = (uint)writeCount;
                args.VertexCountPerInstance = math.max(1u, args.VertexCountPerInstance);
                bool argsLocked = false;
                try
                {
                    NativeArray<CoralIndirectArgsDTO> mappedArgs =
                        argsTarget.LockBufferForWrite<CoralIndirectArgsDTO>(0, 1);
                    argsLocked = true;
                    mappedArgs[0] = args;
                }
                finally
                {
                    if (argsLocked)
                        argsTarget.UnlockBufferAfterWrite<CoralIndirectArgsDTO>(1);
                }

                uploadCompleted = true;
            }
            finally
            {
                if (uploadCompleted)
                    GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                else
                    GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
            }

            _activeIndex = _writeIndex;
            _activeInstanceCount = writeCount;
            _writeIndex ^= 1;
            CaptureSway(gpuSway);
            return true;
        }

        public bool TryDraw(Material material, Bounds bounds, MeshTopology topology = MeshTopology.Triangles)
        {
            if (material == null ||
                _activeInstanceCount <= 0 ||
                !TryGetActiveBuffers(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer))
            {
                return false;
            }

            material.SetBuffer(_CoralMatricesId, matrixBuffer);
            ApplySway(material);
            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                bounds,
                topology,
                argsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.On,
                true,
                0);
            return true;
        }

        public bool TryGetActiveBuffers(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer)
        {
            matrixBuffer = null;
            argsBuffer = null;
            if (_activeIndex < 0)
                return false;

            matrixBuffer = _activeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            argsBuffer = _activeIndex == 0 ? _argsBufferA : _argsBufferB;
            return matrixBuffer != null && argsBuffer != null && matrixBuffer.IsValid() && argsBuffer.IsValid();
        }

        public void Dispose()
        {
            ReleaseGraphicsResources();
        }

        private void CaptureSway(NativeArray<CoralGpuSwayDTO> gpuSway)
        {
            if (!gpuSway.IsCreated || gpuSway.Length <= 0)
                return;

            _activeSway = gpuSway[0];
            _hasActiveSway = true;
        }

        private void ApplySway(Material material)
        {
            if (material == null)
                return;

            CoralGpuSwayDTO sway = _hasActiveSway ? _activeSway : default;
            material.SetVector(
                _CoralSway0Id,
                ToFiniteVector4(sway.FlowAndAmplitude, new float4(0.04f, 0f, 1f, 0f)));
            material.SetVector(
                _CoralSway1Id,
                ToFiniteVector4(sway.BoundsAndDensity, new float4(0f, 0f, 0f, 1f)));
            material.SetVector(_CoralSway2Id, ToFiniteVector4(sway.FaultAndFrame, float4.zero));
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseBuffer(ref _matrixBufferA);
            ReleaseBuffer(ref _matrixBufferB);
            ReleaseBuffer(ref _argsBufferA);
            ReleaseBuffer(ref _argsBufferB);
            _capacity = 0;
            _writeIndex = 0;
            _activeIndex = -1;
            _activeInstanceCount = 0;
            _activeSway = default;
            _hasActiveSway = false;
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>());
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<CoralIndirectArgsDTO>());
        }

        private static bool IsValid(GraphicsBuffer buffer, int count, int stride)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= count && buffer.stride == stride;
        }

        private static int ResolveRequestedInstanceCount(uint rawInstanceCount, int matrixCapacity)
        {
            int safeCapacity = math.max(0, matrixCapacity);
            return rawInstanceCount > (uint)safeCapacity ? safeCapacity : (int)rawInstanceCount;
        }

        private bool HasGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            return _capacity >= capacity &&
                   IsValid(_matrixBufferA, capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                   IsValid(_matrixBufferB, capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                   IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<CoralIndirectArgsDTO>()) &&
                   IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<CoralIndirectArgsDTO>());
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Dispose();
            buffer = null;
        }

        private static int NextPowerOfTwo(int value)
        {
            int v = math.max(1, value);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        private static Vector4 ToFiniteVector4(float4 value, float4 fallback)
        {
            float4 safe = math.all(math.isfinite(value)) ? value : fallback;
            return new Vector4(safe.x, safe.y, safe.z, safe.w);
        }
    }
}
