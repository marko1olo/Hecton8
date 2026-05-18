# LOG_SHINOBU_28

## 2026-05-17 - Terminal OS Diegetic Compute Renderer

What was wrong:
- Existing project UI contains Canvas/TMP-heavy systems; those are not acceptable for 50 diegetic base/submarine terminals because Canvas rebuilds can spike several milliseconds.
- No usable `terminal_ui_layouts.h8bin` or `font_sdf_atlas.bin` was found in current `Docs/Archive` / StreamingAssets scan, and no reliable SHINOBU terminal kerning rationale was present.
- Real Power/Damage/Grid ownership is outside this agent domain, so direct dependencies would create compile-wall risk.

What was done:
- Added `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs`.
  - `TerminalStateDTO` is 48 bytes with raw public fields.
  - `ScreenCommandDTO` is 16 bytes.
  - Added local partial mock signals for power, damage, power status, terminal click, and terminal command.
  - Added Burst `UpdateTerminalTextJob` and `TerminalClickResolveJob`.
- Added `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs`.
  - Resolves terminal NativeArrays from GlobalDataVault buffer ids `70520-70531` when available; falls back to registered persistent NativeArrays before vault bootstrap.
  - Allocates one random-write `RenderTexture` Texture2DArray: 64 slices, 512px high tier or 256px low tier.
  - Uploads only dirty state ranges plus dirty indices through `GraphicsBuffer.LockBufferForWrite`.
  - Dispatches compute once over dirty z-slices.
  - Culls dirty updates behind the camera or past 20m.
  - Maintains a 300-frame telemetry ring and dumps `Docs/AgentLogs/Dump_TERMINAL_OS.bin` on fault threshold.
  - Provides prewarmed NativeQueue lanes for click/command routing.
- Added `Assets/_Project/Art/Shaders/TerminalBlit.compute`.
  - Reads `FixedString32Bytes` byte layout directly.
  - Samples real SDF atlas when bound; otherwise uses procedural SDF/hash strokes from emergency glyph UV grid.
  - Draws the power bar with UV math.
  - Injects damage glitch through shader UV tearing.
- Added `Assets/_Project/Art/Shaders/Hecton_TerminalTextureArrayPanel.shader`.
  - Physical quad material samples the shared Texture2DArray slice.
- Added `Assets/_Project/Scripts/UI/Editor/TerminalOsDesignerWindow.cs`.
  - Editor-only facade edits text layout, bar width, damage scalar, and previews a selected slice using `Graphics.DrawTexture`.
- Added `Docs/AgentLogs/SelfAudit_SHINOBU_28.xml`.
- Updated `Docs/Tasks/Status_SHINOBU_28.md` and `Docs/AgentLogs/Rationale_SHINOBU_28.md`.

Cinematic cheats used:
- Bar graphs: `uv.x < Value1 && uv.y < 0.2`; no chart meshes, no CPU chart data.
- Glyph fallback: hash-driven procedural SDF strokes from a 16x16 UV grid until the real atlas exists.
- Damage: shader-only UV tearing driven by mock scalar; no CPU mesh deformation.
- Power outage: black slice after clearing RGB/text; no overlay animation.
- Attention: dot/range culling instead of raycasts.

Exact microseconds saved estimates:
- Canvas rebuild avoided on attacked base wall: ~5000.0 us spike removed.
- 50-line text formatting without managed strings: ~25.0 us per update.
- Dirty z-dispatch vs all-slice dispatch: ~140.0 us driver/GPU work on static 50-screen walls.
- Chart fake vs object/chart renderer: ~40.0 us per chart batch.
- Attention culling 30 hidden panels: ~60.0 us per frame.
- Texture reallocation avoided during play: ~600.0 us allocation/driver spike.
- Low-tier 256px array: 75% texture bandwidth reduction versus 512px.

Verification:
- Static grep: no runtime Canvas/TMP/ToString/string.Format/LINQ/GetData/SetData in TerminalOS files.
- Static grep: no Pack=1, double3, or AbsoluteUniversePosition in TerminalOS files.
- `dotnet restore Hecton8.Core.csproj`: passed.
- `dotnet build Hecton8.Core.csproj --no-restore`: blocked by unrelated current-disk errors in `BinaryLayoutManifest` / `GlobalTelemetryBus` and earlier other-domain files. Completed build attempts reported no SHINOBU_28 terminal file errors.
- No Unity Play Mode, Unity import compile, profiler, RenderDoc, or visual screenshot proof was run.

## 2026-05-18 - Ultra Polish Mandate Pass

What was wrong:
- The first terminal implementation met the 48-byte/16-byte primary DTO contract, but two secondary runtime DTOs still had 12-byte strides: `MockPowerStatusSignal` and `TerminalClickSignal`.
- Terminal attention-cull pose caches used `NativeArray<float3>`, which is a 12-byte runtime stride. That is not acceptable under the strict ARM64 alignment mandate.
- Blackbox output matched the original `.bin` task but did not also satisfy the later `.h8dump` fatal-artifact wording.
- The final compile boundary shifted under concurrent work: current-disk `LocRegistry.cs` and `UI/SubtitleManager.cs` now block Core compilation before any SHINOBU_28 terminal error appears.

