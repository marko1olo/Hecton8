# LOG_FAUNA_BITE_IK_SOLVER

## 2026-05-16 - Procedural Bite IK Pass

What was wrong:
- Predator bite presentation depended on authored strike intent and could visually clip target hulls.
- No bite-specific DataVault packets existed for jaw targets/current pose/blackbox telemetry.
- `FaunaStateChangedSignal` existed but did not expose a typed Strike lane consumer path for the bite solver.
- Existing Leviathan spine GPU path had no procedural jaw/tentacle mutation between terrain IK and upload.

What was done:
- Added `Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs`.
- Implemented `ProceduralBiteJob` as Burst `IJob` math over `NativeArray` packets.
- Added fixed 128-byte packets: `JawIkTarget`, `CurrentJawPos`, `BiteIkSolveEvent`.
- Added DataVault buffer IDs: `JawIkTargets`, `CurrentJawPos`, `BiteIkSolveEvents`, `BiteIkTelemetryCursor`.
- Integrated job scheduling in `FaunaKinematicsRuntime` after `LeviathanTerrainIkJob` and before `UploadBonesToGpu`.
- Added local predator-space AUP solve to avoid world-origin float precision loss.
- Added low-tier/stress fallback that only rotates/scales the head bone.
- Added high/ultra mandible and tentacle wrap writes into `LeviathanBones`.
- Added `FaunaStateChangedSignalKinds.Strike` consumption and strike publication from `FaunaBrain`.
- Added contact feedback: `DebrisSpawnSignal` sparks, `HapticRequest(ChannelCrush)`, `AcousticPingSignal(ChannelJawSnap)`.
- Added 300-entry blackbox telemetry ring and invalid-pose dump path `Docs/AgentLogs/Dump_FAUNA_BITE_IK_SOLVER.bin`.
- Added miss recovery through deterministic local-space triangle recoil, not a canned animation clip.

Cinematic Cheats used:
- Target hull is approximated by bounded AABB closest-point descent instead of mesh-level collision.
- Submarine wrap is approximated by a cylinder radius from target bounds.
- Low-tier bite is a head scale/rotation fake.
- Snap miss recovery is a triangle-wave recoil.
- Jaw snap is stabilized by 3-frame blend and `FastNlerp` instead of instantaneous accuracy.

Exact microseconds saved:
- 0 us measured by profiler. No profiler capture was available in this CLI session.
- Static budget estimate: low-tier fake avoids the mandible/tentacle path, estimated 80 us saved per active bite solve on i3/MX350.
- Static budget estimate: DataVault fixed buffers avoid managed allocation; GC spike avoided is unmeasured, but hot path allocation count is 0.
- Static budget estimate: AABB/cylinder cheats replace mesh collision, estimated 120 us saved per active bite contact check.
- Static budget estimate: no Unity `Animator.SetIKPosition`/Physics overlap path, estimated 40 us saved per frame during active strike presentation.

Validation:
- `rg` found no `BiteManager.Instance`.
- `rg` found no forbidden `Animator.SetIKPosition` or Unity physics overlap in the bite solver path.
- `git diff --check` on touched files passed except repository line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 due unrelated cross-agent compile failures. Errors include missing `JobAdmissionLane`/contract references, missing visual signal types, voxel debris fields, player motor helpers, and `HectonShaderGlobalDataVaultBridge`.
- No emitted build error targeted `Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs`.
- Final status is `[BLOCKED BY DEPENDENCY]`, not `VERIFIED MASTER GRADE`.
## 2026-05-16 - Loop 6 Multiplatform/H-Phi Inquisition
What was wrong:
- `FaunaKinematicsRuntime` still held persistent private `NativeArray<T>` views for spine/bone/telemetry state after the first DataVault eviction.
- The shared Leviathan IK vault helper still requested animation buffers under `SystemID.AICognition`.
- A stale `CurrentJawPos` contact frame could survive after strike release and re-trigger contact feedback.
- The shared terrain IK pass used `FloatMode.Fast` while the bite solver depends on deterministic bone output.

What was done:
- Replaced persistent native-array fields with `VaultBufferHandle<T>` fields and generation-checked resolves at job scheduling, GPU upload, origin-shift rebase, and black-box dump boundaries.
- Moved Leviathan IK vault helper requests to `SystemID.AnimationFauna`.
- Added inactive-strike target/pose clearing and target-hash/frame gates before sparks, haptics, hull dent, or acoustic jaw snap publish.
- Switched `LeviathanTerrainIkJob` to deterministic Burst mode.
- Reran forbidden-pattern, struct-pack, line-diff, and `dotnet build` checks.

Cinematic Cheats used:
- Toaster mode remains a head-bone orientation/scale lie with no mandible/tentacle solve.
- High/Ultra still use deterministic cylindrical wrap anchors and overkill debris/dent/audio signals when contact is current.

Exact Microseconds saved:
- Private NativeArray lifetime removal: no claimed per-frame saving; it removes stale-view/leak risk.
- Stale feedback gate: estimated 2-6 us avoided on false contact frames by skipping debris/haptic/audio/dent publishes.
- SystemID correction: 0 us runtime; ownership/audit fix.
- Deterministic terrain Burst: no saving claimed; it trades tiny math cost for cross-platform predictability.

Verification:
- `rg` found no `BiteManager.Instance`, `Animator.SetIKPosition`, Unity physics overlap/raycast bite query, `Update()`, `string.Format`, `EventBus`, `H8Memory.Allocate`, or `SystemID.External` in the owned bite/IK slice.
- `rg --pcre2` found no private `NativeArray<T>` fields and no non-Pack struct layouts in the owned bite/IK slice.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` remains blocked by external SubmarineStructuralGrid, Bioluminescence, SpatialAudio, XR, and VaultProbe errors. No emitted error targets `ProceduralBiteIkJobs.cs`, `FaunaKinematicsRuntime.cs`, or `LeviathanTerrainIkJobs.cs`.
