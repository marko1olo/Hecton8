# Hecton8 Full Diagnostic Report
**Generated:** 2026-06-12 01:38:04 | **Mode:** PlayMode_Bootstrap_60s
**Active Scene:** `01_MAIN_MENU`

## Top Findings
- **[Error]** Registry slot 'Input' is null: `null`
- **[Error]** Registry slot 'UI' is null: `null`
- **[Error]** Registry slot 'AtmosphereRuntime' is null: `null`
- **[Error]** Registry slot 'CelestialEngineRuntime' is null: `null`
- **[Error]** Registry slot 'MapMagicRuntime' is null: `null`
- **[Error]** Registry slot 'TerrainProviderRuntime' is null: `null`
- **[Error]** MapMagicObject not found in scene: `not found`
- **[Error]** Ocean_Crest GameObject not found in scene: `not found`
- **[Error]** AtmosphereManager not found in scene: `not found`
- **[Error]** CelestialEngine not found in scene: `not found`

## 23 Diagnostics Questions
1. **Did bootstrap run?** -> `Ready` (Phase 2)
2. **Is GlobalRegistry ready?** -> `READY`
3. **Registry Slots:** `Null=7, Missing=0, Filled=11`
   - Null slots: `Input, UI, AtmosphereRuntime, CelestialEngineRuntime, MapMagicRuntime, TerrainProviderRuntime, Weather`
4. **Is this direct 02_HECTON_WORLD start?** -> `BOOTSTRAP_COMPLETE`
5. **Is Ocean_Crest active?** -> `False` (hierarchy: False)
6. **Is OceanRenderer active and enabled?** -> Active: `False`, Enabled: `False`
7. **Is Crest4KinematicsAdapter active?** -> `False` (enabled: False)
8. **Is OceanKinematics registered?** -> `True`
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
20. **Is there an active MainCamera?** -> `True`
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
