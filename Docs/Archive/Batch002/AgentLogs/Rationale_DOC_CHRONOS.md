# DOC_CHRONOS Rationale

Status: ACTIVE
Domain: Architecture & Audit Sync

## Session Baseline

Problem: Existing documentation may contradict current code and permanent project mandates.
Solution: Treat source code, AGENTS.md, registry mandates, and extracted DOC_CHRONOS prompt as authority. Update stable documentation with evidence tables instead of prose claims.
Rejected Alternatives: Blind prose rewrite and dated-report-first policy; both preserve poisoned documentation.
Scalability potential: Low tier docs must expose cheapest path and build gates; Ultra tier docs must expose overkill paths only when bounded by quality tiers.
Hardware Impact: Documentation-only change. Runtime gain is indirect: fewer wrong implementations targeting i3/MX350 budgets.

## Decisions

- Mandate selection capped at 8 files per AGENTS.md. Selected evidence, architecture, registry, bootstrap, zero-GC, native jobs, arena, and telemetry mandates. Rejected `DATA_Save_Persistence_Binary_Delta_Checksum.txt` as an active mandate for this pass because the file output was a single `AsyncWriteManager.Write(...)` fragment, not a usable policy body.

## Loop 1: Tasks 1-5

Problem: Existing docs described event, I/O, and domain topology with stale abstractions.
Solution: Replaced prose authority with source-scanned counters: 189 interface declaration hits, 33 `GlobalSignals` lanes, 13 MPSC `ParallelWriter` lanes, 127 aligned struct attributes, and 85 domain ids.
Rejected Alternatives: Keeping the five-artery bus model as current; it is now historical shorthand and hides the typed NativeQueue corridor.
Scalability potential: Low tier gets fewer direct callbacks and less main-thread listener churn; high tier can add visual/audio signals without changing simulation ownership.
Hardware Impact: Documentation-only. Expected indirect gain on i3/MX350 is fewer bad implementations that add 0.1-1.0 ms cross-domain callback stalls.

Problem: Boot docs used Unity lifecycle language instead of deterministic service topology.
Solution: Documented the six-stage lifecycle: Allocators -> Signals -> I/O -> Monolith -> Simulation -> Presentation, with `GameBootstrapper` dependency nodes mapped to registry slots.
Rejected Alternatives: Treating `Awake`/`Start` order as architecture; that path is nondeterministic under multi-agent integration.
Scalability potential: Low tier can skip or defer presentation work after monolith/simulation readiness; ultra tier can warm high math LOD only after core services exist.
Hardware Impact: Indirect. Reduces risk of scene search or service lookup retries during boot, which can cost multi-ms spikes on weak CPUs.

Problem: Current compile proof is blocked by source symbols outside DOC_CHRONOS edits.
Solution: Ran required build gate, classified failures as dependency blockers, and shut down build servers. Did not edit unrelated C# to mask architecture debt.
Rejected Alternatives: Reporting stale May 11 success, or making code changes outside documentation scope to chase another agent's platform/native work.
Scalability potential: Accurate blocker classification keeps low-end build validation from being polluted by false doc reports.
Hardware Impact: No runtime impact. Build gate consumed local CPU only; follow-up agents must repair platform/native symbols.

## Loop 2: Tasks 6-10

Problem: Compile debt was scattered through other agents' rationale/log files.
Solution: Added an Archivarius table listing all `[BLOCKED BY DEPENDENCY]` hits found by `rg`.
Rejected Alternatives: Editing other agents' AgentLogs, which is forbidden for DOC_CHRONOS.
Scalability potential: Build triage becomes deterministic; low-tier validation does not waste repeated full-build passes on known blockers.
Hardware Impact: Avoids repeated 45-120 second local build attempts on the i5-class host when dependency classes are already known.

Problem: Silo leaks are not single-file proof; regex can over-report presentation terms.
Solution: Recorded leak candidates as owner-review items, separated UI->Voxel, UI->Save, UI->Fauna/Ecosystem, and Core->UI surfaces.
Rejected Alternatives: Refactoring code in a documentation audit, or declaring every regex hit a confirmed violation.
Scalability potential: Correct cleanup path moves UI to snapshots/signals, reducing per-frame polling and direct owner coupling.
Hardware Impact: Prevents future frame-lane costs in the 100-1000 us range on weak CPUs when UI asks simulation owners directly.

Problem: The contract document still read like product policy instead of Burst-facing system law.
Solution: Inserted a Burst/Zero-GC override: unmanaged payloads, no managed references in jobs, no hot allocations, no singleton reads, no frame-critical `Complete()`.
Rejected Alternatives: Waiting for a full rewrite of every legacy section; the top override now controls contradictions.
Scalability potential: Low tier gets alloc-free hot paths; ultra tier can spend saved CPU/GPU budget on visible overkill.
Hardware Impact: Indirect. Prevents common managed allocation and sync-point costs that can exceed 0.1 ms.

