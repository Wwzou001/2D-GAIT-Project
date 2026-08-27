using UnityEngine;


/// Sprint 2 FSM skeleton.
/// The enemy switches between PATROL and CHASE according to
/// Manhattan distance from the player.


[RequireComponent(typeof(GridMover))]
public class FSMEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase
    }

    [Header("References")]
    [SerializeField] private GridMover player;

    [Header("FSM Settings")]
    [SerializeField] private int chaseDistance = 3;
    [SerializeField] private float moveInterval = 0.5f;

    [Header("Patrol")]
    [SerializeField] private Vector2Int patrolPointA = new Vector2Int(4, 4);
    [SerializeField] private Vector2Int patrolPointB = new Vector2Int(0, 4);

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private GridMover mover;
    private EnemyState currentState = EnemyState.Patrol;
    private Vector2Int currentPatrolTarget;
    private float moveTimer;

    public EnemyState CurrentState => currentState;

    private void Awake()
    {
        mover = GetComponent<GridMover>();
        currentPatrolTarget = patrolPointA;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        if (player == null)
            return;

        UpdateState();

        moveTimer += Time.deltaTime;
        if (moveTimer >= moveInterval)
        {
            moveTimer = 0f;
            ExecuteCurrentState();
        }
    }

    private void UpdateState()
    {
        int distanceToPlayer = ManhattanDistance(
            mover.GridPosition,
            player.GridPosition);

        EnemyState nextState =
            distanceToPlayer <= chaseDistance
            ? EnemyState.Chase
            : EnemyState.Patrol;

        if (nextState != currentState)
        {
            EnemyState previousState = currentState;
            currentState = nextState;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"FSM: {previousState} -> {currentState}. " +
                    $"Player distance = {distanceToPlayer}");
            }
        }
    }

    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                Patrol();
                break;

            case EnemyState.Chase:
                Chase();
                break;
        }
    }

    private void Patrol()
    {
        if (mover.GridPosition == currentPatrolTarget)
        {
            currentPatrolTarget =
                currentPatrolTarget == patrolPointA
                ? patrolPointB
                : patrolPointA;

            if (showDebugLogs)
                Debug.Log($"FSM Patrol: new target = {currentPatrolTarget}");
        }

        MoveOneStepTowards(currentPatrolTarget);
    }

    private void Chase()
    {
        MoveOneStepTowards(player.GridPosition);
    }

    private void MoveOneStepTowards(Vector2Int target)
    {
        Vector2Int current = mover.GridPosition;
        Vector2Int difference = target - current;

        // This skeleton deliberately keeps navigation simple.
        // A later/full version can request an A* path instead.
        if (difference.x != 0)
        {
            Direction horizontal =
                difference.x > 0 ? Direction.Right : Direction.Left;

            if (mover.TryMove(horizontal))
                return;
        }

        if (difference.y != 0)
        {
            Direction vertical =
                difference.y > 0 ? Direction.Up : Direction.Down;

            if (mover.TryMove(vertical))
                return;
        }

        // If the preferred direction was blocked, try safe alternatives.
        TryFallbackMove();
    }

    private void TryFallbackMove()
    {
        Direction[] alternatives =
        {
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right
        };

        foreach (Direction dir in alternatives)
        {
            if (mover.TryMove(dir))
                return;
        }

        if (showDebugLogs)
            Debug.Log("FSM: Enemy has no valid move.");
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null)
            return;

        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(patrolPointA),
            0.2f);

        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(patrolPointB),
            0.2f);
    }
}