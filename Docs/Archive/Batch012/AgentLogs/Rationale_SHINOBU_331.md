# Rationale_SHINOBU_331

Agent: SHINOBU_331
Status: PENDING VERIFICATION

## Initial Analysis

Problem: Existing terminal/world UI routing may rely on Canvas World Space, GraphicRaycaster, EventSystem, ScreenPointToRay, or collider-backed UI hit tests. These are forbidden for the assigned terminal input path because they allocate, couple presentation to PhysX/UI rebuild paths, and create hot main-thread work.

Solution: Discover existing runtime ownership first, then integrate through an existing partial interaction runtime if present. The intended kernel is a Burst `IJobParallelFor` over explicit unmanaged DTOs: subtract terminal `double3` AUP from gaze origin `double3` in double precision, cast the localized delta to `float3`, solve ray-plane intersection, project to UV, write flags/commands into unmanaged buffers and typed signal/queue lanes.

Rejected Alternatives: A standalone `HectonTerminalInputManager` is rejected until archaeology proves no existing interaction runtime owner. Canvas/GraphicRaycaster and BoxCollider UI routes are rejected because they are managed, object-oriented UI hit paths. Runtime CSV/string parsing is rejected; layout ingestion must be cold/editor or cold boot only.

Scalability potential: Low uses flat plane projection, 5m evaluation radius, minimal cursor shader. Middle increases radius and tighter tolerance. High can add curve compensation and richer telemetry. Ultra can spend saved CPU/GPU budget on visual-only cursor trails, glass salt/noise, and shader detail without changing gameplay truth.

Hardware Impact: Expected gain on i3/MX350 is removal of UI raycaster/collider broadphase work and replacement with cache-linear Burst dot products. Exact microseconds are PENDING VERIFICATION until profiler/GCMonitor evidence exists.

## Mandate Selection

- UI_Diegetic_Physical_Interfaces: ray-to-panel math and GraphicRaycaster rejection.
- OPT_Zero_GC_Policy_AllocFree_Mandate: hot path allocation law.
- DATA_Runtime_Struct_Layout_ARM64: explicit 64-byte DTO proof.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: AUP subtraction before float cast.
- OPT_Native_Memory_Collections_JobSystem_Protocol: DataVault/native/job ownership.
- ARCH_Execution_Phases: PRE_SIMULATION solver, POST_SIMULATION telemetry, VISUAL_SYNC upload.
- ARCH_Signal_Lane_Segregation: typed unmanaged command/interaction signal lane discipline.
- TOOL_Designer_Facades_CSV_Binary_Bridge: cold CSV layout path and runtime parser boundary.

## Decision 01: Existing Owner Route

Problem: The repository already contains `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs` with DataVault handles, `TerminalCommandSignal`, `TerminalClickSignal`, `InteractionUiSignal`, GPU buffers, layout ingestion, and terminal ray-plane jobs. Creating a new manager would duplicate authority.

Solution: Use `TerminalOsRuntime` as owner and add SHINOBU_331 projection as a partial owner file with a compact `TerminalInputStateDTO` mirror.

Rejected Alternatives: `HectonTerminalInputManager` standalone was rejected because it would race the existing TerminalOS buffers and signal lanes. Editing unrelated HUD Canvas systems was rejected because the assigned domain is submarine/base terminal input projection.

Scalability potential: Low/Middle/High/Ultra all use one owner route. Low reduces evaluation radius and batch size; Ultra enriches shader cursor presentation only.

Hardware Impact: i3/MX350 avoids duplicate scheduler and duplicate NativeArray scans. Exact gain PENDING VERIFICATION.

## Decision 02: Signal Route

Problem: Terminal click payload needs Terminal Hash, UV, and Action/Command ID without managed UnityEvents.

Solution: Adopt existing `TerminalCommandSignal` and `InteractionUiSignal` lanes. `TerminalCommandSignal` is explicit 16 bytes: TerminalHash 0, CommandHash 4, LocalUv 8. `InteractionUiSignal` already carries terminal hover UI data through SignalBus.

Rejected Alternatives: New `TerminalButtonClickedSignal` rejected as signal fragmentation. `HectonEventBus` rejected because it is mod/API/cold isolation, not first-party hot input.

Scalability potential: Low coalesces/drops through existing SignalBus limits; Ultra may add presentation-only consumers in VISUAL_SYNC without changing command truth.

Hardware Impact: No new hot lane allocation; queue capacity already bounded. Exact enqueue cost PENDING PROFILER.

## Decision 03: Legacy Canvas/Collider Boundaries

