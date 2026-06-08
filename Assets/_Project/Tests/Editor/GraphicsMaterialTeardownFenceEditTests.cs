using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GraphicsMaterialTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs",
            "private void CompleteSimulationForLifecycle(IDataVault lockVault)",
            "DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true)",
            "_simulationScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs",
            "private void CompleteSimulationForLifecycle()",
            "DispatcherJobFence.TryComplete(ref _scheduledSimulationHandle, forceComplete: true)",
            "_runtimeFlags &= ~FlagJobFencePending;")]
        public void MaterialRuntimeLifecycleForceCompletesInsidePostSimulationSwapWindow(
            string relativePath,
            string signature,
            string completeCall,
            string finishSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            const string beginWindow = "DispatcherJobFence.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostSimulationSwapWindow();";

            int beginIndex = method.IndexOf(beginWindow, StringComparison.Ordinal);
            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, "Missing post-simulation begin: " + relativePath);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + relativePath);
            Assert.GreaterOrEqual(endIndex, 0, "Missing post-simulation end: " + relativePath);
            Assert.GreaterOrEqual(finishIndex, 0, "Missing finish signal: " + relativePath);
            Assert.Less(beginIndex, completeIndex, relativePath);
            Assert.Less(completeIndex, endIndex, relativePath);
            Assert.Less(endIndex, finishIndex, relativePath);
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
