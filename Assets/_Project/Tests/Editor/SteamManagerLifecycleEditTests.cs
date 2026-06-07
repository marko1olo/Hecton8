using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SteamManagerLifecycleEditTests
    {
        [Test]
        public void BackgroundInitThreadStartFailureDoesNotLeaveServiceBooting()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs"));
            string startBody = ExtractMethodBody(source, "private void StartBackgroundInit()");

            StringAssert.Contains("Interlocked.CompareExchange(ref _state, StateBooting, StateNotStarted)", startBody);
            StringAssert.Contains("try", startBody);
            StringAssert.Contains("Thread thread = new Thread(InitializeSteamworksBackground)", startBody);
            StringAssert.Contains("_initThread = thread;", startBody);
            StringAssert.Contains("thread.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("_initThread = null;", startBody);
            StringAssert.Contains("Volatile.Write(ref _state, _shutdownRequested ? StateShutdown : StateFailed);", startBody);
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
