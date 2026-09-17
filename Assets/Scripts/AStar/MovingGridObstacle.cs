using UnityEngine;

/// <summary>
/// Simple grid-based moving obstacle used to demonstrate dynamic A* replanning.
///
/// Environment/parameters:
/// - pointA / pointB: the two grid cells the obstacle travels between.
/// - moveInterval: seconds between grid movement attempts.
/// - GridMover performs the same bounds/fixed-obstacle checks as other grid entities.
///
/// Add this component and GridMover to an obstacle GameObject, then drag that
/// GridMover into AStarDemoController.movingObstacles (and FSMEnemyController's
/// movingObstacles if the FSM should also avoid it).
/// </summary>
[RequireComponent(typeof(GridMover))]
public class MovingGridObstacle : MonoBehaviour
{
    [Header("Movement Environment")]
    [SerializeField] private Vector2Int pointA = new Vector2Int(2, 2);
    [SerializeField] private Vector2Int pointB = new Vector2Int(2, 4);

    [Header("Movement Parameter")]
    [SerializeField] private float moveInterval = 0.75f;

    private GridMover mover;
    private Vector2Int target;
    private float moveTimer;

    private void Awake()
    {
        mover = GetComponent<GridMover>();
        target = pointB;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        moveTimer += Time.deltaTime;
        if (moveTimer < moveInterval)
            return;

        moveTimer = 0f;

        if (mover.GridPosition == target)
            target = target == pointA ? pointB : pointA;

        MoveOneStepToward(target);
    }

    private void MoveOneStepToward(Vector2Int destination)
    {
        Vector2Int difference = destination - mover.GridPosition;
        Direction direction;

        if (difference.x > 0) direction = Direction.Right;
        else if (difference.x < 0) direction = Direction.Left;
        else if (difference.y > 0) direction = Direction.Up;
        else if (difference.y < 0) direction = Direction.Down;
        else return;

        mover.TryMove(direction);
    }

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null)
            return;

        Gizmos.DrawWireCube(GridSystem.Instance.GridToWorld(pointA), Vector3.one * 0.35f);
        Gizmos.DrawWireCube(GridSystem.Instance.GridToWorld(pointB), Vector3.one * 0.35f);
        Gizmos.DrawLine(
            GridSystem.Instance.GridToWorld(pointA),
            GridSystem.Instance.GridToWorld(pointB));
    }
}
