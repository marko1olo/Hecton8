# Three-Pillar Acceptance Matrix - 2026-06-05

ID: `QUALITY_OWNER_01_THREE_PILLAR_ACCEPTANCE_MATRIX_WRITER`
Status: `PENDING_VERIFICATION / REJECTED_FRONTS_PRESENT`
Evidence class: `STATIC_DOC + STATIC_SOURCE_SCAN + STATIC_PREFAB_YAML + STATIC_REPORT_SYNTHESIS + USER_CONTEXT_PROCESS_STATE`
Runtime proof: absent.
Unity proof: absent.
Profiler/GC/memory proof: absent.
Scene, prefab, material, import, build, and `Assets/` mutation: none.

## Scope

This controller matrix joins graphics, optimization, and gameplay proof requirements for the active fronts. It does not accept any front. It exists to prevent static reports, raw screenshots, or controller prose from being promoted into runtime readiness.

First-20 route blocker removed: false promotion of the first exit, surface read, swim lane, HUD/player route, product-face assets, world dressing, and proof tooling before the three pillars have current artifacts.

CSV companion: `Docs/Reports/QualitySystem_20260605/THREE_PILLAR_ACCEPTANCE_MATRIX_20260605.csv`.

## Authorities Used

Mandates followed:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

Acceptance boundaries read from root docs:

- `quality.md`: missing proof means `PENDING VERIFICATION`; static documentation cannot claim runtime proof; runtime claims require Unity/profiler/GC/frame/debugger/memory/gameplay artifacts.
- `quality.md`: acceptance requires the owner/truth path, compact readability, high-tier sensory value without truth changes, and proof artifacts for every runtime claim.
- `TASTE.md`: surface, sky, Aegir, moon silhouettes, coastline, ocean surface, and photic shallows must be bright, legible, detailed, beautiful, and at least Subnautica-level for readability and density.
- `TASTE.md`: darkness/fog/post cannot hide weak assets; UI must expose real decisions; optimization that buys nothing visible is rejected.

Named evidence used:

- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.csv`
- `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md`
- `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.csv`
- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md`
- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.csv`
- `taskslocal/world_system_20260605/WORLD_OWNER_01_ROCK_FLORA_CORAL_PLACEMENT_STAGING_PACKET.md`
- `quality.md`
- `TASTE.md`

## Evidence Rules

- `STATIC_DOC`, `STATIC_SOURCE_SCAN`, `STATIC_PREFAB_YAML`, and `STATIC_IMAGE_QA` can reject or stage work. They cannot accept runtime, visual, profiler, GC, memory, scene wiring, build, or player-readiness claims.
- Raw `Docs/Screenshots/MCP/*.png` screenshots are diagnostic context only. Acceptance needs the canonical `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` packet with manifest, checksum, Unity log, console export, readback reports, dirty-state audit, Frame Debugger/Stats, and required screenshots.
- Current product-face visuals are rejected by existing evidence.
- Player movement/UI is static-audited only.
- World placement is staged only and blocked until base proof exists.
- Audio P0 is readback-pending.
- Unity tooling/process gate is red from user context and lacks a clean process-gate artifact in the read evidence. Any Unity proof run is blocked until the gate is clean.

## Acceptance Matrix

| Front | Graphics proof required | Optimization proof required | Gameplay proof required | Current evidence | Reject if | Next owner | Status |
|---|---|---|---|---|---|---|---|
| Sky / Aegir | Canonical `h8_1475_surface_sky_aegir_ocean_hud_game.png`, `h8_1475_sky_aegir_slots_inspector.png`, and optional Frame Debugger view showing active sky/Aegir route, premium cloud/band detail, limb atmosphere, and no duplicate/stale owner. | Frame Debugger/Stats for sky/ocean/terrain pass, shader/material slot readback, no bloom/fog/darkness camouflage, compact and high capture cost boundaries. | First-exit route context: Aegir/sky helps orientation and scale without changing route truth; HUD remains readable against sky/ocean. | `STATIC_IMAGE_QA + STATIC_DOC`: visual rejection says Aegir is smeared/muddy/toy-like; `h8_1475` packet absent. | Muddy pasted sphere, sine stripes, low-res bands, stale/null/ignored slots, second sun/backdrop owner, surface gloom hiding weak art, raw MCP PNG substitution. | Sky/Aegir owner + Rendering owner + Unity proof owner | `REJECTED / H8_1475_PROOF_PENDING` |
| Ocean / Surface Water | Canonical surface and underwater water views: `h8_1475_surface_sky_aegir_ocean_hud_game.png`, `h8_1475_underwater_0_5m_route_game.png`, `h8_1475_crest_ocean_slots_inspector.png`; water reads as volume with refraction, surface ceiling, depth falloff, foam/contact contribution, and route visibility. | Frame Debugger/Stats for Crest/ocean, VRAM/texture residency proof, no Crest material clone/wrapper, compact and high proof, 0 B/frame for runtime VFX/control paths if touched. | Player can read swim route, surface/underwater transition, return path, oxygen risk, and route cues without full-screen haze. | `STATIC_IMAGE_QA + STATIC_DOC`: current water reads as green/teal sheet/slab; Crest proof packet absent. | Flat green/blue slab, black water, empty fog, repeated foam, no seabed/ceiling read, artist textures bound into Crest wave-data slots, missing active OceanRenderer readback. | Ocean/Crest owner + Underwater VFX owner + Unity proof owner | `REJECTED / H8_1475_PROOF_PENDING` |
| Shoreline / Terrain | Canonical `h8_1475_surface_shoreline_waterline_game.png`, `h8_1475_photic_terrain_route_game.png`, `h8_1475_terrain_material_slots_inspector.png`; wet geology, strata, sediment, scale, material masks, waterline contact, and non-primitive terrain source. | Frame Debugger/Stats for terrain material route, texture import/compression/residency proof, SetPass/batches, shadow eligibility, compact/high captures, no post camouflage. | Shoreline supports first exit, return-path memory, safe/unsafe traversal read, and resource/hazard approach decisions. | `STATIC_IMAGE_QA + STATIC_DOC`: current coastline/terrain reads as crushed dark silhouette, slick noisy blob, and weak waterline; runtime proof absent. | Terrain hidden by darkness/fog, noisy slope, primitive blob, blurry material, null/stale PBR slots, random scatter camouflage, no close shoreline proof. | Terrain/geology owner + Material owner + Unity proof owner | `REJECTED / H8_1475_PROOF_PENDING` |
| Underwater Route Density | Canonical `h8_1475_underwater_0_5m_route_game.png`, optional `h8_1475_underwater_20_50m_route_game.png`, and photic route capture showing cliffs/shelves, flora/coral, sparse particles, fauna silhouettes where relevant, negative space, and return cues. | Profiler/GC/GPU proof for VFX/scatter if runtime, Frame Debugger/Stats for density, overdraw control, LOD/instancing proof, compact/high density comparison. | Route density must sharpen oxygen planning, hazard approach/avoidance, resource/tool affordance, return path, and orientation. | `STATIC_IMAGE_QA + STATIC_DOC`: current underwater proof called catastrophic/empty; previous 20-50m capture rejected as invalid/mislabeled. | Empty slab water, full-screen snow/haze, random coral carpet, no player decision, no return cue, no biota/terrain density, invalid underwater label. | Underwater VFX/source owner + World route owner + Ecology/flora/fauna owner | `REJECTED / H8_1475_PROOF_PENDING` |
| Product-Face Prefabs / Materials | Inspector/readback proof for product-face primitive targets, material blocker rows, LOD/collider source, PBR role separation, and canonical screenshots showing no visible primitive/blockout/default/proxy/null material routes. | Import setting proof, texture memory delta, material variant/SRP Batcher proof, LOD transition proof, no runtime material clones, compact/high visual proof. | Product-face assets must support real player verbs: tool use, transport, scan, resource read, cockpit/instrument trust, and salvage choices. | `STATIC_DOC`: visual rejection cites material/texture gate failed, prefab quality gate failed, sky/ocean source primitive gate failed; h8_1475 proof absent. | Built-in primitive visible route, blockout mesh, package-default/proxy/null materials, `foam.png` final use, missing PBR roles, no LOD/collider proof, material edits without Unity readback. | Product-face prefab owner + Material/texture owner + AssetSystem controller | `REJECTED / PRODUCT_FACE_SOURCE_BLOCKED` |
| Player Walk / Swim Movement | Gameplay-height capture of walk/swim/surface/dive route, production player binding screenshot, movement/camera owner readback, and route readability screenshots across required aspect ratios. | 300-frame and 60-second profiler/GC proof for input, movement, motor, camera, interaction; black-box dump manifest for player kinematics/input; no active shell direct-input route. | Movement must prove walk/swim/ascend/descend/strafe, oxygen/depth response, interaction approach, return route, save/load state restore, and no camera/UI ownership of gameplay truth. | `STATIC_SOURCE_SCAN + STATIC_PREFAB_YAML`: production anchors exist; shell controller and active route remain unproven; no Unity/runtime/build/profiler claim. | Active `HectonWorldShellController1428`, direct `Input.Get*`/Keyboard/Mouse shell path, transform truth writes by shell, missing dispatcher registrations, no 0 B GC proof, no save/load proof. | Runtime readback owner + Player movement owner + Input owner | `PENDING_VERIFICATION / STATIC_ONLY` |
| HUD / UI | Canonical HUD/player binding screenshots, `Suit_HUD_Canvas` and `HUD_Internal` readback, low-res/aspect screenshot matrix, and readable oxygen/pressure/power/signal/tool state in route context. | HUD stress profiler/GC proof for text updates and prompt spam, `SetCharArray` active route proof, Canvas rebuild/visibility proof, no `SetActive`/string churn in active hot path. | UI must expose decisions: oxygen, pressure, route, signal, trust, hull, tool, warnings, PDA/pause/rebind state; no fake telemetry. | `STATIC_SOURCE_SCAN + STATIC_PREFAB_YAML`: `SetCharArray` anchors found; `HUD_Internal.forceScreenSpaceOverlay: 1` suspect; active route unproven. | Flat generic overlay accepted as cockpit proof, fake/decorative telemetry, unreadable or clipping UI, active screen-space overlay blocker, duplicate InteractionUI authority, no input navigation proof. | UI/HUD owner + Player/cockpit owner + Runtime readback owner | `PENDING_VERIFICATION / STATIC_ONLY` |
| Audio P0 | Audio route readback rows plus console export proving `MusicDirector` mixer, first-party DSP route, spatial/ambient/hull/route cues, and no direct unowned Player AudioClip path. | DSPGraph/Audio Profiler proof, underrun count 0 over 60 seconds, no AudioSource hot-path PlayOneShot, voice/memory budget proof, GC proof for event-to-audio routing. | Audio must carry route/hazard/pressure/salvage decisions: oxygen warnings, pressure creaks, sonar/ambient cues, player action feedback, and no decorative beeps. | `STATIC_DOC`: H8 dependency graph row 11 names audio P0 readback; no runtime mix proof present in read evidence. | Null mixer, direct Player AudioClip without owner/release proof, managed audio callback/hot strings, generic clean beeps, no pressure/route cause, no profiler/underrun proof. | Audio remediation owner + Unity proof owner | `PENDING_VERIFICATION / RUNTIME_MIX_PROOF_MISSING` |
| World Placement | Base-before and placement-after h8 proof views, route segment table, seed/mask, density caps, material/LOD/collider readback for rocks/flora/coral/debris, and compact/high route captures. | Frame Debugger/Stats for SetPass/batches/shadows/instancing/LOD, profiler/GC/memory/VRAM when scene/runtime placement changes, continuous GlobalQualityWeight density/load-shed proof. | Placement must improve route decisions: oxygen return, hazard edge, resource/tool affordance, cover, landmark memory, depth/slope read, salvage/evidence interpretation. | `STATIC_DOC`: staging packet only; visible placement deferred until base water/sky/terrain/player/HUD/material proof passes. | Placement used as camouflage, proxy/primitive/blockout pools, random scatter, coral carpet, clutter over affordances, no LOD/collider/material proof, beauty without decision value. | World placement owner + Terrain/ecology owner + Unity proof owner | `PENDING_VERIFICATION / BASE_PROOF_BLOCKED` |
| h8_1475 Proof Packet | Canonical proof root with `manifest.json`, `manifest.sha256`, Unity log, console export, no-mutation readback, dirty-state audit, Frame Debugger/Stats, visual comparison, and required `h8_1475_*.png` set. | Profiler/GC/memory boundaries where runtime claims are made; render stats artifact; no fake runtime numbers; no dirty mutation risk. | Packet must prove active player/HUD, sky/Aegir, Crest/ocean, shoreline, terrain, product-face blockers, audio P0 route, and screenshot route sequence before triage. | `STATIC_DOC`: dependency graph says packet order only; visual rejection says h8_1475 packet absent. | Raw MCP PNG substitution, missing root/manifest/hash/log, dirty scene/prefab/material/importer state, fake hash, stale screenshots, acceptance claim without artifacts. | ASSET_OWNER_36 Unity proof owner + ASSET_OWNER_26 no-mutation owner | `REJECTED / H8_1475_MISSING` |
| Unity Tooling Gate | Clean `process_gate.md` proving CPU under 50 percent and no busy Unity/import/compiler/build processes before any no-mutation Unity proof run. | No build/import/compiler contention; no dotnet/csc/MSBuild/UnityShaderCompiler/UnityPackageManager busy state; no profiler/build claims from static text. | Tooling gate protects the proof run from corrupt state, dirty mutation, and false runtime evidence; if red, owners do static prep only. | `USER_CONTEXT_PROCESS_STATE + STATIC_DOC`: user states Unity tooling gate is red; dependency graph row 01 requires clean preflight before proof execution. | CPU high, active compiler/import/build process, ambiguous Unity state, dirty-state risk, Unity launched while gate red, proof generated without preflight artifact. | Controller + Unity proof owner + Process gate owner | `REJECTED / PROCESS_GATE_RED` |

## Low / Middle / High / Ultra Proof Consequences

These are labels on one continuous `GlobalQualityWeight` curve, not binary modes.

- Low / compact `0.0-0.25`: proof must show readable water, sky/Aegir, shoreline, terrain silhouette, route cue, HUD decision state, and 0 B hot-path allocations where runtime is touched. Reduced optional density is allowed. Ugly water, muddy sky, primitive meshes, flat materials, and overlay-only HUD are rejected.
- Middle `0.25-0.55`: expected player lane. Proof must show production player/HUD binding, active owner routes, coherent water/terrain/sky composition, route density with negative space, and no product-face proxy/default contamination.
- High `0.55-0.85`: saved budget must buy visible value: richer normals, wetness/contact response, stronger Aegir/cloud detail, denser but authored route dressing, better visor/camera/haptic/audio presentation, and longer LOD residency. Gameplay truth, save identity, DTO layout, command semantics, and owner route remain unchanged.
- Ultra `0.85-1.0`: visual overkill only after compact passes. Proof may add layered atmosphere, richer water sparkle, denser near-field biota/debris, capture-grade shoreline/photic/medium-depth route detail, and richer audio/spatial response. It cannot introduce a second truth owner or turn high-end features into required gameplay understanding.

## Regression Model

- CPU: any front adding runtime, render, VFX, audio, movement, UI, placement, or proof tooling work requires profiler evidence. A system over `0.1ms` remains suspicious until cost, cadence, and load-shed path are proven.
- GC: static zero-GC patterns are not proof. UI, movement, input, interaction, audio routing, VFX, and placement claims require 300-frame and stress-window GC/Profiler artifacts when runtime paths are active.
- Memory/VRAM: visual and placement fronts require texture/mesh/material residency evidence. Compact VRAM remains hard-bound; visual quality cannot be lowered below the floor to fit.
- Cadence: `GlobalQualityWeight` must scale fidelity, cadence, density, diagnostic depth, and load shedding continuously with hysteresis. Binary quality switches and ultra-only readability are rejected.
- Correctness: owner/truth route must stay explicit. Quality cannot change gameplay truth, save identity, DTO layout, collider truth, command semantics, material channel semantics, or public claim state.
- Visual: static evidence currently rejects product-face visuals and underwater/surface route captures. Future proof must beat the reference floor without fog, darkness, bloom, or scatter camouflage.

## Current Controller Decision

No active front is accepted.

The project state represented by the read evidence is:

- product-face visuals: `REJECTED`;
- h8_1475 proof packet: `MISSING / REJECTED`;
- runtime movement/UI: `STATIC_ONLY / PENDING_VERIFICATION`;
- audio P0: `PENDING_RUNTIME_MIX_PROOF`;
- world placement: `STATIC_STAGING_ONLY / BASE_PROOF_BLOCKED`;
- Unity tooling gate: `REJECTED / PROCESS_GATE_RED`.

Next valid controller action is to clear the Unity/process gate, then execute no-mutation h8_1475 proof in dependency order. Until that exists, all runtime, visual, profiler, GC, memory, and gameplay acceptance claims remain `PENDING_VERIFICATION` or `REJECTED` according to the rows above.
