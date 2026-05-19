Status: PENDING VERIFICATION
Agent: SHINOBU_104
Domain: Echelon 8 Presentation & UX

## 2026-05-19 Session Start
What was wrong: Assignment not yet mapped to current source; old DRS blur path suspected but not verified.
What was done: SHINOBU_104 prompt extracted, domain boundary read, mandates selected, status/rationale/log files created.
Cinematic Cheats used: None yet. Planned domain uses film grain, chromatic aberration, vignette, and edge-preserving reconstruction as visual fake for missing pixels.
Exact Microseconds saved: 0 us runtime at setup phase. Claims pending source audit and profiler evidence.

## 2026-05-19 Interim Reconstruction Pass
What was wrong: DRS still identified its fallback as BLTA and derived sharpening from inverse render-scale pressure. The renderer had no edge-preserving reconstruction pass, no Vault-backed reconstruction constants, no black-box ring for scale crashes, and no human facade for forcing 0.3x proof.
What was done: Added explicit reconstruction contracts, editor layout tests, bounded CAS-style DRS sharpen scalar, BILU upscaler hash, `Hecton_BilateralUpsample.shader`, RenderGraph reconstruction pass, CBuffer dispatcher, Vault handles 71030-71034, telemetry dump path, CSV aesthetic profiles, renderer-asset shader wiring, UI Toolkit `Uber Noir Tuner`, and continuous UberNoir material overkill bridge.
Cinematic Cheats used: 5-tap bilateral edge preservation, procedural film grain, vignette, chromatic offset, scalar-gated diagonal taps, and salt glints. Temporal history is fail-closed: shader/pass hooks exist, but runtime blend scalar remains 0 until a no-reallocation history owner exists.
Exact Microseconds saved: Estimated 40-90 us by rejecting CPU variance sharpening; estimated 50-180 us by keeping motion/history pass disabled without stable history; estimated 50-300 us hitch prevention by rejecting persistent RTHandle history allocation under DRS churn; variant churn avoided by removing unused binary visual-overkill keyword. Compile/profiler proof pending because CPU gate reports 100%.

