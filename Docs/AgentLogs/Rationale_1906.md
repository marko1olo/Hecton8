# Rationale 1906 - PBR Channel Derivation QA

Evidence class: STATIC_DOC / STATIC_SOURCE only.

## Decisions

1. Exact shader contracts were accepted only when Batch18 evidence named channels and target property.
   - ToolDecayLit `_MaskMap`: PackedMaskV1.
   - ProceduralBio `_ORMAtlas`: ORM.
   - MraoAtlasLit `_MraoMap`: MRAO.
   - SuitVisor `_VisorMaskTex`: dedicated visor mask.
   - ToolScreenDiegetic `_ToolScreenTex`: RGB screen signal.
   - FoamRibbon `_BaseMap/_MainTex`: RGB foam flows/breakup.

2. AI/UberNoir ARM stayed blocked.
   - Reason: Batch18 says the ingestion path classifies and binds ARM-like maps, but exact target shader channel semantics were not found.
   - Filename terms are not evidence.

3. WetBasaltShoreline 1428 was rejected for production PBR derivation.
   - Source evidence exists in Docs.
   - It is albedo-only.
   - Static seam metrics are high: left-right 30.78, top-bottom 33.40 mean RGB diff.
   - QA-only normal/AO/roughness previews would be albedo-derived guesses, not physical channel proof.

4. No production source-channel candidates were accepted.
   - This packet accepts contracts and blocks bad derivation.
   - Source families remain missing, blocked, or rejected until owner/import/Unity proof exists.

5. Pillow was used because ImageMagick was absent and Pillow was available.
   - Output is bounded to one contact sheet and one metrics file under `Docs/GeneratedAssets/Gemini/QA/1906`.
   - No ad hoc tool files were written.

6. Existing source references under `Assets/**` were not classified as owned output.
   - They can be future Unity-owner evidence.
   - This task forbids Assets writes and product promotion.

## Quality Consequence

Low, Middle, High, and Ultra may scale resolution, detail density, preview/report depth, and optional decals. They must not change channel order, shader semantics, material identity, source ownership, prefab authority, or gameplay truth.
