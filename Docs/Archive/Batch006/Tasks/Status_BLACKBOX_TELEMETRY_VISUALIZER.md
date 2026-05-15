# Status - BLACKBOX_TELEMETRY_VISUALIZER

Domain: Auxiliary Node (Web/Python)
Task Count: 15
Status: DASHBOARD OPERATIONAL / UNITY RUNTIME TELEMETRY CONTENT PENDING VERIFICATION

Mandates loaded:
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `QA_Evidence_Text_Filter_Audit.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

Source prompt: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="BLACKBOX_TELEMETRY_VISUALIZER">`

## Loop 1 - Backend And Data Contracts
- [x] 1. FASTAPI SERVER | Justification: `Tools/TelemetryDashboard/server.py` exposes `/`, `/api/summary`, and `/api/health`; DOD is standalone auxiliary process, not Unity runtime. | Alternatives Rejected: Unity EditorWindow and C# telemetry changes rejected by Task 15. | Estimate: 0 us Unity hot path; 2,000-8,000 us external request parse on local disk, PENDING MEASUREMENT.
- [x] 2. BINARY PARSERS | Justification: parser covers generic 64-byte HECTON8 blackbox and `.h8dump`, live `TELM` telemetry, memory defrag raw rings, thermal manual writer records, biomass/macro-swarm/fauna-mutation dumps, headless QA dumps, H8Memory text tables, and safe unknown binary fallback; API entry arrays are capped to latest 600 records while `latest` is preserved. | Alternatives Rejected: single guessed struct parser and unbounded JSON dump payloads rejected. | Estimate: 0 us Unity hot path; bounded by 10 MB dump cap, PENDING MEASUREMENT.
- [x] 3. CSV INGESTION | Justification: backend reads `QA_Endurance_Log.csv` and `HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv`, caps latest rows at 600, derives frame ms from FPS/delta fields, and computes jitter if missing. | Alternatives Rejected: pandas-only ingestion rejected for dependency fragility; stdlib CSV remains disconnected from Unity. | Estimate: 0 us Unity hot path; <5,000 us external parse for capped rows, PENDING MEASUREMENT.

## Loop 2 - Primary Visualization
- [x] 4. HTML/JS UI | Justification: `index.html` is a dark dashboard surface with KPIs, charts, memory map, and file table. | Alternatives Rejected: marketing/landing page rejected; the first screen is operational data. | Estimate: 0 us Unity hot path; browser-only rendering.
- [x] 5. FRAME TIME GRAPH | Justification: Chart.js line graph plots frame ms and jitter, and red point coloring marks frames above 16.6 ms. | Alternatives Rejected: Excel/manual charting rejected by prompt; Unity Profiler dependency rejected for standalone dashboard. | Estimate: 0 us Unity hot path; browser chart update every 2 s.
- [x] 6. H-PHI GAUGE | Justification: frontend gauge reads latest CSV H-Phi if present or static `Docs/Reports/HECTON_PHI_REPORT.md` fallback. | Alternatives Rejected: fabricating runtime H-Phi rejected; report is labeled STATIC_DOC unless CSV overrides. | Estimate: 0 us Unity hot path; report regex parse is cold file I/O.

## Loop 3 - Memory And Live Data
- [x] 7. MEMORY MAP | Justification: frontend renders free/occupied blocks from H8Memory text tables or defrag summaries, prefers source allocation tables over fully estimated defrag maps, and labels evidence class in the UI. | Alternatives Rejected: pretending defrag summary contains exact block layout rejected; map is best-effort and source-labeled. | Estimate: 0 us Unity hot path; browser-only render every 2 s.
- [x] 8. WEBSOCKETS/POLLING | Justification: UI polls `/api/summary` every 2 seconds; simpler than WebSockets and enough for file-backed telemetry. | Alternatives Rejected: WebSockets rejected because no push producer exists and file watchers add platform complexity. | Estimate: 0 us Unity hot path; one local HTTP request per 2 s.
- [x] 9. ECOLOGY TRACKER | Justification: dashboard plots prey/predator biomass from headless CSV or ecology/headless binary dumps when present. | Alternatives Rejected: coupling to `EcosystemDirector` runtime rejected by standalone/no-C# requirement. | Estimate: 0 us Unity hot path; external parse only.

