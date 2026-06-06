using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Atlas6Liability
{
    /// <summary>
    /// Varnek Protocol: Thermal Sheer Masking
    /// Smooths a 14% risk variance to a 4% variance to avoid halting extraction.
    /// UI deliberately lies to the player. Sensible feedback (groans, micro-fractures) 
    /// is the only way to know the true state. Downgrades alerts.
    /// </summary>
    public sealed class ThermalSheerManager
    {
        public const uint TelemetryFlagMasked = 1u << 0;
        public const uint TelemetryFlagCriticalDowngraded = 1u << 1;
        public const byte AlertClassNominal = 0;
        public const byte AlertClassCritical = 3;
        public const byte AlertClassDowngraded = 4;

        private const float TrueVariance = 0.14f;
        private const float ReportedVariance = 0.04f;
        private const float CriticalSheerThreshold01 = 0.8f;
        private const float MaskingDistanceThreshold = 150f; // Distance from drill site where masking is active

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct TelemetryReadout
        {
            [FieldOffset(0)] public float ReportedSheer;
            [FieldOffset(4)] public float TrueSheer;
            [FieldOffset(8)] public float MaskDelta01;
            [FieldOffset(12)] public float DistanceToDrillSiteMeters;
            [FieldOffset(16)] public uint Flags;
            [FieldOffset(20)] public byte AlertClass;
            [FieldOffset(21)] private byte _pad0;
            [FieldOffset(22)] private ushort _pad1;

            public readonly bool IsMasked => (Flags & TelemetryFlagMasked) != 0u;
        }

        /// <summary>
        /// Calculates the telemetry to feed into the HectonSubmarineOS.
        /// The OS will naturally display ReportedSheer, requiring the player to rely on 
        /// acoustic cues linked to TrueSheer.
        /// </summary>
        public TelemetryReadout CalculateTelemetry(float actualSheer, float distanceToDrillSite)
        {
            float safeSheer = math.isfinite(actualSheer) ? math.saturate(actualSheer) : 0f;
            float safeDistance = math.isfinite(distanceToDrillSite) ? math.max(0f, distanceToDrillSite) : float.MaxValue;
            bool isMasked = safeDistance <= MaskingDistanceThreshold;

            float reportedSheer = safeSheer;
            float maskDelta01 = 0f;
            uint flags = 0u;
            byte alertClass = safeSheer > CriticalSheerThreshold01 ? AlertClassCritical : AlertClassNominal;

            if (isMasked)
            {
                // Smooth out the 14% danger to a 4% danger, lying to the player by 10%
                float deviation = TrueVariance - ReportedVariance;
                reportedSheer = math.max(0f, safeSheer - (safeSheer * deviation));
                maskDelta01 = math.max(0f, safeSheer - reportedSheer);
                flags |= TelemetryFlagMasked;

                // Downgrade the alert classification to avoid halting extraction
                if (safeSheer > CriticalSheerThreshold01)
                {
                    alertClass = AlertClassDowngraded;
                    flags |= TelemetryFlagCriticalDowngraded;
                }
            }

            return new TelemetryReadout
            {
                ReportedSheer = reportedSheer,
                TrueSheer = safeSheer,
                MaskDelta01 = maskDelta01,
                DistanceToDrillSiteMeters = safeDistance,
                Flags = flags,
                AlertClass = alertClass
            };
        }

        /// <summary>
        /// Exposes the true sheer exclusively for the AcousticSensoryXRayWindow and audio engine.
        /// Players must listen to groaning metal instead of looking at the green UI.
        /// </summary>
        public float GetTrueSensoryFeedback(float actualSheer)
        {
            return math.isfinite(actualSheer) ? math.saturate(actualSheer) : 0f;
        }
    }
}
