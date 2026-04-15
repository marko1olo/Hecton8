# Implementation Plan: VRAM/RenderTexture Optimization System

## Overview

This implementation plan breaks down the VRAM/RenderTexture Optimization System into discrete coding tasks. The system enforces strict memory budgets (Texture < 900 MB, RenderTexture < 500 MB, Total VRAM < 1.2 GB) on NVIDIA MX350 2GB VRAM hardware through runtime monitoring, lifecycle tracking, O(1) pooling, and automated optimization.

**Architecture**: 7 core components (VRAMMonitor, LifecycleTracker, RTPool, 4 subsystem managers) + 2 Editor tools (FormatOptimizer, ResolutionAnalyzer) + Bootstrap initialization.

**Key Constraints**: Zero GC in hot paths, ISlowTickable (~0.5s interval), O(1) pooling, MaterialPropertyBlock (no renderer.material), singleton pattern, scene cleanup on unload.

**Integration Points**: GameTickManager (ISlowTickable), VisorHUDController (RT disposal fix), SceneManager (pool cleanup), RuntimePerformanceProfiler (VRAM reporting).

## Tasks

- [x] 1. Set up project structure and core data models
  - Create `Assets/_Project/Scripts/Optimization/` directory
  - Create namespace `Hecton8.Optimization` for runtime components
  - Create namespace `Hecton8.Optimization.Editor` for Editor tools
  - Define `RenderTextureAllocationRecord` struct with memory calculation
  - Define `VRAMBudgetThresholds` struct with default values (900/500/1200 MB)
  - _Requirements: 1.1, 1.2, 1.3, 2.1_

- [ ] 2. Implement VRAMMonitor singleton with ISlowTickable
  - [x] 2.1 Create VRAMMonitor class with singleton pattern and ISlowTickable
    - Implement singleton pattern (explicit `_instance` field, Awake null-check, OnDestroy cleanup)
    - Add `[DefaultExecutionOrder(-8000)]` and `[DisallowMultipleComponent]`
    - Implement ISlowTickable interface with GameTickManager registration/unregistration
    - Add cold allocations: `StringBuilder[1024]`, `List<ProfilerRecorder>[8]`
    - Cache `Shader.PropertyToID()` as `static readonly int` for MaterialPropertyBlock usage
    - _Requirements: 1.1, 1.2, 1.3, 13.1, 13.2, 13.3, 13.4, 13.5_

  - [ ]* 2.2 Write property test for VRAMMonitor threshold enforcement
    - **Property 1: VRAM Threshold Enforcement**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    - Test that VRAMMonitor correctly reports threshold violations for texture > 900 MB, RT > 500 MB, total > 1.2 GB
    - Test that warnings are logged with category breakdown when thresholds exceeded

  - [x] 2.3 Implement Unity Profiler API integration
    - Create `ProfilerRecorder` instances for texture memory and RenderTexture memory
    - Implement `MeasureVRAM()` method using `Profiler.GetTotalAllocatedMemoryLong()`
    - Implement `SlowTick()` with zero-GC measurement loop (no LINQ, no string concat)
    - Add throttled logging (once per 5s) with `StringBuilder` for zero-GC formatting
    - Handle Profiler API unavailable error (log warning, disable monitoring)
    - _Requirements: 1.5, 1.6, 13.6, 14.1_

  - [ ]* 2.4 Write unit tests for VRAMMonitor query API
    - **Property 2: VRAM Query API Consistency**
    - **Validates: Requirements 1.6**
    - Test that `GetVRAMBreakdown()` returns values matching internal state
    - Test that `IsTextureMemoryOverBudget`, `IsRenderTextureMemoryOverBudget`, `IsTotalVRAMOverBudget` properties work correctly

