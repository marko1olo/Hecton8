# Rationale 1876

Decision: implement a source authoring EditorWindow that writes only future `.mesh` assets.
Reason: prompt requires a later-run source route and forbids prefab, asset execution, Unity, build, and runtime changes during this task.

Decision: keep mesh construction manual through vertex/index helpers.
Reason: transport replacement must not rely on built-in primitive creation and must establish non-generic silhouettes for CargoSled, ExosuitFrame, MicroSub, and ScoutGlider.

Decision: encode rider/dismount clearance as validated source-spec intent, not mesh geometry and not gameplay anchor edits.
Reason: source authoring must preserve future anchor intent without altering transport truth, presets, anchors, colliders, or mount/dismount behavior.

Decision: use continuous `GlobalQualityWeight` for segment and detail counts.
Reason: root law rejects binary quality switches. Compact keeps silhouette/material-role identity; Middle/High/Ultra add density only.

Evidence boundary: static source only. No Unity import, compile, screenshot, prefab, collider, runtime, profiler, or visual acceptance claim exists.
