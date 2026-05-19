# Rationale_SHINOBU_130

Status: PENDING VERIFICATION

## Initial Boundary

Problem: PDA encyclopedia text path can allocate managed strings and freeze Canvas/TextMeshPro rebuilds when reading large lore payloads.
Solution: Implement owner-local PDA streaming over MMF/native bytes, decode UTF-8 into pooled char spans, push to TMP through `SetCharArray`, store unlock truth in a 128-byte unmanaged bitmask.
Rejected Alternatives: Runtime JSON/string loading and `TMP_Text.text` are rejected because they allocate and dirty large UI surfaces. Direct hard dependency on other agents' baker output is rejected; mock lore database must allow isolated verification.
Scalability potential: Low uses 1 char/frame typewriter and nested dynamic Canvas only; Middle increases char stride; High reveals larger chunks; Ultra spends saved cycles on richer PDA presentation without changing the data path.
Hardware Impact: Estimated gain on i3/MX350 is removal of multi-MB managed string spikes and Canvas rebuild spreading; exact microseconds are PENDING VERIFICATION until Unity profiler/GCMonitor evidence exists.

## Decision 01 - Existing MMF Store Reuse

Problem: A second PDA-specific MMF owner would duplicate file handles, checksums, Vault fallback buffers, and black-box telemetry already implemented in `BabelDictionaryStore`.
Solution: Use `BabelDictionaryStore.GetUtf8(uint)` as the authoritative MMF byte-span source and add a small Burst `ExtractLoreSpanJob` DTO lookup for mock/index validation.
Rejected Alternatives: A new `MemoryMappedFile` in the PDA UI was rejected because it creates a parallel lifetime owner and risks desync with Agent 103's baked `.h8bin` contract.
Scalability potential: Low/Middle use the same byte-span path with smaller decode budgets; High/Ultra spend the same source bytes on larger reveal chunks and richer UI.
Hardware Impact: Estimated low-end gain is avoiding a duplicate mapping and redundant file validation; exact microseconds pending profiler.

## Decision 02 - 128-Byte EncyclopediaStateDTO

Problem: Unlock state must be lockstep/rollback compatible and ARM64-aligned without CS1612 copy traps.
Solution: `EncyclopediaStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 128)]`, exposes four raw `ulong` mask fields for 256 entries, and stores discovery AUP/revision metadata in the same Vault DTO.
Rejected Alternatives: `List<string>`, properties, and 16-mask 1024-bit expansion were rejected after re-reading the batch block; the directive explicitly requires four `ulong` masks.
Scalability potential: Low/Middle/High/Ultra all snapshot the same 128-byte block; content density changes through metadata, not state shape.
Hardware Impact: 128-byte blind copy is rollback-friendly and cache-stable on low-end silicon; exact microseconds pending profiler.

## Decision 03 - Atomic Unlock Write

Problem: Scan signals can arrive from decoupled systems, and unlock truth must not be lost under contested writes.
Solution: Mask set uses CAS-backed `AtomicOr(ref ulong, ulong)` over the raw Vault field; the changed-bit result drives unlocked count/revision.
Rejected Alternatives: Plain `word |= bit` was rejected because the task explicitly requested atomic OR semantics.
Scalability potential: Same code path across quality tiers; visual reveal budget is independent of authoritative unlock state.
Hardware Impact: CAS cost is negligible per unlock; prevents expensive rollback/debug failures.

## Decision 04 - Mock Lore Database

Problem: PDA streamer must be testable without waiting on Narrative/Agent 103 final data.
Solution: Generate deterministic ASCII UTF-8 fallback entries into Vault buffer `(BufferID)70565` with `BabelIndexDTO` offsets in `(BufferID)70566`.
Rejected Alternatives: `string[]`, ScriptableObject text assets, JSON, and `File.ReadAllText` were rejected because they do not prove the byte-span path.
Scalability potential: Low hardware decodes small chunks from the same slab; Ultra reveals faster but keeps the data path identical.
Hardware Impact: Removes play-session managed text loading; exact microseconds pending Unity profiler.

## Decision 05 - Editor Allocation Quarantine

Problem: Writers need lock/unlock and raw byte x-ray tools, but editor display APIs allocate strings.
Solution: Runtime exports span-writing inspection methods; `PDAEncyclopediaTunerWindow` converts to strings only inside `#if UNITY_EDITOR`.
Rejected Alternatives: Runtime debug labels and always-on string formatting were rejected.
Scalability potential: No runtime cost on any device; high-tier debugging remains editor-only.
Hardware Impact: 0 runtime overhead.

