using Hecton8.AI;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnvironmentalAnalyzerTool : PlayerTool
    {
        [Header("Analysis")]
        [SerializeField] private float range = 14f;
        [SerializeField] private float analysisCooldown = 0.4f;
        [SerializeField] private LayerMask analysisMask = ~0;

        private Transform _cachedTransform;
        private HectonSurvivalSystem _survival;
        private HUDNotification _notification;
        private float _cooldown;

        [SerializeField] private string _debugLastMessage;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void OnEquip()
        {
            base.OnEquip();

            if (_survival == null)
                _survival = FindFirstObjectByType<HectonSurvivalSystem>();

            if (_notification == null)
                _notification = FindFirstObjectByType<HUDNotification>();
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            string message;

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                analysisMask,
                QueryTriggerInteraction.Collide))
            {
                message = BuildTargetMessage(hit);
            }
            else
            {
                message = "ANALYZER: NO TARGET RETURN";
            }

            Publish(message);
            _cooldown = analysisCooldown;
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (_survival == null)
                _survival = FindFirstObjectByType<HectonSurvivalSystem>();

            if (_survival == null)
            {
                Publish("ANALYZER: SURVIVAL LINK OFFLINE");
            }
            else
            {
                Publish(
                    $"SUIT STATUS | DEPTH {-_survival.Depth:0} m | P { _survival.Pressure:0.0} atm | O2 {_survival.OxygenNormalized * 100f:0}% | EN {_survival.EnergyNormalized * 100f:0}% | HLT {_survival.IntegrityNormalized * 100f:0}%");
            }

            _cooldown = analysisCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }

        private string BuildTargetMessage(RaycastHit hit)
        {
            Collider collider = hit.collider;
            if (collider == null)
                return "ANALYZER: INVALID HIT";

            if (collider.TryGetComponent(out HectonItem item))
            {
                return $"ITEM | {item.Data.itemName.ToUpperInvariant()} | QTY {item.Quantity}";
            }

            HectonItem parentItem = collider.GetComponentInParent<HectonItem>();
            if (parentItem != null)
                return $"ITEM | {parentItem.Data.itemName.ToUpperInvariant()} | QTY {parentItem.Quantity}";

            if (collider.TryGetComponent(out ResourceNode _))
                return $"RESOURCE NODE | RANGE {hit.distance:0.0} m";

            if (collider.TryGetComponent(out BaseModule module))
                return module.CanDeconstruct()
                    ? $"BASE MODULE | CUTTABLE | RANGE {hit.distance:0.0} m"
                    : $"BASE MODULE | SEALED | RANGE {hit.distance:0.0} m";

            if (collider.TryGetComponent(out HectonBaseAI _))
                return $"BIOFORM | ACTIVE | RANGE {hit.distance:0.0} m";

            HectonBaseAI aiParent = collider.GetComponentInParent<HectonBaseAI>();
            if (aiParent != null)
                return $"BIOFORM | ACTIVE | RANGE {hit.distance:0.0} m";

            if (ToolHitUtility.TryGetRigidbody(collider, out Rigidbody body))
                return $"MASS OBJECT | {body.mass:0.0} kg | RANGE {hit.distance:0.0} m";

            return $"UNCLASSIFIED | {collider.gameObject.name.ToUpperInvariant()} | RANGE {hit.distance:0.0} m";
        }

        private void Publish(string message)
        {
            _debugLastMessage = message;

            if (_notification == null)
                _notification = FindFirstObjectByType<HUDNotification>();

            if (_notification != null)
                _notification.ShowInfo(message);
            else
                Debug.Log($"[EnvironmentalAnalyzer] {message}");
        }
    }
}
