using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AsyncWriteManagerWorkerLifecycleEditTests
    {
        [Test]
        public void WorkerStartFailuresClearStickyStartedFlags()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string flushBody = ExtractMethodBody(source, "private static void EnsureFlushThread()");
            string readPrefetchBody = ExtractMethodBody(source, "private static void EnsureReadPrefetchWorker()");

            AssertWorkerStartFailureResetsFlag(flushBody, "s_flushThreadStarted", "FlushWorkerLoop");
            AssertWorkerStartFailureResetsFlag(readPrefetchBody, "s_readPrefetchWorkerStarted", "ReadPrefetchWorkerLoop");
        }

        private static void AssertWorkerStartFailureResetsFlag(string methodBody, string flagName, string workerLoopName)
        {
            StringAssert.Contains("Interlocked.CompareExchange(ref " + flagName + ", 1, 0)", methodBody);
            StringAssert.Contains("try", methodBody);
            StringAssert.Contains("Thread thread = new Thread(" + workerLoopName + ")", methodBody);
            StringAssert.Contains("thread.Start();", methodBody);
            StringAssert.Contains("catch", methodBody);
            StringAssert.Contains("Volatile.Write(ref " + flagName + ", 0);", methodBody);
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
    }
}
