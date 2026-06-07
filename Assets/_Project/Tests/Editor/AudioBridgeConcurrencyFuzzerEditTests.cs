using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AudioBridgeConcurrencyFuzzerEditTests
    {
        [Test]
        public void EditorFuzzerThreadsUseBoundedCooperativeShutdown()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs");

            Assert.AreEqual(0, CountToken(source, "producer.Join();"));
            Assert.AreEqual(0, CountToken(source, "consumer.Join();"));
            StringAssert.Contains("private const int FuzzerThreadJoinTimeoutMilliseconds", source);
            StringAssert.Contains("private const int FuzzerThreadStopJoinTimeoutMilliseconds", source);
            StringAssert.Contains("bool producerStopped = TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadJoinTimeoutMilliseconds);", source);
            StringAssert.Contains("producerStopped = TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadStopJoinTimeoutMilliseconds);", source);
            StringAssert.Contains("bool consumerStopped = TryJoinFuzzerThreadNoThrow(consumerThread, FuzzerThreadStopJoinTimeoutMilliseconds);", source);
            StringAssert.Contains("Volatile.Read(ref running) == 0", source);
            StringAssert.Contains("canReleaseThreadSharedState && samples.IsCreated", source);
            StringAssert.Contains("if (canReleaseThreadSharedState)", source);
            StringAssert.Contains("TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadStopJoinTimeoutMilliseconds);", source);
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", source);
            StringAssert.Contains("thread.Join(math.max(1, timeoutMilliseconds));", source);
            StringAssert.Contains("return !thread.IsAlive;", source);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
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
