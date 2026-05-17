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

## 2026-05-16 - Loops 10-12 Multiplatform Adjacent IK Polish
What was wrong:
- `FaunaTentacleConstrainedIkChain` and `FaunaTentacleJointPose` had explicit 32-byte layouts but did not declare `Pack = 1`.
- `LeviathanTentacleVerletSolver` allocated and released native tentacle buffers under `SystemID.External`.
- `ProceduralCrabLegIKRuntime` had sequential data, telemetry, and Burst job structs without explicit `Pack = 1`.
- `LeviathanTentacleVerletSolver` and `ProceduralCrabLegIKRuntime` still contain larger private NativeArray/DataVault debt; that remains recorded as unresolved adjacent debt, not claimed fixed.

What was done:
- Re-read AGENTS.md, the domain map, 8 mandate files, and the original `FAUNA_BITE_IK_SOLVER` XML assignment.
- Added `Pack = 1` to the two adjacent tentacle IK explicit payload structs.
- Replaced `SystemID.External` with `SystemID.AnimationFauna` on the Leviathan tentacle H8Memory allocate/release path.
- Added `Pack = 1` to every `StructLayout(LayoutKind.Sequential)` declaration in `ProceduralCrabLegIKRuntime.cs`.
- Updated `Status_FAUNA_BITE_IK_SOLVER.md` and `Rationale_FAUNA_BITE_IK_SOLVER.md` with Loops 10-12 and Decisions 22-24.

Cinematic Cheats used:
- No new simulation was added. These loops harden ABI and memory ownership around the existing cheap IK/math-lie systems.
- Existing toaster mode and high/ultra overkill paths remain unchanged.

Exact Microseconds saved:
- 0 us measured by profiler. No profiler capture was available in this CLI session.
- ABI pack changes: 0 us claimed; the gain is platform layout determinism.
- `SystemID.AnimationFauna` owner correction: 0 us claimed; the gain is leak/pressure attribution.

Verification:
- `rg --pcre2` found no `StructLayout` entry missing `Pack = 1` in `Assets/_Project/Scripts/Animation/Fauna`, `FaunaTentacleConstrainedIk.cs`, or `ProceduralCrabLegIKRuntime.cs`.
- Forbidden-pattern scan over the touched bite/fauna IK files found no `BiteManager.Instance`, `Animator.SetIKPosition`, Unity physics cast/overlap, `PhysicsEventBus`, generic `EventBus`, `string.Format`, or standard `Update/LateUpdate/FixedUpdate`.
- `git diff --check` passed with line-ending warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 with 53 external errors in world ecosystem, player tool, bootstrap, fluid feedback, lockstep, global signals, and tether files. No emitted error targets `ProceduralBiteIkJobs.cs`, `FaunaTentacleConstrainedIk.cs`, `LeviathanTentacleVerletSolver.cs`, or `ProceduralCrabLegIKRuntime.cs`.

## 2026-05-16 - Loop 13 Leviathan Tentacle DataVault Eviction
What was wrong:
- `LeviathanTentacleVerletSolver` still owned private persistent `NativeArray<T>` state for tentacle Verlet positions, previous positions, radii, segment matrices, scratch corrections, root/target caches, state bits, and 300-frame telemetry.
- The earlier owner-ID correction fixed sentinel attribution only; it did not satisfy DataVault sovereignty.

What was done:
- Added dedicated `LeviathanTentacle*` `BufferID` values in `H8Memory`.
- Replaced private persistent native arrays with `VaultBufferHandle<T>` fields.
- Added a narrow vault-resolution view used only at seeding, job scheduling, damage contact, graphics upload, origin-shift rebase, telemetry write, and dump boundaries.
- Removed local `H8Memory.Allocate/Release` and `NativeMemorySentinel` registration/release paths from the tentacle solver.
- Kept 300-frame black-box telemetry capacity intact and still dumping to `Docs/AgentLogs/Dump_LEVIATHAN_TENTACLE_IK.bin`.

Cinematic Cheats used:
- No new expensive physics was added. The tentacle solver still uses the cheap Verlet/Jacobi visual lie with triangle-wave organic motion and AUP-only high-tier contact direction.
- Low-tier remains fixed to minimal iterations; High/Ultra keep richer matrix/radius upload and flow-reactive visual overkill without extra private memory.

