// ============================================================================
// HECTON-8 - HectonLoreSystemsRoot.cs
// Root component for all lore systems.
//
// Purpose:
//   Single bootstrap point for lore-related runtime systems.
//   Intended for a GameObject named "LoreSystems" in 02_HECTON_WORLD.
// ============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Bootstrap
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Bootstrap/Hecton Lore Systems Root")]
    public sealed class HectonLoreSystemsRoot : MonoBehaviour
    {
        public const int ExpectedSystemCount = 16;

        private enum RecoveryLoreKind
        {
            NarrativeDiscovery,
            AudioLogPickup
        }

        private readonly struct RecoveryLorePlacement
        {
            public readonly RecoveryLoreKind Kind;
            public readonly string HostPath;
            public readonly string MarkerName;
            public readonly string LogId;
            public readonly string FallbackDisplayName;
            public readonly Vector3 LocalPosition;
            public readonly Vector3 ColliderSize;

            public RecoveryLorePlacement(
                RecoveryLoreKind kind,
                string hostPath,
                string markerName,
                string logId,
                string fallbackDisplayName,
                Vector3 localPosition,
                Vector3 colliderSize)
            {
                Kind = kind;
                HostPath = hostPath;
                MarkerName = markerName;
                LogId = logId;
                FallbackDisplayName = fallbackDisplayName;
                LocalPosition = localPosition;
                ColliderSize = colliderSize;
            }
        }

        // COLD ALLOC: RecoveryLorePlacement[5] — runtime lore fail-safe descriptors for zero-placement world state — owner: HectonLoreSystemsRoot
        private static readonly RecoveryLorePlacement[] _runtimeRecoveryPlacements =
        {
            new RecoveryLorePlacement(
                RecoveryLoreKind.NarrativeDiscovery,
                "--- WORLD ---/Resource_FieldSources",
                "Lore_ChenDatapad01",
                "chen_m_datapad_01",
                "Chen Datapad",
                new Vector3(0.55f, 0.35f, 0.60f),
                new Vector3(0.45f, 0.25f, 0.45f)),
            new RecoveryLorePlacement(
                RecoveryLoreKind.NarrativeDiscovery,
                "--- WORLD ---/Resource_FieldSources",
                "Lore_BiologistSamples",
                "biologist_samples",
                "Biologist Samples",
                new Vector3(-6.60f, 0.35f, 7.40f),
                new Vector3(0.55f, 0.35f, 0.55f)),
            new RecoveryLorePlacement(
                RecoveryLoreKind.NarrativeDiscovery,
                "--- WORLD ---/Resource_FieldSources",
                "Lore_MedicDiary",
                "medic_diary",
                "Medic Diary",
                new Vector3(9.40f, 0.35f, 9.10f),
                new Vector3(0.55f, 0.35f, 0.55f)),
            new RecoveryLorePlacement(
                RecoveryLoreKind.AudioLogPickup,
                "--- WORLD ---/Fabrication_Outpost",
                "Lore_CaptainBroadcastTerminal",
                "captain_last_broadcast",
                "Captain Broadcast",
                new Vector3(1794.03943f, 4901.20f, 798.74396f),
                new Vector3(0.35f, 0.45f, 0.35f)),
            new RecoveryLorePlacement(
                RecoveryLoreKind.AudioLogPickup,
                "--- WORLD ---/Fabrication_Outpost",
                "Lore_Atlas6Terminal",
                "atlas6_terminal_sector3",
                "Atlas-6 Terminal",
                new Vector3(1795.95935f, 4901.20f, 798.74396f),
                new Vector3(0.35f, 0.45f, 0.35f))
        };

        [Header("Auto Setup")]
        [Tooltip("Create missing child systems automatically during startup.")]
        [SerializeField] private bool autoSetupOnAwake = true;

        [Header("Runtime Recovery")]
        [Tooltip("Registry used by runtime lore fail-safe when the production scene has zero placed lore POIs.")]
        [SerializeField] private Hecton8.Narrative.ColonistLoreRegistry runtimeRecoveryRegistry;

        [Header("Status")]
        [SerializeField] private bool _audioLogSystemFound;
        [SerializeField] private bool _questManagerFound;
        [SerializeField] private bool _atlasSignalSystemFound;
        [SerializeField] private bool _suitUpgradeManagerFound;
        [SerializeField] private bool _depthZoneDirectorFound;
        [SerializeField] private bool _eclipseGameplaySystemFound;
        [SerializeField] private bool _spectrumSystemFound;
        [SerializeField] private bool _atlasSignalDecoderFound;
        [SerializeField] private bool _biolumControllerFound;
        [SerializeField] private bool _atlas6DirectiveSystemFound;
        [SerializeField] private bool _corporateOrderSystemFound;
        [SerializeField] private bool _randomEventSystemFound;
        [SerializeField] private bool _firstHourDirectorFound;
        [SerializeField] private bool _soundscapeSystemFound;
        [SerializeField] private bool _baseIntegrityHUDFound;
        [SerializeField] private bool _endingSystemFound;
        [SerializeField] private int _narrativeDiscoveryCount;
        [SerializeField] private int _audioLogPickupCount;
        private bool _startupLoreAuditReported;
        private bool _runtimeLoreRecoveryAttempted;

        private void Awake()
        {
            if (autoSetupOnAwake)
            {
                SetupAllSystems();
            }
            else
            {
                RefreshSystemStatus(false);
            }

            RefreshLoreContentStatus();
            TryApplyRuntimeLoreRecovery();
            RefreshLoreContentStatus();
            ReportMissingLoreContentAtStartup();
        }

        private void OnEnable()
        {
            RefreshSystemStatus(false);
            RefreshLoreContentStatus();
            TryApplyRuntimeLoreRecovery();
            RefreshLoreContentStatus();
            ReportMissingLoreContentAtStartup();
        }

        private void OnValidate()
        {
            RefreshSystemStatus(false);
            RefreshLoreContentStatus();
        }

        /// <summary>
        /// Creates missing child objects and components for all lore systems.
        /// </summary>
        [ContextMenu("Setup All Systems")]
        public void SetupAllSystems()
        {
            EnsureSystem<Hecton8.Narrative.AudioLogSystem>("AudioLogSystem", ref _audioLogSystemFound);
            EnsureSystem<Hecton8.Quest.QuestManager>("QuestManager", ref _questManagerFound);
            EnsureSystem<Hecton8.AtlasSignal.AtlasSignalSystem>("AtlasSignalSystem", ref _atlasSignalSystemFound);
            EnsureAuthoringBoundSystem<Hecton8.Gameplay.SuitUpgradeManager>("SuitUpgradeManager", ref _suitUpgradeManagerFound);
            EnsureSystem<Hecton8.World.DepthZoneDirector>("DepthZoneDirector", ref _depthZoneDirectorFound);
            EnsureSystem<Hecton8.Gameplay.EclipseGameplaySystem>("EclipseGameplaySystem", ref _eclipseGameplaySystemFound);
            EnsureSystem<Hecton8.Visor.SpectrumSystem>("SpectrumSystem", ref _spectrumSystemFound);
            EnsureSystem<Hecton8.AtlasSignal.AtlasSignalDecoder>("AtlasSignalDecoder", ref _atlasSignalDecoderFound);
            EnsureSystem<Hecton8.World.HectonBiolumController>("HectonBiolumController", ref _biolumControllerFound);
            EnsureSystem<Hecton8.AtlasSignal.Atlas6DirectiveSystem>("Atlas6DirectiveSystem", ref _atlas6DirectiveSystemFound);
            EnsureSystem<Hecton8.Narrative.CorporateOrderSystem>("CorporateOrderSystem", ref _corporateOrderSystemFound);
            EnsureSystem<Hecton8.Gameplay.RandomEventSystem>("RandomEventSystem", ref _randomEventSystemFound);
            EnsureSystem<Hecton8.Gameplay.FirstHourDirector>("FirstHourDirector", ref _firstHourDirectorFound);
            EnsureSystem<Hecton8.World.SoundscapeSystem>("SoundscapeSystem", ref _soundscapeSystemFound);
            EnsureSystem<Hecton8.UI.BaseIntegrityHUD>("BaseIntegrityHUD", ref _baseIntegrityHUDFound);
            EnsureSystem<Hecton8.Gameplay.EndingSystem>("EndingSystem", ref _endingSystemFound);

            RefreshSystemStatus(false);

        }

        /// <summary>
        /// Refreshes the status flags and optionally reports missing systems.
        /// </summary>
        [ContextMenu("Validate Systems")]
        public void ValidateSystems()
        {
            RefreshSystemStatus(true);
            RefreshLoreContentStatus();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LoreSystemsRoot] Validation: {CountFoundSystems()}/{ExpectedSystemCount} systems present. " +
                $"Missing: {GetMissingSystemsSummary()}. " +
                $"NarrativeDiscovery placed: {_narrativeDiscoveryCount}. " +
                $"AudioLogPickup placed: {_audioLogPickupCount}.");

            if (_narrativeDiscoveryCount <= 0)
            {
                Debug.LogWarning(
                    "[LoreSystemsRoot] No NarrativeDiscovery components found in the loaded scene scope. " +
                    "Lore framework exists, but player-facing POIs are not placed.");
            }

            if (_audioLogPickupCount <= 0)
            {
                Debug.LogWarning(
                    "[LoreSystemsRoot] No AudioLogPickup components found in the loaded scene scope. " +
                    "AudioLog framework exists, but no world pickup surfaces are placed.");
            }
