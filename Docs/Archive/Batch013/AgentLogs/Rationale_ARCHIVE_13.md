# Rationale ARCHIVE_13
Date: 2026-05-28
Status: COMPLETE - STATIC_FILESYSTEM / PENDING USER REVIEW

Problem: Batch 13 task/log folders contain active evidence files and ongoing agent writes, but user requested a Batch 13 archive without wiping current work.
Solution: Use copy-only archival into `Docs/Archive/Batch013`, preserving active folders and excluding `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Moving active files would break current agents. Copying `CURRENT_BATCH.md` would violate the explicit request. Overwriting an existing archive would destroy proof boundary if one exists.
Scalability potential: Low/Middle tiers benefit from smaller summaries for search/context. High/Ultra retain raw copied evidence for forensic ingestion.
Hardware Impact: Runtime/game impact 0 us on i3/MX350. This is disk IO and text processing only.

Problem: User requested summaries as concentrated signal, cleaned of duplicates, extra syntax, articles, and garbage.
Solution: Generate extractive summaries from copied raw files, keep evidence-bearing lines, normalize duplicate lines, remove decorative XML/Markdown syntax where safe, drop high-frequency boilerplate, and report source/retained counts.
Rejected Alternatives: Full concatenation is too large. Aggressive rewrite risks losing blockers and proof artifacts. Deleting raw logs would erase evidence.
Scalability potential: Low keeps summary readable. Middle/High/Ultra keep raw source for exact proof when needed.
Hardware Impact: Runtime/game impact 0 us. Summary generation is one-time filesystem CPU/disk cost.

Problem: First PowerShell copy/summary attempt used `[System.IO.Path]::GetRelativePath`, unavailable in the local PowerShell/.NET host.
Solution: Re-ran with explicit full-path prefix subtraction. Existing partially-created `Batch013` directories were reused; no source files were deleted or moved.
Rejected Alternatives: Deleting and recreating archive root blindly would risk removing partial evidence. Continuing with failed script would leave empty archive buckets.
Scalability potential: No runtime tier effect. Operational script now works on older host APIs.
Hardware Impact: Runtime/game impact 0 us. Extra filesystem pass only.

Problem: Initial summary pass retained too much prompt mandate prose and remained too large for quick batch review.
Solution: Regenerated v2 summaries: exact duplicate files collapse by SHA256, prompt files keep task/id/role evidence only, status/rationale/log files keep blockers, proof artifacts, route terms, build/runtime/GC lines, and decision lines.
Rejected Alternatives: Keeping full prompt bodies in summary defeats the user's "concentrate" requirement. Rewriting into narrative risks false synthesis.
Scalability potential: Low/Middle read smaller summaries; High/Ultra retain raw copied source for exact forensic proof.
Hardware Impact: Runtime/game impact 0 us. Summary sizes after v2: TASKS 573729 B, RATIONALE 424772 B, LOGS 322709 B.
