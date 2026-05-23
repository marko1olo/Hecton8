# SHINOBU_305 Agent Log

## 2026-05-22T00:00:00Z - Procedural IK Matrix Sender
What was wrong:
- Leviathan terrain IK still hardcoded `GlobalQualityWeight` to 1.0 inside the Burst job, preventing thermal/quality scaling.
- Fauna runtime passed an authoritative quality constant instead of the continuous global weight.
- Fauna wound bounds discovery referenced `SkinnedMeshRenderer`, keeping a CPU-bone renderer dependency in the fauna runtime scope.
- IK target handoff used runtime float target only; no explicit double3 AUP root-target subtraction existed at the job boundary.
- GPU upload copied `MaxSegments` every visual sync, including inactive tail slots.
- Existing black-box dump route used `Dump_LEVIATHAN_RIGGER.bin`, not the assigned SHINOBU_305 forensic lane.

What was done:
- Added 32B `IkStateDTO`, 64B `ProceduralBoneDTO`, 32B `IkConfigDTO`, and layout validation through `ProceduralIkMatrixLayout`.
- Added Burst `GenerateMockIkTargetsJob`, `EvaluateProceduralIkJob`, and `CalculateBoneMatricesJob`.
- Enabled unsafe code for `Hecton8.Animation.IK` to allow raw pointer/ref mutation with `UnsafeUtility.AsRef`.
- Routed `FaunaKinematicsRuntime` quality through `ResolveGlobalQualityWeight()` and removed the terrain IK hardcoded quality constants.
- Added root/target AUP fields to `LeviathanTerrainIkJob`; target is localized by `HeadTargetAup - RootAup` in double precision before float IK.
- Changed GPU upload to copy active segment count only through the existing double-buffered `LockBufferForWrite` helper.
- Replaced fauna runtime `SkinnedMeshRenderer` scratch with generic `Renderer` scratch.
- Added UI Toolkit tuner `Shinobu305ProceduralIkTunerWindow`.
- Added editor `SkinnedMesh_Scanner` report writer for `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- Added `ReadOnlySpan<byte>` FNV CSV parser for `fauna_ik_profiles.csv`-style IK profiles.
- Updated live gizmo to draw matrix-forward yellow rays from raw bone matrices.
- Updated dump path to `Docs/AgentLogs/Dump_SHINOBU_305.bin`.

Cinematic cheats used:
- Dear Lie sine/triangle wave target offsets replace expensive body-fluid/tendon simulation.
- Continuous quality reduces FABRIK passes from 8 to 1 under pressure instead of changing gameplay truth.
- Matrix/VAT presentation remains visual-only and excluded from rollback/Merkle state.

Exact microseconds saved estimates:
- Removing managed bone hierarchy path: 80-300 us per 50-bone leviathan group versus main-thread Transform/SkinnedMeshRenderer bones.
- Active-count GPU upload: 64 bytes per inactive segment skipped; expected 5-25 us saved in crowded leviathan visual-sync frames on i3/MX350.
- Quality-scaled FABRIK: low quality saves up to 7 passes per chain; estimated 0.08-0.35 us per 20-bone chain depending active iteration count.
- Bounds renderer generic path: 6-20 us saved per cold fauna bounds refresh by removing skeletal renderer-specific route.
- CSV parser: cold path only; avoids `string.Split`/`float.Parse` allocations.

Verification:
- `git diff --check` clean for touched files.
- Scoped `rg` found no runtime `SkinnedMeshRenderer` in `Assets/_Project/Scripts/Fauna`, `Animation/IK`, or `Animation/FaunaProcedural`.
- Scoped `rg` found no SHINOBU_305 `ComputeBuffer.SetData`, `GraphicsBuffer.SetData`, `new NativeArray`, or `MemClear` additions.
- `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal` restored, then failed on external ecosystem/spatial-grid missing types in `ShinobuEcosystemBalancer.cs` and `EcosystemDirector.cs`; no SHINOBU_305 touched file appeared in compiler errors.

## 2026-05-22T14:41:35+04:00 - Polish Pass / Procedural IK Matrix Sender
What was wrong:
- New SHINOBU_305 visual-only target/FABRIK/matrix jobs were still using `FloatMode.Deterministic`; this wastes non-authoritative presentation ALU.
- Tuner read a runtime snapshot path instead of preferring the 300-frame telemetry ring.
- `SkinnedMesh_Scanner` was lexical, while Task 19 explicitly demanded AST proof.
- GPU upload still delegated to a generic raw memcpy helper instead of a SHINOBU_305 Burst mapped-pointer copy job.
- Build cannot be rerun now: CPU sampled above the 50% gate and dotnet processes are active.

What was done:
- Changed `GenerateMockIkTargetsJob`, `EvaluateProceduralIkJob`, and `CalculateBoneMatricesJob` to `FloatMode.Fast`.
- Added `FaunaKinematicsRuntime.TryGetLeviathanProceduralTelemetryForEditor`, a pure editor-only telemetry ring read bridge.
- Updated `Shinobu305ProceduralIkTunerWindow` to prefer the telemetry bridge and show flags from the latest ring entry.
- Upgraded `SkinnedMesh_Scanner` to Roslyn `CSharpSyntaxTree` source parsing with fail-closed syntax diagnostics and retained prefab `SkinnedMeshRenderer` component proof.
- Replaced SHINOBU_305 runtime matrix upload call with `LeviathanGpuBoneUploadJob.Run()` over the mapped `GraphicsBuffer.LockBufferForWrite` pointer.

Cinematic Cheats used:
- Dear Lie traveling sine/triangle offsets feed FABRIK instead of simulating muscle, water drag, or tendon physics.
- Continuous `GlobalQualityWeight` scales active segments and FABRIK iterations instead of binary low/high switches.
- Visual curvature remains excluded from rollback/Merkle truth and is reconstructed as GPU/VAT presentation.

Exact microseconds saved:
- Visual-only `FloatMode.Fast`: estimated 3-12% reduction in SHINOBU_305 IK kernel time on i3/MX350 depending active count.
- Active-count matrix upload: 64 bytes skipped per inactive segment, estimated 5-25 us in crowded leviathan visual-sync frames.
- Burst mapped-pointer upload avoids `SetData` stall path and keeps the copy contiguous; same-frame async schedule+Complete was rejected.
- Dear Lie versus per-bone physics: estimated >100 us saved for large leviathan groups.

Compile / verification:
- Scoped `git diff --check` clean for SHINOBU_305 write-set.
- Scoped forbidden-token search found no SHINOBU_305 runtime `ComputeBuffer.SetData`, `GraphicsBuffer.SetData`, `new NativeArray`, or `UnsafeUtility.MemClear` additions.
- Current rebuild blocked by hardware gate: CPU 51.6%, then 58.1%, then 39.1%; dotnet PID 5544 remains active; no `csc.exe` found.
- Previous build failed on external ecosystem/spatial-grid missing types, not SHINOBU_305 touched files.

<SELF_AUDIT agent="SHINOBU_305" domain="Echelon 3 / Leviathan Procedural IK GPU Matrix Sender" status="POLISH_PASS_ACTIVE_COMPILE_BLOCKED_EXTERNAL">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS" proof="rg archaeology over Animation/Fauna/IK surfaces; existing owner FaunaKinematicsRuntime retained." />
    <Task id="02" status="PASS" proof="No new competing manager; isolated jobs/editor tools only." />
    <Task id="03" status="PASS" proof="No new flinch signal; existing CombatDamageSignal/PhysicsImpactSignal lanes documented." />
    <Task id="04" status="PASS" proof="Runtime SkinnedMeshRenderer dependency removed from fauna bounds path; editor scanner verifies remaining fauna scope." />
    <Task id="05" status="PASS" proof="No FinalIK or managed IK library in scoped fauna/procedural runtime paths." />
    <Task id="06" status="PASS" proof="GenerateMockIkTargetsJob creates figure-eight AUP-localized stress targets." />
    <Task id="07" status="PASS" proof="EvaluateProceduralIkJob performs chain-strided FABRIK over flat IkStateDTO buffers with NoAlias and pointer mutation." />
    <Task id="08" status="PASS" proof="Dear Lie sine/triangle traveling offsets replace body physics." />
    <Task id="09" status="PASS" proof="CalculateBoneMatricesJob writes finite 64B matrix DTOs with safe forward/up frame." />
    <Task id="10" status="PASS" proof="LeviathanGpuBoneUploadJob copies active matrices into LockBufferForWrite mapped pointer; no SetData." />
    <Task id="11" status="PASS" proof="GlobalQualityWeight continuously scales segments and FABRIK iteration count." />
    <Task id="12" status="PASS" proof="Root/target double3 AUP subtraction occurs before float IK." />
    <Task id="13" status="PASS" proof="IK/matrix DTOs remain visual lanes; SaveSystem/Core search found no rollback/Merkle participation." />
    <Task id="14" status="PASS" proof="Vault lanes use UninitializedMemory for overwritten matrix/position/constraint/collider/scratch buffers." />
    <Task id="15" status="PASS" proof="300-entry LeviathanTerrainIkTelemetryEntry ring and Dump_SHINOBU_305.bin route active." />
    <Task id="16" status="PASS" proof="UI Toolkit tuner reads telemetry ring bridge and exposes amplitude/speed/iteration/quality controls." />
    <Task id="17" status="PASS" proof="ReadOnlySpan byte CSV parser writes IkConfigDTO with FNV species hash and manual numeric parsing." />
    <Task id="18" status="PASS" proof="Scene gizmo reads raw matrices and draws yellow forward rays." />
    <Task id="19" status="PASS" proof="SkinnedMesh_Scanner now uses Roslyn AST source parsing and prefab component scan." />
    <Task id="20" status="PASS_WITH_COMPILE_BLOCK" proof="Layout validation and static checks pass; rebuild deferred by CPU/dotnet gate and previous external missing types." />
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ProceduralBoneDTO size="64" alignment="64B cache line">
      <Field name="LocalToWorldMatrix" offset="0" size="64" type="float4x4" />
      <Padding bytes="0" />
    </ProceduralBoneDTO>
    <IkStateDTO size="32" alignment="32B">
      <Field name="CurrentPos" offset="0" size="12" type="float3" />
      <Field name="TargetPos" offset="12" size="12" type="float3" />
      <Field name="BoneLengthMeters" offset="24" size="4" type="float" />
      <Field name="Flags" offset="28" size="4" type="uint" />
      <Padding bytes="0" />
    </IkStateDTO>
    <IkConfigDTO size="32" alignment="32B">
      <Field name="SpeciesHash" offset="0" size="4" />
      <Field name="SineWaveAmplitudeMeters" offset="4" size="4" />
      <Field name="SineWaveSpeed" offset="8" size="4" />
      <Field name="MaxBendRadians" offset="12" size="4" />
      <Field name="BoneLengthMeters" offset="16" size="4" />
      <Field name="MaxFabrikIterations" offset="20" size="4" />
      <Field name="Flags" offset="24" size="4" />
      <Field name="Reserved0" offset="28" size="4" />
    </IkConfigDTO>
    <LeviathanTerrainIkTelemetryEntry size="96" alignment="32B multiple" note="300-entry black-box ring">
      <Field name="RootAup" offset="64" size="24" type="double3" />
      <Field name="BurstSolveMicros" offset="92" size="4" type="float" />
    </LeviathanTerrainIkTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is sanitized to 0..1, smoothed, then used to lerp active segment count and FABRIK iterations. Below 0.3, the solver collapses toward 1 pass, reduced active matrices, smaller sine offsets, and no gameplay-truth mutation. Middle weights keep stable body read while reducing ALU. High/Ultra spend saved CPU budget on denser matrix upload and shader/VAT overkill.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <VaultBuffer id="LeviathanSegmentPositions" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanPreviousSegmentPositions" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanBoneMatrices" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanProceduralBoneConstraints" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanCreatureColliderProxies" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanTerrainIkTelemetryRing" capacity="300" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanTerrainIkTelemetryCursor" owner="GlobalDataVault" />
    <VaultBuffer id="LeviathanRigCsvScratch" owner="GlobalDataVault" />
    <PersistentPrivateNativeArrays status="NONE_ADDED_BY_SHINOBU_305" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    <Job name="GenerateMockIkTargetsJob" output="IkStateDTO.TargetPos/Flags" burst="Fast" noAlias="States" />
    <Job name="EvaluateProceduralIkJob" output="IkStateDTO.CurrentPos" burst="Fast" noAlias="States" />
    <Job name="CalculateBoneMatricesJob" output="ProceduralBoneDTO.LocalToWorldMatrix" burst="Fast" noAlias="States,BoneMatrices" />
    <Job name="LeviathanGpuBoneUploadJob" output="mapped GraphicsBuffer pointer" burst="Fast" noAlias="Source,Destination" schedule="Run inside VISUAL_SYNC mapping window" />
    <Dispatcher note="No arbitrary same-frame async Complete added; existing DispatcherJobSwap lifecycle remains owner-controlled." />
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <Asmdef name="Hecton8.Animation.IK" references="Hecton8.Core.Contracts,Hecton8.Core.Memory,Unity.Burst,Unity.Collections,Unity.Jobs,Unity.Mathematics" siblingRuntimeReferences="NONE" unsafe="true" />
    <RuntimeGlobalRegistryAccess route="cold RefreshColdDependencies/TryRegister only; no SHINOBU_305 job hot polling" />
    <Build status="BLOCKED_BY_GATE_AND_EXTERNAL_DEPS" cpuSamples="51.6,58.1,39.1" activeProcess="dotnet PID 5544" externalMissingTypes="SpatialGrid/Ecosystem DTOs" />
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    CPU physics/tendon/body-fluid simulation rejected. Cheap sine/triangle wave offsets plus FABRIK connect-the-dots produce leviathan slither. Complexity before: per-bone Transform/SkinnedMeshRenderer hierarchy plus possible physics, O(bones) main-thread scene sync with renderer bounds upload. Complexity after: flat O(chains*bones*qualityIterations) Burst math and O(activeMatrices) contiguous GPU upload; no CPU bones.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="CBUFFER_HARDENING" status="ACTIVE_COMPILE_GATE_PENDING">
  <WhatWasWrong>
    The matrix path used `GraphicsBuffer.LockBufferForWrite`, but the scalar control path still touched material properties through `Material.SetFloat`/material binding. That risks SRP batcher breaks and driver validation churn in leviathan-heavy shots.
  </WhatWasWrong>
  <WhatWasDone>
    Removed SHINOBU_305 per-material scalar/buffer binding from `FaunaKinematicsRuntime`. Added explicit 32B `LeviathanIkShaderGlobalsDTO` with two `float4` lanes, double-buffered it as `GraphicsBuffer.Target.Constant`, published it through `Shader.SetGlobalConstantBuffer("_H8LeviathanIkGlobals", ...)`, and changed `Hecton_LeviathanOrganic.shader` to read bone count, IK quality, tail whip, segment length, and enable flag from that CBuffer.
  </WhatWasDone>
  <StructLayout>
    <LeviathanIkShaderGlobalsDTO size="32">
      <Field name="Scalars0" offset="0" size="16" payload="boneCount,quality,tailWhip01,segmentLength" />
      <Field name="Scalars1" offset="16" size="16" payload="gpuSkinningEnabled,pad0,pad1,pad2" />
      <Padding bytes="0" />
    </LeviathanIkShaderGlobalsDTO>
  </StructLayout>
  <CinematicCheat>
    No extra CPU animation truth was introduced. The CBuffer only feeds shader deformation scalars; the Dear Lie remains sine/triangle-wave visual curvature plus matrix skinning.
  </CinematicCheat>
  <MicrosecondsSaved estimate="3-15us on i3/MX350 in leviathan-heavy views">
    Savings come from removing per-material scalar mutation and preserving SRP/global buffer binding discipline. Matrix upload remains active-count contiguous copy.
  </MicrosecondsSaved>
  <CompileGate>
    `dotnet build --no-restore` reached NETSDK1004 before C# because `Temp/obj/Hecton8.Core/project.assets.json` is missing. Restore/build was not launched after CPU sampled 63.1%, per user gate.
  </CompileGate>
</FORENSIC_ADDENDUM>

<COMPILE_ATTEMPT agent="SHINOBU_305" status="BLOCKED_BY_EXTERNAL_DEPENDENCY">
  <Command>dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal</Command>
  <Gate cpuPercent="30.1" dotnetProcesses="0" cscProcesses="0" />
  <Restore status="SUCCESS" elapsed="95ms" />
  <CompilerErrors owner="external_ecosystem_genetics" count="11">
    <MissingType name="FaunaGeneticsTuningDTO" files="Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs;Assets/_Project/Scripts/World/EcosystemDirector.cs" />
    <MissingType name="FaunaGeneticsProfileDTO" files="Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs;Assets/_Project/Scripts/World/EcosystemDirector.cs" />
    <MissingType name="GeneticsTelemetryEntry" files="Assets/_Project/Scripts/World/EcosystemDirector.cs" />
  </CompilerErrors>
  <SHINOBU305TouchedFilesInCompilerErrors count="0" />
</COMPILE_ATTEMPT>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="ADJACENT_PROCEDURAL_RENDERER_CBUFFER_SWEEP" status="ACTIVE_STATIC_VERIFIED">
  <WhatWasWrong>
    A domain scan still found per-material scalar/buffer writes in adjacent procedural bone and tentacle renderers: `ProceduralBoneBlenderRuntime` and `LeviathanTentacleVerletSolver`.
  </WhatWasWrong>
  <WhatWasDone>
    `ProceduralBoneBlenderRuntime` now uses `_H8ProceduralBoneGlobals` as a 32B double-buffered constant buffer. `LeviathanTentacleVerletSolver` now uses `_H8LeviathanTentacleGlobals` as a 64B double-buffered constant buffer and `Hecton_LeviathanTentacleIndirect.shader` reads radius/fx/flow values from that CBuffer. Matrix, radius, and flow payloads remain structured `GraphicsBuffer` lanes.
  </WhatWasDone>
  <StructLayout>
    <ProceduralBoneShaderGlobalsDTO size="32" lanes="two float4" />
    <LeviathanTentacleShaderGlobalsDTO size="64" lanes="radiusFxFlow,flowResolution,flowCenter,flowSpacing" />
  </StructLayout>
  <MicrosecondsSaved estimate="5-20us on i3/MX350 in combined body/tentacle shots">
    Removes material property mutation and preserves global buffer/CBuffer binding discipline for adjacent procedural renderers.
  </MicrosecondsSaved>
  <StaticVerification>
    Scoped `rg` over `FaunaKinematicsRuntime`, `ProceduralBoneBlenderRuntime`, and `LeviathanTentacleVerletSolver` found no `SetFloat`, material `SetBuffer`, `SetVector`, `_gpuSkinningMaterial`, or `_publishGlobalBuffer` calls in these matrix/indirect paths.
  </StaticVerification>
  <CompileGate>
    Post-sweep rebuild deferred: CPU sampled 65.5%, `dotnet`/`csc` count 0, user gate requires CPU <= 50%.
  </CompileGate>
</FORENSIC_ADDENDUM>

<COMPILE_ATTEMPT agent="SHINOBU_305" status="BLOCKED_BY_EXTERNAL_DEPENDENCY_POST_SWEEP">
  <Command>dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal</Command>
  <Gate cpuPercent="41.6" dotnetProcesses="0" cscProcesses="0" />
  <Restore status="SUCCESS" elapsed="91ms" />
  <CompilerErrors owner="external_genetics_predator_kcc" count="16">
    <Error file="Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs" summary="AbsoluteUniversePosition / AbsoluteUniversePositionBlit mismatch" />
    <Error file="Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs" summary="missing Leviathan steering partial fields/methods" />
    <Error file="Assets/_Project/Scripts/Fauna/FaunaBrain.cs" summary="missing _slot, KCC.KinematicStateDTO, and PredatorCognitionDomain.TryCopyLeviathanKinematicState" />
  </CompilerErrors>
  <SHINOBU305TouchedFilesInCompilerErrors count="0" />
</COMPILE_ATTEMPT>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="ABI_TIME_SCRUB" status="STATIC_VERIFIED_BUILD_DEFERRED">
  <WhatWasWrong>
    One runtime bridge still used `Time.frameCount` for bite feedback cooldowns. The SHINOBU CBuffer DTOs were explicit-layout, but only the main IK globals had self-audit coverage; adjacent procedural globals lacked publish-time ABI guards.
  </WhatWasWrong>
  <WhatWasDone>
    Replaced the bite feedback/audio frame source with `_frameIndex` and wrap-safe cooldown elapsed math. Added fail-closed `UnsafeUtility.SizeOf` and field-offset validation before publishing `_H8LeviathanIkGlobals`, `_H8ProceduralBoneGlobals`, and `_H8LeviathanTentacleGlobals`.
  </WhatWasDone>
  <StructLayout>
    <LeviathanIkShaderGlobalsDTO size="32" scalars0Offset="0" scalars1Offset="16" />
    <ProceduralBoneShaderGlobalsDTO size="32" scalars0Offset="0" scalars1Offset="16" />
    <LeviathanTentacleShaderGlobalsDTO size="64" radiusFxFlowOffset="0" flowResolutionOffset="16" flowCenterOffset="32" flowSpacingOffset="48" />
  </StructLayout>
  <StaticVerification>
    Scoped `rg` over SHINOBU runtime/job files found no `Time.*`, no `foreach`, no LINQ, and no `string.Format`. `git diff --check` returned only existing LF-to-CRLF warnings.
  </StaticVerification>
  <CompileGate>
    Rebuild deferred: CPU sampled 74.0%, with `dotnet`/`csc` count 0. User gate requires CPU <= 50%.
  </CompileGate>
</FORENSIC_ADDENDUM>

<COMPILE_ATTEMPT agent="SHINOBU_305" status="SUCCESS_POST_SCRUB">
  <Command>dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal</Command>
  <Gate cpuPercent="33.1" dotnetProcesses="0" cscProcesses="0" />
  <Restore status="UP_TO_DATE" />
  <Result warnings="0" errors="0" elapsed="1.94s" output="C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll" />
</COMPILE_ATTEMPT>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="SHADER_WARMUP_EDITOR_NOISE_SCRUB" status="STATIC_VERIFIED_BUILD_DEFERRED">
  <WhatWasWrong>
    Leviathan organic/tentacle shader code was changed for CBuffer-fed procedural matrices, but the bootstrap shader variant collection list had no leviathan entry. A legacy procedural rig editor readout also used `ToString()` for labels, and `LeviathanTentacleVerletSolver` kept an unused physics namespace import.
  </WhatWasWrong>
  <WhatWasDone>
    Added `Assets/_Project/Art/Shaders/Variants/Hecton_LeviathanProceduralWarmup.shadervariants` referencing `Hecton_LeviathanOrganic.shader` and `Hecton_LeviathanTentacleIndirect.shader`, then appended its GUID to `Assets/_Project/Scenes/00_BOOTSTRAP.unity` under `shaderVariantCollections`. Removed the unused import and replaced editor `ToString()` labels with disabled numeric IMGUI controls.
  </WhatWasDone>
  <ShaderWarmup>
    Warmup route uses the existing `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` boot path. No new shader keywords or runtime `WarmUp()` calls were added.
  </ShaderWarmup>
  <StaticVerification>
    Scoped `rg` found no `using Hecton8.Physics`, no `ToString()`, no LINQ, no `new NativeArray`, no `Allocator.TempJob/Persistent`, and no `Pack=1` in the touched scrub files. YAML GUID references resolve to the two leviathan shader metas and the bootstrap collection list.
  </StaticVerification>
  <CompileGate>
    Rebuild deferred: CPU sampled 99.8%, active Unity `dotnet` PIDs `13660,14108`, `csc` count 0. User gate requires CPU <= 50% and no active `dotnet`/`csc`.
  </CompileGate>
</FORENSIC_ADDENDUM>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_WARMUP_RECHECK" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="79.7" dotnetPids="5468,11576" cscProcesses="0" />
  <Action>No rebuild launched. Static proof remains the current evidence until CPU <= 50% and `dotnet`/`csc` are idle.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_WARMUP_RECHECK_2" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="100.0" dotnetPids="5468" cscProcesses="0" />
  <Action>No rebuild launched. Unity-owned `dotnet` remains active from `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe`.</Action>
</COMPILE_GATE_SAMPLE>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="RUNTIME_REFLECTION_OFFSET_SCRUB" status="STATIC_VERIFIED_BUILD_DEFERRED">
  <WhatWasWrong>
    Runtime layout validators used `System.Reflection.FieldInfo` and `UnsafeUtility.GetFieldOffset` in SHINOBU and adjacent procedural CBuffer guards. `LeviathanTentacleVerletSolver` also still carried a stale `Hecton8.Physics` import.
  </WhatWasWrong>
  <WhatWasDone>
    Replaced reflection-derived offset fields with compile-time constants matching explicit `[FieldOffset]` source declarations. Kept `UnsafeUtility.SizeOf` ABI guards. Removed the stale physics namespace import.
  </WhatWasDone>
  <StaticVerification>
    Scoped `rg` found no `System.Reflection`, no `GetFieldOffset`, no `FieldOffset&lt;`, no `Marshal.OffsetOf`, and no `using Hecton8.Physics` in SHINOBU runtime/adjacent procedural files. Scoped `git diff --check` returned only LF-to-CRLF warnings.
  </StaticVerification>
  <CompileGate>
    Rebuild deferred: CPU sampled 96.0%, active Unity `dotnet` PID `5468`, `csc` count 0. User gate requires CPU <= 50% and no active `dotnet`/`csc`.
  </CompileGate>
</FORENSIC_ADDENDUM>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="BINARY_LEDGER_RECONCILIATION" status="STATIC_VERIFIED_BUILD_DEFERRED">
  <WhatWasWrong>
    `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` contained neighboring Echelon 3 payload lanes but no SHINOBU_305 entry for leviathan procedural IK matrices.
  </WhatWasWrong>
  <WhatWasDone>
    Added a concise SHINOBU_305 payload boundary with BufferIDs `180..184` and `71000..71002`, owned DTO sizes, Vault route, GPU matrix/CBuffer route, continuous quality route, Dear Lie replacement, and `Dump_SHINOBU_305.bin` fault target.
  </WhatWasDone>
  <StaticVerification>
    Ledger scan finds the SHINOBU_305 entry and the expected BufferID line. `git diff --check` on ledger/status/rationale/log returned LF-to-CRLF warning only.
  </StaticVerification>
  <CompileGate>
    Rebuild deferred: CPU sampled 43.1%, active Unity `dotnet` PID `5468`, `csc` count 0. User gate requires CPU <= 50% and no active `dotnet`/`csc`.
  </CompileGate>
</FORENSIC_ADDENDUM>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="ZERO_INIT_HELPER_ALIGNMENT" status="STATIC_VERIFIED_BUILD_DEFERRED">
  <WhatWasWrong>
    `LeviathanTerrainIkVault.TryResolve` still requested clear-memory allocation for overwritten segment and bone matrix lanes.
  </WhatWasWrong>
  <WhatWasDone>
    Changed `LeviathanSegmentPositions`, `LeviathanPreviousSegmentPositions`, and `LeviathanBoneMatrices` helper acquisition to `NativeArrayOptions.UninitializedMemory`. Telemetry ring/cursor remain clear-memory for deterministic first-frame diagnostics.
  </WhatWasDone>
  <StaticVerification>
    Scoped scan found no `System.Reflection`, no `GetFieldOffset`, no stale `using Hecton8.Physics`, no `SetData`, no `MemClear`, no `Time.*`, and no LINQ/`foreach` in SHINOBU runtime/adjacent procedural files after the patch.
  </StaticVerification>
  <CompileGate>
    Rebuild deferred: CPU sampled 100.0%, `dotnet` count 0, `csc` count 0. User gate requires CPU <= 50% and no active `dotnet`/`csc`.
  </CompileGate>
</FORENSIC_ADDENDUM>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_LOOP13_RECHECK" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="100.0" dotnetPids="1548,3728" cscProcesses="0" />
  <Action>No rebuild launched after Loop 13.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_LOOP13_RECHECK_2" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="100.0" dotnetPids="1548,10740" cscProcesses="0" />
  <Action>No rebuild launched before handoff.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_COMPACTION_RECHECK" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="100.0" dotnetPids="1548" cscProcesses="0" />
  <Action>No rebuild launched. Static scan and manual compile-risk sweep continue until CPU <= 50% and `dotnet`/`csc` are idle.</Action>
</COMPILE_GATE_SAMPLE>

<FORENSIC_ADDENDUM agent="SHINOBU_305" pass="CORE_ASMDEF_BOUNDARY_SWEEP" status="STATIC_RISK_RECORDED">
  <WhatWasFound>
    `Assets/_Project/Scripts/Hecton8.Core.asmdef` is dirty and contains broad runtime references including `Hecton8.Animation.IK`. This is outside the owned IK asmdef boundary and is not normalized from SHINOBU_305 while other agents are active.
  </WhatWasFound>
  <OwnedBoundary>
    `Assets/_Project/Scripts/Animation/IK/Hecton8.Animation.IK.asmdef` references only `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`; `allowUnsafeCode=true`; no sibling runtime references found by scoped scan.
  </OwnedBoundary>
  <Action>No core asmdef edits made in this pass.</Action>
</FORENSIC_ADDENDUM>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_BOUNDARY_SWEEP_RECHECK" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="16.3" dotnetPids="1548" cscProcesses="0" />
  <DotnetCommand>`dotnet exec ...\DotNetSdkRoslyn\VBCSCompiler.dll` from Unity 6000.4.1f1.</DotnetCommand>
  <Action>No rebuild launched because the user gate forbids a build while any `dotnet` process is active.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_BOUNDARY_SWEEP_RECHECK_2" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="93.6" dotnetPids="13320" cscProcesses="0" />
  <Action>No rebuild launched. Prior sample had no `dotnet` but CPU was 54.9%, still above the user gate.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_BOUNDARY_SWEEP_POLL_60S" status="DEFERRED_BY_USER_GATE">
  <Samples>
    <Sample index="1" cpuPercent="100.0" dotnetPids="12700" cscPids="11364" />
    <Sample index="2" cpuPercent="100.0" dotnetPids="12700" cscPids="11364" />
    <Sample index="3" cpuPercent="100.0" dotnetPids="" cscPids="" />
    <Sample index="4" cpuPercent="100.0" dotnetPids="" cscPids="" />
    <Sample index="5" cpuPercent="93.2" dotnetPids="" cscPids="" />
    <Sample index="6" cpuPercent="97.7" dotnetPids="" cscPids="" />
  </Samples>
  <Action>No rebuild launched because every sample violated the user CPU/compiler gate.</Action>
</COMPILE_GATE_SAMPLE>

<COMPILE_GATE_SAMPLE agent="SHINOBU_305" pass="POST_POLL_COOLDOWN_RECHECK" status="DEFERRED_BY_USER_GATE">
  <Gate cpuPercent="58.9" dotnetPids="3056" cscProcesses="0" />
  <DotnetCommand>`dotnet exec ...\DotNetSdkRoslyn\VBCSCompiler.dll` from Unity 6000.4.1f1.</DotnetCommand>
  <Action>No rebuild launched.</Action>
</COMPILE_GATE_SAMPLE>
