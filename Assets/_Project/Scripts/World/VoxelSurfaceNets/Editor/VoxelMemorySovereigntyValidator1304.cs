#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.World.VoxelSurfaceNets.Editor
{
    [InitializeOnLoad]
    public static class VoxelMemorySovereigntyValidator1304
    {
        private const int StressCarveCount = 512;
        private const uint StressSeed = 0x13041304u;
        private const uint FailureLayout = 1u << 0;
        private const uint FailureHandle = 1u << 1;
        private const uint FailureLock = 1u << 2;
        private const uint FailureDefrag = 1u << 3;
        private const uint FailureThread = 1u << 4;
        private const int DefragWorkerJoinMilliseconds = 1000;

        static VoxelMemorySovereigntyValidator1304()
        {
            ValidateLayoutsOrThrow();
        }

        [MenuItem("HECTON-8/Voxel/Run Memory Sovereignty Validator 1304")]
        public static void RunMenu()
        {
            ValidateLayoutsOrThrow();
            if (!RunDefragRaceFuzzer(out uint failureFlags))
                throw new FatalArchitectureException("1304 voxel memory sovereignty validator failed.");

            H8Debug.Log("[1304] Voxel memory sovereignty validator passed.");
        }

        public static bool RunDefragRaceFuzzer(out uint failureFlags)
        {
            failureFlags = 0u;
            int stopThread = 0;
            int workerIterations = 0;
            bool workerFaulted = false;

            using GlobalDataVault vault = GlobalDataVault.Create(256, 32L * 1024L * 1024L);
            VaultGenerationHandle<VoxelCarveEvent> carveHandle = vault.EnsureGenerationHandle<VoxelCarveEvent>(
                BufferID.ShinobuDeltaCrusherCarveEventQueue,
                StressCarveCount,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<sbyte> densityHandle = vault.EnsureGenerationHandle<sbyte>(
                VoxelSurfaceNetsVaultBufferIds.Density,
                VoxelSurfaceNetsConstants.DensitySampleCount,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);

            if (carveHandle.BufferID == 0u || densityHandle.BufferID == 0u)
            {
                failureFlags |= FailureHandle;
                return false;
            }

            Thread worker = new Thread(
                () =>
                {
                    try
                    {
                        while (Volatile.Read(ref stopThread) == 0)
                        {
                            vault.TryGetBufferGeneration(BufferID.ShinobuDeltaCrusherCarveEventQueue, out uint _);
                            vault.TryGetBufferGeneration(VoxelSurfaceNetsVaultBufferIds.Density, out uint _);
                            _ = vault.IsCompactionFenceActive;
                            Interlocked.Increment(ref workerIterations);
                            Thread.Yield();
                        }
                    }
                    catch
                    {
                        Volatile.Write(ref workerFaulted, true);
                    }
                });
            worker.IsBackground = true;
            worker.Name = "H8_1304_VoxelVaultFuzzer";
            if (!TryStartDefragWorkerNoThrow(worker))
            {
                failureFlags |= FailureThread;
                return false;
            }

            try
            {
                for (int pass = 0; pass < 16; pass++)
                {
                    if (!vault.TryAcquireWriteLock(in carveHandle, SystemID.TerrainSeams, out NativeArray<VoxelCarveEvent> carves))
                    {
                        failureFlags |= FailureLock;
                        return false;
                    }

                    try
                    {
                        WriteStressCarves(carves, pass);

                        vault.RequestEditorForceDefragmentation();
                        vault.FrostTickDefrag(1f / 60f, pass * (1f / 15f), MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in carveHandle, SystemID.TerrainSeams);
                    }

                    if (!vault.TryAcquireWriteLock(in densityHandle, SystemID.TerrainSeams, out NativeArray<sbyte> density))
                    {
                        failureFlags |= FailureLock;
                        return false;
                    }

                    try
                    {
                        WriteStressDensity(density, pass);

                        vault.RequestEditorForceDefragmentation();
                        vault.FrostTickDefrag(1f / 60f, pass * (1f / 15f), MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in densityHandle, SystemID.TerrainSeams);
                    }

                    vault.RequestEditorForceDefragmentation();
                    vault.FrostTickDefrag(1f / 60f, pass * (1f / 15f), MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                    if (!vault.TryReadOnlyHandle(in carveHandle, out NativeArray<VoxelCarveEvent>.ReadOnly refreshedCarves) ||
                        !vault.TryReadOnlyHandle(in densityHandle, out NativeArray<sbyte>.ReadOnly refreshedDensity) ||
                        refreshedCarves.Length < StressCarveCount ||
                        refreshedDensity.Length < VoxelSurfaceNetsConstants.DensitySampleCount ||
                        refreshedCarves[pass & (StressCarveCount - 1)].VolumeInstanceId == 0ul)
                    {
                        failureFlags |= FailureDefrag;
                        return false;
                    }
                }

                bool relocated = vault.GenerateMockVaultRelocationForValidation(
                    StressSeed,
                    StressCarveCount,
                    MemoryDefragPhase.PreSimulation,
                    vault.ActiveBurstLockMask);
                carveHandle = vault.EnsureGenerationHandle<VoxelCarveEvent>(
                    BufferID.ShinobuDeltaCrusherCarveEventQueue,
                    StressCarveCount,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                densityHandle = vault.EnsureGenerationHandle<sbyte>(
                    VoxelSurfaceNetsVaultBufferIds.Density,
                    VoxelSurfaceNetsConstants.DensitySampleCount,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                if (!relocated ||
                    !vault.TryReadOnlyHandle(in carveHandle, out NativeArray<VoxelCarveEvent>.ReadOnly relocatedCarves) ||
                    !vault.TryReadOnlyHandle(in densityHandle, out NativeArray<sbyte>.ReadOnly relocatedDensity) ||
                    relocatedCarves.Length < StressCarveCount ||
                    relocatedDensity.Length < VoxelSurfaceNetsConstants.DensitySampleCount)
                {
                    failureFlags |= FailureDefrag;
                    return false;
                }
            }
            finally
            {
                Volatile.Write(ref stopThread, 1);
                if (!TryJoinDefragWorkerNoThrow(worker, DefragWorkerJoinMilliseconds))
                    failureFlags |= FailureThread;
            }

            if (workerFaulted || workerIterations <= 0)
                failureFlags |= FailureThread;

            return failureFlags == 0u;
        }

        private static bool TryStartDefragWorkerNoThrow(Thread worker)
        {
            if (worker == null)
                return false;

            try
            {
                worker.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryJoinDefragWorkerNoThrow(Thread worker, int timeoutMilliseconds)
        {
            if (worker == null || !worker.IsAlive)
                return true;

            if (Thread.CurrentThread.ManagedThreadId == worker.ManagedThreadId)
                return false;

            try
            {
                worker.Join(timeoutMilliseconds);
                return !worker.IsAlive;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteStressCarves(NativeArray<VoxelCarveEvent> carves, int pass)
        {
            int count = math.min(carves.Length, StressCarveCount);
            for (int i = 0; i < count; i++)
            {
                double x = pass * 17.0d + i * 0.125d;
                double y = pass * 3.0d + (i & 31) * 0.25d;
                double z = pass * -11.0d + (i >> 5) * 0.5d;
                double3 originAup = double3.zero;
                double3 hitAup = new double3(x, y, z);
                double3 endAup = new double3(x + 0.75d, y, z);
                double3 hitLocal = hitAup - originAup;
                double3 endLocal = endAup - originAup;
                VoxelCarveEvent carve = default;
                carve.VolumeInstanceId = (ulong)(i + 1);
                carve.AbsoluteHitPointDouble = hitAup;
                carve.AbsoluteSegmentEndDouble = endAup;
                carve.AbsoluteHitPoint = new float3((float)hitLocal.x, (float)hitLocal.y, (float)hitLocal.z);
                carve.AbsoluteSegmentEnd = new float3((float)endLocal.x, (float)endLocal.y, (float)endLocal.z);
                carve.AbsoluteHalfExtents = new float3(0.5f, 0.5f, 0.5f);
                carve.AbsoluteImpulseDirection = new float3(1f, 0f, 0f);
                carve.RadiusMeters = 0.75f;
                carve.BlendStrengthMeters = 1f;
                carve.Operation = 1;
                carve.Shape = 1;
                carve.MaterialId = (byte)(1 + (i & 7));
                carves[i] = carve;
            }
        }

        private static void WriteStressDensity(NativeArray<sbyte> density, int pass)
        {
            int count = density.Length;
            for (int i = 0; i < count; i++)
                density[i] = (sbyte)(((i + pass) & 63) - 32);
        }

        private static void ValidateLayoutsOrThrow()
        {
            uint failureFlags = 0u;
            AssertExplicit<VoxelModifiedCell>(8, ref failureFlags);
            AssertOffset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Density), 0, ref failureFlags);
            AssertOffset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Reserved), 2, ref failureFlags);
            AssertOffset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Reserved1), 4, ref failureFlags);
            AssertOffset<VoxelModifiedCell>(nameof(VoxelModifiedCell.MaterialId), 6, ref failureFlags);
            AssertOffset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Flags), 7, ref failureFlags);

            AssertExplicit<VoxelCraterStamp>(32, ref failureFlags);
            AssertOffset<VoxelCraterStamp>(nameof(VoxelCraterStamp.position), 0, ref failureFlags);
            AssertOffset<VoxelCraterStamp>(nameof(VoxelCraterStamp.radius), 24, ref failureFlags);
            AssertOffset<VoxelCraterStamp>(nameof(VoxelCraterStamp.blendRadius), 28, ref failureFlags);

            AssertExplicit<VoxelSdfRaycastHit>(40, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>(nameof(VoxelSdfRaycastHit.Point), 0, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>(nameof(VoxelSdfRaycastHit.Normal), 12, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>(nameof(VoxelSdfRaycastHit.Distance), 24, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>(nameof(VoxelSdfRaycastHit.Density), 28, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>(nameof(VoxelSdfRaycastHit.Hit), 32, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>("_pad0", 33, ref failureFlags);
            AssertOffset<VoxelSdfRaycastHit>("_pad6", 39, ref failureFlags);

            AssertExplicit<CaveNode>(40, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.position), 0, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.radii), 12, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.blendRadius), 24, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.noiseScale), 28, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.noiseAmplitude), 32, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode.roomType), 36, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode._pad0), 37, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode._pad1), 38, ref failureFlags);
            AssertOffset<CaveNode>(nameof(CaveNode._pad2), 39, ref failureFlags);

            AssertExplicit<CaveTunnel>(56, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.pointA), 0, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.pointB), 12, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.radiusA), 24, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.radiusB), 28, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.blendRadius), 32, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.heightScale), 36, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.widthScale), 40, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.warpAmount), 44, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel.tunnelType), 48, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel._pad0), 49, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel._pad1), 50, ref failureFlags);
            AssertOffset<CaveTunnel>(nameof(CaveTunnel._pad2), 51, ref failureFlags);
            AssertOffset<CaveTunnel>("_pad3", 52, ref failureFlags);

            AssertExplicit<CaveEntrance>(72, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.surfacePosition), 0, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.inwardDirection), 12, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.radius), 24, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.funnelLength), 28, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.innerRadius), 32, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.terrainNormal), 36, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.terrainNormalBlend), 48, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.terrainSplatColor), 52, ref failureFlags);
            AssertOffset<CaveEntrance>(nameof(CaveEntrance.terrainSplatBlend), 68, ref failureFlags);

            AssertExplicit<CaveStructure>(48, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.position), 0, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.size), 12, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.pointB), 24, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.blendRadius), 36, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.noiseAmount), 40, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure.structureType), 44, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure._pad0), 45, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure._pad1), 46, ref failureFlags);
            AssertOffset<CaveStructure>(nameof(CaveStructure._pad2), 47, ref failureFlags);

            AssertExplicit<CaveGenerationParams>(80, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.warpFrequency), 0, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.warpAmplitude), 4, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.warpOctaves), 8, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.wallNoiseFrequency), 12, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.wallNoiseAmplitude), 16, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.wallNoiseOctaves), 20, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.wallNoiseLacunarity), 24, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.wallNoisePersistence), 28, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.terraceFrequency), 32, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.terraceAmplitude), 36, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.terraceSharpness), 40, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.globalBlendK), 44, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.shellThickness), 48, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.seed), 52, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.noiseEvalDistance), 56, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.floorFlatness), 60, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.structureBlendK), 64, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.entranceBlendK), 68, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.structureOnlyMode), 72, ref failureFlags);
            AssertOffset<CaveGenerationParams>(nameof(CaveGenerationParams.spawnContext), 73, ref failureFlags);
            AssertOffset<CaveGenerationParams>("_pad0", 74, ref failureFlags);
            AssertOffset<CaveGenerationParams>("_pad5", 79, ref failureFlags);

            AssertExplicit<CaveSpawnData>(16, ref failureFlags);
            AssertOffset<CaveSpawnData>(nameof(CaveSpawnData.position), 0, ref failureFlags);
            AssertOffset<CaveSpawnData>(nameof(CaveSpawnData.hashId), 12, ref failureFlags);

            AssertExplicit<VoxelSonarSdfRaycastHit>(64, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Point), 0, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Normal), 12, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Distance), 24, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Density), 28, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Density01), 32, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.SdfRange), 36, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Version), 40, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>(nameof(VoxelSonarSdfRaycastHit.Flags), 44, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>("_pad0", 48, ref failureFlags);
            AssertOffset<VoxelSonarSdfRaycastHit>("_pad1", 56, ref failureFlags);

            AssertExplicit<VoxelSdfPayloadDescriptorDTO>(80, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.VolumeOrigin), 0, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.GridDimensions), 12, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.VoxelCellSize), 24, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.SdfRangeMeters), 36, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.ByteCount), 40, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.BufferId), 44, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.BufferGeneration), 48, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.SdfVersion), 52, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.OwnerSystemId), 56, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.Flags), 60, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.AudioMaterialByteCount), 64, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.AudioMaterialBufferId), 68, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>(nameof(VoxelSdfPayloadDescriptorDTO.AudioMaterialBufferGeneration), 72, ref failureFlags);
            AssertOffset<VoxelSdfPayloadDescriptorDTO>("_pad0", 76, ref failureFlags);

            AssertExplicit<VoxelVertexDTO>(32, ref failureFlags);
            AssertOffset<VoxelVertexDTO>(nameof(VoxelVertexDTO.Position), 0, ref failureFlags);
            AssertOffset<VoxelVertexDTO>(nameof(VoxelVertexDTO.NormalPacked), 12, ref failureFlags);
            AssertOffset<VoxelVertexDTO>(nameof(VoxelVertexDTO.TangentPacked), 16, ref failureFlags);
            AssertOffset<VoxelVertexDTO>(nameof(VoxelVertexDTO.ColorPacked), 20, ref failureFlags);
            AssertOffset<VoxelVertexDTO>(nameof(VoxelVertexDTO.UV), 24, ref failureFlags);

            AssertExplicit<ChunkMeshingStateDTO>(64, ref failureFlags);
            AssertOffset<ChunkMeshingStateDTO>(nameof(ChunkMeshingStateDTO.ChunkOriginAup), 0, ref failureFlags);
            AssertOffset<ChunkMeshingStateDTO>(nameof(ChunkMeshingStateDTO.BoundsCenterLocal), 24, ref failureFlags);
            AssertOffset<ChunkMeshingStateDTO>(nameof(ChunkMeshingStateDTO.Stage), 60, ref failureFlags);

            AssertExplicit<VoxelMeshingTuningDTO>(64, ref failureFlags);
            AssertOffset<VoxelMeshingTuningDTO>(nameof(VoxelMeshingTuningDTO.LastCsvWriteTicks), 0, ref failureFlags);
            AssertOffset<VoxelMeshingTuningDTO>(nameof(VoxelMeshingTuningDTO.GlobalQualityWeight), 8, ref failureFlags);
            AssertOffset<VoxelMeshingTuningDTO>(nameof(VoxelMeshingTuningDTO.MaxChunksPerFrame), 40, ref failureFlags);
            AssertOffset<VoxelMeshingTuningDTO>(nameof(VoxelMeshingTuningDTO.LastCsvHash), 60, ref failureFlags);

            AssertExplicit<VoxelMeshingTelemetryEntry>(64, ref failureFlags);
            AssertOffset<VoxelMeshingTelemetryEntry>(nameof(VoxelMeshingTelemetryEntry.Frame), 0, ref failureFlags);
            AssertOffset<VoxelMeshingTelemetryEntry>(nameof(VoxelMeshingTelemetryEntry.DumpReason), 48, ref failureFlags);
            AssertOffset<VoxelMeshingTelemetryEntry>(nameof(VoxelMeshingTelemetryEntry._pad0), 56, ref failureFlags);

            AssertExplicit<VoxelSurfaceAabbDTO>(64, ref failureFlags);
            AssertOffset<VoxelSurfaceAabbDTO>(nameof(VoxelSurfaceAabbDTO.CenterAup), 0, ref failureFlags);
            AssertOffset<VoxelSurfaceAabbDTO>(nameof(VoxelSurfaceAabbDTO.ExtentsLocal), 24, ref failureFlags);
            AssertOffset<VoxelSurfaceAabbDTO>(nameof(VoxelSurfaceAabbDTO._pad1), 48, ref failureFlags);

            AssertExplicit<VoxelSurfaceModifiedSignal>(64, ref failureFlags);
            AssertOffset<VoxelSurfaceModifiedSignal>(nameof(VoxelSurfaceModifiedSignal.ChunkOriginAup), 0, ref failureFlags);
            AssertOffset<VoxelSurfaceModifiedSignal>(nameof(VoxelSurfaceModifiedSignal.ChunkCoord), 24, ref failureFlags);
            AssertOffset<VoxelSurfaceModifiedSignal>(nameof(VoxelSurfaceModifiedSignal._pad1), 48, ref failureFlags);

            AssertExplicit<VoxelSurfacePriorityDTO>(16, ref failureFlags);
            AssertExplicit<VoxelSurfaceIndirectArgsDTO>(32, ref failureFlags);
            AssertExplicit<MockVoxelDensityArray>(48, ref failureFlags);
            AssertExplicit<VoxelSurfacePhysicsBakeRequestDTO>(32, ref failureFlags);
            AssertExplicit<VoxelSurfaceHzbTileDTO>(16, ref failureFlags);

            if (!VoxelDeltaProcessor.ValidateAgent1304PrivateLayouts(ref failureFlags))
                failureFlags |= FailureLayout;

            if (!global::HectonVoxelEngine.ValidateAgent1304EnginePrivateLayouts(ref failureFlags))
                failureFlags |= FailureLayout;

            if (failureFlags != 0u)
                throw new FatalArchitectureException("1304 voxel DTO layout violation.");
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureFlags)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }
    }
}
#endif
