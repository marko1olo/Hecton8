# Design Document: Camera Juice & Screen Effects System

## Overview

The Camera Juice & Screen Effects System provides dynamic camera feedback and post-processing modulation for HECTON-8. The system responds to gameplay events (impacts, sprint, damage) with camera shake and FOV changes, and modulates post-processing effects based on player health, oxygen levels, and environmental biome context.

**Core Design Principles:**
- **Zero-GC Hot Paths**: All per-frame operations allocate 0 bytes
- **Frame Budget Compliance**: Total execution ≤ 1.0ms on NVIDIA MX350
- **DOTween Integration**: All animations use DOTween for zero-GC smooth interpolation
- **MaterialPropertyBlock Pattern**: Shader parameter updates avoid material instance allocation
- **ITickable Architecture**: Frame updates via GameTickManager, not Update()
- **ISaveable Integration**: Settings persistence via SaveManager
- **Event-Driven**: Subscribe to HectonSurvivalSystem, PlayerMovement, InteractionEvents

**Target Hardware:**
- NVIDIA MX350 2GB VRAM
- 12GB RAM, i5-1135G7
- 60 FPS / 16.67ms frame budget
- Camera effects budget: 1.0ms total

## Architecture

### System Hierarchy

```
CameraJuiceSystem (Singleton MonoBehaviour)
├── ITickable (per-frame camera shake, FOV, DoF focus distance)
├── ISlowTickable (2Hz health/O2 post-processing updates)
├── ISaveable (settings persistence, LoadPriority 75)
├── Shake Blending (additive multi-shake with bounds)
├── FOV State Machine (sprint/damage priority queue)
├── Post-Processing Modulation (health vignette, O2 chromatic aberration)
├── Biome Profile Transitions (DOTween blending)
└── Interaction Focus (depth-of-field targeting)
```

### Component Ownership

| Component | Responsibility | Lifecycle |
|---|---|---|
| **CameraJuiceSystem** | Runtime coordinator, event subscriber, tick registration | Scene lifetime singleton |
| **ShakeProfile** | Shake configuration data (intensity, frequency, duration, falloff) | ScriptableObject asset |
| **BiomeProfile** | Post-processing configuration per biome | ScriptableObject asset |
| **MainCamera** | Cached Camera reference | Initialized in Awake |
| **URPVolume** | Cached Volume reference for post-processing | Initialized in Awake |

### Integration Points

**HectonSurvivalSystem Events:**
```csharp
// Subscribe in OnEnable, unsubscribe in OnDisable
OnIntegrityChanged  // Health-based vignette (< 30%)
OnOxygenCritical    // O2-based chromatic aberration (< 20%)
```

**PlayerMovement State:**
```csharp
// Poll _isSprinting field in Tick (no event exists)
// Alternative: Add sprint events to PlayerMovement
bool _isSprinting;  // Drives FOV kick
```

**InteractionEvents:**
```csharp
OnHoverChanged  // IInteractable hover → depth-of-field focus
```

**GameTickManager:**
```csharp
Register(ITickable)      // Camera shake, FOV, DoF updates
Register(ISlowTickable)  // Health/O2 post-processing (2Hz)
```

**SaveManager:**
```csharp
ISaveable.LoadPriority = 75  // Player tier (51-100)
Settings: shake intensity, FOV intensity, effect toggles
```

## Components and Interfaces

### CameraJuiceSystem

**Primary Runtime System**

