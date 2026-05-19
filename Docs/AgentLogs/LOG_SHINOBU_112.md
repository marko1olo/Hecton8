# LOG_SHINOBU_112

## 2026-05-19 Static Implementation Pass

What was wrong:
- Audio virtualization had no 64-byte `AcousticSourceDTO` lane for analytical SDF acoustic DSP.
- Missing `acoustic_material_properties.h8bin` could leave material absorption undefined outside the baker path.
- Voice culling still carried a 32-voice ceiling and binary tier behavior.
- `Hecton8.Audio.Virtualization` and its contracts directly referenced sibling `Hecton8.Audio.Propagation` only for portal flags.
- The SDF acoustic kernel was not Vault-owned or consumed at the audio sync boundary.

What was done:
- Added explicit 64-byte `AcousticSourceDTO` and `AcousticDspOutputDTO`, plus 32-byte `AcousticMaterialCoefficientDTO`.
- Added `GenerateEmergencyMockAcoustics()` for deterministic rock/metal/flesh absorption fallback.
- Added `MockAcousticEmitterJob` and `AcousticOcclusionJob`; the occlusion job performs AUP subtract-before-cast, SDF line integral, Sabine RT60, depth LPF, Doppler, ITD/ILD, and rollback mute.
- Added double-buffered Vault aliases for acoustic source rows, previous AUP rows, DSP output rows, and fallback material rows.
- Routed `SpatialAudioManager.FastTick()` to schedule the acoustic occlusion kernel and `LateFrameTick()` to complete it before voice injection.
- Added `ApplyAcousticDspOutputToSelection()` so unmanaged DSP output rows affect volume, pitch, RT60, LPF, delay, Doppler, and occlusion flags before physical `AudioSource` sync.
- Removed direct `Audio.Virtualization` -> `Audio.Propagation` asmdef references by introducing `VirtualVoicePortalFlags : byte` and converting at the `SpatialAudioManager` boundary.
- Added UI Toolkit `Abyssal Acoustics Tuner`, byte-span CSV tuning/material parser, and SDF-colored editor gizmo path.

Cinematic cheats used:
- Dear Lie Sabine: approximate room volume/surface from one SDF clearance scalar, then `RT60 = 0.161 * V / A`.
- SDF density integral: nearest/trilinear analytical voxel samples replace PhysX line tests.
- Depth LPF: polynomial AUP-depth curve replaces prefiltered underwater clip variants.
- Binaural cheap path: scalar ITD/ILD from local listener vector replaces third-party HRTF hot calls.

