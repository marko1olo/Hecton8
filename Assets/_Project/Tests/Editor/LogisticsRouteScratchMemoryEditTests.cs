using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class LogisticsRouteScratchMemoryEditTests
    {
        [Test]
        public void RouteScratchMutationGuard_ReleasesThroughAcquiredVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Construction/LogisticsRouteScratchMemory.cs");

            StringAssert.Contains("private static IDataVault s_WriteGuardVault;", source);
            StringAssert.Contains("s_WriteGuardVault = vault;", source);
            StringAssert.Contains("IDataVault guardVault = s_WriteGuardVault ?? vault;", source);
            StringAssert.Contains("s_WriteGuardVault = null;", source);
            StringAssert.Contains("guardVault?.ReleaseMutationGuard(RouteScratchMutationGuardMask);", source);

            string releaseMethod = ExtractMethodBody(source, "internal static void ReleaseWriteLocks(IDataVault vault)");
            StringAssert.DoesNotContain("vault.ReleaseMutationGuard(RouteScratchMutationGuardMask);", releaseMethod);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Expected method signature was not found.");

            int nextMethod = source.IndexOf("private static bool TryEnsureHandle", start, StringComparison.Ordinal);
            Assert.Greater(nextMethod, start, "Expected next method boundary was not found.");
            return source.Substring(start, nextMethod - start);
        }
    }
}