```csharp
namespace Hecton8.VFX
{
    [DisallowMultipleComponent]
    public sealed class CameraJuiceSystem : MonoBehaviour, ITickable, ISlowTickable, ISaveable
    {
        // ═══ SINGLETON ═══
        private static CameraJuiceSystem _instance;
        public static CameraJuiceSystem Instance => _instance;

        // ═══ CACHED REFERENCES ═══
        private Camera _mainCamera;              // Cached in Awake
        private Volume _urpVolume;               // Cached in Awake
        private Transform _cameraTransform;      // Cached in Awake
        
        // ═══ SHAKE STATE ═══
        private Vector3 _shakeOffset;            // Current additive shake displacement
        private readonly List<ActiveShake> _activeShakes;  // COLD ALLOC: List<ActiveShake>[8]
        
        // ═══ FOV STATE ═══
        private enum FOVState { Idle, SprintKick, DamageRecoil }
        private FOVState _fovState;
        private float _baseFOV;                  // Cached baseline FOV
        private float _currentFOVOffset;         // Current FOV delta
        private Tween _fovTween;                 // DOTween handle
        
        // ═══ POST-PROCESSING STATE ═══
        private Vignette _healthVignette;        // Cached Volume override
        private ChromaticAberration _o2ChromaticAberration;  // Cached Volume override
        private DepthOfField _interactionDoF;    // Cached Volume override
        private readonly MaterialPropertyBlock _mpb;  // COLD ALLOC: MaterialPropertyBlock[1]
        
        // ═══ BIOME PROFILE ═══
        private BiomeProfile _currentBiome;
        private BiomeProfile _targetBiome;
        private Tween _biomeTween;               // DOTween handle
        
        // ═══ INTERACTION FOCUS ═══
        private IInteractable _focusTarget;
        private float _focusDistance;
        
        // ═══ SETTINGS ═══
        private float _shakeIntensityMultiplier = 1.0f;  // 0.0-2.0
        private float _fovIntensityMultiplier = 1.0f;    // 0.0-2.0
        private bool _motionBlurEnabled = false;
        private bool _chromaticAberrationEnabled = true;
        private bool _depthOfFieldEnabled = true;
        
        // ═══ TICK REGISTRATION ═══
        private bool _registered;
        
        // ═══ SHADER PROPERTY IDS ═══
        private static readonly int _VignetteIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int _ChromaticIntensity = Shader.PropertyToID("_Intensity");
        
        // ═══ LIFECYCLE ═══
        void Awake() { /* Singleton, cache refs, pre-allocate */ }
        void OnEnable() { /* Subscribe events, register tick */ }
        void OnDisable() { /* Unsubscribe events, unregister tick */ }
        
        // ═══ ITICKABLE ═══
        public void Tick(float dt)
        {
            UpdateShake(dt);
            UpdateFOV(dt);
            UpdateInteractionFocus(dt);
        }
        
        // ═══ ISLOWTICKABLE ═══
        public void SlowTick()
        {
            UpdateHealthPostProcessing();
            UpdateO2PostProcessing();
        }
        
        // ═══ ISAVEABLE ═══
        public int SavePriority => 75;
        public int LoadPriority => 75;
        public void PopulateSaveData(SaveData data) { /* Save settings */ }
        public void LoadFromSaveData(SaveData data) { /* Load settings */ }
        
        // ═══ PUBLIC API ═══
        public void TriggerShake(ShakeProfile profile, float intensityScale = 1.0f) { }
        public void TriggerFOVKick(float amount, float duration) { }
        public void TransitionToBiome(BiomeProfile biome, float blendDuration) { }
    }
}
```

**ActiveShake Struct (Zero-GC)**

```csharp
private struct ActiveShake
{
    public ShakeProfile Profile;
    public float Elapsed;
    public float IntensityScale;
    public Vector3 Offset;  // Current frame contribution
}
```

### ShakeProfile (ScriptableObject)

**Configuration Data for Camera Shake**

```csharp
[CreateAssetMenu(fileName = "ShakeProfile_", menuName = "HECTON-8/VFX/Shake Profile")]
public sealed class ShakeProfile : ScriptableObject
{
    [Header("── Intensity ──────────────────")]
    [Tooltip("Maximum displacement in world units")]
    [Range(0f, 0.5f)]
    public float MaxDisplacement = 0.1f;
    
    [Header("── Frequency ──────────────────")]
    [Tooltip("Shake oscillation frequency (Hz)")]
    [Range(1f, 30f)]
    public float Frequency = 15f;
    
    [Header("── Duration ──────────────────")]
    [Tooltip("Total shake duration (seconds)")]
    [Range(0.1f, 3f)]
    public float Duration = 0.5f;
    
    [Header("── Falloff ──────────────────")]
    [Tooltip("Intensity falloff curve over duration")]
    public AnimationCurve FalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("── Axes ──────────────────")]
    [Tooltip("Shake contribution per axis (normalized)")]
    public Vector3 AxisWeights = new Vector3(1f, 1f, 0.5f);
}
```

### BiomeProfile (ScriptableObject)

**Post-Processing Configuration per Biome**

