# Manual Review Pass 6 - Runtime Architecture Pattern Sweep

Status: STATIC HOTSPOT REVIEW - UNITY/PROFILER NOT RUN
Date: 2026-06-02

## Scope

This pass compared the execution-phase, zero-GC, native-memory, telemetry, and bootstrap mandates against non-editor runtime code paths. It focused on five patterns that can make a project look compliant in root bibles while the player build still behaves like a prototype:

- raw Unity phases: `Update`, `LateUpdate`, `FixedUpdate`, `OnGUI`
- scene lookup and sync load: `Find*`, `GameObject.Find`, `Resources.Load`, `WaitForCompletion`
- direct Unity debug logging outside a first-party runtime diagnostic route
- `Allocator.Temp`, `Allocator.TempJob`, local native allocation, and explicit `Complete`
- startup/recovery paths that can be legal only if they are bounded, dev-gated, or fault-only

The filter intentionally excluded `/Editor/` folders before escalating runtime findings. Editor windows, bake tools, static validators, and smoke-test harnesses are not runtime release defects by themselves.

## Findings

### Raw Unity Phase Loops

`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1490` contains a narrow `Update()` that only calls `EnsureBootstrapProgressAfterLifecycleResume()` until bootstrap is complete. Static reading suggests this is a bootstrap lifecycle recovery shim, not a long-running gameplay scheduler. It still requires boot proof because the execution-phase mandate forbids private runtime scheduling unless the file can prove it is bounded to boot/reload recovery and registers normal work through the dispatcher.

`Assets/_Project/Scripts/World/HectonWorldShellController1428.cs:33` is not an acceptable release player-controller route. It reads keyboard/mouse directly, uses `Camera.main`, mutates transform/camera state in `Update`, and has no dispatcher/input-provider boundary. This looks like a world-shell prototype controller or debug fly shell. It must be excluded from release gameplay or rewritten behind the input, camera, player, and systems bibles.

`Assets/_Project/Scripts/World/HectonWorldShellVisualDriver1428.cs:28` is a direct `LateUpdate()` visual animator. The hot body is simple sine transform/light animation, but `Awake()` also calls `FindObjectsByType` and allocates managed arrays/lists when serialized references are missing. This is only acceptable as an authored demo shell or dev visualizer. Production presentation must use serialized references, shader/VFX time parameters, pooled assets, or a `VISUAL_SYNC` route with proof.

Non-editor scan also found `DcsAscentProfileOverlay.cs`, `ThermodynamicsTunerWindow.cs`, `NarrativeDagInspectorWindow.cs`, and `ShinobuVoxelSculptorWindow.cs` with `OnGUI`. Their names indicate diagnostic/tuner usage, but they are outside `/Editor/` folders. They need assembly/define proof so they do not enter release player builds as IMGUI runtime overlays.

### Scene Lookup And Sync Wait

The non-editor scene/sync filter reduced the list to a small set:

- `ScavengingLootOracle.cs:1782` uses `Resources.FindObjectsOfTypeAll<GameObject>()` for HideAndDontSave orphan cleanup. The comment says reload cleanup, but release closure needs proof this cannot run on gameplay hot paths.
- `SaveManager.cs:542` uses `FindObjectsByType<SaveManager>` for manager duplication/lifecycle validation. This may be a cold bootstrap guard; it still needs bootstrap-only proof.
- `GpuScatterLodManager.cs:1725`, `SargassumMicroFaunaBoids.cs:8544`, and `GPUScatterDirector.cs:2303` call GPU readback `WaitForCompletion`. This is a serious proof gate. Even if used only during teardown/fault recovery, the code must show it cannot stall normal frames. If it is visible in gameplay cadence, it violates the GPU/readback and execution-phase mandates.
- `HectonWorldShellVisualDriver1428.cs:43` and `:55` use `FindObjectsByType` in `Awake` if arrays are unassigned. This is a cold path, but production prefabs must serialize references instead of searching the scene.

