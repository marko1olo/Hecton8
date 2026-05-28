using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    public float TimeTillDeath = 1f;
    private float deactivateAt;
    private bool deactivationScheduled;

    // Start is called before the first frame update
    void Start()
    {
        deactivateAt = Time.time + Mathf.Max(0f, TimeTillDeath);
        deactivationScheduled = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (deactivationScheduled && Time.time >= deactivateAt)
        {
            deactivationScheduled = false;
            gameObject.SetActive(false);
        }
    }
}
