using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineAutopilotOwnershipTests
    {
        private const string SourcePath = "Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs";

        [Test]
        public void DataVaultHotSwap_CompletesPendingJobsBeforeReleasingOldVaultHandles()
        {
            string source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), SourcePath));
            string callback = ExtractMethodBlock(source, "void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(");

            int completionIndex = callback.IndexOf("CompletePendingJobsForTeardown();", StringComparison.Ordinal);
            int releaseIndex = callback.IndexOf("ReleaseAutopilotVaultHandles(previousVault ?? _dataVault);", StringComparison.Ordinal);
            int rebindIndex = callback.IndexOf("_dataVault = currentService as IDataVault;", StringComparison.Ordinal);

            Assert.GreaterOrEqual(completionIndex, 0, "Autopilot DataVault hot-swap must complete pending jobs.");
            Assert.GreaterOrEqual(releaseIndex, 0, "Autopilot DataVault hot-swap must release old handles.");
            Assert.GreaterOrEqual(rebindIndex, 0, "Autopilot DataVault hot-swap must bind the replacement vault.");
            Assert.Less(completionIndex, releaseIndex, "Autopilot jobs must complete before their old DataVault handles are released.");
            Assert.Less(releaseIndex, rebindIndex, "Old autopilot vault handles must be released before binding the replacement vault.");
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
