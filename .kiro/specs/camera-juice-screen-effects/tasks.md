# Implementation Plan: Camera Juice & Screen Effects System

## Overview

This implementation plan breaks down the Camera Juice & Screen Effects System into discrete coding tasks. The system provides dynamic camera feedback (shake, FOV effects) and post-processing modulation (health vignette, O2 chromatic aberration, biome profiles, interaction focus) within strict performance constraints (1.0ms frame budget, zero-GC hot paths).

**Architecture Context:**
- CameraJuiceSystem: Singleton MonoBehaviour implementing ITickable, ISlowTickable, ISaveable
- Integration: HectonSurvivalSystem (health/O2 events), PlayerMovement (sprint state), InteractionEvents (hover), GameTickManager (tick registration), SaveManager (settings persistence)
- Zero-GC patterns: DOTween, MaterialPropertyBlock, pre-allocated collections, cached references
- Frame budget: 1.0ms total (0.2ms shake, 0.1ms FOV, 0.1ms DoF, 0.15ms health/O2, 0.15ms biome)

## Tasks

- [x] 1. Core Infrastructure Setup
  - Create CameraJuiceSystem singleton MonoBehaviour in Assets/_Project/Scripts/VFX/
  - Implement singleton pattern (explicit _instance field, Awake null-check, OnDestroy cleanup)
  - Cache MainCamera reference in Awake (Camera.main → _mainCamera)
  - Cache URPVolume reference in Awake (GetComponent<Volume>() → _urpVolume)
  - Cache camera Transform reference (_cameraTransform = _mainCamera.transform)
  - Pre-allocate ActiveShake list: `List<ActiveShake>(8)` with COLD ALLOC comment
  - Pre-allocate MaterialPropertyBlock: `new MaterialPropertyBlock()` with COLD ALLOC comment
  - Define static readonly Shader.PropertyToID fields (_VignetteIntensity, _ChromaticIntensity)
  - Implement graceful degradation (null MainCamera → disable system, null URPVolume → disable post-processing only)
  - _Requirements: 1.1, 1.5, 1.6, 6.3, 6.5, 6.6, 9.4, 9.5, 12.1, 12.2_

- [x] 2. GameTickManager Integration
  - [x] 2.1 Implement ITickable interface
    - Add `Tick(float dt)` method signature
    - Add `_registered` boolean field to prevent double registration
    - Implement OnEnable: null-check GameTickManager.Instance, guard with `!_registered`, call `Register(ITickable)`, set `_registered = true`
    - Implement OnDisable: null-check GameTickManager.Instance, guard with `_registered`, call `Unregister(ITickable)`, set `_registered = false`
    - _Requirements: 6.4, 8.1, 8.2, 9.6_
  
  - [x] 2.2 Implement ISlowTickable interface
    - Add `SlowTick()` method signature
    - Update OnEnable: call `Register(ISlowTickable)` after ITickable registration
    - Update OnDisable: call `Unregister(ISlowTickable)` before ITickable unregistration
    - _Requirements: 3.5, 8.1, 8.2_

