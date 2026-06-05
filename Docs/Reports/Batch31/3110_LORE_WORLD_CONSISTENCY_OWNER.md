# 3110 Lore / World Consistency Owner

Status: STATIC ROUTE DECISION / NO SCENE ACCEPTANCE

Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.

Runtime, Unity scene, profiler, GC, save/load, UI placement, and visual acceptance remain `PENDING VERIFICATION`.

Mandates followed:

- `PROG_Quest_State_Graph_Logic.txt`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `QA_Evidence_Text_Filter_Audit.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Mandatory First Route

`boot -> world load -> damaged safe anchor -> bright semi-open photic exit -> swim -> oxygen/depth pressure -> local unease/avoidable danger -> find reachable starter resource -> collect/use it -> craft/repair/build route improvement -> save/load -> same state restored`

Tone is not "Subnautica but darker." The first route is bright, beautiful shallows with alien biota, industrial traces, route readability, oxygen pressure, salvage function, and evidence before exposition.

Opening canon: player is a debt-bound Marauder / former Deep Reach field-systems specialist arriving by damaged bathy-drop from Black Keel. Motive is contract, salvage, payout, and old procedure recognition. Family-revenge/missing-relative hook is forbidden.

## Static Support

- `FIRST_20_MINUTES_ROUTE_BRIEF.md`: V0 route and copper chain are useful, but copper alone is insufficient.
- `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`: route moments and proof package.
- `CP01_Arrival_Shallow_Water.md`: bathy-drop, bright shelf, heat-shield trail, broken uplink, first reef, Deep Reach marker, Black Keel handshake.
- `Gameable_World_Packets.md`: P01 Broken Bathy-Drop, P02 Aegir Sky Windows, P03 Bright Shallows, P04 Drowned Colony Edge.
- `Canon_Locks.md` / `Lore_Bible.md`: Black Keel, Aegir route pressure, Deep Reach liability language, Atlas damaged repair logic, no-FTL rescue delay.
- Static assets exist for `Quest_CopperSample.asset`, `ResourceNodeTemplate_CopperVein.asset`, `Data_Copper.asset`, `Comp_CopperWire.asset`, and `Recipe_CopperWire.asset`.
- `FirstHourDirector.cs` statically references `quest_copper_sample`, `Data_Copper`, `Comp_CopperWire`, first-hour guidance, save data fields, and lore route contact flags.
- `NarrativeProgressionBridge.cs` statically raises `first_hour_exit_lifepod`.
- AppliedContent packets `RS050`, `RS058`, `RS064`, and `RS090` contain P-63 / bathy-drop / first-hour placement and artifact text seeds.
- `ResourceNodeTemplate_FiberKelpStand.asset` is shallow (`0-140m`), `requiredToolClass: 0`, has a pickup prefab, and yields `Data_FiberKelp`.
- `Recipe_FiberMesh.asset` consumes `Data_FiberKelp` and produces `Comp_FiberMesh`.
- `Recipe_PressureSeal.asset` produces `Comp_PressureSeal` from `Comp_FiberMesh`, membrane tissue, and hydrocarbon resin.
- `FirstHourDirector.cs` statically lists `Comp_PressureSeal` as one first-craft milestone result.
- `ContentSanityValidator.cs` explicitly keeps CopperVein Drill-gated and reports missing starter seafloor drill route as incomplete.

## Blockers

- Scene proof is absent.
- `02_HECTON_WORLD.unity` has massive dirty diff risk; scene-object claims are suspect until Unity owner audits object by object.
- Boot route conflict: root AGENTS flow is `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`, while first-20/topology docs and BuildSettings include `01_ORBIT`. Detailed report: `Docs/Reports/Batch31/SCENE_FLOW_AUTHORITY_DRIFT_20260605.md`.
- Copper route blocker: copper vein static template requires Drill (`requiredToolClass: 2`), while starter drill item/metadata/held prefab/acquisition route are missing. Detailed report: `Docs/Reports/Batch31/COPPER_STARTER_CHAIN_REACHABILITY_20260605.md`.
- `Quest_CopperSample.asset` activation/completion path is static only; runtime proof absent.
- PressureSeal reroute blocker: membrane/resin route placement, powered fabricator access, physical seal target, applied-repair flag, and save/load restored state are not proven.
- Localization has mock/draft/transliterated/English fallback risk. Native-final claim forbidden.
- AppliedContent packets are briefs, not placed scene objects.
- Rejected surface/water/Aegir screenshots cannot be repaired by lore text.

## 3110 Route Decision

Preferred static V0 reroute while Unity is blocked:

`Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal -> apply to a visible P-63/bathy-drop pressure boundary`

Reason:

- FiberKelp is shallow, bright-zone compatible, and physically readable.
- Harvest can happen without unavailable Drill.
- FiberMesh and PressureSeal fit HECTON-8 machinery/pressure logic better than generic loot.
- `Comp_PressureSeal` is statically listed by `FirstHourDirector` as a first-craft milestone result.
- The output can visibly change route safety: reduce leak, open a safe return pocket, stabilize a hatch ring, or make a service buoy/fabricator loop credible.

Rejected:

- Copper-only first route until starter drill route exists.
- Weakening CopperVein to Any/Knife/Salvage.
- Silica -> GlassPanel as preferred 3110 route until a concrete first-hour repair/build target accepts it.

## Required First-20-Minute Objects

- Damaged bathy-drop / safe anchor.
- Bright photic exit with alien life and readable route landmark.
- Heat-shield debris / crash trail.
- Reachable starter resource: copper or lore-consistent replacement.
- Fabricator or repair station that changes route safety/capability.
- P-63 or equivalent pump/repair pocket.
- Shallow service buoy / pinger / relay.
- Damaged instrument / visor warning.
- First Deep Reach lie panel: clean wording contradicted by physical damage.
- First Atlas trace: useful-wrong repair growth, not villain monologue.
- Fair early hazard: oxygen neglect, leak, small predator, electrical short, surge, route distance, or pressure.
- Save/load state objects: inventory, quest, opened/looted/scanned flags, hazard state.

## Object Briefs

### Damaged Bathy-Drop / Safe Anchor

- Gameplay function: spawn safety, oxygen refill, save anchor, first repair target.
- Physical state: heat-scored shell, cracked seal collar, one working internal pump, exposed service hatch.
- Readable visual cue: amber pressure tag on the damaged ring; condensation line around the leak.
- Scanner/PDA line: `ANCHOR SEAL: holding. Outer collar leak increases return risk. Patch before extended swim.`
- Evidence before exposition: Black Keel arrival status says "retrievable"; damaged collar proves retrieval is not immediate.

### Bright Photic Exit

- Gameplay function: first route choice and visual identity proof.
- Physical state: clear shallow shelf, alien FiberKelp stands, wet rock, Aegir/sky cue where visible, drowned industrial cable or buoy line.
- Readable visual cue: safe anchor behind player, pinger line ahead, uneasy silhouette off-route.
- Scanner/PDA line: `PHOTIC SHELF: breathable reserve insufficient for wide sweep. Mark return bearing.`
- Evidence before exposition: beauty is real; route cost is oxygen and return path, not darkness.

### FiberKelp Stand

- Gameplay function: reachable starter resource.
- Physical state: flexible ribbon growth around warm shallow current and industrial debris.
- Readable visual cue: pale woven strands, tool-reticle harvest prompt, cut fibers drifting toward current.
- Scanner/PDA line: `FIBER KELP: usable in mesh and soft seals. Harvest does not require Drill.`
- Evidence before exposition: plant grows on/near damaged hardware, so material use is visible before recipe text.

### Membrane / Resin Secondary Inputs

- Gameplay function: prevents PressureSeal from being a one-item fake craft.
- Physical state: membrane tissue from shallow organic sheet or resin seep on warm rock/old casing.
- Readable visual cue: translucent film, amber bead, shallow safe placement inside the return loop.
- Scanner/PDA line: `SEAL INPUT: bonding material present. Keep sample clean; sand contamination lowers seal rating.`
- Blocker: placement and acquisition proof absent.

### P-63 / Bathy-Drop Pressure Seal Target

- Gameplay function: craft/build improvement after PressureSeal.
- Physical state: hatch ring or pump collar leaks under load.
- Readable visual cue: leaking bubbles, pressure tick, repair decal after application.
- Scanner/PDA line: `PRESSURE SEAL APPLIED: leak reduced. Return pocket remains temporary, not ascent-rated.`
- Save/load state: applied seal flag, leak intensity, oxygen/safe-anchor state, opened access if any.

### Return Cue / Service Buoy

- Gameplay function: swim/return route and oxygen planning.
- Physical state: damaged pinger spool, buoy with weak lamp, cable line to safe anchor.
- Readable visual cue: pulsing low-cost marker visible on compact; richer light/silt on high tiers.
- Scanner/PDA line: `PINGER LINE: signal weak but stable. Follow cable back before reserve warning.`
- Evidence before exposition: return path is a physical object, not only a minimap marker.

### First Deep Reach Lie Panel

- Gameplay function: evidence beat tied to repair route.
- Physical state: clean label claims pressure variance within tolerance; nearby cracked seal and drowned maintenance mark contradict it.
- Readable visual cue: white corporate label beside salt bloom and warped bolts.
- Scanner/PDA line: `PANEL TEXT: "variance nominal." Seal deformation says manual override was ignored.`
- Text limit: short instrument text only; no lore wall.

## 3110 Task Queue

1. Safe anchor: one-page damaged bathy-drop / Shallow Annex P-63 object brief.
2. First exit: bright-shallows identity with biota, waterline/coast/sky/Aegir cue, industrial trace, and one uneasy element.
3. Swim/return: pinger spool, heat-shield trail, service buoy, silhouette landmark, oxygen pocket.
4. Resource: use FiberKelp as preferred static reroute unless drill route is authored and proven.
5. Tool: define first tool/interaction as harvest/repair with actual route verb; do not imply Drill if Drill route is missing.
6. Craft/repair/build: bind PressureSeal to visible machine result.
7. Hazard: specify one fair early hazard with evidence; darkness is not default in 0-100m open water.
8. Evidence before exposition: place first Deep Reach lie plus visible contradiction.
9. Lore route contact: first Black Keel/relay contact is clipped, conditional, and unhelpful as rescue.
10. Save/load: state checklist for route artifacts.

## Save / Quest State Checklist

- Resource harvested flag: FiberKelp stand depleted or partially depleted.
- Inventory state: `Data_FiberKelp`, `Comp_FiberMesh`, `Comp_PressureSeal` as applicable.
- Craft state: FiberMesh craft, PressureSeal craft, consumed inputs.
- Applied repair state: seal target repaired, leak/pressure state changed.
- Route state: opened access, safer return pocket, or stabilized anchor state.
- Hazard state: oxygen/depth warning, avoidable danger state, or leak pressure state.
- Narrative state: first lie panel scanned/read, first Black Keel/relay contact if triggered.

No string runtime comparisons are accepted for these states; use baked IDs/flags and save-compatible state per quest/resource/persistence mandates.

## Low / Middle / High / Ultra

- Low: route readability, oxygen/depth pressure, FiberKelp harvest, one visible seal target, short localized warnings, no text masking weak visuals.
- Middle: stronger object evidence, pinger/service buoy cues, localized scanner/PDA short forms.
- High: richer environmental storytelling, better seal feedback, optional scanner layers.
- Ultra: additional black-box/archive/sensor depth only after route gameplay and visual proof hold.
