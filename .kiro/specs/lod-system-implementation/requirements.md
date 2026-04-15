# LOD System Implementation — Requirements Document

## Introduction

HECTON-8 currently renders all meshes at full detail regardless of distance, causing performance issues on target hardware (NVIDIA MX350 2GB VRAM, 12GB RAM, i5-1135G7). This document defines requirements for an automatic LOD (Level of Detail) management system that maintains 60 FPS @ 1080p by reducing geometric complexity for distant objects through LOD groups, impostor rendering, culling systems, and dynamic resolution scaling.

## Glossary

- **LOD_System**: The runtime manager responsible for LOD group registration, distance calculations, and transition orchestration
- **LOD_Group**: Unity component defining multiple mesh detail levels (LOD0/LOD1/LOD2) with distance-based switching thresholds
- **Impostor_System**: Billboard-based rendering system for very distant objects using baked texture atlases
- **Culling_Manager**: System managing frustum culling, occlusion culling, and distance-based object deactivation
- **Dynamic_Resolution_Scaler**: Runtime system adjusting render resolution to maintain target frame rate
- **LOD_Bias**: Global multiplier affecting LOD transition distances (quality preset control)
- **Crossfade_Transition**: Dithered alpha blending between LOD levels to eliminate visual popping
- **Hero_Asset**: High-importance visual asset requiring 3 LOD levels (LOD0+LOD1+LOD2+Cull)
- **Prop_Asset**: Standard world object requiring 2 LOD levels minimum (LOD0+LOD1+Cull)
- **Cull_Distance**: Distance threshold beyond which objects are completely deactivated
- **SetPass_Count**: Number of render state changes per frame (target ≤ 600)
- **Batch_Count**: Number of draw calls per frame (target ≤ 1800)

## Requirements

### Requirement 1: LOD Group Configuration System

**User Story:** As a technical artist, I want to configure LOD groups for all complex meshes, so that distant objects render with appropriate geometric detail.

#### Acceptance Criteria

1. THE LOD_System SHALL register all LOD_Group components during scene initialization
2. WHEN a mesh is larger than 0.5 meters, THE LOD_System SHALL enforce minimum LOD0+LOD1+Cull configuration
3. WHEN a mesh is classified as Hero_Asset, THE LOD_System SHALL enforce LOD0+LOD1+LOD2+Cull configuration
4. THE LOD_System SHALL validate that LOD1 polygon count is at most 50% of LOD0 polygon count
5. THE LOD_System SHALL validate that LOD2 polygon count is at most 25% of LOD0 polygon count
6. WHEN LOD validation fails, THE LOD_System SHALL log a warning with asset name and violation details
7. THE LOD_System SHALL expose LOD_Group registration count via public property

---

### Requirement 2: Distance-Based LOD Switching

**User Story:** As a player, I want distant objects to automatically reduce detail, so that the game maintains smooth performance.

#### Acceptance Criteria

1. THE LOD_System SHALL calculate camera-to-object distance for all registered LOD_Group instances per frame
2. WHEN camera distance exceeds LOD transition threshold, THE LOD_System SHALL switch to the next LOD level
3. THE LOD_System SHALL apply LOD_Bias multiplier to all transition distances
4. WHEN LOD_Bias is greater than 1.0 without quality preset justification, THE LOD_System SHALL log a warning
5. THE LOD_System SHALL use squared distance calculations to avoid square root operations
6. THE LOD_System SHALL batch distance calculations using Unity Jobs System
7. THE LOD_System SHALL complete distance calculation jobs within 2 milliseconds per frame

---

### Requirement 3: LOD Transition Rendering

**User Story:** As a player, I want smooth transitions between LOD levels, so that I do not see visual popping artifacts.

#### Acceptance Criteria

1. WHEN objects are within 50 meters of camera, THE LOD_System SHALL use Crossfade_Transition mode
2. WHEN objects are beyond 50 meters of camera, THE LOD_System SHALL use discrete LOD switching
3. THE LOD_System SHALL configure crossfade duration between 0.5 and 1.0 seconds
4. THE LOD_System SHALL use dithered alpha blending for crossfade rendering
5. WHEN crossfade is active, THE LOD_System SHALL render both source and target LOD levels simultaneously
6. THE LOD_System SHALL disable crossfade for objects with transparent materials
7. THE LOD_System SHALL expose crossfade distance threshold as configurable parameter

---

### Requirement 4: Distance-Based Culling

**User Story:** As a developer, I want objects beyond visibility range to be culled, so that rendering performance is optimized.

#### Acceptance Criteria

