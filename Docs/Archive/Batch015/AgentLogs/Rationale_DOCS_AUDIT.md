# Rationale_DOCS_AUDIT

Date: 2026-06-02
Domain: 83 - The Chronicler (Docs)

## Decision 1

Problem: User requested global documentation actualization, but no batch XML prompt with an agent ID was supplied.
Solution: Use `DOCS_AUDIT` as the working ID and constrain edits to Domain 83 documentation. CLI-searched `Docs/Tasks/CURRENT_BATCH.md` for `<AGENT_PROMPT id=...>`.
Rejected Alternatives: Reading archive batch prompts would import stale neighboring tasks. Inventing an XML task count would be false authority.
Scalability potential: Process-only; no runtime system changed. Low/Middle/High/Ultra all remain unaffected.
Hardware Impact: 0 us runtime gain on i3/MX350; prevents documentation drift only.

## Decision 2

Problem: Documentation can overclaim runtime state from source scans.
Solution: Treat edits as `STATIC_DOC` / `STATIC_SOURCE` only unless fresh Unity/profiler/player artifacts exist.
Rejected Alternatives: Marking systems as complete from `rg` hits or docs text. That violates evidence law.
Scalability potential: Keeps low-tier and high-tier readiness claims tied to artifacts, not prose.
Hardware Impact: 0 us runtime gain on i3/MX350; reduces false planning assumptions.

## Decision 3

Problem: Root documentation indexed contracts but did not classify large active content/support corpora such as Lore, Marketing, Modding, Design, Data, Audio, Atmosphere, and AI_Texturing_Templates.
Solution: Add concise folder classification to `Docs/README.md`, boundary text to `PROJECT_BASELINE.md`, and a static snapshot to the actuality ledger.
Rejected Alternatives: Indexing every lore/marketing file would create unreadable bloat. Ignoring the folders would leave the project map incomplete.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged.
Hardware Impact: 0 us runtime gain on i3/MX350; reduces agent misrouting and false authority.

## Decision 4

Problem: `PROJECT_RUNTIME_TOPOLOGY.md` listed core source anchors but did not expose the broader first-party script surface.
Solution: Add a grouped inventory of the `56` direct `Assets/_Project/Scripts` directories.
Rejected Alternatives: Listing every `.cs` file would be stale immediately and noisy. Keeping only core anchors hides active source domains.
Scalability potential: Process-only. Helps agents route work to the correct owner before changing systems.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 16

Problem: Stable world docs compressed streaming, voxel, terrain seams, scatter, vegetation, persistent world, wreckage, and resource routes into one broad row, hiding source owners and proof boundaries.
Solution: Split world source reality into streaming/residency/HLOD, voxel/geology/terrain seams, and scatter/vegetation/persistent content with concrete owner files and explicit missing proof.
Rejected Alternatives: Listing every `World/` file; claiming streamed-world readiness from source; merging visual HLOD proof with gameplay/world truth.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove scene wiring, save/load, backend parity, VRAM/frame hitch, and visual capture before changing fidelity.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 17

Problem: Stable construction docs compressed construction, habitat graph, deconstruction, flooding, fluid pipes, bulkheads, power, logistics, base modules, and drones into one vague row.
Solution: Split the row into construction/deconstruction, flooding/fluid pipes/bulkheads, and power/logistics/base modules/drones with concrete owner files and pending proof boundaries.
Rejected Alternatives: Treating all base systems as one "habitat" owner; listing every module file; claiming power/flood/base readiness from source.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove placement, save/load, flood containment, pipe rupture/drainage, power brownout, drone repair, UI/VR feedback, and frame/GC budgets before changing fidelity.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 5

Problem: Previous documentation pass was too close to folder classification and did not state what the code actually implements.
Solution: Re-read core runtime sources and promote source-backed facts into `PROJECT_RUNTIME_TOPOLOGY.md`: bootstrap phases, broad registry surface, dispatcher phase/tick reality, SignalBus frame snapshots, DataVault handles/locks, save binary paths, and Data Monolith arena/layout.
Rejected Alternatives: Creating another report file would bury the useful facts. Keeping the folder map as the main update would mislead agents about real implementation state.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime behavior unchanged; documentation now exposes where scalability proof is still missing.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 6

Problem: Some domains have real code breadth but no current compile/import/profiler proof in this pass.
Solution: State implemented source surfaces and proof gaps separately. The docs now say "source-backed implementation exists" without upgrading status to runtime ready.
Rejected Alternatives: Marking systems ready from source presence would be fake evidence. Avoiding the gaps would recreate the original documentation problem.
Scalability potential: Process-only. Helps future low/mid/high/ultra work target profiler and GC evidence instead of arguing from docs.
Hardware Impact: 0 us runtime gain on i3/MX350; prevents false performance/readiness claims only.

