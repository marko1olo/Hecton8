# Rationale_SHINOBU_119

Status: POLISH STATIC PASS R3 / BUILD BLOCKED BY EXTERNAL STALE WORLD SOURCE INCLUDE

## Initial Domain Boundary

Problem: SHINOBU_119 owns domain 53, Fluid Incursion, and must not claim direct ownership over logistics graph, structural integrity, hydrodynamic vehicle control, audio DSP, or shader systems beyond typed scalar/event payload bridges.
Solution: Implement fluid truth as owner-local Burst/NativeArray buffers with documented bridge points for CSR topology, integrity breach input, physics mass publication, acoustic flood flags, and shader fill-ratio upload.
Rejected Alternatives: Direct coupling to future Agent 114/115 concrete classes would break simultaneous execution. Runtime GameObject water-plane or particle approaches violate visual-fake-first and batcher/overdraw constraints.
Scalability potential: Low uses one equalization pass and scalar waterlines; middle uses more relaxation; high/ultra buy stronger waterline wobble, more detailed telemetry/gizmos, and smoother mass publication without changing authoritative truth.
Hardware Impact: On i3/MX350, O(N+E) scalar BFS/CSR avoids particle collision and transform movement. Expected steady solver budget target remains below 0.1 ms for small submarine graphs, pending profiler proof.

## Mandate Selection

Problem: Task touches flood physics, native buffers, AUP, audio, debug, and global authority.
Solution: Read eight mandate files before coding: PHYS Fluid Incursion, Zero GC, ARM64 struct layout, Native Memory Jobs, Submarine AUP/Kinematics, Global Registry DI, Debug Telemetry, Acoustic Sensory.
Rejected Alternatives: Reading only AGENTS.md misses task-specific DTO, black-box, and audio bridge details.
Scalability potential: Mandates force continuous GlobalQualityWeight instead of low/high switches.
Hardware Impact: Prevents hidden GC and native allocation churn on low-end silicon.

## DTO Layout And CS1612

Problem: Existing module flood state uses managed object APIs and properties; batch requires a flat 32-byte room DTO with exact offsets.
Solution: Added `FluidCompartmentDTO` at exact offsets 0/4/8/12/16/20/24-31 and `FluidCompartmentPointerUtility` using `UnsafeUtility.AsRef` for direct pointer mutation.
Rejected Alternatives: Mutating `NativeArray<T>[i].CurrentWaterVolume` directly risks CS1612 copy/write mistakes and does not prove memcpy-ready rollback layout.
Scalability potential: Low/Middle/High/Ultra all use the same 32-byte truth buffer; quality changes iteration count and visual wobble only.
Hardware Impact: Four DTOs per 128-byte cache line. On i3/MX350 this removes property dispatch and stack copy churn; estimate 3-6 us saved per 128 active rooms versus managed room object reads.

## Scalar Flooding Instead Of Geometry Or Particles

Problem: Realistic room flooding can be faked visually, while particle water and moving planes burn CPU/GPU without improving authoritative gameplay truth.
Solution: Represent water as scalar volume + shader StructuredBuffer waterline (`FluidWaterlineShaderDTO`) uploaded after jobs. No ParticleSystem or rising water-plane authority was added.
Rejected Alternatives: Unity particles for flood mass; Transform-driven water planes per module; per-room mesh deformation in gameplay loop.
Scalability potential: Low gets fill/waterline only; Middle gets extra BFS passes; High gets stronger wobble; Ultra can consume the same buffer for expensive shader overkill.
Hardware Impact: Avoids particle collision and transform hierarchy churn. Estimated low-end saving is >0.1 ms compared with even a small per-room particle emitter set.

## BFS Equalization

Problem: Water must traverse habitat graph topology and respect sealed bulkheads without coupling to another agent's concrete graph owner.
Solution: Added CSR inputs (`edgeOffsets`, `edgeDestinations`, `edgeFlags`) and `FluidBfsPressureEqualizationJob`. The job BFS-walks connected components, then applies conserved pairwise transfer through unsealed edges.
Rejected Alternatives: Direct `HabitatGraphManager` mutation would create cross-agent ownership conflict; O(N^2) all-room pressure solve is too expensive and ignores topology.
Scalability potential: Iterations are `round(lerp(1,5,GlobalQualityWeight))`, so toaster hardware gets one conserved pass and Ultra gets five relaxation passes.
Hardware Impact: O(N+E) traversal with NativeArray scratch. For 64 rooms/128 edges estimate 12-35 us depending on iteration count; no managed queue allocation.

## AUP Depth And Breach Input