Problem: Static scan found `GraphicRaycaster`/Canvas surfaces in HUD/debug systems and collider-backed terminal controls in `PhysicalPanelButton` and `PhysicalSnapSwitch`.

Solution: Do not raw-edit scene/prefab YAML. Replace the terminal authority path with DataVault/Burst projection and add a scanner/report for OOP terminal UI debt. Runtime terminal interaction will no longer depend on those collider/UI components for command truth.

Rejected Alternatives: Blind component deletion from YAML rejected due prefab corruption risk and possible non-terminal HUD ownership. Keeping BoxCollider terminal input as primary authority rejected because it uses PhysX broadphase for UI.

Scalability potential: Low uses flat mathematical hit testing; Middle/High/Ultra retain shader-driven visuals and can layer visual overkill without restoring physics/UI authority.

Hardware Impact: Expected MX350 gain is removal of terminal-specific UI raycaster/collider authority. Exact microseconds PENDING VERIFICATION.

## Decision 04: Fused Projection Kernel

Problem: The existing TerminalOS path split gaze mock, cull, ray-plane intersection, and button dispatch across multiple jobs and only wrote `TerminalInteractionDTO`. The assignment requires `TerminalInputStateDTO` 64-byte shader state and one clean UV projection route.

Solution: Add `GenerateMockGazeVectorsJob` and fused `EvaluateTerminalGazeJob`. The fused job writes `TerminalInputStateDTO` rows for shader cursors and `TerminalInteractionDTO` rows for existing readbacks, then dispatches existing `TerminalCommandSignal` and `InteractionUiSignal` queues. AUP terminal and ray origins are converted to `double3`, subtracted in double, then cast to localized `float3`.

Rejected Alternatives: Keeping the old three-job pipeline as authority was rejected because it did not publish the shader-facing 64-byte input DTO. A new `TerminalButtonClickedSignal` was rejected because the existing terminal command lane carries terminal hash, command hash, and UV.

Scalability potential: Low uses 5m culling and flat plane math. Middle/High expand radius continuously. Ultra spends saved CPU budget on shader cursor ring glow only; command truth and DTO layout stay fixed.

Hardware Impact: i3/MX350 avoids Canvas/GraphicRaycaster/PhysX UI authority and uses linear Burst dot products. Runtime microseconds are recorded in buffer 71381; exact saving is PENDING PROFILER.

## Decision 05: Shader Cursor Lie

Problem: A CPU cursor object, Canvas element, or UI mesh rebuild would reintroduce managed UI work into the terminal path.

Solution: Keep `TerminalInputStateDTO.ProjectedUV` and `InputFlags` in Vault as the CPU/AUP projection row, then compact those fields into 32-byte `TerminalInputGpuStateDTO` rows for `_TerminalInputStates`. `Hecton_DiegeticTerminal.shader` reads the slim buffer by terminal instance index and draws a fragment-space glowing cursor/ring.

Rejected Alternatives: World-space Canvas cursor, LineRenderer cursor, and movable GameObject cursor were rejected because they add transform/UI update work and do not scale cleanly across terminal counts.

Scalability potential: Low draws a compact cursor. Middle/High/Ultra increase ring visibility through existing quality interpolation without changing hit math.

Hardware Impact: CPU geometry update cost is 0 us for cursor rendering; GPU ALU cost is per-fragment and presentation-only.

## Decision 06: Projection Black Box And Rollback Fence

Problem: Projection UV is transient presentation data and must be diagnosable without entering rollback/Merkle truth.

Solution: Allocate Vault buffers 71380 (`TerminalInputStateDTO`) and 71381 (`TerminalInputTelemetryEntry`). Buffer 71381 records 300 frames: evaluated terminals, successful projections, dispatched command estimates, Burst microseconds, radius, quality, fault flags. Faults dump raw rows to `Docs/AgentLogs/Dump_SHINOBU_331.bin`. Route card marks 71380 as presentation-only and rollback/Merkle excluded.

Rejected Alternatives: Hashing cursor UV in StateRingBuffer was rejected because it would create false network desyncs. Logging managed strings on fault was rejected; dump uses `ReadOnlySpan<byte>` raw writes.

Scalability potential: Low/Middle/High/Ultra all keep the same telemetry ABI; quality only changes radius and shader detail.

Hardware Impact: Telemetry is one 64-byte write per frame plus fault-only disk IO. Normal-frame i3/MX350 impact is below measurement until profiler proof.

## Decision 07: Editor And Static Proof Surface

Problem: Designers need visibility into projection timing/radius, and architecture review needs evidence that terminal authority no longer depends on Canvas/GraphicRaycaster/BoxCollider UI.

