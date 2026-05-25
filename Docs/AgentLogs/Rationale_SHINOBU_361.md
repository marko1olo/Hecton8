# SHINOBU_361 Rationale

Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE until scripts are run.

## Decision 001: Static Audit First

Problem: Unity material and prefab texture defects can exist even when the Editor is not in a clean import state.
Solution: Use a CLI/static scanner to build a `.meta` GUID map, parse YAML-ish material/prefab/shadergraph references, and emit CSV/Markdown/JSON artifacts.
Rejected Alternatives: Unity AssetDatabase-only scan was rejected because it requires a clean Editor import and can hide raw broken GUIDs behind import failure noise. Raw YAML mutation was rejected because the task is audit/queue generation, not direct asset repair.
Scalability potential: Low uses audit output to avoid loading useless stubs; Middle uses corrected BC7/BC5 packs; High uses richer near-field texture families; Ultra can add visual-overkill detail maps without changing gameplay truth.
Hardware Impact: Expected benefit on i3/MX350 is VRAM predictability, fewer magenta fallback materials, and lower sampler count through ORM packing. Microseconds saved: PENDING PROFILER; static audit cannot prove frame time.

## Decision 002: Visual Fake Texture Detail

Problem: Material surface complexity can tempt geometry-heavy rivets, seams, cracks, grates, salt buildup, and flora membrane detail.
Solution: Generate prompt and bake plans that encode those details into albedo/normal/ORM/emissive masks, preserving low-poly render geometry.
Rejected Alternatives: Mesh rivets, dense panel bevel geometry, realtime wetness simulation, or per-detail decals were rejected for this audit because the prompt mandates Dear Lie texture baking and MX350 texture/sampler control.
Scalability potential: Low keeps baked AO and compressed 512-1024 maps; Middle restores mip detail; High/Ultra allow 2048 hero surfaces and optional emissive/specular masks for presentation only.
Hardware Impact: Expected gain on i3/MX350 is fewer vertices, fewer material samples, and stable streaming. Exact microseconds saved: PENDING PROFILER.

## Decision 003: Empty Slot Deficiency Filter

Problem: The raw material YAML contains thousands of empty optional texture slots. Treating every empty optional normal/detail/mask slot as mandatory art debt inflated the replacement queue to 3,739.884 MiB, which is a false production requirement.
Solution: Keep every slot visible in `production_texture_manifest.csv`, but only required albedo slots, broken texture GUIDs, built-in default texture references, stub references, and import-setting defects generate prompt/VRAM debt.
Rejected Alternatives: Filling every optional slot was rejected because it would invent art requirements and break ZERO-RECONSTRUCTION ASSUMPTIONS. Hiding optional slots was rejected because forensic audit still needs a complete serialized surface map.
Scalability potential: Low avoids wasteful optional maps; Middle restores required PBR surfaces; High and Ultra can selectively promote optional detail maps after art direction and VRAM proof.
Hardware Impact: Queue dropped from the earlier optional-slot false debt to 783.529 MiB factual replacement debt after the FBX embedded-path pass and UI/script false-positive filter. Microseconds saved: PENDING PROFILER; static VRAM math only.

## Decision 004: Editor Gizmo Cache Boundary

Problem: A SceneView migration overlay can accidentally become a per-repaint file parser or scene search loop.
Solution: `TextureMigrationDebugGizmo` reads `production_texture_manifest.csv` only on menu toggle/refresh, caches material priorities, then draws cached renderer bounds in SceneView.
Rejected Alternatives: Runtime gizmo was rejected because migration visualization is editor diagnostics. Automatic per-frame CSV polling was rejected because it creates editor repaint I/O and avoidable allocations.
Scalability potential: Low devices are unaffected because this code is editor-only. High-end editor machines can inspect more scene renderers without changing gameplay truth or player builds.
Hardware Impact: 0 us gameplay hot path. Editor repaint cost is not profiled; runtime impact is structurally absent through `#if UNITY_EDITOR`.

## Decision 005: Compile Protection

Problem: The prompt requires compile verification, but project rules forbid launching `dotnet build` when CPU is over 50 percent or a compiler is active.
Solution: Performed Python `py_compile` for new tools and checked CPU/compiler process state before C# build. CPU was 99.4178073412672 percent; no C# compile was launched.
Rejected Alternatives: Forcing `dotnet build` under load was rejected because it violates the build-protection rule and can damage parallel agent throughput.
Scalability potential: Protects the shared workstation from avoidable rebuild contention while preserving static evidence for review.
Hardware Impact: Avoided a full C# build under 99 percent CPU load. Exact microseconds saved: not measured; this is workstation contention avoidance, not runtime optimization.

