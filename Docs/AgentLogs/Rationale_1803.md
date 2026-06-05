# Rationale 1803 - FIRST20 Gameplay Route Blocker Auditor

Updated: 2026-06-04 04:18 +04:00

## Non-Trivial Decisions

- Audit scope stays static unless a free Unity slot is objectively available. The task forbids fighting the current verification agent and static proof cannot become runtime proof.
- Selected mandate set for this audit: survival/oxygen/pressure, tools/interaction, inventory/resource data, sonar/navigation, diegetic UI, creature/encounter pressure, performance budgets, telemetry/black-box. These cover the first-20 route blockers without unrelated asset-generation or platform scope.
- Classified `WorldContentSocket`/`futurePrefabKey` route entries as BLOCKED STATIC for acceptance. They prove authored intent, not live gameplay population, interaction, save identity, or route readability.
- Classified starter copper interaction as BLOCKED STATIC. Copper Vein requires `requiredToolClass: 2`; the visible loadout provisioner is explicitly a development helper with startup provisioning disabled.
- Classified `Data_Copper` as contaminated until catalog proof exists. Two assets share `stableId: Data_Copper` while disagreeing on raw-resource semantics and world prefab.
- Classified first-depth pressure as BLOCKED STATIC. Survival pressure damage is a no-op owner handoff and the visible movement crush path is an extreme-depth presentation/wipeout path, not first-20 pressure gameplay proof.
- Did not run Unity, Play Mode, build, profiler, or screenshots. Active Unity processes were present; no runtime proof was claimed.

## Locked Route Assumptions

- The accepted first-20 route is not "craft Copper Wire and stop." It must prove boot, beautiful shallow exit, swim, oxygen/pressure, fair danger, resource/tool acquisition, craft/repair/build route impact, save/load, and same-state return.
- Visual acceptance requires surface, sky, Aegir/moons, coastline, ocean surface, photic shallows, and medium-depth route quality. Darkness/noir is not allowed to hide weak surface or water art.
- Graphics, optimization, and gameplay all have to pass. Static architecture without player-route proof remains PENDING VERIFICATION.
- Quality scaling must remain continuous through `GlobalQualityWeight`: compact, middle, high, and ultra change fidelity/cadence/capacity only, not gameplay truth, DTO identity, save ownership, or authority routes.
