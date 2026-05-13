# Rationale_VR_SOMATIC_ENGINEER

Agent: VR_SOMATIC_ENGINEER
Domain: ECHELON 4 VR Somatic Comfort / OpenXR Kinematics

Problem: Runtime VR bridge authority was unclear because the existing runtime bootstrap could self-spawn from XR state changes.
Solution: Move bridge creation under GameBootstrapper and leave runtime state changes as binding refresh only.
Rejected Alternatives: A singleton VRManager.Instance was rejected because the bootstrap mandate requires GlobalRegistry/contracts, not direct singleton ownership.
Scalability potential: Low uses one bridge and no duplicate rigs; Middle/High/Ultra spend saved frame time on comfort/hand visuals instead of service churn.
Hardware Impact: i3/MX350 saves avoidable scene scans and duplicate object creation; estimate 6-12 microseconds during XR activation spikes.

Problem: Tools must not know about VR controller devices.
Solution: Decode OpenXR controller values in the input/bridge layer, publish ToolTriggerSignal through GlobalSignals, and expose gameplay as PlayerInputAction bits.
Rejected Alternatives: Direct XR polling in ScannerTool/LaserCutter was rejected because it couples tools to device backend and violates controller abstraction.
Scalability potential: Low keeps only action scalar; High/Ultra can add richer hand visuals from the same signal without tool changes.
Hardware Impact: i3/MX350 avoids per-tool XR polling; estimate 2-5 microseconds saved per active tool tick.

Problem: VR root inherits player/submarine movement if bound directly to gameplay transforms.
Solution: Give the somatic provider a decoupled runtime root and feed it smoothed pose output from a Burst job.
Rejected Alternatives: Parenting the camera rig under the submarine was rejected because submarine pitch/roll nausea then leaks into head pose.
Scalability potential: Low uses the same root with cheaper horizon correction; High/Ultra can spend more on visual comfort layers.
Hardware Impact: i3/MX350 trades transform hierarchy churn for one tiny Burst job; estimate 4-9 microseconds saved in transform propagation.

Problem: XR tool input was leaking through device semantics instead of gameplay actions.
Solution: Added ToolTriggerSignal to GlobalSignals and translated OpenXR trigger/grip state inside InputDispatcher into PlayerInputAction bits.
Rejected Alternatives: Editing LaserCutter/Scanner to poll XR controller state was rejected because it creates tool-device coupling and multiplies hot polling cost.
Scalability potential: Low uses the scalar only; Middle/High/Ultra can add richer tool hand feedback from the same signal without changing tools.
Hardware Impact: i3/MX350 avoids duplicated controller reads per equipped tool; estimate 2-5 microseconds saved per tool tick and 0 B/frame.

Problem: Root stabilization needs to fight nausea without simulating submarine physics through the player's head.
Solution: Added VRSomaticRootSyncJob using quaternion.AxisAngle horizon correction above the 15 degree sine threshold, bounded by a 15 degree visual counter-rotation.
Rejected Alternatives: Quaternion.Euler hot-path correction and Rigidbody parenting were rejected because they are less predictable and couple comfort to transport physics.
Scalability potential: Low runs one scalar correction; Middle/High/Ultra can layer stronger post and hand visuals using the same root output.
Hardware Impact: i3/MX350 pays one tiny Burst job instead of transform hierarchy propagation; estimate 3-9 microseconds saved during sub roll.

Problem: Hands clipped because target pose and physical pose were the same data.
Solution: Added persistent HandTargets and HandPhysicalPositions NativeArrays and a Burst spring job using Velocity = (Target - Physical) * SpringForce.
Rejected Alternatives: Managed arrays and transform-only snapping were rejected because they allocate or remove the visual language needed for blocked hands.
Scalability potential: Low disables ghost visuals; Middle/High/Ultra can render target ghosts and physical hands with the same SOA buffers.
Hardware Impact: i3/MX350 two-hand job estimate 3-7 microseconds, 0 B/frame, future low-tier draw-call suppression through ghost mask.