- [ ] 3. Implement RenderTextureLifecycleTracker singleton
  - [x] 3.1 Create RenderTextureLifecycleTracker class with singleton pattern
    - Implement singleton pattern with `[DefaultExecutionOrder(-7999)]`
    - Add cold allocations: `Dictionary<int, RenderTextureAllocationRecord>[256]`, `List<Record>[32]`, `StringBuilder[2048]`
    - Implement `RegisterAllocation()` with owner, resolution, format, timestamp, stack trace
    - Implement `RegisterDisposal()` with disposal timestamp
    - Handle null owner error (log error, skip registration)
    - Handle duplicate registration (log warning, update existing record)
    - _Requirements: 2.1, 2.4_

  - [ ]* 3.2 Write property test for RenderTexture registration completeness
    - **Property 3: RenderTexture Registration Completeness**
    - **Validates: Requirements 2.1**
    - Test that all required fields are registered (owner, width, height, format, timestamp, stack trace)

  - [x] 3.3 Implement leak detection with ISlowTickable
    - Register as ISlowTickable with GameTickManager
    - Implement `SlowTick()` to check for `owner == null && !IsDisposed && Time.time - AllocationTime > 10f`
    - Log error with owner name and allocation stack trace when leak detected
    - Implement `GetLeakedRenderTextures()` with pre-allocated List for zero-GC query
    - _Requirements: 2.2, 2.6_

  - [ ]* 3.4 Write property test for RenderTexture leak detection
    - **Property 4: RenderTexture Leak Detection**
    - **Validates: Requirements 2.2, 2.6**
    - Test that RTs not disposed within 10s of owner destruction are flagged as leaked
    - Test that leak errors include owner name and stack trace

  - [x] 3.5 Implement audit report generation
    - Implement `GenerateAuditReport()` with pre-allocated StringBuilder for zero-GC
    - Group RenderTextures by owner category (Visor, Camera, PostFX, UI)
    - Calculate total memory per category
    - Add `TrackedRenderTextureCount` and `TrackedRenderTextureMemoryBytes` properties
    - _Requirements: 2.3_

  - [ ]* 3.6 Write property test for audit report grouping
    - **Property 5: RenderTexture Audit Report Grouping**
    - **Validates: Requirements 2.3**
    - Test that audit report correctly groups RTs by owner category

  - [ ]* 3.7 Write property test for duplicate RenderTexture detection
    - **Property 6: Duplicate RenderTexture Detection**
    - **Validates: Requirements 2.4**
    - Test that duplicate allocations (same owner, resolution, format within 1 frame) are detected

- [ ] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Implement RenderTexturePool singleton with O(1) pooling
  - [x] 5.1 Create RenderTexturePool class with singleton pattern
    - Implement singleton pattern with `[DefaultExecutionOrder(-7998)]`
    - Add cold allocations: 4 `Dictionary<int, Queue<RenderTexture>>[16]` (one per format: R8, RG16, RGBA16, RGBA32)
    - Implement `CalculateRTHash()` function: `width ^ (height << 16) ^ ((int)format << 24)`
    - Add `_totalRentCalls` and `_totalReuseCount` counters for hit rate calculation
    - _Requirements: 5.1, 5.2, 5.3, 14.3_

  - [x] 5.2 Implement Rent and Return methods with zero-GC
    - Implement `Rent(width, height, format, owner)` with O(1) hash lookup
    - Return pooled RT if available (pool hit), otherwise allocate new RT (pool miss)
    - Register allocation with LifecycleTracker on pool miss
    - Implement `Return(rt)` with capacity check (max 16 per pool)
    - If pool full, immediately `rt.Release()` instead of storing
    - Handle null RT error in Return() (log warning, return early)
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 5.3 Write property test for RenderTexture pool state consistency
    - **Property 17: RenderTexture Pool State Consistency**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    - Test that Rent with matching RT in pool returns pooled RT
    - Test that Return with pool under capacity adds RT to pool
    - Test that Return with pool at capacity releases RT immediately
    - Test that pool never exceeds 16 RT per format

  - [x] 5.4 Implement pool hit rate tracking and scene cleanup
    - Implement `PoolHitRate` property: `_totalReuseCount / (float)_totalRentCalls`
    - Implement `TotalPooledCount` property summing all pools
    - Implement `ClearAllPools()` to release all pooled RTs
    - Subscribe to `SceneManager.sceneUnloaded` event in OnEnable
    - Call `ClearAllPools()` on scene unload
    - Unsubscribe in OnDisable
    - Log pool statistics every 60s in Development Build (throttled)
    - _Requirements: 5.5, 5.6_

  - [ ]* 5.5 Write property test for pool hit rate calculation
    - **Property 18: RenderTexture Pool Hit Rate Calculation**
    - **Validates: Requirements 5.6**
    - Test that hit rate is calculated correctly as reuse_count / total_rent_calls

  - [ ]* 5.6 Write integration test for scene unload pool cleanup
    - Test that all pooled RTs are released on SceneManager.sceneUnloaded event
    - Verify pool count is 0 after scene unload

