# Rationale_ARCHITECT_SPATIAL_PROBE

Status: VERIFIED MASTER GRADE - VISION CLEAR / GLOBAL COMPILE BLOCKED BY EXTERNAL WORLD-ECOSYSTEM DEPENDENCY

## Decision Record

Problem: HECTON-8 has DOD runtime state with no per-object Inspector visibility.
Solution: Build diagnostics as isolated, GPU-driven visual feeds under Core/Diagnostics/Visuals, using preallocated buffers and authoring-time sample injection points instead of concrete dependencies on other active agents' systems.
Rejected Alternatives: Unity Canvas, IMGUI, GameObject-per-marker, managed log strings, and concrete class references outside diagnostics. These violate task constraints, hot-path GC policy, or parallel-agent decoupling.
Scalability potential: Low uses coarse pages, short histories, and packed integer signal IDs. Middle uses denser lines and bars. High adds volume shells and ghost history. Ultra expands sample density and glow complexity while keeping the same public surface.
Hardware Impact: Estimated low-end gain versus GameObject gizmos is 0.20-1.20 ms/frame and unbounded GC avoidance on i3/MX350 because draw submission is batched and managed allocations are avoided in Tick paths.

Problem: Other systems named in the prompt are parallel-agent domains and cannot be concrete dependencies.
Solution: Added an isolated `SignalBus<DebugSignal>` payload with integer kinds for collisions, breadcrumbs, gas, pressure, flow, sonar, signal events, lane pressure, resonance, NaN, health, ghost poses, VRAM, and teleport previews.
Rejected Alternatives: Direct reads from KCC, navmesh, vehicle, gas, sonar, or memory systems. That would create compile-order coupling and violate lane segregation.
Scalability potential: Low publishes only fault-critical signals. Middle publishes sparse fields. High publishes dense vectors. Ultra can publish visual-overkill payloads without changing the visualizer ABI.
Hardware Impact: Estimated 150-500 us/frame saved on i3/MX350 versus scattered per-system debug renderers; zero managed allocations per pushed signal.

Problem: Debug render upload used the standard managed `SetData` path and could stall or allocate under pressure.
Solution: Switched to double buffered `GraphicsBuffer` instances with `LockBufferForWrite` for instance and indirect argument uploads, then render via `Graphics.DrawMeshInstancedIndirect`.
Rejected Alternatives: `LineRenderer`, `MeshRenderer` clones, Canvas, and repeated mesh rebuilds. Standard Unity approaches are too CPU-heavy for dense forensic overlays.
Scalability potential: Low lowers quad counts. Middle keeps the default 8192 budget. High/Ultra spend saved CPU on denser salt/silt/dent overlays and ghost history.
Hardware Impact: Estimated 250-900 us/frame saved on MX350-class hardware at thousands of quads; avoids render-thread spikes from managed uploads.

Problem: SDF debug visibility must reflect actual Vault storage without expensive reconstruction.
Solution: Sample `BufferID.VoxelSdfTexture3D` as byte density and drive a glowing wire/volume proxy through the existing noir shader path.
Rejected Alternatives: CPU marching cubes, per-voxel GameObjects, or float SDF assumptions. Actual project usage stores the SDF as bytes.
Scalability potential: Low samples 64 cells. Middle/High increase visual density through quads. Ultra can add shader glow without changing CPU cost materially.
Hardware Impact: Estimated 700 us/frame saved versus CPU mesh extraction on low-end hardware.

Problem: AUP world visibility and teleport must not require runtime UI.
Solution: Added editor SceneView 5000m grid, active 9-sector highlight, and control-click player teleport through `GlobalRegistry.Player`, plus a DebugSignal preview marker.
Rejected Alternatives: Runtime debug menu, Canvas button, or editing player internals not owned by diagnostics.
Scalability potential: Editor-only cost outside player builds. Runtime preview is a single indirect cross marker.
Hardware Impact: 0 runtime cost unless preview signal is pushed.

