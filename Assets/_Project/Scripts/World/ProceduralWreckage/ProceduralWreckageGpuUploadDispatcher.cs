using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.ProceduralWreckage
{
    public sealed class ProceduralWreckageGpuUploadDispatcher : IDisposable
    {
        private static readonly int _WreckageMatricesId = Shader.PropertyToID("_H8WreckageMatrices");
        private static readonly int _WreckageScalar0Id = Shader.PropertyToID("_H8WreckageScalar0");
        private static readonly int _WreckageScalar1Id = Shader.PropertyToID("_H8WreckageScalar1");
        private static readonly int _WreckageScalar2Id = Shader.PropertyToID("_H8WreckageScalar2");

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private int _capacity;
        private int _writeIndex;
        private int _activeIndex = -1;
        private int _activeInstanceCount;
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        private WreckageGpuScalarDTO _activeScalar;
        private bool _hasActiveScalar;

        public bool EnsureGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            if (_capacity >= capacity &&
                IsValid(_matrixBufferA, _capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                IsValid(_matrixBufferB, _capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<WreckageIndirectArgsDTO>()) &&
                IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<WreckageIndirectArgsDTO>()))
            {
                return true;
            }

            ReleaseGraphicsResources();
            _capacity = NextPowerOfTwo(capacity);
            try
            {
                // COLD ALLOC: GraphicsBuffer[float4x4 wreckage matrices A] - double-buffered procedural wreck matrix upload - owner: ProceduralWreckageGpuUploadDispatcher
                _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_capacity);
                // COLD ALLOC: GraphicsBuffer[float4x4 wreckage matrices B] - double-buffered procedural wreck matrix upload - owner: ProceduralWreckageGpuUploadDispatcher
                _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_capacity);
                // COLD ALLOC: GraphicsBuffer[indirect args A] - DrawProceduralIndirect argument buffer - owner: ProceduralWreckageGpuUploadDispatcher
                _argsBufferA = CreateIndirectArgsBuffer();
                // COLD ALLOC: GraphicsBuffer[indirect args B] - DrawProceduralIndirect argument buffer - owner: ProceduralWreckageGpuUploadDispatcher
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
            NativeArray<WreckageIndirectArgsDTO> indirectArgs,
            NativeArray<WreckageGpuScalarDTO> gpuScalars)
        {
            if (!matrices.IsCreated || !indirectArgs.IsCreated || indirectArgs.Length <= 0)
                return false;

            int requested = ResolveRequestedInstanceCount(indirectArgs[0].InstanceCount, matrices.Length);
            if (!EnsureGraphicsResources(math.max(requested, 1)))
                return false;

            GraphicsBuffer matrixTarget = _writeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            GraphicsBuffer argsTarget = _writeIndex == 0 ? _argsBufferA : _argsBufferB;
            int writeCount = math.clamp(requested, 0, math.min(_capacity, matrixTarget.count));
            long uploadBytes =
                GraphicsBufferUploadUtility.EstimateUploadBytes<float4x4>(writeCount) +
                GraphicsBufferUploadUtility.EstimateUploadBytes<WreckageIndirectArgsDTO>(1);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return false;

            bool uploadCompleted = false;
            try
            {
                if (writeCount > 0)
                {
                    NativeArray<float4x4> mappedMatrices = matrixTarget.LockBufferForWrite<float4x4>(0, writeCount);
                    try
                    {
                        void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedMatrices);
                        void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(matrices);
                        UnsafeUtility.MemCpy(dst, src, writeCount * UnsafeUtility.SizeOf<float4x4>());
                    }
                    finally
                    {
                        matrixTarget.UnlockBufferAfterWrite<float4x4>(writeCount);
                    }
                }

                WreckageIndirectArgsDTO args = indirectArgs[0];
                args.InstanceCount = (uint)writeCount;
                NativeArray<WreckageIndirectArgsDTO> mappedArgs = argsTarget.LockBufferForWrite<WreckageIndirectArgsDTO>(0, 1);
                try
                {
                    mappedArgs[0] = args;
                }
                finally
                {
                    argsTarget.UnlockBufferAfterWrite<WreckageIndirectArgsDTO>(1);
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
            CaptureScalars(gpuScalars);
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

            _propertyBlock.Clear();
            _propertyBlock.SetBuffer(_WreckageMatricesId, matrixBuffer);
            ApplyScalars(_propertyBlock);
            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                bounds,
                topology,
                argsBuffer,
                0,
                null,
                _propertyBlock,
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

        private void CaptureScalars(NativeArray<WreckageGpuScalarDTO> gpuScalars)
        {
            if (!gpuScalars.IsCreated || gpuScalars.Length <= 0)
                return;

            _activeScalar = gpuScalars[0];
            _hasActiveScalar = true;
        }

        private void ApplyScalars(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
                return;

            WreckageGpuScalarDTO scalar = _hasActiveScalar ? _activeScalar : default;
            propertyBlock.SetVector(
                _WreckageScalar0Id,
                ToFiniteVector4(scalar.CausticRustSiltQuality, new float4(0.08f, 0.35f, 0.25f, 0.5f)));
            propertyBlock.SetVector(
                _WreckageScalar1Id,
                ToFiniteVector4(scalar.BoundsAndDensity, new float4(0f, 0f, 0f, 1f)));
            propertyBlock.SetVector(_WreckageScalar2Id, ToFiniteVector4(scalar.FaultAndFrame, float4.zero));
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
            _activeScalar = default;
            _hasActiveScalar = false;
            _propertyBlock.Clear();
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
                UnsafeUtility.SizeOf<WreckageIndirectArgsDTO>());
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
