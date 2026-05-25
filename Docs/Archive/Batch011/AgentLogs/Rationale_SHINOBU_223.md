# Rationale_SHINOBU_223

Status: IMPLEMENTED / EXTERNAL BUILD WALL / POLISH ACTIVE

## Decision 0: Scope And Authority
Problem: The task demands a power-grid rewrite while 20+ agents may be changing adjacent systems.
Solution: Constrain edits to ECHELON 6 power/logistics data, Burst jobs, and shader brownout handoff. Use owner-local code and existing interfaces if present; avoid new global registry slots unless the current source already requires one.
Rejected Alternatives: Direct cross-domain concrete references were rejected because global authority law requires owner interfaces, typed signals, or vault snapshots. Per-object light scripts were rejected because they violate the Dear Lie brownout requirement.
Scalability potential: Low uses 1 Jacobi iteration and sparse shader scalar updates; Middle increases iterations; High/Ultra spend saved CPU on smoother brownout/flicker visual response.
Hardware Impact: On i3/MX350, removing per-object light updates can save 200-800 us in dense base interiors if legacy scripts exist; static source scan evidence pending.

## Decision 1: Mandate Set
Problem: The XML includes graph math, ARM64 DTO layout, AUP, GlobalQualityWeight, dispatcher phases, and shader brownout.
Solution: Read eight mandates before code: logistics graph flow, ARM64 layout, zero-GC, native jobs, AUP determinism, registry DI, execution phases, noir shader.
Rejected Alternatives: Reading generic docs only was rejected because the task has exact technical mandates.
Scalability potential: Solver cost scales continuously from weak to ultra hardware by iteration count and visual shader detail, not binary hardware switches.
Hardware Impact: Mandate-driven SoA/CSR and no per-frame managed allocation protects MX350/i3 frame stability; exact profiler proof absent until Unity run.

## Decision 2: DTO And Vault Contract
Problem: The power solver needed a new 32-byte node DTO while the project already had a `PowerEdgeDTO` type in submarine thermal runtime.
Solution: Added explicit `PowerNodeDTO`, `PowerGridEdgeDTO`, `PowerProfileDTO`, and `PowerTelemetryEntry`. Power Vault lanes now use owner-local numeric `BufferID` casts `70850..70864` plus pointer-free `VaultGenerationHandle<T>` descriptors. Kept the existing `PowerEdgeDTO` untouched and used `PowerGridEdgeDTO` to avoid duplicate type collision.
Rejected Alternatives: Renaming the existing submarine thermal `PowerEdgeDTO` was rejected because it belongs to another established runtime. Central `H8Memory` enum churn and legacy pointer-bearing `VaultBufferHandle<T>` state were rejected after the Binary Payload ledger pass. C# properties were rejected because NativeArray element mutation would create CS1612 copy hazards.
Scalability potential: Low reads compact 32-byte nodes with 1 solver pass; Middle raises iterations; High/Ultra can spend more passes and telemetry without changing the memory contract.
Hardware Impact: 32-byte nodes are two ARM64 128-bit loads or one cache-friendly lane pair; expected gain is 5-12 us per 4096-node pass on i3/MX350 versus property/copy layouts.

## Decision 3: Jacobi Kernel And Isolation
Problem: Looped base grids need stable voltage propagation without main-thread recursion or lock-based edge relaxation.
Solution: Implemented CSR build and Jacobi jobs over flat NativeArrays. Sources inject normalized generator potential, consumers subtract demand, damaged/sealed/shorted edges remain in CSR but receive zero conductance.
Rejected Alternatives: Recursive flood-fill power propagation was rejected for unbounded traversal cost. Removing damaged edges from CSR was rejected because it changes topology memory and complicates rollback snapshots.
Scalability potential: Low uses 1 iteration and slower visual leveling; Middle uses 3-4; High uses 6-7; Ultra uses 8 plus tighter residual tolerance in existing solver paths.
Hardware Impact: For i3/MX350, contiguous CSR reads should keep the solve in L1/L2 and avoid physics broadphase; expected saved cost is 100-500 us compared with hierarchy traversal on large bases.

