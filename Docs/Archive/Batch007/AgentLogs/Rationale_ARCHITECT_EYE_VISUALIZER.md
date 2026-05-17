# ARCHITECT_EYE_VISUALIZER Rationale

## Decision 0 - Domain and Prompt Source
Problem: The active `Docs/Tasks/CURRENT_BATCH.md` lacks the `ARCHITECT_EYE_VISUALIZER` XML block, while the user supplied the full XML inline.
Solution: Treat inline XML as the assignment source and record the missing batch extraction as evidence. Keep implementation inside `Assets/_Project/Scripts/Core/Diagnostics/Visuals/` except editor/build glue required by CSV bake, watchers, and blackbox playback.
Rejected Alternatives: Reading archived batch prompts as authority. That violates fresh-batch hygiene and risks neighboring-task contamination.
Scalability potential: Low tier keeps diagnostics disabled or 5Hz-bucketed; Middle uses bounded overlays; High adds richer graph samples; Ultra uses overkill heat overlays without touching gameplay truth.
Hardware Impact: Expected hot-path impact is near-zero when disabled; active debug target is bounded under 0.1 ms CPU on i3/MX350 by dirty pages and indirect quads.

## Decision 1 - Rendering Primitive
Problem: Debug HUD must not use UGUI Canvas, and text/heatmaps must avoid per-frame managed strings.
Solution: Build one indirect-quad renderer with fixed CPU-side instance buffers, `GraphicsBuffer`, and `Graphics.DrawMeshInstancedIndirect`. Labels use a fixed bitmap atlas with preformatted glyph indices.
Rejected Alternatives: TMP/UGUI overlays, GameObject labels, `Debug.DrawLine` loops, and `Handles` runtime drawing. These allocate, rebuild canvases, or do not survive player builds.
Scalability potential: Low draws coarse cells and fewer labels; Middle draws sector grids; High draws per-system strips; Ultra increases label density and heatmap detail.
Hardware Impact: One draw per layer and sparse buffer uploads replace hundreds of debug GameObjects; estimated savings versus GameObject/TMP debug is 200-900 microseconds on i3/MX350 in active debug views.

## Decision 2 - CSV Authority Integration
Problem: The prompt requires `Data/Balance/*.csv`, but the existing Data Monolith compiler only read `Assets/_SourceData`.
Solution: Extend the existing compiler to enumerate both roots and extend the existing editor watcher to `Data/Balance`. Hash validation occurs before row conversion so bad authoring data cannot reach `.h8bin`.
Rejected Alternatives: A second baker, runtime CSV parsing, or copying Balance CSV into Assets. Those paths create duplicate truth or player-runtime file I/O.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime because CSV ingest is editor/cold build-time only.
Hardware Impact: 0 us player hot path. Editor bake cost increases only by the number of authored Balance files.

## Decision 3 - Vault Probe Shape
Problem: C# cannot infer the element type of "any NativeArray by ID" from `BufferID` alone without exposing GlobalDataVault internals or reflection.
Solution: Provide a generic `TryReadBufferBytes<T>` and typed handle probe. Callers supply the known buffer type; the utility returns a raw byte span over existing vault memory without allocation.
Rejected Alternatives: Reflection over private vault metadata, `object`-typed NativeArray boxing, or a giant switch for every BufferID. These either allocate or mutate public contracts.
Scalability potential: Low samples tiny slices; Middle samples selected lanes; High/Ultra can scan more entries because the API has no heap cost.
Hardware Impact: Probe cost is a linear native read over caller-selected samples; no persistent memory is allocated by the utility.

