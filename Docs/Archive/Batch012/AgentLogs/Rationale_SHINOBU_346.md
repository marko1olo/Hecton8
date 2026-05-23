# Rationale_SHINOBU_346

Status: POLISH PASS ACTIVE / PENDING EXTERNAL COMPILE WALL

## Decision 01: Existing Seismic Owner

Problem: The assignment forbids duplicate seismic managers and requires integration with existing environment authority.
Solution: Reuse `HectonSeismicTideDirector` as the owner. It already owns seismic/tide vault buffers, SignalBus prewarm, editor tuner, and blackbox dump paths.
Rejected Alternatives: A new `HectonSeismicManager` would duplicate authority, create merge conflicts, and force consumers to choose between two seismic facts.
Scalability potential: Low keeps one bounded owner and one signal route; Middle/High/Ultra add richer signal fields and visual consumers without additional managers.
Hardware Impact: Removes manager fan-out and scene-search risk; estimated low-end i3/MX350 gain is 20-80 us per event dispatch path before profiler proof.

## Decision 02: AUP Signal Expansion Instead Of New Event Name

Problem: Current `SeismicSignal` is a 32B presentation packet without epicenter/radius, while the task requires bases/boats to compute their own stress from a `double3` epicenter.
Solution: Expand `SeismicSignal` in the existing typed lane while preserving existing fields by name. Emit AUP, magnitude, P/S radii, frame, source hash, and flags so structural/vehicle consumers can calculate distance locally.
Rejected Alternatives: A new `EarthquakeEventSignal` fragments the lane; only `SeismicShockwaveSignal` would not satisfy the explicit assignment and leaves legacy consumers on non-AUP `SeismicSignal`.
Scalability potential: Low consumers can use scalar radius/intensity only; Middle can evaluate P/S attenuation; High/Ultra can consume phase/noise for visual overkill without changing gameplay truth.
Hardware Impact: +64B payload expansion per bounded event, but removes PhysX broadphase lock. Expected net gain on i3/MX350 for 2 km blast route: 200-800 us and 0 B GC pending runtime proof.

## Decision 03: Visual Fake First

Problem: Earthquakes are often implemented as object overlap plus force application, which mutates many objects and synchronizes transforms.
Solution: Authoritative route is pure math: one epicenter fact, SignalBus broadcast, each owner computes local stress with double precision subtraction before float attenuation.
Rejected Alternatives: `Physics.OverlapSphere`, `Rigidbody.AddExplosionForce`, `Camera.main` shake, or coroutine noise. They are non-deterministic, broadphase-bound, and not AUP-safe.
Scalability potential: Low updates fewer times with cached radius; Middle uses normal cadence; High/Ultra spend saved CPU on VFX/audio/haptics, not more gameplay truth.
Hardware Impact: Expected low-end gain is proportional to affected collider count; for 100 colliders, avoids O(n) PhysX query and force calls. Static proof only until profiler.

## Decision 04: DTO Alignment

Problem: Existing `SeismicEventDTO` is 40B and violates the assignment’s explicit 32B `double3 + float + uint` envelope.
Solution: Convert `SeismicEventDTO` to `[StructLayout(LayoutKind.Explicit, Size = 32)]` with `EpicenterAUP@0`, `MagnitudeRichter@24`, `EventTypeHash@28`; move wave time/radius state to a separate aligned `SeismicStateDTO`.
Rejected Alternatives: Keeping frequency/decay in the event DTO preserves old convenience but fails ARM64 layout mandate and task contract.
Scalability potential: Low event scan stays compact; Middle/High/Ultra keep extra wave state in a separate row only for active slots.
Hardware Impact: Event scan stride shrinks from 40B to 32B, reducing L1 fetch pressure by 20% for the primary event array.

## Decision 05: Blackbox Dump Path

Problem: Existing constants reference `Dump_SHINOBU_129.bin` and generic seismic dump names, not the assigned SHINOBU_346 forensic path.
Solution: Add/route SHINOBU_346 dump/report paths for this task’s blackbox and self-audit output.
Rejected Alternatives: Reusing older agent dump names would hide ownership and fail postmortem traceability.
Scalability potential: Same 300-frame ring for all tiers; higher tiers may retain richer visual-only consumers outside the truth DTO.
Hardware Impact: 300 x 64B telemetry remains 19.2 KB, negligible on MX350/i3.

