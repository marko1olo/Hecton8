using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay.AirlockPressurization;
using Hecton8.Power;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;

namespace Hecton8.Thermodynamics
{
    public sealed unsafe partial class AbyssalThermodynamicsSolver
    {
        private VaultGenerationHandle<ReactorStateDTO> _reactorStates;
        private VaultGenerationHandle<ReactorKinematicStateDTO> _reactorKinematics;
        private VaultGenerationHandle<int> _reactorCount;
        private VaultGenerationHandle<ReactorThermalTuningDTO> _reactorTuning;
        private VaultGenerationHandle<ReactorThermalTelemetryEntry> _reactorTelemetryRing;
        private VaultGenerationHandle<int> _reactorTelemetryCursor;
        private VaultGenerationHandle<ReactorThermalProfileDTO> _reactorProfiles;
        private VaultGenerationHandle<int> _reactorProfileCount;
        private VaultGenerationHandle<byte> _reactorCsvScratch;
        private VaultGenerationHandle<ReactorThermalScratchDTO> _reactorScratch;
        private VaultGenerationHandle<int> _reactorDumpLatch;
        private VaultGenerationHandle<BaseReactorStateDTO> _baseReactorStates;
        private VaultGenerationHandle<NuclearReactorThermalTuningDTO> _baseReactorTuning;
        private VaultGenerationHandle<ReactorPowerInjectionDTO> _baseReactorPowerLedger;
        private VaultGenerationHandle<NuclearReactorTelemetryEntry> _baseReactorTelemetryRing;
        private VaultGenerationHandle<int> _baseReactorTelemetryCursor;
        private VaultGenerationHandle<ReactorThermalVisualDTO> _baseReactorVisuals;
        private VaultGenerationHandle<int> _baseReactorDumpLatch;
        private VaultGenerationHandle<NuclearReactorProfileDTO> _baseReactorProfiles;
        private VaultGenerationHandle<int> _baseReactorProfileCount;
        private static readonly int ReactorThermalVisualMetaId = Shader.PropertyToID("_H8SubmarineReactorThermalMeta");
        private static readonly int ReactorThermalVisualPointId = Shader.PropertyToID("_H8SubmarineReactorThermalPoint");
        private static readonly int ReactorThermalStructuredBufferId = Shader.PropertyToID("_H8SubmarineReactorThermalStates");
        private static readonly int ReactorThermalStructuredMetaId = Shader.PropertyToID("_H8SubmarineReactorThermalStatesMeta");
        private bool _reactorBridgeInitialized;
        private float _lastReactorInjectionMicroseconds;
        private float _lastNuclearThermoMicroseconds;
        private float _reactorCadenceAccumulator;
        private string _reactorDumpPath;
        private string _baseReactorDumpPath;
        private IDataVault _reactorSharedGuardVault;
        private ulong _reactorSharedGuardMask;
        private int _reactorThermalVisualWriteIndex;
        private GraphicsBuffer _reactorThermalVisualBufferA;
        private GraphicsBuffer _reactorThermalVisualBufferB;

        private void EnsureReactorThermalVaultBuffers()
        {
            _reactorStates = Acquire<ReactorStateDTO>(BufferID.Shinobu337ReactorStates, ReactorThermalMath.MaxReactors);
            _reactorKinematics = Acquire<ReactorKinematicStateDTO>(BufferID.Shinobu337ReactorKinematics, ReactorThermalMath.MaxReactors);
            _reactorCount = Acquire<int>(BufferID.Shinobu337ReactorCount, 1);
            _reactorTuning = Acquire<ReactorThermalTuningDTO>(BufferID.Shinobu337ReactorTuning, 1);
            _reactorTelemetryRing = Acquire<ReactorThermalTelemetryEntry>(BufferID.Shinobu337ReactorTelemetryRing, ReactorThermalMath.TelemetryCapacity);
            _reactorTelemetryCursor = Acquire<int>(BufferID.Shinobu337ReactorTelemetryCursor, 1);
            _reactorProfiles = Acquire<ReactorThermalProfileDTO>(BufferID.Shinobu337ReactorProfiles, ReactorThermalMath.MaxProfiles);
            _reactorProfileCount = Acquire<int>(BufferID.Shinobu337ReactorProfileCount, 1);
            _reactorCsvScratch = Acquire<byte>(BufferID.Shinobu337ReactorCsvScratch, ReactorThermalMath.CsvScratchBytes);
            _reactorScratch = Acquire<ReactorThermalScratchDTO>(BufferID.Shinobu337ReactorScratch, ReactorThermalMath.MaxReactors);
            _reactorDumpLatch = Acquire<int>(BufferID.Shinobu337ReactorDumpLatch, 1);
            _baseReactorStates = Acquire<BaseReactorStateDTO>(BaseReactorThermalBufferIds.States, ReactorThermalMath.MaxReactors);
            _baseReactorTuning = Acquire<NuclearReactorThermalTuningDTO>(BaseReactorThermalBufferIds.Tuning, 1);
            _baseReactorPowerLedger = Acquire<ReactorPowerInjectionDTO>(BaseReactorThermalBufferIds.PowerLedger, ReactorThermalMath.MaxReactors);
            _baseReactorTelemetryRing = Acquire<NuclearReactorTelemetryEntry>(BaseReactorThermalBufferIds.TelemetryRing, ReactorThermalMath.TelemetryCapacity);
            _baseReactorTelemetryCursor = Acquire<int>(BaseReactorThermalBufferIds.TelemetryCursor, 1);
            _baseReactorVisuals = Acquire<ReactorThermalVisualDTO>(BaseReactorThermalBufferIds.Visuals, ReactorThermalMath.MaxReactors);
            _baseReactorDumpLatch = Acquire<int>(BaseReactorThermalBufferIds.DumpLatch, 1);
            _baseReactorProfiles = Acquire<NuclearReactorProfileDTO>(BaseReactorThermalBufferIds.Profiles, ReactorThermalMath.MaxProfiles);
            _baseReactorProfileCount = Acquire<int>(BaseReactorThermalBufferIds.ProfileCount, 1);

            IDataVault vault = _vault;
            if (vault == null ||
                !TryResolveArray(vault, in _reactorStates, ReactorThermalMath.MaxReactors, out NativeArray<ReactorStateDTO> stateArray) ||
                !TryResolveArray(vault, in _reactorKinematics, ReactorThermalMath.MaxReactors, out NativeArray<ReactorKinematicStateDTO> kinematicArray) ||
                !TryResolveArray(vault, in _reactorCount, 1, out NativeArray<int> countArray) ||
                !TryResolveArray(vault, in _reactorTuning, 1, out NativeArray<ReactorThermalTuningDTO> tuningArray) ||
                !TryResolveArray(vault, in _reactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<ReactorThermalTelemetryEntry> telemetryArray) ||
                !TryResolveArray(vault, in _reactorTelemetryCursor, 1, out NativeArray<int> cursorArray) ||
                !TryResolveArray(vault, in _reactorProfiles, ReactorThermalMath.MaxProfiles, out NativeArray<ReactorThermalProfileDTO> profileArray) ||
                !TryResolveArray(vault, in _reactorProfileCount, 1, out NativeArray<int> profileCountArray) ||
                !TryResolveArray(vault, in _reactorScratch, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalScratchDTO> scratchArray) ||
                !TryResolveArray(vault, in _reactorDumpLatch, 1, out NativeArray<int> dumpLatchArray) ||
                !TryResolveArray(vault, in _baseReactorStates, ReactorThermalMath.MaxReactors, out NativeArray<BaseReactorStateDTO> baseStateArray) ||
                !TryResolveArray(vault, in _baseReactorTuning, 1, out NativeArray<NuclearReactorThermalTuningDTO> baseTuningArray) ||
                !TryResolveArray(vault, in _baseReactorPowerLedger, ReactorThermalMath.MaxReactors, out NativeArray<ReactorPowerInjectionDTO> powerLedgerArray) ||
                !TryResolveArray(vault, in _baseReactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<NuclearReactorTelemetryEntry> nuclearTelemetryArray) ||
                !TryResolveArray(vault, in _baseReactorTelemetryCursor, 1, out NativeArray<int> nuclearCursorArray) ||
                !TryResolveArray(vault, in _baseReactorVisuals, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalVisualDTO> visualArray) ||
                !TryResolveArray(vault, in _baseReactorDumpLatch, 1, out NativeArray<int> nuclearDumpLatchArray) ||
                !TryResolveArray(vault, in _baseReactorProfiles, ReactorThermalMath.MaxProfiles, out NativeArray<NuclearReactorProfileDTO> nuclearProfileArray) ||
                !TryResolveArray(vault, in _baseReactorProfileCount, 1, out NativeArray<int> nuclearProfileCountArray))
            {
                throw new InvalidOperationException("SHINOBU_342 reactor thermal Vault pointer resolution failed.");
            }

            for (int i = 0; i < ReactorThermalMath.MaxReactors; i++)
            {
                stateArray[i] = default;
                kinematicArray[i] = default;
                scratchArray[i] = default;
                baseStateArray[i] = default;
                powerLedgerArray[i] = default;
                visualArray[i] = default;
            }

            for (int i = 0; i < ReactorThermalMath.TelemetryCapacity; i++)
            {
                telemetryArray[i] = default;
                nuclearTelemetryArray[i] = default;
            }

            for (int i = 0; i < ReactorThermalMath.MaxProfiles; i++)
            {
                profileArray[i] = default;
                nuclearProfileArray[i] = default;
            }

            countArray[0] = 0;
            cursorArray[0] = 0;
            profileCountArray[0] = 0;
            dumpLatchArray[0] = 0;
            tuningArray[0] = BuildDefaultReactorTuning();
            nuclearCursorArray[0] = 0;
            nuclearProfileCountArray[0] = 0;
            nuclearDumpLatchArray[0] = 0;
            baseTuningArray[0] = BuildDefaultNuclearReactorTuning();
            SeedDefaultReactorProfiles(profileArray, profileCountArray);
            SeedDefaultNuclearReactorProfiles(nuclearProfileArray, nuclearProfileCountArray);
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
            Directory.CreateDirectory(directory);
            _reactorDumpPath = Path.Combine(directory, "Dump_SHINOBU_342_legacy.bin");
            _baseReactorDumpPath = Path.Combine(directory, "Dump_SHINOBU_342.bin");
            TryLoadReactorProfilesCold();
            TryLoadNuclearReactorProfilesCold();

            SignalBus<ThermalStateChangedSignal>.EnsureInitialized();
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<BaseModuleCompromisedSignal>.Configure(
                BaseModuleCompromisedSignal.ExpectedCapacity,
                BaseModuleCompromisedSignal.MaxFrameSignals,
                BaseModuleCompromisedSignal.LowTierFrameSignals,
                BaseModuleCompromisedSignal.LaneHash);
            SignalBus<BaseModuleCompromisedSignal>.EnsureInitialized();
            SignalBus<RadiationSourceSignal>.EnsureInitialized();
            EnsureReactorThermalVisualBuffersCold();
            _reactorBridgeInitialized = true;
        }

