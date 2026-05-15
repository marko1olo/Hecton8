# HECTON-8 PBR Surface Doctrine

Owner: TECHNICAL_ARTIST_DATA
Prompt: PBR_MATERIAL_REFACTOR_SCOUT
Status: SURFACE DOCTRINE READY - PENDING UNITY IMPORT VERIFICATION
Audit source: `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA.json`

## Audit Facts

- First-party scan root: `Assets/_Project`
- Textures scanned by filename/material audit: 137
- Albedo energy candidates decoded by Python/Pillow: 26
- Albedo energy failures: 0
- Albedo energy warnings: 0
- Texture read errors: 0
- Albedo read errors: 0
- Texture import-setting issues: 5
- Estimated texture residency by offline BC-class model: 497.565 MiB
- ORM/mask candidates found: 17
- Detail candidates found: 13
- Materials scanned: 176
- Materials with prompt ORM slots: 0
- Materials with legacy/unknown packed mask slots: 9
- Materials with any packed mask slots: 9
- Materials with detail slots: 0
- Materials with audit issues: 29
- Materials with unresolved first-party texture references: 9
- Unresolved first-party texture references: 27
- Surface materials with unresolved texture references: 2
- Surface unresolved texture references: 8
- Surface unresolved BLOCKER materials: 2
- Channel-packing migration candidates: 22 (`LOW` = 13, `MEDIUM` = 9)
- Channel-packing candidate model: 146.3 MiB standard -> 65.78 MiB optimized, saving 80.52 MiB (55.0%)
- Machine-readable GOD_MODE texture override rows: 12
- Machine-readable global detail overlay rows: 10, minimum expected detail gain 20%
- Issue counts: `NO_PROMPT_ORM_SLOT` = 22, `NO_PACKED_ORM_OR_MASK_SLOT` = 13, `NO_DETAIL_MAP_SLOT` = 22, `UNRESOLVED_TEXTURE_GUID` = 9, `LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW` = 9

Conclusion: the current albedo set does not break the offline energy test. The material system has zero prompt-authoritative ORM slots, nine legacy/unknown mask slots, and zero wired detail slots. Five texture import settings are suspect. Broad unresolved texture GUID debt remains at 9 materials / 27 refs, but only 2 materials / 8 refs are prompt-surface unresolved debt after non-surface filtering. Both prompt-surface unresolved materials are `BLOCKER` severity because base/normal slots are unresolved. The offline residency estimate is not Unity profiler proof; it is a deterministic BC-class triage model for asset prioritization.

## ORM Packing Spec

Prompt-authoritative ORM layout:

- `R = Ambient Occlusion`
- `G = Roughness`
- `B = Metallic`
- `A = reserved`, default `1.0` if present

Import rules:

- File suffix: `_ORM`.
- sRGB: Off.
- Mipmaps: On for world materials.
- Compression: BC7 for hero/inspection materials, BC1/BC3 acceptable for low-risk world masks if banding is not visible.
- Wrap: Repeat for tileable world surfaces, Clamp only for authored one-off masks.
- Default fallback: `R=1.0`, `G=0.65`, `B=0.0`.

Conflict note: `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` contains an older packed-mask convention: `R=Metallic, G=AO, B=Smoothness, A=Emission`. Do not mix the two layouts in one shader family. This scout pass follows the extracted prompt: ORM = AO/Roughness/Metallic.

## Detail Map Library

Actual first-party candidates found:

1. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png`
2. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching.v2/detail___family.coral.branching.v2.png`
3. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/detail___family.coral.brittle.png`
4. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low/detail___family.coral.low.png`
5. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/detail___family.coral.massive.png`
6. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive.2/detail___family.coral.massive.2.png`
7. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/detail___family.coral.plate.png`
8. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal/detail___family.kelp.abyssal.png`
9. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy/detail___family.kelp.canopy.png`
10. `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/detail___family.kelp.patch.dense.png`

Missing hard-surface overlays:

- Fine cockpit scratches.
- Dust/grit in panel seams.
- Carbon fiber weave.
- Worn rubber.
- Brushed steel streaking.
- Oxidized aluminum pitting.
- Salt deposit speckle.
- Grease-hand smudges.
- Edge-chipped paint.
- Condensation micro-droplets.

These should be shared global overlays, not duplicated per material. They are surface belief fakes: one 512-1024 tileable map can buy apparent high-frequency wear on dozens of materials.

## Clearcoat Fake

Goal: wet glass and polished chrome without a second pass.

Shader parameters:

- `_FakeClearcoat`: 0..1
- `_FakeClearcoatRoughness`: 0.04..0.35
- `_WetEdgeBias`: 0..1
- `_NoirGrazingBoost`: 0..2
- `_ClearcoatTint`: RGB, default white

Single-pass math:

```hlsl
half NoV = saturate(dot(normalWS, viewDirWS));
half invNoV = 1.0h - NoV;
half fresnel = invNoV * invNoV;
fresnel *= fresnel * invNoV;

half coatTightness = saturate(1.0h - _FakeClearcoatRoughness);
half coatMask = saturate(_FakeClearcoat * (fresnel + _WetEdgeBias * coatTightness));
half3 coatedSpec = specularColor + (_ClearcoatTint.rgb * coatMask * _NoirGrazingBoost);
half3 coatedBase = lerp(baseColor, baseColor * 0.72h, coatMask * 0.35h);
```

Wet glass profile:

- Metallic: 0.0
- Roughness: 0.03..0.12
- `_FakeClearcoat`: 0.75..1.0
- `_WetEdgeBias`: 0.35
- Use detail normal at 0.05..0.12 strength for film breakup.

Polished chrome profile:

- Metallic: 1.0
- Roughness: 0.06..0.18
- `_FakeClearcoat`: 0.25..0.45
- `_WetEdgeBias`: 0.08
- Keep albedo dark; chrome brightness comes from reflection/specular, not white base color.

## Anisotropic Fake

Goal: brushed cockpit metal without ray-traced or multi-pass reflection truth.

Required per-material data:

- Tangent direction from mesh UV/tangent.
- `_AnisoStrength`: 0..1
- `_AnisoDirection`: -1..1, flips tangent/bitangent bias.
- `_BrushScale`: 16..96
- Optional brushed detail mask from a shared grayscale overlay.

Cheap lobe modulation:

```hlsl
half3 halfVec = normalize(lightDirWS + viewDirWS);
half tangentH = dot(tangentWS, halfVec);
half bitangentH = dot(bitangentWS, halfVec);

half tangentBand = saturate(1.0h - tangentH * tangentH);
half bitangentBand = saturate(1.0h - bitangentH * bitangentH);
half brushBand = lerp(bitangentBand, tangentBand, step(0.0h, _AnisoDirection));
brushBand = brushBand * brushBand * (3.0h - 2.0h * brushBand);

