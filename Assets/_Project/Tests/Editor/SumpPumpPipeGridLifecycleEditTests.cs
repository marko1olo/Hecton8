using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SumpPumpPipeGridLifecycleEditTests
    {
        [Test]
        public void ReleaseOwnedBuffersUnlocksSolverPinsBeforeHandleReleaseOrReset()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs");
            string releaseOwned = ExtractMethodBlock(source, "private void ReleaseOwnedBuffers()");

            Assert.That(releaseOwned, Does.Contain("ReleaseDrainageSolverBufferPins();"));
            Assert.That(releaseOwned, Does.Contain("ReleaseDrainageMutationGuard();"));
            Assert.That(releaseOwned, Does.Contain("if (_vault == null)"));
            Assert.That(releaseOwned, Does.Contain("ReleaseOwnedHandle(ref _flowGpuHandle"));

            Assert.Less(
                releaseOwned.IndexOf("ReleaseDrainageSolverBufferPins();", StringComparison.Ordinal),
                releaseOwned.IndexOf("if (_vault == null)", StringComparison.Ordinal));
            Assert.Less(
                releaseOwned.IndexOf("ReleaseDrainageMutationGuard();", StringComparison.Ordinal),
                releaseOwned.IndexOf("if (_vault == null)", StringComparison.Ordinal));
            Assert.Less(
                releaseOwned.IndexOf("ReleaseDrainageSolverBufferPins();", StringComparison.Ordinal),
                releaseOwned.IndexOf("ReleaseOwnedHandle(ref _flowGpuHandle", StringComparison.Ordinal));
            Assert.Less(
                releaseOwned.IndexOf("ReleaseDrainageMutationGuard();", StringComparison.Ordinal),
                releaseOwned.IndexOf("ReleaseOwnedHandle(ref _flowGpuHandle", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

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

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