        private ReactorThermalTuningDTO BuildDefaultReactorTuning()
        {
            float qualityWeight = ResolveVisualQualityWeight();
            ReactorThermalTuningDTO tuning = default;
            tuning.BaseDissipationRate = 0.085f;
            tuning.ForcedConvectionMultiplier = 0.08f;
            tuning.MaxConvectionMultiplier = 4.5f;
            tuning.CoreHeatCapacityJoulesPerCelsius = 1250000f;
            tuning.WaterDensityKgPerCubicMeter = 1027f;
            tuning.WaterHeatCapacityJoulesPerKgC = 3993f;
            tuning.SafeCoreTempCelsius = 760f;
            tuning.MeltdownCoreTempCelsius = 1850f;
            tuning.GridTemperatureClampCelsius = 2200f;
            tuning.HeatShimmerMinJoules = 40000f;
            tuning.GlobalQualityWeight = qualityWeight;
            tuning.MockReactorCount = 2;
            tuning.ThermalSignalStrideFrames = 8u;
            tuning.MeltdownSignalStrideFrames = 1u;
            tuning.Flags = 0u;
            tuning.Frame = 0u;
            tuning.SourceHash = ReactorThermalMath.SourceHash;
            tuning.DamageTypeHash = ReactorThermalMath.DamageTypeReactorMeltdown;
            tuning.MaxReactors = ReactorThermalMath.MaxReactors;
            tuning.MockPowerMW = 14f;
            tuning.MockCoreTempCelsius = 720f;
            tuning.MockThermalDissipationRate = 0.08f;
            tuning.VisualOverkillScalar = 1f;
            tuning.CellConvectionGain = 0.000018f;
            tuning.ProfileHash = ReactorThermalMath.ProfileHashDefault;
            return tuning;
        }

        private NuclearReactorThermalTuningDTO BuildDefaultNuclearReactorTuning()
        {
            float qualityWeight = ResolveVisualQualityWeight();
            NuclearReactorThermalTuningDTO tuning = default;
            tuning.BaseFissionHeatJoulesPerSecond = 42000000f;
            tuning.CoreHeatCapacityJoulesPerCelsius = 1250000f;
            tuning.TurbineThermalDrawWatts = 30000000f;
            tuning.LatentHeatJoulesPerLiter = 2256000f;
            tuning.AmbientCoolantTempCelsius = 18f;
            tuning.DryCoolantTempCelsius = 3200f;
            tuning.MeltdownCoreTempCelsius = 2500f;
            tuning.SafeCoreTempCelsius = 1100f;
            tuning.MaxBoilOffLitersPerSecond = 4200f;
            tuning.RadiationIntensityBase = 48f;
            tuning.RadiationRadiusMeters = 120f;
            tuning.GlobalQualityWeight = qualityWeight;
            tuning.MinTickIntervalSeconds = 0.016f;
            tuning.MaxTickIntervalSeconds = 0.2f;
            tuning.MockRunawayCount = 2;
            tuning.Frame = 0u;
            tuning.SourceHash = ReactorThermalMath.SourceHashShinobu342;
            tuning.DamageTypeHash = ReactorThermalMath.DamageTypeReactorMeltdown;
            tuning.MaxReactors = ReactorThermalMath.MaxReactors;
            tuning.ThermalLeakToGrid01 = 0.035f;
            tuning.CoolantLitersForNominalColdSink = 4000f;
            tuning.Flags = 0u;
            tuning.VisualOverkillScalar = 1f;
            tuning.FuelBurnPerMegawattSecond = 0.00000025f;
            tuning.ProfileHash = ReactorThermalMath.ProfileHashNuclearDefault;
            return tuning;
        }

        private static void SeedDefaultReactorProfiles(NativeArray<ReactorThermalProfileDTO> profiles, NativeArray<int> count)
        {
            ReactorThermalProfileDTO profile = default;
            profile.ProfileHash = ReactorThermalMath.ProfileHashDefault;
            profile.BaseDissipationRate = 0.085f;
            profile.CoreHeatCapacityJoulesPerCelsius = 1250000f;
            profile.SafeCoreTempCelsius = 760f;
            profile.MeltdownCoreTempCelsius = 1850f;
            profile.NominalPowerMW = 14f;
            profile.ForcedConvectionMultiplier = 0.08f;
            profile.Flags = 0u;
            profiles[0] = profile;
            count[0] = 1;
        }

