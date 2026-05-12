# Rationale_DOC_VULCAN

Date: 2026-05-12
Agent: DOC_VULCAN
Status: VERIFIED MASTER GRADE - DOC SOURCE SCAN; RUNTIME PENDING VERIFICATION; CORE BUILD BLOCKED BY EXISTING DEPENDENCIES

## Decision 1: Resolve Prompt Source Conflict

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain a `DOC_VULCAN` block, while the user supplied a complete XML prompt in chat.
Solution: Use the chat XML block as the primary directive and record the missing batch block in status. This preserves cover-to-cover assignment text without inventing a non-existent batch source.
Rejected Alternatives: Treating neighboring batch prompts as authority was rejected because strict parsing forbids cross-agent contamination. Waiting for a new batch file was rejected because the supplied XML is complete.
Scalability potential: Low keeps tracking minimal; Middle adds source-backed README updates; High/Ultra expand troubleshooting sections without changing runtime code.
Hardware Impact: Documentation-only. Estimated low-end i3/MX350 runtime gain is indirect: prevents GameObject/CPU pipeline drift that would waste 0.1 ms+ in future work.

## Decision 2: Status and Logs Exception

Problem: The agent prompt marks `Docs/AgentLogs` and `Docs/Tasks` as a forbidden zone, while AGENTS.md and Task 1 require own status/rationale/log files.
Solution: Touch only `Status_DOC_VULCAN.md`, `Rationale_DOC_VULCAN.md`, and final `LOG_DOC_VULCAN.md`. Do not edit unrelated task or log files.
Rejected Alternatives: Ignoring status/log requirements would violate AGENTS.md. Editing unrelated AgentLogs for pass 2 was rejected unless needed as read-only evidence.
Scalability potential: Low preserves minimal compliance files; Middle/High/Ultra keep per-agent evidence isolated.
Hardware Impact: Documentation-only. Avoids future coordination waste rather than runtime cost.

## Decision 3: Source-Backed Pipeline Alignment

Problem: Existing bundle READMEs are sparse and do not document the current compute/BRG/CoreLit math path.
Solution: Update active README files with requirements tied to `Hecton_GpuScatter.compute`, `FloraCulling.compute`, `AbyssalFlowField.compute`, `BoidSimulation.compute`, flora shaders, and `Hecton_CoreLit.hlsl`.
Rejected Alternatives: Rewriting every file in each folder was rejected as broad churn. Creating a separate giant root report was rejected because the scope names four canonical bundles.
Scalability potential: Low documents MX350 fallbacks; Middle documents standard compute/BRG path; High and Ultra document visual overkill paths bought by saved cycles.
Hardware Impact: Expected indirect low-end gain is reduced future CPU scatter/AI/flora drift; estimated prevention value 0.1-0.8 ms/frame if implemented literally.

## Decision 4: Correct CoreLit Math LOD Claim

Problem: The task requested documentation for `_MATH_LOD_LOW` stripping point lights, but the current `Hecton_CoreLit.hlsl` source does not prove that behavior. The source keeps glow-point evaluation capped by `HECTON_GLOW_POINT_MAX` and additional-light helper paths remain present.
Solution: Document the proven low path only: dominant-axis safe normalize, squared color depth crush, and variant policy. Add an explicit warning that docs must not claim point/glow light stripping until source implements it.
Rejected Alternatives: Documenting the requested strip behavior was rejected because it would create a false runtime contract. Editing shader code was rejected because DOC_VULCAN is scoped to documentation.
Scalability potential: Low uses cheap normalize/depth math; Middle keeps standard CoreLit; High/Ultra spend saved cycles on richer fog/glow response without lying about source behavior.
Hardware Impact: Documentation-only. Prevents future shader work from assuming non-existent low-tier savings; estimated review prevention value 40 us per 10k CoreLit fragments discussed.

## Decision 5: Deprecate Legacy, Do Not Delete

Problem: `Legacy_World_Reference` contains strong prose that calls itself definitive and can mislead future agents into restoring GameObject-era world assumptions.
Solution: Add deprecation banners, a master replacement table, and technical replacement rules. Keep the files for visual/lore reference.
Rejected Alternatives: Deleting legacy files was rejected because they still contain usable mood and terrain language. Leaving them untouched was rejected because the prose conflicts with compute, BRG, shader morphing, and packed SoA reality.
Scalability potential: Low protects MX350/mobile DOD paths; Middle/High/Ultra can still mine visual intent without adopting old runtime architecture.
Hardware Impact: Indirect. Avoids resurrecting full GameObject terrain/scatter/fauna approaches that would burn 0.1 ms to multi-ms budgets on i3/MX350.

## Decision 6: Compile Check Handling

Problem: AGENTS requires compile verification, but this pass changed only markdown/text documentation and `Hecton8.Core.csproj` currently fails on unrelated missing source symbols.
Solution: Run the build once with quiet diagnostics, record the exact failure class, and do not edit code outside DOC_VULCAN scope to chase unrelated dependencies.
Rejected Alternatives: Ignoring compile was rejected. Editing core systems to satisfy a docs-only task was rejected as domain breach and cross-agent interference.
Scalability potential: Low keeps doc pass isolated; Middle/High/Ultra preserve ability for system owners to fix missing dependencies without DOC_VULCAN churn.
Hardware Impact: Runtime unchanged. Coordination gain is concrete: no accidental code edits in core/audio/save/voxel systems.

## Decision 7: Promote Cross-Agent R&D Only After Source Verification

Problem: New AgentLogs reported dithered biome blending, whale-fall scavenger hardening, voxel carving events, and origin-shift cache fixes after the first DOC_VULCAN pass. Logs are useful but not authoritative.
Solution: Verify each claim against source symbols before updating pipeline docs. Added requirements only where `GPUScatterDirector.cs`, `TerrainMaster.shader`, `HectonIndirectVegetationRenderer.cs`, `EcosystemDirector.cs`, `MigrationDirector.cs`, `SargassumMicroFaunaBoids.cs`, `Hecton_LeviathanOrganic.shader`, `VoxelDeltaProcessor.cs`, `VoxelChunkModifiedEvents.cs`, and `VoxelDynamicNavGridRuntime.cs` matched the claim.
Rejected Alternatives: Copying AgentLog prose was rejected because it can lag source. Waiting for runtime profiler proof was rejected for documentation-only requirements that are already source-backed and marked runtime pending.
Scalability potential: Low gets explicit no-GameObject fakes, tight cull radii, shader/acoustic whale-fall, and bounded event queues. Middle keeps full DOD path. High/Ultra spend saved CPU/GPU on denser micro-scatter, richer biome fog, and whale-fall visual overkill without changing gameplay truth.
Hardware Impact: Indirect but specific: avoids four-way terrain splat pressure (~35 us per 100k pixels), low-tier micro-scatter overreach (~90 us per scatter pass), synchronous nav/collider carve spikes (200+ us and 300-900 us classes), and low-tier individual scavenger proxies (zero extra low-tier boid cost).
