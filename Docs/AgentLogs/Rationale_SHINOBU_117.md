# Rationale_SHINOBU_117

Agent: SHINOBU_117
Role: ABYSSAL_THERMODYNAMICS_SOLVER
Status: POLISH IN PROGRESS; COMPILE BLOCKED BY VISOR/SOMATIC DEPENDENCY

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

## Polish Pass: ThermalSourceSignal Authority Route
Problem: The new 3D thermodynamics solver could not safely implement `IThermodynamicsService` because that interface exposes `AbyssalThermalManager.ThermalFlowSample`, tying the contract to the legacy World concrete type. Producers reached the legacy service facade, but the Vault-backed solver only saw direct/mock sources.
Solution: Added a 64-byte `ThermalSourceSignal` payload in the existing core signal lane layer. `AbyssalThermalManager` remains the service facade and publishes source AUP/radius/intensity/source id; `AbyssalThermodynamicsSolver` reads the typed snapshot, removes mock DTOs, writes real `HeatSourceDTO` records, and expires transient records after 6 solver frames unless refreshed.
Rejected Alternatives: Add a Thermodynamics reference to World; make World reference Thermodynamics and call `AbyssalThermodynamicsSolver.ActiveRuntimeInstance`; widen `TemperatureChangedSignal` with radius, which would corrupt an existing binary payload; keep the solver on mock sources.
Scalability potential: Low uses sparse transient sources and one to two Jacobi passes. Middle/High/Ultra reuse the exact same source lane while spending more `GlobalQualityWeight` on resolution, relaxation passes, and shader heat-shimmer fidelity.
Hardware Impact: i3/MX350 avoids duplicate heat authority and stale mock injection once real vents publish. Estimated 12-25us producer-heavy frame ambiguity/branch debt avoided; profiler proof remains blocked by CPU gate.

## Polish Pass: H-PHI Vault Authority Tightening
Problem: The abyssal solver and legacy thermodynamics hazard grid still contained cold `GlobalDataVault.Create` fallbacks. Even though they were not hot allocations, they created private memory authorities when boot order was wrong.
Solution: Removed standalone Vault fallbacks from both thermodynamics runtimes. `EnsureVault`/`ResolveDataVault` now accepts `GlobalRegistry.DataVault` or the last boot-created GlobalDataVault, then fail-fasts if neither exists.
Rejected Alternatives: Keep a private fallback for convenience; silently allocate owner-local NativeArrays; defer allocation until first Tick.
Scalability potential: Low/Middle/High/Ultra all use one Vault ownership route and the same BufferIDs.
Hardware Impact: 0us direct runtime gain; prevents hidden persistent memory fragmentation and invalid ownership reports.

## Polish Pass: Direct Lane Determinism and Damage Ownership
Problem: `ThermalSourceSignal` was registered as a typed signal and manually flushed, but it was not yet in the direct registry dispatch list. The legacy thermodynamics grid also still contained a scheduled entity damage job that converted sampled heat/radiation into `CombatDamageSignal`, contradicting the data-provider requirement for Task 09.
Solution: Added `ThermalSourceSignal` to direct registry dispatch, retaining deterministic mutation order and source-id/AUP folded sort keys. Removed the legacy `EntityDamageSamplingJob` scheduling and its mock/combat damage publish loops. `ThermodynamicsHazardGridRuntime` now publishes only `ThermalUpdraftSignal`; damage owners must query samples or consume DTOs through their own authority route.
Rejected Alternatives: Leave `ThermalSourceSignal` on generic fallback dispatch; keep thermodynamics-owned `CombatDamageSignal` emission as a compatibility layer; add a new sibling-domain damage dependency.
Scalability potential: Low gets bounded source-signal capacity 32 and no legacy entity damage scan. Middle/High/Ultra raise source throughput to 128 and spend heat data on visual shimmer and owner-local damage policies, not duplicate scalar-field authority.
Hardware Impact: Estimated 2-5us dispatch ambiguity avoided and 8-20us legacy entity damage sampling/emission avoided per thermodynamics tick on low-end silicon; profiler proof still blocked by CPU gate.

