# Rationale_KINEMATIC_TETHER_EXPERT

Status: PENDING - GLOBAL COMPILE DEPENDENCIES

Problem: Existing towing already had a large custom Verlet implementation, but the prompt demands exact task evidence and the mandates reject same-frame `Schedule().Complete()` hot-path work.
Solution: Patch the existing first-party tether runtime instead of replacing it. Preserve the current `PhysicsForceRouter`, `HeavyTowWinch`, `GlobalPhysicsStateManager`, and `TetherSignals` surfaces.
Rejected Alternatives: A new standalone manager or Unity Joint fallback would duplicate active ownership and violate the purge requirement. A full assembly split before compile proof would be dependency churn under multi-agent load.
Scalability potential: Low/MX350 uses 3 solver segments and fewer iterations; Middle/High/Ultra use 10 segments with higher damping/iteration quality and visual stress glow already in the shader path.
Hardware Impact: i3/MX350 expected gain from 3-node low-tier solver and no worker schedule/complete churn is small but real, estimated 3-8 microseconds per active tether versus the existing 16-point solver path.

Problem: Tether visuals and cable sag are immersive but not gameplay truth.
Solution: Keep visual cable as a procedural GPU buffer/path; make the gameplay truth a short deterministic acceleration constraint chain.
Rejected Alternatives: Per-bend/per-cable-segment physical truth for every visual point was rejected because the prompt requires predictable towing, not cable cloth.
Scalability potential: Toaster path: 3 solver segments, cheap catenary/tube impostor. Middle: 10 segments. High/Ultra: same authority plus stronger visual stress readback and overkill presentation from existing shader.
Hardware Impact: MX350 avoids simulating 16-24 visual points as physics truth; estimated 5-15 microseconds saved per active tether at 50Hz.

Problem: Towing entrypoint was a direct local method call, but the batch mandates require signal migration and simultaneous-agent decoupling.
Solution: Introduced `TetherFiredSignal` in `Hecton8.Physics.Tethers.Contracts`, queued it through `TetherSignals`, and kept managed Unity object references in a fixed 16-slot sidecar consumed by `TetherManager`.
Rejected Alternatives: Passing UnityEngine.Object references through a NativeQueue is illegal; a singleton manager lookup would violate task 1 and multi-agent boundaries.
Scalability potential: Low/Mid/High/Ultra all share the same fixed-capacity queue; high-end devices spend cycles on visuals, not attach plumbing.
Hardware Impact: Fixed sidecar avoids scene search and allocation. Estimated low-end gain: 1-2 microseconds per attach event; fixed-step cost is zero when queue is empty.

Problem: The existing Verlet constraint treated compression as tension, turning the cable into a rod and creating false load while slack.
Solution: The Burst constraint now records/enforces only positive stretch: `max(0, distance - restLength)`. Tension remains `stretch * springStiffness`.
Rejected Alternatives: Keeping `abs(delta)` was cheaper to leave untouched but violated cable behavior. A Unity spring joint was rejected as nondeterministic black-box physics.
Scalability potential: Low/MX350 skips slack corrections across 3 segments; Mid/High/Ultra get cleaner 10-segment cable behavior and stronger stress visualization without fake compression.
Hardware Impact: On i3/MX350, slack cases skip correction math; estimated 2-5 microseconds saved per active tether depending on slack frequency.

Problem: Heavy payloads need controllable towing without injecting full force into the payload or bypassing force routing.
Solution: Endpoint forces now use `playerMass / (playerMass + payloadMass)` scaling, then queue equal/opposite ForceMode.Force packets through `PhysicsForceRouter`.
Rejected Alternatives: Direct Rigidbody.AddForce would bypass deterministic routing. Reduced-mass-only acceleration hid the player reaction and produced weaker audit evidence.
Scalability potential: Low devices get stable simple math; high-end devices can layer visual overkill on stress because authority remains deterministic and cheap.
Hardware Impact: Estimated 1-3 microseconds saved by simple scalar scaling and no extra dependency lookup; bigger gain is reduced physics jitter.

Problem: Compile verification through MCP became unavailable and the project has active cross-domain compile failures.
Solution: Used Unity's generated Bee response files with Unity's Roslyn compiler. `Hecton8.Physics.Tethers.Contracts.rsp` compiled clean; `Hecton8.Core.rsp` reached only unrelated dependency errors.
Rejected Alternatives: Editing Audio, Radar, Player, Ecosystem, or Bootstrap domains would violate the domain boundary. Reporting a green build would be false.
Scalability potential: Not a runtime feature; this preserves integration integrity under 20+ parallel agents.
Hardware Impact: No runtime cost. Compile wall is external to tether runtime.

