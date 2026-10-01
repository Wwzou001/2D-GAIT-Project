using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(GridMover))]
public class FSMEnemyController : MonoBehaviour
{
    public enum FSMStage { Stage1_Basic = 1, Stage2_Search = 2, Stage3_Snooze = 3 }
    public enum EnemyState { PatrolAway, PatrolHome, Chase, Attack, GoHome, Search, Snooze }
    public enum EnemyEvent
    {
        ReachedTarget, PlayerDetected, PlayerLost, PlayerInAttackRange,
        AttackSuccess, SearchTimerOff, LowEnergy, HighEnergy
    }

    [Header("Teaching Stage")]
    [SerializeField] private FSMStage stage = FSMStage.Stage3_Snooze;

    [Header("References")]
    [SerializeField] private GridMover player;

    [Header("FSM Parameters")]
    [SerializeField] private int detectDistance = 3;
    [SerializeField] private int loseDistance = 5;
    [SerializeField] private int attackDistance = 1;
    [SerializeField] private float moveInterval = 0.5f;

    [Header("Patrol Environment")]
    [SerializeField] private Vector2Int patrolHome = new Vector2Int(0, 4);
    [SerializeField] private Vector2Int patrolAway = new Vector2Int(4, 4);

    [Header("Stage 2 - Search")]
    [SerializeField] private float searchDuration = 6f;
    [SerializeField] private float searchPointPause = 0.5f;

    [Header("Stage 3 - Energy / Snooze")]
    [SerializeField] private float startingEnergy = 100f;
    [SerializeField] private float lowEnergyThreshold = 20f;
    [SerializeField] private float highEnergyThreshold = 80f;
    [SerializeField] private float activeEnergyDrainPerSecond = 3f;
    [SerializeField] private float snoozeRecoveryPerSecond = 30f;

    [Header("A* Movement")]
    [SerializeField] private AStarPathfinder.HeuristicType heuristic =
        AStarPathfinder.HeuristicType.Manhattan;
    [SerializeField] private List<GridMover> movingObstacles = new List<GridMover>();

    [Header("Teaching / Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private TMP_Text stateText;

    private GridMover mover;
    private EnemyState currentState = EnemyState.PatrolAway;
    private EnemyState previousState = EnemyState.PatrolAway;
    private Vector2Int targetPosition;
    private Vector2Int lastKnownPlayerPosition;
    private float moveTimer, searchTimer, searchPointTimer, energy;
    private readonly List<Vector2Int> searchPoints = new List<Vector2Int>();
    private int currentSearchPointIndex;

    public EnemyState CurrentState => currentState;
    public Vector2Int CurrentTarget => targetPosition;
    public float CurrentEnergy => energy;

    private void Awake()
    {
        mover = GetComponent<GridMover>();
        energy = startingEnergy;
        targetPosition = patrolAway;
        UpdateStateDisplay();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;
        if (player == null) return;

        UpdateEnergy();

        // Stage 3 metastate: LowEnergy can interrupt any active state.
        if (stage >= FSMStage.Stage3_Snooze)
        {
            if (currentState != EnemyState.Snooze && energy <= lowEnergyThreshold)
                HandleEvent(EnemyEvent.LowEnergy);
            if (currentState == EnemyState.Snooze && energy >= highEnergyThreshold)
                HandleEvent(EnemyEvent.HighEnergy);
        }

        if (currentState != EnemyState.Snooze) CheckStateEvents();

        moveTimer += Time.deltaTime;
        if (moveTimer >= moveInterval)
        {
            moveTimer = 0f;
            ExecuteCurrentState();
        }

        UpdateStateDisplay();
    }

    private void CheckStateEvents()
    {
        int d = ManhattanDistance(mover.GridPosition, player.GridPosition);

        switch (currentState)
        {
            case EnemyState.PatrolAway:
            case EnemyState.PatrolHome:
                if (d <= detectDistance) HandleEvent(EnemyEvent.PlayerDetected);
                break;

            case EnemyState.Chase:
                if (d <= attackDistance) HandleEvent(EnemyEvent.PlayerInAttackRange);
                else if (d > loseDistance) HandleEvent(EnemyEvent.PlayerLost);
                else
                {
                    lastKnownPlayerPosition = player.GridPosition;
                    targetPosition = player.GridPosition;
                }
                break;

            case EnemyState.Search:
            case EnemyState.GoHome:
                if (d <= detectDistance) HandleEvent(EnemyEvent.PlayerDetected);
                break;
        }
    }

    private void HandleEvent(EnemyEvent e)
    {
        // Stage 3 Snooze behaves like a metastate interrupt.
        if (stage >= FSMStage.Stage3_Snooze)
        {
            if (e == EnemyEvent.LowEnergy && currentState != EnemyState.Snooze)
            {
                ChangeState(EnemyState.Snooze, "LowEnergy");
                return;
            }
            if (e == EnemyEvent.HighEnergy && currentState == EnemyState.Snooze)
            {
                targetPosition = patrolHome;
                ChangeState(EnemyState.PatrolHome, "HighEnergy");
                return;
            }
        }

        switch (currentState)
        {
            case EnemyState.PatrolAway:
                if (e == EnemyEvent.ReachedTarget)
                {
                    targetPosition = patrolHome;
                    ChangeState(EnemyState.PatrolHome, "ReachedTarget");
                }
                else if (e == EnemyEvent.PlayerDetected) BeginChase();
                break;

            case EnemyState.PatrolHome:
                if (e == EnemyEvent.ReachedTarget)
                {
                    targetPosition = patrolAway;
                    ChangeState(EnemyState.PatrolAway, "ReachedTarget");
                }
                else if (e == EnemyEvent.PlayerDetected) BeginChase();
                break;

            case EnemyState.Chase:
                if (e == EnemyEvent.PlayerInAttackRange)
                    ChangeState(EnemyState.Attack, "PlayerInAttackRange");
                else if (e == EnemyEvent.PlayerLost)
                {
                    if (stage >= FSMStage.Stage2_Search)
                    {
                        BeginSearch();
                        ChangeState(EnemyState.Search, "PlayerLost");
                    }
                    else
                    {
                        targetPosition = patrolHome;
                        ChangeState(EnemyState.GoHome, "PlayerLost");
                    }
                }
                break;

            case EnemyState.Attack:
                if (e == EnemyEvent.AttackSuccess)
                {
                    targetPosition = patrolHome;
                    ChangeState(EnemyState.GoHome, "AttackSuccess");
                }
                break;

            case EnemyState.Search:
                if (e == EnemyEvent.PlayerDetected) BeginChase();
                else if (e == EnemyEvent.SearchTimerOff)
                {
                    targetPosition = patrolHome;
                    ChangeState(EnemyState.GoHome, "SearchTimerOff");
                }
                break;

            case EnemyState.GoHome:
                if (e == EnemyEvent.ReachedTarget)
                {
                    targetPosition = patrolAway;
                    ChangeState(EnemyState.PatrolAway, "ReachedTarget");
                }
                else if (e == EnemyEvent.PlayerDetected) BeginChase();
                break;
        }
    }

    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case EnemyState.PatrolAway:
            case EnemyState.PatrolHome: ExecutePatrol(); break;
            case EnemyState.Chase: ExecuteChase(); break;
            case EnemyState.Attack: ExecuteAttack(); break;
            case EnemyState.GoHome: ExecuteGoHome(); break;
            case EnemyState.Search: ExecuteSearch(); break;
            case EnemyState.Snooze: break; // no movement while sleeping
        }
    }

