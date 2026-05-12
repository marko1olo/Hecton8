# HECTON8_XR Rationale

Status: PENDING VERIFICATION

## Loop 1

Problem: VR comfort state already existed, but the high-speed tunnel threshold was tuned for desktop-style comfort at 5.25 m/s rather than the requested 15 m/s VR gate.
Solution: Raise the serialized default and validation clamp so the KCC-speed tunnel reaches full intent around 15 m/s, with shader-side peripheral darkening.
Rejected Alternatives: Camera FOV reduction and transform shake were rejected because they add vestibular conflict. A full extra blur pass was rejected as more expensive than a dithered peripheral mask.
Scalability potential: Low uses dithered black edge mask; Middle adds mild peripheral desaturation; High keeps visor distortion coupling; Ultra can spend saved cycles on better lens/refraction detail.
Hardware Impact: MX350-class gain is avoiding an always-on blur path; expected CPU cost stays below 10 us because work is scalar state plus shader uniforms.

Problem: Submarine roll/pitch inheritance can rotate the XR eye horizon with the vehicle frame.
Solution: When VR horizon lock is active, preserve platform yaw while removing platform tilt with a yaw-only basis. Math form: R_locked = (R_yawOnly * inverse(R_platform)) * R_platform * R_local.
Rejected Alternatives: Smoothly rolling the camera back to zero only solves local camera roll and still inherits submarine tilt. Disabling platform rotation globally would break PC/submarine locomotion.
Scalability potential: Low/Middle/High/Ultra all use the same quaternion path; visual quality scales in the shader layer, not in vestibular math.
Hardware Impact: A few quaternion/vector ops in an existing camera state path; estimated under 10 us on i3/MX350.

Problem: Snap turn was an atomic yaw swap but had no explicit 0.1 second visual blackout envelope.
Solution: Add a zero-GC timer that publishes blackout intensity through the existing VR comfort signal vector.
Rejected Alternatives: Coroutine fade and UI overlay were rejected because they allocate/route through non-diegetic UI.
Scalability potential: Low uses full-screen black fade only; High/Ultra may add visor distortion while preserving the same CPU path.
Hardware Impact: One scalar timer and one shader vector component; expected CPU impact below 5 us.
