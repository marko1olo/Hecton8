// ============================================================================
// HECTON-8 — ModuleSocketLane.cs
//
// FIRST_20_MINUTES moment served: "Craft/repair/build". A socket compatibility
// lane is the only thing that decides whether the module the player just built
// will accept the next one, so a lane vocabulary that three subsystems disagree
// about is a build-route blocker, not a hygiene item.
//
// WHY THIS FILE IS RUNTIME AND NOT EDITOR-ONLY:
//   The lane string is consumed by THREE runtime comparators that today disagree
//   with each other. A vocabulary that lives in an Editor assembly can never be
//   adopted by any of them, so the disagreement would be documented instead of
//   closed. This type is deliberately placed beside ModuleSocket.cs, in the same
//   Hecton8.Building namespace and the same Hecton8.Core assembly
//   (Assets/_Project/Scripts/Hecton8.Core.asmdef), so all three owners can adopt
//   it without an assembly reference change:
//
//     1. ModuleSocketTopology.AreCompatible (ModuleSocket.cs:62-75) — the SNAPPER.
//        Empty on either side => universal; otherwise
//        string.Equals(..., StringComparison.OrdinalIgnoreCase).
//        So "Habitat", "habitat" and "HABITAT" are ONE lane here.
//
//     2. BaseModuleCatalogRuntime.ComputeCompatibilityMask
//        (BaseModuleCatalogRuntime.cs:874-882) — the GRAPH MASK.
//        Empty => UniversalConnectionMask; otherwise
//        1u << (8 + (uint)LocHash.Compute(lane) % 23).
//        LocHash.Compute (LocRegistry.cs:42-65) is case SENSITIVE, so the three
//        spellings above are THREE lanes here, and the % 23 fold makes unrelated
//        spellings share a bit. Verified by reimplementing LocHash.Compute
//        (INFERRED, not measured in-engine):
//          "Dock"                     -> u32 249426022   -> % 23 = 15
//          "h8.structure.hardsurface" -> u32 1298547746  -> % 23 = 15
//          "Structure"                -> u32 221943736   -> % 23 = 15
//          "Moonpool"                 -> u32 2386007434  -> % 23 = 15
//          "exterior"                 -> u32 2119747359  -> % 23 = 15
//          "Habitat" 10, "habitat" 13, "HABITAT" 9, "Exterior" 3, "dock" 3
//        The Moonpool Bottom socket is authored "Dock"
//        (BaseModuleTemplate_Moonpool.asset:36) and every generated socket was
//        authored "h8.structure.hardsurface", so the graph mask already reports
//        those two as connectable while the snapper rejects them. Two subsystems,
//        two answers, same data.
//
//     3. ShinobuSocketConstructionRuntime.HashCompatibility
//        (ShinobuSocketConstructionData.cs:1272-1289) — the PLACEMENT DTO HASH.
//        Case INSENSITIVE ASCII FNV folded to 24 bits, 0 reserved as universal.
//        Agrees with the snapper on case, disagrees with the graph mask.
//
// WHAT THIS TYPE FIXES, EXACTLY:
//   • Closes the vocabulary. Three lanes, counted off the live authored set: of
//     the 19 sockets across the seven StandardModuleTemplates assets, 17 are
//     "Habitat", one is "Exterior" (BaseModuleTemplate_Airlock.asset:30) and one
//     is "Dock" (BaseModuleTemplate_Moonpool.asset:36). Nothing else was ever
//     authored. A fourth string is a typo until this file says otherwise.
//   • Replaces the hash fold with an EXACT lane index. The mask format has 23
//     bit slots (BaseModuleCatalogRuntime.cs:184, :197-198) and the vocabulary
//     has three entries, so one bit per lane is collision-free by construction
//     rather than by luck. UniversalConnectionMask keeps working unchanged: it
//     sets all 23 slots, so it still ANDs true against any single lane bit.
//   • Gives one canonical lane comparator, byte-for-byte behaviourally identical
//     to the snapper's lane half, that an Editor assembly can also call —
//     ModuleSocketTopology is `internal` and therefore invisible outside
//     Hecton8.Core, which is why the authoring gate could not previously check
//     the snapper's real answer.
//
// ADOPTION REQUIRED FROM THE THREE OWNERS (named, not done here — those files
// are not in this change's edit scope):
//   • BaseModuleCatalogRuntime.ComputeCompatibilityMask should return
//     TryResolveLaneMask's value and reject an unknown lane instead of folding
//     it. That changes the baked mask bits in
//     Assets/_Project/Data/Construction/BaseModuleCatalog.h8bin, so it needs a
//     re-bake through Hecton8/Construction/Base Module Catalog. It does NOT need
//     a save migration: AllowedConnectionsMask appears nowhere in SaveData.cs,
//     and no geometric or lane field reaches any persisted hash
//     (BaseModuleTemplate.cs:242 folds stableId alone).
//   • ModuleSocketTopology.AreCompatible should delegate its lane half to
//     AreLanesCompatible below.
//   • ShinobuSocketConstructionRuntime.HashCompatibility should hash the
//     resolved lane index rather than the raw string.
//
// HOT-PATH NOTE: every method here is allocation-free — no LINQ, no ToString, no
// string concat, no lambda, no boxing. TryResolveLaneIndex is an ordinal
// ignore-case scan over a fixed three-entry table. The call sites are cold by
// contract anyway (catalog bake, placement commit, and event-driven graph
// rebuild per construction.md section 8B), but the guarantee holds regardless.
// This type is managed-string based and is therefore NOT Burst-callable; the
// Burst path consumes the resolved uint mask, never the string.
// ============================================================================

