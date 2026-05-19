# Rationale_SHINOBU_117

Agent: SHINOBU_117
Role: ABYSSAL_THERMODYNAMICS_SOLVER
Status: POLISH IN PROGRESS; COMPILE BLOCKED BY CPU GATE

## Initial Domain Decision
Problem: Prior heat gameplay was defined as PhysX trigger/distance checks in the prompt, but the live project must be verified before deletion.
Solution: Run archaeology first. Replace only confirmed heat-specific trigger/distance code with a Vault-style double-buffered scalar field and data-provider sampling jobs. Use SIMULATION for heat source injection/diffusion, POST_SIMULATION for swap/telemetry, VISUAL_SYNC for heat distortion upload/debug views.
Rejected Alternatives: Blind deletion by filename; direct references to player/survival/predator concrete systems; particle/fluid convection truth; managed source lists.
Scalability potential: Low uses coarse grid, one Jacobi step, sparse dirty pages. Middle raises cadence/resolution. High/Ultra spend saved CPU on visual heat-shimmer fidelity and denser debug/telemetry presentation, not on unbounded simulation.
Hardware Impact: Expected i3/MX350 gain comes from eliminating PhysX broadphase trigger checks and O(M*N) source scans; actual microseconds remain PENDING MEASUREMENT until profiler/GCMonitor evidence exists.

## Mandate Selection
Problem: Thermodynamics touches Burst jobs, Native memory, AUP, rendering facade, and telemetry.
Solution: Selected 8 mandates: Zero-GC, Native Memory/Jobs, ARM64 DTO layout, AUP determinism, coordinate precision/floating origin, crash telemetry, execution phases, cinematic-cheat visual fake.
Rejected Alternatives: Reading all registry mandates before source archaeology; too much irrelevant context, higher chance of stale neighboring decisions.
Scalability potential: Mandates explicitly support continuous quality weight, deterministic authority, and visual-overkill presentation lanes.
Hardware Impact: Mandates force flat NativeArray access, double buffering, and no per-frame managed allocations; target impact is stable frame time on i3/MX350.

## Current Risk Register
Problem: Prompt requests GlobalDataVault ownership, but actual Vault APIs may not exist or may be unstable.
Solution: Verify existing Core/DataVault contracts before adding surfaces. If absent, implement owner-local native memory with documented Vault-shaped contract only if compile-safe, and log dependency block for Integrator.
Rejected Alternatives: Inventing a GlobalDataVault API and breaking the build.
Scalability potential: Owner-local compile-safe implementation can migrate to Vault handle later without changing thermal DTO layout.
Hardware Impact: Avoids compile wall and avoids allocations hidden behind unverified global APIs.

## Decision 01-05: Field Contract and Legacy Heat Eradication
Problem: Heat was still entering gameplay through hazard-radius and optional trigger components, while the thermodynamics assembly used anonymous BufferID casts and no 16-byte thermal cell DTO.
Solution: Added `SystemID.Thermodynamics`, official Thermodynamics and AbyssalThermal BufferIDs, `ThermalCellDTO` explicit 16-byte layout, layout validation, mock volcano source job, and redirected heat hazard producers (`ThermalUpdraftVolume`, `HectonHazardSource`, `EnvironmentalHazard`) into `IThermodynamicsService.TryInjectTransientHeatSource` instead of PhysX heat exposure. Removed the direct per-vent temperature fallback in `AbyssalThermalManager`.
Rejected Alternatives: Keeping HectonHazardManager as heat fallback; deleting all generic trigger scripts; directly coupling gameplay scripts to the new Thermodynamics asmdef, which is `autoReferenced=false`.
Scalability potential: Low/Middle/High/Ultra use the same scalar field contract. Low falls back to coarse field and one solver iteration. Middle raises resolution and cadence. High/Ultra spend the stable O(N) field on heat-shimmer and telemetry density.
Hardware Impact: i3/MX350 target saves the O(entity*vent) direct temperature path and PhysX heat-zone path; expected hot-path win is tens of microseconds per hazard-heavy frame, exact profiler evidence still pending compile/playmode verification.

