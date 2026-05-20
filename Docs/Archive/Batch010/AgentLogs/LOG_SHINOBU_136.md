# SHINOBU_136 Agent Log

<SELF_AUDIT agent_id="SHINOBU_136" status="STATIC_VERIFIED_COMPILE_BLOCKED" date="2026-05-19">
  <SUMMARY>
    <WHAT_WAS_WRONG>Player presentation retained a Unity Animator-facing swim parameter route and PlayerKinematicsRuntime still had a cold path to ContextualPhysicalIkRig, a legacy Animator/PlayableGraph bridge. KineticCharacter also initially risked consuming Gameplay implementation DTOs directly and inherited producer frame metadata that can be backed by Unity Time.frameCount.</WHAT_WAS_WRONG>
    <WHAT_WAS_DONE>Replaced player animation output with Burst/Vault procedural matrix generation, removed the player route to ContextualPhysicalIkRig, removed Animator parameter writes from PlayerSwimPresentationController, added SDF wall-bracing IK, deterministic sine/triangle breathing, damage flinch, tool alignment, GPU matrix upload, editor tuning, CSV ingestion, black-box telemetry, and architecture documentation.</WHAT_WAS_DONE>
    <CINEMATIC_CHEATS>Breathing is a scalar sine/triangle wave, wall contact is byte-SDF sampling with optional 6-tap gradient, and the renderer consumes flat matrices instead of Transform/Animator graph traversal.</CINEMATIC_CHEATS>
    <COMPILE_STATUS>FAIL-COMPILE-GATE: build intentionally not launched. Guard sample: 7 external dotnet MSBuild processes were active and CPU was 100%, violating the AGENTS rule forbidding dotnet build when CPU exceeds 50% or another dotnet/csc is running.</COMPILE_STATUS>
  </SUMMARY>

  <TASK_RECONCILIATION>
    <TASK id="01" name="ANIMATOR_COMPONENT_ERADICATION" result="PASS">Player prefab no longer serializes swimAnimator. PlayerSwimPresentationController no longer calls Animator.StringToHash or Animator.SetFloat. PlayerKinematicsRuntime no longer resolves or calls ContextualPhysicalIkRig.</TASK>
    <TASK id="02" name="PHYSICS_RAYCAST_PURGE" result="PASS">Kinetic wall awareness samples BufferID.VoxelSdfTexture3D in Burst. Static scan found no Physics.Raycast in KineticCharacter or the player bridge surface.</TASK>
    <TASK id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS">Hot DTOs expose public fields only. No getter/setter properties were found in the KineticCharacter DTO/job files.</TASK>
    <TASK id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS">Explicit layouts are 32/64/128/192/272 byte aligned. ProceduralBoneDTO is exactly 64 bytes.</TASK>
    <TASK id="05" name="EMERGENCY_MOCK_KINEMATIC_DATA" result="PASS">MockCharacterKinematicsJob plus GenerateEmergencyMockRig provide deterministic fallback data without waiting on KCC authored rig input.</TASK>
    <TASK id="06" name="BURST_LOCOMOTION_PHASE_KERNEL" result="PASS">ProceduralLocomotionPhaseJob computes root, spine, head, arms, legs, and tool socket matrices from deterministic math.</TASK>
    <TASK id="07" name="SDF_WALL_BRACING_IK" result="PASS">EvaluateWallProximityJob generates arm targets from SDF surface proximity and feeds two-bone FABRIK solve.</TASK>
    <TASK id="08" name="THE_DEAR_LIE_BREATHING_BOB" result="PASS">Breathing bob is scalar wave math modulated by oxygen/quality, not clip or physics simulation.</TASK>
    <TASK id="09" name="ASYNCHRONOUS_MATRIX_UPLOADER" result="PASS">Jobs write float4x4 matrices to Vault; LateFrameTick uploads to double GraphicsBuffer after job completion.</TASK>
    <TASK id="10" name="CONTINUOUS_SCALABILITY_IK_ITERATIONS" result="PASS">GlobalQualityWeight drives IK iterations and active bone count through math.lerp/step/smooth curves.</TASK>
    <TASK id="11" name="WEAPON_AND_TOOL_ALIGNMENT" result="PASS">Tool pose, weight, and active tool hash are submitted from the player bridge; right hand/tool socket alignment and hash-derived support grip are solved with matrix math when finite and nonzero.</TASK>
    <TASK id="12" name="AUP_SECTOR_RELATIVE_ROOT" result="PASS">Root/contact positions subtract observer sector/local first, then cast to float3 for IK math.</TASK>
    <TASK id="13" name="PROCEDURAL_DAMAGE_FLINCH" result="PASS">Damage impulses feed deterministic decaying spine/neck flinch; no flinch clip route was added.</TASK>
    <TASK id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS">Kinetic jobs use FloatMode.Deterministic. Solver frame identity is runtime-owned _frameCounter; producer Time.frameCount metadata is ignored. No UnityEngine.Random remains on the edited route.</TASK>
    <TASK id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS">Large overwritten Vault buffers use NativeArrayOptions.UninitializedMemory; telemetry/cursor safety buffers use ClearMemory.</TASK>
    <TASK id="16" name="TELEMETRY_ANIMATION_RECORDER" result="PASS">300-entry KineticAnimationTelemetryEntry ring and Dump_KINETIC_ANIMATOR.bin route were added.</TASK>
    <TASK id="17" name="ANIMATION_TUNER_EDITOR_WINDOW" result="PASS">Procedural Animation Tuner editor facade exposes layout, readout, sliders, mock rig, and CSV load. It is under Editor/ and wrapped in #if UNITY_EDITOR.</TASK>
    <TASK id="18" name="CSV_RIG_RULES_INGESTOR" result="PASS">character_rig_constraints.csv is parsed through span/FNV code; no string.Split, LINQ, or foreach on the edited route.</TASK>
    <TASK id="19" name="LIVE_RIG_DEBUG_GIZMO" result="PASS">OnDrawGizmosSelected reads Vault matrices/parents and draws rig lines without runtime GameObjects.</TASK>
    <TASK id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="FAIL-COMPILE-GATE">Static audit is written here. Compile proof remains blocked by the CPU gate; latest sample is CPU 100%, with no dotnet/csc process visible on that sample, so no build was launched.</TASK>
  </TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION>
    <PRIMARY_DTO name="ProceduralBoneDTO" size="64" alignment="64-byte cache-line stride">
      <FIELD name="LocalToWorld" offset="0" size="64" type="float4x4"/>
      <MATH>float4x4 = 4 columns * float4 = 4 * 16 = 64 bytes. Total size 64; 64 % 16 = 0 and 64 % 64 = 0.</MATH>
    </PRIMARY_DTO>
    <DTO name="ProceduralIKTargetDTO" size="32">
      <FIELD name="LocalPosition" offset="0" size="12"/>
      <FIELD name="Weight01" offset="12" size="4"/>
      <FIELD name="PoleOrNormal" offset="16" size="12"/>
      <FIELD name="Flags" offset="28" size="4"/>
      <MATH>12 + 4 + 12 + 4 = 32; 32 % 16 = 0.</MATH>
    </DTO>
    <DTO name="KineticCharacterFrameInputDTO" size="272">
      <FIELD name="RootSectorX/Y/Z" offsets="0,8,16" size_each="8"/>
      <FIELD name="RootLocalPosition" offset="24" size="12"/>
      <FIELD name="RootRotation" offset="40" size="16"/>
      <FIELD name="CameraSectorX/Y/Z" offsets="72,80,88" size_each="8"/>
      <FIELD name="ToolPoseMatrix" offset="144" size="64"/>
      <FIELD name="ActiveToolHash" offset="248" size="4"/>
      <FIELD name="Frame" offset="252" size="4"/>
      <FIELD name="Flags" offset="256" size="4"/>
      <FIELD name="_pad0" offset="260" size="4"/>
      <FIELD name="_pad1" offset="264" size="8"/>
      <MATH>Total 272; 272 % 16 = 0. 8-byte fields are placed before dense float/scalar payloads in each AUP block, and tail padding preserves 16-byte alignment.</MATH>
    </DTO>
    <DTO name="KineticAnimationTelemetryEntry" size="64">
      <FIELD name="RootSectorX/Y/Z" offsets="0,8,16" size_each="8"/>
      <FIELD name="RootLocal" offset="24" size="12"/>
      <FIELD name="Frame" offset="36" size="4"/>
      <FIELD name="BonesEvaluated/AverageIkIterations/CpuTime/Hash/Flags/Quality" offsets="40..60" size_each="4"/>
      <MATH>Total 64; one telemetry row is one cache line, reducing false sharing risk for ring writes.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE_EXPLANATION>
    When GlobalQualityWeight drops below 0.3, EvaluateWallProximityJob collapses SDF contact from gradient-normal evaluation to nearest-byte SDF lookup because the gradient gate is math.step(0.24, quality). ProceduralLocomotionPhaseJob resolves lower IK iterations with math.lerp(minimum, ultra, smooth01(quality)) and lowers active bone count through a continuous quality curve, preserving macro silhouette while skipping secondary bones. Breathing blends toward TriangleWaveSigned at low quality and toward authored/sine motion as quality rises. There are no IsLowEndHardware binary switches.
  </SCALABILITY_CURVE_EXPLANATION>

  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_ALLOCATIONS>ZERO persistent private NativeArray/NativeList/NativeHashMap fields in KineticCharacter runtime. Persistent native memory is requested from IDataVault.</PRIVATE_NATIVE_ALLOCATIONS>
    <VAULT_HANDLES>
      <HANDLE id="13671360" name="Rigs"/>
      <HANDLE id="13671361" name="FrameInputs"/>
      <HANDLE id="13671362" name="ParentIndices"/>
      <HANDLE id="13671363" name="BindPoses"/>
      <HANDLE id="13671364" name="BoneOutputs"/>
      <HANDLE id="13671365" name="BoneMatrices"/>
      <HANDLE id="13671366" name="IkTargets"/>
      <HANDLE id="13671367" name="FrameStats"/>
      <HANDLE id="13671368" name="TelemetryRing"/>
      <HANDLE id="13671369" name="TelemetryCursor"/>
      <HANDLE id="13671370" name="Tuning"/>
      <HANDLE id="13671371" name="CsvScratch"/>
    </VAULT_HANDLES>
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>All NativeArray fields in KineticCharacter jobs that are independent are annotated with [NoAlias]; read-only inputs also use [ReadOnly].</NO_ALIAS>
    <JOBS>
      <JOB name="MockCharacterKinematicsJob" consumes="default dependency" outputs="KineticCharacterFrameInputDTO"/>
      <JOB name="EvaluateWallProximityJob" consumes="mock dependency if scheduled" outputs="ProceduralIKTargetDTO"/>
      <JOB name="ProceduralLocomotionPhaseJob" consumes="IK handle" outputs="ProceduralBoneDTO and KineticCharacterFrameStatsDTO"/>
      <JOB name="ComputeFinalBoneMatricesJob" consumes="solve handle" outputs="float4x4 BoneMatrices"/>
      <JOB name="KineticAnimationTelemetryJob" consumes="matrix handle" outputs="KineticAnimationTelemetryEntry ring and cursor"/>
    </JOBS>
    <COMPLETE_POLICY>CompletePendingSolver(false) returns without blocking when the handle is not completed. Complete() is used only when IsCompleted is true or during forced shutdown/dispose/service replacement.</COMPLETE_POLICY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    KineticCharacter files do not use Hecton8.Gameplay, ContextualPhysicalIkRig, PlayerKinematicsHandTarget, Animation Rigging, UnityEngine.Animator, RuntimeAnimatorController, or Physics.Raycast. The runtime stays in the existing Hecton8.Core assembly surface and routes through Core/Core.Contracts/DataVault IDs rather than importing a sibling Gameplay implementation type. A new child asmdef was not added because it would require editing the massive parent Hecton8.Core.asmdef reference graph.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    <BEFORE>Animator graph + clip/state-machine blending + Transform hierarchy + physics/raycast wall probes: O(graph bones + clip layers + transform hierarchy + raycasts).</BEFORE>
    <AFTER>Flat matrix solve: O(active bones + 4 two-bone chains * quality-scaled iterations + O(1) SDF samples). Low quality drops gradient taps and secondary bones.</AFTER>
    <NOTE>HZB/BRG/indirect draw requirements are not applied to the single player bone-matrix route. This domain outputs the player matrix palette; large instance culling/draw arguments remain renderer/vegetation/VFX ownership.</NOTE>
  </DEAR_LIE_CONFIRMATION>

  <STATIC_VERIFICATION>
    <CHECK result="PASS">rg found no swimAnimator, Animator.StringToHash, GetComponent&lt;Animator, RuntimeAnimatorController, AnimationRigging, Physics.Raycast, Pack=, Time., UnityEngine.Random, or Random. in Assets/_Project/Scripts/Animation/KineticCharacter, PlayerSwimPresentationController, editor tuner, and character_rig_constraints.csv.</CHECK>
    <CHECK result="KNOWN-RESIDUAL">PlayerKinematicsRuntime still contains legacy Time.frameCount uses outside the kinetic matrix route. The kinetic solver ignores LockstepPlayerKinematicState.Frame and consumes only AUP/velocity/forward from that source.</CHECK>
    <CHECK result="PASS">rg found no new NativeArray/List/HashMap, Allocator.Persistent, DTO properties, System.Linq, string.Format, foreach, or ToString formatting on the edited route; only one gated JobHandle.Complete() remains.</CHECK>
    <CHECK result="PASS">git diff --check passed for edited SHINOBU_136 files; only CRLF normalization warnings were emitted.</CHECK>
    <CHECK result="FAIL-COMPILE-GATE">dotnet build not run because the latest CPU gate sample was 100%. No dotnet/csc process was visible on the latest sample, but CPU alone violates the AGENTS build rule.</CHECK>
  </STATIC_VERIFICATION>

  <MICROSECONDS_SAVED_ESTIMATE>
    Animator graph and Transform stream removal: estimated 60-250 us on i3/MX350 class CPU when legacy bridge would be present. Main-thread raycast purge: estimated 20-180 us on contact-heavy frames. Breathing clip/physics replacement: estimated 5-40 us. CSV split/LINQ rejection: estimated 20-200 us per cold reload plus avoided GC. Runtime profiler proof remains pending.
  </MICROSECONDS_SAVED_ESTIMATE>
