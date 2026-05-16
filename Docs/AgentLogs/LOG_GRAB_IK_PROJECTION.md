# LOG_GRAB_IK_PROJECTION

## 2026-05-16 | GRAB_IK_PROJECTION | ANIMATION_LEAD

What was wrong:
- VR hand presence had no Animation/IK-owned physical projection kernel. Controller truth could visually pass through cockpit steel.
- No `VRHandManager` singleton or first-party `Physics.SphereCast` hand snapper was found to remove.
- Global compile is currently blocked outside this domain by missing VFX wake, docking/autopilot, light shaft, lockstep, and ecosystem contract symbols.

What was done:
- Added `Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs` and `.meta`.
- Added DataVault buffer IDs for `HandTargetAUP`, `HandActualAUP`, `HandGrabState`, `HandIkTelemetryRing`, and `HandIkTelemetryCursor`.
- Added an Animation/IK vault resolver, fixed two-hand `IJob`, hand AUP structs, grab state, output pose, and 300-frame black-box telemetry.
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
- Hand IK crash dumps serialized the 300-frame circular telemetry buffer by raw index, so a wrapped ring was not oldest-to-newest.
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
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal` succeeds with 0 warnings and 0 errors.
