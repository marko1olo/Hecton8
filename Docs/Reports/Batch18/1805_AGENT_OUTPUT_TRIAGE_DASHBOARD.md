# 1805 Agent Output Triage Dashboard

ID: 1805  
Role: AGENT_OUTPUT_TRIAGE_AND_NEXT_WAVE_CONTROLLER  
Date: 2026-06-04  
Mode: STATIC TRIAGE ONLY. No Unity control. No code, scene, prefab, or asset edits by 1805.

## Evidence Boundary

This dashboard is an evidence map, not acceptance proof. Static reports, source scans, CSV parses, and old screenshots do not prove current Unity import, Play Mode behavior, profiler cost, GC, Frame Debugger state, Memory Profiler state, player build, save/load continuity, device behavior, or visual quality.

Proof labels used:

- TRUSTED STATIC: source/docs/data inspected; no runtime claim.
- TRUSTED EDITOR: a current Unity editor/import/script-validation artifact exists for the scoped claim only.
- TRUSTED PLAYMODE: a Play Mode route artifact exists for the scoped claim only.
- TRUSTED PROFILER: profiler/GC/Frame Debugger/Memory Profiler artifact exists.
- PENDING: plausible or claimed but missing required proof.
- BLOCKED: a known blocker prevents acceptance or the next proof gate.
- STALE: old evidence or superseded claim cannot prove current state.
- UNSAFE: contains bad source text, proof upgrade, stale doctrine, fake metrics, or current contradiction.
- DUPLICATE: overlaps another output without new proof.

Visual acceptance for surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes remains PENDING current player capture. Darkness/fog cannot hide weak art.

## Evidence Inspected

Authority and proof rules:

