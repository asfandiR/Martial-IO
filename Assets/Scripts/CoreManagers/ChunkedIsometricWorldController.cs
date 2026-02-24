using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Generates a finite random isometric map, caches chunk data in RAM,
// and streams 64x64 tile chunks around the player.
// Also regenerates a new random map on player death and teleports player to origin.
public class ChunkedIsometricWorldController : MonoBehaviour
{
    private const int RequiredChunkSize = 64;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Grid grid;
    [SerializeField] private Transform chunksRoot;

    [Header("Tile Source")]
    [SerializeField] private TileBase[] groundTiles;
    [SerializeField] private string tileFolderPath = "Assets/Free 32x32 Isometric Tileset Pack/Tile Palette/Palette Tiles";

    [Header("Map Size (Tiles)")]
    [SerializeField] private Vector2Int maxMapSizeTiles = new Vector2Int(512, 512);

    [Header("Chunk Streaming")]
    [SerializeField, Min(8)] private int chunkSize = 64;
    [SerializeField, Min(0)] private int activeChunkRadius = 1;
    [SerializeField, Min(0.05f)] private float chunkRefreshInterval = 0.15f;
    [SerializeField] private bool generateOnStart = true;

    [Header("Random Generation")]
    [SerializeField] private bool randomizeSeedOnStart = true;
    [SerializeField] private int seed = 12345;

    [Header("Player Death Reset")]
    [SerializeField] private bool regenerateOnPlayerDeath = true;
    [SerializeField, Min(0f)] private float respawnDelayRealtime = 0.15f;
    [SerializeField] private Vector2 respawnPosition = Vector2.zero;
    [SerializeField] private bool clearEnemiesOnRespawn = true;
    [SerializeField] private bool clearLootOnRespawn = true;
    [SerializeField] private bool resetLevelTimerOnRespawn = true;

    private readonly Dictionary<Vector2Int, ChunkData> chunkCache = new Dictionary<Vector2Int, ChunkData>();
    private readonly Dictionary<Vector2Int, Tilemap> loadedChunks = new Dictionary<Vector2Int, Tilemap>();
    private readonly List<Vector2Int> scratchChunkKeys = new List<Vector2Int>(128);

    private HealthSystem playerHealth;
    private Rigidbody2D playerRb;
    private Animator playerAnimator;
    private float nextRefreshTime;
    private bool worldInitialized;
    private bool respawnRoutineRunning;
    private Vector2Int currentPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private sealed class ChunkData
    {
        public int[] tileIndices;
        public bool hasAnyTile;
    }

    private void Awake()
    {
        chunkSize = RequiredChunkSize;
        ResolveReferences();
        EnsureGrid();
    }

    private void OnEnable()
    {
        SubscribePlayerDeath();
    }

    private void OnDisable()
    {
        UnsubscribePlayerDeath();
    }

    private void Start()
    {
        SubscribePlayerDeath();

        if (randomizeSeedOnStart)
            seed = Random.Range(int.MinValue, int.MaxValue);

        if (generateOnStart)
            GenerateNewMap();
    }

    private void Update()
    {
        if (!worldInitialized || player == null)
            return;

        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + chunkRefreshInterval;
        RefreshChunksAroundPlayer(force: false);
    }

    [ContextMenu("Generate New Map")]
    public void GenerateNewMap()
    {
        GenerateNewMap(Random.Range(int.MinValue, int.MaxValue));
    }

    public void GenerateNewMap(int newSeed)
    {
        seed = newSeed;

        EnsureGrid();
        ResolveReferences();

        chunkCache.Clear();
        UnloadAllChunks();

        worldInitialized = true;
        currentPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
        RefreshChunksAroundPlayer(force: true);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                player = playerGo.transform;
        }

