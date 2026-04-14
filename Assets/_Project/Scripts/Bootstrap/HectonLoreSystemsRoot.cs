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

        [Header("Auto Setup")]
        [Tooltip("Create missing child systems automatically during startup.")]
        [SerializeField] private bool autoSetupOnAwake = true;

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

        private void Awake()
        {
            if (autoSetupOnAwake)
            {
                SetupAllSystems();
                return;
            }

            RefreshSystemStatus(false);
        }

        private void OnEnable()
        {
            RefreshSystemStatus(false);
        }

        private void OnValidate()
        {
            RefreshSystemStatus(false);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LoreSystemsRoot] Validation: {CountFoundSystems()}/{ExpectedSystemCount} systems present. Missing: {GetMissingSystemsSummary()}");
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