- `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `testing.md`, `release.md`.
- `HECTON8_ORCHESTRATOR.md` and `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` for controller behavior only.
- Mandates: QA evidence filter, telemetry/postmortem, performance budgets, DSP audio, procedural wreckage, localization, voxel SDF, signal lanes.

Recent task and output set:

- Batch 18: `1801` through `1805`, plus task files `1806` through `1810`.
- Batch 17 lore/content: `1770` through `1779`.
- Batch 17 visual/presentation: `1741`, `1746`, `1747`, `1748`; no status/log found for `1742`, `1743`, `1744`, `1745`, `1749`, `1750`.
- Route/proof leads: `1428`, `1700`, `1701`, `1738`, `17-C`, `17-D`.
- Active Unity verifier lead: Batch 18 index says existing Codex thread `Verify HECTON-8 refactor safety` owns slow Unity/editor verification. No 1805-local runtime proof artifact was found or created.

## Closure Matrix

| ID / Output | Classification | What Is Safe To Trust | Downgrade / Blocker |
|---|---:|---|---|
| 1801 World surface route evidence | TRUSTED STATIC | Useful static action packet: scene/prefab/asset paths, candidate route anchors, static screenshots, stale dark-surface assumptions identified. | Runtime visual quality, active material path, active Aegir path, route readability, interaction chain, profiler/GC/Frame Debugger remain PENDING UNITY SLOT. |
| 1802 Surface/shallow asset inventory | TRUSTED STATIC | Useful static inventory of water, shore, terrain, sky, Aegir, flora, industrial traces, VFX, UI overlays. Corrected missing TerrainLayer and prefab material false leads. | No import health, scene assignment, runtime density, visual floor, frame time, or memory proof. |
| 1803 First-20 gameplay route blocker auditor | PENDING / INCOMPLETE | Status/log show bootstrap only. | No blocker matrix/report produced in inspected files. Do not treat as route audit completion. |
| 1804 AppliedLore DataMonolith reconciler | TRUSTED STATIC / STATIC BINARY PARTIAL / BLOCKED | Current packet CSV shape coherent: 6900 rows, 460 packets, 15 locale rows each. Direct AppliedLore packet parity to `static_data.h8bin` passes. | Full audit fails on `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` generated page status drift. P456 source/public page still has production-brief residue. Unity bake/import/runtime proof PENDING. |
| 1770 Canon release-set sorting | TRUSTED STATIC / STALE LEADS | Useful packet inventory, surface ownership, spoiler risk, locale coverage, route/binding cross-checks. | Its source-only audit failed on older P456 status/frontmatter state. Current blockers changed; use as static map, not current acceptance. |
| 1771 External public wiki | UNSAFE FOR P456 / TRUSTED STATIC FOR MAPS | Public site editorial map and translation units are useful as leads. | Current `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` still contains production-brief residue and mojibake markers. Its clean P456 claim is contradicted by current disk. |
| 1772 PDA/wiki plus runtime hardening passes | MIXED TRUSTED STATIC / TRUSTED EDITOR FOR SCOPED VALIDATIONS | Useful edited en_US PDA/wiki packet rows and several C# hygiene passes with targeted Unity validation on some files. | Many C# changes have no project build, Play Mode, profiler, or runtime UI proof. Non-English rows stale. Audit repeatedly blocked by P456/P151 drift. |
| 1773 Scanner/field notes | TRUSTED STATIC / SOURCE | Useful scanner/field-note repair set and runtime follow-up defects around scan lock/order/AUP validation. Scoped validations reported. | No scanner UI/TMP overflow, runtime placement, player route, or full build proof. Audit was blocked by P456 status drift at the time. |
| 1774 Terminal/docs/memos | TRUSTED STATIC / SOURCE | Useful terminal/document packet repairs. Current source-only audit pass was reported. | No generated page sync, Unity placement, runtime terminal UI, native localization, or layout proof. |
| 1775 Audio blackbox transcript | TRUSTED STATIC / SOURCE | Useful audio/subtitle segmentation, speaker map, selected transcript repairs, and RU cleanup for RS088. | No VO, subtitle timing, runtime audio, native review, or build/profiler proof. Audio runtime source still has managed callbacks elsewhere. |
| 1776 Facts/crosslinks/player notes | TRUSTED STATIC | Useful fact owner matrix, crosslink inventory, player-note candidates, schema drift callout. | No runtime schema owner, bake, or UI proof. Legacy single-packet drift is a schema/exporter decision, not dead content. |
| 1777 Localization bounds QA | TRUSTED STATIC / BLOCKED | Useful localization issue candidates, text expansion risk, native review queue, RTL/CJK requirements. | `issues=61060` is blocker evidence, not runtime proof. Native review, TMP bounds, font atlas, RTL/CJK rendering remain PENDING. |
| 1778 AppliedLore DataMonolith integrator | TRUSTED STATIC / CLI / STALE BINARY LEAD | Useful route-card/binding matrices, placement blockers, integration recipe, scene placement backlog. | Older P288 stale-binary mismatch is not current first blocker after 1804 direct parity pass. Placement remains weak; Unity bake/runtime proof absent. |
| 1779 static reader/protosite | TRUSTED STATIC plus LOCAL HTTP SMOKE | Useful dependency-free reader improvements, locale selector, RTL handling, filters, packet warm-up accounting. | No browser-render, Playwright, mobile viewport, overflow, or Unity proof. It also includes C# follow-up edits needing build/runtime verification. |
| 1741 orbital prologue | TRUSTED STATIC plus BUILD PASS / PENDING VISUAL | Useful code/scene changes and `dotnet build --no-restore` pass. | No screenshot, profiler, Frame Debugger, Memory Profiler, or current route acceptance. Audio snapshot route PENDING. |
| 1746 camera shake/impact | TRUSTED EDITOR FOR SCRIPT VALIDATION | Scoped Unity script validation and console 0 errors for touched files. | No Play Mode impulse feel, FOV comfort, 0 B/frame, profiler, or full build proof. |
| 1747 ambient particles/marine snow | TRUSTED STATIC | Useful proof packet showing existing GPU marine-snow route; no duplicate rewrite. | No screenshot, GPU timing, Frame Debugger overdraw, or profiler proof. |
| 1748 decal/waterline | PARTIAL TRUSTED STATIC | Useful audit and limited code/shader changes. | Laser cutter glow consumer unresolved; salt crust route unresolved; no shader compile, screenshot, or profiler proof. |
| 1742/1743/1744/1745/1749/1750 | PENDING / NOT FOUND | Task files exist. | No status/log/report found in inspected evidence set. Do not assume launched or complete. |
| 1700 standards director | TRUSTED STATIC / DO NOT USE ESTIMATES AS MEASURED | Useful root standards and domain bible coverage. | Runtime microsecond savings are static estimates unless profiler artifacts exist. No current runtime acceptance. |
| 1701 survival/HUD/scanner | TRUSTED EDITOR FOR MESH IMPORT / STATIC SOURCE | Useful import proof for HUD chevron mesh and extensive source hygiene. | Full build not launched; profiler/play route PENDING. Some current mission marker fallback claims are stale relative to source inspection below. |
| 1738 drone/probe assembler | TRUSTED EDITOR FOR SCOPED SCRIPT VALIDATION / PENDING BUILD | Useful drone factory/metadata/source hygiene and Unity script validations. | No full build, runtime drone route, profiler, or player proof. |
| 1428 route verifier | TRUSTED PLAYMODE FOR ITS SCOPED OLD ROUTE / STALE FOR CURRENT ACCEPTANCE | Useful old Play Mode screenshots, console 0, Crest/world integration leads, static scans. | Later agents changed many files. It is not current first-20 acceptance, not profiler proof, and not a release/player build. |
| 17-C / 17-D | TRUSTED STATIC | Useful math/input guard edits and source scans. | Static microsecond estimates are not measured proof. Builds/tests mostly blocked by CPU/Unity state. |

## Named Fresh Leads Verified

### Procedural Wreck Generator

Lead: runtime wreck mesh fallback in `ProceduralWreckGenerator.cs`.

Current source result: OVERSTATED / STALE.

- `BuildMergedMesh*` exists, but the inspected fallback route is under `#if UNITY_EDITOR`.
- `ShouldBuildMergedMeshFallback()` returns `!Application.isPlaying`.
- `BuildMergedMeshForTier`, `BuildMergedMesh`, and async variants return `null` if `Application.isPlaying`.
- `BuildProxyMesh` was not found by the focused scan.

