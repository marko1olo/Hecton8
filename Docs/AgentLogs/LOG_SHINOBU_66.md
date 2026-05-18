# LOG_SHINOBU_66

## 2026-05-18 - DRS And FSR Director

What was wrong:
Existing dynamic resolution logic was close to usable but still had high-risk gaps: SHI polarity was ambiguous, scale changes were too step-like, fallback URP asset mutation could blur UI cameras, DRS state had no dedicated 16B DTO in the Vault, the requested editor facade did not exist, and telemetry did not explicitly carry frames-below-target/upscaler-cost data. No owned DRS file used `Screen.SetResolution`, but the display-resolution path exists elsewhere in settings UI and was left outside this DRS domain.

What was done:
Implemented `DrsStateDTO` (16B, no `Pack=1`) and `BufferID.DrsState`. The adapter now resolves continuous `GlobalQualityWeight` from stress, EWMA-smooths current scale toward target scale, and panic-drops to tier min scale at 33ms/pressure level 3. DRS writes internal scalable buffers and DynamicResolutionHandler state without `Screen.SetResolution`. System override no longer mutates the URP asset renderScale. World cameras get `allowDynamicResolution`; UI/RT/overlay cameras stay native. Shader globals now receive TAA sharpen, mip bias, screen pixel dimensions, post-process weight, and upscaler hash. The telemetry ring records current/target scale, stress, `FramesBelowTarget`, estimated `UpscalerComputeTimeMs`, and dumps `Dump_DRS_SURGEON.bin` on invalid state. Added the editor-only Dynamic Resolution Tuner with sliders, mock quality drop, CSV loading, and oscilloscope.

Cinematic Cheats used:
EWMA frame pressure instead of GPU timestamp plumbing. Bilinear+TAA hash on low/MX350 instead of FSR-class compute. Inverse-scale sharpen and mip-bias shader globals instead of expensive reconstruction ownership. Continuous post-process weight instead of physically simulating image stability. Estimated upscaler compute telemetry until real GPU timer integration exists.

Exact Microseconds saved:
Display-resolution mutation avoided: unbounded hitch/reallocation path removed from DRS. Internal target scale 1.0 -> 0.65 saves about 55-58% fill-rate pixels before post overhead. UI shield avoids native UI re-render workaround cost. Estimated hot CPU costs: scale solve 4us, render-scale commit 8us, sharpen 3us, mip/screen globals 5us, telemetry 4us, camera shield ~5us per active camera callback.

Verification:
PASS `dotnet csc @Library/Bee/.../Hecton8.Core.Contracts.rsp`.
PASS `dotnet csc @Library/Bee/.../Hecton8.Core.Memory.rsp`.
PASS `dotnet csc @Library/Bee/.../Hecton8.Graphics.Scalability.rsp`.
PASS scoped editor compile `@Docs/AgentLogs/Hecton8.Editor.DynamicResolutionTuner_SHINOBU_66.rsp`.
BLOCKED external full project build: `PlayerBuilder.cs` currently misses construction DTO types outside SHINOBU_66.

## 2026-05-18 - Ultra Polish Recheck

What was wrong:
The first pass still carried three residues that were not titanium: DRS contracts lived in the broad `CoreContractsAssemblyMarker.cs`, the UI camera shield stored runtime `Camera[]` / `byte[]` role caches, and the dump filename had drifted to another agent lane (`Dump_SHINOBU_68.bin`). Those defects did not break the owned compile, but they violated compile-wall hygiene, H-PHI reporting precision, and black-box ownership.

What was done:
Moved DRS runtime snapshot, scale state, flags, hot DTO, and mock signal into `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs`. Removed the runtime camera-role arrays and now classifies camera eligibility directly in `beginCameraRendering`. Restored `Dump_DRS_SURGEON.bin`. Re-ran owned assembly compiles after the polish delta and confirmed the remaining project-level failure is outside the SHINOBU_66 lane.

Cinematic Cheats used:
No physical reconstruction system was added. The DRS lie remains scalar: EWMA render-scale motion, inverse-scale TAA sharpen, mip bias, screen-pixel CBuffer globals, bilinear+TAA hash on MX350, and continuous post-process load shedding.

Exact Microseconds saved:
Camera role cache removal saves no meaningful frame time directly; it removes persistent managed array residency and stale scene-reference risk. Internal render-scale drop to 0.65 still buys roughly 55-58% pixel fill-rate reduction before post overhead. Runtime scalar solve remains estimated at 4us, render-scale commit 8us, shader globals 5us, telemetry 4us, camera classification under 5us per camera callback.

Verification:
PASS static scan: no `Screen.SetResolution`, `new RenderTexture`, `RenderTexture.GetTemporary`, `.Split`, `.ToArray`, `Enumerable`, `using Hecton8.UI`, camera-role arrays, or `Dump_SHINOBU_68` in owned DRS paths.
PASS static scan: no `Pack=1`, sequential `Pack`, or hot DTO `{ get; set; }` pattern in DRS contract/runtime files.
PASS `dotnet csc @Library/Bee/.../Hecton8.Core.Contracts.rsp Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs`.
PASS `dotnet csc @Library/Bee/.../Hecton8.Core.Memory.rsp`.
PASS `dotnet csc @Library/Bee/.../Hecton8.Graphics.Scalability.rsp`.
PASS scoped editor compile `@Docs/AgentLogs/Hecton8.Editor.DynamicResolutionTuner_SHINOBU_66.rsp`.
BLOCKED external project build: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails in `Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs` because `WristHudQuadTransformDTO` is missing. This is UI domain, not DRS.

