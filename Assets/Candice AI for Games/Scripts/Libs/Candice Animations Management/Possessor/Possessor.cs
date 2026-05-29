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
    private CandiceProjectile[] possessableProjectiles;
    private Transform[] possessableCameraParents;
    private Transform[] possessableVsfxRoots;
    

    // Start is called before the first frame update
    void Start()
    {
        if (animationManager == null) {
            animationManager = gameObject.AddComponent(typeof(CandiceAnimationManager)) as CandiceAnimationManager;
        }
        EnsurePossessableCapacity();
        vsfx = transform.Find("VSFX");
        RefreshPossessableCache();
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
                CandiceProjectile projectile;
                if (CandiceProjectile.TryGetActiveProjectileComponent(out projectile))
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
                        Transform camParent = possessableCameraParents[i];
                        if (camParent != null)
                        {
                            if (!camParent.gameObject.activeSelf)
                            {
                                camParent.gameObject.SetActive(true);

                            }
                        }
                        Transform activeVsfx = possessableVsfxRoots[i] == null ? vsfx : possessableVsfxRoots[i];
                        if (activeVsfx != null)
                        {
                            for (int fxIndex = 0; fxIndex < activeVsfx.childCount; fxIndex++)
                            {
                                Transform fx = activeVsfx.GetChild(fxIndex);
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
                            CandiceProjectile projectileComponent = possessableProjectiles[i];
                            if (projectileComponent != null)
                            {
                                projectileComponent.ScheduleDeactivate(ProjectilePossessionTimer);
                            }
                        }
                    }
                }
                else {
                    //ensure any projectile possessables have been discarded
                    ClearPossessableSlot(i);
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
        }

        if (possessableProjectiles == null || possessableProjectiles.Length != MaxPossessableObjects)
        {
            // COLD ALLOC: CandiceProjectile[3] - cached possession components - owner: Possessor
            possessableProjectiles = new CandiceProjectile[MaxPossessableObjects];
        }

        if (possessableCameraParents == null || possessableCameraParents.Length != MaxPossessableObjects)
        {
            // COLD ALLOC: Transform[3] - cached possession camera parents - owner: Possessor
            possessableCameraParents = new Transform[MaxPossessableObjects];
        }

        if (possessableVsfxRoots == null || possessableVsfxRoots.Length != MaxPossessableObjects)
        {
            // COLD ALLOC: Transform[3] - cached possession VFX roots - owner: Possessor
            possessableVsfxRoots = new Transform[MaxPossessableObjects];
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

    public void RefreshPossessableCache()
    {
        EnsurePossessableCapacity();

        for (int i = 0; i < PossessableObjects.Length; i++)
        {
            GameObject possessable = PossessableObjects[i];
            CachePossessableSlot(i, possessable);
        }
    }

    private void TryAddPossessable(CandiceProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        GameObject projectileObject = projectile.gameObject;
        for (int i = 0; i < PossessableObjects.Length; i++)
        {
            if (ReferenceEquals(PossessableObjects[i], projectileObject))
            {
                return;
            }
        }

        for (int i = 0; i < PossessableObjects.Length; i++)
        {
            if (PossessableObjects[i] == null)
            {
                PossessableObjects[i] = projectileObject;
                possessableProjectiles[i] = projectile;
                possessableCameraParents[i] = projectile.CachedCameraParent;
                possessableVsfxRoots[i] = projectile.CachedVsfxRoot;
                return;
            }
        }
    }

    private void CachePossessableSlot(int slot, GameObject possessable)
    {
        ClearPossessableCache(slot);

        if (possessable == null)
        {
            return;
        }

        Transform possessableTransform = possessable.transform;
        possessableCameraParents[slot] = possessableTransform.Find("CameraParent");
        possessableVsfxRoots[slot] = possessableTransform.Find("VSFX");

        if (possessable.TryGetComponent(out CandiceProjectile projectile))
        {
            possessableProjectiles[slot] = projectile;
            if (possessableCameraParents[slot] == null)
            {
                possessableCameraParents[slot] = projectile.CachedCameraParent;
            }
            if (possessableVsfxRoots[slot] == null)
            {
                possessableVsfxRoots[slot] = projectile.CachedVsfxRoot;
            }
        }
    }

    private void ClearPossessableSlot(int slot)
    {
        PossessableObjects[slot] = null;
        ClearPossessableCache(slot);
    }

    private void ClearPossessableCache(int slot)
    {
        possessableProjectiles[slot] = null;
        possessableCameraParents[slot] = null;
        possessableVsfxRoots[slot] = null;
    }
}