Problem: Ingress must use stable absolute depth, not fragile runtime transform Y or scene-parent assumptions.
Solution: `IntegrityStateDTO` carries `AbsoluteUniversePositionBlit`; `FluidIngressJob` converts grid/local Y to absolute meters and compares to the runtime waterline absolute Y.
Rejected Alternatives: Reading Transform in jobs is impossible and non-deterministic; using local Y alone breaks after floating-origin shifts.
Scalability potential: All quality levels use the same depth scalar; high tiers spend extra cycles only after ingress on equalization and visuals.
Hardware Impact: One double Y resolve per active breached node. Cost is negligible versus wrong-origin flood bugs; estimated <2 us for 32 breach candidates.

## Publication Bridges

Problem: Fluid must affect submarine mass, audio perception, and shaders without claiming those systems' internals.
Solution: Publish `SubmarineFloodStateSignal`, `PhysicsEventBus.NotifyFloodMassShift`, direct `SignalBus<HabitatFloodAcousticMuffleSignal>` payloads, and `_H8HabitatFluidWaterlines` buffer. `AcousticZoneEvents` remains an audio-domain facade around the same typed lane; the flood director does not call the audio facade or reference audio namespace.
Rejected Alternatives: Direct Rigidbody mass mutation, direct AudioMixer parameter writes, or material-instance mutation from the flood solver would violate domain authority and create coupling.
Scalability potential: Low publishes coarser intervals; Middle/High/Ultra consume same mass/acoustic scalars and may render more elaborate waterline shaders.
Hardware Impact: Low-cadence bus publication avoids per-frame listener work when water changes slowly. Estimated <5 us per publish window excluding downstream consumers.

## Black Box And Dump

Problem: NaN or invalid flood state must leave evidence, not a vague crash report.
Solution: Added 300-entry `FluidIncursionTelemetryEntry` ring in `GlobalDataVault`; invalid summary triggers one binary dump to `Docs/AgentLogs/Dump_FLUID_INCURSION.bin`.
Rejected Alternatives: Logging text every frame allocates and destroys the evidence density needed for postmortem analysis.
Scalability potential: Same fixed ring on all hardware; higher tiers can add visual debug consumers without changing telemetry ABI.
Hardware Impact: One 64-byte entry per fixed step. At 50 Hz this is 3.2 KB/s written inside persistent memory, no GC.

## Editor And CSV

Problem: Designers need flood tuning and room volume ingestion without runtime string splitting or entering playmode with hardcoded constants.
Solution: Added UI Toolkit Flood Control Tuner, cold-created live fill bars, direct DTO CSV application, and a caller-provided `NativeParallelHashMap<uint,float>` CSV table hydrator for `ModuleName,MaxVolume` data.
Rejected Alternatives: `string.Split`, LINQ, per-frame UI element spawning, and ScriptableObject-only workflow hide runtime parser cost and do not satisfy CSV ingestion.
Scalability potential: CSV can feed tiny mock bases or large Ultra-tier habitats through the same native buffer.
Hardware Impact: Parser is cold-path; runtime buffers stay unchanged. Hot-path savings are from avoiding managed text parsing entirely.

## Static Compile Sweep

Problem: CPU gate prevented `dotnet build`, so obvious type and ownership faults had to be found by source inspection.
Solution: Corrected `float3` to `Vector3` conversion at the `FloodMassShiftEvent` bridge, locked the tuning buffer during the job window, guarded render upload while jobs are scheduled, and guarded gizmo snapshot before vault initialization.
Rejected Alternatives: Launching `dotnet` at 100% CPU violates batch rule; leaving bridge conversion to implicit operators is invalid because Unity.Mathematics does not guarantee `float3` to `Vector3` implicit conversion.
Scalability potential: Low/Middle/High/Ultra share the same data path; render upload simply waits for post-fixed completion instead of reading in-flight native buffers.
Hardware Impact: Avoids job/read overlap and prevents a compile-stopping type mismatch. CPU budget unchanged; stability gain is correctness, not throughput.

## Ultra Polish Compile-Wall Repair

Problem: The first acoustic bridge shape risked Core.Contracts depending on world/runtime types or flood runtime depending on the audio facade. The generated `Hecton8.Core.csproj` also did not include the newly added Core.Contracts file, so `AcousticZoneController` could not see the payload during `dotnet build`.
Solution: Moved `HabitatFloodAcousticMuffleSignal` into the existing `GlobalSignals.cs` typed-lane surface under `Hecton8.Core.Contracts.Signals` as a 64-byte raw AUP grid/local payload. Flood runtime pushes the typed SignalBus lane directly. Audio may configure/read the same lane through `AcousticZoneEvents`, but it is no longer in the flood call path.
Rejected Alternatives: Keeping `AbsoluteUniversePosition` inside a contracts file would require a sibling World reference; adding a manual csproj include for generated Unity project files is brittle; calling `AcousticZoneEvents.RaiseFloodMuffle` from fluid keeps a direct audio surface in the producer.
Scalability potential: Low/Middle/High/Ultra all share the same 64-byte lane; low tier has lane cap 8, full cap 32.
Hardware Impact: Compile-wall risk reduced; runtime cost remains one value payload per publish window, no audio ray loop. Estimated 20-80 us saved versus per-frame acoustic tracing in flooded bases.

