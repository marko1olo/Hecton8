# 1818 Mission Marker Assignment Visibility Audit

Agent: 1818 / MISSION_MARKER_ASSIGNMENT_VISIBILITY_AUDITOR
Date: 2026-06-04
Mode: Static source/data audit only. No Unity, Play Mode, profiler, runtime capture, or player build proof was produced.

## Verdict

Mission marker fallback-delete claims are stale. Current `MissionMarkerSystem` does not fabricate a runtime marker mesh/material fallback. It requires authored `markerMesh` and `markerMaterial`, validates them, and draws instanced meshes only when valid resources and resolved marker positions exist.

Current blocker is assignment and visibility proof:

- `MissionMarkerSystem` has no static scene/prefab/data owner proof under `Assets/_Project/Scenes`, `Assets/_Project/Prefabs`, or `Assets/_Project/Data`; no reference to script guid `98f551b622676294787aa78593c06504` was found in those scopes.
- First-route quest IDs are published by `FirstHourDirector`, but the matching quest assets do not statically prove marker targets or fallback positions for `quest_arrival`, `quest_copper_sample`, or `quest_first_breath`.
- Existing HUD/scanner marker assets exist, but current static proof binds them to scanner/HUD surfaces, not to `MissionMarkerSystem`.

Acceptance state: **BLOCKED STATIC / PENDING UNITY SLOT**. The audit deliverable is complete; route marker runtime acceptance is not complete.

## Authority And Scope

Read authority: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `gameplay.md`, `ui.md`, `performance.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `sonar.md`, `Docs/Reports/Batch18/1803_FIRST20_ROUTE_BLOCKER_MATRIX.md`, `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`, and `Docs/Reports/Batch18/1813_STALE_BLOCKER_ERRATA_PACKET.md`.

Requested `hud.md` and `navigation.md` were not present at project root during this audit. Used `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `sonar.md`, and selected `.agents-skills` mandates as nearest authorities.

Mandates loaded: UI diegetic physical interfaces, UI zero-GC streaming, quest state graph logic, acoustic/sonar visibility, signal lane segregation, zero-GC policy, and performance budgets.

## Current Source Truth

`Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs:39` and `:42` define serialized `markerMesh` and `markerMaterial`.

`MissionMarkerSystem.EnsureRuntimeResources()` validates authored resources only:

- `MissionMarkerSystem.cs:349-351` requires non-null mesh, submesh count, and nonzero index count.
- `MissionMarkerSystem.cs:352-354` requires non-null material, shader, and instancing enabled.
- `MissionMarkerSystem.cs:356-361` nulls runtime refs and sets `_visibleMarkerCount = 0` when invalid.
- No `CreateMarkerMesh` or material fabrication path was found in current `MissionMarkerSystem.cs`.

`MissionMarkerSystem.Render()` draws through `Graphics.DrawMeshInstanced` only when visible count and runtime resources are valid (`MissionMarkerSystem.cs:190-206`).

Marker positions are resolved from quest presentation:

- `QuestData.cs:82-89` has `markerTargetId`, `markerWorldPosition`, and `markerHeightOffset`.
- `QuestStateManager.cs:249-252` copies authored presentation into runtime marker arrays.
- `QuestStateManager.cs:511-551` exposes marker target hash, world position, and height through `TryCopyQuestPresentation`.
- `MissionMarkerSystem.cs:547-575` caches the quest marker presentation.
- `MissionMarkerSystem.cs:491-513` renders only `atlas6_core` targets or nonzero fallback positions; otherwise it returns false.

This is fail-closed, not placeholder art. It is also not fail-visible to the player when assignments are missing.

## First Route Quest Path

`FirstHourDirector.cs:711`, `:714`, and `:720` define the first-route quest IDs:

- `quest_arrival`
- `quest_copper_sample`
- `quest_first_breath`

Quest activation/transition proof exists in source:

- `FirstHourDirector.cs:1288-1290` completes arrival and activates the first resource quest.
- `FirstHourDirector.cs:1360-1361` activates first depth after first resource completion.
- `FirstHourDirector.cs:1402-1404` completes first resource and activates first depth when copper is collected.
- `FirstHourDirector.cs:1545-1571` routes activation/completion through `IQuestSystem`.

Marker assignment proof does not exist for those three quest assets:

- `Quest_Arrival.asset:15` has `questId: quest_arrival`; `rg` found no serialized marker fields in that asset.
- `Quest_CopperSample.asset:15` has `questId: quest_copper_sample`; `rg` found no serialized marker fields in that asset.
- `Quest_FirstBreath.asset:15` has `questId: quest_first_breath`; `rg` found no serialized marker fields in that asset.

Related first-hour quest assets with marker fields are still unassigned:

- `Quest_FirstHour_ExitLifePod.asset:36-38`: empty `markerTargetId`, zero `markerWorldPosition`, height 6.
- `Quest_FirstHour_CollectTitanium.asset:36-38`: empty `markerTargetId`, zero `markerWorldPosition`, height 6.
- `Quest_FirstHour_CraftScanner.asset:37-39`: empty `markerTargetId`, zero `markerWorldPosition`, height 6.

Atlas quests have static targets (`atlas6_core`, height 18) in `Quest_SignalDetected.asset`, `Quest_SignalDecoded.asset`, and `Quest_CoreReached.asset`. That is not proof for first-route arrival/resource/depth markers.

## Asset Binding Findings

Candidate visual assets exist:

- `Assets/_Project/Art/Meshes/M_HUD_ThreatChevron.asset`, guid `f842ecd7ee1f487fa7e631164e81fbea`.
- `Assets/_Project/Art/Materials/MAT_HUD_ThreatChevronInstanced.mat`, guid `a8512bd7412f4b46903d53022b9a0c1e`, with `m_EnableInstancingVariants: 1` at line 17.

Current static bindings found:

- `Suit_HUD_Canvas.prefab:2484-2485` binds the material and mesh to HUD threat chevrons.
- `Tool_Scanner_Held.prefab:66` binds the material to scanner marker material.
- `Suit_HUD_Canvas.prefab:3723` contains `RelayRouteMarker`.
- `RelayHUDRuntimeBootstrap.cs:41` warns if `RelayRouteMarker` is missing and states runtime marker fabrication is disabled.

No static binding found:

- No `MissionMarkerSystem` component reference by script guid under production scene/prefab/data scopes.
- No serialized `markerMesh` or `markerMaterial` assignment tied to `MissionMarkerSystem`.

The scanner and relay HUD systems cannot be counted as mission marker proof without an explicit quest/mission connection.

## Failure Behavior

Safe:

- Missing or invalid marker mesh/material fails closed: visible marker count becomes zero.
- Missing quest marker target/position fails closed: no marker matrix is produced.
- No placeholder marker art is generated by the inspected source.

Unsafe for acceptance:

- Active quests can have title/description presentation but no marker target/position. `QuestStateManager.TryCopyQuestPresentation()` returns true when `titleLength > 0`, even if `markerTargetHash == 0` and `markerWorldPosition == Vector3.zero`; `MissionMarkerSystem` then caches a quest that cannot render a marker.
- The inspected mission marker path does not expose a player-visible or editor-fail-visible missing-assignment state.
- Missing renderer component/resource assignment can silently remove all mission markers.

## HUD, Sonar, And Map Risk

Mission markers are player-facing instruments, not decorative icons. A first-route marker must not become an omniscient arrow that reveals hidden world truth without discovery or sonar evidence.

Required integration rule for future binding:

- For authored/tutorial-known anchors, direct marker is acceptable if the objective is already known to the player.
- For resource/search objectives, marker should point to a discovered route zone, scanner clue, or sonar-confirmed confidence target, not an exact hidden pickup unless the pickup has been discovered.
- Stale/uncertain marker state must be visible through material/intensity/pulse cadence or HUD status, not through placeholder art or noisy UI spam.
- World-space rendering must respect depth/occlusion readability and not draw a cheap screen-overlay marker through geometry.