</SELF_AUDIT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_POLISH_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>Second-pass static review found one correctness-risk dirty predicate: GPU active-character constants were compared against the previous active count before latest telemetry assignment. The stable binary payload ledger also lacked the SHINOBU_136 CSV/Vault lane, so the route was documented only in owner-local architecture/log files.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Patched the dirty predicate to compare a local `activeCharacters` value derived from latest telemetry, converted two hot magnitude calculations to finite-guarded `math.rsqrt`, and added the SHINOBU_136 kinetic matrix lane to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: scalar sine/triangle breathing, byte-SDF hand brace, and direct matrix palette output remain the fake-first route replacing Animator/clip/physics graph work.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Rsqrt tightening is expected sub-5 us for the single-player route on i3/MX350. It is recorded as potential only; profiler proof is still absent.</MICROSECONDS_SAVED>
  <VERIFICATION>Static source patch only. Build remains blocked because latest CPU gate sample is 100 percent, and AGENTS forbids dotnet build above 50 percent CPU.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_FATAL_MATH_SWEEP_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>A follow-up forbidden-pattern scan still found one `math.sqrt` in the hot two-bone FABRIK angle reconstruction. It was finite guarded, but it violated the stricter NaN-vaccination/rsqrt audit posture for this domain.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Replaced the final FABRIK angle sqrt with `sinSq * math.rsqrt(math.max(sinSq, 0.000001f))`. Re-ran the SHINOBU kinetic route forbidden-pattern scan; it returned no `math.sqrt`, Unity time/random, Animator, Raycast, LINQ, foreach, managed formatting, `Pack=`, or legacy swim Animator names.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: no clip/physics breathing or raycast bracing was reintroduced. Low quality still uses nearest SDF and triangle breathing; higher quality spends the saved CPU budget on more active bones and IK iterations.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Expected delta is sub-2 us for a single player on i3/MX350 class hardware. The main value is removing the last hot sqrt precedent from the solver; runtime profiler proof is still blocked.</MICROSECONDS_SAVED>
  <VERIFICATION>Static scans passed except the known gated `_pendingHandle.Complete()` path used only after IsCompleted or forced shutdown. `git diff --check` returned no whitespace errors for edited files, only CRLF normalization warnings. Build remains blocked because the CPU gate sample is 100%.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_DENOMINATOR_OFFSET_HARDENING_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>Further source review found the SDF grid conversion still divided by `SdfCellSize` after trusting caller-side clamps. The editor layout facade also used `Marshal.OffsetOf` instead of Unity `UnsafeUtility` offset proof.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Moved denominator guards into the hot math site: SDF cell size now clamps with `math.max(math.abs(...), 0.0001f)` before reciprocal, brace weight and telemetry inverse-active use guarded reciprocals, and quaternion normalization guards `rsqrt` locally. The editor facade now uses `UnsafeUtility.GetFieldOffset(FieldInfo)` for DTO offset proof.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: SDF byte sampling remains the visual/physical cheat for hand bracing; breathing remains scalar wave math. No Animator, clip, raycast, or Transform hierarchy route was restored.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Expected runtime delta is below 1 us for one player. This pass is NaN containment, not claimed frame-time optimization.</MICROSECONDS_SAVED>
  <VERIFICATION>Static scans found no player-domain Animator/AnimationRigging/RuntimeAnimatorController/swimAnimator route in Player prefab or edited source. `01_MAIN_MENU` still contains unrelated `panelAnimator` UI serialization outside SHINOBU player/NPC scope. All 5 Kinetic jobs retain deterministic Burst directives and NoAlias fields. Build remains blocked: CPU gate sample is 100% with no dotnet/csc process visible.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_PREFAB_WIRING_HARDENING_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>The player prefab still depended on a play-mode fallback that could call `AddComponent<KineticCharacterAnimatorRuntime>()` if `kineticMatrixRuntime` was null. That was a hidden Unity component bootstrap, not a serialized owner route.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added one serialized kinetic matrix runtime component to `Assets/_Project/Prefabs/Player.prefab`, wired `kineticMatrixRuntime` to fileID `136713600000000136`, and removed the bridge's runtime `AddComponent` fallback. The AddComponent menu label was changed to `Kinetic Character Matrix Runtime` to remove the last audit false positive containing standalone `Animator` text.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: direct Vault matrix palette output remains the substitute for Animator/Transform hierarchy evaluation; byte-SDF brace and scalar breathing remain the physical fakes.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Hot path unchanged. Cold startup removes one potential component allocation and scene mutation route; no precise us claim is made without Unity profiler proof.</MICROSECONDS_SAVED>
  <VERIFICATION>Prefab scan reports exactly three occurrences of fileID `136713600000000136`: root component list, serialized field, and MonoBehaviour block. `kineticMatrixRuntime: {fileID: 0}` count is 0. Static forbidden scan finds no Unity Animator type, RuntimeAnimatorController, swimAnimator, Animation Rigging, Physics.Raycast, UnityEngine.Random, hot math.sqrt, Marshal.OffsetOf, Pack=, or `AddComponent<KineticCharacterAnimatorRuntime>` in the edited SHINOBU route. `git diff --check` passes with CRLF warnings only. Build remains blocked because CPU gate is 100% with no dotnet/csc process visible.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_UNMANAGED_UPLOAD_CONTRACT_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>The raw GPU matrix upload helper used `where T : struct` while copying bytes with `UnsafeUtility.MemCpy` into a locked `GraphicsBuffer`. That contract was too weak for an unsafe upload boundary.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Changed `CreateStructuredLockBuffer`, `UploadNativeArray`, and `ResolveSafeWriteCount` to require `where T : unmanaged`. The current caller still uploads `float4x4` bone matrices from Vault, so runtime behavior is unchanged while the compiler fence is stricter.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: the renderer receives a flat matrix palette instead of a Unity Animator/Transform hierarchy stream; SDF brace and scalar breathing remain fake-first math routes.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Runtime delta is 0 us. This is architectural risk removal: future managed payloads fail at compile time instead of corrupting the raw matrix upload path.</MICROSECONDS_SAVED>
  <VERIFICATION>Static scan reports three `where T : unmanaged` upload-helper sites and zero `where T : struct` hits on the edited route. Forbidden-pattern scan passes for Animator, RuntimeAnimatorController, Animation Rigging, raycast, random, hot sqrt, Marshal.OffsetOf, Pack=, and runtime AddComponent fallback. `git diff --check` passes with CRLF warnings only. Compile remains blocked: CPU gate sample is 100% and no dotnet/csc process is visible.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_ACTIVE_TOOL_HASH_BOUNDARY_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>`SubmitToolPose(..., toolHash)` accepted the active Equipment tool hash but discarded it before the Burst frame DTO. Task 11 therefore aligned to a pose matrix but lost tool identity for deterministic grip bias, telemetry, and state hashing.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `ActiveToolHash` to `KineticCharacterFrameInputDTO`, set `InputFlagToolHashValid`, carried the hash from the player bridge into Burst, used it for deterministic left-hand support-grip bias, and mixed it into `StateHash`. No direct Equipment runtime dependency or managed grip lookup was introduced.</WHAT_WAS_DONE>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="KineticCharacterFrameInputDTO" size="272">
      <FIELD name="RootSectorX" offset="0" size="8"/>
      <FIELD name="RootSectorY" offset="8" size="8"/>
      <FIELD name="RootSectorZ" offset="16" size="8"/>
      <FIELD name="RootLocalPosition" offset="24" size="12"/>
      <FIELD name="GlobalQualityWeight" offset="36" size="4"/>
      <FIELD name="RootRotation" offset="40" size="16"/>
      <FIELD name="VelocityLocal" offset="56" size="12"/>
      <FIELD name="Visible01" offset="68" size="4"/>
      <FIELD name="CameraSectorX" offset="72" size="8"/>
      <FIELD name="CameraSectorY" offset="80" size="8"/>
      <FIELD name="CameraSectorZ" offset="88" size="8"/>
      <FIELD name="CameraLocalPosition" offset="96" size="12"/>
      <FIELD name="StressLevel01" offset="108" size="4"/>
      <FIELD name="CameraForwardLocal" offset="112" size="12"/>
      <FIELD name="OxygenLevel01" offset="124" size="4"/>
      <FIELD name="DamageImpulseLocal" offset="128" size="12"/>
      <FIELD name="DamageImpulse01" offset="140" size="4"/>
      <FIELD name="ToolPoseMatrix" offset="144" size="64"/>
      <FIELD name="SimulationTickDelta" offset="208" size="4"/>
      <FIELD name="SimulationTime" offset="212" size="4"/>
      <FIELD name="SwimWaveForward" offset="216" size="4"/>
      <FIELD name="SwimWaveLateral" offset="220" size="4"/>
      <FIELD name="SwimCrestReach" offset="224" size="4"/>
      <FIELD name="SwimDescentTuck" offset="228" size="4"/>
      <FIELD name="SwimLeanWeight" offset="232" size="4"/>
      <FIELD name="ImmersionDepth" offset="236" size="4"/>
      <FIELD name="BreathingPhase" offset="240" size="4"/>
      <FIELD name="ActiveToolWeight01" offset="244" size="4"/>
      <FIELD name="ActiveToolHash" offset="248" size="4"/>
      <FIELD name="Frame" offset="252" size="4"/>
      <FIELD name="Flags" offset="256" size="4"/>
      <FIELD name="_pad0" offset="260" size="4"/>
      <FIELD name="_pad1" offset="264" size="8"/>
      <MATH>Total 272; 272 % 16 = 0. Active tool hash adds 4 bytes, explicit 12-byte tail padding preserves ARM64/GPU-safe alignment.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <CINEMATIC_CHEATS_USED>The tool-specific two-hand pose is a deterministic hash-derived support-grip fake. No authored Animator layer, per-tool AnimationClip, or runtime Equipment grip database is evaluated.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Runtime delta is expected below 1 us for one player. The gain is correctness and telemetry specificity, not measurable frame-time reduction.</MICROSECONDS_SAVED>
  <VERIFICATION>Static source patch and docs updated. Compile remains blocked until the CPU gate drops below the AGENTS threshold.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_ACTIVE_TOOL_HASH_PRODUCER_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>The kinetic DTO and Burst solver preserved `ActiveToolHash`, but the live player swim bridge still passed literal `0u` into `SubmitToolPose`. That made Task 11 formally present in the solver and inert at the producer boundary.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>`PlayerToolManager` now caches `_currentActiveToolHash` when a tool becomes current and clears it on despawn/failure. `PlayerSwimPresentationController` submits `CurrentActiveToolHash` to `KineticCharacterAnimatorRuntime`; missing item persistent IDs fall back to cold `LocHash.Compute(metadata.toolID)`, not the existing `RuntimeToolId`/`Animator.StringToHash` path.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The tool-specific grip remains a hash-derived deterministic support-pose fake. No per-tool AnimationClip, Animator layer, managed grip table, or Equipment runtime dependency was added to the Burst solver.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Expected hot-path delta versus per-frame persistent-id hashing is sub-5 us on i3/MX350 class hardware. Submitting the cached uint is sub-1 us; the main gain is eliminating a producer-boundary correctness fault.</MICROSECONDS_SAVED>
  <VERIFICATION>Static source patch, status, rationale, and architecture docs updated. Re-scan proves no `SubmitToolPose` call block passes literal `0u`; added bridge diff contains no `Animator.StringToHash`, `RuntimeToolId`, raycast, Unity random, LINQ/foreach, string formatting, `math.sqrt`, `Marshal.OffsetOf`, or `Pack=`. `git diff --check` passes with CRLF warnings only. Compile remains blocked: CPU gate sample is 100% and no dotnet/csc process is visible.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>