<SELF_AUDIT agent_id="SHINOBU_66" domain="DYNAMIC_RESOLUTION_AND_FSR_DIRECTOR" date="2026-05-18">
  <TaskReconciliation total="20">
    <Task id="01" status="[PASS]">Archive reconnaissance found no clean `resolution_scaling_curves.h8bin`; emergency 16B scale-limit DTO is generated at boot.</Task>
    <Task id="02" status="[PASS]">Owned DRS files scan clean for `Screen.SetResolution`; internal scalable buffers are used instead.</Task>
    <Task id="03" status="[PASS]">`DrsStateDTO` is fields-only and exposed through `ref DrsStateDTO GetMutableDrsState()`.</Task>
    <Task id="04" status="[PASS]">`DrsStateDTO` is 16B; telemetry is 48B explicit; no `Pack=1` in owned DRS structs.</Task>
    <Task id="05" status="[PASS]">`MockQualityWeightSignal` exists in contract-only code; Burst mock job drops target weight to 0.2 without Agent 44 concrete dependency.</Task>
    <Task id="06" status="[PASS]">Core scale target is `lerp(MinScaleLimit, 1.0, GlobalQualityWeight)` and current scale follows EWMA math.</Task>
    <Task id="07" status="[PASS]">DRS commits through DynamicResolutionHandler plus `ScalableBufferManager.ResizeBuffers`; system override avoids URP asset renderScale mutation.</Task>
    <Task id="08" status="[PASS]">TAA/FSR sharpen is inverse-scale math exported as `_SharpenIntensity` and `_H8DrsTaaSharpen`.</Task>
    <Task id="09" status="[PASS]">Only base game/world cameras get dynamic resolution; UI-only, non-game, and target-texture cameras stay native.</Task>
    <Task id="10" status="[PASS]">Mip bias is `log2(1/currentScale)` and broadcast as `_H8DrsMipBias`.</Task>
    <Task id="11" status="[PASS]">Upscaler hash is continuous by scale and tier: native, bilinear+TAA on low/MX350, FSR+TAA on stronger tiers.</Task>
    <Task id="12" status="[PASS]">Shader globals receive render scale, screen pixel dimensions, post weight, visual flags, and upscaler hash.</Task>
    <Task id="13" status="[PASS]">DRS has no AUP/double coordinate payload; it remains screen-space only.</Task>
    <Task id="14" status="[PASS]">`>=33ms` frame EWMA or pressure level 3 bypasses smoothing and panic-drops to tier min scale.</Task>
    <Task id="15" status="[PASS]">Heavy post-process weight is continuous from min-scale floor to native, not a binary toggle.</Task>
    <Task id="16" status="[PASS]">One `BufferID.DrsState` Vault element is allocated with `NativeArrayOptions.UninitializedMemory`.</Task>
    <Task id="17" status="[PASS]">300-frame telemetry ring records scale, stress, `FramesBelowTarget`, `UpscalerComputeTimeMs`, and dumps `Dump_DRS_SURGEON.bin` on invalid state.</Task>
    <Task id="18" status="[PASS]">Editor facade exposes min scale, smoothing, sharpen, and mock controls.</Task>
    <Task id="19" status="[PASS]">CSV override parser uses `ReadOnlySpan<char>`, FNV hash, and no `Split` or LINQ in runtime parser.</Task>
    <Task id="20" status="[PASS]">Editor-only OnGUI oscilloscope draws preallocated telemetry arrays over the ring.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>
    <Struct name="DrsStateDTO" layout="Sequential" size="16" alignment="16B">
      <Field name="CurrentRenderScale" offset="0" size="4" type="float" />
      <Field name="TargetRenderScale" offset="4" size="4" type="float" />
      <Field name="UpscalerTypeHash" offset="8" size="4" type="uint" />
      <Field name="_pad0" offset="12" size="4" type="uint" />
      <Math>4+4+4+4=16; 16 % 16 = 0; no `Pack=1`.</Math>
    </Struct>
    <Struct name="DrsTelemetryEntry" layout="Explicit" size="48" alignment="8B">
      <Field name="Frame" offset="0" size="4" type="uint" />
      <Field name="CurrentScale01" offset="4" size="4" type="float" />
      <Field name="TargetScale01" offset="8" size="4" type="float" />
      <Field name="FrameTimeEwmaMs" offset="12" size="4" type="float" />
      <Field name="SystemStress01" offset="16" size="4" type="float" />
      <Field name="SystemStressEwma01" offset="20" size="4" type="float" />
      <Field name="SharpenIntensity01" offset="24" size="4" type="float" />
      <Field name="Flags" offset="28" size="4" type="uint" />
      <Field name="Sequence" offset="32" size="4" type="uint" />
      <Field name="PressureLevel" offset="36" size="1" type="byte" />
      <Field name="ThermalSeverity" offset="37" size="1" type="byte" />
      <Field name="StpActive" offset="38" size="1" type="byte" />
      <Field name="AupLockFrames" offset="39" size="1" type="byte" />
      <Field name="HysteresisCounters" offset="40" size="2" type="ushort" />
      <Field name="FramesBelowTarget" offset="42" size="2" type="ushort" />
      <Field name="UpscalerComputeTimeMsBits" offset="44" size="4" type="uint" />
      <Math>Final byte offset 44+4=48; 48 % 8 = 0.</Math>
    </Struct>
  </StructLayoutVerification>
  <ScalabilityCurve>
    When `GlobalQualityWeight` drops below 0.3, target scale collapses continuously toward the tier floor through `math.lerp(MinScaleLimit, 1.0, weight)`, then current scale approaches through EWMA unless panic state forces the floor. Low/MX350 uses bilinear+TAA instead of FSR compute. `SharpenIntensity = clamp((1/scale - 1) * multiplier, 0, 0.85)`, mip bias is `log2(1/scale)`, heavy post-process weight trends to 0 at the floor, and visual-overkill feature flags collapse by scalar thresholds instead of quality switches.
  </ScalabilityCurve>
  <HPHIVaultStatus private_runtime_arrays="0" private_runtime_native_collections="0">
    <VaultBuffer id="BufferID.DrsState" type="DrsStateDTO" length="1" owner="SystemID.GraphicsScalability" options="UninitializedMemory" />
    <VaultBuffer id="BufferID.ResolutionScaleState" type="ResolutionScaleState" length="1" owner="SystemID.GraphicsScalability" />
    <VaultBuffer id="BufferID.ResolutionScaleTelemetry" type="DrsTelemetryEntry" length="300" owner="SystemID.GraphicsScalability" />
    <Note>Editor oscilloscope arrays are editor-only. Android XR list scratch is cold subsystem API bridge, not DRS-owned simulation state.</Note>
  </HPHIVaultStatus>
  <PointerAliasingAndDependencyGraph>
    <Job name="SystemStressEwmaJob" burst="CompileSynchronously=true FloatMode.Fast FloatPrecision.Standard" noalias="State pointer" consumes="ResolutionScaleState, InputStress01" outputs="ResolutionScaleState.SystemStressEwma01" handle="_stressEwmaHandle" completion="deferred until IsCompleted, TryGetScaleState, or forced shutdown" />
    <Job name="MockQualityWeightDropJob" burst="CompileSynchronously=true FloatMode.Fast FloatPrecision.Standard" noalias="State pointer" consumes="DrsStateDTO, MinScaleLimit" outputs="DrsStateDTO.TargetRenderScale, UpscalerTypeHash" handle="cold editor proof path only" completion="immediate only outside Tick" />
  </PointerAliasingAndDependencyGraph>
  <CompileGuard direct_sibling_runtime_refs="false">
    `Hecton8.Graphics.Scalability.asmdef` references Core, Core.Contracts, Core.Memory, Bootstrap.Contracts, and Unity packages only. It does not reference UI, World, Gameplay, or other sibling runtime domains.
  </CompileGuard>
  <DearLieConfirmation>
    The fake is scalar temporal reconstruction: lower internal world render target, preserve native UI target, push inverse-scale sharpen/mip/post globals, and let shaders/upscaler consume the signal. Before: native fill or display-resolution mutation with reallocations and visible jumps. After: O(1) CPU scalar updates plus GPU pixel cost O(scale^2 * W * H), with no swapchain resolution mutation.
  </DearLieConfirmation>
  <ResidualRisk>
    Runtime profiler, Unity import, Frame Debugger, and VR headset capture are still pending. Full project `dotnet build` is blocked outside this lane by missing UI `WristHudQuadTransformDTO`.
  </ResidualRisk>
