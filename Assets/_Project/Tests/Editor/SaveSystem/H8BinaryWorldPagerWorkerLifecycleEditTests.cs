using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class H8BinaryWorldPagerWorkerLifecycleEditTests
    {
        [Test]
        public void WorkerStartFailureClearsInitializedPagerState()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs");
            string initializeBody = ExtractMethodBody(source, "public void Initialize(string absolutePath)");
            string waitBody = ExtractMethodBody(source, "private bool WaitForWorkerExit()");
            string startBody = ExtractMethodBody(source, "private bool StartWorker()");

            StringAssert.Contains("Volatile.Write(ref _initialized, 1);", initializeBody);
            StringAssert.Contains("if (!StartWorker())", initializeBody);
            StringAssert.Contains("MarkInitializationFault(PagerInitializationFaultReason.WorkerStart);", initializeBody);

            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, workerThread)", waitBody);
            StringAssert.Contains("workerThread.Join(WorkerShutdownWaitMilliseconds);", waitBody);
            StringAssert.Contains("if (!workerThread.IsAlive)", waitBody);
            StringAssert.Contains("catch (Exception)", waitBody);

            StringAssert.Contains("Interlocked.Exchange(ref _workerRunning, 1)", startBody);
            StringAssert.Contains("return true;", startBody);
            StringAssert.Contains("workerThread.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("_workerThread = null;", startBody);
            StringAssert.Contains("Volatile.Write(ref _workerThreadId, 0);", startBody);
            StringAssert.Contains("Volatile.Write(ref _workerRunning, 0);", startBody);
            StringAssert.Contains("return false;", startBody);

            StringAssert.Contains("WorkerStart = 6", source);
            Assert.AreEqual(0, CountToken(startBody, "MarkInitializationFault("));
        }

        [Test]
        public void DirectWorldAndWalMutationsInvalidateAsyncReadCache()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs");
            string processWriteBody = ExtractMethodBody(source, "private void ProcessWrite(in PageWriteCommand command)");
            string ensureDirectoryBody = ExtractMethodBody(source, "private unsafe void EnsureDirectoryPage()");
            string writeDirectoryBody = ExtractMethodBody(source, "private unsafe bool WriteDirectoryEntry(long sectorHash, long offset, out int directorySlot, out bool collision, out long previousSectorHash)");
            string appendWalBody = ExtractMethodBody(source, "private unsafe bool AppendWalRecord(");
            string clearWalBody = ExtractMethodBody(source, "private void ClearWalAfterCommit()");
            string replayWalBody = ExtractMethodBody(source, "private unsafe void ReplayWalIfPresent()");
            string mappedWriteBody = ExtractMethodBody(source, "private unsafe bool TryWriteWorldPageMapped(");

            StringAssert.Contains("private void InvalidateWorldReadCache()", source);
            StringAssert.Contains("private void InvalidateWalReadCache()", source);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(_path);", source);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(_walPath);", source);

            Assert.IsTrue(ContainsTokensInOrder(
                processWriteBody,
                "InvalidateWorldReadCache();",
                "stream.Write(header);",
                "stream.Flush(true);",
                "InvalidateWorldReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                ensureDirectoryBody,
                "InvalidateWorldReadCache();",
                "stream.Write(directory);",
                "stream.Flush(true);",
                "InvalidateWorldReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                writeDirectoryBody,
                "InvalidateWorldReadCache();",
                "stream.Write(entry);",
                "stream.Flush(true);",
                "InvalidateWorldReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                appendWalBody,
                "InvalidateWalReadCache();",
                "walStream.Write(walHeader);",
                "walStream.Flush(true);",
                "InvalidateWalReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                clearWalBody,
                "InvalidateWalReadCache();",
                "walStream.SetLength(0L);",
                "walStream.Flush(true);",
                "InvalidateWalReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                replayWalBody,
                "InvalidateWorldReadCache();",
                "worldStream.Write(pageHeader);",
                "worldStream.Flush(true);",
                "InvalidateWorldReadCache();",
                "InvalidateWalReadCache();",
                "walStream.SetLength(0L);",
                "walStream.Flush(true);",
                "InvalidateWalReadCache();"));

            Assert.IsTrue(ContainsTokensInOrder(
                mappedWriteBody,
                "InvalidateWorldReadCache();",
                "UnsafeUtility.MemCpy(target, headerPtr, SectorHeaderBytes);",
                "stream.Flush(true);",
                "InvalidateWorldReadCache();"));
        }

        [Test]
        public void BlackBoxDumpWritesTempFlushesAndPromotesAtomically()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs");
            string dumpBody = ExtractMethodBody(source, "private unsafe void WriteBlackBoxDump(");
            string validateBody = ExtractMethodBody(source, "private static bool TryFlushAndValidateBlackBoxDumpFile");
            string cleanupBody = ExtractMethodBody(source, "private static void TryDeleteBlackBoxDumpTempFile");

            Assert.IsFalse(dumpBody.Contains("new FileStream(dumpPath", StringComparison.Ordinal));
            Assert.IsFalse(dumpBody.Contains("dumpPath,\r\n                           FileMode.Create", StringComparison.Ordinal));
            Assert.IsFalse(dumpBody.Contains("dumpPath,\n                           FileMode.Create", StringComparison.Ordinal));
            StringAssert.Contains("absoluteDumpPath = Path.GetFullPath(dumpPath);", dumpBody);
            StringAssert.Contains("tempPath = absoluteDumpPath + \".tmp\";", dumpBody);
            StringAssert.Contains("TryDeleteBlackBoxDumpTempFile(tempPath);", dumpBody);
            StringAssert.Contains("long expectedBytes = (long)count * UnsafeUtility.SizeOf<H8BinaryWorldPagerTelemetryEntry>();", dumpBody);
            StringAssert.Contains("FileOptions.WriteThrough | FileOptions.SequentialScan", dumpBody);
            StringAssert.Contains("writer.Flush();", dumpBody);
            StringAssert.Contains("stream.Flush(true);", dumpBody);
            StringAssert.Contains("stream.Length != expectedBytes", dumpBody);
            StringAssert.Contains("TryFlushAndValidateBlackBoxDumpFile(tempPath, expectedBytes)", dumpBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", dumpBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absoluteDumpPath);", dumpBody);
            StringAssert.Contains("File.Replace(tempPath, absoluteDumpPath, null, true);", dumpBody);
            StringAssert.Contains("File.Move(tempPath, absoluteDumpPath);", dumpBody);
            StringAssert.Contains("TryFlushAndValidateBlackBoxDumpFile(absoluteDumpPath, expectedBytes)", dumpBody);
            Assert.IsTrue(ContainsTokensInOrder(
                dumpBody,
                "absoluteDumpPath = Path.GetFullPath(dumpPath);",
                "tempPath = absoluteDumpPath + \".tmp\";",
                "TryDeleteBlackBoxDumpTempFile(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "new FileStream(",
                "tempPath",
                "FileOptions.WriteThrough | FileOptions.SequentialScan",
                "writer.Flush();",
                "stream.Flush(true);",
                "stream.Length != expectedBytes",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "TryFlushAndValidateBlackBoxDumpFile(tempPath, expectedBytes)",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteDumpPath);",
                "File.Replace(tempPath, absoluteDumpPath, null, true);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(absoluteDumpPath);",
                "TryFlushAndValidateBlackBoxDumpFile(absoluteDumpPath, expectedBytes)"));

            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(path, expectedBytes, out _)", validateBody);
            Assert.IsTrue(ContainsTokensInOrder(
                cleanupBody,
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "File.Delete(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

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

            Assert.Fail("Missing method close brace: " + signature);
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

        private static bool ContainsTokensInOrder(string source, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int next = source.IndexOf(token, index, StringComparison.Ordinal);
                if (next < 0)
                    return false;

                index = next + token.Length;
            }

            return true;
        }
    }
}