## Decision 4 - Compile Wall Classification
Problem: First compile verification fails in `GameBootstrapper` because `Hecton8.Core.Bucketing.ModuloSimulationBucketer` is missing from the codebase, outside the assigned diagnostics domain.
Solution: Record as an existing dependency wall and continue diagnostics implementation until final compile, where a minimal cross-domain bridge may be considered only if required to satisfy `PLATINUM_COMPILE`.
Rejected Alternatives: Editing `GameBootstrapper` immediately or inventing broad Bucketing behavior before diagnostics depends on it. That risks architectural drift in another domain.
Scalability potential: Diagnostics renderer will still self-bucket to 5 Hz using existing tick cadence and fixed counters, independent of the missing bucketer.
Hardware Impact: Avoids unnecessary domain churn now; estimated 0 us player impact from this recording decision.

## Decision 5 - Vault-Owned Visualizer State
Problem: A diagnostics system with private `NativeArray` state violates the H-Phi data sovereignty demand and hides memory from the Architect.
Solution: Added dedicated `BufferID` slots for Architect Eye quads, signal telemetry, sector hashes, runtime state, and black-box history. The runtime asks `GlobalDataVault` for buffers every slow tick and owns no persistent `NativeArray` fields.
Rejected Alternatives: Component-owned NativeArrays, managed lists, TMP canvases, or per-system event buffers. Those create private memory islands and garbage hazards.
Scalability potential: Low/MX350 clamps quads and sampled entities; Middle opens more room/sector cells; High increases vector and label density; Ultra uses the same path with larger draw counts for overkill overlays.
Hardware Impact: One vault-backed buffer upload and one indirect draw replaces hundreds of debug objects. Current estimate is 35-120 us CPU at 5 Hz by tier; render submission is one indirect draw under normal load.

## Decision 6 - Multiplatform Shader Path
Problem: The visualizer must survive Metal/Quest/Android without DX-only debug shortcuts or compute thread-group assumptions.
Solution: Used `Graphics.DrawMeshInstancedIndirect` with a structured instance buffer and simple vertex/fragment shaders. No compute dispatch, no group-size dependency, no geometry shader, no UGUI canvas.
Rejected Alternatives: Compute-generated quads, geometry-shader billboards, `Debug.DrawLine`, or IMGUI runtime overlays. These are brittle on mobile/Metal or allocate.
Scalability potential: Toaster mode emits coarse hash cells and triangle-line fakes; God-mode increases the same indirect instance count instead of changing architecture.
Hardware Impact: Steam Deck/MX350 avoids canvas rebuilds and line renderer churn; RTX tier pays only more instance count, not new draw-call classes.

## Decision 7 - Blackbox and Fault Survival
Problem: A non-finite value in a vault buffer can poison the debug renderer and mobile GPU pipeline before a normal log tells the Architect what broke.
Solution: Scan sampled vault data with guarded finite checks, draw a red indirect warning at the last AUP fault, and write the 300-frame fixed blackbox ring to `Docs/AgentLogs/Dump_ARCHITECT_EYE_VISUALIZER.bin` once per fault burst.
Rejected Alternatives: `Debug.LogError` spam, exception-only handling, or a managed list of recent frames. Those miss player builds, allocate, or do not preserve the last stable frames.
Scalability potential: Low samples fewer entities and still dumps the same 300-frame record; Middle/High/Ultra increase visible vector/label density without changing crash evidence format.
Hardware Impact: Fault scan estimate is 3-20 microseconds at 5Hz on i3/MX350 by sample budget. Binary dump is fault-path I/O only, not a normal-frame cost.

## Decision 8 - Editor Replay and Breadcrumb CSV
Problem: Runtime binary dumps and designer POIs need offline inspection without adding runtime GameObjects or asset types that bypass Balance CSV authority.
Solution: Added an EditorWindow timeline reader for fixed blackbox records and a SceneView Ctrl+Click breadcrumb writer that appends AUP rows with FNV-1a hash columns to `Data/Balance/POIs.csv`.
Rejected Alternatives: JSON replay files, ScriptableObject POI assets, or runtime Handles. These either allocate more, avoid the Data Monolith path, or do not ship with deterministic binary evidence.
Scalability potential: Runtime is unaffected on all tiers. High/Ultra machines get richer editor visualization from the same dump; low-tier runtime pays 0 microseconds for editor playback.
Hardware Impact: 0 microseconds player hot path. Editor-only parsing is bounded by fixed record size and happens on demand.