## Decision 06: P/S Wave State Split

Problem: The event contract must remain a 32B AUP envelope, but wave propagation needs birth time, P radius, S radius, cadence, and decay state.
Solution: Store immutable rupture facts in `SeismicEventDTO` and mutable propagation facts in `SeismicStateDTO`; the Burst job advances both P and S radii from `H8TimeSeconds - BirthTimeSeconds`.
Rejected Alternatives: Packing radii/frequency back into `SeismicEventDTO` violates the explicit ARM64 layout. Direct object forces violate the SignalBus authority route.
Scalability potential: Low uses wider bands and lower cadence; Middle uses normal P/S radii; High uses narrower bands; Ultra consumers can add richer visual displacement from the same signal without changing truth.
Hardware Impact: 16 active slots cost fixed pointer traversal; removes PhysX broadphase/object mutation, estimated i3/MX350 gain 200-800 us for 2 km events before profiler proof.

## Decision 07: Dear Lie Tide Scalar

Problem: Moon-driven tides need to reach ocean rendering without moving terrain, water meshes, or buoyancy objects from the seismic owner.
Solution: Write one double scalar to `WaterSurfaceAupYBuffer`, derived from global tide height plus `TideVector.y`; ocean/buoyancy consumers can read the scalar.
Rejected Alternatives: Mesh deformation, scene object transforms, or per-water-body updates create broad synchronization work and duplicate ownership.
Scalability potential: Low/Middle/High/Ultra share the same scalar truth; higher tiers spend GPU/render cycles on foam and shoreline overkill.
Hardware Impact: One 8B vault write replaces potential mesh updates; expected hot-path cost under 5 us on low-end silicon.

## Decision 08: Structural Stress Route

Problem: Bases and boats need quake stress, but the environment domain must not own their damage mutation.
Solution: Emit `SeismicSignal` with `double3 EpicenterAUP`, P/S/current radii, magnitude, frame, source hash, and event hash. Structural and vehicle systems subtract their own AUP in double and compute stress locally.
Rejected Alternatives: `CombatDamageSignal` or module scans from the environment owner create cross-domain authority leaks and O(n) fan-out.
Scalability potential: Low consumers can evaluate inverse-square only; Middle/High/Ultra can add phase/noise visual displacement.
Hardware Impact: Replaces direct per-module mutation with one unmanaged signal per active rupture; expected low-end savings scale with module count.

## Decision 09: Continuous Cadence

Problem: Full seismic propagation cadence at 60 Hz is wasteful under thermal pressure and unnecessary for far-field visual waves.
Solution: Gate `ScheduleSeismicEvaluation` with `math.lerp(0.016f, 0.1f, 1f - GlobalQualityWeight)` and keep wave truth derived from absolute birth time, not accumulated per-tick distance.
Rejected Alternatives: Binary low-tier/high-tier switches and accumulated radius deltas; both create discontinuities or drift.
Scalability potential: Low gets chunkier radius updates; Middle stays near normal cadence; High/Ultra can update closer to frame rate while consumers use the same DTO layout.
Hardware Impact: Skipped evaluations save the full fixed slot scan and noise phase work, estimated 20-80 us on i3/MX350 under active quakes.

## Decision 10: Fault Profile CSV Bridge

Problem: Designer fault limits need a cold authoring route, but `tectonic_fault_profiles.csv` is absent in this workspace.
Solution: Add `SeismicFaultProfileDTO[16]`, byte scratch, and a cursor parser for `zone,min,max,frequency,radius,decay`; missing CSV seeds one deterministic emergency profile.
Rejected Alternatives: `string.Split`, `float.Parse`, managed dictionaries, or claiming final Data Monolith readiness.
Scalability potential: Low/Middle can use broad profiles and lower magnitudes; High/Ultra can author larger radius and Richter caps without changing runtime ABI.
Hardware Impact: Cold boot only; runtime hot path pays no CSV/string cost.

## Decision 11: Telemetry Ring Contents

Problem: The black box must prove what the quake solver was doing before a crash or slow frame, including tide and wave radius.
Solution: Keep `SeismicTelemetryEntry` at 64B and store active count, max magnitude, max wave radius, tide offset, propagation compute time, translation, hash, and flags.
Rejected Alternatives: Managed log strings, chat reports, or dumping only celestial telemetry.
Scalability potential: Same 300-frame ring at all tiers; high-tier visual consumers can add separate telemetry without changing gameplay truth.
Hardware Impact: 300 x 64B equals 19.2 KB; dump occurs only on non-finite/slow fault path.

