# SHINOBU_109 Rationale - KINEMATICS_DEFORMATION_SCULPTOR

Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE until Unity import/console/profiler proof exists.

## Initial Mandate Selection
Problem: Hull damage visuals need deformation without changing physics authority.
Solution: Use cinematic fake-first shader deformation, Burst-native DTOs, AUP localization, and GraphicsBuffer-driven rendering paths.
Rejected Alternatives: Runtime mesh vertex mutation and MeshCollider rebuilds are rejected because PhysX rebuild spikes violate the 0.1 ms suspicion threshold and pollute gameplay truth.
Scalability potential: Low keeps a small dent evaluation window and cheap normal perturbation; Middle raises active dents and breach jets; High expands analytical normals; Ultra spends saved CPU on dense deformation and jet overdraw.
Hardware Impact: Estimated low-end gain versus mesh-collider deformation is avoidance of multi-millisecond PhysX rebuild spikes on i3/MX350. Exact gain PENDING VERIFICATION.

## Active Decisions
Problem: Physical hull damage must look severe without changing collision truth.
Solution: Added a separate visual deformation lane: AUP impact DTOs -> Burst accumulation into DeformationStateDTO -> UberNoir Gaussian vertex displacement and normal bias. The gameplay MeshCollider and physical mesh remain untouched.
Rejected Alternatives: MeshCollider rebuilds, MeshFilter vertex writes, and decal GameObject spawns were rejected because they create PhysX rebuild spikes, managed churn, and rollback ambiguity.
Scalability potential: Low evaluates a tiny visible dent window; Middle keeps packed active dents and pressure buckles; High expands breach jets; Ultra evaluates up to 256 shader dents with analytical normal bias.
Hardware Impact: i3/MX350 avoids multi-ms PhysX rebuild spikes. Quest-class ARM64 avoids runtime mesh upload churn. RTX-class hardware spends saved CPU on shader Gaussian detail and indirect breach jets.

Problem: Runtime DTOs must be ARM64-safe and NativeArray mutation must not trigger CS1612 copies.
Solution: Added explicit layouts: HullImpactDTO = 32 B; DeformationStateDTO = 64 B; BreachJetDTO = 64 B; BreachJetIndirectArgsDTO = 16 B; DeformationTelemetryEntry = 64 B; HullMaterialStrengthDTO = 32 B. DTOs expose public fields and unsafe ref helpers. Runtime layout validation now uses UnsafeUtility.GetFieldOffset for cold offset checks.
Rejected Alternatives: Auto layout, Pack=1, and C# properties on DTOs were rejected because Burst/IL2CPP would lose predictable alignment and mutation semantics.
Scalability potential: Low and Ultra use the same DTO stride, preventing quality-tier-specific serialization bugs.
Hardware Impact: 64 B deformation and telemetry entries align to one L1 cache line; expected gain is stable SIMD/cache behavior on ARM64 and fewer defensive copies in Burst loops.

Problem: Collision/pressure impacts arrive as global positions while the shader requires local float3 precision.
Solution: HullImpactDTO stores double3 ImpactAup. AccumulateHullDamageJob subtracts submarine double3 AUP first, verifies finite delta, then casts only the localized value to float3.
Rejected Alternatives: Passing absolute float positions or shader-side AUP math was rejected because the 100 km world scale would amplify jitter and waste GPU precision.
Scalability potential: All quality weights share one localization law; only evaluated dent count changes.
Hardware Impact: Correctness gain dominates. Prevents flickering dents and NaN propagation on long-distance sectors.