## Decision 9 - Diegetic Command and STP Control
Problem: The Architect needs emergency bit flips and STP raw visibility without UGUI/TMP or managed console callbacks.
Solution: Added a fixed-char diegetic PDA command receiver using the existing physical panel interface and routed commands into preserved `SubmitCommand(ReadOnlySpan<char>)` APIs. Commands support kill-switch mask set/clear and raw STP overlay state.
Rejected Alternatives: Unity `InputField`, Canvas console, reflection console, or managed delegate command buses. These violate zero-UGUI/zero-GC requirements or bypass typed systems.
Scalability potential: Low tier has idle cost 0 and only processes on panel events; Middle/High/Ultra can expose the same command surface with denser visual diagnostics.
Hardware Impact: 0 microseconds idle. Input cost is O(command length) on deliberate panel input only.

## Decision 10 - Compile-Wall Repairs
Problem: Final `dotnet build` was blocked by small external compile faults unrelated to diagnostics: unavailable `BitConverter.SingleToUInt32Bits`, a missing `Unity.Collections` import, and missing lockstep lane constants.
Solution: Applied surgical compatibility repairs: bridge-local float bit union, one namespace import, and lockstep constants mirrored from `GlobalSignals` literals. No behavioral refactor was made in those domains.
Rejected Alternatives: Marking `PLATINUM_COMPILE` blocked while a safe compile fix was available, or editing broad system behavior to hide errors.
Scalability potential: These repairs are compile-time hygiene and do not change tier behavior.
Hardware Impact: 0 microseconds player impact; the bridge helper is an inline 4-byte reinterpret used where the unavailable framework API was intended.

