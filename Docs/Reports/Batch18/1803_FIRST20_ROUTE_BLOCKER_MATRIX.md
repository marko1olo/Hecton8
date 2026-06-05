# Agent 1803 - First 20 Gameplay Route Blocker Matrix

Date: 2026-06-04 04:18 +04:00  
Agent: 1803 / FIRST20_GAMEPLAY_ROUTE_BLOCKER_AUDITOR  
Scope: boot -> world load -> safe anchor -> bright shallow exit -> swim -> oxygen/depth pressure -> salvage/tool/resource -> craft/repair/build -> hazard response -> save/load -> same-state return.

## Proof Boundary

This is a static audit. No Unity editor route run, Play Mode capture, profiler pass, player build, or runtime screenshot was produced by Agent 1803.

Unity slot state: active Unity processes were present during the audit. No dotnet, csc, or MSBuild process appeared in the process check output. Per task instruction, this agent did not fight the current Unity verification slot.

Proof labels used:

- STATIC VERIFIED: source/data/scene YAML or existing report evidence inspected.
- PENDING PLAYMODE: route must be executed in Unity before acceptance.
- PENDING PLAYER-CAPTURE: first-person visual proof required.
- PENDING PROFILER: frame time, GC, memory, and GPU/VRAM proof required.
- BLOCKED STATIC: source/data evidence exposes a route blocker before runtime.

## Authority Loaded

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- Route bibles: `gameplay.md`, `survival.md`, `player.md`, `tools.md`, `inventory.md`, `construction.md`, `sonar.md`, `ui.md`, `world.md`, `water.md`, `creatures.md`
- First-20 contract: `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- Route brief: `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`
- Recent adjacent evidence: `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`, `Docs/Tasks/Status_1801.md`, `Docs/Tasks/Status_1802.md`
- Domain file check: `Docs/Actual Domains of Project.txt` was absent; narrow first-20 gameplay route domain inferred from route contract and bibles.

Selected mandates:

- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `UI_Diegetic_Physical_Interfaces.txt`
- `AI_Director_Encounter_Manager.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Static Evidence

- Boot target exists: `GameBootstrapper.cs:118` sets `DefaultGameplaySceneName = "02_HECTON_WORLD"`.
- Menu target exists: `MainMenuController.cs:72-73` and `01_MAIN_MENU.unity:418-419` point New Game and Load Game at `02_HECTON_WORLD`.
- First-hour route director exists: `FirstHourDirector.cs:714` uses `quest_copper_sample`; `FirstHourDirector.cs:717` uses `Data_Copper`; `FirstHourDirector.cs:1472` and `FirstHourDirector.cs:1480` implement save/load hooks.
- First-hour fallback inventory scan exists: `FirstHourDirector.cs:1660` scans runtime inventory for first resource completion.
- Oxygen death route exists statically: `HectonSurvivalSystem.cs:2376` checks lethal conditions; `HectonSurvivalSystem.cs:3708` resolves death cause.
- Pressure route is split and unproven: `HectonSurvivalSystem.cs:1083` leaves pressure damage as a no-op owner handoff; `HectonPlayerMovement.cs:540` starts crush at 1000m and `HectonPlayerMovement.cs:10550` triggers implosion presentation.
- Copper Wire recipe exists: `Recipe_CopperWire.asset:13`, `:15`, `:26`, `:28`, `:31`, `:32`.
- Copper quest exists: `Quest_CopperSample.asset:13`, `:15`, completion item visible in inspected YAML as `completionId: Data_Copper`.
- Copper resource node template requires a tool: `ResourceNodeTemplate_CopperVein.asset:19` has `requiredToolClass: 2`.
- Product starter tool proof is absent: `ToolLoadoutProvisioner.cs:3` identifies itself as a development helper; `ToolLoadoutProvisioner.cs:36-37` disables startup provisioning by default.
- Copper identity collision exists: both `Assets/_Project/Data/Items/Data_Copper.asset:16` and `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset:16` use `stableId: Data_Copper`; they disagree on `isRawResource` and `worldPrefab`.
- Route objects in `02_HECTON_WORLD.unity` are sockets, not live gameplay proof: `Node_Copper_A` uses `futurePrefabKey: resource.node.copper`; `Forward_Fabricator` uses `futurePrefabKey: station.fabricator.forward`; `Route_Anchor` uses `futurePrefabKey: nav.route.anchor`.
- Fabricator gates exist: `Fabricator.cs:713`, `:831`, `:1203`, `:2477`, `:3312`, and `:1335` show craft validation, start, completion, ingredient consumption, and craft-completed signaling.
- Encounter Director black box exists: `EncounterDirector.cs:276`, `:280`, `:294`, `:1088`, `:1138`.
- First-route strong predator proof is not present: `EncounterDirector.cs:1451-1462` gates Stalker/Leviathan by deeper biome/depth or `positionY <= -60f`.