Problem: GPU upload must avoid SetData stalls and avoid arbitrary JobHandle.Complete in visual sync.
Solution: Deformation and breach data use double GraphicsBuffer instances with LockBufferForWrite and `HullIntegrityMappedCopyJob.Run()` over unsafe mapped pointers. Deformation buffers are bound on the following frame via pending read index. The copy is a Burst job executed synchronously inside the map/unmap window, without scheduling a job only to immediately Complete it.
Rejected Alternatives: GraphicsBuffer.SetData was rejected for sync stalls. Schedule().Complete on a tiny copy job was rejected because it violates dependency chaining and adds scheduler overhead to a memory copy. Raw direct MemCpy was rejected in the polish pass because the XML explicitly requested a Burst copy job.
Scalability potential: Low copies 1-4 states; Ultra copies up to 256 states. Upload time is recorded in telemetry.
Hardware Impact: Expected low-end gain is 80-400 us versus SetData/stalled upload paths; exact number pending profiler proof.

Problem: Breaches require cinematic water jets without ParticleSystem/GameObject overhead.
Solution: BuildBreachJetsJob packs BreachJetDTO and an indirect args DTO. Hecton_LeakPlume.shader reads the breach buffer and Graphics.DrawProceduralIndirect renders quads from breach coordinates.
Rejected Alternatives: Unity ParticleSystem, instantiated leak prefabs, and CPU mesh generation were rejected because they allocate or force main-thread transform work.
Scalability potential: Low produces few/no jets based on dent threshold and quality-scaled intensity; Ultra permits dense breach presentation under the same draw path.
Hardware Impact: Expected i3/MX350 gain is 250-1100 us under catastrophic leaks compared to managed particle prefabs.

Problem: Breach billboards initially used Camera.main, which is a scene search shortcut forbidden in hot paths.
Solution: Added a serialized breach jet camera override and cached GlobalRegistry.Player.PlayerCamera fallback. If neither exists, breach jets use submarine local right/up axes.
Rejected Alternatives: Camera.main and scene-wide camera discovery were rejected because they hide lookup cost and violate hot-path rules.
Scalability potential: Low and Ultra share the same camera basis cache; quality only changes jet count/intensity.
Hardware Impact: Removes an avoidable camera lookup from the active breach-jet draw path.

Problem: Pressure buckling must communicate abyssal crush without a rigidbody deformation simulation.
Solution: ApplyPressureBucklingJob reads ExternalPressure01 if present, falls back to integrity ledger pressure/SIP, and synthesizes deterministic wide-radius pressure dents across hull faces.
Rejected Alternatives: Runtime constraints, physical frame bending, or per-vertex CPU deformation were rejected as expensive and gameplay-authoritative.
Scalability potential: Low creates one broad buckle; Ultra creates up to eight pressure dents with larger visual footprint.
Hardware Impact: Expected gain is avoiding hundreds of micro-constraints or mesh edits; exact microseconds pending profiler.

Problem: Designers need tuning without recompiles and without GC-heavy CSV parsing.
Solution: Hull Deformation Tuner writes unmanaged tuning DTOs. Cold CSV ingestion reads hull_material_strengths.csv into Vault scratch bytes, parses ReadOnlySpan<byte>, hashes material names, and writes HullMaterialStrengthDTO rows. AccumulateHullDamageJob now reads the material-strength NativeArray and applies matching material/damage hashes to impact plasticity and max dent depth.
Rejected Alternatives: string.Split, LINQ, ScriptableObject-only constants, hard-coded material strengths, and parsed-but-unused CSV rows were rejected because they allocate, require recompiles, or fake designer control.
Scalability potential: Low-to-Ultra behavior can be changed by CSV/tuner constants while the runtime math remains continuous.
Hardware Impact: Cold-path gain only; removes avoidable GC spikes during material strength load.

Problem: Task 20 required an OnDrawGizmos hook, not only an EditorWindow SceneView overlay.
Solution: Added a runtime OnDrawGizmos method guarded by UNITY_EDITOR that reads Vault deformation states and draws color-coded wire spheres/normal stubs through UnityEngine.Gizmos. The UI Toolkit editor overlay remains as a richer facade.
Rejected Alternatives: Debug GameObjects or prefab markers were rejected because they pollute runtime hierarchy and can allocate.
Scalability potential: Editor-only visualization reads the same packed deformation state across low through ultra.
Hardware Impact: No player runtime cost; editor-only diagnostic cost is proportional to active visible dents.

