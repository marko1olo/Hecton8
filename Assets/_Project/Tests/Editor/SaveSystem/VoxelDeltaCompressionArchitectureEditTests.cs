using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VoxelDeltaCompressionArchitectureEditTests
    {
        [Test]
        public void TelemetryDumpWritesTempFlushesAndPromotesAtomically()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs");
            string dumpBody = ExtractWindow(
                source,
                "NativeArray<int> telemetryCursor,",
                "private static int CountTelemetryEntries");
            string validateBody = ExtractMethodBody(source, "private static bool TryFlushAndValidateTelemetryDumpFile");
            string cleanupBody = ExtractMethodBody(source, "private static void TryDeleteTelemetryDumpTempFile");

            Assert.IsFalse(dumpBody.Contains("new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)"));
            StringAssert.Contains("absolutePath = Path.GetFullPath(path);", dumpBody);
            StringAssert.Contains("tempPath = absolutePath + \".tmp\";", dumpBody);
            StringAssert.Contains("TryDeleteTelemetryDumpTempFile(tempPath);", dumpBody);
            StringAssert.Contains("long expectedBytes = headerBytes + ((long)entryCount * stride);", dumpBody);
            StringAssert.Contains("new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough)", dumpBody);
            StringAssert.Contains("stream.Flush(true);", dumpBody);
            StringAssert.Contains("tempLengthMatched = stream.Length == expectedBytes;", dumpBody);
            StringAssert.Contains("if (!tempLengthMatched)", dumpBody);
            StringAssert.Contains("TryFlushAndValidateTelemetryDumpFile(tempPath, expectedBytes)", dumpBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", dumpBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", dumpBody);
            StringAssert.Contains("File.Replace(tempPath, absolutePath, null, true);", dumpBody);
            StringAssert.Contains("File.Move(tempPath, absolutePath);", dumpBody);
            StringAssert.Contains("TryFlushAndValidateTelemetryDumpFile(absolutePath, expectedBytes)", dumpBody);
            Assert.IsTrue(ContainsTokensInOrder(
                dumpBody,
                "absolutePath = Path.GetFullPath(path);",
                "tempPath = absolutePath + \".tmp\";",
                "long expectedBytes = headerBytes + ((long)entryCount * stride);",
                "TryDeleteTelemetryDumpTempFile(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "FileOptions.WriteThrough",
                "stream.Flush(true);",
                "tempLengthMatched = stream.Length == expectedBytes;",
                "if (!tempLengthMatched)",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "TryFlushAndValidateTelemetryDumpFile(tempPath, expectedBytes)",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);",
                "File.Replace(tempPath, absolutePath, null, true);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);",
                "TryFlushAndValidateTelemetryDumpFile(absolutePath, expectedBytes)"));

            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(path, expectedBytes, out _)", validateBody);
            Assert.IsTrue(ContainsTokensInOrder(
                cleanupBody,
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "File.Delete(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static string ExtractWindow(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "start token not found: " + startToken);
            int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.Greater(end, start, "end token not found: " + endToken);
            return source.Substring(start, end - start);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "signature not found: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, start);

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

            Assert.Fail("method body not closed: " + signature);
            return string.Empty;
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
