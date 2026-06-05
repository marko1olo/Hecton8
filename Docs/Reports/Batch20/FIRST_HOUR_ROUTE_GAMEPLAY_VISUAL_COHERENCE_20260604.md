# First-Hour Route Gameplay/Visual Coherence Audit

Date: 2026-06-04
Workspace: `C:\hades\Hecton8`
Evidence class: STATIC DOC + STATIC SOURCE INSPECTION ONLY
Unity/build status: NOT RUN. NO UNITY. NO BUILD. NO ASSETS EDITED.

## Authority Read

Root authority:
- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`

Domain bibles:
- `gameplay.md`
- `survival.md`
- `water.md`
- `world.md`
- `sonar.md`
- `inventory.md`
- `combat.md`
- `creatures.md`

Route contracts:
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`

Registry mandates loaded:
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt`
- `.agents-skills/AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/AI_Director_Encounter_Manager.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

`Docs/Actual Domains of Project.txt` was not present. Domain inferred narrowly from the task: first-hour surface/photic/medium-depth route coherence across gameplay, survival, water/world presentation, sonar, inventory, combat, and creatures.

## Verdict

The product direction is coherent on paper: first-hour route must be semi-open, visually spectacular, bright/readable at surface and 0-100 m, oxygen-driven, danger-capable, salvage/craft/build-capable, and capable of a medium-depth twilight escalation without making darkness a cheap mask.

The current evidence does not prove the route as gameplay. It proves only static intent and some source/data anchors. Runtime acceptance remains blocked until Unity captures prove:
- player can actually start from the selected safe anchor;
- first exit is bright, beautiful, readable, and not a dark fog cover;
- oxygen/depth pressure creates a fair route clock;
- the player can pause to look at beauty in a safe/local rest window;
- danger is fair, avoidable, and capable of killing by player error;
- starter resource/tool/craft loop is reachable without hidden grants;
- death drops resources, respawns at base/safe anchor, and retains core tools;
- save/load preserves route state, inventory, quest state, crafted result, and hazard state;
- medium-depth 200-400 m escalation is twilight/gloomy but still navigable and instrument-readable.

Highest static blocker: `ResourceNodeTemplate_CopperVein.asset` requires `requiredToolClass: 2` and has `minimumDepthMeters: 40`. If `2` is the drill class from `ResourceNodeTemplate.HarvestToolClass`, copper is not a free shallow pickup. Unity owner must prove the starter tool route grants/uses the required drill interaction before copper becomes the first-hour spine. Otherwise use a different starter node or an authored `Any` starter resource for the first proof loop.

## Runtime Proof Needed Next

Screenshots alone are insufficient. Required next Unity proof is a playable route packet:

1. Full run path:
   `boot -> main menu/new game -> world load -> damaged safe anchor -> first exit -> photic swim -> resource/tool interaction -> craft/repair/build -> fair hazard/death response -> save -> load -> return to same state`.

2. Capture set:
   - Console after import/load.
   - Play Mode or player run clip through the route.
   - Surface/photic screenshot from gameplay camera.
   - Compact-equivalent and high/normal visual capture.
   - 60 second profiler capture on the selected route.
   - GC allocation evidence for route-critical hot paths.
   - memory/VRAM snapshot after route load and after save/load.
   - save directory diff before/after save/load.
   - death/respawn/drop/tool-retention proof.

3. Gameplay proof:
   - oxygen decreases while submerged, refills at valid air/oxygen source, and kills on neglect;
   - pressure/depth warnings exist where the route crosses authored safe depth or medium-depth edge;
   - inventory receives `Data_Copper` or chosen starter resource through actual interaction;
   - quest `quest_copper_sample` activates from `first_hour_exit_lifepod` and completes on `Data_Copper`, or the route need is changed and proven;
   - reachable powered fabricator/repair/build action consumes material and changes capability or route safety;
   - large threat/danger is evasion/distraction/route-choice pressure, not DPS target;
   - small threat can be fought only with cost, readable hit feedback, and damage route.