What was done:
- Padded `MockPowerStatusSignal` to 16 bytes: `Frame`, `PoweredMask0`, `PoweredMask1`, `Reserved0`.
- Padded `TerminalClickSignal` to 16 bytes: `TerminalHash`, `LocalUv`, `Reserved0`.
- Changed terminal position/forward caches from `NativeArray<float3>` to `NativeArray<float4>`; only `.xyz` is used during local attention culling.
- Added mirrored fatal dump output: `Docs/AgentLogs/Dump_TERMINAL_OS.h8dump` in addition to `Docs/AgentLogs/Dump_TERMINAL_OS.bin`.
- Rewrote `SelfAudit_SHINOBU_28.xml`, status, and rationale with the updated alignment and compile-wall evidence.

Cinematic cheats used:
- Still no physical chart system: the power/health bar remains a UV inequality in compute.
- Still no CPU text mesh: byte text is consumed from `FixedString32Bytes` and rasterized to Texture2DArray slices.
- Still no simulated terminal damage: glitch is shader hash/sine UV tearing.
- Low tier still buys survival with 256px slices and 10Hz text cadence; high tier spends the saved budget on higher resolution and stronger shader treatment.

Exact microseconds saved estimates:
- 12-byte signal padding: 0.0 us claimed on x86; risk reduction only. On ARM64, prevents pathological unaligned queue stride reads.
- `float4` pose caches: +512 bytes total for two 64-entry caches; expected sub-microsecond impact, chosen for deterministic aligned stepping.
- `.h8dump` mirror: 0.0 us normal-frame cost; one extra sequential fixed-size write on fatal state only.
- Scoped static zero-GC audit: no measured runtime gain; prevents reintroducing Canvas/TMP/string formatting paths.

Verification:
- Re-read exact `<AGENT_PROMPT id="SHINOBU_28">` from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read `Docs/AgentLogs/Rationale_SHINOBU_28.md` and `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Scoped grep found no `Size = 12`, `NativeArray<float3>`, `Pack=1`, Canvas/TMP, `.ToString()`, `string.Format`, LINQ, or `foreach` markers in TerminalOS files.
- `dotnet restore Hecton8.Core.csproj`: passed.
- `dotnet build Hecton8.Core.csproj --no-restore`: blocked by current-disk non-terminal errors. First real source boundary hit `LocRegistry.cs`; after another concurrent shift it hit `UI/SubtitleManager.cs` missing subtitle command/typewriter methods despite those names existing later in the modified file. No SHINOBU_28 terminal file errors were reported.
- No Unity Play Mode, profiler, RenderDoc, frame debugger, or visual screenshot proof was run.

## 2026-05-18 - Ultra Polish Mandate Pass R2

What was wrong:
- The previous report still described private terminal NativeQueues even after the Signal Corridor mandate required global typed lanes.
- Panel slice binding still needed stronger SRP-batcher discipline: per-renderer property state is not acceptable for a wall of terminals.
- Compute dispatch used shader knowledge implicitly instead of querying kernel dimensions.
- Late-frame resilience still had hidden cold-path hazards: `Camera.main` recovery and shipping CSV file polling.

What was done:
- Routed terminal interaction through `SignalBus<TerminalClickSignal>` and `SignalBus<TerminalCommandSignal>`; the Burst resolver reads click snapshots and writes command output through the typed signal lane writer.
- Added `TerminalPanelInstanceDTO` as an 80-byte vault-backed panel instance buffer: `float4x4 LocalToWorld` plus `float4 SliceFlags`.
- Reworked the terminal panel shader to use `SV_InstanceID` and `_TerminalPanelInstances[instanceID].SliceFlags.x` for Texture2DArray slice selection.
- Replaced runtime per-renderer slice property binding with `Graphics.RenderMeshPrimitives` using one material and one structured buffer.
- Derived compute dispatch group counts from `ComputeShader.GetKernelThreadGroupSizes` after `FindKernel`.
- Removed `Camera.main` from terminal late tick; camera is now serialized/injected via `SetAttentionCamera`.
- Gated `terminal_layouts.csv` polling behind editor/development builds so release builds do not probe disk.
- Updated `SelfAudit_SHINOBU_28.xml`, status, and rationale to match the actual final architecture.

Cinematic cheats used:
- Bar graphs remain a UV inequality in compute: no chart meshes, no CPU chart object state.
- Panel identity is faked by instance index and slice flag: one material path, many diegetic screens.
- Damage remains shader UV tearing from a scalar, not CPU deformation.
- Low tier keeps 256px/10Hz behavior; higher tiers spend the budget on 512px slices and stronger visual treatment.

Exact microseconds saved estimates:
- SignalBus migration: 0.0 us claimed as a speed win; it removes local queue ownership and gives global load/backpressure control.
- Instanced panel draw: avoids per-renderer slice property churn; worst-case moving panel upload is 64 * 80 = 5120 bytes per frame.
- Compute group query: 0.0 us claimed; removes a kernel mismatch hazard across Metal/mobile variants.
- Release CSV gate: removes all terminal CSV file timestamp checks in shipping builds.
- Camera injection: removes periodic tag lookup from terminal late-frame work.

Verification:
- Re-read exact `<AGENT_PROMPT id="SHINOBU_28" ...>` from `Docs/Tasks/CURRENT_BATCH.md`; task count is 20.
- Re-read `Docs/Tasks/Status_SHINOBU_28.md`, `Docs/AgentLogs/Rationale_SHINOBU_28.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, domain file, AGENTS, and relevant `.agents-skills` mandates.
- Scoped grep found no runtime `Camera.main`, `MaterialPropertyBlock`, terminal private NativeQueue field, `Pack=1`, `Size = 12`, `NativeArray<float3>`, Canvas/TMP, `.ToString()`, `string.Format`, LINQ, or `foreach` markers in TerminalOS.
- `dotnet restore Hecton8.Core.csproj`: passed in prior pass.
- `dotnet build Hecton8.Core.csproj --no-restore`: remains blocked by unrelated current-disk errors in `GlobalPhysicsStateManager.cs`, `UI/SubtitleManager.cs`, and `World/HectonIndirectVegetationRenderer.cs`; the project also warns that `PhysicsWakeSignalContracts.cs` is included twice. No SHINOBU_28 terminal file errors were reported.
- No Unity Play Mode, profiler, RenderDoc, frame debugger, or visual screenshot proof was run.

