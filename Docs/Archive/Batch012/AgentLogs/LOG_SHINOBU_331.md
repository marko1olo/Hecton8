# LOG_SHINOBU_331

## 2026-05-22 Terminal UV Input Projection

What was wrong:
- Terminal input authority had legacy exposure to Canvas World Space, GraphicRaycaster/EventSystem, ScreenPointToRay, and collider-backed panel controls.
- Existing TerminalOS projection path did not publish a 64-byte shader-facing cursor DTO and still split projection/dispatch work across separate stages.
- Cursor rendering risked becoming a CPU object/UI rebuild path instead of a terminal material lie.

What was done:
- Integrated into existing owner `TerminalOsRuntime`; no new hot manager authority was introduced.
- Added `TerminalInputStateDTO` and `TerminalInputTelemetryEntry`, both fixed at 64 bytes with explicit ARM64-safe offsets.
- Added Burst jobs `GenerateMockGazeVectorsJob` and `EvaluateTerminalGazeJob`.
- `EvaluateTerminalGazeJob` subtracts world AUP `double3` values in double precision, casts localized deltas to float, solves gaze-ray/terminal-plane intersection, projects local right/up to 0..1 UV, bezel-culls invalid hits, writes shader state, mirrors existing `TerminalInteractionDTO`, and enqueues existing `TerminalCommandSignal` / `InteractionUiSignal` lanes.
- Added Vault buffers 71380 (`TerminalInputStateDTO`) and 71381 (`TerminalInputTelemetryEntry` ring, 300 frames).
- Added fault dump route `Docs/AgentLogs/Dump_SHINOBU_331.bin` for non-finite/budget/layout faults.
- Bound `_TerminalInputStates` to diegetic terminal renderers and added shader-side cursor/ring rendering in `Hecton_DiegeticTerminal.shader`.
- Switched terminal layout CSV path to `Assets/_SourceData/UI/TerminalOS/terminal_ui_layouts.csv`.
- Added editor proof/tuning tools: `DiegeticTerminalXRayWindow` and `OOP_Canvas_Scanner`.
- Added route/audit/report artifacts:
  - `Docs/ARCHITECTURE/SHINOBU_331_TERMINAL_PROJECTION_ROUTE_CARD.md`
  - `Docs/Reports/SHINOBU_331_SELF_AUDIT.xml`
  - `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` section `shinobu_331_terminal_projection`
  - `Docs/Tasks/Status_SHINOBU_331.md`
  - `Docs/AgentLogs/Rationale_SHINOBU_331.md`

Cinematic cheats used:
- "Sweet Lie" cursor: terminal hit position is rendered in the fragment shader from UV state; no Canvas cursor, no LineRenderer, no cursor GameObject transform updates.
- Flat plane interaction: terminal intent is a deterministic ray/plane UV solve, not PhysX UI colliders.
- Continuous `GlobalQualityWeight` drives radius/fidelity continuously: low 5m terminal evaluation radius, high/ultra up to 25m plus visual shader polish without changing command truth.

Exact microseconds saved:
- Cursor CPU geometry/transform update path: 0 us runtime cost in this implementation because cursor is shader-only.
- Runtime hot clear for terminal input state buffer 71380: 0 us; buffer is opened with `UninitializedMemory` and overwritten by owner state.
- Editor tools and static scanner: 0 us player runtime cost.
- Canvas/GraphicRaycaster/PhysX terminal-authority net savings: PENDING PROFILER. No fabricated value recorded. Runtime exact values are captured per frame in 71381 as `BurstMicroseconds`; budget fault threshold is 200 us.

Verification:
- `git diff --check` over SHINOBU_331 files returned only pre-existing/project line-ending warnings.
- Static source scan recorded 1709 runtime files scanned, 0 Habitat/Vehicles OOP UI hit files, 31 Canvas token hits, 65 GraphicRaycaster token hits, 101 BoxCollider token hits; remaining hits are documented debt outside the terminal takeover route.
- `dotnet build Hecton8.slnx --no-restore` was attempted, timed out after 124 seconds, and spawned workers were terminated. A second build was blocked by the project CPU/dotnet guard after CPU samples exceeded 50 percent. Compile pass is not claimed.

