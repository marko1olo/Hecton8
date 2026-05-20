# Rationale_SHINOBU_226

Status: PENDING VERIFICATION

Problem: XML assignment has 19 explicit task nodes because Task 09 is absent.
Solution: Track 19 concrete tasks and record the numbering gap instead of inventing Task 09.
Rejected Alternatives: Inventing a missing task would violate strict parsing and cross-agent boundary discipline.
Scalability potential: No runtime effect; prevents scope creep that would waste engineer time and create merge risk.
Hardware Impact: 0 us runtime; avoids unnecessary source churn on low-end i3/MX350 and high-end machines alike.

Problem: Scanner/lore sync belongs to Echelon 8 Presentation & UX but touches Tools, UI, DataVault, shader presentation, and static data.
Solution: Keep authoritative scan state as owner-local Vault-backed DTOs with cold bootstrap dependency caching and method-local resolves; communicate with neighboring systems only through contracts/Vault/shader scalar surfaces.
Rejected Alternatives: Direct references to concrete tool/PDA/DataMonolith runtime classes, per-target MonoBehaviour state, or managed event/string routes.
Scalability potential: Low uses the same integer state and cheap shader scalar; middle/high/ultra can spend saved CPU on denser diegetic visual noise without changing scan truth.
Hardware Impact: Expected CPU saving from replacing managed string lookup/UI unlock paths with uint hash and bitmask writes is bounded in single-digit microseconds per scan tick; measured proof absent.
