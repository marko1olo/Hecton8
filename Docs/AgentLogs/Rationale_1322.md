# Rationale 1322 - MEMORY_SOVEREIGN_FLUID_ENGINE_EXORCIST

## Initial Boundary

Problem: Assignment exists in `Docs/Tasks/CURRENT_BATCH.md` under `<AGENT_PROMPT id="1322">`; root `current_batch.md` is absent.
Solution: Use actual active batch file discovered by `rg --files`, then extract only the 1322 XML block with PowerShell regex.
Rejected Alternatives: Reading neighboring prompts as context; this violates strict parsing and contaminates domain decisions.
Scalability potential: No runtime effect. Prevents wrong-domain edits.
Hardware Impact: No frame impact.

Problem: No existing `Status_1322.md`, `Rationale_1322.md`, or `LOG_1322.md` present.
Solution: Create current-batch status and rationale files before coding.
Rejected Alternatives: Chat-only tracking; rejected by project protocol.
Scalability potential: No runtime effect.
Hardware Impact: No frame impact.

## Primary Memory Exorcism

Problem: `HectonFluidEngine.cs` held 39 persistent `NativeArray<T>` fields. These aliases survive across vault relocation windows and can become stale raw physical addresses.
Solution: Replace the 39 fields with `FluidVaultBuffer<T>`, a pointer-free descriptor wrapper around `VaultGenerationHandle<T>` plus `BufferID` and required length metadata. All storage is now requested from `GlobalDataVault`.
Rejected Alternatives: Keep the `NativeArray<T>` fields and register them with `NativeMemorySentinel`; sentinel registration does not make stale aliases relocation-safe.
Scalability potential: Low tier keeps the same continuous LOD math and avoids crash stalls; mid/high/ultra tiers keep larger vault buffers without private aliases.
Hardware Impact: Removes hard-crash class on i3/MX350 during defrag. Normal resolve overhead is handle-level; no managed GC.

Problem: Cold boot previously allocated persistent fluid arrays directly and then disposed/reallocated on capacity changes.
Solution: Route primary buoyancy, GPU upload, brine, advection, splashdown, maelstrom, and abyssal telemetry buffers through `GlobalDataVault.EnsureGenerationHandle<T>` using `SystemID.Fluid` and local `BufferID` range `1322000-1322040`.
Rejected Alternatives: Reuse old buffer IDs from submarine flooding or ocean wave systems; that would collapse ownership provenance and break one-owner routing.
Scalability potential: Low devices can keep smaller capacities; high/ultra devices can grow capacity through vault ownership without invalidating class fields.
Hardware Impact: Capacity growth remains cold. Hot path avoids managed allocation and stale pointer lifetime.

Problem: Read accessors could expose old aliases or force buffer growth if implemented through normal ensure paths.
Solution: `FluidVaultBuffer.AsReadOnly()` refreshes stale handles through `TryGetGenerationHandle` and resolves with `TryReadOnlyHandle` only. It does not allocate, publish, complete jobs, or poll the scene.
Rejected Alternatives: `GlobalRegistry.DataVault` polling in read properties; registry is cold identity/DI only.
Scalability potential: UI/debug readbacks degrade closed when a handle is stale instead of blocking simulation.
Hardware Impact: Read accessor cost is a bounded handle lookup; no GC.

Problem: Mutable writes needed a clear lock/release discipline after alias removal.
Solution: `FluidVaultBuffer<T>` write path uses `TryAcquireWriteLock` and `ReleaseWriteLock` in `finally`; lock contention records to the new fluid sovereignty ring when available.
Rejected Alternatives: Direct `TryResolveHandle` writes for all setters; faster but not relocation-fence explicit.
Scalability potential: Low tier gets safety over throughput; high/ultra can later replace setter-level locking with coarser dispatcher-phase write windows for visual overkill.
Hardware Impact: Setter-level lock is a conservative bridge. It is heavier than ideal but does not allocate; future work should batch locks in `GatherData`.

Problem: The new memory route needed a black-box record for stale handles, contention, and non-finite force/torque failures.
Solution: Add explicit 64-byte `FluidTelemetryEntry`, vault-owned 300-entry ring, cursor buffer, event writer, and cold binary dump `Docs/AgentLogs/Dump_1322_FluidEngine.bin`.
Rejected Alternatives: Managed `List<string>`/Unity log-only telemetry; useless for crash post-mortem and GC-hostile.
Scalability potential: Low/mid tiers record only faults/events; high/ultra can afford more detailed microsecond fields without DTO changes.
Hardware Impact: Fault/event writes only. Non-finite force/torque dump is cold I/O, not frame-normal.

Problem: Background-thread dump requested by batch conflicts with DataVault view lifetime because a background thread would hold a read alias after the current phase.
Solution: Write the binary dump synchronously in the cold catastrophic path from a read-only view, then release control. This keeps alias lifetime local and avoids background stale pointers.
Rejected Alternatives: Queue raw `NativeArray`/pointer to a background worker; that violates the relocation compatibility mandate.
Scalability potential: No impact in healthy runtime. Fault cost is accepted because the alternative corrupts post-mortem data.
Hardware Impact: On i3/MX350 the dump can hitch during a catastrophic event, but it avoids undefined memory access.

Problem: Proof required domain sweep without fighting other agents.
Solution: `git status --short` confirmed only this agent's target/new validator in fluid scope; scanner over fluid/buoyancy physics files found zero persistent native field violations outside `HectonFluidEngine.cs`.
Rejected Alternatives: Editing clean sibling files to look busy; rejected as conflict risk and bureaucracy.
Scalability potential: No runtime effect.
Hardware Impact: No frame impact.

Problem: Compile verification was required, but project policy forbids builds under high CPU or active `dotnet`/`csc`.
Solution: Waited until CPU samples were 19.09% and 15.74% with no compiler process, then ran `dotnet build Hecton8.Core.csproj --no-restore`.
Rejected Alternatives: Building during the earlier 85%+ CPU readings; forbidden by local protocol.
Scalability potential: No runtime effect.
Hardware Impact: No frame impact.

Problem: Build failed before proving fluid code because `SubmarineStructuralGrid.cs` references missing `DamageControlTelemetryEntry`.
Solution: Record compile as blocked by unrelated dependency and keep static proof artifacts current.
Rejected Alternatives: Editing submarine structural code outside assigned domain; no critical fluid justification and high cross-agent conflict risk.
Scalability potential: No runtime effect.
Hardware Impact: No frame impact.

Problem: Repeated 1322 directive arrived after completion; build retry would violate CPU/compiler gate.
Solution: Re-read status, rationale, and the 1322 XML block; do not launch a second build while CPU samples are 100%/100% and multiple `dotnet` processes are active.
Rejected Alternatives: Running another compiler under saturated CPU; forbidden by project protocol and would interfere with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Avoids additional contention on low-end silicon and shared workstation CPU.
