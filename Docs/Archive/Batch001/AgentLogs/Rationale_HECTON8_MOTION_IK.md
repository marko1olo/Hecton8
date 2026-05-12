# HECTON8_MOTION_IK Rationale

Status: PENDING VERIFICATION

## Assignment Binding

Problem: The active request is a chat-provided 25-task Adaptive Motion Engine audit, not the existing Deterministic Replay status file.
Solution: Bind this pass to `HECTON8_MOTION_IK` and keep a separate status/rationale trail.
Rejected Alternatives: Reusing `Status_HECTON-8.md` was rejected because it belongs to CORE/MEMORY replay work and would contaminate domain evidence.
Scalability potential: Low/Middle/High/Ultra decisions remain isolated to fauna/player animation and kinematics.
Hardware Impact: Process-only; no runtime impact.

## Mandate Selection

Problem: The work crosses physics scalar math, fauna procedural IK, shader VAT, and zero-GC hot paths.
Solution: Apply FABRIK/Contextual IK, VAT, Cinematic Cheat, Zero-GC, and Physics Determinism mandates before code edits.
Rejected Alternatives: Applying a generic refactor pass was rejected because AGENTS forbids broad domain drift.
Scalability potential: Low uses cadence skipping, dominant-axis cheats, and VAT fallback; High/Ultra can spend saved cycles on higher-frequency procedural solves.
Hardware Impact: Expected gains are static estimates until profiler logs exist.

## Predator Hit-Flash Presentation

Problem: Boid VAT shader already had GPU hit flash, but the main leviathan/fauna material path had corpse bloat and wounds only; damage feedback for predators still lacked the exact `_HitFlash` shader contract requested by the prompt.
Solution: Add `_HitFlash` to `Hecton_LeviathanOrganic.shader`, drive bloat with `smoothstep`, and route emission through one shader-side `lerp`. `FaunaBrain` now caches a hit-flash property mask and decays a scalar in the existing presentation update path.
Rejected Alternatives: Animator hit reactions were rejected because they add state transitions and parameter churn. MaterialPropertyBlock was rejected because AGENTS forbids MPB on standard geometry due SRP Batcher cost. Per-vertex CPU deformation was rejected because the shader can carry the player-visible effect.
Scalability potential: Low gets the same one-scalar shader fake with no Animator overhead; High/Ultra can layer wounds/sonar/biolum on top without changing CPU state.
Hardware Impact: Estimated ~5-20 us per damage event avoided versus Animator/material transition logic on MX350-class hardware. PENDING PROFILER.

## Adaptive IK Scalability Binding

Problem: The adaptive IK cadence was reading `GlobalRegistry.QualityTier`, which is currently equivalent but weaker evidence than the requested Scalability Matrix linkage.
Solution: Use `GlobalRegistry.ScalabilityTier` directly in `ResolveScalabilityMatrixIkFrameInterval`.
Rejected Alternatives: Adding a new ScalabilityMatrix interface was rejected because `GlobalRegistry` already exposes the required alias and AGENTS forbids invented dependencies.
Scalability potential: Low/Mx350 and distance >20m use 10Hz cadence; High/Ultra keep 30Hz cadence.
Hardware Impact: No new runtime cost. Evidence clarity improved; skipped solve savings remain ~20-80 us per distant leviathan solve window, PENDING PROFILER.

## Reciprocal Footstep Scalar

Problem: The deterministic footstep path still used one scalar division for speed normalization.
Solution: Replace `hSpeed / maxSpeed` with `hSpeed * math.rcp(maxSpeed)`.
Rejected Alternatives: Keeping division was rejected because the polish mandate requested reciprocal multiplication where safe. Sqrt planar speed was already absent and stayed absent.
Scalability potential: All tiers share the cheaper scalar path.
Hardware Impact: Estimated ~0.02-0.05 us per footstep event. PENDING PROFILER.

## Archivarius Hygiene Fix

Problem: Archivarius naming/content reports identify non-English/transliterated active-source comments as cleanup debt.
Solution: Replace one transliterated `DirectorMissionBridge` comment with ASCII English without changing behavior.
Rejected Alternatives: Mass-renaming or broad comment sweeps were rejected because Unity asset/reference moves need a dependency walk and this pass owns Motion/IK.
Scalability potential: No runtime effect.
Hardware Impact: No runtime impact.
