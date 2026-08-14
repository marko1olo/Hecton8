using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class PowerGridManagerOwnershipTests
    {
        private const string SourcePath = "Assets/_Project/Scripts/PowerGridManager.cs";

        [Test]
        public void ShutdownServiceState_CompletesPendingJobsBeforeReleasingVaultBackends()
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), SourcePath));
            string shutdown = ExtractMethodBlock(source, "private void ShutdownServiceState()");

            int completionIndex = shutdown.IndexOf("CompletePendingSlowTickEvaluationsForTeardown();", StringComparison.Ordinal);
            int backendDisposeIndex = shutdown.IndexOf("_wfcOutpostPowerBoot?.Dispose();", StringComparison.Ordinal);
            int vaultReleaseIndex = shutdown.IndexOf("ReleaseJacobiPowerVaultBuffers(_jacobiVaultOwner);", StringComparison.Ordinal);

            Assert.GreaterOrEqual(completionIndex, 0, "Service shutdown must complete pending slow-tick and thermal jobs.");
            Assert.GreaterOrEqual(backendDisposeIndex, 0, "Service shutdown must dispose runtime backends.");
            Assert.GreaterOrEqual(vaultReleaseIndex, 0, "Service shutdown must release Jacobi DataVault buffers.");
            Assert.Less(completionIndex, backendDisposeIndex, "Pending jobs must complete before disposing their runtime backends.");
            Assert.Less(completionIndex, vaultReleaseIndex, "Pending jobs must complete before their Jacobi DataVault buffers are released.");
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