## Blocker Matrix

| Beat | Static evidence | Blocking fact | Acceptance state | Proof needed |
|---|---|---|---|---|
| 01 Boot to menu/world | Bootstrap/menu targets point to `02_HECTON_WORLD`. | Static target is not handoff proof. | STATIC VERIFIED / PENDING PLAYMODE | Play from boot/menu into world with zero blocking errors. |
| 02 World load | Scene `02_HECTON_WORLD.unity` exists and contains route sockets. | Sockets do not prove populated gameplay objects. | PARTIAL / PENDING PLAYMODE | Runtime object/component list after population. |
| 03 Bright shallow exit spectacle | 1801 screenshots exist. | 1801 marked water/coast richness weak and runtime proof pending. Visual floor not cleared. | PARTIAL / PENDING PLAYER-CAPTURE | Player-view capture with sky, Aegir, moons/coast, ocean surface, photic shallows. |
| 04 Safe anchor | `Route_Anchor` socket exists. | `futurePrefabKey` is not a live anchor, save point, or respawn proof. | BLOCKED STATIC / PENDING PLAYMODE | Live safe anchor object, marker, and return/respawn behavior. |
| 05 Swim/control | Player movement systems exist in codebase. | No route capture proves first swim readability, controller feel, or no stuck states. | PARTIAL / PENDING PLAYMODE | First-person swim from exit to resource and back. |
| 06 Oxygen risk | Survival lethal check and death cause route exist. | Oxygen depletion and UI warning not route-tested. | PARTIAL / PENDING PLAYMODE | Controlled O2 drain/recover/death proof and HUD/diegetic feedback. |
| 07 Depth pressure | Survival pressure damage is no-op; movement crush starts at 1000m. | First-20 pressure consequence is not proven and likely mismatched to shallow route depth. | BLOCKED STATIC | SHINOBU pressure owner proof or first-depth survivable pressure implementation. |
| 08 Calm view time | FirstHourDirector timer milestones exist. | Timers do not prove premium calm view or player-readable route space. | PENDING PLAYER-CAPTURE | 60-120s first-route capture without darkness hiding weak art. |
| 09 First fair hazard | Encounter/Ambient systems exist with black boxes. | First-route shallow hazard is not proven; strong predator gates favor deeper zones. | BLOCKED STATIC / PENDING PLAYMODE | Fair warning, avoid/escape path, no cheap darkness, no unavoidable spawn. |
| 10 Copper discovery | Copper socket and copper template exist. | Socket population, marker visibility, and route readability unproven. | BLOCKED STATIC / PENDING PLAYMODE | Copper node visible/discoverable in first route. |
| 11 Starter tool interaction | Tool/raycast/harvest systems exist. | Required copper tool class is `2`; product-owned starter tool authority absent. Dev provisioner is disabled. | BLOCKED STATIC | Product route loadout grants correct tool or copper V0 uses an approved starter interaction. |
| 12 Copper acquisition quest | Quest and FirstHourDirector use `Data_Copper`; inventory scan mitigation exists. | `Data_Copper` stable ID collision risks wrong item semantics; pickup event path is mixed. | BLOCKED STATIC / PENDING PLAYMODE | Actual pickup advances quest with correct raw item GUID and quantity. |
| 13 Inventory/storage | Player inventory and SOA query systems exist. | First-route storage/add/drop/save semantics untested. | PARTIAL / PENDING PLAYMODE | Inventory assert after pickup, overflow case, save/load roundtrip. |
| 14 Craft/repair/build route improvement | Copper Wire recipe and Fabricator gates exist. | Forward fabricator is a socket; `HandleCraftCompleted` accepts any craft, not a route-relevant craft. | BLOCKED STATIC / PENDING PLAYMODE | Craft Copper Wire or approved item, then prove route improvement. |
| 15 Sonar/map return aid | Sonar synthesizer, PDA map, compass systems exist. | No proof first-route anchor/resource/hazard markers are registered and useful. | PARTIAL / PENDING PLAYMODE | Sonar/map capture for copper, safe anchor, hazard, return bearing. |
| 16 Return path | Route sockets `Route_Anchor` and `Route_Frontier` exist. | No live landmark/marker/readability proof. | PARTIAL / PENDING PLAYER-CAPTURE | Player returns without debug knowledge using world cues and instruments. |
| 17 Save | Survival/FirstHour/Inventory save hooks exist statically. | No route save file diff or reload assert. | PARTIAL / PENDING PLAYMODE | Save after resource/craft/hazard and inspect serialized route state. |
| 18 Load same state | Load hooks exist statically. | Same position/inventory/quest/fabricator/anchor state not proven. | PARTIAL / PENDING PLAYMODE | Load and compare route state hashes/values. |
| 19 Death/respawn | Survival queues respawn; respawn runtime exists. | Respawn defaults include fallback/mock routes; real safe anchor/base proof missing. | PARTIAL / PENDING PLAYMODE | O2 or hazard death returns to valid authored safe route state. |
| 20 Frame/GC/memory | Quality scaling appears in sonar/encounter/respawn. | No profiler, GC allocation, GPU/VRAM, or player build proof. | PENDING PROFILER | 60s route profiler and player capture packet. |

