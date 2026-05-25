# Abandoned Outpost Failure Modes

Date: 2026-05-17
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: MISSION_FAIL_SAFE_ARCHITECT
Domain: Documentation/Logic, Echelon 8 AUP Narrative Triggers
Evidence Class: STATIC_DOC / STATIC_SOURCE

## Source Boundary

The requested `CURRENT_BATCH_OSHINO.md` file is absent in this workspace. Historical extraction source: the Mission Fail-Safe prompt was originally extracted from `Docs/Tasks/CURRENT_BATCH.md` when that file contained `<AGENT_PROMPT id="MISSION_FAIL_SAFE_ARCHITECT">`.

Current source authority: `ACTIVE_BATCH_DRIFT_DETECTED`. The live `Docs/Tasks/CURRENT_BATCH.md` no longer contains `MISSION_FAIL_SAFE_ARCHITECT` or `SCENARIO_DESIGNER`. Treat this document and `Docs/Design/Missions/Outpost_FailSafe_Handoff.json` as historical static design artifacts until the Mission Fail-Safe prompt is restored to the live batch file or an archived prompt is explicitly accepted as source authority.

`Docs/Tasks/Status_META_CAMPAIGN_DIRECTOR.md` is also absent. Current source evidence shows `MetaCampaignService` has four implemented global variables: `CampaignStageHash`, `ToxicityLevelHash`, `LeviathanAwakenedHash`, and `BaseDeltaDestroyedHash`. The outpost mission therefore needs its own compiled quest/DAG flags and must not assume a richer campaign-state file exists.

Current WFC outpost source exposes these generated cell kinds: `Empty`, `Corridor`, `Room`, `Hatch`, `Datapad`, `SealedDoor`, `Window`, and `Pillar`. There is no `Generator` cell kind in the current source. The Generator-room failure is therefore a live design risk, not a theoretical edge case.

Mandates followed:
- `PROG_Quest_State_Graph_Logic`
- `UI_Diegetic_Physical_Interfaces`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow`
- `PHYS_Fluid_Incursion_Interior`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits`
- `QA_Evidence_Text_Filter_Audit`

Machine-readable handoff: `Docs/Design/Missions/Outpost_FailSafe_Handoff.json`. It contains the authored DAG flags, topological order, fallback rules, 10 tooltip entries, 5 Marauder log entries, and `LocHash`-compatible FNV hashes. Runtime localization assets were not mutated in this pass because the active language table, generated `LocKeys`, and translated language tables must be baked together.

Editor validation hook: `Hecton-8/Validate Outpost Fail-Safe Handoff`, implemented by `Assets/_Project/Scripts/Editor/OutpostFailSafeHandoffValidator.cs`. It is editor-only and validates the handoff JSON plus this prose document for schema, source-authority, hash, flag-reference, stale-alias, tooltip/log-shape, and gas-limit drift before a quest/localization bake. It also rejects legacy room-flag namespace tokens, unsupported `GasDynamicsRoomFlags.*` values, and bare `Submerged` flag claims; submerged-room logic must use the `roomSubmerged01` scalar because the gas enum has no `Submerged` flag.

## Mission Rule

The Abandoned Outpost mission is a DAG, not a story script. Every progression step must be reachable from event-driven flags, a fallback marker, and a revert path. WFC topology is presentation and placement authority; it is not allowed to be the only owner of a critical path.

The mission succeeds when the player:
1. reaches the outpost shell,
2. identifies the brownout,
3. restores minimum relay power by normal generator route or Ghost Power fallback,
4. powers one safe route and one evidence terminal,
5. reads the Marauder evidence,
6. exits or marks the outpost as recoverable.

No mission objective may require a room that is `Breached`, `InternalFire`, unreachable from the entrance, or dependent on an absent generated room role.

## Outpost DAG Variables

These are authored logical flags. Runtime implementation must compile them into hash-backed quest/narrative/location/deadlock bands. Names are stable source labels, not runtime string comparisons.

