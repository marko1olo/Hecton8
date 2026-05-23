# SHINOBU_354 Rationale - PROCEDURAL_CAMERA_SHAKE_IMPULSE

## Decision 001 - Integration Owner
Problem: The prompt names `HectonVFXRuntime`, but the repository has no such symbol. Camera shake ownership is already in `CameraJuiceSystem`.
Solution: Use `CameraJuiceSystem` as the owner and add isolated partial code instead of inventing a missing runtime.
Rejected Alternatives: Creating `HectonVFXRuntime` would create a second owner and violate one fact -> one owner -> one route.
Scalability potential: Low tier keeps camera hierarchy static and pays only projection jitter; middle/high/ultra scale procedural octaves and directional bias without gameplay truth changes.
Hardware Impact: On i3/MX350, eliminating transform/AnimationClip shake avoids managed evaluation and hierarchy dirties; expected saving is 20-80 us during impulse frames depending scene camera stack.

## Decision 002 - Signal Route
Problem: Camera shake must respond to impacts without new direct dependencies.
Solution: Consume existing `SignalBus<T>` snapshot lanes for impact, high-speed impact, combat damage, seismic, and camera-juice impact signals.
Rejected Alternatives: New `ShakeScreen` events or direct component callbacks would duplicate first-party signal lanes and add routing ambiguity.
Scalability potential: Low tier consumes capped event counts; higher tiers can use more octaves after the same deterministic trauma scalar.
Hardware Impact: Snapshot reads avoid allocations and avoid scene searches; expected hot-path saving is 5-25 us versus managed queue/callback fanout.

## Decision 003 - Projection Instead Of Transform Motion
Problem: Existing procedural shake mutates `Camera.transform.localPosition` and rotation, forcing hierarchy and culling side effects.
Solution: Keep camera transform static at runtime and apply a projection-matrix offset plus optional comfort rotation DTO.
Rejected Alternatives: Standard Unity transform shake and Cinemachine impulse were rejected because they dirty the camera hierarchy and can allocate through extension stacks.
Scalability potential: Low uses a single damped sine; middle/high/ultra add continuous octave weight and directional impulse shaping.
Hardware Impact: On i3/MX350, avoiding per-frame transform dirtiness is expected to save 15-60 us and reduce jitter in late camera sync.

## Decision 004 - Tiny Job Scheduling Rejected
Problem: A two-job camera impulse update is too small to justify same-frame schedule/readback fences.
Solution: Use Burst `IJob.Run()` for `EvaluateCameraTraumaJob` and `IntegrateProceduralShakeJob`, measuring the Burst section with `Stopwatch` and avoiding hidden `.Complete()`.
Rejected Alternatives: Scheduling tiny jobs then reading the projection matrix in the same late frame would violate the hidden completion mandate without profiler proof.
Scalability potential: Low tier executes one damped wave; middle/high/ultra increase octave contribution and attenuation radius continuously without extra ownership routes.
Hardware Impact: On i3/MX350 this avoids scheduler/fence overhead estimated at 8-25 us for a job that normally computes below 50 us.

## Decision 005 - VRSomatic DTO Ownership
Problem: The prompt requests direct `VRSomaticComfortDTO` writes, but that buffer is owned and seeded by `VRSomaticProvider` / SHINOBU_326.
Solution: Generate a comfort-compatible quaternion in the projection DTO and apply camera juice through the VFX projection matrix. Do not mutate SHINOBU_326 buffers.
Rejected Alternatives: Writing `BufferID.ShinobuVRSomaticHorizonWrite` from VFX would create a race against the somatic comfort job and violate one owner -> one route.
Scalability potential: Low/middle/high/ultra visual shake remains in VFX projection space, while VR comfort remains the somatic provider's authority.
Hardware Impact: Avoids cross-domain synchronization or buffer arbitration; expected saving is 3-15 us and eliminates a race class.

## Decision 006 - DataVault Initialization
Problem: `NativeArrayOptions.ClearMemory` hides initialization cost and violates the explicit overwrite rule for hot buffers.
Solution: Allocate SHINOBU_354 state/tuning/profile/mock buffers and telemetry with `UninitializedMemory`, then seed active slots through Burst jobs.
Rejected Alternatives: Managed array clear, `TryGetLatestCreated`, or per-frame lazy buffer growth.
Scalability potential: Low tier keeps fixed 1-entry state/projection buffers; high/ultra can expand profiles/mock capacity by BufferID contract without changing DTO layout.
Hardware Impact: Cold init only; hot path has no allocation and no buffer growth.

## Decision 007 - Compile Wall Handling
Problem: `dotnet build Assembly-CSharp.csproj` failed before reaching SHINOBU_354 code because `Hecton8.Core.csproj` cannot resolve `Hecton8.Habitat` from two Construction files.
Solution: Mark compile verification blocked by dependency and do not edit Construction/Habitat ownership. Keep SHINOBU_354 changes scoped to VFX/Core BufferID/docs.
Rejected Alternatives: Fixing HatchLock/Habitat references from this VFX task would cross the domain boundary and risk breaking another active agent's integration.
Scalability potential: No runtime impact; this is build graph ownership only.
Hardware Impact: No frame impact. Build attempt consumed editor-side CPU only; no runtime code path changed.

## Decision 008 - Core Enum Churn Removed
Problem: The first pass added `Shinobu354CameraJuice*` rows to `H8Memory.cs`, expanding a shared core enum for a VFX-local presentation route.
Solution: Remove the enum rows and use local casted `BufferID` constants `73373..73379` inside `CameraJuiceSystem_CameraJuiceBurst.cs`; document the allocation in the binary ledger.
Rejected Alternatives: Keeping named core enum rows would make a VFX iteration touch shared core memory metadata and increase merge/compile risk for neighboring agents.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged; compile surface and merge pressure are reduced.
Hardware Impact: Runtime frame impact is zero. Editor/build impact is lower because core enum churn is removed from future SHINOBU_354 tuning passes.

