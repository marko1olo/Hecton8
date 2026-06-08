using System;
using System.IO;
using System.Reflection;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SignalBusRadiationSourceEditTests
    {
        private const int RadiationSourceSignalGuardCode = unchecked((int)0x51A1001Au);

        [Test]
        public void RadiationSourceSignal_HasFailClosedCentralGuard()
        {
            string signalBusSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"));
            string sanitizeBody = ExtractMethodBody(signalBusSource, "private static int SanitizeRadiationSourceSignal(");
            string resolveBody = ExtractMethodBody(signalBusSource, "private static byte ResolveGuardKind<T>()");
            string guardSwitchBody = ExtractMethodBody(signalBusSource, "public static int Sanitize<T>(ref T signal)");

            StringAssert.Contains("private const int RadiationSourceSignalGuardCode = unchecked((int)0x51A1001Au);", signalBusSource);
            StringAssert.Contains("private const byte GuardRadiationSource = 26;", signalBusSource);
            StringAssert.Contains("typeof(T) == typeof(RadiationSourceSignal)", resolveBody);
            StringAssert.Contains("return GuardRadiationSource;", resolveBody);
            StringAssert.Contains("case GuardRadiationSource:", guardSwitchBody);
            StringAssert.Contains("UnsafeUtility.As<T, RadiationSourceSignal>(ref signal)", guardSwitchBody);
            StringAssert.Contains("return SanitizeRadiationSourceSignal(ref typed);", guardSwitchBody);

            StringAssert.Contains("bool repairedAup = SanitizeAup(ref signal.PositionAup);", sanitizeBody);
            StringAssert.Contains("bool repairedIntensity = SanitizeNonNegative(ref signal.Intensity);", sanitizeBody);
            StringAssert.Contains("bool repairedRadius = SanitizeNonNegative(ref signal.RadiusMeters);", sanitizeBody);
            StringAssert.Contains("bool knownOperation =", sanitizeBody);
            StringAssert.Contains("signal.Operation == RadiationSourceSignal.OperationUpsert", sanitizeBody);
            StringAssert.Contains("signal.Operation == RadiationSourceSignal.OperationRemove", sanitizeBody);
            StringAssert.Contains("if (!knownOperation)", sanitizeBody);
            StringAssert.Contains("signal.Operation = RadiationSourceSignal.OperationRemove;", sanitizeBody);
            StringAssert.Contains("(repairedAup || signal.Intensity <= 0f || signal.RadiusMeters <= 0f)", sanitizeBody);
            StringAssert.Contains("signal.Intensity = 0f;", sanitizeBody);
            StringAssert.Contains("signal.RadiusMeters = 0f;", sanitizeBody);
            AssertSourceOrder(sanitizeBody, "bool repairedAup = SanitizeAup(ref signal.PositionAup);", "bool knownOperation =");
            AssertSourceOrder(sanitizeBody, "if (!knownOperation)", "if (signal.Operation == RadiationSourceSignal.OperationUpsert");
        }

        [Test]
        public void RadiationSourceSignal_CentralGuardPreservesValidUpsert()
        {
            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(12.0, 24.0, 36.0)),
                Intensity = 0.75f,
                RadiusMeters = 18f,
                SourceId = 123,
                Operation = RadiationSourceSignal.OperationUpsert,
                Flags = 7
            };

            int guardCode = InvokeRadiationSourceGuard(ref signal);

            Assert.AreEqual(0, guardCode);
            Assert.AreEqual(RadiationSourceSignal.OperationUpsert, signal.Operation);
            Assert.AreEqual(0.75f, signal.Intensity);
            Assert.AreEqual(18f, signal.RadiusMeters);
            Assert.AreEqual(123, signal.SourceId);
            Assert.AreEqual(7, signal.Flags);
            Assert.IsTrue(AbsoluteUniversePosition.IsFinite(in signal.PositionAup));
        }

        [Test]
        public void RadiationSourceSignal_CentralGuardFailsClosedBadUpsert()
        {
            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.Invalid(),
                Intensity = 0.5f,
                RadiusMeters = 14f,
                SourceId = 456,
                Operation = RadiationSourceSignal.OperationUpsert,
                Flags = 3
            };

            int guardCode = InvokeRadiationSourceGuard(ref signal);

            Assert.AreEqual(RadiationSourceSignalGuardCode, guardCode);
            Assert.AreEqual(RadiationSourceSignal.OperationRemove, signal.Operation);
            Assert.AreEqual(0f, signal.Intensity);
            Assert.AreEqual(0f, signal.RadiusMeters);
            Assert.AreEqual(456, signal.SourceId);
            Assert.AreEqual(3, signal.Flags);
            Assert.AreEqual(0f, signal.PositionAup.LocalX);
            Assert.AreEqual(0f, signal.PositionAup.LocalY);
            Assert.AreEqual(0f, signal.PositionAup.LocalZ);
        }

        [Test]
        public void RadiationSourceSignal_CentralGuardNormalizesRemoveAndUnknownOperations()
        {
            RadiationSourceSignal removeSignal = new RadiationSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(1.0, 2.0, 3.0)),
                Intensity = 0.25f,
                RadiusMeters = 8f,
                SourceId = 789,
                Operation = RadiationSourceSignal.OperationRemove,
                Flags = 1
            };

            int removeGuardCode = InvokeRadiationSourceGuard(ref removeSignal);

            Assert.AreEqual(RadiationSourceSignalGuardCode, removeGuardCode);
            Assert.AreEqual(RadiationSourceSignal.OperationRemove, removeSignal.Operation);
            Assert.AreEqual(0f, removeSignal.Intensity);
            Assert.AreEqual(0f, removeSignal.RadiusMeters);
            Assert.AreEqual(789, removeSignal.SourceId);
            Assert.AreEqual(1, removeSignal.Flags);

            RadiationSourceSignal unknownSignal = new RadiationSourceSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(4.0, 5.0, 6.0)),
                Intensity = 0.65f,
                RadiusMeters = 16f,
                SourceId = 987,
                Operation = 255,
                Flags = 2
            };

            int unknownGuardCode = InvokeRadiationSourceGuard(ref unknownSignal);

            Assert.AreEqual(RadiationSourceSignalGuardCode, unknownGuardCode);
            Assert.AreEqual(RadiationSourceSignal.OperationRemove, unknownSignal.Operation);
            Assert.AreEqual(0f, unknownSignal.Intensity);
            Assert.AreEqual(0f, unknownSignal.RadiusMeters);
            Assert.AreEqual(987, unknownSignal.SourceId);
            Assert.AreEqual(2, unknownSignal.Flags);
        }

        private static int InvokeRadiationSourceGuard(ref RadiationSourceSignal signal)
        {
            Type guardType = typeof(SignalBusRegistry).Assembly.GetType("Hecton8.Core.Contracts.Signals.SignalPayloadFiniteGuards");
            Assert.NotNull(guardType, "Missing SignalPayloadFiniteGuards type.");

            MethodInfo sanitizeMethod = guardType.GetMethod("Sanitize", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(sanitizeMethod, "Missing SignalPayloadFiniteGuards.Sanitize<T> method.");

            MethodInfo closedMethod = sanitizeMethod.MakeGenericMethod(typeof(RadiationSourceSignal));
            object[] args = { signal };
            int guardCode = (int)closedMethod.Invoke(null, args);
            signal = (RadiationSourceSignal)args[0];
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
