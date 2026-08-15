using UnityEngine;

public enum CellType { Empty, Obstacle, Coin }


public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    public const int Width = 5;
    public const int Height = 5;

    [SerializeField] private int coinCount = 3;
    [SerializeField] private int obstacleCount = 3; // tweak as needed

    private CellType[,] grid = new CellType[Width, Height];

    void Awake()
    {
        Instance = this;
        InitialiseGrid();
    }

    void InitialiseGrid()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                grid[x, y] = CellType.Empty;

        PlaceRandomly(CellType.Coin, coinCount);
        PlaceRandomly(CellType.Obstacle, obstacleCount);
    }

    void PlaceRandomly(CellType type, int count)
    {
        int placed = 0;
        int safetyLimit = 200; // avoid an infinite loop if count is too high for the grid
        while (placed < count && safetyLimit-- > 0)
        {
            int x = Random.Range(0, Width);
            int y = Random.Range(0, Height);
            if (grid[x, y] == CellType.Empty)
            {
                grid[x, y] = type;
                placed++;
            }
        }
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height;
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
            grid[pos.x, pos.y] = CellType.Empty;
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

    // Coordinate system: grid <-> world space 
    // Assumes 1 Unity unit per cell, grid's (0,0) sits at the world origin.

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, 0f, gridPos.y);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
    }
}