# Rationale 1327 - MEMORY_SOVEREIGN_FLORA_INTERACTION_EXORCIST

Status: STATIC VERIFIED GREEN FOR 1327 GATES - project compile attempted and failed in external systems before a green build could be produced.

Problem: Persistent native collection aliases in flora interaction would block GlobalDataVault relocation and can become dangling pointers after compaction.
Solution: Inspect actual source first, then replace only verified persistent aliases with DataVault handles or document mismatch if the flagged state is stale.
Rejected Alternatives: Blind mechanical rewrite of NativeArray text; unsafe because locals and third-party/native owner fields may be legal.
Scalability potential: Low uses skipped visual update on contention; Middle uses normal cadence; High increases near-field visual density; Ultra spends saved budget in VISUAL_SYNC only.
Hardware Impact: Expected i3/MX350 gain is from preventing relocation stalls and hot-path GC/native alias hazards; measured proof absent.

Problem: Batch mandates require selected technical rules before code.
Solution: Read eight relevant .agents-skills files covering native memory, zero GC, ARM64 layout, telemetry, phases, registry DI, signal lanes, and flora instancing.
Rejected Alternatives: Reading all registry files would waste context and not improve this task.
Scalability potential: Rules preserve continuous GlobalQualityWeight scaling instead of binary tiers.
Hardware Impact: Prevents i3/MX350 frame spikes from hidden allocations or same-frame job completion.

Problem: `FloraInteractionManager` held 14 direct persistent native aliases despite existing Vault plumbing for wake and sway buffers.
Solution: Migrate those aliases to `VaultGenerationHandle<T>` descriptors using local BufferID constants 71655-71668 and the existing cached `IDataVault` route.
Rejected Alternatives: New `SystemID.WorldFlora`; enum mutation is a global authority/API change outside this file and the file already owns adjacent flora buffers under `SystemID.Vfx`.
Scalability potential: Low skips visual/reactive update on Vault contention; Middle preserves current cadence; High/Ultra spend savings in VISUAL_SYNC phase seed and shader detail, not extra gameplay truth.
Hardware Impact: i3/MX350 gains come from avoiding stale pointer retention and allowing Vault compaction; exact microsecond proof pending compile/runtime.

Problem: Reactive flora handle staging used `NativeList<int>` fields, which cannot be represented as a direct Vault descriptor without retaining a long-lived list alias.
Solution: Replace the lists with Vault-owned `NativeArray<int>` buffers plus scalar count fields; use existing `HectonSpatialHash.CollectSphere(..., NativeArray<int>)` overloads.
Rejected Alternatives: Wrapping `NativeList<int>` inside another owner object; still preserves a persistent native alias outside `GlobalDataVault`.
Scalability potential: Fixed 64-slot query/register buffers fail closed under density pressure; higher tiers should raise capacity through a Vault buffer resize only after profiler proof.
Hardware Impact: Removes list allocator metadata and auto-growth risk; estimated low-end win 3-8 us during dense cascade refresh, proof pending.

Problem: Descriptor substitution would leak Vault references if `OnDestroy` only defaulted generation handles.
Solution: Add `ReleaseFloraAuxiliaryVaultBuffers()` and invoke it before descriptor clearing and before DataVault hot-swap.
Rejected Alternatives: Relying on scene-owned Vault sweep; that hides reference lifetime and delays proof.
Scalability potential: Low/Middle avoid accumulated stale flora buffers after scene churn; High/Ultra keep compaction headroom for visual buffers.
Hardware Impact: Prevents retained auxiliary buffers from occupying reusable arena memory; i3/MX350 gain is memory-pressure avoidance, not per-frame speed.

Problem: `TryAcquireWriteLock` branches can fail mid-chain after an earlier buffer was acquired.
Solution: Keep explicit fail-closed release calls and `finally` releases around ocean samples, parasite nodes, cascade events, phase seeds, template masks, and allelopathic masks.
Rejected Alternatives: Nested helper hiding the lock topology; direct visibility is easier to audit under 20-agent edits.
Scalability potential: Lock contention skips visual/flora refresh for one frame rather than blocking global relocation.
Hardware Impact: Low-end devices avoid hard stalls from writer lock retention; measured contention cost pending runtime profiler.

