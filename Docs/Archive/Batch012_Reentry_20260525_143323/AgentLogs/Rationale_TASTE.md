# Rationale_TASTE

Date: 2026-05-25
Agent: TASTE
Domain: Documentation / Taste Principles

## Decision 1 - Root Document Placement

Problem: The user asked for `TASTE.md` for the project, not a design-doc subsection.

Solution: Created root `TASTE.md` so code, art, audio, design, and marketing can use it as a fast taste authority.

Rejected Alternatives: `Docs/Design/TASTE.md` would make it look like a design-only artifact. `Docs/Marketing/TASTE.md` would bias it toward public positioning instead of production judgement.

Scalability potential: Low, middle, high, and ultra teams need the same taste target. Hardware changes fidelity, not identity.

Hardware Impact: 0us runtime. On i3/MX350 this prevents low-tier work from being treated as ugly fallback. On top-tier machines it prevents wasted overkill that does not serve identity.

## Decision 2 - Principles Instead Of Feature List

Problem: A taste document can degrade into a design doc, backlog, or marketing manifesto.

Solution: Wrote principles, acceptance tests, and anti-patterns. The file says what "good" feels like and how to reject weak work without assigning implementation owners.

Rejected Alternatives: Feature matrix, content roadmap, or world bible duplicate. Those already exist in active docs and would create stale parallel authority.

Scalability potential: Weak devices can meet taste through composition, fog LUTs, audio, scalar pressure, and clear routes. Middle adds density. High and ultra spend saved budget on sensory overkill without changing gameplay truth.

Hardware Impact: 0us runtime. Practical benefit is reduced review churn and fewer expensive features approved for the wrong reason.

## Decision 3 - Evidence-First Tone

Problem: The project rules reject fake reports, runtime claims without proof, and optimism.

Solution: `TASTE.md` explicitly states that taste guidance does not prove runtime quality. It uses review questions and rejection criteria, not certification language.

Rejected Alternatives: "This will make the game feel..." language and broad AAA claims. Those would be promotional, not production-safe.

Scalability potential: All tiers can be judged by visible output and evidence, not by promises. Ultra is allowed to be visually excessive only when it is still readable and measured.

Hardware Impact: 0us runtime. Prevents low-end overcommit and high-end decorative waste.

## Decision 4 - Identity Boundary

Problem: Existing docs repeatedly reject "Subnautica but darker"; a taste doc must enforce a distinct identity.

Solution: Centered taste on pressure, machinery, salvage, acoustic dread, damaged instruments, black-water readability, and evidence left by failure.

Rejected Alternatives: Bright alien-ocean wonder, creature zoo spectacle, cozy base fantasy, or generic sci-fi horror.

Scalability potential: Low quality uses hard silhouettes, dirty light cones, authored route cues, and sound. Middle adds object-batched density. High and ultra add fog shafts, wetness, dents, visor contamination, and silt.

Hardware Impact: 0us runtime. On i3/MX350 this keeps the game intentional with sparse assets. On high-end hardware it directs extra cost toward identity-carrying visuals.

## Decision 5 - Continuous Quality Language

Problem: Project doctrine rejects binary quality switches and low/ultra dichotomies.

Solution: The taste doc describes minimum, middle, high, and ultra as sampled points on one continuous `GlobalQualityWeight` curve.

Rejected Alternatives: "Low mode" and "Ultra mode" as separate taste standards. That would encourage hard gates and gameplay truth drift.

Scalability potential: Continuous weight lets fidelity, density, cadence, and presentation cost breathe with hardware while keeping gameplay truth stable.

Hardware Impact: 0us runtime. On weak hardware it protects frame stability; on strong hardware it gives a controlled place to spend saved CPU/GPU budget.

## Decision 6 - No Build

Problem: The task edited Markdown only. Running a build would violate the "do not launch dotnet rebuild if not needed" and CPU/compiler-guard spirit.

Solution: Used static file checks and content review only.

Rejected Alternatives: `dotnet build` or Unity import. These are irrelevant for a root Markdown document and can interfere with other agents.

Scalability potential: Documentation-only; no runtime path.

Hardware Impact: 0us runtime, 0us claimed savings.
