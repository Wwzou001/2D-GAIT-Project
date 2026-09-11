using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(GridMover))]
public class FSMEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Search,
        ReturnToPatrol
    }

    [Header("References")]
    [SerializeField] private GridMover player;

    [Header("FSM Settings")]

    // Enemy starts chasing at this distance
    [SerializeField] private int detectDistance = 3;

    // Enemy does not immediately lose the player
    // when they move just outside detectDistance.
    [SerializeField] private int loseDistance = 5;

    [SerializeField] private float moveInterval = 0.5f;


    [Header("Patrol")]

    [SerializeField] private Vector2Int patrolPointA =
        new Vector2Int(4, 4);

    [SerializeField] private Vector2Int patrolPointB =
        new Vector2Int(0, 4);


    [Header("Search")]

    // How long the enemy pauses at each search point
    [SerializeField] private float searchPauseDuration = 0.5f;


    [Header("Teaching / Debug")]

    [SerializeField] private bool showDebugLogs = true;

    [SerializeField] private TMP_Text stateText;


    private GridMover mover;

    private EnemyState currentState =
        EnemyState.Patrol;

    private Vector2Int currentPatrolTarget;

    private Vector2Int lastKnownPlayerPosition;

    private Vector2Int returnTarget;

    private float moveTimer;

    private float searchPauseTimer;


    // Search locations around the player's
    // last known position.
    private readonly List<Vector2Int> searchPoints =
        new List<Vector2Int>();

    private int currentSearchPointIndex = 0;


    public EnemyState CurrentState =>
        currentState;


     
    // INITIALISATION
     

    private void Awake()
    {
        mover = GetComponent<GridMover>();

        currentPatrolTarget = patrolPointA;

        UpdateStateDisplay();
    }


     
    // UPDATE
     

    private void Update()
    {
        // Stop AI once game ends.
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


     
    // STATE TRANSITIONS
     

    private void UpdateState()
    {
        int distanceToPlayer =
            ManhattanDistance(
                mover.GridPosition,
                player.GridPosition
            );


        switch (currentState)
        {
            // ------------------------------------------
            // PATROL
            // ------------------------------------------

            case EnemyState.Patrol:

                if (distanceToPlayer <= detectDistance)
                {
                    lastKnownPlayerPosition =
                        player.GridPosition;

                    ChangeState(
                        EnemyState.Chase
                    );
                }

                break;


            // ------------------------------------------
            // CHASE
            // ------------------------------------------

            case EnemyState.Chase:

                /*
                 * We use a larger loseDistance
                 * than detectDistance.
                 *
                 * This stops the FSM rapidly switching
                 * between Patrol/Search and Chase when
                 * the player is standing near the edge
                 * of the detection range.
                 */

                if (distanceToPlayer <= loseDistance)
                {
                    // Keep remembering where
                    // the player currently is.
                    lastKnownPlayerPosition =
                        player.GridPosition;
                }
                else
                {
                    StartSearch();

                    ChangeState(
                        EnemyState.Search
                    );
                }

                break;


            // ------------------------------------------
            // SEARCH
            // ------------------------------------------

            case EnemyState.Search:

                // If player comes back into detection
                // range, immediately chase again.
                if (distanceToPlayer <= detectDistance)
                {
                    lastKnownPlayerPosition =
                        player.GridPosition;

                    ChangeState(
                        EnemyState.Chase
                    );
                }

                break;


            // ------------------------------------------
            // RETURN TO PATROL
            // ------------------------------------------

            case EnemyState.ReturnToPatrol:

                // Enemy can still detect the player
                // while returning.
                if (distanceToPlayer <= detectDistance)
                {
                    lastKnownPlayerPosition =
                        player.GridPosition;

                    ChangeState(
                        EnemyState.Chase
                    );
                }

                break;
        }
    }


     
    // EXECUTE STATE
     

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


            case EnemyState.ReturnToPatrol:

                ReturnToPatrol();

                break;
        }
    }


     
    // PATROL
     

    private void Patrol()
    {
        // Reached patrol point?
        if (mover.GridPosition ==
            currentPatrolTarget)
        {
            // Switch to the opposite patrol point.
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


        MoveUsingAStar(
            currentPatrolTarget
        );
    }


     
    // CHASE
     

    private void Chase()
    {
        // Update the player's last-known position.
        lastKnownPlayerPosition =
            player.GridPosition;


        /*
         * Rather than moving horizontally /
         * vertically directly toward the player,
         * A* calculates a valid route.
         *
         * This allows the enemy to chase
         * around obstacles.
         */
        MoveUsingAStar(
            player.GridPosition
        );
    }


     
    // START SEARCH
     

    private void StartSearch()
    {
        searchPoints.Clear();

        currentSearchPointIndex = 0;

        searchPauseTimer = 0f;


        /*
         * First search the exact location
         * where the player was last seen.
         */
        AddSearchPointIfValid(
            lastKnownPlayerPosition
        );


        /*
         * Then search nearby cells.
         *
         * This makes SEARCH actually look
         * like searching rather than standing
         * still at one point.
         */

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            Vector2Int.up
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            Vector2Int.right
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            Vector2Int.down
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            Vector2Int.left
        );


        // Optional diagonal search locations

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            new Vector2Int(1, 1)
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            new Vector2Int(-1, 1)
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            new Vector2Int(1, -1)
        );

        AddSearchPointIfValid(
            lastKnownPlayerPosition +
            new Vector2Int(-1, -1)
        );


        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM SEARCH: starting search around " +
                $"{lastKnownPlayerPosition}. " +
                $"Search points = {searchPoints.Count}"
            );
        }
    }


     
    // SEARCH
     

    private void Search()
    {
        /*
         * If there are no valid search cells,
         * stop searching and return.
         */

        if (searchPoints.Count == 0)
        {
            BeginReturnToPatrol();

            return;
        }


        /*
         * Have we checked all search points?
         */

        if (currentSearchPointIndex >=
            searchPoints.Count)
        {
            BeginReturnToPatrol();

            return;
        }


        Vector2Int searchTarget =
            searchPoints[currentSearchPointIndex];


        /*
         * Move towards the current
         * search location.
         */

        if (mover.GridPosition != searchTarget)
        {
            MoveUsingAStar(
                searchTarget
            );

            return;
        }


        /*
         * We reached the search point.
         *
         * Pause briefly as though the enemy
         * is checking the area.
         */

        searchPauseTimer += moveInterval;


        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM SEARCH: checking " +
                $"{searchTarget}"
            );
        }


        if (searchPauseTimer >=
            searchPauseDuration)
        {
            searchPauseTimer = 0f;

            currentSearchPointIndex++;


            if (showDebugLogs)
            {
                Debug.Log(
                    $"FSM SEARCH: moving to " +
                    $"search point " +
                    $"{currentSearchPointIndex + 1}"
                );
            }
        }
    }


     
    // SEARCH POINT VALIDATION
     

    private void AddSearchPointIfValid(
        Vector2Int position)
    {
        if (GridSystem.Instance == null)
            return;


        // Ignore positions outside grid.
        if (!GridSystem.Instance.IsInBounds(
            position))
        {
            return;
        }


        // Ignore obstacles.
        if (GridSystem.Instance.IsObstacle(
            position))
        {
            return;
        }


        // Avoid duplicates.
        if (searchPoints.Contains(position))
            return;


        searchPoints.Add(position);
    }


     
    // RETURN TO PATROL
     

    private void BeginReturnToPatrol()
    {
        /*
         * Choose whichever patrol point
         * is closest to the enemy.
         */

        int distanceToA =
            ManhattanDistance(
                mover.GridPosition,
                patrolPointA
            );

        int distanceToB =
            ManhattanDistance(
                mover.GridPosition,
                patrolPointB
            );


        returnTarget =
            distanceToA <= distanceToB
            ? patrolPointA
            : patrolPointB;


        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM SEARCH finished. " +
                $"Returning to patrol at " +
                $"{returnTarget}"
            );
        }


        ChangeState(
            EnemyState.ReturnToPatrol
        );
    }


    private void ReturnToPatrol()
    {
        /*
         * Once we reach the patrol route,
         * go back to normal Patrol.
         */

        if (mover.GridPosition ==
            returnTarget)
        {
            currentPatrolTarget =
                returnTarget == patrolPointA
                ? patrolPointB
                : patrolPointA;


            ChangeState(
                EnemyState.Patrol
            );


            return;
        }


        MoveUsingAStar(
            returnTarget
        );
    }


     
    // A* MOVEMENT
     

    private bool MoveUsingAStar(
        Vector2Int target)
    {
        // Already at destination.
        if (mover.GridPosition == target)
        {
            return true;
        }


        /*
         * Calculate path from current
         * position to target.
         */

        List<Vector2Int> path =
            AStarPathfinder.FindPath(
                mover.GridPosition,
                target
            );


        if (path == null ||
            path.Count == 0)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    $"FSM: No path found from " +
                    $"{mover.GridPosition} " +
                    $"to {target}"
                );
            }

            return false;
        }


        /*
         * We only move ONE node along
         * the A* path each movement tick.
         */

        Vector2Int nextPosition =
            path[0];


        Vector2Int difference =
            nextPosition -
            mover.GridPosition;


        Direction direction;


        if (difference.x > 0)
        {
            direction = Direction.Right;
        }
        else if (difference.x < 0)
        {
            direction = Direction.Left;
        }
        else if (difference.y > 0)
        {
            direction = Direction.Up;
        }
        else if (difference.y < 0)
        {
            direction = Direction.Down;
        }
        else
        {
            return false;
        }


        return mover.TryMove(
            direction
        );
    }


     
    // CHANGE STATE
     

    private void ChangeState(
        EnemyState newState)
    {
        if (newState == currentState)
            return;


        EnemyState previousState =
            currentState;


        currentState =
            newState;


        if (showDebugLogs)
        {
            Debug.Log(
                $"FSM: {previousState} -> " +
                $"{currentState}"
            );
        }


        UpdateStateDisplay();
    }


     
    // DISTANCE
     

    private int ManhattanDistance(
        Vector2Int a,
        Vector2Int b)
    {
        return
            Mathf.Abs(a.x - b.x) +
            Mathf.Abs(a.y - b.y);
    }


     
    // TEACHING DISPLAY
     

    private void UpdateStateDisplay()
    {
        if (stateText == null)
            return;


        int distance =
            player != null
            ? ManhattanDistance(
                mover.GridPosition,
                player.GridPosition
            )
            : 0;


        stateText.text =
            $"FSM State: {currentState}\n" +
            $"Player Distance: {distance}";
    }


     
    // GIZMOS
     

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null)
            return;


        // Patrol point A
        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(
                patrolPointA
            ),
            0.2f
        );


        // Patrol point B
        Gizmos.DrawWireSphere(
            GridSystem.Instance.GridToWorld(
                patrolPointB
            ),
            0.2f
        );


        if (Application.isPlaying)
        {
            // Last known player location
            Gizmos.DrawWireCube(
                GridSystem.Instance.GridToWorld(
                    lastKnownPlayerPosition
                ),
                Vector3.one * 0.35f
            );


            // Search positions
            foreach (
                Vector2Int point
                in searchPoints)
            {
                Gizmos.DrawWireSphere(
                    GridSystem.Instance.GridToWorld(
                        point
                    ),
                    0.12f
                );
            }
        }
    }
}