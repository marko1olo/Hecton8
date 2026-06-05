# Gemini Texture Pipeline Adversarial Review - 2017

Status: FAIL / PRODUCTION MATERIAL BLOCKED
Evidence class: STATIC_IMAGE_QA + SCRIPT REVIEW + VISUAL PREVIEW REVIEW
Unity evidence: none. No Assets import, material binding, texture importer proof, URP lighting proof, profiler proof, or in-scene screenshot was produced by this review.

## Scope

Reviewed:

- `Tools/GeminiTextureIntakeAudit.py`
- `Tools/TextureSeamPeriodicRefiner.py`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1429_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429*`

Authority used:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_Shader_Stutter_Linux_Vulkan.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Verdict

The Gemini wet-basalt pipeline is useful as a source/reference and quarantine QA lane. It is not a Unity production material pipeline yet.

The current 1429 raw candidate is correctly rejected. The refined `1429_periodic_mean` candidate can remain as a diagnostic source/decal candidate, but must not be accepted as a naked broad shoreline or terrain tile. It still has broad repeated rock plates, teal vein repetition, clipped blacks/whites, baked-light/specular-looking highlights, and no PBR family proof.

The most serious pipeline flaw is that `TextureSeamPeriodicRefiner.py` can produce `0.000` seam metrics by exact edge pinning. That is not visual tileability proof. It only proves the first and last rows/columns were forced to match.

## Blocking Findings

1. Edge pinning invalidates seam acceptance as a hard proof.

`TextureSeamPeriodicRefiner.py` averages the first/last column and first/last row after refinement. The audit then measures only those exact outer pixels. Result: refined candidates report `LR seam = 0.000` and `TB seam = 0.000` by construction.

Measured after pinning:

| Candidate | Exact LR/TB | Inner LR/TB |
|---|---:|---:|
| raw 1429 | 30.611 / 34.508 | 33.498 / 41.205 |
| periodic | 0.000 / 0.000 | 63.055 / 77.089 |
| periodic_darkpreserve | 0.000 / 0.000 | 63.055 / 77.089 |
| periodic_mean | 0.000 / 0.000 | 67.023 / 76.253 |

The inner-edge deltas are worse than raw. The report must not imply seam-fixed source acceptance from exact edge values alone. Required fix: audit an edge band, not just one pixel; add 2x2 and 3x3 visual review flags; report post-pin inner-band discontinuity.

2. Albedo has baked lighting and clipped range risk.

All reviewed 1429 variants keep `min=0` and `max=255`. `periodic_mean` has `15.857%` pixels at luminance `0`, `17.656%` at luminance `<=3`, `3.172%` at luminance `255`, and `25.342%` channel saturation. This is unacceptable for a production albedo because scene lighting will fight painted shadows/highlights.

The non-mean periodic variants are worse as basalt material truth: luminance mean `156.559`, `13.870%` pixels at luminance `>=252`, and `11.588%` at `255`. That reads as light grey/white wet rock, not black-grey wet basalt.

3. The material stack is incomplete.

Both manifests correctly state albedo-only / Unity material blocked, but the pipeline output still lacks required production proof:

- no normal map;
- no packed MRAO/wetness channel;
- no channel independence report;
- no roughness logic;
- no AO cavity-bias proof;
- no import settings report proving sRGB/linear, compression, mipmaps, max size, streaming;
- no material slot/SRP Batcher report;
- no URP neutral/grazing/final lighting previews;
- no Unity texture importer `.meta` proof for the 1429 source.

This blocks any production material claim.

4. Broad repeated forms remain visible.

The `WetBasalt1429PeriodicMean` 2x2 preview shows repeated large rock plates, teal veins, black holes, and bright speckle clusters. This is not acceptable as a naked broad terrain tile for surface/coast/photic routes. It may survive only as a small masked detail/decal or a low-opacity layer under macro variation.

5. Thresholds are too weak for HECTON-8 material acceptance.

`GeminiTextureIntakeAudit.py` allows `REVIEW` for severe clipped albedo as long as exact seams are below threshold. It also treats luminance mean below `45` as the only hard darkness failure. For surface/photic wet basalt, that misses bad white clipping, baked highlights, black crush, and material-truth failure.

Minimum missing gates:

- clipped black/white percentage thresholds;
- edge-band seam metrics over 8/16/32 px;
- low-frequency repeat detection;
- baked-light directional gradient detection;
- large-feature repeat visual flag;
- albedo mean and percentile range by material family;
- alpha channel validity;
- MRAO channel independence and semantic value checks when masks exist.

6. `periodic` and `periodic_darkpreserve` are byte-equivalent in metrics and file size.

Both refined variants report the same length, luminance, seam, clipping, and shift deltas. The manifest only promotes `periodic_mean`, which is correct, but the pipeline should not leave near-duplicate candidates with different semantic names unless their parameter route and metric delta are explicit. This creates false option confidence.

7. Production import is explicitly blocked.

The reviewed docs say 1429 is not imported into `Assets/**`. That must remain true until the full texture family exists. No TerrainLayer, material, prefab, or scene reference should consume these files.

## Warnings

- The raw 1429 source is better than 1428 for material identity, but still rejects for seam and repeated form.
- The `periodic_mean` source preserves darker basalt identity better than the lifted variants, but its black crush is severe.
- The QA reports use `PASS_STATIC` language correctly as non-Unity acceptance, but `SEAM_FIXED_SOURCE_CANDIDATE` in the 1429 manifest is fragile. Safer wording: `EDGE-PINNED_REFINEMENT_CANDIDATE / VISUAL_TILEABILITY_UNPROVEN`.
- 1024 source size is acceptable only for compact/standard diagnostic lanes. Hero coastline and close waterline use needs higher source quality or layered detail.
- If derived normal/MRAO maps are generated from this albedo without cleanup, baked highlights and black holes will contaminate roughness/AO and create physically false material response.

## Required Corrections Before Unity Use

1. Change seam QA to measure edge bands and inner discontinuity after refiner pinning.
2. Add clipping percentages and material-family luminance percentiles to the audit report.
3. Rename any exact-edge-pinned result as edge-pinned, not seam-proven.
4. Produce a cleaned albedo variant with reduced large-form repetition and no baked lighting.
5. Generate normal and MRAO/wetness from the cleaned source, not from the clipped candidate.
6. Run channel QA: normal validity, roughness variation, AO cavity bias, metallic near-zero for basalt except real ore inclusions, wetness mask semantics.
7. Produce importer proof: albedo sRGB, normal linear/NormalMap, MRAO linear, mips enabled, compression targets, streaming settings, size per quality lane.
8. Produce URP preview proof under neutral, low, grazing, and final surface/photic lighting.
9. Only then permit a quarantined material or terrain layer candidate during a Unity-owner slot.

## Scalability Consequences

Low / compact: do not ship this as a direct terrain tile. Use existing proven basalt or a cleaned 1024 albedo as a low-opacity macro-blended detail only after PBR/import proof. Avoid upload/memory churn by keeping it out of Addressables until accepted.

Middle: requires a cleaned 2048 key-world material or layered blend with proven MRAO/normal and mip behavior. Current broad repetition will be visible.

High: can use richer normals, decals, and wetness masks, but only if the albedo is cleaned first. More resolution will not fix baked lighting or repeated rock plates.

Ultra: hero shoreline needs 2048/4096 source, detail maps, macro breakup, and URP proof. The current candidate is source material, not visual-overkill material.

## Pass/Fail Summary

- Intake audit tool: FAIL for production acceptance; useful as first-pass static scanner only.
- Periodic refiner: FAIL as seam-proof tool; useful as an offline candidate generator only if reports expose edge pinning and inner-band discontinuity.
- 1428 manifest: PASS as honest rejection/source-only documentation.
- 1429 manifest: PARTIAL PASS; honest production blockers, but wording should stop treating exact edge pinning as seam-fixed proof.
- WetBasalt1429 QA artifacts: FAIL for production material acceptance.
- Unity import readiness: FAIL.
