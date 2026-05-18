# SHINOBU_73 Rationale

## Initial Architectural Decisions

Problem: Vault hot data needs anti-tamper validation without managed obfuscation or runtime reflection scanners.
Solution: Use Vault-owned unmanaged buffers, Burst pointer-span hashing, typed SignalBus lanes, previous-frame VisualSync completion, and fixed-size telemetry.
Rejected Alternatives: Managed C# obfuscators and object graph scanners allocate, miss NativeArray payloads, and are trivial to bypass by patching managed metadata.
Scalability potential: Low = sparse cadence and critical targets only; Middle = inventory + AUP + selected Vault buffers; High = deeper target set; Ultra = aggressive cadence and larger rollback mirrors.
Hardware Impact: Low-end i3/MX350 target is bounded by cadence and byte budget, with validation work shifted to jobs and completion delayed to the next VisualSync frame.

Problem: Desync correction must be invisible for inventory tamper and teleport edits.
Solution: Keep last valid byte mirrors in Vault memory, correct mutated bytes after validation mismatch, and emit typed signals for diagnostics.
Rejected Alternatives: Immediate gameplay-visible lockout on first mismatch would expose the sentinel and create false positives on legal state changes.
Scalability potential: Low = rollback only for critical spans; Middle = rollback plus hash delta updates; High/Ultra = more spans and richer telemetry.
Hardware Impact: Copy cost is proportional to span size and capped by fixed rollback capacity.

Problem: Legal inventory/AUP changes must not look like cheats.
Solution: Consume typed hash-delta updates and AUP shift signals; only unannounced direct byte changes trigger rollback.
Rejected Alternatives: Hash-only comparison without semantic update path would punish legitimate gameplay and save/load transitions.
Scalability potential: Same DTO and signal lanes scale from one mock span to many registered target spans.
Hardware Impact: Signal reads are snapshot spans; no managed allocations in hot path.

Problem: Validator state must survive context compression and concurrent agent work.
Solution: Store task state and rationale in Docs/Tasks and Docs/AgentLogs before code edits.
Rejected Alternatives: Chat-only status is not authoritative under batch protocol.
Scalability potential: Enables integrator to audit progress without replaying chat.
Hardware Impact: Documentation only; no runtime impact.

## Decision 001 - Assembly Boundary Split
Problem: Core/Memory asmdef owns GlobalDataVault contracts but cannot reference GlobalRegistry/SystemDispatcher without a cyclic assembly dependency.
Solution: Put Burst DTOs/jobs in Hecton8.Core.Memory and put runtime driver/signals/editor facade in Hecton8.Core/Hecton8.Editor.
Rejected Alternatives: Moving GlobalRegistry-facing code into Core/Memory would create an assembly cycle. Moving all DTOs out of Core/Memory would weaken the DataVault ownership boundary.
Scalability potential: Low/Middle/High/Ultra all use the same Vault handles; quality only changes cadence and target depth.
Hardware Impact: 0 us claimed; this is compile topology. Runtime avoids registry polling after cache unless Vault is missing.

## Decision 002 - Emergency Mock Signatures
Problem: Archive search did not find validation_keys_006.h8bin or an authoritative byte layout for validation signatures.
Solution: Seed a 64-byte MockInventorySpan in Vault memory and generate expected XXHash3 from live bytes during initialization.
Rejected Alternatives: Fabricating an old binary key layout would be false evidence. Using managed arrays would violate DataVault sovereignty.
Scalability potential: Low tier can validate only the mock/economy/AUP lanes; higher tiers add broader VaultAup64 coverage.
Hardware Impact: 64B mock hash is a micro-target; estimate 1-3 us on desktop-class CPU, not a profiler claim.

## Decision 003 - XXHash3 Instead Of Managed Scanner
Problem: Memory edits happen in unmanaged NativeArray/Vault spans where C# obfuscators and reflection scanners do not prove integrity.
Solution: Burst IJobParallelFor consumes raw void* spans and uses Unity.Mathematics.xxHash3.Hash64, folding to StoredHash while retaining FullHash64 telemetry.
Rejected Alternatives: CRC32-only, reflection, managed heap scanners, or string event reports.
Scalability potential: Low = critical spans and sparse cadence; Middle = inventory plus player AUP; High = VaultAup64 samples; Ultra = tighter cadence and more spans.
Hardware Impact: Target budget is <0.2 ms aggregate by byte budget and cadence. Exact saved microseconds are not claimed without profiler capture.