## Decision 12: Editor Proof Facade

Problem: Designers need to see and tune cataclysms without modifying C# or shaking the runtime camera to prove a wave exists.
Solution: Use the existing UI Toolkit editor window route, rename the menu to `Cataclysmic Event Tuner`, add wave/richter sliders, and draw SceneView wave radii from vault state.
Rejected Alternatives: Runtime debug GameObjects, LineRenderers, or transform-driven camera shake. These pollute gameplay authority and create object churn.
Scalability potential: Editor facade is tier-neutral; runtime quality still comes from continuous `GlobalQualityWeight`.
Hardware Impact: Editor-only. Runtime hot path unchanged.

## Decision 13: OOP Explosion Scanner

Problem: The task requires proof that Environment/Events seismic paths no longer call broadphase/explosion APIs.
Solution: Add cold `Tools/OOP_Explosion_Scanner.py`, scan Environment/Events C# files, and merge `OOP Seismic Forces Eradicated` into physics reports.
Rejected Alternatives: Manual claims or broad whole-project deletion. Other domains may still own valid NonAlloc proximity routes outside seismic authority.
Scalability potential: Static proof supports all hardware tiers because runtime route stays one SignalBus packet per active rupture.
Hardware Impact: Cold tooling only; no runtime cost.

## Decision 14: Compile Guard Honored

Problem: A build would violate project guard rules when CPU is busy or Unity compiler processes are active.
Solution: CPU sampled `57.9006266182999%` and Unity `dotnet.exe` PID `25560` was active, so no dotnet build was launched.
Rejected Alternatives: Forcing a compile under load would violate the explicit hardware protection rule and contaminate other agents' runs.
Scalability potential: Protects shared workstation throughput; static proof remains available until a legal build window opens.
Hardware Impact: Avoided additional compiler CPU and disk pressure while Unity dotnet was already active.

## Decision 15: Remove Event Magnitude Alias

Problem: `SeismicEventDTO` carried an overlapping `Magnitude` alias at offset 24 after the initial conversion to `MagnitudeRichter`, which made the ABI look wider than the task contract even though the bytes overlapped.
Solution: Delete the alias and update event consumers/gizmos/injectors to use `MagnitudeRichter` only.
Rejected Alternatives: Keeping a convenience alias for old code. It preserves ambiguity in the exact 32B payload and weakens the self-audit.
Scalability potential: Low/Middle/High/Ultra all read the same compact event truth. Richer wave state remains in `SeismicStateDTO`.
Hardware Impact: Runtime stride unchanged at 32B; audit/devirtualization risk reduced because one field name owns the magnitude fact.

## Decision 16: Delete Direct Combat Damage Fan-Out

Problem: A dead `PublishKineticImpactRoute` helper still encoded environment-owned base stress mutation through `CombatDamageSignal`.
Solution: Remove the helper. Seismic structural impact now routes only through `SeismicSignal`/`SeismicShockwaveSignal`; bases, boats, and habitat owners compute stress from their own AUP and authority state.
Rejected Alternatives: Leaving dead code because no caller existed. Dead direct routes are easy to reconnect during panic fixes and violate one fact -> one owner -> one route.
Scalability potential: Low uses one scalar/radius packet; Middle/High/Ultra add richer local stress and visual damage in their owner domains without bloating the seismic owner.
Hardware Impact: Prevents reintroduction of O(n) base-module fan-out and direct per-module writes. Expected low-end avoidance remains 200-800 us for large quakes before profiler proof.

## Decision 17: ReadOnlySpan Fault Profile Parser

Problem: Task 17 explicitly required `ReadOnlySpan<byte>` slicing, while the first parser read directly from `NativeArray<byte>`.
Solution: File bytes still land in Vault scratch, then a `ReadOnlySpan<byte>` wraps the raw scratch pointer for cold cursor parsing into `SeismicFaultProfileDTO[16]`.
Rejected Alternatives: `string.Split`, `float.Parse`, managed dictionaries, or leaving NativeArray-only parser semantics.
Scalability potential: Low/Middle/High/Ultra tuning stays data-driven without changing runtime ABI. Quality and magnitude caps remain continuous scalars.
Hardware Impact: Cold boot/editor only. Runtime hot path pays 0 us and 0 B GC for profile parsing.