- [ ] 6. Implement subsystem managers (Visor, Camera, PostFX, UI)
  - [x] 6.1 Create VisorRTManager singleton with ISlowTickable
    - Implement singleton pattern with `[DefaultExecutionOrder(-7997)]`
    - Add cold allocations: `StringBuilder[1024]`, `List<RenderTextureAllocationRecord>[32]`
    - Implement `SlowTick()` to query LifecycleTracker for Visor-owned RTs
    - Calculate total Visor RT memory consumption
    - Implement `IsOverBudget` property (> 64 MB)
    - Log warning with breakdown if over budget (throttled to once per 5s)
    - _Requirements: 6.1, 6.4, 6.5_

  - [x] 6.2 Create CameraRTManager singleton with ISlowTickable
    - Implement singleton pattern with `[DefaultExecutionOrder(-7996)]`
    - Query LifecycleTracker for Camera-owned RTs (main camera, overlay cameras, render cameras)
    - Calculate total Camera RT memory consumption
    - Implement `IsOverBudget` property (> 256 MB)
    - Log warning with breakdown if over budget
    - _Requirements: 7.1, 7.3, 7.4_

  - [x] 6.3 Create PostFXRTManager singleton with ISlowTickable
    - Implement singleton pattern with `[DefaultExecutionOrder(-7995)]`
    - Query LifecycleTracker for PostFX-owned RTs (URP Volume components, post-processing effects)
    - Calculate total PostFX RT memory consumption
    - Implement `IsOverBudget` property (> 128 MB)
    - Log warning with breakdown if over budget
    - _Requirements: 8.1, 8.3, 8.4_

  - [x] 6.4 Create UIRTManager singleton with ISlowTickable
    - Implement singleton pattern with `[DefaultExecutionOrder(-7994)]`
    - Query LifecycleTracker for UI-owned RTs (Canvas, RawImage, UIDocument)
    - Calculate total UI RT memory consumption
    - Implement `IsOverBudget` property (> 64 MB)
    - Log warning with breakdown if over budget
    - _Requirements: 9.1, 9.3, 9.4_

  - [ ]* 6.5 Write property test for subsystem ownership filtering
    - **Property 19: Subsystem RenderTexture Ownership Filtering**
    - **Validates: Requirements 6.1, 7.1, 8.1, 9.1**
    - Test that each subsystem manager correctly identifies RTs owned by components in its subsystem

  - [ ]* 6.6 Write property test for subsystem budget enforcement
    - **Property 21: Subsystem Budget Enforcement**
    - **Validates: Requirements 6.4, 7.3, 8.3, 9.3**
    - Test that subsystem managers correctly calculate total memory and report over-budget state

  - [ ]* 6.7 Write property test for subsystem budget violation logging
    - **Property 22: Subsystem Budget Violation Logging**
    - **Validates: Requirements 6.5, 7.4, 8.4, 9.4**
    - Test that warnings include breakdown by RT owner when over budget