| # | Flag | Band | Set By | Blocks |
| --- | --- | --- | --- | --- |
| 1 | `outpost.generated` | Location | outpost generation snapshot `Ready` | all outpost mission nodes |
| 2 | `outpost.generation_faulted` | Deadlock | outpost snapshot `Faulted` | switches to marker-only recovery |
| 3 | `outpost.entry_reached` | Location | player reaches entry marker | repair tutorial start |
| 4 | `outpost.main_path_connected` | Location | WFC connectivity audit | direct critical path |
| 5 | `outpost.generator_room_present` | Location | role audit finds generator-capable cell | normal power route |
| 6 | `outpost.ghost_power_enabled` | Quest | generator role absent or inaccessible | fail-safe power route |
| 7 | `outpost.power_relay_found` | Location | relay or fallback relay scanned | coupler objective |
| 8 | `outpost.power_coupler_acquired` | Item | item or fallback coupler acquired | install objective |
| 9 | `outpost.power_coupler_installed` | Quest | relay interaction completed | voltage test |
| 10 | `outpost.bus_voltage_stable` | Quest | supply ratio or Ghost Power relay valid | door/scrubber enable |
| 11 | `outpost.brownout_cleared` | Quest | brownout tier drops or fallback route latches | final terminal |
| 12 | `outpost.scrubber_powered` | Quest | safe-room scrubber receives power | longer read time |
| 13 | `outpost.safe_air_confirmed` | Quest | gas snapshot safe or room marked external-suit-only | terminal read |
| 14 | `outpost.sealed_door_powered` | Quest | voltage stable at door relay | sealed door interaction |
| 15 | `outpost.sealed_door_opened` | Quest | door open event | evidence room |
| 16 | `outpost.datapad_found` | Narrative | datapad scan or fallback terminal scan | Marauder log chain |
| 17 | `outpost.marauder_log_power_read` | Narrative | power fallback log committed | lore continuity |
| 18 | `outpost.marauder_log_air_read` | Narrative | air/scrubber log committed | O2 sanity branch |
| 19 | `outpost.marauder_log_fire_read` | Narrative | fire log committed | fire lore branch |
| 20 | `outpost.marauder_log_breach_read` | Narrative | breach log committed | breach lore branch |
| 21 | `outpost.marauder_log_exit_read` | Narrative | exit discipline log committed | mission complete |
| 22 | `outpost.internal_fire_seen` | Location | room flag `InternalFire` observed | fire-route blocker |
| 23 | `outpost.fire_route_optional` | Quest | alternate route exists or fire door stays locked | prevents fire soft-lock |
| 24 | `outpost.breached_room_seen` | Location | room flag `Breached` observed | breached-room blocker |
| 25 | `outpost.flooded_room_bypassed` | Quest | alternate route or marker reroute active | prevents submerged O2 trap |
| 26 | `outpost.exit_route_marked` | Location | entrance marker or emergency hatch marked | retreat guarantee |
| 27 | `outpost.critical_item_lost` | Deadlock | coupler/key discarded or destroyed | revert kernel |
| 28 | `outpost.deadlock_revert_requested` | Deadlock | quest revert emitted | respawn/fallback grant |
| 29 | `outpost.marker_fallback_active` | Quest | marker target unresolved | no-marker recovery |
| 30 | `outpost.state_restored_from_save` | Quest | WFC state override applied | post-load validation |
| 31 | `outpost.evidence_uploaded` | Narrative | final evidence terminal committed | completion gate |
| 32 | `outpost.mission_complete` | Quest | evidence uploaded and exit known | reward/unlock |

Required topological ordering:

The machine-readable handoff must list all 32 flags exactly once. Critical-path order:

`outpost.generated` -> `outpost.entry_reached` -> `outpost.main_path_connected` -> `outpost.power_relay_found` -> `outpost.power_coupler_acquired` -> `outpost.power_coupler_installed` -> `outpost.bus_voltage_stable` -> `outpost.brownout_cleared` -> `outpost.sealed_door_powered` -> `outpost.sealed_door_opened` -> `outpost.datapad_found` -> Marauder log reads -> `outpost.evidence_uploaded` -> `outpost.mission_complete`

Fallback branch:

`outpost.generated` + NOT `outpost.generator_room_present` -> `outpost.ghost_power_enabled` -> `outpost.bus_voltage_stable`

Deadlock branch:

`outpost.critical_item_lost` -> `outpost.deadlock_revert_requested` -> re-grant coupler or bind Ghost Power relay -> clear `outpost.critical_item_lost`

## Ghost Power Fallback Rule

Ghost Power is not a physical generator. It is a deterministic mission fail-safe that represents a sealed reserve bus, a decaying RTG trickle, or a charged service capacitor below the floor plates. It powers only mission-critical endpoints. It must not lie to the full power grid, fabricate resource generation, or satisfy unrelated crafting/base demands.

Activation:
- If the WFC role audit finds no generator-capable room, set `outpost.ghost_power_enabled`.
- If a generator-like room exists but is unreachable, breached, burning, or behind its own power gate, set `outpost.ghost_power_enabled`.
- If the power coupler is lost after acquisition, set `outpost.deadlock_revert_requested` and either respawn the coupler at the original hashed spawn or bind Ghost Power to the relay.

Scope:
- Allowed endpoints: entrance marker, one relay panel, one sealed door, one evidence terminal, one scrubber if the safe room exists.
- Forbidden endpoints: full base power, crafting stations, vehicle charge, unrelated outpost rooms, resource production.

Determinism:
- Use the outpost sector hash and world seed to select the fallback relay anchor.
- Preferred anchor order: first generated `Datapad`, first `SealedDoor`, entry corridor cell, marker fallback position.
- If no interactable cell exists, the entry marker becomes the fallback relay target and the mission stays completable without spawning a new room.

Presentation:
- Minimum quality: one static panel glow and one short audio tick.
- Intermediate quality: panel glow plus brownout flicker.
- High quality: brownout flicker, relay hum, and a single wet spark VFX.
- Maximum quality: richer relay arcing, layered alarm audio, and terminal CRT decay. Quest truth remains the same bit.

Rejected alternative: forcing the WFC to always generate a Generator room. That hides the soft-lock instead of handling it, and the current source does not expose a Generator cell kind.

## Edge-Case Matrix

| Failure | Trigger | Bad Result Without Guard | Required Fail-Safe |
| --- | --- | --- | --- |
| No Generator room | WFC role audit finds no generator-capable cell | power repair cannot start | set `outpost.ghost_power_enabled` and bind relay to fallback anchor |
| Generator unreachable | connectivity audit fails path from entry to generator | player sees objective through wall or locked path | mark normal route blocked, activate Ghost Power |
| Generator in `Breached` room | gas room flag `Breached` on generator room | critical objective in zero-O2 room | generator route invalid, use Ghost Power |
| Generator in `InternalFire` room | gas room flag `InternalFire` on generator room | fire depletes O2 before repair | route is optional only; Ghost Power carries critical path |
| No Datapad generated | generated cells lack `Datapad` | final evidence cannot be read | bind evidence to fallback relay terminal |
| No SealedDoor generated | generated cells lack `SealedDoor` | door objective cannot complete | skip door node and require evidence terminal only |
| Door requires its own power | door relay is behind the sealed door | circular dependency | door power must be fed by relay outside the door room or Ghost Power |
| Coupler lost | player drops/destroys critical coupler | repair path cannot complete | quest revert requests respawn or Ghost Power binding |
| Marker target unresolved | no target hash, no world position | player gets no objective location | set `outpost.marker_fallback_active` and use entry marker |
| Power telemetry deficit persists | `HasPowerDeficit` stays true after coupler | brownout never clears | completion reads local relay stable OR Ghost Power latch, not global grid health |
| Battery reserve only | `EmergencyReserveActive` true | player mistakes reserve for full power | tooltip says reserve bus, only mission endpoints energize |
| Scrubber unpowered | safe room has no powered scrubber | CO2 climbs during log read | log terminal is short, or scrubber becomes an allowed Ghost Power endpoint |
| CO2 rises in sealed room | no scrubber, player present | toxicity before player understands repair | critical text exposure capped below 90 seconds |
| Breached room on main path | `Breached` flag on required room | O2 and CO2 are zeroed by solver | reroute mission marker, never require prolonged stay |
| Flooded/submerged room | submerged fraction clamps O2 | oxygen vanishes despite UI prompt | mark as bypass or external-suit-only optional room |
| Internal fire lore mismatch | log mentions fire but no `InternalFire` | physical/lore contradiction | require matching room flag or rewrite log |
| Breach lore mismatch | log mentions pressure loss but no `Breached` | physical/lore contradiction | require `Breached` or rewrite log |
| WFC state lost on save/load | outpost mutation bitmask not restored | completed door/relay re-locks | block completion until `outpost.state_restored_from_save` verified |
| Signal flood | many scan/quest/power events same frame | queue drop hides completion | single mission transition per signal; repeatable signals are idempotent |
| Minimum-budget grid too small | 5x5x3 outpost lacks route variety | no alternate path | Ghost Power and fallback terminal are mandatory on minimum-budget layouts |
| Player repairs before tutorial | sequence broken by skilled play | tooltip order desync | tooltips suppress by flags; completion logic does not depend on seeing text |