Accepted blocker wording: editor fallback mesh generation exists and still needs import/editor proof if used. A player-runtime `BuildMergedMesh*` fallback was not proven by current source.

### Mission Marker System

Lead: runtime marker mesh/material fallback in `MissionMarkerSystem.cs`.

Current source result: OVERSTATED / STALE.

- `CreateMarkerMesh()` was not found.
- `EnsureRuntimeResources()` validates assigned `markerMesh` and `markerMaterial` only.
- If mesh/material are invalid, runtime marker resources are nulled and visible marker count is set to zero.
- Render path uses `Graphics.DrawMeshInstanced` with the assigned mesh/material.

Accepted blocker wording: mission markers require scene/prefab assignment and visibility proof. Current source does not fabricate marker mesh/material fallback; missing assets disable markers.

### Audio Managed Callback

Lead: `DynamicMusicGranularSynthesizer.cs` and `VocalBankPlaybackRuntime.cs` use managed `OnAudioFilterRead(float[] data, int channels)`.

Current source result: CONFIRMED STATIC SOURCE BLOCKER.

- `DynamicMusicGranularSynthesizer.OnAudioFilterRead(float[] data, int channels)` copies native buffer data into the managed Unity callback array and zeroes underruns.
- `VocalBankPlaybackRuntime.OnAudioFilterRead(float[] data, int channels)` locks DataVault views, pins managed callback data, calls decode code, and records callback timing with `Stopwatch`.