## Decision 4: Brownout Dear Lie
Problem: The legacy SubmarineOS path could mutate cached lights/materials during brownout, which scales with object count and violates the shader-only requirement.
Solution: Brownout now publishes one global vector into the shader Vault. `GlobalShaderDispatcher` sends `_HectonPowerBrownoutParams`, and UberNoir applies supply dimming plus sine/triangle flicker per pixel using world position, instance seed, phase, and GlobalQualityWeight. The dead SubmarineOS light/material cache rebuild, apply, and restore routes were deleted so the old CPU path cannot silently return.
Rejected Alternatives: Per-light `Update`, material-instance dimming, CPU particles, cached shared-material mutation, hierarchy scans, and GameObject enable/disable were rejected because they convert one scalar power shortage into object-count CPU cost.
Scalability potential: Low uses coarse flicker; Middle adds stronger phase variation; High/Ultra blend fine flicker through shader quality without CPU object mutation.
Hardware Impact: On MX350/i3 interiors, expected CPU saving is 200-800 us when many lamps/materials are present; GPU cost is a few ALU ops inside an already-bound UberNoir shader.

## Decision 5: Conservation And Black Box
Problem: Closed electrical loops can drift if float deltas are accumulated directly, and crash forensics need fixed-size binary state instead of string logs.
Solution: Battery integration truncates milli-watt-second deltas and carries per-node remainders. The existing power black box now uses `PowerTelemetryEntry[300]` and dumps to `Docs/AgentLogs/Dump_SHINOBU_223.bin` on fault.
Rejected Alternatives: Raw float-only storage was rejected because loop drift can create/destroy energy. Managed logs were rejected because they allocate and are unusable in a crash path.
Scalability potential: Low records the same 300-frame ring with cheap fields; Middle/High/Ultra can add richer fault interpretation outside the hot job without changing the binary ring.
Hardware Impact: Remainder carry is one float lane per battery node; expected cost is under 10 us for recording and under 80 us for quantized active battery integration on low silicon.

## Decision 6: AUP And Editor Facades
Problem: Power graph nodes can exist in a 100km world; direct float casting of absolute coordinates will lose precision. Designers also need inspection without runtime debug objects.
Solution: Added `PowerGridAupMath.ToBaseLocalFloat3` and a test proving origin subtraction before float cast. Reused existing Grid Architect editor path with a Base Power Tuner alias and kept gizmo/debug work editor-only.
Rejected Alternatives: Storing world-space floats was rejected because precision fails far from origin. Runtime debug GameObjects were rejected because they pollute gameplay and add transform cost.
Scalability potential: Low/Middle use the same local-delta math and minimal editor draw; High/Ultra can render denser power previews from the same CSR/flow lanes.
Hardware Impact: AUP subtraction is cold or editor/render-preview math, not per-object runtime mutation; expected gameplay cost is 0 us unless a power graph preview is explicitly drawn.

## Decision 7: Compile Wall Boundary
Problem: Build verification had to run only after CPU dropped below 50%. Once it ran, the first code-visible error was a generated-project omission for the new power contract file, followed by unrelated project-wide missing dependencies.
Solution: Added `PowerGridJacobiContracts.cs` to `Hecton8.Core.csproj` so current dotnet build sees `PowerTelemetryEntry`. After that, SHINOBU_223-specific missing-type errors disappeared. Stopped at the external wall.
Rejected Alternatives: Editing `VaultGenerationHandle<>`, WFC outpost grid, docking autopilot, or construction socket contracts was rejected because those are outside ECHELON 6 power-grid ownership and belong to other agents' dependency domains.
Scalability potential: No runtime scalability effect. This is build plumbing and dependency attribution only.
Hardware Impact: Build-server processes were shut down after verification to avoid leaving CPU load for other agents; no gameplay hardware cost.

