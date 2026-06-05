# Rationale 2101

Agent ID: 2101  
Evidence class: STATIC_DOC

## Decisions

1. Channel contract uses `R=Metallic`, `G=Roughness`, `B=AO`, `A=Wetness/family mask` for the 2101 source package.
   - Reason: `2005_TEXTURE_CHANNEL_CONTRACTS.csv` and the wet basalt checklist require channel order to be named, not guessed. The wet basalt checklist names roughness for the known Hecton MRAO route, while other mandates contain older/different packing. Unity owner must lock target shader before import.
   - Risk: If target shader expects smoothness or alpha emission only, G must be inverted offline or A wetness must move to a separate mask. Marked `CANDIDATE_UNTIL_SHADER_LOCK`.

2. Included only five owned shoreline families: wet basalt shoreline, dry basalt transition, triplanar cliff rock, foam/salt contact mask, and wet sheen/leak/waterline mask.
   - Reason: task scope is shoreline/coastline material source package. Photic seabed, coral, Aegir, cloud deck, scanner, and resource pickup are 2022 queue items but not 2101-owned output.
   - Risk: none for 2101; sibling families remain future tasks.

3. Foam/salt contact is defined as a mask/decal/shader visual fake, not simulated foam or water truth.
   - Reason: water bible and cinematic cheat mandate prefer presentation fake for waterline contact unless gameplay truth requires simulation.
   - Risk: future alpha/decal route needs Frame Debugger/profiler proof before acceptance.

4. No Unity import, relink, material binding, scene scan, or visual proof was attempted.
   - Reason: user hard-forbid Unity, MCP, imports, builds, Assets edits, materials, shaders, scenes, prefabs, and runtime scripts.
   - Risk: all runtime/material readiness remains `PENDING VERIFICATION`.

