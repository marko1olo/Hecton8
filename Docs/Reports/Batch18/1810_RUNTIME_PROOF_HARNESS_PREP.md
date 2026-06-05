# 1810 Runtime Proof Harness Prep

Agent: 1810
Role: RUNTIME_PROOF_HARNESS_PREP
Final state: STATIC PROOF HARNESS COMPLETE

## Evidence Boundary

This packet is static verification prep. It proves that the route proof requirements, capture IDs, rejection gates, storage rules, and verifier/controller prompts have been prepared from current static docs, reports, folders, and source scans.

It does not prove Unity import, Play Mode behavior, visual quality, profiler cost, GC, Frame Debugger state, Memory Profiler state, player build health, save/load behavior, or device behavior.

No Unity control, Play Mode run, profiler run, Frame Debugger run, Memory Profiler run, screenshot capture, player build, dotnet build, scene edit, prefab edit, material edit, script edit, or asset edit was performed by 1810.

## Authorities And Mandates

Authorities read:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `presentation.md`
- `gameplay.md`
- `survival.md`
- `water.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`
- `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`
- `Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.md`
- `Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.csv`

Selected mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`

`Docs/Actual Domains of Project.txt` was checked and is missing. Narrow domain used: surface/photic first-route runtime proof and acceptance harness.

## Static Evidence Used

Completed reports 1801 and 1802 define the current surface/shallow capture needs:

- player spawn / first surface look;
- waterline toward Aegir and coast;
- underwater 0-30 m `Starter_ReefField`;
- `Route_Anchor` toward `Node_Copper_A` / `Scrap_A`;
- Aegir/sky/horizon;
- wet basalt/shore foam close-up;
- industrial trace: dock/sub/turbine;
- resource/fabricator chain;
- Compact/Middle/High/Ultra comparison from same cameras.

1805 confirms current acceptance state is still pending Unity/player/profiler proof. 1806 gives the static action manifest and route beat names. 1810 does not replace 1806; it defines how later runtime proof must be accepted or rejected.

Static scene YAML search confirmed these route object names in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`:

- `Main Camera`
- `Player`
- `Route_Anchor`
- `Node_Copper_A`
- `Scrap_A`
- `Forward_Fabricator`
- `Fabrication_Outpost`
- `Starter_ReefField`

Current screenshot folders:

- `Assets/Screenshots/` contains `h8_water_ui_baseline_before_08.png` and `h8_scene_water_ui_baseline_before_08.png`. These are static baseline references, not acceptance proof.
- `Docs/Screenshots/` contains many older `1428_*` screenshots. They are stale leads unless a later Unity verifier explicitly ties them to the current scene state.

## Owned Machine Output

CSV checklist:

- `Docs/Reports/Batch18/1810_SURFACE_ROUTE_CAPTURE_CHECKLIST.csv`

Columns match task contract:

- `proof_id`
- `route_moment`
- `camera_position_or_scene_hint`
- `visual_requirement`
- `gameplay_requirement`
- `performance_requirement`
- `capture_type`
- `file_naming`
- `pass_gate`
- `fail_gate`
- `required_after_task`
- `proof_label`

The CSV contains 16 proof IDs and covers graphics, optimization, and gameplay together.

## Play Mode Route Smoke Checks

Minimum route smoke path for later Unity verifier:

1. Boot through the proper route: `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
2. Spawn: player has control, camera is not buried/black, HUD/instrument state is readable.
3. Surface look: Aegir, sky, moons/clouds, coastline, ocean surface, and a route/evidence cue are visible.
4. Water entry: waterline transition is readable; surface remains bright and premium.
5. Oxygen display: oxygen reserve or survival instrument is visible, unclipped, and zero-GC under profiler/GCMonitor.
6. Route anchor: `Route_Anchor` or equivalent world cue is visible without debug map dependence.
7. Resource/scrap: `Node_Copper_A` and `Scrap_A` are visible, physically credible, and interactable if the owning system is active.
8. Fabricator: `Forward_Fabricator` / `Fabrication_Outpost` reads as a physical machine and route anchor, not menu-only UI.
9. Return path: player can retreat to anchor/coast using world landmarks first and instruments second.
10. Hazard response: if a creature, pressure, weather, or other hazard is active, it must telegraph and leave evidence.
11. Death/respawn/drop rule: only required if death is reachable in the proof route; cause, respawn, dropped resources, and core-tool policy must be recorded.
12. Save/load only if the verifier claims route persistence; otherwise explicitly mark save/load as PENDING.

## Visual Proof Gates

Required visual gates:

- Waterline: premium ocean color, wave normals, specular, foam, refraction hint, wet edge.
- Coast: wet basalt, strata/detail/masks, foam contact, sediment, silhouette.
- Aegir/sky/moons/clouds: textured, scaled, soft, bright, not muddy or procedural stripes.
- 0-30 m shallows: colorful authored coral/kelp/biota density with route corridor and return cue.
- 30-100 m route: bright/readable photic route; no fog/darkness hiding missing assets.
- Industrial traces: dock/sub/turbine/wreck/service lines read as machinery/evidence.
- UI legibility: oxygen/pressure/route instruments readable at target aspect and compact tier with no text overlap.
- Compact tier: still attractive and intentional. `GlobalQualityWeight = 0.0` is not ugly mode.

Reject surface/photic proof if it uses darkness, fog, bloom, storm, silt, UI overlays, or noir language to hide flat water, weak terrain, bad sky, sparse biota, primitive debris, or route absence.

## Optimization Proof Gates

Runtime acceptance needs the same repro path as the visual proof:

- Unity Profiler artifact: frame time, main thread, render thread, GPU where available.
- GC proof: GC Alloc column or GCMonitor/ProfilerRecorder artifact, 0 B/frame for exercised hot paths.
- Frame Debugger proof: active water/sky/coast/material passes and SetPass/draw evidence.
- Memory/VRAM proof: texture/RT/graphics memory where assets or render passes changed.
- Batches/SetPass: route capture must not ignore draw-call/material churn.
- No hidden hot-path generation: no runtime hero mesh/texture/scene searches to make the route look good.
- Any single feature over 0.1 ms is suspicious until profiler evidence and fallback exist.
- No static estimate may be reported as measured performance.

Existing leverage:

- `Assets/_Project/Scripts/Core/GCMonitor.cs`
- `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
- `Assets/_Project/Scripts/RuntimePerformanceProfiler.cs`
- `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs`
- `Assets/_Project/Tests/PlayMode/InquisitionStabilityPlayModeTests.cs`

These are leverage points only. They do not prove the current route until run in the current Unity slot.

## Gameplay Proof Gates

Gameplay proof must show:

- Oxygen risk: visible reserve/warning, route planning value, no hidden spreadsheet drain.
- Pressure/hazard response: readable cause, counterplay, and evidence.
- Interaction/readability: resource, scrap, and fabricator are physical route decisions, not abstract icons.
- Return path: retreat is readable without omniscient debug map.
- Failure evidence: death or hazard failure must record cause; if relevant, respawn anchor, dropped resource policy, and core tool preservation must be visible or logged.
- Authority boundary: no UI, VFX, or presentation element may invent survival/resource/route truth.
- Quality scaling: Compact/Middle/High/Ultra changes sensory density, not gameplay truth, save identity, DTO layout, route ownership, or interaction validity.

## Existing Tests And Scripts To Leverage

Static/source inspection found useful existing assets:

- `Assets/_Project/Tests/PlayMode/InquisitionStabilityPlayModeTests.cs`: zero-GC frame capture, mono memory checks, save/thread-affinity, physics determinism, hardware tier gate.
- `Assets/_Project/Tests/Editor/HectonSurvivalSystemEditTests.cs`: oxygen drain, temperature, pressure, database, and transport pressure scale formula checks.
- `Assets/_Project/Tests/Editor/OceanSinglePassGuillotineEditTests.cs`: ocean/shoreline DTO layout, continuous quality scaling, Crest camera cut guards, telemetry buffer IDs.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`: celestial/sky material and atmosphere math checks.
- `Assets/_Project/Tests/Editor/ShinobuOceanSurfaceAtmosphereEditTests.cs`: wave math, read accessor purity, buffer IDs, finite water math.
- `Assets/_Project/Tests/Editor/SceneIntegrityValidator1627Tests.cs`: missing script, bootstrap graph, hot dependency lookup, DataVault lock, and presentation phase violation scans.

No existing file was edited. No test was run. These remain PENDING UNITY SLOT.

## Capture Storage And Naming

Preferred proof storage:

- `Docs/Reports/Batch18/1810_Captures/`

Naming pattern:

- `1810_SHOT_<nn>_<route_label>_<tier>_<YYYYMMDD_HHMMSS>.png`
- `1810_PM_<nn>_<route_label>_<tier>_<YYYYMMDD_HHMMSS>.png`
- `1810_PROF_<nn>_<route_label>_<tier>_<YYYYMMDD_HHMMSS>.<ext>`
- `1810_FD_<nn>_<route_label>_<tier>_<YYYYMMDD_HHMMSS>.<ext>`
- `1810_GC_<nn>_<route_label>_<tier>_<YYYYMMDD_HHMMSS>.<ext>`

Use tiers exactly:

- `compact`
- `middle`
- `high`
- `ultra`

`Assets/Screenshots/` is allowed only when a Unity capture path already writes there or an import-sensitive screenshot must be reproduced. Do not use `Assets/` for temporary proof clutter. If a capture lands in `Assets/Screenshots/`, the verifier must record the exact path and avoid overwriting the current baseline files.

## Unity Slot Protocol

Use Unity only when all are true:

- no active Unity/editor owner is running;
- CPU is not over 50 percent;
- no `dotnet`, `csc`, `VBCSCompiler`, Unity import, shader compile, player build, profiler, Frame Debugger, Memory Profiler, or DataMonolith bake is active;
- the verifier owns the slot and records the start/stop time;
- the verifier can produce artifact paths, not prose claims.

Back off immediately when:

- another Unity verifier lane is active;
- CPU/build gate fails;
- compile/import work begins under another owner;
- the route proof requires scene edits outside the verifier's scope;
- screenshot/profiler data cannot be written to deterministic paths;
- Unity console reports current errors that make route capture untrustworthy.

No 1810-local Unity slot was used.

## No-Proof Rejection Language

Use this wording when rejecting future reports:

- `REJECT: static source/path evidence was reported as runtime proof. Downgrade to PENDING UNITY SLOT.`
- `REJECT: screenshot lacks player decision, route cue, evidence cue, or survival instrument state. Beauty-only is insufficient.`
- `REJECT: surface/photic route hides weak art with darkness, fog, storm, bloom, silt, or UI overlay.`
- `REJECT: profiler/GC/Frame Debugger claim has no artifact path, command/tool, scene, timestamp, and hardware/tier.`
- `REJECT: old 1428 screenshot or previous batch proof was reused as current acceptance after later source/scene changes.`
- `REJECT: Compact mode is ugly, muddy, flat, or route-hostile. Compact is minimum survival presentation, not low art.`
- `REJECT: quality tier changes gameplay truth, resource identity, save identity, route ownership, or interaction validity.`
- `REJECT: primitive placeholder, proxy, or third-party package path was treated as final visual proof.`

## Compact / Middle / High / Ultra Expectations

Compact:

- bright ocean color, clean Aegir/sky silhouette, readable coastline, strong route landmarks, limited but authored biota, minimal HUD, bounded foam/VFX;
- no bloom cover, no full volumetrics, no runtime hero generation, no dark-surface downgrade.

Middle:

- more route dressing, richer shoreline foam, cheap caustic fakes, stronger material breakup, clearer sonar/HUD support;
- same gameplay truth and route ownership.

High:

- stronger water reflection/glint, richer wet basalt, denser flora with LOD, better Aegir/cloud transition, measured optics/VFX;
- saved performance buys visible richness only.

Ultra:

- visual overkill: richer sky layering, near-field water/foam breakup, dense photic biota, detailed machinery wear, stronger lens/instrument response;
- no new gameplay facts, save identity, DTO layout, or authority route.

## Unity Verifier Prompt

```xml
<UNITY_VERIFIER_PROMPT id="1810">
Role: SURFACE_ROUTE_RUNTIME_PROOF_VERIFIER
Input:
- Docs/Reports/Batch18/1810_RUNTIME_PROOF_HARNESS_PREP.md
- Docs/Reports/Batch18/1810_SURFACE_ROUTE_CAPTURE_CHECKLIST.csv
- Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.md
- Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.csv