Acceptance: audio cannot be called DSP-clean or release-accepted until this route is replaced or isolated with a proven native/DSPGraph/IAudioOutputJob-style path and profiler evidence.

### GPR / Foundation / Drone World Truth

Lead: GPR, Foundation, and Drone routes rely on mock SDF/substrate/world truth.

Current source result: PARTLY OVERSTATED, STILL PENDING RUNTIME PROOF.

- `FoundationPylonGpuBatch` explicitly fails closed and publishes `FOUNDATION SNAP FAILED: VOXEL SDF SUBSTRATE MISSING` if it cannot resolve `VoxelSdfTexture3D` from the `WorldStreaming` owner.
- `GroundPenetratingRadarRuntime.TryStageNearestSdf()` uses `IVoxelSonarSdfReadLeaseModel.TryAcquireNearestSonarSdfReadLease` and stages a snapshot. The `_fallbackFrameId` is a frame-id fallback, not an SDF mock in the inspected window.
- `DroneFleetManager.TryAcquireDroneSdfGrid()` also uses `IVoxelSonarSdfReadLeaseModel` and releases failed leases.

Accepted blocker wording: the SDF lease routes exist, but no current Unity/PlayMode/player proof shows real substrate data is available and consumed by GPR/Foundation/Drone in the first-20 route. Foundation has a confirmed missing-substrate fail-closed warning path.

### Proof Blocker

Current result: CONFIRMED.

No 1805-local proof artifact was found for current Unity import, Play Mode first-20 route, profiler, GCMonitor, Frame Debugger, Memory Profiler, player build, save/load diff, or device capture. 1428 has scoped old Play Mode route artifacts, but later work invalidates treating those as current acceptance.

## Outputs Not To Feed Forward As Truth

- 1771 P456 clean-public-page claim. Current disk still has production-brief residue and mojibake in `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md`.
- Any launch prompt that says the wreck route has a proven player-runtime `BuildMergedMesh*` fallback. Current source says editor-only and play-guarded.
- Any launch prompt that says `MissionMarkerSystem` has a current `CreateMarkerMesh()` runtime fallback. Current source has assigned-resource validation only.
- 1778 P288 stale-binary mismatch as the current primary DataMonolith blocker. 1804 direct packet parity no longer reproduces it; current first blockers are P151 generated status drift and P456 source residue.
- 1700, 17-C, 17-D microsecond estimates as measured performance. They are static estimates unless a profiler artifact is named.
- Any report claiming native-final localization, TMP-fit, RTL/CJK visual proof, or release-clean text from static rows.
- 1803 as a completed first-20 blocker matrix. Its inspected status/log show only bootstrap.
- 1742/1743/1744/1745/1749/1750 as completed work. No status/log/report found.

## Useful Outputs To Reference

- 1801 and 1802 for surface/shallow static inventory and route-action leads.
- 1804 for current AppliedLore/DataMonolith blockers: P151 status drift, P456 source residue, static parity boundary.
- 1778 for AppliedLore tool route, scene placement backlog, and binding matrices.
- 1777 for localization issue queues, text expansion risk, native review queue, RTL/CJK requirements.
- 1776 for fact owner/crosslink/player-note schema map.
- 1774 and 1775 for terminal/audio packet repair sets and placement review lists.
- 1779 for static reader/protosite improvement and browser QA route.
- 1701 for HUD/scanner mesh import evidence and authored-mesh direction.
- 1428 for old Play Mode route/capture lead and world/Crest integration evidence, with stale-current-proof caveat.

## Ranked Next Wave Tasks

### No-Unity Tasks

