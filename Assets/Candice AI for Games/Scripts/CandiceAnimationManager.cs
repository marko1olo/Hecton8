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
    public class CandiceAnimationManager : MonoBehaviour
    {

        //Some enemy type control, basic, advanced, titanic, boss        
        public bool isATitan = false;

        //Animations Module
        [HideInInspector]
        public CandiceModuleAnimations CandiceModuleAnimations;
        private CandiceStandardActions standardActions;
        private CandiceHumanoidMelee candiceHumanoidMelee;
        private CandicePlayerOverrides candicePlayerOverrides;
        [HideInInspector]
        public static CandiceCamera candiceCamera;
        [HideInInspector]
        public static GameObject mainCam;
        [HideInInspector]
        public static GameObject KillCam;
        public bool attachKillCam = false;
        //private bool killShot = false;
        public ScriptableObject shakeData;
        [HideInInspector]
        public CandiceUI candiceUI;

        //Candice Agent
        [HideInInspector]
        public Transform thisAgent;
        public float atkDamage;
        private bool isAttack = false;
        private bool isWalking = false;

        //Template AnimationController (we provide various out of the box animation controllers for humanoid and standard rigs, so you don't necessarily need to provide your own). 
        //This could be on the agent gameObject, a child gameObject of the agent or on any gameObject in the scene.
        public Animator TemplateAnimator;
        private Transform CandiceVSFX;
        private Rigidbody thisRigidbody;
        private Transform[] _vfxSlots = Array.Empty<Transform>();
        private float[] _vfxDisableAt = Array.Empty<float>();
        private CandiceAIController _aiController;
        private CandiceAIPlayerController _playerController;
        private bool _pendingGenericCombo;
        private int _pendingGenericComboAttack;
        private float _pendingGenericComboAt;

        //Animation Speed Control
        //When set on character animSpeed overides globalSpeed, but when gloablSpeed > 1f then it is possible to slow down time, or generate other complex time manipulation animation scenarios.
        public float animSpeed = 1f;        
        public static float globalSpeed = 1f;
        private float thisGlobalSpeed = 1f;
        private float thisCharacterMoveSpeed;
        private float thisCharacterJumpSpeed;

        //Inventory manager
        private CandiceInventoryManager candiceInventoryManager;
        [HideInInspector]
        public GameObject inventoryDrop;

        //Healthbar if attached to UI layer (usually player healthbar)
        public GameObject HealthBar;
        public bool hit = false;
        public bool dead = false;

        //Combo Control
        [HideInInspector]
        private float timeSinceAttack = 0.0f;
        private int currentAttackInChainedCombo = 0;
        public float thisComboDamagePerHit = 0f;

        void Start() {
            thisRigidbody = GetComponent<Rigidbody>();
            _aiController = GetComponent<CandiceAIController>();
            _playerController = GetComponent<CandiceAIPlayerController>();
            //When called in Start it allows for you to attach this script to an agent independently from the CandiceAIController
            //Some scenarios call for control this way. Scenarios such as cataclysms, massive destruction on cosmic scales etc.
            InitializeAnimations();
        }

        // Start is called before the first frame update
        public void InitializeAnimations()
        {

            /// 
            /// CANDICE ANIMATIONS
            /// 

            //Create a new instance of the Candice Animations module if not passed via public reference instance from CandiceAIController.
            if (CandiceModuleAnimations == null) {
                CandiceModuleAnimations = new CandiceModuleAnimations();
                //get standard actions
                standardActions = new CandiceStandardActions();
                //get melee actions
                candiceHumanoidMelee = new CandiceHumanoidMelee();
                //get player overrides
                candicePlayerOverrides = new CandicePlayerOverrides();
                
            }
            if (standardActions == null)
            {
                // COLD ALLOC: animation action bridge constructed during Candice startup only.
                standardActions = new CandiceStandardActions();
            }
            if (candiceHumanoidMelee == null)
            {
                // COLD ALLOC: animation action bridge constructed during Candice startup only.
                candiceHumanoidMelee = new CandiceHumanoidMelee();
            }
            if (candicePlayerOverrides == null)
            {
                // COLD ALLOC: player override bridge constructed during Candice startup only.
                candicePlayerOverrides = new CandicePlayerOverrides();
            }

            //Get Candice AI Agent being animated
            //Since CandiceAIController now inherits from this class, we can instantiate the agent transform here first.
            thisAgent = transform;
            if (_aiController == null)
            {
                _aiController = thisAgent.GetComponent<CandiceAIController>();
            }
            if (_playerController == null)
            {
                _playerController = thisAgent.GetComponent<CandiceAIPlayerController>();
            }
            CandiceVSFX = thisAgent.Find("VSFX");
            CacheVfxSlots();

            //Get this Animator in case one is not provided as a template, has to be attached on this agent.
            //All standard CandiceAI prefabs contain an animator and animation controller.
            if (TemplateAnimator == null)
            {
                TemplateAnimator = thisAgent.GetComponent<Animator>();
            }

            //now assign animator
            CandiceModuleAnimations.TemplateAnimator = TemplateAnimator;
            standardActions.TemplateAnimator = TemplateAnimator;
            candiceHumanoidMelee.TemplateAnimator = TemplateAnimator;

            //animation speed control
            if (globalSpeed > 1f)
            {
                if (TemplateAnimator != null) {
                    SetSpeed(TemplateAnimator, "animSpeed", globalSpeed);
                }                
            }
            else
            {
                if (TemplateAnimator != null) {
                    SetSpeed(TemplateAnimator, "animSpeed", animSpeed);
                }
            }

            //CANDICE CAMERA
            if (candiceCamera == null)
            {   
                candiceCamera = new CandiceCamera();
            }
            //Set main camera      
            candiceCamera.MainCamera = mainCam;

            //Set shake data for CandiceAI Tag objects
            if (thisAgent.gameObject.CompareTag("Player") || thisAgent.gameObject.CompareTag("CandiceAgent"))
            {
                if (_aiController != null)
                {
                    shakeData = _aiController.CameraShakeData;
                }
                //add the shake data to candiceCamera
                candiceCamera.ShakeData = shakeData;
            }
            else {
                //add the shake data to candiceCamera
                candiceCamera.ShakeData = shakeData;
            }
            
            //add a killcam if checked
            if (attachKillCam) {
                //kill cam if any
                if (KillCam != null)
                {
                    KillCam.SetActive(false);
                    candiceCamera.KillCameraParent = KillCam;
                    if (KillCam.TryGetComponent(out FollowPlayer followPlayer))
                    {
                        candiceCamera.KillCameraFollow = followPlayer;
                    }
                }
            }

            //CANDICE INVENTORY
            //drop support
            if (candiceInventoryManager == null)
            {                
                candiceInventoryManager = gameObject.AddComponent(typeof(CandiceInventoryManager)) as CandiceInventoryManager;
            }
            if (candiceInventoryManager != null && inventoryDrop != null)
            {
                candiceInventoryManager.PrepareDropPool(inventoryDrop);
            }

            //CANDICE UI
            if (candiceUI == null) {
                candiceUI = new CandiceUI();
            }
            //set the agent on the UI element
            candiceUI.thisAgent = thisAgent.gameObject;
            //assign Healthbar in candiceUI
            candiceUI.HealthBar = HealthBar;
            candicePlayerOverrides.PrepareAttackTarget(thisAgent);

            //grab the player controller speed variables
            if (thisAgent.gameObject.CompareTag("Player")) {
                thisGlobalSpeed = globalSpeed;
                //we want to inference the player controller speeds with the animation speeds for advanced time effects
                //we can also control the player controller speeds this way for special animations like: shiftJump (also called blink or phaseShift), groundSmash and other high-level animations requiring special timing.
                if(_playerController != null)
                {
                    thisCharacterMoveSpeed = _playerController.speed;
                    thisCharacterJumpSpeed = _playerController.jumpSpeed;
                }
                
            }

        }

        //Update is called once per frame
        public void Animate()
        {
            ProcessScheduledVfx();
            if (TemplateAnimator == null || TemplateAnimator.runtimeAnimatorController == null)
            {
                return;
            }
            ProcessGenericCombo();
            //player animations
            if (gameObject.CompareTag("Player"))
            {
                if (_playerController != null)
                {
                    PlayerInput();
                }
            }
            //all other agents
            else {
                AgentInput();
            }
        }

        //Collision Control
        public bool IveHitSomething(Collision col)
        {
            //if colliding with agent projectiles
            if (col.gameObject.CompareTag("Projectile"))
            {
                //ive been hit by a candice projectile
                hit = true;
                //we want some control over the attack damage of the projectile
                atkDamage = col.gameObject.transform.GetComponent<CandiceProjectile>().attackDamage;
            }
            else if (col.gameObject.CompareTag("Player"))
            {
                //ive been hit by a candice projectile
                hit = true;
            }
            //return hit
            return hit;

        }

        //Support for PC & Standard Inputs defined in Edit > Project Settings > Input Manager currently        
        public bool StandardInputCall(string input) {
            if (Input.GetButton(input))
            {
                return true;
            }            
            return false;
        }

        //Generic Evaluate Input with multi input support //also supports while pressed, on key press down, and on keypress up
        public bool EvaluateInput(string input, bool isKey, bool down, bool up)
        {

            bool returnValue = false;

            //if key then just give it a key
            if (isKey)
            {
                if (down)
                {
                    returnValue = Input.GetKeyDown(input);
                }
                else if (up)
                {
                    returnValue = Input.GetKeyUp(input);
                }
                else
                {
                    returnValue = Input.GetKey(input);
                }

            }
            //otherwise uses unity input system
            else
            {
                if (down)
                {
                    returnValue = Input.GetButtonDown(input);
                }
                else if (up)
                {
                    returnValue = Input.GetButtonUp(input);
                }
                else
                {
                    returnValue = Input.GetButton(input);
                }
            }

            return returnValue;


        }

        //Support for new Unity Input System and Manager upcoming
        public bool InputManager2Call(string input) {
            return false;
        }

        //Handles all player input
        public void PlayerInput() {

            //player input animSpeed overrides global
            //animation speed control
            if (animSpeed > 1f)
            {
                //use speed function on thisAgent TemplateAnimator or set directly on globalSpeed when working with advanced timed effects.
                SetSpeed(TemplateAnimator, "animSpeed", animSpeed);
            }

            //temporary global movement speed for flash step
            float templGlobalSpeed = 3f;

            //get movement axes
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            //Primary fire                        
            //we have this set currently for light hand combo 1                        
            if (EvaluateInput("Fire1", false, true, false))            {
                timeSinceAttack += Time.deltaTime;
                currentAttackInChainedCombo++;
                if (timeSinceAttack > 0.1f)
                {
                    timeSinceAttack = 0.0f;
                    currentAttackInChainedCombo = 0;
                }
                isAttack = true;
                ScheduleGenericCombo(currentAttackInChainedCombo, 0.33f);
                //Shake it baby!!!
                candiceCamera.ShakeData = shakeData;
                candiceCamera.CameraShake();
                verticalInput = 0f;
                horizontalInput = 0f;
            }
            //Throwing
            else if (EvaluateInput("Fire2", false, false, false))
            {
                candiceHumanoidMelee.Throw();
                candicePlayerOverrides.PATRAN_BOOSTED_CANDICEAI(_aiController);
                verticalInput = 0f;
                horizontalInput = 0f;
            }
            //GroundSmash
            else if (EvaluateInput("Fire3", false, true, false)) {
                candiceHumanoidMelee.GroundSmash();
                //Shake it baby!!!
                candiceCamera.ShakeData = shakeData;
                candiceCamera.CameraShake();
            }
            //Jumping
            else if (EvaluateInput("Jump", false, false, false))
            {
                verticalInput = 0f;
                horizontalInput = 0f;
                standardActions.Jump();          
            }
            //axis based movement animation (walk, run, strafe) (run uses standard input call)
            else if (verticalInput > 0f && horizontalInput == 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkForwards();
            }
            else if (verticalInput > 0f && horizontalInput < 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkForwardsLeft();
            }
            else if (verticalInput == 0f && horizontalInput < 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.StrafeLeft();
            }
            else if (verticalInput > 0f && horizontalInput > 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkForwardsRight();
            }
            else if (verticalInput == 0f && horizontalInput > 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.StrafeRight();
            }
            else if (verticalInput < 0f && horizontalInput == 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkBackwards();
            }
            else if (verticalInput < 0f && horizontalInput < 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkBackwardsLeft();
            }
            else if (verticalInput < 0f && horizontalInput > 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                standardActions.Walk();
                standardActions.WalkBackwardsRight();
            }
            else if (verticalInput == 0f && horizontalInput == 0f && !EvaluateInput("Run", false, false, false) && !EvaluateInput("Jump", false, false, false))
            {
                //when standing still, reset movement
                standardActions.Idle();
                globalSpeed = thisGlobalSpeed;
                SetSpeed(TemplateAnimator, "animSpeed", globalSpeed);
                if (_playerController != null)
                {
                    _playerController.speed = thisCharacterMoveSpeed;
                    _playerController.jumpSpeed = thisCharacterJumpSpeed;
                }
            }

            //Running
            else if (verticalInput > 0f && horizontalInput == 0 && EvaluateInput("Run", false, false, false))
            {
                standardActions.RunForwards();
                //flash step
                globalSpeed = templGlobalSpeed;
                SetSpeed(TemplateAnimator, "animSpeed", globalSpeed);
                if (_playerController != null)
                {
                    _playerController.speed += (templGlobalSpeed * Time.deltaTime);
                    _playerController.jumpSpeed += (templGlobalSpeed * Time.deltaTime);
                }
                //Shake it baby!!!
                candiceCamera.ShakeData = shakeData;
                candiceCamera.CameraShake();

            }
            else if (verticalInput > 0f && horizontalInput < 0f && EvaluateInput("Run", false, false, false))
            {
                standardActions.RunForwardsLeft();
                //flash step
                globalSpeed = templGlobalSpeed;
                SetSpeed(TemplateAnimator, "animSpeed", globalSpeed);
                if (_playerController != null)
                {
                    _playerController.speed += (templGlobalSpeed * Time.deltaTime);
                    _playerController.jumpSpeed += (templGlobalSpeed * Time.deltaTime);
                }
                //Shake it baby!!!
                candiceCamera.ShakeData = shakeData;
                candiceCamera.CameraShake();

            }
            else if (verticalInput > 0f && horizontalInput > 0f && EvaluateInput("Run", false, false, false))
            {
                standardActions.RunForwardsRight();
                //flash step
                globalSpeed = templGlobalSpeed;
                SetSpeed(TemplateAnimator, "animSpeed", globalSpeed);
                if (_playerController != null)
                {
                    _playerController.speed += (templGlobalSpeed * Time.deltaTime);
                    _playerController.jumpSpeed += (templGlobalSpeed * Time.deltaTime);
                }
                //Shake it baby!!!
                candiceCamera.ShakeData = shakeData;
                candiceCamera.CameraShake();
            }

            //Health & UI
            if (CheckHealth(1f) && hit) { standardActions.Hurt(); candiceUI.UpdateHealthUI("ClassicProgressBar", atkDamage); ActivateVfxByName("Hurt", transform.position, 5f); hit = false; }
            else if (dead) { standardActions.Death(); if (candiceInventoryManager != null) { candiceInventoryManager.drop = inventoryDrop; candiceInventoryManager.Drop(thisAgent); } ActivateVfxByName("Death", transform.position, 5f); candiceCamera.CameraShake(); dead = false; }

            //Handicaps
            if (Handicaper.SelectedHandicap == "Bleed")
            {
                if (_aiController != null)
                {
                    _aiController.CandiceReceiveDamage(5f * Time.deltaTime);
                }
                candiceUI.UpdateHealthUI("ClassicProgressBar", 5f * Time.deltaTime);
            }

            //VSFX
            if (CandiceVSFX != null) {
                //when in movement
                if (horizontalInput != 0f || verticalInput != 0f)
                {
                    if (isAttack) { ActivateVfxByName("Attack", transform.position, 1f); isAttack = false; }
                    ActivateVfxByName("Footsteps", transform.position, 1f);
                    SetVfxActiveByName("PowerAura", true);
                    if (EvaluateInput("Run", false, false, false)) {
                        ActivateVfxByName("Run", transform.position, 0.33f);
                        globalSpeed = thisGlobalSpeed;
                        SetSpeed(TemplateAnimator, "animSpeed", thisGlobalSpeed);
                    }
                    if (EvaluateInput("Fire3", false, true, false))
                    {
                        ActivateVfxByName("GroundSmash", transform.position, 1f);
                    }
                }
                //when idle, show no sfx except idle animation
                //can be tweaked
                else
                {
                    SetVfxActiveByName("PowerAura", false);
                    if (isAttack)
                    {
                        ActivateVfxByName("Attack", transform.position, 1f);
                        isAttack = false;
                    }
                    if (EvaluateInput("Fire3", false, true, false))
                    {
                        ActivateVfxByName("GroundSmash", transform.position, 1f);
                    }
                }
            }

            //IF IS TITANIC
            if (isATitan) {
                if (horizontalInput != 0f || verticalInput != 0f) {
                    candiceCamera.ShakeData = shakeData;
                    candiceCamera.CameraShake();
                }
            }

        }

        //Handles all AI input
        public void AgentInput() {
            
            isAttack = _aiController != null && _aiController.isAttacking;

            //when in motion or not
            if (thisRigidbody == null) {
                thisRigidbody = thisAgent.GetComponent<Rigidbody>();
                if (thisRigidbody != null) {
                    //walk and no attack
                    if (thisRigidbody.linearVelocity.magnitude != 0f && !isAttack)
                    {
                        isWalking = true;
                        standardActions.Walk();
                        if (isATitan)
                        {
                            candiceCamera.ShakeData = shakeData;
                            candiceCamera.CameraShake();
                        }
                    }
                    //walk and attack
                    else if ((thisRigidbody.linearVelocity.magnitude != 0f && isAttack) || (thisRigidbody.linearVelocity.magnitude == 0f && isAttack)) {
                        isWalking = false;
                        standardActions.Attack();
                        candiceCamera.ShakeData = shakeData;
                        candiceCamera.CameraShake();
                    }
                    //all other
                    else
                    {
                        isWalking = false;
                        standardActions.Idle();
                    }
                }
            }
            else {
                //walk and no attack
                if (thisRigidbody.linearVelocity.magnitude != 0f && !isAttack)
                {
                    isWalking = true;
                    standardActions.Walk();
                    if (isATitan)
                    {
                        candiceCamera.ShakeData = shakeData;
                        candiceCamera.CameraShake();
                    }
                }
                //walk and attack
                else if ((thisRigidbody.linearVelocity.magnitude != 0f && isAttack) || (thisRigidbody.linearVelocity.magnitude == 0f && isAttack))
                {
                    isWalking = false;
                    standardActions.Attack();
                    candiceCamera.ShakeData = shakeData;
                    candiceCamera.CameraShake();
                }
                //all other
                else
                {
                    isWalking = false;
                    standardActions.Idle();
                }
            }

            //if you collide and you're not dead
            //ui support to come (in case you want to display enemy health bars in 3D and not in the UI layer.
            if (CheckHealth(1f) && hit) { standardActions.Hurt(); ActivateVfxByName("Hurt", transform.position, 5f); hit = false; }
            else if (dead) { standardActions.Death(); if (candiceInventoryManager != null) { candiceInventoryManager.drop = inventoryDrop; candiceInventoryManager.Drop(thisAgent); } ActivateVfxByName("Death", transform.position, 5f); candiceCamera.CameraShake(); dead = false;}

            //VFX
            if (CandiceVSFX != null)
            {
                if (isWalking)
                {
                    ActivateVfxByName("Footsteps", transform.position, 1f);
                    candiceCamera.CameraShake();
                    isWalking = false;
                }
            }

        }

        //Used to set animation speed (local or global)
        private void SetSpeed(Animator animator, string name, float animSpeed) {
            animator.SetFloat(name, animSpeed);
        }

        //Used to assess all agent types health
        private bool CheckHealth(float minHealthToTrigger) {            
            if (_aiController != null && _aiController.hitPoints >= minHealthToTrigger)
            {
                return true;
            }
            else {
                return false;
            }
        }

        //OnTriggerEnter ensures this script can also be attached to a gameObject for more direct control of some variables, while fully invokable by type and serialization in CandiceAIController
        //Currently used to add impact vsfx to environment and other objects
        void OnTriggerEnter(Collider collider) {
            if (collider.gameObject.CompareTag("Projectile"))
            {
                if (CandiceVSFX != null)
                {
                    ActivateVfxByName("EnviroImpacts", collider.transform.position, 5f);
                    candiceCamera.ShakeData = shakeData;
                    candiceCamera.CameraShake();
                    if (LayerMask.LayerToName(transform.gameObject.layer) == "Obstacle") {
                        transform.gameObject.SetActive(false);
                    }
                }
                else
                {
                    //shake on any other CandiceVSFX activation during projectile collisions
                    if (thisAgent.gameObject.CompareTag("CandiceVSFX"))
                    {
                        candiceCamera.ShakeData = shakeData;
                        candiceCamera.CameraShake();
                    }
                }

            }
            else if (collider.gameObject.CompareTag("Enemy")) {
                if (collider.gameObject.TryGetComponent(out CandiceAIController enemyController) && enemyController.hitPoints > 0.01f) {
                    enemyController.IsAttacking = true;
                    enemyController.CandiceReceiveDamage(thisComboDamagePerHit);
                }
            }
        }

        private void CacheVfxSlots()
        {
            if (CandiceVSFX == null)
            {
                _vfxSlots = Array.Empty<Transform>();
                _vfxDisableAt = Array.Empty<float>();
                return;
            }

            int childCount = CandiceVSFX.childCount;
            if (_vfxSlots.Length != childCount)
            {
                // COLD ALLOC: Transform[childCount] - Candice VFX slot table built during animation initialization only.
                _vfxSlots = new Transform[childCount];
                // COLD ALLOC: float[childCount] - Candice VFX disable schedule built during animation initialization only.
                _vfxDisableAt = new float[childCount];
            }

            for (int i = 0; i < childCount; i++)
            {
                Transform vfx = CandiceVSFX.GetChild(i);
                _vfxSlots[i] = vfx;
                _vfxDisableAt[i] = 0f;
                if (vfx != null && vfx.gameObject.CompareTag("CandiceVSFX"))
                {
                    vfx.gameObject.SetActive(false);
                }
            }
        }

        private void ProcessScheduledVfx()
        {
            float now = Time.time;
            for (int i = 0; i < _vfxSlots.Length; i++)
            {
                Transform vfx = _vfxSlots[i];
                if (vfx != null && _vfxDisableAt[i] > 0f && now >= _vfxDisableAt[i])
                {
                    vfx.gameObject.SetActive(false);
                    _vfxDisableAt[i] = 0f;
                }
            }
        }

        private void ActivateVfxByName(string vfxName, Vector3 position, float activeSeconds)
        {
            for (int i = 0; i < _vfxSlots.Length; i++)
            {
                Transform vfx = _vfxSlots[i];
                if (vfx != null && vfx.gameObject.CompareTag("CandiceVSFX") && vfx.gameObject.name == vfxName)
                {
                    vfx.position = position;
                    vfx.gameObject.SetActive(true);
                    _vfxDisableAt[i] = activeSeconds > 0f ? Time.time + activeSeconds : 0f;
                    return;
                }
            }
        }

        private void SetVfxActiveByName(string vfxName, bool isActive)
        {
            for (int i = 0; i < _vfxSlots.Length; i++)
            {
                Transform vfx = _vfxSlots[i];
                if (vfx != null && vfx.gameObject.CompareTag("CandiceVSFX") && vfx.gameObject.name == vfxName)
                {
                    vfx.gameObject.SetActive(isActive);
                    if (!isActive)
                    {
                        _vfxDisableAt[i] = 0f;
                    }
                    return;
                }
            }
        }

        private void ScheduleGenericCombo(int attackNumber, float delaySeconds)
        {
            _pendingGenericCombo = true;
            _pendingGenericComboAttack = attackNumber;
            _pendingGenericComboAt = Time.time + Mathf.Max(0f, delaySeconds);
        }

        private void ProcessGenericCombo()
        {
            if (!_pendingGenericCombo || Time.time < _pendingGenericComboAt)
            {
                return;
            }

            _pendingGenericCombo = false;
            if (candiceHumanoidMelee != null)
            {
                candiceHumanoidMelee.TriggerGenericCombo(_pendingGenericComboAttack);
            }
        }


    }
}