</SELF_AUDIT>

## SHINOBU_66 LOG - PROPERTY/FACADE PURGE - 2026-05-19

What was wrong:
- The validator still exposed property-style seams after the CS1612 audit: `MockModQueue.IsCreated`, validator state flags, and pending/devnull count properties.
- The editor tuner owned a private `NativeArray<ModSandboxTelemetryEntry>` scratch buffer, editor-only but still a weak H-PHI audit story.
- Compile could not be retried after this polish because CPU sampled 79% with an external `dotnet.exe` active, then 100% with no compiler process.

What was done:
- Replaced property-style seams with explicit methods: `GetIsCreated`, `GetPendingEnvelopeCount`, `GetDevNullEnvelopeCount`, `GetIsInitialized`, and `GetHasScheduledValidation`.
- Added `TryGetTelemetryEntry(int, out ModSandboxTelemetryEntry)` so editor histogram reads the Vault telemetry ring directly.
- Removed editor-owned `NativeArray` telemetry scratch allocation and its `Allocator.Persistent` lifetime from `ModApiSandboxTunerWindow`.
- Re-ran static greps against the validator/editor facade: no property/arrow expression seams; no private persistent native containers; no `NativeParallel*`; no owned `NativeQueue`; no `Allocator.Persistent`; no bare `[BurstCompile]`; no `Stopwatch.StartNew`; no Harmony/BepInEx/reflection; no `Pack=1`; no LINQ; no `string.Format`; no hot-path `foreach`.
- Re-ran `git diff --check` on all touched files; no whitespace errors, only Git CRLF conversion warnings for pre-existing tracked files.

Cinematic Cheats used:
- UGC remains the Dear Lie API: external code serializes 64-byte envelopes; the engine processes only deterministic mathematical requests and DevNulls unclaimed seams.
- Editor traffic visualization is a direct Vault histogram, not a runtime HUD or managed telemetry mirror.

Exact Microseconds saved:
- Runtime packet hot path: no measured delta from property purge; compile/audit hygiene change only.
- Editor-only scratch removal: one 300-entry native allocation removed from tuner lifetime; no runtime frame claim.

<SELF_AUDIT agent_id="SHINOBU_66" iteration="property_facade_purge">
  <Task id="01" status="[PASS]">Archive/ledger read remained unchanged: no compatible `allowed_mod_opcodes.h8bin`; emergency 16-byte opcode records stay active.</Task>
  <Task id="02" status="[PASS]">No Harmony/BepInEx/reflection path in the validator surface; managed mod entry remains quarantined.</Task>
  <Task id="03" status="[PASS]">Post-polish grep found no property or expression-bodied property seams in `FutureCommandSandboxValidator.cs` or the editor facade.</Task>
  <Task id="04" status="[PASS]">Primary envelope remains explicit 64B with no `Pack=1`.</Task>
  <Task id="05" status="[PASS]">`MockModQueue` remains caller-owned only; readiness is now an explicit method, not a property.</Task>
  <Task id="06" status="[PASS]">Burst validation kernel unchanged; static banned-pattern scan still clean.</Task>
  <Task id="07" status="[PASS]">Signal routing unchanged and unmanaged.</Task>
  <Task id="08" status="[PASS]">DevNull routing unchanged.</Task>
  <Task id="09" status="[PASS]">Vault blackbox memory unchanged.</Task>
  <Task id="10" status="[PASS]">Per-signature budget/drop behavior unchanged.</Task>
  <Task id="11" status="[PASS]">Continuous `GlobalQualityWeight` budget/shed behavior unchanged.</Task>
  <Task id="12" status="[PASS]">AUP bounds validation unchanged.</Task>
  <Task id="13" status="[PASS]">Rollback freeze view unchanged and remains local/no sibling networking reference.</Task>
  <Task id="14" status="[PASS]">CRC/byte-length asset gate unchanged.</Task>
  <Task id="15" status="[PASS]">Fauna stimulus sandbox unchanged.</Task>
  <Task id="16" status="[PASS]">Editor facade no longer owns a telemetry NativeArray scratch; runtime persistent buffers remain Vault handles only.</Task>
  <Task id="17" status="[PASS]">300-frame telemetry ring remains Vault-owned; editor reads entries directly.</Task>
  <Task id="18" status="[PASS]">Editor tuner remains available without runtime HUD coupling.</Task>
  <Task id="19" status="[PASS]">CSV parser unchanged; cold disk reload remains outside gameplay.</Task>
  <Task id="20" status="[PASS]">Live histogram now reads Vault entries directly and draws with `EditorGUI.DrawRect`.</Task>
  <Compile status="[BLOCKED]">No compile launched: CPU 79% with external `dotnet.exe`, then CPU 100% with no compiler process.</Compile>
</SELF_AUDIT>

---

# SHINOBU_66 LOG - JOBHANDLE CHAIN SEAM - 2026-05-19

What was wrong:
- `DrainPreSimulation()` had only the legacy synchronous void-dispatcher path. It did not expose a `JobHandle` seam for future Kahn/topological dispatcher integration.

What was done:
- Added `TrySchedulePreSimulation(JobHandle dependsOn, out JobHandle validationHandle)`.
- Added `TryFinalizeScheduledPreSimulation(bool forceComplete)` with non-blocking `IsCompleted` behavior when `forceComplete` is false.
- Registered scheduled validator work with `H8Memory.RegisterActiveJob(SystemID.ModSandbox, validationHandle)`.
- Refactored validation job preparation/telemetry finalization into shared helpers so sync and scheduled paths use the same packet drain, budget, thermal shed, Vault views, and Burst job.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains a bounded binary intent validator rather than executing mod code.

Exact Microseconds saved:
- No measured runtime number claimed. The scheduled seam removes the architectural need for an immediate main-thread fence when the dispatcher adopts it.