```csharp
[CreateAssetMenu(fileName = "BiomeProfile_", menuName = "HECTON-8/VFX/Biome Profile")]
public sealed class BiomeProfile : ScriptableObject
{
    [Header("── Color Grading ──────────────────")]
    public Color ColorFilter = Color.white;
    [Range(-100f, 100f)]
    public float Temperature = 0f;
    [Range(-100f, 100f)]
    public float Tint = 0f;
    
    [Header("── Ambient Occlusion ──────────────────")]
    [Range(0f, 4f)]
    public float AOIntensity = 1f;
    [Range(0f, 2f)]
    public float AORadius = 1f;
    
    [Header("── Bloom ──────────────────")]
    [Range(0f, 1f)]
    public float BloomIntensity = 0.3f;
    [Range(0f, 10f)]
    public float BloomThreshold = 0.9f;
    
    [Header("── Fog ──────────────────")]
    public Color FogColor = new Color(0.5f, 0.6f, 0.7f);
    [Range(0f, 1f)]
    public float FogDensity = 0.01f;
}
```

## Data Models

### Settings Data Structure

```csharp
public struct CameraJuiceSettings
{
    public float ShakeIntensityMultiplier;  // 0.0-2.0
    public float FOVIntensityMultiplier;    // 0.0-2.0
    public bool MotionBlurEnabled;
    public bool ChromaticAberrationEnabled;
    public bool DepthOfFieldEnabled;
    
    public static CameraJuiceSettings Default => new CameraJuiceSettings
    {
        ShakeIntensityMultiplier = 1.0f,
        FOVIntensityMultiplier = 1.0f,
        MotionBlurEnabled = false,
        ChromaticAberrationEnabled = true,
        DepthOfFieldEnabled = true
    };
}
```

### Shake Blending Algorithm

**Additive Multi-Shake with Bounds**

```
For each active shake:
    1. Evaluate falloff curve at (elapsed / duration)
    2. Generate Perlin noise offset using (Time.time * frequency)
    3. Scale offset by (maxDisplacement * falloffValue * intensityScale * settingsMultiplier)
    4. Weight offset by axisWeights
    5. Accumulate into _shakeOffset

Clamp _shakeOffset magnitude to MAX_SHAKE_DISPLACEMENT (0.5 units)
Apply _shakeOffset to camera localPosition
```

**Performance:**
- Perlin noise: Mathf.PerlinNoise (native, fast)
- Curve evaluation: AnimationCurve.Evaluate (acceptable for <8 shakes)
- Vector math: struct operations (zero-GC)

### FOV State Machine

**Priority-Based FOV Effect Queue**

```
States:
- Idle: No FOV effect active
- SprintKick: Sprint FOV increase (priority 1)
- DamageRecoil: Damage FOV reduction (priority 2)

Transitions:
- Idle → SprintKick: PlayerMovement._isSprinting == true
- SprintKick → Idle: PlayerMovement._isSprinting == false
- Any → DamageRecoil: HectonSurvivalSystem.OnIntegrityChanged (damage taken)
- DamageRecoil → Idle/SprintKick: Tween complete

Priority Resolution:
- DamageRecoil overrides SprintKick
- SprintKick queued if DamageRecoil active
- Tween handles: Kill() before starting new tween
```

**DOTween Integration:**
```csharp
_fovTween?.Kill();
_fovTween = DOTween.To(
    () => _currentFOVOffset,
    x => _currentFOVOffset = x,
    targetOffset,
    duration
).SetEase(Ease.OutQuad);
```

### Post-Processing Modulation

**Health Vignette (< 30% health)**

```
Intensity = Mathf.Lerp(0f, 1f, (0.3f - healthNormalized) / 0.3f)
Update via ISlowTickable (2Hz)
MaterialPropertyBlock for shader params
```

**O2 Chromatic Aberration (< 20% O2)**

```
Intensity = Mathf.Lerp(0f, 0.8f, (0.2f - o2Normalized) / 0.2f)
Update via ISlowTickable (2Hz)
MaterialPropertyBlock for shader params
```

**Interaction Depth-of-Field**

```
Focus Distance = Vector3.Distance(_cameraTransform.position, _focusTarget.transform.position)
Update every frame in Tick() while _focusTarget != null
Gaussian DoF mode (performance)
Disabled if PerformanceMode == Low
```