half brushNoise = SAMPLE_TEXTURE2D(_GlobalBrushDetail, sampler_GlobalBrushDetail, uv * _BrushScale).r;
half aniso = lerp(1.0h, brushBand * lerp(0.75h, 1.25h, brushNoise), _AnisoStrength);
specularTerm *= aniso;
```

This is a visual fake. It does not simulate full anisotropic GGX. It is predictable, cheap, and controllable for cockpit readability.

## Standard vs Optimized Spec

Standard material assumption:

- Albedo 1024 BC7: 1.33 MB with mips.
- Normal 1024 BC5: 1.33 MB with mips.
- AO 1024 grayscale/BC7-equivalent: 1.33 MB with mips.
- Roughness 1024 grayscale/BC7-equivalent: 1.33 MB with mips.
- Metallic 1024 grayscale/BC7-equivalent: 1.33 MB with mips.
- Total: 6.65 MB per material set.

Optimized material assumption:

- Albedo 1024 BC7: 1.33 MB with mips.
- Normal 1024 BC5: 1.33 MB with mips.
- ORM 512 BC7/BC3: 0.33 MB with mips.
- Detail maps: shared global overlays, not unique per material.
- Total unique set: 2.99 MB per material set.

Result:

- VRAM reduction: about 55% per material set under this import model.
- Detail increase: shared detail overlay tiled at 4x-16x adds visible high-frequency wear without raising unique albedo size.
- Runtime cost: one extra detail sample only on MED+ or inspection/hero material tiers. Low tier can disable detail normal and keep ORM only.

## NASA-Punk Noir Rationale

Surface rule: everything is functional, everything is worn.

- NASA-Punk means panels, tools, habitat modules, cockpit metals, suit gear, and vehicles must read as engineered objects with service history.
- Deep Sea Noir means the values stay controlled: dark bases, high grazing response, wet edges, salt/oxidation breakup, and no baked studio highlights in albedo.
- White albedo is suspect. Brightness belongs in lighting/specular/emission, not diffuse color.
- Wear should describe use: hand contact, maintenance scrapes, pressure seals, latch arcs, bolt halos, cable abrasion, salt traces, and flood-line stains.
- On low hardware, material richness comes from packed masks and shared detail, not extra draw passes.
- On high hardware, saved VRAM buys stronger detail overlays, longer mip residency, richer wetness response, and brushed-metal directionality.

## GOD_MODE Texture Resolution Overrides

These are tier overrides, not MX350 defaults. Apply only after hardware tier selection and memory residency checks.

| Asset class | GOD_MODE max | Format | Notes |
|---|---:|---|---|
| Hero cockpit albedo | 4096 | BC7 sRGB | Inspection-radius only. |
| Hero cockpit normal | 4096 | BC5 linear | Use detail normals before unique 4K normals. |
| Hero cockpit ORM | 2048 | BC7/BC3 linear | ORM stays below albedo resolution unless mask aliasing is visible. |
| World module albedo | 2048 | BC7 sRGB | Do not push all habitat panels to 4K. |
| World module normal | 2048 | BC5 linear | Shared trimsheets preferred. |
| Terrain albedo | 4096 | BC7/BC1 sRGB | Only for near hero terrain material families. |
| Terrain ORM | 2048 | BC7/BC3 linear | Shared packed masks. |
| Flora albedo atlas | 2048 | BC7 sRGB | Current detail maps must wire into shader before increasing size. |
| Flora detail atlas | 1024 | BC4/BC5 linear | Global tiling; no per-family duplication above 1024 unless proven. |
| Decal sheet | 1024 | BC7/BC3 | Damage/wear decals get priority over raw base-map resolution. |
| Brush/scratch globals | 1024 | BC4/BC5 linear | Shared globally across cockpit/habitat/vehicle materials. |
| UI atlas | 2048 | BC7 sRGB | Only for diegetic close-read surfaces. |

Load-shed:

- If VRAM used/total exceeds 0.90, demote GOD_MODE material overrides by one mip tier.
- If sustained frame time exceeds 25 ms, disable MED+ detail normal overlays before dropping base albedo.
- If texture upload spikes appear, keep async upload persistent buffer enabled and stage hero material residency behind loading/transition gates.

## Validator Command

```powershell
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json --markdown Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.md --csv-prefix Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA --ci-surface-gates
```

Current result: `ci_surface_gates=enabled`, `active_gate_profiles=surface_safe`, `active_gates=energy_failures,energy_warnings,albedo_read_errors,texture_budget`, `textures=137`, `energy_failures=0`, `energy_warnings=0`, `texture_read_errors=0`, `albedo_read_errors=0`, `import_issue_textures=5`, `estimated_texture_mib=497.565`, `texture_budget_mib=900.0`, `texture_budget_status=PASS`, `materials_with_prompt_orm=0`, `materials_with_legacy_mask=9`, `materials_with_detail=0`, `detail_map_missing_materials=22`, `channel_packing_candidates=22`, `channel_candidate_saved_mib=80.52`, `god_mode_override_count=12`, `global_detail_overlay_count=10`, `materials_with_unresolved_texture_refs=9`, `unresolved_texture_refs=27`, `surface_materials_with_unresolved_texture_refs=2`, `surface_unresolved_texture_refs=8`, `surface_unresolved_blocker_materials=2`, `materials_with_issues=29`.

Generated CSV artifacts:

- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_texture_import_issues.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_texture_read_errors.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_material_issues.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_unresolved_texture_refs.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_surface_unresolved_texture_refs.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_detail_candidates.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_detail_map_missing_materials.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_channel_packing_candidates.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_texture_memory_hotspots.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_god_mode_texture_overrides.csv`
- `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_global_detail_overlay_plan.csv`