<SELF_AUDIT status="INTERIM_NOT_COMPLETE">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Inverse sharpening removed; bounded CAS-style scalar installed.</TASK>
    <TASK id="02" status="PASS_STATIC">No forbidden blit/temp APIs in touched path; explicit TextureHandle used.</TASK>
    <TASK id="03" status="PASS_STATIC">New DTOs are fields-only; RuntimeState properties removed.</TASK>
    <TASK id="04" status="PASS_STATIC">48B constants, 32B mock, 64B telemetry layouts and editor tests added.</TASK>
    <TASK id="05" status="PASS_STATIC">MockReconstructionInputSignal and 0.3x editor/adapter path added.</TASK>
    <TASK id="06" status="PASS_STATIC">Bilateral HLSL shader added with 5/9 taps.</TASK>
    <TASK id="07" status="PARTIAL">Motion/history shader hook and Motion input gate exist; runtime history scalar is 0 pending stable history owner.</TASK>
    <TASK id="08" status="PASS_STATIC">Film grain, vignette, CA, salt glints added.</TASK>
    <TASK id="09" status="PASS_STATIC">GlobalQualityWeight drives reconstruction constants continuously.</TASK>
    <TASK id="10" status="PASS_STATIC">Overkill scalar drives extra taps/glints; material bridge now publishes continuous high-cost/overkill scalars for POM/SSS lanes.</TASK>
    <TASK id="11" status="PASS_STATIC">GraphicsBuffer constant upload uses LockBufferForWrite and MemCpy; RasterCommandBuffer API verified in package source.</TASK>
    <TASK id="12" status="PASS_STATIC">Jitter constant scales by inverse render scale.</TASK>
    <TASK id="13" status="PASS_STATIC">No double3/AUP work in reconstruction; depth remains local float screen-space.</TASK>
    <TASK id="14" status="PASS_STATIC">RenderGraph pass declares color/depth reads and output write.</TASK>
    <TASK id="15" status="PASS_STATIC">Unsupported CBuffer or missing temporal history fails closed.</TASK>
    <TASK id="16" status="PASS_STATIC">CBuffer allocated once; persistent runtime DTO memory is Vault-backed.</TASK>
    <TASK id="17" status="PASS_STATIC">300-entry telemetry ring and dump path added.</TASK>
    <TASK id="18" status="PASS_STATIC">Editor window added with Vault-first readback.</TASK>
    <TASK id="19" status="PASS_STATIC">CSV parser added with native scratch and hashed names.</TASK>
    <TASK id="20" status="PASS_STATIC">A/B split added.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="UberNoirReconstructionConstantsDTO" size="48">offset0 float4 RenderScaleParams; offset16 float4 TemporalParams; offset32 float4 OverkillParams. 3x16B lanes = 48B.</DTO>
    <DTO name="MockReconstructionInputSignal" size="32">offset0 RenderScale01; 4 GlobalQualityWeight01; 8 JitterPixels; 12 FrameTimeMs; 16 TemporalStress01; 20 Flags; 24 pad0; 28 pad1.</DTO>
    <DTO name="ReconstructionTelemetryEntry" size="64">offset0 Frame; 4 Flags; 8 CurrentScale; 12 TargetScale; 16 Sharpen; 20 Radius; 24 History; 28 Quality; 32 Grain; 36 Chroma; 40 Vignette; 44 Mode; 48 GpuMs; 52 Jitter; 56 pad0; 60 pad1.</DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below GlobalQualityWeight 0.3, temporal scalar is 0, shader stays at 5 taps, radius/grain/vignette rise smoothly, and noir noise masks missing pixels. High/Ultra increases clamp quality and enables diagonal taps/glints through `_H8OverkillParams.w`.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Private persistent NativeArray allocations added: zero. VaultBufferHandle IDs: 71030 constants, 71031 telemetry, 71032 profiles, 71033 CSV scratch, 71034 mock signal.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No Burst jobs added; no JobHandle chain changed. RenderGraph dependencies: read camera color, optional depth, optional motion only when temporal scalar is active; write reconstruction texture; optional visor post reads reconstruction texture.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new asmdef references added. Existing Hecton8.Core assembly has pre-existing sibling refs; SHINOBU_104 did not expand them.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Heavy clean reconstruction rejected. Complexity is O(pixels * taps), baseline 5 taps, overkill 9 taps; no CPU simulation, no object instantiation, no exp-weight loop.</DEAR_LIE_CONFIRMATION>
  <VERIFICATION_GAP>dotnet/Unity compile not launched because CPU measurement is 100% and user forbids build when CPU is above 50% or dotnet/csc is active.</VERIFICATION_GAP>
</SELF_AUDIT>

## 2026-05-19 Telemetry Truth Tightening
What was wrong: The `FALL` telemetry mode was present, but an inactive reconstruction pass could still inherit Dear Lie or A/B flags from constants, creating a misleading black-box sample.
What was done: Telemetry now computes `reconstructionActive` from material + valid CBuffer. `FALL` writes only fallback facts; temporal, Dear Lie, and A/B flags are emitted only when the reconstruction pass can actually execute. Static forbidden scan remains clean. `git diff --check` reports only CRLF warnings.
Cinematic Cheats used: None added. This is forensic truth tightening for the existing bilateral/noir fake path.
Exact Microseconds saved: 0 us claimed. Build not launched because latest CPU gate is 94.0% and no dotnet/csc process is active.

