# Rationale_CORE_ORIGIN_SHIFT

Status: PENDING VERIFICATION

## Intake Decisions

Problem: Visual tearing occurs because origin shifts are atomic in gameplay authority but not atomic across presentation systems.
Solution: Treat AUP shift as a one-frame presentation transaction: pre-shift notice, freeze tick writes, rebase Unity presentation objects, reset interpolation caches, update shader globals before camera render, and flush stale GPU culling state.
Rejected Alternatives: Allowing Unity ParticleSystem, TrailRenderer, Cinemachine, and Rigidbody interpolation to self-correct after transform teleport was rejected because those systems retain previous-frame state and interpolate across the 5000m discontinuity.
Scalability potential: Low uses reset/re-simulate and shader jitter mask only; Middle adds preallocated particle correction buffers; High adds native trail ring buffers; Ultra can keep longer trail history and heavier visual overkill without changing AUP authority.
Hardware Impact: Estimated gain on i3/MX350 is prevention of one-frame full-screen tear with sub-0.1ms steady-state overhead; shift-frame cost remains PENDING VERIFICATION until profiling.

Problem: Current task demands broad VFX/rendering/physics coordination while 20+ agents may edit adjacent systems.
Solution: Use only existing GlobalRegistry/EventBus contracts where present and local registries for presentation components; avoid inventing direct hard dependencies on unrelated domains.
Rejected Alternatives: Concrete compile-time coupling to fauna, scatter, drone, audio, or decal managers was rejected unless an existing interface already exists.
Scalability potential: Low tier can no-op optional receivers; high tier can register richer receivers through the same broadcast.
Hardware Impact: Dense local registries and one-pass rebase avoid scene-wide search on MX350; exact microseconds PENDING VERIFICATION.

Problem: The prompt requires a custom trail renderer and bans Unity TrailRenderer, but deleting all existing TrailRenderer users would create cross-domain blast radius.
Solution: Implement a native AUP-safe trail renderer and migration/audit path first; do not globally remove components without scene/prefab ownership proof.
Rejected Alternatives: Raw YAML mass-edit of prefabs/scenes was rejected because AGENTS.md forbids unsafe prefab/YAML mutation without FileID certainty.
Scalability potential: Low uses shorter ring buffers and fewer draw batches; Ultra uses longer AUP trail history and richer material response.
Hardware Impact: Ring-buffered mesh draw replaces TrailRenderer's hidden state interpolation; expected to remove shift tear while keeping steady-state allocation at 0 B/frame. Measurements pending.

## Loop 1 Decisions - Tasks 1-5

Problem: World-space ParticleSystem particles keep previous-frame world positions while their owner transforms are shifted.
Solution: HectonFloatingOrigin rebases active world-space ParticleSystem particles with preallocated GetParticles/SetParticles scratch and forces a zero-time resimulate to refresh bounds.
Rejected Alternatives: Allocating per-system particle arrays, relying on local-space transform inheritance, or restarting all particle systems was rejected because it either allocates, misses world-space emitters, or destroys active presentation.
Scalability potential: Low uses capped 16K correction scratch and bounds refresh; Middle/High can increase authored particle caps only where visual payoff exists; Ultra can keep dense world-space particles without tearing because the rebase path is chunked.
Hardware Impact: MX350 avoids full-screen streaks from stale particle positions; shift-frame cost is one scene particle scan plus particle writes and remains PENDING VERIFICATION.

Problem: Unity TrailRenderer hides previous vertices and cannot rebase them on AUP shifts.
Solution: Added NativeTrailRenderer with an AUP ring buffer, generated mesh strip, and Graphics.DrawMeshInstanced render path.
Rejected Alternatives: Continuing Unity TrailRenderer or mass-editing scene/prefab YAML was rejected; hidden vertex history cannot be corrected safely and scene mutation needs ownership proof.
Scalability potential: Low uses 16-32 samples and narrow materials; Middle uses 64 samples; High/Ultra can raise to 128-256 with richer material response while retaining the same AUP storage.
Hardware Impact: MX350 steady-state target is one mesh strip rebuild and one instanced draw per active trail; exact microseconds saved versus TrailRenderer tearing are PENDING VERIFICATION.