## Diegetic Tooltip Text

These are exact English source strings for localization handoff. Runtime must resolve by hashed keys and write through the zero-GC text path. Do not paste these as per-frame literal strings in code.

| # | Key | Trigger | Text | Suppress When |
| --- | --- | --- | --- | --- |
| 1 | `TIP_OUTPOST_REPAIR_01_EXIT` | `outpost.entry_reached` | `MARAUDER FIELD NOTE: Mark the way out before touching the prize. A lit panel is not a promise.` | `outpost.exit_route_marked` |
| 2 | `TIP_OUTPOST_REPAIR_02_BROWNOUT` | first brownout telemetry near relay | `POWER BUS: Feed is starving. The relay is awake enough to accuse you, not enough to open doors.` | `outpost.power_relay_found` |
| 3 | `TIP_OUTPOST_REPAIR_03_TRACE` | relay scanned | `TRACE PAINT: Green runs to feed. Red runs to load. Follow the paint, ignore the sparks.` | `outpost.power_coupler_acquired` |
| 4 | `TIP_OUTPOST_REPAIR_04_COUPLER` | coupler visible or fallback coupler granted | `MARAUDER NOTE: Pull the coupler clean. Bent teeth mean heat, heat means lies in the gauge.` | `outpost.power_coupler_installed` |
| 5 | `TIP_OUTPOST_REPAIR_05_INSTALL` | relay has missing coupler | `RELAY PLATE: Seat the coupler until the bus hums steady. If it chatters, back away.` | `outpost.bus_voltage_stable` |
| 6 | `TIP_OUTPOST_REPAIR_06_GHOST_POWER` | `outpost.ghost_power_enabled` | `RESERVE BUS: No generator on this deck. Something under the floor still remembers its last charge.` | `outpost.bus_voltage_stable` |
| 7 | `TIP_OUTPOST_REPAIR_07_DOOR` | sealed door gets mission power | `LOCK STATUS: Door motor has one clean pull left. Waste it only if the exit is still marked.` | `outpost.sealed_door_opened` |
| 8 | `TIP_OUTPOST_REPAIR_08_AIR` | safe room terminal powers | `AIR LEDGER: Scrubber is cutting CO2, not making oxygen. Read fast. Breathe slow.` | `outpost.safe_air_confirmed` |
| 9 | `TIP_OUTPOST_REPAIR_09_LOG` | datapad/evidence terminal found | `MARAUDER RULE: A log is not loot until its room agrees with it. Check scorch, breach, and gauge.` | `outpost.marauder_log_exit_read` |
| 10 | `TIP_OUTPOST_REPAIR_10_LEAVE` | evidence uploaded | `CLAIM MARK: Evidence copied. The outpost can keep its dead power. You keep the route home.` | `outpost.mission_complete` |

## Marauder Log Text And Physical Flags

Every log must match generated room state. If the required flags are absent, the log must not spawn there.

