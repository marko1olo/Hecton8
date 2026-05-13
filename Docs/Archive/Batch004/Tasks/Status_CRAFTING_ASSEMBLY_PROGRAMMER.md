# Status: CRAFTING_ASSEMBLY_PROGRAMMER

Prompt: Holographic Assembly
Domain: Crafting / Gameplay Assembly
Status: PENDING VERIFICATION - GLOBAL BUILD RED OUTSIDE THIS DOMAIN

## Task Checklist

- [x] 1. SINGLETON ERADICATION: `rg` found no `CraftingManager.Instance` / `CraftingManager` in active crafting code. DOD: static purge scan. Rejected: adding compatibility singleton. Estimate: 0 us.
- [x] 2. SIGNAL MIGRATION: Verified typed `CraftingStartedSignal` and `CraftingCompletedSignal` lanes in `GlobalSignals`; Fabricator publishes both while keeping existing `CraftingEvents` sidecar listeners alive. DOD: NativeQueue/SignalBus path. Rejected: managed event callbacks. Estimate: 0.7 us per enqueue.
- [!] 3. ASMDEF ISOLATION: BLOCKED BY EXISTING MONOLITH. No `Hecton8.Gameplay.Crafting` asmdef exists; Fabricator/RecipeData/CraftingEvents live in the root `Hecton8.Core` assembly and moving them would create circular references with UI, inventory, power, world, and logistics systems. DOD: asmdef scan. Rejected: fake empty asmdef. Estimate: 0 us.
- [x] 4. DEAD CODE HUNT: Removed `fabricationSparks.Play(false)` from Fabricator sequence; authored particle emission can be enabled by rate only, no runtime particle start spam. DOD: targeted `rg fabricationSparks.Play|ParticleSystem.Play`. Rejected: instantiating new sparks per craft. Estimate: saves 40-120 us on start spikes.
- [x] 5. THE HOLOGRAPHIC SHADER: `Hecton_HologramAssembly.shader` exists as URP transparent-cutout hologram; Fabricator now has a shared cold-path fallback mesh for craftables without `worldPrefab`. DOD: shader/file inspection and item asset scan. Rejected: blank preview or per-craft prefab spawn. Estimate: shader cost only; fallback mesh is one shared cold allocation.
- [x] 6. THE CLIPPING PLANE: `_AssemblyHeightY` clips with Fabricator-local `clip(_AssemblyHeightY - fabricatorLocalY)` so geometry above the plane is discarded. DOD: sign checked against HLSL `clip` semantics. Rejected: prompt literal sign because it discards the built lower half. Estimate: 0.2 us GPU/fragment batch dependent.
- [x] 7. THE BURN EDGE: High-tier shader branch adds hot blue-white rim within `_AssemblyEdgeWidth` default 0.05. DOD: shader branch audit. Rejected: CPU mesh slicing. Estimate: <0.05 ms on target item mesh.
- [x] 8. PROGRESS LERP: Fabricator `SlowTick` applies `math.lerp(_assemblyBaseY, _assemblyTopY, progress)` from the same task progress scalar backing `CraftingProgress01`. DOD: code readback. Rejected: per-frame Update. Estimate: 1 MPB set per SlowTick.
- [x] 9. MATERIAL SWAP: `CompleteAssemblyVisual()` swaps from hologram shared material to source `sharedMaterial` at progress 1.0. DOD: completion path audit. Rejected: `new Material()` cloning. Estimate: cold path only.
- [x] 10. WELDING AUDIO: Fabricator emits `ToolAcousticSignal` state 3 while progress is below 1.0; pitch lerps with progress. DOD: GlobalSignals publish path. Rejected: local-only audio source as sole signal. Estimate: 0.7 us per SlowTick enqueue.
- [x] 11. INVENTORY COMMIT: `ItemAcquiredSignal` is published only after `CompleteAssemblyVisual()` reaches 1.0 and output delivery succeeds. DOD: completion order audit. Rejected: acquire signal on timer start. Estimate: 0.8 us once per craft.
- [x] 12. AUP SHIFT SAFETY: Shader consumes Fabricator-local Y via `_AssemblyWorldToFabricator`; Fabricator refreshes the matrix after origin shifts and acquired output still records AUP only at delivery. DOD: shader/Fabricator readback plus `IOriginShiftListener` audit. Rejected: absolute world clip plane. Estimate: one MPB matrix write per SlowTick or shift.
- [x] 13. MATH LOD: Low/MX350/Unknown tier sets `_AssemblyQuality = 0`; shader branch skips burn edge calculation. DOD: quality resolver audit. Rejected: always-on rim math. Estimate: saves smoothstep/abs edge work per fragment on low tier.
- [x] 14. ZERO-GC: Assembly path uses one cached `MaterialPropertyBlock`; no `new Material()` added to Fabricator assembly flow. DOD: `rg new Material` review. Rejected: material instance per craft. Estimate: avoids managed allocation and material clone churn.
- [x] 15. ABORT LOGIC: Power loss and emergency lock call `ApplyAssemblyVisualProgress(current, true)`; no height advancement and shader pulses red. DOD: no-power branch and power callback audit. Rejected: canceling craft on brownout. Estimate: one MPB update per SlowTick while paused.
- [x] 16. TELEMETRY: Fabricator active count is published to `GlobalTelemetryBus.PublishModTelemetry`. DOD: register/unregister/start/complete path audit. Rejected: string log telemetry. Estimate: blackbox ring write only.
- [x] 17. EVENT BUS: `PowerDrainSignal` typed lane added; Fabricator publishes watts proportional to progress-per-second. DOD: GlobalSignals enqueue path. Rejected: direct power UI dependency. Estimate: 0.7 us per active SlowTick.
- [x] 18. CROSS-DOMAIN AUDIT: UI reveal now reads `Fabricator.CraftingProgress01`; legacy progress events still mirror the same scalar. DOD: `HectonFabricatorUI.ResolveHologramRevealProgress` audit. Rejected: separate UI timer. Estimate: 0 us additional.
- [x] 19. OMEGA COMPILE CHECK: Hologram shader has no ShadowCaster pass and Fabricator forces preview renderer `ShadowCastingMode.Off`. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` was attempted and failed on pre-existing missing-domain types (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, etc.), not on this crafting surface. Unity MCP validation unavailable: no Unity session. Estimate: no runtime cost.

## Iteration Notes

- Iteration 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; status/rationale initialized. No code touched.
- Iteration 1: Tasks 1-5 implemented/static-verified; build attempt hit global dependency wall outside crafting.
- Iteration 2: Tasks 6-10 read back in shader and Fabricator. Corrected clip sign for actual "invisible above" behavior.
- Iteration 3: Tasks 11-14 audited completion order, AUP locality, low-tier shader branch, and MPB use.
- Iteration 4: Tasks 15-18 audited brownout pulse, blackbox telemetry, power drain lane, and UI scalar.
- Iteration 5: Task 19 audited shadow disabling, build output, and no Unity MCP session.
- Omega Polish: Read `<POLISH_MANDATE>` after checklist completion/block. Removed shader `pow()` from assembly fresnel, reran anti-bloat scans, and documented blocked build dependency.
- Iteration 6: Upgraded clipping from preview-object local to Fabricator-transform local using `_AssemblyWorldToFabricator`, recalculated source mesh bounds through the preview transform, and registered the Fabricator as an origin-shift listener for active previews.
- Iteration 7: Re-extracted `<AGENT_PROMPT id="CRAFTING_ASSEMBLY_PROGRAMMER">` from `CURRENT_BATCH.md` with an attribute-tolerant CLI regex; assignment remains 19 tasks and status remains pending verification.
- Iteration 8: Asset scan found multiple craftable items without `worldPrefab`; added a shared fallback hologram mesh and retained full hologram state when no actual material exists to swap to.