## 2026-05-19 Temporal Fail-Closed Binding Polish
What was wrong: A narrow runtime race remained possible: constants could be built from a previously readable `RawColorHistory`, then `RecordRenderGraph` could fail to import it and leave `_H8ReconstructionHistoryTex` unbound while a stale temporal scalar opened the shader branch.
What was done: Hardened reconstruction pass bindings. During raw-history warm-up, missing history or missing motion now binds source color as history and RenderGraph default black texture as motion. Real temporal accumulation still requires imported URP `RawColorHistory` plus a valid `motionVectorColor`; the fallback only prevents black/garbage sampling. Removed the external call to protected-internal `ScriptableRenderer.SupportsMotionVectors()`. Depthless TBDR reconstruction now binds RenderGraph black texture as `_CameraDepthTexture`, collapsing depth weighting to luma/spatial instead of sampling stale depth. Editor CBuffer readback now locks the Vault buffer before resolving the pointer. CBuffer allocation is now cold-only through `Create()`; frame path fails closed without requesting history/motion if the buffer is unavailable. CSV profile loading is now a one-shot cold/delayed attempt, not repeated frame-loop filesystem polling. Mock jitter/stress lanes now actually affect CBuffer jitter and temporal trust. The legacy visor heat-haze, bullet-time, waterline, global wake, mock caustic, and fallback-overkill low-tier gates now consume continuous GlobalQualityWeight instead of hard low/Ultra binaries. Reconstruction telemetry now records `FALL` when the CBuffer is absent.
Cinematic Cheats used: Source-as-history is used only as a fail-closed safety illusion, not as a normal temporal route. If triggered, the shader blends current-to-current and preserves spatial bilateral/noir output.
Exact Microseconds saved: 0 us claimed. This is correctness hardening. Current static scans show no forbidden temp/blit/Pack=1/string-split/random/old-BLTA patterns in touched files. Build remains blocked by CPU=98.2 with no dotnet/csc process.

<SELF_AUDIT status="INTERIM_FAIL_CLOSED_POLISH_NOT_UNITY_VERIFIED">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="PASS_STATIC">Temporal history now has both owner-local RawColorHistory import and safety binding when import is absent during warm-up.</TASK>
    <TASK id="15" status="PASS_STATIC">Fallback cannot sample an unbound history texture; source+black-motion collapses temporal to current-frame output.</TASK>
    <TASK id="16" status="PASS_STATIC">GraphicsBuffer allocation is no longer reachable from AddRenderPasses or constants update.</TASK>
    <TASK id="19" status="PASS_STATIC">Missing/late CSV no longer causes repeated frame-loop File.Exists/FileStream probes.</TASK>
    <TASK id="05" status="PASS_STATIC">Mock jitter/stress now reaches reconstruction constants instead of scale-only proof.</TASK>
    <TASK id="09" status="PASS_STATIC">Touched visor heat-haze gate now scales from GlobalQualityWeight instead of a binary low-tier switch.</TASK>
    <TASK id="10" status="PASS_STATIC">GlobalShaderDispatcher wake/caustic/overkill lanes now use quality-weight curves and exact Burst flags.</TASK>
    <TASK id="18" status="PASS_STATIC">Editor tuner readback now follows Vault lock discipline.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <RENDERGRAPH_DEPENDENCIES>When history, motion, and depth import, reconstruction reads color/depth/motion/history. Missing depth binds default black depth. Missing history or motion during warm-up binds source history plus default black motion.</RENDERGRAPH_DEPENDENCIES>
  <VERIFICATION_GAP>Unity import, shader compile, RenderGraph Viewer, Frame Debugger, and profiler proof remain pending. dotnet build remains forbidden by CPU gate.</VERIFICATION_GAP>
</SELF_AUDIT>

## 2026-05-19 Temporal History Repair
What was wrong: Task 07 was still partial. The shader hook existed, but runtime blend stayed inert because no stable history owner had been proven. The earlier shader reprojection also used the wrong sign for URP motion vectors and scaled motion by jitter pixels.
What was done: Bound URP-owned `RawColorHistory` as the temporal source. The reconstruction pass now requests `RawColorHistory`, imports the previous raw-color RTHandle into RenderGraph, binds `_H8ReconstructionHistoryTex`, binds `_MotionVectorTexture`, and enables `TemporalParams.z` only when previous history is readable and the renderer supports motion vectors. The raw-history request is gated by a smooth GlobalQualityWeight warm-up curve. Shader reprojection now follows URP TAA convention: `historyUv = uv + motion * motionScale`.
Cinematic Cheats used: Still no feature-owned history simulation. Weak hardware stays on spatial bilateral/noir masking and does not pay for URP raw-history copies until the continuous quality curve opens temporal work.
Exact Microseconds saved: Estimated 50-300 us hitch risk avoided by rejecting private RTHandle history under DRS. Temporal high-end cost remains estimated +0.05 ms to +0.18 ms only after history/motion availability.

