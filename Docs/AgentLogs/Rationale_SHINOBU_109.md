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
Solution: Added a serialized breach jet camera override and cold-cached GlobalRegistry.Player.PlayerCamera fallback. The render path now only reads the override or cached camera; if neither exists, breach jets use submarine local right/up axes.
Rejected Alternatives: Camera.main, scene-wide camera discovery, and hot-path GlobalRegistry fallback were rejected because they hide lookup cost and violate hot-path authority rules.
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

Problem: Persistent memory ownership must be Vault-owned. The first implementation kept a private `NativeQueue<HullImpactDTO>` because the XML asked for a NativeQueue accumulator, but that exception weakened H-PHI and introduced a separate native allocator owner.
Solution: Replaced the private NativeQueue with Vault buffer `70099 PendingVisualImpacts` plus `CounterPendingVisualImpactCount`. `HullImpactScratch` remains a separate mock-generation buffer, so editor stress injection cannot overwrite pending production impacts. `AccumulateHullDamageJob` now drains the Vault-owned pending ring by raw read-only pointer, resets the pending counter, and writes packed `DeformationStateDTO` state.
Rejected Alternatives: Keeping the NativeQueue exception was rejected after the polish audit because the H-PHI law is stronger than a convenience container. Signal-only routing was rejected because the batch needs a deterministic fixed-capacity accumulation surface and materialized GPU upload proof.
Scalability potential: The pending ring remains fixed-capacity, low quality discards over-capacity impacts early, and ultra accepts more visual dents without an extra native allocator.
Hardware Impact: Removes one persistent allocator/lifecycle surface and avoids first-impact NativeQueue growth behavior. Direct frame gain is small; memory ownership proof is materially stronger.

Problem: A private managed CSV/dump byte buffer undermined the H-PHI claim that persistent domain data is Vault-owned.
Solution: Removed the runtime byte[] field. Integrity CSV, material-strength CSV, and telemetry dump paths now use the Vault CSV scratch buffer or direct native-pointer ReadOnlySpan<byte> writes; UInt32 dump headers use stackalloc Span<byte>.
Rejected Alternatives: Keeping the cold managed byte[] was rejected because final forensic claims must match source, even when the allocation is outside the gameplay hot path.
Scalability potential: Low-to-Ultra share one scratch lane; larger visual budgets do not create extra managed buffers.
Hardware Impact: Removes a permanent managed allocation from the component and keeps cold file I/O bounded to Vault scratch/native spans.

Problem: Earlier domain jobs lacked explicit aliasing proof on NativeArray fields, limiting Burst's ability to vectorize safely.
Solution: Added [NoAlias] to all NativeArray fields in HullIntegrityTypes jobs. Read-only fields keep [ReadOnly] [NoAlias], including the Vault-owned pending impact ring. AccumulateHullDamageJob, DecayDeformationJob, ApplyPressureBucklingJob, BuildBreachJetsJob, and ClearDeformationActiveFlagsJob now use NativeArrayUnsafeUtility pointers and UnsafeUtility.AsRef where they mutate DeformationStateDTO/BreachJetDTO state.
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

Problem: Guarded solution build finally passed the CPU gate but failed before SHINOBU verification on a missing Core source file.
Solution: Classified the failure as a dependency compile wall: `Hecton8.Core.csproj` references `Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`, `Test-Path` confirms the file is absent, and `rg` finds the symbol only in the Core project include. SHINOBU keeps the exact compiler error in status/log and does not patch `World` or Core project ownership while unrelated World files are already modified.
Rejected Alternatives: Removing the Core `<Compile Include>` or fabricating a stub file was rejected because that would edit a sibling domain with active unrelated changes and could hide another agent's integration failure.
Scalability potential: No runtime scalability change; this preserves compile-wall accountability and prevents SHINOBU from turning a dependency break into a cross-domain refactor.
Hardware Impact: No frame-time gain. The value is build determinism: the current verification blocker is objective and outside the deformation code path.

Problem: Manual source audit found legacy capacity names and dent-cap hysteresis still listening to a tier-profile byte. The math used GlobalQualityWeight, but the profile byte could still force a GPU upload and make future readers treat tier as behavior.
Solution: Renamed dent capacity extrema to `MinTrackedDentCapacity` and `MaxTrackedDentCapacity`, removed `_cachedQualityTier` and `_pendingQualityTier` from the dent quality state machine, and kept `ScalabilityChangedEvent.CurrentTier` only as compatibility metadata for existing signals/shader params. Capacity and shader dent limits now change only through `GlobalQualityWeight`, `VisualOverkillLimit`, and live health-pressure clamps.
Rejected Alternatives: Leaving tier names as "legacy but harmless" was rejected because they encode the exact low/ultra dichotomy the mandate forbids and invite future binary branches.
Scalability potential: Weak, middle, high, and ultra machines all move along the same scalar curve; profile metadata no longer changes deformation behavior or upload cadence.
Hardware Impact: Direct microsecond gain is negligible; architectural gain is removal of a visual-pop route and a false shader-upload invalidation path.