Problem: Visual-only state still needs black-box forensic proof.
Solution: Added 300-frame DeformationTelemetryEntry ring with active dents, discarded impacts, breach jets, upload microseconds, quality, last dent position, and hash. Fault or first capacity saturation dumps Docs/AgentLogs/Dump_DEFORMATION_SCULPTOR.bin; saturation uses a bounded fault flag to avoid per-frame disk spam after the first overflow.
Rejected Alternatives: Debug.Log-only reporting, unbounded managed history, and repeated dump writes after a persistent saturation flag were rejected because they lose crash context, allocate, or stall cold diagnostics.
Scalability potential: Fixed 64 B entries keep telemetry cost constant on low and ultra hardware.
Hardware Impact: Cost is bounded NativeArray writes; benefit is post-fault diagnosis without rerunning endurance cases blindly.

Problem: Persistent memory ownership must remain mostly Vault-owned while XML requires a NativeQueue impact accumulator.
Solution: Deformation states, mock impact scratch, telemetry, breach jets, args, material strengths, CSV scratch, and external pressure scalar are VaultBufferHandle-backed. The only local NativeQueue is a cold-prewarmed transient impact event lane because NativeQueue itself is required by the assignment and is not a rollback/gameplay state owner.
Rejected Alternatives: Private persistent NativeArray/NativeList storage was rejected. Signal-only routing was rejected for this batch because Task 06 explicitly required a NativeQueue accumulator.
Scalability potential: The queue only feeds packed Vault state; low quality discards over-capacity impacts early, ultra accepts more visual dents.
Hardware Impact: Prewarming avoids first-impact queue growth; remaining risk is NativeQueue lifecycle outside Vault, documented for integrator review.

Problem: A private managed CSV/dump byte buffer undermined the H-PHI claim that persistent domain data is Vault-owned.
Solution: Removed the runtime byte[] field. Integrity CSV, material-strength CSV, and telemetry dump paths now use the Vault CSV scratch buffer or direct native-pointer ReadOnlySpan<byte> writes; UInt32 dump headers use stackalloc Span<byte>.
Rejected Alternatives: Keeping the cold managed byte[] was rejected because final forensic claims must match source, even when the allocation is outside the gameplay hot path.
Scalability potential: Low-to-Ultra share one scratch lane; larger visual budgets do not create extra managed buffers.
Hardware Impact: Removes a permanent managed allocation from the component and keeps cold file I/O bounded to Vault scratch/native spans.

Problem: Earlier domain jobs lacked explicit aliasing proof on NativeArray fields, limiting Burst's ability to vectorize safely.
Solution: Added [NoAlias] to all NativeArray fields in HullIntegrityTypes jobs and to the AccumulateHullDamageJob impact NativeQueue; read-only fields keep [ReadOnly] [NoAlias]. AccumulateHullDamageJob, DecayDeformationJob, ApplyPressureBucklingJob, BuildBreachJetsJob, and ClearDeformationActiveFlagsJob now use NativeArrayUnsafeUtility pointers and UnsafeUtility.AsRef where they mutate DeformationStateDTO/BreachJetDTO state.
Rejected Alternatives: Leaving implicit alias analysis and NativeArray index copy/writeback in hot deformation loops was rejected because the mandate requires explicit pointer isolation and raw mutation proof.
Scalability potential: Low hardware benefits from fewer conservative memory dependencies; Ultra keeps heavier visual paths vectorizable.
Hardware Impact: Expected gain is small per job but cumulative under damage/pressure spikes; exact profiler proof remains pending compile/import.

