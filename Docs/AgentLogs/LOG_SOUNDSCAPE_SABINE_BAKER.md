# SOUNDSCAPE_SABINE_BAKER Log

## 2026-05-14 - Reverb LUT Bake

What was wrong:
Runtime-side acoustic reverb cannot calculate FDN/Sabine coefficients for many rooms on i3/MX350. The existing `sabine_reverb_rt60.bin` is `40 x 25`, headerless, and capped at `10,000m3`, so it does not satisfy the Sabine batch contract.

What was done:
Created `Tools/AcousticValidator.py`, baked `Data/Precomputed/Reverb_LUT.bin`, and documented the C# read-map in `Docs/Design/Acoustic_Binary_Specs.md`. The binary is `262400` bytes: `256` byte header plus `256 * 256 * 4` payload bytes. Payload CRC32 is `02A710C5`.

Cinematic Cheats used:
The LUT uses an equal-volume cube surface proxy instead of physical room mesh acoustics. Material damping curves are perceptual underwater fakes for Steel, Rock, Coral, and Water. This buys believable acoustic state without runtime convolution or FDN coefficient generation on low tier.

Exact Microseconds saved:
Offline bake measured `17,950.40us`. Runtime savings are source-estimated at `2-5us` per acoustic zone update by removing Sabine surface/absorption math and material damping curve evaluation from runtime. Unity profiler proof is absent; runtime status remains PENDING VERIFICATION.

Verification:
`python Tools/AcousticValidator.py` returned `STATUS: ACOUSTICS BAKED`.
`python Tools/AcousticValidator.py --verify-only` returned `STATUS: ACOUSTICS BAKED`.
`python -m py_compile Tools/AcousticValidator.py` passed.
`dotnet build --no-restore` could not run because `dotnet` is not installed in the shell PATH.
Unity compile/PlayMode could not run because `Unity` is not in PATH and `C:\Program Files\Unity\Hub\Editor` is missing.
Commit `8dc0eed5` was pushed to `origin/main`.
`<POLISH_MANDATE>` was absent from `Docs/Tasks/CURRENT_BATCH.md`; no invented mandate was executed.
Final anti-bloat pass found `Tools/AcousticValidator.py` at `11778` bytes, spec at `4810` bytes, binary at `262400` bytes, and no `AcousticValidator.pyc` residue.
Additional assurance added `Tools/test_acoustic_validator.py` to test deterministic output, header contract, recursive edge cases, CRC failure, and truncation failure.
Regression test result: `PYTHONDONTWRITEBYTECODE=1 python Tools/test_acoustic_validator.py` ran `5` tests in `0.261s` on final rerun and passed.
Post-test verify-only result: `STATUS: ACOUSTICS BAKED`.
Regression test commit `b87d7e17` was pushed to `origin/main`.
Clean isolated Sabine-only Unity-boundary commit `3b26e6af` was pushed to `origin/main`; unrelated local encyclopedia history was not pushed.

Edge cases:
Small locker error `0.00000439%`.
Crew compartment error `0.00001528%`.
Pressurized corridor error `0.00002720%`.
Mega-Cave error `0.00003172%`, under the required `0.01%`.
Giant Void error `0.00000000%`.
