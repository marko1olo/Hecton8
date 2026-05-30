#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Vehicles.Editor
{
    [TestFixture]
    public static class SubmarineNavigationStressHarness1420
    {
        private const int Attempts = 1000;
        private const SystemID Owner = SystemID.VehiclesPhysics;
        private static readonly ulong SolverMutationGuardMask =
            MutationGuardBit(SubmarineBallastBufferIds.Tanks) |
            MutationGuardBit(SubmarineBallastBufferIds.Commands) |
            MutationGuardBit(SubmarineBallastBufferIds.FluidSamples) |
            MutationGuardBit(SubmarineBallastBufferIds.ForcePackets) |
            MutationGuardBit(SubmarineBallastBufferIds.TelemetryRing);

        private struct HarnessHandles
        {
            public VaultGenerationHandle<BallastTankDTO> Tanks;
            public VaultGenerationHandle<BallastTankCommandDTO> Commands;
            public VaultGenerationHandle<SubmarineBallastFluidSampleDTO> Samples;
            public VaultGenerationHandle<SubmarineBallastForcePacketDTO> ForcePackets;
            public VaultGenerationHandle<SubmarineBallastTelemetryEntry> Telemetry;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        [Test]
        public static void BallastVaultWriteLock_FailsClosedWithoutGc_WhenAlreadyHeld()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                VaultGenerationHandle<BallastTankDTO> handle = vault.EnsureGenerationHandle<BallastTankDTO>(
                    SubmarineBallastBufferIds.Tanks,
                    SubmarineBallastConstants.TankCount,
                    Owner,
                    NativeArrayOptions.ClearMemory);

                Assert.IsTrue(vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<BallastTankDTO> first));
                Assert.IsTrue(first.IsCreated);
                try
                {
                    bool warmupAcquired = vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<BallastTankDTO> warmup);
                    if (warmupAcquired)
                        vault.ReleaseWriteLock(in handle, Owner);

                    Assert.IsFalse(warmupAcquired);
                    Assert.IsFalse(warmup.IsCreated);

                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool allFailedClosed = true;
                    for (int i = 0; i < Attempts; i++)
                    {
                        bool blockedAcquired = vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<BallastTankDTO> blocked);
                        if (blockedAcquired)
                            vault.ReleaseWriteLock(in handle, Owner);

                        allFailedClosed &= !blockedAcquired;
                        allFailedClosed &= !blocked.IsCreated;
                    }

                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                    Assert.IsTrue(allFailedClosed);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    vault.ReleaseWriteLock(in handle, Owner);
                }
            }
        }

        [Test]
        public static void BallastSolverJobs_IntegrateExtremeCommandsWithoutManagedGc()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                HarnessHandles handles = CreateHandles(vault);
                SeedTanks(vault, in handles);
                RunBallastIteration(vault, in handles, 0u);

                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                uint hash = 2166136261u;
                bool allFinite = true;
                bool allValid = true;
                int minActiveSamples = 4;
                int maxActiveSamples = 0;
                for (uint frame = 1u; frame <= Attempts; frame++)
                {
                    SubmarineBallastForcePacketDTO packet = RunBallastIteration(vault, in handles, frame);
                    allFinite &= math.all(math.isfinite(packet.NetForce));
                    allValid &= (packet.Flags & SubmarineBallastConstants.ForceFlagValid) != 0u;
                    minActiveSamples = math.min(minActiveSamples, packet.ActiveSamples);
                    maxActiveSamples = math.max(maxActiveSamples, packet.ActiveSamples);
                    hash = Mix(hash, packet.StateHash);
                }

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                Assert.IsTrue(allFinite);
                Assert.IsTrue(allValid);
                Assert.AreEqual(1, minActiveSamples);
                Assert.AreEqual(4, maxActiveSamples);
                Assert.AreNotEqual(0u, hash);
                Assert.AreEqual(0L, afterBytes - beforeBytes);
            }
        }

        private static HarnessHandles CreateHandles(GlobalDataVault vault)
        {
            return new HarnessHandles
            {
                Tanks = vault.EnsureGenerationHandle<BallastTankDTO>(
                    SubmarineBallastBufferIds.Tanks,
                    SubmarineBallastConstants.TankCount,
                    Owner,
                    NativeArrayOptions.ClearMemory),
                Commands = vault.EnsureGenerationHandle<BallastTankCommandDTO>(
                    SubmarineBallastBufferIds.Commands,
                    SubmarineBallastConstants.TankCount,
                    Owner,
                    NativeArrayOptions.ClearMemory),
                Samples = vault.EnsureGenerationHandle<SubmarineBallastFluidSampleDTO>(
                    SubmarineBallastBufferIds.FluidSamples,
                    1,
                    Owner,
                    NativeArrayOptions.ClearMemory),
                ForcePackets = vault.EnsureGenerationHandle<SubmarineBallastForcePacketDTO>(
                    SubmarineBallastBufferIds.ForcePackets,
                    1,
                    Owner,
                    NativeArrayOptions.ClearMemory),
                Telemetry = vault.EnsureGenerationHandle<SubmarineBallastTelemetryEntry>(
                    SubmarineBallastBufferIds.TelemetryRing,
                    SubmarineBallastConstants.TelemetryCapacity,
                    Owner,
                    NativeArrayOptions.ClearMemory)
            };
        }

        private static void SeedTanks(GlobalDataVault vault, in HarnessHandles handles)
        {
            Assert.IsTrue(vault.TryAcquireWriteLock(in handles.Tanks, Owner, out NativeArray<BallastTankDTO> tanks));
            try
            {
                for (int i = 0; i < SubmarineBallastConstants.TankCount; i++)
                {
                    tanks[i] = new BallastTankDTO
                    {
                        TankVolumeLiters = 1200f,
                        CurrentWaterLiters = 600f,
                        CompressedAirPressureATM = 12f,
                        InputStateFlags = SubmarineBallastConstants.TankFlagInitialized,
                        PumpRateLitersPerSecond = 240f
                    };
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.Tanks, Owner);
            }
        }

        private static SubmarineBallastForcePacketDTO RunBallastIteration(
            GlobalDataVault vault,
            in HarnessHandles handles,
            uint frame)
        {
            bool guardAcquired = false;
            NativeArray<BallastTankDTO> tanks = default;
            NativeArray<BallastTankCommandDTO> commands = default;
            NativeArray<SubmarineBallastFluidSampleDTO> samples = default;
            NativeArray<SubmarineBallastForcePacketDTO> forcePackets = default;
            NativeArray<SubmarineBallastTelemetryEntry> telemetry = default;
            try
            {
                guardAcquired = vault.TryAcquireMutationGuard(SolverMutationGuardMask);
                if (!guardAcquired ||
                    !vault.TryResolveHandle(in handles.Tanks, out tanks) ||
                    !vault.TryResolveHandle(in handles.Commands, out commands) ||
                    !vault.TryResolveHandle(in handles.Samples, out samples) ||
                    !vault.TryResolveHandle(in handles.ForcePackets, out forcePackets) ||
                    !vault.TryResolveHandle(in handles.Telemetry, out telemetry))
                {
                    Assert.Fail("Ballast stress harness failed to acquire Vault mutation guard.");
                    return default;
                }

                WriteExtremeCommands(commands, samples, frame);
                JobHandle evaluateHandle = new EvaluateBallastTanksJob
                {
                    Tanks = tanks,
                    Commands = commands,
                    FluidSamples = samples,
                    DeltaTime = 0.02f,
                    Frame = frame,
                    EmitAcousticSignals = 0
                }.Schedule(SubmarineBallastConstants.TankCount, 1);
                Hecton8.Core.DispatcherJobFence.TryComplete(ref evaluateHandle, forceComplete: true);

                JobHandle forceHandle = new CalculateBuoyancyForceJob
                {
                    Tanks = tanks,
                    FluidSamples = samples,
                    ForcePackets = forcePackets,
                    TelemetryRing = telemetry,
                    TankCount = SubmarineBallastConstants.TankCount,
                    Frame = frame
                }.Schedule(1, 1);
                Hecton8.Core.DispatcherJobFence.TryComplete(ref forceHandle, forceComplete: true);

                return forcePackets[0];
            }
            finally
            {
                if (guardAcquired)
                    vault.ReleaseMutationGuard(SolverMutationGuardMask);
            }
        }

        private static void WriteExtremeCommands(
            NativeArray<BallastTankCommandDTO> commands,
            NativeArray<SubmarineBallastFluidSampleDTO> samples,
            uint frame)
        {
            float targetA = ((frame & 1u) == 0u) ? 1175f : 25f;
            float targetB = ((frame & 2u) == 0u) ? 25f : 1175f;
            for (int i = 0; i < SubmarineBallastConstants.TankCount; i++)
            {
                float target = ((i & 1) == 0) ? targetA : targetB;
                commands[i] = new BallastTankCommandDTO
                {
                    TargetWaterLiters = target,
                    FloodRateLitersPerSecond = 900f,
                    BlowRateLitersPerSecond = 900f,
                    CompressedAirPressureATM = 18f + (float)(frame & 7u),
                    CommandFlags = target > 600f
                        ? SubmarineBallastConstants.CommandFlagFlood
                        : SubmarineBallastConstants.CommandFlagBlow,
                    TargetEntityHash = 1420u,
                    Frame = frame,
                    TankIndex = i
                };
            }

            float quality = math.saturate((frame & 255u) * (1f / 255f));
            int activeSampleBudget = math.clamp(1 + (int)math.ceil(quality * 3f), 1, 4);
            samples[0] = new SubmarineBallastFluidSampleDTO
            {
                HullAup = new double3(0d, -160d - frame * 0.05d, 0d),
                OceanSurfaceAup = new double3(0d, 0d, 0d),
                HullVelocity = new float3(0f, -3.5f, 0f),
                HullHeightMeters = 4f,
                HullVolumeCubicMeters = 22f,
                FluidDensityKgPerM3 = SubmarineBallastConstants.DefaultWaterDensityKgPerM3,
                AmbientPressureATM = SubmarineBallastConstants.AtmosphericPressureAtm + 160f * SubmarineBallastConstants.SeaWaterAtmPerMeter,
                GlobalQualityWeight = quality,
                SimulationDeltaTime = 0.02f,
                TargetEntityHash = 1420u,
                Frame = frame,
                Flags = 0u,
                ActiveSampleBudget = activeSampleBudget
            };
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }
}
#endif
