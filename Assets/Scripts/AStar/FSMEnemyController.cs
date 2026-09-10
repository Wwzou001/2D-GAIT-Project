using UnityEngine;
using TMPro;

/// <summary>
/// FSM reference-progress implementation.
///
/// States:
/// PATROL -> CHASE -> SEARCH -> PATROL
///
/// The enemy patrols normally.
/// If the player enters the chase distance, the enemy chases.
/// If the player escapes, the enemy searches the player's
/// last known position before returning to patrol.
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

    [Header("FSM Settings")]
    [SerializeField] private int chaseDistance = 3;
    [SerializeField] private float moveInterval = 0.5f;

    [Header("Patrol")]
    [SerializeField] private Vector2Int patrolPointA =
        new Vector2Int(4, 4);

    [SerializeField] private Vector2Int patrolPointB =
        new Vector2Int(0, 4);

    [Header("Search")]
    [SerializeField] private float searchDuration = 2f;

    [Header("Teaching / Debug")]
    [SerializeField] private bool showDebugLogs = true;

   
    [SerializeField] private TMP_Text stateText;

    private GridMover mover;

    private EnemyState currentState = EnemyState.Patrol;

    private Vector2Int currentPatrolTarget;

    // Remembers where the player was last seen.
    private Vector2Int lastKnownPlayerPosition;

    private float moveTimer;
    private float searchTimer;

    public EnemyState CurrentState => currentState;

    private void Awake()
    {
        mover = GetComponent<GridMover>();

        currentPatrolTarget = patrolPointA;

        UpdateStateDisplay();
    }

    private void Update()
    {
        // Stop FSM once game has ended.
        if (GameManager.Instance != null &&
            GameManager.Instance.GameOver)
        {
            return;
        }

        if (player == null)
            return;

        UpdateState();

        moveTimer += Time.deltaTime;

        if (moveTimer >= moveInterval)
        {
            moveTimer = 0f;

            ExecuteCurrentState();
        }

        UpdateStateDisplay();
    }

   
    // STATE TRANSITION LOGIC
    

    private void UpdateState()
    {
        int distanceToPlayer =
            ManhattanDistance(
                mover.GridPosition,
                player.GridPosition
            );

        switch (currentState)
        {
            case EnemyState.Patrol:

                // PATROL -> CHASE
                if (distanceToPlayer <= chaseDistance)
                {
                    lastKnownPlayerPosition =
                        player.GridPosition;

                    ChangeState(EnemyState.Chase);
                }

                break;


            case EnemyState.Chase:

                // Player still visible/in range.
                if (distanceToPlayer <= chaseDistance)
                {
                    // Keep updating the last known position.
                    lastKnownPlayerPosition =
                        player.GridPosition;
                }

                // CHASE -> SEARCH
                else
                {
                    searchTimer = 0f;

                    ChangeState(EnemyState.Search);
                }

                break;


            case EnemyState.Search:

                // SEARCH -> CHASE
                // Player enters detection range again.
                if (distanceToPlayer <= chaseDistance)
                {
                    lastKnownPlayerPosition =
                        player.GridPosition;

                    ChangeState(EnemyState.Chase);
                }

                break;
        }
    }

    
    // EXECUTE CURRENT STATE
    

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

    
    // PATROL

    private void Patrol()
    {
        // If enemy reaches current patrol point,
        // switch to the other patrol point.
        if (mover.GridPosition ==
            currentPatrolTarget)
        {
            currentPatrolTarget =
                currentPatrolTarget == patrolPointA
                ? patrolPointB
                : patrolPointA;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"FSM PATROL: new target = " +
                    $"{currentPatrolTarget}"
                );
            }
        }

        MoveOneStepTowards(currentPatrolTarget);
    }

    
    // CHASE
    

    private void Chase()
    {
        // Continuously chase the player's
        // current grid location.
        lastKnownPlayerPosition =
            player.GridPosition;

        MoveOneStepTowards(
            player.GridPosition
        );
    }

    
    // SEARCH
    

    private void Search()
    {
        // Move to where the player was last seen.
        if (mover.GridPosition !=
            lastKnownPlayerPosition)
        {
            MoveOneStepTowards(
                lastKnownPlayerPosition
            );

            return;
        }

        // Enemy reached the last-known position
        // but player is not there.
        searchTimer += moveInterval;

        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM SEARCH: checking last known " +
                $"position {lastKnownPlayerPosition}"
            );
        }

        // SEARCH -> PATROL after waiting.
        if (searchTimer >= searchDuration)
        {
            searchTimer = 0f;

            ChangeState(EnemyState.Patrol);
        }
    }

    
    // CHANGE STATE
    

    private void ChangeState(
        EnemyState newState)
    {
        if (newState == currentState)
            return;

        EnemyState previousState =
            currentState;

        currentState = newState;

        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM: {previousState} -> " +
                $"{currentState}"
            );
        }

        UpdateStateDisplay();
    }

    
    // MOVEMENT
    

    private void MoveOneStepTowards(
        Vector2Int target)
    {
        Vector2Int current =
            mover.GridPosition;

        Vector2Int difference =
            target - current;

        // Try horizontal movement first.
        if (difference.x != 0)
        {
            Direction horizontal =
                difference.x > 0
                ? Direction.Right
                : Direction.Left;

            if (mover.TryMove(horizontal))
                return;
        }

        // Then try vertical movement.
        if (difference.y != 0)
        {
            Direction vertical =
                difference.y > 0
                ? Direction.Up
                : Direction.Down;

            if (mover.TryMove(vertical))
                return;
        }

        // Preferred direction was blocked.
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

        foreach (Direction direction
                 in alternatives)
        {
            if (mover.TryMove(direction))
                return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "FSM: Enemy has no valid move."
            );
        }
    }

   
    // DISTANCE


    private int ManhattanDistance(
        Vector2Int a,
        Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) +
               Mathf.Abs(a.y - b.y);
    }

    
    // TEACHING / DEBUG DISPLAY


    private void UpdateStateDisplay()
    {
        if (stateText != null)
        {
            stateText.text =
                $"FSM State: {currentState}";
        }
    }

    // SCENE DEBUGGING
   

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null)
            return;

        // Patrol Point A
        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(
                patrolPointA
            ),
            0.2f
        );

        // Patrol Point B
        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(
                patrolPointB
            ),
            0.2f
        );

        // Last known player position
        if (Application.isPlaying)
        {
            Gizmos.DrawWireCube(
                GridSystem.Instance.GridToWorld(
                    lastKnownPlayerPosition
                ),
                Vector3.one * 0.35f
            );
        }
    }
}