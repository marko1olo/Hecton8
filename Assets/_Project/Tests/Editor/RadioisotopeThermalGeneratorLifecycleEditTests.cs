using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RadioisotopeThermalGeneratorLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapCompletesDecayJobBeforeReleasingVaultHandles()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private static void RebindDataVault(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("RebindDataVault(currentService as IDataVault);"));

            Assert.That(rebind, Does.Contain("CompleteDecayJobForTeardown();"));
            Assert.That(rebind, Does.Contain("SetLeaderSlot(-1);"));
            Assert.That(rebind, Does.Contain("DisposeNativeBuffers();"));
            Assert.That(rebind, Does.Contain("s_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("EnsureNativeBuffers();"));
            Assert.That(rebind, Does.Contain("RebuildActiveRuntimeStateFromInstances();"));
            Assert.That(rebind, Does.Contain("RefreshLeader();"));

            Assert.Less(
                rebind.IndexOf("CompleteDecayJobForTeardown();", StringComparison.Ordinal),
                rebind.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("s_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("s_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("EnsureNativeBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("EnsureNativeBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("RebuildActiveRuntimeStateFromInstances();", StringComparison.Ordinal));
        }

        [Test]
        public void DisposeNativeBuffersReleasesPowerOwnedVaultBuffersWithoutAllocatingNewOnes()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string dispose = ExtractMethodBlock(source, "private static void DisposeNativeBuffers()");
            string release = ExtractMethodBlock(source, "private static void ReleaseRtgVaultBuffers(");
            string clear = ExtractMethodBlock(source, "private static void ClearResolvedNativeArray");

            Assert.That(dispose, Does.Contain("ClearResolvedNativeArray(vault, in s_rtgStartTimesHandle);"));
            Assert.That(dispose, Does.Contain("ReleaseRtgVaultBuffers(vault);"));
            Assert.That(dispose, Does.Not.Contain("TryResolveRtgBuffers("));
            Assert.That(release, Does.Contain("BufferID.RtgStartTimes"));
            Assert.That(release, Does.Contain("BufferID.RtgTelemetryRing"));
            Assert.That(clear, Does.Contain("vault.TryResolveHandle(in handle, out NativeArray<T> buffer)"));
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
