using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ThermodynamicsConfigWorkerEditTests
    {
        [Test]
        public void ConfigWorkerLifecycleUsesBoundedNoThrowStopAndDeadThreadRestart()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs");
            string startBody = ExtractMethodBody(source, "private void StartConfigWorkerIfNeeded()");
            string stopBody = ExtractMethodBody(source, "private bool StopConfigWorker()");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinConfigWorkerNoThrow(Thread worker)");

            StringAssert.Contains("Thread existingWorker = _configWorkerThread;", startBody);
            StringAssert.Contains("if (existingWorker.IsAlive)", startBody);
            StringAssert.Contains("_configWorkerThread = null;", startBody);
            StringAssert.Contains("worker.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("Volatile.Write(ref _configWorkerRun, 0);", startBody);
            StringAssert.Contains("Volatile.Write(ref _binaryRequestState, ConfigWorkerFault);", startBody);

            StringAssert.Contains("bool stopped = TryJoinConfigWorkerNoThrow(worker);", stopBody);
            StringAssert.Contains("ReferenceEquals(_configWorkerThread, worker)", stopBody);
            Assert.AreEqual(0, CountToken(source, "worker.Join(250);"));

            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, worker)", joinBody);
            StringAssert.Contains("worker.Join(ConfigWorkerJoinTimeoutMs);", joinBody);
            StringAssert.Contains("return !worker.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);
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
