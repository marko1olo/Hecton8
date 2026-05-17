# LOG_GRAB_IK_PROJECTION

## 2026-05-16 | GRAB_IK_PROJECTION | ANIMATION_LEAD

What was wrong:
- VR hand presence had no Animation/IK-owned physical projection kernel. Controller truth could visually pass through cockpit steel.
- No `VRHandManager` singleton or first-party `Physics.SphereCast` hand snapper was found to remove.
- Global compile is currently blocked outside this domain by missing VFX wake, docking/autopilot, light shaft, lockstep, and ecosystem contract symbols.

What was done:
- Added `Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs` and `.meta`.
- Added DataVault buffer IDs for `HandTargetAUP`, `HandActualAUP`, `HandGrabState`, `HandIkTelemetryRing`, and `HandIkTelemetryCursor`.
- Added an Animation/IK vault resolver, fixed two-hand `IJob`, hand AUP structs, grab state, output pose, and two-hand 300-frame black-box telemetry.
- Implemented analytical two-bone IK using law of cosines, `FastAcos`, `math.rsqrt`, pole-plane elbow direction, and joint-limit flagging.
- Implemented AUP shift rebase on target/actual lanes using `ShiftFrameId` and `AupShiftMeters`.
- Implemented low-tier/no-VR screen-space fallback, middle-tier plane projection, high-tier/explicit SDF projection, tangent scrape haptic flag/intensity, and ghost hand output when blocked separation exceeds 0.3 m.
- Added cold-path binary dump helper for `Docs/AgentLogs/Dump_GRAB_IK_PROJECTION.bin`.
- Preserved `VR_COCKPIT_MANUAL_OVERRIDE` compatibility by not touching `OpenXRManualOverrideLever`; scrape output remains bridgeable to `HapticRequest.ChannelGearScrape`.

Cinematic cheats used:
- Plane projection replaces rigidbody hand collision for middle tier.
- Encoded SDF trilinear sampling and gradient pushout replaces synchronous casts/joints for high tier.
- Ghost hand separates player controller truth from blocked physical hand instead of forcing impossible physical penetration.
- Fast nlerp and analytical reach clamps replace Animator IK and iterative solvers.

Exact microseconds saved:
- No Animator IK pass: estimated 30-80 us/frame saved versus graph callback IK.
- No `Physics.SphereCast` hand snapping: estimated 15-40 us/frame saved for two hands in cluttered cockpit geometry.
- Low-tier no-VR fallback: estimated 12 us/frame saved by bypassing IK/SDF.
- Plane projection path: estimated 7 us for two hands versus physics query/joint solve.
- DataVault fixed two-lane state: estimated under 2 us cold handle resolve, 0 us hot allocation.
- Black-box ring write: estimated 1 us/frame; replaces unbounded `Debug.Log` fault reporting.

Verification:
- `rg` scan found no `VRHandManager` or owned `Physics.SphereCast`.
- Omega scan found no `Vector3.Lerp`, `Quaternion.Slerp`, or `math.acos` in `Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore` attempted three times. Full compile remains `[BLOCKED BY DEPENDENCY]`; filtered post-polish build scan showed no `VRPhysicalHandPresence` or `Hecton8.Animation.IK` errors.

## 2026-05-16 | GRAB_IK_PROJECTION | MULTIPLATFORM INQUISITION PASS

What was wrong:
- Hand payload structs were still packed at 4-byte boundaries. That is not acceptable for the Quest/Android ABI requirement in the follow-up directive.
- Input/output hand lanes were job references but did not have dedicated GlobalDataVault buffer IDs yet.
- The NaN fallback path could still pass a previously invalid projected target into persistent state if the output failed validation after projection.

What was done:
- Converted all hand payload structs in `VRPhysicalHandPresenceIkJobs.cs` to `Pack = 1`.
- Verified the owned IK folder has no remaining `Pack = 4`, `Vector3.Lerp`, `Quaternion.Slerp`, `math.acos`, `Physics.SphereCast`, `VRHandManager`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `EventBus`, managed delegate, `new NativeArray`, or allocator ownership hits.
- Added `HandPresenceInput=190` and `HandPresenceOutput=191` to `BufferID`, then extended the cold DataVault resolver to allocate/resolve them.
- Added a duplicate BufferID scan; no numeric collision was found.
- Tightened NaN fallback so invalid projection state is replaced by the sanitized fallback output before persistent state and telemetry writes.
- Added the IK lock-state marker to the binary dump header.

