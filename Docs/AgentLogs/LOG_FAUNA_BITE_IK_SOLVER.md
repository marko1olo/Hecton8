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
- Removed unused `Action<AIState> OnStateChanged` from `FaunaBrain`; in-repo search found no subscribers.
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
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` remains blocked by external core memory debt: duplicate `GlobalDataVault.ValidateAbiLayout`. No emitted error targets `ProceduralBiteIkJobs.cs`, `FaunaKinematicsRuntime.cs`, or `LeviathanTerrainIkJobs.cs`.
- Two unrelated `PhysicsEventBus` calls remain in `FaunaBrain` EMP/mimicry behavior. They were not replaced because the listener contract belongs to physics/audio ownership, not bite IK; this is recorded debt, not claimed clean.

## 2026-05-16 - Loop 7 Oriented Hull/DataVault Pass
What was wrong:
- The previous hull-contact fake was predator-axis aligned, so angled submarine hulls could still produce visible jaw/tentacle drift.
- Bite feedback resolved more vault buffers than necessary for a pose-only contact check.
- The touched `FaunaBrain` surface still had private persistent corpse-sink NativeArrays.

What was done:
- Re-read `Status_FAUNA_BITE_IK_SOLVER.md`, `Rationale_FAUNA_BITE_IK_SOLVER.md`, and the full original XML assignment.
- Reworked closest-point solve to use target right/up/forward axes for an oriented box approximation.
- Reworked high/ultra tentacle anchors to wrap around the target-forward cylindrical hull axis.
- Split bite vault resolution into full solve, current-pose-only feedback, and telemetry-only dump helpers.
- Moved corpse-sink input/output scratch from private NativeArrays to DataVault handles owned by `SystemID.AnimationFauna`.
- Reran forbidden-pattern, struct-pack, diff-check, SignalBus/EventBus, assignment extraction, and build checks.

Cinematic Cheats used:
- Oriented OBB closest-point descent replaces mesh collision.
- Target-forward cylinder wrap replaces submarine hull mesh queries.
- Low-tier still uses the head-bone scale/rotation lie.
- High/Ultra use saved collision budget for mandible/tentacle contact staging, sparks, crush haptics, hull dent, and jaw-snap acoustic signals.

Exact Microseconds saved:
- 0 us measured by profiler. No profiler capture was available in this CLI session.
- Pose-only feedback vault split: static estimate 3-10 us avoided on contact feedback frames on i3/MX350.
- OBB/cylinder cheat versus mesh collision: static estimate remains about 120 us avoided per active bite contact check.
- Corpse-sink DataVault eviction: no claimed frame-time saving; it removes private native lifetime risk.

Verification:
- `rg` found no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, private `NativeArray<T>`, `new NativeArray<T>`, managed `Action`, `OnStateChanged`, `BiteManager.Instance`, `Animator.SetIKPosition`, or Unity physics casts in the owned bite/fauna IK scan set.
- `rg --pcre2` found no non-`Pack = 1` struct layouts in the audited bite/fauna IK files.
- `git diff --check` passed on touched files with line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 with 243 external errors. Current blockers include `HectonUnderwaterVisuals`, `SargassumMicroFaunaBoids`, `RepairTool`, and `ToolDurabilitySystem`. Captured output shows no owned bite/IK compile error.

## 2026-05-16 - Loop 8 Basis Degeneracy Polish
What was wrong:
- Target right/up/forward vectors were normalized but not fully re-orthogonalized after malformed or nearly parallel authored input.
- That could distort the oriented box closest point or high-tier cylinder wrap on bad content.

What was done:
- Added a Burst-safe `OrthonormalizeTargetBasis` path.
- Added a finite perpendicular fallback for degenerate target axes.
- Re-ran forbidden-pattern, struct-pack, diff-check, and build checks.

Cinematic Cheats used:
- The hull is still an oriented box/cylinder fake, now with a stable basis even when target metadata is poor.
- Low-tier still bypasses the detailed basis-dependent solve.

Exact Microseconds saved:
- 0 us measured by profiler. No profiler capture was available in this CLI session.
- Basis guard adds a few scalar ops on non-low-tier solves; no saving is claimed.

Verification:
- `rg` found no forbidden `Update`, `string.Format`, local native allocation, Animator IK, physics cast, `H8Memory.Allocate`, or `SystemID.External` pattern in the audited bite/fauna IK files.
- `rg --pcre2` found no non-`Pack = 1` struct layouts in the audited bite/fauna IK files.
- `git diff --check` passed on touched files with line-ending warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 with one external error: `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18)` cannot resolve `Hecton8.AI.Ecosystem`. No owned bite/IK compile error is emitted.

## 2026-05-16 - Loop 9 Attack Path Purge And Build Green
What was wrong:
- The adjacent predator lunge attack presentation still used `Physics.CapsuleCastNonAlloc`.
- The touched fauna path still emitted EMP and mimic acoustic payloads through legacy `PhysicsEventBus`.
- Previous final validation was stale because the repository did not build at that point.

What was done:
- Replaced the lunge capsule cast and RaycastHit scratch scan with deterministic swept-sphere versus captured target OBB math.
- Captured lunge target center/extents/basis/hash/material at telegraph time for zero-query contact resolution.
- Routed mimic acoustic ping through existing `AcousticPingSignal`.
- Routed EMP attack through existing typed `CombatDamageSignal` with `DamageTypeMask.Emp`.
- Added finite vector/bounds guards and near-zero reciprocal guards for the new lunge OBB sweep.
- Re-ran forbidden-pattern, struct-pack, diff-check, and build checks.

Cinematic Cheats used:
- Lunge contact is a swept-sphere against target OBB, not a real physics scene query.
- Target hull material is sampled once from the captured collider/provider and reused as a deterministic contact payload.
- Low-tier and high-tier share the same cheap contact lie; high-tier spends the saved query cost on IK wrap, sparks, haptics, dents, and jaw-snap audio.

Exact Microseconds saved:
- 0 us measured by profiler. No profiler capture was available in this CLI session.
- Static estimate: removing `Physics.CapsuleCastNonAlloc` avoids roughly 40-120 us on active lunge frames on i3/MX350, depending on scene collider density.
- Static estimate: replacing legacy EventBus emits with typed lanes avoids listener fanout on those emission frames; no measured value claimed.

Verification:
- `rg` found no Unity physics casts, `PhysicsEventBus`, generic `EventBus`, managed delegates, local NativeArray allocations, `string.Format`, standard `Update`, `BiteManager.Instance`, Animator IK, `H8Memory.Allocate/Release`, or `SystemID.External` in the audited bite/fauna files.
- `rg --pcre2` found no non-`Pack = 1` struct layouts in the audited bite/fauna IK files.
- `git diff --check` passed on touched files with line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 0 with 0 warnings and 0 errors.