Problem: Origin shifts can desynchronize cached VR runtime positions.
Solution: VRSomaticProvider now listens for OriginShiftEventData, force-completes its owned jobs inside the origin-shift barrier, subtracts shift from hand native buffers, and resets root/head pose history.
Rejected Alternatives: Relying on world-space cached NativeArrays after AUP shifts was rejected because it causes post-shift snaps and false wall-clipping masks.
Scalability potential: Low/Middle/High/Ultra all keep deterministic hand state; higher tiers can safely add visuals without shift drift.
Hardware Impact: Shift cost is cold-path only; hot path remains unchanged.

Problem: Compile verification hit unrelated assembly failures before VR code could be validated.
Solution: Ran the mandated dotnet build twice and recorded the external compile wall: missing Memory, Determinism, Cartography, DataVault, InputSignal, and StateCorrectionSignal symbols.
Rejected Alternatives: Changing PDA/Cartography/UI assembly ownership was rejected as outside VR Somatic domain and unsafe in the 20-agent dirty tree.
Scalability potential: No runtime impact; integrator must clear assembly references before Burst compile proof is meaningful.
Hardware Impact: No runtime impact. Verification blocked, not hidden.

Problem: Ghost-hand state existed as a provider-private mask, so future renderers would have to read provider internals or race NativeArrays while the hand job was scheduled.
Solution: Add VRSomaticHandPose plus IVRSomaticProvider.TryGetHandPose and HandGhostMask. Reads return false while the hand Burst job is scheduled, then expose target and spring-physical positions after completion.
Rejected Alternatives: Exposing NativeArray<float3> directly was rejected because it leaks ownership and creates race-prone external reads.
Scalability potential: Low can ignore the pose contract; Middle/High/Ultra can bind ghost/solid hand renderers from the same stable snapshot without changing gameplay tools.
Hardware Impact: i3/MX350 cost is an O(1) late-frame read only when visuals ask for it; expected hot cost under 1 microsecond and 0 B/frame.

Problem: AUP and non-finite failures in the VR somatic path did not have the mandated fixed-size postmortem buffer.
Solution: Added NativeArray<VRSomaticBlackBoxEntry>[300] with one record per frame, state hash, flags, head pose, hand separation, vignette, and AUP shift sequence. Non-finite head/root state dumps to Docs/AgentLogs/Dump_VR_SOMATIC_ENGINEER.bin.
Rejected Alternatives: Relying on GlobalTelemetryBus warnings was rejected because warnings do not preserve the last 300 frame states needed after a crash.
Scalability potential: Low/Middle/High/Ultra all pay one tiny fixed write per frame while active; high-tier visuals gain better fault evidence without adding runtime allocation.
Hardware Impact: i3/MX350 estimated 1-2 microseconds per active VR frame, 0 B/frame; binary writer is fault-only cold path.

Problem: Static review found three behavior risks after the first implementation: horizon correction sign, tool action gating tied to pose tracking, and possible root/AUP double movement.
Solution: Corrected the AxisAngle sign, kept trigger/grip action mapping alive through pose tracking hiccups, persisted the decoupled root through GameBootstrapper, and removed manual root offset during origin-shift rebasing.
Rejected Alternatives: Waiting for compile proof was rejected because the global build is blocked by unrelated dependencies and the defects were visible by source inspection.
Scalability potential: Low gets stable action input and less nausea; High/Ultra can layer richer visuals on the corrected root without inheriting submarine roll.
Hardware Impact: No extra hot allocation; trigger bridge still uses the preallocated XRInputState lane and ToolTriggerSignal queue.

