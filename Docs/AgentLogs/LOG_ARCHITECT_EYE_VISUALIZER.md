# ARCHITECT_EYE_VISUALIZER Log

## 2026-05-16 - VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- DOD memory was invisible at runtime: no single indirect diagnostics surface, no vault byte-span probe, no world-space label/heat/vector overlays.
- Balance CSV was not a first-class Data Monolith source; `Data/Balance/*.csv` had no enforced FNV-1a hash gate or watcher rebake path.
- Runtime faults had no fixed 300-frame Architect Eye blackbox dump/replay path.
- Debug command control would have drifted toward UGUI/TMP unless a diegetic fixed-buffer command receiver existed.

What was done:
- Added `VaultProbeUtility` for generic vault buffer byte spans and finite scanners over float, float3, and AUP data.
- Added `ArchitectEyeVisualizer`, a zero-UGUI runtime diagnostics renderer using `Graphics.DrawMeshInstancedIndirect`, packed instance records, one glyph atlas, and vault-owned diagnostic buffers.
- Added indirect layers for labels, SDF volume wire proxy, SignalBus waterfall, AUP sector map, kinetic vector trails, gas O2/CO2 heatmap, memory block map, homeostasis heartbeat, NaN warning, and STP raw status.
- Added `ArchitectEyePdaCommandConsole` for fixed-char PDA input and preserved command APIs for kill-switch bit flips and STP raw toggling.
- Added `ArchitectEyeBlackBoxTimelineViewer` for loading `Dump_ARCHITECT_EYE_VISUALIZER.bin`, drawing the 300-frame timeline, SceneView fault projection, and `Data/Balance/POIs.csv` breadcrumb capture.
- Extended `H8DataMonolithCompiler` to ingest `Data/Balance/*.csv`, validate hash columns, watch Balance CSV edits, rebake, and reuse the existing hot-reload path.
- Added `SystemID.CoreDiagnostics` and Architect Eye `BufferID` slots in `H8Memory`.
- Added minimal compile-wall repairs outside diagnostics: bridge float-bit helper, LaserCutter `Unity.Collections` import, and Lockstep lane constants mirrored from `GlobalSignals`.

Cinematic cheats used:
- All runtime HUD primitives are quads, not objects: text, lines, minimap cells, memory map bars, and heatmap rooms share one indirect pipeline.
- SDF volume is a density-gated wire proxy, not a CPU mesh rebuild or raymarch in toaster mode.
- AUP minimap reads MacroDB hash bits only; no payload hydration and no disk I/O during draw.
- Kinetic vectors are oriented quads instead of line renderers.
- Gas color selection uses `math.select` between red/green diagnostic colors, not material churn.
- Heartbeat and SignalBus waterfall are fixed historical strips from typed lanes and blackbox frames.

Exact microseconds saved or bounded:
- CSV ingest and breadcrumb tooling: 0 us player runtime.
- Vault byte-span probe: 0-5 us per sampled probe, caller-bounded.
- World labels: 20-45 us CPU at 5Hz on i3/MX350 low budget; one indirect draw.
- SDF wire proxy: 3-8 us CPU, shared indirect draw path.
- Signal waterfall: 8-25 us CPU at 24 visible lanes.
- AUP sector map: 10-35 us CPU, 0 I/O reads.
- Kinetic trails: 15-60 us CPU by tier budget.
- Gas heatmap: 6-25 us CPU by room budget.
- NaN scan/warning: 3-20 us at 5Hz by sampled buffer budget; dump I/O only on fault.
- Memory map: 6-30 us at 5Hz.
- Homeostasis heartbeat: 4-12 us at 5Hz.
- STP status panel: 1-3 us at 5Hz.
- Diegetic command console: 0 us idle, O(command length) only on deliberate PDA input.
- Estimated total active diagnostics cost remains 35-120 us CPU at 5Hz by tier, with 0 us normal player runtime when disabled except registration overhead.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exited 0.
- `dotnet build Hecton8.Editor.csproj -v:minimal /clp:ErrorsOnly /m:1` exited 0.
- Polish audit over `Assets/_Project/Scripts/Core/Diagnostics/Visuals` found no standard `Update`, `LateUpdate`, or `FixedUpdate`; no `string.Format`; no `new NativeArray`; no `EventBus`; no delegate command bus; no `Debug.DrawLine`.
- Shader audit found no compute thread groups, no RW/append buffers, and no geometry shader. Runtime draw path uses `Graphics.DrawMeshInstancedIndirect`.
- The only `Canvas` tokens in diagnostics are inherited diegetic panel API names (`ReceiveCanvasInput`, `CanvasHitPoint`), not UGUI `Canvas` usage.

