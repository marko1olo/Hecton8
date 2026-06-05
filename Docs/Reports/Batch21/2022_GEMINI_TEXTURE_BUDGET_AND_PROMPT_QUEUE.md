# 2022 Gemini Texture Budget And Prompt Queue

Agent ID: 2022
Date: 2026-06-04
Role: GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE_CONTROLLER
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW

No browser, Gemini generation, Unity, MCP, Play Mode, profiler, import, build, asset edit, material edit, scene edit, prefab edit, package edit, or texture setting edit was performed.

## Boundary

This queue controls expensive browser/Gemini image generations. It does not approve any image for production. Every image remains a source candidate until intake QA, PBR derivation, Unity import, material binding, scene captures, and profiler/VRAM proof exist where required.

Available daily budget is roughly 7 browser accounts times 3-4 generations each, about 21-28 possible generations per day. That budget is not a license to burn attempts. Spend only on the ranked queue, review each candidate before retrying, and do not expose account names or emails in any report.

## Authorities And Evidence Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `HECTON8_ORCHESTRATOR.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `quality.md`
- `rendering.md`
- `water.md`
- `world.md`
- `celestial.md`
- `atmosphere.md`
- `Docs/Orchestration/GEMINI_BROWSER_IMAGE_ASSET_WORKFLOW.md`
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1429_MANIFEST.md`
- `Docs/Reports/Batch20/2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md`
- `Docs/Reports/Batch20/2017_GEMINI_TEXTURE_PIPELINE_ADVERSARIAL_REVIEW.md`
- `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md`
- `Docs/Reports/Batch20/2009_PROMPT_INDEX.csv`
- `Docs/Reports/Batch20/2009_TEXTURE_DERIVATION_QA_RULES.md`
- `Docs/Reports/Batch20/WET_BASALT_SEAMFIX_QA_CHECKLIST_20260604.md`
- `Docs/Reports/Batch20/2016_TOP_BLOCKING_MATERIALS.csv`
- `Tools/GeminiTextureIntakeAudit.py`
- `Tools/TextureSeamPeriodicRefiner.py`

Relevant registry mandates loaded:

- `QA_Evidence_Text_Filter_Audit.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Terrain_VirtualTexturing.txt`

## What Was Wrong

Surface/photic visual readiness is statically blocked by unresolved sky/cloud/terrain/triplanar/wetness/rock/coral material routes. Batch20 reports also show active primitive/proxy art debt in product-face surface, sky, water, coast, and shallow route names.

The current wet basalt 1429 source is statically rejected for broad terrain use. Its manifest and adversarial review show hard seam and band mismatches, clipped albedo, baked-light risk, and repeated large rock forms. It may be source/reference only, not a replacement TerrainLayer and not a production PBR material.

Older prompt packs contain useful prompts but do not enforce budget order. This Batch21 queue ranks scarce generation attempts against the current visual blockers.

## Spend-First Top 5

1. `TX_B21_WetBasaltShoreline_Albedo_20260604`
   - Reason: terrain, triplanar rock, wetness, coastline, and 1429 rejection all converge here. A clean albedo source is prerequisite for normal/MRAO/wetness derivation.
2. `TX_B21_ShoreFoamSaltContact_Mask_20260604`
   - Reason: waterline/foam contact is a visible first-route surface blocker. It is a cheap visual fake, not fluid simulation.
3. `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604`
   - Reason: photic shallows need readable substrate, not generic blue water over placeholder floor.
4. `TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604`
   - Reason: shallow alien coral is locked by vision and Batch20 coral material debt. It improves 0-100 m beauty and route density.
5. `TX_B21_AegirCloudBands_AlbedoSource_20260604`
   - Reason: sky/Aegir sources are top surface blockers. Aegir needs premium cloud-band hierarchy, not procedural sine stripes.

Do not spend retry attempts on ranks 6-10 until the top 5 have at least one generated candidate and static intake result.

## Full Candidate Queue

The machine-readable queue is in `Docs/Reports/Batch21/2022_gemini_texture_generation_queue.csv`.
The copy-paste prompt pack is in `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2022_priority_texture_prompts_20260604.md`.

| Rank | Spend first | Target | Output type | Unity use | Static basis |
|---:|---|---|---|---|---|
| 1 | YES | Wet basalt shoreline | Seamless square albedo source | Terrain/triplanar coastline material source | 1429 rejected; terrain/triplanar/wetness blockers |
| 2 | YES | Shore foam/salt contact | Seamless square RGBA mask source | Foam/contact/waterline fake mask | Wet basalt QA checklist; waterline blocker |
| 3 | YES | Photic seabed substrate | Seamless square albedo plus height-like source | Terrain/seabed substrate derivation | photic material floor and terrain blockers |
| 4 | YES | Shallow branching coral | Seamless square albedo plus height-like source | Coral material family source | 2004 coral channel matrix; coral blocker |
| 5 | YES | Aegir cloud bands | Seamless square albedo/cloud-band source | Celestial/Aegir source material | sky/Aegir blockers and prompt index |
| 6 | NO | Bright surface cloud deck | Seamless square cloud coverage source | sky/cloud atlas source | HectonSky cloud refs open |
| 7 | NO | Caustic/particle lookup | Seamless square linear mask/LUT source | shallow caustic/particle visual fake | water/rendering fake-first route |
| 8 | NO | Kelp blade/holdfast | Seamless square albedo plus height-like source | photic kelp material source | 2004 kelp matrix |
| 9 | NO | Scanner tool material | Seamless square albedo plus height-like source | product-face scanner/tool material | Batch18 scanner cube debt; lower than surface blockers |
| 10 | NO | Resource ore pickup material | Seamless square albedo plus height-like source | pickup ore/source material | Batch18 pickup primitive debt; lower than surface blockers |

## Budget Rules

- Generate ranks 1-5 first, one primary attempt each.
- Do not request a retry before running intake QA and writing a rejection reason.
- No more than 1 retry per texture unless it is rank 1-3 and the failure reason is clear, narrow, and prompt-fixable.
- Top-3 second retry is allowed only for explicit failures such as seam mismatch, baked lighting, perspective/object framing, or text/logo contamination. Do not retry vague taste failure twice.
- If rank 1 fails twice, stop wet basalt spending and use existing 1429 only as source/reference while moving to rank 2 and rank 3.
- Account switching is allowed only to avoid Gemini limits. Logs must say only `account switched`, never names or emails.
- Never download or save generated candidates into `Assets/**`. Use `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` first.

## Intake QA Command Use

For every tileable square source, save the downloaded file under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`, then run the candidate-specific command listed in the CSV. Example shape:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604
```

The audit creates CSV, Markdown, and 2x2 previews. `PASS_STATIC` is not Unity acceptance. It only means this static gate found no hard issue. Every candidate still needs manual 3x3 tiling review, channel role review, PBR derivation, import settings proof, material binding proof, and in-scene captures.

## TextureSeamPeriodicRefiner Use

`TextureSeamPeriodicRefiner.py` is diagnostic only. It may create candidate variants for study under `Docs/GeneratedAssets/Gemini/Refined/`, but it must not be used as seam proof.

Rules:

- Do not use `--edge-pin` as acceptance evidence.
- Any refined output must be labeled `diagnostic_refinement_candidate`.
- Rerun `GeminiTextureIntakeAudit.py` on the refined output.
- Manual 3x3 tiling review still controls visual tileability.
- A refined candidate with exact outer edge match but inner band mismatch remains rejected.

## QA And Import Gates

Source intake gate:

- file under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`;
- no account/email in filename, manifest, report, or prompt;
- exact prompt ID recorded;
- SHA-256 recorded by future intake owner;
- square/power-of-two where the row requires tileable material source;
- 2x2 and manual 3x3 tile preview;
- no text, logos, UI, watermark, border, perspective, horizon, object render, baked lighting, cast shadows, directional highlights, black/noir cover-up, muddy grade, crayon marks, or low-resolution mush.

PBR derivation gate:

- albedo is base color only and sRGB intent;
- normal or height source matches material scale and tile edges;
- roughness follows material state;
- AO is cavity-biased;
- MRAO/ORM/PackedMask channel order is documented, not guessed from filename;
- metallic is zero for rock, coral, kelp, foam, clouds, moons, and water unless the row declares real exposed metal;
- emission is semantic and sparse, not random glow.

Unity import gate for later owner:

- texture type, sRGB/linear, compression, mipmaps, Read/Write off, streaming policy, and max size proved;
- material/TerrainLayer slots proved for albedo, normal, packed mask, detail/emission where relevant;
- no material clones or per-instance material mutation;
- source remains outside `Assets/**` until accepted;
- Game View and Scene View captures exist for the target route;
- Frame Debugger/RenderGraph/profiler/GC/memory/VRAM proof exists if render route, shader route, texture residency, or material binding behavior changed.

## Wet Basalt 1429 Decision

Include wet basalt in Batch21 because the current 1429 candidate is statically rejected and the surface/coastline material floor remains blocked. Do not accept 1429 as production art.

Allowed 1429 use:

- visual reference;
- small masked decal source;
- prompt correction input;
- manual source study.

Forbidden 1429 use:

- broad terrain tile;
- direct TerrainLayer replacement;
- normal/MRAO derivation before albedo cleanup;
- production-ready claim.

## Scanner And Resource Pickup Decision

Scanner/tool and resource pickup material candidates are included at ranks 9 and 10 because Batch18 evidence shows product-face cube/plane primitive debt for scanner and pickups. They are not spend-first because Batch20 surface, sky, terrain, waterline, and photic blockers have higher first-route visual impact.

## Continuous Quality Consequences

These consequences apply to texture/source fidelity only. They do not alter gameplay truth, material role semantics, save identity, collision, authority route, item identity, or shader channel order.

| Lane | Consequence |
|---|---|
| Low / compact | Use smaller imported sizes and fewer secondary masks only after material identity, tileability, and channel sanity pass. No muddy/noir fallback. |
| Middle | Expected player lane: clear material read, correct normal/packed masks, enough detail for surface/photic route credibility. |
| High | Spend extra resolution and derivation quality on richer normals, wetness, foam breakup, cloud depth, coral pores, kelp fibers, and product-face scratches. |
| Ultra | Hero-source 2048/4096 detail and visual overkill are allowed only after the same source passes static and Unity proof gates. Ultra cannot rescue bad seams or wrong channels. |

## Residual Risks

- Gemini may ignore seamless/tileable constraints; every tileable candidate still needs 2x2 and 3x3 visual review.
- Gemini may bake lighting into albedo or masks; those outputs must be rejected, not repaired into production.
- `GeminiTextureIntakeAudit.py` is useful but incomplete. It is static source QA, not material, import, or Unity proof.
- Current queue does not resolve missing Unity material GUIDs or shader slot contracts. It only prepares source candidates.
- No generated image exists from this task. All visual quality claims remain `PENDING VERIFICATION`.

## Verification State

STATIC VERIFIED:

- Authorities and targeted evidence were reviewed.
- Queue ranked 10 candidates.
- Top 5 spend-first targets were selected.
- QA/import gates and retry limits were defined.

PENDING VERIFICATION:

- Gemini generation.
- image download.
- image QA result.
- PBR derivation.
- Unity import.
- material binding.
- Game View / Scene View proof.
- profiler, GC, memory, and VRAM proof.
