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
