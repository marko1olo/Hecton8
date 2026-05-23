# SHINOBU_332 Rationale - Submarine Pitch/Roll Auto-Level

Date: 2026-05-22
Status: YELLOW_STATIC_SOURCE_REPAIRED / BUILD DEFERRED BY CPU POLICY

## Preflight

Problem: Legacy submarine auto-level routes can hide Unity `ConfigurableJoint`, Euler-angle correction, or direct `Rigidbody.AddTorque` in vehicle/physics code.
Solution: Treat `CURRENT_BATCH.md` XML block as the only assignment, constrain ownership to Echelon 6 item 58, and run archaeology before authoring any runtime surface.
Rejected Alternatives: Creating a standalone gyro manager before scan; copying another agent's DataVault/physics assumptions; reading neighboring batch prompts.
Scalability potential: Low/MX350 path must be diagonal tensor or scalar fallback; Middle path keeps full PD at normal cadence; High path keeps full inverse tensor; Ultra spends surplus in VISUAL_SYNC instrumentation and presentation, not extra gameplay truth.
Hardware Impact: Expected gain on i3/MX350 comes from removing joint solver constraint churn and Euler/Transform access; exact microseconds are PENDING VERIFICATION until profiler or static compile artifacts exist.

Problem: Runtime DTOs and force/telemetry payloads can fault or stall on ARM64 if padding is guessed.
Solution: Primary DTO must use explicit layout and a self-audit offset map. `SubmarineGyroDTO` target: 32 bytes, field offsets 0/4/8/12/16/20, padding at 24 and 28.
Rejected Alternatives: Sequential layout with implicit padding; runtime `bool`; property wrappers causing CS1612 copies.
Scalability potential: Stable 32-byte stride supports flat NativeArray traversal from weak mobile silicon through high-end desktop SIMD.
Hardware Impact: Avoids misaligned runtime fields and defensive copies; estimated benefit is cache-line stability, not a fake timing claim.

## Mandates Selected Before Coding

- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `PHYS_Determinism_Multithreaded_Body_Solving.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Execution_Phases.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`

## Initial Analysis

Target: Replace joint/Euler/constraint-based pitch-roll leveling with Burst quaternion PD math and force-packet output.
Affected systems: Vehicles, Physics, DataVault-native DTOs, ForcePacket/PhysicsApplySystem handoff, SignalBus/global signal warning lanes, VISUAL_SYNC cockpit horizon buffer.
Zero GC proof: Planned hot path is `IJobParallelFor` over unmanaged arrays, no LINQ, no managed classes, no string/logging, no Transform or Rigidbody access, no per-frame allocation.
State check: Existing owner/runtime classes and buffer contracts are unknown until archaeology. No dictionary/pool assumptions accepted. No post-OnDisable ownership changes until existing lifecycle is read.
Rule quote: AGENTS.md global systems doctrine requires one fact -> one owner -> one route -> one proof artifact; direct hot `GlobalRegistry` polling and tiny same-frame jobs are rejected.

## Decisions Implemented

Problem: Existing submarine stabilization was embedded as direct cross-product gyro strength and angular damping inside `Submarine6DIntegratorJob`, with no independent tuning DTO, packet artifact, or telemetry ring.
Solution: Removed direct gyro torque math from the integrator and inserted a producer chain: `CalculateAddedMassTensorJob -> CalculateGyroscopicErrorJob -> EvaluatePdControllerJob -> RecordGyroTelemetryJob -> Submarine6DIntegratorJob`. The integrator now consumes `ForceFlagGyroCorrection` if it matches the frame.
Rejected Alternatives: `ConfigurableJoint`, `Rigidbody.AddTorque`, Transform/Euler leveling, and a separate `HectonSubmarineGyroManager` tick owner. All would split authority or re-enter managed/PhysX hot paths.
Scalability potential: Low uses diagonal angular tensor via `ResolveTensorBlend`; Middle blends partial tensor; High/Ultra uses full added-mass inverse and spends surplus on visual horizon/telemetry, not extra gameplay truth.
Hardware Impact: i3/MX350 avoids joint solver churn and avoids full inverse when quality is low. Expected saving is solver work removal; exact microseconds require profiler capture after CPU gate clears.