CI gate modes:

```powershell
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --ci-surface-gates
python Tools\MaterialAudit.py --root Assets\_Project --fail-on-import-issues
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-energy-warnings
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-texture-read-errors
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-unresolved-refs
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-surface-unresolved-refs
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-channel-packing-candidates
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-detail-map-missing
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-material-issues
python Tools\MaterialAudit.py --root Assets\_Project --resolve-root Assets\_Project --fail-on-texture-budget
```

`--ci-surface-gates` is the current-corpus safe profile. It enables `energy_warnings`, `albedo_read_errors`, and `texture_budget`. It does not enable broad import/material/unresolved-reference gates because current first-party assets still have known migration debt.
Generated JSON/Markdown artifacts must record both available profiles and active gates so the report proves which gate mode produced it.
Projection/HUD, UI, celestial/moon/gas giant, skybox, and terrain material names are excluded from surface ORM/detail migration debt because they are not first-party hard-surface PBR material targets for this prompt.

Exit code contract:

- `1` = albedo energy failures.
- `2` = texture import-setting issues when `--fail-on-import-issues` is set.
- `3` = broad material migration issues when `--fail-on-material-issues` is set.
- `4` = unresolved material texture references when `--fail-on-unresolved-refs` is set.
- `5` = offline estimated texture residency exceeds `--texture-budget-mib` when `--fail-on-texture-budget` is set.
- `6` = albedo candidate texture cannot be decoded for energy validation when `--fail-on-texture-read-errors` is set.
- `7` = albedo bright-area energy warnings when `--fail-on-energy-warnings` is set.
- `8` = channel-packing migration candidates exist when `--fail-on-channel-packing-candidates` is set.
- `9` = base materials missing detail-map slots when `--fail-on-detail-map-missing` is set.
- `10` = surface-material texture references cannot be resolved when `--fail-on-surface-unresolved-refs` is set.

Regression proof:

```powershell
python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py
python -m unittest Tools.test_material_audit
```

Current test result: 13 tests pass, including subprocess coverage for import-debt exit 2, material-debt exit 3, unresolved-reference exit 4, texture-budget exit 5, albedo-read-error exit 6, energy-warning exit 7, channel-packing exit 8, detail-map exit 9, surface-unresolved-reference exit 10, non-surface material exclusion, and the `--ci-surface-gates` profile.

Generated lighting exclusion:

- Scene-generated lighting/probe EXR/HDR files such as `Assets/_Project/Scenes/02_HECTON_WORLD/ReflectionProbe-0.exr` are excluded from the surface PBR scan. They are not albedo, ORM, normal, or detail maps.

Known import issues from the current audit:

- `Assets/_Project/Art/TEXTURES/Detali/Soft Plume Noise - second try.png` - data texture has sRGB enabled.
- `Assets/_Project/Art/TEXTURES/Detali/soft_plume_noise_-_kakoy_to_seryy_nu_norm.png` - normal/data texture has sRGB enabled and is not imported as Normal Map.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png` - normal/bump map has sRGB enabled and is not imported as Normal Map.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png` - normal map has sRGB enabled and is not imported as Normal Map.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png` - specular/data texture has sRGB enabled.