## Decision 8: Ledger-Compliant Vault And Brownout Purge
Problem: The first implementation still carried two architectural liabilities: central SHINOBU power `BufferID` enum entries in `H8Memory`, and dead SubmarineOS CPU brownout caches that could be reconnected by later edits.
Solution: Moved power lanes to owner-local numeric casts in `PowerGridBufferIds` and migrated `PowerGridVaultHandles` fields to `VaultGenerationHandle<T>`. `EnsureCoreBuffers` now requests `GetGenerationHandle` and validates each lane through `TryResolveHandle` without persisting raw pointers. `PowerGridManager` boot and DataVault hot-swap paths now request these Jacobi lanes cold. Removed SubmarineOS `GetComponentsInChildren` cache rebuild, point-light intensity mutation, shared-material emission mutation, restore cursors, and brownout binding arrays.
Rejected Alternatives: Keeping central enum additions was rejected because the current Binary Payload ledger uses owner-local casts for new lanes. Keeping dead CPU brownout helpers was rejected because unused object-mutation code is a future regression surface. Relaunching `dotnet build` after this polish was rejected because the prior SHINOBU_223 compile issue was already removed and the active build wall is foreign.
Scalability potential: Low devices keep one scalar shader route and one generation descriptor per Vault lane; middle/high/ultra increase solver iterations and shader flicker richness through continuous quality math without adding CPU object traversal.
Hardware Impact: i3/MX350 avoids stale pointer-handle metadata and loses the old O(lights + renderers + shared materials) brownout cache/restore path. Expected preserved saving remains 200-800 us in dense interiors; descriptor validation is cold boot only.

## Decision 9: Blackbox Ring Vault Eviction
Problem: `LogisticsNetworkGraph` still owned SHINOBU forensic state as a private `NativeArray<PowerTelemetryEntry>`, contradicting the Vault law for the critical 300-frame blackbox.
Solution: Replaced the private blackbox NativeArray with `VaultGenerationHandle<PowerTelemetryEntry>` and `VaultGenerationHandle<PowerGridCounter64>`. Blackbox writes and dumps resolve transient Vault views for ring `70861` and cursor `70862`; `PowerGridCounter64` is explicit 64 bytes so the cursor/control row owns a full cache line and carries the blackbox frame counter. Brownout signal frame now uses a manager-owned monotonic counter instead of `Time.frameCount`; shader flicker keeps `Time.unscaledTime` only as non-authoritative visual phase.
Rejected Alternatives: Migrating every legacy graph buffer in one patch was rejected as a high-risk compile-wall move across active agents. Keeping the blackbox as a local NativeArray was rejected because Task 16 explicitly owns forensic proof and can be safely isolated into Vault now.
Scalability potential: Low through Ultra share the same fixed 300-frame ring. No quality branch is introduced; richer interpretation can happen offline from the dump without adding runtime memory ownership.
Hardware Impact: Removes one persistent graph-owned telemetry allocation and avoids cursor false sharing. Runtime cost remains a Vault descriptor resolve on write/dump, estimated under 10 us and outside Burst solver loops.

## Decision 10: Equipment Drain Boundary DTO
Problem: `PowerGridJacobiContracts.cs` imported `Hecton8.Tools` only to read `EquipmentGridLoadRequest`, creating an avoidable sibling-domain namespace dependency in a power Burst contract.
Solution: Added power-local `PowerEquipmentLoadRequest`, explicit 16 bytes with the same hash/energy/flags/reserved lane shape, and changed `ApplyEquipmentPowerDrainJob` to consume it. Tool-domain producers must adapt their signal or Vault row at the boundary rather than forcing the power solver to reference Tools.
Rejected Alternatives: Keeping the Tools using was rejected because the active compile-wall mandate requires routing through owner-local DTOs, SignalBus/Vault boundaries, or contracts. Adding a central Core contract was rejected as broader churn for a local adapter shape.
Scalability potential: No quality-curve change. This preserves the same zero-GC batch drain path across all hardware tiers.
Hardware Impact: No hot-frame cost increase. It removes an assembly/namespace coupling risk and keeps the Burst job input blittable at 16 bytes.

