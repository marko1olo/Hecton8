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
    public class CandiceUI : CandiceMiddleware
    {
        //UI
        public GameObject HealthBar;
        public GameObject thisAgent;

        public void UpdateHealthUI(string dependencyName, float attackDamage)
        {            

            //get health bar dependecy middleware
            //Type dependency = GetDependency(dependencyName);
            if (HealthBar == null || thisAgent == null)
            {
                return;
            }

            if (HealthBar.TryGetComponent(out CandiceHealthBar hlth))
            {
                //get health indicator (progress bar) value
                if (thisAgent.TryGetComponent(out CandiceAIController controller) && controller.hitPoints > 0f)
                {
                    hlth.m_FillAmount -= (hlth.m_FillAmount / controller.hitPoints * attackDamage);
                }
            }
            else {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("Candice health bar prefab is missing.");
#endif
            }


        }

        public void ResetBar(GameObject healthBar, float hitPoints)
        {
            //get health indicator (progress bar) value
            var hlth = healthBar.GetComponent<CandiceHealthBar>();
            if (hlth != null)
            {
                hlth.m_FillAmount = hitPoints / hitPoints;
            }
        }

    }
}
