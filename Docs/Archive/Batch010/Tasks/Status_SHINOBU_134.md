# SHINOBU_134 Status

Agent: SHINOBU_134
Domain: ABYSSAL_SHADOW_CULLING_DIRECTOR
Task Count: 20
Status: PENDING VERIFICATION

## Mandates Read

- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- DATA_Runtime_Struct_Layout_ARM64
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows
- REND_GPU_Occlusion_Culling_6000
- REND_GPU_Sovereignty
- REND_URP_Graphics_HotPath_Optimization_HLOD
- DBG_Telemetry_Crash_Reporting_PostMortem
- TOOL_Designer_Facades_CSV_Binary_Bridge

## Assignment Snapshot

Source: Docs/Tasks/CURRENT_BATCH.md
XML ID: SHINOBU_134
Role: ABYSSAL_SHADOW_CULLING_DIRECTOR

## Checklist

- [x] Task 01 MONOBEHAVIOUR_CULLING_ERADICATION | Static archaeology found no first-party `ShadowDistanceCull` owner script to delete; vendor/editor references left untouched. DOD: ownership scan before deletion. Alternative rejected: blind `shadowCastingMode` removal from vendor/prefab YAML. Estimate: 6-20 us CPU avoided per 1k legacy renderer toggles if such scripts reappear; measured proof absent.
- [x] Task 02 UNITY_LOD_GROUP_SHADOW_PURGE | Shadow submission moved to Vault state flags + indirect args instead of relying on black-box LODGroup shadow side effects. DOD: data-driven shadow-pass authority. Alternative rejected: raw prefab LODGroup mutation without FileID proof. Estimate: 40-250 us CPU/GPU submission avoided per dense 10k caster pass; measured proof absent.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | `ShadowCullStateDTO` and hot DTOs use raw public fields, explicit layout, no get/set properties. DOD: static scan for property DTO contamination. Alternative rejected: C# property wrappers over NativeArray structs. Estimate: 1-3 us per 50k pass from fewer defensive copies; measured proof absent.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | `ShadowCullStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 32)]`; counters padded to 64B; editor validator checks required offsets. DOD: `UnsafeUtility.SizeOf/FieldOffset` route. Alternative rejected: sequential layout trust. Estimate: prevents ARM64 unaligned read stalls; measured proof absent.
- [x] Task 05 EMERGENCY_MOCK_INSTANCE_DATA | `GenerateMockCullingDataJob` seeds deterministic 50k AUP-centered instances, illumination, source flags, and states. DOD: isolated synthetic throughput path. Alternative rejected: waiting for world/flora owner. Estimate: enables 50k stress path with 0 managed list construction; measured proof absent.
- [x] Task 06 BURST_FRUSTUM_CULLING_KERNEL | `EvaluateShadowCullingJob` runs `IJobParallelFor` with exact Burst flags, `[NoAlias]`, local AUP subtraction, frustum AABB tests, directional light expansion, and HZB/SDF gates. DOD: Burst kernel route. Alternative rejected: per-object `GeometryUtility`/Renderer mutations. Estimate: 120-600 us CPU avoided at 50k vs managed culling; measured proof absent.
- [x] Task 07 SQUARED_DISTANCE_SHADOW_LOD | Distance culling uses `math.dot(center, center)` and squared thresholds only; static scan found no `math.sqrt`. DOD: no sqrt in hot loop. Alternative rejected: `Vector3.Distance`. Estimate: 50k sqrt eliminations, roughly 20-80 us on low silicon; measured proof absent.
- [x] Task 08 THE_DEAR_LIE_DITHERED_SHADOWS | Fade scalar is packed into `IlluminationScalar`; `Hecton_AbyssalShadowDither.hlsl` provides Bayer `clip()` shadow dissolve. DOD: visual fake over hard pop. Alternative rejected: CPU-side fade/extra caster meshes. Estimate: visual continuity at near-zero CPU cost; GPU proof pending.
- [x] Task 09 ILLUMINATION_AWARE_CULLING | Burst job multiplies ambient illumination, material shadow scalar, and SDF/occlusion scalar; darkness clears `CastShadows`. DOD: abyssal darkness is default. Alternative rejected: treating all lit/unlit objects as equal shadow casters. Estimate: caves/trenches can remove broad shadow-map draw pressure; measured proof absent.
- [x] Task 10 ASYNCHRONOUS_INDIRECT_DISPATCH | VisualSync uploads state and indirect args via double-buffered `GraphicsBuffer.LockBufferForWrite` + `UnsafeUtility.MemCpy`; no managed visible list exists. DOD: direct buffer bridge. Alternative rejected: `List<int>` visible indices. Estimate: avoids list allocation and CPU draw-list walk; measured proof absent.
- [x] Task 11 CONTINUOUS_SCALABILITY_CULL_AGGRESSION | `GlobalQualityWeight` drives max distance, caster radius, HZB grid, point-light allowance, dither band profile scale, and SDF aggression through `math.lerp/smoothstep`. DOD: no binary low-end switch. Alternative rejected: quality enum branch. Estimate: low weight collapses shadow residency from 150m toward 20m; measured proof absent.
- [x] Task 12 DIRECTIONAL_LIGHT_ONLY_OPTIMIZATION | Point-light shadow casters are culled by instance-stable quality-weight allowance plus previous-state hysteresis until ultra range; directional casters remain primary. DOD: point shadows treated as luxury without frame lottery flicker. Alternative rejected: point shadows always on or frame-rerolled. Estimate: removes most local shadow atlas pressure below Q 0.85; measured proof absent.
- [x] Task 13 AUP_PRECISION_FRUSTUM_PLANES | Runtime accepts localized frustum planes and exposes plane localization helper; culling subtracts `CameraAUP` before float cast. DOD: 100km jitter guard. Alternative rejected: float absolute world planes. Estimate: correctness protection, not a fake metric.
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | `ShadowCullStateDTO` writes `RollbackExcluded`; states are presentation-only Vault buffers and not Merkle/state-ring authority. DOD: visual lie excluded from gameplay truth. Alternative rejected: hashing shadow fade/cull flags. Estimate: avoids rollback ring bandwidth; measured proof absent.
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | All steady-state Vault buffers request `NativeArrayOptions.UninitializedMemory`; jobs overwrite active windows. DOD: H-PHI Vault law. Alternative rejected: per-frame clear or local persistent NativeArray. Estimate: avoids zeroing ~4 MB shadow lane per allocation/grow; measured proof absent.
- [x] Task 16 TELEMETRY_CULLING_RECORDER | 300-entry `CullingTelemetryEntry` ring records counts, timings, quality, state hash; non-finite fault dumps `Docs/AgentLogs/Dump_SHADOW_DIRECTOR.bin`. DOD: blackbox forensic ring. Alternative rejected: `Debug.Log` as diagnosis. Estimate: no frame-time saving; crash autopsy route.
- [x] Task 17 CULLING_TUNER_EDITOR_WINDOW | UI Toolkit `Abyssal Shadow Tuner` provides sliders, snapshot labels, mock run, layout validation, and CSV ingestion buttons in editor assembly. DOD: designer control without runtime HUD. Alternative rejected: gameplay debug canvas. Estimate: no hot-path cost; editor-only.
- [x] Task 18 CSV_CULLING_PROFILES_INGESTOR | CSV bytes read cold into Vault scratch; parser hashes ASCII names and writes unmanaged profile rules without string splits in parser. DOD: byte parser + FNV-1a. Alternative rejected: `string.Split` in runtime profile flow. Estimate: cold path only; zero-GC parser surface.
- [x] Task 19 LIVE_FRUSTUM_DEBUG_GIZMO | Editor-only `OnDrawGizmos` colors AUP-local wire boxes green/yellow/red from Vault states. DOD: x-ray math proof view. Alternative rejected: runtime debug GameObjects. Estimate: no player hot-path cost.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Static scans completed for no LINQ/foreach/new NativeArray/Renderer.shadowCastingMode/math.sqrt/UnityEngine.Random in owned files; `git diff --check` passed. DOD: static verification and forensic report appended to log; runtime proof pending. Alternative rejected: chat-only proof. Estimate: no runtime saving.