## NoAlias Burst And Cadence

Problem: Burst jobs carried multiple pointer/NativeArray fields that are separate by Vault ownership, but without alias proof the compiler can be conservative. Solver cadence also scaled iterations but still evaluated every fixed tick.
Solution: Added `[NoAlias]` to separate job inputs and kept exact Burst flags with `FloatMode.Deterministic`. Added a deterministic accumulator so `GlobalQualityWeight^2` lerps solver windows from 5Hz to 50Hz while preserving water volume by passing the accumulated delta into ingress/equalization.
Rejected Alternatives: Binary hardware tier switch, `Time.deltaTime`, or main-thread Complete fences inside `FixedTick`.
Scalability potential: Weak devices shed cadence and passes continuously; middle devices interpolate; high/ultra spend saved CPU on smoother waterline/mass updates.
Hardware Impact: At q near 0.1, scheduled solver calls collapse toward 5Hz and one pass. On i3/MX350 this can remove most idle fixed-frame solver dispatches; exact profiler proof pending because CPU gate blocked build.

## Telemetry Facade Repair

Problem: Editor bars originally read active compartment DTOs directly, which could alias job-owned buffers and made the editor facade less isolated than requested.
Solution: Added Vault buffer `ShinobuFluidCompartmentTelemetry` with 32-byte `FluidCompartmentTelemetryDTO`; summary job writes a read-oriented snapshot. Editor reads and tuning writes now return false while `ActiveBurstLockMask` is nonzero.
Rejected Alternatives: Reading live front/back buffers from UI Toolkit, or allocating managed snapshots for editor charts.
Scalability potential: Low tier still writes compact telemetry; high/ultra can draw denser editor diagnostics without touching solver authority.
Hardware Impact: Adds one 32-byte write per active compartment per solved frame. Avoids editor/job data races and keeps runtime hot path allocation-free.

## GPU Upload Double Buffer

Problem: A single GraphicsBuffer upload path could rewrite a buffer still bound as a global shader resource and uploaded every render pass even without a new solved frame.
Solution: Added A/B GraphicsBuffers, dirty flag, and `ResolveNextWaterlineWriteBuffer`. Render upload is skipped while jobs are scheduled or no new waterline DTOs exist.
Rejected Alternatives: Per-material instance edits, per-room mesh planes, or unconditional SetData-style uploads.
Scalability potential: Low tier gets sparse dirty uploads; high/ultra can consume the same scalar buffer for more expensive shader waterline noise.
Hardware Impact: Eliminates redundant upload work on frames without solved flood changes. Estimated savings depend on room count; static proof only until profiler is allowed.

## Global Authority Route Cards

Problem: The domain had real bridges but no route-card proof tying owner, producer phase, consumer phase, payload layout, and failure modes.
Solution: Added route cards for `SHINOBU_119_FLUID_VAULT_STATE`, `SHINOBU_119_FLUID_MASS_SHIFT`, and `SHINOBU_119_FLUID_ACOUSTIC_MUFFLE` in `Docs/ARCHITECTURE/HABITAT_FLUID_INCURSION.md`. They are marked `YELLOW / STATIC PROOF ONLY`.
Rejected Alternatives: Treating chat report as proof or claiming profiler/import verification without running it.
Scalability potential: Cards document low/middle/high/ultra behavior and identify required profiler/GC/runtime proof.
Hardware Impact: No runtime cost; integration cost reduced by explicit one-owner/one-route evidence.

## Build Wall Handling

Problem: A CPU-gated `dotnet build Hecton8.Core.csproj` ran once when CPU reported 36. The build failed on cross-agent/pre-existing missing symbols and one generated-project visibility error for the new flood muffle payload.
Solution: Fixed the SHINOBU-owned visibility error by moving `HabitatFloodAcousticMuffleSignal` into the already compiled `GlobalSignals.cs` typed-lane surface. Follow-up builds were blocked when CPU returned 80 and then 94, with no dotnet/csc processes active.
Rejected Alternatives: Editing generated `.csproj` include lists, touching Visor/Optimization/SaveSystem/Power/Networking errors outside domain, or running another build while CPU exceeded the explicit gate.
Scalability potential: No runtime behavior change; the payload location preserves the typed-lane ABI and avoids a generated-project blind spot.
Hardware Impact: Prevents repeated high-CPU compiler churn on a loaded workstation. Runtime hardware impact is unchanged.