## Decision 11: Jacobi Vault Release And Literal Editor Facade
Problem: The Jacobi Vault lane had boot-time generation descriptors but no explicit owner release path, and repeated `Awake`/`OnEnable`/bootstrap calls could reacquire the same descriptors. Task 17 also required literal Base Wire Conductance, Sump Pump Draw, and Jacobi Smoothing controls; the previous facade only exposed base resistance and omitted pump draw.
Solution: Added `PowerGridVaultRuntime.ValidateCoreBuffers` and `ReleaseCoreBuffers`; `PowerGridManager` now caches the owning `IDataVault`, validates same-vault descriptors before reuse, releases lanes on shutdown/DataVault hot-swap, and clears descriptor state after release. `LogisticsNetworkGraph` reads the blackbox ring/cursor through `TryGetGenerationHandle` so the graph does not create a second Vault-owner refcount for lanes owned by the manager. The editor facade now presents Base Wire Conductance as the reciprocal of the existing 32-byte logistics resistance field, and Sump Pump Draw writes the existing Vault-backed `DrainageTuningDTO.PumpPowerDraw` bridge owned by the pump grid.
Rejected Alternatives: Widening `LogisticsTuningDTO` was rejected because it would change a 32-byte ABI and duplicate a fact already owned by drainage. Reacquiring generation handles every cold lifecycle call or from each graph blackbox write was rejected because it can inflate Vault references without adding data authority. Runtime UI sliders were rejected because Task 17 is an editor facade.
Scalability potential: Low through Ultra runtime solver behavior is unchanged; designers can tune conductance, pump draw, and smoothing without C# recompiles, while the actual solver still scales continuously through `GlobalQualityWeight`.
Hardware Impact: Runtime hot-frame cost is unchanged. The release path prevents native Vault retention across shutdown/hot-swap, and the editor-only pump/conductance bridge has 0 us gameplay impact.

## Decision 12: Full ABI Audit And Failed-Acquire Cleanup
Problem: The previous ARM64 audit was strongest for `PowerNodeDTO` but only size-oriented for the adjacent power DTOs, and `EnsureCoreBuffers` could leave already-acquired generation descriptors retained if a later lane failed validation.
Solution: Expanded editor-only `PowerGridLayoutAudit.ValidateAllPowerLayouts` to exact offset checks for `PowerGridEdgeDTO`, `PowerProfileDTO`, `PowerTelemetryEntry`, `PowerGridCounter64`, `PowerEquipmentLoadRequest`, and `PumpPowerRequest` using `UnsafeUtility.GetFieldOffset`. The editor test mirrors the alias offsets used by telemetry. `EnsureCoreBuffers` now calls `ReleaseCoreBuffers(vault, ref handles)` before returning false on validation failure. Mock and editor-test DTO rows use `default` plus raw field assignment, matching hot-path field discipline and removing scanner noise.
Rejected Alternatives: Runtime/player reflection and size-only validation were rejected because alias or offset drift can compile while corrupting Vault/telemetry interpretation. `Marshal.OffsetOf` was rejected because the project already depends on Unity's `UnsafeUtility` for native layout proof. Keeping partial descriptor acquisitions on a failed boot was rejected because it can retain native Vault refs without an owner.
Scalability potential: Low through Ultra solver math is unchanged. The improvement is cold-path correctness: every hardware tier consumes the same compact 16/32/64-byte rows, while high-tier visual brownout still spends saved CPU in the shader route.
Hardware Impact: 0 us hot-frame cost. On i3/MX350/ARM64, exact native offset tests reduce the risk of unaligned DTO drift and leaked Vault refs across boot failure/hot-swap; failed-acquire release prevents native memory retention under startup fault conditions.

