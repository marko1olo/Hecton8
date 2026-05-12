# ASSET_JANITOR Rationale

Status: PENDING VERIFICATION
Evidence policy: `QA_Evidence_Text_Filter_Audit` applies. Static scans prove text/file presence only, not Unity import/runtime state.

## Decision 0 - Assignment Source

Problem: Batch protocol expects a disk path and `CURRENT_BATCH.md` extraction by ID, but the received assignment was inline chat XML. Initial CLI search found `Docs/Tasks/CURRENT_BATCH.md` and `<POLISH_MANDATE>`, but no `ASSET_JANITOR` prompt hit.
Solution: Treat chat XML as the primary assignment while continuing to use CLI scans for `CURRENT_BATCH.md` and recording the exception.
Rejected Alternatives: Pretending a missing disk prompt exists; reading neighboring prompts; blocking cleanup without a batch path.
Scalability potential: Low/Middle/High/Ultra unaffected; janitor work reduces CI/import entropy and protects iteration speed.
Hardware Impact: Runtime impact 0 us/frame; editor import hygiene can reduce wasted import churn on i3/MX350-class machines, but no profiler proof is claimed.

## Decision 1 - Destructive Scope

Problem: The task authorizes deletion of orphan `.meta`, temp files, and empty folders, but Unity YAML/prefab/material raw mutation is high risk.
Solution: Only delete mathematically paired or exact-match hygiene targets. Report naming, asmdef, shader/material, prefab override, and third-party isolation issues unless a safe Unity API path is available and references are proven.
Rejected Alternatives: Bulk renames, raw YAML shader rewrites, moving vendor folders, blanket prefab apply/revert.
Scalability potential: Low tier avoids reference breakage/import stalls; high tier receives the same deterministic asset graph without hidden editor debt.
Hardware Impact: 0 us/frame runtime; expected gain is editor/CI stability, not frame time.

## Decision 2 - Meta Generation

Problem: Task 2 requires missing `.meta` generation or critical reporting. Fifteen missing-meta paths are inside third-party plugin/native bundle internals and one RealtimeCSG temporary bundle path.
Solution: Triggered Unity refresh/compile request, then re-scanned. Unity did not become ready in 60 seconds and missing-meta count stayed 15, so the paths remain `[CRITICAL_SYNC_ERROR]` in `RECON_JANITOR_META.md`.
Rejected Alternatives: Hand-writing Unity `.meta` GUIDs; deleting native bundle contents; pretending Unity import verified.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. Import determinism improves only after Unity or vendor-package owners resolve the missing metas.
Hardware Impact: 0 us/frame runtime; possible editor import churn remains unmeasured.

## Decision 3 - Script and Naming Corrections Are Report-Only

Problem: The scan found 410 first-party naming violations and 3944 scripts outside `_Project/Scripts/`, but renames/moves would rewrite references, asmdefs, and vendor ownership boundaries.
Solution: Generated recon reports and made no moves. Naming and script-location repair requires a reference-aware Unity API migration, not filesystem surgery.
Rejected Alternatives: Bulk `Move-Item`; raw YAML GUID edits; renaming `Mat_` to `MAT_` without checking references; moving third-party scripts into first-party domains.
Scalability potential: Clean naming improves CI/search and asset streaming discipline. Unsafe moves would increase low-end import failures and broken references.
Hardware Impact: 0 us/frame runtime; no claimed microsecond savings.

## Decision 4 - `_ThirdParty` Architecture Root Exception

Problem: Empty-folder purge removed `Assets/_ThirdParty.meta` because the directory was empty and had no `.gitkeep`, but `AGENTS.md` declares `Assets/_ThirdParty/` as the reserved vendor isolation root.
Solution: Restored `Assets/_ThirdParty/` and its original tracked `.meta` GUID from `HEAD` as an architecture root exception. Future empty-folder scans must not delete required top-level architecture roots.
Rejected Alternatives: Leaving `_ThirdParty` missing; moving third-party vendors into a deleted target; creating a new random `.meta` GUID.
Scalability potential: Low tier and high tier both need deterministic vendor isolation for import and CI. The folder itself has no runtime load cost.
Hardware Impact: 0 us/frame runtime; preserves asset topology for future vendor migration.

## Decision 5 - Prefab Override Audit Boundaries

Problem: Task 9 asks for unapplied prefab overrides older than 48 hours. Static YAML can show `PrefabInstance` blocks, but it cannot identify Unity's current unapplied override state or the age of individual overrides.
Solution: Wrote `RECON_JANITOR_PREFAB_OVERRIDES.md` with static scene PrefabInstance counts and marked the real override-age audit `[BLOCKED BY DEPENDENCY]` until Unity Editor API inspection is available.
Rejected Alternatives: Raw `.unity` edits; blanket Apply/Revert; treating scene file modified time as override age.
Scalability potential: Correct prefab source-of-truth cleanup prevents duplicated scene overrides from bloating imports and runtime object state across all hardware tiers.
Hardware Impact: 0 us/frame now; potential future savings require measured prefab cleanup, not static claims.

## Decision 6 - Plugin Isolation Report-Only

