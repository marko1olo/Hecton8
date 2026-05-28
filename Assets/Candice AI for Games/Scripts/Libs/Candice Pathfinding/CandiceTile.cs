using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CandiceAIforGames.AI
{
    public class CandiceTile : MonoBehaviour
    {
        // COLD ALLOC: Collider[16] - bounded neighbor query scratch for Candice tile adjacency - owner: CandiceTile
        private static readonly Collider[] NeighborColliders = new Collider[16];

        public bool walkable = true;
        public bool current = false;
        public bool target = false;
        public bool selectable = false;
        // COLD ALLOC: List<CandiceTile>[4] - four cardinal Candice tile neighbors - owner: CandiceTile
        public List<CandiceTile> adjacencyList = new List<CandiceTile>(4);

        //BFS (Breadth first search)
        public bool visited = false;
        public CandiceTile parent = null;
        public int distance = 0;

        public float f = 0;
        public float g = 0;
        public float h = 0;
        private Renderer _cachedRenderer;
        private Material _tileMaterial;
        private Color _lastColor;
        private bool _hasLastColor;

        private void Awake()
        {
            _cachedRenderer = GetComponent<Renderer>();
            if (_cachedRenderer != null)
            {
                // COLD ALLOC: Material[1] - renderer-owned debug tile color instance - owner: CandiceTile
                _tileMaterial = _cachedRenderer.material;
            }
        }

        void Update()
        {
            Color nextColor;
            if (current)
            {
                nextColor = Color.magenta;
            }
            else if (target)
            {
                nextColor = Color.green;
            }
            else if (selectable)
            {
                nextColor = Color.red;
            }
            else
            {
                nextColor = Color.blue;
            }

            if (_tileMaterial != null && (!_hasLastColor || _lastColor != nextColor))
            {
                _tileMaterial.color = nextColor;
                _lastColor = nextColor;
                _hasLastColor = true;
            }
        }

        public void Reset()
        {
            adjacencyList.Clear();
            current = false;
            target = false;
            selectable = false;
            visited = false;
            parent = null;
            distance = 0;
            f = g = h = 0;
        }
        public void FindNeighbors(float jumpHeight, CandiceTile target)
        {
            Reset();
            CheckTile(Vector3.forward, jumpHeight, target);
            CheckTile(-Vector3.forward, jumpHeight, target);
            CheckTile(Vector3.right, jumpHeight, target);
            CheckTile(-Vector3.right, jumpHeight, target);
        }

        public void CheckTile(Vector3 direction, float jumpHeight, CandiceTile target)
        {
            Vector3 halfExtents = new Vector3(0.25f, (1 + jumpHeight) / 2.0f, 0.25f);
            int colliderCount = Physics.OverlapBoxNonAlloc(transform.position + direction, halfExtents, NeighborColliders);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (colliderCount == NeighborColliders.Length)
            {
                Debug.LogWarning("CandiceTile neighbor collider buffer saturated. Increase NeighborColliders capacity.");
            }
#endif
            for (int i = 0; i < colliderCount; i++)
            {
                Collider item = NeighborColliders[i];
                if (item == null || !item.TryGetComponent(out CandiceTile candiceTile))
                {
                    continue;
                }

                if (candiceTile.walkable)
                {
                    RaycastHit hit;
                    if (!Physics.Raycast(candiceTile.transform.position, Vector3.up, out hit, 1) || (candiceTile == target))
                    {
                        adjacencyList.Add(candiceTile);
                    }
                }
            }
        }
    }
}
