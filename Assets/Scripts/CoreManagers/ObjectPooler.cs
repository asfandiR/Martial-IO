using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generic object pooler for enemies, bullets, XP, VFX.
// Responsibilities:
// - Prewarm pools
// - Get/Return pooled objects
// - Expand pools when needed
public class ObjectPooler : MonoBehaviour
{
    [System.Serializable]
    public class PoolDefinition
    {
        public GameObject prefab;
        public int size = 16;
        public bool canExpand = false;
        public Transform parentOverride;
    }

    private class PooledObject : MonoBehaviour
    {
        public int prefabId;
    }

    public static ObjectPooler Instance { get; private set; }

    [SerializeField] private PoolDefinition[] pools;

    private readonly Dictionary<int, Queue<GameObject>> poolMap = new Dictionary<int, Queue<GameObject>>(32);
    private readonly Dictionary<int, PoolDefinition> poolDefs = new Dictionary<int, PoolDefinition>(32);
    private readonly HashSet<int> warnedEmptyPool = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Prewarm();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        RebuildPools();
    }

    private void RebuildPools()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        poolMap.Clear();
        poolDefs.Clear();
        warnedEmptyPool.Clear();
        Prewarm();
    }

    private void Prewarm()
    {
        if (pools == null) return;

        for (int i = 0; i < pools.Length; i++)
        {
            var def = pools[i];
            if (def == null || def.prefab == null) continue;

            int id = def.prefab.GetInstanceID();
            poolDefs[id] = def;

            if (!poolMap.TryGetValue(id, out var queue))
            {
                queue = new Queue<GameObject>(def.size);
                poolMap[id] = queue;
            }

            Transform parent = def.parentOverride != null ? def.parentOverride : transform;
            for (int j = 0; j < def.size; j++)
            {
                var instance = Instantiate(def.prefab, parent);
                instance.SetActive(false);
                AttachPooledMeta(instance, id);
                queue.Enqueue(instance);
            }
        }
    }

    private static void AttachPooledMeta(GameObject go, int prefabId)
    {
        var meta = go.GetComponent<PooledObject>();
        if (meta == null) meta = go.AddComponent<PooledObject>();
        meta.prefabId = prefabId;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;
        int id = prefab.GetInstanceID();

        if (!poolMap.TryGetValue(id, out var queue))
        {
            Debug.LogWarning($"[ObjectPooler] No pool registered for prefab: {prefab.name}");
            return null;
        }

        if (queue.Count == 0)
        {
            if (poolDefs.TryGetValue(id, out var def) && def.canExpand)
            {
                var instance = Instantiate(prefab, parent != null ? parent : transform);
                AttachPooledMeta(instance, id);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
                return instance;
            }

            if (!warnedEmptyPool.Contains(id))
            {
                warnedEmptyPool.Add(id);
                Debug.LogWarning($"[ObjectPooler] Pool exhausted for prefab: {prefab.name}. Consider increasing pool size.");
            }

            return null;
        }

        var obj = queue.Dequeue();
        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        var obj = Get(prefab.gameObject, position, rotation, parent);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    public void ReturnToPool(GameObject instance)
    {
        if (instance == null) return;

        var meta = instance.GetComponent<PooledObject>();
        if (meta == null || meta.prefabId == 0)
        {
            Debug.LogWarning("[ObjectPooler] Returned object has no pool metadata.");
            instance.SetActive(false);
            return;
        }

        if (!poolMap.TryGetValue(meta.prefabId, out var queue))
        {
            Debug.LogWarning("[ObjectPooler] Returned object has unknown pool id.");
            instance.SetActive(false);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform);
        queue.Enqueue(instance);
    }
}