Problem: Pitch and roll PD gains require exact ARM64 layout and no property copies.
Solution: Added `SubmarineGyroDTO` `[StructLayout(LayoutKind.Explicit, Size = 32)]` with offsets 0/4/8/12/16/20 and private uint pads at 24/28. Hot jobs read unmanaged fields through pointer lanes.
Rejected Alternatives: Sequential layout, C# properties, `bool`, managed controller classes, or editor-only serialized wrappers as runtime truth.
Scalability potential: 32-byte stride keeps tuning cache-local for all devices; profile lanes allow Scout/Middle/Freighter/Ultra tuning without changing DTO authority.
Hardware Impact: Stable 32-byte loads avoid defensive copies and alignment faults on ARM64-class devices; direct microsecond gain is memory-stability, not a fake timing number.

Problem: Submarine currents/weather owner was not available as a stable producer, but PD needs stress input.
Solution: Added `GenerateMockTurbulenceJob`, deterministic per-frame hash injection into angular velocity behind `enableMockGyroTurbulence`.
Rejected Alternatives: Blocking on ocean/weather agents or using random managed noise in `Update`.
Scalability potential: Disabled in normal path; when enabled, amplitude is scalar and the same job validates Low/Middle/High/Ultra damping response.
Hardware Impact: Zero cost when disabled; enabled path is a small deterministic arithmetic spike for QA only.

Problem: Player cockpit needs stabilization effort without CPU transform updates.
Solution: Added `SubmarineGyroVisualStateDTO` and `SyncGyroVisualBuffer()` to upload error/effort to `_H8SubmarineGyroVisuals` during visual sync.
Rejected Alternatives: Rotating a 3D artificial horizon model, UI geometry updates, or console logs.
Scalability potential: Low can ignore shader detail; Middle/High/Ultra shader can use the same buffer for richer cockpit instrumentation.
Hardware Impact: CPU pays one structured-buffer upload for active vehicles; no UI mesh churn or Transform hierarchy sync.

Problem: Fault forensics required last-300-frame evidence.
Solution: Added `GyroTelemetryEntry` ring in DataVault and `DumpGyroBlackBoxIfFaulted()` writing `Docs/AgentLogs/Dump_SHINOBU_332.bin` on NaN or >200us.
Rejected Alternatives: managed logs, exceptions, or "cannot reproduce" reports.
Scalability potential: Same 64-byte ring entry across tiers; quality only changes tensor blend, not telemetry identity/layout.
Hardware Impact: One reduction job and one ring write per frame; dump only on fault.

Problem: Static proof of Euler/joint purge must be visible to integrators.
Solution: Added `Euler_Angle_Scanner` and updated `PHYSICS_OPTIMIZATION_REPORT.json` plus sidecar report. The scoped executable stabilizer count is zero; two remaining tokens are non-executable editor/report strings.
Rejected Alternatives: Relying on chat claims or broad grep without AST filtering.
Scalability potential: Editor-only scanner; no player runtime cost.
Hardware Impact: No runtime impact.

Problem: Build proof could not be executed without violating local CPU/dotnet protection.
Solution: Sampled CPU at 87.18%; no `dotnet`/`csc` process was active, but build was deferred because CPU exceeded the 50% ceiling. Static checks run instead: JSON parse, `diff --check`, focused forbidden-token scan, brace count, DataVault ID scan.
Rejected Alternatives: Launching dotnet anyway and starving the user workstation.
Scalability potential: Compile proof remains a pending gate, not a quality claim.
Hardware Impact: Protected local machine from rebuild contention.

