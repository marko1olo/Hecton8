using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AIEcosystemDumpWorkerLifecycleEditTests
    {
        [Test]
        public void SpatialAndEcosystemDumpWorkersUseNoThrowBoundedShutdown()
        {
            AssertDumpWorkerLifecycle("Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs");
            AssertDumpWorkerLifecycle("Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs");
        }

        private static void AssertDumpWorkerLifecycle(string relativePath)
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
            string shutdownBody = ExtractMethodBody(source, "public static void ShutdownDumpWorker()");
            string signalBody = ExtractMethodBody(source, "private static bool SignalDumpWorkerNoThrow(AutoResetEvent signal)");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinDumpWorkerNoThrow(Thread worker)");
            string disposeBody = ExtractMethodBody(source, "private static void DisposeDumpSignalNoThrow(AutoResetEvent signal)");

            StringAssert.Contains("SignalDumpWorkerNoThrow(signal);", shutdownBody);
            StringAssert.Contains("TryJoinDumpWorkerNoThrow(worker);", shutdownBody);
            StringAssert.Contains("DisposeDumpSignalNoThrow(signal);", shutdownBody);
            StringAssert.DoesNotContain("worker.Join(DumpWorkerJoinMilliseconds)", shutdownBody);
            StringAssert.DoesNotContain("signal.Set();", shutdownBody);
            StringAssert.DoesNotContain("signal.Dispose();", shutdownBody);

            StringAssert.Contains("signal.Set();", signalBody);
            StringAssert.Contains("catch (Exception)", signalBody);

            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, worker)", joinBody);
            StringAssert.Contains("worker.Join(DumpWorkerJoinMilliseconds);", joinBody);
            StringAssert.Contains("return !worker.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);

            StringAssert.Contains("signal.Dispose();", disposeBody);
            StringAssert.Contains("catch (Exception)", disposeBody);
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
