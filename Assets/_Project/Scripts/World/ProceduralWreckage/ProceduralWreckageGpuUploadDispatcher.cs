using System;
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
            // COLD ALLOC: GraphicsBuffer[float4x4 wreckage matrices A] - double-buffered procedural wreck matrix upload - owner: ProceduralWreckageGpuUploadDispatcher
            _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_capacity);
            // COLD ALLOC: GraphicsBuffer[float4x4 wreckage matrices B] - double-buffered procedural wreck matrix upload - owner: ProceduralWreckageGpuUploadDispatcher
            _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_capacity);
            // COLD ALLOC: GraphicsBuffer[indirect args A] - DrawProceduralIndirect argument buffer - owner: ProceduralWreckageGpuUploadDispatcher
            _argsBufferA = CreateIndirectArgsBuffer();
            // COLD ALLOC: GraphicsBuffer[indirect args B] - DrawProceduralIndirect argument buffer - owner: ProceduralWreckageGpuUploadDispatcher
            _argsBufferB = CreateIndirectArgsBuffer();
            _writeIndex = 0;
            _activeIndex = -1;
            return _matrixBufferA != null && _matrixBufferB != null && _argsBufferA != null && _argsBufferB != null;
        }

        public unsafe bool UploadFromVault(
            NativeArray<float4x4> matrices,
            NativeArray<WreckageIndirectArgsDTO> indirectArgs,
            NativeArray<WreckageGpuScalarDTO> gpuScalars)
        {
            if (!matrices.IsCreated || !indirectArgs.IsCreated || indirectArgs.Length <= 0)
                return false;

            int requested = math.min((int)indirectArgs[0].InstanceCount, matrices.Length);
            if (!EnsureGraphicsResources(math.max(requested, 1)))
                return false;

            GraphicsBuffer matrixTarget = _writeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            GraphicsBuffer argsTarget = _writeIndex == 0 ? _argsBufferA : _argsBufferB;
            int writeCount = math.clamp(requested, 0, math.min(_capacity, matrixTarget.count));
            if (writeCount > 0)
            {
                NativeArray<float4x4> mappedMatrices = matrixTarget.LockBufferForWrite<float4x4>(0, writeCount);
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedMatrices);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(matrices);
                UnsafeUtility.MemCpy(dst, src, writeCount * UnsafeUtility.SizeOf<float4x4>());
                matrixTarget.UnlockBufferAfterWrite<float4x4>(writeCount);
            }

            WreckageIndirectArgsDTO args = indirectArgs[0];
            args.InstanceCount = (uint)writeCount;
            NativeArray<WreckageIndirectArgsDTO> mappedArgs = argsTarget.LockBufferForWrite<WreckageIndirectArgsDTO>(0, 1);
            mappedArgs[0] = args;
            argsTarget.UnlockBufferAfterWrite<WreckageIndirectArgsDTO>(1);

            _activeIndex = _writeIndex;
            _writeIndex ^= 1;
            Shader.SetGlobalBuffer(_WreckageMatricesId, matrixTarget);
            PublishScalars(gpuScalars);
            return true;
        }

        public bool TryDraw(Material material, Bounds bounds, MeshTopology topology = MeshTopology.Triangles)
        {
            if (material == null || !TryGetActiveBuffers(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer))
                return false;

            Shader.SetGlobalBuffer(_WreckageMatricesId, matrixBuffer);
            Graphics.DrawProceduralIndirect(
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

        private static void PublishScalars(NativeArray<WreckageGpuScalarDTO> gpuScalars)
        {
            if (!gpuScalars.IsCreated || gpuScalars.Length <= 0)
                return;

            WreckageGpuScalarDTO scalar = gpuScalars[0];
            Shader.SetGlobalVector(_WreckageScalar0Id, new Vector4(scalar.CausticRustSiltQuality.x, scalar.CausticRustSiltQuality.y, scalar.CausticRustSiltQuality.z, scalar.CausticRustSiltQuality.w));
            Shader.SetGlobalVector(_WreckageScalar1Id, new Vector4(scalar.BoundsAndDensity.x, scalar.BoundsAndDensity.y, scalar.BoundsAndDensity.z, scalar.BoundsAndDensity.w));
            Shader.SetGlobalVector(_WreckageScalar2Id, new Vector4(scalar.FaultAndFrame.x, scalar.FaultAndFrame.y, scalar.FaultAndFrame.z, scalar.FaultAndFrame.w));
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
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<WreckageIndirectArgsDTO>());
        }

        private static bool IsValid(GraphicsBuffer buffer, int count, int stride)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= count && buffer.stride == stride;
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
    }
}