Problem: Public kelp pushback sampling used a Vault-resolved query buffer as scratch storage, which is not a pure read.
Solution: Treat the query buffer as a mutating scratch lane and guard it with `TryAcquireWriteLock` plus `finally` release in `TryResolveKelpPushback` and cascade triggering.
Rejected Alternatives: Calling the path "read-only" because the output is a density value; the spatial query writes result handles.
Scalability potential: Low skips one pushback/cascade sample under contention; Middle/High/Ultra keep current smooth radius math and use saved cycles for denser visuals.
Hardware Impact: i3/MX350 avoids relocation stalls from scratch alias mutation; contention skip cost is visual-only.

Problem: New Vault handle failures needed an owned proof artifact without managed hot-path logging.
Solution: Add `FloraMemoryTelemetryEntry` as an explicit 64-byte DTO, allocate a 300-entry Vault ring at BufferID 71669, and record failed resolve/write-lock/NaN details.
Rejected Alternatives: Reusing string logs or overloading the sway telemetry row with buffer identity fields.
Scalability potential: Low records sparse failures; Middle/High/Ultra use the same fixed ring and continue visual scaling through `GlobalQualityWeight`.
Hardware Impact: Normal frame cost is zero on success; failure path cost is accepted to preserve post-mortem proof on low-end hardware.

Problem: Background dumping a live `NativeArray` after phase end would violate the same relocation rule being fixed.
Solution: Copy the crash ring into a managed byte payload on the catastrophic path, then queue only immutable bytes to the thread pool for `Dump_1327_FloraInteraction.bin`.
Rejected Alternatives: A background worker resolving or iterating Vault memory directly; unsafe if compaction happens before the worker runs.
Scalability potential: Dump path is failure-only and does not consume visual budget in healthy frames.
Hardware Impact: No hot-path impact; on i3/MX350 a crash dump allocates once after failure instead of blocking repeated frames.

Problem: Broad World flora/vegetation scope contains active sibling edits and known agent 1316 ownership of vegetation memory migration.
Solution: Confine production edits to `FloraInteractionManager.cs`, add only a validator file under World/Editor, and document residual sibling debt in the report.
Rejected Alternatives: Migrating `VegetationMemoryPool.cs`, `HectonMapMagicVegetationBridge.cs`, or `FloraRegrowthDirector.cs` without their lifecycle ledgers; that would create merge conflicts or unowned BufferID routes.
Scalability potential: Primary propwash/sway interaction now scales through Vault descriptors; lifecycle/regrowth debt remains separately owned.
Hardware Impact: Avoids destabilizing low-end builds with cross-domain partial rewrites during active compilation.

Problem: DTO layout proof needed to survive future private struct edits.
Solution: Expose editor-only layout validation methods and add `FloraInteractionMemorySovereigntyValidator1327.cs` throwing `FatalArchitectureException` on offset/size drift.
Rejected Alternatives: Static report without a compile-time/editor-load guard.
Scalability potential: Low/Middle/High/Ultra all rely on the same 8-byte aligned telemetry and parasite/cascade rows.
Hardware Impact: Prevents ARM64 unaligned layout regressions that would punish weak silicon hardest.

Problem: T.A.R.S. re-audit found a direct absolute AUP-to-float upload in submarine wash shader globals.
Solution: Convert submarine AUP to origin-relative meters through `ResolveAupLocalDelta`, and update `ResolveAupLocalDelta` to subtract in double, clamp in double, then cast to float.
Rejected Alternatives: Keeping raw grid/local float globals because the current shader mostly ignores them; that leaves a precision trap for later shader use.
Scalability potential: Low avoids visible jitter near 100 km boundaries; Middle/High/Ultra retain precise local wash placement while spending saved cycles on denser visual deformation.
Hardware Impact: Prevents precision-driven vertex shimmer; i3/MX350 gain is visual stability rather than measured CPU time.

