# Rationale_SHINOBU_28

Date: 2026-05-17
Status: CORE TASKS COMPLETE / GLOBAL BUILD BLOCKED BY OTHER DOMAINS

## Decision 000 - Work Boundary

Problem: Diegetic terminals need runtime text/graphs without Unity Canvas rebuild cost while running beside other agents.
Solution: Own a local Presentation/UX terminal subsystem and mock cross-domain data. Route future integration through hash/signal DTOs instead of concrete power/damage dependencies.
Rejected Alternatives: Direct references to power grid/damage systems would create compile-wall risk; Canvas/TMP runtime text would reintroduce rebuild and allocation paths.
Scalability potential: Low uses 256 texture slices and 10 Hz updates; Middle uses 512 slices and dirty-only updates; High uses richer glitch math; Ultra can increase active dirty slice budget and shader overdraw polish.
Hardware Impact: On i3/MX350, replacing 20 Canvas rebuilds with dirty compute slices targets removal of multi-millisecond layout spikes; measured proof absent until Unity profiling.

## Decision 001 - Prompt Intake

Problem: Batch file contains 40 agent prompts; adjacent tasks can corrupt scope.
Solution: Extract only `<AGENT_PROMPT id="SHINOBU_28">` using PowerShell line range from CURRENT_BATCH.md and ignore neighboring prompts.
Rejected Alternatives: Reading broad batch context or using IDE tabs would risk cross-agent contamination.
Scalability potential: Scope isolation prevents accidental dependency bloat that would hurt both toaster and high-end profiles.
Hardware Impact: No runtime impact; compile-scope containment reduces editor compile stalls.

## Decision 002 - Missing OSHINO Terminal Binaries

Problem: Archive and StreamingAssets scan did not expose terminal layout/font binaries or reliable kerning logs for SHINOBU_28.
Solution: Generate a deterministic emergency 16x16 glyph UV grid in unmanaged `NativeArray<float4>` and keep the compute shader capable of using a real SDF atlas when one is assigned.
Rejected Alternatives: Throwing on missing content would brick boot; using TMP fallback would violate the no-Canvas/no-TMP mandate.
Scalability potential: Low uses the grid fallback at 256px; Middle/High/Ultra can bind a real SDF atlas without DTO or shader interface churn.
Hardware Impact: On i3/MX350, avoiding binary parse failure keeps boot cost to a small fixed loop over 256 glyphs; no per-frame cost.

## Decision 003 - Dirty Flag Without Breaking 48-Byte DTO

Problem: Task 09 demands `byte IsDirty` but Task 04 fixes `TerminalStateDTO` at 48 bytes.
Solution: Use explicit layout and overlay `IsDirty` at byte 7, the high byte of `BackgroundColor`; shader consumes lower RGB only.
Rejected Alternatives: Adding a 49th byte would break GPU stride and ARM64 alignment; storing dirty state in a sidecar buffer would violate the prompt wording and add another sync lane.
Scalability potential: Low/Middle/High/Ultra all keep the same 48-byte GPU ABI; only dirty slice count scales.
Hardware Impact: Saves one extra dirty buffer upload and preserves 48-byte stride; expected sub-microsecond gain on MX350-class CPU cache paths.

## Decision 004 - Mocked Cross-Domain Power

Problem: Real power grid/damage systems are owned by other agents and may not compile or exist during this batch.
Solution: Define local partial mock signal DTOs and a triangle-wave `MockTerminalDataGenerator`; future integration can replace the producer while keeping the terminal ABI unchanged.
Rejected Alternatives: Directly reading power/damage classes would create sibling-domain dependency risk; random managed data would allocate and be nondeterministic.
Scalability potential: Toaster uses slow 10Hz mock updates; Ultra can feed denser real signals without changing compute-side rendering.
Hardware Impact: Deterministic arithmetic mock is effectively free; avoids dependency-driven rebuild stalls and supports isolated profiling.

## Decision 005 - Compile Wall Boundary

Problem: Loop 1 shell build fails in `Gameplay/SomaticKinematicsRuntime.cs` because `AbsoluteUniversePosition` is unresolved outside this agent domain.
Solution: Do not patch gameplay/AUP ownership from the terminal agent; record the dependency wall and continue local verification/audit on the terminal files.
Rejected Alternatives: Editing gameplay/AUP contracts from Presentation & UX would violate the domain boundary and risk architecture sabotage.
Scalability potential: No runtime terminal impact; avoiding cross-domain fixes keeps the terminal ABI clean for Low through Ultra tiers.
Hardware Impact: No frame-time impact; prevents a broad rebuild/debug detour unrelated to diegetic terminal rendering.

