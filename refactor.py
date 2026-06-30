import re

def refactor(content):
    swim_physics_pattern = r"(private void SwimPhysics\(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset\)\s*\{)(.*?)(^\s*private bool TryResolveHeavyBrineSinkMultiplier)"
    match = re.search(swim_physics_pattern, content, re.DOTALL | re.MULTILINE)
    if not match:
        print("Could not find SwimPhysics!")
        return content

    body = match.group(2)

    # We will build the new helpers and new SwimPhysics
    helpers = []

    # Helper 1: ApplySwimVelocityOverrides
    velocity_override = """
        private void ApplySwimVelocityOverrides()
        {
            if (TryResolveHeavyBrineSinkMultiplier(ResolvePlayerAupRuntimePosition(), out float brineSinkMultiplier))
            {
                bool thrusterActive = math.abs(_inputVertical) > 0.01f || ResolveActiveTransportPropulsionForce() > 0.01f;
                Vector3 brineVelocity = HectonPlayerMotor.ResolveBuoyancyInversionVelocity(
                    _velocity,
                    true,
                    thrusterActive,
                    brineSinkMultiplier);
                Vector3 brineDelta = brineVelocity - _velocity;
                if (brineDelta.sqrMagnitude > 0.000001f)
                {
                    ApplyMotorVelocityChange(brineDelta);
                    _velocity = brineVelocity;
                }
            }

            if (IsCriticallyEncumbered && _velocity.y > 0f)
            {
                _velocity.y = 0f;
                ApplyMotorLinearVelocity(_velocity);
            }
        }
"""
    helpers.append(velocity_override)

    # Helper 2: ApplySwimDrag
    swim_drag = """
        private void ApplySwimDrag(SuitData suit, float fixedDeltaTime, bool isSurfaceSwim, float brineWaterDensityScale)
        {
            float speedSq = _velocity.sqrMagnitude;
            float depthDragAdd = PlayerSwimMotor.ResolveDepthDragAdd(
                _currentDepth,
                suit.depthSwimSlowdownStart,
                suit.depthSwimSlowdownEnd,
                suit.depthDragIncreaseMax);

            float effectiveDragCoeff = suit.swimDragCoefficient + depthDragAdd;
            if (isSurfaceSwim)
                effectiveDragCoeff *= surfaceDragMultiplier;

            float sargassumDragMultiplier = ResolveSargassumDragMultiplier();
            float externalEnvironmentalDragMultiplier = ResolveExternalEnvironmentalDragMultiplier();
            effectiveDragCoeff *= sargassumDragMultiplier;
            effectiveDragCoeff *= externalEnvironmentalDragMultiplier;
            effectiveDragCoeff *= ResolveActiveTransportDragCoefficientMultiplier();
            effectiveDragCoeff *= math.lerp(1f, crushDepthDragMultiplier, _hullStressIntensity);
            effectiveDragCoeff *= ResolveEquipmentDragCoefficientMultiplier();

            _lastPlayerKinematicsDragCoefficient = effectiveDragCoeff;
            _lastPlayerKinematicsWaterDensityScale = brineWaterDensityScale;

            if (speedSq > 0.0001f && _surfaceBreachFluidDragBypassTimer <= 0f)
            {
                Vector3 dampedVelocity = ResolvePlayerKinematicsBurstDragVelocity(
                    _velocity,
                    _lastPlayerKinematicsIntendedMovement,
                    effectiveDragCoeff,
                    brineWaterDensityScale,
                    fixedDeltaTime);
                ApplyMotorVelocityChange(dampedVelocity - _velocity);
                _velocity = dampedVelocity;
            }
        }
"""
    helpers.append(swim_drag)

    # Helper 3: CalculateSwimThrustMultipliers
    swim_thrust = """
        private void CalculateSwimThrustMultipliers(
            SuitData suit,
            bool isSurfaceSwim,
            float shoreSwimBlend,
            float brineSwimSpeedMultiplier,
            float sargassumSpeedMultiplier,
            float externalEnvironmentalThrustMultiplier,
            out float effectiveSwimForce,
            out float effectiveVerticalForce)
        {
            float depthSlowdown = PlayerSwimMotor.ResolveDepthSlowdown(
                _currentDepth,
                suit.depthSwimSlowdownStart,
                suit.depthSwimSlowdownEnd,
                suit.depthSwimSlowdownMax);

            bool heavyCarryActive = IsHeavyCarryActive();
            float sprintMult = _isSprinting && !heavyCarryActive ? suit.sprintMultiplier : 1f;
            float runtimeSwimSpeedScale = _runtimeSwimSpeedMultiplier * _runtimeVoxelBackpressureSwimSpeedMultiplier * _runtimeInjurySwimSpeedMultiplier * _runtimeEmergencyMovementMultiplier * _runtimeStaminaMultiplier * ResolveRuntimeInventoryLoadMovementMultiplier() * brineSwimSpeedMultiplier;

            effectiveSwimForce = suit.swimForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            effectiveVerticalForce = suit.swimVerticalForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            effectiveVerticalForce *= _runtimeInventoryUpwardSwimMultiplier;

            float heavyCarryForceMultiplier = ResolveHeavyCarryForceMultiplier();
            effectiveSwimForce *= heavyCarryForceMultiplier;
            effectiveVerticalForce *= heavyCarryForceMultiplier;

            effectiveSwimForce *= externalEnvironmentalThrustMultiplier;
            effectiveVerticalForce *= math.lerp(1f, externalEnvironmentalThrustMultiplier, 0.7f);
            effectiveSwimForce *= sargassumSpeedMultiplier;
            effectiveVerticalForce *= math.lerp(1f, sargassumSpeedMultiplier, 0.55f);
            effectiveSwimForce *= shoreSwimBlend;
            effectiveVerticalForce *= math.lerp(0.45f, 1f, shoreSwimBlend);
        }
"""
    helpers.append(swim_thrust)

    # Helper 4: CalculateSwimOrientation
    swim_orientation = """
        private void CalculateSwimOrientation(bool isSurfaceSwim, bool hasSurfaceDiveIntent, float transportForwardPitchInfluence, out Vector3 fwd, out Vector3 right)
        {
            ResolveDegreesSinCosFast(ResolveVrSwimmingReferenceYawDegrees(), out float sinBodyYaw, out float cosBodyYaw);
            ResolveDegreesSinCosFast(_cameraPitch, out float sinPitch, out float cosPitch);

            if (isSurfaceSwim && !hasSurfaceDiveIntent)
            {
                Vector3 bodyForward = new Vector3(sinBodyYaw, 0f, cosBodyYaw);
                Vector3 bodyRight = new Vector3(cosBodyYaw, 0f, -sinBodyYaw);
                Vector3 surfaceNormal = EffectiveWaterSurfaceNormal;
                Vector3 surfaceForward = ProjectOnPlaneFast(bodyForward, surfaceNormal);
                Vector3 surfaceRight = ProjectOnPlaneFast(bodyRight, surfaceNormal);

                if (surfaceForward.sqrMagnitude <= 0.0001f)
                    surfaceForward = bodyForward;
                else
                    surfaceForward = NormalizeVectorRsqrt(surfaceForward, bodyForward);

                if (surfaceRight.sqrMagnitude <= 0.0001f)
                    surfaceRight = bodyRight;
                else
                    surfaceRight = NormalizeVectorRsqrt(surfaceRight, bodyRight);

                fwd = surfaceForward;
                right = surfaceRight;
            }
            else
            {
                float surfaceDepthT = isSurfaceSwim
                    ? math.saturate(_currentDepth / math.max(surfaceSwimDepthBand, 0.01f))
                    : 1f;
                float surfacePitchBlend = isSurfaceSwim
                    ? math.lerp(1f - surfaceForwardPitchSuppression, 1f, surfaceDepthT)
                    : 1f;
                surfacePitchBlend *= transportForwardPitchInfluence;

                float fwdPlanarScale = math.lerp(1f, cosPitch, surfacePitchBlend);
                fwd = new Vector3(sinBodyYaw * fwdPlanarScale, -sinPitch * transportForwardPitchInfluence, cosBodyYaw * fwdPlanarScale);
                right = new Vector3(cosBodyYaw, 0f, -sinBodyYaw);
            }
        }
"""
    helpers.append(swim_orientation)

    # Helper 5: CalculateSwimIntendedDirection
    swim_dir = """
        private void CalculateSwimIntendedDirection(
            Vector3 fwd,
            Vector3 right,
            float forwardInput,
            float forwardScale,
            float gatedInputH,
            float strafeScale,
            float gatedInputVertical,
            float transportVerticalInputScale,
            bool isSurfaceSwim,
            out Vector3 inputDir,
            out float verticalInput)
        {
            float dirX = fwd.x * (forwardInput * forwardScale) + right.x * (gatedInputH * strafeScale);
            float dirY = fwd.y * (forwardInput * forwardScale);
            float dirZ = fwd.z * (forwardInput * forwardScale) + right.z * (gatedInputH * strafeScale);

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = math.rsqrt(math.max(sqrMag, 0.000001f));
                dirX *= invMag; dirY *= invMag; dirZ *= invMag;
            }

            verticalInput = gatedInputVertical;
            if (IsCriticallyEncumbered && verticalInput > 0f)
                verticalInput = 0f;

            if (isSurfaceSwim && verticalInput > 0f)
            {
                float ascendGate = math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                verticalInput *= ascendGate;
            }
            verticalInput *= transportVerticalInputScale;

            if (_activeTransportPlatform != null)
            {
                Vector3 platformUp = TransformTransportPlatformDirectionToWorld(Vector3.up);
                Vector3 rawInputWorld =
                    new Vector3(dirX, dirY, dirZ) +
                    (platformUp * verticalInput);
                Vector3 transformedInputWorld = ResolveTransportPlatformRelativeWorldDirection(rawInputWorld);
                dirX = transformedInputWorld.x;
                dirY = transformedInputWorld.y;
                dirZ = transformedInputWorld.z;
                verticalInput = 0f;
            }

            inputDir = new Vector3(dirX, dirY, dirZ);
        }
"""
    helpers.append(swim_dir)

    # Helper 6: CalculateBaseSwimForceVector
    swim_base_force = """
        private void CalculateBaseSwimForceVector(
            Vector3 inputDir,
            float verticalInput,
            float effectiveSwimForce,
            float effectiveVerticalForce,
            bool isSurfaceSwim,
            bool surfaceDiveAssistActive,
            float transportSurfaceDiveAssistScale,
            bool hasSurfaceDiveIntent)
        {
            _forceVector.x = inputDir.x * effectiveSwimForce;
            _forceVector.y = inputDir.y * effectiveSwimForce;
            _forceVector.z = inputDir.z * effectiveSwimForce;
            _forceVector.y += verticalInput * effectiveVerticalForce * (isSurfaceSwim ? surfaceVerticalForceMultiplier : 1f);

            if (surfaceDiveAssistActive)
            {
                float diveAssistT = math.saturate(_surfaceDiveAssistTimer / math.max(surfaceDiveAssistDuration, 0.01f));
                _forceVector.y -= effectiveVerticalForce * surfaceDiveAssistForceMultiplier * transportSurfaceDiveAssistScale * diveAssistT;
            }

            if (isSurfaceSwim && hasSurfaceDiveIntent && surfaceDiveResistanceDamping > 0f && _velocity.y < 0f)
            {
                float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
                float surfaceResistanceT = 1f - math.saturate(headDepth / math.max(surfaceDiveBreakDepth, 0.01f));
                if (surfaceResistanceT > 0f)
                {
                    _forceVector.y -= _velocity.y * ResolveAuthoritativeBodyMassKg() * surfaceDiveResistanceDamping * surfaceResistanceT;
                }
            }
        }
"""
    helpers.append(swim_base_force)

    # Helper 7: ApplyTransportPropulsionForce
    swim_propulsion = """
        private void ApplyTransportPropulsionForce(Vector3 fwd, float transportPropulsionForce)
        {
            Vector3 transportPropulsionDirection = new Vector3(_forceVector.x, _forceVector.y, _forceVector.z);
            if (transportPropulsionDirection.sqrMagnitude <= 0.0001f)
                transportPropulsionDirection = fwd;
            else
                transportPropulsionDirection = NormalizeVectorRsqrt(transportPropulsionDirection, fwd);

            if (ResolveActiveTransportSource() is MantaScooter mantaScooter &&
                mantaScooter.TryGetHullStressMisfireDeviation(out Vector2 misfireDeviationDegrees))
            {
                transportPropulsionDirection = RotateVectorByAxisAnglesDegrees(
                    transportPropulsionDirection,
                    misfireDeviationDegrees.x,
                    misfireDeviationDegrees.y,
                    0f);
            }

            if (math.abs(_abyssalTransportTurbulencePitchOffset) > 0.001f ||
                math.abs(_abyssalTransportTurbulenceYawOffset) > 0.001f)
            {
                transportPropulsionDirection = RotateVectorByAxisAnglesDegrees(
                    transportPropulsionDirection,
                    _abyssalTransportTurbulencePitchOffset,
                    _abyssalTransportTurbulenceYawOffset,
                    0f);
            }

            transportPropulsionDirection = ResolveProceduralThrusterNoiseDirection(transportPropulsionDirection);
            _forceVector.x += transportPropulsionDirection.x * transportPropulsionForce;
            _forceVector.y += transportPropulsionDirection.y * transportPropulsionForce;
            _forceVector.z += transportPropulsionDirection.z * transportPropulsionForce;
        }
"""
    helpers.append(swim_propulsion)

    # Helper 8: ApplySurfaceAscendDamping
    swim_damping = """
        private void ApplySurfaceAscendDamping(bool isSurfaceSwim)
        {
            if (isSurfaceSwim && surfaceAscendVelocityDamping > 0f && _velocity.y > 0f)
            {
                if (_velocity.y >= surfaceBreachReleaseVelocity)
                    return;

                float upwardDampingT = 1f - math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                if (upwardDampingT > 0f)
                {
                    _forceVector.x = 0f;
                    _forceVector.y = -_velocity.y * ResolveAuthoritativeBodyMassKg() * surfaceAscendVelocityDamping * upwardDampingT;
                    _forceVector.z = 0f;
                    ApplyMotorAccelerationFromForce(_forceVector);
                }
            }
        }
"""
    helpers.append(swim_damping)

    new_swim_physics = """        private void SwimPhysics(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            ApplySwimVelocityOverrides();

            bool isSurfaceSwim = _isSurfaceSwimming;
            bool hasSurfaceDiveIntent = isSurfaceSwim && HasCommittedSurfaceDive(transportPreset);
            float shoreSwimBlend = isSurfaceSwim ? _shoreBuoyancyBlend : 1f;
            float brineSwimSpeedMultiplier = _isInsideBrineLayer ? BrineLayerConstants.SwimSpeedMultiplier : 1f;
            float brineWaterDensityScale = _isInsideBrineLayer ? BrineLayerConstants.DensityMultiplier : 1f;

            float sargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
            float externalEnvironmentalThrustMultiplier = ResolveExternalEnvironmentalThrustMultiplier();

            ApplySwimDrag(suit, fixedDeltaTime, isSurfaceSwim, brineWaterDensityScale);

            // ─── Swim thrust ───
            float rawTransportPropulsionForce =
                ResolveActiveTransportPropulsionForce() *
                sargassumSpeedMultiplier *
                externalEnvironmentalThrustMultiplier *
                ResolveWipeoutTransportControl01();

            float gatedInputH = IsCriticalStaminaFailureActive ? 0f : _inputH;
            float gatedInputV = IsCriticalStaminaFailureActive ? 0f : _inputV;
            float gatedInputVertical = IsCriticalStaminaFailureActive ? 0f : _inputVertical;
            ApplyRuntimeNarcosisInputNoise(ref gatedInputH, ref gatedInputV, ref gatedInputVertical);
            _lastPlayerKinematicsIntendedMovement = new float3(gatedInputH, gatedInputVertical, gatedInputV);
            bool hasInput = gatedInputH != 0f || gatedInputV != 0f || gatedInputVertical != 0f;
            bool surfaceDiveAssistActive = _surfaceDiveAssistTimer > 0f;
            if (!hasInput && rawTransportPropulsionForce <= 0f && !surfaceDiveAssistActive)
                return;

            CalculateSwimThrustMultipliers(suit, isSurfaceSwim, shoreSwimBlend, brineSwimSpeedMultiplier, sargassumSpeedMultiplier, externalEnvironmentalThrustMultiplier, out float effectiveSwimForce, out float effectiveVerticalForce);

            float transportForwardPitchInfluence = ResolveTransportForwardPitchInfluence(transportPreset);
            float transportStrafeInputScale = ResolveTransportStrafeInputScale(transportPreset);
            float transportVerticalInputScale = ResolveTransportVerticalInputScale(transportPreset);
            float transportReverseThrustScale = ResolveTransportReverseThrustScale(transportPreset);
            float transportSurfaceDiveAssistScale = ResolveTransportSurfaceDiveAssistScale(transportPreset);
            float hullStressTurnScale = ResolveHullStressTurnResponsivenessScale(transportPreset);
            transportStrafeInputScale *= hullStressTurnScale;

            CalculateSwimOrientation(isSurfaceSwim, hasSurfaceDiveIntent, transportForwardPitchInfluence, out Vector3 fwd, out Vector3 right);

            float forwardScale = isSurfaceSwim ? surfaceForwardForceMultiplier : 1f;
            float strafeScale = (isSurfaceSwim ? surfaceStrafeForceMultiplier : 1f) * transportStrafeInputScale;
            float forwardInput = gatedInputV;
            if (forwardInput < 0f)
                forwardInput *= transportReverseThrustScale;

            float forwardVelocity = _velocity.x * fwd.x + _velocity.y * fwd.y + _velocity.z * fwd.z;
            float transportPropulsionForce = rawTransportPropulsionForce;
            if (transportPropulsionForce > 0f)
            {
                transportPropulsionForce *= shoreSwimBlend;
                float cavitationEfficiency = ResolveTransportCavitationEfficiency(
                    fixedDeltaTime,
                    true,
                    forwardVelocity,
                    ResolveActiveTransportBoost01());
                transportPropulsionForce *= cavitationEfficiency;
            }
            else
            {
                ResolveTransportCavitationEfficiency(fixedDeltaTime, false, forwardVelocity, 0f);
            }

            CalculateSwimIntendedDirection(fwd, right, forwardInput, forwardScale, gatedInputH, strafeScale, gatedInputVertical, transportVerticalInputScale, isSurfaceSwim, out Vector3 inputDir, out float verticalInput);

            _lastPlayerKinematicsIntendedMovement = new float3(inputDir.x, inputDir.y + verticalInput, inputDir.z);

            CalculateBaseSwimForceVector(inputDir, verticalInput, effectiveSwimForce, effectiveVerticalForce, isSurfaceSwim, surfaceDiveAssistActive, transportSurfaceDiveAssistScale, hasSurfaceDiveIntent);

            if (transportPropulsionForce > 0f)
            {
                ApplyTransportPropulsionForce(fwd, transportPropulsionForce);
            }

            _forceVector = ResolveCriticalEncumbranceSwimForce(_forceVector, IsCriticallyEncumbered);
            Vector3 swimAcceleration = HectonPlayerMotor.ResolveHydrodynamicAddedMassStatelessAcceleration(
                _forceVector,
                _velocity,
                ResolveAuthoritativeBodyMassKg());

            ApplyMotorAcceleration(swimAcceleration);
            ApplySargassumEntanglementForce(transportPreset);
            ApplyAbyssalCableEntanglementForce(transportPreset);
            ApplySargassumMatBuoyancySupport();

            ApplySurfaceAscendDamping(isSurfaceSwim);
        }
"""

    replacement = new_swim_physics + "\n" + "".join(helpers) + "\n"

    new_content = content[:match.start(1)] + replacement + match.group(3) + content[match.end(3):]

    with open("Assets/_Project/Scripts/HectonPlayerMovement.cs", "w") as f:
        f.write(new_content)

if __name__ == "__main__":
    with open("Assets/_Project/Scripts/HectonPlayerMovement.cs", "r") as f:
        content = f.read()
    refactor(content)
