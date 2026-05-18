using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.Networking;
using Hecton8.World;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class RollbackNetcodeEditTests
    {
        [Test]
        public void FrameSnapshotDto_Layout_IsTwentyFourBytes()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<FrameSnapshotDTO>());
            Assert.AreEqual(0, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.FrameHash64)));
            Assert.AreEqual(8, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.InputMaskP1)));
            Assert.AreEqual(12, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.InputMaskP2)));
            Assert.AreEqual(16, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.MemoryOffset)));
            Assert.AreEqual(20, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.Reserved0)));
        }

        [Test]
        public void RollbackDtos_StayAlignedAndBlittable()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<StatePageHeaderDTO>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<MockTickCommand>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RemoteInputFrameDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RollbackTuningDTO>());
            Assert.AreEqual(80, UnsafeUtility.SizeOf<RollbackRuntimeStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<VisualStateDTO>());
            Assert.AreEqual(80, UnsafeUtility.SizeOf<NetcodeTelemetryEntry>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<RollbackAudioSuppressionDTO>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<LockstepReplayInputFrame>());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<LockstepReplayBlockHeader>());
            Assert.AreEqual(0, OffsetOf<RollbackRuntimeStateDTO>(nameof(RollbackRuntimeStateDTO.LastFrameHash64)));
            Assert.AreEqual(8, OffsetOf<RollbackRuntimeStateDTO>(nameof(RollbackRuntimeStateDTO.LastRemoteHash64)));
            Assert.AreEqual(96, UnsafeUtility.SizeOf<LockstepPlayerKinematicState>());
            Assert.AreEqual(0, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.SectorX)));
            Assert.AreEqual(8, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.SectorY)));
            Assert.AreEqual(16, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.SectorZ)));
            Assert.AreEqual(24, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.LocalPosition)));
            Assert.AreEqual(36, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Velocity)));
            Assert.AreEqual(48, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Forward)));
            Assert.AreEqual(60, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Frame)));
            Assert.AreEqual(64, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Flags)));
            Assert.AreEqual(68, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputActions)));
            Assert.AreEqual(72, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.StableId)));
            Assert.AreEqual(76, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.HashCadenceFrames)));
            Assert.AreEqual(0, OffsetOf<LockstepReplayInputFrame>(nameof(LockstepReplayInputFrame.Frame)));
            Assert.AreEqual(8, OffsetOf<LockstepReplayInputFrame>(nameof(LockstepReplayInputFrame.MoveDelta)));
            Assert.AreEqual(16, OffsetOf<LockstepReplayInputFrame>(nameof(LockstepReplayInputFrame.LookDelta)));
            Assert.AreEqual(24, OffsetOf<LockstepReplayInputFrame>(nameof(LockstepReplayInputFrame.VerticalDelta)));
            Assert.AreEqual(44, OffsetOf<LockstepReplayInputFrame>(nameof(LockstepReplayInputFrame.Reserved1)));
            Assert.AreEqual(0, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.Magic)));
            Assert.AreEqual(8, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.Version)));
            Assert.AreEqual(28, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.Flags)));
            Assert.AreEqual(32, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.MasterHash)));
            Assert.AreEqual(88, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.Reserved1)));
            Assert.AreEqual(120, OffsetOf<LockstepReplayBlockHeader>(nameof(LockstepReplayBlockHeader.Reserved5)));
            Assert.AreEqual(0, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.AnchorAupAbsolute)));
            Assert.AreEqual(24, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.TrueLocalMeters)));
            Assert.AreEqual(36, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.InterpolatedLocalMeters)));
            Assert.AreEqual(48, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.Blend01)));
            Assert.AreEqual(52, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.BlendStep01)));
            Assert.AreEqual(56, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.EntityId)));
            Assert.AreEqual(60, OffsetOf<VisualStateDTO>(nameof(VisualStateDTO.Flags)));
            Assert.AreEqual(0, OffsetOf<NetcodeTelemetryEntry>(nameof(NetcodeTelemetryEntry.FrameHash64)));
            Assert.AreEqual(8, OffsetOf<NetcodeTelemetryEntry>(nameof(NetcodeTelemetryEntry.RemoteHash64)));
        }

        [Test]
        public void InputMismatch_QualitySkipsLookOnlyRollback()
        {
            InputStateDTO predicted = default;
            InputStateDTO remote = default;
            remote.LookDelta = new float2(0.25f, 0f);

            uint lookOnly = RollbackNetcodeMath.ResolveInputDifferenceFlags(predicted, remote, 0.001f, 0.001f);
            Assert.AreEqual(InputMismatchFlags.Look, lookOnly);
            Assert.IsFalse(RollbackNetcodeMath.ShouldRollback(lookOnly, 0.1f, 0.55f));
            Assert.IsTrue(RollbackNetcodeMath.ShouldRollback(lookOnly, 1f, 0.55f));

            remote.ButtonMask = 1u;
            uint buttonMismatch = RollbackNetcodeMath.ResolveInputDifferenceFlags(predicted, remote, 0.001f, 0.001f);
            Assert.IsTrue(RollbackNetcodeMath.ShouldRollback(buttonMismatch, 0f, 0.55f));
        }

        [Test]
        public void AupHash_ConsumesDouble3Bytes()
        {
            double3 a = new double3(1.0d, -4d, 7d);
            double3 b = new double3(1.0000000000000002d, -4d, 7d);

            Assert.AreNotEqual(
                RollbackNetcodeMath.HashExactAupDouble3(in a),
                RollbackNetcodeMath.HashExactAupDouble3(in b));
        }

        [Test]
        public void VisualCorrection_UsesAupLocalFloatSpace()
        {
            double3 anchor = new double3(100000000d, -80000000d, 42d);
            double3 corrected = anchor + new double3(0.125d, -0.25d, 3.5d);

            float3 local = RollbackNetcodeMath.LocalMetersFromAnchor(corrected, anchor);

            Assert.AreEqual(0.125f, local.x);
            Assert.AreEqual(-0.25f, local.y);
            Assert.AreEqual(3.5f, local.z);
        }

        [Test]
        public void RollbackBudget_ContinuouslyShedsResimulationDepth()
        {
            RollbackTuningDTO tuning = default;
            tuning.MaxRollbackFrames = RollbackNetcodeConstants.MaxRollbackFrames;

            int low = RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, 0.1f);
            int middle = RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, 0.5f);
            int ultra = RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, 1f);

            Assert.Greater(low, 0);
            Assert.Less(low, middle);
            Assert.Less(middle, ultra);
            Assert.AreEqual(RollbackNetcodeConstants.MaxRollbackFrames, ultra);
        }

        [Test]
        public void DeterministicRandom_UsesSectorAndFrame()
        {
            Unity.Mathematics.Random a = RollbackNetcodeMath.CreateDeterministicRandom(0xC0FFEEu, 1337u);
            Unity.Mathematics.Random b = RollbackNetcodeMath.CreateDeterministicRandom(0xC0FFEEu, 1337u);
            Unity.Mathematics.Random c = RollbackNetcodeMath.CreateDeterministicRandom(0xC0FFEEu, 1338u);
            uint sampleA = a.NextUInt();
            uint sampleB = b.NextUInt();
            uint sampleC = c.NextUInt();

            Assert.AreEqual(sampleA, sampleB);
            Assert.AreNotEqual(sampleA, sampleC);
        }

        [Test]
        public void SnapshotAndRestore_CopyExactAupBytes()
        {
            NativeArray<AbsoluteUniversePosition> aups = new NativeArray<AbsoluteUniversePosition>(1, Allocator.TempJob);
            NativeArray<byte> stateRing = new NativeArray<byte>(128, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<FrameSnapshotDTO> snapshots = new NativeArray<FrameSnapshotDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                AbsoluteUniversePosition original = new AbsoluteUniversePosition
                {
                    GridX = 1234567890123L,
                    GridY = -44L,
                    GridZ = 9876543210L,
                    LocalX = 12.25f,
                    LocalY = -3.5f,
                    LocalZ = 0.125f
                };
                aups[0] = original;

                StateSnapshotJob snapshotJob = new StateSnapshotJob
                {
                    RigidbodyAups = aups,
                    StateRingBuffer = stateRing,
                    FrameSnapshots = snapshots,
                    RuntimeState = runtime,
                    Frame = 7u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 128,
                    MaxRigidbodyAups = 1
                };
                snapshotJob.Run();

                Assert.AreNotEqual(0UL, snapshots[0].FrameHash64);
                Assert.AreEqual(0u, snapshots[0].MemoryOffset);

                aups[0] = default;
                RestoreSnapshotJob restoreJob = new RestoreSnapshotJob
                {
                    StateRingBuffer = stateRing,
                    RigidbodyAups = aups,
                    RuntimeState = runtime,
                    RollbackFrame = 7u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 128
                };
                restoreJob.Run();

                Assert.AreEqual(original.GridX, aups[0].GridX);
                Assert.AreEqual(original.GridY, aups[0].GridY);
                Assert.AreEqual(original.GridZ, aups[0].GridZ);
                Assert.AreEqual(original.LocalX, aups[0].LocalX);
                Assert.AreEqual(original.LocalY, aups[0].LocalY);
                Assert.AreEqual(original.LocalZ, aups[0].LocalZ);
            }
            finally
            {
                runtime.Dispose();
                snapshots.Dispose();
                stateRing.Dispose();
                aups.Dispose();
            }
        }

        [Test]
        public void BufferIds_AreRegisteredForRollbackNetcode()
        {
            Assert.AreEqual(SystemID.CoreDeterminism, RollbackNetcodeVault.OwnerSystem);
            Assert.AreEqual(70750, (int)RollbackNetcodeVault.StateRingBuffer);
            Assert.AreEqual(70751, (int)RollbackNetcodeVault.FrameSnapshots);
            Assert.AreEqual(70757, (int)RollbackNetcodeVault.Tuning);
            Assert.AreEqual(70759, (int)RollbackNetcodeVault.CsvScratch);
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }
}
