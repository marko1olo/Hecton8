# Manual Review Pass 17 - UI, Localization, Runtime Mesh, And Input Remap Boundaries

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/UI/DiegeticMenuCanvasUtility.cs`
- `Assets/_Project/Scripts/UI/DiegeticPDAController.cs`
- `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs`
- `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`
- `Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs`
- `Assets/_Project/Scripts/UI/PDAMapTab.cs`
- `Assets/_Project/Scripts/UI/RelayHUDRuntimeBootstrap.cs`
- `Assets/_Project/Scripts/Input/ControlRemapper.cs`
- `Assets/_Project/Scripts/LocRegistry.cs`
- `Assets/_Project/Scripts/UI/LocalizedFontResolver.cs`
- `Assets/_Project/Scripts/UI/FontAssetRecovery.cs`

## Findings

`DiegeticMenuCanvasUtility.ResolveCamera(...)` falls back to `Camera.main`, but method context shows this is used by menu panel setup, not a per-frame interaction loop. `NormalizeReadableText(...)` uses a static scratch list and explicitly marks the hierarchy scan as main-menu setup only. Current classification: `LEGAL_COLD_MENU_SETUP_WITH_INJECTED_CAMERA_PROOF_REQUIRED`; release proof still needs an assigned/injected menu camera so `Camera.main` does not become normal search behavior.

`DiegeticPDAController` resolves player/PDA/panel references in `Awake()` and `OnEnable()`, then rebuilds tablet renderer/collider/canvas visibility caches only when the tablet root changes. This is cold/rebind-shaped, not a confirmed hot-path violation. It remains yellow because `OnEnable()` can call the same setup/rebuild chain again, and because fallback UI interaction objects/event system/materials are still runtime-created if authoring is incomplete.

`AcousticRadarSphereRenderer` has a reasonable draw path: it uses fixed sample/matrix arrays, resolves camera from cached player context, and draws via `Graphics.DrawMeshInstanced` from `LateFrameTick()`. The release blocker is fallback asset creation. If `voxelMesh` is missing, `EnsureResources()` calls `CreateVoxelMesh()`, which creates a cube `Mesh`, heap vertex/index arrays, recalculates normals, and uploads the mesh at runtime. If `voxelShader` is assigned, it also creates a runtime material. This is acceptable only as a dev/recovery fallback; production sonar/radar UI needs authored mesh/material assignment proof.

`DiegeticVisorHudMesh` creates its projection mesh, retained managed arrays, material instance, and DataVault black-box ring during `OnEnable()`. It uses fixed 300-frame black-box telemetry and fault-only Temp payload dumps, which is the right crash-forensics shape. The unresolved issue is runtime mesh quality behavior: `RefreshQualityPolicy()` can set `_meshRebuildDirty` when `GlobalQualityWeight` changes, but the reviewed `LateFrameTick()` clears `_meshRebuildDirty` without calling `RebuildMesh()`. That avoids a runtime rebuild spike, but it also means continuous quality scaling for this projection mesh appears ineffective after bootstrap unless another path rebuilds it. This needs owner confirmation or a fix.

`DiegeticGlitchSurgeonRuntime` centralizes a large set of persistent H8Memory scratch buffers and DataVault handles for glitch state, tables, text, quads, radar blips, synth parameters, and telemetry. That is compatible with the zero-GC direction only if allocation happens during boot/prewarm. Its black-box dump uses a Temp byte payload and `NativeFaultDumpWriter.TryWriteAll(...)` on fault, not as a normal render path. Current classification: `YELLOW_UI_GLITCH_OWNER_STORAGE_WITH_PREWARM_AND_FAULT_DUMP_PROOF_REQUIRED`.

`LocRegistry` has strong span/UTF-8 lookup surfaces and several zero-allocation resolve APIs. However, language dictionary staging can allocate persistent H8Memory storage in `TryBeginBabelDictionaryStage(...)`, and override CSV scratch is allocated in `EnsureOverrideCsvScratch()`. These are legal during boot, import, or explicit language-change transactions, but not as a hidden first-use HUD lookup. Current classification: `YELLOW_LOCALIZATION_STAGE_STORAGE_PREWARM_AND_LANGUAGE_SWITCH_PROOF_REQUIRED`.

`ControlRemapper` uses Temp `NativeArray<byte>` / `NativeArray<InputActionStateDTO>` buffers in `TrySaveOverrides(...)`, `TryLoadOverrides(...)`, and telemetry dump routes. Method context shows explicit user-action IO / settings save-load behavior, not steady-state HUD text or gameplay tick. Current classification: `LEGAL_USER_ACTION_IO_WITH_STALL_UI_PROOF_REQUIRED`; acceptance needs a blocking/settings overlay proof and must not count these Temp buffers as per-frame UI cost.

`PDAMapTab`, `RelayHUDRuntimeBootstrap`, and several HUD helper classes still have recovery routes that create GameObjects, meshes, materials, or shader fallbacks at runtime when authoring is missing. `RelayHUDRuntimeBootstrap` specifically runs after scene load and creates a relay HUD marker if the active overlay has none. This is a useful fail-safe, but it is not release scene composition proof.

## Current Classification Updates

- `DiegeticMenuCanvasUtility`: `LEGAL_COLD_MENU_SETUP_WITH_INJECTED_CAMERA_PROOF_REQUIRED`.
- `DiegeticPDAController`: `YELLOW_PDA_REBIND_AND_RUNTIME_FALLBACK_UI_PROOF_REQUIRED`.
- `AcousticRadarSphereRenderer`: `YELLOW_RADAR_RUNTIME_FALLBACK_MESH_MATERIAL_PROOF_REQUIRED`.
- `DiegeticVisorHudMesh`: `YELLOW_VISOR_RUNTIME_MESH_BOOTSTRAP_AND_QUALITY_SCALING_FIX_REQUIRED`.
- `DiegeticGlitchSurgeonRuntime`: `YELLOW_UI_GLITCH_OWNER_STORAGE_WITH_PREWARM_AND_FAULT_DUMP_PROOF_REQUIRED`.
- `LocRegistry`: `YELLOW_LOCALIZATION_STAGE_STORAGE_PREWARM_AND_LANGUAGE_SWITCH_PROOF_REQUIRED`.
- `ControlRemapper`: `LEGAL_USER_ACTION_IO_WITH_STALL_UI_PROOF_REQUIRED`.
- `RelayHUDRuntimeBootstrap`: `YELLOW_RUNTIME_HUD_FAILSAFE_AUTHORING_PROOF_REQUIRED`.

## Required Proof / Fix

- Production prefabs must assign radar/sonar/visor/PDA/HUD meshes, materials, shaders, and relay marker hierarchy so runtime fallback builders do not execute in normal release scenes.
- A 300-frame menu/HUD/PDA/cockpit/visor interaction capture must show 0 B/frame steady-state UI, no post-bootstrap GameObject creation, no repeated material clones, no Canvas rebuild spikes, and no text allocation.
- Boot/prewarm counters must show `LocRegistry`, `UIStateStore`, `DiegeticGlitchSurgeonRuntime`, PDA, sonar, and visor resources are initialized before first player interaction.
- Language switch proof must show staged font/locale swap drains across bounded ticks, max-label-per-tick behavior, no dynamic atlas growth for fixed game text, and readable localized screenshots for long German/Russian/Arabic/CJK strings.
- `DiegeticVisorHudMesh` must either rebuild its mesh when quality segments change through an explicitly budgeted cold/transition window, or document that projection mesh quality is bootstrap-only and should not respond to runtime `GlobalQualityWeight` changes.
- Control remap save/load must be treated as an explicit settings transaction with UI blocking/feedback and telemetry; it is not a gameplay hot-path operation.

## Non-Closure

This pass improves static classification for UI/localization/input. It does not close RB-004, RB-104, RB-110, RB-119, RB-129, or the new UI/localization gate. No profiler, build, localization screenshot, Unity import, or device run was executed.