## 2026-05-18 - Ultra Polish Mandate Pass R3

What was wrong:
- `TerminalOsRuntime` still carried a sibling-domain dependency risk: `using Hecton8.World` solely for `DispatcherJobSwap`.
- The click resolver job read a live `SignalBus` frame snapshot. That is unsafe if the job is still running when the global signal lane refreshes its snapshot.
- Finite protection was too narrow: CSV/editor layout values, camera transforms, panel matrices, and render bounds could still carry NaN/Inf into rendering.
- Runtime had a hidden `Resources.GetBuiltinResource` fallback for the panel mesh.

What was done:
- Removed the `Hecton8.World` import and replaced the missing `DispatcherJobSwap` dependency with local helpers: non-blocking finalize only completes already-finished handles; force complete is teardown-only.
- Added GlobalDataVault buffer id `70532` for `TerminalClickSignal` scratch. The terminal copies up to 64 click signals into stable vault memory before scheduling the Burst UV-to-button job.
- Added finite guards for layout UV/scale, parsed CSV floats, camera position/forward, attention distance, terminal matrices, and panel bounds.
- Removed the runtime Resources mesh fallback. Instanced terminal drawing now requires an explicit `terminalPanelMesh` binding and fails closed if it is absent.
- Re-ran scoped forbidden-pattern audit and `dotnet build`.

Cinematic cheats used:
- Bar graph remains the cheap UV inequality.
- Damage remains shader UV tearing from a scalar.
- Panel identity remains an instance-buffer slice flag, not 64 materials.
- Low tier keeps 256px and 10Hz formatting; high/ultra keep the richer 512px shader path.

Exact microseconds saved estimates:
- Stable click scratch copy: no speed claim; worst-case copy is 1024 bytes and removes a snapshot lifetime race.
- Removing `Hecton8.World` import: no frame-time claim; it reduces compile coupling.
- Finite guards: no frame-time claim; prevents non-finite render/compute state.
- Removing Resources fallback: no frame-time claim; removes hidden cold asset lookup and makes mesh binding explicit.

Verification:
- Re-read `Status_SHINOBU_28.md`, `Rationale_SHINOBU_28.md`, exact `<AGENT_PROMPT id="SHINOBU_28" ...>`, `PROJECT_STATE_STATIC_XRAY.md`, AGENTS, domain file, and relevant `.agents-skills`.
- Scoped grep passed: no `using Hecton8.World`, `DispatcherJobSwap`, `Resources.Get`, runtime `Camera.main`, `MaterialPropertyBlock`, `Pack=1`, `Size = 12`, `NativeArray<float3>`, Canvas/TMP, `.ToString()`, `string.Format`, LINQ, or `foreach` markers in TerminalOS.
- `dotnet build Hecton8.Core.csproj --no-restore`: no SHINOBU_28 terminal errors. Remaining blocker is unrelated `GlobalPhysicsStateManager.cs(2608)` missing `FlushPhysicsTargetWakeRequests`, plus duplicate-source warning for `PhysicsWakeSignalContracts.cs`.
- No Unity Play Mode, profiler, RenderDoc, frame debugger, GCMonitor, Memory Profiler, player build, or visual screenshot proof was run.