## Hard Blockers

1. Route sockets are being mistaken for route content. `WorldContentSocket` plus `futurePrefabKey` is planning data, not proof that copper, fabricator, anchor, hazard, or return markers exist as live route objects.

2. Starter tool truth is missing. Copper Vein requires `requiredToolClass: 2`, while the visible provisioning path is a disabled development helper. The first 20 cannot depend on editor/dev loadout charity.

3. `Data_Copper` identity is contaminated. The raw copper resource and legacy root copper asset share the same stable ID while disagreeing on raw-resource flags and world prefab. This can break quest, crafting, pickup, catalog, and save identity.

4. Pressure gameplay is not accepted. Survival pressure damage is an owner handoff/no-op, and player movement fatal pressure starts at 1000m+ as a wipeout presentation path. That does not prove first-20 depth pressure.

5. Craft completion is too broad. The first-hour director can mark FirstCraft from any completed craft. Acceptance requires route-relevant Copper Wire, repair, or build impact, not any craft event.

6. The first hazard is not staged. Encounter systems are present, but the route needs a fair, readable, shallow first danger. Strong predator gates do not establish that.

7. Save/load is not proven. Saveable hooks exist, but the route acceptance requires same-state return after resource, quest, craft/repair/build, hazard, and position changes.

8. Visual spectacle is still pending. Recent static screenshots and 1801 report do not clear the Subnautica-level surface/shallow-water floor. Darkness cannot hide surface, water, or coastline weakness.

## Static Evidence Worth Keeping

- Boot and main menu route constants are aligned on `02_HECTON_WORLD`.
- FirstHourDirector already owns a quest/milestone spine and saves first-hour state.
- FirstHourDirector has a runtime inventory scan mitigation, which can recover from mixed item acquisition events if catalog identity is correct.
- Fabricator has real craft gates and event signaling.
- Oxygen death and respawn request routes exist statically.
- Sonar/map/compass infrastructure exists and already uses continuous quality scaling.
- Encounter Director has a 300-frame black box and quality-aware pacing hooks.

## Independent Work Packets

These packets are independent and should use GlobalRegistry/SignalBus seams instead of inventing direct dependencies.

1. Route socket population verifier/fixer: prove or fix population for `Node_Copper_A`, `Forward_Fabricator`, `Route_Anchor`, first hazard, and return markers. Output runtime object/component list and player-view screenshots.

2. Starter tool authority: add or prove a product-owned starter loadout/interaction route that satisfies copper extraction. Do not use `ToolLoadoutProvisioner` as acceptance proof.

3. Copper catalog cleanup: ensure the raw `Data_Copper` resource is the only route-resolvable copper for quest/craft/pickup/save. Migrate or quarantine the legacy root asset after scoped proof.

4. First-depth pressure route: prove SHINOBU pressure collapse authority or implement a first-20 survivable pressure warning/consequence. Do not use 1000m implosion as shallow-route proof.

5. Route-relevant craft/repair/build: bind Copper Wire or an approved item to a real improvement, repair, or build state. Update FirstHourDirector to check the route-relevant item/event, not any craft.

