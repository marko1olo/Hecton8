using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldProceduralFieldSamplerLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapCompletesSamplingJobBeforeReleasingVaultState()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralFieldSampler.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private void RebindDataVault(");

            Assert.That(listener, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(listener, Does.Contain("RebindDataVault(currentService as IDataVault);"));
            Assert.Less(
                listener.IndexOf("GlobalRegistryServiceSlot.DataVault", StringComparison.Ordinal),
                listener.IndexOf("GlobalRegistryServiceSlot.Player", StringComparison.Ordinal));

            Assert.That(rebind, Does.Contain("CompletePendingSamplingJobForBarrier();"));
            Assert.That(rebind, Does.Contain("DisposeBurstData();"));
            Assert.That(rebind, Does.Contain("ReleaseBiomeInfluenceGraphicsBuffer();"));
            Assert.That(rebind, Does.Contain("_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("_isDataDirty = true;"));
            Assert.That(rebind, Does.Contain("_samplingFramePrepared = false;"));
            Assert.That(rebind, Does.Contain("ClearSeafloorHeightCache();"));

            Assert.Less(
                rebind.IndexOf("CompletePendingSamplingJobForBarrier();", StringComparison.Ordinal),
                rebind.IndexOf("DisposeBurstData();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("DisposeBurstData();", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseBiomeInfluenceGraphicsBuffer();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseBiomeInfluenceGraphicsBuffer();", StringComparison.Ordinal),
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("_isDataDirty = true;", StringComparison.Ordinal));
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