## Decision 009 - Read-Like Allocation Removed
Problem: `TryResolveCameraJuiceTelemetry` and `TryResolveOrAcquireCameraJuiceBuffer` looked like read accessors while one path could allocate or cold-initialize memory.
Solution: Rename allocation path to `AcquireCameraJuiceBuffer`; runtime telemetry recording now uses `OpenCameraJuiceTelemetry`, which only resolves an already-created handle and fails closed otherwise.
Rejected Alternatives: Leaving allocation behind a `TryResolve*` name violates the global accessor doctrine and makes hot-path audits unreliable.
Scalability potential: Weak devices avoid surprise cold work during frame recording; high-end devices get the same visual path without hidden ownership changes.
Hardware Impact: Prevents rare frame spikes from late telemetry ensure; expected worst-case stall avoided is hundreds of microseconds if DataVault allocation were triggered mid-frame.

## Decision 010 - UI Toolkit Tuner And Vault Mutation
Problem: The editor facade was IMGUI and wrote tuning by copying `tuning[0]`, which failed the requested UI Toolkit/direct Vault mutation proof.
Solution: Replace the window with UI Toolkit controls and a fixed-sample graph; mutate `CameraJuiceTuningDTO` through `UnsafeUtility.AsRef` over the Vault row.
Rejected Alternatives: IMGUI sliders and managed debug UI were rejected because they do not match project editor tooling doctrine and obscure whether the Vault DTO is the tuning source.
Scalability potential: Editor-only. It exposes continuous low/mid/high/ultra tuning parameters without changing runtime DTO layout.
Hardware Impact: Runtime frame impact is zero. Editor graph uses fixed `float[300]` arrays allocated once per window instance.

## Decision 011 - Cold CSV Profile Source
Problem: The parser existed but did not hydrate production profile rows from a file, leaving Task 17 as a partial implementation.
Solution: Add `Assets/StreamingAssets/Hecton8/camera_trauma_profiles.csv`, stream bytes into the Vault scratch row, and parse `ReadOnlySpan<byte>` into `CameraTraumaProfileDTO` rows during cold seed.
Rejected Alternatives: `File.ReadAllBytes`, `string.Split`, ScriptableObject profile assets, and per-frame file polling were rejected because they allocate or route tuning through Unity object state.
Scalability potential: Low profile keeps one cheap damped curve; middle/high/ultra profiles raise radius/frequency/gain continuously through the same tuning DTO.
Hardware Impact: Cold-only file stream. Runtime saves are indirect: no managed profile assets, no per-frame parsing, no GC pressure.

## Decision 012 - Vault-Backed Scene Gizmo
Problem: The SceneView gizmo drew cached fields, not the final DTO that QA and designers need to inspect.
Solution: `OnDrawGizmosSelected` opens `CameraJuiceStateDTO` from Vault and draws a Yellow camera box plus Red offset box from the final translational row.
Rejected Alternatives: Drawing cached projection fields is easier but can lie after sanitization, buffer restore, or a projection suppression path.
Scalability potential: Editor-only. The same state row represents all quality tiers.
Hardware Impact: Runtime frame impact is zero; editor draw is one handle resolve and two wire boxes.

## Decision 013 - Deterministic Presentation Sequence
Problem: Mock AUP spike generation used `Time.frameCount`, and the fallback AUP route could derive an absolute position from camera transform float coordinates.
Solution: Mock signal seeding now uses the owner `_cameraJuiceSequence`; the camera transform AUP fallback was removed. If player AUP is unavailable, the system fails closed and clears projection shake.
Rejected Alternatives: Keeping `Time.frameCount` and transform-derived AUP is convenient, but it weakens deterministic replay analysis and risks 100km float-origin jitter.
Scalability potential: All quality tiers share one deterministic presentation sequence and the same AUP authority fallback policy.
Hardware Impact: Runtime cost is unchanged. Failure is cheaper on invalid context because it clears projection instead of deriving non-authoritative float AUP.

## Decision 014 - Remove Dormant Managed Shake Fallback
Problem: `CameraJuiceSystem.cs` still contained unreferenced legacy methods for local cnoise sampling, seismic transform jitter, local rotation undo, and destructive `CameraJuiceSignals.TryDequeueImpact` consumption.
Solution: Delete the dormant fallback path and leave one presentation route: SignalBus snapshots -> Burst state/projection DTOs -> projection matrix offset.
Rejected Alternatives: Keeping dead legacy methods as "fallback" would preserve a second mental model and make future agents re-enable transform shake by mistake.
Scalability potential: Low tier now pays only the Burst low waveform. Middle/high/ultra visual richness lives in the Burst octave path, not in a managed backup path.
Hardware Impact: No intended hot-path behavior change because methods were unreferenced. Static risk and future regression surface are reduced; old cnoise/transform fallback could have cost 10-50 us if reconnected.

## Decision 015 - Cached Player AUP Vault Handle
Problem: The camera juice hot path resolved `BufferID.PlayerKinematicState` by calling `TryGetGenerationHandle<LockstepPlayerKinematicState>` inside the per-frame AUP read.
Solution: Cache `_cameraJuicePlayerKinematicStateHandle` during DataVault rebind and slow dependency refresh. The hot path only `TryResolveHandle`s the cached descriptor, then falls back to cached `HectonPlayerMovement.CurrentAup` if unavailable.
Rejected Alternatives: Hot `TryGetGenerationHandle` is convenient but violates the cold ownership/accessor doctrine. `TryGetLatestCreated` remains rejected.
Scalability potential: Weak devices avoid per-frame metadata lookup. High/ultra get identical effect fidelity because signal math and DTO layout do not change.
Hardware Impact: Expected saving is small but structural: 1-5 us in metadata-heavy frames and no accidental hot Vault ownership query.

## Decision 016 - DataVault Rebind Repair
Problem: A live DataVault rebind released telemetry but left procedural buffer descriptors and `_cameraJuiceBuffersSeeded` associated with the old Vault.
Solution: Release procedural buffers before swapping `_dataVault`, clear cached player kinematic generation, reacquire cold buffers, and seed rows against the new Vault.
Rejected Alternatives: Relying on the next Tick to repair descriptors risks unseeded `UninitializedMemory` reads if a new Vault resolves rows while `_cameraJuiceBuffersSeeded` still says true.
Scalability potential: All tiers preserve one owner route and fail closed during Vault churn; no gameplay truth changes.
Hardware Impact: Cold rebind only. It prevents a rare projection/telemetry fault after origin/bootstrap/service replacement.

