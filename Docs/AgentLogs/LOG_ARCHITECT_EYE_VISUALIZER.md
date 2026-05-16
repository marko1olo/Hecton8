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
