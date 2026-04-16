# Requirements Document: Camera Juice & Screen Effects System

## Introduction

The Camera Juice & Screen Effects System provides dynamic camera feedback and post-processing effects for HECTON-8. The system responds to gameplay events (impacts, sprint, damage) with camera shake and FOV changes, and modulates post-processing effects based on player health and environmental context. All effects must maintain zero GC allocation in hot paths and execute within 1ms per frame on target hardware (NVIDIA MX350 2GB VRAM).

## Glossary

- **CameraJuiceSystem**: The runtime system managing camera shake, FOV effects, and post-processing modulation
- **ShakeProfile**: Configuration data defining shake intensity, frequency, duration, and falloff curves
- **FOVKick**: Temporary field-of-view adjustment triggered by gameplay events
- **HealthVignette**: Post-processing vignette effect that intensifies as player health decreases
- **O2ChromaticAberration**: Chromatic aberration effect that intensifies as oxygen levels decrease
- **BiomeProfile**: Post-processing configuration specific to a biome or environmental zone
- **PerformanceMode**: Runtime quality setting that enables/disables optional expensive effects
- **InteractionFocus**: Depth-of-field effect that focuses on interaction targets
- **HectonSurvivalSystem**: Existing system tracking player O2 and health
- **PlayerMovement**: Existing system detecting sprint state
- **MainCamera**: The primary gameplay camera
- **URPVolume**: Unity URP Volume component for post-processing
- **DOTween**: Zero-GC animation library used for smooth transitions
- **ITickable**: Interface for systems updated via GameTickManager
- **ObjectPoolManager**: Singleton managing object pooling for zero-GC spawning

## Requirements

### Requirement 1: Camera Shake System

**User Story:** As a player, I want the camera to shake on impacts and explosions, so that I feel the weight and force of events in the game world.

#### Acceptance Criteria

1. WHEN an impact event occurs, THE CameraJuiceSystem SHALL apply camera shake with intensity proportional to impact force
2. WHEN an explosion event occurs, THE CameraJuiceSystem SHALL apply camera shake with intensity inversely proportional to distance from explosion center
3. THE CameraJuiceSystem SHALL support configurable ShakeProfiles defining intensity, frequency, duration, and falloff curves
4. THE CameraJuiceSystem SHALL blend multiple simultaneous shake effects additively without exceeding maximum displacement bounds
5. THE CameraJuiceSystem SHALL complete all shake calculations within 0.2ms per frame
6. THE CameraJuiceSystem SHALL allocate zero bytes per frame during shake execution
7. WHEN shake intensity is set to zero via settings, THE CameraJuiceSystem SHALL bypass all shake calculations

### Requirement 2: FOV Effects System

**User Story:** As a player, I want the field of view to change during sprint and damage events, so that I experience visual feedback for my movement and survival state.

#### Acceptance Criteria

1. WHEN PlayerMovement enters sprint state, THE CameraJuiceSystem SHALL increase MainCamera FOV by a configurable sprint kick amount over a configurable duration
2. WHEN PlayerMovement exits sprint state, THE CameraJuiceSystem SHALL return MainCamera FOV to baseline over a configurable duration
3. WHEN HectonSurvivalSystem reports damage taken, THE CameraJuiceSystem SHALL apply a temporary FOV reduction proportional to damage amount
4. THE CameraJuiceSystem SHALL use DOTween for all FOV transitions to ensure zero-GC smooth interpolation
5. THE CameraJuiceSystem SHALL clamp FOV values between configurable minimum and maximum bounds
6. THE CameraJuiceSystem SHALL complete all FOV calculations within 0.1ms per frame
7. WHEN multiple FOV effects are active simultaneously, THE CameraJuiceSystem SHALL apply the effect with highest priority and queue others

### Requirement 3: Health-Based Post-Processing

**User Story:** As a player, I want visual feedback when my health or oxygen is low, so that I am aware of survival threats without constantly checking UI.

#### Acceptance Criteria

1. WHEN HectonSurvivalSystem reports health below 30%, THE CameraJuiceSystem SHALL increase HealthVignette intensity proportional to health deficit
2. WHEN HectonSurvivalSystem reports health at or above 30%, THE CameraJuiceSystem SHALL disable HealthVignette
3. WHEN HectonSurvivalSystem reports O2 below 20%, THE CameraJuiceSystem SHALL increase O2ChromaticAberration intensity proportional to O2 deficit
4. WHEN HectonSurvivalSystem reports O2 at or above 20%, THE CameraJuiceSystem SHALL disable O2ChromaticAberration
5. THE CameraJuiceSystem SHALL update health and O2 post-processing effects via ISlowTickable at 2Hz maximum frequency
6. THE CameraJuiceSystem SHALL use MaterialPropertyBlock for all post-processing parameter updates to avoid material instance allocation
7. THE CameraJuiceSystem SHALL cache all Shader.PropertyToID values as static readonly fields

