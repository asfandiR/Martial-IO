using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns enemies using waves and patterns.
// Responsibilities:
// - Wave definitions and progression
// - Spawn patterns (circle, lines, random, etc.)
// - Communicate with LevelManager for scaling
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyDatabase enemyDatabase;
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;

    [Header("Wave")]
    [SerializeField] private float waveInterval = 10f;
    [SerializeField] private int baseEnemiesPerWave = 5;
    [SerializeField] private int enemiesPerWaveGrowth = 2;
    [SerializeField] private float spawnInterval = 0.3f;

    [Header("Spawn Position")]
    [SerializeField] private float offscreenMargin = 2f;
    [SerializeField] private float minSpawnDistanceFromPlayer = 6f;
    [SerializeField] private float fallbackMinRadius = 10f;
    [SerializeField] private float fallbackMaxRadius = 14f;

    private Coroutine waveRoutine;
    private int waveIndex;
    private LevelManager levelManager;
    private bool warnedPoolMissing;

    private void Awake()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Start()
    {
        levelManager = LevelManager.Instance;
        waveRoutine = StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        while (true)
        {
            int count = baseEnemiesPerWave + waveIndex * enemiesPerWaveGrowth;
            count = Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                SpawnEnemy();
                if (spawnInterval > 0f)
                    yield return new WaitForSeconds(spawnInterval);
            }

            waveIndex += 1;
            if (waveInterval > 0f)
                yield return new WaitForSeconds(waveInterval);
            else
                yield return null;
        }
    }

    private void SpawnEnemy()
    {
        if (enemyDatabase == null || enemyDatabase.enemies.Count == 0) return;

        int difficultyStep = levelManager != null ? levelManager.DifficultyStep : 0;
        float difficultyMultiplier = levelManager != null ? levelManager.DifficultyMultiplier : 1f;

        var candidates = enemyDatabase.GetCandidates(difficultyStep);
        if (candidates.Count == 0) candidates = enemyDatabase.enemies;

        var data = PickWeighted(candidates);
        if (data == null || data.prefab == null) return;

        Vector3 pos = GetOffscreenSpawnPosition();
        Quaternion rot = Quaternion.identity;

        GameObject enemy = null;
        if (ObjectPooler.Instance != null)
        {
            enemy = ObjectPooler.Instance.Get(data.prefab, pos, rot);
        }
        else
        {
            if (!warnedPoolMissing)
            {
                warnedPoolMissing = true;
                Debug.LogWarning("[EnemySpawner] ObjectPooler missing, instantiating enemies.");
            }
            enemy = Instantiate(data.prefab, pos, rot);
        }

        if (enemy == null) return;

        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
            controller.Configure(data, difficultyMultiplier);
    }

    private Vector3 GetOffscreenSpawnPosition()
    {
        if (player == null || mainCamera == null)
            return GetFallbackPosition();

        if (!TryGetCameraBounds2D(out var min, out var max))
            return GetFallbackPosition();

        float left = min.x - offscreenMargin;
        float right = max.x + offscreenMargin;
        float bottom = min.y - offscreenMargin;
        float top = max.y + offscreenMargin;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate;
            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: // left
                    candidate = new Vector3(left, Random.Range(bottom, top), player.position.z);
                    break;
                case 1: // right
                    candidate = new Vector3(right, Random.Range(bottom, top), player.position.z);
                    break;
                case 2: // bottom
                    candidate = new Vector3(Random.Range(left, right), bottom, player.position.z);
                    break;
                default: // top
                    candidate = new Vector3(Random.Range(left, right), top, player.position.z);
                    break;
            }

            bool farEnough = (candidate - player.position).sqrMagnitude >= (minSpawnDistanceFromPlayer * minSpawnDistanceFromPlayer);
            if (farEnough && IsOutsideCameraRect(candidate, min, max))
                return candidate;
        }

        return GetFallbackPosition();
    }

    private bool TryGetCameraBounds2D(out Vector3 min, out Vector3 max)
    {
        min = Vector3.zero;
        max = Vector3.zero;

        if (mainCamera == null || player == null) return false;

        float depth = Mathf.Abs(player.position.z - mainCamera.transform.position.z);
        if (depth < 0.01f)
            depth = 1f;

        Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        if (float.IsNaN(bottomLeft.x) || float.IsNaN(bottomLeft.y)) return false;
        if (float.IsNaN(topRight.x) || float.IsNaN(topRight.y)) return false;

        float minX = Mathf.Min(bottomLeft.x, topRight.x);
        float minY = Mathf.Min(bottomLeft.y, topRight.y);
        float maxX = Mathf.Max(bottomLeft.x, topRight.x);
        float maxY = Mathf.Max(bottomLeft.y, topRight.y);

        min = new Vector3(minX, minY, player.position.z);
        max = new Vector3(maxX, maxY, player.position.z);
        return true;
    }

    private bool IsOutsideCameraRect(Vector3 point, Vector3 min, Vector3 max)
    {
        return point.x <= min.x
            || point.x >= max.x
            || point.y <= min.y
            || point.y >= max.y;
    }

    private Vector3 GetFallbackPosition()
    {
        Vector3 center = player != null ? player.position : transform.position;
        float radius = Random.Range(fallbackMinRadius, fallbackMaxRadius);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, center.z);
    }

    private static EnemyData PickWeighted(List<EnemyData> list)
    {
        int total = 0;
        for (int i = 0; i < list.Count; i++)
            total += Mathf.Max(1, list[i].weight);

        int roll = Random.Range(0, total);
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            sum += Mathf.Max(1, list[i].weight);
            if (roll < sum)
                return list[i];
        }

        return list.Count > 0 ? list[0] : null;
    }
}
