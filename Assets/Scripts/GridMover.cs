using UnityEngine;

public enum Direction { Up, Down, Left, Right }

public class GridMover : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    [SerializeField] private Vector2Int startPosition = Vector2Int.zero;

    [SerializeField] private bool canCollectCoins = true;
    [SerializeField] private bool canCollectKeys = true; 

// new obstacle 
    [SerializeField] private float slowCooldownDuration = 1f;
    private float moveCooldownUntil = 0f;

    private bool IsOnCooldown => Time.time < moveCooldownUntil;

    void Start()
    {
        GridPosition = startPosition;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);
    }

    public bool TryMove(Direction dir)
    {
        //slowing down player
        if (IsOnCooldown)
            return false;

        // when game over, nobody should able to move
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
        {
            return false;
        }

        Vector2Int targetPos = GridPosition + DirectionToOffset(dir);

        if (!GridSystem.Instance.IsInBounds(targetPos))
            return false; // blocked: edge of the grid

        if (GridSystem.Instance.IsObstacle(targetPos))
            return false; // blocked: obstacle in the way

        GridPosition = targetPos;
        transform.position = GridSystem.Instance.GridToWorld(GridPosition);

        //slwoing down player
        if (GridSystem.Instance.IsSlow(targetPos))
        {
            moveCooldownUntil = Time.time + slowCooldownDuration;
        }

        OnEnterCell(targetPos);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckGameState();
        }

        return true;
    }

    private void OnEnterCell(Vector2Int pos)
    {
        if (canCollectCoins && GridSystem.Instance.IsCoin(pos))
        {
            GridSystem.Instance.CollectCoin(pos);
        }
        if (canCollectKeys && GridSystem.Instance.IsKey(pos))        
        {
            GridSystem.Instance.CollectKey(pos);    
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