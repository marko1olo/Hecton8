// ============================================================================
// HECTON-8 — FlowFieldProfile.cs
// Профиль настроек для FlowFieldVisualizer.
//
// Позволяет сохранять и переиспользовать конфигурации визуализации
// для разных сценариев (тестирование течений, баланс геймплея и т.д.).
// ============================================================================

using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// ScriptableObject-профиль с сериализуемыми настройками визуализатора течений.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FlowFieldProfile",
        menuName = "Hecton/Physics/Flow Field Profile",
        order = 41)]
    public sealed class FlowFieldProfile : ScriptableObject
    {
        [Header("Grid Settings")]
        [Min(1f)] public float areaWidth = 50f;
        [Min(1f)] public float areaHeight = 50f;
        [Min(2)] public int gridResolutionX = 20;
        [Min(2)] public int gridResolutionY = 20;
        [Min(0f)] public float sampleHeight = 0.5f;

        [Header("Arrow Settings")]
        [Min(0.1f)] public float arrowLength = 2f;
        [Min(0.01f)] public float arrowThickness = 0.05f;
        [Min(0.1f)] public float maxForceScale = 5f;

        [Header("Performance")]
        [Min(5)] public int maxGridResolution = 50;
        [Min(100)] public int asyncThreshold = 1000;
        [Min(0.1f)] public float asyncTimeout = 2f;
        public bool useBurstSampling = true;
        public bool useJobSystem = true;

        [Header("Advanced Visualization")]
        public bool useHDRColors = true;
        public bool animateInEditor = false;
        [Min(0.1f)] public float animationSpeed = 1f;
        public bool useParticleEffects = false;

        [Header("Visualization")]
        public ArrowStyle arrowStyle = ArrowStyle.Arrows;
        public bool showForceLabels = false;
        [Min(8)] public int labelFontSize = 12;
        public bool cullWeakFlows = true;
        [Min(0.01f)] public float minFlowStrength = 0.1f;

        [Header("Current Sources")]
        public bool showGlobalCurrent = true;
        public bool showLocalCurrents = true;
        public bool onlySelectedVolumes = false;

        /// <summary>Применяет настройки профиля к визуализатору.</summary>
        public void ApplyTo(FlowFieldVisualizer visualizer)
        {
            if (visualizer == null) return;

            visualizer.AreaSize = new Vector2(areaWidth, areaHeight);
            visualizer.GridResolution = new Vector2Int(gridResolutionX, gridResolutionY);
            visualizer.SampleHeight = sampleHeight;
            visualizer.ArrowLength = arrowLength;
            visualizer.ArrowThickness = arrowThickness;
            visualizer.MaxForceScale = maxForceScale;
            visualizer.MaxGridResolution = maxGridResolution;
            visualizer.AsyncThreshold = asyncThreshold;
            visualizer.AsyncTimeout = asyncTimeout;
            visualizer.UseBurstSampling = useBurstSampling;
            visualizer.UseJobSystem = useJobSystem;
            visualizer.ArrowStyle = arrowStyle;
            visualizer.ShowForceLabels = showForceLabels;
            visualizer.LabelFontSize = labelFontSize;
            visualizer.CullWeakFlows = cullWeakFlows;
            visualizer.MinFlowStrength = minFlowStrength;
            visualizer.UseHDRColors = useHDRColors;
            visualizer.AnimateInEditor = animateInEditor;
            visualizer.AnimationSpeed = animationSpeed;
            visualizer.UseParticleEffects = useParticleEffects;
            visualizer.ShowGlobalCurrent = showGlobalCurrent;
            visualizer.ShowLocalCurrents = showLocalCurrents;
            visualizer.OnlySelectedVolumes = onlySelectedVolumes;

            visualizer.Recalculate();
        }

        /// <summary>Сохраняет текущие настройки визуализатора в профиль.</summary>
        public void CaptureFrom(FlowFieldVisualizer visualizer)
        {
            if (visualizer == null) return;

            areaWidth = visualizer.AreaSize.x;
            areaHeight = visualizer.AreaSize.y;
            gridResolutionX = visualizer.GridResolution.x;
            gridResolutionY = visualizer.GridResolution.y;
            sampleHeight = visualizer.SampleHeight;
            arrowLength = visualizer.ArrowLength;
            arrowThickness = visualizer.ArrowThickness;
            maxForceScale = visualizer.MaxForceScale;
            maxGridResolution = visualizer.MaxGridResolution;
            asyncThreshold = visualizer.AsyncThreshold;
            asyncTimeout = visualizer.AsyncTimeout;
            useBurstSampling = visualizer.UseBurstSampling;
            useJobSystem = visualizer.UseJobSystem;
            arrowStyle = visualizer.ArrowStyle;
            showForceLabels = visualizer.ShowForceLabels;
            labelFontSize = visualizer.LabelFontSize;
            cullWeakFlows = visualizer.CullWeakFlows;
            minFlowStrength = visualizer.MinFlowStrength;
            useHDRColors = visualizer.UseHDRColors;
            animateInEditor = visualizer.AnimateInEditor;
            animationSpeed = visualizer.AnimationSpeed;
            useParticleEffects = visualizer.UseParticleEffects;
            showGlobalCurrent = visualizer.ShowGlobalCurrent;
            showLocalCurrents = visualizer.ShowLocalCurrents;
            onlySelectedVolumes = visualizer.OnlySelectedVolumes;
        }
    }
}
