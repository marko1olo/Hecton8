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
            StringAssert.Contains("stream.Write(payload.Slice(0, byteCount))", writerBlock);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}
