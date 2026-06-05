# 1824 First Route Mission Marker Policy Packet

Agent: 1824 / FIRST_ROUTE_MISSION_MARKER_POLICY_PACKET
Date: 2026-06-04
Mode: static policy and Unity-slot handoff only
Evidence class: STATIC_DOC / STATIC_SOURCE references only
Runtime/editor/profiler/player-capture proof: PENDING UNITY SLOT

## Boundary

No Unity, PlayMode, profiler, screenshot, build, exporter, scene edit, prefab edit, quest asset edit, marker asset edit, UI code edit, source data edit, task-file edit, or unrelated doc edit was performed.

This packet decides first-route marker policy. It does not prove marker runtime behavior, component binding, material assignment, quest asset serialization, visibility, occlusion, overdraw, GC, save/load, or player readability.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `ui.md`
- `gameplay.md`
- `world.md`
- `narrative.md`
- `sonar.md`
- `survival.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`

`quests.md` was requested by the task packet but is absent at project root.

Selected mandates:

- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Batch18 source packets:

- `Docs/Reports/Batch18/1803_FIRST20_ROUTE_BLOCKER_MATRIX.md`
- `Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.md`
- `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md`
- `Docs/Reports/Batch18/1818_MISSION_MARKER_ASSIGNMENT_VISIBILITY_AUDIT.md`
- `Docs/Reports/Batch18/1818_MISSION_MARKER_BINDING_MATRIX.csv`

## Static Findings

1. The first-route proof target is a spectacular semi-open shallow route, not a narrow Copper Wire-only proof lane.
2. Route sockets and future prefab keys are not live marker or gameplay proof.
3. 1818 proves `MissionMarkerSystem` does not fabricate fallback marker art; it requires authored `markerMesh` and `markerMaterial`.
4. 1818 found no production scene/prefab/data reference to `MissionMarkerSystem` script guid `98f551b622676294787aa78593c06504`.
5. 1818 proves first-route quest assets `quest_arrival`, `quest_copper_sample`, and `quest_first_breath` do not statically prove marker targets or fallback marker positions.
6. HUD threat chevron mesh/material are candidate marker art only. Scanner/HUD bindings are not mission marker bindings.
7. Sonar/navigation may be usable, but not omniscient. Mission markers must not reveal exact hidden resource or hazard truth before discovery/sensor evidence.

## Policy Taxonomy

`REQUIRED_MARKER`: A marker route must exist for safety, return, or core progression once the target is known to the player. It may point to a route zone or known anchor, not necessarily an exact object.

`DIEGETIC_CUE_ONLY`: World geometry, lighting, sound, scanner, HUD warning, or route landmark carries the beat. No mission marker is required.

`OPTIONAL_HINT`: A low-clutter cue is allowed after player delay, scanner/sonar confirmation, low oxygen pressure, or route confusion. It must be confidence/stale aware.

`FORBIDDEN_MARKER`: A mission marker would damage exploration, reveal hidden truth, create GPS clutter, or cover a beauty/rest beat. Use environment/instrument cues instead.

`PENDING_DISCOVERY`: Static evidence does not prove a marker target. Future Unity owner must discover or author a target, or write explicit no-marker policy with proof.

## First-Route Policy Summary

See `1824_FIRST_ROUTE_MARKER_TARGET_POLICY.csv` for the machine-readable row list.

Core decisions:

- Safe anchor and return bearing: `REQUIRED_MARKER` after the anchor is discovered or assigned as the player start/safe point.
- Copper/resource route: `REQUIRED_MARKER` to a discovered resource zone or authored search area; exact hidden pickup marker is forbidden before discovery.
- Fabricator/safe pocket: `REQUIRED_MARKER` after discovery because it is the craft/repair/build gate and likely return node.
- Oxygen and pressure: no floating world GPS marker for oxygen itself. Use diegetic HUD, warning cadence, depth/pressure instrumentation, and return-anchor marker when survival pressure rises.
- First hazard: `DIEGETIC_CUE_ONLY` by default. Optional warning zone is allowed only after sensor/world evidence. Exact hidden hazard marker is forbidden.
- Surface look, Aegir horizon, coastline, waterline, calm photic rest, and beauty beats: `FORBIDDEN_MARKER`.
- `quest_arrival` and `quest_first_breath`: `PENDING_DISCOVERY` because 1818 proves no static marker assignment. Future owner must bind route-known targets or prove explicit no-marker behavior.

## Safety-Critical Beats

- Oxygen: must remain readable through HUD/suit warning and return-route marker support. No marker should claim oxygen truth; Survival owns that state.
- Pressure/depth: must remain readable through depth/pressure instrument and route boundary cue. No exact mission GPS for a pressure threshold unless it marks an authored safe descent/retreat zone.
- First return route: required known-anchor marker plus world landmarks.
- First hazard: warning must be fair, but exact hazard marker is forbidden before evidence.
- First craft/use gate: fabricator or route-improvement station marker required after the player has discovered or been introduced to it.

## Beauty And Rest Beats

Markers are forbidden on:

- first surface/Aegir look;
- waterline close-up;
- coastline/wet basalt/foam beat;
- 0-30 m calm photic shallows;
- pure scenic rest windows;
- Aegir/sky/moon composition shots.

These moments must be carried by premium landmarks and instruments only. A marker here would hide weak composition instead of fixing it.

## Landmark Substitutes

Use these as static candidate landmark families for future Unity proof:

- `Route_Anchor` / damaged safe anchor silhouette.
- Coastline, wet basalt, foam ribbons, and waterline.
- Aegir, sky, moons, and horizon framing.
- `Starter_ReefField` biota corridor.
- `SUB_PRESSURE_HULL`, `SUB_PORTLIGHT_*`, `Power_CurrentTurbine`, dock/sub/turbine/industrial traces.
- `Fabrication_Outpost` / `Forward_Fabricator` silhouette and light/noise signature.
- `Route_Frontier` and lower-photic silhouettes.

