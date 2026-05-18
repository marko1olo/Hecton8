using Hecton8.Global.Contracts;
using UnityEngine;

namespace Hecton8.Global.FutureSeams.Authoring
{
    /// <summary>
    /// Human-readable authoring facade for dormant future system seams.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FutureSystemSeamProfile",
        menuName = "Hecton8/Architecture/Future System Seam Profile",
        order = 920)]
    public sealed class FutureSystemSeamProfile : ScriptableObject
    {
        public const int DefaultReservationCount = 7;

        [Header("Reservations")]
        [Tooltip("Reserved future surfaces. This asset does not activate runtime systems.")]
        [SerializeField]
        private FutureRuntimeSurface[] reservedSurfaces =
        {
            FutureRuntimeSurface.SurvivalOverride,
            FutureRuntimeSurface.HapticPulse,
            FutureRuntimeSurface.SubtitleCue,
            FutureRuntimeSurface.TelemetryMarker,
            FutureRuntimeSurface.QaScenarioMarker,
            FutureRuntimeSurface.ChunkInterestHint,
            FutureRuntimeSurface.SaveHashProbe
        };

        [Header("Survival Override Preview")]
        [Tooltip("Preview TTL for the future survival override payload. Runtime still clamps to 3000 ms.")]
        [SerializeField, Range(0, FutureSystemSeamContracts.SurvivalOverrideMaxTtlMs)]
        private int survivalTtlMilliseconds = FutureSystemSeamContracts.SurvivalOverrideMaxTtlMs;

        [Tooltip("Preview override flags. Bits outside the reserved mask are stripped.")]
        [SerializeField]
        private ushort survivalOverrideFlags = 1;

        [Tooltip("Preview oxygen floor used only to validate payload packing.")]
        [SerializeField, Range(0f, 1f)]
        private float oxygenFloor01 = 1f;

        [Header("Binary Export Contract")]
        [Tooltip("Expected blackbox frames per future owner ring.")]
        [SerializeField, Range(300, 300)]
        private int blackboxFrameCount = FutureSystemSeamContracts.RequiredBlackboxFrames;

        /// <summary>Number of authoring rows currently serialized in this facade.</summary>
        public int SurfaceCount => reservedSurfaces != null ? reservedSurfaces.Length : 0;

        /// <summary>Preview TTL after contract clamping.</summary>
        public ushort SurvivalTtlMilliseconds => ClampTtl(survivalTtlMilliseconds);

        /// <summary>Preview oxygen floor after finite-range clamping.</summary>
        public float OxygenFloor01 => Clamp01(oxygenFloor01);

        /// <summary>Expected owner blackbox ring capacity for every exported reservation.</summary>
        public int BlackboxFrameCount => blackboxFrameCount;

        /// <summary>Returns a reserved surface by index, or None when out of range.</summary>
        public FutureRuntimeSurface GetSurface(int index)
        {
            return reservedSurfaces != null && index >= 0 && index < reservedSurfaces.Length
                ? reservedSurfaces[index]
                : FutureRuntimeSurface.None;
        }

        /// <summary>Copies validated reservation records into caller-owned storage.</summary>
        public int BuildRecords(FutureSystemSeamRecord64[] destination)
        {
            if (destination == null || reservedSurfaces == null)
                return 0;

            int count = 0;
            int limit = reservedSurfaces.Length < destination.Length ? reservedSurfaces.Length : destination.Length;
            for (int i = 0; i < limit; i++)
            {
                FutureRuntimeSurface surface = reservedSurfaces[i];
                if (!FutureSystemSeamContracts.TryBuildReservation(surface, out FutureSystemSeamRecord64 record))
                    continue;

                record.BlackboxCapacity = unchecked((uint)BlackboxFrameCount);
                if (FutureSystemSeamContracts.ValidateReservation(in record) != FutureSeamValidationError.None)
                    continue;

                destination[count++] = record;
            }

            return count;
        }

        /// <summary>Validates serialized rows against the fixed contract-only reservation rules.</summary>
        public FutureSeamValidationError ValidateProfile()
        {
            FutureSeamValidationError errors = FutureSeamValidationError.None;
            if (reservedSurfaces == null || reservedSurfaces.Length == 0)
                return FutureSeamValidationError.MissingSurface;

            for (int i = 0; i < reservedSurfaces.Length; i++)
            {
                FutureRuntimeSurface surface = reservedSurfaces[i];
                if (!FutureSystemSeamContracts.TryBuildReservation(surface, out FutureSystemSeamRecord64 record))
                {
                    errors |= FutureSeamValidationError.MissingSurface;
                    continue;
                }

                errors |= FutureSystemSeamContracts.ValidateReservation(in record);
            }

            FutureCommandEnvelope64 preview = BuildSurvivalOverridePreview(0u, 1u, 0u);
            errors |= FutureSystemSeamContracts.ValidateSurvivalOverrideEnvelope(in preview);
            return errors;
        }

        /// <summary>Builds a dormant preview command packet for editor validation.</summary>
        public FutureCommandEnvelope64 BuildSurvivalOverridePreview(uint modHash, uint requestId, uint targetPlayerHash)
        {
            return FutureSystemSeamContracts.BuildSurvivalOverrideEnvelope(
                modHash,
                requestId,
                targetPlayerHash,
                ClampTtl(survivalTtlMilliseconds),
                unchecked((ushort)(survivalOverrideFlags & FutureSystemSeamContracts.SurvivalOverrideAllowedFlags)),
                Clamp01(oxygenFloor01));
        }

        /// <summary>Restores the seven current non-public reservation rows.</summary>
        public void SeedDefaultSurfaces()
        {
            // COLD ALLOC: FutureRuntimeSurface[7] - designer-authored dormant reservation list - owner: FutureSystemSeamProfile
            reservedSurfaces = new FutureRuntimeSurface[DefaultReservationCount];
            reservedSurfaces[0] = FutureRuntimeSurface.SurvivalOverride;
            reservedSurfaces[1] = FutureRuntimeSurface.HapticPulse;
            reservedSurfaces[2] = FutureRuntimeSurface.SubtitleCue;
            reservedSurfaces[3] = FutureRuntimeSurface.TelemetryMarker;
            reservedSurfaces[4] = FutureRuntimeSurface.QaScenarioMarker;
            reservedSurfaces[5] = FutureRuntimeSurface.ChunkInterestHint;
            reservedSurfaces[6] = FutureRuntimeSurface.SaveHashProbe;
            Sanitize();
        }

        private void Reset()
        {
            SeedDefaultSurfaces();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        private void Sanitize()
        {
            if (reservedSurfaces == null || reservedSurfaces.Length == 0)
                SeedDefaultSurfaces();

            survivalTtlMilliseconds = ClampTtl(survivalTtlMilliseconds);
            survivalOverrideFlags = unchecked((ushort)(survivalOverrideFlags & FutureSystemSeamContracts.SurvivalOverrideAllowedFlags));
            oxygenFloor01 = Clamp01(oxygenFloor01);
            blackboxFrameCount = FutureSystemSeamContracts.RequiredBlackboxFrames;
        }

        private static ushort ClampTtl(int ttlMilliseconds)
        {
            if (ttlMilliseconds < 0)
                return 0;

            return ttlMilliseconds > FutureSystemSeamContracts.SurvivalOverrideMaxTtlMs
                ? FutureSystemSeamContracts.SurvivalOverrideMaxTtlMs
                : unchecked((ushort)ttlMilliseconds);
        }

        private static float Clamp01(float value)
        {
            if (!(value >= 0f))
                return 0f;

            return value > 1f ? 1f : value;
        }
    }
}
