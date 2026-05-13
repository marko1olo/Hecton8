# LOCKSTEP_STATE_VALIDATOR Rationale

Status: PENDING - GLOBAL COMPILE DEPENDENCIES BLOCK OMEGA BURST CHECK

Problem: The batch requires deterministic replay hashing without depending on systems that may not exist or may be owned by other agents.
Solution: Use GlobalRegistry/EventBus boundaries, discover existing contracts first, and implement a narrow determinism assembly if no existing owner exists.
Rejected Alternatives: Directly editing physics, inventory, VFX, or audio owners before interface discovery would create brittle cross-domain coupling and break parallel-agent isolation.
Scalability potential: Low disables normal-play hashing except replay mode; Middle hashes every 300 POST_SIMULATION frames; High keeps full diagnostic subsystem hashes; Ultra can retain extra individual category hashes and replay diagnostics for visual overkill tooling.
Hardware Impact: On i3/MX350, a 300-frame cadence amortizes hashing cost and keeps normal play at 0us when disabled by tier. The target is a bounded POST_SIMULATION spike below 0.1ms on sampled frames, pending Unity/Burst profiler proof.

Problem: Origin shifts can create false desyncs if world-space floats are hashed.
Solution: Hash AUP sector-local state and stable IDs, not shifted `Transform.position` floats.
Rejected Alternatives: Hashing Unity transforms or rigidbody world positions is faster to wire but invalid under AUP rebase and PhysX presentation drift.
Scalability potential: Low stores master hash only; Middle stores subsystem hashes; High/Ultra store individual category hash vectors for replay forensic UI.
Hardware Impact: Avoids repeated transform reads and float instability, saving main-thread work and preventing false replay breakage on MX350/i3.

Problem: The validator needs `PlayerKinematicState` and `RoomWaterLevels`, but current owners expose player pose and habitat water through service contracts rather than pre-existing DataVault buffers.
Solution: Register new `BufferID` values and mirror the current service snapshots into vault-owned native arrays before each sampled hash frame.
Rejected Alternatives: Editing player movement, habitat, or inventory owners to publish new arrays would create direct cross-domain dependencies while other agents are active.
Scalability potential: Low keeps mirror writes disabled with normal hashing; Middle mirrors one player and room water; High/Ultra can expand the mirrored state contract without changing the replay file format.
Hardware Impact: On i3/MX350 the room mirror is linear in room count every 300 frames only; normal low-tier play pays 0us because hashing is disabled outside replay mode.

Problem: Replay desync needs a deterministic event path that decoupled systems can subscribe to.
Solution: Reuse `PhysicsDeterminismSignals.DesyncDetectedSignal` and publish from the replay mismatch branch, then request pause through `GlobalRegistry.Dispatcher`.
Rejected Alternatives: Throwing exceptions or writing only console logs would be non-deterministic, user-facing hostile, and useless to replay tooling.
Scalability potential: Low dumps master/subsystem hashes; Middle adds replay block frame; High/Ultra can wire forensic UI to the same signal without touching the hash job.
Hardware Impact: Mismatch path only; expected 0us in non-desync gameplay.

Problem: Full project compilation currently fails in unrelated domains, which can hide new regressions if treated as a single binary result.
Solution: Use Unity console filtering and `validate_script` on `LockstepStateValidator.cs`; record the unrelated compile wall separately until dependencies are fixed.
Rejected Alternatives: Fixing submarine fluid, fauna, UI, terrain, world, and kinematics errors from the core lockstep task would violate domain scope and risk other agents' work.
Scalability potential: Keeps core determinism isolated and reviewable while integration catches up.
Hardware Impact: 0us runtime; prevents architectural churn.

Problem: Starting replay file IO at component enable creates thread/file overhead on Low/MX350 even though normal gameplay hashing must be disabled there.
Solution: Start the replay writer lazily from the sampled write path and skip writes while ghost replay is active.
Rejected Alternatives: Always-on writer thread was simpler but violates the low-tier zero-cost normal-play requirement.
Scalability potential: Low normal play pays no writer overhead; Middle/High record replay hashes; Ultra can use the same block format for heavier forensic capture.
Hardware Impact: Saves one background thread, one open file handle, and all `.h8replay` IO on i3/MX350 normal play.