1. `NEXT_P456_SOURCE_PUBLIC_REPAIR`: Rewrite `P456_SITE_HOME_LONGFORM_BRIEF` source packet fields into real public article copy and remove production-brief residue from source, not only generated pages. Independent. Blocks clean public-site export and full AppliedLore audit.
2. `NEXT_P151_STATUS_DRIFT_EXPORTER_FIX`: Fix generated-page/index/source-status drift for `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` through the owning exporter/source route. Independent after no concurrent page export. Blocks full AppliedLore audit.
3. `NEXT_BLOCKER_ERRATA_PACKET`: Write a short errata packet for future agents correcting stale wreck/mission-marker fallback claims. Independent. Prevents wasted work.
4. `NEXT_LEGACY_PACKET_SCHEMA_DECISION`: Decide whether the 9 legacy single-packet JSON files become bundle members or remain first-class ingestion route. Independent schema/exporter task.
5. `NEXT_STATIC_REPORT_PROOF_NORMALIZER`: Audit recent reports for wording that upgrades static estimates to runtime/profiler proof. Independent. Output should be an errata report, not code.

### Content / Lore Tasks

6. `NEXT_LOCALIZATION_RELEASE_TRIAGE`: Split 1777's 61,060 static localization/text-bound findings into release-blocking, internal-QA, and false-positive buckets. Independent. No native-final claim.
7. `NEXT_TERMINAL_AUDIO_SCANNER_PLACEMENT_MANIFEST`: Merge 1773/1774/1775 placement handoffs into one Unity authoring manifest keyed by packet ID, POI, surface, and proof needed. Independent. No scene edits.
8. `NEXT_NATIVE_REVIEW_PACKETS_TOP100`: Create native-review packets for the highest-value first-hour rows only. Independent after P456/P151 source state is corrected.

### Visual Asset Tasks

9. Launch `1806_SURFACE_ROUTE_ACTION_MANIFEST_BUILDER`: prepare static route action manifest from 1801/1802. Independent; no Unity.
10. Launch `1807_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC`: prepare offline bake spec for shoreline/waterline. Independent; no Unity.
11. Launch `1808_AEGIR_SKY_ACTIVE_PATH_AUDITOR`: identify active Aegir/sky path and proof requirements. Independent; no Unity.
12. Launch `1809_PHOTIC_SHALLOWS_BIOTA_PLACEMENT_MANIFEST`: static placement manifest for shallow biota/industrial traces. Independent; no Unity.

### Unity-Slot Tasks - PENDING UNITY SLOT

13. `UNITY_DATAMONOLITH_BAKE_AND_AUDIT`: After P456/P151 source/export blockers are resolved, run DataMonolith bake, full audit, import proof, and boot proof. Conflicts with any other Unity/editor task.
14. `UNITY_APPLIED_LORE_SCENE_PLACEMENT`: Run the 1778 placement plan through Unity owner route and prove increased scene/prefab binding coverage. Depends on DataMonolith/source route stability.
15. `UNITY_MISSION_MARKER_ASSIGNMENT_PROOF`: Verify actual mission marker mesh/material assignments and marker visibility in the first-20 route. Independent of DataMonolith, but requires Unity slot.
16. `UNITY_SURFACE_SHALLOW_CAPTURE_PASS`: Capture current surface/sky/Aegir/waterline/shallow route with screenshots plus console state. Depends on 1806-1809 manifests for target list.
17. `UNITY_SDF_SUBSTRATE_ROUTE_PROOF`: Prove real SDF substrate is present and consumed by GPR, Foundation pylon snapping, and Drone repulsion. Conflicts with gameplay route proof.

### Player Build / Profiler Tasks - PENDING UNITY SLOT

18. Launch or finish `1810_RUNTIME_PROOF_HARNESS_PREP` as no-Unity prep first, then run `PLAYER_FIRST20_PROOF_RUN`: boot -> menu -> world -> swim -> oxygen/pressure risk -> salvage/tool/resource -> craft/repair/build -> hazard response -> save/load -> return state.
19. `PROFILER_FRAME_DEBUGGER_GC_PASS`: Capture profiler, GCMonitor, Frame Debugger, Memory Profiler, and render screenshots for the same route after source blockers are fixed.
20. `WINDOWS_PLAYER_BUILD_COPPER_ROUTE`: Build and run Windows player route with save/load diff after Play Mode proof is clean.