    private void ExecutePatrol()
    {
        if (mover.GridPosition == targetPosition)
        {
            HandleEvent(EnemyEvent.ReachedTarget);
            return;
        }
        MoveUsingAStar(targetPosition);
    }

    private void BeginChase()
    {
        lastKnownPlayerPosition = player.GridPosition;
        targetPosition = player.GridPosition;
        ChangeState(EnemyState.Chase, "PlayerDetected");
    }

    private void ExecuteChase()
    {
        lastKnownPlayerPosition = player.GridPosition;
        targetPosition = player.GridPosition;
        MoveUsingAStar(targetPosition);
    }

    private void ExecuteAttack()
    {
            // Move onto the player's cell to perform the attack.
        if (mover.GridPosition != player.GridPosition)
        {
            targetPosition = player.GridPosition;
            MoveUsingAStar(targetPosition);
            return;
        }

        // Once the enemy reaches the player, the existing
        // GameManager collision rule handles the player's death.
        Log("FSM ATTACK: 1 damage action.");

        HandleEvent(EnemyEvent.AttackSuccess);
    }

    private void ExecuteGoHome()
    {
        targetPosition = patrolHome;
        if (mover.GridPosition == patrolHome)
        {
            HandleEvent(EnemyEvent.ReachedTarget);
            return;
        }
        MoveUsingAStar(targetPosition);
    }

    private void BeginSearch()
    {
        searchTimer = searchPointTimer = 0f;
        currentSearchPointIndex = 0;
        searchPoints.Clear();

        AddSearchPointIfValid(lastKnownPlayerPosition);
        AddSearchPointIfValid(lastKnownPlayerPosition + Vector2Int.up);
        AddSearchPointIfValid(lastKnownPlayerPosition + Vector2Int.right);
        AddSearchPointIfValid(lastKnownPlayerPosition + Vector2Int.down);
        AddSearchPointIfValid(lastKnownPlayerPosition + Vector2Int.left);
        AddSearchPointIfValid(lastKnownPlayerPosition + new Vector2Int(1, 1));
        AddSearchPointIfValid(lastKnownPlayerPosition + new Vector2Int(1, -1));
        AddSearchPointIfValid(lastKnownPlayerPosition + new Vector2Int(-1, -1));
        AddSearchPointIfValid(lastKnownPlayerPosition + new Vector2Int(-1, 1));

        targetPosition = searchPoints.Count > 0 ? searchPoints[0] : lastKnownPlayerPosition;
    }