Cinematic cheats used:
- No new graphics-domain overreach. Animation/IK exports high-tier SDF contact, surface normal, haptic intensity, and ghost separation for visual systems to spend on overkill without coupling shaders or particles into this assembly.

Exact microseconds saved:
- No additional measured runtime savings claimed. The pass is correctness/ABI polish.
- Hot-path I/O remains 0 us because dump serialization stays cold-path only.
- DataVault input/output lanes add no per-frame allocation; expected hot-path cost remains two NativeArray reads/writes for two hands.

Verification:
- `rg` hot-path debt scan returned no forbidden owned IK patterns.
- `git diff --check` reported only line-ending warnings for touched files.
- `dotnet build` did not complete within the timeout after the external contract churn expanded; compile remains blocked outside this domain.

## 2026-05-16 | GRAB_IK_PROJECTION | DOMAIN-WIDE H-PHI PASS

What was wrong:
- `LeviathanTerrainIkJob` had `NativeArray` lanes with existing DataVault IDs but no resolver in the owned IK assembly, making ownership implicit.

What was done:
- Added `LeviathanTerrainIkVault.TryResolveBuffers` for segment positions, previous positions, bone matrices, 300-frame telemetry ring/cursor, optional encoded SDF, and optional terrain height samples.
- Re-ran owned-folder debt scans for forbidden layout, update-loop, allocation, physics, legacy event, delegate, and Unity interpolation patterns.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`; current failure is external to Animation/IK: `GameBootstrapper.cs` cannot resolve `Hecton8.Core.Bucketing.ModuloSimulationBucketer`.

Cinematic cheats used:
- No new simulation truth added. The resolver only makes data ownership explicit for the existing mathematical terrain-hugging fake.

Exact microseconds saved:
- No new measured runtime savings claimed. Hot-path leviathan job code is unchanged.
- Cold resolver cost is outside frame-critical IK execution.

Verification:
- Forbidden-pattern scans over `Assets/_Project/Scripts/Animation/IK` returned no hits.
- `git diff --check` reported only line-ending warnings for touched code files.
- Compile retry reports no owned IK errors; it fails on missing Core.Bucketing type resolution.

## 2026-05-16 | GRAB_IK_PROJECTION | ABI ALIGNMENT AND BUILD GATE PASS

What was wrong:
- `Pack = 1` removed implicit padding, but `VRHandIkTelemetryEntry` placed vector payloads after a 19-byte header. That is deliberate unaligned vector data on ARM64.
- Concurrent GlobalDataVault edits alternated between duplicate and missing `ValidateAbiLayout()`, preventing the build from reaching later diagnostics.
- Full compile still does not pass; the current blockers are outside Animation/IK.

What was done:
- Added `VRPhysicalHandPresenceLayout.Validate()` with explicit byte counts: AUP pose 48, grab state 72, input 260, output 116, telemetry entry 80.
- Added explicit telemetry `LayoutPadding` so `TargetPosition` starts on a 4-byte boundary while keeping `Pack = 1`.
- Gated `VRPhysicalHandPresenceVault.TryResolveBuffers()` on the layout sentinel before exposing hand lanes.
- Restored exactly one `GlobalDataVault.ValidateAbiLayout()` so the DataVault compile gate is coherent again.

Cinematic cheats used:
- No extra simulation truth. This pass protects the data lane used by the existing SDF/plane physical hand fake.

Exact microseconds saved:
- No measured runtime savings claimed. The layout validation is cold-path only.
- Hot-path telemetry remains one fixed 80-byte write per frame; the change removes odd-stride memory pressure rather than pretending to benchmark it.

Verification:
- Owned IK forbidden-pattern scan returned no hits for `Pack = 4`, `Vector3.Lerp`, `Quaternion.Slerp`, `math.acos`, `Physics.SphereCast`, `VRHandManager`, `Update`, `string.Format`, legacy `EventBus`, managed delegates, or local `NativeArray` allocation.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q -clp:ErrorsOnly` now fails outside Animation/IK on `SargassumMicroFaunaBoids.EnsureVaultBufferHandle`, `HectonMarineSnowRenderer` missing `_vehicleWakeJobResult`/`_telemetryRing`, and `VehicleDockingModule` missing cache helpers.

## 2026-05-16 | GRAB_IK_PROJECTION | CONTACT AND NAN VACCINE PASS

