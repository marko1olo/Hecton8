# Rationale_SHINOBU_232

Status: PENDING VERIFICATION

## Decision 0 - Scope Boundary
Problem: Caustic lighting task spans rendering, voxel SDF visibility, ocean phase, and quality scaling while 20+ agents may edit neighboring systems.
Solution: Keep implementation in first-party rendering domain and use existing registry/vault/shader interfaces when present; otherwise provide owner-local fallback state without hard dependencies on unfinished agents.
Rejected Alternatives: Direct coupling to Celestial/Ocean/Voxel concrete classes because batch protocol forbids invented dependencies.
Scalability potential: Low uses one cheap monochrome Voronoi layer and shallow depth cutoff; Middle adds smoother panning; High adds dual-layer chroma; Ultra spends saved projector passes on richer distortion.
Hardware Impact: MX350/i3 avoids projector geometry re-render and light cookie passes; expected saving is from removed extra passes, not CPU claims until Unity profiling exists.

## Decision 1 - Task Count
Problem: Initial regex undercounted tasks because task lines are text labels, not XML task tags.
Solution: Treat Task 01 through Task 20 in the extracted XML as the authoritative count.
Rejected Alternatives: Counting XML phase tags or self-reflection questions as tasks.
Scalability potential: Correct loop scheduling prevents skipped quality tiers and verification.
Hardware Impact: No runtime impact.
