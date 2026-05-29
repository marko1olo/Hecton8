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
            for (int i = 0; i < dropPool.Length; i++)
            {
                if (dropPool[i] != null)
                {
                    dropPool[i].SetActive(false);
                    continue;
                }

                GameObject pooledDrop = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);
                pooledDrop.SetActive(false);
                dropPool[i] = pooledDrop;
            }
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
                if (candidate != null && !candidate.activeSelf)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
