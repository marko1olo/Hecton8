# Rationale 1747 - Ambient VFX

## Decisions

Problem: The task text assumed a global CPU particle bottleneck and listed `WorldProceduralScatterDirector.Bridge.cs` as verified, but local discovery did not find that bridge file and did find an existing GPU marine-snow owner.
Chosen route: Treat `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` as the active owner and audit it with its shader, compute shader, DTO contracts, budget catalog, and publisher `HectonUnderwaterVisuals`.
Rejected route: Creating `AmbientVfxDirector.cs` or repurposing `WreckageScatterManager.cs` was rejected because that would duplicate a stronger existing owner without proof of an actual defect.
Scaling impact: Low through Ultra devices keep one authority route; quality changes presentation budget and shader lanes, not gameplay truth.
Proof: Static references in `Docs/Reports/AMBIENT_VFX_DIRECTOR_REPORT_1747.json`.

Problem: The old assignment demanded Shuriken module rewrites and CPU-side emission/velocity module updates.
Chosen route: Preserve the existing compute-particle route: GPU buffers hold particle state, `Hecton_MarineSnow.compute` integrates motion, and `Hecton_MarineSnow.shader` renders a visible-index list through indirect procedural draw.
Rejected route: CPU `ParticleSystem.GetParticles`, `SetParticles`, or managed per-particle loops were rejected. Static scan found none in the GPU owner.
Scaling impact: Compact hardware gets fewer active particles and cheaper quality lanes; High/Ultra spend budget on flow, depth, and atlas lighting detail.
Proof: `HectonMarineSnowRenderer.cs:1262`, `HectonMarineSnowRenderer.cs:4657`, `Hecton_MarineSnow.compute:1207`, `Hecton_MarineSnow.shader:17`.

Problem: The task specified hard 500/5000 particle caps, which conflicts with the existing VFX budget catalog and root continuous-quality doctrine.
Chosen route: Keep `VfxComputeParticleBudgetCatalog` as source of truth: 8000 minimum marine snow, 14336 middle, 100000 maximum/overkill, then compress by pressure, render scale, VRAM state, density, and kill switches.
Rejected route: Replacing the catalog with hard binary caps was rejected as a stale task assumption and a violation of continuous `GlobalQualityWeight`.
Scaling impact: Weak devices keep authored spatial particulate instead of collapsing to a flat void; top-tier devices retain visual overkill without changing DTO layout or authority.
Proof: `VfxComputeParticleBudgetCatalog.cs:43`, `HectonMarineSnowRenderer.cs:4869`, `HectonMarineSnowRenderer.cs:5190`.

Problem: Runtime visual/performance claims require Unity scene, Profiler, Frame Debugger, or screenshot proof.
Chosen route: Mark runtime allocation, fill-rate, screenshot, and GPU timing claims as `PENDING VERIFICATION`.
Rejected route: Reporting estimated 0 B/frame, milliseconds, or visual quality as measured proof was rejected.
Scaling impact: No false acceptance. Static proof is separated from runtime acceptance gates.
Proof: Status and JSON report mark pending verifier lanes explicitly.