- [ ] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement VRAMOptimizationBootstrap initialization
  - [x] 8.1 Create VRAMOptimizationBootstrap class with RuntimeInitializeOnLoadMethod
    - Add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`
    - Create GameObject "__VRAMOptimizationBootstrap" with DontDestroyOnLoad
    - Add components in dependency order: VRAMMonitor, LifecycleTracker, RTPool, VisorRTManager, CameraRTManager, PostFXRTManager, UIRTManager
    - Log "Bootstrap complete" in Development Build
    - _Requirements: 1.1, 2.1, 5.1, 6.1, 7.1, 8.1, 9.1_

  - [ ]* 8.2 Write integration test for bootstrap initialization
    - Test that all singletons are created in correct order
    - Test that DontDestroyOnLoad is applied
    - Test that all singletons are registered with GameTickManager

- [ ] 9. Integrate with VisorHUDController for RT disposal fix
  - [x] 9.1 Modify VisorHUDController to use RenderTexturePool
    - Replace `new RenderTexture()` with `RenderTexturePool.Instance.Rent()`
    - Call `RenderTextureLifecycleTracker.Instance.RegisterAllocation()` after Rent
    - Implement `ReleaseRT()` method calling `RegisterDisposal()` and `Return()`
    - Call `ReleaseRT()` in OnDisable and OnDestroy
    - Verify no RT retention after VisorHUDController is disabled
    - _Requirements: 6.2, 6.3, 6.6_

  - [ ]* 9.2 Write integration test for Visor RT disposal
    - **Property 20: Subsystem RenderTexture Disposal Verification**
    - **Validates: Requirements 6.3, 7.3, 8.3, 9.3**
    - Test that all Visor RTs are disposed within 1 frame of VisorHUDController disable/destroy

- [ ] 10. Integrate with RuntimePerformanceProfiler for VRAM reporting
  - [x] 10.1 Add VRAM monitoring to RuntimePerformanceProfiler.SlowTick()
    - Query `VRAMMonitor.Instance.GetVRAMBreakdown()` for texture, RT, total VRAM
    - Store values in `_debugLastTextureMB`, `_debugLastRenderTextureMB`, `_debugLastTotalVRAMMB` fields
    - Set `_debugLastVRAMWarning` if any threshold exceeded
    - Display VRAM stats in existing debug UI
    - _Requirements: 1.6, 11.1_

  - [ ]* 10.2 Write integration test for Profiler integration
    - Test that RuntimePerformanceProfiler displays VRAM stats correctly
    - Test that warnings are shown when thresholds exceeded

- [ ] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 12. Implement RenderTextureFormatOptimizer Editor tool
  - [x] 12.1 Create RenderTextureFormatOptimizer static class (Editor-only)
    - Add `#if UNITY_EDITOR` guard
    - Implement `AnalyzeFormats()` querying LifecycleTracker for all tracked RTs
    - Implement format recommendation heuristics: RGBA32 → RGBA16 (no HDR), RGBA16 → RG16 (RG-only), RG16 → R8 (R-only)
    - Implement `CalculateMemorySavings()`: `width × height × (old_bpp - new_bpp) / 8`
    - Return `List<FormatOptimizationRecommendation>` with owner, current format, recommended format, savings
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 12.2 Write property test for format optimization recommendation
    - **Property 7: Format Optimization Recommendation**
    - **Validates: Requirements 3.1, 3.2**
    - Test that FormatOptimizer recommends minimal viable format for various RT usage patterns

  - [ ]* 12.3 Write property test for format optimization memory calculation
    - **Property 8: Format Optimization Memory Calculation**
    - **Validates: Requirements 3.3**
    - Test that memory savings are calculated correctly for format changes

  - [ ] 12.4 Implement format change validation
    - Implement `ValidateFormatChange()` rendering test frame at old and new formats
    - Use `Texture2D.ReadPixels()` to capture pixel data
    - Compare byte-by-byte for bit-identical validation
    - Return true if bit-identical, false otherwise
    - _Requirements: 3.5_

  - [ ]* 12.5 Write property test for format change bit-identical validation
    - **Property 10: Format Change Bit-Identical Validation**
    - **Validates: Requirements 3.5**
    - Test that lossless format changes (RGBA32 → R8 for R-only) produce bit-identical output

  - [ ] 12.6 Implement VRAM delta measurement for format optimization
    - Capture BEFORE VRAM via `Profiler.GetTotalAllocatedMemoryLong()`
    - Apply format change
    - Capture AFTER VRAM
    - Calculate delta and verify it matches calculated savings
    - Generate report with BEFORE, AFTER, DELTA, PERCENT_CHANGE
    - _Requirements: 3.6_

  - [ ]* 12.7 Write property test for format optimization VRAM delta
    - **Property 11: Format Optimization VRAM Delta Measurement**
    - **Validates: Requirements 3.6**
    - Test that measured VRAM delta matches calculated memory savings

  - [ ]* 12.8 Write property test for format optimization report completeness
    - **Property 9: Format Optimization Report Completeness**
    - **Validates: Requirements 3.4**
    - Test that report includes all RTs with suboptimal formats

