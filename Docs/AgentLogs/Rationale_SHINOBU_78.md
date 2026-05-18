# Rationale_SHINOBU_78

Status: PENDING VERIFICATION

## Initial Boundary

Problem: `SHINOBU_78` duplicates existing memory-sentinel functionality while adding mod-protection and future command-kernel opcode reservation pressure.
Solution: Treat existing `MemorySentinelRuntime` as the primary integration point and only add missing integrity/quarantine surfaces after source readback.
Rejected Alternatives: Rewriting a second sentinel would create two schedulers hashing the same vault ranges and produce false desyncs under parallel agents.
Scalability potential: Low = player/economy only; Middle = base integrity and registry hashes; High = richer telemetry; Ultra = broader validation density without binary quality switches.
Hardware Impact: Avoiding a duplicate runtime scheduler preserves estimated 20-60 us/frame on i3/MX350 and prevents extra DataVault buffers.

Problem: Mandate selection before code.
Solution: Use eight directly relevant mandates: Zero-GC, Native Jobs, ARM64 struct layout, SoA inventory, AUP determinism, floating origin precision, execution phases, typed signal lanes.
Rejected Alternatives: Reading graphics/audio mandates would dilute the domain; using dated archive reports as authority would violate current AGENTS hierarchy.
Scalability potential: Mandates force continuous GlobalQualityWeight cadence rather than low/ultra toggle.
Hardware Impact: Bounded job and layout rules protect Quest/MX350 from unaligned access and managed heap stalls.

## Decision 01 - Mod quarantine cannot weaken base data

Problem: `AppendExistingBuffer` was adding `TargetFlagAllowModPrefix` to every protected DataVault span. In a modded session, a malicious write could plant `MODP` at the start of base inventory/AUP memory and make the sentinel skip that target.
Solution: Add a separate 64-byte `MemorySentinelModQuarantineSpan` seeded with `MODP`, allocate it as `ModQuarantineBuffer`, and make it the only runtime target with `TargetFlagAllowModPrefix`. Base buffers now append only `TargetFlagActive` plus their critical/AUP/inventory flags.
Rejected Alternatives: Global mod-mode disable was rejected because it sacrifices base-game integrity. String/path mod whitelists were rejected because they are managed and forgeable. Per-buffer reflection metadata was rejected because it invents dependencies on systems that are not in this domain.
Scalability potential: Low = one quarantine span and player/economy hashes; Middle = more mod spans by DataVault ID; High = richer per-mod telemetry; Ultra = larger quarantined UGC surface while first-party memory remains fully protected.
Hardware Impact: Adds one 64-byte hash target only when scheduled. Low-end i3/MX350 cost estimate is 1-3 us on validation frames, while closing an integrity bypass that would otherwise invalidate the sentinel.

## Decision 02 - Future command-kernel opcode reservation

Problem: Current mod command opcodes use 1-8, but no guard stopped public mods or kernels from occupying a future command-kernel range.
Solution: Reserve `0x7800..0x78FF` for both opcode and target IDs. `ModCommandDispatcher` rejects that range at kernel registration and security-gate validation. `FutureSystemSeamContracts` exposes the same range and a validation helper for seam auditors.
Rejected Alternatives: Waiting for Agent 80 was rejected because mods can ship against the current API surface. Adding a dependency from ModdingAPI to `Hecton8.Global.Contracts` was rejected to avoid asmdef compile-wall expansion.
Scalability potential: Low = hard reject reserved IDs; Middle = seam validator consumes range constants; High = future command kernel binds the range without API break; Ultra = generated contract tables can expand inside the reserved band.
Hardware Impact: Two unsigned range comparisons in the cold command gate. Estimate: <0.1 us per mod command, 0 us on frames with no mod commands.

## Decision 03 - Dump identity and black-box evidence

Problem: The global black-box rule requires `Docs/AgentLogs/Dump_[YourID].bin`, while the inherited sentinel path used an older role-based filename.
Solution: Route fatal/NaN/manual dumps to `Docs/AgentLogs/Dump_SHINOBU_78.bin` while preserving the same 300-frame fixed NativeArray telemetry payload.
Rejected Alternatives: Keeping `Dump_INTEGRITY_SURGEON.bin` was rejected because CTO review is ID-indexed. Writing text dumps was rejected because binary circular buffers preserve exact fixed-size state.
Scalability potential: Low = 300 x 64-byte entries; Middle = same ring with more flags; High = richer mismatch state; Ultra = additional per-target forensic bands after the same ID path.
Hardware Impact: 0 us in normal frames. Fatal/manual dump cost is cold I/O only.