Verification:
- PASS `git diff --check` after this patch.
- PASS static grep: scheduled API is present, no new allocator-owned queue path, no private persistent native container, no bare Burst attribute, no `Stopwatch.StartNew`, no Harmony/BepInEx/reflection, no `Pack=1`, no LINQ hot path.
- BLOCKED scoped compile: CPU sampled 100% with active external `dotnet.exe`/`csc.exe`; after those exited CPU still sampled 85%, then 100% with no compiler process, so no compiler was launched.

<SELF_AUDIT agent_id="SHINOBU_66" domain="MOD_SANDBOX_AND_OPCODE_VALIDATOR" date="2026-05-19" iteration="jobhandle_seam">
  <TaskReconciliation total="20">
    <Task id="01" status="[PASS]">Archive scan found no compatible opcode bin; emergency 16B records seed the Vault table.</Task>
    <Task id="02" status="[PASS]">No Harmony/BepInEx/reflection mod execution path in active UGC command flow.</Task>
    <Task id="03" status="[PASS]">`FutureCommandEnvelope` is public fields only.</Task>
    <Task id="04" status="[PASS]">Primary envelope is 64B, explicit offsets, no `Pack=1`.</Task>
    <Task id="05" status="[PASS]">Mock queue is caller-owned; malicious injection is packet-only.</Task>
    <Task id="06" status="[PASS]">Burst validator uses deterministic compile flags, `[NoAlias]`, opcode/integrity/AUP/CRC/counter checks.</Task>
    <Task id="07" status="[PASS]">Valid packets route to typed unmanaged SignalBus lanes.</Task>
    <Task id="08" status="[PASS]">Unclaimed seams route to DevNull without crash.</Task>
    <Task id="09" status="[PASS]">Mod custom memory is isolated in Vault blackbox chunks.</Task>
    <Task id="10" status="[PASS]">Per-signature DoS caps and ring eviction bound flood cost.</Task>
    <Task id="11" status="[PASS]">`GlobalQualityWeight` lerps command budget and drives thermal shed below q=0.3.</Task>
    <Task id="12" status="[PASS]">AUP coordinates reject non-finite and out-of-world values.</Task>
    <Task id="13" status="[PASS]">Rollback freeze is local Vault flag view, no Networking reference.</Task>
    <Task id="14" status="[PASS]">Asset opcodes require approved CRC32 and byte length.</Task>
    <Task id="15" status="[PASS]">Fauna opcodes emit stimulus only.</Task>
    <Task id="16" status="[PASS]">Persistent buffers are Vault-owned; validator stores handles only.</Task>
    <Task id="17" status="[PASS]">300-frame telemetry ring and dump path exist.</Task>
    <Task id="18" status="[PASS]">Editor tuner exists.</Task>
    <Task id="19" status="[PASS]">CSV ingestor parses byte spans without runtime `Split`/LINQ.</Task>
    <Task id="20" status="[PASS]">Editor histogram draws traffic/rejections.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>FutureCommandEnvelope offsets: 0 OpcodeHash 4B, 4 ModderSignature 4B, 8 TargetAUP 24B, 32 PayloadData 16B, 48 IntegrityHash 8B, 56 _pad0 8B. Total 64B; 64%16=0. `ModSandboxScheduledValidationState`, counters, stats, telemetry, signals are also explicit 64B where false sharing matters.</StructLayoutVerification>
  <ScalabilityCurve>Budget = round(lerp(10, MaxCommandsPerFrame, saturate(GlobalQualityWeight))). Below q=0.3, thermal shed = saturate((0.30-q)*3.3333333) and drops overflow above the safe window.</ScalabilityCurve>
  <HPHIVaultStatus private_runtime_arrays="0">Vault handles: PendingRing, DevNullRing, Staging, Stats, OpcodeRecords, ModCounters, MemoryLeases, ApprovedAssets, RingState, Tuning, BlackboxMemory, TelemetryRing, TelemetryCursor.</HPHIVaultStatus>
  <PointerAliasingAndDependencyGraph>`ValidateFutureCommandEnvelopeJob` consumes caller dependency in scheduled mode and outputs `validationHandle`; native views are `[NoAlias]`. `TryFinalizeScheduledPreSimulation(false)` refuses to call `Complete()` until `IsCompleted` is true.</PointerAliasingAndDependencyGraph>
  <CompileGuard>`FutureCommandSandboxValidator.cs` has no direct sibling runtime assembly reference; scheduled registration uses `SystemID.ModSandbox` through Core memory only.</CompileGuard>
  <DearLieConfirmation>Before: arbitrary managed mod code could run O(user code) with GC/desync risk. After: bounded O(n) 64B packet validation plus O(1) signal/DevNull routing.</DearLieConfirmation>
</SELF_AUDIT>

# SHINOBU_66 LOG - SCOPED COMPILE RETRY AFTER INGRESS HARDENING - 2026-05-19

What was wrong:
- The ingress hardening patch changed source after the previous compile-wall probe, so the verification ledger needed a fresh gated Roslyn pass.

What was done:
- Checked CPU and compiler process gate before probing: CPU was below 50%, no `dotnet`/`csc` process was active.
- Did not run `dotnet build`.
- Ran scoped Roslyn against `Hecton8.Core.rsp` with the new validator source explicitly included.
- Compile stopped on the same non-owned dependency wall: `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`.

Cinematic Cheats used:
- None in verification. Runtime Dear Lie remains envelope-only intent processing: external UGC serializes 64B packets; engine never executes mod code.

Exact Microseconds saved:
- No runtime microseconds claimed from this verification step.
- Build-time protection preserved by avoiding full project build and by obeying the CPU/CSC gate.

Verification:
- BLOCKED scoped compile: first emitted errors were `Hecton8.Construction.MockWorldSampler`, missing `HectonRollbackNetcodeRuntime`, missing `VolcanicUpdraftDirector`, and missing construction DTOs.
- No `FutureCommandSandboxValidator.cs` compiler error appeared before the external wall.

# SHINOBU_66 LOG - MOCK QUEUE OWNERSHIP PURGE - 2026-05-19

What was wrong:
- `MockModQueue.Initialize(int)` still allocated a persistent `NativeQueue`, creating a convenience allocator seam inside the quarantine domain.

What was done:
- Removed the persistent queue allocation path from `MockModQueue`.
- Added `MockModQueue.Wrap(ref NativeQueue<FutureCommandEnvelope>)` and `Attach(ref NativeQueue<FutureCommandEnvelope>)`.
- `Dispose()` now releases only the wrapper handle; the external producer/test harness owns actual queue lifetime.

Cinematic Cheats used:
- Mocking remains a binary-envelope fake: malicious behavior is represented as corrupted 64B packets, not direct gameplay code.

Exact Microseconds saved:
- No per-frame saving claimed; this removes an allocator ownership hazard from test/producer paths.