## Polish Pass: Legacy Determinism, Continuum Scaling, and Compile Wall
Problem: The legacy thermodynamics grid still had three correctness leaks: source emission used parallel CAS float accumulation, updraft signal ordering came from interlocked counters inside a parallel diffusion job, and resolution selection was a binary low/high tier decision. Thermodynamics source metadata also still depended on Unity frame count in one producer bridge.
Solution: Converted legacy emission to a serial deterministic `IJob`, moved updraft extraction into the serial telemetry scan, added `[NoAlias]` to proven distinct legacy job pointer fields, changed legacy resolution selection to a polynomial `GlobalQualityWeight` curve with smooth health-pressure damping, replaced thermodynamics frame metadata with local `_simulationFrame`/`HectonArenaAllocator.CurrentFrameSequence`, and removed CAS from the serial abyssal injection job.
Rejected Alternatives: Keep atomics and claim determinism; keep low/high resolution switch for convenience; fix unrelated Visor/Somatic compile failures from this domain.
Scalability potential: Low resolves near 16^3 and updraft cap remains fixed; middle/high/ultra glide toward 32^3 without a pop. Saved CPU from serial finite add/no atomics is spent on stable shader-fed heat shimmer rather than extra physics.
Hardware Impact: Estimated 1-4us dense injection CAS overhead removed, 3-10us legacy source/updraft atomic contention avoided when sources overlap, and resolution cost now scales continuously instead of stepping from 16^3 to 32^3.
Compile Wall: `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` was launched only after CPU opened to 19 percent. It failed in `Visor/HectonVisorUberPostFeature.cs` and `Editor/SomaticTunerWindow.cs` because `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`, `VrComfortProfileDTO`, and `ComfortTelemetryEntry` are missing. No thermodynamics compile error was emitted before that external wall.

## Polish Pass: Sample LOD, Resolution Hysteresis, and GPU Upload Discipline
Problem: Three remaining abyssal routes were still weaker than the mandate. `SampleTemperatureJob` always used nearest-cell reads, so high-tier consumers would get blocky heat perception. Active resolution followed `GlobalQualityWeight` immediately, so noisy quality pressure could rebuild the whole field every frame. VISUAL_SYNC uploaded the thermal cell buffer through single-buffer `GraphicsBuffer.SetData`, violating the bandwidth discipline rule that GPU writes use `LockBufferForWrite` and double buffering. Legacy thermodynamics also still exposed a binary `forceLowResolution` debug switch.
Solution: Added a quality-derived sample interpolation curve using `math.step`, `math.lerp`, and a smooth polynomial. Quality <= 0.15 exits after nearest-cell sampling; higher quality blends toward trilinear temperature/convection/conductivity. Added a 3 second active-resolution hysteresis band before accepting new quality-derived resolution targets. Replaced the single shader buffer with A/B `GraphicsBuffer` ownership and copied Vault front cells through `LockBufferForWrite` plus `UnsafeUtility.MemCpy`. Replaced the legacy debug bool with a continuous `qualityCeiling` scalar.
Rejected Alternatives: Always trilinear sampling, because low-tier consumers would pay eight reads when nearest is enough; immediate resolution rebuilds, because they violate the state hysteresis mandate; `SetData`, because it hides driver synchronization and does not prove bandwidth ownership; forced 16^3 debug bool, because it violates continuous scalability law.
Scalability potential: Low uses nearest samples, 16^3-ish resolution after stable pressure, and one to two diffusion passes. Middle gradually blends samples and resolution. High/Ultra get trilinear owner samples plus double-buffered shader payload without adding physical fluid simulation.
Hardware Impact: Low-end silicon avoids up to 7 extra cell reads per sample at quality <= 0.15 and avoids full-grid rebuild churn during quality jitter. Double-buffered upload reduces GPU/CPU contention risk; exact microseconds require Unity profiler/Frame Debugger once the external compile wall is cleared.
Compile Gate: post-pass-05 build was not launched. First gate check had `Get-CimInstance Win32_Processor` denied and `Get-Process dotnet,csc` showed seven active `dotnet` processes. Later `Get-Counter` sampled `CPU_COUNTER=100`. The no-build-while-dotnet/csc-running and no-build-over-50-percent-CPU rules both blocked verification.