## Decision 017 - Continuous Noise ALU Load Shed
Problem: The Burst integrator previously computed high Simplex taps even when the continuous quality curve reduced their contribution to zero.
Solution: Use continuous `octaveWeight` and `ultraWeight`; bypass high/ultra taps only when those weights are mathematically zero, keeping low-tier visuals on damped sine/triangle waves and adding ultra grit only above the smooth threshold.
Rejected Alternatives: A binary `IsLowEndHardware` branch was rejected. Always evaluating all noise taps wastes mobile ALU.
Scalability potential: Low uses the cheapest believable waveform; middle blends first Simplex octave; high/ultra add richer grit without changing DTOs, authority, or save identity.
Hardware Impact: On i3/MX350/Quest-class silicon, low-quality frames avoid three to six `noise.snoise` evaluations per camera tick. Expected saving: 3-12 us depending Burst backend.

## Decision 018 - Csv Scratch Clear Removed
Problem: `SeedCameraJuiceBuffersJob` carried the Vault CSV scratch array only to zero every byte, undermining the `UninitializedMemory` contract for a cold file-ingest scratch lane.
Solution: Remove the job field and the scratch clear loop. `TryLoadCameraJuiceTraumaProfilesFromCsv` streams bytes into the scratch buffer and parses exactly `byteCount` bytes through `ReadOnlySpan<byte>`, so stale tail bytes are unreachable.
Rejected Alternatives: Keeping a full 4096-byte clear is harmless for correctness but trains future agents to treat uninitialized Vault scratch as zero-filled memory. `UnsafeUtility.MemClear` remains rejected.
Scalability potential: All tiers share the same cold profile hydration route. Low/middle/high/ultra runtime math is unchanged because tuning is already written into fixed DTO rows.
Hardware Impact: Cold boot/rebind saves one 4096-byte scalar clear pass. Frame impact is zero, but it preserves the explicit overwrite discipline required for weak mobile CPUs.

## Decision 019 - Manual Impact Float-Position Fallback Removed
Problem: `ResolvePhysicsImpactDirection` could derive a manual shake direction from `_cameraTransform.position - impactSignal.Point` when a direct `PhysicsImpactSignal` lacked a finite normal.
Solution: Return `float3.zero` for malformed direct-listener direction data. The AUP-authoritative SignalBus path still handles impact, high-speed, combat, seismic, and camera impact signals in Burst with double-precision player/epicenter subtraction.
Rejected Alternatives: Keeping a float transform fallback makes rare malformed events feel reactive, but it can reintroduce 100km floating-origin jitter and conflicts with the AUP-only spatial mandate.
Scalability potential: Low/middle/high/ultra visual fidelity is unchanged for valid signals. Malformed direct events decay through trauma scalar only instead of inventing a non-authoritative direction.
Hardware Impact: Removes one camera-transform read and vector subtraction from malformed direct-listener events. Expected frame gain is negligible; correctness gain is the point.

## Decision 020 - Default NativeArray ReadOnly Avoided
Problem: `EvaluateCameraTraumaJob` received `default` for the mock signal `NativeArray<T>.ReadOnly` when editor mock count was zero, then still evaluated `MockSignals.Length` while clamping the limit.
Solution: Always pass the created Vault-backed mock signal buffer read-only and gate processing only through `MockSignalCount`.
Rejected Alternatives: Depending on default `NativeArray<T>.ReadOnly.Length` behavior is brittle across Unity package versions and Burst safety modes.
Scalability potential: All quality tiers keep the same mock/test capacity. Runtime mock-disabled cost is one Length read on an already-created descriptor and zero iterations.
Hardware Impact: No material frame delta. It removes a possible safety exception path during editor/test toggles.

## Decision 021 - Adjacent Player Locomotion Camera Boundary
Problem: A broader archaeology pass found `Assets/_Project/Scripts/CameraJuiceProcessor.cs` and `HectonPlayerMovement` applying locomotion bob, collision dip, water-entry FOV, sonar ping, and suit/sargassum presentation offsets through `HectonPlayerCameraRig`. This is managed camera presentation, but it is not the SHINOBU_354 AnimationClip/Cinemachine/Random explosion shake route.
Solution: Leave the player locomotion camera route untouched and document it as adjacent ownership. SHINOBU_354 owns explosive/seismic/AUP trauma synthesis in `CameraJuiceSystem` and applies projection DTO offsets only; the player movement owner remains responsible for locomotion/somatic bob/FOV camera transform composition.
Rejected Alternatives: Ripping out `CameraJuiceProcessor` from this VFX task would break player locomotion, water transition, collision dip, sonar, transport, and VR camera rig behavior while violating the domain boundary. Folding it into SHINOBU_354 would also create a second owner for player camera state.
Scalability potential: Low/middle/high/ultra explosive trauma remains on the Burst projection route. Player locomotion camera presentation should be handled by its own owner if later mandated, ideally by routing its presentation offsets into a shared projection/somatic composition contract.
Hardware Impact: Runtime frame impact of this decision is 0 us. It prevents a high-risk cross-domain edit; any future migration of player locomotion camera juice needs a separate Player-domain pass with profiler proof.

## Decision 022 - Hot Vault Acquisition And Unity Frame Counter Removed
Problem: `RunProceduralCameraJuice` still called `EnsureProceduralCameraJuiceBuffers()` from the per-frame Tick route, so a missed cold seed could trigger Vault acquisition/initialization semantics in hot presentation code. `RecordCameraJuiceTelemetry` also wrote Unity `Time.frameCount` into the black-box row.
Solution: Gate the hot camera juice kernel on `_cameraJuiceBuffersSeeded`, clear projection on missing/stale rows, and open only cached generation handles during Tick. Keep `EnsureProceduralCameraJuiceBuffers()` confined to `Awake`, `OnEnable`, `BindDataVault`, and editor tuning. Use `_cameraJuiceTelemetryCursor` as the telemetry frame lane.
Rejected Alternatives: Leaving an ensure/acquire path in Tick hides ownership mutation under a presentation solve. Keeping `Time.frameCount` is convenient for editor inspection but adds an engine frame-state dependency to deterministic forensic rows.
Scalability potential: Low tier avoids rare Vault seed spikes during visual frames; middle/high/ultra keep the same damped sine/octave projection math and telemetry schema. Quality still changes fidelity only, not ownership or DTO layout.
Hardware Impact: Normal frame cost is unchanged. Worst-case late allocation/seed spike avoided is estimated at 100-800 us depending Vault state; removing the Unity frame read saves negligible ALU but tightens deterministic postmortem replay.

