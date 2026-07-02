using System;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI
{

    #region ENUMS
    public enum DropType
    {
        Weapon,
        Tool,
        Resource,
        Health,
        Invincibility,
        Speed,
        Damage
    }
    #endregion

    public class CandiceDrop : MonoBehaviour
    {
        private enum PendingRevertKind
        {
            None,
            Speed,
            PlayerHealth,
            PlayerSpeed
        }

        public DropType dropType = new DropType();
        public DropType dType { get => dropType; set => dropType = value; }
        public float boostValue = 0.0f;
        public float boostDuration = 10f;

        //
        [HideInInspector]
        public GameObject agentToBoost;
        private float agentHealth;
        private float agentSpeed;
        private CandiceAIController agentController;
        private CandiceAIPlayerController playerController;
        private PendingRevertKind pendingRevert;
        private float revertAt;
        private bool deactivateScheduled;
        private float deactivateAt;
        private bool consumed;

        //UI
        private CandiceUI candiceUI;

        //VSFX
        private Transform VSFX;
        private Transform[] vfxSlots = Array.Empty<Transform>();
        private float[] vfxDisableAt = Array.Empty<float>();
        private Renderer[] dropRenderers = Array.Empty<Renderer>();
        private Collider[] dropColliders = Array.Empty<Collider>();


        private void Awake()
        {
            dropRenderers = GetComponentsInChildren<Renderer>(true);
            dropColliders = GetComponentsInChildren<Collider>(true);
        }

        void Start() {
            CacheDropComponents();
            candiceUI = new CandiceUI();
        }

        private void OnEnable()
        {
            consumed = false;
            pendingRevert = PendingRevertKind.None;
            deactivateScheduled = false;
            SetDropCollisionEnabled(true);
            SetDropVisualEnabled(true);
        }

        // Start is called before the first frame update
        public void AssessDrop()
        {
            if (agentToBoost == null)
            {
                return;
            }

            agentController = agentToBoost.GetComponent<CandiceAIController>();
            playerController = agentToBoost.GetComponent<CandiceAIPlayerController>();
            if (agentController == null || candiceUI == null)
            {
                return;
            }

            candiceUI.thisAgent = agentToBoost;
            if (candiceUI.HealthBar == null)
            {
                candiceUI.HealthBar = agentController.HealthBar;
            }

            agentHealth = agentController.hitPoints;
            agentSpeed = agentController.moveSpeed;
            switch (dType)
            {
                case DropType.Weapon:
                    //no Weapon inventory support yet
                    break;
                case DropType.Tool:
                    //no Tool inventory support yet
                    break;
                case DropType.Resource:
                    //no Resource inventory support yet
                    break;
                case DropType.Health:
                    //boost is permanent and is agent type independent (which means your enemies can snatch up the drops), it's only fair.
                    agentController.hitPoints += boostValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("Candice drop health applied.");
#endif
                    candiceUI.UpdateHealthUI("ClassicProgressBar", -boostValue);
                    break;
                case DropType.Invincibility:
                    //invincibility is timed and is only applied to the player agent (we don't want enemy or npc agents to get this boost). Here, there is no fairness.
                    if (agentToBoost.CompareTag("Player"))
                    {
                        agentController.hitPoints += boostValue;
                        candiceUI.UpdateHealthUI("ClassicProgressBar", -boostValue);
                        ScheduleRevert(PendingRevertKind.PlayerHealth, boostDuration);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("Candice drop invincibility applied.");
#endif
                    }
                    else {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("Candice drop invincibility ignored for non-player agent.");
#endif
                    }
                    break;
                case DropType.Speed:
                    if (agentToBoost.CompareTag("Player"))
                    {
                        if (playerController != null)
                        {
                            agentSpeed = playerController.speed;
                            playerController.speed += boostValue;
                            ScheduleRevert(PendingRevertKind.PlayerSpeed, boostDuration);
                        }
                    }
                    else {
                        agentController.moveSpeed += boostValue;
                        ScheduleRevert(PendingRevertKind.Speed, boostDuration);
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("Candice drop speed applied.");
#endif
                    break;
                case DropType.Damage:

                    break;
            }

            ActivateVfx(agentToBoost.transform.position, 5f);

            ConsumeDrop();

        }

        // Update is called once per frame
        void Update()
        {
            ProcessVfxTimers();
            ProcessRevert();
            ProcessDeactivate();
        }

        void OnTriggerEnter(Collider col) {
            agentToBoost = col.gameObject;
            if (!consumed && agentToBoost != null && !agentToBoost.CompareTag("Projectile"))
            {
                AssessDrop();
            }
        }

        private void CacheDropComponents()
        {
            VSFX = transform.Find("VSFX");
            if (VSFX != null)
            {
                int childCount = VSFX.childCount;
                if (vfxSlots.Length != childCount)
                {
                    // COLD ALLOC: Transform[childCount] - Candice drop VFX slot table built during startup.
                    vfxSlots = new Transform[childCount];
                    // COLD ALLOC: float[childCount] - Candice drop VFX disable schedule built during startup.
                    vfxDisableAt = new float[childCount];
                }

                for (int i = 0; i < childCount; i++)
                {
                    Transform fx = VSFX.GetChild(i);
                    vfxSlots[i] = fx;
                    vfxDisableAt[i] = 0f;
                    if (fx != null)
                    {
                        fx.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void ActivateVfx(Vector3 position, float activeSeconds)
        {
            for (int i = 0; i < vfxSlots.Length; i++)
            {
                Transform fx = vfxSlots[i];
                if (fx != null)
                {
                    fx.position = position;
                    fx.gameObject.SetActive(true);
                    vfxDisableAt[i] = Time.time + Mathf.Max(0f, activeSeconds);
                }
            }
        }

        private void ProcessVfxTimers()
        {
            float now = Time.time;
            for (int i = 0; i < vfxSlots.Length; i++)
            {
                Transform fx = vfxSlots[i];
                if (fx != null && vfxDisableAt[i] > 0f && now >= vfxDisableAt[i])
                {
                    fx.gameObject.SetActive(false);
                    vfxDisableAt[i] = 0f;
                }
            }
        }

        private void ScheduleRevert(PendingRevertKind kind, float delay)
        {
            pendingRevert = kind;
            revertAt = Time.time + Mathf.Max(0f, delay);
        }

        private void ProcessRevert()
        {
            if (pendingRevert == PendingRevertKind.None || Time.time < revertAt)
            {
                return;
            }

            PendingRevertKind kind = pendingRevert;
            pendingRevert = PendingRevertKind.None;
            if (agentController == null)
            {
                return;
            }

            if (kind == PendingRevertKind.Speed)
            {
                agentController.moveSpeed = agentSpeed;
            }
            else if (kind == PendingRevertKind.PlayerHealth)
            {
                agentController.hitPoints = agentHealth;
                if (candiceUI != null)
                {
                    candiceUI.UpdateHealthUI("ClassicProgressBar", agentHealth);
                }
            }
            else if (kind == PendingRevertKind.PlayerSpeed && playerController != null)
            {
                playerController.speed = agentSpeed;
            }
        }

        private void ConsumeDrop()
        {
            consumed = true;
            SetDropCollisionEnabled(false);
            SetDropVisualEnabled(false);
            float deactivateDelay = pendingRevert == PendingRevertKind.None ? 5f : boostDuration + 0.1f;
            deactivateAt = Time.time + Mathf.Max(0f, deactivateDelay);
            deactivateScheduled = true;
        }

        private void ProcessDeactivate()
        {
            if (deactivateScheduled && Time.time >= deactivateAt)
            {
                deactivateScheduled = false;
                gameObject.SetActive(false);
            }
        }

        private void SetDropCollisionEnabled(bool isEnabled)
        {
            for (int i = 0; i < dropColliders.Length; i++)
            {
                Collider dropCollider = dropColliders[i];
                if (dropCollider != null)
                {
                    dropCollider.enabled = isEnabled;
                }
            }
        }

        private void SetDropVisualEnabled(bool isEnabled)
        {
            for (int i = 0; i < dropRenderers.Length; i++)
            {
                Renderer dropRenderer = dropRenderers[i];
                if (dropRenderer != null && (VSFX == null || !dropRenderer.transform.IsChildOf(VSFX)))
                {
                    dropRenderer.enabled = isEnabled;
                }
            }
        }

    }
}
