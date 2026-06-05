# Rationale 1905

Evidence class: STATIC_DOC

## Decisions

1. Gemini candidates were not fabricated.
   - Reason: task requires browser Gemini workflow if available. No callable Gemini browser/image workflow is exposed in this environment.
   - Consequence: all 24 rows remain `PENDING_GENERATION` and `BLOCKED_BY_TOOL_ACCESS`.

2. Source families were selected from Batch18 reports, not invented.
   - Shoreline and terrain rows answer inactive/under-textured foam, wet basalt, wet/dry waterline, basalt sediment, and salt grime debt from 1802 and 1821.
   - Flora/coral/kelp rows answer source-only shallow proof and material identity gaps from 1802 and 1901.
   - Product-face rows answer placeholder/default material contamination from 1893.
   - Sky/ocean rows are source/reference only from 1883; they are not final skybox, Aegir, moon, Crest, or cloud proof.

3. Prompt rows use a shared mandatory prefix instead of duplicating the full global contract in every table cell.
   - Reason: same-file shared prefix keeps prompts extractable and reduces copy error.
   - Gate: every row must be assembled as shared prefix plus row-specific delta.

4. No QA preview sheets were generated.
   - Reason: no candidate images exist.
   - Consequence: static image QA remains pending and no acceptance/rejection image verdicts are claimed.

## Rejection Constraints Preserved

- No writes under `Assets/**`, `Packages/**`, or `ProjectSettings/**`.
- No Unity launch, Unity MCP call, build, import, scene edit, prefab edit, material edit, shader/code edit, or `.meta` creation.
- No claim that a Gemini candidate is a final Unity-ready asset.
- No binary quality switches. All prompt rows record continuous `GlobalQualityWeight` consequences.