## Decision 023 - Read-Only Vault Readbacks And Versioned Dump Header
Problem: Diagnostic readbacks opened telemetry/player/state Vault rows through mutable resolves, and `Dump_SHINOBU_354.bin` had no magic/version/stride header for offline forensics. The editor scanner also risked overwriting the shared multi-agent UX report when re-run from Unity.
Solution: Split telemetry openers into owner-write and read-only variants; route player AUP validation, editor graph, dump export, and selected-camera gizmo through `IDataVault.TryReadOnlyHandle`. Add a fixed 32-byte dump header with `SCJ5`, version `3`, `CameraJuiceTelemetryEntry` stride, capacity, cursor, emitted count, and ring start index before raw 64-byte rows. Make `OOP_CameraShake_Scanner` upsert SHINOBU_354 into the shared report envelope.
Rejected Alternatives: Keeping mutable views for reads is faster to write but weakens accessor-proof discipline. A raw row-only dump is smaller, but postmortem tooling cannot prove ABI/version after future payload changes. Rewriting the shared report blindly would erase neighboring agent evidence.
Scalability potential: Runtime visual fidelity is unchanged across low/middle/high/ultra. The change protects forensic and tooling routes without adding gameplay ownership or quality-dependent schema changes.
Hardware Impact: Hot frame delta is effectively 0 us. Read-only views prevent accidental diagnostic mutation, and the dump header adds 32 cold fault-path bytes before fixed rows.

## Decision 024 - Fail-Closed Stale Impulse And Hash API Fix
Problem: Static API scan found `math.rotateleft` in `CameraJuiceBurstMath.HashState`, while the project source uses Unity.Mathematics `math.rol`; this was a direct compile-risk in SHINOBU_354. The same pass found that AUP/Vault failure paths cleared projection but could leave pending manual trauma and stale native state rows alive for a later frame.
Solution: Replace `math.rotateleft` with `math.rol`. Add `CameraJuiceFlagVaultUnavailable` and route missing seed, player AUP failure, and handle-open failure through `FailClosedProceduralCameraJuiceFrame`, which clears pending manual impulse, clears projection cache, and zeroes native state/impulse/projection rows when cached handles are available.
Rejected Alternatives: Letting manual trauma survive fail-closed frames would make an old local impact fire after the authoritative AUP context returns. Keeping the wrong rotate API would defer a known source error to Unity import/build.
Scalability potential: All quality tiers keep identical waveform fidelity. The change only affects failure determinism and telemetry flags; it does not change DTO layout, BufferIDs, save identity, rollback identity, or signal authority.
Hardware Impact: Normal frame cost is unchanged. Failure frames pay up to three cached Vault handle opens and three row writes, estimated below 3 us, while avoiding stale-shake corruption and a hard compile failure.

## Decision 025 - Fault Dump Success Gate
Problem: `DumpCameraJuiceTelemetry` marked `_cameraJuiceTelemetryDumped` before creating and writing `Dump_SHINOBU_354.bin`. A cold-path `IOException` or missing `Docs/AgentLogs` directory could suppress every later black-box dump while leaving no forensic artifact.
Solution: Create the dump directory on the cold fault path and set `_cameraJuiceTelemetryDumped` only after the raw `FileStream` span writes exit successfully. The telemetry route still reads via `IDataVault.TryReadOnlyHandle`, writes the same `SCJ5` version-3 header, and does not touch the hot Tick path.
Rejected Alternatives: Keeping the early throttle avoids repeat IO attempts after an error, but it violates the black-box mandate because failure becomes silent after one bad write. Retrying through managed logging was rejected because the binary dump is the proof artifact.
Scalability potential: Low/middle/high/ultra presentation math is unchanged. This only improves crash forensics; quality still scales fidelity, not authority, layout, or dump schema.
Hardware Impact: Normal frame cost is 0 us. Fault path adds one directory existence check/create before the dump stream and prevents losing the 300-frame ring after transient IO failure.

## Decision 026 - Raw Span Blackbox Rows
Problem: The camera-juice dump path must be a forensic binary artifact, not a managed field-loop serializer. `BinaryWriter` obscures row ABI, adds avoidable managed writer state on the fault path, and made the status proof weaker than the project black-box rule.
Solution: Define `CameraJuiceTelemetryDumpHeader` as an explicit 32-byte DTO and emit it plus `CameraJuiceTelemetryEntry` rows directly through `FileStream.Write(ReadOnlySpan<byte>)`. Ring wrap is serialized oldest-to-newest as at most two contiguous native spans. `ValidateCameraJuiceTelemetryLayout()` now gates both the 64-byte row and 32-byte header.
Rejected Alternatives: Keeping `BinaryWriter` was convenient but violates the raw-span dump expectation. Copying rows into a managed byte array was rejected because the Vault ring is already contiguous native memory and the fault route should not allocate an intermediate payload.
Scalability potential: Runtime visual tiers are unchanged. Low/middle/high/ultra still differ only in projection-shake fidelity; the black-box schema remains fixed and quality-independent.
Hardware Impact: Hot frame cost is 0 us. Fault path avoids 300 row-level writer calls and writes two native spans instead; estimated dump CPU saved is tens to hundreds of microseconds during crash export, while preserving the proof artifact.

