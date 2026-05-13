# LOG_TOOLCHAIN_REPAIR

## 2026-05-13 - `tomli==2.4.1` wheel install failure

What was wrong: `uv` attempted to install `tomli-2.4.1-cp313-cp313-win_amd64.whl`, but the local cached artifact was invalid and lacked `.dist-info`.

What was done: Ran package-scoped `uv cache clean tomli`, which removed 13 cached files. `pip cache remove tomli` found no matching pip cache. Verified a fresh `uv` install in a disposable venv and imported `tomli.__version__ == 2.4.1`.

Cinematic Cheats used: None. Toolchain repair only.

Exact Microseconds saved: 0 us/frame. This unblocks installer/bootstrap path; no Unity hot path exists.

Verification: Fresh `uv` install succeeded. Rebuilt cache includes `tomli-2.4.1.dist-info`. No Unity code, assets, prefabs, scenes, or settings changed.
