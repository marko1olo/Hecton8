using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.SeedShipAnomaly
{
    internal static class SeedShipAnomalyFlags
    {
        public const uint Active = 1u << 0;
        public const uint MockRebased = 1u << 1;
        public const uint Healing = 1u << 2;
        public const uint NonFinite = 1u << 3;
        public const uint BudgetExceeded = 1u << 4;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SeedShipMockAupRebaseJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<MockAupRebaseSignal> RebaseSignals;
        public uint Frame;
        public uint Seed;
        public uint SectorHash;
        public float Chance01;

        public void Execute()
        {
            if (!RebaseSignals.IsCreated || RebaseSignals.Length == 0 || Frame == 0u)
                return;

            uint rngSeed = Seed ^ SectorHash ^ (Frame * 747796405u) ^ 0x9E3779B9u;
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(rngSeed != 0u ? rngSeed : 1u);
            if (random.NextFloat() > math.saturate(Chance01))
                return;

            int sx = random.NextInt(-1, 2);
            int sy = random.NextInt(-1, 2);
            int sz = random.NextInt(-1, 2);
            int3 sectorDelta = new int3(sx, sy, sz);
            if (sectorDelta.x == 0 && sectorDelta.y == 0 && sectorDelta.z == 0)
                sectorDelta.x = 1;
            float3 shiftMeters = new float3((float)sectorDelta.x, (float)sectorDelta.y, (float)sectorDelta.z) * 8f;
            RebaseSignals[0] = new MockAupRebaseSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = Frame,
                SectorDelta = sectorDelta,
                Flags = 1u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SeedShipAnomalyFieldJob : IJob
    {
        [NoAlias] public NativeArray<AnomalyFieldDTO> Field;
        [NoAlias] public NativeArray<AnomalyTuningDTO> Tuning;
        [NoAlias] public NativeArray<AnomalyGlobalScalarsDTO> Globals;
        [WriteOnly, NoAlias] public NativeArray<GlitchCommandDTO> GlitchCommands;
        [WriteOnly, NoAlias] public NativeArray<MockHudSignal> HudSignals;
        [WriteOnly, NoAlias] public NativeArray<AnomalyThermoSourceDTO> ThermoSources;
        [ReadOnly, NoAlias] public NativeArray<MockAupRebaseSignal> RebaseSignals;
        [WriteOnly, NoAlias] public NativeArray<AnomalyTelemetryEntry> Telemetry;
        public global::Hecton8.Core.MpscSignalRingBuffer<RadarJamSignal>.ParallelWriter RadarJamWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> RadarJamWriterBudget;
        public double3 PlayerAUP;
        public float DeltaSeconds;
        public float TimeSeconds;
        public float HackHealingSeconds;
        public int TelemetryCursor;
        public int EntityBudget;
        public uint Frame;
        public byte EmitRadarSignal;

        public void Execute()
        {
            if (!Field.IsCreated || Field.Length == 0 ||
                !Tuning.IsCreated || Tuning.Length == 0 ||
                !Globals.IsCreated || Globals.Length == 0)
            {
                return;
            }

            AnomalyTuningDTO tuning = SeedShipAnomalyMath.SanitizeTuning(Tuning[0]);
            Tuning[0] = tuning;

            AnomalyFieldDTO field = Field[0];
            AnomalyGlobalScalarsDTO globals = Globals[0];
            uint flags = SeedShipAnomalyFlags.Active;
            double3 playerAup = PlayerAUP;

            if (!math.all(math.isfinite(playerAup)) || !math.all(math.isfinite(field.EpicenterAUP)))
            {
                playerAup = new double3(0.0, SeedShipAnomalyConstants.DefaultSeedShipDepthMeters, 0.0);
                field.EpicenterAUP = playerAup;
                flags |= SeedShipAnomalyFlags.NonFinite;
            }

            if (RebaseSignals.IsCreated && RebaseSignals.Length > 0)
            {
                MockAupRebaseSignal rebase = RebaseSignals[0];
                if (rebase.Flags != 0u &&
                    rebase.ShiftFrameId != 0u &&
                    rebase.ShiftFrameId != globals.LastRebaseFrame)
                {
                    double3 shift = new double3(rebase.ShiftMeters.x, rebase.ShiftMeters.y, rebase.ShiftMeters.z);
                    field.EpicenterAUP += shift;
                    playerAup += shift;
                    globals.LastRebaseFrame = rebase.ShiftFrameId;
                    flags |= SeedShipAnomalyFlags.MockRebased;
                }
            }

            field.Radius = tuning.MaxCorruptionRadius;
            field.GlitchHash = field.GlitchHash != 0u ? field.GlitchHash : SeedShipAnomalyConstants.GlitchHash;
            float targetCorruption = SeedShipAnomalyMath.ResolveCorruption01(playerAup, field.EpicenterAUP, field.Radius);
            float smoothing = math.saturate(math.max(0.0001f, DeltaSeconds) * 4f);
            float corruption = math.lerp(math.saturate(field.CorruptionLevel), targetCorruption, smoothing);
            if (HackHealingSeconds > 0f)
            {
                corruption = math.max(0f, corruption - math.max(0.0001f, DeltaSeconds) * 0.1f * tuning.HealingRateScalar);
                flags |= SeedShipAnomalyFlags.Healing;
            }

            if (!math.isfinite(corruption))
            {
                corruption = 0f;
                flags |= SeedShipAnomalyFlags.NonFinite;
            }

            field.CorruptionLevel = math.saturate(corruption);
            float pulse = TimeSeconds * tuning.PulseFrequency;
            float sine01 = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(pulse);
            float gravityY = SeedShipAnomalyMath.ResolveGravityY(field.CorruptionLevel, pulse, tuning.GravityInversionStrength);
            float shaderCorruption = math.saturate(field.CorruptionLevel * tuning.GlitchIntensity);
            float universeNoise = math.saturate(shaderCorruption * tuning.ShaderNoiseStrength * (0.35f + 0.65f * sine01));
            float heat01 = math.saturate(field.CorruptionLevel * tuning.HeatEmission * (0.6f + 0.4f * sine01));
            float radiation01 = math.saturate(field.CorruptionLevel * tuning.RadiationEmission);
            float radar01 = math.saturate(field.CorruptionLevel * tuning.RadarJamIntensity * sine01);
            float babel01 = math.saturate(field.CorruptionLevel * tuning.BabelScrambleStrength);

            globals.Corruption01 = field.CorruptionLevel;
            globals.GravityY = gravityY;
            globals.ShaderCorruption01 = shaderCorruption;
            globals.UniverseOffsetNoise01 = universeNoise;
            globals.HeatSource01 = heat01;
            globals.Radiation01 = radiation01;
            globals.RadarJam01 = radar01;
            globals.BabelScramble01 = babel01;
            globals.GlobalQualityWeight = tuning.GlobalQualityWeight;
            globals.EntityBudget = math.max(0, EntityBudget);
            globals.EntitiesAffected = math.max(0, EntityBudget);
            globals.Frame = Frame;
            globals.Flags = flags;
            globals.RadiusMeters = field.Radius;

            GlitchCommandDTO command = new GlitchCommandDTO
            {
                Intensity = shaderCorruption,
                Frequency = tuning.PulseFrequency,
                GlyphHash = field.GlitchHash,
                _pad0 = 0u
            };

            if (GlitchCommands.IsCreated && GlitchCommands.Length > 0)
                GlitchCommands[0] = command;

            if (HudSignals.IsCreated && HudSignals.Length > 0)
            {
                HudSignals[0] = new MockHudSignal
                {
                    Command = command,
                    Frame = Frame,
                    SourceHash = SeedShipAnomalyConstants.SourceHash,
                    Corruption01 = field.CorruptionLevel,
                    Flags = (byte)(field.CorruptionLevel > 0.001f ? 1 : 0)
                };
            }

            if (ThermoSources.IsCreated && ThermoSources.Length > 0)
            {
                ThermoSources[0] = new AnomalyThermoSourceDTO
                {
                    EpicenterAUP = field.EpicenterAUP,
                    Heat01 = heat01,
                    Radiation01 = radiation01,
                    RadiusMeters = field.Radius,
                    Pulse01 = sine01,
                    Frame = Frame,
                    Flags = flags
                };
            }

            if (EmitRadarSignal != 0 && radar01 > 0.01f && sine01 > 0.965f)
            {
                SignalBus<RadarJamSignal>.TryEnqueueBounded(RadarJamWriter, RadarJamWriterBudget, new RadarJamSignal
                {
                    Intensity01 = radar01,
                    Frequency = tuning.PulseFrequency,
                    Frame = Frame,
                    SourceHash = SeedShipAnomalyConstants.SourceHash,
                    Phase01 = sine01,
                    DropLock01 = math.saturate(radar01 * 1.25f),
                    Flags = 1
                });
            }

            if (Telemetry.IsCreated && Telemetry.Length > 0)
            {
                int cursor = math.clamp(TelemetryCursor, 0, Telemetry.Length - 1);
                Telemetry[cursor] = new AnomalyTelemetryEntry
                {
                    CurrentCorruptionLevel = field.CorruptionLevel,
                    EntitiesAffected = globals.EntitiesAffected,
                    AnomalyComputeTimeMs = globals.AnomalyComputeTimeMs,
                    GravityY = gravityY,
                    RadarJam01 = radar01,
                    HeatSource01 = heat01,
                    GlobalQualityWeight = tuning.GlobalQualityWeight,
                    Frame = Frame,
                    Flags = flags,
                    StateHash = SeedShipAnomalyMath.HashFrameState(field.CorruptionLevel, globals.EntitiesAffected, Frame),
                    EpicenterAUP = field.EpicenterAUP
                };
            }

            Field[0] = field;
            Globals[0] = globals;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SeedShipLeviathanFrenzyJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AnomalyFieldDTO> Field;
        [ReadOnly, NoAlias] public NativeArray<AnomalyTuningDTO> Tuning;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MockLeviathanState> Leviathans;
        public uint Frame;

        public void Execute(int index)
        {
            if (!Field.IsCreated || Field.Length == 0 ||
                !Tuning.IsCreated || Tuning.Length == 0 ||
                !Leviathans.IsCreated ||
                (uint)index >= (uint)Leviathans.Length)
            {
                return;
            }

            AnomalyFieldDTO field = Field[0];
            AnomalyTuningDTO tuning = Tuning[0];
            MockLeviathanState state = Leviathans[index];
            float corruption = SeedShipAnomalyMath.ResolveCorruption01(state.AUP, field.EpicenterAUP, field.Radius);
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float frenzy = math.saturate(corruption * (0.25f + 0.75f * quality));
            double3 delta64 = state.AUP - field.EpicenterAUP;
            float3 delta = (float3)delta64;
            float distance = math.all(math.isfinite(delta)) ? math.length(delta) : field.Radius;

            state.Corruption01 = corruption;
            state.Frenzy01 = math.max(state.Frenzy01 * 0.96f, frenzy);
            state.AggressionWeight = math.max(state.AggressionWeight * 0.98f, state.Frenzy01 * 10f);
            state.LightAversion = math.saturate(math.lerp(state.LightAversion, 0f, state.Frenzy01));
            state.LastDistanceMeters = distance;
            state.LastFrame = Frame;
            state.Flags = state.Frenzy01 > 0.01f ? (state.Flags | 1u) : (state.Flags & ~1u);
            Leviathans[index] = state;
        }
    }
}
