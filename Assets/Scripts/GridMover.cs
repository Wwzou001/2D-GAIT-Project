using UnityEngine;

public enum Direction { Up, Down, Left, Right }

public class GridMover : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    [SerializeField] private Vector2Int startPosition = Vector2Int.zero;

    void Start()
    {
        GridPosition = startPosition;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);
    }

    public bool TryMove(Direction dir)
    {
        Vector2Int targetPos = GridPosition + DirectionToOffset(dir);

        if (!GridSystem.Instance.IsInBounds(targetPos))
            return false; // blocked: edge of the grid

        if (GridSystem.Instance.IsObstacle(targetPos))
            return false; // blocked: obstacle in the way

        GridPosition = targetPos;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);

        OnEnterCell(targetPos);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckGameState();
        }

        return true;
    }

    private void OnEnterCell(Vector2Int pos)
    {
        if (GridSystem.Instance.IsCoin(pos))
        {
            GridSystem.Instance.CollectCoin(pos);
        }
    }

    private Vector2Int DirectionToOffset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(0, 1);
            case Direction.Down: return new Vector2Int(0, -1);
            case Direction.Left: return new Vector2Int(-1, 0);
            case Direction.Right: return new Vector2Int(1, 0);
            default: return Vector2Int.zero;
        }
    }
}