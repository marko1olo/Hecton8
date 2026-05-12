# ProceduralFloraCorals Status

Assignment: Procedural Flora & Corals / Living Biota
Domain: Echelon 3 Flora, Fauna & Biota
Task Count: 30
Status: PENDING VERIFICATION

Batch source: No CURRENT_BATCH.md found by `rg --files -g '*BATCH*.md'`; using the 30-task XML prompt as active assignment.

## Loop 1: Tasks 1-5

- [x] 1. Vertex-wave sway | DOD: GPU vertex fake in kelp master shaders, root-pinned uv.y parabola, multi-octave bounded sine wave in Forward/Shadow/Depth passes. Alternative rejected: per-blade physics joints. Estimate: shader ALU only; Unity shader import still PENDING VERIFICATION.
- [x] 2. Propwash interaction | DOD: `FloraInteractionManager` publishes exact `SubmarinePropwash`; shaders read it with existing submarine wash sphere and 10m cap. Alternative rejected: trigger colliders around thrusters. Estimate: shader ALU plus one existing global vector write; Unity runtime still PENDING VERIFICATION.
- [x] 3. Interactive turbulence | DOD: kelp shader reads existing player flora globals; no new KCC dependency. Alternative rejected: plant MonoBehaviour proximity checks. Estimate: shader ALU only; Unity runtime still PENDING VERIFICATION.
- [x] 4. Lunar pulse glow | DOD: coral/kelp shaders consume `_HectonCelestialBiolumMultiplier`, already published by Celestial engine as 2x on full moon bloom. Alternative rejected: biolum manager polling moon phase directly. Estimate: shader scalar multiply; Unity runtime still PENDING VERIFICATION.
- [x] 5. Sensory reaction | DOD: coral vertex retraction driven by flashlight cone globals and player proximity; fragment photophobia still handles emission. Alternative rejected: per-anemone animated GameObjects. Estimate: shader ALU only; Unity shader import still PENDING VERIFICATION.

## Loop 2: Tasks 6-10

- [ ] 6. Biome color masks | DOD pending.
- [ ] 7. GPU instancing dictator | DOD pending.
- [ ] 8. Dithered fade-in | DOD pending.
- [ ] 9. VRAM packing | DOD pending.
- [ ] 10. Sargassum drag scalars | DOD pending.

## Loop 3: Tasks 11-15

- [ ] 11. Flora decay | DOD pending.
- [ ] 12. Bioluminescent spores | DOD pending.
- [ ] 13. Deep Sea Bloom | DOD pending.
- [ ] 14. Coral growth masks | DOD pending.
- [ ] 15. Vertex-color AO | DOD pending.

## Loop 4: Tasks 16-20

- [ ] 16. Toxic flora hazard | DOD pending.
- [ ] 17. Flora soundscape | DOD pending.
- [ ] 18. Procedural kelp length | DOD pending.
- [ ] 19. Plant harvesting | DOD pending.
- [ ] 20. math.rcp wave normalization | DOD pending.

## Loop 5: Tasks 21-30

- [ ] 21. Positional hashes only | DOD pending.
- [ ] 22. Clean Cyrillic comments from FloraMaster shader | DOD pending.
- [ ] 23. Pad flora metadata structs to 16 bytes | DOD pending.
- [ ] 24. Shadow casting LODs | DOD pending.
- [ ] 25. Surface weather wind direction influence | DOD pending.
- [ ] 26. Glowing flora light proxies | DOD pending.
- [ ] 27. FrostTick distant cluster update-jobs | DOD pending.
- [ ] 28. Replace smoothstep in glow curves | DOD pending.
- [ ] 29. Flora destruction | DOD pending.
- [ ] 30. Generate .meta files | DOD pending.

## Verification Log

- 2026-05-11: Static shader scan found no `smoothstep`, `UnityEngine.Random`, or `Random.` in touched flora shaders.
- 2026-05-11: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` succeeded with 0 warnings / 0 errors.
- 2026-05-11: Full `dotnet build Assembly-CSharp.csproj` failed before project-wide verification due unrelated `Hecton8.Input` errors in `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs` (`IPlatformIntegration`, `HectonQualityTier`, `ScalabilityTierProfiles`). Marking full compile as BLOCKED BY DEPENDENCY.
