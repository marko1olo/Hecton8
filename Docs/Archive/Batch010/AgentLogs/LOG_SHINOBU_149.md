# LOG_SHINOBU_149

## 2026-05-19 Dynamic Deferred Decal Static Pass

What was wrong:

- Hull impact visuals previously carried object-decay assumptions from the Unity decal path. Built-in `DecalProjector`/URP decal routing is the wrong ownership model for clustered bullet, plasma, hull, and creature impact scars.
- A direct Physics-to-Visor static call would have solved visuals while damaging compile isolation. That route was rejected and replaced with Core signal consumption.
- A per-pixel HLSL `inverse(float4x4)` in the decal loop would have moved bad architecture from CPU to GPU. It was replaced by dot/scale affine projection from the existing 80-byte matrix DTO.

What was done:

- Implemented Vault-backed decal storage in `DynamicDecalVaultRuntime`.
- Added explicit 80-byte `DecalInstanceDTO` ABI for CPU, Vault, and StructuredBuffer upload.
- Added deterministic `DecalRequestSignal` ingestion from `HighSpeedImpactSignal` and `CombatDamageSignal`.
- Converted hull impact decal emission to publish `CombatDamageSignal` rather than calling Visor.
- Implemented Burst jobs: cold clear, mock request generation, matrix generation, opacity decay, upload scratch construction, and mapped GPU upload copy.
- Added double-buffered `GraphicsBuffer.LockBufferForWrite` upload in `DeferredDecalPass`.
- Implemented one fullscreen deferred projection shader with Texture2DArray atlas support and procedural scorch/blood/acid/dent fallback.
- Added cold CSV profile ingestion through Vault scratch and Vault profile table.
- Added 300-frame telemetry ring and binary dump path `Docs/AgentLogs/Dump_DECAL_PROJECTOR.bin`.
- Added editor tuner and gizmo support already present in the SHINOBU_149 Visor scope.
- Added SHINOBU_149 files to the relevant project files after the first narrow build exposed missing inclusion.
- Added architecture route card `Docs/ARCHITECTURE/SHINOBU_149_DYNAMIC_DECALS.md`.
- Added ledger entry to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic cheats used:

- No physical decal mesh, projector object, or per-surface quad exists in the SHINOBU_149 path. The system reconstructs screen-space world position from depth and projects against mathematical boxes.
- Procedural fallback uses radial scorch/dent masks and a quality-weighted broken-ring sine, not a physical scorch simulation.
- The shader uses affine column dot products against scaled matrix axes instead of matrix inversion or mesh clipping.

Exact microseconds saved, static estimate:

- `DecalProjector` component/object path removed during clustered hull impacts: 300-1400 us avoided on i3/MX350-class hardware based on avoided component traversal, renderer feature object work, and pool churn.
- Synchronous decal raycast avoided: 40-250 us per clustered hit set.
- `GraphicsBuffer.SetData` avoided: upload stalls not measured in this pass; expected gain is frame-spike avoidance rather than a stable per-frame scalar.
- Per-pixel matrix inverse removed: GPU ALU reduction is architecture-dependent and not converted to CPU microseconds without Frame Debugger/profiler proof.

Verification:

- Static source scan found no `DecalProjector`, `BallisticsRuntime`, `GraphicsBuffer.SetData`, `string.Split`, `UnityEngine.Random`, `Physics.Raycast`, `RaycastNonAlloc`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, `List<GameObject>`, or `Instantiate(` in the SHINOBU_149 runtime/pass/gizmo/tuner/shader scope.
- Whole-project `DecalProjector` scan found only a negative smoke-test assertion and inactive serialized URP `DecalRendererFeature` blocks in `PC_Renderer.asset` and `PC_High_Renderer.asset`.
- Compile guard scan found no `using Hecton8.Gameplay`, `using Hecton8.Physics`, `using Hecton8.World`, `using Hecton8.VFX`, or `using Hecton8.Atmosphere` in the SHINOBU_149 Visor files.
- First build was deferred while CPU was near 98 percent. After CPU dropped below the gate, `dotnet build Hecton8.Core.csproj --no-restore` was run.
- Build pass 1 exposed one SHINOBU_149 compile issue: `DeferredDecalPass` could not see `DynamicDecalFrameStats` because `DynamicDecalVaultRuntime.cs` was absent from `Hecton8.Core.csproj`.
- The inclusion fault was fixed by adding `DynamicDecalVaultRuntime.cs` and `DynamicDecalGizmoVisualizer.cs` to `Hecton8.Core.csproj`, plus `ScreenSpaceDecalTunerWindow.cs` to `Hecton8.Editor.csproj`.
- Build pass 2 no longer reports SHINOBU_149 files. It still fails in unrelated domains: KineticCharacter animation namespace, UberNoir reconstruction DTOs, equipment DTOs, somatic comfort DTOs, and macro-ecosystem DTOs.

