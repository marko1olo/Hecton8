using System;

namespace Hecton.Localization
{
    /// <summary>
    /// Built-in VWS caption fallback catalog used only when the Babel runtime table has no caption entry.
    /// </summary>
    internal static class VwsCaptionFallbackCatalog
    {
        public const uint LowPowerCaptionHash = 0x9806014Eu;
        public const uint LifeSupportCaptionHash = 0x8140980Fu;
        public const uint MultiFailureCaptionHash = 0x386392CBu;
        public const uint EmergencyDangerCaptionHash = 0xE984BC39u;
        public const uint AbandonShipCaptionHash = 0x5CEF8426u;
        public const uint HostileDroneCaptionHash = 0x964F3F39u;
        public const uint OxygenLowCaptionHash = 0x12DCC423u;
        public const uint OxygenCriticalCaptionHash = 0xAE1F0464u;
        public const uint HullBreachCaptionHash = 0x723B50A7u;
        public const uint PressureHighCaptionHash = 0xEE32D0F3u;
        public const uint ThermalStressCaptionHash = 0x585A5256u;

        public static bool TryResolveCaptionTextSpan(uint captionHashId, out ReadOnlySpan<char> captionText)
        {
            if (captionHashId == LowPowerCaptionHash)
            {
                captionText = "SUBMARINE LOW POWER".AsSpan();
                return true;
            }

            if (captionHashId == LifeSupportCaptionHash)
            {
                captionText = "LIFE SUPPORT CRITICAL".AsSpan();
                return true;
            }

            if (captionHashId == MultiFailureCaptionHash)
            {
                captionText = "MULTIPLE SYSTEM FAILURES".AsSpan();
                return true;
            }

            if (captionHashId == EmergencyDangerCaptionHash)
            {
                captionText = "EMERGENCY LEVEL DANGER".AsSpan();
                return true;
            }

            if (captionHashId == AbandonShipCaptionHash)
            {
                captionText = "ABANDON SHIP".AsSpan();
                return true;
            }

            if (captionHashId == HostileDroneCaptionHash)
            {
                captionText = "HOSTILE DRONE DETECTED".AsSpan();
                return true;
            }

            if (captionHashId == OxygenLowCaptionHash)
            {
                captionText = "OXYGEN LOW".AsSpan();
                return true;
            }

            if (captionHashId == OxygenCriticalCaptionHash)
            {
                captionText = "OXYGEN CRITICAL".AsSpan();
                return true;
            }

            if (captionHashId == HullBreachCaptionHash)
            {
                captionText = "HULL BREACH".AsSpan();
                return true;
            }

            if (captionHashId == PressureHighCaptionHash)
            {
                captionText = "HULL PRESSURE HIGH".AsSpan();
                return true;
            }

            if (captionHashId == ThermalStressCaptionHash)
            {
                captionText = "THERMAL STRESS".AsSpan();
                return true;
            }

            captionText = ReadOnlySpan<char>.Empty;
            return false;
        }
    }
}
