# Rationale_PROCEDURAL_GEOMETRY_ARCHITECT

Status: PENDING VERIFICATION

## Decision 0 - Tooling Boundary

Problem: Procedural kelp/coral/rock generation must not become runtime mesh generation on the i3/MX350 target.
Solution: Keep Bio-Forge entirely under an Editor assembly and write production `.asset` meshes and prefabs for runtime consumption.
Rejected Alternatives: Runtime MonoBehaviour generator, scene-time generator, or bootstrap-registered generator. All would violate the prompt and risk CPU stalls.
Scalability potential: Low uses coarse baked meshes and aggressive LOD2; Middle uses denser LOD0; High uses richer silhouettes; Ultra spends saved runtime cycles on shader detail, not CPU generation.
Hardware Impact: Estimated runtime cost stays 0 us on i3/MX350 because generation is offline.

## Decision 1 - SDF Blend Math

Problem: Branching organic forms need smooth joins without per-branch mesh intersections.
Solution: Use exponential smooth-min: `-log(exp(-k*a) + exp(-k*b)) / k`, with clamped exponent inputs and `k` authored per BioRuleData.
Rejected Alternatives: Boolean mesh union is slower and fragile in editor batches; naive `min(a,b)` causes visible hard seams; runtime deformation is banned.
Scalability potential: Low uses smaller volume resolution and lower branch counts; Middle/High raise resolution; Ultra can increase smoothness/detail because runtime is still baked.
Hardware Impact: Runtime 0 us. Editor generation cost is bounded by volume resolution and uses Native containers.

## Decision 2 - Deterministic Variation

Problem: Batch output must be reproducible and not depend on UnityEngine.Random or wall-clock state.
Solution: Use explicit integer seed hashing and deterministic xorshift-style generation for rule expansion, rock noise offsets, and variation naming.
Rejected Alternatives: `UnityEngine.Random.Range`, `System.Random`, object instance IDs, and clock-based naming. These break replayability and auditability.
Scalability potential: Same seed can output Low/Middle/High/Ultra mesh budgets deterministically.
Hardware Impact: No runtime cost; editor batches are reproducible for asset diffs.
