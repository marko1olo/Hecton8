Tests to add:
1. `Awake_RegistersWithGlobalRegistry()`
    - Verify that `GlobalRegistry.ActiveCelestialEngine` gets set when `HectonCelestialEngine` initializes in PlayMode.
    - Since `Awake` calls `GlobalRegistry.RegisterCelestialEngineRuntime` when `Application.isPlaying` via `OnEnable` -> `InitializeRuntimeAuthority`.
2. `DuplicateEngine_DisablesPresentation()`
    - If a second `HectonCelestialEngine` is spawned, it detects a duplicate and disables things or destroys itself.
3. `TryApplyRuntimeTimeOfDay01_InvalidTime_ReturnsFalse()`
    - Call with `float.NaN`.
4. `TryApplyRuntimeTimeOfDay01_ValidTimeWithoutAtmosphere_ReturnsFalse()`
    - If `_atmosphereManager` is null, returns false.
5. `SetDebugCelestialTimeScale_ClampsToMinimum1()`
    - Calls `SetDebugCelestialTimeScale(0.5f)` -> `DebugCelestialTimeScale` is 1f.

Let's check if `HectonCelestialEnginePlayModeTests` builds and passes. I will write a simple test file to see if we can do this without complex mocking, just by letting it attach to a `GameObject`.
