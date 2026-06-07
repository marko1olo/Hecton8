# HECTON-8 Quality Gates Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: cross-system acceptance gates, proof artifacts, screenshot review, profiler proof, taste review, low-tier validation, and anti-fake reporting.

## 0. Prime Quality Law

No system is accepted because it sounds good in chat.

Quality means the change improves at least one real product dimension:

- player-visible beauty, readability, or material truth;
- player decision value, route clarity, or gameplay consequence;
- runtime stability, memory safety, or compact-lane survival;
- high-tier sensory richness without changing gameplay truth;
- source authority, proof clarity, or a blocker that directly prevents the above.

Documentation-only work is accepted only when it removes ambiguity, fixes stale authority, routes agents to the correct source, or records a durable proof boundary. Bureaucracy that does not unblock implementation is rejected.

Work-product priority:

- first: source, asset, scene, proof, runtime capture, profiler artifact, player-visible improvement, or blocker removal;
- second: concise proof boundary or command-ready triage;
- rejected: paperwork that substitutes for fixing, capturing, profiling, importing, or proving.

Acceptance requires proof appropriate to the change:

- static scan;
- screenshot;
- render capture;
- Unity Profiler;
- GCMonitor;
- Frame Debugger;
- Memory Profiler;
- generated asset manifest;
- gameplay repro;
- black-box dump;
- low-tier capture.

If proof is missing, status is `PENDING VERIFICATION`.

## 0A. Verification Loop Firewall

Verification is useful only when it answers a named gate and changes the next action. It is process debt when it restates the same missing proof.

Allowed:

- one scoped static scan to locate defects;
- one scoped triage pass to separate blockers from false positives;
- one concise proof-boundary note when Unity, profiler, build, device, or process gates are genuinely blocked.

Rejected:

- repeated static scans over unchanged source or assets;
- new CSVs, boards, packets, or validation summaries that only restate `PENDING VERIFICATION`;
- "green" reports that do not unlock implementation, capture, runtime proof, or a blocker removal;
- old-log mining after the active owner route and missing proof are already known;
- using process-gate blockage as permission to keep writing reports instead of fixing source, preparing the exact proof command, or stopping.

After a check finds a blocker, the next owner action must be one of:

- run the missing proof command or Unity tool when the gate is clear;
- make the source, scene, asset, material, or route-owner change that can make the proof pass;
- mark the task blocked with the exact external blocker and stop that lane.

A third check of the same unchanged state requires explicit user/controller authorization or new evidence. Without that, it is rejected as a verification loop.

## 0B. Paper-Success Rejection

The project rejects "work" that mainly increases artifact count while the player route, source route, visual result, or proof state stays unchanged.

Paper-success signals:

- many docs created, no source/asset/proof changed;
- repeated `PENDING VERIFICATION` summaries with no new command, blocker, or owner action;
- controller boards that multiply tasks instead of deleting, fixing, or proving a blocker;
- "validation" that only checks formatting, rows, or links after the route defect is already known;
- report language that sounds complete while the proof class remains static.

Accepted documentation must do at least one of these:

- remove a stale or conflicting rule that was blocking correct work;
- define a missing acceptance gate that agents will actually hit;
- map a known defect to one owner, one file route, and one proof command;
- preserve a real proof boundary after a tool/process gate blocked the proof run.

If none of these is true, stop writing and return to source, assets, Unity proof, or the concrete blocker.

## 0C. Production Deliverable Lock

Every non-trivial production task must resolve to one primary deliverable class:

- `SOURCE_CHANGE`;
- `ASSET_CHANGE`;
- `CONTENT_ARTIFACT`;
- `FRESH_PROOF`;
- `BLOCKER`;
- `POLICY_DOC` only when the user explicitly requested policy, audit, routing, or rule work.

Support artifacts cannot accept the task by themselves. Static scans, validators, route cards, checklists, summaries, screenshots that only reject a bad state, and report synthesis are useful only when they route the next source, asset, content, proof, or blocker action.

