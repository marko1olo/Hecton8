# Requirements Document

## Introduction

The VRAM/RenderTexture Optimization System addresses critical memory overruns on the target hardware (NVIDIA MX350 2GB VRAM). Current measurements show ~966 MB texture memory and ~531 MB RenderTexture memory, totaling ~1.5 GB—exceeding both RED thresholds (Texture > 900 MB, RenderTexture > 500 MB). This system will audit, optimize, and enforce VRAM budgets to achieve MASTER GRADE status while maintaining 60 FPS performance and zero visual regression.

## Glossary

- **VRAM_Monitor**: Runtime system tracking texture and RenderTexture memory consumption
- **RT_Pool**: RenderTexture pooling system for reuse across frames
- **RT_Owner**: Component or system responsible for a RenderTexture's lifecycle
- **Format_Optimizer**: System analyzing and recommending optimal RenderTexture formats
- **Resolution_Analyzer**: System determining minimum viable RenderTexture resolutions
- **Lifecycle_Tracker**: System tracking RenderTexture allocation, usage, and disposal
- **Visor_RT_Manager**: Subsystem managing VisorHUDController RenderTexture allocations
- **Camera_RT_Manager**: Subsystem managing camera stack RenderTexture allocations
- **PostFX_RT_Manager**: Subsystem managing post-processing RenderTexture allocations
- **UI_RT_Manager**: Subsystem managing UI RenderTexture allocations
- **Profiler_Integration**: Unity Profiler measurement and reporting integration
- **Target_Hardware**: NVIDIA MX350 2GB VRAM, 12GB RAM, i5-1135G7

## Requirements

### Requirement 1: VRAM Budget Enforcement

**User Story:** As a technical director, I want strict VRAM budget enforcement, so that the game runs reliably on Target_Hardware without memory-related crashes or throttling.

#### Acceptance Criteria

1. THE VRAM_Monitor SHALL track total texture memory consumption and report when exceeding 900 MB
2. THE VRAM_Monitor SHALL track total RenderTexture memory consumption and report when exceeding 500 MB
3. THE VRAM_Monitor SHALL track combined VRAM consumption and report when exceeding 1.2 GB
4. WHEN VRAM consumption exceeds any threshold, THE VRAM_Monitor SHALL log a warning with breakdown by category (textures, RenderTextures, meshes, shaders)
5. THE VRAM_Monitor SHALL measure memory consumption via Unity Profiler API (Profiler.GetTotalAllocatedMemoryLong, Profiler.GetTotalReservedMemoryLong)
6. THE VRAM_Monitor SHALL provide a runtime query API returning current texture memory, RenderTexture memory, and total VRAM usage

### Requirement 2: RenderTexture Ownership Audit

**User Story:** As a technical director, I want complete visibility into RenderTexture ownership, so that I can identify leaks, redundant allocations, and optimization opportunities.

#### Acceptance Criteria

1. THE Lifecycle_Tracker SHALL register every RenderTexture allocation with owner component, resolution, format, and timestamp
2. THE Lifecycle_Tracker SHALL track RenderTexture disposal and flag any RT not disposed within 10 seconds of owner destruction
3. THE Lifecycle_Tracker SHALL generate an audit report listing all active RenderTextures grouped by RT_Owner (Visor, Camera, PostFX, UI)
4. THE Lifecycle_Tracker SHALL detect duplicate RenderTexture allocations (same owner, same resolution, same format within 1 frame)
5. THE Lifecycle_Tracker SHALL provide an Editor window displaying real-time RenderTexture allocations with owner hierarchy
6. WHEN a RenderTexture leaks (not disposed after owner destruction), THE Lifecycle_Tracker SHALL log an error with owner name and allocation stack trace

### Requirement 3: RenderTexture Format Optimization

**User Story:** As a graphics programmer, I want optimal RenderTexture formats, so that VRAM usage is minimized without sacrificing visual quality.

#### Acceptance Criteria