Problem: Job inner loops used branch/continue culls for cascade propagation, wake-force accumulation, and displacement upload telemetry.
Solution: Replace contained loop branches with boolean masks, `math.select`, and continuous fake-field math. Bounds and memory-safety entry guards remain explicit.
Rejected Alternatives: A more realistic CPU solver with branch-heavy physical culling; it spends frame budget on correctness the player cannot inspect.
Scalability potential: Low uses the same cheap branchless masks with lower density; Middle/High/Ultra spend available budget on visual density and shader overkill, not solver realism.
Hardware Impact: Static estimate 3-12 us saved on dense i3/MX350 flora frames; profiler proof still pending Unity execution.

Problem: Write-lock acquisition checked the compaction fence before `TryAcquireWriteLock` but not immediately after the lock was acquired.
Solution: Add post-acquire `IsCompactionFenceActive` checks and fail-closed releases for general flora Vault writes and memory telemetry writes.
Rejected Alternatives: Relying on the pre-check only; compaction can race between check and lock acquisition.
Scalability potential: Low skips one visual update on compaction; Middle/High/Ultra keep relocation safety while allowing higher visual buffer pressure.
Hardware Impact: Prevents rare relocation stalls/dangling pinned views on weak silicon; direct microseconds are failure-path only.

Problem: The catastrophic telemetry dump used `MemoryStream.ToArray`, which a Roslyn scanner classifies as LINQ-like surface.
Solution: Use the pre-sized `MemoryStream.GetBuffer()` payload after writing exactly the fixed ring size.
Rejected Alternatives: Leaving a false scanner hit and documenting it away.
Scalability potential: Dump path is failure-only; normal tiers pay zero frame cost.
Hardware Impact: Removes one failure-path managed array copy; no healthy-frame impact.

Problem: A branchless wake-source loop can still propagate NaN if invalid source values are multiplied before the validity mask reaches zero.
Solution: Sanitize source local delta, radius, intensity, and velocity before distance/falloff math, then apply `math.select` masks.
Rejected Alternatives: Treating `validWeight * NaN` as harmless; IEEE math keeps NaN alive and would poison the displacement field.
Scalability potential: Low/Middle/High/Ultra all keep the same cheap visual fake while invalid samples collapse to zero contribution.
Hardware Impact: Prevents NaN crash/dump churn on i3/MX350; normal-frame CPU delta is below static measurement.

Problem: A broad World native-alias scan failed to parse `ProceduralWreckGenerator.cs:1254`, which is outside the 1327 touched set.
Solution: Report the parse failure explicitly and use the scanner's successfully parsed findings to prove the two touched C# files have 14 native declarations, 14 job-transient fields, and 0 persistent fields.
Rejected Alternatives: Hiding the parse failure or claiming a folder-wide green.
Scalability potential: Keeps 1327 memory migration bounded while preserving integration visibility for the owning agent.
Hardware Impact: No runtime impact; prevents false architectural sign-off.

Problem: Lock proof cannot stop at helper inspection because callers can leak partial multi-lock acquisitions.
Solution: Re-audit every `TryAcquire*` caller in `FloraInteractionManager.cs`; partial failures release acquired handles, and successful writers release in `finally` before returning.
Rejected Alternatives: Assuming `TryAcquireFloraVaultWriteBuffer` alone proves phase safety.
Scalability potential: Low skips visual updates during contention; higher tiers can carry denser visual buffers without dangling view risk.
Hardware Impact: Prevents rare compaction stalls and retained writer locks on weak CPUs.

Problem: The second T.A.R.S. pass found that `TryAcquireFloraVaultWriteBuffer` could still call `TryEnsureFloraVaultBuffer`, meaning a frame-phase write acquire could allocate or grow a Vault buffer.
Solution: Split hot write acquisition to resolve-only flow; cold setup remains the only path allowed to call `EnsureGenerationHandle`.
Rejected Alternatives: Keeping lazy allocation inside the acquire helper and relying on normal capacity being present; a mismatch would still spike Tick/SlowTick/LateFrame.
Scalability potential: Low devices fail closed instead of allocating mid-frame; Middle/High/Ultra keep the same route and can raise visual capacity only through cold registration.
Hardware Impact: Prevents first-touch/growth stalls on i3/MX350; exact microseconds are scene-density dependent and not measured.