Verification:
- PASS static grep: no `new NativeQueue`, `RegisterNativeQueue`, `UnregisterNativeQueue`, or `Allocator.Persistent` remains in `FutureCommandSandboxValidator.cs`.
- BLOCKED scoped compile: after this patch, `Hecton8.Core.rsp` repeated the same non-owned wall in `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`; no `FutureCommandSandboxValidator.cs` error appeared before the wall.

---

# SHINOBU_66 LOG - MOD_SANDBOX_AND_OPCODE_VALIDATOR - INGRESS HARDENING - 2026-05-19

What was wrong:
- `RequestRawEnvelopeStream` called `Request()` for every envelope, which repeated Vault resolution and ring-state writes per 64B packet.
- The Vault refactor removed validator-owned queues, but the original external producer model still needed a zero-GC `NativeQueue` drain seam.
- Asset approval stored CRC but not approved byte length, leaving the asset gate size-blind except for the global max.
- Binary endianness was implicit little-endian only.

What was done:
- Rewrote `RequestRawEnvelopeStream` to resolve pending ring/ring state once and enqueue the entire stream in one pass.
- Added `RequestRawEnvelopeStream(NativeArray<byte>, int, bool sourceBigEndian)` with explicit field byte reversal for legacy/big-endian payload hydration.
- Added `RequestFromExternalQueue(ref NativeQueue<FutureCommandEnvelope>, maxEnvelopeCount)` so producer-owned queues can drain into the Vault ring without validator-owned allocator state.
- Added `RegisterApprovedAsset(assetHash, crc32, byteLength)` and made the asset opcode reject zero-byte declarations or declarations above the approved byte length.

Cinematic Cheats used:
- The API remains the fake: external code only emits binary intent. Queue or stream producers never execute inside the engine; the validator transforms intent into cheap signals or DevNull.

Exact Microseconds saved:
- Bulk stream ingestion removes one Vault resolve and one ring-state write per packet. For 10,000 envelopes, that collapses 10,000 state-resolve/write cycles into one resolve and one final state write. Exact profiler proof is still pending.
- External `NativeQueue` support preserves parallel producer ergonomics without reintroducing validator-owned queue allocation.

Verification:
- PASS `git diff --check` for `FutureCommandSandboxValidator.cs`.
- PASS static grep for bulk ingress methods and absence of private persistent native containers / bare Burst attributes in the validator.
- BLOCKED compile rerun: a `dotnet`/`csc` compiler process appeared externally after source edits, CPU stayed above the >50% build gate. No additional compiler was launched on top of it.

---

# SHINOBU_66 LOG - MOD_SANDBOX_AND_OPCODE_VALIDATOR - 2026-05-18

What was wrong:
- `CURRENT_BATCH.md` has duplicate `SHINOBU_66`; previous local status/logs belonged to DRS. Active user assignment is the later `MOD_SANDBOX_AND_OPCODE_VALIDATOR`.
- Existing mod runtime still allowed managed `IHectonMod` callbacks. That violates envelope-only UGC and keeps a GC/desync path alive.
- Existing `FutureCommandEnvelope64` in Global contracts is 64B but has the wrong field layout for this assignment.
- No `allowed_mod_opcodes.h8bin` was found in archive search.

What was done:
- Added `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs`.
- Added explicit 64B `FutureCommandEnvelope`: `uint OpcodeHash`, `uint ModderSignature`, `double3 TargetAUP`, `float4 PayloadData`, `ulong IntegrityHash`, `ulong _pad0`.
- Added emergency 16B opcode registry records for spawn, health, gravity, asset, mod memory, fauna acoustic/damage, and subtitle seams.
- Added Burst `ValidateFutureCommandEnvelopeJob`: allowlist, XXHash3 integrity over bytes `0..47`, finite +/-50km AUP gate, per-signature counter, CRC32 asset manifest gate, mod-memory bounds, DevNull routing.
- Added unmanaged output lanes: `ModSpawnRequestSignal`, `ModAssetReferenceSignal`, `MockAcousticSignal`, `MockDamageSignal`, `ModFutureDevNullSignal`.
- Added Vault-backed modder memory via `BufferID.ShinobuModSandboxBlackboxMemory`, plus tuning/telemetry buffer IDs and `SystemID.ModSandbox`.
- Integrated validator into `ModCommandDispatcher.DrainPreSimulation` and `DrainLateFrame`.
- Added `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope)`.
- Disabled managed-entry mod candidates in `ModLoader`; content-only mods remain loadable.
- Added `Assets/_Project/Scripts/ModdingAPI/Editor/ModApiSandboxTunerWindow.cs` with budget/memory/asset sliders, opcode toggles, CSV reload, self-audit injection, dump button, and `EditorGUI.DrawRect` traffic histogram.
- Added `Docs/Modding/Mod_API_Sandbox_Quarantine.md`.
- Replaced stale DRS `Status_SHINOBU_66.md` and `Rationale_SHINOBU_66.md` with MOD sandbox state and decisions.

Cinematic Cheats used:
- The Dear Lie: valid but unclaimed future opcodes route to DevNull instead of requiring real owner systems.
- Fauna control is fake stimulus only: acoustic/damage signals, no direct AI command.
- Asset reference opcodes verify hashes and emit intent, not load arbitrary bytes on the simulation path.

Exact Microseconds saved:
- Managed callback execution removed from active UGC path: unpredictable GC cost reduced to 0 B hot-path allocation by policy.
- Per-envelope validation estimate: 0.02-0.12 us static target for hash/AUP/counter path; profiler pending.
- Flood collapse: 1,000,000 submitted commands cannot drain into gameplay; global/per-signature budget caps visible processing to continuous `GlobalQualityWeight` result. On hot low-tier weight 0.1, per-signature budget floors at 10 commands/frame.
- Memory op path: 1-4 bytes written in assigned Vault chunk; no managed dictionary/object storage.
- Telemetry cost: 64B ring write/frame; dump is cold fault path.

Verification:
- `git diff --check` on touched files: PASS, CRLF warnings only.
- Static grep: no Harmony/BepInEx/reflection path in ModdingAPI changes; no `Pack=1`; no `FutureCommandEnvelope` properties; no runtime `string.Split`/LINQ parser; no direct Inventory/AI dependency.
- Compile/build: NOT RUN. CPU samples returned 100%; AGENTS forbids build at >50% CPU and forbids launching another compiler under load.

SELF_AUDIT:
- Reflection or C# method invocation for mods: NO. Managed entries are disabled; runtime path is binary envelope.
- 64B ARM64 envelope: YES by explicit `FieldOffset` layout, no `Pack=1`.
- `{ get; set; }` properties in `FutureCommandEnvelope`: NO.
- `GlobalQualityWeight` throttling: YES, continuous lerp from 10 to tuner max.
- Editor facade: YES, Mod API Sandbox Tuner plus histogram.