## Decision 027 - Default Signal Snapshot Guards
Problem: `SignalBus<T>.GetFrameSnapshotArray()` returns `default` when a lane has no frame snapshot. `EvaluateCameraTraumaJob` guarded the mock lane but still read `.Length` directly from impact, high-speed impact, combat, seismic, and camera-impact read-only arrays. A default `NativeArray<T>.ReadOnly` can trip safety behavior or Burst/runtime differences before the zero-signal frame is processed.
Solution: Guard every SignalBus snapshot lane with `.IsCreated` before reading `.Length` or indexing. Empty or uncreated signal lanes now contribute zero trauma without invoking unsafe readback behavior.
Rejected Alternatives: Relying on default `ReadOnly.Length == 0` is not a contract worth betting the camera presentation path on. Allocating persistent empty arrays per signal type was rejected because it adds ownership and memory surface for a zero-event case.
Scalability potential: All quality tiers keep identical visual math for non-empty lanes. Low-event frames get the cheapest zero-signal path; high/ultra still scale octave/radius only through `GlobalQualityWeight`.
Hardware Impact: Normal impact-heavy frames pay five branch checks, below 1 us. Empty frames avoid possible safety exceptions and any hidden default-view handling cost.

## Decision 028 - Bounded Shake Phase
Problem: `CameraJuiceStateDTO.TimeAccumulator` was a float that grew forever. In a 100-hour endurance run, a phase value in the millions loses fractional precision, degrading damped sine/noise evaluation and weakening the black-box state hash.
Solution: Add `CameraJuiceBurstMath.WrapPhase()` and wrap the accumulator to a 1024-cycle window every Burst integration step. The DTO layout stays exactly 32 bytes, the phase remains deterministic, and non-finite phase input collapses to zero.
Rejected Alternatives: Expanding the state DTO with a double accumulator would violate the mandated 32-byte layout. Leaving the float unbounded would pass short tests but fail the long-session numerical stability mandate.
Scalability potential: Low/middle/high/ultra visual tiers are unchanged; only long-session numerical stability improves. Quality still controls octave/radius/frequency, not state schema or authority.
Hardware Impact: Adds one `floor` and one reciprocal multiply per visual frame, below 1 us, while preventing precision loss after long endurance sessions.

## Decision 029 - Runtime Curve Authoring Removal
Problem: `ShakeProfile` was already reduced to scalar input for the procedural route, but the source still declared `AnimationCurve FalloffCurve`. Even unused, that field preserves an object-oriented camera-shake authoring pattern and invites future per-frame curve evaluation.
Solution: Replace the runtime source field with scalar `FalloffExponent` and extend `OOP_CameraShake_Scanner` to flag `AnimationCurve` in camera/VFX source. The Burst route remains the only decay evaluator through Vault `CameraJuiceTuningDTO`.
Rejected Alternatives: Editing the existing `ShakeProfile_*.asset` YAML by hand was rejected because stale serialized `FalloffCurve` data is ignored once the source field is gone, and raw ScriptableObject YAML surgery needs Unity reserialize/import proof. Keeping the field under `#if UNITY_EDITOR` was also rejected because the token would keep teaching future agents that curve-shaped camera shake is acceptable.
Scalability potential: Low tier keeps scalar damped sine/triangle trauma decay; middle/high/ultra add continuous octave grit through `GlobalQualityWeight`, not through authored animation curves.
Hardware Impact: Current hot-path gain is 0 us because runtime did not evaluate the curve. The preventive gain is avoiding a likely future `AnimationCurve.Evaluate` path, which would add managed object state and curve sampling overhead to camera feedback.

## Decision 030 - No Package Dependency In Camera Shake Scanner
Problem: The editor-only `OOP_CameraShake_Scanner` imported Roslyn APIs. Even inside an `Editor/` folder, the repository Roslyn DLL metadata did not prove Editor-only plugin isolation, and adding a SHINOBU_354 asmdef would expand the compile graph for a proof tool.
Solution: Replace the scanner with a zero-dependency source parser that strips comments and string/char literals, detects forbidden camera-shake tokens, and evaluates `transform.localPosition` writes only inside hot camera method scopes.
Rejected Alternatives: Adding an editor asmdef plus precompiled Roslyn references was rejected because the task does not need semantic compilation. Editing shared plugin import metadata was rejected as cross-domain churn. Keeping the dependency was rejected because scanner evidence should not contaminate player/runtime assembly risk.
Scalability potential: Runtime low/middle/high/ultra presentation math is unchanged. The tooling route is now cheaper and deterministic for editor audit runs while preserving the same UX report proof.
Hardware Impact: Runtime impact is 0 us. Editor scanner avoids loading parser packages and reduces import/player-build risk; expected editor audit savings are small but compile-boundary risk is materially lower.

## Decision 031 - Pre-Sanitize NaN And AUP Delta Guard
Problem: `IntegrateProceduralShakeJob` flagged non-finite output after `SanitizeFloat3` already zeroed the vectors, so telemetry could miss a sanitized projection fault. `EvaluateCameraTraumaJob` also verified epicenter AUP but did not explicitly reject non-finite `PlayerAup` or non-finite `deltaD` before the localized `float3` cast.
Solution: Preserve `sanitizedInput` and `sanitizedOutput` flags before clearing invalid values, sanitize incoming trauma deltas before `math.saturate`, and include `CameraJuiceFlagNanSanitized` in XR/suppressed projection rows when input was invalid. The AUP path now rejects non-finite player/epicenter/delta values and clamps the local double delta to +/-262144 meters before float-local math.
Rejected Alternatives: Relying on the post-sanitize finite vector check was rejected because it made the black-box ring lie about repaired NaNs. Leaving unclamped double-to-float casts was rejected because a malformed AUP should fail closed or attenuate, not inject Infinity into distance math.
Scalability potential: Low/middle/high/ultra visual behavior is unchanged for finite inputs. Faulted or malformed inputs converge to zero projection with telemetry flags instead of changing DTO layout or authority routing.
Hardware Impact: Adds a few finite checks and one double3 clamp in accepted impulse records, below 1 us for the 32-record cap. It prevents fault propagation into projection matrices and preserves forensic evidence when sanitation occurs.

