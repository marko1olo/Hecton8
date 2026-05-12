# Rationale_PHYSICS_KINEMATICS

Agent: KINEMATICS_OFFICER
Prompt ID: PHYSICS_KINEMATICS
Status: PENDING VERIFICATION

## Intake Decision

Problem: The batch prompt path specified by protocol used .md, but the workspace contains CURRENT_BATCH.txt.
Solution: Extracted the exact PHYSICS_KINEMATICS XML block with a CLI regex from CURRENT_BATCH.txt.
Rejected Alternatives: Reading adjacent prompts, trusting IDE context, or scanning by chat_name would risk cross-agent contamination.
Scalability potential: Low uses strict prompt isolation to avoid wasted edits; Middle/High/Ultra keep the same deterministic extraction because agent count does not change the parsing cost.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is indirect: avoids wrong-domain code churn and compile loops, saving developer time rather than frame time.

## Initial Architecture Decision

Problem: Synchronous player probe casts violate the 0 synchronous casts mandate and can stall the main thread during kinematic movement.
Solution: Convert ground and ladder probes to batched CapsulecastCommand flow, consume previous-frame hits, and gate stale hits by floating-origin sequence.
Rejected Alternatives: Keeping SphereCastNonAlloc as a "safe" fallback, calling Complete in the same movement tick, or adding direct dependencies on unfinished systems.
Scalability potential: Low uses minimal probe batch count and previous-frame speculation; Middle expands probe richness; High adds better contact classification; Ultra spends saved main-thread time on smoother visual interpolation and richer movement feedback.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is removal of synchronous physics-query stalls, target 40-120 us in player movement spikes depending on scene collider density.

## Verification-Only Runtime Decision

