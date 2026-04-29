using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Pooled harvestable node with AUP-derived persistence and spatial-hash registration.
    /// Legacy UniqueId support remains for scene-authored compatibility, but authoritative depletion lives in PersistentWorldRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour, IPoolable, ICuttable
    {
        private static readonly int _MeltCenterId = Shader.PropertyToID("_MeltCenter");
        private static readonly int _MeltRadiusId = Shader.PropertyToID("_MeltRadius");
        // COLD ALLOC: RegistryBucket<ResourceNode>[4096] — authored/persistent resource node registry for legacy world-state compatibility — owner: ResourceNode
        private static readonly RegistryBucket<ResourceNode> _worldStateRegistry = new RegistryBucket<ResourceNode>(4096);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _worldStateRegistry.Clear();
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Legacy scene-facing ID retained for WorldStateManager compatibility. Runtime procedural nodes derive this from the AUP tombstone hash.")]
        private string uniqueId;

        [SerializeField]
        [Tooltip("When enabled, the node derives its legacy uniqueId from the AUP tombstone hash.")]
        private bool autoGenerateId = true;

        [SerializeField]
        [Tooltip("Legacy chunk scalar kept for older authoring paths that still expect deterministic scene IDs.")]
        private float chunkSize = 1000f;

        [SerializeField]
        [Tooltip("Optional data-driven resource template applied by the distribution director.")]
        private ResourceNodeTemplate resourceTemplate;

        [Header("Health")]
        [SerializeField, Min(1f)]
        [Tooltip("Maximum integrity before the node resolves and despawns.")]
        private float maxHealth = 100f;

        [Header("Loot")]
        [SerializeField]
        [Tooltip("Legacy pooled loot prefab emitted when the node resolves.")]
        private GameObject lootPrefab;

        [SerializeField, Min(0)]
        [Tooltip("How many pooled pickup pieces this node emits on depletion.")]
        private int lootCount = 3;

        [SerializeField, Min(0f)]
        [Tooltip("Lifetime applied to emitted pooled loot.")]
        private float lootLifetime = 30f;

        [Header("Scatter")]
        [SerializeField, Min(0f)]
        [Tooltip("Randomized scatter radius applied to emitted pooled loot.")]
        private float scatterRadius = 0.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Impulse magnitude applied to emitted pooled loot.")]
        private float scatterForce = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional upward lift applied to emitted pooled loot.")]
        private float upwardBias = 1.5f;

        [Header("Presentation")]
        [SerializeField, Min(0f)]
        [Tooltip("Maximum melt radius driven into the authored shader.")]
        private float maxMeltRadius = 0.5f;

        [SerializeField]
        [Tooltip("Optional explicit renderer receiving the melt property block.")]
        private Renderer targetRenderer;

        private Transform _cachedTransform;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider _boxCollider;
        private SphereCollider _sphereCollider;
        private MaterialPropertyBlock _propertyBlock;
        private Vector4 _localHitPoint;
        private float _currentHealth;
        private bool _isDepleted;
        private bool _despawnRequested;
        private bool _lootSpawnBlockedLogged;
        private bool _registeredToWorldStateRegistry;
        private int _spatialHandle;
        private ulong _persistentTombstoneId;

        /// <summary>Legacy scene-facing ID retained for compatibility systems.</summary>
        public string UniqueId => uniqueId;

        /// <summary>Current node integrity.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Normalized health in the range [0,1].</summary>
        public float HealthNormalized => maxHealth > 0f ? _currentHealth / maxHealth : 0f;

        /// <summary>True once the node has resolved and should not be interacted with again.</summary>
        public bool IsDepleted => _isDepleted;

        /// <summary>Applied authoring template when spawned by the distribution director.</summary>
        public ResourceNodeTemplate ResourceTemplate => resourceTemplate;

        /// <summary>AUP-derived persistence tombstone key used by PersistentWorldRegistry.</summary>
        public ulong PersistentTombstoneId => _persistentTombstoneId;

        /// <summary>Half extents registered into the AUP spatial hash.</summary>
        public Vector3 SpatialHalfExtents
        {
            get
            {
                if (resourceTemplate != null)
                {
                    Vector3 physicalSize = resourceTemplate.PhysicalSize;
                    return new Vector3(
                        Mathf.Max(0.05f, physicalSize.x * 0.5f),
                        Mathf.Max(0.05f, physicalSize.y * 0.5f),
                        Mathf.Max(0.05f, physicalSize.z * 0.5f));
                }

                if (targetRenderer != null)
                    return targetRenderer.bounds.extents;

                return Vector3.one * 0.5f;
            }
        }

        public static int WorldStateRegistryCount => _worldStateRegistry.Count;

        public static ResourceNode GetWorldStateRegistryAt(int index)
        {
            return _worldStateRegistry.GetAt(index);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _boxCollider);
            TryGetComponent(out _sphereCollider);
            targetRenderer = targetRenderer != null
                ? targetRenderer
                : (_meshRenderer != null ? _meshRenderer : ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(_cachedTransform));

            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-node melt shader overrides — owner: ResourceNode
            ResetState();
        }

        private void OnEnable()
        {
            RegisterWorldStateRegistry();

            if (IsPooledInstance())
                return;

            ActivateRuntimeState();
        }

        private void OnDisable()
        {
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
        }

        public void OnSpawn()
        {
            ResetState();
            RegisterWorldStateRegistry();
            ActivateRuntimeState();
        }

        public void OnDespawn()
        {
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            ResetState();
            _persistentTombstoneId = 0UL;
            if (autoGenerateId)
                uniqueId = null;
        }

        /// <summary>
        /// Assigns a legacy compatibility ID explicitly.
        /// </summary>
        public void SetUniqueId(string id)
        {
            uniqueId = id;
            autoGenerateId = false;
        }

        /// <summary>
        /// Applies a runtime template and optional fallback ghost presentation after a pooled spawn.
        /// </summary>
        public void ApplyRuntimeTemplate(ResourceNodeTemplate template, Mesh fallbackMesh, Material fallbackMaterial)
        {
            resourceTemplate = template;
            if (template == null)
                return;

            maxHealth = template.MaxIntegrity;
            lootPrefab = template.LootPickupPrefab;
            lootCount = template.DefaultLootCount;
            ApplyPresentation(template, fallbackMesh, fallbackMaterial);
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);

            if (_spatialHandle != 0)
                WorldSpatialHashGrid.SetResourceHalfExtents(_spatialHandle, SpatialHalfExtents);
        }

        /// <summary>
        /// Re-evaluates the node's AUP identity and spatial registration after a runtime template update.
        /// </summary>
        public void RefreshRuntimeSpatialRegistration()
        {
            if (!isActiveAndEnabled)
                return;

            ResolvePersistentIdentity();
            if (ShouldSuppressSpawn())
            {
                DespawnSelf();
                return;
            }

            if (_spatialHandle == 0)
            {
                RegisterSpatialHandle();
                return;
            }

            WorldSpatialHashGrid.SetResourceHalfExtents(_spatialHandle, SpatialHalfExtents);
            WorldSpatialHashGrid.Refresh(_spatialHandle);
        }

        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage);
            if (!_isDepleted && targetRenderer != null)
                UpdateMeltProperties(hitPoint);
        }

        public void TakeDamage(float amount)
        {
            if (_isDepleted || _despawnRequested || amount <= 0f)
                return;

            float previousHealth = _currentHealth;
            _currentHealth -= amount;

            if (_currentHealth > 0f)
                return;

            if (!TrySpawnLoot())
            {
                _currentHealth = previousHealth;
                return;
            }

            _currentHealth = 0f;
            _isDepleted = true;
            RegisterPersistentDepletion();
            DespawnSelf();
        }

        private void ActivateRuntimeState()
        {
            ResolvePersistentIdentity();
            if (ShouldSuppressSpawn())
            {
                _isDepleted = true;
                DespawnSelf();
                return;
            }

            RegisterSpatialHandle();
        }

        private void ResolvePersistentIdentity()
        {
            _persistentTombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(_cachedTransform.position);

            if (autoGenerateId)
                uniqueId = PersistentWorldRegistry.FormatResourceNodeTombstoneId(_persistentTombstoneId);
        }

        private bool ShouldSuppressSpawn()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null && registry.IsResourceNodeTombstoned(_persistentTombstoneId))
                return true;

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = WorldStateManager.Instance;
                if (worldStateManager != null && worldStateManager.IsNodeDepleted(uniqueId))
                    return true;
            }

            return false;
        }

        private void RegisterPersistentDepletion()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null)
                registry.TryRegisterDestroyedResourceNode(_persistentTombstoneId, _cachedTransform.position);

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = WorldStateManager.Instance;
                if (worldStateManager != null)
                    worldStateManager.RegisterDepletedNode(uniqueId);
            }
        }

        private bool TrySpawnLoot()
        {
            if (lootPrefab == null || lootCount <= 0)
                return true;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                if (!_lootSpawnBlockedLogged)
                {
                    Debug.LogError("[ResourceNode] ObjectPoolManager unavailable. Depletion aborted to prevent loot loss.", this);
                    _lootSpawnBlockedLogged = true;
                }

                return false;
            }

            Vector3 origin = _cachedTransform.position;
            for (int i = 0; i < lootCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * scatterRadius;
                Vector3 spawnPosition = origin + offset;
                Quaternion spawnRotation = Random.rotation;
                GameObject loot = pool.Spawn(lootPrefab, spawnPosition, spawnRotation);
                if (loot == null)
                    continue;

                if (loot.TryGetComponent(out Rigidbody rigidbody))
                {
                    Vector3 force = Random.insideUnitSphere * scatterForce;
                    force.y = Mathf.Abs(force.y) + upwardBias;
                    PhysicsForceRouter.QueueForce(rigidbody, force, ForceMode.Impulse);
                    PhysicsForceRouter.QueueTorque(rigidbody, Random.insideUnitSphere * (scatterForce * 0.5f), ForceMode.Impulse);
                }

                if (lootLifetime > 0f)
                    pool.Despawn(loot, lootLifetime);
            }

            return true;
        }

        private void DespawnSelf()
        {
            if (_despawnRequested)
                return;

            _despawnRequested = true;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void ResetState()
        {
            _currentHealth = Mathf.Max(1f, maxHealth);
            _isDepleted = false;
            _despawnRequested = false;
            _lootSpawnBlockedLogged = false;
            ResetMeltProperties();
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle != 0 || _isDepleted || !isActiveAndEnabled)
                return;

            _spatialHandle = WorldSpatialHashGrid.RegisterResource(this, SpatialHalfExtents);
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void RegisterWorldStateRegistry()
        {
            if (_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Register(this);
            _registeredToWorldStateRegistry = true;
        }

        private void UnregisterWorldStateRegistry()
        {
            if (!_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Unregister(this);
            _registeredToWorldStateRegistry = false;
        }

        private void UpdateMeltProperties(Vector3 worldHitPoint)
        {
            Vector3 localPosition = _cachedTransform.InverseTransformPoint(worldHitPoint);
            _localHitPoint.x = localPosition.x;
            _localHitPoint.y = localPosition.y;
            _localHitPoint.z = localPosition.z;
            _localHitPoint.w = 0f;

            float meltRadius = (1f - HealthNormalized) * maxMeltRadius;
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_MeltCenterId, _localHitPoint);
            _propertyBlock.SetFloat(_MeltRadiusId, meltRadius);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ResetMeltProperties()
        {
            if (targetRenderer == null || _propertyBlock == null)
                return;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_MeltCenterId, Vector4.zero);
            _propertyBlock.SetFloat(_MeltRadiusId, 0f);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ApplyPresentation(ResourceNodeTemplate template, Mesh fallbackMesh, Material fallbackMaterial)
        {
            if (template == null)
                return;

            Vector3 physicalSize = template.PhysicalSize;
            _cachedTransform.localScale = physicalSize;

            if (_meshFilter != null)
                _meshFilter.sharedMesh = template.NodeMesh != null ? template.NodeMesh : fallbackMesh;

            if (_meshRenderer != null)
            {
                Material sharedMaterial = template.NodeMaterial != null ? template.NodeMaterial : fallbackMaterial;
                if (sharedMaterial != null)
                    _meshRenderer.sharedMaterial = sharedMaterial;

                targetRenderer = _meshRenderer;
            }

            ConfigurePrimitiveColliders(template.RuntimeColliderShape, physicalSize);
            ResetMeltProperties();
        }

        private void ConfigurePrimitiveColliders(ResourceNodeTemplate.ColliderShape shape, Vector3 physicalSize)
        {
            Vector3 safeSize = new Vector3(
                Mathf.Max(0.1f, physicalSize.x),
                Mathf.Max(0.1f, physicalSize.y),
                Mathf.Max(0.1f, physicalSize.z));

            if (_boxCollider != null)
            {
                _boxCollider.enabled = shape == ResourceNodeTemplate.ColliderShape.Box;
                _boxCollider.center = Vector3.zero;
                _boxCollider.size = Vector3.one;
            }

            if (_sphereCollider != null)
            {
                _sphereCollider.enabled = shape == ResourceNodeTemplate.ColliderShape.Sphere;
                _sphereCollider.center = Vector3.zero;
                _sphereCollider.radius = 0.5f;
            }
        }

        private bool IsPooledInstance()
        {
            return TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            lootCount = Mathf.Max(0, lootCount);
            lootLifetime = Mathf.Max(0f, lootLifetime);
            scatterRadius = Mathf.Max(0f, scatterRadius);
            scatterForce = Mathf.Max(0f, scatterForce);
            chunkSize = Mathf.Max(1f, chunkSize);
            maxMeltRadius = Mathf.Max(0f, maxMeltRadius);
        }

        [ContextMenu("Generate Deterministic ID")]
        private void EditorGenerateDeterministicId()
        {
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(transform.position);
            uniqueId = PersistentWorldRegistry.FormatResourceNodeTombstoneId(tombstoneId);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnDrawGizmos()
        {
            Vector3 halfExtents = Application.isPlaying ? SpatialHalfExtents : Vector3.one * 0.5f;
            Gizmos.color = !string.IsNullOrEmpty(uniqueId)
                ? new Color(1f, 0.35f, 0.1f, 0.35f)
                : new Color(0.6f, 0.6f, 0.6f, 0.2f);
            Gizmos.DrawWireCube(transform.position, halfExtents * 2f);
        }
#endif
    }
}
