# 3108 First-20 Stake / UI Route Owner

Status: STATIC_ROUTE_MATRIX / RUNTIME PROOF PENDING
Date: 2026-06-05
Evidence class: STATIC_DOC, STATIC_SOURCE, SUBAGENT_STATIC_REPORT

## Verdict

The first-20 route must be judged as a player-facing survival operation, not a beauty pass and not a Copper Wire-only proof chain.

Chosen static route target:

`damaged safe anchor -> bright photic exit -> swim/return -> oxygen/depth/pressure -> reachable starter resource -> tool interaction -> FiberMesh/PressureSeal or proven equivalent -> visible repair/craft improvement -> fair hazard -> save/load restored state`

Copper is not currently chosen as the V0 static spine. Copper remains useful if a starter Drill item/metadata/held prefab/acquisition route is authored and Unity-proven. Until then, the preferred static reroute is:

`Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal -> apply to a visible bathy-drop/P-63 pressure boundary`

## Mandates followed

- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`

## Authority integrated

Read: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `quality.md`, `gameplay.md`, `ui.md`, `player.md`, `survival.md`, `tools.md`, `narrative.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`, `taskslocal/batch31_night_visual_recovery/3108_FIRST20_STAKE_UI_ROUTE_OWNER.txt`, `PLAYER_HUD_BOOTSTRAP_BINDING_BLOCKER_20260605.md`, `3110_LORE_WORLD_CONSISTENCY_OWNER.md`, and `COPPER_STARTER_CHAIN_REACHABILITY_20260605.md`.

## Current blockers

- Player/HUD binding: static evidence says the scene-authored tagged `Player` shell can win over production `Player.prefab`. Full movement/HUD/UI acceptance is blocked until Play Mode readback proves the production player and HUD graph are active.
- Copper route: copper node requires Drill, but starter Drill data/prefab/acquisition route is missing.
- Scene flow: root AGENTS says `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`; first-20/topology docs and BuildSettings include `01_ORBIT`. Proof routes can diverge until owner resolves it.
- Visual proof: no accepted h8_1475 proof packet exists. Raw PNGs and diagnostic screenshots are not proof.

## Player-facing route acceptance checklist

### 1. Boot and world load

Acceptance:
- New Game reaches the selected route through the owner-approved scene flow.
- Load Game resumes the same saved first-route state.
- Safe anchor exists before the player is asked to swim.

UI/stake:
- Loading or boot screen may exist, but route acceptance starts only when the player can see a damaged safe anchor and one real exit.
- If `01_ORBIT` remains in the route, proof must state whether orbit is product New Game handoff or optional/prologue bridge.

Rejected:
- Direct world spawn through dev grants.
- Scene-flow proof that ignores the unresolved `01_ORBIT` drift.

### 2. Damaged safe anchor

Acceptance:
- Player starts at damaged bathy-drop, Shallow Annex P-63, or equivalent safe anchor.
- Anchor provides oxygen refill/save context and a visible repair target or pressure boundary.

UI/stake:
- HUD/visor must show oxygen reserve, depth, pressure state, and a return-safe cue.
- First visible failure should be physical: leaking collar, amber pressure tag, cracked hatch ring, pump tick, or condensation line.

Decision:
- Leave now with limited reserve, inspect anchor, or repair/stabilize later.

### 3. Bright semi-open photic exit

Acceptance:
- Exit is bright, beautiful, readable, and alien. It includes water clarity, route landmark, biota, wet terrain/material truth, and technogenic trace.

UI/stake:
- UI can be minimal, but instruments must not vanish if the player is in survival risk.
- A return cue must be visible through world composition: pinger, cable, buoy lamp, crash trail, anchor silhouette, or service line.

Decision:
- Swim out, keep return bearing, or stay within oxygen-safe viewing distance.

Rejected:
- Empty beauty shot.
- Darkness/fog used to hide weak surface/shallow art.

### 4. Swim, oxygen, depth, pressure

Acceptance:
- Player can surface/dive, move through shallow water, read oxygen/depth/pressure, and return to the known anchor.
- Oxygen and pressure are the first active survival pressures.

UI/stake:
- O2, depth, and pressure readouts must show true owner state.
- Warnings must be readable at compact/720p.
- HUD text/update path must be zero-GC when implemented: preallocated char buffers, `TryFormat`, `TMP_Text.SetCharArray`, no `.text =`, no runtime string formatting.

Decision:
- Continue route, abort and return, or spend reserve on resource/hazard interaction.

### 5. Interaction prompt and first tool verb

Acceptance:
- First interaction target is physically readable: FiberKelp strand, seal input, valve, hatch ring, black-box port, salvage grip, or proven copper/drill target.
- Prompt appears only from a valid target owner and disappears when invalid.

UI/stake:
- Prompt must state verb, target, and failure reason when blocked.
- If the target needs a tool, tool capability must be real, not implied by text.

Rejected:
- Pixel-hunt colliders.
- Prompt claimed by filename only.
- Copper target accepted while Drill route is absent.

### 6. Starter resource

Preferred static route:
- `Data_FiberKelp` harvested from shallow `ResourceNodeTemplate_FiberKelpStand`.
- Craft `Comp_FiberMesh`.
- Use `Comp_FiberMesh` plus proven secondary inputs to craft `Comp_PressureSeal`.

Copper route condition:
- Copper may return only after starter Drill route is authored and proven in Unity.

Decision:
- Spend oxygen to harvest now, return with partial material, or risk wider sweep.

### 7. First craft/repair/build result

Acceptance:
- Craft/repair/build consumes real inventory and changes capability or route safety.
- Preferred result: apply `Comp_PressureSeal` to bathy-drop/P-63 pressure boundary, reducing leak intensity, stabilizing return pocket, or opening a service access.

UI/stake:
- Fabricator/repair UI must show required inputs, missing inputs, craft/apply state, and post-apply result.
- UI must not own recipe truth. Inventory/crafting/repair owners supply state.

Rejected:
- Crafting a component with no visible route consequence.
- Repair text without changed leak, access, pressure, or return state.

### 8. Fair early hazard

Allowed static hazard candidates:
- oxygen neglect;
- route distance/return misread;
- leak/pressure state near anchor;
- electrical short;
- small predator/uneasy off-route creature;
- surge/current pushing player off return line.

Rules:
- In 0-100 m open water, darkness is not the default hazard.
- Immediate death is allowed only from fair player error: ignoring oxygen, going too far, or approaching an aggressive creature.

UI/stake:
- Hazard must expose cause and counterplay through instrument, sound, route cue, or physical evidence.

### 9. Evidence/read beat

Acceptance:
- First lore beat is evidence before exposition.
- Use a Deep Reach lie panel or equivalent: clean corporate label contradicted by visible seal deformation, drowned maintenance mark, black-box fragment, or pump failure.

UI/stake:
- Text is short and operational.
- Example acceptable scanner line: `PANEL TEXT: "variance nominal." Seal deformation says manual override was ignored.`

Rejected:
- Lore wall before the player needs it.
- Text as the only source of critical truth.

### 10. Save/load state

Save/load must preserve:
- player position and safe anchor;
- oxygen/depth/pressure-relevant survival state;
- inventory: `Data_FiberKelp`, `Comp_FiberMesh`, `Comp_PressureSeal`, or selected proven route items;
- harvested/depleted resource flags;
- craft state and consumed inputs;
- applied repair state: seal target, leak intensity, opened access, safer pocket;
- interaction/narrative flags: scanned/read lie panel, first contact if triggered;
- hazard state if persistent;
- route cue state: pinger/service buoy/return marker if changed.

No runtime string comparisons are accepted for these states. Use baked IDs/flags and binary save-compatible records.

## h8_1475+ six-view stake matrix

| Canonical file | Required player stake | Required UI / predicate stance | Reject if |
|---|---|---|---|
| `01_surface_coast_aegir_ui_off.png` | Shows surface/coast/Aegir beauty as route context, not empty wallpaper. Must include anchor/return or descent implication. | UI must be off per ProofGate. Route stake comes from composition, landmark, scale, or visible route hardware. | Black/muddy surface, no route implication, Aegir/sky as placeholder, or beauty with no next decision. |
| `02_shoreline_close_1m.png` | Shows material truth at player inspection distance: wet rock/sand/foam/industrial trace or repair boundary. | Must include non-empty `route_anchor_id`. UI optional only if it proves interaction/repair state. | Crayon rock, flat foam sheet, primitive mesh, no anchor, or no physical operation. |
| `03_underwater_0_5m.png` | Shows shallow entry readability, return line, oxygen-safe lookaround, alien biota, and first resource/interaction affordance. | `underwater_active=true`, depth 0.25-5 m. HUD may be visible only if production binding is proven. | Generic blue/green water, no route cue, no resource/target, or UI claimed without active owner proof. |
| `04_underwater_20_50m_route.png` | Shows route pressure: depth, return cost, pinger/service cue, salvage/machinery/evidence target, and fair hazard/unease. | `underwater_active=true`, depth 20-50 m. O2/depth/pressure UI should be visible for gameplay proof after HUD blocker clears. | Empty murk, no player decision, darkness used as cover, no return readability. |
| `05_aegir_celestial_long.png` | Shows Aegir/sky as product identity and navigation/timing context, not a decorative sticker. | Must include route anchor id. UI normally off unless an instrument/cockpit view is deliberately being proven. | Muddy/translucent Aegir, missing cloud/detail, no route meaning, or sky used to hide weak terrain. |
| `06_regression_low_oblique.png` | Catches wide route regressions: water, terrain, sky, route cue, anchor, UI if enabled, and object density. | Must include route anchor id. If UI visible, manifest must record UI predicate and source owner. | Looks good only from one angle, hides product-face blockers, or lacks survival/route stake. |

## Implementation order for agents

1. Resolve scene-flow authority.
Output: owner decision for direct world route, orbit route, or dual route proof cards.

2. Resolve player/HUD binding.
Output: Play Mode readback proving production `Player.prefab`, production movement, interaction, PDA/tools, visor/HUD, and active interaction UI route, or safe Unity-owner replacement of shell route.

3. Choose starter resource spine.
Output: either author/prove starter Drill route for copper, or formally switch V0 route to FiberKelp/FiberMesh/PressureSeal.

4. Place route objects in Unity through owners.
Required objects: damaged safe anchor, bright exit, return cue, starter resource, first repair target, fair hazard, Deep Reach lie/evidence object, save/load state objects.

5. Implement UI acceptance route.
Output: oxygen/depth/pressure, interaction prompt, return cue, craft/repair state, save/load state readouts from named source owners. UI writes in `VISUAL_SYNC`; no hot registry polling; zero-GC text path.

6. Implement craft/repair route.
Output: resource acquisition, inventory state, recipe/craft state, applied repair state, visible leak/access/return change.

7. Implement hazard and death/recovery proof.
Output: fair oxygen/depth/pressure or creature/current/electrical hazard, death or recovery evidence, black-box/survival state note where applicable.

8. Implement save/load proof.
Output: save, quit/reload, position/inventory/quest/repair/hazard/route state restored with directory diff.

9. Produce h8_1475+ proof packet.
Output: canonical six screenshots, manifest, checksum, copied clean Unity log, route/depth/UI predicates, strict ProofGate pass.

10. Run runtime proof.
Output: Unity Console, Play Mode/player capture, profiler/GC/memory, screenshot/clip, and exact unresolved failures.

## UI source owner map

| UI element | Truth owner | Acceptance |
|---|---|---|
| Oxygen reserve | Survival owner | Visible reserve, warning threshold, compact-readable, no string allocation. |
| Depth / pressure | Survival/depth owner | Depth and pressure envelope shown; pressure cannot be hidden in UI only. |
| Interaction prompt | Interaction/tool owner | Valid target, verb, required tool/cost, failure reason. |
| Return cue | World/route/navigation owner | Physical cue visible in world; UI marker may assist but cannot be sole route truth. |
| Craft/repair state | Inventory/crafting/repair owners | Inputs, missing inputs, consumed state, applied result. |
| Save/load state | Persistence/quest/inventory/survival owners | State restored by binary save route; no JSON/string-runtime claims. |
| Evidence/log read | Narrative/quest owner | Short operational text after visible evidence; baked IDs/flags. |

## Required proof views must sharpen these decisions

- Can I leave the anchor and still return?
- How much oxygen/pressure budget remains?
- What physical object is the next route cue?
- What can I harvest or repair now?
- What tool or material is missing?
- What hazard can kill me if I ignore it?
- What changed after crafting/repair?
- What persisted after save/load?

## Low / Middle / High / Ultra consequences

- Low: keep route cue, oxygen/depth/pressure, interaction prompt, repair result, and readable silhouettes. Reduced motion and atlas detail are allowed; ugly water/sky/terrain is not.
- Middle: add better pinger/service-buoy feedback, material response, shallow biota density, and scanner/PDA short labels.
- High: add richer seal/leak feedback, visor degradation, particle/silt density, stronger environmental evidence, and longer LOD residency.
- Ultra: add secondary black-box reconstruction, richer instrument glass, denser near-field evidence, and sensory overkill without changing gameplay truth, resource math, or save identity.

## Regression model

- CPU: this report changes no runtime code. Future UI route must keep text flushing in `VISUAL_SYNC` and bounded cadence.
- GC: no runtime claim. Future HUD/interaction/craft UI must prove 0 B/frame hot paths.
- Memory/VRAM: no assets changed. Future diegetic panels must use pooled RTs and continuous quality scaling.
- Cadence: survival/tool/quest/save truth remains owner-driven; UI consumes stable snapshots.
- Correctness: blocker risk is false acceptance from static docs. Runtime proof remains mandatory.

## Verification state

Verified:
- Authority docs and task reports read.
- Copper route blocker integrated.
- Player/HUD shell blocker integrated.
- Scene-flow authority drift integrated.
- Six canonical ProofGate view filenames integrated.
- Static route/UI acceptance checklist and implementation order written.

Pending:
- Unity scene proof.
- Production player/HUD binding proof.
- Visual acceptance.
- h8_1475+ proof packet.
- Play Mode/player capture.
- Profiler/GC/memory proof.
- Save/load restoration proof.
