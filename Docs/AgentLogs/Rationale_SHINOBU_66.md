# Rationale_SHINOBU_66

Status: POLISH STATIC - scoped compile gated by CPU/compiler load after property purge.
Evidence Class: STATIC_SOURCE until compile/profiler proof exists.

## Decision 000 - Duplicate Prompt Hygiene

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_66` prompts. The existing status/rationale belonged to the earlier DRS prompt, while the user explicitly assigned `SHINOBU_MOD_SANDBOX_VALIDATOR`.
Solution: Treat the later `role="MOD_SANDBOX_AND_OPCODE_VALIDATOR"` block as active authority and replace state files with MOD sandbox state.
Rejected Alternatives: Continuing DRS work was rejected because it contradicts the user's explicit UGC security directive. Reading neighboring prompts as requirements was rejected by strict parsing.
Scalability potential: No runtime effect; prevents wrong-domain work from consuming integration time.
Hardware Impact: No frame impact. Prevents compile churn in the wrong presentation domain.

## Decision 001 - Envelope-Only Sandbox Direction

Problem: Existing `ModdingAPI` exposes `ModCommand` and managed `IHectonMod` callback surfaces. That can still charge GC and creates a path for user code to run inside engine cadence.
Solution: Add a narrow `FutureCommandEnvelope` ABI and validator that accepts only 64-byte unmanaged envelopes, checks opcode/integrity/AUP/budget, and translates to internal unmanaged signal payloads.
Rejected Alternatives: Harmony/BepInEx/runtime reflection patching is rejected outright. Direct gameplay class calls from mods are rejected because they break rollback and Zero-GC assumptions. Fully deleting legacy mod UI/content systems in this pass is rejected because it crosses wider domains and can break menus; the active runtime command path must be envelope-only.
Scalability potential: Low = 10-100 envelopes per frame by `GlobalQualityWeight`; Middle = moderate queue drain; High = richer optional signals; Ultra = more accepted UGC traffic only after core frame remains under budget.
Hardware Impact: On i3/MX350, throttling from 1000 to roughly 100 or lower prevents mod spam from consuming >0.1ms and protects core simulation.

## Decision 002 - Emergency Opcode Registry

Problem: `Docs/Archive/` contains no `allowed_mod_opcodes.h8bin`; the only found future command contract is `FutureCommandEnvelope64`, whose layout does not match the assigned UGC ABI.
Solution: Seed a 16-byte `FutureCommandOpcodeRecord` fallback table with FNV-1a hashes for `SPAWN_ITEM_OP`, `ALTER_HEALTH_OP`, `ALTER_GRAVITY_OP`, asset, memory, fauna, and subtitle future seams. Store it in a Vault-owned fixed array, not a private `NativeHashSet`.
Rejected Alternatives: Reusing `FutureCommandEnvelope64` was rejected because it has a different word layout. Adding public `ModCommandOpcode` enum values was rejected because the future reservation doc forbids claiming owner kernels early.
Scalability potential: Low keeps only essential opcodes enabled; Middle/High/Ultra can enable additional seams through the editor or CSV without recompilation.
Hardware Impact: Fallback lookup is a small fixed-array scan over <=32 records, estimated 0.02-0.08 us per envelope on low-end silicon without allocator or hash-map residency.

## Decision 003 - Burst Quarantine Kernel

Problem: Mods can flood commands, corrupt AUP coordinates, forge payloads, and force core systems into GC-heavy managed callback paths.
Solution: Added `FutureCommandSandboxValidator` with explicit 64-byte `FutureCommandEnvelope`, XXHash3 integrity over bytes 0..47, finite +/-50km AUP rejection, CRC32 asset manifest gate, 64B per-signature counters, DevNull routing, and unmanaged SignalBus outputs.
Rejected Alternatives: Reflection dispatch, Harmony/BepInEx hooks, direct gameplay object references, private `NativeHashMap` state, and direct rollback assembly references were rejected. Scheduling a fake async job then immediately completing it was rejected; the PRE_SIMULATION validator uses a bounded Burst `Run()` path because the existing dispatcher phase is synchronous.
Scalability potential: Low = 10 commands/signature and small global drain; Middle = hundreds; High = default 1000/signature; Ultra = up to tuner cap 10000 while still bounded.
Hardware Impact: On i3/MX350, command flood cost is capped by the global drain, thermal backlog shed, and per-mod counter before gameplay systems see packets; expected saved worst-case is unbounded spam down to <0.1ms target envelope work.

## Decision 004 - Vault-Backed Mod Memory Isolation

Problem: UGC custom variables cannot mutate core DTOs without breaking rollback, ownership, and DataVault invariants.
Solution: Added `BufferID.ShinobuModSandboxBlackboxMemory` and assigns fixed chunks by `ModderSignature` through a Vault-owned open-address lease table. Memory read/write opcodes are range-checked in Burst; core rollback ignores this arena.
Rejected Alternatives: Adding mod fields to engine DTOs was rejected as cross-domain sabotage. Heap dictionaries were rejected for GC and rollback instability.
Scalability potential: Low = small chunks and rejected overflow; Middle = 16MB default; High/Ultra = tuner raises max memory while preserving chunk isolation.
Hardware Impact: On i3/MX350, fixed byte slices avoid managed hash/object storage and keep memory writes to 1-4 bytes per accepted opcode.

## Decision 005 - Managed Entry Quarantine

Problem: Existing `ModLoader` could instantiate `IHectonMod` and call `OnLoad`/`OnInitialize`, preserving a managed execution lane that can allocate and desync.
Solution: Managed-entry candidates are disabled with an explicit message; content-only assets/localization remain loadable, and active runtime commands must enter through `HectonAPI.Commands.RequestFuture`.
Rejected Alternatives: Keeping managed callbacks and relying on allocation policing was rejected because the user ordered envelope-only UGC. Deleting content asset loading was rejected because asset validation is handled by CRC-gated opcodes and does not require running mod code.
Scalability potential: Low/Medium/High/Ultra all share the same no-code-execution security boundary; only accepted envelope volume scales.
Hardware Impact: Removes per-callback managed allocation exposure entirely from the mod path.

## Decision 006 - Editor Facade and CSV Hot Gate

Problem: The lead needs human control over opcode gates, budget, mod memory, and live rejection visibility without adding runtime HUD allocations.
Solution: Added `Mod API Sandbox Tuner` editor window with sliders/toggles, `allowed_opcodes.csv` reload into the Vault opcode table, self-audit injection, blackbox dump button, and `EditorGUI.DrawRect` histogram.
Rejected Alternatives: Runtime UI was rejected because it would add in-game allocation and presentation coupling. `string.Split`/LINQ runtime parsing was rejected; the runtime parser consumes `NativeArray<byte>`.
Scalability potential: Low can disable expensive seams and force quality down; Middle/High/Ultra can expand command and asset budgets with continuous values.
Hardware Impact: Editor-only cost in development; runtime parser and gates stay allocation-free once bytes are provided.

## Decision 008 - H-PHI Vault Refactor

Problem: The first implementation still owned persistent `NativeQueue`, `NativeArray`, `NativeParallelHashSet`, and `NativeParallelHashMap` fields. That violates the Vault law and gives mods a path to allocator churn under flood.
Solution: Replaced validator-owned containers with `VaultBufferHandle<T>` fields and short-lived resolved `NativeArray<T>` views. Pending/DevNull are fixed Vault rings. Opcode records, per-mod counters, memory leases, and approved assets are fixed open-address arrays. `FutureCommandValidationStats`, `ModderFrameCounter`, and `ModSandboxRingState` are 64B explicit structs.
Rejected Alternatives: Keeping private native containers was rejected even though it was easier. `NativeHashMap` was rejected because its bucket storage is allocator-owned and not visible as a stable Vault contract. Managed dictionaries were rejected for GC.
Scalability potential: Low/Middle/High/Ultra all share the same memory topology; only budgets and table occupancy change. Low devices shed backlog; Ultra can fill the fixed rings without allocator spikes.
Hardware Impact: On i3/MX350, the hot path avoids hash-map allocator indirection and false-sharing counters; expected gain is stability more than raw microseconds, with flood behavior bounded to fixed cache-resident tables.

## Decision 009 - CPU Overheat Packet Shedding

Problem: Merely reducing the drain budget lets bad mods accumulate backlog. Under thermal pressure, delayed UGC packets can become a second-order hitch later.
Solution: Added `DropThermalBacklog`: when `GlobalQualityWeight < 0.3`, overflow above a safe window is dropped by a continuous shed curve `saturate((0.30 - q) * 3.3333333)`. The mod lags or loses commands; the core frame remains protected.
Rejected Alternatives: Binary low/high throttles and unlimited backlog were rejected. Blocking the main thread to catch up was rejected.
Scalability potential: Low drops aggressively, Middle drops only overflow, High/Ultra process without shed unless the homeostasis signal falls.
Hardware Impact: On i3/MX350, backlog collapse prevents thermal recovery frames from being spent on stale UGC work.

## Decision 010 - Compile-Wall Rollback View

Problem: `FutureCommandSandboxValidator` only needed the rollback resimulation bit, but referencing `Hecton8.Networking` directly creates a sibling runtime dependency.
Solution: Removed the networking using and reads buffer `70752` through a local explicit 64B `RollbackRuntimeStateFlagView` at offset 44, checking bit `1 << 4`.
Rejected Alternatives: Direct `RollbackNetcodeVault`/`RollbackRuntimeStateDTO` reference was rejected for compile-wall hygiene. Reflection was rejected. Ignoring rollback was rejected because Task 13 requires freeze during resimulation.
Scalability potential: No visual tier impact; this is architectural isolation.
Hardware Impact: Same single Vault read as before, but avoids compile-wall churn when Networking changes.

## Decision 011 - Bulk Ingress and Endian Hygiene

Problem: `RequestRawEnvelopeStream` still called `Request()` per envelope, paying Vault resolution and ring-state read/write for every 64B packet. The original assignment also allowed an external `NativeQueue` producer boundary, while the Vault refactor removed the validator-owned queue. Asset approval stored CRC but not the approved byte length.
Solution: Rewrote raw stream ingestion to resolve Vault once, enqueue all packets into the pending ring, and write ring state once. Added `RequestRawEnvelopeStream(..., sourceBigEndian)` to normalize legacy/big-endian field bytes before validation. Added `RequestFromExternalQueue(ref NativeQueue<FutureCommandEnvelope>, maxEnvelopeCount)` so producers can still use a queue without validator-owned allocator state. Added approved asset byte-length storage and validation.
Rejected Alternatives: Per-packet `Request()` was rejected as repeated Vault lookup overhead. Reintroducing a private persistent queue was rejected by the Vault law. Size-blind CRC approval was rejected because corrupted or oversized assets can still match an approved hash path if the declared byte contract is ignored.
Scalability potential: Low drains a small bounded queue/stream once, then thermal shed drops overflow. Middle/High/Ultra increase envelope volume without changing allocator topology. Big-endian support stays opt-in and cold/compatibility-facing.
Hardware Impact: On i3/MX350, bulk stream ingress removes thousands of redundant Vault resolves during mod floods; exact profiler proof pending behind external compile wall.

## Decision 012 - Mock Queue Ownership Purge

Problem: `MockModQueue.Initialize(int)` still created a `NativeQueue<FutureCommandEnvelope>` with `Allocator.Persistent`. Even though this was not validator-owned static runtime state, it left a weak allocation seam inside the quarantine domain.
Solution: Removed the persistent allocator path from `MockModQueue`. The mock now wraps or attaches to an external caller-owned `NativeQueue`, and `Dispose()` only releases the wrapper handle. External producers and tests own their queue lifetime; the validator drains through `RequestFromExternalQueue`.
Rejected Alternatives: Keeping a convenience allocator inside the mock was rejected because it contradicts the Vault/external-producer boundary. Moving the mock queue into validator static state was rejected as worse.
Scalability potential: Low/Middle/High/Ultra all keep the same no-owned-allocator topology; test injection volume can scale only through caller-owned buffers.
Hardware Impact: Removes one accidental persistent allocator path from the domain and preserves deterministic allocator ownership under UGC flood tests.

## Decision 013 - Scheduled Validation Chain Seam

Problem: The current `ModCommandDispatcher.DrainPreSimulation()` is a legacy void phase, so the validator had a bounded `Run()` path and no way for the master dispatcher to weave it into a `JobHandle` graph later.
Solution: Added `TrySchedulePreSimulation(JobHandle dependsOn, out JobHandle validationHandle)` and `TryFinalizeScheduledPreSimulation(bool forceComplete)`. The scheduled path drains pending packets into Vault staging, schedules the same deterministic Burst job with caller dependency, registers the handle under `SystemID.ModSandbox`, and finalizes telemetry only when completed or at teardown. The old `DrainPreSimulation()` remains for the current void dispatcher without touching massive core dispatcher files.
Rejected Alternatives: Editing `SystemDispatcher.cs` from this domain was rejected as compile-wall sabotage. Scheduling then immediately completing was rejected as a fake async path. Leaving no JobHandle seam was rejected by the concurrency mandate.
Scalability potential: Low/Middle/High/Ultra all retain the same packet budget math; scheduled mode lets higher-tier machines overlap validation with independent work while low-tier devices can defer or skip finalization until the fence is naturally complete.
Hardware Impact: On i3/MX350, avoids introducing an unconditional main-thread fence when the integrator adopts the scheduled path. Measured gain is pending behind the existing compile wall.

## Decision 014 - Property and Editor Scratch Purge

Problem: The validator still exposed property-style seams for mock queue readiness, initialization flags, scheduled state, and pending/devnull counts. The editor tuner also kept a private `NativeArray<ModSandboxTelemetryEntry>` scratch buffer, which was editor-only but weakened the H-PHI "no private array ownership" audit story.
Solution: Replaced the property-style seams with explicit methods (`GetIsCreated`, `GetPendingEnvelopeCount`, `GetDevNullEnvelopeCount`, `GetIsInitialized`, `GetHasScheduledValidation`). Added `TryGetTelemetryEntry(int, out ModSandboxTelemetryEntry)` so the editor histogram reads Vault telemetry entries directly without owning a native scratch array.
Rejected Alternatives: Leaving the properties because they were not `{ get; set; }` was rejected; the mandate calls properties methods in disguise and the validator should not force auditors to distinguish expression-bodied properties from setters. Keeping the editor `NativeArray` was rejected because the editor facade can read Vault-owned telemetry directly.
Scalability potential: Low/Middle/High/Ultra keep identical runtime topology; editor graph density is capped at the fixed 300-frame ring.
Hardware Impact: Runtime hot path impact is effectively zero; the gain is compile/audit clarity and one less editor-only native allocation path.

## Decision 007 - Verification Block

Problem: The project forbids launching `dotnet` build while CPU is >50% or `csc.exe` is active. CPU later dropped below the gate, but the first compile probe used the wrong response file and the correct `Hecton8.Core.rsp` compile hit unrelated existing dependency errors.
Solution: Did not launch full `dotnet build`. Ran `git diff --check` and static grep passes. Attempted scoped Roslyn compilation only after CPU dropped and no compiler was active. `Assembly-CSharp.rsp` was rejected as the wrong assembly. `Hecton8.Core.rsp` reached non-owned errors in `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`; no `FutureCommandSandboxValidator.cs` errors were emitted before the external wall. After ingress hardening and again after mock ownership purge, repeated the gated scoped Roslyn probe and hit the same first errors: `Hecton8.Construction.MockWorldSampler`, `HectonRollbackNetcodeRuntime`, `VolcanicUpdraftDirector`, and missing construction DTOs. After the JobHandle seam, compile was not relaunched because CPU sampled 100% with external `dotnet.exe`/`csc.exe`; after those processes exited CPU still sampled 85%, then 100% with no compiler process, so the build gate remained closed. After property/facade purge, compile was again not launched because CPU sampled 79% with one active external `dotnet.exe`, then 100% with no compiler process. Stale compiler child processes were stopped/verified gone only when they belonged to this probe.
Rejected Alternatives: Violating the CPU/build rule was rejected. Editing construction/networking/world files to force this lane's compile proof was rejected as cross-domain work.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding build load to an already saturated machine.

## Struct Layout Audit Targets

- `FutureCommandEnvelope`: `uint OpcodeHash` offset 0 size 4; `uint ModderSignature` offset 4 size 4; `double3 TargetAUP` offset 8 size 24; `float4 PayloadData` offset 32 size 16; `ulong IntegrityHash` offset 48 size 8; `ulong _pad0` offset 56 size 8; total 64 bytes, no `Pack=1`.
- `FutureCommandValidationStats`: explicit 64 bytes to avoid false-sharing when read beside other frame state.
- `ModderFrameCounter`: explicit 64 bytes; `uint ModderSignature` offset 0, `uint Frame` offset 4, `int Count` offset 8, `int Dropped` offset 12, six `ulong` pads through byte 63.
- `ModSandboxRingState`: explicit 64 bytes; pending/devnull heads/tails/counts, lease/opcode/asset counts, dump frame, and pads.
- `ModSandboxTelemetryEntry`: unmanaged 64 bytes for 300-frame blackbox ring.

## Scalability Matrix

- Low: command budget collapses through continuous quality to emergency floor; thermal backlog shed drops overflow above a safe window; asset and fauna opcodes are accepted only as cheap signals or DevNull.
- Middle: bounded spawn/memory/asset requests with strict CRC and per-mod counters.
- High: higher command budget and richer telemetry, still no direct code execution.
- Ultra: same ABI, larger accepted burst volume and visual-overkill consumers in downstream presentation lanes only.
