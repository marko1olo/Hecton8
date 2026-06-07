using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SignalStormConcurrencyFuzzerEditTests
    {
        [Test]
        public void ProducerThreadsUseBoundedCooperativeShutdown()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs");
            string runBody = ExtractMethodBody(source, "public static SignalStormFuzzerResult1311 Run(int producerCount, int writesPerProducer)");
            string producerBody = ExtractMethodBody(source, "private static void ProducerThread(object stateObject)");
            string joinBody = ExtractMethodBody(source, "private static bool JoinProducerThreadsNoThrow(Thread[] threads, ProducerState[] states)");
            string tryJoinBody = ExtractMethodBody(source, "private static bool TryJoinProducerThreadNoThrow(Thread thread, int timeoutMilliseconds)");
            string stopBody = ExtractMethodBody(source, "private static void RequestProducerStop(ProducerState[] states)");

            StringAssert.Contains("SignalStartGateNoThrow(startGate);", runBody);
            StringAssert.Contains("JoinProducerThreadsNoThrow(threads, states);", runBody);
            StringAssert.Contains("JoinProducerThreadsAfterStopNoThrow(threads);", runBody);
            StringAssert.Contains("RequestProducerStop(states);", runBody);
            StringAssert.Contains("!HasAliveProducerThread(threads)", runBody);
            Assert.AreEqual(0, CountToken(source, "threads[i].Join();"));

            StringAssert.Contains("Volatile.Read(ref state.StopRequested)", producerBody);
            StringAssert.Contains("Volatile.Write(ref state.Faulted, 1);", producerBody);

            StringAssert.Contains("ProducerJoinTimeoutMilliseconds", joinBody);
            StringAssert.Contains("RequestProducerStop(states);", joinBody);
            StringAssert.Contains("ProducerStopJoinTimeoutMilliseconds", source);

            StringAssert.Contains("Volatile.Write(ref state.StopRequested, 1);", stopBody);
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", tryJoinBody);
            StringAssert.Contains("thread.Join(math.max(1, timeoutMilliseconds));", tryJoinBody);
            StringAssert.Contains("return !thread.IsAlive;", tryJoinBody);
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
