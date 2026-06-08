using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ToxicityProducerStaticResetEditTests
    {
        [Test]
        public void StaticToxicityProducerDropCountersResetOnSubsystemRegistration()
        {
            string traumaDispatcher = ReadProjectFile("Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs");
            string survivalSystem = ReadProjectFile("Assets/_Project/Scripts/HectonSurvivalSystem.cs");
            string shinobuMetabolismRuntime = ReadProjectFile("Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs");
            string toxicOutgassingRuntime = ReadProjectFile("Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs");
            string floraInteractionManager = ReadProjectFile("Assets/_Project/Scripts/World/FloraInteractionManager.cs");

            AssertStaticDropReset(
                traumaDispatcher,
                "private static void ResetStaticRuntimeState()",
                "s_x001TraumaDispatcherSignalPushDropCount");
            AssertStaticDropReset(
                survivalSystem,
                "private static void ResetStaticRuntimeState()",
                "s_x001HectonSurvivalSystemSignalPushDropCount");
            AssertStaticDropReset(
                shinobuMetabolismRuntime,
                "private static void ResetStaticRuntimeState()",
                "s_x001ShinobuMetabolismRuntimeSignalPushDropCount");
            AssertStaticDropReset(
                toxicOutgassingRuntime,
                "private static void ResetStaticRuntimeState()",
                "s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount");
            AssertStaticDropReset(
                floraInteractionManager,
                "private static void ResetStaticState()",
                "s_x001FloraInteractionManagerSignalPushDropCount");

            string outgassingReset = ExtractMethodBody(toxicOutgassingRuntime, "private static void ResetStaticRuntimeState()");
            StringAssert.Contains("Instance = null;", outgassingReset);
            AssertSourceOrder(outgassingReset, "Instance = null;", "Volatile.Write(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount, 0);");

            string floraReset = ExtractMethodBody(floraInteractionManager, "private static void ResetStaticState()");
            StringAssert.Contains("s_ActiveRuntimeInstance = null;", floraReset);
            AssertSourceOrder(floraReset, "s_ActiveRuntimeInstance = null;", "Volatile.Write(ref s_x001FloraInteractionManagerSignalPushDropCount, 0);");
            AssertSourceOrder(floraReset, "Volatile.Write(ref s_x001FloraInteractionManagerSignalPushDropCount, 0);", "DroneFleetManager.ClearFloraInteractionManager(null);");
        }

        private static void AssertStaticDropReset(string source, string resetSignature, string counterName)
        {
            string resetBody = ExtractMethodBody(source, resetSignature);
            StringAssert.Contains("using System.Threading;", source);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]", source);
            StringAssert.Contains("Volatile.Write(ref " + counterName + ", 0);", resetBody);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }
    }
}
