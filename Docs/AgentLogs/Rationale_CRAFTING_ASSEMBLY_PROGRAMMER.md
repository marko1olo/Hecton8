# Rationale: CRAFTING_ASSEMBLY_PROGRAMMER

Status: PENDING VERIFICATION - GLOBAL BUILD RED OUTSIDE THIS DOMAIN

## Mandate Intake

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: use registry/signal lanes instead of direct singleton dependencies.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no hot-path allocation, no material clones for assembly path.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: shader clipping/wire illusion beats physical staged construction.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: transparent cutout, dithered coverage, low-tier feature gate.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`: local clip plane survives AUP shifts.
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`: item identity remains hash/quantity signal data.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`: power draw exposed as decoupled signal.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`: blackbox scalar for active fabricator count.

## Decisions

### D0: Initialize Persistent State
Problem: Batch protocol requires disk-backed state before implementation so context loss does not erase task ownership.
Solution: Created `Status_CRAFTING_ASSEMBLY_PROGRAMMER.md` and this rationale file before code edits.
Rejected Alternatives: Chat-only progress tracking; invalid because the CTO reads logs and compression can erase context.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime. Documentation-only.

### D1: Shader Clip Instead Of Physical Assembly
Problem: Item crafting appeared instantly and physical staged assembly would require mesh slicing or particle spam.
Solution: Added/used `Hecton_HologramAssembly.shader` with a local Y clipping plane, fake wire grid, and dithered cutout.
Rejected Alternatives: Instantiate partial meshes, particle bursts, or per-frame mesh mutation; all are CPU/GC traps.
Scalability potential: Low = clipped blue silhouette without burn edge. Middle = grid/fresnel. High = burn edge. Ultra = high density wire and stronger rim via material settings.
Hardware Impact: Low tier skips edge branch; estimated 15-35 us saved on MX350 for medium item previews versus always-on burn math.

### D2: Correct Clip Sign
Problem: Prompt literal `clip(worldY - _AssemblyHeightY)` contradicts "invisible above this height" because HLSL discards negative values.
Solution: Used `clip(_AssemblyHeightY - localY)` so fragments above the plane are negative and discarded.
Rejected Alternatives: Follow prompt literal and build downward/inside-out; visually wrong.
Scalability potential: Same behavior all tiers, local space only.
Hardware Impact: 0 us delta; correctness decision.

### D3: MaterialPropertyBlock Assembly State
Problem: Per-craft `new Material()` or `renderer.material` would clone materials and allocate managed/native state.
Solution: Fabricator owns one cached `MaterialPropertyBlock` for `_AssemblyHeightY`, bounds, pause color, and quality.
Rejected Alternatives: `material.SetFloat`, per-output material clone, or global shader property.
Scalability potential: Low/Mid/High/Ultra all share the same cached block; tier changes only one float.
Hardware Impact: Avoids material clone spikes; estimated 80-300 us and native material memory per craft avoided.

### D4: Native Signal Lanes
Problem: Crafting, power, and audio consequences must not hard-bind UI/audio/power systems.
Solution: Added typed `CraftingStartedSignal`, `CraftingCompletedSignal`, and `PowerDrainSignal` lanes in `GlobalSignals`; Fabricator also emits `ToolAcousticSignal`.
Rejected Alternatives: Direct calls into UI/audio/power listeners or managed events.
Scalability potential: Low devices can ignore high-frequency consumers; high devices can attach richer acoustic/power visualization consumers.
Hardware Impact: NativeQueue enqueue estimated below 1 us per SlowTick; zero managed allocation.

### D5: Visual Completion Before Inventory Signal
Problem: An acquire signal before visual completion would make UI/inventory reality outrun the holographic fake.
Solution: `CompleteAssemblyVisual()` runs before output routing and before `ItemAcquiredSignal`.
Rejected Alternatives: emit item-acquired on timer completion before renderer swap.
Scalability potential: Same ordering across tiers; high tier can add extra visual overkill after the same barrier.
Hardware Impact: 0 us meaningful; ordering only.