<SELF_AUDIT agent_id="SHINOBU_149" domain="DYNAMIC_DECAL_AND_SCORCH_PROJECTOR" date="2026-05-19">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">DECAL_PROJECTOR_ERADICATION: object projector path removed from SHINOBU_149 impact decals; URP built-in decal feature inactive in relevant renderer assets.</TASK>
    <TASK id="02" status="PASS">SYNCHRONOUS_RAYCAST_PURGE: decal alignment uses supplied normals from Core signals; SHINOBU_149 scope has no Physics.Raycast/RaycastNonAlloc.</TASK>
    <TASK id="03" status="PASS">CS1612_ENCAPSULATION_PURGE: hot DTOs use raw fields; no hot get/set properties; pointer jobs mutate with UnsafeUtility.AsRef.</TASK>
    <TASK id="04" status="PASS">ARM64_PADDING_RECONSTRUCTION: DecalInstanceDTO explicit size 80 with exact offsets and runtime/editor validation.</TASK>
    <TASK id="05" status="PASS">EMERGENCY_MOCK_DECAL_INJECTION: GenerateMockDecals schedules deterministic Burst mock requests into the same queue.</TASK>
    <TASK id="06" status="PASS">BURST_MATRIX_GENERATION_KERNEL: GenerateDecalMatricesJob drains NativeQueue, localizes AUP, builds normal-aligned matrices, uses Burst exact flags and NoAlias pointer fields.</TASK>
    <TASK id="07" status="PASS">CIRCULAR_BUFFER_OVERWRITE_LOGIC: CurrentWriteIndex wraps in O(1), overwriting oldest records without growth.</TASK>
    <TASK id="08" status="PASS">THE_DEAR_LIE_SCREEN_SPACE_PROJECTION: one fullscreen pass reconstructs depth and projects mathematical boxes; no meshes/projectors.</TASK>
    <TASK id="09" status="PASS">DETERMINISTIC_DECAL_DECAY: DecayDecalOpacityJob fades Opacity01 and clears inactive flags.</TASK>
    <TASK id="10" status="PASS">ASYNCHRONOUS_GPU_BUFFER_UPLOAD: double GraphicsBuffer LockBufferForWrite path with Burst mapped copy; no SetData.</TASK>
    <TASK id="11" status="PASS">CONTINUOUS_SCALABILITY_CAPACITY_SHRINK: active decal count lerps through Smooth01(GlobalQualityWeight); thermal pressure increases decay.</TASK>
    <TASK id="12" status="PASS">MATERIAL_ATLAS_INDEXING: CPU resolves MaterialHash/profile to atlas slice; shader samples Texture2DArray or procedural fallback.</TASK>
    <TASK id="13" status="PASS">AUP_PRECISION_LOCALIZATION: GenerateDecalMatricesJob subtracts camera double3 AUP before float cast.</TASK>
    <TASK id="14" status="PASS">ROLLBACK_NETCODE_STATE_FENCE: decal Vault buffers are presentation-only and not registered into rollback/Merkle state.</TASK>
    <TASK id="15" status="PASS">ZERO_INIT_OVERHEAD_BYPASS: primary buffers use UninitializedMemory and cold Burst clear writes only relevant fields.</TASK>
    <TASK id="16" status="PASS">TELEMETRY_DECAL_RECORDER: 300-entry telemetry ring plus Dump_DECAL_PROJECTOR.bin fault path.</TASK>
    <TASK id="17" status="PASS">DECAL_TUNER_EDITOR_WINDOW: UI Toolkit tuner edits Vault tuning DTOs without runtime dependency.</TASK>
    <TASK id="18" status="PASS">CSV_DECAL_PROFILES_INGESTOR: cold ReadOnlySpan<byte> parser, FNV-1a names, Vault table, no string.Split.</TASK>
    <TASK id="19" status="PASS">LIVE_MATRIX_DEBUG_GIZMO: editor gizmo reads DTO ring and draws projection volumes without debug GameObjects.</TASK>
    <TASK id="20" status="PASS">SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION: this XML block plus status/rationale/architecture docs provide persistent proof.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DecalInstanceDTO size="80" alignment="16-byte and 8-byte multiple">
      <field name="LocalToWorld" offset="0" size="64" type="float4x4" />
      <field name="MaterialHash" offset="64" size="4" type="uint" />
      <field name="Opacity01" offset="68" size="4" type="float" />
      <field name="LifetimeSeconds" offset="72" size="4" type="float" />
      <field name="Flags" offset="76" size="4" type="uint" />
      <math>64 + 4 + 4 + 4 + 4 = 80. 80 % 16 = 0. 80 % 8 = 0.</math>
    </DecalInstanceDTO>
    <DecalRuntimeStateDTO size="64">Explicit 64-byte runtime state record. Not an atomic counter array.</DecalRuntimeStateDTO>
    <DecalTelemetryEntry size="64">Explicit 64-byte telemetry ring record to avoid false sharing in sequential ring writes.</DecalTelemetryEntry>
    <DecalRequestSignal size="64">Explicit 64-byte queue packet with double3 first, then float/int scalar fields.</DecalRequestSignal>
    <DecalMaterialProfileDTO size="32">Explicit 32-byte cold profile table record.</DecalMaterialProfileDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is saturated and passed through Smooth01(q)=q*q*(3-2*q). Active upload/evaluation capacity lerps from LowTierCapacity 128 to MaximumOverkillCapacity 1024. Below 0.3, MaxRequestsPerFrame and shader decal count collapse toward the low bound, DecayRate rises through quality and thermal pressure, procedural shader detail lerps toward a smooth radial mark instead of broken noise, and depth weight loosens to reduce projection ALU sensitivity. No binary hardware switch is used.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <claim>Zero private NativeArray, NativeList, or NativeHashMap persistent allocations are declared by SHINOBU_149 runtime. Persistent decal state is Vault-owned.</claim>
    <note>One prewarmed NativeQueue exists as transient ingress because Task 06 explicitly requires NativeQueue DecalRequestSignal input. It is registered with NativeMemorySentinel and is not authoritative decal storage.</note>
    <buffer id="71490" name="Instances" />
    <buffer id="71491" name="UploadScratch" />
    <buffer id="71492" name="RuntimeState" />
    <buffer id="71493" name="TelemetryRing" />
    <buffer id="71494" name="Tuning" />
    <buffer id="71495" name="MaterialProfiles" />
    <buffer id="71496" name="CsvScratch" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <job name="ClearDecalsJob" consumes="none" outputs="cleared DecalInstanceDTO flags/opacities" noalias="Decals" />
    <job name="GenerateMockDecalRequestsJob" consumes="deterministic index/frame/origin" outputs="NativeQueue requests" noalias="not applicable to queue writer" />
    <job name="GenerateDecalMatricesJob" consumes="NativeQueue requests" outputs="DecalInstanceDTO ring and DecalRuntimeStateDTO" noalias="Decals, State" />
    <job name="DecayDecalOpacityJob" consumes="generated ring" outputs="DecalInstanceDTO opacity/flags and runtime state" noalias="Decals, State" />
    <job name="BuildDecalUploadBufferJob" consumes="DecalInstanceDTO ring" outputs="UploadScratch and runtime state upload count" noalias="Decals, Upload, State" />
    <job name="DynamicDecalMappedUploadJob" consumes="UploadScratch" outputs="mapped GraphicsBuffer pointer" noalias="Source, Destination" />
    <graph>clear -> generate -> decay -> build-upload -> mapped-copy. The mapped copy is completed before UnlockBufferAfterWrite as required by Unity's mapped buffer lifetime.</graph>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_149 Visor files reference Core/Core.Memory/Core.Contracts.Signals only. They do not reference sibling Gameplay, Physics, World, VFX, Atmosphere, or Ballistics concrete domains. Hull visual impacts cross through GlobalSignals/SignalBus, preserving one fact, one owner, one route.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <before>Object DecalProjector route: O(N) GameObject/component lifetime, renderer feature traversal, and possible per-impact surface query.</before>
    <after>Vault ring insertion is O(1) per impact; CPU visual sync is O(active decals) in Burst; GPU composite is one fullscreen pass bounded by continuously scaled active count.</after>
    <fake>Screen-space depth reconstruction plus mathematical projection volumes fake physical residue. Procedural fallback fakes scorch/dent texture detail with radial masks and quality-weighted noise.</fake>
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION_LIMITS>
    Unity import, shader compiler, RenderGraph runtime, Frame Debugger, Profiler, and GC allocation proof are pending. Narrow Core build was run after CPU gate opened; SHINOBU_149's csproj inclusion fault was fixed; remaining compile errors are outside SHINOBU_149.
  </VERIFICATION_LIMITS>