### Integration Tasks

21. `AUDIO_DSP_CALLBACK_REPLACEMENT_DESIGN`: Replace or isolate `OnAudioFilterRead(float[]...)` routes in DynamicMusic and VocalBank. Independent design/source task first; build/profiler later.
22. `BUILD_COMPILE_CONSOLIDATION_LANE`: Controlled build/test lane for C# changes from 1772, 1773, 1775, 1779, 1746, 1701, 1738, 17-C, 17-D. Must wait for CPU/dotnet/Unity gate.
23. `SDF_OWNER_CONTRACT_INTEGRATION`: If Unity proof shows missing substrate, define one owner route for VoxelSdfTexture3D publish and consumer read leases across GPR/Foundation/Drone. Depends on failed or incomplete Unity proof.

## NEXT_8_HOURS

1. Launch no-Unity work first: `1806`, `1807`, `1808`, `1809`, and `1810` prep if not already running. These can read 1801/1802, must not claim runtime proof, and do not need Unity.
2. Launch `NEXT_P456_SOURCE_PUBLIC_REPAIR` and `NEXT_P151_STATUS_DRIFT_EXPORTER_FIX` before any DataMonolith bake. Full AppliedLore audit currently stops at P151, and P456 source is bad.
3. Launch `NEXT_BLOCKER_ERRATA_PACKET` immediately to stop future agents from chasing stale wreck/marker fallback claims.
4. Monitor only file artifacts: `Status_1806.md` through `Status_1810.md`, `LOG_1806.md` through `LOG_1810.md`, and new Batch18 reports. Do not require live chat output.
5. Poll every 20-30 minutes for no-Unity outputs. If CPU stays above 50 percent, any `dotnet build`, Unity bake, Play Mode, player build, or profiler task sleeps.
6. Hand Unity/editor proof to the existing verifier lane or a single later Unity-slot agent. Do not start a second editor/profiler/build run while the current verifier owns it.

Monitor for:

- P456 source residue removed from `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`, generated public pages, and indexes.
- P151 `ru_RU` source/index/generated page status alignment.
- 1806-1810 outputs using proof labels and no runtime claims.
- No task output accepting surface/shallow visuals without current captures.

## DO_NOT_LAUNCH_TOGETHER

- Do not run DataMonolith bake, scene placement, Play Mode route proof, player build, profiler, Frame Debugger, or Memory Profiler in parallel. They all contend for Unity/editor state.
- Do not run `dotnet build` while CPU is over 50 percent, `dotnet`, `csc`, `VBCSCompiler`, Unity import, shader compile, or another build is active.
- Do not run page exporters while P456/P151 source repair is in progress unless the exporter owner is that same task.
- Do not run multiple agents editing `Publication_Surface_Index.csv`, generated pages, and packet CSVs at the same time.
- Do not run scene/prefab visual placement agents at the same time as scene-placement AppliedLore agents.
- Do not run audio DSP integration at the same time as broad build/profiler proof. The audio change should land before the proof lane.
- Do not launch Unity SDF proof and first-20 route proof together. They need deterministic editor focus and clean logs.

## DOBIVKA_PROMPTS

Use these only if the orchestrator decides the named agent needs follow-up. Do not send them from 1805.

### Dobivka 1803

```text
HECTON-8 DOBIVKA AGENT 1803. Your inspected Status/LOG show only bootstrap. Finish the FIRST20 route blocker matrix or explicitly mark BLOCKED. Use proof labels from quality.md. Do not claim Unity/runtime/profiler proof. Include boot->menu->world->swim->oxygen/pressure->salvage/tool/resource->craft/repair/build->hazard->save/load beat state, blockers, Unity-slot proof packet, and next implementation tasks.
```

