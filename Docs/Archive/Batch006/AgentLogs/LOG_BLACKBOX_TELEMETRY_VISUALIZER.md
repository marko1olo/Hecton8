# LOG - BLACKBOX_TELEMETRY_VISUALIZER

## 2026-05-14 - FastAPI Telemetry Dashboard

What was wrong:
- The active project had no browser dashboard for `Docs/AgentLogs/Dump_*.bin`, `QA_Endurance_Log.csv`, or headless ecology telemetry.
- Active `Docs/AgentLogs` currently contains no `Dump_*.bin` and no `QA_Endurance_Log.csv`, so the tool needed missing-file-safe startup.
- Dump formats are not uniform. Current source shows generic HECTON8 64-byte records, Data Vault defrag raw rings, thermal manual binary records, biomass magic-header dumps, headless QA blackbox dumps, and H8Memory text allocation tables.

What was done:
- Added `Tools/TelemetryDashboard/server.py` with FastAPI endpoints `/`, `/api/summary`, and `/api/health`.
- Added parsers for generic blackbox, memory defrag, thermal, biomass, headless QA, H8Memory text, CSV frame/ecology/thermal/H-Phi aliases, and static H-Phi report fallback.
- Added `Tools/TelemetryDashboard/index.html` with dark dashboard UI, Chart.js frame/jitter graph, H-Phi gauge, ecology graph, thermal/battery KPI, dump/CSV file table, 2-second polling, and memory map.
- Added `requirements.txt`, `start_dashboard.bat`, `start_dashboard.sh`, and `README.md`.
- Installed FastAPI/uvicorn for local verification. Pandas remains conditional for Python versions below 3.13 because this environment is Python 3.14 and pandas is not required by the implemented stdlib CSV parser.
- No C# files were edited.

Cinematic Cheats used:
- Dashboard uses aggregate telemetry and 2D canvas/grid visualization instead of opening Unity, Excel, or simulating runtime systems.
- Memory defrag summaries are rendered as explicitly estimated free/occupied bands when exact block geometry is absent. No fake precision is presented.
- Polling is used instead of WebSocket state because telemetry is file-backed, not a live event stream.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed; dashboard is an external Python/browser process.
- Excel/Unity opening avoided: not measured. Operational latency reduction is expected but PENDING MEASUREMENT.
- Parser refresh estimate: 2,000-8,000 us on local disk for current capped row/dump sizes, PENDING MEASUREMENT.
- Browser chart update: not profiled; runs every 2 seconds outside Unity.

Verification:
- `python -m py_compile Tools\TelemetryDashboard\server.py`: PASS.
- Synthetic parser smoke test for generic blackbox, memory defrag pack-1, thermal, biomass, headless QA, H8Memory text, and CSV: PASS.
- `http://127.0.0.1:8000/`: HTTP 200, 16155 bytes.
- `http://127.0.0.1:8000/api/summary`: `DASHBOARD OPERATIONAL`, `FRAMES=0`, `DUMPS=0`, `HPHI=0.00062`.
- `git status --short -- Tools/TelemetryDashboard Docs/Tasks/Status_BLACKBOX_TELEMETRY_VISUALIZER.md Docs/AgentLogs/Rationale_BLACKBOX_TELEMETRY_VISUALIZER.md Assets/_Project/Scripts`: only dashboard/docs files; no C# edits.
- `<POLISH_MANDATE>` extraction after all tasks: tag not found.

Runtime:
- Server is running at `http://127.0.0.1:8000` with Python PID 7420.

Regression model:
- CPU: external HTTP/file polling only; no Unity CPU path changed.
- GC: no Unity managed allocation path changed.
- Memory: no Unity memory path changed; Python process owns its own memory.
- Cadence: browser polls every 2 seconds; no game tick cadence changed.
- Correctness: parser smoke tests pass for observed source layouts; real dump content remains PENDING VERIFICATION until active dump files exist.

## 2026-05-14 - Self Review Patch

What was wrong:
- Self-review found the frame graph used only CSV-derived frame samples. Generic HECTON8 blackbox dumps carry `DeltaTime`, so a dump-only forensic session would show no frame graph.
- Requirements listed pandas conditionally for all Python `<3.14`; pinned pandas 2.2.3 is not safe for Python 3.13+ in this environment class.