- [x] 3. Camera Shake System
  - [x] 3.1 Create ShakeProfile ScriptableObject
    - Create ShakeProfile.cs in Assets/_Project/Scripts/VFX/
    - Add [CreateAssetMenu] attribute with path "HECTON-8/VFX/Shake Profile"
    - Define fields: MaxDisplacement (Range 0-0.5f), Frequency (Range 1-30f), Duration (Range 0.1-3f), FalloffCurve (AnimationCurve), AxisWeights (Vector3)
    - Add [Header] and [Tooltip] attributes on all fields
    - _Requirements: 1.3_
  
  - [x] 3.2 Implement ActiveShake struct
    - Define struct with fields: ShakeProfile Profile, float Elapsed, float IntensityScale, Vector3 Offset
    - Place struct inside CameraJuiceSystem class (zero-GC value type)
    - _Requirements: 1.4, 1.6_
  
  - [x] 3.3 Implement TriggerShake public API
    - Add `TriggerShake(ShakeProfile profile, float intensityScale = 1.0f)` method
    - Validate profile parameter (null-check, log error if null)
    - Check capacity: if `_activeShakes.Count >= 8`, remove oldest shake at index 0
    - Create new ActiveShake struct, add to _activeShakes list
    - _Requirements: 1.1, 1.4_
  
  - [x] 3.4 Implement UpdateShake in Tick
    - Add private `UpdateShake(float dt)` method called from Tick
    - Bypass if `_shakeIntensityMultiplier <= 0f` (zero-intensity optimization)
    - Iterate _activeShakes with for loop (cached count)
    - For each shake: increment Elapsed by dt, evaluate FalloffCurve at (Elapsed / Duration)
    - Generate Perlin noise offset using Mathf.PerlinNoise(Time.time * Frequency)
    - Scale offset by (MaxDisplacement * falloffValue * intensityScale * _shakeIntensityMultiplier)
    - Weight offset by AxisWeights, accumulate into _shakeOffset
    - Remove completed shakes (Elapsed >= Duration) using RemoveAt in reverse order
    - Clamp _shakeOffset magnitude to 0.5 units (MAX_SHAKE_DISPLACEMENT constant)
    - Apply _shakeOffset to _cameraTransform.localPosition
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7_

- [x] 4. FOV Effects System
  - [x] 4.1 Implement FOV state machine
    - Define FOVState enum: Idle, SprintKick, DamageRecoil
    - Add fields: FOVState _fovState, float _baseFOV, float _currentFOVOffset, Tween _fovTween
    - Cache _baseFOV in Awake: `_baseFOV = _mainCamera.fieldOfView`
    - _Requirements: 2.1, 2.2, 2.7_
  
  - [x] 4.2 Implement TriggerFOVKick public API
    - Add `TriggerFOVKick(float amount, float duration)` method
    - Kill existing _fovTween if active
    - Use DOTween.To to animate _currentFOVOffset to target amount over duration
    - Set Ease.OutQuad for smooth interpolation
    - Store tween handle in _fovTween field
    - _Requirements: 2.1, 2.2, 2.4_
  
  - [x] 4.3 Implement UpdateFOV in Tick
    - Add private `UpdateFOV(float dt)` method called from Tick
    - Calculate target FOV: `_baseFOV + (_currentFOVOffset * _fovIntensityMultiplier)`
    - Clamp FOV to min/max bounds (define constants: MIN_FOV = 40f, MAX_FOV = 90f)
    - Apply to _mainCamera.fieldOfView
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6_
  
  - [x] 4.4 Integrate sprint FOV kick
    - Cache PlayerMovement reference in Awake (FindObjectOfType with null-check warning)
    - In UpdateFOV, poll PlayerMovement._isSprinting field (or subscribe to events if added)
    - On sprint start: call TriggerFOVKick with positive amount (e.g., +10f, 0.3s duration)
    - On sprint end: call TriggerFOVKick with zero amount (return to baseline, 0.3s duration)
    - _Requirements: 2.1, 2.2, 9.2, 12.5_
  
  - [x] 4.5 Integrate damage FOV recoil
    - Subscribe to HectonSurvivalSystem.OnIntegrityChanged in OnEnable
    - Unsubscribe in OnDisable
    - On damage event: calculate FOV reduction proportional to damage amount
    - Call TriggerFOVKick with negative amount (e.g., -5f, 0.2s duration)
    - Implement priority override: DamageRecoil kills SprintKick tween
    - _Requirements: 2.3, 2.7, 9.1_