## Decision 18: Editor Injection Without Event ClearMemory

Problem: The editor quake injector used `NativeArrayOptions.ClearMemory` for event/state lanes, violating the zero-init bypass spirit for the exact buffers SHINOBU owns.
Solution: Open existing buffers first; if absent, acquire with `UninitializedMemory` and overwrite deterministic defaults with explicit loops before scanning for a free slot.
Rejected Alternatives: Paying zero-fill for convenience or reading uninitialized rows.
Scalability potential: Editor-only; the runtime event/state route remains unchanged and deterministic.
Hardware Impact: Cold/editor saving estimated 5-25 us depending on allocator state; no runtime cost.

## Decision 19: Build Guard Recheck

Problem: After polish edits, compilation proof was still required but the hardware guard forbids build launch above 50% CPU.
Solution: Sampled CPU again: `96%`; no `dotnet`/`csc.exe` processes were active. Build remains blocked by CPU guard.
Rejected Alternatives: Forcing a compile under 96% CPU would violate the explicit command discipline rule and interfere with other agents.
Scalability potential: Protects shared workstation iteration throughput.
Hardware Impact: Avoided compiler CPU/IO pressure under overloaded host conditions.

## Decision 20: Add Roslyn Scanner Source Without Forcing Unity Execution

Problem: The CLI Python scanner provides immediate proof, but Task 19 explicitly asks for AST parsing. Calling it an AST scanner would be false.
Solution: Add `Assets/_Project/Scripts/Environment/Editor/OOP_Explosion_Scanner.cs`, a scoped Editor-only Roslyn `CSharpSyntaxTree` invocation scanner for `Rigidbody.AddExplosionForce` and `Physics.OverlapSphere`, while keeping the CLI scanner as the shell-runnable preflight.
Rejected Alternatives: Pretending the Python token scanner is Roslyn, or launching Unity/dotnet under a 74-96% CPU guard.
Scalability potential: Editor-only proof route; no runtime cost on Low/Middle/High/Ultra.
Hardware Impact: 0 us runtime. Unity menu execution and compile proof remain pending until CPU is below 50% and no compiler process is active.

## Decision 21: Roslyn Proof Must Upsert Shared Physics Report