New self-check scripts are accepted only when they catch a concrete repeated failure, include a negative case or reproducible rejected state, map to a named product gate, and do not claim runtime, visual, prose, profiler, or player-readiness proof from static output.

## 1. Universal Review Questions

Every player-facing change must answer:

- What physical fact does this reveal?
- What player decision does this sharpen?
- What fails?
- What remains readable on compact hardware?
- What does high-end add without changing truth?
- What cheaper premium approximation was considered?
- What artifact proves the claim?

If the answer is only "looks cool", reject.

## 2. Screenshot Gate

Every visual/UI/world/asset change needs screenshots when implemented:

- normal tier;
- compact/low tier;
- target aspect ratio;
- 720p or low render scale;
- debug view if relevant: wireframe, colliders, masks, LODs, overdraw, or UI layout.

Reject beauty shots that hide problems.

## 2A. Visual Failure Firewall

Player-visible visual work must not become a repaint loop. If two consecutive fresh captures, or 90 minutes of work, still show the same root failure, the task status becomes `VISUAL_ROUTE_INVALID` and the agent must stop cosmetic iteration.

Root failures include:

- slab, card, or rectangle water;
- terrain that reads as random noise, black clipped silhouettes, low-poly filler, or a colorized heightfield;
- sky, Aegir, moons, clouds, water, terrain, flora, UI, VFX, lighting, or camera below the route bible visual floor;
- waterline/foam/contact/wetness missing or visibly fake;
- diagnostic-only capture presented as product proof;
- temp haze, temp cards, disabled renderers, post-processing, darkness, or color grading used to hide weak source art, binding, geometry, lighting, or route composition.

When `VISUAL_ROUTE_INVALID` is reached, the next action is route recovery, not another color pass:

- identify the authoritative route owner and active scene/material/component binding;
- inspect existing source assets and reference images before generating replacement visuals;
- compare against known-good in-project screenshots before inventing a new polish route;
- decide whether to restore, replace, or remove the broken route;
- fix water/terrain/sky/material/contact/camera foundations before fog, bloom, exposure, grading, vignette, or decorative light changes;
- capture the same shot list again only after root cause changes;
- keep all acceptance claims at `PENDING VERIFICATION` until the screenshots pass taste and the required Unity/profiler proof exists.

Forbid visual task completion if the final evidence is only a diagnostic screenshot, editor-only unsaved probe, dirty-scene capture, temporary object, disabled-renderer A/B shot, or static text report. Such artifacts may reject a result; they cannot accept it.

The controller must terminate or re-scope a visual owner that keeps producing same-failure captures. More boards, CSVs, task packets, or rationale prose are process debt unless they directly name the root route change and the next proof command.

## 3. Performance Gate

Runtime claims require:

- before/after frame time;
- GC allocation;
- memory/VRAM delta if assets or RTs changed;
- load-shed path;
- exact scene/repro;
- hardware/tier statement.

Any single feature over 0.1 ms is suspicious until proven. Treat this as a profiler triage threshold, not an automatic rejection and not a reason to reduce visual quality, route readability, or gameplay value.

## 3A. GlobalQualityWeight Gate

Every runtime or visual system must describe how `GlobalQualityWeight` affects fidelity, cadence, capacity, diagnostic depth, and load shedding. It is a continuous scalar, not a low/high switch.

Reject if `GlobalQualityWeight` changes gameplay truth, save identity, DTO layout, authority route, command semantics, hitbox/collider truth, item economy truth, or public claim state. High and Ultra are allowed to buy richer sensory detail only after Compact remains readable and the owner route is stable.

## 4. Taste Gate

Taste proof requires checking against:

- `taste.md`;
- relevant system bible;
- low-tier readability;
- screenshot consequence;
- Deep Sea Noir/NASA-punk identity;
- anti-generic rejection list.

If the work could belong unchanged in a generic sci-fi survival game, reject.

## 5. Generated Asset Gate

