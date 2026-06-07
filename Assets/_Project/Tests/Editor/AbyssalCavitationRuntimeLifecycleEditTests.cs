using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AbyssalCavitationRuntimeLifecycleEditTests
    {
        [Test]
        public void RebindDataVaultCompletesScheduledJobAndReleasesOldVaultHandles()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs");
            string rebind = ExtractMethodBlock(source, "public static bool RebindDataVault(");

            Assert.That(rebind, Does.Contain("CompleteScheduledForTeardown()"));
            Assert.That(rebind, Does.Contain("ReleaseSimulationGuard();"));
            Assert.That(rebind, Does.Contain("ReleaseVaultHandles(_vault);"));
            Assert.That(rebind, Does.Contain("_vault = null;"));
            Assert.That(rebind, Does.Contain("_initialized = false;"));
            Assert.That(rebind, Does.Contain("EnsureInitialized(currentVault)"));

            Assert.Less(
                rebind.IndexOf("CompleteScheduledForTeardown()", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseVaultHandles(_vault);", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseSimulationGuard();", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseVaultHandles(_vault);", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseVaultHandles(_vault);", StringComparison.Ordinal),
                rebind.IndexOf("EnsureInitialized(currentVault)", StringComparison.Ordinal));
        }

        [Test]
        public void HostDataVaultHotSwapRebindsRuntimeBeforeReloadingCsv()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("AbyssalCavitationRuntime.RebindDataVault(currentService as IDataVault)"));
            Assert.That(listener, Does.Contain("AbyssalCavitationRuntime.EnsureGraphicsBuffers();"));
            Assert.That(listener, Does.Contain("AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv(forceReload: true);"));
            Assert.Less(
                listener.IndexOf("AbyssalCavitationRuntime.RebindDataVault(currentService as IDataVault)", StringComparison.Ordinal),
                listener.IndexOf("AbyssalCavitationRuntime.EnsureGraphicsBuffers();", StringComparison.Ordinal));
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