### D6: Power Pause As Presentation State
Problem: Brownout should pause construction without refund/cancel churn.
Solution: No-power branch re-enqueues the task, freezes current height, and sets `_PowerPause01` for red pulse.
Rejected Alternatives: cancel craft or run a coroutine until power returns.
Scalability potential: Low = red tint pulse only. Middle = red pulse plus wire. High/Ultra = red pulse plus full burn rim when powered again.
Hardware Impact: One MPB update per SlowTick while paused; estimated below 10 us CPU.

### D7: ASMDEF Isolation Block
Problem: Requested `Hecton8.Gameplay.Crafting` isolation conflicts with current root assembly placement of Fabricator, RecipeData, UI, inventory, power, world, and logistics dependencies.
Solution: Marked task blocked rather than moving root gameplay files into a circular assembly break.
Rejected Alternatives: fake empty asmdef, mass file move during concurrent multi-agent work.
Scalability potential: Build architecture only.
Hardware Impact: 0 us runtime.

### D8: Verification Wall
Problem: Full `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` fails before isolated crafting validation due missing namespaces and types from other active agents/domains.
Solution: Logged the wall, ran targeted static checks for singleton purge, particle play purge, signal presence, MPB use, and shadow disabling.
Rejected Alternatives: editing unrelated Core/Scheduling/Memory/Audio domains to make this task look green.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime.

### D9: Fabricator-Local Clip Plane
Problem: Preview-object local Y is not strict enough for assembly stations with rotated/scaled child preview meshes; it can make the build plane drift from the Fabricator frame after AUP origin shifts.
Solution: Added `_AssemblyWorldToFabricator`, computed source mesh Y bounds through `previewTransform.localToWorldMatrix` into `transform.worldToLocalMatrix`, and refreshed the MPB matrix through `IOriginShiftListener`.
Rejected Alternatives: absolute world Y plane, per-frame world-space rebuild mesh, or parenting restrictions that would break existing prefab layouts.
Scalability potential: Low = one matrix set and no burn edge. Middle = stable station-local reveal. High = stable reveal plus burn edge. Ultra = stable reveal with denser material tuning; no CPU mesh work is added.
Hardware Impact: Estimated 1-3 us CPU per active SlowTick for one MPB matrix write; avoids per-vertex CPU slicing and keeps MX350 path shader-only.

## OMEGA POLISH CHANGES

- Removed shader `pow()` from the assembly fresnel and replaced it with multiply/lerp shaping. Cinematic cheat: polynomial rim, not physically correct fresnel. Hardware Impact: saves SFU work per visible hologram fragment on MX350.
- Re-ran own-surface anti-bloat scan for `pow(`, `math.sqrt`, `math.normalize`, `foreach`, `new Material(`, `material.SetFloat`, and `renderer.material` on Fabricator plus the assembly shader. Result: no hits on the assembly implementation surface.
- Upgraded `_AssemblyHeightY` from preview-object local to Fabricator-transform local. The shader now transforms `positionWS` by `_AssemblyWorldToFabricator`, and Fabricator recomputes mesh bounds through the preview transform.
- Registered active Fabricators as `IOriginShiftListener` only in play mode, then refreshed AUP cache and the MPB after a shift. This keeps the visual fake stable without global polling.
- Final verified surface for this agent: `Fabricator.cs`, `GlobalSignals.cs`, `HectonFabricatorUI.cs`, `Hecton_HologramAssembly.shader`, `Status_CRAFTING_ASSEMBLY_PROGRAMMER.md`, and this rationale. Current `GlobalSignals.cs` working-tree diff contains unrelated concurrent inventory/acoustic edits; crafting start/completion and power-drain lanes are verified present and consumed by Fabricator.
- Build status remains PENDING because the project compile is blocked by unrelated missing-domain contracts/types. Unity MCP script validation was unavailable because no Unity session was connected.
