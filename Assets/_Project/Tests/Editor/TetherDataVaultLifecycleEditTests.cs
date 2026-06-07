using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class TetherDataVaultLifecycleEditTests
    {
        [Test]
        public void ManagerRebindsActiveAndPooledInstancesBeforeReplacingCachedVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/TetherManager.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private void RebindTetherInstancesForDataVault(");

            Assert.That(listener, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(listener, Does.Contain("IDataVault currentVault = currentService as IDataVault;"));
            Assert.That(listener, Does.Contain("RebindTetherInstancesForDataVault(currentVault);"));
            Assert.That(listener, Does.Contain("_dataVault = currentVault;"));
            Assert.Less(
                listener.IndexOf("RebindTetherInstancesForDataVault(currentVault);", StringComparison.Ordinal),
                listener.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));

            Assert.That(rebind, Does.Contain("_activeInstances[i]?.RebindDataVault(currentVault);"));
            Assert.That(rebind, Does.Contain("_pooledInstances[i]?.RebindDataVault(currentVault);"));
        }

        [Test]
        public void InstanceDataVaultRebindDisposesOldVaultStateBeforeAssigningNewVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/TetherInstance.cs");
            string rebind = ExtractMethodBlock(source, "internal void RebindDataVault(");

            Assert.That(rebind, Does.Contain("FinalizePendingVerletSolveForBarrier(publishResults: false);"));
            Assert.That(rebind, Does.Contain("DisposeDataVaultCableState();"));
            Assert.That(rebind, Does.Contain("_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("_verletRuntimeInitialized = false;"));
            Assert.That(rebind, Does.Contain("EnsureDataVaultCableState(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));"));

            Assert.Less(
                rebind.IndexOf("FinalizePendingVerletSolveForBarrier(publishResults: false);", StringComparison.Ordinal),
                rebind.IndexOf("DisposeDataVaultCableState();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("DisposeDataVaultCableState();", StringComparison.Ordinal),
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("_verletRuntimeInitialized = false;", StringComparison.Ordinal));
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
