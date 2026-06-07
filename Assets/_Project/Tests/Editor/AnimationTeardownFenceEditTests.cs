using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AnimationTeardownFenceEditTests
    {
        [TestCase("Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs")]
        [TestCase("Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderRuntime.cs")]
        public void AnimationSolverTeardownForceCompletesInsideLateFrameSwapWindow(string relativePath)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, "private bool CompletePendingSolverForTeardown()");

            Assert.That(method, Does.Contain("DispatcherJobFence.BeginLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain("DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)"));
            Assert.That(method, Does.Contain("DispatcherJobFence.EndLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain("return FinishPendingSolverCompletion();"));

            Assert.Less(
                method.IndexOf("DispatcherJobFence.BeginLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)", StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf("return FinishPendingSolverCompletion();", StringComparison.Ordinal));
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