Problem: `RequestTimeDilation(10f)` is clamped by the normal dispatcher path and would not satisfy 10x ghost replay.
Solution: Use the existing `RequestHeadlessTimeDilation(10f)` path for replay validation rather than raising the normal global maximum.
Rejected Alternatives: Changing the global player-facing time dilation cap would be wider than the lockstep validator's domain and could destabilize other systems.
Scalability potential: Low/Replay can validate faster without affecting normal gameplay; High/Ultra can run long replay files at diagnostic speed.
Hardware Impact: 10x replay is a deliberate debug load; no impact outside ghost replay mode.

Problem: Entity and rigidbody state can be corrupted by floating-origin shifts if hashed as presentation-world coordinates.
Solution: Hash the player AUP grid/local state and consume the existing `RigidbodyAUPs` buffer that `GlobalPhysicsStateManager` fills with camera/player-relative AUP positions.
Rejected Alternatives: Scanning every Rigidbody transform in the validator would be a slow, unstable cross-domain transform crawl.
Scalability potential: Low hashes none outside replay; Middle hashes relative positions; High/Ultra can extend `EntityAUPs` with sector/local packed data when the owning entity system publishes it.
Hardware Impact: Reuses existing AUP buffers; no new transform reads on i3/MX350.

Problem: The blackbox must identify which category diverged without hashing VFX/audio or other presentation data.
Solution: Store master hash plus rigidbody/player/room/entity category hashes in a fixed 300-frame `NativeArray<LockstepTelemetryEntry>` ring.
Rejected Alternatives: Dumping managed logs every frame or adding VFX/audio state would add GC and false positives.
Scalability potential: Low writes only fault dumps; Middle captures category hashes; High/Ultra can build forensic UI over the same binary dump.
Hardware Impact: 300 * 64 bytes persistent CPU memory; 0MB VRAM and no per-frame GC.

Problem: Omega Burst/SIMD verification cannot run while Unity compilation and Burst entrypoint discovery are blocked by unrelated assemblies.
Solution: Mark task 19 blocked by dependency after validating `LockstepStateValidator.cs` directly and confirming Unity console has zero lockstep-specific errors.
Rejected Alternatives: Editing gameplay mining contracts, UI tooling assemblies, or bucketing contracts from the lockstep validator pass would create cross-agent conflict.
Scalability potential: Once dependencies compile, the Burst jobs are isolated and ready for SIMD disassembly/profiler verification.
Hardware Impact: No runtime cost from the blocker; verification remains pending.

Problem: Ghost replay could false-desync if the validator's local frame counter did not match the recorded replay block fence.
Solution: Validate replay headers, require contiguous 300-frame blocks, align `_postSimulationFrame` to the first replay block start, and emit a distinct desync flag when the replay fence frame does not match the sampled frame.
Rejected Alternatives: Ignoring the recorded frame numbers would hide replay corruption; removing frame from the master hash would reduce desync evidence quality.
Scalability potential: Low/Replay gets deterministic debug playback without extra normal-play cost; Middle/High/Ultra can trust replay blocks as forensic assets rather than stale appended files.
Hardware Impact: Header validation is cold file-load work only. Runtime sampled-frame cost is unchanged except a single integer comparison on replay samples.

Problem: `.h8replay` recording appended new blocks to old files, allowing stale cross-session blocks to poison later ghost replays.
Solution: Open the writer with `FileMode.Create` when the lazy replay writer starts so every run owns one clean replay stream.
Rejected Alternatives: Appending with sequence checks still loads stale first blocks and creates ambiguous forensic files.
Scalability potential: Low still avoids writer creation during normal play; Middle/High record clean replay windows; Ultra can layer richer replay UI over clean session files.
Hardware Impact: 0us normal low-tier cost. Cold writer startup now truncates once instead of seeking to end; sampled-frame staging remains unchanged.

Problem: The hash category flags incorrectly marked exactly `MaxHashElements` entries as truncated and sampled input twice on hash frames.
Solution: Track truncation as an explicit boolean from the source count and reuse the already captured `InputStateSignal` when mirroring player state.
Rejected Alternatives: Treating `count >= MaxHashElements` as truncation creates false telemetry; rereading `SignalBus<InputStateSignal>` on sampled frames wastes cache bandwidth.
Scalability potential: Low unaffected; Middle gets cleaner telemetry; High/Ultra avoid false forensic noise at the 8192-element cap.
Hardware Impact: Prevents false truncation flags at 8192 elements and removes one sampled-frame SignalBus snapshot. Estimated save is 1-3us per sampled frame on i3/MX350, pending profiler proof.