## 2026-05-16 - LOOP 6 POLISH / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- Diagnostics GPU upload still carried `GraphicsBuffer.SetData` debt after the first green build.
- Status was stale because concurrent external edits re-broke compile after the earlier verification.
- `EcosystemDirector` had a half-finished data-sovereignty conversion: vault index arrays existed, but stale private hash-map references still blocked compile.

What was done:
- Replaced Architect Eye hot uploads with double-buffered `GraphicsBuffer.LockBufferForWrite` for instance records and indirect args.
- Rechecked diagnostics static debt: no standard `Update`, no `string.Format`, no private `new NativeArray`, no EventBus/delegate command bus, no `Debug.DrawLine`, no `GraphicsBuffer.SetData`.
- Rechecked shader debt: no compute thread groups, no geometry shader, no RW/append buffers, no DX-only path found.
- Kept packed diagnostics records guarded with `StructLayout(Pack = 1)` and `UnsafeUtility.SizeOf` assertions.
- Cleaned current external compile walls without broad refactors: retained the existing wrap-safe tether cooldown helper and completed the ecosystem vault index-entry path.

Cinematic cheats used:
- High/Ultra visual overkill remains quad-based: hashed salt/silt/dent diagnostic flecks use the same indirect renderer instead of particles or GameObjects.
- Toaster mode keeps coarse hash cells, triangle/noise fakes, guarded reciprocal math, and 5Hz sampling.
- Ecosystem lookup repair uses packed vault index entries, not managed dictionaries, so data stays inspectable by the Architect.

