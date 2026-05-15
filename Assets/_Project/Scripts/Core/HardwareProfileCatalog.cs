using System.Runtime.CompilerServices;

namespace Hecton8.Core
{
    /// <summary>
    /// Stable hardware profile IDs mirrored from Data/Hardware/Profiles.json.
    /// </summary>
    public enum HardwareProfileKind : byte
    {
        /// <summary>Meta Quest 3 Android XR UMA profile.</summary>
        Quest3 = 0,

        /// <summary>Steam Deck LCD SteamOS handheld UMA profile.</summary>
        SteamDeckLcd = 1
    }

    /// <summary>
    /// Runtime execution phases mirrored from the hardware profile row-major budget table.
    /// </summary>
    public enum HardwareProfilePhase : byte
    {
        /// <summary>Input, dependency, and pre-simulation admission phase.</summary>
        PreSimulation = 0,

        /// <summary>Main simulation phase.</summary>
        Simulation = 1,

        /// <summary>Post-simulation sync and cleanup phase.</summary>
        PostSimulation = 2,

        /// <summary>Presentation and visual synchronization phase.</summary>
        VisualSync = 3
    }

    /// <summary>
    /// Pressure levels mirrored from the homeostasis kill-switch mask table.
    /// </summary>
    public enum HardwarePressureLevel : byte
    {
        /// <summary>No pressure mask active.</summary>
        Clear = 0,

        /// <summary>Presentation-only sacrifice level.</summary>
        SacrificeLevel1 = 1,

        /// <summary>Presentation plus non-critical simulation sacrifice level.</summary>
        SacrificeLevel2 = 2,

        /// <summary>Emergency visual and cadence sacrifice level.</summary>
        EmergencyLevel3 = 3
    }

    /// <summary>
    /// Allocation-free generated constants for hardware homeostasis and UMA budget consumers.
    /// </summary>
    public static class HardwareProfileCatalog
    {
        /// <summary>Flat profile row count in Data/Hardware/Profiles.json.</summary>
        public const int ProfileCount = 2;

        /// <summary>Flat phase count in Data/Hardware/Profiles.json.</summary>
        public const int PhaseCount = 4;

        /// <summary>FNV-1a 32-bit hash of QUEST_3.</summary>
        public const uint Quest3StableHash32 = 1478646863u;

        /// <summary>FNV-1a 32-bit hash of STEAM_DECK_LCD.</summary>
        public const uint SteamDeckLcdStableHash32 = 1871614729u;

        /// <summary>Quest 3 project graphics budget on unified memory.</summary>
        public const int Quest3GraphicsBudgetMegabytes = 1536;

        /// <summary>Steam Deck LCD project graphics budget on unified memory.</summary>
        public const int SteamDeckLcdGraphicsBudgetMegabytes = 4096;

        /// <summary>Quest 3 texture residency budget.</summary>
        public const int Quest3TextureBudgetMegabytes = 768;

        /// <summary>Steam Deck LCD texture residency budget.</summary>
        public const int SteamDeckLcdTextureBudgetMegabytes = 2048;

        /// <summary>Quest 3 render target and depth budget.</summary>
        public const int Quest3RenderTargetBudgetMegabytes = 240;

        /// <summary>Steam Deck LCD render target and depth budget.</summary>
        public const int SteamDeckLcdRenderTargetBudgetMegabytes = 384;

        /// <summary>Quest 3 sustained project target frame rate.</summary>
        public const int Quest3TargetFps = 72;

        /// <summary>Steam Deck LCD sustained project target frame rate.</summary>
        public const int SteamDeckLcdTargetFps = 60;

        /// <summary>Quest 3 baseline dynamic-resolution scale in thousandths.</summary>
        public const int Quest3BaselineRenderScaleMilli = 850;

        /// <summary>Steam Deck LCD baseline dynamic-resolution scale in thousandths.</summary>
        public const int SteamDeckLcdBaselineRenderScaleMilli = 780;

        /// <summary>Quest 3 conservative job worker budget.</summary>
        public const int Quest3JobWorkerBudget = 4;

        /// <summary>Steam Deck LCD conservative job worker budget.</summary>
        public const int SteamDeckLcdJobWorkerBudget = 6;

        /// <summary>Shared compute group size used by both generated hardware profiles.</summary>
        public const int DefaultComputeGroupThreads = 64;

        /// <summary>Clear pressure mask.</summary>
        public const ulong ClearPressureMask = 0x0000000000000000UL;

        /// <summary>Level 1 sacrifice mask.</summary>
        public const ulong SacrificeLevel1Mask = 0x0000000000000070UL;

        /// <summary>Level 2 sacrifice mask.</summary>
        public const ulong SacrificeLevel2Mask = 0x00000000002007F0UL;

        /// <summary>Emergency level 3 sacrifice mask.</summary>
        public const ulong EmergencyLevel3Mask = 0x0000000000F017F0UL;

        /// <summary>
        /// Resolves a generated profile from its FNV-1a stable hash.
        /// </summary>
        /// <param name="stableHash32">Profile hash from Data/Hardware/Profiles.json.</param>
        /// <param name="profile">Resolved profile kind.</param>
        /// <returns>True when the hash maps to a generated profile.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveProfileByStableHash(uint stableHash32, out HardwareProfileKind profile)
        {
            switch (stableHash32)
            {
                case Quest3StableHash32:
                    profile = HardwareProfileKind.Quest3;
                    return true;
                case SteamDeckLcdStableHash32:
                    profile = HardwareProfileKind.SteamDeckLcd;
                    return true;
                default:
                    profile = HardwareProfileKind.Quest3;
                    return false;
            }
        }

