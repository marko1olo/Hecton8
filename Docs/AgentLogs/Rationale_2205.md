# Rationale 2205

## Decisions

1. Treated 2205 as explicit batch/logging mode because the user supplied ID 2205 and a task file. Created Status/Rationale/LOG.

2. Did not run Unity, builds, or Play Mode. The task explicitly forbids taking the Unity slot by default and scopes work to file/log/proof audit.

3. Classified 1473 as relevant latest evidence even though the task names 1465-1472. It exists under `Docs/Screenshots/MCP`, is newer than 1472, and must not be ignored when judging latest proof state.

4. Rejected 1472 underwater proof after visual inspection because `h8_1472_underwater_0_5m.png` and `h8_1472_underwater_20_50m_route.png` show hard plane/surface clipping and do not prove underwater depth/post-stack quality.

5. Rejected 1473 underwater proof after visual inspection because `h8_1473_underwater_0_5m.png` and `h8_1473_underwater_20_50m_route.png` visually match the surface/coast composition instead of underwater route proof.

6. Kept runtime status rejected/pending because latest found visual-audit log contains repeated `ArgumentNullException` in `HectonCelestialEngine.UpdateAegirMaterial()` and no clean post-capture tail after 1473 screenshots.

7. Marked screenshot route risk mostly resolved by static source for `MMScreenshot` and `MMScreenshotEditor`: legacy/default `Assets/Screenshots` paths are redirected to `Docs/Screenshots`. The exact MCP named-packet generator still needs capture-session proof.

8. Did not edit code. The obvious fault is not a narrow proof-script path typo; it is a runtime celestial/material path requiring Unity-owner fix and fresh proof.
