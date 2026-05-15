# TECH_RESEARCHER Rationale - HECTON_MASTER_PROJECT_ATLAS

## Decision 1 - Scope Boundary

Problem: The prompt demands a global architecture atlas while 20+ agents may be editing runtime systems concurrently.
Solution: Treat this pass as STATIC_SOURCE / STATIC_DOC evidence. Generate an atlas from current disk state, avoid runtime code edits, and validate path references with a deterministic Python checker.
Rejected Alternatives: Editing contracts or creating new signal lanes would mutate public API during a batch and create integration risk.
Scalability potential: Low/Middle/High/Ultra unchanged because this pass does not alter runtime. It improves integrator visibility for tiered decisions.
Hardware Impact: 0 us runtime impact on i3/MX350. Offline scan only.

## Decision 2 - Evidence Grade

Problem: Static scans can expose dependencies and strings but cannot prove Unity import, Play Mode, profiler, or GC health.
Solution: Every atlas section must label evidence as STATIC_SOURCE or STATIC_DOC and leave runtime claims as PENDING VERIFICATION.
Rejected Alternatives: Claiming "verified" from grep results violates `QA_Evidence_Text_Filter_Audit.txt`.
Scalability potential: Prevents false assumptions about MX350 and high-tier behavior.
Hardware Impact: 0 us runtime impact. Avoids unsafe optimization claims.

## Decision 3 - Atlas Integrity Tool

Problem: A large markdown atlas can decay immediately if it references missing files.
Solution: Write `Tools/AtlasCheck.py` to parse markdown path references and fail on missing files, while ignoring external URLs and wildcard/glob fragments.
Rejected Alternatives: Rely on human review. Too error-prone for a 1.6M LOC project.
Scalability potential: Low devices are protected indirectly by keeping dependency ownership clear; high-tier visual systems can be mapped without guessing.
Hardware Impact: 0 us runtime impact. Offline tool expected under 500000 us on this workspace.

## Decision 4 - Resource-Limited Scan Boundary

Problem: The first full `Assets/` + `Packages/` C# walk hit local resource limits before it could write the atlas.
Solution: Switch the source scan to first-party `Assets/_Project/Scripts/` for code ownership and keep all `.asmdef` files under `Assets/` and `Packages/` for assembly dependency edges.
Rejected Alternatives: Keep retrying the full third-party C# walk until the machine stalls again; claiming a full package LOC count without successful scan.
Scalability potential: Low/Middle/High/Ultra architecture decisions should be based on first-party ownership and package dependency boundaries, not vendor source LOC volume.
Hardware Impact: 0 us runtime impact. Offline scan completed without further resource failure; first-party map covers 939,903 C# lines.

## Decision 5 - Exact PHI_SYN Missing

Problem: The prompt requires reading Rationale_PHI_SYN, but the exact active log file is absent.
Solution: Record the absence as evidence, scan near-match H-Phi rationale logs only as supporting context, and map only signals found in source.
Rejected Alternatives: Inventing a missing rationale file, treating HPHI_SYNAPTIC_FORGER as a literal substitute without disclosure, or failing the whole atlas.
Scalability potential: Low/High tier behavior unchanged; prevents false interface claims from a missing artifact.
Hardware Impact: 0 us runtime impact.

## Decision 6 - Dependency Inversion Watchpoint

Problem: `Hecton8.Core` currently references many domain assemblies directly, while the architecture mandates a stable core/interface spine.
Solution: Record the current asmdef truth in `Docs/DEPENDENCY_GRAPH.md` and flag it as an integrator watchpoint instead of changing public API during the batch.
Rejected Alternatives: Mutating asmdefs from the atlas role or hiding the outward references.
Scalability potential: A cleaner dependency spine would reduce cross-domain compile pressure and let Low/Ultra paths attach consumers through contracts rather than direct domain references.
Hardware Impact: 0 us runtime impact in this pass; compile-time and integration stability benefit is pending integrator action.

## Decision 7 - Compile Boundary

Problem: The workflow asks for compilation, but the current root scan found no `.sln` or `.csproj` files to build from this workspace state.
Solution: Run Python compile and atlas integrity gates; mark C# compile unavailable instead of fabricating a green build.
Rejected Alternatives: Claiming previous agents' build logs as this pass, or editing/generated project files outside scope.
Scalability potential: Accurate verification boundaries prevent bad Low/MX350 runtime conclusions from static docs.
Hardware Impact: 0 us runtime impact. Verification status is honest: Python/static pass only.

## Decision 8 - Repeatable Atlas Generator

Problem: The first atlas was generated from an inline one-off scanner. That left the markdown auditable but not cheaply reproducible.
Solution: Add `Tools/BuildArchitectureAtlas.py` as the owned generator and make it write `Docs/DEPENDENCY_GRAPH.md` from current disk state.
Rejected Alternatives: Leave the generation logic in chat/tool history or require manual markdown edits.
Scalability potential: Low/Middle/High/Ultra architecture maps can be refreshed after each batch without manual drift.
Hardware Impact: 0 us runtime impact. Offline generation completed in 139.5 seconds on this machine.

## Decision 9 - Full Source Scale With Fast Enumeration