4. Visual proof:
   - surface, sky, Aegir/moons where visible, coastline, ocean skin, waterline, photic terrain, and medium-depth hero path meet or exceed Subnautica-level readability;
   - 0-100 m open water remains bright/readable;
   - 200-400 m is subdued/twilight, not blind black;
   - true darkness is reserved for 400-500 m+, caves, storms, interiors, or events;
   - compact lane preserves composition, silhouettes, route cues, material identity, and instrument readability.

## Oxygen, Danger, Rest, Beauty Pacing Gates

The first hour needs alternating pressure and permission. Constant panic makes the world unlovable. Constant scenic drift makes the survival loop fake.

| Gate | Route Purpose | Required Unity Proof |
|---|---|---|
| Safe anchor orientation | Player starts in a damaged but usable base/safe room. | Player spawn has oxygen safety, route exit, readable instruments, no hidden dev grants. |
| First beauty look | The world sells itself before punishment. | At first exit, player has a local low-threat window to look around without immediate hostile contact. Oxygen still runs if submerged. |
| First oxygen decision | Oxygen becomes route planning, not UI decoration. | Player can leave, inspect/collect, and return if they respect warnings; neglect kills. |
| First unease | Threat is present before full reveal. | Audio/sonar/silhouette/environment cue appears before close danger. |
| First danger | Player error can kill, but route is fair. | Avoidable aggressive contact, oxygen neglect, route distance, or pressure creates death/recovery proof. |
| First resource | Salvage has physical location and extraction cost. | Starter resource appears in route, not console grant; pickup/harvest routes through inventory event or equivalent. |
| First craft/repair/build | Resource changes capability or safety. | Fabricator/repair/build consumes resource and changes state the player can inspect. |
| First rest return | Safe room/base is cozy enough to care about, industrial enough to believe. | Return area shows pressure infrastructure plus a warm readable refuge. |
| Medium-depth invitation | First hour points below the bright shallows. | 200-400 m route hint or optional excursion uses twilight, sonar, oxygen extension, and large-threat avoidance. |

## Route Sequence And Proof Per Segment

| Segment | Depth/Light | Gameplay Job | Visual Job | Proof Gate |
|---|---|---|---|---|
| 0. Boot and damaged safe anchor | Interior/safe | Start from real route state. | Industrial cozy pressure shelter, not sterile tutorial box. | Spawn, oxygen safe state, HUD, first route marker, no dev-only grants. |
| 1. Surface or surface-adjacent exit | Surface/0-20 m bright | Establish return anchor and player orientation. | Ocean skin, wet rock/coast, sky/Aegir/moons where visible, foam/specular/waterline. | Gameplay screenshot and clip from player camera, compact/high capture. |
| 2. Photic shallows scenic rest | 0-100 m bright/colorful | Let player look, breathe route grammar, identify landmarks. | Alien biota, technogenic traces, readable terrain through water. | Low-threat window, route landmark visible, no black fog. |
| 3. First oxygen route | 10-80 m bright | O2 clock and return planning. | Depth falloff, caustic hints, clear silhouettes. | O2 drain/refill/death warning proof; UI remains legible. |
| 4. Starter salvage/resource | 40-100 m if copper | Collect/harvest real item. | Resource node readable without sparkle; extraction scar/depletion cue. | `Data_Copper` or chosen resource enters grid inventory through interaction. |
| 5. First danger | Photic or edge route | Fair avoid/fight/retreat decision. | Threat cue via sound, partial silhouette, route disturbance. | Death possible by error; no unavoidable cheap scare; black-box/death record evidence. |
| 6. Return and craft/repair/build | Safe anchor/base | Convert salvage into capability/safety. | Fabricator/repair/build has physical operation and industrial comfort. | Recipe/action consumes item, creates `Comp_CopperWire` or route safety state, persists. |
| 7. Medium-depth bridge | 100-200 m transition | Teach descent cost and sonar usefulness. | Light fades, terrain landmarks survive, instruments matter. | Player can navigate out using landmarks/sonar/oxygen planning. |
| 8. Medium-depth hero route | 200-400 m twilight | Optional/first-hour escalation: bigger threat/drones/wreck/relay. | Gloomier, structured, premium, not true dark. | Route silhouette, sonar confidence/staleness, large-threat evasion, return path proof. |
| 9. Save/load and recovery | Any selected route state | Preserve consequences. | Restored state visually matches route truth. | Save/load diff plus restored position/inventory/quest/hazard/crafted/depleted states. |

