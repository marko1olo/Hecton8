#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Animation.FaunaProcedural;

namespace Hecton8.Animation.FaunaProcedural.Tests.Editor
{
    public class ProceduralBoneRigTuningDTOTests
    {
        [Test]
        public void Default_ReturnsStructWithExpectedDefaultValues()
        {
            // Act
            var dto = ProceduralBoneRigTuningDTO.Default();

            // Assert
            Assert.AreEqual(1.35f, dto.SineFrequency, "SineFrequency mismatch");
            Assert.AreEqual(0.32f, dto.WaveAmplitudeRadians, "WaveAmplitudeRadians mismatch");
            Assert.AreEqual(0.72f, dto.PhaseOffset, "PhaseOffset mismatch");
            Assert.AreEqual(6.5f, dto.DampingHz, "DampingHz mismatch");
            Assert.AreEqual(1f, dto.GlobalQualityWeight, "GlobalQualityWeight mismatch");
            Assert.AreEqual(0.28f, dto.SecondaryBoneStart01, "SecondaryBoneStart01 mismatch");
            Assert.AreEqual(1f, dto.JawIkWeight, "JawIkWeight mismatch");
            Assert.AreEqual(1f, dto.MockSignalWeight, "MockSignalWeight mismatch");
            Assert.AreEqual(15f, dto.TraumaFrequencyHz, "TraumaFrequencyHz mismatch");
            Assert.AreEqual(0.18f, dto.TraumaAmplitudeRadians, "TraumaAmplitudeRadians mismatch");
            Assert.AreEqual(5f, dto.LowQualityUpdateHz, "LowQualityUpdateHz mismatch");
            Assert.AreEqual(60f, dto.HighQualityUpdateHz, "HighQualityUpdateHz mismatch");
            Assert.AreEqual(ProceduralBoneBlenderConstants.RigFlagEmergencyMock, dto.Flags, "Flags mismatch");
            Assert.AreEqual(1, dto.ActiveSkeletonCount, "ActiveSkeletonCount mismatch");
            Assert.AreEqual(0x53484E42u, dto.SectorHash, "SectorHash mismatch");
            Assert.AreEqual(0u, dto._pad0, "_pad0 mismatch");
        }
    }
}
#endif