## Loop Log

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; domain docs, AGENTS.md, binary ledger, and mandates read.
- Loop 1: Tasks 01-05 implemented/static-verified; no legacy first-party culling script deleted because none was found in owned domain.
- Loop 2: Tasks 06-10 implemented; Burst job, squared-distance LOD, dither shader include, illumination cull, and double-buffer GPU upload added.
- Loop 3: Tasks 11-15 implemented; continuous `GlobalQualityWeight`, point-light budget, AUP-local planes, rollback exclusion flag, and uninitialized Vault buffers added.
- Loop 4: Tasks 16-20 implemented statically; telemetry ring, dump writer, UI Toolkit tuner, CSV parser, gizmo, and scans added.
- Loop 5: ULTRA_THINK polish pass added HZB tile DTO/job, SDF-occlusion flag, indirect args job, 64B counters, and Simulation/VisualSync dispatcher split. Unity import/profiler proof remains pending.
- Loop 6: Strict XML extraction corrected for additional tag attributes; aggregate DTO layout validator added for state/instance/counters/telemetry/runtime/HZB/indirect/profile structs; gizmo callback fenced behind `#if UNITY_EDITOR`.
- Loop 7: Added external producer ingress for Lighting/HZB/World buffers, `JobHandle.CombineDependencies` chaining, active-count/HZB-count handoff, mock suppression flags, and published GPU-buffer facade for renderer ownership.
- Loop 8: Corrected HZB occlusion mapping from raw `center.xy/center.z` to camera-basis right/up/forward dot products with finite fallback vectors.
- Loop 9: Removed Unity `Time.frameCount` fallback from SHINOBU_134 runtime; frame identity now comes from dispatcher `context.Frame` or Vault runtime frame fallback.
- Loop 10: Wrapped VisualSync `GraphicsBuffer.LockBufferForWrite` mappings in `try/finally` unlock guards for state and indirect-args uploads.
- Loop 11: Split external producer/tuner/CSV/snapshot access to Vault-only `EnsureVaultBuffers`; GPU buffers are prewarmed in cold `OnEnable` when Vault is available and ensured only by runtime visual paths.
- Loop 12: Confirmed Vault job-buffer lock acquisition is fail-fast: scheduling now records `TelemetryFlagVaultLockFailed`, releases only acquired buffers, preserves producer handoff state, and returns the incoming dependency instead of scheduling against contested Vault ownership.
- Loop 13: Hardened `RunMockCullingOnce()` against false positive editor/CI stress results: if Vault lock fail-fast prevents job scheduling, the facade now returns `false` instead of reporting a completed 50k pass.
- Loop 14: Removed hidden `math.length` sqrt from `GenerateMockHzbTilesJob`; mock HZB occluder now uses squared radial distance via `math.dot(uv, uv)`.
- Loop 15: Moved reflection-based `AbyssalShadowLayoutAudit` out of runtime DTO source and into the Editor facade file, keeping player/runtime IL free of layout-reflection proof helpers.
- Loop 16: Corrected the Bayer shadow shader include so dither clipping runs only when `DitherFadeActive` is set; valid dim casters no longer become permanently noisy partial shadows.
- Loop 17: Hardened CSV profile hot reload to fail closed: zero-valid-row files no longer clear the previous live profile table; successful shorter files clear only the stale tail.
- Loop 18: Added `_jobPending` mutation gates to frustum-plane writes and CSV profile reload so editor/control facades cannot mutate NativeArrays while the Burst culling reader is scheduled.
- Loop 19: Made CSV profile reload transactional: `Validate()` scans bytes first, rejects malformed or over-capacity content before commit, and only then parses into the live Vault rule prefix.
- Loop 20: Tightened CSV float parsing so numeric prefixes with trailing garbage, e.g. `1abc`, fail validation instead of silently producing a scalar.
- Loop 21: Added previous-state hysteresis inside `EvaluateShadowCullingJob` for distance, frustum, darkness, SDF, radius, and point-light gates; removed frame-rerolled point-light decimation.
- Loop 22: Hardened hysteresis history validation so seeded/default mock state with `DistanceSq=0` cannot relax the first real cull evaluation.
- Loop 23: Added `_jobPending` guard to `ApplyTunerSettings()` so runtime tuning writes cannot skew completion telemetry for an already scheduled Burst job.