Problem: The Roslyn scanner source originally wrote only `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_346_ROSLYN.json`, while Task 19 requires proof in `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.
Solution: Extend the Editor scanner with a scoped top-level JSON upsert for `SHINOBU_346_OOP_Explosion_Scanner_Roslyn` and reuse the same Roslyn report body for sidecar and shared report output.
Rejected Alternatives: Sidecar-only evidence, or claiming the Python preflight section is the AST proof. Both weaken the audit chain and make future CTO review depend on chat context.
Scalability potential: Editor-only proof path; Low/Middle/High/Ultra runtime remains the same one SignalBus packet per active rupture.
Hardware Impact: 0 us runtime. Cold editor report write only; no runtime allocations, no new runtime assembly dependency, no sibling domain reference.

## Decision 22: Widen Roslyn Invocation Detection Without Semantic Compile

Problem: A syntax-only scanner that only flags `Physics.OverlapSphere(...)` can miss a `using static UnityEngine.Physics; OverlapSphere(...)` invocation.
Solution: Detect `IdentifierNameSyntax` invocations named `OverlapSphere` in addition to member access syntax, and sort file paths before parsing for deterministic reports.
Rejected Alternatives: Adding a full Roslyn semantic compilation step. That would drag assembly reference resolution into a cold scanner that only needs to prove forbidden call shapes in scoped source.
Scalability potential: Editor-only proof path; runtime Low/Middle/High/Ultra behavior remains unchanged.
Hardware Impact: 0 us runtime. Cold scanner may do a few extra string checks only when manually executed from Unity.

## Decision 23: Namespace Context In Roslyn Findings

Problem: Task 19 scopes the AST scanner to Environment/Events namespaces, but the first Roslyn sidecar finding rows only emitted path/type/member context.
Solution: Add namespace resolution for block-scoped and file-scoped namespace declarations, and emit structured `forbiddenRuntimeApis` arrays in the Roslyn report proof block.
Rejected Alternatives: Path-only findings. They are weaker when files are moved or when a project contains multiple namespaces under the same folder.
Scalability potential: Editor-only proof path; runtime route and quality scaling are unchanged.
Hardware Impact: 0 us runtime. Cold scanner adds a parent-chain walk only for forbidden findings.

## Decision 24: Keep CLI Proof Metadata Synchronized With Roslyn Source

Problem: The shared Python preflight report still described the Roslyn companion as only `source-added`, omitting the later shared-report upsert and unqualified `OverlapSphere` detection.
Solution: Update the CLI report metadata so `PHYSICS_OPTIMIZATION_REPORT.json` points at the actual current Roslyn companion surface while still labeling CLI evidence as non-AST.
Rejected Alternatives: Leaving stale metadata and relying on chat history. The CTO reads report files, not transcript context.
Scalability potential: Static proof path only; runtime quality/cadence remains unchanged.
Hardware Impact: 0 us runtime. Cold report text update only.

## Decision 25: Add Explicit GenerateMockSeismicEventsJob

Problem: Task 06 explicitly names a Burst `GenerateMockSeismicEventsJob`, but the prior implementation relied on direct editor row mutation plus a narrative trigger job.
Solution: Add a deterministic Burst `IJob` that writes synthetic cataclysm events directly into `SeismicEventDTO` and `SeismicStateDTO` Vault rows through raw pointers, then route the editor test-event injector through `job.Run()`.
Rejected Alternatives: Keeping the direct row mutation and arguing it was equivalent. The job-name and IJob requirement were explicit in the XML task.
Scalability potential: Cold/editor proof path; Low/Middle/High/Ultra runtime wave cadence still scales through the existing `GlobalQualityWeight` curve.
Hardware Impact: 0 us runtime hot-path change. Cold injection uses one fixed 16-slot scan and no managed allocation.

## Decision 26: Match Task 07 Job Name Exactly

Problem: The XML requires `EvaluateSeismicPropagationJob`, while the implementation used the behaviorally correct but non-matching `SeismicEvaluationJob` name.
Solution: Rename the Burst job type to `EvaluateSeismicPropagationJob` and keep the existing `_seismicEvaluationJob` `JobHandle` field because that field describes the dispatcher fence, not the job type.
Rejected Alternatives: Adding a wrapper alias around the old job would create a second nested type and weaken static audit clarity without changing runtime behavior.
Scalability potential: No runtime scalability change; Low/Middle/High/Ultra still use the same continuous cadence and wave richness curves.
Hardware Impact: 0 us runtime. Static audit surface now matches the XML assignment and avoids reviewer ambiguity.

## Decision 27: Match Task 15 Telemetry Name Exactly

Problem: The XML requires a `SeismicTelemetryEntry` blackbox ring, but the code kept the older `SeismicDirectorTelemetryEntry` type name and `OscillatorComputeTimeMs` field.
Solution: Rename the DTO to `SeismicTelemetryEntry` and the timing field to `PropagationComputeTimeMs` while preserving every byte offset and the 64-byte stride.
Rejected Alternatives: Adding an alias struct would duplicate a telemetry ABI without ownership value. Keeping the legacy names would continue to obscure the propagation blackbox route.
Scalability potential: No runtime scalability change; all tiers still write the same fixed 300-row ring, and quality only changes cadence/richness.
Hardware Impact: 0 us runtime and no cache layout delta. Static audit now matches the XML language and the blackbox field describes the actual propagation job.

## Decision 28: Raw Seismic Blackbox Dump

Problem: The seismic telemetry dump still used `BinaryWriter` and per-field writes, which is weaker than the mandated raw 300-frame dump and creates an avoidable managed serialization surface on the fault path.
Solution: Add a fixed 32-byte `SeismicTelemetryDumpHeader` and write the ring as raw `ReadOnlySpan<byte>` slices directly from `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, preserving oldest-to-newest order with at most two payload writes.
Rejected Alternatives: Keeping per-field `BinaryWriter` for readability. The CTO reads binary forensic artifacts; field-wise managed serialization is slower and easier to drift from the 64-byte DTO.
Scalability potential: Low/Middle/High/Ultra all retain the same fixed 19.2 KB payload. Higher tiers can interpret the same raw rows with richer tooling without altering runtime truth.
Hardware Impact: Hot path 0 us. Fault path avoids 300 per-row writer loops and writes one 32B header plus 19.2 KB raw telemetry payload.

## Decision 29: Keep Water Surface AUP-Y In Double

