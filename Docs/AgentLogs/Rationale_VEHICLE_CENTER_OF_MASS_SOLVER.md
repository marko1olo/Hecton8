# Rationale_VEHICLE_CENTER_OF_MASS_SOLVER

Status: PENDING VERIFICATION

Problem: The batch requires a dynamic flooding mass solver for player submarine physics without per-frame PhysX center-of-mass rebuilds.
Solution: Use visual-fake-first physics truth: Burst computes low-cadence weighted COM and angular drag multiplier from compartment water data; main-thread Rigidbody writes are throttled outside `Update`, while PID/control systems consume a compact result snapshot.
Rejected Alternatives: Continuous slosh simulation, exact 3x3 inertia tensor recomputation, and `Rigidbody.centerOfMass` writes every frame. They are unstable on MX350/i3 and violate the mandate against repeated PhysX rebuilds.
Scalability potential: Low uses 1Hz COM updates with scalar fill math; Middle uses slow tick smoothing; High adds richer stress/audio/visual response; Ultra can spend saved cycles on presentation overkill without changing gameplay authority.
Hardware Impact: Expected hot math is O(roomCount) over fixed room arrays. For 8 rooms, Burst loop is sub-10 us on i3/MX350 class CPU before Unity scheduling overhead; static proof only until profiler evidence exists.

Problem: The prompt requires coupling GAS_DYNAMICS_SOLVER and PIPE_LOGISTICS without inventing direct dependencies on other agents.
Solution: Discover existing contracts first; consume existing signal/provider interfaces if present, otherwise add narrow contract structs/interfaces in a contracts assembly and keep concrete code out of cross-domain references.
Rejected Alternatives: Concrete references to pipe/gas solver classes, polling scene objects, or direct singleton access from hot paths.
Scalability potential: Interface/signal path allows low tier to receive scalar fill deltas and high tier to receive richer compartment state later.
Hardware Impact: Avoids scene search and virtual dependency chains in hot cadence; expected gain is removal of O(scene) lookup risk, not a fake measured number.

Problem: Critical vehicle physics needs post-mortem state.
Solution: Implement/bridge a 300-entry blackbox for COM offset, total water mass, flags, and pitch threshold state, dumping to `Docs/AgentLogs/Dump_VEHICLE_CENTER_OF_MASS_SOLVER.bin` on invalid numeric state if no shared telemetry writer exists.
Rejected Alternatives: Debug.Log spam, string telemetry, or "inspect later" reports.
Scalability potential: Low stores compact hashes/flags; Ultra can add extra presentation diagnostics in dev builds only.
Hardware Impact: 300 compact entries are kilobytes-scale persistent memory, negligible versus 8GB RAM target; runtime write is one struct assignment.
