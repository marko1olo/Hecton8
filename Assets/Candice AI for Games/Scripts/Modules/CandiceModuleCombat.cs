using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CandiceAIforGames.AI
{
    public class CandiceModuleCombat: CandiceBaseModule
    {
        private const int MaxDamageHits = 64;
        // COLD ALLOC: RaycastHit[64] - bounded 3D damage query scratch - owner: CandiceModuleCombat
        private static readonly RaycastHit[] DamageHits = new RaycastHit[MaxDamageHits];
        // COLD ALLOC: RaycastHit2D[64] - bounded 2D damage query scratch - owner: CandiceModuleCombat
        private static readonly RaycastHit2D[] DamageHits2D = new RaycastHit2D[MaxDamageHits];
        private static ContactFilter2D Damage2DFilter = ContactFilter2D.noFilter;
        private const int MaxProjectilePool = 16;
        // COLD ALLOC: GameObject[16] - bounded ranged attack projectile pool - owner: CandiceModuleCombat
        private readonly GameObject[] projectilePool = new GameObject[MaxProjectilePool];
        private GameObject projectilePoolPrefab;

        Transform transform;
        public Action<bool> attackCompleteCallback;
        public CandiceModuleCombat(Transform transform, Action<bool> _attackCompleteCallback,string moduleName = "CandiceModuleCombat"):base(moduleName) {
            this.transform = transform;
            this.attackCompleteCallback = _attackCompleteCallback;
        }

        public void DealDamage(float damage,float attackRange, float damageAngle,List<string> tags)
        {
            //
            //Method Name : void Attack()
            //Purpose     : This method is called by the attack animation event. Deals the required damage to all targets in range..
            //Re-use      : none
            //Input       : none
            //Output      : none
            //

            int hitCount = Physics.SphereCastNonAlloc(transform.position, attackRange, transform.forward, DamageHits, attackRange);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount == DamageHits.Length)
            {
                Debug.LogWarning("Candice damage hit buffer saturated. Increase MaxDamageHits.");
            }
#endif
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = DamageHits[i];
                if (hit.transform == null)
                {
                    continue;
                }

                GameObject hitObject = hit.transform.gameObject;
                //if the object is hittable
                if (MatchesAnyTag(hitObject, tags))
                {
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    float angle = Vector3.Angle(hit.transform.position - transform.position, transform.forward);
                    if (angle <= damageAngle / 2 && distance <= attackRange)//If the object is within the attack range and within the damage angle.
                    {
                        ApplyDamage(hitObject, damage);
                    }
                }
            }
            attackCompleteCallback(true);//Callback to the AI Controller when the attack is complete. Usually to reset isAttacking variable to false;
        }

        public void DealDamage2D(float damage, float attackRange, float damageAngle, List<string> tags)
        {
            int hitCount = Physics2D.CircleCast(new Vector2(transform.position.x, transform.position.y), attackRange, transform.up, Damage2DFilter, DamageHits2D, attackRange);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount == DamageHits2D.Length)
            {
                Debug.LogWarning("Candice 2D damage hit buffer saturated. Increase MaxDamageHits.");
            }
#endif
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = DamageHits2D[i];
                if (hit.transform == null)
                {
                    continue;
                }

                GameObject hitObject = hit.transform.gameObject;
                //if the object is hittable
                if (MatchesAnyTag(hitObject, tags))
                {
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    if (distance <= attackRange)//If the object is within the attack range and within the damage angle.
                    {
                        ApplyDamage(hitObject, damage);
                    }
                }
            }
            attackCompleteCallback(true);
        }
        public IEnumerator DealTimedDamage(float time, float damage, float attackRange, float damageAngle, List<string> tags)
        {
            yield return new WaitForSecondsRealtime(time);
            DealDamage(damage, attackRange, damageAngle, tags);
        }
        public IEnumerator DealTimedDamage2D(float time, float damage, float attackRange, float damageAngle, List<string> tags)
        {
            yield return new WaitForSecondsRealtime(time);
            DealDamage2D(damage, attackRange, damageAngle, tags);
        }
        public void PrepareProjectilePool(GameObject attackProjectile, Transform spawnPosition)
        {
            if (attackProjectile == null || spawnPosition == null || projectilePoolPrefab != null)
            {
                return;
            }

            projectilePoolPrefab = attackProjectile;
            for (int i = 0; i < projectilePool.Length; i++)
            {
                if (projectilePool[i] == null)
                {
                    GameObject projectile = UnityEngine.Object.Instantiate(attackProjectile, spawnPosition.position, Quaternion.identity);
                    projectile.SetActive(false);
                    projectilePool[i] = projectile;
                }
            }
        }

        public void FireProjectile(GameObject attackTarget, GameObject attackProjectile,Transform spawnPosition)
        {
            //
            //Method Name : void AttackRange()
            //Purpose     : This method is called by the attack animation event. Deals the required damage to all targets in range..
            //Re-use      : none
            //Input       : none
            //Output      : none
            //
            if (attackTarget == null || attackProjectile == null || spawnPosition == null)
            {
                attackCompleteCallback(true);
                return;
            }

            if (!ReferenceEquals(projectilePoolPrefab, attackProjectile))
            {
                attackCompleteCallback(true);
                return;
            }

            GameObject projectile = GetInactiveProjectile();
            if (projectile != null)
            {
                projectile.transform.position = spawnPosition.position;
                projectile.transform.rotation = spawnPosition.rotation;
                projectile.SetActive(true);
                if (projectile.TryGetComponent(out CandiceProjectile ai))
                {
                    ai.Fire(attackTarget);
                }
            }
            attackCompleteCallback(true);
        }

        public float ReceiveDamage(float damage,float currentHP)
        {
            //
            //Method Name : void CandiceReceiveDamage(float damage)
            //Purpose     : This method receives damage from various sources and applies it to the character.
            //Re-use      : none
            //Input       : float damage
            //Output      : none
            //
            currentHP -= damage;
            if (currentHP <= 0)
            {
                currentHP = 0;
                //CharacterDead() method should be called after the death animation has finished playing using an Animation Event. 
                //Alternatively, you can implement your own logic here to suit your needs.
            }
            if (EnableDebug)
                Utils.Utils.LogDamageReceived(ModuleName, damage, currentHP);

            return currentHP;
            //if (CandiceConfig.enableDebug)
            //Debug.Log("Hit with: " + damage + " damage. New Health: " + HitPoints);
        }

        private static bool MatchesAnyTag(GameObject hitObject, List<string> tags)
        {
            if (hitObject == null || tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (!string.IsNullOrEmpty(tag) && hitObject.CompareTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject GetInactiveProjectile()
        {
            for (int i = 0; i < projectilePool.Length; i++)
            {
                GameObject projectile = projectilePool[i];
                if (projectile != null && !projectile.activeSelf)
                {
                    return projectile;
                }
            }

            return null;
        }

        private static void ApplyDamage(GameObject hitObject, float damage)
        {
            if (hitObject == null)
            {
                return;
            }

            if (hitObject.TryGetComponent(out CandiceAIController aiController))
            {
                aiController.CandiceReceiveDamage(damage);
                return;
            }

            if (hitObject.TryGetComponent(out global::BasicPlayerController playerController))
            {
                playerController.CandiceReceiveDamage(damage);
            }
        }

        


    }
}
