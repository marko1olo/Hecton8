# Rationale 1824 - Mission Marker Policy Decisions

Evidence class: STATIC_DOC / STATIC_SOURCE references only.

## Decisions

1. Quest marker targets are policy-gated, not blanket-enabled.
   Reason: 1818 proves MissionMarkerSystem fails closed without authored mesh/material and marker assignments. VISION_LOCKS and sonar.md allow useful navigation but reject omniscient debug GPS.

2. `quest_copper_sample` needs a marker route to a discovered resource zone, not an exact hidden copper pickup.
   Reason: copper is first-route critical per 1803 and FIRST_20_MINUTES_ROUTE_BRIEF, but sonar.md forbids free hidden truth. `Node_Copper_A` / `Resource_FieldSources` are CANDIDATE targets until Unity proves live objects and discovery state.

3. Safe anchor and return route are marker-required after discovery.
   Reason: oxygen and death/return are safety-critical first-route beats. The marker may point to the known anchor/return bearing, not to hidden future objectives.

4. Beauty/rest beats forbid explicit mission markers.
   Reason: VISION_LOCKS, TASTE, world.md, 1806, and 1816 require bright readable surface/shallow spectacle. Markers over Aegir, waterline, coastline, and calm photic views would reduce exploration and hide world landmark work.

5. First hazard gets diegetic cue first; exact hazard GPS is forbidden.
   Reason: 1803 says first fair hazard is unproven. gameplay.md and sonar.md require counterplay and partial evidence, not exact enemy/trap certainty.

6. `quest_arrival` and `quest_first_breath` remain PENDING_DISCOVERY.
   Reason: 1818 proves their current quest assets lack marker target fields. A future Unity owner must bind route-known anchor/descent targets or explicitly record no-marker policy with route proof.

7. GlobalQualityWeight scales marker presentation only.
   Reason: AGENTS.md, quality.md, gameplay.md, ui.md, and sonar.md forbid quality from changing gameplay truth, save identity, DTO layout, command routing, or objective authority.