#endif
        }

        public int GetFoundSystemCount()
        {
            RefreshSystemStatus(false);
            return CountFoundSystems();
        }

        public bool IsBootstrappedFully()
        {
            return GetFoundSystemCount() == ExpectedSystemCount;
        }

        public string GetMissingSystemsSummary()
        {
            RefreshSystemStatus(false);

            string[] missing = new string[ExpectedSystemCount];
            int index = 0;

            if (!_audioLogSystemFound) missing[index++] = "AudioLogSystem";
            if (!_questManagerFound) missing[index++] = "QuestManager";
            if (!_atlasSignalSystemFound) missing[index++] = "AtlasSignalSystem";
            if (!_suitUpgradeManagerFound) missing[index++] = "SuitUpgradeManager";
            if (!_depthZoneDirectorFound) missing[index++] = "DepthZoneDirector";
            if (!_eclipseGameplaySystemFound) missing[index++] = "EclipseGameplaySystem";
            if (!_spectrumSystemFound) missing[index++] = "SpectrumSystem";
            if (!_atlasSignalDecoderFound) missing[index++] = "AtlasSignalDecoder";
            if (!_biolumControllerFound) missing[index++] = "HectonBiolumController";
            if (!_atlas6DirectiveSystemFound) missing[index++] = "Atlas6DirectiveSystem";
            if (!_corporateOrderSystemFound) missing[index++] = "CorporateOrderSystem";
            if (!_randomEventSystemFound) missing[index++] = "RandomEventSystem";
            if (!_firstHourDirectorFound) missing[index++] = "FirstHourDirector";
            if (!_soundscapeSystemFound) missing[index++] = "SoundscapeSystem";
            if (!_baseIntegrityHUDFound) missing[index++] = "BaseIntegrityHUD";
            if (!_endingSystemFound) missing[index++] = "EndingSystem";

            if (index == 0)
            {
                return "None";
            }

            string[] compact = new string[index];
            System.Array.Copy(missing, compact, index);
            return string.Join(", ", compact);
        }

        public void RefreshSystemStatus(bool logMissingSystems)
        {
            _audioLogSystemFound = GetComponentInChildren<Hecton8.Narrative.AudioLogSystem>(true) != null;
            _questManagerFound = GetComponentInChildren<Hecton8.Quest.QuestManager>(true) != null;
            _atlasSignalSystemFound = GetComponentInChildren<Hecton8.AtlasSignal.AtlasSignalSystem>(true) != null;
            _suitUpgradeManagerFound = GetComponentInChildren<Hecton8.Gameplay.SuitUpgradeManager>(true) != null;
            _depthZoneDirectorFound = GetComponentInChildren<Hecton8.World.DepthZoneDirector>(true) != null;
            _eclipseGameplaySystemFound = GetComponentInChildren<Hecton8.Gameplay.EclipseGameplaySystem>(true) != null;
            _spectrumSystemFound = GetComponentInChildren<Hecton8.Visor.SpectrumSystem>(true) != null;
            _atlasSignalDecoderFound = GetComponentInChildren<Hecton8.AtlasSignal.AtlasSignalDecoder>(true) != null;
            _biolumControllerFound = GetComponentInChildren<Hecton8.World.HectonBiolumController>(true) != null;
            _atlas6DirectiveSystemFound = GetComponentInChildren<Hecton8.AtlasSignal.Atlas6DirectiveSystem>(true) != null;
            _corporateOrderSystemFound = GetComponentInChildren<Hecton8.Narrative.CorporateOrderSystem>(true) != null;
            _randomEventSystemFound = GetComponentInChildren<Hecton8.Gameplay.RandomEventSystem>(true) != null;
            _firstHourDirectorFound = GetComponentInChildren<Hecton8.Gameplay.FirstHourDirector>(true) != null;
            _soundscapeSystemFound = GetComponentInChildren<Hecton8.World.SoundscapeSystem>(true) != null;
            _baseIntegrityHUDFound = GetComponentInChildren<Hecton8.UI.BaseIntegrityHUD>(true) != null;
            _endingSystemFound = GetComponentInChildren<Hecton8.Gameplay.EndingSystem>(true) != null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logMissingSystems && CountFoundSystems() < ExpectedSystemCount)
            {
                Debug.LogWarning($"[LoreSystemsRoot] Missing systems: {GetMissingSystemsSummary()}");
            }