## Decision 13: CSR Truncation And NaN Input Hardening
Problem: `BuildCsrPowerGraphJob` counted only the edges that fit in the adjacency buffer during the prefix pass, but the second pass still iterated every traversable edge. With a truncated CSR buffer, a later edge could increment a cursor for a node that had no accepted degree and overwrite a valid slot from an earlier edge. Separately, mock topology assumed `NodeAup.Length >= Nodes.Length`, and drain/battery jobs accepted NaN tick/remainder/request values into arithmetic.
Solution: Added a `writtenAdjacency` cutoff in the second CSR pass so it stops at the same capacity accepted by the prefix pass. `GenerateMockPowerNetworkJob` now clamps node count to `min(Nodes.Length, NodeAup.Length)` and returns zero counts when the AUP lane is absent. `IntegrateBatteryChargeJob` sanitizes tick delta and carried milli-remainder before quantization. `ApplyEquipmentPowerDrainJob` sanitizes tick delta, request energy, and existing demand before accumulation. Added editor tests for truncated adjacency overwrite and missing AUP lane.
Rejected Alternatives: Letting write guards silently skip out-of-range writes was rejected because in-range writes can still corrupt the wrong node's adjacency interval after prefix truncation. Generating nodes without AUP rows was rejected because Task 04 requires graph positions as `double3` AUP truth. Treating NaN inputs as "should not happen" was rejected because the mandate requires NaN vaccination.
Scalability potential: Low through Ultra solver work and visual quality curves are unchanged. The cutoff preserves deterministic degraded behavior under undersized Vault lanes instead of corrupting graph topology.
Hardware Impact: Adds one integer capacity guard per accepted CSR edge and a few finite checks around demand/battery inputs. Expected hot cost is below 1 us for normal graph sizes; prevents far more expensive blackbox fault cascades and invalid memory writes.

## Decision 14: Edge Lane Parity Guard
Problem: Voltage and battery jobs clamped edge iteration against `EdgeDestinations.Length` but also indexed `EdgeConductance[edgeCursor]`. Normal Vault acquisition requests equal lengths, but a failed or stale lane could expose mismatched arrays and turn a solver pass into an out-of-bounds read.
Solution: Added `edgeReadLimit = math.min(EdgeDestinations.Length, EdgeConductance.Length)` in both `PowerVoltageSolverJob` and `IntegrateBatteryChargeJob`, then clamp CSR offsets against that limit before reading destination/conductance pairs. `EdgeCurrentFlow` remains write-guarded separately because it is an output lane.
Rejected Alternatives: Relying on Vault lane parity alone was rejected because the fault-hardening mandate requires local guards against stale or undersized buffers. Clamping only the write lane was rejected because the dangerous access is the conductance read.
Scalability potential: No quality-curve change. All tiers get the same bounded behavior; lower-quality partial solves fail closed instead of reading invalid memory.
Hardware Impact: Two integer `min` operations per scheduled job pass, not per edge. Expected hot cost is below measurement noise; it removes a crash-class memory safety fault.

