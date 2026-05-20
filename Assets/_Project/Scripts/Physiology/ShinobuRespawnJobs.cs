using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockRespawnPointsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MedicalBayRespawnPointDTO> MedicalBays;
        public double3 FallbackLifepodAUP;
        public float ValidationClearanceMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)MedicalBays.Length)
                return;

            float clearance = math.max(ValidationClearanceMeters, 1.5f);
            float ring = 9f + ((index & 3) * 2.25f);
            float angle = ((index & 7) * 0.78539816339f) + 0.39269908169f;
            double3 offset = default;
            offset.x = math.cos(angle) * ring;
            offset.y = 1.5f + ((index & 1) * 0.5f);
            offset.z = math.sin(angle) * ring;
            double3 terrainOffset = default;
            terrainOffset.y = clearance + 0.25f;
            MedicalBayRespawnPointDTO bay = default;
            bay.BayAUP = SanitizeAup(FallbackLifepodAUP + offset, FallbackLifepodAUP);
            bay.NearestTerrainAUP = bay.BayAUP - terrainOffset;
            bay.MedicalBayHashID = Hash(index, 0x4D454442u);
            bay.ClearanceMeters = clearance;
            bay.Flags = ShinobuRespawnFlags.MockMedicalBay;
            MedicalBays[index] = bay;
        }

        private static double3 SanitizeAup(double3 value, double3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : (math.all(math.isfinite(fallback)) ? fallback : DefaultFallbackAup());
        }

        private static double3 DefaultFallbackAup()
        {
            double3 fallback = default;
            fallback.y = -18d;
            return fallback;
        }

        private static uint Hash(int index, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? salt : hash;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ResetPlayerPhysiologyJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnStateDTO* RespawnState;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnRequestDTO* RespawnRequest;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MedicalBayRespawnPointDTO* MedicalBays;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnFadeDTO* RespawnFade;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnTelemetryEntry* TelemetryRing;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnTelemetryCursor64* TelemetryCursor;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnTuningDTO* Tuning;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InventoryDeathPenaltyRuleDTO* PenaltyRules;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* PenaltyRuleCount;
        [NativeDisableUnsafePtrRestriction, NoAlias] public PhysiologyDTO* Vitals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DecompressionStateDTO* Decompression;
        [NativeDisableUnsafePtrRestriction, NoAlias] public TissueCompartmentDTO* Tissues;
        [NativeDisableUnsafePtrRestriction, NoAlias] public PhysiologyScalarsDTO* Scalars;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* Metabolism;
        [NativeDisableUnsafePtrRestriction, NoAlias] public LockstepPlayerKinematicState* PlayerKinematic;
        public NativeQueue<InventoryCommandSignal>.ParallelWriter InventoryCommands;
        public int MedicalBayCount;
        public int TissueCount;
        public int PenaltyCapacity;
        public uint Frame;
        public float GlobalQualityWeight;
        public float ScheduleMicroseconds;

        public void Execute()
        {
            if (RespawnState == null || RespawnRequest == null || Tuning == null || RespawnFade == null)
                return;

            RespawnRequestDTO request = *RespawnRequest;
            if ((request.Flags & ShinobuRespawnFlags.PendingRequest) == 0u)
                return;

            float quality = ResolveQuality(GlobalQualityWeight);
            RespawnTuningDTO tuning = SanitizeTuning(*Tuning);
            double3 fallback = SanitizeAup(tuning.FallbackLifepodAUP, DefaultFallbackAup());
            RespawnStateDTO stagedState = *RespawnState;
            uint stagedRouteFlags = stagedState.Flags & (ShinobuRespawnFlags.MockMedicalBay |
                                                         ShinobuRespawnFlags.FallbackLifepod |
                                                         ShinobuRespawnFlags.InvalidTargetAup);
            uint flags = ShinobuRespawnFlags.RespawnActive | ShinobuRespawnFlags.Committed;
            uint medicalBayHash = stagedState.MedicalBayHashID;
            double3 target = stagedState.TargetAUP;
            bool stagedFallback = (stagedState.Flags & ShinobuRespawnFlags.FallbackLifepod) != 0u;
            bool stagedResolved = (stagedState.Flags & ShinobuRespawnFlags.PendingRequest) != 0u &&
                                  math.all(math.isfinite(target)) &&
                                  (medicalBayHash != 0u || stagedFallback);

            if (!stagedResolved)
            {
                target = fallback;
                medicalBayHash = 0u;
                double bestSq = double.MaxValue;
                int count = math.max(0, MedicalBayCount);

                for (int i = 0; i < count; i++)
                {
                    MedicalBayRespawnPointDTO bay = MedicalBays[i];
                    if (!math.all(math.isfinite(bay.BayAUP)))
                        continue;

                    double3 delta = bay.BayAUP - request.DeathAUP;
                    if (!math.all(math.isfinite(delta)))
                        continue;

                    float distanceSq = math.lengthsq(AupDeltaToFloat3(delta));
                    if (!math.isfinite(distanceSq))
                        continue;

                    if (distanceSq >= bestSq)
                        continue;

                    if (!ValidateMedicalBay(in bay, tuning.ValidationClearanceMeters))
                    {
                        flags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    bestSq = distanceSq;
                    target = bay.BayAUP;
                    medicalBayHash = bay.MedicalBayHashID;
                    flags |= bay.Flags & ShinobuRespawnFlags.MockMedicalBay;
                }

                if (!math.all(math.isfinite(target)) || medicalBayHash == 0u)
                {
                    target = fallback;
                    flags |= ShinobuRespawnFlags.FallbackLifepod;
                }
            }
            else
            {
                flags |= stagedRouteFlags;
            }

            WritePhysiology(request.PlayerHash);
            WriteKinematic(target);
            flags |= EmitInventoryPenalty(request);
            WriteFade(quality, tuning, flags);
            WriteTelemetry(request.DeathAUP, target, request.DamageHash, flags);

            RespawnStateDTO committedState = default;
            committedState.TargetAUP = target;
            committedState.MedicalBayHashID = medicalBayHash;
            committedState.Flags = flags;
            *RespawnState = committedState;

            request.Flags = ShinobuRespawnFlags.Committed | (flags & (ShinobuRespawnFlags.MockMedicalBay | ShinobuRespawnFlags.InvalidTargetAup | ShinobuRespawnFlags.FallbackLifepod | ShinobuRespawnFlags.PenaltyApplied));
            request.MedicalBayHashID = medicalBayHash;
            *RespawnRequest = request;
        }

        private void WritePhysiology(uint playerHash)
        {
            if (Vitals != null)
            {
                PhysiologyDTO vitals = default;
                vitals.BloodOxygen = 1f;
                vitals.TissueNitrogen = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
                vitals.CoreTemperature = 37f;
                vitals.ActiveTraumaMask = 0u;
                vitals.HeartRate = 72f;
                vitals.Adrenaline = 0f;
                *Vitals = vitals;
            }

            if (Decompression != null)
            {
                DecompressionStateDTO state = default;
                for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                    state.TissueTensions[i] = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
                state.AmbientPressure = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
                state.AscentRate = 0f;
                *Decompression = state;
            }

            if (Tissues != null)
            {
                int count = math.min(math.max(0, TissueCount), ShinobuPhysiologyConstants.TissueCompartmentCount);
                for (int i = 0; i < count; i++)
                {
                    TissueCompartmentDTO tissue = Tissues[i];
                    tissue.NitrogenTension = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
                    tissue.Halftime = math.isfinite(tissue.Halftime) && tissue.Halftime > 0f ? tissue.Halftime : 5f + (i * 3f);
                    tissue.MValue = math.isfinite(tissue.MValue) && tissue.MValue > 0f ? tissue.MValue : 1.58f;
                    tissue.Flags = 0u;
                    Tissues[i] = tissue;
                }
            }

            if (Scalars != null)
            {
                PhysiologyScalarsDTO scalars = default;
                scalars.FatigueMultiplier = 1f;
                scalars.OxygenDrainPerSecond = 0f;
                scalars.StatusFlags = 0u;
                *Scalars = scalars;
            }

            if (Metabolism != null)
            {
                MetabolicStateDTO metabolism = default;
                metabolism.Calories = 1f;
                metabolism.Hydration = 1f;
                metabolism.CoreTemperature = 37f;
                metabolism.Toxicity = 0f;
                metabolism.EntityHashID = playerHash;
                metabolism.Flags = 0u;
                *Metabolism = metabolism;
            }
        }

        private void WriteKinematic(double3 target)
        {
            if (PlayerKinematic == null)
                return;

            LockstepPlayerKinematicState state = *PlayerKinematic;
            double sectorSize = math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d);
            double3 sector = math.floor(target / sectorSize);
            state.SectorX = (long)sector.x;
            state.SectorY = (long)sector.y;
            state.SectorZ = (long)sector.z;
            float3 localPosition = default;
            localPosition.x = SafeLocal(target.x - (state.SectorX * sectorSize));
            localPosition.y = SafeLocal(target.y - (state.SectorY * sectorSize));
            localPosition.z = SafeLocal(target.z - (state.SectorZ * sectorSize));
            state.LocalPosition = localPosition;
            state.Velocity = float3.zero;
            state.Forward = ResolveForward(state.Forward);
            state.Frame = Frame;
            state.Flags |= ShinobuRespawnFlags.Committed;
            *PlayerKinematic = state;
        }

        private void WriteFade(float quality, RespawnTuningDTO tuning, uint flags)
        {
            float fadeRate = math.lerp(
                math.max(0.0001f, tuning.HighQualityFadeRate),
                math.max(0.0001f, tuning.LowQualityFadeRate),
                1f - quality);
            float detailGate = Smooth01(math.saturate((quality - 0.18f) * 1.6129032f));
            RespawnFadeDTO fade = default;
            fade.DeathFadeIntensity = 1f;
            fade.FadeRate = fadeRate;
            fade.ChromaticAberration01 = math.saturate(math.lerp(0f, 0.85f, detailGate));
            fade.FilmGrain01 = math.saturate(math.lerp(0.25f, 1f, quality));
            fade.GlobalQualityWeight = quality;
            fade.Frame = Frame;
            fade.Flags = flags;
            *RespawnFade = fade;
        }

        private void WriteTelemetry(double3 deathAup, double3 respawnAup, uint causeHash, uint flags)
        {
            if (TelemetryRing == null || TelemetryCursor == null)
                return;

            RespawnTelemetryCursor64 cursor = *TelemetryCursor;
            int index = cursor.Cursor % ShinobuRespawnConstants.TelemetryFrameCount;
            if (index < 0)
                index += ShinobuRespawnConstants.TelemetryFrameCount;

            RespawnTelemetryEntry entry = default;
            entry.DeathAUP = SanitizeAup(deathAup, DefaultFallbackAup());
            entry.RespawnAUP = SanitizeAup(respawnAup, DefaultFallbackAup());
            entry.CauseHash = causeHash;
            entry.Frame = Frame;
            entry.ReconcileMicroseconds = math.max(0f, ScheduleMicroseconds);
            entry.Flags = flags;
            TelemetryRing[index] = entry;
            cursor.Cursor = (index + 1) % ShinobuRespawnConstants.TelemetryFrameCount;
            cursor.Flags = flags & (ShinobuRespawnFlags.NanDetected | ShinobuRespawnFlags.InvalidTargetAup);
            *TelemetryCursor = cursor;
        }

        private uint EmitInventoryPenalty(RespawnRequestDTO request)
        {
            int count = PenaltyRuleCount != null ? math.min(math.max(0, *PenaltyRuleCount), PenaltyCapacity) : 0;
            RespawnTuningDTO tuning = SanitizeTuning(*Tuning);
            if (tuning.PenaltyMultiplier <= 0.0001f)
                return 0u;

            byte emit = (byte)(count <= 0 ? 1 : 0);
            if (PenaltyRules != null)
            {
                for (int i = 0; i < count; i++)
                    emit |= PenaltyRules[i].DropOnDeath;
            }

            if (emit == 0)
                return 0u;

            InventoryCommandSignal command = default;
            command.InventoryHash = request.PlayerHash;
            command.Frame = Frame;
            command.Sequence = request.Sequence;
            command.Command = InventoryCommandSignalCommands.DropNonEquippedResources;
            command.Flags = (byte)math.min(255, (int)math.round(tuning.PenaltyMultiplier * 255f));
            if (count > 0)
            {
                command.PayloadFlags = InventoryCommandSignalPayloadFlags.VaultPenaltyRules;
                command.Payload0 = (uint)ShinobuRespawnConstants.RespawnPenaltyRulesBuffer;
                command.Payload1 = (uint)count;
                command.Payload2 = (uint)PenaltyCapacity;
                command.Payload3 = ShinobuRespawnConstants.SourceHash;
            }
            else
            {
                command.PayloadFlags = InventoryCommandSignalPayloadFlags.FallbackWhenRuleTableMissing;
            }

            InventoryCommands.Enqueue(command);
            return ShinobuRespawnFlags.PenaltyApplied;
        }

        private static bool ValidateMedicalBay(in MedicalBayRespawnPointDTO bay, float clearanceMeters)
        {
            double3 delta = bay.BayAUP - bay.NearestTerrainAUP;
            if (!math.all(math.isfinite(delta)))
                return false;

            float3 local = AupDeltaToFloat3(delta);
            float distanceSq = math.lengthsq(local);
            float clearance = math.max(math.max(clearanceMeters, bay.ClearanceMeters), 0.25f);
            return math.isfinite(distanceSq) && distanceSq >= clearance * clearance;
        }

        private static RespawnTuningDTO SanitizeTuning(RespawnTuningDTO tuning)
        {
            tuning.FallbackLifepodAUP = SanitizeAup(tuning.FallbackLifepodAUP, DefaultFallbackAup());
            tuning.HighQualityFadeRate = math.clamp(FiniteOr(tuning.HighQualityFadeRate, 0.5f), 0.0001f, 16f);
            tuning.LowQualityFadeRate = math.clamp(FiniteOr(tuning.LowQualityFadeRate, 2f), 0.0001f, 16f);
            tuning.PenaltyMultiplier = math.saturate(FiniteOr(tuning.PenaltyMultiplier, 1f));
            tuning.ValidationClearanceMeters = math.clamp(FiniteOr(tuning.ValidationClearanceMeters, 1.5f), 0.25f, 16f);
            return tuning;
        }

        private static double3 SanitizeAup(double3 value, double3 fallback)
        {
            if (math.all(math.isfinite(value)))
                return value;
            return math.all(math.isfinite(fallback)) ? fallback : DefaultFallbackAup();
        }

        private static double3 DefaultFallbackAup()
        {
            double3 fallback = default;
            fallback.y = -18d;
            return fallback;
        }

        private static float ResolveQuality(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SafeLocal(double value)
        {
            if (!math.isfinite(value))
                return 0f;
            double clamp = SafeAupClampMeters();
            return (float)math.clamp(value, -clamp, clamp);
        }

        private static float3 AupDeltaToFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            float3 result = default;
            result.x = SafeLocal(delta.x);
            result.y = SafeLocal(delta.y);
            result.z = SafeLocal(delta.z);
            return result;
        }

        private static double SafeAupClampMeters()
        {
            return math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d);
        }

        private static float3 ResolveForward(float3 forward)
        {
            float lengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(forward)) || !math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            {
                float3 fallback = default;
                fallback.z = 1f;
                return fallback;
            }
            return forward * math.rsqrt(math.max(lengthSq, 0.0001f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateRespawnFadeJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnStateDTO* RespawnState;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnFadeDTO* RespawnFade;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RespawnTuningDTO* Tuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute()
        {
            if (RespawnFade == null || RespawnState == null || Tuning == null)
                return;

            RespawnFadeDTO fade = *RespawnFade;
            RespawnStateDTO state = *RespawnState;
            RespawnTuningDTO tuning = *Tuning;
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float highRate = math.clamp(math.isfinite(tuning.HighQualityFadeRate) ? tuning.HighQualityFadeRate : 0.5f, 0.0001f, 16f);
            float lowRate = math.clamp(math.isfinite(tuning.LowQualityFadeRate) ? tuning.LowQualityFadeRate : 2f, 0.0001f, 16f);
            float rate = math.lerp(highRate, lowRate, 1f - quality);
            float dt = math.clamp(math.isfinite(DeltaSeconds) ? DeltaSeconds : 0f, 0f, 0.1f);

            fade.DeathFadeIntensity = math.max(0f, fade.DeathFadeIntensity - (rate * dt));
            fade.FadeRate = rate;
            fade.GlobalQualityWeight = quality;
            fade.Frame = Frame;
            fade.ChromaticAberration01 = math.saturate(fade.DeathFadeIntensity * Smooth01((quality - 0.18f) * 1.6129032f));
            fade.FilmGrain01 = math.saturate(fade.DeathFadeIntensity * math.lerp(0.2f, 1f, quality));

            if (fade.DeathFadeIntensity <= 0.0001f)
            {
                fade.DeathFadeIntensity = 0f;
                fade.Flags &= ~ShinobuRespawnFlags.RespawnActive;
                state.Flags &= ~ShinobuRespawnFlags.RespawnActive;
            }

            *RespawnFade = fade;
            *RespawnState = state;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }
    }
}