#endif
        }

        private void RefreshLoreContentStatus()
        {
            // COLD ALLOC: scene validation arrays — explicit validation/editor-only diagnostics
            Hecton8.Interaction.NarrativeDiscovery[] discoveries =
                FindObjectsByType<Hecton8.Interaction.NarrativeDiscovery>(
                    FindObjectsInactive.Include);

            // COLD ALLOC: scene validation arrays — explicit validation/editor-only diagnostics
            Hecton8.Narrative.AudioLogPickup[] audioPickups =
                FindObjectsByType<Hecton8.Narrative.AudioLogPickup>(
                    FindObjectsInactive.Include);

            _narrativeDiscoveryCount = discoveries != null ? discoveries.Length : 0;
            _audioLogPickupCount = audioPickups != null ? audioPickups.Length : 0;
        }

        private void TryApplyRuntimeLoreRecovery()
        {
            if (!Application.isPlaying || _runtimeLoreRecoveryAttempted)
                return;

            if (_narrativeDiscoveryCount > 0 && _audioLogPickupCount > 0)
                return;

            if (runtimeRecoveryRegistry == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[LoreSystemsRoot] Runtime lore recovery skipped. ColonistLoreRegistry is not assigned on LoreSystems.",
                    this);
#endif
                return;
            }

            _runtimeLoreRecoveryAttempted = true;

            int createdOrUpdatedCount = 0;
            for (int i = 0; i < _runtimeRecoveryPlacements.Length; i++)
            {
                if (TryEnsureRuntimeRecoveryPlacement(_runtimeRecoveryPlacements[i]))
                    createdOrUpdatedCount++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (createdOrUpdatedCount > 0)
            {
                Debug.LogWarning(
                    "[LoreSystemsRoot] Applied runtime lore recovery because the production scene had no placed player-facing lore. " +
                    "This is a fail-safe, not a substitute for authored placement.",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    "[LoreSystemsRoot] Runtime lore recovery attempted, but no fallback markers could be resolved. " +
                    "Scene still lacks player-facing lore entry points.",
                    this);
            }
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ReportMissingLoreContentAtStartup()
        {
            if (_startupLoreAuditReported)
                return;

            _startupLoreAuditReported = true;

            if (_narrativeDiscoveryCount > 0 && _audioLogPickupCount > 0)
                return;

            Debug.LogWarning(
                $"[LoreSystemsRoot] Startup lore audit failed. " +
                $"NarrativeDiscovery placed: {_narrativeDiscoveryCount}. " +
                $"AudioLogPickup placed: {_audioLogPickupCount}. " +
                $"Lore systems exist, but player-facing lore placement is incomplete.",
                this);
        }

        private bool TryEnsureRuntimeRecoveryPlacement(RecoveryLorePlacement placement)
        {
            if (runtimeRecoveryRegistry == null || string.IsNullOrWhiteSpace(placement.HostPath))
                return false;

            GameObject hostObject = GameObject.Find(placement.HostPath);
            if (hostObject == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[LoreSystemsRoot] Runtime lore recovery host not found: {placement.HostPath}",
                    this);
#endif
                return false;
            }

            if (!TryResolveRecoveryEntry(placement, out Hecton8.Narrative.AudioLogData logData, out string displayName))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[LoreSystemsRoot] Runtime lore recovery entry not resolved from ColonistLoreRegistry: {placement.LogId}",
                    this);
