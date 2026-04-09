// ============================================================================
// HECTON-8 — RuntimeSurvivalStats.cs
// Mutable runtime wrapper над SurvivalStats для системы апгрейдов.
//
// SurvivalStats — immutable SO (private setters).
// RuntimeSurvivalStats — ScriptableObject с публичными setters,
// создаётся через ScriptableObject.CreateInstance<>() в SuitUpgradeManager.
//
// АРХИТЕКТУРА:
//   • Наследует SurvivalStats — совместим с HectonSurvivalSystem.OverrideStats().
//   • ApplyDeltas() пересчитывает все параметры из base + дельты.
//   • Не сохраняется на диск — пересоздаётся при загрузке.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Mutable runtime версия SurvivalStats для применения апгрейдов.
    /// Создаётся через ScriptableObject.CreateInstance — не является ассетом.
    /// </summary>
    public sealed class RuntimeSurvivalStats : SurvivalStats
    {
        // Mutable overrides — теневые поля поверх базовых
        private float _maxOxygen;
        private float _maxEnergy;
        private float _maxIntegrity;
        private float _safeDepth;
        private float _minSafeTemp;
        private float _maxSafeTemp;
        private float _radiationThreshold;

        private bool _initialized;

        // Override properties
        public override float MaxOxygen           => _initialized ? _maxOxygen           : base.MaxOxygen;
        public override float MaxEnergy           => _initialized ? _maxEnergy           : base.MaxEnergy;
        public override float MaxIntegrity        => _initialized ? _maxIntegrity        : base.MaxIntegrity;
        public override float SafeDepth           => _initialized ? _safeDepth           : base.SafeDepth;
        public override float MinSafeTemp         => _initialized ? _minSafeTemp         : base.MinSafeTemp;
        public override float MaxSafeTemp         => _initialized ? _maxSafeTemp         : base.MaxSafeTemp;
        public override float RadiationThreshold  => _initialized ? _radiationThreshold  : base.RadiationThreshold;

        /// <summary>
        /// Применяет дельты апгрейдов поверх базовых параметров.
        /// </summary>
        public void ApplyDeltas(
            SurvivalStats baseStats,
            float dOxygen, float dEnergy, float dDepth, float dIntegrity,
            float dMinTemp, float dMaxTemp, float dRad)
        {
            if (baseStats == null) return;

            _maxOxygen          = Mathf.Max(1f, baseStats.MaxOxygen          + dOxygen);
            _maxEnergy          = Mathf.Max(1f, baseStats.MaxEnergy          + dEnergy);
            _maxIntegrity       = Mathf.Max(1f, baseStats.MaxIntegrity       + dIntegrity);
            _safeDepth          = Mathf.Max(0f, baseStats.SafeDepth          + dDepth);
            _minSafeTemp        = baseStats.MinSafeTemp + dMinTemp;
            _maxSafeTemp        = Mathf.Max(_minSafeTemp + 1f, baseStats.MaxSafeTemp + dMaxTemp);
            _radiationThreshold = Mathf.Max(0f, baseStats.RadiationThreshold + dRad);

            _initialized = true;
        }
    }
}