### Requirement 4: Biome-Based Post-Processing Profiles

**User Story:** As a player, I want the visual atmosphere to change between biomes, so that each environment feels distinct and immersive.

#### Acceptance Criteria

1. WHEN the player enters a new biome zone, THE CameraJuiceSystem SHALL transition to the corresponding BiomeProfile over a configurable blend duration
2. THE CameraJuiceSystem SHALL support BiomeProfiles defining color grading, ambient occlusion, bloom, and fog parameters
3. THE CameraJuiceSystem SHALL blend between BiomeProfiles using DOTween to ensure zero-GC smooth transitions
4. THE CameraJuiceSystem SHALL apply BiomeProfile changes to the global URPVolume without creating new Volume instances
5. THE CameraJuiceSystem SHALL complete biome profile transitions within 0.15ms per frame during blend
6. WHEN no BiomeProfile is assigned to a zone, THE CameraJuiceSystem SHALL use a default fallback profile

### Requirement 5: Depth of Field for Interaction Focus

**User Story:** As a player, I want the camera to focus on objects I can interact with, so that important gameplay elements are visually emphasized.

#### Acceptance Criteria

1. WHEN an IInteractable enters hover state, THE CameraJuiceSystem SHALL enable InteractionFocus depth-of-field targeting the interactable's position
2. WHEN an IInteractable exits hover state, THE CameraJuiceSystem SHALL disable InteractionFocus depth-of-field over a configurable fade duration
3. THE CameraJuiceSystem SHALL calculate focus distance using Vector3.Distance between MainCamera position and target position
4. THE CameraJuiceSystem SHALL update focus distance every frame while InteractionFocus is active
5. THE CameraJuiceSystem SHALL use Gaussian depth-of-field mode for gameplay to minimize performance cost
6. WHERE PerformanceMode is Low, THE CameraJuiceSystem SHALL disable InteractionFocus depth-of-field entirely
7. THE CameraJuiceSystem SHALL complete all depth-of-field calculations within 0.1ms per frame

### Requirement 6: Performance Optimization and Budget Compliance

**User Story:** As a developer, I want the camera effects system to meet strict performance budgets, so that it does not degrade overall game performance on target hardware.

#### Acceptance Criteria

1. THE CameraJuiceSystem SHALL complete all per-frame calculations within 1.0ms total on NVIDIA MX350 hardware
2. THE CameraJuiceSystem SHALL allocate zero bytes per frame in all hot paths (Tick, FixedTick, SlowTick)
3. THE CameraJuiceSystem SHALL pre-allocate all buffers and data structures during Awake with COLD ALLOC comments
4. THE CameraJuiceSystem SHALL use ITickable interface for per-frame updates instead of Update method
5. THE CameraJuiceSystem SHALL cache MainCamera reference in Awake to avoid Camera.main calls
6. THE CameraJuiceSystem SHALL use static readonly int fields for all Animator.StringToHash and Shader.PropertyToID values
7. WHERE PerformanceMode is Low, THE CameraJuiceSystem SHALL disable motion blur and reduce post-processing quality
8. THE CameraJuiceSystem SHALL implement IPoolable for any spawned effect objects with proper OnSpawn and OnDespawn state reset

### Requirement 7: Settings Integration and User Control

**User Story:** As a player, I want to control camera effects intensity and enable/disable specific effects, so that I can customize the experience to my preferences.

#### Acceptance Criteria

1. THE CameraJuiceSystem SHALL expose a camera shake intensity multiplier setting ranging from 0.0 to 2.0
2. THE CameraJuiceSystem SHALL expose a FOV effects intensity multiplier setting ranging from 0.0 to 2.0
3. THE CameraJuiceSystem SHALL expose boolean toggles for motion blur, chromatic aberration, and depth-of-field effects
4. WHEN a setting is changed, THE CameraJuiceSystem SHALL apply the new value immediately without requiring scene reload
5. THE CameraJuiceSystem SHALL persist all settings via SaveManager using ISaveable interface
6. THE CameraJuiceSystem SHALL provide default settings values that work well on target hardware
7. THE CameraJuiceSystem SHALL validate all settings values and clamp to valid ranges before application

### Requirement 8: Zero-GC Design with Object Pooling

**User Story:** As a developer, I want all camera effects to use object pooling and zero-GC patterns, so that the system adheres to HECTON-8 performance standards.

#### Acceptance Criteria