1. THE Format_Optimizer SHALL analyze each RenderTexture usage and recommend the minimal viable format (R8, RG16, RGBA16, RGBA32)
2. WHEN a RenderTexture uses RGBA32 but only reads R or RG channels, THE Format_Optimizer SHALL recommend R8 or RG16 format
3. THE Format_Optimizer SHALL calculate memory savings for each format recommendation (bytes saved = width × height × (old_bpp - new_bpp) / 8)
4. THE Format_Optimizer SHALL generate a report listing all RenderTextures with suboptimal formats and estimated savings
5. THE Format_Optimizer SHALL validate format changes produce bit-identical output for R8/RG16 cases (no precision loss)
6. WHEN format optimization is applied, THE Format_Optimizer SHALL measure BEFORE and AFTER VRAM consumption via Unity Profiler

### Requirement 4: RenderTexture Resolution Optimization

**User Story:** As a graphics programmer, I want minimal viable RenderTexture resolutions, so that VRAM usage is reduced while maintaining visual fidelity.

#### Acceptance Criteria

1. THE Resolution_Analyzer SHALL measure visual difference between RenderTexture at native resolution and downscaled resolutions (0.75×, 0.5×, 0.25×)
2. THE Resolution_Analyzer SHALL recommend the smallest resolution where visual difference is below 2% RMSE (root mean square error)
3. THE Resolution_Analyzer SHALL prioritize resolution reduction for off-screen, blurred, or distant RenderTextures
4. THE Resolution_Analyzer SHALL calculate memory savings for each resolution recommendation (bytes saved = width × height × bpp / 8 × (1 - scale²))
5. THE Resolution_Analyzer SHALL generate a report listing all RenderTextures with oversized resolutions and estimated savings
6. WHEN resolution optimization is applied, THE Resolution_Analyzer SHALL capture BEFORE and AFTER screenshots for visual regression testing

### Requirement 5: RenderTexture Pooling System

**User Story:** As a graphics programmer, I want RenderTexture pooling, so that temporary RenderTextures are reused instead of allocated and destroyed every frame.

#### Acceptance Criteria

1. THE RT_Pool SHALL provide Rent(width, height, format) and Return(rt) methods for temporary RenderTexture acquisition
2. THE RT_Pool SHALL maintain separate pools per format (R8, RG16, RGBA16, RGBA32) with maximum 16 RenderTextures per pool
3. WHEN Rent() is called, THE RT_Pool SHALL return an existing RenderTexture matching resolution and format if available, otherwise allocate a new one
4. WHEN Return() is called, THE RT_Pool SHALL add the RenderTexture to the pool if under capacity, otherwise Release() it immediately
5. THE RT_Pool SHALL clear all pooled RenderTextures on scene unload via SceneManager.sceneUnloaded event
6. THE RT_Pool SHALL track pool hit rate (reuse count / total Rent calls) and log statistics every 60 seconds in Development Build

### Requirement 6: Visor RenderTexture Management

**User Story:** As a gameplay programmer, I want optimized Visor RenderTextures, so that HUD rendering does not exceed VRAM budget.

#### Acceptance Criteria

1. THE Visor_RT_Manager SHALL audit all RenderTextures owned by VisorHUDController and related components
2. THE Visor_RT_Manager SHALL ensure VisorHUDController disposes RenderTextures in OnDisable and OnDestroy
3. THE Visor_RT_Manager SHALL verify no RenderTexture retention after VisorHUDController is disabled (per MASTER_RELEASE_WORK_PLAN fix)
4. THE Visor_RT_Manager SHALL measure Visor RenderTexture memory consumption and ensure it stays below 64 MB
5. WHEN Visor RenderTexture memory exceeds 64 MB, THE Visor_RT_Manager SHALL log a warning with breakdown by RT owner
6. THE Visor_RT_Manager SHALL apply format and resolution optimizations to Visor RenderTextures without visual regression

### Requirement 7: Camera Stack RenderTexture Management

**User Story:** As a graphics programmer, I want optimized camera stack RenderTextures, so that multi-camera rendering does not exceed VRAM budget.

#### Acceptance Criteria