        private static void SeedDefaultNuclearReactorProfiles(NativeArray<NuclearReactorProfileDTO> profiles, NativeArray<int> count)
        {
            NuclearReactorProfileDTO profile = default;
            profile.ProfileHash = ReactorThermalMath.ProfileHashNuclearDefault;
            profile.BaseFissionHeatJoulesPerSecond = 42000000f;
            profile.CoreHeatCapacityJoulesPerCelsius = 1250000f;
            profile.TurbineThermalDrawWatts = 30000000f;
            profile.LatentHeatJoulesPerLiter = 2256000f;
            profile.SafeCoreTempCelsius = 1100f;
            profile.MeltdownCoreTempCelsius = 2500f;
            profile.RadiationRadiusMeters = 120f;
            profile.MaxBoilOffLitersPerSecond = 4200f;
            profile.FuelBurnPerMegawattSecond = 0.00000025f;
            profile.Flags = 0u;
            profiles[0] = profile;
            count[0] = 1;
        }

        private JobHandle ScheduleReactorThermalLink(
            IDataVault vault,
            ThermalCellDTO* front,
            ThermalCellDTO* injection,
            in ThermalGridTuningDTO gridTuning,
            JobHandle dependency)
        {
            if (!_reactorBridgeInitialized ||
                vault == null ||
                front == null ||
                injection == null ||
                !TryResolveArray(vault, in _reactorStates, ReactorThermalMath.MaxReactors, out NativeArray<ReactorStateDTO> reactorArray) ||
                !TryResolveArray(vault, in _reactorKinematics, ReactorThermalMath.MaxReactors, out NativeArray<ReactorKinematicStateDTO> kinematicArray) ||
                !TryResolveArray(vault, in _reactorCount, 1, out NativeArray<int> countArray) ||
                !TryResolveArray(vault, in _reactorTuning, 1, out NativeArray<ReactorThermalTuningDTO> tuningArray) ||
                !TryResolveArray(vault, in _reactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<ReactorThermalTelemetryEntry> telemetryArray) ||
                !TryResolveArray(vault, in _reactorTelemetryCursor, 1, out NativeArray<int> cursorArray) ||
                !TryResolveArray(vault, in _reactorScratch, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalScratchDTO> scratchArray) ||
                !TryResolveArray(vault, in _baseReactorStates, ReactorThermalMath.MaxReactors, out NativeArray<BaseReactorStateDTO> baseReactorArray) ||
                !TryResolveArray(vault, in _baseReactorTuning, 1, out NativeArray<NuclearReactorThermalTuningDTO> nuclearTuningArray) ||
                !TryResolveArray(vault, in _baseReactorPowerLedger, ReactorThermalMath.MaxReactors, out NativeArray<ReactorPowerInjectionDTO> powerLedgerArray) ||
                !TryResolveArray(vault, in _baseReactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<NuclearReactorTelemetryEntry> nuclearTelemetryArray) ||
                !TryResolveArray(vault, in _baseReactorTelemetryCursor, 1, out NativeArray<int> nuclearCursorArray) ||
                !TryResolveArray(vault, in _baseReactorVisuals, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalVisualDTO> visualArray))
            {
                return dependency;
            }

            ReactorStateDTO* reactors = (ReactorStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(reactorArray);
            ReactorKinematicStateDTO* kinematics = (ReactorKinematicStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(kinematicArray);
            int* count = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(countArray);
            ReactorThermalTuningDTO* tuningPtr = (ReactorThermalTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            ReactorThermalTelemetryEntry* telemetry = (ReactorThermalTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryArray);
            int* cursor = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(cursorArray);
            ReactorThermalScratchDTO* scratch = (ReactorThermalScratchDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchArray);
            BaseReactorStateDTO* baseReactors = (BaseReactorStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(baseReactorArray);
            NuclearReactorThermalTuningDTO* nuclearTuningPtr = (NuclearReactorThermalTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nuclearTuningArray);
            ReactorPowerInjectionDTO* powerLedger = (ReactorPowerInjectionDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(powerLedgerArray);
            NuclearReactorTelemetryEntry* nuclearTelemetry = (NuclearReactorTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(nuclearTelemetryArray);
            int* nuclearCursor = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(nuclearCursorArray);
            ReactorThermalVisualDTO* visuals = (ReactorThermalVisualDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(visualArray);

            ReactorThermalTuningDTO reactorTuning = SanitizeReactorTuning(*tuningPtr, gridTuning.GlobalQualityWeight, gridTuning.Frame);
            *tuningPtr = reactorTuning;
            NuclearReactorThermalTuningDTO nuclearTuning = SanitizeNuclearReactorTuning(*nuclearTuningPtr, gridTuning.GlobalQualityWeight, gridTuning.Frame);
            *nuclearTuningPtr = nuclearTuning;
            int resolvedCount = math.clamp(*count, 0, ReactorThermalMath.MaxReactors);
            if (resolvedCount == 0)
            {
                GenerateMockReactorLoadJob mockJob;
                mockJob.Reactors = reactors;
                mockJob.Kinematics = kinematics;
                mockJob.ReactorCount = count;
                mockJob.Tuning = reactorTuning;
                mockJob.GridTuning = gridTuning;
                mockJob.Frame = gridTuning.Frame;
                dependency = mockJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);
                resolvedCount = math.clamp(reactorTuning.MockReactorCount, 1, ReactorThermalMath.MaxReactors);
            }

            float nuclearInterval = ResolveNuclearReactorTickInterval(nuclearTuning);
            float cadenceDeltaTime = math.clamp(
                ReactorThermalMath.FiniteOr(gridTuning.SimulationTickDeltaSeconds, 1f / 60f),
                0.0001f,
                nuclearTuning.MaxTickIntervalSeconds);
            _reactorCadenceAccumulator = math.min(_reactorCadenceAccumulator + cadenceDeltaTime, nuclearTuning.MaxTickIntervalSeconds * 4f);
            if (_reactorCadenceAccumulator >= nuclearInterval)
            {
                float nuclearDeltaTime = _reactorCadenceAccumulator;
                _reactorCadenceAccumulator = 0f;
                if (*count <= 0)
                {
                    GenerateMockThermalRunawayJob runawayJob;
                    runawayJob.Reactors = baseReactors;
                    runawayJob.Kinematics = kinematics;
                    runawayJob.ReactorCount = count;
                    runawayJob.Tuning = nuclearTuning;
                    runawayJob.GridTuning = gridTuning;
                    runawayJob.Frame = gridTuning.Frame;
                    dependency = runawayJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);
                }

                HydrateBaseReactorFromLegacyJob hydrateJob;
                hydrateJob.BaseReactors = baseReactors;
                hydrateJob.LegacyReactors = reactors;
                hydrateJob.ReactorCount = resolvedCount;
                hydrateJob.ReactorCapacity = ReactorThermalMath.MaxReactors;
                dependency = hydrateJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);

                EvaluateFissionReactionJob fissionJob;
                fissionJob.Reactors = baseReactors;
                fissionJob.Tuning = nuclearTuning;
                fissionJob.ReactorCount = resolvedCount;
                fissionJob.ReactorCapacity = ReactorThermalMath.MaxReactors;
                fissionJob.DeltaTime = nuclearDeltaTime;
                dependency = fissionJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);

                PowerNodeDTO* powerNodes;
                int powerNodeCount;
                FluidCompartmentDTO* fluidCompartments;
                int fluidCompartmentCount;
                AirlockStateDTO* airlocks;
                int airlockCount;
                ReleaseReactorSharedLocks();
                ResolveOptionalReactorIntegrationPointers(
                    out powerNodes,
                    out powerNodeCount,
                    out fluidCompartments,
                    out fluidCompartmentCount,
                    out airlocks,
                    out airlockCount);

                CalculateThermoelectricPowerJob thermoJob;
                thermoJob.Reactors = baseReactors;
                thermoJob.LegacyReactors = reactors;
                thermoJob.Kinematics = kinematics;
                thermoJob.PowerNodes = powerNodes;
                thermoJob.FluidCompartments = fluidCompartments;
                thermoJob.Airlocks = airlocks;
                thermoJob.PowerLedger = powerLedger;
                thermoJob.Visuals = visuals;
                thermoJob.Tuning = nuclearTuning;
                thermoJob.GridTuning = gridTuning;
                thermoJob.ReactorCount = resolvedCount;
                thermoJob.ReactorCapacity = ReactorThermalMath.MaxReactors;
                thermoJob.PowerNodeCount = powerNodeCount;
                thermoJob.FluidCompartmentCount = fluidCompartmentCount;
                thermoJob.AirlockCount = airlockCount;
                thermoJob.DeltaTime = nuclearDeltaTime;
                thermoJob.Frame = gridTuning.Frame;
                long nuclearScheduleStart = System.Diagnostics.Stopwatch.GetTimestamp();
                bool thermoJobScheduled = false;
                try
                {
                    dependency = thermoJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);
                    thermoJobScheduled = true;
                }
                finally
                {
                    if (!thermoJobScheduled)
                        ReleaseReactorSharedLocks();
                }

                long nuclearScheduleEnd = System.Diagnostics.Stopwatch.GetTimestamp();
                _lastNuclearThermoMicroseconds = (float)((nuclearScheduleEnd - nuclearScheduleStart) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

                PublishNuclearReactorMeltdownSignalsJob publishJob;
                publishJob.Reactors = baseReactors;
                publishJob.PowerLedger = powerLedger;
                publishJob.Visuals = visuals;
                publishJob.BaseModuleWriter = SignalBus<BaseModuleCompromisedSignal>.ParallelWriter;
                publishJob.BaseModuleWriterBudget = SignalBus<BaseModuleCompromisedSignal>.ParallelWriterBudget;
                publishJob.RadiationWriter = SignalBus<RadiationSourceSignal>.ParallelWriter;
                publishJob.RadiationWriterBudget = SignalBus<RadiationSourceSignal>.ParallelWriterBudget;
                publishJob.DamageWriter = SignalBus<CombatDamageSignal>.ParallelWriter;
                publishJob.DamageWriterBudget = SignalBus<CombatDamageSignal>.ParallelWriterBudget;
                publishJob.Tuning = nuclearTuning;
                publishJob.GridTuning = gridTuning;
                publishJob.ReactorCount = resolvedCount;
                publishJob.ReactorCapacity = ReactorThermalMath.MaxReactors;
                publishJob.Frame = gridTuning.Frame;
                dependency = publishJob.Schedule(dependency);

                NuclearReactorTelemetryRecorderJob nuclearTelemetryJob;
                nuclearTelemetryJob.Reactors = baseReactors;
                nuclearTelemetryJob.Kinematics = kinematics;
                nuclearTelemetryJob.PowerLedger = powerLedger;
                nuclearTelemetryJob.Visuals = visuals;
                nuclearTelemetryJob.Ring = nuclearTelemetry;
                nuclearTelemetryJob.ReactorCount = count;
                nuclearTelemetryJob.Cursor = nuclearCursor;
                nuclearTelemetryJob.Capacity = ReactorThermalMath.MaxReactors;
                nuclearTelemetryJob.Frame = gridTuning.Frame;
                nuclearTelemetryJob.LastExecutionMicroseconds = _lastNuclearThermoMicroseconds;
                dependency = nuclearTelemetryJob.Schedule(dependency);
            }

            InjectReactorHeatJob injectJob;
            injectJob.Reactors = reactors;
            injectJob.FallbackKinematics = kinematics;
            injectJob.Front = front;
            injectJob.Injection = injection;
            injectJob.Scratch = scratch;
            injectJob.ThermalWriter = SignalBus<ThermalStateChangedSignal>.ParallelWriter;
            injectJob.ThermalWriterBudget = SignalBus<ThermalStateChangedSignal>.ParallelWriterBudget;
            injectJob.DamageWriter = SignalBus<CombatDamageSignal>.ParallelWriter;
            injectJob.DamageWriterBudget = SignalBus<CombatDamageSignal>.ParallelWriterBudget;
            injectJob.Tuning = reactorTuning;
            injectJob.GridTuning = gridTuning;
            injectJob.ReactorCount = resolvedCount;
            injectJob.ReactorCapacity = ReactorThermalMath.MaxReactors;
            injectJob.DeltaTime = gridTuning.SimulationTickDeltaSeconds;
            injectJob.Frame = gridTuning.Frame;
            long injectScheduleStart = System.Diagnostics.Stopwatch.GetTimestamp();
            dependency = injectJob.Schedule(ReactorThermalMath.MaxReactors, 4, dependency);
            long injectScheduleEnd = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastReactorInjectionMicroseconds = (float)((injectScheduleEnd - injectScheduleStart) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

            ReactorTelemetryRecorderJob telemetryJob;
            telemetryJob.Reactors = reactors;
            telemetryJob.Kinematics = kinematics;
            telemetryJob.Scratch = scratch;
            telemetryJob.Ring = telemetry;
            telemetryJob.ReactorCount = count;
            telemetryJob.Cursor = cursor;
            telemetryJob.Capacity = ReactorThermalMath.MaxReactors;
            telemetryJob.Frame = gridTuning.Frame;
            telemetryJob.LastInjectionMicroseconds = _lastReactorInjectionMicroseconds;
            return telemetryJob.Schedule(dependency);
        }

        private static ReactorThermalTuningDTO SanitizeReactorTuning(ReactorThermalTuningDTO tuning, float gridQuality, uint frame)
        {
            tuning.BaseDissipationRate = math.max(0f, ReactorThermalMath.FiniteOr(tuning.BaseDissipationRate, 0.085f));
            tuning.ForcedConvectionMultiplier = math.max(0f, ReactorThermalMath.FiniteOr(tuning.ForcedConvectionMultiplier, 0.08f));
            tuning.MaxConvectionMultiplier = math.max(1f, ReactorThermalMath.FiniteOr(tuning.MaxConvectionMultiplier, 4.5f));
            tuning.CoreHeatCapacityJoulesPerCelsius = math.max(1f, ReactorThermalMath.FiniteOr(tuning.CoreHeatCapacityJoulesPerCelsius, 1250000f));
            tuning.WaterDensityKgPerCubicMeter = math.max(1f, ReactorThermalMath.FiniteOr(tuning.WaterDensityKgPerCubicMeter, 1027f));
            tuning.WaterHeatCapacityJoulesPerKgC = math.max(1f, ReactorThermalMath.FiniteOr(tuning.WaterHeatCapacityJoulesPerKgC, 3993f));
            tuning.SafeCoreTempCelsius = math.max(1f, ReactorThermalMath.FiniteOr(tuning.SafeCoreTempCelsius, 760f));
            tuning.MeltdownCoreTempCelsius = math.max(tuning.SafeCoreTempCelsius + 1f, ReactorThermalMath.FiniteOr(tuning.MeltdownCoreTempCelsius, 1850f));
            tuning.GridTemperatureClampCelsius = math.max(1f, ReactorThermalMath.FiniteOr(tuning.GridTemperatureClampCelsius, 2200f));
            tuning.HeatShimmerMinJoules = math.max(0f, ReactorThermalMath.FiniteOr(tuning.HeatShimmerMinJoules, 40000f));
            tuning.GlobalQualityWeight = math.saturate(ReactorThermalMath.FiniteOr(gridQuality, tuning.GlobalQualityWeight));
            tuning.MockReactorCount = math.clamp(tuning.MockReactorCount, 1, ReactorThermalMath.MaxReactors);
            tuning.ThermalSignalStrideFrames = tuning.ThermalSignalStrideFrames == 0u ? 1u : tuning.ThermalSignalStrideFrames;
            tuning.MeltdownSignalStrideFrames = tuning.MeltdownSignalStrideFrames == 0u ? 1u : tuning.MeltdownSignalStrideFrames;
            tuning.Frame = frame;
            tuning.SourceHash = tuning.SourceHash != 0u ? tuning.SourceHash : ReactorThermalMath.SourceHash;
            tuning.DamageTypeHash = tuning.DamageTypeHash != 0u ? tuning.DamageTypeHash : ReactorThermalMath.DamageTypeReactorMeltdown;
            tuning.MaxReactors = ReactorThermalMath.MaxReactors;
            tuning.MockPowerMW = math.max(0.1f, ReactorThermalMath.FiniteOr(tuning.MockPowerMW, 14f));
            tuning.MockCoreTempCelsius = math.max(300f, ReactorThermalMath.FiniteOr(tuning.MockCoreTempCelsius, 720f));
            tuning.MockThermalDissipationRate = math.max(0.0001f, ReactorThermalMath.FiniteOr(tuning.MockThermalDissipationRate, 0.08f));
            tuning.VisualOverkillScalar = math.max(0f, ReactorThermalMath.FiniteOr(tuning.VisualOverkillScalar, 1f));
            tuning.CellConvectionGain = math.max(0f, ReactorThermalMath.FiniteOr(tuning.CellConvectionGain, 0.000018f));
            tuning.ProfileHash = tuning.ProfileHash != 0u ? tuning.ProfileHash : ReactorThermalMath.ProfileHashDefault;
            return tuning;
        }

        private static NuclearReactorThermalTuningDTO SanitizeNuclearReactorTuning(NuclearReactorThermalTuningDTO tuning, float gridQuality, uint frame)
        {
            tuning.BaseFissionHeatJoulesPerSecond = math.max(0f, ReactorThermalMath.FiniteOr(tuning.BaseFissionHeatJoulesPerSecond, 42000000f));
            tuning.CoreHeatCapacityJoulesPerCelsius = math.max(1f, ReactorThermalMath.FiniteOr(tuning.CoreHeatCapacityJoulesPerCelsius, 1250000f));
            tuning.TurbineThermalDrawWatts = math.max(0f, ReactorThermalMath.FiniteOr(tuning.TurbineThermalDrawWatts, 30000000f));
            tuning.LatentHeatJoulesPerLiter = math.max(1f, ReactorThermalMath.FiniteOr(tuning.LatentHeatJoulesPerLiter, 2256000f));
            tuning.AmbientCoolantTempCelsius = ReactorThermalMath.FiniteOr(tuning.AmbientCoolantTempCelsius, 18f);
            tuning.DryCoolantTempCelsius = math.max(tuning.AmbientCoolantTempCelsius + 1f, ReactorThermalMath.FiniteOr(tuning.DryCoolantTempCelsius, 3200f));
            tuning.SafeCoreTempCelsius = math.max(100f, ReactorThermalMath.FiniteOr(tuning.SafeCoreTempCelsius, 1100f));
            tuning.MeltdownCoreTempCelsius = math.max(tuning.SafeCoreTempCelsius + 1f, ReactorThermalMath.FiniteOr(tuning.MeltdownCoreTempCelsius, 2500f));
            tuning.MaxBoilOffLitersPerSecond = math.max(0f, ReactorThermalMath.FiniteOr(tuning.MaxBoilOffLitersPerSecond, 4200f));
            tuning.RadiationIntensityBase = math.max(0f, ReactorThermalMath.FiniteOr(tuning.RadiationIntensityBase, 48f));
            tuning.RadiationRadiusMeters = math.max(1f, ReactorThermalMath.FiniteOr(tuning.RadiationRadiusMeters, 120f));
            tuning.GlobalQualityWeight = math.saturate(ReactorThermalMath.FiniteOr(gridQuality, tuning.GlobalQualityWeight));
            tuning.MinTickIntervalSeconds = math.clamp(ReactorThermalMath.FiniteOr(tuning.MinTickIntervalSeconds, 0.016f), 0.001f, 0.25f);
            tuning.MaxTickIntervalSeconds = math.max(tuning.MinTickIntervalSeconds, ReactorThermalMath.FiniteOr(tuning.MaxTickIntervalSeconds, 0.2f));
            tuning.MockRunawayCount = math.clamp(tuning.MockRunawayCount, 1, ReactorThermalMath.MaxReactors);
            tuning.Frame = frame;
            tuning.SourceHash = tuning.SourceHash != 0u ? tuning.SourceHash : ReactorThermalMath.SourceHashShinobu342;
            tuning.DamageTypeHash = tuning.DamageTypeHash != 0u ? tuning.DamageTypeHash : ReactorThermalMath.DamageTypeReactorMeltdown;
            tuning.MaxReactors = ReactorThermalMath.MaxReactors;
            tuning.ThermalLeakToGrid01 = math.clamp(ReactorThermalMath.FiniteOr(tuning.ThermalLeakToGrid01, 0.035f), 0f, 1f);
            tuning.CoolantLitersForNominalColdSink = math.max(1f, ReactorThermalMath.FiniteOr(tuning.CoolantLitersForNominalColdSink, 4000f));
            tuning.VisualOverkillScalar = math.max(0f, ReactorThermalMath.FiniteOr(tuning.VisualOverkillScalar, 1f));
            tuning.FuelBurnPerMegawattSecond = math.max(0f, ReactorThermalMath.FiniteOr(tuning.FuelBurnPerMegawattSecond, 0.00000025f));
            tuning.ProfileHash = tuning.ProfileHash != 0u ? tuning.ProfileHash : ReactorThermalMath.ProfileHashNuclearDefault;
            return tuning;
        }

        private static float ResolveNuclearReactorTickInterval(in NuclearReactorThermalTuningDTO tuning)
        {
            float quality = math.saturate(ReactorThermalMath.FiniteOr(tuning.GlobalQualityWeight, 1f));
            return math.lerp(tuning.MaxTickIntervalSeconds, tuning.MinTickIntervalSeconds, quality);
        }

        private unsafe void ResolveOptionalReactorIntegrationPointers(
            out PowerNodeDTO* powerNodes,
            out int powerNodeCount,
            out FluidCompartmentDTO* fluidCompartments,
            out int fluidCompartmentCount,
            out AirlockStateDTO* airlocks,
            out int airlockCount)
        {
            powerNodes = null;
            powerNodeCount = 0;
            fluidCompartments = null;
            fluidCompartmentCount = 0;
            airlocks = null;
            airlockCount = 0;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            bool hasPower = TryGetOptionalSharedHandle(
                vault,
                PowerGridBufferIds.Nodes,
                SystemID.Power,
                out VaultGenerationHandle<PowerNodeDTO> powerHandle);
            bool hasFluid = TryGetOptionalSharedHandle(
                vault,
                BufferID.ShinobuFluidCompartmentBack,
                SystemID.Fluid,
                out VaultGenerationHandle<FluidCompartmentDTO> fluidHandle);
            bool hasAirlock = TryGetOptionalSharedHandle(
                vault,
                AirlockPressurizationBufferIds.AirlockStates,
                SystemID.HabitatAtmosphere,
                out VaultGenerationHandle<AirlockStateDTO> airlockHandle);

            ulong guardMask = 0UL;
            if (hasPower)
                guardMask |= ReactorSharedMutationGuardBit(PowerGridBufferIds.Nodes);
            if (hasFluid)
                guardMask |= ReactorSharedMutationGuardBit(BufferID.ShinobuFluidCompartmentBack);
            if (hasAirlock)
                guardMask |= ReactorSharedMutationGuardBit(AirlockPressurizationBufferIds.AirlockStates);

            if (guardMask == 0UL ||
                !vault.TryAcquireMutationGuard(guardMask))
            {
                return;
            }

            _reactorSharedGuardVault = vault;
            _reactorSharedGuardMask = guardMask;
            if (hasPower &&
                TryResolveOptionalSharedBuffer(vault, in powerHandle, out NativeArray<PowerNodeDTO> powerArray))
            {
                powerNodes = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(powerArray);
                powerNodeCount = powerArray.Length;
            }

            if (hasFluid &&
                TryResolveOptionalSharedBuffer(vault, in fluidHandle, out NativeArray<FluidCompartmentDTO> fluidArray))
            {
                fluidCompartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidArray);
                fluidCompartmentCount = fluidArray.Length;
            }

            if (hasAirlock &&
                TryResolveOptionalSharedBuffer(vault, in airlockHandle, out NativeArray<AirlockStateDTO> airlockArray))
            {
                airlocks = (AirlockStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(airlockArray);
                airlockCount = airlockArray.Length;
            }

            if (powerNodes == null && fluidCompartments == null && airlocks == null)
                ReleaseReactorSharedLocks();
        }

        private static bool TryGetOptionalSharedHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            SystemID ownerSystem,
            out VaultGenerationHandle<T> handle)
            where T : struct
        {
            handle = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                   handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)ownerSystem &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveOptionalSharedBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private void ReleaseReactorSharedLocks()
        {
            ulong guardMask = _reactorSharedGuardMask;
            if (guardMask == 0UL)
                return;

            IDataVault vault = _reactorSharedGuardVault ?? _vault;
            _reactorSharedGuardVault = null;
            _reactorSharedGuardMask = 0UL;
            vault?.ReleaseMutationGuard(guardMask);
        }

        private static ulong ReactorSharedMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        public bool TryReadReactorTuning(out ReactorThermalTuningDTO tuning)
        {
            tuning = default;
            if (!_nativeReady || !_reactorBridgeInitialized)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !TryReadArray(vault, in _reactorTuning, 1, out NativeArray<ReactorThermalTuningDTO> tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TryWriteReactorTuning(ReactorThermalTuningDTO tuning)
        {
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireWriteLock(in _reactorTuning, SystemID.CoreDiagnostics, out NativeArray<ReactorThermalTuningDTO> tuningArray))
                return false;

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length < 1)
                    return false;

                float quality = TryReadTuning(out ThermalGridTuningDTO gridTuning)
                    ? gridTuning.GlobalQualityWeight
                    : ResolveVisualQualityWeight();
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<ReactorThermalTuningDTO>(ptr) = SanitizeReactorTuning(tuning, quality, _frame);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _reactorTuning, SystemID.CoreDiagnostics);
            }
        }

        public bool TryReadNuclearReactorTuning(out NuclearReactorThermalTuningDTO tuning)
        {
            tuning = default;
            if (!_nativeReady || !_reactorBridgeInitialized)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !TryReadArray(vault, in _baseReactorTuning, 1, out NativeArray<NuclearReactorThermalTuningDTO> tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TryWriteNuclearReactorTuning(NuclearReactorThermalTuningDTO tuning)
        {
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireWriteLock(in _baseReactorTuning, SystemID.CoreDiagnostics, out NativeArray<NuclearReactorThermalTuningDTO> tuningArray))
                return false;

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length < 1)
                    return false;

                float quality = TryReadTuning(out ThermalGridTuningDTO gridTuning)
                    ? gridTuning.GlobalQualityWeight
                    : ResolveVisualQualityWeight();
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<NuclearReactorThermalTuningDTO>(ptr) = SanitizeNuclearReactorTuning(tuning, quality, _frame);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _baseReactorTuning, SystemID.CoreDiagnostics);
            }
        }