Solution: Add `DiegeticTerminalXRayWindow` for telemetry readout and tuning, `OOP_Canvas_Scanner` for static source evidence, `SHINOBU_331_TERMINAL_PROJECTION_ROUTE_CARD.md`, and `SHINOBU_331_SELF_AUDIT.xml`.

Rejected Alternatives: Runtime reflection scanners and live hierarchy searches were rejected because they are hot-path hostile and produce non-deterministic evidence.

Scalability potential: Editor tools do not affect player runtime. Scanner output documents remaining HUD/PDA/debug Canvas debt separately from terminal authority.

Hardware Impact: 0 us player runtime cost from editor tools. Static scan found 0 Habitat/Vehicles OOP UI hit files in 1709 runtime scripts; unrelated UI token counts remain documented debt.

## Decision 08: Polish Hardening Pass

Problem: The first route had three hard audit gaps: `_TerminalInputStates` upload was single-buffered, tuning sliders only mirrored serialized owner fields instead of mutating a Vault tuning row, and `OOP_Canvas_Scanner` was token-count based instead of AST based. Documentation also lacked Binary Payload Ledger and `YELLOW` review disposition.

Solution: Add `TerminalInputTuningDTO=64` in Vault buffer 71382, write it from the UI Toolkit facade via `UnsafeUtility.AsRef`, mirror sanitized values into owner scheduling, pass cursor tolerance/raycast thickness into `EvaluateTerminalGazeJob`, and expand UV/button hit bands continuously by quality. Replace single input GraphicsBuffer with two LockBufferForWrite buffers and bind only the most recent completed upload. Upgrade `OOP_Canvas_Scanner` to Roslyn `CSharpSyntaxTree` object-creation/invocation/assignment scanning with token fallback only on parse failures. Patch route card, self-audit, rendering report, and Binary Payload Integration Ledger.

Rejected Alternatives: Keeping serialized-only tuning was rejected because Task 16 requires Vault-backed live tuning. `MaterialPropertyBlock` was rejected for cursor data because the project forbids MPB on standard geometry and the shader already consumes a StructuredBuffer. Full YAML Canvas deletion was still rejected because unrelated HUD/PDA/debug Canvas surfaces are outside this terminal authority route and raw YAML deletion risks prefab corruption.

Scalability potential: Low uses 5m radius, larger tolerance band, simple shader cursor, and small job batches. Middle expands radius/batch size. High/Ultra tighten hit tolerance, reduce mock sway, extend radius to 25m, and spend saved CPU on shader ring/glow without changing command truth.

Hardware Impact: i3/MX350 gains bandwidth discipline from double-buffered bounded uploads and avoids UI/PhysX terminal authority. Exact terminal-authority savings remain PENDING PROFILER; shader cursor CPU geometry update remains 0 us by design.

## Decision 09: Sub-Agent Audit Corrections

Problem: Read accessors still used the mutable `TryOpenVaultBuffer` route, non-finite input vectors were sanitized before fault classification, tuning radius fields existed but were not consumed by the hot job, `UpdateTerminalTextJob` lacked explicit unsafe pointer proof, and the scanner report overstated profiler-backed allocation evidence.

Solution: Change public terminal projection `TryGet*` accessors to `TryReadVaultBuffer`. Classify raw `plane.Normal`, `plane.Up`, `plane.Right`, `gaze.Direction`, and localized AUP delta for non-finite values before sanitized fallback. Pass `LowRadiusMeters`, `UltraRadiusMeters`, and `QualityCurvePower` into `EvaluateTerminalGazeJob`; use deterministic linear-to-cubic polynomial shaping instead of `math.pow`. Add pointer safety proof comments to `UpdateTerminalTextJob`. Reword scanner report to static script scan only and mark hot-path allocation proof as `PENDING_PROFILER_GCMONITOR`.

Rejected Alternatives: Keeping `math.pow` in deterministic Burst was rejected because a polynomial blend is cheaper and more predictable across ARM64/x86. Treating sanitized fallbacks as clean input was rejected because it hides corrupt source facts from the black box. Reporting 0 B/frame from static scan was rejected because only profiler/GCMonitor can prove runtime allocation state.

Scalability potential: Low/Middle/High/Ultra continue to use one command truth route while tuning controls shape how quickly the radius grows with quality. Weak devices get cheap flat-plane/local radius behavior; high devices spend saved work on shader cursor/ring polish.

Hardware Impact: Expected low-end gain is reduced ALU from tuned radius and no hidden fault propagation. Exact microsecond delta remains PENDING PROFILER.