<SELF_AUDIT status="INTERIM_TEMPORAL_REPAIR_NOT_UNITY_VERIFIED">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="PASS_STATIC">URP RawColorHistory previous texture and motion vector texture are bound through RenderGraph; temporal scalar remains fail-closed until both are available.</TASK>
    <TASK id="12" status="PASS_STATIC">Motion reprojection now uses URP velocity sign and the temporal motion scalar; jitter scale is no longer misused as velocity multiplier.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <HISTORY_OWNERSHIP>Owner is URP camera history manager, not SHINOBU_104. No private RTHandle history allocation was added; raw history requests are suppressed on collapsed low-quality paths.</HISTORY_OWNERSHIP>
  <RENDERGRAPH_DEPENDENCIES>Reconstruction reads active color, optional depth, optional motionVectorColor, optional imported RawColorHistory previous texture; writes _HectonUberNoirReconstruction.</RENDERGRAPH_DEPENDENCIES>
  <VERIFICATION_GAP>Static scans passed and git diff --check reports only line-ending warnings. dotnet/Unity compile still blocked because CPU recheck rose from 97.7 to 100 and user forbids build over 50% CPU.</VERIFICATION_GAP>
</SELF_AUDIT>

## 2026-05-19 Build Attempt Blocked By External World File
What was wrong: Compile verification was needed, CPU gate briefly cleared, and no dotnet/csc processes were active.
What was done: Ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. Build failed before SHINOBU_104 proof on `CS2001` because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is referenced by `Hecton8.Core.csproj` but deleted in the working tree with its `.meta`. This is outside Echelon 8 Presentation & UX. No dummy file, project edit, or revert was made.
Cinematic Cheats used: None.
Exact Microseconds saved: 0 us. Verification wall is integration state, not runtime performance.

## 2026-05-19 Burst Alias Polish
What was wrong: `MockGlobalShaderDataJob` in the touched global shader dispatcher had a `NativeArray<float4>` slot buffer without `[NoAlias]`.
What was done: Added `[NoAlias]` to the slot buffer and reran targeted Burst/NoAlias scan. The two ThermalDynamicResolutionAdapter jobs and the dispatcher job now show exact Burst flags plus NoAlias where NativeArray pointers exist.
Cinematic Cheats used: None.
Exact Microseconds saved: Small compiler-enablement only, estimated 0-3 us on the mock/global shader data path depending Burst vectorization.

