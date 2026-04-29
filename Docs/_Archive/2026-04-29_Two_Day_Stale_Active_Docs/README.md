**WARNING: LEGACY DOCUMENT BUNDLE.** These items were removed from the active `Docs/` surface on `2026-04-29` because they were older than two days and no longer acting as current execution anchors.

# 2026-04-29 Two Day Stale Active Docs

Date: `2026-04-29`
Status: `PENDING VERIFICATION`

Mandates followed:

- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_Persistent_Object_Registry.txt`

## Archive Rule Used

- active `Docs/` items older than two days
- excluded long-lived reference bundles and current-day materials
- excluded `Docs/_Archive/` existing bundles
- excluded `Docs/2026-04-20_Deepseek_Ideas_Reality_Audit/` because its last-write evidence is `2026-04-29`

## Moved Items

- `2026-04-14_UNDERWATER_VISUAL_AUDIT_AND_EXECUTION_PLAN.md`
- `2026-04-15_Player_Expression/`
- `2026-04-15_Player_Retention_Recovery/`
- `2026-04-15_Subnautica_Gap_Audit/`
- `2026-04-15_UNDERWATER_ASSET_REQUIREMENTS.md`
- `2026-04-15_Underwater_Recovery/`
- `2026-04-16_Autonomous_Runtime_Stabilization/`
- `2026-04-16_Celestial_Mechanics_Audit/`
- `2026-04-16_Movement_Realism_Audit/`
- `2026-04-16_Soft_Onboarding_Spine/`
- `2026-04-16_Swim_Presentation_Architecture/`
- `2026-04-17_Terrain_Runtime_Audit/`
- `2026-04-17_Underwater_Visual_Audit/`
- `2026-04-19_Gemini_Reality_Audit/`
- `2026-04-19_Underwater_System_Audit/`
- `2026-04-20_Sargassum_Reality_Audit/`
- `2026-04-26_Iteration18_StartupAudit.md`

## Regression Model

- CPU: none
- GC: none
- Memory: none
- Cadence: archive drift remains possible if active indexes are not updated after moves
- Correctness: moving stale active docs reduces false-current authority but can break links if indexes are not maintained

## Hot Path Impact

- none

## Failure Modes

- hardcoded links from legacy docs may still point to the old root-level paths
- stale docs can be reintroduced to active use if users ignore archive warnings

## Why Kept

- the bundle preserves history without leaving stale execution plans in the active index
