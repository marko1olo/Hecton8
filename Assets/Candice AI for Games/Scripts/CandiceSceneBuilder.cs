//System
using System;
using System.Collections;
using System.Collections.Generic;
//Unity
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
//Candice AI
using CandiceAIforGames.AI;
using Hecton8.Core;

namespace CandiceAIforGames.AI
{
    public class CandiceSceneBuilder : MonoBehaviour
    {

        //store your gameObjects
        public GameObject AI;
        public GameObject EventSystem;
        public GameObject Cameras;
        public GameObject Lighting;
        public GameObject Audio;
        public GameObject UI;
        public GameObject Agents;
        public GameObject Environment;
        public GameObject SceneDecor;
        
        
        //static references to objects for reset
        public static GameObject aimngr;
        public static GameObject eventsys;
        public static GameObject cams;
        public static GameObject lights;
        public static GameObject audios;
        public static GameObject userInterface;
        public static GameObject aicontrollers;
        public static GameObject enviro;
        public static GameObject decor;

        private List<GameObject> _instantiatedObjects = new List<GameObject>();

        //store these in an array of objects
        public static GameObject[] sceneBuilderObjects;
        private int sceneBuilderObjectsTotal = 9;

        //reset button
        public Button resetButton;

        //Loading 
        public GameObject LoadingUIObject;

        // Start is called before the first frame update
        //void Start()
        //{
        //    sceneBuilderObjects = new object[sceneBuilderObjectsTotal];
        //    candiceUI = new CandiceUI();
        //    Store();
        //    if (resetButton != null) {
        //        resetButton.onClick.AddListener(delegate { Reset();});
        //    }            
        //}

        void Awake() {

            //new sceneBuilderObject array
            sceneBuilderObjects = new GameObject[sceneBuilderObjectsTotal];

            //store first
            Store();
            
            //add listener
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(delegate { Reset(); });
            }

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void Store() {
            //store a reference to gameObjects on start, can be used from static later for reset
            aimngr = AI;
            eventsys = EventSystem;
            cams = Cameras;
            lights = Lighting;
            audios = Audio;
            userInterface = UI;
            aicontrollers = Agents;
            enviro = Environment;
            decor = SceneDecor;
            sceneBuilderObjects[0] = aimngr;
            sceneBuilderObjects[1] = eventsys;
            sceneBuilderObjects[2] = cams;
            sceneBuilderObjects[3] = lights;
            sceneBuilderObjects[4] = audios;
            sceneBuilderObjects[5] = userInterface;
            sceneBuilderObjects[6] = aicontrollers;
            sceneBuilderObjects[7] = enviro;
            sceneBuilderObjects[8] = decor;

        }

        public void Reset() {

            //store new values if any first
            //requires that you re-add agents if any were destroyed prior to reset
            Store();

            //destroy
            if (_instantiatedObjects != null) {
                ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
                foreach (GameObject obj in _instantiatedObjects) {
                    if (obj != null) {
                        if (objectPoolManager != null) {
                            objectPoolManager.Despawn(obj);
                        } else {
                            Destroy(obj);
                        }
                    }
                }
                _instantiatedObjects.Clear();
            }

            //after destroy drop a loading object
            //GameObject canvasContainer = new GameObject();
            //Canvas canvas = canvasContainer.AddComponent(typeof(Canvas)) as Canvas;
            //GameObject loading = Instantiate(LoadingUIObject, LoadingUIObject.transform.position, Quaternion.identity);
            //loading.transform.parent = canvasContainer.gameObject.transform;


            //build while loading
            if (sceneBuilderObjects != null && sceneBuilderObjects.Length > 0)
            {

                ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;

                foreach (GameObject sceneBuilderObject in sceneBuilderObjects) {

                    if (sceneBuilderObject != null) {

                        GameObject instantiatedObj;
                        if (objectPoolManager != null) {
                            instantiatedObj = objectPoolManager.Spawn(sceneBuilderObject, sceneBuilderObject.transform.position, Quaternion.identity);
                        } else {
                            instantiatedObj = Instantiate(sceneBuilderObject, sceneBuilderObject.transform.position, Quaternion.identity);
                        }
                        if (_instantiatedObjects == null) _instantiatedObjects = new List<GameObject>();
                        _instantiatedObjects.Add(instantiatedObj);

                    } 

                }

            }




        }


        IEnumerator timedIsActive(float resetTime, GameObject toSet) { 
            
            yield return new WaitForSeconds(resetTime);

            if (toSet.activeSelf) {

                toSet.SetActive(false);

            }
            else {

                toSet.SetActive(true);

            }


        }

    }
}
