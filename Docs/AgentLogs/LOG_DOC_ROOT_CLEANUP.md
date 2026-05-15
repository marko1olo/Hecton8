# LOG: DOC_ROOT_CLEANUP

Date: 2026-05-15
Evidence class: FILESYSTEM + STATIC_DOC + CLI_COMPILE

## Root Documentation Cleanup

What was wrong:

- Repository root contained non-anchor reports and evidence artifacts even though `Docs/DOC_GOVERNANCE.md` allows only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md` as active root documentation anchors.
- Root had same-day `COMPUTE_*.md` report slices, stale/generated snapshots, compatibility mirrors, raw logs, JSON/XML/PNG/zip artifacts, and a destructive legacy cleanup script.

What was done:

- Moved `18` root `COMPUTE_*.md` files into `Docs/Reports/2026-05-15_COMPUTE_AUDIT/`.
- Moved `BROKEN_PREFABS.md` into `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md`.
- Moved root `PROJECT_ATLAS.md` and `TERRAIN_AND_BIOME_REALITY_MAP.md` into `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`.
- Moved root raw evidence/log/artifact files into `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/`.
- Added README files to the new report/deprecated bundles.
- Updated `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/README.md` with the May 15 cleanup boundary.
- Left Unity-generated `.csproj`, `.slnx`, `Directory.Build.*`, `.lscache`, `Library/`, `Temp/`, and build/tooling directories in place.

Cinematic Cheats used:

- Not applicable. No simulation, rendering, physics, water, fog, light, deformation, particles, or gameplay runtime path was changed.

Exact Microseconds saved:

- Runtime: `0` claimed. No profiler evidence was collected and no runtime path was changed.
- Process/context: qualitative only; root context clutter reduced from `19` root markdown files plus raw artifacts to `3` root markdown anchors in the filtered documentation/evidence scan.

Verification:

- Filesystem scan after cleanup: only `AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md` remain in root for `.md/.log/.json/.xml/.png/.zip/.txt/.py` cleanup scope.
- `Docs/Reports/2026-05-15_COMPUTE_AUDIT/` contains `19` files including its README.
- `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/` contains `12` files including its README.
- Command: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Boundary: CLI_COMPILE only. Not Unity Console, Play Mode, profiler, GCMonitor, player-build, scene-wiring, or visual proof.
