# QUEST_VULKAN_RENDER_PIPELINE Rationale

Status: PENDING VERIFICATION  
Owner: GRAPHICS_PROGRAMMER

## Baseline Decisions

Problem: Quest TBDR needs bandwidth reduction, but HECTON-8 also targets PC and other platforms.  
Solution: Quest-specific assets, Android/OpenXR compile gates, and runtime guards that require XR/Android evidence before changing texture limits or FFR.  
Rejected Alternatives: Global URP mutation and unconditional runtime `QualitySettings.masterTextureLimit = 1`; both would damage PC/non-Quest builds and violate the user's explicit platform warning.  
Scalability potential: Low uses no depth/opaque copies, 4x MSAA, mip limit, and FFR High; Middle keeps same asset with less aggressive FFR; High/Ultra PC keeps richer post stack and native textures.  
Hardware Impact: Quest 2/3 bandwidth saved by avoiding depth/opaque texture resolves and reducing texture residency; i3/MX350 unaffected unless an explicit low-tier profile selects similar behavior.

Problem: Active batch file does not contain the agent XML prompt.  
Solution: Record the direct dispatch summary in `Status_QUEST_VULKAN_RENDER_PIPELINE.md` and use CLI scans when available; do not invent neighboring batch context.  
Rejected Alternatives: Reading archive batch prompts or applying unrelated agent prompts. That would violate strict parsing and domain boundary.  
Scalability potential: No runtime impact. Preserves task isolation with 20+ agents active.  
Hardware Impact: 0 microseconds runtime; documentation-only.

## Pending Decision Journal

- URP asset mutation path: pending project scan.
- FFR API choice: pending installed package/API scan.
- Blackbox telemetry route: pending existing interface scan.
- Depth fake location: pending flood/waterline shader scan.