Problem: The water-surface lane was `double[1]`, but the writer accepted `float` and cast `TideVector.y` to float before the write.
Solution: Change `WriteWaterSurfaceAupY` to accept `double` and pass `(double)tide.HeightMeters + environmentState.TideVector.y` directly into the Vault lane.
Rejected Alternatives: Keeping the float intermediate because tide height is visually small. The task explicitly calls this a double-precision AUP water-level scalar, and the route should not silently narrow `TideVector.y`.
Scalability potential: Low/Middle/High/Ultra use the same scalar truth; higher tiers spend visual budget in ocean rendering without changing the authority route.
Hardware Impact: Runtime cost unchanged: one 8-byte Vault write. Precision risk reduced for far-origin tide/water consumers.

## Decision 30: Split Seismic And Celestial Agent Dump Paths

Problem: The seismic fault dump wrote the generic seismic dump plus `Dump_SHINOBU_345.bin` through a shared `AgentDumpPath`, contradicting Task 15's required `Dump_SHINOBU_346.bin` artifact.
Solution: Add `SeismicAgentDumpPath` for `Dump_SHINOBU_346.bin` and `CelestialAgentDumpPath` for `Dump_SHINOBU_345.bin`; route seismic and celestial dumps to their own owner artifacts.
Rejected Alternatives: Renaming the shared constant to SHINOBU_346. That would steal the celestial route card's SHINOBU_345 dump artifact and create cross-agent forensic ambiguity.
Scalability potential: No runtime scalability change. Fault artifacts are now owner-specific across low/mid/high/ultra runs.
Hardware Impact: Hot path 0 us. Fault path writes the same raw seismic payload to the correct owner-specific file.

## Decision 31: SeismicSignal Truth Flag Split

Problem: `SeismicSignal` now carries both legacy presentation tremor fields and radial AUP shockwave truth. Relying only on nonzero magnitude/radius would be a weak contract for future base/boat stress consumers.
Solution: Add layout-neutral constants `FlagRadialWave=0x80`, `FlagPresentationOnly=0x40`, and `LegacyQualityMask=0x0F` to `SeismicSignal`. Propagation/spawn packets set the radial bit; legacy camera/audio/turbidity packets set the presentation bit and keep the quality nibble isolated.
Rejected Alternatives: Splitting a new signal lane would fragment the mandated existing `SeismicSignal` route. Leaving flags ambiguous risks accidental structural stress from presentation-only tremor packets.
Scalability potential: Low/Middle/High/Ultra share the same 96B packet. Quality remains a low-nibble presentation scalar and never changes truth ownership or DTO layout.
Hardware Impact: Runtime cost 0 us; no payload growth; consumer branch becomes one byte mask instead of heuristic checks.

## Decision 32: Build Guard Attempt Must Stay Factual

Problem: A legal-looking first guard sample (`cpu=43`, `compilerProcesses=0`) can become illegal before the compile process starts on a shared 20+ agent workstation.
Solution: Keep the `dotnet build Hecton8.Core.csproj --no-restore /m:1 /p:BuildInParallel=false` command behind a second in-command preflight guard. The second sample hit `cpu=62`, so no build launched.
Rejected Alternatives: Starting compile from the stale first sample, or broad-building every generated Unity project. Both violate command discipline and risk starving neighboring agents.
Scalability potential: Protects iteration throughput across all hardware tiers; runtime seismic quality behavior is unchanged.
Hardware Impact: Avoided compiler CPU/IO pressure under a host load spike; runtime delta 0 us.

## Decision 33: Guard Monitor Instead Of Forced Compile

Problem: The build proof is useful, but host load remained unstable after the first blocked attempt.
Solution: Run a bounded six-sample guard monitor and only launch `Hecton8.Core.csproj` if CPU fell to 50% or below with zero compiler processes. Samples stayed `100/88/74/94/90/100%`; no build launched.
Rejected Alternatives: Ignoring the 50% CPU rule, or keeping an unbounded watcher alive. Both create uncontrolled workstation pressure.
Scalability potential: No runtime change. Protects multi-agent iteration capacity.
Hardware Impact: Avoided starting MSBuild/csc on an overloaded host; runtime delta 0 us.

## Decision 34: Roslyn Scanner Type Compatibility

