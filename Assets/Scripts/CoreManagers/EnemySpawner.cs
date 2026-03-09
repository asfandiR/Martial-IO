using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns enemies in progression phases using weighted history between unlocked enemy tiers.
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyDatabase enemyDatabase;
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;

    [Header("Phase Progression")]
    [SerializeField] private float spawnInterval = 0.3f;
    [SerializeField] private float firstPhaseDurationSeconds = 15f;
    [SerializeField] private float phaseDurationGrowthSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float phaseHistoryInertia = 0.5f;
    [SerializeField] private float postFinalPhaseQualityGrowth = 0.03f;
    [SerializeField] private float postFinalPhaseExtraSpawnsGrowth = 0.25f;

    [Header("Spawn Position")]
    [SerializeField] private float offscreenMargin = 2f;
    [SerializeField] private float minSpawnDistanceFromPlayer = 6f;
    [SerializeField] private float fallbackMinRadius = 10f;
    [SerializeField] private float fallbackMaxRadius = 14f;

    private Coroutine waveRoutine;
    private int phaseIndex;
    private LevelManager levelManager;
    private bool warnedPoolMissing;
    private bool warnedPoolUnavailable;

    private readonly List<int> difficultyPhases = new List<int>(16);
    private readonly List<List<EnemyData>> enemiesByPhase = new List<List<EnemyData>>(16);

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
        RebuildPhaseCache();
        waveRoutine = StartCoroutine(PhaseLoop());
    }

    private IEnumerator PhaseLoop()
    {
        while (true)
        {
            if (!HasSpawnableEnemies())
            {
                yield return null;
                continue;
            }

            if (difficultyPhases.Count == 0)
                RebuildPhaseCache();

            int totalPhases = Mathf.Max(1, difficultyPhases.Count);
            int unlockedPhases = Mathf.Min(totalPhases, phaseIndex + 1);
            int overflowPhases = Mathf.Max(0, phaseIndex - (totalPhases - 1));
            float[] phaseWeights = CalculateSpawnWeights(unlockedPhases, phaseHistoryInertia);

            float phaseDuration = Mathf.Max(0.01f, firstPhaseDurationSeconds + (phaseIndex * phaseDurationGrowthSeconds));
            float phaseEndTime = Time.time + phaseDuration;

            while (Time.time < phaseEndTime)
            {
                float baseDifficultyMultiplier = levelManager != null ? levelManager.DifficultyMultiplier : 1f;
                float phaseQualityMultiplier = 1f + (overflowPhases * Mathf.Max(0f, postFinalPhaseQualityGrowth));
                float difficultyMultiplier = baseDifficultyMultiplier * phaseQualityMultiplier;

                int spawnsThisTick = 1 + Mathf.FloorToInt(Mathf.Max(0f, overflowPhases) * Mathf.Max(0f, postFinalPhaseExtraSpawnsGrowth));
                spawnsThisTick = Mathf.Max(1, spawnsThisTick);

                for (int i = 0; i < spawnsThisTick; i++)
                    SpawnEnemyFromPhases(unlockedPhases, phaseWeights, difficultyMultiplier);

                if (spawnInterval > 0f)
                    yield return new WaitForSeconds(spawnInterval);
                else
                    yield return null;
            }

            phaseIndex += 1;
        }
    }

    private bool HasSpawnableEnemies()
    {
        return enemyDatabase != null && enemyDatabase.enemies != null && enemyDatabase.enemies.Count > 0;
    }

    private void RebuildPhaseCache()
    {
        difficultyPhases.Clear();
        enemiesByPhase.Clear();

        if (!HasSpawnableEnemies())
            return;

        var uniqueSteps = new SortedSet<int>();
        for (int i = 0; i < enemyDatabase.enemies.Count; i++)
        {
            var enemy = enemyDatabase.enemies[i];
            if (enemy == null) continue;
            uniqueSteps.Add(Mathf.Max(0, enemy.minDifficultyStep));
        }

        foreach (int step in uniqueSteps)
        {
            difficultyPhases.Add(step);
            enemiesByPhase.Add(new List<EnemyData>(8));
        }

        if (difficultyPhases.Count == 0)
            return;

        for (int i = 0; i < enemyDatabase.enemies.Count; i++)
        {
            var enemy = enemyDatabase.enemies[i];
            if (enemy == null) continue;

            int step = Mathf.Max(0, enemy.minDifficultyStep);
            int phaseListIndex = difficultyPhases.IndexOf(step);
            if (phaseListIndex >= 0)
                enemiesByPhase[phaseListIndex].Add(enemy);
        }
    }

    private void SpawnEnemyFromPhases(int unlockedPhases, float[] phaseWeights, float difficultyMultiplier)
    {
        if (!HasSpawnableEnemies()) return;

        var phaseCandidates = PickPhaseCandidates(unlockedPhases, phaseWeights);
        if (phaseCandidates == null || phaseCandidates.Count == 0)
            phaseCandidates = enemyDatabase.enemies;

        var data = PickWeighted(phaseCandidates);
        if (data == null || data.prefab == null) return;

        Vector3 pos = GetOffscreenSpawnPosition();
        Quaternion rot = Quaternion.identity;

        GameObject enemy = null;
        if (ObjectPooler.Instance != null)
        {
            enemy = ObjectPooler.Instance.Get(data.prefab, pos, rot);
        }

        if (enemy == null)
        {
            if (ObjectPooler.Instance == null)
            {
                if (!warnedPoolMissing)
                {
                    warnedPoolMissing = true;
                    Debug.LogWarning("[EnemySpawner] ObjectPooler missing, instantiating enemies.");
                }
            }
            else if (!warnedPoolUnavailable)
            {
                warnedPoolUnavailable = true;
                Debug.LogWarning("[EnemySpawner] Pool unavailable for this enemy prefab, falling back to Instantiate.");
            }

            enemy = Instantiate(data.prefab, pos, rot);
        }

        if (enemy == null) return;

        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
            controller.Configure(data, difficultyMultiplier);
    }

    private List<EnemyData> PickPhaseCandidates(int unlockedPhases, float[] phaseWeights)
    {
        if (difficultyPhases.Count == 0 || enemiesByPhase.Count == 0)
            return null;

        int phasePoolCount = Mathf.Clamp(unlockedPhases, 1, enemiesByPhase.Count);
        int pickedPhaseLocalIndex = PickWeightedIndex(phaseWeights, phasePoolCount);
        pickedPhaseLocalIndex = Mathf.Clamp(pickedPhaseLocalIndex, 0, phasePoolCount - 1);

        var list = enemiesByPhase[pickedPhaseLocalIndex];
        if (list != null && list.Count > 0)
            return list;

        for (int i = phasePoolCount - 1; i >= 0; i--)
        {
            list = enemiesByPhase[i];
            if (list != null && list.Count > 0)
                return list;
        }

        return null;
    }

    private static int PickWeightedIndex(float[] weights, int count)
    {
        if (weights == null || weights.Length == 0 || count <= 0)
            return 0;

        int safeCount = Mathf.Min(count, weights.Length);
        float total = 0f;
        for (int i = 0; i < safeCount; i++)
            total += Mathf.Max(0f, weights[i]);

        if (total <= 0.0001f)
            return safeCount - 1;

        float roll = UnityEngine.Random.value * total;
        float sum = 0f;
        for (int i = 0; i < safeCount; i++)
        {
            sum += Mathf.Max(0f, weights[i]);
            if (roll <= sum)
                return i;
        }

        return safeCount - 1;
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
            int side = UnityEngine.Random.Range(0, 4);
            switch (side)
            {
                case 0: // left
                    candidate = new Vector3(left, UnityEngine.Random.Range(bottom, top), player.position.z);
                    break;
                case 1: // right
                    candidate = new Vector3(right, UnityEngine.Random.Range(bottom, top), player.position.z);
                    break;
                case 2: // bottom
                    candidate = new Vector3(UnityEngine.Random.Range(left, right), bottom, player.position.z);
                    break;
                default: // top
                    candidate = new Vector3(UnityEngine.Random.Range(left, right), top, player.position.z);
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
        float radius = UnityEngine.Random.Range(fallbackMinRadius, fallbackMaxRadius);
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, center.z);
    }

    private static EnemyData PickWeighted(List<EnemyData> list)
    {
        int total = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            total += Mathf.Max(1, list[i].weight);
        }

        if (total <= 0)
            return list.Count > 0 ? list[0] : null;

        int roll = UnityEngine.Random.Range(0, total);
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            sum += Mathf.Max(1, list[i].weight);
            if (roll < sum)
                return list[i];
        }

        return list.Count > 0 ? list[0] : null;
    }

    private static float[] CalculateSpawnWeights(int n, float q)
    {
        if (n <= 0) return Array.Empty<float>();
        if (n == 1) return new[] { 1.0f };

        float[] weights = new float[n];

        // Most recent phase always keeps half of the probability mass.
        weights[n - 1] = 0.5f;

        float sumPrev = 0f;
        float currentPower = 1.0f;

        for (int i = n - 2; i >= 0; i--)
        {
            weights[i] = currentPower;
            sumPrev += currentPower;
            currentPower *= q;
        }

        float normalizationFactor = (sumPrev > 0.0001f) ? (0.5f / sumPrev) : 0f;
        for (int i = 0; i < n - 1; i++)
            weights[i] *= normalizationFactor;

        if (sumPrev <= 0.0001f)
            weights[n - 2] = 0.5f;

        return weights;
    }
}