## Decision 04 - Pointer protection without phantom dependencies

Problem: The prompt asks for GlobalRegistry `FunctionPointer<T>` protection, but no Agent 80 function-pointer registry segment exists in the active Core domain.
Solution: Keep protection at the concrete memory descriptor boundary: every target stores `FunctionPointerHash`/pointer fingerprint and the Burst job flags pointer/fingerprint mutation. Future Agent 80 can publish a DataVault registry buffer and it can be appended with `TargetFlagPointerRegistry` without coupling today.
Rejected Alternatives: Inventing a registry buffer or reading private static function-pointer fields was rejected as architectural sabotage and would require reflection or non-owned dependencies.
Scalability potential: Low = target pointer fingerprints; Middle = published registry buffer; High = periodic kernel pointer hash lanes; Ultra = command-kernel signed pointer table.
Hardware Impact: Current per-target fingerprint check is a few integer ops, estimated 5-12 us across the validation set on i3/MX350 cadence frames.

## Decision 05 - Compile wall handling

Problem: Build verification is required, but the environment had CPU at 100.0 and active `dotnet`/`csc.exe` processes. The batch explicitly forbids launching dotnet under those conditions.
Solution: Do not start a build. Mark compile as externally blocked, continue static self-audit, and leave exact process evidence in status/log.
Rejected Alternatives: Launching another `dotnet build` was rejected because it violates the batch CPU/process gate and risks worsening another agent's compile.
Scalability potential: Low/Middle/High/Ultra unaffected; this is workflow integrity, not runtime.
Hardware Impact: Avoided adding another compiler process under saturation; preserves machine time for existing compiles.

<SELF_AUDIT agent_id="SHINOBU_78">
  <Q1>No managed memory scanners or Reflection were added. Runtime anti-tamper uses direct `void*` hashing, NativeArrays, and Burst jobs; cold CSV/dump file I/O remains outside the hot validation path.</Q1>
  <Q2>`ValidationStateDTO` is explicit 32 bytes: offset 0 `ulong TargetMemoryPointer`, 8 `uint ExpectedHash`, 12 `uint StoredHash`, 16 `uint CheckInterval`, 20 `uint _pad0`, 24 `ulong _pad1`.</Q2>
  <Q3>Sentinel runtime DTO grep for `{ get; set; }` and `{ get; private set; }` returned no matches.</Q3>
  <Q4>`GlobalQualityWeight` continuously changes validation cadence and target gating; no low/ultra binary switch was introduced.</Q4>
  <Q5>`Memory Sentinel Tuner` exists and now exposes validation parameters, CSV load, dump, tamper simulation, and mod quarantine mask control.</Q5>
</SELF_AUDIT>

## Decision 06 - Real mod lifecycle must feed quarantine state

Problem: The first hardening pass allowed the editor facade and CSV path to set `ModdedGameMask`, but the live mod lifecycle could register mods before the sentinel runtime existed.
Solution: Publish a 64-byte unmanaged `ModdedGameMaskSignal` from `ModCommandDispatcher.RegisterMod`, `UnregisterMod`, and `Shutdown`. `MemorySentinelRuntime` configures the typed lane and consumes the latest snapshot before target resolution.
Rejected Alternatives: Polling `ModLoader` every frame was rejected because it couples hot validation to managed mod metadata. Direct `ModCommandDispatcher -> MemorySentinelRuntime` static notification was rejected in the third polish pass because the batch demands GlobalRegistry/SignalBus style isolation. Editor-only toggling was rejected as test-only coverage.
Scalability potential: Low = one global modded bit; Middle = extend pending mask by mod category; High = per-quarantine-span mod class bits; Ultra = signed per-mod memory lanes while base memory remains fully hashed.
Hardware Impact: One typed signal publish on mod lifecycle events, estimated <0.1 us per event and 0 us/frame when no mod lifecycle changes occur.

