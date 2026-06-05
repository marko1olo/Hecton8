# Rationale 2306

Evidence class: STATIC FILE REVIEW ONLY. Unity was not run.

## Decisions

- Rejected direct Unity binding for all current Gemini/source candidates because every inspected source is `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED` or otherwise source-only without a passing manifest/audit.
- Treated root 1428 surface materials and all `Photic14xx` materials as risky for direct edits because `git status` shows dirty root materials and untracked photic folders from parallel work.
- Kept the output to planning artifacts only. No source image was imported, generated, deleted, moved, or rebound.
- Chose `Assets/_Project/Art/TEXTURES/World/Photic/` as the future canonical intake route requested by the task, while recording the existing project routes that currently hold terrain texture references.

## Risk Notes

- Existing Unity route contains an untracked wet basalt PNG under `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/`; static evidence does not prove it is safe. Treat as quarantine until source manifest and audit pass.
- Existing foam material routes point at `Assets/_Project/Art/TEXTURES/foam.png`; this may be usable as a legacy fallback, not final photic salt/foam proof.
- Existing terrain layers `L_Basalt`, `L_Sand`, and `L_sandGreen` use 1K/legacy texture stacks and lack the full accepted PBR family evidence required by the texture bibles.