## Decision 032 - ReadOnly Telemetry Dump Pointer
Problem: Subagent static audit found `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry)` was called with `NativeArray<CameraJuiceTelemetryEntry>.ReadOnly` in the cold dump path. Unity's read-only view exposes `GetUnsafeReadOnlyPtr()` directly; using the NativeArray utility on the view is a compile-risk.
Solution: Replace the call with `telemetry.GetUnsafeReadOnlyPtr()` while preserving the raw `ReadOnlySpan<byte>` dump writer and oldest-to-newest wrapped ring emission.
Rejected Alternatives: Copying telemetry rows into a managed byte array or switching back to `BinaryWriter` was rejected because the Vault ring is already contiguous native memory and the dump must preserve ABI proof without managed serialization loops.
Scalability potential: Runtime low/middle/high/ultra presentation math is unchanged. This is cold fault-path pointer correctness only.
Hardware Impact: Hot frame impact is 0 us. Fault dump work remains two raw span writes after the header; the change removes a compile-risk without adding allocation.

## Decision 033 - Strict CSV Tuning Cell Parse
Problem: The cold `camera_trauma_profiles.csv` parser assigned `out` values directly into profile scalars. A malformed numeric cell could overwrite the safe default with `0`, and a token like `1abc` could be accepted as `1`.
Solution: Parse into temporary locals, commit only on successful parse, and reject tokens with trailing nonnumeric bytes after the optional sign, integer, and fractional lanes.
Rejected Alternatives: `float.Parse`, `string.Split`, and exception-based validation were rejected because this tuning bridge must stay span-based, predictable, and allocation-free.
Scalability potential: Low/middle/high/ultra profile rows keep valid authoring values; invalid required cells reject the row, and blank optional cells fall back to safe defaults instead of collapsing radius/translation/frequency.
Hardware Impact: Hot frame impact is 0 us. Cold CSV import adds one index-end check per numeric cell and avoids bad profile data propagating into every runtime camera impulse.

## Decision 034 - Required CSV Profile Fields Must Be Valid
Problem: After the first strict parser pass, required numeric profile fields could still fail silently into safe defaults. A future un-commented header row like `name,translation_gain,...` would become a valid profile name with default scalar values.
Solution: Require a non-empty profile name plus valid translation gain, rotation gain, and radius cells before writing a profile row. Optional decay/frequency cells fall back only when blank; malformed non-empty optional cells and extra non-empty columns reject the row.
Rejected Alternatives: Accepting headers or malformed required cells as default rows was rejected because it turns authoring errors into plausible but wrong runtime tuning. Managed CSV libraries remain rejected for allocation and dependency reasons.
Scalability potential: Low/middle/high/ultra profile rows now represent only valid authored data. Invalid rows are skipped and default seeded profiles remain the fallback route.
Hardware Impact: Hot frame impact is 0 us. Cold CSV import adds a few boolean checks per row and prevents bad authoring from poisoning every later camera impulse.

## Decision 035 - Manual Impulse Finite Gate
Problem: `EvaluateCameraTraumaJob` read `ManualDirectionalImpulseLocal` directly into the accumulation direction before a local finite guard. `QueueProceduralCameraJuiceManualImpulse` sanitizes normal sources, but a stale or malformed manual lane could still enter Burst with NaN/Infinity and only be repaired later by projection sanitation.
Solution: Sanitize manual trauma and manual direction at the start of `EvaluateCameraTraumaJob`, set `CameraJuiceFlagNanSanitized` when repaired, sanitize previous `DirectionalMemory`/`DirectionalTimer` before blending, and make `IntegrateProceduralShakeJob` treat non-finite directional memory/timer or prior impulse flags as sanitized input.
Rejected Alternatives: Relying on later projection sanitation was rejected because it lets the black-box row miss which input lane carried the fault. Clearing manual state outside the Burst job was rejected as incomplete because the Vault impulse row can be stale across fail-closed paths.
Scalability potential: Low/middle/high/ultra finite behavior is unchanged. Faulted manual presentation inputs collapse to zero/flagged projection without changing DTO layout, authority route, or quality schema.
Hardware Impact: Adds several finite checks and one `float3` sanitize on frames with camera juice evaluation, below 1 us for the fixed one-row impulse lane. It prevents malformed manual impulses from entering directional normalization and state hashes.

## Decision 036 - CSV Optional Cell Contract Alignment
Problem: File memory required malformed non-empty optional CSV cells to reject a profile row, but the code path temporarily kept defaults for those malformed cells. That made authoring errors look like intentional defaults.
Solution: Keep blank optional decay/frequency cells as defaults, but reject malformed non-empty optional cells through the same full-token `TryParseFloat` path used by required cells.
Rejected Alternatives: Silently defaulting malformed optional cells was rejected because it hides tuning data corruption. Managed parser validation remains rejected for allocation and dependency reasons.
Scalability potential: Low/middle/high/ultra camera trauma profiles now only come from valid authored rows or explicit blank defaults; invalid rows cannot silently bias the quality curve.
Hardware Impact: Hot frame impact is 0 us. Cold CSV import adds only branch checks around optional tokens.

## Decision 037 - Runtime Scalar Finite Gate
Problem: Vector lanes were sanitized, but non-finite scalar inputs from `GlobalQualityWeight`, effective scale, signal radius/severity, or Vault tuning could still contaminate attenuation, sine/noise amplitude, or projection math before the final output finite check.
Solution: Finite-gate quality, signal severity/radius, effective scale, and the tuning scalar lanes inside the Burst jobs. Repaired scalar lanes set `CameraJuiceFlagNanSanitized` before projection rows are emitted.
Rejected Alternatives: Relying on final vector sanitation was rejected because it can hide which input scalar carried the fault and lets NaN reach intermediate attenuation/projection math. Clamping in managed callers was rejected because SignalBus/Vault data must be defended at the mathematical kernel boundary.
Scalability potential: Low/middle/high/ultra finite behavior is unchanged. Faulted scalar inputs collapse to safe defaults or zeroed projection without changing DTO layout, BufferIDs, quality schema, or authority route.
Hardware Impact: Adds finite checks to the fixed one-row integrator and capped 32-signal evaluator. Expected cost is below 1 us on i3/MX350-class hardware; it prevents scalar NaN propagation into projection matrices and telemetry hashes.

