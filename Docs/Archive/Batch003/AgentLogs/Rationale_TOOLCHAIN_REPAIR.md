# Rationale_TOOLCHAIN_REPAIR

## 2026-05-13 - Invalid tomli wheel cache

Problem: `uv` failed to install `tomli==2.4.1` because the cached wheel was invalid: missing `.dist-info` directory.

Solution: Removed only the `tomli` entries from the `uv` cache, then forced a clean re-download through a disposable venv. The rebuilt cache now contains `tomli-2.4.1.dist-info`.

Rejected Alternatives: Full `uv cache clean` was wider than required. `pip cache purge` was irrelevant because `pip` had no cached `tomli`. Installing `tomli` into system Python was rejected because the failing command likely creates its own target environment and the package was not present globally before the repair.

Scalability potential: No runtime game system involved. Low/Middle/High/Ultra device tiers are unaffected. This only removes installer nondeterminism for tool bootstrap.

Hardware Impact: 0 us/frame on i3/MX350. Cold install reliability improves by avoiding reuse of corrupt local package artifacts.

Regression Model: CPU/GC/memory/frame cadence unchanged for Unity runtime. Risk is limited to package manager cache state. Verification uses fresh `uv` install and import in an isolated temporary environment.
