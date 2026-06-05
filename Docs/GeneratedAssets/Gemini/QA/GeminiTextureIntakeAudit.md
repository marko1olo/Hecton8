# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Docs/GeneratedAssets/Gemini`
Images scanned: 0
PASS_STATIC: 0
REVIEW: 0
REJECT: 0

## Rules

- `REJECT` means at least one hard static issue exists, usually non-square, severe edge mismatch, or too-dark albedo for surface/shallows.
- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.
- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.
- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.

## Findings

| Verdict | Role | Size | LR seam | TB seam | Lum mean | Path | Preview |
|---|---|---:|---:|---:|---:|---|---|
