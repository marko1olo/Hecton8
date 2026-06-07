using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineFluidDynamicsLifecycleEditTests
    {
        [Test]
        public void DisposeNativeStateCompletesCombinedJobsInsidePostFixedWindowBeforeClearingViews()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SubmarineFluidDynamics.cs");
            string dispose = ExtractMethodBlock(source, "private void DisposeNativeStateDeferred()");
            string helper = ExtractMethodBlock(source, "private void CompleteDisposeHandleInPostFixedSwapWindow()");

            Assert.That(dispose, Does.Contain("CompleteDisposeHandleInPostFixedSwapWindow();"));
            Assert.That(dispose, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _disposeHandle, true)"));
            AssertOrdered(
                dispose,
                "DisposeNativeStateBuffer(ref _exteriorThermalHazardIds, VaultExteriorThermalHazardIdsFlag);",
                "CompleteDisposeHandleInPostFixedSwapWindow();");
            AssertOrdered(dispose, "CompleteDisposeHandleInPostFixedSwapWindow();", "ClearNativeStateViews();");

            AssertForceCompleteInsidePostFixedWindow(helper);
        }

        private static void AssertForceCompleteInsidePostFixedWindow(string method)
        {
            const string beginWindow = "DispatcherJobSwap.BeginPostFixedSwapWindow();";
            const string completeCall = "DispatcherJobSwap.TryComplete(ref _disposeHandle, forceComplete: true)";
            const string endWindow = "DispatcherJobSwap.EndPostFixedSwapWindow();";

            Assert.That(method, Does.Contain(beginWindow));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain(endWindow));
            AssertOrdered(method, beginWindow, completeCall);
            AssertOrdered(method, completeCall, endWindow);
        }

        private static void AssertOrdered(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, before);
            Assert.GreaterOrEqual(afterIndex, 0, after);
            Assert.Less(beforeIndex, afterIndex, before + " before " + after);
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
