using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.World.SeedShipAnomaly
{
    [DisallowMultipleComponent]
    public sealed unsafe class SeedShipAnomalyRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.EndgameAnomaly;
        private const int CsvMaxBytes = 8192;
        private const int DumpScratchBytes = 32 + SeedShipAnomalyConstants.TelemetryFrameCount * 64;
        private const int LockBufferCount = 9;
        private const int JobBatchSize = 64;
        private const float ComputeBudgetMs = 0.1f;
        private const string CsvRelativePath = "anomaly_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SEED_SHIP_ANOMALY.bin";
        private const string LegacyEmissionFile = "seed_ship_emission_rates.h8bin";
        private const string LegacyGlitchFile = "glitch_zones_007.bin";
        private const ulong DumpMagic = 0x5345454453484950UL; // SEEDSHIP
        private const uint DumpVersion = 1u;

        private static readonly uint _MaxCorruptionRadiusHash = HashLowerAsciiString("max_corruption_radius");
        private static readonly uint _GravityInversionStrengthHash = HashLowerAsciiString("gravity_inversion_strength");
        private static readonly uint _PulseFrequencyHash = HashLowerAsciiString("pulse_frequency");
        private static readonly uint _GlitchIntensityHash = HashLowerAsciiString("glitch_intensity");
        private static readonly uint _HeatEmissionHash = HashLowerAsciiString("heat_emission");
        private static readonly uint _RadiationEmissionHash = HashLowerAsciiString("radiation_emission");
        private static readonly uint _RadarJamIntensityHash = HashLowerAsciiString("radar_jam_intensity");
        private static readonly uint _BabelScrambleStrengthHash = HashLowerAsciiString("babel_scramble_strength");
        private static readonly uint _GlobalQualityWeightHash = HashLowerAsciiString("global_quality_weight");

        [Header("Seed Ship AUP")]
        [SerializeField] private double seedShipAupX;
        [SerializeField] private double seedShipAupY = SeedShipAnomalyConstants.DefaultSeedShipDepthMeters;
        [SerializeField] private double seedShipAupZ;

        [Header("Runtime Capacity")]
        [SerializeField, Min(1)] private int mockLeviathanCapacity = SeedShipAnomalyConstants.DefaultMockLeviathanCapacity;
        [SerializeField, Range(0f, 1f)] private float defaultGlobalQualityWeight = 1f;

        private VaultBufferHandle<AnomalyFieldDTO> _fieldHandle;
        private VaultBufferHandle<AnomalyTuningDTO> _tuningHandle;
        private VaultBufferHandle<AnomalyGlobalScalarsDTO> _globalsHandle;
        private VaultBufferHandle<GlitchCommandDTO> _glitchHandle;
        private VaultBufferHandle<MockHudSignal> _hudHandle;
        private VaultBufferHandle<MockLeviathanState> _leviathanHandle;
        private VaultBufferHandle<MockAupRebaseSignal> _rebaseHandle;
        private VaultBufferHandle<AnomalyThermoSourceDTO> _thermoHandle;
        private VaultBufferHandle<AnomalyTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<AnomalyCsvOverrideDTO> _csvOverrideHandle;
        private VaultBufferHandle<byte> _ioScratchHandle;
        private VaultBufferHandle<byte> _dumpScratchHandle;
        private VaultBufferHandle<ScalabilityStateDTO> _scalabilityHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private JobHandle _activeJobHandle;
        private string _projectRoot;
        private string _csvPath;
        private string _dumpPath;
        private long _csvLastWriteTicks;
        private long _jobStartTimestamp;
        private int _telemetryCursor;
        private int _scheduledEntityBudget;
        private float _localTimeSeconds;
        private float _healingSecondsRemaining;
        private uint _simulationFrameCounter;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _defaultsInitialized;
        private bool _dumpedBudgetBreach;
        private bool _legacyReconComplete;
        private uint _legacyReconFlags;

        private void Awake()
        {
            mockLeviathanCapacity = math.max(1, mockLeviathanCapacity);
            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(_projectRoot, CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(_projectRoot, DumpRelativePath));
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureSignalLanesReady();
            TryRegisterHotSwapListener();
            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            ClearCachedHandles();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteFrameJobForTeardown();
                UnlockJobBuffers();
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _legacyReconComplete = false;
                if (_dataVault != null && EnsureVaultState())
                    TryRegisterTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerContext = currentService as IPlayerRuntimeContext;
        }

        public void Tick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return;

            float dt = math.clamp(deltaTime, 0.0001f, 0.05f);
            _localTimeSeconds += dt;
            _simulationFrameCounter++;
            ConsumeHackSignals(dt);

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AnomalyFieldDTO> field,
                    out NativeArray<AnomalyTuningDTO> tuningArray,
                    out NativeArray<AnomalyGlobalScalarsDTO> globals,
                    out NativeArray<GlitchCommandDTO> glitch,
                    out NativeArray<MockHudSignal> hud,
                    out NativeArray<MockLeviathanState> leviathans,
                    out NativeArray<MockAupRebaseSignal> rebase,
                    out NativeArray<AnomalyThermoSourceDTO> thermo,
                    out NativeArray<AnomalyTelemetryEntry> telemetry))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            AnomalyTuningDTO tuning = SeedShipAnomalyMath.SanitizeTuning(tuningArray[0]);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight(vault, tuning.GlobalQualityWeight);
            tuningArray[0] = tuning;

            int entityBudget = SeedShipAnomalyMath.ResolveEntityBudget(
                leviathans.Length,
                tuning.GlobalQualityWeight,
                globals[0].Corruption01,
                tuning.MinEntityBudget,
                tuning.MaxEntityBudget);
            _scheduledEntityBudget = entityBudget;

            double3 playerAup = ResolvePlayerAup();
            uint frame = _simulationFrameCounter;
            uint sectorHash = SeedShipAnomalyMath.HashAupSector(playerAup);
            _jobStartTimestamp = Stopwatch.GetTimestamp();

            JobHandle handle = new SeedShipMockAupRebaseJob
            {
                RebaseSignals = rebase,
                Frame = frame,
                Seed = SeedShipAnomalyConstants.SourceHash,
                SectorHash = sectorHash,
                Chance01 = tuning.MockRebaseChance01
            }.Schedule();

            handle = new SeedShipAnomalyFieldJob
            {
                Field = field,
                Tuning = tuningArray,
                Globals = globals,
                GlitchCommands = glitch,
                HudSignals = hud,
                ThermoSources = thermo,
                RebaseSignals = rebase,
                Telemetry = telemetry,
                RadarJamWriter = SignalBus<RadarJamSignal>.ParallelWriter,
                PlayerAUP = playerAup,
                DeltaSeconds = dt,
                TimeSeconds = _localTimeSeconds,
                HackHealingSeconds = _healingSecondsRemaining,
                TelemetryCursor = _telemetryCursor,
                EntityBudget = entityBudget,
                Frame = frame,
                EmitRadarSignal = 1
            }.Schedule(handle);

            if (entityBudget > 0)
            {
                handle = new SeedShipLeviathanFrenzyJob
                {
                    Field = field,
                    Tuning = tuningArray,
                    Leviathans = leviathans,
                    Frame = frame
                }.Schedule(entityBudget, JobBatchSize, handle);
            }

            _activeJobHandle = handle;
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        public void SlowTick()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _jobScheduled || !EnsureVaultState())
                return;

            MonitorCsvOverrides(vault);
        }

        public ref AnomalyFieldDTO GetAnomalyFieldRef()
        {
            IDataVault vault = _dataVault;
            NativeArray<AnomalyFieldDTO> field = vault != null ? _fieldHandle.Resolve(vault) : default;
            if (!field.IsCreated || field.Length == 0)
                FatalMemoryException.ThrowStaleVaultHandle();

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(field);
            return ref UnsafeUtility.ArrayElementAsRef<AnomalyFieldDTO>(ptr, 0);
        }

        public bool TryGetField(out AnomalyFieldDTO field)
        {
            field = default;
            IDataVault vault = _dataVault;
            NativeArray<AnomalyFieldDTO> array = vault != null ? _fieldHandle.Resolve(vault) : default;
            if (!array.IsCreated || array.Length == 0)
                return false;

            field = array[0];
            return true;
        }

        public bool TryGetGlobals(out AnomalyGlobalScalarsDTO globals)
        {
            globals = default;
            IDataVault vault = _dataVault;
            NativeArray<AnomalyGlobalScalarsDTO> array = vault != null ? _globalsHandle.Resolve(vault) : default;
            if (!array.IsCreated || array.Length == 0)
                return false;

            globals = array[0];
            return true;
        }

        public bool TryGetTuning(out AnomalyTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            NativeArray<AnomalyTuningDTO> array = vault != null ? _tuningHandle.Resolve(vault) : default;
            if (!array.IsCreated || array.Length == 0)
                return false;

            tuning = array[0];
            return true;
        }

        public bool SetEditorTuning(AnomalyTuningDTO tuning)
        {
            IDataVault vault = _dataVault;
            NativeArray<AnomalyTuningDTO> array = vault != null ? _tuningHandle.Resolve(vault) : default;
            if (!array.IsCreated || array.Length == 0)
                return false;

            array[0] = SeedShipAnomalyMath.SanitizeTuning(tuning);
            return true;
        }

        public bool SetEditorField(AnomalyFieldDTO field)
        {
            IDataVault vault = _dataVault;
            NativeArray<AnomalyFieldDTO> array = vault != null ? _fieldHandle.Resolve(vault) : default;
            if (!array.IsCreated || array.Length == 0)
                return false;

            field.Radius = math.max(0f, field.Radius);
            field.CorruptionLevel = math.saturate(field.CorruptionLevel);
            field.GlitchHash = field.GlitchHash != 0u ? field.GlitchHash : SeedShipAnomalyConstants.GlitchHash;
            array[0] = field;
            return true;
        }

        public void InjectCoreHack(uint codeHash, float validity01)
        {
            SignalBus<CoreHackedSignal>.TryPush(new CoreHackedSignal
            {
                Frame = _simulationFrameCounter,
                SourceHash = SeedShipAnomalyConstants.SourceHash,
                CodeHash = codeHash,
                Validity01 = math.saturate(validity01),
                Flags = 1
            });
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            mockLeviathanCapacity = math.max(1, mockLeviathanCapacity);
            if (HandlesReady())
                return true;

            _fieldHandle = vault.GetBufferHandle<AnomalyFieldDTO>(BufferID.ShinobuSeedShipAnomalyField, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<AnomalyTuningDTO>(BufferID.ShinobuSeedShipAnomalyTuning, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _globalsHandle = vault.GetBufferHandle<AnomalyGlobalScalarsDTO>(BufferID.ShinobuSeedShipAnomalyGlobals, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _glitchHandle = vault.GetBufferHandle<GlitchCommandDTO>(BufferID.ShinobuSeedShipAnomalyGlitchCommand, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _hudHandle = vault.GetBufferHandle<MockHudSignal>(BufferID.ShinobuSeedShipAnomalyMockHudSignals, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _leviathanHandle = vault.GetBufferHandle<MockLeviathanState>(BufferID.ShinobuSeedShipAnomalyMockLeviathans, mockLeviathanCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _rebaseHandle = vault.GetBufferHandle<MockAupRebaseSignal>(BufferID.ShinobuSeedShipAnomalyMockAupRebase, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _thermoHandle = vault.GetBufferHandle<AnomalyThermoSourceDTO>(BufferID.ShinobuSeedShipAnomalyThermoSource, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<AnomalyTelemetryEntry>(BufferID.ShinobuSeedShipAnomalyTelemetryRing, SeedShipAnomalyConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _csvOverrideHandle = vault.GetBufferHandle<AnomalyCsvOverrideDTO>(BufferID.ShinobuSeedShipAnomalyCsvOverrides, SeedShipAnomalyConstants.CsvOverrideCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _ioScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuSeedShipAnomalyIoScratch, CsvMaxBytes, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _dumpScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuSeedShipAnomalyDumpScratch, DumpScratchBytes, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            if (vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out VaultBufferHandle<ScalabilityStateDTO> scalability))
                _scalabilityHandle = scalability;

            if (!HandlesReady())
                return false;

            InitializeDefaults(vault);
            return true;
        }

        private bool HandlesReady()
        {
            return _fieldHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _globalsHandle.IsCreated &&
                   _glitchHandle.IsCreated &&
                   _hudHandle.IsCreated &&
                   _leviathanHandle.IsCreated &&
                   _rebaseHandle.IsCreated &&
                   _thermoHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _csvOverrideHandle.IsCreated &&
                   _ioScratchHandle.IsCreated &&
                   _dumpScratchHandle.IsCreated;
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AnomalyFieldDTO> field,
                    out NativeArray<AnomalyTuningDTO> tuning,
                    out NativeArray<AnomalyGlobalScalarsDTO> globals,
                    out NativeArray<GlitchCommandDTO> glitch,
                    out NativeArray<MockHudSignal> hud,
                    out NativeArray<MockLeviathanState> leviathans,
                    out NativeArray<MockAupRebaseSignal> rebase,
                    out NativeArray<AnomalyThermoSourceDTO> thermo,
                    out NativeArray<AnomalyTelemetryEntry> telemetry))
            {
                return;
            }

            NativeArray<AnomalyCsvOverrideDTO> csvOverrides = _csvOverrideHandle.Resolve(vault);
            GenerateEmergencyMockAnomalies(field, tuning, globals, glitch, hud, leviathans, rebase, thermo, telemetry, csvOverrides);
            TryLoadLegacyAnomalyTables(vault, field, tuning);
            _defaultsInitialized = true;
        }

        private void GenerateEmergencyMockAnomalies(
            NativeArray<AnomalyFieldDTO> field,
            NativeArray<AnomalyTuningDTO> tuning,
            NativeArray<AnomalyGlobalScalarsDTO> globals,
            NativeArray<GlitchCommandDTO> glitch,
            NativeArray<MockHudSignal> hud,
            NativeArray<MockLeviathanState> leviathans,
            NativeArray<MockAupRebaseSignal> rebase,
            NativeArray<AnomalyThermoSourceDTO> thermo,
            NativeArray<AnomalyTelemetryEntry> telemetry,
            NativeArray<AnomalyCsvOverrideDTO> csvOverrides)
        {
            double3 epicenter = new double3(seedShipAupX, seedShipAupY, seedShipAupZ);
            AnomalyFieldDTO fieldDto = new AnomalyFieldDTO
            {
                EpicenterAUP = epicenter,
                Radius = SeedShipAnomalyConstants.DefaultRadiusMeters,
                CorruptionLevel = 0f,
                GlitchHash = SeedShipAnomalyConstants.GlitchHash,
                _pad0 = 0u,
                _pad1 = 0UL
            };

            AnomalyTuningDTO tuningDto = SeedShipAnomalyMath.SanitizeTuning(new AnomalyTuningDTO
            {
                MaxCorruptionRadius = SeedShipAnomalyConstants.DefaultRadiusMeters,
                GravityInversionStrength = 1f,
                PulseFrequency = 1.7f,
                GlitchIntensity = 0.85f,
                HeatEmission = 0.9f,
                RadiationEmission = 0.7f,
                RadarJamIntensity = 0.8f,
                BabelScrambleStrength = 0.65f,
                GlobalQualityWeight = math.saturate(defaultGlobalQualityWeight),
                MinEntityBudget = 0,
                MaxEntityBudget = mockLeviathanCapacity,
                ShaderNoiseStrength = 0.75f,
                HealingRateScalar = 1f,
                MockRebaseChance01 = 0.015f
            });

            field[0] = fieldDto;
            tuning[0] = tuningDto;
            globals[0] = new AnomalyGlobalScalarsDTO
            {
                GravityY = 9.80665f,
                GlobalQualityWeight = tuningDto.GlobalQualityWeight,
                RadiusMeters = fieldDto.Radius
            };
            glitch[0] = new GlitchCommandDTO { GlyphHash = SeedShipAnomalyConstants.GlitchHash };
            hud[0] = default;
            rebase[0] = default;
            thermo[0] = new AnomalyThermoSourceDTO { EpicenterAUP = epicenter, RadiusMeters = fieldDto.Radius };

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            for (int i = 0; i < csvOverrides.Length; i++)
                csvOverrides[i] = default;

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0xA48D1E55u);
            for (int i = 0; i < leviathans.Length; i++)
            {
                float angle = random.NextFloat(0f, math.PI * 2f);
                float radius = random.NextFloat(250f, 250f + fieldDto.Radius);
                float y = random.NextFloat(-850f, 850f);
                double3 offset = new double3(math.cos(angle) * radius, y, math.sin(angle) * radius);
                leviathans[i] = new MockLeviathanState
                {
                    AUP = epicenter + offset,
                    AggressionWeight = 0.1f,
                    LightAversion = 1f,
                    EntityId = unchecked((uint)(0x4C455600u + i)),
                    LastDistanceMeters = radius
                };
            }
        }

        private bool TryLoadLegacyAnomalyTables(IDataVault vault, NativeArray<AnomalyFieldDTO> field, NativeArray<AnomalyTuningDTO> tuning)
        {
            if (_legacyReconComplete)
                return (_legacyReconFlags & 1u) != 0u;

            _legacyReconComplete = true;
            try
            {
                if (TryFindLegacyFile(LegacyEmissionFile, out string emissionPath) &&
                    TryReadLegacyEmission(vault, emissionPath, out float radius, out float heat, out float radiation))
                {
                    AnomalyFieldDTO currentField = field[0];
                    currentField.Radius = math.clamp(radius, 1f, 12000f);
                    field[0] = currentField;

                    AnomalyTuningDTO currentTuning = tuning[0];
                    currentTuning.MaxCorruptionRadius = currentField.Radius;
                    currentTuning.HeatEmission = math.saturate(heat);
                    currentTuning.RadiationEmission = math.saturate(radiation);
                    tuning[0] = SeedShipAnomalyMath.SanitizeTuning(currentTuning);
                    _legacyReconFlags |= 1u;
                }

                if (TryFindLegacyFile(LegacyGlitchFile, out string glitchPath) &&
                    TryReadLegacyGlitch(vault, glitchPath, out uint glitchHash, out float intensity))
                {
                    AnomalyFieldDTO currentField = field[0];
                    currentField.GlitchHash = glitchHash != 0u ? glitchHash : SeedShipAnomalyConstants.GlitchHash;
                    field[0] = currentField;

                    AnomalyTuningDTO currentTuning = tuning[0];
                    currentTuning.GlitchIntensity = math.saturate(intensity);
                    tuning[0] = SeedShipAnomalyMath.SanitizeTuning(currentTuning);
                    _legacyReconFlags |= 2u;
                }
            }
            catch (Exception)
            {
                _legacyReconFlags |= 4u;
            }

            return (_legacyReconFlags & 1u) != 0u;
        }

        private bool TryFindLegacyFile(string fileName, out string path)
        {
            path = null;
            if (TryFindLegacyFileInRoot(Path.Combine(_projectRoot, "Docs", "Archive"), fileName, out path))
                return true;

            string streamingRoot = Application.streamingAssetsPath;
            return !string.IsNullOrEmpty(streamingRoot) && TryFindLegacyFileInRoot(streamingRoot, fileName, out path);
        }

        private static bool TryFindLegacyFileInRoot(string root, string fileName, out string path)
        {
            path = null;
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return false;

                string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (files.Length == 0)
                    return false;

                path = files[0];
                return true;
            }
            catch (Exception)
            {
                path = null;
                return false;
            }
        }

        private bool TryReadLegacyEmission(IDataVault vault, string path, out float radius, out float heat, out float radiation)
        {
            radius = SeedShipAnomalyConstants.DefaultRadiusMeters;
            heat = 0.9f;
            radiation = 0.7f;
            if (!TryLockIoScratch(vault, out NativeArray<byte> scratch))
                return false;

            try
            {
                int read = ReadColdBytes(path, 16, scratch);
                if (read < 12)
                    return false;

                radius = ReadFloatLittleEndian(scratch, 0);
                heat = ReadFloatLittleEndian(scratch, 4);
                radiation = ReadFloatLittleEndian(scratch, 8);
                return math.isfinite(radius) && math.isfinite(heat) && math.isfinite(radiation);
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyIoScratch, OwnerSystem);
            }
        }

        private bool TryReadLegacyGlitch(IDataVault vault, string path, out uint glitchHash, out float intensity)
        {
            glitchHash = SeedShipAnomalyConstants.GlitchHash;
            intensity = 0.85f;
            if (!TryLockIoScratch(vault, out NativeArray<byte> scratch))
                return false;

            try
            {
                int read = ReadColdBytes(path, 16, scratch);
                if (read < 8)
                    return false;

                glitchHash = ReadUInt32LittleEndian(scratch, 0);
                intensity = ReadFloatLittleEndian(scratch, 4);
                return math.isfinite(intensity);
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyIoScratch, OwnerSystem);
            }
        }

        private bool TryLockIoScratch(IDataVault vault, out NativeArray<byte> scratch)
        {
            scratch = _ioScratchHandle.Resolve(vault);
            if (!scratch.IsCreated || scratch.Length == 0)
                return false;

            return vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyIoScratch, OwnerSystem);
        }

        private static int ReadColdBytes(string path, int maxBytes, NativeArray<byte> scratch)
        {
            int byteCount = math.min(math.max(0, maxBytes), scratch.Length);
            if (byteCount == 0)
                return 0;

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            Span<byte> span = new Span<byte>(ptr, byteCount);
            return stream.Read(span);
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AnomalyFieldDTO> field,
            out NativeArray<AnomalyTuningDTO> tuning,
            out NativeArray<AnomalyGlobalScalarsDTO> globals,
            out NativeArray<GlitchCommandDTO> glitch,
            out NativeArray<MockHudSignal> hud,
            out NativeArray<MockLeviathanState> leviathans,
            out NativeArray<MockAupRebaseSignal> rebase,
            out NativeArray<AnomalyThermoSourceDTO> thermo,
            out NativeArray<AnomalyTelemetryEntry> telemetry)
        {
            field = _fieldHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            globals = _globalsHandle.Resolve(vault);
            glitch = _glitchHandle.Resolve(vault);
            hud = _hudHandle.Resolve(vault);
            leviathans = _leviathanHandle.Resolve(vault);
            rebase = _rebaseHandle.Resolve(vault);
            thermo = _thermoHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            return field.IsCreated &&
                   tuning.IsCreated &&
                   globals.IsCreated &&
                   glitch.IsCreated &&
                   hud.IsCreated &&
                   leviathans.IsCreated &&
                   rebase.IsCreated &&
                   thermo.IsCreated &&
                   telemetry.IsCreated;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobLocksHeld)
                return true;

            int locked = 0;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyField, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyGlobals, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyGlitchCommand, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyMockHudSignals, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyMockLeviathans, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyMockAupRebase, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyThermoSource, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyTelemetryRing, OwnerSystem)) { UnlockPartial(vault, locked); return false; }
            locked++;

            _jobLocksHeld = locked == LockBufferCount;
            return _jobLocksHeld;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_jobLocksHeld)
                return;

            UnlockPartial(vault, LockBufferCount);
            _jobLocksHeld = false;
        }

        private static void UnlockPartial(IDataVault vault, int lockedCount)
        {
            if (lockedCount >= 9) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyTelemetryRing, OwnerSystem);
            if (lockedCount >= 8) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyThermoSource, OwnerSystem);
            if (lockedCount >= 7) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyMockAupRebase, OwnerSystem);
            if (lockedCount >= 6) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyMockLeviathans, OwnerSystem);
            if (lockedCount >= 5) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyMockHudSignals, OwnerSystem);
            if (lockedCount >= 4) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyGlitchCommand, OwnerSystem);
            if (lockedCount >= 3) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyGlobals, OwnerSystem);
            if (lockedCount >= 2) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, OwnerSystem);
            if (lockedCount >= 1) vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyField, OwnerSystem);
        }

        private void TryFinalizeFrameJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            FinishFrameJobCompletion();
        }

        private void CompleteFrameJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                return;

            FinishFrameJobCompletion();
        }

        private void FinishFrameJobCompletion()
        {
            float elapsedMs = (float)((Stopwatch.GetTimestamp() - _jobStartTimestamp) * 1000.0 / Stopwatch.Frequency);
            IDataVault vault = _dataVault;
            if (vault != null &&
                TryResolveBuffers(
                    vault,
                    out NativeArray<AnomalyFieldDTO> field,
                    out _,
                    out NativeArray<AnomalyGlobalScalarsDTO> globals,
                    out _,
                    out NativeArray<MockHudSignal> hud,
                    out _,
                    out NativeArray<MockAupRebaseSignal> rebase,
                    out NativeArray<AnomalyThermoSourceDTO> thermo,
                    out NativeArray<AnomalyTelemetryEntry> telemetry))
            {
                AnomalyGlobalScalarsDTO scalar = globals[0];
                uint diagnosticFlags = scalar.Flags;
                if (elapsedMs > ComputeBudgetMs)
                    diagnosticFlags |= SeedShipAnomalyFlags.BudgetExceeded;
                scalar.AnomalyComputeTimeMs = 0f;
                globals[0] = scalar;

                if (telemetry.IsCreated && telemetry.Length > 0)
                {
                    int cursor = math.clamp(_telemetryCursor, 0, telemetry.Length - 1);
                    AnomalyTelemetryEntry entry = telemetry[cursor];
                    entry.AnomalyComputeTimeMs = elapsedMs;
                    if (elapsedMs > ComputeBudgetMs)
                        entry.Flags |= SeedShipAnomalyFlags.BudgetExceeded;
                    telemetry[cursor] = entry;
                }

                AnomalyFieldDTO fieldSnapshot = field[0];
                MockHudSignal hudSnapshot = hud[0];
                MockAupRebaseSignal rebaseSnapshot = rebase[0];
                AnomalyThermoSourceDTO thermoSnapshot = thermo[0];
                PublishLateFrameSignals(in fieldSnapshot, in scalar, in hudSnapshot, in rebaseSnapshot, in thermoSnapshot);
                SeedShipAnomalyShaderBridge.Publish(vault, in fieldSnapshot, in scalar);

                if ((diagnosticFlags & (SeedShipAnomalyFlags.BudgetExceeded | SeedShipAnomalyFlags.NonFinite)) != 0u)
                    TryDumpTelemetry(vault, telemetry, diagnosticFlags);
            }

            _telemetryCursor = (_telemetryCursor + 1) % SeedShipAnomalyConstants.TelemetryFrameCount;
            _jobScheduled = false;
            UnlockJobBuffers();
        }

        private void PublishLateFrameSignals(
            in AnomalyFieldDTO field,
            in AnomalyGlobalScalarsDTO globals,
            in MockHudSignal hud,
            in MockAupRebaseSignal rebase,
            in AnomalyThermoSourceDTO thermo)
        {
            AbsoluteUniversePosition epicenter = AbsoluteUniversePosition.FromAbsolutePosition(field.EpicenterAUP);
            SignalBus<MockHudSignal>.TryPush(in hud);
            if (rebase.Flags != 0u && rebase.ShiftFrameId == globals.LastRebaseFrame)
                SignalBus<MockAupRebaseSignal>.TryPush(in rebase);

            SignalBus<AnomalyProximitySignal>.TryPush(new AnomalyProximitySignal
            {
                SourceAup = epicenter,
                Proximity01 = globals.Corruption01,
                Interference01 = globals.RadarJam01,
                Frame = globals.Frame,
                SourceHash = SeedShipAnomalyConstants.SourceHash,
                Flags = (byte)(globals.Corruption01 > 0.001f ? 1 : 0)
            });

            if (globals.Corruption01 > 0.001f)
            {
                SignalBus<SystemGlitchSignal>.TryPush(new SystemGlitchSignal
                {
                    Frame = globals.Frame,
                    SourceId = SeedShipAnomalyConstants.SourceHash,
                    LocalHash = field.GlitchHash,
                    ExpectedHash = SeedShipAnomalyConstants.GlitchHash,
                    Intensity01 = globals.ShaderCorruption01,
                    DurationSeconds = 0.25f,
                    Reason = 48,
                    Flags = 1
                });

                SignalBus<TelemetryAnomalySignal>.TryPush(new TelemetryAnomalySignal
                {
                    SystemHash = SeedShipAnomalyConstants.SourceHash,
                    AnomalyHash = field.GlitchHash,
                    Scalar = globals.Corruption01,
                    Frame = globals.Frame,
                    Severity = (byte)math.clamp((int)math.round(globals.Corruption01 * 255f), 0, 255),
                    Flags = (byte)(globals.Flags & 0xFFu)
                });

                SignalBus<RadiationSourceSignal>.TryPush(new RadiationSourceSignal
                {
                    PositionAup = epicenter,
                    Intensity = thermo.Radiation01,
                    RadiusMeters = thermo.RadiusMeters,
                    SourceId = unchecked((int)SeedShipAnomalyConstants.SourceHash),
                    Operation = RadiationSourceSignal.OperationUpsert,
                    Flags = 1
                });

                SignalBus<RadiationDoseSignal>.TryPush(new RadiationDoseSignal
                {
                    PositionAup = epicenter,
                    Dose = globals.Radiation01 * 2.5f,
                    Intensity01 = globals.Radiation01,
                    SourceId = SeedShipAnomalyConstants.SourceHash,
                    DoseKind = 48,
                    Flags = 1
                });
            }
        }

        private void TryDumpTelemetry(IDataVault vault, NativeArray<AnomalyTelemetryEntry> telemetry, uint reasonFlags)
        {
            if (_dumpedBudgetBreach || !telemetry.IsCreated || telemetry.Length == 0)
                return;

            NativeArray<byte> dumpScratch = _dumpScratchHandle.Resolve(vault);
            if (!dumpScratch.IsCreated || dumpScratch.Length < 32)
                return;

            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyDumpScratch, OwnerSystem))
                return;

            try
            {
                _dumpedBudgetBreach = true;
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dumpScratch);
                Span<byte> scratch = new Span<byte>(ptr, math.min(DumpScratchBytes, dumpScratch.Length));
                scratch.Clear();
                BinaryPrimitives.WriteUInt64LittleEndian(scratch.Slice(0, 8), DumpMagic);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(8, 4), DumpVersion);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(12, 4), reasonFlags);
                BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(16, 4), telemetry.Length);
                BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(20, 4), _telemetryCursor);

                int offset = 32;
                for (int i = 0; i < telemetry.Length && offset + 64 <= scratch.Length; i++)
                {
                    AnomalyTelemetryEntry entry = telemetry[i];
                    WriteFloatLittleEndian(scratch.Slice(offset, 4), entry.CurrentCorruptionLevel);
                    BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(offset + 4, 4), entry.EntitiesAffected);
                    WriteFloatLittleEndian(scratch.Slice(offset + 8, 4), entry.AnomalyComputeTimeMs);
                    WriteFloatLittleEndian(scratch.Slice(offset + 12, 4), entry.GravityY);
                    WriteFloatLittleEndian(scratch.Slice(offset + 16, 4), entry.RadarJam01);
                    WriteFloatLittleEndian(scratch.Slice(offset + 20, 4), entry.HeatSource01);
                    WriteFloatLittleEndian(scratch.Slice(offset + 24, 4), entry.GlobalQualityWeight);
                    BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 28, 4), entry.Frame);
                    BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 32, 4), entry.Flags);
                    BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 36, 4), entry.StateHash);
                    WriteDoubleLittleEndian(scratch.Slice(offset + 40, 8), entry.EpicenterAUP.x);
                    WriteDoubleLittleEndian(scratch.Slice(offset + 48, 8), entry.EpicenterAUP.y);
                    WriteDoubleLittleEndian(scratch.Slice(offset + 56, 8), entry.EpicenterAUP.z);
                    offset += 64;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));
                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                stream.Write(scratch.Slice(0, math.min(offset, scratch.Length)));
            }
            catch (Exception)
            {
                _dumpedBudgetBreach = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyDumpScratch, OwnerSystem);
            }
        }

        private void ConsumeHackSignals(float dt)
        {
            ReadOnlySpan<CoreHackedSignal> signals = SignalBus<CoreHackedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                CoreHackedSignal signal = signals[i];
                if (signal.CodeHash == SeedShipAnomalyConstants.CoreHackAcceptedHash &&
                    signal.Validity01 >= 0.999f)
                {
                    _healingSecondsRemaining = 10f;
                }
            }

            if (_healingSecondsRemaining > 0f)
                _healingSecondsRemaining = math.max(0f, _healingSecondsRemaining - dt);
        }

        private double3 ResolvePlayerAup()
        {
            IPlayerRuntimeContext context = _playerContext;
            if (context != null &&
                context.IsInitialized &&
                context.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return snapshot.Aup.ToAbsoluteDouble3();
            }

            return new double3(seedShipAupX, seedShipAupY, seedShipAupZ);
        }

        private float ResolveGlobalQualityWeight(IDataVault vault, float fallback)
        {
            if (!_scalabilityHandle.IsCreated &&
                vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out VaultBufferHandle<ScalabilityStateDTO> scalability))
            {
                _scalabilityHandle = scalability;
            }

            NativeArray<ScalabilityStateDTO> state = _scalabilityHandle.IsCreated ? _scalabilityHandle.Resolve(vault) : default;
            if (state.IsCreated && state.Length > 0 && math.isfinite(state[0].GlobalQualityWeight))
                return math.saturate(state[0].GlobalQualityWeight);

            return math.saturate(math.isfinite(fallback) ? fallback : defaultGlobalQualityWeight);
        }

        private void MonitorCsvOverrides(IDataVault vault)
        {
            try
            {
                if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                    return;

                long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
                if (ticks == _csvLastWriteTicks)
                    return;

                _csvLastWriteTicks = ticks;
                if (!TryLockIoScratch(vault, out NativeArray<byte> scratch))
                    return;

                try
                {
                    int read = ReadColdBytes(_csvPath, CsvMaxBytes, scratch);
                    if (read > 0)
                    {
                        void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                        ParseCsvOverrides(vault, new ReadOnlySpan<byte>(ptr, read));
                    }
                }
                finally
                {
                    vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyIoScratch, OwnerSystem);
                }
            }
            catch (Exception)
            {
                _csvLastWriteTicks = 0L;
            }
        }

        private void ParseCsvOverrides(IDataVault vault, ReadOnlySpan<byte> bytes)
        {
            NativeArray<AnomalyTuningDTO> tuningArray = _tuningHandle.Resolve(vault);
            NativeArray<AnomalyFieldDTO> fieldArray = _fieldHandle.Resolve(vault);
            NativeArray<AnomalyCsvOverrideDTO> overrides = _csvOverrideHandle.Resolve(vault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0 ||
                !fieldArray.IsCreated || fieldArray.Length == 0 ||
                !overrides.IsCreated)
            {
                return;
            }

            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, OwnerSystem))
                return;

            bool fieldLocked = false;
            bool overrideLocked = false;
            try
            {
                fieldLocked = vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyField, OwnerSystem);
                overrideLocked = vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyCsvOverrides, OwnerSystem);
                AnomalyTuningDTO tuning = tuningArray[0];
                AnomalyFieldDTO field = fieldArray[0];
                int overrideIndex = 0;
                int lineStart = 0;
                uint frame = _simulationFrameCounter;

                for (int i = 0; i <= bytes.Length; i++)
                {
                    if (i < bytes.Length && bytes[i] != (byte)'\n')
                        continue;

                    ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, i - lineStart));
                    lineStart = i + 1;
                    if (line.Length == 0 || line[0] == (byte)'#')
                        continue;

                    int comma = IndexOf(line, (byte)',');
                    if (comma <= 0)
                        comma = IndexOf(line, (byte)'=');
                    if (comma <= 0)
                        continue;

                    ReadOnlySpan<byte> key = TrimAscii(line.Slice(0, comma));
                    ReadOnlySpan<byte> valueSpan = TrimAscii(line.Slice(comma + 1));
                    if (!TryParseAsciiFloat(valueSpan, out float value))
                        continue;

                    uint keyHash = HashLowerAscii(key);
                    ApplyCsvOverride(keyHash, value, ref tuning, ref field);
                    if (overrideLocked && overrideIndex < overrides.Length)
                    {
                        overrides[overrideIndex++] = new AnomalyCsvOverrideDTO
                        {
                            KeyHash = keyHash,
                            Value = value,
                            Frame = frame,
                            Flags = 1u
                        };
                    }
                }

                tuningArray[0] = SeedShipAnomalyMath.SanitizeTuning(tuning);
                if (fieldLocked)
                {
                    field.Radius = math.max(0f, field.Radius);
                    field.CorruptionLevel = math.saturate(field.CorruptionLevel);
                    fieldArray[0] = field;
                }
            }
            finally
            {
                if (overrideLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyCsvOverrides, OwnerSystem);
                if (fieldLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyField, OwnerSystem);
                vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, OwnerSystem);
            }
        }

        private static void ApplyCsvOverride(uint keyHash, float value, ref AnomalyTuningDTO tuning, ref AnomalyFieldDTO field)
        {
            if (keyHash == _MaxCorruptionRadiusHash)
            {
                tuning.MaxCorruptionRadius = value;
                field.Radius = value;
            }
            else if (keyHash == _GravityInversionStrengthHash)
            {
                tuning.GravityInversionStrength = value;
            }
            else if (keyHash == _PulseFrequencyHash)
            {
                tuning.PulseFrequency = value;
            }
            else if (keyHash == _GlitchIntensityHash)
            {
                tuning.GlitchIntensity = value;
            }
            else if (keyHash == _HeatEmissionHash)
            {
                tuning.HeatEmission = value;
            }
            else if (keyHash == _RadiationEmissionHash)
            {
                tuning.RadiationEmission = value;
            }
            else if (keyHash == _RadarJamIntensityHash)
            {
                tuning.RadarJamIntensity = value;
            }
            else if (keyHash == _BabelScrambleStrengthHash)
            {
                tuning.BabelScrambleStrength = value;
            }
            else if (keyHash == _GlobalQualityWeightHash)
            {
                tuning.GlobalQualityWeight = value;
            }
        }

        private void TryRegisterTicks()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredUpdate || !_registeredLateFrame || !_registeredSlowTick)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ClearCachedHandles()
        {
            _fieldHandle = default;
            _tuningHandle = default;
            _globalsHandle = default;
            _glitchHandle = default;
            _hudHandle = default;
            _leviathanHandle = default;
            _rebaseHandle = default;
            _thermoHandle = default;
            _telemetryHandle = default;
            _csvOverrideHandle = default;
            _ioScratchHandle = default;
            _dumpScratchHandle = default;
            _scalabilityHandle = default;
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<RadarJamSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeedShipAnomalyConstants.RadarJamLaneHash);
            SignalBus<RadarJamSignal>.EnsureInitialized();
            SignalBus<CoreHackedSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.CoreHackLaneHash);
            SignalBus<CoreHackedSignal>.EnsureInitialized();
            SignalBus<MockHudSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.MockHudLaneHash);
            SignalBus<MockHudSignal>.EnsureInitialized();
            SignalBus<MockAupRebaseSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.MockAupRebaseLaneHash);
            SignalBus<MockAupRebaseSignal>.EnsureInitialized();
            SignalBus<AnomalyProximitySignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
            SignalBus<TelemetryAnomalySignal>.EnsureInitialized();
            SignalBus<RadiationSourceSignal>.EnsureInitialized();
            SignalBus<RadiationDoseSignal>.EnsureInitialized();
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start <= end && IsCsvSpace(bytes[start]))
                start++;
            while (end >= start && (IsCsvSpace(bytes[end]) || bytes[end] == (byte)'\r'))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> bytes, byte target)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == target)
                    return i;
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCsvSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            int cursor = 0;
            while (cursor < bytes.Length && IsCsvSpace(bytes[cursor]))
                cursor++;

            float sign = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }
            else if (cursor < bytes.Length && bytes[cursor] == (byte)'+')
            {
                cursor++;
            }

            float whole = 0f;
            bool hasDigit = false;
            while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                hasDigit = true;
                whole = whole * 10f + (bytes[cursor] - (byte)'0');
                cursor++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10f + (bytes[cursor] - (byte)'0');
                    divisor *= 10f;
                    cursor++;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (whole + fraction * math.rcp(math.max(1f, divisor)));
            return math.isfinite(value);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint HashLowerAsciiString(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint ReadUInt32LittleEndian(NativeArray<byte> bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private static float ReadFloatLittleEndian(NativeArray<byte> bytes, int offset)
        {
            uint raw = ReadUInt32LittleEndian(bytes, offset);
            return math.asfloat(raw);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }
    }
}
