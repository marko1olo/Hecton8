# HECTON-8 Night Operating Plan 2026-06-05

Status: ACTIVE / USER APPROVED AUTONOMOUS ALL-FRONT EXECUTION
Mode: MASSIVE FRONT / CONTINUE UNTIL USER STOPS THE ORCHESTRATOR

## Objective

Recover HECTON-8's first surface/photic product face from rejected diagnostic visuals into a manifest-bound proof lane, while preparing the scene for real water, shoreline terrain, Aegir/sky, underwater volume, route cues, full player-facing UI, full swimming/walking/movement proof, and later controlled object/prefab/flora/coral placement.

Target proof candidate:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

## Non-Negotiable Standard

Current visuals are not production-adjacent. They are rejected.

The night plan must not protect bad work. It must identify, cut, replace, or quarantine:

- black primitive slabs/boulders;
- flat green water sheets;
- fake underwater labels;
- muddy Aegir outputs;
- broken foam strips;
- unsupported caustic sheets;
- terrain with no wet geology;
- plants/coral/rocks/prefabs that look dumped, primitive, floating, unscaled, or unintegrated;
- "UI on" or "player movement works" claims without real runtime/controller evidence.

## Work Lanes

### Lane 0 - Orchestrator Control

Owner: primary orchestrator.

Actions:

1. Maintain this memory file and append concise state changes.
2. Keep at least 5 independent fronts moving after approval.
3. Use GUI Codex agents for long tasks.
4. Use local subagents for bounded audits.
5. Never over-monitor one Unity owner while other work is available.
6. Preserve proof labels: static is static, runtime is runtime.

Approval deliverables:

- active front statement;
- launched agents list;
- proof read;
- rejected/accepted evidence;
- next action.

### Lane 1 - Unity Scene Diff Owner

Purpose: stop the dirty scene from poisoning all future proof.

Candidate GUI agent ID: `3101`.

Owned scope:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
- quarantine metadata under `Docs/Screenshots/MCP`

Tasks:

1. Inspect Unity/process state first.
2. Do not build while import/compile/shader work is active.
3. Review `93725` line scene diff by object categories.
4. Separate direct 1912 quarantine changes from prior/concurrent scene churn.
5. Identify renderer disables, active-state changes, camera/sun changes, prefab/fileID churn.
6. Produce per-object decision matrix: restore, keep disabled, replace, delete candidate, needs Unity visual review.
7. Do not perform destructive scene revert without approval.
8. If Unity slot is free, use Unity API/editor readback, not raw YAML mutation, for scene inspection.

Proof:

- static diff report;
- Unity editor readback if available;
- no runtime acceptance claim.

### Lane 2 - Proof Harness `1475`

Purpose: replace raw screenshot chaos with manifest-bound proof.

Candidate GUI agent ID: `3102`.

Owned scope:

- `Tools/ProofGate`
- future editor proof harness under `Assets/_Project/Scripts/Editor/Proof/`
- output under `Docs/Screenshots/HectonProofPackets/`

Tasks:

1. Read Batch30 `3002` spec and ProofGate code.
2. Build or prepare reusable harness route.
3. Ensure diagnostics do not save production scenes.
4. Ensure six exact production filenames.
5. Emit manifest, checksum, log copy, route/depth/UI predicates.
6. Add or update tests only for proof tooling.
7. Run ProofGate unit tests if CPU/build state allows. This is Python, not dotnet build.

Proof:

- static tool tests;
- generated sample packet only if Unity slot allows;
- no visual acceptance without human review.

### Lane 3 - Water / Crest / Foam / Caustics

Purpose: make ocean and underwater water credible, not green sheets.

Candidate GUI agent ID: `3103`.

Owned scope:

- Crest material route audit and safe assignment plan.
- `HectonUnderwaterVisuals`
- water/foam/caustic materials and shaders already in project.

Tasks:

1. Verify active surface route is `Ocean.mat`.
2. Verify underwater owner material and runtime overwrite path.
3. Reject raw curtain/slab/green sheet routes.
4. Prove or prepare Crest foam route, not just transparent ribbon.
5. Prepare shallow caustic fake with light/depth/receiver gating.
6. Define compact/middle/high/ultra scaling through continuous `GlobalQualityWeight`.
7. Prepare Unity-owner changes only after scene diff risk is controlled.

Proof:

- material route report;
- Frame Debugger/profiler pending until Unity proof.

### Lane 4 - Shoreline / Terrain / Wet Geology

Purpose: replace black primitive shoreline with real wet geology and readable waterline.

Candidate GUI agent ID: `3104`.

Owned scope:

- terrain materials;
- generated asset candidates under `Docs/GeneratedAssets`;
- shoreline mesh/prefab candidates;
- no direct production import until QA passes.

Tasks:

1. Inventory existing basalt/rock/shell/sand textures and materials.
2. Reject failed Gemini tiles already marked reject.
3. Generate or request new wet basalt, shell/sand, foam/salt/wet-contact, caustic receiver sources in `Docs/GeneratedAssets`.
4. Run texture intake QA.
5. Build PBR package plan: albedo, normal, MRAO/wetness/salt masks.
6. Identify current black foreground objects and replacement candidates.
7. Prepare terrain/mesh placement plan with LOD/collider requirements.

Proof:

- QA reports;
- contact sheets;
- no Unity import claim until later.

### Lane 5 - Aegir / Sky / Celestial Surface

Purpose: make Aegir and sky match mandatory examples instead of muddy sticker sphere.

Candidate GUI agent ID: `3105`.

Owned scope:

- `Mat_HectonSky`
- active Aegir materials/textures/shaders
- celestial owner routes

Tasks:

1. Verify one active Aegir owner.
2. Verify missing cloud/sky slots.
3. Reject flat mesh sun quick fix.
4. Choose texture-driven Aegir route using existing 4K/2K candidates or generate source textures outside `Assets`.
5. Define long-view and crop proof requirements.
6. Preserve `PrimarySunDiscOwner=SkyMaterial`.
7. Prepare controlled material tuning plan, not random parameter pushing.

Proof:

- static route report;
- visual proof pending Unity capture.

### Lane 6 - Underwater Route Volume

Purpose: create real 0-5m and 20-50m proof routes.

Candidate GUI agent ID: `3106`.

Owned scope:

- underwater route anchors;
- `HectonUnderwaterVisuals`;
- particle/silt/volume candidates;
- scene route object placement plan.

Tasks:

1. Define actual underwater camera anchors.
2. Define return cue and route silhouette requirements.
3. Check current missing motes/snow/bubbles/beams references.
4. Prepare shallow 0-5m route with visible surface refraction and terrain.
5. Prepare 20-50m route with volume, silhouettes, particles, route landmark, danger/return cue.
6. Use mandatory `photo_1/photo_2` reference traits.
7. Avoid global decorative particle noise.

Proof:

- route/anchor plan;
- Unity screenshot pending.

### Lane 7 - Product Face Object / Prefab Placement

Purpose: after base water/terrain/sky route is no longer trash, place actual rocks, flora, coral, debris, route hardware, and scale witnesses.

Candidate GUI agent ID: `3107`, staged later.

Owned scope:

- first route scene objects;
- prefabs under first-party project paths;
- generated/procedural asset outputs that pass QA.

Rules:

1. Do not decorate broken water/terrain. Fix base route first.
2. Every placed object needs purpose: route, scale, ecology, salvage, threat, return cue, or material witness.
3. Reject floating blades, detached coral bulbs, primitive rocks, nonmatching scale, bad LOD, no collider/proxy plan.
4. Near-field rocks/flora/coral need material truth and silhouette quality.
5. Compact keeps fewer objects but strong composition; High/Ultra add density and longer LOD residency.

Proof:

- scene screenshots;
- object list;
- LOD/collider/material notes;
- visual rejection notes.

### Lane 8 - First-20 Gameplay / Instrument Stake

Purpose: proof frames must show player-facing game, not empty vista.

Candidate GUI agent ID: `3108`.

Owned scope:

- first 20 minutes route brief/contract;
- HUD/instrument proof;
- route cue object proposals;
- salvage/machinery/threat cues.

Tasks:

1. Read first 20 route docs.
2. Define what player decision each proof view sharpens.
3. Add or plan visible oxygen/pressure/route/instrument stake.
4. Avoid pure beauty shot acceptance.
5. Keep scenic rest allowed but not empty.

Proof:

- route stake matrix;
- UI/runtime proof pending.

### Lane 9 - Full UI / Player Movement / Control Proof

Purpose: HECTON-8 must become a playable product slice, not a screenshot set.

Candidate GUI agent ID: `3109`.

Owned scope:

- player movement contracts and existing movement systems;
- input abstraction and device path;
- swimming, walking/shore/interior movement, camera, interaction visibility, and movement state proof plan;
- visor/HUD/survival UI route and real UI-on proof requirements;
- no runtime edits until code owners and existing APIs are audited.

Tasks:

1. Read `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `player.md`, `input.md`, `camera.md`, `gameplay.md`, `survival.md`, `tools.md`, and matching mandates.
2. Inspect existing code before proposing implementation: player movement, input dispatcher/state, exosuit kinematics, camera juice, visor HUD, survival HUD, UI registries.
3. Build a current-state matrix: implemented, present-but-unwired, fake/diagnostic, missing, forbidden.
4. Define minimum playable control slice: walk, swim, ascend/descend, surface/shore transition, look/camera, interact, tool aim, UI focus, pause/PDA route.
5. Define full UI proof: oxygen, depth, pressure, route cue, tool/interaction prompt, system warning, PDA/visor/cockpit distinction, no fake filename-only UI.
6. Produce implementation order that preserves zero-GC, owner-local truth, input snapshot ownership, and diegetic UI law.
7. State Low / Middle / High / Ultra consequences using continuous `GlobalQualityWeight`.
8. Do not invent new public APIs unless existing contracts prove a gap; propose wrappers or owner-local expansion only.

Proof:

- static codebase audit first;
- runtime movement/UI proof pending Unity owner;
- profiler/GC proof mandatory before acceptance.

### Lane 10 - Lore / World Consistency

Purpose: keep broad production moving while Unity is busy.

Candidate GUI agent ID: `3110`, optional.

Owned scope:

- lore/world text only if assigned;
- evidence objects and route context;
- no runtime claims.

Tasks:

1. Tie first route objects to Deep Reach/Marauder/salvage/evidence logic.
2. Produce concise object/story briefs for placed machinery/debris.
3. Avoid lore walls and generic evil corp language.
4. Use `writing.md`, `narrative.md`, `localization.md` if writing in-world content.

Proof:

- static text artifacts only.

## Local Subagent Plan

Use subagents for narrow bounded work after approval:

- Reference Comparator: produce a table comparing mandatory examples against current frames.
- Scene Object Classifier: parse quarantine object list and categorize visual trash vs route candidate.
- Texture QA Scout: inspect existing texture candidates and Gemini outputs.
- Aegir Source Scout: inventory all gas giant/cloud textures and dimensions.
- ProofGate Scout: summarize validator failure modes and required fields.
- Process Watchdog: sample Unity/dotnet/shader compiler state before build or Unity-heavy work.
- Player/UI Codebase Scout: map real movement/input/HUD owners and current violations.

Subagent output is advisory. Primary orchestrator integrates and judges.

## GUI Codex Agent Launch Protocol

After approval:

1. Create `taskslocal/batch31_night_visual_recovery/`.
2. Write `BATCH_INDEX.txt`.
3. Write self-contained task files for `3101` to `3109`, plus optional `3110`.
4. Use VS Code Codex GUI new chats.
5. Launcher message includes:
   - explicit ID;
   - task file path;
   - read `AGENTS.md`, `PROJECT_BIBLES.md`, `TASTE.md`, `VISION_LOCKS.md`, route bibles;
   - update Status/Rationale/LOG because ID is explicit;
   - no fake runtime proof;
   - no Unity contention unless assigned.
6. Start at least 5 independent agents if system load allows, up to 10 GUI agents when the local machine is not compiling/importing.
7. Monitor blue-circle states.
8. Send dobivka prompts only after reports or clear violations.

## Night Phases

### Phase A - Recovery And Task Dispatch

- Refresh process state.
- Write batch31 task files.
- Launch first 5 GUI agents:
  - 3101 scene diff owner
  - 3102 proof harness
  - 3103 water/Crest
  - 3104 shoreline/terrain
  - 3105 Aegir/sky
- Spawn local subagents for reference compare and process watchdog.
- Include 3109 in the first launch wave if UI/player codebase audit can run read-only while Unity is busy.

### Phase B - Static Closure

- Read early reports.
- Reject fake completion.
- Build synthesis for Unity owner.
- Prepare Unity actions only when compiler/import load clears.

### Phase C - Unity Controlled Work

- One Unity owner at a time.
- Inspect/repair scene diff.
- Remove or restore diagnostic trash.
- Apply water/sky/terrain fixes only after owner routes are clear.
- Capture interim diagnostic screenshots but do not accept raw PNG proof.
- Validate player movement and UI only through runtime evidence, never through file names or editor-only widgets.

### Phase D - Asset Generation And QA

- Generate or request texture sources outside `Assets`.
- QA all candidates.
- Import only reviewed candidates through Unity owner.
- Reject AI baked lighting, seams, repeated hero shapes, false PBR.

### Phase E - Object / Prefab / Flora / Coral Placement

- Only after base visual route is not garbage.
- Place rocks, plants, coral, debris, route hardware, and scale witnesses.
- Audit each placement from gameplay camera.
- Cut anything that reads primitive, floating, dense-noise, unscaled, or below floor.

### Phase F - Manifest Proof Packet

- Produce `h8_1475+` packet.
- Run ProofGate.
- Human visual review against mandatory examples.
- If rejected, write exact reject steer and repeat.

## Kill Conditions

Stop or block a lane if:

- it attempts to accept raw screenshots;
- it saves diagnostic scene changes without owner review;
- it launches build during Unity/import/compiler load;
- it uses darkness/fog to hide weak art;
- it imports failed/generated textures into production;
- it creates sibling dependencies inside same wave;
- it changes public API without proof/approval;
- it deletes Unity assets without `.meta` handling and reference proof.

## User Approval State

User approved autonomous all-front work and explicitly ordered continuous work until stopped.

Execution rule:

- keep independent fronts moving;
- use subagents and GUI agents where available;
- repeat checks and rejection passes;
- do not claim acceptance without proof;
- do not run Unity/build-heavy work during compile/import/shader/process contention.