## Current Static Systems Likely Responsible

| System/File | Static Fact | Missing Proof |
|---|---|---|
| `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md` | Selects spectacular semi-open shallow V0. | Real selected scene, clip, profiler, save/load roundtrip. |
| `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` | Defines product gate and proof package. | No runtime evidence attached here. |
| `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs` | Has route quest ids: `quest_copper_sample`, `Data_Copper`, `Comp_CopperWire`; save priority/load priority 13; quest sync logic. | Scene trigger `first_hour_exit_lifepod`, real route activation, runtime inventory completion, guidance validity. |
| `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset` | `questId: quest_copper_sample`, trigger `first_hour_exit_lifepod`, completion `Data_Copper`. | Trigger observed in scene and completion observed through real pickup. |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset` | Copper node exists, depth 40-420 m, requires tool class 2, yields `Data_Copper`, loot count 2. | Starter tool reachability, authored placement in first route, node visibility, harvest success, depletion persistence. |
| `Assets/_Project/Scripts/ResourceNode.cs` | Pooled node, template-driven, inventory service route, AUP/persistence tombstone, interaction signal consumer. | Real player interaction path, no hidden grants, inventory capacity, visual depletion/scar. |
| `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset` | `Copper Wire`, result `Comp_CopperWire`, ingredient one `Data_Copper`, craft time 1.5, power cost 5, no scan gate. | Reachable powered fabricator, recipe visible/unlocked, craft consumes input and outputs result. |
| `Assets/_Project/Scripts/Fabricator.cs` | Fabricator consumes ingredients, tracks craft state, power, progress, events. | Route object exists, has power, UI/actuator works, result persists after save/load. |
| `Assets/_Project/Scripts/HectonSurvivalSystem.cs` | Owns oxygen, pressure, oxygen grace, death cause, death record, save fields, pressure damage. | Timed O2 route, oxygen death, respawn reconciliation, 300-frame black-box proof, HUD readability, profiler/GC. |
| `Assets/_Project/Scripts/PlayerInventory.cs` and `InventoryGrid.cs` | Grid-limited inventory, save priority/load priority 20, death drop penalty routes, tool retention logic hints. | Death resource drop like Minecraft/Subnautica, base respawn, equipped/core tools retained, save/load preservation. |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs` | Stores player position, quest section, checksums, backup/temp paths. | Full route save/load diff and restored runtime state. |
| `Assets/_Project/Scripts/HectonDirectorAI.cs` | Encounter director reads survival stress, sonar stress, predator pressure; has bounded event lanes. | First-hour fair danger, no early helper drones, neutral/hostile source drones deeper, large-threat evasion proof. |
| `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`, `Visor/SonarGridOverlay.cs`, sonar shaders | Sonar/scanner presentation systems exist. | Useful but non-omniscient first-hour navigation, active ping cost/reaction, stale/confidence presentation. |
| `water.md`, `world.md`, `TASTE.md`, `VISION_LOCKS.md` | Surface/photic brightness and Subnautica-level floor are locked. | Actual route screenshots/captures showing water, sky, Aegir/moons, coastline, terrain, biota, medium-depth twilight. |

## Blockers

1. Copper reachability is not proven. Static data says Copper Vein requires tool class 2 and starts at 40 m depth. If starter tool and oxygen capacity do not support that, copper cannot be the first route proof.

2. Spectacle is not proven. The docs demand bright, beautiful surface/photic visuals, but no current Unity capture is part of this audit.

3. Death policy is not proven. Static source shows death records and inventory drop machinery, but no run proves resource drop, base respawn, and tool retention together.

4. Safe scenic rest is not proven. The route must allow the player to look at beauty without instant hostile pressure. No runtime pacing capture exists.

5. Medium-depth transition is not proven. The first-hour route needs a 200-400 m twilight bridge or optional excursion with oxygen/sonar/danger proof, not a screenshot-only mood board.

