# OUTPOST_LOGISTICS_INITIALIZER Rationale

Status: `PENDING VERIFICATION`

## Decision 0 - Decoupled Boot Boundary

Problem: The prompt names `MARAUDER_OUTPOST_ARCHITECT`, but no source-backed class is present in the current project scan.
Solution: Use typed `SignalBus<WfcOutpostGeneratedSignal>` plus a native grid data registry. The generator only needs to register grid data and publish a signal; the power boot never references the generator class.
Rejected Alternatives: Direct class reference or scene search would break parallel-agent work and create compile failures while the architect is absent. Physics overlap was rejected because the prompt forbids collider adjacency and the logistics mandate requires logical graph data.
Scalability potential: Low keeps 500-cell cold graph translation only; Middle/High/Ultra can add more presentation flicker and richer brownout response without increasing adjacency truth cost.
Hardware Impact: Avoids `Physics.Overlap` on i3/MX350 and replaces it with fixed-grid integer adjacency; estimated cold-generation saving is 250-900 us per 500-cell outpost versus overlap scans.

## Decision 1 - Native Graph Translation

Problem: WFC modules need a power network without MonoBehaviour `PowerNode` objects or managed adjacency lists.
Solution: Build SOA node records and a `NativeParallelMultiHashMap<int,int>` edge set from the 10x10x5 logical grid using a Burst-compatible job.
Rejected Alternatives: GameObject components and `List<PowerNode>` links were too allocation-heavy and tied gameplay truth to scene presentation.
Scalability potential: Low uses one node per active cell; High/Ultra can layer visual-only cable sparks and flicker while the same compact graph remains authoritative.
Hardware Impact: Fixed 500-cell scan is predictable; expected cold-path translation under 150 us on target silicon, with 0 B managed allocation after persistent buffers are allocated.
