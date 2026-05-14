# Rationale - BLACKBOX_TELEMETRY_VISUALIZER

## Decision 001 - Tooling Boundary
Problem: The prompt needs instant visualization for Unity-produced telemetry, but Task 15 forbids C# edits and active `Docs/AgentLogs` has no dump/QA artifacts.
Solution: Build a standalone FastAPI reader that tolerates missing files, parses known binary contracts when files appear, and exposes a static JSON contract to the frontend.
Rejected Alternatives: Editing Unity telemetry exporters was rejected by the explicit NO C# task. Relying on Unity Editor windows was rejected because the Lead Architect needs browser access without opening Unity.
Scalability potential: Low/Middle/High/Ultra all run outside gameplay; cheap devices read only latest capped rows and bounded dump records, strong machines can keep charts dense without touching Unity runtime.
Hardware Impact: On i3/MX350, zero gameplay-frame impact because all work is external Python I/O. Dashboard poll cost is isolated to the auxiliary process.

## Decision 002 - Mandate Set
Problem: The task crosses telemetry, binary persistence, UI readouts, and evidence reporting but is not a gameplay hot path.
Solution: Loaded telemetry post-mortem, save/binary persistence, performance budget, zero-GC, UI data streaming, QA evidence, and cinematic cheat mandates; applying them as parser correctness and evidence-label rules.
Rejected Alternatives: Loading all 60+ mandates was rejected as context pollution. Treating dated archive reports as authority was rejected by registry authority order.
Scalability potential: Parser and UI stay bounded by row/entry caps, leaving room for richer frontend visuals on high-end machines without changing dump producers.
Hardware Impact: Avoids Unity allocations entirely; expected low-end gain is operational, not frame-time: no Excel/Unity launch needed to inspect crash telemetry.

## Decision 003 - Binary Parser Contract
Problem: The prompt names `Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin`, but active source writes multiple incompatible dump layouts: generic HECTON8 header+64-byte records, raw `MemoryDefragTelemetryEntry` rings, thermal records manually written without reserved bytes, and ecology/headless formats with custom magic headers.
Solution: Implement per-file-layout parsers keyed by magic/header/name with safe unknown fallback. Memory defrag supports both pack-1 64-byte and aligned 72-byte interpretation because `GlobalDataVault.MemoryDefragTelemetryEntry` currently declares sequential layout without explicit Pack.
Rejected Alternatives: A single SaveData-derived DTO parser was rejected because these dump producers are not all `SaveData.cs` DTOs. Editing C# emitters to normalize headers was rejected by Task 15.
Scalability potential: Low tier reads capped files and latest 600 CSV rows. High/Ultra can display dense charts and multiple dumps without game-runtime cost.
Hardware Impact: 0 us gameplay frame impact. External local parsing is estimated at 2,000-8,000 us per refresh on low-end storage for typical small logs, PENDING MEASUREMENT.

## Decision 004 - H-Phi Evidence Label
Problem: H-Phi is a static architecture metric in current docs, not a live runtime signal.
Solution: Prefer CSV H-Phi if a QA log emits it; otherwise parse `Docs/Reports/HECTON_PHI_REPORT.md` and label it `STATIC_DOC`.
Rejected Alternatives: Rendering an optimistic runtime score was rejected as a fake report. Hiding the gauge when runtime data is absent was rejected because Task 6 requires a prominent gauge.
Scalability potential: All tiers use the same scalar. Strong devices gain visual density, not a different metric.
Hardware Impact: 0 us Unity frame impact; one report regex scan in the Python process.

## Decision 005 - Polling Instead Of WebSockets
Problem: Telemetry arrives as files on disk, not as an in-process event stream.
Solution: Poll `/api/summary` every 2 seconds from the browser. The backend reads capped CSV rows and dump files on demand.
Rejected Alternatives: WebSockets and file watcher daemons were rejected because they add lifecycle state without a push source. Reloading the page manually was rejected because the prompt requires auto-refresh.
Scalability potential: Low tier receives small bounded JSON. High/Ultra can render denser Chart.js datasets if caps are raised later.
Hardware Impact: 0 us Unity frame impact; one auxiliary local HTTP request per 2 seconds.

