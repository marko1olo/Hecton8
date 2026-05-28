//Unity
using UnityEngine;
//Candice AI
using CandiceAIforGames.AI;

public class Possessor : MonoBehaviour
{
    private const int MaxPossessableObjects = 3;

    public GameObject[] PossessableObjects; //must have an instance of CandiceAIController script attached
    public bool PlayerCanPossessProjectile = false;
    public float ProjectilePossessionTimer = 10f;
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
        EnsurePossessableCapacity();
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
                GameObject projectile;
                if (CandiceProjectile.TryGetActiveProjectile(out projectile))
                {
                    TryAddPossessable(projectile);
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
                                    fx.position = possessed.transform.position;
                                    fx.gameObject.SetActive(true);
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
                            if (possessed.TryGetComponent(out CandiceProjectile projectileComponent))
                            {
                                projectileComponent.ScheduleDeactivate(ProjectilePossessionTimer);
                            }
                        }
                    }
                }
                else {
                    //ensure any projectile possessables have been discarded
                    PossessableObjects[i] = null;
                }


            }
        }
    }

    private void EnsurePossessableCapacity()
    {
        if (PossessableObjects == null)
        {
            // COLD ALLOC: GameObject[3] - fixed possession slots - owner: Possessor
            PossessableObjects = new GameObject[MaxPossessableObjects];
            return;
        }

        if (PossessableObjects.Length == MaxPossessableObjects)
        {
            return;
        }

        // COLD ALLOC: GameObject[3] - normalize vendor possession slots - owner: Possessor
        GameObject[] normalized = new GameObject[MaxPossessableObjects];
        int copyCount = Mathf.Min(PossessableObjects.Length, normalized.Length);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = PossessableObjects[i];
        }

        PossessableObjects = normalized;
    }

    private void TryAddPossessable(GameObject projectile)
    {
        for (int i = 0; i < PossessableObjects.Length; i++)
        {
            if (ReferenceEquals(PossessableObjects[i], projectile))
            {
                return;
            }
        }

        for (int i = 0; i < PossessableObjects.Length; i++)
        {
            if (PossessableObjects[i] == null)
            {
                PossessableObjects[i] = projectile;
                return;
            }
        }
    }
}