## Decision 004 - Invisible Rollback
Problem: Inventory tamper should not surface as visible lockout when the last legal byte mirror is available.
Solution: Maintain Vault-owned rollback bytes and MemCpy back over the tampered span when a mismatch is correctable.
Rejected Alternatives: Throwing on every mismatch creates false positives for legal gameplay and exposes the sentinel behavior.
Scalability potential: Low keeps only critical mirrors; High/Ultra can increase protected spans if Vault capacity is raised.
Hardware Impact: Copy cost is span-size bounded; mock is 64B, inventory spans are capped by target registration.

## Decision 005 - AUP Teleport Heuristic
Problem: A raw hash mismatch catches byte edits but not all semantically impossible teleports if another system writes a new legal-looking AUP.
Solution: Store the previous PlayerKinematicState AUP, compare double3 delta/required speed, allow AupShiftSignal and PlayerTransportBailoutSignal, then clamp back on illegal jumps.
Rejected Alternatives: Transform.position checks are not authoritative in HECTON-8. Hashing downcast floats would miss double precision tamper.
Scalability potential: The heuristic is one double3 operation on all tiers; only target set/cadence scales.
Hardware Impact: Estimated under 1 us for the scalar check; not profiler-claimed.

## Decision 006 - Mod Quarantine
Problem: Modded sectors must mutate without disabling base-game integrity.
Solution: Allow skip only for target ranges with MODP/0x4D50 prefix and nonzero ModdedGameMask.
Rejected Alternatives: A global modded-game bypass would remove the economy/player protection that matters.
Scalability potential: Same prefix check works on all tiers; higher tiers can protect more non-mod ranges.
Hardware Impact: Two to four byte prefix read per mod-flagged target.

## Decision 007 - Function Pointer Boundary
Problem: The prompt asks for GlobalRegistry FunctionPointer validation, but no exposed unmanaged registry segment is available without inventing a dependency.
Solution: Implement pointer fingerprint validation for every internal target record: pointer + byte length + buffer id are checked in Burst before hashing.
Rejected Alternatives: Scanning managed GlobalRegistry internals or inventing a fake FunctionPointer array would be unverifiable and fragile.
Scalability potential: Target fingerprints scale linearly with active target count and are gated by GlobalQualityWeight.
Hardware Impact: One FNV64-style mix per active target; tiny relative to span hashing.

## Decision 008 - Verification Wall
Problem: Local compile verification could not reach SHINOBU files through the generated project files.
Solution: Ran dotnet and Unity batch attempts, recorded objective blockers, and did not claim a green Unity compile.
Rejected Alternatives: Reporting success from stale generated csproj would be fake; patching EconomyRuntimeInstaller is outside SHINOBU_73 domain.
Scalability potential: None; validation process only.
Hardware Impact: 0 runtime us. External blockers observed during retries: missing TradeMarauderDirector in EconomyRuntimeInstaller, missing WristHudQuadTransformDTO in DiegeticGlitchSurgeonRuntime, and open Unity instance. Final dotnet rerun for Hecton8.Core.csproj succeeded, but the generated csproj still does not include the new MemorySentinel files.

## Decision 009 - Player AUP Contract Correction
Problem: The first runtime draft referenced `Hecton8.Gameplay.PlayerKinematicState`, but `BufferID.PlayerKinematicState` is actually written by the Core lockstep path as `LockstepPlayerKinematicState`.
Solution: Remove the Gameplay namespace dependency and consume the existing Core determinism vault contract for that buffer. Reconstruct absolute meters from sector/local using `HectonPhysicsContract.AupSectorSizeMetersDouble`, then write sector/local back only on rollback.
Rejected Alternatives: A local 96-byte mirror looked cleaner but would fail DataVault alignment validation because the vault stores both stride and alignment. `Transform.position` was rejected because it is not AUP authority.
Scalability potential: Low/Middle/High/Ultra all pay one scalar AUP check when the buffer exists. Broader AUP hashing still scales through `VaultAup64` target count and `GlobalQualityWeight`.
Hardware Impact: Expected under 1 us for the scalar check on i3/MX350 class CPUs; not profiler-claimed.