## Decision 006 - Compute Slice Dispatch

Problem: Rendering 50 panels with separate RTs/materials would burn memory and driver time.
Solution: Allocate one `RenderTexture` Texture2DArray and upload a compact dirty-index buffer; dispatch the compute shader once with z-depth equal to dirty count.
Rejected Alternatives: 50 individual render textures, per-panel blit loops, or CPU-generated text meshes all reintroduce rebuild or driver overhead.
Scalability potential: Low uses 256px array and 10Hz format cadence; Middle uses 512px dirty updates; High/Ultra can spend saved cycles on glitch and atlas detail.
Hardware Impact: On i3/MX350, dirty-index dispatch avoids touching static slices and targets sub-0.1ms driver work for sparse updates.

## Decision 007 - Zero-GC Text Bytes

Problem: Runtime terminal text must change without managed string creation or Canvas/TMP layout dirtiness.
Solution: `UpdateTerminalTextJob` formats `PWR ###% PSI ##` directly into `FixedString32Bytes` with byte appends and integer division/modulo.
Rejected Alternatives: `ToString`, `string.Format`, `StringBuilder`, TMP `SetText`, and localization string buffers all either allocate or move work back to UI layout.
Scalability potential: Low throttles the same job to 10Hz; Ultra can update every frame without changing the byte ABI.
Hardware Impact: Avoids managed heap churn and expected 20-30us formatting spikes for 50 lines on low-end CPUs.

## Decision 008 - Attention Culling Uses Float UV/Runtime Space Only

Problem: Prompt demands AUP-aware attention culling but GPU rendering must not receive double precision coordinates.
Solution: CPU-side culling compares camera float position/forward against terminal float positions or bound transforms; compute shader receives only UV-space scalars.
Rejected Alternatives: Passing `double3`/AUP data to GPU would waste bandwidth and violate task 14; relying on Unity renderer culling would still format hidden screens.
Scalability potential: Low drops offscreen updates aggressively; High/Ultra can keep richer visible panels without touching hidden ones.
Hardware Impact: Avoids upload/dispatch for panels behind or beyond 20m; expected tens of microseconds saved on dense base rooms.

## Decision 009 - Texture Lifetime vs Runtime Tier Changes

Problem: Task 12 forbids gameplay `Release/new` churn, while task 13 requires low-tier 256px behavior.
Solution: Resolve tier before allocation and create the array once for the active play session; if tier changes later, throttle cadence/math but do not recreate the texture until teardown/editor.
Rejected Alternatives: Reallocating a Texture2DArray during gameplay would cause driver stalls; ignoring tier entirely would burn MX350 bandwidth.
Scalability potential: Low boots 256px; Middle/High/Ultra boot 512px and can use saved dirty work for richer shader polish.
Hardware Impact: Avoids runtime RT allocation spikes while still cutting low-tier VRAM bandwidth by 75% when booted on weak hardware.

## Decision 010 - Black Screen Means Text Clear

Problem: Power outage response must be instant and must not render advisory text.
Solution: When powered mask is off, the Burst job sets lower RGB to zero, clears `FixedString32Bytes`, and raises dirty; compute redraws a black slice.
Rejected Alternatives: Rendering "BLACKOUT" text would contradict the prompt and still spend glyph pixels; Canvas blackout overlays are prohibited.
Scalability potential: All tiers share the same black-slice path; Ultra can add external post effects later without terminal CPU work.
Hardware Impact: Blackout updates one dirty slice and then becomes static, so ongoing cost returns to zero.

## Decision 011 - Telemetry Ring

Problem: Terminal performance faults need post-mortem evidence without allocating logs every frame.
Solution: Store the last 300 high-level frames in `NativeArray<TerminalTelemetryEntry>` and synchronously dump binary only on fault threshold.
Rejected Alternatives: Managed log spam allocates and is useless in a crash; no telemetry violates the Black Box mandate.
Scalability potential: Low keeps the same 300-entry buffer; Ultra can add more metrics by versioning the dump, not by changing hot-path allocations.
Hardware Impact: Normal path is one fixed NativeArray write per frame; dump IO is failure-only and not part of frame budget.

