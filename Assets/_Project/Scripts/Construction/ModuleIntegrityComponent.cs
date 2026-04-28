using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [System.Flags]
    internal enum ModuleStateFlags : byte
    {
        None = 0,
        Flooded = 1 << 0,
        Draining = 1 << 1,
        WasFlooded = 1 << 2
    }

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
        private float _drainDuration;
        private float _repairWearPerCascade;
        private float _minimumRecoverableIntegrityRatio;
        private float _maxRecoverableIntegrity;
        private BaseModuleFailureMode _failureMode;
        private ModuleStateFlags _stateFlags;
        private float _drainTimer;

        public float MaxIntegrity => _maxIntegrity;
        public float CurrentIntegrity => _currentIntegrity;
        public bool IsFlooded => HasStateFlag(ModuleStateFlags.Flooded);
        public float DrainDuration => _drainDuration;
        public BaseModuleFailureMode FailureMode => _failureMode;
        public float MaxRecoverableIntegrity => GetRepairIntegrityCap();
        public bool IsDraining => HasStateFlag(ModuleStateFlags.Draining);
        public float DrainProgress => _drainDuration > 0.01f ? math.saturate(_drainTimer / _drainDuration) : (IsFlooded ? 0f : 1f);
        public bool WasFlooded => HasStateFlag(ModuleStateFlags.WasFlooded);

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
            _maxIntegrity = math.max(1f, maxIntegrity);
            _drainDuration = math.max(0.1f, drainDuration);
            _repairWearPerCascade = math.max(0f, repairWearPerCascade);
            _minimumRecoverableIntegrityRatio = math.clamp(minimumRecoverableIntegrityRatio, 0.1f, 1f);
            _maxRecoverableIntegrity = maxRecoverableIntegrity <= 0f ? _maxIntegrity : maxRecoverableIntegrity;
            _currentIntegrity = currentIntegrity;
            _failureMode = failureMode;
            EnsureRepairIntegrityCapInitialized();
            _currentIntegrity = math.clamp(_currentIntegrity, 0f, GetRepairIntegrityCap());
            _stateFlags = ModuleStateFlags.None;
            SetStateFlag(ModuleStateFlags.Flooded, flooded);
            SetStateFlag(ModuleStateFlags.WasFlooded, flooded);
        }

        public void ResetForSpawn()
        {
            EnsureRepairIntegrityCapInitialized();
            _currentIntegrity = math.clamp(_currentIntegrity, 0f, GetRepairIntegrityCap());
            SetStateFlag(ModuleStateFlags.Draining, false);
            SetStateFlag(ModuleStateFlags.WasFlooded, IsFlooded);
        }

        public void ResetForDespawn()
        {
            SetStateFlag(ModuleStateFlags.Draining, false);
            _drainTimer = 0f;
            _failureMode = BaseModuleFailureMode.None;
            _maxRecoverableIntegrity = _maxIntegrity;
            _currentIntegrity = _maxIntegrity;
            _stateFlags = ModuleStateFlags.None;
        }

        public void RestoreState(float integrity, bool flooded, BaseModuleFailureMode failureMode, float repairIntegrityCap)
        {
            EnsureRepairIntegrityCapInitialized();
            _maxRecoverableIntegrity = math.clamp(repairIntegrityCap, _maxIntegrity * _minimumRecoverableIntegrityRatio, _maxIntegrity);
            _currentIntegrity = math.clamp(integrity, 0f, GetRepairIntegrityCap());
            _stateFlags = ModuleStateFlags.None;
            SetStateFlag(ModuleStateFlags.Flooded, flooded);
            SetStateFlag(ModuleStateFlags.WasFlooded, flooded);
            _failureMode = failureMode;
            _drainTimer = 0f;
        }

        public void SetCurrentIntegrity(float value)
        {
            _currentIntegrity = math.clamp(value, 0f, GetRepairIntegrityCap());
        }

        public void SetFlooded(bool flooded)
        {
            SetStateFlag(ModuleStateFlags.Flooded, flooded);
        }

        public float GetRepairIntegrityCap()
        {
            return math.clamp(_maxRecoverableIntegrity, ResolveMinimumRepairIntegrityCap(), _maxIntegrity);
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

            return (int)math.floor(remainingRecoverableMargin / _repairWearPerCascade + 0.0001f);
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
            return IsFlooded != WasFlooded;
        }

        public void MarkFloodStateSynchronized()
        {
            SetStateFlag(ModuleStateFlags.WasFlooded, IsFlooded);
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

            float nextCap = math.clamp(repairIntegrityCap, ResolveMinimumRepairIntegrityCap(), _maxIntegrity);
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

            float nextCap = math.clamp(
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
            if (_currentIntegrity >= repairCap && !IsFlooded)
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
            SetStateFlag(ModuleStateFlags.Draining, false);
            _drainTimer = 0f;

            switch (_failureMode)
            {
                case BaseModuleFailureMode.Fire:
                    SetStateFlag(ModuleStateFlags.Flooded, false);
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    SetStateFlag(ModuleStateFlags.Flooded, true);
                    break;
                default:
                    _failureMode = BaseModuleFailureMode.OxygenLeak;
                    SetStateFlag(ModuleStateFlags.Flooded, true);
                    break;
            }
        }

        public void ClearCascadeFailure()
        {
            _failureMode = BaseModuleFailureMode.None;
        }

        public void ForceFlood()
        {
            SetStateFlag(ModuleStateFlags.Flooded, true);
            StopDrain();
        }

        public void ForceDrainComplete(bool clearOxygenLeakFailure)
        {
            SetStateFlag(ModuleStateFlags.Flooded, false);
            StopDrain();
            if (clearOxygenLeakFailure && _failureMode == BaseModuleFailureMode.OxygenLeak)
                _failureMode = BaseModuleFailureMode.None;
        }

        public bool TryStartDrain(bool hasOperationalPower)
        {
            if (!HasOperationalPower(hasOperationalPower))
                return false;

            if (!IsFlooded || _currentIntegrity < GetRepairIntegrityCap())
                return false;

            bool startedNow = !IsDraining;
            SetStateFlag(ModuleStateFlags.Draining, true);
            return startedNow;
        }

        public void StopDrain()
        {
            SetStateFlag(ModuleStateFlags.Draining, false);
            _drainTimer = 0f;
        }

        public bool AdvanceDrain(float dt)
        {
            if (!IsDraining)
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

            hash = hash * 31 + (int)math.round(worldPosition.x * 10f);
            hash = hash * 31 + (int)math.round(worldPosition.y * 10f);
            hash = hash * 31 + (int)math.round(worldPosition.z * 10f);

            int resolved = math.abs(hash % 3);
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

        private bool HasStateFlag(ModuleStateFlags flag)
        {
            return (_stateFlags & flag) != 0;
        }

        private void SetStateFlag(ModuleStateFlags flag, bool enabled)
        {
            if (enabled)
                _stateFlags |= flag;
            else
                _stateFlags &= ~flag;
        }

        private void EnsureRepairIntegrityCapInitialized()
        {
            if (_maxRecoverableIntegrity <= 0f)
                _maxRecoverableIntegrity = _maxIntegrity;

            _maxRecoverableIntegrity = GetRepairIntegrityCap();
        }
    }
}
