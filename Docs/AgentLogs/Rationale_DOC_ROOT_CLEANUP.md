# Rationale: DOC_ROOT_CLEANUP

Date: 2026-05-15
Domain: Documentation Hygiene / Echelon 9 Meta, Polish & Integration
Evidence class: STATIC_DOC + FILESYSTEM

## Decision 1: Scope

Problem: Repository root contains documentation/report artifacts beyond the approved root anchors.

Solution: Apply `Docs/DOC_GOVERNANCE.md`: keep only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md` as active root documentation anchors. Move current reports into `Docs/Reports`, and raw logs/evidence artifacts into a dated deprecated evidence bundle.

Rejected Alternatives: Moving generated Unity project files or caches into Docs was rejected; those are build/tooling outputs, not documentation. Deleting files was rejected because the user requested sorting, not destruction. Bulk "misc" folder was rejected by governance naming rules.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. The gain is context hygiene: agents load fewer irrelevant root docs.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed. This is filesystem/documentation hygiene only.

## Decision 2: Verification Language

Problem: Cleanup can be proven by filesystem state, not by runtime execution.

Solution: Report evidence as FILESYSTEM / STATIC_DOC only. No `0 GC`, compile, profiler, or runtime readiness claim will be made unless matching artifacts are produced.

Rejected Alternatives: Fake microsecond tables and "verified runtime" wording were rejected under `QA_Evidence_Text_Filter_Audit.txt`.

Scalability potential: Same as above; no runtime path changed.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 3: Classification Targets

Problem: Root noise is not one category. Some files are active report workstream outputs, some are compatibility mirrors, and some are raw evidence or stale tools.

Solution: Move `COMPUTE_*.md` into `Docs/Reports/2026-05-15_COMPUTE_AUDIT/` because they are same-day report slices. Move `BROKEN_PREFABS.md` into `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md` because it is a generated prefab snapshot with a static-proof boundary. Move root `PROJECT_ATLAS.md` and `TERRAIN_AND_BIOME_REALITY_MAP.md` into `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/` because canonical Docs copies already exist and the root copies are not authority. Move raw `.log`, `.json`, `.xml`, `.png`, `.zip`, and `clean_project.py` into `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/`.

Rejected Alternatives: `Docs/misc` was rejected by `DOC_GOVERNANCE.md`. Moving `.csproj`, `.slnx`, `Directory.Build.*`, Unity `Library/`, `Temp/`, or generated `.lscache` files was rejected because this task is documentation/evidence sorting, not build-system relocation. Deleting `clean_project.py` was rejected even though it is destructive/stale; preservation in deprecated evidence is safer.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Agent context load becomes cleaner because root report clutter no longer masquerades as project anchors.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 4: Index Updates

Problem: Moving files without updating stable docs would leave stale root counters and paths in the project brain.

Solution: Update `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/README.md` to state the May 15 root cleanup result and the new canonical/deprecated paths. Add README files inside each new bundle so future agents can classify the moved material without reading every payload file.

Rejected Alternatives: Leaving May 13 counters untouched was rejected because it would create false root hygiene debt. Editing historical reports was rejected; historical dated reports should remain evidence snapshots.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Documentation navigation becomes cheaper and less ambiguous.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 5: Compile Check Scope

Problem: Documentation moves do not change source inputs, but the project protocol prefers compile evidence after a workspace change.

Solution: Run a scoped Core no-restore CLI build after the filesystem cleanup: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. Result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`. Evidence class: CLI_COMPILE.

Rejected Alternatives: Skipping all build evidence was rejected because the workspace protocol asks for compile checks. Claiming Unity runtime readiness from this CLI build was rejected because it is not Unity Console, Play Mode, profiler, GCMonitor, player-build, or visual proof.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.