## Decision 06-16: Solver, AUP, Shift, and Black Box
Problem: A honest 3D temperature field needs deterministic memory, source injection, diffusion, AUP mapping, sliding-window shift, and crash telemetry without managed allocations in hot jobs.
Solution: Implemented Vault-backed `AbyssalThermodynamicsSolver`, raw-pointer Burst jobs for init, injection, Jacobi diffusion, sampling, hull insulation, shift, and telemetry. Mapping subtracts `GridOriginAup` before float cast. Grid shift uses asynchronous `ShiftThermalGridJob` with `UnsafeUtility.MemMove` into Vault scratch. Telemetry records 300 frames and dumps `Dump_THERMO_SURGEON.bin` on NaN.
Rejected Alternatives: True fluid convection, particle heat columns, `NativeList<HeatSourceDTO>` ownership inside gameplay, single-resolution high/low switch, immediate main-thread completion of the per-frame solver.
Scalability potential: Resolution resolves continuously from GlobalQualityWeight; iterations use `math.lerp(1, 6, GlobalQualityWeight)`. Cheap devices get 16^3 to 20^3 field and one to two relaxations. Middle devices climb gradually. High/Ultra get dense field and stronger visual data without changing gameplay API.
Hardware Impact: SIMD-friendly 16-byte cells keep four cells per 64-byte fetch. MX350/i3 avoids physics overlap and direct vent loops; expected solver cost is bounded by activeCellCount, not entity count. Exact microseconds pending build/runtime profiler.

## Decision 17-20: Control, CSV, Gizmo, and Verification
Problem: Designers need live thermal tuning and proof without enabling volumetric shaders; final evidence must survive context compression.
Solution: Added UI Toolkit `Abyssal Heat Tuner`, cold `ReadOnlySpan<byte>` CSV profile parser, live thermal slice gizmo, architecture doc, status checklist, and final self-audit target for the log. CSV loads into Vault scratch through `FileStream.Read(Span<byte>)`.
Rejected Alternatives: IMGUI editor extension, `string.Split`, shader-only debug, chat-only final report.
Scalability potential: Low uses gizmo and coarse telemetry for validation. Middle/High/Ultra can visualize denser data through the same buffer and shader global without changing gameplay semantics.
Hardware Impact: Editor work is outside runtime. CSV parse avoids managed split churn. Visual upload is one structured buffer transfer instead of per-vent particles.

## Verification Gate
Problem: Build verification is mandatory, but project CPU stayed at 100 percent and the batch forbids dotnet build when CPU is above 50 percent.
Solution: Did not launch `dotnet build`; ran `git diff --check`, Burst enum source verification, targeted static scans for LINQ/List/Physics in new thermodynamics files, heat-route scans, and five manual self-review passes.
Rejected Alternatives: Violating the CPU gate to obtain a fake compile report.
Scalability potential: Static proof is insufficient for final performance numbers; runtime profiler remains required once CPU/build gate clears.
Hardware Impact: No compile-time load added while machine is already saturated.

## Polish Pass: Jacobi, Alias Proof, and Black Box Correction
Problem: Even Jacobi pass counts could overwrite the original Front buffer. That made the energy audit dishonest and made `[NoAlias]` unsafe if telemetry read Front and final cells from the same pointer.
Solution: Runtime diffusion now preserves the original Front buffer and rotates writes through Back and the Vault-owned ShiftScratch buffer. LateFrame promotes whichever buffer contains the final field. Telemetry reads original Front + Injection versus final Back/Scratch and flags drift outside dissipation tolerance. Burst jobs use `CompileSynchronously=true`, deterministic float mode, and `[NoAlias]` only on proven non-overlapping pointers.
Rejected Alternatives: Keep repeated local averaging inside one job; force odd pass counts; blanket `[NoAlias]` on possibly overlapping telemetry pointers; add a new persistent private NativeArray.
Scalability potential: Low runs one pass and promotes Back. Middle/High/Ultra add passes without blocking the main thread; saved CPU still feeds shader heat shimmer, not particle fluid truth.
Hardware Impact: i3/MX350 avoids false audit data and preserves vectorization opportunities. Back/Scratch rotation costs no new allocation because ShiftScratch already belongs to the Vault. Exact microseconds remain pending profiler because CPU gate still blocks build/playmode.

## Polish Pass: Legacy Heat Producer Eviction
Problem: Two submarine producers still routed boiling heat through `HectonHazardManager.Register(... HazardType.Heat)`, keeping the old hazard-zone authority alive behind the new scalar field.
Solution: `SubmarineFluidDynamics` and `SubmarineAtmosphereSystem` now cache `IThermodynamicsService` and inject transient heat sources. Old `Unregister` calls remain only to clear stale hazard entries from previous sessions or older scenes.
Rejected Alternatives: Query `GlobalRegistry.ThermodynamicsService` inside every boiling branch; keep `HectonHazardManager` as fallback; edit generic non-heat hazard systems.
Scalability potential: Low/Middle/High/Ultra all use one route into the thermal field; richer devices spend the resulting scalar data in visual heat shimmer instead of duplicating heat authority.
Hardware Impact: Removes boiling-frame heat zone registration debt and prevents extra hazard-manager scan pressure on i3/MX350. Exact microseconds remain pending profiler because CPU gate still blocks build/playmode.