## Decision 012 - Human Facade Without Runtime UI

Problem: Designers need to move terminal text/bar layout without reopening binaries, but runtime Canvas is prohibited.
Solution: Add an editor-only `Terminal OS Designer` that mutates `ScreenCommandDTO`/`TerminalStateDTO` through runtime ref-backed APIs and previews slices with `Graphics.DrawTexture`.
Rejected Alternatives: In-game debug Canvas would poison runtime UI; manual binary editing would block iteration and invite layout corruption.
Scalability potential: Low/Middle/High/Ultra share the same CSV/DTO layout; editor changes do not add runtime dependencies.
Hardware Impact: Zero player-frame impact because the facade is wrapped in `#if UNITY_EDITOR`; developer iteration savings are material, runtime cost is none.

## Decision 013 - CSV Parser Scope

Problem: Lead layout changes must flow into unmanaged layout floats without allocating per-row strings.
Solution: Monitor `terminal_layouts.csv` on a throttled cadence, read into a preallocated byte buffer, parse hash/x/y/scale in-place, and mark affected screens dirty.
Rejected Alternatives: `File.ReadAllLines`, LINQ, managed CSV packages, or reflection-based data binding would allocate and add brittle dependencies.
Scalability potential: Low probes less often; High/Ultra can probe more often while preserving the same parse code and data ABI.
Hardware Impact: Normal path is a timestamp check only; reload path is bounded to an 8KB buffer and fixed terminal scan.

## Decision 014 - Vault-First Terminal Buffers

Problem: Polish mandate rejects private persistent NativeArrays when GlobalDataVault exists.
Solution: Resolve all terminal state/layout/glyph/scratch/blackbox NativeArrays from GlobalDataVault buffer IDs 70520-70532 when the vault is available, with NativeArray fallback only before bootstrap creates the vault; terminal interaction queues are owned by typed SignalBus lanes and the click job reads a vault-backed frame scratch copy.
Rejected Alternatives: Adding formal BufferID enum entries in Core memory contracts would touch a shared contract file during a concurrent batch; keeping only private arrays or live queue snapshots would fail H-Phi/signal safety.
Scalability potential: Low/Middle/High/Ultra all use the same vault-backed ABI; fallback exists only for isolated editor/mock boot.
Hardware Impact: Vault-backed buffers improve memory accounting and avoid untracked persistent allocations; no per-frame cost change.

## Decision 015 - ARM64 Peripheral DTO Padding

Problem: Ultra polish found two non-primary terminal DTOs at 12 bytes (`MockPowerStatusSignal`, `TerminalClickSignal`). They were legal C# structs but failed the strict "all runtime structs multiple of 8" ARM64 rule.
Solution: Pad both DTOs to 16 bytes with explicit `uint Reserved0`. Keep primary `TerminalStateDTO` at 48 bytes and `ScreenCommandDTO` at 16 bytes.
Rejected Alternatives: Leaving 12-byte queue payloads because they are small would preserve a hidden ARM64 stride hazard; using `Pack=1` is explicitly forbidden.
Scalability potential: Low/Middle/High/Ultra now share 8-byte clean signal strides. Higher tiers can route more terminal clicks without changing ABI.
Hardware Impact: On i3/MX350 the gain is negligible; on ARM64/Quest class silicon it avoids misaligned queue stride reads that can become disproportionate stalls.

## Decision 016 - 16-Byte Runtime Position Cache

Problem: Terminal attention culling cached positions/forwards as `NativeArray<float3>`, creating a 12-byte stride in persistent runtime memory.
Solution: Store those cold caches as `NativeArray<float4>` and cast `.xyz` only for the local culling math. The GPU still receives no AUP/double payload.
Rejected Alternatives: Keeping `float3` would save 4 bytes per terminal but violates the strict cache-line audit; passing transform data directly every frame would increase Unity object reads.
Scalability potential: Low keeps 64 aligned records; High/Ultra can expand terminal density while preserving clean SIMD-friendly strides.
Hardware Impact: Adds 512 bytes total for two 64-entry caches and buys deterministic 16-byte stepping on ARM64/mobile CPUs.

## Decision 017 - Dual Blackbox Dump Extension