- [ ] 13. Implement RenderTextureResolutionAnalyzer Editor tool
  - [x] 13.1 Create RenderTextureResolutionAnalyzer static class (Editor-only)
    - Add `#if UNITY_EDITOR` guard
    - Implement `AnalyzeResolutions()` querying LifecycleTracker for all tracked RTs
    - Implement `MeasureVisualDifference()` rendering at native and downscaled resolutions
    - Calculate RMSE: `sqrt(sum((pixel_native - pixel_scaled)^2) / pixel_count) × 100%`
    - Test scales: 1.0 (baseline), 0.75, 0.5, 0.25
    - _Requirements: 4.1, 4.2_

  - [ ]* 13.2 Write property test for resolution RMSE calculation
    - **Property 12: Resolution RMSE Calculation**
    - **Validates: Requirements 4.1**
    - Test that RMSE is calculated correctly for native vs downscaled resolutions

  - [x] 13.3 Implement resolution recommendation logic
    - Recommend smallest scale where RMSE < 2%
    - Prioritize off-screen, blurred, or distant RTs higher in recommendation list
    - Calculate memory savings: `width × height × bpp / 8 × (1 - scale²)`
    - Return `List<ResolutionOptimizationRecommendation>` with owner, current resolution, recommended resolution, RMSE, savings
    - _Requirements: 4.2, 4.3, 4.4, 4.5_

  - [ ]* 13.4 Write property test for resolution optimization recommendation
    - **Property 13: Resolution Optimization Recommendation**
    - **Validates: Requirements 4.2**
    - Test that smallest scale with RMSE < 2% is recommended

  - [ ]* 13.5 Write property test for resolution optimization prioritization
    - **Property 14: Resolution Optimization Prioritization**
    - **Validates: Requirements 4.3**
    - Test that off-screen/blurred/distant RTs are prioritized higher

  - [ ]* 13.6 Write property test for resolution optimization memory calculation
    - **Property 15: Resolution Optimization Memory Calculation**
    - **Validates: Requirements 4.4**
    - Test that memory savings are calculated correctly for resolution changes

  - [ ]* 13.7 Write property test for resolution optimization report completeness
    - **Property 16: Resolution Optimization Report Completeness**
    - **Validates: Requirements 4.5**
    - Test that report includes all RTs with oversized resolutions

  - [ ] 13.8 Implement screenshot capture for visual regression testing
    - Implement `CaptureScreenshot()` rendering RT to Texture2D
    - Export as PNG at 1920×1080 resolution
    - Capture BEFORE and AFTER screenshots for comparison
    - Store in `Assets/_Project/Optimization/Screenshots/` directory
    - _Requirements: 4.6_

  - [ ]* 13.9 Write integration test for screenshot capture
    - Test that screenshots are captured correctly at 1920×1080
    - Test that BEFORE and AFTER screenshots are saved to correct paths

- [ ] 14. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 15. Implement RenderTextureLifecycleWindow Editor UI
  - [x] 15.1 Create RenderTextureLifecycleWindow EditorWindow class
    - Add `#if UNITY_EDITOR` guard
    - Add `[MenuItem("Hecton8/Optimization/RenderTexture Lifecycle Viewer")]`
    - Implement `OnGUI()` displaying tracked RT count, total memory, allocations by owner
    - Query `RenderTextureLifecycleTracker.Instance.GenerateAuditReport()` for display
    - Add "Refresh" button to manually update display
    - Auto-refresh in Play Mode via `Update()` calling `Repaint()`
    - _Requirements: 2.5_

  - [ ]* 15.2 Write integration test for Editor window
    - Test that Editor window displays correct RT count and memory
    - Test that audit report is displayed correctly grouped by owner

- [ ] 16. Create Editor menu items for optimization tools
  - [x] 16.1 Add menu items for FormatOptimizer and ResolutionAnalyzer
    - Add `[MenuItem("Hecton8/Optimization/Analyze RT Formats")]` calling `FormatOptimizer.AnalyzeFormats()`
    - Add `[MenuItem("Hecton8/Optimization/Analyze RT Resolutions")]` calling `ResolutionAnalyzer.AnalyzeResolutions()`
    - Display results in EditorWindow with recommendations and estimated savings
    - Add "Apply Optimization" button to apply recommended changes
    - _Requirements: 3.1, 4.1_

  - [ ]* 16.2 Write integration test for Editor menu items
    - Test that menu items invoke correct analysis methods
    - Test that results are displayed correctly in EditorWindow

- [ ] 17. Final integration and wiring
  - [x] 17.1 Wire all components together and verify initialization order
    - Verify bootstrap creates all singletons in correct order
    - Verify all singletons register with GameTickManager
    - Verify all event subscriptions (SceneManager.sceneUnloaded)
    - Verify VisorHUDController uses RenderTexturePool
    - Verify RuntimePerformanceProfiler displays VRAM stats
    - _Requirements: 1.1, 2.1, 5.1, 6.1, 7.1, 8.1, 9.1_

  - [ ]* 17.2 Write integration test for full system wiring
    - Test that all singletons are initialized correctly
    - Test that VRAM monitoring works end-to-end
    - Test that lifecycle tracking works end-to-end
    - Test that pooling works end-to-end
    - Test that subsystem managers work end-to-end

- [ ] 18. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- Integration tests validate Unity API integration and system wiring
- All code must follow AGENTS.md constraints: Zero GC in hot paths, ISlowTickable pattern, singleton pattern, MaterialPropertyBlock, scene cleanup
- Target hardware: NVIDIA MX350 2GB VRAM, 12GB RAM, i5-1135G7
- Performance target: 60 FPS (frame time ≤ 16.67 ms)
- VRAM budget: Texture < 900 MB, RenderTexture < 500 MB, Total < 1.2 GB
- Subsystem budgets: Visor 64 MB, Camera 256 MB, PostFX 128 MB, UI 64 MB