using System;

namespace Hecton8.Building
{
    /// <summary>
    /// Closed, validated vocabulary of module socket compatibility lanes.
    /// A lane string outside this list is an authoring error, not a new lane.
    /// </summary>
    public static class ModuleSocketLane
    {
        // ══════════════════════════════════════════════════════════
        //  MASK ABI — mirrored, with the owner named
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// First mask bit reserved for compatibility lanes. Mirrors the private
        /// <c>BaseModuleCatalogRuntime.CompatibilityLaneBitOffset</c>
        /// (BaseModuleCatalogRuntime.cs:197). Mirrored rather than referenced
        /// because that constant is private; if it moves, this file is wrong and
        /// the authoring gate that cross-checks both values reports it.
        /// </summary>
        public const int MaskBitOffset = 8;

        /// <summary>
        /// Number of mask bit slots reserved for lanes. Mirrors the private
        /// <c>BaseModuleCatalogRuntime.CompatibilityLaneCount</c>
        /// (BaseModuleCatalogRuntime.cs:198). This is the SLOT budget, not the
        /// lane count — the vocabulary uses three of the twenty-three.
        /// </summary>
        public const int MaskSlotCount = 23;

        /// <summary>
        /// Mask meaning "connects to every lane", identical to
        /// <c>BaseModuleCatalogRuntime.UniversalConnectionMask</c>
        /// (BaseModuleCatalogRuntime.cs:184). All 23 lane slots set, so it ANDs
        /// true against any single lane bit under
        /// <c>AreSocketMasksCompatible</c> (BaseModuleCatalogRuntime.cs:862-865).
        /// </summary>
        public const uint UniversalMask = 0x7FFFFF00u;

        // ══════════════════════════════════════════════════════════
        //  THE VOCABULARY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pressurized habitat interior. 17 of the 19 authored sockets use this
        /// lane, across BaseModuleTemplate_Airlock, _CorridorStraight,
        /// _Foundation, _JunctionT, _JunctionX, _Moonpool and _MultiPurposeRoom.
        /// </summary>
        public const string Habitat = "Habitat";

        /// <summary>
        /// Open water face. One authored socket: the Airlock South hatch
        /// (BaseModuleTemplate_Airlock.asset:30). It is the outward face of the
        /// only emergency airlock in the kit.
        /// </summary>
        public const string Exterior = "Exterior";

        /// <summary>
        /// Vehicle docking well. One authored socket: the Moonpool Bottom well
        /// (BaseModuleTemplate_Moonpool.asset:36).
        /// </summary>
        public const string Dock = "Dock";

        /// <summary>
        /// Canonical spelling table. Lane index is the position in this array and
        /// is the value folded into the mask bit, so entries are APPENDED and
        /// never reordered: reordering silently re-numbers every baked
        /// <c>SocketDefinitionDTO.AllowedConnectionsMask</c> in
        /// <c>BaseModuleCatalog.h8bin</c> and requires a re-bake. Reordering is
        /// still save-safe, because the mask is not persisted in SaveData.cs and
        /// no lane field reaches a module hash (BaseModuleTemplate.cs:242).
        /// </summary>
        // COLD ALLOC: string[3] - closed socket lane vocabulary, one static table for the process - owner: ModuleSocketLane
        private static readonly string[] s_LaneNames = { Habitat, Exterior, Dock };

        /// <summary>Number of authored lanes. Three.</summary>
        public static int LaneCount
        {
            get { return s_LaneNames.Length; }
        }

        /// <summary>
        /// True while the vocabulary still fits the mask ABI. Adding a 24th lane
        /// makes this false and the shift in <see cref="TryResolveLaneMask"/>
        /// would run past bit 30 into the reserved high bit, so the authoring
        /// gate fails on it rather than emitting a corrupt mask.
        /// </summary>
        public static bool FitsMaskBudget
        {
            get { return s_LaneNames.Length <= MaskSlotCount; }
        }

