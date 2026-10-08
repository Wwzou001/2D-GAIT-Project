using UnityEngine;

public class TilemapGridMover : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    private void Start()
    {
        if (TilemapGridSystem.Instance == null)
        {
            Debug.LogError("TilemapGridSystem not found.");
            return;
        }

        // Use wherever the character was placed in the Scene
        GridPosition =
            TilemapGridSystem.Instance.WorldToGrid(transform.position);

        // Snap character to the centre of that tile
        transform.position =
            TilemapGridSystem.Instance.GridToWorld(GridPosition);
    }

    public bool TryMove(Direction dir)
    {
        Vector2Int targetPos =
            GridPosition + DirectionToOffset(dir);

        // Don't move if there is no walkable ground there
        if (!TilemapGridSystem.Instance.IsWalkable(targetPos))
            return false;

        GridPosition = targetPos;

        transform.position =
            TilemapGridSystem.Instance.GridToWorld(GridPosition);

        if (GameManager.Instance != null)
            GameManager.Instance.CheckGameState();

        return true;
    }

    private Vector2Int DirectionToOffset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up:
                return new Vector2Int(0, 1);

            case Direction.Down:
                return new Vector2Int(0, -1);

            case Direction.Left:
                return new Vector2Int(-1, 0);

            case Direction.Right:
                return new Vector2Int(1, 0);

            default:
                return Vector2Int.zero;
        }
    }
}