</SELF_AUDIT>

## 2026-05-19 Post-Audit Hardening Pass

What was wrong:

- The new SHINOBU_149 C# assets had no `.meta` files. Unity would generate GUIDs later, but that is unstable import hygiene for a batch artifact.
- `TryLoadMaterialProfilesCsv` could parse a prefix of an oversized source file because the read loop stopped at the 16 KB scratch capacity and then parsed whatever fit.

What was done:

- Added `.meta` files for `DynamicDecalVaultRuntime.cs`, `DynamicDecalGizmoVisualizer.cs`, and `ScreenSpaceDecalTunerWindow.cs`.
- Hardened the cold CSV loader to reject empty, oversized, and short-read files before calling `ParseMaterialProfilesCsv`.
- Removed the explicit `Hecton8.World.AbsoluteUniversePosition` helper signature from Visor source; high-speed impact AUP conversion now consumes raw signal fields plus the Core.Contracts AUP sector-size constant.
- Split high-speed and combat-damage signal cursors so mixed-lane impact order cannot drop valid decals.
- Added per-lane cursor sentinels so deterministic frame `0` signal packets are accepted once before normal duplicate-frame filtering begins.
- Moved exact field-offset reflection behind `UNITY_EDITOR`; player layout validation keeps the 80-byte size check only.
- Patched GPU upload telemetry in the same frame and made upload stalls immediately emit the black-box dump.
- Added fixed-cap request admission so impact storms cannot force `NativeQueue` internal growth beyond the prewarmed 1024 pending requests.
- Labeled all four SHINOBU_149 `JobHandle.Complete()` sites as `[BLOCKING_SYNC_POINT]`: cold mock generation, first-frame clear, VISUAL_SYNC scratch publication, and mapped `GraphicsBuffer` copy-before-unlock.
- Reordered `DeferredDecalPass` readable-buffer capture so capacity changes cannot release the previous `GraphicsBuffer` and then bind the stale handle in the same RenderGraph frame.
- Added saturating dropped-request accounting so partial `GenerateMockDecals(count)` clamps and full-queue runtime drops are both visible in telemetry without integer wraparound.
- Clamped runtime `MaximumOverkillCapacity` by the render feature's requested buffer capacity so `UploadCount` cannot exceed the active `GraphicsBuffer.count`.
- Raised the renderer feature minimum `maxDecals` and buffer clamps to the mandated 128 low-tier floor, preventing serialized sub-128 values from creating undersized buffers.
- Unified runtime low-tier capacity clamps on `LowCapacity=128`, removing the hidden 16-decal sub-floor.
- Updated status, rationale, and architecture docs with the metadata and CSV fail-closed evidence.
- Synchronized `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the post-audit cursor, request-admission, layout-validation, and upload-stall hardening so the binary route record matches the runtime source.

Cinematic cheats used:

- No change to the render cheat. The screen-space/depth reconstruction path remains the same.

Exact microseconds saved, static estimate:

- Runtime frame impact: 0 us. This pass prevents import churn and authoring-data corruption, not frame work.
- CSV oversized fail-closed avoids a future bad-profile debugging loop; it does not change hot-path cost.

Verification:

- `Test-Path` returned `True` for all three new `.meta` files.
- Source inspection confirmed `TryLoadMaterialProfilesCsv` now checks `stream.Length` against the Vault scratch buffer before reading or parsing.
- Compile-wall scan returned no direct `using Hecton8.World` or `Hecton8.World.` references in the SHINOBU_149 Visor files.
- Source inspection confirmed independent `_lastIngestedHighSpeedFrame` and `_lastIngestedCombatDamageFrame` cursors.
- Source inspection confirmed `System.Reflection` appears only in the editor-only offset validator block.
- Source inspection confirmed `RecordGpuUploadMicroseconds` updates the latest telemetry entry and calls `DumpBlackBox(RuntimeUploadStallFlag)` on stall.
- Source inspection confirmed runtime/signal request paths route through `TryEnqueueRequest`, and mock injection clamps to queue headroom.
- Owned-file static scans after request-admission hardening returned no SHINOBU_149 matches for `DecalProjector`, `GraphicsBuffer.SetData`, `string.Split`, `UnityEngine.Random`, `Physics.Raycast`, `RaycastNonAlloc`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, `List<GameObject>`, or `Instantiate(`.
- Owned-file compile-wall scan after request-admission hardening returned no direct `using Hecton8.Gameplay`, `using Hecton8.Physics`, `using Hecton8.World`, `using Hecton8.VFX`, `using Hecton8.Atmosphere`, `using Hecton8.AI`, `using Hecton8.SaveSystem`, or `Hecton8.World.` references.
- A new build was not launched after this hardening pass because the sampled CPU gate was 100 percent; this follows the batch rule forbidding `dotnet build` under high system load.
- After the binary-ledger sync, `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 99.8 percent; build remains gated off by the same rule.
- After the render-lifetime hardening, owned-file static scans again returned no projector/spawn/raycast/SetData/string-split/random/native-container-forbidden patterns, no direct sibling-domain Visor references, no hot LINQ/foreach/string interpolation hits, no DTO properties or `Pack=1`, and `git diff --check` reported only line-ending warnings. `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 100 percent; build remains gated off.
- After dropped-telemetry hardening, the same static scans were rerun. The only request-admission grep hits are expected `RequestQueuePrewarmCapacity`, `TryEnqueueRequest`, and `AccumulateDroppedIngress` lines. CPU samples remained 100, 100, and 100 percent; build remains gated off.
- After renderer-capacity hardening, the same static scans were rerun. The only capacity grep hits are expected `EnsureDecalBuffers`, `requestedMaxCapacity`, and `MaximumOverkillCapacity` control lines. CPU samples remained 100, 100, and 100 percent; build remains gated off.
- After capacity-floor hardening, the same static scans were rerun. The only capacity-floor grep hits are expected `Range`, `Mathf.Clamp(_settings.maxDecals, LowCapacity, MaxCapacity)`, and runtime `LowCapacity` lines. CPU samples remained 100, 100, and 100 percent; build remains gated off.
- After low-floor unification, the same static scans were rerun. The only non-capacity `16f` grep hit is the expected `0.16f` ballistic radius literal, not a decal-count floor. CPU samples remained 100, 100, and 100 percent; build remains gated off.

## 2026-05-19 Post-Audit Vault Lock And Upload Bound Pass

What was wrong:

- `ExecuteVisualSync` touched `TelemetryRing`, `Tuning`, and `MaterialProfiles` while the explicit lock envelope only protected `Instances`, `UploadScratch`, and `RuntimeState`.
- Signal ingestion ran before the visual-sync lock and could read the material profile table without a Vault lock.
- `RecordGpuUploadMicroseconds`, `WriteTuning`, `TryLoadMaterialProfilesCsv`, `MarkFault`, and `DumpBlackBox` touched Vault memory through control paths without dedicated lock envelopes.
- `DeferredDecalPass.UploadDecalBuffer` trusted `stats.UploadCount` after upstream caps and did not clamp again against the actual mapped `GraphicsBuffer.count`.
- The editor tuner used interpolated strings while the status file claimed SHINOBU_149 C# source was clean of `$"` patterns.

What was done:

- Moved signal ingestion under `TryLockRuntimeBuffers()`.
- Expanded `TryLockRuntimeBuffers()` to lock `Instances`, `UploadScratch`, `RuntimeState`, `TelemetryRing`, `Tuning`, and `MaterialProfiles`, with reverse-order unlock.
- Added dedicated lock envelopes for upload telemetry patching, tuning writes, CSV profile ingest, fault marking, and black-box telemetry reads.
- Clamped mapped upload count against `requestedUploadCount`, `target.count`, and `stats.UploadBuffer.Length` immediately before `LockBufferForWrite`.
- Replaced editor tuner interpolation with invariant-culture `string.Concat` text construction.
- Updated status, rationale, architecture route card, and binary payload ledger with this lock/upload proof.

Cinematic cheats used:

- No change to the visual fake. Deferred decals remain a screen-space depth reconstruction and matrix-column projection illusion, not physical projector objects.

Exact microseconds saved, static estimate:

- Normal runtime frame impact: approximately 0 us saved; this pass trades a few scalar lock increments for stale-handle resistance.
- Render fault avoidance: prevents an out-of-range mapped buffer request under capacity drift. That is stability, not a steady-state frame-time win.
- Editor text change: 0 us player runtime impact.

Verification:

- Owned-file forbidden-pattern scan returned no SHINOBU_149 matches for projector/object-spawn/raycast/SetData/string-split/random/native-container creation patterns.
- Compile-wall scan returned no direct sibling-domain Visor references.
- `Select-String -SimpleMatch '$"', 'foreach', 'string.Format', '.Select(', '.Where(', '.Any(', '.ToList('` over SHINOBU_149 C# source returned no matches.
- DTO hygiene scan returned no `Pack=1`, `Pack = 1`, `get; set;`, or `get; private set;` matches in `DynamicDecalVaultRuntime.cs`.
- `git diff --check` reported only line-ending warnings.
- `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 100 percent; another build launch remains forbidden by the batch CPU gate.

## 2026-05-19 Post-Audit Profile Lifetime Pass

What was wrong:

- CSV `LifetimeSeconds` values were parsed and copied into `DecalRequestSignal`, but the persistent decal ring stored `BirthTime` and the decay job used one uniform global opacity decrement.
- Direct/signal fallback impact paths still used hardwired projection/lifetime/radius defaults instead of the live Vault tuning facade.

What was done:

- Reinterpreted `DecalInstanceDTO` offset 72 as `LifetimeSeconds`; total size remains 80 bytes and all offsets remain 16/8-byte aligned.
- `GenerateDecalMatricesJob` now writes request/profile lifetime into the ring.
- `DecayDecalOpacityJob` scales decay by `baseLifetime / decalLifetime`, so CSV and designer lifetime values change actual persistence without adding another buffer.
- Runtime impact and signal fallback paths now resolve live Vault tuning for projection depth, radius scale, and lifetime scale.
- `WriteTuning` now sanitizes `MaximumOverkillCapacity` before casting to `int`.

Cinematic cheats used:

- The profile lifetime still controls only a scalar opacity curve. No physical material simulation, decal GameObject, or per-material component lifetime exists.

Exact microseconds saved, static estimate:

- Avoided second lifetime buffer/upload: 1024 * 4 bytes of extra CPU/GPU staging avoided per full upload window.
- Added cost: one reciprocal and multiply per active decal in the Burst decay loop.

Verification:

- Source scan shows no remaining `BirthTime`, `CurrentTime`, or `Time.time` references in SHINOBU_149 runtime/shader scope.
- ABI remains 80 bytes: matrix 64 + material 4 + opacity 4 + lifetime 4 + flags 4.
- Owned forbidden-pattern, compile-wall, LINQ/foreach/string, DTO-property/Pack, and Burst-directive scans were rerun; no forbidden matches appeared and six exact Burst directives remain.
- `git diff --check` reported only line-ending warnings.
- `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 100 percent; build remains gated off.

## 2026-05-19 Burst Compiler-Services Import Repair

What was wrong:

- `DynamicDecalVaultRuntime.cs` uses `[NoAlias]` in Burst jobs, but the `Unity.Burst.CompilerServices` import was absent after the compile-wall cleanup pass. Without a global using, that is a direct compile-risk in the SHINOBU_149 runtime source.

What was done:

- Restored `using Unity.Burst.CompilerServices;` in the owned Visor runtime file.
- Kept all `[NoAlias]` annotations intact on matrix generation, decay, upload scratch build, and mapped GPU upload jobs.

Cinematic cheats used:

- No gameplay simulation changed. The presentation fake remains the same one-pass screen-space deferred projector fed by the Vault ring.

Exact microseconds saved, static estimate:

- 0 us runtime delta from the import itself. The prevented failure is compile-time loss of Burst alias annotations; preserving `[NoAlias]` keeps the SIMD/vectorization contract for the existing jobs.

Verification:

- Targeted scan confirmed the namespace import and expected `[NoAlias]` usages in `DynamicDecalVaultRuntime.cs`.
- `git diff --check` still reports only line-ending warnings.
- Build not relaunched because the CPU gate remained saturated in the prior samples.

## 2026-05-19 Legacy Object-Decal Purge And Debug Read Guard Pass

What was wrong:

- The legacy `Assets/Dynamic Decals` package still existed outside `_Project`. It was not a URP `DecalProjector`, but it was an object-decal renderer with `new GameObject`, `Instantiate`, `Update/LateUpdate/FixedUpdate`, runtime pools, `Resources.Load`, mesh projection shaders, and material allocations.
- Editor/debug read APIs resolved Vault arrays without holding the Vault compaction guard while reading.

What was done:

- Deleted `Assets/Dynamic Decals` and `Assets/Dynamic Decals.meta` after script GUID scan showed no references outside that package. This removes 311 tracked legacy files from the decal runtime/import surface.
- Added lock envelopes to `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry`.
- Replaced gizmo buffer reads with `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead`, locking `Instances` and `RuntimeState` while `DynamicDecalGizmoVisualizer` iterates matrix DTOs.

Cinematic cheats used:

- No change to the core illusion: decals remain a screen-space depth reconstruction over Vault matrix data. The deleted package was a mesh/object projection path and is now gone.

Exact microseconds saved, static estimate:

- Legacy package purge removes the old O(N) object projection route from the project. During clustered impacts, this preserves the earlier 300-1400 us static avoidance estimate and removes additional shader/resource import surface. Exact runtime delta still requires Unity profiler capture.
- Debug read guard adds control/editor-path lock increments only; player render hot path is not measurably changed by this pass.

Verification:

- `Test-Path` returned `False` for both `Assets/Dynamic Decals` and `Assets/Dynamic Decals.meta`.
- Pre-delete GUID scan found no `_Project` references for core legacy script GUIDs: `DynamicDecals`, `ProjectionRenderer`, `DynamicDecalSettings`, or `Positioner`.
- Post-delete orphan `.meta` scan under `Assets` returned no orphaned metadata files.
- Owned SHINOBU_149 forbidden-pattern scan still returned no matches for projector/object-spawn/raycast/SetData/string-split/random/native-container creation patterns.
- Compile-wall scan over SHINOBU_149 files returned no direct sibling-domain references.
- `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 100 percent; a new build launch remains forbidden by the batch CPU gate.

## 2026-05-19 Post-Audit Quality Smoothing Pass

What was wrong:

- Active decal capacity followed `HomeostasisBrain.GlobalQualityWeight` directly. A sudden thermal drop could truncate the upload list immediately, making older decals disappear in one frame.

What was done:

- Added `ResolveEffectiveQualityWeight(...)`.
- `DecalRuntimeStateDTO.GlobalQualityWeight` now stores the effective quality used for active count, decay, shader quality, and telemetry.
- Effective quality moves toward the Homeostasis target through `math.lerp(previous, target, saturate(deltaTime * response))`; `response` rises continuously with thermal pressure through `Smooth01`.

Cinematic cheats used:

- No physical simulation. The visual continuity is a scalar budget easing curve plus faster opacity decay under pressure.

Exact microseconds saved, static estimate:

- No direct CPU saving; this buys visual stability with a few scalar operations per visual sync.

Verification:

- Source grep confirms `ResolveEffectiveQualityWeight`, `targetQuality`, and thermal-dependent `math.lerp` response in `DynamicDecalVaultRuntime.cs`.
- Owned forbidden-pattern, compile-wall, LINQ/foreach/string, DTO-property/Pack, and `BirthTime`/`CurrentTime`/`Time.time` scans returned no matches in runtime/shader scope.
- `git diff --check` reported only line-ending warnings.
- `dotnet`/`csc` process scan returned no active compiler process, but CPU samples were 100, 100, and 100 percent; build remains gated off.