## Decision 038 - Raw Signal Scalar Sanitizer
Problem: `math.max` can hide a malformed raw signal scalar when the paired scalar is finite, so the earlier finite gate could miss evidence of a NaN radius/severity lane before attenuation.
Solution: Add explicit `SanitizeSignalScalar` and `MaxFinite` helpers inside `EvaluateCameraTraumaJob`; every raw impact/seismic/mock scalar is finite-checked before any `math.max`, `math.abs`, amplitude sum, or radius max.
Rejected Alternatives: Trusting `math.max` behavior across Burst backends was rejected because NaN selection semantics are not the telemetry contract. Sanitizing after derived severity was rejected because it loses which raw lane was malformed.
Scalability potential: Finite low/middle/high/ultra behavior is unchanged. Malformed raw SignalBus scalar lanes are now flagged and replaced with neutral scalar contribution without altering authority, DTO layout, or quality schema.
Hardware Impact: Adds fixed finite checks to the 32-signal cap. Expected cost stays below 1 us; the gain is deterministic black-box evidence and no NaN entering severity/radius expressions.

## Decision 039 - Bounded Fault-Proven Telemetry Lane
Problem: Static audit found three remaining failure surfaces: finite but oversized manual/directional vectors could overflow `lengthsq` after component finite checks; the shake catch could disable presentation without writing the 300-frame black-box dump; and signed telemetry cursor overflow could produce a negative ring index during long endurance runs.
Solution: Clamp manual direction to a finite presentation envelope before `lengthsq`, clamp directional memory/timer to normalized bounds before blend/integration, route shake exceptions through `FailClosedProceduralCameraJuiceFault()` with a forced telemetry row and dump request, and convert the telemetry cursor/frame/dump cursor lanes to unsigned modulo with dump header version `4`.
Rejected Alternatives: Relying on final projection sanitation was rejected because `huge * rsqrt(inf)` can corrupt state before the output gate. Keeping a broad catch that only logs was rejected because it violates the black-box rule. Keeping signed cursors was rejected because a 100-hour target cannot accept overflow-dependent negative indexing.
Scalability potential: Finite low/middle/high/ultra behavior remains continuous. Later Loop 28 restored Math-LOD tap admission so low quality can physically skip zero-weight Simplex taps while preserving smooth visual amplitude; quality still changes presentation fidelity/cost only, not DTO layout, BufferIDs, save identity, or authority route.
Hardware Impact: Normal finite frame adds bounded vector clamps and continuous high/ultra noise taps; expected camera-only cost remains below the 0.1 ms suspicion threshold but requires Unity Profiler proof. Fault frames now pay cold dump I/O only after failure and preserve forensic evidence instead of silently disabling the route.

## Decision 040 - Vault Tuning Radius Route
Problem: CSV/profile hydration updated `CameraJuiceTuningDTO.LowTierRadiusMeters` and `UltraRadiusMeters`, but `EvaluateCameraTraumaJob` still used a hard-coded `32..120m` radius curve for AUP attenuation. Designer-authored radius values were therefore not part of the actual runtime impulse falloff.
Solution: Pass the existing Vault tuning row into `EvaluateCameraTraumaJob` as `[ReadOnly, NoAlias] NativeArray<CameraJuiceTuningDTO>.ReadOnly`, finite-gate the low/ultra radius fields, and lerp attenuation radius from the Vault values with safe `32..120m` fallbacks. Keep smooth quality weights for visual continuity.
Rejected Alternatives: Keeping constant radii was rejected because it made the cold CSV bridge partial. Adding a new SignalBus or BufferID for radius was rejected because the tuning row already owns that presentation fact. A hardware-tier branch was rejected; later Loop 28 uses quality-weight Math LOD admission only.
Scalability potential: Low/middle/high/ultra now use authored radius curves continuously through `GlobalQualityWeight`, while noise richness remains scalar-blended. Quality still changes presentation fidelity only; it does not change DTO layout, BufferIDs, save identity, rollback identity, or authority route.
Hardware Impact: Adds one read-only tuning-row load and finite checks in the capped evaluator, below 1 us. It does not buy ALU savings; it fixes authoring truth and avoids another route for profile radius.

## Decision 041 - Smooth Visual Weight With Math LOD Tap Admission
Problem: Loop 26 removed high/ultra quality branch gates to avoid feature-pop semantics, but that made low-quality frames still evaluate all six `noise.snoise` taps. That violates the explicit ALU shedding requirement for weak/mobile devices.
Solution: Keep smooth visual weights, move first high-octave onset to quality `0.30`, and admit Simplex taps only when their smooth weight is nonzero. Below `GlobalQualityWeight < 0.30`, the integrator executes only damped sine/triangle math. Ultra grit taps are admitted only above the smooth ultra window.
Rejected Alternatives: Always computing all taps was rejected because it burns mobile ALU for zero visual contribution. A hardware-tier boolean was rejected because it would change behavior by device class instead of by the continuous quality scalar. Abrupt amplitude steps were rejected because they would cause visible feature popping.
Scalability potential: Low tier pays no Simplex cost and keeps readable impact feedback; middle tier admits high taps gradually; high/ultra add grit with smooth amplitude. Quality changes presentation fidelity/cost only and never changes DTO layout, BufferIDs, save identity, rollback identity, or authority route.
Hardware Impact: On i3/MX350/Quest-class devices at quality below `0.30`, six `noise.snoise` evaluations are skipped per camera juice frame. Expected saving: 3-12 us depending Burst backend and CPU.

## Decision 042 - Unity Time Runtime Eviction
Problem: Static subagent audit found `Time.realtimeSinceStartup` and `Time.time` in the SHINOBU_354 runtime owner for development budget logging and slow dependency cadence. They were not Burst shake math and not `Time.deltaTime`, but they weakened the strict no-Unity-Time runtime proof.
Solution: Replace dev frame-budget measurement with `Stopwatch.GetTimestamp()` and throttle logs using a dt-driven owner cooldown. Replace `Time.time` slow dependency cadence with a deterministic slow-tick countdown.
Rejected Alternatives: Keeping Unity `Time` because the code was non-critical was rejected; the owner file is the forensic surface for this mandate. Polling dependencies every SlowTick was rejected because it would increase cold dependency work cadence. A coroutine/timer was rejected because it would reintroduce managed scheduling.
Scalability potential: Low/middle/high/ultra camera trauma behavior is unchanged. The dependency refresh cadence is now tick-count based and quality-independent; visual scalability remains in the Burst projection route.
Hardware Impact: Runtime savings are negligible. Structural gain is deterministic proof: scoped SHINOBU_354 runtime files now have zero `Time.` hits, and development timing no longer depends on Unity global clock state.

