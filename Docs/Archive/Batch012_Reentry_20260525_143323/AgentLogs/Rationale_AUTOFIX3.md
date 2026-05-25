# AUTOFIX3 Rationale

Date: 2026-05-25
Agent: AUTOFIX3
Domain: Cross-domain diagnostic/runtime hygiene

## Decision 1

Problem: Many event/smoke/fallback diagnostic files still call `UnityEngine.Debug.Log*` directly. Some exception sites carry Unity object context, but `H8Debug` only exposes context overloads for normal log/warning/error, not exceptions.
Solution: Expand `H8Debug` with a development-only `LogException(Exception, UnityEngine.Object)` overload and convert selected non-authority diagnostics to the facade while preserving context where available.
Rejected Alternatives: Removing logs would hide development proof. Editing bootstrap fatal paths was rejected because release crash visibility may intentionally bypass the dev-only facade. Adding a new logger was rejected as redundant and cross-domain.
Scalability potential: Low/MX350 avoids release-player diagnostic call surface; Middle keeps development diagnostics; High/Ultra keep full editor/dev exception context for faster proof without production noise.
Hardware Impact: Measured savings pending. Expected steady-frame gameplay savings are 0 us; improvement is release IL stripping and lower diagnostic fault surface on cold/dev/fallback paths.
Phase: cold diagnostics, event dispatch exception isolation, UI fallback diagnostics, VISUAL_SYNC/POST_SIMULATION validation diagnostics.
Ownership: no gameplay fact owner changed; no DTO/save identity/public route mutation.
Evidence: pending scoped `rg` and build-gate check.

## Decision 2

Problem: Event dispatch exception isolation and fallback diagnostics were inconsistent: some paths were routed through `H8Debug`, others still went directly to `UnityEngine.Debug`.
Solution: Converted selected diagnostics in 34 non-facade files to `H8Debug`; added one facade overload in `Core/H8Debug.cs` to preserve Unity object context for exception diagnostics.
Rejected Alternatives: Project-wide direct-log conversion was rejected because bootstrap/fatal paths require separate release-diagnostic review. Raw scene/prefab changes were rejected because no scene ownership problem was proven.
Scalability potential: Low/MX350 strips these diagnostics from release IL; Middle keeps development-only proof; High/Ultra preserve context-rich editor navigation without making diagnostics a production runtime feature.
Hardware Impact: Measured microseconds saved: PENDING VERIFICATION. Static steady-frame impact: 0 us expected. Release-player impact: selected cold/dev/fallback diagnostic call sites are stripped by `[Conditional]`.
Phase: cold diagnostics, event dispatch isolation, VISUAL_SYNC fallback diagnostics, POST_SIMULATION validation diagnostics.
Ownership: no gameplay truth, save identity, DTO layout, public route, or scene/prefab authority changed.
Evidence: scoped identifier-bound `rg` over converted files returned no direct `Debug.Log*` matches. `git diff --check` exit 0 with LF/CRLF warnings only. Build skipped because CPU average was 70.8%; AGENTS.md forbids build under CPU >50%.
