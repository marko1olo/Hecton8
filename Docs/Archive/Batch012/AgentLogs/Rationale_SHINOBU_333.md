# SHINOBU_333 Rationale

Evidence class: STATIC_SOURCE until Unity import, Burst compiler, Play Mode, GCMonitor, profiler, and player-build artifacts exist.

## Decision 00: Work Scope And Hygiene

Problem: The shared worktree is already heavily dirty from other agents. A broad cleanup or deletion pass would collide with unrelated ownership.
Solution: Limit writes to SHINOBU_333 vehicle/physics files, editor scanner/tuner files, and SHINOBU_333 logs/reports. Treat unrelated git changes as external.
Rejected Alternatives: `git reset`, broad YAML/prefab edits, or deleting suspicious files without source proof. These create integration damage and violate concurrent-agent rules.
Scalability potential: Low/MX350 avoids unbounded source churn and compile-wall risk; Middle/High/Ultra retain the same authority route and can add presentation detail outside gameplay truth.
Hardware Impact: Avoiding sibling-domain edits saves rebuild churn; estimated low-end i3/MX350 iteration impact is 0 runtime microseconds and reduced editor compile risk only.

## Decision 01: Replace Scalar Ballast Lift With Data-Only Ballast Tanks

Problem: Existing submarine dynamics computes `totalMass = baseMass + floodMass + cargoMass` and adds `math.lerp(-BallastLiftN, BallastLiftN, BallastRatio01)` to buoyancy. That is a scalar control hack, not ballast physics.
Solution: Add explicit `BallastTankDTO` rows and Burst jobs that integrate tank water liters, compressed air pressure, ambient pressure, displaced volume, and net vertical force.
Rejected Alternatives: Increase `Rigidbody.mass`, mutate config mass, or keep `BallastLiftN` as the primary vertical control. Those hide ballast truth from Added Mass Tensor, rollback, telemetry, and pressure failure modes.
Scalability potential: Low uses one central sample and cached density; Middle adds bow/stern blending; High/Ultra can raise sample fidelity and visual/audio presentation without changing tank DTO authority.
Hardware Impact: Linear `NativeArray` traversal over 32-byte tank rows is cache-predictable. Estimated low-end i3/MX350 gain versus managed per-component buoyancy scripts: 20-80 us/frame depending on object count; exact profiler proof absent.

## Decision 02: Cinematic Cheat Boundary

Problem: Compressed-air expulsion feels important, but simulating air particles or bubbles as physical truth is waste.
Solution: Gameplay truth is two float comparisons and liter integration. Presentation uses a sparse `MovementAcousticSignal`/acoustic lane and later VFX can consume telemetry.
Rejected Alternatives: CPU particle air, per-bubble buoyancy, or AudioSource mixing in the solver. They spend frame budget on invisible causes.
Scalability potential: Low gets cheap hiss/telemetry only; Middle adds richer audio cadence; High/Ultra can buy volumetric wakes/salt/splash visuals in VISUAL_SYNC only.
Hardware Impact: Dear-lie acoustic dispatch costs bounded signal write cadence, estimated <2 us when active and 0 us when not emitted; exact proof pending.

## Decision 03: External Force Instead Of Rigidbody Mass

Problem: The old auto-level path computed ballast water as internal mass and wrote `_hull.mass = totalMass` when `SubmarineFluidDynamics` was absent. That changes PhysX body mass instead of expressing ballast water as a force truth.
Solution: Keep center-of-mass weighting for presentation/control, but move vertical ballast truth to `CalculateBuoyancyForceJob` and route its `SubmarineBallastForcePacketDTO.NetForce` through `PhysicsForceRouter.QueueAmbientForce`.
Rejected Alternatives: Direct `Rigidbody.mass`, direct `Rigidbody.AddForce`, or preserving `BallastLiftN` scalar lift. Those bypass central force ownership and make Added Mass / rollback data lie.
Scalability potential: Low uses one central submerged-volume sample; Middle/High/Ultra raise active sample count continuously from GlobalQualityWeight while the force packet schema stays unchanged.
Hardware Impact: Avoids PhysX mass resynchronization and per-component buoyancy loops. Estimated low-end i3/MX350 gain is 10-40 us/frame for the submarine body, exact profiler proof blocked by external compile wall.