## Decision 043 - Burst Budget Fault Artifact
Problem: The 300-frame telemetry ring recorded `BurstExecutionMicroseconds`, but a frame above the 0.1 ms suspicion threshold did not set an explicit fault bit or request a dump after the current row was written.
Solution: Add `CameraJuiceFlagBurstBudgetExceeded` plus a fixed `CameraJuiceBurstBudgetMicroseconds=100` threshold. `RunProceduralCameraJuice` marks the projection DTO after the synchronous Burst section, and `RecordCameraJuiceTelemetry` services the pending dump request after writing the row so the offending frame is in `Dump_SHINOBU_354.bin`.
Rejected Alternatives: Scheduling the two tiny jobs and completing them for same-frame projection was rejected again because that would add a hidden fence. Dumping before `RecordCameraJuiceTelemetry` was rejected because it can miss the over-budget frame.
Scalability potential: Low quality already sheds Simplex taps through Math LOD admission; high/ultra may intentionally spend more ALU, but any section above 100 us becomes a black-box proof artifact instead of silent drift.
Hardware Impact: Normal frames add one scalar compare and one rare flag write. Fault frames pay cold dump I/O only after telemetry capture; expected normal-frame cost is below 1 us.

## Decision 044 - Sidecar Audit Closure: Temporal Admission, Vault-Only AUP, Fault-Frame Dump
Problem: Sidecar static audit found four concrete surfaces after Loop 28: hard quality threshold tap gates could still be interpreted as binary quality switches; invalid native state dumped before sanitized fault values were copied into `_cameraJuiceLast*` and recorded; `TryResolvePlayerCameraJuiceAup` still had a hot fallback to `_playerMovement.CurrentAup`; and the OOP scanner missed hot `transform.localRotation` / `transform.localEulerAngles` shake routes.
Solution: Replace hard `math.step(qualityThreshold, quality)` tap gates with deterministic `TemporalAdmission01(sequence, salt, smoothWeight)`, so expected Simplex ALU scales with continuous quality weight and no hard quality threshold exists. Remove the Gameplay AUP fallback; the procedural route now reads only the cached read-only `PlayerKinematicState` Vault row and otherwise fails closed. Move invalid native state dump ordering so sanitized state/projection is copied into `_cameraJuiceLast*`, recorded into the telemetry ring, then dumped. Extend the scanner to cover hot local rotation and local Euler mutations.
Rejected Alternatives: Keeping the Gameplay fallback was rejected because it creates a direct sibling dependency in the hot AUP route. Dumping before telemetry record was rejected because it can omit the fault frame. Keeping hard quality thresholds was rejected because the project forbids binary quality switches even when the visual amplitude is smooth. Ignoring local rotation/euler scans was rejected because camera shake can be rotational.
Scalability potential: Low/middle/high/ultra presentation richness is now admitted by deterministic temporal sampling against smooth weights. Weak devices see lower expected Simplex cost; high/ultra devices converge toward every-frame high/grit taps. Quality still cannot alter DTO layout, BufferIDs, save identity, rollback identity, or authority route.
Hardware Impact: Low-quality frames skip Simplex taps in proportion to the smooth admission weight instead of always paying six taps or crossing a hard quality threshold. Expected saving remains profiler-dependent; static path now has no hard `math.step(0.30/0.65, quality)` gates and no hot Gameplay AUP fallback.

## Decision 045 - Exact Zero Temporal Admission
Problem: `TemporalAdmission01` used `dither <= weight`, so a mathematically zero smooth weight could still admit a Simplex tap when the 24-bit dither value was exactly zero. The visual term was multiplied by zero later, but the low-tier ALU proof was not exact.
Solution: Add explicit `weight <= 0f -> 0f` and `weight >= 1f -> 1f` exits, then use `dither < weight` for the middle range.
Rejected Alternatives: Relying on the rare 1-in-16,777,216 dither leak was rejected because the mandate requires mathematical proof, not statistical excuse. Always computing the taps and masking contribution was rejected because it burns mobile ALU for no visible output.
Scalability potential: Low tier now executes exactly damped sine/triangle only when smooth admission weight is zero. Middle/high/ultra still use deterministic temporal admission against the continuous quality scalar.
Hardware Impact: Normal overhead is two scalar comparisons inside the helper. Low-quality zero-weight frames now have a hard guarantee of zero Simplex evaluations from this route.

## Decision 046 - Reproducible UX Scanner Report Fields
Problem: `OOP_CameraShake_Scanner` had been extended to catch hot local rotation/euler mutations, but a future editor menu run would replace the shared SHINOBU_354 UX object without `cameraRelevantFiles`, status, burst-budget proof, or a concise manual proof field. The current JSON on disk was correct, but the tool was not the reproducible source of that proof.
Solution: Add a `cameraRelevantFiles` counter and emit `status`, `burstBudgetProof`, and `manualProof` in the editor scanner output. The scanner still preserves adjacent agents through the shared multi-agent envelope and remains zero-dependency editor-only code.
Rejected Alternatives: Leaving the JSON as a hand-maintained report was rejected because the scanner is the Task 19 proof artifact. Adding Roslyn or a new editor asmdef was rejected because the lexical scanner is enough for this bounded camera-shake inquisition and avoids compile graph expansion.
Scalability potential: Runtime low/middle/high/ultra camera trauma behavior is unchanged. This improves proof reproducibility only; the runtime quality scalar still governs presentation fidelity and expected Simplex admission.
Hardware Impact: Runtime cost is 0 us because the scanner is `#if UNITY_EDITOR`. Editor scan cost gains one integer counter and a few JSON fields; no player frame impact.