<SELF_AUDIT_AMENDMENT agent_id="SHINOBU_136" status="STATIC_VAULT_HOTSWAP_GPU_FENCE_COMPILE_BLOCKED" date="2026-05-19">
  <WHAT_WAS_WRONG>The `DataVault` hot-swap route cleared Vault handles after completing jobs, but it did not explicitly clear material/global GPU skinning bindings before reacquiring new Vault buffers. A stale matrix buffer could survive the registry replacement boundary for a frame.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `ClearGpuSkinningBinding()` to `OnGlobalRegistryServiceReplaced(DataVault)` immediately after `ClearHandles()` and before emergency mock regeneration. This invalidates matrix count, active-character scalar, quality scalar, and global GPU skinning flag on the boundary.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Unchanged: GPU still consumes flat Vault matrix palettes instead of Transform/Animator graph traversal. This patch only fences stale visual state when the Vault owner changes.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Steady-state runtime delta is 0 us. Hot-swap adds a few shader/material scalar writes; that cost is outside the per-frame animation solve and prevents stale rendering after registry rebind/origin-reset style events.</MICROSECONDS_SAVED>
  <VERIFICATION>Static scan verifies the DataVault replacement block orders `CompletePendingSolver(true)` -> `UnlockJobBuffers()` -> `_dataVault = currentService` -> `ClearHandles()` -> `ClearGpuSkinningBinding()` -> `EnsureVaultBuffers()`/mock rig regeneration. Forbidden-pattern scan over the SHINOBU kinetic route remains clean; `git diff --check` passes with CRLF warnings only. Compile remains blocked by the CPU gate.</VERIFICATION>
</SELF_AUDIT_AMENDMENT>