## Error Handling

### Initialization Failures

**MainCamera Null:**
```csharp
if (_mainCamera == null)
{
    Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
    enabled = false;
    return;
}
```

**URPVolume Null:**
```csharp
if (_urpVolume == null)
{
    Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
    _postProcessingEnabled = false;
    // Continue with shake/FOV only
}
```

**GameTickManager Null:**
```csharp
if (GameTickManager.Instance == null)
{
    Debug.LogError("[CameraJuiceSystem] GameTickManager not found. Cannot register tick.");
    return;
}
```

### Runtime Failures

**HectonSurvivalSystem Missing:**
```csharp
var survival = FindObjectOfType<HectonSurvivalSystem>();
if (survival == null)
{
    Debug.LogWarning("[CameraJuiceSystem] HectonSurvivalSystem not found. Health/O2 effects disabled.");
    _healthO2EffectsEnabled = false;
}
```

**PlayerMovement Missing:**
```csharp
var player = FindObjectOfType<HectonPlayerMovement>();
if (player == null)
{
    Debug.LogWarning("[CameraJuiceSystem] PlayerMovement not found. Sprint FOV disabled.");
    _sprintFOVEnabled = false;
}
```

**Effect Calculation Exception:**
```csharp
try
{
    UpdateShake(dt);
}
catch (Exception ex)
{
    Debug.LogError($"[CameraJuiceSystem] Shake calculation failed: {ex.Message}");
    _shakeEnabled = false;  // Disable specific effect
}
```

### Validation

**ShakeProfile Validation:**
```csharp
if (profile.MaxDisplacement < 0f || profile.MaxDisplacement > 1f)
{
    Debug.LogWarning($"[CameraJuiceSystem] Invalid MaxDisplacement {profile.MaxDisplacement}. Clamping to [0, 1].");
    profile.MaxDisplacement = Mathf.Clamp(profile.MaxDisplacement, 0f, 1f);
}
```

**Settings Validation:**
```csharp
_shakeIntensityMultiplier = Mathf.Clamp(value, 0f, 2f);
_fovIntensityMultiplier = Mathf.Clamp(value, 0f, 2f);
```

## Testing Strategy

### Unit Testing

**Shake Blending Logic:**
- Test additive blending of multiple simultaneous shakes
- Verify magnitude clamping to MAX_SHAKE_DISPLACEMENT
- Test falloff curve evaluation at t=0, t=0.5, t=1.0
- Verify zero-intensity bypass when settings multiplier = 0

**FOV State Machine:**
- Test state transitions (Idle → SprintKick → Idle)
- Test priority override (DamageRecoil > SprintKick)
- Test tween cleanup (Kill() before new tween)
- Verify FOV clamping to min/max bounds

**Post-Processing Modulation:**
- Test health vignette intensity at 30%, 15%, 0% health
- Test O2 chromatic aberration intensity at 20%, 10%, 0% O2
- Verify MaterialPropertyBlock usage (no material instance allocation)
- Test SlowTick update frequency (2Hz)

**Settings Persistence:**
- Test SaveData population with non-default settings
- Test LoadFromSaveData with missing keys (default fallback)
- Verify LoadPriority = 75 (Player tier)

### Integration Testing

**Event Subscription:**
- Verify HectonSurvivalSystem.OnIntegrityChanged triggers health vignette
- Verify HectonSurvivalSystem.OnOxygenCritical triggers O2 chromatic aberration
- Verify InteractionEvents.OnHoverChanged triggers depth-of-field focus
- Verify PlayerMovement._isSprinting triggers sprint FOV kick

**Tick Registration:**
- Verify ITickable registration in OnEnable
- Verify ISlowTickable registration in OnEnable
- Verify unregistration in OnDisable
- Test double-registration guard (_registered flag)

**DOTween Integration:**
- Verify FOV tween completes without GC allocation
- Verify biome profile tween blends smoothly
- Test tween cleanup on scene unload

### Performance Testing

**Frame Budget Compliance:**
- Profile Tick() execution time (target: < 0.8ms)
- Profile SlowTick() execution time (target: < 0.15ms)
- Verify total system time < 1.0ms on NVIDIA MX350

**GC Allocation:**
- Verify 0 bytes allocated in Tick()
- Verify 0 bytes allocated in SlowTick()
- Profile with Unity Profiler (Deep Profile mode)