Problem: Vendor tokens exist under `_Project` and major vendor roots still sit outside `_ThirdParty`, but moving them would rewrite GUID/reference topology and may break scenes/materials.
Solution: Generated `RECON_JANITOR_PLUGIN_ISOLATION.md` with `VENDOR_LEAK_CANDIDATE` versus `FIRST_PARTY_BRIDGE_OR_MIGRATION_REVIEW` classifications. No filesystem moves.
Rejected Alternatives: Bulk moving `Crest`, `MapMagic`, `GPUInstancer`, or bridge scripts; deleting migration data; changing references without Unity API migration.
Scalability potential: Proper vendor isolation improves CI determinism and import predictability for low-end developer machines; high-end runtime visuals unchanged.
Hardware Impact: 0 us/frame runtime; no measured microsecond savings.

## Decision 7 - README Creation and Meta Handling

Problem: Task 11 requires micro-READMEs under every major `_Project` folder, but new files under `Assets/` need `.meta` generation.
Solution: Created 17 concise README files, then triggered Unity asset refresh. Post-refresh scan found 0 missing README metas.
Rejected Alternatives: Leaving folders undocumented; hand-authoring random `.meta` GUIDs; writing long duplicated docs into asset folders.
Scalability potential: Better folder intent reduces asset import and ownership mistakes on low-end dev machines and high-end production branches.
Hardware Impact: 0 us/frame runtime.

## Decision 8 - LF Conversion Scope

Problem: Task 13 asks for all text files to use LF, but the worktree contains broad concurrent edits and 6841 CR-containing text files. Mass conversion would create uncontrolled churn across other agents' files and third-party packages.
Solution: Converted ASSET_JANITOR-owned generated reports to LF and wrote `RECON_JANITOR_LINE_ENDINGS.md` with global CR debt sample. Marked broad conversion `[BLOCKED BY CONCURRENT DIRTY WORKTREE]`.
Rejected Alternatives: Converting every `.cs`, `.asset`, `.prefab`, `.unity`, `.mat`, `.meta`, and vendor file during parallel development; touching binary-unknown files.
Scalability potential: LF consistency matters for Steam Deck/POSIX tooling, but must be enforced through `.gitattributes`/CI or a coordinated freeze.
Hardware Impact: 0 us/frame runtime.

## Decision 9 - Shader and Material Cleanup Are Read-Only

Problem: ShaderCache and material shader policy can affect stutter/SRP batching, but deleting Library cache or rewriting material shader GUIDs during active Unity work is unsafe.
Solution: Reported ShaderCache size/count and used Unity AssetDatabase to identify 129 first-party materials/subasset materials using Standard/Lit-class shaders. No deletion or material mutation.
Rejected Alternatives: Deleting `Library/ShaderCache`; raw `.mat` YAML edits; automatic material migration without visual/profiler review.
Scalability potential: Future migration to Hecton shaders can buy visual consistency and SRP batching stability across tiers.
Hardware Impact: 0 us/frame now; no measured microsecond savings.

## Decision 10 - Bounded Vigilance

Problem: Task 20 says to continue scanning until the Architect terminates the session, but this Codex turn must produce a handoff and cannot run an infinite watcher.
Solution: Performed a final bounded re-verification pass and wrote `RECON_JANITOR_CONTINUOUS_VIGILANCE.md`. Future daemon behavior should be a real editor/CI watcher, not a stuck chat turn.
Rejected Alternatives: Infinite loop in the shell; sleeping watcher process; blocking final report.
Scalability potential: A CI watcher would catch import hygiene drift before low-end machines waste time on broken imports and before high-end branches accumulate asset bloat.
Hardware Impact: 0 us/frame runtime; editor/CI time savings are unmeasured.

## OMEGA POLISH CHANGES

Problem: Final POLISH_MANDATE requires anti-bloat review and build proof after all ASSET_JANITOR tasks are checked/blocked.
Solution: Reviewed touched scope. No runtime code, Burst jobs, math loops, managed hot paths, physical simulation, `math.sqrt`, `math.normalize`, `string.Format`, or gameplay branching were introduced. Reports are static/Unity-API evidence only. ASSET_JANITOR-generated recon files were normalized to LF where practical.
Rejected Alternatives: Inventing runtime optimizations for a filesystem janitor task; mass-editing other agents' dirty files to satisfy broad line-ending policy; reporting build success despite compiler errors.
Scalability potential: Low/Middle/High/Ultra runtime is unchanged. Hygiene reports identify future import, shader, vendor, and line-ending cleanup work that can reduce CI/editor churn when executed under a coordinated freeze.
Hardware Impact: 0 us/frame measured. No frame-time savings claimed.
Cinematic Cheats Used: none in runtime; janitor used report-only/static scans instead of expensive or unsafe Unity YAML mutation.
Final Git Diff: `Docs/AgentLogs/RECON_JANITOR_FINAL_DIFF.md`.
Build Evidence: `Docs/AgentLogs/ASSET_JANITOR_omega_dotnet_build.log` failed with dependency errors outside ASSET_JANITOR edits.
STATUS: PENDING VERIFICATION, not VERIFIED MASTER GRADE.