Problem: The prompt explicitly says 1.6M LOC. The first successful atlas only counted first-party source because full Python tree walking stalled.
Solution: Use `rg --files` from the generator for C# enumeration, with a Python fallback. Count all `Assets/` + `Packages/` C# files by bytes, but parse signal/namespace ownership only for first-party source.
Rejected Alternatives: Regex-parse all vendor/package source for Hecton signal ownership, which timed out and adds no first-party architecture value; keep the first-party-only count.
Scalability potential: Low/MX350 and Ultra decisions now have accurate project-scale context while keeping ownership mapping focused on controllable first-party code.
Hardware Impact: 0 us runtime impact. Offline source scale is now 5,034 C# files and 1,709,155 lines; first-party ownership is 1,505 files and 960,494 lines.

## Decision 10 - Tool Self-Tests

Problem: Atlas generation and validation were reproducible, but the helper behaviors had no unit tests.
Solution: Add `Tools/test_architecture_atlas.py` covering path normalization, markdown reference collection, signal-name normalization, line-number math, and required atlas sections.
Rejected Alternatives: Relying only on a full atlas generation run. That checks current output but not small parser behaviors that can silently rot.
Scalability potential: Low/Middle/High/Ultra architecture refreshes keep the same validation contract as the atlas grows.
Hardware Impact: 0 us runtime impact. Unit tests ran in 0.003 seconds reported test time; wall time was tool-shell overhead only.

## Decision 11 - Machine-Readable Atlas Sidecar

Problem: The markdown atlas is readable, but downstream integrator tooling would have to scrape tables to verify dependency and signal data.
Solution: Emit `Docs/DEPENDENCY_GRAPH.json` from `Tools/BuildArchitectureAtlas.py` using the same source scan as the markdown, then extend `Tools/AtlasCheck.py` to validate path references in both artifacts.
Rejected Alternatives: Force the GRAND_INTEGRATOR to parse markdown, or create a second hand-maintained JSON file that could drift from the atlas.
Scalability potential: Low/Middle/High/Ultra architecture refreshes now have a stable machine-readable contract; low-tier VRAM and signal-lane audits can consume the same data as high-tier visual-overkill planning without manual transcription.
Hardware Impact: 0 us runtime impact on i3/MX350. Offline verification expanded from 179 markdown references to 5,651 markdown+JSON+cache references.

## Decision 12 - Generator Reproducibility Under Workspace Load

Problem: A final `python Tools/BuildArchitectureAtlas.py` run timed out under shell/session load even though the already-generated atlas remained integrity-clean.
Solution: Profile generator sections with unbuffered Python, identify `scan_source` as the expensive section, then rerun the actual generator command with `python -u` and a long guard. The command completed and wrote both atlas artifacts.
Rejected Alternatives: Ignore the timeout because the files already passed validation, or hide the timeout from the handoff report.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. Offline atlas refresh is now characterized: source scanning is the cost center; markdown/JSON emission is cheap.
Hardware Impact: 0 us runtime impact on i3/MX350. Latest reproducible generator run completed in 153.4 s wall time; `scan_source` profiling ranged from 130.641 s to 261.070 s under session load.

## Decision 13 - Incremental Atlas Source Cache

Problem: Re-running the atlas generator still spent too much wall time scanning unchanged C# files during multi-agent handoff.
Solution: Add `Docs/DEPENDENCY_GRAPH.cache.json` as a guarded per-file source-analysis cache. Cache reuse requires matching file size, mtime, and first-party classification; changed or uncached files are still re-read and re-parsed. Replace per-file `Path.resolve()` formatting with lexical repo-relative conversion.
Rejected Alternatives: Trust stale atlas output without regeneration, or use an unsafe unguarded cache that could hide changed signal lanes.
Scalability potential: Low/Middle/High/Ultra runtime remains unchanged. Offline refresh is cheaper, so integrators can regenerate the architecture map more often during concurrent agent edits.
Hardware Impact: 0 us runtime impact on i3/MX350. Cold-cache generator run completed in 221.3 s; warm-cache generator run after path optimization completed in 95.5 s; refactored warm generator completed in 63.4 s; profiled `scan_source` warm section dropped to 22.696 s.

## Decision 14 - Cache Key Integrity Validation

Problem: The atlas checker validated markdown and JSON sidecar references, but the source-analysis cache could still retain a deleted C# path without being caught.
Solution: Extend `Tools/AtlasCheck.py` to parse `Docs/DEPENDENCY_GRAPH.cache.json`, validate cache `files` keys as repo paths, and fail on invalid keys before missing-file checks. Refactor source analysis into `analyze_source_bytes()` so unit tests can validate parser behavior without sandbox-denied temp-file writes.
Rejected Alternatives: Trust cache keys because the generator writes them, or keep tests dependent on OS temp writes under a restricted sandbox.
Scalability potential: Low/Middle/High/Ultra runtime unchanged. Integrator tooling can now trust that the cache artifact itself is not silently pointing at deleted source files.
Hardware Impact: 0 us runtime impact on i3/MX350. Atlas integrity coverage now validates 5,651 markdown+JSON+cache references.