## Decision 7

Problem: Folder-based source maps hide active root-level runtime files under `Assets/_Project/Scripts/*.cs`.
Solution: Add a loose-root script reality section to `PROJECT_RUNTIME_TOPOLOGY.md` and a source-anchor rule to `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
Rejected Alternatives: Moving files or pretending folders define ownership. That would be out of domain and false under current source layout.
Scalability potential: Process-only. Future low/mid/high/ultra work must open concrete source owners before changing cadence, quality, or runtime truth.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 8

Problem: Several route cards used old static-verified, green-build, or source-complete wording that can be misread as runtime readiness.
Solution: Downgrade wording to `STATIC_SOURCE_PRESENT`, CLI exit-specific evidence, and explicit pending Unity/import/profiler/device boundaries.
Rejected Alternatives: Leaving stronger wording because the surrounding paragraphs mention pending proof. Status lines are what agents copy first.
Scalability potential: Process-only. Prevents fake readiness from driving performance/platform decisions.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 9

Problem: Data Monolith docs mixed current payload facts with historical CLI report chains.
Solution: Add a short current-state block and a current SHA-256 for `static_data.h8bin`, while keeping Unity/player/profiler proof pending.
Rejected Alternatives: Rerunning build/Unity proof despite CPU/build rules and user scope. Claiming historical CLI pass as current player proof.
Scalability potential: Process-only. Low/mid/high/ultra runtime readiness still needs player/profiler/device artifacts.
Hardware Impact: 0 us runtime gain on i3/MX350; static file identity only.

## Decision 10

Problem: Existing docs still forced agents to infer real implementation from folder maps and scattered route cards.
Solution: Add `SOURCE_SYSTEMS_REALITY_MAP.md` as a stable, concise source-owner map grouped by actual scripts and routes, then wire it into the read order.
Rejected Alternatives: Expanding every domain route card in place would create bloat. Keeping only `PROJECT_RUNTIME_TOPOLOGY.md` would mix topology with per-system source ownership.
Scalability potential: Process-only. Future low/mid/high/ultra decisions must begin from actual owners and proof gaps instead of invented domain assumptions.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 11

Problem: AppliedLore docs contained detailed route evidence, but stable architecture docs underrepresented narrative/quest/PDA/audio-log as real source systems and the lore note top status was too weak.
Solution: Promote concise source-present narrative route facts into the source map, runtime topology, coverage matrix, lore implementation note, and actuality ledger while keeping scene bindings and Unity route proof pending.
Rejected Alternatives: Claiming lore is level-integrated from authoring docs, expanding every packet/locale into architecture docs, or treating generated markdown/wiki pages as gameplay runtime data.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future runtime work can target actual content owners without inventing routes.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 12

Problem: Stable meta docs underreported real optimization/networking/modding/QA/build owner code, while `Docs/Modding` schema/spec/README had drifted behind the current `ISignal` source inventory (`175 / 2 / 173` vs stale `173 / 2 / 171`).
Solution: Update mod signal counts and `rg --pcre2` extraction command, then promote concrete source owners for asset lifecycle/load dispatch, VRAM pressure/telemetry, RT lifecycle/pools, rollback/Merkle/prediction, envelope-only mod sandbox/loader, mod-world persistence, QA/headless blackbox runners, and editor build/preflight validators.
Rejected Alternatives: Claiming network/modding/build/QA runtime readiness from source; expanding every validator or DTO into architecture prose; leaving stale signal numbers because the audit matrix already had the right count.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; docs now point future runtime proof at actual owner systems and keep continuous quality/fidelity proof pending.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 13

Problem: `Docs/Lore/Encyclopedia/Article_Index.md` used `runtime-ready` for AppliedContent packets, but the current AppliedLore evidence boundary says source route exists while scene bindings and Unity/PDA/scanner/terminal/save proof remain pending.
Solution: Downgrade packet entries to `source-authored` and add an explicit route-binding proof boundary at the top of the article index.
Rejected Alternatives: Keeping the term because packet text exists; expanding every packet with route notes; claiming runtime readiness from offline AppliedContent audits.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future content integration must prove bindings instead of copying text readiness into runtime status.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 14

Problem: Stable presentation docs still compressed hundreds of UI/Visor/Audio/VFX/Graphics source files into generic labels, hiding concrete owner systems and the proof boundary for GPU/audio-thread-heavy routes.
Solution: Promote concrete presentation owners into source map, runtime topology, coverage matrix, and actuality ledger: PDA streamer, subtitle/font/loading managers, visor HUD/URP features, foveated render commander, thermal DRS adapter, culling/TBDR, player-critical procedural audio, vocal warnings, music/adaptive stems, marine snow/propwash/plasma/fog VFX.
Rejected Alternatives: Listing every presentation file; claiming visual/audio readiness from source; treating UI or VFX as gameplay truth owners.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future presentation work must prove UI GC, audio-thread/device, RenderGraph/Frame Debugger, shader import, visual capture, and VRAM/DRS behavior.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 15

Problem: Gameplay/economy docs described inventory, crafting, survival, tools, and loot too generically despite concrete source owners for native inventory, fabricator, recipe jobs, survival, auxiliary equipment, loot magnet, recycler, first-hour, data archaeology, endings, and airlocks.
Solution: Update source map, runtime topology, coverage matrix, and ledger with concrete owner files and keep copper acquisition, boot-to-craft-to-save/load, UI feedback, loot route, profiler, and GC proof pending.
Rejected Alternatives: Treating gameplay economy as planned from design docs; listing every tool file; claiming first-hour/copper route complete from source presence.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work can target actual gameplay economy owners and proof artifacts.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 18

Problem: Stable AI/atmosphere docs compressed fauna simulation, cognition, pathfinding, ecosystem, gas, weather, ocean surface, and thermodynamics into two vague rows.
Solution: Split the rows into fauna simulation/cognition/damage, AI cognition/pathfinding, ecosystem/boids/macro migration, base atmosphere/gas/weather/ocean, and thermodynamics/reactor hazards with concrete owner files and explicit pending proof boundaries.
Rejected Alternatives: Treating AI/ecosystem as one owner; treating atmosphere/thermal/weather as one owner; listing every file; claiming runtime readiness from source presence.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove scene wiring, deterministic order, visual capture, continuous quality load-shed, device behavior, frame time, and GC before changing fidelity.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 19

Problem: Stable combat and vehicle docs still used broad route-card labels and one vague vehicle/equipment physics row, hiding concrete combat, hazard, submarine, seaglide, exosuit, buoyancy, cavitation, tether, and cable owners.
Solution: Split combat/hazard owners from vehicle physics and force/buoyancy/tether routes; update source map, topology, coverage matrix, and ledger with source owners and proof gaps.
Rejected Alternatives: Treating combat docs as doc-only despite source owners; merging vehicle damage with habitat damage; claiming physics stability from source; listing every physics helper file.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove force ownership, collision correctness, controller scenes, ballast/docking, tether/cable gameplay, feedback, frame time, GC, and device behavior before changing fidelity.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 20

Problem: Stable player docs compressed deterministic input, hydrodynamic KCC, player kinematics, physical grab/snap, VR hand bridge, somatic comfort, and tool/equipment interaction queues into one vague "movement and interaction" row.
Solution: Split player source reality into player kinematics/KCC/input, physical interaction/VR somatic hands, and tool/equipment interaction signal route; update source map, topology, Echelon 4 coverage, and ledger with concrete owners and proof gaps.
Rejected Alternatives: Treating `Interaction/` as one owner; claiming controller/KCC/VR readiness from source; listing every input or tool helper file; merging tool signal queues with inventory/economy.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove controller devices, KCC collision, environment providers, grab/force ownership, XR hand bridge, tool queue route, UI/haptic feedback, frame time, and GC before changing fidelity.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.

## Decision 21

Problem: Stable meta docs still compressed optimization, rollback networking, modding, QA/headless, and build/platform tooling into one "Optimization / ModdingAPI / Networking / QA / Editor" row.
Solution: Split meta-runtime support into five source-backed routes: optimization/asset lifecycle/VRAM/RT, rollback networking/prediction, modding API/sandbox/persistence, QA watchdog/endurance/headless, and editor build/platform/SDK tooling.
Rejected Alternatives: Keeping the single meta row; listing every validator/editor window; claiming loopback, mod, QA, or build readiness from source presence.
Scalability potential: Process-only. Low/Middle/High/Ultra runtime paths unchanged; future work must prove Addressables/RT/VRAM/DRS behavior, rollback/device transport, mod load/save/security, QA artifacts, and platform/player build outputs before changing fidelity or authority routes.
Hardware Impact: 0 us runtime gain on i3/MX350; no runtime code changed.
