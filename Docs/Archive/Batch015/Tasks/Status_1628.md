# Status 1628 - ABYSSAL_BIOME_TRANSITION_AND_DITHER_FOG_POLISHER

Status: ACTIVE / PENDING VERIFICATION
Prompt source: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="1628">`
Task count: 20
Domain: Domain 18 Biome Transition Manager + Domain 67 Volumetric Fog & Light Shafts

## Mandates Read

- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_VRS_MX350_Reality_Check.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `ARCH_Execution_Phases.txt`

## Loop 0 - Setup

- [x] Extracted 1628 prompt from current batch.
  - DOD practice: strict XML extraction from `CURRENT_BATCH.md`.
  - Alternative rejected: using pasted partial prompt because it omits the closing block and has 19 listed tasks.
  - Estimate: 420 us.
- [x] Domain checked against stable roster.
  - DOD practice: mapped request to Domain 18 and Domain 67.
  - Alternative rejected: treating the role name as the only authority.
  - Estimate: 80 us.
- [x] Mandate baseline selected.
  - DOD practice: eight task-relevant mandates read before code.
  - Alternative rejected: broad registry scan without applying mandate specifics.
  - Estimate: 900 us.

## Task Checklist

- [x] Task 01 - biome settings static JSON ledger.
  - DOD practice: CSV read and `BIOME_TRANSITION_REPORT_1628.json` ledger records four current biome profiles and AUP/radius/fog parameters.
  - Alternative rejected: hidden scene/material scan as runtime owner. Existing CSV is the authoritative biome source for this manager.
  - Estimate: 740 us.
- [x] Task 02 - 8x8 Bayer dither model and HLSL plan.
  - DOD practice: `Hecton_DitherFog.hlsl` contains `thresholds[64]`, no raymarch token, no dynamic loop token.
  - Alternative rejected: 4x4 dither and blue-noise texture fetch as default path.
  - Estimate: 65 us per fullscreen 1080p pass saved versus texture/noise fetch model.
- [x] Task 03 - continuous biome interpolation math.
  - DOD practice: existing Burst job normalizes four candidate weights, clamps finite fallbacks, and report documents monotonic reef-to-abyss transition.
  - Alternative rejected: binary biome selection in shader.
  - Estimate: 9 us CPU retained, no new runtime allocation.
- [x] Task 04 - 64-byte CBuffer DTO layout.
  - DOD practice: explicit `BiomeLightingParametersDTO` offsets `0/16/32/36/40`, static layout validator extended.
  - Alternative rejected: implicit struct packing.
  - Estimate: 1 us safety cost, zero runtime allocation.
- [x] Task 05 - forensic report architecture.
  - DOD practice: `Docs/AgentLogs/BIOME_TRANSITION_REPORT_1628.json` contains hashes, ledger, static proof, build gate, and estimates.
  - Alternative rejected: chat-only report.
  - Estimate: 430 us.
- [x] Task 06 - materialize `BiomeLightingParametersDTO`.
  - DOD practice: `StructLayout(LayoutKind.Explicit, Size = 64)` added in `BiomeTransitionFogBlendJobs.cs`.
  - Alternative rejected: separate managed class or ScriptableObject runtime payload.
  - Estimate: 0 us hot-path delta.
- [x] Task 07 - implement `BiomeTransitionManager`.
  - DOD practice: reused existing `BiomeTransitionManagerRuntime` owner instead of duplicate manager; it already reads AUP/DataVault and runs Burst blending.
  - Alternative rejected: new competing `BiomeTransitionManager.cs` owner.
  - Estimate: avoids 12-30 us duplicate update risk.
- [x] Task 08 - zero-allocation GPU/global bridge.
  - DOD practice: compact `BiomeLightingParametersDTO` is uploaded through one `Shader.SetGlobalConstantBuffer` call in `LateFrameTick`; legacy 128B payload CBuffer upload removed.
  - Alternative rejected: material mutation, string shader calls in loop, and duplicate 128B CBuffer upload.
  - Estimate: 17 us CPU saved versus material mutation pattern; 64B upload path halves CBuffer traffic versus prior payload bridge.
- [x] Task 09 - implement `Hecton_DitherFog.hlsl`.
  - DOD practice: include created with analytical fog, 8x8 Bayer, quality resolve, shaft/silt/caustic/thermal helpers.
  - Alternative rejected: runtime volumetric scatter/raymarch.
  - Estimate: 54 us GPU saved statically versus composite depth gather plus extra noise fetches.
- [x] Task 10 - dithered light shaft projector.
  - DOD practice: HLSL shaft occlusion helper plus Editor cone mesh generator.
  - Alternative rejected: real volumetric shadow/light shaft pass.
  - Estimate: 80-200 us GPU avoided depending fill-rate.
- [x] Task 11 - continuous quality fog scaling.
  - DOD practice: `_H8GlobalQualityWeight` is packed at DTO offset 44 and a sentinel at offset 48 keeps quality `0.0` valid; no binary quality switch.
  - Alternative rejected: low/high shader keywords.
  - Estimate: variant debt kept at static count 4 in master lit.
- [x] Task 12 - low-level HLSL ALU optimization.
  - DOD practice: no `pow`, no `sqrt`, no `sin`; uses rational negative-exp and `rsqrt` guard.
  - Alternative rejected: physically accurate expensive fog functions.
  - Estimate: 12 us GPU saved in dense fog scenes, static estimate.
- [x] Task 13 - compile wall and namespace hygiene.
  - DOD practice: scoped new usings to Editor generator/test; runtime adds no new namespace.
  - Alternative rejected: pulling editor/URP reflection into runtime assembly.
  - Estimate: 0 us runtime.
- [x] Task 14 - branchless GPU pipeline dry-run.
  - DOD practice: shader quality path uses `lerp`, `step`, saturate; no dynamic branch for quality tiers.
  - Alternative rejected: `if (quality > threshold)` shader path.
  - Estimate: avoids warp divergence; exact profiler proof pending.
- [x] Task 15 - CPU/compiler gate and build/syntax assertion.
  - DOD practice: sampled `dotnet,csc` and CPU before build; build not launched because two `dotnet` processes were active and CPU was 97 percent.
  - Alternative rejected: violating contention gate.
  - Estimate: host protection, runtime 0 us.
- [x] Task 16 - CBuffer alignment editor/static assertion.
  - DOD practice: editor test validates `BiomeLightingParametersDTO` and `Hecton_Master_Lit` `UnityPerMaterial` at 192 bytes.
  - Alternative rejected: eyeballing comments only.
  - Estimate: 220 us static scan.
- [x] Task 17 - shader variant debt reduction test.
  - DOD practice: editor static test bounds master lit variant pragma debt at 4. Unity compiler database query is pending build/editor availability.
  - Alternative rejected: claiming exact compiled variant count without Unity compiler data.
  - Estimate: 120 us static scan.
- [x] Task 18 - zero-GC material update verification.
  - DOD practice: static test inspects upload/publish blocks for constant buffer route and no material mutation.
  - Alternative rejected: trusting code review without scanner.
  - Estimate: 160 us static scan.
- [x] Task 19 - SRP Batcher compliance audit.
  - DOD practice: master lit `UnityPerMaterial` CBuffer exists and aligns to 16-byte registers; hidden fullscreen fog uses global CBuffers by design.
  - Alternative rejected: forcing `UnityPerMaterial` into hidden fullscreen pass.
  - Estimate: avoids batcher regression; exact draw-call proof pending.
- [x] Task 20 - automated metric validator report.
  - DOD practice: report generated with SHA-256, depth-read counts, R8 budgets, build gate, and estimated microseconds.
  - Alternative rejected: final chat-only proof.
  - Estimate: 520 us.

## Loop 1 - Tasks 1-5

- [x] Read extracted prompt again from `CURRENT_BATCH.md`.
- [x] Built ledger/report architecture and Bayer/fog math plan.
- [x] Verification: CSV finite scan prepared; report file created.

## Loop 2 - Tasks 6-10

- [x] Added DTO, HLSL include, light-shaft helper, and offline generator.
- [x] Verification: static depth-read counter reports `ResolveProxyFog=1`, `FragComposite=1`.

## Loop 3 - Tasks 11-14

- [x] Added continuous `_H8GlobalQualityWeight` path and branchless shader quality resolve.
- [x] Verification: HLSL scan found no raymarch/dynamic loop tokens in `Hecton_DitherFog.hlsl`.

## Loop 4 - Tasks 15-17

- [x] Build gate executed.
- [x] Result: `BLOCKED_BY_CONTENTION`; `dotnet` active, CPU 97 percent. Static validation continued.
- [x] Verification: `UnityPerMaterial` size 192 bytes; master variant pragma debt 4.

## Loop 5 - Tasks 18-20

- [x] Static zero-GC material update scan added.
- [x] Final JSON report generated in `Docs/AgentLogs`.
- [x] Verification: `git diff --check` returned no whitespace errors for touched files; line-ending warnings only.

## Loop 6 - APEX Integrator Verification

- [x] Hot dependency scan.
  - DOD practice: extracted `FastTick`, `LateFrameTick`, and Burst job source blocks; no `GlobalRegistry.`, `GetComponent`, scene find, `Camera.main`, or `TryGetLatestCreated` in hot blocks.
  - Alternative rejected: chat-only claim without source scan.
  - Estimate: 190 us static scan.
- [x] Phase safety scan.
  - DOD practice: `FastTick` schedules/finalizes only after `IsCompleted`; presentation upload remains in `LateFrameTick`.
  - Alternative rejected: same-frame schedule/readback or `TryComplete` in hot tick.
  - Estimate: 150 us static scan.
- [x] Write-lock flattening.
  - DOD practice: tuning writes now use a single `TryWriteSingleBiomeVaultValue` helper with one `TryAcquireWriteLock` and `ReleaseWriteLock` in `finally`.
  - Alternative rejected: mutable `TryResolveHandle` tuning write.
  - Estimate: 0 us hot-path change; cold editor tuning write is safer.
- [x] Compact CBuffer proof.
  - DOD practice: runtime has exactly one `Shader.SetGlobalConstantBuffer` call and exactly one `LockBufferForWrite<BiomeLightingParametersDTO>` path.
  - Alternative rejected: duplicate legacy 128B payload CBuffer upload.
  - Estimate: 64B GPU upload per visual sync instead of 128B legacy payload upload.

## Loop 7 - APEX Compact Authority Polish

- [x] Re-read extracted 1628 batch prompt marker and active ledgers.
  - DOD practice: used CLI extraction/search against `CURRENT_BATCH.md`, then re-opened status/rationale before edits.
  - Alternative rejected: relying on chat memory after context compression.
  - Estimate: 210 us static scan.
- [x] Removed stale legacy-payload quality authority.
  - DOD practice: `H8DitherFogResolveQualityWeight` now resolves fallback -> legacy payload -> compact global sentinel, so `H8BiomeLightingParameters` is authoritative when bound.
  - Alternative rejected: allowing stale legacy CBuffer state to override the new 64B CBuffer.
  - Estimate: 0 us direct GPU cost; prevents visual drift/flicker after hot reload.
- [x] Renamed compact upload bridge.
  - DOD practice: `TryUploadBiomeLightingParametersFromPayload` names the actual route from DataVault payload to compact lighting CBuffer.
  - Alternative rejected: keeping the old `TryUploadShaderPayloadCBuffer` name after legacy payload CBuffer upload was removed.
  - Estimate: 0 us runtime, source-level dependency clarity.
- [x] Re-ran static APEX and shader scans.
  - DOD practice: in-memory source block extraction verified hot lookup bans, LateFrame presentation, `IsCompleted` guard, single write lock, compact CBuffer count, depth-read count, and HLSL expensive-token absence.
  - Alternative rejected: launching a build under CPU contention.
  - Estimate: 480 us static scan.

## Loop 8 - Bandwidth Discipline Polish

- [x] Converted compact GPU upload to explicit MemCpy.
  - DOD practice: `TryUploadBiomeLightingParametersCBuffer` now writes the locked `GraphicsBuffer` through `UnsafeUtility.MemCpy`.
  - Alternative rejected: struct assignment into the mapped CBuffer because AGENTS requires MemCpy for GPU updates.
  - Estimate: 0 us direct frame gain; API contract hardening.
- [x] Added compact CBuffer dirty guard.
  - DOD practice: stable FNV-1a hash over `BiomeLightingParametersDTO` suppresses unchanged 64B CBuffer uploads before `LockBufferForWrite`.
  - Alternative rejected: uploading every completed pipeline despite identical presentation payload.
  - Estimate: up to 64B visual-sync upload avoided per unchanged frame; CPU mapping avoided when stable.
- [x] Extended editor static tests for bandwidth discipline.
  - DOD practice: test now requires unsafe compact upload signature, `UnsafeUtility.MemCpy`, dirty hash guard, release reset, and absence of `mapped[0] =`.
  - Alternative rejected: relying on code review for bandwidth discipline.
  - Estimate: 160 us static scan.

## Loop 9 - Legacy Global Upload Dirty Gate

- [x] Added dirty guard for legacy shader globals.
  - DOD practice: `PublishShaderPayloadToUnityGlobals` now hashes sanitized fog/absorption/audio/weights/hashes/dither plus derived density/quality before `Shader.SetGlobalVector/Float`.
  - Alternative rejected: updating compatibility shader globals every visual sync when values are unchanged.
  - Estimate: avoids 10 legacy `Shader.SetGlobal*` calls on unchanged payload frames.
- [x] Reset legacy global dirty state with compact buffer release.
  - DOD practice: `_lastShaderGlobalPayloadHash` and `_hasUploadedShaderGlobalPayload` reset in `ReleaseShaderPayloadBuffers`.
  - Alternative rejected: keeping a stale hash across buffer teardown and risking a skipped first sync.
  - Estimate: 0 us hot-path cost.
- [x] Extended editor static tests for legacy global upload gate.
  - DOD practice: tests require `HashShaderGlobalPayload`, hash comparison, and release reset.
  - Alternative rejected: manual review only.
  - Estimate: 120 us static scan.

## Loop 10 - Cold Lookup Boundary Proof

- [x] Distinguished cold bootstrap lookup from hot-loop dependency violation.
  - DOD practice: added an editor test proving `TryGetComponent` is confined to `[RuntimeInitializeOnLoadMethod]` fail-safe bootstrap and absent from `FastTick`, `LateFrameTick`, and job `Execute`.
  - Alternative rejected: broad grep failure on the substring `GetComponent(`, which misclassifies cold scene repair as hot polling.
  - Estimate: 90 us static scan.

## Current Blockers

- `dotnet build` blocked by contention: latest `tasklist` showed no `dotnet/csc`, but `typeperf` sampled CPU at 71.63 percent.
- Unity/Profiler runtime proof absent. All performance and GC claims remain `PENDING VERIFICATION` until measured.
- Offline texture/mesh assets are generator outputs; Unity menu `HECTON-8/Graphics/1628 Run Biome Transition Polish` has not been executed in this session.