What was done:
- Added dump-derived `frameSeries` from generic blackbox entries and wired the frontend to use top-level `summary.frameSeries`.
- Tightened pandas marker to `python_version < "3.13"` while keeping FastAPI/uvicorn pinned.
- Updated README and rationale with the fallback frame source and dependency guard.

Cinematic Cheats used:
- Reused existing blackbox aggregate `DeltaTime` instead of adding new Unity instrumentation.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard parser cost: one extra linear pass over already-loaded generic dump entries, capped at 600 returned points; not profiled, PENDING MEASUREMENT.

Verification:
- `py_compile.compile(..., cfile=.codex_tmp\telemetry_server_compile.pyc, doraise=True)`: PASS.
- Self-review parser smoke test with synthetic generic blackbox fallback frame series, defrag, thermal, biomass, headless, H8Memory text, and QA CSV: PASS.
- `pip install -r Tools\TelemetryDashboard\requirements.txt`: PASS; pandas ignored on Python 3.14 by marker.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `10140`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0` with no live QA/dump artifacts, `HPHI=0.00062`.
- `/`: HTTP 200, 16178 bytes.
- Removed generated `Tools/TelemetryDashboard/__pycache__` after verification.

## 2026-05-14 - Reproducible Smoke Test

What was wrong:
- Verification was accurate but not reproducible from a checked-in command; the parser smoke existed only as an inline shell snippet.

What was done:
- Added `Tools/TelemetryDashboard/smoke_test.py`.
- Updated README with the smoke-test command and expected output.

Cinematic Cheats used:
- Synthetic little-endian test payloads stand in for Unity-produced files, proving parser layout without needing Unity or live crash artifacts.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Future verification setup time: not measured. Removes manual reconstruction of binary test payloads.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `Tools/TelemetryDashboard/__pycache__`: absent after verification.
- `/api/summary` on running dashboard: `DASHBOARD OPERATIONAL`, `HPHI=0.00062`, `frameSeries=0` because no live QA/dump artifacts exist.
- Python process review: only dashboard uvicorn process remains visible by WMI, PID `10140`.

## 2026-05-14 - Payload Bound And Evidence Ordering Review

What was wrong:
- Dump parsers were bounded by file size but still exposed every decoded entry in the API JSON payload. A 10 MB file could produce a large browser update every 2 seconds.
- Memory-map selection used first filename order. A fully estimated defrag map could be displayed ahead of a more exact H8Memory text allocation table.

What was done:
- Added `MAX_DUMP_ENTRIES = 600` and capped returned parser `entries` arrays while preserving `latest` from the full decoded set.
- Sorted memory maps so source allocation tables win over fully estimated defrag summaries.
- Updated the frontend memory-map status to show `source table` versus `estimated map`.
- Updated the checked-in smoke test to cover cap behavior and exact-map priority.

Cinematic Cheats used:
- Kept the memory view as a labeled 2D block visualization. It does not invent exact defrag block geometry when the source dump only contains aggregate fragmentation telemetry.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard browser/API polling: unbounded JSON/render growth removed beyond the latest 600 entries; exact auxiliary-process saving is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- Unsafe-pattern scan: no `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, `yaml.load`, `debugger`, or `console.log`; only fixed-container `innerHTML = ""` clears remain.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `9556`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `HPHI=0.00062` with no active live dump/QA artifacts.
- `/`: HTTP 200, 16268 bytes.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary scan remains clean for this task scope: only dashboard/docs/status/log paths are untracked or modified.

## 2026-05-14 - Source-Contract Parser Extension

What was wrong:
- Active C# source audit found source-proven binary layouts that the dashboard still treated as unknown or did not collect: macro-swarm migration, fauna mutation, `.h8dump` crash exports, and `runtime_telemetry.bin`.

What was done:
- Added explicit parsers for `HECOSWM` macro-swarm telemetry, `HECOGUM` fauna-mutation telemetry, and `TELM` live crash telemetry.
- Extended file collection to include `.h8dump`, `BLACKBOX_CRASH.*`, and `runtime_telemetry.bin` under `Docs/AgentLogs`.
- Updated smoke-test payloads to prove those layouts decode.