## Verification Constraint

Problem: Compile verification is required but build launch is forbidden when CPU is under load.
Solution: Checked `csc.exe` and CPU before build attempts. `csc.exe` was absent, but Win32 CPU load stayed above the guard threshold (100, then 79, then 74), so no `dotnet build` was launched.
Rejected Alternatives: Ignoring the CPU guard or reporting a fake compile pass was rejected.
Scalability potential: Not applicable.
Hardware Impact: Avoided adding compiler load to an already saturated machine.

## Decision 06 - MMF First, Mock Second

Problem: The rough draft could return the Vault mock slab before checking the memory-mapped Babel dictionary, hiding real baked lore and weakening Task 06 proof.
Solution: `ResolveActiveUtf8` and editor raw hex now query `BabelDictionaryStore.GetUtf8()` first; only an empty span or the Babel `ERROR` sentinel falls back to the mock slab.
Rejected Alternatives: Mock-first routing was rejected because it can make a broken `.h8bin` look healthy. A second PDA-specific MMF owner was also rejected because the binary ledger identifies Babel as the current aligned MMF contract.
Scalability potential: Low/Middle/High/Ultra use the same byte source routing; quality only changes decode/reveal cadence.
Hardware Impact: Avoids duplicate file mapping and validates production data path on low-end silicon; exact microseconds pending profiler.

## Decision 07 - Vault Result Slot For ExtractLoreSpanJob

Problem: `ExtractLoreSpanJob` existed but mock lookup still used a normal component-side scan.
Solution: Added Vault buffer `(BufferID)70568` for `BabelLookupResultDTO[1]`; mock extraction now runs the Burst lookup job with `[NoAlias]` fields and reads the result slot.
Rejected Alternatives: A managed array result, a linear hot lookup, or a `NativeArray` allocated inside the component were rejected.
Scalability potential: Current mock capacity is small, but the job shape remains O(log n) and scales to larger baked index tables without changing PDA presentation.
Hardware Impact: Removes a misleading O(n) proof path; on i3/MX350 the gain is small for 8 mock rows but structurally correct for large lore tables.

## Decision 08 - AUP Distance Token Without Object References

Problem: Task 13 requires "Location Discovered" distance while the PDA must not store GameObject/Transform references to scanned objects.
Solution: Store discovery AUP in Vault metadata/state, read player pose through cached `IPlayerRuntimeContext`, subtract discovery AUP from player AUP, cast the localized delta to `float3`, then append meters into `Span<char>`.
Rejected Alternatives: Formatting absolute double coordinates, querying destroyed scan objects, or using `Vector3.Distance` in shifted runtime space were rejected.
Scalability potential: Low only resolves the token while streaming the page; higher tiers reveal faster but use identical deterministic math.
Hardware Impact: One token resolution is below noise; it prevents 100 km jitter and object lookup failures on cheap hardware.

## Decision 09 - Cold Boot Uninitialized Vault + Burst Run

Problem: Task 15 explicitly requires avoiding OS zero-fill for the `EncyclopediaStateDTO` buffer, but arbitrary `Schedule().Complete()` violates the dispatcher dependency rule.
Solution: Request state buffers with `NativeArrayOptions.UninitializedMemory` and clear the two 128-byte state rows with a Burst `IJob.Run()` during cold bootstrap only.
Rejected Alternatives: `NativeArrayOptions.ClearMemory` for the state rows was rejected after re-reading Task 15. `Schedule().Complete()` was rejected because it creates a fake async job and blocks immediately.
Scalability potential: Boot cost is fixed across tiers; saved CPU goes to PDA presentation rather than initialization.
Hardware Impact: Low-end gain is cold-start only, estimated microseconds not frame-time; exact measurement pending Unity profiler.

## Decision 10 - Dual Blackbox Dump Path

Problem: The global agent protocol requires `Dump_SHINOBU_130.bin`, while the SHINOBU_130 XML explicitly requires `Dump_PDA_STREAMER.bin`.
Solution: Fault dumping writes the same fixed 300-entry telemetry ring to both paths.
Rejected Alternatives: Picking one filename was rejected because it would break either integrator automation or task-specific QA lookup.
Scalability potential: No runtime cost unless fault/NaN/invalid UTF-8 occurs.
Hardware Impact: Fault-only disk IO; zero normal-frame impact.

## Verification Constraint Update

