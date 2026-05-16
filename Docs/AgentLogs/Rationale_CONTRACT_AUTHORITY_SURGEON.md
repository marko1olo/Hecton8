# CONTRACT_AUTHORITY_SURGEON Rationale

## Initial State
Problem: Mandatory state files were absent; initial required read returned missing-file errors.
Solution: Create persistent status and rationale files before C# edits so context compression cannot erase work state.
Rejected Alternatives: Chat-only memory; violates anti-amnesia protocol.
Scalability potential: Audit files do not touch runtime. Cheap devices and top-tier devices unaffected.
Hardware Impact: 0 us/frame. Disk write occurs outside runtime.

## Mandate Selection
Problem: Contract centralization touches Burst constants, AUP cell sizes, save hashes, telemetry identifiers, and frame-budget thresholds.
Solution: Bind work to PROJECT_LTS_Compatibility_Layer, OPT_Zero_GC_Policy_AllocFree_Mandate, MATH_Coordinate_Precision_AUP_FloatingOrigin, DATA_Save_Persistence_Binary_Delta_Checksum, DBG_Telemetry_Crash_Reporting_PostMortem, ARCH_Signal_Lane_Segregation, OPT_Performance_Budgets_FrameTime_VRAM_Limits, and OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.
Rejected Alternatives: Reading only the two required registry files; insufficient because the task explicitly owns AUP, save versioning, telemetry lanes, and LOD budgets.
Scalability potential: Low tier gets central caps and cheap approximations; Ultra tier gets explicit overkill caps without searching system code.
Hardware Impact: 0 us/frame for mandate selection. Runtime impact is constrained to compile-time constants or static readonly data.

## Loop 1 - Contract Authority Spine
Problem: Critical constants were split across physics math, gas dynamics, ecology balancing, AUP conversion, Homeostasis, MMF defaults, and save hashing.
Solution: Added contract classes under Core/Contracts using const aliases and private static readonly ref-readonly wrappers. Bound high-risk callers to the contracts and kept cross-domain edits to ABI-facing constants only.
Rejected Alternatives: Global search-and-replace of every number; too risky in a 20-agent dirty worktree. Inspector defaults as authority; too slow to audit and not Burst-inlineable.
Scalability potential: Low tier gets explicit hard caps and early hibernation thresholds; Ultra tier gets larger boid ceilings, wider spawn reactivation distances, and visual-overkill headroom without changing systems.
Hardware Impact: 0 us/frame for const aliases. Static validation is cold path only. Save payload grows by 16 bytes.

## Loop 1 - Compile Repair
Problem: Generated project state saw contract files through mixed Core and Core.Contracts assembly contexts; the save path could not resolve HectonContractVersion, then validator access was too narrow.
Solution: Kept a stable HectonContractVersion.cs anchor and moved the public version type into an included contract file; made HectonContractValidator public because other contract files can compile in a separate assembly.
Rejected Alternatives: Editing generated csproj; it is explicitly marked generated and would not survive Unity regeneration. Reverting save hash; violates BLACKBOX_VERSIONING.
Scalability potential: Compile repair only. No runtime scalability change.
Hardware Impact: 0 us/frame.

## Loop 2 - Human Sync And Sanity Gates
Problem: The contract Bible needs a human-readable sync path and automated impossible-value checks, or magic numbers will re-enter through markdown drift and inspector defaults.
Solution: Added a PowerShell handbook generator that parses public const declarations from Core/Contracts and regenerates Docs/ARCHITECT_HANDBOOK.md. Added NUnit edit-mode tests for finiteness, survival range bounds, ecology/scalability ratios, signal lane uniqueness, and contract version hash presence.
Rejected Alternatives: Manual documentation as source of truth; too easy to desynchronize. Runtime assertions in hot loops; violates zero-GC/frame-budget policy.
Scalability potential: Low tier keeps cheap caps testable; Middle/High/Ultra contract ratios remain visible for visual-overkill tuning without editing gameplay systems.
Hardware Impact: 0 us/frame. Generator and tests are editor/build-time only.

## Loop 3 - Omega Polish And Semantic Sweep
Problem: Post-polish scans still found semantic physical constants outside the first pass: water density, gravity, surface pressure, hydrostatic pressure, sound speed, survival gas defaults, and world Lotka defaults.
Solution: Added hydrostatic and world-ecology constants to the contracts, regenerated the handbook, mixed the new values into HectonContractVersion, and rebound targeted runtime/editor call sites. Replaced one consumer-side inverse with the math.rcp-backed ref-readonly AUP inverse.
Rejected Alternatives: Treat every 0.012f visual offset as O2 drain; false positive and cross-domain churn. Rebuilding all editor dependencies as proof; blocked by missing RealtimeCSG source files unrelated to this domain.
Scalability potential: Low tier gets identical laws with cheaper caps; Ultra tier can raise visual/entity ceilings from ScalabilityContract without changing gameplay systems.
Hardware Impact: 0 us/frame. Constants inline or static-ref copy; save payload remains +16 bytes; editor handbook/test work is offline.

## Loop 4 - Prompt Replay Re-Verification
Problem: The same assignment was replayed after the contract pass had already reached checked and polished status.
Solution: Re-extracted the XML block from CURRENT_BATCH.md, re-read AGENTS.md and the domain map, ran targeted contract scans, and rebuilt Hecton8.Core without code churn.
Rejected Alternatives: Rewriting completed contract files to create artificial progress; violates anti-refactor-loop discipline. Moving quaternion coefficients or visual offsets into physics contracts; those are authored presentation values, not engine laws.
Scalability potential: Re-verification preserves the current Low/Middle/High/Ultra contract surface and avoids bloating the authority layer with non-scaling art constants.
Hardware Impact: 0 us/frame. No runtime code changed during replay verification.