6. Save/load route state is not proven. Static storage has checksum/quest/position support, but no route diff proves inventory, quest, crafted result, resource depletion, and hazard state survive.

## Slot-Safe Tasks

Unity owner tasks:
- `U20_ROUTE_01`: Run the exact first-hour route from new game through save/load. Produce Console, clip, screenshots, profiler, GC, memory/VRAM, save diff.
- `U20_VIS_01`: Capture surface/photic/medium-depth route at compact-equivalent and high/normal settings. Reject darkness as surface cover.
- `U20_OXYGEN_01`: Timed oxygen loop: leave, look, collect/harvest, return, then intentionally neglect oxygen and prove death/recovery.
- `U20_COPPER_01`: Prove Copper Vein reachability with required tool class and 40 m minimum depth, or mark copper as blocked and select a reachable starter resource.
- `U20_CRAFT_01`: Prove reachable powered fabricator/repair/build action consumes starter material and changes capability/safety.
- `U20_DEATH_01`: Prove resource drop, base/safe-anchor respawn, and core tool retention.
- `U20_DANGER_01`: Prove one fair avoidable danger using sound/sensor/silhouette before contact.
- `U20_SAVE_01`: Save/load after resource depletion, quest completion, craft/build, and hazard interaction. Verify restored state.

Non-Unity agent tasks:
- `N20_ROUTE_MATRIX`: Convert the route sequence above into a one-page owner/proof matrix with no new code dependencies.
- `N20_COPPER_DECISION`: Audit starter resource choices from data files only. Decide whether copper remains first-hour spine or becomes second-step after tool proof.
- `N20_VIS_ASSET_MANIFEST`: List route-visible water/sky/Aegir/moon/terrain/biota/industrial assets that must appear in captures. Do not edit Assets.
- `N20_SONAR_UX_PACKET`: Define first-hour sonar/hydrophone confidence, staleness, cost, and creature reaction acceptance text.
- `N20_CREATURE_ENCOUNTER_PACKET`: Specify small-threat fight gate and large-threat evasion gate with sensory causes and no generic hunting loop.
- `N20_DEATH_POLICY_PACKET`: Document death drop/tool-retention/base respawn acceptance against `PlayerInventory` and `HectonSurvivalSystem` source facts.
- `N20_PROOF_CSV_MAINT`: Maintain `first_hour_route_proof_gates_20260604.csv` as gates are proven or failed by Unity owner artifacts.

## Low/Middle/High/Ultra Consequences

Gameplay truth does not change by quality lane. Oxygen math, death eligibility, item ids, recipe truth, quest flags, save identity, damage packets, creature sensory causes, and route validity remain identical. `GlobalQualityWeight` changes density, cadence, presentation richness, and optional telemetry only.

Low:
- Preserve clean ocean color, surface readability, route silhouettes, landmark shapes, instrument clarity, basic fog LUTs, simple silt, limited fauna count, simple sonar UI, stable O2/death/craft truth.
- No ugly mode. No black surface. No hidden route-critical detail behind high-end effects.

Middle:
- Add normal route density, richer photic biota, better waterline/specular response, more readable item proxy detail, fuller sonar silhouettes, more local silt and audio layers.
- This is expected player hardware and must look genuinely good.

High:
- Spend saved frame time on richer terrain material response, local caustics where justified, better creature secondary motion, stronger sonar/hydrophone presentation, longer LOD residency, denser technogenic traces.
- No new hidden objective truth.

Ultra:
- Visual overkill: volumetric silt/light shafts where justified, premium water/reflection/foam, richer Aegir/sky/moon atmosphere, detailed wetness, visor contamination, richer medium-depth silhouettes, stronger fauna animation/VFX.
- Ultra cannot become required for navigation, combat readability, oxygen decisions, or map/sonar truth.

## Acceptance Statement

The first-hour route is accepted only when graphics, optimization, and gameplay all pass in the same runtime packet. Beautiful screenshots without playable O2/resource/danger/save proof fail. Fast route proof with flat water, muddy sky, weak terrain, or empty gameplay fails. Complex survival/combat without profiler/GC/memory evidence fails.
