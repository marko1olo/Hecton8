using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    public partial class FaunaBrain :
        IDamageReceiver,
        ICombatHitProfileSource,
        ICombatPushbackBodySource
    {
        private int _combatDamageTargetId;
        private bool _combatDamageRegistered;
        private bool _combatDamageSyncDirty;
        private bool _interactionTargetRegistered;

        public Vector3 CombatForward => ResolveSelfLogicForward();

        public float CombatHeight => ResolveFaunaCombatHeight();

        public Rigidbody CombatPushbackBody => _rb;

        public void ReceiveDamage(in DamagePacket packet)
        {
            if (packet.Channel != DamageChannel.Integrity || packet.Magnitude <= 0f)
                return;

            Vector3 hitPoint = ResolveCombatPacketWorldPoint(in packet);
            if (!TryApplyAuthoritativeCombatDamagePacket(in packet, hitPoint, out float appliedDamage))
            {
                appliedDamage = packet.Magnitude;
                TakeDamageFromSource(packet.Magnitude, hitPoint);
            }

            if (packet.SourceId == DamageSourceIds.SurvivalBlade)
            {
                float feedbackDamage = appliedDamage > 0f ? appliedDamage : packet.Magnitude;
                ApplyFaunaInteraction(FaunaInteractionKind.Cut, hitPoint, feedbackDamage);
                if (_creatureDamageManager != null)
                    _creatureDamageManager.RegisterWoundWS(hitPoint, feedbackDamage);
            }
        }

        private bool TryApplyAuthoritativeCombatDamagePacket(
            in DamagePacket packet,
            Vector3 hitPoint,
            out float appliedDamage)
        {
            appliedDamage = 0f;

            if (!math.isfinite(packet.PreviousValue) ||
                !math.isfinite(packet.NextValue) ||
                packet.PreviousValue <= packet.NextValue)
            {
                return false;
            }

            if (_isDead)
                return true;

            float previousHealth = _currentHealth;
            float safeMaxHealth = math.max(1f, _maxHealth);
            float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth);
            _currentHealth = math.min(previousHealth, packetNextHealth);
            appliedDamage = math.max(0f, previousHealth - _currentHealth);
            if (appliedDamage <= 0f)
                return true;

            float normalizedDamage = appliedDamage * math.rcp(safeMaxHealth);
            NotifyFoveatedCombatDamageLock();
            TriggerHitFlash(normalizedDamage);

            if (_currentHealth > 0.001f)
                ApplyImmediateHitReaction(hitPoint, normalizedDamage);

            EmitParentalDefenseSignal(hitPoint, normalizedDamage);
            if (_utilityBrain.UsesPredatorRole != 0 && normalizedDamage >= 0.3f)
                TryRegisterPredatorFearNode(normalizedDamage);

            if (_currentHealth <= 0.001f)
                Die();

            return true;
        }

        private void TryRegisterPredatorFearNode(float normalizedDamage)
        {
            IVegetationThreatPulseSink vegetationSink = _vegetationThreatPulseSink;
            if (vegetationSink == null)
                return;

            int speciesId = ComputeStableSpeciesId();
            if (speciesId == 0 ||
                !TryResolveSelfLogicPosition(out Vector3 selfPosition))
            {
                return;
            }

            vegetationSink.RegisterPredatorFearNode(speciesId, selfPosition, normalizedDamage);
        }

        internal void ApplyHibernationHealthSnapshot(float savedHealth)
        {
            if (_isDead)
                return;

            float safeMaxHealth = math.max(1f, _maxHealth);
            float safeHealth = math.isfinite(savedHealth)
                ? math.clamp(savedHealth, 0f, safeMaxHealth)
                : safeMaxHealth;
            _currentHealth = safeHealth;
            MarkCombatDamageSyncDirty();
            QueueCurrentFaunaPresentationShaderState();

            if (_currentHealth <= 0.001f)
                Die();
        }

        private void TryRegisterCombatDamageTarget()
        {
            if (!Application.isPlaying || _isDead)
                return;

            TryRegisterInteractionTargetTree();

            if (_combatDamageRegistered)
                return;

            if (_combatDamageTargetId == 0)
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);

            float safeMaxHealth = math.max(1f, _maxHealth);
            float safeHealth = math.clamp(_currentHealth, 0f, safeMaxHealth);
            CombatArmorClass armorClass = ResolveFaunaCombatArmorClass();
            _combatDamageRegistered = CombatDamageRuntime.RegisterTarget(
                _combatDamageTargetId,
                this,
                safeHealth,
                safeMaxHealth,
                CombatEntityKind.Fauna,
                armorClass,
                ResolveFaunaCombatArmorValue(armorClass),
                0f);
            _combatDamageSyncDirty = !_combatDamageRegistered;
        }

        private void TryUnregisterCombatDamageTarget()
        {
            TryUnregisterInteractionTargetTree();

            if (!_combatDamageRegistered)
                return;

            CombatDamageRuntime.UnregisterTarget(_combatDamageTargetId, this);
            _combatDamageRegistered = false;
            _combatDamageSyncDirty = false;
        }

        private void TryRegisterInteractionTargetTree()
        {
            if (_interactionTargetRegistered || !Application.isPlaying || _isDead)
                return;

            InteractableRegistry.RegisterTree(this);
            _interactionTargetRegistered = true;
        }

        private void TryUnregisterInteractionTargetTree()
        {
            if (!_interactionTargetRegistered)
                return;

            InteractableRegistry.InvalidateTree(this);
            _interactionTargetRegistered = false;
        }

        private void MarkCombatDamageSyncDirty()
        {
            if (!_combatDamageRegistered)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(
                _combatDamageTargetId,
                _currentHealth,
                math.max(1f, _maxHealth));
        }

        private void TryFlushCombatDamageSync()
        {
            if (!Application.isPlaying)
                return;

            if (!_combatDamageRegistered)
            {
                TryRegisterCombatDamageTarget();
                return;
            }

            if (!_combatDamageSyncDirty)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(
                _combatDamageTargetId,
                _currentHealth,
                math.max(1f, _maxHealth));
        }

        private CombatArmorClass ResolveFaunaCombatArmorClass()
        {
            if (IsApexPredator())
                return CombatArmorClass.OrganicHeavy;

            if (isAggressive || _maxHealth >= LargeCorpseResourceMinHealth)
                return CombatArmorClass.Shell;

            return CombatArmorClass.None;
        }

        private float ResolveFaunaCombatArmorValue(CombatArmorClass armorClass)
        {
            switch (armorClass)
            {
                case CombatArmorClass.OrganicHeavy:
                    return 12f;
                case CombatArmorClass.Shell:
                    return 6f;
                default:
                    return 0f;
            }
        }

        private float ResolveFaunaCombatHeight()
        {
            Vector3 scale = transform.lossyScale;
            float height = math.abs(scale.y);
            return math.max(0.5f, height);
        }

        private Vector3 ResolveCombatPacketWorldPoint(in DamagePacket packet)
        {
            float3 localPoint = packet.LocalPoint;
            if (math.all(math.isfinite(localPoint)) && math.lengthsq(localPoint) > 0.000001f)
                return transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, localPoint.z));

            return ResolveFallbackDamageSourcePosition();
        }
    }
}