Exact Microseconds saved:
- 0 us measured by profiler. No Unity profiler or GCMonitor capture was available in this CLI session.
- DataVault eviction: no frame-time saving claimed. Static impact is lower leak and stale-view risk, not measured CPU speed.

Verification:
- Re-read status/rationale, full `FAUNA_BITE_IK_SOLVER` XML, AGENTS.md, domain map, Unity MCP skill notes, and 8 mandate files before editing.
- `rg` found no private `NativeArray`, `new NativeArray`, `H8Memory.Allocate/Release`, `NativeMemorySentinel.Register/Unregister`, or `SystemID.External` in `LeviathanTentacleVerletSolver.cs`.
- `rg --pcre2` found no `StructLayout` entry missing `Pack = 1` in the audited owned/adjacent fauna IK files.
- Forbidden-pattern scan found no Unity physics query, legacy EventBus, `BiteManager.Instance`, `Animator.SetIKPosition`, `string.Format`, or standard `Update/LateUpdate/FixedUpdate` in the audited IK set.
- Targeted `git diff --check --` on touched files exits 0 with line-ending warnings only. Repository-wide `git diff --check` is blocked by unrelated trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md:2312`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 with 38 external errors in `TetherInstance.cs` and `PhysicsApplySystem.cs`. No emitted error targets `LeviathanTentacleVerletSolver.cs`, `H8Memory.cs`, or owned bite IK files.

## 2026-05-16 - Loop 14 Procedural Crab DataVault Eviction
What was wrong:
- `ProceduralCrabLegIKRuntime` still owned private persistent `NativeArray<T>` buffers for entity state, foot positions, target feet, step state, raycast commands, raycast hits, low-tier raycast masks, body pose upload data, solved joint matrices, and 300-frame telemetry.
- The ABI pack sweep fixed struct layout only; it did not remove the private native lifetime from the adjacent fauna IK runtime.

What was done:
- Re-read status/rationale and extracted the full `FAUNA_BITE_IK_SOLVER` XML assignment before editing.
- Added dedicated `ProceduralCrab*` `BufferID` values in `H8Memory`.
- Replaced private persistent native arrays with `VaultBufferHandle<T>` fields.
- Added a narrow vault-resolution view used for entity registration, pose mutation, Burst scheduling, origin-shift rebase, indirect GPU upload, telemetry write, and dump boundaries.
- Removed local `new NativeArray<T>` allocation and `NativeMemorySentinel.Register/Unregister` paths from the crab IK runtime.
- Preserved the existing 300-entry black-box telemetry ring and dump path `Docs/AgentLogs/Dump_ANIM_PROCEDURAL_BEHAVIOR.bin`.

Cinematic Cheats used:
- No new physics simulation was added. The crab solver still uses scheduled ground probes plus analytical two-bone visual IK.
- Low/MX350 still probes only two legs per frame; High/Ultra keep all-leg probes, body tilt, and full joint matrix upload with vault-owned data.

Exact Microseconds saved:
- 0 us measured by profiler. No Unity profiler or GCMonitor capture was available in this CLI session.
- DataVault eviction: no frame-time saving claimed. Static impact is lower leak/stale-view risk and cleaner memory ownership on Quest/Android and Steam Deck.

Verification:
- `rg` found no private `NativeArray`, `new NativeArray`, `H8Memory.Allocate/Release`, `NativeMemorySentinel.Register/Unregister`, `SystemID.External`, or private native owner constants in `ProceduralCrabLegIKRuntime.cs`.
- `rg --pcre2` found no `StructLayout` entry missing `Pack = 1` in the audited owned/adjacent fauna IK files.
- Forbidden-pattern scan found no Unity physics cast/overlap, legacy EventBus, `BiteManager.Instance`, `Animator.SetIKPosition`, `string.Format`, or standard `Update/LateUpdate/FixedUpdate` in the audited IK set.
- Targeted `git diff --check --` on touched files exits 0 with line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /clp:ErrorsOnly /m:1` exits 0 with 0 warnings and 0 errors. Two earlier parallel build attempts timed out without diagnostics, then the serialized retry completed cleanly.

## 2026-05-17 - Loops 15-16 ABI Pack And Dead Memory Purge
What was wrong:
- `FaunaTier1LodProxyEntry` still used `Pack = 4` while the current ARM64/Quest audit requires `Pack = 1` on native/Burst-adjacent fauna payloads.
- `FaunaBrain.Compatibility.cs` contained unused `PredatorMemory` dead code with a private persistent `NativeArray<float4>`, local `new NativeArray`, and sentinel register/unregister path.

