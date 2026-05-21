// ============================================================================
// HECTON-8 — PlayerFootstepAudio.cs
// Plays randomized footstep sounds synchronized with head bobbing step events.
//
// v2.0 — HYBRID SURFACE DETECTION:
//
//   [NEW] Dual detection system for procedural + handcrafted worlds:
//
//     TERRAIN PATH (MapMagic 2):
//       KCC batched footstep hit lands on Terrain layer (or tagged "Terrain").
//       → MapMagicBridge.TryGetBiomeIndex(hitPoint) → biome index.
//       → Match against SurfaceSoundSet.mapMagicBiomeIndex.
//       This handles procedural ground where all terrain shares one tag.
//
//     OBJECT PATH (Base Modules, Props):
//       KCC batched footstep hit lands on object NOT on Terrain layer.
//       → CompareTag against SurfaceSoundSet.surfaceTag.
//       This handles handcrafted objects with explicit tags.
//
//     FALLBACK:
//       No match in either path → defaultFootstepClips.
//
//   [PRESERVED] Zero GC in event handler. No LINQ. No allocations.
//   [PRESERVED] Pitch randomization, speed-based volume, step cooldown.
//   [PRESERVED] Anti-repeat clip selection.
//
// SETUP:
//   1. Set your Terrain objects to Layer "Terrain" (or tag them "Terrain").
//   2. In Inspector, add SurfaceSoundSet entries:
//      - For MapMagic biomes: set mapMagicBiomeIndex (0=sand, 1=rock, etc.)
//      - For base modules: set surfaceTag ("MetalFloor", "Grate", etc.)
//      - Each entry can have BOTH fields set (matched independently).
//   3. Assign audio clips to each entry.
//   4. Set defaultFootstepClips as fallback.
//   5. Na stsene dolzhen byt SpatialAudioManager (Bootstrap) — shagi idut v ego 3D-pul.
//
// INSPECTOR EXAMPLE:
//   Element 0: biomeIndex=0, tag="",          clips=[sand_01..04]  ← MapMagic sand biome
//   Element 1: biomeIndex=1, tag="",          clips=[rock_01..04]  ← MapMagic rock biome
//   Element 2: biomeIndex=2, tag="",          clips=[mud_01..03]   ← MapMagic swamp biome
//   Element 3: biomeIndex=-1, tag="MetalFloor", clips=[metal_01..04] ← base module floors
//   Element 4: biomeIndex=-1, tag="Grate",      clips=[grate_01..03] ← walkway grates
//   Element 5: biomeIndex=-1, tag="WetFloor",   clips=[wet_01..03]   ← flooded compartments
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Maps a surface (by tag OR MapMagic biome index) to footstep AudioClips.
    ///
    /// MATCHING RULES:
    ///   • mapMagicBiomeIndex >= 0: used when batched footstep hit lands on Terrain layer.
    ///   • surfaceTag not empty: used when batched footstep hit lands on non-Terrain object.
    ///   • Both can be set on same entry (matched independently by path).
    ///   • mapMagicBiomeIndex = -1 means "ignore biome matching for this entry".
    /// </summary>
    [System.Serializable]
    public struct SurfaceSoundSet
    {
        [Tooltip("Human-readable name for this surface (editor convenience only).")]
        public string label;

        [Tooltip("MapMagic biome index for terrain surfaces.\n" +
                 "Matches the splat layer index from MapMagic Biomes Set node.\n" +
                 "-1 = this entry does NOT match terrain biomes.\n" +
                 "0+ = matches specific biome index.")]
        public int mapMagicBiomeIndex;

        [Tooltip("GameObject tag for non-terrain surfaces (base modules, props).\n" +
                 "Empty = this entry does NOT match by tag.\n" +
                 "Used when the batched footstep hit lands on objects NOT on the Terrain layer.")]
        public string surfaceTag;

        [Tooltip("Footstep clips for this surface. 3-6 variations recommended.")]
        public AudioClip[] clips;

        [Tooltip("Volume multiplier for this surface.\n" +
                 "Metal = louder (1.0-1.2). Sand = quieter (0.5-0.7).")]
        [Range(0.1f, 2f)]
        public float volumeMultiplier;
    }

    [DisallowMultipleComponent]
    public sealed class PlayerFootstepAudio : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Reference to the player movement script. Assign in Inspector.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Header("── Default Footsteps ─────────────────────────")]
        [Tooltip("Fallback footstep clips when no surface match is found.\n" +
                 "Also used if the batched surface hit misses entirely.")]
        [SerializeField] private AudioClip[] defaultFootstepClips;

        [Header("── Surface-Specific Footsteps ────────────────")]
        [Tooltip("Map surfaces to specific footstep sound sets.\n" +
                 "Each entry can match by MapMagic biome index, tag, or both.")]
        [SerializeField] private SurfaceSoundSet[] surfaceSounds;

        [Header("── Audio Settings ────────────────────────────")]
        [Tooltip("Base volume for footstep playback.")]
        [SerializeField, Range(0f, 1f)]
        private float baseVolume = 0.7f;

        [Tooltip("Random pitch variation range (±).\n" +
                 "0.05 = ±5% pitch shift. Adds organic feel.")]
        [SerializeField, Range(0f, 0.2f)]
        private float pitchVariation = 0.05f;

        [Tooltip("Minimum time between footstep sounds (seconds).\n" +
                 "Prevents rapid-fire sounds on physics glitches.")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float minStepInterval = 0.15f;

        [Header("── Surface Detection ─────────────────────────")]
        [Tooltip("Enable batched-KCC-hit surface detection.\n" +
                 "When off, always uses default clips.")]
        [SerializeField] private bool enableSurfaceDetection = true;

        [Tooltip("Maximum accepted distance for cached batched surface detection.")]
        [SerializeField, Range(0.5f, 3f)]
        private float surfaceRayDistance = 1.5f;

        [Tooltip("Layers accepted from the batched KCC surface hit.")]
        [SerializeField] private LayerMask surfaceLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Layer index for Terrain objects.\n" +
                 "Objects on this layer trigger MapMagic biome lookup.\n" +
                 "Objects on OTHER layers trigger tag-based lookup.\n" +
                 "Default Unity Terrain layer is usually index 6 or custom.\n" +
                 "Set this to match your Terrain layer in Layer settings.")]
        [SerializeField] private int terrainLayerIndex = 6;

        [Header("── Speed-Based Volume ────────────────────────")]
        [Tooltip("Scale footstep volume by player horizontal speed.\n" +
                 "Slow walk = quieter, fast walk = louder.")]
        [SerializeField] private bool scaleVolumeBySpeed = true;

        [Tooltip("Minimum volume multiplier at zero speed.")]
        [SerializeField, Range(0.1f, 1f)]
        private float minSpeedVolumeScale = 0.4f;

        [Header("── Locomotion Mode Mix ──────────────────────")]
        [Tooltip("Volume multiplier for dry interior footsteps. Interiors should feel tighter and calmer than exterior walk.")]
        [SerializeField, Range(0.1f, 1f)]
        private float dryInteriorVolumeMultiplier = 0.88f;

        [Tooltip("Volume multiplier for shallow wading footsteps. Water should soften the step impact.")]
        [SerializeField, Range(0.1f, 1f)]
        private float shallowWadeVolumeMultiplier = 0.7f;

        [Tooltip("Pitch offset applied while shallow wading. Slightly lower pitch sells weight in water.")]
        [SerializeField, Range(-0.3f, 0.3f)]
        private float shallowWadePitchOffset = -0.04f;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private string _debugLastSurface = "none";
        [SerializeField] private int _debugLastBiomeIndex = -1;
        [SerializeField] private bool _debugUsedBiomePath;

        // ══════════════════════════════════════════════════════════
        //  CACHED
        // ══════════════════════════════════════════════════════════

        private Rigidbody _playerRb;
        private float _lastStepTime;
        private RaycastHit _surfaceHit;
        private bool _surfaceHitValid;
        private int _lastClipIndex = -1;
        private uint _footstepRandomState;
        private uint _lastConsumedFootstepSignalFrame;
        private IAudioService _audioService;
        private MapMagicBridge _mapMagic;
        private bool _registeredUpdate;
        private bool _registeredHotSwapListener;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Application.isPlaying && playerMovement != null && !_registeredUpdate)
            {
                _playerRb = playerMovement.GetComponent<Rigidbody>();
            }

            uint entitySeed = unchecked((uint)EntityId.ToULong(GetEntityId()));
            _footstepRandomState = entitySeed != 0u ? entitySeed : 0xA511E9B3u;
            _lastStepTime = -1f;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            RefreshColdRegistryReferences();

            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void OnDisable()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredUpdate = false;
            }

            TryUnregisterHotSwapListener();
            _audioService = null;
            _mapMagic = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                _audioService = currentService as IAudioService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicRuntime)
            {
                _mapMagic = currentService as MapMagicBridge;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled && !_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLER — Zero GC
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            ReadOnlySpan<PlayerFootstepSignal> signals = SignalBus<PlayerFootstepSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerFootstepSignal signal = signals[i];
                if (signal.Frame == _lastConsumedFootstepSignalFrame)
                    continue;

                _lastConsumedFootstepSignalFrame = signal.Frame;
                HandleFootstep();
            }
        }

        private void HandleFootstep()
        {
            // ── Guard: only play when walking on ground ──
            if (playerMovement == null) return;
            if (!playerMovement.IsGrounded) return;

            PlayerLocomotionMode locomotionMode = playerMovement.CurrentLocomotionMode;
            if (locomotionMode == PlayerLocomotionMode.SurfaceSwim ||
                locomotionMode == PlayerLocomotionMode.UnderwaterSwim)
                return;

            // ── Cooldown ──
            float currentTime = Time.time;
            if (currentTime - _lastStepTime < minStepInterval) return;
            _lastStepTime = currentTime;

            // ── Select clip array based on surface ──
            AudioClip[] clips = defaultFootstepClips;
            float surfaceVolumeMult = 1f;
            _surfaceHitValid = false;

            if (enableSurfaceDetection
                && surfaceSounds != null
                && surfaceSounds.Length > 0)
            {
                DetectSurface(ref clips, ref surfaceVolumeMult);
            }

            // ── Guard: no clips ──
            if (clips == null || clips.Length == 0) return;

            // ── Select random clip (avoid immediate repeat) ──
            int clipIndex;
            if (clips.Length == 1)
            {
                clipIndex = 0;
            }
            else
            {
                int attempts = 0;
                do
                {
                    clipIndex = NextFootstepIndex(clips.Length);
                    attempts++;
                }
                while (clipIndex == _lastClipIndex && attempts < 4);
            }

            _lastClipIndex = clipIndex;

            AudioClip clip = clips[clipIndex];
            if (clip == null) return;

            // ── Volume: base × surface × speed ──
            float locomotionVolumeMultiplier = 1f;
            float locomotionPitchOffset = 0f;

            switch (locomotionMode)
            {
                case PlayerLocomotionMode.DryInteriorWalk:
                    locomotionVolumeMultiplier = dryInteriorVolumeMultiplier;
                    break;

                case PlayerLocomotionMode.ShallowWadeWalk:
                    locomotionVolumeMultiplier = shallowWadeVolumeMultiplier;
                    locomotionPitchOffset = shallowWadePitchOffset;
                    break;
            }

            float finalVolume = baseVolume * surfaceVolumeMult * locomotionVolumeMultiplier;

            if (scaleVolumeBySpeed && _playerRb != null)
            {
                Vector3 vel = _playerRb.linearVelocity;
                float hSpeed = ApproximatePlanarMagnitude(vel.x, vel.z);
                float maxSpeed = playerMovement.CurrentSuit != null
                    ? playerMovement.CurrentSuit.maxWalkSpeed
                    : 6f;
                float speedFactor = maxSpeed > 0f
                    ? math.clamp(hSpeed * math.rcp(maxSpeed), 0f, 1f)
                    : 1f;
                float speedVolume = math.lerp(minSpeedVolumeScale, 1f, speedFactor);
                finalVolume *= speedVolume;
            }

            float pitch = 1f + locomotionPitchOffset + NextFootstepRange(-pitchVariation, pitchVariation);
            Vector3 playPosition = _surfaceHitValid ? _surfaceHit.point : transform.position;

            IAudioService sam = _audioService;
            if (sam != null)
                sam.PlayAtPoint(clip, playPosition, finalVolume, pitch);
        }

        private int NextFootstepIndex(int exclusiveMax)
        {
            if (exclusiveMax <= 1)
                return 0;

            return (int)(NextFootstepRandomUInt() % (uint)exclusiveMax);
        }

        private float NextFootstepRange(float min, float max)
        {
            uint value = NextFootstepRandomUInt() >> 8;
            float t = value * (1f / 16777215f);
            return math.lerp(min, max, t);
        }

        private uint NextFootstepRandomUInt()
        {
            _footstepRandomState = unchecked(_footstepRandomState * 1664525u + 1013904223u);
            return _footstepRandomState;
        }

        private static float ApproximatePlanarMagnitude(float x, float z)
        {
            float ax = math.abs(x);
            float az = math.abs(z);
            float max = math.max(ax, az);
            float min = math.min(ax, az);
            return max + (0.375f * min);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — HYBRID SURFACE DETECTION (v2.0)
        //
        //  Decision tree:
        //
        //  Batched ground hit
        //    │
        //    ├── Miss → return (use defaults)
        //    │
        //    └── Hit
        //         │
        //         ├── Hit object layer == terrainLayerIndex?
        //         │     │
        //         │     YES → BIOME PATH
        //         │     │   MapMagicBridge.TryGetBiomeIndex(hit.point)
        //         │     │   → scan surfaceSounds for matching biomeIndex
        //         │     │   → found? use those clips
        //         │     │   → not found? use defaults
        //         │     │
        //         │     NO → TAG PATH
        //         │         CompareTag against surfaceSounds[].surfaceTag
        //         │         → found? use those clips
        //         │         → not found? use defaults
        //
        //  ZERO GC:
        //    • CompareTag: uses internal string interning (no alloc)
        //    • TryGetBiomeIndex: returns int (struct), zero GC internally
        //    • Linear scan of surfaceSounds: array index, no alloc
        //    • RaycastHit: cached as _surfaceHit field
        //    • No LINQ, no lambda, no temporary arrays
        // ══════════════════════════════════════════════════════════

        private void DetectSurface(ref AudioClip[] clips, ref float volumeMult)
        {
            _surfaceHitValid = false;

            // Reuse the movement controller's previous-frame batched ground hit.
            if (!TryGetSurfaceHit(out _surfaceHit))
            {
                UpdateSurfaceDiagnostics("batched miss", -1, false);
                return;
            }

            _surfaceHitValid = true;

            GameObject hitObj = _surfaceHit.collider.gameObject;
            int hitLayer = hitObj.layer;

            // ══════════════════════════════════════════════
            //  PATH A: TERRAIN — MapMagic biome lookup
            //
            //  The hit object is on the Terrain layer.
            //  This means it's a MapMagic-generated terrain tile.
            //  All such tiles share the same tag ("Terrain" or "Untagged")
            //  so tag-based detection is useless here.
            //
            //  Instead, we ask MapMagicBridge for the dominant biome
            //  at the hit point's world XZ coordinates.
            //  The biome index corresponds to the splat layer index
            //  from the MapMagic Biomes Set node.
            //
            //  We then scan surfaceSounds[] for an entry whose
            //  mapMagicBiomeIndex matches.
            // ══════════════════════════════════════════════

            if (hitLayer == terrainLayerIndex)
            {
                MapMagicBridge bridge = _mapMagic;

                if (bridge != null
                    && bridge.TryGetBiomeIndex(
                        _surfaceHit.point.x,
                        _surfaceHit.point.z,
                        out int biomeIndex))
                {
                    // ── Scan for biome index match ──
                    for (int i = 0; i < surfaceSounds.Length; i++)
                    {
                        ref SurfaceSoundSet set = ref surfaceSounds[i];

                        // Skip entries that don't participate in biome matching
                        if (set.mapMagicBiomeIndex < 0) continue;

                        if (set.mapMagicBiomeIndex == biomeIndex)
                        {
                            if (set.clips != null && set.clips.Length > 0)
                            {
                                clips = set.clips;
                                volumeMult = set.volumeMultiplier;

                                UpdateSurfaceDiagnostics(
                                    set.label, biomeIndex, true);
                                return;
                            }
                        }
                    }

                    // No matching biome entry found — fall through to defaults
                    UpdateSurfaceDiagnostics("biome unmatched", biomeIndex, true);
                }
                else
                {
                    // MapMagicBridge unavailable or no biome data
                    UpdateSurfaceDiagnostics("bridge unavailable", -1, true);
                }

                return;
            }

            // ══════════════════════════════════════════════
            //  PATH B: TAGGED OBJECT — CompareTag lookup
            //
            //  The hit object is NOT on the Terrain layer.
            //  This means it's a handcrafted object: base module,
            //  walkway, prop, etc. These use explicit tags.
            //
            //  Scan surfaceSounds[] for an entry whose surfaceTag
            //  matches via CompareTag (zero GC).
            // ══════════════════════════════════════════════

            for (int i = 0; i < surfaceSounds.Length; i++)
            {
                ref SurfaceSoundSet set = ref surfaceSounds[i];

                // Skip entries that don't participate in tag matching
                if (string.IsNullOrEmpty(set.surfaceTag)) continue;
                if (set.clips == null || set.clips.Length == 0) continue;

                if (hitObj.CompareTag(set.surfaceTag))
                {
                    clips = set.clips;
                    volumeMult = set.volumeMultiplier;

                    UpdateSurfaceDiagnostics(set.label, -1, false);
                    return;
                }
            }

            // No tag match — defaults remain
            UpdateSurfaceDiagnostics("tag unmatched", -1, false);
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        private bool TryGetSurfaceHit(out RaycastHit hit)
        {
            if (playerMovement != null &&
                playerMovement.TryGetRecentFootstepSurfaceHit(surfaceRayDistance, surfaceLayers, out hit))
            {
                return true;
            }

            hit = default;
            return false;
        }

        private void RefreshColdRegistryReferences()
        {
            _audioService = GlobalRegistry.Audio;
            _mapMagic = GlobalRegistry.MapMagic;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSurfaceDiagnostics(
            string surfaceName, int biomeIndex, bool usedBiomePath)
        {
            _debugLastSurface = surfaceName;
            _debugLastBiomeIndex = biomeIndex;
            _debugUsedBiomePath = usedBiomePath;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (minStepInterval < 0.01f) minStepInterval = 0.01f;
            if (terrainLayerIndex < 0) terrainLayerIndex = 0;
            if (terrainLayerIndex > 31) terrainLayerIndex = 31;

            // Auto-initialize new SurfaceSoundSet entries with sane defaults
            if (surfaceSounds != null)
            {
                for (int i = 0; i < surfaceSounds.Length; i++)
                {
                    if (surfaceSounds[i].volumeMultiplier < 0.01f)
                    {
                        surfaceSounds[i].volumeMultiplier = 1f;
                    }

                    // Default biome index to -1 (disabled) if not set
                    // This only triggers on brand-new entries where everything is 0
                    // We check clips==null as proxy for "newly added entry"
                    if (surfaceSounds[i].clips == null
                        && surfaceSounds[i].mapMagicBiomeIndex == 0
                        && string.IsNullOrEmpty(surfaceSounds[i].surfaceTag))
                    {
                        surfaceSounds[i].mapMagicBiomeIndex = -1;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (!enableSurfaceDetection) return;

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Vector3 end = origin + Vector3.down * surfaceRayDistance;

            // Draw detection ray
            Gizmos.color = _debugUsedBiomePath
                ? new Color(0.2f, 0.8f, 0.2f, 0.7f)   // green = biome path
                : new Color(0.2f, 0.5f, 1f, 0.7f);     // blue = tag path

            Gizmos.DrawLine(origin, end);

            // Draw hit point
            if (_surfaceHit.collider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_surfaceHit.point, 0.1f);
            }
        }
#endif
    }
}