6. Fair first hazard staging: author a shallow warning/danger beat with readable avoid/escape logic, black-box telemetry, and no darkness cover for weak art.

7. Sonar/map/return aid: register first-route copper, safe anchor, hazard, and return bearing with PDA/Sonar/Compass. Keep it non-omniscient but useful.

8. Save/load route harness: automate boot -> world -> exit -> collect copper -> craft/repair/build -> hazard response -> save -> load -> assert same state. Include console, save diff, and state hashes.

9. Visual route capture pass: collect player-view screenshots/video for surface, sky/Aegir/moons, coastline, waterline, photic shallows, resource pocket, hazard, and return route.

10. Profiler packet: capture 60 seconds of first-route CPU, GC alloc, memory, GPU/VRAM where available, and frame-time spikes. Any system above 0.1 ms is suspicious and needs evidence.

## Unity Slot Packet

Run only when Unity verification slot is free and CPU/build state allows it.

Minimum route execution:

1. Launch from `00_BOOTSTRAP` through `01_MAIN_MENU` into `02_HECTON_WORLD`.
2. Exit safe anchor into bright shallow water.
3. Swim to starter resource pocket.
4. Trigger oxygen and depth-pressure feedback without killing the player unless death test is scoped.
5. Locate copper with world cues and optional sonar/map aid.
6. Use the approved starter tool/interaction to acquire raw copper.
7. Confirm `quest_copper_sample` advances from actual inventory state.
8. Craft Copper Wire or approved route item at the forward fabricator or equivalent station.
9. Prove the craft/repair/build changes route state.
10. Trigger first fair hazard, respond, and recover.
11. Return to anchor using world/sonar/map cues.
12. Save.
13. Load.
14. Assert same inventory, quest flags, craft/build state, position/anchor, survival state, and no blocking console errors.
15. Run death/respawn test from oxygen or approved hazard and prove return to authored safe state.

Required artifacts:

- Console log with errors/warnings separated.
- Player-view screenshots: menu handoff, first exit, surface/waterline, 5m photic shallows, 30m or first pressure depth, copper discovery, interaction/pickup, quest advance, fabricator/craft, route improvement, sonar/map, hazard warning/escape, save/load return, death/respawn.
- Profiler summary: CPU frame time, GC alloc, memory, GPU/VRAM if available.
- Save diff or state dump before save and after load.
- Black-box dump only on crash/NaN/fault; otherwise note buffer owner presence.

## Do Not Do

- Do not declare `WorldContentSocket` or `futurePrefabKey` values as live gameplay proof.
- Do not use a disabled development provisioner as starter loadout acceptance.
- Do not accept Copper Wire alone if it does not repair, build, unlock, or materially improve the route.
- Do not hide weak surface/water/coastline with darkness, storm, blur, or fog.
- Do not introduce binary quality switches. All route clarity and fidelity scaling must consume continuous `GlobalQualityWeight`.
- Do not add managed hot polling through GlobalRegistry. Runtime signals use SignalBus/native queues or immutable snapshots.
- Do not run dotnet/Unity verification while another build/editor verification owns the slot.

## Compact/Middle/High/Ultra Consequences

- Compact: fewer sonar rays, lower particle density, cheaper ambient schools, reduced marker cadence. Copper, safe anchor, hazard warning, oxygen/pressure feedback, and return cues must remain readable.
- Middle: normal first-route waterline detail, resource markers, fair hazard cadence, and fabricator route feedback.
- High: richer water/foam, denser photic biota, stronger acoustic/sonar fidelity, improved route landmark legibility. Gameplay truth remains unchanged.
- Ultra: visual overkill only - stronger caustics, waterline richness, biolum response, distant Aegir/moon/coastline treatment, and higher temporal fidelity. No DTO/save identity/authority route changes.

## Final Classification

The first 20 minutes are not acceptance-ready.

The static architecture is not empty: boot target, first-hour director, survival death route, inventory, fabricator, sonar/map, encounter director, and black-box patterns exist. The blocker is proof and seams: route sockets are not live content, starter tool authority is missing, copper identity is contaminated, first-depth pressure is not proven, craft completion is too broad, the first hazard is not staged, save/load is unproven, and the visual surface/shallow route has not cleared player-capture/profiler acceptance.

Current state: STATIC VERIFIED blocker matrix complete. Unity/editor/play/profiler/player-capture proof remains PENDING VERIFICATION.