## 2026-05-19 URP History Timing And ABI Polish
What was wrong: RawColorHistory was still vulnerable to a URP timing bug if requested during RenderGraph recording, shader telemetry used `Sequential, Pack=4`, the editor facade directly cast to `ThermalDynamicResolutionAdapter`, constants were redundantly bound through `Shader.SetGlobalConstantBuffer` outside the RenderGraph pass, and material overkill still had a hard `step(0.8, stress)` visual cliff.
What was done: Registered RawColorHistory through `ICameraHistoryReadAccess.OnGatherHistoryRequests`, left RenderGraph to import only already-written history, changed fullscreen shader vertex code to URP/Core helpers, inherited source texture format for reconstruction/post passes, converted `UberNoirShaderTelemetryEntry` to explicit 48B layout, added layout tests for mock quality and shader telemetry, removed the external global CBuffer bind, routed editor mock writes through Vault buffer 71034 with `Flags=0` clearing when disabled, and replaced the material overkill stress step with `H8UberNoirSmoothRange01(0.72, 0.88, stress)`.
Cinematic Cheats used: Source-as-history remains a safety-only illusion during warm-up; normal weak-device output is still 5-tap bilateral plus film grain/vignette/chromatic masking.
Exact Microseconds saved: <1 us from removing redundant global CBuffer binding; 0 us claimed for URP timing, ABI, XR, and format fixes. Current verification is blocked: `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is missing outside this domain, dotnet process 19436 is running, and CPU reports 100.

<SELF_AUDIT status="INTERIM_LOOP_8_NOT_UNITY_VERIFIED">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="05" status="PASS_STATIC">Editor mock now writes the `MockReconstructionInputSignal` Vault payload directly and clears stale mock flags.</TASK>
    <TASK id="07" status="PASS_STATIC">RawColorHistory request timing now uses URP's external history request event; RenderGraph only imports readable previous textures.</TASK>
    <TASK id="11" status="PASS_STATIC">CBuffer upload remains `LockBufferForWrite`/`MemCpy`; binding is now only inside the RenderGraph raster pass.</TASK>
    <TASK id="14" status="PASS_STATIC">Reconstruction/post descriptors inherit source color format; fullscreen shader uses URP/Core helpers.</TASK>
    <TASK id="15" status="PASS_STATIC">Format/XR fail-closed risk reduced by avoiding forced B10G11R11 and manual UV generation.</TASK>
    <TASK id="18" status="PASS_STATIC">Editor facade no longer depends on the concrete scalability runtime class for mock input.</TASK>
    <TASK id="10" status="PASS_STATIC">Material overkill stress shedding is now a smooth shader-constant curve, not `step(0.8, stress)`.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="UberNoirShaderTelemetryEntry" size="48">offset0 Frame; 4 FeatureMask; 8 SystemStress01; 12 HighCostAllowed01; 16 VisualOverkill01; 20 QualityTier; 24 Flags; 28 StateHash; 32 PomEnabled01; 36 SecondaryCaustics01; 40 Refraction01; 44 Reserved0.</DTO>
    <DTO name="MockQualityWeightSignal" size="16">offset0 GlobalQualityWeight; 4 FrameTimeMs; 8 Flags; 12 pad0.</DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>Persistent SHINOBU_104 buffers remain owner-local in Vault: 71030 constants, 71031 telemetry ring, 71032 aesthetic profiles, 71033 CSV scratch, 71034 mock signal.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>Loop 8 removed the tuner-to-`ThermalDynamicResolutionAdapter` concrete dependency. No asmdef references were added.</COMPILE_GUARD>
  <VERIFICATION_GAP>Static scans passed except the expected RawColorHistory request callback. Build/Unity import not rerun because a dotnet process is active, CPU is 100, and the external World/MapMagic source deletion still blocks `Hecton8.Core.csproj`.</VERIFICATION_GAP>
</SELF_AUDIT>

## 2026-05-19 Mock Scale Authority Repair
What was wrong: The Loop 8 decoupled editor mock route could become shader-only. It wrote the 32B mock DTO for reconstruction constants, but the actual DRS scaler did not read the same Vault fact, so a 0.3x proof could lie about internal render scale.
What was done: Added `UberNoirReconstructionVaultIds` to `DrsContracts.cs` as the single contract owner for IDs 71030-71034 and pinned them with an EditMode test. `HectonVisorUberPostFeature` now consumes those IDs. `ThermalDynamicResolutionAdapter` now reads Vault buffer 71034 under lock each tick and consumes the same `MockReconstructionInputSignal`; active flags force current/target/next render scale and system scale percentage, default zeroed buffers are ignored until a mock was active, and `Flags=0` clears only the reconstruction mock lane.
Cinematic Cheats used: None added. This repairs the isolated proof path for the existing bilateral/noir fake.
Exact Microseconds saved: 0 us. This adds one 32B locked DTO read when the mock buffer exists; runtime payoff is proof correctness, not speed. Build not rerun because the external World/MapMagic source file is still missing and would reproduce `CS2001`.

<SELF_AUDIT status="INTERIM_LOOP_9_NOT_UNITY_VERIFIED">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="05" status="PASS_STATIC">Mock reconstruction input now forces real DRS scaler state and shader constants from one Vault DTO.</TASK>
    <TASK id="09" status="PASS_STATIC">Mock quality uses the same continuous `GlobalQualityWeight01` lane without a low/high switch.</TASK>
    <TASK id="16" status="PASS_STATIC">No private array storage was added; scaler caches only a VaultBufferHandle for buffer 71034.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <H_PHI_VAULT_STATUS>Contract-owned Vault IDs: 71030 constants, 71031 telemetry, 71032 aesthetic profiles, 71033 CSV scratch, 71034 mock signal.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>The editor facade still has no concrete `ThermalDynamicResolutionAdapter` dependency. The scaler and visor communicate through `Core.Contracts` DTO plus Vault buffer ID.</COMPILE_GUARD>
  <VERIFICATION_GAP>Static scans are clean. Build/Unity proof remains blocked by missing `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` outside SHINOBU_104 ownership.</VERIFICATION_GAP>
</SELF_AUDIT>

## 2026-05-19 Shader Overkill Curve Polish
What was wrong: The bilateral shader had a hard `_H8OverkillParams.w > 0.35` threshold for diagonal taps. That contradicted the continuous GlobalQualityWeight law and could create a visible 5-tap to 9-tap pop.
What was done: Replaced the threshold with `SmoothRange01(0.25, 0.85, _H8OverkillParams.w)` and multiplied diagonal contribution by that scalar. The remaining branch is a near-zero ALU skip only. Static scan shows no old hard threshold, no `_H8_VISUAL_OVERKILL`, no `_LowTierMode`, and no `step(0.8, stress)` in touched shader paths.
Cinematic Cheats used: The Dear Lie remains bilateral edge preservation plus grain/vignette/chromatic masking; high-end diagonal taps now fade in as visual overkill instead of flipping on.
Exact Microseconds saved: 0 us CPU. GPU diagonal work is skipped on collapsed quality and ramps for High/Ultra; no profiler number claimed.

## 2026-05-19 Vault Lock Discipline Polish
What was wrong: Runtime mock constants and aesthetic profile selection still resolved Vault-backed `NativeArray` views without a lock while editor mock writes and cold CSV ingest could touch the same buffers.
What was done: `TryReadMockReconstructionSignal` now locks buffer 71034 for the 32B copy. `TryLoadAestheticCsvCold` locks CSV scratch 71033 and profiles 71032 while reading/parsing, and missing Vault or transient lock failure no longer burns the one-shot CSV attempt before delayed Vault availability. `TryResolveAestheticProfile` locks 71032 while scanning profiles for the current depth/sanity slice. Static forbidden scan remains clean; `git diff --check` reports CRLF warnings only on the touched Visor file.
Cinematic Cheats used: None added. This protects the existing CSV-authored noir reconstruction fake from stale or torn profile reads.
Exact Microseconds saved: 0 us. Current build proof remains blocked: `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is still missing, dotnet process 16624 is active, and CPU reports 91.1%.

