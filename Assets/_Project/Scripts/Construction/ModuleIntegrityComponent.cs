using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Construction
{
    internal enum ModuleDamageOutcome
    {
        None = 0,
        Damaged = 1,
        Catastrophic = 2
    }

    internal enum ModuleRepairOutcome
    {
        None = 0,
        Partial = 1,
        FullyRestored = 2
    }

    /// <summary>
    /// Runtime integrity state extracted from BaseModule so hull wear, flooding, drain, and cascade state
    /// no longer live as ad-hoc logic in the coordinator itself.
    /// Serialized authoring data still originates from BaseModule to avoid prefab/save migration breakage.
    /// </summary>
    [System.Serializable]
    internal sealed class ModuleIntegrityComponent
    {
        private float _maxIntegrity;
        private float _currentIntegrity;
        private bool _isFlooded;
        private float _drainDuration;
        private float _repairWearPerCascade;
        private float _minimumRecoverableIntegrityRatio;
        private float _maxRecoverableIntegrity;
        private BaseModuleFailureMode _failureMode;
        private bool _isDraining;
        private float _drainTimer;
        private bool _wasFlooded;

        public float MaxIntegrity => _maxIntegrity;
        public float CurrentIntegrity => _currentIntegrity;
        public bool IsFlooded => _isFlooded;
        public float DrainDuration => _drainDuration;
        public BaseModuleFailureMode FailureMode => _failureMode;
        public float MaxRecoverableIntegrity => GetRepairIntegrityCap();
        public bool IsDraining => _isDraining;
        public float DrainProgress => _drainDuration > 0.01f ? Mathf.Clamp01(_drainTimer / _drainDuration) : (_isFlooded ? 0f : 1f);
        public bool WasFlooded => _wasFlooded;

        public void Configure(
            float maxIntegrity,
            float currentIntegrity,
            bool flooded,
            float drainDuration,
            float repairWearPerCascade,
            float minimumRecoverableIntegrityRatio,
            float maxRecoverableIntegrity,
            BaseModuleFailureMode failureMode)
        {
            _maxIntegrity = Mathf.Max(1f, maxIntegrity);
            _drainDuration = Mathf.Max(0.1f, drainDuration);
            _repairWearPerCascade = Mathf.Max(0f, repairWearPerCascade);
            _minimumRecoverableIntegrityRatio = Mathf.Clamp(minimumRecoverableIntegrityRatio, 0.1f, 1f);
            _maxRecoverableIntegrity = maxRecoverableIntegrity <= 0f ? _maxIntegrity : maxRecoverableIntegrity;
            _currentIntegrity = currentIntegrity;
            _isFlooded = flooded;
            _failureMode = failureMode;
            EnsureRepairIntegrityCapInitialized();
            _currentIntegrity = Mathf.Clamp(_currentIntegrity, 0f, GetRepairIntegrityCap());
            _wasFlooded = _isFlooded;
        }

        public void ResetForSpawn()
        {
            EnsureRepairIntegrityCapInitialized();
            _currentIntegrity = Mathf.Clamp(_currentIntegrity, 0f, GetRepairIntegrityCap());
            _wasFlooded = _isFlooded;
        }

        public void ResetForDespawn()
        {
            _isDraining = false;
            _drainTimer = 0f;
            _failureMode = BaseModuleFailureMode.None;
            _maxRecoverableIntegrity = _maxIntegrity;
            _currentIntegrity = _maxIntegrity;
            _isFlooded = false;
            _wasFlooded = false;
        }

        public void RestoreState(float integrity, bool flooded, BaseModuleFailureMode failureMode, float repairIntegrityCap)
        {
            EnsureRepairIntegrityCapInitialized();
            _maxRecoverableIntegrity = Mathf.Clamp(repairIntegrityCap, _maxIntegrity * _minimumRecoverableIntegrityRatio, _maxIntegrity);
            _currentIntegrity = Mathf.Clamp(integrity, 0f, GetRepairIntegrityCap());
            _isFlooded = flooded;
            _wasFlooded = flooded;
            _failureMode = failureMode;
            _isDraining = false;
            _drainTimer = 0f;
        }

        public void SetCurrentIntegrity(float value)
        {
            _currentIntegrity = Mathf.Clamp(value, 0f, GetRepairIntegrityCap());
        }

        public void SetFlooded(bool flooded)
        {
            _isFlooded = flooded;
        }

        public float GetRepairIntegrityCap()
        {
            return Mathf.Clamp(_maxRecoverableIntegrity, ResolveMinimumRepairIntegrityCap(), _maxIntegrity);
        }

        public int ResolveRemainingRepairCycles()
        {
            EnsureRepairIntegrityCapInitialized();

            if (_repairWearPerCascade <= 0f)
                return -1;

            float minimumCap = ResolveMinimumRepairIntegrityCap();
            float remainingRecoverableMargin = _maxRecoverableIntegrity - minimumCap;
            if (remainingRecoverableMargin <= 0f)
                return 0;

            return Mathf.FloorToInt(remainingRecoverableMargin / _repairWearPerCascade + 0.0001f);
        }

        public bool HasOperationalPower(bool hasPower)
        {
            return hasPower && _failureMode != BaseModuleFailureMode.ShortCircuit;
        }

        public bool ShouldLeakBeActive()
        {
            if (_failureMode == BaseModuleFailureMode.OxygenLeak ||
                _failureMode == BaseModuleFailureMode.Fire)
            {
                return true;
            }

            return _currentIntegrity < GetRepairIntegrityCap() && _currentIntegrity > 0f;
        }

        public bool ShouldSyncFloodState()
        {
            return _isFlooded != _wasFlooded;
        }

        public void MarkFloodStateSynchronized()
        {
            _wasFlooded = _isFlooded;
        }

        public void ApplyMaterialFatigue()
        {
            EnsureRepairIntegrityCapInitialized();

            if (_repairWearPerCascade <= 0f)
                return;

            float minimumCap = ResolveMinimumRepairIntegrityCap();
            _maxRecoverableIntegrity -= _repairWearPerCascade;
            if (_maxRecoverableIntegrity < minimumCap)
                _maxRecoverableIntegrity = minimumCap;
        }

        public bool ClampRepairIntegrityCap(float repairIntegrityCap)
        {
            EnsureRepairIntegrityCapInitialized();

            float nextCap = Mathf.Clamp(repairIntegrityCap, ResolveMinimumRepairIntegrityCap(), _maxIntegrity);
            if (_maxRecoverableIntegrity <= nextCap + 0.0001f)
                return false;

            _maxRecoverableIntegrity = nextCap;
            if (_currentIntegrity > _maxRecoverableIntegrity)
                _currentIntegrity = _maxRecoverableIntegrity;

            return true;
        }

        public bool RestoreRepairIntegrityCap(float amount)
        {
            if (amount <= 0f)
                return false;

            EnsureRepairIntegrityCapInitialized();

            float nextCap = Mathf.Clamp(
                _maxRecoverableIntegrity + amount,
                ResolveMinimumRepairIntegrityCap(),
                _maxIntegrity);
            if (nextCap <= _maxRecoverableIntegrity + 0.0001f)
                return false;

            _maxRecoverableIntegrity = nextCap;
            return true;
        }

        public ModuleDamageOutcome ApplyDamage(float amount)
        {
            if (amount <= 0f || _currentIntegrity <= 0f)
                return ModuleDamageOutcome.None;

            _currentIntegrity -= amount;
            if (_currentIntegrity > 0f)
                return ModuleDamageOutcome.Damaged;

            _currentIntegrity = 0f;
            return ModuleDamageOutcome.Catastrophic;
        }

        public ModuleRepairOutcome Repair(float amount)
        {
            if (amount <= 0f)
                return ModuleRepairOutcome.None;

            float repairCap = GetRepairIntegrityCap();
            if (_currentIntegrity >= repairCap && !_isFlooded)
                return ModuleRepairOutcome.None;

            _currentIntegrity += amount;
            if (_currentIntegrity < repairCap)
                return ModuleRepairOutcome.Partial;

            _currentIntegrity = repairCap;
            return ModuleRepairOutcome.FullyRestored;
        }

        public void TriggerCascadeFailure(BaseModuleFailureMode resolvedFailureMode)
        {
            ApplyMaterialFatigue();
            _failureMode = resolvedFailureMode;
            _isDraining = false;
            _drainTimer = 0f;

            switch (_failureMode)
            {
                case BaseModuleFailureMode.Fire:
                    _isFlooded = false;
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    _isFlooded = true;
                    break;
                default:
                    _failureMode = BaseModuleFailureMode.OxygenLeak;
                    _isFlooded = true;
                    break;
            }
        }

        public void ClearCascadeFailure()
        {
            _failureMode = BaseModuleFailureMode.None;
        }

        public void ForceFlood()
        {
            _isFlooded = true;
            StopDrain();
        }

        public void ForceDrainComplete(bool clearOxygenLeakFailure)
        {
            _isFlooded = false;
            StopDrain();
            if (clearOxygenLeakFailure && _failureMode == BaseModuleFailureMode.OxygenLeak)
                _failureMode = BaseModuleFailureMode.None;
        }

        public bool TryStartDrain(bool hasOperationalPower)
        {
            if (!HasOperationalPower(hasOperationalPower))
                return false;

            if (!_isFlooded || _currentIntegrity < GetRepairIntegrityCap())
                return false;

            bool startedNow = !_isDraining;
            _isDraining = true;
            return startedNow;
        }

        public void StopDrain()
        {
            _isDraining = false;
            _drainTimer = 0f;
        }

        public bool AdvanceDrain(float dt)
        {
            if (!_isDraining)
                return false;

            _drainTimer += dt;
            if (_drainTimer < _drainDuration)
                return false;

            _drainTimer = _drainDuration;
            return true;
        }

        public BaseModuleFailureMode ResolveCascadeFailureMode(string prefabId, Vector3 worldPosition, bool hasPower)
        {
            int hash = 17;

            if (!string.IsNullOrEmpty(prefabId))
            {
                int length = prefabId.Length;
                for (int i = 0; i < length; i++)
                    hash = hash * 31 + prefabId[i];
            }

            hash = hash * 31 + Mathf.RoundToInt(worldPosition.x * 10f);
            hash = hash * 31 + Mathf.RoundToInt(worldPosition.y * 10f);
            hash = hash * 31 + Mathf.RoundToInt(worldPosition.z * 10f);

            int resolved = Mathf.Abs(hash % 3);
            if (!hasPower && resolved == 2)
                resolved = 0;

            switch (resolved)
            {
                case 1:
                    return BaseModuleFailureMode.Fire;
                case 2:
                    return BaseModuleFailureMode.ShortCircuit;
                default:
                    return BaseModuleFailureMode.OxygenLeak;
            }
        }

        private float ResolveMinimumRepairIntegrityCap()
        {
            float minimumCap = _maxIntegrity * _minimumRecoverableIntegrityRatio;
            if (minimumCap < 1f)
                minimumCap = 1f;

            return minimumCap;
        }

        private void EnsureRepairIntegrityCapInitialized()
        {
            if (_maxRecoverableIntegrity <= 0f)
                _maxRecoverableIntegrity = _maxIntegrity;

            _maxRecoverableIntegrity = GetRepairIntegrityCap();
        }
    }
}
