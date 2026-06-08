using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonMapMagicVegetationBridgeDisposeFenceEditTests
    {
        [Test]
        public void DeferredVegetationNativeDisposalsForceCompleteInsidePostSimulationWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs");
            string arrayDispose = ExtractMethodBlock(source, "private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)");
            string listDispose = ExtractMethodBlock(source, "private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency");
            string mapDispose = ExtractMethodBlock(source, "private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(");
            string helper = ExtractMethodBlock(source, "private static void ForceCompleteVegetationDisposeDependencyInPostSimulationWindow(");

            AssertDisposePathUsesPostSimulationHelper(arrayDispose, "array.Dispose();", "array.Dispose(dependency)");
            AssertDisposePathUsesPostSimulationHelper(listDispose, "list.Dispose();", "list.Dispose(dependency)");
            AssertDisposePathUsesPostSimulationHelper(mapDispose, "map.Dispose();", "map.Dispose(dependency)");
            AssertForceCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobFence.TryComplete(ref handle, forceComplete: true);");
        }

        private static void AssertDisposePathUsesPostSimulationHelper(
            string method,
            string immediateDispose,
            string deferredDispose)
        {
            const string dependencyComplete = "ForceCompleteVegetationDisposeDependencyInPostSimulationWindow(ref dependency);";
            const string disposeComplete = "ForceCompleteVegetationDisposeDependencyInPostSimulationWindow(ref disposeHandle);";

            Assert.That(method, Does.Contain(dependencyComplete));
            Assert.That(method, Does.Contain(disposeComplete));
            Assert.That(method, Does.Not.Contain("DispatcherJobFence.TryComplete(ref dependency, forceComplete: true)"));
            Assert.That(method, Does.Not.Contain("DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)"));
            AssertOrdered(method, dependencyComplete, immediateDispose);
            AssertOrdered(method, deferredDispose, disposeComplete);
        }

        private static void AssertForceCompleteInsidePostSimulationWindow(string method, string completeCall)
        {
            const string beginWindow = "DispatcherJobFence.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostSimulationSwapWindow();";

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, beginWindow);
            Assert.GreaterOrEqual(endIndex, 0, endWindow);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
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
