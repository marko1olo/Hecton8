using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuEcosystemOwnershipTests
    {
        private const string SourcePath = "Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs";

        [Test]
        public void VaultRelease_CompletesFrameJobBeforeReleasingOwnedHandles()
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), SourcePath));
            string release = ExtractMethodBlock(source, "private void ReleaseVaultStateForLifecycle(bool clearRenderState)");

            int completionIndex = release.IndexOf("CompleteFrameJobForTeardown();", StringComparison.Ordinal);
            int releaseIndex = release.IndexOf("ReleaseOwnedVaultHandles(_dataVault);", StringComparison.Ordinal);
            int telemetryMirrorDisposeIndex = release.IndexOf("DisposeTelemetryMirrorsCold();", StringComparison.Ordinal);

            Assert.GreaterOrEqual(completionIndex, 0, "Vault release must complete the active frame job before releasing native ownership.");
            Assert.GreaterOrEqual(releaseIndex, 0, "Vault release must release owned DataVault handles.");
            Assert.GreaterOrEqual(telemetryMirrorDisposeIndex, 0, "Vault release must dispose telemetry mirrors.");
            Assert.Less(completionIndex, releaseIndex, "Frame jobs must complete before their DataVault-backed handles are released.");
            Assert.Less(releaseIndex, telemetryMirrorDisposeIndex, "Telemetry mirrors must be disposed after the owning DataVault handles are released.");
        }

        private static string ExtractMethodBlock(string source, string methodSignature)
        {
            int start = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing method: {methodSignature}");

            int braceStart = source.IndexOf('{' , start);
            Assert.GreaterOrEqual(braceStart, 0, $"Missing method body: {methodSignature}");

            int depth = 0;
            for (int index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                    depth++;
                else if (source[index] == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }

            Assert.Fail($"Unterminated method: {methodSignature}");
            return string.Empty;
        }
    }
}
