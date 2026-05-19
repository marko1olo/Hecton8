using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// SHINOBU_116 data-only macro ecosystem solver. It owns no GameObjects and never calls Unity physics.
    /// </summary>
    public unsafe sealed class MacroEcosystemMathematicianRuntime : IFrostTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int GridWidth = 100;
        private const int GridHeight = 100;
        private const int SectorCapacity = GridWidth * GridHeight;
        private const int IndexCapacity = 32768;
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 16;
        private const int BiomeSpecCapacity = 256;
        private const int CsvScratchBytes = 32768;
        private const int JobBatchSize = 64;
        private const float SectorSizeMeters = 1000f;
        private const float FrostDeltaSeconds = 5f;
        private const string CsvFileName = "biome_ecosystem_specs.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_MACRO_ECOSYSTEM.bin";
        private const ulong DumpMagic = 0x4D4143524F45434FUL; // MACROECO
        private const uint RouteHash = 0x53483136u; // SH16

        private static MacroEcosystemMathematicianRuntime s_runtime;

        private VaultBufferHandle<EcosystemSectorDTO> _frontHandle;
        private VaultBufferHandle<EcosystemSectorDTO> _backHandle;
        private VaultBufferHandle<EcosystemSectorRemainderDTO> _remainderHandle;
        private VaultBufferHandle<EcosystemSectorCoordDTO> _coordHandle;
        private VaultBufferHandle<EcosystemSectorIndexEntryDTO> _indexEntryHandle;
        private VaultBufferHandle<BiomeEcosystemSpecDTO> _biomeSpecHandle;
        private VaultBufferHandle<MacroEcosystemTuningDTO> _tuningHandle;
        private VaultBufferHandle<MacroEcosystemCounterDTO> _counterHandle;
        private VaultBufferHandle<MacroEcosystemTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<uint> _faultFlagHandle;

        private IDataVault _vault;
        private JobHandle _activeJobHandle;
        private long _scheduleTicks;
        private long _csvTimestampTicks;
        private uint _simulationTick;
        private int _telemetryCursor;
        private int _lastTelemetrySlot;
        private int _lastDiffusionSteps;
        private bool _initialized;
        private bool _registeredFrost;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _dumpedFault;

        private MacroEcosystemMathematicianRuntime()
        {
        }

        /// <summary>
        /// Ensures the headless runtime exists and is registered to FrostTick.
        /// </summary>
        public static MacroEcosystemMathematicianRuntime EnsureRuntime()
        {
            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null)
            {
                runtime = new MacroEcosystemMathematicianRuntime();
                s_runtime = runtime;
            }

            runtime.Activate();
            return runtime;
        }

        /// <summary>
        /// Resolves local macro biomass for consumers that hydrate visual boids near the player.
        /// </summary>
        public static bool TryGetBiomassAvailability(float3 runtimePosition, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;

            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null || !runtime._initialized || !math.all(math.isfinite(runtimePosition)))
                return false;

            long sectorX = (long)math.floor((double)runtimePosition.x / SectorSizeMeters);
            long sectorZ = (long)math.floor((double)runtimePosition.z / SectorSizeMeters);
            return runtime.TryGetSectorBiomass(sectorX, 0L, sectorZ, out preyBiomass01, out predatorBiomass01, out carryingCapacity01);
        }

        /// <summary>
        /// Resolves predator and rare-resource spawn weights from temperature, toxicity, and biomass.
        /// </summary>
        public static bool TryGetSectorSpawnWeights(float3 runtimePosition, out float predatorWeight01, out float rareResourceWeight01)
        {
            predatorWeight01 = 0f;
            rareResourceWeight01 = 0f;

            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null || !runtime._initialized || !math.all(math.isfinite(runtimePosition)))
                return false;

            long sectorX = (long)math.floor((double)runtimePosition.x / SectorSizeMeters);
            long sectorZ = (long)math.floor((double)runtimePosition.z / SectorSizeMeters);
            return runtime.TryGetSectorSpawnWeights(sectorX, 0L, sectorZ, out predatorWeight01, out rareResourceWeight01);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystem()
        {
            if (s_runtime != null)
                s_runtime.Dispose();

            s_runtime = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureRuntime();
        }

        /// <inheritdoc />
        public void FrostTick()
        {
            if (_jobScheduled || !EnsureVaultState())
                return;

#if UNITY_EDITOR
            TryLoadBiomeSpecsCsv();
#endif
            IDataVault vault = _vault;
            if (vault == null || !TryLockJobBuffers(vault))
                return;

            NativeArray<EcosystemSectorDTO> front = _frontHandle.Resolve(vault);
            NativeArray<EcosystemSectorDTO> back = _backHandle.Resolve(vault);
            NativeArray<EcosystemSectorRemainderDTO> remainders = _remainderHandle.Resolve(vault);
            NativeArray<EcosystemSectorCoordDTO> coords = _coordHandle.Resolve(vault);
            NativeArray<BiomeEcosystemSpecDTO> biomeSpecs = _biomeSpecHandle.Resolve(vault);
            NativeArray<MacroEcosystemTuningDTO> tuningArray = _tuningHandle.Resolve(vault);
            NativeArray<MacroEcosystemCounterDTO> counters = _counterHandle.Resolve(vault);
            NativeArray<MacroEcosystemTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<uint> faultFlags = _faultFlagHandle.Resolve(vault);
            if (!front.IsCreated ||
                !back.IsCreated ||
                !remainders.IsCreated ||
                !coords.IsCreated ||
                !biomeSpecs.IsCreated ||
                biomeSpecs.Length <= 0 ||
                !tuningArray.IsCreated ||
                tuningArray.Length <= 0 ||
                !counters.IsCreated ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                !faultFlags.IsCreated)
            {
                UnlockJobBuffers();
                return;
            }
            int sectorCount = math.min(SectorCapacity, front.Length);
            sectorCount = math.min(sectorCount, back.Length);
            sectorCount = math.min(sectorCount, remainders.Length);
            sectorCount = math.min(sectorCount, coords.Length);
            sectorCount = math.min(sectorCount, faultFlags.Length);
            if (sectorCount <= 0)
            {
                UnlockJobBuffers();
                return;
            }

            MacroEcosystemTuningDTO tuning = MacroEcosystemTuningDTO.Sanitize(tuningArray[0]);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.FrostDeltaSeconds = FrostDeltaSeconds;
            tuning.Flags |= MacroEcosystemMath.TuningFlagSnapshotWriteInFlight;
            tuning.StateHash = MacroEcosystemMath.Mix32(tuning.StateHash, math.asuint(tuning.GlobalQualityWeight));
            tuningArray[0] = tuning;

            int diffusionSteps = MacroEcosystemMath.ResolveDiffusionSteps(tuning.GlobalQualityWeight);
            _lastDiffusionSteps = diffusionSteps;
            int telemetrySlot = _telemetryCursor % telemetry.Length;
            _lastTelemetrySlot = telemetrySlot;

            EcosystemSectorDTO* frontPtr = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front);
            EcosystemSectorDTO* backPtr = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
            EcosystemSectorRemainderDTO* remainderPtr = (EcosystemSectorRemainderDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(remainders);
            EcosystemSectorCoordDTO* coordPtr = (EcosystemSectorCoordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(coords);
            BiomeEcosystemSpecDTO* biomeSpecPtr = (BiomeEcosystemSpecDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(biomeSpecs);
            uint* faultFlagPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags);

            var populationJob = new EcosystemPopulationJob
            {
                Front = frontPtr,
                Back = backPtr,
                Remainders = remainderPtr,
                Coords = coordPtr,
                BiomeSpecs = biomeSpecPtr,
                FaultFlags = faultFlagPtr,
                Tuning = tuning,
                SectorCount = sectorCount,
                BiomeSpecCapacity = biomeSpecs.Length
            };

            JobHandle handle = populationJob.Schedule(populationJob.SectorCount, JobBatchSize);
            EcosystemSectorDTO* source = backPtr;
            EcosystemSectorDTO* destination = frontPtr;
            for (int step = 0; step < diffusionSteps; step++)
            {
                var diffusionJob = new BiomassDiffusionJob
                {
                    Source = source,
                    Destination = destination,
                    Remainders = remainderPtr,
                    Coords = coordPtr,
                    BiomeSpecs = biomeSpecPtr,
                    SectorCount = populationJob.SectorCount,
                    BiomeSpecCapacity = biomeSpecs.Length,
                    Width = GridWidth,
                    Height = GridHeight,
                    SectorSizeMeters = SectorSizeMeters,
                    MigrationRate = tuning.MigrationRate,
                    CarryingCapacityPrey = tuning.CarryingCapacityPrey,
                    CarryingCapacityPredator = tuning.CarryingCapacityPredator,
                    TemperatureOptimum = tuning.TemperatureOptimum,
                    TemperatureHalfRange = tuning.TemperatureHalfRange
                };
                handle = diffusionJob.Schedule(populationJob.SectorCount, JobBatchSize, handle);
                EcosystemSectorDTO* swap = source;
                source = destination;
                destination = swap;
            }

            if (source != frontPtr)
            {
                var copyJob = new CopySectorBufferJob
                {
                    Source = source,
                    Destination = frontPtr,
                    SectorCount = populationJob.SectorCount
                };
                handle = copyJob.Schedule(populationJob.SectorCount, JobBatchSize, handle);
            }

            var telemetryJob = new EcosystemTelemetryReductionJob
            {
                Sectors = frontPtr,
                Telemetry = telemetry,
                Counters = counters,
                FaultFlags = faultFlagPtr,
                SectorCount = populationJob.SectorCount,
                TelemetryIndex = telemetrySlot,
                FrameIndex = _simulationTick++,
                DiffusionSteps = unchecked((uint)diffusionSteps),
                GlobalQualityWeight = tuning.GlobalQualityWeight
            };
            _activeJobHandle = telemetryJob.Schedule(handle);
            _scheduleTicks = Stopwatch.GetTimestamp();
            _jobScheduled = true;
            _telemetryCursor++;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteScheduledJob(false);
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteScheduledJob(true);
            UnlockJobBuffers();
            _vault = currentService as IDataVault;
            ResetVaultHandles();
            _initialized = false;
            _dumpedFault = false;
            if (_vault != null)
                EnsureVaultState();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CompleteScheduledJob(true);
            UnlockJobBuffers();
            TryUnregister();
            ResetVaultHandles();
            _vault = null;
            _initialized = false;
        }

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            TryRegister();
            EnsureVaultState();
        }

        private bool EnsureVaultState()
        {
            MacroEcosystemLayoutManifest.VerifyColdBoot();

            IDataVault vault = _vault;
            if (vault == null)
            {
                vault = GlobalRegistry.DataVault;
                _vault = vault;
            }

            if (vault == null)
                return false;

            _frontHandle = vault.GetBufferHandle<EcosystemSectorDTO>(BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _backHandle = vault.GetBufferHandle<EcosystemSectorDTO>(BufferID.ShinobuMacroEcosystemSectorBack, SectorCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _remainderHandle = vault.GetBufferHandle<EcosystemSectorRemainderDTO>(BufferID.ShinobuMacroEcosystemRemainders, SectorCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _coordHandle = vault.GetBufferHandle<EcosystemSectorCoordDTO>(BufferID.ShinobuMacroEcosystemSectorCoords, SectorCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _indexEntryHandle = vault.GetBufferHandle<EcosystemSectorIndexEntryDTO>(BufferID.ShinobuMacroEcosystemIndexEntries, IndexCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _biomeSpecHandle = vault.GetBufferHandle<BiomeEcosystemSpecDTO>(BufferID.ShinobuMacroEcosystemBiomeSpecs, BiomeSpecCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<MacroEcosystemTuningDTO>(BufferID.ShinobuMacroEcosystemTuning, 1, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _counterHandle = vault.GetBufferHandle<MacroEcosystemCounterDTO>(BufferID.ShinobuMacroEcosystemCounters, CounterCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<MacroEcosystemTelemetryEntry>(BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuMacroEcosystemCsvScratch, CsvScratchBytes, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);
            _faultFlagHandle = vault.GetBufferHandle<uint>(BufferID.ShinobuMacroEcosystemFaultFlags, SectorCapacity, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory);

            if (!_frontHandle.IsCreated ||
                !_backHandle.IsCreated ||
                !_remainderHandle.IsCreated ||
                !_coordHandle.IsCreated ||
                !_indexEntryHandle.IsCreated ||
                !_biomeSpecHandle.IsCreated ||
                !_tuningHandle.IsCreated ||
                !_counterHandle.IsCreated ||
                !_telemetryHandle.IsCreated ||
                !_csvScratchHandle.IsCreated ||
                !_faultFlagHandle.IsCreated)
            {
                return false;
            }

            if (!_initialized)
                GenerateEmergencyMockEcosystem(vault);

            return _initialized;
        }

        private void GenerateEmergencyMockEcosystem(IDataVault vault)
        {
            NativeArray<EcosystemSectorDTO> front = _frontHandle.Resolve(vault);
            NativeArray<EcosystemSectorDTO> back = _backHandle.Resolve(vault);
            NativeArray<EcosystemSectorRemainderDTO> remainders = _remainderHandle.Resolve(vault);
            NativeArray<EcosystemSectorCoordDTO> coords = _coordHandle.Resolve(vault);
            NativeArray<EcosystemSectorIndexEntryDTO> indexEntries = _indexEntryHandle.Resolve(vault);
            NativeArray<BiomeEcosystemSpecDTO> biomeSpecs = _biomeSpecHandle.Resolve(vault);
            NativeArray<MacroEcosystemTuningDTO> tuning = _tuningHandle.Resolve(vault);
            NativeArray<MacroEcosystemCounterDTO> counters = _counterHandle.Resolve(vault);
            NativeArray<MacroEcosystemTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<uint> faultFlags = _faultFlagHandle.Resolve(vault);
            if (!front.IsCreated ||
                front.Length < SectorCapacity ||
                !back.IsCreated ||
                back.Length < SectorCapacity ||
                !remainders.IsCreated ||
                remainders.Length < SectorCapacity ||
                !coords.IsCreated ||
                coords.Length < SectorCapacity ||
                !indexEntries.IsCreated ||
                indexEntries.Length < IndexCapacity ||
                !biomeSpecs.IsCreated ||
                biomeSpecs.Length <= 0 ||
                !tuning.IsCreated ||
                tuning.Length <= 0 ||
                !counters.IsCreated ||
                !telemetry.IsCreated ||
                !faultFlags.IsCreated ||
                faultFlags.Length < SectorCapacity)
            {
                return;
            }

            MacroEcosystemTuningDTO defaultTuning = MacroEcosystemTuningDTO.CreateDefault();
            defaultTuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning[0] = defaultTuning;

            var clearJob = new ClearMacroEcosystemVaultTablesJob
            {
                IndexEntries = indexEntries,
                BiomeSpecs = biomeSpecs,
                Counters = counters,
                Telemetry = telemetry,
                FaultFlags = faultFlags
            };

            var mockJob = new GenerateEmergencyMockEcosystemJob
            {
                Front = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                Back = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back),
                Remainders = (EcosystemSectorRemainderDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(remainders),
                Coords = (EcosystemSectorCoordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(coords),
                FaultFlags = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags),
                Width = GridWidth,
                Height = GridHeight,
                OriginX = -(GridWidth / 2),
                OriginZ = -(GridHeight / 2)
            };

            var indexJob = new BuildSectorIndexJob
            {
                Sectors = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                IndexEntries = (EcosystemSectorIndexEntryDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(indexEntries),
                SectorCount = SectorCapacity,
                IndexCapacity = IndexCapacity
            };

            // COLD SYNC JOB: first boot writes every uninitialized Vault sector before any reader can observe it.
            JobHandle clearHandle = clearJob.Schedule();
            JobHandle mockHandle = mockJob.Schedule(SectorCapacity, JobBatchSize, clearHandle);
            indexJob.Schedule(mockHandle).Complete();
            _initialized = true;
        }

        private bool TryGetSectorBiomass(long sectorX, long sectorY, long sectorZ, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;
            IDataVault vault = _vault;
            if (vault == null)
                return false;
            if (_jobScheduled)
                return false;

            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            if (!TryResolveSectorIndex(vault, hash, out int index))
                return false;

            NativeArray<EcosystemSectorDTO> front = _frontHandle.Resolve(vault);
            NativeArray<MacroEcosystemTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (!front.IsCreated || (uint)index >= (uint)front.Length || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            EcosystemSectorDTO sector = front[index];
            float preyCapacity = math.max(1f, tuning[0].CarryingCapacityPrey);
            float predatorCapacity = math.max(1f, tuning[0].CarryingCapacityPredator);
            preyBiomass01 = math.saturate(sector.PreyBiomass * math.rcp(preyCapacity));
            predatorBiomass01 = math.saturate(sector.PredatorBiomass * math.rcp(predatorCapacity));
            MacroEcosystemTuningDTO fallback = MacroEcosystemTuningDTO.CreateDefault();
            carryingCapacity01 = math.saturate((preyCapacity + predatorCapacity) * math.rcp(fallback.CarryingCapacityPrey + fallback.CarryingCapacityPredator));
            return true;
        }

        private bool TryResolveSectorIndex(IDataVault vault, ulong sectorHash, out int sectorIndex)
        {
            sectorIndex = -1;
            NativeArray<EcosystemSectorIndexEntryDTO> entries = _indexEntryHandle.Resolve(vault);
            if (!entries.IsCreated || entries.Length <= 0 || sectorHash == 0UL)
                return false;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(sectorHash, entries.Length);
            for (int probe = 0; probe < entries.Length; probe++)
            {
                EcosystemSectorIndexEntryDTO entry = entries[slot];
                if (entry.Occupied == 0u)
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    sectorIndex = entry.Slot;
                    return sectorIndex >= 0 && sectorIndex < SectorCapacity;
                }

                slot++;
                if (slot == entries.Length)
                    slot = 0;
            }

            return false;
        }

        private bool TryGetSectorSpawnWeights(long sectorX, long sectorY, long sectorZ, out float predatorWeight01, out float rareResourceWeight01)
        {
            predatorWeight01 = 0f;
            rareResourceWeight01 = 0f;
            IDataVault vault = _vault;
            if (vault == null)
                return false;
            if (_jobScheduled)
                return false;

            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            if (!TryResolveSectorIndex(vault, hash, out int index))
                return false;

            NativeArray<EcosystemSectorDTO> front = _frontHandle.Resolve(vault);
            NativeArray<MacroEcosystemTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (!front.IsCreated || (uint)index >= (uint)front.Length || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            EcosystemSectorDTO sector = front[index];
            MacroEcosystemTuningDTO t = MacroEcosystemTuningDTO.Sanitize(tuning[0]);
            float predatorMass = sector.PredatorBiomass * math.rcp(math.max(1f, t.CarryingCapacityPredator));
            float tempSuitability = MacroEcosystemMath.ResolveTemperatureSuitability(sector.LocalTemperature, t.TemperatureOptimum, t.TemperatureHalfRange);
            float toxin = math.saturate(sector.ToxinLevel);
            predatorWeight01 = math.saturate(predatorMass * math.lerp(1f, 0.2f, toxin));
            rareResourceWeight01 = math.saturate((1f - tempSuitability) * 0.55f + toxin * 0.45f);
            return true;
        }

        private void TryRegister()
        {
            if (!_registeredFrost)
                _registeredFrost = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredFrost)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrost = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            int locked = 0;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemSectorFront, SystemID.AIEcology)) return false;
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemSectorBack, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemRemainders, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemSectorCoords, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemBiomeSpecs, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemTuning, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemCounters, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemFaultFlags, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMacroEcosystemTelemetryRing, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            _jobLocksHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _vault;
            if (vault != null)
                UnlockLockedJobBuffers(vault, 9);
            _jobLocksHeld = false;
        }

        private static void UnlockLockedJobBuffers(IDataVault vault, int locked)
        {
            if (locked >= 9) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemTelemetryRing, SystemID.AIEcology);
            if (locked >= 8) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemFaultFlags, SystemID.AIEcology);
            if (locked >= 7) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemCounters, SystemID.AIEcology);
            if (locked >= 6) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemTuning, SystemID.AIEcology);
            if (locked >= 5) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemBiomeSpecs, SystemID.AIEcology);
            if (locked >= 4) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemSectorCoords, SystemID.AIEcology);
            if (locked >= 3) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemRemainders, SystemID.AIEcology);
            if (locked >= 2) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemSectorBack, SystemID.AIEcology);
            if (locked >= 1) vault.TryUnlockBuffer(BufferID.ShinobuMacroEcosystemSectorFront, SystemID.AIEcology);
        }

        private void CompleteScheduledJob(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!forceComplete && !_activeJobHandle.IsCompleted)
                return;

            _activeJobHandle.Complete();
            _jobScheduled = false;
            long now = Stopwatch.GetTimestamp();
            float micros = Stopwatch.Frequency > 0
                ? (float)((now - _scheduleTicks) * 1000000.0 / Stopwatch.Frequency)
                : 0f;

            IDataVault vault = _vault;
            if (vault != null)
            {
                ClearSnapshotWriteInFlight(vault);
                PatchCompletedTelemetry(vault, micros);
            }

            UnlockJobBuffers();
        }

        private void ClearSnapshotWriteInFlight(IDataVault vault)
        {
            NativeArray<MacroEcosystemTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            MacroEcosystemTuningDTO value = tuning[0];
            value.Flags &= ~MacroEcosystemMath.TuningFlagSnapshotWriteInFlight;
            tuning[0] = value;
        }

        private void PatchCompletedTelemetry(IDataVault vault, float solverMicros)
        {
            NativeArray<MacroEcosystemTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!telemetry.IsCreated || (uint)_lastTelemetrySlot >= (uint)telemetry.Length)
                return;

            MacroEcosystemTelemetryEntry entry = telemetry[_lastTelemetrySlot];
            entry.SolverMicroseconds = math.max(0f, solverMicros);
            entry.DiffusionSteps = unchecked((uint)_lastDiffusionSteps);
            telemetry[_lastTelemetrySlot] = entry;

            bool fault = (entry.Flags & MacroEcosystemTelemetryEntry.FlagInvalidMath) != 0u;
            if (fault && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpTelemetry(vault);
            }
        }

        private unsafe void DumpTelemetry(IDataVault vault)
        {
            NativeArray<MacroEcosystemTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            try
            {
                string projectRoot = Application.dataPath;
                DirectoryInfo directory = Directory.GetParent(projectRoot);
                if (directory != null)
                    projectRoot = directory.FullName;
                string path = Path.Combine(projectRoot, DumpRelativePath);
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[24];
                    WriteUInt64(header.Slice(0, 8), DumpMagic);
                    WriteUInt32(header.Slice(8, 4), unchecked((uint)TelemetryCapacity));
                    WriteUInt32(header.Slice(12, 4), unchecked((uint)UnsafeUtility.SizeOf<MacroEcosystemTelemetryEntry>()));
                    WriteUInt32(header.Slice(16, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32(header.Slice(20, 4), RouteHash);
                    stream.Write(header);

                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int bytes = telemetry.Length * UnsafeUtility.SizeOf<MacroEcosystemTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
        }

#if UNITY_EDITOR
        private unsafe void TryLoadBiomeSpecsCsv()
        {
            IDataVault vault = _vault;
            if (vault == null || !_csvScratchHandle.IsCreated || !_biomeSpecHandle.IsCreated)
                return;

            string path = ResolveCsvPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Ticks == _csvTimestampTicks)
                return;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(vault);
            NativeArray<BiomeEcosystemSpecDTO> specs = _biomeSpecHandle.Resolve(vault);
            NativeArray<MacroEcosystemCounterDTO> counters = _counterHandle.Resolve(vault);
            if (!scratch.IsCreated || scratch.Length <= 0 || !specs.IsCreated)
                return;

            try
            {
                int bytesRead;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int maxBytes = math.min(scratch.Length, CsvScratchBytes);
                    bytesRead = stream.Read(new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(scratch), maxBytes));
                }

                if (bytesRead <= 0)
                    return;

                int parsed = MacroEcosystemCsvParser.ParseBiomeSpecs(
                    new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch), bytesRead),
                    specs);

                if (counters.IsCreated && counters.Length > 6)
                    counters[6] = MacroEcosystemCounterDTO.FromValue(parsed);
                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
        }

        private static string ResolveCsvPath()
        {
            string dataPath = Application.dataPath;
            string first = Path.Combine(dataPath, "_Project", "Data", CsvFileName);
            if (File.Exists(first))
                return first;

            DirectoryInfo root = Directory.GetParent(dataPath);
            if (root == null)
                return first;

            return Path.Combine(root.FullName, "Data", CsvFileName);
        }
