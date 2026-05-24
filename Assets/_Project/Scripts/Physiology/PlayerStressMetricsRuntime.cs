using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physiology
{
    /// <summary>
    /// SlowTick-only player psycho-metrics authority. It owns one stress scalar and publishes consequences by signal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStressMetricsRuntime : MonoBehaviour, ISlowTickable, IModuleStatusEventListener
    {
        private const float StressSubstepDeltaSeconds = 0.1f;
        private const int StressSubstepsPerSlowTick = 5;
        private const float DarknessLightThreshold01 = 0.2f;
        private const float SafeLightThreshold01 = 0.8f;
        private const float DarknessStressPerSecond = 0.05f;
        private const float ApexStressPerSecond = 0.2f;
        private const float RecoveryPerSecond = 0.1f;
        private const float ApexThreatRadiusMeters = 50f;
        private const float AcousticStressImpulseScale = 0.08f;
        private const float DamageStressImpulseScale = 0.18f;
        private const float SqueezeStressImpulseScale = 1.0f;
        private const float SqueezeStressPerSecond = 0.1f;
        private const float SqueezeStressPerSlowTick = SqueezeStressPerSecond * StressSubstepDeltaSeconds * StressSubstepsPerSlowTick;
        private const float O2StressMultiplier = 1.5f;
        private const float NeutralLightLevel01 = 0.5f;
        private const float InvByteMax = 1f / 255f;
        private const float PanicAttackThreshold01 = 1f;
        private const float PeakStressTelemetryStep01 = 0.05f;
        private const float HallucinationStressThreshold01 = 0.9f;
        private const float HallucinationResetThreshold01 = 0.84f;
        private const int HallucinationCooldownMinSlowTicks = 36;
        private const int HallucinationCooldownRandomSlowTicks = 48;
        private const float HallucinationForwardMeters = 36f;
        private const float HallucinationSideMeters = 18f;
        private const float HallucinationUpMeters = 1.25f;
        private const uint GhostlyFishSpeciesHash = 0x47534648u;
        private const uint PanicAttackTraumaHash = 0x50414E49u;
        private const byte GhostlyFishDebrisKind = 9;
        private const byte TraumaKindPanicAttack = 1;
        private const byte CauseDarkness = 1;
        private const byte CauseApexPredator = 2;
        private const byte CauseDamage = 3;
        private const byte CauseAcoustic = 4;
        private const byte CauseRecovery = 5;
        private const byte CauseSqueeze = 6;
        private const byte FlagDarkness = 1 << 0;
        private const byte FlagApexPredator = 1 << 1;
        private const byte FlagDamage = 1 << 2;
        private const byte FlagAcoustic = 1 << 3;
        private const byte FlagRecovery = 1 << 4;
        private const byte FlagInsidePoweredBase = 1 << 5;
        private const byte FlagHallucination = 1 << 6;
        private const byte FlagPanicAttack = 1 << 7;
        private const uint PhysiologyNanContextHash = 0x5053594Eu;

        private static PlayerStressMetricsRuntime _runtimeInstance;

        [SerializeField] private float _debugPlayerStress01;
        [SerializeField] private float _debugLightLevel01 = NeutralLightLevel01;
        [SerializeField] private float _debugPredatorThreat01;
        [SerializeField] private float _debugO2DrainMultiplier = 1f;

        private StressSoA _state;
        private int _lastDamageSequence;
        private int _lastAcousticSequence;
        private int _lastLightSequence;
        private int _lastPlayerStateSequence;
        private int _hallucinationCooldownSlowTicks;
        private uint _rngState = 0xA341316Cu;
        private uint _sourceEntityId;
        private uint _slowTickFrameCounter;
        private bool _registeredSlowTick;
        private bool _registeredModuleStatus;
        private bool _insidePoweredBase;

        public float PlayerStress01 => _state.PlayerStress01;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootRuntime()
        {
            if (!Application.isPlaying || _runtimeInstance != null)
                return;

            GameObject runtimeObject = new GameObject("[PlayerStressMetricsRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap physiology authority - owner: PlayerStressMetricsRuntime
            DontDestroyOnLoad(runtimeObject);
            runtimeObject.AddComponent<PlayerStressMetricsRuntime>(); // COLD ALLOC: MonoBehaviour[1] - bootstrap physiology authority - owner: PlayerStressMetricsRuntime
        }

        private void Awake()
        {
            if (_runtimeInstance != null && _runtimeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            _runtimeInstance = this;
            _sourceEntityId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            _state.LightLevel01 = NeutralLightLevel01;
            _state.O2DrainMultiplier = 1f;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }

            if (_registeredModuleStatus)
            {
                ModuleStatusEvents.Unregister(this);
                _registeredModuleStatus = false;
            }

            if (ReferenceEquals(_runtimeInstance, this))
                _runtimeInstance = null;
        }

        public void SlowTick()
        {
            _slowTickFrameCounter = unchecked(_slowTickFrameCounter + 1u);
            if (!TryResolvePlayerPose(out PlayerPose pose))
            {
                HandleMissingPlayerPose();
                return;
            }

            ConsumeLatestInputSignals(in pose);
            EvaluateThreats(in pose);
            if (!IntegrateStress())
            {
                PublishSignals(0, 0);
                ApplyDebugState();
                return;
            }

            if (!TryPublishableStateOrRecover())
                return;

            PublishSignals(_state.LastCause, _state.LastFlags);
            TryEmitHallucination(in pose);
            WritePeakTelemetryIfNeeded();
            ApplyDebugState();
        }

        public void OnModuleStatusEvent(in ModuleStatusEventPayload payload)
        {
            if (!ModuleStatusEvents.IsPlayerInsideInterior(in payload))
                return;

            if (ModuleStatusEvents.IsEnterEvent(in payload))
            {
                _insidePoweredBase =
                    ModuleStatusEvents.HasPower(in payload) &&
                    !ModuleStatusEvents.IsBreached(in payload) &&
                    !ModuleStatusEvents.IsFlooded(in payload);
                return;
            }

            _insidePoweredBase = false;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);

            if (!_registeredModuleStatus)
            {
                ModuleStatusEvents.Register(this);
                _registeredModuleStatus = true;
            }
        }

        private void ConsumeLatestInputSignals(in PlayerPose pose)
        {
            if (GlobalSignals.TryGetLatestAcousticPingSignal(out AcousticPingSignal acousticSignal, out int acousticSequence) &&
                acousticSequence != _lastAcousticSequence)
            {
                _lastAcousticSequence = acousticSequence;
                _state.AcousticImpulse01 = math.max(_state.AcousticImpulse01, ResolveAcousticStress(in acousticSignal, in pose));
            }

            if (GlobalSignals.TryGetLatestDamageSignal(out CombatDamageSignal damageSignal, out int damageSequence) &&
                damageSequence != _lastDamageSequence)
            {
                _lastDamageSequence = damageSequence;
                float magnitude = SanitizeUnit(damageSignal.Magnitude * 0.01f);
                float integrityDelta = SanitizeUnit(damageSignal.IntegrityDelta * InvByteMax);
                _state.DamageImpulse01 = math.max(_state.DamageImpulse01, SanitizeUnit(magnitude + integrityDelta));
            }

            if (GlobalSignals.TryGetLatestLightLevelSignal(out LightLevelSignal lightSignal, out int lightSequence) &&
                lightSequence != _lastLightSequence)
            {
                _lastLightSequence = lightSequence;
                if ((lightSignal.Flags & LightLevelSignalFlags.ValidSample) != 0 &&
                    lightSignal.SampleKind == LightLevelSignalSampleKinds.CaveVoxelSdf)
                {
                    _state.LightLevel01 = SanitizeUnit(lightSignal.LightLevel01, NeutralLightLevel01);
                }
                else
                {
                    _state.LightLevel01 = NeutralLightLevel01;
                }
            }

            if (GlobalSignals.TryGetLatestPlayerStateSignal(out PlayerStateSignal playerStateSignal, out int playerStateSequence) &&
                playerStateSequence != _lastPlayerStateSequence)
            {
                _lastPlayerStateSequence = playerStateSequence;
                if (playerStateSignal.State == PlayerStateSignal.StateSqueezing &&
                    (playerStateSignal.Flags & PlayerStateSignal.FlagSqueezing) != 0)
                {
                    _state.SqueezeImpulse01 = math.max(
                        _state.SqueezeImpulse01,
                        SqueezeStressPerSlowTick);
                }
            }
        }

        private void HandleMissingPlayerPose()
        {
            if (!TryPublishableStateOrRecover())
                return;

            _state.PlayerStress01 = math.saturate(_state.PlayerStress01 -
                RecoveryPerSecond * StressSubstepDeltaSeconds * StressSubstepsPerSlowTick);
            _state.LightLevel01 = NeutralLightLevel01;
            _state.PredatorThreat01 = 0f;
            _state.AcousticImpulse01 = 0f;
            _state.DamageImpulse01 = 0f;
            _state.SqueezeImpulse01 = 0f;
            _state.Recovery01 = 0f;
            _state.O2DrainMultiplier = 1f + _state.PlayerStress01 * O2StressMultiplier;
            _state.LastCause = 0;
            _state.LastFlags = 0;
            if (!TryPublishableStateOrRecover())
                return;

            PublishSignals(0, 0);
            ApplyDebugState();
        }

        private void EvaluateThreats(in PlayerPose pose)
        {
            _state.PredatorThreat01 = 0f;
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector != null &&
                ecosystemDirector.TryGetApexPredatorThreat(pose.RuntimePosition, ApexThreatRadiusMeters, out float proximity01))
            {
                float safeProximity01 = SanitizeUnit(proximity01);
                if (safeProximity01 > 0f)
                    _state.PredatorThreat01 = math.max(0.25f, safeProximity01);
            }
        }

        private bool IntegrateStress()
        {
            float stress = _state.PlayerStress01;
            byte flags = 0;
            byte cause = 0;

            if (_state.AcousticImpulse01 > 0f)
            {
                stress += _state.AcousticImpulse01 * AcousticStressImpulseScale;
                flags |= FlagAcoustic;
                cause = CauseAcoustic;
            }

            if (_state.DamageImpulse01 > 0f)
            {
                stress += _state.DamageImpulse01 * DamageStressImpulseScale;
                flags |= FlagDamage;
                cause = CauseDamage;
            }

            if (_state.SqueezeImpulse01 > 0f)
            {
                stress += _state.SqueezeImpulse01 * SqueezeStressImpulseScale;
                cause = CauseSqueeze;
            }

            for (int i = 0; i < StressSubstepsPerSlowTick; i++)
            {
                if (_state.LightLevel01 < DarknessLightThreshold01)
                {
                    stress += DarknessStressPerSecond * StressSubstepDeltaSeconds;
                    flags |= FlagDarkness;
                    cause = CauseDarkness;
                }

                if (_state.PredatorThreat01 > 0f)
                {
                    stress += ApexStressPerSecond * _state.PredatorThreat01 * StressSubstepDeltaSeconds;
                    flags |= FlagApexPredator;
                    cause = CauseApexPredator;
                }

                bool recoveryActive = _insidePoweredBase || _state.LightLevel01 > SafeLightThreshold01;
                if (recoveryActive)
                {
                    stress -= RecoveryPerSecond * StressSubstepDeltaSeconds;
                    flags |= FlagRecovery;
                    cause = cause == 0 ? CauseRecovery : cause;
                    if (_insidePoweredBase)
                        flags |= FlagInsidePoweredBase;
                }
            }

            if (!math.isfinite(stress))
            {
                _state.LastCause = cause;
                _state.LastFlags = flags;
                _state.AcousticImpulse01 = 0f;
                _state.DamageImpulse01 = 0f;
                _state.SqueezeImpulse01 = 0f;
                CrashTelemetryBuffer.ReportPhysiologyNan(
                    stress,
                    _state.O2DrainMultiplier,
                    PhysiologyNanContextHash);
                ResetStressStateToNeutral();
                return false;
            }

            _state.PlayerStress01 = math.saturate(stress);
            _state.O2DrainMultiplier = 1f + _state.PlayerStress01 * O2StressMultiplier;
            _state.Recovery01 = (flags & FlagRecovery) != 0 ? 1f : 0f;
            _state.LastCause = cause;
            _state.LastFlags = flags;
            _state.AcousticImpulse01 = 0f;
            _state.DamageImpulse01 = 0f;
            _state.SqueezeImpulse01 = 0f;

            if (_state.PlayerStress01 >= PanicAttackThreshold01 && _state.PanicAttackEmitted == 0)
            {
                _state.PanicAttackEmitted = 1;
                EmitPanicAttack();
            }
            else if (_state.PlayerStress01 < 0.75f)
            {
                _state.PanicAttackEmitted = 0;
            }

            return true;
        }

        private void PublishSignals(byte cause, byte flags)
        {
            uint frame = _slowTickFrameCounter;
            PlayerStressSignal stressSignal = new PlayerStressSignal
            {
                Stress01 = _state.PlayerStress01,
                OxygenDrainScale = _state.O2DrainMultiplier,
                AggressionScale = 1f + _state.PlayerStress01,
                Frame = frame,
                Cause = cause,
                Flags = flags
            };
            GlobalSignals.Publish(in stressSignal);

            PhysiologyStateSignal physiologySignal = new PhysiologyStateSignal
            {
                PlayerStress01 = _state.PlayerStress01,
                O2DrainMultiplier = _state.O2DrainMultiplier,
                Recovery01 = _state.Recovery01,
                Frame = frame,
                Cause = cause,
                Flags = flags
            };
            GlobalSignals.Publish(in physiologySignal);
        }

        private void EmitPanicAttack()
        {
            TraumaSignal signal = new TraumaSignal
            {
                TraumaHash = PanicAttackTraumaHash,
                Stress01 = _state.PlayerStress01,
                Frame = _slowTickFrameCounter,
                TraumaKind = TraumaKindPanicAttack,
                Severity = byte.MaxValue,
                Flags = FlagPanicAttack
            };
            GlobalSignals.Publish(in signal);
        }

        private void TryEmitHallucination(in PlayerPose pose)
        {
            if (_state.PlayerStress01 <= HallucinationStressThreshold01)
            {
                if (_state.PlayerStress01 < HallucinationResetThreshold01)
                    _hallucinationCooldownSlowTicks = 0;
                return;
            }

            float hallucinationWeight = ResolveHallucinationVisualWeight();
            if (hallucinationWeight <= 0.0001f)
                return;

            if (_hallucinationCooldownSlowTicks > 0)
            {
                _hallucinationCooldownSlowTicks--;
                return;
            }

            uint next = NextRandom();
            int minCooldown = (int)math.round(math.lerp(
                HallucinationCooldownMinSlowTicks * 4f,
                HallucinationCooldownMinSlowTicks,
                hallucinationWeight));
            int randomWindow = math.max(1, (int)math.round(math.lerp(
                HallucinationCooldownRandomSlowTicks * 4f,
                HallucinationCooldownRandomSlowTicks,
                hallucinationWeight)));
            _hallucinationCooldownSlowTicks = minCooldown + (int)(next % (uint)randomWindow);
            float sideSign = (next & 1u) == 0u ? -1f : 1f;
            float verticalJitter = ((next >> 8) & 255u) * InvByteMax - 0.5f;
            float3 right = NormalizeApproxNoSqrt(math.cross(new float3(0f, 1f, 0f), pose.Forward), new float3(1f, 0f, 0f));
            float distanceScale = math.lerp(0.55f, 1f, hallucinationWeight);
            float3 offset = pose.Forward * (HallucinationForwardMeters * distanceScale) +
                right * (sideSign * HallucinationSideMeters * distanceScale) +
                new float3(0f, HallucinationUpMeters + verticalJitter * hallucinationWeight, 0f);

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = OffsetAupByRuntimeDelta(in pose.Aup, offset),
                SpeciesHash = GhostlyFishSpeciesHash,
                SourceEntityId = _sourceEntityId,
                Intensity01 = _state.PlayerStress01 * hallucinationWeight,
                DebrisKind = GhostlyFishDebrisKind,
                Flags = FlagHallucination
            };
            GlobalSignals.Publish(in signal);
        }

        private void WritePeakTelemetryIfNeeded()
        {
            if (_state.PlayerStress01 < _state.PeakStress01 + PeakStressTelemetryStep01 &&
                _state.PlayerStress01 < PanicAttackThreshold01)
            {
                return;
            }

            _state.PeakStress01 = math.max(_state.PeakStress01, _state.PlayerStress01);
            _state.PeakStressEvents++;
            CrashTelemetryBuffer.ReportPeakStressEvent(_state.PlayerStress01, _state.O2DrainMultiplier, _state.PeakStressEvents);
        }

        private float ResolveAcousticStress(in AcousticPingSignal signal, in PlayerPose pose)
        {
            if (!math.isfinite(signal.RadiusMeters) || !math.isfinite(signal.Intensity01))
                return 0f;

            float radius = math.max(0.001f, signal.RadiusMeters);
            double radiusSq = (double)radius * radius;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in signal.PositionAup, in pose.Aup);
            if (!math.isfinite(distanceSq) || distanceSq >= radiusSq)
                return 0f;

            double invRadiusSq = math.rcp(radiusSq);
            float proximity01 = math.saturate(1f - (float)(distanceSq * invRadiusSq));
            return math.saturate(signal.Intensity01 * proximity01);
        }

        private bool TryPublishableStateOrRecover()
        {
            if (IsStressStateFinite())
                return true;

            CrashTelemetryBuffer.ReportPhysiologyNan(
                _state.PlayerStress01,
                _state.O2DrainMultiplier,
                PhysiologyNanContextHash);
            ResetStressStateToNeutral();
            PublishSignals(0, 0);
            ApplyDebugState();
            return false;
        }

        private void ResetStressStateToNeutral()
        {
            uint peakStressEvents = _state.PeakStressEvents;
            _state = default;
            _state.LightLevel01 = NeutralLightLevel01;
            _state.O2DrainMultiplier = 1f;
            _state.PeakStressEvents = peakStressEvents;
        }

        private bool IsStressStateFinite()
        {
            return math.isfinite(_state.PlayerStress01) &&
                math.isfinite(_state.LightLevel01) &&
                math.isfinite(_state.PredatorThreat01) &&
                math.isfinite(_state.AcousticImpulse01) &&
                math.isfinite(_state.DamageImpulse01) &&
                math.isfinite(_state.SqueezeImpulse01) &&
                math.isfinite(_state.Recovery01) &&
                math.isfinite(_state.O2DrainMultiplier) &&
                math.isfinite(_state.PeakStress01);
        }

        private static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeUnit(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : fallback;
        }

        private static AbsoluteUniversePosition OffsetAupByRuntimeDelta(in AbsoluteUniversePosition origin, float3 runtimeDelta)
        {
            return AbsoluteUniversePosition.OffsetMeters(
                in origin,
                new double3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z));
        }

        private bool TryResolvePlayerPose(out PlayerPose pose)
        {
            pose = default;
            IPlayerRuntimeContext player = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            if (player == null || !player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return false;

            float3 runtimePosition = snapshot.RuntimePosition;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition aup = snapshot.Aup;
            if (!IsFiniteAup(in aup))
            {
                AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
                if (!IsFiniteAup(in originAup))
                    return false;

                aup = OffsetAupByRuntimeDelta(in originAup, runtimePosition);
                if (!IsFiniteAup(in aup))
                    return false;
            }

            pose = new PlayerPose(
                new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                aup,
                NormalizeApproxNoSqrt(snapshot.Forward, new float3(0f, 0f, 1f)));
            return true;
        }

        private static float3 NormalizeApproxNoSqrt(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.0001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            double3 absolute = aup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolute));
        }

        private static float ResolveHallucinationVisualWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            float curve = quality * quality * (3f - 2f * quality);
            return math.saturate((curve - 0.08f) * math.rcp(0.92f));
        }

        private uint NextRandom()
        {
            uint state = _rngState;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            if (state == 0u)
                state = 0xA341316Cu;

            _rngState = state;
            return state;
        }

        private void ApplyDebugState()
        {
            _debugPlayerStress01 = _state.PlayerStress01;
            _debugLightLevel01 = _state.LightLevel01;
            _debugPredatorThreat01 = _state.PredatorThreat01;
            _debugO2DrainMultiplier = _state.O2DrainMultiplier;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct StressSoA
        {
            [FieldOffset(0)]
            public float PlayerStress01;
            [FieldOffset(4)]
            public float LightLevel01;
            [FieldOffset(8)]
            public float PredatorThreat01;
            [FieldOffset(12)]
            public float AcousticImpulse01;
            [FieldOffset(16)]
            public float DamageImpulse01;
            [FieldOffset(20)]
            public float SqueezeImpulse01;
            [FieldOffset(24)]
            public float Recovery01;
            [FieldOffset(28)]
            public float O2DrainMultiplier;
            [FieldOffset(32)]
            public float PeakStress01;
            [FieldOffset(36)]
            public uint PeakStressEvents;
            [FieldOffset(40)]
            public byte PanicAttackEmitted;
            [FieldOffset(41)]
            public byte LastCause;
            [FieldOffset(42)]
            public byte LastFlags;
            [FieldOffset(43)]
            private byte _pad0;
            [FieldOffset(44)]
            private uint _pad1;
        }

        private readonly struct PlayerPose
        {
            public readonly Vector3 RuntimePosition;
            public readonly AbsoluteUniversePosition Aup;
            public readonly float3 Forward;

            public PlayerPose(Vector3 runtimePosition, AbsoluteUniversePosition aup, float3 forward)
            {
                RuntimePosition = runtimePosition;
                Aup = aup;
                Forward = forward;
            }
        }
    }
}