Cinematic Cheats used:
- Kept macro/mutation data as compact source-labeled telemetry rather than inventing gameplay state or simulating ecology in the dashboard.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard: unknown-file manual inspection avoided; exact auxiliary parser cost is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- Magic constant audit: `BIOMASS=b'HECSMB8\0'`, `MACRO=b'HECOSWM\0'`, `MUT=b'HECOGUM\0'`, `HECTON8=b'HECTON8\0'`, `LIVE=b'TELM'`.
- Safety scan: no `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, `yaml.load`, `debugger`, `console.log`, or `innerHTML`; fixed DOM clearing uses `replaceChildren()`.
- Rebuilt `Tools/TelemetryDashboard` after the directory disappeared during restart recovery.
- H-Phi regression caught and fixed: direct parse of `Docs\Reports\HECTON_PHI_REPORT.md` returns `0.00062`; smoke test asserts this formula case.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `9804`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `files=0`, `HPHI=0.00062` with no active live dump/QA artifacts.
- `/`: HTTP 200, 14918 bytes.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits.

## 2026-05-14 - Read-Only Dump Collection Guard

What was wrong:
- The dashboard read path needed an explicit regression guard proving missing `Docs/AgentLogs` input stays read-only and returns empty telemetry instead of mutating disk or faulting.

What was done:
- Reviewed the current server read path: file metadata is guarded, binary parsing handles missing/unreadable files, and `collect_dumps()` does not create `AGENT_LOGS`.
- Added a checked smoke-test assertion that calls `collect_dumps()` against a missing `MissingAgentLogs` directory and verifies `files == []` and the directory was not created.

Cinematic Cheats used:
- Synthetic missing-directory input replaces a live Unity telemetry rotation race. It proves the dashboard contract without requiring a crash producer.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/API: avoided potential filesystem write and exception path during polling; exact auxiliary-process savings are PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\server.py Tools\TelemetryDashboard\smoke_test.py`: PASS; Git reports only LF-to-CRLF warning for `smoke_test.py`.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `7244`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `files=0`, `HPHI=0.00062` with no active live dump/QA artifacts.
- `/`: HTTP 200, 15327 bytes.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits.

## 2026-05-14 - Frontend Partial-Payload Guard

What was wrong:
- `index.html` assumed `/api/summary` always returned nested `csv.sources`, `csv.frameSeries`, `dumps.files`, and `dumps.memoryMaps`. A partial response could throw in the browser before showing diagnostic status.

What was done:
- Added `asArray`, `asObject`, and `normalizeSummary()` helpers.
- Routed `updateDashboard()` through normalized data before calculating KPIs, charts, memory maps, and the file table.
- Added an HTTP status guard before parsing `/api/summary` JSON.

Cinematic Cheats used:
- The browser now uses empty-series fallbacks instead of inventing telemetry when the payload is partial. Missing data remains visible as empty gauges/tables.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/browser: prevented exception cascade on partial payloads; exact render timing is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\index.html Tools\TelemetryDashboard\smoke_test.py`: PASS; Git reports only LF-to-CRLF warning for `index.html`.
- `node --version`: FAILED, Node is not installed in this environment; JavaScript parser check is PENDING TOOLING.
- Safety scan: no `eval`, `new Function`, `document.write`, `innerHTML`, `console.log`, or `debugger` in dashboard source.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `files=0`, `HPHI=0.00062` with no active live dump/QA artifacts.
- `/`: HTTP 200, 16472 bytes; served HTML contains `normalizeSummary()` and the HTTP status guard.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits by this task; unrelated untracked `Assets/_Project/Scripts/Editor/OutpostFailSafeHandoffValidator.cs` exists and was not touched.

## 2026-05-14 - Frontend Nested-Shape Guard

What was wrong:
- Top-level frontend payload normalization did not protect against malformed array elements. `memoryMaps[0].blocks` could still throw if a partial payload supplied a memory-map object without a block list.

What was done:
- Added `objectArray()` to normalize array elements to objects.
- Added `normalizeMemoryMap()` to normalize each memory-map entry and its `blocks` list.
- Updated `normalizeSummary()` to normalize frame/ecology points, CSV sources, dump files, and memory-map entries before widget rendering.
- Extended `smoke_test.py` to assert the frontend guard functions exist and that unsafe browser patterns remain absent from `index.html`.

Cinematic Cheats used:
- Malformed or missing telemetry renders as empty charts/tables/maps instead of fabricated values. The dashboard remains honest when source data is incomplete.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/browser: prevented memory-map and table exception paths from malformed JSON; exact render timing is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\index.html Tools\TelemetryDashboard\smoke_test.py`: PASS; Git reports only LF-to-CRLF warnings.
- Safety scan on executable dashboard sources: no `eval`, `new Function`, `document.write`, `innerHTML`, `console.log`, or `debugger`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `files=0`, `HPHI=0.00062` with no active live dump/QA artifacts.
- `/`: HTTP 200, 16769 bytes; served HTML contains `objectArray()`, `normalizeMemoryMap()`, and the HTTP status guard.
- Dashboard process remains PID `7244` on `http://127.0.0.1:8000`.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits by this task. Unrelated modified/untracked C# files exist and were not touched.

