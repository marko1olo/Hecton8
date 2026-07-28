using System.Collections.Generic;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Audits, and on a separate deliberate click lowers, the <c>minDepthMeters</c> floor on the
    /// technogenic procedural placement rules so the photic shallows can receive them.
    ///
    /// WHY THIS EXISTS. VISION_LOCKS.md:70 and world.md:35 both require technogenic history in the
    /// shallows - "old colony traces, wreck fragments, route hardware, pipes, stations, cables,
    /// salvage cuts". VISION_LOCKS.md:60 puts the photic band at 0-100 m. Measured against the
    /// authored rule set on 2026-07-28, that content cannot arrive in the top of that band:
    ///
    ///   rule.debris.scatter  minDepthMeters 30    rule.route.power    minDepthMeters 40
    ///   rule.service.scar    minDepthMeters 30    rule.debris.field   minDepthMeters 40
    ///                                             rule.debris.salvage minDepthMeters 40
    ///
    /// Those are the ONLY five non-ruin technogenic rules in the set, so 30 m is the shallowest
    /// depth at which any machine trace can appear anywhere. Every natural family reaches the
    /// waterline by comparison - rule.rocks.floor, rule.rocks.cluster, rule.coral.low,
    /// rule.coral.reef, rule.coral.branching, all four kelp rules, rule.landmark.spire,
    /// rule.pocket.safe and rule.fauna.passive are all authored at 0 m. The result is a shallow
    /// route that is structurally biota-only: the 0-30 m band, which is the brightest and most
    /// player-visible water on the route, can receive coral, kelp, rock and fauna but is incapable
    /// of receiving a single cable, scar, pipe or wreck fragment.
    ///
    /// The gate is unconditional. WorldProceduralPlacementRule.MatchesScatter
    /// (Assets/_Project/Scripts/WorldProceduralPlacementRule.cs:187) is a plain
    /// <c>if (depthMeters &lt; minDepthMeters) return false;</c> - no noise, no blend, no heat term
    /// can lift a candidate past it. A rule whose floor is above the water the player swims in is
    /// simply switched off there, silently, and reads as correctly authored in the inspector.
    ///
    /// WHAT THIS TOOL DELIBERATELY DOES NOT TOUCH. The rule.ruin.* floors (80 m, 80 m, 80 m and
    /// 120 m) are defensible and stay. Ruins are deep-water memory anchors by design - see the
    /// gameplayIntent on ProceduralRule_rule_ruin_megastructure ("deep-water memory anchors") - and
    /// the RuinModule domain is excluded from the predicate below for exactly that reason.
    ///
    /// HOW IT SELECTS. By domain, never by GUID and never by a hardcoded rule-name list, so a
    /// technogenic rule added later is covered automatically and a renamed asset does not slip
    /// through. WorldPrefabFamilyProfile.proceduralDomain
    /// (Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs:117) is the authored classification;
    /// Debris, PowerRoute and ServiceScar are the technogenic domains that belong in shallow water,
    /// and on the current data that predicate selects exactly the five rules listed above and
    /// nothing else.
    ///
    /// THE SECOND GATE, WHICH THIS TOOL DOES NOT CLOSE. Depth is the first of two gates. The rule
    /// also carries requiredHeatmapChannel plus minHeatmapValue, and the shallow patterns shape
    /// those channels down hard (WorldProceduralFieldSampler.cs:3889 and :3897 for FertileShallows,
    /// :3907 and :3915 for ReefNavigation). The audit prints each rule's channel and threshold so
    /// the second gate stays visible instead of being mistaken for closed. It is NOT a hard block
    /// the way depth is - the shaped value is lerped against the unshaped field value at
    /// WorldProceduralFieldSampler.cs:3742 with a blend of 0.18-0.78 depending on seafloor source,
    /// so the base field can still carry a candidate over the threshold - which is precisely why
    /// this tool changes only the gate that is unconditional and reports the one that is not.
    ///
    /// WHY IT NEVER SAVES. AGENTS.md Sandbox Firewall Rule forbids automated runners and scripts
    /// from calling EditorUtility.SetDirty, EditorSceneManager.SaveScene or
    /// PrefabUtility.SaveAsPrefabAsset on production assets, so no automated pass can wipe authored
    /// designer work. This tool therefore calls none of them and calls no AssetDatabase save either.
    /// It is a MenuItem a human invokes on purpose, the write is recorded with Undo so Ctrl+Z
    /// reverts it, and persisting it to disk is the human's explicit File > Save Project. It also
    /// refuses to run in batch mode, so -executeMethod cannot drive it, and it carries no
    /// [InitializeOnLoad], so opening the project changes nothing.
    ///
    /// The audit half is pure information and is always safe to run. Read the printed
    /// current -> proposed table; those numbers are what a human would type into the inspector by
    /// hand, so the audit remains useful even if the in-memory write does not stick on this Unity
    /// version.
    ///
    /// This class holds no mutable static state - every field is const or static readonly - so the
    /// disabled domain reload (m_EnterPlayModeOptions: 1) has nothing to stale here.
    /// </summary>
    public static class H8_TechnogenicShallowDepthFloorRepair
    {
        private const string Marker = "[H8_TECHNOGENIC_FLOOR]";
        private const string AuditMenuPath = "Hecton8/Diagnostics/Technogenic Shallow Depth Floor Audit";
        private const string RepairMenuPath = "Hecton8/Diagnostics/Lower Technogenic Shallow Depth Floors";
        private const string UndoLabel = "Lower technogenic shallow depth floor";

        /// <summary>
        /// Type filter for AssetDatabase.FindAssets. Deliberately unscoped by folder: the rules live
        /// under Assets/_Project/Data/World/ProceduralPlacementRules today, but a folder constant
        /// would silently miss a rule authored elsewhere, and a missed rule is the failure this tool
        /// exists to catch.
        /// </summary>
        private const string RuleSearchFilter = "t:WorldProceduralPlacementRule";

        /// <summary>
        /// Target floor for flat technogenic traces with no vertical extent - service scars,
        /// maintenance cuts, cable and relay runs. Zero, matching the house floor already authored
        /// for the other zero-extent families (rule.rocks.floor, rule.coral.low, rule.kelp.*, all
        /// at 0 m). Cables and route hardware descend from the surface, so this is the content the
        /// shallow band should carry first.
        /// </summary>
        private const float FlatTraceTargetFloorMeters = 0f;

        /// <summary>
        /// Target floor for debris, which unlike a scar has real vertical extent. Held just off the
        /// waterline so a wreck fragment cannot breach the ocean skin, and below the 6 m floor the
        /// project already authored for rule.coral.massive - the house precedent for a bulky natural
        /// form - because a debris fragment is smaller than a massive coral head.
        /// </summary>
        private const float DebrisTargetFloorMeters = 4f;

        /// <summary>
        /// Slack on the "already shallow enough" comparison so serialized float noise cannot
        /// provoke a write that changes nothing and pollutes the undo stack.
        /// </summary>
        private const float FloorComparisonEpsilonMeters = 0.001f;

        /// <summary>
        /// Photic band ceiling, VISION_LOCKS.md:60. Used only to classify a rule as shallow-relevant
        /// in the printed report.
        /// </summary>
        private const float PhoticBandDepthMeters = 100f;

        private sealed class RuleSighting
        {
            public WorldProceduralPlacementRule Rule;
            public string AssetPath;
            public string RuleId;
            public string FamilyId;
            public WorldPrefabFamilyProfile.ProceduralDomain Domain;
            public bool HasFamily;
            public bool IsShallowTechnogenic;
            public float CurrentFloorMeters;
            public float TargetFloorMeters;
            public bool NeedsLowering;
        }

        [MenuItem(AuditMenuPath)]
        public static void Audit()
        {
            if (!GuardAllowsRun() || !SelfTestPassed())
                return;

            RuleSighting[] sightings = CollectRules();
            if (sightings.Length == 0)
            {
                Debug.LogWarning(
                    Marker + " NO PLACEMENT RULES FOUND for filter " + RuleSearchFilter +
                    ". Either the rule assets are absent or the asset database has not imported " +
                    "them. Absence here is not evidence that the project has no placement rules.");
                return;
            }

            ReportSightings(sightings);
            ReportVerdict(sightings, RepairMenuPath);
        }

        [MenuItem(RepairMenuPath)]
        public static void LowerTechnogenicShallowFloors()
        {
            if (!GuardAllowsRun() || !SelfTestPassed())
                return;

            RuleSighting[] sightings = CollectRules();
            if (sightings.Length == 0)
            {
                Debug.LogWarning(
                    Marker + " NOTHING TO REPAIR - no placement rule assets matched " +
                    RuleSearchFilter + ".");
                return;
            }

            // Print the whole table BEFORE mutating anything, so the human sees every current and
            // proposed value even if they then undo the write.
            ReportSightings(sightings);

            int loweredCount = 0;
            for (int i = 0; i < sightings.Length; i++)
            {
                RuleSighting sighting = sightings[i];
                if (!sighting.IsShallowTechnogenic || !sighting.NeedsLowering)
                    continue;

                Undo.RecordObject(sighting.Rule, UndoLabel);
                sighting.Rule.minDepthMeters = sighting.TargetFloorMeters;
                loweredCount++;

                Debug.Log(
                    Marker + " LOWERED " + sighting.RuleId + "  minDepthMeters " +
                    FormatMeters(sighting.CurrentFloorMeters) + " -> " +
                    FormatMeters(sighting.TargetFloorMeters) + "  " + sighting.AssetPath,
                    sighting.Rule);
            }

            if (loweredCount == 0)
            {
                Debug.Log(
                    Marker + " NO CHANGE - every technogenic shallow rule is already at or below " +
                    "its target floor. Nothing was recorded and nothing was written.");
                return;
            }

            Debug.LogWarning(
                Marker + " IN-MEMORY ONLY: lowered the depth floor on " + loweredCount +
                " technogenic rule(s). The write is recorded for Undo (Ctrl+Z). This tool did NOT " +
                "call EditorUtility.SetDirty, AssetDatabase.SaveAssets or any other save, per the " +
                "AGENTS.md Sandbox Firewall Rule - so NOTHING ON DISK HAS CHANGED YET. Persist it " +
                "with File > Save Project, and confirm in the inspector that each value actually " +
                "reads its new floor. If a value did not stick, type the printed target by hand: " +
                "that is the same edit, and the printed table is the authority for it.");

            Debug.LogWarning(
                Marker + " DEPTH IS ONLY THE FIRST GATE. Re-check the scatter preview after saving. " +
                "If technogenic candidates still do not appear in shallow water, the remaining " +
                "rejection is the heat gate (requiredHeatmapChannel / minHeatmapValue against the " +
                "pattern-shaped channel in WorldProceduralFieldSampler.cs:3889/3897 for " +
                "FertileShallows and :3907/3915 for ReefNavigation), which this tool does not touch.");
        }

        /// <summary>
        /// Refuses batch mode so -executeMethod cannot drive this, and refuses Play Mode because a
        /// ScriptableObject write during play is the COMMON_SENSE.md:22 state-leak trap - the change
        /// would persist to disk as a side effect of a play session instead of a deliberate edit.
        /// </summary>
        private static bool GuardAllowsRun()
        {
            if (Application.isBatchMode)
            {
                Debug.LogError(
                    Marker + " REFUSED - this is a human-invoked menu item, not an automated pass. " +
                    "It will not run in batch mode, so -executeMethod cannot use it to rewrite " +
                    "production placement data.");
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    Marker + " REFUSED - exit Play Mode first. Writing a ScriptableObject field " +
                    "while playing persists the change to disk as a side effect of the play " +
                    "session, which is the exact state leak COMMON_SENSE.md:22 bans.");
                return false;
            }

            return true;
        }

        private static RuleSighting[] CollectRules()
        {
            string[] guids = AssetDatabase.FindAssets(RuleSearchFilter);
            if (guids == null || guids.Length == 0)
                return new RuleSighting[0];

            var sightings = new List<RuleSighting>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                WorldProceduralPlacementRule rule =
                    AssetDatabase.LoadAssetAtPath<WorldProceduralPlacementRule>(assetPath);
                if (rule == null)
                    continue;

                WorldPrefabFamilyProfile family = rule.familyProfile;
                bool hasFamily = family != null;
                WorldPrefabFamilyProfile.ProceduralDomain domain = hasFamily
                    ? family.proceduralDomain
                    : WorldPrefabFamilyProfile.ProceduralDomain.Generic;

                // targetFloor is initialised before the short-circuit so definite assignment never
                // depends on whether the && evaluated its right operand.
                float targetFloor = 0f;
                bool isShallowTechnogenic =
                    hasFamily && TryResolveShallowTechnogenicFloor(domain, out targetFloor);
                if (!isShallowTechnogenic)
                    targetFloor = 0f;

                float currentFloor = rule.minDepthMeters;

                sightings.Add(new RuleSighting
                {
                    Rule = rule,
                    AssetPath = assetPath,
                    RuleId = string.IsNullOrEmpty(rule.ruleId) ? rule.name : rule.ruleId,
                    FamilyId = hasFamily && !string.IsNullOrEmpty(family.familyId)
                        ? family.familyId
                        : "<no family>",
                    Domain = domain,
                    HasFamily = hasFamily,
                    IsShallowTechnogenic = isShallowTechnogenic,
                    CurrentFloorMeters = currentFloor,
                    TargetFloorMeters = targetFloor,
                    NeedsLowering = isShallowTechnogenic && NeedsLowering(currentFloor, targetFloor),
                });
            }

            return sightings.ToArray();
        }

        /// <summary>
        /// The selection predicate. Debris, PowerRoute and ServiceScar are the technogenic domains
        /// that VISION_LOCKS.md:70 places in shallow water. RuinModule is technogenic too and is
        /// deliberately absent: its 80-120 m floors are correct and this tool must never lower them.
        /// Every natural domain is absent as well - their floors are already authored at 0 m where
        /// they belong.
        /// Pure function of the domain, so the two known-answer cases in <see cref="SelfTestPassed"/>
        /// fully cover it.
        /// </summary>
        internal static bool TryResolveShallowTechnogenicFloor(
            WorldPrefabFamilyProfile.ProceduralDomain domain,
            out float targetFloorMeters)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                    targetFloorMeters = FlatTraceTargetFloorMeters;
                    return true;
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                    targetFloorMeters = DebrisTargetFloorMeters;
                    return true;
                default:
                    targetFloorMeters = 0f;
                    return false;
            }
        }

        /// <summary>
        /// A rule already at or below its target is left alone, so re-running this tool is a no-op
        /// and it can never raise a floor a designer chose to author shallower than the target.
        /// </summary>
        internal static bool NeedsLowering(float currentFloorMeters, float targetFloorMeters)
        {
            return currentFloorMeters > targetFloorMeters + FloorComparisonEpsilonMeters;
        }

        private static void ReportSightings(RuleSighting[] sightings)
        {
            var line = new StringBuilder(256);
            for (int i = 0; i < sightings.Length; i++)
            {
                RuleSighting sighting = sightings[i];
                line.Length = 0;
                line.Append(Marker);
                line.Append(sighting.IsShallowTechnogenic ? " TECHNOGENIC " : " OTHER       ");
                line.Append(sighting.RuleId);
                line.Append("  domain=");
                line.Append(sighting.HasFamily ? sighting.Domain.ToString() : "<no family>");
                line.Append("  minDepthMeters=");
                line.Append(FormatMeters(sighting.CurrentFloorMeters));

                if (sighting.IsShallowTechnogenic)
                {
                    line.Append(" -> proposed ");
                    line.Append(FormatMeters(sighting.TargetFloorMeters));
                    line.Append(sighting.NeedsLowering ? "  ACTION=LOWER" : "  ACTION=NONE(already shallow)");
                }
                else
                {
                    line.Append("  ACTION=NONE(out of scope: this tool only lowers Debris, ");
                    line.Append("PowerRoute and ServiceScar)");
                }

                line.Append("  heatGate=");
                line.Append(string.IsNullOrEmpty(sighting.Rule.requiredHeatmapChannel)
                    ? "<none>"
                    : sighting.Rule.requiredHeatmapChannel);
                line.Append(">=");
                line.Append(sighting.Rule.minHeatmapValue.ToString("0.###"));
                line.Append("  ");
                line.Append(sighting.AssetPath);

                Debug.Log(line.ToString(), sighting.Rule);
            }
        }

        /// <summary>
        /// Prints the contrast that makes the defect self-evident: the shallowest floor the
        /// technogenic rules reach against the shallowest floor everything else reaches. Both
        /// numbers are computed from the live assets, so this verdict cannot go stale the way a
        /// number written into a comment would.
        /// </summary>
        private static void ReportVerdict(RuleSighting[] sightings, string repairMenuPath)
        {
            float shallowestTechnogenic = float.MaxValue;
            float shallowestOther = float.MaxValue;
            int technogenicCount = 0;
            int otherCount = 0;
            int needsLoweringCount = 0;

            for (int i = 0; i < sightings.Length; i++)
            {
                RuleSighting sighting = sightings[i];
                if (sighting.IsShallowTechnogenic)
                {
                    technogenicCount++;
                    if (sighting.CurrentFloorMeters < shallowestTechnogenic)
                        shallowestTechnogenic = sighting.CurrentFloorMeters;
                    if (sighting.NeedsLowering)
                        needsLoweringCount++;
                }
                else
                {
                    otherCount++;
                    if (sighting.CurrentFloorMeters < shallowestOther)
                        shallowestOther = sighting.CurrentFloorMeters;
                }
            }

            if (technogenicCount == 0)
            {
                Debug.LogError(
                    Marker + " VERDICT NO technogenic shallow rule exists at all. Not one rule in " +
                    "the project carries a Debris, PowerRoute or ServiceScar family, so the " +
                    "shallows cannot receive machine history at any depth. Lowering a floor cannot " +
                    "fix this - a rule has to be authored.");
                return;
            }

            var verdict = new StringBuilder(512);
            verdict.Append(Marker);
            verdict.Append(" VERDICT shallowest technogenic floor = ");
            verdict.Append(FormatMeters(shallowestTechnogenic));
            verdict.Append(" m across ");
            verdict.Append(technogenicCount);
            verdict.Append(" rule(s); shallowest other floor = ");
            verdict.Append(otherCount > 0 ? FormatMeters(shallowestOther) : "n/a");
            verdict.Append(" m across ");
            verdict.Append(otherCount);
            verdict.Append(" rule(s). Photic band is 0-");
            verdict.Append(FormatMeters(PhoticBandDepthMeters));
            verdict.Append(" m (VISION_LOCKS.md:60).");

            if (needsLoweringCount == 0)
            {
                verdict.Append(" Every technogenic rule is already at or below its target floor - ");
                verdict.Append("nothing to lower.");
                Debug.Log(verdict.ToString());
                return;
            }

            verdict.Append(' ');
            verdict.Append(needsLoweringCount);
            verdict.Append(" technogenic rule(s) are floored out of the top ");
            verdict.Append(FormatMeters(shallowestTechnogenic));
            verdict.Append(" m of the photic band, so that water can receive coral, kelp, rock and ");
            verdict.Append("fauna but no cable, scar, pipe or wreck fragment - against ");
            verdict.Append("VISION_LOCKS.md:70 and world.md:35. Lower them with: ");
            verdict.Append(repairMenuPath);
            verdict.Append(" (nothing is written to disk until you save the project yourself).");

            Debug.LogError(verdict.ToString());
        }

        private static string FormatMeters(float meters)
        {
            return meters.ToString("0.###");
        }

        /// <summary>
        /// Known-answer cases for the two pure functions this tool decides with, run before it
        /// prints or changes anything. A tool that mis-classifies a rule would either miss the
        /// defect or lower a ruin floor that must stay deep, so a failure here suppresses the whole
        /// run rather than reporting a number it cannot compute correctly.
        /// </summary>
        private static bool SelfTestPassed()
        {
            // Selection: the three shallow technogenic domains resolve, with the flat-trace domains
            // at the waterline and debris held just off it.
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar, true, FlatTraceTargetFloorMeters))
                return false;
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute, true, FlatTraceTargetFloorMeters))
                return false;
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.Debris, true, DebrisTargetFloorMeters))
                return false;

            // Exclusion: ruins must never be selected, and neither may a natural domain.
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.RuinModule, false, 0f))
                return false;
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.Coral, false, 0f))
                return false;
            if (!ExpectFloor(WorldPrefabFamilyProfile.ProceduralDomain.Generic, false, 0f))
                return false;

            // Idempotence: an authored floor above target lowers, one at or below target does not.
            if (!ExpectNeedsLowering(30f, DebrisTargetFloorMeters, true))
                return false;
            if (!ExpectNeedsLowering(DebrisTargetFloorMeters, DebrisTargetFloorMeters, false))
                return false;
            if (!ExpectNeedsLowering(0f, DebrisTargetFloorMeters, false))
                return false;
            if (!ExpectNeedsLowering(0f, FlatTraceTargetFloorMeters, false))
                return false;

            return true;
        }

        private static bool ExpectFloor(
            WorldPrefabFamilyProfile.ProceduralDomain domain,
            bool expectedSelected,
            float expectedFloorMeters)
        {
            bool selected = TryResolveShallowTechnogenicFloor(domain, out float floor);
            if (selected == expectedSelected &&
                (!expectedSelected || Mathf.Approximately(floor, expectedFloorMeters)))
            {
                return true;
            }

            Debug.LogError(
                Marker + " SELF-TEST FAILED domain " + domain + " expected selected=" +
                (expectedSelected ? "1" : "0") + " floor=" + FormatMeters(expectedFloorMeters) +
                " but got selected=" + (selected ? "1" : "0") + " floor=" + FormatMeters(floor) +
                ". Run suppressed - no rule was read and none was changed.");
            return false;
        }

        private static bool ExpectNeedsLowering(
            float currentFloorMeters,
            float targetFloorMeters,
            bool expected)
        {
            bool actual = NeedsLowering(currentFloorMeters, targetFloorMeters);
            if (actual == expected)
                return true;

            Debug.LogError(
                Marker + " SELF-TEST FAILED NeedsLowering(" + FormatMeters(currentFloorMeters) +
                ", " + FormatMeters(targetFloorMeters) + ") expected " +
                (expected ? "1" : "0") + " got " + (actual ? "1" : "0") +
                ". Run suppressed - no rule was read and none was changed.");
            return false;
        }
    }
}