## Decision 010 - Deterministic Burst and Aliasing Proof
Problem: Default `[BurstCompile]` leaves compile timing and float mode implicit, and unannotated `NativeArray` fields make Burst assume pointer aliasing.
Solution: Add `CompileSynchronously = true`, `FloatMode.Deterministic`, `FloatPrecision.Standard`, and field-level `[NoAlias]` to the validation and mock mutation jobs.
Rejected Alternatives: `FloatMode.Fast` is acceptable for visual-only math, but this lane touches lockstep rollback evidence. Default alias assumptions would reduce vectorization confidence.
Scalability potential: Low tier skips more targets; High/Ultra can spend the preserved SIMD budget on deeper hot-data coverage.
Hardware Impact: Protects NEON/AVX vectorization eligibility. Exact microseconds saved require Unity profiler/Burst disassembly.

## Decision 011 - Local Heap State Removal
Problem: `_lockedBuffers = new BufferID[]` was a managed private field in the sentinel driver.
Solution: Replace it with `FixedList128Bytes<BufferID>`, enough for the fixed target budget without heap allocation.
Rejected Alternatives: Keeping the array as "cold enough" weakens the Vault-law audit and creates a false positive for managed state.
Scalability potential: Capacity is fixed to the SHINOBU target budget; target breadth still scales through registered Vault handles and quality gates.
Hardware Impact: Removes one cold heap object and keeps lock bookkeeping cache-local; hot-path us change is negligible.

## Decision 012 - Unity Compile Wall Evidence
Problem: Unity batch compile terminates before import/compile while another Unity process owns the project lock.
Solution: Preserve logs and mark Unity verification blocked by the existing editor process/`Temp/UnityLockfile`; do not kill developer tooling.
Rejected Alternatives: Killing Unity would risk the developer's open scene/inspector state. Claiming compile success from `dotnet build` would be false because generated csproj excludes new SHINOBU files.
Scalability potential: None; verification only.
Hardware Impact: 0 runtime us. The code path remains unproven by Unity until the lock is released.

## Decision 013 - Signal Payload Assembly Relocation
Problem: `MemoryDesyncSignal` and `HashDeltaUpdateSignal` originally lived under Core runtime files, forcing peer domains to reference Core if they wanted to publish legal hash deltas.
Solution: Move SHINOBU signal payloads to the `Hecton8.Core.Contracts` assembly while leaving the SignalBus storage/driver in Core runtime. The payloads stay unmanaged and 64-byte padded.
Rejected Alternatives: Keeping the signals in Core runtime would create exactly the compile-wall coupling the mandate forbids. Moving the full SignalBus into Contracts would be a broad architecture change outside SHINOBU scope.
Scalability potential: Low/Middle/High/Ultra all use the same typed signal payloads; lane capacities remain configurable by the existing SignalBus.
Hardware Impact: 0 us in normal frames; this is assembly topology, not simulation work.

## Decision 014 - Job-Side Desync Emission
Problem: The initial kernel only wrote result flags and let runtime publish desyncs after job completion. That was defensible for correction, but it was not literal enough for Task 06.
Solution: Pass `NativeQueue<MemoryDesyncSignal>.ParallelWriter` into `MemorySentinelValidationJob` and enqueue mismatch/invalid-pointer signals directly from the Burst kernel. Runtime keeps secondary desync emission only for rollback/fatal context.
Rejected Alternatives: Calling `SignalBus<T>.Push` from the Core.Memory job would introduce a Core runtime dependency and is not a Burst-safe static call boundary. Duplicating every signal in runtime was rejected as telemetry noise.
Scalability potential: Low tier normally emits zero signals; High/Ultra can validate more targets without changing the fault-lane contract.
Hardware Impact: Normal path has 0 queue writes. Tamper path pays one NativeQueue enqueue per mismatch; exact us requires profiler.

## Decision 015 - Continuous Quality Curve
Problem: Quality cadence used a linear `lerp` only, which lacked the mandated polynomial breathing curve and explicit `math.step` collapse.
Solution: Use smoothstep polynomial `q*q*(3-2q)` before `math.lerp` for validation cadence, and `math.step` for per-target `MinQualityWeight` gating inside the Burst kernel.
Rejected Alternatives: Binary `if low-end` switches and linear-only cadence scaling create either abrupt protection changes or noncompliant quality behavior.
Scalability potential: Low = critical mock/inventory/player spans with sparse cadence; Middle = economy plus AUP; High = broader VaultAup64; Ultra = tight cadence and more hot spans.
Hardware Impact: Low-end i3/MX350 reduces bytes hashed by skipping high-min-quality targets and increasing cadence frames; no exact microseconds claimed without profiler capture.