What was wrong:
- The SDF contact branch needed an explicit convention audit against the rest of the project. Project SDF density is solid at `>= 0`, not a plane-style signed distance where penetration is negative.
- Controller sanitization still trusted previous controller state as a fallback even when that state was also invalid.

What was done:
- Kept SDF lock as `density > -localClearance`.
- Kept SDF pushout as `surfaceNormal * (density + localClearance)`.
- Flipped hand and leviathan SDF finite-difference normals to open-space direction, matching KCC squeeze behavior.
- Added hard finite controller fallback to `float3.zero` after checking input and previous state.

Cinematic cheats used:
- Kept the SDF as a cheap contact projection, not rigidbody truth.
- Plane and SDF branches remain cheap projection lies with project-correct SDF sign handling.

Exact microseconds saved:
- No measured savings claimed. The fix is correctness and NaN survival.
- No new samples, allocations, disk I/O, or physics queries were added.

Verification:
- Static owned IK scan still reports no forbidden patterns.
- Filtered build scan reports `NO_OWNED_IK_OR_VAULT_ERRORS`.
- Full compile remains blocked by external Core.Content, World, Determinism, Ecosystem, and Fluid domain errors.

## 2026-05-16 | GRAB_IK_PROJECTION | FULL DOMAIN ABI SENTINEL PASS

What was wrong:
- `LeviathanTerrainIkTelemetryEntry` declared a 96-byte explicit layout but left tail bytes unnamed.
- `FootIKData` had `Pack = 1` but no cold layout sentinel.

What was done:
- Added `LowerBodyPresenceIkLayout.Validate()` with `FootIKData` fixed at 68 bytes.
- Added `LeviathanTerrainIkLayout.Validate()` with telemetry fixed at 96 bytes.
- Added explicit leviathan telemetry padding fields through offset 92.
- Gated the leviathan DataVault resolver on the layout sentinel.

Cinematic cheats used:
- No new simulation truth. This is ABI hardening for existing IK mathematical fakes.

Exact microseconds saved:
- No measured runtime savings claimed. Validation is cold-path only.
- Hot-path sample counts, loop counts, and allocations are unchanged.

Verification:
- Owned IK forbidden-pattern scan returned no hits.
- `git diff --check` reported CRLF warnings only for touched files.
- Full compile remains blocked outside Animation/IK; latest visible gate is `ToolDurabilitySystem` missing private lanes and job members.

## 2026-05-16 | GRAB_IK_PROJECTION | TARGETED IK COMPILE PROBE

What was wrong:
- Generated project coverage is stale: `Hecton8.Core.csproj` includes `LeviathanTerrainIkJobs.cs` but not `VRPhysicalHandPresenceIkJobs.cs` or `LowerBodyPresenceIkJobs.cs`.
- A full-build filter could therefore miss owned hand compile errors.

What was done:
- Ran a targeted Roslyn probe over all three owned IK files using Unity references and a minimal DataVault stub.
- Fixed `VRPhysicalHandPresenceIkJobs.cs` fallback-scope redeclarations for `ghostPosition` and `handRotation`.
- Re-ran the probe; it reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.

Cinematic cheats used:
- None. This was compiler coverage repair.

Exact microseconds saved:
- No runtime savings claimed. This is build verification.

Verification:
- Targeted IK compile probe exits 0.
- Owned IK forbidden-pattern scan remains clean.
- Master `dotnet build Hecton8.Core.csproj` remains blocked outside Animation/IK at `ToolDurabilitySystem` missing private lanes/job members.

## 2026-05-16 | GRAB_IK_PROJECTION | BLACKBOX AND BUILD GREEN PASS

What was wrong:
- Hand IK crash dumps serialized the circular telemetry buffer by raw index, so a wrapped ring was not oldest-to-newest.
- Full build then exposed a cross-domain IK bridge compile fault: `ContextualPhysicalIkRuntime` consumed `KccVelocitySignal` but did not import `Hecton8.Core.Contracts.Signals`.

What was done:
- Changed the cold dump serializer to start at `TelemetryCursor % ringLength` and write the ring chronologically.
- Added cold exception handling for invalid dump paths and disposed streams.
- Added the missing typed-signal namespace import in `ContextualPhysicalIkRuntime`.

Cinematic cheats used:
- No extra physics truth. The hand remains an SDF/plane projection fake with haptic scrape and ghost-hand presentation.