    private void ExecuteSearch()
    {
        searchTimer += moveInterval;

        if (searchTimer >= searchDuration || searchPoints.Count == 0)
        {
            HandleEvent(EnemyEvent.SearchTimerOff);
            return;
        }

        targetPosition = searchPoints[currentSearchPointIndex];
        if (mover.GridPosition != targetPosition)
        {
            MoveUsingAStar(targetPosition);
            return;
        }

        searchPointTimer += moveInterval;
        if (searchPointTimer < searchPointPause) return;

        searchPointTimer = 0f;
        currentSearchPointIndex++;

        if (currentSearchPointIndex >= searchPoints.Count)
        {
            HandleEvent(EnemyEvent.SearchTimerOff);
            return;
        }

        targetPosition = searchPoints[currentSearchPointIndex];
    }

    private void UpdateEnergy()
    {
        if (stage < FSMStage.Stage3_Snooze) return;

        if (currentState == EnemyState.Snooze)
            energy = Mathf.Min(startingEnergy, energy + snoozeRecoveryPerSecond * Time.deltaTime);
        else
            energy = Mathf.Max(0f, energy - activeEnergyDrainPerSecond * Time.deltaTime);
    }

    private void AddSearchPointIfValid(Vector2Int p)
    {
        if (GridSystem.Instance == null) return;
        if (!GridSystem.Instance.IsInBounds(p)) return;
        if (GridSystem.Instance.IsObstacle(p)) return;
        if (!searchPoints.Contains(p)) searchPoints.Add(p);
    }

    private bool MoveUsingAStar(Vector2Int target)
    {
        if (mover.GridPosition == target) return true;

        HashSet<Vector2Int> blocked = GetMovingObstacleCells();
        AStarPathfinder.SearchResult result =
            AStarPathfinder.FindPath(mover.GridPosition, target, heuristic, blocked);

        if (!result.PathFound || result.Path.Count == 0) return false;

        Vector2Int next = result.Path[0];
        if (GetMovingObstacleCells().Contains(next)) return false;

        Vector2Int diff = next - mover.GridPosition;
        Direction dir;
        if (diff.x > 0) dir = Direction.Right;
        else if (diff.x < 0) dir = Direction.Left;
        else if (diff.y > 0) dir = Direction.Up;
        else if (diff.y < 0) dir = Direction.Down;
        else return false;

        return mover.TryMove(dir);
    }

    private HashSet<Vector2Int> GetMovingObstacleCells()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        foreach (GridMover obstacle in movingObstacles)
            if (obstacle != null && obstacle != mover && obstacle != player)
                blocked.Add(obstacle.GridPosition);
        return blocked;
    }

    private void ChangeState(EnemyState next, string reason)
    {
        if (next == currentState) return;
        previousState = currentState;
        currentState = next;
        Log($"FSM: {previousState} -> {currentState} ({reason})");
        UpdateStateDisplay();
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private void UpdateStateDisplay()
    {
        if (stateText == null) return;

        int d = player != null
            ? ManhattanDistance(mover.GridPosition, player.GridPosition)
            : 0;

        string extra = "";

        if (currentState == EnemyState.Search)
        {
            extra += $"\nSearch Time: {searchTimer:0.0}/{searchDuration:0.0}s";
            extra += $"\nSearch Point: {Mathf.Min(currentSearchPointIndex + 1, searchPoints.Count)}/{searchPoints.Count}";
        }

        if (stage >= FSMStage.Stage3_Snooze)
        {
            extra += $"\nEnergy: {energy:0}";
        }

        stateText.text =
            $"Stage: {(int)stage}\n" +
            $"Previous State: {previousState}\n" +
            $"Current State: {currentState}\n" +
            $"Target: {targetPosition}\n" +
            $"Player Distance: {d}" +
            extra;
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log(message);
    }

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null) return;
        Gizmos.DrawWireSphere(GridSystem.Instance.GridToWorld(patrolHome), 0.2f);
        Gizmos.DrawWireSphere(GridSystem.Instance.GridToWorld(patrolAway), 0.2f);

        if (Application.isPlaying && currentState == EnemyState.Search)
            foreach (Vector2Int p in searchPoints)
                Gizmos.DrawWireSphere(GridSystem.Instance.GridToWorld(p), 0.12f);
    }
    // Lets RoomGenerator (or any other setup script) assign the player
    // reference after spawning, since it's private for the Inspector otherwise.
    public void SetPlayer(GridMover p)
    {
        player = p;
    }
}