        if (player != null)
        {
            if (playerHealth == null)
                playerHealth = player.GetComponent<HealthSystem>();
            if (playerRb == null)
                playerRb = player.GetComponent<Rigidbody2D>();
            if (playerAnimator == null)
                playerAnimator = player.GetComponentInChildren<Animator>();
        }
    }

    private void EnsureGrid()
    {
        chunkSize = RequiredChunkSize;

        if (grid == null)
            grid = GetComponentInChildren<Grid>();

        if (grid == null)
        {
            GameObject gridGo = new GameObject("ProceduralIsometricGrid");
            gridGo.transform.SetParent(transform, false);
            grid = gridGo.AddComponent<Grid>();
        }

        grid.cellSize = new Vector3(1f, 0.5f, 1f);
        grid.cellGap = Vector3.zero;

        if (grid.cellLayout != GridLayout.CellLayout.Isometric)
            Debug.LogWarning("[ChunkedIsometricWorldController] Assigned Grid is not Isometric. Use an Isometric Grid (e.g. Sample Grid from the tileset pack).", this);

        if (chunksRoot == null)
        {
            Transform found = grid.transform.Find("Chunks");
            if (found != null)
            {
                chunksRoot = found;
            }
            else
            {
                GameObject root = new GameObject("Chunks");
                root.transform.SetParent(grid.transform, false);
                chunksRoot = root.transform;
            }
        }
    }

    private void SubscribePlayerDeath()
    {
        ResolveReferences();
        if (!regenerateOnPlayerDeath || playerHealth == null)
            return;

        playerHealth.OnDeath -= HandlePlayerDeath;
        playerHealth.OnDeath += HandlePlayerDeath;
    }

    private void UnsubscribePlayerDeath()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        if (!regenerateOnPlayerDeath || respawnRoutineRunning)
            return;

        StartCoroutine(RespawnAndRegenerateRoutine());
    }

    private IEnumerator RespawnAndRegenerateRoutine()
    {
        respawnRoutineRunning = true;

        if (respawnDelayRealtime > 0f)
            yield return new WaitForSecondsRealtime(respawnDelayRealtime);
        else
            yield return null;

        ClearRuntimeActorsForRespawn();
        RespawnPlayerAtOrigin();

        if (resetLevelTimerOnRespawn && LevelManager.Instance != null)
            LevelManager.Instance.ResetLevel();

        GenerateNewMap();

        if (GameManager.Instance != null)
            GameManager.Instance.StartGameplay();

        respawnRoutineRunning = false;
    }

    private void RespawnPlayerAtOrigin()
    {
        if (player == null)
            return;

        Vector3 pos = player.position;
        pos.x = respawnPosition.x;
        pos.y = respawnPosition.y;
        player.position = pos;

        if (playerRb != null)
            playerRb.linearVelocity = Vector2.zero;

        if (playerHealth != null && playerHealth.IsDead)
            playerHealth.Revive(playerHealth.MaxHp);

        if (playerAnimator != null)
        {
            playerAnimator.SetBool(IsDeadHash, false);
            playerAnimator.SetFloat(RunHash, 0f);
        }
    }

    private void ClearRuntimeActorsForRespawn()
    {
        if (clearEnemiesOnRespawn)
        {
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) continue;
                ReturnOrDestroy(enemies[i].gameObject);
            }
        }

        if (clearLootOnRespawn)
        {
            XPGem[] gems = FindObjectsByType<XPGem>(FindObjectsSortMode.None);
            for (int i = 0; i < gems.Length; i++)
            {
                if (gems[i] == null) continue;
                ReturnOrDestroy(gems[i].gameObject);
            }

            RelicPickup[] relics = FindObjectsByType<RelicPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < relics.Length; i++)
            {
                if (relics[i] == null) continue;
                ReturnOrDestroy(relics[i].gameObject);
            }

            EffectorPickup[] effectors = FindObjectsByType<EffectorPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < effectors.Length; i++)
            {
                if (effectors[i] == null) continue;
                ReturnOrDestroy(effectors[i].gameObject);
            }
        }
    }

    private static void ReturnOrDestroy(GameObject go)
    {
        if (go == null) return;

        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(go);
        else
            Destroy(go);
    }

    private void RefreshChunksAroundPlayer(bool force)
    {
        if (player == null || grid == null || chunksRoot == null)
            return;

        Vector3Int cell = grid.WorldToCell(player.position);
        Vector2Int playerChunk = WorldCellToChunk(cell.x, cell.y);

        if (!force && playerChunk == currentPlayerChunk)
            return;

        currentPlayerChunk = playerChunk;

        for (int y = -activeChunkRadius; y <= activeChunkRadius; y++)
        {
            for (int x = -activeChunkRadius; x <= activeChunkRadius; x++)
            {
                Vector2Int coord = new Vector2Int(playerChunk.x + x, playerChunk.y + y);
                if (!DoesChunkIntersectMap(coord))
                    continue;

                EnsureChunkLoaded(coord);
            }
        }

        scratchChunkKeys.Clear();
        foreach (KeyValuePair<Vector2Int, Tilemap> kv in loadedChunks)
            scratchChunkKeys.Add(kv.Key);

        int maxDist = activeChunkRadius;
        for (int i = 0; i < scratchChunkKeys.Count; i++)
        {
            Vector2Int key = scratchChunkKeys[i];
            if (Mathf.Abs(key.x - playerChunk.x) <= maxDist && Mathf.Abs(key.y - playerChunk.y) <= maxDist)
                continue;

            UnloadChunk(key);
        }
    }

    private void EnsureChunkLoaded(Vector2Int coord)
    {
        if (loadedChunks.ContainsKey(coord))
            return;

        ChunkData data = GetOrCreateChunkData(coord);
        if (data == null || !data.hasAnyTile)
            return;

        GameObject chunkGo = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunkGo.transform.SetParent(chunksRoot, false);
        chunkGo.transform.localPosition = grid.CellToLocal(new Vector3Int(coord.x * chunkSize, coord.y * chunkSize, 0));

        Tilemap tilemap = chunkGo.AddComponent<Tilemap>();
        TilemapRenderer renderer = chunkGo.AddComponent<TilemapRenderer>();
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;

        BoundsInt bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);
        TileBase[] tiles = new TileBase[chunkSize * chunkSize];

        for (int i = 0; i < data.tileIndices.Length && i < tiles.Length; i++)
        {
            int tileIndex = data.tileIndices[i];
            if (tileIndex < 0 || groundTiles == null || tileIndex >= groundTiles.Length)
                continue;

            tiles[i] = groundTiles[tileIndex];
        }

        tilemap.SetTilesBlock(bounds, tiles);
        tilemap.CompressBounds();

        loadedChunks[coord] = tilemap;
    }

    private ChunkData GetOrCreateChunkData(Vector2Int coord)
    {
        if (chunkCache.TryGetValue(coord, out ChunkData data))
            return data;

        data = GenerateChunkData(coord);
        chunkCache[coord] = data;
        return data;
    }

    private ChunkData GenerateChunkData(Vector2Int coord)
    {
        ChunkData data = new ChunkData
        {
            tileIndices = new int[chunkSize * chunkSize],
            hasAnyTile = false
        };

        for (int i = 0; i < data.tileIndices.Length; i++)
            data.tileIndices[i] = -1;

        if (groundTiles == null || groundTiles.Length == 0)
            return data;

        int worldOriginX = coord.x * chunkSize;
        int worldOriginY = coord.y * chunkSize;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int wx = worldOriginX + x;
                int wy = worldOriginY + y;
                if (!IsInsideMap(wx, wy))
                    continue;

                int idx = x + y * chunkSize;
                data.tileIndices[idx] = SelectTileIndex(wx, wy);
                data.hasAnyTile = true;
            }
        }

        return data;
    }

    private int SelectTileIndex(int wx, int wy)
    {
        uint hash = Hash2D(wx, wy, seed);

        // Use a bit of coherent noise + hash to avoid overly uniform randomness.
        float noise = Mathf.PerlinNoise(
            (wx + (seed * 0.0137f)) * 0.08f,
            (wy - (seed * 0.0211f)) * 0.08f
        );

        int baseIndex = (int)(noise * groundTiles.Length);
        baseIndex = Mathf.Clamp(baseIndex, 0, groundTiles.Length - 1);

        if ((hash & 7u) == 0u)
            baseIndex = (int)(hash % (uint)groundTiles.Length);

        return baseIndex;
    }

    private bool IsInsideMap(int wx, int wy)
    {
        Vector2Int size = GetSanitizedMapSize();

        int minX = -size.x / 2;
        int minY = -size.y / 2;
        int maxXExclusive = minX + size.x;
        int maxYExclusive = minY + size.y;

        return wx >= minX && wx < maxXExclusive && wy >= minY && wy < maxYExclusive;
    }

    private bool DoesChunkIntersectMap(Vector2Int coord)
    {
        Vector2Int size = GetSanitizedMapSize();
        int minX = -size.x / 2;
        int minY = -size.y / 2;
        int maxXExclusive = minX + size.x;
        int maxYExclusive = minY + size.y;

        int chunkMinX = coord.x * chunkSize;
        int chunkMinY = coord.y * chunkSize;
        int chunkMaxXExclusive = chunkMinX + chunkSize;
        int chunkMaxYExclusive = chunkMinY + chunkSize;

        bool overlapsX = chunkMaxXExclusive > minX && chunkMinX < maxXExclusive;
        bool overlapsY = chunkMaxYExclusive > minY && chunkMinY < maxYExclusive;
        return overlapsX && overlapsY;
    }

    private Vector2Int WorldCellToChunk(int x, int y)
    {
        return new Vector2Int(FloorDiv(x, chunkSize), FloorDiv(y, chunkSize));
    }

    private static int FloorDiv(int value, int divisor)
    {
        int q = value / divisor;
        int r = value % divisor;
        if (r != 0 && ((r < 0) ^ (divisor < 0)))
            q--;
        return q;
    }

    private Vector2Int GetSanitizedMapSize()
    {
        return new Vector2Int(
            Mathf.Max(chunkSize, maxMapSizeTiles.x),
            Mathf.Max(chunkSize, maxMapSizeTiles.y)
        );
    }

    private void OnValidate()
    {
        chunkSize = RequiredChunkSize;
        activeChunkRadius = Mathf.Max(0, activeChunkRadius);
        chunkRefreshInterval = Mathf.Max(0.05f, chunkRefreshInterval);
        maxMapSizeTiles.x = Mathf.Max(RequiredChunkSize, maxMapSizeTiles.x);
        maxMapSizeTiles.y = Mathf.Max(RequiredChunkSize, maxMapSizeTiles.y);
    }

    private void UnloadChunk(Vector2Int coord)
    {
        if (!loadedChunks.TryGetValue(coord, out Tilemap tilemap))
            return;

        loadedChunks.Remove(coord);

        if (tilemap != null)
            Destroy(tilemap.gameObject);
    }

    private void UnloadAllChunks()
    {
        scratchChunkKeys.Clear();
        foreach (KeyValuePair<Vector2Int, Tilemap> kv in loadedChunks)
            scratchChunkKeys.Add(kv.Key);

        for (int i = 0; i < scratchChunkKeys.Count; i++)
            UnloadChunk(scratchChunkKeys[i]);
    }

    private static uint Hash2D(int x, int y, int s)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)x) * 16777619u;
            h = (h ^ (uint)y) * 16777619u;
            h = (h ^ (uint)s) * 16777619u;
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Load Tiles From Pack Folder")]
    private void LoadTilesFromPackFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:Tile", new[] { tileFolderPath });
        List<TileBase> tiles = new List<TileBase>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null)
                tiles.Add(tile);
        }

        tiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        groundTiles = tiles.ToArray();

        EditorUtility.SetDirty(this);
        Debug.Log($"[ChunkedIsometricWorldController] Loaded {groundTiles.Length} tiles from: {tileFolderPath}", this);
    }
#endif
}
