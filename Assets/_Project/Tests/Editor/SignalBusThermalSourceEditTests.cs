using System;
using System.IO;
using System.Reflection;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SignalBusThermalSourceEditTests
    {
        private const int ThermalSourceSignalGuardCode = unchecked((int)0x51A1005Fu);

        [Test]
        public void ThermalSourceSignal_HasInertCentralGuardBeforeSolverIngest()
        {
            string signalBusSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"));
            string solverSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs"));
            string sanitizeBody = ExtractMethodBody(signalBusSource, "private static int SanitizeThermalSourceSignal(");
            string resolveBody = ExtractMethodBody(signalBusSource, "private static byte ResolveGuardKind<T>()");
            string guardSwitchBody = ExtractMethodBody(signalBusSource, "public static int Sanitize<T>(ref T signal)");
            string ingestBody = ExtractMethodBody(solverSource, "private static bool TryIngestThermalSourceSignals(");

            StringAssert.Contains("private const int ThermalSourceSignalGuardCode = unchecked((int)0x51A1005Fu);", signalBusSource);
            StringAssert.Contains("private const byte GuardThermalSource = 95;", signalBusSource);
            StringAssert.Contains("typeof(T) == typeof(ThermalSourceSignal)", resolveBody);
            StringAssert.Contains("return GuardThermalSource;", resolveBody);
            StringAssert.Contains("case GuardThermalSource:", guardSwitchBody);
            StringAssert.Contains("UnsafeUtility.As<T, ThermalSourceSignal>(ref signal)", guardSwitchBody);
            StringAssert.Contains("return SanitizeThermalSourceSignal(ref typed);", guardSwitchBody);

            StringAssert.Contains("bool repairedAup = SanitizeAup(ref signal.PositionAup);", sanitizeBody);
            StringAssert.Contains("bool repairedRadius = SanitizeNonNegative(ref signal.RadiusMeters);", sanitizeBody);
            StringAssert.Contains("bool repairedIntensity = SanitizeNonNegative(ref signal.IntensityCelsiusPerSecond);", sanitizeBody);
            StringAssert.Contains("if (repairedAup || signal.RadiusMeters <= 0f || signal.IntensityCelsiusPerSecond <= 0f)", sanitizeBody);
            StringAssert.Contains("signal.RadiusMeters = 0f;", sanitizeBody);
            StringAssert.Contains("signal.IntensityCelsiusPerSecond = 0f;", sanitizeBody);

            StringAssert.Contains("if (signal.RadiusMeters <= 0f || signal.IntensityCelsiusPerSecond <= 0f)", ingestBody);
            StringAssert.Contains("continue;", ingestBody);
            StringAssert.Contains("uint sourceId = signal.SourceId != 0u ? signal.SourceId : BuildThermalSourceId(in signal);", ingestBody);
            AssertSourceOrder(ingestBody,
                "if (signal.RadiusMeters <= 0f || signal.IntensityCelsiusPerSecond <= 0f)",
                "uint sourceId = signal.SourceId != 0u ? signal.SourceId : BuildThermalSourceId(in signal);");
        }

        [Test]
        public void ThermalSourceSignal_CentralGuardPreservesValidSource()
        {
            ThermalSourceSignal signal = new ThermalSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(12.0, 24.0, 36.0)),
                RadiusMeters = 24f,
                IntensityCelsiusPerSecond = 8f,
                SourceId = 123u,
                Frame = 456u
            };

            int guardCode = InvokeThermalSourceGuard(ref signal);

            Assert.AreEqual(0, guardCode);
            Assert.AreEqual(24f, signal.RadiusMeters);
            Assert.AreEqual(8f, signal.IntensityCelsiusPerSecond);
            Assert.AreEqual(123u, signal.SourceId);
            Assert.AreEqual(456u, signal.Frame);
            Assert.IsTrue(AbsoluteUniversePosition.IsFinite(in signal.PositionAup));
        }

        [Test]
        public void ThermalSourceSignal_CentralGuardZerosBadSourceBeforeIngest()
        {
            ThermalSourceSignal signal = new ThermalSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.Invalid(),
                RadiusMeters = 32f,
                IntensityCelsiusPerSecond = 11f,
                SourceId = 789u,
                Frame = 101112u
            };

            int guardCode = InvokeThermalSourceGuard(ref signal);

            Assert.AreEqual(ThermalSourceSignalGuardCode, guardCode);
            Assert.AreEqual(0f, signal.RadiusMeters);
            Assert.AreEqual(0f, signal.IntensityCelsiusPerSecond);
            Assert.AreEqual(789u, signal.SourceId);
            Assert.AreEqual(101112u, signal.Frame);
            Assert.AreEqual(0f, signal.PositionAup.LocalX);
            Assert.AreEqual(0f, signal.PositionAup.LocalY);
            Assert.AreEqual(0f, signal.PositionAup.LocalZ);
        }

        private static int InvokeThermalSourceGuard(ref ThermalSourceSignal signal)
        {
            Type guardType = typeof(SignalBusRegistry).Assembly.GetType("Hecton8.Core.Contracts.Signals.SignalPayloadFiniteGuards");
            Assert.NotNull(guardType, "Missing SignalPayloadFiniteGuards type.");

            MethodInfo sanitizeMethod = guardType.GetMethod("Sanitize", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(sanitizeMethod, "Missing SignalPayloadFiniteGuards.Sanitize<T> method.");

            MethodInfo closedMethod = sanitizeMethod.MakeGenericMethod(typeof(ThermalSourceSignal));
            object[] args = { signal };
            int guardCode = (int)closedMethod.Invoke(null, args);
            signal = (ThermalSourceSignal)args[0];
            return guardCode;
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
