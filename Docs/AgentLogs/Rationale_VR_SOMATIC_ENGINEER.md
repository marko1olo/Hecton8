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