- [x] 5. Checkpoint - Core Systems Functional
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Post-Processing Modulation
  - [x] 6.1 Cache Volume override references
    - In Awake, get Vignette override from _urpVolume.profile
    - In Awake, get ChromaticAberration override from _urpVolume.profile
    - In Awake, get DepthOfField override from _urpVolume.profile
    - Add null-checks for each override, log warnings if missing
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6, 9.5_
  
  - [x] 6.2 Implement UpdateHealthPostProcessing in SlowTick
    - Add private `UpdateHealthPostProcessing()` method called from SlowTick
    - Get current health from HectonSurvivalSystem (null-check, early return if null)
    - Calculate healthNormalized (0-1 range)
    - If healthNormalized < 0.3f: calculate vignette intensity using Mathf.Lerp(0f, 1f, (0.3f - healthNormalized) / 0.3f)
    - If healthNormalized >= 0.3f: set vignette intensity to 0f
    - Use MaterialPropertyBlock pattern: GetPropertyBlock → SetFloat(_VignetteIntensity) → SetPropertyBlock
    - _Requirements: 3.1, 3.2, 3.5, 3.6, 3.7, 12.4_
  
  - [x] 6.3 Implement UpdateO2PostProcessing in SlowTick
    - Add private `UpdateO2PostProcessing()` method called from SlowTick
    - Get current O2 from HectonSurvivalSystem (null-check, early return if null)
    - Calculate o2Normalized (0-1 range)
    - If o2Normalized < 0.2f: calculate chromatic aberration intensity using Mathf.Lerp(0f, 0.8f, (0.2f - o2Normalized) / 0.2f)
    - If o2Normalized >= 0.2f: set chromatic aberration intensity to 0f
    - Use MaterialPropertyBlock pattern: GetPropertyBlock → SetFloat(_ChromaticIntensity) → SetPropertyBlock
    - _Requirements: 3.3, 3.4, 3.5, 3.6, 3.7, 12.4_
  
  - [x] 6.4 Integrate HectonSurvivalSystem events
    - Subscribe to OnIntegrityChanged and OnOxygenCritical in OnEnable
    - Unsubscribe in OnDisable
    - Cache HectonSurvivalSystem reference in Awake (FindObjectOfType with null-check warning)
    - _Requirements: 9.1, 12.4_

