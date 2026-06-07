using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AudioTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs",
            "private void CompletePendingVocalWarningJobsForTeardown()",
            "DispatcherJobFence.TryComplete(ref handle, forceComplete: true)",
            "_pendingVocalWarningJobHandle = default;")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs",
            "private void ForceFlushSynthJobForShutdown()",
            "DispatcherJobFence.TryComplete(ref _synthJobHandle, forceComplete: true)",
            "Volatile.Write(ref _synthJobPending, 0);")]
        public void AudioTeardownForceCompletesInsideLateFrameSwapWindow(
            string relativePath,
            string signature,
            string completeCall,
            string releaseSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            Assert.That(method, Does.Contain("DispatcherJobFence.BeginLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain("DispatcherJobFence.EndLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain(releaseSignal));

            Assert.Less(
                method.IndexOf("DispatcherJobFence.BeginLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(completeCall, StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf(completeCall, StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(releaseSignal, StringComparison.Ordinal));
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