Problem: The first visual buffer route still allowed `GraphicsBuffer` creation from `LateFrameTick`, and used `SetData` for the whole row set.
Solution: Moved `GraphicsBuffer` create/resize into `EnsureGyroVisualGraphicsBuffer`, called from the Vault/capacity setup path. `SyncGyroVisualBuffer` now performs no hot resource allocation, skips duplicate telemetry frames, and uploads with `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`.
Rejected Alternatives: Recreating the buffer on visual sync, relying on `SetData`, or adding per-row dirty bookkeeping for a maximum 16-row buffer. Dirty pages are unjustified at a fixed 1024-byte worst-case upload.
Scalability potential: Low/Middle/High/Ultra all share the same 64-byte shader-safe rows; higher quality can spend the scalar data on richer cockpit shader treatment without changing gameplay truth.
Hardware Impact: Removes hot GPU resource churn. Worst-case upload is 16 * 64 = 1024 bytes per new sim frame, below the threshold where page-level complexity is defensible.

Problem: `SubmarineGyroVisualStateDTO` was named GPU-facing but carried `double3 CurrentAup`, making shader structured-buffer layout and portability suspect.
Solution: Repacked visual DTO to 64 bytes of float/uint lanes only: error vector, effort, horizon angles, corrective torque, target hash, frame, flags, and explicit padding.
Rejected Alternatives: Uploading AUP doubles to shaders or splitting the same presentation fact across two buffers.
Scalability potential: Low can ignore corrective torque; Middle/High/Ultra can use it for gauge shimmer, stabilizer stress, and dashboard overdrive visuals.
Hardware Impact: Removes 24 bytes of double data from the GPU row and avoids platform-specific double support/layout risk.

Problem: CSV profile ingest declared a Vault scratch lane but staged bytes through `stackalloc`, and the simulation lock path also locked the scratch lane.
Solution: File bytes are now read into `BufferID.Shinobu332GyroCsvScratch` under a cold write lock, and `TryLockGyroBuffers` no longer locks that scratch lane.
Rejected Alternatives: Keeping `stackalloc byte[length]`, `float.Parse`, or hot simulation scratch locks.
Scalability potential: Profile count and byte capacity can grow via Vault capacity policy without changing parser logic or hot simulation locks.
Hardware Impact: Removes cold stack pressure and avoids widening the per-frame lock set.