Problem: The dent budget path still had a LowMx350 tier-name clamp, which is a binary hardware branch even if the rest of the math was continuous.
Solution: Removed the LowMx350 dent-budget cap and the health-critical tier-name assignment. Hardware degradation now enters through HomeostasisBrain.GlobalQualityWeight only; health critical/warning can still cap the scalar quality because it is a live system-pressure signal, not a fixed hardware tier. Dent capacity and shader active limit use polynomial curves gated with math.step.
Rejected Alternatives: Keeping tier-specific clamps was rejected because the same device can thermally move across the continuum and must not pop between hard-coded modes.
Scalability potential: Weak, middle, high, and ultra hardware follow the same scalar curve; thermal adapters own the weight.
Hardware Impact: Reduces visual pop risk and keeps budget shedding proportional to the published global quality scalar.

Problem: HullDeformedSignal still emitted LowTierVisualOnlyFlag from a cached tier equality check, leaving a binary quality artifact outside the core math path.
Solution: Removed the flag emission. The signal still carries QualityTier as legacy/telemetry metadata, but deformation behavior and event flags no longer branch on it.
Rejected Alternatives: Keeping the flag was rejected because downstream systems could treat it as a hard visual-mode switch.
Scalability potential: Low, middle, high, and ultra presentation now consume the same deformation facts and differ through GlobalQualityWeight-driven budgets.
Hardware Impact: Removes a visual-pop route and preserves continuous quality degradation; direct microsecond gain is negligible.

Problem: Removing `using Hecton8.World` left unqualified `AbsoluteUniversePosition` references in HullIntegrityRuntime, creating a compile-risk while trying to defend the compile wall.
Solution: Kept the namespace import removed and fully qualified the remaining AUP type as `global::Hecton8.World.AbsoluteUniversePosition`. `HectonFloatingOrigin` remains routed through the existing Core reference.
Rejected Alternatives: Restoring a broad `using Hecton8.World` was rejected because it hides the concrete world-type touchpoint at the top of the file.
Scalability potential: No runtime scalability change; this is compile-wall clarity.
Hardware Impact: No frame-time change; prevents a compiler failure before profiling can happen.

Problem: Legacy `HullDentDTO` presentation and new `DeformationStateDTO` presentation shared `CounterActiveDentCount`, violating one fact -> one owner and risking wrong shader upload counts.
Solution: Added `CounterActiveDeformationCount = 13`. Only the new deformation accumulation, pressure buckling, decay, breach extraction, deformation telemetry, gizmo, and deformation GPU upload paths use it. Legacy dent DTO upload and legacy dent repair/crush remain on `CounterActiveDentCount`.
Rejected Alternatives: Reusing the legacy counter was rejected because two buffers with different lifetimes and capacities would silently corrupt each other's active-count fact.
Scalability potential: Low-to-ultra deformation budgets now scale independently from the legacy dent DTO bridge, so shader active limits cannot be polluted by older dent paths.
Hardware Impact: Prevents over-uploading/over-evaluating junk deformation states; expected gain depends on legacy dent traffic and remains pending profiler proof.

Problem: SHINOBU deformation shader helpers still contained local `_MATH_LOD_LOW` branches, which contradicted the requirement that this domain collapse through continuous GlobalQualityWeight rather than hard low/high shader logic.
Solution: Removed the local binary branches from `H8UberNoirEvaluateDeformationNormalBiasOS`, `H8UberNoirApplyHullDentsOS`, and `H8UberNoirEvaluateHullDentScarOS`. The collapse now comes from CPU-side active count, shader count params, `step`, and `lerp`.
Rejected Alternatives: Keeping the macro branch was rejected because it made normal/scar/deformation behavior depend on a compile-time mode instead of the live quality scalar.
Scalability potential: Low devices still evaluate a small active dent window; middle/high/ultra expand smoothly without changing shader variant behavior for this feature.
Hardware Impact: Low-tier shader may do a tiny amount more arithmetic than the macro-zero path, but avoids visual popping and keeps the load bounded by 4 active deformation states.