Problem: The custom player camera rig retained an old interpolation frame across the origin shift.
Solution: OnOriginShift now copies current camera local position and world rotation into the previous-state cache and locks application for the shift frame.
Rejected Alternatives: Waiting until shiftData.Frame + 1 was rejected because the player lane can run later in the same frame as the Core origin shift.
Scalability potential: All tiers use the same zero-cost state reset; higher tiers can layer camera FX after the cut without changing positional authority.
Hardware Impact: Removes the 5000m one-frame interpolation span with no measurable steady-state cost; verification pending.

Problem: Rigidbody interpolation and stale center-of-mass state can preserve pre-shift broadphase state.
Solution: GlobalPhysicsStateManager now resets center of mass and inertia, assigns Rigidbody.position/rotation, then publishes the transform while collisions are disabled and interpolation is suspended.
Rejected Alternatives: MovePosition-only teleport was rejected because it leaves too much state for PhysX interpolation to smear.
Scalability potential: Low tier gets deterministic no-smear physics visuals; high tier can retain CCD overrides for fast bodies without changing the shift contract.
Hardware Impact: One ResetCenterOfMass/ResetInertiaTensor per tracked body on shift frame; MX350 cost is bounded by the existing tracked-body cap and needs profiler proof.

Problem: Deferred crack/rust decal matrices are cached in runtime space and become stale after the transform epoch changes.
Solution: BaseDegradationSystem rebases cached rupture and integrity decal matrix translations atomically during ConstructionManager's origin-shift callback and marks the global decal buffer dirty.
Rejected Alternatives: Rebuilding every decal from module transforms on shift was rejected because it would rescan construction state and duplicate existing cached-authority paths.
Scalability potential: Low shifts cached matrices only; High/Ultra can keep more screen-space decals because the same matrix rebase stays O(active decals).
Hardware Impact: MX350 avoids decal streak/offset errors with O(active cached decals) vector adds; exact timing pending.

Problem: Loop 1 compile failed before validation could prove the new AUP code.
Solution: Classify the failure as a dependency wall because diagnostics point to unrelated files: missing NativeArenaArray<>, missing TetherVerletTelemetryEntry, and AbyssalThermalManager lacking IFixedTickable.FixedTick(float).
Rejected Alternatives: Editing allocator, tether, or thermal systems in this pass was rejected because they are outside CORE_ORIGIN_SHIFT domain and not caused by the visual tearing patch.
Scalability potential: No runtime scalability change; this protects integration from cross-domain churn.
Hardware Impact: None from this decision; build verification remains blocked until dependency owners restore compile.

## Loop 2 Decisions - Tasks 6-10

Problem: Systems later in the dispatcher frame can write old-epoch runtime positions after HectonFloatingOrigin commits a shift.
Solution: SystemDispatcher now accepts an origin-shift frame lock and returns from Update/LateUpdate for exactly the locked frame after the Core lane requests it.
Rejected Alternatives: Multi-frame freeze or global timeScale pause was rejected because it would smear input/audio and create avoidable simulation debt.
Scalability potential: Low/Middle/High/Ultra all use the same one-frame gate; saved complexity buys predictable visuals rather than more simulation.
Hardware Impact: MX350 impact is negative work: later lanes are skipped for one shift frame. Exact saved microseconds are PENDING VERIFICATION.