## 2026-05-14 - API Degraded-Response Guard

What was wrong:
- `/api/summary` directly returned `build_summary()`. One unexpected parser bug or filesystem edge case could make the whole dashboard API return a hard framework error instead of a structured diagnostic payload.

What was done:
- Added `empty_csv_data()`, `empty_dump_data()`, and `build_degraded_summary()`.
- Wrapped `/api/summary` summary generation in route-level exception handling.
- Extended `smoke_test.py` to monkeypatch `build_summary()` into a forced route failure and verify the response remains HTTP 200 with `DASHBOARD DEGRADED` and explicit error metadata.
- Corrected the stale status wording for the C# boundary: unrelated C# worktree changes exist, but this dashboard task did not touch them.

Cinematic Cheats used:
- Failure mode renders as empty telemetry plus an explicit error contract. No stale or invented runtime values are shown.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/API: avoided hard 500 response path during unexpected summary exceptions; exact auxiliary-process cost is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\server.py Tools\TelemetryDashboard\smoke_test.py Tools\TelemetryDashboard\index.html`: PASS; Git reports only LF-to-CRLF warnings.
- Static source scan confirms degraded response functions and route `except Exception` guard are present.
- Safety scan on executable dashboard sources: no `eval`, `new Function`, `document.write`, `innerHTML`, `console.log`, or `debugger`.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `4084`.
- `/api/summary`: `DASHBOARD OPERATIONAL`, `frameSeries=0`, `memoryMaps=0`, `files=0`, `HPHI=0.00062`, no `errors` in normal state.
- `/`: HTTP 200, 16769 bytes.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits by this task. Unrelated modified/untracked C# files exist and were not touched.

## 2026-05-15 - Response Cache Guard

What was wrong:
- The browser used `cache: no-store`, but the server did not explicitly send no-store/no-cache headers. A stale HTML or JSON response would be a false telemetry view.

What was done:
- Added shared `NO_STORE_HEADERS`.
- Added `dashboard_json()` for all dashboard JSON responses.
- Applied no-store/no-cache/nosniff headers to `/`, `/api/summary`, and `/api/health`.
- Extended `smoke_test.py` to assert summary, index, and health route headers.

Cinematic Cheats used:
- No telemetry value is invented. Header hardening prevents stale cached views from masquerading as live file-backed evidence.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/API: stale-cache failure mode removed; exact auxiliary-process cost is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `py_compile` for `server.py` and `smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\server.py Tools\TelemetryDashboard\smoke_test.py Tools\TelemetryDashboard\index.html`: PASS; Git reports only LF-to-CRLF warnings.
- Restarted dashboard on `http://127.0.0.1:8000`, Python PID `5920`.
- `curl -D - -o NUL /api/summary`: HTTP 200 with `cache-control: no-store, max-age=0`, `pragma: no-cache`, `x-content-type-options: nosniff`.
- `curl -D - -o NUL /`: HTTP 200 with `cache-control: no-store, max-age=0`, `pragma: no-cache`, `x-content-type-options: nosniff`.
- `curl -D - -o NUL /api/health`: HTTP 200 with `cache-control: no-store, max-age=0`, `pragma: no-cache`, `x-content-type-options: nosniff`.
- Removed `.codex_tmp`; `Tools/TelemetryDashboard/__pycache__` absent after verification.
- C# boundary: no `Assets/_Project/Scripts` edits by this task. Unrelated modified/untracked C# files exist and were not touched.

