# Rationale_X_004_SUB_A

Problem: X_004 Roslyn report contains valid historical findings, but live source has changed during inspection. A stale line list would misclassify already queued VISUAL_SYNC paths as still fatal.
Solution: Re-read current source with line-level `rg`/PowerShell ranges and classify by actual caller phase. DOD pattern: phase segregation proof by helper-chain, not token presence alone.
Rejected Alternatives: Reporting raw JSON hits was rejected because HectonCaveVoxelLightingVolume and SargassumCutManager now have queue/LateFrame paths that the report does not reflect.
Scalability potential: Low uses scalar/DTO dirty flags and sparse uploads; Middle flushes current global sets in VISUAL_SYNC; High/Ultra spend saved simulation time on shader/VFX detail only.
Hardware Impact: Avoiding Shader/Material/Graphics/Particle work in Tick/SIMULATION removes main-thread jitter risk on i3/MX350. Exact gain is pending profiler proof; no fake microsecond claim.

Problem: Several systems combine gameplay truth and presentation ownership in one MonoBehaviour, causing helper methods to be reachable from Tick/SlowTick/FixedTick.
Solution: Minimal patch plan per file uses dirty DTO/scalar staging plus `LateFrameTick`/render-owner flush. DOD pattern: simulation writes finite structs/scalars only; presentation consumes read-only snapshots.
Rejected Alternatives: Moving all logic to new global services was rejected; it grows authority surface and risks cross-agent dependency collisions. Keep owner-local queues unless fan-out requires SignalBus.
Scalability potential: Low flushes only dirty fields and limits texture uploads; Middle enables normal cadence; High/Ultra increase shader/VAT/particle richness, not gameplay truth cost.
Hardware Impact: On i3/MX350 this reduces hot path Unity API calls and texture uploads; on high-end hardware it preserves visual-overkill room in VISUAL_SYNC.

Problem: Graphics and compute calls were not always represented in the old fatal list, especially `Graphics.RenderMesh*`, `Graphics.RenderPrimitives`, `ComputeShader.Dispatch`, `RenderTexture` clears, and MPB uploads.
Solution: Treat direct rendering, material/MPB mutation, particle emission, texture apply, and shader global writes as presentation leaks when reachable from Tick/SlowTick/FixedTick.
Rejected Alternatives: Ignoring compute/render work because it is GPU-side was rejected; phase law forbids presentation uploads/draw submission from simulation phases.
Scalability potential: Low defers and coalesces GPU uploads/draws; Middle preserves existing visuals through VISUAL_SYNC; High/Ultra can raise buffer density and shader detail without simulation contamination.
Hardware Impact: Removes driver/API submission work from simulation on low-end silicon; exact microseconds require Unity profiler, not static inspection.

Problem: Some source uses strings only in cold or editor paths; no evidence shows hot string allocation in the inspected chains.
Solution: Do not claim string GC without source evidence. DOD pattern: evidence-only reporting.
Rejected Alternatives: Blanket string-allocation accusations were rejected as fake reporting.
Scalability potential: Keeps focus on measured hot leaks instead of noise.
Hardware Impact: No quantified impact claimed.