---

# SHINOBU_66 LOG - MOD_SANDBOX_AND_OPCODE_VALIDATOR - ULTRA POLISH RECHECK - 2026-05-18

What was wrong:
- First MOD pass still owned private persistent `NativeQueue`, `NativeArray`, `NativeParallelHashSet`, and `NativeParallelHashMap` fields inside `FutureCommandSandboxValidator`.
- Burst jobs used bare `[BurstCompile]` instead of deterministic rollback-safe flags.
- Per-mod counters were 16B, vulnerable to false-sharing if parallelized later.
- Hot validation timing used `Stopwatch.StartNew()`, creating a managed object on the gameplay path.
- Rollback freeze directly referenced `Hecton8.Networking` for one flag bit, weakening compile-wall isolation.
- Thermal throttling reduced drain budget but did not actively discard overheated backlog.

What was done:
- Replaced validator-owned persistent containers with `VaultBufferHandle<T>` fields and short-lived resolved Vault views.
- Moved pending ingress, DevNull, staging, stats, opcode records, per-mod counters, memory leases, approved assets, ring state, tuning, modder memory, and telemetry into `BufferID.ShinobuModSandbox*` Vault buffers.
- Replaced hash collections with fixed open-address arrays.
- Made `FutureCommandValidationStats`, `ModderFrameCounter`, and `ModSandboxRingState` explicit 64B structs.
- Added `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` to validator and malicious mock jobs.
- Added `[NoAlias]` to Burst job NativeArray views.
- Replaced `Stopwatch.StartNew()` with allocation-free `Stopwatch.GetTimestamp()`.
- Removed direct `Hecton8.Networking` using; rollback freeze now reads local 64B Vault flag view at buffer `70752`, `Flags` offset 44, bit `1 << 4`.
- Added continuous CPU-overheat packet shedding for backlog when `GlobalQualityWeight < 0.3`.

Cinematic Cheats used:
- UGC remains a binary intent illusion: mods never execute engine-side code.
- Unclaimed seams are accepted and routed to DevNull so authors perceive API continuity without forcing premature owner systems.
- Fauna control remains acoustic/damage stimulus, not direct AI control.
- Asset opcodes emit verified intent after CRC/byte ceiling checks; no arbitrary runtime asset load enters simulation.

Exact Microseconds saved:
- Private hash allocator state removed from hot validator residency: saves allocator churn and unpredictable cache misses under flood; exact profiler proof pending.
- `Stopwatch.StartNew()` allocation removed from `DrainPreSimulation`: saves one managed object per processed frame.
- 64B counters avoid future false-sharing if the job becomes parallel; expected gain is stability under multi-core load, not a claimed measured frame number.
- Thermal shed collapses stale backlog instead of paying delayed UGC validation later; on low quality 0.1, overflow above the safe window drops by about 66.7%.
- Envelope validation remains static-estimated at 0.02-0.12 us/envelope; profiler proof blocked by CPU/build rule.

Verification:
- PASS `git diff --check` on scoped touched tracked files; CRLF warnings only.
- PASS static grep: no `NativeParallel*`, private persistent `NativeArray`/`NativeQueue`/`NativeHashMap`/`NativeHashSet`, bare `[BurstCompile]`, `Stopwatch.StartNew`, Harmony/BepInEx/reflection, `Pack=1`, LINQ, or direct sibling using in `FutureCommandSandboxValidator.cs`.
- PASS static grep: no direct `Hecton8.Networking` reference in `FutureCommandSandboxValidator.cs`.
- BLOCKED compile/build: after CPU dropped below the gate, wrong `Assembly-CSharp.rsp` probe produced missing `Hecton8.Core` references. Correct `Hecton8.Core.rsp` compile reached non-owned errors in `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`; no `FutureCommandSandboxValidator.cs` errors were emitted before that wall. Roslyn child processes left after failed attempts were stopped/verified gone.

