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
    }
}