## Ultra Polish R2 AUP Head Math

Problem: The ingress job used absolute-Y arithmetic that was numerically equivalent for shallow cases but did not prove the AUP-local subtraction rule. BFS transfer also compared only fill heights and ignored deck elevation between compartments.
Solution: `FluidIngressJob` now receives `ExternalWaterlineAup` and resolves depth by subtracting breached module grid/local AUP into bounded local float meters. `FluidBfsPressureEqualizationJob` now reads integrity AUPs and computes surface head as AUP delta plus floor delta plus fill-height delta before conserved transfer.
Rejected Alternatives: Scene-local transform Y, raw `gridY * cellSize` absolute depth inside the hot job, or equalizing only normalized fill ratio across decks.
Scalability potential: All quality levels use the same stable head scalar; low tier still reduces cadence/passes, high/ultra spend on more passes and visual wobble.
Hardware Impact: Adds two AUP delta subtractions per traversed transfer edge. On weak devices this is cheaper than wrong-deck oscillation or physics collision checks; exact profiler proof blocked by external build wall.

## Ultra Polish R2 Devirtualization Repair

Problem: The flood mass bridge introduced `IPhysicsFloodMassShiftEventListener[]` dispatch, which is an interface-array hot-path risk under IL2CPP and violates the mandate even though legacy event types still use that pattern.
Solution: Removed the flood-specific listener interface, RegistryBucket, register/unregister overloads, and dispatch loop. Flood still publishes `SubmarineFloodStateSignal` and enqueues unmanaged `PhysicsEventPayload` with `FloodMassShift`; existing submarine physics already reads the typed signal lane.
Rejected Alternatives: Keeping a managed listener array for convenience, adding delegates, or touching legacy non-flood listener paths outside SHINOBU scope.
Scalability potential: Payload shape is unchanged across low/middle/high/ultra; consumers use value snapshots rather than virtual dispatch.
Hardware Impact: Removes one new interface-array dispatch path and cold RegistryBucket allocation. Estimated saving is small per event but eliminates a structural Burst/IL2CPP risk.

## Ultra Polish R2 Human Control Repair

Problem: The editor tuner exposed ingress and equalization but missed the requested water-density control.
Solution: Added a `Water Density` slider backed by `FluidIncursionTuningDTO.WaterDensityKgPerM3`.
Rejected Alternatives: Hardcoding seawater density or requiring a C# change for density tuning.
Scalability potential: Same slider feeds all tiers; visual/mass response scales through existing solver cadence and publish interval.
Hardware Impact: Editor-only control. Runtime hot path remains unchanged except reading the existing tuning field.

## Build Wall R2

Problem: After the SHINOBU-owned flood bridge repair, CPU gate opened at 24 and a new `dotnet build Hecton8.Core.csproj` failed before domain compilation because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is listed in `Hecton8.Core.csproj` but is absent from the filesystem and not tracked by `git ls-files` in this checkout.
Solution: Recorded as external stale generated-project/source dependency wall. Did not synthesize, restore, or edit the World-domain file.
Rejected Alternatives: Inventing a World-domain source file or masking the project include from generated `.csproj`.
Scalability potential: None; build infrastructure dependency.
Hardware Impact: Avoids repeated builds until the missing World source is resolved.

## Ultra Polish R3 Static Reconciliation

Problem: The repeated mandate exposed a process defect: the first strict prompt extraction regex only matched `<AGENT_PROMPT id="SHINOBU_119">` and missed the actual tag with `role` and `chat_name` attributes.
Solution: Reran prompt extraction with an attribute-tolerant CLI regex and reread the full 20-task assignment from `CURRENT_BATCH.md`, plus `AGENTS.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, the binary payload ledger, HFI architecture, and the selected mandate files. No new code change was required after the R2 devirtualization/AUP/editor repairs.
Rejected Alternatives: Trusting the status file alone or accepting the failed strict regex as sufficient evidence would violate the batch protocol.
Scalability potential: No runtime behavior change; the R2 code path still scales cadence 5Hz..50Hz, iterations 1..5, dirty GPU upload, and shader-side visual overkill from the same scalar truth.
Hardware Impact: Static proof only. Current source sweeps are clean for exact Burst directives, forbidden SHINOBU hot-path allocation tokens, `Pack=1`, sequential SHINOBU DTO layout, flood listener interface arrays, and direct audio facade calls. `dotnet build` remains blocked by the unrelated absent World-domain source listed by `Hecton8.Core.csproj`, not by a SHINOBU-owned compile error observed in the latest attempt.