## Decision 10: Dirty Upload Hash Gate And Shader False-Read Fence

Problem: Sub-agent shader audit found three remaining hard gaps: `_TerminalInputStates` could still upload every finalized frame despite double-buffering, teardown left `_TerminalInputStateCount` positive after buffer release, and non-instanced shader mode inferred cursor row from texture slice without a guaranteed row/slice identity contract. The route card also overstated PRE_SIMULATION ownership while current integration is under `ILateFrameTickable`.

Solution: Add Vault buffer `71383` for per-row terminal input hashes. The existing post-job audit loop computes a 31-bit hash from terminal hash, flags, and UV; it marks the high bit as dirty only when a row changes. GPU upload now maps and copies only contiguous dirty runs through `GraphicsBuffer.LockBufferForWrite(start,count)`, clears dirty bits after successful run copies, and skips upload entirely when no row changed after the forced first upload. The upload ABI is `TerminalInputGpuStateDTO=32`, matching shader `TerminalInputStateGPU`; the 64-byte AUP CPU DTO is not copied verbatim to the GPU. Teardown sets `_TerminalInputStateCount` to `0` before releasing GraphicsBuffers. Non-instanced shader mode now bypasses cursor buffer reads until an explicit non-instanced terminal index route exists. `HotPathAllocBytes` uses `uint.MaxValue` as an unknown sentinel until GCMonitor proof exists.

Rejected Alternatives: Keeping full-buffer uploads was rejected because double-buffering alone does not satisfy the bandwidth-discipline ban on unchanged uploads. Per-row managed arrays were rejected because H-PHI requires Vault-owned persistent tracking. Using `_TerminalSlice` as cursor row in non-instanced mode was rejected because texture slice and terminal row are not documented as the same fact. Claiming PRE_SIMULATION proof was rejected because the owner currently runs from `LateFrameTick`.

Scalability potential: Low quality benefits from zero uploads when cursor state is stable and from squared-distance cursor ring math with quality-smoothed ring suppression. Middle and High keep exact UV command truth and upload only changed rows. Ultra spends shader ALU on richer cursor presentation only when instanced mode provides a valid row index.

Hardware Impact: MX350/i3 avoids redundant PCIe writes for unchanged terminal cursor rows, halves cursor-state GPU row bandwidth from the CPU DTO's 64 bytes to a 32-byte shader payload, and removes per-fragment `sqrt` from the cursor ring. Exact microsecond and bandwidth deltas remain PENDING PROFILER/Frame Debugger.

## Decision 11: Scanner Report Upsert Hardening

Problem: `OOP_Canvas_Scanner` used a manual JSON section-end search that counted braces inside quoted strings. The scanner is editor-only, but corrupting `RENDERING_OPTIMIZATION_REPORT.json` would destroy the proof artifact and could hide neighboring agents' evidence.

Solution: Patch `FindSectionEnd` and top-level object-end search to track quoted strings and escaped characters before counting `{` and `}`. The scanner still emits the same SHINOBU_331 section, including buffer `71383` and 32-byte GPU DTO upload proof, but no longer treats text inside JSON strings as structure.

Rejected Alternatives: Pulling in a new runtime/editor JSON package was rejected as an unnecessary dependency surface for one report upsert. Leaving the brace walker unchanged was rejected because shared forensic reports must be mechanically robust.

Scalability potential: Editor-only; player runtime remains 0 us. The benefit is proof integrity across parallel agents updating the shared report.

Hardware Impact: 0 us runtime impact. Latest guarded `dotnet build Hecton8.Core.csproj --no-restore` failed in foreign domains with 72 errors and no SHINOBU_331 diagnostics, so compile proof remains blocked by external compile wall.

## Decision 12: Near-Parallel Ray Cull And Fault Latch

Problem: The ray-plane solver guarded division by clamping tiny denominators. That avoids NaN, but an edge-on panel can produce a mathematically false hit by turning a near-parallel ray into a finite intersection. The fault dump path also latched `_terminalProjectionDumped` before file IO succeeded, suppressing later dumps after a transient IO failure.

Solution: In `EvaluateTerminalGazeJob`, non-finite denominators still set the non-finite fault path, but finite `abs(denom) < 0.01` is now a clean cull before division. Valid hits divide by raw `denom`, keeping the geometry honest. In `TryDumpTerminalInputBlackBox`, `_terminalProjectionDumped` is set only after `WriteTerminalInputBlackBoxDump` returns without exception.

