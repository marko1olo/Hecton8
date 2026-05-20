# Rationale: PAYLOAD_AND_STREAMING_DOC_SCOUT_CURRENT34

Problem: Payload documentation can drift from actual StreamingAssets/Addressables/source-data state.
Solution: Use STATIC_SOURCE/STATIC_DOC evidence only: direct filesystem inventory plus line-numbered text scan.
Rejected Alternatives: No Unity import, runtime build, or Addressables analysis; those exceed read-only scout scope and would create false verification claims.
Scalability potential: Low/Middle/High/Ultra unaffected; this pass prevents stale payload assumptions from driving wrong streaming budgets.
Hardware Impact: 0 us runtime gain directly. Indirect i3/MX350 value is avoiding load-path work based on nonexistent files.

Problem: Mission explicitly forbids modification.
Solution: Write only required agent status/rationale/report files, then provide safe patch recommendations for the primary agent.
Rejected Alternatives: Direct doc patching rejected because sub-agent scope is read-only for project docs.
Scalability potential: Documentation truth supports tiered streaming plans without binary present/absent lies.
Hardware Impact: 0 us runtime impact.

Problem: Active docs disagree on directory absence versus empty-directory presence for `Assets/AddressableAssetsData` and `Assets/StreamingAssets`.
Solution: Classify current filesystem facts precisely: directory exists with 0 files, directory exists with Unity CSV plus `.meta`, or file missing.
Rejected Alternatives: Treating `.meta` as production payload or ignoring it completely. Recommendation reports both total files and non-meta payload files.
Scalability potential: Low/Middle/High/Ultra streaming gates can distinguish absent configuration from empty configuration.
Hardware Impact: 0 us runtime impact; prevents future i3/MX350 work from targeting nonexistent catalogs.
