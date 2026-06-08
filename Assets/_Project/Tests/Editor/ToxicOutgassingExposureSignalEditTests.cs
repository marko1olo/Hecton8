using System;
using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts.Signals;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class ToxicOutgassingExposureSignalEditTests
    {
        [Test]
        public void ToxicOutgassingRuntime_PublishesOnlyBoundedExposureSignals()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs"));
            string publishBody = ExtractMethodBody(source, "private void PublishSignals(");
            string prepareBody = ExtractMethodBody(source, "private static bool TryPrepareToxicityExposureSignalForPublish(");
            string biolumPrepareBody = ExtractMethodBody(source, "private static bool TryPrepareToxicBioluminescenceSignalForPublish(");
            string sourceAupBody = ExtractMethodBody(source, "private static bool IsPublishableToxicitySourceAup(");
            string dropBody = ExtractMethodBody(source, "private static void IncrementToxicOutgassingSignalDropCount()");
            string prewarmBody = ExtractMethodBody(source, "private static void PrewarmSignalLanes()");

            StringAssert.Contains("private const byte ToxicityExposureSignalFlagsActive = ToxicityExposureSignal.FlagHasSourceAup;", source);
            StringAssert.Contains("private const byte ToxicBioluminescenceSignalFlagsActive = ToxicBioluminescenceSignal.FlagActive;", source);
            StringAssert.DoesNotContain("SignalFlagsTrilinear", source);
            StringAssert.Contains("ToxicityExposureSignal.ExpectedCapacity", prewarmBody);
            StringAssert.Contains("ToxicityExposureSignal.MaxFrameSignals", prewarmBody);
            StringAssert.Contains("ToxicityExposureSignal.LowTierFrameSignals", prewarmBody);
            StringAssert.Contains("ToxicityExposureSignal.LaneHash", prewarmBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.ExpectedCapacity", prewarmBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.MaxFrameSignals", prewarmBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.LowTierFrameSignals", prewarmBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.LaneHash", prewarmBody);
            StringAssert.Contains("int exposureCount = math.clamp(counters[0], 0, MaxSignalsPerFrame);", publishBody);
            StringAssert.Contains("ToxicityExposureSignal exposure = exposures[i];", publishBody);
            StringAssert.Contains("if (!TryPrepareToxicityExposureSignalForPublish(ref exposure))", publishBody);
            StringAssert.Contains("IncrementToxicOutgassingSignalDropCount();", publishBody);
            StringAssert.Contains("continue;", publishBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);", publishBody);
            AssertSourceOrder(publishBody, "if (!TryPrepareToxicityExposureSignalForPublish(ref exposure))", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            StringAssert.Contains("ToxicBioluminescenceSignal signal = biolums[i];", publishBody);
            StringAssert.Contains("if (!TryPrepareToxicBioluminescenceSignalForPublish(ref signal))", publishBody);
            StringAssert.Contains("SignalBus<ToxicBioluminescenceSignal>.TryPushTracked(in signal, ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);", publishBody);
            AssertSourceOrder(publishBody, "if (!TryPrepareToxicBioluminescenceSignalForPublish(ref signal))", "SignalBus<ToxicBioluminescenceSignal>.TryPushTracked");
            Assert.That(publishBody, Does.Not.Contain("if (math.isfinite(signal.Intensity01))"));

            StringAssert.Contains("if (exposure.EntityId == 0u)", prepareBody);
            StringAssert.Contains("return false;", prepareBody);
            StringAssert.Contains("if (!IsPublishableToxicitySourceAup(exposure.AUP))", prepareBody);
            StringAssert.Contains("if (!math.isfinite(exposure.Exposure01))", prepareBody);
            StringAssert.Contains("exposure.Exposure01 = math.saturate(exposure.Exposure01);", prepareBody);
            StringAssert.Contains("if (exposure.Exposure01 <= 0.0001f)", prepareBody);
            StringAssert.Contains("if (!math.isfinite(exposure.ToxemiaDelta))", prepareBody);
            StringAssert.Contains("exposure.ToxemiaDelta = math.saturate(math.max(0f, exposure.ToxemiaDelta));", prepareBody);
            StringAssert.Contains("exposure.Flags = ToxicityExposureSignalFlagsActive;", prepareBody);
            StringAssert.Contains("exposure._pad0 = 0;", prepareBody);
            StringAssert.Contains("exposure._pad1 = 0;", prepareBody);
            StringAssert.Contains("exposure._pad2 = 0ul;", prepareBody);
            StringAssert.Contains("exposure._pad3 = 0ul;", prepareBody);
            StringAssert.Contains("return true;", prepareBody);
            AssertSourceOrder(prepareBody, "if (!math.isfinite(exposure.Exposure01))", "exposure.Exposure01 = math.saturate");
            AssertSourceOrder(prepareBody, "if (!math.isfinite(exposure.ToxemiaDelta))", "exposure.ToxemiaDelta = math.saturate");
            AssertSourceOrder(prepareBody, "exposure.ToxemiaDelta = math.saturate(math.max(0f, exposure.ToxemiaDelta));", "exposure.Flags = ToxicityExposureSignalFlagsActive;");

            StringAssert.Contains("math.all(math.isfinite(aup))", sourceAupBody);
            StringAssert.Contains("math.lengthsq(aup) > 0.000001d", sourceAupBody);
            StringAssert.Contains("math.abs(aup.x) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", sourceAupBody);
            StringAssert.Contains("math.abs(aup.y) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", sourceAupBody);
            StringAssert.Contains("math.abs(aup.z) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", sourceAupBody);

            StringAssert.Contains("if (!IsPublishableToxicitySourceAup(signal.AUP))", biolumPrepareBody);
            StringAssert.Contains("if (!math.isfinite(signal.Intensity01))", biolumPrepareBody);
            StringAssert.Contains("signal.Intensity01 = math.saturate(signal.Intensity01);", biolumPrepareBody);
            StringAssert.Contains("if (signal.Intensity01 <= 0.0001f)", biolumPrepareBody);
            StringAssert.Contains("if (!math.isfinite(signal.ToxicDensity))", biolumPrepareBody);
            StringAssert.Contains("signal.ToxicDensity = math.max(0f, signal.ToxicDensity);", biolumPrepareBody);
            StringAssert.Contains("if (signal.ToxicDensity <= 0.0001f)", biolumPrepareBody);
            StringAssert.Contains("signal.LocalNormal = math.all(math.isfinite(signal.LocalNormal))", biolumPrepareBody);
            StringAssert.Contains(": float3.zero;", biolumPrepareBody);
            StringAssert.Contains("signal.Flags = ToxicBioluminescenceSignalFlagsActive;", biolumPrepareBody);
            StringAssert.Contains("signal._pad0 = 0;", biolumPrepareBody);
            StringAssert.Contains("signal._pad1 = 0ul;", biolumPrepareBody);

            StringAssert.Contains("Volatile.Read(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount)", dropBody);
            StringAssert.Contains("if (current < int.MaxValue)", dropBody);
            StringAssert.Contains("Interlocked.Increment(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);", dropBody);
        }

        [Test]
        public void ToxicOutgassingRuntime_PreparedExposurePassesCentralToxicityGuard()
        {
            ToxicityExposureSignal signal = new ToxicityExposureSignal
            {
                AUP = new double3(12.0, 34.0, 56.0),
                Exposure01 = 1.5f,
                ToxemiaDelta = 0.25f,
                EntityId = 77u,
                ChemicalHash = 88u,
                Frame = 99u,
                Flags = byte.MaxValue,
                _pad0 = byte.MaxValue,
                _pad1 = ushort.MaxValue,
                _pad2 = ulong.MaxValue,
                _pad3 = ulong.MaxValue
            };

            Assert.IsTrue(InvokePrepareExposureForPublish(ref signal));

            Assert.AreEqual(1f, signal.Exposure01);
            Assert.AreEqual(0.25f, signal.ToxemiaDelta);
            Assert.AreEqual(ToxicityExposureSignal.FlagHasSourceAup, signal.Flags);
            Assert.AreEqual(0, signal._pad0);
            Assert.AreEqual(0, signal._pad1);
            Assert.AreEqual(0ul, signal._pad2);
            Assert.AreEqual(0ul, signal._pad3);
            Assert.AreEqual(0, InvokeCentralToxicityGuard(ref signal));
        }

        [Test]
        public void ToxicOutgassingRuntime_RejectsInvalidSourceAupBeforePublishingActiveFlag()
        {
            ToxicityExposureSignal zeroAup = new ToxicityExposureSignal
            {
                AUP = double3.zero,
                Exposure01 = 0.5f,
                ToxemiaDelta = 0.25f,
                EntityId = 77u
            };
            Assert.IsFalse(InvokePrepareExposureForPublish(ref zeroAup));

            ToxicityExposureSignal outOfRangeAup = new ToxicityExposureSignal
            {
                AUP = new double3(ToxicityExposureSignal.MaxSourceAupExtentMeters + 1.0d, 0.0d, 0.0d),
                Exposure01 = 0.5f,
                ToxemiaDelta = 0.25f,
                EntityId = 77u
            };
            Assert.IsFalse(InvokePrepareExposureForPublish(ref outOfRangeAup));
        }

        [Test]
        public void ToxicOutgassingRuntime_PreparesBioluminescenceSignalsBeforePublishing()
        {
            ToxicBioluminescenceSignal valid = new ToxicBioluminescenceSignal
            {
                AUP = new double3(12.0d, 34.0d, 56.0d),
                Intensity01 = 2f,
                ToxicDensity = 0.5f,
                LocalNormal = new float3(float.NaN, 1f, 0f),
                ChemicalHash = 88u,
                Frame = 99u,
                CellIndex = 7,
                Flags = byte.MaxValue,
                _pad0 = byte.MaxValue,
                _pad1 = ulong.MaxValue
            };

            Assert.IsTrue(InvokePrepareBioluminescenceForPublish(ref valid));
            Assert.AreEqual(1f, valid.Intensity01);
            Assert.AreEqual(0.5f, valid.ToxicDensity);
            Assert.IsTrue(math.all(valid.LocalNormal == float3.zero));
            Assert.AreEqual(ToxicBioluminescenceSignal.FlagActive, valid.Flags);
            Assert.AreEqual(0, valid._pad0);
            Assert.AreEqual(0ul, valid._pad1);

            ToxicBioluminescenceSignal invalidAup = valid;
            invalidAup.AUP = new double3(0.0d, 0.0d, 0.0d);
            Assert.IsFalse(InvokePrepareBioluminescenceForPublish(ref invalidAup));

            ToxicBioluminescenceSignal invalidDensity = valid;
            invalidDensity.ToxicDensity = float.PositiveInfinity;
            Assert.IsFalse(InvokePrepareBioluminescenceForPublish(ref invalidDensity));

            ToxicBioluminescenceSignal zeroDensity = valid;
            zeroDensity.ToxicDensity = 0f;
            Assert.IsFalse(InvokePrepareBioluminescenceForPublish(ref zeroDensity));
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

        private static bool InvokePrepareExposureForPublish(ref ToxicityExposureSignal signal)
        {
            MethodInfo method = typeof(ToxicOutgassingChemistryRuntime).GetMethod(
                "TryPrepareToxicityExposureSignalForPublish",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, "Missing ToxicOutgassingChemistryRuntime.TryPrepareToxicityExposureSignalForPublish.");

            object[] args = { signal };
            bool result = (bool)method.Invoke(null, args);
            signal = (ToxicityExposureSignal)args[0];
            return result;
        }

        private static bool InvokePrepareBioluminescenceForPublish(ref ToxicBioluminescenceSignal signal)
        {
            MethodInfo method = typeof(ToxicOutgassingChemistryRuntime).GetMethod(
                "TryPrepareToxicBioluminescenceSignalForPublish",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, "Missing ToxicOutgassingChemistryRuntime.TryPrepareToxicBioluminescenceSignalForPublish.");

            object[] args = { signal };
            bool result = (bool)method.Invoke(null, args);
            signal = (ToxicBioluminescenceSignal)args[0];
            return result;
        }

        private static int InvokeCentralToxicityGuard(ref ToxicityExposureSignal signal)
        {
            Type guardType = typeof(SignalBusRegistry).Assembly.GetType("Hecton8.Core.Contracts.Signals.SignalPayloadFiniteGuards");
            Assert.NotNull(guardType, "Missing SignalPayloadFiniteGuards type.");

            MethodInfo sanitizeMethod = guardType.GetMethod("Sanitize", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(sanitizeMethod, "Missing SignalPayloadFiniteGuards.Sanitize<T> method.");

            MethodInfo closedMethod = sanitizeMethod.MakeGenericMethod(typeof(ToxicityExposureSignal));
            object[] args = { signal };
            int guardCode = (int)closedMethod.Invoke(null, args);
            signal = (ToxicityExposureSignal)args[0];
            return guardCode;
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
