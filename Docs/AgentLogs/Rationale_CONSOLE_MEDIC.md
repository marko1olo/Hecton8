# Rationale_CONSOLE_MEDIC

## Decision 001

Problem: User requested Unity Console diagnosis without a matching `<AGENT_PROMPT id="...">` in the active batch.
Solution: Use isolated `CONSOLE_MEDIC` identity and integration/console domain; do not steal neighboring batch prompts.
Rejected Alternatives: Using `PIPE_LOGISTICS_ARCHITECT` because an IDE tab referenced a missing status file; using any active batch prompt ID without explicit user assignment.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 002

Problem: Console fixes can become fake reports if based on static search or partial console reads.
Solution: Apply `QA_Evidence_Text_Filter_Audit` and treat Unity Console data as the primary evidence class for this task.
Rejected Alternatives: `rg`-only diagnosis; dotnet-only proof; speculative success language.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; prevents wrong edits.

## Decision 003

Problem: Unity MCP stopped exposing an active editor instance while Unity.exe stayed alive.
Solution: Fall back to the live Unity launch log at `Logs/CodexUnityLaunch_20260512_hud_surface.log` and only edit defects that matched current source.
Rejected Alternatives: Blocking indefinitely on MCP; inventing success from stale Console pages; launching a second editor over the same project.
Scalability potential: Low/Middle/High/Ultra unaffected directly; keeps console triage evidence-based under concurrent agent churn.
Hardware Impact: 0 us runtime; prevents duplicate editor contention on low-end disks/CPUs.

## Decision 004

Problem: `AcousticPortalPropagation` passed fields from `NativeArray<AcousticPortalNode>`-derived structs into `in` parameters, producing CS1612 in Unity compile output.
Solution: Copy `AcousticAup` positions into stack locals before `DistanceMeters(in ..., in ...)`.
Rejected Alternatives: Removing `in` from AUP math; changing NativeArray storage; converting path solve to managed lists.
Scalability potential: Low uses the same cheap stack copy; Middle/High/Ultra keep deterministic AUP distance math without GC.
Hardware Impact: Estimated 0 us frame cost, likely compile-only fix; avoids managed fallback.

## Decision 005

Problem: `IFoveatedSimulationTarget` expanded, while `FaunaBrain.Foveated` lagged and no longer satisfied the contract.
Solution: Implement stable entity hash/id, tier callback, and frozen-wrap handling using existing foveated/director/voxel contracts.
Rejected Alternatives: Removing the interface from fauna; direct manager lookups from unrelated domains; blind transform teleport.
Scalability potential: Low/Middle can cold-gate far predators; High/Ultra keep combat Tier0 lock and visual overkill room.
Hardware Impact: Estimated low-end gain is preserving far-frozen predator cold paths; no new per-frame managed allocation.

## Decision 006

Problem: Hologram assembly shader used `line` as an HLSL local token and `Hecton_CoreLit.hlsl` contained ShaderLab pragmas inside an include, generating shader errors/warnings.
Solution: Rename the local to `gridLine` and remove invalid shared include pragmas; pass-local shaders already own variant pragmas.
Rejected Alternatives: Disabling the shader; moving all variants globally; suppressing warnings without fixing source.
Scalability potential: Low keeps the cheap grid fake; High/Ultra retain hologram visual density without shader compile failure.
Hardware Impact: Estimated 0 us runtime; fixes shader import path and preserves existing visual cheat.

## Decision 007

Problem: Final verification cannot be completed from the active tools: MCP reports zero Unity instances and the live log has not refreshed after final edits.
Solution: Mark verification blocked, preserve exact evidence, and do not claim a green Console.
Rejected Alternatives: Using `dotnet build` as Unity proof; normalizing all mixed-line-ending shaders; editing unrelated asmdefs.
Scalability potential: Low/Middle/High/Ultra unchanged until Unity recompiles.
Hardware Impact: 0 us runtime; avoids high-churn line-ending rewrites and invalid verification.

## Decision 008

Problem: Second-pass audit found the same acoustic `in node.Position` risk in `FindNearestNode`, an unguarded foveated distance cache, and a fragment-stage fabricator-space matrix multiply in the hologram shader.
Solution: Copy acoustic node position once per loop iteration, clamp non-finite foveated distance to zero, and interpolate fabricator-local Y from the vertex stage for clip/edge calculations.
Rejected Alternatives: Leave adjacent risky code because the stale log named only earlier lines; add runtime logging; rewrite hologram assembly into geometry or simulation.
Scalability potential: Low keeps stack-only acoustic work and cheaper shader ALU; Middle keeps deterministic foveated state; High/Ultra spend saved fragment ALU on denser hologram visuals instead of duplicate coordinate transforms.
Hardware Impact: 0 us measured; low-end MX350/i3 path avoids one float4x4 multiply per affected hologram fragment and prevents NaN-driven foveated wrap churn without managed allocations.
