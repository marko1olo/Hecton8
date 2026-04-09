// ============================================================================
// HECTON-8 — HectonLoreSystemsRoot.cs
// Корневой компонент для всех лорных систем.
//
// НАЗНАЧЕНИЕ:
//   Единая точка инициализации всех систем из лора.
//   Назначить на GameObject "LoreSystems" в сцене 02_HECTON_WORLD.
//
// СИСТЕМЫ (в порядке DefaultExecutionOrder):
//   -150: HectonNarrativeDirector (существующий)
//   -140: AudioLogSystem
//   -130: QuestManager
//   -120: AtlasSignalSystem
//   -110: SuitUpgradeManager
//   -105: DepthZoneDirector
//   -100: EclipseGameplaySystem
//    -95: SpectrumSystem
//    -90: AtlasSignalDecoder
//    -85: HectonBiolumController
//    -80: Atlas6DirectiveSystem
//    -75: CorporateOrderSystem
//    -70: RandomEventSystem
//    -65: FirstHourDirector
//    -60: SoundscapeSystem
//    -55: BaseIntegrityHUD
//    -50: EndingSystem
//
// ИСПОЛЬЗОВАНИЕ:
//   1. Создать GameObject "LoreSystems" в сцене 02_HECTON_WORLD
//   2. Добавить этот компонент
//   3. Нажать [Setup All Systems] в инспекторе
//   4. Назначить ссылки в инспекторе каждой системы
//
// ВАЖНО:
//   Все системы используют [DefaultExecutionOrder] — порядок гарантирован.
//   Этот компонент только создаёт дочерние объекты если их нет.
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
        [Header("── Auto-Setup ──────────────────────────────")]
        [Tooltip("Автоматически создать дочерние объекты для всех систем при старте.")]
        [SerializeField] private bool autoSetupOnAwake = true;

        [Header("── Status ──────────────────────────────────")]
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
                SetupAllSystems();
        }

        /// <summary>
        /// Создаёт дочерние объекты для всех лорных систем если их нет.
        /// Вызывается автоматически в Awake или вручную из инспектора.
        /// </summary>
        public void SetupAllSystems()
        {
            EnsureSystem<Hecton8.Narrative.AudioLogSystem>("AudioLogSystem",
                ref _audioLogSystemFound);

            EnsureSystem<Hecton8.Quest.QuestManager>("QuestManager",
                ref _questManagerFound);

            EnsureSystem<Hecton8.AtlasSignal.AtlasSignalSystem>("AtlasSignalSystem",
                ref _atlasSignalSystemFound);

            EnsureSystem<Hecton8.Gameplay.SuitUpgradeManager>("SuitUpgradeManager",
                ref _suitUpgradeManagerFound);

            EnsureSystem<Hecton8.World.DepthZoneDirector>("DepthZoneDirector",
                ref _depthZoneDirectorFound);

            EnsureSystem<Hecton8.Gameplay.EclipseGameplaySystem>("EclipseGameplaySystem",
                ref _eclipseGameplaySystemFound);

            EnsureSystem<Hecton8.Visor.SpectrumSystem>("SpectrumSystem",
                ref _spectrumSystemFound);

            EnsureSystem<Hecton8.AtlasSignal.AtlasSignalDecoder>("AtlasSignalDecoder",
                ref _atlasSignalDecoderFound);

            EnsureSystem<Hecton8.World.HectonBiolumController>("HectonBiolumController",
                ref _biolumControllerFound);

            EnsureSystem<Hecton8.AtlasSignal.Atlas6DirectiveSystem>("Atlas6DirectiveSystem",
                ref _atlas6DirectiveSystemFound);

            EnsureSystem<Hecton8.Narrative.CorporateOrderSystem>("CorporateOrderSystem",
                ref _corporateOrderSystemFound);

            EnsureSystem<Hecton8.Gameplay.RandomEventSystem>("RandomEventSystem",
                ref _randomEventSystemFound);

            EnsureSystem<Hecton8.Gameplay.FirstHourDirector>("FirstHourDirector",
                ref _firstHourDirectorFound);

            EnsureSystem<Hecton8.World.SoundscapeSystem>("SoundscapeSystem",
                ref _soundscapeSystemFound);

            EnsureSystem<Hecton8.UI.BaseIntegrityHUD>("BaseIntegrityHUD",
                ref _baseIntegrityHUDFound);

            EnsureSystem<Hecton8.Gameplay.EndingSystem>("EndingSystem",
                ref _endingSystemFound);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[LoreSystemsRoot] All lore systems initialized.");
#endif
        }

        private void EnsureSystem<T>(string goName, ref bool foundFlag)
            where T : MonoBehaviour
        {
            // Проверяем существующий дочерний объект
            Transform existing = transform.Find(goName);
            if (existing != null)
            {
                foundFlag = existing.GetComponent<T>() != null;
                if (!foundFlag)
                {
                    existing.gameObject.AddComponent<T>();
                    foundFlag = true;
                }
                return;
            }

            // Создаём новый
            GameObject go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.AddComponent<T>();
            foundFlag = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Setup All Systems")]
        private void SetupAllSystemsEditor()
        {
            SetupAllSystems();
            EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Validate Systems")]
        private void ValidateSystems()
        {
            int found = 0;
            int total = 16;

            _audioLogSystemFound        = GetComponentInChildren<Hecton8.Narrative.AudioLogSystem>() != null;
            _questManagerFound          = GetComponentInChildren<Hecton8.Quest.QuestManager>() != null;
            _atlasSignalSystemFound     = GetComponentInChildren<Hecton8.AtlasSignal.AtlasSignalSystem>() != null;
            _suitUpgradeManagerFound    = GetComponentInChildren<Hecton8.Gameplay.SuitUpgradeManager>() != null;
            _depthZoneDirectorFound     = GetComponentInChildren<Hecton8.World.DepthZoneDirector>() != null;
            _eclipseGameplaySystemFound = GetComponentInChildren<Hecton8.Gameplay.EclipseGameplaySystem>() != null;
            _spectrumSystemFound        = GetComponentInChildren<Hecton8.Visor.SpectrumSystem>() != null;
            _atlasSignalDecoderFound    = GetComponentInChildren<Hecton8.AtlasSignal.AtlasSignalDecoder>() != null;
            _biolumControllerFound      = GetComponentInChildren<Hecton8.World.HectonBiolumController>() != null;
            _atlas6DirectiveSystemFound = GetComponentInChildren<Hecton8.AtlasSignal.Atlas6DirectiveSystem>() != null;
            _corporateOrderSystemFound  = GetComponentInChildren<Hecton8.Narrative.CorporateOrderSystem>() != null;
            _randomEventSystemFound     = GetComponentInChildren<Hecton8.Gameplay.RandomEventSystem>() != null;
            _firstHourDirectorFound     = GetComponentInChildren<Hecton8.Gameplay.FirstHourDirector>() != null;
            _soundscapeSystemFound      = GetComponentInChildren<Hecton8.World.SoundscapeSystem>() != null;
            _baseIntegrityHUDFound      = GetComponentInChildren<Hecton8.UI.BaseIntegrityHUD>() != null;
            _endingSystemFound          = GetComponentInChildren<Hecton8.Gameplay.EndingSystem>() != null;

            bool[] flags = {
                _audioLogSystemFound, _questManagerFound, _atlasSignalSystemFound,
                _suitUpgradeManagerFound, _depthZoneDirectorFound, _eclipseGameplaySystemFound,
                _spectrumSystemFound, _atlasSignalDecoderFound, _biolumControllerFound,
                _atlas6DirectiveSystemFound, _corporateOrderSystemFound, _randomEventSystemFound,
                _firstHourDirectorFound, _soundscapeSystemFound, _baseIntegrityHUDFound,
                _endingSystemFound
            };

            foreach (bool f in flags) if (f) found++;

            Debug.Log($"[LoreSystemsRoot] Validation: {found}/{total} systems found.");
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
