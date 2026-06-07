using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class WfcOutpostPowerBootRuntimeLifecycleEditTests
    {
        [Test]
        public void PowerGridManagerRebindsWfcOutpostBootWhenDataVaultChanges()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/PowerGridManager.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("IDataVault currentVault = currentService as IDataVault;"));
            Assert.That(listener, Does.Contain("_wfcOutpostPowerBoot?.RebindDataVault(currentVault);"));
            Assert.That(listener, Does.Contain("InjectDataVaultForAllGrids(currentVault);"));
            Assert.That(listener, Does.Contain("EnsureWfcOutpostPowerBoot();"));

            Assert.Less(
                listener.IndexOf("_wfcOutpostPowerBoot?.RebindDataVault(currentVault);", StringComparison.Ordinal),
                listener.IndexOf("InjectDataVaultForAllGrids(currentVault);", StringComparison.Ordinal));
            Assert.Less(
                listener.IndexOf("InjectDataVaultForAllGrids(currentVault);", StringComparison.Ordinal),
                listener.IndexOf("EnsureWfcOutpostPowerBoot();", StringComparison.Ordinal));
        }

        [Test]
        public void DataVaultHotSwapCompletesTranslationAndReleasesOldVaultBeforeRebuild()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs");
            string rebind = ExtractMethodBlock(source, "public void RebindDataVault(");
            string reset = ExtractMethodBlock(source, "private void ResetRuntimeStateForDataVaultRebind()");

            Assert.That(rebind, Does.Contain("_graph.InjectDataVault(currentVault);"));
            Assert.That(rebind, Does.Contain("ForceCompleteTranslationInPostSimulationWindow(ref translationDependency);"));
            Assert.That(rebind, Does.Contain("ReleasePendingTranslationLocks();"));
            Assert.That(rebind, Does.Contain("ReleaseBuffers();"));
            Assert.That(rebind, Does.Contain("ResetRuntimeStateForDataVaultRebind();"));
            Assert.That(rebind, Does.Contain("_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("EnsureBuffers();"));
            Assert.That(rebind, Does.Contain("TryBindGraphBaseAwakeState();"));

            Assert.Less(
                rebind.IndexOf("ForceCompleteTranslationInPostSimulationWindow(ref translationDependency);", StringComparison.Ordinal),
                rebind.IndexOf("ReleasePendingTranslationLocks();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleasePendingTranslationLocks();", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("ResetRuntimeStateForDataVaultRebind();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ResetRuntimeStateForDataVaultRebind();", StringComparison.Ordinal),
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("EnsureBuffers();", StringComparison.Ordinal));

            Assert.That(reset, Does.Contain("_activeGeneratorNodeIndex = -1;"));
            Assert.That(reset, Does.Contain("_translationHandle = default;"));
            Assert.That(reset, Does.Contain("_translationGridLeaseBufferId = BufferID.Unknown;"));
            Assert.That(reset, Does.Contain("_translationBufferLockMask = 0UL;"));
            Assert.That(reset, Does.Contain("_translationPending = false;"));
            Assert.That(reset, Does.Contain("_graphEvaluationPending = false;"));
            Assert.That(reset, Does.Contain("_hasActiveGraph = false;"));
            Assert.That(reset, Does.Contain("_gasSeedPending = false;"));
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