1. THE Culling_Manager SHALL set Cull_Distance for objects smaller than 1 meter to 30 meters
2. THE Culling_Manager SHALL set Cull_Distance for medium-sized objects to 80 meters
3. THE Culling_Manager SHALL set Cull_Distance for large objects to 200 meters
4. WHEN object distance exceeds Cull_Distance, THE Culling_Manager SHALL deactivate the GameObject
5. WHEN object distance returns below Cull_Distance threshold, THE Culling_Manager SHALL reactivate the GameObject
6. THE Culling_Manager SHALL use hysteresis (10% threshold difference) to prevent activation thrashing
7. THE Culling_Manager SHALL process culling checks via ISlowTickable (approximately 0.5 second intervals)

---

### Requirement 5: Frustum Culling Integration

**User Story:** As a developer, I want objects outside camera view to be culled, so that GPU resources are not wasted on invisible geometry.

#### Acceptance Criteria

1. THE Culling_Manager SHALL verify Unity frustum culling is enabled for all renderers
2. THE Culling_Manager SHALL calculate camera frustum planes once per frame
3. WHEN renderer bounds are outside frustum planes, THE Culling_Manager SHALL mark renderer as culled
4. THE Culling_Manager SHALL use Burst-compiled jobs for frustum plane calculations
5. THE Culling_Manager SHALL expose frustum-culled object count via public property
6. THE Culling_Manager SHALL integrate with Unity's built-in culling system without duplication
7. THE Culling_Manager SHALL process frustum checks within 1 millisecond per frame

---

### Requirement 6: Occlusion Culling System

**User Story:** As a developer, I want objects hidden behind other geometry to be culled, so that overdraw is minimized.

#### Acceptance Criteria

1. THE Culling_Manager SHALL verify occlusion culling data exists for current scene
2. WHEN occlusion culling data is missing, THE Culling_Manager SHALL log a warning once per scene load
3. THE Culling_Manager SHALL mark objects larger than 1 cubic meter as Occludee Static
4. THE Culling_Manager SHALL mark objects larger than 2 cubic meters as Occluder Static
5. THE Culling_Manager SHALL integrate with Unity's baked occlusion culling system
6. THE Culling_Manager SHALL expose occlusion-culled object count via public property
7. THE Culling_Manager SHALL validate occlusion culling settings during scene initialization

---

### Requirement 7: Impostor System for Distant Objects

**User Story:** As a developer, I want very distant objects rendered as impostors, so that geometric complexity is minimized at extreme distances.

#### Acceptance Criteria

1. THE Impostor_System SHALL generate billboard textures for objects beyond 150 meters
2. THE Impostor_System SHALL use Amplify Impostors plugin for impostor generation
3. WHEN object distance exceeds impostor threshold, THE Impostor_System SHALL replace mesh renderer with billboard renderer
4. THE Impostor_System SHALL bake impostor textures at 512x512 resolution
5. THE Impostor_System SHALL use octahedral mapping for impostor texture atlases
6. THE Impostor_System SHALL cache impostor textures in Addressables asset bundles
7. THE Impostor_System SHALL transition from LOD2 to impostor using crossfade blending

---

### Requirement 8: Dynamic Resolution Scaling

**User Story:** As a player, I want the game to maintain 60 FPS by adjusting resolution, so that gameplay remains smooth during demanding scenes.

#### Acceptance Criteria

1. THE Dynamic_Resolution_Scaler SHALL monitor frame time every frame
2. WHEN frame time exceeds 16.67 milliseconds for 3 consecutive frames, THE Dynamic_Resolution_Scaler SHALL reduce render scale by 5%
3. WHEN frame time is below 15 milliseconds for 30 consecutive frames, THE Dynamic_Resolution_Scaler SHALL increase render scale by 2%
4. THE Dynamic_Resolution_Scaler SHALL clamp render scale between 0.5 and 1.0
5. THE Dynamic_Resolution_Scaler SHALL apply render scale changes over 1 second duration
6. THE Dynamic_Resolution_Scaler SHALL expose current render scale via public property
7. WHERE quality preset is Low, THE Dynamic_Resolution_Scaler SHALL set minimum render scale to 0.7

---

### Requirement 9: LOD Bias Quality Presets

**User Story:** As a player, I want to select graphics quality presets, so that I can balance visual quality with performance.

#### Acceptance Criteria

1. THE LOD_System SHALL provide three quality presets: Low, Medium, High
2. WHEN quality preset is Low, THE LOD_System SHALL set LOD_Bias to 1.5
3. WHEN quality preset is Medium, THE LOD_System SHALL set LOD_Bias to 1.0
4. WHEN quality preset is High, THE LOD_System SHALL set LOD_Bias to 0.7
5. THE LOD_System SHALL apply LOD_Bias changes immediately without scene reload
6. THE LOD_System SHALL persist quality preset selection via SaveManager
7. THE LOD_System SHALL expose current quality preset via public property