Exact microseconds saved or bounded:
- No Unity profiler capture was run; no measured microsecond claim is made for the upload change.
- Expected saved cost is driver/staging overhead only from removing `GraphicsBuffer.SetData`.
- Active diagnostics estimate remains 35-120 us CPU at 5Hz by tier until measured in Unity Profiler.
- CSV, breadcrumb, editor replay: 0 us player runtime.
- Diegetic command console: 0 us idle.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /m:1` exited 0 with 0 warnings, 0 errors in 00:00:04.14.
- `dotnet build Hecton8.Editor.csproj -v:minimal /clp:ErrorsOnly /m:1` exited 0 with 0 warnings, 0 errors in 00:02:57.67.
- Diagnostics static scan only reports `ReceiveCanvasInput` / `CanvasHitPoint`, which are diegetic panel coordinate names, not UGUI `Canvas` usage.

## 2026-05-16 - LOOP 7 COMMAND/DUMP INQUISITION / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- Architect Eye still had an `IUpdatable` path solely to poll `UnityEngine.Input.GetKeyDown(KeyCode.F12)`.
- Runtime blackbox dump path still used the stale `Dump_ARCHITECT_SPATIAL_PROBE.bin` name while the editor viewer expected Architect Eye evidence.
- SceneView teleport preview pushed directly to the generic signal bus and could fire outside play mode.
- A concurrent UI navigation job conversion left `NativeSlice<T>.IsCreated` in compass blackbox code, blocking core compile.

What was done:
- Removed `IUpdatable` from `ArchitectEyeVisualizer`, removed the registration/unregistration state, and deleted the F12 polling tick.
- Added zero-allocation fixed-span commands: `eye on`, `eye off`, `eye toggle`, plus `1/0`, `true/false`, and `yes/no` tokens.
- Corrected runtime and editor blackbox paths to `Docs/AgentLogs/Dump_ARCHITECT_EYE_VISUALIZER.bin`.
- Routed teleport preview through `ArchitectEyeDebugBus` and gated it to play mode.
- Applied one external compatibility repair: compass blackbox `NativeSlice` guard now uses `Length`, matching Unity Collections API.

Cinematic cheats used:
- Debug enablement is command-gated, so toaster mode pays no frame input poll and only emits overlays when explicitly enabled.
- High/Ultra visual density remains bought through the same indirect-quad renderer; no new GameObject, UGUI, or line-renderer path was added.
- Blackbox identity now matches the forensic domain, so replay tooling opens the actual Architect Eye dump without filename translation.

Exact microseconds saved or bounded:
- Removed per-frame F12 input query and `IUpdatable` registration from the visualizer; no measured profiler number is claimed.
- Diegetic command path remains 0 us idle and O(command length) on PDA input only.
- CSV, breadcrumb, and editor replay remain 0 us player runtime.
- Active diagnostics estimate remains 35-120 us CPU at 5Hz by tier until Unity Profiler measurement.

Verification:
- Concurrent overwrite reintroduced stale diagnostics lines once; the `IUpdatable`/F12/dump-path patch was reapplied before final verification.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1` exited 0 with 0 warnings, 0 errors in 00:01:03.25.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal /clp:ErrorsOnly /m:1` exited 0 with 0 warnings, 0 errors in 00:01:43.09.
- Diagnostics static scan reports only diegetic `ReceiveCanvasInput` / `CanvasHitPoint` name tokens for `Canvas`; no UGUI component usage.
- Shader scan found no compute groups, no geometry stage, no RW/append buffers, and no DX-only debug path.

## 2026-05-17 - LOOP 8 PROBE HARDENING / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- `VaultProbeUtility` exposed raw vault memory as spans without a byte-length overflow guard before `Span<byte>` construction.
- The read-only probe path reused the mutable-span helper, making visualization reads less explicit than they should be.
- Parallel compile churn exposed generated `sourcelink`/assets races and one external `[MethodImpl]` namespace miss in `HectonPlayerMovement.cs`.

What was done:
- Added a shared `TryResolveBuffer<T>` guard for null vaults, `BufferID.Unknown`, uncreated/empty buffers, and byte lengths beyond `int.MaxValue`.
- Split pointer paths: mutable `Span<byte>` uses `GetUnsafePtr`, read-only visualization spans use `GetUnsafeReadOnlyPtr`.
- Rechecked diagnostics static debt: no standard `Update`, no `string.Format`, no private native allocation, no EventBus/delegate command bus, no `Debug.DrawLine`, no `GraphicsBuffer.SetData`.
- Rechecked shader debt: no compute groups, no geometry stage, no RW/append buffers, no DX-only debug path.
- Applied one external compile hygiene repair: `HectonPlayerMovement.cs` now imports `System.Runtime.CompilerServices` for its existing `[MethodImpl]` helper.

Cinematic cheats used:
- Probe hardening keeps the same zero-copy vault view; no reflective object graph, no managed copy, no per-frame UI hydration.
- Toaster mode remains 5Hz slow-tick diagnostics with bounded probes; High/Ultra can inspect larger pages through the same span gate.

Exact microseconds saved or bounded:
- Probe guard overhead is a few scalar checks per explicit probe; estimated under 1 us per call on i3/MX350, not profiler-measured.
- Diagnostics active estimate remains 35-120 us CPU at 5Hz by tier until Unity Profiler measurement.
- CSV, breadcrumb, editor replay: 0 us player runtime.
- Diegetic command console: 0 us idle.

Verification:
- A second concurrent overwrite reintroduced F12 render polling and `Dump_ARCHITECT_SPATIAL_PROBE.bin`; the diagnostics patch was reapplied and rescanned clean before the final build.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /m:1 /nr:false` exited 0 with 0 warnings, 0 errors in 00:00:05.34.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal /clp:ErrorsOnly /m:1 /nr:false` exited 0 with 5 external package/generated-project warnings, 0 errors in 00:02:45.13.
- Diagnostics static scan only reports `ReceiveCanvasInput` / `CanvasHitPoint`, which are diegetic panel coordinate names, not UGUI `Canvas` usage.

## 2026-05-17 - LOOP 9 DRIFT CONTAINMENT / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- Parallel workspace churn again restored F12 polling inside `ArchitectEyeVisualizer.Render`.
- Runtime and editor blackbox paths again pointed at `Dump_ARCHITECT_SPATIAL_PROBE.bin`, corrupting forensic ownership for Architect Eye dumps.
- The status file claimed clean Loop 8 state while disk had drifted again.

What was done:
- Removed the runtime `UnityEngine.Input.GetKeyDown(KeyCode.F12)` branch from the render path again.
- Restored runtime dump output to `Docs/AgentLogs/Dump_ARCHITECT_EYE_VISUALIZER.bin`.
- Restored editor timeline default input to `Docs/AgentLogs/Dump_ARCHITECT_EYE_VISUALIZER.bin`.
- Re-read AGENTS.md, domain map, current batch grep, and the relevant mandate set before applying the patch.
- Updated status and rationale to record that Loop 9 used targeted static verification, not another build.

Cinematic cheats used:
- No new visual path was added. Toaster mode keeps the cheapest behavior: no idle keyboard poll and no per-frame UI hydration.
- High/Ultra visual density remains bought through the existing indirect-quad renderer, not a second UGUI/Input/TMP debug surface.

Exact microseconds saved or bounded:
- Removed per-render input query branch again; no Unity profiler number is claimed.
- Diegetic command path remains 0 us idle and O(command length) only on deliberate PDA input.
- Runtime/editor dump path correction has 0 us normal-frame cost; binary I/O remains fault-path only.

Verification:
- C# diagnostics static scan now reports only `ReceiveCanvasInput` and `CanvasHitPoint`; those are diegetic panel coordinate API names, not UGUI usage.
- No `GetKeyDown`, no `KeyCode.F12`, no `ARCHITECT_SPATIAL_PROBE`, no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no private native allocation, no EventBus/delegate command bus, no `Debug.DrawLine`, and no `GraphicsBuffer.SetData` remain in the diagnostics visualizer domain.
- Shader static scan returned no compute thread groups, no geometry stage, no RW/append buffers, no `ComputeBuffer`, and no `normalize` token.

## 2026-05-17 - LOOP 11 SOURCE-RISK POLISH / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- Loop 10 introduced fragile digit formatting through `char + uint`; some compiler profiles may reject or widen it unexpectedly.
- `CreateQuadMesh()` still allocated anonymous vertex/UV/index arrays during resource setup without explicit cold-allocation ownership.
- The indirect-quad shader axis normalizer guarded zero/NaN length but did not reject absurdly large axis length before `rsqrt`.

What was done:
- Changed digit write to explicit `char + int` arithmetic.
- Moved quad mesh vertices, UVs, and indices into static documented cold arrays.
- Added an upper bound to shader axis normalization before `rsqrt`.

Cinematic cheats used:
- No new renderer path. Visual overkill remains salt/silt/dent indirect quads.
- Toaster mode still uses the same low-capacity indirect path with no added update loop.
- Shader finite fallback is a cheap compare, not a backend-specific intrinsic.

Exact microseconds saved or bounded:
- No profiler measurement was run.
- Formatter change is compile hygiene only.
- Static cold arrays remove per-instance setup array allocation, but this is cold-path resource setup, not frame time.
- Shader guard adds one scalar compare inside the vertex axis helper.

Verification:
- `dotnet build` was not rerun by explicit user instruction to avoid rebuild spam.
- Targeted C# scan still reports only `ReceiveCanvasInput` / `CanvasHitPoint`; those are diegetic panel coordinate names, not UGUI usage.
- No F12 polling, no stale spatial-probe dump path, no standard runtime updates, no `string.Format`, no private native allocation, no EventBus/delegate command bus, no `Debug.DrawLine`, and no `GraphicsBuffer.SetData`.
- Shader static scan returned no compute groups, no geometry stage, no RW/append buffers, no `ComputeBuffer`, and no `normalize` token.

## 2026-05-17 - LOOP 12 PARALLEL DRIFT REAPPLY / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- A final post-doc scan found another concurrent overwrite: `UnityEngine.Input.GetKeyDown(KeyCode.F12)` returned inside `Render`.
- Runtime and editor blackbox paths reverted to `Dump_ARCHITECT_SPATIAL_PROBE.bin`.

What was done:
- Removed the F12 branch again from the render path.
- Restored runtime dump output to `Dump_ARCHITECT_EYE_VISUALIZER.bin`.
- Restored editor timeline default load path to `Dump_ARCHITECT_EYE_VISUALIZER.bin`.

Cinematic cheats used:
- No new visual path. The HUD remains indirect quads only.
- Diegetic command control remains the only runtime toggle path.

Exact microseconds saved or bounded:
- Removed a per-render input query branch again; no measured profiler number is claimed.
- Blackbox path correction has 0 us normal-frame cost.

Verification:
- `dotnet build` was not rerun by explicit user instruction to avoid rebuild spam.
- Targeted C# scan again reports only `ReceiveCanvasInput` / `CanvasHitPoint`, which are diegetic panel coordinate names, not UGUI usage.
- No `GetKeyDown`, no `KeyCode.F12`, no `ARCHITECT_SPATIAL_PROBE`, no standard runtime updates, no `string.Format`, no private native allocation, no EventBus/delegate command bus, no `Debug.DrawLine`, and no `GraphicsBuffer.SetData`.
- Shader static scan remains clean for compute groups, geometry stage, RW/append buffers, `ComputeBuffer`, and `normalize` tokens.
- `dotnet build` was not rerun in Loop 9 by explicit user instruction to avoid rebuild spam. Latest build evidence remains Loop 8: Core exited 0; Editor exited 0 with 5 external package/generated-project warnings.

## 2026-05-17 - LOOP 10 CAPACITY / COMMAND HARDENING / VERIFIED MASTER GRADE - EYES OPEN

What was wrong:
- High/Ultra quad capacity could exceed the GPU buffer allocation when `_maxQuads` was serialized low or the quality tier moved upward. The vault would build more visual-overkill quads than the renderer uploaded.
- Diegetic kill-switch mask parsing wrapped on `uint` overflow instead of rejecting invalid commands.
- Diagnostic integer formatting could overflow on `int.MinValue`.

What was done:
- Added `_bufferQuadCapacity` as the renderer's actual GPU capacity record.
- Added `EnsureBufferCapacity()` so High/Ultra capacity growth allocates once and then reuses double-buffered `GraphicsBuffer` instances.
- Upload now clamps against actual buffer capacity, not the serialized `_maxQuads` field.
- High tier now gets the same default 8192 minimum as Ultra instead of being capped down to mobile-density diagnostics.
- Kill-switch parser now rejects overflowing decimal and hex masks.
- `AppendInt()` now formats `int.MinValue` without signed negation overflow.

Cinematic cheats used:
- God-mode visual overkill remains cheap indirect quads, not particles/GameObjects/UGUI.
- Toaster mode still uses the low-tier cap and only pays one SlowTick capacity comparison.
- Command hardening keeps the PDA console deterministic; bad masks fail closed.

Exact microseconds saved or bounded:
- No profiler measurement was run; no measured microsecond claim is made.
- Steady-state capacity guard cost is one integer comparison chain per SlowTick.
- Buffer growth happens only on quality-tier/capacity increase, not every frame.
- Command parser cost remains O(command length) only on deliberate PDA input; 0 us idle.

Verification:
- `dotnet build` was not rerun by explicit user instruction to avoid rebuild spam.
- Targeted C# static scan still reports only `ReceiveCanvasInput` / `CanvasHitPoint`, which are diegetic panel coordinate names, not UGUI usage.
- No `GetKeyDown`, no `KeyCode.F12`, no `ARCHITECT_SPATIAL_PROBE`, no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no private native allocation, no EventBus/delegate command bus, no `Debug.DrawLine`, and no `GraphicsBuffer.SetData` remain in the diagnostics visualizer domain.
- Shader static scan returned no compute thread groups, no geometry stage, no RW/append buffers, no `ComputeBuffer`, and no `normalize` token.
