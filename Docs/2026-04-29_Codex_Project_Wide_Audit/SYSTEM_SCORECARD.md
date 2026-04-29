# HECTON-8 Project-Wide System Scorecard

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Good

### Core dispatch contracts

Evidence:

- `Core/GlobalRegistry.cs` holds dense registry buckets and typed slots
- `Core/SystemDispatcher.cs` owns lane-based `Update`, `FixedUpdate`, `LateUpdate`, and slow-tick dispatch
- runtime native loop declarations across non-editor code are limited to `12`, with `SystemDispatcher` acting as the intended main owner

Verdict:

- The codebase still has a real dispatcher spine.
- This is one of the strongest architectural surfaces in the project.

### Interaction packetization

Evidence:

- `Interaction/EquipmentInteractionContracts.cs` defines struct-based `InteractionPacket` and `InteractionSignal`
- `Interaction/EquipmentInteractionHandler.cs` uses a persistent `NativeQueue<InteractionSignal>` and staged `RaycastCommand` buffers

Verdict:

- Tool interaction is being pushed through queue-friendly data contracts instead of string-based sludge.
- This is closer to the declared architecture than many other subsystems.

### Zero-GC UI infrastructure exists

Evidence:

- runtime-like scripts contain `TryFormat=32` and `SetCharArray=56`
- `UI/SuitHUDV4CanvasOverlay.cs` is heavily invested in `TryFormat` and `SetCharArray`
- `QuestEvents.cs` and `SaveEvents.cs` use `NativeQueue<T>` payload lanes rather than delegate-only broadcasting

Verdict:

- The project has genuine zero-GC UI and queue-event infrastructure.
- The problem is inconsistent adoption, not total absence.

### No live runtime coroutine gameplay path found in production-like code

Evidence:

- broad scan found `StartCoroutine=37` across all first-party scripts
- after excluding `Editor`, `Dev`, `SmokeTester`, and `Verifier`, executable runtime hits dropped to `0`
- the one remaining runtime-like text hit is comment text in `InteractionHighlighter.cs`

Verdict:

- This is better than older audit slices implied.
- Coroutine drift still exists in tooling/test surfaces, but not as a confirmed live gameplay path in the current production-like pass.

## Acceptable

### Asset streaming governance

Evidence:

- `Optimization/AssetLifecycleGovernor.cs` and `Optimization/AssetLoadDispatcher.cs` exist and are non-trivial
- runtime-like script scan found `Addressables=33`
- `ItemCatalog.cs` consumes dispatcher tickets via `TryConsumeReadyTicketByAssetKey(...)` and calls `LoadAssetAsync<GameObject>()`

Verdict:

- This is not a fake subsystem.
- It is partially wired and materially more real than older audits suggested.
- It still needs end-to-end runtime verification under load before it can be called trustworthy.

### Save system depth

Evidence:

- `SaveBinaryStorage.cs` is extremely large and uses memory-mapped file IO
- binary storage logic shows explicit file handles, temp paths, and compression/decompression jobs
- top `JobHandle.Complete()` sites still include `SaveBinaryStorage.cs`

Verdict:

- The save stack is deep and serious.
- It is also dense, high-risk, and too large to trust without focused runtime validation.

### World/vegetation/scatter implementation strength

Evidence:

- `World/HectonMapMagicVegetationBridge.cs` is `13205` lines
- `WorldProceduralScatterDirector.cs` is `10316` lines
- both clearly contain real systems rather than stubs

Verdict:

- There is substantial world-tech investment here.
- The price is fragility through extreme file size and high cognitive load.

### Event migration progress

Evidence:

- `SaveEvents.cs` and `QuestEvents.cs` now use `NativeQueue<T>` with `FlushPending()`
- `SystemDispatcher.LateUpdate()` explicitly flushes pending events
- `ModdingAPI/HectonEventBus.cs` still uses managed `List<>` channels and class events

Verdict:

- The project is mid-migration, not uniform.
- Some legacy audit statements saying "delegate buses dominate everything" are now too blunt.

## Bad

### Current build health

Evidence:

- Unity compile request completed during this audit
- console still reports active compile errors in:
  - `Visor/VolumetricLightFeature.cs`
  - `FluidCompartmentTemplate.cs`
  - `Construction/BaseDegradationSystem.cs`

Real-game consequence:

- The project is not in a trustworthy playable state while these blockers remain.
- Any gameplay-feel claim beyond partial scene/editor inspection is compromised.

### Bootstrap ownership is split and still singleton-heavy

Evidence:

- `Bootstrap/BootstrapController.cs` is explicit singleton/DDOL bootstrap authority
- `Bootstrap/GameBootstrapper.cs` is a second bootstrap authority with additional service registration logic
- runtime-like scripts still contain `DontDestroyOnLoad=61`
- top DDOL owners are led by `BootstrapController.cs` and `GameBootstrapper.cs`

Real-game consequence:

- Scene transitions and startup behavior remain high regression zones.
- Lifetime ownership is still too distributed to call deterministic.

### UI panel layer is still inconsistent and expensive

Evidence:

- runtime-like scripts still contain `TextAssignment=61`
- runtime-like scripts still contain `SetActive=159`
- `PDAInventoryTab.cs` alone contains `29` direct `.text =` hits and `6` `SetActive(...)` hits

Real-game consequence:

- PDA and secondary UI flows remain likely GC and rebuild hotspots.
- The codebase has a strong HUD core and a much weaker long-tail UI layer.

### Oversized god objects

Evidence:

- `31` scripts exceed `2000` lines
- `6` scripts exceed `4000` lines
- largest owners include world, player movement, visuals, and suit HUD

Real-game consequence:

- These files are hard to patch safely.
- Regressions become easier to introduce than to isolate.

### Job barrier density remains dangerous

Evidence:

- runtime-like scan found `JobComplete=128`
- top completion-heavy owners include:
  - `World/HectonMapMagicVegetationBridge.cs` (`11`)
  - `HectonWorldGenerator.cs` (`9`)
  - `Core/ConnectionSplineBatchRenderer.cs` (`6`)
  - `World/WorldSpatialHashGrid.cs` (`6`)
  - `SubmarineFluidDynamics.cs` (`5`)
  - `SaveBinaryStorage.cs` (`3`)

Real-game consequence:

- Even when some barriers are legal, the density is too high to trust without frame captures.
- Mid-frame stall risk remains significant.

### Rendering memory pressure is suspicious even before gameplay proof

Evidence:

- current Unity rendering stats snapshot reported:
  - `render_textures=1118`
  - `render_textures_bytes=1511534709`
  - `draw_calls=0`
  - `batches=0`

Real-game consequence:

- This may indicate aggressive editor-side RT retention, a real leak, or both.
- It is too large to ignore against an MX350 VRAM target.
- Runtime leak status is still unverified.

## Bottom Line

- Good: core dispatcher spine, interaction packetization, zero-GC HUD infrastructure, partial queue-event migration
- Acceptable: asset streaming governance, save stack depth, world-tech ambition
- Bad: current compile state, bootstrap ownership convergence, long-tail UI discipline, god-object size, barrier density, suspicious render-target memory

This codebase has serious engineering in it. It is also carrying enough structural debt that "it exists" and "it is safe in a real session" are still different statements.