## Decision 07 - Default repair cannot erase integrity state

Problem: Runtime state repair treated `Strictness01 <= 0` as invalid and rewrote `ModdedGameMask` to zero. That created two failures: a valid minimum-continuum strictness was rejected, and mod quarantine could silently disengage after any default repair.
Solution: Validate finite/out-of-range values explicitly, accept `Strictness01 = 0`, preserve the existing mod mask during repair, and then apply the pending mod mask if one exists.
Rejected Alternatives: Clamping every field every frame was rejected because it dirties the runtime DTO unnecessarily. Binary strictness was rejected because the project requires continuous `GlobalQualityWeight` and continuous tuning.
Scalability potential: Low = minimum strictness still runs at survival cadence; Middle = standard cadence; High = dense cadence; Ultra = full validation density. No load screen or hard mode flip.
Hardware Impact: A few finite/range checks during state resolve; estimated <1 us on validation frames, with correctness gain preventing false trust of modded memory.

## Decision 08 - Tamper simulation is not a production command

Problem: `TrySimulateCheatEngineWrite` was public and could schedule an editor test job with `Schedule().Complete()`. The button is required, but the production API surface should not expose a memory-write test command.
Solution: Compile-gate the public simulation entry so non-editor/non-development builds return false. In the editor/development path, execute the deterministic mutation kernel directly instead of scheduling and synchronously completing a job.
Rejected Alternatives: Removing the button was rejected because Task 20 explicitly requires live tamper simulation. Keeping `Schedule().Complete()` was rejected because it creates a false dependency-stall pattern in a security-sensitive file.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; this is test-surface containment.
Hardware Impact: Avoids one avoidable job schedule/complete during explicit editor tests; 0 us/frame in production.

<SELF_AUDIT_POLISH agent_id="SHINOBU_78">
  <TWENTY_TASK_RECONCILIATION>
    <T01 status="PASS">Archive scan found no `validation_keys_00*.h8bin`; fallback mock remains explicit, not invented binary layout.</T01>
    <T02 status="PASS">No managed scanner/reflection/process anti-cheat added.</T02>
    <T03 status="PASS">Hot DTOs use fields and ref access, no DTO properties.</T03>
    <T04 status="PASS">Primary DTO is 32 bytes; sentinel spans are 64-byte explicit/sequential fixed-size layouts.</T04>
    <T05 status="PASS">Mock inventory mutation exists without Agent 19 dependency.</T05>
    <T06 status="PASS">Burst validation hashes direct pointers with deterministic float mode.</T06>
    <T07 status="PASS">Hash delta signal updates expected hashes for legal mutations.</T07>
    <T08 status="PASS">Rollback mirror corrects non-critical desyncs; WAL Agent 72 dependency was not invented.</T08>
    <T09 status="PASS">Critical tamper dumps black box and throws fatal exception.</T09>
    <T10 status="PASS">AUP heuristic checks raw double3 bytes and impossible displacement.</T10>
    <T11 status="PASS">Cadence and target gates follow continuous `GlobalQualityWeight`.</T11>
    <T12 status="PASS">`MODP` quarantine is isolated; real mod lifecycle now feeds `ModdedGameMask`.</T12>
    <T13 status="PASS">AUP hashes use raw 64-bit bytes, not float truncation.</T13>
    <T14 status="PASS">VisualSync scheduling consumes previous results without arbitrary current-frame blocking.</T14>
    <T15 status="PASS">Pointer fingerprints protect target descriptors; no phantom Agent 80 buffer dependency.</T15>
    <T16 status="PASS">Vault buffers allocate with `UninitializedMemory` where overwritten deterministically.</T16>
    <T17 status="PASS">300-frame telemetry ring dumps to `Docs/AgentLogs/Dump_SHINOBU_78.bin`.</T17>
    <T18 status="PASS">Editor tuner facade exposes tuning, CSV, dump, mod mask, and tamper simulation.</T18>
    <T19 status="PASS">CSV parser uses byte scratch, not `Split`/LINQ.</T19>
    <T20 status="PASS">Tamper button is editor/development-only and mutation is deterministic.</T20>
  </TWENTY_TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <ValidationStateDTO size="32">0:ulong TargetMemoryPointer, 8:uint ExpectedHash, 12:uint StoredHash, 16:uint CheckInterval, 20:uint _pad0, 24:ulong _pad1. Final size 32 = 16 * 2.</ValidationStateDTO>
    <MemorySentinelModQuarantineSpan size="64">0:uint Prefix, 4:uint ModHash, 8:uint MutationCounter, 12:uint Flags, 16/24/32/40/48/56: six ulong payload words. Final size 64 = one L1 cache line.</MemorySentinelModQuarantineSpan>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, cadence collapses toward 1Hz survival validation and low-quality target gates skip non-critical spans. Near 1.0, cadence approaches configured 10Hz and richer spans stay eligible. Transition uses saturate/lerp/smooth polynomial, not low/high switches.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No private persistent `NativeArray` fields in the sentinel runtime. Handles requested: 70873 states, 70874 targets, 70875 results, 70876 rollback bytes, 70877 mock inventory, 70878 telemetry, 70879 runtime state, 70880 AUP snapshot, 70881 CSV scratch, 70882 mod quarantine.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>`MemorySentinelValidationJob` consumes states/targets/results plus desync writer and uses `[NoAlias]` on NativeArray fields. Scheduler output is `_validationHandle`; current-frame path only consumes completed previous work unless teardown/test forces completion.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>No new asmdef sibling reference was added. Existing Core/ModdingAPI assembly debt is pre-existing; mod lifecycle now communicates through a typed SignalBus lane rather than a concrete runtime static call.</COMPILE_GUARD>
  <DEAR_LIE>Algorithm before fake: scan arbitrary process/RAM/mod allocations, O(total memory). Algorithm after fake: hash explicit vault spans plus one 64-byte quarantine span, O(selected protected bytes). UGC mutability is represented by `MODP` quarantine data instead of trying to divine all mod memory.</DEAR_LIE>
