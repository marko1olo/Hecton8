using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class FabricationAssemblerLifecycleEditTests
    {
        [Test]
        public void LifecycleForceCompletesSimulationInsidePostSimulationWindowBeforeVaultRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/FabricationAssemblerRuntime.cs");
            string complete = ExtractMethodBlock(source, "private void CompleteSimulationForLifecycle()");
            string shutdown = ExtractMethodBlock(source, "private void Shutdown()");
            string rebind = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            const string beginWindow = "DispatcherJobFence.BeginPostSimulationSwapWindow();";
            const string completeCall = "DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true)";
            const string endWindow = "DispatcherJobFence.EndPostSimulationSwapWindow();";

            Assert.That(complete, Does.Contain(beginWindow));
            Assert.That(complete, Does.Contain(completeCall));
            Assert.That(complete, Does.Contain(endWindow));
            AssertOrdered(complete, beginWindow, completeCall);
            AssertOrdered(complete, completeCall, endWindow);
            AssertOrdered(complete, endWindow, "_simulationScheduled = false;");

            AssertOrdered(shutdown, "CompleteSimulationForLifecycle();", "ReleaseVaultHandles(_vault);");
            AssertOrdered(rebind, "CompleteSimulationForLifecycle();", "ReleaseVaultHandles(previousService is IDataVault previousVault ? previousVault : _vault);");
        }

        private static void AssertOrdered(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThan(firstIndex), second);
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
