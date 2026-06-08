using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class RadiationHazardGridSignalDropTelemetryEditTests
    {
        [Test]
        public void RadiationTelemetryConsumesSignalDropsOnlyAfterSuccessfulEntryWrite()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));
            string recordBody = ExtractMethodBody(source, "private void RecordTelemetry(");
            string consumeBody = ExtractMethodBody(source, "private static uint ConsumeSignalDropFlags(");
            string resetBody = ExtractMethodBody(source, "private static void ResetStaticRuntimeState()");

            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]", source);
            StringAssert.Contains("ActiveRuntimeInstance = null;", resetBody);
            StringAssert.Contains("Volatile.Write(ref _signalPushDropCount, 0);", resetBody);
            StringAssert.Contains("Flags = flags", recordBody);
            StringAssert.Contains("entry.Flags = ConsumeSignalDropFlags(flags);", recordBody);
            AssertSourceOrder(recordBody, "if (telemetryCapacity <= 0)", "entry.Flags = ConsumeSignalDropFlags(flags);");
            AssertSourceOrder(recordBody, "entry.Flags = ConsumeSignalDropFlags(flags);", "telemetryRing[writeIndex] = entry;");
            AssertSourceOrder(recordBody, "telemetryRing[writeIndex] = entry;", "wrote = true;");
            StringAssert.Contains("Interlocked.Exchange(ref _signalPushDropCount, 0)", consumeBody);
            StringAssert.Contains("flags | RadiationTelemetryFlagSignalDrops", consumeBody);
            StringAssert.DoesNotContain("_signalPushDropCount > 0 ? flags | RadiationTelemetryFlagSignalDrops : flags", recordBody);
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
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
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