#endif
                return false;
            }

            Transform hostTransform = hostObject.transform;
            Transform markerTransform = hostTransform.Find(placement.MarkerName);
            bool createdMarker = markerTransform == null;

            if (createdMarker)
            {
                // COLD ALLOC: GameObject[1] — runtime lore recovery marker for zero-placement world state — owner: HectonLoreSystemsRoot
                GameObject markerObject = new GameObject(placement.MarkerName);
                markerTransform = markerObject.transform;
                markerTransform.SetParent(hostTransform, false);
            }

            GameObject marker = markerTransform.gameObject;
            bool restoreActive = createdMarker || marker.activeSelf;
            if (marker.activeSelf)
                marker.SetActive(false);

            markerTransform.localPosition = placement.LocalPosition;
            markerTransform.localRotation = Quaternion.identity;
            markerTransform.localScale = Vector3.one;

            BoxCollider collider = marker.GetComponent<BoxCollider>();
            if (collider == null)
                collider = marker.AddComponent<BoxCollider>();

            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size = placement.ColliderSize;

            switch (placement.Kind)
            {
                case RecoveryLoreKind.NarrativeDiscovery:
                {
                    Hecton8.Interaction.NarrativeDiscovery discovery =
                        marker.GetComponent<Hecton8.Interaction.NarrativeDiscovery>();
                    if (discovery == null)
                        discovery = marker.AddComponent<Hecton8.Interaction.NarrativeDiscovery>();

                    discovery.ConfigureRecoveryPlacement(
                        placement.LogId,
                        displayName,
                        logData,
                        true);
                    break;
                }

                case RecoveryLoreKind.AudioLogPickup:
                {
                    Hecton8.Narrative.AudioLogPickup pickup =
                        marker.GetComponent<Hecton8.Narrative.AudioLogPickup>();
                    if (pickup == null)
                        pickup = marker.AddComponent<Hecton8.Narrative.AudioLogPickup>();

                    pickup.ConfigureRecoveryPickup(logData, true);
                    break;
                }
            }

            if (restoreActive)
                marker.SetActive(true);

            return true;
        }

        private bool TryResolveRecoveryEntry(
            RecoveryLorePlacement placement,
            out Hecton8.Narrative.AudioLogData logData,
            out string displayName)
        {
            if (runtimeRecoveryRegistry != null &&
                runtimeRecoveryRegistry.TryGetEntry(placement.LogId, out Hecton8.Narrative.LoreEntry entry) &&
                entry.linkedAudioLog != null)
            {
                logData = entry.linkedAudioLog;
                displayName = string.IsNullOrWhiteSpace(entry.DisplayNameOrFallback)
                    ? placement.FallbackDisplayName
                    : entry.DisplayNameOrFallback;
                return true;
            }

            logData = null;
            displayName = placement.FallbackDisplayName;
            return false;
        }

        private int CountFoundSystems()
        {
            int found = 0;
            if (_audioLogSystemFound) found++;
            if (_questManagerFound) found++;
            if (_atlasSignalSystemFound) found++;
            if (_suitUpgradeManagerFound) found++;
            if (_depthZoneDirectorFound) found++;
            if (_eclipseGameplaySystemFound) found++;
            if (_spectrumSystemFound) found++;
            if (_atlasSignalDecoderFound) found++;
            if (_biolumControllerFound) found++;
            if (_atlas6DirectiveSystemFound) found++;
            if (_corporateOrderSystemFound) found++;
            if (_randomEventSystemFound) found++;
            if (_firstHourDirectorFound) found++;
            if (_soundscapeSystemFound) found++;
            if (_baseIntegrityHUDFound) found++;
            if (_endingSystemFound) found++;
            return found;
        }

        private void EnsureSystem<T>(string goName, ref bool foundFlag)
            where T : MonoBehaviour
        {
            T existingComponent = GetComponentInChildren<T>(true);
            if (existingComponent != null)
            {
                foundFlag = true;
                return;
            }

            Transform existingChild = transform.Find(goName);
            if (existingChild != null)
            {
                if (existingChild.GetComponent<T>() == null)
                {
#if UNITY_EDITOR
                    Undo.AddComponent<T>(existingChild.gameObject);
#else
                    existingChild.gameObject.AddComponent<T>();
#endif
                }

                foundFlag = true;
                return;
            }

            GameObject go = new GameObject(goName);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
#endif
            go.transform.SetParent(transform, false);
            go.AddComponent<T>();
            foundFlag = true;
        }

        private void EnsureAuthoringBoundSystem<T>(string goName, ref bool foundFlag)
            where T : MonoBehaviour
        {
            T existingComponent = GetComponentInChildren<T>(true);
            if (existingComponent != null)
            {
                foundFlag = true;
                return;
            }

            if (Application.isPlaying)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[LoreSystemsRoot] Skipping runtime auto-create for '{goName}'. This system requires authored inspector data before play.",
                    this);
#endif
                foundFlag = false;
                return;
            }

            EnsureSystem<T>(goName, ref foundFlag);
        }
    }
}
