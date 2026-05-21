# Status_SHINOBU_275_AUDIT_SHADER_RG

Prompt: inline SUB_AGENT_PROMPT. No CURRENT_BATCH.md path was provided.
Domain: Echelon 5 / Screen-Space Wounds & Decals, URP RenderGraph shader ABI audit.
Relevant mandates read:
- REND_DescriptorBinding_Reality_Check.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Checklist

- [x] Task 1: Extract inline assignment and identify scope.
  DOD practice: strict XML block scope only; ignored neighboring systems except direct shader/render assets named in prompt.
  Rejected alternative: broad project render audit; too noisy and outside sub-agent mission.
  Estimate: 45 us static parsing.

- [x] Task 2: Read project authority/domain/mandates.
  DOD practice: authority spine and task-relevant registry mandates read before source judgment.
  Rejected alternative: infer URP policy from memory; risk against Unity 6000 RenderGraph mandate.
  Estimate: 900 us static read cost excluding CLI IO.

- [x] Task 3: Verify DTO/shader ABI.
  DOD practice: C# explicit layout offsets matched against HLSL StructuredBuffer field order.
  Rejected alternative: trust context statement "80B"; ABI needs source proof.
  Estimate: 7 us per DTO lane audit.

- [x] Task 4: Verify RenderGraph declarations and hot binding route.
  DOD practice: read RecordRenderGraph declarations and shader texture/buffer consumers.
  Rejected alternative: assume AddBlitPass captures external buffer/depth dependencies; hidden dependency is not acceptable.
  Estimate: 35 us CPU risk per pass record, GPU risk unmeasured.

- [x] Task 5: Verify renderer serialized shader references.
  DOD practice: map renderer GUIDs to .meta files.
  Rejected alternative: trust feature class default asset path; serialized renderer wins in player.
  Estimate: 20 us static GUID lookup.

- [x] Task 6: Verify old shader names and UsePass.
  DOD practice: project-local rg scan over named shader/script/data files and shader folder.
  Rejected alternative: visual inspection only; stale hidden shader names are easy to miss.
  Estimate: 40 us static symbol scan.

- [x] Task 7: Inspect shader loop cost and quality scaling.
  DOD practice: traced _GlobalVisorWoundCount from Vault quality-scaled maxActive to shader loop break.
  Rejected alternative: count hard loop 128 as automatic failure; active count is CPU-gated but still needs profiler proof.
  Estimate: 3-128 wound iterations per pixel, GPU unmeasured.

- [x] Task 8: Inspect stale binding risks.
  DOD practice: checked material mutation timing, buffer binding, and depth sampling path.
  Rejected alternative: treat material.SetBuffer during graph recording as pass-local state; it is shared material state.
  Estimate: 10-30 us CPU risk under multi-camera/reordered graph, GPU correctness risk unmeasured.

- [x] Task 9: Inspect visual integration with UberPost/torn edges.
  DOD practice: compared active serialized shader path with shader file named in prompt.
  Rejected alternative: assume HectonVisorUberPost.shader is active because it exists.
  Estimate: 20 us static GUID proof.

- [x] Task 10: Verify no source edits.
  DOD practice: audit-only; no source, shader, or renderer asset patch applied.
  Rejected alternative: hotfix renderer YAML; sub-agent mission said do not edit files.
  Estimate: 0 us runtime impact.

- [x] Task 11: Record final audit evidence.
  DOD practice: findings are file/line/severity/exact patch recommendation only.
  Rejected alternative: fake Unity verification; no Unity import, Console, Frame Debugger, or profiler run.
  Estimate: 120 us report assembly.

## Verification

Static-only audit. No Unity import, shader compile, Frame Debugger, RenderGraph Viewer, profiler, GCMonitor, player build, or dotnet build was run.