#endif

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private static void ResetVaultHandles()
        {
            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null)
                return;

            runtime._frontHandle = default;
            runtime._backHandle = default;
            runtime._remainderHandle = default;
            runtime._coordHandle = default;
            runtime._indexEntryHandle = default;
            runtime._biomeSpecHandle = default;
            runtime._tuningHandle = default;
            runtime._counterHandle = default;
            runtime._telemetryHandle = default;
            runtime._csvScratchHandle = default;
            runtime._faultFlagHandle = default;
        }

        private static void WriteUInt32(Span<byte> target, uint value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(Span<byte> target, ulong value)
        {
            WriteUInt32(target, unchecked((uint)value));
            WriteUInt32(target.Slice(4, 4), unchecked((uint)(value >> 32)));
        }
    }

    /// <summary>
    /// Exact ARM64 sector truth record. Keep this at 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EcosystemSectorDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint PreyBiomass;
        [FieldOffset(12)] public uint PredatorBiomass;
        [FieldOffset(16)] public float LocalTemperature;
        [FieldOffset(20)] public float ToxinLevel;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EcosystemSectorCoordDTO
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public uint BiomeHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EcosystemSectorRemainderDTO
    {
        [FieldOffset(0)] public float PreyFraction;
        [FieldOffset(4)] public float PredatorFraction;
        [FieldOffset(8)] public float DiffusionPreyFraction;
        [FieldOffset(12)] public float DiffusionPredatorFraction;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EcosystemSectorIndexEntryDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public int Slot;
        [FieldOffset(12)] public uint Occupied;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct BiomeEcosystemSpecDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public float CarryingCapacityPrey;
        [FieldOffset(8)] public float CarryingCapacityPredator;
        [FieldOffset(12)] public float MigrationResistance;
        [FieldOffset(16)] public float TemperatureOptimum;
        [FieldOffset(20)] public float ToxinPenalty;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemTuningDTO
    {
        [FieldOffset(0)] public float BaseBirthRate;
        [FieldOffset(4)] public float PredationRate;
        [FieldOffset(8)] public float PredatorConversionRate;
        [FieldOffset(12)] public float PredatorStarvationRate;
        [FieldOffset(16)] public float CarryingCapacityPrey;
        [FieldOffset(20)] public float CarryingCapacityPredator;
        [FieldOffset(24)] public float MigrationRate;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float FrostDeltaSeconds;
        [FieldOffset(36)] public float TemperatureOptimum;
        [FieldOffset(40)] public float TemperatureHalfRange;
        [FieldOffset(44)] public float ToxicityBirthSuppression;
        [FieldOffset(48)] public float ToxicityDeathBoost;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Reserved;

        public static MacroEcosystemTuningDTO CreateDefault()
        {
            return new MacroEcosystemTuningDTO
            {
                BaseBirthRate = 0.034f,
                PredationRate = 0.0000015f,
                PredatorConversionRate = 0.00000055f,
                PredatorStarvationRate = 0.020f,
                CarryingCapacityPrey = 60000f,
                CarryingCapacityPredator = 12000f,
                MigrationRate = 0.045f,
                GlobalQualityWeight = 1f,
                FrostDeltaSeconds = 5f,
                TemperatureOptimum = 8f,
                TemperatureHalfRange = 18f,
                ToxicityBirthSuppression = 0.92f,
                ToxicityDeathBoost = 2.4f,
                Flags = 1u,
                StateHash = 0x4D45434Fu,
                Reserved = 0u
            };
        }

        public static MacroEcosystemTuningDTO Sanitize(MacroEcosystemTuningDTO value)
        {
            MacroEcosystemTuningDTO fallback = CreateDefault();
            value.BaseBirthRate = PositiveOr(value.BaseBirthRate, fallback.BaseBirthRate);
            value.PredationRate = PositiveOr(value.PredationRate, fallback.PredationRate);
            value.PredatorConversionRate = PositiveOr(value.PredatorConversionRate, fallback.PredatorConversionRate);
            value.PredatorStarvationRate = PositiveOr(value.PredatorStarvationRate, fallback.PredatorStarvationRate);
            value.CarryingCapacityPrey = PositiveOr(value.CarryingCapacityPrey, fallback.CarryingCapacityPrey);
            value.CarryingCapacityPredator = PositiveOr(value.CarryingCapacityPredator, fallback.CarryingCapacityPredator);
            value.MigrationRate = math.clamp(PositiveOr(value.MigrationRate, fallback.MigrationRate), 0.001f, 0.25f);
            value.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, value.GlobalQualityWeight, math.isfinite(value.GlobalQualityWeight)));
            value.FrostDeltaSeconds = PositiveOr(value.FrostDeltaSeconds, fallback.FrostDeltaSeconds);
            value.TemperatureOptimum = math.select(fallback.TemperatureOptimum, value.TemperatureOptimum, math.isfinite(value.TemperatureOptimum));
            value.TemperatureHalfRange = PositiveOr(value.TemperatureHalfRange, fallback.TemperatureHalfRange);
            value.ToxicityBirthSuppression = math.saturate(math.select(fallback.ToxicityBirthSuppression, value.ToxicityBirthSuppression, math.isfinite(value.ToxicityBirthSuppression)));
            value.ToxicityDeathBoost = PositiveOr(value.ToxicityDeathBoost, fallback.ToxicityDeathBoost);
            return value;
        }

        private static float PositiveOr(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemTelemetryEntry
    {
        public const uint FlagInvalidMath = 1u << 0;
        public const uint FlagPopulationExplosion = 1u << 1;

        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public ulong TotalPreyBiomass;
        [FieldOffset(16)] public ulong TotalPredatorBiomass;
        [FieldOffset(24)] public uint SterileSectorCount;
        [FieldOffset(28)] public uint ToxicSectorCount;
        [FieldOffset(32)] public float SolverMicroseconds;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint DiffusionSteps;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong TotalMass;
        [FieldOffset(56)] public ulong Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemCounterDTO
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong Reserved0;
        [FieldOffset(16)] public ulong Reserved1;
        [FieldOffset(24)] public ulong Reserved2;
        [FieldOffset(32)] public ulong Reserved3;
        [FieldOffset(40)] public ulong Reserved4;
        [FieldOffset(48)] public ulong Reserved5;
        [FieldOffset(56)] public ulong Reserved6;

        public static MacroEcosystemCounterDTO FromValue(int value)
        {
            return new MacroEcosystemCounterDTO
            {
                Value = value,
                Flags = 0u,
                Reserved0 = 0UL,
                Reserved1 = 0UL,
                Reserved2 = 0UL,
                Reserved3 = 0UL,
                Reserved4 = 0UL,
                Reserved5 = 0UL,
                Reserved6 = 0UL
            };
        }
    }

    internal static class MacroEcosystemMath
    {
        public const uint SectorFaultInvalidMath = 1u << 0;
        public const uint TuningFlagSnapshotWriteInFlight = MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight;
        public const uint BiomeAbyssalPlain = 0x2B38B429u;
        public const uint BiomeThermalVent = 0x6F90CB76u;
        public const uint BiomeKelpTrench = 0x64E62B68u;
        public const uint BiomeReactorRuin = 0x213B297Cu;
        public const uint BiomeBrineLake = 0x2F4F2039u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeSectorHash(long sectorX, long sectorY, long sectorZ)
        {
            ulong hash = 14695981039346656037UL;
            hash = MixLong(hash, sectorX);
            hash = MixLong(hash, sectorY);
            hash = MixLong(hash, sectorZ);
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix32(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SelectMockBiomeHash(uint noise)
        {
            uint bucket = noise % 5u;
            return bucket == 0u
                ? BiomeAbyssalPlain
                : bucket == 1u
                    ? BiomeThermalVent
                    : bucket == 2u
                        ? BiomeKelpTrench
                        : bucket == 3u
                            ? BiomeReactorRuin
                            : BiomeBrineLake;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveDiffusionSteps(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            return math.clamp((int)math.lerp(1f, 5.999f, q), 1, 5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveOpenAddressSlot(ulong hash, int capacity)
        {
            capacity = math.max(1, capacity);
            uint mixed = (uint)(hash ^ (hash >> 32));
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return (capacity & (capacity - 1)) == 0
                ? (int)(mixed & (uint)(capacity - 1))
                : (int)(mixed % (uint)capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryResolveBiomeSpec(
            BiomeEcosystemSpecDTO* specs,
            int capacity,
            uint biomeHash,
            out BiomeEcosystemSpecDTO spec)
        {
            spec = default;
            if (specs == null || capacity <= 0 || biomeHash == 0u)
                return false;

            int slot = ResolveOpenAddressSlot(biomeHash, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                BiomeEcosystemSpecDTO current = specs[slot];
                if (current.BiomeHash == 0u)
                    return false;
                if (current.BiomeHash == biomeHash)
                {
                    spec = current;
                    return true;
                }

                slot++;
                if (slot == capacity)
                    slot = 0;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTemperatureSuitability(float temperature, float optimum, float halfRange)
        {
            float safeTemp = math.select(optimum, temperature, math.isfinite(temperature));
            float safeRange = math.max(0.001f, math.select(1f, halfRange, math.isfinite(halfRange)));
            return math.saturate(1f - math.abs(safeTemp - optimum) * math.rcp(safeRange));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint QuantizeBiomass(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0u;
            if (value >= 4294967040f)
                return uint.MaxValue;
            return (uint)math.floor(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixLong(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                hash = (hash ^ ((v >> shift) & 0xFFUL)) * 1099511628211UL;
            return hash;
        }
    }

    internal static class MacroEcosystemLayoutManifest
    {
        private static bool _verified;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void VerifyEditor()
        {
            VerifyColdBoot();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _verified = false;
        }

        internal static void VerifyColdBoot()
        {
            if (_verified)
                return;

            AssertSize<EcosystemSectorDTO>(32);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PreyBiomass), 8);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PredatorBiomass), 12);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.LocalTemperature), 16);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.ToxinLevel), 20);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO._pad0), 24);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO._pad7), 31);
            AssertSize<MacroEcosystemSectorVaultRecord>(32);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.SectorHash), 0);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.PreyBiomass), 8);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.PredatorBiomass), 12);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.LocalTemperature), 16);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.ToxinLevel), 20);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord._pad0), 24);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord._pad7), 31);
            AssertSize<EcosystemSectorCoordDTO>(32);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorX), 0);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorY), 8);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorZ), 16);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.BiomeHash), 24);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.Flags), 28);
            AssertSize<EcosystemSectorRemainderDTO>(16);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.PreyFraction), 0);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.PredatorFraction), 4);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.DiffusionPreyFraction), 8);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.DiffusionPredatorFraction), 12);
            AssertSize<EcosystemSectorIndexEntryDTO>(16);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.Slot), 8);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.Occupied), 12);
            AssertSize<MacroEcosystemSectorIndexRecord>(16);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.SectorHash), 0);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.Slot), 8);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.Occupied), 12);
            AssertSize<BiomeEcosystemSpecDTO>(24);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.BiomeHash), 0);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.CarryingCapacityPrey), 4);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.CarryingCapacityPredator), 8);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.MigrationResistance), 12);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.TemperatureOptimum), 16);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.ToxinPenalty), 20);
            AssertSize<MacroEcosystemTuningDTO>(64);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.BaseBirthRate), 0);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.MigrationRate), 24);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.GlobalQualityWeight), 28);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.Flags), 52);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.StateHash), 56);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.Reserved), 60);
            AssertSize<MacroEcosystemTuningVaultRecord>(64);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.BaseBirthRate), 0);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.MigrationRate), 24);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.GlobalQualityWeight), 28);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.Flags), 52);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.StateHash), 56);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.Reserved), 60);
            AssertSize<MacroEcosystemTelemetryEntry>(64);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.FrameIndex), 0);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalPreyBiomass), 8);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalPredatorBiomass), 16);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.SolverMicroseconds), 32);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.Flags), 44);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalMass), 48);
            AssertSize<MacroEcosystemCounterDTO>(64);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Value), 0);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Flags), 4);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Reserved0), 8);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Reserved6), 56);
            _verified = true;
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                throw new CriticalBootException("[MacroEcosystemLayout] Size mismatch " + typeof(T).Name);
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed != expected)
                throw new CriticalBootException("[MacroEcosystemLayout] Offset mismatch " + typeof(T).Name + "." + fieldName);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearMacroEcosystemVaultTablesJob : IJob
    {
        [NoAlias] public NativeArray<EcosystemSectorIndexEntryDTO> IndexEntries;
        [NoAlias] public NativeArray<BiomeEcosystemSpecDTO> BiomeSpecs;
        [NoAlias] public NativeArray<MacroEcosystemCounterDTO> Counters;
        [NoAlias] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<uint> FaultFlags;

        public void Execute()
        {
            for (int i = 0; i < IndexEntries.Length; i++)
                IndexEntries[i] = default;
            for (int i = 0; i < BiomeSpecs.Length; i++)
                BiomeSpecs[i] = default;
            SeedDefaultBiomeSpecs(BiomeSpecs);
            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = default;
            for (int i = 0; i < Telemetry.Length; i++)
                Telemetry[i] = default;
            for (int i = 0; i < FaultFlags.Length; i++)
                FaultFlags[i] = 0u;
            if (Counters.Length > 0)
                Counters[0] = MacroEcosystemCounterDTO.FromValue(1);
        }

        private static void SeedDefaultBiomeSpecs(NativeArray<BiomeEcosystemSpecDTO> specs)
        {
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeAbyssalPlain,
                CarryingCapacityPrey = 56000f,
                CarryingCapacityPredator = 9000f,
                MigrationResistance = 0.22f,
                TemperatureOptimum = 5f,
                ToxinPenalty = 0.35f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeThermalVent,
                CarryingCapacityPrey = 32000f,
                CarryingCapacityPredator = 14000f,
                MigrationResistance = 0.38f,
                TemperatureOptimum = 18f,
                ToxinPenalty = 0.62f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeKelpTrench,
                CarryingCapacityPrey = 72000f,
                CarryingCapacityPredator = 7000f,
                MigrationResistance = 0.18f,
                TemperatureOptimum = 9f,
                ToxinPenalty = 0.18f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeReactorRuin,
                CarryingCapacityPrey = 12000f,
                CarryingCapacityPredator = 3000f,
                MigrationResistance = 0.64f,
                TemperatureOptimum = 7f,
                ToxinPenalty = 0.95f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeBrineLake,
                CarryingCapacityPrey = 26000f,
                CarryingCapacityPredator = 5000f,
                MigrationResistance = 0.52f,
                TemperatureOptimum = 3f,
                ToxinPenalty = 0.78f
            });
        }

        private static void InsertBiomeSpec(NativeArray<BiomeEcosystemSpecDTO> specs, BiomeEcosystemSpecDTO spec)
        {
            if (specs.Length <= 0 || spec.BiomeHash == 0u)
                return;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(spec.BiomeHash, specs.Length);
            for (int probe = 0; probe < specs.Length; probe++)
            {
                BiomeEcosystemSpecDTO existing = specs[slot];
                if (existing.BiomeHash == 0u || existing.BiomeHash == spec.BiomeHash)
                {
                    specs[slot] = spec;
                    return;
                }

                slot++;
                if (slot == specs.Length)
                    slot = 0;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateEmergencyMockEcosystemJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Front;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Back;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        public int Width;
        public int Height;
        public long OriginX;
        public long OriginZ;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            long sectorX = OriginX + x;
            long sectorY = 0L;
            long sectorZ = OriginZ + z;
            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            uint noise = (uint)(hash ^ (hash >> 32));
            noise = noise * 747796405u + 2891336453u;
            float temp01 = (noise & 1023u) * math.rcp(1023f);
            float toxin01 = ((noise >> 10) & 255u) * math.rcp(255f);
            uint prey = 1800u + ((noise >> 18) & 4095u);
            uint predator = 80u + ((noise >> 6) & 511u);
            EcosystemSectorDTO sector = new EcosystemSectorDTO
            {
                SectorHash = hash,
                PreyBiomass = prey,
                PredatorBiomass = predator,
                LocalTemperature = math.lerp(-2f, 18f, temp01),
                ToxinLevel = toxin01 * toxin01 * 0.65f
            };

            Front[index] = sector;
            Back[index] = sector;
            Remainders[index] = default;
            FaultFlags[index] = 0u;
            Coords[index] = new EcosystemSectorCoordDTO
            {
                SectorX = sectorX,
                SectorY = sectorY,
                SectorZ = sectorZ,
                BiomeHash = MacroEcosystemMath.SelectMockBiomeHash(noise),
                Flags = 1u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BuildSectorIndexJob : IJob
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Sectors;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorIndexEntryDTO* IndexEntries;
        public int SectorCount;
        public int IndexCapacity;

        public void Execute()
        {
            for (int i = 0; i < SectorCount; i++)
            {
                ulong hash = Sectors[i].SectorHash;
                int slot = MacroEcosystemMath.ResolveOpenAddressSlot(hash, IndexCapacity);
                for (int probe = 0; probe < IndexCapacity; probe++)
                {
                    EcosystemSectorIndexEntryDTO existing = IndexEntries[slot];
                    if (existing.Occupied == 0u || existing.SectorHash == hash)
                    {
                        IndexEntries[slot] = new EcosystemSectorIndexEntryDTO
                        {
                            SectorHash = hash,
                            Slot = i,
                            Occupied = 1u
                        };
                        break;
                    }

                    slot++;
                    if (slot == IndexCapacity)
                        slot = 0;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EcosystemPopulationJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Front;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Back;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public BiomeEcosystemSpecDTO* BiomeSpecs;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        public MacroEcosystemTuningDTO Tuning;
        public int SectorCount;
        public int BiomeSpecCapacity;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SectorCount)
                return;

            ref EcosystemSectorDTO src = ref UnsafeUtility.AsRef<EcosystemSectorDTO>(Front + index);
            ref EcosystemSectorDTO dst = ref UnsafeUtility.AsRef<EcosystemSectorDTO>(Back + index);
            ref EcosystemSectorRemainderDTO rem = ref UnsafeUtility.AsRef<EcosystemSectorRemainderDTO>(Remainders + index);
            EcosystemSectorCoordDTO coord = Coords[index];
            bool hasSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, coord.BiomeHash, out BiomeEcosystemSpecDTO spec);
            float preyCapacity = hasSpec ? math.max(1f, spec.CarryingCapacityPrey) : Tuning.CarryingCapacityPrey;
            float predatorCapacity = hasSpec ? math.max(1f, spec.CarryingCapacityPredator) : Tuning.CarryingCapacityPredator;
            float temperatureOptimum = hasSpec ? spec.TemperatureOptimum : Tuning.TemperatureOptimum;
            float toxinPenalty = hasSpec ? math.saturate(spec.ToxinPenalty) : 1f;

            float tempSuitability = MacroEcosystemMath.ResolveTemperatureSuitability(src.LocalTemperature, temperatureOptimum, Tuning.TemperatureHalfRange);
            float toxin = math.saturate(math.select(1f, src.ToxinLevel, math.isfinite(src.ToxinLevel)));
            float x = src.PreyBiomass + rem.PreyFraction;
            float y = src.PredatorBiomass + rem.PredatorFraction;
            float alpha = Tuning.BaseBirthRate * tempSuitability * (1f - toxin * Tuning.ToxicityBirthSuppression * toxinPenalty);
            float beta = Tuning.PredationRate;
            float delta = Tuning.PredatorConversionRate;
            float gamma = Tuning.PredatorStarvationRate * (1f + toxin * Tuning.ToxicityDeathBoost * math.max(0.25f, toxinPenalty));
            float interaction = x * y;
            float dt = math.max(0.001f, Tuning.FrostDeltaSeconds);
            float preyNext = math.clamp(x + ((alpha * x - beta * interaction) * dt), 0f, preyCapacity);
            float predatorNext = math.clamp(y + ((delta * interaction - gamma * y) * dt), 0f, predatorCapacity);

            bool valid = math.isfinite(preyNext) & math.isfinite(predatorNext);
            if (!valid)
            {
                preyNext = math.min((float)src.PreyBiomass, preyCapacity);
                predatorNext = math.min((float)src.PredatorBiomass, predatorCapacity);
            }
            FaultFlags[index] = valid ? 0u : MacroEcosystemMath.SectorFaultInvalidMath;

            uint preyQuantized = MacroEcosystemMath.QuantizeBiomass(preyNext);
            uint predatorQuantized = MacroEcosystemMath.QuantizeBiomass(predatorNext);
            rem.PreyFraction = math.saturate(preyNext - preyQuantized);
            rem.PredatorFraction = math.saturate(predatorNext - predatorQuantized);
            dst = src;
            dst.PreyBiomass = preyQuantized;
            dst.PredatorBiomass = predatorQuantized;
            dst.ToxinLevel = toxin;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BiomassDiffusionJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Source;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Destination;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public BiomeEcosystemSpecDTO* BiomeSpecs;
        public int SectorCount;
        public int BiomeSpecCapacity;
        public int Width;
        public int Height;
        public float SectorSizeMeters;
        public float MigrationRate;
        public float CarryingCapacityPrey;
        public float CarryingCapacityPredator;
        public float TemperatureOptimum;
        public float TemperatureHalfRange;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SectorCount)
                return;

            int x = index % Width;
            int z = index / Width;
            EcosystemSectorDTO center = Source[index];
            EcosystemSectorCoordDTO centerCoord = Coords[index];
            float preyDelta = 0f;
            float predatorDelta = 0f;
            AccumulateNeighbor(x > 0 ? index - 1 : -1, center, centerCoord, ref preyDelta, ref predatorDelta);
            AccumulateNeighbor(x < Width - 1 ? index + 1 : -1, center, centerCoord, ref preyDelta, ref predatorDelta);
            AccumulateNeighbor(z > 0 ? index - Width : -1, center, centerCoord, ref preyDelta, ref predatorDelta);
            AccumulateNeighbor(z < Height - 1 ? index + Width : -1, center, centerCoord, ref preyDelta, ref predatorDelta);

            ref EcosystemSectorRemainderDTO rem = ref UnsafeUtility.AsRef<EcosystemSectorRemainderDTO>(Remainders + index);
            float preyNext = math.clamp(center.PreyBiomass + preyDelta + rem.DiffusionPreyFraction, 0f, CarryingCapacityPrey);
            float predatorNext = math.clamp(center.PredatorBiomass + predatorDelta + rem.DiffusionPredatorFraction, 0f, CarryingCapacityPredator);
            uint preyQuantized = MacroEcosystemMath.QuantizeBiomass(preyNext);
            uint predatorQuantized = MacroEcosystemMath.QuantizeBiomass(predatorNext);
            rem.DiffusionPreyFraction = math.saturate(preyNext - preyQuantized);
            rem.DiffusionPredatorFraction = math.saturate(predatorNext - predatorQuantized);

            EcosystemSectorDTO output = center;
            output.PreyBiomass = preyQuantized;
            output.PredatorBiomass = predatorQuantized;
            Destination[index] = output;
        }

        private void AccumulateNeighbor(int neighborIndex, EcosystemSectorDTO center, EcosystemSectorCoordDTO centerCoord, ref float preyDelta, ref float predatorDelta)
        {
            if ((uint)neighborIndex >= (uint)SectorCount)
                return;

            EcosystemSectorDTO neighbor = Source[neighborIndex];
            EcosystemSectorCoordDTO neighborCoord = Coords[neighborIndex];
            double3 centerAup = new double3((double)centerCoord.SectorX, (double)centerCoord.SectorY, (double)centerCoord.SectorZ) * (double)SectorSizeMeters;
            double3 neighborAup = new double3((double)neighborCoord.SectorX, (double)neighborCoord.SectorY, (double)neighborCoord.SectorZ) * (double)SectorSizeMeters;
            float3 delta = (float3)(neighborAup - centerAup);
            float invDistance = math.rsqrt(math.max(1f, math.lengthsq(delta)));
            bool hasCenterSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, centerCoord.BiomeHash, out BiomeEcosystemSpecDTO centerSpec);
            bool hasNeighborSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, neighborCoord.BiomeHash, out BiomeEcosystemSpecDTO neighborSpec);
            float centerOptimum = hasCenterSpec ? centerSpec.TemperatureOptimum : TemperatureOptimum;
            float neighborOptimum = hasNeighborSpec ? neighborSpec.TemperatureOptimum : TemperatureOptimum;
            float resistance = math.saturate(((hasCenterSpec ? centerSpec.MigrationResistance : 0f) + (hasNeighborSpec ? neighborSpec.MigrationResistance : 0f)) * 0.5f);
            float centerSuit = MacroEcosystemMath.ResolveTemperatureSuitability(center.LocalTemperature, centerOptimum, TemperatureHalfRange);
            float neighborSuit = MacroEcosystemMath.ResolveTemperatureSuitability(neighbor.LocalTemperature, neighborOptimum, TemperatureHalfRange);
            float pairWeight = MigrationRate * (1f - resistance) * math.min(centerSuit, neighborSuit) * math.saturate(invDistance * SectorSizeMeters);
            preyDelta += (neighbor.PreyBiomass - center.PreyBiomass) * pairWeight;
            predatorDelta += (neighbor.PredatorBiomass - center.PredatorBiomass) * pairWeight * 0.65f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CopySectorBufferJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Source;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Destination;
        public int SectorCount;

        public void Execute(int index)
        {
            if ((uint)index < (uint)SectorCount)
                Destination[index] = Source[index];
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EcosystemTelemetryReductionJob : IJob
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Sectors;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        [NoAlias] [NativeDisableParallelForRestriction] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;
        [NoAlias] [NativeDisableParallelForRestriction] public NativeArray<MacroEcosystemCounterDTO> Counters;
        public int SectorCount;
        public int TelemetryIndex;
        public uint FrameIndex;
        public uint DiffusionSteps;
        public float GlobalQualityWeight;

        public void Execute()
        {
            ulong prey = 0UL;
            ulong predator = 0UL;
            uint sterile = 0u;
            uint toxic = 0u;
            uint flags = 0u;
            uint hash = 2166136261u;
            for (int i = 0; i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                prey += sector.PreyBiomass;
                predator += sector.PredatorBiomass;
                float toxin = math.saturate(math.select(1f, sector.ToxinLevel, math.isfinite(sector.ToxinLevel)));
                toxic += toxin > 0.65f ? 1u : 0u;
                ulong sectorMass = (ulong)sector.PreyBiomass + sector.PredatorBiomass;
                sterile += sectorMass <= 1UL || toxin > 0.95f ? 1u : 0u;
                flags |= math.isfinite(sector.LocalTemperature) & math.isfinite(sector.ToxinLevel)
                    ? 0u
                    : MacroEcosystemTelemetryEntry.FlagInvalidMath;
                flags |= (FaultFlags[i] & MacroEcosystemMath.SectorFaultInvalidMath) != 0u
                    ? MacroEcosystemTelemetryEntry.FlagInvalidMath
                    : 0u;
                flags |= sector.PreyBiomass > 500000u || sector.PredatorBiomass > 250000u
                    ? MacroEcosystemTelemetryEntry.FlagPopulationExplosion
                    : 0u;
                hash = MacroEcosystemMath.Mix32(hash, (uint)sector.SectorHash);
                hash = MacroEcosystemMath.Mix32(hash, sector.PreyBiomass);
                hash = MacroEcosystemMath.Mix32(hash, sector.PredatorBiomass);
            }

            if ((uint)TelemetryIndex < (uint)Telemetry.Length)
            {
                Telemetry[TelemetryIndex] = new MacroEcosystemTelemetryEntry
                {
                    FrameIndex = FrameIndex,
                    StateHash = hash,
                    TotalPreyBiomass = prey,
                    TotalPredatorBiomass = predator,
                    SterileSectorCount = sterile,
                    ToxicSectorCount = toxic,
                    SolverMicroseconds = 0f,
                    GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                    DiffusionSteps = DiffusionSteps,
                    Flags = flags,
                    TotalMass = prey + predator,
                    Reserved = 0UL
                };
            }

            if (Counters.IsCreated)
            {
                if (Counters.Length > 1) Counters[1] = MacroEcosystemCounterDTO.FromValue(SectorCount);
                if (Counters.Length > 2) Counters[2] = MacroEcosystemCounterDTO.FromValue(toxic > int.MaxValue ? int.MaxValue : (int)toxic);
                if (Counters.Length > 3) Counters[3] = MacroEcosystemCounterDTO.FromValue(sterile > int.MaxValue ? int.MaxValue : (int)sterile);
                if (Counters.Length > 4) Counters[4] = MacroEcosystemCounterDTO.FromValue((int)DiffusionSteps);
                if (Counters.Length > 5) Counters[5] = MacroEcosystemCounterDTO.FromValue((int)flags);
            }
        }
    }

    internal static class MacroEcosystemCsvParser
    {
        internal static int ParseBiomeSpecs(
            ReadOnlySpan<byte> csv,
            NativeArray<BiomeEcosystemSpecDTO> specs)
        {
            int parsed = 0;
            int cursor = 0;
            int row = 0;
            for (int i = 0; i < specs.Length; i++)
                specs[i] = default;

            while (cursor < csv.Length && parsed < specs.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                    cursor++;
                int lineEnd = cursor;
                while (cursor < csv.Length && (csv[cursor] == (byte)'\n' || csv[cursor] == (byte)'\r'))
                    cursor++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, lineEnd - lineStart);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                row++;
                if (row == 1 && StartsWithAscii(line, "biome"))
                    continue;

                if (TryParseBiomeLine(line, out BiomeEcosystemSpecDTO spec))
                {
                    parsed += TryInsertBiomeSpec(specs, spec) ? 1 : 0;
                }
            }

            return parsed;
        }

        private static bool TryInsertBiomeSpec(NativeArray<BiomeEcosystemSpecDTO> specs, BiomeEcosystemSpecDTO spec)
        {
            if (!specs.IsCreated || specs.Length <= 0 || spec.BiomeHash == 0u)
                return false;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(spec.BiomeHash, specs.Length);
            for (int probe = 0; probe < specs.Length; probe++)
            {
                BiomeEcosystemSpecDTO existing = specs[slot];
                if (existing.BiomeHash == 0u || existing.BiomeHash == spec.BiomeHash)
                {
                    specs[slot] = spec;
                    return true;
                }

                slot++;
                if (slot == specs.Length)
                    slot = 0;
            }

            return false;
        }

        private static bool TryParseBiomeLine(ReadOnlySpan<byte> line, out BiomeEcosystemSpecDTO spec)
        {
            spec = default;
            ReadOnlySpan<byte> biome = ReadField(line, 0);
            if (biome.Length == 0)
                return false;

            if (!TryParseFloat(ReadField(line, 1), out float preyCapacity) ||
                !TryParseFloat(ReadField(line, 2), out float predatorCapacity) ||
                !TryParseFloat(ReadField(line, 3), out float migrationResistance) ||
                !TryParseFloat(ReadField(line, 4), out float temperatureOptimum) ||
                !TryParseFloat(ReadField(line, 5), out float toxinPenalty))
            {
                return false;
            }

            spec = new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.HashLowerAscii(biome),
                CarryingCapacityPrey = math.max(1f, preyCapacity),
                CarryingCapacityPredator = math.max(1f, predatorCapacity),
                MigrationResistance = math.saturate(migrationResistance),
                TemperatureOptimum = math.select(8f, temperatureOptimum, math.isfinite(temperatureOptimum)),
                ToxinPenalty = math.saturate(toxinPenalty)
            };
            return true;
        }

        private static ReadOnlySpan<byte> ReadField(ReadOnlySpan<byte> line, int fieldIndex)
        {
            int current = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i != line.Length && line[i] != (byte)',')
                    continue;

                if (current == fieldIndex)
                    return Trim(line.Slice(start, i - start));

                current++;
                start = i + 1;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length;
            while (start < end && value[start] <= 32)
                start++;
            while (end > start && value[end - 1] <= 32)
                end--;
            return value.Slice(start, end - start);
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (value[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = integer * 10f + (value[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction += (value[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            result = (integer + fraction) * sign;
            return hasDigit && math.isfinite(result);
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> value, string ascii)
        {
            if (value.Length < ascii.Length)
                return false;

            for (int i = 0; i < ascii.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)ascii[i])
                    return false;
            }

            return true;
        }
    }
}
