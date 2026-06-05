# Rationale 1871

Problem: four product-face transport prefabs are still root cube visuals with unresolved default-material-style material references. They preserve valid transport presets and anchors, so deletion/quarantine would destroy useful owner routes without proof.

Decision: mark all four as replace-not-quarantine. Define separate silhouettes because preset data and vehicle taste differ: scout glider is exposed/agile, cargo sled is load-hauling, exosuit frame is wearable mechanical survival gear, micro-sub is a pressure vessel.

Decision: use existing first-party material families as candidate material sources only, not as acceptance proof. `RuntimeShell1428` and runtime visual proof materials may inform hull/glass/wet steel identity, but no static scan proves a complete vehicle mesh or final material package.

Decision: reject `PFB_Submarine_Core` as direct visual source. Static scan found vehicle components and no renderer mesh evidence; it is not a micro-sub hull art asset.

Decision: reject `WorldProceduralProxy` and placeholder construction prefabs as source routes. The task explicitly forbids relinking to `WorldProceduralProxy`, and placeholder/proxy naming cannot satisfy the transport visual floor.

Priority: `PFB_ScoutGlider_Transport` first. Its preset has high early traversal/presentation evidence (`speedMultiplier 3.1`, `propulsionForce 1250`, `energyDrainPerSecond 3`, `swimPresentationScale 0.9`, `thrusterAudioScale 1`, `cameraMotionScale 0.92`) and best fits first-hour semi-open movement.

Evidence boundary: static source/doc only. No runtime, screenshot, visual acceptance, import health, profiler, or collision correctness claim was made.