Generated assets must prove:

- mesh validation;
- UV/texture validation;
- material route;
- LOD chain;
- collision proxy;
- render proof;
- low-tier proof;
- manifest path.

No generated asset enters production because it merely exists.

## 6. UI Gate

UI must prove:

- task clarity in 3 seconds;
- no text clipping;
- localization expansion;
- color roles;
- input navigation;
- zero-GC hot path;
- low-tier screenshot;
- no decorative-only graphics.

## 7. Gameplay Gate

Gameplay must prove:

- player decision;
- physical consequence;
- failure evidence;
- resource/resource-free justification;
- save/authority ownership;
- no hidden hot polling;
- no binary quality switch.

## 8. Audio Gate

Audio must prove:

- information carried;
- priority/mix behavior;
- no spam;
- low-tier path;
- no hot-path allocations;
- cue IDs not runtime strings.

## 9. Controller And Batch Prompt Gate

Controller prompts, batch prompts, task files, and old logs are routing aids, not authority to lower the game.

Reject or patch a controller/batch instruction if it:

- demands bulk reading of unrelated root docs, archives, or old logs for a narrow task;
- demands `Status_[ID].md`, `Rationale_[ID].md`, or `LOG_[ID].md` without an explicit active agent ID or logging mode;
- tells agents to delete deprecated paths immediately without scoped proof, `.meta` handling, and rollback awareness;
- downgrades the visual target to "visually acceptable";
- treats approximation-first rendering or the 0.1 ms suspicion rule as permission for flat water, muddy skies, weak terrain, blurry textures, primitive meshes, or placeholder-looking assets;
- contains absolute implementation bans without profiling, capture, platform, or domain context;
- asks for fake microseconds, fake proof, or proof-label upgrades not backed by artifacts;
- omits the surface/photic-shallows/medium-depth Subnautica-level visual floor for visual work.

A valid controller prompt preserves inspect-first autonomy, the relevant root bibles, exact proof labels, three-pillar acceptance, continuous `GlobalQualityWeight`, active-ID logging rules, and the requirement that cheap paths still look premium.

For serious user-distributed HECTON-8 agent waves, 20-30 task prompts are valid and preferred when they are phase-gated, evidence-based, and domain-owned. Reject large prompts only when they broaden scope without proof, create sibling dependencies, fake metrics, or skip checkpoints.

Controller prompts must also preserve `VISION_LOCKS.md`: spectacular semi-open first route, broad parallel production, cozy but industrial bases, Subnautica-level visual floor, useful Subnautica-like map/sonar, honest 15-locale status, and the user's current stance on modding/XR/platform foundations.

## 10. Final Report Shape

A valid report says:

- what was wrong;
- what changed;
- in-game result;
- what was verified;
- what remains `PENDING VERIFICATION`;
- exact files;
- exact proof artifact or explicit absence.

Reports that claim quality without evidence are rejected.

## 11. Proof State Labels

Every report must use one of these labels:

- `STATIC VERIFIED`: source, docs, manifests, or generated files inspected; no runtime claim.
- `EDITOR VERIFIED`: Unity Editor path executed; compile/import/basic editor behavior proven.
- `PLAYMODE VERIFIED`: Play Mode repro executed; runtime behavior observed.
- `PROFILER VERIFIED`: Unity Profiler/GC/Frame Debugger/Memory Profiler artifact exists.
- `PLAYER-CAPTURE VERIFIED`: screenshot/video from target gameplay scenario exists.
- `PENDING VERIFICATION`: claim is plausible but not proven.

Do not collapse these labels. Static documentation work cannot claim runtime proof.

Evidence class mapping:

- Proof state label is the report status.
- Evidence class is the artifact type behind the status, such as `STATIC_DOC`, `STATIC_SOURCE`, `PACKAGE_LOCK`, `CLI_COMPILE`, `UNITY_CONSOLE`, `PLAYMODE`, `PROFILER`, `FRAME_DEBUGGER`, or `PLAYER_BUILD`.
- `STATIC VERIFIED` may be backed by `STATIC_DOC`, `STATIC_SOURCE`, package/manifests, generated files, or static tool output only. It must not imply Unity import, runtime behavior, profiler, GC, visual, platform, or save/load proof.
- `EDITOR VERIFIED`, `PLAYMODE VERIFIED`, `PROFILER VERIFIED`, and `PLAYER-CAPTURE VERIFIED` require the matching runtime/editor artifact class and path.
- If the proof state label and evidence class disagree, use the lower claim and mark the rest `PENDING VERIFICATION`.

## 12. Domain Proof Matrix

Minimum proof by domain:

- Generated asset: manifest, validation report, render screenshot, LOD/collider proof.
- UI/menu/HUD: low-resolution screenshot, localization expansion, input navigation, zero-GC route.
- Settings/configuration (`settings.md`): setting schema, owner/range/default, persistence proof, invalid value clamp, apply timing, rollback/fallback proof for display-sensitive options.
- Localization/text (`localization.md`): string ids, font atlas coverage, expansion/RTL/CJK or fallback note, zero-GC hot text proof if runtime, missing-key behavior.
- Input/haptics (`input.md`): action id map, device matrix, forbidden API scan, rebinding conflict proof, UI navigation proof, haptic priority map, GC proof for runtime claims.
- Camera/capture (`camera.md`): gameplay capture, compact capture, route/interaction visibility proof, shake cause list, comfort note, capture truth label.
- Sonar/navigation (`sonar.md`): sensor source list, confidence/staleness rules, active ping cost/reaction route, map reveal persistence if used, compact sensor UI proof, no free omniscience.
- Water/current/flooding presentation: owner route, field/fill manifest, Compact and High capture, profiler/GC/GPU proof if runtime code or render passes changed.
- Survival/damage physiology (`survival.md`): channel formulas, owner route, failure/recovery states, compact UI proof, black-box fields, save/load proof where persistent.
- Combat/damage contact (`combat.md`): damage packet schema, hitbox/proxy map, resistance/penetration table, damage channel owner route, black-box fields, profiler proof when runtime contact changed.
- Atmosphere/weather (`atmosphere.md`): macro field manifest, owner route, cadence, affected-system list, Compact and High capture, profiler/GPU proof if runtime fields or render passes changed.
- Celestial/macro cycles (`celestial.md`): phase model, cadence, event list, consumer owner list, save/load phase proof if persistent, compact route/visibility impact.
- Terrain/biome/scatter (`terrain.md`): terrain/biome mask manifest, deterministic seed route, slope/traversal classification, scatter density caps, collision/proxy/SDF proof, compact and high captures.
- Ecosystem/biome simulation (`ecosystem.md`): biome ecology table, spawn masks, biomass cadence, fauna/flora roles, deterministic seed, compact/normal captures, persistence/profiler proof if runtime.
- Rendering/VFX: Frame Debugger or RenderGraph proof, target-tier screenshot, profiler cost.
- Lighting (`lighting.md`): light source manifest, owner/state list, compact no-black-screen capture, shadow eligibility, Frame Debugger/RenderGraph/profiler proof for runtime changes.
- VFX (`vfx.md`): effect cause owner, pool capacity/overflow policy, spam prevention, compact and normal captures, profiler/GPU/memory proof where runtime or flipbooks changed.
- Shaders/material runtime (`shaders.md`): shader family contract, texture/channel map, keyword/variant count, SRP Batcher/instancing note, Frame Debugger/GPU proof for costly features.
- Performance/zero-GC (`performance.md`): profiler marker, before/after frame time, GC allocation proof, memory/VRAM proof, load-shed route, arena/native lifetime proof where relevant.
- GPU compute (`compute.md`): kernel `numthreads`, buffer layout, dispatch group proof, barrier/readback route, GPU capture or explicit pending runtime proof.
- Gameplay/tool/construction: repro steps, ownership route, save/authority note, failure evidence.
- Logistics/power networks (`logistics.md`): network family/unit table, node/edge schema, producer/consumer list, graph cadence, failure thresholds, save/load and profiler proof where runtime.
- Drones/automation (`drones.md`): role/task contract, command queue schema, owner route for actions, active count/cadence, docking/charging/failure state, save/profiler proof where runtime.
- Physics/vehicles: collider/proxy proof, force ownership, fixed-tick route, profiler when runtime changed.
- AI/creatures: active counts, tick cadence, stimulus cause, Director/token state, black-box fields.
- Networking/rollback (`networking.md`): authority class, packet layout, AUP wire proof, loopback/reconciliation proof, rollback/Merkle/desync proof before any multiplayer claim.
- Bootstrap/startup (`bootstrap.md`): boot state list, dependency route table, data monolith readiness if touched, non-reload transition proof, native allocation/disposal proof, startup fault behavior.
- Authoring/data bridge (`authoring.md`): source path, schema hash, validation report, h8bin output, atomic write/readback proof, explicit runtime parser absence.
- Inventory/economy (`inventory.md`): item id/schema list, recipe/source table, storage rules, save/load proof if persistent, UI snapshot, compact readability, no string/hot allocation route.
- Streaming/persistence/data: memory/save artifact, schema/version, checksum or ledger, leak/load proof.
- Math/determinism/AUP (`math.md`): coordinate representation, shift generation/fence proof, deterministic seed route, replay proof, CI suppression artifact if banned tokens remain.
- Telemetry/black-box: schema, record size, capacity, owner phase, dump trigger, sample manifest or explicit pending runtime proof.
- Modding/SDK/API (`modding.md`): static validator output, schema/audit matrix agreement, envelope owner route, accepted/rejected command proof, starter kit validation/review manifest proof, explicit runtime verification state.
- Platform/hardware (`platform.md`): target device, player build or launch artifact, profiler/GC/memory/VRAM proof, shader/input/storage proof where relevant.
- XR/VR (`xr.md`): headset/provider, launch/device proof, frame pacing, input binding, UI readability, comfort settings, foveation/stencil capability where claimed.
- Release readiness: build/import proof, Play Mode or player proof, profiler/GC/memory proof, save/load proof where relevant, platform/device proof for platform claims.
- Testing/CI (`testing.md`): evidence class, command/tool, target scene or repro, artifact path, timestamp, unresolved failures, and no static-proof upgrade for runtime claims.
- Cinematics/capture (`cinematics.md`): sequence purpose, truth label, owner state list, entry/exit/interrupt rules, compact capture, control-loss duration, public-claim review if used externally.
- Accessibility: low-tier readability capture, remapping/subtitle/flash/motion checks.
- In-world writing (`writing.md`): canon sources, speaker/source, surface type, unlock context, evidence object, LocID/runtime layer, English authority text, 15-locale draft rows with status or explicit English-only scope, anti-AI prose scan, forbidden facts avoided, and native-review/runtime status.
- Public writing: attached proof asset or explicit no-asset housekeeping reason, unsupported claims removed, channel target, owner approval state for Steam/demo/release/platform/access claims.

If the domain has no matching proof artifact, it remains pending.

## 13. Release-Grade Packet

For release-facing work, a final handoff is valid only if it contains:

- route bibles read;
- owner/truth path;
- affected files;
- player-visible result;
- Compact, Middle, High, and Ultra scaling note when runtime or visuals changed;
- proof state label;
- proof artifact paths or explicit `PENDING VERIFICATION`;
- rejection gates checked;
- known remaining risks.

Do not write "AAA", "production ready", "release ready", "optimized", or "done" unless the packet contains proof for that exact claim.

## 14. Acceptance Sentence

A change is accepted only when the relevant bible route was read, the owner and truth path are explicit, the compact-tier result remains readable, the high-tier path adds sensory value without changing truth, and proof artifacts exist for every runtime claim.
