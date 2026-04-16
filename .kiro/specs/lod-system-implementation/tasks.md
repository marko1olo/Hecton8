# LOD System Implementation — Tasks

## Task Breakdown

### Phase 1: Core Infrastructure

- [x] 1. Create LODSystemManager singleton
  - [x] 1.1 Implement singleton pattern with Instance property
  - [x] 1.2 Add [DefaultExecutionOrder(-150)] attribute
  - [x] 1.3 Implement ITickable interface
  - [x] 1.4 Implement ISaveable interface (SavePriority=5, LoadPriority=5)
  - [x] 1.5 Add inspector settings (quality preset, crossfade config)
  - [x] 1.6 Pre-allocate collections in Awake (List<LODGroup>[500], NativeArrays)
  - [x] 1.7 Implement RegisterLODGroup/UnregisterLODGroup methods
  - [x] 1.8 Cache Camera.main and transform in Tick
  - [x] 1.9 Add XML documentation to all public members

- [x] 2. Implement DistanceCalculationJob
  - [x] 2.1 Create Burst-compiled IJobParallelFor struct
  - [x] 2.2 Add [BurstCompile] attribute with FloatMode.Fast
  - [x] 2.3 Implement Execute method (squared distance calculation)
  - [x] 2.4 Add NativeArray inputs/outputs (positions, distances)
  - [x] 2.5 Implement job scheduling in LODSystemManager
  - [x] 2.6 Implement job completion and result processing
  - [x] 2.7 Add job disposal in OnDestroy

- [x] 3. Implement LOD transition logic
  - [x] 3.1 Create ApplyLODTransitions method
  - [x] 3.2 Implement crossfade mode for objects < 50m
  - [x] 3.3 Implement discrete switching for objects > 50m
  - [x] 3.4 Apply LOD bias multiplier to transition distances
  - [x] 3.5 Configure LODGroup.fadeMode and animateCrossFading
  - [x] 3.6 Add crossfade duration configuration
  - [x] 3.7 Disable crossfade for transparent materials

- [x] 4. Implement quality preset system
  - [x] 4.1 Create QualityPreset enum (Low/Medium/High)
  - [x] 4.2 Implement GetLODBias method (1.5/1.0/0.7)
  - [x] 4.3 Implement SetQualityPreset method
  - [x] 4.4 Apply LOD bias changes immediately without scene reload
  - [x] 4.5 Add quality preset to save/load data
  - [x] 4.6 Validate loaded quality preset values

---

### Phase 2: Culling Systems

- [x] 5. Create CullingManager singleton
  - [x] 5.1 Implement singleton pattern with Instance property
  - [x] 5.2 Add [DefaultExecutionOrder(-140)] attribute
  - [x] 5.3 Implement ISlowTickable interface
  - [x] 5.4 Add inspector settings (cull distances, hysteresis)
  - [x] 5.5 Pre-allocate collections in Awake (List<CullableObject>[1000])
  - [x] 5.6 Implement RegisterCullableObject/UnregisterCullableObject
  - [x] 5.7 Cache Camera.main in SlowTick
  - [x] 5.8 Add XML documentation to all public members

- [x] 6. Implement distance-based culling
  - [x] 6.1 Create CullableObject struct (GameObject, Transform, Bounds, distances)
  - [x] 6.2 Implement size-based cull distance assignment (30m/80m/200m)
  - [x] 6.3 Implement hysteresis logic (10% threshold difference)
  - [x] 6.4 Implement GameObject.SetActive(false) for culled objects
  - [x] 6.5 Implement GameObject.SetActive(true) for reactivated objects
  - [x] 6.6 Track DistanceCulledCount property
  - [x] 6.7 Add #if UNITY_EDITOR guards for debug logging

- [x] 7. Implement frustum culling integration
  - [x] 7.1 Pre-allocate Plane[6] array for frustum planes
  - [x] 7.2 Call GeometryUtility.CalculateFrustumPlanes in SlowTick
  - [x] 7.3 Verify Unity frustum culling enabled for all renderers
  - [x] 7.4 Track FrustumCulledCount property
  - [x] 7.5 Integrate with Unity's built-in culling (no duplication)
  - [x] 7.6 Add performance monitoring (< 1ms per SlowTick)

- [x] 8. Implement layer-based cull distances
  - [x] 8.1 Create ApplyLayerCullDistances method
  - [x] 8.2 Set debris layer cull distance to 40m
  - [x] 8.3 Set particles layer cull distance to 40m
  - [x] 8.4 Set props layer cull distance to 100m
  - [x] 8.5 Set flora layer cull distance to 100m
  - [x] 8.6 Set terrain layer to camera far clip plane
  - [x] 8.7 Apply Camera.layerCullDistances in scene initialization

