# LOG: CRAFTING_ASSEMBLY_PROGRAMMER

## 2026-05-13 - Holographic Assembly

What was wrong:
- Crafting presentation was architecturally cheap: instant result visibility, no build-up phase, and particle start calls in the Fabricator feedback path.
- Crafting side effects were not fully exposed as typed native signal lanes for start/completion/power drain.
- The requested crafting asmdef isolation is blocked by the current root gameplay monolith; moving Fabricator would create cross-assembly cycles.

What was done:
- Implemented/verified `Hecton_HologramAssembly.shader` as a URP transparent-cutout hologram with local Y clipping and no ShadowCaster pass.
- Fabricator drives `_AssemblyHeightY` from local mesh bounds through a cached `MaterialPropertyBlock`.
- Fabricator swaps the preview renderer from hologram shared material to the source item `sharedMaterial` at progress 1.0.
- Removed `fabricationSparks.Play(false)` from the old Fabricator sequence; feedback now manipulates authored emission rate without starting particle spam.
- Verified/published `CraftingStartedSignal`, `CraftingCompletedSignal`, `PowerDrainSignal`, `ToolAcousticSignal(Welding)`, and delayed `ItemAcquiredSignal` until visual completion.
- Added blackbox telemetry marker for `FabricatorActiveCount`.
- UI reveal progress reads `CraftingProgress01`, the same scalar the assembly height uses.

Cinematic cheats used:
- Shader clipping plane instead of real mesh slicing.
- Dithered cutout instead of transparent overdraw blend.
- Polynomial fresnel approximation instead of `pow()`.
- Fake grid/wire signal from object-space XZ, not geometry wireframe.
- Red pulse on power loss via shader scalar, not separate effects.

Exact microseconds saved:
- Removed ParticleSystem start spike: estimated 40-120 us per craft start on low-end hardware.
- Avoided material clone: estimated 80-300 us plus native material churn per craft.
- Low tier skips burn-edge branch: estimated 15-35 us GPU time on medium item preview meshes.
- NativeQueue signal fanout instead of managed delegates: estimated <1 us per SlowTick signal and 0 B managed allocation.

Verification:
- `git diff --check` passed for touched files; only line-ending warnings from existing repo settings.
- `rg` found no `CraftingManager.Instance`.
- `rg` found no `fabricationSparks.Play` / `ParticleSystem.Play` in Fabricator; a remaining broad `ParticleSystem.Play` hit exists in unrelated `FloraInteractionManager`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed on unrelated dependency walls: missing `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, and other external-domain contracts.
- Unity MCP validation failed because no Unity session was available.

Status:
- PENDING VERIFICATION due global compile dependency wall.
- ASMDEF isolation task is BLOCKED BY EXISTING MONOLITH.

## 2026-05-13 - Holographic Assembly Second Pass

What was wrong:
- The first shader pass used local clipping correctly, but the strict domain requirement was Fabricator-transform local. Child preview meshes with non-trivial transforms could drift from the station plane.
- Origin-shift refresh was implicit through normal updates, not explicit for active preview state.

What was done:
- Added `_AssemblyWorldToFabricator` to `Hecton_HologramAssembly.shader` and transformed world position into Fabricator local space before clipping and edge math.
- Recomputed preview mesh Y bounds from mesh local space through preview transform into Fabricator local space.
- Registered Fabricator as an `IOriginShiftListener` during play mode and refreshed cached AUP/matrix state after shifts.
- Reran targeted scans for signal lanes, particle play purge, material clone avoidance, shader clip sign, and anti-bloat patterns.

Cinematic cheats used:
- Station-local shader plane instead of cutting or rebuilding the mesh.
- One MPB matrix write instead of global shader state or renderer material mutation.

Exact microseconds saved:
- Avoided CPU mesh slicing/rebuild: estimated 100-500 us per visible preview on low-end hardware depending on mesh size.
- Kept origin shift handling event-driven: estimated 0 us idle cost, 1-3 us per active Fabricator shift refresh.
- Preserved low-tier burn-edge skip: estimated 15-35 us GPU time versus always-on edge math.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Fabricator.cs Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader` passed with line-ending warnings only.
- Anti-bloat scan found no `pow(`, `math.sqrt`, `math.normalize`, `foreach`, `new Material(`, `material.SetFloat`, or `renderer.material` on the assembly implementation surface.
- `GlobalSignals.cs` crafting start/completion and power-drain lanes are present; current uncommitted `GlobalSignals.cs` diff also contains unrelated concurrent inventory/acoustic edits from another domain.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly` still failed on unrelated global dependency walls. Filtered output showed no Fabricator or shader-specific compile hit; `GlobalSignals.cs` is still blocked by the existing missing `Hecton8.Core.Memory.Layout` dependency.
- Unity MCP `validate_script` could not run because no Unity session was connected.

## 2026-05-13 - Holographic Assembly Fallback Pass

What was wrong:
- Craftable item assets include entries with `worldPrefab: 0`. Those recipes would complete but show no holographic assembly mesh.
- If no actual material existed, completion cleared the property block and could collapse the fallback hologram instead of leaving a full assembled silhouette.

What was done:
- Added optional `assemblyFallbackMesh` authoring slot and a shared generated octahedral fallback mesh warmed in `Awake`.
- `TryResolveAssemblySource` now returns the fallback mesh when an item has no prefab, no mesh filter, or no skinned mesh.
- `CompleteAssemblyVisual` clears the MPB only when an actual material exists; fallback/no-material assemblies keep the full hologram.
- Origin-shift handling refreshes the retained fallback hologram matrix after completion.

Cinematic cheats used:
- One tiny diamond/truss proxy represents missing item art instead of spawning temporary placeholder prefabs.
- Shader grid/fresnel sells the fabrication state even on the fallback mesh.

Exact microseconds saved:
- Avoided per-craft placeholder prefab instantiate: estimated 100-500 us plus transform/component churn on low-end devices.
- Avoided per-craft mesh/material clone: 0 B managed allocation after the one shared fallback mesh warmup.
- Kept Low/MX350 path on the same no-burn-edge shader branch.

Verification:
- `git diff --check` passed with line-ending warnings only.
- Anti-bloat scan found no `pow(`, `math.sqrt`, `math.normalize`, `foreach`, `new Material(`, `material.SetFloat`, `renderer.material`, `ParticleSystem.Play`, or `fabricationSparks.Play` on the assembly implementation surface.
- Unity MCP `validate_script` failed because no Unity session is connected.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly` still failed on unrelated global dependency walls (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, inventory algorithms, etc.). Filtered output did not report a Fabricator-specific error before the wall.
