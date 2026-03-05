using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Generates a finite random isometric map at once.
public class ChunkedIsometricWorldController : MonoBehaviour
{
    [System.Serializable]
    private class BiomeTileSet
    {
        public string biomeName = "Biome";
        public TileBase earthTile;
        public TileBase mudTile;
        public TileBase grassTile;

        public bool IsConfigured =>
            earthTile != null &&
            mudTile != null &&
            grassTile != null;
    }

    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Transform mapRoot;

    [Header("Tile Source")]
    [SerializeField] private TileBase[] groundTiles;
    [SerializeField] private string tileFolderPath = "Assets/Free 32x32 Isometric Tileset Pack/Tile Palette/Palette Tiles";

    [Header("Biome Tiles (3 biomes x 3 tiles)")]
    [SerializeField] private BiomeTileSet[] biomes = new BiomeTileSet[3];

    [Header("Biome Noise")]
    [SerializeField, Min(0.001f)] private float biomeNoiseScale = 0.0125f;
    [SerializeField, Range(0f, 0.49f)] private float biomeBlendWidth = 0.18f;

    [Header("Biome Detail Noise")]
    [SerializeField, Min(0.001f)] private float biomeDetailNoiseScale = 0.08f;
    [SerializeField, Range(0f, 1f)] private float mudThreshold = 0.38f;
    [SerializeField, Range(0f, 1f)] private float grassThreshold = 0.7f;

    [Header("Map Size (Tiles)")]
    [SerializeField] private Vector2Int maxMapSizeTiles = new Vector2Int(512, 512);

    [Header("Generation Settings")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Random Generation")]
    [SerializeField] private bool randomizeSeedOnStart = true;
    [SerializeField] private int seed = 12345;

    private bool worldInitialized;

    private void Awake()
    {
        EnsureGrid();
    }

    private void Start()
    {
        if (randomizeSeedOnStart)
            seed = Random.Range(int.MinValue, int.MaxValue);

        if (generateOnStart)
            GenerateNewMap();
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
        GenerateFullMap();

        worldInitialized = true;
    }

    private void EnsureGrid()
    {
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

        if (mapRoot == null)
        {
            Transform found = grid.transform.Find("MapRoot");
            if (found != null)
            {
                mapRoot = found;
            }
            else
            {
                GameObject root = new GameObject("MapRoot");
                root.transform.SetParent(grid.transform, false);
                mapRoot = root.transform;
            }
        }
    }


    private void GenerateFullMap()
    {
        bool hasBiomeTiles = GetConfiguredBiomeCount() > 0;
        bool hasFallbackTiles = groundTiles != null && groundTiles.Length > 0;

        if (mapRoot == null || (!hasBiomeTiles && !hasFallbackTiles))
            return;

        // Clear old map
        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(mapRoot.GetChild(i).gameObject);
            else
                DestroyImmediate(mapRoot.GetChild(i).gameObject);
        }

        GameObject mapGo = new GameObject("FullMap");
        mapGo.transform.SetParent(mapRoot, false);
        mapGo.transform.localPosition = Vector3.zero;

        Tilemap tilemap = mapGo.AddComponent<Tilemap>();
        TilemapRenderer renderer = mapGo.AddComponent<TilemapRenderer>();
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        renderer.sortingOrder = -2;

        int width = maxMapSizeTiles.x;
        int height = maxMapSizeTiles.y;
        int minX = -width / 2;
        int minY = -height / 2;

        BoundsInt bounds = new BoundsInt(minX, minY, 0, width, height, 1);
        TileBase[] tiles = new TileBase[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int wx = minX + x;
                int wy = minY + y;
                
                tiles[x + y * width] = SelectTile(wx, wy);
            }
        }

        tilemap.SetTilesBlock(bounds, tiles);
        tilemap.CompressBounds();
    }

    private TileBase SelectTile(int wx, int wy)
    {
        if (TrySelectBiomeTile(wx, wy, out TileBase biomeTile))
            return biomeTile;

        if (groundTiles == null || groundTiles.Length == 0)
            return null;

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

        return groundTiles[baseIndex];
    }

    private bool TrySelectBiomeTile(int wx, int wy, out TileBase tile)
    {
        tile = null;

        int biomeCount = GetConfiguredBiomeCount();
        if (biomeCount < 1)
            return false;

        float biomeNoise = Mathf.PerlinNoise(
            (wx + seed * 0.0013f) * biomeNoiseScale,
            (wy - seed * 0.0017f) * biomeNoiseScale
        );

        float biomeCoord = biomeNoise * biomeCount;
        int primaryIndex = Mathf.Clamp(Mathf.FloorToInt(biomeCoord), 0, biomeCount - 1);
        float frac = biomeCoord - Mathf.Floor(biomeCoord);

        int selectedBiome = primaryIndex;

        // Blend into the next biome near the edge of the current biome band.
        if (primaryIndex < biomeCount - 1 && frac > 1f - biomeBlendWidth && biomeBlendWidth > 0f)
        {
            float t = Mathf.InverseLerp(1f - biomeBlendWidth, 1f, frac);
            t = t * t * (3f - 2f * t); // smoothstep

            uint edgeHash = Hash2D(wx * 31, wy * 31, seed ^ 0x4F1BBCDC);
            float random01 = (edgeHash & 1023u) / 1023f;
            if (random01 < t)
                selectedBiome = primaryIndex + 1;
        }

        BiomeTileSet biome = biomes[selectedBiome];
        if (biome == null || !biome.IsConfigured)
            return false;

        float detailNoise = Mathf.PerlinNoise(
            (wx - seed * 0.0091f) * biomeDetailNoiseScale,
            (wy + seed * 0.0067f) * biomeDetailNoiseScale
        );

        float mudCutoff = Mathf.Clamp01(mudThreshold);
        float grassCutoff = Mathf.Max(mudCutoff, Mathf.Clamp01(grassThreshold));

        if (detailNoise < mudCutoff)
            tile = biome.mudTile;
        else if (detailNoise < grassCutoff)
            tile = biome.earthTile;
        else
            tile = biome.grassTile;

        return tile != null;
    }

    private int GetConfiguredBiomeCount()
    {
        if (biomes == null || biomes.Length == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] != null && biomes[i].IsConfigured)
                count++;
            else
                break;
        }

        return count;
    }

    private void OnValidate()
    {
        maxMapSizeTiles.x = Mathf.Max(16, maxMapSizeTiles.x);
        maxMapSizeTiles.y = Mathf.Max(16, maxMapSizeTiles.y);
        grassThreshold = Mathf.Max(mudThreshold, grassThreshold);
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