## Decision 006 - Memory Map Honesty
Problem: `GlobalDataVault` defrag dumps contain aggregate fragmentation telemetry, not a full block descriptor table. `H8Memory.DumpAllocationTableText` contains occupied allocation records but no true free-list geometry.
Solution: Render exact occupied records from text allocation tables when present. For defrag-only binary summaries, render estimated occupied/free bands and mark them as estimated in the API.
Rejected Alternatives: Fabricating precise block positions was rejected as a false forensic view. Hiding the memory map until exact descriptors exist was rejected because Task 7 requires a visual.
Scalability potential: Low tier renders compact cell map. High/Ultra can display more cells and multiple dump tracks later.
Hardware Impact: 0 us Unity frame impact. External browser rendering only.

## Decision 007 - Dependency Guard
Problem: Python 3.14 in this environment has no FastAPI by default, and pinned pandas 2.2.3 is unsafe on Python 3.13+ because it can fall back to slow source builds or unsupported wheels.
Solution: Install FastAPI/uvicorn for local verification, keep pandas pinned behind `python_version < "3.13"`, and keep server CSV parsing on stdlib.
Rejected Alternatives: Blocking dashboard startup on pandas was rejected because pandas is not required by the implemented reader. Removing pandas entirely was rejected because the prompt explicitly listed it as an expected requirements item.
Scalability potential: Low devices avoid pandas import/startup cost. High/Ultra workstations on supported Python can still install pandas for future heavier analysis.
Hardware Impact: 0 us Unity frame impact. Dependency install is tooling-time only.

## Decision 008 - Frame Graph Fallback Source
Problem: The first pass only fed the frame graph from CSV. Generic blackbox dumps also carry `DeltaTime`, so a crash dump without `QA_Endurance_Log.csv` would leave the frame graph empty.
Solution: Add dump-derived `frameSeries` from generic HECTON8 blackbox entries and use it when CSV frame data is absent.
Rejected Alternatives: Keeping CSV-only graph was rejected because the dashboard is explicitly for both `Dump_*.bin` and QA CSV. Mixing dump and CSV series in the same graph was rejected for now because source cadence can differ and would make spike timing ambiguous.
Scalability potential: Low tier still reads the same capped 600 points. High/Ultra can raise caps later for longer forensic windows.
Hardware Impact: 0 us Unity frame impact. External parser adds one linear pass over already-loaded dump entries.

## Decision 009 - API Payload Bound And Memory Map Evidence Priority
Problem: Dump parsers decoded bounded files but returned every decoded entry in `/api/summary`, and memory map rendering selected the first dump by filename even if that dump was only an estimated defrag summary.
Solution: Cap returned dump entry arrays to the latest 600 records while preserving each parser's `latest` field from the full decoded set. Sort memory maps so source H8Memory allocation tables are displayed before fully estimated defrag maps, and expose the selected map evidence label in the UI.
Rejected Alternatives: Raising the binary size cap was rejected because it increases browser JSON cost without improving the last-300-frame forensic use case. Always showing the defrag map was rejected because it hides more exact allocation-table evidence when both exist.
Scalability potential: Low tier receives bounded JSON and prefers truthful source tables. Middle/High/Ultra can later raise caps or add multi-map tabs without changing Unity dump emitters.
Hardware Impact: 0 us Unity frame impact. On low-end i3/MX350, the browser avoids unbounded JSON/render cost during 2-second polling; exact dashboard cost remains PENDING MEASUREMENT.

