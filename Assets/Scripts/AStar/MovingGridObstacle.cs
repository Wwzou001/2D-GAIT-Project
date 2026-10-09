using UnityEngine;

/// <summary>
/// Simple grid-based moving obstacle used to demonstrate dynamic A* replanning.
///
/// Environment/parameters:
/// - pointA / pointB: the two grid cells the obstacle travels between.
/// - moveInterval: seconds between grid movement attempts.
/// - The mover performs the same bounds/fixed-obstacle checks as other grid entities.
///
/// Works with either mover type: a GridMover (original fixed-grid scenes) or a
/// TilemapGridMover (tilemap scenes). If an object has both, the tilemap one is used,
/// so the obstacle is never moved twice. In a tilemap scene you can remove the old
/// GridMover from the object.
///
/// Then drag the obstacle's mover into AStarDemoController (movingObstacles or
/// tilemapMovingObstacles) and FSMEnemyController (movingObstacles) so they avoid it.
/// </summary>
public class MovingGridObstacle : MonoBehaviour
{
    [Header("Movement Environment")]
    [SerializeField] private Vector2Int pointA = new Vector2Int(2, 2);
    [SerializeField] private Vector2Int pointB = new Vector2Int(2, 4);

    [Header("Movement Parameter")]
    [SerializeField] private float moveInterval = 0.75f;

    private GridMover gridMover;
    private TilemapGridMover tilemapMover;
    private Vector2Int target;
    private float moveTimer;
    private int refusedMoves;
    private float nextWarningTime;

    private Vector2Int CurrentCell =>
        tilemapMover != null ? tilemapMover.GridPosition : gridMover.GridPosition;

    private void Awake()
    {
        tilemapMover = GetComponent<TilemapGridMover>();
        gridMover = GetComponent<GridMover>();
        target = pointB;

        if (tilemapMover == null && gridMover == null)
        {
            Debug.LogError(
                "MovingGridObstacle: " + gameObject.name +
                " has neither a TilemapGridMover nor a GridMover, so it cannot move.",
                this);
        }
    }

    private void Start()
    {
        WarnIfPointIsBlocked(pointA, "Point A");
        WarnIfPointIsBlocked(pointB, "Point B");
    }

    private void Update()
    {
        if (tilemapMover == null && gridMover == null)
            return;

        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        moveTimer += Time.deltaTime;
        if (moveTimer < moveInterval)
            return;

        moveTimer = 0f;

        if (CurrentCell == target)
            target = target == pointA ? pointB : pointA;

        MoveOneStepToward(target);
    }

    private void MoveOneStepToward(Vector2Int destination)
    {
        Vector2Int from = CurrentCell;
        Vector2Int difference = destination - from;
        Direction direction;

        if (difference.x > 0) direction = Direction.Right;
        else if (difference.x < 0) direction = Direction.Left;
        else if (difference.y > 0) direction = Direction.Up;
        else if (difference.y < 0) direction = Direction.Down;
        else return;

        if (tilemapMover != null) tilemapMover.TryMove(direction);
        else gridMover.TryMove(direction);

        // Say so if it is stuck, instead of silently standing still.
        if (CurrentCell != from)
        {
            refusedMoves = 0;
            return;
        }

        refusedMoves++;

        if (refusedMoves >= 4 && Time.time >= nextWarningTime)
        {
            nextWarningTime = Time.time + 5f;
            Debug.LogWarning(
                $"MovingGridObstacle: cannot step {direction} from {from} towards {destination}. " +
                "A wall is in the way, or the mover is disabled. The obstacle walks in a straight " +
                "line (horizontal first), so Point A and Point B need a clear route between them " +
                "from where it starts.",
                this);
        }
    }

    // Tells you if a point is inside a wall or off the map, which would block the obstacle.
    private void WarnIfPointIsBlocked(Vector2Int cell, string label)
    {
        bool blocked = false;

        if (tilemapMover != null && TilemapGridSystem.Instance != null)
        {
            blocked = !TilemapGridSystem.Instance.IsWalkable(cell);
        }
        else if (GridSystem.Instance != null)
        {
            blocked = !GridSystem.Instance.IsInBounds(cell) || GridSystem.Instance.IsObstacle(cell);
        }

        if (blocked)
        {
            Debug.LogWarning(
                $"MovingGridObstacle: {label} {cell} is not a walkable cell, so the obstacle " +
                "cannot reach it. Pick a cell on the floor.",
                this);
        }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        if (tilemapMover != null && TilemapGridSystem.Instance != null)
            return (Vector3)TilemapGridSystem.Instance.GridToWorld(cell);

        if (GridSystem.Instance != null)
            return (Vector3)GridSystem.Instance.GridToWorld(cell);

        if (TilemapGridSystem.Instance != null)
            return (Vector3)TilemapGridSystem.Instance.GridToWorld(cell);

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null && TilemapGridSystem.Instance == null)
            return;

        Gizmos.DrawWireCube(CellToWorld(pointA), Vector3.one * 0.35f);
        Gizmos.DrawWireCube(CellToWorld(pointB), Vector3.one * 0.35f);
        Gizmos.DrawLine(CellToWorld(pointA), CellToWorld(pointB));
    }
}