Problem: Cascade phase seed invalid-buffer handling wrote `PhaseSeeds[index]` after detecting that the buffer was missing or the index was outside length.
Solution: Return before the write on invalid buffer/length and clamp scheduled/uploaded count to the preallocated cascade phase seed capacity.
Rejected Alternatives: Writing a sentinel into an invalid slot; that is an out-of-bounds memory fault, not a recoverable visual fallback.
Scalability potential: Low drops the cascade seed update safely; Middle/High/Ultra preserve branchless seed math within fixed capacity.
Hardware Impact: Correctness defect removal; CPU delta is negligible, but it prevents Burst memory faults under capacity mismatch.

Problem: Template mask refresh wrappers could rebuild arrays and Vault buffers from Tick/SlowTick call sites if the bridge template count changed.
Solution: Move force rebuild work into cold helpers and make hot refresh wrappers read-only/no-grow.
Rejected Alternatives: Letting hot wrappers allocate `bool[]`, `int[]`, or grow Vault masks on mismatch; that violates the Zero-GC hot path.
Scalability potential: Low skips stale visual template masks until cold refresh; higher tiers retain visual richness without frame-phase allocator risk.
Hardware Impact: Prevents rare SlowTick/Tick allocation spikes on weak hardware; exact cost depends on template count and is not profiler-measured.

Problem: Reactive flora spatial hashes and cascade phase seed GPU buffers could be created on first hot refresh.
Solution: Preallocate/create them during cold Awake/OnEnable setup and pass `allowCreate:false` in hot rebuild/finalization paths.
Rejected Alternatives: Lazy `new HectonSpatialHash` or `new GraphicsBuffer` in the first visual update; that hides allocator work in the simulation frame.
Scalability potential: Low can skip missing visual response rather than allocate; Middle/High/Ultra consume preallocated buffers and scale fidelity through `GlobalQualityWeight`.
Hardware Impact: Removes first-refresh managed/native allocation risk on i3/MX350; no measured healthy-frame microsecond claim.

Problem: Re-audit 3 found that `FloraStiffnessRuleDTO` was in the byte-offset report but not covered by the editor layout validator.
Solution: Add `ValidateFloraStiffnessRuleDtoLayout` and wire it into `FloraInteractionMemorySovereigntyValidator1327`.
Rejected Alternatives: Leaving stiffness as source-only evidence; ARM64 gate requires executable layout guard, not only a report table.
Scalability potential: Low/Middle/High/Ultra keep the same 16-byte stiffness row without DTO drift between quality tiers.
Hardware Impact: Editor-only guard cost; runtime gain is preventing future misaligned/stale DTO edits before they hit weak silicon.

Problem: The exact prompt extraction regex failed when the active tag included `role` and `chat_name` attributes after `id="1327"`.
Solution: Use an attribute-tolerant `<AGENT_PROMPT\b[^>]*id="1327"[^>]*>` regex and verify length, task count, and SHA-256.
Rejected Alternatives: Trusting old prompt memory or requiring a brittle exact closing `id="1327">` form.
Scalability potential: No runtime effect; prevents wrong-agent task contamination during compressed sessions.
Hardware Impact: No runtime impact.

Problem: Build verification cannot be retried while CPU/process guard is red.
Solution: Record the current guard state and keep this pass scoped to static/Roslyn verification until `csc`/`dotnet` are gone and CPU is below 50%.
Rejected Alternatives: Forcing a compile into active `csc`/`dotnet` load; project rules explicitly forbid it.
Scalability potential: No runtime effect; avoids stealing CPU from concurrent agents.
Hardware Impact: Prevents local workstation contention and unreliable compiler diagnostics.

