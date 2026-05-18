using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World.SeedShipAnomaly;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SeedShipAnomalyEditTests
    {
        [Test]
        public void AnomalyFieldDto_Arm64Layout_IsExact()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<AnomalyFieldDTO>());
            Assert.AreEqual(0, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO.EpicenterAUP)));
            Assert.AreEqual(24, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO.Radius)));
            Assert.AreEqual(28, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO.CorruptionLevel)));
            Assert.AreEqual(32, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO.GlitchHash)));
            Assert.AreEqual(36, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO._pad0)));
            Assert.AreEqual(40, OffsetOf<AnomalyFieldDTO>(nameof(AnomalyFieldDTO._pad1)));
        }

        [Test]
        public void GlitchCommandDto_Layout_IsSixteenBytes()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<GlitchCommandDTO>());
            Assert.AreEqual(0, OffsetOf<GlitchCommandDTO>(nameof(GlitchCommandDTO.Intensity)));
            Assert.AreEqual(4, OffsetOf<GlitchCommandDTO>(nameof(GlitchCommandDTO.Frequency)));
            Assert.AreEqual(8, OffsetOf<GlitchCommandDTO>(nameof(GlitchCommandDTO.GlyphHash)));
            Assert.AreEqual(12, OffsetOf<GlitchCommandDTO>(nameof(GlitchCommandDTO._pad0)));
        }

        [Test]
        public void SeedShipSignals_StayBlittableAndAligned()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RadarJamSignal>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<CoreHackedSignal>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<MockAupRebaseSignal>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<MockHudSignal>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<MockLeviathanState>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AnomalyTelemetryEntry>());
        }

        [Test]
        public void CorruptionMath_SubtractsAupBeforeFloatCast()
        {
            double3 epicenter = new double3(1000000000.0, -5000.0, -1000000000.0);
            double3 nearPlayer = epicenter + new double3(32.0, 0.0, -16.0);
            double3 farPlayer = epicenter + new double3(6000.0, 0.0, 0.0);

            float near = SeedShipAnomalyMath.ResolveCorruption01(nearPlayer, epicenter, 3000f);
            float far = SeedShipAnomalyMath.ResolveCorruption01(farPlayer, epicenter, 3000f);

            Assert.Greater(near, 0.95f);
            Assert.AreEqual(0f, far);
        }

        [Test]
        public void EntityBudget_ConsumesContinuousQualityWeight()
        {
            int low = SeedShipAnomalyMath.ResolveEntityBudget(50000, 0.1f, 1f, 0, 50000);
            int mid = SeedShipAnomalyMath.ResolveEntityBudget(50000, 0.5f, 1f, 0, 50000);
            int high = SeedShipAnomalyMath.ResolveEntityBudget(50000, 1f, 1f, 0, 50000);
            int lowWithDesignerFloor = SeedShipAnomalyMath.ResolveEntityBudget(50000, 0.1f, 1f, 1000, 50000);

            Assert.Greater(low, 0);
            Assert.Greater(mid, low);
            Assert.Greater(high, mid);
            Assert.Less(lowWithDesignerFloor, 100);
            Assert.LessOrEqual(high, 50000);
        }

        [Test]
        public void BufferIds_AreRegisteredForEndgameAnomaly()
        {
            Assert.AreEqual(197, (int)SystemID.EndgameAnomaly);
            Assert.AreEqual(70700, (int)BufferID.ShinobuSeedShipAnomalyField);
            Assert.AreEqual(70708, (int)BufferID.ShinobuSeedShipAnomalyTelemetryRing);
            Assert.AreEqual(70710, (int)BufferID.ShinobuSeedShipAnomalyIoScratch);
            Assert.AreEqual(70711, (int)BufferID.ShinobuSeedShipAnomalyDumpScratch);
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }
}
