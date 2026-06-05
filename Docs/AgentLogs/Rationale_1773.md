# Rationale 1773 - Scanner / Field Notes / Specimen Cards

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Authority Loaded

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `writing.md`
- `narrative.md`
- `localization.md`
- `textes.md`
- `Docs/Lore/Lore_Bible.md`
- `Docs/Lore/Canon_Locks.md`
- `Docs/Lore/Lore_Content_System.md`
- `Docs/Lore/Lore_Localization_Model.md`
- `Docs/Lore/Lore_Multilingual_Content_Architecture.md`
- `Docs/Lore/HECTON8_Field_Atlas.md`
- `Docs/Lore/HECTON8_Resource_Gameplay_Catalog.md`
- `Docs/Lore/AppliedContent/README.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`

## Decisions

- Scanner edits must stay sensor-level: observed feature, confidence or uncertainty, hazard/action/use, one hook.
- Field-note edits must read as in-world practical artifacts, not writer instructions or tutorial specs.
- Surface and photic-shelf wording must preserve bright/readable/beautiful baseline; bleakness belongs to depth, caves, storms, interiors, and temporary eclipse windows.
- Existing non-English localized rows are not native-final proof. If English source changes and localized rows already exist, mark review status in audit/status notes rather than silently claiming synchronized translation.
- Inventory and validation claims are static evidence only. No Unity/runtime readiness is implied by this content pass.

## Packet Edit Decisions

- `P292` field note replaced authoring/tutorial instruction with observed grazer freeze warning; in-game wiki rewritten to match scan-stage uncertainty.
- `P293` field note and wiki rewritten from "card teaches" language into route-light handling: current/residue/predator pull.
- `P294` scanner/field note/wiki rewritten to make brine vanes a density-navigation object with false-floor risk.
- `P295` field note/wiki rewritten to keep sensor-tagged fauna as animal plus contaminated telemetry, not possession or omniscient Atlas intent.
- `P351` to `P355` rewritten so geology/resource scanners carry cut/scan/containment/pressure-history action instead of guide labels.
- `P411` to `P415` rewritten so fauna scanner rows expose trace, behavior and uncertainty before species or system truth.
- `P426` to `P430` rewritten so resource economy artifacts route to custody, contamination, corrosion, fracture state, payout, receiver risk and evidence value.

## Validation Decisions

- Fixed a JSON comma error in `RS059_ECOLOGY_CODEX_SPECIMEN_CARDS.packets.json` immediately after parse failure.
- Did not edit unrelated `P456_SITE_HOME_LONGFORM_BRIEF.md` frontmatter blocker from source-only AppliedLore audit; recorded it in validation and handoff.
- Did not rewrite non-English locale rows in this pass. They remain draft/stale and require native/UI review after the English source changes.

## Code Follow-Up Decision

- Patched existing runtime owner and existing verifier only. `Lock()` must leave `Locked` as the final lifecycle state after scan teardown; no new helper, manager, or parallel structure was needed.
- Kept late-frame dispatcher registration lifecycle-cold. A demand-driven unregister path would reduce idle callbacks, but it would move dispatcher registration onto the scan call stack; that is worse than the current cold-registration contract.
- Completion events must flush before `DisableFragment()` because disabling the behaviour can run lifecycle cleanup and clear queued compatibility events.
- PDA scanner unlocks may preserve the unlock when `ScanCompleteSignal` AUP is invalid, but they must not mark the discovery position as precise. Finite AUP proof is required before `StateFlagPreciseAup` or `MetaFlagPreciseAup` can be written.