Problem: Original SHINOBU_28 task requires `Dump_TERMINAL_OS.bin`; later polish text also demanded `.h8dump` fatal artifacts.
Solution: Keep the original `.bin` dump and mirror the same binary payload to `Dump_TERMINAL_OS.h8dump` on fault only.
Rejected Alternatives: Renaming the original dump would break the XML task contract; writing only `.bin` would fail the later fatal-artifact convention.
Scalability potential: No tier impact; dump path is failure-only and outside normal frame work.
Hardware Impact: Zero normal-frame cost. On fatal state it performs one additional sequential write of the fixed 300-frame telemetry payload.

## Decision 018 - Current Compile Wall Boundary

Problem: After terminal polish, `Hecton8.Core.csproj` no-restore build is blocked by unrelated current-disk files: `GlobalPhysicsStateManager.cs`, `UI/SubtitleManager.cs`, and `World/HectonIndirectVegetationRenderer.cs`; the project also reports duplicate inclusion of `PhysicsWakeSignalContracts.cs`.
Solution: Do not modify those concurrent files from the terminal pass. Record the compile wall and retain terminal static evidence: no `Size = 12`, no `NativeArray<float3>`, no `Pack=1`, no Canvas/TMP/string allocation markers in the scoped TerminalOS files.
Rejected Alternatives: Fixing physics culling, subtitles, or vegetation renderer from SHINOBU_28 would expand the domain and risk reverting other agents' modified work.
Scalability potential: Compile-wall containment protects iteration time while keeping terminal ABI stable.
Hardware Impact: No runtime terminal impact; prevents a cross-domain rebuild loop.

## Decision 019 - Terminal SignalBus Corridor

Problem: Ultra audit found local terminal click/command `NativeQueue` fields. They were bounded and prewarmed, but still fragmented the project signal corridor.
Solution: Make `TerminalClickSignal` and `TerminalCommandSignal` implement `ISignal`, configure typed `SignalBus<T>` lanes with hashes `TCLK`/`TCMD`, route click publish through `SignalBus<TerminalClickSignal>.TryPush`, route command output through `SignalBus<TerminalCommandSignal>.ParallelWriter`, and consume commands through the deterministic frame snapshot.
Rejected Alternatives: Keeping private queues would fail the Signal Corridor mandate; adding terminal DTOs directly into `GlobalSignals.cs` would touch a huge shared file during concurrent churn.
Scalability potential: Low uses 16-frame low-tier lane cap; Middle/High/Ultra can carry the same payload with higher frame limits and no ABI change.
Hardware Impact: Removes two terminal-owned persistent NativeQueues and their sentinel registrations; command delivery gains global lane telemetry/backpressure at no claimed frame-time win.

## Decision 020 - SV_InstanceID Panel Draw

Problem: Panel slice binding still used per-renderer property blocks, which can break SRP Batcher discipline and does not satisfy the prompt's `SV_InstanceID` slice contract.
Solution: Add `TerminalPanelInstanceDTO` (80 bytes: `float4x4` + `float4`) in the vault, upload it to a `GraphicsBuffer`, and draw panels through `Graphics.RenderMeshPrimitives`; the shader reads `_TerminalPanelInstances[SV_InstanceID].SliceFlags.x`.
Rejected Alternatives: MaterialPropertyBlock per renderer was simpler but cache/batcher-hostile; individual materials per terminal would explode SetPass/material state.
Scalability potential: Low can render fewer or lower-res slices while keeping one material path; Ultra can increase instance count and add panel-surface detail in the shader without new CPU objects.
Hardware Impact: Replaces per-renderer property state with one structured buffer upload only when transforms change. Worst-case moving submarine panel upload is about 64 * 80 = 5120 bytes per frame.

## Decision 021 - Compute Kernel Group Query

Problem: Dispatch group counts assumed the compute shader stayed `numthreads(8,8,1)`.
Solution: Query `ComputeShader.GetKernelThreadGroupSizes` after `FindKernel` and derive `_groupsX/_groupsY` from the actual kernel dimensions.
Rejected Alternatives: Hardcoding 8x8 is fragile when shader variants or mobile-specific kernels change.
Scalability potential: Low/Mobile can move to 4x8 or 8x8 safely; High/Ultra can move to wider groups after capture without C# drift.
Hardware Impact: No measured gain; removes a dispatch mismatch hazard and protects Metal/mobile limits.

## Decision 022 - Hot Path Search and I/O Guard