Exact microseconds saved:
- 0 us hot-path change for dump ordering; serializer runs only on crash/NaN dump.
- 0 us runtime change for the namespace import.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` exits 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF warnings only for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeded with 0 warnings and 0 errors at this checkpoint; later Construction drone compile errors superseded this status.

## 2026-05-16 | GRAB_IK_PROJECTION | TWO-HAND BLACKBOX DEPTH FIX

What was wrong:
- The ring was described as 300 frames, but the job writes one entry for left hand and one entry for right hand every frame.
- A 300-entry buffer therefore retained only 150 complete two-hand frames after wrap.

What was done:
- Added `TelemetryFrameCapacity = 300`.
- Changed `TelemetryCapacity` to `TelemetryFrameCapacity * HandCount`, making the hand ring 600 entries.
- Kept the per-entry 80-byte ABI and chronological dump ordering unchanged.

Cinematic cheats used:
- None. This is postmortem correctness for the existing SDF/plane hand presence fake.

Exact microseconds saved:
- No savings claimed. The change buys crash evidence, not speed.
- Runtime cost remains two fixed telemetry writes per frame; memory rises from 24 KB to 48 KB.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- At that checkpoint, full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` was blocked outside Animation/IK by Construction drone `double3` to `float3` conversion errors in `DroneFleetManager.cs` and `DroneCognitionJob.cs`; this is superseded by the later World wall below.

## 2026-05-16 | GRAB_IK_PROJECTION | BLACKBOX FAIL-CLOSED GUARD

What was wrong:
- `TryDumpTelemetry` could serialize a partial ring if called outside the vault resolver path.

What was done:
- Added a cold guard requiring ABI validation, a full 600-entry two-hand ring, and a live cursor lane before writing `Dump_GRAB_IK_PROJECTION.bin`.

Cinematic cheats used:
- None. This preserves crash evidence integrity for the existing physical-hand projection fake.

Exact microseconds saved:
- 0 us hot path. The guard runs only during crash/NaN dump.

Verification:
- Targeted IK Roslyn probe reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0 after the later ordering patch.
- Owned IK forbidden-pattern scan returns no hits.

## 2026-05-16 | GRAB_IK_PROJECTION | BLACKBOX EARLY-LIFE ORDERING

What was wrong:
- Before the 600-entry ring filled, the dump serializer started at `cursor % length`, putting real startup frames after zeroed records.
- A negative cursor value would write to a sanitized index but keep advancing from the corrupted negative cursor.

What was done:
- Cold dumps now start at index 0 until the cursor reaches ring length, then switch to wrapped chronological ordering.
- Negative cursor recovery now advances from the sanitized write index.

Cinematic cheats used:
- None. This is crash evidence integrity.