## Loop 4 - Thermal And Deployment
- [x] 10. THERMAL GAUGE | Justification: KPI reads `HardwareThermalSeverity`/Battery from CSV aliases or `Dump_THERMAL_THROTTLING_DIRECTOR.bin` parser. | Alternatives Rejected: live hardware polling in dashboard rejected; Unity-owned service already captures this if available. | Estimate: 0 us Unity hot path; external read only.
- [x] 11. REQUIREMENTS | Justification: `requirements.txt` pins FastAPI, uvicorn, and pandas as requested. | Alternatives Rejected: unpinned dependency drift rejected. | Estimate: install-time only.
- [x] 12. RUN SCRIPT | Justification: `start_dashboard.bat` and `start_dashboard.sh` launch uvicorn on `127.0.0.1:8000`. | Alternatives Rejected: requiring manual uvicorn command rejected for Lead Architect workflow. | Estimate: startup only.

## Loop 5 - Standalone, Docs, Boundary
- [x] 13. STANDALONE | Justification: FastAPI server runs without Unity; `/` and `/api/summary` return HTTP 200 with missing-log-safe empty data. | Alternatives Rejected: Unity MCP/Editor dependency rejected. | Estimate: 0 us Unity hot path; auxiliary server startup only.
- [x] 14. DOCS | Justification: `Tools/TelemetryDashboard/README.md` documents launch, localhost URL, data sources, and parser contracts. | Alternatives Rejected: chat-only instructions rejected by reporting protocol. | Estimate: docs-only.
- [x] 15. NO C# | Justification: dashboard-task edits remain confined to `Tools/TelemetryDashboard` and BLACKBOX ledger files; unrelated C# changes exist under `Assets/_Project/Scripts` and were not touched by this task. | Alternatives Rejected: normalizing dump emitters in C# rejected by explicit prompt boundary. | Estimate: 0 us runtime.

## Verification
- [x] Python syntax compile.
- [x] Parser smoke test with synthetic little-endian samples.
- [x] C# edit boundary scan.
- [x] HTTP `/` and `/api/summary` smoke test on `127.0.0.1:8000`.
- [x] Polish mandate extraction attempted after task completion; `<POLISH_MANDATE>` not found in `CURRENT_BATCH.md`.
- [x] Self-review patch verification rerun after frame-series fallback.
- [x] Checked-in smoke-test script verification.
- [x] Self-review patch verification added for dump payload caps and memory-map evidence ordering.
- [x] Source-contract parser extension verification for macro-swarm/fauna-mutation/live telemetry.
- [x] H-Phi parser regression check: `Docs/Reports/HECTON_PHI_REPORT.md` returns `0.00062`, not report date or formula multiplier.
- [x] Recreated and reverified `Tools/TelemetryDashboard` after directory loss during restart recovery.
- [x] Read-only dump collection regression check: `collect_dumps()` on a missing `AgentLogs` path returns empty files and does not create the directory.
- [x] Frontend partial-payload guard: `index.html` normalizes missing `csv`, `dumps`, `frameSeries`, and `ecologySeries` before rendering.
- [x] Frontend nested-shape guard: array elements and memory-map block lists are normalized before chart/table/map rendering.
- [x] API degraded-response guard: `/api/summary` catches summary generation failures and returns explicit empty telemetry with `DASHBOARD DEGRADED` status.
- [x] Response cache guard: `/`, `/api/summary`, and `/api/health` return `no-store`, `no-cache`, and `nosniff` headers.
- [x] Workspace-local smoke harness: `Tools/TelemetryDashboard/smoke_test.py` writes synthetic telemetry under `Temp/CodexValidation/BLACKBOX_TELEMETRY_VISUALIZER_SMOKE` instead of OS temp, so sandboxed verification executes without external temp permissions.
- [x] Live endpoint verification: dashboard started on `http://127.0.0.1:8000` with Python PID `11956`; `/`, `/api/summary`, and `/api/health` returned HTTP 200 with no-store/nosniff headers.