| Log | Exact Text | Required Physical State |
| --- | --- | --- |
| `OUTPOST_LOG_POWER_01` | `Generator bay is missing from the company map. Not sealed. Missing. We found a reserve bus under deck two, warm enough to twitch a relay and honest enough to die after it opens one door.` | `outpost.ghost_power_enabled` OR `outpost.generator_room_present`; room cell `Datapad` or fallback relay terminal |
| `OUTPOST_LOG_AIR_02` | `Air count starts when the panel wakes. Scrubber cuts CO2, it does not give oxygen back. Ninety seconds for the read, less if your hands shake.` | `GasDynamicsRoomFlags.ScrubberInstalled`; if unpowered, no long mandatory read; if powered, `outpost.scrubber_powered` |
| `OUTPOST_LOG_FIRE_03` | `Red load line burned clean through the paint. We sealed that room and left the coupler teeth on the deck. Anyone sending a diver through fire wants the diver dead.` | `GasDynamicsRoomFlags.InternalFire` on referenced room; route must be optional |
| `OUTPOST_LOG_BREACH_04` | `Pressure door held. Wall did not. The gauge hit ambient and nobody argued with it. If your map points through that room, your map is lying.` | `GasDynamicsRoomFlags.Breached` on referenced room; no critical item inside |
| `OUTPOST_LOG_EXIT_05` | `Claim marker is on the return rail. Prize waits after the exit is marked twice. Marauders do not get paid for dying in rooms they already understood.` | `outpost.exit_route_marked`; cell near entry or fallback marker |

Forbidden lore mismatch:
- fire text in a room without `InternalFire`,
- breach text in a room without `Breached`,
- oxygen-production text from a scrubber,
- generator text when only Ghost Power is active,
- Marauder certainty without a visible gauge, mark, or room-state proof.

## Gas And O2 Re-Verification

Current scalar gas source constants:
- standard oxygen: `21.22 kPa`,
- player O2 drain: `0.012 kPa/s` before stress and heart multipliers,
- player CO2 production: `0.010 kPa/s`,
- fire O2 drain: `0.080 kPa/s * 5 = 0.400 kPa/s`,
- scrubber CO2 removal: `0.055 kPa/s`,
- CO2 toxicity starts at `1.0 kPa`,
- narcosis starts at `4 atm`.

Mission constraints:
- A sealed unpowered room cannot force more than 90 seconds of reading/repair. CO2 reaches the 1.0 kPa warning threshold in about 100 seconds at default player production before stress.
- A room with `InternalFire` cannot hold a critical objective. Fire alone can consume a standard room oxygen partial in about 53 seconds; with stressed player consumption the margin is lower.
- A `Breached` room cannot hold critical evidence or repair parts. The solver zeroes oxygen and carbon dioxide and replaces nitrogen from ambient pressure.
- A submerged room cannot be the only route. The solver clamps oxygen by dry fraction.
- Scrubbers reduce CO2. They do not create O2. Ghost Power may power a scrubber, but the mission must still assume finite oxygen.
- The critical path must be readable, repairable, and exit-confirmable within one standard safe-room oxygen reserve, or it must be external-suit-only with an exit marker active.

Result: the Outpost mission does not require more O2 than the base can produce because the fallback route does not claim oxygen production. It only energizes the relay/door/terminal/scrubber endpoints needed to finish the mission and leave.

## Implementation Constraints For Future Runtime Work

- Do not add a new single-use EventID for Ghost Power. Store it as a quest/DAG flag and let door/terminal adapters query the cached quest state through the approved interface path.
- Do not call `GlobalRegistry.Get<T>()` in `Tick`, Burst jobs, or UI render loops. Cache dependencies during initialization.
- Do not mutate ScriptableObject quest data at runtime.
- Do not create room GameObjects for shell pieces. Current outpost shell is matrix/GPU-buffer oriented; only interactables may become pooled proxies.
- Do not update tooltip text with per-frame string assignment. Use localization key hashes and fixed char buffers.
- Do not treat this document as Unity verification. It is a static mission contract.

## Verification Checklist

- [ ] Static compile of quest/outpost/gas/power sources after any runtime implementation.
- [ ] Unity Console after scene import.
- [ ] Play Mode route test: normal generator route.
- [ ] Play Mode route test: no-generator Ghost Power route.
- [ ] Play Mode route test: lost coupler revert.
- [ ] Play Mode route test: breached/fire room cannot be critical path.
- [ ] GCMonitor: tooltip and quest transition path remains 0 B/frame.
- [ ] Save/load: relay, door, evidence, and WFC mutable bitmask restore.