All are static candidates until a Unity slot proves current renderer/material/object state.

## Scanner, HUD, Sonar Pairing

- Known anchor: HUD bearing/compass and optional world marker.
- Return under oxygen pressure: HUD oxygen/depth warning plus return-anchor marker; pulse urgency follows Survival state.
- Resource search: scanner/sonar confidence arc first; marker resolves to zone after discovery/proximity/sensor confidence.
- Fabricator: mission marker after discovery, supported by machinery light/sound and map/sonar route memory.
- Hazard: hydrophone/audio, environment damage, creature silhouette, pressure/leak warning, or sonar-confidence warning zone. No exact hidden enemy marker.
- Stale or uncertain targets: material opacity/pulse/noise should show confidence. Do not display false certainty.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` scales marker presentation only.

Allowed continuous scaling:

- marker draw radius and fade distance;
- pulse cadence and opacity;
- label detail alpha and secondary telemetry density;
- sonar echo richness around known/discovered targets;
- occlusion dither refinement;
- optional diagnostic/stale confidence visualization;
- update cadence for noncritical cosmetic marker animation.

Forbidden scaling:

- objective truth;
- target precision;
- quest state;
- save identity;
- DTO layout;
- marker authority route;
- hidden resource or hazard reveal rules;
- warning priority or command semantics.

Compact consequence: one primary progression cue plus one safety/return cue at most, clear silhouettes, low animation, no bloom dependence.

Middle consequence: richer confidence/stale display and route-zone hinting, still low clutter.

High consequence: stronger material response, smoother pulse, better depth/occlusion, richer scanner relationship.

Ultra consequence: holographic/cinematic detail around the same marker truth only. No added hidden facts.

## No-GPS-Clutter Rules

1. World landmarks are primary. Mission markers are support.
2. Never display more than one primary objective marker plus one safety/return marker in the first route unless an explicit fail state needs a temporary warning.
3. Do not marker-tag beauty/rest beats.
4. Do not reveal exact hidden copper before discovery or sensor confidence.
5. Do not reveal exact hidden hazard or creature position before evidence.
6. Markers must clear or downgrade on quest completion.
7. Markers must respect depth/occlusion and must not read like cheap screen-space overlays through geometry.
8. Marker labels use baked IDs/zero-GC UI routes only after implementation; this packet does not edit UI code.
9. Stale/uncertain route data must look stale/uncertain.
10. Distance, confidence, urgency, and fade changes need hysteresis to avoid flicker.

## Future Unity-Slot Implementation Sequence

1. Locate or create the single production `MissionMarkerSystem` owner in the route scene/prefab.
2. Bind authored non-placeholder marker mesh and instanced marker material; candidate HUD chevron assets need product approval before use as mission marker art.
3. Add explicit marker policy data for first-route quests or an editor-only validation source. Do not hardcode policy in runtime scripts if designers must tune it.
4. Resolve `quest_arrival`: route-known anchor/orientation target, or explicit no-marker policy if the quest completes before player-visible marker display.
5. Resolve `quest_copper_sample`: discovered resource-zone target around `Node_Copper_A` / `Resource_FieldSources`, not exact hidden pickup unless discovery proof exists.
6. Resolve `quest_first_breath`: safe descent/depth-route target, `Route_Frontier`, or explicit no-marker policy with HUD/depth proof.
7. Bind safe anchor/return cue after start/discovery.
8. Bind fabricator/safe-pocket cue after discovery or quest introduction.
9. Bind scanner/HUD/sonar confidence relationships without using mission marker data as a hidden-truth backdoor.
10. Add editor validation for required-marker quests: marker target, nonzero position, or explicit no-marker policy.
11. Add once-per-session development telemetry for active quest with no marker resource/target. No per-frame log spam.
12. Produce the runtime proof packet below.

## Required Runtime Proof For Future Owner

All items are PENDING UNITY SLOT:

- Serialized scene/prefab diff proving `MissionMarkerSystem` owner and marker mesh/material binding.
- Serialized quest/data diff proving marker target, nonzero position, or explicit no-marker policy for each first-route quest.
- Player route screenshot/video: anchor marker, copper zone cue after discovery/sensor evidence, fabricator marker after discovery, return marker under oxygen pressure, and marker clear on completion.
- No marker on surface/Aegir/waterline/coastline/beauty-only rest beats.
- Occlusion/depth proof: marker does not draw as cheap omniscient overlay through terrain/geometry.
- No overdraw clutter proof in first route.
- Save/load proof if marker activation/completion/discovery state persists.
- Profiler/GC proof over 300+ frames: marker/HUD paths allocate 0 B/frame in hot paths.
- Failure proof: missing target, missing owner, inactive marker system, missing mesh/material, bad material instancing, and UI occlusion/fallback states are visible to development validation.

## Failure Proof Requirements

Future owner must prove these fail closed or fail visibly in development:

- no production `MissionMarkerSystem` owner;
- missing marker mesh;
- missing marker material;
- material instancing disabled;
- active quest has marker-required policy but no target/position;
- target object absent or inactive;
- target exists but is hidden behind invalid scene state;
- marker overlaps critical oxygen/pressure/tool UI;
- marker persists after quest completion;
- marker appears on a forbidden beauty/rest beat;
- exact hidden resource or hazard target appears before discovery/evidence.

## Final Static Classification

POLICY_PACKET_COMPLETE.

Current route marker runtime acceptance remains BLOCKED STATIC / PENDING UNITY SLOT because 1818 proves no static production `MissionMarkerSystem` owner/resource binding and no marker assignment proof for the first-route quest assets.
