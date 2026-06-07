using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CoreSignalRouteDropAccountingEditTests
    {
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

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }
    }
}
