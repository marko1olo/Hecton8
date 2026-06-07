using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HullIntegrityRuntimeLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapReleasesHandlesAndReinitializesMockState()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs");
            string rebind = ExtractMethodBlock(source, "private void RebindRegistryDependency(");

            Assert.That(rebind, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(rebind, Does.Contain("DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true);"));
            Assert.That(rebind, Does.Contain("ReleaseVaultHandles();"));
            Assert.That(rebind, Does.Contain("_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("_mockGenerated = 0;"));
            Assert.That(rebind, Does.Contain("TryInitialize()"));
            Assert.That(rebind, Does.Contain("TryRegisterTickables();"));

            Assert.Less(
                rebind.IndexOf("DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true);", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseVaultHandles();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseVaultHandles();", StringComparison.Ordinal),
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("_mockGenerated = 0;", StringComparison.Ordinal),
                rebind.IndexOf("TryInitialize()", StringComparison.Ordinal));
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
