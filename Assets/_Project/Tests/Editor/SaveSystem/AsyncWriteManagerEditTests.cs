#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public class AsyncWriteManagerEditTests
    {
        private string _tempFilePath;

        [SetUp]
        public void Setup()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), "AsyncWriteManager_Test_" + Guid.NewGuid().ToString() + ".bin");
        }

        [TearDown]
        public void Teardown()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [Test]
        public unsafe void WriteAll_ValidData_WritesSuccessfully()
        {
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            fixed (byte* ptr = data)
            {
                bool result = AsyncWriteManager.WriteAll(_tempFilePath, ptr, data.Length, out string error);
                Assert.IsTrue(result, $"Expected success but failed with error: {error}");
                Assert.IsEmpty(error);
            }

            Assert.IsTrue(File.Exists(_tempFilePath));
            byte[] readback = File.ReadAllBytes(_tempFilePath);
            CollectionAssert.AreEqual(data, readback);
        }

        [Test]
        public unsafe void WriteAll_ZeroBytes_ReturnsError()
        {
            byte[] data = new byte[0];
            fixed (byte* ptr = data)
            {
                bool result = AsyncWriteManager.WriteAll(_tempFilePath, ptr, 0, out string error);
                Assert.IsFalse(result);
                Assert.AreEqual("Native write requested zero bytes.", error);
            }
        }

        [Test]
        public unsafe void WriteAll_NullPath_ReturnsError()
        {
            byte[] data = new byte[] { 1, 2, 3 };
            fixed (byte* ptr = data)
            {
                bool result = AsyncWriteManager.WriteAll(null, ptr, data.Length, out string error);
                Assert.IsFalse(result);
                Assert.AreEqual("Native write path is empty.", error);
            }
        }

        [Test]
        public unsafe void WriteAllPaged_ValidData_WritesSuccessfully()
        {
            byte[] data = new byte[] { 10, 20, 30, 40, 50 };
            fixed (byte* ptr = data)
            {
                bool result = AsyncWriteManager.WriteAllPaged(_tempFilePath, ptr, data.Length, out string error);
                Assert.IsTrue(result, $"Expected success but failed with error: {error}");
                Assert.IsEmpty(error);
            }

            Assert.IsTrue(File.Exists(_tempFilePath));
            byte[] readback = File.ReadAllBytes(_tempFilePath);
            CollectionAssert.AreEqual(data, readback);
        }

        [Test]
        public unsafe void WriteDiagnosticDumpAll_ValidData_WritesSuccessfully()
        {
            byte[] data = new byte[] { 99, 98, 97 };
            fixed (byte* ptr = data)
            {
                bool result = AsyncWriteManager.WriteDiagnosticDumpAll(_tempFilePath, ptr, data.Length, out string error);
                Assert.IsTrue(result, $"Expected success but failed with error: {error}");
                Assert.IsEmpty(error);
            }

            Assert.IsTrue(File.Exists(_tempFilePath));
            byte[] readback = File.ReadAllBytes(_tempFilePath);
            CollectionAssert.AreEqual(data, readback);
        }

        [Test]
        public unsafe void TryReadAll_ValidData_ReadsSuccessfully()
        {
            byte[] data = new byte[] { 10, 20, 30, 40 };
            File.WriteAllBytes(_tempFilePath, data);

            byte[] readBuffer = new byte[data.Length];
            fixed (byte* ptr = readBuffer)
            {
                bool result = AsyncWriteManager.TryReadAll(_tempFilePath, ptr, data.Length, out string error);
                Assert.IsTrue(result, $"Expected success but failed with error: {error}");
                Assert.IsEmpty(error);
            }

            CollectionAssert.AreEqual(data, readBuffer);
        }

        [Test]
        public unsafe void OverwriteAll_ValidData_OverwritesSuccessfully()
        {
            byte[] initialData = new byte[] { 1, 1, 1 };
            File.WriteAllBytes(_tempFilePath, initialData);

            byte[] newData = new byte[] { 2, 2, 2, 2 };
            fixed (byte* ptr = newData)
            {
                bool result = AsyncWriteManager.OverwriteAll(_tempFilePath, ptr, newData.Length, out string error);
                Assert.IsTrue(result, $"Expected success but failed with error: {error}");
                Assert.IsEmpty(error);
            }

            byte[] readback = File.ReadAllBytes(_tempFilePath);
            CollectionAssert.AreEqual(newData, readback);
        }

        [Test]
        public unsafe void TryGetFileLength_ValidFile_ReturnsCorrectLength()
        {
            byte[] data = new byte[42];
            File.WriteAllBytes(_tempFilePath, data);

            bool result = AsyncWriteManager.TryGetFileLength(_tempFilePath, out long fileLength, out string error);

            Assert.IsTrue(result, $"Expected success but failed with error: {error}");
            Assert.AreEqual(42, fileLength);
        }

        [Test]
        public unsafe void TryCopyFileRangeToNativeArray_ValidRange_CopiesSuccessfully()
        {
            byte[] data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            File.WriteAllBytes(_tempFilePath, data);

            NativeArray<byte> buffer = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                bool result = AsyncWriteManager.TryCopyFileRangeToNativeArray(_tempFilePath, 2, buffer, 4, out string error);

                Assert.IsTrue(result, $"Expected success but failed with error: {error}");

                byte[] expected = new byte[] { 2, 3, 4, 5 };
                byte[] actual = buffer.ToArray();
                CollectionAssert.AreEqual(expected, actual);
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }
}
#endif
