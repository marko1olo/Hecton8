# Rationale_PhaseSafetyAuditor

## Decision 1
Problem: Audit must find phase-unsafe presentation writes without modifying source or running builds.
Solution: Static scan `Assets/_Project/**/*.cs` for method scopes named `Tick`, `FixedTick`, `Update`, `FixedUpdate`, and `Execute`, then manually inspect call sites involving renderer/material/shader/particle/audio/UI/transform mutation.
Rejected Alternatives: Runtime instrumentation and build validation are rejected because the prompt forbids dotnet/msbuild/Unity batch and requests scan-only. Automated blind regex-only reporting is rejected because it produces false positives without method context.
Scalability potential: Low/Middle/High/Ultra all require simulation to settle first and presentation writes to scale visually in `VISUAL_SYNC`, not mutate truth in simulation.
Hardware Impact: Moving presentation writes out of simulation reduces main-thread phase contention and avoids same-frame sync hazards on i3/MX350; exact microseconds require profiler evidence and are not claimed.

## Decision 2
Problem: Project mandate requires status/rationale/report files, while sub-agent prompt says not to edit files.
Solution: Treat "Do not edit files" as no source/project asset edits. Create only required audit memory/report files under `Docs/Tasks` and `Docs/AgentLogs`.
Rejected Alternatives: Skipping required report files would violate AGENTS.md reporting protocol. Editing source is outside mission.
Scalability potential: Audit records let integrator patch locally without cross-domain churn.
Hardware Impact: Documentation writes have zero runtime impact.

## Decision 3
Problem: Several systems mutate Unity presentation objects from `Tick`/`FixedTick` while the doctrine requires simulation to settle before presentation.
Solution: Mark only direct writes as violations: URP volume `.value` writes, visual `Transform.localRotation`, and `Light` property writes reached from hot methods. Local repair pattern is DTO/pending fields in hot phase and `LateFrameTick`/`VISUAL_SYNC` flush.
Rejected Alternatives: Classifying queued dirty flags as violations would create refactoring noise. Moving all owner logic to LateFrame would also be wrong because simulation truth should remain in Tick/FixedTick.
Scalability potential: Low tier can reduce flush cadence or visual amplitude through continuous `GlobalQualityWeight`; middle/high/ultra can increase smoothness and visual overkill inside LateFrame without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is removal of same-frame presentation calls from simulation lanes. Measured gain is 0 us because runtime profiling was forbidden; estimated patch impact is 3-80 us/frame per active offender based on Unity component/property write cost class, not a profiler measurement.

## Decision 4
Problem: Many files contain renderer/audio/particle tokens but already use deferred presentation queues.
Solution: Classify `BaseModule`, `AcousticZoneController`, `PlayerActionController`, `PhysicalBatteryCompartment`, `PhysicalSnapSwitch`, `SargassumCollapseChunk`, `WorldChunkResidencyManager`, `SargassumGlobalDragManager`, `HectonBiolumManager`, `GlobalWeatherDirector`, `HectonSurfaceWeatherDirector`, `InteriorGIProbeVolumeRuntime`, and `HectonUnderwaterVisuals` as inspected safe or non-violation for this mission where their hot methods only queue pending state and actual GPU/audio/particle/transform writes occur in LateFrame/VisualSync.
Rejected Alternatives: Reporting these as failures would waste integrator time and encourage refactoring loops.
Scalability potential: Existing queue/flush split supports continuous quality scaling by changing cadence, amount, or visual strength without moving authority.
Hardware Impact: No direct change; preserving existing deferred paths avoids unnecessary churn on hot systems.
