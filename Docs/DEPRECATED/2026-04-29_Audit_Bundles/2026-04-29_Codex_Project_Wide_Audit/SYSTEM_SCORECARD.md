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
- registry/service ownership remains a real architectural spine rather than dead documentation

Verdict:

- The codebase still has a real dispatcher backbone.
- This remains one of the strongest architectural surfaces in the project.

### Interaction packetization

Evidence:

- `Interaction/EquipmentInteractionContracts.cs` defines struct-based `InteractionPacket` and `InteractionSignal`
- `Interaction/EquipmentInteractionHandler.cs` uses a persistent `NativeQueue<InteractionSignal>` and staged `RaycastCommand` buffers

Verdict:

- Tool interaction is being pushed through queue-friendly data contracts instead of string-driven sludge.
- This is closer to the declared architecture than many other subsystems.

### Zero-GC UI and event infrastructure exists

Evidence:

- `UI/SuitHUDV4CanvasOverlay.cs` is heavily invested in cached buffers and `SetCharArray`
- queue-backed buses now include `SaveEvents`, `QuestEvents`, `ScanEvents`, `NarrativeEvents`, and `AudioLogEvents`
- direct service ownership for audio/UI is explicit through `SpatialAudioManager` and `SuitHUDV4CanvasOverlay`

Verdict:

- The project has genuine zero-GC HUD and deferred-event work in production code.
- The problem is inconsistent adoption, not absence.

### Current editor console state is cleaner than older same-day first-party compile snapshots

Evidence:

- current Unity MCP readback shows `15` errors, but they are package-side `ManageAsset` conversion failures on `ResourceNodeTemplate_*` assets rather than first-party compile errors
- loaded active scene is `02_HECTON_WORLD`
- Build Settings remain aligned to `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`

Verdict:

- Older "currently broken first-party compile" wording is no longer safe.
- Current console is still not clean, and console cleanliness is still not the same thing as runtime proof.

## Acceptable

### Asset streaming governance

Evidence:

- `Optimization/AssetLifecycleGovernor.cs` and `Optimization/AssetLoadDispatcher.cs` exist and are non-trivial
- `ItemCatalog.cs` consumes ready tickets and calls `LoadAssetAsync<GameObject>()`

Verdict:

- This is not a fake subsystem.
- It is partially wired and materially more real than older audits suggested.
- It still needs end-to-end runtime verification under load before it can be called trustworthy.

### Save system depth

Evidence:

- `SaveBinaryStorage.cs` is still extremely large and uses serious persistence machinery
- save-related code shows explicit temp-path, handle, and integrity ownership

Verdict:

- The save stack is deep and serious.
- It is also dense, high-risk, and too large to trust without focused runtime validation.

### Event migration progress

Evidence:

- queue-backed buses coexist with direct static buses
- `SystemDispatcher.LateUpdate()` flushes deferred event lanes
- `ModdingAPI/HectonEventBus.cs` remains managed and synchronous

Verdict:

- The project is mid-migration, not uniform.
- Some older audit statements saying "delegate buses dominate everything" are now too blunt.

## Bad

### Bootstrap ownership is split and still lifetime-heavy

Evidence:

- `Bootstrap/BootstrapController.cs` and `Bootstrap/GameBootstrapper.cs` both remain startup authorities
- `SceneBootstrap.cs` adds a third startup/readiness owner
- direct `DontDestroyOnLoad` ownership still exists in bootstrap-facing systems

Real-game consequence:

- Scene transitions and startup behavior remain high regression zones.
- Lifetime ownership is still too distributed to call deterministic.

### UI and presentation ownership remain oversized

Evidence:

- `UI/SuitHUDV4CanvasOverlay.cs`: `4608` lines
- `HectonUnderwaterVisuals.cs`: `4826` lines
- large presentation/UI owners still combine formatting, effects, and service responsibilities

Real-game consequence:

- Even when these files contain strong local engineering, they are hard to change safely.
- Long-tail UI and presentation regressions remain likely.

### World and player god objects remain severe

Evidence:

- `World/HectonMapMagicVegetationBridge.cs`: `13279` lines
- `WorldProceduralScatterDirector.cs`: `10333` lines
- `HectonPlayerMovement.cs`: `7851` lines
- `HectonVoxelEngine.cs`: `4138` lines

Real-game consequence:

- These files are patch magnets and regression multipliers.
- Real verification cost is inflated by owner concentration alone.

### Broad runtime proof is still absent

Evidence:

- no play-mode replay
- no profiler capture
- no GCMonitor evidence
- no build verification

Real-game consequence:

- "console currently clean" and "system safe in a real session" are still different statements.

## Bottom Line

- Good: dispatcher spine, interaction packetization, zero-GC HUD/event infrastructure, direct service ownership clarity
- Acceptable: asset streaming governance, save-system depth, event migration progress
- Bad: bootstrap authority convergence, owner size concentration, missing runtime proof

This codebase has serious engineering in it. It is also carrying enough structural debt that current editor cleanliness is not yet the same thing as production trust.