## Decision 016 - Structural Tamper Signal Gate
Problem: The job-side desync lane reported invalid pointers and content hash mismatches, but pointer/fingerprint mismatch flags could remain silent when the pointed bytes still hashed to the expected value.
Solution: Route hash mismatch, pointer mismatch, and pointer-fingerprint mismatch through one post-hash `MemoryDesyncSignal` gate after `ExpectedHash` and `StoredHash` are finalized.
Rejected Alternatives: Hash-only emission misses target-record tampering. Runtime-only emission would defer the evidence outside the Burst validation kernel and duplicate the Loop 7 fault-lane correction.
Scalability potential: Low/Middle/High/Ultra still pay zero NativeQueue writes in the clean path. Higher tiers can validate more target records without changing the signal contract.
Hardware Impact: Normal frame cost is unchanged. Tamper frame cost is one NativeQueue enqueue for structural mismatch; exact microseconds require Unity profiler capture.

## Decision 017 - Prompt-Exact Status Renumbering
Problem: The implementation status split Task 06 signal emission into a separate checklist item and shifted Tasks 07-17 away from the XML assignment, making the audit harder to verify mechanically.
Solution: Rewrite the checklist items 06-17 to match the original SHINOBU_73 prompt numbering exactly while preserving the same DOD evidence.
Rejected Alternatives: Leaving the shifted checklist would be functionally harmless for code but noncompliant with the batch protocol's task reconciliation requirement.
Scalability potential: None; documentation integrity only.
Hardware Impact: 0 runtime us.

## Decision 018 - Mock Signature Literal Repair
Problem: Manual Roslyn probe caught `0x494E565F484F545F31UL`, a 9-byte hexadecimal literal that cannot fit in `ulong`.
Solution: Replace it with the 8-byte mock sentinel word `0x494E565F484F5431UL`.
Rejected Alternatives: Splitting the marker into another word would perturb the existing 64-byte mock span layout. Keeping the literal would fail compile.
Scalability potential: None; mock seed identity only.
Hardware Impact: 0 runtime us. Removes a hard C# compile blocker before Unity import.

## Decision 019 - Probe Verification While Unity Is Locked
Problem: The open Unity editor owns `Temp/UnityLockfile`, so batch import/compile cannot prove the new files.
Solution: Use Unity's bundled Mono/Roslyn compiler with current Unity references and probe DLLs under `Temp/` to compile SHINOBU contracts, memory jobs, runtime, and editor facade. This is not a replacement for Unity import, but it catches syntax/API errors in the edited files.
Rejected Alternatives: Killing the active editor risks developer state. Reporting dotnet success alone is false because the generated csproj excludes the new SHINOBU files.
Scalability potential: None; verification only.
Hardware Impact: 0 runtime us.

## Decision 020 - Hung Dotnet Cleanup
Problem: Final `dotnet build Hecton8.Core.csproj` retries hung after the Roslyn probes and left `dotnet` processes consuming CPU without compiler output.
Solution: Kill only the `dotnet` processes spawned by the timed-out verification attempts and record the timeout separately from the successful Roslyn probes.
Rejected Alternatives: Leaving orphaned build processes would waste developer hardware. Treating the timeout as a compile error would be inaccurate because no compiler diagnostics were emitted.
Scalability potential: None; verification hygiene only.
Hardware Impact: CPU load removed after timeout cleanup.

## Decision 021 - SHINOBU_73 Identity Leak Repair
Problem: The runtime/editor files still contained SHINOBU_78 identity strings and `0x53483738u`, which would misroute telemetry identity, host naming, and fatal-message forensics for Agent 73.
Solution: Replace every SHINOBU_78 marker in the SHINOBU_73 code path with SHINOBU_73 and set `SystemHash` to `0x53483733u`. Keep the dump file as the task-mandated `Dump_INTEGRITY_SURGEON.bin`.
Rejected Alternatives: Treating labels as cosmetic was rejected. In this system labels are forensic routing and black-box autopsy metadata.
Scalability potential: Low/Middle/High/Ultra all use the same sentinel identity; quality changes cadence and scope, not audit ownership.
Hardware Impact: 0 runtime us. The fix prevents wrong-agent forensic attribution without changing the hot path.

