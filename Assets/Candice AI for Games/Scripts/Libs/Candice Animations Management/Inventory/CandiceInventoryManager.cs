//Unity
using UnityEngine;

namespace CandiceAIforGames.AI
{
    public class CandiceInventoryManager : DeriveMono
    {
        private const int MaxDropPool = 8;

        [HideInInspector]
        public GameObject drop;
        private GameObject dropPrefab;
        // COLD ALLOC: GameObject[8] - bounded Candice death drop pool - owner: CandiceInventoryManager
        private readonly GameObject[] dropPool = new GameObject[MaxDropPool];

        public void PrepareDropPool(GameObject prefab)
        {
            if (prefab == null || ReferenceEquals(dropPrefab, prefab))
            {
                return;
            }

            drop = prefab;
            dropPrefab = prefab;
        }

        public void Drop(Transform t)
        {
            if (t == null || dropPrefab == null || !ReferenceEquals(dropPrefab, drop))
            {
                return;
            }

            GameObject pooledDrop = GetInactiveDrop();
            if (pooledDrop == null)
            {
                return;
            }

            Vector3 dropPosition = t.position;
            dropPosition.y += 1f;
            dropPosition.z += 2f;
            pooledDrop.transform.position = dropPosition;
            pooledDrop.transform.rotation = Quaternion.identity;
            pooledDrop.SetActive(true);
        }

        private GameObject GetInactiveDrop()
        {
            for (int i = 0; i < dropPool.Length; i++)
            {
                GameObject candidate = dropPool[i];
                if (candidate != null)
                {
                    if (!candidate.activeSelf)
                    {
                        return candidate;
                    }
                }
                else if (dropPrefab != null)
                {
                    GameObject pooledDrop = Instantiate(dropPrefab, dropPrefab.transform.position, dropPrefab.transform.rotation);
                    pooledDrop.SetActive(false);
                    dropPool[i] = pooledDrop;
                    return pooledDrop;
                }
            }

            return null;
        }
    }
}