## Decision 006: Shared Report Upsert

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` is a shared multi-agent report surface. Writing the SHINOBU_361 OOP texture scanner result as the root object erased neighboring sections in the active file.
Solution: `Tools/OOP_Texture_Scanner.py` now chooses the existing shared JSON as base, recovers the tracked HEAD baseline if the current file is already a SHINOBU_361 scanner-only object, and upserts under `shinobu_361_oop_texture_scanner`.
Rejected Alternatives: Keeping a root overwrite was rejected because it damages other agents' evidence. Writing only a sidecar report was rejected because the prompt explicitly names the shared report path.
Scalability potential: Low devices unaffected; this is report hygiene. High/Ultra editor workflows keep all rendering evidence in one report without cross-agent data loss.
Hardware Impact: 0 us gameplay hot path. Report write is offline.

## Decision 007: Unique Production Queue

Problem: The forensic manifest correctly records every defective slot, but multiple slots can map to the same generated target texture. Sending all 413 prompt rows directly to art production would duplicate work.
Solution: Added `TextureProductionQueue_SHINOBU_361.csv/json`, grouping by target texture path while preserving source count, source paths, slots, states, prompt, normal plan, ORM plan, compression, and resolution.
Rejected Alternatives: Deleting duplicate manifest rows was rejected because forensic traceability requires every serialized slot. Keeping only the 413 prompt list was rejected because art production needs unique targets.
Scalability potential: Low/Middle avoid redundant generated textures; High/Ultra still have one upgrade path per target family.
Hardware Impact: Collapsed 413 prompt rows to 175 unique target textures and 238 duplicate slot references. Runtime microseconds saved: PENDING PROFILER; production waste reduced statically.

## Decision 008: Editor CSV Parser Hardening

Problem: `TextureMigrationDebugGizmo.SplitCsv` toggled quote state on every quote character. Current manifest rows contain no escaped double-quote sequences, but future designer strings or generated asset names could contain escaped `""` and corrupt column boundaries.
Solution: Skip the second quote in escaped CSV quote pairs before toggling quote state. Keep parsing cold and editor-only; the overlay still reads the manifest only on toggle/refresh.
Rejected Alternatives: Relying on the current manifest shape was rejected because the tool consumes generated CSV and should not silently mis-map material priorities when quoted text changes. Importing a full CSV package was rejected because this is an editor diagnostic with a narrow local parser.
Scalability potential: Low/Middle/High/Ultra player builds are unaffected; this is editor-only audit stability. High-end editor workflows can keep larger manifest text fields without changing runtime routes.
Hardware Impact: 0 us gameplay hot path. Editor refresh cost remains cold and unprofiled; Unity import/compile proof is still pending.

## Decision 009: Exact Prompt Contract Gate

Problem: The generated prompts already described an orthographic top-down view, but the production contract asks for a precise flat top-down orthographic demand. Similar prose is not enough when the queue is consumed by external image-generation tools and manual artists.
Solution: `Tools/TextureAuditAndBakeDirector_SHINOBU_361.py` now emits `flat, top-down, orthogonal orthographic view` in every prompt and `prompt_syntax_audit` fails if required view, lighting, zero-shadow, or seamlessness phrases are absent.
Rejected Alternatives: Keeping the weaker `flat top down orthogonal view` wording was rejected because it passes human intent but does not give a strict machine-checkable production gate. Adding bracketed generator flags was rejected by the natural-language prompt mandate.
Scalability potential: Low/Middle/High/Ultra all consume the same source prompt truth. Quality tiers can choose resolution and mip residency, but prompt wording and material identity do not branch by hardware.
Hardware Impact: 0 us gameplay hot path. Avoids downstream texture rework and prevents directional baked lighting that would conflict with triplanar/geometric lighting. Runtime savings remain PENDING PROFILER.

## Decision 010: Explicit Platform Import Formats

Problem: `BatchImportTextures.py` previously edited generic import fields while the reports claimed BC7/BC5. Generic `textureCompression` does not prove Standalone BC7/BC5 or mobile ASTC selection.
Solution: The script now records and applies role-specific platform format numbers derived from the local Unity 6000 enum: Standalone BC7 = 25, Standalone BC5 = 27, Android ASTC_6x6 = 50. It still refuses to create `.meta` files or GUIDs; `--write-meta` only mutates existing Unity-generated metas.
Rejected Alternatives: Blind GUID/meta invention was rejected because it would corrupt Unity identity. Relying on artists to click Inspector settings was rejected because the task demands an automated import bridge. Adding another C# AssetPostprocessor was rejected for now because it would widen the compile surface during active dotnet contention.
Scalability potential: Low keeps generated maps compressed and mipmapped; Middle/High/Ultra retain BC7 visual quality on Standalone and ASTC mobile portability without changing texture identity or manifest route.
Hardware Impact: 0 us gameplay code. Expected benefit on i3/MX350 is lower VRAM residency and bandwidth versus default/automatic imports; exact frame and memory impact is PENDING Memory Profiler / player capture.

## Decision 011: Reproducible Readable Queue

Problem: The readable queue is the active human-facing production file, but a hand-maintained Markdown sidecar can drift from the CSV/JSON manifest.
Solution: `TextureAuditAndBakeDirector_SHINOBU_361.py` now generates `TextureProductionQueue_SHINOBU_361_READABLE.md` from the unique queue, including role/category/resolution summaries and every prompt/normal/ORM plan.
Rejected Alternatives: Manual Markdown maintenance was rejected because it can silently diverge from the authoritative CSV. Deleting the readable file was rejected because artists need the human bridge, not only dense CSV.
Scalability potential: Low/Middle/High/Ultra all use the same readable production queue; hardware scaling remains in target resolution, compression, mip bias, and optional presentation masks.
Hardware Impact: 0 us gameplay hot path. Production-time drift avoidance only; runtime cost is structurally absent.

## Decision 012: FBX Embedded Texture Debt Inclusion

Problem: FBX files can carry embedded or external texture path strings that are not Unity GUID references. Ignoring those paths leaves imported mesh materials visually broken while the GUID scan falsely appears clean.
Solution: Treat unresolved FBX embedded texture paths as `MISSING_EMBEDDED_TEXTURE`, include them in the deficiency set, add `missing_embedded_texture_count` to the summary, and derive deterministic target names from the FBX material plus embedded basename.
Rejected Alternatives: Ignoring non-GUID FBX paths was rejected because the task explicitly includes `.fbx` static audit. Generating one target per repeated embedded path occurrence was rejected because 119 rows collapse to 20 unique texture targets.
Scalability potential: Low tier gets a small set of compressed replacements instead of broken mesh materials; Middle/High/Ultra can increase resolution/mip residency on the same target identities without changing asset ownership.
Hardware Impact: 0 us gameplay hot path. Current static debt is 119 FBX embedded references, 20 unique target textures. Estimated replacement residency is 783.529 MiB after false-positive filtering, status PASS under the 900 MiB cap.

## Decision 013: Production Action Schema

Problem: The unique texture queue exposed `reference_states` but no explicit work action, forcing artists and import automation to infer whether a row needs generation, rebake/import repair, or both.
Solution: Add an `action` column to `TextureProductionQueue_SHINOBU_361.csv/json` and readable action counts. Map missing required slots, missing GUIDs, missing embedded texture paths, stubs, and built-in defaults to `GENERATE_REPLACEMENT_PBR`; map import-only defects to `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`; map mixed rows to `GENERATE_REPLACEMENT_PBR_AND_FIX_IMPORT`.
Rejected Alternatives: Keeping inference in human process was rejected because it creates production ambiguity. A binary generate/fix flag was rejected because mixed rows exist and need a combined action.
Scalability potential: Low/Middle/High/Ultra share one production queue; quality tiering is handled by resolution/compression/mip residency, not by changing truth ownership or target identity.
Hardware Impact: 0 us gameplay hot path. Current action counts: 171 generate replacements and 4 rebake/import fixes. This removes queue ambiguity before any Unity import work.

## Decision 014: Prefab Sprite and Missing Script Rejection

Problem: The first prefab direct-GUID pass treated `m_Sprite` image references and missing `m_Script` MonoBehaviour GUIDs as PBR texture debt. That polluted the queue with HUD sprite rows and a false `Suit_HUD_Canvas_Albedo` target.
Solution: Prefab parsing now skips sprite-context windows and skips missing script GUID windows. Missing prefab GUIDs are considered texture defects only when the local YAML window contains explicit texture fields such as `m_Texture`, `texture:`, `textureGuid`, or `_Tex`.
Rejected Alternatives: Keeping broad `tex` substring matching was rejected because UI text and scripts are not material surface ownership. Excluding all prefab image references was rejected because actual prefab texture references still need audit.
Scalability potential: Low/Middle/High/Ultra avoid spending texture budget on UI sprite rebakes. PBR replacement identity remains scoped to material surfaces, FBX embedded paths, and real texture slots.
Hardware Impact: 0 us gameplay hot path. Static effect: audited rows dropped to 4,529, prompt rows to 413, unique targets to 175, and estimated replacement residency to 783.529 MiB PASS; `Suit_HUD_Canvas` now contributes 0 texture defect rows.

## Decision 015: Compile Wall Stop

Problem: A guarded `dotnet build Hecton8.Editor.csproj --no-restore` was justified after CPU/dotnet guards cleared, but the build failed in `Hecton8.Core.csproj` before editor compilation because Construction files reference missing namespace `Hecton8.Habitat`.
Solution: Stop after the first failed build attempt, record the dependency block, and avoid touching Construction/Habitat domain files. The generated `Hecton8.Editor.csproj` also does not yet list `TextureMigrationDebugGizmo.cs`, so Unity project regeneration/import is required before authoritative compile proof of that new editor file.
Rejected Alternatives: Editing `HatchLockJobs.cs`, `BulkheadContainmentRuntime_HatchLocks.cs`, or Habitat assemblies was rejected as outside SHINOBU_361 texture-audit domain. Repeated build attempts were rejected after 7 `dotnet.exe` processes appeared again.
Scalability potential: Compile-wall discipline protects parallel agent throughput and keeps texture-audit work isolated from Construction/Habitat ownership.
Hardware Impact: Build attempt consumed 13.77 seconds and failed with 2 unrelated errors, 3 warnings. No runtime code changed by SHINOBU_361.

## Decision 016: OOP Texture Scanner Evidence Split

Problem: The material scanner previously mixed high-confidence allocation debt with broad `.material` text hits, and the refined scan initially took about 44 seconds because it parsed every source file line-by-line with regex checks.
Solution: Split findings into high-confidence `Resources.Load<Texture/Material>`, `new Material`, and likely `Renderer.material` rows versus review-only material member access, then add a byte-level token prefilter before line parsing. The current scan checks 2,650 files, parses 130 candidate files, and records `elapsedMs=2835.119`.
Rejected Alternatives: Counting every `.material` token as renderer clone debt was rejected because TMP/font/UI material members are different APIs. Keeping full line-regex scanning was rejected because it wastes batch machine time with no better evidence.
Scalability potential: Low/Middle/High/Ultra runtime unaffected; this is an offline evidence gate. Faster static scans reduce batch contention and make repeated verification less expensive.
Hardware Impact: 0 us gameplay hot path. Offline scanner wall time dropped from about 44 seconds to 2.835 seconds command time in this workstation run. Runtime microseconds saved: PENDING PROFILER.

## Decision 017: Priority Policy Repair

Problem: The unique production queue had become technically correct but operationally weak because most active rows sorted as `MEDIUM`, hiding the immediate cockpit/habitat/HUD/terminal work that should drive first-pass art remediation.
Solution: `priority_for` now uses explicit immediate tokens for prologue/start/cockpit/habitat/terminal/airlock/HUD/visor paths and explicit distant-background tokens for skybox, planet, star, celestial, background, and panorama paths. The regenerated queue reports BLOCKER=15, MEDIUM=154, LOW=6, and `TextureAudit_SHINOBU_361.json` records the priority policy.
Rejected Alternatives: A flat MEDIUM queue was rejected because it is not a prioritized migration queue. Category-only priority was rejected because broad habitat/backdrop categories cannot distinguish near-field starter surfaces from distant presentation assets.
Scalability potential: Low/Middle devices get the first pass spent on near-field visual blockers instead of distant backgrounds; High/Ultra can later raise resolution or add optional detail maps on the same target identities.
Hardware Impact: 0 us gameplay hot path. This is production triage only; runtime savings remain PENDING Unity import and player capture.

## Decision 018: Evidence Refresh Discipline

Problem: SHINOBU_361 reports can drift if the generator/scanner code is polished without immediately regenerating the derived CSV, JSON, and readable Markdown artifacts.
Solution: Rerun `TextureAuditAndBakeDirector_SHINOBU_361.py`, `BatchImportTextures.py`, `OOP_Texture_Scanner.py`, `py_compile`, CSV/JSON structural validation, and `git diff --check` after the current continuation pass. Treat the regenerated files as the only current evidence.
Rejected Alternatives: Leaving stale counts in status/logs was rejected because intermediate reports already contain older 157/333 and 175/413 states. A .NET rebuild was rejected for this pass because 7 active `dotnet.exe` processes remain and the previous build already identified an unrelated Construction/Habitat compile wall.
Scalability potential: Low/Middle/High/Ultra texture identity remains stable; regenerated reports only update evidence, queue ordering, compression/import policy, and production readability.
Hardware Impact: 0 us gameplay hot path. Current static audit remains 783.529 MiB estimated replacement residency under the 900 MiB texture cap; runtime memory/frame impact is PENDING Unity import, Memory Profiler, and player capture.

## Decision 019: Forensic Metrics vs Production Metrics Split

Problem: `TextureAudit_SHINOBU_361.md` printed all-row forensic category and priority counts next to the unique production queue size. That is technically valid data, but easy to misread because 4,529 audited slot/reference rows and 175 unique art targets answer different questions.
Solution: Add unique queue priority, category, action, role, and resolution counts into `unique_texture_queue_summary`; rename the Markdown sections to `Forensic Category Counts` and `Forensic Priority Counts`; print unique production counts directly under the queue header.
Rejected Alternatives: Keeping only forensic counts was rejected because the active art queue is unique-target based. Keeping only unique counts was rejected because forensic slot traceability is required for audit and migration gizmo diagnostics.
Scalability potential: Low/Middle/High/Ultra asset planning now uses the same target identities while preserving complete forensic coverage for later import and validation passes.
Hardware Impact: 0 us gameplay hot path. This is report correctness only; it prevents production misprioritization, not a measured frame-time win.

## Decision 020: Handmade Prompt Pass

Problem: The generated prompt template was structurally valid but artistically too generic and too close to a low-value grim industrial look. It did not give an image model enough specific beauty, material taste, or HECTON-8 premium expedition identity.
Solution: Start a separate human-authored prompt book at `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`. The first pass covers the 15 BLOCKER prologue habitat targets with distinct material intent per ceiling, floor, wall, bulkhead, visor glass, and planetary normal source. The style target is premium NASA research hardware under the ocean: off-white pressure panels, satin titanium, graphite rubber, teal/amber accents, controlled salt wear, readable AAA material separation.
Rejected Alternatives: Regenerating with another template pass was rejected because it repeats the failure mode. Replacing the authoritative CSV/JSON immediately was rejected until a full 175-card handmade pass exists; the handmade file is the current art-direction override.
Scalability potential: Low/Middle/High/Ultra still share the same target paths and import plan; handmade prompts improve source art quality without changing runtime truth or texture ownership.
Hardware Impact: 0 us gameplay hot path. This is art-production text; runtime impact remains PENDING Unity import and capture.

## Decision 021: Handmade Flora Prompt Pass

Problem: The previous generated flora language was valid as a queue placeholder, but artistically weak. It leaned toward generic wet organic surfaces and did not separate coral, kelp, proxy grass, bud, stem, canopy, and membrane material behavior enough for modern image models to produce attractive game-ready PBR sources.
Solution: Append a manual `FLORA_EPIDERMIS` section to `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md` with 26 distinct prompt cards. Each card states a specific biological surface identity, controlled premium palette, top-down flat-lit generation contract, BC5 normal intent, and ORM mask behavior.
Rejected Alternatives: A second automatic template pass was rejected because it repeats the low-value failure mode. A single "alien wet plant" master prompt was rejected because it would flatten all flora into one visual family and produce unusable repetition.
Scalability potential: Low tier can use the same source identities at 512-1024 with ASTC/BC compression and still read as beautiful biology; Middle keeps the full 1024 flora set; High can promote hero flora mips; Ultra can add emissive/spec/translucency masks later without changing target paths.
Hardware Impact: 0 us gameplay hot path. This is offline art-direction text. Expected low-end benefit is fewer wasted generated candidates because the prompts now specify clear material families and mask plans before import.

## Decision 022: Handmade Geology Prompt Pass

Problem: The geology queue needed more than generic gray rock prompts. HECTON-8 terrain must sell an attractive abyssal planet with readable traversal materials, triplanar-safe albedo, and specific mineral identity that fits the habitat and flora palette.
Solution: Append 23 manual `GEOLOGY_TRIPLANAR` prompt cards to `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`. The prompts split cave entrances, landmark spires, medium clusters, shelves, shared rock materials, Rock2, Rock4, river rocks, samples, terrain variants, and the misplaced flashlight material into specific art directions with BC5 normal and packed ORM plans.
Rejected Alternatives: Reusing one gray basalt prompt was rejected because it would produce repeated stone noise across terrain, caves, cliffs, and props. Directional-lit scenic rock images were rejected because triplanar material sources need flat diffuse color and no baked scene lighting.
Scalability potential: Low tier can keep the same 2048 source compressed/mipped or downscale terrain variants while retaining readable mineral identity; Middle keeps full set; High/Ultra can add detail normals, parallax-like shader fakery, or emissive mineral flecks without changing texture targets.
Hardware Impact: 0 us gameplay hot path. This is offline prompt authoring. Expected low-end impact is fewer unusable generated rock candidates and cleaner BC7/BC5/ORM import work.

## Decision 023: Handmade Gameplay Family Prompt Pass

Problem: Remaining habitat-interior targets include gameplay feedback, proxy, and world-family materials. If these use one generic sci-fi prompt, valid/invalid construction ghosts, safe pockets, hazard pockets, resource pockets, creature zones, and debris fields will lose distinct gameplay readability.
Solution: Append 31 manual `HABITAT_INTERIORS` prompt cards to `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`. Each card defines the material's gameplay signal first, then a premium HECTON-8 visual language, BC5 normal intent, and packed ORM behavior.
Rejected Alternatives: Generic "interface texture" and "danger texture" prompts were rejected because they collapse player feedback into repeated color noise. Grim danger wording was rejected because the corrected art direction is premium abyssal expedition beauty with disciplined warning signals.
Scalability potential: Low tier keeps clean readable albedo/normal/ORM signals on simple meshes; Middle retains full 1024 maps; High/Ultra can add shader glow, translucency, or detail masks later without changing target texture ownership.
Hardware Impact: 0 us gameplay hot path. Expected low-end benefit is fewer confusing gameplay-marker textures and fewer regeneration cycles before import.

## Decision 024: Handmade Completion Pass

Problem: After the geology and gameplay-family passes, 50 unique targets still had only generated/template prompt coverage. They included resources, sargassum overlays, sky/celestial sources, support markers, tool placeholders, tool trial states, imported barnacles, and residual red/sand/snow/skybox textures.
Solution: Append the final 50 manual prompt cards to `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`, bringing handmade coverage to 175 of 175 unique production target textures. Each prompt preserves the corrected direction: premium abyssal expedition visuals, clean PBR material separation, flat orthographic generation, BC5 normal plan, and packed ORM plan.
Rejected Alternatives: Leaving the remaining targets on the automatic readable queue was rejected because the user explicitly rejected automatic prompt language. Writing one master "tool material" or "resource material" prompt was rejected because it would lose gameplay identity.
Scalability potential: Low tier can downscale or stream these same source identities without losing readability; Middle keeps 1024/2048 targets; High/Ultra can add shader polish, emissive masks, and detail overlays using the same target ownership.
Hardware Impact: 0 us gameplay hot path. This is offline art-production text. Production impact: no remaining unique target depends on the rejected generic prompt style.

## Decision 025: Reference Pack / Style Lock Protocol

Problem: Handmade prompts alone do not guarantee visual unity once an image generator starts producing candidates. Without controlled references, the 175 targets can drift into separate styles: generic sci-fi habitat panels, over-dark geology, noisy flora, and random sky art.
Solution: Add `Docs/Reports/TextureGenerationStyleLock_SHINOBU_361.md`. The protocol uses a three-part reference pack: global Hecton mood refs, category-specific refs, and a same-family approved output after the first good candidate exists. Existing project references are used as the source of truth: planet/cloud PNGs for mood, `TX_ProceduralBio_Shallows_*` and imported flora textures for biology, rendered procedural rock/flora previews for shape, and `MAT_family_*` materials only through previews or bound textures.
Rejected Alternatives: Sending all existing references into every prompt was rejected because it averages the style into mud. Random internet NASA/sci-fi references were rejected because they are not project-owned style truth. Text-only generation for all 175 targets was rejected because it risks batch drift after the first candidates diverge.
Scalability potential: Low keeps the same texture identities with downscaled mips and clean readable forms; Middle keeps full 1024/2048 maps; High/Ultra can add emissive masks, detail normals, shader polish, and richer material response while retaining the same albedo/normal/ORM ownership.
Hardware Impact: 0 us gameplay hot path. Production impact is fewer rejected candidates and lower risk of importing stylistically incompatible textures. Runtime gains remain PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 026: No-Reference Bootstrap Seeds

Problem: Some production families do not yet have strong project-owned bitmap references, especially the hard-surface habitat master look, tools, gameplay signal surfaces, visor glass, terminals, and resource-pocket materials. Starting final generation from text only across 175 targets risks early style drift.
Solution: Add `Docs/Reports/TextureReferenceBootstrap_SHINOBU_361.md` with 12 handmade look-dev seed prompts. The seeds are not factual missing texture targets and are not added to the manifest; they are controlled reference generators. The workflow is to generate 3 candidates per seed, pick 1 winner, use it as a temporary same-family reference, then replace the seed with approved production outputs after two good final textures exist.
Rejected Alternatives: Waiting for nonexistent references was rejected because it blocks production. Random external references were rejected because they break project visual sovereignty. Using only planet/cloud references for hard-surface habitat was rejected because they carry palette mood but not panel construction grammar.
Scalability potential: Low gets clean broad shapes that survive downscale and mip pressure; Middle keeps full production resolution; High/Ultra can add detail normals, emissive masks, and richer shader response after the same identity is locked.
Hardware Impact: 0 us gameplay hot path. The value is production rejection reduction and consistency before import. Runtime impact remains PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 027: Operator Execution Playbook

Problem: The prompt book, seed prompts, style lock, CSV queue, and import plan are all correct but spread across multiple artifacts. An artist/operator needs one file that says exactly what to generate first, what references to attach, how to name candidates, how to reject bad results, and when a texture is actually done.
Solution: Add `Docs/Reports/TextureGenerationExecutionPlaybook_SHINOBU_361.md`. It consolidates the full workflow: 175-scope summary, non-negotiable prompt contract, seed procedure, reference rule, candidate naming, Batch 0 look-dev, Batch 1 blockers, style-anchor promotion, flora/geology/habitat/sky batches, PBR map rules, Unity import order, examples, and done criteria.
Rejected Alternatives: Chat-only instruction was rejected because it will be lost and is not an artifact. Putting all execution detail into the handmade prompt file was rejected because it would make prompt copying noisy. Adding generation candidates to the manifest was rejected because look-dev seeds are not factual missing texture targets.
Scalability potential: Low tier benefits from explicit rejection of noisy detail that shimmers after mips; Middle keeps full production maps; High/Ultra can extend from the same approved albedo/normal/ORM identities with optional emissive/detail masks.
Hardware Impact: 0 us gameplay hot path. Production impact is fewer bad generations, fewer inconsistent imports, and clearer final QA. Runtime proof remains PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 028: Batch 01 Golden Prompt Override

Problem: The first 15 blocker textures will define the look of the remaining 160 production textures. The earlier handmade blocker prompts were acceptable, but not strong enough as the first style-defining generation batch because they did not carry per-target reference pack instructions and acceptance gates.
Solution: Add `Docs/Reports/TextureProductionBatch01_Blockers_GoldenPrompts_SHINOBU_361.md` with a stronger V2 prompt set for the 15 `BLOCKER` targets. Each entry includes target path, reference pack, improved natural-English prompt, normal plan, ORM plan, and acceptance rule. The execution playbook now points Batch 1 to this override file.
Rejected Alternatives: Editing the original 175-card prompt book directly was rejected because it is the complete coverage artifact and should remain stable. Generating the first batch from weaker prompt text was rejected because early outputs become style anchors for later batches.
Scalability potential: Low tier benefits from broad readable forms and reduced mip shimmer; Middle keeps the same final source maps; High/Ultra can layer extra shader detail over the same approved anchors.
Hardware Impact: 0 us gameplay hot path. Production impact is higher chance that the first approved floor/wall/glass outputs are strong enough to become style anchors. Runtime impact remains PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 029: PBR Set / External Pre-Reference Guide

Problem: The operator instruction said "make a PBR set" but did not define how to produce albedo, normal, and ORM maps, and it did not expose the channel packing conflict between the SHINOBU ORM plan and standard URP Lit mask packing. The user also requested internet pre-references, with direction closer to Subnautica but slightly darker, more hi-tech, and more industrial.
Solution: Add `Docs/Reports/TexturePBRSetAndExternalReferenceGuide_SHINOBU_361.md`. It defines albedo/normal/ORM roles, manual height-to-normal workflow, roughness/AO/metallic value ranges, URP Lit repack rules, external pre-reference hierarchy, reviewed source URLs, search strings, and HECTON translation rules. Project refs remain primary; real subsea engineering and Subnautica/Pinterest refs are do-references only.
Rejected Alternatives: Chat-only explanation was rejected because the CTO reads disk artifacts. Directly using random Pinterest images as primary style authority was rejected because it breaks project style ownership. Assigning SHINOBU `R=AO/G=Roughness/B=Metallic` ORM to raw URP Lit without conversion was rejected because Unity URP Lit uses a different packed mask contract.
Scalability potential: Low/Middle get clean compressed albedo, BC5 normals, and packed masks without extra samplers; High/Ultra can add detail/emission polish on the same approved identities. Darker tone is carried by lighting/fog/material response, not by crushing albedo into black.
Hardware Impact: 0 us gameplay hot path. Expected low-end benefit is avoiding sampler waste and wrong roughness/metallic response that would force later rework. Exact microseconds saved remain PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 030: Generator Workpack Instead Of More Instructions

Problem: The prior artifacts were correct but too operator-heavy. The user needs to generate and save images, not read process essays before every target.
Solution: Generate `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/` with machine-split copy-paste prompt files: 12 seed refs, 15 Batch 01 blockers from the golden override, and 175 all-target prompt files from the handmade book. Each file includes job id, role, category, candidate save folder, final target path, reference pack, and the exact prompt. Candidate drop folders were created under `Docs/ArtDrop/SHINOBU_361/`.
Rejected Alternatives: Another long instruction page was rejected. Editing generated pixels was unavailable in this session because no built-in image generation tool is exposed and `OPENAI_API_KEY` is absent, so producing a ready generation workpack is the maximum local completion without credentials.
Scalability potential: Low/Middle/High/Ultra texture identities stay unchanged; this work reduces production friction and keeps final asset paths stable for later import.
Hardware Impact: 0 us gameplay hot path. Production impact is reduced manual prompt extraction and fewer save-path mistakes. Runtime impact remains PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 031: Reject Tile-Like Wall Seed And Split Wall Into Layers

Problem: The first `REF_SEED_001` candidate is visually clean but structurally wrong. It reads as repeated rounded sci-fi bathroom tile rather than a habitat wall system. If accepted, it would bias all wall blockers toward decorative panel repetition.
Solution: Reject it as a style anchor. Add `Docs/Reports/TextureLookdevDecisionLog_SHINOBU_361.md`, update `REF_SEED_001` and `REF_SEED_003`, add `NEXT_RETRY_WALL_SYSTEM.md`, and create four layer jobs: base pressure skin, service conduit overlay, instrument attachment kit, and wall trim height source. Patch Batch 01 wall prompt files and corresponding all-target prompt files to demand layered wall construction.
Rejected Alternatives: Saving the candidate as `LOOKDEV_APPROVED_REF_SEED_001.png` was rejected because it would poison the wall family. A single all-in-one wall texture was rejected because the wall needs layered construction: base material, conduit/service pass, and separate mounted instruments/tools.
Scalability potential: Low tier can use broad base wall fields and cheaper normal relief; Middle keeps separate readable layers; High/Ultra can add denser placed instruments, cable overlays, decals, and richer normal/emissive masks without changing wall identity.
Hardware Impact: 0 us gameplay hot path. Expected production gain is fewer rejected wall candidates and less repeated texture tiling. Runtime impact remains PENDING Unity import, Memory Profiler, Frame Debugger, and player capture.

## Decision 032: Single Active Wall Prompt File

Problem: The wall correction existed but was spread across `NEXT_RETRY_WALL_SYSTEM.md`, prompt subfolders, summary text, and older batch files. That still forced the operator to decide which file is current.
Solution: Add `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/START_HERE_WALL.md` as the single active wall-generation entry point. It contains only the current task: do not save the rejected seed, generate four wall-layer images, save variants to exact folders, and return them for review.
Rejected Alternatives: Keeping multiple equivalent entry files was rejected because it wastes operator attention and invites using stale prompts.
Scalability potential: No runtime tier impact; production scaling improves because wall layers can become base/overlay/attachment sources for low through ultra variants.
Hardware Impact: 0 us gameplay hot path. Production impact is lower prompt confusion and fewer wrong candidate generations.

## Decision 033: Layered Wall Candidate A Triage

Problem: The first layered wall candidate set improves over the rejected tile seed, but the base and height images still encode repeated rounded panel logic. Accepting them as final would lock the wall family back into tile/grid repetition.
Solution: Copy all four received PNGs into the SHINOBU_361 drop zone, keep the service conduit and instrument kit as reference sources only, reject the base and height as final, and add `Prompt 1B - Base Wall Retry` to the active wall file. The retry demands a continuous pressure skin with at least 70 percent uninterrupted wall material and no repeated rounded panels.
Rejected Alternatives: Accepting the base because it is cleaner than the first seed was rejected; "less bad tile" is still the wrong wall architecture. Regenerating normal/height immediately was rejected because height must follow the accepted base wall, not the rejected one.
Scalability potential: Low tier can use a calm continuous wall material that survives mip compression; Middle can layer conduit/instrument refs as separate trims/props; High/Ultra can add dense service overlays, detail normals, and emissive accents without making the base texture noisy.
Hardware Impact: 0 us gameplay hot path. Production impact is lower probability of importing a tile-biased wall family. Runtime impact remains PENDING Unity import and material preview.

## Decision 034: Layered Wall Round 2 Pass Gate

Problem: The active wall file still contained old prompts below the Candidate A verdict, which could cause another full four-image generation pass before the base wall problem is solved.
Solution: Replace `START_HERE_WALL.md` with a single Round 2 file. `Prompt 1C` is the only immediate prompt. Service conduit, instrument atlas, and heightfield prompts remain present but gated behind an accepted base wall. `Prompt 1C` raises uninterrupted wall material from 70 percent to 85 percent and explicitly forbids connected seam grids, rounded rectangles, black outline art, and baked shadows.
Rejected Alternatives: Regenerating normal/height immediately was rejected because a height map generated from a rejected base repeats the same panel-grid error. Keeping the old prompt set was rejected because it lets stale instructions compete with current art direction.
Scalability potential: Low tier benefits from a calm base surface that survives mipmaps and compression; Middle can layer service hardware as trims; High/Ultra can add richer overlays and detail normals without making the base noisy.
Hardware Impact: 0 us gameplay hot path. Production impact is fewer bad wall iterations before Unity import. Runtime impact remains PENDING material preview and texture import.

## Decision 035: Layered Wall Round 3 Base-Only Lock

Problem: Candidate A proves that the generator keeps returning to panel-grid grammar when it sees any wall-layer prompt context. Keeping service/instrument/height prompts visible in the active file risks the next pass continuing before the base pressure skin is corrected.
Solution: Replace `START_HERE_WALL.md` with a base-only Round 3 operator file and `Prompt 1D`. The prompt requires 90-95 percent uninterrupted monolithic pressure-shell material, allows only a few non-closed seams, forbids grids/rounded rectangles/fake lighting, and freezes every other layer until the base passes.
Rejected Alternatives: Accepting the current base as "good enough" was rejected because it remains tile-biased. Running pipe/instrument/height polish now was rejected because those outputs must sit on a correct base, not rescue a wrong one.
Scalability potential: Low tier gets calm wall albedo that survives mipmaps and compression; Middle can add service overlays as separate source assets; High/Ultra can spend saved visual budget on denser placed equipment, detail normals, and emissive accents without making the base noisy.
Hardware Impact: 0 us gameplay hot path. Production impact is one fewer branch of bad derived maps before import. Runtime impact remains PENDING material preview and Unity import.