## Decision 010 - Source-Contract Parser Extension
Problem: Re-auditing active C# dump writers showed additional known layouts: `Dump_SWARM_MACRO_MIGRATION_DIRECTOR.bin`, `Dump_ECOLOGY_MUTATION_DIRECTOR.bin`, `.h8dump` crash exports, and `runtime_telemetry.bin`. Leaving them as unknown would make the dashboard less forensic than the active source permits.
Solution: Add explicit little-endian parsers for macro-swarm, fauna-mutation, and `TELM` live telemetry. Extend collection to `.h8dump`, `BLACKBOX_CRASH.*`, and `runtime_telemetry.bin` when they are placed under `Docs/AgentLogs`. Keep persistentDataPath discovery out of scope because the dashboard has no reliable Unity `Application.persistentDataPath` at standalone Python runtime.
Rejected Alternatives: Scanning arbitrary user profile folders for persistent data was rejected as fragile and outside the prompt's `Docs/AgentLogs` workflow. Guessing every `Dump_*.bin` layout was rejected; only source-proven writer contracts were added.
Scalability potential: Low tier still receives capped JSON and one live telemetry record. High/Ultra can add dedicated macro/mutation panels later without changing the parser contract.
Hardware Impact: 0 us Unity frame impact. External parser cost grows only when those files exist and remains bounded by `MAX_DUMP_BYTES` and `MAX_DUMP_ENTRIES`; exact auxiliary-process cost is PENDING MEASUREMENT.

## Decision 011 - H-Phi Report Parsing Recovery
Problem: Reconstructing the missing dashboard directory introduced an H-Phi parser regression: the first HTTP check reported `2026.0` from the report date, and the first regex fix captured the formula multiplier `0.535` instead of the final assigned score.
Solution: Replace broad document-wide regex parsing with line-based parsing that only considers lines containing `H-Phi`/`HPhi` and reads the numeric value after the final equals sign.
Rejected Alternatives: Hardcoding `0.00062` was rejected because future reports may change. Keeping broad regex was rejected because it demonstrably accepted dates and intermediate formula operands.
Scalability potential: All tiers get the same evidence-labeled scalar. High/Ultra visual density is separate from metric truth.
Hardware Impact: 0 us Unity frame impact. One short text scan in the external dashboard process; exact auxiliary cost is PENDING MEASUREMENT.

## Decision 012 - Ledger Recovery After Directory Loss
Problem: During restart recovery, the untracked `Tools/TelemetryDashboard` directory disappeared from the filesystem while the dashboard task was still active. Reconstructed code passed, but the task ledger reverted to a stale pending verification line.
Solution: Recreate all dashboard files, rerun smoke/HTTP checks, and explicitly update status/rationale/log so disk state matches verified tool state.
Rejected Alternatives: Ignoring the stale ledger was rejected because project protocol treats disk files as long-term memory. Reverting C# or project settings was rejected because the task is Python/web only.
Scalability potential: Low tier remains a bounded local dashboard. High/Ultra can extend panels later; recovery did not change Unity runtime.
Hardware Impact: 0 us Unity frame impact. File recreation and status repair are external tooling operations only.

## Decision 013 - Read-Only Dump Collection Regression Guard
Problem: A dashboard GET path must not mutate `Docs/AgentLogs`, and missing or rotating telemetry files must not turn `/api/summary` into a fault.
Solution: Verify the server read path uses guarded file metadata and add a smoke-test assertion that points `AGENT_LOGS` at a missing directory, calls `collect_dumps()`, and confirms the directory is still absent.
Rejected Alternatives: Creating `Docs/AgentLogs` from the API was rejected because read endpoints should not repair filesystem layout. Letting missing files raise was rejected because telemetry writers can rotate or delete files while the browser polls.
Scalability potential: Low tier avoids accidental disk writes during polling. Middle/High/Ultra can run continuous dashboard refreshes without the reader creating evidence folders or crashing on absent artifacts.
Hardware Impact: 0 us Unity frame impact. Auxiliary Python behavior only; exact API timing remains PENDING MEASUREMENT.