        /// <summary>
        /// Resolves the project graphics budget for a generated profile.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Graphics memory budget in megabytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveGraphicsBudgetMegabytes(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdGraphicsBudgetMegabytes
                : Quest3GraphicsBudgetMegabytes;
        }

        /// <summary>
        /// Resolves the project texture budget for a generated profile.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Texture memory budget in megabytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveTextureBudgetMegabytes(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdTextureBudgetMegabytes
                : Quest3TextureBudgetMegabytes;
        }

        /// <summary>
        /// Resolves the project render target budget for a generated profile.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Render target and depth memory budget in megabytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRenderTargetBudgetMegabytes(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdRenderTargetBudgetMegabytes
                : Quest3RenderTargetBudgetMegabytes;
        }

        /// <summary>
        /// Resolves the sustained project target frame rate for a generated profile.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Target frames per second.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveTargetFps(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdTargetFps
                : Quest3TargetFps;
        }

        /// <summary>
        /// Resolves the baseline dynamic-resolution scale encoded as thousandths.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Render scale multiplied by 1000.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBaselineRenderScaleMilli(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdBaselineRenderScaleMilli
                : Quest3BaselineRenderScaleMilli;
        }

        /// <summary>
        /// Resolves the conservative worker budget for generated profile hardware.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <returns>Job worker count budget.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveJobWorkerBudget(HardwareProfileKind profile)
        {
            return profile == HardwareProfileKind.SteamDeckLcd
                ? SteamDeckLcdJobWorkerBudget
                : Quest3JobWorkerBudget;
        }

        /// <summary>
        /// Resolves the shared-memory graphics budget used by profile-aware UMA detection.
        /// </summary>
        /// <param name="steamDeckLike">True when the detected platform matches Steam Deck signatures.</param>
        /// <param name="quest3Like">True when the detected platform matches Quest 3 signatures.</param>
        /// <param name="fallbackMegabytes">Fallback budget for unknown shared-memory devices.</param>
        /// <returns>UMA graphics budget in megabytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSharedMemoryGraphicsBudgetMegabytes(
            bool steamDeckLike,
            bool quest3Like,
            int fallbackMegabytes)
        {
            if (steamDeckLike)
                return SteamDeckLcdGraphicsBudgetMegabytes;
            if (quest3Like)
                return Quest3GraphicsBudgetMegabytes;

            return fallbackMegabytes > 0 ? fallbackMegabytes : Quest3GraphicsBudgetMegabytes;
        }

        /// <summary>
        /// Resolves the shared-memory texture budget used by profile-aware UMA consumers.
        /// </summary>
        /// <param name="steamDeckLike">True when the detected platform matches Steam Deck signatures.</param>
        /// <param name="quest3Like">True when the detected platform matches Quest 3 signatures.</param>
        /// <param name="fallbackMegabytes">Fallback budget for unknown shared-memory devices.</param>
        /// <returns>Texture budget in megabytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSharedMemoryTextureBudgetMegabytes(
            bool steamDeckLike,
            bool quest3Like,
            int fallbackMegabytes)
        {
            if (steamDeckLike)
                return SteamDeckLcdTextureBudgetMegabytes;
            if (quest3Like)
                return Quest3TextureBudgetMegabytes;

            return fallbackMegabytes > 0 ? fallbackMegabytes : Quest3TextureBudgetMegabytes;
        }

        /// <summary>
        /// Resolves the generated homeostasis mask for a pressure level.
        /// </summary>
        /// <param name="pressureLevel">Hardware pressure level.</param>
        /// <returns>Kill-switch bit mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ResolvePressureMask(HardwarePressureLevel pressureLevel)
        {
            switch (pressureLevel)
            {
                case HardwarePressureLevel.SacrificeLevel1:
                    return SacrificeLevel1Mask;
                case HardwarePressureLevel.SacrificeLevel2:
                    return SacrificeLevel2Mask;
                case HardwarePressureLevel.EmergencyLevel3:
                    return EmergencyLevel3Mask;
                default:
                    return ClearPressureMask;
            }
        }

        /// <summary>
        /// Resolves a generated phase watchdog budget.
        /// </summary>
        /// <param name="profile">Generated profile kind.</param>
        /// <param name="phase">Execution phase.</param>
        /// <returns>Budget in milliseconds.</returns>
        public static float ResolvePhaseBudgetMilliseconds(HardwareProfileKind profile, HardwareProfilePhase phase)
        {
            if (profile == HardwareProfileKind.SteamDeckLcd)
                return ResolveSteamDeckPhaseBudgetMilliseconds(phase);

            return ResolveQuest3PhaseBudgetMilliseconds(phase);
        }

        private static float ResolveQuest3PhaseBudgetMilliseconds(HardwareProfilePhase phase)
        {
            switch (phase)
            {
                case HardwareProfilePhase.PreSimulation:
                    return 0.3f;
                case HardwareProfilePhase.Simulation:
                    return 2.6f;
                case HardwareProfilePhase.PostSimulation:
                    return 0.45f;
                case HardwareProfilePhase.VisualSync:
                    return 1.6f;
                default:
                    return 0f;
            }
        }

        private static float ResolveSteamDeckPhaseBudgetMilliseconds(HardwareProfilePhase phase)
        {
            switch (phase)
            {
                case HardwareProfilePhase.PreSimulation:
                    return 0.4f;
                case HardwareProfilePhase.Simulation:
                    return 3.4f;
                case HardwareProfilePhase.PostSimulation:
                    return 0.7f;
                case HardwareProfilePhase.VisualSync:
                    return 2.2f;
                default:
                    return 0f;
            }
        }
    }
}