Problem: Terminal culling attempted a throttled `Camera.main` recovery and CSV probing ran in all builds.
Solution: Remove `Camera.main` from late tick; camera is injected through serialized override or `SetAttentionCamera`. Gate CSV hot reload behind `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Rejected Alternatives: Periodic `Camera.main` search is a hidden tag lookup; shipping CSV polling on Steam Deck MicroSD risks avoidable stutters.
Scalability potential: Low tier avoids stray main-thread lookup/I/O; High/Ultra keep the same authored hot-reload workflow in development builds.
Hardware Impact: Normal release path removes file timestamp checks entirely and removes periodic camera tag lookup from late-frame work.

## Decision 023 - Stable Click Snapshot Scratch

Problem: The Burst click resolver originally read `SignalBus<TerminalClickSignal>.GetFrameSnapshotArray()` directly. If the job survives into the next frame, the global signal flush can mutate that snapshot while the job still reads it.
Solution: Add vault-backed buffer id `70532` for `TerminalClickSignal` scratch. Before scheduling the Burst resolver, copy at most 64 16-byte click payloads from the SignalBus snapshot into this stable buffer and schedule the job against `_clickScratch.AsReadOnly()`.
Rejected Alternatives: Completing the click job immediately would serialize the worker path; returning to private NativeQueues would violate Signal Corridor; adding a shared contract buffer enum during concurrent churn would widen compile scope.
Scalability potential: Low keeps a 16-event lane cap and 64-entry stable scratch; Middle/High/Ultra can carry the same ABI with no new queue ownership.
Hardware Impact: Worst-case scratch copy is 64 * 16 = 1024 bytes on main thread before scheduling. This buys deterministic snapshot lifetime and avoids a hidden race rather than claiming a speed win.

## Decision 024 - UI Runtime Compile Isolation

Problem: `TerminalOsRuntime` imported `Hecton8.World` only to reach `DispatcherJobSwap`, creating a sibling-domain dependency and then failing the current compile because that helper is not visible in the terminal assembly surface.
Solution: Remove `using Hecton8.World` and replace the dependency with two local helpers: `TryFinalizeCompletedJob` completes only already-finished handles, and `ForceCompleteJob` is used only during teardown.
Rejected Alternatives: Re-adding the world namespace would hide the compile smell and couple Echelon 8 UI to world runtime; moving `DispatcherJobSwap` into shared contracts would be a public API change outside this task.
Scalability potential: Compile-scope isolation protects all tiers by reducing rebuild blast radius; runtime behavior remains the same swap-window pattern.
Hardware Impact: No measured frame-time gain. It removes a compile-wall dependency and preserves no-stall finalize semantics in normal late-frame work.

## Decision 025 - NaN and Hidden Resource Lookup Hardening

Problem: CSV/editor layout values, camera transforms, terminal matrices, and render bounds could pass non-finite floats into rendering. The runtime also used a hidden `Resources.GetBuiltinResource` quad fallback.
Solution: Add finite guards for layout UV/scale, camera vectors, attention-cull distance, panel matrices, and bounds generation. Remove the Resources fallback; instanced terminal drawing now requires an explicit mesh binding and otherwise fails closed.
Rejected Alternatives: Trusting Unity transforms and editor sliders leaves NaN propagation undefined; hidden Resources lookup violates the cold-path transparency expected by AGENTS.
Scalability potential: Low fails closed instead of spending GPU work on corrupt panels; High/Ultra keep the same explicit instanced mesh path for richer panel visuals.
Hardware Impact: Finite checks are scalar branch work on at most 64 panels; no microseconds are claimed. The value is crash containment and removal of an implicit asset lookup.

## Decision 026 - Latest Compile Wall Boundary

Problem: After local terminal compile fixes, the Core build no longer reports SHINOBU_28 errors. The remaining source error is outside this domain: `GlobalPhysicsStateManager.cs(2608)` calls missing `FlushPhysicsTargetWakeRequests`; the project also warns that `PhysicsWakeSignalContracts.cs` is included twice.
Solution: Stop at this boundary and do not patch physics from the terminal agent. Keep the terminal evidence scoped: no terminal compile errors, no forbidden scoped markers, and documented external blocker.
Rejected Alternatives: Editing `GlobalPhysicsStateManager` from Echelon 8 UI would violate domain ownership and risk overwriting concurrent physics-agent work.
Scalability potential: No runtime terminal effect; compile-wall containment prevents a terminal polishing pass from becoming a physics refactor.
Hardware Impact: No frame-time impact. This is iteration-loop protection, not runtime optimization.
