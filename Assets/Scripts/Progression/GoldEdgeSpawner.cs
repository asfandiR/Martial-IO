using UnityEngine;
using System.Collections;

// Spawns gold pickups on camera borders so half of pickup is visible on screen.
public class GoldEdgeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject goldPrefab;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform player;

    [Header("Runtime")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool repeatSpawn;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float despawnAfterSeconds = 25f;

    [Header("Half Visible Tuning")]
    [SerializeField, Range(0f, 1f)] private float visiblePortion = 0.5f;
    [SerializeField] private float extraInset;

    private bool warnedPoolMissing;
    private bool warnedPoolUnavailable;
    private Coroutine spawnRoutine;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
            SpawnGoldAtCameraEdge();

        if (repeatSpawn && spawnRoutine == null)
            spawnRoutine = StartCoroutine(RepeatSpawnLoop());
    }

    private void OnEnable()
    {
        if (repeatSpawn && spawnRoutine == null && Application.isPlaying)
            spawnRoutine = StartCoroutine(RepeatSpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator RepeatSpawnLoop()
    {
        while (enabled && gameObject.activeInHierarchy && repeatSpawn)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
                continue;

            SpawnGoldAtCameraEdge();
        }

        spawnRoutine = null;
    }

    [ContextMenu("Spawn Gold At Camera Edge")]
    public void SpawnGoldAtCameraEdge()
    {
        if (goldPrefab == null || targetCamera == null)
            return;

        if (!TryGetCameraBounds(out Vector2 min, out Vector2 max, out float depth))
            return;

        const int attempts = 16;
        for (int i = 0; i < attempts; i++)
        {
            int side = Random.Range(0, 4);
            Vector3 candidate = GetHalfVisiblePosition(side, min, max);

            if (!IsFarEnoughFromPlayer(candidate))
                continue;

            SpawnGold(candidate, Quaternion.identity);
            return;
        }

        Vector3 center = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        center.z = transform.position.z;
        SpawnGold(center, Quaternion.identity);
    }

    private void SpawnGold(Vector3 position, Quaternion rotation)
    {
        GameObject goldInstance = null;
        if (ObjectPooler.Instance != null)
        {
            goldInstance = ObjectPooler.Instance.Get(goldPrefab, position, rotation);
        }

        if (goldInstance == null)
        {
            if (ObjectPooler.Instance == null)
            {
                if (!warnedPoolMissing)
                {
                    warnedPoolMissing = true;
                    Debug.LogWarning("[GoldEdgeSpawner] ObjectPooler missing, using Instantiate for gold pickups.");
                }
            }
            else if (!warnedPoolUnavailable)
            {
                warnedPoolUnavailable = true;
                Debug.LogWarning("[GoldEdgeSpawner] Pool unavailable for gold prefab, using Instantiate.");
            }

            goldInstance = Instantiate(goldPrefab, position, rotation);
        }

        if (goldInstance == null)
            return;

        var pickup = goldInstance.GetComponent<GoldPickup>();
        if (pickup != null)
        {
            pickup.SetDespawnTimer(despawnAfterSeconds);
        }
    }

    private bool TryGetCameraBounds(out Vector2 min, out Vector2 max, out float depth)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        depth = 1f;

        if (targetCamera == null)
            return false;

        float refZ = player != null ? player.position.z : transform.position.z;
        depth = Mathf.Abs(refZ - targetCamera.transform.position.z);
        if (depth < 0.01f)
            depth = 1f;

        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

        min = new Vector2(Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y));
        max = new Vector2(Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));
        return true;
    }

    private Vector3 GetHalfVisiblePosition(int side, Vector2 min, Vector2 max)
    {
        float halfWidth = GetPrefabHalfWidth();
        float halfHeight = GetPrefabHalfHeight();
        float clampedVisible = Mathf.Clamp01(visiblePortion);
        float xOffset = halfWidth * (clampedVisible * 2f - 1f) + extraInset;
        float yOffset = halfHeight * (clampedVisible * 2f - 1f) + extraInset;
        float z = player != null ? player.position.z : transform.position.z;

        switch (side)
        {
            case 0:
                return new Vector3(min.x + xOffset, Random.Range(min.y, max.y), z);
            case 1:
                return new Vector3(max.x - xOffset, Random.Range(min.y, max.y), z);
            case 2:
                return new Vector3(Random.Range(min.x, max.x), min.y + yOffset, z);
            default:
                return new Vector3(Random.Range(min.x, max.x), max.y - yOffset, z);
        }
    }

    private float GetPrefabHalfWidth()
    {
        var sr = goldPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return Mathf.Abs(sr.sprite.bounds.extents.x * sr.transform.lossyScale.x);

        var col2D = goldPrefab.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.extents.x;

        return 0.25f;
    }

    private float GetPrefabHalfHeight()
    {
        var sr = goldPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return Mathf.Abs(sr.sprite.bounds.extents.y * sr.transform.lossyScale.y);

        var col2D = goldPrefab.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.extents.y;

        return 0.25f;
    }

    private bool IsFarEnoughFromPlayer(Vector3 position)
    {
        if (player == null || minDistanceFromPlayer <= 0f)
            return true;

        float sqr = (position - player.position).sqrMagnitude;
        return sqr >= minDistanceFromPlayer * minDistanceFromPlayer;
    }
}
