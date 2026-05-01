// ============================================================================
// HECTON-8 — AcousticZoneController.cs
// Управление акустическими зонами: плавный переход аудио между
// открытым океаном и сухими зонами внутри модулей базы.
//
// АРХИТЕКТУРА:
//   • Синглтон, ITickable — проверка состояния игрока каждый кадр.
//   • Edge detection: переход запускается ТОЛЬКО при смене состояния
//     (вода → суша или суша → вода). Один bool-comparison per frame.
//   • AudioMixerSnapshot.TransitionTo — плавный кроссфейд пресетов.
//   • SpatialAudioManager — воспроизведение переходных звуков (drain/fill).
//
// СТОЛП 1 — ТЕХНОЛОГИЧЕСКИЙ УЮТ:
//   Внутри базы: тишина, гул генераторов, шаги по металлу.
//   Снаружи: давящий низкочастотный гул, бульканье, эхо глубины.
//   Контраст создаётся через AudioMixer Snapshots:
//     UnderwaterSnapshot: Low-Pass Filter (LPF) на Master,
//       Reverb (Large Hall), приглушённые высокие частоты.
//     BaseInteriorSnapshot: LPF снят, Reverb (Small Room / Metallic),
//       чистые средние частоты, лёгкий механический гул.
//
// ИНТЕГРАЦИЯ:
//   • Читает BuoyancyObject.IsInAir через кэшированную ссылку.
//   • BuoyancyObject.IsInAir = true → игрок внутри сухого модуля.
//   • BuoyancyObject.IsInAir = false → игрок в воде.
//   • Ленивый resolve игрока через SceneBootstrap (один раз).
//
// TRANSITION FLOW:
//   FixedTick: BuoyancyObject.IsInAir changes
//     → Tick: AcousticZoneController detects edge
//       → snapshot.TransitionTo(transitionDuration)
//       → SpatialAudioManager.PlayStatic2D(transitionClip)
//       → Optional: event OnAcousticZoneChanged(isInterior)
//
// ZERO GC:
//   • Tick: один bool comparison + edge detection. Zero alloc.
//   • TransitionTo: Unity internal, no managed alloc.
//   • PlayStatic2D: пул 2D-голосов SpatialAudioManager.
//   • Ленивый resolve игрока через SceneBootstrap.
//   • Нет Update, нет корутин, нет LINQ.
//
// CPU COST:
//   ~0.0001ms per Tick (one bool read + comparison).
//   Transition itself is handled by Unity AudioMixer internally.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)] // После FluidEngine (-5000), до большинства систем
    public sealed class AcousticZoneController : MonoBehaviour, ITickable, IUpdatable
    {
        private static readonly string[] SoundscapeTierLabels = System.Enum.GetNames(typeof(SoundscapeTier));

#if UNITY_EDITOR
        private const string DefaultWaterDrainSoundPath = "Assets/_Project/Audio/Movement/swimming -onwater.wav";
        private const string DefaultWaterFillSoundPath = "Assets/_Project/Audio/Movement/swimming - underwater.ogg";
        private const string DefaultMasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string DefaultStormStaticPrimaryPath = "Assets/_Project/Audio/Music for Game/shelf_6_Decaying Analog Static.ogg";
        private const string DefaultStormStaticSecondaryPath = "Assets/_Project/Audio/Music for Game/shelf_7_Decaying Analog Static.ogg";
#endif
        private const string AcousticLowPassCutoffParameterDefault = "AcousticLowPassCutoffHz";
        private const string AcousticLowPassResonanceParameterDefault = "AcousticLowPassResonanceQ";
        private const string AcousticReverbDecayParameterDefault = "AcousticReverbDecayTime";
        private const string AcousticReflectionsLevelParameterDefault = "AcousticReverbReflectionsLevelDb";
        private const string AcousticReverbLevelParameterDefault = "AcousticReverbLevelDb";
        private const string AcousticRoomHighFrequencyParameterDefault = "AcousticRoomHighFrequencyDb";
        private const string AcousticDryLevelParameterDefault = "AcousticDryLevelDb";
        private const int AcousticEmitterSampleCapacity = 24;
        private const float AcousticEmitterOcclusionMaxDistanceMeters = 48f;
        private const float AcousticEmitterDistanceWeightScale = 0.05f;
        private const float AmbientSourceResolveRetryInterval = 0.5f;

        private enum AcousticZoneState : byte
        {
            Surface = 0,
            Underwater = 1,
            Interior = 2
        }

        private struct AcousticGraphState
        {
            public float LowPassCutoffHz;
            public float LowPassResonanceQ;
            public float ReverbDecayTime;
            public float ReflectionsLevelDb;
            public float ReverbLevelDb;
            public float RoomHighFrequencyDb;
            public float DryLevelDb;
        }

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static AcousticZoneController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnAcousticZoneChanged = null;
        }

        public static AcousticZoneController Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GLOBAL EVENT — ACOUSTIC ZONE CHANGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when player transitions between acoustic zones.
        /// Parameter: true = interior (dry, inside base), false = underwater.
        ///
        /// Subscribers (future):
        ///   - Ambient sound layers (start/stop underwater drone).
        ///   - Footstep system (switch to metal footsteps).
        ///   - Music system (switch between exploration/safety tracks).
        ///   - VFX (water droplets on helmet when exiting water).
        /// </summary>
        public static event Action<bool> OnAcousticZoneChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SNAPSHOTS
        // ══════════════════════════════════════════════════════════

        [Header("── AudioMixer Snapshots ──────────────────────")]
        [Tooltip("Snapshot для подводной среды.\n" +
                 "Настройки: Low-Pass Filter, Reverb (Large Hall),\n" +
                 "приглушённые высокие, усиленные низкие.")]
        [SerializeField] private AudioMixerSnapshot underwaterSnapshot;

        [Tooltip("Snapshot для интерьера базы.\n" +
                 "Настройки: LPF снят, Reverb (Small Room),\n" +
                 "чистые средние, лёгкий механический гул.")]
        [SerializeField] private AudioMixerSnapshot baseInteriorSnapshot;

        [Tooltip("Optional MasterMixer asset used to auto-resolve authored snapshot refs by name in cold path/editor.")]
        [SerializeField] private AudioMixer masterMixer;

        [SerializeField] private AudioMixerSnapshot surfaceSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceRainSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceStormSnapshot;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Settings ───────────────────────")]
        [Tooltip("Время перехода между snapshot'ами (секунды).\n" +
                 "2.0 = плавный кроссфейд, имитирующий откачку воды.\n" +
                 "0.5 = быстрый переход для тестирования.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Время перехода в интерьер базы. Даёт отдельный control над dry-zone LPF/reverb response.")]
        [SerializeField] private float interiorTransitionDuration = 2.0f;

        [Tooltip("Время перехода к обычному surface snapshot без погодного перебленда.")]
        [SerializeField] private float surfaceTransitionDuration = 2.0f;

        [Tooltip("Время перехода при входе в воду (может быть быстрее,\n" +
                 "т.к. 'вода заполняет шлюз' мгновеннее, чем 'откачка').")]
        [SerializeField] private float underwaterTransitionDuration = 1.5f;

        [Tooltip("Время weather-перебленда для Surface/Rain/Storm snapshots.")]
        [SerializeField] private float surfaceWeatherTransitionDuration = 1.0f;

        [Tooltip("Вес Rain snapshot в Surface weather mix. Управляет perceived wet layer без правки кода.")]
        [SerializeField, Range(0f, 1f)] private float surfaceRainSnapshotWeight = 0.55f;

        [Tooltip("Вес Storm snapshot в Surface weather mix. Управляет интенсивностью storm wet layer.")]
        [SerializeField, Range(0f, 1f)] private float surfaceStormSnapshotWeight = 0.8f;

        [Header("── Exterior State Stability ───────────────────────")]
        [Tooltip("Глубина входа в подводное акустическое состояние.\n" +
                 "Держится выше визуального порога, чтобы акустика не дрожала на ряби у поверхности.")]
        [SerializeField] private float acousticEnterUnderwaterDepth = SurfaceStateUtility.EnterUnderwaterDepth;

        [Tooltip("Глубина выхода из подводного акустического состояния.\n" +
                 "Должна быть ниже enter-порога, чтобы сохранить hysteresis.")]
        [SerializeField] private float acousticExitUnderwaterDepth = SurfaceStateUtility.ExitUnderwaterDepth;
        [SerializeField, Range(0.1f, 1f)] private float acousticEnterImmersionRatio = 0.82f;
        [SerializeField, Range(0.05f, 0.95f)] private float acousticExitImmersionRatio = 0.6f;
        [SerializeField] private float acousticForceUnderwaterDepth = 1.1f;

        [Tooltip("Минимальное время подтверждения для переключения между Surface и Underwater.\n" +
                 "Interior переключается без задержки.")]
        [SerializeField] private float exteriorTransitionDebounce = 0.35f;

        [Tooltip("Минимальное время удержания внешнего акустического состояния после уже совершенного перехода.\n" +
                 "Не дает Surface/Underwater щелкать на пограничной болтанке у поверхности.")]
        [SerializeField] private float exteriorTransitionHoldTime = 1.25f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION SOUNDS
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Audio ──────────────────────────")]
        [Tooltip("Звук откачки воды (вход в сухую зону).\n" +
                 "Воспроизводится через SpatialAudioManager.PlayStatic2D\n" +
                 "(2D, 'внутри шлема'). Длительность ~2-3 секунды.")]
        [SerializeField] private AudioClip waterDrainSound;

        [Tooltip("Звук заполнения водой (выход в океан).\n" +
                 "Бульканье + давление + шипение.")]
        [SerializeField] private AudioClip waterFillSound;

        [Tooltip("Громкость переходных звуков [0..1].")]
        [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.8f;

        [Header("Storm Interference Audio")]
        [Tooltip("Optional 2D helmet-static pulse used during heavy electrical storms.")]
        [SerializeField] private AudioClip stormStaticPrimary;

        [Tooltip("Optional alternate static pulse so repeated storm interference does not sound identical.")]
        [SerializeField] private AudioClip stormStaticSecondary;

        [Tooltip("Electrical activity required before storm audio interference becomes audible.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticElectricalThreshold = 0.52f;

        [Tooltip("Slowest cadence between static pulses when the storm only barely exceeds the threshold.")]
        [SerializeField, Min(0.1f)] private float stormStaticIntervalMax = 5.2f;

        [Tooltip("Fastest cadence between static pulses during peak electrical activity.")]
        [SerializeField, Min(0.1f)] private float stormStaticIntervalMin = 1.6f;

        [Tooltip("Helmet-static pulse volume when the storm first crosses the interference threshold.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticVolumeMin = 0.08f;

        [Tooltip("Helmet-static pulse volume during peak electrical activity.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticVolumeMax = 0.2f;

        [Tooltip("Volume multiplier for storm static pulses while the player remains underwater.")]
        [SerializeField, Range(0f, 1f)] private float stormStaticUnderwaterVolumeScale = 0.72f;

        [Tooltip("Maximum ducking applied to the underwater ambient loop while storms interfere with the suit audio path.")]
        [SerializeField, Range(0f, 0.5f)] private float stormAmbientDuckMax = 0.18f;

        [Tooltip("Maximum downward pitch shift applied to the underwater ambient loop during heavy electrical storms.")]
        [SerializeField, Range(0f, 0.25f)] private float stormAmbientPitchDropMax = 0.08f;

        [Tooltip("Maximum flutter amplitude layered on the underwater ambient loop pitch during heavy electrical storms.")]
        [SerializeField, Range(0f, 0.15f)] private float stormAmbientPitchFlutterMax = 0.035f;

        [Tooltip("Pitch flutter frequency range floor for underwater storm interference.")]
        [SerializeField, Range(0.1f, 5f)] private float stormAmbientFlutterFrequencyMin = 0.6f;

        [Tooltip("Pitch flutter frequency range ceiling for underwater storm interference.")]
        [SerializeField, Range(0.1f, 8f)] private float stormAmbientFlutterFrequencyMax = 2.1f;

        [Header("Sonar Pulse Audio")]
        [Tooltip("Optional 2D sonar ping one-shot used when the player sends an active sonar pulse.")]
        [SerializeField] private AudioClip sonarPingClip;
        [Tooltip("Minimum sonar ping volume for low-intensity pulses.")]
        [SerializeField, Range(0f, 1f)] private float sonarPingVolumeMin = 0.18f;
        [Tooltip("Maximum sonar ping volume for full-strength active pulses.")]
        [SerializeField, Range(0f, 1f)] private float sonarPingVolumeMax = 0.42f;

        [Header("Manta Misfire Audio")]
        [Tooltip("Optional 2D sputter one-shot used when the handheld Manta drive misfires under hull stress.")]
        [SerializeField] private AudioClip mantaMisfireClip;
        [Tooltip("Minimum misfire sputter volume when the hull only barely exceeds the failure threshold.")]
        [SerializeField, Range(0f, 1f)] private float mantaMisfireVolumeMin = 0.14f;
        [Tooltip("Maximum misfire sputter volume when the hull is near catastrophic stress.")]
        [SerializeField, Range(0f, 1f)] private float mantaMisfireVolumeMax = 0.36f;

        [Header("Fatal Pressure Audio")]
        [Tooltip("Primary 2D white-noise burst used during the fatal crush-depth glitch loop.")]
        [SerializeField] private AudioClip fatalPressureNoisePrimary;
        [Tooltip("Alternate 2D white-noise burst so repeated fatal-pressure warnings do not sound identical.")]
        [SerializeField] private AudioClip fatalPressureNoiseSecondary;
        [Tooltip("Slowest cadence between fatal-pressure white-noise bursts at sequence start.")]
        [SerializeField, Min(0.05f)] private float fatalPressureNoiseIntervalMax = 0.38f;
        [Tooltip("Fastest cadence between fatal-pressure white-noise bursts right before implosion.")]
        [SerializeField, Min(0.05f)] private float fatalPressureNoiseIntervalMin = 0.08f;
        [Tooltip("Minimum white-noise burst volume during the fatal-pressure loop.")]
        [SerializeField, Range(0f, 1f)] private float fatalPressureNoiseVolumeMin = 0.16f;
        [Tooltip("Maximum white-noise burst volume at the end of the fatal-pressure loop.")]
        [SerializeField, Range(0f, 1f)] private float fatalPressureNoiseVolumeMax = 0.45f;

        [Header("Madness Whisper Audio")]
        [Tooltip("Very low 2D whisper/static cue played once when PDA lore is fully replaced by a madness line.")]
        [SerializeField, Range(0f, 1f)] private float madnessWhisperVolume = 0.045f;
        [Tooltip("Minimum cooldown between madness whisper cues so repeated PDA swaps do not stack into noise spam.")]
        [SerializeField, Min(0.1f)] private float madnessWhisperCooldown = 0.9f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER REFERENCE
        // ══════════════════════════════════════════════════════════

        [Header("── Player ────────────────────────────────────")]
        [Tooltip("BuoyancyObject на игроке. Если не назначен —\n" +
                 "ищется автоматически по тегу 'Player' при старте.")]

        [SerializeField] private BuoyancyObject playerBuoyancy; // player acoustic owner ref

        [Header("Underwater Vegetation Overlay")]
        [Tooltip("Optional 2D ambient pulse used when underwater audio moves through dense sargassum fields.")]
        [SerializeField] private AudioClip underwaterSargassumBubblesClip;
        [Tooltip("Optional 2D ambient pulse used when underwater audio moves through dense grass or kelp fields.")]
        [SerializeField] private AudioClip underwaterGrassRustleClip;
        [Tooltip("Minimum global vegetation audio density before underwater vegetation overlays become audible.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationDensityThreshold = 0.16f;
        [Tooltip("Slowest cadence between underwater vegetation overlay pulses.")]
        [SerializeField, Min(0.1f)] private float underwaterVegetationIntervalMax = 2.4f;
        [Tooltip("Fastest cadence between underwater vegetation overlay pulses at peak density.")]
        [SerializeField, Min(0.1f)] private float underwaterVegetationIntervalMin = 0.7f;
        [Tooltip("Minimum overlay volume once underwater vegetation density crosses the threshold.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationVolumeMin = 0.06f;
        [Tooltip("Maximum overlay volume at peak underwater vegetation density.")]
        [SerializeField, Range(0f, 1f)] private float underwaterVegetationVolumeMax = 0.22f;


        [Tooltip("Опциональная ссылка на loop AudioSource с подводным эмбиентом на игроке.\n" +
                 "Если не задана — контроллер лениво ищет первый 2D loop/playOnAwake source под player root.")]
        [SerializeField] private AudioSource playerUnderwaterAmbientSource;
        [Tooltip("Явный AudioMixerGroup для подводного loop источника игрока. Если null — используется AmbientGroup из SpatialAudioManager.")]
        [SerializeField] private AudioMixerGroup playerUnderwaterAmbientMixerGroup;
        [Tooltip("Имя exposed-параметра AudioMixer для частоты low-pass фильтра.")]
        [SerializeField] private string acousticLowPassCutoffParameter = AcousticLowPassCutoffParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для резонанса low-pass фильтра.")]
        [SerializeField] private string acousticLowPassResonanceParameter = AcousticLowPassResonanceParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для decay reverb.")]
        [SerializeField] private string acousticReverbDecayParameter = AcousticReverbDecayParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для reflections level.")]
        [SerializeField] private string acousticReflectionsLevelParameter = AcousticReflectionsLevelParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для reverb level.")]
        [SerializeField] private string acousticReverbLevelParameter = AcousticReverbLevelParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для room HF.")]
        [SerializeField] private string acousticRoomHighFrequencyParameter = AcousticRoomHighFrequencyParameterDefault;
        [Tooltip("Имя exposed-параметра AudioMixer для dry level.")]
        [SerializeField] private string acousticDryLevelParameter = AcousticDryLevelParameterDefault;

        [Header("── Biome Ambient Response ─────────────────────────")]
        [Tooltip("Опциональная ссылка на BiomeMatrixDirector. Если не задана — контроллер лениво резолвит runtime owner.")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        [Tooltip("Период повторной попытки резолва BiomeMatrixDirector в cold/runtime path.")]
        [SerializeField] private float biomeMatrixResolveRetryInterval = 1f;

        [Tooltip("Множитель громкости подводного loop в calm biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float calmAmbientVolumeScale = 0.84f;

        [Tooltip("Множитель громкости подводного loop в lively biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float livelyAmbientVolumeScale = 1.05f;

        [Tooltip("Множитель громкости подводного loop в mixed/neutral biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float mixedAmbientVolumeScale = 0.94f;

        [Tooltip("Множитель громкости подводного loop в hostile biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float hostileAmbientVolumeScale = 0.72f;

        [Tooltip("Множитель pitch подводного loop в calm biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmAmbientPitchScale = 1.02f;

        [Tooltip("Множитель pitch подводного loop в lively biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyAmbientPitchScale = 1.01f;

        [Tooltip("Множитель pitch подводного loop в mixed/neutral biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedAmbientPitchScale = 0.96f;

        [Tooltip("Множитель pitch подводного loop в hostile biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileAmbientPitchScale = 0.90f;

        [Header("── Soundscape Tier Response ────────────────────")]
        // Existing underwater acoustic owner consumes depth-band context directly.
        [Tooltip("Опциональная ссылка на SoundscapeSystem. Если не задана — контроллер лениво резолвит runtime owner.")]
        [SerializeField] private SoundscapeSystem soundscapeSystem;

        [Tooltip("Период повторной попытки резолва SoundscapeSystem в cold/runtime path.")]
        [SerializeField] private float soundscapeResolveRetryInterval = 1f;

        [Tooltip("Множитель громкости подводного loop в shallow tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float shallowTierAmbientVolumeScale = 1f;

        [Tooltip("Множитель громкости подводного loop в twilight tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float twilightTierAmbientVolumeScale = 0.94f;

        [Tooltip("Множитель громкости подводного loop в darkness tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float darknessTierAmbientVolumeScale = 0.88f;

        [Tooltip("Множитель громкости подводного loop в abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float abyssTierAmbientVolumeScale = 0.82f;

        [Tooltip("Множитель громкости подводного loop в deep abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float deepAbyssTierAmbientVolumeScale = 0.74f;

        [Tooltip("Множитель громкости подводного loop в thermal tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float thermalTierAmbientVolumeScale = 0.86f;

        [Tooltip("Множитель pitch подводного loop в shallow tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float shallowTierAmbientPitchScale = 1f;

        [Tooltip("Множитель pitch подводного loop в twilight tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float twilightTierAmbientPitchScale = 0.97f;

        [Tooltip("Множитель pitch подводного loop в darkness tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float darknessTierAmbientPitchScale = 0.93f;

        [Tooltip("Множитель pitch подводного loop в abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float abyssTierAmbientPitchScale = 0.88f;

        [Tooltip("Множитель pitch подводного loop в deep abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float deepAbyssTierAmbientPitchScale = 0.82f;

        [Tooltip("Множитель pitch подводного loop в thermal tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float thermalTierAmbientPitchScale = 0.9f;

        [Header("── Listener Fallback Processing ─────────────")]
        [Tooltip("If mixer snapshot authoring is incomplete, apply listener-level low-pass/reverb fallback so underwater/interior contrast still exists.")]
        [SerializeField] private bool enableSourceLevelAcousticFallback = true;

        [Tooltip("Legacy serialized underwater fallback cutoff retained for inspector compatibility.")]
#pragma warning disable CS0414
        [SerializeField, Range(500f, 22000f)] private float underwaterFallbackLowPassCutoff = 1100f;
#pragma warning restore CS0414

        [Tooltip("Fallback low-pass cutoff for interior listener processing.")]
        [SerializeField, Range(5000f, 22000f)] private float interiorFallbackLowPassCutoff = 16000f;

        [Tooltip("Legacy serialized interior reverb preset retained for inspector compatibility.")]
#pragma warning disable CS0414
        [SerializeField] private AudioReverbPreset interiorFallbackReverbPreset = AudioReverbPreset.Room;
#pragma warning restore CS0414

        [Tooltip("Fallback interior reverb dry level. Exposed so sound design can retune dry/wet balance without code changes.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorFallbackReverbDryLevel = 0f;

        [Header("── Runtime Acoustic Graph Fallback ─────────────")]
        [Tooltip("Continuous low-pass/reverb listener graph used when the authored mixer only contains attenuation.")]
        [SerializeField] private bool enableRuntimeAcousticGraph = true;

        [Tooltip("How quickly runtime fallback filter coefficients chase the target acoustic state.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticGraphFollowSharpness = 7.5f;

        [Tooltip("Decay speed for hull-impact energy injected into the acoustic graph.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticImpactImpulseDecay = 3.6f;

        [Tooltip("Decay speed for active-sonar energy injected into the acoustic graph.")]
        [SerializeField, Range(0.5f, 20f)] private float acousticSonarImpulseDecay = 2.2f;

        [Tooltip("Reference depth used to fully close the underwater low-pass curve.")]
        [SerializeField, Min(1f)] private float acousticDeepWaterReferenceDepth = 240f;

        [Tooltip("Maximum listener low-pass cutoff when underwater but still near the surface.")]
        [SerializeField, Range(500f, 22000f)] private float underwaterGraphShallowCutoff = 1800f;

        [Tooltip("Minimum listener low-pass cutoff when the player is fully committed to the abyss.")]
        [SerializeField, Range(500f, 22000f)] private float underwaterGraphDeepCutoff = 650f;

        [Tooltip("Interior listener low-pass cutoff before collision impulses darken the room tone.")]
        [SerializeField, Range(5000f, 22000f)] private float interiorGraphLowPassCutoff = 15800f;

        [Tooltip("Base resonance used by the underwater low-pass contour.")]
        [SerializeField, Range(0.5f, 3f)] private float underwaterGraphResonance = 1.22f;

        [Tooltip("Base resonance used by the interior low-pass contour.")]
        [SerializeField, Range(0.5f, 3f)] private float interiorGraphResonance = 1.05f;

        [Tooltip("Baseline underwater reverb decay in seconds.")]
        [SerializeField, Range(0.05f, 12f)] private float underwaterGraphDecayTime = 1.35f;

        [Tooltip("Baseline interior reverb decay in seconds.")]
        [SerializeField, Range(0.05f, 12f)] private float interiorGraphDecayTime = 0.95f;

        [Tooltip("Additional interior decay time injected by heavy hull impacts.")]
        [SerializeField, Range(0f, 4f)] private float interiorImpactDecayBoost = 0.65f;

        [Tooltip("How strongly sonar pings temporarily open the underwater low-pass window.")]
        [SerializeField, Range(0f, 1f)] private float sonarGraphOpenUpBoost = 0.35f;

        [Tooltip("How strongly local hull impacts bend the active graph toward metallic ringing.")]
        [SerializeField, Range(0f, 1f)] private float impactGraphMetallicBoost = 0.6f;

        [Tooltip("Maximum distance for feeding a physics impact into the listener acoustic graph.")]
        [SerializeField, Min(0.5f)] private float acousticImpactImpulseRadius = 18f;

        [Tooltip("Underwater reflection level in dB.")]
        [SerializeField, Range(-10000f, 1000f)] private float underwaterGraphReflectionsLevel = -4200f;

        [Tooltip("Interior reflection level in dB.")]
        [SerializeField, Range(-10000f, 1000f)] private float interiorGraphReflectionsLevel = -800f;

        [Tooltip("Underwater late-reverb level in dB.")]
        [SerializeField, Range(-10000f, 2000f)] private float underwaterGraphReverbLevel = -2200f;

        [Tooltip("Interior late-reverb level in dB.")]
        [SerializeField, Range(-10000f, 2000f)] private float interiorGraphReverbLevel = -1200f;

        [Tooltip("Underwater high-frequency room loss in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float underwaterGraphRoomHighFrequency = -6500f;

        [Tooltip("Interior high-frequency room loss in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorGraphRoomHighFrequency = -1450f;

        [Tooltip("Underwater dry level in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float underwaterGraphDryLevel = -800f;

        [Tooltip("Interior dry level in dB.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorGraphDryLevel = -120f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsInterior;
        [SerializeField] private bool _debugIsUnderwater;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private int  _debugTransitionCount;
        [SerializeField] private string _debugFaunaMood;
        [SerializeField] private string _debugAmbientSummary;
        [SerializeField] private string _debugSnapshotCoverage;
        [SerializeField] private string _debugMixerCoverage;
        [SerializeField] private float _debugAmbientVolume;
        [SerializeField] private float _debugAmbientPitch;
        [SerializeField] private float _debugStormInterference;
        [SerializeField] private string _debugSoundscapeTier;
        [SerializeField] private float _debugSoundscapeVolumeScale = 1f;
        [SerializeField] private float _debugSoundscapePitchScale = 1f;
        [SerializeField] private float _debugAcousticLowPassCutoff = 22000f;
        [SerializeField] private float _debugAcousticReverbDecay = 0f;
        [SerializeField] private float _debugImpactImpulse;
        [SerializeField] private float _debugSonarImpulse;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Последнее известное состояние: true = интерьер (сухая зона).
        /// Используется для edge detection. -1-like: первый кадр
        /// определяет начальное состояние без запуска перехода.
        /// </summary>
        private AcousticZoneState _lastZone;

        /// <summary>
        /// Флаг: начальное состояние уже определено.
        /// false = первый Tick ещё не прошёл.
        /// Предотвращает ложный переход при старте.
        /// </summary>
        private bool _stateInitialized;

        /// <summary>
        /// Registration tracking для GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;
        private float _nextPlayerResolveTime;
        private const float PlayerResolveRetryInterval = 1f;
        private const float SurfaceWeatherStateEpsilon = 0.001f;
        private float _nextBiomeMatrixResolveTime;
        private float _nextSoundscapeResolveTime;
        private HectonAtmosphereManager _atmosphereManager;
        private HectonPlayerMovement _playerMovement;
        private bool _fallbackUnderwaterState;
        private bool _acousticUnderwaterState;
        private bool _hasPendingExteriorZone;
        private float _pendingExteriorZoneResolveTime;
        private AcousticZoneState _pendingExteriorZone;
        private float _nextExteriorTransitionAllowedTime;
        private bool _hasCachedExteriorZone;
        private AcousticZoneState _cachedExteriorZone;
        private List<AudioSource> _playerAudioSources;
        private AudioSource _cachedAmbientSource;
        private AudioListener _cachedPlayerAudioListener;
        private Transform _lastAmbientSourceSearchRoot;
        private float _nextAmbientSourceHierarchyResolveTime;
        private bool _ambientSourceDefaultsCaptured;
        private bool _listenerFallbackDefaultsCaptured;
        private float _ambientSourceBaseVolume = 1f;
        private float _ambientSourceBasePitch = 1f;
        private float _listenerLowPassBaseCutoff = 22000f;
        private float _listenerLowPassBaseResonance = 1f;
        private float _listenerReverbBaseDryLevel;
        private float _listenerReverbBaseDecayTime = 1f;
        private float _listenerReverbBaseReflectionsLevel = -10000f;
        private float _listenerReverbBaseReverbLevel = -10000f;
        private float _listenerReverbBaseRoomHighFrequency = 0f;
        private bool _acousticMixerBindingsResolved;
        private bool _acousticMixerBindingsValid;
        private string _resolvedAcousticLowPassCutoffParameter;
        private string _resolvedAcousticLowPassResonanceParameter;
        private string _resolvedAcousticReverbDecayParameter;
        private string _resolvedAcousticReflectionsLevelParameter;
        private string _resolvedAcousticReverbLevelParameter;
        private string _resolvedAcousticRoomHighFrequencyParameter;
        private string _resolvedAcousticDryLevelParameter;
        private bool _warnedMissingAcousticMixerParameters;
        private float _snapshotTransitionLockUntilTime;
        private HectonBiomeMatrixProfile _lastBiomeProfileForAmbient;
        private int _currentAmbientSurvivalPressure;
        private int _currentAmbientRewardPull;
        private string _currentAmbientSummary;
        private float _currentAmbientVolumeScale = 1f;
        private float _currentAmbientPitchScale = 1f;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _currentSoundscapeVolumeScale = 1f;
        private float _currentSoundscapePitchScale = 1f;
        private float _surfacePrecipitationIntensity;
        private float _surfaceElectricalActivity;
        private float _stormInterferencePulseTimer;
        private float _stormAmbientInterference;
        private float _stormAmbientFlutterPhase;
        private float _stormAmbientFlutter;
        private bool _stormStaticUsePrimaryNext = true;
        private float _underwaterVegetationPulseTimer;
        private float _fatalPressureNoiseTimer;
        private float _nextMadnessWhisperTime;
        private bool _fatalPressureNoiseUsePrimaryNext = true;
        private bool _snapshotBindingsResolved;
        private bool _warnedMissingInteriorSnapshot;
        private bool _warnedMissingUnderwaterSnapshot;
        private bool _warnedMissingSurfaceSnapshotSet;
        private bool _warnedMissingSnapshotCoverage;
        private bool _warnedIncompleteMixerSnapshotAuthoring;
        private int _validatedMixerSnapshotCount;
        private float _acousticImpactImpulse;
        private float _acousticSonarImpulse;
        private float _currentAcousticLowPassCutoffHz = 22000f;
        private float _currentAcousticLowPassResonanceQ = 1f;
        private float _currentAcousticReverbDecayTime = 0f;
        private float _currentAcousticReflectionsLevelDb = -10000f;
        private float _currentAcousticReverbLevelDb = -10000f;
        private float _currentAcousticRoomHighFrequencyDb = 0f;
        private float _currentAcousticDryLevelDb = 0f;
        private float _lastAppliedAcousticLowPassCutoffHz = float.NaN;
        private float _lastAppliedAcousticLowPassResonanceQ = float.NaN;
        private float _lastAppliedAcousticReverbDecayTime = float.NaN;
        private float _lastAppliedAcousticReflectionsLevelDb = float.NaN;
        private float _lastAppliedAcousticReverbLevelDb = float.NaN;
        private float _lastAppliedAcousticRoomHighFrequencyDb = float.NaN;
        private float _lastAppliedAcousticDryLevelDb = float.NaN;
        private bool _acousticGraphStateInitialized;
        private bool _validatedMixerHasNamedCoverage;
        private bool _validatedMixerHasEffectGraph;
        private bool _usingSourceLevelAcousticFallback;
        private int _resolvedEmitterOcclusionLayerMask;
        private float _emitterOcclusionTransmission01 = 1f;
        private float _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
        private bool _hasPendingSnapshotTransition;
        private AcousticZoneState _pendingSnapshotZone;
        private float _pendingSnapshotDuration;
        private const float AcousticCutoffWriteEpsilonHz = 8f;
        private const float AcousticResonanceWriteEpsilon = 0.01f;
        private const float AcousticDecayWriteEpsilonSeconds = 0.01f;
        private const float AcousticDbWriteEpsilon = 0.1f;
        // COLD ALLOC: AudioMixerSnapshot[3] — surface weather snapshot blend targets — owner: AcousticZoneController
        private readonly AudioMixerSnapshot[] _surfaceBlendSnapshots = new AudioMixerSnapshot[3];
        // COLD ALLOC: float[3] — surface weather snapshot blend weights — owner: AcousticZoneController
        private readonly float[] _surfaceBlendWeights = new float[3];
        private bool _hasActiveResolvedSnapshotState;
        private bool _activeSurfaceBlendState;
        private AcousticZoneState _activeResolvedZone;
        private AudioMixerSnapshot _activeResolvedSnapshot;
        private int _activeSurfaceBlendSnapshotCount;
        // COLD ALLOC: AudioMixerSnapshot[3] — last applied surface weather snapshot blend targets — owner: AcousticZoneController
        private readonly AudioMixerSnapshot[] _activeSurfaceBlendSnapshots = new AudioMixerSnapshot[3];
        // COLD ALLOC: float[3] — last applied surface weather snapshot blend weights — owner: AcousticZoneController
        private readonly float[] _activeSurfaceBlendWeights = new float[3];
        // COLD ALLOC: ActiveEmitterSample[24] — pooled world-emitter acoustic occlusion sample buffer — owner: AcousticZoneController
        private static readonly SpatialAudioManager.ActiveEmitterSample[] s_emitterOcclusionSamples =
            new SpatialAudioManager.ActiveEmitterSample[AcousticEmitterSampleCapacity];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true если игрок сейчас в сухой зоне (интерьер базы).
        /// false если в воде.
        /// </summary>
        public bool IsInterior => _lastZone == AcousticZoneState.Interior;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _stateInitialized = false;
            _registeredToTickManager = false;
            // COLD ALLOC: List<AudioSource>[8] — reused player-local audio scan buffer — owner: AcousticZoneController
            _playerAudioSources = new List<AudioSource>(8);

#if UNITY_EDITOR
            TryAssignEditorAuthoringDefaults();
#endif
        }

        private void OnEnable()
        {
            TryRegister();
            HectonAtmosphereManager.OnStateChanged += HandleAtmosphereStateChanged;
            SoundscapeEvents.RegisterTierChanged(HandleSoundscapeTierChanged);
            PhysicsEvents.OnImpact += HandlePhysicsImpact;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutterPhase = 0f;
            _stormAmbientFlutter = 0f;
            _stormStaticUsePrimaryNext = true;
            _underwaterVegetationPulseTimer = 0f;
            _fatalPressureNoiseTimer = 0f;
            _nextMadnessWhisperTime = 0f;
            _fatalPressureNoiseUsePrimaryNext = true;
            _acousticImpactImpulse = 0f;
            _acousticSonarImpulse = 0f;
            _resolvedEmitterOcclusionLayerMask = 0;
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            ResolveBiomeMatrixDirector(true);
            RefreshSoundscapeTierContext(true);
        }

        private void Start()
        {
            // ── Ленивый поиск игрока ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(true);
            }

            // ── Deferred registration ──
            if (!_registeredToTickManager)
            {
                TryRegister();
            }

            if (!_registeredToTickManager)
            {
                Debug.LogError(
                    "[AcousticZoneController] GameTickManager not found at Start(). " +
                    "Acoustic transitions will NOT work.", this);
            }

            ResolvePlayerAmbientSource();
            ResolvePlayerListenerFilters();
            ResolveBiomeMatrixDirector(true);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(true);
            EnsureSnapshotBindings();
            RefreshAtmosphereZoneCache();

            // ── Установка начального snapshot без перехода ──
            ApplyInitialSnapshot();
        }

        private void OnDisable()
        {
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;
            SoundscapeEvents.UnregisterTierChanged(HandleSoundscapeTierChanged);
            PhysicsEvents.OnImpact -= HandlePhysicsImpact;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutterPhase = 0f;
            _stormAmbientFlutter = 0f;
            _underwaterVegetationPulseTimer = 0f;
            _fatalPressureNoiseTimer = 0f;
            _nextMadnessWhisperTime = 0f;
            _acousticImpactImpulse = 0f;
            _acousticSonarImpulse = 0f;
            ResetSourceLevelAcousticFallback();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;
            SoundscapeEvents.UnregisterTierChanged(HandleSoundscapeTierChanged);
            PhysicsEvents.OnImpact -= HandlePhysicsImpact;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            ResetSourceLevelAcousticFallback();

            if (_instance == this)
            {
                _instance = null;
                OnAcousticZoneChanged = null;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — ACOUSTIC ZONE DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет состояние игрока каждый кадр.
        /// Edge detection: переход запускается ТОЛЬКО при смене
        /// IsInAir (false→true или true→false).
        ///
        /// CPU cost: ~0.0001ms (один bool read + comparison).
        /// Нет аллокаций, нет сложной логики.
        ///
        /// Почему ITickable а не ISlowTickable:
        ///   Аудио-переход должен начинаться МГНОВЕННО при смене зоны.
        ///   Задержка 0.5с (SlowTick) заметна игроку — звук "запаздывает"
        ///   относительно визуального перехода через шлюз.
        ///   Один bool per frame — ничтожная нагрузка даже на MX350.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ── Ленивый поиск игрока (если ещё не найден) ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(false);
                if (playerBuoyancy == null)
                    return; // Игрок не найден — skip
            }

            // ── Unity destroyed object check ──
            if ((object)playerBuoyancy == null || playerBuoyancy == null)
            {
                playerBuoyancy = null;
                return;
            }

            // ── Текущее состояние ──
            AcousticZoneState currentZone = ResolveCurrentZone();
            currentZone = ResolveStableZone(currentZone);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(false);
            UpdateStormInterferenceAudio(currentZone, deltaTime);
            UpdateAmbientLoopMix(currentZone);
            UpdateUnderwaterVegetationOverlay(currentZone, deltaTime);
            UpdateFatalPressureLoopAudio(currentZone, deltaTime);
            UpdateSourceLevelAcousticGraph(currentZone, deltaTime);

            // ── Первый кадр: установить начальное состояние без перехода ──
            if (!_stateInitialized)
            {
                ApplyInitialSnapshot(currentZone);
                return;
            }

            ProcessPendingSnapshotTransition();

            // ── Edge detection: переход только при СМЕНЕ состояния ──
            if (currentZone == _lastZone)
                return;

            // ══════════════════════════════════════════════
            //  TRANSITION DETECTED!
            // ══════════════════════════════════════════════

            _lastZone = currentZone;
            if (currentZone != AcousticZoneState.Interior)
                _nextExteriorTransitionAllowedTime = Time.unscaledTime + exteriorTransitionHoldTime;

            ApplyZoneTransition(currentZone);

            // ── Событие для внешних систем ──
            OnAcousticZoneChanged?.Invoke(currentZone == AcousticZoneState.Interior);

            UpdateDiagnostics(currentZone);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Плавный переход в интерьер базы.
        ///
        /// AudioMixerSnapshot.TransitionTo(timeToReach):
        ///   Плавно переводит AudioMixer в состояние snapshot'а
        ///   за указанное время. Unity внутренне интерполирует
        ///   ВСЕ параметры mixer'а (volume, LPF cutoff, reverb wet, etc.).
        ///   Zero GC — нативная операция.
        ///
        /// Переходный звук (waterDrainSound):
        ///   Воспроизводится через PlayStatic2D (2D, "внутри шлема").
        ///   Имитирует шипение откачиваемой воды из шлюза.
        ///   Длительность клипа должна примерно совпадать с transitionDuration.
        /// </summary>
        private void TransitionToInterior()
        {
            ApplyAmbientLoopState(AcousticZoneState.Interior);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Interior, interiorTransitionDuration))
                return;

            // ── Переходный звук ──
            PlayTransitionSound(waterDrainSound);

            LogDiagnostic($"[AcousticZoneController] Interior (dry zone). Transition: {interiorTransitionDuration}s");
        }

        /// <summary>
        /// Плавный переход в подводную среду.
        ///
        /// underwaterTransitionDuration может быть короче transitionDuration,
        /// т.к. "заполнение водой" физически быстрее, чем "откачка".
        /// Это создаёт асимметричный, более реалистичный переход:
        ///   Вход в базу: 2.0с (медленная откачка, шипение)
        ///   Выход в воду: 1.5с (быстрое заполнение, бульканье)
        /// </summary>
        private void TransitionToSurface()
        {
            ApplyAmbientLoopState(AcousticZoneState.Surface);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceTransitionDuration))
                return;

            LogDiagnostic($"[AcousticZoneController] Surface/open air. Transition: {surfaceTransitionDuration}s");
        }

        private void TransitionToUnderwater()
        {
            ApplyAmbientLoopState(AcousticZoneState.Underwater);
            if (!TransitionToResolvedSnapshot(AcousticZoneState.Underwater, underwaterTransitionDuration))
                return;

            // ── Переходный звук ──
            PlayTransitionSound(waterFillSound);

            LogDiagnostic($"[AcousticZoneController] Underwater. Transition: {underwaterTransitionDuration}s");
        }

        /// <summary>
        /// Устанавливает начальный snapshot БЕЗ перехода (мгновенно).
        /// Вызывается в Start() для корректного начального состояния.
        ///
        /// TransitionTo(0f) — мгновенное переключение (Unity поддерживает 0).
        /// </summary>
        private void ApplyInitialSnapshot()
        {
            if (playerBuoyancy == null) return;

            ApplyInitialSnapshot(ResolveCurrentZone());
        }

        private void ApplyInitialSnapshot(AcousticZoneState zone)
        {
            _lastZone = zone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = 0f;
            ApplyAmbientLoopState(zone);
            TransitionToResolvedSnapshot(zone, 0f);

            UpdateDiagnostics(zone);

            LogDiagnostic($"[AcousticZoneController] Initial zone: {zone}");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION SOUND
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Воспроизводит переходный звук через SpatialAudioManager.
        /// 2D (PlayStatic2D) — звук "внутри шлема", не позиционный.
        ///
        /// Null-safe для clip и SpatialAudioManager.
        /// </summary>
        private void PlayTransitionSound(AudioClip clip)
        {
            if (clip == null) return;

            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (sam == null)
                return;

            sam.PlayStatic2D(clip, transitionVolume);
        }

        internal void PlayMadnessWhisperCue()
        {
            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (Time.unscaledTime < _nextMadnessWhisperTime || sam == null)
                return;

            AudioClip clip = stormStaticPrimary;
            if (clip == null)
                clip = stormStaticSecondary;

            if (clip == null)
                clip = fatalPressureNoisePrimary;

            if (clip == null)
                clip = fatalPressureNoiseSecondary;

            if (clip == null)
                return;

            sam.PlayStatic2D(clip, madnessWhisperVolume, sam.InterfaceGroup);
            _nextMadnessWhisperTime = Time.unscaledTime + Mathf.Max(0.1f, madnessWhisperCooldown);
        }

        private AudioMixerSnapshot ResolveSurfaceSnapshot()
        {
            EnsureSnapshotBindings();

            if (!HasAnyResolvedSnapshotCoverage())
                return null;

            if (_surfaceElectricalActivity >= 0.55f && surfaceStormSnapshot != null)
                return surfaceStormSnapshot;

            if (_surfacePrecipitationIntensity >= 0.2f && surfaceRainSnapshot != null)
                return surfaceRainSnapshot;

            if (surfaceSnapshot != null)
                return surfaceSnapshot;

            if (baseInteriorSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to BaseInteriorSnapshot. Author Surface/SurfaceRain/SurfaceStorm snapshots in MasterMixer.");
                return baseInteriorSnapshot;
            }

            if (underwaterSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to UnderwaterSnapshot because no dry/exterior snapshot is authored.");
                return underwaterSnapshot;
            }

            LogSnapshotFallbackWarningOnce(
                ref _warnedMissingSurfaceSnapshotSet,
                "[AcousticZoneController] Surface snapshot set missing and no fallback snapshot exists. Surface acoustic transitions will keep the previous mixer state.");
            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ленивый resolve BuoyancyObject на текущем игроке через SceneBootstrap.
        /// Вызывается один раз. Если игрок ещё не готов — повторяет позже в Tick.
        ///
        /// TryGetComponent — zero GC.
        /// TryGetComponent — zero GC.
        /// </summary>
        private void FindPlayerBuoyancy(bool force)
        {
            if (!force && Time.unscaledTime < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = Time.unscaledTime + PlayerResolveRetryInterval;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                playerTransform.TryGetComponent(out playerBuoyancy);
                playerTransform.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(playerTransform);
                ResolvePlayerListenerFilters(playerTransform);
            }

            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — MANUAL CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Принудительный переход в указанную зону.
        /// Используется из внешних систем (скриптовые сцены, читы, тесты).
        ///
        /// Пример: AcousticZoneController.Instance.ForceZone(true); // Interior
        /// </summary>
        /// <param name="isInterior">true = интерьер, false = подводная.</param>
        public void ForceZone(bool isInterior)
        {
            AcousticZoneState forcedZone = isInterior
                ? AcousticZoneState.Interior
                : AcousticZoneState.Underwater;

            if (forcedZone == _lastZone && _stateInitialized)
                return; // Уже в нужной зоне

            _lastZone = forcedZone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = forcedZone == AcousticZoneState.Interior
                ? 0f
                : Time.unscaledTime + exteriorTransitionHoldTime;

            ApplyZoneTransition(forcedZone);

            OnAcousticZoneChanged?.Invoke(isInterior);
            UpdateDiagnostics(forcedZone);
        }

        /// <summary>
        /// Устанавливает BuoyancyObject игрока в рантайме.
        /// Вызывается при респавне игрока или смене контроллера.
        /// </summary>
        public void SetPlayerBuoyancy(BuoyancyObject buoyancy)
        {
            playerBuoyancy = buoyancy;
            _playerMovement = null;
            playerUnderwaterAmbientSource = null;
            _cachedPlayerAudioListener = null;
            _cachedAmbientSource = null;
            _lastAmbientSourceSearchRoot = null;
            _nextAmbientSourceHierarchyResolveTime = 0f;
            _listenerFallbackDefaultsCaptured = false;
            _acousticMixerBindingsResolved = false;
            _acousticMixerBindingsValid = false;
            InvalidateAppliedAcousticMixerStateCache();
            _snapshotTransitionLockUntilTime = 0f;
            _hasPendingSnapshotTransition = false;
            _pendingSnapshotDuration = 0f;
            _hasPendingExteriorZone = false;
            if (buoyancy != null)
            {
                buoyancy.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(buoyancy.transform);
                ResolvePlayerListenerFilters(buoyancy.transform);
            }
            _stateInitialized = false; // Переинициализация при следующем Tick
            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(AcousticZoneState zone)
        {
            _debugIsInterior = zone == AcousticZoneState.Interior;
            _debugIsUnderwater = zone == AcousticZoneState.Underwater;
            _debugTransitionCount++;
            _debugFaunaMood = ResolveAmbientMoodLabel();
            _debugAmbientSummary = string.IsNullOrWhiteSpace(_currentAmbientSummary) ? "None" : _currentAmbientSummary;
            _debugSnapshotCoverage = BuildSnapshotCoverageSummary();
            _debugMixerCoverage = BuildMixerCoverageSummary();
            _debugAmbientVolume = _ambientSourceBaseVolume * _currentAmbientVolumeScale * _currentSoundscapeVolumeScale;
            _debugAmbientPitch = _ambientSourceBasePitch * _currentAmbientPitchScale * _currentSoundscapePitchScale;
            _debugSoundscapeTier = ResolveSoundscapeTierLabel(_currentSoundscapeTier);
            _debugSoundscapeVolumeScale = _currentSoundscapeVolumeScale;
            _debugSoundscapePitchScale = _currentSoundscapePitchScale;
            _debugAcousticLowPassCutoff = _currentAcousticLowPassCutoffHz;
            _debugAcousticReverbDecay = _currentAcousticReverbDecayTime;
            _debugImpactImpulse = _acousticImpactImpulse;
            _debugSonarImpulse = _acousticSonarImpulse;
            if (_usingSourceLevelAcousticFallback)
                _debugMixerCoverage += " | MixerParamFallback";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdatePlayerFoundDiagnostic()
        {
            _debugPlayerFound = playerBuoyancy != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogDiagnostic(string message)
        {
            Debug.Log(message, this);
        }

        private AcousticZoneState ResolveCurrentZone()
        {
            if (playerBuoyancy != null && playerBuoyancy.IsInDryZone)
                return AcousticZoneState.Interior;

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
            {
                _acousticUnderwaterState = ResolveMovementDrivenExteriorState(movement);

                return _acousticUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
            }

            if (_hasCachedExteriorZone)
                return _cachedExteriorZone;

            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere != null)
            {
                AcousticZoneState zone = atmosphere.CurrentState == EnvironmentState.UNDERWATER
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
                _cachedExteriorZone = zone;
                _hasCachedExteriorZone = true;
                return zone;
            }

            _fallbackUnderwaterState =
                SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    ResolvePlayerDepthFallback(),
                    _fallbackUnderwaterState,
                    acousticEnterUnderwaterDepth,
                    acousticExitUnderwaterDepth);

            return _fallbackUnderwaterState
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
        }

        private bool ResolveMovementDrivenExteriorState(HectonPlayerMovement movement)
        {
            float depth = Mathf.Max(0f, movement.CurrentDepth);
            float immersion = Mathf.Clamp01(movement.WaterImmersionRatio);
            bool headSubmerged = movement.IsPlayerSubmerged || depth > 0f;

            if (headSubmerged || depth >= acousticForceUnderwaterDepth)
                return true;

            if (_acousticUnderwaterState)
            {
                if (immersion <= acousticExitImmersionRatio && depth <= acousticExitUnderwaterDepth)
                    return false;

                return depth > acousticExitUnderwaterDepth || immersion > acousticExitImmersionRatio;
            }

            if (depth < acousticEnterUnderwaterDepth)
                return false;

            return immersion >= acousticEnterImmersionRatio;
        }

        private AcousticZoneState ResolveStableZone(AcousticZoneState candidateZone)
        {
            if (!_stateInitialized)
                return candidateZone;

            if (candidateZone == AcousticZoneState.Interior || _lastZone == AcousticZoneState.Interior)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            if (candidateZone == _lastZone)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            float now = Time.unscaledTime;
            if (now < _nextExteriorTransitionAllowedTime)
            {
                _hasPendingExteriorZone = false;
                return _lastZone;
            }

            if (!_hasPendingExteriorZone || _pendingExteriorZone != candidateZone)
            {
                _pendingExteriorZone = candidateZone;
                _pendingExteriorZoneResolveTime = now + exteriorTransitionDebounce;
                _hasPendingExteriorZone = true;
                return _lastZone;
            }

            if (now < _pendingExteriorZoneResolveTime)
                return _lastZone;

            _hasPendingExteriorZone = false;
            return candidateZone;
        }

        private float ResolvePlayerDepthFallback()
        {
            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
                return movement.CurrentDepth;

            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere != null && atmosphere.CurrentState == EnvironmentState.UNDERWATER)
                return acousticEnterUnderwaterDepth;

            return 0f;
        }

        private void HandleAtmosphereStateChanged(EnvironmentState state)
        {
            _cachedExteriorZone = state == EnvironmentState.UNDERWATER
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
            _hasCachedExteriorZone = true;
        }

        private void ResolveBiomeMatrixDirector(bool force)
        {
            if (biomeMatrixDirector != null)
                return;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextBiomeMatrixResolveTime)
                return;

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            _nextBiomeMatrixResolveTime = currentTime + biomeMatrixResolveRetryInterval;
        }

        private void ResolveSoundscapeSystem(bool force)
        {
            if (soundscapeSystem != null)
                return;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextSoundscapeResolveTime)
                return;

            soundscapeSystem = SoundscapeSystem.Instance;
            _nextSoundscapeResolveTime = currentTime + soundscapeResolveRetryInterval;
        }

        private void RefreshSoundscapeTierContext(bool force)
        {
            ResolveSoundscapeSystem(force);

            SoundscapeTier tier = soundscapeSystem != null
                ? soundscapeSystem.CurrentTier
                : SoundscapeTier.Shallow;

            ApplySoundscapeTierContext(tier);
        }

        private void ApplySoundscapeTierContext(SoundscapeTier tier)
        {
            _currentSoundscapeTier = tier;
            _currentSoundscapeVolumeScale = shallowTierAmbientVolumeScale;
            _currentSoundscapePitchScale = shallowTierAmbientPitchScale;

            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    _currentSoundscapeVolumeScale = twilightTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = twilightTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Darkness:
                    _currentSoundscapeVolumeScale = darknessTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = darknessTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Abyss:
                    _currentSoundscapeVolumeScale = abyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = abyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.DeepAbyss:
                    _currentSoundscapeVolumeScale = deepAbyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = deepAbyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Thermal:
                    _currentSoundscapeVolumeScale = thermalTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = thermalTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    break;
            }
        }

        private void HandleSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            ApplySoundscapeTierContext(newTier);
        }

        private void RefreshBiomeAmbientContext()
        {
            ResolveBiomeMatrixDirector(false);

            HectonBiomeMatrixProfile profile = biomeMatrixDirector != null
                ? biomeMatrixDirector.CurrentProfile
                : null;

            if (ReferenceEquals(profile, _lastBiomeProfileForAmbient))
                return;

            _lastBiomeProfileForAmbient = profile;
            _currentAmbientSurvivalPressure = 0;
            _currentAmbientRewardPull = 0;
            _currentAmbientSummary = null;
            _currentAmbientVolumeScale = 1f;
            _currentAmbientPitchScale = 1f;

            if (profile == null)
                return;

            _currentAmbientSurvivalPressure = profile.survivalPressure;
            _currentAmbientRewardPull = profile.rewardPull;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            if (familyProfile != null)
            {
                HectonFaunaFamilyProfile faunaFamilyProfile = familyProfile.faunaFamilyProfile;
                if (faunaFamilyProfile != null)
                    _currentAmbientSummary = faunaFamilyProfile.ambienceSummary;
            }

            if (_currentAmbientSurvivalPressure >= 4)
            {
                _currentAmbientVolumeScale = hostileAmbientVolumeScale;
                _currentAmbientPitchScale = hostileAmbientPitchScale;
                return;
            }

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
            {
                _currentAmbientVolumeScale = livelyAmbientVolumeScale;
                _currentAmbientPitchScale = livelyAmbientPitchScale;
                return;
            }

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
            {
                _currentAmbientVolumeScale = calmAmbientVolumeScale;
                _currentAmbientPitchScale = calmAmbientPitchScale;
                return;
            }

            _currentAmbientVolumeScale = mixedAmbientVolumeScale;
            _currentAmbientPitchScale = mixedAmbientPitchScale;
        }

        private void ApplyZoneTransition(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    TransitionToInterior();
                    break;

                case AcousticZoneState.Surface:
                    TransitionToSurface();
                    break;

                default:
                    TransitionToUnderwater();
                    break;
            }
        }

        private HectonAtmosphereManager ResolveAtmosphereManager()
        {
            if (_atmosphereManager == null)
                _atmosphereManager = HectonAtmosphereManager.Instance;

            return _atmosphereManager;
        }

        private void RefreshAtmosphereZoneCache()
        {
            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere == null)
            {
                _hasCachedExteriorZone = false;
                return;
            }

            HandleAtmosphereStateChanged(atmosphere.CurrentState);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement == null && playerBuoyancy != null)
                playerBuoyancy.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private AudioSource ResolvePlayerAmbientSource()
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
            {
                EnsureAmbientSourceMixerRouting(playerUnderwaterAmbientSource);
                CacheAmbientSourceDefaults(playerUnderwaterAmbientSource);
                return playerUnderwaterAmbientSource;
            }

            playerUnderwaterAmbientSource = null;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                ResolvePlayerAmbientSource(playerTransform);

            return playerUnderwaterAmbientSource;
        }

        private void ResolvePlayerAmbientSource(Transform playerTransform)
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
                return;

            if (playerTransform == null || _playerAudioSources == null)
                return;

            if (_lastAmbientSourceSearchRoot != playerTransform)
            {
                _lastAmbientSourceSearchRoot = playerTransform;
                _nextAmbientSourceHierarchyResolveTime = 0f;
            }

            if (Time.unscaledTime < _nextAmbientSourceHierarchyResolveTime)
                return;

            _playerAudioSources.Clear();
            playerTransform.GetComponentsInChildren(true, _playerAudioSources);

            int count = _playerAudioSources.Count;
            for (int i = 0; i < count; i++)
            {
                AudioSource candidate = _playerAudioSources[i];
                if (candidate == null || candidate.clip == null)
                    continue;

                if (!candidate.loop || candidate.spatialBlend > 0.01f)
                    continue;

                if (!candidate.playOnAwake && !candidate.isPlaying)
                    continue;

                playerUnderwaterAmbientSource = candidate;
                EnsureAmbientSourceMixerRouting(candidate);
                CacheAmbientSourceDefaults(candidate);
                return;
            }

            _nextAmbientSourceHierarchyResolveTime = Time.unscaledTime + AmbientSourceResolveRetryInterval;
        }

        private void CacheAmbientSourceDefaults(AudioSource ambientSource)
        {
            if (ambientSource == null)
                return;

            EnsureAmbientSourceMixerRouting(ambientSource);
            if (_cachedAmbientSource == ambientSource && _ambientSourceDefaultsCaptured)
                return;

            _cachedAmbientSource = ambientSource;
            _ambientSourceBaseVolume = ambientSource.volume;
            _ambientSourceBasePitch = ambientSource.pitch;
            _ambientSourceDefaultsCaptured = true;
        }

        private void EnsureAmbientSourceMixerRouting(AudioSource ambientSource)
        {
            if (ambientSource == null)
                return;

            if (playerUnderwaterAmbientMixerGroup != null)
            {
                if (ambientSource.outputAudioMixerGroup != playerUnderwaterAmbientMixerGroup)
                    ambientSource.outputAudioMixerGroup = playerUnderwaterAmbientMixerGroup;
                return;
            }

            if (ambientSource.outputAudioMixerGroup == null &&
                Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService spatialAudioManager &&
                spatialAudioManager != null &&
                spatialAudioManager.AmbientGroup != null)
            {
                ambientSource.outputAudioMixerGroup = spatialAudioManager.AmbientGroup;
            }
        }

        private AudioListener ResolvePlayerListenerFilters()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                ResolvePlayerListenerFilters(playerTransform);

            return _cachedPlayerAudioListener;
        }

        private void ResolvePlayerListenerFilters(Transform playerTransform)
        {
            if (playerTransform == null)
                return;

            AudioListener listener = _cachedPlayerAudioListener;
            if ((object)listener == null || listener == null)
            {
                if (!playerTransform.TryGetComponent(out listener))
                    listener = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerTransform);

                _cachedPlayerAudioListener = listener;
            }

            if ((object)listener == null || listener == null)
                return;

            EnsureAcousticMixerParameterBindings();
        }

        private bool EnsureAcousticMixerParameterBindings()
        {
            if (_acousticMixerBindingsResolved)
                return _acousticMixerBindingsValid;

            _acousticMixerBindingsResolved = true;
            _acousticMixerBindingsValid = false;

            if (masterMixer == null)
                return false;

            _resolvedAcousticLowPassCutoffParameter = ResolveAcousticMixerParameterName(acousticLowPassCutoffParameter, AcousticLowPassCutoffParameterDefault);
            _resolvedAcousticLowPassResonanceParameter = ResolveAcousticMixerParameterName(acousticLowPassResonanceParameter, AcousticLowPassResonanceParameterDefault);
            _resolvedAcousticReverbDecayParameter = ResolveAcousticMixerParameterName(acousticReverbDecayParameter, AcousticReverbDecayParameterDefault);
            _resolvedAcousticReflectionsLevelParameter = ResolveAcousticMixerParameterName(acousticReflectionsLevelParameter, AcousticReflectionsLevelParameterDefault);
            _resolvedAcousticReverbLevelParameter = ResolveAcousticMixerParameterName(acousticReverbLevelParameter, AcousticReverbLevelParameterDefault);
            _resolvedAcousticRoomHighFrequencyParameter = ResolveAcousticMixerParameterName(acousticRoomHighFrequencyParameter, AcousticRoomHighFrequencyParameterDefault);
            _resolvedAcousticDryLevelParameter = ResolveAcousticMixerParameterName(acousticDryLevelParameter, AcousticDryLevelParameterDefault);

            if (!masterMixer.GetFloat(_resolvedAcousticLowPassCutoffParameter, out _listenerLowPassBaseCutoff) ||
                !masterMixer.GetFloat(_resolvedAcousticLowPassResonanceParameter, out _listenerLowPassBaseResonance) ||
                !masterMixer.GetFloat(_resolvedAcousticReverbDecayParameter, out _listenerReverbBaseDecayTime) ||
                !masterMixer.GetFloat(_resolvedAcousticReflectionsLevelParameter, out _listenerReverbBaseReflectionsLevel) ||
                !masterMixer.GetFloat(_resolvedAcousticReverbLevelParameter, out _listenerReverbBaseReverbLevel) ||
                !masterMixer.GetFloat(_resolvedAcousticRoomHighFrequencyParameter, out _listenerReverbBaseRoomHighFrequency) ||
                !masterMixer.GetFloat(_resolvedAcousticDryLevelParameter, out _listenerReverbBaseDryLevel))
            {
                LogMissingAcousticMixerParameterWarning();
                return false;
            }

            _currentAcousticLowPassCutoffHz = _listenerLowPassBaseCutoff;
            _currentAcousticLowPassResonanceQ = _listenerLowPassBaseResonance;
            _currentAcousticReverbDecayTime = _listenerReverbBaseDecayTime;
            _currentAcousticReflectionsLevelDb = _listenerReverbBaseReflectionsLevel;
            _currentAcousticReverbLevelDb = _listenerReverbBaseReverbLevel;
            _currentAcousticRoomHighFrequencyDb = _listenerReverbBaseRoomHighFrequency;
            _currentAcousticDryLevelDb = _listenerReverbBaseDryLevel;
            _acousticGraphStateInitialized = false;
            _listenerFallbackDefaultsCaptured = true;
            _acousticMixerBindingsValid = true;
            InvalidateAppliedAcousticMixerStateCache();
            return true;
        }

        private bool ApplyAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            if (masterMixer == null)
                return false;

            if (HasAppliedAcousticMixerState(
                    lowPassCutoffHz,
                    lowPassResonanceQ,
                    reverbDecayTime,
                    reflectionsLevelDb,
                    reverbLevelDb,
                    roomHighFrequencyDb,
                    dryLevelDb))
            {
                return true;
            }

            if (!masterMixer.SetFloat(_resolvedAcousticLowPassCutoffParameter, lowPassCutoffHz) ||
                !masterMixer.SetFloat(_resolvedAcousticLowPassResonanceParameter, lowPassResonanceQ) ||
                !masterMixer.SetFloat(_resolvedAcousticReverbDecayParameter, reverbDecayTime) ||
                !masterMixer.SetFloat(_resolvedAcousticReflectionsLevelParameter, reflectionsLevelDb) ||
                !masterMixer.SetFloat(_resolvedAcousticReverbLevelParameter, reverbLevelDb) ||
                !masterMixer.SetFloat(_resolvedAcousticRoomHighFrequencyParameter, roomHighFrequencyDb) ||
                !masterMixer.SetFloat(_resolvedAcousticDryLevelParameter, dryLevelDb))
            {
                _acousticMixerBindingsValid = false;
                _usingSourceLevelAcousticFallback = false;
                LogMissingAcousticMixerParameterWarning();
                InvalidateAppliedAcousticMixerStateCache();
                return false;
            }

            CacheAppliedAcousticMixerState(
                lowPassCutoffHz,
                lowPassResonanceQ,
                reverbDecayTime,
                reflectionsLevelDb,
                reverbLevelDb,
                roomHighFrequencyDb,
                dryLevelDb);
            return true;
        }

        private static string ResolveAcousticMixerParameterName(string configuredName, string fallbackName)
        {
            return string.IsNullOrWhiteSpace(configuredName) ? fallbackName : configuredName;
        }

        private void LogMissingAcousticMixerParameterWarning()
        {
            LogSnapshotFallbackWarningOnce(
                ref _warnedMissingAcousticMixerParameters,
                "[AcousticZoneController] MasterMixer acoustic exposed parameters are missing. Required params: " +
                AcousticLowPassCutoffParameterDefault + ", " +
                AcousticLowPassResonanceParameterDefault + ", " +
                AcousticReverbDecayParameterDefault + ", " +
                AcousticReflectionsLevelParameterDefault + ", " +
                AcousticReverbLevelParameterDefault + ", " +
                AcousticRoomHighFrequencyParameterDefault + ", " +
                AcousticDryLevelParameterDefault +
                ". Runtime acoustic graph fallback is disabled to avoid direct DSP component mutation.");
        }

        private void UpdateAmbientLoopMix(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource();
            if (ambientSource == null)
                return;

            CacheAmbientSourceDefaults(ambientSource);

            float targetVolume = _ambientSourceBaseVolume;
            float targetPitch = _ambientSourceBasePitch;

            if (zone == AcousticZoneState.Underwater)
            {
                targetVolume *= _currentAmbientVolumeScale;
                targetPitch *= _currentAmbientPitchScale;
                targetVolume *= _currentSoundscapeVolumeScale;
                targetPitch *= _currentSoundscapePitchScale;
                if (_stormAmbientInterference > 0.001f)
                {
                    targetVolume *= Mathf.Lerp(1f, Mathf.Max(0.1f, 1f - stormAmbientDuckMax), _stormAmbientInterference);
                    targetPitch *= Mathf.Lerp(1f, Mathf.Max(0.5f, 1f - stormAmbientPitchDropMax), _stormAmbientInterference);
                    targetPitch += _stormAmbientFlutter;
                }
            }

            if (Mathf.Abs(ambientSource.volume - targetVolume) > 0.01f)
                ambientSource.volume = targetVolume;

            if (Mathf.Abs(ambientSource.pitch - targetPitch) > 0.01f)
                ambientSource.pitch = targetPitch;
        }

        private void UpdateStormInterferenceAudio(AcousticZoneState zone, float deltaTime)
        {
            if (zone == AcousticZoneState.Interior)
            {
                _stormAmbientInterference = 0f;
                _stormAmbientFlutter = 0f;
                _stormInterferencePulseTimer = 0f;
                _debugStormInterference = 0f;
                return;
            }

            if (_surfaceElectricalActivity <= stormStaticElectricalThreshold)
            {
                _stormAmbientInterference = 0f;
                _stormAmbientFlutter = 0f;
                _stormInterferencePulseTimer = 0f;
                _debugStormInterference = 0f;
                return;
            }

            float stormInterference = Mathf.InverseLerp(stormStaticElectricalThreshold, 1f, _surfaceElectricalActivity);
            _stormAmbientInterference = stormInterference;
            _debugStormInterference = stormInterference;

            float flutterFrequency = Mathf.Lerp(stormAmbientFlutterFrequencyMin, stormAmbientFlutterFrequencyMax, stormInterference);
            _stormAmbientFlutterPhase += deltaTime * flutterFrequency * Mathf.PI * 2f;
            if (_stormAmbientFlutterPhase >= Mathf.PI * 2f)
                _stormAmbientFlutterPhase -= Mathf.PI * 2f;

            _stormAmbientFlutter = Mathf.Sin(_stormAmbientFlutterPhase) * (stormAmbientPitchFlutterMax * stormInterference);

            _stormInterferencePulseTimer -= deltaTime;
            if (_stormInterferencePulseTimer > 0f)
                return;

            PlayStormInterferencePulse(stormInterference, zone);
            _stormInterferencePulseTimer = Mathf.Lerp(
                Mathf.Max(0.1f, stormStaticIntervalMax),
                Mathf.Max(0.1f, stormStaticIntervalMin),
                stormInterference);
        }

        private void UpdateUnderwaterVegetationOverlay(AcousticZoneState zone, float deltaTime)
        {
            if (zone != AcousticZoneState.Underwater)
            {
                _underwaterVegetationPulseTimer = 0f;
                return;
            }

            HectonMapMagicVegetationBridge.VegetationAcousticType acousticType =
                HectonMapMagicVegetationBridge.GlobalVegetationAcousticType;
            float density = Mathf.Clamp01(HectonMapMagicVegetationBridge.GlobalVegetationAudioDensity);
            if (acousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.Silence ||
                density <= underwaterVegetationDensityThreshold)
            {
                _underwaterVegetationPulseTimer = 0f;
                return;
            }

            _underwaterVegetationPulseTimer -= deltaTime;
            if (_underwaterVegetationPulseTimer > 0f)
                return;

            AudioClip clip = acousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles
                ? underwaterSargassumBubblesClip
                : underwaterGrassRustleClip;
            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (clip == null || sam == null)
                return;

            float densityT = Mathf.InverseLerp(underwaterVegetationDensityThreshold, 1f, density);
            float volume = Mathf.Lerp(underwaterVegetationVolumeMin, underwaterVegetationVolumeMax, densityT);
            sam.PlayStatic2D(clip, volume, sam.AmbientGroup);
            _underwaterVegetationPulseTimer = Mathf.Lerp(
                Mathf.Max(0.1f, underwaterVegetationIntervalMax),
                Mathf.Max(0.1f, underwaterVegetationIntervalMin),
                densityT);
        }

        private void UpdateFatalPressureLoopAudio(AcousticZoneState zone, float deltaTime)
        {
            HectonPlayerMovement movement = ResolvePlayerMovement();
            float intensity = movement != null ? Mathf.Clamp01(movement.CurrentFatalPressureSequence01) : 0f;
            if (zone != AcousticZoneState.Underwater || intensity <= 0.001f)
            {
                _fatalPressureNoiseTimer = 0f;
                return;
            }

            _fatalPressureNoiseTimer -= deltaTime;
            if (_fatalPressureNoiseTimer > 0f)
                return;

            AudioClip clip = null;
            if (fatalPressureNoisePrimary != null && fatalPressureNoiseSecondary != null)
            {
                clip = _fatalPressureNoiseUsePrimaryNext ? fatalPressureNoisePrimary : fatalPressureNoiseSecondary;
                _fatalPressureNoiseUsePrimaryNext = !_fatalPressureNoiseUsePrimaryNext;
            }
            else if (fatalPressureNoisePrimary != null)
            {
                clip = fatalPressureNoisePrimary;
            }
            else if (fatalPressureNoiseSecondary != null)
            {
                clip = fatalPressureNoiseSecondary;
            }

            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (clip == null || sam == null)
                return;

            float volume = Mathf.Lerp(fatalPressureNoiseVolumeMin, fatalPressureNoiseVolumeMax, intensity);
            sam.PlayStatic2D(clip, volume, sam.InterfaceGroup);
            _fatalPressureNoiseTimer = Mathf.Lerp(
                Mathf.Max(0.05f, fatalPressureNoiseIntervalMax),
                Mathf.Max(0.05f, fatalPressureNoiseIntervalMin),
                intensity);
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (enableRuntimeAcousticGraph)
                _acousticSonarImpulse = Mathf.Max(_acousticSonarImpulse, Mathf.Clamp01(intensity));

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
                return;

            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (sonarPingClip == null || sam == null)
                return;

            float volume = Mathf.Lerp(sonarPingVolumeMin, sonarPingVolumeMax, Mathf.Clamp01(intensity));
            sam.PlayStatic2D(sonarPingClip, volume, sam.InterfaceGroup);
        }

        private void HandlePhysicsImpact(PhysicsImpactSignal impactSignal)
        {
            if (!enableRuntimeAcousticGraph)
                return;

            AudioListener listener = _cachedPlayerAudioListener;
            if ((object)listener == null || listener == null)
            {
                ResolvePlayerListenerFilters();
                listener = _cachedPlayerAudioListener;
            }

            if ((object)listener == null || listener == null)
                return;

            float radius = Mathf.Max(0.5f, acousticImpactImpulseRadius);
            float distance = Vector3.Distance(listener.transform.position, impactSignal.Point);
            if (distance > radius)
                return;

            float proximity = 1f - Mathf.Clamp01(distance / radius);
            float impulse = Mathf.Clamp01(impactSignal.Intensity * Mathf.Max(0.15f, proximity));
            if (impactSignal.IsHeavy)
                impulse = Mathf.Max(impulse, 0.35f * Mathf.Max(0.35f, proximity));

            _acousticImpactImpulse = Mathf.Max(_acousticImpactImpulse, impulse);
        }

        internal void PlayMantaMisfire(float intensity)
        {
            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (mantaMisfireClip == null || sam == null)
                return;

            float volume = Mathf.Lerp(mantaMisfireVolumeMin, mantaMisfireVolumeMax, Mathf.Clamp01(intensity));
            sam.PlayStatic2D(mantaMisfireClip, volume, sam.InterfaceGroup);
        }

        private void PlayStormInterferencePulse(float stormInterference, AcousticZoneState zone)
        {
            AudioClip clip = null;
            if (stormStaticPrimary != null && stormStaticSecondary != null)
            {
                clip = _stormStaticUsePrimaryNext ? stormStaticPrimary : stormStaticSecondary;
                _stormStaticUsePrimaryNext = !_stormStaticUsePrimaryNext;
            }
            else if (stormStaticPrimary != null)
            {
                clip = stormStaticPrimary;
            }
            else if (stormStaticSecondary != null)
            {
                clip = stormStaticSecondary;
            }

            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (clip == null || sam == null)
                return;

            float volume = Mathf.Lerp(stormStaticVolumeMin, stormStaticVolumeMax, stormInterference);
            if (zone == AcousticZoneState.Underwater)
                volume *= stormStaticUnderwaterVolumeScale;

            sam.PlayStatic2D(clip, volume, sam.InterfaceGroup);
        }

        private string ResolveAmbientMoodLabel()
        {
            if (_currentAmbientSurvivalPressure >= 4)
                return "Hostile";

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
                return "Lively";

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
                return "Calm";

            if (_currentAmbientSurvivalPressure <= 0 && _currentAmbientRewardPull <= 0)
                return "None";

            return "Mixed";
        }

        internal void SetSurfaceWeatherMix(float precipitationIntensity, float electricalActivity)
        {
            float clampedPrecipitation = Mathf.Clamp01(precipitationIntensity);
            float clampedElectrical = Mathf.Clamp01(electricalActivity);
            if (ApproximatelyEqual(_surfacePrecipitationIntensity, clampedPrecipitation) &&
                ApproximatelyEqual(_surfaceElectricalActivity, clampedElectrical))
            {
                return;
            }

            _surfacePrecipitationIntensity = clampedPrecipitation;
            _surfaceElectricalActivity = clampedElectrical;
            _debugStormInterference = clampedElectrical <= stormStaticElectricalThreshold
                ? 0f
                : Mathf.InverseLerp(stormStaticElectricalThreshold, 1f, clampedElectrical);

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        internal void ClearSurfaceWeatherMix()
        {
            if (ApproximatelyEqual(_surfacePrecipitationIntensity, 0f) &&
                ApproximatelyEqual(_surfaceElectricalActivity, 0f))
            {
                return;
            }

            _surfacePrecipitationIntensity = 0f;
            _surfaceElectricalActivity = 0f;
            _stormInterferencePulseTimer = 0f;
            _stormAmbientInterference = 0f;
            _stormAmbientFlutter = 0f;
            _debugStormInterference = 0f;

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        private void ApplyAmbientLoopState(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource();
            if (ambientSource == null)
            {
                ApplySourceLevelAcousticFallback(zone);
                return;
            }

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (ambientSource.isPlaying)
                    ambientSource.Stop();

                if (!ambientSource.mute)
                    ambientSource.mute = true;

                ApplySourceLevelAcousticFallback(zone);
                return;
            }

            bool shouldBeAudible = zone == AcousticZoneState.Underwater;
            bool shouldMute = !shouldBeAudible;

            if (ambientSource.mute != shouldMute)
                ambientSource.mute = shouldMute;

            if (shouldBeAudible && !ambientSource.isPlaying && ambientSource.clip != null)
                ambientSource.Play();

            UpdateAmbientLoopMix(zone);
            ApplySourceLevelAcousticFallback(zone);
        }

        private void ApplySourceLevelAcousticFallback(AcousticZoneState zone)
        {
            UpdateSourceLevelAcousticGraph(zone, 0f);
        }

        private void UpdateSourceLevelAcousticGraph(AcousticZoneState zone, float deltaTime)
        {
            DecayAcousticGraphImpulses(deltaTime);

            if (!ShouldUseSourceLevelAcousticFallback())
            {
                ResetSourceLevelAcousticFallback();
                return;
            }

            ResolvePlayerListenerFilters();
            if (!_listenerFallbackDefaultsCaptured)
            {
                return;
            }

            if (zone == AcousticZoneState.Surface)
            {
                ResetSourceLevelAcousticFallback();
                return;
            }

            AudioListener listener = _cachedPlayerAudioListener;
            UpdateEmitterOcclusionState(listener);

            AcousticGraphState targetState = zone == AcousticZoneState.Interior
                ? ResolveInteriorAcousticGraphState()
                : ResolveUnderwaterAcousticGraphState();

            float blendT = deltaTime <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Mathf.Max(0.01f, acousticGraphFollowSharpness) * deltaTime);

            if (!_acousticGraphStateInitialized)
            {
                _currentAcousticLowPassCutoffHz = targetState.LowPassCutoffHz;
                _currentAcousticLowPassResonanceQ = targetState.LowPassResonanceQ;
                _currentAcousticReverbDecayTime = targetState.ReverbDecayTime;
                _currentAcousticReflectionsLevelDb = targetState.ReflectionsLevelDb;
                _currentAcousticReverbLevelDb = targetState.ReverbLevelDb;
                _currentAcousticRoomHighFrequencyDb = targetState.RoomHighFrequencyDb;
                _currentAcousticDryLevelDb = targetState.DryLevelDb;
                _acousticGraphStateInitialized = true;
            }
            else
            {
                _currentAcousticLowPassCutoffHz = Mathf.Lerp(_currentAcousticLowPassCutoffHz, targetState.LowPassCutoffHz, blendT);
                _currentAcousticLowPassResonanceQ = Mathf.Lerp(_currentAcousticLowPassResonanceQ, targetState.LowPassResonanceQ, blendT);
                _currentAcousticReverbDecayTime = Mathf.Lerp(_currentAcousticReverbDecayTime, targetState.ReverbDecayTime, blendT);
                _currentAcousticReflectionsLevelDb = Mathf.Lerp(_currentAcousticReflectionsLevelDb, targetState.ReflectionsLevelDb, blendT);
                _currentAcousticReverbLevelDb = Mathf.Lerp(_currentAcousticReverbLevelDb, targetState.ReverbLevelDb, blendT);
                _currentAcousticRoomHighFrequencyDb = Mathf.Lerp(_currentAcousticRoomHighFrequencyDb, targetState.RoomHighFrequencyDb, blendT);
                _currentAcousticDryLevelDb = Mathf.Lerp(_currentAcousticDryLevelDb, targetState.DryLevelDb, blendT);
            }

            _usingSourceLevelAcousticFallback = ApplyAcousticMixerState(
                _currentAcousticLowPassCutoffHz,
                _currentAcousticLowPassResonanceQ,
                _currentAcousticReverbDecayTime,
                _currentAcousticReflectionsLevelDb,
                _currentAcousticReverbLevelDb,
                _currentAcousticRoomHighFrequencyDb,
                _currentAcousticDryLevelDb);
        }

        private void DecayAcousticGraphImpulses(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _acousticImpactImpulse = Mathf.MoveTowards(
                _acousticImpactImpulse,
                0f,
                Mathf.Max(0.01f, acousticImpactImpulseDecay) * deltaTime);
            _acousticSonarImpulse = Mathf.MoveTowards(
                _acousticSonarImpulse,
                0f,
                Mathf.Max(0.01f, acousticSonarImpulseDecay) * deltaTime);
        }

        private void UpdateEmitterOcclusionState(AudioListener listener)
        {
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if ((object)listener == null || listener == null || !(Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager))
                return;

            if (_resolvedEmitterOcclusionLayerMask == 0)
                _resolvedEmitterOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask();

            if (_resolvedEmitterOcclusionLayerMask == 0)
                return;

            int emitterCount = spatialAudioManager.CopyActiveWorldEmitterSamples(s_emitterOcclusionSamples);
            if (emitterCount <= 0)
                return;

            Transform listenerTransform = listener.transform;
            Vector3 listenerPosition = listenerTransform.position;
            Transform listenerRoot = listenerTransform.root;
            float maxDistanceSqr = AcousticEmitterOcclusionMaxDistanceMeters * AcousticEmitterOcclusionMaxDistanceMeters;
            float weightedTransmission = 0f;
            float weightedCutoff = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < emitterCount; i++)
            {
                SpatialAudioManager.ActiveEmitterSample sample = s_emitterOcclusionSamples[i];
                if (!(sample.Amplitude > 0.0001f))
                    continue;

                Vector3 delta = sample.Position - listenerPosition;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > maxDistanceSqr)
                    continue;

                float sampleWeight = sample.Amplitude / (1f + (distanceSqr * AcousticEmitterDistanceWeightScale));
                if (!(sampleWeight > 0.0001f))
                    continue;

                if (!AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                        sample.Position,
                        listenerPosition,
                        _resolvedEmitterOcclusionLayerMask,
                        null,
                        listenerRoot,
                        out AcousticOcclusionResult occlusion))
                {
                    AcousticOcclusionUtility.PrimeOcclusionPath(
                        sample.Position,
                        listenerPosition,
                        _resolvedEmitterOcclusionLayerMask,
                        null,
                        listenerRoot);
                    continue;
                }

                weightedTransmission += occlusion.Transmission01 * sampleWeight;
                weightedCutoff += occlusion.LowPassCutoffHz * sampleWeight;
                totalWeight += sampleWeight;

                AcousticOcclusionUtility.PrimeOcclusionPath(
                    sample.Position,
                    listenerPosition,
                    _resolvedEmitterOcclusionLayerMask,
                    null,
                    listenerRoot);
            }

            if (!(totalWeight > 0.0001f))
                return;

            _emitterOcclusionTransmission01 = Mathf.Clamp01(weightedTransmission / totalWeight);
            _emitterOcclusionLowPassCutoffHz = Mathf.Clamp(
                weightedCutoff / totalWeight,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
        }

        private AcousticGraphState ResolveInteriorAcousticGraphState()
        {
            float metallicImpulse = Mathf.Clamp01(_acousticImpactImpulse * Mathf.Max(0f, impactGraphMetallicBoost));
            float sonarImpulse = Mathf.Clamp01(_acousticSonarImpulse);
            AcousticGraphState state;
            state.LowPassCutoffHz = Mathf.Lerp(interiorGraphLowPassCutoff, 7200f, metallicImpulse);
            state.LowPassResonanceQ = Mathf.Lerp(interiorGraphResonance, interiorGraphResonance + 0.22f, metallicImpulse);
            state.ReverbDecayTime = Mathf.Clamp(
                interiorGraphDecayTime +
                (interiorImpactDecayBoost * metallicImpulse) +
                (0.22f * sonarImpulse),
                0.05f,
                12f);
            state.ReflectionsLevelDb = Mathf.Clamp(
                interiorGraphReflectionsLevel + (550f * metallicImpulse),
                -10000f,
                1000f);
            state.ReverbLevelDb = Mathf.Clamp(
                interiorGraphReverbLevel + (450f * sonarImpulse),
                -10000f,
                2000f);
            state.RoomHighFrequencyDb = Mathf.Clamp(
                interiorGraphRoomHighFrequency - (1600f * metallicImpulse),
                -10000f,
                0f);
            state.DryLevelDb = Mathf.Clamp(
                interiorGraphDryLevel - (120f * sonarImpulse),
                -10000f,
                0f);
            ApplyEmitterOcclusionToAcousticState(ref state);
            return state;
        }

        private AcousticGraphState ResolveUnderwaterAcousticGraphState()
        {
            float depth01 = ResolveUnderwaterGraphDepth01();
            float sonarImpulse = Mathf.Clamp01(_acousticSonarImpulse * Mathf.Max(0f, sonarGraphOpenUpBoost));
            float metallicImpulse = Mathf.Clamp01(_acousticImpactImpulse * Mathf.Max(0f, impactGraphMetallicBoost));
            float baseCutoff = Mathf.Lerp(underwaterGraphShallowCutoff, underwaterGraphDeepCutoff, depth01);
            float openedCutoff = Mathf.Min(interiorFallbackLowPassCutoff, baseCutoff + 2400f);
            AcousticGraphState state;
            state.LowPassCutoffHz = Mathf.Clamp(Mathf.Lerp(baseCutoff, openedCutoff, sonarImpulse), 500f, 22000f);
            state.LowPassResonanceQ = Mathf.Lerp(underwaterGraphResonance, underwaterGraphResonance + 0.18f, metallicImpulse);
            state.ReverbDecayTime = Mathf.Clamp(
                Mathf.Lerp(0.92f, underwaterGraphDecayTime, depth01) +
                (0.2f * sonarImpulse),
                0.05f,
                12f);
            state.ReflectionsLevelDb = Mathf.Clamp(
                underwaterGraphReflectionsLevel + (600f * sonarImpulse),
                -10000f,
                1000f);
            state.ReverbLevelDb = Mathf.Clamp(
                underwaterGraphReverbLevel + (300f * sonarImpulse) - (120f * metallicImpulse),
                -10000f,
                2000f);
            state.RoomHighFrequencyDb = Mathf.Clamp(
                Mathf.Lerp(underwaterGraphRoomHighFrequency, underwaterGraphRoomHighFrequency + 1200f, sonarImpulse),
                -10000f,
                0f);
            state.DryLevelDb = Mathf.Clamp(
                underwaterGraphDryLevel - (350f * depth01),
                -10000f,
                0f);
            ApplyEmitterOcclusionToAcousticState(ref state);
            return state;
        }

        private void ApplyEmitterOcclusionToAcousticState(ref AcousticGraphState state)
        {
            float occlusionShadow01 = Mathf.Clamp01(1f - _emitterOcclusionTransmission01);
            if (occlusionShadow01 <= 0.0001f)
                return;

            float occludedCutoffHz = Mathf.Clamp(
                _emitterOcclusionLowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);

            state.LowPassCutoffHz = Mathf.Clamp(
                Mathf.Min(state.LowPassCutoffHz, Mathf.Lerp(state.LowPassCutoffHz, occludedCutoffHz, occlusionShadow01)),
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            state.LowPassResonanceQ = Mathf.Lerp(state.LowPassResonanceQ, state.LowPassResonanceQ + 0.18f, occlusionShadow01);
            state.ReflectionsLevelDb = Mathf.Clamp(state.ReflectionsLevelDb + (420f * occlusionShadow01), -10000f, 1000f);
            state.RoomHighFrequencyDb = Mathf.Clamp(state.RoomHighFrequencyDb - (2200f * occlusionShadow01), -10000f, 0f);
            state.DryLevelDb = Mathf.Clamp(state.DryLevelDb - (260f * occlusionShadow01), -10000f, 0f);
        }

        private float ResolveUnderwaterGraphDepth01()
        {
            HectonPlayerMovement movement = ResolvePlayerMovement();
            float depth = movement != null
                ? Mathf.Max(0f, movement.CurrentDepth)
                : ResolvePlayerDepthFallback();
            float immersion = movement != null
                ? Mathf.Clamp01(movement.WaterImmersionRatio)
                : (_acousticUnderwaterState ? 1f : 0f);
            float depth01 = Mathf.Clamp01(depth / Mathf.Max(1f, acousticDeepWaterReferenceDepth));
            float immersion01 = Mathf.InverseLerp(acousticExitImmersionRatio, 1f, immersion);
            return Mathf.Max(depth01, Mathf.Max(immersion01, ResolveSoundscapeTierDepth01()));
        }

        private float ResolveSoundscapeTierDepth01()
        {
            switch (_currentSoundscapeTier)
            {
                case SoundscapeTier.DeepAbyss:
                    return 1f;

                case SoundscapeTier.Abyss:
                    return 0.75f;

                case SoundscapeTier.Darkness:
                    return 0.52f;

                case SoundscapeTier.Thermal:
                    return 0.48f;

                case SoundscapeTier.Twilight:
                    return 0.24f;

                default:
                    return 0f;
            }
        }

        private bool ShouldUseSourceLevelAcousticFallback()
        {
            return enableSourceLevelAcousticFallback &&
                   enableRuntimeAcousticGraph &&
                   masterMixer != null &&
                   EnsureAcousticMixerParameterBindings();
        }

        private void ResetSourceLevelAcousticFallback()
        {
            if (!_listenerFallbackDefaultsCaptured)
            {
                _usingSourceLevelAcousticFallback = false;
                _acousticGraphStateInitialized = false;
                return;
            }

            if (_acousticMixerBindingsValid)
            {
                ApplyAcousticMixerState(
                    _listenerLowPassBaseCutoff,
                    _listenerLowPassBaseResonance,
                    _listenerReverbBaseDecayTime,
                    _listenerReverbBaseReflectionsLevel,
                    _listenerReverbBaseReverbLevel,
                    _listenerReverbBaseRoomHighFrequency,
                    _listenerReverbBaseDryLevel);
            }

            _currentAcousticLowPassCutoffHz = _listenerLowPassBaseCutoff;
            _currentAcousticLowPassResonanceQ = _listenerLowPassBaseResonance;
            _currentAcousticReverbDecayTime = _listenerReverbBaseDecayTime;
            _currentAcousticReflectionsLevelDb = _listenerReverbBaseReflectionsLevel;
            _currentAcousticReverbLevelDb = _listenerReverbBaseReverbLevel;
            _currentAcousticRoomHighFrequencyDb = _listenerReverbBaseRoomHighFrequency;
            _currentAcousticDryLevelDb = _listenerReverbBaseDryLevel;
            _emitterOcclusionTransmission01 = 1f;
            _emitterOcclusionLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            _acousticGraphStateInitialized = false;
            _usingSourceLevelAcousticFallback = false;
        }

        // ══════════════════════════════════════════════════════════
        //  SNAPSHOT BINDING / FALLBACKS
        // ══════════════════════════════════════════════════════════

        private void EnsureSnapshotBindings()
        {
            if (_snapshotBindingsResolved)
                return;

            _snapshotBindingsResolved = true;

            if (masterMixer == null)
                return;

            ResolveSnapshotBinding(ref underwaterSnapshot, "Underwater", "UnderwaterSnapshot");
            ResolveSnapshotBinding(ref baseInteriorSnapshot, "BaseInterior", "BaseInteriorSnapshot");
            ResolveSnapshotBinding(ref surfaceSnapshot, "Surface", "SurfaceSnapshot");
            ResolveSnapshotBinding(ref surfaceRainSnapshot, "SurfaceRain", "SurfaceRainSnapshot");
            ResolveSnapshotBinding(ref surfaceStormSnapshot, "SurfaceStorm", "SurfaceStormSnapshot");

            if (underwaterSnapshot == null &&
                baseInteriorSnapshot == null &&
                surfaceSnapshot == null &&
                surfaceRainSnapshot == null &&
                surfaceStormSnapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] MasterMixer is assigned but no authored acoustic snapshots were resolved by name. Expected names include Underwater/UnderwaterSnapshot, BaseInterior/BaseInteriorSnapshot, Surface/SurfaceSnapshot, SurfaceRain/SurfaceRainSnapshot, SurfaceStorm/SurfaceStormSnapshot.");
            }

#if UNITY_EDITOR
            ValidateMixerAuthoringCoverage();
#endif
        }

        private void ResolveSnapshotBinding(
            ref AudioMixerSnapshot snapshot,
            string primaryName,
            string alternateName)
        {
            if (snapshot != null || masterMixer == null)
                return;

            snapshot = masterMixer.FindSnapshot(primaryName);
            if (snapshot == null && !string.IsNullOrEmpty(alternateName))
                snapshot = masterMixer.FindSnapshot(alternateName);
        }

        private bool TransitionToResolvedSnapshot(AcousticZoneState zone, float duration)
        {
            EnsureSnapshotBindings();
            bool blendResolved = false;

            if (zone == AcousticZoneState.Surface &&
                TryTransitionSurfaceSnapshotBlend(duration, out blendResolved))
            {
                LogDiagnostic("[AcousticZoneController] Snapshot activated: SurfaceBlend");
                return true;
            }

            if (zone == AcousticZoneState.Surface && blendResolved)
            {
                return false;
            }

            AudioMixerSnapshot snapshot = ResolveSnapshotForZone(zone);
            if (snapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] No valid snapshot could be resolved for the requested acoustic zone. Mixer state will remain unchanged.");
                return false;
            }

            if (IsResolvedSnapshotAlreadyActive(zone, snapshot))
                return false;

            if (IsSnapshotTransitionLocked())
            {
                QueuePendingSnapshotTransition(zone, duration);
                return false;
            }

            float transitionTime = Mathf.Max(0f, duration);
            snapshot.TransitionTo(transitionTime);
            ArmSnapshotTransitionLock(transitionTime);
            CacheResolvedSnapshotState(zone, snapshot);
            LogDiagnostic($"[AcousticZoneController] Snapshot activated: {snapshot.name}");
            return true;
        }

        private bool TryTransitionSurfaceSnapshotBlend(float duration, out bool blendResolved)
        {
            blendResolved = false;

            if (masterMixer == null || surfaceSnapshot == null)
                return false;

            int snapshotCount = 0;
            float totalWeight = 0f;

            _surfaceBlendSnapshots[snapshotCount] = surfaceSnapshot;
            _surfaceBlendWeights[snapshotCount] = 1f;
            totalWeight += 1f;
            snapshotCount++;

            if (surfaceRainSnapshot != null && _surfacePrecipitationIntensity >= 0.2f)
            {
                float rainWeight = Mathf.Clamp01(_surfacePrecipitationIntensity) * surfaceRainSnapshotWeight;
                if (rainWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceRainSnapshot;
                    _surfaceBlendWeights[snapshotCount] = rainWeight;
                    totalWeight += rainWeight;
                    snapshotCount++;
                }
            }

            if (surfaceStormSnapshot != null && _surfaceElectricalActivity >= 0.55f)
            {
                float stormWeight = Mathf.Clamp01(_surfaceElectricalActivity) * surfaceStormSnapshotWeight;
                if (stormWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceStormSnapshot;
                    _surfaceBlendWeights[snapshotCount] = stormWeight;
                    totalWeight += stormWeight;
                    snapshotCount++;
                }
            }

            if (snapshotCount <= 1 || totalWeight <= 0.001f)
                return false;

            for (int i = 0; i < snapshotCount; i++)
                _surfaceBlendWeights[i] /= totalWeight;

            ClearBlendTail(_surfaceBlendSnapshots, _surfaceBlendWeights, snapshotCount);

            blendResolved = true;
            if (IsActiveSurfaceBlendEquivalent(snapshotCount))
                return false;

            float transitionTime = Mathf.Max(0f, surfaceWeatherTransitionDuration > 0f ? surfaceWeatherTransitionDuration : duration);
            if (IsSnapshotTransitionLocked())
            {
                QueuePendingSnapshotTransition(AcousticZoneState.Surface, transitionTime);
                return false;
            }

            masterMixer.TransitionToSnapshots(_surfaceBlendSnapshots, _surfaceBlendWeights, transitionTime);
            ArmSnapshotTransitionLock(transitionTime);
            CacheSurfaceBlendState(snapshotCount);
            return true;
        }

        private static bool ApproximatelyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) <= SurfaceWeatherStateEpsilon;
        }

        private static void ClearBlendTail(AudioMixerSnapshot[] snapshots, float[] weights, int startIndex)
        {
            for (int i = startIndex; i < snapshots.Length; i++)
            {
                snapshots[i] = null;
                weights[i] = 0f;
            }
        }

        private bool IsSnapshotTransitionLocked()
        {
            return Time.unscaledTime < _snapshotTransitionLockUntilTime;
        }

        private void ArmSnapshotTransitionLock(float duration)
        {
            if (duration <= 0f)
                return;

            float unlockTime = Time.unscaledTime + duration;
            if (unlockTime > _snapshotTransitionLockUntilTime)
                _snapshotTransitionLockUntilTime = unlockTime;
        }

        private void QueuePendingSnapshotTransition(AcousticZoneState zone, float duration)
        {
            _pendingSnapshotZone = zone;
            _pendingSnapshotDuration = Mathf.Max(0f, duration);
            _hasPendingSnapshotTransition = true;
        }

        private void ProcessPendingSnapshotTransition()
        {
            if (!_hasPendingSnapshotTransition || IsSnapshotTransitionLocked())
                return;

            AcousticZoneState pendingZone = _pendingSnapshotZone;
            float pendingDuration = _pendingSnapshotDuration;
            _hasPendingSnapshotTransition = false;
            _pendingSnapshotDuration = 0f;
            TransitionToResolvedSnapshot(pendingZone, pendingDuration);
        }

        private bool IsResolvedSnapshotAlreadyActive(AcousticZoneState zone, AudioMixerSnapshot snapshot)
        {
            return _hasActiveResolvedSnapshotState &&
                   !_activeSurfaceBlendState &&
                   _activeResolvedZone == zone &&
                   _activeResolvedSnapshot == snapshot;
        }

        private bool IsActiveSurfaceBlendEquivalent(int snapshotCount)
        {
            if (!_hasActiveResolvedSnapshotState ||
                !_activeSurfaceBlendState ||
                _activeResolvedZone != AcousticZoneState.Surface ||
                _activeSurfaceBlendSnapshotCount != snapshotCount)
            {
                return false;
            }

            for (int i = 0; i < snapshotCount; i++)
            {
                if (_activeSurfaceBlendSnapshots[i] != _surfaceBlendSnapshots[i] ||
                    !ApproximatelyEqual(_activeSurfaceBlendWeights[i], _surfaceBlendWeights[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheResolvedSnapshotState(AcousticZoneState zone, AudioMixerSnapshot snapshot)
        {
            _hasActiveResolvedSnapshotState = true;
            _activeSurfaceBlendState = false;
            _activeResolvedZone = zone;
            _activeResolvedSnapshot = snapshot;
            _activeSurfaceBlendSnapshotCount = 0;
            ClearBlendTail(_activeSurfaceBlendSnapshots, _activeSurfaceBlendWeights, 0);
        }

        private void CacheSurfaceBlendState(int snapshotCount)
        {
            _hasActiveResolvedSnapshotState = true;
            _activeSurfaceBlendState = true;
            _activeResolvedZone = AcousticZoneState.Surface;
            _activeResolvedSnapshot = null;
            _activeSurfaceBlendSnapshotCount = snapshotCount;

            for (int i = 0; i < snapshotCount; i++)
            {
                _activeSurfaceBlendSnapshots[i] = _surfaceBlendSnapshots[i];
                _activeSurfaceBlendWeights[i] = _surfaceBlendWeights[i];
            }

            ClearBlendTail(_activeSurfaceBlendSnapshots, _activeSurfaceBlendWeights, snapshotCount);
        }

        private bool HasAppliedAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            return !float.IsNaN(_lastAppliedAcousticLowPassCutoffHz) &&
                   Mathf.Abs(_lastAppliedAcousticLowPassCutoffHz - lowPassCutoffHz) <= AcousticCutoffWriteEpsilonHz &&
                   Mathf.Abs(_lastAppliedAcousticLowPassResonanceQ - lowPassResonanceQ) <= AcousticResonanceWriteEpsilon &&
                   Mathf.Abs(_lastAppliedAcousticReverbDecayTime - reverbDecayTime) <= AcousticDecayWriteEpsilonSeconds &&
                   Mathf.Abs(_lastAppliedAcousticReflectionsLevelDb - reflectionsLevelDb) <= AcousticDbWriteEpsilon &&
                   Mathf.Abs(_lastAppliedAcousticReverbLevelDb - reverbLevelDb) <= AcousticDbWriteEpsilon &&
                   Mathf.Abs(_lastAppliedAcousticRoomHighFrequencyDb - roomHighFrequencyDb) <= AcousticDbWriteEpsilon &&
                   Mathf.Abs(_lastAppliedAcousticDryLevelDb - dryLevelDb) <= AcousticDbWriteEpsilon;
        }

        private void CacheAppliedAcousticMixerState(
            float lowPassCutoffHz,
            float lowPassResonanceQ,
            float reverbDecayTime,
            float reflectionsLevelDb,
            float reverbLevelDb,
            float roomHighFrequencyDb,
            float dryLevelDb)
        {
            _lastAppliedAcousticLowPassCutoffHz = lowPassCutoffHz;
            _lastAppliedAcousticLowPassResonanceQ = lowPassResonanceQ;
            _lastAppliedAcousticReverbDecayTime = reverbDecayTime;
            _lastAppliedAcousticReflectionsLevelDb = reflectionsLevelDb;
            _lastAppliedAcousticReverbLevelDb = reverbLevelDb;
            _lastAppliedAcousticRoomHighFrequencyDb = roomHighFrequencyDb;
            _lastAppliedAcousticDryLevelDb = dryLevelDb;
        }

        private void InvalidateAppliedAcousticMixerStateCache()
        {
            _lastAppliedAcousticLowPassCutoffHz = float.NaN;
            _lastAppliedAcousticLowPassResonanceQ = float.NaN;
            _lastAppliedAcousticReverbDecayTime = float.NaN;
            _lastAppliedAcousticReflectionsLevelDb = float.NaN;
            _lastAppliedAcousticReverbLevelDb = float.NaN;
            _lastAppliedAcousticRoomHighFrequencyDb = float.NaN;
            _lastAppliedAcousticDryLevelDb = float.NaN;
        }

        private AudioMixerSnapshot ResolveSnapshotForZone(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    if (baseInteriorSnapshot != null)
                        return baseInteriorSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingInteriorSnapshot,
                        "[AcousticZoneController] BaseInteriorSnapshot missing. Falling back to exterior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? underwaterSnapshot;

                case AcousticZoneState.Surface:
                    return ResolveSurfaceSnapshot();

                default:
                    if (underwaterSnapshot != null)
                        return underwaterSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingUnderwaterSnapshot,
                        "[AcousticZoneController] UnderwaterSnapshot missing. Falling back to surface/interior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? baseInteriorSnapshot;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogSnapshotFallbackWarningOnce(ref bool warnedFlag, string message)
        {
            if (warnedFlag)
                return;

            warnedFlag = true;
            Debug.LogWarning(message, this);
        }

        private bool HasAnyResolvedSnapshotCoverage()
        {
            return underwaterSnapshot != null ||
                   baseInteriorSnapshot != null ||
                   surfaceSnapshot != null ||
                   surfaceRainSnapshot != null ||
                   surfaceStormSnapshot != null;
        }

        private string BuildSnapshotCoverageSummary()
        {
            return string.Concat(
                underwaterSnapshot != null ? "UW " : "uw- ",
                baseInteriorSnapshot != null ? "INT " : "int- ",
                surfaceSnapshot != null ? "SURF " : "surf- ",
                surfaceRainSnapshot != null ? "RAIN " : "rain- ",
                surfaceStormSnapshot != null ? "STORM" : "storm-");
        }

        private string BuildMixerCoverageSummary()
        {
            if (masterMixer == null)
                return "Mixer: None";

            return string.Concat(
                "Mixer snapshots=", _validatedMixerSnapshotCount.ToString(),
                " named=", _validatedMixerHasNamedCoverage ? "yes" : "no",
                " fx=", _validatedMixerHasEffectGraph ? "yes" : "no",
                " acousticParams=", _acousticMixerBindingsValid ? "yes" : "no");
        }

        private static string ResolveSoundscapeTierLabel(SoundscapeTier tier)
        {
            int index = (int)tier;
            return (uint)index < (uint)SoundscapeTierLabels.Length ? SoundscapeTierLabels[index] : SoundscapeTierLabels[0];
        }

#if UNITY_EDITOR
        private void Reset()
        {
            TryAssignEditorAuthoringDefaults();
        }

        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (transitionDuration < 0f) transitionDuration = 0f;
            if (interiorTransitionDuration < 0f) interiorTransitionDuration = 0f;
            if (surfaceTransitionDuration < 0f) surfaceTransitionDuration = 0f;
            if (underwaterTransitionDuration < 0f) underwaterTransitionDuration = 0f;
            if (surfaceWeatherTransitionDuration < 0f) surfaceWeatherTransitionDuration = 0f;
            if (acousticEnterUnderwaterDepth < 0f) acousticEnterUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth < 0f) acousticExitUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth > acousticEnterUnderwaterDepth) acousticExitUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (acousticEnterImmersionRatio < 0.1f) acousticEnterImmersionRatio = 0.1f;
            if (acousticEnterImmersionRatio > 1f) acousticEnterImmersionRatio = 1f;
            if (acousticExitImmersionRatio < 0.05f) acousticExitImmersionRatio = 0.05f;
            if (acousticExitImmersionRatio > acousticEnterImmersionRatio) acousticExitImmersionRatio = acousticEnterImmersionRatio;
            if (acousticForceUnderwaterDepth < acousticEnterUnderwaterDepth) acousticForceUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (exteriorTransitionDebounce < 0f) exteriorTransitionDebounce = 0f;
            if (exteriorTransitionHoldTime < 0f) exteriorTransitionHoldTime = 0f;
            if (transitionVolume < 0f) transitionVolume = 0f;
            if (transitionVolume > 1f) transitionVolume = 1f;
            if (stormStaticElectricalThreshold < 0f) stormStaticElectricalThreshold = 0f;
            if (stormStaticElectricalThreshold > 1f) stormStaticElectricalThreshold = 1f;
            if (stormStaticIntervalMax < 0.1f) stormStaticIntervalMax = 0.1f;
            if (stormStaticIntervalMin < 0.1f) stormStaticIntervalMin = 0.1f;
            if (stormStaticIntervalMin > stormStaticIntervalMax) stormStaticIntervalMin = stormStaticIntervalMax;
            if (stormStaticVolumeMin < 0f) stormStaticVolumeMin = 0f;
            if (stormStaticVolumeMin > 1f) stormStaticVolumeMin = 1f;
            if (stormStaticVolumeMax < stormStaticVolumeMin) stormStaticVolumeMax = stormStaticVolumeMin;
            if (stormStaticVolumeMax > 1f) stormStaticVolumeMax = 1f;
            if (stormStaticUnderwaterVolumeScale < 0f) stormStaticUnderwaterVolumeScale = 0f;
            if (stormStaticUnderwaterVolumeScale > 1f) stormStaticUnderwaterVolumeScale = 1f;
            if (stormAmbientDuckMax < 0f) stormAmbientDuckMax = 0f;
            if (stormAmbientDuckMax > 0.5f) stormAmbientDuckMax = 0.5f;
            if (stormAmbientPitchDropMax < 0f) stormAmbientPitchDropMax = 0f;
            if (stormAmbientPitchDropMax > 0.25f) stormAmbientPitchDropMax = 0.25f;
            if (stormAmbientPitchFlutterMax < 0f) stormAmbientPitchFlutterMax = 0f;
            if (stormAmbientPitchFlutterMax > 0.15f) stormAmbientPitchFlutterMax = 0.15f;
            if (stormAmbientFlutterFrequencyMin < 0.1f) stormAmbientFlutterFrequencyMin = 0.1f;
            if (stormAmbientFlutterFrequencyMax < stormAmbientFlutterFrequencyMin) stormAmbientFlutterFrequencyMax = stormAmbientFlutterFrequencyMin;
            if (interiorFallbackReverbDryLevel < -10000f) interiorFallbackReverbDryLevel = -10000f;
            if (interiorFallbackReverbDryLevel > 0f) interiorFallbackReverbDryLevel = 0f;
            _snapshotBindingsResolved = false;
            ResetAuthoringWarnings();
            TryAssignEditorAuthoringDefaults();
            EnsureSnapshotBindings();
        }

        private void TryAssignEditorAuthoringDefaults()
        {
            if (masterMixer == null)
                masterMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(DefaultMasterMixerPath);

            if (waterDrainSound == null)
                waterDrainSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterDrainSoundPath);

            if (waterFillSound == null)
                waterFillSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterFillSoundPath);

            if (stormStaticPrimary == null)
                stormStaticPrimary = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultStormStaticPrimaryPath);

            if (stormStaticSecondary == null)
                stormStaticSecondary = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultStormStaticSecondaryPath);
        }

        private void ResetAuthoringWarnings()
        {
            _warnedMissingInteriorSnapshot = false;
            _warnedMissingUnderwaterSnapshot = false;
            _warnedMissingSurfaceSnapshotSet = false;
            _warnedMissingSnapshotCoverage = false;
            _warnedIncompleteMixerSnapshotAuthoring = false;
            _validatedMixerSnapshotCount = 0;
            _validatedMixerHasNamedCoverage = false;
            _validatedMixerHasEffectGraph = false;
        }

        private void ValidateMixerAuthoringCoverage()
        {
            if (masterMixer == null)
                return;

            string mixerAssetPath = UnityEditor.AssetDatabase.GetAssetPath(masterMixer);
            if (string.IsNullOrEmpty(mixerAssetPath))
                return;

            UnityEngine.Object[] mixerSubAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(mixerAssetPath);
            if (mixerSubAssets == null || mixerSubAssets.Length <= 0)
                return;

            int snapshotCount = 0;
            bool hasNamedCoverage = false;
            bool hasNonAttenuationEffect = false;

            for (int i = 0; i < mixerSubAssets.Length; i++)
            {
                UnityEngine.Object subAsset = mixerSubAssets[i];
                if (subAsset == null)
                    continue;

                Type subAssetType = subAsset.GetType();
                if (subAssetType == null)
                    continue;

                string typeName = subAssetType.Name;
                if (typeName == "AudioMixerSnapshotController")
                {
                    snapshotCount++;
                    string snapshotName = subAsset.name;
                    if (snapshotName == "Underwater" ||
                        snapshotName == "UnderwaterSnapshot" ||
                        snapshotName == "BaseInterior" ||
                        snapshotName == "BaseInteriorSnapshot" ||
                        snapshotName == "Surface" ||
                        snapshotName == "SurfaceSnapshot" ||
                        snapshotName == "SurfaceRain" ||
                        snapshotName == "SurfaceRainSnapshot" ||
                        snapshotName == "SurfaceStorm" ||
                        snapshotName == "SurfaceStormSnapshot")
                    {
                        hasNamedCoverage = true;
                    }

                    continue;
                }

                if (typeName != "AudioMixerEffectController")
                    continue;

                SerializedObject effectSerializedObject = new SerializedObject(subAsset);
                SerializedProperty effectNameProperty = effectSerializedObject.FindProperty("m_EffectName");
                if (effectNameProperty == null)
                    continue;

                string effectName = effectNameProperty.stringValue;
                if (!string.IsNullOrEmpty(effectName) && effectName != "Attenuation")
                    hasNonAttenuationEffect = true;
            }

            _validatedMixerSnapshotCount = snapshotCount;
            _validatedMixerHasNamedCoverage = hasNamedCoverage;
            _validatedMixerHasEffectGraph = hasNonAttenuationEffect;

            if (snapshotCount <= 1 || !hasNamedCoverage)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedIncompleteMixerSnapshotAuthoring,
                    $"[AcousticZoneController] MasterMixer snapshot authoring is incomplete. Snapshot count={snapshotCount}. Expected named coverage includes Underwater, BaseInterior, Surface, SurfaceRain, and SurfaceStorm.");
            }

        }
#endif
    }
}
