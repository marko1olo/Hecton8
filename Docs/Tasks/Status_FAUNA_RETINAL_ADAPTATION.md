# Status: FAUNA_RETINAL_ADAPTATION

Batch prompt: `FAUNA_RETINAL_ADAPTATION`  
Role: `AI_PROGRAMMER`  
Domain: Flora, Fauna & Biota / AI Perception  
Status policy: `PENDING VERIFICATION` until Unity Console / tests / profiler evidence exists.

## Task Checklist

- [ ] 1. SINGLETON ERADICATION: Purge `VisionManager.Instance`.
- [ ] 2. SIGNAL MIGRATION: Consume `SubmarineLightsChangedSignal`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.AI.Perception` -> Contracts.
- [ ] 4. DEAD CODE HUNT: Eradicate `Physics.Raycast` used for light detection.
- [ ] 5. S.O.A. RETINA STATE: Add `NativeArray<float> RetinalExposure` and `NativeArray<byte> BlindnessState` to Fauna data structures.
- [ ] 6. LIGHT SOURCE REGISTRY: Maintain a `NativeArray<LightSourceData>` for the 4 brightest lights.
- [ ] 7. DOT PRODUCT SIGHT: In Fauna `SlowTick` job, use distance squared then dot product.
- [ ] 8. EXPOSURE INTEGRATION: Integrate exposure when predators look into lights.
- [ ] 9. BLINDNESS TRIGGER: Set `BlindnessState = 1` over threshold.
- [ ] 10. FLINCH BEHAVIOR: Inject perpendicular flee impulse in `PredatorCognitionDomain`.
- [ ] 11. ENRAGE BEHAVIOR: Species hashes can enrage instead of fleeing.
- [ ] 12. RECOVERY DECAY: Decay exposure outside direct glare.
- [ ] 13. AUP SHIFT SAFETY: Positions survive origin shift frame.
- [ ] 14. MATH LOD: Low tier evaluates retinal exposure at 1Hz.
- [ ] 15. ZERO-GC: Dot products and state writes allocate 0 bytes.
- [ ] 16. BLACKBOX DUMP: Push `TotalBlindPredators` to telemetry.
- [ ] 17. EVENT BUS: Emit `FaunaStateChangedSignal(Blind)`.
- [ ] 18. CROSS-DOMAIN AUDIT: Brownouts kill light sources in registry.
- [ ] 19. OMEGA COMPILE CHECK: Verify normalize uses `math.rsqrt`.

## Iteration Log

### Loop 0 - Initialization

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex.
- Fresh status file created because no prior `Status_FAUNA_RETINAL_ADAPTATION.md` existed.
- Fresh rationale file will be maintained before marking tasks done.