What was done:
- Re-read `Status_FAUNA_BITE_IK_SOLVER.md`, `Rationale_FAUNA_BITE_IK_SOLVER.md`, and the full original `FAUNA_BITE_IK_SOLVER` XML assignment from `CURRENT_BATCH.md`.
- Changed `FaunaTier1LodProxyEntry` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)`.
- Verified `PredatorMemory` had no in-repo references, then deleted the unused struct instead of migrating dead code to the DataVault.
- Preserved `using System` in `FaunaBrain.Compatibility.cs` for the existing `[Flags]` usage after the first rebuild exposed that dependency.

Cinematic Cheats used:
- No new simulation was added. This loop only hardens low-tier proxy ABI and deletes dead memory ownership.
- Existing bite/toaster/high-tier IK lies remain unchanged.

Exact Microseconds saved:
- 0 us measured by profiler. No Unity profiler or GCMonitor capture was available in this CLI session.
- ABI pack correction: 0 us claimed; the gain is layout determinism.
- Dead `PredatorMemory` deletion: no frame-time saving claimed because the type was unused; static impact is lower memory-governance and leak-risk surface.

Verification:
- `rg` found no `PredatorMemory`, private `NativeArray`, local `new NativeArray`, `NativeMemorySentinel`, `H8Memory.Allocate/Release`, `SystemID.External`, `EventBus`, `string.Format`, standard `Update/LateUpdate/FixedUpdate`, `BiteManager.Instance`, or `Animator.SetIKPosition` in `FaunaBrain.Compatibility.cs`.
- `rg --pcre2` found no `StructLayout` entry missing `Pack = 1` in the audited bite/adjacent IK/proxy files.
- Targeted `git diff --check --` on touched files exits 0 with line-ending warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /clp:ErrorsOnly /m:1` exits 1 with one external error in `Assets/_Project/Scripts/AcousticZoneController.cs(3175,17)` missing `Type`. No emitted error targets `FaunaBrain.Compatibility.cs`, `FaunaTier1LodProxyRegistry.cs`, or the owned bite IK files.

## 2026-05-17 - Loop 17 Build Green Revalidation And Shader Audit
What was wrong:
- Previous compile-wall records were stale under concurrent repo edits. `AcousticZoneController` and `HectonSurvivalSystem` had already changed by the time their build errors were inspected.
- The Leviathan-owned shader surface had not been rechecked in this loop for Metal/Mac hazards.

What was done:
- Re-read `Status_FAUNA_BITE_IK_SOLVER.md`, `Rationale_FAUNA_BITE_IK_SOLVER.md`, the Unity MCP workflow notes, and the full original `FAUNA_BITE_IK_SOLVER` XML assignment.
- Reran a serialized build with explicit exit capture after inspecting the live external files.
- Re-ran forbidden-pattern, native ownership, ABI pack, shader, and diff-hygiene scans over the owned/adjacent bite IK set.
- Did not overwrite the concurrent external fixes; no runtime code was changed in this loop.

Cinematic Cheats used:
- No new physical simulation was added. Existing bite/mandible/tentacle math lies remain intact.
- Shader audit found no need to add or remove high-tier visual work in the Leviathan-owned shader pair.

Exact Microseconds saved:
- 0 us measured by profiler. No Unity profiler or GCMonitor capture was available in this CLI session.
- Build revalidation and shader audit have no runtime effect.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /m:1` exits 0 with 0 warnings and 0 errors.
- `rg` found no local native allocation, private `NativeArray`, `NativeMemorySentinel`, `H8Memory.Allocate/Release`, `SystemID.External`, Unity physics query, legacy EventBus, `string.Format`, standard `Update/LateUpdate/FixedUpdate`, `BiteManager.Instance`, or `Animator.SetIKPosition` in the audited bite/adjacent IK/proxy set.
- `rg --pcre2` found no `StructLayout` entry missing `Pack = 1` in the audited bite/adjacent IK/proxy set.
- Leviathan shader scan found no compute kernels, `numthreads`, RW buffers/textures, D3D-only macros, derivative intrinsics, `tex2Dlod`, or `only_renderers` restrictions in `Hecton_LeviathanTentacleIndirect.shader` and `Hecton_LeviathanOrganic.shader`.
- Targeted `git diff --check --` on touched fauna/docs files exits 0 with line-ending warnings only.