## Decision 022 - Build Probe Throttle Under Active Compiler Load
Problem: After the identity patch, the machine already had active `dotnet`/`csc` processes and reported 100% CPU load.
Solution: Do not launch another build or Roslyn probe in this pass. Record static audits and leave compile state as PENDING UNITY VERIFICATION until the compiler lane is free.
Rejected Alternatives: Starting another compile would violate AGENTS.md hardware-protection guidance and could interfere with other agents. Claiming prior probes as post-patch compile proof would also be imprecise because this patch happened after those probes.
Scalability potential: None; verification discipline only.
Hardware Impact: Avoided adding another CPU-saturating compiler process on developer hardware.

## Decision 023 - H-PHI Handle List Correction
Problem: The previous self-audit listed Vault handles 70873..70881 but omitted the later `ModQuarantineBuffer` handle 70882.
Solution: Append a LOG self-audit update that lists all active SHINOBU handles: states, targets, results, rollback bytes, mock inventory, telemetry, runtime state, AUP snapshot, CSV scratch, and mod quarantine.
Rejected Alternatives: Leaving the old list would make the Vault-sovereignty audit incomplete even though the code requests the buffer correctly.
Scalability potential: Mod quarantine remains a continuous-scope target gated by `ModdedGameMask` and `GlobalQualityWeight`; higher tiers can protect more non-mod spans without widening private ownership.
Hardware Impact: 0 runtime us. Documentation correction only.

## Decision 024 - Delayed Readback Against Concurrent Overwrite
Problem: A later static audit showed the runtime/editor files had reverted to stale SHINOBU_78 identity after the Loop 9 patch, likely from concurrent file activity in the shared workspace.
Solution: Re-apply the identity repair and perform a delayed 10-second code-only readback before reporting. Record the event instead of silently overwriting history.
Rejected Alternatives: Marking the task as clean from the first patch was rejected because readback contradicted it. Making the files read-only was rejected because it would sabotage concurrent agent/user edits.
Scalability potential: None; this is workspace integrity discipline.
Hardware Impact: 0 runtime us. Prevents wrong dump/telemetry identity from shipping.

## Decision 025 - Desync Signal Flag Isolation
Problem: Runtime `PublishDesync` copied `MemorySentinelResultDTO.Flags` directly into `MemoryDesyncSignal.Flags`. The two flag domains are not bit-compatible; `ResultFlagMismatch` uses bit 1, which is `MemoryDesyncSignal.FlagFatal`. A healed rollback could therefore be routed as fatal evidence.
Solution: Clear the public signal flags and map only signal-owned semantics: rollback applied, fatal, teleport, critical, and pointer mismatch. Pointer mismatch now also covers pointer fingerprint mismatch and invalid pointer result bits.
Rejected Alternatives: Keeping raw result-bit passthrough was rejected because it corrupts the typed SignalBus contract. Adding a second result-flags field to the signal was rejected because it would require a wider contract change and is unnecessary for the current Watchdog lane.
Scalability potential: Low/Middle/High/Ultra all keep the same clean signal contract; higher validation scope does not amplify false fatal routing.
Hardware Impact: 0 normal-frame us. Fault path performs the same number of branches and removes incorrect Watchdog escalation risk.

## Decision 026 - Probe Boundary Classification
Problem: Once CPU load dropped, a fresh runtime/editor Roslyn probe still failed because it linked current SHINOBU source against stale pre-import `Library/ScriptAssemblies/Hecton8.Core.dll`. That old DLL does not expose the current `HomeostasisBrain.GlobalQualityWeight` source property and its `SignalBus<T>` generic constraint is bound to the pre-import `ISignal` assembly identity.
Solution: Record contracts/memory probe success as valid for current SHINOBU DTOs/jobs, and classify runtime/editor probe failure as Unity-import boundary evidence, not a SHINOBU code compile diagnostic. The authoritative next step remains Unity import/compile after the active editor releases the project.
Rejected Alternatives: Building local stubs for `SignalBus`, `HomeostasisBrain`, or `GlobalRegistry` was rejected because it would create fake compile evidence. Killing the open Unity editor was rejected because it risks developer state.
Scalability potential: None; verification boundary only.
Hardware Impact: 0 runtime us. Probe retry was limited to source validation and did not spawn extra compilers while CPU was saturated.