---

### Requirement 10: Layer-Based Cull Distance Configuration

**User Story:** As a developer, I want different object layers to have different cull distances, so that rendering is optimized per object category.

#### Acceptance Criteria

1. THE Culling_Manager SHALL configure layer-specific cull distances via Camera.layerCullDistances
2. THE Culling_Manager SHALL set debris layer cull distance to 40 meters
3. THE Culling_Manager SHALL set particles layer cull distance to 40 meters
4. THE Culling_Manager SHALL set props layer cull distance to 100 meters
5. THE Culling_Manager SHALL set flora layer cull distance to 100 meters
6. THE Culling_Manager SHALL set large geometry layer cull distance to camera far clip plane
7. THE Culling_Manager SHALL apply layer cull distances during scene initialization

---

### Requirement 11: Performance Monitoring and Validation

**User Story:** As a developer, I want to monitor LOD system performance, so that I can verify 60 FPS target is maintained.

#### Acceptance Criteria

1. THE LOD_System SHALL track SetPass_Count per frame
2. WHEN SetPass_Count exceeds 600, THE LOD_System SHALL log a performance warning
3. THE LOD_System SHALL track Batch_Count per frame
4. WHEN Batch_Count exceeds 1800, THE LOD_System SHALL log a performance warning
5. THE LOD_System SHALL expose LOD system CPU time via public property
6. THE LOD_System SHALL target LOD processing time below 2 milliseconds per frame
7. THE LOD_System SHALL allocate zero bytes per frame during LOD updates

---

### Requirement 12: Zero-Allocation LOD Management

**User Story:** As a developer, I want LOD system to produce zero garbage collection, so that frame time remains stable.

#### Acceptance Criteria

1. THE LOD_System SHALL pre-allocate all collections during initialization
2. THE LOD_System SHALL use NativeArray for distance calculation job data
3. THE LOD_System SHALL cache all LOD_Group references during registration
4. THE LOD_System SHALL use object pooling for impostor billboard instances
5. THE LOD_System SHALL avoid LINQ operations in all hot paths
6. THE LOD_System SHALL use for loops instead of foreach on collections
7. THE LOD_System SHALL produce zero bytes of garbage collection per frame during gameplay

---

### Requirement 13: LOD System Integration with GameTickManager

**User Story:** As a developer, I want LOD system to integrate with existing tick architecture, so that it follows project conventions.

#### Acceptance Criteria

1. THE LOD_System SHALL implement ITickable interface
2. THE LOD_System SHALL register with GameTickManager during OnEnable
3. THE LOD_System SHALL unregister from GameTickManager during OnDisable
4. THE LOD_System SHALL use dt parameter from Tick method instead of Time.deltaTime
5. THE LOD_System SHALL implement singleton pattern via LOD_System.Instance
6. THE LOD_System SHALL use DefaultExecutionOrder less than -100
7. THE LOD_System SHALL null-check GameTickManager.Instance before registration

---

### Requirement 14: LOD System Save/Load Integration

**User Story:** As a player, I want LOD quality settings to persist across sessions, so that I do not need to reconfigure preferences.

#### Acceptance Criteria

1. THE LOD_System SHALL implement ISaveable interface
2. THE LOD_System SHALL save LOD_Bias value to save file
3. THE LOD_System SHALL save quality preset selection to save file
4. THE LOD_System SHALL save dynamic resolution enabled state to save file
5. THE LOD_System SHALL restore LOD settings during LoadFromSaveData
6. THE LOD_System SHALL use LoadPriority value of 5 (Core system)
7. THE LOD_System SHALL validate loaded values and use defaults if invalid

---

### Requirement 15: Editor Validation and Debugging Tools

**User Story:** As a technical artist, I want editor tools to validate LOD configurations, so that I can identify and fix LOD issues during development.

#### Acceptance Criteria

1. WHERE Unity Editor is active, THE LOD_System SHALL provide LOD validation menu command
2. WHERE Unity Editor is active, THE LOD_System SHALL scan all prefabs for LOD_Group components
3. WHERE Unity Editor is active, THE LOD_System SHALL report assets missing required LOD levels
4. WHERE Unity Editor is active, THE LOD_System SHALL report assets with incorrect polygon count ratios
5. WHERE Unity Editor is active, THE LOD_System SHALL visualize LOD transition distances via Gizmos
6. WHERE Unity Editor is active, THE LOD_System SHALL display current LOD level per object in Scene view
7. WHERE Unity Editor is active, THE LOD_System SHALL provide LOD statistics window showing system performance

