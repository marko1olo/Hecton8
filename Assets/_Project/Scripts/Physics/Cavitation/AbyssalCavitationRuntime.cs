using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    public static class AbyssalCavitationRuntime
    {
        private const SystemID OwnerSystem = SystemID.Physics;
        private static readonly int _shockwavesShaderId = Shader.PropertyToID("_H8CavitationShockwaves");
        private static readonly int _shockwaveCountShaderId = Shader.PropertyToID("_H8CavitationShockwaveCount");
        private static readonly int _shockwaveParamsShaderId = Shader.PropertyToID("_H8CavitationShockwaveParams");

        private static IDataVault _vault;
        private static uint _resolvedVaultGeneration;
        private static bool _initialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool _layoutValidated;
#endif
        private static bool _jobScheduled;
        private static bool _csvLoaded;
        private static bool _defaultCsvLoadAttempted;
        private static JobHandle _scheduledHandle;
        private static long _scheduleTimestamp;
        private static float _lastSolveMicroseconds;
        private static uint _frameIndex;

        private static VaultBufferHandle<ShockwaveEventDTO> _shockwaveHandle;
        private static VaultBufferHandle<ShockwaveCounterBlock> _counterHandle;
        private static VaultBufferHandle<ShockwaveEntitySnapshotDTO> _entityHandle;
        private static VaultBufferHandle<ShockwaveForcePacketDTO> _forceHandle;
        private static VaultBufferHandle<CavitationVisualSphereDTO> _visualHandle;
        private static VaultBufferHandle<ShockwaveTelemetryEntry> _telemetryHandle;
        private static VaultBufferHandle<OrdnanceProfileDTO> _profileHandle;
        private static VaultBufferHandle<byte> _csvScratchHandle;
        private static VaultBufferHandle<AbyssalCavitationTuningDTO> _tuningHandle;
        private static VaultBufferHandle<AbyssalCavitationSdfVolumeDTO> _sdfDescriptorHandle;
        private static VaultBufferHandle<sbyte> _sdfVoxelsHandle;

        private static GraphicsBuffer _visualBufferA;
        private static GraphicsBuffer _visualBufferB;
        private static GraphicsBuffer _emptyVisualBuffer;
        private static int _visualPage;
        private static int _lastUploadedVisualCount = -1;
        private static float _lastUploadedQuality = -1f;
        private static float _lastUploadedVisualIntensity = -1f;
        private static uint _lastUploadedFrameIndex;
        private static GraphicsBuffer _lastUploadedBuffer;

        public static uint FrameIndex => _frameIndex;
        public static bool HasScheduledWork => _jobScheduled;

        public static bool EnsureInitialized(IDataVault explicitVault = null)
        {
            if (_initialized && _vault != null)
            {
                if (explicitVault == null && _resolvedVaultGeneration == _vault.VaultGenerationID)
                    return true;
                if (explicitVault != null && ReferenceEquals(_vault, explicitVault) && _resolvedVaultGeneration == explicitVault.VaultGenerationID)
                    return true;
            }

            IDataVault vault = explicitVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                vault = latestVault;

            if (vault == null)
                return false;

            ValidateLayoutColdOnce();
            _vault = vault;
            _shockwaveHandle = vault.GetBufferHandle<ShockwaveEventDTO>(
                AbyssalCavitationVaultBufferIds.ShockwaveEvents,
                AbyssalCavitationConstants.MaxShockwaves,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _counterHandle = vault.GetBufferHandle<ShockwaveCounterBlock>(
                AbyssalCavitationVaultBufferIds.ShockwaveCounters,
                AbyssalCavitationConstants.CounterBlockCount,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _entityHandle = vault.GetBufferHandle<ShockwaveEntitySnapshotDTO>(
                AbyssalCavitationVaultBufferIds.EntitySnapshots,
                AbyssalCavitationConstants.MaxEntitySnapshots,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _forceHandle = vault.GetBufferHandle<ShockwaveForcePacketDTO>(
                AbyssalCavitationVaultBufferIds.ForcePackets,
                AbyssalCavitationConstants.MaxForcePackets,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.GetBufferHandle<CavitationVisualSphereDTO>(
                AbyssalCavitationVaultBufferIds.VisualSpheres,
                AbyssalCavitationConstants.MaxVisualSpheres,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<ShockwaveTelemetryEntry>(
                AbyssalCavitationVaultBufferIds.TelemetryRing,
                AbyssalCavitationConstants.TelemetryCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _profileHandle = vault.GetBufferHandle<OrdnanceProfileDTO>(
                AbyssalCavitationVaultBufferIds.OrdnanceProfiles,
                AbyssalCavitationConstants.OrdnanceProfileCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                AbyssalCavitationVaultBufferIds.CsvScratch,
                AbyssalCavitationConstants.CsvScratchBytes,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<AbyssalCavitationTuningDTO>(
                AbyssalCavitationVaultBufferIds.Tuning,
                1,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _sdfDescriptorHandle = vault.GetBufferHandle<AbyssalCavitationSdfVolumeDTO>(
                AbyssalCavitationVaultBufferIds.SdfDescriptor,
                AbyssalCavitationConstants.SdfDescriptorCount,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _sdfVoxelsHandle = vault.GetBufferHandle<sbyte>(
                AbyssalCavitationVaultBufferIds.SdfVoxels,
                AbyssalCavitationConstants.SdfVoxelCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);

            InitializeBuffersCold(vault);
            _csvLoaded = false;
            _defaultCsvLoadAttempted = false;
            _resolvedVaultGeneration = vault.VaultGenerationID;
            _initialized = true;
            return true;
        }

        private static void ValidateLayoutColdOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_layoutValidated)
                return;

            AbyssalCavitationLayout.ValidateOrThrow();
            _layoutValidated = true;
#endif
        }

        public static bool TryGetTuning(out AbyssalCavitationTuningDTO tuning)
        {
            tuning = default;
            if (!EnsureInitialized())
                return false;

            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            return true;
        }

        public static bool TryApplyTuning(in AbyssalCavitationTuningDTO tuning)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuningArray[0] = AbyssalCavitationSanitizer.SanitizeTuning(tuning);
            return true;
        }

        public static bool TryWriteEntitySnapshot(
            int slot,
            double3 aup,
            float3 velocity,
            float effectiveArea,
            float inverseMass,
            int rigidbodySlot,
            uint entityHash,
            uint flags)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveEntitySnapshotDTO> entities = _entityHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!entities.IsCreated || !counters.IsCreated || (uint)slot >= (uint)entities.Length)
                return false;

            uint safeFlags = flags | AbyssalCavitationEntityFlags.Active | AbyssalCavitationEntityFlags.ForceReceiver;
            if (!math.all(math.isfinite(aup)) || !math.all(math.isfinite(velocity)))
                safeFlags |= AbyssalCavitationEntityFlags.NonFinite;

            entities[slot] = new ShockwaveEntitySnapshotDTO
            {
                AUP = math.all(math.isfinite(aup)) ? aup : double3.zero,
                Velocity = math.all(math.isfinite(velocity)) ? velocity : float3.zero,
                EffectiveArea = math.clamp(math.isfinite(effectiveArea) ? effectiveArea : 1f, 0.05f, 64f),
                InverseMass = math.clamp(math.isfinite(inverseMass) ? inverseMass : 1f, 0f, 1000f),
                RigidbodySlot = rigidbodySlot,
                EntityHash = entityHash != 0u ? entityHash : unchecked((uint)(slot + 1) * 2654435761u),
                Flags = safeFlags
            };

            int current = counters[AbyssalCavitationCounterIndex.CandidateCount].Value;
            if (slot + 1 > current)
            {
                ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CandidateCount];
                block.Value = math.min(slot + 1, entities.Length);
                counters[AbyssalCavitationCounterIndex.CandidateCount] = block;
            }

            return true;
        }

        public static bool TryClearEntitySnapshots()
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!counters.IsCreated || counters.Length <= AbyssalCavitationCounterIndex.CandidateCount)
                return false;

            ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CandidateCount];
            block.Value = 0;
            counters[AbyssalCavitationCounterIndex.CandidateCount] = block;
            return true;
        }

        public static bool TryWriteSdfVolume(
            double3 originAup,
            int3 dimensions,
            float3 cellSizeMeters,
            float decodeRangeMeters,
            NativeArray<sbyte> signedDistanceBytes,
            uint version)
        {
            if (!EnsureInitialized() || !signedDistanceBytes.IsCreated || _jobScheduled)
                return false;

            int3 maxDimensions = new int3(
                AbyssalCavitationConstants.SdfVolumeDimX,
                AbyssalCavitationConstants.SdfVolumeDimY,
                AbyssalCavitationConstants.SdfVolumeDimZ);
            if (!math.all(dimensions > 0) ||
                !math.all(dimensions <= maxDimensions) ||
                !math.all(math.isfinite(cellSizeMeters)) ||
                !math.all(cellSizeMeters > 0.0001f) ||
                !math.all(math.isfinite(originAup)) ||
                !math.isfinite(decodeRangeMeters))
            {
                return false;
            }

            int voxelCount = dimensions.x * dimensions.y * dimensions.z;
            NativeArray<AbyssalCavitationSdfVolumeDTO> descriptors = _sdfDescriptorHandle.Resolve(_vault);
            NativeArray<sbyte> voxels = _sdfVoxelsHandle.Resolve(_vault);
            if (!descriptors.IsCreated ||
                descriptors.Length == 0 ||
                !voxels.IsCreated ||
                voxelCount <= 0 ||
                voxelCount > voxels.Length ||
                voxelCount > signedDistanceBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < voxelCount; i++)
                voxels[i] = signedDistanceBytes[i];
            for (int i = voxelCount; i < voxels.Length; i++)
                voxels[i] = sbyte.MaxValue;

            descriptors[0] = new AbyssalCavitationSdfVolumeDTO
            {
                OriginAUP = originAup,
                CellSizeMeters = math.max(cellSizeMeters, new float3(0.0001f)),
                Dimensions = dimensions,
                DecodeRangeMeters = math.clamp(decodeRangeMeters, 0.05f, 512f),
                Version = version,
                Flags = AbyssalCavitationSdfFlags.Active | AbyssalCavitationSdfFlags.SignedDistanceBytes
            };
            return true;
        }

        public static bool TryClearSdfVolume()
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<AbyssalCavitationSdfVolumeDTO> descriptors = _sdfDescriptorHandle.Resolve(_vault);
            if (!descriptors.IsCreated || descriptors.Length == 0)
                return false;

            descriptors[0] = default;
            return true;
        }

        public static bool TryQueueRuntimeDetonation(
            Vector3 runtimePosition,
            float maxRadius,
            float peakPressure,
            float expansionSpeed,
            uint sourceHash)
        {
            double3 aup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            return TryQueueDetonationAup(aup, maxRadius, peakPressure, expansionSpeed, sourceHash);
        }

        public static bool TryQueueDetonationAup(
            double3 epicenterAup,
            float maxRadius,
            float peakPressure,
            float expansionSpeed,
            uint sourceHash)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveEventDTO> shockwaves = _shockwaveHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!shockwaves.IsCreated || !counters.IsCreated)
                return false;

            int activeCount = math.clamp(counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, shockwaves.Length);
            if (activeCount >= shockwaves.Length)
                return false;

            ShockwaveEventDTO wave = default;
            wave.EpicenterAUP = math.all(math.isfinite(epicenterAup)) ? epicenterAup : double3.zero;
            wave.CurrentRadius = 0.05f;
            wave.MaxRadius = math.clamp(math.isfinite(maxRadius) ? maxRadius : 24f, 1f, 2048f);
            wave.PeakPressure = math.clamp(math.isfinite(peakPressure) ? peakPressure : 9000f, 1f, 100000000f);
            wave.ExpansionSpeed = math.clamp(math.isfinite(expansionSpeed) ? expansionSpeed : 220f, 1f, 2000f);
            wave.SourceHashID = sourceHash != 0u ? sourceHash : AbyssalCavitationConstants.SourceHash;
            shockwaves[activeCount] = wave;

            ShockwaveCounterBlock countBlock = counters[AbyssalCavitationCounterIndex.ActiveShockwaves];
            countBlock.Value = activeCount + 1;
            counters[AbyssalCavitationCounterIndex.ActiveShockwaves] = countBlock;

            PublishImpulseSignals(in wave);
            return true;
        }

        public static bool TryQueueOrdnanceDetonationAup(double3 epicenterAup, uint profileHash)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<OrdnanceProfileDTO> profiles = _profileHandle.Resolve(_vault);
            if (!profiles.IsCreated)
                return false;

            if (!AbyssalCavitationOrdnanceCsv.TryFindProfile(profiles, profileHash, out OrdnanceProfileDTO profile))
                return false;

            return TryQueueDetonationAup(
                epicenterAup,
                profile.MaxRadius,
                profile.PeakPressure,
                profile.ExpansionSpeed,
                profile.SourceHash);
        }

        public static bool GenerateMockDetonations(uint sectorHash = 0x5348494Eu)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveEventDTO> shockwaves = _shockwaveHandle.Resolve(_vault);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = _entityHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!shockwaves.IsCreated || !entities.IsCreated || !counters.IsCreated)
                return false;

            var job = new GenerateMockDetonationsJob
            {
                Shockwaves = shockwaves,
                Entities = entities,
                Counters = counters,
                OriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                FrameIndex = ++_frameIndex,
                SectorHash = sectorHash != 0u ? sectorHash : 0x5348494Eu,
                GlobalQualityWeight = ResolveGlobalQualityWeight()
            };
            JobHandle handle = job.Schedule(32, 8);
            H8Memory.RegisterActiveJob(OwnerSystem, handle);
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD SYNC JOB: CI/editor fallback injection, never the live propagation path.
            return true;
        }

        public static JobHandle ScheduleSimulation(
            float simulationTickDelta,
            JobHandle inputDependency = default)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return inputDependency;

            NativeArray<ShockwaveEventDTO> shockwaves = _shockwaveHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = _entityHandle.Resolve(_vault);
            NativeArray<ShockwaveForcePacketDTO> forcePackets = _forceHandle.Resolve(_vault);
            NativeArray<CavitationVisualSphereDTO> visuals = _visualHandle.Resolve(_vault);
            NativeArray<ShockwaveTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            NativeArray<AbyssalCavitationSdfVolumeDTO> sdfDescriptors = _sdfDescriptorHandle.Resolve(_vault);
            NativeArray<sbyte> sdfVoxels = _sdfVoxelsHandle.Resolve(_vault);
            if (!shockwaves.IsCreated ||
                !counters.IsCreated ||
                !entities.IsCreated ||
                !forcePackets.IsCreated ||
                !visuals.IsCreated ||
                !telemetry.IsCreated ||
                !tuningArray.IsCreated ||
                !sdfDescriptors.IsCreated ||
                !sdfVoxels.IsCreated ||
                tuningArray.Length == 0)
            {
                return inputDependency;
            }

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.SimulationTickDelta = math.clamp(math.isfinite(simulationTickDelta) ? simulationTickDelta : tuning.SimulationTickDelta, 0.0001f, 0.1f);
            tuningArray[0] = tuning;

            MockSDFSampler mockSdf = new MockSDFSampler
            {
                SphereCenter = new float3(18f, tuning.MockSeafloorY + 8f, -12f),
                SphereRadius = 16f,
                SecondarySphereCenter = new float3(-22f, tuning.MockSeafloorY + 11f, 15f),
                SecondarySphereRadius = 12f,
                PlaneY = tuning.MockSeafloorY
            };

            uint frame = ++_frameIndex;
            JobHandle propagateHandle = new PropagateShockwavesJob
            {
                Shockwaves = shockwaves,
                SimulationTickDelta = tuning.SimulationTickDelta
            }.Schedule(shockwaves.Length, 32, inputDependency);

            JobHandle compactHandle = new CompactShockwavesJob
            {
                Shockwaves = shockwaves,
                Counters = counters
            }.Schedule(propagateHandle);

            JobHandle pressureHandle = new EvaluateShockwavePressureJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Entities = entities,
                ForcePackets = forcePackets,
                SdfVoxels = sdfVoxels,
                Tuning = tuning,
                SdfVolume = sdfDescriptors.Length > 0 ? sdfDescriptors[0] : default,
                MockSdf = mockSdf,
                SdfReferenceAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                FrameIndex = frame
            }.Schedule(entities.Length, 32, compactHandle);

            JobHandle visualHandle = new BuildCavitationVisualsJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Visuals = visuals,
                LocalOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                Tuning = tuning,
                FrameIndex = frame
            }.Schedule(visuals.Length, 32, compactHandle);

            _scheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _scheduledHandle = new RecordShockwaveTelemetryJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                ForcePackets = forcePackets,
                Telemetry = telemetry,
                Tuning = tuning,
                FrameIndex = frame,
                CpuMicroseconds = _lastSolveMicroseconds
            }.Schedule(JobHandle.CombineDependencies(pressureHandle, visualHandle));
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _scheduledHandle);
            return _scheduledHandle;
        }

        public static bool CompleteScheduledIfReady(bool force)
        {
            if (!_jobScheduled)
                return true;

            if (!force && !_scheduledHandle.IsCompleted)
                return false;

            bool completed = force
                ? DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true)
                : DispatcherJobFence.TryFinalizeCompleted(ref _scheduledHandle);
            if (!completed)
                return false;

            _jobScheduled = false;
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - _scheduleTimestamp;
            _lastSolveMicroseconds = (float)math.max(0.0, elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            PatchLatestTelemetryCpu(_lastSolveMicroseconds);

            if (TrySampleLatestTelemetry(out ShockwaveTelemetryEntry entry) &&
                (entry.Flags & AbyssalCavitationTelemetryFlags.NonFiniteRecovered) != 0u)
            {
                TryDumpBlackBox(entry.Flags);
            }

            return true;
        }

        private static void PatchLatestTelemetryCpu(float microseconds)
        {
            NativeArray<ShockwaveTelemetryEntry> ring = _telemetryHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!ring.IsCreated || !counters.IsCreated || ring.Length == 0)
                return;

            int head = counters[AbyssalCavitationCounterIndex.TelemetryHead].Value;
            int index = head - 1;
            if (index < 0)
                index = ring.Length - 1;
            ShockwaveTelemetryEntry entry = ring[index];
            entry.CpuMicroseconds = math.isfinite(microseconds) ? math.max(0f, microseconds) : 0f;
            ring[index] = entry;
        }

        public static int FlushForcesToPhysics(double3 localOriginAUP, uint frameIndex = 0u, int maxPackets = 64)
        {
            if (!EnsureInitialized())
                return 0;

            if (!CompleteScheduledIfReady(false))
                return 0;

            NativeArray<ShockwaveForcePacketDTO> packets = _forceHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!packets.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            PhysicsApplySystem.DrainCavitationForcePackets(
                packets,
                counters,
                AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]),
                localOriginAUP,
                frameIndex,
                maxPackets,
                out int accepted,
                out _);

            return accepted;
        }

        public static int FlushForcesToPhysics(Rigidbody[] bodySlots, double3 localOriginAUP, uint frameIndex = 0u, int maxPackets = 64)
        {
            if (bodySlots == null || bodySlots.Length == 0 || !EnsureInitialized())
                return 0;

            if (!CompleteScheduledIfReady(false))
                return 0;

            NativeArray<ShockwaveForcePacketDTO> packets = _forceHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!packets.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            if (system == null)
                return 0;

            int candidateCount = math.clamp(counters[AbyssalCavitationCounterIndex.CandidateCount].Value, 0, packets.Length);
            int safeLimit = math.min(candidateCount, math.clamp(maxPackets, 0, packets.Length));
            int accepted = 0;
            for (int i = 0; i < safeLimit; i++)
            {
                ShockwaveForcePacketDTO packet = packets[i];
                if ((packet.Flags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;
                if (frameIndex != 0u && packet.FrameIndex != frameIndex)
                    continue;
                if ((uint)packet.RigidbodySlot >= (uint)bodySlots.Length)
                    continue;

                Rigidbody body = bodySlots[packet.RigidbodySlot];
                if (body == null)
                    continue;

                float3 force = packet.Force;
                float forceSq = math.lengthsq(force);
                if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                    continue;

                float maxForce = math.max(1f, tuning.MaxForceNewton);
                if (forceSq > maxForce * maxForce)
                    force *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));

                float3 point = LocalAupToFloat3(packet.ApplicationAUP - localOriginAUP);
                if (system.QueueForceAtPosition(
                        body,
                        new Vector3(force.x, force.y, force.z),
                        new Vector3(point.x, point.y, point.z),
                        ForceMode.Impulse,
                        ForcePacketPriority.Critical,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
            }

            return accepted;
        }

        public static void EnsureGraphicsBuffers()
        {
            if (_emptyVisualBuffer == null)
                _emptyVisualBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(1);
            if (_visualBufferA == null)
                _visualBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(AbyssalCavitationConstants.MaxVisualSpheres);
            if (_visualBufferB == null)
                _visualBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(AbyssalCavitationConstants.MaxVisualSpheres);
        }

        public static void ReleaseGraphicsBuffers()
        {
            _visualBufferA?.Dispose();
            _visualBufferB?.Dispose();
            _emptyVisualBuffer?.Dispose();
            _visualBufferA = null;
            _visualBufferB = null;
            _emptyVisualBuffer = null;
            _lastUploadedVisualCount = -1;
            _lastUploadedQuality = -1f;
            _lastUploadedVisualIntensity = -1f;
            _lastUploadedFrameIndex = 0u;
            _lastUploadedBuffer = null;
        }

        public static int SyncShaderVisuals(CommandBuffer commandBuffer = null)
        {
            if (!EnsureInitialized())
                return 0;

            if (!CompleteScheduledIfReady(false))
                return math.max(0, _lastUploadedVisualCount);
            EnsureGraphicsBuffers();

            NativeArray<CavitationVisualSphereDTO> visuals = _visualHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!visuals.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            float q = Smooth01(tuning.GlobalQualityWeight);
            int active = math.clamp(counters[AbyssalCavitationCounterIndex.VisualCount].Value, 0, visuals.Length);
            int qualityLimit = math.clamp((int)math.round(math.lerp(2f, AbyssalCavitationConstants.MaxVisualSpheres, q)), 1, AbyssalCavitationConstants.MaxVisualSpheres);
            int uploadCount = math.min(active, qualityLimit);
            float visualIntensity = tuning.VisualIntensityScale;
            bool reuseUploadedBuffer = _lastUploadedBuffer != null &&
                                       _lastUploadedFrameIndex == _frameIndex &&
                                       _lastUploadedVisualCount == uploadCount &&
                                       math.abs(_lastUploadedQuality - q) <= 0.00001f &&
                                       math.abs(_lastUploadedVisualIntensity - visualIntensity) <= 0.00001f;
            GraphicsBuffer target = reuseUploadedBuffer
                ? _lastUploadedBuffer
                : uploadCount > 0
                    ? ((_visualPage++ & 1) == 0 ? _visualBufferA : _visualBufferB)
                    : _emptyVisualBuffer;

            if (!reuseUploadedBuffer && uploadCount > 0)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(target, visuals, uploadCount);
            }

            if (!reuseUploadedBuffer)
            {
                _lastUploadedBuffer = target;
                _lastUploadedVisualCount = uploadCount;
                _lastUploadedQuality = q;
                _lastUploadedVisualIntensity = visualIntensity;
                _lastUploadedFrameIndex = _frameIndex;
            }

            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalBuffer(_shockwavesShaderId, target);
                commandBuffer.SetGlobalInt(_shockwaveCountShaderId, uploadCount);
                commandBuffer.SetGlobalVector(_shockwaveParamsShaderId, new Vector4(q, visualIntensity, uploadCount, _frameIndex));
            }
            else
            {
                Shader.SetGlobalBuffer(_shockwavesShaderId, target);
                Shader.SetGlobalInt(_shockwaveCountShaderId, uploadCount);
                Shader.SetGlobalVector(_shockwaveParamsShaderId, new Vector4(q, visualIntensity, uploadCount, _frameIndex));
            }

            return uploadCount;
        }

        public static bool TryLoadDefaultOrdnanceCsv()
        {
            return TryLoadDefaultOrdnanceCsv(false);
        }

        public static bool TryLoadDefaultOrdnanceCsv(bool forceReload)
        {
            if (_csvLoaded && !forceReload)
                return true;

            if (!EnsureInitialized())
                return false;

            if (_jobScheduled)
                return false;

            if (_defaultCsvLoadAttempted && !forceReload)
                return false;

            _defaultCsvLoadAttempted = true;
            string path = Path.Combine(Application.dataPath, "_Project", "Data", "Combat", "ordnance_specs.csv");
            return TryLoadOrdnanceCsv(path);
        }

        public static bool TryLoadOrdnanceCsv(string csvPath)
        {
            if (!EnsureInitialized() || _jobScheduled || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
            NativeArray<OrdnanceProfileDTO> profiles = _profileHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!scratch.IsCreated || !profiles.IsCreated || !counters.IsCreated)
                return false;

            int bytesRead = 0;
            try
            {
                using (FileStream stream = File.OpenRead(csvPath))
                {
                    while (bytesRead < scratch.Length)
                    {
                        int value = stream.ReadByte();
                        if (value < 0)
                            break;
                        scratch[bytesRead++] = (byte)value;
                    }

                    if (bytesRead == scratch.Length && stream.ReadByte() >= 0)
                        return false;
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SHINOBU_156] Ordnance CSV load failed.");
#endif
                return false;
            }

            int parsed;
            unsafe
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                parsed = AbyssalCavitationOrdnanceCsv.Parse(new ReadOnlySpan<byte>(ptr, bytesRead), profiles);
            }

            ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CsvProfileCount];
            block.Value = parsed;
            counters[AbyssalCavitationCounterIndex.CsvProfileCount] = block;
            _csvLoaded = parsed > 0;
            return _csvLoaded;
        }

        public static bool TrySampleLatestTelemetry(out ShockwaveTelemetryEntry telemetry)
        {
            telemetry = default;
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveTelemetryEntry> ring = _telemetryHandle.Resolve(_vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(_vault);
            if (!ring.IsCreated || !counters.IsCreated || ring.Length == 0)
                return false;

            int head = counters[AbyssalCavitationCounterIndex.TelemetryHead].Value;
            int index = head - 1;
            if (index < 0)
                index = ring.Length - 1;
            telemetry = ring[index];
            return telemetry.FrameIndex != 0u;
        }

        public static bool TryDumpBlackBox(uint reasonFlags)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveTelemetryEntry> ring = _telemetryHandle.Resolve(_vault);
            if (!ring.IsCreated)
                return false;

            string path = Path.Combine(Directory.GetCurrentDirectory(), AbyssalCavitationConstants.DumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    unsafe
                    {
                        ulong magic = 0x5355524756414348UL; // H8CAVGRS little-endian marker.
                        uint version = 1u;
                        stream.Write(new ReadOnlySpan<byte>(&magic, sizeof(ulong)));
                        stream.Write(new ReadOnlySpan<byte>(&version, sizeof(uint)));
                        stream.Write(new ReadOnlySpan<byte>(&reasonFlags, sizeof(uint)));
                        void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                        int bytes = UnsafeUtility.SizeOf<ShockwaveTelemetryEntry>() * ring.Length;
                        stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
                    }
                }

                return true;
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SHINOBU_156] Cavitation black-box dump failed.");
#endif
                return false;
            }
        }

        public static bool IsCsvLoaded()
        {
            return _csvLoaded;
        }

        private static void InitializeBuffersCold(IDataVault vault)
        {
            NativeArray<ShockwaveEventDTO> shockwaves = _shockwaveHandle.Resolve(vault);
            NativeArray<ShockwaveCounterBlock> counters = _counterHandle.Resolve(vault);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = _entityHandle.Resolve(vault);
            NativeArray<ShockwaveForcePacketDTO> forcePackets = _forceHandle.Resolve(vault);
            NativeArray<CavitationVisualSphereDTO> visuals = _visualHandle.Resolve(vault);
            NativeArray<ShockwaveTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<OrdnanceProfileDTO> profiles = _profileHandle.Resolve(vault);
            NativeArray<AbyssalCavitationTuningDTO> tuning = _tuningHandle.Resolve(vault);
            NativeArray<AbyssalCavitationSdfVolumeDTO> sdfDescriptors = _sdfDescriptorHandle.Resolve(vault);
            NativeArray<sbyte> sdfVoxels = _sdfVoxelsHandle.Resolve(vault);

            int count = math.max(
                math.max(
                    math.max(AbyssalCavitationConstants.MaxShockwaves, AbyssalCavitationConstants.MaxEntitySnapshots),
                    AbyssalCavitationConstants.SdfVoxelCapacity),
                AbyssalCavitationConstants.TelemetryCapacity);
            var job = new InitializeAbyssalCavitationBuffersJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Entities = entities,
                ForcePackets = forcePackets,
                Visuals = visuals,
                Telemetry = telemetry,
                Profiles = profiles,
                Tuning = tuning,
                SdfDescriptors = sdfDescriptors,
                SdfVoxels = sdfVoxels,
                GlobalQualityWeight = ResolveGlobalQualityWeight()
            };
            JobHandle handle = job.Schedule(count, 64);
            H8Memory.RegisterActiveJob(OwnerSystem, handle);
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD SYNC JOB: required after UninitializedMemory vault acquisition.
        }

        private static void PublishImpulseSignals(in ShockwaveEventDTO wave)
        {
            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAbsolutePosition(wave.EpicenterAUP);
            float intensity = math.saturate(wave.PeakPressure * 0.00008f);

            AcousticPingSignal ping = default;
            ping.PositionAup = position;
            ping.RadiusMeters = wave.MaxRadius;
            ping.Intensity01 = intensity;
            ping.SourceId = wave.SourceHashID;
            ping.Channel = AcousticPingSignal.ChannelMetalStress;
            ping.Flags = AcousticPingSignal.FlagActiveSonar;
            SignalBus<AcousticPingSignal>.Push(in ping);

            WakeRequestSignal wake = default;
            wake.OriginAup = wave.EpicenterAUP;
            wake.RadiusMeters = wave.MaxRadius;
            wake.SourceHash = wave.SourceHashID;
            wake.Frame = _frameIndex;
            wake.Flags = 1;
            SignalBus<WakeRequestSignal>.Push(in wake);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float3 LocalAupToFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Abyssal Cavitation Runtime")]
    public sealed class AbyssalCavitationRuntimeHost : MonoBehaviour, IFixedTickable, ILateFrameTickable, ISlowTickable
    {
        [SerializeField] private bool autoLoadCsv = true;
        [SerializeField] private bool uploadShaderVisuals = true;
        [SerializeField] private bool injectMockOnEnable;
        [SerializeField] private bool drawDebugGizmos = true;

        private bool _registeredFixed;
        private bool _registeredLate;
        private bool _registeredSlow;

        private void Awake()
        {
            AbyssalCavitationRuntime.EnsureInitialized();
            AbyssalCavitationRuntime.EnsureGraphicsBuffers();
            if (autoLoadCsv)
                AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv();
        }

        private void OnEnable()
        {
            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (injectMockOnEnable)
                AbyssalCavitationRuntime.GenerateMockDetonations();
        }

        private void OnDisable()
        {
            AbyssalCavitationRuntime.CompleteScheduledIfReady(true);
            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredFixed = false;
            _registeredLate = false;
            _registeredSlow = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            AbyssalCavitationRuntime.ScheduleSimulation(fixedDeltaTime);
        }

        public void LateFrameTick()
        {
            AbyssalCavitationRuntime.CompleteScheduledIfReady(false);
            if (uploadShaderVisuals)
                AbyssalCavitationRuntime.SyncShaderVisuals();
        }

        public void SlowTick()
        {
            AbyssalCavitationRuntime.EnsureInitialized();
            if (autoLoadCsv && !AbyssalCavitationRuntime.IsCsvLoaded())
                AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv();
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || !Application.isPlaying)
                return;

            if (!AbyssalCavitationRuntime.EnsureInitialized())
                return;
            if (AbyssalCavitationRuntime.HasScheduledWork)
                return;

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;
            if (!vault.TryGetBufferHandle(AbyssalCavitationVaultBufferIds.ShockwaveEvents, out VaultBufferHandle<ShockwaveEventDTO> waveHandle) ||
                !vault.TryGetBufferHandle(AbyssalCavitationVaultBufferIds.ShockwaveCounters, out VaultBufferHandle<ShockwaveCounterBlock> counterHandle))
            {
                return;
            }

            NativeArray<ShockwaveEventDTO> waves = waveHandle.Resolve(vault);
            NativeArray<ShockwaveCounterBlock> counters = counterHandle.Resolve(vault);
            if (!waves.IsCreated || !counters.IsCreated)
                return;

            int count = math.clamp(counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, waves.Length);
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            for (int i = 0; i < count; i++)
            {
                ShockwaveEventDTO wave = waves[i];
                if (wave.CurrentRadius <= 0f)
                    continue;

                double3 delta = wave.EpicenterAUP - origin;
                float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                if (!math.all(math.isfinite(local)))
                    continue;

                Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.16f);
                Gizmos.DrawWireSphere(new Vector3(local.x, local.y, local.z), math.max(0.01f, wave.MaxRadius));
                Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.42f);
                Gizmos.DrawWireSphere(new Vector3(local.x, local.y, local.z), math.max(0.01f, wave.CurrentRadius));
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeAbyssalCavitationBuffersJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<CavitationVisualSphereDTO> Visuals;
        [NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<OrdnanceProfileDTO> Profiles;
        [NoAlias] public NativeArray<AbyssalCavitationTuningDTO> Tuning;
        [NoAlias] public NativeArray<AbyssalCavitationSdfVolumeDTO> SdfDescriptors;
        [NoAlias] public NativeArray<sbyte> SdfVoxels;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (Shockwaves.IsCreated && index < Shockwaves.Length)
            {
                ShockwaveEventDTO wave = default;
                wave.CurrentRadius = -1f;
                Shockwaves[index] = wave;
            }

            if (Counters.IsCreated && index < Counters.Length)
                Counters[index] = default;

            if (Entities.IsCreated && index < Entities.Length)
            {
                ShockwaveEntitySnapshotDTO entity = default;
                entity.RigidbodySlot = -1;
                entity.InverseMass = 1f;
                Entities[index] = entity;
            }

            if (ForcePackets.IsCreated && index < ForcePackets.Length)
                ForcePackets[index] = default;
            if (Visuals.IsCreated && index < Visuals.Length)
                Visuals[index] = default;
            if (Telemetry.IsCreated && index < Telemetry.Length)
                Telemetry[index] = default;
            if (Profiles.IsCreated && index < Profiles.Length)
                Profiles[index] = default;
            if (SdfVoxels.IsCreated && index < SdfVoxels.Length)
                SdfVoxels[index] = sbyte.MaxValue;
            if (Tuning.IsCreated && index == 0)
                Tuning[0] = AbyssalCavitationSanitizer.DefaultTuning(GlobalQualityWeight);
            if (SdfDescriptors.IsCreated && index == 0)
                SdfDescriptors[0] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDetonationsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        public double3 OriginAUP;
        public uint FrameIndex;
        public uint SectorHash;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            float q = Smooth01(GlobalQualityWeight);
            if (index == 0 && Counters.IsCreated && Counters.Length >= AbyssalCavitationConstants.CounterBlockCount)
            {
                SetCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, math.min(10, Shockwaves.Length));
                SetCounter(AbyssalCavitationCounterIndex.CandidateCount, math.min(32, Entities.Length));
                SetCounter(AbyssalCavitationCounterIndex.FaultFlags, (int)AbyssalCavitationTelemetryFlags.MockFallback);
            }

            if (index < 10 && Shockwaves.IsCreated && index < Shockwaves.Length)
            {
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(SafeSeed(SectorHash ^ FrameIndex ^ ((uint)index * 0x9E3779B9u)));
                float angle = rng.NextFloat(0f, 6.2831855f);
                math.sincos(angle, out float s, out float c);
                float ring = math.lerp(4f, 22f, rng.NextFloat());
                Shockwaves[index] = new ShockwaveEventDTO
                {
                    EpicenterAUP = OriginAUP + new double3(c * ring, rng.NextFloat(-12f, -4f), s * ring),
                    CurrentRadius = 0.05f,
                    MaxRadius = math.lerp(16f, 56f, q) + rng.NextFloat(0f, 9f),
                    PeakPressure = math.lerp(5500f, 26000f, q) * (0.75f + rng.NextFloat() * 0.5f),
                    ExpansionSpeed = math.lerp(120f, 340f, q),
                    SourceHashID = SectorHash ^ ((uint)index * 0x85EBCA6Bu)
                };
            }

            if (index < 32 && Entities.IsCreated && index < Entities.Length)
            {
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(SafeSeed(SectorHash ^ FrameIndex ^ 0xC2B2AE35u ^ ((uint)index * 0x27D4EB2Du)));
                float angle = rng.NextFloat(0f, 6.2831855f);
                math.sincos(angle, out float s, out float c);
                float ring = math.lerp(3f, 72f, rng.NextFloat());
                uint flags = AbyssalCavitationEntityFlags.Active | AbyssalCavitationEntityFlags.ForceReceiver;
                if ((index & 7) == 0)
                    flags |= AbyssalCavitationEntityFlags.Critical;

                Entities[index] = new ShockwaveEntitySnapshotDTO
                {
                    AUP = OriginAUP + new double3(c * ring, rng.NextFloat(-16f, 2f), s * ring),
                    Velocity = float3.zero,
                    EffectiveArea = math.lerp(0.35f, 3.5f, rng.NextFloat()),
                    InverseMass = math.lerp(0.1f, 1.0f, rng.NextFloat()),
                    RigidbodySlot = index,
                    EntityHash = SectorHash ^ (uint)(index + 1) * 2654435761u,
                    Flags = flags
                };
            }
        }

        private void SetCounter(int index, int value)
        {
            ShockwaveCounterBlock block = Counters[index];
            block.Value = value;
            Counters[index] = block;
        }

        private static uint SafeSeed(uint seed)
        {
            return seed != 0u ? seed : 1u;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PropagateShockwavesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Shockwaves.Length)
                return;

            ShockwaveEventDTO wave = Shockwaves[index];
            if (!IsActive(in wave))
                return;

            float dt = math.clamp(math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.02f, 0.0001f, 0.1f);
            wave.CurrentRadius += math.max(0f, wave.ExpansionSpeed) * dt;
            if (!math.isfinite(wave.CurrentRadius) || wave.CurrentRadius > wave.MaxRadius)
            {
                wave = default;
                wave.CurrentRadius = -1f;
            }

            Shockwaves[index] = wave;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CompactShockwavesJob : IJob
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;

        public void Execute()
        {
            if (!Shockwaves.IsCreated || !Counters.IsCreated || Counters.Length <= AbyssalCavitationCounterIndex.ActiveShockwaves)
                return;

            int count = math.clamp(Counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, Shockwaves.Length);
            int i = 0;
            while (i < count)
            {
                if (IsActive(in Shockwaves[i]))
                {
                    i++;
                    continue;
                }

                int last = count - 1;
                Shockwaves[i] = Shockwaves[last];
                ShockwaveEventDTO inactive = default;
                inactive.CurrentRadius = -1f;
                Shockwaves[last] = inactive;
                count--;
            }

            ShockwaveCounterBlock block = Counters[AbyssalCavitationCounterIndex.ActiveShockwaves];
            block.Value = count;
            Counters[AbyssalCavitationCounterIndex.ActiveShockwaves] = block;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateShockwavePressureJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [ReadOnly, NoAlias] public NativeArray<sbyte> SdfVoxels;
        public AbyssalCavitationTuningDTO Tuning;
        public AbyssalCavitationSdfVolumeDTO SdfVolume;
        public MockSDFSampler MockSdf;
        public double3 SdfReferenceAUP;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ForcePackets.Length)
                return;

            ForcePackets[index] = default;
            int candidateCount = ReadCounter(AbyssalCavitationCounterIndex.CandidateCount, Entities.Length);
            if ((uint)index >= (uint)candidateCount)
                return;

            ShockwaveEntitySnapshotDTO entity = Entities[index];
            if ((entity.Flags & AbyssalCavitationEntityFlags.Active) == 0u ||
                (entity.Flags & AbyssalCavitationEntityFlags.ForceReceiver) == 0u ||
                (entity.Flags & AbyssalCavitationEntityFlags.NonFinite) != 0u)
            {
                return;
            }

            float q = Smooth01(Tuning.GlobalQualityWeight);
            bool critical = (entity.Flags & AbyssalCavitationEntityFlags.Critical) != 0u;
            float acceptance = math.lerp(0.08f, 1.0f, q);
            if (!critical && Hash01(entity.EntityHash ^ FrameIndex * 747796405u) > acceptance)
                return;

            int waveCount = ReadCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, Shockwaves.Length);
            float3 accumulatedForce = float3.zero;
            double3 applicationAup = entity.AUP;
            float peakPressure = 0f;
            float minSdfDamp = 1f;
            uint flags = 0u;

            for (int i = 0; i < waveCount; i++)
            {
                ShockwaveEventDTO wave = Shockwaves[i];
                if (!IsActive(in wave))
                    continue;

                double3 deltaD = entity.AUP - wave.EpicenterAUP;
                float3 delta = LocalDeltaToFloat3(deltaD, ref flags);
                float distanceSq = math.max(math.lengthsq(delta), 0.0001f);
                float distance = math.sqrt(distanceSq);
                float shellWidth = math.max(
                    Tuning.CavitationShellMeters,
                    math.max(0.05f, wave.ExpansionSpeed * Tuning.SimulationTickDelta) * math.lerp(0.35f, 1.1f, q));
                float shell = math.saturate(1f - math.abs(distance - wave.CurrentRadius) * math.rcp(math.max(shellWidth, 0.0001f)));
                if (shell <= 0f)
                    continue;

                double3 midpoint = wave.EpicenterAUP + (double3)delta * 0.5;
                float sdfDistance = SampleSdfDistance(midpoint);
                float sdfDamp = ResolveSdfDampening(sdfDistance, Tuning.SdfSoftnessMeters, Tuning.SdfHardDampening);
                if (sdfDamp < 0.999f)
                    flags |= AbyssalCavitationPacketFlags.SdfDampened;

                minSdfDamp = math.min(minSdfDamp, sdfDamp);
                float inverseSquare = math.rcp(math.max(1f, distanceSq));
                float pressure = wave.PeakPressure * inverseSquare * shell * sdfDamp;
                peakPressure = math.max(peakPressure, pressure);
                if (pressure <= Tuning.MinPressure)
                    continue;

                float3 direction = distanceSq > 0.0001f ? delta * math.rsqrt(distanceSq) : new float3(0f, 1f, 0f);
                float area = math.clamp(math.isfinite(entity.EffectiveArea) ? entity.EffectiveArea : 1f, 0.05f, 64f);
                accumulatedForce += direction * (pressure * area * math.max(0.00001f, Tuning.ForceScale));
            }

            float forceSq = math.lengthsq(accumulatedForce);
            if (!math.isfinite(forceSq))
            {
                accumulatedForce = float3.zero;
                flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
            }

            if (forceSq <= 0.000001f || peakPressure <= Tuning.MinPressure)
                return;

            float maxForce = math.max(1f, Tuning.MaxForceNewton);
            if (forceSq > maxForce * maxForce)
            {
                accumulatedForce *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));
                flags |= AbyssalCavitationPacketFlags.ForceSaturated;
            }

            if (critical)
                flags |= AbyssalCavitationPacketFlags.CriticalTarget;

            ForcePackets[index] = new ShockwaveForcePacketDTO
            {
                ApplicationAUP = applicationAup,
                Force = accumulatedForce,
                Pressure = peakPressure,
                RigidbodySlot = entity.RigidbodySlot,
                TargetEntityHash = entity.EntityHash,
                SourceHashID = AbyssalCavitationConstants.SourceHash,
                FrameIndex = FrameIndex,
                Flags = flags | AbyssalCavitationPacketFlags.Active,
                SdfDampening = minSdfDamp
            };
        }

        private int ReadCounter(int index, int maxValue)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return 0;

            return math.clamp(Counters[index].Value, 0, maxValue);
        }

        private float SampleSdfDistance(double3 midpointAup)
        {
            if ((SdfVolume.Flags & AbyssalCavitationSdfFlags.Active) != 0u &&
                SdfVoxels.IsCreated)
                return SampleSdfVolume(midpointAup);

            float3 local = LocalDeltaToFloat3NoFlags(midpointAup - SdfReferenceAUP);
            return MockSdf.SampleDistance(local);
        }

        private float SampleSdfVolume(double3 midpointAup)
        {
            int3 dimensions = SdfVolume.Dimensions;
            if (!math.all(dimensions > 0))
                return 1f;

            int voxelCount = dimensions.x * dimensions.y * dimensions.z;
            if (voxelCount <= 0 || voxelCount > SdfVoxels.Length)
                return 1f;

            float3 cellSize = math.max(SdfVolume.CellSizeMeters, new float3(0.0001f));
            float3 local = LocalDeltaToFloat3NoFlags(midpointAup - SdfVolume.OriginAUP);
            float3 grid = local / cellSize;
            float3 maxGrid = new float3(dimensions - 1);
            if (math.any(grid < 0f) || math.any(grid > maxGrid))
                return 1f;

            int3 nearestCoord = (int3)math.floor(grid + 0.5f);
            nearestCoord = math.clamp(nearestCoord, int3.zero, dimensions - 1);
            float nearest = DecodeSdfByte(SdfVoxels[FlatIndex(nearestCoord, dimensions)]);
            float highTapWeight = math.step(0.3f, Smooth01(Tuning.GlobalQualityWeight));
            if (highTapWeight <= 0f)
                return nearest;

            int3 baseCoord = (int3)math.floor(grid);
            baseCoord = math.clamp(baseCoord, int3.zero, dimensions - 1);
            int3 nextCoord = math.min(baseCoord + 1, dimensions - 1);
            float3 t = math.saturate(grid - (float3)baseCoord);

            float c000 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, baseCoord.y, baseCoord.z), dimensions)]);
            float c100 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, baseCoord.y, baseCoord.z), dimensions)]);
            float c010 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, nextCoord.y, baseCoord.z), dimensions)]);
            float c110 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, nextCoord.y, baseCoord.z), dimensions)]);
            float c001 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, baseCoord.y, nextCoord.z), dimensions)]);
            float c101 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, baseCoord.y, nextCoord.z), dimensions)]);
            float c011 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, nextCoord.y, nextCoord.z), dimensions)]);
            float c111 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, nextCoord.y, nextCoord.z), dimensions)]);

            float c00 = math.lerp(c000, c100, t.x);
            float c10 = math.lerp(c010, c110, t.x);
            float c01 = math.lerp(c001, c101, t.x);
            float c11 = math.lerp(c011, c111, t.x);
            float c0 = math.lerp(c00, c10, t.y);
            float c1 = math.lerp(c01, c11, t.y);
            float trilinear = math.lerp(c0, c1, t.z);
            return math.lerp(nearest, trilinear, highTapWeight);
        }

        private float DecodeSdfByte(sbyte encoded)
        {
            float range = math.max(0.05f, math.isfinite(SdfVolume.DecodeRangeMeters) ? SdfVolume.DecodeRangeMeters : 32f);
            return math.clamp(encoded * (1f / 127f), -1f, 1f) * range;
        }

        private static int FlatIndex(int3 coord, int3 dimensions)
        {
            return coord.x + coord.y * dimensions.x + coord.z * dimensions.x * dimensions.y;
        }

        private static float ResolveSdfDampening(float sdfDistance, float softnessMeters, float hardDampening)
        {
            float softness = math.max(0.05f, math.isfinite(softnessMeters) ? softnessMeters : 4f);
            float open = SmoothRange(-softness, softness, math.isfinite(sdfDistance) ? sdfDistance : softness);
            return math.lerp(math.saturate(hardDampening), 1f, open);
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!math.all(math.isfinite(delta)))
            {
                flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
                return float3.zero;
            }

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            if (math.all(math.isfinite(local)))
                return local;

            flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
            return float3.zero;
        }

        private static float3 LocalDeltaToFloat3NoFlags(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }

        private static float Hash01(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }

        private static float SmoothRange(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(edge1 - edge0, 0.0001f)));
            return t * t * (3f - 2f * t);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildCavitationVisualsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [NoAlias] public NativeArray<CavitationVisualSphereDTO> Visuals;
        public double3 LocalOriginAUP;
        public AbyssalCavitationTuningDTO Tuning;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Visuals.Length)
                return;

            Visuals[index] = default;
            int active = Counters.IsCreated && Counters.Length > AbyssalCavitationCounterIndex.ActiveShockwaves
                ? math.clamp(Counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, Shockwaves.Length)
                : 0;
            if ((uint)index >= (uint)active)
                return;

            ShockwaveEventDTO wave = Shockwaves[index];
            if (wave.CurrentRadius < 0f || wave.MaxRadius <= 0f)
                return;

            uint flags = 0u;
            float3 center = LocalDeltaToFloat3(wave.EpicenterAUP - LocalOriginAUP, ref flags);
            float age01 = math.saturate(wave.CurrentRadius * math.rcp(math.max(wave.MaxRadius, 0.0001f)));
            float q = Smooth01(Tuning.GlobalQualityWeight);
            float intensity = math.saturate(wave.PeakPressure * 0.00008f) * math.max(0f, Tuning.VisualIntensityScale);
            float phase = Hash01(wave.SourceHashID ^ FrameIndex * 747796405u);
            Visuals[index] = new CavitationVisualSphereDTO
            {
                CenterRadius = new float4(center, wave.CurrentRadius),
                IntensityAgeQualityFlags = new float4(intensity, age01, q, flags),
                CurlPhase = new float4(math.sin(phase * 6.2831855f), math.cos(phase * 6.2831855f), phase, wave.MaxRadius),
                Reserved = new float4(wave.ExpansionSpeed, wave.PeakPressure, wave.SourceHashID & 0xFFFFu, 0f)
            };
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!math.all(math.isfinite(delta)))
            {
                flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
                return float3.zero;
            }

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static float Hash01(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct RecordShockwaveTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;
        public AbyssalCavitationTuningDTO Tuning;
        public uint FrameIndex;
        public float CpuMicroseconds;

        public void Execute()
        {
            if (!Counters.IsCreated || !Telemetry.IsCreated || Telemetry.Length == 0)
                return;

            int active = ReadCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, Shockwaves.IsCreated ? Shockwaves.Length : 0);
            int candidates = ReadCounter(AbyssalCavitationCounterIndex.CandidateCount, ForcePackets.IsCreated ? ForcePackets.Length : 0);
            int forcePackets = 0;
            float peakForce = 0f;
            float peakPressure = 0f;
            uint flags = (uint)math.max(0, ReadCounter(AbyssalCavitationCounterIndex.FaultFlags, int.MaxValue));
            uint hash = 2166136261u;
            double3 epicenter = double3.zero;
            float radius = 0f;

            for (int i = 0; i < active; i++)
            {
                ShockwaveEventDTO wave = Shockwaves[i];
                epicenter = wave.EpicenterAUP;
                radius = math.max(radius, wave.CurrentRadius);
                peakPressure = math.max(peakPressure, wave.PeakPressure);
                hash = Hash(hash, math.asuint(wave.CurrentRadius));
                hash = Hash(hash, math.asuint(wave.PeakPressure));
                if (!math.all(math.isfinite(wave.EpicenterAUP)) || !math.isfinite(wave.CurrentRadius))
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
            }

            for (int i = 0; i < candidates; i++)
            {
                ShockwaveForcePacketDTO packet = ForcePackets[i];
                if ((packet.Flags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;

                forcePackets++;
                float forceSq = math.lengthsq(packet.Force);
                if (!math.isfinite(forceSq))
                {
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
                    continue;
                }

                peakForce = math.max(peakForce, math.sqrt(math.max(forceSq, 0f)));
                if ((packet.Flags & AbyssalCavitationPacketFlags.SdfDampened) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.SdfDampened;
                if ((packet.Flags & AbyssalCavitationPacketFlags.ForceSaturated) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.ForceSaturated;
                if ((packet.Flags & AbyssalCavitationPacketFlags.NonFiniteRecovered) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
            }

            WriteCounter(AbyssalCavitationCounterIndex.ForcePacketCount, forcePackets);
            WriteCounter(AbyssalCavitationCounterIndex.VisualCount, active);
            WriteCounter(AbyssalCavitationCounterIndex.LastFrame, unchecked((int)FrameIndex));

            int head = ReadCounter(AbyssalCavitationCounterIndex.TelemetryHead, Telemetry.Length);
            Telemetry[head] = new ShockwaveTelemetryEntry
            {
                EpicenterAUP = epicenter,
                CurrentRadius = radius,
                PeakPressure = peakPressure,
                PeakForce = peakForce,
                GlobalQualityWeight = math.saturate(Tuning.GlobalQualityWeight),
                FrameIndex = FrameIndex,
                StateHash = hash,
                ActiveShockwaves = active,
                CandidateCount = candidates,
                CpuMicroseconds = math.isfinite(CpuMicroseconds) ? math.max(0f, CpuMicroseconds) : 0f,
                Flags = flags
            };

            head++;
            if (head >= Telemetry.Length)
                head = 0;
            WriteCounter(AbyssalCavitationCounterIndex.TelemetryHead, head);
            WriteCounter(AbyssalCavitationCounterIndex.FaultFlags, (int)flags);
        }

        private int ReadCounter(int index, int maxValue)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return 0;

            return math.clamp(Counters[index].Value, 0, maxValue);
        }

        private void WriteCounter(int index, int value)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return;

            ShockwaveCounterBlock block = Counters[index];
            block.Value = value;
            Counters[index] = block;
        }

        private static uint Hash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    public sealed partial class PhysicsApplySystem
    {
        internal static void DrainCavitationForcePackets(
            NativeArray<ShockwaveForcePacketDTO> packets,
            NativeArray<ShockwaveCounterBlock> counters,
            AbyssalCavitationTuningDTO tuning,
            double3 localOriginAUP,
            uint frameIndex,
            int maxPackets,
            out int accepted,
            out int unresolved)
        {
            accepted = 0;
            unresolved = 0;
            if (!packets.IsCreated ||
                packets.Length <= 0 ||
                !counters.IsCreated ||
                counters.Length <= AbyssalCavitationCounterIndex.CandidateCount)
            {
                return;
            }

            PhysicsApplySystem system = EnsureRuntimeInstance();
            int candidateCount = math.clamp(counters[AbyssalCavitationCounterIndex.CandidateCount].Value, 0, packets.Length);
            int budget = math.min(candidateCount, math.clamp(maxPackets, 0, packets.Length));
            float maxForce = math.max(1f, AbyssalCavitationSanitizer.SanitizeTuning(tuning).MaxForceNewton);
            for (int i = 0; i < budget; i++)
            {
                ShockwaveForcePacketDTO packet = packets[i];
                if ((packet.Flags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;
                if (frameIndex != 0u && packet.FrameIndex != frameIndex)
                    continue;
                if (system == null ||
                    packet.TargetEntityHash == 0u ||
                    !GlobalPhysicsStateManager.TryResolveTrackedBodyByFoldedEntityHash(packet.TargetEntityHash, out Rigidbody body))
                {
                    unresolved++;
                    continue;
                }

                float3 force = packet.Force;
                float forceSq = math.lengthsq(force);
                if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                {
                    unresolved++;
                    continue;
                }

                if (forceSq > maxForce * maxForce)
                    force *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));

                float3 point = LocalAupToFloat3(packet.ApplicationAUP - localOriginAUP);
                if (!math.all(math.isfinite(point)))
                {
                    unresolved++;
                    continue;
                }

                if (system.QueueForceAtPosition(
                        body,
                        new Vector3(force.x, force.y, force.z),
                        new Vector3(point.x, point.y, point.z),
                        ForceMode.Impulse,
                        ForcePacketPriority.Critical,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
                else
                {
                    unresolved++;
                }
            }
        }

        private static float3 LocalAupToFloat3(double3 delta)
        {
            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            return new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
        }
    }
}