Problem: The editor-only scanner used `FileScopedNamespaceDeclarationSyntax`; older Unity/Roslyn package combinations can lack that syntax type even when basic `CSharpSyntaxTree` parsing is available.
Solution: Resolve file-scoped namespaces through `SyntaxNode.Kind().ToString()` plus a cold string slice fallback. The scanner keeps namespace context where possible without a hard compile-time dependency on that newer type.
Rejected Alternatives: Keeping the direct type reference because nearby scanners use it. Other agents' files are outside SHINOBU_346 authority; this scanner should minimize its own compile-risk surface.
Scalability potential: Editor-only proof path; runtime Low/Middle/High/Ultra wave behavior is unchanged.
Hardware Impact: 0 us runtime. Cold scanner only pays the fallback string parse on forbidden findings or namespace resolution.

## Decision 35: Isolate OOP Scanner In Editor Assembly

Problem: `OOP_Explosion_Scanner.cs` lives under the root `Assets/_Project/Scripts/Hecton8.Core.asmdef` tree. Without a child editor asmdef, the cold scanner can pull `UnityEditor` and Roslyn references into the runtime Core compile surface.
Solution: Add a local `Hecton8.Environment.Editor.asmdef` in `Environment/Editor` with `includePlatforms=["Editor"]`, Roslyn precompiled references, and no runtime assembly references.
Rejected Alternatives: Leaving the file under implicit root assembly routing, or referencing `Hecton8.Core` from the scanner. The scanner only needs filesystem/Roslyn proof and must not widen the runtime compile wall.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Editor proof stays isolated from player builds.
Hardware Impact: 0 us runtime; reduces rebuild surface and prevents editor-only API leakage into runtime compilation.

## Decision 36: Do Not Patch External Hatch Compile Wall

Problem: The guarded `Hecton8.Core.csproj` compile failed, but diagnostics are `CS0234 Hecton8.Habitat` in `Assets/_Project/Scripts/Construction/HatchLockJobs.cs` and `BulkheadContainmentRuntime_HatchLocks.cs`, which are outside SHINOBU_346 and currently untracked.
Solution: Treat as an external compile wall and preserve the exact failure in the log. Continue SHINOBU_346 static verification instead of editing Construction/Habitat ownership.
Rejected Alternatives: Patching another agent's untracked Construction files or changing Habitat namespace/sourcegraph from the seismic domain. That would violate domain boundary and risk corrupting another agent's route.
Scalability potential: Runtime seismic route unchanged; compile-wall evidence remains factual for integrator handoff.
Hardware Impact: 0 us runtime. Avoids unscoped rebuild churn and cross-domain merge risk.

## Decision 37: Stackalloc Legacy Fault Binary Staging

Problem: The legacy `.h8bin` fault importer used managed `byte[]` staging for a 16B header and 40B record. It is cold, but it weakens the zero-GC and binary hydration audit.
Solution: Use `stackalloc Span<byte>` for header/record staging and change endian readers to `ReadOnlySpan<byte>`.
Rejected Alternatives: Leaving cold managed arrays with comments. The cost is small, but the fix is local and removes a needless managed allocation path.
Scalability potential: Runtime wave cadence and quality tiers unchanged; cold data import remains deterministic.
Hardware Impact: Runtime 0 us. Cold import avoids two managed allocations and keeps `.h8bin` hydration on stack memory.

## Decision 38: Helper Enforces Radial Truth Bit

Problem: The shared consumer helper could calculate displacement for any `SeismicSignal` if a future structural consumer forgot to check the radial/presentation flag contract.
Solution: Add an early return in `SeismicWaveMath.CalculateSeismicDisplacement` unless `SeismicSignal.FlagRadialWave` is set.
Rejected Alternatives: Relying on documentation only. The byte mask is cheaper and protects all consumers that use the helper.
Scalability potential: Runtime quality behavior unchanged; Low/Middle/High/Ultra all share the same truth mask.
Hardware Impact: One byte mask branch in helper; prevents accidental structural work on presentation-only packets.

## Decision 39: Sanitize Shared Displacement Helper Inputs

