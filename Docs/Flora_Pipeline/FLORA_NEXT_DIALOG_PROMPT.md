# Flora Next Dialog Prompt

Read only these files first and use them as the source of truth for flora work:

1. /AGENTS.md
2. /Docs/PROCEDURAL_ASSET_PIPELINE.md
3. /Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md
4. /Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md
5. /Assets/_Project/Prefabs/Nature/Flora/Baked/README.md

Do not start by reading legacy flora docs in the repo root unless you need concept recovery after the main docs.
Treat root legacy files like `Vodorosli.md`, `Coralli.md`, the old Russian work notes, and the old transfer ledgers as reference-only, not primary instructions.

Task:
- continue building the HECTON-8 flora system for kelp and coral
- preserve the existing owner stack
- no parallel subsystem
- no runtime mesh generation for flora finals
- no runtime texture generation for flora finals
- no raw runtime Instantiate for flora scatter
- use MX350-first decisions
- keep status as PENDING VERIFICATION until validator/log/profiler evidence exists

Primary implementation priorities:
1. keep /Docs as the clean source of truth
2. align flora texture/material/shader contract with PROCEDURAL_ASSET_PIPELINE.md
3. keep LOD thresholds at 0.6 / 0.15 / 0.04 / 0
4. keep flora culling in the 60-120m contract through Unity/GPUI visibility rules
5. expand validator/report so stale assets fail closed
6. when real textures are required, stop and output only the exact master prompts + import settings for user generation

Current owner stack to preserve:
- WorldRuntimeBootstrapAuthoring
- WorldProceduralScatterDirector
- WorldProceduralFloraBakedStarterGenerator
- WorldProceduralFloraFinalVariantAuthoring
- WorldProceduralFloraTextureAuthoring
- WorldProceduralFloraMaterialAuthoring
- WorldProceduralFloraFinalVariantValidator

Current flora families:
- family.kelp.tall
- family.kelp.patch.dense
- family.kelp.canopy
- family.kelp.abyssal
- family.coral.low
- family.coral.branching
- family.coral.massive
- family.coral.plate
- family.coral.brittle

Before coding:
- inspect the existing codebase first
- state the exact affected files/systems
- do not guess unknown package APIs
- keep explanations short and factual
