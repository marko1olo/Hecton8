using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VRSomaticProviderLifecycleEditTests
    {
        [Test]
        public void SomaticComfortBarrierForceCompletesBeforeReleasingVaultBuffers()
        {
            string comfortSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs");
            string providerSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs");
            string barrier = ExtractMethodBlock(comfortSource, "private void CompleteSomaticComfortForBarrier()");
            string reset = ExtractMethodBlock(comfortSource, "private void ResetSomaticComfortBuffers()");
            string listener = ExtractMethodBlock(providerSource, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(barrier, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow();"));
            Assert.That(barrier, Does.Contain("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)"));
            Assert.That(barrier, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow();"));
            Assert.That(barrier, Does.Contain("PublishSomaticComfortStateFromWrite();"));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.BeginPostSimulationSwapWindow();", StringComparison.Ordinal),
                barrier.IndexOf("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)", StringComparison.Ordinal));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)", StringComparison.Ordinal),
                barrier.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal),
                barrier.IndexOf("PublishSomaticComfortStateFromWrite();", StringComparison.Ordinal));

            Assert.That(reset, Does.Contain("CompleteSomaticComfortForBarrier();"));
            Assert.That(reset, Does.Contain("_somaticComfortWrite.Release();"));
            Assert.Less(
                reset.IndexOf("CompleteSomaticComfortForBarrier();", StringComparison.Ordinal),
                reset.IndexOf("_somaticComfortWrite.Release();", StringComparison.Ordinal));

            Assert.That(listener, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(listener, Does.Contain("DisposeNativeBuffers();"));
            Assert.That(listener, Does.Contain("_dataVault = currentService as IDataVault;"));
            Assert.Less(
                listener.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal),
                listener.IndexOf("_dataVault = currentService as IDataVault;", StringComparison.Ordinal));
        }

        [Test]
        public void XrRuntimeSubscription_DeduplicatesBeforeSubscribe()
        {
            string providerSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs");
            string subscribe = ExtractMethodBlock(providerSource, "private void TrySubscribeXRRuntime()");

            Assert.That(subscribe, Does.Contain("HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;"));
            Assert.That(subscribe, Does.Contain("HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;"));
            Assert.Less(
                subscribe.IndexOf("HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;", StringComparison.Ordinal),
                subscribe.IndexOf("HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;", StringComparison.Ordinal));
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