Problem: Scalability docs did not connect C# math LOD to shader keywords.
Solution: Created a matrix tying `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, `_MATH_LOD_LOW`, `_MATH_LOD_HIGH`, `_QUALITY_MX350`, and `_QUALITY_HIGH`.
Rejected Alternatives: Static quality tiers without source keyword proof.
Scalability potential: Low, Middle, High, and Ultra paths are explicit and can be tested independently.
Hardware Impact: Low-tier path should recover hundreds of microseconds by disabling additional lights/samples and using cheap math branches.

Problem: Arena allocator docs lacked slab/bump pointer mechanics.
Solution: Documented double-buffer layout, per-thread slab selection, CAS cursor bump, alignment normalization, OOM telemetry, and `EndFrameSwap`.
Rejected Alternatives: Calling it a "pool" without cache-line, frame-lifetime, or telemetry rules.
Scalability potential: Low tier avoids allocator jitter; high tier gets wider scratch use for richer visuals without GC.
Hardware Impact: Indirect. Avoids allocator spikes under dense scratch allocations and keeps native memory ownership diagnosable.

## Loop 3: Tasks 11-16

Problem: Registry docs allowed the reader to confuse service location with singleton ownership.
Solution: Created `GLOBAL_REGISTRY_SERVICE_LOCATOR.md` with bootstrap-only registration, interface retrieval, listener bucket ownership, and singleton terminal-offense rules.
Rejected Alternatives: Normalizing `Instance` access as a convenience layer; it bypasses startup order, teardown order, and Burst-safe data snapshots.
Scalability potential: Low tier avoids scene searches and lazy singleton repair; ultra tier can add presentation services without changing simulation ownership.
Hardware Impact: Indirect. Prevents 100-500 us lookup/rebind stalls on i3/MX350-class CPUs.

Problem: Build protocol was not permanent enough to stop stale compile reports.
Solution: Promoted the queue rules into `QUALITY_GATES.md`: one active compile owner, `--no-restore`, `/nr:false`, build-server shutdown, and blocker classification.
Rejected Alternatives: Trusting IDE success, stale logs, or parallel agents running restore-heavy builds.
Scalability potential: Low tier validation gets deterministic build ownership; high tier agents can still run targeted tests after the compile lane is clear.
Hardware Impact: Saves repeated 45-120 second failed build passes on the local i5-class host.

Problem: The non-ASCII/Cyrillic state had old contradictory claims and one active path violation in the audited docs.
Solution: Recorded current counts in the Archivarius report and converted DOC_CHRONOS report path displays to ASCII-safe placeholders.
Rejected Alternatives: Renaming files during a documentation audit, or inserting Cyrillic paths into a new active report.
Scalability potential: Low tier build/package scripts avoid encoding edge cases; ultra tier localization remains a data concern, not a path hygiene exception.
Hardware Impact: No runtime effect. Prevents file-system and package pipeline ambiguity.

Problem: The atlas did not expose current assembly ownership.
Solution: Updated `PROJECT_ATLAS.md` with 2026-05-12 source counts and 13 first-party asmdefs.
Rejected Alternatives: Relying on legacy tree descriptions and obsolete domain counts.
Scalability potential: Low tier agents avoid wrong compile targets; ultra tier subsystem owners can add asmdefs without hiding dependencies.
Hardware Impact: Indirect. Reduces bad-target build attempts and ownership lookup overhead.

Problem: Data Monolith documentation risked repeating the "FileStream everywhere" lie.
Solution: Created `DATA_MONOLITH_H8BIN_SPEC.md` with the 16-byte header, 64-byte directory, 16-byte section entries, 24 sections, 16-byte section alignment, and 32-bit FNV-1a hashing contract.
Rejected Alternatives: Claiming the `.h8bin` runtime path is POSIX/FileStream-complete; source still stages via `File.ReadAllBytes`.
Scalability potential: Low tier gets one compact native database after boot; ultra tier can add dense static sections only through fixed layout expansion.
Hardware Impact: Prevents future boot and streaming work from targeting false APIs.

Problem: Replay determinism needed fixed-record proof, not a feature promise.
Solution: Created `CORE_REPLAY_DETERMINISM.md` for the circular FileStream file, 499 MB cap, 10-frame snapshot interval, panic ring, sidecar lanes, FNV64 hashing, and writer-thread boundaries.
Rejected Alternatives: Certifying determinism without a green compile and runtime replay pass.
Scalability potential: Low tier records bounded state; ultra tier can widen diagnostic payloads only by explicit capacity changes.
Hardware Impact: Indirect. Cuts crash triage from unbounded log search to fixed-record inspection.

Problem: Compile status after tasks 11-16 still needed proof.
Solution: Re-ran the build gate and classified the unchanged failure families as dependency blockers.
Rejected Alternatives: Editing unrelated platform/native/voxel code from a documentation auditor role.
Scalability potential: Keeps architecture docs honest while source owners repair their lanes.
Hardware Impact: No runtime effect. Build gate remains blocked by external missing symbols, not DOC_CHRONOS markdown.

## Loop 4: Tasks 17-20

Problem: The first documentation pass could still carry hidden contradictions after source truth changed.
Solution: Re-ran the internal proof path: `GlobalSignals` recount, Data Monolith I/O check, stale-build sentence scan, non-ASCII check, and stable doc existence check.
Rejected Alternatives: Declaring the first pass final without a second source scan.
Scalability potential: Low tier agents stop implementing against false I/O/event/build assumptions; ultra tier work can target the actual high-detail lanes.
Hardware Impact: Indirect. Prevents wasted build and implementation passes that cost minutes, not microseconds.

Problem: A stable architecture file still carried May 11 green-build proof as current truth.
Solution: Replaced it with the May 12 DOC_CHRONOS compile boundary: `111` C# errors, `3` warnings, `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: Keeping historical green proof beside current failed proof without hierarchy.
Scalability potential: Compile triage now routes to the missing platform/native/voxel symbol owners instead of documentation.
Hardware Impact: Saves repeated false verification on the local i5-class host.