Boundary:
- Use Unity only when the slot is uncontested and CPU/build gates pass.
- Do not run in parallel with DataMonolith bake, scene placement, profiler, player build, or another Unity verifier.
- Do not claim visual/profiler/GC/Frame Debugger proof without fresh artifact paths.
- Do not use old 1428 screenshots as current proof.
- Do not edit scene/prefab/material/script unless explicitly assigned implementation authority.

Route:
00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD -> spawn -> surface look -> waterline -> 0-30m shallows -> 30-100m route -> Route_Anchor -> Node_Copper_A -> Scrap_A -> Forward_Fabricator/Fabrication_Outpost -> return path.

Produce:
- all required 1810 screenshots or mark exact proof IDs blocked;
- Play Mode smoke notes for spawn, surface look, water entry, oxygen display, route anchor, resource/scrap, fabricator, return path;
- Unity console state;
- Unity Profiler artifact;
- GC Alloc or GCMonitor artifact;
- Frame Debugger material/pass artifact for water/sky/coast/UI if changed or claimed;
- memory/VRAM/batches/SetPass notes tied to same repro path;
- Compact/Middle/High/Ultra comparison from same camera where possible.

Reject:
- beauty-only screenshots;
- dark-cover surface;
- flat water;
- grey procedural coast;
- UI-only route guidance;
- primitive debris/fabricator/resource proof;
- unmeasured expensive water/sky/VFX/render path;
- any proof label upgraded beyond artifact class.

Final state must be one of:
- RUNTIME PROOF PASS WITH CURRENT ARTIFACTS
- BLOCKED BY SPECIFIC UNITY EVIDENCE
- ABORTED DUE TO UNITY SLOT/BUSY BUILD GATE
</UNITY_VERIFIER_PROMPT>
```

## Controller Triage Prompt

```xml
<CONTROLLER_TRIAGE_PROMPT id="1810">
Role: PROOF_REPORT_TRIAGE_CONTROLLER
Input: a future Unity/runtime report claiming surface/shallow proof.

Check every claim:
1. Does the report cite 1810 proof IDs from the CSV?
2. Does each visual claim have current screenshot/video artifact path?
3. Does each profiler/GC/Frame Debugger claim have artifact path, scene, timestamp, hardware/tier, and repro route?
4. Does Compact remain bright, readable, and premium, not merely cheap?
5. Does High/Ultra add sensory richness without changing gameplay truth?
6. Do oxygen, hazard, resource, scrap, fabricator, return path, and death/respawn/drop claims have Play Mode evidence?
7. Does the report separate STATIC, EDITOR, PLAYMODE, PROFILER, FRAME_DEBUGGER, PLAYER_CAPTURE, and PLAYER_BUILD evidence?
8. Does any claim depend on old 1428 screenshots, inactive scene objects, static path existence, disabled renderer candidates, or fake metrics?

Accept only if graphics, optimization, and gameplay all pass for the same route packet.
Reject or downgrade everything else to PENDING UNITY SLOT.
</CONTROLLER_TRIAGE_PROMPT>
```

## Final Fake-Proof Scan

1810 produced no fake screenshots, fake profiler numbers, fake Frame Debugger events, fake Play Mode results, fake Unity console state, fake build output, fake line numbers, or fake hardware metrics.

All runtime acceptance remains PENDING UNITY SLOT.
