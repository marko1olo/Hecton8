using System;
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

        public bool EnsureGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            if (_capacity >= capacity &&
                IsValid(_matrixBufferA, _capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                IsValid(_matrixBufferB, _capacity, UnsafeUtility.SizeOf<float4x4>()) &&
                IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<CoralIndirectArgsDTO>()) &&
                IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<CoralIndirectArgsDTO>()))
            {
                return true;
            }

            ReleaseGraphicsResources();
            _capacity = NextPowerOfTwo(capacity);
            // COLD ALLOC: double-buffered coral instance matrix upload; no GameObject hierarchy.
            _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_capacity);
            // COLD ALLOC: double-buffered coral instance matrix upload; no GameObject hierarchy.
            _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_capacity);
            // COLD ALLOC: DrawProceduralIndirect argument buffer.
            _argsBufferA = CreateIndirectArgsBuffer();
            // COLD ALLOC: DrawProceduralIndirect argument buffer.
            _argsBufferB = CreateIndirectArgsBuffer();
            _writeIndex = 0;
            _activeIndex = -1;
            return _matrixBufferA != null && _matrixBufferB != null && _argsBufferA != null && _argsBufferB != null;
        }

        public unsafe bool UploadFromVault(
            NativeArray<float4x4> matrices,
            NativeArray<CoralIndirectArgsDTO> indirectArgs,
            NativeArray<CoralGpuSwayDTO> gpuSway)
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

            CoralIndirectArgsDTO args = indirectArgs[0];
            args.InstanceCount = (uint)writeCount;
            NativeArray<CoralIndirectArgsDTO> mappedArgs = argsTarget.LockBufferForWrite<CoralIndirectArgsDTO>(0, 1);
            mappedArgs[0] = args;
            argsTarget.UnlockBufferAfterWrite<CoralIndirectArgsDTO>(1);

            _activeIndex = _writeIndex;
            _writeIndex ^= 1;
            Shader.SetGlobalBuffer(_CoralMatricesId, matrixTarget);
            PublishSway(gpuSway);
            return true;
        }

        public bool TryDraw(Material material, Bounds bounds, MeshTopology topology = MeshTopology.Triangles)
        {
            if (material == null || !TryGetActiveBuffers(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer))
                return false;

            Shader.SetGlobalBuffer(_CoralMatricesId, matrixBuffer);
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

        private static void PublishSway(NativeArray<CoralGpuSwayDTO> gpuSway)
        {
            if (!gpuSway.IsCreated || gpuSway.Length <= 0)
                return;

            CoralGpuSwayDTO sway = gpuSway[0];
            Shader.SetGlobalVector(_CoralSway0Id, new Vector4(sway.FlowAndAmplitude.x, sway.FlowAndAmplitude.y, sway.FlowAndAmplitude.z, sway.FlowAndAmplitude.w));
            Shader.SetGlobalVector(_CoralSway1Id, new Vector4(sway.BoundsAndDensity.x, sway.BoundsAndDensity.y, sway.BoundsAndDensity.z, sway.BoundsAndDensity.w));
            Shader.SetGlobalVector(_CoralSway2Id, new Vector4(sway.FaultAndFrame.x, sway.FaultAndFrame.y, sway.FaultAndFrame.z, sway.FaultAndFrame.w));
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
                UnsafeUtility.SizeOf<CoralIndirectArgsDTO>());
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
