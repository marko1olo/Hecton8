namespace Hecton8.Core.Contracts
{
    public static class HectonEditorBreadcrumbContract
    {
        public const ushort IconUnknown = 0;
        public const ushort IconPlayer = 1;
        public const ushort IconObjective = 2;
        public const ushort IconHazard = 3;
        public const ushort IconLore = 4;
        public const ushort IconResource = 5;
        public const ushort IconBase = 6;
        public const ushort IconVehicle = 7;
        public const ushort IconSignal = 8;
        public const uint ColorUnknownRgba = 0x9AA3ADFFu;
        public const uint ColorPlayerRgba = 0x4EC9FFFFu;
        public const uint ColorObjectiveRgba = 0xFFE066FFu;
        public const uint ColorHazardRgba = 0xFF4D4DFFu;
        public const uint ColorLoreRgba = 0xB58CFFFFu;
        public const uint ColorResourceRgba = 0x5DFFB1FFu;
        public const uint ColorBaseRgba = 0x7CD1FFFFu;
        public const uint ColorVehicleRgba = 0xC4D7E8FFu;
        public const uint ColorSignalRgba = 0xFFB86BFFu;
        public const float DefaultWorldMarkerRadiusMeters = 1.25f;
        public const float DefaultHudMarkerFadeInMeters = 16f;
        public const float DefaultHudMarkerFadeOutMeters = 128f;

        static HectonEditorBreadcrumbContract()
        {
            HectonContractValidator.RequirePositive(DefaultWorldMarkerRadiusMeters, nameof(DefaultWorldMarkerRadiusMeters));
            HectonContractValidator.RequirePositive(DefaultHudMarkerFadeInMeters, nameof(DefaultHudMarkerFadeInMeters));
            HectonContractValidator.RequirePositive(DefaultHudMarkerFadeOutMeters, nameof(DefaultHudMarkerFadeOutMeters));
        }
    }
}
