//System
using System;
using System.Collections;
using System.Collections.Generic;
//Unity
using UnityEngine;
using UnityEngine.UI;
//Candice AI
using CandiceAIforGames.AI;

public class Possessor : MonoBehaviour
{

    public GameObject[] PossessableObjects; //must have an instance of CandiceAIController script attached
    public bool PlayerCanPossessProjectile = false;
    public float ProjectilePossessionTimer = 10f;
    private GameObject[] projectiles;
    [HideInInspector]
    public CandiceAnimationManager animationManager;
    private Transform vsfx;
    private const string ProjectileTag = "Projectile";
    private const string CandiceVsfxTag = "CandiceVSFX";
    

    // Start is called before the first frame update
    void Start()
    {
        if (animationManager == null) {
            animationManager = gameObject.AddComponent(typeof(CandiceAnimationManager)) as CandiceAnimationManager;
        }
        vsfx = transform.Find("VSFX");
        if (vsfx != null)
        {
            for (int i = 0; i < vsfx.childCount; i++)
            {
                Transform fx = vsfx.GetChild(i);
                if (fx.CompareTag(CandiceVsfxTag)) {
                    fx.gameObject.SetActive(false);
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (animationManager.EvaluateInput("Possess", false, true, false)) {
            //if can possess nearest projectile
            if (PlayerCanPossessProjectile) {
                GameObject projectile = GameObject.FindWithTag(ProjectileTag);
                if (projectile != null && PossessableObjects.Length < 3)
                {
                    Array.Resize<GameObject>(ref PossessableObjects, PossessableObjects.Length + 1);
                    PossessableObjects[PossessableObjects.Length - 1] = projectile;
                }
            }
            //now cycle through all assigned possessed
            for (int i = 0; i < PossessableObjects.Length; i++) {
                GameObject possessed = PossessableObjects[i];

                if (possessed != null)
                {
                    if (!possessed.activeSelf)
                    {
                        //in case you are not active, set yourself active                        
                        possessed.SetActive(true);
                        Transform camParent = possessed.transform.Find("CameraParent");                        
                        if (camParent != null)
                        {
                            if (!camParent.gameObject.activeSelf)
                            {
                                camParent.gameObject.SetActive(true);

                            }
                        }
                        if (vsfx == null)
                        {
                            vsfx = possessed.transform.Find("VSFX");
                        }
                        if (vsfx != null)
                        {
                            for (int fxIndex = 0; fxIndex < vsfx.childCount; fxIndex++)
                            {
                                Transform fx = vsfx.GetChild(fxIndex);
                                if (fx.CompareTag(CandiceVsfxTag))
                                {
                                    GameObject thisfx = Instantiate(fx.gameObject, possessed.transform.position, Quaternion.identity);
                                    thisfx.SetActive(true);
                                    Destroy(thisfx, 5f);
                                }
                            }
                        }

                    }
                    else
                    {
                        //if you are already active
                        possessed.SetActive(false);
                        if (possessed.CompareTag(ProjectileTag))
                        {
                            Destroy(possessed, ProjectilePossessionTimer);
                        }
                    }
                }
                else {
                    //ensure any projectile possessables have been discarded
                    Array.Resize<GameObject>(ref PossessableObjects, PossessableObjects.Length - 1);
                }


            }
        }
    }
}