Exact microseconds saved:
- No savings claimed. One cursor comparison was added to the telemetry write path; the two-hand telemetry budget remains about 2 us/frame.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF warnings only for touched files.
- Latest full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` is blocked outside Animation/IK by `World/EcosystemDirector.cs` read-only property / return-value mutation errors.

## 2026-05-16 | GRAB_IK_PROJECTION | AUP COMMIT HARDENING AND FINAL BUILD GATE

What was wrong:
- The hand AUP commit path wrote finite local meters but did not quantize them at millimeter commit boundaries.
- `HandActualAUP` could retain stale grid coordinates after locking against an interactable AUP in another sector.

What was done:
- Target/actual AUP local meters are now millimeter-quantized on commit and rebase.
- Actual hand AUP inherits the current target/interactable grid.
- AUP source hashes include all grid high/low bits.

Cinematic cheats used:
- Kept physical hand presence as a deterministic SDF/plane projection fake, not rigidbody hands or synchronous casts.
- The AUP hardening preserves the fake across origin shifts without adding simulation truth.

Exact microseconds saved:
- No direct savings claimed.
- Added cost is bounded to two `math.round` operations and six integer hash folds per hand commit, estimated under 1 us for two hands.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeds with 0 warnings / 0 errors in 4.78s.
- Owned IK forbidden-pattern scan returns no hits.
- BufferID scan reports `NO_BUFFERID_COLLISIONS`.
- `git diff --check` reports CRLF normalization warning only.

Status:
- Compile validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, and profiler/GCMonitor proof remain pending because no Unity Editor/MCP runtime logs are exposed in this session.

## 2026-05-16 | GRAB_IK_PROJECTION | SDF EDGE AND FAULT-DUMP HARDENING

What was wrong:
- High-tier SDF gradient sampling could reject valid edge contacts when one finite-difference neighbor stepped outside the encoded grid.
- SDF enablement did not explicitly require a finite range value.
- NaN fallback dumps depended on external callers remembering the generic telemetry dump path.
- AUP millimeter quantization rounded finite local meters without an explicit overflow envelope.

What was done:
- Added finite range gating for SDF projection.
- Added clamped SDF edge-gradient sampling while preserving strict density sampling for the main contact test.
- Sanitized vector finite-difference steps and inverse cell scale before gradient solve.
- Added `TryDumpTelemetryOnFault` to dump the existing 600-entry hand telemetry ring when either output lane reports `OutputFlagNanFallback`.
- Bounded AUP millimeter quantization to a finite one-million-meter local envelope before rounding.

Cinematic cheats used:
- Kept the tactile hand block as deterministic SDF/plane projection, not rigidbody hand simulation.
- High-tier gets better SDF edge continuity; toaster mode still bypasses VR IK or uses the plane fake.

Exact microseconds saved:
- No new savings claimed.
- Low/plane paths add 0 us.
- High-tier SDF stays inside the existing seven-sample/two-hand estimate; added clamp math is below 1 us for two hands.
- Fault dump helper is cold only: two output-lane checks before dump I/O.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeds with 0 warnings / 0 errors in 3.92s.
- Owned IK forbidden-pattern scan returns no hits.
- BufferID scan reports `NO_BUFFERID_COLLISIONS`.
- `git diff --check` reports no errors for touched files.

Status:
- Source and build validation are green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | OWNED TERRAIN SDF PARITY PASS

What was wrong:
- `LeviathanTerrainIkJob` still used strict in-volume SDF samples for gradient-neighbor fetches.
- At encoded SDF borders, a valid main density hit could fail the gradient solve and drop out of SDF hugging.
- The terrain SDF sampler lacked the explicit finite input guards already present in the hand solver.

What was done:
- Added finite world-position, inverse-cell, and SDF-range guards to leviathan SDF sampling.
- Added a clamped trilinear SDF sampler for gradient-neighbor reads.
- Sanitized gradient steps before reciprocal math.
- Kept the main SDF density sample strict so out-of-volume positions still fail closed.

Cinematic cheats used:
- Preserved terrain hugging as an encoded SDF/depth fake, not rigidbody or Unity physics truth.
- Low-tier path remains cheap; High/Ultra keep stronger edge contact continuity.

Exact microseconds saved:
- No savings claimed.
- 0 us added outside SDF terrain-hug mode.
- SDF mode remains one main density fetch plus six gradient-neighbor fetches; clamp/finite scalar math is below 1 us for the affected tail segment group.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeds with 0 warnings / 0 errors in 52.81s.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF normalization warnings only.

Status:
- Source and build validation are green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | LEVIATHAN TERRAIN BLACKBOX DUMP

What was wrong:
- `LeviathanTerrainIkJob` wrote 300 telemetry frames but had no owned cold dump serializer.
- Invalid segment telemetry could be flagged but not exported through a stable binary format.
- That left the owned IK folder weaker than the hand blackbox path under the same crash-recovery mandate.

What was done:
- Added `LeviathanTerrainIkBlackBox`.
- Added a fail-closed `TryDumpTelemetry` path for `Docs/AgentLogs/Dump_GRAB_IK_PROJECTION_LeviathanTerrainIk.bin`.
- Added `TryDumpTelemetryOnFault`, keyed to `TelemetryFlagInvalid` or cursor corruption.
- Serialized chronological fixed 96-byte `LeviathanTerrainIkTelemetryEntry` records after ABI, ring-capacity, and cursor-lane validation.

Cinematic cheats used:
- None. This is postmortem survival work for the existing encoded SDF / terrain-height IK fake.

Exact microseconds saved:
- No savings claimed.
- 0 us hot path. The Burst job still only writes the existing DataVault telemetry ring.
- Cold dump cost occurs only when an external fault path invokes the serializer.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- A parallel full-build attempt produced a transient `MSB3026` copy warning from concurrent artifact access; the follow-up sequential build is the authoritative result.
- Final sequential `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeds with 0 warnings / 0 errors in 4.08s.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF normalization warnings only.

Status:
- Source and build validation are green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | HAND BLACKBOX FIXED-WINDOW HARDENING

