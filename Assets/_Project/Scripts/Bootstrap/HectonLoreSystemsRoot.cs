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
        public const int ExpectedSystemCount = 17;

        [Header("Status")]
        [SerializeField] private bool _audioLogSystemFound;
        [SerializeField] private bool _loreDatabaseManagerFound;
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

        private void Awake()
        {
            // Runtime bootstrap must stay self-owned. Manual setup and validation remain
            // available through inspector actions, but play-mode startup does not mutate scene state.
            RefreshSystemStatus(false);
        }

        private void OnEnable()
        {
            RefreshSystemStatus(false);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
#endif

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
            EnsureSystem<Hecton8.Narrative.LoreDatabaseManager>("LoreDatabaseManager", ref _loreDatabaseManagerFound);
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
            if (_narrativeDiscoveryCount <= 0)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[LoreSystemsRoot] No NarrativeDiscovery components found in the loaded scene scope. " +
                    "Lore framework exists, but player-facing POIs are not placed.");
            }

            if (_audioLogPickupCount <= 0)
            {
                Hecton8.Core.H8Debug.LogWarning(
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
            return BuildMissingSystemsSummary();
        }

        private string BuildMissingSystemsSummary()
        {
            string[] missing = new string[ExpectedSystemCount];
            int index = 0;

            if (!_audioLogSystemFound) missing[index++] = "AudioLogSystem";
            if (!_loreDatabaseManagerFound) missing[index++] = "LoreDatabaseManager";
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
            _audioLogSystemFound = HasNamedSystem<Hecton8.Narrative.AudioLogSystem>("AudioLogSystem");
            _loreDatabaseManagerFound = HasNamedSystem<Hecton8.Narrative.LoreDatabaseManager>("LoreDatabaseManager");
            _questManagerFound = HasNamedSystem<Hecton8.Quest.QuestManager>("QuestManager");
            _atlasSignalSystemFound = HasNamedSystem<Hecton8.AtlasSignal.AtlasSignalSystem>("AtlasSignalSystem");
            _suitUpgradeManagerFound = HasNamedSystem<Hecton8.Gameplay.SuitUpgradeManager>("SuitUpgradeManager");
            _depthZoneDirectorFound = HasNamedSystem<Hecton8.World.DepthZoneDirector>("DepthZoneDirector");
            _eclipseGameplaySystemFound = HasNamedSystem<Hecton8.Gameplay.EclipseGameplaySystem>("EclipseGameplaySystem");
            _spectrumSystemFound = HasNamedSystem<Hecton8.Visor.SpectrumSystem>("SpectrumSystem");
            _atlasSignalDecoderFound = HasNamedSystem<Hecton8.AtlasSignal.AtlasSignalDecoder>("AtlasSignalDecoder");
            _biolumControllerFound = HasNamedSystem<Hecton8.World.HectonBiolumController>("HectonBiolumController");
            _atlas6DirectiveSystemFound = HasNamedSystem<Hecton8.AtlasSignal.Atlas6DirectiveSystem>("Atlas6DirectiveSystem");
            _corporateOrderSystemFound = HasNamedSystem<Hecton8.Narrative.CorporateOrderSystem>("CorporateOrderSystem");
            _randomEventSystemFound = HasNamedSystem<Hecton8.Gameplay.RandomEventSystem>("RandomEventSystem");
            _firstHourDirectorFound = HasNamedSystem<Hecton8.Gameplay.FirstHourDirector>("FirstHourDirector");
            _soundscapeSystemFound = HasNamedSystem<Hecton8.World.SoundscapeSystem>("SoundscapeSystem");
            _baseIntegrityHUDFound = HasNamedSystem<Hecton8.UI.BaseIntegrityHUD>("BaseIntegrityHUD");
            _endingSystemFound = HasNamedSystem<Hecton8.Gameplay.EndingSystem>("EndingSystem");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logMissingSystems && CountFoundSystems() < ExpectedSystemCount)
            {
                Hecton8.Core.H8Debug.LogWarning($"[LoreSystemsRoot] Missing systems: {BuildMissingSystemsSummary()}");
            }
#endif
        }

        private void RefreshLoreContentStatus()
        {
            int discoveries = Hecton8.Interaction.NarrativeDiscovery.ActiveDiscoveryCount;

            int audioPickups = Hecton8.Narrative.AudioLogPickup.RegisteredPickupTemplateCount;

            _narrativeDiscoveryCount = discoveries;
            _audioLogPickupCount = audioPickups;
        }

        private int CountFoundSystems()
        {
            int found = 0;
            if (_audioLogSystemFound) found++;
            if (_loreDatabaseManagerFound) found++;
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
            if (TryResolveNamedSystem(goName, out T existingComponent) && existingComponent != null)
            {
                foundFlag = true;
                return;
            }

            Transform existingChild = transform.Find(goName);
            if (existingChild != null)
            {
                if (!existingChild.TryGetComponent<T>(out _))
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
            if (TryResolveNamedSystem(goName, out T existingComponent) && existingComponent != null)
            {
                foundFlag = true;
                return;
            }

            if (Application.isPlaying)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[LoreSystemsRoot] Skipping runtime auto-create for '{goName}'. This system requires authored inspector data before play.",
                    this);
#endif
                foundFlag = false;
                return;
            }

            EnsureSystem<T>(goName, ref foundFlag);
        }

        private bool HasNamedSystem<T>(string goName)
            where T : Component
        {
            return TryResolveNamedSystem(goName, out T _);
        }

        private bool TryResolveNamedSystem<T>(string goName, out T component)
            where T : Component
        {
            component = null;
            if (string.IsNullOrWhiteSpace(goName))
                return false;

            Transform child = transform.Find(goName);
            if (child == null)
                return false;

            return child.TryGetComponent(out component);
        }
    }
}
