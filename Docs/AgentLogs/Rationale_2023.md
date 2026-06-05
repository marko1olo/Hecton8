# Rationale 2023

## Decisions

1. Read `UNITY_IMPORT_CHURN_READONLY_AUDIT_20260604.md` although not explicitly listed because the orchestration file cited it as current Unity/import evidence and the task asks for import-race critique.

2. Created a steer draft because evidence justifies light coordination: live Unity/import context, repeated scene/material imports, stale failing visual proof, and no fresh capture after later Photic1453/1455 imports.

3. Did not recommend killing Unity, AssetImportWorkers, ShaderCompilers, ILPP, UnwrapCL, MCP, or python wrappers. Process/log evidence shows active work and does not prove a hang.

4. Did not claim visual acceptance. The only inspected Unity visual capture is stale and below floor; newer captures are UI/control-plane images.

5. Treated generated asset names as weak static evidence only. Final-looking names do not prove mesh quality, material quality, placement correctness, LOD/collider validity, or route visual acceptance.

6. Kept output writes limited to requested report/status/log/orchestration docs.
