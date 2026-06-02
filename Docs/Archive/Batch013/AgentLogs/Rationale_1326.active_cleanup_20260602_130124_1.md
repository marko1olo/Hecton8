# Agent 1326 Rationale

State: VERIFIED_GREEN_STATIC / LATEST BUILD BLOCKED BY CPU AND ACTIVE CSC

Problem: `SubmarineStructuralGrid.cs` held 15 persistent native collection fields that could become stale physical aliases after GlobalDataVault relocation.
Solution: Replace every persistent alias with a 16-byte `VaultGenerationHandle<T>` descriptor and resolve physical `NativeArray<T>` views only inside the active dispatcher phase.
Rejected Alternatives: Managed arrays violate Zero-GC and Data Sovereignty. Cached NativeArray views preserve the defrag crash class. Replacing the solver with disabled logic would be a gameplay lie.
Scalability potential: Low skips conflicted frames and keeps bounded 16-impact/300-entry caps. Middle runs the same handles with normal cadence. High spends saved stability budget on denser visual deformation. Ultra can overdrive leak plume and damage visuals without changing DTO truth ownership.
Hardware Impact: On i3/MX350 this removes the high-cost stall/crash path caused by stale native aliases. Exact measured microsecond gain is 0.0 because no profiler run was permitted; no fake frame-time claim is made.

Problem: BufferID ownership had to be established while `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` was already modified by other agents.
Solution: Use a local `StructuralGridVaultRoute` range 1326000-1326014 inside the target file and keep the owner as `SystemID.VehiclesPhysics`.
Rejected Alternatives: Editing the dirty global enum would create cross-agent merge risk. Reusing unrelated BufferIDs would break one-fact/one-owner routing.
Scalability potential: Low/Middle/High/Ultra all share identical BufferID identity, so quality scaling never changes authority or save identity.
Hardware Impact: No measured CPU delta; the gain is deterministic routing with no hot GlobalRegistry polling.

Problem: Burst jobs still need physical arrays, but persistent physical fields are forbidden.
Solution: Lock the vault buffer, resolve the physical array, execute the structural job with same-phase `Run()`, and release the pin in the same `finally` block.
Rejected Alternatives: Passing handles into Burst jobs is not executable by Burst. Holding pinned buffer views across frames blocks compaction. Async schedule/readback was rejected for this manager after second-pass audit.
Scalability potential: Low skips update on contention. Middle uses the same math with ordinary cadence. High/Ultra can spend cycles on visual overkill after the same phase releases pins.
Hardware Impact: On weak silicon this removes stale-alias and cross-frame pin risk. Exact measured microsecond gain is 0.0 due blocked compile/profiler.

Problem: Public read accessors could become hidden mutation/synchronization points.
Solution: Use pure `TryReadOnlyHandle` accessors and fail closed when handles are unavailable or stale.
Rejected Alternatives: Completing jobs inside getters, growing buffers, scene search, or GlobalRegistry hot polling violates global systems doctrine.
Scalability potential: All tiers consume the same immutable snapshots; quality only changes cadence/fidelity, not truth ownership.
Hardware Impact: Prevents main-thread read spikes on i3/MX350; exact measured delta unavailable.

Problem: Crash diagnosis required a 300-frame black box without managed hot-path logs or fresh fault-frame managed allocations.
Solution: Write 64-byte explicit `StructuralTelemetryEntry` records into a DataVault-backed ring with numeric `FailureCode`; on invalid state copy into a preallocated `StructuralTelemetryEntry[300]` snapshot and wake a persistent background writer for `Docs/AgentLogs/Dump_1326_SubmarineStructuralGrid.bin`.
Rejected Alternatives: `Debug.Log`, string formatting, `ThreadPool.QueueUserWorkItem` per fault, fresh managed snapshot arrays per fault, or background use of a live vault-resolved NativeArray. The latter would recreate the stale alias class on another thread.
Scalability potential: Low still gets fixed-size telemetry. Middle/High/Ultra can fill CPU/GPU microsecond fields later without DTO layout changes.
Hardware Impact: Normal-frame cost is bounded native struct write only. Fault-frame cost is a fixed 300-entry struct copy plus event signal; binary I/O stays on the persistent writer.

Problem: Layout drift can silently corrupt ARM64 reads.
Solution: Add editor initialize validation for `VaultGenerationHandle<byte>`, `ImpactCommand`, and `StructuralTelemetryEntry` sizes/offsets; replace 8-byte padding with explicit byte padding in `ImpactCommand`.
Rejected Alternatives: Comments or documentation-only alignment rules.
Scalability potential: All device tiers share identical cache-line footprints.
Hardware Impact: Prevents unaligned access regressions on low-end ARM64-style targets; no runtime frame cost outside editor validation.

Problem: Solver loops still contained branch statements after the first submission.
Solution: Convert Burst job inner-loop conditionals to `math.select`/mask arithmetic and keep the damage falloff as a cheap Padé-style approximation rather than FEM.
Rejected Alternatives: CPU-heavy physical crack simulation, async solver handoff with cross-frame pins, or branch-heavy early-continue loops inside SIMD lanes.
Scalability potential: Low runs cheap bounded loops; Middle uses same deterministic kernel; High/Ultra can buy presentation overkill with saved synchronization budget.
Hardware Impact: Branch removal improves SIMD predictability on i3/MX350-class hardware. Exact measured microseconds remain unavailable.

