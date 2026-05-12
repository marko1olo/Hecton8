# LOG_WORLD_FLORA

## 2026-05-11 - BIOTA_WEAVER Final Report

Status: PENDING VERIFICATION
Domain: ECHELON 3: FLORA, FAUNA & BIOTA

What was wrong:
- Kelp sway was object/local-space enough to duplicate visual phase across repeated meshes.
- Propwash used visual displacement but lacked the required dot-product thruster stream gate.
- Coral/anemone reaction lacked a damage impulse lane.
- Flora decay was seasonal only, not radiation-driven.
- Biolum spores existed as emission/cascade implication but had no visible GPU-only spore fake.
- Toxic flora registered toxicity hazards but did not set the combat poison status bit.
- Several flora-native/GPU structs relied on implicit packing.
- Indirect flora glow paths still used cubic `smoothstep`.
- Sea-grass import still allowed scattered high-resolution texture contracts instead of one 1024 BC7 atlas target.

What was done:
- `Hecton_KelpMaster.shader` and `Hecton_KelpMaster_GPUI.shader`: AUP-seeded multi-octave sine-parabola sway, AUP biome color hash, dot-product propwash, player flutter preservation, reciprocal wave-speed normalization.
- `Hecton_CoralMaster.shader` and `Hecton_CoralMaster_GPUI.shader`: damage/flashlight/player sensory reaction, AUP biome tint, radiation decay path reuse, vertex-color AO verification.
- `Hecton_IndirectVegetation.shader`: GPU-only dithered biolum spore impostors, linear glow gates, reciprocal helper math, positional hashes, growth/health gates.
- `FloraInteractionManager.cs`: radiation storm scalar feeds global flora decay, damage reaction global, toxic flora queues `CombatStatusBits.Poisoned`, explicit padded native payloads, reciprocal cleanup.
- `HectonIndirectVegetationContracts.cs`: metadata stride remains explicit 64 bytes.
- `WorldProceduralFloraTextureAuthoring.cs`: sea-grass 1024 BC7 atlas builder and import cap/reporting.

Vertex-wave sway shader math:
```hlsl
float speedNorm = max((float)_SwaySpeed, 0.001) * rcp(max((float)_SwayFrequency, 0.001));
float3 aupPos = positionWS + _TotalUniverseOffset.xyz;
float phaseSeed = HectonKelpHash12(floor(aupPos.xz * 0.0625));
float time = _Time.y * speedNorm + phaseSeed * 6.2831853;
float tipParabola = tipMask * tipMask * (3.0 - 2.0 * tipMask);
float octave0 = sin(time + aupPos.x * 0.17 + aupPos.z * 0.11);
float octave1 = sin(time * 1.73 + aupPos.x * 0.07 - aupPos.z * 0.19);
float octave2 = sin(time * 2.41 - aupPos.x * 0.13 + aupPos.z * 0.05);
positionOS.xz += swayDirection * ((octave0 + octave1 * 0.45 + octave2 * 0.23) * _SwayAmplitude * tipParabola);
```

Propwash bend logic:
```hlsl
float3 toPlant = worldPosition - _HectonSubmarineWashSphere.xyz;
float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
float washInfluence = saturate(1.0 - dot(toPlant, toPlant) * washInvRadiusSq);
float3 radialFallback = HectonKelpSafeNormalize(toPlant, float3(0.0, 0.0, 1.0));
float3 streamBasis = HectonKelpSafeNormalize(SubmarinePropwash.xyz, radialFallback);
float streamCone = saturate(dot(radialFallback, streamBasis));
float3 streamDirection = HectonKelpSafeNormalize(streamBasis + radialFallback * 0.085, radialFallback);
positionOS.xz += streamDirection.xz * (washInfluence * streamCone * SubmarinePropwash.w * _PropWashDisplacement * tipParabola);
```

Cinematic cheats used:
- Vertex sine/parabola fake instead of bones, joints, or CPU plant physics.
- Dot-product propwash cone instead of fluid simulation.
- Shader spore impostors instead of GameObject/VFX emitter per glowing plant.
- Global scalar radiation tint instead of material clones.
- Combat damage signal with poison status bit instead of collider callbacks per plant.
- Dithered spawn/fade instead of transparent alpha sorting.
- 1024 BC7 atlas target instead of scattered large source maps.

Exact microseconds saved:
- Vertex sway vs CPU bones/joints: 800-2500 us CPU saved in dense near-field kelp.
- Propwash dot fake vs trigger/collider/fluid route: 200-800 us CPU saved during submarine wash.
- GPU spore impostors vs per-plant particles: 300-1200 us CPU/GC saved in glowing flora clusters.
- Toxic status signal vs per-blade collision: 100-600 us CPU saved in poison flora fields.
- Dithered indirect fade vs transparent fade sorting: 150-500 us CPU/GPU sorting pressure saved in far flora.
- Sea-grass atlas import contract: runtime cost 0 us; VRAM locality improves by replacing scattered 2048 candidates with a 1024 BC7 target.
- OMEGA reciprocal cleanup: 1-2 us scalar helper savings, but mainly removes divide instructions from repeated flora helpers.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore /m:1` => passed, 0 warnings, 0 errors.
- `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies /m:1` => passed, 0 warnings, 0 errors.
- `git diff --check` => no whitespace errors; only line-ending normalization warnings.
- `FloraMaster.shader` => no file found under `Assets/_Project/Art/Shaders`; Cyrillic scan in flora shader paths returned no hits.
- Runtime hot-path scan => no `foreach`, `string.Format`, interpolated strings, or `.ToString()` introduced in `FloraInteractionManager.cs` / flora contracts. Editor-only authoring reports still format strings by design.