Problem: The prompt literally named `NativeQueue<ForcePacketDTO>`, but this submarine owner already has a deterministic force accumulator consumed by its integrator.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_332_SUBMARINE_GYRO_ROUTE_CARD.md` and ledger entry. The chosen route is `SubmarineGyroForcePacketDTO` as proof lane plus existing `SubmarineForceAccumulator` as the single apply lane.
Rejected Alternatives: Adding a second queue and second apply owner for the same vehicle torque fact.
Scalability potential: One-slot-per-vehicle packets avoid atomic queue contention on weak devices and remain inspectable for high-tier telemetry/debug views.
Hardware Impact: Avoids queue atomics and extra apply dispatch for a fixed 16-vehicle submarine lane.

Problem: Legacy Gameplay ballast controller could still run a PhysX PID auto-level torque path if it existed on a scene object, even though default auto-install is disabled.
Solution: Added a narrow compatibility fence in `SubmarineAutoLevelBallastController`: register/slow lifecycle refreshes a cached SHINOBU_332 active flag; fixed paths read the byte flag. When active, legacy PID state is reset, `PhysicsForceRouter.QueueTorque` is not called, and `SuppressesKinematicPitch` returns false.
Rejected Alternatives: Deleting the entire ballast/flood/combat bridge outside the safe domain slice, or allowing duplicate torque authority.
Scalability potential: Low through Ultra all keep one stabilization authority; old controller can still serve non-gyro ballast/flood presentation state when needed.
Hardware Impact: Removes redundant scheduled PID torque, PhysX force queue work, legacy pitch-input suppression, and hot static lookup when the DataVault gyro owner is active.

Problem: AGENTS.md requires double-buffering for all GPU data, but the gyro visual upload used a single `GraphicsBuffer`.
Solution: Added `_gyroVisualBufferA` and `_gyroVisualBufferB`, created only from `EnsureGyroVisualGraphicsBuffer`, and alternated the write index after each upload. `SyncGyroVisualBuffer` writes an inactive buffer with `LockBufferForWrite`, unlocks it, then binds that buffer globally.
Rejected Alternatives: Single-buffer `LockBufferForWrite` relying on driver synchronization, or triple buffering for a fixed 1024-byte visual payload.
Scalability potential: Low can leave the shader count at zero when no active controllers exist; Middle/High/Ultra can consume the same double-buffered rows for richer cockpit stress visuals without changing authority.
Hardware Impact: Same maximum upload size, but removes CPU/GPU read-write hazard and driver stall risk on MX350/Quest-class unified memory.

Problem: The compile guard wording implied a dedicated vehicle runtime asmdef that does not exist in the current project tree.
Solution: Verified affected runtime scripts live under the existing root `Assets/_Project/Scripts/Hecton8.Core.asmdef`; no asmdef reference was added or mutated. Editor scanner remains in `Hecton8.Physics.Vehicles.Editor.asmdef`.
Rejected Alternatives: Inventing or editing a new `Hecton8.Physics.Vehicles.Runtime.asmdef` during a batch with 20+ active agents.
Scalability potential: No runtime scalability effect; this protects compile-wall truthfulness.
Hardware Impact: Avoids project file churn and unnecessary recompile graph expansion.

Problem: Subagent static audit found SHINOBU_332 Vault IDs `71740..71742` collided with terrain pager IDs `71740..71758`.
Solution: Moved the entire SHINOBU_332 block to `71780..71787`, leaving no split ownership inside the route, and updated `H8Memory`, route card, binary ledger, self-audit, editor scanner, shared report, and sidecar report.
Rejected Alternatives: Moving only the three colliding tail IDs, or keeping current docs while relying on allocation order. Both preserve hidden ABI risk.
Scalability potential: Stable IDs do not change Low/Middle/High/Ultra math; they prevent cross-domain Vault alias corruption across every tier.
Hardware Impact: Prevents type/owner mismatch and cache corruption. Microsecond estimate is not meaningful; this is correctness and memory sovereignty.

Problem: `GyroTelemetryEntry[300]` used uninitialized memory but `DumpGyroBlackBoxIfFaulted` can read it during OnDisable before the first valid frame.
Solution: Telemetry now uses cold `NativeArrayOptions.ClearMemory`, and dump checks return immediately while `_frameCounter == 0u`.
Rejected Alternatives: Trusting uninitialized rows, or clearing the ring every frame. Frame-zero guard plus cold clear is deterministic and cheap.
Scalability potential: One 19,200-byte cold clear at boot is constant across tiers and does not alter gameplay truth or DTO layout.
Hardware Impact: Removes bogus fault dump/glitch risk with no recurring frame cost.

Problem: The first legacy ballast fence was global and slow-polled; one active submarine runtime could suppress unrelated ballast controllers, and a newly active gyro route could race one fixed tick before the next slow refresh.
Solution: Added `SubmarineDynamicsRuntime.TryGetActiveGyroRouteForEntity(uint)` and changed the legacy controller to refresh with hull/fallback entity hashes before PID scheduling and before torque apply.
Rejected Alternatives: Global `TryGetLatest(out _)` existence checks, deleting the entire ballast controller, or accepting one-tick duplicate torque.
Scalability potential: All tiers keep one stabilization owner per matched entity; unrelated controllers remain free to run ballast/flood logic.
Hardware Impact: Two cached hash comparisons on the legacy path; avoids duplicate `PhysicsForceRouter.QueueTorque` for the matched submarine.
