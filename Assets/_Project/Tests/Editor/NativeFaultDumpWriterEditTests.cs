using System;
using System.IO;
using Hecton8.Core;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class NativeFaultDumpWriterEditTests
    {
        [Test]
        public void TryWriteAll_NativeArrayPayloadWritesExactBytes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Hecton8_NativeFaultDumpWriterEditTests");
            string path = Path.Combine(directory, "native-array.bin");
            NativeArray<byte> payload = new NativeArray<byte>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                payload[0] = 0x48;
                payload[1] = 0x38;
                payload[2] = 0xAA;
                payload[3] = 0x55;

                Assert.IsTrue(NativeFaultDumpWriter.TryWriteAll(path, payload, payload.Length));

                byte[] bytes = File.ReadAllBytes(path);
                Assert.AreEqual(4, bytes.Length);
                Assert.AreEqual(0x48, bytes[0]);
                Assert.AreEqual(0x38, bytes[1]);
                Assert.AreEqual(0xAA, bytes[2]);
                Assert.AreEqual(0x55, bytes[3]);
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(directory))
                    Directory.Delete(directory);
            }
        }

        [Test]
        public void TryWriteAll_EditorRelativePathResolvesUnderProjectRoot()
        {
            string relativePath = Path.Combine(".tmp", "NativeFaultDumpWriterEditTests", "relative.bin");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string expectedPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            byte[] payload = { 1, 2, 3, 4, 5 };

            try
            {
                Assert.IsTrue(NativeFaultDumpWriter.TryWriteAll(relativePath, payload, payload.Length));
                Assert.IsTrue(File.Exists(expectedPath));
                Assert.AreEqual(payload, File.ReadAllBytes(expectedPath));
            }
            finally
            {
                if (File.Exists(expectedPath))
                    File.Delete(expectedPath);

                string directory = Path.GetDirectoryName(expectedPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    Directory.Delete(directory);
            }
        }

        [Test]
        public void TryWriteAll_RelativeTraversalPathFailsClosed()
        {
            byte[] payload = { 9, 8, 7 };

            Assert.IsFalse(NativeFaultDumpWriter.TryWriteAll("..\\bad-dump.bin", payload, payload.Length));
            Assert.IsFalse(NativeFaultDumpWriter.TryWriteAll("../bad-dump.bin", payload, payload.Length));
        }

        [Test]
        public void TryWriteAll_RelativeFilenameWithDoubleDotStillWrites()
        {
            string relativePath = Path.Combine(".tmp", "NativeFaultDumpWriterEditTests", "dump..bin");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string expectedPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            byte[] payload = { 6, 6, 8 };

            try
            {
                Assert.IsTrue(NativeFaultDumpWriter.TryWriteAll(relativePath, payload, payload.Length));
                Assert.IsTrue(File.Exists(expectedPath));
                Assert.AreEqual(payload, File.ReadAllBytes(expectedPath));
            }
            finally
            {
                if (File.Exists(expectedPath))
                    File.Delete(expectedPath);

                string directory = Path.GetDirectoryName(expectedPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    Directory.Delete(directory);
            }
        }

        [Test]
        public void TryWriteAll_ReadOnlySpanPayloadWritesRequestedPrefixOnly()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Hecton8_NativeFaultDumpWriterEditTests");
            string path = Path.Combine(directory, "span-prefix.bin");
            byte[] payload = { 0x10, 0x20, 0x30, 0x40, 0x50 };

            try
            {
                Assert.IsTrue(NativeFaultDumpWriter.TryWriteAll(path, payload, 3));

                byte[] bytes = File.ReadAllBytes(path);
                Assert.AreEqual(3, bytes.Length);
                Assert.AreEqual(0x10, bytes[0]);
                Assert.AreEqual(0x20, bytes[1]);
                Assert.AreEqual(0x30, bytes[2]);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(directory))
                    Directory.Delete(directory);
            }
        }

        [Test]
        public void TryWriteAll_ReplacesExistingFileOnlyAfterTempPromotion()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Hecton8_NativeFaultDumpWriterEditTests");
            string path = Path.Combine(directory, "replace-existing.bin");
            byte[] oldPayload = { 0x01, 0x02, 0x03, 0x04 };
            byte[] newPayload = { 0x90, 0x91, 0x92 };

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, oldPayload);

                Assert.IsTrue(NativeFaultDumpWriter.TryWriteAll(path, newPayload, newPayload.Length));

                Assert.AreEqual(newPayload, File.ReadAllBytes(path));
                Assert.IsFalse(File.Exists(path + ".tmp"));
            }
            finally
            {
                if (File.Exists(path + ".tmp"))
                    File.Delete(path + ".tmp");
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(directory))
                    Directory.Delete(directory);
            }
        }

        [Test]
        public void NativeFaultDumpWriter_SourceAvoidsManagedPayloadClone()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs");
            int start = source.IndexOf("public static unsafe class NativeFaultDumpWriter", StringComparison.Ordinal);
            int end = source.IndexOf("    public static class DispatcherJobFence", start, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);

            string writerBlock = source.Substring(start, end - start);
            Assert.IsFalse(writerBlock.Contains("new byte[byteCount]"));
            Assert.IsFalse(writerBlock.Contains(".ToArray("));
            Assert.IsFalse(writerBlock.Contains("File.WriteAllBytes"));
            Assert.IsFalse(writerBlock.Contains("Dictionary<"));
            Assert.IsFalse(writerBlock.Contains("new List<"));
            Assert.IsFalse(writerBlock.Contains("FileOptions.None"));
            Assert.IsFalse(writerBlock.Contains("new FileStream(fullPath, FileMode.Create,"));
            StringAssert.Contains("FileOptions.WriteThrough", writerBlock);
            StringAssert.Contains("string tempPath = fullPath + \".tmp\";", writerBlock);
            StringAssert.Contains("FileMode.CreateNew", writerBlock);
            StringAssert.Contains("stream.Write(payload.Slice(0, byteCount))", writerBlock);
            StringAssert.Contains("stream.Flush(true);", writerBlock);
            StringAssert.Contains("bool tempLengthMatched;", writerBlock);
            StringAssert.Contains("tempLengthMatched = stream.Length == byteCount;", writerBlock);
            StringAssert.Contains("if (!tempLengthMatched)", writerBlock);
            StringAssert.Contains("TryFlushAndValidateFaultDumpFile(tempPath, byteCount)", writerBlock);
            StringAssert.Contains("File.Replace(tempPath, fullPath, null, true);", writerBlock);
            StringAssert.Contains("File.Move(tempPath, fullPath);", writerBlock);
            StringAssert.Contains("TryFlushAndValidateFaultDumpFile(fullPath, byteCount)", writerBlock);
            StringAssert.Contains("private const int MaxTrackedTransientPayloads", writerBlock);
            StringAssert.Contains("private static readonly IntPtr[] s_transientPayloadPointers", writerBlock);
            StringAssert.Contains("private static readonly int[] s_transientPayloadRegistrationIds", writerBlock);
            StringAssert.Contains("Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArrayInstance(", writerBlock);
            StringAssert.DoesNotContain("Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArray(", writerBlock);
            StringAssert.Contains("if (registered && !TryUnregisterTransientNativeArrayPayload(payloadPointer))", writerBlock);
            StringAssert.DoesNotContain("TransientPayloadRestoreFailureMessage", writerBlock);
            StringAssert.Contains("catch (Exception disposalException)", writerBlock);
            StringAssert.Contains("TryRegisterTransientNativeArrayPayload(payload, owner, label, allocator)", writerBlock);
            StringAssert.Contains("throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);", writerBlock);
            StringAssert.Contains("TryRememberTransientPayloadRegistration(ResolveTransientPayloadPointer(payload), registrationId)", writerBlock);
            StringAssert.Contains("int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);", writerBlock);
            StringAssert.Contains("TryForgetTransientPayloadRegistration(payloadPointer, registrationId)", writerBlock);
            StringAssert.Contains("Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);", writerBlock);
            StringAssert.DoesNotContain("Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(owner, label);", writerBlock);
            StringAssert.DoesNotContain("bool restored = TryRegisterTransientNativeArrayPayload(payload, owner, label, allocator);", writerBlock);
            Assert.IsTrue(ContainsTokensInOrder(
                writerBlock,
                "string tempPath = fullPath + \".tmp\";",
                "bool tempLengthMatched;",
                "FileMode.CreateNew",
                "stream.Flush(true);",
                "tempLengthMatched = stream.Length == byteCount;",
                "if (!tempLengthMatched)",
                "TryFlushAndValidateFaultDumpFile(tempPath, byteCount)",
                "File.Replace(tempPath, fullPath, null, true);",
                "TryFlushAndValidateFaultDumpFile(fullPath, byteCount)"));
            Assert.IsTrue(ContainsTokensInOrder(
                writerBlock,
                "public static void DisposeTransientPayload(",
                "int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);",
                "payload.Dispose();",
                "payload = default;",
                "TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId);"));
            Assert.IsTrue(ContainsTokensInOrder(
                writerBlock,
                "public static NativeArray<byte> CreateTransientPayload(",
                "catch",
                "if (payload.IsCreated)",
                "payload.Dispose();",
                "if (registered && !TryUnregisterTransientNativeArrayPayload(payloadPointer))"));
            Assert.IsTrue(ContainsTokensInOrder(
                writerBlock,
                "if (TryRememberTransientPayloadRegistration(ResolveTransientPayloadPointer(payload), registrationId))",
                "return true;",
                "Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);",
                "return false;"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static bool ContainsTokensInOrder(string value, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int next = value.IndexOf(token, index, StringComparison.Ordinal);
                if (next < 0)
                    return false;

                index = next + token.Length;
            }

            return true;
        }
    }
}
