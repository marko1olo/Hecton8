using System;
using System.IO;
using System.Reflection;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SignalBusRadiationDoseEditTests
    {
        private const int RadiationDoseSignalGuardCode = unchecked((int)0x51A10018u);

        [Test]
        public void RadiationDoseSignal_HasCentralFiniteGuard()
        {
            string signalBusSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"));
            string payloadSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs"));
            string sanitizeBody = ExtractMethodBody(signalBusSource, "private static int SanitizeRadiationDoseSignal(");
            string resolveBody = ExtractMethodBody(signalBusSource, "private static byte ResolveGuardKind<T>()");
            string guardSwitchBody = ExtractMethodBody(signalBusSource, "public static int Sanitize<T>(ref T signal)");
            string doseToUnitBody = ExtractMethodBody(payloadSource, "public static float DoseToUnit01(");

            StringAssert.Contains("private const int RadiationDoseSignalGuardCode = unchecked((int)0x51A10018u);", signalBusSource);
            StringAssert.Contains("private const byte GuardRadiationDose = 24;", signalBusSource);
            StringAssert.Contains("public const float DoseFullScaleRad = 100f;", payloadSource);
            StringAssert.Contains("public const float DoseToUnitScale = 0.01f;", payloadSource);
            StringAssert.Contains("typeof(T) == typeof(RadiationDoseSignal)", resolveBody);
            StringAssert.Contains("return GuardRadiationDose;", resolveBody);
            StringAssert.Contains("case GuardRadiationDose:", guardSwitchBody);
            StringAssert.Contains("UnsafeUtility.As<T, RadiationDoseSignal>(ref signal)", guardSwitchBody);
            StringAssert.Contains("return SanitizeRadiationDoseSignal(ref typed);", guardSwitchBody);
            StringAssert.Contains("SanitizeAup(ref signal.PositionAup)", sanitizeBody);
            StringAssert.Contains("SanitizeNonNegative(ref signal.Dose)", sanitizeBody);
            StringAssert.Contains("SanitizeUnit01(ref signal.Intensity01)", sanitizeBody);
            StringAssert.Contains("if (!math.isfinite(dose) || dose <= 0f)", doseToUnitBody);
            StringAssert.Contains("return 0f;", doseToUnitBody);
            StringAssert.Contains("return math.min(dose, DoseFullScaleRad) * DoseToUnitScale;", doseToUnitBody);
        }

        [Test]
        public void RadiationDoseSignal_DoseToUnit01IsBoundedAndOverflowSafe()
        {
            Assert.AreEqual(0f, RadiationDoseSignal.DoseToUnit01(-1f));
            Assert.AreEqual(0f, RadiationDoseSignal.DoseToUnit01(float.NaN));
            Assert.AreEqual(0f, RadiationDoseSignal.DoseToUnit01(float.PositiveInfinity));
            Assert.AreEqual(0.5f, RadiationDoseSignal.DoseToUnit01(50f));
            Assert.AreEqual(1f, RadiationDoseSignal.DoseToUnit01(100f));
            Assert.AreEqual(1f, RadiationDoseSignal.DoseToUnit01(float.MaxValue));
        }

        [Test]
        public void RadiationDoseSignal_CentralGuardPreservesValidDose()
        {
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(5.0, 6.0, 7.0)),
                Dose = 12.5f,
                Intensity01 = 0.45f,
                SourceId = 321u,
                DoseKind = 9,
                Flags = 3
            };

            int guardCode = InvokeRadiationDoseGuard(ref signal);

            Assert.AreEqual(0, guardCode);
            Assert.AreEqual(12.5f, signal.Dose);
            Assert.AreEqual(0.45f, signal.Intensity01);
            Assert.AreEqual(321u, signal.SourceId);
            Assert.AreEqual(9, signal.DoseKind);
            Assert.AreEqual(3, signal.Flags);
            Assert.IsTrue(AbsoluteUniversePosition.IsFinite(in signal.PositionAup));
        }

        [Test]
        public void RadiationDoseSignal_CentralGuardRepairsBadDoseWithoutChangingIdentity()
        {
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = AbsoluteUniversePosition.Invalid(),
                Dose = float.NaN,
                Intensity01 = 2.5f,
                SourceId = 654u,
                DoseKind = 11,
                Flags = 7
            };

            int guardCode = InvokeRadiationDoseGuard(ref signal);

            Assert.AreEqual(RadiationDoseSignalGuardCode, guardCode);
            Assert.AreEqual(0f, signal.Dose);
            Assert.AreEqual(1f, signal.Intensity01);
            Assert.AreEqual(654u, signal.SourceId);
            Assert.AreEqual(11, signal.DoseKind);
            Assert.AreEqual(7, signal.Flags);
            Assert.AreEqual(0f, signal.PositionAup.LocalX);
            Assert.AreEqual(0f, signal.PositionAup.LocalY);
            Assert.AreEqual(0f, signal.PositionAup.LocalZ);
        }

        private static int InvokeRadiationDoseGuard(ref RadiationDoseSignal signal)
        {
            Type guardType = typeof(SignalBusRegistry).Assembly.GetType("Hecton8.Core.Contracts.Signals.SignalPayloadFiniteGuards");
            Assert.NotNull(guardType, "Missing SignalPayloadFiniteGuards type.");

            MethodInfo sanitizeMethod = guardType.GetMethod("Sanitize", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(sanitizeMethod, "Missing SignalPayloadFiniteGuards.Sanitize<T> method.");

            MethodInfo closedMethod = sanitizeMethod.MakeGenericMethod(typeof(RadiationDoseSignal));
            object[] args = { signal };
            int guardCode = (int)closedMethod.Invoke(null, args);
            signal = (RadiationDoseSignal)args[0];
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
    }
}
