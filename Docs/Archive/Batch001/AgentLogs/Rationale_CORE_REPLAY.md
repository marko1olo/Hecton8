# CORE_REPLAY Rationale

Status: PENDING VERIFICATION.

Problem: The batch requires a deterministic replay black box, but a direct implementation that wires into every system would create cross-domain dependencies and collide with concurrent agents.
Solution: Build a core replay recorder around explicit registration APIs, unmanaged snapshot sources, fixed-size replay records, and debug/editor surfaces. Use `UnsafeUtility.MemCpy` for byte snapshots while keeping live system ownership outside CORE_REPLAY.
Rejected Alternatives: Direct references to Fauna, Physics, Logistics, Atmosphere, and renderer concrete classes were rejected because batch rules require `GlobalRegistry`/EventBus-style decoupling. Per-system wrappers were rejected because third-party/concurrent owner code may not exist or may be moving.
Scalability potential: Low uses sparse 10-frame snapshots and compact headers. Middle enables comparer and drift detector. High adds overlay/ghost paths. Ultra can spend saved cycles on richer wireframe overlays and longer history without changing authority data.
Hardware Impact: MX350/i3 path avoids per-frame managed allocation and limits disk ring to 500MB; estimated debug-inactive cost < 0.02 ms and active snapshot copy cost bounded by registered byte count.

Problem: `Status_CORE_REPLAY.md` and `Rationale_CORE_REPLAY.md` were missing at session start.
Solution: Create fresh files and treat missing files as fresh hygiene state, not old batch carryover.
Rejected Alternatives: Reading old unrelated agent logs was rejected because batch hygiene forbids previous-batch contamination.
Scalability potential: Disk-backed state survives context compression and lets future loops resume deterministically.
Hardware Impact: Documentation-only; no runtime cost.

Problem: The replay writer still used a managed byte staging array plus `FileStream.Write`, which violated the batch demand for zero-GC circular MMF snapshots.
Solution: Replace the writer surface with `MemoryMappedFile`/`MemoryMappedViewAccessor`, acquire the raw view pointer once, and copy staged snapshot pages into `replay.bin` with `UnsafeMemoryCopyGuard.TryMemCpy`.
Rejected Alternatives: `Marshal.Copy` chunks were rejected because they keep a managed transfer buffer. Direct `FileStream.Write` was rejected because it is not a memory mapped circular recorder and hides copy cost behind managed IO APIs.
Scalability potential: Low writes sparse 10-frame pages into a 499MB ring. Middle adds sidecar rings only when dirty. High/Ultra can increase registered source count or overlay density without changing the disk contract.
Hardware Impact: MX350/i3 path removes 64KB managed staging and avoids per-write array churn; expected active-dump saving is roughly 6-20 us per 2MB page versus chunked managed copy, with no replay file growth past 499MB.

Problem: Tasks 1-5 need deterministic capture without importing Fauna, Physics, Logistics, or Atmosphere implementation types into core.
Solution: Use `NativeMemorySentinel` registered native sources, raw InputSystem packet journaling, frame-indexed LCG seed headers, MathGuard-triggered numeric fault dumps, and an Editor-only binary scrubber.
Rejected Alternatives: Direct concrete subsystem hooks were rejected because 20+ agents are editing those domains and the batch requires decoupled registry/event-style boundaries.
Scalability potential: Low captures only changed byte segments. Middle compares snapshots. High replays ghost and flow sidecars. Ultra can spend saved debug frame time on denser editor visualization.
Hardware Impact: Inactive runtime cost stays near branch checks plus dirty flags. Active snapshot scan is bounded by registered native bytes; unchanged arrays record headers only through `math.select` delta suppression.

Problem: The replay overlay requirement could be interpreted as a render-pipeline mode, but touching URP/camera state from CORE_REPLAY would expand the blast radius and alter frame timing.
Solution: Add an editor gizmo wireframe overlay on the recorder that draws ghost-path replay records over the live scene and can filter by entity hash.
Rejected Alternatives: URP render-feature injection and camera replacement were rejected because they are renderer-domain changes and introduce more failure modes than a forensic overlay needs.
Scalability potential: Low draws last 100 breadcrumbs. Middle filters one entity. High can increase ghost density from sidecar data. Ultra can layer logistics arrows and pressure overlays without changing simulation state.
Hardware Impact: Player cost is 0 us outside editor gizmo drawing; editor draw cost is proportional to <=100 records.

Problem: AUP drift must be caught at the grid/remainder boundary, not after it becomes visible as physics drift.
Solution: Track subject-hash AUP samples over 1000 frames, compare 64-bit grid deltas and float local remainder drift >0.0001, then write a numeric drift record and force a replay dump.
Rejected Alternatives: Vector3 world-space drift checks were rejected because floating-origin conversion can mask the precise bit that changed. Logging only was rejected because the guilty native source can be overwritten before inspection.
Scalability potential: Low tracks 256 subject hashes. Middle forces dumps only on drift. High can sample more systems by calling the same numeric API. Ultra can add richer editor heatmaps from the same sidecar records.
Hardware Impact: MX350/i3 cost is a fixed ring scan up to 256 slots per new subject and constant time after slot discovery; steady sample estimate <0.5 us.

