using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class MonoBehaviourResidualNativePurge1403EditTests
    {
        private const int LifecycleFuzzerIterations = 10000;
        private const int StaticRaceIterations = 512;

        [Test]
        [Explicit("Agent 1403 lifecycle fuzzer: 10000 component create/destroy cycles. Run only in an isolated Unity Editor test pass.")]
        public void LifecycleLeakFuzzer_SaveManagerAndIkRigReturnSentinelToBaseline()
        {
            long baselineBytes = NativeMemorySentinel.TrackedBytes;
            int baselineAllocations = NativeMemorySentinel.ActiveAllocationCount;

            for (int i = 0; i < LifecycleFuzzerIterations; i++)
            {
                GameObject saveObject = new GameObject("SaveManager_1403_LeakProbe");
                GameObject rigObject = new GameObject("ContextualPhysicalIkRig_1403_LeakProbe");
                try
                {
                    saveObject.AddComponent<SaveManager>();
                    rigObject.AddComponent<ContextualPhysicalIkRig>();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(rigObject);
                    UnityEngine.Object.DestroyImmediate(saveObject);
                }
            }

            Assert.AreEqual(baselineAllocations, NativeMemorySentinel.ActiveAllocationCount);
            Assert.AreEqual(baselineBytes, NativeMemorySentinel.TrackedBytes);
        }

        [Test]
        [Explicit("Agent 1403 static buffer race probe allocates large save buffers. Run only in an isolated Unity Editor test pass.")]
        public void StaticRaceCondition_StaticWriteBufferPairsDoNotAliasUnderContention()
        {
            MethodInfo acquireWrite = RequirePrivateStatic("AcquireWriteBuffers");
            MethodInfo releaseWrite = RequirePrivateStatic("ReleaseWriteBuffers");
            MethodInfo disposeStatic = RequirePrivateStatic("DisposeStaticLoadCandidateScratch");

            int failures = 0;
            int stopRequested = 0;
            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < StaticRaceIterations && Volatile.Read(ref stopRequested) == 0; i++)
                {
                    NativeArray<byte> rawBuffer = default;
                    NativeArray<byte> compressedBuffer = default;
                    bool ownsRawBuffer = false;
                    bool ownsCompressedBuffer = false;
                    try
                    {
                        InvokeAcquireWrite(
                            acquireWrite,
                            out rawBuffer,
                            out ownsRawBuffer,
                            out compressedBuffer,
                            out ownsCompressedBuffer);

                        if (!rawBuffer.IsCreated || !compressedBuffer.IsCreated)
                        {
                            Interlocked.Increment(ref failures);
                            continue;
                        }

                        AssertNotSamePointer(rawBuffer, compressedBuffer);
                        rawBuffer[0] = 0xA5;
                        compressedBuffer[0] = 0x5A;
                        if (rawBuffer[0] != 0xA5 || compressedBuffer[0] != 0x5A)
                            Interlocked.Increment(ref failures);
                    }
                    catch
                    {
                        Interlocked.Increment(ref failures);
                    }
                    finally
                    {
                        if (rawBuffer.IsCreated || compressedBuffer.IsCreated)
                            InvokeReleaseWrite(releaseWrite, rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer);
                    }
                }
            });

            reader.Start();
            try
            {
                for (int i = 0; i < StaticRaceIterations; i++)
                {
                    NativeArray<byte> rawBuffer = default;
                    NativeArray<byte> compressedBuffer = default;
                    bool ownsRawBuffer = false;
                    bool ownsCompressedBuffer = false;
                    try
                    {
                        InvokeAcquireWrite(
                            acquireWrite,
                            out rawBuffer,
                            out ownsRawBuffer,
                            out compressedBuffer,
                            out ownsCompressedBuffer);

                        Assert.IsTrue(rawBuffer.IsCreated);
                        Assert.IsTrue(compressedBuffer.IsCreated);
                        AssertNotSamePointer(rawBuffer, compressedBuffer);

                        rawBuffer[0] = 0x11;
                        compressedBuffer[0] = 0x22;
                        Assert.AreEqual(0x11, rawBuffer[0]);
                        Assert.AreEqual(0x22, compressedBuffer[0]);
                    }
                    finally
                    {
                        if (rawBuffer.IsCreated || compressedBuffer.IsCreated)
                            InvokeReleaseWrite(releaseWrite, rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer);
                    }
                }
            }
            finally
            {
                Volatile.Write(ref stopRequested, 1);
                Assert.IsTrue(reader.Join(5000));
                disposeStatic.Invoke(null, Array.Empty<object>());
            }

            Assert.AreEqual(0, failures);
        }

        [Test]
        public void IkMatrixIntegrity_JobPayloadsUseNativeFacadeArrays()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project/Scripts");
            string rigSource = File.ReadAllText(Path.Combine(scriptsRoot, "Gameplay/ContextualPhysicalIkRig.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine(scriptsRoot, "Gameplay/ContextualPhysicalIkRuntime.cs"));

            string buildApplyJob = ExtractMethodBody(rigSource, "BuildApplyJob");
            Assert.That(buildApplyJob, Does.Contain("StreamHandles = _streamHandles"));
            Assert.That(buildApplyJob, Does.Contain("TwoBoneSetups = _twoBoneSetups"));
            Assert.That(buildApplyJob, Does.Contain("TargetFrames = _currentTargetFrames"));
            Assert.That(buildApplyJob, Does.Not.Contain("RigNativeBufferSet"));

            string scheduleGroundPipeline = ExtractMethodBody(runtimeSource, "ScheduleGroundPipeline");
            Assert.That(scheduleGroundPipeline, Does.Contain("EntityStates = _scheduledEntityStates"));
            Assert.That(scheduleGroundPipeline, Does.Contain("Hits = _scheduledHits"));
            Assert.That(scheduleGroundPipeline, Does.Contain("PreviousTargets = _frontTargetFrames"));
            Assert.That(scheduleGroundPipeline, Does.Contain("NextTargets = _backTargetFrames"));
            Assert.That(scheduleGroundPipeline, Does.Contain("IkTargets = _ikTargets"));
            Assert.That(scheduleGroundPipeline, Does.Contain("IkWeights = _ikWeights"));
            Assert.That(scheduleGroundPipeline, Does.Not.Contain("RuntimeNativeBufferSet"));
        }

        private static MethodInfo RequirePrivateStatic(string name)
        {
            MethodInfo method = typeof(SaveManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, name);
            return method;
        }

        private static void InvokeAcquireWrite(
            MethodInfo method,
            out NativeArray<byte> rawBuffer,
            out bool ownsRawBuffer,
            out NativeArray<byte> compressedBuffer,
            out bool ownsCompressedBuffer)
        {
            object[] args =
            {
                default(NativeArray<byte>),
                false,
                default(NativeArray<byte>),
                false
            };
            method.Invoke(null, args);
            rawBuffer = (NativeArray<byte>)args[0];
            ownsRawBuffer = (bool)args[1];
            compressedBuffer = (NativeArray<byte>)args[2];
            ownsCompressedBuffer = (bool)args[3];
        }

        private static void InvokeReleaseWrite(
            MethodInfo method,
            NativeArray<byte> rawBuffer,
            bool ownsRawBuffer,
            NativeArray<byte> compressedBuffer,
            bool ownsCompressedBuffer)
        {
            object[] args =
            {
                rawBuffer,
                ownsRawBuffer,
                compressedBuffer,
                ownsCompressedBuffer
            };
            method.Invoke(null, args);
        }

        private static void AssertNotSamePointer(NativeArray<byte> left, NativeArray<byte> right)
        {
            IntPtr leftPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(left);
            IntPtr rightPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(right);
            Assert.AreNotEqual(leftPointer, rightPointer);
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, methodName);
            int openBrace = source.IndexOf('{', methodIndex);
            Assert.GreaterOrEqual(openBrace, 0, methodName);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char value = source[i];
                if (value == '{')
                    depth++;
                else if (value == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail(methodName);
            return string.Empty;
        }
    }
}