## Decision 04: Pressure Failure Is Gameplay Truth

Problem: A purge command that always succeeds makes abyss depth irrelevant and hides compressed-air constraints.
Solution: `EvaluateBallastTanksJob` computes ambient ATM from the fluid sample and refuses to reduce tank water when `AmbientPressureATM >= CompressedAirPressureATM`, setting pressure-block flags for telemetry.
Rejected Alternatives: A designer boolean such as `canSurface` or a depth curve that ignores tank air pressure. Those are easier but not mathematically inspectable.
Scalability potential: Weak devices pay two float comparisons per tank; high/ultra devices can attach richer hiss/bubble/wake visuals to the same pressure flag.
Hardware Impact: Less than 1 us for four tanks on i3/MX350-class CPU; visual overkill remains decoupled in signal consumers.

## Decision 05: Verification Wall Handling

Problem: The first build failed because Unity's ignored generated `Hecton8.Core.csproj` did not include the new runtime file. After adding that compile item locally, the second build reached unrelated missing-symbol failures in VRSomatic, Gyro partials, Metabolism, and Fauna files. The third gated build produced no C# source diagnostics and ended with `csc.exe` exit code -1 after roughly one minute.
Solution: Treat SHINOBU_333 compile as statically clean under the attempted project build because no SHINOBU_333 file appears in the dependency error set. Record the external compile wall and compiler crash boundary instead of editing sibling domains or running a fourth compile while CPU sampling is saturated.
Rejected Alternatives: Fix VRSomatic/Gyro/Metabolism symbols outside domain, retry builds indefinitely, or claim a green build. These would violate domain boundary, three-strike compile protocol, or evidence rules.
Scalability potential: No runtime impact. Integration remains ready once sibling-domain compile walls are resolved.
Hardware Impact: Build-time only; 0 runtime microseconds. Avoiding cross-domain edits prevents additional rebuild churn on low-end developer machines.

## Decision 06: BufferID Collision Repair

Problem: The draft SHINOBU_333 range `71820..71827` collided with SHINOBU_264 async buoyancy ownership `71820..71831` in the binary payload ledger.
Solution: Move SHINOBU_333 ballast lanes to `71771..71778` under `SystemID.VehiclesPhysics`, update H8Memory, reports, route card, and ledger.
Rejected Alternatives: Share SHINOBU_264 lanes or keep the collision with a note. Shared lanes would destroy one-owner proof and make crash dumps ambiguous.
Scalability potential: Low/Middle/High/Ultra all retain identical DTO identity; quality changes math cadence only, not buffer ownership.
Hardware Impact: Runtime microseconds saved: 0. Hardware risk removed: deterministic memory alias/corruption from two systems writing the same BufferIDs.

## Decision 07: Hot Read Split From Cold Ensure

Problem: Read-looking `TryResolve*` helpers could call `EnsureGenerationHandle`, which is acceptable during cold owner setup but not inside fixed/post-fixed ballast work.
Solution: Rename SHINOBU_333 creation path to `Ensure*Cold` and keep runtime ballast schedule/complete paths on `TryReadBallast*`/`TryReadVaultBuffer`, which only opens existing generation handles or fails closed.
Rejected Alternatives: Keep mutating `TryResolveBallast*` names or allocate missing buffers from the hot scheduler. Both violate accessor purity and hide Vault mutations behind read syntax.
Scalability potential: Weak devices avoid surprise allocation stalls; high/ultra devices can raise sample count without changing memory ownership.
Hardware Impact: Prevents hot allocation/growth stalls. Estimated low-end i3/MX350 win is spike prevention, not steady-state ALU; exact profiler proof pending.

## Decision 08: CSV Bridge Without Data Monolith Claim