<SELF_AUDIT agent_id="SHINOBU_66" domain="MOD_SANDBOX_AND_OPCODE_VALIDATOR" date="2026-05-18">
  <TaskReconciliation total="20">
    <Task id="01" status="[PASS]">Archive/current-doc reconnaissance found no compatible `allowed_mod_opcodes.h8bin`; emergency 16B opcode records are generated into a Vault table.</Task>
    <Task id="02" status="[PASS]">Managed runtime patching path is quarantined; loader disables managed entry and exposes only `HectonAPI.Commands.RequestFuture` for active UGC commands.</Task>
    <Task id="03" status="[PASS]">`FutureCommandEnvelope` is fields-only; no `{ get; set; }` accessors.</Task>
    <Task id="04" status="[PASS]">Envelope and routing DTOs use explicit layouts aligned to 8/16/64B; no `Pack=1`.</Task>
    <Task id="05" status="[PASS]">`MockModQueue` and deterministic Burst malicious injection job can push corrupted envelopes without Inventory/AI dependencies.</Task>
    <Task id="06" status="[PASS]">Burst validator checks opcode table, XXHash3 integrity, AUP bounds, per-mod counters, CRC assets, and memory ranges with deterministic Burst flags.</Task>
    <Task id="07" status="[PASS]">Valid requests become unmanaged SignalBus payloads: spawn, asset, acoustic, damage, or DevNull future seam.</Task>
    <Task id="08" status="[PASS]">Unclaimed health/gravity/subtitle/read seams route to Vault DevNull ring plus DevNull signal.</Task>
    <Task id="09" status="[PASS]">Mod memory is isolated in `ShinobuModSandboxBlackboxMemory` with fixed per-signature leases.</Task>
    <Task id="10" status="[PASS]">Flood control uses 64B per-signature counters and fixed pending ring eviction.</Task>
    <Task id="11" status="[PASS]">Continuous quality scales per-signature budget and drops overheated backlog below quality 0.3.</Task>
    <Task id="12" status="[PASS]">AUP is finite-checked and rejected outside +/-50000m before routing.</Task>
    <Task id="13" status="[PASS]">Rollback resimulation freezes intake through local Vault flag view; no direct Networking assembly reference remains.</Task>
    <Task id="14" status="[PASS]">Asset references require approved CRC32 and byte ceiling before SignalBus output.</Task>
    <Task id="15" status="[PASS]">Fauna opcodes become acoustic/damage stimuli only, never direct AI commands.</Task>
    <Task id="16" status="[PASS]">Persistent runtime state is Vault-owned with `UninitializedMemory` plus explicit `MemClear`; validator stores handles only.</Task>
    <Task id="17" status="[PASS]">300-frame telemetry ring writes 64B entries and dumps `Dump_QUARANTINE_SURGEON.bin` on fault/layout violation.</Task>
    <Task id="18" status="[PASS]">Editor facade exists at `HECTON-8/Mod API Sandbox Tuner`.</Task>
    <Task id="19" status="[PASS]">CSV opcode ingestor parses `NativeArray<byte>` with FNV/hex path, no `Split`/LINQ runtime parser.</Task>
    <Task id="20" status="[PASS]">Editor histogram draws incoming/rejected traffic with `EditorGUI.DrawRect` over a preallocated editor-only buffer.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>
    <Struct name="FutureCommandEnvelope" size="64" alignment="8/16">
      <Field name="OpcodeHash" offset="0" size="4" />
      <Field name="ModderSignature" offset="4" size="4" />
      <Field name="TargetAUP" offset="8" size="24" />
      <Field name="PayloadData" offset="32" size="16" />
      <Field name="IntegrityHash" offset="48" size="8" />
      <Field name="_pad0" offset="56" size="8" />
      <Math>4+4+24+16+8+8=64; 64 % 16 = 0.</Math>
    </Struct>
    <Struct name="ModderFrameCounter" size="64" false_sharing_padding="true">
      <Field name="ModderSignature" offset="0" size="4" />
      <Field name="Frame" offset="4" size="4" />
      <Field name="Count" offset="8" size="4" />
      <Field name="Dropped" offset="12" size="4" />
      <Field name="Reserved0..5" offset="16" size="48" />
      <Math>16 hot bytes + 48 pad bytes = 64; one L1 cache line.</Math>
    </Struct>
    <Struct name="FutureCommandValidationStats" size="64" false_sharing_padding="true">
      <Math>Eight 4B counters/masks through byte 31, four 4B reserved words through byte 47, two 8B pads through byte 63.</Math>
    </Struct>
    <Struct name="ModSandboxRingState" size="64" false_sharing_padding="true">
      <Math>Pending/devnull ring cursors, counts, lease/opcode/asset counters, last dump frame, and 16B final padding total 64B.</Math>
    </Struct>
  </StructLayoutVerification>
  <ScalabilityCurve>
    `ResolveScaledCommandBudget` computes `round(lerp(10, MaxCommandsPerFrame, saturate(GlobalQualityWeight)))`. Below quality 0.3, `DropThermalBacklog` computes `thermalShed01 = saturate((0.30 - q) * 3.3333333)` and drops overflow above `max(20, maxCommandsPerSignature)`. At q=0.1, about 66.7% of overflow is discarded. At q>=0.3, no thermal shedding occurs. Expensive owner systems are not invoked; valid seams become cheap signals or DevNull.
  </ScalabilityCurve>
  <HPHIVaultStatus private_runtime_arrays="0" private_runtime_native_collections="0">
    <VaultBuffer id="ShinobuModSandboxPendingRing" type="FutureCommandEnvelope" length="4096" />
    <VaultBuffer id="ShinobuModSandboxDevNullRing" type="FutureCommandEnvelope" length="4096" />
    <VaultBuffer id="ShinobuModSandboxStaging" type="FutureCommandEnvelope" length="4096" />
    <VaultBuffer id="ShinobuModSandboxStats" type="FutureCommandValidationStats" length="1" />
    <VaultBuffer id="ShinobuModSandboxOpcodeRecords" type="FutureCommandOpcodeRecord" length="32" />
    <VaultBuffer id="ShinobuModSandboxModCounters" type="ModderFrameCounter" length="128" />
    <VaultBuffer id="ShinobuModSandboxMemoryLeases" type="ModderMemoryLease" length="128" />
    <VaultBuffer id="ShinobuModSandboxApprovedAssets" type="ApprovedAssetRecord" length="512" />
    <VaultBuffer id="ShinobuModSandboxRingState" type="ModSandboxRingState" length="1" />
    <VaultBuffer id="ShinobuModSandboxTuning" type="FutureCommandSandboxTuning" length="1" />
    <VaultBuffer id="ShinobuModSandboxBlackboxMemory" type="byte" length="16777216" />
    <VaultBuffer id="ShinobuModSandboxTelemetryRing" type="ModSandboxTelemetryEntry" length="300" />
    <VaultBuffer id="ShinobuModSandboxTelemetryCursor" type="int" length="1" />
  </HPHIVaultStatus>
  <PointerAliasingAndDependencyGraph>
    <Job name="ValidateFutureCommandEnvelopeJob" burst="CompileSynchronously=true FloatMode.Deterministic FloatPrecision.Standard" noalias="Inputs, Stats, OpcodeRecords, PerModCounters, MemoryLeases, ApprovedAssetManifest, ModderBlackboxMemory, DevNullRing, RingState" consumes="pending staging, opcode table, counters, leases, manifest, blackbox memory, ring state" outputs="stats, counters, blackbox memory, DevNull ring, SignalBus lanes" dependency="current ModCommandDispatcher PRE_SIMULATION is synchronous void; no arbitrary external Complete added" />
    <Job name="MockMaliciousEnvelopeInjectionJob" burst="CompileSynchronously=true FloatMode.Deterministic FloatPrecision.Standard" noalias="Output" consumes="ModderSignature" outputs="MockModQueue NativeQueue writer" />
  </PointerAliasingAndDependencyGraph>
  <CompileGuard direct_sibling_runtime_refs="false_for_validator">
    `FutureCommandSandboxValidator.cs` references Core, Core.Memory, Core.Contracts.Signals, Unity packages, and local Modding namespace only. Rollback freeze uses a local Vault flag view instead of `Hecton8.Networking`. Legacy `ModCommandDispatcher.cs` and `HectonAPI.cs` still contain pre-existing sibling usings outside the new validator surface.
  </CompileGuard>
  <DearLieConfirmation>
    The fake is the API boundary itself: external mod code serializes 64B intent packets, while the engine only validates math and emits DOD signals. Before: arbitrary managed callback/reflection/Harmony control can execute O(user code) with GC and rollback desync. After: O(n) bounded packet scan over fixed Vault rings/tables; unclaimed systems are O(1) DevNull writes.
  </DearLieConfirmation>
  <ResidualRisk>
    Full compile proof is blocked by unrelated existing `Hecton8.Core` dependency errors. Runtime profiler, Unity import, Play Mode, and headset proof remain pending.
  </ResidualRisk>
</SELF_AUDIT>

---

# SHINOBU_66 LOG - MOD SANDBOX FINAL FORENSIC APPEND - 2026-05-19

What was wrong:
- Prior log ordering contained older DRS residue for the duplicate `SHINOBU_66` prompt and later MOD entries were not all at the physical bottom.
- `MockModQueue` previously owned a persistent queue allocator seam.
- Scoped compile proof still cannot pass the non-owned `Hecton8.Core` dependency wall.

