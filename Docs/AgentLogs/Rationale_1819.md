# Rationale 1819

## Decisions

- Evidence boundary is STATIC only.
  Reason: task forbids Unity/runtime/profiler claims, scene edits, and hot `GlobalRegistry` polling additions. Real substrate consumption must remain pending unless current source/data proves it.
- Current GPR/Foundation/Drone source paths are classified as `STATIC_SOURCE_VERIFIED`, not runtime accepted.
  Reason: inspected code has lease/descriptor routes and fail-closed guards, but no current PlayMode/player/profiler/capture artifact shows live SDF substrate consumption in the first-20 route.
- DataMonolith is classified as `STATIC_BINARY_PRESENT_NO_ROUTE_BINDING` for this route.
  Reason: `static_data.h8bin` exists, but binary/text searches found no inspected `VoxelSdfTexture3D`, GPR, foundation, or drone SDF route keys; source-data `substrate` hits were narrative content.
- No source/data fix was applied.
  Reason: consumer-side fallback or fake SDF would hide missing owner publication. The narrow next step is a single Unity proof slot, then an owner-publish route if the payload is absent.
