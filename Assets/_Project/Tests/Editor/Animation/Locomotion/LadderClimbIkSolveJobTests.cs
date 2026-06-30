
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Animation.Locomotion;
using Hecton8.World;

namespace Hecton8.Tests.Editor.Animation.Locomotion
{
    public class LadderClimbIkSolveJobTests
    {
        private NativeArray<LadderClimbIkInput> _inputs;
        private NativeArray<LadderClimbIkOutput> _outputs;
        private NativeArray<AbsoluteUniversePosition> _ladderAups;
        private NativeArray<LadderClimbTelemetryEntry> _telemetryRing;
        private NativeArray<int> _telemetryCursor;

        [SetUp]
        public void SetUp()
        {
            _inputs = new NativeArray<LadderClimbIkInput>(1, Allocator.Temp);
            _outputs = new NativeArray<LadderClimbIkOutput>(1, Allocator.Temp);
            _ladderAups = new NativeArray<AbsoluteUniversePosition>(1, Allocator.Temp);
            _telemetryRing = new NativeArray<LadderClimbTelemetryEntry>(1, Allocator.Temp);
            _telemetryCursor = new NativeArray<int>(2, Allocator.Temp);
        }

        [TearDown]
        public void TearDown()
        {
            if (_inputs.IsCreated) _inputs.Dispose();
            if (_outputs.IsCreated) _outputs.Dispose();
            if (_ladderAups.IsCreated) _ladderAups.Dispose();
            if (_telemetryRing.IsCreated) _telemetryRing.Dispose();
            if (_telemetryCursor.IsCreated) _telemetryCursor.Dispose();
        }

        [Test]
        public void Execute_EarlyExit_WhenInputsLengthIsZero()
        {
            _inputs.Dispose();
            _inputs = new NativeArray<LadderClimbIkInput>(0, Allocator.Temp);

            var job = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };

            Assert.DoesNotThrow(() => job.Execute());
        }

        [Test]
        public void Execute_EarlyExit_WhenOutputsLengthIsZero()
        {
            _outputs.Dispose();
            _outputs = new NativeArray<LadderClimbIkOutput>(0, Allocator.Temp);

            var job = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };

