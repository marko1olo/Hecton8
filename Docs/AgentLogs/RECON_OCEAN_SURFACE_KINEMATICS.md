# RECON: OCEAN_SURFACE_KINEMATICS

Command:
`rg -n "Crest\.SampleHeightHelper|SampleHeightHelper" "C:\hades\Hecton8\Assets\_Project\Scripts"`

Result:
No active first-party script hits under `Assets/_Project/Scripts`.

Decision:
Mass-object buoyancy bypasses managed Crest helpers entirely. Surface kinematics now use Burst-side Gerstner evaluation from persistent `NativeArray<GerstnerWaveComponent>` data, with shader-only sargassum presentation using the same published wave spectrum.
