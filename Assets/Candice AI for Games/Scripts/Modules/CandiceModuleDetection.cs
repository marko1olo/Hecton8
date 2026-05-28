using CandiceAIforGames.AI.Pathfinding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CandiceAIforGames.AI.Utils.Enums;

namespace CandiceAIforGames.AI
{
    public class CandiceModuleDetection:CandiceBaseModule
    {
        private const int MaxDetectionColliders = 64;
        private const int MaxDetectionTags = 16;
        private const int MaxObstacleLines = 64;
        // COLD ALLOC: Collider[64] - bounded 3D detection query scratch - owner: CandiceModuleDetection
        private readonly Collider[] _scanColliders = new Collider[MaxDetectionColliders];
        // COLD ALLOC: Collider2D[64] - bounded 2D detection query scratch - owner: CandiceModuleDetection
        private readonly Collider2D[] _scanColliders2D = new Collider2D[MaxDetectionColliders];
        // COLD ALLOC: RaycastHit[1] - per-line 3D obstacle ray scratch - owner: CandiceModuleDetection
        private readonly RaycastHit[] _obstacleRayHits = new RaycastHit[1];
        // COLD ALLOC: RaycastHit2D[1] - per-line 2D obstacle ray scratch - owner: CandiceModuleDetection
        private readonly RaycastHit2D[] _obstacleRayHits2D = new RaycastHit2D[1];
        // COLD ALLOC: Vector3[64] - obstacle avoidance normal scratch - owner: CandiceModuleDetection
        private readonly Vector3[] _normalScratch = new Vector3[MaxObstacleLines];
        // COLD ALLOC: Dictionary<string,List<GameObject>>[16] - active detection result view - owner: CandiceModuleDetection
        private readonly Dictionary<string, List<GameObject>> _detectedObjects = new Dictionary<string, List<GameObject>>(MaxDetectionTags);
        // COLD ALLOC: List<GameObject>[16] - reusable detection result buckets - owner: CandiceModuleDetection
        private readonly List<GameObject>[] _detectedBuckets = new List<GameObject>[MaxDetectionTags];
        private ContactFilter2D _physics2DFilter = ContactFilter2D.noFilter;

        Transform transform;
        public Action<CandiceDetectionResults> objectDetectedCallback;
        int direction = 0; //0=left,1=right

        public CandiceModuleDetection(Transform transform, Action<CandiceDetectionResults> _objectDetectedCallback, string moduleName = "CandiceModuleDetection") : base(moduleName)
        {
            this.transform = transform;
            objectDetectedCallback = _objectDetectedCallback;
            Utils.Utils.LogClassInitialisation(this);
            for (int i = 0; i < _detectedBuckets.Length; i++)
            {
                // COLD ALLOC: List<GameObject>[64] - bounded detected objects per tag - owner: CandiceModuleDetection
                _detectedBuckets[i] = new List<GameObject>(MaxDetectionColliders);
            }
        }

