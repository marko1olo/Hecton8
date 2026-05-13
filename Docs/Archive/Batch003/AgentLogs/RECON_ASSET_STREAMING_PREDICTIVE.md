# RECON_ASSET_STREAMING_PREDICTIVE

Scan command: `rg "SceneManager\.LoadScene\s*\(" -n Assets/_Project/Scripts`

## Synchronous SceneManager.LoadScene Offenders
- `Assets/_Project/Scripts/Bootstrap/BootstrapRouteEnforcer.cs:44` -> `SceneManager.LoadScene(BootstrapSceneName);`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3371` -> `SceneManager.LoadScene(BootstrapSceneName);`
- `Assets/_Project/Scripts/Bootstrap/SceneGuard.cs:64` -> `SceneManager.LoadScene("00_BOOTSTRAP");`

## Streaming Domain Result
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` uses `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` for chunk scenes.
- No synchronous `SceneManager.LoadScene` calls were found in `Assets/_Project/Scripts/World/`.