Problem: The deformation normal-bias shader used `max(_HectonDeformationStateParams.w, H8UberNoirGlobalQualityWeight())`. If the runtime clamps SHINOBU's effective quality down for health/thermal pressure while the broader global remains higher, the shader would ignore the clamp for normal overkill.
Solution: `H8UberNoirEvaluateDeformationNormalBiasOS` now uses `_HectonDeformationStateParams.w` directly when finite and falls back to `H8UberNoirGlobalQualityWeight()` only if the deformation param is invalid.
Rejected Alternatives: Keeping `max()` was rejected because it creates a one-way upscale path and violates proportional load shedding.
Scalability potential: Weak devices and critical-health frames now shed deformation normal ALU in line with the same scalar that limits active dents; strong hardware still receives full normal bias when the effective weight is high.
Hardware Impact: Expected low-end shader ALU reduction under thermal/health clamp; exact GPU microseconds remain pending Frame Debugger/profiler proof.

Problem: Discarded-impact telemetry used raw increments. Long endurance runs with repeated over-capacity impacts could overflow the counter and corrupt black-box interpretation.
Solution: Added saturating discard accounting in `AccumulateHullDamageJob` and `EnqueueVisualImpact`, capped at `0x3FFFFFFF`.
Rejected Alternatives: Resetting discard count every frame was rejected because QA needs cumulative capacity-pressure evidence. Letting signed int wrap was rejected because it creates false negative telemetry.
Scalability potential: Low-quality frames that intentionally shed impacts retain bounded forensic pressure data; high-quality frames still report rare overflow events.
Hardware Impact: One branch per discarded impact only; no cost on the common accepted-impact path.

Problem: Compile verification status changed after recheck. The previous missing-file include in `Hecton8.Core.csproj` is no longer present, but a new build cannot be launched while another dotnet process is active.
Solution: Rechecked the stale Core include, confirmed no current `HectonMapMagicVegetationBridgeFloraCollisionProxies` reference in `Hecton8.Core.csproj`, then attempted the guarded build gate. The gate skipped before launch because `dotnet` process Id 16624 is active.
Rejected Alternatives: Launching a competing `dotnet build` was rejected by AGENTS CPU/compiler-process rules.
Scalability potential: No runtime impact; this is verification hygiene.
Hardware Impact: Prevents developer-machine contention and avoids compounding compile-wall latency.

Problem: The next compile gate had no active compiler process, but the workstation was under load.
Solution: Ran the guarded CPU gate and stopped before `dotnet build` because samples were `100, 86.5, 100, 82.5, 38.5, 20.1, 71.6, 51.2, 9.1, 19.6`; the AGENTS rule forbids launching when CPU exceeds 50%. While blocked, performed cheap static scans over the SHINOBU runtime/types/editor/shader paths.
Rejected Alternatives: Starting a build during CPU spikes was rejected because it would violate the hardware-protection mandate and produce noisy compile-wall evidence.
Scalability potential: No runtime scalability change; this preserves verification discipline while keeping the source proof current.
Hardware Impact: Avoids stealing CPU from active developer/agent work; no frame-time claim is made.

Problem: A cooldown retry still showed CPU spikes even without an active compiler process.
Solution: Waited 20 seconds, sampled CPU again, and stopped before build because samples were `100, 99.1, 26.8, 15.3, 16.4, 38.4, 26.3, 48.4, 98.9, 44`. The skip is an environment gate, not a source compile result.
Rejected Alternatives: Retrying until a quiet sample appeared was rejected because repeated polling/build launches would fight the concurrent-agent workload.
Scalability potential: No runtime impact; keeps compile proof honest and non-destructive.
Hardware Impact: Protects the developer workstation from forced compile contention.

Problem: The compile-wall proof needed actual asmdef evidence, not only using-statement claims.
Solution: Read `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef`. It references `Hecton8.Bootstrap.Contracts`, `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Habitat.Deformation.Contracts`, and Unity package assemblies. No `Hecton8.World` or sibling runtime domain reference is present.
Rejected Alternatives: Inferring assembly routing from C# namespaces was rejected because asmdef references are the compile-wall truth.
Scalability potential: No runtime quality change; prevents future deformation work from gaining hidden sibling rebuild dependencies.
Hardware Impact: No frame-time gain; protects iteration latency by keeping SHINOBU routing explicit.

Problem: Two additional build windows still violated the AGENTS hardware gate before this handoff.
Solution: Recorded the skipped CPU samples exactly: `31.2, 22.7, 44.4, 76.4, 32.8, 17.8, 38.4, 44.3, 18.4, 100` and `52.3, 95.3, 94.9, 100, 100, 93.7, 100, 100, 100, 100`. No build was launched.
Rejected Alternatives: Treating CPU-gated skips as compiler proof was rejected because it would turn environment pressure into a false source verdict.
Scalability potential: No runtime scalability change.
Hardware Impact: Avoided forced compile contention on a loaded workstation.

Problem: The latest guarded build attempt found an active compiler process again.
Solution: Stopped before `dotnet build` because `dotnet` process Id 19164 was active from `C:\Program Files\dotnet\dotnet.exe`.
Rejected Alternatives: Launching a second build while another `dotnet` process is active was rejected by the explicit AGENTS rule.
Scalability potential: No runtime impact; verification remains STATIC_SOURCE.
Hardware Impact: Prevents compile-wall amplification from concurrent builds.

Problem: A final short retry found no active compiler process, but the workstation was still above the allowed CPU threshold.
Solution: Waited 20 seconds, sampled CPU, and stopped before build because samples were `100, 99.8, 100, 92.3, 97.7, 82.9, 75.3, 78.7, 100, 100`.
Rejected Alternatives: Forcing `dotnet build` under sustained >50% CPU was rejected by the explicit hardware-protection rule.
Scalability potential: No runtime impact; this is verification gating only.
Hardware Impact: Avoided adding compile pressure during a sustained CPU spike.
