using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// FSM reference implementation using three clear states: Patrol, Chase and Search.
///
/// Sprint 3 simplification:
/// A single targetPosition variable represents where the enemy currently wants to go.
/// Events/state transitions update that target instead of creating extra movement states.
///
/// Parameters:
/// - detectDistance: distance at which Patrol/Search detects the player.
/// - loseDistance: larger distance at which Chase loses the player.
/// - moveInterval: time between enemy grid moves.
/// - patrolPointA/B: endpoints of the normal patrol route.
/// - heuristic: A* heuristic used by the FSM's movement.
/// - movingObstacles: temporary blocked cells that A* must route around.
/// </summary>
[RequireComponent(typeof(GridMover))]
public class FSMEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Search
    }

    [Header("References")]
    [SerializeField] private GridMover player;

    [Header("FSM Parameters")]
    [SerializeField] private int detectDistance = 3;
    [SerializeField] private int loseDistance = 5;
    [SerializeField] private float moveInterval = 0.5f;

    [Header("Patrol Environment")]
    [SerializeField] private Vector2Int patrolPointA = new Vector2Int(4, 4);
    [SerializeField] private Vector2Int patrolPointB = new Vector2Int(0, 4);

    [Header("A* Movement")]
    [SerializeField] private AStarPathfinder.HeuristicType heuristic =
        AStarPathfinder.HeuristicType.Manhattan;
    [SerializeField] private List<GridMover> movingObstacles = new List<GridMover>();

    [Header("Teaching / Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private TMP_Text stateText;

    private GridMover mover;
    private EnemyState currentState = EnemyState.Patrol;
    private EnemyState previousState = EnemyState.Patrol;

    // Core Sprint 3 simplification: states update this one destination variable.
    private Vector2Int targetPosition;
    private Vector2Int lastKnownPlayerPosition;
    private float moveTimer;

    public EnemyState CurrentState => currentState;
    public Vector2Int CurrentTarget => targetPosition;

    private void Awake()
    {
        mover = GetComponent<GridMover>();
        targetPosition = patrolPointA;
        UpdateStateDisplay();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        if (player == null)
            return;

        UpdateStateFromEvents();

        moveTimer += Time.deltaTime;
        if (moveTimer >= moveInterval)
        {
            moveTimer = 0f;
            ExecuteCurrentState();
        }

        UpdateStateDisplay();
    }

    /// <summary>
    /// State changes are event-like decisions. Each important event updates targetPosition:
    /// player detected -> player cell; player lost -> last known cell; patrol point reached -> other patrol point.
    /// </summary>
    private void UpdateStateFromEvents()
    {
        int distanceToPlayer = ManhattanDistance(mover.GridPosition, player.GridPosition);

        switch (currentState)
        {
            case EnemyState.Patrol:
                if (distanceToPlayer <= detectDistance)
                {
                    lastKnownPlayerPosition = player.GridPosition;
                    targetPosition = player.GridPosition;
                    ChangeState(EnemyState.Chase, "player detected");
                }
                break;

            case EnemyState.Chase:
                if (distanceToPlayer <= loseDistance)
                {
                    // Player is still known, so the target follows the player's current cell.
                    lastKnownPlayerPosition = player.GridPosition;
                    targetPosition = player.GridPosition;
                }
                else
                {
                    // Player-lost event: Search has ONE target, the last known position.
                    targetPosition = lastKnownPlayerPosition;
                    ChangeState(EnemyState.Search, "player lost");
                }
                break;

            case EnemyState.Search:
                if (distanceToPlayer <= detectDistance)
                {
                    lastKnownPlayerPosition = player.GridPosition;
                    targetPosition = player.GridPosition;
                    ChangeState(EnemyState.Chase, "player found again");
                }
                break;
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

            case EnemyState.Search:
                Search();
                break;
        }
    }

    private void Patrol()
    {
        if (mover.GridPosition == targetPosition)
        {
            // Patrol-point event: update the same target variable to the other endpoint.
            targetPosition = targetPosition == patrolPointA ? patrolPointB : patrolPointA;
            Log($"FSM PATROL: target updated to {targetPosition}");
        }

        MoveUsingAStar(targetPosition);
    }

    private void Chase()
    {
        // UpdateStateFromEvents keeps targetPosition synced with the visible player.
        MoveUsingAStar(targetPosition);
    }

    private void Search()
    {
        // Search is intentionally simple per client feedback:
        // travel to the last-known player position stored in targetPosition.
        if (mover.GridPosition != targetPosition)
        {
            MoveUsingAStar(targetPosition);
            return;
        }

        // Search-target reached and player was not rediscovered.
        // No ReturnToPatrol state is needed: update the target and resume Patrol.
        targetPosition = GetNearestPatrolPoint();
        ChangeState(EnemyState.Patrol, "last-known position checked");
    }

    private Vector2Int GetNearestPatrolPoint()
    {
        int distanceA = ManhattanDistance(mover.GridPosition, patrolPointA);
        int distanceB = ManhattanDistance(mover.GridPosition, patrolPointB);
        return distanceA <= distanceB ? patrolPointA : patrolPointB;
    }

    private bool MoveUsingAStar(Vector2Int target)
    {
        if (mover.GridPosition == target)
            return true;

        HashSet<Vector2Int> blocked = GetMovingObstacleCells();
        AStarPathfinder.SearchResult result = AStarPathfinder.FindPath(
            mover.GridPosition,
            target,
            heuristic,
            blocked);

        if (!result.PathFound || result.Path.Count == 0)
        {
            Log($"FSM: no current path to {target}");
            return false;
        }

        Vector2Int nextPosition = result.Path[0];

        // A moving obstacle may have changed cell since the search began.
        blocked = GetMovingObstacleCells();
        if (blocked.Contains(nextPosition))
            return false;

        Vector2Int difference = nextPosition - mover.GridPosition;
        Direction direction;

        if (difference.x > 0) direction = Direction.Right;
        else if (difference.x < 0) direction = Direction.Left;
        else if (difference.y > 0) direction = Direction.Up;
        else if (difference.y < 0) direction = Direction.Down;
        else return false;

        return mover.TryMove(direction);
    }

    private HashSet<Vector2Int> GetMovingObstacleCells()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        foreach (GridMover obstacle in movingObstacles)
        {
            if (obstacle == null || obstacle == mover || obstacle == player)
                continue;

            blocked.Add(obstacle.GridPosition);
        }

        return blocked;
    }

    private void ChangeState(EnemyState newState, string reason)
    {
        if (newState == currentState)
            return;

        previousState = currentState;
        currentState = newState;
        Log($"FSM: {previousState} -> {currentState} ({reason}) | Target = {targetPosition}");
        UpdateStateDisplay();
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void UpdateStateDisplay()
    {
        if (stateText == null)
            return;

        int playerDistance = player != null
            ? ManhattanDistance(mover.GridPosition, player.GridPosition)
            : 0;

        stateText.text =
            $"Previous State: {previousState}\n" +
            $"Current State: {currentState}\n" +
            $"Target: {targetPosition}\n" +
            $"Player Distance: {playerDistance}";
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log(message);
    }

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null)
            return;

        Gizmos.DrawWireSphere(GridSystem.Instance.GridToWorld(patrolPointA), 0.2f);
        Gizmos.DrawWireSphere(GridSystem.Instance.GridToWorld(patrolPointB), 0.2f);

        if (Application.isPlaying)
        {
            Gizmos.DrawWireCube(
                GridSystem.Instance.GridToWorld(targetPosition),
                Vector3.one * 0.35f);
        }
    }
}
