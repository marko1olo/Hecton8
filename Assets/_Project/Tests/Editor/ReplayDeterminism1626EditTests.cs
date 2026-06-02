using System;
using System.IO;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Physics.KCC;
using Hecton8.Physics.KCC.Editor;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed class ReplayDeterminism1626EditTests
    {
        private const string InputDispatcherPath = "Assets/_Project/Scripts/Core/InputDispatcher.cs";
        private const string KccSmokePath = "Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs";
        private const string ReplayValidatorPath = "Assets/_Project/Scripts/Physics/KCC/Editor/ReplayDeterminismValidator1626.cs";
        private const string GlobalDataVaultPath = "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs";
        private const string H8MemoryPath = "Assets/_Project/Scripts/Core/Memory/H8Memory.cs";

        [Test]
        public void ReplayDtosRemainUnmanagedExplicitAndEightByteAligned()
        {
            Assert.AreEqual(80, UnsafeUtility.SizeOf<ReplayFrameDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<ReplayFrameDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<MemoryStateTelemetryEntry>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<MemoryStateTelemetryEntry>());
            Assert.AreEqual("Hecton8.Core.Contracts", typeof(ReplayFrameDTO).Assembly.GetName().Name);
        }

        [Test]
        public void ReplayValidatorAcceptsCleanRecordedFrames()
        {
            Assert.IsTrue(ReplayDeterminismValidator1626.Run(false, out ReplayDeterminismValidation1626Summary summary));
            Assert.AreEqual(HydrodynamicKccRuntime.KccSmokeFailureNone, summary.ErrorFlags);
            Assert.AreEqual(0u, summary.FailureCode);
        }

        [Test]
        public void ReplayValidatorFlagsOneMicrometerAupDriftWithFailureCode12()
        {
            Assert.IsTrue(ReplayDeterminismValidator1626.Run(true, out ReplayDeterminismValidation1626Summary summary));
            Assert.AreEqual(HydrodynamicKccRuntime.ReplayDeterminismFailureDrift, summary.FailureCode);
            Assert.AreNotEqual(0u, summary.ErrorFlags & HydrodynamicKccRuntime.KccSmokeFailurePrecisionDrift);
            Assert.GreaterOrEqual(summary.MaxDriftMillimeters, 0.001f);
        }

        [Test]
        public void ReplayValidatorUsesNamedBufferIds()
        {
            string validator = File.ReadAllText(ReplayValidatorPath);
            StringAssert.Contains("BufferID.ShinobuInputReplayFrames", validator);
            StringAssert.Contains("BufferID.ShinobuInputReplayTelemetry", validator);
            StringAssert.Contains("BufferID.ShinobuInputReplayValidationResults", validator);
            StringAssert.Contains("if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))", validator);
            Assert.IsFalse(validator.Contains("(BufferID)718", StringComparison.Ordinal));
            Assert.IsFalse(validator.Contains("new ", StringComparison.Ordinal));
        }

        [Test]
        public void ReplayHotPathsDoNotUseSceneOrRegistryLookups()
        {
            string inputDispatcher = File.ReadAllText(InputDispatcherPath);
            string kccSmoke = File.ReadAllText(KccSmokePath);
            string publishBody = ExtractMethodBody(inputDispatcher, "PublishDeterministicInputState");
            string replayWriteBody = ExtractMethodBody(inputDispatcher, "WriteReplayFrameDto");
            string validatorBody = ExtractStructMethodBody(kccSmoke, "ValidateReplayDeterminismJob", "Execute");
            string replayTelemetryBody = ExtractMethodBody(kccSmoke, "WriteReplayTelemetry");

            AssertHotBodyClean(publishBody);
            AssertHotBodyClean(replayWriteBody);
            AssertHotBodyClean(validatorBody);
            AssertHotBodyClean(replayTelemetryBody);
            StringAssert.Contains("IsInputReplayRecordingActive()", replayWriteBody);
            Assert.IsFalse(replayWriteBody.Contains("new ", StringComparison.Ordinal));
            Assert.IsFalse(validatorBody.Contains("new ", StringComparison.Ordinal));
            Assert.IsFalse(replayTelemetryBody.Contains("new ", StringComparison.Ordinal));
            string resolveMoveAxisBody = ExtractMethodBodyFromSignature(inputDispatcher, "private static float3 ResolveReplayMoveAxis(");
            string sanitizeReplayBody = ExtractMethodBodyFromSignature(inputDispatcher, "private static float3 SanitizeReplayFloat3(");
            string resolveAupBody = ExtractMethodBodyFromSignature(inputDispatcher, "private static bool TryResolveReplayAup(");
            string canonicalFloatBody = ExtractMethodBodyFromSignature(inputDispatcher, "private static float CanonicalizeReplayFloat(");
            string canonicalDoubleBody = ExtractMethodBodyFromSignature(inputDispatcher, "private static double CanonicalizeReplayDouble(");
            Assert.IsFalse(resolveMoveAxisBody.Contains("new ", StringComparison.Ordinal));
            Assert.IsFalse(sanitizeReplayBody.Contains("new ", StringComparison.Ordinal));
            Assert.IsFalse(resolveAupBody.Contains("new ", StringComparison.Ordinal));
            StringAssert.Contains("CanonicalizeReplayFloat(value.x)", sanitizeReplayBody);
            StringAssert.Contains("CanonicalizeReplayFloat(value.y)", sanitizeReplayBody);
            StringAssert.Contains("CanonicalizeReplayFloat(value.z)", sanitizeReplayBody);
            StringAssert.Contains("CanonicalizeReplayDouble(candidate.x)", resolveAupBody);
            StringAssert.Contains("CanonicalizeReplayDouble(candidate.y)", resolveAupBody);
            StringAssert.Contains("CanonicalizeReplayDouble(candidate.z)", resolveAupBody);
            StringAssert.Contains("math.isfinite(value)", canonicalFloatBody);
            StringAssert.Contains("value != 0f", canonicalFloatBody);
            StringAssert.Contains("math.isfinite(value)", canonicalDoubleBody);
            StringAssert.Contains("value != 0d", canonicalDoubleBody);
        }

        [Test]
        public void ReplaySnapshotStagingUsesCasGateAndTryFinally()
        {
            string inputDispatcher = File.ReadAllText(InputDispatcherPath);
            string stageBody = ExtractMethodBody(inputDispatcher, "StageInputReplaySnapshot");
            Assert.AreEqual(1, CountToken(stageBody, "TryAcquireInputMutationGuard()"));
            Assert.AreEqual(1, CountToken(stageBody, "ReleaseInputMutationGuard();"));
            StringAssert.Contains("try", stageBody);
            StringAssert.Contains("finally", stageBody);
            StringAssert.Contains("TryAcquireInputReplaySnapshotGate()", stageBody);
            StringAssert.Contains("ReleaseInputReplaySnapshotGate()", stageBody);

            string writerBody = ExtractMethodBody(inputDispatcher, "InputReplayWriterLoop");
            StringAssert.Contains("TryAcquireInputReplaySnapshotGate()", writerBody);
            StringAssert.Contains("ReleaseInputReplaySnapshotGate()", writerBody);
            StringAssert.Contains("accessor?.Flush()", writerBody);
            StringAssert.Contains("finally", writerBody);
            int writerAcquire = writerBody.IndexOf("TryAcquireInputReplaySnapshotGate()", StringComparison.Ordinal);
            int writerFlush = writerBody.IndexOf("accessor?.Flush()", StringComparison.Ordinal);
            int writerRelease = writerBody.IndexOf("ReleaseInputReplaySnapshotGate()", StringComparison.Ordinal);
            Assert.Greater(writerFlush, writerAcquire);
            Assert.Greater(writerRelease, writerFlush);
            Assert.IsFalse(stageBody.Contains("Monitor.Enter(", StringComparison.Ordinal));
            Assert.IsFalse(writerBody.Contains("Monitor.Enter(", StringComparison.Ordinal));
            Assert.IsFalse(writerBody.Contains("lock (_inputReplayGate)", StringComparison.Ordinal));

            string releaseMapBody = ExtractMethodBody(inputDispatcher, "ReleaseInputReplayMap");
            StringAssert.Contains("Volatile.Write(ref _inputReplaySnapshotGate, 0)", releaseMapBody);
            StringAssert.Contains("if (_inputReplayPointer != null)", releaseMapBody);
            StringAssert.Contains("if (_inputReplayAccessor != null)", releaseMapBody);
            StringAssert.Contains("_inputReplayPointer = null", releaseMapBody);
        }

        [Test]
        public void ReplayIngressRunsBeforeSimulationAndVisualSyncStaysLateFrame()
        {
            string inputDispatcher = File.ReadAllText(InputDispatcherPath);
            string preSimulationBody = ExtractMethodBody(inputDispatcher, "PreSimulationInputTick");
            string lateFrameBody = ExtractMethodBody(inputDispatcher, "LateFrameTick");

            StringAssert.Contains("PublishDeterministicInputState(_standardInputFrame++)", preSimulationBody);
            Assert.IsFalse(preSimulationBody.Contains("UpdateVisualLookInterpolation(", StringComparison.Ordinal));
            StringAssert.Contains("UpdateVisualLookInterpolation()", lateFrameBody);
            Assert.IsFalse(lateFrameBody.Contains("PublishDeterministicInputState(", StringComparison.Ordinal));
        }

        [Test]
        public void DataVaultWriterLockRejectsNestedSameThreadOwnershipAndReleasesThroughFinally()
        {
            string vault = File.ReadAllText(GlobalDataVaultPath);
            string acquireBody = ExtractMethodBodyFromSignature(vault, "public bool TryAcquireWriteLock<T>(");
            string releaseBody = ExtractMethodBodyFromSignature(vault, "public bool ReleaseWriteLock<T>(");
            string threadSlotBody = ExtractMethodBody(vault, "TryReserveThreadWriterSlot");

            StringAssert.Contains("Thread.CurrentThread.ManagedThreadId", acquireBody);
            StringAssert.Contains("TryReserveThreadWriterSlot", acquireBody);
            StringAssert.Contains("releaseThreadWriterSlot", acquireBody);
            StringAssert.Contains("finally", acquireBody);
            StringAssert.Contains("catch", acquireBody);
            StringAssert.Contains("writerLockCommitted = true", acquireBody);
            StringAssert.Contains("RollbackWriterLockUnlocked(key, writerSlotOffsetBytes, activeLockBit, (int)systemID)", acquireBody);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock", acquireBody);
            StringAssert.Contains("Volatile.Read(ref slot->ThreadId) == threadId", threadSlotBody);
            StringAssert.Contains("RecordLockContentionFault(bufferKey)", threadSlotBody);
            StringAssert.Contains("try", releaseBody);
            StringAssert.Contains("finally", releaseBody);
            StringAssert.Contains("ReleaseBlockMutationGate()", releaseBody);
        }

        [Test]
        public void DataVaultBufferPinLockRollsBackOnExceptionBeforeGateRelease()
        {
            string vault = File.ReadAllText(GlobalDataVaultPath);
            string lockBody = ExtractMethodBodyFromSignature(vault, "public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)");

            StringAssert.Contains("bool pinLockCommitted = false", lockBody);
            StringAssert.Contains("SystemID committedPreviousAliasRequester = SystemID.Unknown", lockBody);
            StringAssert.Contains("committedPreviousAliasRequester = previousAliasRequester", lockBody);
            StringAssert.Contains("pinLockCommitted = true", lockBody);
            StringAssert.Contains("catch", lockBody);
            StringAssert.Contains("RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, committedPreviousAliasRequester)", lockBody);
            StringAssert.Contains("finally", lockBody);
            StringAssert.Contains("ReleaseBlockMutationGate()", lockBody);
            Assert.Greater(lockBody.IndexOf("catch", StringComparison.Ordinal), lockBody.IndexOf("pinLockCommitted = true", StringComparison.Ordinal));
            Assert.Greater(lockBody.IndexOf("ReleaseBlockMutationGate()", StringComparison.Ordinal), lockBody.IndexOf("catch", StringComparison.Ordinal));
        }

        [Test]
        public void DataVaultDisposeClearsLatestCreatedPointer()
        {
            string vault = File.ReadAllText(GlobalDataVaultPath);
            string disposeBody = ExtractMethodBodyFromSignature(vault, "public void Dispose()");

            StringAssert.Contains("ReferenceEquals(_latestCreated, this)", disposeBody);
            StringAssert.Contains("_latestCreated = null", disposeBody);
        }

        [Test]
        public void DataVaultInitializeAbortsOnNativeAllocationException()
        {
            string vault = File.ReadAllText(GlobalDataVaultPath);
            string initializeBody = ExtractMethodBodyFromSignature(vault, "public void Initialize(");

            StringAssert.Contains("try", initializeBody);
            StringAssert.Contains("catch", initializeBody);
            StringAssert.Contains("AbortInitialize();", initializeBody);
            StringAssert.Contains("throw;", initializeBody);
            Assert.Greater(initializeBody.IndexOf("try", StringComparison.Ordinal), initializeBody.IndexOf("H8Memory.Initialize()", StringComparison.Ordinal));
            Assert.Greater(initializeBody.IndexOf("_buffers = new UnsafeHashMap<int, IntPtr>", StringComparison.Ordinal), initializeBody.IndexOf("try", StringComparison.Ordinal));
            Assert.Greater(initializeBody.IndexOf("catch", StringComparison.Ordinal), initializeBody.IndexOf("_latestCreated = this", StringComparison.Ordinal));
        }

        [Test]
        public void DataVaultCreateFailsClosedWhenInitializeDoesNotComplete()
        {
            string vault = File.ReadAllText(GlobalDataVaultPath);
            string h8Memory = File.ReadAllText(H8MemoryPath);
            string createBody = ExtractMethodBodyFromSignature(vault, "public static GlobalDataVault Create(");

            StringAssert.Contains("vault.Initialize(capacity, arenaCapacityLimitBytes)", createBody);
            StringAssert.Contains("if (vault._initialized)", createBody);
            StringAssert.Contains("return vault;", createBody);
            StringAssert.Contains("vault.AbortInitialize();", createBody);
            StringAssert.Contains("FatalMemoryException.ThrowVaultInitializationFailed();", createBody);
            StringAssert.Contains("public static void ThrowVaultInitializationFailed()", h8Memory);
            Assert.Greater(createBody.IndexOf("vault.AbortInitialize();", StringComparison.Ordinal), createBody.IndexOf("if (vault._initialized)", StringComparison.Ordinal));
            Assert.Greater(createBody.IndexOf("FatalMemoryException.ThrowVaultInitializationFailed();", StringComparison.Ordinal), createBody.IndexOf("vault.AbortInitialize();", StringComparison.Ordinal));
        }

        [Test]
        public void H8MemoryShutdownDisposesTrackingContainersEvenAfterPartialInitialize()
        {
            string h8Memory = File.ReadAllText(H8MemoryPath);
            string shutdownBody = ExtractMethodBodyFromSignature(h8Memory, "public static void Shutdown()");
            string disposeTrackingBody = ExtractMethodBody(h8Memory, "DisposeTrackingContainers");

            StringAssert.Contains("if (!_initialized)", shutdownBody);
            StringAssert.Contains("DisposeTrackingContainers()", shutdownBody);
            StringAssert.Contains("ResetStaticValueState()", shutdownBody);
            Assert.GreaterOrEqual(CountToken(shutdownBody, "DisposeTrackingContainers()"), 2);
            StringAssert.Contains("_allocationOwners.Dispose()", disposeTrackingBody);
            StringAssert.Contains("_ownerPointers.Dispose()", disposeTrackingBody);
            StringAssert.Contains("_records.Dispose()", disposeTrackingBody);
            StringAssert.Contains("_eventBlackBox.Dispose()", disposeTrackingBody);
        }

        [Test]
        public void InputClearFrameStateClearsAllReplayTruthBuffers()
        {
            string inputDispatcher = File.ReadAllText(InputDispatcherPath);
            string clearFrameBody = ExtractMethodBody(inputDispatcher, "ClearFrameState");

            StringAssert.Contains("ClearVaultBuffer(ref _inputReplaySnapshotHandle)", clearFrameBody);
            StringAssert.Contains("ClearVaultBuffer(ref _inputReplayFrameHandle)", clearFrameBody);
            StringAssert.Contains("ClearVaultBuffer(ref _inputReplayTelemetryHandle)", clearFrameBody);
        }

        private static void AssertHotBodyClean(string body)
        {
            Assert.IsFalse(body.Contains("GlobalRegistry.Get<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains(".GetComponent<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("GetComponent(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("FindObjectOfType<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("FindFirstObjectByType<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("GameObject.Find(", StringComparison.Ordinal));
        }

        private static string ExtractStructMethodBody(string source, string structName, string methodName)
        {
            int structIndex = source.IndexOf("struct " + structName, StringComparison.Ordinal);
            Assert.GreaterOrEqual(structIndex, 0, structName);
            int methodIndex = source.IndexOf("void " + methodName + "()", structIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, methodName);
            return ExtractBodyAt(source, methodIndex);
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int methodIndex = FindMethodDeclaration(source, methodName);
            Assert.GreaterOrEqual(methodIndex, 0, methodName);
            return ExtractBodyAt(source, methodIndex);
        }

        private static string ExtractMethodBodyFromSignature(string source, string signature)
        {
            int methodIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, signature);
            return ExtractBodyAt(source, methodIndex);
        }

        private static int FindMethodDeclaration(string source, string methodName)
        {
            string[] prefixes =
            {
                "private void ",
                "public void ",
                "internal void ",
                "protected void ",
                "private bool ",
                "public bool ",
                "private static void ",
                "public static void ",
                "private static bool ",
                "public static bool "
            };

            int best = -1;
            for (int i = 0; i < prefixes.Length; i++)
            {
                int index = source.IndexOf(prefixes[i] + methodName + "(", StringComparison.Ordinal);
                if (index >= 0 && (best < 0 || index < best))
                    best = index;
            }

            return best;
        }

        private static string ExtractBodyAt(string source, int methodIndex)
        {
            int open = source.IndexOf('{', methodIndex);
            Assert.GreaterOrEqual(open, 0, "method open brace");
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

            Assert.Fail("method close brace");
            return string.Empty;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }
        }
    }
}