- [x] 7. Biome Profile System
  - [x] 7.1 Create BiomeProfile ScriptableObject
    - Create BiomeProfile.cs in Assets/_Project/Scripts/VFX/
    - Add [CreateAssetMenu] attribute with path "HECTON-8/VFX/Biome Profile"
    - Define color grading fields: ColorFilter (Color), Temperature (Range -100 to 100), Tint (Range -100 to 100)
    - Define AO fields: AOIntensity (Range 0-4), AORadius (Range 0-2)
    - Define bloom fields: BloomIntensity (Range 0-1), BloomThreshold (Range 0-10)
    - Define fog fields: FogColor (Color), FogDensity (Range 0-1)
    - Add [Header] and [Tooltip] attributes on all fields
    - _Requirements: 4.2, 10.1_
  
  - [x] 7.2 Implement TransitionToBiome public API
    - Add `TransitionToBiome(BiomeProfile biome, float blendDuration)` method
    - Validate biome parameter (null-check, use default fallback if null)
    - Kill existing _biomeTween if active
    - Store target biome in _targetBiome field
    - Use DOTween to blend from _currentBiome to _targetBiome over blendDuration
    - Apply blended values to _urpVolume overrides each frame during blend
    - On complete: set _currentBiome = _targetBiome
    - _Requirements: 4.1, 4.3, 4.4, 4.5, 4.6_
  
  - [x] 7.3 Create default BiomeProfile assets
    - Create 8 BiomeProfile assets in Assets/_Project/Data/VFX/BiomeProfiles/
    - Name convention: BiomeProfile_Default, BiomeProfile_DeepSea, BiomeProfile_Cave, BiomeProfile_Surface, BiomeProfile_Volcanic, BiomeProfile_Ice, BiomeProfile_Bioluminescent, BiomeProfile_Toxic
    - Configure each with distinct color grading, AO, bloom, and fog parameters
    - Mark BiomeProfile_Default as fallback in code
    - _Requirements: 4.6, 10.2, 10.4_
  
  - [x] 7.4 Implement biome profile validation
    - Add OnValidate method to BiomeProfile (guarded with #if UNITY_EDITOR)
    - Clamp all Range fields to valid bounds
    - Log warnings for invalid configurations
    - _Requirements: 10.3_

- [x] 8. Interaction Focus (Depth of Field)
  - [x] 8.1 Implement UpdateInteractionFocus in Tick
    - Add private `UpdateInteractionFocus(float dt)` method called from Tick
    - Check if _focusTarget != null
    - If target exists: calculate focus distance using Vector3.Distance(_cameraTransform.position, _focusTarget.transform.position)
    - Apply focus distance to DepthOfField override
    - Enable DepthOfField override
    - If target is null: disable DepthOfField override
    - Bypass if PerformanceMode is Low (check QualitySettings.GetQualityLevel() == 0)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.6, 5.7_
  
  - [x] 8.2 Integrate InteractionEvents
    - Subscribe to InteractionEvents.OnHoverChanged in OnEnable
    - Unsubscribe in OnDisable
    - On hover start: store IInteractable reference in _focusTarget field
    - On hover end: clear _focusTarget field (set to null)
    - Use Gaussian DoF mode for performance (set in Awake)
    - _Requirements: 5.1, 5.2, 5.5, 9.3_

- [x] 9. Settings Integration and ISaveable
  - [x] 9.1 Define settings fields
    - Add fields: float _shakeIntensityMultiplier (default 1.0f), float _fovIntensityMultiplier (default 1.0f)
    - Add fields: bool _motionBlurEnabled (default false), bool _chromaticAberrationEnabled (default true), bool _depthOfFieldEnabled (default true)
    - Add [SerializeField] attributes for Inspector visibility
    - Add [Range] attributes for multipliers (0.0-2.0)
    - _Requirements: 7.1, 7.2, 7.3_
  
  - [x] 9.2 Implement ISaveable interface
    - Add SavePriority property: return 75 (Player tier)
    - Add LoadPriority property: return 75 (Player tier)
    - Implement PopulateSaveData: write all settings fields to SaveData with "save_camerajuice_" key prefix
    - Implement LoadFromSaveData: read all settings fields from SaveData with null-checks, use defaults if keys missing
    - _Requirements: 7.5, 7.6, 9.8_
  
  - [x] 9.3 Implement settings validation
    - Add public properties for each setting with validation in setters
    - Clamp _shakeIntensityMultiplier to [0.0, 2.0]
    - Clamp _fovIntensityMultiplier to [0.0, 2.0]
    - Apply settings immediately without scene reload
    - _Requirements: 7.4, 7.7_
  
  - [x] 9.4 Implement performance mode degradation
    - In Awake, check QualitySettings.GetQualityLevel()
    - If Low tier (level 0): disable _depthOfFieldEnabled, disable _motionBlurEnabled
    - Apply quality settings to Volume overrides
    - _Requirements: 6.7, 10.5_

- [x] 10. Checkpoint - Integration Complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Debug Visualization (Editor Only)
  - [x] 11.1 Implement OnDrawGizmos for shake visualization
    - Add OnDrawGizmos method guarded with #if UNITY_EDITOR
    - Draw red line from camera position to camera position + _shakeOffset
    - Draw red wire sphere at shake offset endpoint (radius 0.05f)
    - _Requirements: 11.1_
  
  - [x] 11.2 Implement OnDrawGizmos for FOV visualization
    - In OnDrawGizmos, draw yellow FOV cone representation
    - Visualize current FOV offset and target FOV
    - _Requirements: 11.2_
  
  - [x] 11.3 Implement OnDrawGizmos for DoF focus visualization
    - In OnDrawGizmos, check if _focusTarget != null
    - Draw cyan line from camera to focus target
    - Draw cyan wire sphere at focus target position (radius 0.2f)
    - _Requirements: 11.3_
  
  - [x] 11.4 Add read-only monitoring properties
    - Add public int ActiveShakeCount => _activeShakes.Count
    - Add public float CurrentFOVOffset => _currentFOVOffset
    - Add public FOVState CurrentFOVState => _fovState
    - Add public bool IsPostProcessingEnabled => _postProcessingEnabled
    - _Requirements: 11.4_
  
  - [x] 11.5 Implement frame time warning logs
    - Add frame time tracking in Tick (measure execution time)
    - Add throttled warning log if frame time exceeds 1.0ms budget
    - Guard with #if UNITY_EDITOR || DEVELOPMENT_BUILD
    - Throttle to 1 log per 5 seconds using static _nextLogTime pattern
    - _Requirements: 11.5, 11.6_

- [x] 12. Default Shake Profiles
  - Create ShakeProfile assets in Assets/_Project/Data/VFX/ShakeProfiles/
  - Create ShakeProfile_ImpactLight: MaxDisplacement 0.05f, Frequency 20f, Duration 0.2s
  - Create ShakeProfile_ImpactMedium: MaxDisplacement 0.1f, Frequency 15f, Duration 0.4s
  - Create ShakeProfile_ImpactHeavy: MaxDisplacement 0.2f, Frequency 12f, Duration 0.6s
  - Create ShakeProfile_Explosion: MaxDisplacement 0.3f, Frequency 10f, Duration 0.8s
  - Create ShakeProfile_Damage: MaxDisplacement 0.15f, Frequency 25f, Duration 0.3s
  - Configure FalloffCurve for each (EaseInOut for smooth decay)
  - Configure AxisWeights for each (e.g., Vector3(1, 1, 0.5) for reduced Z shake)
  - _Requirements: 1.3_

- [x] 13. Error Handling and Graceful Degradation
  - [x] 13.1 Add initialization error handling
    - In Awake, if MainCamera is null: log error, set enabled = false, return
    - In Awake, if URPVolume is null: log error, set _postProcessingEnabled = false, continue
    - In OnEnable, if GameTickManager.Instance is null: log error, return without registration
    - _Requirements: 12.1, 12.2, 12.3_
  
  - [x] 13.2 Add runtime error handling
    - Wrap UpdateShake in try-catch: on exception, log error, set _shakeEnabled = false
    - Wrap UpdateFOV in try-catch: on exception, log error, set _fovEnabled = false
    - Wrap UpdateHealthPostProcessing in try-catch: on exception, log error, set _healthO2EffectsEnabled = false
    - Wrap UpdateO2PostProcessing in try-catch: on exception, log error, set _healthO2EffectsEnabled = false
    - _Requirements: 12.6_
  
  - [x] 13.3 Add ShakeProfile validation
    - In TriggerShake, validate profile.MaxDisplacement is in [0, 1] range
    - If out of range: log warning, clamp to valid range
    - Validate profile.Duration > 0, if not: log warning, use default 0.5s
    - _Requirements: 12.7_

- [x] 14. DOTween Cleanup
  - In OnDisable, kill _fovTween if not null
  - In OnDisable, kill _biomeTween if not null
  - In OnDestroy, call DOTween.Kill(this) to cleanup all tweens targeting this instance
  - _Requirements: 2.4, 4.3_

- [x] 15. Final Integration and Wiring
  - Add CameraJuiceSystem component to MainCamera GameObject in 02_HECTON_WORLD scene
  - Verify URPVolume component exists on MainCamera or parent
  - Test shake triggering from impact events (manual test in Play Mode)
  - Test FOV kick from sprint (manual test in Play Mode)
  - Test health vignette at < 30% health (manual test in Play Mode)
  - Test O2 chromatic aberration at < 20% O2 (manual test in Play Mode)
  - Test biome profile transitions (manual test in Play Mode)
  - Test interaction focus DoF (manual test in Play Mode)
  - Verify settings persistence across save/load cycles
  - _Requirements: All_

- [x] 16. Final Checkpoint - System Complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks reference specific requirements for traceability
- Checkpoints ensure incremental validation at logical breaks
- Testing tasks require Unity Play Mode and are deferred to manual verification
- Zero-GC patterns enforced: DOTween, MaterialPropertyBlock, pre-allocated collections, cached references
- Frame budget: 1.0ms total verified via profiling in Play Mode
- Settings persistence via SaveManager ISaveable interface (LoadPriority 75)
- Graceful degradation on missing dependencies (null-checks, warnings, partial functionality)
- Debug visualization guarded with #if UNITY_EDITOR
- Performance mode degradation for Low quality tier (disable DoF, motion blur)

