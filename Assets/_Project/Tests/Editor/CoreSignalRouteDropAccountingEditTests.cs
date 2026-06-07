using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CoreSignalRouteDropAccountingEditTests
    {
        private const string ScriptsRoot = "Assets/_Project/Scripts";
        private const string LegacyFacadePath = "Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs";
        private static readonly Regex NakedSignalPushPattern =
            new Regex(@"SignalBus<[^>]+>\.TryPush\s*\(", RegexOptions.Compiled);

        private static readonly string[] CoreRouteFiles =
        {
            "Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs",
            "Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs",
            "Assets/_Project/Scripts/Core/Signals/SessionLifecycleSignalRoute.cs",
            "Assets/_Project/Scripts/Core/Signals/ProgressionMetaSignalRoute.cs",
            "Assets/_Project/Scripts/Core/Signals/ItemLifecycleSignalRoute.cs",
            "Assets/_Project/Scripts/Core/Signals/SignalCorridorMockSignalGenerators.cs"
        };

        [Test]
        public void CoreRoutePublishers_UseTrackedSignalPushes()
        {
            for (int i = 0; i < CoreRouteFiles.Length; i++)
            {
                string path = CoreRouteFiles[i];
                string source = ReadProjectFile(path);

                Assert.IsFalse(
                    source.Contains(".TryPush(in "),
                    path + " must use TryPushTracked so owner-local drop counters stay visible.");
                StringAssert.Contains("TryPushTracked", source);
                StringAssert.Contains("SignalPushDropCount", source);
            }
        }

        [Test]
        public void ProjectSignalPublishers_UseTrackedPushesOutsideLegacyFacade()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string scriptsRoot = Path.Combine(projectRoot, ScriptsRoot);
            List<string> failures = new List<string>();
            foreach (string filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = ToProjectRelativePath(projectRoot, filePath);
                if (string.Equals(relativePath, LegacyFacadePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(filePath);
                Match match = NakedSignalPushPattern.Match(source);
                if (match.Success)
                    failures.Add(relativePath + ": " + match.Value);
            }

            Assert.IsEmpty(failures, "Use SignalBus<T>.TryPushTracked with an owner drop counter outside the explicit legacy facade.");
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ToProjectRelativePath(string projectRoot, string filePath)
        {
            string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedPath = filePath.Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length + 1);

            return normalizedPath;
        }
    }
}