Problem: Controller tracking loss made HandTargets snap to the fallback head-relative pose after the first valid hand frame.
Solution: Keep the last finite hand target after StateHandsInitialized and use the head fallback only before initialization or after invalid cached data.
Rejected Alternatives: Hard-snapping to fallback every untracked frame was rejected because it creates visible hand pops and false wall-clip distance spikes.
Scalability potential: Low gets stable physical hands without ghost churn; Middle/High/Ultra can layer ghost visuals on a less noisy target/physical separation signal.
Hardware Impact: i3/MX350 cost is two O(1) finite checks per frame; expected under 1 microsecond, 0 B/frame.

Problem: OpenXR analog/pose reads could pass non-finite values into XRInputState and ToolTriggerSignal, and dominant-controller changes could be hidden when strengths and masks were unchanged.
Solution: Clamp non-finite trigger/grip/joystick values to zero, invalid poses to zero/identity, compute dominance from trigger or grip strength, and include dominant-controller changes in signal publish gating.
Rejected Alternatives: Letting downstream tools sanitize was rejected because the input bridge is the single device abstraction boundary.
Scalability potential: Low avoids NaN-driven false tool states; High/Ultra can use stable controller dominance for richer hand/tool feedback.
Hardware Impact: Extra finite checks are scalar branch work in the two-controller bridge; estimated under 1 microsecond on i3/MX350 and 0 B/frame.

Problem: Invalid XR controller poses were sanitized to zero/identity but could still be marked tracked, which would let hand consumers treat fallback data as a real tracked hand.
Solution: Tie XRInputState.IsTracked to controller tracking and finite position/rotation validity. Invalid poses remain sanitized for safety but are no longer published as tracked.
Rejected Alternatives: Passing zero/identity as a tracked pose was rejected because it teleports physical hands toward the world origin and creates false wall-clipping signals.
Scalability potential: Low avoids visible hand snaps; Middle/High/Ultra get stable ghost/solid hand separation without renderer churn.
Hardware Impact: No new allocation; two cached boolean checks per controller, estimated below 1 microsecond on i3/MX350.

Problem: Same-frame black-box updates could overwrite an earlier non-finite flag with a later inactive/normal record.
Solution: OR previous same-frame black-box flags into the replacement entry so fault evidence survives the circular buffer even after late-frame state cleanup.
Rejected Alternatives: Writing multiple records per frame was rejected because the mandate asks for a fixed 300-frame buffer, not an event spam queue.
Scalability potential: All tiers get cleaner postmortem evidence with no extra NativeArray capacity.
Hardware Impact: One cached entry read on same-frame overwrite only; no measurable frame cost.

OMEGA POLISH CHANGES:
- Dear Lie audit: root horizon locking uses a bounded visual fake, not a physical submarine-roll simulation. The correction is a sine-threshold AxisAngle clamp with max 15 degree counter-rotation.
- Scalability matrix: low tier and H8_LOW_MEMORY_PROFILE suppress ghost-hand mask output; high/ultra can bind ghost renderers later without changing hand SOA data.
- sqrt/normalize audit: no math.sqrt or math.normalize was introduced in the VR somatic code path; root/hand jobs use math.rsqrt or finite fallbacks.
- Division audit: new root sync division sites were converted to math.rcp multiplications.
- GC audit: no foreach, string interpolation, string.Format, or ToString was introduced in the VR somatic hot path. Existing GameBootstrapper ToString sites are unrelated boot/report formatting.
- Domain audit: GameBootstrapper, GlobalSignals, InputDispatcher, GlobalRegistryContracts, VRSomaticRuntimeBootstrap, and VRSomaticProvider were touched because VR bridge ownership requires bootstrap, signal, input, contract, and provider seams. No tool file was edited.
- Final diff stat for touched files: GameBootstrapper.cs +58; GlobalRegistryContracts.cs +492 dirty-file total; GlobalSignals.cs +1361 dirty-file total; InputDispatcher.cs +93/-; VRSomaticProvider.cs +601/-; VRSomaticRuntimeBootstrap.cs +66/-; status/rationale logs updated. Dirty-file totals include pre-existing edits from other agents in shared files.
- STATUS: PENDING due global compile dependency wall.