        public bool TryReadReactorTelemetry(int offsetFromLatest, out ReactorThermalTelemetryEntry entry)
        {
            entry = default;
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !TryReadArray(vault, in _reactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<ReactorThermalTelemetryEntry> ring))
                return false;

            if (_frame == 0u)
                return false;

            uint offset = (uint)math.max(0, offsetFromLatest);
            if (offset > _frame)
                return false;

            uint frame = _frame - offset;
            int index = (int)(frame % ReactorThermalMath.TelemetryCapacity);
            entry = ring[index];
            return entry.Frame == frame;
        }

        public bool TryReadNuclearReactorTelemetry(int offsetFromLatest, out NuclearReactorTelemetryEntry entry)
        {
            entry = default;
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _baseReactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<NuclearReactorTelemetryEntry> ring) ||
                !TryReadArray(vault, in _baseReactorTelemetryCursor, 1, out NativeArray<int> cursorArray))
            {
                return false;
            }

            int cursor = cursorArray[0];
            if (cursor <= 0)
                return false;

            int offset = math.max(0, offsetFromLatest);
            int frame = cursor - offset;
            if (frame <= 0 || offset >= ReactorThermalMath.TelemetryCapacity)
                return false;

            int index = frame % ReactorThermalMath.TelemetryCapacity;
            entry = ring[index];
            return entry.Frame == (uint)frame;
        }

        public bool TryGetReactorDebugReadback(
            out NativeArray<ReactorStateDTO>.ReadOnly reactors,
            out NativeArray<ReactorKinematicStateDTO>.ReadOnly kinematics,
            out int count,
            out ThermalGridTuningDTO gridTuning)
        {
            reactors = default;
            kinematics = default;
            count = 0;
            gridTuning = default;
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _reactorStates, ReactorThermalMath.MaxReactors, out NativeArray<ReactorStateDTO> reactorArray) ||
                !TryReadArray(vault, in _reactorKinematics, ReactorThermalMath.MaxReactors, out NativeArray<ReactorKinematicStateDTO> kinematicArray) ||
                !TryReadArray(vault, in _reactorCount, 1, out NativeArray<int> countArray) ||
                !TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
            {
                return false;
            }

            reactors = reactorArray.AsReadOnly();
            kinematics = kinematicArray.AsReadOnly();
            count = math.clamp(countArray[0], 0, ReactorThermalMath.MaxReactors);
            gridTuning = tuningArray[0];
            return true;
        }

        public bool TryGetNuclearReactorDebugReadback(
            out NativeArray<BaseReactorStateDTO>.ReadOnly reactors,
            out NativeArray<ReactorKinematicStateDTO>.ReadOnly kinematics,
            out NativeArray<ReactorThermalVisualDTO>.ReadOnly visuals,
            out int count,
            out ThermalGridTuningDTO gridTuning,
            out NuclearReactorThermalTuningDTO reactorTuning)
        {
            reactors = default;
            kinematics = default;
            visuals = default;
            count = 0;
            gridTuning = default;
            reactorTuning = default;
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _baseReactorStates, ReactorThermalMath.MaxReactors, out NativeArray<BaseReactorStateDTO> reactorArray) ||
                !TryReadArray(vault, in _reactorKinematics, ReactorThermalMath.MaxReactors, out NativeArray<ReactorKinematicStateDTO> kinematicArray) ||
                !TryReadArray(vault, in _baseReactorVisuals, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalVisualDTO> visualArray) ||
                !TryReadArray(vault, in _reactorCount, 1, out NativeArray<int> countArray) ||
                !TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray) ||
                !TryReadArray(vault, in _baseReactorTuning, 1, out NativeArray<NuclearReactorThermalTuningDTO> reactorTuningArray))
            {
                return false;
            }

            reactors = reactorArray.AsReadOnly();
            kinematics = kinematicArray.AsReadOnly();
            visuals = visualArray.AsReadOnly();
            count = math.clamp(countArray[0], 0, ReactorThermalMath.MaxReactors);
            gridTuning = tuningArray[0];
            reactorTuning = reactorTuningArray[0];
            return true;
        }

        private void InspectReactorTelemetryAndDumpIfFaulted()
        {
            InspectNuclearReactorTelemetryAndDumpIfFaulted();

            if (!TryReadReactorTelemetry(0, out ReactorThermalTelemetryEntry entry))
                return;

            IDataVault vault = _vault;
            if (vault == null || !TryResolveArray(vault, in _reactorDumpLatch, 1, out NativeArray<int> latchArray))
                return;

            int* latch = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(latchArray);
            const uint faultMask = ReactorThermalMath.TelemetryFlagNonFinite | ReactorThermalMath.TelemetryFlagCostOverBudget;
            uint faultKey = entry.Flags & faultMask;
            if (faultKey == 0u)
            {
                *latch = 0;
                return;
            }

            int key = unchecked((int)faultKey);
            if (*latch == key)
                return;

            DumpReactorBlackBox();
            *latch = key;
        }

        private void InspectNuclearReactorTelemetryAndDumpIfFaulted()
        {
            if (!TryReadNuclearReactorTelemetry(0, out NuclearReactorTelemetryEntry entry))
                return;

            IDataVault vault = _vault;
            if (vault == null || !TryResolveArray(vault, in _baseReactorDumpLatch, 1, out NativeArray<int> latchArray))
                return;

            int* latch = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(latchArray);
            const uint faultMask =
                ReactorThermalMath.TelemetryFlagNonFinite |
                ReactorThermalMath.TelemetryFlagCostOverBudget |
                ReactorThermalMath.TelemetryFlagMeltdown |
                ReactorThermalMath.TelemetryFlagAtomicAbort;
            uint faultKey = entry.Flags & faultMask;
            if (faultKey == 0u)
            {
                *latch = 0;
                return;
            }

            int key = unchecked((int)faultKey);
            if (*latch == key)
                return;

            DumpNuclearReactorBlackBox();
            *latch = key;
        }

        private void DumpReactorBlackBox()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _reactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<ReactorThermalTelemetryEntry> ringArray))
            {
                return;
            }

            if (string.IsNullOrEmpty(_reactorDumpPath))
                return;

            long bytes = UnsafeUtility.SizeOf<ReactorThermalTelemetryEntry>() * ReactorThermalMath.TelemetryCapacity;
            ReactorThermalTelemetryEntry* ring = (ReactorThermalTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ringArray);
            WriteReactorDumpFile(_reactorDumpPath, ring, bytes);
        }

        private void DumpNuclearReactorBlackBox()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _baseReactorTelemetryRing, ReactorThermalMath.TelemetryCapacity, out NativeArray<NuclearReactorTelemetryEntry> ringArray))
            {
                return;
            }

            if (string.IsNullOrEmpty(_baseReactorDumpPath))
                return;

            long bytes = UnsafeUtility.SizeOf<NuclearReactorTelemetryEntry>() * ReactorThermalMath.TelemetryCapacity;
            NuclearReactorTelemetryEntry* ring = (NuclearReactorTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ringArray);
            WriteReactorDumpFile(_baseReactorDumpPath, ring, bytes);
        }

        private static void WriteReactorDumpFile(string path, void* ring, long bytes)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(new ReadOnlySpan<byte>((byte*)ring, checked((int)bytes)));
        }

        private void UploadReactorVisualScalar()
        {
            if (!_reactorBridgeInitialized || _hasPendingJob)
                return;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _reactorScratch, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalScratchDTO> scratchArray) ||
                !TryReadArray(vault, in _reactorKinematics, ReactorThermalMath.MaxReactors, out NativeArray<ReactorKinematicStateDTO> kinematicArray) ||
                !TryReadArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryReadArray(vault, in _reactorCount, 1, out NativeArray<int> countArray) ||
                !TryReadTuning(out ThermalGridTuningDTO gridTuning))
            {
                return;
            }

            int count = math.clamp(countArray[0], 0, ReactorThermalMath.MaxReactors);
            float maxCore = 0f;
            float targetCellTemperature = gridTuning.AmbientTemperatureCelsius;
            double3 targetAup = gridTuning.GridOriginAup;
            for (int i = 0; i < count; i++)
            {
                ReactorThermalScratchDTO scratch = scratchArray[i];
                if (scratch.CoreTempCelsius <= maxCore)
                    continue;

                maxCore = scratch.CoreTempCelsius;
                int cellIndex = (int)math.min(scratch.CenterCellIndex, (uint)math.max(0, frontArray.Length - 1));
                targetCellTemperature = frontArray[cellIndex].TemperatureCelsius;
                targetAup = kinematicArray[i].Aup;
            }

            float totalJoules = TryReadReactorTelemetry(0, out ReactorThermalTelemetryEntry telemetry)
                ? telemetry.TotalJoulesInjected
                : 0f;
            Shader.SetGlobalVector(
                ReactorThermalVisualMetaId,
                new Vector4(
                    targetCellTemperature,
                    maxCore,
                    totalJoules,
                    gridTuning.GlobalQualityWeight));
            Vector3 runtimePoint = HectonFloatingOrigin.ToRuntimePosition(targetAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            Shader.SetGlobalVector(
                ReactorThermalVisualPointId,
                new Vector4(runtimePoint.x, runtimePoint.y, runtimePoint.z, maxCore));

            UploadNuclearReactorVisualBuffer(count);
        }

        private void UploadNuclearReactorVisualBuffer(int count)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadArray(vault, in _baseReactorVisuals, ReactorThermalMath.MaxReactors, out NativeArray<ReactorThermalVisualDTO> visualArray))
            {
                return;
            }

            int stride = UnsafeUtility.SizeOf<ReactorThermalVisualDTO>();
            if (!IsUsableReactorVisualBuffer(_reactorThermalVisualBufferA, stride) ||
                !IsUsableReactorVisualBuffer(_reactorThermalVisualBufferB, stride))
            {
                ReleaseReactorThermalVisualBuffer();
                return;
            }

            GraphicsBuffer target = ((_reactorThermalVisualWriteIndex++ & 1) == 0) ? _reactorThermalVisualBufferA : _reactorThermalVisualBufferB;
            NativeArray<ReactorThermalVisualDTO> writeWindow = default;
            bool mapped = false;
            try
            {
                writeWindow = target.LockBufferForWrite<ReactorThermalVisualDTO>(0, ReactorThermalMath.MaxReactors);
                mapped = true;
                UnsafeUtility.MemCpy(
                    NativeArrayUnsafeUtility.GetUnsafePtr(writeWindow),
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(visualArray),
                    (long)ReactorThermalMath.MaxReactors * stride);
            }
            finally
            {
                if (mapped)
                    target.UnlockBufferAfterWrite<ReactorThermalVisualDTO>(ReactorThermalMath.MaxReactors);
            }

            Shader.SetGlobalBuffer(ReactorThermalStructuredBufferId, target);
            NuclearReactorTelemetryEntry telemetry = TryReadNuclearReactorTelemetry(0, out NuclearReactorTelemetryEntry latest)
                ? latest
                : default;
            Shader.SetGlobalVector(
                ReactorThermalStructuredMetaId,
                new Vector4(
                    math.clamp(count, 0, ReactorThermalMath.MaxReactors),
                    telemetry.TotalGeneratedWatts * 0.000001f,
                    telemetry.TotalBoiledLiters,
                    telemetry.AverageCarnotEfficiency01));
        }

        private static bool IsUsableReactorVisualBuffer(GraphicsBuffer buffer, int stride)
        {
            return buffer != null && buffer.count == ReactorThermalMath.MaxReactors && buffer.stride == stride;
        }

        private void EnsureReactorThermalVisualBuffersCold()
        {
            int stride = UnsafeUtility.SizeOf<ReactorThermalVisualDTO>();
            if (IsUsableReactorVisualBuffer(_reactorThermalVisualBufferA, stride) &&
                IsUsableReactorVisualBuffer(_reactorThermalVisualBufferB, stride))
            {
                return;
            }

            ReleaseReactorThermalVisualBuffer();
            _reactorThermalVisualBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ReactorThermalMath.MaxReactors, stride);
            _reactorThermalVisualBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ReactorThermalMath.MaxReactors, stride);
        }

        private void ReleaseReactorThermalVisualBuffer()
        {
            _reactorThermalVisualBufferA?.Release();
            _reactorThermalVisualBufferB?.Release();
            _reactorThermalVisualBufferA = null;
            _reactorThermalVisualBufferB = null;
            _reactorThermalVisualWriteIndex = 0;
        }

        private void ReleaseReactorThermalVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _reactorStates);
            ReleaseOwnedVaultHandle(vault, ref _reactorKinematics);
            ReleaseOwnedVaultHandle(vault, ref _reactorCount);
            ReleaseOwnedVaultHandle(vault, ref _reactorTuning);
            ReleaseOwnedVaultHandle(vault, ref _reactorTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _reactorTelemetryCursor);
            ReleaseOwnedVaultHandle(vault, ref _reactorProfiles);
            ReleaseOwnedVaultHandle(vault, ref _reactorProfileCount);
            ReleaseOwnedVaultHandle(vault, ref _reactorCsvScratch);
            ReleaseOwnedVaultHandle(vault, ref _reactorScratch);
            ReleaseOwnedVaultHandle(vault, ref _reactorDumpLatch);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorStates);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorTuning);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorPowerLedger);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorTelemetryCursor);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorVisuals);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorDumpLatch);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorProfiles);
            ReleaseOwnedVaultHandle(vault, ref _baseReactorProfileCount);
        }

        private void ClearReactorThermalVaultHandles()
        {
            _reactorStates = default;
            _reactorKinematics = default;
            _reactorCount = default;
            _reactorTuning = default;
            _reactorTelemetryRing = default;
            _reactorTelemetryCursor = default;
            _reactorProfiles = default;
            _reactorProfileCount = default;
            _reactorCsvScratch = default;
            _reactorScratch = default;
            _reactorDumpLatch = default;
            _baseReactorStates = default;
            _baseReactorTuning = default;
            _baseReactorPowerLedger = default;
            _baseReactorTelemetryRing = default;
            _baseReactorTelemetryCursor = default;
            _baseReactorVisuals = default;
            _baseReactorDumpLatch = default;
            _baseReactorProfiles = default;
            _baseReactorProfileCount = default;
            _reactorBridgeInitialized = false;
            _reactorSharedGuardVault = null;
            _reactorSharedGuardMask = 0UL;
        }

        private void TryLoadReactorProfilesCold()
        {
#if UNITY_EDITOR
            IDataVault vault = _vault;
            if (vault == null)
                return;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Assets", "_SourceData", "Thermodynamics", "reactor_hardware_profiles.csv"));
            if (!File.Exists(path))
                return;

            if (!TryResolveArray(vault, in _reactorCsvScratch, ReactorThermalMath.CsvScratchBytes, out NativeArray<byte> scratchArray) ||
                !TryResolveArray(vault, in _reactorProfiles, ReactorThermalMath.MaxProfiles, out NativeArray<ReactorThermalProfileDTO> profileArray) ||
                !TryResolveArray(vault, in _reactorProfileCount, 1, out NativeArray<int> countArray) ||
                !TryResolveArray(vault, in _reactorTuning, 1, out NativeArray<ReactorThermalTuningDTO> tuningArray))
            {
                return;
            }

            byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchArray);
            ReactorThermalProfileDTO* profiles = (ReactorThermalProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileArray);
            int length;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                length = stream.Read(new Span<byte>(scratch, ReactorThermalMath.CsvScratchBytes));
            }

            int parsed = ReactorThermalProfileCsvParser.Parse(new ReadOnlySpan<byte>(scratch, length), profiles, ReactorThermalMath.MaxProfiles);
            if (parsed <= 0)
                return;

            countArray[0] = parsed;
            ReactorThermalProfileDTO profile = profileArray[0];
            ReactorThermalTuningDTO tuning = tuningArray[0];
            tuning.BaseDissipationRate = profile.BaseDissipationRate;
            tuning.CoreHeatCapacityJoulesPerCelsius = profile.CoreHeatCapacityJoulesPerCelsius;
            tuning.SafeCoreTempCelsius = profile.SafeCoreTempCelsius;
            tuning.MeltdownCoreTempCelsius = profile.MeltdownCoreTempCelsius;
            tuning.MockPowerMW = profile.NominalPowerMW;
            tuning.ForcedConvectionMultiplier = profile.ForcedConvectionMultiplier;
            tuning.ProfileHash = profile.ProfileHash;
            tuningArray[0] = tuning;