            Assert.DoesNotThrow(() => job.Execute());
        }

        [Test]
        public void Execute_ValidInput_UpdatesOutput()
        {
            _inputs[0] = new LadderClimbIkInput
            {
                PlayerRoot = new float3(0f, 0f, 0f),
                LadderUp = new float3(0f, 1f, 0f),
                LadderForward = new float3(0f, 0f, 1f),
                LeftShoulder = new float3(-0.5f, 1.5f, 0f),
                RightShoulder = new float3(0.5f, 1.5f, 0f),
                LeftPole = new float3(-0.5f, 1.5f, 1f),
                RightPole = new float3(0.5f, 1.5f, 1f),
                ProgressMeters = 1f,
                LadderHeightMeters = 5f,
                RungSpacingMeters = 0.5f,
                UpperArmMeters = 0.3f,
                LowerArmMeters = 0.3f,
                Stamina01 = 1f,
                LadderIndex = 0,
                Frame = 10,
                Flags = LadderClimbIkConstants.FlagActive
            };

            var job = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };

            job.Execute();

            var output = job.Outputs[0];
            Assert.That(output.Flags, Is.Not.EqualTo(0u));
            Assert.That(output.Progress01, Is.EqualTo(1f / 5f));
            Assert.That(output.Stamina01, Is.EqualTo(1f));
            Assert.That(output.LeftRungIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(output.RightRungIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(output.LeftHandTarget.y, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Execute_ValidatesSanitization()
        {
            _inputs[0] = new LadderClimbIkInput
            {
                PlayerRoot = new float3(0f, 0f, 0f),
                LadderUp = new float3(0f, 1f, 0f),
                LadderForward = new float3(0f, 0f, 1f),
                LeftShoulder = new float3(-0.5f, 1.5f, 0f),
                RightShoulder = new float3(0.5f, 1.5f, 0f),
                LeftPole = new float3(-0.5f, 1.5f, 1f),
                RightPole = new float3(0.5f, 1.5f, 1f),
                ProgressMeters = 0f,
                LadderHeightMeters = -1f,
                RungSpacingMeters = -1f,
                UpperArmMeters = 0.34f,
                LowerArmMeters = 0.36f,
                Stamina01 = 0f,
                LadderIndex = 0,
                Frame = 0,
                Flags = LadderClimbIkConstants.FlagActive
            };

            var job = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };

            job.Execute();

            var output = job.Outputs[0];

            // Progress01 should be safely 0 due to sanitization of height preventing div by zero
            Assert.That(output.Progress01, Is.EqualTo(0f));
            // Output hands should still be valid coordinates, likely defaulted near ladder base
            Assert.That(float.IsNaN(output.LeftHandTarget.x), Is.False);
            Assert.That(float.IsInfinity(output.LeftHandTarget.x), Is.False);
        }

        [Test]
        public void Execute_UnreachableFlag_WhenDistanceExceedsReach()
        {
            _inputs[0] = new LadderClimbIkInput
            {
                PlayerRoot = new float3(0f, 0f, 0f),
                LadderUp = new float3(0f, 1f, 0f),
                LadderForward = new float3(0f, 0f, 1f),
                LeftShoulder = new float3(0f, 1f, 0f),
                RightShoulder = new float3(0f, 1f, 0f),
                LeftPole = new float3(0f, 1f, -1f),
                RightPole = new float3(0f, 1f, -1f),
                ProgressMeters = 5f,
                LadderHeightMeters = 10f,
                RungSpacingMeters = 1f,
                UpperArmMeters = 0.5f,
                LowerArmMeters = 0.5f,
                Stamina01 = 1f,
                LadderIndex = 0,
                Frame = 0,
                Flags = LadderClimbIkConstants.FlagActive
            };

            var job = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };

            job.Execute();

            var output = job.Outputs[0];
            Assert.That((output.Flags & LadderClimbIkConstants.FlagUnreachable) != 0, Is.True);
        }

        [Test]
        public void Execute_CameraSlideFake_ModifiesElbowTarget()
        {
            var baseInput = new LadderClimbIkInput
            {
                PlayerRoot = new float3(0f, 0f, 0f),
                LadderUp = new float3(0f, 1f, 0f),
                LadderForward = new float3(0f, 0f, 1f),
                LeftShoulder = new float3(-0.5f, 1.5f, 0f),
                RightShoulder = new float3(0.5f, 1.5f, 0f),
                LeftPole = new float3(-0.5f, 1.5f, 1f),
                RightPole = new float3(0.5f, 1.5f, 1f),
                ProgressMeters = 1f,
                LadderHeightMeters = 5f,
                RungSpacingMeters = 0.5f,
                UpperArmMeters = 0.3f,
                LowerArmMeters = 0.3f,
                Stamina01 = 1f,
                LadderIndex = 0,
                Frame = 10,
                Flags = LadderClimbIkConstants.FlagActive
            };

            // Run WITHOUT CameraSlideFake
            _inputs[0] = baseInput;
            var job1 = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };
            job1.Execute();
            float3 leftElbowNormal = _outputs[0].LeftElbowTarget;

            // Run WITH CameraSlideFake
            baseInput.Flags |= LadderClimbIkConstants.FlagCameraSlideFake;
            _inputs[0] = baseInput;
            var job2 = new LadderClimbIkSolveJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                LadderAups = _ladderAups,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor
            };
            job2.Execute();
            float3 leftElbowFake = _outputs[0].LeftElbowTarget;

            // They should differ because CameraSlideFake uses a lerp + offset logic
            bool isDifferent =
                math.abs(leftElbowNormal.x - leftElbowFake.x) > 0.001f ||
                math.abs(leftElbowNormal.y - leftElbowFake.y) > 0.001f ||
                math.abs(leftElbowNormal.z - leftElbowFake.z) > 0.001f;

            Assert.That(isDifferent, Is.True);
        }
    }
}