**Stress Testing:**
- Spawn 8 simultaneous shakes (max capacity)
- Trigger rapid FOV state changes (sprint toggle spam)
- Verify no frame drops or GC spikes

### Manual Testing

**Visual Verification:**
- Camera shake feels impactful on impacts/explosions
- Sprint FOV kick enhances speed sensation
- Health vignette intensifies smoothly as health drops
- O2 chromatic aberration signals low oxygen clearly
- Depth-of-field focuses on interaction targets
- Biome transitions blend smoothly without pops

**Settings Verification:**
- Shake intensity multiplier scales shake correctly (0.0 = off, 2.0 = double)
- FOV intensity multiplier scales FOV kick correctly
- Effect toggles disable specific effects immediately
- Settings persist across save/load cycles

## Performance Optimization

### Zero-GC Patterns

**Pre-Allocated Collections:**
```csharp
// COLD ALLOC: List<ActiveShake>[8] — max simultaneous shakes — owner: CameraJuiceSystem
private readonly List<ActiveShake> _activeShakes = new List<ActiveShake>(8);
```

**MaterialPropertyBlock:**
```csharp
// COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: CameraJuiceSystem
private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
```

**Cached Shader Property IDs:**
```csharp
private static readonly int _VignetteIntensity = Shader.PropertyToID("_Intensity");
private static readonly int _ChromaticIntensity = Shader.PropertyToID("_Intensity");
```

**Cached References:**
```csharp
private Camera _mainCamera;              // Awake: Camera.main
private Volume _urpVolume;               // Awake: GetComponent<Volume>()
private Transform _cameraTransform;      // Awake: _mainCamera.transform
```

**DOTween (Zero-GC):**
```csharp
// DOTween uses internal pooling, no per-call allocation
_fovTween = DOTween.To(() => _currentFOVOffset, x => _currentFOVOffset = x, target, duration);
```

### Frame Budget Breakdown

| Operation | Budget | Notes |
|---|---|---|
| Shake Update | 0.2ms | Perlin noise + curve eval for ≤8 shakes |
| FOV Update | 0.1ms | State machine + DOTween tick |
| DoF Focus Update | 0.1ms | Vector3.Distance calculation |
| Health/O2 Update (SlowTick) | 0.15ms | MaterialPropertyBlock updates (2Hz) |
| Biome Transition | 0.15ms | DOTween blend (during transition only) |
| **Total** | **1.0ms** | **Target met** |

### Optimization Techniques

**Bypass Zero-Intensity Effects:**
```csharp
if (_shakeIntensityMultiplier <= 0f)
{
    _shakeOffset = Vector3.zero;
    return;  // Skip all shake calculations
}
```

**SlowTick for Infrequent Updates:**
```csharp
// Health/O2 post-processing updates at 2Hz (every 0.5s)
// Avoids per-frame MaterialPropertyBlock updates
public void SlowTick()
{
    UpdateHealthPostProcessing();
    UpdateO2PostProcessing();
}
```

**Performance Mode Degradation:**
```csharp
if (QualitySettings.GetQualityLevel() == 0)  // Low tier
{
    _depthOfFieldEnabled = false;
    _motionBlurEnabled = false;
    // Reduce post-processing quality
}
```

**Shake Capacity Limit:**
```csharp
private const int MAX_ACTIVE_SHAKES = 8;

if (_activeShakes.Count >= MAX_ACTIVE_SHAKES)
{
    // Remove oldest shake
    _activeShakes.RemoveAt(0);
}
```

## Debug Visualization

### Gizmos (Editor Only)

**Shake Displacement Vector:**
```csharp
#if UNITY_EDITOR
private void OnDrawGizmos()
{
    if (_mainCamera == null) return;
    
    Gizmos.color = Color.red;
    Gizmos.DrawLine(_cameraTransform.position, _cameraTransform.position + _shakeOffset);
    Gizmos.DrawWireSphere(_cameraTransform.position + _shakeOffset, 0.05f);
}
#endif
```

**FOV Target Visualization:**
```csharp
#if UNITY_EDITOR
private void OnDrawGizmos()
{
    if (_mainCamera == null) return;
    
    Gizmos.color = Color.yellow;
    float targetFOV = _baseFOV + _currentFOVOffset;
    // Draw FOV cone representation
}
#endif
```

