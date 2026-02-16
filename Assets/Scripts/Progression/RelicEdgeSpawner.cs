using UnityEngine;
using System.Collections.Generic;

// Spawns relics on camera borders so half of the relic is visible on screen.
public class RelicEdgeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject relicPrefab;
    [SerializeField] private List<RelicData> allRelics = new List<RelicData>(256);
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform player;

    [Header("Runtime")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool repeatSpawn = false;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float despawnAfterSeconds = 25f;

    [Header("Half Visible Tuning")]
    [SerializeField, Range(0f, 1f)] private float visiblePortion = 0.5f;
    [SerializeField] private float extraInset = 0f;

    private float timer;
    private bool warnedPoolMissing;

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
        timer = Mathf.Max(0.01f, spawnInterval);

        if (spawnOnStart)
            SpawnRelicAtCameraEdge();
    }

    private void Update()
    {
        if (!repeatSpawn)
            return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = Mathf.Max(0.01f, spawnInterval);
        SpawnRelicAtCameraEdge();
    }

    [ContextMenu("Spawn Relic At Camera Edge")]
    public void SpawnRelicAtCameraEdge()
    {
        if (relicPrefab == null || targetCamera == null)
            return;
        if (allRelics == null || allRelics.Count == 0)
            return;

        RelicData relicToSpawn = GetRandomUnownedRelic();
        if (relicToSpawn == null)
            return;

        if (!TryGetCameraBounds(out Vector2 min, out Vector2 max, out float depth))
            return;

        const int attempts = 16;
        for (int i = 0; i < attempts; i++)
        {
            int side = Random.Range(0, 4);
            Quaternion rot = Quaternion.identity;
            Vector3 candidate = GetHalfVisiblePosition(side, min, max);

            if (!IsFarEnoughFromPlayer(candidate))
                continue;

            SpawnRelic(candidate, rot, relicToSpawn);
            return;
        }

        // Fallback: spawn in camera center if all attempts failed.
        Vector3 center = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        center.z = transform.position.z;
        SpawnRelic(center, Quaternion.identity, relicToSpawn);
    }

    private void SpawnRelic(Vector3 position, Quaternion rotation, RelicData relicData)
    {
        GameObject relicInstance = null;
        if (ObjectPooler.Instance != null)
        {
            relicInstance = ObjectPooler.Instance.Get(relicPrefab, position, rotation);
        }
        else
        {
            if (!warnedPoolMissing)
            {
                warnedPoolMissing = true;
                Debug.LogWarning("[RelicEdgeSpawner] ObjectPooler missing, using Instantiate for relics.");
            }
            relicInstance = Instantiate(relicPrefab, position, rotation);
        }

        if (relicInstance == null)
            return;

        var pickup = relicInstance.GetComponent<RelicPickup>();
        if (pickup != null)
        {
            if (relicData == null)
            {
                if (ObjectPooler.Instance != null)
                    ObjectPooler.Instance.ReturnToPool(relicInstance);
                else
                    Destroy(relicInstance);
                return;
            }

            pickup.SetRelicData(relicData);
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
            case 0: // left edge
                return new Vector3(min.x + xOffset, Random.Range(min.y, max.y), z);
            case 1: // right edge
                return new Vector3(max.x - xOffset, Random.Range(min.y, max.y), z);
            case 2: // bottom edge
                return new Vector3(Random.Range(min.x, max.x), min.y + yOffset, z);
            default: // top edge
                return new Vector3(Random.Range(min.x, max.x), max.y - yOffset, z);
        }
    }

    private float GetPrefabHalfWidth()
    {
        var sr = relicPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return Mathf.Abs(sr.sprite.bounds.extents.x * sr.transform.lossyScale.x);

        var col2D = relicPrefab.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.extents.x;

        return 0.25f;
    }

    private float GetPrefabHalfHeight()
    {
        var sr = relicPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return Mathf.Abs(sr.sprite.bounds.extents.y * sr.transform.lossyScale.y);

        var col2D = relicPrefab.GetComponentInChildren<Collider2D>();
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

    private RelicData GetRandomUnownedRelic()
    {
        if (allRelics == null || allRelics.Count == 0)
            return null;

        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            for (int i = 0; i < allRelics.Count; i++)
            {
                if (allRelics[i] != null)
                    return allRelics[i];
            }
            return null;
        }

        List<RelicData> candidates = new List<RelicData>(allRelics.Count);
        for (int i = 0; i < allRelics.Count; i++)
        {
            RelicData relic = allRelics[i];
            if (relic == null) continue;
            if (inventory.HasRelic(relic)) continue;
            candidates.Add(relic);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