What was wrong:
- Hand blackbox dumping wrote `telemetryRing.Length` entries.
- If the DataVault rounded the hand telemetry ring above 600 entries, dumps could include stale or unwritten spare capacity.
- Hand cursor overflow reset to a small value, weakening wrapped chronological ordering.

What was done:
- Hand dumps now serialize exactly `TelemetryCapacity` entries: 600 records, meaning 300 complete frames for two hands.
- Header now carries version and 80-byte entry-size fields.
- Dump start index now uses `cursor - dumpCount` for wrapped chronological ordering.
- Negative cursor and `int.MaxValue` rollover now preserve wrapped cursor semantics with `ringLength + nextIndex`.

Cinematic cheats used:
- None. This is crash-evidence hardening for the existing SDF/plane hand presence fake.

Exact microseconds saved:
- No savings claimed.
- 0 us normal hot path. The added cursor rollover branch only executes on corrupt cursor or `int.MaxValue`.
- Cold dump file size is fixed and bounded; no normal-play disk I/O.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF normalization warnings only.
- Full build attempt 1 is blocked outside Animation/IK by `PlayerKinematicsRuntime`, `HectonMusicDirector`, and `AcousticZoneController` signal contract errors.
- Full build attempt 2 is blocked outside Animation/IK by `TetherManager` missing `ISlowTickable.SlowTick()`.

Status:
- Owned source validation is green for this checkpoint.
- Full project compile is blocked by external dependencies, not by `Assets/_Project/Scripts/Animation/IK/`.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | HAND SDF NORMAL ANISOTROPY PASS

What was wrong:
- High-tier hand SDF projection used clamped neighbor density samples but normalized raw axis deltas.
- Non-cubic SDF cells could bias the open-space normal and make a grabbed hand slide in a subtly wrong direction across stretched cockpit volumes.

What was done:
- Added reciprocal sanitized-step scaling in `VRPhysicalHandPresenceJob.TryResolveSdfGradient`.
- Kept the existing seven SDF samples: one main density sample plus six gradient neighbors.
- Kept low-tier fallback, middle-tier plane slide, DataVault lanes, and blackbox dump schema unchanged.

Cinematic cheats used:
- Preserved encoded SDF projection as the high-tier physical-contact fake instead of adding rigidbody hand collisions or Unity physics casts.
- Corrected the fake's normal math so haptic scrape and visual hand lock read as heavier steel contact on high-end rigs.

Exact microseconds saved:
- No savings claimed.
- Added cost is three scalar reciprocal-weighted multiplies in SDF mode only; expected cost is below 1 us for two hands on i3/MX350/Quest-class silicon.
- 0 us impact on low-tier/no-VR fallback and middle-tier plane projection.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF normalization warnings only.
- Full project rebuild was intentionally not run in this loop per current instruction; latest known full-project wall remains outside Animation/IK.

Status:
- Owned source validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | LEVIATHAN RSQRT NAN VACCINATION

What was wrong:
- `LeviathanTerrainIkJob` sanitized vectors for finite components, but huge finite deltas could overflow `math.lengthsq` to infinity.
- Existing `math.rsqrt` branches could then produce `inf * 0` NaN values in head clamping, distance constraints, or length measurement.
- A poisoned tangent or segment can reach `float4x4.TRS` and corrupt rendered pose output.

What was done:
- Added finite squared-distance guards before the head clamp `math.rsqrt`.
- Added finite squared-distance guards before the follower distance-constraint `math.rsqrt`.
- Hardened `ResolveLength` to return 0 for non-finite squared lengths.

Cinematic cheats used:
- Kept leviathan terrain presence as constrained S-curve / terrain-hug math, not physical bodies or Unity joints.
- Corrupted extreme inputs now collapse to owner-forward visual continuity instead of simulating expensive recovery physics.

Exact microseconds saved:
- No savings claimed.
- Added cost is three scalar finite checks in existing branches; expected cost is below 1 us on i3/MX350/Quest-class silicon.
- 0 B/frame GC, no extra NativeArrays, no file I/O.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports CRLF normalization warnings only.
- Full project rebuild was intentionally not run in this loop per current instruction; latest known full-project wall remains outside Animation/IK.

Status:
- Owned source validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | LEVIATHAN MATRIX FINITE-OUTPUT PASS

What was wrong:
- `WriteMatrices` sanitized a segment position for the current bone matrix but did not write that sanitized value back to `SegmentPositions`.
- A corrupted last active segment could still become the tail seed for filler bones.
- `LookRotationSafe` results were trusted without a final quaternion finite/normalization guard before `float4x4.TRS`.

