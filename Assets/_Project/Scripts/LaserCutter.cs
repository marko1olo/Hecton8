// ============================================================================
// HECTON-8 — LaserCutter.cs  v2.0
// Лазерный резак — PlayerTool с термическим менеджментом.
//
// v2.0 CHANGES:
//   [ADD] Heat accumulation system:
//     • _heatLevel (0..1) accumulates while firing, decays when idle.
//     • At _heatLevel >= 1.0: lockout for overheatLockoutTime seconds.
//     • Heat is exposed via HeatLevel property for HUD reading.
//     • Static event OnHeatChanged for reactive systems.
//
//   [ADD] Risk/Reward damage scaling:
//     • Damage increases by up to heatDamageBonus (15%) at max heat.
//     • Cutting is more efficient when hot — but lockout is the cost.
//
//   [ADD] Visual heat feedback:
//     • Beam jitter amplitude scales with _heatLevel.
//     • Spark emission rate scales with _heatLevel.
//     • Cut audio pitch scales from 1.0 to 1.3 with heat.
//
//   [ADD] Overheat lockout via Unity 6 Awaitable:
//     • 2-second forced cooldown on overheat.
//     • Auto-cancelled via destroyCancellationToken on despawn.
//
// PRESERVED FROM v1.0:
//   ✓ Dual mode: Cut (LKM) / Deconstruct (LKM + R)
//   ✓ ICuttable integration with hitPoint (Melt VFX chain)
//   ✓ BaseModule.Deconstruct with progress accumulation
//   ✓ Zero GC in hot path (TryGetComponent, InstanceID caching)
//   ✓ LineRenderer, ParticleSystem, AudioSource management
//   ✓ Lazy PlayerInventory lookup
//
// ZERO GC:
//   • All per-frame math is struct (float, Vector3, int).
//   • No GetComponent in UsePrimary — TryGetComponent + InstanceID cache.
//   • ParticleSystem.EmissionModule is a struct — zero boxing.
//   • Awaitable is pooled by Unity 6 — zero heap allocation.
//   • cutAudio.pitch assignment is a direct native call — zero GC.
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Core;
    using Hecton8.Inventory;
    using Hecton8.Input;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool
    {
        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when heat level changes significantly.
        /// Parameter: normalized heat [0..1].
        /// Subscribers: HUD heat gauge, warning audio system.
        /// Throttled: only fires when delta > 0.02 to avoid spam.
        /// </summary>
        public static event Action<float> OnHeatChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LASER SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Laser Settings ────────────────────────────")]
        [Tooltip("Maximum beam range (meters).")]
        [SerializeField] private float maxRange = 5f;

        [Tooltip("Base damage per second when cutting.")]
        [SerializeField] private float damagePerSecond = 25f;

        [Tooltip("LayerMask for raycast targets.")]
        [SerializeField] private LayerMask cuttableLayer = ~0;

        [Header("── Heat Management ───────────────────────────")]
        [Tooltip("Seconds of continuous firing to reach overheat (heat 0→1).")]
        [SerializeField] private float overheatTime = 5f;

        [Tooltip("Heat units lost per second when NOT firing.\n" +
                 "0.3 = full cooldown from max in ~3.3 seconds.")]
        [SerializeField] private float cooldownRate = 0.3f;

        [Tooltip("Lockout duration after overheat (seconds).\n" +
                 "Tool is completely disabled during this time.")]
        [SerializeField] private float overheatLockoutTime = 2f;

        [Tooltip("Bonus damage multiplier at maximum heat.\n" +
                 "0.15 = 15% more damage when red-hot.\n" +
                 "Risk/reward: more efficient but lockout is the cost.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float heatDamageBonus = 0.15f;

        [Header("── Beam Visual ───────────────────────────────")]
        [Tooltip("Maximum jitter amplitude at full heat (meters).\n" +
                 "Beam endpoint vibrates more as tool heats up.")]
        [SerializeField] private float maxJitterAmplitude = 0.008f;

        [Tooltip("Jitter frequency (Hz). Higher = faster vibration.")]
        [SerializeField] private float jitterFrequency = 50f;

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Seconds of continuous cutting to fully deconstruct a module.\n" +
                 "Progress resets if target changes or R/LKM released.")]
        [SerializeField] private float deconstructThreshold = 3f;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("LineRenderer for beam visualization.")]
        [SerializeField] private LineRenderer laserLine;

        [Tooltip("ParticleSystem for impact sparks.")]
        [SerializeField] private ParticleSystem sparksVFX;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Looping AudioSource for cutting sound.")]
        [SerializeField] private AudioSource cutAudio;

        [Tooltip("Sound played when attempting to fire during overheat lockout.")]
        [SerializeField] private AudioClip overheatErrorClip;

        [Tooltip("Base pitch of cutting audio (at zero heat).")]
        [SerializeField] private float basePitch = 1.0f;

        [Tooltip("Maximum pitch of cutting audio (at full heat).")]
        [SerializeField] private float maxPitch = 1.3f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Raycast result (reused, zero GC).</summary>
        private RaycastHit _hitInfo;

        /// <summary>Is beam active this frame.</summary>
        private bool _isFiring;

        /// <summary>Was beam active last frame (for toggle VFX).</summary>
        private bool _wasFiringLastFrame;

        /// <summary>Cached transform for ray origin/direction.</summary>
        private Transform _cachedTransform;

        // ── Heat State ──

        /// <summary>
        /// Current heat level [0..1].
        /// 0 = cold, 1 = overheated.
        /// Accumulates during firing, decays during idle.
        /// </summary>
        private float _heatLevel;

        /// <summary>Is tool currently in overheat lockout.</summary>
        private bool _isLockedOut;

        /// <summary>Last published heat value (for event throttling).</summary>
        private float _lastPublishedHeat;

        /// <summary>Has the error clip been played this lockout cycle.
        /// Prevents spamming the error sound every frame while locked.</summary>
        private bool _lockoutSoundPlayed;

        // ── Deconstruct State ──

        /// <summary>Accumulated deconstruct progress (seconds).</summary>
        private float _deconstructProgress;

        /// <summary>InstanceID of current deconstruct target (-1 = none).</summary>
        private int _cachedDeconstructTargetId = -1;

        /// <summary>Cached BaseModule of current deconstruct target.</summary>
        private BaseModule _cachedDeconstructModule;

        /// <summary>Cached PlayerInventory for Deconstruct calls.</summary>
        private PlayerInventory _cachedInventory;
        private bool _inventorySearched;

        // ── Sparks cache ──

        /// <summary>Cached emission module (struct, zero GC).</summary>
        private ParticleSystem.EmissionModule _sparksEmission;
        private bool _sparksEmissionCached;

        /// <summary>Base emission rate from prefab (for scaling).</summary>
        private float _baseSparksRate;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current heat level [0..1]. Read by HUD systems.
        /// 0 = cold, 1 = overheated/locked.
        /// </summary>
        public float HeatLevel => _heatLevel;

        /// <summary>Is the tool currently in overheat lockout.</summary>
        public bool IsOverheated => _isLockedOut;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            CacheSparksEmission();
            SetVisualsActive(false);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnEquip()
        {
            base.OnEquip();
            // Don't reset heat on equip — tool remembers its temperature
            // This rewards careful heat management across equip cycles
        }

        public override void OnUnequip()
        {
            _isFiring = false;
            _wasFiringLastFrame = false;
            ResetDeconstructState();
            SetVisualsActive(false);
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Primary action: fire laser beam.
        /// Called every frame while Fire1 is held.
        ///
        /// v2.0: Heat accumulation + lockout + damage scaling.
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            // ── Overheat lockout check ──
            if (_isLockedOut)
            {
                // Play error sound once per lockout cycle
                if (!_lockoutSoundPlayed && overheatErrorClip != null)
                {
                    if (Hecton8.Audio.SpatialAudioManager.Instance != null)
                    {
                        Hecton8.Audio.SpatialAudioManager.Instance.PlayStatic2D(
                            overheatErrorClip, 0.5f);
                    }
                    _lockoutSoundPlayed = true;
                }
                return;
            }

            _isFiring = true;

            // ── Accumulate heat ──
            _heatLevel += deltaTime / math.max(overheatTime, 0.1f);

            // ── Check overheat ──
            if (_heatLevel >= 1f)
            {
                _heatLevel = 1f;
                TriggerOverheatLockout();
                return;
            }

            // ── Raycast ──
            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;

            bool didHit = Physics.Raycast(
                origin, direction, out _hitInfo, maxRange,
                cuttableLayer, QueryTriggerInteraction.Ignore);

            // ── Visuals ──
            UpdateLaserLine(didHit);
            UpdateSparks(didHit);
            UpdateAudioState(true);

            // ── Damage / Deconstruct ──
            if (didHit)
            {
                InputManager inputManager = InputManager.Instance;
                bool deconstructMode = inputManager != null
                    && inputManager.IsSecondaryActionHeld;

                if (deconstructMode)
                {
                    ResetDeconstructState(); // reset cut-mode state
                    ProcessDeconstructMode(deltaTime);
                }
                else
                {
                    ResetDeconstructState();
                    ApplyCutDamage(deltaTime);
                }
            }
            else
            {
                ResetDeconstructState();
            }

            // ── Publish heat ──
            PublishHeat();
        }

        public override void UseSecondary(float deltaTime)
        {
            // Reserved for future: weld mode, focus beam
        }

        /// <summary>
        /// Called every frame regardless of input.
        /// Handles: heat decay, visual shutdown, audio pitch.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            // ── Heat decay when not firing ──
            if (!_isFiring && !_isLockedOut)
            {
                if (_heatLevel > 0f)
                {
                    _heatLevel = math.max(0f, _heatLevel - deltaTime * cooldownRate);
                    PublishHeat();
                }
            }

            // ── Visual shutdown on release ──
            if (_wasFiringLastFrame && !_isFiring)
            {
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;
        }

        // ══════════════════════════════════════════════════════════
        //  HEAT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Triggers overheat lockout. Tool cannot fire for overheatLockoutTime.
        /// Uses Unity 6 Awaitable — pooled, zero GC, auto-cancelled on despawn.
        /// </summary>
        private void TriggerOverheatLockout()
        {
            _isLockedOut = true;
            _lockoutSoundPlayed = false;
            _isFiring = false;
            SetVisualsActive(false);
            ResetDeconstructState();

            // Fire and forget — auto-cancelled if despawned
            _ = OverheatLockoutAsync();
        }

        /// <summary>
        /// Async lockout timer. Awaitable is pooled by Unity 6 runtime.
        /// destroyCancellationToken auto-cancels if GameObject is destroyed.
        /// During lockout: heat decays naturally via ToolTick (but tool can't fire).
        /// </summary>
        private async Awaitable OverheatLockoutAsync()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(
                    overheatLockoutTime, destroyCancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // Despawned during lockout — normal
                return;
            }

            _isLockedOut = false;
            _lockoutSoundPlayed = false;

            // Force heat down to 80% so player has a small buffer
            _heatLevel = math.min(_heatLevel, 0.8f);
            PublishHeat();
        }

        /// <summary>
        /// Publishes heat level via static event.
        /// Throttled: only fires when change exceeds threshold.
        /// </summary>
        private void PublishHeat()
        {
            if (math.abs(_heatLevel - _lastPublishedHeat) > 0.02f)
            {
                _lastPublishedHeat = _heatLevel;
                OnHeatChanged?.Invoke(_heatLevel);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CUT DAMAGE — with heat-scaled bonus
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Applies cut damage to the raycast target.
        ///
        /// v2.0: Damage scales with heat level.
        ///   damageMultiplier = 1.0 + (_heatLevel × heatDamageBonus)
        ///   At heatDamageBonus = 0.15:
        ///     cold (heat=0): 100% damage
        ///     hot  (heat=1): 115% damage
        ///   Risk/reward: staying hot is efficient but lockout looms.
        /// </summary>
        private void ApplyCutDamage(float deltaTime)
        {
            if (_hitInfo.collider == null) return;

            if (_hitInfo.collider.TryGetComponent(out ICuttable cuttable))
            {
                float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
                float damage = damagePerSecond * deltaTime * heatMultiplier;
                cuttable.ApplyCutDamage(damage, _hitInfo.point);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DECONSTRUCT MODE
        // ══════════════════════════════════════════════════════════

        private void ProcessDeconstructMode(float deltaTime)
        {
            if (_hitInfo.collider == null)
            {
                ResetDeconstructState();
                return;
            }

            int targetId = _hitInfo.collider.GetInstanceID();

            // ── Target changed? ──
            if (targetId != _cachedDeconstructTargetId)
            {
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                _cachedDeconstructModule = null;
                _hitInfo.collider.TryGetComponent(out _cachedDeconstructModule);
            }

            // ── No BaseModule → standard cut ──
            if (_cachedDeconstructModule == null)
            {
                ApplyCutDamage(deltaTime);
                return;
            }

            // ── Can't deconstruct → standard cut ──
            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                ApplyCutDamage(deltaTime);
                return;
            }

            // ── Accumulate progress ──
            _deconstructProgress += deltaTime;

            // ── Complete deconstruction ──
            if (_deconstructProgress >= deconstructThreshold)
            {
                EnsurePlayerInventory();
                _cachedDeconstructModule.Deconstruct(_cachedInventory);
                ResetDeconstructState();
            }
        }

        private void ResetDeconstructState()
        {
            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
        }

        private void EnsurePlayerInventory()
        {
            if (_inventorySearched) return;
            _inventorySearched = true;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                player.TryGetComponent(out _cachedInventory);
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS — Beam, Sparks, Audio
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates laser beam LineRenderer.
        /// v2.0: Endpoint jitter scales with heat level.
        ///   Cold: stable beam.
        ///   Hot: visible vibration warns player of impending lockout.
        /// </summary>
        private void UpdateLaserLine(bool didHit)
        {
            if (laserLine == null) return;

            if (!laserLine.enabled)
                laserLine.enabled = true;

            laserLine.SetPosition(0, Vector3.zero);

            if (didHit)
            {
                Vector3 localHitPoint = _cachedTransform.InverseTransformPoint(_hitInfo.point);

                // v2.0: Heat-scaled jitter
                // At heat=0: zero jitter (stable beam)
                // At heat=1: maxJitterAmplitude vibration
                float jitterAmp = _heatLevel * maxJitterAmplitude;
                if (jitterAmp > 0.0001f)
                {
                    float t = Time.time * jitterFrequency;
                    float jx = math.sin(t) * jitterAmp;
                    float jy = math.sin(t * 1.37f + 2.1f) * jitterAmp * 0.7f;
                    // Use irrational multiplier 1.37 to prevent X/Y sync
                    localHitPoint.x += jx;
                    localHitPoint.y += jy;
                }

                laserLine.SetPosition(1, localHitPoint);
            }
            else
            {
                laserLine.SetPosition(1, Vector3.forward * maxRange);
            }
        }

        /// <summary>
        /// Updates spark VFX.
        /// v2.0: Emission rate scales with heat level.
        ///   Cold: base sparks.
        ///   Hot: 4x sparks (1 + heat * 3).
        /// </summary>
        private void UpdateSparks(bool didHit)
        {
            if (sparksVFX == null) return;

            if (didHit)
            {
                Transform sparksTransform = sparksVFX.transform;
                sparksTransform.position = _hitInfo.point;
                sparksTransform.rotation = Quaternion.LookRotation(_hitInfo.normal);

                // v2.0: Scale emission rate with heat
                if (_sparksEmissionCached)
                {
                    float heatScaledRate = _baseSparksRate * (1f + _heatLevel * 3f);
                    _sparksEmission.rateOverTimeMultiplier = heatScaledRate;
                }

                if (!sparksVFX.isPlaying)
                    sparksVFX.Play();
            }
            else
            {
                if (sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Updates audio state and pitch.
        /// v2.0: Pitch rises with heat (1.0 → 1.3).
        ///   Creates audible "strain" warning before overheat.
        ///   Player learns to associate high pitch with danger.
        /// </summary>
        private void UpdateAudioState(bool shouldPlay)
        {
            if (cutAudio == null) return;

            if (shouldPlay)
            {
                if (!cutAudio.isPlaying)
                    cutAudio.Play();

                // v2.0: Heat-scaled pitch
                cutAudio.pitch = math.lerp(basePitch, maxPitch, _heatLevel);
            }
            else
            {
                if (cutAudio.isPlaying)
                    cutAudio.Stop();

                cutAudio.pitch = basePitch;
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (laserLine != null)
                laserLine.enabled = active;

            if (sparksVFX != null)
            {
                if (!active && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Reset emission rate when deactivating
                if (!active && _sparksEmissionCached)
                    _sparksEmission.rateOverTimeMultiplier = _baseSparksRate;
            }

            if (!active)
                UpdateAudioState(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INIT HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Caches ParticleSystem.EmissionModule and base rate.
        /// EmissionModule is a struct — this is safe to store.
        /// </summary>
        private void CacheSparksEmission()
        {
            if (sparksVFX != null)
            {
                _sparksEmission = sparksVFX.emission;
                _baseSparksRate = _sparksEmission.rateOverTimeMultiplier;
                _sparksEmissionCached = true;
            }
        }

        /// <summary>
        /// Full state reset. Called on Spawn/Despawn.
        /// </summary>
        private void ResetAllState()
        {
            _isFiring = false;
            _wasFiringLastFrame = false;
            _heatLevel = 0f;
            _isLockedOut = false;
            _lockoutSoundPlayed = false;
            _lastPublishedHeat = -1f;
            ResetDeconstructState();
        }
    }
}