What was done:
- Active authority remains the later `role="MOD_SANDBOX_AND_OPCODE_VALIDATOR"` XML block.
- Current MOD source keeps 64B `FutureCommandEnvelope`, Vault-backed rings/tables, continuous `GlobalQualityWeight` budget/shedding, CRC/byte asset gate, rollback freeze via local Vault flag view, external queue drain, endian-normalized stream ingress, and editor tuner.
- Mock queue ownership is external-only: `MockModQueue.Wrap/Attach` accepts caller-owned queues; validator owns no persistent mock allocator state.

Cinematic Cheats used:
- UGC API is the Dear Lie: mods emit binary intent only. The engine executes no mod code, only validates math and routes DOD signals or DevNull.
- Fauna influence is acoustic/damage stimulus, not direct AI possession.
- Unclaimed future seams are accepted as valid intent and quarantined to DevNull to avoid owner-system compile coupling.

Exact Microseconds saved:
- `RequestRawEnvelopeStream` removes per-packet Vault resolve/state-write overhead; 10,000 packets become one Vault resolve and one final ring-state write.
- Managed UGC callback cost is removed from the active command path.
- Thermal shed drops stale backlog below quality 0.3 instead of paying delayed validation later.
- No measured profiler microseconds are claimed; runtime profiling is blocked until the unrelated compile wall is cleared.

Verification:
- PASS `git diff --check` on scoped source/docs; only CRLF warnings on tracked files.
- PASS static grep: no `new NativeQueue`, queue sentinel registration, or `Allocator.Persistent` remains in `FutureCommandSandboxValidator.cs`.
- PASS static grep: no private persistent native containers, no `NativeParallel*`, no bare `[BurstCompile]`, no `Stopwatch.StartNew`, no Harmony/BepInEx/reflection, no `Pack=1`, no LINQ hot path, and no direct sibling runtime using in `FutureCommandSandboxValidator.cs`. The only `NativeArray<T>` grep hits are resolver return types, not fields.
- BLOCKED scoped Roslyn: `Hecton8.Core.rsp` stops first on non-owned `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs` missing dependency types; no validator compiler error appears before that wall.

<SELF_AUDIT agent_id="SHINOBU_66" domain="MOD_SANDBOX_AND_OPCODE_VALIDATOR" date="2026-05-19" supersedes_prior_mod_audit="true">
  <TaskReconciliation total="20">
    <Task id="01" status="[PASS]">No compatible `allowed_mod_opcodes.h8bin`; emergency 16B opcode records are Vault-seeded.</Task>
    <Task id="02" status="[PASS]">Managed patch/callback entry is quarantined; active UGC enters through 64B envelopes.</Task>
    <Task id="03" status="[PASS]">`FutureCommandEnvelope` has public fields only.</Task>
    <Task id="04" status="[PASS]">Envelope/stats/counters/ring/telemetry layouts are explicit 64B or 16B aligned, no `Pack=1`.</Task>
    <Task id="05" status="[PASS]">Mock queue wraps caller-owned `NativeQueue`; malicious injection job emits corrupted envelopes without gameplay dependencies.</Task>
    <Task id="06" status="[PASS]">Deterministic Burst validator checks opcode, XXHash3 integrity, AUP, CRC/bytes, counters, and memory bounds.</Task>
    <Task id="07" status="[PASS]">Valid packets route to unmanaged SignalBus payloads.</Task>
    <Task id="08" status="[PASS]">Unclaimed future seams route to DevNull.</Task>
    <Task id="09" status="[PASS]">Mod memory is isolated in Vault blackbox memory via per-signature leases.</Task>
    <Task id="10" status="[PASS]">DoS flood is capped by 64B counters and fixed pending ring eviction.</Task>
    <Task id="11" status="[PASS]">`GlobalQualityWeight` continuously scales budgets and thermal shedding.</Task>
    <Task id="12" status="[PASS]">AUP is finite-checked and bounded to +/-50km.</Task>
    <Task id="13" status="[PASS]">Rollback freeze uses local 64B Vault flag view, no Networking runtime reference.</Task>
    <Task id="14" status="[PASS]">Assets require approved CRC32 and approved byte length.</Task>
    <Task id="15" status="[PASS]">Fauna commands become acoustic/damage stimuli only.</Task>
    <Task id="16" status="[PASS]">Persistent state is Vault-owned; validator stores handles only.</Task>
    <Task id="17" status="[PASS]">300-frame telemetry ring and fault dump path exist.</Task>
    <Task id="18" status="[PASS]">`HECTON-8/Mod API Sandbox Tuner` editor facade exists.</Task>
    <Task id="19" status="[PASS]">CSV opcode ingestor parses bytes without `Split`/LINQ runtime path.</Task>
    <Task id="20" status="[PASS]">Editor histogram draws incoming/rejected traffic via `EditorGUI.DrawRect`.</Task>
  </TaskReconciliation>
  <StructLayoutVerification primary="FutureCommandEnvelope" size="64">Offsets: OpcodeHash 0:4, ModderSignature 4:4, TargetAUP 8:24, PayloadData 32:16, IntegrityHash 48:8, _pad0 56:8. Math: 4+4+24+16+8+8=64, 64%16=0.</StructLayoutVerification>
  <ScalabilityCurve>Budget = round(lerp(10, MaxCommandsPerFrame, saturate(GlobalQualityWeight))). Below q=0.3, thermal shed = saturate((0.30-q)*3.3333333) and drops overflow above the safe window; at q=0.1 overflow shed is about 66.7%.</ScalabilityCurve>
  <HPHIVaultStatus private_runtime_arrays="0" private_runtime_native_collections="0">Uses `ShinobuModSandboxPendingRing`, `DevNullRing`, `Staging`, `Stats`, `OpcodeRecords`, `ModCounters`, `MemoryLeases`, `ApprovedAssets`, `RingState`, `Tuning`, `BlackboxMemory`, `TelemetryRing`, and `TelemetryCursor`.</HPHIVaultStatus>
  <PointerAliasingAndDependencyGraph>`ValidateFutureCommandEnvelopeJob` and `MockMaliciousEnvelopeInjectionJob` use deterministic Burst flags and `[NoAlias]` on native views/writer. Current dispatcher phase is synchronous void, so no fake async Complete chain was introduced.</PointerAliasingAndDependencyGraph>
  <CompileGuard>`FutureCommandSandboxValidator.cs` has no direct sibling runtime assembly reference; rollback uses Vault flag view.</CompileGuard>
  <DearLieConfirmation>Before: O(user managed code) callback/Harmony risk. After: O(n) bounded fixed-ring packet validation plus O(1) DevNull/signal routing.</DearLieConfirmation>
</SELF_AUDIT>