Problem: Sub-pixel camera-relative jitter can expose tiny cracks even after transform rebasing.
Solution: HectonFloatingOrigin drives `_AupJitterMask` during the shift render frame and Hecton_CoreLit rounds camera-relative world positions to 1 mm when enabled.
Rejected Alternatives: CPU snapping all transforms was rejected because it mutates gameplay authority and costs more than a shader-side presentation mask.
Scalability potential: Low uses the same millimeter mask; High/Ultra can keep richer material deformation because the mask is a cheap presentation layer.
Hardware Impact: One shader branch during the masked frame; MX350 cost is expected sub-0.1 ms and pending measurement.

Problem: `_TotalUniverseOffset` can be stale for render systems that execute before normal frame-side offset publication.
Solution: RenderDispatcher calls HectonFloatingOrigin.PublishCurrentGlobalOffsetsForRenderLoop at beginCameraRendering before registry renderables draw.
Rejected Alternatives: Letting individual renderers publish offsets was rejected because it duplicates authority and risks order-dependent bugs.
Scalability potential: All tiers use one global publication point; high-tier renderers can assume current AUP constants.
Hardware Impact: Two Shader.SetGlobalVector calls and one jitter float write before each camera render; verification pending.

Problem: Long-tail audio/VFX consumers need one frame warning before the epoch changes.
Solution: GlobalSignals now has an unmanaged AupPreShiftSignal lane; HectonFloatingOrigin schedules shift execution for the next frame and publishes the pre-shift packet immediately.
Rejected Alternatives: Broadcasting only the committed AupShiftSignal was rejected because it gives no fade-out window for spatialized tails.
Scalability potential: Low can ignore the signal; High/Ultra audio/VFX can register richer fades without coupling to HectonFloatingOrigin.
Hardware Impact: One prewarmed NativeQueue enqueue per shift; steady-state cost is zero.

Problem: Headless repair-drone render matrices and native state can preserve old runtime positions across shifts.
Solution: DroneFleetManager applies a DroneFleetOriginShiftJob over native drone states, SoA positions, and render matrices, then mirrors managed pending launch positions.
Rejected Alternatives: Rebuilding drone matrices from managed hubs only was rejected because native job state and back buffers would remain stale.
Scalability potential: Low has few active drones; Ultra can shift the fixed fleet capacity using the same IJobParallelFor path.
Hardware Impact: 64-slot native translation job on MX350-class CPU; expected below 0.05 ms, pending verification.

Problem: Loop 2 compile failed before Unity-side verification could run.
Solution: Classify as a second dependency wall because diagnostics point to Survival, Gameplay transport, and Tether types outside CORE_ORIGIN_SHIFT ownership.
Rejected Alternatives: Editing Survival physiology, Manta scooter transport contracts, or Tether telemetry was rejected because those are unrelated domains and would violate the batch boundary.
Scalability potential: No runtime scalability change; preserves integration hygiene while origin-shift work continues.
Hardware Impact: None from this decision; build verification remains blocked until dependency owners restore the missing contracts.

## Loop 3 Decisions - Tasks 11-12

Problem: Scatter Hi-Z occlusion can keep a depth pyramid built in the old runtime epoch and falsely cull shifted vegetation.
Solution: GPUScatterDirector marks the depth pyramid invalid for the shift frame, disables same-frame occlusion, resets scatter frame cadence, and drops foveated visibility history so the next frame rebuilds from the new camera-relative epoch.
Rejected Alternatives: Destroying and reallocating the RenderTexture on every shift was rejected because it creates VRAM churn and can hitch the MX350; leaving the texture allocated but invalidated is the cheaper cache flush.
Scalability potential: Low disables stale occlusion for one frame; Middle/High/Ultra rebuild the same Hi-Z pyramid next frame and can keep denser scatter without smear.
Hardware Impact: MX350 spends only scalar state writes on shift and skips one occlusion dispatch; exact saved microseconds are PENDING VERIFICATION.