## Verification Ledger

- Static scan: no owned-file hits for LINQ, `foreach`, `new NativeArray`, `new NativeList`, `new NativeHashMap`, `Renderer.shadowCastingMode`, `math.sqrt`, `UnityEngine.Random`, `JobHandle.ScheduleBatchedJobs`, `Shader.SetGlobalInteger`, or `Time.`.
- Static scan: all owned Burst jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Static asmdef check: `Hecton8.Graphics.Culling.asmdef` references Core, Core.Contracts, Core.Memory, World.Contracts, and Unity packages; no added sibling runtime concrete dependency.
- Static layout guard: editor-only `AbyssalShadowLayoutAudit.ValidateAllLayouts()` checks state=32B, instance/counters/telemetry/runtime=64B, HZB=16B, indirect/profile/CSV=32B paths.
- Dependency guard: external producers can now hand off `JobHandle` dependencies through `RegisterExternalProducerDependency`; culling combines them before mock/evaluate/reduce/indirect jobs.
- HZB math guard: `EvaluateShadowCullingJob` now receives sanitized HZB right/up/forward vectors and computes tile/depth tests in that basis.
- Determinism guard: point-light culling no longer uses a frame-rerolled hash; dispatcher/Vault frame identity remains only for telemetry/runtime cadence, and Unity `Time.frameCount` is absent.
- Hysteresis guard: `EvaluateShadowCullingJob` now validates previous `InstanceHash`, finite positive previous `DistanceSq`, and non-fault flags before using previous `CullFlags`, then applies quality-scaled bands to suppress one-frame LOD/shadow-state flipping without adding a history buffer.
- GPU upload guard: both VisualSync mapped GraphicsBuffers unlock through `try/finally`.
- Boot/allocation guard: external producer facade no longer calls GPU-buffer initialization.
- Vault lock guard: `TryLockJobBuffers` checks every `TryLockBuffer` result and uses counted reverse unlock on partial acquisition failure.
- Mock facade guard: `RunMockCullingOnce()` now requires `_jobPending` after `ScheduleCullingPass`; lock-failed or unresolved scheduling paths do not report success.
- Hidden ALU guard: owned Burst jobs no longer use `math.length`; HZB mock radial falloff is squared-distance based.
- Runtime reflection guard: runtime culling source no longer declares `AbyssalShadowLayoutAudit` or calls `typeof(T).GetField` for layout proof.
- Shader dither guard: `Hecton_AbyssalShadowDither.hlsl` now gates Bayer `clip()` on `DitherFadeActive` and uses named flag constants matching `AbyssalShadowCullFlags`.
- CSV mutation guard: `LoadProfileCsv()` now commits only when `ParsedRuleCount > 0` and clears stale rule tail after success instead of erasing live rules before parse proof.
- Scheduled-reader guard: `SetLocalizedFrustumPlanes()`, `LoadProfileCsv()`, and `ApplyTunerSettings()` now refuse mutation while `_jobPending` is true.
- CSV transaction guard: malformed non-comment lines and capacity overflow reject the reload before live profile rules are mutated.
- CSV scalar guard: `TryParseFloat()` now requires full token consumption after optional sign/integer/fraction parsing.
- Whitespace guard: tracked `git diff --check` passes for the architecture ledger with only the existing LF->CRLF warning; explicit trailing-whitespace scan passes for SHINOBU_134 source/shader/status/rationale/log paths, including untracked owned files.
- Full build: NOT RUN after polish. Latest process probe found no visible `dotnet/csc`, but the user explicitly forbade launching build until needed and prior unrelated solution blockers are already known.
- Unity import, Burst Inspector, Play Mode, Frame Debugger, Profiler, GCMonitor, and player build: PENDING VERIFICATION.