Problem: The forensic summary contained adjective-heavy verdict language and mojibake.
Solution: Converted the affected paragraphs to tables and ASCII text.
Rejected Alternatives: Leaving player-facing probability language as architecture evidence.
Scalability potential: Low tier and ultra tier claims now require source/runtime proof instead of adjectives.
Hardware Impact: No runtime effect. Reduces reviewer ambiguity and grep/encoding failure.

Problem: AGENTS.md forbids runtime readiness claims from docs, static scans, or local build.
Solution: Kept `VERIFIED MASTER GRADE` scoped to the documentation audit and retained runtime status as `PENDING VERIFICATION`.
Rejected Alternatives: Unqualified success status, which would contradict the project authority spine.
Scalability potential: Verification remains staged: source docs first, then Unity import, Console, Play Mode, profiler, GC, memory, and player build.
Hardware Impact: Prevents false profiler/build work on unresolved source blockers.

Problem: Final compile proof was still required after omega edits.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false -v:quiet -clp:Summary` and shut down build servers.
Rejected Alternatives: Skipping compile because only markdown changed.
Scalability potential: The compile lane remains classified and reproducible for the integrator.
Hardware Impact: Build remains blocked by external missing symbols: `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `SteamDeckInputPal`, `HectonNativeBridge`, `HectonNativeLibrary`, `VoxelChunkModifiedEvents`, plus related haptics/hardware tier symbols.

## OMEGA POLISH CHANGES

Problem: The OMEGA mandate asks for runtime math, Burst branch, division, and managed-loop audits, but DOC_CHRONOS edited documentation only.
Solution: Scoped the polish to the actual implementation surface: markdown authority files, status/rationale state, build-gate proof, ASCII hygiene, stable-authority contradictions, and final diff evidence.
Rejected Alternatives: Inventing code changes to satisfy a code-oriented polish block, or claiming runtime microseconds from documentation edits.
Scalability potential: `SCALABILITY_MATRIX.md` now gives Low/Middle/High/Ultra paths so runtime owners can apply dominant-axis, bitmask, dither, shader-keyword, and high-tier visual-overkill paths without reading obsolete prose.
Hardware Impact: Exact measured runtime gain from DOC_CHRONOS edits is `0 us` because no runtime code changed. Indirect avoided costs recorded in `Status_DOC_CHRONOS.md` remain estimates until profiler proof exists.

Cinematic cheats used:
- documentation-level cheat: replaced adjective verdicts with source tables and bounded verification states
- architecture-level cheat documented: low-tier math/shader paths use `_MATH_LOD_LOW` and `_QUALITY_MX350`; high-tier paths use `_MATH_LOD_HIGH` and `_QUALITY_HIGH`
- runtime cheat implemented by DOC_CHRONOS: none

Final diff evidence:
- stable docs changed: forensic summary, architecture map, systems contracts, quality gates, save/streaming architecture docs, project atlas
- new DOC_CHRONOS docs: signal corridor, boot topology, arena allocator, registry, scalability matrix, data monolith, replay determinism, Archivarius truth-sync report
- agent state docs: `Status_DOC_CHRONOS.md`, `Rationale_DOC_CHRONOS.md`, `LOG_DOC_CHRONOS.md`

STATUS: VERIFIED MASTER GRADE FOR DOCUMENTATION AUDIT / RUNTIME PENDING VERIFICATION
