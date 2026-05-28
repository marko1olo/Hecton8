using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AndroidAssetBridge1404EditTests
    {
        [Test]
        public void AndroidJniBridge_FailsClosedBeforeNativeAssetLoad()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");

            StringAssert.Contains("AndroidJNI.FindClass(\"com/unity3d/player/UnityPlayer\")", arena);
            StringAssert.Contains("TryConsumePendingAndroidJniException() || unityPlayerClass == IntPtr.Zero", arena);
            StringAssert.Contains("TryConsumePendingAndroidJniException() || activity == IntPtr.Zero", arena);
            StringAssert.Contains("TryConsumePendingAndroidJniException() || activityClass == IntPtr.Zero", arena);
            StringAssert.Contains("TryConsumePendingAndroidJniException() || getAssetsMethod == IntPtr.Zero", arena);
            StringAssert.Contains("TryConsumePendingAndroidJniException() || assetManager == IntPtr.Zero", arena);
            StringAssert.Contains("IntPtr javaVm = AndroidJNI.GetJavaVM();", arena);
            StringAssert.Contains("if (javaVm == IntPtr.Zero)", arena);
            StringAssert.Contains("H8_GetAssetSize(javaVm, assetManager, assetName)", arena);
            StringAssert.Contains("H8_LoadAssetToPointer(javaVm, assetManager, assetName, destination, blobBytes)", arena);
        }

        [Test]
        public void AndroidJniBridge_AvoidsManagedWrappersAndManagedDumpStaging()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");

            Assert.IsFalse(arena.Contains("new AndroidJavaClass", StringComparison.Ordinal));
            Assert.IsFalse(arena.Contains("new jvalue", StringComparison.Ordinal));
            Assert.IsFalse(arena.Contains("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal));
            Assert.IsFalse(arena.Contains("System.IO.Path.Combine(Application.persistentDataPath", StringComparison.Ordinal));
            StringAssert.Contains("CallObjectMethodUnsafe(activity, getAssetsMethod, null)", arena);
            StringAssert.Contains("DllImport(\"__Internal\"", arena);
            StringAssert.Contains("EntryPoint = \"H8_WriteTelemetryDump\"", arena);
            StringAssert.Contains("WriteTelemetryDumpAndroid", arena);
        }

        [Test]
        public void NativeBridge_RejectsSizeDriftAndUsesNoHeapStaging()
        {
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");

            StringAssert.Contains("#include <android/asset_manager.h>", native);
            StringAssert.Contains("#include <android/asset_manager_jni.h>", native);
            StringAssert.Contains("AAssetManager_fromJava", native);
            StringAssert.Contains("AAssetManager_open", native);
            StringAssert.Contains("AAsset_getLength64", native);
            StringAssert.Contains("AAsset_read", native);
            StringAssert.Contains("AAsset_close(asset)", native);
            StringAssert.Contains("assetLength < 0 || assetLength != bufferSize", native);
            StringAssert.Contains("H8_WriteTelemetryDump", native);
            StringAssert.Contains("Docs/AgentLogs/Dump_1404.bin", native);
            Assert.IsFalse(native.Contains("std::vector", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("std::string", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("malloc", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("free(", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("delete", StringComparison.Ordinal));
        }

        [Test]
        public void DataVault_DeferredWriterReleaseGate_DoesNotSpinOnContention()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock");
            string releaseBlock = ExtractMethod(vault, "private bool ReleaseWriterBlockLock");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            StringAssert.Contains("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0)", releaseBlock);
            StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", queue);
            StringAssert.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0", queue);
            Assert.IsFalse(queue.Contains("if (!enqueueGateAcquired)", StringComparison.Ordinal));
            StringAssert.Contains("pending->Kind == DeferredReleaseKindWriter", queue);
            StringAssert.Contains("finally", queue);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", queue);
            Assert.IsFalse(queue.Contains("Thread.SpinWait", StringComparison.Ordinal));
            Assert.IsFalse(queue.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate", StringComparison.Ordinal));
            Assert.IsFalse(queue.Contains("pending->Kind == kind", StringComparison.Ordinal));
        }

        [Test]
        public void DataMonolithTelemetryWrites_DoNotHoldNestedDataVaultWriteLocks()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string reserve = ExtractMethod(arena, "private static bool TryReserveTelemetrySlot");
            string write = ExtractMethod(arena, "private static bool TryWriteTelemetryEntry");
            string record = ExtractMethod(arena, "private static void RecordTelemetry");

            Assert.IsFalse(arena.Contains("TryAcquireTelemetryWriteViews", StringComparison.Ordinal));
            Assert.IsFalse(arena.Contains("ReleaseTelemetryWriteViews", StringComparison.Ordinal));
            Assert.AreEqual(1, CountToken(reserve, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(write, "TryAcquireWriteLock("));
            Assert.AreEqual(0, CountToken(record, "TryAcquireWriteLock("));
            StringAssert.Contains("finally", reserve);
            StringAssert.Contains("if (!ReleaseWriteLockWithRetry(vault, in _telemetryCursorHandle, SystemID.CoreDataVault))", reserve);
            StringAssert.Contains("return reserved;", reserve);
            StringAssert.Contains("finally", write);
            StringAssert.Contains("if (!ReleaseWriteLockWithRetry(vault, in _telemetryHandle, SystemID.CoreDataVault))", write);
            StringAssert.Contains("return written;", write);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root could not be resolved.");

            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Could not extract method body: " + signature);
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