### Direct Debug Logging

Most `GameBootstrapper` direct `Debug.Log*` calls inspected in context are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` or `Conditional` helper methods. That means they are not automatically a release defect. Remaining uninspected direct calls in `GameBootstrapper`, `GlobalRegistry`, `BootstrapStatus`, `Power/LogisticsNetworkGraph`, and validation/smoke classes still need filtered method-level classification. Runtime error reporting should prefer the black-box ring, `RuntimeDiagnosticsTrace`, `GlobalTelemetryBus`, or H8Debug-style compile-stripped routes.

Direct debug calls inside `H8Debug.cs` are expected because that is the wrapper. Direct debug calls in source comments or scanners are not defects. Direct debug calls in runtime owner methods are yellow until gated or proven fault-only.

### Temp/TempJob And Native Allocation

Several apparent `Allocator.TempJob` findings are legal-looking fault, smoke, or warmup routes after method reading:

- `FabricationAssemblerRuntime.cs:1478` and `:1508` allocate and dispose a transient payload in a worker telemetry dump path.
- `FoveatedRenderCommander.cs:1208` and `:1261` allocate and dispose a transient payload for black-box dump bytes.
- `HectonSurvivalSystem.cs:3652` and `:3731` allocate staging arrays during cold survival database parse, not steady-state survival ticks.
- `WorldProceduralFieldSampler.cs:2577-2605` is explicitly a Burst prewarm job and completes through `DispatcherJobSwap.TryComplete`.

These are not closed by comments alone. Closure requires callsite proof: fault-only, boot-only, cold parse only, or pre-player-activation only. The broader non-editor `Temp/TempJob` scan still contains many dump payloads, smoke testers, world generator paths, map magic nodes, and runtime systems. They should be split into `FAULT_DUMP_PAYLOAD`, `COLD_BOOT_PARSE`, `SMOKE_TEST`, `OFFLINE_GENERATOR`, `RUNTIME_HOT_SUSPECT`, and `UNKNOWN` classes before any release claim.

`WorldProceduralScatterWorkingMemory.cs:155-170` allocates persistent scatter working memory at construction and registers it with `NativeMemorySentinel`; `EnsureCapacity()` can dispose/reallocate arrays at `:571-581`. This is not immediately illegal, but it needs a hard "no growth after gameplay begins" proof because the mandate rejects surprise runtime capacity growth.

### Fault-Only Native Payloads

The project has many `NativeArray<byte> payload = new NativeArray<byte>(..., Allocator.Temp...)` routes for binary dump/export payloads. That pattern is acceptable only when:

- invoked by NaN/fault/manual diagnostic/export, not per-frame gameplay
- labeled and bounded by max size
- not called from a hot read accessor
- not used as normal telemetry transport
- accompanied by a 300-frame black-box ring that already owns recent state

The docs and mandates are correct here; the codebase still needs full callsite classification.

## New Closure Requirements

1. Release builds must not include `HectonWorldShellController1428` as gameplay control unless it is rewritten behind `InputProvider`, `Camera`, `Player`, and dispatcher routes.
2. `HectonWorldShellVisualDriver1428` must either serialize all references and prove one cold startup cache only, or be dev/demo-only.
3. All non-editor `OnGUI` tuner/overlay files must be moved to editor assemblies or guarded from release players.
4. GPU readback `WaitForCompletion` callsites in scatter/microfauna must be proven teardown/fault-only or replaced with deferred async readback handling.
5. Direct runtime `Debug.Log*` callsites must be gated, wrapper-routed, or proven fatal/fault-only.
6. Temp/TempJob payload routes must be classified by callsite, not by comments.

## Verdict

Pass 6 does not change the global conclusion: the bible/mandate route is structurally strong, but the implementation is not release-clean. The biggest newly isolated risks are world-shell prototype runtime loops and non-editor GPU readback `WaitForCompletion` callsites. They do not invalidate the bibles; they prove why the bibles need code-level enforcement before release claims.