</SELF_AUDIT_POLISH>

## Decision 09 - Mod lifecycle isolation must use SignalBus

Problem: A fresh source scan found two issues the prior report text did not prove away: SH73 identity residue was still present in current source, and `ModCommandDispatcher` directly called `MemorySentinelRuntime.TrySetModdedGameMask`.
Solution: Restore SH78 runtime/editor identity and replace the direct static lifecycle call with `ModdedGameMaskSignal`, an explicit 64-byte typed SignalBus payload. The sentinel consumes the latest mask before building validation targets.
Rejected Alternatives: Keeping the direct call was rejected because it violates the architectural isolation rule even inside the current broad Core assembly. Polling mod state from the sentinel was rejected because it creates managed metadata coupling and per-frame work. A small sequential signal struct was rejected because the signal snapshot should remain cache-line explicit.
Scalability potential: Low = one global mask signal; Middle = active mod count helps quarantine telemetry; High = flags can carry per-class trust bands; Ultra = future signed mod-lane masks can fit without changing the public lane contract.
Hardware Impact: Signal publication occurs only on mod register/unregister/shutdown, estimated <0.1 us per lifecycle event. Runtime consumption scans an 8-entry maximum snapshot before target build, estimated 1-2 us only on frames with lifecycle signals, 0 us when snapshot is empty.

<SELF_AUDIT_SIGNAL_ISOLATION agent_id="SHINOBU_78">
  <SIGNAL_LAYOUT>`ModdedGameMaskSignal` is 64 bytes: 0:uint mask, 4:uint active count, 8:uint frame, 12:uint source hash, 16:uint flags, 20:uint pad, 24/32/40/48/56 five ulong pads.</SIGNAL_LAYOUT>
  <ROUTING>`ModCommandDispatcher` publishes `SignalBus&lt;ModdedGameMaskSignal&gt;`; `MemorySentinelRuntime` reads the snapshot and no longer receives a direct call from the mod dispatcher.</ROUTING>
  <SOURCE_DRIFT>`rg` found current SH73 residue before this pass; runtime hash/name/fatal strings and editor warning were restored to SH78 in source, not just in logs.</SOURCE_DRIFT>
  <COMPILE_WALL>No asmdef reference was added. Communication is via existing Core.Contracts.Signals infrastructure.</COMPILE_WALL>
</SELF_AUDIT_SIGNAL_ISOLATION>