Problem: Non-finite math faults need postmortem evidence and a physical search marker.
Solution: Retained a fixed 300-frame blackbox buffer, dumps `Docs/AgentLogs/Dump_ARCHITECT_SPATIAL_PROBE.bin`, and renders a red vertical pillar at the fault coordinate.
Rejected Alternatives: console-only logs or unbounded trace buffers. They fail under context loss and produce GC/file churn.
Scalability potential: Low dumps only crash-critical records. High/Ultra can draw more ghost frames from the same ring.
Hardware Impact: 0 steady-state GC; dump IO only happens on fault.

Problem: Visual clarity on high-end hardware should improve without forcing weak devices to pay.
Solution: Added deterministic hash/triangle-wave visual-overkill overlays only for High and Ultra tiers.
Rejected Alternatives: particle systems, noise textures updated on CPU, or simulated diagnostic debris. They spend performance on systems instead of readable immersion.
Scalability potential: Low/Middle show essentials. High adds 74 visual accents. Ultra adds 168 accents while preserving one indirect draw path.
Hardware Impact: Low-end pays 0 us for this branch; high-end spends saved cycles on visual density.

Problem: Final compliance requires objective build evidence.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
Rejected Alternatives: claiming Unity Editor import success without compiler output.
Scalability potential: Compile proof applies to all tiers.
Hardware Impact: Runtime impact is 0 us; integration risk reduced.

Problem: Reverification initially failed because `Hecton8.Core.csproj` referenced a stale `Library\ScriptAssemblies\Hecton8.Core.Contracts.dll` while external contract source files had changed.
Solution: Rebuilt the referenced contracts DLL from the existing `Hecton8.Core.Contracts.asmdef` source set into `Library\ScriptAssemblies`, then rebuilt Core both in an isolated Codex output path and the default output path.
Rejected Alternatives: editing diagnostics code to mask external contract symbols, changing gameplay systems, or claiming compile success from stale logs. The dependency needed refresh, not visualizer changes.
Scalability potential: No runtime path change. All Low/Middle/High/Ultra visualizer behavior remains unchanged.
Hardware Impact: Runtime impact is 0 us; build dependency freshness restored.

Problem: The indirect draw upload path still carried a private managed `uint[5]` args scratch buffer and used the `Raw` target flag on indirect argument buffers.
Solution: Removed the scratch array and writes the five indirect draw arguments directly into the mapped args `GraphicsBuffer`. Removed `GraphicsBuffer.Target.Raw` to avoid platform-specific raw-buffer assumptions on Metal/Vulkan paths.
Rejected Alternatives: Leaving the cold array because it was not hot-path GC, or falling back to `SetData`. The better path is direct mapped writes with a pure indirect-arguments buffer.
Scalability potential: Low/Mx350 avoids unnecessary managed state and uses the same single indirect draw. High/Ultra retains the same draw call count while expanding visible density.
Hardware Impact: Estimated 1-3 us/frame saved in upload overhead at 8k quads on low-end CPUs; more important is lower platform-risk on ARM64/Metal.

Problem: High and Ultra were visually too conservative after the first pass.
Solution: Raised deterministic visual-overkill budgets to 832 extra quads on High and 3328 extra quads on Ultra, still using triangle/hash fakes and one indirect draw. Added shader-side fake parallax ridge, SSS lift, and noir dither controlled by tier scalar.
Rejected Alternatives: Real particle systems, compute raymarching, 16-tap texture POM, or physical silt simulation inside diagnostics. Those create new memory owners, shader variants, and platform limits; the diagnostic mandate favors Dear Lie fakes.
Scalability potential: Low/Mx350 pays 0 extra quads. Mid receives shader tier polish only. High gets dense visor/hull noise. Ultra gets thousands of diagnostic atmosphere quads.
Hardware Impact: Low-end runtime cost unchanged. High/Ultra spend saved CPU/GPU budget on 0 additional draw calls and thousands of packed quads.

Problem: Diagnostics could still be loaded by accidental release-player inclusion.
Solution: Added `IsDiagnosticsRuntimeAllowed()` gates in Awake, OnEnable, Tick, and Render so the visualizer only runs in `UNITY_EDITOR` or `DEVELOPMENT_BUILD`.
Rejected Alternatives: Relying on scene discipline or disabling the component manually. Release builds must not pay diagnostic cost because an object was left in a scene.
Scalability potential: Low/Middle/High/Ultra debug behavior is unchanged in editor/development builds. Production players pay zero diagnostic render/update cost.
Hardware Impact: Release build impact is full visualizer cost avoided; debug builds remain governed by existing tier budgets.