---

## Non-Functional Requirements

### Performance Requirements

- **Frame Rate:** Maintain 60 FPS @ 1080p on NVIDIA MX350 (target hardware)
- **Frame Budget:** LOD system processing ≤ 2 milliseconds per frame
- **Memory Allocation:** Zero bytes garbage collection per frame during gameplay
- **SetPass Calls:** ≤ 600 per frame
- **Batch Count:** ≤ 1800 per frame
- **Job System:** Distance calculations complete within 2 milliseconds

### Quality Requirements

- **LOD Transitions:** No visible popping artifacts within 50 meters
- **Crossfade Duration:** 0.5 to 1.0 seconds for smooth blending
- **Polygon Reduction:** LOD1 ≤ 50% LOD0, LOD2 ≤ 25% LOD0
- **Impostor Quality:** 512x512 texture resolution minimum
- **Culling Accuracy:** Hysteresis prevents activation thrashing

### Scalability Requirements

- **Registered Objects:** Support 10,000+ LOD_Group instances
- **Concurrent Jobs:** Batch process distance calculations for all objects
- **Memory Footprint:** LOD system overhead ≤ 10 MB
- **Scene Complexity:** Support large open-world environments (10km x 10km)

### Compatibility Requirements

- **Unity Version:** Unity 6000.4.1f1 URP
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Third-Party Integration:** Amplify Impostors plugin
- **Platform:** Windows 10/11, target hardware NVIDIA MX350

---

## Dependencies & Constraints

### External Dependencies

- **Unity URP:** LOD system relies on URP rendering pipeline
- **Amplify Impostors:** Required for impostor generation and rendering
- **GameTickManager:** LOD system integrates with existing tick architecture
- **SaveManager:** LOD settings persistence via Easy Save 3
- **Unity Jobs System:** Distance calculations use Burst-compiled jobs

### Technical Constraints

- **Zero GC Allocation:** All hot paths must produce zero garbage
- **Burst Compilation:** Distance calculations must use Burst compiler
- **Object Pooling:** Impostor billboards must use ObjectPoolManager
- **ITickable Pattern:** LOD updates must use GameTickManager, not Update()
- **Singleton Pattern:** LOD_System must follow project singleton conventions

### Performance Constraints

- **Main Thread Budget:** ≤ 12 milliseconds per frame
- **GC Budget:** 0 bytes per frame
- **Memory Budget:** ≤ 4096 MB total application memory
- **VRAM Budget:** Texture memory ≤ 900 MB, RenderTexture ≤ 500 MB

---

## Success Criteria

### Technical Success

- ✅ All meshes > 0.5m have LOD groups configured
- ✅ Hero assets have 3 LOD levels (LOD0+LOD1+LOD2+Cull)
- ✅ Standard props have 2 LOD levels (LOD0+LOD1+Cull)
- ✅ LOD system produces zero GC allocations per frame
- ✅ LOD processing time ≤ 2 milliseconds per frame
- ✅ SetPass calls ≤ 600, Batch count ≤ 1800

### Gameplay Success

- ✅ 60 FPS maintained @ 1080p on target hardware
- ✅ No visible LOD popping within 50 meters
- ✅ Smooth crossfade transitions for near-field objects
- ✅ Dynamic resolution maintains frame rate during demanding scenes
- ✅ Quality presets provide meaningful performance/quality tradeoffs

### Production Success

- ✅ LOD validation tools integrated in Unity Editor
- ✅ All existing assets validated and configured
- ✅ LOD settings persist across save/load cycles
- ✅ Performance monitoring tools available for profiling
- ✅ Documentation complete for technical artists

---

## Risks & Mitigation

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Job system overhead exceeds budget | HIGH | MEDIUM | Profile early, optimize batch sizes, use Burst |
| Crossfade transitions cause frame drops | MEDIUM | MEDIUM | Limit concurrent crossfades, use distance threshold |
| Impostor generation memory spikes | MEDIUM | LOW | Generate impostors offline, cache in Addressables |
| LOD thrashing at transition boundaries | LOW | MEDIUM | Implement hysteresis, smooth distance calculations |

### Project Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Asset rework required for LOD compliance | HIGH | HIGH | Automated validation tools, batch processing scripts |
| Third-party plugin compatibility issues | MEDIUM | LOW | Version lock Amplify Impostors, test thoroughly |
| Performance regression on target hardware | HIGH | MEDIUM | Continuous profiling, performance benchmarks |

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-XX  
**Status:** DRAFT — AWAITING USER REVIEW  
**Next Phase:** Design Document Creation (after user approval)