1. THE Camera_RT_Manager SHALL audit all RenderTextures owned by camera components (main camera, overlay cameras, render cameras)
2. THE Camera_RT_Manager SHALL identify redundant camera RenderTextures (multiple cameras rendering to separate RTs when one shared RT suffices)
3. THE Camera_RT_Manager SHALL measure camera RenderTexture memory consumption and ensure it stays below 256 MB
4. WHEN camera RenderTexture memory exceeds 256 MB, THE Camera_RT_Manager SHALL log a warning with breakdown by camera
5. THE Camera_RT_Manager SHALL apply resolution scaling to off-screen or low-priority camera RenderTextures
6. THE Camera_RT_Manager SHALL ensure all camera RenderTextures use RT_Pool for temporary allocations

### Requirement 8: Post-Processing RenderTexture Management

**User Story:** As a graphics programmer, I want optimized post-processing RenderTextures, so that post-FX do not exceed VRAM budget.

#### Acceptance Criteria

1. THE PostFX_RT_Manager SHALL audit all RenderTextures owned by URP Volume components and post-processing effects
2. THE PostFX_RT_Manager SHALL identify redundant post-processing RenderTextures (multiple effects allocating separate RTs when one shared RT suffices)
3. THE PostFX_RT_Manager SHALL measure post-processing RenderTexture memory consumption and ensure it stays below 128 MB
4. WHEN post-processing RenderTexture memory exceeds 128 MB, THE PostFX_RT_Manager SHALL log a warning with breakdown by effect
5. THE PostFX_RT_Manager SHALL apply format optimization to post-processing RenderTextures (R8 for masks, RG16 for flow maps)
6. THE PostFX_RT_Manager SHALL ensure all post-processing RenderTextures use RT_Pool for temporary allocations

### Requirement 9: UI RenderTexture Management

**User Story:** As a UI programmer, I want optimized UI RenderTextures, so that UI rendering does not exceed VRAM budget.

#### Acceptance Criteria

1. THE UI_RT_Manager SHALL audit all RenderTextures owned by UI components (Canvas, RawImage, UIDocument)
2. THE UI_RT_Manager SHALL identify redundant UI RenderTextures (multiple UI elements rendering to separate RTs when one shared RT suffices)
3. THE UI_RT_Manager SHALL measure UI RenderTexture memory consumption and ensure it stays below 64 MB
4. WHEN UI RenderTexture memory exceeds 64 MB, THE UI_RT_Manager SHALL log a warning with breakdown by UI element
5. THE UI_RT_Manager SHALL apply resolution scaling to off-screen or low-priority UI RenderTextures
6. THE UI_RT_Manager SHALL ensure all UI RenderTextures use RT_Pool for temporary allocations

### Requirement 10: Reflection Probe and Shadow Map Optimization

**User Story:** As a graphics programmer, I want optimized reflection probes and shadow maps, so that baked lighting does not exceed VRAM budget.

#### Acceptance Criteria

1. THE VRAM_Monitor SHALL measure reflection probe memory consumption and ensure it stays below 32 MB
2. THE VRAM_Monitor SHALL measure shadow map memory consumption and ensure it stays below 64 MB
3. WHEN reflection probe memory exceeds 32 MB, THE VRAM_Monitor SHALL log a warning with breakdown by probe
4. WHEN shadow map memory exceeds 64 MB, THE VRAM_Monitor SHALL log a warning with breakdown by light
5. THE Format_Optimizer SHALL recommend optimal reflection probe resolution (128, 256, 512) based on probe importance
6. THE Format_Optimizer SHALL recommend optimal shadow map resolution (512, 1024, 2048) based on light importance

### Requirement 11: Unity Profiler Integration

**User Story:** As a technical director, I want automated Profiler measurements, so that VRAM optimization is verified with objective data.

#### Acceptance Criteria