Problem: WorldSpatialHashGrid rebuilt absolute positions and called TryUpdateEntry after origin shifts, which can reinsert every resident fish into native hash cells.
Solution: Treat native hash cells as AUP-authoritative and rebase only presentation/runtime metadata caches by the negative shift offset; transient signal runtime positions are also rebased.
Rejected Alternatives: Rebuilding all native entries was rejected because it makes shift cost O(cell occupancy) and contradicts virtual-grid-origin semantics.
Scalability potential: Low/Middle use the same metadata-only rebase; High/Ultra can support large fish counts because native buckets remain stable across presentation shifts.
Hardware Impact: MX350 avoids native multi-hash remove/add churn for 10K fish; estimate stays PENDING until profiler proof.

Problem: Loop 3 build did not reach compiler diagnostics within the tool timeout.
Solution: Stop only the build worker dotnet processes spawned by this pass and classify the attempt as tooling-blocked, not a code verdict.
Rejected Alternatives: Killing the older dotnet process was rejected because it predates this pass and may belong to the editor/toolchain.
Scalability potential: No runtime impact.
Hardware Impact: None; build verification remains pending.

## Loop 4 Decisions - Tasks 13-14

Problem: Zero-GC shift proof can be invalidated by small managed allocations such as trail buffer resizing or particle scratch growth.
Solution: Keep ParticleSystem correction data in a fixed `ParticleSystem.Particle[]`, raise scene root and particle discovery lists to cold capacities, and prevent NativeTrailRenderer capacity changes from reallocating inside `Tick`.
Rejected Alternatives: Allocating arrays per ParticleSystem, resizing trail buffers from Tick, or claiming GC success without a scan was rejected.
Scalability potential: Low uses the fixed scratch and shorter trails; High/Ultra can author more particles/trail samples within capped buffers and must increase capacities deliberately.
Hardware Impact: Intended managed allocation is 0 B on shift; actual GCMonitor proof is still absent.

Problem: Static scan found many class-level runtime/world position caches outside CORE_ORIGIN_SHIFT.
Solution: Append the high-risk owners to `Docs/AgentLogs/RECON_CORE_ORIGIN_SHIFT.md` and leave patches to their domains unless they directly block AUP authority.
Rejected Alternatives: Editing every cached position owner was rejected because it would create cross-domain churn and violate ownership boundaries.
Scalability potential: Recon gives each domain a finite patch list; Low and Ultra both need correctness because origin tearing is not a quality setting.
Hardware Impact: No runtime cost from the report; future targeted fixes should be O(1) cache resets or AUP rebases.

## Loop 5 Decisions - Task 15

Problem: `OriginShiftTranslateJob` was Burst-marked while using `TransformAccess`, which is a Unity transform API surface.
Solution: Remove the Burst attribute from the transform-shift job; keep Burst only on pure native/math AUP validation and drone fleet data jobs.
Rejected Alternatives: Leaving TransformAccess under Burst was rejected because the prompt explicitly forbids Unity API calls inside Burst AUP shift jobs.
Scalability potential: Root-transform shifts are low-count scene operations; high-tier visual overkill is bought by keeping data jobs Burst-safe, not by Burst-compiling Unity Transform writes.
Hardware Impact: MX350 may spend slightly more on root transform presentation shifts, but correctness and Burst compliance outrank this small cost.

Problem: Final compile remains blocked before CORE_ORIGIN_SHIFT diagnostics can fully prove clean integration.
Solution: Run `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false`; classify the failure as dependency-owned because it stops at `HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`.
Rejected Alternatives: Editing Survival physiology was rejected because it is outside CORE_ORIGIN_SHIFT and not caused by this batch.
Scalability potential: No runtime change.
Hardware Impact: None from this decision; build proof remains dependency-blocked.

## OMEGA POLISH CHANGES