Rejected Alternatives: Keeping a denominator clamp was rejected because it protects floating-point safety while corrupting ray/plane truth near grazing incidence. Marking near-parallel as non-finite was rejected because the input is finite; it is a valid cull, not corrupt data. Latching dump attempts before IO success was rejected because black-box evidence must survive transient file errors.

Scalability potential: Low/Middle/High/Ultra all use the same validity rule; quality still controls radius/tolerance/presentation only. No binary hardware branch or layout change.

Hardware Impact: Near-parallel cull removes downstream UV/button checks for invalid grazing cases. Exact microsecond delta remains PENDING PROFILER.

## Decision 13: Physical Terminal Shader Render State

Problem: Subagent audit proposed transparent render state for the terminal shader. That would make the screen behave like HUD glass and introduce transparent sorting/depth ambiguity, contradicting the physical diegetic terminal surface route.

Solution: Keep `Hecton_DiegeticTerminal.shader` as `Geometry`/`Opaque`, `ZWrite On`, `Cull Back`. Document the exception in the route card: the terminal screen is a front-facing physical panel that must write world depth and occlude correctly. Backside visibility is intentionally rejected unless art adds a separate backside material route.

Rejected Alternatives: Switching to `Queue=Transparent`, `ZWrite Off`, `Cull Off` was rejected because it would bypass physical world-depth behavior and turn the terminal into a transparent HUD pane.

Scalability potential: Opaque depth keeps early-Z and HZB compatibility stable across devices. Shader cursor detail remains quality-scaled.

Hardware Impact: Preserves early-Z/depth rejection on low-end GPUs. Exact GPU delta remains PENDING Frame Debugger.

## Decision 14: Post-Subagent Guarded Build Boundary

Problem: After the subagent patch pass, static proof was not enough; the code needed a narrow compile attempt without violating the active CPU/compiler guard. A build pass can only be used as evidence if it does not hide SHINOBU_331 diagnostics behind broad rebuild noise.

Solution: Sampled CPU and compiler state before build. Guard was clear: CPU average 13.8 percent, dotnet process count 0, csc process count 0. Ran `dotnet build Hecton8.Core.csproj --no-restore` only once after the patch pass. It failed after 67.79 seconds with 72 errors and 1 warning in foreign domains: `VRSomaticProvider`, `SubmarineDynamicsRuntime`, `TetherManager`, `CombatDamageRuntime_StatusEffects`, and `SubmarineAutoLevelBallastController`. No `Assets/_Project/Scripts/UI/TerminalOS`, `TerminalOsTypes`, `TerminalOsRuntime_TerminalProjection`, or SHINOBU_331 diagnostic appeared.

Rejected Alternatives: Broadly repairing foreign gameplay/physics domains was rejected because it violates the SHINOBU_331 boundary and risks overwriting parallel agents. Declaring a clean compile was rejected because the project compile wall is objectively still red.

Scalability potential: No runtime algorithm change. The build boundary preserves compile-wall discipline while keeping the terminal projection route isolated from sibling runtime failures.

Hardware Impact: Build proof remains blocked by external compile wall. SHINOBU_331 runtime remains subject to profiler, Unity import, Burst Inspector, and Frame Debugger proof once the foreign compile wall is cleared.

## Decision 15: Static Artifact Closure Boundary

Problem: After documenting the guarded build wall, the proof artifacts needed a final syntax and forbidden-route scan so the next agent can trust disk state after context compaction.

Solution: Parsed `Docs/Reports/SHINOBU_331_SELF_AUDIT.xml` as XML and `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` as JSON. Re-extracted the SHINOBU_331 prompt from `CURRENT_BATCH.md`; count remains 20 tasks. Focused runtime forbidden scan over `TerminalOsRuntime_TerminalProjection.cs`, `TerminalOsTypes.cs`, and `Hecton_DiegeticTerminal.shader` returned 0 hits for `Physics.Raycast`, `GraphicRaycaster`, `EventSystem.RaycastAll`, UnityEvent, hot `GetComponent`/Find calls, `new NativeArray`, LINQ, or `foreach`. `git diff --check` returned only LF-to-CRLF warnings.

Rejected Alternatives: A broad grep over all changed files was rejected as misleading because this shared worktree contains many unrelated parallel-agent edits. Another build was rejected because the last guarded build already isolated the current foreign compile wall.

Scalability potential: No runtime change. This preserves a compact proof trail for Low/Middle/High/Ultra terminal projection behavior and leaves profiler proof as the next objective gate.

Hardware Impact: Runtime impact 0 us. Verification confidence improved without touching foreign domains or triggering another compile pass.
