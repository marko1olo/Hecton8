//System
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
//Unity
using UnityEngine;
using UnityEngine.UI;
//Candice AI
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI
{    
    //class to handle Camera middleware: such as cameraShakers, KillCameras like those provided by Cinemamachine and so on...
    public class CandiceCamera : CandiceMiddleware
    {

        public bool ShakeSupport;
        public GameObject MainCamera;
        public GameObject KillCameraParent;
        public FollowPlayer KillCameraFollow;
        public Transform target;
        public ScriptableObject ShakeData;

        //CameraShake Action
        public void CameraShake()
        {
            if (CheckForDependency("FirstGearGames.SmoothCameraShaker"))
            {
                //FirstGearGames.SmoothCameraShaker.CameraShakerHandler.Shake(ShakeData as FirstGearGames.SmoothCameraShaker.ShakeData);
            }
            else {
                //Debug.Log("Camera Shake will not work until you download and import the free SmoothCameraShaker Asset by First Gear Games from the Unity asset store.");
            }
            
        }

        //KillCam Action
        //uses cinemamachine, and your virtual cams / dolly cams needs to be tagged KillCam
        //this method sets lookatTarget then calls FreeFly
        public void KillCam()
        {
            if (MainCamera != null)
            {
                //first, disable main camera                      
                MainCamera.SetActive(false);
            }
            if (KillCameraParent != null)
            {
                //enable freefly camera parent
                KillCameraParent.SetActive(true);
                if (target != null) {
                    if (KillCameraFollow != null)
                    {
                        KillCameraFollow.player = target;
                    }

                    if (KillCameraParent.transform.childCount > 0)
                    {
                        KillCameraParent.transform.GetChild(0).LookAt(target);
                    }
                }
                
            }
            
        }

        //FreeFly Camera Technique (used in 
        public IEnumerator FreeFly(float waitTime)
        {

            yield return new WaitForSeconds(waitTime);

            //disable freefly camera
            KillCameraParent.SetActive(false);
            //re-enable Main Camera
            MainCamera.SetActive(true);

        }


    }
}