Problem: Polish audit found the transform shift job was Burst-attributed while touching Unity `TransformAccess`.
Solution: Removed `[BurstCompile]` from `OriginShiftTranslateJob`; remaining Burst AUP work is pure native/math data (`AupDriftCheckJob`, `DroneFleetOriginShiftJob`, WorldSpatialHashGrid jobs).
Rejected Alternatives: Keeping TransformAccess under Burst was rejected by the explicit no-Unity-API-in-Burst requirement.
Scalability potential: Low/Middle/High/Ultra all keep correctness; the expensive visual path remains shader/GPU-side, not Unity Transform Burst.
Hardware Impact: Root transform count is low. Expected cost increase is below visual-tear cost; exact microseconds remain PENDING VERIFICATION.

Problem: Polish zero-GC purge required managed collection/string/normalization audit.
Solution: Scanned modified implementation files for `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, and `.magnitude`; no new hot-path violation from this pass was found. Existing `CinematicMath.NormalizeQuaternionOrIdentity` reference in DroneCognitionJob predates/serves non-origin-shift rotation normalization and was left untouched.
Rejected Alternatives: Blind replacements in unrelated drone cognition math were rejected because they would change behavior outside the origin-shift path.
Scalability potential: No tier-specific change needed.
Hardware Impact: No runtime change from scan.

Problem: Polish demanded final diff evidence.
Solution: Scoped diff/status evidence:
- Modified tracked files: `Hecton_CoreLit.hlsl`, `BaseDegradationSystem.cs`, `DroneCognitionJob.cs`, `DroneFleetManager.cs`, `ConstructionManager.cs`, `SystemDispatcher.cs`, `HectonPlayerCameraRig.cs`, `GlobalPhysicsStateManager.cs`, `HectonFloatingOrigin.cs`, `GPUScatterDirector.cs`, `WorldSpatialHashGrid.cs`.
- Untracked/new files: `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/VFX/NativeTrailRenderer.cs`, `Docs/Tasks/Status_CORE_ORIGIN_SHIFT.md`, `Docs/AgentLogs/Rationale_CORE_ORIGIN_SHIFT.md`, `Docs/AgentLogs/RECON_CORE_ORIGIN_SHIFT.md`, `Docs/AgentLogs/LOG_CORE_ORIGIN_SHIFT.md`.
- Tracked diff stat: 11 files, 2368 insertions, 153 deletions.
Rejected Alternatives: Pasting a multi-thousand-line diff into the rationale log was rejected because it would bury the decision trail; exact diff remains in git.
Scalability potential: No runtime change.
Hardware Impact: None.

Problem: Polish tag requested `VERIFIED MASTER GRADE`, but AGENTS.md and the agent prompt require `PENDING VERIFICATION` without Unity/profiler/visual proof.
Solution: Keep status as PENDING VERIFICATION and document the conflict; no false verified status will be written.
Rejected Alternatives: Marking verified without Unity Console, GCMonitor, profiler, and visual capture was rejected as a fake report.
Scalability potential: No runtime change.
Hardware Impact: None.

Cinematic Cheats Used:
- One-frame shader `_AupJitterMask` rounds camera-relative vertices to millimeters instead of simulating precision correction.
- Hi-Z occlusion is disabled for the shift frame instead of trying to salvage stale depth history.
- Particle history is rebased in place with capped scratch instead of restarting/re-simulating full effects.
- NativeTrailRenderer stores AUP sample history and rebuilds a simple strip mesh instead of relying on Unity TrailRenderer hidden history.
- Spatial hash keeps AUP buckets authoritative and shifts runtime metadata only instead of physically reinserting every fish.

## Loop 6 Decisions - Honest R&D / AAA Presentation Cache Hardening

Problem: `HectonIndirectVegetationRenderer` cached the cull camera position, previous motion-vector camera position, explicit world-space draw bounds, and far-cull snapshot without an origin-shift listener. A committed 5000m runtime rebase could therefore feed one old-epoch camera/bounds sample into vegetation culling or motion-vector passes.
Solution: Implement `IOriginShiftListener` directly on the renderer. On committed shift, subtract `ShiftOffset` from cached camera positions and explicit bounds, clear the previous-motion-camera flag, invalidate the far-cull snapshot, and reset the culling cadence. This is an O(1) presentation cache repair, not a vegetation data rebuild.
Rejected Alternatives: Releasing and recreating BRG/indirect buffers, rebuilding all instance matrices, or force-refreshing every vegetation source was rejected because the stale state is camera/bounds history, not authoritative vegetation placement. Full rebuild would trade one-frame tearing for avoidable GPU/CPU churn.
Scalability potential: Low uses the same scalar cache reset and pays one refreshed far-cull pass after shift. Middle/High/Ultra can keep longer far-cull cadence and denser impostor vegetation because the shift invalidation prevents old-epoch reuse without permanent extra cost.
Hardware Impact: MX350 estimate is roughly 2 us for field writes during the shift and one necessary far-cull refresh on the next vegetation render. Measured profiler proof is absent, so status remains PENDING VERIFICATION.

Problem: Recon also listed `PickupItem` and other spatial caches, but not all old-position fields are equal.
Solution: Checked `WorldSpatialHashGrid.UpdateGridPosition`; it ignores the provided old/new positions and refreshes from the registered transform, so `PickupItem._lastSpatialPosition` is not currently a spatial-hash epoch-removal hazard. Leave it in recon for domain-owned cleanup rather than widening CORE_ORIGIN_SHIFT scope.
Rejected Alternatives: Adding `IOriginShiftListener` to every item/interaction owner from the recon list was rejected because it creates broad cross-domain churn without proof that those caches feed visual tearing.
Scalability potential: Focused cache repairs keep the listener bucket from filling with low-value receivers; high-volume item/domain listeners should be handled by their owning batches or by an AUP-authoritative spatial record.
Hardware Impact: No runtime impact from the rejected item patch.

Problem: `dotnet build` exposed `HectonIndirectVegetationRenderer` depending on `HardwareTierDetector`, but `HardwareTierDetector.cs` is currently untracked/not present in the generated `Hecton8.Core.csproj`. That makes the vegetation renderer compile depend on another agent's uncommitted platform-policy file.
Solution: Remove the `HardwareTierDetector.AllowComputeCulling` gate from the vegetation renderer and keep the existing local guards: authored `_preferGpuIndirectRendering`, `SystemInfo.supportsComputeShaders`, valid camera/mesh/compute kernels, and fallback BRG path. This keeps the renderer compiling from tracked/generated project inputs instead of waiting on an untracked policy type.
Rejected Alternatives: Adding `HardwareTierDetector.cs` to the generated csproj manually was rejected because Unity owns csproj generation and the file is not CORE_ORIGIN_SHIFT output. Replacing the gate with a new local hardware policy was rejected because that would duplicate the platform domain.
Scalability potential: Low still has `_preferGpuIndirectRendering` and `SystemInfo.supportsComputeShaders` gates plus existing BRG fallback. Middle/High/Ultra retain GPU indirect when authored and supported. Once the platform-policy owner lands `HardwareTierDetector` properly, a domain-owned gate can be restored.
Hardware Impact: No new hot-path work. The change removes a compile-time dependency only; runtime compute eligibility remains gated by existing local flags. MX350 proof remains PENDING VERIFICATION.

Problem: Post-patch `dotnet build` did not reach a clean verdict. One run produced 77 errors dominated by missing shared platform/path/native bridge types; follow-up attempts timed out before diagnostics and left build worker processes.
Solution: Stop only the dotnet processes spawned by this pass and classify Loop 6 as BLOCKED BY TOOLING/DEPENDENCY. Static checks confirm the vegetation renderer no longer references `HardwareTierDetector` and `git diff --check` has no whitespace errors.
Rejected Alternatives: Editing path policy, platform clock, SteamDeck PAL, native bridge, save, input, audio, and combat missing contracts from CORE_ORIGIN_SHIFT was rejected as cross-domain sabotage.
Scalability potential: No runtime change.
Hardware Impact: No runtime change; compile proof remains blocked.