## Decision 15: Brownout Dispatch Compile And NaN Fence
Problem: The brownout publisher was converted to an instance-owned monotonic frame counter, but the method still carried a `static` declaration. That would make the next honest compile attempt fail inside SHINOBU_223 instead of stopping only at the external wall. The shader dispatcher also trusted the raw `PowerBrownoutSlot` value after resolving the shared shader-global Vault.
Solution: Made `PublishBrownoutSignal` an instance method, finite-clamped `SupplyRatio`, `Severity01`, and `GlobalQualityWeight` before emitting the brownout signal/global, and added `SanitizePowerBrownoutVector` at the CBuffer dispatch boundary. A corrupt or NaN slot now collapses to supply `1`, severity `0`, phase `0`, and a finite quality fallback before `CommandBuffer.SetGlobalVector`. The already-touched CBuffer telemetry path now uses a dispatcher-owned monotonic frame counter instead of `Time.frameCount`.
Rejected Alternatives: Reverting to `Time.frameCount` was rejected because brownout and adjacent dispatch telemetry frame IDs are domain-owned telemetry state. Trusting only the publisher clamp was rejected because the shader-global Vault is shared and can be damaged by another writer. Per-object lamp fallback was rejected because it reopens the CPU brownout path.
Scalability potential: Low through Ultra keep the same continuous shader flicker curve; this change only makes the scalar route fail closed. Strong hardware still spends visual richness in UberNoir, not CPU light traversal.
Hardware Impact: One finite-clamp block per brownout publish and one finite-clamp block per global dispatch. Expected CPU cost is below measurement noise; it prevents a bad CBuffer row from poisoning all UberNoir emissive pixels.

## Decision 16: Generated Core Project Visibility For VaultGenerationHandle
Problem: Build attempt 4 failed with SHINOBU-visible `VaultGenerationHandle<>` errors because the generated `Hecton8.Core.csproj` compiled `PowerGridJacobiContracts.cs` but did not include `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, where `VaultGenerationHandle<T>` is declared. This was a generated-project compile-wall fault, not a solver DTO/layout fault.
Solution: Added existing Core memory source includes for `GlobalDataVault.cs` and `H8Memory.cs` to the generated `Hecton8.Core.csproj` so local CLI compilation sees the pointer-free Vault descriptor type before the SHINOBU power contract. No Core memory source content was modified in this decision. After CPU dropped to 33%, a guarded `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1` proved the `VaultGenerationHandle<>` symptom was removed; the build now stops at 62 external missing-symbol errors.
Rejected Alternatives: Duplicating `VaultGenerationHandle<T>` in the power namespace was rejected because it would create a second authority for the descriptor ABI. Editing unrelated WFC/Construction/Audio/World/Construction missing types was rejected because those remain outside SHINOBU_223 ownership. Running another build while CPU was 85% was rejected until the documented rebuild gate opened.
Scalability potential: No runtime quality-curve change. This protects verification plumbing only; runtime power scaling remains 1-8 Jacobi passes plus shader-side brownout quality.
Hardware Impact: 0 us gameplay impact. The change prevents a false SHINOBU compile attribution in local CLI builds without touching hot-path code.

## Decision 17: Vault Telemetry Fold Job
Problem: The 64-byte telemetry DTO and Vault ring existed, but Task 16 still needed an explicit recorder kernel that derives generation, load, potential range, brownout count, state hash, and solver timing from native power lanes without object logs or managed ownership.
Solution: Added deterministic Burst `RecordPowerTelemetryJob`. It reads `PowerNodeDTO` and demand lanes with `[NoAlias]`, finite-clamps inputs, computes total generation/load/supply ratio/average/min/max potential/brownout count, writes one `PowerTelemetryEntry`, and advances the 64-byte `PowerGridCounter64` cursor. Added editor regression test `RecordPowerTelemetryJob_WritesGenerationLoadPotentialAndCursor`.
Rejected Alternatives: Writing telemetry through managed strings, LINQ summaries, or per-frame manager-side loops was rejected because Task 16 is a black-box native recorder requirement and the hot telemetry path must stay blittable. Reusing the old graph-owned blackbox array was rejected because Vault owns critical persistent proof lanes.
Scalability potential: Low through Ultra write the same fixed 300-frame proof ring. The job does not introduce a quality branch; solver fidelity still scales through Jacobi iterations and brownout visual richness stays shader-side.
Hardware Impact: One linear native fold over the solved node slice, expected <=10 us for the recorded power subset on low silicon. The 64-byte cursor avoids cache-line contention for the ring write index.