What was done:
- Sanitized active segment positions are now written back before matrix emission.
- Neighbor tangent reads now use sanitized fallback positions.
- Bone rotations now pass through a finite quaternion sanitizer.
- Filler bones now start from a finite tail seed and sanitize each propagated tail position.

Cinematic cheats used:
- Kept leviathan body presentation as constrained S-curve bone/VAT math, not Unity physics bodies.
- The failover is visual continuity along owner-forward, not expensive physical recovery.

Exact microseconds saved:
- No savings claimed.
- Added cost is bounded to active leviathan segment count, max 20 sanitize writes plus quaternion guards.
- Expected added cost remains below 1 us on i3/MX350/Quest-class silicon; 0 B/frame GC.

Verification:
- Targeted Roslyn probe over `VRPhysicalHandPresenceIkJobs.cs`, `LeviathanTerrainIkJobs.cs`, and `LowerBodyPresenceIkJobs.cs` reports `TARGETED_IK_COMPILE_PROBE_CLEAN`, exit 0.
- Owned IK forbidden-pattern scan returns no hits.
- `git diff --check` reports one CRLF normalization warning for `LeviathanTerrainIkJobs.cs`.
- Full project rebuild was intentionally not run in this loop per current instruction; latest known full-project wall remains outside Animation/IK.

Status:
- Owned source validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | LEVIATHAN BLACKBOX FALLBACK VISIBILITY

What was wrong:
- Loop 27 finite matrix repair could sanitize poisoned segment data before `HasInvalidSegment` ran.
- That made blackbox telemetry look clean after a correction occurred, weakening postmortem evidence.

What was done:
- `WriteMatrices` now returns whether it used any finite vector or rotation fallback.
- The caller ORs that result into `TelemetryFlagInvalid` before writing the 300-frame terrain IK blackbox entry.
- No new buffers, no local `NativeArray`, no hot-path I/O, and no cross-domain dependency were added.

Cinematic cheats used:
- Dear Lie: keep the visual pose finite and stable, but mark the blackbox as invalid when the presentation layer had to correct poison.
- Visual overkill path remains available because Ultra/High bone emission gets stable matrices without hiding corrected fault state.

Exact microseconds saved:
- Prevents downstream renderer/animation poison without adding a second validation pass; avoided cost is estimated at 1-3 us per frame on low-end silicon.
- Added bool accumulation is below 1 us and only rides existing matrix emission work.

Verification:
- Targeted IK Roslyn probe reports `TARGETED_IK_COMPILE_PROBE_CLEAN`.
- Owned forbidden-pattern scan remains clean.
- Full project rebuild intentionally not run per current instruction.
- `git diff --check` reports CRLF normalization warnings only for touched text/source files.

Status:
- Owned source validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.

## 2026-05-17 | GRAB_IK_PROJECTION | HAND QUATERNION OVERFLOW VACCINATION

What was wrong:
- `VRPhysicalHandPresenceJob.SanitizeQuaternion` checked finite quaternion components but not finite squared length.
- Huge finite components could overflow `math.lengthsq` to infinity and normalize through `math.rsqrt(infinity)`, producing a zero rotation instead of using the fallback.

What was done:
- Added a finite squared-length gate before quaternion normalization.
- Kept the existing caller-provided fallback path, ABI, DataVault lanes, and telemetry format unchanged.

Cinematic cheats used:
- Dear Lie: preserve stable hand presentation by rejecting impossible rotations instead of trying to physically recover the pose.

Exact microseconds saved:
- Avoids downstream rotation poison without adding a matrix validation pass; avoided failure cost is unbounded during fault recovery.
- Added cost is one scalar finite check in an inline helper; expected below 1 us for two hands on i3/MX350/Quest-class silicon.

Verification:
- Targeted IK Roslyn probe reports `TARGETED_IK_COMPILE_PROBE_CLEAN`.
- Owned forbidden-pattern scan remains clean.
- Full project rebuild intentionally not run per current instruction.
- `git diff --check` reports CRLF normalization warnings only for touched text/source files.

Status:
- Owned source validation is green for this checkpoint.
- Unity runtime, Quest IL2CPP, Metal, Steam Deck I/O, and profiler/GCMonitor proof remain pending because no Unity Editor/device runtime channel is exposed in this session.