Problem: A guarded compile run is now allowed, but the project fails before this domain can prove compilation.
Solution: Ran `dotnet build .\Hecton8.slnx --no-restore -v:minimal` only after CPU=5 and `csc.exe` absent. Build failed on missing external source `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` referenced by `Hecton8.Core.csproj`.
Rejected Alternatives: Touching World project files from PDA domain, deleting the stale compile include, or claiming a pass were rejected.
Scalability potential: Not applicable.
Hardware Impact: One guarded build attempt; no further compile loops until the external missing source is resolved.

## Decision 11 - Cached Player Context For Distance Tokens

Problem: The distance token path originally risked hiding a live `GlobalRegistry.Player` read inside UI formatting work.
Solution: Resolve `IPlayerRuntimeContext` during `OnEnable`/`Start` and refresh it through `IGlobalRegistryHotSwapListener`; token formatting now reads the cached interface and immediately localizes discovery-player AUP delta to `float3`.
Rejected Alternatives: Polling `GlobalRegistry.Player` during every decode/token pass was rejected because registry convenience properties are cold dependency routes, not hot query buses.
Scalability potential: Low/Middle/High/Ultra all use identical deterministic AUP math; quality only changes how quickly the decoded page is revealed.
Hardware Impact: Removes one avoidable static registry read from token formatting on i3/MX350; exact microseconds pending profiler.

## Decision 12 - Vault Typewriter DTO And Source Pointer Cache

Problem: A scalar-only typewriter reveal proved the visual fake but did not satisfy the literal `TypewriterTextJob` task or eliminate repeated source lookup during long reveals.
Solution: Added `PdaTypewriterStateDTO` as a 64-byte Vault row `(BufferID)70569`, driven by Burst `TypewriterTextJob`, and cached the active unmanaged UTF-8 pointer/length/source flags after the first MMF or Vault mock lookup per entry.
Rejected Alternatives: Re-querying `BabelDictionaryStore.GetUtf8()` every late frame was rejected because it adds O(log n) lookup/telemetry work while the active entry is unchanged. Storing managed strings or component-owned arrays was rejected.
Scalability potential: Low: slow typewriter cadence limits TMP rebuilds. Middle: smooth larger increments. High: rapid chunks. Ultra: near-instant reveal while the same cached source feeds richer PDA presentation.
Hardware Impact: Avoids per-frame dictionary lookup during page reveal and keeps reveal state in Vault, not component-owned memory. Exact low-end microseconds pending profiler.

## Decision 13 - Source-Aware Blackbox Flags

Problem: The black-box ring could show bytes/chars/ticks but could not prove whether a fault happened on real MMF/Babel bytes or the Vault mock fallback.
Solution: Runtime state flags encode source bits at 8-9 (`1=MMF/Babel`, `2=Vault mock`); telemetry packs stream state, source bits, and canvas-split proof, and hashes now mix source bytes plus flags.
Rejected Alternatives: Leaving source implicit was rejected because a dump that cannot distinguish production bytes from fallback bytes is not forensic evidence.
Scalability potential: Same flags across all quality weights; high-tier visual behavior never changes authoritative source semantics.
Hardware Impact: Normal-frame cost is a few bit operations in the already-written telemetry row; accepted because it prevents expensive post-crash ambiguity.

## Decision 14 - Owner-Local AUP Copy And Full Layout Audit

Problem: The PDA runtime still named `Hecton8.World.AbsoluteUniversePosition` directly while the compile-wall mandate requires sibling-domain data to enter through contracts and be reduced to owner-local facts. The previous editor validation also proved only the 128-byte unlock DTO, leaving runtime/typewriter/telemetry rows under-documented.
Solution: Copy contract signal/player AUP fields immediately into `PdaAup48`, a 48-byte primitive transfer row owned by the PDA runtime. Distance token math now uses `HectonPhysicsContract.AupSectorSizeMetersInt` plus local clamp rails instead of calling World AUP helpers. Added `ValidatePdaStreamerLayouts` to verify state/runtime/meta/telemetry/typewriter/AUP row sizes and critical offsets.
Rejected Alternatives: Keeping `using Hecton8.World` was rejected because UI presentation does not need World behavior, only contract-provided coordinates. Duplicating the full World AUP type was rejected; the PDA only needs six primitive fields and explicit padding.
Scalability potential: Low/Middle/High/Ultra all use the same primitive AUP copy. Higher tiers spend saved UI time on faster reveal, not on richer coordinate math.
Hardware Impact: No claimed frame-time win. The gain is compile-wall isolation and safer ARM64 proof. Token math remains sub-us and deterministic on i3/MX350-class silicon.
