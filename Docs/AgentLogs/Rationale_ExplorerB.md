# ExplorerB Rationale

Problem: Need audit findings without mutating `dental-crm`.
Solution: Static source evidence only; label conclusions as STATIC_SOURCE and avoid compile/runtime claims.
Rejected Alternatives: Running broad rebuild or editing UI directly; task requests no edits and source audit.
Scalability potential: Not applicable to Unity runtime tiers; UX target is smaller copy/persistence fixes with high user impact.
Hardware Impact: No runtime impact measured; source-only audit.

Problem: Several UI strings remain English while existing smoke scripts claim Russian fallback guard coverage.
Solution: Treat smoke pass as insufficient coverage, not proof of absence; cite exact doctor-facing strings.
Rejected Alternatives: Editing strings in this audit pass; user requested no edits.
Scalability potential: N/A for web UI runtime tiers; small string-table target has broad UX impact.
Hardware Impact: 0 us measured; static source audit only.

Problem: Telegram settings looked manually saved because buttons remain visible.
Solution: Traced debounce autosave at `App.tsx` 4440-4458 and manual flush on onboarding transitions at 3806-3815; report as copy/affordance risk, not persistence failure.
Rejected Alternatives: Claiming autosave missing based on button text alone.
Scalability potential: Clear autosave state reduces support load across clinics.
Hardware Impact: 0 us measured; no runtime profiling.
