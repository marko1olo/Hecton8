# Rationale X_016

## Decision 001 - Active Batch Path

Problem: User text requested root `current_batch.md`, but no such file exists at `C:\hades\current_batch.md` or `C:\hades\Hecton8\current_batch.md`.
Solution: Used `rg --files` and selected active authority file `C:\hades\Hecton8\Docs\Tasks\CURRENT_BATCH.md`, then extracted only the `<AGENT_PROMPT id="X_016"...>` block.
Rejected Alternatives: Reading archive batch files would import stale neighboring assignments; guessing from user prose would violate strict parsing.
Scalability potential: Low/Middle/High/Ultra unaffected; this is audit routing.
Hardware Impact: 0 us runtime gain on i3/MX350; prevents wrong-domain edits.

## Decision 002 - Read-Only Boundary

Problem: Assignment demands detailed evidence but forbids C# source mutation.
Solution: Audit source files and write only report/status/log artifacts under `Docs`.
Rejected Alternatives: Adding alignment tests or instrumentation to C# would improve proof but violates task constraints.
Scalability potential: Low gets risk map for fixed pools; Middle/High/Ultra get future capacity and DSP route evidence without current source churn.
Hardware Impact: 0 us runtime change; avoids compile risk during parallel batch.

## Decision 003 - Mandate Set

Problem: Audio audit spans DSP thread safety, occlusion, DTO layout, zero-GC, native memory, signal routing, and crash telemetry.
Solution: Used eight mandates: DSP SPSC, acoustic occlusion, HRTF/spatialization, ARM64 struct layout, zero-GC, native memory/jobs, signal segregation, and postmortem telemetry.
Rejected Alternatives: Reading unrelated physics/voxel/AI mandates would dilute audit and risk false dependency assumptions.
Scalability potential: Low requires bounded source counts and cheap occlusion fakes; Middle/High/Ultra can consume saved budget for richer reverb/HRTF without changing gameplay truth.
Hardware Impact: Static audit only; estimated future savings depend on identified violations, not claimed here.

## Decision 004 - Native Ring Descriptor Alignment Finding

Problem: Native bridge validation requires 8-byte alignment for `WriteIndex`, but `WriteIndex` is assigned from an `int*` shared-state slot at slot 1.
Solution: Recorded this as critical: `sharedStatePtr + 1` is base+4 bytes, so a normally 8-aligned `NativeArray<int>` base makes `WriteIndex` fail the 8-byte alignment check.
Rejected Alternatives: Treating descriptor validation as harmless because native code may ignore it; source shows descriptor rejection clears registration before native registration.
Scalability potential: Low/Middle/High/Ultra all require the native audio bridge to register before quality scaling matters.
Hardware Impact: 0 us measured by this audit; likely impact is functional registration failure, not a small frame-time delta.

## Decision 005 - Portal Path Synchronous Execute Classification

Problem: Portal pathfinding uses `AcousticPathJob`, but the call site invokes `pathJob.Execute()` inside presentation/hydration flow.
Solution: Classified as a bounded synchronous cost: 30 nodes, 60 edges, max 30 expansions, quality-gated and cached, but still inline.
Rejected Alternatives: Marking it as Burst-scheduled job work; the call site is direct `Execute()`.
Scalability potential: Low can disable via `GlobalQualityWeight` gate; Middle/High/Ultra can afford richer portal paths if moved out of immediate play path later.
Hardware Impact: 0 us saved by audit; future savings depend on real profiler data for the 30-node/60-edge inline pass.

## Decision 006 - Virtual Voice Budget Truth

Problem: The system has both virtual voice capacity and physical AudioSource hydration capacity.
Solution: Reported them separately: 1000 virtual requests, 64 max physical voices, 12 low-tier physical voices, 32 default world AudioSources, 8 default 2D sources.
Rejected Alternatives: Reporting only AudioSource pool size would hide the DataVault virtual sorting stage; reporting only 1000 voices would hide Unity source hydration limits.
Scalability potential: Low maps to 12 hydrated physical voices; Middle scales through continuous `GlobalQualityWeight`; High/Ultra reach 64 physical selections while retaining 1000-request sorting capacity.
Hardware Impact: 0 us measured by audit; prevents future agents from overhydrating AudioSources on i3/MX350.

## Decision 007 - DSP Path Classification

Problem: Mandate expects unmanaged DSP proof, but the actual player-critical path uses a managed producer thread plus SPSC ring/native bridge, while world voices use pooled Unity `AudioSource`.
Solution: Reported both facts without flattening them: HullStress kernel is raw-pointer Burst; PlayerCritical renderer is producer-thread/SPSC; SpatialAudioManager is AudioSource presentation for virtual selections.
Rejected Alternatives: Calling the whole system `IAudioOutputJob` based on intent; no `IAudioOutputJob` implementation was found in audited first-party files.
Scalability potential: Low gets reduced granular voice count and SDF taps; Middle/High/Ultra can buy richer reverb/binaural/granular content if bridge correctness is fixed.
Hardware Impact: 0 us measured by audit; no code changed.

## Decision 008 - Padding Hygiene

Problem: Several explicit-layout DTOs are size-aligned but leave unnamed bytes between the last declared field and explicit struct size.
Solution: Reported unnamed padding as medium layout hygiene debt, not ARM64 failure.
Rejected Alternatives: Claiming memory corruption from padding alone; explicit size multiples of 8 satisfy coarse ARM64 alignment.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime today; named pads improve future binary manifest stability.
Hardware Impact: 0 us measured; this is correctness/documentation debt.

## Decision 009 - APEX Formula Addendum

Problem: Initial report documented the architecture but did not spell out exact overload-prevention equations.
Solution: Appended an APEX addendum with the explicit `AcousticPortalNode` offsets, virtual voice priority/culling formulas, SDF occlusion equation, sort comparator, no-wait drop route, and hull-stress DSP parameter conveyor.
Rejected Alternatives: Chat-only correction would violate disk-as-memory reporting; adding C# instrumentation would violate read-only scope.
Scalability potential: Low drops to 12 hydrated voices and 1 SDF tap; Middle scales continuously; High/Ultra reach 64 hydrated voices and 8 SDF taps, while granular stress voices scale 8..64 with hysteresis.
Hardware Impact: 0 us runtime change; future agents now have exact formulas to prevent overhydration and thread stalls on i3/MX350-class hardware.