## 2026-05-19 DRS Quality Vault Read Lock
What was wrong: The DRS adapter read `BufferID.ShinobuScalabilityState` through a raw Vault pointer without taking a lock. That float is the continuous authority for render scale and reconstruction aggressiveness, so a read-side race could create a visual/scale discontinuity even if the shader path was clean.
What was done: `ThermalDynamicResolutionAdapter.TryReadScalabilityStateQualityWeight` now locks `ShinobuScalabilityState`, resolves/copies the 4B float inside the lock, and unlocks in `finally` on null pointer, non-finite value, zero boot value, and success paths. No DRS math or fallback order changed.
Cinematic Cheats used: None added. This preserves the existing bilateral/noir fake by protecting the scalar that controls it.
Exact Microseconds saved: 0 us. Adds a short owner-tagged lock around a 4B read; correctness repair only. Static forbidden scan is clean. Build not run: `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is still missing and CPU reports 100.

## 2026-05-19 Continuous Shader Feature Telemetry
What was wrong: The UberNoir black-box ring stored POM, secondary caustics, and refraction lanes as 0/1 bitmask facts even though shader constants fade those features continuously through `highCostAllowed01`.
What was done: `UberNoirShaderTelemetryEntry.PomEnabled01`, `SecondaryCaustics01`, and `Refraction01` now store the continuous high-cost scalar. The state hash also mixes stress, high-cost, and overkill buckets so scalar drift appears in crash dumps. Feature-mask thresholds now use `FeatureMaskEpsilon` presence checks instead of hard 0.5 gates; shader cost remains controlled by scalar constants.
Cinematic Cheats used: None added. This makes the forensic record match the existing shader-constant visual fake/overkill curve.
Exact Microseconds saved: 0 us. Late-frame telemetry correctness only.

## 2026-05-19 Global Shader CSV Hot-Path Eviction
What was wrong: `GlobalShaderDispatcher.LateFrameTick` polled `shader_globals_override.csv`, making filesystem checks and a possible 4KB read reachable from VISUAL_SYNC.
What was done: Removed CSV polling from `LateFrameTick`. The parser path is now `TryLoadCsvOverridesCold()` and runs from `Awake` only. Existing manual/editor overrides and Vault shader slots remain the runtime path. Also split legacy binary archaeology from hot fallback: `RunBinaryGraveyardProbeCold` may inspect files during cold boot, while `LateFrameTick` only calls `GenerateEmergencyMockShaderGlobalsNoIo`.
Cinematic Cheats used: None added. This protects the existing noir shader scalar fake from IO jitter.
Exact Microseconds saved: Avoids a periodic filesystem stat every former poll, a possible 4KB read, and a delayed-Vault first-frame legacy-file probe. On weak storage this is spike removal rather than deterministic per-frame ALU saving.

## 2026-05-19 DRS Visual Flag Presence Semantics
What was wrong: DRS visual overkill weights were continuous, but the companion feature flags flipped at `math.step(0.5f, weight)`.
What was done: `ResolveVisualFeatureFlags` now uses `VisualFeatureFlagEpsilon` presence checks. `_H8VisualFeatureWeights0/1` remain the only magnitude controls.
Cinematic Cheats used: None added. The existing overkill/noir fake now has flag telemetry that matches the scalar ramp instead of a half-scale cliff.
Exact Microseconds saved: 0 us. This is control/telemetry correctness; runtime cost is negligible. Static scan confirms no `math.step(0.5f, weights...)`, no hot `RefreshCsvOverrides`, and no forbidden temp/blit/Pack/string-split/random/global-CBuffer-bind patterns in touched files. Build not run because the external World/MapMagic file is still absent.

## 2026-05-19 Global Keyword Churn Removal
What was wrong: `GlobalShaderDispatcher.LateFrameTick` still mapped low/high tier state to global shader keyword flips.
What was done: Removed the keyword constants and `Shader.EnableKeyword`/`Shader.DisableKeyword` calls. Tier changes now refresh telemetry only; visual behavior continues through scalar globals.
Cinematic Cheats used: None added. The existing visual fake remains scalar-driven.
Exact Microseconds saved: No stable per-frame number claimed. Removes keyword/variant state churn on tier transitions.

<SELF_AUDIT agent_id="SHINOBU_104" status="PENDING_UNITY_COMPILE_AND_VISUAL_VERIFICATION">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS_STATIC">Naive inverse-scale sharpening replaced with bounded CAS-style scalar.</TASK>
    <TASK id="02" status="PASS_STATIC">Touched reconstruction path uses RenderGraph textures; temp/blit/unsafe scans clean.</TASK>
    <TASK id="03" status="PASS_STATIC">Hot DTOs use public fields; no property wrappers added.</TASK>
    <TASK id="04" status="PASS_STATIC">Reconstruction constants DTO is explicit 48B; mock signal 32B; telemetry 64B.</TASK>
    <TASK id="05" status="PASS_STATIC">Mock reconstruction signal routes through Vault 71034 and forces real DRS scale.</TASK>
    <TASK id="06" status="PASS_STATIC">Bilateral upsampler shader provides 5-tap baseline with scalar-gated diagonal overkill.</TASK>
    <TASK id="07" status="PASS_STATIC">URP RawColorHistory plus motion vector hooks are bound fail-closed through RenderGraph.</TASK>
    <TASK id="08" status="PASS_STATIC">Film grain, vignette, chroma, and salt glints mask low-scale artifacts as noir camera imperfection.</TASK>
    <TASK id="09" status="PASS_STATIC">Radius, history, sharpness, grain, chroma, and overkill consume continuous quality/scale signals.</TASK>
    <TASK id="10" status="PASS_STATIC">Visual overkill is scalar-driven; hard overkill thresholds and global keyword churn removed in touched paths.</TASK>
    <TASK id="11" status="PASS_STATIC">Reconstruction constants upload through one constant GraphicsBuffer and pass-local RenderGraph CBuffer bind.</TASK>
    <TASK id="12" status="PASS_STATIC">Jitter scales inversely with render scale; motion reprojection uses URP velocity sign.</TASK>
    <TASK id="13" status="PASS_STATIC">Reconstruction remains UV/clip/depth float-space; no AUP double3 reconstruction added.</TASK>
    <TASK id="14" status="PASS_STATIC">RenderGraph pass declares color/depth/motion/history reads and reconstruction/post writes.</TASK>
    <TASK id="15" status="PASS_STATIC">Unsupported CBuffer/history/depth paths fail closed to source/black fallbacks, not black frames.</TASK>
    <TASK id="16" status="PASS_STATIC">Persistent reconstruction storage is Vault-owned; CBuffer allocation is cold only.</TASK>
    <TASK id="17" status="PASS_STATIC">300-entry 64B reconstruction telemetry ring and dump path implemented.</TASK>
    <TASK id="18" status="PASS_STATIC">Uber Noir Tuner editor facade writes constants/mock through owner-local routes.</TASK>
    <TASK id="19" status="PASS_STATIC">Noir aesthetic CSV parser reads cold FileStream into Vault scratch and hashes tokens.</TASK>
    <TASK id="20" status="PASS_STATIC">A/B split renders raw left and reconstructed right through shader scalar.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="UberNoirReconstructionConstantsDTO" size="48" alignment="16">0-15 RenderScaleParams float4; 16-31 TemporalParams float4; 32-47 OverkillParams float4.</DTO>
    <DTO name="MockReconstructionInputSignal" size="32" alignment="16">0 RenderScale01; 4 GlobalQualityWeight01; 8 JitterPixels; 12 TemporalStress01; 16 Flags; 20 SectorHash; 24 SimulationFrame; 28 _pad0.</DTO>
    <DTO name="ReconstructionTelemetryEntry" size="64" alignment="64">0 Frame; 4 ModeHash; 8 RenderScale01; 12 TargetScale01; 16 Sharpness01; 20 HistoryWeight01; 24 EstimatedGpuMs; 28 Flags; 32 Constants0; 48 Constants1.</DTO>
    <DTO name="UberNoirShaderTelemetryEntry" size="48" alignment="16">0 Frame; 4 FeatureMask; 8 SystemStress01; 12 HighCostAllowed01; 16 VisualOverkill01; 20 QualityTier; 24 Flags; 28 StateHash; 32 PomEnabled01; 36 SecondaryCaustics01; 40 Refraction01; 44 Reserved0.</DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below GlobalQualityWeight 0.3, temporal history remains collapsed unless URP history and motion are proven, diagonal taps are near-zero skipped, wake upload capacity trends to 4 slots, film grain/vignette/chroma rise, and bilateral radius/sharpness increase through smooth polynomial curves. Above 0.8, temporal blend, diagonal taps, salt glints, and UberNoir high-cost material lanes fade in through scalar constants, not keywords.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_arrays="ZERO_FOR_RECONSTRUCTION">
    <BUFFER id="71030" name="UberNoirReconstructionConstantsDTO[1]" />
    <BUFFER id="71031" name="ReconstructionTelemetryEntry[300]" />
    <BUFFER id="71032" name="NoirAestheticProfileDTO[16]" />
    <BUFFER id="71033" name="CSV scratch byte[16384]" />
    <BUFFER id="71034" name="MockReconstructionInputSignal[1]" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>
    Burst jobs touched in this scope use exact compile flags and NoAlias on pointer/NativeArray fields. Reconstruction RenderGraph consumes active color, optional depth, optional motion, optional RawColorHistory previous texture, and the constants CBuffer; outputs reconstruction texture then visor post texture. No arbitrary JobHandle.Complete was added.
  </POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>
    SHINOBU_104 communication uses Core.Contracts DTOs, Vault buffer IDs, GlobalRegistry service contracts, and shader scalar IDs. The editor mock facade no longer casts to ThermalDynamicResolutionAdapter. No asmdef reference was added. Visor legacy Gameplay/Physics concrete references remain pre-existing and were not expanded.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The low-device path rejects expensive clean reconstruction and uses a 5-tap bilateral edge-preserving upsample plus film grain, vignette, chromatic offset, and salt glints. Before: full temporal/FSR-style reconstruction pressure would be O(history + motion + high taps). After: weak path is O(5 taps) plus cheap procedural noise; high path scales toward O(9 taps + temporal) only as quality permits.
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION_GAP>
    Source/static scans pass. Unity import, shader compile, RenderGraph viewer, frame debugger, and profiler proof remain blocked. Build is intentionally skipped because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is absent and `Hecton8.Core.csproj` will fail with external CS2001 before SHINOBU_104 diagnostics.
  </VERIFICATION_GAP>
</SELF_AUDIT>
