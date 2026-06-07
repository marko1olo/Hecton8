using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BlackboxWatchdogThreadEditTests
    {
        [Test]
        public void HeartbeatStartFailsClosedWhenThreadStartFails()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs");
            string startBody = ExtractMethodBody(source, "public static void Start()");

            StringAssert.Contains("if (_thread != null && !_thread.IsAlive)", startBody);
            StringAssert.Contains("if (_thread != null && _thread.IsAlive)", startBody);
            StringAssert.Contains("thread.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("Volatile.Write(ref _running, 0);", startBody);
            StringAssert.Contains("_thread = null;", startBody);
        }

        [Test]
        public void HeartbeatStopJoinIsNoThrowShutdownPath()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs");
            string stopBody = ExtractMethodBody(source, "public static void Stop()");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinHeartbeatThreadNoThrow(Thread thread)");

            StringAssert.Contains("TryJoinHeartbeatThreadNoThrow(thread)", stopBody);
            StringAssert.Contains("ReferenceEquals(_thread, thread)", stopBody);
            Assert.AreEqual(0, CountToken(stopBody, "thread.Join(StopJoinMilliseconds);"));
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", joinBody);
            StringAssert.Contains("thread.Join(StopJoinMilliseconds);", joinBody);
            StringAssert.Contains("return !thread.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);
        }

        [Test]
        public void HeartbeatRunDelegatesStallFlushAndResetsRunningOnException()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs");
            string runBody = ExtractMethodBody(source, "private static void Run()");
            string flushBody = ExtractMethodBody(source, "private static void FlushAndTerminateAfterStall()");

            StringAssert.Contains("FlushAndTerminateAfterStall();", runBody);
            StringAssert.Contains("catch (Exception)", runBody);
            StringAssert.Contains("Volatile.Write(ref _running, 0);", runBody);
            AssertFlushBeforeKill(flushBody, "GlobalTelemetryBus.TryEmergencyFlushFromBackground();");
        }

        [Test]
        public void GlobalTelemetryWatchdogIsolatesFatalDumpBeforeKill()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs");
            string runBody = ExtractMethodBody(source, "private static void RunBlackboxWatchdogThread()");
            string fatalBody = ExtractMethodBody(source, "private static void HandleBlackboxWatchdogFatalStall()");

            StringAssert.Contains("HandleBlackboxWatchdogFatalStall();", runBody);
            StringAssert.Contains("catch (Exception)", runBody);
            StringAssert.Contains("Volatile.Write(ref _blackboxWatchdogStopRequested, 1);", runBody);
            StringAssert.Contains("SetCatastrophicFailure(BlackboxWatchdogFatalHash);", fatalBody);
            AssertFlushBeforeKill(fatalBody, "TryWriteBlackboxDumpFromBackground(BlackboxWatchdogFatalHash);");
        }

        [Test]
        public void GlobalTelemetryWatchdogStopUsesBoundedNoThrowJoin()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs");
            string startBody = ExtractMethodBody(source, "private static bool StartBlackboxWatchdogThread()");
            string stopBody = ExtractMethodBody(source, "private static void StopBlackboxWatchdogThread()");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinBlackboxWatchdogThreadNoThrow(Thread thread)");

            StringAssert.Contains("thread.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("Volatile.Write(ref _blackboxWatchdogStopRequested, 1);", startBody);
            StringAssert.Contains("_blackboxWatchdogThread = null;", startBody);
            StringAssert.Contains("TryJoinBlackboxWatchdogThreadNoThrow(thread);", stopBody);
            Assert.AreEqual(0, CountToken(source, "thread.Join();"));
            StringAssert.Contains("thread.Join(BlackboxWatchdogStopJoinMilliseconds);", joinBody);
            StringAssert.Contains("return !thread.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", joinBody);
        }

        private static void AssertFlushBeforeKill(string body, string flushCall)
        {
            int flushIndex = body.IndexOf(flushCall, StringComparison.Ordinal);
            int killIndex = body.IndexOf("Process.GetCurrentProcess().Kill();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(flushIndex, 0);
            Assert.Greater(killIndex, flushIndex);
            StringAssert.Contains("catch (Exception)", body);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int braceStart = source.IndexOf('{', signatureIndex);
            Assert.Greater(braceStart, signatureIndex, signature);

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            Assert.Fail("Could not find method body for " + signature);
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
