using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Macro-sector biome ids used by the deterministic world regrowth simulation.
    /// </summary>
    public enum WorldRegrowthBiomeId : byte
    {
        SafeShallows = 0,
        TemperateReef = 1,
        ThermalVent = 2,
        DeepAbyss = 3
    }

    /// <summary>
    /// Compact per-sector lifecycle state. Values are byte-sized so the whole model can live inside H8_MacroDB payload pages.
    /// </summary>
    public enum WorldRegrowthStage : byte
    {
        Tombstone = 0,
        Seed = 1,
        Immature = 2,
        Mature = 3
    }

    /// <summary>
    /// Fixed-point constants for the daily macro regrowth solve.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct WorldRegrowthConfig
    {
        public int GridWidth;
        public int GridHeight;
        public int MacroSectorMeters;
        public ushort BaseGrowthProgressPerDayQ;
        public ushort NutrientDiffusionPermille;
        public ushort PreyGrowthPermille;
        public ushort PredationPermille;
        public ushort PredatorConversionPermille;
        public ushort PredatorMortalityPermille;
        public byte PassiveNutrientRecoveryPerDayQ;
        public byte NutrientPenaltyOnMiningQ;
        public byte MinimumNutrientsQ;
        public byte SeedToMatureProgressQ;
        public byte TombstoneBaseDecayDays;
        public byte MinApexRespawnDays;
        public byte MaxApexRespawnDays;
        public byte SafeShallowsTemperatureQ;
        public byte TemperateReefTemperatureQ;
        public byte ThermalVentTemperatureQ;
        public byte DeepAbyssTemperatureQ;
        public byte SafeShallowsNutrientStartQ;
        public byte TemperateReefNutrientStartQ;
        public byte ThermalVentNutrientStartQ;
        public byte DeepAbyssNutrientStartQ;
        public byte Reserved0;
        public byte Reserved1;

        /// <summary>
        /// Current entropy-balanced constants mirrored by Data/Economy/Regrowth_Constants.json.
        /// </summary>
        public static WorldRegrowthConfig Default => new WorldRegrowthConfig
        {
            GridWidth = 64,
            GridHeight = 64,
            MacroSectorMeters = 512,
            BaseGrowthProgressPerDayQ = 10,
            NutrientDiffusionPermille = 220,
            PreyGrowthPermille = 115,
            PredationPermille = 28,
            PredatorConversionPermille = 50,
            PredatorMortalityPermille = 35,
            PassiveNutrientRecoveryPerDayQ = 2,
            NutrientPenaltyOnMiningQ = 90,
            MinimumNutrientsQ = 24,
            SeedToMatureProgressQ = 100,
            TombstoneBaseDecayDays = 8,
            MinApexRespawnDays = 7,
            MaxApexRespawnDays = 90,
            SafeShallowsTemperatureQ = 240,
            TemperateReefTemperatureQ = 190,
            ThermalVentTemperatureQ = 180,
            DeepAbyssTemperatureQ = 80,
            SafeShallowsNutrientStartQ = 230,
            TemperateReefNutrientStartQ = 210,
            ThermalVentNutrientStartQ = 190,
            DeepAbyssNutrientStartQ = 150
        };
    }

    /// <summary>
    /// Last-frame state sample for post-mortem regrowth diagnostics.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
    public struct WorldRegrowthTelemetryEntry
    {
        public uint DayIndex;
        public uint StateHash;
        public int MatureCells;
        public int SeedCells;
        public int TombstoneCells;
        public int AverageNutrientQ;
        public int AverageApexRespawnDays;
        public int Flags;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
    }

    /// <summary>
    /// Data-owner memory block for deterministic macro-sector regrowth.
    /// </summary>
    public struct WorldRegrowthSimulationMemory : IDisposable
    {
        private const string NativeMemoryOwner = nameof(WorldRegrowthSimulationMemory);
        private const SystemID NativeMemorySystemId = SystemID.WorldStreaming;
        private const int BlackBoxCapacity = 300;
        private const int MaxGridDimension = 4096;
        private const int MaxCellCount = 1048576;

        public NativeArray<byte> SoilNutrients;
        public NativeArray<byte> SoilNutrientsScratch;
        public NativeArray<byte> TemperatureQ;
        public NativeArray<byte> BiomeIds;
        public NativeArray<byte> ResourceStages;
        public NativeArray<byte> TombstoneAgeDays;
        public NativeArray<byte> RegrowthProgressQ;
        public NativeArray<byte> OreStockQ;
        public NativeArray<byte> FloraStockQ;
        public NativeArray<byte> PreyBiomassQ;
        public NativeArray<byte> PredatorBiomassQ;
        public NativeArray<byte> ApexRespawnDays;
        public NativeArray<WorldRegrowthTelemetryEntry> BlackBox;
        public int Width;
        public int Height;
        public int CellCount;
        public int CurrentDay;

        public bool IsCreated =>
            SoilNutrients.IsCreated &&
            SoilNutrientsScratch.IsCreated &&
            TemperatureQ.IsCreated &&
            BiomeIds.IsCreated &&
            ResourceStages.IsCreated &&
            TombstoneAgeDays.IsCreated &&
            RegrowthProgressQ.IsCreated &&
            OreStockQ.IsCreated &&
            FloraStockQ.IsCreated &&
            PreyBiomassQ.IsCreated &&
            PredatorBiomassQ.IsCreated &&
            ApexRespawnDays.IsCreated &&
            BlackBox.IsCreated;

        internal bool HasValidDimensions =>
            Width > 0 &&
            Height > 0 &&
            CellCount > 0 &&
            Width <= MaxGridDimension &&
            Height <= MaxGridDimension &&
            CellCount <= MaxCellCount &&
            Width <= MaxCellCount / Height &&
            Width * Height == CellCount;

        internal bool HasValidStorage =>
            IsCreated &&
            HasValidDimensions &&
            SoilNutrients.Length == CellCount &&
            SoilNutrientsScratch.Length == CellCount &&
            TemperatureQ.Length == CellCount &&
            BiomeIds.Length == CellCount &&
            ResourceStages.Length == CellCount &&
            TombstoneAgeDays.Length == CellCount &&
            RegrowthProgressQ.Length == CellCount &&
            OreStockQ.Length == CellCount &&
            FloraStockQ.Length == CellCount &&
            PreyBiomassQ.Length == CellCount &&
            PredatorBiomassQ.Length == CellCount &&
            ApexRespawnDays.Length == CellCount &&
            BlackBox.Length == BlackBoxCapacity;

        private bool HasAnyCreatedLane =>
            SoilNutrients.IsCreated ||
            SoilNutrientsScratch.IsCreated ||
            TemperatureQ.IsCreated ||
            BiomeIds.IsCreated ||
            ResourceStages.IsCreated ||
            TombstoneAgeDays.IsCreated ||
            RegrowthProgressQ.IsCreated ||
            OreStockQ.IsCreated ||
            FloraStockQ.IsCreated ||
            PreyBiomassQ.IsCreated ||
            PredatorBiomassQ.IsCreated ||
            ApexRespawnDays.IsCreated ||
            BlackBox.IsCreated;

        /// <summary>
        /// Allocates all regrowth SOA lanes. This is a cold-path scene/bootstrap operation.
        /// </summary>
        public void Allocate(in WorldRegrowthConfig config, Allocator allocator)
        {
            if (IsCreated)
                return;

            if (HasAnyCreatedLane)
                Dispose();

            Width = math.clamp(config.GridWidth, 1, MaxGridDimension);
            int maxHeightForBudget = math.max(1, MaxCellCount / Width);
            Height = math.min(math.clamp(config.GridHeight, 1, MaxGridDimension), maxHeightForBudget);
            CellCount = Width * Height;
            CurrentDay = 0;
            _ = allocator;
            // Scene-lifetime regrowth lanes must not use Temp or TempJob allocators.
            Allocator laneAllocator = Allocator.Persistent;

            SoilNutrients = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — macro-sector soil nutrients SOA lane — owner: WorldRegrowthSimulationMemory
            SoilNutrientsScratch = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — deterministic nutrient diffusion scratch lane — owner: WorldRegrowthSimulationMemory
            TemperatureQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — macro-sector temperature SOA lane — owner: WorldRegrowthSimulationMemory
            BiomeIds = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — macro-sector biome ids — owner: WorldRegrowthSimulationMemory
            ResourceStages = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — tombstone/seed/mature state lane — owner: WorldRegrowthSimulationMemory
            TombstoneAgeDays = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — tombstone decay age lane — owner: WorldRegrowthSimulationMemory
            RegrowthProgressQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — seed growth progress lane — owner: WorldRegrowthSimulationMemory
            OreStockQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — ore stock lane — owner: WorldRegrowthSimulationMemory
            FloraStockQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — flora stock lane — owner: WorldRegrowthSimulationMemory
            PreyBiomassQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — prey biomass lane — owner: WorldRegrowthSimulationMemory
            PredatorBiomassQ = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — predator biomass lane — owner: WorldRegrowthSimulationMemory
            ApexRespawnDays = H8Memory.Allocate<byte>(CellCount, NativeMemorySystemId, laneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[cellCount] — apex respawn timer lane — owner: WorldRegrowthSimulationMemory
            BlackBox = H8Memory.Allocate<WorldRegrowthTelemetryEntry>(BlackBoxCapacity, NativeMemorySystemId, laneAllocator, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WorldRegrowthTelemetryEntry>[300] — regrowth blackbox ring — owner: WorldRegrowthSimulationMemory

            if (!IsCreated)
            {
                ReleaseUnregisteredNativeArrays();
                ResetState();
                return;
            }

            RegisterNativeArrays();
        }

        /// <summary>
        /// Schedules disposal behind an existing dependency fence.
        /// </summary>
        public JobHandle Dispose(JobHandle dependency)
        {
            dependency = DisposeNativeArray(ref SoilNutrients, dependency);
            dependency = DisposeNativeArray(ref SoilNutrientsScratch, dependency);
            dependency = DisposeNativeArray(ref TemperatureQ, dependency);
            dependency = DisposeNativeArray(ref BiomeIds, dependency);
            dependency = DisposeNativeArray(ref ResourceStages, dependency);
            dependency = DisposeNativeArray(ref TombstoneAgeDays, dependency);
            dependency = DisposeNativeArray(ref RegrowthProgressQ, dependency);
            dependency = DisposeNativeArray(ref OreStockQ, dependency);
            dependency = DisposeNativeArray(ref FloraStockQ, dependency);
            dependency = DisposeNativeArray(ref PreyBiomassQ, dependency);
            dependency = DisposeNativeArray(ref PredatorBiomassQ, dependency);
            dependency = DisposeNativeArray(ref ApexRespawnDays, dependency);
            dependency = DisposeNativeArray(ref BlackBox, dependency);
            ResetState();
            return dependency;
        }

        public void Dispose()
        {
            DisposeNativeArrayImmediate(ref SoilNutrients);
            DisposeNativeArrayImmediate(ref SoilNutrientsScratch);
            DisposeNativeArrayImmediate(ref TemperatureQ);
            DisposeNativeArrayImmediate(ref BiomeIds);
            DisposeNativeArrayImmediate(ref ResourceStages);
            DisposeNativeArrayImmediate(ref TombstoneAgeDays);
            DisposeNativeArrayImmediate(ref RegrowthProgressQ);
            DisposeNativeArrayImmediate(ref OreStockQ);
            DisposeNativeArrayImmediate(ref FloraStockQ);
            DisposeNativeArrayImmediate(ref PreyBiomassQ);
            DisposeNativeArrayImmediate(ref PredatorBiomassQ);
            DisposeNativeArrayImmediate(ref ApexRespawnDays);
            DisposeNativeArrayImmediate(ref BlackBox);
            ResetState();
        }

        private void RegisterNativeArrays()
        {
            NativeMemorySentinel.RegisterNativeArray(SoilNutrients, NativeMemoryOwner, nameof(SoilNutrients), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(SoilNutrientsScratch, NativeMemoryOwner, nameof(SoilNutrientsScratch), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(TemperatureQ, NativeMemoryOwner, nameof(TemperatureQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(BiomeIds, NativeMemoryOwner, nameof(BiomeIds), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(ResourceStages, NativeMemoryOwner, nameof(ResourceStages), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(TombstoneAgeDays, NativeMemoryOwner, nameof(TombstoneAgeDays), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(RegrowthProgressQ, NativeMemoryOwner, nameof(RegrowthProgressQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(OreStockQ, NativeMemoryOwner, nameof(OreStockQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(FloraStockQ, NativeMemoryOwner, nameof(FloraStockQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(PreyBiomassQ, NativeMemoryOwner, nameof(PreyBiomassQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(PredatorBiomassQ, NativeMemoryOwner, nameof(PredatorBiomassQ), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(ApexRespawnDays, NativeMemoryOwner, nameof(ApexRespawnDays), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(BlackBox, NativeMemoryOwner, nameof(BlackBox), NativeAllocationLifetime.Scene);
        }

        private void ReleaseUnregisteredNativeArrays()
        {
            ReleaseUnregisteredNativeArray(ref SoilNutrients);
            ReleaseUnregisteredNativeArray(ref SoilNutrientsScratch);
            ReleaseUnregisteredNativeArray(ref TemperatureQ);
            ReleaseUnregisteredNativeArray(ref BiomeIds);
            ReleaseUnregisteredNativeArray(ref ResourceStages);
            ReleaseUnregisteredNativeArray(ref TombstoneAgeDays);
            ReleaseUnregisteredNativeArray(ref RegrowthProgressQ);
            ReleaseUnregisteredNativeArray(ref OreStockQ);
            ReleaseUnregisteredNativeArray(ref FloraStockQ);
            ReleaseUnregisteredNativeArray(ref PreyBiomassQ);
            ReleaseUnregisteredNativeArray(ref PredatorBiomassQ);
            ReleaseUnregisteredNativeArray(ref ApexRespawnDays);
            ReleaseUnregisteredNativeArray(ref BlackBox);
        }

        private static void ReleaseUnregisteredNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            H8Memory.Release(ref array, NativeMemorySystemId);
            array = default;
        }

        private static JobHandle DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeArray(array);
            JobHandle disposeHandle = H8Memory.Release(ref array, dependency, NativeMemorySystemId);
            array = default;
            return disposeHandle;
        }

        private static void DisposeNativeArrayImmediate<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            H8Memory.Release(ref array, NativeMemorySystemId);
            array = default;
        }

        private void ResetState()
        {
            Width = 0;
            Height = 0;
            CellCount = 0;
            CurrentDay = 0;
        }
    }

    /// <summary>
    /// Schedules deterministic regrowth jobs without owning scene objects.
    /// </summary>
    public static class WorldRegrowthSimulation
    {
        public const int TelemetryCapacity = 300;
        public const string DefaultBlackBoxDumpPath = "Docs/AgentLogs/Dump_ORGANIC_ENTROPY_REGENERATOR.bin";

        /// <summary>
        /// Schedules macro-sector initialization.
        /// </summary>
        public static JobHandle ScheduleInitialize(
            ref WorldRegrowthSimulationMemory memory,
            in WorldRegrowthConfig config,
            int2 macroSectorOrigin,
            uint worldSeed,
            JobHandle dependency)
        {
            if (!memory.HasValidStorage)
                return dependency;

            return new InitializeRegrowthGridJob
            {
                SoilNutrients = memory.SoilNutrients,
                SoilNutrientsScratch = memory.SoilNutrientsScratch,
                TemperatureQ = memory.TemperatureQ,
                BiomeIds = memory.BiomeIds,
                ResourceStages = memory.ResourceStages,
                TombstoneAgeDays = memory.TombstoneAgeDays,
                RegrowthProgressQ = memory.RegrowthProgressQ,
                OreStockQ = memory.OreStockQ,
                FloraStockQ = memory.FloraStockQ,
                PreyBiomassQ = memory.PreyBiomassQ,
                PredatorBiomassQ = memory.PredatorBiomassQ,
                ApexRespawnDays = memory.ApexRespawnDays,
                Config = config,
                MacroSectorOrigin = macroSectorOrigin,
                WorldSeed = worldSeed,
                Width = memory.Width,
                Height = memory.Height
            }.Schedule(memory.CellCount, 64, dependency);
        }

        /// <summary>
        /// Schedules one in-game day of regrowth, nutrient diffusion, and apex respawn projection.
        /// </summary>
        public static JobHandle ScheduleDailySolve(
            ref WorldRegrowthSimulationMemory memory,
            in WorldRegrowthConfig config,
            int dayIndex,
            JobHandle dependency)
        {
            if (!memory.HasValidStorage)
                return dependency;

            memory.CurrentDay = math.max(0, dayIndex);
            JobHandle diffusionHandle = new NutrientDiffusionJob
            {
                SoilNutrients = memory.SoilNutrients,
                SoilNutrientsScratch = memory.SoilNutrientsScratch,
                ResourceStages = memory.ResourceStages,
                Config = config,
                Width = memory.Width,
                Height = memory.Height
            }.Schedule(memory.CellCount, 64, dependency);

            JobHandle solveHandle = new DailyRegrowthJob
            {
                SoilNutrients = memory.SoilNutrients,
                SoilNutrientsScratch = memory.SoilNutrientsScratch,
                TemperatureQ = memory.TemperatureQ,
                ResourceStages = memory.ResourceStages,
                TombstoneAgeDays = memory.TombstoneAgeDays,
                RegrowthProgressQ = memory.RegrowthProgressQ,
                OreStockQ = memory.OreStockQ,
                FloraStockQ = memory.FloraStockQ,
                PreyBiomassQ = memory.PreyBiomassQ,
                PredatorBiomassQ = memory.PredatorBiomassQ,
                ApexRespawnDays = memory.ApexRespawnDays,
                Config = config
            }.Schedule(memory.CellCount, 64, diffusionHandle);

            return new RegrowthTelemetryJob
            {
                SoilNutrients = memory.SoilNutrients,
                ResourceStages = memory.ResourceStages,
                ApexRespawnDays = memory.ApexRespawnDays,
                BlackBox = memory.BlackBox,
                DayIndex = (uint)math.max(0, dayIndex)
            }.Schedule(solveHandle);
        }

        /// <summary>
        /// Schedules mining tombstone writes for caller-owned cell index batches.
        /// </summary>
        public static JobHandle ScheduleMiningTombstones(
            ref WorldRegrowthSimulationMemory memory,
            NativeArray<int> minedCellIndices,
            byte depletionSeverityQ,
            in WorldRegrowthConfig config,
            JobHandle dependency)
        {
            if (!memory.HasValidStorage || !minedCellIndices.IsCreated || minedCellIndices.Length <= 0)
                return dependency;

            return new MiningTombstoneJob
            {
                MinedCellIndices = minedCellIndices,
                SoilNutrients = memory.SoilNutrients,
                ResourceStages = memory.ResourceStages,
                TombstoneAgeDays = memory.TombstoneAgeDays,
                RegrowthProgressQ = memory.RegrowthProgressQ,
                OreStockQ = memory.OreStockQ,
                FloraStockQ = memory.FloraStockQ,
                PreyBiomassQ = memory.PreyBiomassQ,
                DepletionSeverityQ = depletionSeverityQ,
                NutrientPenaltyQ = config.NutrientPenaltyOnMiningQ
            }.Schedule(dependency);
        }

        /// <summary>
        /// Resolves an apex predator respawn delay from byte-quantized Lotka-Volterra state.
        /// </summary>
        public static byte ResolveApexRespawnDays(byte preyBiomassQ, byte predatorBiomassQ, in WorldRegrowthConfig config)
        {
            int prey = preyBiomassQ;
            int predator = predatorBiomassQ;
            int preyDelta = ((prey * config.PreyGrowthPermille) - ((prey * predator * config.PredationPermille) / 255)) / 1000;
            int nextPrey = math.clamp(prey + preyDelta, 0, 255);
            int predatorDelta = (((predator * nextPrey * config.PredatorConversionPermille) / 255) - (predator * config.PredatorMortalityPermille)) / 1000;
            int nextPredator = math.clamp(predator + predatorDelta, 0, 255);
            int range = math.max(0, config.MaxApexRespawnDays - config.MinApexRespawnDays);
            int delay = config.MaxApexRespawnDays - ((range * nextPrey) / 255) + ((nextPredator * 12) / 255);
            return (byte)math.clamp(delay, config.MinApexRespawnDays, config.MaxApexRespawnDays);
        }

        /// <summary>
        /// Dumps the fixed regrowth telemetry ring for crash/post-mortem analysis.
        /// </summary>
        public static unsafe bool TryDumpBlackBox(in WorldRegrowthSimulationMemory memory, string path = DefaultBlackBoxDumpPath)
        {
            if (!memory.BlackBox.IsCreated || memory.BlackBox.Length != TelemetryCapacity || string.IsNullOrEmpty(path))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int entryBytes = UnsafeUtility.SizeOf<WorldRegrowthTelemetryEntry>();
                int dumpBytes = entryBytes * memory.BlackBox.Length;
                byte[] dump = new byte[dumpBytes]; // COLD ALLOC: byte[WorldRegrowthTelemetryEntry*300] — crash dump staging buffer — owner: WorldRegrowthSimulation
                fixed (byte* destination = dump)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(memory.BlackBox);
                    UnsafeUtility.MemCpy(destination, source, dumpBytes);
                }

                File.WriteAllBytes(path, dump);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeRegrowthGridJob : IJobParallelFor
    {
        public NativeArray<byte> SoilNutrients;
        public NativeArray<byte> SoilNutrientsScratch;
        public NativeArray<byte> TemperatureQ;
        public NativeArray<byte> BiomeIds;
        public NativeArray<byte> ResourceStages;
        public NativeArray<byte> TombstoneAgeDays;
        public NativeArray<byte> RegrowthProgressQ;
        public NativeArray<byte> OreStockQ;
        public NativeArray<byte> FloraStockQ;
        public NativeArray<byte> PreyBiomassQ;
        public NativeArray<byte> PredatorBiomassQ;
        public NativeArray<byte> ApexRespawnDays;
        public WorldRegrowthConfig Config;
        public int2 MacroSectorOrigin;
        public uint WorldSeed;
        public int Width;
        public int Height;

        public void Execute(int index)
        {
            int width = math.max(1, Width);
            int x = index % width;
            int z = index / width;
            byte biome = ResolveBiomeId(MacroSectorOrigin.x + x, MacroSectorOrigin.y + z, WorldSeed, Height);
            byte temperature = ResolveTemperature(biome);
            byte nutrients = ResolveNutrientStart(biome);

            BiomeIds[index] = biome;
            TemperatureQ[index] = temperature;
            SoilNutrients[index] = nutrients;
            SoilNutrientsScratch[index] = nutrients;
            ResourceStages[index] = (byte)WorldRegrowthStage.Mature;
            TombstoneAgeDays[index] = 0;
            RegrowthProgressQ[index] = Config.SeedToMatureProgressQ;
            OreStockQ[index] = byte.MaxValue;
            FloraStockQ[index] = byte.MaxValue;
            PreyBiomassQ[index] = byte.MaxValue;
            PredatorBiomassQ[index] = (byte)math.clamp((int)temperature >> 2, 8, 96);
            ApexRespawnDays[index] = WorldRegrowthSimulation.ResolveApexRespawnDays(PreyBiomassQ[index], PredatorBiomassQ[index], Config);
        }

        private byte ResolveBiomeId(int sectorX, int sectorZ, uint seed, int gridHeight)
        {
            uint hash = Hash32(unchecked((uint)sectorX) ^ RotateLeft(unchecked((uint)sectorZ), 13) ^ seed);
            int band = (int)(((ulong)hash * 100UL) >> 32);
            int safeHeight = math.max(1, gridHeight);
            int localZ = sectorZ % safeHeight;
            if (localZ < 0)
                localZ = -localZ;

            if (localZ < safeHeight / 4 || band < 24)
                return (byte)WorldRegrowthBiomeId.SafeShallows;
            if (band < 58)
                return (byte)WorldRegrowthBiomeId.TemperateReef;
            if (band < 75)
                return (byte)WorldRegrowthBiomeId.ThermalVent;
            return (byte)WorldRegrowthBiomeId.DeepAbyss;
        }

        private byte ResolveTemperature(byte biome)
        {
            if (biome == (byte)WorldRegrowthBiomeId.SafeShallows)
                return Config.SafeShallowsTemperatureQ;
            if (biome == (byte)WorldRegrowthBiomeId.TemperateReef)
                return Config.TemperateReefTemperatureQ;
            if (biome == (byte)WorldRegrowthBiomeId.ThermalVent)
                return Config.ThermalVentTemperatureQ;
            return Config.DeepAbyssTemperatureQ;
        }

        private byte ResolveNutrientStart(byte biome)
        {
            if (biome == (byte)WorldRegrowthBiomeId.SafeShallows)
                return Config.SafeShallowsNutrientStartQ;
            if (biome == (byte)WorldRegrowthBiomeId.TemperateReef)
                return Config.TemperateReefNutrientStartQ;
            if (biome == (byte)WorldRegrowthBiomeId.ThermalVent)
                return Config.ThermalVentNutrientStartQ;
            return Config.DeepAbyssNutrientStartQ;
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct NutrientDiffusionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> SoilNutrients;
        [WriteOnly] public NativeArray<byte> SoilNutrientsScratch;
        [ReadOnly] public NativeArray<byte> ResourceStages;
        public WorldRegrowthConfig Config;
        public int Width;
        public int Height;

        public void Execute(int index)
        {
            int nutrient = ResolveDiffusedNutrient(index);
            if (ResourceStages[index] == (byte)WorldRegrowthStage.Tombstone)
                nutrient = math.max(Config.MinimumNutrientsQ, nutrient - 1);

            SoilNutrientsScratch[index] = (byte)math.clamp(nutrient, Config.MinimumNutrientsQ, byte.MaxValue);
        }

        private int ResolveDiffusedNutrient(int index)
        {
            int x = index % Width;
            int z = index / Width;
            int sum = 0;
            int count = 0;

            if (x > 0)
            {
                sum += SoilNutrients[index - 1];
                count++;
            }

            if (x + 1 < Width)
            {
                sum += SoilNutrients[index + 1];
                count++;
            }

            if (z > 0)
            {
                sum += SoilNutrients[index - Width];
                count++;
            }

            if (z + 1 < Height)
            {
                sum += SoilNutrients[index + Width];
                count++;
            }

            int current = SoilNutrients[index];
            int average = count > 0 ? sum / count : current;
            int diffusion = ((average - current) * Config.NutrientDiffusionPermille) / 1000;
            return current + diffusion + Config.PassiveNutrientRecoveryPerDayQ;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct DailyRegrowthJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<byte> SoilNutrients;
        [ReadOnly] public NativeArray<byte> SoilNutrientsScratch;
        [ReadOnly] public NativeArray<byte> TemperatureQ;
        public NativeArray<byte> ResourceStages;
        public NativeArray<byte> TombstoneAgeDays;
        public NativeArray<byte> RegrowthProgressQ;
        public NativeArray<byte> OreStockQ;
        public NativeArray<byte> FloraStockQ;
        public NativeArray<byte> PreyBiomassQ;
        public NativeArray<byte> PredatorBiomassQ;
        public NativeArray<byte> ApexRespawnDays;
        public WorldRegrowthConfig Config;

        public void Execute(int index)
        {
            int nutrient = SoilNutrientsScratch[index];
            byte stage = ResourceStages[index];
            byte nutrientQ = (byte)math.clamp(nutrient, Config.MinimumNutrientsQ, byte.MaxValue);
            SoilNutrients[index] = nutrientQ;
            byte temperature = TemperatureQ[index];

            if (stage == (byte)WorldRegrowthStage.Tombstone)
            {
                int age = math.min(byte.MaxValue, TombstoneAgeDays[index] + 1);
                TombstoneAgeDays[index] = (byte)age;
                int decayDays = ResolveTombstoneDecayDays(temperature);
                if (age >= decayDays)
                {
                    ResourceStages[index] = (byte)WorldRegrowthStage.Seed;
                    TombstoneAgeDays[index] = 0;
                    RegrowthProgressQ[index] = 0;
                }

                ApexRespawnDays[index] = WorldRegrowthSimulation.ResolveApexRespawnDays(PreyBiomassQ[index], PredatorBiomassQ[index], Config);
                return;
            }

            int growth = ResolveGrowthProgress(nutrientQ, temperature);
            if (stage == (byte)WorldRegrowthStage.Seed || stage == (byte)WorldRegrowthStage.Immature)
            {
                int nextProgress = RegrowthProgressQ[index] + growth;
                if (nextProgress >= Config.SeedToMatureProgressQ)
                {
                    ResourceStages[index] = (byte)WorldRegrowthStage.Mature;
                    RegrowthProgressQ[index] = Config.SeedToMatureProgressQ;
                    OreStockQ[index] = byte.MaxValue;
                    FloraStockQ[index] = byte.MaxValue;
                    PreyBiomassQ[index] = byte.MaxValue;
                }
                else
                {
                    ResourceStages[index] = (byte)WorldRegrowthStage.Immature;
                    RegrowthProgressQ[index] = (byte)math.clamp(nextProgress, 0, byte.MaxValue);
                    int stock = (nextProgress * byte.MaxValue) / math.max(1, Config.SeedToMatureProgressQ);
                    OreStockQ[index] = (byte)math.clamp(stock, 0, 180);
                    FloraStockQ[index] = (byte)math.clamp(stock, 0, 180);
                    PreyBiomassQ[index] = FloraStockQ[index];
                }
            }
            else
            {
                int refill = math.max(1, growth >> 1);
                OreStockQ[index] = (byte)math.min(byte.MaxValue, OreStockQ[index] + refill);
                FloraStockQ[index] = (byte)math.min(byte.MaxValue, FloraStockQ[index] + refill);
                PreyBiomassQ[index] = FloraStockQ[index];
            }

            byte predator = PredatorBiomassQ[index];
            int prey = PreyBiomassQ[index];
            int predatorDelta = (((predator * prey * Config.PredatorConversionPermille) / 255) - (predator * Config.PredatorMortalityPermille)) / 1000;
            PredatorBiomassQ[index] = (byte)math.clamp(predator + predatorDelta, 0, 255);
            ApexRespawnDays[index] = WorldRegrowthSimulation.ResolveApexRespawnDays(PreyBiomassQ[index], PredatorBiomassQ[index], Config);
        }

        private int ResolveTombstoneDecayDays(byte temperature)
        {
            return math.max(1, ((Config.TombstoneBaseDecayDays * byte.MaxValue) + temperature - 1) / math.max(1, temperature));
        }

        private int ResolveGrowthProgress(byte nutrients, byte temperature)
        {
            int growth = (Config.BaseGrowthProgressPerDayQ * nutrients * temperature) / (byte.MaxValue * byte.MaxValue);
            return math.max(1, growth);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct MiningTombstoneJob : IJob
    {
        [ReadOnly] public NativeArray<int> MinedCellIndices;
        public NativeArray<byte> SoilNutrients;
        public NativeArray<byte> ResourceStages;
        public NativeArray<byte> TombstoneAgeDays;
        public NativeArray<byte> RegrowthProgressQ;
        public NativeArray<byte> OreStockQ;
        public NativeArray<byte> FloraStockQ;
        public NativeArray<byte> PreyBiomassQ;
        public byte DepletionSeverityQ;
        public byte NutrientPenaltyQ;

        public void Execute()
        {
            int severity = math.max(1, DepletionSeverityQ);
            int floraSeverity = math.max(1, severity >> 1);
            for (int index = 0; index < MinedCellIndices.Length; index++)
            {
                int cellIndex = MinedCellIndices[index];
                if ((uint)cellIndex >= (uint)ResourceStages.Length)
                    continue;

                ResourceStages[cellIndex] = (byte)WorldRegrowthStage.Tombstone;
                TombstoneAgeDays[cellIndex] = 0;
                RegrowthProgressQ[cellIndex] = 0;
                OreStockQ[cellIndex] = (byte)math.max(0, OreStockQ[cellIndex] - severity);
                FloraStockQ[cellIndex] = (byte)math.max(0, FloraStockQ[cellIndex] - floraSeverity);
                PreyBiomassQ[cellIndex] = FloraStockQ[cellIndex];
                SoilNutrients[cellIndex] = (byte)math.max(0, SoilNutrients[cellIndex] - NutrientPenaltyQ);
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct RegrowthTelemetryJob : IJob
    {
        [ReadOnly] public NativeArray<byte> SoilNutrients;
        [ReadOnly] public NativeArray<byte> ResourceStages;
        [ReadOnly] public NativeArray<byte> ApexRespawnDays;
        public NativeArray<WorldRegrowthTelemetryEntry> BlackBox;
        public uint DayIndex;

        public void Execute()
        {
            if (!SoilNutrients.IsCreated || !BlackBox.IsCreated || BlackBox.Length <= 0)
                return;

            int mature = 0;
            int seed = 0;
            int tombstone = 0;
            int nutrientSum = 0;
            int respawnSum = 0;
            uint hash = 2166136261u;
            int count = SoilNutrients.Length;

            for (int i = 0; i < count; i++)
            {
                byte stage = ResourceStages[i];
                byte nutrient = SoilNutrients[i];
                byte respawn = ApexRespawnDays[i];
                if (stage == (byte)WorldRegrowthStage.Mature)
                    mature++;
                else if (stage == (byte)WorldRegrowthStage.Tombstone)
                    tombstone++;
                else
                    seed++;

                nutrientSum += nutrient;
                respawnSum += respawn;
                hash = (hash ^ stage) * 16777619u;
                hash = (hash ^ nutrient) * 16777619u;
                hash = (hash ^ respawn) * 16777619u;
            }

            int divisor = math.max(1, count);
            BlackBox[(int)(DayIndex % (uint)BlackBox.Length)] = new WorldRegrowthTelemetryEntry
            {
                DayIndex = DayIndex,
                StateHash = hash,
                MatureCells = mature,
                SeedCells = seed,
                TombstoneCells = tombstone,
                AverageNutrientQ = nutrientSum / divisor,
                AverageApexRespawnDays = respawnSum / divisor,
                Flags = mature > 0 ? 1 : 0
            };
        }
    }

    /// <summary>
    /// H8_MacroDB binary payload header for regrowth pages.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 80)]
    public struct WorldRegrowthPayloadHeader
    {
        public uint Magic;
        public uint Version;
        public int Width;
        public int Height;
        public int CellCount;
        public int SimDay;
        public int SoilOffset;
        public int TemperatureOffset;
        public int BiomeOffset;
        public int StageOffset;
        public int TombstoneOffset;
        public int ProgressOffset;
        public int OreOffset;
        public int FloraOffset;
        public int PreyOffset;
        public int PredatorOffset;
        public int ApexOffset;
        public uint Checksum;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Packs and unpacks regrowth state as a contiguous H8_MacroDB sector payload.
    /// </summary>
    public static unsafe class WorldRegrowthMacroDatabaseCodec
    {
        public const uint PayloadMagic = 0x52473848u;
        public const uint PayloadVersion = 1u;

        /// <summary>
        /// Calculates the byte capacity required for one packed regrowth payload.
        /// </summary>
        public static int CalculatePayloadBytes(int cellCount)
        {
            int headerBytes = UnsafeUtility.SizeOf<WorldRegrowthPayloadHeader>();
            int safeCount = math.max(0, cellCount);
            if (safeCount > (int.MaxValue - headerBytes) / 11)
                return -1;

            return headerBytes + (safeCount * 11);
        }

        /// <summary>
        /// Packs regrowth SOA lanes into caller-owned bytes ready for IMacroDatabaseService.MarkDirty.
        /// </summary>
        public static bool TryPack(in WorldRegrowthSimulationMemory memory, NativeArray<byte> destination, out int writtenBytes)
        {
            writtenBytes = 0;
            if (!memory.HasValidStorage || !destination.IsCreated)
                return false;

            int requiredBytes = CalculatePayloadBytes(memory.CellCount);
            if (requiredBytes <= UnsafeUtility.SizeOf<WorldRegrowthPayloadHeader>() || destination.Length < requiredBytes)
                return false;

            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            int headerBytes = UnsafeUtility.SizeOf<WorldRegrowthPayloadHeader>();
            WorldRegrowthPayloadHeader header = BuildHeader(in memory, headerBytes);

            if (!CopyLane(memory.SoilNutrients, dst + header.SoilOffset, memory.CellCount) ||
                !CopyLane(memory.TemperatureQ, dst + header.TemperatureOffset, memory.CellCount) ||
                !CopyLane(memory.BiomeIds, dst + header.BiomeOffset, memory.CellCount) ||
                !CopyLane(memory.ResourceStages, dst + header.StageOffset, memory.CellCount) ||
                !CopyLane(memory.TombstoneAgeDays, dst + header.TombstoneOffset, memory.CellCount) ||
                !CopyLane(memory.RegrowthProgressQ, dst + header.ProgressOffset, memory.CellCount) ||
                !CopyLane(memory.OreStockQ, dst + header.OreOffset, memory.CellCount) ||
                !CopyLane(memory.FloraStockQ, dst + header.FloraOffset, memory.CellCount) ||
                !CopyLane(memory.PreyBiomassQ, dst + header.PreyOffset, memory.CellCount) ||
                !CopyLane(memory.PredatorBiomassQ, dst + header.PredatorOffset, memory.CellCount) ||
                !CopyLane(memory.ApexRespawnDays, dst + header.ApexOffset, memory.CellCount))
            {
                return false;
            }

            header.Checksum = ComputeChecksum(dst + headerBytes, requiredBytes - headerBytes);
            UnsafeUtility.CopyStructureToPtr(ref header, dst);
            writtenBytes = requiredBytes;
            return true;
        }

        /// <summary>
        /// Restores a packed H8_MacroDB regrowth payload into preallocated SOA lanes.
        /// </summary>
        public static bool TryUnpack(NativeArray<byte> source, ref WorldRegrowthSimulationMemory memory, out WorldRegrowthPayloadHeader header)
        {
            header = default;
            if (!source.IsCreated || !memory.HasValidStorage || source.Length < UnsafeUtility.SizeOf<WorldRegrowthPayloadHeader>())
                return false;

            byte* src = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            header = UnsafeUtility.ReadArrayElement<WorldRegrowthPayloadHeader>(src, 0);
            if (header.Magic != PayloadMagic || header.Version != PayloadVersion || header.CellCount != memory.CellCount)
                return false;

            int headerBytes = UnsafeUtility.SizeOf<WorldRegrowthPayloadHeader>();
            int requiredBytes = CalculatePayloadBytes(header.CellCount);
            if (requiredBytes <= headerBytes || source.Length < requiredBytes || !HasValidHeaderLayout(in header, in memory, headerBytes, requiredBytes))
                return false;

            uint checksum = ComputeChecksum(src + headerBytes, requiredBytes - headerBytes);
            if (checksum != header.Checksum)
                return false;

            if (!CopyLane(src + header.SoilOffset, memory.SoilNutrients, header.CellCount) ||
                !CopyLane(src + header.TemperatureOffset, memory.TemperatureQ, header.CellCount) ||
                !CopyLane(src + header.BiomeOffset, memory.BiomeIds, header.CellCount) ||
                !CopyLane(src + header.StageOffset, memory.ResourceStages, header.CellCount) ||
                !CopyLane(src + header.TombstoneOffset, memory.TombstoneAgeDays, header.CellCount) ||
                !CopyLane(src + header.ProgressOffset, memory.RegrowthProgressQ, header.CellCount) ||
                !CopyLane(src + header.OreOffset, memory.OreStockQ, header.CellCount) ||
                !CopyLane(src + header.FloraOffset, memory.FloraStockQ, header.CellCount) ||
                !CopyLane(src + header.PreyOffset, memory.PreyBiomassQ, header.CellCount) ||
                !CopyLane(src + header.PredatorOffset, memory.PredatorBiomassQ, header.CellCount) ||
                !CopyLane(src + header.ApexOffset, memory.ApexRespawnDays, header.CellCount) ||
                !CopyLane(src + header.SoilOffset, memory.SoilNutrientsScratch, header.CellCount))
            {
                return false;
            }

            memory.CurrentDay = math.max(0, header.SimDay);
            return true;
        }

        private static WorldRegrowthPayloadHeader BuildHeader(in WorldRegrowthSimulationMemory memory, int headerBytes)
        {
            int offset = headerBytes;
            WorldRegrowthPayloadHeader header = new WorldRegrowthPayloadHeader
            {
                Magic = PayloadMagic,
                Version = PayloadVersion,
                Width = memory.Width,
                Height = memory.Height,
                CellCount = memory.CellCount,
                SimDay = memory.CurrentDay,
                SoilOffset = offset
            };

            offset += memory.CellCount;
            header.TemperatureOffset = offset;
            offset += memory.CellCount;
            header.BiomeOffset = offset;
            offset += memory.CellCount;
            header.StageOffset = offset;
            offset += memory.CellCount;
            header.TombstoneOffset = offset;
            offset += memory.CellCount;
            header.ProgressOffset = offset;
            offset += memory.CellCount;
            header.OreOffset = offset;
            offset += memory.CellCount;
            header.FloraOffset = offset;
            offset += memory.CellCount;
            header.PreyOffset = offset;
            offset += memory.CellCount;
            header.PredatorOffset = offset;
            offset += memory.CellCount;
            header.ApexOffset = offset;
            return header;
        }

        private static bool HasValidHeaderLayout(
            in WorldRegrowthPayloadHeader header,
            in WorldRegrowthSimulationMemory memory,
            int headerBytes,
            int requiredBytes)
        {
            if (header.Width != memory.Width || header.Height != memory.Height || header.CellCount != memory.CellCount)
                return false;

            int count = header.CellCount;
            if (header.SoilOffset != headerBytes)
                return false;

            if (header.TemperatureOffset != header.SoilOffset + count)
                return false;
            if (header.BiomeOffset != header.TemperatureOffset + count)
                return false;
            if (header.StageOffset != header.BiomeOffset + count)
                return false;
            if (header.TombstoneOffset != header.StageOffset + count)
                return false;
            if (header.ProgressOffset != header.TombstoneOffset + count)
                return false;
            if (header.OreOffset != header.ProgressOffset + count)
                return false;
            if (header.FloraOffset != header.OreOffset + count)
                return false;
            if (header.PreyOffset != header.FloraOffset + count)
                return false;
            if (header.PredatorOffset != header.PreyOffset + count)
                return false;
            if (header.ApexOffset != header.PredatorOffset + count)
                return false;

            return header.ApexOffset + count <= requiredBytes;
        }

        private static bool CopyLane(NativeArray<byte> source, byte* destination, int count)
        {
            if (!source.IsCreated || source.Length < count || destination == null)
                return false;

            void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            UnsafeUtility.MemCpy(destination, src, count);
            return true;
        }

        private static bool CopyLane(byte* source, NativeArray<byte> destination, int count)
        {
            if (source == null || !destination.IsCreated || destination.Length < count)
                return false;

            void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            UnsafeUtility.MemCpy(dst, source, count);
            return true;
        }

        private static uint ComputeChecksum(byte* data, int byteCount)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < byteCount; i++)
                hash = (hash ^ data[i]) * 16777619u;
            return hash;
        }
    }
}
