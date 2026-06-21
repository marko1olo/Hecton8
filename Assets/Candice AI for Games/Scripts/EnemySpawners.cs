using UnityEngine;

public class EnemySpawners : MonoBehaviour
{
    private const int MaxPooledEnemies = 32;
    private const int MaxPooledSpawnFx = 32;

    public GameObject SpawnFx;
    public GameObject enemyPrefab;    
    // In seconds
    [SerializeField] private float interval = 1f;
    private float timer = 0f;
    //parent layer parallax
    [SerializeField]
    private GameObject parentLayer = null;
    private Transform parentLayerTransform;
    // COLD ALLOC: GameObject[32] - bounded Candice demo enemy pool - owner: EnemySpawners
    private readonly GameObject[] enemyPool = new GameObject[MaxPooledEnemies];
    // COLD ALLOC: GameObject[32] - bounded Candice demo spawn effect pool - owner: EnemySpawners
    private readonly GameObject[] spawnFxPool = new GameObject[MaxPooledSpawnFx];
    // COLD ALLOC: float[32] - spawn effect deactivation times - owner: EnemySpawners
    private readonly float[] spawnFxDeactivateAt = new float[MaxPooledSpawnFx];
    private int enemyCursor;
    private int spawnFxCursor;

    // COLD ALLOC: System.Action caches for InstantiateAsync - owner: EnemySpawners
    private System.Action<AsyncOperation> onEnemyInstantiated;
    private System.Action<AsyncOperation> onSpawnFxInstantiated;

    private void Awake()
    {
        onEnemyInstantiated = OnEnemyInstantiated;
        onSpawnFxInstantiated = OnSpawnFxInstantiated;
    }

    // Start is called before the first frame update
    void Start()
    {
        parentLayerTransform = parentLayer == null ? null : parentLayer.transform;

        if (enemyPrefab != null)
        {
            var op = InstantiateAsync(enemyPrefab, enemyPool.Length, parentLayerTransform, transform.position, transform.rotation);
            op.completed += onEnemyInstantiated;
        }

        if (SpawnFx != null)
        {
            var op = InstantiateAsync(SpawnFx, spawnFxPool.Length, transform.position, transform.rotation);
            op.completed += onSpawnFxInstantiated;
        }
    }

    private void OnEnemyInstantiated(AsyncOperation op)
    {
        if (op is AsyncInstantiateOperation<GameObject> instantiateOp)
        {
            var result = instantiateOp.Result;
            for (int i = 0; i < result.Length && i < enemyPool.Length; i++)
            {
                GameObject enemy = result[i];
                if (enemy != null)
                {
                    enemy.SetActive(false);
                    enemyPool[i] = enemy;
                }
            }
        }
    }

    private void OnSpawnFxInstantiated(AsyncOperation op)
    {
        if (op is AsyncInstantiateOperation<GameObject> instantiateOp)
        {
            var result = instantiateOp.Result;
            for (int i = 0; i < result.Length && i < spawnFxPool.Length; i++)
            {
                GameObject spawnFx = result[i];
                if (spawnFx != null)
                {
                    spawnFx.SetActive(false);
                    spawnFxPool[i] = spawnFx;
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateSpawnFxPool();

        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            if (enemyPrefab != null)
            {
                SpawnEnemy();
            }
        }        
        
    }

    private void SpawnEnemy()
    {
        GameObject enemy = GetNextInactive(enemyPool, ref enemyCursor);
        if (enemy == null)
        {
            return;
        }

        Transform enemyTransform = enemy.transform;
        enemyTransform.SetParent(parentLayerTransform);
        enemyTransform.position = transform.position;
        enemyTransform.rotation = transform.rotation;
        enemy.SetActive(true);

        GameObject spawnFx = GetNextInactive(spawnFxPool, ref spawnFxCursor);
        if (spawnFx != null)
        {
            Transform spawnFxTransform = spawnFx.transform;
            spawnFxTransform.SetParent(enemyTransform);
            spawnFxTransform.position = new Vector3(transform.position.x - 0.4f, transform.position.y - 0.3f, 1);
            spawnFxTransform.rotation = transform.rotation;
            spawnFx.SetActive(true);
            int fxIndex = spawnFxCursor == 0 ? spawnFxPool.Length - 1 : spawnFxCursor - 1;
            spawnFxDeactivateAt[fxIndex] = Time.time + 2f;
        }
    }

    private void UpdateSpawnFxPool()
    {
        float now = Time.time;
        for (int i = 0; i < spawnFxPool.Length; i++)
        {
            GameObject spawnFx = spawnFxPool[i];
            if (spawnFx != null && spawnFx.activeSelf && now >= spawnFxDeactivateAt[i])
            {
                spawnFx.SetActive(false);
                spawnFx.transform.SetParent(null);
            }
        }
    }

    private static GameObject GetNextInactive(GameObject[] pool, ref int cursor)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (cursor + i) % pool.Length;
            GameObject candidate = pool[index];
            if (candidate != null && !candidate.activeSelf)
            {
                cursor = (index + 1) % pool.Length;
                return candidate;
            }
        }

        return null;
    }

}