Problem: Cached scalability tier could become stale after user or platform tier changes.
Solution: Register the validator as an `IScalabilityChangedEventListener` and update `_cachedScalabilityTier` from the dispatcher-drained scalability event lane.
Rejected Alternatives: Polling `GlobalRegistry.ScalabilityTier` inside `PostFixedTick` violates the two-stage dependency rule and adds needless hot-path registry reads.
Scalability potential: Low/MX350 can shed the validator immediately after a tier downgrade; High/Ultra can keep forensic hashing without manual reload.
Hardware Impact: 0us per tick beyond the existing cached branch; tier-change handling is event-only.

Problem: Current Unity compile verification is blocked after validator assembly emission by a non-lockstep underwater visuals syntax error.
Solution: Verify the validator directly with `validate_script`, confirm zero `Lockstep` console errors, confirm `Hecton8.Core.Determinism.dll` emission, and leave the underwater visual compile wall to its owning domain.
Rejected Alternatives: Editing `HectonUnderwaterVisuals.cs` from the lockstep validator pass would violate the domain boundary and risk presentation-system regressions unrelated to deterministic replay hashing.
Scalability potential: Core determinism stays isolated; once the visual compile error is cleared, Burst/SIMD proof can be run without changing validator code.
Hardware Impact: 0us runtime. The blocker is compile-only; the validator remains compiled and isolated.

Problem: A replay file could contain valid 300-frame block headers but corrupted per-frame input frame numbers, delaying fault discovery until the hash fence.
Solution: Validate each input frame number during ghost replay load, require contiguous block sequence numbers, add a runtime ghost input frame mismatch tripwire, and stop ghost input override on desync without calling the public time-dilation reset path.
Rejected Alternatives: Waiting until the next 300-frame hash mismatch makes the forensic signal weaker and can replay hundreds of bad inputs before the pause.
Scalability potential: Low/Replay rejects corrupt files during cold load; Middle/High/Ultra get cleaner desync attribution without extra normal-play cost.
Hardware Impact: Cold file-load loop adds one integer comparison per replay frame. Normal gameplay remains 0us. Runtime replay adds one integer comparison per replay input frame.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat audit required proof that the validator does not spend frame time on honest simulation, presentation state, or avoidable floating math.
Solution: Kept the system as a quantized integer hash validator: sampled every 300 post-simulation frames, category-reduced with Burst jobs, low-tier skipped outside replay, and VFX/audio excluded entirely.
Rejected Alternatives: Full-scene transform crawling, per-frame replay IO, raw byte hashing of padded structs, direct controller mutation, or normal dispatcher 10x cap changes.
Scalability potential: Low/MX350 has 0us normal hash/IO cost; Middle gets sampled replay validation; High gets category forensic hashes; Ultra can add richer replay UI over the same fixed binary blocks.
Hardware Impact: 0B GC in hot path; 300 * 64 bytes telemetry ring; replay writer thread/file handle only after first recorded block; expected sampled-frame spike remains pending profiler proof.

Cinematic Cheats Used: 300-frame cadence instead of continuous validation; quantized sector-local/AUP-relative values instead of raw world floats; category Merkle-style reduction instead of serializing full state; low-tier normal-play skip; presentation systems excluded.

Omega Audit Result: No managed `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, `math.normalize`, or floating divisions were found in `LockstepStateValidator.cs`. Structs are default-zeroed and field-hashed; no raw padded memory is part of the hash.

Final Git Diff: modified `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` for `CoreDeterminism`, `PlayerKinematicState`, `RoomWaterLevels`, and `EntityAUPs` IDs; added `Assets/_Project/Scripts/Core/Determinism/Hecton8.Core.Determinism.asmdef`; added `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs`; Unity generated `.meta` files for the new determinism assets; updated this rationale and `Docs/Tasks/Status_LOCKSTEP_STATE_VALIDATOR.md`.

Build Proof: Current manual Roslyn compile of `LockstepStateValidator.cs` against Unity/Bee references passes with and without `UNITY_EDITOR,DEVELOPMENT_BUILD` defines. `validate_script` reported zero diagnostics for `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs` after replay-frame hardening, and the Unity console filter reported zero `Lockstep` errors before later MCP endpoint timeouts. Unity emitted `Library/ScriptAssemblies/Hecton8.Core.Memory.dll` at 2026-05-13 22:21:51 and `Library/ScriptAssemblies/Hecton8.Core.Determinism.dll` at 2026-05-13 22:24:19. `git diff --check` is clean except existing line-ending warnings. Final Burst/SIMD entrypoint verification remains blocked because Unity MCP console/validate endpoints time out after assembly emission.