## Required Unity Slot Checks

Do these in a later Unity/editor slot only:

1. Locate or create the production `MissionMarkerSystem` owner in the route scene/prefab.
2. Serialize the component with script guid `98f551b622676294787aa78593c06504`.
3. Bind `markerMesh` to an approved non-placeholder mission marker mesh. `M_HUD_ThreatChevron.asset` is only a candidate; product must accept it as mission marker art, not merely scanner/HUD art.
4. Bind `markerMaterial` to an approved instanced material. `MAT_HUD_ThreatChevronInstanced.mat` is a candidate because static YAML shows instancing enabled.
5. Capture serialized diff proving component, `markerMesh`, and `markerMaterial` refs.
6. Assign each first-route quest a real marker route:
   - `quest_arrival`: route-known orientation/lifepod/exit anchor, or explicit no-marker policy if completed before marker display is expected.
   - `quest_copper_sample`: discovered resource-zone marker or authored route hint. Do not reveal exact hidden copper unless discovery proof exists.
   - `quest_first_breath`: safe descent/depth-route marker, not a magic final coordinate.
7. Capture serialized quest data diff proving `markerTargetId` or nonzero `markerWorldPosition` for every marker-required quest.
8. Run Play Mode route proof: marker appears when objective activates, updates against player/AUP, respects distance/occlusion, and clears on quest completion.
9. Capture screenshots for arrival/resource/depth marker visible states and post-completion clear state.
10. Capture profiler proof separately: 300+ frames, 0 B GC in marker/HUD hot paths, bounded draw count, no hot `Find`/`GetComponent`/string allocation path.

## Narrow Fix Proposal

No source/data edits were made in this audit.

Safe future fixes:

- Add editor validation that first-route marker-required `QuestData` assets must have either `markerTargetId`, nonzero `markerWorldPosition`, or an explicit marker policy of `None`.
- Add once-per-session development telemetry when `MissionMarkerSystem` has active quests but no valid mesh/material or no renderable marker assignment. Do not log per frame.
- Add a fail-visible HUD/diagnostic state for missing marker signal in development builds, using authored HUD elements only. Do not generate placeholder marker art.
- Keep resource/search objectives tied to discovered zone or sonar-confidence data; do not use mission marker data to leak hidden pickups.

## Quality Scaling Consequences

Compact:

- Maximum one or two active mission markers, instanced mesh only, no bloom, hard distance cutoff, clean silhouette, confidence encoded by opacity/pulse cadence.

Middle:

- Up to four active markers, depth-tested material, stable low-frequency pulse, occlusion fade/dither, route-zone label handled through zero-GC HUD text only if already authored.

High:

- Richer material response, confidence/staleness bands, optional sonar-ping echo relation for uncertain targets, still same marker truth and same quest assignment source.

Ultra:

- Extra holographic detail, refined depth/occlusion treatment, cinematic overdraw within budget, no added gameplay truth and no more precise target than lower tiers.

## Binding Matrix

See `Docs/Reports/Batch18/1818_MISSION_MARKER_BINDING_MATRIX.csv`.

## Future-Agent Copy Prompt

Continue from `Docs/Reports/Batch18/1818_MISSION_MARKER_ASSIGNMENT_VISIBILITY_AUDIT.md` and `Docs/Reports/Batch18/1818_MISSION_MARKER_BINDING_MATRIX.csv`. Do not chase `CreateMarkerMesh` fallback deletion; current `MissionMarkerSystem` validates authored `markerMesh`/`markerMaterial` and fails closed. Work item is Unity-slot assignment proof: serialize a production `MissionMarkerSystem` owner, bind approved non-placeholder marker mesh/material, assign `quest_arrival`, `quest_copper_sample`, and `quest_first_breath` marker target/position or explicit no-marker policy, then produce Play Mode screenshots and profiler proof. Until those artifacts exist, mark mission marker route acceptance `PENDING UNITY SLOT`.