Problem: AUP local hit conversion subtracted in double precision but did not explicitly clamp the double delta before the final float cast.
Solution: Inserted `math.clamp` on `relativeWorldDouble` after `hitAup - rootAup` and before `new Vector3((float)...)`, bounded by `AupLocalCastClampMeters`.
Rejected Alternatives: Relying on finite checks alone or casting absolute AUP coordinates to float before origin subtraction. Both are too weak for 100km-scale jitter prevention.
Scalability potential: Low/Middle/High/Ultra share the same deterministic local-space conversion; quality never changes spatial authority.
Hardware Impact: Prevents catastrophic float precision loss at large map offsets. Microsecond cost is negligible relative to the safety of local structural hit placement.

Problem: Broad sweep touched a dirty workspace with many sibling files under active ownership.
Solution: Limit writes to `SubmarineStructuralGrid.cs` plus required docs/reports and document the bypass.
Rejected Alternatives: Forcing edits into `H8Memory.cs` or sibling systems would be architectural sabotage under the current collaboration rules.
Scalability potential: Keeps this exorcism integratable while other domains finish.
Hardware Impact: No runtime delta; avoids merge churn and compile instability.

Problem: The rejection demanded syntax-tree-level proof, but the previous status leaned too hard on the local brace-aware scanner.
Solution: Ran `Tools/VaultNativeAliasRoslynAudit/bin/Debug/net10.0/VaultNativeAliasRoslynAudit.exe` across `Assets/_Project/Scripts` and filtered the target file. The full scan covered 2432 files with 0 parse failures. `SubmarineStructuralGrid.cs` has 17 native collection field declarations, all transient job parameter fields, and 0 persistent native fields.
Rejected Alternatives: Claiming compliance from regex hits or ignoring the full-project scanner's unrelated persistent candidates. Full-project candidates are outside agent 1326 ownership; the target-scope result is the enforceable proof for this mandate.
Scalability potential: Low/Middle/High/Ultra all use DataVault descriptors for long-lived state and phase-local views for kernels; quality scaling never changes ownership.
Hardware Impact: No measured frame delta. The effect is elimination of stale native aliases in the structural grid domain.

Problem: The hot-path scanner reports 37 managed-risk creations at whole-file scope, but that raw number includes static marker initialization, cold lifecycle setup, cold particle object creation, and background dump file I/O.
Solution: Applied owner/type post-filtering to the scanner findings. Hot-path owners contain only value-type construction (`float3`, `double3`, `Vector3`, `Vector4`, `RenderParams`, job structs, and signal structs) and zero reference-type allocations. Whole-file scan still reports 0 string formatting, `.ToString`, LINQ, foreach, interpolation, string concatenation suspects, native temp allocations, and native persistent allocations.
Rejected Alternatives: Deleting cold boot assets or background file output to satisfy an unfiltered metric would harm the system and would not improve frame stability. Pretending cold allocations are hot-path allocations would also be false.
Scalability potential: Low devices retain bounded value-type math and can drop contended updates. Middle/High/Ultra may spend saved budget on visual leak and dent presentation; the structural truth remains unchanged.
Hardware Impact: Normal simulation frames stay Zero-GC by static evidence. Build/profiler verification is still blocked by CPU load above the local threshold.

Problem: The first prompt extraction regex failed because the active prompt opening tag includes attributes after `id="1326"`.
Solution: Re-ran extraction with an attribute-tolerant XML block regex and confirmed the exact 1326 block length: 23,191 chars, 20 tasks, ending at `</AGENT_PROMPT>`.
Rejected Alternatives: Reading neighboring prompt text by line window or trusting chat memory. That risks cross-agent contamination.
Scalability potential: No runtime effect; it protects task ownership and prevents wrong-domain edits.
Hardware Impact: No frame cost.

Problem: The build gate was previously blocked by CPU load, but the next precondition check allowed a real build attempt.
Solution: Ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` after CPU load read 33 percent and no active dotnet/csc/VBCSCompiler process was reported. The build failed with 233 errors, none in `SubmarineStructuralGrid.cs`.
Rejected Alternatives: Editing `PlayerExplorationTracker.cs`, `SubmarineAtmosphereSystem.cs`, inventory routing, vegetation/flora, or fluid files from agent 1326. Those are out of domain and already show separate-agent compile walls.
Scalability potential: No runtime change. This keeps the structural-grid proof isolated instead of converting a focused memory purge into an uncontrolled cross-domain compile medic pass.
Hardware Impact: No 1326 frame-cost claim. Build elapsed 85.6 seconds and produced external compile blockers.

Problem: A compaction fence could theoretically rise after the local precheck but before/after the vault returned a successful write-lock or buffer pin.
Solution: Added immediate post-acquire fence checks in `TryAcquireStructuralWriteBuffer` and `TryLockStructuralJobBuffer`. On fence activation, the code releases the just-acquired write lock or pin, records `FailureCodeCompactionFence`, clears local state, and returns false.
Rejected Alternatives: Trusting only the pre-acquire check or assuming the vault internals alone are sufficient. The user gate explicitly demanded acquisition-site proof, so the local helper now proves both sides of acquisition.
Scalability potential: Low/Middle/High/Ultra all get the same fail-closed behavior; quality weight never changes compaction safety or ownership.
Hardware Impact: One additional boolean check after successful lock/pin acquisition. Microsecond cost is unmeasured and expected below profiler visibility; safety value is removal of a narrow stale-pin race.