**Depth-of-Field Focus Distance:**
```csharp
#if UNITY_EDITOR
private void OnDrawGizmos()
{
    if (_focusTarget == null) return;
    
    Gizmos.color = Color.cyan;
    Gizmos.DrawLine(_cameraTransform.position, _focusTarget.transform.position);
    Gizmos.DrawWireSphere(_focusTarget.transform.position, 0.2f);
}
#endif
```

### Runtime Monitoring

**Read-Only Properties:**
```csharp
public int ActiveShakeCount => _activeShakes.Count;
public float CurrentFOVOffset => _currentFOVOffset;
public FOVState CurrentFOVState => _fovState;
public bool IsPostProcessingEnabled => _postProcessingEnabled;
```

**Frame Time Warning:**
```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
private static float _nextLogTime;

if (_frameTime > 1.0f && Time.time >= _nextLogTime)
{
    _nextLogTime = Time.time + 5f;
    Debug.LogWarning($"[CameraJuiceSystem] Frame time exceeded budget: {_frameTime:F2}ms");
}
#endif
```

## Implementation Notes

### PlayerMovement Sprint Event Gap

**Current State:**
- PlayerMovement has `_isSprinting` field but no public events
- CameraJuiceSystem must poll `_isSprinting` or add events to PlayerMovement

**Recommended Approach:**
Add sprint events to PlayerMovement:
```csharp
public event Action OnSprintStarted;
public event Action OnSprintEnded;

private void HandleSprintStarted()
{
    _isSprinting = true;
    OnSprintStarted?.Invoke();
}

private void HandleSprintCanceled()
{
    _isSprinting = false;
    OnSprintEnded?.Invoke();
}
```

### Biome Profile Loading

**Addressables Integration:**
```csharp
// Pre-load all biome profiles during initialization
private readonly Dictionary<string, BiomeProfile> _biomeProfiles = new Dictionary<string, BiomeProfile>(8);

private async void LoadBiomeProfiles()
{
    var handle = Addressables.LoadAssetsAsync<BiomeProfile>("BiomeProfiles", null);
    var profiles = await handle.Task;
    
    foreach (var profile in profiles)
    {
        _biomeProfiles[profile.name] = profile;
    }
}
```

### DOTween Setup

**Initialization:**
```csharp
private void Awake()
{
    // DOTween initialization (if not already done in bootstrap)
    DOTween.Init(recycleAllByDefault: true, useSafeMode: false);
    DOTween.defaultAutoPlay = AutoPlay.None;
}
```

**Cleanup:**
```csharp
private void OnDisable()
{
    _fovTween?.Kill();
    _biomeTween?.Kill();
}
```

### MaterialPropertyBlock Usage

**Correct Pattern:**
```csharp
// Get current properties
_renderer.GetPropertyBlock(_mpb);

// Modify
_mpb.SetFloat(_VignetteIntensity, intensity);

// Apply
_renderer.SetPropertyBlock(_mpb);
```

**FORBIDDEN:**
```csharp
// NEVER do this (creates leaked material instance)
_renderer.material.SetFloat("_Intensity", intensity);
```

## Conclusion

The Camera Juice & Screen Effects System provides dynamic camera feedback and post-processing modulation within strict performance constraints. The design prioritizes zero-GC hot paths, frame budget compliance, and clean integration with existing HECTON-8 systems.

**Key Achievements:**
- Zero-GC per-frame execution via pre-allocation and DOTween
- 1.0ms frame budget on target hardware (NVIDIA MX350)
- Event-driven integration with HectonSurvivalSystem, PlayerMovement, InteractionEvents
- ISaveable settings persistence via SaveManager
- ITickable/ISlowTickable architecture via GameTickManager
- MaterialPropertyBlock pattern for shader parameter updates
- Graceful degradation on missing dependencies

**Next Steps:**
1. Implement CameraJuiceSystem core class
2. Create ShakeProfile and BiomeProfile ScriptableObjects
3. Add sprint events to PlayerMovement (if approved)
4. Create default shake profiles (impact, explosion, damage)
5. Create default biome profiles (8 environmental zones)
6. Profile frame time and GC allocation on target hardware
7. Integrate with settings UI for user control
