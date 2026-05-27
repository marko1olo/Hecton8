# Unity Render Feature Shader.Find Trap - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Evidence class: STATIC_SOURCE + OFFICIAL_UNITY_DOCS + BUILD_GUARD_BLOCKED
Domain: Unity URP render-feature player shader reference hygiene

## Scope

User directive: keep finding subtle Unity traps, verify online/current docs, fix only correct low-risk source, and do not interfere with dirty agent files.

Relevant mandates re-read:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`

Official Unity facts used:

- Unity `Shader.Find` can work in Editor while failing in player if the shader is not included through a tracked reference or build inclusion route.
- Unity shader loading/prewarm documentation treats shader references and variant loading as explicit build/runtime concerns, not safe string lookup doctrine.

Source URLs:

- `https://docs.unity3d.com/ScriptReference/Shader.Find.html`
- `https://docs.unity.cn/Manual/shader-loading.html`

## Changed Files

- `Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs`
- `Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`

## Fixes

1. Release player shader route
   - Removed release-reachable fallback `Shader.Find` from four clean URP `ScriptableRendererFeature` owners.
   - Kept material ownership and pass setup unchanged.
   - Release builds now require the existing serialized `Shader` fields to carry the player dependency.

2. Editor/development fallback
   - Kept `Shader.Find` only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
   - Added editor asset loading for `HectonBiolumSSGIFeature` composite shader and `HectonSonarPointCloudFeature` overlay shader, matching the existing pattern in ocean/caustics features.

## Static Proof

Scoped release reachability scan:

```text
HectonSinglePassOceanFeature.cs: Shader.Find ctx=UNITY_EDITOR || DEVELOPMENT_BUILD; releaseReachable=False
HectonDeferredCausticsFeature.cs: Shader.Find ctx=UNITY_EDITOR || DEVELOPMENT_BUILD; releaseReachable=False
HectonBiolumSSGIFeature.cs: Shader.Find ctx=UNITY_EDITOR || DEVELOPMENT_BUILD; releaseReachable=False
HectonSonarPointCloudFeature.cs: Shader.Find ctx=UNITY_EDITOR || DEVELOPMENT_BUILD; releaseReachable=False
```

Shader assets found:

```text
Assets/_Project/Art/Shaders/Hidden_Hecton_OceanDepthFoam.shader
Assets/_Project/Art/Shaders/Hecton_DeferredCaustics.shader
Assets/_Project/Art/Shaders/Hecton_BiolumSSGIComposite.shader
Assets/_Project/Art/Shaders/SonarGridOverlay.shader
```

`git diff --check` passed for the four source files with only LF/CRLF working-copy warnings.

## Build State

Build recheck was attempted through the required AGENTS guard:

```text
Docs/Reports/BUILD_UNKNOWN_RENDER_FEATURE_SHADER_FIND_RECHECK_20260526.log
result: guard blocked build; no legal CPU/compiler window
post-block sample: CPU=71%; compilerProcessCount=0
```

This is not a compile failure. It is a blocked build launch. The latest clean full-solution CLI build remains:

```text
Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log
result: Build succeeded; 0 Warning(s); 0 Error(s)
```

That clean build is pre-render-feature-shader-fix evidence. Do not claim the four render-feature source edits are CLI-compile-proven until a later legal build window completes.

## Documentation Gates

Final documentation validation after the report/root-doc update:

```text
Tools/VerifyDocStructure.py -> pass=true; activeDocCount=702; encodingWithoutUtf8Sig=0
Tools/OOP_Doc_Scanner.py -> finalPass=true; activeFileCount=702; sourceSyncPass=true
```

## Residual

- Runtime/profiler microseconds saved claimed: `0`; no Unity Play Mode or profiler capture was run.
- Unity import, Console, Play Mode, player build, shader variants, render feature asset serialization, scene wiring, and visual proof remain pending.
- Dirty residual `Shader.Find` files were not edited.
- Documentation gates are closed for this pass.
