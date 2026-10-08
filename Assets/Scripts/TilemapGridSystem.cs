using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapGridSystem : MonoBehaviour
{
    public static TilemapGridSystem Instance { get; private set; }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap wallsTilemap;
    [SerializeField] private Tilemap obstaclesTilemap;

    private void Awake()
    {
        Instance = this;
    }

    public bool IsWalkable(Vector2Int gridPos)
    {
        Vector3Int cell = ToCell(gridPos);

        if (groundTilemap == null || !groundTilemap.HasTile(cell))
            return false;

        if (wallsTilemap != null && wallsTilemap.HasTile(cell))
            return false;

        if (obstaclesTilemap != null && obstaclesTilemap.HasTile(cell))
            return false;

        return true;
    }

    public bool IsObstacle(Vector2Int gridPos)
    {
        Vector3Int cell = ToCell(gridPos);

        return (wallsTilemap != null && wallsTilemap.HasTile(cell)) ||
               (obstaclesTilemap != null && obstaclesTilemap.HasTile(cell));
    }

    public bool IsInBounds(Vector2Int gridPos)
    {
        if (groundTilemap == null)
            return false;

        return groundTilemap.HasTile(ToCell(gridPos));
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return groundTilemap.GetCellCenterWorld(ToCell(gridPos));
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3Int cell = groundTilemap.WorldToCell(worldPos);
        return new Vector2Int(cell.x, cell.y);
    }

    private Vector3Int ToCell(Vector2Int gridPos)
    {
        return new Vector3Int(gridPos.x, gridPos.y, 0);
    }
}