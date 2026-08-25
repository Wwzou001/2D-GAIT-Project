using UnityEngine;
using System;

public enum CellType { Empty, Obstacle, Coin }


public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;

    public int Width => width;
    public int Height => height;

    [SerializeField] private int coinCount = 3;
    [SerializeField] private int obstacleCount = 2; // tweak as needed

    [SerializeField] private Vector2Int playerStart = new Vector2Int(0, 0);
    [SerializeField] private Vector2Int npcStart = new Vector2Int(4, 4);

    private CellType[,] grid;

    public event Action<Vector2Int> CoinCollected;

    void Awake()
    {
        Instance = this;
        grid = new CellType[width, height];
        InitialiseGrid();
    }

    void InitialiseGrid()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                grid[x, y] = CellType.Empty;

        PlaceRandomly(CellType.Coin, coinCount, isObstacle: false);
        PlaceRandomly(CellType.Obstacle, obstacleCount, isObstacle: true);
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
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Obstacle;
    }

    public bool IsCoin(Vector2Int pos)
    {
        return IsInBounds(pos) && grid[pos.x, pos.y] == CellType.Coin;
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