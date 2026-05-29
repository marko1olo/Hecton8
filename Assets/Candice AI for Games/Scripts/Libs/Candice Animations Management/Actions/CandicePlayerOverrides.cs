//System
using System;
using System.Collections;
using System.Collections.Generic;
//Unity
using UnityEngine;
using UnityEngine.UI;
//Candice AI
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI
{

    //class to handle player animation overrides that interact with CandiceAI system    
    public class CandicePlayerOverrides
    {
        private GameObject attackTarget;
        private Camera attackCamera;
        //original scale of the root parent gameObject should always be 1
        public Vector3 originalScale = new Vector3(1f, 1f, 1f);
        public float boostedScale = 2f;
        public static float bufferMousePositionz = 10f;
        //projectile speeds should be in the 00s and 000s and should be maintained in these ranges as these values are optimal values tested against various projectile types
        public float projectileMoveSpeed = 3000f;
        public float projectileBoostedMoveSpeed = 5000f;

        public void PrepareAttackTarget(Transform owner)
        {
            if (attackTarget == null)
            {
                // COLD ALLOC: persistent attack target created during Candice animation startup, reused by player ranged overrides.
                attackTarget = new GameObject("CandicePlayerAttackTarget");
                attackTarget.hideFlags = HideFlags.DontSave;
            }

            if (owner != null && attackTarget.transform.parent != owner)
            {
                attackTarget.transform.SetParent(owner, false);
            }

            if (attackCamera == null)
            {
                attackCamera = owner != null ? owner.GetComponentInChildren<Camera>() : null;
            }

            attackTarget.SetActive(false);
        }

        //PATRAN_CANDICEAI IS SHORT FOR (PLAYER ATTACK RANGED USING CANDICEAI)
        //this uses the candice ai projectile to performed a ranged attack. It requires that CandiceAIController script be also attached to Player.
        public void PATRAN_CANDICEAI(CandiceAIController player)
        {
            if (player == null)
            {
                return;
            }

            //get mouse position from input
            Vector3 mousePos = Input.mousePosition;
            GameObject currentAttackTarget = GetAttackTarget();
            if (currentAttackTarget == null)
            {
                return;
            }

            Camera currentCamera = attackCamera;
            if (currentCamera != null)
            {
                //buffer mouse position z
                mousePos.z = bufferMousePositionz;
                //create new attack target from reticule position
                currentAttackTarget.transform.position = currentCamera.ScreenToWorldPoint(mousePos);
                currentAttackTarget.SetActive(true);
            }

            if (player.projectile != null)
            {
                //adjust projectile scale (scale is always 1 on normal projectile)
                player.projectile.gameObject.transform.localScale = OriginalScale(player);

                //adjust projectile move speed
                if (player.projectile.TryGetComponent(out CandiceProjectile projectile))
                {
                    projectile.moveSpeed = projectileMoveSpeed;
                }

                //assign newly created attack target to player
                player.AttackTarget = currentAttackTarget;

                //perform ranged attack using CandiceAIController (player must have a CandiceAIController script attached wtih a projectile prefab attached at a minimum for this to work)
                player.AttackRanged();
            }
        }

        //PATRAN_BOOSTED_CANDICEAI IS SHORT FOR (PLAYER BOOSTED ATTACK RANGED USING CANDICEAI)
        //this uses the candice ai projectile to performed a ranged attack. It requires that CandiceAIController script be also attached to Player.
        public void PATRAN_BOOSTED_CANDICEAI(CandiceAIController player)
        {
            if (player == null)
            {
                return;
            }

            //get mouse position from input
            Vector3 mousePos = Input.mousePosition;
            GameObject currentAttackTarget = GetAttackTarget();
            if (currentAttackTarget == null)
            {
                return;
            }

            Camera currentCamera = attackCamera;
            if (currentCamera != null) {
                //buffer mouse position z
                mousePos.z = bufferMousePositionz;
                //create new attack target from reticule position
                currentAttackTarget.transform.position = currentCamera.ScreenToWorldPoint(mousePos);
                currentAttackTarget.SetActive(true);
                
            }

            if (player.projectile != null) {
                //adjust projectile scale
                player.projectile.gameObject.transform.localScale = new Vector3(boostedScale, boostedScale, boostedScale);

                //adjust projectile move speed
                if (player.projectile.TryGetComponent(out CandiceProjectile projectile))
                {
                    projectile.moveSpeed = projectileBoostedMoveSpeed;
                }

                //assign newly created attack target to player
                player.AttackTarget = currentAttackTarget;

                //perform ranged attack using CandiceAIController (player must have a CandiceAIController script attached wtih a projectile prefab attached at a minimum for this to work)
                player.AttackRanged();
            }
        }

        //returns original projectile scale
        public Vector3 OriginalScale(CandiceAIController original) {
            if (original != null && original.projectile != null)
            {
                originalScale = original.projectile.gameObject.transform.localScale;
            }
            return originalScale;
        }

        private GameObject GetAttackTarget()
        {
            return attackTarget;
        }
    }
}
