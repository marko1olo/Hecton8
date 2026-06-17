#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World
{
    internal static class PcieBandwidthGuard1411SelfTest
    {
        private const int PageSize = 256;
        private const int MatrixInstanceCount = 100000;
        private const int LastMatrixIndex = MatrixInstanceCount - 1;
        private const int FuzzerInstanceCount = 8192;
        private const int FuzzerFrames = 512;

        [MenuItem("Hecton8/Agent 1411/Run PCIe Bandwidth Guard Self Test")]
        private static void RunMenu()
        {
            RunAll();
        }

        public static void RunAll()
        {
            AssertSingleDirtyPageForLastMatrix();
            AssertDirtyPageBudgetDefersWithoutPublishingHalfState();
            AssertCombinedMatrixAndMetadataBudgetDoesNotOvershoot();
            AssertDoubleBufferBacklogFuzzer();
            Debug.Log("Agent 1411 PCIe bandwidth guard self test passed.");
        }

        private static void AssertSingleDirtyPageForLastMatrix()
        {
            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(MatrixInstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> dirtyPages = new NativeArray<byte>(
                GraphicsBufferUploadUtility.ResolveDirtyPageCount(MatrixInstanceCount, PageSize),
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            try
            {
                matrices[LastMatrixIndex] = Matrix4x4.Translate(new Vector3(1f, 2f, 3f));
                GraphicsBufferUploadUtility.MarkDirtyPage(dirtyPages, LastMatrixIndex, MatrixInstanceCount, PageSize);
                GraphicsBufferUploadUtility.PageUploadStats stats =
                    GraphicsBufferUploadUtility.CalculateDirtyPageUploadStats<Matrix4x4>(
                        dirtyPages,
                        matrices.Length,
                        matrices.Length,
                        MatrixInstanceCount,
                        PageSize,
                        int.MaxValue);

                int dirtyPage = LastMatrixIndex / PageSize;
                int dirtyStart = dirtyPage * PageSize;
                int dirtyElementCount = math.min(PageSize, MatrixInstanceCount - dirtyStart);
                long expectedBytes = (long)dirtyElementCount * UnsafeUtility.SizeOf<Matrix4x4>();
                Require(stats.UploadedPages == 1, "Dirty page isolation failed: uploaded page count != 1.");
                Require(stats.LockSpans == 1, "Dirty page isolation failed: lock span count != 1.");
                Require(stats.UploadedBytes == expectedBytes, "Dirty page isolation failed: bytes did not match the single dirty page.");
                Require(stats.UploadedBytes < (long)MatrixInstanceCount * UnsafeUtility.SizeOf<Matrix4x4>(), "Dirty page isolation failed: full array transfer was not eliminated.");
            }
            finally
            {
                if (dirtyPages.IsCreated)
                    dirtyPages.Dispose();
                if (matrices.IsCreated)
                    matrices.Dispose();
            }
        }

        private static void AssertDirtyPageBudgetDefersWithoutPublishingHalfState()
        {
            NativeArray<byte> dirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                dirtyPages[0] = 1;
                dirtyPages[1] = 1;
                int pageBytes = PageSize * UnsafeUtility.SizeOf<Matrix4x4>();
                GraphicsBufferUploadUtility.PageUploadStats stats =
                    GraphicsBufferUploadUtility.CalculateDirtyPageUploadStats<Matrix4x4>(
                        dirtyPages,
                        PageSize * 4,
                        PageSize * 4,
                        PageSize * 4,
                        PageSize,
                        pageBytes);

                Require(stats.UploadedPages == 1, "Budget gate failed: expected exactly one uploaded page.");
                Require(stats.DeferredPages == 1, "Budget gate failed: expected exactly one deferred page.");
                Require(stats.FirstDeferredPage == 1, "Budget gate failed: wrong deferred page index.");
            }
            finally
            {
                if (dirtyPages.IsCreated)
                    dirtyPages.Dispose();
            }
        }

        private static void AssertCombinedMatrixAndMetadataBudgetDoesNotOvershoot()
        {
            NativeArray<byte> matrixDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> metadataDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                matrixDirtyPages[0] = 1;
                matrixDirtyPages[1] = 1;
                metadataDirtyPages[0] = 1;

                int instanceCount = PageSize * 4;
                int budgetBytes = PageSize * UnsafeUtility.SizeOf<Matrix4x4>() * 2;
                GraphicsBufferUploadUtility.PageUploadStats matrixStats =
                    GraphicsBufferUploadUtility.CalculateDirtyPageUploadStats<Matrix4x4>(
                        matrixDirtyPages,
                        instanceCount,
                        instanceCount,
                        instanceCount,
                        PageSize,
                        budgetBytes);

                long remainingBudgetBytes = (long)budgetBytes - matrixStats.UploadedBytes;
                int metadataFirstPageBytes =
                    GraphicsBufferUploadUtility.ResolveFirstDirtyPageBytes<HectonVegetationInstanceData>(
                        metadataDirtyPages,
                        instanceCount,
                        PageSize);
                bool canUploadMetadataThisFrame =
                    metadataFirstPageBytes > 0 &&
                    (remainingBudgetBytes >= metadataFirstPageBytes || matrixStats.UploadedBytes <= 0L);

                Require(matrixStats.UploadedBytes == budgetBytes, "Combined budget gate failed: matrix lane did not consume the expected budget.");
                Require(!canUploadMetadataThisFrame, "Combined budget gate failed: metadata lane would overshoot the visual-sync byte budget.");
            }
            finally
            {
                if (metadataDirtyPages.IsCreated)
                    metadataDirtyPages.Dispose();
                if (matrixDirtyPages.IsCreated)
                    matrixDirtyPages.Dispose();
            }
        }

        private static void AssertDoubleBufferBacklogFuzzer()
        {
            NativeArray<int> source = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> bufferA = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> bufferB = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            int pageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(FuzzerInstanceCount, PageSize);
            NativeArray<byte> dirtyA = new NativeArray<byte>(pageCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> dirtyB = new NativeArray<byte>(pageCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                int activeIndex = 0;
                int writeIndex = 1;
                uint rng = 0x8E1411u;
                for (int frame = 0; frame < FuzzerFrames; frame++)
                {
                    int mutationCount = 1 + (int)(NextRandom(ref rng) & 7u);
                    for (int mutation = 0; mutation < mutationCount; mutation++)
                    {
                        int index = (int)(NextRandom(ref rng) % FuzzerInstanceCount);
                        int value = (int)(NextRandom(ref rng) & 0x7FFFFFFFu);
                        source[index] = value;
                        GraphicsBufferUploadUtility.MarkDirtyPage(dirtyA, index, FuzzerInstanceCount, PageSize);
                        GraphicsBufferUploadUtility.MarkDirtyPage(dirtyB, index, FuzzerInstanceCount, PageSize);
                    }

                    Require(writeIndex != activeIndex, "Ping-pong violation: attempted to write active front buffer.");
                    NativeArray<int> writeBuffer = writeIndex == 0 ? bufferA : bufferB;
                    NativeArray<byte> writeDirty = writeIndex == 0 ? dirtyA : dirtyB;
                    CopyDirtyIntPages(source, writeBuffer, writeDirty, FuzzerInstanceCount, PageSize);

                    activeIndex = writeIndex;
                    writeIndex ^= 1;
                    NativeArray<int> activeBuffer = activeIndex == 0 ? bufferA : bufferB;
                    AssertBuffersEqual(source, activeBuffer, FuzzerInstanceCount);
                }
            }
            finally
            {
                if (dirtyB.IsCreated)
                    dirtyB.Dispose();
                if (dirtyA.IsCreated)
                    dirtyA.Dispose();
                if (bufferB.IsCreated)
                    bufferB.Dispose();
                if (bufferA.IsCreated)
                    bufferA.Dispose();
                if (source.IsCreated)
                    source.Dispose();
            }
        }

        private static void CopyDirtyIntPages(NativeArray<int> source, NativeArray<int> target, NativeArray<byte> dirtyPages, int count, int pageSize)
        {
            int pageCount = math.min(dirtyPages.Length, GraphicsBufferUploadUtility.ResolveDirtyPageCount(count, pageSize));
            for (int page = 0; page < pageCount; page++)
            {
                if (dirtyPages[page] == 0)
                    continue;

                int start = page * pageSize;
                int end = math.min(count, start + pageSize);
                for (int i = start; i < end; i++)
                    target[i] = source[i];
                dirtyPages[page] = 0;
            }
        }

        private static void AssertBuffersEqual(NativeArray<int> expected, NativeArray<int> actual, int count)
        {
            for (int i = 0; i < count; i++)
                Require(expected[i] == actual[i], "Ping-pong backlog failed: active buffer diverged from source.");
        }

        private static uint NextRandom(ref uint state)
        {
            state = (state * 1664525u) + 1013904223u;
            return state;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
#endif