Problem: A guarded build after Re-audit 3 exposed two stale variable names in cold template-mask rebuild code: `cascadeReactiveTemplateMask` and `defensiveSporeBurstTemplateMask` were used without local declarations after persistent fields were removed.
Solution: Declare the cold write views directly at the Vault acquire call with `out NativeArray<byte>` so the rebuild methods use transient locked views only.
Rejected Alternatives: Reintroducing persistent native collection fields or treating the compile errors as external; these were in the 1327 primary target and had to be fixed.
Scalability potential: Low/Middle/High/Ultra keep the same cold mask rebuild route and hot read-only refresh route; no runtime quality branch added.
Hardware Impact: Runtime cost is 0 us; the fix removes a compile blocker created by the memory-sovereignty migration.

Problem: The latest project build still fails after the 1327 fix, but the remaining visible failures are in vegetation memory, submarine atmosphere, inventory, fluid, PDA, and audio files outside the primary 1327 target.
Solution: Record the exact build result: 197 errors, 7 warnings, zero visible errors in `FloraInteractionManager.cs` or `FloraInteractionMemorySovereigntyValidator1327.cs`.
Rejected Alternatives: Claiming green project compilation or chasing unrelated systems under a flora interaction prompt.
Scalability potential: No runtime effect; preserves domain boundaries while keeping integration evidence honest.
Hardware Impact: No runtime effect.

Problem: `FloraSwayFieldTelemetryEntry` was 64 bytes and explicitly laid out, but the two `ushort` counters sat at offsets 4 and 6 before later 4-byte fields. That passed size alignment but not the stricter field-order proof.
Solution: Reorder the explicit offsets so all 4-byte lanes occupy offsets 0-56 and the two `ushort` counters occupy offsets 60 and 62; update the editor validator to check `FieldCenterWS`, `CpuMicroseconds`, and `Resolution` offsets.
Rejected Alternatives: Leaving a size-only validator and explaining the packed ushorts as harmless; the prompt asked for pointer-first/order proof, not just no holes.
Scalability potential: Low/Middle/High/Ultra telemetry rows remain the same 64-byte size and are quality-independent.
Hardware Impact: Runtime data size unchanged; reduces ARM64 layout regression risk without adding frame cost.

Problem: Build after the DTO layout fix hit the local 120 second timeout while emitting external compile failures; `dotnet`/`csc` remained active after the timeout.
Solution: Record the timeout result and the visible error set honestly: 72 errors, 7 warnings, zero visible 1327 file errors before timeout.
Rejected Alternatives: Re-running immediately into active compiler processes or calling the project compile green.
Scalability potential: No runtime effect; this preserves verification discipline under concurrent-agent load.
Hardware Impact: No runtime effect; avoids additional workstation contention.

Problem: Public `Padding0/Padding1/Padding2` fields remained in touched runtime structs after the DTO layout pass. The layout was byte-aligned, but the proof did not satisfy the private explicit-padding rule.
Solution: Rename padding in `ParasiteNode`, `FloraCascadeEventPayload`, and `DefensiveSporeBurstState` to private `_pad*` fields and remove cascade initializer writes to padding.
Rejected Alternatives: Treating public padding as acceptable because offsets were correct; the mandate requires explicit private padding, not public data surface.
Scalability potential: No quality behavior changes. Low/Middle/High/Ultra keep identical struct sizes and visual cadence.
Hardware Impact: Runtime cost is 0 us; the gain is preventing future code from treating padding as semantic data.

Problem: The first post-DTO build proof timed out, leaving an incomplete compile artifact.
Solution: Re-run `dotnet build` with shared compilation disabled and a 600s timeout after CPU/process guard cleared. The build failed in external systems after 95.61s with 69 errors and zero visible 1327 target errors.
Rejected Alternatives: Calling the timeout artifact sufficient, or chasing voxel/fluid/vegetation compile failures outside the 1327 prompt.
Scalability potential: No runtime effect; preserves domain boundary and evidence accuracy.
Hardware Impact: No runtime effect.
