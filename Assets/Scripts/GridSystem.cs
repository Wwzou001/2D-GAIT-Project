using UnityEngine;
using System;
using System.Collections.Generic;

public enum CellType { Empty, Obstacle, Coin, Fountain, Slow, Key }


public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;

    public int Width => width;
    public int Height => height;

    [SerializeField] private int coinCount = 3;
    [SerializeField] private int obstacleCount = 2; // tweak as needed

    [SerializeField] private int fountainCount = 0; // default off, only MCTS need to change value

    [SerializeField] private int keyCount = 1; // key

    // Confirm character position rule, player bottom left, enemy top right
    public Vector2Int playerStart => new Vector2Int(0, 0);
    public Vector2Int npcStart => new Vector2Int(width - 1, height - 1);

    private CellType[,] grid;
    private List<Vector2Int> fountainPositions = new List<Vector2Int>();
    private List<GameObject> spawnedObstacleColliders = new List<GameObject>();

    public event Action<Vector2Int> CoinCollected;
    public event Action<Vector2Int> KeyCollected; 

    public int TotalKeys => keyCount;

// for slowing player down
    public int slowCount = 2; 

    void Awake()
    {
        Instance = this;
        grid = new CellType[width, height];
        InitialiseGrid();
    }

    private void SpawnObstacleColliders()
    {
        // Clean up old obstacle when regenerate layout
        foreach (GameObject go in spawnedObstacleColliders)
        {
            if (go != null) Destroy(go);
        }
        spawnedObstacleColliders.Clear();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (IsObstacle(pos))
                {
                    GameObject go = new GameObject($"ObstacleCollider_{x}_{y}");
                    go.transform.position = GridToWorld(pos);

                    Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Static;

                    BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(0.9f, 0.9f);
                    go.layer = LayerMask.NameToLayer("Obstacles");  
                }
            }
        }
    }

    void InitialiseGrid(bool spawnColliders = true)
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                grid[x, y] = CellType.Empty;

        fountainPositions.Clear();

        PlaceRandomly(CellType.Coin, coinCount, isObstacle: false);
        PlaceRandomly(CellType.Obstacle, obstacleCount, isObstacle: true);
        PlaceFountains(fountainCount);
        PlaceRandomly(CellType.Slow, slowCount, isObstacle: false);  // new obstacle
        PlaceRandomly(CellType.Key, keyCount, isObstacle: false); // key

        if (spawnColliders)
        {
            SpawnObstacleColliders();
        }
    }

    private void EnsureInitialisedForEditMode()
    {
        if (grid == null)
        {
            grid = new CellType[width, height];
        }
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void RegenerateLayout(bool spawnColliders = true)
    {
        EnsureInitialisedForEditMode();
        InitialiseGrid(spawnColliders);
    }

    void PlaceRandomly(CellType type, int count, bool isObstacle)
    {
        int placed = 0;
        int safetyLimit = 200; // avoid an infinite loop if count is too high for the grid
        while (placed < count && safetyLimit-- > 0)
        {
            int x = UnityEngine.Random.Range(0, width);
            int y = UnityEngine.Random.Range(0, height);
            Vector2Int pos = new Vector2Int(x, y);
            if (grid[x, y] != CellType.Empty)
            {
                continue;
            }

            if (isObstacle && (IsAdjacentToStart(pos, playerStart) || IsAdjacentToStart(pos, npcStart)))
                continue;

            if (!isObstacle && (pos == playerStart) || (pos == npcStart) )
                continue;

            grid[x, y] = type;
            placed++;
        }
    }

    void PlaceFountains(int count)
    {
        int placed = 0;
        int safetyLimit = 300;
        int minSpacing = Mathf.Max(width, height) / 2;

        while (placed < count && safetyLimit -- > 0)
        {
            int x = UnityEngine.Random.Range(0, width);
            int y = UnityEngine.Random.Range(0, height);
            Vector2Int pos = new Vector2Int(x, y);

            if (grid[x, y] != CellType.Empty) continue;

            if (IsAdjacentToStart(pos, playerStart) || IsAdjacentToStart(pos, npcStart)) continue;

            bool tooCloseToAnotherFountain = false;
            foreach (Vector2Int existing in fountainPositions)
            {
                int dx = Mathf.Abs(pos.x - existing.x);
                int dy = Mathf.Abs(pos.y - existing.y);
                int manhattanDistance = dx + dy;
                if (manhattanDistance < minSpacing)
                {
                    tooCloseToAnotherFountain = true;
                    break;
                }
            }
            if (tooCloseToAnotherFountain) continue;

            grid[x, y] = CellType.Fountain;
            fountainPositions.Add(pos);
            placed++;
        }
    }

    bool IsAdjacentToStart(Vector2Int pos, Vector2Int start)
    {
        if (pos == start) return true;
        int dx = Mathf.Abs(pos.x - start.x);
        int dy = Mathf.Abs(pos.y - start.y);
        return (dx + dy) == 1;
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public bool IsObstacle(Vector2Int pos)
    {
        if (!IsInBounds(pos)) return false;
        CellType cell = grid[pos.x, pos.y];
        return cell == CellType.Obstacle || cell == CellType.Fountain;
    }

    public bool IsCoin(Vector2Int pos)
    {
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Coin;
    }

    public bool IsFountain(Vector2Int pos)
    {
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Fountain;
    }

    public bool IsKey(Vector2Int pos)
    {
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Key;
    }

    public void CollectKey(Vector2Int pos)
    {
        if (IsKey(pos))
            grid[pos.x, pos.y] = CellType.Empty;
            KeyCollected?.Invoke(pos);   
    }

    // Check if any fountain within surround 8 cells
    public bool IsNearFountain(Vector2Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // skip cell itself
                Vector2Int neighbour = new Vector2Int(pos.x + dx, pos.y + dy);
                if (IsFountain(neighbour)) return true;
            }
        }
        return false;
    }

    //new obstacle
    public bool IsSlow(Vector2Int pos)
    {
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Slow;
    }

    public void CollectCoin(Vector2Int pos)
    {
        if (IsCoin(pos))
        { 
            grid[pos.x, pos.y] = CellType.Empty;
            CoinCollected?.Invoke(pos);
        }
    }

    // All coins collected = win
    public int RemainingCoins()
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (grid[x, y] == CellType.Coin) count++;
        return count;
    }

    public int RemainingKeys()
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (grid[x, y] == CellType.Key) count++;
        return count;
    }

    // How many coins this grid was configured to place - useful for UI
    public int TotalCoins => coinCount;

    // Coordinate system: grid <-> world space 
    // Assumes 1 Unity unit per cell, grid's (0,0) sits at the world origin.

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, gridPos.y, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }
}