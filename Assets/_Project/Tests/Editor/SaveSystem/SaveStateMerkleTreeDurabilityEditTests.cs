using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SaveStateMerkleTreeDurabilityEditTests
    {
        [Test]
        public void MerkleWalAppend_InvalidatesReadCacheAroundWalMutation()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs"));

            int methodIndex = source.IndexOf(
                "internal static bool TryAppendCompressedWalMmf(",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);

            int nextMethodIndex = source.IndexOf(
                "internal static bool TryValidateWalAndRollback(",
                methodIndex,
                StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, methodIndex, source);

            string methodBody = source.Substring(methodIndex, nextMethodIndex - methodIndex);
            AssertTokensInOrder(
                methodBody,
                "string absoluteWalPath = Path.GetFullPath(walPath);",
                "HectonPersistentPathPolicy.EnsureParentDirectory(absoluteWalPath);",
                "absoluteWalPath,",
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);",
                "stream.SetLength(endOffset);",
                "stream.Flush(true);",
                "finally",
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);");

            StringAssert.Contains("stream.Write(headerBytes.Slice(0, headerByteCount));", methodBody);
            StringAssert.Contains("stream.Write(new ReadOnlySpan<byte>(payload, byteCount));", methodBody);
        }

        [Test]
        public void MerkleWalBackupRestore_InvalidatesAndFlushesCopiedWal()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs"));

            int methodIndex = source.IndexOf(
                "private static bool TryRestoreBackup(",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);

            int nextMethodIndex = source.IndexOf(
                "private static bool TryReadExact(",
                methodIndex,
                StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, methodIndex, source);

            string methodBody = source.Substring(methodIndex, nextMethodIndex - methodIndex);
            StringAssert.Contains("string absoluteWalPath = Path.GetFullPath(walPath);", methodBody);
            StringAssert.Contains("string absoluteBackupPath = Path.GetFullPath(backupPath);", methodBody);
            StringAssert.Contains(
                "!AsyncWriteManager.TryGetFileLength(absoluteBackupPath, out long backupBytes, out string backupLengthError)",
                methodBody);

            int preInvalidationIndex = methodBody.IndexOf(
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(preInvalidationIndex, 0, methodBody);

            int copyIndex = methodBody.IndexOf(
                "File.Copy(absoluteBackupPath, absoluteWalPath, true);",
                preInvalidationIndex,
                StringComparison.Ordinal);
            Assert.Greater(copyIndex, preInvalidationIndex, methodBody);

            int finallyIndex = methodBody.IndexOf("finally", copyIndex, StringComparison.Ordinal);
            Assert.Greater(finallyIndex, copyIndex, methodBody);

            int postInvalidationIndex = methodBody.IndexOf(
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);",
                finallyIndex,
                StringComparison.Ordinal);
            Assert.Greater(postInvalidationIndex, finallyIndex, methodBody);

            int restoredLengthIndex = methodBody.IndexOf(
                "!AsyncWriteManager.TryGetFileLength(absoluteWalPath, out long restoredBytes, out string restoredLengthError)",
                postInvalidationIndex,
                StringComparison.Ordinal);
            Assert.Greater(restoredLengthIndex, postInvalidationIndex, methodBody);

            int lengthMismatchIndex = methodBody.IndexOf(
                "restoredBytes != backupBytes",
                restoredLengthIndex,
                StringComparison.Ordinal);
            Assert.Greater(lengthMismatchIndex, restoredLengthIndex, methodBody);

            int flushIndex = methodBody.IndexOf(
                "!AsyncWriteManager.FlushCriticalSavePath(absoluteWalPath, restoredBytes, out string flushError)",
                lengthMismatchIndex,
                StringComparison.Ordinal);
            Assert.Greater(flushIndex, lengthMismatchIndex, methodBody);

            StringAssert.Contains("catch (IOException exception)", methodBody);
            StringAssert.Contains("catch (UnauthorizedAccessException exception)", methodBody);
            StringAssert.Contains("catch (System.Security.SecurityException exception)", methodBody);
        }

        private static void AssertTokensInOrder(string value, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int next = value.IndexOf(token, index, StringComparison.Ordinal);
                Assert.GreaterOrEqual(next, 0, "Missing token after offset " + index + ": " + token);
                index = next + token.Length;
            }
        }
    }
}