## Decision 11 - Final Polish Audit
Problem: The diagnostics domain must prove no standard `Update`, no `string.Format`, no UGUI Canvas renderer, no private native allocation, and no DX-only shader shortcut.
Solution: Ran a targeted `rg` audit over `Assets/_Project/Scripts/Core/Diagnostics/Visuals`. Findings: no `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no `new NativeArray`, no `EventBus`, no delegate command path, no `Debug.DrawLine`. The only `Canvas` string is the existing diegetic panel interface name, not a UGUI component. Shaders use vertex/fragment paths, no compute groups, no geometry shader.
Rejected Alternatives: Relying on visual inspection or compile success alone. Compile success does not prove allocation/rendering discipline.
Scalability potential: Low clamps entity/quad counts; Middle opens more overlays; High increases densities; Ultra spends saved draw-call budget on denser diagnostic overkill through the same indirect path.
Hardware Impact: Current estimate remains 35-120 microseconds CPU at 5Hz by tier when active, 0 microseconds when disabled except registration overhead.

## Decision 12 - GPU Upload Bandwidth Repair
Problem: The visualizer still had a `GraphicsBuffer.SetData` upload path, which is acceptable for cold tooling but not for a live HUD on Steam Deck/MicroSD-pressure debug sessions or mobile GPUs.
Solution: Replaced the hot upload path with double-buffered `GraphicsBuffer.LockBufferForWrite` for both instances and indirect args, then publish the completed buffer alias to the material. This keeps the indirect-quad rule intact and avoids a driver-side SetData staging copy in the diagnostics loop.
Rejected Alternatives: Keeping `SetData`, switching to compute-generated quads, or adding a command-buffer-specific render feature. `SetData` is the bandwidth debt; compute adds Metal/Quest risk; render-feature ownership belongs outside diagnostics.
Scalability potential: Low/MX350 keeps low quad caps and sparse 5Hz updates; Middle/High/Ultra increase the same locked-buffer instance count. Ultra can spend saved submission pressure on salt/silt/dent diagnostic overdraw without changing the primitive.
Hardware Impact: No profiler microseconds were measured in this pass. Expected CPU/driver saving is bounded to upload overhead only; runtime estimates remain 35-120 us CPU at 5Hz by tier until Unity profiler capture proves a narrower number.

## Decision 13 - Multiplatform NaN and Packing Inquisition
Problem: ARM64/Quest and Metal builds punish implicit padding, unguarded reciprocal math, and DX-only shader assumptions. One NaN in the diagnostic draw data can poison the mobile GPU pipeline.
Solution: Kept all Architect Eye records `[StructLayout(Pack = 1)]`, added editor/development `UnsafeUtility.SizeOf` assertions, routed divisions through `SafeRcp`, line normalization through `SafeRsqrt`, and kept shaders as vertex/fragment code with no compute thread groups, geometry stage, or RW buffers.
Rejected Alternatives: Trusting C# layout defaults, using `normalize`/unchecked `rsqrt` everywhere, or relying on PC-only shader behavior. Those are cheap until Quest or Metal fails.
Scalability potential: Low uses triangle/hash fakes and coarse cells; Middle adds readable strips; High/Ultra add denser visual-overkill quads through the same safe pipeline.
Hardware Impact: 0 us when disabled. Active-pass overhead is still estimate-only, not measured: 35-120 us CPU at 5Hz by tier, with GPU cost proportional to indirect quad count.

## Decision 14 - Concurrent Compile-Wall Cleanup
Problem: After diagnostics was green, concurrent edits reintroduced compile failures in `TetherInstance` and `EcosystemDirector`, outside the assigned domain but blocking `PLATINUM_COMPILE`.
Solution: Applied surgical compatibility cleanup only. `TetherInstance` keeps the existing wrap-safe frame cooldown helper. `EcosystemDirector` now consistently uses vault-backed `EcosystemIndexEntry` arrays instead of stale private hash-map names.
Rejected Alternatives: Reverting other agents' staged work or marking the visualizer blocked while a narrow compile fix was available. Reverts would violate parallel-agent ownership; blocking would leave stale false status.
Scalability potential: The ecosystem index conversion supports the same H-Phi data-sovereignty direction as Architect Eye: state lives in the vault and systems stay inspectable.
Hardware Impact: Architect Eye runtime cost unchanged. Compile evidence: Core build 0 warnings/0 errors in 00:00:04.14; Editor build 0 warnings/0 errors in 00:02:57.67.

## Decision 15 - Input Poll Eviction and Dump Identity
Problem: Architect Eye still had an `IUpdatable` registration for F12 polling and a stale `Dump_ARCHITECT_SPATIAL_PROBE.bin` filename, which violated the diegetic command surface and made blackbox evidence ambiguous.
Solution: Removed the per-frame input poll and routed HUD enable control through fixed-span PDA commands: `eye on`, `eye off`, `eye toggle`, and boolean tokens. Corrected runtime dump and editor replay default path to `Dump_ARCHITECT_EYE_VISUALIZER.bin`.
Rejected Alternatives: Keeping a hidden keyboard shortcut or aliasing two dump names. Polling costs frame attention when the PDA command path already exists; stale dump names break postmortem ownership.
Scalability potential: Low tier pays 0 us idle because no frame input polling remains; Middle/High/Ultra get the same command path and can raise visual density only after explicit enable.
Hardware Impact: Removes a tiny per-frame branch and input query from diagnostics. No measured profiler number is claimed; command processing remains O(command length) only on deliberate panel input.

## Decision 16 - External NativeSlice Compile Wall
Problem: A concurrent UI navigation edit moved compass blackbox writes into a scheduled job with `NativeSlice<CompassBlackBoxEntry>` but left a `NativeSlice.IsCreated` check, which does not exist in Unity Collections and blocked core compile.
Solution: Applied the narrow compatibility repair: the write guard now checks slice length only. The caller already creates the slice from a vault-owned `NativeArray` after `TryGetCompassBuffers` verifies creation and capacity.
Rejected Alternatives: Reverting the UI navigation job conversion or adding a fake extension method. Revert would overwrite another agent's work; an extension would hide an API mismatch and broaden the patch.
Scalability potential: No Architect Eye behavior changes. The external compass keeps its job path and fixed blackbox capacity without managed allocations.
Hardware Impact: Architect Eye runtime cost unchanged. Latest verification: Core build 0 warnings/0 errors in 00:01:03.25; Editor build 0 warnings/0 errors in 00:01:43.09.

## Decision 17 - Vault Probe Span Hardening
Problem: The probe API exposed raw vault buffers as byte spans but did not guard byte-length overflow before constructing `Span<byte>`, and its read-only helper reused the mutable span path.
Solution: Added a shared `TryResolveBuffer<T>` guard that rejects null vaults, unknown buffer IDs, uncreated buffers, empty buffers, and buffers whose byte length exceeds `int.MaxValue`. Mutable `Span<byte>` now uses `GetUnsafePtr`; read-only visualization spans use `GetUnsafeReadOnlyPtr` directly.
Rejected Alternatives: Reflection over vault internals, unchecked `buffer.Length * sizeof(T)`, or forcing every caller through the mutable span path. Those are either slow, unsafe on large buffers, or too permissive for diagnostic reads.
Scalability potential: Low-tier probes can sample tiny spans without heap cost; High/Ultra can inspect larger vault pages while remaining bounded by `Span<T>` limits and caller-selected buffer type.
Hardware Impact: Runtime hot path unchanged unless a probe is explicitly requested. Added work is a few scalar checks per probe; estimated under 1 us per call on i3/MX350, not measured in Unity Profiler.

## Decision 18 - Loop 8 Compile Hygiene
Problem: Rebuilds during parallel-agent churn exposed external compile walls unrelated to diagnostics: generated `sourcelink`/assets races and an existing `[MethodImpl]` use in `HectonPlayerMovement.cs` without the required namespace import.
Solution: Retried after generated-file contention, then applied the one surgical source repair: `using System.Runtime.CompilerServices;` in `HectonPlayerMovement.cs`. No behavior was changed in player movement.
Rejected Alternatives: Cleaning `Temp` globally, killing other agents' dotnet processes, or reverting external work. Those would be disruptive in the parallel workspace.
Scalability potential: No Architect Eye behavior changes. The compile repair keeps the external movement helper eligible for inlining across tiers.
Hardware Impact: Architect Eye runtime cost unchanged. Latest verification: Core build 0 warnings/0 errors in 00:00:05.34; Editor build 5 external package/generated-project warnings and 0 errors in 00:02:45.13.

## Decision 19 - Loop 9 Recurrent Drift Containment
Problem: Parallel agents again reintroduced the same diagnostics debt after a previously clean audit: runtime F12 polling inside the render path and stale `Dump_ARCHITECT_SPATIAL_PROBE.bin` identity in runtime/editor blackbox paths.
Solution: Reapplied the narrow diagnostics-domain correction: enable state is PDA-command-only, and both runtime dump writing and editor replay default load `Dump_ARCHITECT_EYE_VISUALIZER.bin`. Verification used targeted static scans instead of another full build.
Rejected Alternatives: Running `dotnet build` again after a non-signature string/removal patch, keeping the hidden F12 shortcut, or aliasing two dump filenames. The user explicitly rejected rebuild spam, the shortcut violates diegetic command ownership, and aliasing weakens forensic ownership.
Scalability potential: Low tier keeps 0 us idle keyboard polling and only spends diagnostics budget after explicit panel command. Middle/High/Ultra keep the same indirect-quad density controls and can increase visual detail without adding a second input path.
Hardware Impact: Removes a per-render input query branch again; no profiler measurement is claimed. Static verification only: no F12 token, no stale dump token, shader debt scan clean. Latest compile evidence remains Loop 8 because Loop 9 intentionally skipped rebuild.

## Decision 20 - Loop 10 High-Tier Capacity And Command Mask Hardening
Problem: The visualizer calculated High/Ultra quad capacity independently from GPU buffer allocation. If `_maxQuads` was serialized below the High/Ultra floor or the quality tier changed upward, the vault could build more quads than the GPU buffers could upload, silently reducing God-mode density. The diegetic kill-switch parser also allowed `uint` overflow, which could flip the wrong mask after a long command.
Solution: Track actual GPU quad capacity in `_bufferQuadCapacity`, allocate buffers with `ResolveQuadCapacity()`, grow them with `EnsureBufferCapacity()` when tier demand increases, and clamp uploads against the real buffer capacity. Command parsing now rejects decimal/hex overflow; `AppendInt()` handles `int.MinValue` safely.
Rejected Alternatives: Leaving `_maxQuads` as both designer cap and GPU truth, rebuilding buffers every tick, or accepting overflow wrap as "user error." The first hides visual truncation, the second burns driver time, and the third makes a survival console unsafe.
Scalability potential: Low/MX350 keeps 512-2048 quads and no extra allocation churn. Middle stays capped at 4096. High/Ultra now guarantee at least the default 8192 indirect quads unless configured higher, preserving salt/silt/dent diagnostic overkill on top-tier machines.
Hardware Impact: No Unity profiler measurement was run. Runtime steady-state cost is one integer capacity comparison per SlowTick; buffer growth is one rare quality-tier transition allocation. Command parser hardening is 0 us idle and O(command length) on deliberate PDA input.

## Decision 21 - Loop 11 Source-Risk And Shader Guard Polish
Problem: Follow-up static review found a compiler-risky `char + uint` digit conversion in the fixed integer formatter, anonymous cold arrays in quad mesh setup, and a shader `rsqrt` guard that rejected zero/NaN axes but not absurdly large axes.
Solution: Cast digit arithmetic through `int`, move quad mesh vertices/UVs/indices into explicitly documented static cold arrays, and bound shader axis length before `rsqrt`.
Rejected Alternatives: Relying on compiler numeric promotion, leaving cold array allocation undocumented because it is not per-frame, or using shader `isfinite`. Numeric promotion ambiguity is avoidable; cold allocation still needs ownership evidence; shader `isfinite` is less portable across the target shader backends.
Scalability potential: Low/MX350 keeps identical draw count and no added hot-path work. High/Ultra keep the same dense visual-overkill path, but with safer vertex-axis fallback under corrupt camera/instance data.
Hardware Impact: No profiler measurement was run. Source-risk fix is compile hygiene only. Static cold arrays remove per-instance setup arrays. Shader guard adds one scalar compare in the vertex helper only when billboarding/oriented quads normalize axes.

## Decision 22 - Loop 12 Parallel Drift Reapplication
Problem: A final post-doc static scan found the same parallel overwrite drift again: F12 input polling was restored in `Render`, and runtime/editor dump paths reverted to `Dump_ARCHITECT_SPATIAL_PROBE.bin`.
Solution: Reapplied the same narrow diagnostics-domain patch: no keyboard polling in the render path, and both runtime dump and editor viewer point at `Dump_ARCHITECT_EYE_VISUALIZER.bin`.
Rejected Alternatives: Ignoring the drift because docs were already updated, or running a rebuild to hide a source-level regression. The source scan is the relevant evidence for this regression; the user explicitly rejected rebuild spam.
Scalability potential: Low/MX350 keeps 0 us idle keyboard polling. High/Ultra retain explicit diegetic control and correct blackbox identity for dense visual diagnostics.
Hardware Impact: Removes a per-render input query branch again; no profiler measurement is claimed. Verification is source/static only.

## Decision 23 - Loop 13 Probe Contract And Visual Stability
Problem: Fresh static review found three concrete mismatches: the vault probe utility exposed only read-only byte spans while the prompt requires mutable `Span<byte>` visualization access; memory-map fragmentation gaps were red instead of the required yellow; and High/Ultra salt/silt/dent overkill used `state.LastFrame` as a position seed, causing 5Hz visual popping. Concurrent drift also restored F12 polling and the stale spatial-probe dump name again.
Solution: Added `VaultProbeUtility.TryBufferBytes<T>()` with the same bounded `TryResolveBuffer<T>` guard and `GetUnsafePtr` path. Changed fragmented free gaps to yellow. Changed visual-overkill particles to stable per-index hash positions with a slow phase term only for growth/orbit alpha. Reapplied the no-F12 and `Dump_ARCHITECT_EYE_VISUALIZER.bin` patch.
Rejected Alternatives: Keeping read-only spans only, accepting red as "warning enough", or hiding visual popping behind higher counts. The prompt asked for raw Span probe access, task 14 says yellow gaps, and overkill should look stable on top-tier hardware rather than more chaotic.
Scalability potential: Low/MX350 unchanged: same low caps, no new renderer path, no idle command cost. Middle unchanged. High/Ultra keep dense overkill quads but with stable spatial seeds, so saved cycles buy richer visible detail without noisy popping.
Hardware Impact: No profiler measurement was run. Mutable probe access is 0 us unless explicitly called and adds only resolver scalar checks. Yellow color change is 0 us. Stable seeding replaces one frame-seeded hash input with constant hash input and one phase multiply per overkill quad; cost is effectively equivalent in the existing High/Ultra-only 5Hz path.

## Decision 24 - Loop 14 Compile-Wall And Drift Containment
Problem: A fresh no-rebuild verification pass exposed moving external compile walls and generated-project churn: the player presentation signal ABI alternated between an empty shim and the named signal file, `BioCableIK` had call sites for finite helpers before helper definitions stabilized, several typed-signal references were binding to the wrong namespace, and the final editor build lost `Temp/obj/Hecton8.Editor/project.assets.json` before C# compilation. Concurrent drift also kept restoring F12 polling and the stale spatial-probe dump name inside Architect Eye.
Solution: Kept the player signal ABI single-owner at final inspection: concurrent ownership returned `PlayerMovementPresentationSignals.cs` to an empty shim, so the packed player presentation structs remain compiled once from `GlobalSignals.cs`; `Directory.Build.targets` still explicitly includes the shim after the remove so generated project metadata stays consistent. Preserved the completed `BioCableIK` finite-helper patch, qualified `CameraJuiceImpactSignal` to `Hecton8.Core`, and qualified the audio signal bus to `Hecton8.Core.Contracts.Signals.AudioEvent`. Reapplied Architect Eye diagnostics fixes: no F12 render polling, correct dump identity, yellow fragmentation gaps, and static scans over the diagnostics domain.
Rejected Alternatives: Running repeated rebuild/clean loops, duplicating signal structs in both files, leaving hidden keyboard input in the render path, or claiming Loop 8 green build evidence as current truth. Rebuild loops fight the parallel workspace; duplicate ABI breaks typed lanes; hidden input violates the PDA command surface; stale compile evidence is a false report.
Scalability potential: Low/MX350 remains unchanged in Architect Eye: no idle keyboard polling, 5Hz bounded overlays, indirect quads only. Middle/High/Ultra retain the stable-seeded overkill path and can raise density without new data ownership or render paths.
Hardware Impact: No profiler measurement was run. All compile-wall repairs are source/contract hygiene with 0 us runtime impact to Architect Eye. The diagnostics drift fix again removes one render-path input query and restores correct forensic dump ownership; no microsecond claim beyond static evidence.
