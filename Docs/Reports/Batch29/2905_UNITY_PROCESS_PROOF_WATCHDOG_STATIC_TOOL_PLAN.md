# 2905 Unity Process Proof Watchdog Static Tool Plan

Status: STATIC VERIFIED / PLAN ONLY / NO UNITY RUN / NO IMPLEMENTATION
Agent: 2905_UNITY_PROCESS_PROOF_WATCHDOG_STATIC_TOOL_PLAN
Date: 2026-06-04
Workspace: `C:\hades\Hecton8`
Evidence class: STATIC_DOC + STATIC_SOURCE + STATIC_FILESYSTEM + STATIC_PROCESS_SAMPLE

## Boundary

No Unity Editor launch, Play Mode run, build, process kill, asset edit, source edit, proof deletion, screenshot capture, or tool implementation was performed.

Owned write path used:

- `Docs/Reports/Batch29/2905_UNITY_PROCESS_PROOF_WATCHDOG_STATIC_TOOL_PLAN.md`

This report is a static tool plan. It is not runtime proof, profiler proof, visual proof, Unity slot proof, or GUI success proof.

## Authority Read

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `quality.md`
- `Docs/Orchestration/ORCHESTRATOR_DAY_20260604_BATCH28_APPEND.md`
- `Tools/ProofGate/validate_proof_packet.py`
- `Docs/Reports/Batch28/2805_LOG_PROCESS_PROOF_GATE_TOOL_AUDIT.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

`Docs/Actual Domains of Project.txt` was checked and produced no content. Narrow inferred domain: static process/proof/log watchdog for orchestration recovery.

## Mandates Applied

- `QA_Evidence_Text_Filter_Audit.txt`: static scans prove text/files/process samples only. Runtime, profiler, visual, and Unity health claims remain `PENDING VERIFICATION`.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`: no build or Unity launch while process/compile state may be busy; process watchdog must be read-only and cheap.
- `quality.md`: proof labels must not be upgraded. Static packet gates can allow review but cannot accept visual quality or runtime behavior.
- `HECTON8_ORCHESTRATOR.md`: resume preflight needs current front, latest proof artifacts, newest reports/logs, and active Unity/build/process state from disk, not compressed chat memory.

## Static Inputs Observed

Evidence:

- `Docs/Screenshots/MCP` contains recent raw PNGs including `h8_1908_surface_runtime_ui_on.png` and the six `h8_1474_*` route-named screenshots.
- `Docs/Screenshots/HectonProofPackets` was not found by the read-only directory sample.
- Latest report outputs include `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.json` and `.md`.
- Latest orchestration append states latest accepted visual/runtime proof is none, `1474` is rejected, and `h8_1908_surface_runtime_ui_on.png` is a raw screenshot, not a proof packet.
- Read-only process sample found no matching `Unity`, `dotnet`, `csc`, `VBCSCompiler`, `ilpp`, shader compiler, AssetImportWorker, or MSBuild process at sample time.

Residual risk: process state is point-in-time only. The future tool must timestamp every sample and refuse to turn a clean sample into a persistent no-busy claim.

## Proposed CLI Contract

Command:

```text
python Tools/ProofGate/unity_process_proof_watchdog.py ^
  --repo-root C:\hades\Hecton8 ^
  --screenshots-root Docs/Screenshots ^
  --proof-packets-root Docs/Screenshots/HectonProofPackets ^
  --mcp-root Docs/Screenshots/MCP ^
  --reports-root Docs/Reports ^
  --agent-logs-root Docs/AgentLogs ^
  --proofgate Tools/ProofGate/validate_proof_packet.py ^
  --max-log-files 12 ^
  --max-proof-packets 12 ^
  --json-out Docs/Reports/Batch29/UnityProcessProofWatchdog_latest.json ^
  --md-out Docs/Reports/Batch29/UnityProcessProofWatchdog_latest.md
```

Flags:

- `--repo-root`: required project root, default current directory only when it contains `AGENTS.md`.
- `--screenshots-root`: parent screenshot root, default `Docs/Screenshots`.
- `--proof-packets-root`: manifest-bound proof packet root.
- `--mcp-root`: raw MCP screenshot root.
- `--reports-root`: reports root for newest ProofGate outputs and synthesis files.
- `--agent-logs-root`: log root for Unity log copies and dirty-token scan.
- `--proofgate`: path to `validate_proof_packet.py`.
- `--packet-id`: optional exact packet id to validate. If absent, discover newest packet candidate.
- `--session-id`: optional session id. Required when `--packet-id` points to a manifest-bound packet with ambiguous sessions.
- `--strict`: pass strict mode through to ProofGate.
- `--max-log-files`: cap latest log scan count.
- `--max-proof-packets`: cap latest packet discovery count.
- `--json-out` / `--md-out`: optional output. Writing output is explicit and outside `Assets`.
- `--no-write`: print JSON only, useful for controller one-off status checks.

