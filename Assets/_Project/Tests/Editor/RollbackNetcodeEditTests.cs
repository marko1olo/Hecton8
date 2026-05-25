using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.Networking;
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
        public void FrameSnapshotDto_Layout_IsThirtyTwoBytes()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<FrameSnapshotDTO>());
            Assert.AreEqual(0, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.FrameHash64)));
            Assert.AreEqual(8, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.Tick)));
            Assert.AreEqual(12, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.InputMaskP1)));
            Assert.AreEqual(16, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.InputMaskP2)));
            Assert.AreEqual(20, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.MemoryOffset)));
            Assert.AreEqual(24, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.MerkleRootIndex)));
            Assert.AreEqual(28, OffsetOf<FrameSnapshotDTO>(nameof(FrameSnapshotDTO.Flags)));
        }

        [Test]
        public void RollbackDtos_StayAlignedAndBlittable()
        {
            Assert.AreEqual(0u, RollbackNetcodeLayoutGuard.Validate());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<StatePageHeaderDTO>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<MockTickCommand>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RemoteInputFrameDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<RollbackTuningDTO>());
            Assert.AreEqual(96, UnsafeUtility.SizeOf<RollbackRuntimeStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<VisualStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<VisualStateHistoryDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<NetTelemetryEntry64>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RollbackBlackBoxDumpHeader32>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<RollbackAup48>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<RollbackAudioSuppressionDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<H8NetMerkleNodeRecord32>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<H8NetLeafDeltaRecord64>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<RollbackInputJournalSlot64>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<MockNetworkJitterPacket64>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<MockNetworkJitterState64>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<LockstepReplayInputFrame>());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<LockstepReplayBlockHeader>());
            Assert.AreEqual(0, OffsetOf<RollbackRuntimeStateDTO>(nameof(RollbackRuntimeStateDTO.LastFrameHash64)));
            Assert.AreEqual(8, OffsetOf<RollbackRuntimeStateDTO>(nameof(RollbackRuntimeStateDTO.LastRemoteHash64)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<LockstepPlayerKinematicState>());
            Assert.AreEqual(0, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.PositionAup)));
            Assert.AreEqual(24, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Velocity)));
            Assert.AreEqual(36, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputVector)));
            Assert.AreEqual(48, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Frame)));
            Assert.AreEqual(52, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Flags)));
            Assert.AreEqual(56, OffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputActions)));
            Assert.AreEqual(0, OffsetOf<RollbackAup48>(nameof(RollbackAup48.GridX)));
            Assert.AreEqual(8, OffsetOf<RollbackAup48>(nameof(RollbackAup48.GridY)));
            Assert.AreEqual(16, OffsetOf<RollbackAup48>(nameof(RollbackAup48.GridZ)));
            Assert.AreEqual(24, OffsetOf<RollbackAup48>(nameof(RollbackAup48.LocalX)));
            Assert.AreEqual(28, OffsetOf<RollbackAup48>(nameof(RollbackAup48.LocalY)));
            Assert.AreEqual(32, OffsetOf<RollbackAup48>(nameof(RollbackAup48.LocalZ)));
            Assert.AreEqual(36, OffsetOf<RollbackAup48>(nameof(RollbackAup48._pad0)));
            Assert.AreEqual(40, OffsetOf<RollbackAup48>(nameof(RollbackAup48._pad1)));
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
            Assert.AreEqual(0, OffsetOf<NetTelemetryEntry64>(nameof(NetTelemetryEntry64.FrameHash64)));
            Assert.AreEqual(8, OffsetOf<NetTelemetryEntry64>(nameof(NetTelemetryEntry64.RemoteHash64)));
            Assert.AreEqual(0, OffsetOf<RollbackBlackBoxDumpHeader32>(nameof(RollbackBlackBoxDumpHeader32.Magic)));
            Assert.AreEqual(8, OffsetOf<RollbackBlackBoxDumpHeader32>(nameof(RollbackBlackBoxDumpHeader32.SourceHash)));
            Assert.AreEqual(24, OffsetOf<RollbackBlackBoxDumpHeader32>(nameof(RollbackBlackBoxDumpHeader32.EntrySizeBytes)));
            Assert.AreEqual(0, OffsetOf<H8NetMerkleNodeRecord32>(nameof(H8NetMerkleNodeRecord32.HashLo)));
            Assert.AreEqual(8, OffsetOf<H8NetMerkleNodeRecord32>(nameof(H8NetMerkleNodeRecord32.HashHi)));
            Assert.AreEqual(16, OffsetOf<H8NetMerkleNodeRecord32>(nameof(H8NetMerkleNodeRecord32.BufferId)));
        }

        [Test]
        public void InputMismatch_QualitySkipsLookOnlyRollback()
        {
            InputStateDTO predicted = default;
            InputStateDTO remote = default;
            remote.LookDelta = new float2(0.25f, 0f);

            uint lookOnly = RollbackNetcodeMath.ResolveInputDifferenceFlags(predicted, remote, 0.001f, 0.001f);
            Assert.AreEqual(InputMismatchFlags.Look, lookOnly);
            Assert.IsTrue(RollbackNetcodeMath.ShouldRollback(lookOnly));

            remote.ButtonMask = 1u;
            uint buttonMismatch = RollbackNetcodeMath.ResolveInputDifferenceFlags(predicted, remote, 0.001f, 0.001f);
            Assert.IsTrue(RollbackNetcodeMath.ShouldRollback(buttonMismatch));
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

            Assert.AreEqual(30, low);
            Assert.Less(low, middle);
            Assert.Less(middle, ultra);
            Assert.AreEqual(RollbackNetcodeConstants.MaxRollbackFrames, ultra);

            tuning.MaxRollbackFrames = 60;
            Assert.AreEqual(15, RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, 0.1f));
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
        public void RollbackFrameMath_HandlesUintWrap()
        {
            Assert.AreEqual(4, RollbackNetcodeMath.ResolveRollbackFrameCount(uint.MaxValue - 2u, 1u));
            Assert.AreEqual(0, RollbackNetcodeMath.ResolveRollbackFrameCount(10u, 3u));
            Assert.IsTrue(RollbackNetcodeMath.HasFrameReached(1u, uint.MaxValue - 2u));
            Assert.IsFalse(RollbackNetcodeMath.HasFrameReached(uint.MaxValue - 2u, 1u));
            Assert.IsTrue(RollbackNetcodeMath.DidFrameWrap(uint.MaxValue - 1u, 2u));
            Assert.IsFalse(RollbackNetcodeMath.DidFrameWrap(9u, 2u));

            Assert.IsTrue(RollbackNetcodeMath.TryResolveHistoricalFrame(2u, uint.MaxValue - 1u, 4u, out uint wrappedFrame));
            Assert.AreEqual(uint.MaxValue - 1u, wrappedFrame);
            Assert.IsFalse(RollbackNetcodeMath.TryResolveHistoricalFrame(2u, 1u, 4u, out _));
        }

        [Test]
        public void DetectInputMismatch_UsesScheduledPreviousFrameAcrossWrap()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackInputJournalSlot64> journal = new NativeArray<RollbackInputJournalSlot64>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                uint matchingFrame = uint.MaxValue - 1u;
                uint mismatchFrame = uint.MaxValue;
                predicted[(int)(matchingFrame % (uint)predicted.Length)] = CreatePredictedInput(matchingFrame, 1u);
                predicted[(int)(mismatchFrame % (uint)predicted.Length)] = CreatePredictedInput(mismatchFrame, 1u);
                remote[(int)(matchingFrame % (uint)remote.Length)] = new RemoteInputFrameDTO
                {
                    Input = CreatePredictedInput(matchingFrame, 1u),
                    Frame = matchingFrame,
                    Flags = RemoteInputFlags.Received | RemoteInputFlags.Valid
                };
                remote[(int)(mismatchFrame % (uint)remote.Length)] = new RemoteInputFrameDTO
                {
                    Input = CreatePredictedInput(mismatchFrame, 2u),
                    Frame = mismatchFrame,
                    Flags = RemoteInputFlags.Received | RemoteInputFlags.Valid
                };

                new EvaluateInputMismatchJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    InputJournalRing = journal,
                    RuntimeState = runtime,
                    CurrentFrame = 1u,
                    PreviousFrame = uint.MaxValue,
                    MaxRollbackFrames = 4,
                    GlobalQualityWeight = 1f,
                    MoveEpsilon = 0.001f,
                    LookEpsilon = 0.001f,
                    LookMismatchSeverityWeight = 1f
                }.Run();

                Assert.AreEqual(mismatchFrame, runtime[0].LastMismatchFrame);
                Assert.AreNotEqual(0u, runtime[0].Flags & RollbackNetcodeFlags.RollbackRequired);
            }
            finally
            {
                runtime.Dispose();
                journal.Dispose();
                remote.Dispose();
                predicted.Dispose();
            }
        }

        [Test]
        public void SnapshotAndRestore_CopyExactAupBytes()
        {
            NativeArray<RollbackAup48> aups = new NativeArray<RollbackAup48>(1, Allocator.TempJob);
            NativeArray<byte> stateRing = new NativeArray<byte>(256, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<FrameSnapshotDTO> snapshots = new NativeArray<FrameSnapshotDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                RollbackAup48 original = new RollbackAup48
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
                    EntityAups = aups,
                    StateRingBuffer = stateRing,
                    FrameSnapshots = snapshots,
                    RuntimeState = runtime,
                    Frame = 7u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 256,
                    MaxEntityAups = 1
                };
                snapshotJob.Run();

                Assert.AreNotEqual(0UL, snapshots[0].FrameHash64);
                Assert.AreEqual(0u, snapshots[0].MemoryOffset);

                aups[0] = default;
                RestoreSnapshotJob restoreJob = new RestoreSnapshotJob
                {
                    StateRingBuffer = stateRing,
                    EntityAups = aups,
                    RuntimeState = runtime,
                    RollbackFrame = 7u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 256
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
        public unsafe void RestoreSnapshot_RejectsOutOfBoundsPayloadHeader()
        {
            NativeArray<byte> stateRing = new NativeArray<byte>(256, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<double3> rigidbodyAups = new NativeArray<double3>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                StatePageHeaderDTO* header = (StatePageHeaderDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateRing);
                header->Frame = 42u;
                header->PayloadBytes = 4096u;
                header->RigidbodyAupCount = 1u;

                new RestoreSnapshotJob
                {
                    StateRingBuffer = stateRing,
                    RigidbodyAups = rigidbodyAups,
                    RuntimeState = runtime,
                    RollbackFrame = 42u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 256
                }.Run();

                Assert.AreNotEqual(0u, runtime[0].Flags & RollbackNetcodeFlags.SnapshotMissing);
                Assert.AreEqual(default(double3), rigidbodyAups[0]);
            }
            finally
            {
                runtime.Dispose();
                rigidbodyAups.Dispose();
                stateRing.Dispose();
            }
        }

        [Test]
        public void SnapshotAfterRollback_ForcesRawPageHashWhenMerkleIsStale()
        {
            NativeArray<double3> rigidbodyAups = new NativeArray<double3>(1, Allocator.TempJob);
            NativeArray<byte> stateRing = new NativeArray<byte>(256, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<FrameSnapshotDTO> snapshots = new NativeArray<FrameSnapshotDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> merkleNodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                rigidbodyAups[0] = new double3(5.5d, 7.25d, -11.125d);

                merkleNodes[RollbackNetcodeConstants.MerkleRootNodeIndex] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0x1111222233334444UL,
                    HashHi = 0x5555666677778888UL,
                    Flags = RollbackMerkleFlags.RootNode
                };

                StateSnapshotJob staleMerkleSnapshot = new StateSnapshotJob
                {
                    RigidbodyAups = rigidbodyAups,
                    MerkleNodes = merkleNodes,
                    StateRingBuffer = stateRing,
                    FrameSnapshots = snapshots,
                    RuntimeState = runtime,
                    Frame = 9u,
                    RingFrameCapacity = 1,
                    SnapshotStrideBytes = 256,
                    MaxRigidbodyAups = 1,
                    MerkleRootIndex = RollbackNetcodeConstants.MerkleRootNodeIndex,
                    ForceRawPageHash = 1u
                };
                staleMerkleSnapshot.Run();

                Assert.AreNotEqual(0x1111222233334444UL, snapshots[0].FrameHash64);
                Assert.AreNotEqual(0UL, snapshots[0].FrameHash64);
            }
            finally
            {
                merkleNodes.Dispose();
                runtime.Dispose();
                snapshots.Dispose();
                stateRing.Dispose();
                rigidbodyAups.Dispose();
            }
        }

        [Test]
        public void RemoteMerkleBranchIsolation_UsesBranchNodesAfterPlainRootHash()
        {
            NativeArray<RollbackTuningDTO> tuning = new NativeArray<RollbackTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remoteInput = new NativeArray<RemoteInputFrameDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackInputJournalSlot64> inputJournal = new NativeArray<RollbackInputJournalSlot64>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> stateRing = new NativeArray<byte>(4096, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<FrameSnapshotDTO> snapshots = new NativeArray<FrameSnapshotDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> localNodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> remoteNodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetLeafDeltaRecord64> leafDeltas = new NativeArray<H8NetLeafDeltaRecord64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                tuning[0] = new RollbackTuningDTO
                {
                    MaxRollbackFrames = 1,
                    VisualInterpolationFrames = 3,
                    VisualInterpolationSeconds = RollbackNetcodeConstants.DefaultVisualInterpolationSeconds,
                    MinQualityForLookRollback = 0f,
                    HashCadenceFrames = 15u,
                    MaxMerkleLeaves = RollbackNetcodeConstants.MerkleLeafCapacity,
                    RedundancyCount = 1u
                };
                runtime[0] = new RollbackRuntimeStateDTO
                {
                    LastRemoteFrame = 15u,
                    LastRemoteHash64 = 0x9999888877776666UL
                };

                PredictedInputDTO input = CreatePredictedInput(15u, 3u);
                predicted[15] = input;
                remoteInput[15] = new RemoteInputFrameDTO
                {
                    Input = input,
                    Frame = 15u,
                    Flags = RemoteInputFlags.Received | RemoteInputFlags.Valid
                };

                localNodes[RollbackNetcodeConstants.MerkleRootNodeIndex] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0x1111222233334444UL,
                    HashHi = 0x5555666677778888UL,
                    Flags = RollbackMerkleFlags.RootNode
                };
                localNodes[RollbackNetcodeConstants.MerkleBranchNodeStart] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0x1010UL,
                    HashHi = 0x2020UL,
                    Flags = RollbackMerkleFlags.BranchNode
                };
                localNodes[0] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0xAAAAUL,
                    HashHi = 0xBBBBUL,
                    BufferId = (uint)BufferID.EntityFlags,
                    ByteOffset = 64u,
                    ByteLength = 4u,
                    Flags = RollbackMerkleFlags.Authoritative
                };
                remoteNodes[RollbackNetcodeConstants.MerkleBranchNodeStart] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0x3030UL,
                    HashHi = 0x4040UL,
                    Flags = RollbackMerkleFlags.BranchNode
                };
                remoteNodes[0] = new H8NetMerkleNodeRecord32
                {
                    HashLo = 0xCCCCUL,
                    HashHi = 0xDDDDUL,
                    BufferId = (uint)BufferID.EntityFlags,
                    ByteOffset = 64u,
                    ByteLength = 4u,
                    Flags = RollbackMerkleFlags.Authoritative
                };

                new RollbackFixedPipelineJob
                {
                    Tuning = tuning,
                    RuntimeState = runtime,
                    PredictedJournal = predicted,
                    RemoteInputRing = remoteInput,
                    InputJournalRing = inputJournal,
                    StateRingBuffer = stateRing,
                    FrameSnapshots = snapshots,
                    MerkleNodes = localNodes,
                    RemoteMerkleNodes = remoteNodes,
                    LeafDeltaRecords = leafDeltas,
                    CurrentFrame = 15u,
                    RingFrameCapacity = 16,
                    SnapshotStrideBytes = 256,
                    MaxRollbackFrames = 1,
                    GlobalQualityWeight = 1f,
                    MoveEpsilon = 0.001f,
                    LookEpsilon = 0.001f
                }.Execute();

                Assert.AreNotEqual(0u, runtime[0].Flags & RollbackNetcodeFlags.BranchProbeRequested);
                Assert.AreEqual((uint)BufferID.EntityFlags, runtime[0].FirstMismatchBufferId);
                Assert.AreEqual(64u, runtime[0].FirstMismatchByteOffset);
                Assert.AreEqual(0xCCCCUL, leafDeltas[0].RemoteHashLo);
                Assert.AreEqual(0xDDDDUL, leafDeltas[0].RemoteHashHi);
            }
            finally
            {
                leafDeltas.Dispose();
                remoteNodes.Dispose();
                localNodes.Dispose();
                snapshots.Dispose();
                stateRing.Dispose();
                inputJournal.Dispose();
                remoteInput.Dispose();
                predicted.Dispose();
                runtime.Dispose();
                tuning.Dispose();
            }
        }

        [Test]
        public void MerkleRoot_ConsumesExactAupDouble3Bits()
        {
            NativeArray<RollbackAup48> aups = new NativeArray<RollbackAup48>(1, Allocator.TempJob);
            NativeArray<RollbackVaultBufferDescriptor32> descriptors = new NativeArray<RollbackVaultBufferDescriptor32>(RollbackNetcodeConstants.MerkleLeafCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> nodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                descriptors[0] = new RollbackVaultBufferDescriptor32
                {
                    BufferId = (uint)BufferID.EntityAUPs,
                    ElementStride = (uint)UnsafeUtility.SizeOf<double3>(),
                    ElementCount = 1u,
                    ByteLength = (uint)UnsafeUtility.SizeOf<double3>(),
                    Flags = RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.AupExactDouble3
                };

                aups[0] = new RollbackAup48
                {
                    GridX = 1,
                    GridY = 2,
                    GridZ = 3,
                    LocalX = 0.125f,
                    LocalY = -0.25f,
                    LocalZ = 0.5f
                };

                ComputeMerkleRootJob merkle = new ComputeMerkleRootJob
                {
                    LeafDescriptors = descriptors,
                    MerkleNodes = nodes,
                    EntityAups = aups,
                    QualityLeafBudget = 1,
                    Frame = 17u
                };
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 17u,
                    QualityLeafBudget = 1
                }.Run();
                ulong firstRoot = nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo;
                Assert.AreNotEqual(0UL, firstRoot);

                RollbackAup48 changed = aups[0];
                changed.LocalX = 0.12500012f;
                aups[0] = changed;
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 17u,
                    QualityLeafBudget = 1
                }.Run();

                Assert.AreNotEqual(firstRoot, nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo);
            }
            finally
            {
                runtime.Dispose();
                nodes.Dispose();
                descriptors.Dispose();
                aups.Dispose();
            }
        }

        [Test]
        public void MerkleRoot_EntityAupDescriptorByteOffsetSelectsSlice()
        {
            NativeArray<RollbackAup48> aups = new NativeArray<RollbackAup48>(2, Allocator.TempJob);
            NativeArray<RollbackVaultBufferDescriptor32> descriptors = new NativeArray<RollbackVaultBufferDescriptor32>(RollbackNetcodeConstants.MerkleLeafCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> nodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                descriptors[0] = new RollbackVaultBufferDescriptor32
                {
                    BufferId = (uint)BufferID.EntityAUPs,
                    ByteOffset = (uint)UnsafeUtility.SizeOf<double3>(),
                    ElementStride = (uint)UnsafeUtility.SizeOf<double3>(),
                    ElementCount = 1u,
                    ByteLength = (uint)UnsafeUtility.SizeOf<double3>(),
                    Flags = RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.AupExactDouble3
                };

                aups[0] = new RollbackAup48
                {
                    GridX = 1,
                    GridY = 2,
                    GridZ = 3,
                    LocalX = 0.125f,
                    LocalY = -0.25f,
                    LocalZ = 0.5f
                };
                aups[1] = new RollbackAup48
                {
                    GridX = 4,
                    GridY = 5,
                    GridZ = 6,
                    LocalX = -0.75f,
                    LocalY = 0.875f,
                    LocalZ = -0.375f
                };

                ComputeMerkleRootJob merkle = new ComputeMerkleRootJob
                {
                    LeafDescriptors = descriptors,
                    MerkleNodes = nodes,
                    EntityAups = aups,
                    QualityLeafBudget = 1,
                    Frame = 23u
                };
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 23u,
                    QualityLeafBudget = 1
                }.Run();
                ulong selectedSliceRoot = nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo;
                Assert.AreEqual((uint)UnsafeUtility.SizeOf<double3>(), nodes[0].ByteLength);

                RollbackAup48 ignoredPrefix = aups[0];
                ignoredPrefix.LocalX = 42.0f;
                aups[0] = ignoredPrefix;
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 23u,
                    QualityLeafBudget = 1
                }.Run();
                Assert.AreEqual(selectedSliceRoot, nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo);

                RollbackAup48 selected = aups[1];
                selected.LocalX = -0.625f;
                aups[1] = selected;
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 23u,
                    QualityLeafBudget = 1
                }.Run();
                Assert.AreNotEqual(selectedSliceRoot, nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo);
            }
            finally
            {
                runtime.Dispose();
                nodes.Dispose();
                descriptors.Dispose();
                aups.Dispose();
            }
        }

        [Test]
        public void MerkleRoot_ConsumesRawRigidbodyDouble3Bytes()
        {
            NativeArray<double3> rigidbodyAups = new NativeArray<double3>(1, Allocator.TempJob);
            NativeArray<RollbackVaultBufferDescriptor32> descriptors = new NativeArray<RollbackVaultBufferDescriptor32>(RollbackNetcodeConstants.MerkleLeafCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<H8NetMerkleNodeRecord32> nodes = new NativeArray<H8NetMerkleNodeRecord32>(RollbackNetcodeConstants.MerkleNodeCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RollbackRuntimeStateDTO> runtime = new NativeArray<RollbackRuntimeStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                descriptors[0] = new RollbackVaultBufferDescriptor32
                {
                    BufferId = (uint)BufferID.RigidbodyAUPs,
                    ElementStride = (uint)UnsafeUtility.SizeOf<double3>(),
                    ElementCount = 1u,
                    ByteLength = (uint)UnsafeUtility.SizeOf<double3>(),
                    Flags = RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.AupExactDouble3
                };

                rigidbodyAups[0] = new double3(1.0d, -2.0d, 3.0d);
                ComputeMerkleRootJob merkle = new ComputeMerkleRootJob
                {
                    LeafDescriptors = descriptors,
                    MerkleNodes = nodes,
                    RigidbodyAups = rigidbodyAups,
                    QualityLeafBudget = 1,
                    Frame = 19u
                };
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 19u,
                    QualityLeafBudget = 1
                }.Run();
                ulong firstRoot = nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo;

                rigidbodyAups[0] = new double3(1.0000000000000002d, -2.0d, 3.0d);
                merkle.Run(RollbackNetcodeConstants.MerkleLeafCapacity);
                new FinalizeMerkleRootJob
                {
                    MerkleNodes = nodes,
                    RuntimeState = runtime,
                    Frame = 19u,
                    QualityLeafBudget = 1
                }.Run();

                Assert.AreNotEqual(firstRoot, nodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo);
            }
            finally
            {
                runtime.Dispose();
                nodes.Dispose();
                descriptors.Dispose();
                rigidbodyAups.Dispose();
            }
        }

        [Test]
        public void MockNetworkJitter_DelaysAndReleasesInput()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(32, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(32, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterPacket64> packets = new NativeArray<MockNetworkJitterPacket64>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterState64> state = new NativeArray<MockNetworkJitterState64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                predicted[10] = CreatePredictedInput(10u, 7u);
                GenerateMockNetworkJitterJob jitter = new GenerateMockNetworkJitterJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    Packets = packets,
                    JitterState = state,
                    CurrentFrame = 10u,
                    DelayFrames = 2u,
                    PacketLossPermille = 0u,
                    DuplicatePermille = 0u,
                    Seed = 0x1234u
                };
                jitter.Run();
                Assert.AreEqual(0u, remote[10].Flags);

                jitter.CurrentFrame = 12u;
                jitter.Run();
                Assert.AreEqual(10u, remote[10].Frame);
                Assert.AreEqual(7u, remote[10].Input.ActionButtonsMask);
                Assert.AreNotEqual(0u, remote[10].Flags & RemoteInputFlags.Received);
                Assert.AreNotEqual(0u, remote[10].Flags & RemoteInputFlags.Valid);
            }
            finally
            {
                state.Dispose();
                packets.Dispose();
                remote.Dispose();
                predicted.Dispose();
            }
        }

        [Test]
        public void MockNetworkJitter_DoesNotOverwriteRealRemoteInput()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterPacket64> packets = new NativeArray<MockNetworkJitterPacket64>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterState64> state = new NativeArray<MockNetworkJitterState64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                predicted[3] = CreatePredictedInput(3u, 1u);
                remote[3] = new RemoteInputFrameDTO
                {
                    Input = CreatePredictedInput(3u, 9u),
                    Frame = 3u,
                    Flags = RemoteInputFlags.Received | RemoteInputFlags.Valid
                };

                new GenerateMockNetworkJitterJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    Packets = packets,
                    JitterState = state,
                    CurrentFrame = 3u,
                    DelayFrames = 0u,
                    PacketLossPermille = 0u,
                    DuplicatePermille = 0u,
                    Seed = 0x44u
                }.Run();

                Assert.AreEqual(3u, remote[3].Frame);
                Assert.AreEqual(9u, remote[3].Input.ActionButtonsMask);
                Assert.AreEqual(0u, remote[3].Flags & RemoteInputFlags.MockGenerated);
            }
            finally
            {
                state.Dispose();
                packets.Dispose();
                remote.Dispose();
                predicted.Dispose();
            }
        }

        [Test]
        public void MockNetworkJitter_ReleasesAcrossUintWrap()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterPacket64> packets = new NativeArray<MockNetworkJitterPacket64>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<MockNetworkJitterState64> state = new NativeArray<MockNetworkJitterState64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                uint sourceFrame = uint.MaxValue;
                predicted[(int)(sourceFrame % (uint)predicted.Length)] = CreatePredictedInput(sourceFrame, 13u);

                GenerateMockNetworkJitterJob jitter = new GenerateMockNetworkJitterJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    Packets = packets,
                    JitterState = state,
                    CurrentFrame = sourceFrame,
                    DelayFrames = 2u,
                    PacketLossPermille = 0u,
                    DuplicatePermille = 0u,
                    Seed = 0x99u
                };
                jitter.Run();
                int remoteIndex = (int)(sourceFrame % (uint)remote.Length);
                Assert.AreEqual(0u, remote[remoteIndex].Flags);

                jitter.CurrentFrame = 1u;
                jitter.Run();
                Assert.AreEqual(sourceFrame, remote[remoteIndex].Frame);
                Assert.AreEqual(13u, remote[remoteIndex].Input.ActionButtonsMask);
                Assert.AreNotEqual(0u, remote[remoteIndex].Flags & RemoteInputFlags.Received);
                Assert.AreNotEqual(0u, remote[remoteIndex].Flags & RemoteInputFlags.Valid);
            }
            finally
            {
                state.Dispose();
                packets.Dispose();
                remote.Dispose();
                predicted.Dispose();
            }
        }

        [Test]
        public void ApplyRemoteInputCorrection_HandlesUintWrap()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                uint start = uint.MaxValue - 1u;
                for (int offset = 0; offset <= 3; offset++)
                {
                    uint frame = start + (uint)offset;
                    remote[(int)(frame % (uint)remote.Length)] = new RemoteInputFrameDTO
                    {
                        Input = CreatePredictedInput(frame, (uint)(20 + offset)),
                        Frame = frame,
                        Flags = RemoteInputFlags.Received | RemoteInputFlags.Valid
                    };
                }

                new ApplyRemoteInputCorrectionJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    RollbackFrame = start,
                    CurrentFrame = 1u
                }.Run();

                for (int offset = 0; offset <= 3; offset++)
                {
                    uint frame = start + (uint)offset;
                    Assert.AreEqual((uint)(20 + offset), predicted[(int)(frame % (uint)predicted.Length)].ActionButtonsMask);
                }
            }
            finally
            {
                remote.Dispose();
                predicted.Dispose();
            }
        }

        [Test]
        public void ApplyRemoteInputCorrection_IgnoresUnsealedRemoteSlot()
        {
            NativeArray<PredictedInputDTO> predicted = new NativeArray<PredictedInputDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<RemoteInputFrameDTO> remote = new NativeArray<RemoteInputFrameDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                predicted[4] = CreatePredictedInput(4u, 1u);
                remote[4] = new RemoteInputFrameDTO
                {
                    Input = CreatePredictedInput(4u, 99u),
                    Frame = 4u,
                    Flags = RemoteInputFlags.Received
                };

                new ApplyRemoteInputCorrectionJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    RollbackFrame = 4u,
                    CurrentFrame = 4u
                }.Run();

                Assert.AreEqual(1u, predicted[4].ActionButtonsMask);

                RemoteInputFrameDTO sealedRemote = remote[4];
                sealedRemote.Flags |= RemoteInputFlags.Valid;
                remote[4] = sealedRemote;

                new ApplyRemoteInputCorrectionJob
                {
                    PredictedJournal = predicted,
                    RemoteInputRing = remote,
                    RollbackFrame = 4u,
                    CurrentFrame = 4u
                }.Run();

                Assert.AreEqual(99u, predicted[4].ActionButtonsMask);
            }
            finally
            {
                remote.Dispose();
                predicted.Dispose();
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
            Assert.AreEqual(70770, (int)RollbackNetcodeVault.MerkleNodes);
            Assert.AreEqual(70773, (int)RollbackNetcodeVault.InputJournalRing);
            Assert.AreEqual(70776, (int)RollbackNetcodeVault.VisualHistory);
            Assert.AreEqual(70777, (int)RollbackNetcodeVault.RemoteMerkleNodes);
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        private static PredictedInputDTO CreatePredictedInput(uint tickNumber, uint buttonMask)
        {
            return new PredictedInputDTO
            {
                TickNumber = tickNumber,
                LocalMoveVector = float3.zero,
                LookDelta = float2.zero,
                ActionButtonsMask = buttonMask,
                _pad0 = PredictedInputFlags.Predicted | PredictedInputFlags.Valid
            };
        }
    }
}