Problem: Task 17 required a cold CSV ingestor, but the Data Monolith static payload `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
Solution: Add `Data/Physics/vehicle_ballast_profiles.csv` and wire cold ingestion through Vault scratch `71778` into profile rows `71776` using `ReadOnlySpan<byte>`, FNV-1a hashes, and manual float parsing. Mark Data Monolith status as absent/yellow.
Rejected Alternatives: Claim Data Monolith readiness without the binary payload, or use managed `float.Parse`/ScriptableObject config. Both violate evidence and zero-GC parser mandates.
Scalability potential: Low uses the same parsed physical limits; middle/high/ultra spend saved CPU on presentation, not different gameplay truth.
Hardware Impact: Runtime hot-path cost 0 us after cold load. Cold sequential file read is bounded by 32768 bytes.

## Decision 09: Route Card Yellow Instead Of Green

Problem: Static source proof exists, but compile/runtime/profiler/Data Monolith proof is missing or externally blocked.
Solution: Mark SHINOBU_333 route disposition `YELLOW / STATIC_SOURCE_ONLY` and create `Docs/Reports/SHINOBU_333_SELF_AUDIT.xml`.
Rejected Alternatives: Report green or hide the external compile wall. That would be a fake proof artifact.
Scalability potential: No gameplay effect; integration gates stay explicit across hardware tiers.
Hardware Impact: Runtime 0 us. Saves developer time by preventing false green handoff into QA.

## Decision 10: Legacy PhysX Mass-Property Bridge Boundary

Problem: The compatibility controller still caches/applies `Rigidbody.centerOfMass`, `angularDamping`, and `inertiaTensor` for legacy vehicle stabilization/flood presentation, while SHINOBU_333 only owns ballast buoyancy truth.
Solution: Keep ballast vertical force authority in Vault/job/force-router path and document the mass-property bridge as a yellow dependency until a dedicated vehicle mass-properties packet route exists.
Rejected Alternatives: Invent a new cross-domain mass-properties packet without a registered owner, or delete legacy stabilization state blindly. Both risk breaking another agent's vehicle dynamics work.
Scalability potential: Weak devices keep the cheap bridge; high/ultra can move visual mass-property response to a proper route later without changing ballast DTOs.
Hardware Impact: Current SHINOBU_333 force path avoids Rigidbody.mass sync. Remaining bridge cost is pre-existing vehicle compatibility debt, not new ballast solver cost.

## Decision 11: Unity Import Metadata Hygiene

Problem: A read-only audit found three new SHINOBU_333 files without metadata sidecars: the runtime ballast contract, the OOP buoyancy scanner, and the CSV source.
Solution: Add stable `.meta` files with unique SHINOBU_333 GUIDs and verify the GUIDs appear only in those three metadata files. The CSV remains a non-Unity external cold source under `Data/Physics`; its sidecar is repository identity hygiene, not a Unity import claim.
Rejected Alternatives: Let each workstation generate different GUIDs during Unity import for the two Unity assets, or move the CSV under another domain. Both would create nondeterministic identity and merge churn.
Scalability potential: Runtime behavior is unchanged across Low/Middle/High/Ultra; this protects editor/import determinism for Unity assets and repository identity for external data only.
Hardware Impact: Runtime 0 us. Editor/import impact is reduced asset reimport churn and no duplicate GUID collision.

## Decision 12: Sample Budget Hysteresis

Problem: Task 11 mapped `GlobalQualityWeight` directly to an integer sample count. That is continuous in source weight but can flicker around the 1/2/3/4 thresholds under thermal oscillation.
Solution: Keep the continuous smoothstep/lerp quality curve, but resolve the integer sample budget through a 2.5 second hysteresis hold in the owner phase and store it in the existing 160-byte fluid sample DTO at offset 148.
Rejected Alternatives: Use a binary low/high quality branch, run all four samples always, or let the Burst force job flip sample count immediately every frame. The first violates doctrine, the second wastes mobile ALU, and the third causes visible pitching/telemetry thrash.
Scalability potential: Low uses one center sample; middle tiers hold stable 2-3 sample approximations; high/ultra hold four analytical points and can spend saved CPU budget on presentation while force ownership stays unchanged.
Hardware Impact: Adds two owner-phase floats/ints and one 4-byte DTO field already inside padding. Weak devices avoid three analytical sample computations; estimated saving remains micro-scale per submarine, but jitter prevention is the real gain.

## Decision 13: Runtime Assembly Boundary Scan

Problem: The polish mandate requires proof that SHINOBU_333 did not add a direct sibling runtime assembly dependency.
Solution: Scan `*.asmdef` files. `Assets/_Project/Scripts/Physics/Vehicles` currently has only `Hecton8.Physics.Vehicles.Editor.asmdef`; SHINOBU_333 runtime code remains inside the existing root `Hecton8.Core.asmdef` compilation unit and no new runtime asmdef or asmdef reference was added.
Rejected Alternatives: Introduce `Hecton8.Physics.Vehicles.Runtime.asmdef` during this polish pass, or move ballast DTOs into Core.Contracts without a broader integration ticket. Both would force a compile-wall event and likely require sibling owners to rewire references.
Scalability potential: Runtime behavior is unchanged; Low/Middle/High/Ultra all keep the same Vault and Signal routes.
Hardware Impact: Runtime 0 us. Developer hardware impact is avoided recompilation churn and no new assembly dependency graph edge.

## Decision 14: Ballast Timing Proxy Flag

Problem: `ComputeMicros` is currently derived from schedule-to-completion wall time in the owner phase. That is useful for black-box spike detection, but it is not exact Burst job execution time.
Solution: Add `ForceFlagTimingProxy` and copy that flag into the force packet and telemetry row whenever owner-phase timing is patched.
Rejected Alternatives: Claim exact Burst execution timing without profiler instrumentation, or remove timing from the telemetry ring. The first is false evidence; the second loses endurance-bot forensic value.
Scalability potential: Low/Middle/High/Ultra all retain identical telemetry layout and fault route. Future profiler-backed timing can replace the proxy without changing the DTO.
Hardware Impact: One flag OR and one telemetry flag assignment after job completion; runtime hot Burst cost is 0 us. Prevents QA from chasing false exact-timing claims.

## Decision 15: Owner-Phase Snapshots For External Runtime Facts

Problem: The fixed/post-fixed PID bridge still refreshed SHINOBU_332 gyro activity through `SubmarineDynamicsRuntime.TryGetActiveGyroRouteForEntity`, and ballast sample prep read `HomeostasisBrain.GlobalQualityWeight` plus runtime-origin AUP through live global accessors.
Solution: Cache the continuous quality scalar and runtime-origin AUP in owner/cold phases, and read SHINOBU_332 activity from its Vault counter `BufferID.Shinobu332GyroCounters` through a cached generation handle. The hot PID path now resolves only an already-acquired Vault handle and compares `SubmarineGyroCounterDTO.LastTargetEntityHash` against this controller's target/fallback hashes.
Rejected Alternatives: Keep polling the sibling runtime static, call `GlobalRegistry`/`GlobalSignals` from read helpers, or allocate a SHINOBU_333 shadow route flag. The first preserves compile/runtime coupling, the second violates accessor purity, and the third creates a second owner for SHINOBU_332 route truth.
Scalability potential: Low devices avoid managed sibling runtime calls in fixed/post-fixed PID suppression; Middle/High/Ultra keep identical gameplay truth while quality and sample budgets continue to scale smoothly from the cached scalar.
Hardware Impact: Estimated steady-state ALU win is small (<1 us/submarine), but it removes branchy managed runtime lookups from the hot path and prevents hidden global-origin reads in AUP conversion. No new BufferID is owned by SHINOBU_333; `71786` remains SHINOBU_332-owned read-only input.

## Decision 16: Broadphase Water Query Scanner Scope

Problem: The scanner counted both `Physics.OverlapSphere` and `Physics.OverlapSphereNonAlloc` as water-query violations, but the report builder did not explain that scope.
Solution: Keep both calls in scope because the prompt targets CPU broadphase water-volume detection, not only managed allocation. Update the editor-only report builder and JSON reports with an explicit `overlapScannerScope` field.
Rejected Alternatives: Exempt `OverlapSphereNonAlloc` or rely on manual report edits after scanner runs. The first would allow broadphase water-volume authority back into vehicles; the second makes the proof artifact non-reproducible.
Scalability potential: Low/Middle/High/Ultra retain the same scanner rule; runtime gameplay cost is 0 because the scanner is editor-only.
Hardware Impact: Runtime 0 us. Editor scan remains bounded to source text/Roslyn traversal and prevents a CPU broadphase query class from re-entering the vehicle runtime.

## Decision 17: Cached-Handle-Only Hot Vault Reads

Problem: `TryReadVaultBuffer` was a read-looking helper used by fixed/post-fixed ballast paths, but on a cached-handle miss it could call `TryGetGenerationHandle`. That is not allocation, but it is still a Vault metadata refresh hidden inside a hot read helper.
Solution: Remove the `TryGetGenerationHandle` fallback from `TryReadVaultBuffer`. Hot ballast reads now resolve only already cached generation handles and fail closed. `Ensure*Cold` and `TryResolveExistingVaultBuffer` remain the only places that acquire or refresh handles.
Rejected Alternatives: Keep metadata refresh inside every hot read, or call `Ensure*Cold` from fixed/post-fixed when a buffer is missing. The first hides a Vault resolve behind read syntax; the second would allow allocation/growth pressure in gameplay cadence.
Scalability potential: Low devices avoid surprise Vault metadata work in ballast fixed/post-fixed reads; middle/high/ultra retain identical gameplay truth and can still increase sample richness from cached quality.
Hardware Impact: Steady-state ALU change is near 0 us when handles are valid. Spike risk is lower on i3/MX350 because stale-handle recovery is deferred to cold/owner windows instead of occurring inside the solver cadence.

## Decision 18: Private Padding Fields In Support DTOs

Problem: `SubmarineBallastFluidSampleDTO`, `SubmarineBallastForcePacketDTO`, `SubmarineBallastTuningDTO`, and `SubmarineBallastProfileDTO` had explicit padding fields that were public. The byte layout was correct, but padding should not be part of the mutable external state surface.
Solution: Change those support DTO padding fields to `private` while preserving every `[FieldOffset]` and total size.
Rejected Alternatives: Leave public padding because it does not affect `UnsafeUtility.SizeOf`, or remove padding entirely. The first leaks meaningless fields into the ABI; the second risks size drift and ARM64 layout regression.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The gain is contract clarity: only meaningful payload fields are externally visible.
Hardware Impact: Runtime 0 us. Layout is unchanged; this removes accidental writes/reads to padding without changing cache-line behavior.

## Decision 19: Shared Proof Artifact Merge Repair

Problem: A read-only subagent found the shared `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` had been overwritten by another agent's top-level report and no longer contained the SHINOBU_333 scanner block. During verification it was overwritten again by SHINOBU_346/340, proving the file is a concurrent shared artifact. The editor scanner builder also lagged behind the sidecar on GUID-scan and compile-wall wording.
Solution: Re-add `shinobu333SubmarineBallastScanner` as a non-destructive top-level property while preserving the current shared JSON object. Update `OOP_Buoyancy_Scanner.BuildReport()` so reruns emit the same metadata and compile proof as `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_333.json`, and record the sidecar as the authoritative SHINOBU_333 proof when the shared report is clobbered by other agents.
Rejected Alternatives: Replace the shared report wholesale, or rely only on the sidecar. Wholesale replacement would clobber other agents; sidecar-only proof would fail Task 19's shared report requirement.
Scalability potential: Runtime behavior is unchanged across hardware tiers. The gain is audit stability: proof artifacts can be regenerated without erasing neighbor domains.
Hardware Impact: Runtime 0 us. Editor/report path only; prevents QA and CTO review from losing the ballast proof artifact due to concurrent report writes.