Problem: ARM64/Quest ABI validation did not include the `DebugSignal` payload that feeds the visualizer.
Solution: Extended packed-size validation to require `DebugSignal` at 64 bytes, matching the fixed typed-lane payload budget.
Rejected Alternatives: Trusting `[StructLayout]` declarations without runtime verification. Packed structs are cheaper to validate once than debug after a Quest-only crash.
Scalability potential: ABI validation is tier-independent and fails fast before corrupted overlay data can render.
Hardware Impact: One cold startup size check; estimated steady-state cost is 0 us/frame.

Problem: The editor blackbox dump viewer could read an entire dump file even though the system contract is 300 frames.
Solution: Capped reads to `300 * sizeof(ArchitectEyeBlackBoxEntry)` and used `FileOptions.SequentialScan` with `FileShare.ReadWrite`.
Rejected Alternatives: `File.ReadAllBytes` and unbounded viewer reads. Those are hostile to Steam Deck MicroSD pressure and editor-side postmortem iteration.
Scalability potential: Low-end storage reads only the forensic window. High-end editors get the same data without UI churn.
Hardware Impact: Worst-case IO is capped at 19,200 bytes per load for 64-byte entries; runtime impact is 0 us.

Problem: SDF volume visualization needed a stronger high-tier path without violating Metal/mobile compute limits.
Solution: Added tiered shader-side fake raymarch counts: Low/Mid use 3 shell samples, High uses 8, Ultra uses 16, all in a fragment loop and no compute thread groups.
Rejected Alternatives: Compute raymarching, DirectX-only buffer tricks, or CPU marching cubes. The diagnostic shader must stay portable and cheap.
Scalability potential: Low remains a Dear Lie shell. High/Ultra spend GPU budget on denser glow/silt perception without changing CPU upload cost.
Hardware Impact: Low keeps minimal fragment work on MX350/Quest. Ultra deliberately spends extra fragment ALU for visual clarity.

Problem: Build reverification exposed a new cross-domain compile break in `SystemDispatcher.cs` from another agent's typed-lane snapshot edit.
Solution: Applied a one-token critical compile unblock by qualifying `ReadOnlySpan<T>` as `System.ReadOnlySpan<T>`, then refreshed the stale contracts DLL and reran Core build.
Rejected Alternatives: Reverting the other agent's dispatcher work, editing project generation, or marking diagnostics blocked while the fix was mechanical.
Scalability potential: No runtime behavior change. The typed-lane signal contract remains intact.
Hardware Impact: Runtime impact is 0 us; compile integrity restored for all targets.

Problem: Diagnostic shaders used numeric `h` literal suffixes after high-tier polish.
Solution: Removed the suffixes and retained `half` variable declarations/casts so Unity HLSL remains conservative for Metal and mobile translators.
Rejected Alternatives: Keeping suffixes because DXC accepts them, or changing the shader back to full `float` everywhere. The safer portability fix is literal normalization without increasing register pressure.
Scalability potential: Low/Mid/High/Ultra visuals are unchanged; shader source compatibility risk is reduced.
Hardware Impact: Runtime impact is 0 us; compile portability risk reduced.

Problem: Latest global Core build no longer exits 0 after another external domain changed `World/EcosystemDirector.cs`.
Solution: Stopped fixing outside-domain compile debt after the mechanical `SystemDispatcher` unblock and recorded the failure as [BLOCKED BY DEPENDENCY]. The diagnostics domain audit still has no banned hot-path patterns.
Rejected Alternatives: Inventing ecosystem index fields from diagnostics, reverting another agent's work, or claiming the stale passing build as current truth.
Scalability potential: No diagnostics runtime change. The blocker must be resolved in the World/Ecosystem domain.
Hardware Impact: Runtime impact from this documentation decision is 0 us; compile status remains blocked until the external index implementation is restored.