Problem: Loop 2 compile verification hit non-CORE errors in `World/FloraInteractionManager.cs`.
Solution: Treat this as a dependency wall and keep CORE_REPLAY work isolated; do not edit world/flora code from the core replay domain.
Rejected Alternatives: Fixing Flora from CORE_REPLAY was rejected because it violates the domain boundary and could overwrite another agent's current work.
Scalability potential: Documentation captures the wall for the integrator and keeps replay code reviewable.
Hardware Impact: No runtime impact.

Problem: Tasks 11-15 span jobs, entity ghosting, logistics, atmosphere, and VRAM, but direct ownership belongs to other systems.
Solution: Expose narrow numeric/POD APIs (`RecordJobCompletion`, `RecordEntityGhost`, `RecordLogisticFlow`, `RecordAtmosphereCell`, `RecordGraphicsBufferAllocation`) and fixed copy APIs for editor consumers. Every record is stored in a native sidecar ring and flushed to MMF only when dirty.
Rejected Alternatives: Owning `JobHandle` completion, fish objects, logistics solvers, atmosphere grids, or `GraphicsBuffer` registries inside CORE_REPLAY was rejected as cross-domain coupling and concurrent-agent sabotage risk.
Scalability potential: Low samples only selected subjects. Middle opens editor overlays. High can sample more systems through the same fixed APIs. Ultra can draw dense logistics/pressure overlays because runtime state remains POD and bounded.
Hardware Impact: MX350/i3 path uses O(1) ring writes and no managed allocations per sample; editor visualization cost is isolated to editor windows/gizmos.

Problem: Atmosphere pressure map requires a 2D view but runtime UI would steal frame time and create debug allocations.
Solution: Add `DodReplayPressureMapWindow` under Editor only, backed by a persistent `NativeArray<DodReplayAtmosphereCellRecord>` staging buffer and immediate-mode rect drawing.
Rejected Alternatives: Generating textures/materials at runtime was rejected because it adds GPU/GC pressure for a forensic view.
Scalability potential: Low draws 256 sampled cells. Middle color-codes pressure/O2/CO2. High/Ultra can scale by increasing sidecar capacity without touching atmosphere simulation.
Hardware Impact: Runtime cost remains the sidecar write only; editor window draw cost is proportional to copied cell count and does not affect player builds.

Problem: The physics smoke test must fail on a one-bit AUP divergence, but epsilon comparisons would hide determinism failures.
Solution: Add `RecordPhysicsSmokeAupResult`, hash 64-bit grid values and raw `float3` local bits with FNV64, record all grid deltas, and force a dump on any hash or grid mismatch.
Rejected Alternatives: Float epsilon comparison was rejected because replay determinism requires bit identity. Running physics simulations inside CORE_REPLAY was rejected because physics system ownership is outside the core replay domain; the caller runs both sequences and reports final AUP facts.
Scalability potential: Low validates one deterministic fixture. Middle reports numeric deltas. High can batch many subsystem smoke checks through the same fixed record. Ultra can visualize mismatched paths through ghost sidecars.
Hardware Impact: MX350/i3 path performs fixed integer hashing and one ring write; estimate <0.5 us/test result.

Problem: Circular replay storage must never grow past the forensic budget.
Solution: Use a 499MB MMF and wrap the write offset to zero when the next snapshot would exceed capacity; all writes are bounded by the 2MB staging page.
Rejected Alternatives: Multiple rotating files and append-only dumps were rejected because file discovery and unbounded growth complicate crash recovery.
Scalability potential: Low stores sparse delta snapshots. Middle includes sidecars. High/Ultra use the same ring while increasing sample richness only when enabled.
Hardware Impact: Disk footprint hard-capped below 500MB; no managed copy staging.

Problem: A late file state check showed the old `FileStream`/`Marshal.Copy` replay writer path had reappeared.
Solution: Re-apply the MMF writer, remove the managed staging buffer and `TryWriteReplayBytes`, then rebuild Core and Editor successfully.
Rejected Alternatives: Leaving the old writer was rejected because it violates the zero-GC MMF objective even if it compiles.
Scalability potential: Low through Ultra all share the fixed MMF contract; debug richness changes sidecar content, not IO architecture.
Hardware Impact: Removes the 64KB managed write buffer and chunked managed copy from the active dump path.

Problem: The batch required Omega polish only after all tasks were checked, but `CURRENT_BATCH.md` contained no `<POLISH_MANDATE>` tag.
Solution: Record the missing tag as objective evidence, then run a local anti-bloat check against replay writer paths, metadata, and compile state before final logging.
Rejected Alternatives: Inventing a polish mandate was rejected because batch parsing must be literal.
Scalability potential: Final report now points future integrators to exact evidence files and verification commands.
Hardware Impact: Documentation-only; no runtime cost.
