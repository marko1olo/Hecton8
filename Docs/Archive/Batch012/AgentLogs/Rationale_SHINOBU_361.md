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
