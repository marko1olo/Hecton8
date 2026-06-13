# Recommended Next Actions

1. **Fix Bootstrap Issue:** Start Play Mode from 00_BOOTSTRAP, or add an auto-bootstrap mechanism.
2. **Fix Bootstrap Issue:** Always start from 00_BOOTSTRAP. Use EditorBuildSettings to enforce scene 0.
3. **Fix Registry Issue:** Start from 00_BOOTSTRAP scene or debug the bootstrap flow.
4. **Fix Atmosphere Issue:** Ensure CelestialEngine is present and active in the world scene.
5. **Fix Atmosphere Issue:** Ensure AtmosphereManager is present and active in the world scene.
6. **Fix Crest Issue:** Ensure the Crest adapter or kinematics provider calls GlobalRegistry.Register during init.
7. **Fix Crest Issue:** Ensure a Sun directional light exists and is assigned to OceanRenderer.
8. **Fix Crest Issue:** Ensure a main camera exists and Crest can discover it, or assign it explicitly.
9. **Fix Registry Issue:** Ensure tick dispatcher is present in the bootstrap scene and registers during initialization.
10. **Fix Registry Issue:** Ensure the Dispatcher provider is present and its registration runs during bootstrap.
11. **Fix Registry Issue:** Ensure the TickManager provider is present and its registration runs during bootstrap.
12. **Fix Registry Issue:** Ensure the TerrainProviderRuntime provider is present and its registration runs during bootstrap.
13. **Fix Registry Issue:** Ensure the CelestialEngineRuntime provider is present and its registration runs during bootstrap.
14. **Fix Registry Issue:** Ensure the MapMagicRuntime provider is present and its registration runs during bootstrap.
15. **Fix Registry Issue:** Ensure the OceanKinematics provider is present and its registration runs during bootstrap.
16. **Fix Registry Issue:** Ensure the Player provider is present and its registration runs during bootstrap.
17. **Fix Registry Issue:** Ensure the UI provider is present and its registration runs during bootstrap.
18. **Fix Registry Issue:** Ensure the Save provider is present and its registration runs during bootstrap.
19. **Fix Registry Issue:** Ensure the Scene provider is present and its registration runs during bootstrap.
20. **Fix Registry Issue:** Ensure the Audio provider is present and its registration runs during bootstrap.
21. **Fix Registry Issue:** Ensure the Physics provider is present and its registration runs during bootstrap.
22. **Fix Registry Issue:** Ensure the Input provider is present and its registration runs during bootstrap.
23. **Fix Bootstrap Issue:** Debug the bootstrap sequence. Check for missing dependencies or exceptions during initialization.
24. **Fix Registry Issue:** Ensure the AtmosphereRuntime provider is present and its registration runs during bootstrap.
25. **Fix Atmosphere Issue:** Start from 00_BOOTSTRAP scene. These managers should appear after bootstrap phase 2.

## If you only ran EditMode:
Consider running `H8Runner.RunPlayMode()` (once implemented) or pressing Play in the Editor to gather runtime metrics.
