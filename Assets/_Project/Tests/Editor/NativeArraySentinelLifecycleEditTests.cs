using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class NativeArraySentinelLifecycleEditTests
    {
        [Test]
        public void SaveManager_HandlesNativeArraySentinelOrderByDisposalMode()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.Contains("array.Dispose();", helper);
            StringAssert.Contains("JobHandle disposeHandle = array.Dispose(dependency);", helper);
            StringAssert.Contains("if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("sentinelUnregistered", helper);
            StringAssert.DoesNotContain("RestoreNativeSentinelRecordOrThrow", source);
            StringAssert.DoesNotContain("NativeMemoryRestoreFailureMessage", source);
        }

        [Test]
        public void SaveManager_OwnedStaticWriteBuffersUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void ReleaseOwnedBuffer", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);", helper);
            StringAssert.Contains("buffer.Dispose();", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "buffer.Dispose();");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void CrestDepthReadbackUnregistersSentinelBeforeSyncNativePixelsDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static void DisposeRegisteredReadbackPixels", StringComparison.Ordinal));

            StringAssert.Contains("System.Threading.Interlocked.CompareExchange(ref readbackDisposalState.Disposed, 1, 0)", helper);
            StringAssert.DoesNotContain("bool disposed = !", helper);
            StringAssert.Contains("readbackPixels.Dispose();", helper);
            StringAssert.Contains("NativeMemorySentinel.Unregister(readbackDisposalState.SentinelId);", helper);
            StringAssert.Contains("readbackDisposalState.SentinelId = 0;", helper);
            StringAssert.Contains("System.Threading.Volatile.Write(ref readbackDisposalState.Disposed, 2);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.Unregister(readbackDisposalState.SentinelId);", "readbackPixels.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.Unregister(readbackDisposalState.SentinelId);", "readbackDisposalState.SentinelId = 0;");
            AssertTextBefore(helper, "readbackDisposalState.SentinelId = 0;", "System.Threading.Volatile.Write(ref readbackDisposalState.Disposed, 2);");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray(readbackPixels)", helper);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(", helper);
            StringAssert.DoesNotContain("Native memory sentinel restore failed", source);
        }

        [Test]
        public void SaveThumbnailEncodedJpgDisposesBeforeSentinelIdUnregister()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveThumbnailSystem.cs");
            string method = ExtractMethodAt(source, source.IndexOf("private static async Awaitable PersistThumbnailAsync", StringComparison.Ordinal));

            StringAssert.Contains("int encodedJpgSentinelId = 0;", method);
            StringAssert.Contains("encodedJpgSentinelId = NativeMemorySentinel.RegisterNativeArray(", method);
            StringAssert.Contains("encodedJpg.Dispose();", method);
            StringAssert.Contains("NativeMemorySentinel.Unregister(encodedJpgSentinelId);", method);
            StringAssert.Contains("encodedJpgSentinelId = 0;", method);
            AssertTextBefore(method, "NativeMemorySentinel.Unregister(encodedJpgSentinelId);", "encodedJpg.Dispose();");
            AssertTextAfter(method, "NativeMemorySentinel.Unregister(encodedJpgSentinelId);", "encodedJpgSentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", method);
            StringAssert.DoesNotContain("encodedJpgRegistered", method);
        }

        [Test]
        public void VoxelDeltaProcessorTrackedArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/VoxelDeltaProcessor.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeTrackedNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.Contains("array.Dispose();", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void PlayerInventoryVaultArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.Contains("array.Dispose();", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void WorldRegrowthImmediateArraysReleaseBeforePointerSentinelUnregister()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArrayImmediate<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.Contains("H8Memory.Release(ref array, NativeMemorySystemId);", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "H8Memory.Release(ref array, NativeMemorySystemId);");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void WorldRegrowthDeferredArraysCompleteUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe JobHandle DisposeNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.Contains("JobHandle disposeHandle = H8Memory.Release(ref array, dependency, NativeMemorySystemId);", helper);
            StringAssert.Contains("DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "JobHandle disposeHandle = H8Memory.Release(ref array, dependency, NativeMemorySystemId);", "DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)");
            AssertTextBefore(helper, "DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void MapMagicVegetationArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs");
            string immediate = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array)", StringComparison.Ordinal));
            string dependency = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", immediate);
            AssertTextBefore(immediate, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(immediate, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", immediate);

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", dependency);
            AssertTextBefore(dependency, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            StringAssert.Contains("if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", dependency);
            AssertTextBefore(dependency, "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            AssertTextBefore(dependency, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", dependency);
        }

        [Test]
        public void MapMagicTerrainNodesUnregisterTempJobArraySentinelBeforeSyncDispose()
        {
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs");
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs");
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonSandboxAbyssalShelfMapMagicNode.cs");
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonBiomeMatrixMapMagicPostProcessNode.cs");
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs");
            AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose("Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs");
        }

        [Test]
        public void FluidEngineGpuReadbackArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/HectonFluidEngine.cs");
            string ensureSlot = ExtractMethodAt(source, source.IndexOf("private void EnsureSlotCold", StringComparison.Ordinal));
            string disposeSlot = ExtractMethodAt(source, source.IndexOf("private unsafe void DisposeSlot", StringComparison.Ordinal));
            string disposeNativeArray = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)", StringComparison.Ordinal));

            StringAssert.Contains("int sentinelId = 0;", ensureSlot);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeArray(", ensureSlot);
            AssertTextBefore(ensureSlot, "NativeMemorySentinel.Unregister(sentinelId);", "array.Dispose();");
            AssertTextAfter(ensureSlot, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", ensureSlot);

            AssertTextBefore(disposeSlot, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "_data[slot].Dispose();");
            AssertTextBefore(disposeSlot, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "_data[slot] = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", disposeSlot);

            AssertTextBefore(disposeNativeArray, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            StringAssert.Contains("if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", disposeNativeArray);
            AssertTextBefore(disposeNativeArray, "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            AssertTextBefore(disposeNativeArray, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", disposeNativeArray);
        }

        [Test]
        public void ProceduralFieldSamplerTrackedArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralFieldSampler.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeTrackedNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void ScatterEvaluatorDeferredDisposeCompletesBeforePointerSentinelUnregister()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/ScatterEvaluator.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            StringAssert.DoesNotContain("bool disposed", helper);
            StringAssert.Contains("JobHandle disposeHandle = array.Dispose(dependency);", helper);
            StringAssert.Contains("if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", helper);
            StringAssert.Contains("ScatterEvaluator native array disposal did not complete before sentinel unregister.", helper);
            StringAssert.Contains("array.Dispose();", helper);
            StringAssert.DoesNotContain("if (disposed)", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", helper);
            AssertTextBefore(helper, "JobHandle disposeHandle = array.Dispose(dependency);", "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))");
            AssertTextBefore(helper, "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void VoxelEngineTrackedArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/HectonVoxelEngine.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("static unsafe void DisposeTrackedNativeArray<T>", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        [Test]
        public void PersistentWorldRegistryTransientContainersUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string arrayHelper = ExtractMethodAt(source, source.IndexOf("private static unsafe void DisposeTrackedTransientArray<T>", StringComparison.Ordinal));
            string listHelper = ExtractMethodAt(source, source.IndexOf("private static void DisposeTrackedTransientNativeList<T>", StringComparison.Ordinal));
            string indexedSnapshot = ExtractMethodAt(source, source.IndexOf("private bool TryLoadIndexedSectorRecordsSnapshot", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", arrayHelper);
            AssertTextBefore(arrayHelper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextAfter(arrayHelper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", arrayHelper);

            AssertTextBefore(listHelper, "NativeMemorySentinel.Unregister(sentinelId);", "list.Dispose();");
            AssertTextAfter(listHelper, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");

            StringAssert.Contains("DisposeTrackedTransientNativeList(ref loadedSectorRecords, ref loadedSectorRecordsSentinelId);", indexedSnapshot);
            StringAssert.Contains("DisposeTrackedTransientArray(ref desiredSectorHashView);", indexedSnapshot);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", source);
        }

        [Test]
        public void ProceduralWreckageToolArraysUnregisterBeforeSyncDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs");
            string helper = ExtractMethodAt(source, source.IndexOf("private static void DisposeNativeArray<T>", StringComparison.Ordinal));
            string queueHelper = ExtractMethodAt(source, source.IndexOf("private static void DisposeNativeQueue<T>", StringComparison.Ordinal));
            string hashMapHelper = ExtractMethodAt(source, source.IndexOf("private static void DisposeNativeParallelHashMap<TKey, TValue>", StringComparison.Ordinal));

            StringAssert.Contains("RegisterNativeQueueInstance(queue", source);
            StringAssert.Contains("RegisterNativeParallelHashMapInstance(map", source);
            StringAssert.DoesNotContain("bool disposed = !", helper);
            StringAssert.Contains("array.Dispose();", helper);
            StringAssert.DoesNotContain("disposed = true;", helper);
            StringAssert.DoesNotContain("if (disposed)", helper);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", helper);
            StringAssert.Contains("sentinelId = 0;", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.Unregister(sentinelId);", "array.Dispose();");
            AssertTextBefore(helper, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.Contains("finally", helper);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);

            AssertTextBefore(queueHelper, "NativeMemorySentinel.Unregister(sentinelId);", "queue.Dispose();");
            AssertTextBefore(queueHelper, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", source);

            AssertTextBefore(hashMapHelper, "NativeMemorySentinel.Unregister(sentinelId);", "map.Dispose();");
            AssertTextBefore(hashMapHelper, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelHashMap", source);
        }

        [Test]
        public void EditorToolingPointerlessContainersUseInstanceIdsAndUnregisterBeforeSyncDispose()
        {
            string anomaly = ReadProjectFile("Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs");
            string anomalyAllocateQueue = ExtractMethodAt(anomaly, anomaly.IndexOf("private static NativeQueue<T> AllocateTrackedTempJobQueue<T>", StringComparison.Ordinal));
            string anomalyDisposeQueue = ExtractMethodAt(anomaly, anomaly.IndexOf("private static void DisposeTrackedQueue<T>", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeQueueInstance(queue", anomalyAllocateQueue);
            AssertTextBefore(anomalyAllocateQueue, "NativeMemorySentinel.Unregister(sentinelId);", "queue.Dispose();");
            AssertTextBefore(anomalyDisposeQueue, "NativeMemorySentinel.Unregister(sentinelId);", "queue.Dispose();");
            AssertTextBefore(anomalyDisposeQueue, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", anomaly);

            string erosion = ReadProjectFile("Assets/_Project/Scripts/Editor/ErosionTestHarness.cs");
            string erosionAllocateQueue = ExtractMethodAt(erosion, erosion.IndexOf("private static NativeQueue<HydraulicErosionHeightDelta> AllocateTrackedHeightDeltaQueue", StringComparison.Ordinal));
            string erosionDisposeQueue = ExtractMethodAt(erosion, erosion.IndexOf("private static void DisposeTrackedQueue(ref NativeQueue<HydraulicErosionHeightDelta> queue", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeQueueInstance(queue", erosionAllocateQueue);
            AssertTextBefore(erosionAllocateQueue, "NativeMemorySentinel.Unregister(registrationId);", "queue.Dispose();");
            AssertTextBefore(erosionDisposeQueue, "NativeMemorySentinel.Unregister(registrationId);", "queue.Dispose();");
            AssertTextBefore(erosionDisposeQueue, "NativeMemorySentinel.Unregister(registrationId);", "registrationId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", erosion);

            string spatialHash = ReadProjectFile("Assets/_Project/Scripts/Editor/HectonSpatialHashEditorSelfTests.cs");
            string spatialAllocateResults = ExtractMethodAt(spatialHash, spatialHash.IndexOf("private static NativeList<int> AllocateTrackedResults", StringComparison.Ordinal));
            string spatialDisposeResults = ExtractMethodAt(spatialHash, spatialHash.IndexOf("private static void DisposeTrackedResults", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(results", spatialAllocateResults);
            AssertTextBefore(spatialAllocateResults, "NativeMemorySentinel.Unregister(sentinelId);", "results.Dispose();");
            AssertTextBefore(spatialDisposeResults, "NativeMemorySentinel.Unregister(sentinelId);", "results.Dispose();");
            AssertTextBefore(spatialDisposeResults, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", spatialHash);

            string geologyMemory = ReadProjectFile("Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeNativeMemory.cs");
            string geologyAllocateList = ExtractMethodAt(geologyMemory, geologyMemory.IndexOf("internal static NativeList<T> AllocateList<T>", StringComparison.Ordinal));
            string geologyDisposeList = ExtractMethodAt(geologyMemory, geologyMemory.IndexOf("internal static void DisposeList<T>", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(list", geologyAllocateList);
            AssertTextBefore(geologyAllocateList, "NativeMemorySentinel.Unregister(sentinelId);", "list.Dispose();");
            AssertTextBefore(geologyDisposeList, "NativeMemorySentinel.Unregister(sentinelId);", "list.Dispose();");
            AssertTextBefore(geologyDisposeList, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", geologyMemory);

            string topographyCsv = ReadProjectFile("Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeCsv.cs");
            StringAssert.Contains("private int _recipesSentinelId;", topographyCsv);
            StringAssert.Contains("out store._recipesSentinelId", topographyCsv);
            StringAssert.Contains("GeologyForgeNativeMemory.DisposeList(ref _recipes, ref _recipesSentinelId);", topographyCsv);

            string geographyProfiles = ReadProjectFile("Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityProfileCsv.cs");
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(", geographyProfiles);
            AssertTextBefore(geographyProfiles, "NativeMemorySentinel.Unregister(_profilesSentinelId);", "_profiles.Dispose();");
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", geographyProfiles);

            string geologyGenerator = ReadProjectFile("Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs");
            string geologyAllocateHash = ExtractMethodAt(geologyGenerator, geologyGenerator.IndexOf("private static NativeParallelMultiHashMap<TKey, TValue> AllocateGeologyMultiHashMap", StringComparison.Ordinal));
            string geologyReleaseHash = ExtractMethodAt(geologyGenerator, geologyGenerator.IndexOf("private static void ReleaseGeologyMultiHashMap", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeParallelMultiHashMapInstance(", geologyAllocateHash);
            AssertTextBefore(geologyAllocateHash, "NativeMemorySentinel.Unregister(sentinelId);", "map.Dispose();");
            AssertTextBefore(geologyReleaseHash, "NativeMemorySentinel.Unregister(sentinelId);", "map.Dispose();");
            AssertTextBefore(geologyReleaseHash, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelMultiHashMap", geologyGenerator);

            string bioForge = ReadProjectFile("Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs");
            string bioForgeAllocateList = ExtractMethodAt(bioForge, bioForge.IndexOf("private static NativeList<T> AllocateTrackedNativeList<T>", StringComparison.Ordinal));
            string bioForgeDisposeList = ExtractMethodAt(bioForge, bioForge.IndexOf("private static void DisposeTrackedNativeList<T>", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(list", bioForgeAllocateList);
            AssertTextBefore(bioForgeAllocateList, "NativeMemorySentinel.Unregister(sentinelId);", "list.Dispose();");
            AssertTextBefore(bioForgeDisposeList, "NativeMemorySentinel.Unregister(sentinelId);", "list.Dispose();");
            AssertTextBefore(bioForgeDisposeList, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", bioForge);

            string hadalTrenchBake = ReadProjectFile("Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs");
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(", hadalTrenchBake);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", hadalTrenchBake);
        }

        [Test]
        public void SmokeAndFuzzerArraysUnregisterBeforeSyncDispose()
        {
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/AutomationSmokeTester.cs",
                "private static unsafe void DisposeTempArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs",
                "private static unsafe void DisposeTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs",
                "private static unsafe void DisposeSmokeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/SaveRecoverySmokeTester.cs",
                "private static unsafe void DisposeTrackedTempArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs",
                "private static unsafe void DisposeTrackedTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/ThermalMeltSmokeTester.cs",
                "private static unsafe void DisposeTrackedTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs",
                "private static unsafe void DisposeTrackedTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs",
                "private static unsafe void DisposeTrackedTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs",
                "private static unsafe void DisposeTrackedTempJobArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs",
                "private static unsafe void DisposeSmokeArray<T>");
        }

        [Test]
        public void EditorToolingArraysUnregisterBeforeSyncDispose()
        {
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureNativeMemory.cs",
                "internal static unsafe void DisposeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeNativeMemory.cs",
                "internal static unsafe void DisposeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/Shinobu132CablePhysicsTunerWindow.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs",
                "internal static unsafe void DisposeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs",
                "private static unsafe void DisposeTracked<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionWeatheringCsv.cs",
                "private static void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineOptimizationProfileCsv.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs",
                "private static unsafe void DisposeTrackedArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs",
                "private static unsafe void Dispose<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs",
                "private static unsafe void ReleaseGeologyArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs",
                "private static unsafe void ReleaseTopographyArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityPipeline.cs",
                "private static unsafe void ReleaseNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>");
            AssertNativeArrayPointerDisposeHelper(
                "Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs",
                "private static void DisposeTrackedNativeArray<T>");

            AssertIdAndFallbackDisposeBeforeUnregister(
                "Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs",
                "private static unsafe void DisposePreviewArray<T>",
                "array.Dispose();",
                "NativeMemorySentinel.Unregister(sentinelId);");
            string lSystemGenomeLab = ReadProjectFile("Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs");
            string disposePreviewArray = ExtractMethodAt(lSystemGenomeLab, lSystemGenomeLab.IndexOf("private static unsafe void DisposePreviewArray<T>", StringComparison.Ordinal));
            int previewMissingArrayBranch = disposePreviewArray.IndexOf("if (!array.IsCreated)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(previewMissingArrayBranch, 0, "Missing L-system stale array branch.");
            int previewMissingArrayReturn = disposePreviewArray.IndexOf("return;", previewMissingArrayBranch, StringComparison.Ordinal);
            Assert.GreaterOrEqual(previewMissingArrayReturn, 0, "Missing L-system stale array branch return.");
            string previewMissingArrayBlock = disposePreviewArray.Substring(previewMissingArrayBranch, previewMissingArrayReturn - previewMissingArrayBranch);
            StringAssert.Contains("if (!array.IsCreated)", disposePreviewArray);
            StringAssert.DoesNotContain("bool disposed", disposePreviewArray);
            StringAssert.DoesNotContain("if (disposed)", disposePreviewArray);
            AssertTextBefore(disposePreviewArray, "NativeMemorySentinel.Unregister(sentinelId);", "array.Dispose();");
            AssertTextBefore(disposePreviewArray, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextBefore(disposePreviewArray, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(sentinelId);", previewMissingArrayBlock);
            StringAssert.DoesNotContain("sentinelId = 0;", previewMissingArrayBlock);
            AssertIdAndFallbackDisposeBeforeUnregister(
                "Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs",
                "private static void Dispose()",
                "_ring.Dispose();",
                "NativeMemorySentinel.Unregister(_ringSentinelId);");

            string hadalTrenchBake = ReadProjectFile("Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs");
            string releaseRleRuns = ExtractMethodAt(hadalTrenchBake, hadalTrenchBake.IndexOf("private void ReleaseRleRuns()", StringComparison.Ordinal));
            AssertTextBefore(releaseRleRuns, "NativeMemorySentinel.Unregister(_rleRunsSentinelId);", "_rleRuns.Dispose();");
            AssertTextBefore(releaseRleRuns, "NativeMemorySentinel.Unregister(_rleRunsSentinelId);", "_rleRunsSentinelId = 0;");

            string audioScanner = ReadProjectFile("Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs");
            AssertTextBefore(audioScanner, "NativeMemorySentinel.Unregister(samplesSentinelId);", "samples.Dispose();");
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", audioScanner);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", audioScanner);

            string hydraulicErosionForge = ReadProjectFile("Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs");
            string newTrackedQueue = ExtractMethodAt(hydraulicErosionForge, hydraulicErosionForge.IndexOf("private static NativeQueue<T> NewTrackedQueue<T>", StringComparison.Ordinal));
            string disposeTrackedQueue = ExtractMethodAt(hydraulicErosionForge, hydraulicErosionForge.IndexOf("private static void DisposeTrackedQueue<T>", StringComparison.Ordinal));

            StringAssert.Contains("out int sentinelId", newTrackedQueue);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeQueueInstance(queue", newTrackedQueue);
            AssertTextBefore(newTrackedQueue, "NativeMemorySentinel.Unregister(sentinelId);", "queue.Dispose();");
            AssertTextBefore(disposeTrackedQueue, "NativeMemorySentinel.Unregister(sentinelId);", "queue.Dispose();");
            AssertTextBefore(disposeTrackedQueue, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", hydraulicErosionForge);
        }

        [Test]
        public void ReflectionBakeToolsDisposeBeforePointerBridgeUnregister()
        {
            AssertReflectionPointerDisposeBridge(
                "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>",
                "UnregisterTrackedNativeArray(trackedPointer)");
            AssertReflectionPointerDisposeBridge(
                "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs",
                "private static unsafe void DisposeTrackedNativeArray<T>",
                "UnregisterNativeMemorySentinel(trackedPointer)");
            AssertReflectionPointerDisposeBridge(
                "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs",
                "internal static unsafe void DisposeTrackedNativeArray<T>",
                "UnregisterTrackedNativeArray(trackedPointer)");

            string habitat = ReadProjectFile("Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs");
            string disposeHelper = ExtractMethodAt(habitat, habitat.IndexOf("internal static unsafe void DisposeTrackedNativeArray<T>", StringComparison.Ordinal));
            string bridge = ExtractMethodAt(habitat, habitat.IndexOf("internal static void UnregisterPointer(IntPtr trackedPointer)", StringComparison.Ordinal));

            StringAssert.Contains("IntPtr trackedPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", disposeHelper);
            AssertTextBefore(disposeHelper, "array.Dispose();", "HabitatDamageNativeMemorySentinelBridge.UnregisterPointer(trackedPointer);");
            StringAssert.Contains("sentinelType.GetMethod(\"UnregisterPointer\", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)", bridge);
            StringAssert.Contains("method.Invoke(null, new object[] { trackedPointer });", bridge);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", habitat);
            StringAssert.DoesNotContain("HabitatDamageNativeMemorySentinelBridge.UnregisterNativeArray", habitat);
        }

        [Test]
        public void NativeMemorySentinelPointerUnregisterIsPublicForEditorTooling()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");

            StringAssert.Contains("public static void UnregisterPointer(void* pointer)", source);
            StringAssert.Contains("public static void UnregisterPointer(IntPtr pointer)", source);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static void AssertNativeArrayPointerDisposeHelper(string relativePath, string methodNeedle)
        {
            string source = ReadProjectFile(relativePath);
            string helper = ExtractMethodAt(source, source.IndexOf(methodNeedle, StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", helper);
            AssertTextBefore(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array.Dispose();");
            AssertTextAfter(helper, "NativeMemorySentinel.UnregisterPointer(trackedPointer);", "array = default;");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        private static void AssertIdAndFallbackDisposeBeforeUnregister(string relativePath, string methodNeedle, string disposeCall, string idUnregister)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodAt(source, source.IndexOf(methodNeedle, StringComparison.Ordinal));

            AssertTextBefore(method, disposeCall, idUnregister);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", method);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", method);
        }

        private static void AssertMapMagicTempJobArrayUnregisterBeforeSyncDispose(string relativePath)
        {
            string source = ReadProjectFile(relativePath);
            int helperIndex = source.IndexOf("private static void DisposeTracked<T>", StringComparison.Ordinal);
            if (helperIndex < 0)
                helperIndex = source.IndexOf("internal static void DisposeTracked<T>", StringComparison.Ordinal);
            if (helperIndex < 0)
                helperIndex = source.IndexOf("private static void DisposeTracked(ref NativeArray<float>", StringComparison.Ordinal);
            string helper = ExtractMethodAt(source, helperIndex);
            int missingArrayBranch = helper.IndexOf("if (!array.IsCreated)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(missingArrayBranch, 0, "Missing stale array branch.");
            int missingArrayReturn = helper.IndexOf("return;", missingArrayBranch, StringComparison.Ordinal);
            Assert.GreaterOrEqual(missingArrayReturn, 0, "Missing stale array branch return.");
            string missingArrayBlock = helper.Substring(missingArrayBranch, missingArrayReturn - missingArrayBranch);

            StringAssert.Contains("array.Dispose();", helper);
            AssertTextAfter(helper, "array.Dispose();", "array = default;");
            AssertTextAfter(helper, "array.Dispose();", "NativeMemorySentinel.Unregister(registrationId);");
            AssertTextAfter(helper, "NativeMemorySentinel.Unregister(registrationId);", "registrationId = 0;");
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(registrationId);", missingArrayBlock);
            StringAssert.DoesNotContain("registrationId = 0;", missingArrayBlock);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", helper);
        }

        private static void AssertReflectionPointerDisposeBridge(string relativePath, string disposeNeedle, string bridgeCall)
        {
            string source = ReadProjectFile(relativePath);
            string disposeHelper = ExtractMethodAt(source, source.IndexOf(disposeNeedle, StringComparison.Ordinal));

            StringAssert.Contains("IntPtr trackedPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);", disposeHelper);
            AssertTextBefore(disposeHelper, "array.Dispose();", bridgeCall);
            StringAssert.Contains("sentinelType.GetMethod(\"UnregisterPointer\", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)", source);
            StringAssert.Contains("method.Invoke(null, new object[] { trackedPointer });", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", source);
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing expected text: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing expected text: " + after);
            Assert.Less(beforeIndex, afterIndex, before + " must appear before " + after);
        }

        private static void AssertTextAfter(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing expected text: " + before);

            int afterIndex = source.IndexOf(after, beforeIndex + before.Length, StringComparison.Ordinal);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing expected text after " + before + ": " + after);
        }

        private static string ExtractMethodAt(string source, int methodStart)
        {
            Assert.GreaterOrEqual(methodStart, 0, "Missing method start.");

            int open = source.IndexOf('{', methodStart);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace.");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace.");
            return string.Empty;
        }
    }
}
