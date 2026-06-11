# Hecton8 Full Diagnostic Report
**Generated:** 2026-06-11 03:42:29 | **Mode:** EditMode
**Active Scene:** ``

## Top Findings
- **[Critical]** Bootstrap has not started — registry phase is 0 and this is not the bootstrap scene: `registryPhase=0, scene=`
- **[Critical]** GlobalRegistry phase is 0 and all service slots are null: `phase=0, all slots null`
- **[Critical]** No cameras found in the scene at all: `0 cameras`
- **[Error]** Registry slot 'Input' is null: `null`
- **[Error]** CelestialEngine not found in scene: `not found`
- **[Error]** AtmosphereManager not found in scene: `not found`
- **[Error]** Ocean_Crest GameObject not found in scene: `not found`
- **[Error]** MapMagicObject not found in scene: `not found`
- **[Error]** TickManager or Dispatcher slot is null — game loop may not run: `TickManager=True, Dispatcher=True`
- **[Error]** Registry slot 'Dispatcher' is null: `null`
- **[Error]** Bootstrapper found but registry phase is not Complete (2): `phase=0 (Uninitialized)`
- **[Error]** Registry slot 'TerrainProviderRuntime' is null: `null`
- **[Error]** Atmosphere/Celestial missing and bootstrap not complete — likely bootstrap-order issue: `phase=0`
- **[Error]** Registry slot 'TickManager' is null: `null`
- **[Error]** Registry slot 'AtmosphereRuntime' is null: `null`
- **[Error]** Registry slot 'OceanKinematics' is null: `null`
- **[Error]** Registry slot 'Player' is null: `null`
- **[Error]** Registry slot 'UI' is null: `null`
- **[Error]** Registry slot 'Save' is null: `null`
- **[Error]** Registry slot 'Scene' is null: `null`
- **[Error]** Registry slot 'Audio' is null: `null`
- **[Error]** Registry slot 'Physics' is null: `null`
- **[Error]** Registry slot 'CelestialEngineRuntime' is null: `null`
- **[Error]** Registry slot 'MapMagicRuntime' is null: `null`

## 23 Diagnostics Questions
1. **Did bootstrap run?** -> `Uninitialized` (Phase 0)
2. **Is GlobalRegistry ready?** -> `EMPTY`
3. **Registry Slots:** `Null=18, Missing=0, Filled=0`
   - Null slots: `Input, Physics, Audio, Scene, Save, UI, Player, OceanKinematics, AtmosphereRuntime, CelestialEngineRuntime, MapMagicRuntime, TerrainProviderRuntime, TickManager, Dispatcher, RenderDispatcher, ObjectPool, Environment, Weather`
4. **Is this direct 02_HECTON_WORLD start?** -> `DIRECT_WORLD_START_DETECTED`
5. **Is Ocean_Crest active?** -> `False` (hierarchy: False)
6. **Is OceanRenderer active and enabled?** -> Active: `False`, Enabled: `False`
7. **Is Crest4KinematicsAdapter active?** -> `False` (enabled: False)
8. **Is OceanKinematics registered?** -> `False`
9. **Is MapMagicObject active?** -> `False`
10. **Is MapMagicRuntimeBridge active?** -> `False`
11. **Is MapMagic graph assigned?** -> `False` ()
12. **Are there any terrain generated?** -> `0` active
13. **Is MapMagic registered?** -> `False`
14. **Is HectonAtmosphereManager active?** -> `False`
15. **Is HectonCelestialEngine active?** -> `False`
16. **Are atmosphere and celestial registered?** -> Atmo: `False`, Celestial: `False`
17. **Which URP pipeline asset is active?** -> `URP_Medium (PC_RPAsset)`
18. **Which URP Renderer is active?** -> `PC_Renderer`
19. **Are Hecton features enabled?** -> (See feature list below)
20. **Is there an active MainCamera?** -> `False`
21. **Are there Console errors?** -> `0` errors detected
22. **What Unity objects are destroyed but accessed?** -> (Check findings for MissingReference exceptions)
23. **Is Git tree dirty?** -> `True`

## URP Features
| Feature | Type | Active |
|---|---|---|
| ShapesRenderFeature | ShapesRenderFeature | `False` |
| DecalRendererFeature | DecalRendererFeature | `False` |
| ScreenSpaceShadows | ScreenSpaceShadows | `False` |
| HectonScooterVolumetricShaftsFeature | HectonScooterVolumetricShaftsFeature | `True` |
| HectonAbyssalSsdoFeature | HectonAbyssalSsdoFeature | `True` |
| HectonDeferredCausticsFeature | HectonDeferredCausticsFeature | `True` |
| HectonVisorFluidDistortionFeature | HectonVisorFluidDistortionFeature | `False` |
| SaveThumbnailCaptureFeature | SaveThumbnailCaptureFeature | `True` |
| HectonRetinaDistortionFeature | HectonRetinaDistortionFeature | `False` |
| HectonNoirDepthFogFeature | HectonNoirDepthFogFeature | `True` |
| HectonFluidAdvectionRenderFeature | HectonFluidAdvectionRenderFeature | `True` |
| HectonHalfResParticlesFeature | HectonHalfResParticlesFeature | `True` |
| HectonAtmosphereSootFeature | HectonAtmosphereSootFeature | `True` |
| WristPdaScreenProjectorFeature | WristPdaScreenProjectorFeature | `True` |
| HectonVisorUberPostFeature | HectonVisorUberPostFeature | `True` |
| HectonVisorTraumaFeature | DeferredDecalPass | `True` |
| HectonVRBrownoutFeature | HectonVRBrownoutFeature | `True` |
| HectonSinglePassOceanFeature | HectonSinglePassOceanFeature | `True` |
| HectonBilateralDrsUpscalerFeature | HectonBilateralDrsUpscalerFeature | `True` |
| HectonVolumetricParticulateFogFeature | HectonVolumetricParticulateFogFeature | `True` |
