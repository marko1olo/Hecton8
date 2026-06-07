using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ChemicalInfluenceGridLifecycleEditTests
    {
        [Test]
        public void ResetForRebindUnlocksSimulationGuardBeforeHandleReleaseOrReset()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs");
            string reset = ExtractMethodBlock(source, "private void ResetVaultStateForRebind()");

            Assert.That(reset, Does.Contain("UnlockSimulationBuffers();"));
            Assert.That(reset, Does.Contain("ReleaseVaultHandles(_dataVault);"));
            Assert.That(reset, Does.Contain("_scheduledBuffersGuardVault = null;"));
            Assert.Less(
                reset.IndexOf("UnlockSimulationBuffers();", StringComparison.Ordinal),
                reset.IndexOf("ReleaseVaultHandles(_dataVault);", StringComparison.Ordinal));
            Assert.Less(
                reset.IndexOf("UnlockSimulationBuffers();", StringComparison.Ordinal),
                reset.IndexOf("_scheduledBuffersGuardVault = null;", StringComparison.Ordinal));
        }

        [Test]
        public void DumpTelemetryRing_TracksTransientFaultDumpPayload()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs");
            string dump = ExtractMethodBlock(source, "private void DumpTelemetryRing()");

            Assert.That(source, Does.Contain("private const string TelemetryDumpPayloadLabel = \"chemicalInfluenceTelemetryDumpPayload\";"));
            Assert.That(dump, Does.Contain("NativeFaultDumpWriter.CreateTransientPayload("));
            Assert.That(dump, Does.Contain("VaultOwnerName"));
            Assert.That(dump, Does.Contain("TelemetryDumpPayloadLabel"));
            Assert.That(dump, Does.Contain("NativeArrayOptions.UninitializedMemory"));
            Assert.That(dump, Does.Contain("NativeFaultDumpWriter.DisposeTransientPayload("));
            Assert.That(dump, Does.Not.Contain("new NativeArray<byte>(totalBytes"));
            Assert.That(dump, Does.Not.Contain("payload.Dispose()"));
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