Problem: The target kinematics files already contain the async CapsulecastCommand batch, stale-hit discard, speculative hover, ladder snap, and supporting math changes. Editing runtime code without a failing evidence point would create churn during a 20+ agent batch.
Solution: Treat this pass as a hard audit: grep forbidden sync cast/Slerp/Simplex/Debug.DrawRay symbols, inspect target functions, verify .meta files, and run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`.
Rejected Alternatives: Rewriting the movement controller for optics, adding a redundant probe abstraction, or changing non-domain systems with project-wide sync casts.
Scalability potential: Low keeps the four-command probe batch and cached hits; Middle/High can add richer classification without blocking the main thread; Ultra can spend saved CPU on camera and water-contact presentation.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is preservation of the existing 40-120 us sync-cast stall removal with no new regression risk.

## AUP Stale-Hit Decision

Problem: CapsulecastCommand results can complete after a floating-origin shift, making local-space hits stale and unsafe for grounding or ladder snap.
Solution: Keep shift sequence/body epoch checks in the late-swap completion window and discard results when the sequence changes; use one tick of tide-scaled speculative hover for visual continuity.
Rejected Alternatives: Transforming old RaycastHit positions across the shift, forcing synchronous recasts during shift, or accepting one wrong correction frame.
Scalability potential: Low has a single hover tick and no recast; Middle/High can add richer black-box telemetry; Ultra can use the tide-linked hover to hide AUP correction with better camera presentation.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is prevention of correction loops and sync recasts, target 20-60 us avoided during shift frames.

## Cinematic Math Decision

Problem: Multiple movement details can spend CPU on exact physical realism where the player only needs coherent visual feedback.
Solution: Keep triangle-wave jet/tide math, dominant probe lanes, scalar VR roll nlerp, directional drag dot products, and squared-distance gates.
Rejected Alternatives: Simplex noise, quaternion roll smoothing, normalized probe vectors for every command, sqrt distance gates, or full continuous dynamic collision.
Scalability potential: Low uses the cheapest triangle/cardinal/squared paths; Middle adds normal presentation smoothing; High adds additional contact quality; Ultra spends saved cycles on visual overkill instead of simulation precision.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is 15-45 us across hot movement and presentation checks, with larger wins in collision-dense scenes.

## Batch File Path Reconciliation

Problem: The requested batch path was CURRENT_BATCH.md, intake initially found CURRENT_BATCH.txt, and later the workspace presented CURRENT_BATCH.md while the .txt path disappeared.
Solution: Re-extracted PHYSICS_KINEMATICS from CURRENT_BATCH.md after closing the 20-task checklist and confirmed the same task block before final reporting.
Rejected Alternatives: Trusting the stale .txt extraction after the file changed, or scanning neighboring prompts for context.
Scalability potential: Low/Middle/High/Ultra all benefit from deterministic prompt re-read because agent concurrency changes files faster than chat memory can be trusted.
Hardware Impact: No frame-time impact; prevents wrong-domain edits and compile churn during multi-agent execution.

## Polish Mandate Absence

Problem: Omega protocol requires reading POLISH_MANDATE only after all tasks are checked, but the current batch file contains no `<POLISH_MANDATE>` tag.
Solution: Performed the post-task lookup against CURRENT_BATCH.md, recorded absence, and executed the anti-bloat inquisition using direct forbidden-symbol scans and target function audits.
Rejected Alternatives: Inventing a polish mandate, reading another agent's prompt, or delaying final report without an actionable tag.
Scalability potential: Low keeps the no-op polish path deterministic; Middle/High/Ultra can add deeper telemetry later without modifying verified movement code.
Hardware Impact: No runtime change; protects the existing 40-120 us sync-cast removal by avoiding unnecessary code churn.

## Player Footstep Audio Sync Query Removal

Problem: The wider Echelon 4 scan found PlayerFootstepAudio performing a synchronous downward Physics.RaycastNonAlloc on footstep events, duplicating ground data already owned by the KCC batch.
Solution: Added HectonPlayerMovement.TryGetRecentFootstepSurfaceHit and routed PlayerFootstepAudio surface detection through the previous-frame batched footstep/ground cache with layer, distance, finite-value, and support-normal validation.
Rejected Alternatives: Scheduling a new RaycastCommand from audio, keeping the NonAlloc raycast because it is event-driven, or wiring audio directly to HectonPlayerMotor internals.
Scalability potential: Low uses existing cached KCC hit and falls back to default clips on cache miss; Middle can add surface material IDs to the existing hit cache; High can push a compact surface-audio signal; Ultra can spend the saved query time on richer procedural footstep layers.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is 15-45 us on footstep events and removal of one main-thread physics query from player audio.

## Player Interaction Async Look Query

Problem: PlayerInteraction performed a throttled Physics.RaycastNonAlloc in the player tick for hover acquisition, leaving an Echelon 4 player-domain sync cast after the KCC and footstep audio paths were clean.
Solution: Replaced the direct raycast with SystemDispatcher.QueueDispatcherRaycast and IDispatcherRaycastReceiver consumption. The result is validated against InteractableRegistry, written back into QueryCacheContext, and then applied to hover state.
Rejected Alternatives: Allocating a component-owned NativeArray batch for one command, keeping the NonAlloc ray because it is throttled, or forcing same-frame completion to preserve zero-latency hover.
Scalability potential: Low uses one late-frame async look result and keeps old hover until completion; Middle can route tool look queries through the same PlayerLook cache; High can batch multiple interaction/tool probes; Ultra can spend saved main-thread time on richer diegetic hover feedback.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is 20-60 us per interaction probe in collider-dense interiors.

## External Compile Blocker

Problem: After the PlayerInteraction continuation patch, the global Hecton8.Core build fails in VoxelDeltaProcessor.cs with 10 errors unrelated to the player/kinematics files.
Solution: Stopped at domain boundary and recorded BLOCKED BY DEPENDENCY for the continuation compile gate; did not edit voxel code from the kinematics prompt.
Rejected Alternatives: Crossing into voxel ownership, reverting unrelated VoxelDeltaProcessor changes, or claiming a clean global compile after the dependency broke.
Scalability potential: Low/Middle/High/Ultra all require the Integrator or voxel owner to restore the compile gate before final whole-project verification.
Hardware Impact: No runtime frame impact from this blocker; it prevents build verification only.

## Async Interaction Single-Hit Polish Decision

Problem: The dispatcher raycast lane returns one RaycastHit, while the removed PlayerInteraction NonAlloc loop could scan up to four hits for a registered interactable.
Solution: Keep the dispatcher single-hit path, rely on InteractableRegistry parent resolution for normal interactable colliders, and document the layer-discipline risk instead of reintroducing a sync query.
Rejected Alternatives: Reintroducing Physics.RaycastNonAlloc as a fallback, adding a component-owned NativeArray query lane, or expanding SystemDispatcher multi-hit behavior from this kinematics-only pass.
Scalability potential: Low uses one async look result and clean layer discipline; Middle can add a dispatcher-owned multi-hit result lane; High can share that lane across tool and UI look probes; Ultra can spend the preserved main-thread time on richer diegetic hover feedback.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 remains 20-60 us per interaction probe; the polish pass adds 0 runtime us.

## Comment Contract Polish Decision

Problem: PlayerFootstepAudio and PlayerInteraction still had comments/tooltips that described old synchronous raycast ownership after the runtime path had moved to cached KCC hits and async dispatcher results.
Solution: Reworded those comments and added a summary on HectonPlayerMovement.TryGetRecentFootstepSurfaceHit so future work sees the zero-sync contract at the call boundary.
Rejected Alternatives: Leaving stale comments because the compiled code was already clean, or adding runtime assertions just to explain editor-facing behavior.
Scalability potential: Low avoids future sync-cast regressions through accurate documentation; Middle/High/Ultra keep the same contract while richer surface classification or hover feedback can be added behind async/cache APIs.
Hardware Impact: No direct frame-time gain; protects the already removed 15-45 us footstep query and 20-60 us interaction query from accidental regression.

## Compile Gate Timeout After Polish

Problem: A fresh `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` attempt after polish did not return within the 124-second command window and produced no compiler diagnostics.
Solution: Recorded the timeout separately and kept the last concrete compile state as the known external VoxelDeltaProcessor.cs blocker; no clean build is claimed.
Rejected Alternatives: Waiting indefinitely, force-stopping unrelated processes beyond the command timeout, or crossing into voxel ownership to chase a non-kinematics compile path.
Scalability potential: Low/Middle/High/Ultra all need Integrator or build-owner verification once the global compile gate is responsive; kinematics runtime remains bounded by the clean player-domain symbol scans.
Hardware Impact: No runtime frame impact; the issue is verification throughput only.
