using System;
using UnityEngine;

namespace CandiceAIforGames.AI
{
    public class CandiceProjectile : MonoBehaviour
    {
        private const int MaxRegisteredProjectiles = 64;
        // COLD ALLOC: CandiceProjectile[64] - active Candice projectile registry for possession lookup - owner: CandiceProjectile
        private static readonly CandiceProjectile[] RegisteredProjectiles = new CandiceProjectile[MaxRegisteredProjectiles];

        public Rigidbody rb;
        public GameObject target;
        public float attackDamage = 10f;
        public float moveSpeed = 200f;
        public bool destroyOnCollision = true;
        public bool destroyAfterDelay = false;
        public float destroyDelay = 5f;
        public float collisionDelay = 2f;
        public bool isFired = false;
        public bool stopOnCollision = true;
        private float timeElapsed = 0;
        public bool followTarget = false;
        public bool useForce = false;
        private bool _deactivateScheduled;
        private float _deactivateAt;
        private Transform _initialParent;
        [SerializeField] private Transform _cachedCameraParent;
        [SerializeField] private Transform _cachedVsfxRoot;
        private RigidbodyConstraints _initialConstraints;

        public Transform CachedCameraParent => _cachedCameraParent;
        public Transform CachedVsfxRoot => _cachedVsfxRoot;

#if UNITY_EDITOR
        private void Reset()
        {
            if (_cachedCameraParent == null) _cachedCameraParent = transform.Find("CameraParent");
            if (_cachedVsfxRoot == null) _cachedVsfxRoot = transform.Find("VSFX");
        }
#endif

        // Start is called before the first frame update
        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            _initialParent = transform.parent;
            _initialConstraints = rb == null ? RigidbodyConstraints.None : rb.constraints;
        }

        void OnEnable()
        {
            RegisterProjectile(this);
            timeElapsed = 0f;
            if (destroyAfterDelay)
            {
                ScheduleDeactivate(destroyDelay);
            }
        }

        void OnDisable()
        {
            UnregisterProjectile(this);
            _deactivateScheduled = false;
        }

        // Update is called once per frame
        void Update()
        {
            timeElapsed += Time.deltaTime;
            if (_deactivateScheduled && Time.time >= _deactivateAt)
            {
                _deactivateScheduled = false;
                isFired = false;
                gameObject.SetActive(false);
                return;
            }

            if (isFired)
            {
                if(followTarget && target != null)
                {
                    transform.LookAt(new Vector3(target.transform.position.x, gameObject.transform.position.y, target.transform.position.z));
                }
                Move();
            }
        }
        public void Fire(GameObject attackTarget)
        {
            ResetProjectileStateForReuse();
            target = attackTarget;
            if (target == null)
            {
                isFired = false;
                return;
            }

            transform.LookAt(new Vector3(target.transform.position.x, gameObject.transform.position.y-1, target.transform.position.z));
            isFired = true;
            if (destroyAfterDelay)
            {
                ScheduleDeactivate(destroyDelay);
            }
        }

        private void Move()
        {
            if(useForce)
                rb.linearVelocity = transform.forward * moveSpeed * Time.deltaTime;
            else
                transform.position += transform.forward * 10 * Time.deltaTime;
        }


        void OnTriggerEnter(Collider collider)
        {
            DealDamage(collider.gameObject);
            //Check if destroyOnCollision is enabled and check if collided object is the target. 
            if (destroyOnCollision && target != null && collider.gameObject == target.gameObject)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("Collided with: " + collider.gameObject.name);
#endif
                ScheduleDeactivate(collisionDelay);


                if (stopOnCollision)
                {
                    isFired = false;
                    gameObject.transform.SetParent(collider.gameObject.transform);
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }

            }
        }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject != gameObject && isFired)
            {
                //Debug.Log("Collided with: " + collision.gameObject.name);
                //Debug.Log("Fire True: " + fireTrue);
                DealDamage(collision.gameObject);
                if (stopOnCollision)
                {
                    isFired = false;
                    gameObject.transform.SetParent(collision.gameObject.transform);
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
                if (destroyOnCollision)
                {
                    ScheduleDeactivate(collisionDelay);

                }
            }
        }
        void DealDamage(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent(out CandiceAIController aiController))
            {
                aiController.CandiceReceiveDamage(attackDamage);
                return;
            }

            if (go.TryGetComponent(out global::BasicPlayerController playerController))
            {
                playerController.CandiceReceiveDamage(attackDamage);
            }
        }

        public void ScheduleDeactivate(float delay)
        {
            _deactivateAt = Time.time + Mathf.Max(0f, delay);
            _deactivateScheduled = true;
        }

        private void ResetProjectileStateForReuse()
        {
            transform.SetParent(_initialParent);
            if (rb != null)
            {
                rb.constraints = _initialConstraints;
            }
        }

        public static bool TryGetActiveProjectile(out GameObject projectile)
        {
            if (TryGetActiveProjectileComponent(out CandiceProjectile projectileComponent))
            {
                projectile = projectileComponent.gameObject;
                return true;
            }

            projectile = null;
            return false;
        }

        public static bool TryGetActiveProjectileComponent(out CandiceProjectile projectile)
        {
            for (int i = 0; i < RegisteredProjectiles.Length; i++)
            {
                CandiceProjectile candidate = RegisteredProjectiles[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    projectile = candidate;
                    return true;
                }
            }

            projectile = null;
            return false;
        }

        private static void RegisterProjectile(CandiceProjectile projectile)
        {
            for (int i = 0; i < RegisteredProjectiles.Length; i++)
            {
                if (ReferenceEquals(RegisteredProjectiles[i], projectile))
                {
                    return;
                }

                if (RegisteredProjectiles[i] == null)
                {
                    RegisteredProjectiles[i] = projectile;
                    return;
                }
            }
        }

        private static void UnregisterProjectile(CandiceProjectile projectile)
        {
            for (int i = 0; i < RegisteredProjectiles.Length; i++)
            {
                if (ReferenceEquals(RegisteredProjectiles[i], projectile))
                {
                    RegisteredProjectiles[i] = null;
                    return;
                }
            }
        }
    }
}
