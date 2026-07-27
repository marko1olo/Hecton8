// ============================================================================
// HECTON-8 - SaveOwnerCensus.cs
//
// Pure, Unity-free bitmask math for the required-owner census that SaveManager
// runs immediately before it populates a save payload and immediately before it
// applies a loaded payload to the ISaveable registry.
//
// WHY THIS EXISTS
//   The persistence apply loop iterates the ISaveable registry. When a required
//   owner is not registered at that instant the loop simply does not visit it:
//   no exception, no log line, no counter. The load then reports success while
//   the owner's whole section was applied to nothing. This file turns "the
//   registry did not contain the owner" from an invisible non-event into a
//   value that telemetry, the persistence black box, and an editor test can all
//   read.
//
//   Categories mirror the save/load acceptance row of
//   Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:88 -
//   "Save, quit/reload, and return preserve position, inventory, route state,
//   opened/looted/scanned flags, and relevant hazard state."
//
// CONTRACT
//   - No UnityEngine, no Unity.Collections, no allocation, no I/O.
//   - Every function is deterministic and total: any uint input is legal.
//   - Bits outside RequiredCategoryMask are ignored, never faulted on, so a
//     future sixth category cannot silently corrupt an existing verdict.
// ============================================================================

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Required-owner census math for the persistence registry. Pure C#: this type is
    /// referenced by <see cref="SaveManager"/> but depends on no Unity API, so its verdicts
    /// are executable and assertable outside the editor.
    /// </summary>
    public static class SaveOwnerCensus
    {
        /// <summary>Player transform/rotation/velocity truth. Authoritative owner: HectonSurvivalSystem.</summary>
        public const uint CategoryPlayerPosition = 1u << 0;

        /// <summary>Carried item truth. Authoritative owner: PlayerInventory.</summary>
        public const uint CategoryInventory = 1u << 1;

        /// <summary>Opening-route progression truth. Authoritative owner: FirstHourDirector.</summary>
        public const uint CategoryRouteState = 1u << 2;

        /// <summary>Opened/looted/scanned world flags. Owners: WorldStateManager, HectonDiscoveryManager, ScanLogSystem.</summary>
        public const uint CategoryWorldObjectFlags = 1u << 3;

        /// <summary>Hazard exposure truth. Owners: HazardZoneManager, RadiationHazardGrid.</summary>
        public const uint CategoryHazardState = 1u << 4;

        /// <summary>Every category the First-20 save/load acceptance row requires.</summary>
        public const uint RequiredCategoryMask =
            CategoryPlayerPosition |
            CategoryInventory |
            CategoryRouteState |
            CategoryWorldObjectFlags |
            CategoryHazardState;

        /// <summary>Number of set bits in <see cref="RequiredCategoryMask"/>.</summary>
        public const int RequiredCategoryCount = 5;

        private const uint CensusHashPrime = 16777619u;

        /// <summary>
        /// Categories the contract requires that the registry did not supply.
        /// Zero means every required category has at least one live registered owner.
        /// </summary>
        public static uint ResolveMissingCategories(uint presentCategories)
        {
            return RequiredCategoryMask & ~presentCategories;
        }

        /// <summary>
        /// True only when the registry is non-empty AND every required category is covered.
        /// An empty registry is a census failure even though it produces no missing-owner
        /// diagnostics of its own - that is the exact silent case this census exists to catch.
        /// </summary>
        public static bool IsCensusSatisfied(uint presentCategories, int liveOwnerCount)
        {
            return liveOwnerCount > 0 && ResolveMissingCategories(presentCategories) == 0u;
        }

        /// <summary>Population count restricted to the required window. Allocation-free.</summary>
        public static int CountCategories(uint categoryMask)
        {
            uint bits = categoryMask & RequiredCategoryMask;
            int count = 0;
            while (bits != 0u)
            {
                bits &= bits - 1u;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Fraction of the required contract categories the registry currently covers,
        /// in the range 0..1. Reported as the telemetry scalar so a player-build capture
        /// carries the severity, not only the fact.
        /// </summary>
        public static float ResolveCensusCoverage01(uint presentCategories)
        {
            return CountCategories(presentCategories) / (float)RequiredCategoryCount;
        }

        /// <summary>
        /// Stable FNV-style context hash for one census verdict. Deterministic across runs so
        /// two captures of the same failure collapse to one telemetry context instead of noise.
        /// </summary>
        public static uint ComputeCensusContextHash(
            uint seedHash,
            uint slotHash,
            uint missingCategories,
            int liveOwnerCount)
        {
            unchecked
            {
                uint clampedOwners = liveOwnerCount > 0 ? (uint)liveOwnerCount : 0u;
                uint hash = seedHash ^ slotHash;
                hash = (hash * CensusHashPrime) ^ (missingCategories & RequiredCategoryMask);
                hash = (hash * CensusHashPrime) ^ clampedOwners;
                return hash == 0u ? seedHash : hash;
            }
        }

        /// <summary>
        /// Which of the still-outstanding categories a newly registered owner can close.
        /// Zero means the owner is irrelevant to the pending deferred hydration and must not
        /// be re-applied - re-applying an owner that already consumed the payload would
        /// overwrite gameplay that has run since the load.
        /// </summary>
        public static uint ResolveSatisfiedCategories(uint pendingMissingCategories, uint ownerCategories)
        {
            return pendingMissingCategories & ownerCategories & RequiredCategoryMask;
        }

        /// <summary>Outstanding categories after an owner has consumed the payload.</summary>
        public static uint ClearSatisfiedCategories(uint pendingMissingCategories, uint ownerCategories)
        {
            return (pendingMissingCategories & RequiredCategoryMask) & ~ownerCategories;
        }

        /// <summary>
        /// Deferred hydration window check. Uses an absolute unscaled-time deadline supplied by
        /// the caller rather than a tick count, so the window is not a function of frame rate or
        /// of the dispatcher's variable slow-tick interval. A non-finite clock reads as expired
        /// so a broken time source releases the retained payload instead of pinning it forever.
        /// </summary>
        public static bool IsDeferredHydrationExpired(double nowSeconds, double deadlineSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsNaN(deadlineSeconds))
                return true;

            return nowSeconds >= deadlineSeconds;
        }

        /// <summary>
        /// Stable ASCII label for one category bit. Returns a compile-time constant, so
        /// diagnostic composition allocates only the joining buffer, never the names.
        /// Not player-facing text: this is developer/telemetry vocabulary only.
        /// </summary>
        public static string DescribeCategory(uint singleCategoryBit)
        {
            switch (singleCategoryBit)
            {
                case CategoryPlayerPosition:
                    return "position";
                case CategoryInventory:
                    return "inventory";
                case CategoryRouteState:
                    return "route-state";
                case CategoryWorldObjectFlags:
                    return "opened-looted-scanned-flags";
                case CategoryHazardState:
                    return "hazard-state";
                default:
                    return "unknown-category";
            }
        }

        /// <summary>
        /// Category bit at <paramref name="index"/> in the fixed contract order
        /// (0 position, 1 inventory, 2 route state, 3 world flags, 4 hazard state).
        /// Returns zero when the index is outside the contract, so callers can loop
        /// <see cref="RequiredCategoryCount"/> times without a bounds branch of their own.
        /// </summary>
        public static uint ResolveCategoryAtIndex(int index)
        {
            if (index < 0 || index >= RequiredCategoryCount)
                return 0u;

            return 1u << index;
        }
    }
}