1. THE Profiler_Integration SHALL capture BEFORE measurements (texture memory, RenderTexture memory, total VRAM) before optimization
2. THE Profiler_Integration SHALL capture AFTER measurements (texture memory, RenderTexture memory, total VRAM) after optimization
3. THE Profiler_Integration SHALL calculate delta (AFTER - BEFORE) for each memory category
4. THE Profiler_Integration SHALL generate a report with BEFORE, AFTER, DELTA, and PERCENT_CHANGE for each category
5. THE Profiler_Integration SHALL verify zero visual regression by capturing screenshots BEFORE and AFTER optimization
6. THE Profiler_Integration SHALL measure frame time (CPU and GPU) BEFORE and AFTER optimization to detect performance regression

### Requirement 12: Zero Visual Regression Verification

**User Story:** As a technical director, I want zero visual regression, so that VRAM optimization does not degrade visual quality.

#### Acceptance Criteria

1. THE Profiler_Integration SHALL capture reference screenshots at 1920×1080 resolution before optimization
2. THE Profiler_Integration SHALL capture comparison screenshots at 1920×1080 resolution after optimization
3. THE Profiler_Integration SHALL calculate RMSE (root mean square error) between reference and comparison screenshots
4. WHEN RMSE exceeds 2%, THE Profiler_Integration SHALL flag visual regression and revert optimization
5. THE Profiler_Integration SHALL generate a visual diff image highlighting pixel differences
6. THE Profiler_Integration SHALL verify zero regression on Target_Hardware (MX350 equivalent) via build test

### Requirement 13: Zero GC Allocation in Hot Paths

**User Story:** As a performance engineer, I want zero GC allocation in VRAM monitoring, so that runtime tracking does not degrade performance.

#### Acceptance Criteria

1. THE VRAM_Monitor SHALL allocate all buffers and collections in Awake (COLD ALLOC)
2. THE VRAM_Monitor SHALL use pre-allocated List<T> and Dictionary<K,V> for tracking RenderTextures
3. THE VRAM_Monitor SHALL avoid LINQ, string concatenation, and boxing in ITickable.Tick()
4. THE VRAM_Monitor SHALL cache Shader.PropertyToID as static readonly int for MaterialPropertyBlock usage
5. THE VRAM_Monitor SHALL use StringBuilder for log formatting (allocated once in Awake)
6. THE VRAM_Monitor SHALL measure GC allocation via Unity Profiler and ensure 0 B/frame in hot paths

### Requirement 14: 60 FPS Performance Target

**User Story:** As a technical director, I want 60 FPS performance, so that VRAM optimization does not degrade frame rate on Target_Hardware.

#### Acceptance Criteria

1. THE VRAM_Monitor SHALL execute in ISlowTickable (~0.5s interval) to avoid per-frame overhead
2. THE Lifecycle_Tracker SHALL batch RenderTexture registration and disposal to avoid per-frame overhead
3. THE RT_Pool SHALL use O(1) lookup via Dictionary<int, Queue<RenderTexture>> keyed by hash(width, height, format)
4. THE Profiler_Integration SHALL measure frame time BEFORE and AFTER optimization and ensure no regression (≤ 16.67 ms)
5. WHEN frame time exceeds 16.67 ms after optimization, THE Profiler_Integration SHALL flag performance regression and revert optimization
6. THE VRAM_Monitor SHALL verify 60 FPS on Target_Hardware (MX350 equivalent) via build test

### Requirement 15: Build Verification on Target Hardware

**User Story:** As a technical director, I want build verification on Target_Hardware, so that VRAM optimization is proven in production builds.

#### Acceptance Criteria

1. THE Profiler_Integration SHALL generate a build test report with VRAM measurements on Target_Hardware
2. THE Profiler_Integration SHALL verify texture memory < 900 MB on Target_Hardware
3. THE Profiler_Integration SHALL verify RenderTexture memory < 500 MB on Target_Hardware
4. THE Profiler_Integration SHALL verify total VRAM < 1.2 GB on Target_Hardware
5. THE Profiler_Integration SHALL verify 60 FPS (frame time ≤ 16.67 ms) on Target_Hardware
6. WHEN any verification fails, THE Profiler_Integration SHALL log detailed failure report with memory breakdown and frame time analysis
