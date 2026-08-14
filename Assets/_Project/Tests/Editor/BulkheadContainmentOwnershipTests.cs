using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BulkheadContainmentOwnershipTests
    {
        private const string SourcePath = "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs";

        [Test]
        public void PendingDataVaultRebind_FinalizesJobsBeforeReleasingVaultHandles()
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), SourcePath));
            string rebind = ExtractMethodBlock(source, "private bool TryFlushPendingDataVaultRebind()");

            int finalizeIndex = rebind.IndexOf("TryFinalizeBulkheadJobsNoWait()", StringComparison.Ordinal);
            int releaseIndex = rebind.IndexOf("ReleaseVaultHandles()", StringComparison.Ordinal);

            Assert.GreaterOrEqual(finalizeIndex, 0, "Pending DataVault rebind must finalize Bulkhead jobs before ownership changes.");
            Assert.GreaterOrEqual(releaseIndex, 0, "Pending DataVault rebind must release old vault handles.");
            Assert.Less(finalizeIndex, releaseIndex, "Bulkhead jobs must be finalized before their DataVault-backed handles are released.");
        }

        [Test]
        public void ShutdownRuntime_DrainsScheduledJobsBeforeRequestingDataVaultRebind()
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), SourcePath));
            string shutdown = ExtractMethodBlock(source, "private void ShutdownRuntime(bool forceCompletePendingJobs)");

            int drainIndex = shutdown.IndexOf("DrainScheduledJobsForTeardown()", StringComparison.Ordinal);
            int rebindIndex = shutdown.IndexOf("RequestDataVaultRebind(null)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(drainIndex, 0, "Bulkhead shutdown must drain scheduled jobs when teardown requires completion.");
            Assert.GreaterOrEqual(rebindIndex, 0, "Bulkhead shutdown must request DataVault release.");
            Assert.Less(drainIndex, rebindIndex, "Bulkhead jobs must be drained before shutdown requests DataVault handle release.");
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
