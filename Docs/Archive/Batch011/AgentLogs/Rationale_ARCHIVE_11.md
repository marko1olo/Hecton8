ARCHIVE_11 rationale

Problem: Active batch hygiene requires moving Docs/Tasks and Docs/AgentLogs into a new archive while preserving CURRENT_BATCH.md.
Solution: Mirror Batch010 directory pattern with Batch011/Tasks and Batch011/AgentLogs, then add Batch011/Summaries for compressed md/txt-only summaries.
Rejected Alternatives: Flat archive was rejected because Batch010 keeps source buckets. Delete-after-copy was rejected because Move-Item preserves source intent and avoids stale duplicates.
Scalability potential: Low tier uses plain filesystem operations and extractive summaries; middle tier gains searchable compact summaries; high tier keeps source artifacts intact; ultra tier can consume manifests for later indexing.
Hardware Impact: One-time disk I/O only. Runtime/game frame impact 0 us. Estimated low-end i3/MX350 gain is operational: active docs scan avoids thousands of stale files after batch handoff.

Problem: Summary files can exceed readable/manual limits when logs are large.
Solution: Build sanitized chunks capped near 3.5 MB with overlap between parts.
Rejected Alternatives: Single huge combined summary was rejected due 3-4 MB cap. Including JSON/bin/log files was rejected because user constrained summary inputs to txt/md.
Scalability potential: Low keeps compact summaries, middle/high/ultra retain original files for forensic depth.
Hardware Impact: One-time PowerShell text processing; no Unity/player runtime cost.

Problem: Concurrent agents can create late files during archive handoff.
Solution: Three move passes with unique __late suffix on destination collision.
Rejected Alternatives: Single enumeration pass was rejected because it can leave race-created files behind. Overwriting was rejected because it destroys evidence.
Scalability potential: Low keeps deterministic handoff; middle/high/ultra can inspect manifest pass numbers.
Hardware Impact: Three bounded directory enumerations. Runtime/game frame impact 0 us.

Problem: Late active files remained during archive closure cycle 1.
Solution: Moved 10 late files with guarded unique destinations before summary regeneration.
Rejected Alternatives: Leaving late active files was rejected because user required full move except CURRENT_BATCH.md.
Scalability potential: Late manifests preserve concurrent-agent evidence without overwriting originals.
Hardware Impact: One-time filesystem pass. Runtime/game frame impact 0 us.

Problem: Active writer recreated files after prior clean-check.
Solution: Final guarded late close moved 7 files, then summaries regenerated.
Rejected Alternatives: Infinite chase rejected; 2-second stability window used for objective cutoff.
Scalability potential: Captures concurrent-write evidence in separate late manifest.
Hardware Impact: One-time filesystem pass. Runtime/game frame impact 0 us.

Problem: First summary pass capped retained lines per source and omitted useful late critical lines in long agent files.
Solution: Regenerated summaries from archived originals only, retaining all normalized critical md/txt lines and chunking outputs below 3.5 MB.
Rejected Alternatives: Keeping compact-but-lossy summaries was rejected after sample audit found omitted build gate and Vault decision lines. Touching active new files was rejected by user instruction.
Scalability potential: Low tier reads fewer chunks by category; middle/high/ultra preserve more diagnostic signal while original artifacts remain intact.
Hardware Impact: One-time archive text rewrite. Runtime/game frame impact 0 us.

Problem: Direct original-vs-summary spot check found non-critical but useful technical lines omitted: DTO sizes, BufferIDs, SDF/Vault/wake-prepass details, ANALYSIS targets/rule quotes.
Solution: Regenerated summaries from archived originals only, retaining all critical lines plus broad technical signal lines while preserving chunk cap.
Rejected Alternatives: Treating summaries as overview-only was rejected because user asked to validate important loss. Touching active new files was rejected.
Scalability potential: Low tier still reads chunked summaries; middle/high/ultra retain enough technical continuity for agent handoff without opening every original.
Hardware Impact: One-time archive text rewrite. Runtime/game frame impact 0 us.

Problem: Second original-vs-summary spot check still omitted handoff-useful assignment rows and analysis preamble rows: BufferID-style Name = Number, Target, Affected systems, Rule quote, ANALYSIS, and normalized checklist x rows.
Solution: Regenerated archive summaries again with explicit retention for assignment rows, analysis preamble rows, and checklist rows.
Rejected Alternatives: Leaving these in originals only was rejected because summary should preserve handoff-critical breadcrumbs. Active new files remain untouched.
Scalability potential: Slightly larger summaries, still chunked below cap; better continuation value on weak and high-end review machines.
Hardware Impact: One-time archive text rewrite. Runtime/game frame impact 0 us.

Problem: Spot check still found possible handoff loss in filtered summaries: exact StructLayout/offset line and standalone job-name lists can matter even without keywords.
Solution: Rebuilt summaries as cleaned combined text: retain every normalized unique non-empty md/txt line per source file, remove only formatting noise/articles/duplicates, chunk below cap.
Rejected Alternatives: More keyword expansion was rejected because future important lines can be keywordless. Active current files and НЕ ТРОГАТЬ.txt remain untouched.
Scalability potential: Larger but reliable summary chunks; low tier avoids old active-folder scan, high/ultra keep near-lossless text handoff.
Hardware Impact: One-time archive text rewrite. Runtime/game frame impact 0 us.
