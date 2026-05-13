# INTERNAL_FLOOD_RENDERER Status

Prompt: `INTERNAL_FLOOD_RENDERER`
Role: `HABITAT_ARCHITECT`
Domain: `ECHELON 6: HABITAT & VEHICLES`
Status: PENDING VERIFICATION

## Mandates Read

- `PHYS_Fluid_Incursion_Interior.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Checklist

- [ ] 1. SINGLETON ERADICATION: Purge `FloodVfxManager.Instance`.
- [ ] 2. SIGNAL MIGRATION: Read `RoomWaterLevels` from `GlobalRegistry.HabitatGraph`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Habitat.VFX` -> Contracts.
- [ ] 4. DEAD CODE HUNT: Eradicate `Instantiate(WaterMeshPrefab)` from base modules.
- [ ] 5. LOCAL HEIGHT CALCULATION: On `FastTick`, get Player AUP, query RoomID, read fill.
- [ ] 6. CAMERA SPLIT: Compare camera AUP Y against room water surface Y.
- [ ] 7. SHADER UPLOAD: Push `_InternalWaterlineY` and `_InternalWaterColor` globals.
- [ ] 8. POST PROCESS: Compute split in `HectonVisorUberPostFeature`.
- [ ] 9. UNDERWATER DISTORTION: Tint/refraction below split.
- [ ] 10. WATER DROPLETS: 2s droplet distortion on below-to-above transition.
- [ ] 11. O2 BUBBLES: Emit `DebrisSpawnSignal(ScreenBubbles)` while submerged on exhale.
- [ ] 12. AUP SHIFT SAFETY: Shift `_InternalWaterlineY` with `AupShiftSignal`.
- [ ] 13. MATH LOD: Low tier disables refraction, tint only.
- [ ] 14. ZERO-GC: Camera split math allocates 0 bytes.
- [ ] 15. BLACKBOX DUMP: Push `CurrentWaterlineY` to telemetry.
- [ ] 16. EVENT BUS: Emit `AcousticPingSignal(WaterSplash)` on crossing threshold.
- [ ] 17. CROSS-DOMAIN AUDIT: Gas Dynamics treats submerged room portion as 0% O2.
- [ ] 18. TRANSITION LERP: Smooth waterline through partially flooded bulkhead door.
- [ ] 19. OMEGA COMPILE CHECK: Verify shader instructions do not break SRP batcher.

## Iteration Log

- Init: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; mandates selected; no prior status/rationale files found.
