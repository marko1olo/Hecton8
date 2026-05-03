using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {

        private static void EnsureChunkKeyCapacity(ref ChunkKey[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: ChunkKey[nextCapacity] - dynamic chunk key cache growth - owner: HectonMapMagicVegetationBridge
            ChunkKey[] expanded = new ChunkKey[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureFloatCapacity(ref float[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: float[nextCapacity] - dynamic float cache growth - owner: HectonMapMagicVegetationBridge
            float[] expanded = new float[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureMatrixCapacity(ref Matrix4x4[] matrixCache, int requiredCount)
        {
            if (matrixCache != null && matrixCache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Matrix4x4[nextCapacity] - streamed matrix cache growth - owner: HectonMapMagicVegetationBridge
            Matrix4x4[] expanded = new Matrix4x4[nextCapacity];
            if (matrixCache != null && matrixCache.Length > 0)
                Array.Copy(matrixCache, expanded, matrixCache.Length);

            matrixCache = expanded;
        }

        private static void EnsureVegetationDataCapacity(ref HectonVegetationInstanceData[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: HectonVegetationInstanceData[nextCapacity] - streamed metadata cache growth - owner: HectonMapMagicVegetationBridge
            HectonVegetationInstanceData[] expanded = new HectonVegetationInstanceData[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureIntCapacity(ref int[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: int[nextCapacity] - streamed vegetation type cache growth - owner: HectonMapMagicVegetationBridge
            int[] expanded = new int[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureByteCapacity(ref byte[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: byte[nextCapacity] - streamed biome-layer cache growth - owner: HectonMapMagicVegetationBridge
            byte[] expanded = new byte[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureVector2Capacity(ref Vector2[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Vector2[nextCapacity] - streamed flow-direction cache growth - owner: HectonMapMagicVegetationBridge
            Vector2[] expanded = new Vector2[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureVector3Capacity(ref Vector3[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Vector3[nextCapacity] - streamed 3D flow-vector cache growth - owner: HectonMapMagicVegetationBridge
            Vector3[] expanded = new Vector3[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureHLODDataCapacity(ref HLODData[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: HLODData[nextCapacity] - HLOD registry snapshot growth - owner: HectonMapMagicVegetationBridge
            HLODData[] expanded = new HLODData[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureBoolCapacity(ref bool[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: bool[nextCapacity] - selected chunk visibility cache growth - owner: HectonMapMagicVegetationBridge
            bool[] expanded = new bool[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureMatrixNativeCapacity(ref NativeArray<Matrix4x4> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVegetationDataNativeCapacity(ref NativeArray<HectonVegetationInstanceData> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureIntNativeCapacity(ref NativeArray<int> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureByteNativeCapacity(ref NativeArray<byte> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVector2NativeCapacity(ref NativeArray<Vector2> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVector3NativeCapacity(ref NativeArray<Vector3> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureDensityChunkRecordCapacity(ref NativeArray<VegetationDensityChunkRecord> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat3Capacity(ref NativeArray<float3> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloatNativeCapacity(ref NativeArray<float> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat2NativeCapacity(ref NativeArray<float2> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat4NativeCapacity(ref NativeArray<float4> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureNativeCapacity<T>(ref NativeArray<T> cache, int requiredCount)
            where T : struct
        {
            if (requiredCount <= 0)
                return;

            if (cache.IsCreated && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: NativeArray<T>[nextCapacity] - native snapshot/cache growth for streamed vegetation data - owner: HectonMapMagicVegetationBridge
            NativeArray<T> expanded = new NativeArray<T>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cache.IsCreated)
            {
                if (cache.Length > 0)
                    NativeArray<T>.Copy(cache, expanded, cache.Length);

                NativeMemorySentinel.UnregisterNativeArray(cache);
                cache.Dispose();
            }

            cache = expanded;
            RegisterTrackedNativeArray(cache, nameof(EnsureNativeCapacity));
        }

        private static void EnsureInactiveNativeCapacity<T>(ref NativeArray<T> cache, int requiredCount)
            where T : struct
        {
            if (requiredCount <= 0)
                return;

            if (cache.IsCreated && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: NativeArray<T>[nextCapacity] - inactive back-buffer growth for streamed vegetation data - owner: HectonMapMagicVegetationBridge
            NativeArray<T> expanded = new NativeArray<T>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cache.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(cache);
                cache.Dispose();
            }

            cache = expanded;
            RegisterTrackedNativeArray(cache, nameof(EnsureInactiveNativeCapacity));
        }

        private static void CopyNativeToManaged<T>(NativeArray<T> source, int sourceIndex, T[] destination, int destinationIndex, int copyCount)
            where T : struct
        {
            for (int i = 0; i < copyCount; i++)
                destination[destinationIndex + i] = source[sourceIndex + i];
        }

        private static MegaWreckStreamSection[] AllocateMegaWreckSectionPayloadArray(int count)
        {
            // COLD ALLOC: MegaWreckStreamSection[count] - per-chunk mega-wreck section cache finalized from streamed payloads - owner: HectonMapMagicVegetationBridge
            return new MegaWreckStreamSection[count];
        }

        private static void EnsureMegaWreckSectionCapacity(ref MegaWreckStreamSection[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: MegaWreckStreamSection[nextCapacity] - active mega-wreck stream snapshot growth - owner: HectonMapMagicVegetationBridge
            MegaWreckStreamSection[] expanded = new MegaWreckStreamSection[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }
    }
}