Problem: `SeismicWaveMath.CalculateSeismicDisplacement` is the shared base/boat stress helper, but it still trusted every radial packet field after the flag guard and recomputed distance length twice.
Solution: Cache `distanceSq`, derive `distance` from one `rsqrt`, sanitize current/P/S radius, magnitude, P/S amplitudes, and intensity before wavefront math, and return zero if the final displacement vector is non-finite.
Rejected Alternatives: Leaving sanitization to each consumer. That duplicates branch policy in Habitat/Vehicles and lets one forgotten caller reintroduce NaN propagation from a malformed signal.
Scalability potential: Low devices skip duplicate ALU and collapse malformed packets to zero. Middle/High/Ultra retain the same continuous quality curve and noise richness without expanding DTO layout or truth ownership.
Hardware Impact: Saves one duplicate `lengthsq` path per helper call and avoids downstream NaN recovery cost. Runtime win is small per call but scales with every base/boat stress sample near an active rupture.

## Decision 40: Field-Local Queue Writer Safety Proof

Problem: `EvaluateSeismicPropagationJob` had one shared safety justification for two `NativeDisableContainerSafetyRestriction` queue writers, and the text named only the shockwave lane.
Solution: Add immediate three-paragraph justifications for both `SeismicWriter` and `ShockwaveWriter`, explicitly naming each producer-only queue lane, rejected alternatives, and the dispatcher fence invariant.
Rejected Alternatives: Keeping one generic comment. It is easy for a reviewer or future patch to miss that the typed seismic lane and compatibility shockwave lane have separate ownership semantics.
Scalability potential: Runtime quality behavior is unchanged. The proof protects the same low/middle/high/ultra SignalBus route from future unsafe same-frame readback or catch-all queue drift.
Hardware Impact: Runtime delta 0 us; review/safety evidence improved without touching packet layout or queue behavior.

## Decision 41: Producer-Side ParallelWriter Payload Vaccine

Problem: `SignalBus<T>.TryPush` applies generic finite guards, but `EvaluateSeismicPropagationJob` uses `NativeQueue<T>.ParallelWriter.Enqueue` for the Burst path, so the producer must prove payload safety before queue insertion.
Solution: Add `TryFinalizeSeismicSignal` and `TryFinalizeShockwaveSignal` inside the propagation job. The helpers reject non-finite `double3` epicenters, clamp scalar amplitudes/radii/intensity to finite ranges, normalize direction, sanitize raw frequency bits in `Reserved0`, enforce `SeismicSignal.FlagRadialWave`, and drop packets below the minimum magnitude threshold.
Rejected Alternatives: Patching core `SignalBus<T>` or adding a post-queue scrub pass. Core changes widen the compile surface for every domain; post-queue scrubbing adds another pass and risks same-frame readback pressure.
Scalability potential: Low-tier devices avoid downstream consumer NaN handling and keep the cheaper cadence. Middle/High/Ultra retain the same rich P/S signal fields and can spend saved recovery cost on visual shockwave consumers without changing authority or DTO layout.
Hardware Impact: Adds a bounded set of scalar finite clamps per active rupture before enqueue; avoids cross-domain NaN recovery and potential blackbox dump churn. Expected net is neutral-to-positive on i3/MX350 because active rupture count is capped at 16.

## Decision 42: Core Seismic Signal Guard Closure

Problem: Side audit found `SignalPayloadFiniteGuards` fell through to `GuardNone` for `SeismicSignal` and `SeismicShockwaveSignal`, while `GlobalSignals.Publish(in SeismicSignal)` cached `_latestSeismicSignal` before any sanitizer. That left a bad legacy publish able to reach latest-cache consumers even if frame flush later repaired or dropped the queue packet.
Solution: Add explicit seismic guard codes/kinds and fixed DTO sanitizers in `GlobalSignals.cs`; sanitize `SeismicSignal` before latest-cache assignment and before the typed bus push. The sanitizer finite-gates direction, intensity, jitter, audio, thermal scalar, epicenter `double3`, radii, magnitude, P/S amplitudes, and raw frequency bits in `Reserved0`.
Rejected Alternatives: A generic byte-wise finite scanner or reflection pass. Both are wrong for explicit unmanaged DTOs and would not know which `uint` fields are raw float payloads. A post-queue scrub pass was also rejected because it adds another iteration and leaves latest-cache reads exposed.
Scalability potential: Low-tier and overloaded devices drop or repair malformed seismic payloads before downstream stress/camera/VFX consumers burn ALU on recovery. Middle/High/Ultra keep the same 96B/64B payload layout and can continue using richer P/S signal fields.
Hardware Impact: A few scalar finite checks on seismic ingress; active rupture count is capped and latest-cache publish is cold/main-thread. Avoids expensive downstream NaN propagation, blackbox dump churn, and structural consumer fault handling.