        /// <summary>
        /// Canonical spelling of a lane by index, or <see cref="string.Empty"/>
        /// for an index outside the vocabulary.
        /// </summary>
        /// <param name="laneIndex">Zero-based lane index.</param>
        public static string GetLaneName(int laneIndex)
        {
            return (uint)laneIndex < (uint)s_LaneNames.Length
                ? s_LaneNames[laneIndex]
                : string.Empty;
        }

        // ══════════════════════════════════════════════════════════
        //  RESOLUTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// True when this lane string means "connects to every lane".
        /// Uses <c>string.IsNullOrEmpty</c>, NOT
        /// <c>string.IsNullOrWhiteSpace</c>, because that is the exact test all
        /// three current owners apply (ModuleSocket.cs:71,
        /// BaseModuleCatalogRuntime.cs:876,
        /// ShinobuSocketConstructionData.cs:1274). Matching them is the point of
        /// this file; a whitespace-only lane is a real authoring defect that the
        /// authoring gate rejects rather than something to silently absorb here.
        /// </summary>
        /// <param name="lane">Authored lane string.</param>
        public static bool IsUniversal(string lane)
        {
            return string.IsNullOrEmpty(lane);
        }

        /// <summary>
        /// Resolves an authored lane string to its vocabulary index.
        /// Ordinal ignore-case, so "Habitat", "habitat" and "HABITAT" all resolve
        /// to the same lane — which is what the snapper already does and what the
        /// graph mask currently does not.
        /// </summary>
        /// <param name="lane">Authored lane string.</param>
        /// <param name="laneIndex">Resolved vocabulary index on success.</param>
        /// <returns>False for universal, unknown, or whitespace-only lanes.</returns>
        public static bool TryResolveLaneIndex(string lane, out int laneIndex)
        {
            laneIndex = -1;
            if (string.IsNullOrEmpty(lane))
                return false;

            for (int i = 0; i < s_LaneNames.Length; i++)
            {
                if (string.Equals(s_LaneNames[i], lane, StringComparison.OrdinalIgnoreCase))
                {
                    laneIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the lane string is spelled exactly as the vocabulary spells
        /// it. A case variant resolves through
        /// <see cref="TryResolveLaneIndex"/> but is still an authoring defect,
        /// because <c>BaseModuleCatalogRuntime.ComputeCompatibilityMask</c>
        /// hashes it case-sensitively and puts it on a different graph lane than
        /// the canonical spelling.
        /// </summary>
        /// <param name="lane">Authored lane string.</param>
        public static bool IsCanonicalSpelling(string lane)
        {
            if (string.IsNullOrEmpty(lane))
                return false;

            for (int i = 0; i < s_LaneNames.Length; i++)
            {
                if (string.Equals(s_LaneNames[i], lane, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the collision-free connection mask for a lane string.
        /// One dedicated bit per vocabulary lane, so two different lanes can
        /// never share a bit — unlike the current
        /// <c>1u &lt;&lt; (8 + hash % 23)</c> fold, which already puts "Dock" and
        /// "h8.structure.hardsurface" on the same bit.
        /// </summary>
        /// <param name="lane">Authored lane string.</param>
        /// <param name="mask">Resolved single-bit lane mask, or
        /// <see cref="UniversalMask"/> for a universal socket.</param>
        /// <returns>False for an unknown lane; <paramref name="mask"/> is 0, which
        /// ANDs false against everything, so an unresolved lane fails closed.</returns>
        public static bool TryResolveLaneMask(string lane, out uint mask)
        {
            if (IsUniversal(lane))
            {
                mask = UniversalMask;
                return true;
            }

            if (!TryResolveLaneIndex(lane, out int laneIndex) || laneIndex >= MaskSlotCount)
            {
                mask = 0u;
                return false;
            }

            mask = 1u << (MaskBitOffset + laneIndex);
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  CANONICAL COMPARISON
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// The lane half of socket compatibility, behaviourally identical to
        /// <c>ModuleSocketTopology.AreCompatible</c> (ModuleSocket.cs:71-74):
        /// empty on either side connects to anything, otherwise ordinal
        /// ignore-case equality. Exposed publicly because
        /// <c>ModuleSocketTopology</c> is <c>internal</c> to Hecton8.Core, so no
        /// authoring gate could previously ask the snapper what it actually
        /// thinks. Direction inversion is NOT part of this call — that stays with
        /// <c>ModuleSocketTopology.AreInverseDirections</c> (ModuleSocket.cs:48-60).
        /// </summary>
        /// <param name="lhsLane">First authored lane string.</param>
        /// <param name="rhsLane">Second authored lane string.</param>
        public static bool AreLanesCompatible(string lhsLane, string rhsLane)
        {
            if (string.IsNullOrEmpty(lhsLane) || string.IsNullOrEmpty(rhsLane))
                return true;

            return string.Equals(lhsLane, rhsLane, StringComparison.OrdinalIgnoreCase);
        }
    }
}