#endif
        }

        private void TryLoadNuclearReactorProfilesCold()
        {
#if UNITY_EDITOR
            IDataVault vault = _vault;
            if (vault == null)
                return;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Assets", "_SourceData", "Thermodynamics", "reactor_hardware_profiles.csv"));
            if (!File.Exists(path))
                return;

            if (!TryResolveArray(vault, in _reactorCsvScratch, ReactorThermalMath.CsvScratchBytes, out NativeArray<byte> scratchArray) ||
                !TryResolveArray(vault, in _baseReactorProfiles, ReactorThermalMath.MaxProfiles, out NativeArray<NuclearReactorProfileDTO> profileArray) ||
                !TryResolveArray(vault, in _baseReactorProfileCount, 1, out NativeArray<int> countArray) ||
                !TryResolveArray(vault, in _baseReactorTuning, 1, out NativeArray<NuclearReactorThermalTuningDTO> tuningArray))
            {
                return;
            }

            byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchArray);
            NuclearReactorProfileDTO* profiles = (NuclearReactorProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileArray);
            int length;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                length = stream.Read(new Span<byte>(scratch, ReactorThermalMath.CsvScratchBytes));
            }

            int parsed = NuclearReactorProfileCsvParser.Parse(new ReadOnlySpan<byte>(scratch, length), profiles, ReactorThermalMath.MaxProfiles);
            if (parsed <= 0)
                return;

            countArray[0] = parsed;
            NuclearReactorProfileDTO profile = profileArray[0];
            NuclearReactorThermalTuningDTO tuning = tuningArray[0];
            tuning.BaseFissionHeatJoulesPerSecond = profile.BaseFissionHeatJoulesPerSecond;
            tuning.CoreHeatCapacityJoulesPerCelsius = profile.CoreHeatCapacityJoulesPerCelsius;
            tuning.TurbineThermalDrawWatts = profile.TurbineThermalDrawWatts;
            tuning.LatentHeatJoulesPerLiter = profile.LatentHeatJoulesPerLiter;
            tuning.SafeCoreTempCelsius = profile.SafeCoreTempCelsius;
            tuning.MeltdownCoreTempCelsius = profile.MeltdownCoreTempCelsius;
            tuning.RadiationRadiusMeters = profile.RadiationRadiusMeters;
            tuning.MaxBoilOffLitersPerSecond = profile.MaxBoilOffLitersPerSecond;
            tuning.FuelBurnPerMegawattSecond = profile.FuelBurnPerMegawattSecond;
            tuning.ProfileHash = profile.ProfileHash;
            tuningArray[0] = tuning;
#endif
        }
    }
}
