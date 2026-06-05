# Rationale 2011

## Decisions

1. Writer tools were skipped when they created or overwrote shared reports, binaries, manifests, queues, or markdown files outside the exact 2011 deliverables.
   - Reason: task explicitly forbids active Assets edits, Unity, builds, destructive scripts, and requires exact deliverables.

2. `MaterialAudit.py` was run without `--json`, `--markdown`, or `--csv-prefix`.
   - Reason: stdout path is read-only; report export flags would create extra files.

3. `GeneratedAssetProductionAudit.py` was not rerun.
   - Reason: default output writes Batch18 JSON/markdown. Existing Batch18 report was sufficient static evidence.

4. `Crest_Quarantine_Polish_Audit.py` was not rerun.
   - Reason: hardcoded output path writes shared `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json`. Existing report was inspected.

5. Placement findings were classified as contract-only.
   - Reason: YAML depth/slope/heatmap fields do not prove actual MapMagic/world placement, dry-land exclusion, or current scene visuals.

6. ProductFace `0 findings` was not treated as closure.
   - Reason: the ProductFace route audit only checks its own static contract. Historical generated asset and material audits still report primitive mesh/default material debt.

7. Visual LOD verifier and stress sim were treated as static scalability evidence only.
   - Reason: `VisualStressSim.py` explicitly reports `PYTHON_OFFLINE_NOT_RUNTIME_PROOF`; no profiler or capture was run.

## Low / Middle / High / Ultra Consequences

- Compact/low lanes must preserve readable silhouettes, valid material identity, and no placeholder/default product-face art.
- Middle may add richer material/detail routes only after channel contracts and refs are valid.
- High may add stronger texture/detail density after Compact remains visually credible.
- Ultra may add visual overkill only through the same authority route; it must not change gameplay truth, placement truth, or channel semantics.
