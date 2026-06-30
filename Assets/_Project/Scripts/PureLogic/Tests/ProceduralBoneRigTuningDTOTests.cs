using NUnit.Framework;
using Hecton8.Animation.FaunaProcedural;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ProceduralBoneRigTuningDTOTests
    {
        [Test]
        public void Default_ReturnsExpectedValues()
        {
            var tuning = ProceduralBoneRigTuningDTO.Default();

            Assert.That(tuning.SineFrequency, Is.EqualTo(1.35f));
            Assert.That(tuning.WaveAmplitudeRadians, Is.EqualTo(0.32f));
            Assert.That(tuning.PhaseOffset, Is.EqualTo(0.72f));
            Assert.That(tuning.DampingHz, Is.EqualTo(6.5f));
            Assert.That(tuning.GlobalQualityWeight, Is.EqualTo(1f));
            Assert.That(tuning.SecondaryBoneStart01, Is.EqualTo(0.28f));
            Assert.That(tuning.JawIkWeight, Is.EqualTo(1f));
            Assert.That(tuning.MockSignalWeight, Is.EqualTo(1f));
            Assert.That(tuning.TraumaFrequencyHz, Is.EqualTo(15f));
            Assert.That(tuning.TraumaAmplitudeRadians, Is.EqualTo(0.18f));
            Assert.That(tuning.LowQualityUpdateHz, Is.EqualTo(5f));
            Assert.That(tuning.HighQualityUpdateHz, Is.EqualTo(60f));
            Assert.That(tuning.Flags, Is.EqualTo(ProceduralBoneBlenderConstants.RigFlagEmergencyMock));
            Assert.That(tuning.ActiveSkeletonCount, Is.EqualTo(1));
            Assert.That(tuning.SectorHash, Is.EqualTo(0x53484E42u));
            Assert.That(tuning._pad0, Is.EqualTo(0u));
        }
    }
}