Exit codes:

- `0`: watchdog ran and emitted static status.
- `1`: static status contains blockers requiring controller attention.
- `2`: CLI/path/config error.
- `3`: internal watchdog error.

No exit code means Unity is safe to take. The output field `unitySlotRecommendation` must carry the only slot guidance.

## Output JSON Fields

Top-level schema:

```json
{
  "schema": "hecton8.unity_process_proof_watchdog.v1",
  "status": "STATIC_STATUS",
  "evidenceClass": "STATIC_PROCESS_SAMPLE",
  "createdLocal": "2026-06-04T22:30:00+04:00",
  "createdUtc": "2026-06-04T18:30:00Z",
  "repoRoot": "C:/hades/Hecton8",
  "unityBusy": false,
  "compileBusy": false,
  "assetImportBusy": false,
  "shaderCompileBusy": false,
  "buildBusy": false,
  "unitySlotRecommendation": "NO_BUSY_PROCESS_SEEN_STATIC_SAMPLE",
  "processes": [],
  "latestRawScreenshot": {},
  "latestProofPacketCandidate": {},
  "latestProofGateResult": {},
  "latestReports": [],
  "latestLogs": [],
  "dirtyTokenSummary": {},
  "blockers": [],
  "warnings": [],
  "mayClaimRuntimeProof": false,
  "mayClaimVisualAccepted": false
}
```

Required booleans:

- `unityBusy`
- `compileBusy`
- `assetImportBusy`
- `shaderCompileBusy`
- `buildBusy`
- `proofGateAvailable`
- `proofPacketCandidateFound`
- `rawScreenshotOnly`
- `dirtyLogTokensFound`

Required status strings:

- `status`: `STATIC_STATUS`, `STATIC_BLOCKED`, or `STATIC_TOOL_ERROR`.
- `unitySlotRecommendation`: one of `BUSY_DO_NOT_TAKE_SLOT`, `NO_BUSY_PROCESS_SEEN_STATIC_SAMPLE`, `UNKNOWN_PROCESS_STATE`.
- `latestProofGateResult.status`: value copied from ProofGate, usually `PASS_STATIC_GATE` or `REJECTED_STATIC_GATE`.

Forbidden output labels:

- `PLAYMODE VERIFIED`
- `PROFILER VERIFIED`
- `PLAYER-CAPTURE VERIFIED`
- `VISUAL ACCEPTED`
- `RELEASE READY`

## Windows Process Inspection Approach

Implementation target: Python with `subprocess.run()` calling PowerShell only for Windows process inventory, or direct `ctypes`/`psutil` if a dependency-free Windows API path is accepted later. Preferred first implementation remains dependency-free Python plus PowerShell command execution.

Process query:

```powershell
Get-Process |
  Where-Object {
    $_.ProcessName -match '^(Unity|Unity Hub|dotnet|csc|VBCSCompiler|ilpp|ShaderCompiler|UnityShaderCompiler|AssetImportWorker|MSBuild)$'
  } |
  Select-Object ProcessName,Id,CPU,StartTime,Path
```

Classification:

- Unity busy: `Unity` or Unity Editor executable present.
- Compile busy: `dotnet`, `csc`, `VBCSCompiler`, `MSBuild`, `ilpp`, `ILPP`, `Unity.ILPP` present.
- Shader compile busy: process name contains `ShaderCompiler` or `UnityShaderCompiler`.
- Asset import busy: process name contains `AssetImportWorker`.
- Build busy: `dotnet`, `MSBuild`, Unity batchmode process, or logs with active build/import tokens newer than the newest clean report.

Rules:

- Never kill a process.
- Never start Unity or build tools.
- Do not infer "safe forever"; timestamp the sample.
- Include process `id`, `processName`, `startTimeLocal`, `cpuSeconds`, and executable path when readable.
- If process path is unreadable, include process id/name and warning `PROCESS_PATH_UNREADABLE`.
- `dotnet` alone is not always a build. Report it as `compileBusy: true` and let the controller decide.

## Latest Proof Packet Discovery Algorithm