### Dobivka 1771

```text
HECTON-8 DOBIVKA AGENT 1771. Current disk contradicts your P456 clean claim. `Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` still contains production-brief markers (`Longform spine`, `Public brief`, `SITE HOME`, `Assemble for website`) and mojibake markers. Re-audit P456 from packet source through generated pages and indexes. Do not patch generated pages only if source remains wrong. Update handoff with current blocker state and no native-review claim.
```

### Dobivka 1777

```text
HECTON-8 DOBIVKA AGENT 1777. Refresh localization QA handoff against current disk after 1804/1805 findings. Preserve the 61,060 static issue count as static blocker evidence, but add current P456/P151 blockers and remove any implication that P456 ru_RU is clean. Do not claim TMP, RTL/CJK, font atlas, native-review, or runtime proof.
```

### Dobivka 1778

```text
HECTON-8 DOBIVKA AGENT 1778. Your P288 stale-binary blocker is now historical, not the current first blocker. 1804 direct AppliedLore packet parity passes on current `static_data.h8bin`; full audit now fails at P151 generated status drift, and P456 source residue remains. Update handoff to preserve useful placement/binding matrices while downgrading P288 to stale lead unless rerun reproduces it.
```

### Dobivka 1741

```text
HECTON-8 DOBIVKA AGENT 1741. Your report has build proof but no visual/profiler proof. Add a concise errata note: orbital prologue source/build pass is not visual acceptance. List exact screenshot, Frame Debugger, profiler, memory, and route proof still required. Do not run Unity unless explicitly given the Unity slot.
```

### Dobivka 1746 / 1747 / 1748

```text
HECTON-8 DOBIVKA AGENT 1746-1748. Add one-page proof-boundary errata for your outputs. Separate static/source/script-validation evidence from Play Mode/profiler/Frame Debugger/screenshot proof. Mark all visual feel, GPU timing, fill-rate, waterline, particle density, and decal acceptance as PENDING UNITY SLOT unless an artifact path exists.
```

### Dobivka 17-C / 17-D / 1700

```text
HECTON-8 DOBIVKA STATIC ESTIMATE ERRATA. Review your reports for microsecond savings. Any number without profiler artifact must be labeled STATIC ESTIMATE, not measured saving. Preserve useful source gates, but prevent downstream agents from treating estimates as profiler proof.
```

## Acceptance / Rejection Criteria For Next Wave

Accept no-Unity outputs only if they provide:

- Exact source/report paths inspected.
- Proof labels per claim.
- No runtime/profiler/player-build acceptance from static evidence.
- No stale dark-surface doctrine.
- No fake hashes, fake line numbers, fake timings, or fabricated Unity artifacts.
- Low/Middle/High/Ultra consequences where implementation or route proposals affect quality scaling.

Reject outputs if they:

- Claim current runtime proof without artifact paths.
- Treat generated public pages as source truth when packet CSV/JSON is wrong.
- Treat static microsecond estimates as measured.
- Launch Unity/build while the verifier lane or CPU/dotnet gate is busy.
- Hide weak surface/shallow visuals with darkness, fog, storm, or noir phrasing.
- Reintroduce binary quality switches instead of continuous `GlobalQualityWeight`.

## Scaling Consequences

Low: next work must preserve the same facts, packet IDs, route ownership, and readable visual cues with lower density/cadence. Cheap devices still need premium-looking surface/shallow route cues, not flat placeholders.

Middle: accept fuller density and reader/placement manifests after source blockers are fixed, but do not add truth owners or schema fields without a route decision.

High: spend saved CPU/GPU budget on richer Aegir/waterline/biota/audio/UI presentation after profiler proof, not on hidden simulation or extra hot polling.

Ultra: visual overkill can increase capture density, shader/detail lanes, and optional archive presentation. It must not change gameplay truth, save identity, DataMonolith DTO layout, locale identity, or unlock authority.

