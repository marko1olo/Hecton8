using System.Runtime.InteropServices;
using Hecton8.AI;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class PredatorCognitionSteeringLayoutTests
    {
        [Test]
        public void SteeringParamsDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<SteeringParamsDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.MaxSpeed)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.TurnSpeed)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.LungeMultiplier)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.ObstacleAvoidanceWeight)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.CurrentTargetDirection)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.CollisionAvoidanceScale)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.ArmorDeflection01)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.AttackControlBits)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.DamageTypeID)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.ArmorMaterialRow)).ToInt32(), Is.EqualTo(45));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.ArmorAngleStep)).ToInt32(), Is.EqualTo(46));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.ArmorClass)).ToInt32(), Is.EqualTo(47));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.LateralDeflectionDirection)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<SteeringParamsDTO>(nameof(SteeringParamsDTO.PresentationFlags)).ToInt32(), Is.EqualTo(60));
        }
    }
}
