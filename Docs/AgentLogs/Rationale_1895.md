# Rationale 1895

Evidence class: STATIC_SOURCE / STATIC_DOC.

Decision: implement a curated static audit instead of a broad repository crawler.

Reason: the task names exact ProductFace route contracts and forbids Unity/build/import work. A broad crawler would risk binary reads, stale archive noise, and false authority from unrelated reports.

Decision: normalize route roots for slash/backslash and trailing slash.

Reason: current source constants omit trailing slashes while the contract text often includes them. The route owner is the normalized generated root, not the literal slash style.

Decision: parse `In-game result:` line values directly instead of regex-only negative lookahead.

Reason: regex optional whitespace backtracking produced false red gates against reports that correctly said `PENDING VERIFICATION`.

Decision: classify current 1890 report as required when present and warning-only if absent.

Reason: the task says read 1890 if present. In this workspace it is present and audited.

Decision: keep `--fail-on-error` exit behavior separate from normal audit runs.

Reason: task requires default exit 0 unless `--fail-on-error` is supplied and errors exist.

Mandate note: `.agents-skills/PERF_Runtime_CPU_GC_ZeroAlloc.txt` is absent. Supporting context came from `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, but no runtime GC claim is made.