        public void ScanForObjects(CandiceDetectionRequest request)
        {
            Vector3 center = transform.position;

            float radius = request.radius;
            float lineOfSight = request.lineOfSight;
            SensorType type = request.type;

            int hitColliderCount = 0;
            if (type == SensorType.Sphere)
            {
                hitColliderCount = Physics.OverlapSphereNonAlloc(center, radius, _scanColliders);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (hitColliderCount == _scanColliders.Length)
                {
                    Debug.LogWarning("Candice 3D detection collider buffer saturated. Increase MaxDetectionColliders.");
                }
#endif
            }

            PrepareDetectionResults(request.detectionTags);
            //Loop though each object
            for (int i = 0; i < hitColliderCount; i++)
            {
                Collider collider = _scanColliders[i];
                if (collider == null)
                {
                    continue;
                }

                GameObject go = collider.gameObject;
                float angle = Vector3.Angle(go.transform.position - center, transform.forward);
                if (angle <= lineOfSight / 2)
                {
                    CompareTags(go, request.detectionTags, _detectedObjects);
                }
                
                

            }
            objectDetectedCallback(new CandiceDetectionResults(_detectedObjects));
        }
        public void ScanForObjects2D(CandiceDetectionRequest request)
        {
            Vector3 center = transform.position;

            float radius = request.radius;
            float lineOfSight = request.lineOfSight;
            SensorType type = request.type;

            //Array that will store all collided objects
            Vector2 center2D = new Vector2(center.x, center.y);
            int hitColliderCount = Physics2D.OverlapCircle(center2D, radius, _physics2DFilter, _scanColliders2D);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitColliderCount == _scanColliders2D.Length)
            {
                Debug.LogWarning("Candice 2D detection collider buffer saturated. Increase MaxDetectionColliders.");
            }
#endif
            PrepareDetectionResults(request.detectionTags);
            //Loop through each object
            for (int i = 0; i < hitColliderCount; i++)
            {
                Collider2D collider = _scanColliders2D[i];
                if (collider == null)
                {
                    continue;
                }

                GameObject go = collider.gameObject;
                float angle = Vector2.Angle((new Vector2(go.transform.position.x, go.transform.position.y) - center2D), transform.forward);
                //Check if the object is in the enemy line of sight.
                if (angle <= lineOfSight / 2)
                {
                    CompareTags(go, request.detectionTags, _detectedObjects);
                }

            }
            objectDetectedCallback(new CandiceDetectionResults(_detectedObjects));
        }
        public void AvoidObstacles(Transform Target, Vector3 movePoint, Transform transform, float size, float movementSpeed, bool is3D, float maxDistance,int lines, LayerMask perceptionMask)
        {
            //
            //Method Name : void Move(Transform Target, Transform transform, float size)
            //Purpose     : This method moves the agent while avoiding immediate obstacles.
            //Re-use      : none
            //Input       : Transform Target, Transform transform, float size
            //Output      : void
            //
            if (!is3D)
            {
                AvoidObstacles2D(Target, movePoint, transform, size, movementSpeed, maxDistance, lines, perceptionMask);
                return;
            }
            bool obstacleHit = false;
            Vector3 dir = (transform.forward).normalized;
            float distance = maxDistance;

            if (lines <= 0)
            {
                return;
            }

            int lineCount = Mathf.Min(lines, MaxObstacleLines);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (lines > MaxObstacleLines)
            {
                Debug.LogWarning("Candice 3D obstacle line buffer saturated. Increase MaxObstacleLines.");
            }
#endif
            float step = size / lines;
            float currentPos = transform.position.x - size;

            int countLeft = 0;
            int countRight = 0;
            int normalCount = 0;

            for (int i = 0; i < lineCount; i++)
            {
                Vector3 point = transform.position;
                point.x = currentPos;
                currentPos += step * 2;

                if (i == 0 || i == lineCount - 1)
                {
                    distance = maxDistance;
                }
                else
                {
                    distance = maxDistance / 2;
                }
                int hitCount = Physics.RaycastNonAlloc(point, transform.forward, _obstacleRayHits, distance);
                if (hitCount > 0)
                {
                    RaycastHit hit = _obstacleRayHits[0];
                    if (hit.transform != transform && hit.transform != Target.transform)
                    {
                        if (HasLayer(perceptionMask, hit.transform.gameObject.layer))
                        {
                            Color color = Color.red;
                            if (i == 0)
                                color = Color.blue;
                            else if (i == lineCount - 1)
                                color = Color.green;
                            Debug.DrawLine(point, hit.point, color);
                            _normalScratch[normalCount] = hit.normal;
                            normalCount++;
                            if(hit.normal.z < 0)
                            {
                                countRight = countRight + 1;
                                //direction = 1;
                            }
                            if(hit.normal.x < 0)
                            {
                                countLeft = countLeft + 1;
                                //direction = 0;
                            }
                            //dir += hit.normal * 90;
                            obstacleHit = true;
                        }
                    }
                }
            }
            
            if(normalCount > 0)
            {
                if (countLeft > countRight)
                {
                    direction = 0;
                }
                else
                {
                    direction = 1;
                }
                bool isComplete = false;
                int index = 0;
                if (direction == 1)
                {
                    index = normalCount - 1;
                }

                while (!isComplete)
                {

                    dir += _normalScratch[index] * 90;
                    if (direction == 1)
                    {
                        index = index - 1;
                        if (index < 0)
                        {
                            isComplete = true;
                        }
                    }
                    else
                    {
                        index = index + 1;
                        if (index > normalCount - 1)
                        {
                            isComplete = true;
                        }
                    }


                }
            }
            


            
            if (obstacleHit)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, movementSpeed * Time.deltaTime);
            }
            else
            {
                movePoint = new Vector3(movePoint.x, transform.position.y, movePoint.z);
                dir = (movePoint - transform.position).normalized;
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, movementSpeed * Time.deltaTime);
            }
            
        }

        public static bool HasLayer(LayerMask layerMask, int layer)
        {
            if (layerMask == (layerMask | (1 << layer)))
            {
                return true;
            }

            return false;
        }


        private bool compareObjToTags(GameObject obj, List<string> tags)
        {
            //Check if the object is in the tag list
            bool hasMatch = false;

            for (int i = 0; i < tags.Count; i++)
            {
                if(obj.CompareTag(tags[i]))
                {
                    hasMatch = true;
                    i = tags.Count;
                }
            }

            return hasMatch;
        }

        public void AvoidObstacles2D(Transform Target, Vector3 movePoint, Transform transform, float size, float movementSpeed, float distance, int lines, LayerMask perceptionMask)
        {

            bool obstacleHit = false;
            Vector2 dir = (Target.position - transform.position).normalized;
            if (lines <= 0)
            {
                return;
            }

            int lineCount = Mathf.Min(lines, MaxObstacleLines);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (lines > MaxObstacleLines)
            {
                Debug.LogWarning("Candice 2D obstacle line buffer saturated. Increase MaxObstacleLines.");
            }
#endif
            float step = size / lines;
            float currentPos = transform.position.x - size;

            for (int i = 0; i < lineCount; i++)
            {
                Vector3 point = transform.position;
                point.x = currentPos;
                currentPos += step * 2;

                float rayDistance = distance;
                if (i >= lineCount / 2 && i <= lineCount / 2 + 1)
                {
                    rayDistance = distance * 1.5f;
                }
                int hitCount = Physics2D.Raycast(point, transform.forward, _physics2DFilter, _obstacleRayHits2D, rayDistance);
                if (hitCount > 0)
                {
                    RaycastHit2D hit = _obstacleRayHits2D[0];
                    if (hit.collider != null && hit.transform != transform && hit.transform != Target.transform)
                    {
                        Debug.DrawLine(point, hit.point, Color.red);
                        dir += hit.normal * 50;
                        obstacleHit = true;
                    }
                }
            }
            if (obstacleHit)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, movementSpeed * Time.deltaTime);
            }
            else
            {
                movePoint = new Vector3(movePoint.x, transform.position.y, movePoint.z);
                dir = (movePoint - transform.position).normalized;
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, movementSpeed * Time.deltaTime);
            }







            //Quaternion rot = Quaternion.LookRotation(dir);
            //transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime);
            //transform.position += new Vector3(dir.x, dir.y) * movementSpeed * Time.deltaTime;


        }
        public void CompareTags(GameObject go, List<string> detectionTags, Dictionary<string, List<GameObject>> detectedObjects)
        {
            if (detectionTags == null)
            {
                return;
            }

            int tagCount = Mathf.Min(detectionTags.Count, MaxDetectionTags);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (detectionTags.Count > MaxDetectionTags)
            {
                Debug.LogWarning("Candice detection tag buffer saturated. Increase MaxDetectionTags.");
            }
#endif
            for (int i = 0; i < tagCount; i++)
            {
                string detectionTag = detectionTags[i];
                if (go == null || string.IsNullOrEmpty(detectionTag) || !go.CompareTag(detectionTag))
                {
                    continue;
                }

                List<GameObject> detectedList = _detectedBuckets[i];
                if(!detectedObjects.ContainsKey(detectionTag))
                {
                    detectedObjects.Add(detectionTag, detectedList);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (detectedList.Count == detectedList.Capacity)
                {
                    Debug.LogWarning("Candice detection result bucket saturated. Increase MaxDetectionColliders.");
                    return;
                }
#endif
                if (detectedList.Count < detectedList.Capacity)
                {
                    detectedList.Add(go);
                }

                return;
            }
        }

        private void PrepareDetectionResults(List<string> detectionTags)
        {
            _detectedObjects.Clear();

            int tagCount = detectionTags == null ? MaxDetectionTags : Mathf.Min(detectionTags.Count, MaxDetectionTags);
            for (int i = 0; i < tagCount; i++)
            {
                _detectedBuckets[i].Clear();
            }
        }

    }
    
    public struct CandiceDetectionRequest
    {
        public SensorType type;
        public List<string> detectionTags;
        public float radius;
        public float height;
        public float lineOfSight;
        public bool is3D;

        public CandiceDetectionRequest(SensorType type, List<string> detectionTags, float radius, float height, float lineOfSight, bool is3D)
        {
            this.type = type;
            this.detectionTags = detectionTags;
            this.radius = radius;
            this.height = height;
            this.lineOfSight = lineOfSight;
            this.is3D = is3D;
        }
    }
    public struct CandiceDetectionResults
    {
        public Dictionary<string,List<GameObject>> objects;

        public CandiceDetectionResults(Dictionary<string, List<GameObject>> objects)
        {
            this.objects = objects;
        }
    }
}