- [x] 9. Implement occlusion culling validation
  - [x] 9.1 Check for occlusion culling data in current scene
  - [x] 9.2 Log warning once per scene load if data missing
  - [x] 9.3 Validate Occludee Static flag for objects > 1m³
  - [x] 9.4 Validate Occluder Static flag for objects > 2m³
  - [x] 9.5 Expose occlusion-culled object count property
  - [x] 9.6 Add editor validation tool for occlusion settings

---

### Phase 3: Impostor System

- [x] 10. Create ImpostorSystem singleton
  - [x] 10.1 Implement singleton pattern with Instance property
  - [x] 10.2 Add inspector settings (distance threshold, texture resolution)
  - [x] 10.3 Pre-allocate collections (Dictionary<int, GameObject>[100])
  - [x] 10.4 Implement RegisterImpostorCandidate/UnregisterImpostorCandidate
  - [x] 10.5 Cache impostor textures in Addressables
  - [x] 10.6 Add XML documentation to all public members

- [x] 11. Integrate Amplify Impostors plugin
  - [x] 11.1 Verify Amplify Impostors plugin installed
  - [x] 11.2 Create impostor baking preset (512x512, octahedral mapping)
  - [x] 11.3 Implement offline impostor texture generation
  - [x] 11.4 Store impostor textures in Addressables asset bundles
  - [x] 11.5 Implement impostor texture loading at runtime
  - [x] 11.6 Add error handling for missing impostor textures

- [x] 12. Implement impostor billboard pooling
  - [x] 12.1 Create ImpostorInstance struct
  - [x] 12.2 Integrate with ObjectPoolManager for billboard spawning
  - [x] 12.3 Implement IPoolable for billboard prefabs
  - [x] 12.4 Implement impostor activation at 150m threshold
  - [x] 12.5 Implement crossfade transition from LOD2 to impostor
  - [x] 12.6 Implement impostor deactivation when returning to LOD2 range
  - [x] 12.7 Track ActiveImpostorCount property

---

### Phase 4: Dynamic Resolution Scaling

- [x] 13. Create DynamicResolutionScaler singleton
  - [x] 13.1 Implement singleton pattern with Instance property
  - [x] 13.2 Add [DefaultExecutionOrder(-130)] attribute
  - [x] 13.3 Implement ITickable interface
  - [x] 13.4 Add inspector settings (target frame time, scale limits)
  - [x] 13.5 Initialize render scale to 1.0
  - [x] 13.6 Add XML documentation to all public members

- [x] 14. Implement frame time monitoring
  - [x] 14.1 Convert dt to milliseconds in Tick
  - [x] 14.2 Track consecutive slow frames (> 16.67ms)
  - [x] 14.3 Track consecutive fast frames (< 15ms)
  - [x] 14.4 Reduce render scale by 5% after 3 slow frames
  - [x] 14.5 Increase render scale by 2% after 30 fast frames
  - [x] 14.6 Clamp render scale between 0.5 and 1.0
  - [x] 14.7 Apply render scale to UniversalRenderPipeline.asset

- [x] 15. Implement quality preset integration
  - [x] 15.1 Set minimum render scale based on quality preset
  - [x] 15.2 Low preset: min scale = 0.7
  - [x] 15.3 Medium/High preset: min scale = 0.5
  - [x] 15.4 Expose CurrentRenderScale property
  - [x] 15.5 Implement SetEnabled method
  - [x] 15.6 Save/load dynamic resolution enabled state

---

### Phase 5: Editor Tools

- [x] 16. Create LOD validation window
  - [x] 16.1 Create EditorWindow class (Hecton8/LOD System/Validate LOD Groups)
  - [x] 16.2 Scan all prefabs for LODGroup components
  - [x] 16.3 Report missing LOD levels (LOD0+LOD1+Cull minimum)
  - [x] 16.4 Report incorrect polygon count ratios
  - [x] 16.5 Report assets visible beyond 20m without LOD groups
  - [x] 16.6 Export validation report to CSV
  - [x] 16.7 Add #if UNITY_EDITOR guards

- [x] 17. Create LOD statistics window
  - [x] 17.1 Create EditorWindow class (Hecton8/LOD System/LOD Statistics)
  - [x] 17.2 Display registered LOD group count
  - [x] 17.3 Display active impostor count
  - [x] 17.4 Display frustum/distance culled counts
  - [x] 17.5 Display current render scale
  - [x] 17.6 Display LOD system CPU time graph
  - [x] 17.7 Add refresh button and auto-refresh toggle