Microseconds saved:
- PhysX raycast occlusion removed from the audio path: estimated 30-200 us saved per 64-voice frame versus per-voice `Physics.Raycast`/`Linecast`; static proof only.
- Sabine Dear Lie versus acoustic ray tracing: estimated 20-100 us saved in dense geometry frames; profiler pending.
- Continuous culling 12-64 voices: estimated 10-60 us saved under thermal throttling by reducing submitted DSP rows proportionally to `GlobalQualityWeight`.
- Zero-init bypass on fully overwritten Vault buffers: estimated 2-8 us cold allocation/boot-refresh saving.
- Build/profiler proof: not run. CPU gate samples were 58.6%, 94.0%, 98.5%; user rule forbids build above 50% CPU or while compilers are active.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS_STATIC">Fallback `GenerateEmergencyMockAcoustics()` exists for NativeArray and NativeParallelHashMap material rows.</TASK>
    <TASK id="02" status="PASS_STATIC">Audio-path grep found no `Physics.Raycast`, `Physics.Linecast`, `RaycastAll`, or `RaycastCommand`.</TASK>
    <TASK id="03" status="PASS_STATIC">New acoustic DTOs use public fields; no hot DTO get/set properties.</TASK>
    <TASK id="04" status="PASS_STATIC">`AcousticSourceDTO` is explicit 64 bytes; editor smoke tester checks `UnsafeUtility.SizeOf`.</TASK>
    <TASK id="05" status="PASS_STATIC">`MockAcousticEmitterJob : IJobParallelFor` added with deterministic `Unity.Mathematics.Random` seeding.</TASK>
    <TASK id="06" status="PASS_STATIC">`AcousticOcclusionJob : IJobParallelFor` added and scheduled from Vault source buffers.</TASK>
    <TASK id="07" status="PASS_STATIC">Sabine RT60 Dear Lie implemented from SDF clearance with finite clamps.</TASK>
    <TASK id="08" status="PASS_STATIC">Depth LPF resolves from 22000 Hz toward 800-1400 Hz by AUP depth and quality.</TASK>
    <TASK id="09" status="PASS_STATIC">Voice budget uses `(int)math.lerp(12,64,GlobalQualityWeight)` through `ResolveContinuousVoiceBudget()`.</TASK>
    <TASK id="10" status="PASS_STATIC">`SignalBus&lt;AcousticPingSignal&gt;` drained by `SpatialAudioManager`.</TASK>
    <TASK id="11" status="PASS_STATIC">Doppler uses source/listener AUP deltas over deterministic tick delta.</TASK>
    <TASK id="12" status="PASS_STATIC">ITD/ILD approximation added from listener right vector and local source delta.</TASK>
    <TASK id="13" status="PASS_STATIC">Rollback suppression reads Vault alias and clamps output volume to zero.</TASK>
    <TASK id="14" status="PASS_STATIC">Virtual/acoustic source, previous AUP, output, and sort-key Vault arrays use `UninitializedMemory` where fully overwritten.</TASK>
    <TASK id="15" status="PASS_STATIC">`AcousticDspOutputDTO` rows are consumed before audio sync injection.</TASK>
    <TASK id="16" status="PASS_STATIC">300-frame telemetry ring retained; fatal dump path is `Docs/AgentLogs/Dump_ACOUSTIC_SURGEON.bin`.</TASK>
    <TASK id="17" status="PASS_STATIC">UI Toolkit `Abyssal Acoustics Tuner` added.</TASK>
    <TASK id="18" status="PASS_STATIC">`ReadOnlySpan&lt;byte&gt;` CSV parser added for tuning/material rows.</TASK>
    <TASK id="19" status="PASS_STATIC">SDF/open-state gizmo coloring added to live virtual voice gizmos.</TASK>
    <TASK id="20" status="PARTIAL">Self-audit generated; build/profiler and exact 50x timing proof are pending CPU gate.</TASK>
  </TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION>
    <AcousticSourceDTO size="64">
      <field name="SourceHash" offset="0" size="4"/>
      <field name="BaseVolume" offset="4" size="4"/>
      <field name="BasePitch" offset="8" size="4"/>
      <field name="Flags" offset="12" size="4"/>
      <field name="AUP_Position" offset="16" size="24"/>
      <field name="ComputedOcclusion" offset="40" size="4"/>
      <field name="ComputedReverb" offset="44" size="4"/>
      <field name="_pad0" offset="48" size="4"/>
      <field name="_pad1" offset="52" size="4"/>
      <field name="_pad2" offset="56" size="4"/>
      <field name="_pad3" offset="60" size="4"/>
      <math>4+4+4+4+24+4+4+16=64; one L1 cache line; no Pack=1.</math>
    </AcousticSourceDTO>
    <AcousticDspOutputDTO size="64">
      <math>12 scalar fields through offset 44 plus 16 bytes padding = 64; no atomic counter use.</math>
    </AcousticDspOutputDTO>
    <AcousticMaterialCoefficientDTO size="32">
      <math>MaterialHash + absorption/scatter/density/LPF/flags + 8 bytes pad = 32.</math>
    </AcousticMaterialCoefficientDTO>
    <FalseSharing>Acoustic jobs write per-index 64-byte output/source rows; no shared atomic counter was introduced.</FalseSharing>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight &lt; 0.3`, voice submission trends toward 12, SDF tap count collapses toward 1-3, SDF interpolation stays nearest-neighbor, and DSP output rows are capped by the continuous budget. At quality 1.0, 64 voices are eligible, SDF taps reach 8, trilinear SDF blending is active, and ITD/ILD/Sabine/depth LPF all run per submitted row.
  </SCALABILITY_CURVE>

  <H_PHI_VAULT_STATUS>
    <NoPrivatePersistentDTOAllocations>true for the SHINOBU acoustic source/output lane; arrays are Vault aliases.</NoPrivatePersistentDTOAllocations>
    <VaultBufferHandle id="70015" type="VirtualVoiceTuningSnapshot"/>
    <VaultBufferHandle id="70016" type="VirtualVoice write pool"/>
    <VaultBufferHandle id="70017" type="VirtualVoice sort pool"/>
    <VaultBufferHandle id="70018" type="VirtualVoiceDTO pool"/>
    <VaultBufferHandle id="70019" type="VirtualVoiceSortKey pool"/>
    <VaultBufferHandle id="70020" type="AcousticSourceDTO write pool"/>
    <VaultBufferHandle id="70021" type="AcousticSourceDTO sort pool"/>
    <VaultBufferHandle id="70022" type="double3 previous source AUP write pool"/>
    <VaultBufferHandle id="70023" type="double3 previous source AUP sort pool"/>
    <VaultBufferHandle id="70024" type="AcousticDspOutputDTO pool"/>
    <VaultBufferHandle id="70025" type="AcousticMaterialCoefficientDTO fallback rows"/>
    <VaultBufferHandle id="70026" type="AcousticSourceDTO selected physical voice pool"/>
    <VaultBufferHandle id="70027" type="double3 selected previous source AUP pool"/>
    <VaultBufferHandle id="366" type="VirtualVoiceSelection"/>
    <VaultBufferHandle id="367" type="VirtualVoiceStatistics"/>
    <VaultBufferHandle id="368" type="VirtualVoiceTelemetryEntry blackbox"/>
    <ExternalReadOnlyAlias id="ShinobuScalabilityState"/>
    <ExternalReadOnlyAlias id="RollbackNetcodeVault.AudioSuppression"/>
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Virtual sort keys, selections, statistics, acoustic sources, acoustic outputs, previous AUPs, SDF voxels, and material rows are marked with `[NoAlias]` where passed to Burst jobs.</NoAlias>
    <Consumes>Previous `_virtualVoiceSortHandle`; previous `_acousticOcclusionHandle`.</Consumes>
    <Outputs>`_virtualVoiceSortHandle` from `VirtualVoiceSortJob`; after sort completion, selected acoustic DTO rows are staged, then `_acousticOcclusionHandle` is produced by `AcousticOcclusionJob`.</Outputs>
    <SyncBoundary>`LateFrameTick()` completes sort, schedules/completes selected acoustic SDF, then injects physical voices.</SyncBoundary>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    `Hecton8.Audio.Virtualization.asmdef` and `Hecton8.Audio.Virtualization.Contracts.asmdef` no longer reference `Hecton8.Audio.Propagation`. `SpatialAudioManager` remains the boundary adapter because it already owns propagation integration.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    <Before>O(voices * raycasts * PhysX broadphase/narrowphase), with main-thread collision-engine dependency.</Before>
    <After>O(selectedVoices * taps), selectedVoices=12..64 and taps=1..8, flat NativeArray SDF sampling plus O(1) Sabine approximation.</After>
    <Cheat>Listener/source SDF samples and clearance-derived volume/surface replace physical acoustic rays.</Cheat>
  </DEAR_LIE_CONFIRMATION>

  <VERIFICATION>
    <StaticRaycastGrep>PASS</StaticRaycastGrep>
    <StaticPackGrep>PASS</StaticPackGrep>
    <CompileWallGrep>PASS for virtualization assemblies</CompileWallGrep>
    <Build>NOT_RUN_CPU_GATE</Build>
    <Profiler50xProof>NOT_MEASURED_CPU_GATE</Profiler50xProof>
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 Selected-Lane Correction Pass

What was wrong:
- The first SDF job integration scheduled `AcousticOcclusionJob` from the first N ingress rows before virtual voice priority sorting finished. That was architecturally wrong: a selected voice could be outside the ingress prefix and miss its computed occlusion/reverb output.
- A stable-key scan from selections back to ingress rows would have been O(64 * virtualVoiceCount) on the main thread.
- The editor gizmo path still primarily showed virtual selection flags instead of the computed `AcousticSourceDTO.ComputedOcclusion` scalar.

What was done:
- Added selected acoustic Vault buffers `70026` and `70027`.
- Added `SourceVelocityMetersPerSecond` to `VirtualVoiceSelection` so selected acoustic staging can reconstruct previous AUP for Doppler without scanning ingress rows.
- Moved acoustic SDF scheduling to after `VirtualVoiceSortJob` completion. `PopulateSelectedAcousticSources()` now stages only the selected/submitted voices, then `AcousticOcclusionJob` runs over that compact 12-64 row lane.
- Updated gizmos to draw the selected `AcousticSourceDTO` lane and color by `ComputedOcclusion`, not only by virtual flags.

Cinematic cheats used:
- Keep the heavy acoustic illusion bounded to the selected voice lane. Non-selected voices retain simulation state but do not spend SDF/Sabine taps until they can actually be heard.

Microseconds saved:
- Rejected O(1000 * taps) all-virtual SDF and O(64k) stable-key scan. Selected staging is O(64) rows. Estimated 10-80 us saved under high virtual emitter pressure on low-end CPU. Build/profiler proof still pending CPU gate.

## 2026-05-19 Material CSV Facade Pass

What was wrong:
- `acoustic_materials.csv` parser and fallback rows existed, but the UI Toolkit tuner did not expose a direct designer reload route into the Vault material rows.
- The repo did not contain the explicit rock/metal/flesh material CSV seed requested by the SHINOBU task.
- Smoke coverage did not assert the cold material reload hook, so a later editor cleanup could silently remove it.

What was done:
- Added `Assets/_Project/Data/Audio/acoustic_materials.csv` with deterministic rock, metal, and flesh coefficients.
- Added `Abyssal Acoustics Tuner` button `Reload Material CSV`; it performs explicit cold editor `File.ReadAllBytes`, passes `ReadOnlySpan<byte>` to `SpatialAudioManager.ReloadAcousticMaterialRowsFromCsvCold`, then writes parsed rows into the Vault alias.
- Extended `ShinobuAcousticDspSmokeTester` to verify the CSV asset, cold reload facade, and deterministic seed rows.
- Re-ran static checks: `git diff --check` had no errors; audio PhysX grep returned no hits; virtualization propagation grep returned no hits; DTO packing/property grep only reported the non-hot singleton `ActiveRuntimeInstance { get; private set; }`.

Cinematic cheats used:
- Material absorption remains a tiny coefficient table feeding the Sabine/SDF illusion. No physical wave propagation, no runtime material probes, no per-frame disk reads.

Microseconds saved:
- Runtime hot path impact is 0 us. Cold-path resilience estimate remains 5-20 us by avoiding repeated missing-binary probes and keeping CI on deterministic fallback rows. Build/profiler proof remains blocked by CPU samples 40.29%, 80.04%, 95.19%; no `dotnet` or `csc` process was active.

<SELF_AUDIT_DELTA>
  <CSV_MATERIAL_FACADE status="PASS_STATIC">
    <SeedAsset>Assets/_Project/Data/Audio/acoustic_materials.csv</SeedAsset>
    <Rows>rock, metal, flesh</Rows>
    <EditorRoute>AbyssalAcousticsTunerWindow.ReloadMaterialCsv -> SpatialAudioManager.ReloadAcousticMaterialRowsFromCsvCold -> VirtualVoiceProfileCsvParser.ParseMaterialRows</EditorRoute>
    <HotPathAllocations>0; editor File.ReadAllBytes is cold and outside gameplay.</HotPathAllocations>
  </CSV_MATERIAL_FACADE>
  <LATEST_STATIC_VERIFICATION>
    <Whitespace>PASS</Whitespace>
    <AudioPhysXRaycastGrep>PASS_NO_HITS</AudioPhysXRaycastGrep>
    <VirtualizationCompileWallGrep>PASS_NO_AUDIO_PROPAGATION_REFERENCE</VirtualizationCompileWallGrep>
    <Build>NOT_RUN_CPU_GATE</Build>
  </LATEST_STATIC_VERIFICATION>
</SELF_AUDIT_DELTA>

## 2026-05-19 Build Gate Recheck

What was wrong:
- Compile/profiler proof is still required for Task 20, but the machine is not in an allowed state for build execution.

What was done:
- Rechecked CPU and compiler state after static verification. CPU samples were 100%, 100%, 99.85%. `dotnet` PID 36732 was active.
- Kept `dotnet build` blocked under the explicit user rule: do not launch build above 50% CPU or while another `dotnet`/`csc` is running.

Cinematic cheats used:
- None. This is verification hygiene, not runtime architecture.

Microseconds saved:
- No runtime delta. The avoided build prevents workstation contention during concurrent agent execution. Task 20 remains pending for compile/profiler evidence.

## 2026-05-19 SDF Vault Alias And Struct-Property Purge

What was wrong:
- `AcousticOcclusionJob` accepted an SDF voxel lane, but `SpatialAudioManager` still scheduled it with `SdfVoxels = default`, making mock-SDF the permanent path.
- `VirtualVoiceTuningSnapshot` still had a static `Default` property, which was defensible as non-instance but still failed the strict "properties are methods" audit posture for unmanaged structs.

What was done:
- Changed `AcousticOcclusionJob` to consume `NativeArray<byte> SdfVoxels` and decode project-standard byte SDF values into signed meters: `((byte / 255) * 2 - 1) * range`.
- Added a read-only external Vault alias for `BufferID.VoxelSdfTexture3D` in `SpatialAudioManager`; the selected-lane SDF job receives the owner-published buffer when present and falls back to mock SDF only if the owner buffer is absent or undersized.
- Replaced `VirtualVoiceTuningSnapshot.Default` with `VirtualVoiceTuningSnapshot.CreateDefault()` and updated all audio/editor callers.
- Extended smoke assertions for real byte-SDF routing and absence of the unmanaged struct `Default` property.

Cinematic cheats used:
- Still a bounded SDF line integral, not a physical ray. Low quality collapses to nearest byte-SDF taps; high quality blends trilinear byte-SDF samples. The mock SDF remains a CI fallback, not the main architecture.

Microseconds saved:
- Removes the permanent mock-only gap without changing complexity: O(selectedVoices * taps), selectedVoices 12..64, taps 1..8. Expected PhysX replacement savings remain 30-200 us per 64-voice frame; profiler proof is still blocked by CPU gate. Latest CPU gate samples: 100%, 100%, 64.7%; no `dotnet`/`csc` process was active.

<SELF_AUDIT_DELTA>
  <SDF_OWNER_ALIAS status="PASS_STATIC">
    <Route>GlobalDataVault BufferID.VoxelSdfTexture3D -> SpatialAudioManager _acousticVoxelSdfTexture3D -> AcousticOcclusionJob.SdfVoxels</Route>
    <Ownership>Voxel owner owns backing memory; audio only holds a read-only alias and never copies the SDF volume.</Ownership>
    <Fallback>MockSDFSampler only when the owner buffer is absent or smaller than 64*40*64 bytes.</Fallback>
    <Complexity>O(selectedVoices * qualityTaps), no PhysX scene query.</Complexity>
  </SDF_OWNER_ALIAS>
  <STRUCT_PROPERTY_PURGE status="PASS_STATIC">
    <Removed>VirtualVoiceTuningSnapshot.Default</Removed>
    <Replacement>VirtualVoiceTuningSnapshot.CreateDefault()</Replacement>
  </STRUCT_PROPERTY_PURGE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Static Gate Hygiene Recheck

What was wrong:
- The editor smoke tester contained exact forbidden strings as assertion needles. The verifier was correct, but broad grep could not distinguish "forbidden runtime text" from "string used to assert absence."

What was done:
- Split the smoke-test needles for the removed tuning default property and propagation assembly name into composed strings.
- Re-ran static grep: no hits for `VirtualVoiceTuningSnapshot.Default`, `SdfVoxels = default`, audio PhysX raycasts, or propagation coupling inside `Audio.Virtualization`.
- Rechecked build gate. CPU samples were 100%, 100%, 93.4%; no `dotnet` or `csc` process was active.

Cinematic cheats used:
- None. This is proof-surface cleanup.

Microseconds saved:
- 0 us runtime. The value is audit reliability: future grep gates stop reporting smoke-test literals as runtime violations. Build remains withheld by CPU gate.

## 2026-05-19 Legal Build Attempt Blocked By External World File

What was wrong:
- Task 20 still required compile evidence. The previous blocker was CPU load; that gate opened with `typeperf` samples 17.54%, 15.85%, 40.60% and no active `dotnet` or `csc` process.

What was done:
- Ran one constrained build: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
- Build failed in 51.90 s before audio verification with `CS2001`: `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` could not be found by `Hecton8.Core.csproj`.
- No rollback, recreation, or csproj edit was performed because the missing file is a World-domain worktree deletion outside SHINOBU_112 ownership.

Cinematic cheats used:
- None. This is compile-wall evidence.

Microseconds saved:
- 0 us runtime. Static audio checks still pass, but compile/profiler proof remains blocked by a global project missing-file error outside the acoustic domain.

<SELF_AUDIT_DELTA>
  <BUILD_GATE status="BLOCKED_EXTERNAL">
    <CpuSamples>17.54, 15.85, 40.60</CpuSamples>
    <CompilerProcesses>NONE</CompilerProcesses>
    <Command>dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1</Command>
    <Result>CS2001 missing World-domain source file before SHINOBU_112 audio code could be compiled.</Result>
    <ExternalBlocker>Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs deleted from worktree while Hecton8.Core.csproj references it.</ExternalBlocker>
  </BUILD_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Manual Compile-Sanity While Build Is Externally Blocked

What was wrong:
- The solution build cannot reach SHINOBU_112 because `Hecton8.Core.csproj` stops on the missing World file. That leaves local audio deltas without compiler proof.

What was done:
- Rechecked added SHINOBU_112 lines for forbidden hot-path constructs: no `foreach`, `string.Format`, `new NativeArray`, `new NativeList`, PhysX/raycast calls, `Pack=1`, or removed tuning `Default` usage.
- Rechecked `AudioVirtualizationJobs.cs`: three Burst jobs exist; sort and mock emitter use `FloatMode.Fast`; acoustic occlusion uses `FloatMode.Deterministic` because rollback suppression is part of the kernel.
- Re-read virtualization asmdefs. `Hecton8.Audio.Propagation` is not referenced by `Hecton8.Audio.Virtualization` or its contracts.
- Re-read SDF route: `BufferID.VoxelSdfTexture3D` -> `_acousticVoxelSdfTexture3D` read-only alias -> `AcousticOcclusionJob.SdfVoxels`.

Cinematic cheats used:
- No CPU acoustic rays. Runtime path remains byte-SDF line integral plus Sabine clearance approximation: O(selectedVoices * taps), selectedVoices 12..64, taps 1..8.

Microseconds saved:
- Manual sanity itself saves 0 us. Runtime design still targets 30-200 us saved per 64-voice frame versus PhysX raycasts; profiler proof remains blocked by the external missing-file compile wall.