1. THE CameraJuiceSystem SHALL register with GameTickManager via ITickable interface in OnEnable and unregister in OnDisable
2. THE CameraJuiceSystem SHALL use a boolean _registered field to prevent double registration
3. THE CameraJuiceSystem SHALL use DOTween for all animations to ensure zero-GC interpolation
4. THE CameraJuiceSystem SHALL avoid LINQ, foreach on Dictionary, string concatenation, and GetComponent calls in hot paths
5. THE CameraJuiceSystem SHALL pre-allocate all List and Dictionary collections with explicit capacity in Awake
6. THE CameraJuiceSystem SHALL use for loops with cached count instead of foreach for all hot path iterations
7. THE CameraJuiceSystem SHALL implement state machines using enum State fields instead of coroutines for multi-frame operations
8. IF the CameraJuiceSystem spawns temporary effect objects, THEN THE CameraJuiceSystem SHALL use ObjectPoolManager.Instance.Spawn and Despawn

### Requirement 9: Integration with Existing Systems

**User Story:** As a developer, I want the camera effects system to integrate cleanly with existing HECTON-8 systems, so that it works within the established architecture.

#### Acceptance Criteria

1. THE CameraJuiceSystem SHALL subscribe to HectonSurvivalSystem events for health and O2 changes in OnEnable and unsubscribe in OnDisable
2. THE CameraJuiceSystem SHALL subscribe to PlayerMovement events for sprint state changes in OnEnable and unsubscribe in OnDisable
3. THE CameraJuiceSystem SHALL subscribe to InteractionEvents for hover state changes in OnEnable and unsubscribe in OnDisable
4. THE CameraJuiceSystem SHALL access MainCamera via cached reference initialized in Awake with null-check fallback
5. THE CameraJuiceSystem SHALL access URPVolume via cached reference initialized in Awake with null-check fallback
6. THE CameraJuiceSystem SHALL use GameTickManager.Instance for tick registration with null-check guard
7. THE CameraJuiceSystem SHALL use SaveManager.Instance for settings persistence with null-check guard
8. THE CameraJuiceSystem SHALL implement ISaveable with LoadPriority between 51-100 (Player tier)

### Requirement 10: Biome Profile Authoring and Management

**User Story:** As a developer, I want to author and manage biome-specific post-processing profiles, so that each environment can have unique visual characteristics.

#### Acceptance Criteria

1. THE CameraJuiceSystem SHALL load BiomeProfiles from ScriptableObject assets without runtime mutation
2. THE CameraJuiceSystem SHALL support at least 8 distinct BiomeProfiles for different environmental zones
3. THE CameraJuiceSystem SHALL validate BiomeProfile parameters on load and log errors for invalid configurations
4. THE CameraJuiceSystem SHALL provide a default BiomeProfile that works well across all biomes as fallback
5. WHERE a BiomeProfile defines optional effects, THE CameraJuiceSystem SHALL respect PerformanceMode settings when applying them
6. THE CameraJuiceSystem SHALL cache all BiomeProfile references during initialization to avoid Addressables lookups in hot paths

### Requirement 11: Debug Visualization and Monitoring

**User Story:** As a developer, I want debug visualization for camera effects, so that I can verify correct behavior during development.

#### Acceptance Criteria

1. WHERE UNITY_EDITOR is defined, THE CameraJuiceSystem SHALL provide OnDrawGizmos visualization of shake displacement vectors
2. WHERE UNITY_EDITOR is defined, THE CameraJuiceSystem SHALL provide OnDrawGizmos visualization of current FOV target and blend progress
3. WHERE UNITY_EDITOR is defined, THE CameraJuiceSystem SHALL provide OnDrawGizmos visualization of depth-of-field focus distance
4. THE CameraJuiceSystem SHALL expose read-only properties for current shake intensity, FOV offset, and active effect count
5. WHERE UNITY_EDITOR or DEVELOPMENT_BUILD is defined, THE CameraJuiceSystem SHALL log warnings when frame time exceeds 1.0ms budget
6. THE CameraJuiceSystem SHALL throttle debug logs to maximum 1 log per 5 seconds to avoid log spam

### Requirement 12: Graceful Degradation and Error Handling

**User Story:** As a developer, I want the camera effects system to handle errors gracefully, so that missing dependencies or invalid configurations do not crash the game.

#### Acceptance Criteria

1. IF MainCamera is null during initialization, THEN THE CameraJuiceSystem SHALL log an error and disable itself
2. IF URPVolume is null during initialization, THEN THE CameraJuiceSystem SHALL log an error and disable post-processing effects only
3. IF GameTickManager.Instance is null during OnEnable, THEN THE CameraJuiceSystem SHALL log an error and skip registration
4. IF HectonSurvivalSystem is not found, THEN THE CameraJuiceSystem SHALL log a warning and disable health/O2 effects only
5. IF PlayerMovement is not found, THEN THE CameraJuiceSystem SHALL log a warning and disable sprint FOV effects only
6. WHEN any effect calculation throws an exception, THE CameraJuiceSystem SHALL catch it, log the error, disable the specific effect, and continue operation
7. THE CameraJuiceSystem SHALL validate all ShakeProfile and BiomeProfile parameters and clamp to safe ranges before application