- [x] 18. Implement LOD Gizmos
  - [x] 18.1 Add OnDrawGizmos method with #if UNITY_EDITOR guard
  - [x] 18.2 Draw LOD transition distance spheres (color-coded)
  - [x] 18.3 Draw current LOD level label per object
  - [x] 18.4 Draw cull distance visualization
  - [x] 18.5 Draw impostor activation threshold
  - [x] 18.6 Add Gizmos enable/disable toggle in inspector
  - [x] 18.7 Optimize Gizmos (only draw for selected objects)

---

### Phase 6: Integration & Testing

- [x] 19. Integrate with existing systems
  - [x] 19.1 Verify GameTickManager registration/unregistration
  - [x] 19.2 Verify SaveManager integration (save/load settings)
  - [x] 19.3 Verify ObjectPoolManager integration (impostor billboards)
  - [x] 19.4 Test scene load/unload cleanup
  - [x] 19.5 Test with existing LODGroups in project
  - [x] 19.6 Verify no conflicts with existing culling systems

- [ ] 20. Performance validation
  - [ ] 20.1 Profile LOD system CPU time (target < 2ms/frame)
  - [ ] 20.2 Verify zero GC allocations in hot paths
  - [ ] 20.3 Test with 10,000+ LODGroups in scene
  - [ ] 20.4 Verify 60 FPS @ 1080p on MX350
  - [ ] 20.5 Verify SetPass calls ≤ 600, Batches ≤ 1800
  - [ ] 20.6 Profile NativeArray allocation/disposal

- [ ] 21. Unit tests
  - [ ] 21.1 Test LODSystemManager registration/unregistration
  - [ ] 21.2 Test distance calculation accuracy
  - [ ] 21.3 Test LOD bias application
  - [ ] 21.4 Test quality preset switching
  - [ ] 21.5 Test save/load persistence
  - [ ] 21.6 Test NativeArray disposal
  - [ ] 21.7 Achieve 80% code coverage

- [ ] 22. Integration tests
  - [ ] 22.1 Test 1000+ LODGroups in scene
  - [ ] 22.2 Test rapid camera movement (LOD thrashing prevention)
  - [ ] 22.3 Test scene load/unload (cleanup verification)
  - [ ] 22.4 Test save/load cycle (settings persistence)
  - [ ] 22.5 Test dynamic resolution scaling under load
  - [ ] 22.6 Test impostor activation/deactivation

---

### Phase 7: Documentation & Polish

- [x] 23. Create system documentation
  - [x] 23.1 Write README.md (system overview, usage guide)
  - [x] 23.2 Write ARCHITECTURE.md (detailed design, data flow)
  - [x] 23.3 Write INTEGRATION_GUIDE.md (how to integrate with existing code)
  - [x] 23.4 Write PERFORMANCE_GUIDE.md (optimization tips, profiling)
  - [x] 23.5 Add inline code comments for complex logic
  - [x] 23.6 Add XML documentation to all public APIs

- [ ] 24. Final validation
  - [ ] 24.1 Run all unit tests (80% coverage)
  - [ ] 24.2 Run all integration tests
  - [ ] 24.3 Profile on target hardware (MX350)
  - [ ] 24.4 Verify zero GC allocations
  - [ ] 24.5 Verify 60 FPS target maintained
  - [x] 24.6 Code review with AGENTS.MD checklist
  - [x] 24.7 Update SYSTEM STATUS LEDGER in AGENTS.MD

---

## Estimated Effort

| Phase | Tasks | Estimated Hours |
|-------|-------|-----------------|
| Phase 1: Core Infrastructure | 1-4 | 8 hours |
| Phase 2: Culling Systems | 5-9 | 10 hours |
| Phase 3: Impostor System | 10-12 | 8 hours |
| Phase 4: Dynamic Resolution | 13-15 | 4 hours |
| Phase 5: Editor Tools | 16-18 | 6 hours |
| Phase 6: Integration & Testing | 19-22 | 8 hours |
| Phase 7: Documentation | 23-24 | 4 hours |
| **Total** | **24 tasks** | **48 hours** |

---

## Dependencies

- Unity 6000.4.1f1 URP
- Amplify Impostors plugin
- GameTickManager (existing)
- SaveManager (existing)
- ObjectPoolManager (existing)
- Unity Jobs System
- Unity Burst Compiler

---

## Success Criteria

- ✅ All 24 tasks completed
- ✅ Zero GC allocations in hot paths
- ✅ LOD system CPU time < 2ms/frame
- ✅ 60 FPS @ 1080p on MX350
- ✅ SetPass calls ≤ 600, Batches ≤ 1800
- ✅ 80% unit test coverage
- ✅ All integration tests passing
- ✅ Documentation complete

---

**Document Version:** 1.0  
**Last Updated:** 2025-04-15  
**Status:** READY FOR EXECUTION
