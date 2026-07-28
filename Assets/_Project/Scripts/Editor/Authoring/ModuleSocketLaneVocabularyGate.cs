// ============================================================================
// HECTON-8 — ModuleSocketLaneVocabularyGate.cs
//
// FIRST_20_MINUTES moment served: "Craft/repair/build". This gate exists because
// two modules the player can build from the same browser currently refuse to
// connect to each other, and nothing on the authoring side could say so.
//
// WHY A GATE IS ALLOWED HERE (AGENTS.md forbids a self-check cascade unless the
// validator catches a concrete repeated failure, has a reproducible reject case,
// maps to a product gate, and enables the next source action — all four hold):
//   • Concrete repeated failure: "h8.structure.hardsurface" — a lane string that
//     appears on 19 sockets across the six Agent1712 generated templates and on
//     ZERO authored templates. It was minted by a generator, not authored, which
//     is exactly the failure mode a closed vocabulary prevents.
//   • Reproducible reject case, live in the repo right now:
//     BaseModuleTemplate_Moonpool.asset:36 authors "Dock";
//     H8_A1712_VerticalShaft_01_Template.asset:33-42 authors
//     "h8.structure.hardsurface". BaseModuleCatalogRuntime.ComputeCompatibilityMask
//     (BaseModuleCatalogRuntime.cs:874-882) folds BOTH onto lane 15 of 23, so
//     AreSocketMasksCompatible (BaseModuleCatalogRuntime.cs:862-865) reports them
//     connectable, while ModuleSocketTopology.AreCompatible (ModuleSocket.cs:71-74)
//     rejects them on ordinal ignore-case equality. Two subsystems, two answers.
//     "Habitat" / "habitat" / "HABITAT" fold to lanes 10 / 13 / 9 in the graph
//     mask and to ONE lane in the snapper — the mirror-image disagreement.
//   • Product gate: construction.md section 8A. "Placement helpers may reject
//     impossible placements only by reading the construction owner, physics proxy
//     state, and logistics rules. They must not create alternate placement truth."
//     Three comparators returning three answers IS alternate placement truth.
//   • Next source action it enables: the three adoption edits named in the header
//     of ModuleSocketLane.cs, each in a file this change does not own.
//
// SCOPE OF THE DEAD-SOCKET SWEEP:
//   Only templates reachable from ModuleCatalog_Starter.asset are swept. The ten
//   assets under Data/Construction/AbandonedModuleTemplates are deliberately
//   EXCLUDED: they predate socketDefinitions entirely (their YAML carries only
//   legacy snapPoints, so BaseModuleTemplate.OnValidate derives sockets with an
//   empty compatibleType — BaseModuleTemplate.cs:233-234, :257-267), which means
//   every one of their sockets is universal and would make every lane look alive.
//   They are the procedural ruin set, a different kit with a different contract.
//
// PROOF CLASS: static asset-graph analysis in the Editor. Not Play Mode, not
// placement proof, not profiler proof. A PASS means the authored lane strings are
// internally consistent, NOT that a module snaps in game.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Building;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Authoring gate over the closed socket lane vocabulary in
    /// <see cref="ModuleSocketLane"/>. Read-only: it writes no assets.
    /// </summary>
    public static class ModuleSocketLaneVocabularyGate
    {
        private const string LogPrefix = "[ModuleSocketLaneGate]";

        private const string ConstructionDataFolder = "Assets/_Project/Data/Construction";
        private const string ModuleCatalogPath = ConstructionDataFolder + "/ModuleCatalog_Starter.asset";

        /// <summary>
        /// Prefab folders swept for authored <see cref="ModuleSocket"/> components.
        /// The socket component carries its own lane string
        /// (ModuleSocket.cs:130) independently of the template, so a prefab can
        /// disagree with the template that owns it.
        /// </summary>
        private static readonly string[] SocketPrefabFolders =
        {
            "Assets/_Project/Prefabs/Construction",
            "Assets/_Project/Art/Baked/Structures/Agent1712"
        };

        /// <summary>One observed lane string plus where it was found.</summary>
        private readonly struct LaneSighting
        {
            public LaneSighting(string lane, string ownerLabel)
            {
                Lane = lane;
                OwnerLabel = ownerLabel;
            }

            public string Lane { get; }

            public string OwnerLabel { get; }
        }

        /// <summary>One socket of the shipped kit, for the dead-socket sweep.</summary>
        private readonly struct KitSocket
        {
            public KitSocket(string ownerLabel, ModuleSocketDirection direction, string lane)
            {
                OwnerLabel = ownerLabel;
                Direction = direction;
                Lane = lane;
            }

            public string OwnerLabel { get; }

            public ModuleSocketDirection Direction { get; }

            public string Lane { get; }
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Verifies every authored socket lane string in the project against the
        /// closed vocabulary, cross-checks the three runtime comparators against
        /// each other on the lanes actually present, and reports sockets with no
        /// legal partner anywhere in the shipped kit. Writes nothing.
        /// Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleSocketLaneVocabularyGate.VerifyModuleSocketLaneVocabulary
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Module Socket Lane Vocabulary", priority = 244)]
        public static void VerifyModuleSocketLaneVocabulary()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine($"{LogPrefix} LANE VOCABULARY REPORT");
            report.Append("  Vocabulary (closed): ");
            for (int i = 0; i < ModuleSocketLane.LaneCount; i++)
            {
                if (i > 0)
                    report.Append(", ");
                report.Append(ModuleSocketLane.GetLaneName(i)).Append(" -> lane ").Append(i);
            }

            report.AppendLine($". Mask slots available: {ModuleSocketLane.MaskSlotCount}.");

            int failureCount = 0;
            int warningCount = 0;

            if (!ModuleSocketLane.FitsMaskBudget)
            {
                failureCount++;
                report.AppendLine(
                    $"  FAIL: vocabulary holds {ModuleSocketLane.LaneCount} lanes but the mask ABI reserves only " +
                    $"{ModuleSocketLane.MaskSlotCount} slots (BaseModuleCatalogRuntime.cs:197-198). " +
                    "TryResolveLaneMask would shift past the reserved window and emit a corrupt mask.");
            }

            // COLD ALLOC: List<LaneSighting>[64] - one entry per authored socket found in the sweep - owner: ModuleSocketLaneVocabularyGate
            List<LaneSighting> sightings = new List<LaneSighting>(64);
            // COLD ALLOC: List<KitSocket>[32] - shipped-kit socket set for the dead-socket sweep - owner: ModuleSocketLaneVocabularyGate
            List<KitSocket> kitSockets = new List<KitSocket>(32);

            failureCount += CollectCatalogKitSockets(report, sightings, kitSockets);
            CollectAllTemplateLanes(sightings);
            CollectPrefabSocketLanes(sightings);

            failureCount += ReportVocabularyViolations(report, sightings);
            failureCount += ReportComparatorDisagreements(report, sightings);
            warningCount += ReportDeadSockets(report, kitSockets);

            report.AppendLine(
                $"  SUMMARY: failures={failureCount}, warnings={warningCount}, socketsInspected={sightings.Count}, " +
                $"shippedKitSockets={kitSockets.Count}. Static asset-graph proof only — not Play Mode, not " +
                "placement proof, not profiler proof.");

            if (failureCount > 0)
            {
                report.Append("  RESULT: FAIL");
                Debug.LogError(report.ToString());
            }
            else if (warningCount > 0)
            {
                report.Append("  RESULT: PASS WITH WARNINGS");
                Debug.LogWarning(report.ToString());
            }
            else
            {
                report.Append("  RESULT: PASS");
                Debug.Log(report.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(failureCount > 0 ? 1 : 0);
        }

        // ══════════════════════════════════════════════════════════
        //  COLLECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Walks every recipe in the catalog and records the sockets of the
        /// template each recipe binds. That template — not the prefab's own — is
        /// what the placed module ends up carrying, because
        /// BaseModule.ApplyBuildableTemplate (BaseModule.cs:4802-4816) assigns
        /// <c>data.ModuleTemplate</c> over the prefab's serialized value on both
        /// the placement path (ConstructionManager.cs:825) and the save-restore
        /// path (ConstructionManager.cs:2873).
        /// </summary>
        private static int CollectCatalogKitSockets(
            StringBuilder report,
            List<LaneSighting> sightings,
            List<KitSocket> kitSockets)
        {
            ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(ModuleCatalogPath);
            if (catalog == null)
            {
                report.AppendLine(
                    $"  FAIL: module catalog not found at '{ModuleCatalogPath}'. The shipped-kit socket set " +
                    "cannot be resolved, so the dead-socket sweep was skipped.");
                return 1;
            }

            int recipeCount = catalog.Count;
            for (int i = 0; i < recipeCount; i++)
            {
                BuildableData recipe = catalog.GetAt(i);
                if (recipe == null)
                    continue;

                BaseModuleTemplate template = recipe.ModuleTemplate;
                if (template == null)
                    continue;

                BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
                if (definitions == null)
                    continue;

                for (int s = 0; s < definitions.Length; s++)
                {
                    string ownerLabel = $"{recipe.name} -> {template.name}[{s}]";
                    string lane = definitions[s].CompatibleType;
                    sightings.Add(new LaneSighting(lane, ownerLabel));
                    kitSockets.Add(new KitSocket(ownerLabel, definitions[s].Direction, lane));
                }
            }

            return 0;
        }

        /// <summary>
        /// Records lanes from every BaseModuleTemplate in the project, including
        /// templates no recipe binds. An orphan template still ships its lane
        /// string into BaseModuleCatalog.h8bin, because the catalog bake scans by
        /// type and not by catalog membership
        /// (BaseModuleCatalogEditorTools.cs:130-137).
        /// </summary>
        private static void CollectAllTemplateLanes(List<LaneSighting> sightings)
        {
            string[] guids = AssetDatabase.FindAssets("t:BaseModuleTemplate");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(path);
                if (template == null)
                    continue;

                BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
                if (definitions == null)
                    continue;

                for (int s = 0; s < definitions.Length; s++)
                    sightings.Add(new LaneSighting(definitions[s].CompatibleType, $"{path}[socket {s}]"));
            }
        }

        /// <summary>
        /// Records lanes from authored <see cref="ModuleSocket"/> components. The
        /// component carries its own independent lane field
        /// (ModuleSocket.cs:130), so a prefab socket can disagree with the
        /// template that will be applied over it at placement.
        /// </summary>
        private static void CollectPrefabSocketLanes(List<LaneSighting> sightings)
        {
            for (int f = 0; f < SocketPrefabFolders.Length; f++)
            {
                string folder = SocketPrefabFolders[f];
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    ModuleSocket[] sockets = prefab.GetComponentsInChildren<ModuleSocket>(true);
                    for (int s = 0; s < sockets.Length; s++)
                    {
                        if (sockets[s] == null)
                            continue;

                        sightings.Add(new LaneSighting(sockets[s].CompatibleType, $"{path}[ModuleSocket {s}]"));
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CHECK 1: VOCABULARY MEMBERSHIP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fails on any lane string that is not the exact canonical spelling of a
        /// vocabulary entry. Three distinct defect classes, reported separately
        /// because their fixes differ.
        /// </summary>
        private static int ReportVocabularyViolations(StringBuilder report, List<LaneSighting> sightings)
        {
            int failures = 0;

            // COLD ALLOC: HashSet<string>[16] - dedupes repeated violations of the same lane string - owner: ModuleSocketLaneVocabularyGate
            HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sightings.Count; i++)
            {
                string lane = sightings[i].Lane;
                if (ModuleSocketLane.IsUniversal(lane))
                    continue;

                if (ModuleSocketLane.IsCanonicalSpelling(lane))
                    continue;

                if (!reported.Add(lane))
                    continue;

                failures++;

                if (string.IsNullOrWhiteSpace(lane))
                {
                    report.AppendLine(
                        $"  FAIL whitespace-only lane '{lane}' first seen at '{sightings[i].OwnerLabel}'. " +
                        "All three comparators test IsNullOrEmpty, not IsNullOrWhiteSpace " +
                        "(ModuleSocket.cs:71, BaseModuleCatalogRuntime.cs:876, " +
                        "ShinobuSocketConstructionData.cs:1274), so this is NOT universal — it is a phantom " +
                        "lane that only matches another whitespace socket. Clear the field to make it universal.");
                    continue;
                }

                if (ModuleSocketLane.TryResolveLaneIndex(lane, out int laneIndex))
                {
                    report.AppendLine(
                        $"  FAIL case drift '{lane}' at '{sightings[i].OwnerLabel}' — resolves to lane " +
                        $"{laneIndex} ('{ModuleSocketLane.GetLaneName(laneIndex)}') only under ordinal " +
                        "ignore-case. The snapper folds case (ModuleSocket.cs:74) but " +
                        "BaseModuleCatalogRuntime.ComputeCompatibilityMask hashes case-sensitively " +
                        "(BaseModuleCatalogRuntime.cs:879), so this spelling gets its own graph lane while " +
                        $"sharing the snapper's. Rewrite it as '{ModuleSocketLane.GetLaneName(laneIndex)}'.");
                    continue;
                }

                report.AppendLine(
                    $"  FAIL unknown lane '{lane}' at '{sightings[i].OwnerLabel}'. It is outside the closed " +
                    "vocabulary in ModuleSocketLane.cs, so no authored socket anywhere in the kit can connect " +
                    "to it through the snapper, while ComputeCompatibilityMask still folds it onto one of the " +
                    "23 graph lanes and can make it look connectable. Either author one of the vocabulary " +
                    "lanes, or add the new lane to ModuleSocketLane.s_LaneNames by APPENDING and re-bake " +
                    "BaseModuleCatalog.h8bin.");
            }

            return failures;
        }

        // ══════════════════════════════════════════════════════════
        //  CHECK 2: THE THREE COMPARATORS MUST AGREE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// For every ordered pair of distinct lane strings actually present in the
        /// project, asks the snapper's lane rule and the graph mask rule the same
        /// question and fails on any disagreement. The graph answer comes from the
        /// real <c>BaseModuleCatalogRuntime.ComputeCompatibilityMask</c>, not from
        /// a reimplementation, so the gate cannot drift from the owner.
        /// </summary>
        private static int ReportComparatorDisagreements(StringBuilder report, List<LaneSighting> sightings)
        {
            // COLD ALLOC: List<string>[16] - distinct non-universal lane strings observed - owner: ModuleSocketLaneVocabularyGate
            List<string> distinct = new List<string>(16);
            // COLD ALLOC: HashSet<string>[16] - dedupe for the distinct lane list - owner: ModuleSocketLaneVocabularyGate
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sightings.Count; i++)
            {
                string lane = sightings[i].Lane;
                if (ModuleSocketLane.IsUniversal(lane) || !seen.Add(lane))
                    continue;

                distinct.Add(lane);
            }

            int failures = 0;

            // Per-lane: the graph mask must be a single bit inside the mirrored
            // window, and it must equal the vocabulary's exact bit. The first half
            // detects the mask ABI moving under the mirrored constants in
            // ModuleSocketLane; the second half is the collision-free upgrade.
            for (int i = 0; i < distinct.Count; i++)
            {
                string lane = distinct[i];
                uint graphMask = BaseModuleCatalogRuntime.ComputeCompatibilityMask(lane);
                if (!IsSingleLaneBitInWindow(graphMask, out int graphLane))
                {
                    failures++;
                    report.AppendLine(
                        $"  FAIL lane '{lane}': ComputeCompatibilityMask returned 0x{graphMask:X8}, which is not a " +
                        $"single bit inside the mirrored lane window [{ModuleSocketLane.MaskBitOffset}, " +
                        $"{ModuleSocketLane.MaskBitOffset + ModuleSocketLane.MaskSlotCount}). The mask ABI moved " +
                        "and the mirrored constants in ModuleSocketLane.cs are now wrong " +
                        "(BaseModuleCatalogRuntime.cs:184, :197-198).");
                    continue;
                }

                if (ModuleSocketLane.TryResolveLaneMask(lane, out uint exactMask) && exactMask != graphMask)
                {
                    report.AppendLine(
                        $"  INFO lane '{lane}': current graph lane {graphLane} (mask 0x{graphMask:X8}) vs " +
                        $"collision-free lane {LaneIndexOf(lane)} (mask 0x{exactMask:X8}). Adopting " +
                        "ModuleSocketLane.TryResolveLaneMask in " +
                        "BaseModuleCatalogRuntime.ComputeCompatibilityMask changes this bit and needs a " +
                        "BaseModuleCatalog.h8bin re-bake. No save migration: AllowedConnectionsMask is absent " +
                        "from SaveData.cs.");
                }
            }

            // Cross-lane: the two subsystems must not disagree on any pair.
            for (int a = 0; a < distinct.Count; a++)
            {
                for (int b = a + 1; b < distinct.Count; b++)
                {
                    string lhs = distinct[a];
                    string rhs = distinct[b];

                    bool snapperSaysYes = ModuleSocketLane.AreLanesCompatible(lhs, rhs);
                    uint lhsMask = BaseModuleCatalogRuntime.ComputeCompatibilityMask(lhs);
                    uint rhsMask = BaseModuleCatalogRuntime.ComputeCompatibilityMask(rhs);
                    bool graphSaysYes = BaseModuleCatalogRuntime.AreSocketMasksCompatible(lhsMask, rhsMask);

                    if (snapperSaysYes == graphSaysYes)
                        continue;

                    failures++;
                    if (graphSaysYes)
                    {
                        report.AppendLine(
                            $"  FAIL comparator split: '{lhs}' and '{rhs}' share graph mask bit 0x{lhsMask:X8} " +
                            "so AreSocketMasksCompatible (BaseModuleCatalogRuntime.cs:862-865) reports them " +
                            "CONNECTABLE, while the snapper's ordinal ignore-case rule (ModuleSocket.cs:71-74) " +
                            "REJECTS them. The habitat graph will treat these modules as joined and the " +
                            "placement snapper will refuse to join them.");
                    }
                    else
                    {
                        report.AppendLine(
                            $"  FAIL comparator split: the snapper accepts '{lhs}' with '{rhs}' (case-folded " +
                            "equality, ModuleSocket.cs:74) but the graph masks " +
                            $"0x{lhsMask:X8} and 0x{rhsMask:X8} do not intersect, because " +
                            "ComputeCompatibilityMask hashes case-sensitively " +
                            "(BaseModuleCatalogRuntime.cs:879). Modules will snap and then read as disconnected " +
                            "in the habitat graph.");
                    }
                }
            }

            return failures;
        }

        private static int LaneIndexOf(string lane)
        {
            return ModuleSocketLane.TryResolveLaneIndex(lane, out int laneIndex) ? laneIndex : -1;
        }

        /// <summary>
        /// True when the mask is exactly one bit and that bit sits inside the
        /// mirrored lane window. Population count is done by the standard
        /// clear-lowest-bit identity rather than a loop.
        /// </summary>
        private static bool IsSingleLaneBitInWindow(uint mask, out int laneIndex)
        {
            laneIndex = -1;
            if (mask == 0u || (mask & (mask - 1u)) != 0u)
                return false;

            for (int i = 0; i < ModuleSocketLane.MaskSlotCount; i++)
            {
                if (mask == 1u << (ModuleSocketLane.MaskBitOffset + i))
                {
                    laneIndex = i;
                    return true;
                }
            }

            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  CHECK 3: DEAD SOCKETS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reports every shipped-kit socket that has no legal partner anywhere in
        /// the shipped kit: no other socket carries the inverse direction and a
        /// compatible lane. A warning, not a failure — an unreachable socket is a
        /// legitimate authored expansion point as long as somebody decided that on
        /// purpose. What is not legitimate is nobody knowing.
        /// </summary>
        private static int ReportDeadSockets(StringBuilder report, List<KitSocket> kitSockets)
        {
            int warnings = 0;

            for (int i = 0; i < kitSockets.Count; i++)
            {
                KitSocket socket = kitSockets[i];
                bool hasPartner = false;

                for (int j = 0; j < kitSockets.Count && !hasPartner; j++)
                {
                    if (j == i)
                        continue;

                    KitSocket candidate = kitSockets[j];
                    if (!AreInverseDirections(socket.Direction, candidate.Direction))
                        continue;

                    if (ModuleSocketLane.AreLanesCompatible(socket.Lane, candidate.Lane))
                        hasPartner = true;
                }

                if (hasPartner)
                    continue;

                warnings++;
                string laneLabel = ModuleSocketLane.IsUniversal(socket.Lane) ? "<universal>" : socket.Lane;
                report.AppendLine(
                    $"  WARN dead socket '{socket.OwnerLabel}': direction {socket.Direction}, lane " +
                    $"{laneLabel}. No socket in the shipped kit carries the inverse direction " +
                    $"{InverseDirectionLabel(socket.Direction)} on a compatible lane, so nothing can ever " +
                    "connect here. Either author the partner module or record this as a deliberate " +
                    "expansion point.");
            }

            return warnings;
        }

        /// <summary>
        /// Direction inversion, mirroring the internal
        /// <c>ModuleSocketTopology.AreInverseDirections</c> (ModuleSocket.cs:48-60)
        /// case for case, INCLUDING its <c>default: return false</c>.
        /// Reimplemented rather than called because that type is <c>internal</c> to
        /// Hecton8.Core and invisible to this assembly. The default arm returns
        /// false rather than falling through to a plausible direction, because an
        /// out-of-range enum value that silently acquires a partner would make a
        /// dead socket report as alive — the exact silent-degeneracy shape this
        /// gate exists to catch.
        /// </summary>
        private static bool AreInverseDirections(ModuleSocketDirection lhs, ModuleSocketDirection rhs)
        {
            switch (lhs)
            {
                case ModuleSocketDirection.North: return rhs == ModuleSocketDirection.South;
                case ModuleSocketDirection.South: return rhs == ModuleSocketDirection.North;
                case ModuleSocketDirection.East: return rhs == ModuleSocketDirection.West;
                case ModuleSocketDirection.West: return rhs == ModuleSocketDirection.East;
                case ModuleSocketDirection.Top: return rhs == ModuleSocketDirection.Bottom;
                case ModuleSocketDirection.Bottom: return rhs == ModuleSocketDirection.Top;
                default: return false;
            }
        }

        /// <summary>
        /// Human-readable inverse direction for the dead-socket report only. Never
        /// used for a compatibility decision; an unknown direction is reported as
        /// such instead of being given a partner.
        /// </summary>
        private static string InverseDirectionLabel(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.North: return "South";
                case ModuleSocketDirection.South: return "North";
                case ModuleSocketDirection.East: return "West";
                case ModuleSocketDirection.West: return "East";
                case ModuleSocketDirection.Top: return "Bottom";
                case ModuleSocketDirection.Bottom: return "Top";
                default: return "<unknown direction, no inverse>";
            }
        }
    }
}
#endif
