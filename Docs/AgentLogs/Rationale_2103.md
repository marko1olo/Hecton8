# Rationale 2103

ID: 2103  
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW  
Status: concise decisions only.

## Decisions

1. Included anchor debris encrustation in the accepted family set.
   - Reason: task phase 1 explicitly lists anchor debris encrustation, and `2004.anchor.debris.shoreline.blend` defines source, topology, channel, and proof constraints.
   - Boundary: collision, debris placement, and structural material ownership remain future owner work.

2. Kept shoreline/intertidal flora separate from underwater kelp/coral.
   - Reason: `2019-Q006`, `2019-Q007`, `2019-G006`, and `2004.intertidal.shoreline.flora` identify dry-land kelp/coral as a production blocker.
   - Consequence: shoreline vegetation can exist only through coastal/intertidal source and placement constraints, not by reusing kelp/coral dry-land rules.

3. Rejected BioForge starter outputs as final production proof.
   - Reason: `2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md` states BioForge starter atlases and vertex colors are incomplete: ORMA exception, height-gradient-only R, missing final channel semantics, no scene/profiler proof.
   - Consequence: BioForge can be candidate shell/source only until remapped, material-bound, validated, placed, and profiled.

4. Kept soft reef fan as conditional, not unconditional.
   - Reason: `2019-G009` marks source missing/new family and requires explicit photic/mid placement and shader support.
   - Consequence: alpha-only fans are rejected; geometry-backed ribbed fan proof is required before acceptance.

5. Wrote prompt requirements instead of running generation.
   - Reason: task forbids generation/import and 2022 queue treats Gemini outputs as source candidates only.
   - Consequence: prompt pack is STATIC VERIFIED; image/source quality remains PENDING VERIFICATION.

6. Did not create dependencies on sibling agents 2101, 2102, 2104, 2105, 2106, or 2107.
   - Reason: task forbids dependency on sibling outputs.
   - Consequence: report cites prior stable Batch20/Batch21 evidence only.
