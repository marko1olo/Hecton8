using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.Systems.AI;
using Hecton8.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned runtime owner for diegetic PDA intrusion, language hijack cadence, and manual reboot recovery.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Intrusion Manager")]
    public sealed class PDAIntrusionManager : MonoBehaviour, ITickable
    {
        private enum IntrusionVisualPhase : byte
        {
            English = 0,
            Arabic = 1,
            Chinese = 2,
            Glyphs = 3
        }

        private const float HullStressHackThreshold = 0.85f;
        private const float EquipmentGlitchHackThreshold = 0.75f;
        private const float LeviathanCheckInterval = 0.25f;
        private const float LeviathanHackRadius = 54f;
        private const float VisualPhaseDuration = 2f;
        private const float RebootHoldDuration = 3f;
        private const float TextDriftRescanInterval = 0.35f;
        private const float TextDriftAmplitudeMin = 1.5f;
        private const float TextDriftAmplitudeMax = 7.5f;
        private const float TextDriftFrequencyMin = 1.1f;
        private const float TextDriftFrequencyMax = 2.7f;
        private const float HiddenProgressCutoff = 0.0001f;
        private const int MaxBioformContacts = 24;
        private const int MaxDriftTargets = 96;

        private static PDAIntrusionManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnRebootCompleted = null;
        }

        [Header("── Intrusion Thresholds ──────────────────")]
        [Tooltip("Minimum director glitch intensity required before the intrusion owner treats the event as a hostile EMI strike.")]
        [SerializeField, Range(0f, 1f)] private float equipmentGlitchThreshold = EquipmentGlitchHackThreshold;

        [Tooltip("How often to scan the spatial grid for leviathan proximity while the PDA remains nominal.")]
        [SerializeField, Min(0.05f)] private float leviathanScanInterval = LeviathanCheckInterval;

        [Tooltip("Leviathan proximity radius that forces the PDA into hacked state.")]
        [SerializeField, Min(8f)] private float leviathanHackRadius = LeviathanHackRadius;

        [Tooltip("Cadence between visual language swaps during intrusion.")]
        [SerializeField, Min(0.1f)] private float visualPhaseDuration = VisualPhaseDuration;

        [Tooltip("How long the player must hold the reboot action while the PDA is open.")]
        [SerializeField, Min(0.5f)] private float rebootHoldDuration = RebootHoldDuration;

        // COLD ALLOC: SpatialQueryHit[24] — cached bioform proximity buffer for intrusion scans — owner: PDAIntrusionManager
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
        // COLD ALLOC: List<TextMeshProUGUI>[96] — reusable hacked-text scan buffer for PDA drift — owner: PDAIntrusionManager
        private readonly System.Collections.Generic.List<TextMeshProUGUI> _driftScanBuffer = new System.Collections.Generic.List<TextMeshProUGUI>(MaxDriftTargets);
        // COLD ALLOC: TextMeshProUGUI[96] — cached PDA text targets for hacked-line drift — owner: PDAIntrusionManager
        private readonly TextMeshProUGUI[] _driftTargets = new TextMeshProUGUI[MaxDriftTargets];
        // COLD ALLOC: RectTransform[96] — cached rect owners for hacked-line drift — owner: PDAIntrusionManager
        private readonly RectTransform[] _driftRects = new RectTransform[MaxDriftTargets];
        // COLD ALLOC: Vector2[96] — cached pre-hack anchored positions for text drift restore — owner: PDAIntrusionManager
        private readonly Vector2[] _driftBaseAnchoredPositions = new Vector2[MaxDriftTargets];
        // COLD ALLOC: float[96] — deterministic phase offsets for hacked-line drift — owner: PDAIntrusionManager
        private readonly float[] _driftPhaseOffsets = new float[MaxDriftTargets];

        private PlayerPDA _playerPda;
        private HectonPlayerMovement _playerMovement;
        private InputManager _inputManager;
        private InputAction _submitAction;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private GameObject _driftPanelRoot;
        private bool _registeredToTick;
        private bool _isHacked;
        private float _leviathanScanTimer;
        private float _visualPhaseTimer;
        private float _rebootHoldTimer;
        private float _textDriftRescanTimer;
        private float _textDriftWaveTime;
        private int _driftTargetCount;
        private IntrusionVisualPhase _visualPhase;

        /// <summary>
        /// Active runtime intrusion owner attached to the current player.
        /// </summary>
        public static PDAIntrusionManager ActiveRuntimeInstance => _instance;

        /// <summary>
        /// Fired once when the hacked PDA completes a manual reboot and clears intrusion state.
        /// </summary>
        public static event System.Action OnRebootCompleted;

        /// <summary>
        /// True when the PDA is currently hijacked and the player must manually reboot it.
        /// </summary>
        public bool IsHacked => _isHacked;

        /// <summary>
        /// Hold progress for the manual reboot action in normalized [0..1] range.
        /// </summary>
        public float RebootProgressNormalized =>
            rebootHoldDuration > 0.001f
                ? Mathf.Clamp01(_rebootHoldTimer / rebootHoldDuration)
                : 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            ResolveOwners();
        }

        private void OnEnable()
        {
            ResolveOwners();
            RegisterToTickManager();
            HectonDirectorAI.OnRequestEquipmentGlitch += HandleEquipmentGlitchRequested;
        }

        private void Start()
        {
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            HectonDirectorAI.OnRequestEquipmentGlitch -= HandleEquipmentGlitchRequested;
            UnregisterFromTickManager();
            ClearVisualOverride();
            ResetTransientState();
        }

        private void OnDestroy()
        {
            HectonDirectorAI.OnRequestEquipmentGlitch -= HandleEquipmentGlitchRequested;
            UnregisterFromTickManager();

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ResolveOwners();

            if (!_isHacked)
            {
                RestoreTextDriftPositions();
                TickAmbientIntrusionThreat(dt);
                return;
            }

            TickVisualCadence(dt);
            TickTextDrift(dt);
            TickRebootHold(dt);
        }

        private void HandleEquipmentGlitchRequested(float intensity)
        {
            if (intensity < equipmentGlitchThreshold)
                return;

            TriggerHack();
        }

        private void TickAmbientIntrusionThreat(float dt)
        {
            _leviathanScanTimer -= dt;
            if (_leviathanScanTimer > 0f)
                return;

            _leviathanScanTimer = Mathf.Max(0.05f, leviathanScanInterval);

            if (ShouldTriggerAbyssalHack())
            {
                TriggerHack();
                return;
            }

            Vector3 origin = transform.position;
            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                Mathf.Max(8f, leviathanHackRadius),
                SpatialTargetKind.Bioform,
                _bioformContacts);

            for (int i = 0; i < contactCount; i++)
            {
                Component owner = _bioformContacts[i].Owner;
                FaunaBrain brain = owner as FaunaBrain;
                if (brain == null || brain.IsDead)
                    continue;

                FaunaSpeciesProfile speciesProfile = brain.SpeciesProfile;
                if (speciesProfile == null || !speciesProfile.isLeviathan)
                    continue;

                TriggerHack();
                return;
            }
        }

        private bool ShouldTriggerAbyssalHack()
        {
            if (_playerMovement != null && _playerMovement.CurrentHullStress01 > HullStressHackThreshold)
                return true;

            return IsInsideDeadZone();
        }

        private bool IsInsideDeadZone()
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridge;
            if (bridge == null)
                return false;

            HectonMapMagicVegetationBridge.VegetationDensitySample densitySample = bridge.GetVegetationDensity(transform.position);
            return densitySample.BiomeLayer == HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone;
        }

        private void TickVisualCadence(float dt)
        {
            _visualPhaseTimer -= dt;
            if (_visualPhaseTimer > 0f)
                return;

            _visualPhaseTimer = Mathf.Max(0.1f, visualPhaseDuration);
            _visualPhase = NextVisualPhase(_visualPhase);
            ApplyVisualPhase();
        }

        private void TickRebootHold(float dt)
        {
            if (!CanAcceptRebootHold())
            {
                if (_rebootHoldTimer > HiddenProgressCutoff)
                    _rebootHoldTimer = 0f;
                return;
            }

            if (!IsRebootActionHeld())
            {
                if (_rebootHoldTimer > HiddenProgressCutoff)
                    _rebootHoldTimer = 0f;
                return;
            }

            _rebootHoldTimer += dt;
            if (_rebootHoldTimer < rebootHoldDuration)
                return;

            CompleteReboot();
        }

        private void TickTextDrift(float dt)
        {
            if (_playerPda == null || !PlayerPDA.IsOpen)
            {
                RestoreTextDriftPositions();
                return;
            }

            GameObject panelRoot = _playerPda.PanelRoot;
            if (panelRoot == null || !panelRoot.activeInHierarchy)
            {
                RestoreTextDriftPositions();
                return;
            }

            _textDriftRescanTimer -= dt;
            if (!ReferenceEquals(_driftPanelRoot, panelRoot) || _driftTargetCount == 0 || _textDriftRescanTimer <= 0f)
                RebuildTextDriftTargets(panelRoot);

            if (_driftTargetCount == 0)
                return;

            _textDriftWaveTime += dt;
            float glyphScale = _visualPhase == IntrusionVisualPhase.Glyphs ? 1.22f : 1f;
            for (int i = 0; i < _driftTargetCount; i++)
            {
                RectTransform rect = _driftRects[i];
                if (rect == null)
                    continue;

                float normalizedIndex = _driftTargetCount > 1
                    ? (float)i / (_driftTargetCount - 1)
                    : 0f;
                float amplitude = Mathf.Lerp(TextDriftAmplitudeMin, TextDriftAmplitudeMax, normalizedIndex) * glyphScale;
                float frequency = Mathf.Lerp(TextDriftFrequencyMin, TextDriftFrequencyMax, 1f - normalizedIndex);
                float offsetX = Mathf.Sin((_textDriftWaveTime * frequency) + _driftPhaseOffsets[i]) * amplitude;
                Vector2 basePosition = _driftBaseAnchoredPositions[i];
                rect.anchoredPosition = new Vector2(basePosition.x + offsetX, basePosition.y);
            }
        }

        private void RebuildTextDriftTargets(GameObject panelRoot)
        {
            RestoreTextDriftPositions();
            _driftPanelRoot = panelRoot;
            _driftTargetCount = 0;
            _textDriftRescanTimer = Mathf.Max(0.1f, TextDriftRescanInterval);
            _driftScanBuffer.Clear();
            panelRoot.GetComponentsInChildren(true, _driftScanBuffer);

            int candidateCount = _driftScanBuffer.Count;
            for (int i = 0; i < candidateCount && _driftTargetCount < MaxDriftTargets; i++)
            {
                TextMeshProUGUI text = _driftScanBuffer[i];
                if (text == null || !text.enabled)
                    continue;

                RectTransform rect = text.rectTransform;
                if (rect == null)
                    continue;

                int slot = _driftTargetCount;
                _driftTargets[slot] = text;
                _driftRects[slot] = rect;
                _driftBaseAnchoredPositions[slot] = rect.anchoredPosition;
                _driftPhaseOffsets[slot] = (slot * 0.73f) + (text.fontSize * 0.013f);
                _driftTargetCount++;
            }
        }

        private void RestoreTextDriftPositions()
        {
            if (_driftTargetCount <= 0)
                return;

            for (int i = 0; i < _driftTargetCount; i++)
            {
                RectTransform rect = _driftRects[i];
                if (rect != null)
                    rect.anchoredPosition = _driftBaseAnchoredPositions[i];

                _driftTargets[i] = null;
                _driftRects[i] = null;
                _driftPhaseOffsets[i] = 0f;
            }

            _driftTargetCount = 0;
            _driftPanelRoot = null;
        }

        private void TriggerHack()
        {
            if (_isHacked)
                return;

            _isHacked = true;
            _rebootHoldTimer = 0f;
            _visualPhase = IntrusionVisualPhase.English;
            _visualPhaseTimer = Mathf.Max(0.1f, visualPhaseDuration);
            ApplyVisualPhase();
        }

        private void CompleteReboot()
        {
            RestoreTextDriftPositions();
            ClearVisualOverride();
            ResetTransientState();
            OnRebootCompleted?.Invoke();
        }

        private void ApplyVisualPhase()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return;

            switch (_visualPhase)
            {
                case IntrusionVisualPhase.Arabic:
                    manager.SetTransientLanguageOverride(GameLanguage.Arabic);
                    break;

                case IntrusionVisualPhase.Chinese:
                    manager.SetTransientLanguageOverride(GameLanguage.ChineseSimplified);
                    break;

                case IntrusionVisualPhase.Glyphs:
                    manager.SetTransientLanguageOverride(GameLanguage.ChineseSimplified, enableGlyphMode: true);
                    break;

                default:
                    manager.SetTransientLanguageOverride(GameLanguage.English);
                    break;
            }
        }

        private void ClearVisualOverride()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager != null)
                manager.ClearTransientLanguageOverride();
        }

        private void ResetTransientState()
        {
            RestoreTextDriftPositions();
            _isHacked = false;
            _leviathanScanTimer = 0f;
            _visualPhaseTimer = 0f;
            _rebootHoldTimer = 0f;
            _textDriftRescanTimer = 0f;
            _textDriftWaveTime = 0f;
            _visualPhase = IntrusionVisualPhase.English;
        }

        private bool CanAcceptRebootHold()
        {
            return _isHacked &&
                   _playerPda != null &&
                   PlayerPDA.IsOpen &&
                   _submitAction != null;
        }

        private bool IsRebootActionHeld()
        {
            return _submitAction != null && _submitAction.IsPressed();
        }

        private void ResolveOwners()
        {
            if (_playerPda == null)
            {
                if (!TryGetComponent(out _playerPda))
                    _playerPda = GetComponentInChildren<PlayerPDA>(true);
            }

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            InputManager inputManager = InputManager.Instance;
            if (ReferenceEquals(_inputManager, inputManager))
                return;

            _inputManager = inputManager;
            _submitAction = _inputManager != null
                ? _inputManager.UiSubmitAction
                : null;
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTick = false;
        }

        private static IntrusionVisualPhase NextVisualPhase(IntrusionVisualPhase current)
        {
            switch (current)
            {
                case IntrusionVisualPhase.English:
                    return IntrusionVisualPhase.Arabic;

                case IntrusionVisualPhase.Arabic:
                    return IntrusionVisualPhase.Chinese;

                case IntrusionVisualPhase.Chinese:
                    return IntrusionVisualPhase.Glyphs;

                default:
                    return IntrusionVisualPhase.English;
            }
        }
    }
}
