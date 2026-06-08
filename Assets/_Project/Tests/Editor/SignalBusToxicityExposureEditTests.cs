using System;
using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts.Signals;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SignalBusToxicityExposureEditTests
    {
        private const int ToxicityExposureSignalGuardCode = unchecked((int)0x51A10069u);
        private const int ToxicBioluminescenceSignalGuardCode = unchecked((int)0x51A1006Bu);

        [Test]
        public void ToxicityExposureSignal_HasCentralFiniteGuard()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"));
            string sanitizeBody = ExtractMethodBody(source, "private static int SanitizeToxicityExposureSignal(");
            string resolveBody = ExtractMethodBody(source, "private static byte ResolveGuardKind<T>()");
            string guardSwitchBody = ExtractMethodBody(source, "public static int Sanitize<T>(ref T signal)");
            string defaultContractBody = ExtractMethodBody(source, "internal static bool TryResolveDefaultContract(");

            StringAssert.Contains("using ToxicityExposureSignal = Hecton8.Atmosphere.ToxicityExposureSignal;", source);
            StringAssert.Contains("private const int ToxicityExposureSignalGuardCode = unchecked((int)0x51A10069u);", source);
            StringAssert.Contains("private const byte GuardToxicityExposure = 105;", source);
            StringAssert.Contains("case GuardToxicityExposure:", guardSwitchBody);
            StringAssert.Contains("UnsafeUtility.As<T, ToxicityExposureSignal>(ref signal)", guardSwitchBody);
            StringAssert.Contains("return SanitizeToxicityExposureSignal(ref typed);", guardSwitchBody);
            StringAssert.Contains("typeof(T) == typeof(ToxicityExposureSignal)", resolveBody);
            StringAssert.Contains("return GuardToxicityExposure;", resolveBody);
            StringAssert.Contains("expectedCapacity = ToxicityExposureSignal.ExpectedCapacity;", defaultContractBody);
            StringAssert.Contains("maxFrameSignals = ToxicityExposureSignal.MaxFrameSignals;", defaultContractBody);
            StringAssert.Contains("lowTierFrameSignals = ToxicityExposureSignal.LowTierFrameSignals;", defaultContractBody);
            StringAssert.Contains("laneHash = ToxicityExposureSignal.LaneHash;", defaultContractBody);
            StringAssert.Contains("bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);", sanitizeBody);
            StringAssert.Contains("bool outOfRangeAup =", sanitizeBody);
            StringAssert.Contains("math.abs(signal.AUP.x) > ToxicityExposureSignal.MaxSourceAupExtentMeters", sanitizeBody);
            StringAssert.Contains("signal.AUP = double3.zero;", sanitizeBody);
            StringAssert.Contains("int guardCode = repairedAup || outOfRangeAup ? ToxicityExposureSignalGuardCode : 0;", sanitizeBody);
            StringAssert.Contains("SanitizeUnit01(ref signal.Exposure01)", sanitizeBody);
            StringAssert.Contains("SanitizeUnit01(ref signal.ToxemiaDelta)", sanitizeBody);
            StringAssert.Contains("byte supportedFlags = ToxicityExposureSignal.FlagHasSourceAup;", sanitizeBody);
            StringAssert.Contains("byte flags = (byte)(signal.Flags & supportedFlags);", sanitizeBody);
            StringAssert.Contains("bool hasInvalidSourceAup = (flags & ToxicityExposureSignal.FlagHasSourceAup) != 0 &&", sanitizeBody);
            StringAssert.Contains("math.lengthsq(signal.AUP) <= 0.000001d;", sanitizeBody);
            StringAssert.Contains("if (repairedAup || outOfRangeAup || hasInvalidSourceAup)", sanitizeBody);
            StringAssert.Contains("flags = (byte)(flags & ~ToxicityExposureSignal.FlagHasSourceAup);", sanitizeBody);
            StringAssert.Contains("if (signal.Flags != flags)", sanitizeBody);
            StringAssert.Contains("signal.Flags = flags;", sanitizeBody);
            StringAssert.Contains("signal._pad0 = 0;", sanitizeBody);
            StringAssert.Contains("signal._pad1 = 0;", sanitizeBody);
            StringAssert.Contains("signal._pad2 = 0ul;", sanitizeBody);
            StringAssert.Contains("signal._pad3 = 0ul;", sanitizeBody);
            AssertSourceOrder(sanitizeBody, "bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);", "bool outOfRangeAup =");
            AssertSourceOrder(sanitizeBody, "bool outOfRangeAup =", "byte supportedFlags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(sanitizeBody, "bool hasInvalidSourceAup", "if (repairedAup || outOfRangeAup || hasInvalidSourceAup)");
            AssertSourceOrder(sanitizeBody, "if (repairedAup || outOfRangeAup || hasInvalidSourceAup)", "signal.Flags = flags;");
        }

        [Test]
        public void ToxicBioluminescenceSignal_HasCentralFiniteGuardAndLanePolicy()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"));
            string sanitizeBody = ExtractMethodBody(source, "private static int SanitizeToxicBioluminescenceSignal(");
            string resolveBody = ExtractMethodBody(source, "private static byte ResolveGuardKind<T>()");
            string guardSwitchBody = ExtractMethodBody(source, "public static int Sanitize<T>(ref T signal)");
            string nonCriticalBody = ExtractMethodBody(source, "private static bool ResolveNonCriticalVfx()");
            string defaultContractBody = ExtractMethodBody(source, "internal static bool TryResolveDefaultContract(");

            StringAssert.Contains("using ToxicBioluminescenceSignal = Hecton8.Atmosphere.ToxicBioluminescenceSignal;", source);
            StringAssert.Contains("private const int ToxicBioluminescenceSignalGuardCode = unchecked((int)0x51A1006Bu);", source);
            StringAssert.Contains("private const byte GuardToxicBioluminescence = 107;", source);
            StringAssert.Contains("case GuardToxicBioluminescence:", guardSwitchBody);
            StringAssert.Contains("UnsafeUtility.As<T, ToxicBioluminescenceSignal>(ref signal)", guardSwitchBody);
            StringAssert.Contains("return SanitizeToxicBioluminescenceSignal(ref typed);", guardSwitchBody);
            StringAssert.Contains("typeof(T) == typeof(ToxicBioluminescenceSignal)", resolveBody);
            StringAssert.Contains("return GuardToxicBioluminescence;", resolveBody);
            StringAssert.Contains("type == typeof(ToxicBioluminescenceSignal)", nonCriticalBody);
            StringAssert.Contains("expectedCapacity = ToxicBioluminescenceSignal.ExpectedCapacity;", defaultContractBody);
            StringAssert.Contains("maxFrameSignals = ToxicBioluminescenceSignal.MaxFrameSignals;", defaultContractBody);
            StringAssert.Contains("lowTierFrameSignals = ToxicBioluminescenceSignal.LowTierFrameSignals;", defaultContractBody);
            StringAssert.Contains("laneHash = ToxicBioluminescenceSignal.LaneHash;", defaultContractBody);

            StringAssert.Contains("bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);", sanitizeBody);
            StringAssert.Contains("bool outOfRangeAup =", sanitizeBody);
            StringAssert.Contains("math.abs(signal.AUP.x) > ToxicityExposureSignal.MaxSourceAupExtentMeters", sanitizeBody);
            StringAssert.Contains("signal.AUP = double3.zero;", sanitizeBody);
            StringAssert.Contains("int guardCode = repairedAup || outOfRangeAup ? ToxicBioluminescenceSignalGuardCode : 0;", sanitizeBody);
            StringAssert.Contains("SanitizeUnit01(ref signal.Intensity01)", sanitizeBody);
            StringAssert.Contains("SanitizeNonNegative(ref signal.ToxicDensity)", sanitizeBody);
            StringAssert.Contains("SanitizeFloat3Zero(ref signal.LocalNormal)", sanitizeBody);
            StringAssert.Contains("byte supportedFlags = ToxicBioluminescenceSignal.FlagActive;", sanitizeBody);
            StringAssert.Contains("byte flags = (byte)(signal.Flags & supportedFlags);", sanitizeBody);
            StringAssert.Contains("bool hasInvalidSourceAup = (flags & ToxicBioluminescenceSignal.FlagActive) != 0 &&", sanitizeBody);
            StringAssert.Contains("math.lengthsq(signal.AUP) <= 0.000001d;", sanitizeBody);
            StringAssert.Contains("bool hasInactiveScalar = (flags & ToxicBioluminescenceSignal.FlagActive) != 0 &&", sanitizeBody);
            StringAssert.Contains("signal.Intensity01 <= 0.0001f || signal.ToxicDensity <= 0.0001f", sanitizeBody);
            StringAssert.Contains("flags = (byte)(flags & ~ToxicBioluminescenceSignal.FlagActive);", sanitizeBody);
            StringAssert.Contains("signal.Flags = flags;", sanitizeBody);
            StringAssert.Contains("signal._pad0 = 0;", sanitizeBody);
            StringAssert.Contains("signal._pad1 = 0ul;", sanitizeBody);
            AssertSourceOrder(sanitizeBody, "bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);", "bool outOfRangeAup =");
            AssertSourceOrder(sanitizeBody, "bool outOfRangeAup =", "byte supportedFlags = ToxicBioluminescenceSignal.FlagActive;");
            AssertSourceOrder(sanitizeBody, "bool hasInactiveScalar", "if (repairedAup || outOfRangeAup || hasInvalidSourceAup || hasInactiveScalar)");
            AssertSourceOrder(sanitizeBody, "if (repairedAup || outOfRangeAup || hasInvalidSourceAup || hasInactiveScalar)", "signal.Flags = flags;");
        }

        [Test]
        public void ToxicityExposureSignal_CentralGuardRepairsBadPayload()
        {
            ToxicityExposureSignal signal = new ToxicityExposureSignal
            {
                AUP = new double3(double.NaN, 5.0, double.PositiveInfinity),
                Exposure01 = 2.5f,
                ToxemiaDelta = float.NaN,
                EntityId = 123u,
                ChemicalHash = 456u,
                Frame = 789u,
                Flags = byte.MaxValue,
                _pad0 = byte.MaxValue,
                _pad1 = ushort.MaxValue,
                _pad2 = ulong.MaxValue,
                _pad3 = ulong.MaxValue
            };

            int guardCode = InvokeToxicityExposureGuard(ref signal);

            Assert.AreEqual(ToxicityExposureSignalGuardCode, guardCode);
            Assert.IsTrue(math.all(signal.AUP == double3.zero));
            Assert.AreEqual(1f, signal.Exposure01);
            Assert.AreEqual(0f, signal.ToxemiaDelta);
            Assert.AreEqual(123u, signal.EntityId);
            Assert.AreEqual(456u, signal.ChemicalHash);
            Assert.AreEqual(789u, signal.Frame);
            Assert.AreEqual(0, signal.Flags);
            Assert.AreEqual(0, signal._pad0);
            Assert.AreEqual(0, signal._pad1);
            Assert.AreEqual(0ul, signal._pad2);
            Assert.AreEqual(0ul, signal._pad3);
        }

        [Test]
        public void ToxicityExposureSignal_CentralGuardStripsSourceFlagWhenAupIsZero()
        {
            ToxicityExposureSignal signal = new ToxicityExposureSignal
            {
                AUP = double3.zero,
                Exposure01 = 0.5f,
                ToxemiaDelta = 0.25f,
                EntityId = 123u,
                ChemicalHash = 456u,
                Frame = 789u,
                Flags = ToxicityExposureSignal.FlagHasSourceAup
            };

            int guardCode = InvokeToxicityExposureGuard(ref signal);

            Assert.AreEqual(ToxicityExposureSignalGuardCode, guardCode);
            Assert.IsTrue(math.all(signal.AUP == double3.zero));
            Assert.AreEqual(0.5f, signal.Exposure01);
            Assert.AreEqual(0.25f, signal.ToxemiaDelta);
            Assert.AreEqual(123u, signal.EntityId);
            Assert.AreEqual(456u, signal.ChemicalHash);
            Assert.AreEqual(789u, signal.Frame);
            Assert.AreEqual(0, signal.Flags);
        }

        [Test]
        public void ToxicityExposureSignal_CentralGuardStripsSourceFlagWhenAupIsOutOfRange()
        {
            ToxicityExposureSignal signal = new ToxicityExposureSignal
            {
                AUP = new double3(250000.0, 2.0, 3.0),
                Exposure01 = 0.5f,
                ToxemiaDelta = 0.25f,
                EntityId = 123u,
                ChemicalHash = 456u,
                Frame = 789u,
                Flags = ToxicityExposureSignal.FlagHasSourceAup
            };

            int guardCode = InvokeToxicityExposureGuard(ref signal);

            Assert.AreEqual(ToxicityExposureSignalGuardCode, guardCode);
            Assert.IsTrue(math.all(signal.AUP == double3.zero));
            Assert.AreEqual(0.5f, signal.Exposure01);
            Assert.AreEqual(0.25f, signal.ToxemiaDelta);
            Assert.AreEqual(0, signal.Flags);
        }

        [Test]
        public void ToxicityExposureSignal_CentralGuardPreservesValidSourceFlag()
        {
            ToxicityExposureSignal signal = new ToxicityExposureSignal
            {
                AUP = new double3(1.0, 2.0, 3.0),
                Exposure01 = 0.25f,
                ToxemiaDelta = 0.75f,
                EntityId = 12u,
                ChemicalHash = 34u,
                Frame = 56u,
                Flags = ToxicityExposureSignal.FlagHasSourceAup
            };

            int guardCode = InvokeToxicityExposureGuard(ref signal);

            Assert.AreEqual(0, guardCode);
            Assert.IsTrue(math.all(signal.AUP == new double3(1.0, 2.0, 3.0)));
            Assert.AreEqual(0.25f, signal.Exposure01);
            Assert.AreEqual(0.75f, signal.ToxemiaDelta);
            Assert.AreEqual(ToxicityExposureSignal.FlagHasSourceAup, signal.Flags);
        }

        [Test]
        public void ToxicBioluminescenceSignal_CentralGuardRepairsBadPayload()
        {
            ToxicBioluminescenceSignal signal = new ToxicBioluminescenceSignal
            {
                AUP = new double3(250000.0, 5.0, 6.0),
                Intensity01 = 2.5f,
                ToxicDensity = float.NaN,
                LocalNormal = new float3(float.PositiveInfinity, 1f, 0f),
                ChemicalHash = 456u,
                Frame = 789u,
                CellIndex = 12,
                Flags = byte.MaxValue,
                _pad0 = byte.MaxValue,
                _pad1 = ulong.MaxValue
            };

            int guardCode = InvokeToxicBioluminescenceGuard(ref signal);

            Assert.AreEqual(ToxicBioluminescenceSignalGuardCode, guardCode);
            Assert.IsTrue(math.all(signal.AUP == double3.zero));
            Assert.AreEqual(1f, signal.Intensity01);
            Assert.AreEqual(0f, signal.ToxicDensity);
            Assert.IsTrue(math.all(signal.LocalNormal == float3.zero));
            Assert.AreEqual(456u, signal.ChemicalHash);
            Assert.AreEqual(789u, signal.Frame);
            Assert.AreEqual(12, signal.CellIndex);
            Assert.AreEqual(0, signal.Flags);
            Assert.AreEqual(0, signal._pad0);
            Assert.AreEqual(0ul, signal._pad1);
        }

        [Test]
        public void ToxicBioluminescenceSignal_CentralGuardPreservesValidActiveSource()
        {
            ToxicBioluminescenceSignal signal = new ToxicBioluminescenceSignal
            {
                AUP = new double3(1.0, 2.0, 3.0),
                Intensity01 = 0.25f,
                ToxicDensity = 0.75f,
                LocalNormal = new float3(0f, 1f, 0f),
                ChemicalHash = 34u,
                Frame = 56u,
                CellIndex = 7,
                Flags = ToxicBioluminescenceSignal.FlagActive
            };

            int guardCode = InvokeToxicBioluminescenceGuard(ref signal);

            Assert.AreEqual(0, guardCode);
            Assert.IsTrue(math.all(signal.AUP == new double3(1.0, 2.0, 3.0)));
            Assert.AreEqual(0.25f, signal.Intensity01);
            Assert.AreEqual(0.75f, signal.ToxicDensity);
            Assert.IsTrue(math.all(signal.LocalNormal == new float3(0f, 1f, 0f)));
            Assert.AreEqual(ToxicBioluminescenceSignal.FlagActive, signal.Flags);
        }

        [Test]
        public void ToxicBioluminescenceSignal_CentralGuardStripsActiveFlagWhenScalarIsZero()
        {
            ToxicBioluminescenceSignal signal = new ToxicBioluminescenceSignal
            {
                AUP = new double3(1.0, 2.0, 3.0),
                Intensity01 = 0f,
                ToxicDensity = 0.75f,
                LocalNormal = new float3(0f, 1f, 0f),
                ChemicalHash = 34u,
                Frame = 56u,
                CellIndex = 7,
                Flags = ToxicBioluminescenceSignal.FlagActive
            };

            int guardCode = InvokeToxicBioluminescenceGuard(ref signal);

            Assert.AreEqual(ToxicBioluminescenceSignalGuardCode, guardCode);
            Assert.IsTrue(math.all(signal.AUP == new double3(1.0, 2.0, 3.0)));
            Assert.AreEqual(0f, signal.Intensity01);
            Assert.AreEqual(0.75f, signal.ToxicDensity);
            Assert.AreEqual(0, signal.Flags);
        }

        [Test]
        public void ToxicityExposureSignal_HasStableRuntimeLayoutAndLaneContract()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs"));
            string structBody = ExtractMethodBody(source, "public struct ToxicityExposureSignal : ISignal");

            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 64)]", source);
            StringAssert.Contains("public const int ExpectedCapacity = 64;", structBody);
            StringAssert.Contains("public const int MaxFrameSignals = 64;", structBody);
            StringAssert.Contains("public const int LowTierFrameSignals = 16;", structBody);
            StringAssert.Contains("public const uint LaneHash = 0x54584F58u;", structBody);
            StringAssert.Contains("public const uint PlayerEntityFallbackHash = 0x504C5952u;", structBody);
            StringAssert.Contains("public const double MaxSourceAupExtentMeters = 100000.0d;", structBody);
            StringAssert.Contains("public const byte FlagHasSourceAup = 1 << 0;", structBody);
            StringAssert.Contains("[FieldOffset(0)] public double3 AUP;", structBody);
            StringAssert.Contains("[FieldOffset(24)] public float Exposure01;", structBody);
            StringAssert.Contains("[FieldOffset(28)] public float ToxemiaDelta;", structBody);
            StringAssert.Contains("[FieldOffset(32)] public uint EntityId;", structBody);
            StringAssert.Contains("[FieldOffset(36)] public uint ChemicalHash;", structBody);
            StringAssert.Contains("[FieldOffset(40)] public uint Frame;", structBody);
            StringAssert.Contains("[FieldOffset(44)] public byte Flags;", structBody);
            StringAssert.Contains("[FieldOffset(48)] public ulong _pad2;", structBody);
            StringAssert.Contains("[FieldOffset(56)] public ulong _pad3;", structBody);
        }

        [Test]
        public void ToxicBioluminescenceSignal_HasStableRuntimeLayoutAndLaneContract()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs"));
            string structBody = ExtractMethodBody(source, "public struct ToxicBioluminescenceSignal : ISignal");

            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 64)]", source);
            StringAssert.Contains("public const int ExpectedCapacity = 64;", structBody);
            StringAssert.Contains("public const int MaxFrameSignals = 64;", structBody);
            StringAssert.Contains("public const int LowTierFrameSignals = 16;", structBody);
            StringAssert.Contains("public const uint LaneHash = 0x54424C4Du;", structBody);
            StringAssert.Contains("public const byte FlagActive = 1 << 0;", structBody);
            StringAssert.Contains("[FieldOffset(0)] public double3 AUP;", structBody);
            StringAssert.Contains("[FieldOffset(24)] public float Intensity01;", structBody);
            StringAssert.Contains("[FieldOffset(28)] public float ToxicDensity;", structBody);
            StringAssert.Contains("[FieldOffset(32)] public float3 LocalNormal;", structBody);
            StringAssert.Contains("[FieldOffset(44)] public uint ChemicalHash;", structBody);
            StringAssert.Contains("[FieldOffset(48)] public uint Frame;", structBody);
            StringAssert.Contains("[FieldOffset(52)] public ushort CellIndex;", structBody);
            StringAssert.Contains("[FieldOffset(54)] public byte Flags;", structBody);
            StringAssert.Contains("[FieldOffset(55)] public byte _pad0;", structBody);
            StringAssert.Contains("[FieldOffset(56)] public ulong _pad1;", structBody);
        }

        [Test]
        public void ToxicSignalTuningCsvHotSwap_ResolvesHexLaneRowsAsNumericHashes()
        {
            Assert.IsTrue(SignalTuningCsvHotSwap.TryResolveLaneHashForEditor("0x54584F58", out uint toxicityLaneHash));
            Assert.AreEqual(ToxicityExposureSignal.LaneHash, toxicityLaneHash);

            Assert.IsTrue(SignalTuningCsvHotSwap.TryResolveLaneHashForEditor(" 0x54424c4d ", out uint biolumLaneHash));
            Assert.AreEqual(ToxicBioluminescenceSignal.LaneHash, biolumLaneHash);

            Assert.IsTrue(SignalTuningCsvHotSwap.TryResolveLaneHashForEditor("ToxicBioluminescenceSignal", out uint labelHash));
            Assert.AreNotEqual(ToxicBioluminescenceSignal.LaneHash, labelHash);

            Assert.IsFalse(SignalTuningCsvHotSwap.TryResolveLaneHashForEditor("0x100000000", out _));
            Assert.IsFalse(SignalTuningCsvHotSwap.TryResolveLaneHashForEditor("4294967296", out _));

            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"));
            Assert.AreEqual(2, CountOccurrences(source, "if (value > 0x0FFFFFFFu)"));
            Assert.AreEqual(2, CountOccurrences(source, "if (value > (uint.MaxValue - digit) / 10u)"));
        }

        [Test]
        public void ToxicSignalTuningCsvHotSwap_RejectsOverflowingRadiusFields()
        {
            Assert.IsTrue(SignalTuningCsvHotSwap.TryParseRadiusForEditor("1.0", out float unitRadius));
            Assert.AreEqual(1f, unitRadius);

            Assert.IsTrue(SignalTuningCsvHotSwap.TryParseRadiusForEditor(" 0.125 ", out float fractionalRadius));
            Assert.AreEqual(0.125f, fractionalRadius, 0.0001f);

            Assert.IsFalse(SignalTuningCsvHotSwap.TryParseRadiusForEditor("42949672960", out _));
            Assert.IsFalse(SignalTuningCsvHotSwap.TryParseRadiusForEditor("0.12345678901234567890", out _));
            Assert.IsFalse(SignalTuningCsvHotSwap.TryParseRadiusForEditor("1.2.3", out _));

            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"));
            StringAssert.Contains("if (fraction > (uint.MaxValue - digit) / 10u || fractionScale > uint.MaxValue / 10u)", source);
            StringAssert.Contains("if (whole > (uint.MaxValue - digit) / 10u)", source);
        }

        [Test]
        public void SignalTuning_HasSourceDataRowsForFallbackProfiles()
        {
            string csv = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_SourceData/Signals/signal_tuning_profiles.csv"));

            StringAssert.Contains("AcousticPingSignal,16,128,1.0,40", csv);
            StringAssert.Contains("CombatDamageSignal,16,128,1.0,100", csv);
            StringAssert.Contains("SignalWardenMockDamageSignal,8,64,0.5,100", csv);
            StringAssert.Contains("MockPlayerFootstepSignal,4,48,1.5,10", csv);
            StringAssert.Contains("0x54584F58,16,64,1.0,100", csv);
            StringAssert.Contains("0x54424C4D,4,64,1.0,20", csv);
        }

        [Test]
        public void ToxicSignals_HaveColdFallbackTuningAndPriorityRows()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"));
            string priorityBody = ExtractMethodBody(source, "public static void ConstructFallbackSignalPriorities()");
            string tuningInitBody = ExtractMethodBody(source, "public static void Initialize(IDataVault vault)");

            StringAssert.Contains("using Hecton8.Atmosphere;", source);
            Assert.AreEqual(2, CountOccurrences(source, "private const int DefaultToxicityExposurePriority = 100;"));
            Assert.AreEqual(2, CountOccurrences(source, "private const int DefaultToxicBioluminescencePriority = 20;"));
            StringAssert.Contains("private const int DefaultToxicBioluminescenceMinFrameSignals = 4;", source);
            StringAssert.Contains("private const int DefaultSignalWardenMockDamageMinFrameSignals = 8;", source);
            StringAssert.Contains("private const int DefaultSignalWardenMockDamageMaxFrameSignals = 64;", source);
            StringAssert.Contains("private const float DefaultSignalWardenMockDamageCoalescingRadiusMeters = 0.5f;", source);
            StringAssert.Contains("private const int DefaultSignalWardenMockDamagePriority = 100;", source);
            StringAssert.Contains("private const int DefaultMockPlayerFootstepMinFrameSignals = 4;", source);
            StringAssert.Contains("private const int DefaultMockPlayerFootstepMaxFrameSignals = 48;", source);
            StringAssert.Contains("private const float DefaultMockPlayerFootstepCoalescingRadiusMeters = 1.5f;", source);
            StringAssert.Contains("private const int DefaultMockPlayerFootstepPriority = 10;", source);

            StringAssert.Contains("UpsertPriority(ToxicityExposureSignal.LaneHash, DefaultToxicityExposurePriority);", priorityBody);
            StringAssert.Contains("UpsertPriority(ToxicBioluminescenceSignal.LaneHash, DefaultToxicBioluminescencePriority);", priorityBody);
            StringAssert.DoesNotContain("ComputeLabelHash(nameof(ToxicityExposureSignal))", priorityBody);
            StringAssert.DoesNotContain("ComputeLabelHash(nameof(ToxicBioluminescenceSignal))", priorityBody);

            StringAssert.Contains("ToxicityExposureSignal.LaneHash", tuningInitBody);
            StringAssert.Contains("ToxicityExposureSignal.LowTierFrameSignals", tuningInitBody);
            StringAssert.Contains("ToxicityExposureSignal.MaxFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultToxicityExposurePriority", tuningInitBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.LaneHash", tuningInitBody);
            StringAssert.Contains("DefaultToxicBioluminescenceMinFrameSignals", tuningInitBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.MaxFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultToxicBioluminescencePriority", tuningInitBody);
            StringAssert.Contains("ComputeLabelHash(nameof(SignalWardenMockDamageSignal))", tuningInitBody);
            StringAssert.Contains("DefaultSignalWardenMockDamageMinFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultSignalWardenMockDamageMaxFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultSignalWardenMockDamageCoalescingRadiusMeters", tuningInitBody);
            StringAssert.Contains("DefaultSignalWardenMockDamagePriority", tuningInitBody);
            StringAssert.Contains("ComputeLabelHash(nameof(MockPlayerFootstepSignal))", tuningInitBody);
            StringAssert.Contains("DefaultMockPlayerFootstepMinFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultMockPlayerFootstepMaxFrameSignals", tuningInitBody);
            StringAssert.Contains("DefaultMockPlayerFootstepCoalescingRadiusMeters", tuningInitBody);
            StringAssert.Contains("DefaultMockPlayerFootstepPriority", tuningInitBody);
            AssertSourceOrder(tuningInitBody, "ComputeLabelHash(nameof(CombatDamageSignal))", "ToxicityExposureSignal.LaneHash");
            AssertSourceOrder(tuningInitBody, "ComputeLabelHash(nameof(CombatDamageSignal))", "ComputeLabelHash(nameof(SignalWardenMockDamageSignal))");
            AssertSourceOrder(tuningInitBody, "ComputeLabelHash(nameof(SignalWardenMockDamageSignal))", "ComputeLabelHash(nameof(MockPlayerFootstepSignal))");
            AssertSourceOrder(tuningInitBody, "ComputeLabelHash(nameof(MockPlayerFootstepSignal))", "ToxicityExposureSignal.LaneHash");
            AssertSourceOrder(tuningInitBody, "ToxicityExposureSignal.LaneHash", "ToxicBioluminescenceSignal.LaneHash");
        }

        [Test]
        public void ToxicSignals_ArePreservedForAotSignalBusLanes()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"));
            string preserveBody = ExtractMethodBody(source, "public static void PreserveGenerics()");

            StringAssert.Contains("PreserveLane<ToxicityExposureSignal>();", preserveBody);
            StringAssert.Contains("PreserveLane<ToxicBioluminescenceSignal>();", preserveBody);
            AssertSourceOrder(preserveBody, "PreserveLane<CombatDamageSignal>();", "PreserveLane<ToxicityExposureSignal>();");
            AssertSourceOrder(preserveBody, "PreserveLane<ToxicityExposureSignal>();", "PreserveLane<ToxicBioluminescenceSignal>();");
            AssertSourceOrder(preserveBody, "PreserveLane<ToxicBioluminescenceSignal>();", "PreserveLane<AcousticPingSignal>();");
        }

        [Test]
        public void ToxicSignals_AreInitializedByGlobalSignalsLifecycleOwner()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs"));
            string categoryInitBody = ExtractMethodBody(source, "private static void InitializeCategorySignalLanes()");

            StringAssert.Contains("using Hecton8.Atmosphere;", source);
            StringAssert.Contains("ValidateSignalSize<ToxicityExposureSignal>(64);", source);
            StringAssert.Contains("ValidateSignalSize<ToxicBioluminescenceSignal>(64);", source);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.Configure(", categoryInitBody);
            StringAssert.Contains("ToxicityExposureSignal.ExpectedCapacity", categoryInitBody);
            StringAssert.Contains("maxFrameSignals: ToxicityExposureSignal.MaxFrameSignals", categoryInitBody);
            StringAssert.Contains("lowTierFrameSignals: ToxicityExposureSignal.LowTierFrameSignals", categoryInitBody);
            StringAssert.Contains("laneHash: ToxicityExposureSignal.LaneHash", categoryInitBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.EnsureInitialized();", categoryInitBody);
            StringAssert.Contains("SignalBus<ToxicBioluminescenceSignal>.Configure(", categoryInitBody);
            StringAssert.Contains("ToxicBioluminescenceSignal.ExpectedCapacity", categoryInitBody);
            StringAssert.Contains("maxFrameSignals: ToxicBioluminescenceSignal.MaxFrameSignals", categoryInitBody);
            StringAssert.Contains("lowTierFrameSignals: ToxicBioluminescenceSignal.LowTierFrameSignals", categoryInitBody);
            StringAssert.Contains("laneHash: ToxicBioluminescenceSignal.LaneHash", categoryInitBody);
            StringAssert.Contains("SignalBus<ToxicBioluminescenceSignal>.EnsureInitialized();", categoryInitBody);
            AssertSourceOrder(categoryInitBody, "SignalBus<RadiationDoseSignal>.EnsureInitialized();", "SignalBus<ToxicityExposureSignal>.Configure(");
            AssertSourceOrder(categoryInitBody, "SignalBus<ToxicityExposureSignal>.EnsureInitialized();", "SignalBus<ToxicBioluminescenceSignal>.Configure(");
            AssertSourceOrder(categoryInitBody, "SignalBus<ToxicBioluminescenceSignal>.EnsureInitialized();", "SignalBus<RadiationSourceSignal>.Configure(");
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

        private static int InvokeToxicityExposureGuard(ref ToxicityExposureSignal signal)
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

        private static int InvokeToxicBioluminescenceGuard(ref ToxicBioluminescenceSignal signal)
        {
            Type guardType = typeof(SignalBusRegistry).Assembly.GetType("Hecton8.Core.Contracts.Signals.SignalPayloadFiniteGuards");
            Assert.NotNull(guardType, "Missing SignalPayloadFiniteGuards type.");

            MethodInfo sanitizeMethod = guardType.GetMethod("Sanitize", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(sanitizeMethod, "Missing SignalPayloadFiniteGuards.Sanitize<T> method.");

            MethodInfo closedMethod = sanitizeMethod.MakeGenericMethod(typeof(ToxicBioluminescenceSignal));
            object[] args = { signal };
            int guardCode = (int)closedMethod.Invoke(null, args);
            signal = (ToxicBioluminescenceSignal)args[0];
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

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int next = source.IndexOf(token, index, StringComparison.Ordinal);
                if (next < 0)
                    break;

                count++;
                index = next + token.Length;
            }

            return count;
        }
    }
}