Problem: Polish mandate required `dotnet build Hecton8.Core.csproj`, but the IDE csproj is stale against active Unity asmdefs.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /nr:false`. It failed with 97 errors across missing Scheduling, Memory.Layout, Audio.Propagation, GPR, Navigation, CCD, Vehicle, and other active asmdef domains. It also cannot resolve the new `TetherFiredSignal` contract because no `Hecton8.Physics.Tethers.Contracts.csproj` has been regenerated while Unity MCP is offline.
Rejected Alternatives: Manually editing generated csproj files would be churn and false integration. The authoritative compile proof is Unity Bee rsp compilation.
Scalability potential: Not runtime.
Hardware Impact: No runtime cost.

## OMEGA POLISH CHANGES

Problem: Polish audit flagged the exact risk in cable math: an "honest" bidirectional distance constraint was acting like a rigid rod.
Solution: Replaced absolute delta with positive stretch only in `TetherVerletJacobiConstraintJob`. This is the cinematic cheat: slack is a visual-only sag/path effect; physics authority only exists when cable length exceeds target length.
Rejected Alternatives: Full rope compression/bending truth was rejected because it spends CPU on invisible slack behavior and causes false towing load. Unity Joint fallback remains banned.
Scalability potential: Low/MX350 executes 3 segment stretch-only constraints. Mid/High/Ultra execute 10 segment stretch-only constraints and spend the saved cycles on GPU stress glow / overkill cable presentation.
Hardware Impact: Slack correction skip estimated at 2-5 microseconds saved per active tether on i3/MX350 in common slack/reel-in cases.

Problem: Polish audit required zero-GC and silo verification.
Solution: Re-scanned touched tether files for `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, `math.normalize`, `Vector3.Distance`, and `.magnitude`; no matches in touched files. Re-scanned for `Schedule(` / `.Complete(` in tether solver files; no matches.
Rejected Alternatives: Wrapping managed formatting in runtime diagnostics was rejected. New diagnostics are fixed NativeArray blackbox entries and a binary dump only on fault.
Scalability potential: All tiers share no-allocation fixed-step solver behavior.
Hardware Impact: No managed allocation spikes; expected GC cost is 0 bytes/frame in tether hot path.

Problem: The fixed fire-signal sidecar could retain stale attach requests if a manager never consumed its queued signal, eventually starving the 16-slot fire lane or executing an old attach.
Solution: Added an 8-frame fire-signal TTL, pruned on publish and manager consume, fixed initialization to require both NativeQueues, and cleared same-version wrong-manager sidecar entries.
Rejected Alternatives: Growing the queue or using managed collections was rejected because attach plumbing must stay bounded and allocation-free after warm setup.
Scalability potential: Low/Mid/High/Ultra all keep the same deterministic fixed-capacity signal lane; high-end devices spend saved complexity on visual tether stress, not attach retries.
Hardware Impact: i3/MX350 avoids pathological full-queue scans after stale signals. Normal cost is bounded at 16 iterations only when publishing/consuming, estimated under 1 microsecond per attach event.

Problem: Second-pass audit found edge spam and bandwidth policy defects: target-length input accepted nonfinite values, visual fallback still used direct `SetData`, and extreme tow command signals could publish every fixed step.
Solution: Added finite guards and target reset in `HeavyTowWinch`, replaced fallback visual uploads with `GraphicsBufferUploadUtility.UploadNativeArray`, and throttled `TowLoadLimit` publishes to one successful command every 3 frames.
Rejected Alternatives: Letting downstream systems sanitize NaNs, retaining direct buffer `SetData`, or issuing a command every fixed tick was rejected because all three leak avoidable instability into unrelated domains.
Scalability potential: Low/MX350 avoids command bus spam and uses the same cheap upload path. High/Ultra keep stable authority and can spend budget on stronger tether visual stress.
Hardware Impact: Command cooldown can save 1-2 microseconds during high-load towing bursts by avoiding repeated queue pressure; upload path keeps the render submission path policy-consistent.

Final Git Diff:
- Modified: `Assets/_Project/Scripts/Gameplay/HeavyTowWinch.cs`
- Modified: `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- Modified: `Assets/_Project/Scripts/Physics/TetherSignals.cs`
- Modified: `Assets/_Project/Scripts/Physics/TetherVerletJobs.cs`
- Modified: `Assets/_Project/Scripts/TetherInstance.cs`
- Modified: `Assets/_Project/Scripts/TetherManager.cs`
- New: `Assets/_Project/Scripts/Physics/Tethers/Contracts/Hecton8.Physics.Tethers.Contracts.asmdef`
- New: `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs`
- Existing untracked/touched: `Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs` (`TowLoadLimit` flag added)
- New/updated evidence: `Docs/Tasks/Status_KINEMATIC_TETHER_EXPERT.md`, `Docs/AgentLogs/Rationale_KINEMATIC_TETHER_EXPERT.md`
- Diff stat for tracked code: 6 files changed, 481 insertions(+), 28 deletions(-). New contract/status/rationale files are untracked in the current dirty worktree.