## 2026-05-22 Ultra Polish Hardening Pass

What was wrong:
- `_TerminalInputStates` upload used one GraphicsBuffer, which violated the project bandwidth discipline for GPU-bound data.
- Task 16 tuning was not Vault-backed enough: editor sliders affected owner fields but had no dedicated unmanaged tuning row.
- `OOP_Canvas_Scanner` was token-count based, not AST based.
- Route/self-audit/binary ledger proof surface lacked 71382, dependency graph, review disposition, and explicit runtime-proof absence.

What was done:
- Added `TerminalInputTuningDTO=64` and Vault buffer `71382`.
- `DiegeticTerminalXRayWindow` now routes tuning through `ApplyTerminalProjectionEditorTuning`, which writes the 71382 row via `UnsafeUtility.AsRef` and mirrors sanitized scalars to owner fields.
- `EvaluateTerminalGazeJob` now receives `CursorSnappingTolerance` and `RaycastThickness`; UV cull tolerance and button snap expansion are continuous scalar math, not binary hardware branches.
- `_TerminalInputStates` upload now uses two LockBufferForWrite GraphicsBuffers and binds only the most recent completed upload.
- Shader received `_TerminalInputStateCount` guard before StructuredBuffer reads.
- `OOP_Canvas_Scanner` now uses Roslyn `CSharpSyntaxTree` object-creation/invocation/assignment checks with token fallback only on parse failure.
- Route card, self-audit XML, rendering report, status, rationale, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` were patched to record the YELLOW state and exact runtime proof gaps.

Cinematic Cheats used:
- Cursor remains a shader fragment-space lie from `_TerminalInputStates`; no CPU cursor object, Canvas mesh, LineRenderer, GraphicRaycaster, or PhysX UI collider authority.
- Raycast thickness/snapping are scalar UV tolerances, not extra Physics queries.

Exact Microseconds saved:
- Shader cursor CPU geometry update remains 0 us.
- Editor scanner/tuner remains 0 us player runtime.
- Double-buffering prevents CPU/GPU write-read contention risk; exact frame-time saving is PENDING PROFILER.
- Canvas/GraphicRaycaster/PhysX terminal-authority net saving remains PENDING PROFILER. No fabricated value recorded.

Verification:
- Static source patch only. No dotnet build launched in this polish pass per user command and CPU/compiler guard policy.
- Required runtime proof remains pending: Unity import, Console, Play Mode, profiler/GCMonitor, Burst Inspector, Frame Debugger, shader visual inspection, player build, and device proof.

## 2026-05-22 Sub-Agent Audit Correction Pass

What was wrong:
- Public `TryGet*` projection accessors used mutable owner buffer resolution.
- Non-finite source vectors could be hidden by sanitized fallbacks before fault flags.
- `TerminalInputTuningDTO` radius/curve fields were present but not consumed by the projection job.
- Scanner report language implied stronger proof than static source analysis.

What was done:
- Public projection read accessors now use `TryReadVaultBuffer`.
- `EvaluateTerminalGazeJob` and legacy intersection job classify raw vector finiteness before fallback.
- Radius math now consumes `LowRadiusMeters`, `UltraRadiusMeters`, and `QualityCurvePower`; `math.pow` was replaced by deterministic linear-to-cubic polynomial shaping.
- `UpdateTerminalTextJob` unsafe pointer lane now has explicit owner/row/fence safety proof.
- Scanner output now states `STATIC_SCRIPT_SCAN_ONLY` and `hotPathAllocProof: PENDING_PROFILER_GCMONITOR`.

Cinematic Cheats used:
- No change: terminal cursor remains shader-side; CPU uses scalar UV tolerance instead of physics thickness tests.

Exact Microseconds saved:
- `math.pow` removal saves ALU in the projection job, exact value PENDING PROFILER.
- Runtime allocation proof remains PENDING PROFILER_GCMONITOR; no 0 B/frame runtime claim made.

Verification:
- Static grep and `git diff --check` only; no dotnet build launched.
- Guard sample after patches: CPU 100 percent, no dotnet/csc process. Rebuild remains blocked by policy.

## 2026-05-22 Dirty Upload And Shader Fence Pass

What was wrong:
- `_TerminalInputStates` was double-buffered but could still upload the full cursor-state buffer every finalized projection frame.
- Released projection buffers did not reset `_TerminalInputStateCount`, leaving stale material state possible.
- Non-instanced shader mode inferred cursor DTO row from `_TerminalSlice`; no route card proves slice equals terminal row.
- Telemetry hardcoded `HotPathAllocBytes=0` without GCMonitor proof.

What was done:
- Added Vault buffer `71383` as the per-row dirty hash lane.
- The post-job audit computes row hashes from terminal hash, UV, and input flags, marks changed rows through the high bit, and sets upload dirty only when the aggregate hash changes.
- GPU upload now copies only contiguous dirty runs using `LockBufferForWrite(start,count)` and clears dirty row bits after successful copies.
- GPU upload now writes 32-byte `TerminalInputGpuStateDTO` rows matching shader `TerminalInputStateGPU`; the 64-byte AUP CPU DTO stays in Vault and is not uploaded verbatim.
- Projection teardown sets `_TerminalInputStateCount` to `0` before releasing buffers.
- The shader skips cursor StructuredBuffer reads in non-instanced mode and uses squared-distance ring math instead of per-fragment `sqrt`.
- `HotPathAllocBytes` now records `uint.MaxValue` as unknown until profiler/GCMonitor instrumentation supplies measured bytes.

Cinematic Cheats used:
- Cursor remains a shader-side visual lie; CPU still emits only UV/flags and no cursor GameObject, Canvas mesh, LineRenderer, or PhysX UI hit volume.

Exact Microseconds saved:
- Redundant unchanged GPU uploads are skipped; exact PCIe/frame-time delta is PENDING FRAME DEBUGGER/PROFILER.
- Cursor-state GPU row bandwidth is 32 bytes instead of copying the 64-byte CPU AUP row; exact bandwidth delta is PENDING Frame Debugger.
- Cursor ring removed one fragment `sqrt`; exact GPU delta is PENDING RenderDoc/Frame Debugger.
- No runtime allocation metric is claimed without GCMonitor.

Verification:
- Static source patch only. No dotnet build launched.

## 2026-05-22 Static ABI Verification Pass

What was wrong:
- `SHINOBU_331_SELF_AUDIT.xml` contained literal generic type text that was readable by humans but invalid XML.
- CPU/compiler guard still blocked a proving build after the slim GPU ABI pass.

What was done:
- Escaped generic type text in the self-audit artifact and verified it with `System.Xml.XmlDocument`.
- Parsed `RENDERING_OPTIMIZATION_REPORT.json` with `ConvertFrom-Json`.
- Ran focused runtime forbidden scan over `TerminalOsRuntime_TerminalProjection.cs`, `TerminalOsTypes.cs`, and `Hecton_DiegeticTerminal.shader`; no forbidden hot-path hits were returned.
- Ran `git diff --check` on touched SHINOBU_331 code/docs; only LF-to-CRLF warnings were reported.

Cinematic Cheats used:
- No change: cursor presentation remains a shader fragment-space projection from 32-byte UV/flag rows.

Exact Microseconds saved:
- Build/runtime profiler proof still pending. Latest guard sample: CPU average 74.1 percent, dotnet process count 7, csc count 0, so no build was launched.

## 2026-05-22 Scanner Upsert And Guarded Build Pass

What was wrong:
- `OOP_Canvas_Scanner` could misread braces inside quoted JSON strings while replacing the SHINOBU_331 section in the shared rendering report.

What was done:
- `FindSectionEnd` and top-level object-end discovery now ignore escaped characters and quoted strings before counting JSON braces.
- Scanner-generated SHINOBU_331 report output now preserves buffer `71383` and the 32-byte `TerminalInputGpuStateDTO` upload description.
- Ran guarded `dotnet build Hecton8.Core.csproj --no-restore` only after CPU/compiler guard cleared.

Cinematic Cheats used:
- No runtime change. This pass protects forensic proof, not terminal presentation math.

Exact Microseconds saved:
- Runtime: 0 us, editor-only scanner hardening.
- Build result: failed after 78.9s with 72 errors and 1 warning in foreign domains. No SHINOBU_331/TerminalOS diagnostic was emitted. Compile proof remains blocked by external compile wall.

## 2026-05-22 Subagent Audit Patch Pass

What was wrong:
- Near-parallel terminal gaze rays could be converted into false intersections by denominator clamping.
- Black-box fault dumps latched as done before file IO succeeded.
- JSON section replacement skipped a comma after replaced sections, risking malformed shared reports.
- Shader render-state review needed an explicit route-card decision.

What was done:
- `EvaluateTerminalGazeJob` now culls finite `abs(denom) < 0.01` cases before division and divides valid hits by raw `denom`.
- `_terminalProjectionDumped` now flips only after `WriteTerminalInputBlackBoxDump` succeeds.
- `OOP_Canvas_Scanner.FindSectionEnd` no longer consumes the comma after an existing section.
- Route card documents that `Hecton_DiegeticTerminal.shader` remains opaque/depth-writing because it is a physical terminal surface, not transparent HUD glass.

Cinematic Cheats used:
- Shader cursor remains the visual fake; the CPU still resolves only a ray-plane UV and command lane.

Exact Microseconds saved:
- Near-parallel cull avoids invalid UV/button work for grazing rays; exact delta PENDING PROFILER.
- Opaque/depth-writing render state preserves early-Z behavior; exact GPU delta PENDING Frame Debugger.

Verification:
- Static patch reviewed after subagent findings.
- Narrow post-patch build was launched only after guard cleared.

## 2026-05-22 Post-Subagent Guarded Build Pass

What was wrong:
- The prior build evidence predated the near-parallel ray cull, dump-latch correction, and JSON comma preservation patch.

What was done:
- Guard sampled CPU average 13.8 percent with no dotnet/csc processes.
- Ran `dotnet build Hecton8.Core.csproj --no-restore`.
- Build failed after 67.79s with 72 errors and 1 warning in foreign domains: `VRSomaticProvider`, `SubmarineDynamicsRuntime`, `TetherManager`, `CombatDamageRuntime_StatusEffects`, and `SubmarineAutoLevelBallastController`.
- No SHINOBU_331, TerminalOS, `TerminalOsTypes`, or `TerminalOsRuntime_TerminalProjection` diagnostic was emitted.

Cinematic Cheats used:
- No new runtime presentation change in this pass. The shader-side cursor lie remains the selected route.

Exact Microseconds saved:
- Build pass does not provide runtime microsecond proof. Profiler, Burst Inspector, GCMonitor, and Frame Debugger evidence remain pending behind the foreign compile wall.

## 2026-05-22 Final Static Artifact Verification Pass

What was wrong:
- After documentation updates, proof artifacts needed a final parser and forbidden-route scan so the on-disk state can survive context compaction.

What was done:
- Parsed `Docs/Reports/SHINOBU_331_SELF_AUDIT.xml`: OK.
- Parsed `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`: OK.
- Re-extracted SHINOBU_331 prompt from `Docs/Tasks/CURRENT_BATCH.md`: 20 tasks.
- Focused runtime forbidden scan over SHINOBU_331 runtime/shader files returned 0 hits.
- `git diff --check` returned only LF-to-CRLF warnings in touched files; no whitespace errors.

Cinematic Cheats used:
- No new runtime change. CPU keeps only mathematical UV projection; the shader still renders cursor presentation.

Exact Microseconds saved:
- Runtime 0 us for this verification pass.
- Remaining runtime savings are PENDING profiler, GCMonitor, Burst Inspector, and Frame Debugger after the foreign compile wall is cleared.
