# RECON_AUDIO_GRANULAR_SYNTH

Scan target: `Assets`

Command:
`rg -n "AudioSource\.PlayOneShot" C:\hades\Hecton8\Assets`

Result:
- No `AudioSource.PlayOneShot` offenders found in `Assets` during the 2026-05-11 scan.

Migration note:
- Future offenders must be routed through the procedural audio event queue / native SPSC path, not direct managed one-shots.

Status: PENDING VERIFICATION