## 2026-05-15 - Workspace-Local Smoke Harness

What was wrong:
- `Tools/TelemetryDashboard/smoke_test.py` used `tempfile.TemporaryDirectory()`.
- On this host, sandboxed verification cannot write/delete inside OS temp, so the smoke test failed before exercising dashboard parser contracts.

What was done:
- Replaced OS-temp smoke fixtures with `Temp/CodexValidation/BLACKBOX_TELEMETRY_VISUALIZER_SMOKE`.
- Made the synthetic `.h8dump` directory creation idempotent.
- Relaxed frame-series count to tolerate deterministic reruns while still proving the expected jitter and live telemetry points exist.

Cinematic Cheats used:
- Synthetic binary/CSV fixtures still replace live Unity crash production. No runtime telemetry values are fabricated by the dashboard.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard verification now runs without OS-temp permission dependency; auxiliary timing remains PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `python -B -m py_compile Tools\TelemetryDashboard\server.py Tools\TelemetryDashboard\smoke_test.py`: PASS.
- `git diff --check -- Tools\TelemetryDashboard\smoke_test.py`: PASS; Git reports only LF-to-CRLF warning.
- C# boundary: no `Assets/_Project/Scripts` edits by this task.

## 2026-05-15 - Live Dashboard Endpoint Recheck

What was wrong:
- After the smoke harness repair, the browser-facing server needed a fresh live endpoint check, not only parser tests.

What was done:
- Started uvicorn for `Tools/TelemetryDashboard/server.py` on `127.0.0.1:8000`.
- Checked `/`, `/api/summary`, and `/api/health`.

Cinematic Cheats used:
- Missing Unity telemetry still renders as honest empty file-backed data plus static H-Phi fallback; no runtime values were invented.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard auxiliary request timing remains PENDING MEASUREMENT.

Verification:
- Dashboard PID: `11956`.
- URL: `http://127.0.0.1:8000`.
- `/api/summary`: HTTP 200, `DASHBOARD OPERATIONAL`, files 0, frame series 0, H-Phi `0.00062`.
- `/`: HTTP 200, 16769 bytes, `cache-control: no-store, max-age=0`, `x-content-type-options: nosniff`.
- `/api/health`: HTTP 200, `ok`, `cache-control: no-store, max-age=0`, `x-content-type-options: nosniff`.
- C# boundary: no `Assets/_Project/Scripts` edits by this task.

## 2026-05-15 - Per-File Parser Fault Isolation

What was wrong:
- `collect_dumps()` depended on every candidate file parser returning cleanly.
- A single unexpected parser exception could force `/api/summary` into whole-dashboard degraded mode and hide valid telemetry from other files.

What was done:
- Added `parse_failed_dump_file()` in `Tools/TelemetryDashboard/server.py`.
- Replaced the dump parser list comprehension with an explicit guarded loop.
- Extended `Tools/TelemetryDashboard/smoke_test.py` to force one parser exception and verify the failed file is labeled while `runtime_telemetry.bin` still appears.

Cinematic Cheats used:
- No telemetry values are invented. The failed artifact is labeled as failed evidence while valid artifacts remain file-backed.

Exact Microseconds saved:
- Unity gameplay frame: 0 us changed.
- Dashboard/API: whole-payload failure path avoided for single corrupt dumps; exact auxiliary-process timing is PENDING MEASUREMENT.

Verification:
- `python -B Tools\TelemetryDashboard\smoke_test.py`: PASS, output `telemetry dashboard smoke ok`.
- `python -B -m py_compile Tools\TelemetryDashboard\server.py Tools\TelemetryDashboard\smoke_test.py`: PASS.
- Evidence class: CLI_COMPILE + parser smoke. Unity runtime telemetry content remains PENDING VERIFICATION.
