using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class ArenaAllocatorSentinel1414EditTests
    {
        [Test]
        public void GlobalDataVault_ReallocateRaw_CallRequiresRelocationGuard()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");

            StringAssert.Contains("in H8RawReallocationGuard relocationGuard", h8Memory);
            StringAssert.Contains("if (!relocationGuard.AllowsRelocation)", h8Memory);
            StringAssert.Contains("H8RawReallocationGuard.Create", vault);
            StringAssert.Contains("ActiveBurstLockMask", vault);
            StringAssert.Contains("HasPinnedExternalViews()", vault);
        }

        [Test]
        public void GlobalDataVault_RawPayloadAndArenaFreePathsCheckH8MemoryResult()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string freeRaw = ExtractMethod(h8Memory, "public static void FreeRaw(void* pointer, Allocator allocator, SystemID requester)");
            string tryFreeRaw = ExtractMethod(h8Memory, "internal static bool TryFreeRaw");
            string storePayload = ExtractMethod(vault, "public bool TryStoreMacroDatabasePayload");
            string rollbackPayload = ExtractMethod(vault, "private static void FreeMacroDatabasePayloadRollbackOrThrow");
            string removePayload = ExtractMethod(vault, "public bool TryRemoveMacroDatabasePayload");
            string dispose = ExtractMethod(vault, "public void Dispose()");
            string disposePayloadCache = ExtractMethod(vault, "private void DisposeMacroDatabasePayloadCache");

            StringAssert.Contains("if (TryFreeRaw(pointer, allocator, requester))", freeRaw);
            StringAssert.Contains("throw new InvalidOperationException(", freeRaw);
            StringAssert.Contains("pointer ownership remains unchanged", freeRaw);
            StringAssert.Contains("return false;", tryFreeRaw);
            Assert.AreEqual(0, CountOccurrences(vault, "H8Memory.FreeRaw("));

            StringAssert.Contains("!H8Memory.TryFreeRaw(existing.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault)", storePayload);
            StringAssert.Contains("FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, null);", storePayload);
            StringAssert.Contains("if (H8Memory.TryFreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault))", rollbackPayload);
            StringAssert.Contains("throw cleanupFailure;", rollbackPayload);

            StringAssert.Contains("!H8Memory.TryFreeRaw(entry.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault)", removePayload);
            StringAssert.Contains("removed = default;", removePayload);
            StringAssert.Contains("!H8Memory.TryFreeRaw(_arenaBase, Allocator.Persistent, SystemID.CoreDataVault)", dispose);
            StringAssert.Contains("!H8Memory.TryFreeRaw(entry.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault)", disposePayloadCache);
        }

        [Test]
        public void HectonArenaAllocator_ShutdownKeepsSentinelUntilRawFreeSucceeds()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/HectonArenaAllocator.cs");
            string shutdown = ExtractMethod(source, "public static void Shutdown()");

            StringAssert.Contains("ReleaseSafetyHandles();", shutdown);
            StringAssert.Contains("H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", shutdown);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_sentinelId);", shutdown);
            StringAssert.Contains("_basePtr = null;", shutdown);
            Assert.Less(
                shutdown.LastIndexOf("ReleaseSafetyHandles();", StringComparison.Ordinal),
                shutdown.IndexOf("H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", StringComparison.Ordinal));
            Assert.Less(
                shutdown.IndexOf("H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", StringComparison.Ordinal),
                shutdown.IndexOf("NativeMemorySentinel.Unregister(_sentinelId);", StringComparison.Ordinal));
            Assert.Less(
                shutdown.IndexOf("H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", StringComparison.Ordinal),
                shutdown.IndexOf("_basePtr = null;", StringComparison.Ordinal));
        }

        [Test]
        public void HectonArenaAllocator_SentinelRegistrationFailureDisablesAllocatorAfterRollback()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/HectonArenaAllocator.cs");
            string initialize = ExtractMethod(source, "public static void Initialize(int capacityBytes = DefaultArenaBytes)");
            string resetFailed = ExtractMethod(source, "private static void ResetFailedInitializationState()");

            StringAssert.Contains("releasedArenaBase = H8Memory.TryFreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", initialize);
            StringAssert.Contains("ResetFailedInitializationState();", initialize);
            StringAssert.Contains("rollback free failed; allocator was disabled", initialize);
            Assert.Less(
                initialize.IndexOf("releasedArenaBase = H8Memory.TryFreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);", StringComparison.Ordinal),
                initialize.IndexOf("ResetFailedInitializationState();", StringComparison.Ordinal));

            StringAssert.Contains("ReleaseSafetyHandles();", resetFailed);
            StringAssert.Contains("_basePtr = null;", resetFailed);
            StringAssert.Contains("_sentinelId = 0;", resetFailed);
            StringAssert.Contains("ResetScalarState();", resetFailed);
            StringAssert.Contains("ClearManagedState();", resetFailed);
        }

        [Test]
        public void H8Memory_RawRegistrationFailureRollsBackUntrackedPointers()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string allocateRaw = ExtractMethod(h8Memory, "public static void* AllocateRaw");
            string reallocateRaw = ExtractMethod(h8Memory, "internal static void* ReallocateRaw");
            string releaseSentinelReapedRaw = ExtractMethod(h8Memory, "public static bool ReleaseSentinelReapedRaw");
            string freeUntracked = ExtractMethod(h8Memory, "private static bool TryFreeUntrackedRawPointer");

            StringAssert.Contains("TryFreeUntrackedRawPointer(pointer, allocator, owner);", allocateRaw);
            StringAssert.Contains("TryFreeUntrackedRawPointer(newPointer, allocator, owner);", reallocateRaw);
            StringAssert.Contains("TryFreeUntrackedRawPointer(pointer, fallbackAllocator, SystemID.Unknown);", releaseSentinelReapedRaw);
            StringAssert.Contains("UnsafeUtility.Free(pointer, allocator);", freeUntracked);
            StringAssert.Contains("RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);", freeUntracked);
            StringAssert.Contains("return false;", freeUntracked);
        }

        [Test]
        public void H8Memory_UnregisterBeforeNativeFreeHasRestoreOrDelayedRemoval()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string release = ExtractMethod(h8Memory, "public static void Release<T>(ref NativeArray<T> array, SystemID owner)");
            string deferredRelease = ExtractMethod(h8Memory, "public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency, SystemID owner)");
            string reallocateRaw = ExtractMethod(h8Memory, "internal static void* ReallocateRaw");
            string tryFreeRaw = ExtractMethod(h8Memory, "public static bool TryFreeRaw");
            string forceFreeRecordAt = ExtractMethod(h8Memory, "private static bool ForceFreeRecordAt");
            string freeAndRestore = ExtractMethod(h8Memory, "private static bool TryUnregisterFreeAndRestoreOnFailure");

            StringAssert.Contains("H8AllocationRecord record = default;", release);
            StringAssert.Contains("bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);", release);
            StringAssert.Contains("if (!UnregisterPointer(pointer, owner))", release);
            StringAssert.Contains("catch (Exception disposeException)", release);
            StringAssert.Contains("TryRestoreUnregisteredRecord(in record)", release);
            Assert.Less(
                release.IndexOf("if (!UnregisterPointer(pointer, owner))", StringComparison.Ordinal),
                release.IndexOf("array.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                release.IndexOf("array.Dispose();", StringComparison.Ordinal),
                release.IndexOf("array = default;", StringComparison.Ordinal));

            StringAssert.Contains("H8AllocationRecord record = default;", deferredRelease);
            StringAssert.Contains("bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);", deferredRelease);
            StringAssert.Contains("if (!UnregisterPointer(pointer, owner))", deferredRelease);
            StringAssert.Contains("catch (Exception disposeException)", deferredRelease);
            StringAssert.Contains("TryRestoreUnregisteredRecord(in record)", deferredRelease);
            Assert.Less(
                deferredRelease.IndexOf("if (!UnregisterPointer(pointer, owner))", StringComparison.Ordinal),
                deferredRelease.IndexOf("JobHandle disposeHandle = array.Dispose(dependency);", StringComparison.Ordinal));

            StringAssert.Contains("H8AllocationRecord record = default;", tryFreeRaw);
            StringAssert.Contains("bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);", tryFreeRaw);
            StringAssert.Contains("if (!UnregisterPointer(pointer, requester))", tryFreeRaw);
            StringAssert.Contains("UnsafeUtility.Free(pointer, allocator);", tryFreeRaw);
            StringAssert.Contains("TryRestoreUnregisteredRecord(in record)", tryFreeRaw);
            Assert.Less(
                tryFreeRaw.IndexOf("if (!UnregisterPointer(pointer, requester))", StringComparison.Ordinal),
                tryFreeRaw.IndexOf("UnsafeUtility.Free(pointer, allocator);", StringComparison.Ordinal));

            StringAssert.Contains("H8AllocationRecord oldRecord = default;", reallocateRaw);
            StringAssert.Contains("bool canRestoreOldTracking = TryFindRecordIndex((IntPtr)oldPointer, out int oldRecordIndex);", reallocateRaw);
            StringAssert.Contains("if (!UnregisterPointer(oldPointer, owner))", reallocateRaw);
            StringAssert.Contains("UnsafeUtility.Free(oldPointer, allocator);", reallocateRaw);
            StringAssert.Contains("bool restoredOldTracking = !canRestoreOldTracking || TryRestoreUnregisteredRecord(in oldRecord);", reallocateRaw);
            StringAssert.Contains("bool releasedNewPointer = TryUnregisterFreeAndRestoreOnFailure(newPointer, owner, allocator, requireOwnerMatch: false);", reallocateRaw);
            Assert.Less(
                reallocateRaw.IndexOf("if (!UnregisterPointer(oldPointer, owner))", StringComparison.Ordinal),
                reallocateRaw.IndexOf("UnsafeUtility.Free(oldPointer, allocator);", StringComparison.Ordinal));

            StringAssert.Contains("H8AllocationRecord record = default;", freeAndRestore);
            StringAssert.Contains("bool canRestoreTracking = TryFindRecordIndex((IntPtr)pointer, out int recordIndex);", freeAndRestore);
            StringAssert.Contains("if (!UnregisterPointer(pointer, owner, requireOwnerMatch))", freeAndRestore);
            StringAssert.Contains("UnsafeUtility.Free(pointer, allocator);", freeAndRestore);
            StringAssert.Contains("TryRestoreUnregisteredRecord(in record);", freeAndRestore);

            StringAssert.Contains("UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);", forceFreeRecordAt);
            StringAssert.Contains("RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease);", forceFreeRecordAt);
            Assert.Less(
                forceFreeRecordAt.IndexOf("UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);", StringComparison.Ordinal),
                forceFreeRecordAt.IndexOf("RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease);", StringComparison.Ordinal));
        }

        [Test]
        public void MpscSignalRingBuffer_BridgeRegistrationFailureDisposesNativeArrays()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs");
            string bridge = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/NativeMemoryTrackingBridge.cs");
            string installer = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemoryTrackingBridgeInstaller.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string constructor = ExtractMethod(source, "public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator, object owner)");
            string registerArrays = ExtractMethod(source, "private void RegisterNativeArrays");
            string registerNativeArray = ExtractMethod(source, "private int RegisterNativeArray<TArray>");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string disposeNativeArray = ExtractMethod(source, "private static void DisposeRegisteredNativeArray<TArray>");
            string unregisterNativeArray = ExtractMethod(source, "private static void UnregisterNativeArray");
            string bridgeRegisterBytes = ExtractMethod(installer, "private static unsafe int RegisterBytes(");
            string bridgeRegisterBytesInstance = ExtractMethod(installer, "private static unsafe int RegisterBytesInstance");
            string registerPointerlessBridgeRecord = ExtractMethod(sentinel, "internal static int RegisterPointerlessBridgeRecord");

            StringAssert.Contains("_buffer = default;", constructor);
            StringAssert.Contains("_publishedTickets = default;", constructor);
            StringAssert.Contains("_cursor = default;", constructor);
            StringAssert.Contains("_bridgeLifetime = ResolveBridgeLifetime(allocator);", constructor);
            StringAssert.Contains("_bufferRegistrationId = 0;", constructor);
            StringAssert.Contains("_publishedTicketsRegistrationId = 0;", constructor);
            StringAssert.Contains("_cursorRegistrationId = 0;", constructor);
            StringAssert.Contains("try", constructor);
            StringAssert.Contains("if (!_buffer.IsCreated || !_publishedTickets.IsCreated || !_cursor.IsCreated)", constructor);
            StringAssert.Contains("Dispose();", constructor);
            StringAssert.Contains("RegisterNativeArrays(allocator);", constructor);
            StringAssert.Contains("catch", constructor);
            StringAssert.Contains("throw;", constructor);
            Assert.Less(
                constructor.IndexOf("if (!_buffer.IsCreated || !_publishedTickets.IsCreated || !_cursor.IsCreated)", StringComparison.Ordinal),
                constructor.IndexOf("RegisterNativeArrays(allocator);", StringComparison.Ordinal));

            StringAssert.Contains("if (!Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)", registerArrays);
            StringAssert.Contains("throw new InvalidOperationException(BridgeUnavailableMessage);", registerArrays);
            StringAssert.Contains("public delegate void UnregisterIdDelegate(int id);", bridge);
            StringAssert.Contains("private static RegisterBytesDelegate s_registerBytesInstance;", bridge);
            StringAssert.Contains("private static UnregisterIdDelegate s_unregisterId;", bridge);
            StringAssert.Contains("public static bool IsInstalled => s_registerBytes != null && s_registerBytesInstance != null && s_unregisterOwnerLabel != null && s_unregisterId != null;", bridge);
            StringAssert.Contains("public static void Unregister(int id)", bridge);
            StringAssert.Contains("[InitializeOnLoadMethod]", installer);
            StringAssert.Contains("private static void InstallForEditor()", installer);
            StringAssert.Contains("NativeMemoryTrackingBridge.Install(RegisterBytes, RegisterBytesInstance, UnregisterOwnerLabel, UnregisterId);", installer);
            StringAssert.Contains("private static void UnregisterOwnerLabel(string owner, string label)", installer);
            StringAssert.Contains("private static void UnregisterId(int id)", installer);
            StringAssert.Contains("NativeMemorySentinel.RegisterPointer(", bridgeRegisterBytes);
            StringAssert.Contains("NativeMemorySentinel.RegisterPointerlessBridgeRecord(", bridgeRegisterBytesInstance);
            StringAssert.Contains("return RegisterPointer(null, bytes, owner, label, lifetime, false);", registerPointerlessBridgeRecord);
            StringAssert.Contains("_bridgeLifetime = ResolveBridgeLifetime(allocator);", registerArrays);
            StringAssert.Contains("_bufferRegistrationId = RegisterNativeArray(_buffer, BufferLabel, _bridgeLifetime);", registerArrays);
            StringAssert.Contains("_publishedTicketsRegistrationId = RegisterNativeArray(_publishedTickets, PublishedTicketsLabel, _bridgeLifetime);", registerArrays);
            StringAssert.Contains("_cursorRegistrationId = RegisterNativeArray(_cursor, CursorLabel, _bridgeLifetime);", registerArrays);

            StringAssert.Contains("int registrationId = Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArrayInstance(", registerNativeArray);
            StringAssert.Contains("if (registrationId > 0)", registerNativeArray);
            StringAssert.Contains("return registrationId;", registerNativeArray);
            StringAssert.Contains("throw new InvalidOperationException(", registerNativeArray);
            StringAssert.Contains("Native memory tracking bridge registration failed", registerNativeArray);

            StringAssert.Contains("DisposeRegisteredNativeArray(ref _buffer, ref _bufferRegistrationId);", dispose);
            StringAssert.Contains("DisposeRegisteredNativeArray(ref _publishedTickets, ref _publishedTicketsRegistrationId);", dispose);
            StringAssert.Contains("DisposeRegisteredNativeArray(ref _cursor, ref _cursorRegistrationId);", dispose);
            StringAssert.Contains("array.Dispose();", disposeNativeArray);
            StringAssert.Contains("array = default;", disposeNativeArray);
            StringAssert.Contains("UnregisterNativeArray(ref registrationId);", disposeNativeArray);
            Assert.IsFalse(disposeNativeArray.Contains("catch (Exception disposeException)", StringComparison.Ordinal));
            Assert.IsFalse(disposeNativeArray.Contains("RegisterNativeArray(array, label, _bridgeLifetime)", StringComparison.Ordinal));
            Assert.Less(
                disposeNativeArray.IndexOf("array.Dispose();", StringComparison.Ordinal),
                disposeNativeArray.IndexOf("UnregisterNativeArray(ref registrationId);", StringComparison.Ordinal));
            StringAssert.Contains("Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);", unregisterNativeArray);
            StringAssert.DoesNotContain("UnregisterNativeArray(trackedArray, label);", disposeNativeArray);
        }

        [Test]
        public void MockSignalBus_BridgeRegistrationFailureRollsBackNativeQueue()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/VaultMockSignalBus.cs");
            string constructor = ExtractMethod(source, "public MockSignalBus(Allocator allocator");
            string dispose = ExtractMethod(source, "public void Dispose()");

            StringAssert.Contains("_queue = default;", constructor);
            StringAssert.Contains("_sentinelRegistrationId = 0;", constructor);
            StringAssert.Contains("try", constructor);
            StringAssert.Contains("_queue = new NativeQueue<T>(allocator);", constructor);
            StringAssert.Contains("_sentinelRegistrationId = NativeMemoryTrackingBridge.RegisterBytesInstance(", constructor);
            StringAssert.Contains("if (_sentinelRegistrationId <= 0)", constructor);
            StringAssert.Contains("throw new InvalidOperationException(NativeMemoryRegistrationFailureMessage);", constructor);
            StringAssert.Contains("catch", constructor);
            StringAssert.Contains("if (_queue.IsCreated)", constructor);
            StringAssert.Contains("_queue.Dispose();", constructor);
            StringAssert.Contains("NativeMemoryTrackingBridge.Unregister(_sentinelRegistrationId);", constructor);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.Unregister(NativeMemoryOwner, TypedQueueLabel);", constructor);
            StringAssert.Contains("throw;", constructor);
            Assert.Less(
                constructor.IndexOf("if (_sentinelRegistrationId <= 0)", StringComparison.Ordinal),
                constructor.IndexOf("PrewarmQueue(ref _queue, capacity);", StringComparison.Ordinal));
            Assert.Less(
                constructor.IndexOf("_queue.Dispose();", StringComparison.Ordinal),
                constructor.IndexOf("NativeMemoryTrackingBridge.Unregister(_sentinelRegistrationId);", StringComparison.Ordinal));

            StringAssert.Contains("_queue.Dispose();", dispose);
            StringAssert.Contains("NativeMemoryTrackingBridge.Unregister(_sentinelRegistrationId);", dispose);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.Unregister(NativeMemoryOwner, TypedQueueLabel);", dispose);
            StringAssert.Contains("_queue = default;", dispose);
            StringAssert.Contains("if (!_queue.IsCreated && _sentinelRegistrationId > 0)", dispose);
            Assert.Less(
                dispose.IndexOf("_queue.Dispose();", StringComparison.Ordinal),
                dispose.IndexOf("NativeMemoryTrackingBridge.Unregister(_sentinelRegistrationId);", StringComparison.Ordinal));
        }

        [Test]
        public void TBDRFallbackNativeTracking_UsesRegistrationIdsForBridgeUnregister()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs");
            string vertexRegister = ExtractMethod(source, "private void RegisterNativeArrays()");
            string vertexDispose = ExtractMethod(source, "public void Dispose()");
            string vertexDeferredDispose = ExtractMethod(source, "public JobHandle Dispose(JobHandle dependency)");
            string vertexDisposeTracked = ExtractMethod(source, "private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array, ref int registrationId)");
            string vertexDeferredDisposeTracked = ExtractMethod(source, "private static JobHandle DisposeTrackedNativeArray<T>(");
            string vertexUnregister = ExtractMethod(source, "private static void UnregisterNativeArray(ref int registrationId)");
            string vertexRegisterNativeArray = ExtractMethod(source, "private static int RegisterNativeArrayOrThrow<T>");
            int trackerStart = source.IndexOf("public sealed class TBDRTextureStreamingTracker", StringComparison.Ordinal);
            Assert.GreaterOrEqual(trackerStart, 0);
            string tracker = source.Substring(trackerStart);
            string trackerConfigure = ExtractMethod(tracker, "public bool Configure(Texture2DArray targetArray");
            string trackerDispose = ExtractMethod(tracker, "public void Dispose()");
            string trackerDisposeTracked = ExtractMethod(tracker, "private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array, ref int registrationId)");
            string trackerRegister = ExtractMethod(tracker, "private static int RegisterNativeArrayOrThrow<T>");
            string trackerUnregister = ExtractMethod(tracker, "private static void UnregisterNativeArray(ref int registrationId)");

            StringAssert.Contains("private int _vertexBudgetCountersRegistrationId;", source);
            StringAssert.Contains("private int _tileWarningsRegistrationId;", source);
            StringAssert.Contains("private int _transparentQuadCountRegistrationId;", source);
            StringAssert.Contains("private int _telemetryRingRegistrationId;", source);
            StringAssert.Contains("_vertexBudgetCountersRegistrationId = RegisterNativeArrayOrThrow(VertexBudgetCounters, nameof(VertexBudgetCounters));", vertexRegister);
            StringAssert.Contains("_tileWarningsRegistrationId = RegisterNativeArrayOrThrow(TileWarnings, nameof(TileWarnings));", vertexRegister);
            StringAssert.Contains("_transparentQuadCountRegistrationId = RegisterNativeArrayOrThrow(TransparentQuadCount, nameof(TransparentQuadCount));", vertexRegister);
            StringAssert.Contains("_telemetryRingRegistrationId = RegisterNativeArrayOrThrow(TelemetryRing, nameof(TelemetryRing));", vertexRegister);
            StringAssert.Contains("DisposeTrackedNativeArray(ref VertexBudgetCounters, ref _vertexBudgetCountersRegistrationId);", vertexDispose);
            StringAssert.Contains("DisposeTrackedNativeArray(ref TileWarnings, ref _tileWarningsRegistrationId);", vertexDispose);
            StringAssert.Contains("DisposeTrackedNativeArray(ref TransparentQuadCount, ref _transparentQuadCountRegistrationId);", vertexDispose);
            StringAssert.Contains("DisposeTrackedNativeArray(ref TelemetryRing, ref _telemetryRingRegistrationId);", vertexDispose);
            StringAssert.Contains("DisposeTrackedNativeArray(ref VertexBudgetCounters, ref _vertexBudgetCountersRegistrationId, handle);", vertexDeferredDispose);
            StringAssert.Contains("DisposeTrackedNativeArray(ref TelemetryRing, ref _telemetryRingRegistrationId, handle);", vertexDeferredDispose);
            StringAssert.Contains("H8Memory.Release(ref array, NativeArrayOwnerSystem);", vertexDisposeTracked);
            StringAssert.Contains("if (array.IsCreated)", vertexDisposeTracked);
            StringAssert.Contains("return;", vertexDisposeTracked);
            StringAssert.Contains("UnregisterNativeArray(ref registrationId);", vertexDisposeTracked);
            Assert.Less(
                vertexDisposeTracked.IndexOf("H8Memory.Release(ref array, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                vertexDisposeTracked.IndexOf("UnregisterNativeArray(ref registrationId);", StringComparison.Ordinal));
            StringAssert.Contains("dependency = H8Memory.Release(ref array, dependency, NativeArrayOwnerSystem);", vertexDeferredDisposeTracked);
            StringAssert.Contains("if (array.IsCreated)", vertexDeferredDisposeTracked);
            StringAssert.Contains("DispatcherJobFence.TryComplete(ref dependency, forceComplete: true)", vertexDeferredDisposeTracked);
            StringAssert.Contains("return dependency;", vertexDeferredDisposeTracked);
            StringAssert.Contains("UnregisterNativeArray(ref registrationId);", vertexDeferredDisposeTracked);
            Assert.Less(
                vertexDeferredDisposeTracked.IndexOf("dependency = H8Memory.Release(ref array, dependency, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                vertexDeferredDisposeTracked.IndexOf("DispatcherJobFence.TryComplete(ref dependency, forceComplete: true)", StringComparison.Ordinal));
            Assert.Less(
                vertexDeferredDisposeTracked.IndexOf("DispatcherJobFence.TryComplete(ref dependency, forceComplete: true)", StringComparison.Ordinal),
                vertexDeferredDisposeTracked.IndexOf("UnregisterNativeArray(ref registrationId);", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemoryTrackingBridge.Unregister(registrationId);", vertexUnregister);
            StringAssert.Contains("NativeMemoryTrackingBridge.RegisterNativeArrayInstance(", vertexRegisterNativeArray);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.UnregisterNativeArray(VertexBudgetCounters", source);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.UnregisterNativeArray(TileWarnings", source);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.UnregisterNativeArray(TransparentQuadCount", source);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.UnregisterNativeArray(TelemetryRing", source);

            StringAssert.Contains("private int _sliceTableRegistrationId;", tracker);
            StringAssert.Contains("_sliceTableRegistrationId = RegisterNativeArrayOrThrow(SliceTable, nameof(SliceTable));", trackerConfigure);
            StringAssert.Contains("DisposeTrackedNativeArray(ref SliceTable, ref _sliceTableRegistrationId);", trackerDispose);
            StringAssert.Contains("_sliceTableRegistrationId = 0;", trackerDispose);
            StringAssert.Contains("H8Memory.Release(ref array, NativeArrayOwnerSystem);", trackerDisposeTracked);
            StringAssert.Contains("if (array.IsCreated)", trackerDisposeTracked);
            StringAssert.Contains("return;", trackerDisposeTracked);
            StringAssert.Contains("UnregisterNativeArray(ref registrationId);", trackerDisposeTracked);
            Assert.Less(
                trackerDisposeTracked.IndexOf("H8Memory.Release(ref array, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                trackerDisposeTracked.IndexOf("UnregisterNativeArray(ref registrationId);", StringComparison.Ordinal));
            StringAssert.Contains("NativeMemoryTrackingBridge.RegisterNativeArrayInstance(", trackerRegister);
            StringAssert.Contains("return registrationId;", trackerRegister);
            StringAssert.Contains("NativeMemoryTrackingBridge.Unregister(registrationId);", trackerUnregister);
            StringAssert.DoesNotContain("NativeMemoryTrackingBridge.UnregisterNativeArray(SliceTable", tracker);
        }

        [Test]
        public void ThreadSafeCommandQueue_NativeQueueTrackingSurvivesDisposeFailure()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string shutdown = ExtractMethod(source, "public static void Shutdown()");
            string disposeQueue = ExtractMethod(source, "private static void DisposeTrackedPersistentQueue<T>");
            string createQueue = ExtractMethod(source, "private static NativeQueue<T> CreateTrackedPersistentQueue<T>");
            string registerQueueInstance = ExtractMethod(sentinel, "public static int RegisterNativeQueueInstance<T>");

            StringAssert.Contains("return RegisterPointer(null, bytes, owner, label, lifetime, false);", registerQueueInstance);
            StringAssert.Contains("private static int _pendingCommandsSentinelId;", source);
            StringAssert.Contains("private static int _pendingStorageReservationCommitResolvedSentinelId;", source);
            StringAssert.Contains("DisposeTrackedPersistentQueue(ref _pendingCommands, ref _pendingCommandsSentinelId, ref _pendingCommandsReady);", shutdown);
            StringAssert.Contains("DisposeTrackedPersistentQueue(", shutdown);
            StringAssert.Contains("ref _pendingStorageReservationCommitResolved,", shutdown);
            StringAssert.Contains("ref _pendingStorageReservationCommitResolvedSentinelId,", shutdown);
            StringAssert.Contains("ref _pendingStorageReservationCommitResolvedReady);", shutdown);
            StringAssert.DoesNotContain("bool disposed = !", disposeQueue);
            StringAssert.Contains("queue.Dispose();", disposeQueue);
            StringAssert.Contains("queue = default;", disposeQueue);
            StringAssert.DoesNotContain("if (disposed &&", disposeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeQueue);
            StringAssert.Contains("sentinelId = 0;", disposeQueue);
            StringAssert.Contains("Volatile.Write(ref readyFlag, 0);", disposeQueue);
            StringAssert.Contains("finally", disposeQueue);
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeQueue.IndexOf("Volatile.Write(ref readyFlag, 0);", StringComparison.Ordinal),
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal));
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));

            StringAssert.Contains("out int sentinelId", createQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", createQueue);
            StringAssert.Contains("PrewarmQueue(ref queue, capacity);", createQueue);
            StringAssert.Contains("int cleanupReadyFlag = queue.IsCreated ? 1 : 0;", createQueue);
            StringAssert.Contains("DisposeTrackedPersistentQueue(ref queue, ref sentinelId, ref cleanupReadyFlag);", createQueue);
            StringAssert.DoesNotContain("queue.Dispose();", createQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(sentinelId);", createQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ThreadSafeCommandQueue), label);", createQueue);
            Assert.Less(
                createQueue.IndexOf("catch (Exception exception)", StringComparison.Ordinal),
                createQueue.IndexOf("DisposeTrackedPersistentQueue(ref queue, ref sentinelId, ref cleanupReadyFlag);", StringComparison.Ordinal));
        }

        [Test]
        public void ModCommandDispatcher_NativeContainerTrackingUsesStoredSentinelIds()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string initialize = ExtractMethod(source, "internal static void Initialize()");
            string shutdown = ExtractMethod(source, "internal static void Shutdown()");
            string registerQueue = ExtractMethod(source, "private static void RegisterQueue<TPayload>");
            string registerHashMap = ExtractMethod(source, "private static void RegisterHashMap<TValue>");
            string disposeQueue = ExtractMethod(source, "private static void DisposeQueue<TPayload>");
            string disposeHashMap = ExtractMethod(source, "private static void DisposeHashMap<TValue>");
            string registerHashMapInstance = ExtractMethod(sentinel, "public static int RegisterNativeHashMapInstance<TKey, TValue>");

            StringAssert.Contains("return RegisterPointer(null, bytes, owner, label, lifetime, false);", registerHashMapInstance);
            StringAssert.Contains("private static int _pendingCommandsSentinelId;", source);
            StringAssert.Contains("private static int _pendingAupCommandsSentinelId;", source);
            StringAssert.Contains("private static int _pendingRenderCommandsSentinelId;", source);
            StringAssert.Contains("private static int _pendingRaycastResultsSentinelId;", source);
            StringAssert.Contains("private static int _pendingRejectEventsSentinelId;", source);
            StringAssert.Contains("private static int _pendingMemoryEvictionEventsSentinelId;", source);
            StringAssert.Contains("private static int _pendingAupResponsesSentinelId;", source);
            StringAssert.Contains("private static int _modStatesByHashSentinelId;", source);
            StringAssert.Contains("private static int _modIndexByHashSentinelId;", source);
            StringAssert.Contains("private static int _kernelIndexByCommandKeySentinelId;", source);

            StringAssert.Contains("out _pendingCommandsSentinelId", initialize);
            StringAssert.Contains("out _modStatesByHashSentinelId", initialize);
            StringAssert.Contains("DisposeQueue(ref _pendingCommands, ref _pendingCommandsSentinelId);", shutdown);
            StringAssert.Contains("DisposeHashMap(ref _modStatesByHash, ref _modStatesByHashSentinelId);", shutdown);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeHashMapInstance(", registerHashMap);
            StringAssert.Contains("DisposeQueue(ref queue, ref sentinelId);", registerQueue);
            StringAssert.Contains("throw new AggregateException", registerQueue);
            StringAssert.Contains("DisposeHashMap(ref map, ref sentinelId);", registerHashMap);
            StringAssert.Contains("throw new AggregateException", registerHashMap);
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(sentinelId);", registerQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(sentinelId);", registerHashMap);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeHashMap);
            StringAssert.Contains("finally", disposeQueue);
            StringAssert.Contains("finally", disposeHashMap);
            StringAssert.DoesNotContain("if (disposed &&", disposeQueue);
            StringAssert.DoesNotContain("if (disposed &&", disposeHashMap);
            StringAssert.Contains("finally", disposeQueue);
            StringAssert.Contains("finally", disposeHashMap);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeHashMap(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ModCommandDispatcher)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher)", source);
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeHashMap.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeHashMap.IndexOf("map.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void ModResourceRegistry_NativeHashMapTrackingUsesStoredSentinelId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs");
            string initialize = ExtractMethod(source, "internal static void Initialize()");
            string shutdown = ExtractMethod(source, "internal static void Shutdown()");
            string disposeResourceIndexByHash = ExtractMethod(source, "private static void DisposeResourceIndexByHash()");

            StringAssert.Contains("private static int _resourceIndexByHashSentinelId;", source);
            StringAssert.Contains("_resourceIndexByHashSentinelId = NativeMemorySentinel.RegisterNativeHashMapInstance(", initialize);
            StringAssert.Contains("DisposeResourceIndexByHash();", initialize);
            StringAssert.Contains("DisposeResourceIndexByHash();", shutdown);
            StringAssert.Contains("_resourceIndexByHash.Dispose();", disposeResourceIndexByHash);
            StringAssert.DoesNotContain("bool disposed = !", disposeResourceIndexByHash);
            StringAssert.DoesNotContain("if (disposed &&", disposeResourceIndexByHash);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_resourceIndexByHashSentinelId);", disposeResourceIndexByHash);
            StringAssert.Contains("_resourceIndexByHashSentinelId = 0;", disposeResourceIndexByHash);
            StringAssert.Contains("finally", disposeResourceIndexByHash);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeHashMap(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModResourceRegistry)", source);
            Assert.Less(
                disposeResourceIndexByHash.IndexOf("NativeMemorySentinel.Unregister(_resourceIndexByHashSentinelId);", StringComparison.Ordinal),
                disposeResourceIndexByHash.IndexOf("_resourceIndexByHash.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void ModRegistryEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ModRegistryEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void ModEventProjectionBridge_NativeQueueTrackingUsesStoredSentinelId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs");
            string install = ExtractMethod(source, "public void Install()");
            string releaseNativeState = ExtractMethod(source, "private void ReleaseNativeState()");
            string disposeProjectedEvents = ExtractMethod(source, "private void DisposeProjectedEvents()");

            StringAssert.Contains("private int _projectedEventsSentinelId;", source);
            StringAssert.Contains("_projectedEventsSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", install);
            StringAssert.Contains("if (_projectedEventsSentinelId <= 0)", install);
            StringAssert.Contains("EnsureCullTelemetryStorage();", install);
            StringAssert.Contains("ReleaseNativeState();", install);
            StringAssert.Contains("DisposeProjectedEvents();", releaseNativeState);
            StringAssert.Contains("_projectedEvents.Dispose();", disposeProjectedEvents);
            StringAssert.DoesNotContain("bool disposed = !", disposeProjectedEvents);
            StringAssert.DoesNotContain("if (disposed &&", disposeProjectedEvents);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_projectedEventsSentinelId);", disposeProjectedEvents);
            StringAssert.Contains("_projectedEventsSentinelId = 0;", disposeProjectedEvents);
            StringAssert.Contains("finally", disposeProjectedEvents);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner", source);
            Assert.Less(
                disposeProjectedEvents.IndexOf("NativeMemorySentinel.Unregister(_projectedEventsSentinelId);", StringComparison.Ordinal),
                disposeProjectedEvents.IndexOf("_projectedEvents.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void LocalizationEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/LocalizationEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(LocalizationEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void AudioLogEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioLogEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void AtlasSignalEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(AtlasSignalEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void BiomeMatrixEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/BiomeMatrixDirector.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(BiomeMatrixEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void WeatherEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Environment/WeatherEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(WeatherEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void NarrativeEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/NarrativeEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(NarrativeEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void Atlas6Events_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(Atlas6Events)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void QuestEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(QuestEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void ScanEvents_NativeQueueTrackingUsesStoredSentinelIdsWithoutQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ScanEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("_nextFrameEvents.TryDequeue(out ScanEventPayload payload)", promote);
            StringAssert.Contains("_pendingEvents.Enqueue(payload);", promote);
            StringAssert.DoesNotContain("_pendingEvents = _nextFrameEvents;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ScanEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void CraftingEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/CraftingEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(CraftingEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void InventoryEvents_NativeContainerTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/InventoryEvents.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string registerNativeHashSet = ExtractMethod(source, "private static void RegisterNativeHashSet<T>");
            string releaseNativeState = ExtractMethod(source, "private static void ReleaseNativeState()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string releaseNativeHashSet = ExtractMethod(source, "private static void ReleaseNativeHashSet<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");
            string registerHashSetInstance = ExtractMethod(sentinel, "public static int RegisterNativeParallelHashSetInstance<TKey>");

            StringAssert.Contains("return RegisterPointer(null, bytes, owner, label, lifetime, false);", registerHashSetInstance);
            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("private static int _queuedEventKeysSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _queuedEventKeysSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("out int sentinelId", registerNativeHashSet);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeHashSet);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeParallelHashSetInstance(", registerNativeHashSet);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeState);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeState);
            StringAssert.Contains("ReleaseNativeHashSet(ref _queuedEventKeys, ref _queuedEventKeysSentinelId);", releaseNativeState);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("Exception firstException = null;", releaseNativeHashSet);
            StringAssert.Contains("hashSet.Dispose();", releaseNativeHashSet);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeHashSet);
            StringAssert.Contains("finally", releaseNativeHashSet);
            StringAssert.Contains("catch (Exception exception)", releaseNativeHashSet);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeHashSet);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeHashSet);
            StringAssert.Contains("catch (Exception exception)", releaseNativeHashSet);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeHashSet);
            StringAssert.Contains("sentinelId = 0;", releaseNativeHashSet);
            StringAssert.Contains("hashSet = default;", releaseNativeHashSet);
            StringAssert.Contains("throw firstException;", releaseNativeHashSet);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeParallelHashSet(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(InventoryEvents)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelHashSet(nameof(InventoryEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeHashSet.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeHashSet.IndexOf("hashSet.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void PDAEvents_NativeContainerTrackingUsesStoredIdsWithoutQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/PlayerPDA.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string registerNativeHashSet = ExtractMethod(source, "private static void RegisterNativeHashSet<T>");
            string releaseNativeState = ExtractMethod(source, "private static void ReleaseNativeState()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string releaseNativeHashSet = ExtractMethod(source, "private static void ReleaseNativeHashSet<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEvents()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("private static int _queuedEventKeysSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _queuedEventKeysSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("out int sentinelId", registerNativeHashSet);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeHashSet);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeParallelHashSetInstance(", registerNativeHashSet);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeState);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeState);
            StringAssert.Contains("ReleaseNativeHashSet(ref _queuedEventKeys, ref _queuedEventKeysSentinelId);", releaseNativeState);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("Exception firstException = null;", releaseNativeHashSet);
            StringAssert.Contains("hashSet.Dispose();", releaseNativeHashSet);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeHashSet);
            StringAssert.Contains("finally", releaseNativeHashSet);
            StringAssert.Contains("catch (Exception exception)", releaseNativeHashSet);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeHashSet);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeHashSet);
            StringAssert.Contains("catch (Exception exception)", releaseNativeHashSet);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeHashSet);
            StringAssert.Contains("sentinelId = 0;", releaseNativeHashSet);
            StringAssert.Contains("hashSet = default;", releaseNativeHashSet);
            StringAssert.Contains("throw firstException;", releaseNativeHashSet);
            StringAssert.Contains("_nextFrameEvents.TryDequeue(out PDAEventPayload payload)", promote);
            StringAssert.Contains("_pendingEvents.Enqueue(payload);", promote);
            StringAssert.DoesNotContain("_pendingEvents = _nextFrameEvents;", promote);
            StringAssert.DoesNotContain("sentinelIdSwap", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeParallelHashSet(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(PDAEvents)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelHashSet(nameof(PDAEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeHashSet.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeHashSet.IndexOf("hashSet.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void InteractionEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Interaction/InteractionEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(InteractionEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void PlayerSignalEvents_NativeQueueTrackingUsesStoredIdsWithoutQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEvents()");

            StringAssert.Contains("private static int _pendingTraumaHudSignalsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameTraumaHudSignalsSentinelId;", source);
            StringAssert.Contains("private static int _pendingInteractionSignalsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameInteractionSignalsSentinelId;", source);
            StringAssert.Contains("private static int _pendingToolDepletedSignalsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameToolDepletedSignalsSentinelId;", source);
            StringAssert.Contains("out _pendingTraumaHudSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameTraumaHudSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _pendingInteractionSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameInteractionSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _pendingToolDepletedSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameToolDepletedSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingTraumaHudSignals, ref _pendingTraumaHudSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameTraumaHudSignals, ref _nextFrameTraumaHudSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingInteractionSignals, ref _pendingInteractionSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameInteractionSignals, ref _nextFrameInteractionSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingToolDepletedSignals, ref _pendingToolDepletedSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameToolDepletedSignals, ref _nextFrameToolDepletedSignalsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("_nextFrameTraumaHudSignals.TryDequeue(out TraumaHudSignal signal)", promote);
            StringAssert.Contains("_pendingTraumaHudSignals.Enqueue(signal);", promote);
            StringAssert.Contains("_nextFrameInteractionSignals.TryDequeue(out PlayerInteractionStressSignal signal)", promote);
            StringAssert.Contains("_pendingInteractionSignals.Enqueue(signal);", promote);
            StringAssert.Contains("_nextFrameToolDepletedSignals.TryDequeue(out PlayerToolDepletedSignal signal)", promote);
            StringAssert.Contains("_pendingToolDepletedSignals.Enqueue(signal);", promote);
            StringAssert.DoesNotContain("_pendingTraumaHudSignals = _nextFrameTraumaHudSignals;", promote);
            StringAssert.DoesNotContain("_pendingInteractionSignals = _nextFrameInteractionSignals;", promote);
            StringAssert.DoesNotContain("_pendingToolDepletedSignals = _nextFrameToolDepletedSignals;", promote);
            StringAssert.DoesNotContain("sentinelIdSwap", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void AtmosphereEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/HectonAtmosphereManager.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameStatesIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingStatesSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameStatesSentinelId;", source);
            StringAssert.Contains("out _pendingStatesSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameStatesSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingStates, ref _pendingStatesSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameStates, ref _nextFrameStatesSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingStatesSentinelId;", promote);
            StringAssert.Contains("_pendingStatesSentinelId = _nextFrameStatesSentinelId;", promote);
            StringAssert.Contains("_nextFrameStatesSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(AtmosphereEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameStates = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingStatesSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void BaseAirlockEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/BaseAirlockEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseAirlockEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void ModuleStatusEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModuleStatusEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ModuleStatusEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void VehicleCommandSignalBus_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs");
            string resetStaticState = ExtractMethod(source, "private static void ResetStaticState()");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string disposeQueue = ExtractMethod(source, "private static void DisposeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameCommands()");

            StringAssert.Contains("private static int _pendingCommandsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameCommandsSentinelId;", source);
            StringAssert.Contains("out _pendingCommandsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameCommandsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("DisposeQueue(ref _pendingCommands, ref _pendingCommandsSentinelId);", resetStaticState);
            StringAssert.Contains("DisposeQueue(ref _nextFrameCommands, ref _nextFrameCommandsSentinelId);", resetStaticState);
            StringAssert.Contains("queue.Dispose();", disposeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeQueue);
            StringAssert.Contains("sentinelId = 0;", disposeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingCommandsSentinelId;", promote);
            StringAssert.Contains("_pendingCommandsSentinelId = _nextFrameCommandsSentinelId;", promote);
            StringAssert.Contains("_nextFrameCommandsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(VehicleCommandSignalBus)", source);
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameCommands = oldPending;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingCommandsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void SuitMeshUpdateEvents_NativeQueueTrackingUsesStoredIdsWithoutQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs");
            string resetStaticState = ExtractMethod(source, "private static void ResetStaticState()");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string disposeQueue = ExtractMethod(source, "private static void DisposeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameSignals()");

            StringAssert.Contains("private static int _pendingSignalsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameSignalsSentinelId;", source);
            StringAssert.Contains("out _pendingSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameSignalsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("DisposeQueue(ref _pendingSignals, ref _pendingSignalsSentinelId);", resetStaticState);
            StringAssert.Contains("DisposeQueue(ref _nextFrameSignals, ref _nextFrameSignalsSentinelId);", resetStaticState);
            StringAssert.Contains("queue.Dispose();", disposeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeQueue);
            StringAssert.Contains("sentinelId = 0;", disposeQueue);
            StringAssert.Contains("_nextFrameSignals.TryDequeue(out SuitMeshUpdateSignal signal)", promote);
            StringAssert.Contains("_pendingSignals.Enqueue(signal);", promote);
            StringAssert.DoesNotContain("_pendingSignals = _nextFrameSignals;", promote);
            StringAssert.DoesNotContain("sentinelIdSwap", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(SuitMeshUpdateEvents)", source);
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void PlayerExpressionEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerExpressionEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void HectonSubmarineOsEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonSubmarineOsEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void ElectrolysisAcousticEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SubmarineElectrolysisModule.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ElectrolysisAcousticEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void RandomEventEvents_NativeQueueTrackingUsesStoredIdsWithoutQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEvents()");

            StringAssert.Contains("private static int _pendingStartedSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameStartedSentinelId;", source);
            StringAssert.Contains("private static int _pendingEndedSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEndedSentinelId;", source);
            StringAssert.Contains("private static int _pendingSeismicShockwavesSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameSeismicShockwavesSentinelId;", source);
            StringAssert.Contains("out _pendingStartedSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameStartedSentinelId", ensureInitialized);
            StringAssert.Contains("out _pendingEndedSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEndedSentinelId", ensureInitialized);
            StringAssert.Contains("out _pendingSeismicShockwavesSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameSeismicShockwavesSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingStarted, ref _pendingStartedSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameStarted, ref _nextFrameStartedSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEnded, ref _pendingEndedSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEnded, ref _nextFrameEndedSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingSeismicShockwaves, ref _pendingSeismicShockwavesSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameSeismicShockwaves, ref _nextFrameSeismicShockwavesSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("_nextFrameStarted.TryDequeue(out RandomEventStartedPayload payload)", promote);
            StringAssert.Contains("_pendingStarted.Enqueue(payload);", promote);
            StringAssert.Contains("_nextFrameEnded.TryDequeue(out RandomEventType type)", promote);
            StringAssert.Contains("_pendingEnded.Enqueue(type);", promote);
            StringAssert.Contains("_nextFrameSeismicShockwaves.TryDequeue(out SeismicShockwaveEvent payload)", promote);
            StringAssert.Contains("_pendingSeismicShockwaves.Enqueue(payload);", promote);
            StringAssert.DoesNotContain("sentinelIdSwap", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void FirstHourEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(FirstHourEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void EndingEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/EndingSystem.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(EndingEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void EclipseGameplayEvents_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethod(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethod(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethod(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = 0;", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("finally", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(EclipseGameplayEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void GlobalRegistry_ServiceReboundQueueTrackingSurvivesDisposeFailure()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistry.cs");
            string shutdown = ExtractMethod(source, "internal static void DisposeServiceReboundQueuesForShutdown()");
            string disposeQueue = ExtractMethod(source, "private static void DisposeServiceReboundQueue");
            string createQueue = ExtractMethod(source, "private static NativeQueue<RegistryEventPayload> CreateServiceReboundQueue");

            StringAssert.Contains("private static int _pendingServiceReboundsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameServiceReboundsSentinelId;", source);
            StringAssert.Contains("DisposeServiceReboundQueue(ref _pendingServiceRebounds, ref _pendingServiceReboundsSentinelId);", shutdown);
            StringAssert.Contains("DisposeServiceReboundQueue(ref _nextFrameServiceRebounds, ref _nextFrameServiceReboundsSentinelId);", shutdown);
            StringAssert.DoesNotContain("bool disposed = !", disposeQueue);
            StringAssert.Contains("queue.Dispose();", disposeQueue);
            StringAssert.Contains("queue = default;", disposeQueue);
            StringAssert.DoesNotContain("if (disposed &&", disposeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeQueue);
            StringAssert.Contains("sentinelId = 0;", disposeQueue);
            StringAssert.Contains("finally", disposeQueue);
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));

            StringAssert.Contains("out int sentinelId", createQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", createQueue);
            StringAssert.Contains("catch (Exception exception)", createQueue);
            StringAssert.Contains("DisposeServiceReboundQueue(ref queue, ref sentinelId);", createQueue);
            StringAssert.Contains("throw new AggregateException", createQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(GlobalRegistry), label);", createQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.Unregister(sentinelId);", createQueue);
        }

        [Test]
        public void NativeRingBuffer_TrackingSurvivesBackingDisposeFailure()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/NativeRingBuffer.cs");
            string frameTimeWatchdog = ReadProjectFile("Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs");
            string globalTelemetryBus = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs");
            string registerBacking = ExtractMethod(source, "public void RegisterBackingArray");
            string dispose = ExtractMethod(source, "public unsafe void Dispose()");

            StringAssert.Contains("private int _sentinelId;", source);
            StringAssert.DoesNotContain("public void UnregisterBackingArray", source);
            StringAssert.Contains("if (_sentinelId > 0)", registerBacking);
            StringAssert.Contains("_sentinelId = sentinelId;", registerBacking);
            StringAssert.Contains("Dispose();", registerBacking);
            Assert.AreEqual(0, CountOccurrences(registerBacking, "NativeMemorySentinel.UnregisterNativeArray(_buffer);"));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_buffer);", dispose);
            StringAssert.Contains("int sentinelId = _sentinelId;", dispose);
            StringAssert.Contains("H8Memory.Release(ref _buffer, _ownerSystem);", dispose);
            StringAssert.Contains("if (_buffer.IsCreated)", dispose);
            StringAssert.Contains("return;", dispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", dispose);
            StringAssert.Contains("_sentinelId = 0;", dispose);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", dispose);
            Assert.AreEqual(0, CountOccurrences(dispose, "NativeMemorySentinel.UnregisterNativeArray(_buffer);"));
            Assert.Less(
                dispose.IndexOf("H8Memory.Release(ref _buffer, _ownerSystem);", StringComparison.Ordinal),
                dispose.IndexOf("if (_buffer.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("if (_buffer.IsCreated)", StringComparison.Ordinal),
                dispose.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal));

            StringAssert.DoesNotContain(".UnregisterBackingArray();", frameTimeWatchdog);
            StringAssert.DoesNotContain(".UnregisterBackingArray();", globalTelemetryBus);
        }

        [Test]
        public void SaveBinaryStorage_IndexedSectorStateDisposesArraysBeforeUnregister()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            int handleStart = source.IndexOf("internal ref struct IndexedSectorEntityStateWriteHandle", StringComparison.Ordinal);
            Assert.GreaterOrEqual(handleStart, 0);
            int handleEnd = source.IndexOf("private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray", handleStart, StringComparison.Ordinal);
            Assert.Greater(handleEnd, handleStart);
            string handle = source.Substring(handleStart, handleEnd - handleStart);
            string disposeTrackedNativeArray = ExtractMethod(source, "private static void DisposeTrackedNativeArrayByPointer<T>");

            StringAssert.DoesNotContain("UnregisterNativeMemorySentinel", handle);
            StringAssert.DoesNotContain("UnregisterArray<T>", handle);

            string disposeRegisteredArray = ExtractMethod(handle, "private static void DisposeRegisteredArray<T>");
            StringAssert.Contains("DisposeTrackedNativeArrayByPointer(ref array);", disposeRegisteredArray);
            StringAssert.Contains("array.Dispose();", disposeTrackedNativeArray);
            StringAssert.Contains("array = default;", disposeTrackedNativeArray);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", disposeTrackedNativeArray);
            Assert.Less(
                disposeTrackedNativeArray.IndexOf("NativeMemorySentinel.UnregisterPointer(trackedPointer);", StringComparison.Ordinal),
                disposeTrackedNativeArray.IndexOf("array.Dispose();", StringComparison.Ordinal));

            string dispose = ExtractMethod(handle, "internal void Dispose()");
            StringAssert.Contains("DisposeRegisteredArray(ref SourceStates);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref SortEntries);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref RadixScratch);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref SortedEntityStates);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref CompactStates);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref FileBytes);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref ResultLength);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref RadixCounts);", dispose);
            StringAssert.Contains("DisposeRegisteredArray(ref RadixOffsets);", dispose);
        }

        [Test]
        public void SaveBinaryStorage_AllocatorRollbackUsesTrackedNativeArrayDisposal()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            int handleStart = source.IndexOf("internal ref struct IndexedSectorEntityStateWriteHandle", StringComparison.Ordinal);
            Assert.GreaterOrEqual(handleStart, 0);
            int handleEnd = source.IndexOf("private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray", handleStart, StringComparison.Ordinal);
            Assert.Greater(handleEnd, handleStart);
            string handle = source.Substring(handleStart, handleEnd - handleStart);

            string readOnlyMappingAllocator = ExtractMethod(source, "private static NativeArray<byte> AllocateReadOnlyMappingBytes");
            string indexedSectorAllocator = ExtractMethod(handle, "internal static NativeArray<T> AllocateRegisteredArray<T>");
            string persistentScratchAllocator = ExtractMethod(source, "private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray<T>");

            StringAssert.Contains("catch (Exception exception)", readOnlyMappingAllocator);
            StringAssert.Contains("DisposeTrackedNativeArrayByPointer(ref bytes);", readOnlyMappingAllocator);
            StringAssert.Contains("throw new AggregateException(", readOnlyMappingAllocator);
            StringAssert.DoesNotContain("bytes.Dispose();", readOnlyMappingAllocator);

            StringAssert.Contains("catch (Exception exception)", indexedSectorAllocator);
            StringAssert.Contains("DisposeTrackedNativeArrayByPointer(ref array);", indexedSectorAllocator);
            StringAssert.Contains("throw new AggregateException(", indexedSectorAllocator);
            StringAssert.DoesNotContain("array.Dispose();", indexedSectorAllocator);

            StringAssert.Contains("catch (Exception exception)", persistentScratchAllocator);
            StringAssert.Contains("DisposeTrackedNativeArrayByPointer(ref array);", persistentScratchAllocator);
            StringAssert.Contains("throw new AggregateException(", persistentScratchAllocator);
            StringAssert.DoesNotContain("array.Dispose();", persistentScratchAllocator);
        }

        [Test]
        public void SaveManager_AllocatorRollbackUsesTrackedNativeArrayDisposal()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string transientAllocator = ExtractMethod(source, "private static NativeArray<T> CreateTransientNativeArray<T>");
            string persistentAllocator = ExtractMethod(source, "private static NativeArray<T> CreatePersistentNativeArray<T>");

            StringAssert.Contains("catch (Exception exception)", transientAllocator);
            StringAssert.Contains("DisposeNativeArrayBestEffort(ref array, ref cleanupException, sentinelLabel: sentinelLabel);", transientAllocator);
            StringAssert.Contains("throw new AggregateException(", transientAllocator);
            StringAssert.DoesNotContain("array.Dispose();", transientAllocator);

            StringAssert.Contains("catch (Exception exception)", persistentAllocator);
            StringAssert.Contains("DisposeNativeArrayBestEffort(ref array, ref cleanupException, sentinelLabel: sentinelLabel);", persistentAllocator);
            StringAssert.Contains("throw new AggregateException(", persistentAllocator);
            StringAssert.DoesNotContain("array.Dispose();", persistentAllocator);
        }

        [Test]
        public void SaveSmokeTesters_AllocatorRollbackUsesTrackedDisposal()
        {
            string omega = ReadProjectFile("Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs");
            string recovery = ReadProjectFile("Assets/_Project/Scripts/SaveRecoverySmokeTester.cs");
            string runtime = ReadProjectFile("Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs");

            string omegaArrayAllocator = ExtractMethod(omega, "private static NativeArray<T> AllocateTrackedTempJobArray<T>");
            string recoveryArrayAllocator = ExtractMethod(recovery, "private static NativeArray<T> AllocateTrackedTempArray<T>");
            string runtimeArrayAllocator = ExtractMethod(runtime, "private static NativeArray<T> AllocateTrackedTempJobArray<T>");
            string runtimeListAllocator = ExtractMethod(runtime, "private static NativeList<T> AllocateTrackedTempJobList<T>");

            StringAssert.Contains("DisposeTrackedTempJobArray(ref array);", omegaArrayAllocator);
            StringAssert.Contains("throw new AggregateException(", omegaArrayAllocator);
            StringAssert.DoesNotContain("array.Dispose();", omegaArrayAllocator);

            StringAssert.Contains("DisposeTrackedTempArray(ref array);", recoveryArrayAllocator);
            StringAssert.Contains("throw new AggregateException(", recoveryArrayAllocator);
            StringAssert.DoesNotContain("array.Dispose();", recoveryArrayAllocator);

            StringAssert.Contains("DisposeTrackedTempJobArray(ref array, ref sentinelId);", runtimeArrayAllocator);
            StringAssert.Contains("throw new AggregateException(", runtimeArrayAllocator);
            StringAssert.DoesNotContain("array.Dispose();", runtimeArrayAllocator);

            StringAssert.Contains("DisposeTrackedTempJobList(ref list, ref sentinelId);", runtimeListAllocator);
            StringAssert.Contains("throw new AggregateException(", runtimeListAllocator);
            StringAssert.DoesNotContain("list.Dispose();", runtimeListAllocator);
        }

        [Test]
        public void WalIntegrityFuzzerCore_AllocatorRollbackUsesTrackedDisposal()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs");
            string allocator = ExtractMethod(source, "private static NativeArray<T> AllocateTrackedArray<T>");

            StringAssert.Contains("catch (Exception exception)", allocator);
            StringAssert.Contains("DisposeTrackedArray(ref array);", allocator);
            StringAssert.Contains("throw new AggregateException(", allocator);
            StringAssert.DoesNotContain("array.Dispose();", allocator);
        }

        [Test]
        public void SaveBinaryStorage_TransientContainersUnregisterBeforeDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string createList = ExtractMethod(source, "private static NativeList<T> CreateRegisteredTransientNativeList<T>");
            string disposeList = ExtractMethod(source, "private static void DisposeRegisteredTransientNativeList<T>");
            string createHashMap = ExtractMethod(source, "private static NativeParallelHashMap<TKey, TValue> CreateRegisteredTransientNativeParallelHashMap<TKey, TValue>");
            string disposeHashMap = ExtractMethod(source, "private static void DisposeRegisteredTransientNativeParallelHashMap<TKey, TValue>");

            StringAssert.Contains("DisposeRegisteredTransientNativeList(ref list, ref registrationId, label, lifetime);", createList);
            StringAssert.Contains("Exception firstException = null;", disposeList);
            StringAssert.DoesNotContain("bool disposed = !", disposeList);
            StringAssert.Contains("list.Dispose();", disposeList);
            StringAssert.DoesNotContain("if (disposed &&", disposeList);
            StringAssert.Contains("NativeMemorySentinel.Unregister(registrationId);", disposeList);
            StringAssert.Contains("registrationId = 0;", disposeList);
            StringAssert.Contains("finally", disposeList);
            Assert.Less(
                disposeList.IndexOf("NativeMemorySentinel.Unregister(registrationId);", StringComparison.Ordinal),
                disposeList.IndexOf("list.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeList.IndexOf("NativeMemorySentinel.Unregister(registrationId);", StringComparison.Ordinal),
                disposeList.IndexOf("registrationId = 0;", StringComparison.Ordinal));

            StringAssert.Contains("DisposeRegisteredTransientNativeParallelHashMap(ref map, ref registrationId, label, lifetime);", createHashMap);
            StringAssert.Contains("Exception firstException = null;", disposeHashMap);
            StringAssert.DoesNotContain("bool disposed = !", disposeHashMap);
            StringAssert.Contains("map.Dispose();", disposeHashMap);
            StringAssert.DoesNotContain("if (disposed &&", disposeHashMap);
            StringAssert.Contains("NativeMemorySentinel.Unregister(registrationId);", disposeHashMap);
            StringAssert.Contains("registrationId = 0;", disposeHashMap);
            StringAssert.Contains("finally", disposeHashMap);
            Assert.Less(
                disposeHashMap.IndexOf("NativeMemorySentinel.Unregister(registrationId);", StringComparison.Ordinal),
                disposeHashMap.IndexOf("map.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeHashMap.IndexOf("NativeMemorySentinel.Unregister(registrationId);", StringComparison.Ordinal),
                disposeHashMap.IndexOf("registrationId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void MemoryBudgetUnregisterDoesNotPrecedeNearbyNativeRelease()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (!lines[i].Contains("MemoryBudgetTracker.Unregister", StringComparison.Ordinal))
                        continue;

                    if (HasNearbyBudgetedNativeReleaseAfter(lines, i))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void H8MemoryReleaseSentinelUnregisterHasPostReleaseCreatedGuard()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (!lines[i].Contains("H8Memory.Release", StringComparison.Ordinal))
                        continue;

                    int unregisterIndex = FindNearbyPostReleaseTrackingUnregister(lines, i);
                    if (unregisterIndex < 0)
                        continue;

                    if (!HasIsCreatedGuardBetween(lines, i + 1, unregisterIndex - 1))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void H8MemoryReleaseDoesNotBlindDefaultReleasedContainer()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (!TryGetH8MemoryReleaseStatement(lines, i, out string releaseStatement, out int releaseStatementEndIndex))
                        continue;

                    string releasedVariable = TryGetH8MemoryReleasedVariable(releaseStatement);
                    if (releasedVariable.Length == 0)
                        continue;

                    if (HasBlindDefaultAfterH8MemoryRelease(lines, releaseStatementEndIndex, releasedVariable))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void H8MemoryReleaseDoesNotOverwriteReleasedFieldWithoutCreatedGuard()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (!TryGetH8MemoryReleaseStatement(lines, i, out string releaseStatement, out int releaseStatementEndIndex))
                        continue;

                    string releasedVariable = TryGetH8MemoryReleasedVariable(releaseStatement);
                    if (!IsFieldLikeReleasedVariable(releasedVariable))
                        continue;

                    if (HasSameVariableAllocationOverwriteAfterH8MemoryRelease(lines, releaseStatementEndIndex, releasedVariable))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void StoredIdPointerlessNativeReleaseHelpersDisposeBeforeUnregister()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void ReleaseNativeQueue<T>",
                    "queue.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void ReleaseNativeHashSet<T>",
                    "hashSet.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeQueue<T>",
                    "queue.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeHashMap<TValue>",
                    "map.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeTrackedTempJobArray<T>",
                    "array.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeTrackedTempJobList<T>",
                    "list.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void ReleaseNativeBytes",
                    "bytes.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void ReleaseNativeList<T>",
                    "list.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeNativeList<T>",
                    "list.Dispose();",
                    path,
                    failures);
                FindStoredIdReleaseHelperOrderingFailures(
                    source,
                    "private static void DisposeNativeParallelMultiHashMap<TKey, TValue>",
                    "map.Dispose();",
                    path,
                    failures);
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void StoredIdPointerlessNativeReleaseBlocksUnregisterBeforeDispose()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkipSynchronousDisposeOrderingPath(path))
                    continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!IsStableIdNativeMemoryUnregister(lines[i]))
                        continue;

                    if (IsPointerBackedStableIdUnregister(lines[i]))
                        continue;

                    if (HasNearbyDisposeAfter(lines, i) && !HasNearbyDisposeBefore(lines, i))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void RuntimePointerlessNativeContainersUseInstanceTrackingApis()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();
            string[] bannedTokens =
            {
                "NativeMemorySentinel.RegisterNativeList(",
                "NativeMemorySentinel.UnregisterNativeList(",
                "NativeMemorySentinel.RefreshNativeList(",
                "NativeMemorySentinel.RegisterNativeQueue(",
                "NativeMemorySentinel.UnregisterNativeQueue(",
                "NativeMemorySentinel.RegisterNativeHashMap(",
                "NativeMemorySentinel.UnregisterNativeHashMap(",
                "NativeMemorySentinel.RefreshNativeHashMap(",
                "NativeMemorySentinel.RegisterUnsafeHashMap(",
                "NativeMemorySentinel.UnregisterUnsafeHashMap(",
                "NativeMemorySentinel.RefreshUnsafeHashMap(",
                "NativeMemorySentinel.RegisterNativeParallelHashMap(",
                "NativeMemorySentinel.UnregisterNativeParallelHashMap(",
                "NativeMemorySentinel.RefreshNativeParallelHashMap(",
                "NativeMemorySentinel.RegisterNativeParallelHashSet(",
                "NativeMemorySentinel.UnregisterNativeParallelHashSet(",
                "NativeMemorySentinel.RefreshNativeParallelHashSet(",
                "NativeMemorySentinel.RegisterNativeParallelMultiHashMap(",
                "NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(",
                "NativeMemorySentinel.RefreshNativeParallelMultiHashMap("
            };

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkipRuntimePointerlessInstanceTrackingPath(path))
                    continue;

                string source = File.ReadAllText(path);
                foreach (string bannedToken in bannedTokens)
                {
                    if (source.Contains(bannedToken, StringComparison.Ordinal))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ": " + bannedToken);
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void SaveSystemRuntimeSmokeTester_TempJobScratchUsesStoredSentinelIds()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs");
            string tryLoad = ExtractMethod(source, "private static bool TryLoadIndexedSubBlockFallback");
            string allocateArray = ExtractMethod(source, "private static NativeArray<T> AllocateTrackedTempJobArray<T>");
            string allocateList = ExtractMethod(source, "private static NativeList<T> AllocateTrackedTempJobList<T>");
            string disposeArray = ExtractMethod(source, "private static void DisposeTrackedTempJobArray<T>");
            string disposeList = ExtractMethod(source, "private static void DisposeTrackedTempJobList<T>");

            StringAssert.Contains("int requestedSectorsSentinelId = 0;", tryLoad);
            StringAssert.Contains("int restoredRecordsSentinelId = 0;", tryLoad);
            StringAssert.Contains("out requestedSectorsSentinelId", tryLoad);
            StringAssert.Contains("out restoredRecordsSentinelId", tryLoad);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref requestedSectors, ref requestedSectorsSentinelId);", tryLoad);
            StringAssert.Contains("DisposeTrackedTempJobList(ref restoredRecords, ref restoredRecordsSentinelId);", tryLoad);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeArray(", allocateArray);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeListInstance(", allocateList);
            StringAssert.Contains("out int sentinelId", allocateArray);
            StringAssert.Contains("out int sentinelId", allocateList);
            StringAssert.Contains("ref int sentinelId", disposeArray);
            StringAssert.Contains("ref int sentinelId", disposeList);
            AssertContainsInOrder(disposeArray, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;", "array.Dispose();");
            AssertContainsInOrder(disposeList, "NativeMemorySentinel.Unregister(sentinelId);", "sentinelId = 0;", "list.Dispose();");
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(", source);
        }

        [Test]
        public void RuntimeSynchronousPointerBasedDisposeDoesNotPrecedeSentinelUnregister()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkipSynchronousDisposeOrderingPath(path))
                    continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(".Dispose();", StringComparison.Ordinal))
                        continue;

                    if (HasPostDisposeSentinelUnregisterInSameBlock(lines, i))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void RuntimePointerBackedNativeArrayUnregisterDoesNotReadDisposedArrayCopies()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkipSynchronousDisposeOrderingPath(path))
                    continue;

                string source = File.ReadAllText(path);
                FindDisposedNativeArrayCopyUnregisterFailures(source, path, "trackedArray", failures);
                FindDisposedNativeArrayCopyUnregisterFailures(source, path, "trackedNativeArray", failures);
                FindDisposedNativeArrayCopyUnregisterFailures(source, path, "trackedBuffer", failures);
                FindDisposedNativeArrayCopyUnregisterFailures(source, path, "trackedBytes", failures);
                FindDisposedNativeArrayCopyUnregisterFailures(source, path, "trackedScratch", failures);
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void RuntimeDoubleBufferedNativeQueueSwapsCarrySentinelIds()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkipSynchronousDisposeOrderingPath(path))
                    continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("NativeQueue<", StringComparison.Ordinal) &&
                        trimmed.Contains(" swap = _pending", StringComparison.Ordinal) &&
                        !HasSentinelIdSwapNearQueueSwap(lines, i))
                    {
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                    }
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void MapMagicBridge_DoubleBufferedQueueSentinelIdsFollowPromotedBuffers()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/MapMagicBridge.cs");
            string biomePromote = ExtractMethod(source, "private static void PromoteNextFrameBiomeIdsIfFrontEmpty()");
            string tilePromote = ExtractMethod(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");
            int biomeReleaseStart = source.IndexOf("private static void ReleaseNativeQueue<T>", StringComparison.Ordinal);
            int tileReleaseStart = source.IndexOf("private static void ReleaseNativeQueue<T>", biomeReleaseStart + 1, StringComparison.Ordinal);
            string biomeRelease = ExtractBlockAt(source, biomeReleaseStart);
            string tileRelease = ExtractBlockAt(source, tileReleaseStart);

            StringAssert.Contains("private static int _pendingBiomeIdsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameBiomeIdsSentinelId;", source);
            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeQueueInstance(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(MapMagicBiomeEvents)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(MapMagicTerrainTileEvents)", source);
            StringAssert.Contains("Exception firstException = null;", biomeRelease);
            StringAssert.DoesNotContain("bool disposed = !", biomeRelease);
            StringAssert.Contains("queue.Dispose();", biomeRelease);
            StringAssert.DoesNotContain("disposed = true;", biomeRelease);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", biomeRelease);
            StringAssert.Contains("catch (Exception exception)", biomeRelease);
            StringAssert.DoesNotContain("if (disposed)", biomeRelease);
            StringAssert.DoesNotContain("if (disposed &&", biomeRelease);
            StringAssert.Contains("sentinelId = 0;", biomeRelease);
            StringAssert.Contains("queue = default;", biomeRelease);
            StringAssert.Contains("throw firstException;", biomeRelease);
            StringAssert.Contains("Exception firstException = null;", tileRelease);
            StringAssert.DoesNotContain("bool disposed = !", tileRelease);
            StringAssert.Contains("queue.Dispose();", tileRelease);
            StringAssert.DoesNotContain("disposed = true;", tileRelease);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", tileRelease);
            StringAssert.Contains("catch (Exception exception)", tileRelease);
            StringAssert.DoesNotContain("if (disposed)", tileRelease);
            StringAssert.DoesNotContain("if (disposed &&", tileRelease);
            StringAssert.Contains("sentinelId = 0;", tileRelease);
            StringAssert.Contains("queue = default;", tileRelease);
            StringAssert.Contains("throw firstException;", tileRelease);
            Assert.Less(
                biomeRelease.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                biomeRelease.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                biomeRelease.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                biomeRelease.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
            Assert.Less(
                tileRelease.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                tileRelease.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                tileRelease.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                tileRelease.IndexOf("sentinelId = 0;", StringComparison.Ordinal));

            AssertContainsInOrder(
                biomePromote,
                "NativeQueue<int> swap = _pendingBiomeIds;",
                "_pendingBiomeIds = _nextFrameBiomeIds;",
                "_nextFrameBiomeIds = swap;",
                "int sentinelIdSwap = _pendingBiomeIdsSentinelId;",
                "_pendingBiomeIdsSentinelId = _nextFrameBiomeIdsSentinelId;",
                "_nextFrameBiomeIdsSentinelId = sentinelIdSwap;",
                "_pendingBiomeIdCount = _nextFrameBiomeIdCount;");

            AssertContainsInOrder(
                tilePromote,
                "NativeQueue<MapMagicTerrainTileEventPayload> swap = _pendingEvents;",
                "_pendingEvents = _nextFrameEvents;",
                "_nextFrameEvents = swap;",
                "int sentinelIdSwap = _pendingEventsSentinelId;",
                "_pendingEventsSentinelId = _nextFrameEventsSentinelId;",
                "_nextFrameEventsSentinelId = sentinelIdSwap;",
                "_pendingEventCount = _nextFrameEventCount;");
        }

        [Test]
        public void VoxelChunkModifiedEvents_SingleQueueTrackingUsesStoredId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs");
            string ensureInitialized = ExtractMethod(source, "private static void EnsureInitialized()");
            string disposeAll = ExtractMethod(source, "private static void DisposeAll()");

            StringAssert.Contains("private static int _eventsSentinelId;", source);
            StringAssert.Contains("_eventsSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", ensureInitialized);
            StringAssert.Contains("NativeOwner", ensureInitialized);
            StringAssert.Contains("QueueLabel", ensureInitialized);
            StringAssert.Contains("_events.Dispose();", disposeAll);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_eventsSentinelId);", disposeAll);
            StringAssert.Contains("_eventsSentinelId = 0;", disposeAll);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(", source);
            Assert.Less(
                disposeAll.IndexOf("NativeMemorySentinel.Unregister(_eventsSentinelId);", StringComparison.Ordinal),
                disposeAll.IndexOf("_events.Dispose();", StringComparison.Ordinal));
        }

        [Test]
        public void JobFenceManager_BudgetAndSentinelResetWaitForReleaseSuccess()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/JobFenceManager.cs");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string deferredDispose = ExtractMethod(source, "public JobHandle Dispose(JobHandle inputDeps)");

            StringAssert.Contains("H8Memory.Release(ref Handles, NativeArrayOwnerSystem);", dispose);
            StringAssert.Contains("if (Handles.IsCreated)", dispose);
            StringAssert.Contains("return;", dispose);
            StringAssert.Contains("MemoryBudgetTracker.Unregister(BudgetOwner);", dispose);
            StringAssert.Contains("SentinelId = 0;", dispose);
            Assert.Less(
                dispose.IndexOf("H8Memory.Release(ref Handles, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                dispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal),
                dispose.IndexOf("MemoryBudgetTracker.Unregister(BudgetOwner);", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal),
                dispose.IndexOf("SentinelId = 0;", StringComparison.Ordinal));

            StringAssert.Contains("JobHandle disposeHandle = H8Memory.Release(ref Handles, inputDeps, NativeArrayOwnerSystem);", deferredDispose);
            StringAssert.Contains("if (Handles.IsCreated)", deferredDispose);
            StringAssert.Contains("return disposeHandle;", deferredDispose);
            StringAssert.Contains("MemoryBudgetTracker.Unregister(BudgetOwner);", deferredDispose);
            StringAssert.Contains("SentinelId = 0;", deferredDispose);
            Assert.Less(
                deferredDispose.IndexOf("JobHandle disposeHandle = H8Memory.Release(ref Handles, inputDeps, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                deferredDispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                deferredDispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal),
                deferredDispose.IndexOf("MemoryBudgetTracker.Unregister(BudgetOwner);", StringComparison.Ordinal));
            Assert.Less(
                deferredDispose.IndexOf("if (Handles.IsCreated)", StringComparison.Ordinal),
                deferredDispose.IndexOf("SentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void LocRegistry_BabelReleaseHelpersPreserveStateWhenH8ReleaseFails()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/LocRegistry.cs");
            string releaseState = ExtractMethod(source, "private static bool ReleaseBabelBufferState<T>");
            string abortStage = ExtractMethod(source, "private static void AbortBabelDictionaryStage()");
            string growUtf8 = ExtractMethod(source, "private static unsafe bool TryGrowUtf8ByteFallback");
            string disposeScratch = ExtractMethod(source, "private static unsafe void DisposeOverrideCsvScratch()");

            StringAssert.Contains("H8Memory.Release(ref buffer, SystemID.UI);", releaseState);
            StringAssert.Contains("if (buffer.IsCreated)", releaseState);
            StringAssert.Contains("return false;", releaseState);
            StringAssert.Contains("vaultBacked = false;", releaseState);
            Assert.Less(
                releaseState.IndexOf("H8Memory.Release(ref buffer, SystemID.UI);", StringComparison.Ordinal),
                releaseState.IndexOf("if (buffer.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                releaseState.IndexOf("if (buffer.IsCreated)", StringComparison.Ordinal),
                releaseState.IndexOf("vaultBacked = false;", StringComparison.Ordinal));

            StringAssert.Contains("H8Memory.Release(ref _stagedLocaleBytes, SystemID.UI);", abortStage);
            StringAssert.Contains("if (_stagedLocaleBytes.IsCreated)", abortStage);
            StringAssert.Contains("return;", abortStage);
            StringAssert.Contains("_stagedLocaleLocked = false;", abortStage);
            Assert.Less(
                abortStage.IndexOf("H8Memory.Release(ref _stagedLocaleBytes, SystemID.UI);", StringComparison.Ordinal),
                abortStage.IndexOf("if (_stagedLocaleBytes.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                abortStage.IndexOf("if (_stagedLocaleBytes.IsCreated)", StringComparison.Ordinal),
                abortStage.IndexOf("_stagedLocaleLocked = false;", StringComparison.Ordinal));

            StringAssert.Contains("H8Memory.Release(ref _utf8Bytes, SystemID.UI);", growUtf8);
            StringAssert.Contains("if (_utf8Bytes.IsCreated)", growUtf8);
            StringAssert.Contains("H8Memory.Release(ref grown, SystemID.UI);", growUtf8);
            StringAssert.Contains("return false;", growUtf8);
            StringAssert.Contains("_utf8Bytes = grown;", growUtf8);
            Assert.Less(
                growUtf8.IndexOf("H8Memory.Release(ref _utf8Bytes, SystemID.UI);", StringComparison.Ordinal),
                growUtf8.IndexOf("if (_utf8Bytes.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                growUtf8.IndexOf("if (_utf8Bytes.IsCreated)", StringComparison.Ordinal),
                growUtf8.IndexOf("_utf8Bytes = grown;", StringComparison.Ordinal));

            StringAssert.Contains("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_overrideCsvScratch);", disposeScratch);
            StringAssert.Contains("H8Memory.Release(ref _overrideCsvScratch, SystemID.UI);", disposeScratch);
            StringAssert.Contains("if (_overrideCsvScratch.IsCreated)", disposeScratch);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer);", disposeScratch);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray(trackedScratch);", disposeScratch);
            Assert.Less(
                disposeScratch.IndexOf("void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_overrideCsvScratch);", StringComparison.Ordinal),
                disposeScratch.IndexOf("H8Memory.Release(ref _overrideCsvScratch, SystemID.UI);", StringComparison.Ordinal));
            Assert.Less(
                disposeScratch.IndexOf("H8Memory.Release(ref _overrideCsvScratch, SystemID.UI);", StringComparison.Ordinal),
                disposeScratch.IndexOf("NativeMemorySentinel.UnregisterPointer(trackedPointer);", StringComparison.Ordinal));
        }

        [Test]
        public void GroundRadar_PendingJobClearsHandleOnlyAfterAllNativeArraysRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs");
            string release = ExtractMethod(source, "private static void ReleaseRadarPendingJob");

            StringAssert.Contains("H8Memory.Release(ref pending.Hits, SystemID.WorldStreaming);", release);
            StringAssert.Contains("H8Memory.Release(ref pending.SdfSnapshot, SystemID.WorldStreaming);", release);
            StringAssert.Contains("pending.Hits.IsCreated", release);
            StringAssert.Contains("pending.SdfSnapshot.IsCreated", release);
            StringAssert.Contains("return;", release);
            StringAssert.Contains("pending.Handle = default;", release);
            Assert.Less(
                release.IndexOf("H8Memory.Release(ref pending.SdfSnapshot, SystemID.WorldStreaming);", StringComparison.Ordinal),
                release.IndexOf("pending.Hits.IsCreated", StringComparison.Ordinal));
            Assert.Less(
                release.IndexOf("pending.Hits.IsCreated", StringComparison.Ordinal),
                release.IndexOf("pending.Handle = default;", StringComparison.Ordinal));
        }

        [Test]
        public void QuestStateManager_InitializeDoesNotOverwriteLiveNativeStateAfterFailedDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestStateManager.cs");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string initialize = ExtractMethod(source, "public bool Initialize");

            StringAssert.Contains("private bool HasLiveNativeState =>", source);
            StringAssert.Contains("_activatedQuestIndices.IsCreated", source);
            StringAssert.Contains("_completedQuestIndices.IsCreated", source);
            StringAssert.Contains("_nodes.IsCreated", source);
            StringAssert.Contains("_validPackedWordMasks.IsCreated", source);
            StringAssert.Contains("_prerequisites.IsCreated", source);
            StringAssert.Contains("_globalPrerequisites.IsCreated", source);

            StringAssert.Contains("H8Memory.Release(ref _globalPrerequisites, NativeArrayOwnerSystem);", dispose);
            StringAssert.Contains("if (HasLiveNativeState)", dispose);
            StringAssert.Contains("return;", dispose);
            StringAssert.Contains("_runtimeResults.Clear();", dispose);
            Assert.Less(
                dispose.IndexOf("H8Memory.Release(ref _globalPrerequisites, NativeArrayOwnerSystem);", StringComparison.Ordinal),
                dispose.IndexOf("if (HasLiveNativeState)", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("if (HasLiveNativeState)", StringComparison.Ordinal),
                dispose.IndexOf("_runtimeResults.Clear();", StringComparison.Ordinal));

            StringAssert.Contains("Dispose();", initialize);
            StringAssert.Contains("if (HasLiveNativeState)", initialize);
            StringAssert.Contains("return false;", initialize);
            StringAssert.Contains("_localizationManager = localizationManager;", initialize);
            Assert.Less(
                initialize.IndexOf("Dispose();", StringComparison.Ordinal),
                initialize.IndexOf("if (HasLiveNativeState)", StringComparison.Ordinal));
            Assert.Less(
                initialize.IndexOf("if (HasLiveNativeState)", StringComparison.Ordinal),
                initialize.IndexOf("_localizationManager = localizationManager;", StringComparison.Ordinal));
        }

        [Test]
        public void QuestStateManager_NativeListsUseStoredSentinelIdsAcrossLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestStateManager.cs");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string initialize = ExtractMethod(source, "public bool Initialize");
            string registerNativeList = ExtractMethod(source, "private static void RegisterNativeList<T>");
            string releaseNativeList = ExtractMethod(source, "private static void ReleaseNativeList<T>");

            StringAssert.Contains("private int _activatedQuestIndicesSentinelId;", source);
            StringAssert.Contains("private int _completedQuestIndicesSentinelId;", source);
            StringAssert.Contains(
                "RegisterNativeList(_activatedQuestIndices, nameof(_activatedQuestIndices), out _activatedQuestIndicesSentinelId);",
                initialize);
            StringAssert.Contains(
                "RegisterNativeList(_completedQuestIndices, nameof(_completedQuestIndices), out _completedQuestIndicesSentinelId);",
                initialize);
            StringAssert.Contains(
                "sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, NativeMemoryOwner, label, NativeMemoryLifetime);",
                registerNativeList);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(list", registerNativeList);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner", source);

            StringAssert.Contains("ReleaseNativeList(ref _activatedQuestIndices, ref _activatedQuestIndicesSentinelId);", dispose);
            StringAssert.Contains("ReleaseNativeList(ref _completedQuestIndices, ref _completedQuestIndicesSentinelId);", dispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeList);
            StringAssert.Contains("sentinelId = 0;", releaseNativeList);
            StringAssert.Contains("list.Dispose();", releaseNativeList);
            StringAssert.Contains("list = default;", releaseNativeList);
            Assert.Less(
                releaseNativeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeList.IndexOf("list.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeList.IndexOf("list = default;", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeList.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("ReleaseNativeList(ref _completedQuestIndices, ref _completedQuestIndicesSentinelId);", StringComparison.Ordinal),
                dispose.IndexOf("H8Memory.Release(ref _nodes, NativeArrayOwnerSystem);", StringComparison.Ordinal));
        }

        [Test]
        public void EncounterDirector_HeadlessNativeListUsesStoredSentinelIdAcrossConstructorAndDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/EncounterDirector.cs");
            string constructor = ExtractMethod(source, "internal EncounterDirector()");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string register = ExtractMethod(source, "private void RegisterNativeMemorySentinel()");

            StringAssert.Contains("private int _headlessEntitiesSentinelId;", source);
            StringAssert.Contains("try", constructor);
            StringAssert.Contains("RegisterNativeMemorySentinel();", constructor);
            StringAssert.Contains("catch", constructor);
            StringAssert.Contains("Dispose();", constructor);
            StringAssert.Contains("throw;", constructor);
            int registerCallIndex = constructor.LastIndexOf("RegisterNativeMemorySentinel();", StringComparison.Ordinal);
            int registrationCatchIndex = constructor.IndexOf("catch", registerCallIndex, StringComparison.Ordinal);
            Assert.Less(registerCallIndex, registrationCatchIndex);
            Assert.Less(
                constructor.IndexOf("Dispose();", registrationCatchIndex, StringComparison.Ordinal),
                constructor.IndexOf("throw;", registrationCatchIndex, StringComparison.Ordinal));

            StringAssert.Contains("_headlessEntitiesSentinelId = NativeMemorySentinel.RegisterNativeListInstance(", register);
            StringAssert.Contains("nameof(EncounterDirector)", register);
            StringAssert.Contains("nameof(_headlessEntities)", register);
            StringAssert.Contains("if (_headlessEntitiesSentinelId <= 0)", register);
            StringAssert.Contains("_headlessEntitiesSentinelId = 0;", register);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(_headlessEntities", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(nameof(EncounterDirector)", source);

            StringAssert.Contains("bool unregisterHeadlessEntities = _headlessEntitiesSentinelId > 0;", dispose);
            StringAssert.Contains("DisposeNativeList(ref _headlessEntities, ref disposeHandle, ref hasDependency);", dispose);
            StringAssert.Contains("if (hasDependency &&", dispose);
            StringAssert.Contains("!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", dispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_headlessEntitiesSentinelId);", dispose);
            StringAssert.Contains("_headlessEntitiesSentinelId = 0;", dispose);
            Assert.Less(
                dispose.IndexOf("DisposeNativeList(ref _headlessEntities, ref disposeHandle, ref hasDependency);", StringComparison.Ordinal),
                dispose.IndexOf("!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", StringComparison.Ordinal),
                dispose.IndexOf("NativeMemorySentinel.Unregister(_headlessEntitiesSentinelId);", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("NativeMemorySentinel.Unregister(_headlessEntitiesSentinelId);", StringComparison.Ordinal),
                dispose.IndexOf("_headlessEntitiesSentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void NativeMemorySentinel_RefreshNativeListInstanceUsesRegistrationId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string refreshListInstance = ExtractMethod(source, "public static void RefreshNativeListInstance<T>");
            string refreshPointerlessById = ExtractMethod(source, "private static void RefreshPointerlessBytes(int id, long bytes)");

            StringAssert.Contains("if (!list.IsCreated || id <= 0)", refreshListInstance);
            StringAssert.Contains("RefreshPointerlessBytes(id, bytes);", refreshListInstance);
            StringAssert.Contains("record.Id != id", refreshPointerlessById);
            StringAssert.Contains("record.Pointer != IntPtr.Zero", refreshPointerlessById);
            StringAssert.Contains("TrackPersistentReallocationFixed(", refreshPointerlessById);
            StringAssert.Contains("_trackedBytes += delta;", refreshPointerlessById);
            StringAssert.DoesNotContain("ComputeStableHash(owner)", refreshPointerlessById);
            StringAssert.DoesNotContain("FixedStringEquals(in record.Owner", refreshPointerlessById);
        }

        [Test]
        public void NativeMemorySentinel_RefreshNativeParallelMultiHashMapInstanceUsesRegistrationId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string registerMapInstance = ExtractMethod(source, "public static int RegisterNativeParallelMultiHashMapInstance<TKey, TValue>");
            string refreshMapInstance = ExtractMethod(source, "public static void RefreshNativeParallelMultiHashMapInstance<TKey, TValue>");

            StringAssert.Contains("return RegisterPointer(null, bytes, owner, label, lifetime, false);", registerMapInstance);
            StringAssert.Contains("if (!map.IsCreated || id <= 0)", refreshMapInstance);
            StringAssert.Contains("RefreshPointerlessBytes(id, EstimateNativeMultiHashMapBytes<TKey, TValue>(map.Capacity));", refreshMapInstance);
            StringAssert.DoesNotContain("RefreshPointerlessBytes(owner, label", refreshMapInstance);
        }

        [Test]
        public void ScatterWorkingMemory_NativeListsUseStoredSentinelIdsForRegisterRefreshAndDispose()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs");
            string registerAll = ExtractMethod(source, "private void RegisterNativeMemorySentinel()");
            string registerList = ExtractMethod(source, "private static void RegisterNativeList<T>");
            string disposeList = ExtractMethod(source, "private static void DisposeNativeList<T>");
            string registerMultiHashMap = ExtractMethod(source, "private static void RegisterNativeParallelMultiHashMap<TKey, TValue>");
            string disposeMultiHashMap = ExtractMethod(source, "private static void DisposeNativeParallelMultiHashMap<TKey, TValue>");

            StringAssert.Contains("private int _gridPlacementSpatialMetadataSentinelId;", source);
            StringAssert.Contains("private int _gridPlacementPositionBucketsSentinelId;", source);
            StringAssert.Contains("private int _gridPlacementMetadataBucketsSentinelId;", source);
            StringAssert.Contains("private int _candidateAcceptanceBatchInputsSentinelId;", source);
            StringAssert.Contains("private int _candidateAcceptanceBatchResultsSentinelId;", source);
            StringAssert.Contains("private int _candidateAcceptanceBatchPendingMetadataSentinelId;", source);
            StringAssert.Contains("private int _candidateAcceptanceBatchPendingPositionBucketsSentinelId;", source);
            StringAssert.Contains("private int _candidateAcceptanceBatchPendingMetadataBucketsSentinelId;", source);
            StringAssert.Contains("out _gridPlacementSpatialMetadataSentinelId", registerAll);
            StringAssert.Contains("out _gridPlacementPositionBucketsSentinelId", registerAll);
            StringAssert.Contains("out _gridPlacementMetadataBucketsSentinelId", registerAll);
            StringAssert.Contains("out _candidateAcceptanceBatchInputsSentinelId", registerAll);
            StringAssert.Contains("out _candidateAcceptanceBatchResultsSentinelId", registerAll);
            StringAssert.Contains("out _candidateAcceptanceBatchPendingMetadataSentinelId", registerAll);
            StringAssert.Contains("out _candidateAcceptanceBatchPendingPositionBucketsSentinelId", registerAll);
            StringAssert.Contains("out _candidateAcceptanceBatchPendingMetadataBucketsSentinelId", registerAll);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, NativeMemoryOwner, label, NativeMemoryLifetime);", registerList);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeParallelMultiHashMapInstance(map, NativeMemoryOwner, label, NativeMemoryLifetime);", registerMultiHashMap);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(list", registerList);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeParallelMultiHashMap(map", registerMultiHashMap);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner", source);

            StringAssert.Contains("NativeMemorySentinel.RefreshNativeListInstance(GridPlacementSpatialMetadata, _gridPlacementSpatialMetadataSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeListInstance(CandidateAcceptanceBatchInputs, _candidateAcceptanceBatchInputsSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeListInstance(CandidateAcceptanceBatchResults, _candidateAcceptanceBatchResultsSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeListInstance(CandidateAcceptanceBatchPendingMetadata, _candidateAcceptanceBatchPendingMetadataSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeParallelMultiHashMapInstance(GridPlacementPositionBuckets, _gridPlacementPositionBucketsSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeParallelMultiHashMapInstance(GridPlacementMetadataBuckets, _gridPlacementMetadataBucketsSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeParallelMultiHashMapInstance(CandidateAcceptanceBatchPendingPositionBuckets, _candidateAcceptanceBatchPendingPositionBucketsSentinelId);", source);
            StringAssert.Contains("NativeMemorySentinel.RefreshNativeParallelMultiHashMapInstance(CandidateAcceptanceBatchPendingMetadataBuckets, _candidateAcceptanceBatchPendingMetadataBucketsSentinelId);", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RefreshNativeList(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RefreshNativeParallelMultiHashMap(", source);

            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeList);
            StringAssert.Contains("sentinelId = 0;", disposeList);
            StringAssert.Contains("list.Dispose();", disposeList);
            Assert.Less(
                disposeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeList.IndexOf("list.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeList.IndexOf("list = default;", StringComparison.Ordinal));
            Assert.Less(
                disposeList.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeList.IndexOf("sentinelId = 0;", StringComparison.Ordinal));

            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", disposeMultiHashMap);
            StringAssert.Contains("sentinelId = 0;", disposeMultiHashMap);
            StringAssert.Contains("map.Dispose();", disposeMultiHashMap);
            Assert.Less(
                disposeMultiHashMap.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeMultiHashMap.IndexOf("map.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                disposeMultiHashMap.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeMultiHashMap.IndexOf("map = default;", StringComparison.Ordinal));
            Assert.Less(
                disposeMultiHashMap.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                disposeMultiHashMap.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void DestructibleOrganicManager_LootScratchUsesStoredNativeListSentinelId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/DestructibleOrganicManager.cs");
            string buildTemplateCaches = ExtractMethod(source, "private void BuildTemplateCaches()");

            StringAssert.Contains("int lootScratchSentinelId = 0;", buildTemplateCaches);
            StringAssert.Contains("lootScratchSentinelId = NativeMemorySentinel.RegisterNativeListInstance(", buildTemplateCaches);
            StringAssert.Contains("if (lootScratchSentinelId <= 0)", buildTemplateCaches);
            StringAssert.DoesNotContain("bool disposed = !", buildTemplateCaches);
            StringAssert.DoesNotContain("disposed = true;", buildTemplateCaches);
            StringAssert.DoesNotContain("if (disposed &&", buildTemplateCaches);
            StringAssert.Contains("NativeMemorySentinel.Unregister(lootScratchSentinelId);", buildTemplateCaches);
            StringAssert.Contains("lootScratchSentinelId = 0;", buildTemplateCaches);
            StringAssert.Contains("lootScratch.Dispose();", buildTemplateCaches);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", buildTemplateCaches);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, TemplateLootBuildScratchLabel)", buildTemplateCaches);
            Assert.Less(
                buildTemplateCaches.IndexOf("NativeMemorySentinel.Unregister(lootScratchSentinelId);", StringComparison.Ordinal),
                buildTemplateCaches.IndexOf("lootScratch.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                buildTemplateCaches.IndexOf("NativeMemorySentinel.Unregister(lootScratchSentinelId);", StringComparison.Ordinal),
                buildTemplateCaches.IndexOf("lootScratchSentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void PersistentWorldRegistry_IndexedSectorLoadUsesStoredTransientListSentinelId()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string loadSnapshot = ExtractMethod(source, "private bool TryLoadIndexedSectorRecordsSnapshot");
            string registerList = ExtractMethod(source, "private static void RegisterTrackedTransientNativeList<T>");

            StringAssert.Contains("int loadedSectorRecordsSentinelId = 0;", loadSnapshot);
            StringAssert.Contains("out loadedSectorRecordsSentinelId", loadSnapshot);
            StringAssert.Contains("NativeMemorySentinel.Unregister(loadedSectorRecordsSentinelId);", loadSnapshot);
            StringAssert.Contains("loadedSectorRecordsSentinelId = 0;", loadSnapshot);
            StringAssert.Contains("loadedSectorRecords.Dispose();", loadSnapshot);
            StringAssert.Contains("out int sentinelId", registerList);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, MemoryBudgetOwnerName, label, lifetime);", registerList);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(list", registerList);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, IndexedSectorPagingLoadedRecordsLabel)", loadSnapshot);
            Assert.Less(
                loadSnapshot.IndexOf("NativeMemorySentinel.Unregister(loadedSectorRecordsSentinelId);", StringComparison.Ordinal),
                loadSnapshot.IndexOf("loadedSectorRecords.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                loadSnapshot.IndexOf("NativeMemorySentinel.Unregister(loadedSectorRecordsSentinelId);", StringComparison.Ordinal),
                loadSnapshot.IndexOf("loadedSectorRecordsSentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void NativeTrackingUnregisterDoesNotPrecedeNearbyH8MemoryRelease()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (!IsPostH8MemoryReleaseTrackingUnregister(lines[i]))
                        continue;

                    if (HasNearbyH8MemoryReleaseAfter(lines, i))
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + (i + 1));
                }
            }

            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }

        private static int FindNearbyPostReleaseTrackingUnregister(string[] lines, int releaseLineIndex)
        {
            int end = Math.Min(releaseLineIndex + 8, lines.Length - 1);
            for (int i = releaseLineIndex + 1; i <= end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Contains("NativeMemorySentinel.UnregisterNativeArray", StringComparison.Ordinal) ||
                    trimmed.Contains("NativeMemoryTrackingBridge.Unregister", StringComparison.Ordinal))
                {
                    return i;
                }
                if (trimmed.StartsWith("}", StringComparison.Ordinal) ||
                    trimmed.StartsWith("else", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                    trimmed.StartsWith("finally", StringComparison.Ordinal))
                {
                    return -1;
                }
            }

            return -1;
        }

        private static bool TryGetH8MemoryReleaseStatement(string[] lines, int releaseLineIndex, out string releaseStatement, out int releaseStatementEndIndex)
        {
            releaseStatement = string.Empty;
            releaseStatementEndIndex = releaseLineIndex;
            if (!lines[releaseLineIndex].Contains("H8Memory.Release", StringComparison.Ordinal))
                return false;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int end = Math.Min(releaseLineIndex + 12, lines.Length - 1);
            for (int i = releaseLineIndex; i <= end; i++)
            {
                builder.AppendLine(lines[i]);
                if (lines[i].Contains(");", StringComparison.Ordinal))
                {
                    releaseStatement = builder.ToString();
                    releaseStatementEndIndex = i;
                    return true;
                }
            }

            releaseStatement = builder.ToString();
            return true;
        }

        private static string TryGetH8MemoryReleasedVariable(string statement)
        {
            int releaseIndex = statement.IndexOf("H8Memory.Release", StringComparison.Ordinal);
            if (releaseIndex < 0)
                return string.Empty;

            int refIndex = statement.IndexOf("ref ", releaseIndex, StringComparison.Ordinal);
            if (refIndex < 0)
                return string.Empty;

            int start = refIndex + 4;
            while (start < statement.Length && char.IsWhiteSpace(statement[start]))
                start++;

            int end = start;
            while (end < statement.Length)
            {
                char current = statement[end];
                if (current != '_' && current != '.' && !char.IsLetterOrDigit(current))
                    break;

                end++;
            }

            return end > start ? statement.Substring(start, end - start) : string.Empty;
        }

        private static bool HasBlindDefaultAfterH8MemoryRelease(string[] lines, int releaseLineIndex, string releasedVariable)
        {
            int end = Math.Min(releaseLineIndex + 10, lines.Length - 1);
            string defaultAssignment = releasedVariable + " = default;";
            for (int i = releaseLineIndex + 1; i <= end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (trimmed.Contains(".IsCreated", StringComparison.Ordinal))
                    return false;
                if (trimmed.Contains(defaultAssignment, StringComparison.Ordinal))
                    return true;
                if (trimmed.StartsWith("}", StringComparison.Ordinal) ||
                    trimmed.StartsWith("else", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                    trimmed.StartsWith("finally", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsFieldLikeReleasedVariable(string releasedVariable)
        {
            return releasedVariable.StartsWith("_", StringComparison.Ordinal) ||
                   releasedVariable.StartsWith("s_", StringComparison.Ordinal) ||
                   releasedVariable.Contains(".", StringComparison.Ordinal);
        }

        private static bool HasSameVariableAllocationOverwriteAfterH8MemoryRelease(string[] lines, int releaseLineIndex, string releasedVariable)
        {
            int end = Math.Min(releaseLineIndex + 16, lines.Length - 1);
            string spacedAssignment = releasedVariable + " = ";
            string compactAssignment = releasedVariable + "=";
            for (int i = releaseLineIndex + 1; i <= end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (trimmed.Contains(releasedVariable + ".IsCreated", StringComparison.Ordinal))
                    return false;
                if ((trimmed.StartsWith(spacedAssignment, StringComparison.Ordinal) ||
                     trimmed.StartsWith(compactAssignment, StringComparison.Ordinal)) &&
                    (trimmed.Contains("H8Memory.Allocate", StringComparison.Ordinal) ||
                     trimmed.Contains("new NativeArray", StringComparison.Ordinal)))
                {
                    return true;
                }
                if (trimmed.StartsWith("}", StringComparison.Ordinal) ||
                    trimmed.StartsWith("else", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                    trimmed.StartsWith("finally", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsPostH8MemoryReleaseTrackingUnregister(string line)
        {
            return line.Contains("NativeMemorySentinel.UnregisterNativeArray", StringComparison.Ordinal) ||
                   line.Contains("NativeMemoryTrackingBridge.Unregister", StringComparison.Ordinal);
        }

        private static bool HasNearbyH8MemoryReleaseAfter(string[] lines, int unregisterLineIndex)
        {
            int end = Math.Min(unregisterLineIndex + 8, lines.Length - 1);
            for (int i = unregisterLineIndex + 1; i <= end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (trimmed.Contains("H8Memory.Release", StringComparison.Ordinal))
                    return true;
                if (trimmed.StartsWith("}", StringComparison.Ordinal) ||
                    trimmed.StartsWith("else", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                    trimmed.StartsWith("finally", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool HasIsCreatedGuardBetween(string[] lines, int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (lines[i].Contains(".IsCreated", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasNearbyBudgetedNativeReleaseAfter(string[] lines, int budgetUnregisterLineIndex)
        {
            int end = Math.Min(budgetUnregisterLineIndex + 8, lines.Length - 1);
            for (int i = budgetUnregisterLineIndex + 1; i <= end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (trimmed.Contains("H8Memory.Release", StringComparison.Ordinal) ||
                    trimmed.Contains("ReleaseNativeVaultHandles", StringComparison.Ordinal) ||
                    trimmed.Contains("ReleaseHomeostasisVaultHandles", StringComparison.Ordinal) ||
                    trimmed.Contains("DisposeVaultBackedStorage", StringComparison.Ordinal) ||
                    trimmed.Contains("H8Memory.FreeRaw", StringComparison.Ordinal) ||
                    trimmed.Contains("TryFreeRaw", StringComparison.Ordinal) ||
                    trimmed.Contains("FreeRaw(", StringComparison.Ordinal) ||
                    trimmed.Contains(".Dispose(", StringComparison.Ordinal))
                {
                    return true;
                }
                if (trimmed.StartsWith("}", StringComparison.Ordinal) ||
                    trimmed.StartsWith("else", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                    trimmed.StartsWith("finally", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return false;
        }

        [Test]
        public void H8Memory_RegisterActiveJobCompletesIncomingHandlesOnFailure()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string registerActiveJob = ExtractMethod(h8Memory, "public static bool RegisterActiveJob");
            string completeOwnerJobHandle = ExtractMethod(h8Memory, "private static void TryCompleteOwnerJobHandle");

            StringAssert.Contains("public static bool RegisterActiveJob(SystemID owner, JobHandle handle)", h8Memory);
            StringAssert.Contains("TryCompleteOwnerJobHandle(ref handle);", registerActiveJob);
            StringAssert.Contains("TryCompleteOwnerJobHandle(ref combinedHandle);", registerActiveJob);
            StringAssert.Contains("CompleteOwnerJobs(owner);", registerActiveJob);
            StringAssert.Contains("return false;", registerActiveJob);
            StringAssert.Contains("DispatcherJobFence.TryComplete(ref ownerHandle, forceComplete: true);", completeOwnerJobHandle);
        }

        [Test]
        public void H8Memory_DeferredNativeArrayReleaseCompletesDisposeHandleWhenJobRegistrationFails()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string deferredRelease = ExtractMethod(h8Memory, "public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency, SystemID owner)");

            StringAssert.Contains("JobHandle disposeHandle = array.Dispose(dependency);", deferredRelease);
            StringAssert.Contains("if (!RegisterActiveJob(owner, disposeHandle))", deferredRelease);
            StringAssert.Contains("TryCompleteOwnerJobHandle(ref disposeHandle);", deferredRelease);
            StringAssert.Contains("array = default;", deferredRelease);
            StringAssert.Contains("return disposeHandle;", deferredRelease);
        }

        [Test]
        public void BurstCallbackQueue_SentinelRegistrationFailureRollsBackBeforeRethrow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/BurstCallback.cs");
            string constructor = ExtractMethod(source, "public BurstCallbackQueue(int expectedCapacity)");
            string dispose = ExtractMethod(source, "public void Dispose()");
            string disposeDeferred = ExtractMethod(source, "public JobHandle Dispose(JobHandle inputDeps)");
            string completeCounterDispose = ExtractMethod(source, "private void CompleteCounterDisposeBeforeSentinelUnregister");
            string completeEventQueueDispose = ExtractMethod(source, "private void CompleteEventQueueDisposeBeforeSentinelUnregister");

            StringAssert.Contains("private int _queueSentinelId;", source);
            StringAssert.Contains("_queueSentinelId = 0;", constructor);
            StringAssert.Contains("bool budgetRegistered = false;", constructor);
            StringAssert.Contains("_queueSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", constructor);
            StringAssert.Contains("if (_queueSentinelId <= 0)", constructor);
            StringAssert.Contains("throw new InvalidOperationException(\"NativeMemorySentinel rejected BurstCallbackQueue event queue registration.\");", constructor);
            StringAssert.Contains("if (!_counters.IsCreated)", constructor);
            StringAssert.Contains("throw new InvalidOperationException(\"BurstCallbackQueue counter allocation failed.\");", constructor);
            StringAssert.Contains("if (_counterSentinelId <= 0)", constructor);
            StringAssert.Contains("throw new InvalidOperationException(\"NativeMemorySentinel rejected BurstCallbackQueue counter registration.\");", constructor);
            StringAssert.Contains("budgetRegistered = true;", constructor);
            StringAssert.Contains("if (budgetRegistered && !_counters.IsCreated && !_events.IsCreated)", constructor);
            StringAssert.Contains("MemoryBudgetTracker.Unregister(BudgetOwner);", constructor);
            StringAssert.Contains("Hecton8.Core.Memory.H8Memory.Release(", constructor);
            StringAssert.Contains("ref _counters", constructor);
            StringAssert.Contains("if (!_counters.IsCreated && _counterSentinelId > 0)", constructor);
            StringAssert.Contains("if (_queueSentinelId > 0)", constructor);
            StringAssert.Contains("_events.Dispose();", constructor);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_queueSentinelId);", constructor);
            StringAssert.Contains("_queueSentinelId = 0;", constructor);
            StringAssert.Contains("throw;", constructor);
            Assert.Less(
                constructor.IndexOf("if (_counterSentinelId <= 0)", StringComparison.Ordinal),
                constructor.IndexOf("MemoryBudgetTracker.Register", StringComparison.Ordinal));
            Assert.Less(
                constructor.IndexOf("MemoryBudgetTracker.Register", StringComparison.Ordinal),
                constructor.IndexOf("Prewarm();", StringComparison.Ordinal));
            Assert.Less(
                constructor.IndexOf("Hecton8.Core.Memory.H8Memory.Release(", StringComparison.Ordinal),
                constructor.IndexOf("NativeMemorySentinel.Unregister(_counterSentinelId);", StringComparison.Ordinal));
            Assert.Less(
                constructor.IndexOf("NativeMemorySentinel.Unregister(_queueSentinelId);", StringComparison.Ordinal),
                constructor.IndexOf("_events.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                constructor.IndexOf("NativeMemorySentinel.Unregister(_queueSentinelId);", StringComparison.Ordinal),
                constructor.IndexOf("MemoryBudgetTracker.Unregister(BudgetOwner);", StringComparison.Ordinal));

            StringAssert.Contains("_events.Dispose();", dispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_queueSentinelId);", dispose);
            StringAssert.Contains("_queueSentinelId = 0;", dispose);
            StringAssert.Contains("Hecton8.Core.Memory.H8Memory.Release(", dispose);
            StringAssert.Contains("if (_counters.IsCreated)", dispose);
            StringAssert.Contains("return;", dispose);
            StringAssert.Contains("if (_counterSentinelId > 0)", dispose);
            StringAssert.Contains("else if (_counterSentinelId > 0)", dispose);
            StringAssert.Contains("MemoryBudgetTracker.Unregister(BudgetOwner);", dispose);
            Assert.Less(
                dispose.IndexOf("Hecton8.Core.Memory.H8Memory.Release(", StringComparison.Ordinal),
                dispose.IndexOf("if (_counters.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("if (_counters.IsCreated)", StringComparison.Ordinal),
                dispose.IndexOf("NativeMemorySentinel.Unregister(_counterSentinelId);", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("NativeMemorySentinel.Unregister(_queueSentinelId);", StringComparison.Ordinal),
                dispose.IndexOf("_events.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("NativeMemorySentinel.Unregister(_queueSentinelId);", StringComparison.Ordinal),
                dispose.IndexOf("MemoryBudgetTracker.Unregister(BudgetOwner);", StringComparison.Ordinal));

            StringAssert.Contains("JobHandle counterDisposeHandle = inputDeps;", disposeDeferred);
            StringAssert.Contains("counterDisposeHandle = Hecton8.Core.Memory.H8Memory.Release(", disposeDeferred);
            StringAssert.Contains("if (_counters.IsCreated)", disposeDeferred);
            StringAssert.Contains("return counterDisposeHandle;", disposeDeferred);
            StringAssert.Contains("if (_counterSentinelId > 0)", disposeDeferred);
            StringAssert.Contains("CompleteCounterDisposeBeforeSentinelUnregister(ref counterDisposeHandle);", disposeDeferred);
            StringAssert.Contains("else if (_counterSentinelId > 0)", disposeDeferred);
            StringAssert.Contains("JobHandle eventsDisposeHandle = inputDeps;", disposeDeferred);
            StringAssert.Contains("eventsDisposeHandle = _events.Dispose(inputDeps);", disposeDeferred);
            StringAssert.Contains("CompleteEventQueueDisposeBeforeSentinelUnregister(ref eventsDisposeHandle);", disposeDeferred);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_queueSentinelId);", disposeDeferred);
            StringAssert.Contains("_queueSentinelId = 0;", disposeDeferred);
            Assert.Less(
                disposeDeferred.IndexOf("counterDisposeHandle = Hecton8.Core.Memory.H8Memory.Release(", StringComparison.Ordinal),
                disposeDeferred.IndexOf("if (_counters.IsCreated)", StringComparison.Ordinal));
            Assert.Less(
                disposeDeferred.IndexOf("if (_counters.IsCreated)", StringComparison.Ordinal),
                disposeDeferred.IndexOf("CompleteCounterDisposeBeforeSentinelUnregister(ref counterDisposeHandle);", StringComparison.Ordinal));
            Assert.Less(
                disposeDeferred.IndexOf("CompleteCounterDisposeBeforeSentinelUnregister(ref counterDisposeHandle);", StringComparison.Ordinal),
                disposeDeferred.IndexOf("eventsDisposeHandle = _events.Dispose(inputDeps);", StringComparison.Ordinal));
            Assert.Less(
                disposeDeferred.IndexOf("eventsDisposeHandle = _events.Dispose(inputDeps);", StringComparison.Ordinal),
                disposeDeferred.IndexOf("CompleteEventQueueDisposeBeforeSentinelUnregister(ref eventsDisposeHandle);", StringComparison.Ordinal));
            StringAssert.Contains("disposeHandle.Complete();", completeCounterDispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_counterSentinelId);", completeCounterDispose);
            Assert.Less(
                completeCounterDispose.IndexOf("disposeHandle.Complete();", StringComparison.Ordinal),
                completeCounterDispose.IndexOf("NativeMemorySentinel.Unregister(_counterSentinelId);", StringComparison.Ordinal));
            StringAssert.Contains("disposeHandle.Complete();", completeEventQueueDispose);
            StringAssert.Contains("_events = default;", completeEventQueueDispose);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_queueSentinelId);", completeEventQueueDispose);
            Assert.Less(
                completeEventQueueDispose.IndexOf("disposeHandle.Complete();", StringComparison.Ordinal),
                completeEventQueueDispose.IndexOf("NativeMemorySentinel.Unregister(_queueSentinelId);", StringComparison.Ordinal));
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(BurstCallbackQueue), nameof(_events));", source);
        }

        [Test]
        public void GlobalDataVault_DeferredGrowth_ClearUsesCompareExchange()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string queueBlock = ExtractMethod(vault, "private void QueueDeferredArenaGrowth");
            string clearBlock = ExtractMethod(vault, "private void ClearDeferredArenaGrowthIfSatisfied");
            string processBlock = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");

            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes", queueBlock);
            StringAssert.Contains("if (observed >= requiredBytes)", queueBlock);
            StringAssert.Contains("CanSatisfyContiguousFreeBlock(observed)", clearBlock);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, 0L, observed)", clearBlock);
            StringAssert.Contains("HasActiveBurstLocks(0u)", processBlock);
            StringAssert.Contains("HasPinnedExternalViews()", processBlock);
        }

        [Test]
        public void GlobalDataVault_PublicPointerRoutes_CheckCompactionFence()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            AssertMethodChecksFence(vault, "public bool TryResolveHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryReadHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryReadOnlyHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryResolveSlice<T>");
            AssertMethodChecksFence(vault, "public bool TryAcquireWriteLock<T>");
            AssertMethodChecksFence(vault, "public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)");
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdMetadataReadFallsBackToMap()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string metadataRead = ExtractMethod(vault, "private bool TryReadFlatMetadata");

            StringAssert.Contains("(uint)key < (uint)_metadataByBufferId.Length", metadataRead);
            StringAssert.Contains("_metadata.TryGetValue(key, out meta)", metadataRead);
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdWriteLocksAvoidFlatArrayReject()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            Assert.IsFalse(acquire.Contains("(uint)key >= (uint)_metadataByBufferId.Length"));
            Assert.IsFalse(release.Contains("(uint)key >= (uint)_metadataByBufferId.Length"));
            Assert.IsFalse(queue.Contains("(uint)bufferKey >= (uint)_metadataByBufferId.Length"));
            StringAssert.Contains("WriteMetadata(key, in meta)", acquire);
            StringAssert.Contains("WriteMetadata(key, in meta)", release);
            StringAssert.Contains("TryReadFlatMetadata(bufferKey, out _)", queue);
        }

        [Test]
        public void GlobalDataVault_DeferredReleaseAcceptanceAndDeduplicationStayContractSafe()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string blockRelease = ExtractMethod(vault, "private bool ReleaseWriterBlockLock");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0)", blockRelease);
            StringAssert.Contains("return queuedRelease;", release);
            StringAssert.Contains("return queuedRelease;", blockRelease);
            StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", queue);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)", queue);
            StringAssert.Contains("finally", queue);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", queue);
            StringAssert.Contains("pending->Kind == DeferredReleaseKindWriter", queue);
            Assert.IsFalse(queue.Contains("pending->Kind == kind"));
            StringAssert.Contains("pending->LockOwnerSystemId == lockOwnerSystemId", queue);
        }

        [Test]
        public void GlobalDataVault_NewAllocationPublishAndRollbackStayUnderOneMutationGate()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string ensure = ExtractMethod(vault, "private bool TryEnsureVaultBuffer<T>");
            string publish = ExtractMethod(vault, "private bool TryAllocatePublishedBuffer<T>");

            StringAssert.Contains("TryAllocatePublishedBuffer<T>", ensure);
            StringAssert.Contains("TryEnterBlockMutationGate()", publish);
            StringAssert.Contains("TryAllocateBlockLocked(key, requiredBytes", publish);
            StringAssert.Contains("_buffers.TryAdd(key, pointer)", publish);
            StringAssert.Contains("SanitizeFinitePayload<T>(pointer, requiredLength)", publish);
            Assert.Less(
                publish.IndexOf("SanitizeFinitePayload<T>(pointer, requiredLength)", StringComparison.Ordinal),
                publish.IndexOf("_buffers.TryAdd(key, pointer)", StringComparison.Ordinal));
            StringAssert.Contains("TryAddMetadata(key, in meta)", publish);
            StringAssert.Contains("EnsureBufferKeyRegistered(key)", publish);
            StringAssert.Contains("MarkExternalViewLocked(key, meta.OffsetBytes)", publish);
            StringAssert.Contains("if (!success && blockAllocated)", publish);
            StringAssert.Contains("_allocatedBytes = _allocatedBytes > requiredBytes ? _allocatedBytes - requiredBytes : 0L", publish);
            StringAssert.Contains("_buffers.Remove(key)", publish);
            StringAssert.Contains("RemoveMetadata(key)", publish);
            StringAssert.Contains("RemoveBufferKey(key)", publish);
            StringAssert.Contains("FreeBlockLocked(blockIndex, clearPayload: true)", publish);
            StringAssert.Contains("finally", publish);
            StringAssert.Contains("ReleaseBlockMutationGate()", publish);
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdGenerationSurvivesRelease()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string initialize = ExtractMethod(vault, "public void Initialize");
            string addMetadata = ExtractMethod(vault, "private bool TryAddMetadata");
            string writeMetadata = ExtractMethod(vault, "private void WriteMetadata");
            string removeMetadata = ExtractMethod(vault, "private void RemoveMetadata");
            string resolveInitial = ExtractMethod(vault, "private uint ResolveInitialGenerationForAllocation");
            string readGeneration = ExtractMethod(vault, "private uint ReadMetadataGeneration");
            string writeGeneration = ExtractMethod(vault, "private void WriteMetadataGeneration");

            StringAssert.Contains("_metadataGenerationByBufferId = new UnsafeHashMap<int, uint>", initialize);
            StringAssert.Contains("WriteMetadataGeneration(key, stored.Version)", addMetadata);
            StringAssert.Contains("WriteMetadataGeneration(key, stored.Version)", writeMetadata);
            StringAssert.Contains("WriteMetadataGeneration(key, tombstoneGeneration)", removeMetadata);
            StringAssert.Contains("ReadMetadataGeneration(key)", resolveInitial);
            StringAssert.Contains("_metadataGenerationByBufferId.TryGetValue(key, out uint generation)", readGeneration);
            StringAssert.Contains("(uint)key < (uint)_metadataByBufferId.Length", writeGeneration);
        }

        [Test]
        public void GlobalDataVault_DeferredGrowthChecksBurstLocksOnce()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string processBlock = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");
            int first = processBlock.IndexOf("HasActiveBurstLocks(0u)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(first, 0);
            Assert.AreEqual(-1, processBlock.IndexOf("HasActiveBurstLocks(0u)", first + 1, StringComparison.Ordinal));
        }

        [Test]
        public void SystemDispatcher_DeferredArenaGrowthUsesCachedPostSimulationPath()
        {
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            string postSimulation = ExtractMethod(dispatcher, "private void RunMasterPostSimulationPhase");
            string processDeferred = ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation");

            Assert.Less(
                postSimulation.IndexOf("system.PostSimulationTick(in timing)", StringComparison.Ordinal),
                postSimulation.IndexOf("ProcessDeferredArenaGrowthPostSimulation()", StringComparison.Ordinal));
            StringAssert.Contains("IDataVault dataVault = _dataVault;", processDeferred);
            StringAssert.Contains("TryResolveCachedDataVault(out dataVault)", processDeferred);
            StringAssert.Contains("_dataVault = dataVault;", processDeferred);
            StringAssert.Contains("globalDataVault.ProcessDeferredArenaGrowth();", processDeferred);
            Assert.IsFalse(processDeferred.Contains("GlobalRegistry.Get"));
            Assert.IsFalse(processDeferred.Contains("GetComponent"));
        }

        [Test]
        public void GlobalDataVault_WriteLockMutationGatesStayFlatAndFinallyReleased()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            Assert.AreEqual(1, CountOccurrences(acquire, "TryEnterBlockMutationGate()"));
            StringAssert.Contains("finally", acquire);
            StringAssert.Contains("ReleaseBlockMutationGate();", acquire);
            Assert.IsFalse(acquire.Contains("TryEnterReleaseMutationGate()"));

            Assert.AreEqual(1, CountOccurrences(release, "TryEnterReleaseMutationGate()"));
            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("return queuedRelease;", release);
            StringAssert.Contains("finally", release);
            StringAssert.Contains("ReleaseBlockMutationGate();", release);
            Assert.IsFalse(release.Contains("TryEnterBlockMutationGate()"));

            StringAssert.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0", queue);
            Assert.IsFalse(queue.Contains("Thread.SpinWait"));
            Assert.IsFalse(queue.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate"));
            StringAssert.Contains("finally", queue);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", queue);
        }

        [Test]
        public void GlobalDataVault_WriteLockThreadSlotsPreventNestedWritersAndReleaseInFinally()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string initialize = ExtractMethod(vault, "public void Initialize");
            string dispose = ExtractMethod(vault, "public void Dispose");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string rollback = ExtractMethod(vault, "private bool RollbackWriterLockUnlocked");
            string drain = ExtractMethod(vault, "private bool DrainDeferredWriterReleaseLocked");
            string reserveSlot = ExtractMethod(vault, "private bool TryReserveThreadWriterSlot");
            string releaseSlot = ExtractMethod(vault, "private bool ReleaseThreadWriterSlotForLock");

            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 24)]", vault);
            StringAssert.Contains("private const int WriterThreadLockSlotCapacity = 128", vault);
            StringAssert.Contains("private NativeArray<VaultThreadWriteLockSlot> _writerThreadLockSlots;", vault);
            StringAssert.Contains("_writerThreadLockSlots = H8Memory.Allocate<VaultThreadWriteLockSlot>", initialize);
            StringAssert.Contains("_writerThreadLockSlots.IsCreated", initialize);
            StringAssert.Contains("H8Memory.Release(ref _writerThreadLockSlots, SystemID.CoreDataVault)", dispose);

            StringAssert.Contains("int writerThreadId = Thread.CurrentThread.ManagedThreadId;", acquire);
            StringAssert.Contains("writerSlotOffsetBytes = meta.OffsetBytes;", acquire);
            StringAssert.Contains("TryReserveThreadWriterSlot(writerThreadId, key, writerSlotOffsetBytes, (int)systemID)", acquire);
            StringAssert.Contains("bool releaseThreadWriterSlot = false;", acquire);
            Assert.Less(
                acquire.IndexOf("TryEnterBlockMutationGate()", StringComparison.Ordinal),
                acquire.IndexOf("TryReserveThreadWriterSlot(writerThreadId, key, writerSlotOffsetBytes, (int)systemID)", StringComparison.Ordinal));
            StringAssert.Contains("finally", acquire);
            StringAssert.Contains("if (releaseThreadWriterSlot)", acquire);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(key, writerSlotOffsetBytes, (int)systemID)", acquire);
            Assert.AreEqual(1, CountOccurrences(acquire, "TryReserveThreadWriterSlot("));

            StringAssert.Contains("state != WriterThreadLockSlotStateEmpty", reserveSlot);
            StringAssert.Contains("Volatile.Read(ref slot->ThreadId) == threadId", reserveSlot);
            StringAssert.Contains("slot->OffsetBytes = offsetBytes;", reserveSlot);
            StringAssert.Contains("Interlocked.CompareExchange(", reserveSlot);
            StringAssert.Contains("Volatile.Write(ref slot->State, WriterThreadLockSlotStateActive)", reserveSlot);
            StringAssert.Contains("Volatile.Read(ref slot->OffsetBytes) != offsetBytes", releaseSlot);
            StringAssert.Contains("slot->OffsetBytes = 0L;", releaseSlot);
            StringAssert.Contains("Volatile.Write(ref slot->State, WriterThreadLockSlotStateEmpty)", releaseSlot);

            StringAssert.Contains("ReleaseThreadWriterSlotForLock(key, meta.OffsetBytes, (int)systemID)", release);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(bufferKey, offsetBytes, systemID)", rollback);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner)", drain);
        }

        [Test]
        public void CoreMemoryHotPaths_DoNotResolveColdDependencies()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");

            AssertNoColdLookup(ExtractMethod(vault, "public bool TryAcquireWriteLock<T>"));
            AssertNoColdLookup(ExtractMethod(vault, "public bool ReleaseWriteLock<T>"));
            AssertNoColdLookup(ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth"));
            AssertNoColdLookup(ExtractMethod(vault, "private bool TryGrowArena("));
            AssertNoColdLookup(ExtractMethod(h8Memory, "private static int RegisterBlockDescriptorThreadSafe"));
            AssertNoColdLookup(ExtractMethod(h8Memory, "private static bool TryEnterBlockDescriptorMutationGate"));
            AssertNoColdLookup(ExtractMethod(sentinel, "private static int RegisterPointer("));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void RunMasterVisualSyncPhase"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void RunDispatcherLateFrame"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private static bool TryResolveCachedDataVault"));
        }

        [Test]
        public void GlobalDataVault_DeferredGrowthStateTransferIsInterlockedAndAllocationFree()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string queue = ExtractMethod(vault, "private void QueueDeferredArenaGrowth");
            string clear = ExtractMethod(vault, "private void ClearDeferredArenaGrowthIfSatisfied");
            string process = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            string bridge = ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation");

            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", queue);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, requiredBytes, observed)", queue);
            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", clear);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, 0L, observed)", clear);
            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", process);
            StringAssert.Contains("IDataVault dataVault = _dataVault;", bridge);
            AssertNoForbiddenManagedHotPathConstructs(queue);
            AssertNoForbiddenManagedHotPathConstructs(clear);
            AssertNoForbiddenManagedHotPathConstructs(process);
            AssertNoForbiddenManagedHotPathConstructs(bridge);
        }

        [Test]
        public void GlobalDataVault_AupAllocationLockUsesVolatilePublication()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string lockAllocations = ExtractMethod(vault, "public void LockAllocationsForAupShift");
            string unlockAllocations = ExtractMethod(vault, "public void UnlockAllocationsAfterAupShift");

            StringAssert.Contains("public bool IsAllocationLocked => Interlocked.Read(ref _allocationLock) != 0L;", vault);
            StringAssert.Contains("private long _allocationLock;", vault);
            Assert.IsFalse(vault.Contains("_lockedShiftFrameId"));
            Assert.IsFalse(vault.Contains("Volatile.Read(ref _allocationLock)"));
            StringAssert.Contains("private static long ResolveAllocationLockToken", vault);
            StringAssert.Contains("return shiftFrameId == 0u ? -1L : shiftFrameId;", vault);
            StringAssert.Contains("long lockToken = ResolveAllocationLockToken(shiftFrameId);", lockAllocations);
            Assert.Less(
                lockAllocations.IndexOf("long lockToken = ResolveAllocationLockToken(shiftFrameId);", StringComparison.Ordinal),
                lockAllocations.IndexOf("Interlocked.Exchange(ref _allocationLock, lockToken);", StringComparison.Ordinal));
            StringAssert.Contains("long observedLockToken = Interlocked.Read(ref _allocationLock);", unlockAllocations);
            StringAssert.Contains("if (observedLockToken == 0L)", unlockAllocations);
            StringAssert.Contains("Interlocked.Exchange(ref _allocationLock, 0L);", unlockAllocations);
            StringAssert.Contains("Interlocked.CompareExchange(ref _allocationLock, 0L, lockToken);", unlockAllocations);
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "private bool TryGrowArenaForBytes"));
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth"));
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "private bool TryRunLiveCompactionSlice"));
            Assert.AreEqual(0, CountOccurrences(vault, "_allocationLock != 0"));
            Assert.AreEqual(0, CountOccurrences(vault, "_allocationLock == 0"));
        }

        [Test]
        public void GlobalDataVault_ArenaGrowthPreflightsTailMetadataBeforeRawReallocate()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string growForBytes = ExtractMethod(vault, "private bool TryGrowArenaForBytes");
            string growArena = ExtractMethod(vault, "private bool TryGrowArena(");
            string prepare = ExtractMethod(vault, "private bool TryPrepareArenaGrowthTailMetadata");
            string extend = ExtractMethod(vault, "private bool ExtendFreeTail");
            string reserveSlot = ExtractMethod(h8Memory, "internal static bool TryReserveBlockDescriptorSlot");
            string reserveSlotNoLock = ExtractMethod(h8Memory, "private static bool TryReserveBlockDescriptorSlotNoLock");
            string releaseSlot = ExtractMethod(h8Memory, "internal static void ReleaseReservedBlockDescriptor");
            string commitSlot = ExtractMethod(h8Memory, "internal static bool TryCommitReservedBlockDescriptor");
            string registerThreadSafe = ExtractMethod(h8Memory, "private static int RegisterBlockDescriptorThreadSafe");
            string updateDescriptor = ExtractMethod(h8Memory, "internal static bool TryUpdateBlockDescriptor");

            Assert.IsFalse(growForBytes.Contains("_blocks[lastIndex].State != BlockStateFree && _blocks.Length >= _blocks.Capacity"));
            StringAssert.Contains("return TryGrowArena(desiredBytes)", growForBytes);
            Assert.Less(
                growArena.IndexOf("if (!TryPrepareArenaGrowthTailMetadata(out reservedTailH8BlockIndex))", StringComparison.Ordinal),
                growArena.IndexOf("H8Memory.ReallocateRaw", StringComparison.Ordinal));
            StringAssert.Contains("H8Memory.TryReserveBlockDescriptorSlot(out reservedTailH8BlockIndex)", prepare);
            StringAssert.Contains("H8Memory.ReleaseReservedBlockDescriptor(reservedTailH8BlockIndex)", growArena);
            StringAssert.Contains("if (!TryEnterBlockDescriptorMutationGate())", reserveSlot);
            StringAssert.Contains("ReleaseBlockDescriptorMutationGate();", reserveSlot);
            StringAssert.Contains("BlockDescriptor reservation = default;", reserveSlotNoLock);
            StringAssert.Contains("TryReserveReusableBlockDescriptorSlot(in reservation, out index)", reserveSlotNoLock);
            StringAssert.Contains("EnsureBlockDescriptorCapacity(newCapacity)", reserveSlotNoLock);
            StringAssert.Contains("descriptor.State != (byte)H8BlockState.Reserved", releaseSlot);
            StringAssert.Contains("current.State != (byte)H8BlockState.Reserved", commitSlot);
            StringAssert.Contains("if (committed.Generation < nextGeneration)", commitSlot);
            StringAssert.Contains("return RegisterBlockDescriptorNoInit(in descriptor)", registerThreadSafe);
            StringAssert.Contains("if (!TryEnterBlockDescriptorMutationGate())", updateDescriptor);
            Assert.IsFalse(h8Memory.Contains("Thread.SpinWait"));
            Assert.IsFalse(extend.Contains("new VaultArenaBlock"));
            StringAssert.Contains("VaultArenaBlock freeTail = default;", extend);
            StringAssert.Contains("H8Memory.TryCommitReservedBlockDescriptor(descriptorIndex, BuildDescriptor(in freeTail))", extend);
        }

        [Test]
        public void NativeMemorySentinel_FatalLeakAssertion_IdentifiesLeakedBuffer()
        {
            NativeMemorySentinel.ResetForSubsystemReload();
            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
            int id = 0;

            try
            {
                id = NativeMemorySentinel.RegisterPointer(
                    pointer,
                    64,
                    "Agent1414SentinelTest",
                    "MockLeakedBuffer",
                    NativeAllocationLifetime.Session);

                FatalMemoryLeakException exception = Assert.Throws<FatalMemoryLeakException>(
                    () => NativeMemorySentinel.AssertNoAllocationsAfterServiceShutdown("Agent1414Test"));

                StringAssert.Contains("MockLeakedBuffer", exception.Message);
                StringAssert.Contains("Agent1414SentinelTest", exception.Message);
            }
            finally
            {
                if (id > 0)
                    NativeMemorySentinel.Unregister(id);
                if (pointer != null)
                    UnsafeUtility.Free(pointer, Allocator.Persistent);
                NativeMemorySentinel.ResetForSubsystemReload();
            }
        }

        [Test]
        public void NativeMemorySentinel_RegisterPathUsesFixedStringStorage()
        {
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string register = ExtractMethod(sentinel, "private static int RegisterPointer(");
            string persistent = ExtractMethod(sentinel, "private static void TrackPersistentReallocation");
            string findPersistent = ExtractMethod(sentinel, "private static int FindPersistentReallocationRecord");

            Assert.IsFalse(register.Contains("new NativeAllocationRecord"));
            Assert.IsFalse(persistent.Contains("new PersistentReallocationRecord"));
            Assert.IsFalse(register.Contains("string.Equals(existing.Owner"));
            Assert.IsFalse(findPersistent.Contains("string.Equals(record.Owner"));
            Assert.IsFalse(sentinel.Contains("string StackTrace"));
            Assert.IsFalse(sentinel.Contains("CaptureStackTrace"));
            Assert.IsFalse(sentinel.Contains("StackTraceUtility.ExtractStackTrace"));
            Assert.IsFalse(sentinel.Contains("using Hecton.Localization;"));
            Assert.IsFalse(sentinel.Contains("LocHash."));
            Assert.IsFalse(sentinel.Contains("public string Owner"));
            Assert.IsFalse(sentinel.Contains("public string Label"));
            Assert.IsFalse(sentinel.Contains("public bool LeakReported;"));
            Assert.IsFalse(sentinel.Contains("public bool Reported;"));
            StringAssert.Contains("private const uint StableHashFnvOffset = 2166136261u;", sentinel);
            StringAssert.Contains("private const uint StableHashFnvPrime = 16777619u;", sentinel);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 312)]", sentinel);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 288)]", sentinel);
            StringAssert.Contains("[FieldOffset(0)] internal IntPtr Pointer", sentinel);
            StringAssert.Contains("[FieldOffset(16)] public FixedString128Bytes Owner", sentinel);
            StringAssert.Contains("[FieldOffset(144)] public FixedString128Bytes Label", sentinel);
            StringAssert.Contains("[FieldOffset(293)] private byte _leakReported", sentinel);
            StringAssert.Contains("[FieldOffset(280)] private byte _reported", sentinel);
            StringAssert.Contains("public FixedString128Bytes Owner", sentinel);
            StringAssert.Contains("public FixedString128Bytes Label", sentinel);
            StringAssert.Contains("NativeAllocationRecord record = default", register);
            StringAssert.Contains("PersistentReallocationRecord freshRecord = default", persistent);
            StringAssert.Contains("ToFixedString128(owner)", register);
            StringAssert.Contains("FixedStringEquals(in existing.Owner, in ownerFixed)", register);
            StringAssert.Contains("HashUtf16CodeUnit(ref hash, value[i]);", sentinel);
            StringAssert.Contains("HashUtf16CodeUnit(ref hash, (char)scalar);", sentinel);
            StringAssert.Contains("HashUtf16CodeUnit(ref hash, '\\uFFFD');", sentinel);
            StringAssert.Contains("AppendFixedString(builder, in record.Owner)", sentinel);
        }

        [Test]
        public void NativeMemorySentinel_DiagnosticRecordReadsUseMutationGate()
        {
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string snapshotCopy = ExtractMethod(sentinel, "internal static int CopySnapshotSources");
            string canCopySnapshotSource = ExtractMethod(sentinel, "private static bool CanCopySnapshotSource");
            string fatalMessage = ExtractMethod(sentinel, "private static string BuildFatalLeakMessage");

            StringAssert.Contains("EnterMutationGate();", snapshotCopy);
            StringAssert.Contains("finally", snapshotCopy);
            StringAssert.Contains("ExitMutationGate();", snapshotCopy);
            Assert.IsFalse(snapshotCopy.Contains("new NativeAllocationSnapshotSource"));
            StringAssert.Contains("NativeAllocationSnapshotSource snapshot = default;", snapshotCopy);
            StringAssert.Contains("record.Pointer == IntPtr.Zero", canCopySnapshotSource);
            StringAssert.Contains("record.Bytes <= 0L", canCopySnapshotSource);
            StringAssert.Contains("record.LeakReported", canCopySnapshotSource);
            StringAssert.Contains("NativeAllocationLifetime.TransientArena", canCopySnapshotSource);
            StringAssert.Contains("return false;", canCopySnapshotSource);

            StringAssert.Contains("EnterMutationGate();", fatalMessage);
            StringAssert.Contains("finally", fatalMessage);
            StringAssert.Contains("ExitMutationGate();", fatalMessage);
            StringAssert.Contains("builder.Append(_trackedBytes);", fatalMessage);
        }

        private static void AssertMethodChecksFence(string source, string signature)
        {
            string block = ExtractMethod(source, signature);
            Assert.That(
                block.Contains("Volatile.Read(ref _compactionFence)") ||
                block.Contains("TryResolveHandle(in baseHandle"),
                signature);
        }

        private static void AssertNoColdLookup(string block)
        {
            Assert.IsFalse(block.Contains("GlobalRegistry.Get<"));
            Assert.IsFalse(block.Contains("GetComponent<"));
            Assert.IsFalse(block.Contains("GetComponent("));
            Assert.IsFalse(block.Contains("TryGetComponent("));
        }

        private static void AssertNoForbiddenManagedHotPathConstructs(string block)
        {
            Assert.IsFalse(block.Contains("new "));
            Assert.IsFalse(block.Contains("string.Format"));
            Assert.IsFalse(block.Contains(".ToString("));
            Assert.IsFalse(block.Contains("foreach"));
            Assert.IsFalse(block.Contains(".Select("));
            Assert.IsFalse(block.Contains(".Where("));
            Assert.IsFalse(block.Contains(".ToArray("));
            Assert.IsFalse(block.Contains(".ToList("));
        }

        private static void AssertContainsInOrder(string value, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int next = value.IndexOf(token, index, StringComparison.Ordinal);
                Assert.GreaterOrEqual(next, 0, token);
                index = next + token.Length;
            }
        }

        private static void FindStoredIdReleaseHelperOrderingFailures(
            string source,
            string signature,
            string disposeCall,
            string path,
            List<string> failures)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int start = source.IndexOf(signature, searchIndex, StringComparison.Ordinal);
                if (start < 0)
                    return;

                string block = ExtractBlockAt(source, start);
                if (!block.Contains("ref int sentinelId"))
                {
                    searchIndex = start + Math.Max(1, block.Length);
                    continue;
                }

                int disposeIndex = block.IndexOf(disposeCall, StringComparison.Ordinal);
                int unregisterIndex = block.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal);
                if (unregisterIndex < 0 ||
                    disposeIndex < 0 ||
                    unregisterIndex < disposeIndex)
                {
                    failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + LineNumberAt(source, start));
                }

                searchIndex = start + Math.Max(1, block.Length);
            }
        }

        private static bool ShouldSkipSynchronousDisposeOrderingPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.Contains("/Editor/", StringComparison.Ordinal) ||
                   normalized.Contains("BakePipeline", StringComparison.Ordinal) ||
                   normalized.Contains("Baker", StringComparison.Ordinal);
        }

        private static bool ShouldSkipRuntimePointerlessInstanceTrackingPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.Contains("/Editor/", StringComparison.Ordinal) ||
                   normalized.Contains("BakePipeline", StringComparison.Ordinal) ||
                   normalized.Contains("Baker", StringComparison.Ordinal) ||
                   normalized.EndsWith("/Core/NativeMemorySentinel.cs", StringComparison.Ordinal) ||
                   normalized.Contains("/World/TOOL_", StringComparison.Ordinal);
        }

        private static bool HasPostDisposeSentinelUnregisterInSameBlock(string[] lines, int disposeLineIndex)
        {
            int disposeIndent = CountLeadingWhitespace(lines[disposeLineIndex]);
            int end = Math.Min(lines.Length, disposeLineIndex + 8);
            for (int i = disposeLineIndex + 1; i < end; i++)
            {
                string line = lines[i];
                if (CountLeadingWhitespace(line) < disposeIndent)
                    return false;

                if (line.Trim().Length == 0)
                    continue;

                if (line.Contains("NativeMemorySentinel.UnregisterNative", StringComparison.Ordinal) ||
                    line.Contains("NativeMemoryTrackingBridge.Unregister", StringComparison.Ordinal) ||
                    line.Contains("UnregisterNativeArray(ref registrationId)", StringComparison.Ordinal) ||
                    line.Contains("TryUnregisterTransientNativeArrayPayload", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void FindDisposedNativeArrayCopyUnregisterFailures(
            string source,
            string path,
            string variableName,
            List<string> failures)
        {
            string unregisterToken = "NativeMemorySentinel.UnregisterNativeArray(" + variableName + ")";
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int unregisterIndex = source.IndexOf(unregisterToken, searchIndex, StringComparison.Ordinal);
                if (unregisterIndex < 0)
                    return;

                int windowStart = Math.Max(0, unregisterIndex - 1000);
                string priorWindow = source.Substring(windowStart, unregisterIndex - windowStart);
                int assignmentIndex = priorWindow.LastIndexOf(variableName + " = ", StringComparison.Ordinal);
                if (assignmentIndex >= 0)
                {
                    string betweenAssignmentAndUnregister = priorWindow.Substring(assignmentIndex);
                    if (betweenAssignmentAndUnregister.Contains(".Dispose(", StringComparison.Ordinal) ||
                        betweenAssignmentAndUnregister.Contains("H8Memory.Release(ref ", StringComparison.Ordinal))
                    {
                        failures.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), path) + ":" + LineNumberAt(source, unregisterIndex));
                    }
                }

                searchIndex = unregisterIndex + unregisterToken.Length;
            }
        }

        private static bool IsStableIdNativeMemoryUnregister(string line)
        {
            return line.Contains("NativeMemorySentinel.Unregister(", StringComparison.Ordinal) &&
                   !line.Contains("NativeMemorySentinel.UnregisterNative", StringComparison.Ordinal);
        }

        private static bool IsPointerBackedStableIdUnregister(string line)
        {
            return line.Contains("CounterSentinelId", StringComparison.Ordinal) ||
                   line.Contains("ArraySentinelId", StringComparison.Ordinal) ||
                   line.Contains("BytesSentinelId", StringComparison.Ordinal) ||
                   line.Contains("BufferSentinelId", StringComparison.Ordinal) ||
                   line.Contains("Readback", StringComparison.Ordinal);
        }

        private static bool HasNearbyDisposeBefore(string[] lines, int lineIndex)
        {
            int unregisterIndent = CountLeadingWhitespace(lines[lineIndex]);
            int start = Math.Max(0, lineIndex - 12);
            for (int i = lineIndex - 1; i >= start; i--)
            {
                string line = lines[i];
                if (line.Trim().Length == 0)
                    continue;

                if (IsOutsideStableIdUnregisterBlock(line, unregisterIndent))
                    return false;

                if (IsSynchronousDisposeStatement(line))
                    return true;
            }

            return false;
        }

        private static bool HasNearbyDisposeAfter(string[] lines, int lineIndex)
        {
            int unregisterIndent = CountLeadingWhitespace(lines[lineIndex]);
            int end = Math.Min(lines.Length, lineIndex + 13);
            for (int i = lineIndex + 1; i < end; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0)
                    continue;

                if (IsOutsideStableIdUnregisterBlock(line, unregisterIndent))
                    return false;

                if (IsSynchronousDisposeStatement(line))
                    return true;
            }

            return false;
        }

        private static bool IsOutsideStableIdUnregisterBlock(string line, int unregisterIndent)
        {
            return CountLeadingWhitespace(line) < unregisterIndent &&
                   line.TrimStart().StartsWith("}", StringComparison.Ordinal);
        }

        private static bool IsSynchronousDisposeStatement(string line)
        {
            return line.Contains(".Dispose();", StringComparison.Ordinal);
        }

        private static bool HasSentinelIdSwapNearQueueSwap(string[] lines, int swapLineIndex)
        {
            int end = Math.Min(lines.Length, swapLineIndex + 10);
            for (int i = swapLineIndex + 1; i < end; i++)
            {
                if (lines[i].Contains("sentinelIdSwap", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int CountLeadingWhitespace(string value)
        {
            int count = 0;
            while (count < value.Length && char.IsWhiteSpace(value[count]))
                count++;

            return count;
        }

        private static int LineNumberAt(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(value, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += value.Length;
            }

            return count;
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);

            return ExtractBlockAt(source, start);
        }

        private static string ExtractBlockAt(string source, int start)
        {
            int brace = source.IndexOf((char)123, start);
            Assert.GreaterOrEqual(brace, 0, start.ToString());

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == (char)123)
                    depth++;
                else if (c == (char)125)
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail(start.ToString());
            return string.Empty;
        }
    }
}
