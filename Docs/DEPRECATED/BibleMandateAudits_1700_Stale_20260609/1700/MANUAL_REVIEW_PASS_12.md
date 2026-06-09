# Manual Review Pass 12 - TBDR Mock Buffers, Construction Preview GPU Paths, Ambient Biota, Beacon, And Architect Eye

Status: STATIC METHOD REVIEW - NO UNITY / GPU / PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- `Assets/_Project/Scripts/UI/SettingsPanelProfiler.cs`
- `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs`
- `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs`
- `Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs`
- `Assets/_Project/Scripts/Construction/VRPipeBlueprintPreview.cs`
- `Assets/_Project/Scripts/BeaconRuntime.cs`
- `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`

## Findings

### 1. TBDR Pipeline Surgeon Still Has Mock/Fallback Storage Routes

`TBDRPipelineSurgeonRuntime` allocates through DataVault handles when available, but `AllocateNativeMockBuffers()` falls back to persistent `NativeArray` buffers for mock visible instances, sort scratch, mesh counts, histograms, visibility masks, indirect draw args, mock quality, and mock camera at `TBDRPipelineSurgeonRuntime.cs:489-519`. `TBDRPipelineSurgeonTypes` has the same pattern for budget counters, tile warnings, transparent quad counts, telemetry rings, and texture slice tables at `TBDRPipelineSurgeonTypes.cs:477-521` and `:1282-1291`.

The comments explicitly call some paths `CI/mock` or fallback. That is useful, but it is not release proof. Production acceptance requires build-symbol or boot proof that the mock/fallback native storage is not the normal render path when DataVault is missing or not initialized.

Classification: `YELLOW_TBDR_MOCK_FALLBACK_DATA_ROUTE_PROOF_REQUIRED`.

### 2. SettingsPanel Runtime Assembly Is Cold, But Must Be Treated As Recovery UI

`SettingsPanel` caches actions and numeric label buffers in cold initialization. Optional settings/accessibility rows are created with `new GameObject(...)` and component fetches in cold row builders around `SettingsPanel.cs:599-969`. `SettingsPanelProfiler` uses one cold `Stopwatch` and H8Debug logs for apply metrics.

The code shape is acceptable for boot/menu assembly only. It does not prove the final menu is AAA quality or allocation-free under interaction. Production settings UI should be scene-authored or prefab-authored; auto-created rows remain recovery, not normal release UI composition.

Classification: `YELLOW_SETTINGS_COLD_RECOVERY_UI_PROOF_REQUIRED`.

### 3. Construction Preview GPU Paths Use Good Double Buffering, But Runtime Material Fallbacks Remain Open

`FoundationPylonGpuBatch`, `HectonBlueprintPreviewBatch`, and `VRPipeBlueprintPreview` all use double-buffered `GraphicsBuffer` routes and `GraphicsBufferUploadUtility.UploadNativeArray()` for late-frame visual sync. This aligns with the bandwidth discipline better than naive per-object rendering.

The open risk is lifecycle and material assignment. `FoundationPylonGpuBatch` can create six graphics buffers on capacity changes and creates a runtime pylon material fallback at `FoundationPylonGpuBatch.cs:701-726`. `HectonBlueprintPreviewBatch` creates its preview material at `HectonBlueprintPreviewBatch.cs:1160`. `VRPipeBlueprintPreview` creates its preview material at `VRPipeBlueprintPreview.cs:804`.

Classification: `YELLOW_CONSTRUCTION_PREVIEW_GPU_BUFFER_MATERIAL_PROOF_REQUIRED`.

### 4. AmbientBiotaDirector Has Indirect Draw Shape, But Fallback Mesh/Material And Growth Need Proof

`AmbientBiotaDirector` uses dispatcher interfaces, indirect draw buffers, dirty payload upload, and continuous quality data. It creates double-buffered graphics buffers at `AmbientBiotaDirector.cs:1683-1686`, clones an owner-local biota material at `:1741`, and creates a runtime fallback quad mesh at `:2087-2107` when no authored quad mesh is assigned.

That can be a legal owner-presentation path, but not release closure. The fallback quad and material clone must be either assigned/preauthored for release or proven as one-time bounded recovery with SRP/material instance count proof.

Classification: `YELLOW_AMBIENT_BIOTA_INDIRECT_DRAW_FALLBACK_PROOF_REQUIRED`.

### 5. BeaconRuntime And ArchitectEyeVisualizer Are Diagnostic/Fallback Presentation, Not Gameplay Proof

`BeaconRuntime.GetFallbackBeaconMaterial()` creates a per-fallback material at `BeaconRuntime.cs:204-216` and owns its destruction. This is a clear fallback material route and should not be normal release beacon art.

`ArchitectEyeVisualizer` is a diagnostic visualization surface using slow/render registration, runtime quad mesh creation at `ArchitectEyeVisualizer.cs:1726-1732`, runtime material creation at `:1761-1766`, and graphics buffers at `:1812-1815`. Its black-box/fault visualization purpose can be valid, but release acceptance requires a debug flag, build inclusion policy, and fixed resource count proof.

Classification: `YELLOW_DIAGNOSTIC_AND_FALLBACK_PRESENTATION_ASSET_PROOF_REQUIRED`.

## Blocker Changes From Pass 12

- Add `RB-127`: TBDR mock/fallback DataVault route proof.
- Add `RB-128`: Construction preview, ambient biota, beacon, and ArchitectEye runtime GPU/material/fallback lifecycle proof.

## Current Honest Verdict

The reviewed systems are not toy code: several use dispatcher phases, double-buffered GPU resources, dirty upload paths, and owner teardown. They are still not release-clean from static review. Fallback materials, fallback meshes, mock native buffers, and auto-created UI rows must be proven as recovery/debug-only or replaced by authored assets and prewarmed resource ownership before the project can claim production-grade rendering/UI/construction presentation.