1. Resolve `Docs/Screenshots/HectonProofPackets`.
2. If it exists, enumerate immediate child directories and files named `manifest.json`.
3. Rank packet candidates by manifest last-write time, then newest screenshot last-write time.
4. For each candidate, parse `manifest.json` enough to extract `packet_id`, `session_id`, `created_utc`, `final_disposition`, `global_quality_label`, and screenshot count.
5. Run `Tools/ProofGate/validate_proof_packet.py` against the newest candidate when both `packet_id` and `session_id` are available.
6. If no manifest-bound packet exists, scan `Docs/Screenshots/MCP` for route-named raw PNG groups.
7. Group raw MCP PNGs by prefix pattern `h8_<digits>` and newest timestamp.
8. If a raw group has six required route names but no manifest, report `rawScreenshotOnly: true` and blocker `RAW_PNG_SET_NO_MANIFEST`.
9. Do not pass loose raw MCP folders as proof except as a negative-control input to ProofGate.

Packet candidate fields:

- `path`
- `packetId`
- `sessionId`
- `manifestPath`
- `manifestLastWriteLocal`
- `screenshotCount`
- `requiredViewCount`
- `newestScreenshotPath`
- `newestScreenshotLastWriteLocal`
- `candidateEvidenceClass`
- `candidateRisk`

Evidence label:

- Manifest-bound packet: `STATIC_FILESYSTEM`.
- Raw PNG group: `STATIC_FILESYSTEM_RAW_PNG_ONLY`.
- ProofGate output: `STATIC_FILESYSTEM_GATE`.

## Latest Raw Screenshot Detection

Scan roots:

- `Docs/Screenshots/MCP`
- `Docs/Screenshots`
- optionally `Docs/Orchestration/Captures`

Ignore roots for proof acceptance:

- `Assets/Screenshots`
- `MarketingAssets`
- any path under `Assets`

Detection:

- Enumerate `*.png` only.
- Exclude files under manifest-bound packet directories when packet discovery already owns them.
- Sort by last-write descending.
- Return newest raw PNG and newest grouped `h8_<id>` set.
- Flag `.meta` sibling contamination as warning outside packet root and hard blocker inside a packet root.

Required raw screenshot fields:

- `path`
- `fileName`
- `byteSize`
- `lastWriteLocal`
- `prefixGroup`
- `isUnderAssets`
- `hasMetaSibling`
- `matchesRequiredRouteName`
- `proofUse`: always `DIAGNOSTIC_ONLY` unless manifest-bound packet exists.

## Log Dirty-Token Summary

Log scan candidates:

- Latest `*.log` under `Docs/AgentLogs`.
- Latest `LOG_*.md`, `Rationale_*.md`, and relevant Batch synthesis reports only for text dirties and controller state, capped by CLI.
- Manifest `log_path` from the latest proof packet when present.

Dirty-token profile: copy `FORBIDDEN_LOG_TOKENS` from `Tools/ProofGate/validate_proof_packet.py` into the watchdog or import the constant if implementation remains in the same package.

Summary fields:

- `scannedFiles`
- `scanMode`: `FULL_FILE_STATIC_SCAN`, `TAIL_STATIC_SCAN`, or `MANIFEST_WINDOW_SCAN`
- `tokenCountsByCode`
- `firstHitByCode`
- `newestDirtyFile`
- `newestDirtyToken`
- `dirtyLogTokensFound`

Rules:

- Static text scan does not prove compile failure; it proves dirty token presence only.
- The tool must include matched token, file path, and line number or byte offset.
- If scanning only a tail window, report line/byte window.
- If no clean-window manifest offsets exist, report `LOG_WINDOW_UNKNOWN` rather than clean.

## Integration With Tools/ProofGate

Use the existing validator as authority for packet structure:

```text
python Tools/ProofGate/validate_proof_packet.py ^
  --packet-root <candidate> ^
  --packet-id <manifest packet_id> ^
  --session-id <manifest session_id> ^
  --json-out <optional watchdog temp/report path> ^
  --strict
```

Integration rules:

- Do not duplicate ProofGate acceptance logic except for discovery and summary.
- Capture stdout, stderr, exit code, and JSON payload if produced.
- If ProofGate rejects, copy reject codes into watchdog blockers.
- If ProofGate passes, output only `maySubmitForHumanVisualReview: true`; never output visual or runtime acceptance.
- If no manifest exists, do not fabricate a packet id/session id. Report raw PNG group and optional negative-control command only.
- If ProofGate is missing, report `PROOFGATE_TOOL_MISSING` and do not downgrade to raw heuristics.

## False-Positive And False-Negative Risks

False positives:

- `dotnet` may be unrelated to Unity/build work.
- Historical dirty tokens in full logs may not belong to the current capture window.
- File last-write timestamps can move due to copy/sync tooling rather than fresh proof.
- A raw PNG group may have six route names but still be old or diagnostic-only.
- Process path access may fail under permissions and look more suspicious than it is.

False negatives:

- Unity or compiler process can start immediately after the sample.
- Unity may be busy inside one process without visible child compiler/import workers.
- Dirty log tokens can be localized, truncated, or outside the scanned tail.
- Proof packet manifest can be internally consistent but still visually unacceptable.
- Static gate cannot detect false pixels, bad art, hidden UI, weak water, or wrong route if predicates lie.

Required mitigation:

- Timestamp every sample.
- Separate `busyNow` from `recentDirtyEvidence`.
- Keep ProofGate as static-only.
- Require human visual review after `PASS_STATIC_GATE`.
- Require Unity/player/profiler proof for runtime claims.

## Implementation Order

1. Create `Tools/ProofGate/unity_process_proof_watchdog.py` with CLI parser and schema constants.
2. Implement repo-root validation and safe path normalization.
3. Implement Windows process sampler with no kill/start side effects.
4. Implement proof packet discovery under `Docs/Screenshots/HectonProofPackets`.
5. Implement raw MCP screenshot grouping and diagnostic-only classification.
6. Implement latest reports/logs enumeration with caps.
7. Implement dirty-token scanner using the same token profile as ProofGate.
8. Integrate ProofGate subprocess call and result ingestion.
9. Emit deterministic JSON and concise Markdown.
10. Add unit tests under `Tools/ProofGate/test_unity_process_proof_watchdog.py`.
11. Add negative fixtures for raw PNG-only, missing ProofGate, dirty log, and fake busy process sample.
12. Run only Python unit tests for this tool. Do not run Unity or build for watchdog implementation.

## Test Plan

Required unit tests:

- no process sample + no packets: returns `STATIC_STATUS` with no busy process and `proofPacketCandidateFound: false`;
- fake Unity process sample: returns `BUSY_DO_NOT_TAKE_SLOT`;
- fake `dotnet` or `csc` sample: returns `compileBusy: true`;
- raw MCP six PNG group without manifest: blocker `RAW_PNG_SET_NO_MANIFEST`;
- manifest-bound packet with ProofGate pass fixture: copies `PASS_STATIC_GATE` and keeps `mayClaimVisualAccepted: false`;
- manifest-bound packet with ProofGate reject fixture: copies reject codes into blockers;
- missing ProofGate path: blocker `PROOFGATE_TOOL_MISSING`;
- dirty log token scan finds `Error`, `Warning`, `ILPP`, and `AssetDatabase.Refresh` classes;
- `.meta` sibling under packet screenshots is blocker;
- screenshot under `Assets` is blocker;
- output JSON never contains forbidden proof labels.

Manual static checks after implementation:

```text
python -m unittest discover -s Tools/ProofGate -p test_*.py
python Tools/ProofGate/unity_process_proof_watchdog.py --repo-root C:\hades\Hecton8 --no-write
```

These remain static checks. Unity/process runtime correctness remains `PENDING VERIFICATION` unless a controller later supplies live execution artifacts.

## First 20 Minutes Impact

This tool removes a controller recovery blocker for first-20-minutes route proof review. It does not improve graphics, gameplay, or optimization directly. It prevents false acceptance of stale/raw screenshots, dirty logs, and unsafe Unity-slot assumptions.

## Scalability Consequences

- Low: static filesystem/process scan runs without Unity, GPU work, build, import, or asset writes.
- Middle: capped packet/log enumeration prevents runaway controller scans.
- High: future perceptual or image metadata summaries can be added after packet gate, but still cannot replace visual review.
- Ultra: richer diagnostics can include multiple recent packet candidates and process history samples, but output labels remain static-only.

## Strongest Blockers

1. No existing combined watchdog summarizes Unity busy state, raw screenshots, proof packets, logs, and ProofGate result in one artifact.
2. Current visible screenshot state includes raw MCP PNGs; latest accepted visual/runtime proof remains none per orchestration append.
3. `Docs/Screenshots/HectonProofPackets` was not found in the static directory sample, so manifest-bound packet discovery currently has no obvious packet root.
4. `h8_1908_surface_runtime_ui_on.png` is a raw screenshot and cannot be promoted to proof without manifest, route predicates, hashes, and clean log window.
5. Process state is volatile. A clean static process sample cannot prove Unity remains free.
6. ProofGate can reject malformed packets, but it cannot accept visual quality, gameplay correctness, profiler state, or runtime route truth.

## Final Contract Sentence

The watchdog should produce one timestamped static JSON/Markdown status that reports current Unity/build/compiler/import process samples, newest raw screenshots, newest manifest-bound proof packet candidate, latest ProofGate disposition, and dirty-log tokens. It must never kill processes, launch Unity, edit assets, build, delete artifacts, or upgrade static evidence into runtime or visual acceptance.
