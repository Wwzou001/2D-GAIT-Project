using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Enemy FSM built for 3 teaching stages (per client direction):
///   Stage 1 - the basic FSM given to students as a starting point.
///   Stage 2 - students add a Search state and update the rest accordingly.
///   Stage 3 - students add Snooze as a metastate: any state can be
///             interrupted by a LowEnergy event, and Snooze always exits
///             to PatrolHome on a HighEnergy event.
///
/// Teaching visuals: the enemy sprite is tinted per state and a legend
/// (legendText) explains the colours, so students can read what each agent
/// is doing at a glance.
///
/// OPEN QUESTION FOR CLIENT: his email said "remove search from ___" with
/// the end of the sentence missing. Nothing here guesses at it.
/// </summary>
[RequireComponent(typeof(TilemapGridMover))]
public class FSMEnemyController : MonoBehaviour
{
    public enum FSMStage { Stage1_Basic = 1, Stage2_Search = 2, Stage3_Snooze = 3 }

    public enum EnemyState
    {
        /// <summary>Walking from Home toward the Away patrol point via A*. Exits on ReachedTarget (-> PatrolHome) or PlayerDetected (-> Chase).</summary>
        PatrolAway,

        /// <summary>Walking from Away back toward the Home patrol point via A*. Exits on ReachedTarget (-> PatrolAway) or PlayerDetected (-> Chase).</summary>
        PatrolHome,

        /// <summary>Player detected. Target re-syncs every tick to the player's live grid position and is followed via A*. Exits on PlayerInAttackRange (-> Attack) or PlayerLost (-> Search, or GoHome at Stage 1).</summary>
        Chase,

        /// <summary>Close enough to act. Deals exactly 1 damage to the player (PlayerHealth.TakeDamage), then raises AttackSuccess (-> GoHome). One hit per entry, no failure path.</summary>
        Attack,

        /// <summary>Walking back to the Home patrol point after an Attack, a finished Search, or losing the player at Stage 1. Deliberately ignores the player until Home is reached. Exits only on ReachedTarget (-> PatrolAway).</summary>
        GoHome,

        /// <summary>Stage 2+. Entered when Chase loses the player. A small hierarchical state machine (see SearchPhase): go to the last-known spot (and along the player's heading), then repeatedly probe random not-yet-seen spots inside a search area as big as the vision distance, until the time runs out or the whole area has been seen. Exits on PlayerDetected (-> Chase) or SearchTimerOff (-> GoHome).</summary>
        Search,

        /// <summary>Stage 3+. Metastate: any state can be interrupted by LowEnergy. No movement while snoozing. Exits only on HighEnergy (-> PatrolHome, never back to the interrupted state).</summary>
        Snooze
    }

    public enum EnemyEvent
    {
        /// <summary>Patrol/GoHome: the enemy's grid position equals its current target.</summary>
        ReachedTarget,

        /// <summary>The player entered detectDistanceCells while the enemy was in Patrol or Search.</summary>
        PlayerDetected,

        /// <summary>Chase: the player is further than loseDistanceCells.</summary>
        PlayerLost,

        /// <summary>Chase: the player is within attackDistance.</summary>
        PlayerInAttackRange,

        /// <summary>Attack: the one damage action is done. Unconditional (client: always succeeds).</summary>
        AttackSuccess,

        /// <summary>Search: the search time budget ran out, or there are no points left to visit.</summary>
        SearchTimerOff,

        /// <summary>Stage 3+: energy is at or below lowEnergyThreshold. Can interrupt any state except Snooze.</summary>
        LowEnergy,

        /// <summary>Stage 3+: energy is at or above highEnergyThreshold while Snoozing.</summary>
        HighEnergy,

        /// <summary>Search (Smart style only): the player picked up a coin within noiseHearingRadius. The search restarts around the spot of the noise.</summary>
        NoiseHeard
    }

    /// <summary>
    /// How elaborate the Search state is. A difficulty ladder: teach with Simple, show Smart
    /// as the model solution. The explicit numbers match the earlier versions of this enum, so
    /// an Inspector choice saved before the cleanup (Expanding = 1, Smart = 3) still lands on
    /// the right style.
    /// </summary>
    public enum SearchStyle
    {
        /// <summary>Last-known position, then expanding rings (radius 1, 2, 3...) swept as a spiral - the "snail" pattern. (Previously called Expanding.)</summary>
        Simple = 1,

        /// <summary>Follow the player's last heading first ("which way did they run?"), then repeatedly pick a random not-yet-seen spot inside a search area as big as the vision distance, and re-aim the search when a noise is heard.</summary>
        Smart = 3
    }

    /// <summary>Sub-state of Search. Only used inside Search; the top-level FSM never sees it.</summary>
    public enum SearchPhase
    {
        /// <summary>Walking to the last-known position and along the player's last heading.</summary>
        FollowTrail,

        /// <summary>Sweeping outward in rings around the search centre (Simple style).</summary>
        Sweep,

        /// <summary>Walking to a random not-yet-seen spot inside the search area (Smart style).</summary>
        Probe
    }

    [Header("Teaching Stage")]
    [SerializeField] private FSMStage stage = FSMStage.Stage3_Snooze;

    [Header("References")]
    [SerializeField] private TilemapGridMover player;

    [Header("FSM Parameters")]
    [Tooltip("The enemy starts chasing when the player is within this many grid steps (Manhattan). " +
             "It also decides how much of the map Search counts as 'seen' around the enemy.")]
    [SerializeField] private int detectDistanceCells = 5;
    [Tooltip("The enemy gives up the chase beyond this distance. Should be larger than Detect Distance; " +
             "if it is not, Detect Distance + 1 is used while the game runs.")]
    [SerializeField] private int loseDistanceCells = 7;
    [SerializeField] private int attackDistance = 1;
    [SerializeField] private float moveInterval = 0.5f;

    [Header("Patrol Environment")]
    [SerializeField] private Vector2Int patrolHome = new Vector2Int(0, 4);
    [SerializeField] private Vector2Int patrolAway = new Vector2Int(4, 4);

    [Header("Stage 2 - Search")]
    [Tooltip("Simple = spiral rings around the last-known spot. " +
             "Smart = follow the player's heading, then probe random unseen spots, and react to noise.")]
    [SerializeField] private SearchStyle searchStyle = SearchStyle.Smart;
    [Tooltip("Total time budget for the whole search, including walking. The client's example was 6 s " +
             "but called the area 'too small'; 20-30 gives a proper search. NOTE: an enemy that is " +
             "already in your scene keeps its old saved value, so update it in the Inspector.")]
    [SerializeField] private float searchDuration = 20f;
    [SerializeField] private float searchPointPause = 0.5f;
    [Tooltip("Smart: how far from the last-known spot the enemy searches. The client said " +
             "to use the same size as the vision distance (6). Area = (2 x radius + 1) cells across.")]
    [SerializeField] private int searchAreaRadius = 6;
    [Tooltip("Simple: how many rings to sweep (ring r is r cells from the centre).")]
    [SerializeField] private int maxSearchRing = 3;
    [Tooltip("Smart: how many cells ahead of the last-known spot to check along the player's last heading.")]
    [SerializeField] private int trailLookAhead = 3;
    [Tooltip("Smart: restart the search around the player when they pick up a coin (the noise).")]
    [SerializeField] private bool listenForNoise = true;
    [Tooltip("Smart: the enemy only hears noise this many cells away (Manhattan).")]
    [SerializeField] private int noiseHearingRadius = 12;

    [Header("Search Path (in-game visual)")]
    [Tooltip("Draw the search in the Game view without needing Gizmos: the route walked so far, " +
             "the planned route, the search area outline and a dot for every search point.")]
    [SerializeField] private bool showSearchPath = true;
    [Tooltip("Keep the drawn route on screen after the search ends (until the next search starts), " +
             "so you can point at the whole route during a demo.")]
    [SerializeField] private bool keepPathAfterSearch = true;
    [SerializeField] private float searchPathWidth = 0.08f;
    [Tooltip("Lines are never drawn thinner than this many screen pixels, whatever the zoom. " +
             "Lines under 1 pixel wide can vanish in parts (e.g. only one edge of the search square showing).")]
    [SerializeField] private float minSearchLinePixels = 3f;
    [Tooltip("Colour of the route the enemy has actually walked.")]
    [SerializeField] private Color walkedPathColour = new Color(0.25f, 0.50f, 1.00f);
    [Tooltip("Sorting order of the lines and dots. Raise it if they hide behind the map.")]
    [SerializeField] private int searchPathSortingOrder = 6;

    [Header("Stage 3 - Energy / Snooze")]
    [SerializeField] private float startingEnergy = 100f;
    [SerializeField] private float lowEnergyThreshold = 20f;
    [SerializeField] private float highEnergyThreshold = 80f;
    [Tooltip("After waking (or at the start), the enemy cannot fall asleep again until it has been " +
             "awake this many seconds, however low its energy is. This guarantees a long awake window " +
             "for demos. Set to 0 to sleep as soon as energy is low.")]
    [SerializeField] private float minAwakeSeconds = 60f;
    [Tooltip("A search that has already started is allowed to finish before the enemy snoozes. " +
             "Untick for the strict rule that LowEnergy interrupts ANY state, even mid-search.")]
    [SerializeField] private bool finishSearchBeforeSnoozing = true;
    [Tooltip("Energy lost per second while awake. Time until Snooze = (Starting Energy - Low Energy " +
             "Threshold) / this. At 1 that is 80 s, at 3 it is only 27 s. NOTE: an enemy already in " +
             "the scene keeps its old saved value, so set it in the Inspector.")]
    [SerializeField] private float activeEnergyDrainPerSecond = 1f;
    [Tooltip("Energy gained per second while snoozing. Lower this for a longer nap.")]
    [SerializeField] private float snoozeRecoveryPerSecond = 30f;

    [Header("A* Movement")]
    [SerializeField] private AStarPathfinder.HeuristicType heuristic =
        AStarPathfinder.HeuristicType.Manhattan;
    [SerializeField] private List<TilemapGridMover> movingObstacles = new List<TilemapGridMover>();
    [Tooltip("If A* fails or finds no path, take a straight-line step instead of freezing. " +
             "A warning is still logged so the failure is not hidden.")]
    [SerializeField] private bool fallbackWhenAStarFails = true;

    [Header("State Colours")]
    [Tooltip("Tint the enemy sprite by state so students can see what it is doing.")]
    [SerializeField] private bool tintByState = true;
    [Tooltip("Auto-found on this GameObject if left empty.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color patrolColour = new Color(0.35f, 0.60f, 1.00f);
    [SerializeField] private Color chaseColour = new Color(1.00f, 0.30f, 0.30f);
    [SerializeField] private Color attackColour = new Color(1.00f, 0.55f, 0.10f);
    [SerializeField] private Color goHomeColour = Color.white;
    [SerializeField] private Color searchColour = new Color(1.00f, 0.90f, 0.30f);
    [SerializeField] private Color snoozeColour = new Color(0.55f, 0.55f, 0.65f);

    [Header("Teaching / Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private TMP_Text stateText;
    [Tooltip("Font size for the state panel (the legend uses 90% of this). Set to 0 to keep " +
             "whatever size you set on the text object. Ignored if Auto Size is on.")]
    [SerializeField] private float stateFontSize = 15f;
    [Tooltip("Draws a thin black outline around the panel text so it is readable on any background.")]
    [SerializeField] private bool improveTextContrast = true;
    [Tooltip("Optional separate text object that lists each state's colour.")]
    [SerializeField] private TMP_Text legendText;
    [Tooltip("How many recent transitions to list under the state text.")]
    [SerializeField] private int historyLength = 4;

    private TilemapGridMover mover;
    private EnemyState currentState = EnemyState.PatrolAway;
    private EnemyState previousState = EnemyState.PatrolAway;
    private Vector2Int targetPosition;
    private string lastEventName = "None";
    private Vector2Int lastKnownPlayerPosition;
    private float moveTimer, searchTimer, searchPointTimer, energy;
    private readonly List<Vector2Int> searchPoints = new List<Vector2Int>();
    private readonly List<int> searchPointRing = new List<int>();     // parallel to searchPoints: 0 = trail, 1+ = ring number
    private readonly HashSet<Vector2Int> searchChecked = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> searchCovered = new HashSet<Vector2Int>();   // cells the enemy has already "seen"
    private readonly List<Vector2Int> probeCandidates = new List<Vector2Int>();       // reused to avoid garbage
    private Vector2Int searchAreaCentre;
    private int probeCount;
    private int currentSearchPointIndex;
    private int searchStuckTicks;
    private Vector2Int playerHeading = Vector2Int.zero;               // last direction the player was seen moving
    private Vector2Int pendingNoiseCell;
    private readonly List<string> transitionHistory = new List<string>();

    // The 8 sample points of a ring, clockwise from North: N, NE, E, SE, S, SW, W, NW.
    private static readonly Vector2Int[] RingDirections =
    {
        new Vector2Int(0, 1),  new Vector2Int(1, 1),   new Vector2Int(1, 0),  new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1)
    };
    private bool warnedNoPlayer;
    private float nextNoPathLogTime;
    private float nextRefusedLogTime;
    private int consecutiveRefusedMoves;
    private float nextDisplayRefresh;

    // In-game search path visuals (created on the first search).
    private GameObject searchVisualsRoot;
    private LineRenderer walkedLine, plannedLine, areaLine;
    private readonly List<Vector3> walkedPoints = new List<Vector3>();
    private readonly List<SpriteRenderer> searchMarkers = new List<SpriteRenderer>();
    private Vector2Int lastWalkedCell;
    private float nextVisualRefresh;
    private float nextWidthRefresh;
    private float awakeTimer;   // seconds awake since the last nap (or the start)
    private Material visualMaterial;
    private Sprite dotSprite;

    private static readonly Color TrailMarkerColour = new Color(0.18f, 0.62f, 0.37f);
    private static readonly Color ProbeMarkerColour = new Color(0.91f, 0.33f, 0.24f);

    private PlayerHealth playerHealth;

    public EnemyState CurrentState => currentState;
    public Vector2Int CurrentTarget => targetPosition;
    public float CurrentEnergy => energy;

    private void OnEnable()
    {
        CoinCollectible.CoinCollected += OnCoinCollected;
    }

    private void OnDisable()
    {
        CoinCollectible.CoinCollected -= OnCoinCollected;
    }

    // Lose Distance has to be larger than Detect Distance, otherwise the enemy flickers
    // between Chase and Search at the edge. The Inspector numbers are NEVER changed for you
    // (they stay exactly as typed); the larger value is simply used when the game runs.
    private int EffectiveLoseDistance =>
        Mathf.Max(loseDistanceCells, detectDistanceCells + 1);

    // Only a warning, so a number you type is never rewritten behind your back.
    private void OnValidate()
    {
        if (loseDistanceCells <= detectDistanceCells)
        {
            Debug.LogWarning(
                $"FSMEnemyController: Lose Distance ({loseDistanceCells}) should be larger than " +
                $"Detect Distance ({detectDistanceCells}). It will act as {detectDistanceCells + 1} " +
                "while the game runs.", this);
        }
    }

    private void OnDestroy()
    {
        if (searchVisualsRoot != null) Destroy(searchVisualsRoot);
        if (visualMaterial != null) Destroy(visualMaterial);

        if (dotSprite != null)
        {
            Destroy(dotSprite.texture);
            Destroy(dotSprite);
        }
    }

    // A coin pickup makes noise at the player's position. Only Smart search reacts,
    // and only while actually searching, so no extra states or transitions are needed.
    private void OnCoinCollected(CoinCollectible coin)
    {
        if (!listenForNoise || player == null) return;
        if (searchStyle != SearchStyle.Smart || stage < FSMStage.Stage2_Search) return;
        if (currentState != EnemyState.Search) return;

        Vector2Int noiseCell = player.GridPosition;
        if (ManhattanDistance(mover.GridPosition, noiseCell) > noiseHearingRadius) return;

        pendingNoiseCell = noiseCell;
        HandleEvent(EnemyEvent.NoiseHeard);
    }

    private void Awake()
    {
        mover = GetComponent<TilemapGridMover>();
        energy = startingEnergy;
        targetPosition = patrolAway;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        CachePlayerHealth();
        StyleTexts();
        ApplyStateColour();
        UpdateLegend();
        UpdateStateDisplay();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;

        if (player == null)
        {
            // Say so once, instead of silently doing nothing.
            if (!warnedNoPlayer)
            {
                warnedNoPlayer = true;
                Debug.LogWarning(
                    "FSMEnemyController: the Player field is not assigned, so the enemy " +
                    "will not move. Drag the player into it in the Inspector.",
                    this
                );
            }
            return;
        }

        // The player can be assigned after Awake (e.g. by a spawner), so keep trying.
        if (playerHealth == null) CachePlayerHealth();

        // PlayerHealth ends the run itself, so stop the AI once the player is dead.
        if (playerHealth != null && playerHealth.IsDead) return;

        UpdateEnergy();

        // Stage 3 metastate: LowEnergy can interrupt any active state.
        if (stage >= FSMStage.Stage3_Snooze)
        {
            if (currentState != EnemyState.Snooze) awakeTimer += Time.deltaTime;

            // Two optional guards so a demo is never cut short (both are Inspector settings):
            //  - minAwakeSeconds: it cannot fall asleep again until it has been awake this long.
            //  - finishSearchBeforeSnoozing: a search that has started is allowed to finish.
            bool awakeLongEnough = awakeTimer >= minAwakeSeconds;
            bool searchProtected = finishSearchBeforeSnoozing && currentState == EnemyState.Search;

            if (currentState != EnemyState.Snooze && energy <= lowEnergyThreshold &&
                awakeLongEnough && !searchProtected)
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

        UpdateSearchVisuals();

        // Refresh the text 10 times a second instead of every frame
        // (state changes still refresh it immediately).
        if (Time.unscaledTime >= nextDisplayRefresh)
        {
            nextDisplayRefresh = Time.unscaledTime + 0.1f;
            UpdateStateDisplay();
        }
    }

    private void CachePlayerHealth()
    {
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void CheckStateEvents()
    {
        int d = ManhattanDistance(mover.GridPosition, player.GridPosition);

        switch (currentState)
        {
            case EnemyState.PatrolAway:
            case EnemyState.PatrolHome:
                if (d <= detectDistanceCells) HandleEvent(EnemyEvent.PlayerDetected);
                break;

            case EnemyState.Chase:
                if (d <= attackDistance) HandleEvent(EnemyEvent.PlayerInAttackRange);
                else if (d > EffectiveLoseDistance) HandleEvent(EnemyEvent.PlayerLost);
                else
                {
                    RememberPlayer(player.GridPosition);
                    targetPosition = player.GridPosition;
                }
                break;

            case EnemyState.Search:
                if (d <= detectDistanceCells)
                    HandleEvent(EnemyEvent.PlayerDetected);
                break;

            case EnemyState.GoHome:
                // Ignore the player until the enemy reaches home.
                break;
        }
    }

    /// <summary>
    /// TRANSITION REFERENCE (written description per client request - keep
    /// in sync with the switch below):
    ///
    ///   PatrolAway + ReachedTarget       -> PatrolHome : reached the away point, walk home next.
    ///   PatrolAway + PlayerDetected      -> Chase      : player noticed while patrolling.
    ///   PatrolHome + ReachedTarget       -> PatrolAway : reached home, walk away next.
    ///   PatrolHome + PlayerDetected      -> Chase      : player noticed while patrolling.
    ///   Chase + PlayerInAttackRange      -> Attack     : close enough to act.
    ///   Chase + PlayerLost (Stage 2+)    -> Search     : out of range, check the last-known area.
    ///   Chase + PlayerLost (Stage 1)     -> GoHome     : no Search yet, so give up and head home.
    ///   Attack + AttackSuccess           -> GoHome     : 1 damage dealt, always head home next.
    ///   Search + PlayerDetected          -> Chase      : re-acquired the player mid-search.
    ///   Search + SearchTimerOff          -> GoHome     : time budget used up, or the whole search area has been seen.
    ///   Search + NoiseHeard (Smart)      -> Search     : the player picked up a coin nearby; restart the search around that spot.
    ///   GoHome + ReachedTarget           -> PatrolAway : arrived home, resume the patrol loop.
    ///   (GoHome ignores PlayerDetected on purpose - it commits to going home.)
    ///   ANY (Stage 3+) + LowEnergy       -> Snooze     : metastate interrupt, whatever the enemy was doing.
    ///   Snooze + HighEnergy              -> PatrolHome : always PatrolHome, never back to the interrupted state.
    /// </summary>
    private void HandleEvent(EnemyEvent e)
    {
        lastEventName = e.ToString();

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
                else if (e == EnemyEvent.NoiseHeard) HearNoise();
                break;

            case EnemyState.GoHome:
                if (e == EnemyEvent.ReachedTarget)
                {
                    targetPosition = patrolAway;
                    ChangeState(EnemyState.PatrolAway, "ReachedTarget");
                }
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
        // A fresh chase: forget the old heading so a stale one is never used.
        lastKnownPlayerPosition = player.GridPosition;
        playerHeading = Vector2Int.zero;
        targetPosition = player.GridPosition;
        ChangeState(EnemyState.Chase, "PlayerDetected");
    }

    private void ExecuteChase()
    {
        RememberPlayer(player.GridPosition);
        targetPosition = player.GridPosition;
        MoveUsingAStar(targetPosition);
    }

    // Records where the player was last seen and which way they were moving.
    private void RememberPlayer(Vector2Int cell)
    {
        if (cell != lastKnownPlayerPosition)
        {
            Vector2Int delta = cell - lastKnownPlayerPosition;

            // Only trust a normal step, not a respawn or teleport.
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 2)
                playerHeading = DominantAxis(delta);
        }

        lastKnownPlayerPosition = cell;
    }

    // Reduces any offset to a single unit step along its larger axis.
    private static Vector2Int DominantAxis(Vector2Int delta)
    {
        if (delta == Vector2Int.zero) return Vector2Int.zero;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return new Vector2Int(delta.x > 0 ? 1 : -1, 0);

        return new Vector2Int(0, delta.y > 0 ? 1 : -1);
    }

    private void ExecuteAttack()
    {
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            Log("FSM ATTACK: Player takes 1 damage.");
        }

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

        Vector2Int current = mover.GridPosition;
        bool moved = false;

        // Walk horizontally toward home first, then vertically.
        if (current.x < patrolHome.x) moved = mover.TryMove(Direction.Right);
        else if (current.x > patrolHome.x) moved = mover.TryMove(Direction.Left);
        else if (current.y < patrolHome.y) moved = mover.TryMove(Direction.Up);
        else if (current.y > patrolHome.y) moved = mover.TryMove(Direction.Down);

        // If a wall blocks the straight walk, fall back to A* so the enemy
        // can never get stuck behind an obstacle.
        if (!moved) MoveUsingAStar(patrolHome);
    }

    // ---------------------------------------------------------------
    // SEARCH (hierarchical)
    //
    // The top-level FSM only ever sees one Search state. Inside it the
    // enemy walks an ordered list of search points (searchPoints):
    //
    //   FollowTrail : the last-known spot, then (Smart) cells ahead along
    //                 the player's last heading - "which way did they run?"
    //   Sweep       : 8 sample points on ring 1, then ring 2, ring 3...
    //                 as a spiral, centred where the trail ended.
    //
    // searchPointRing tells which phase a point belongs to (0 = trail).
    // ---------------------------------------------------------------

    private void BeginSearch()
    {
        searchTimer = searchPointTimer = 0f;
        searchStuckTicks = 0;
        currentSearchPointIndex = 0;
        searchPoints.Clear();
        searchPointRing.Clear();
        searchChecked.Clear();
        searchCovered.Clear();
        probeCount = 0;
        searchAreaCentre = lastKnownPlayerPosition;

        // Anything within detection range of where the enemy stands is already "seen".
        MarkCovered(mover.GridPosition);

        switch (searchStyle)
        {
            case SearchStyle.Simple:
                // The last-known spot, then the spiral of rings around it.
                TryAddSearchPoint(lastKnownPlayerPosition, 0);
                BuildRings(lastKnownPlayerPosition, 0);
                break;

            default:
                BuildSmartSearch();
                break;
        }

        targetPosition = searchPoints.Count > 0 ? searchPoints[0] : lastKnownPlayerPosition;

        ResetSearchVisuals();

        Log($"FSM SEARCH ({searchStyle}): {searchPoints.Count} points around {lastKnownPlayerPosition}, heading {playerHeading}.");

        // Simple is a fixed pattern, so print its exact visiting order. It can be compared with
        // the pattern drawn in the game, and it is identical every time for the same last-known cell.
        if (showDebugLogs && searchStyle == SearchStyle.Simple)
        {
            StringBuilder order = new StringBuilder();

            for (int i = 0; i < searchPoints.Count; i++)
            {
                if (i > 0) order.Append("  >  ");
                order.Append('(').Append(searchPoints[i].x).Append(',').Append(searchPoints[i].y)
                     .Append(") ring ").Append(searchPointRing[i]);
            }

            Log($"FSM SEARCH (Simple) visiting order: {order}");
        }
    }

    // Smart: last-known spot -> cells ahead along the heading. After the trail ends,
    // ExecuteSearch keeps adding random probes inside the search area.
    private void BuildSmartSearch()
    {
        Vector2Int centre = lastKnownPlayerPosition;
        TryAddSearchPoint(centre, 0);

        if (playerHeading != Vector2Int.zero)
        {
            for (int k = 1; k <= Mathf.Max(0, trailLookAhead); k++)
            {
                Vector2Int cell = centre + playerHeading * k;

                // A wall ends the trail - the player cannot have run through it.
                if (!IsWalkableCell(cell)) break;

                TryAddSearchPoint(cell, 0);
            }
        }
    }

    private bool UsesProbes()
    {
        return searchStyle == SearchStyle.Smart;
    }

    // Everything within detection range of 'centre' counts as seen: if the player
    // were there, the enemy would already have fired PlayerDetected.
    private void MarkCovered(Vector2Int centre)
    {
        int r = Mathf.Max(0, detectDistanceCells);

        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r)
                    searchCovered.Add(centre + new Vector2Int(dx, dy));
    }

    // Picks the next random, walkable, not-yet-seen spot inside the search area and
    // appends it as a new search point. Returns false when the whole area has been seen.
    // To avoid zig-zagging across the area it takes the closest of a few random picks.
    private bool TryAppendProbe()
    {
        int r = Mathf.Max(1, searchAreaRadius);

        probeCandidates.Clear();

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                Vector2Int cell = searchAreaCentre + new Vector2Int(dx, dy);

                if (searchCovered.Contains(cell)) continue;
                if (!IsWalkableCell(cell)) continue;

                probeCandidates.Add(cell);
            }
        }

        if (probeCandidates.Count == 0) return false;

        Vector2Int best = probeCandidates[Random.Range(0, probeCandidates.Count)];
        int bestDistance = ManhattanDistance(mover.GridPosition, best);

        for (int i = 1; i < 3; i++)
        {
            Vector2Int pick = probeCandidates[Random.Range(0, probeCandidates.Count)];
            int distance = ManhattanDistance(mover.GridPosition, pick);

            if (distance < bestDistance)
            {
                best = pick;
                bestDistance = distance;
            }
        }

        searchPoints.Add(best);
        searchPointRing.Add(-1);   // -1 marks a probe point
        probeCount++;
        return true;
    }

    // Percentage of the walkable search area that has been seen so far.
    private float AreaCoveragePercent()
    {
        int r = Mathf.Max(1, searchAreaRadius);
        int total = 0;
        int seen = 0;

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                Vector2Int cell = searchAreaCentre + new Vector2Int(dx, dy);
                if (!IsWalkableCell(cell)) continue;

                total++;
                if (searchCovered.Contains(cell)) seen++;
            }
        }

        return total > 0 ? 100f * seen / total : 100f;
    }

    // Adds rings 1..maxSearchRing. Each ring starts next to where the previous
    // one ended, so the route is a smooth outward spiral instead of zig-zagging.
    private void BuildRings(Vector2Int centre, int startIndex)
    {
        int index = startIndex;

        for (int ring = 1; ring <= Mathf.Max(1, maxSearchRing); ring++)
        {
            for (int i = 0; i < RingDirections.Length; i++)
            {
                Vector2Int offset = RingDirections[(index + i) % RingDirections.Length] * ring;
                TryAddSearchPoint(centre + offset, ring);
            }

            index = (index + RingDirections.Length - 1) % RingDirections.Length;
        }
    }

    private bool IsWalkableCell(Vector2Int p)
    {
        return TilemapGridSystem.Instance != null && TilemapGridSystem.Instance.IsWalkable(p);
    }

    private bool TryAddSearchPoint(Vector2Int p, int ring)
    {
        if (!IsWalkableCell(p)) return false;
        if (searchPoints.Contains(p)) return false;

        searchPoints.Add(p);
        searchPointRing.Add(ring);
        return true;
    }

    private SearchPhase CurrentSearchPhase()
    {
        if (searchPointRing.Count == 0 || currentSearchPointIndex >= searchPointRing.Count)
            return SearchPhase.Sweep;

        int ring = searchPointRing[currentSearchPointIndex];

        if (ring == 0) return SearchPhase.FollowTrail;
        if (ring < 0) return SearchPhase.Probe;
        return SearchPhase.Sweep;
    }

    private void ExecuteSearch()
    {
        searchTimer += moveInterval;

        // Walking counts too: wherever the enemy stands, it sees everything in detection range.
        if (UsesProbes()) MarkCovered(mover.GridPosition);

        if (searchTimer >= searchDuration ||
            searchPoints.Count == 0 ||
            currentSearchPointIndex >= searchPoints.Count)
        {
            HandleEvent(EnemyEvent.SearchTimerOff);
            return;
        }

        targetPosition = searchPoints[currentSearchPointIndex];

        // Still walking to the current point.
        if (mover.GridPosition != targetPosition)
        {
            if (MoveUsingAStar(targetPosition))
            {
                searchStuckTicks = 0;
            }
            else if (++searchStuckTicks >= 3)
            {
                // Unreachable (walled off or blocked) - skip it rather than get stuck,
                // and never pick it again.
                Log($"FSM SEARCH: cannot reach {targetPosition}, skipping it.");
                searchCovered.Add(targetPosition);
                AdvanceSearchPoint();
            }
            return;
        }

        // Arrived: look around for a moment, then mark this point as checked.
        searchStuckTicks = 0;
        searchChecked.Add(targetPosition);

        searchPointTimer += moveInterval;
        if (searchPointTimer < searchPointPause) return;

        AdvanceSearchPoint();
    }

    private void AdvanceSearchPoint()
    {
        searchPointTimer = 0f;
        searchStuckTicks = 0;
        currentSearchPointIndex++;

        if (currentSearchPointIndex >= searchPoints.Count)
        {
            // Probe styles never run out of points: pick a new unseen spot instead.
            // Only when the whole area has been seen is the search really finished.
            if (UsesProbes() && TryAppendProbe())
            {
                targetPosition = searchPoints[currentSearchPointIndex];
                Log($"FSM SEARCH: probe {probeCount} -> {targetPosition} ({AreaCoveragePercent():0}% of the area seen).");
                return;
            }

            // Every point checked (or the whole area seen) and nothing found.
            HandleEvent(EnemyEvent.SearchTimerOff);
            return;
        }

        targetPosition = searchPoints[currentSearchPointIndex];
    }

    // NoiseHeard: the player picked up a coin. Re-aim the search at the new spot.
    private void HearNoise()
    {
        Vector2Int cell = pendingNoiseCell;
        float timeUsed = searchTimer;

        // The player went from the old spot to the noise, so that is their new heading.
        playerHeading = DominantAxis(cell - lastKnownPlayerPosition);
        lastKnownPlayerPosition = cell;

        BeginSearch();

        // The noise buys a fresh search, but never more than half the budget back.
        searchTimer = Mathf.Min(timeUsed, searchDuration * 0.5f);

        Log($"FSM SEARCH: heard noise at {cell}, re-focusing the search.");
        AddHistory($"Search: heard noise at {cell}");
    }

    private void AddHistory(string line)
    {
        transitionHistory.Add(line);
        while (transitionHistory.Count > Mathf.Max(1, historyLength))
            transitionHistory.RemoveAt(0);
    }

    private void UpdateEnergy()
    {
        if (stage < FSMStage.Stage3_Snooze) return;

        if (currentState == EnemyState.Snooze)
            energy = Mathf.Min(startingEnergy, energy + snoozeRecoveryPerSecond * Time.deltaTime);
        else
            energy = Mathf.Max(0f, energy - activeEnergyDrainPerSecond * Time.deltaTime);
    }

    private bool MoveUsingAStar(Vector2Int target)
    {
        if (mover.GridPosition == target) return true;

        bool pathFound = false;
        Vector2Int next = mover.GridPosition;
        string failReason = null;

        try
        {
            HashSet<Vector2Int> blocked = GetMovingObstacleCells();
            AStarPathfinder.SearchResult result =
                AStarPathfinder.FindPath(mover.GridPosition, target, heuristic, blocked);

            if (result.PathFound && result.Path != null && result.Path.Count > 0)
            {
                next = result.Path[0];
                pathFound = true;
            }
            else
            {
                failReason = "A* found no path";
            }
        }
        catch (System.Exception ex)
        {
            // Never let a pathfinding error freeze the enemy silently.
            failReason = "A* threw " + ex.GetType().Name + ": " + ex.Message;
        }

        if (!pathFound)
        {
            WarnThrottled(
                $"FSM: {failReason} from {mover.GridPosition} to {target} ({currentState}).",
                ref nextNoPathLogTime
            );

            if (!fallbackWhenAStarFails) return false;

            // Fallback: one straight-line step so the enemy keeps moving.
            return TryGreedyStep(target);
        }

        if (GetMovingObstacleCells().Contains(next)) return false;

        Vector2Int diff = next - mover.GridPosition;
        Direction dir;
        if (diff.x > 0) dir = Direction.Right;
        else if (diff.x < 0) dir = Direction.Left;
        else if (diff.y > 0) dir = Direction.Up;
        else if (diff.y < 0) dir = Direction.Down;
        else return false;

        bool moved = mover.TryMove(dir);
        TrackMoveResult(moved, dir);
        return moved;
    }

    // Straight-line fallback: try the axis with the larger gap first, then the other.
    private bool TryGreedyStep(Vector2Int target)
    {
        Vector2Int diff = target - mover.GridPosition;

        Direction horizontal = diff.x > 0 ? Direction.Right : Direction.Left;
        Direction vertical = diff.y > 0 ? Direction.Up : Direction.Down;

        bool horizontalFirst = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y);

        if (horizontalFirst)
        {
            if (diff.x != 0 && mover.TryMove(horizontal)) return true;
            if (diff.y != 0 && mover.TryMove(vertical)) return true;
        }
        else
        {
            if (diff.y != 0 && mover.TryMove(vertical)) return true;
            if (diff.x != 0 && mover.TryMove(horizontal)) return true;
        }

        return false;
    }

    // Warns if the mover keeps refusing moves, so a stuck enemy is never a mystery.
    private void TrackMoveResult(bool moved, Direction dir)
    {
        if (moved)
        {
            consecutiveRefusedMoves = 0;
            return;
        }

        consecutiveRefusedMoves++;

        if (consecutiveRefusedMoves >= 6)
        {
            WarnThrottled(
                $"FSM: TilemapGridMover.TryMove({dir}) was refused {consecutiveRefusedMoves} times " +
                $"in a row at {mover.GridPosition}. The enemy is blocked or the mover is locked.",
                ref nextRefusedLogTime
            );
        }
    }

    private void WarnThrottled(string message, ref float nextAllowedTime)
    {
        if (!showDebugLogs || Time.time < nextAllowedTime) return;
        nextAllowedTime = Time.time + 3f;
        Debug.LogWarning(message, this);
    }

    private HashSet<Vector2Int> GetMovingObstacleCells()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        foreach (TilemapGridMover obstacle in movingObstacles)
            if (obstacle != null && obstacle != mover && obstacle != player)
                blocked.Add(obstacle.GridPosition);
        return blocked;
    }

    private void ChangeState(EnemyState next, string reason)
    {
        if (next == currentState) return;
        previousState = currentState;
        currentState = next;

        // A fresh nap: the awake clock starts again from zero when it wakes up.
        if (next == EnemyState.Snooze) awakeTimer = 0f;

        if (previousState == EnemyState.Search) OnLeftSearch();

        string line = $"{previousState} -> {currentState} ({reason})";
        Log($"FSM: {line}");

        transitionHistory.Add(line);
        while (transitionHistory.Count > Mathf.Max(1, historyLength))
            transitionHistory.RemoveAt(0);

        ApplyStateColour();
        UpdateLegend();
        UpdateStateDisplay();
    }

    // ---------------------------------------------------------------
    // Teaching visuals
    // ---------------------------------------------------------------

    private Color GetStateColour(EnemyState s)
    {
        switch (s)
        {
            case EnemyState.PatrolAway:
            case EnemyState.PatrolHome: return patrolColour;
            case EnemyState.Chase: return chaseColour;
            case EnemyState.Attack: return attackColour;
            case EnemyState.GoHome: return goHomeColour;
            case EnemyState.Search: return searchColour;
            case EnemyState.Snooze: return snoozeColour;
            default: return Color.white;
        }
    }

    private void ApplyStateColour()
    {
        if (!tintByState || spriteRenderer == null) return;
        spriteRenderer.color = GetStateColour(currentState);
    }

    private void UpdateLegend()
    {
        if (legendText == null) return;

        StringBuilder sb = new StringBuilder("<b>STATE COLOURS</b>\n");

        AppendLegendLine(sb, "Patrol", patrolColour,
            currentState == EnemyState.PatrolAway || currentState == EnemyState.PatrolHome);
        AppendLegendLine(sb, "Chase", chaseColour, currentState == EnemyState.Chase);
        AppendLegendLine(sb, "Attack", attackColour, currentState == EnemyState.Attack);
        AppendLegendLine(sb, "GoHome", goHomeColour, currentState == EnemyState.GoHome);

        if (stage >= FSMStage.Stage2_Search)
            AppendLegendLine(sb, "Search", searchColour, currentState == EnemyState.Search);

        if (stage >= FSMStage.Stage3_Snooze)
            AppendLegendLine(sb, "Snooze", snoozeColour, currentState == EnemyState.Snooze);

        legendText.text = sb.ToString();
    }

    private void AppendLegendLine(StringBuilder sb, string label, Color colour, bool active)
    {
        string hex = ColorUtility.ToHtmlStringRGB(colour);
        sb.Append($"<color=#{hex}>■</color> ");
        sb.Append(active ? $"<b>{label}  (now)</b>" : label);
        sb.Append('\n');
    }

    // ---------------------------------------------------------------
    // Pretty on-screen text (TextMeshPro rich text)
    // ---------------------------------------------------------------

    private const string MutedHex = "D3D9DF";   // labels (light enough to read on the brown background)
    private const string DimHex = "6B7480";     // empty bar segments / lost hearts
    private const string GoodHex = "5CD65C";
    private const string WarnHex = "F2C94C";
    private const string BadHex = "FF5A5A";

    private static string PrettyState(EnemyState s)
    {
        switch (s)
        {
            case EnemyState.PatrolAway: return "Patrol Away";
            case EnemyState.PatrolHome: return "Patrol Home";
            case EnemyState.GoHome: return "Go Home";
            default: return s.ToString();
        }
    }

    // One "label  value" line. <nobr> stops TextMeshPro wrapping it onto two lines.
    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append("<nobr><color=#").Append(MutedHex).Append('>')
          .Append(label).Append("</color>  ").Append(value).Append("</nobr>\n");
    }

    // 20-segment bar: green when high, yellow when getting low, red when low.
    private string BuildEnergyBar()
    {
        const int segments = 20;
        float ratio = startingEnergy > 0f ? Mathf.Clamp01(energy / startingEnergy) : 0f;
        int filled = Mathf.RoundToInt(ratio * segments);
        string fillHex = ratio > 0.5f ? GoodHex : (ratio > 0.25f ? WarnHex : BadHex);

        return "<color=#" + fillHex + ">" + new string('|', filled) + "</color>" +
               "<color=#" + DimHex + ">" + new string('|', segments - filled) + "</color>";
    }

    private string BuildHearts()
    {
        if (playerHealth == null) return "-";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < playerHealth.MaxLives; i++)
        {
            string hex = i < playerHealth.CurrentLives ? BadHex : DimHex;
            sb.Append("<color=#").Append(hex).Append(">♥</color> ");
        }
        return sb.ToString();
    }

    // Sets up the panel text once: rich text, one line per row (no wrapping), an
    // outline so it stays readable over any background, and a sensible size.
    private void StyleTexts()
    {
        StyleText(stateText, stateFontSize);
        StyleText(legendText, stateFontSize * 0.9f);
    }

#pragma warning disable 0618   // enableWordWrapping is obsolete in newer TextMeshPro but still works
    private void StyleText(TMP_Text text, float fontSize)
    {
        if (text == null) return;

        text.richText = true;
        text.enableWordWrapping = false;   // each row stays on one line
        text.raycastTarget = false;        // never blocks mouse clicks

        if (fontSize > 0f) text.fontSize = fontSize;

        if (improveTextContrast)
        {
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }
    }
#pragma warning restore 0618

    // e.g. "Follow trail" or "Sweep - ring 2 of 3".
    private string DescribeSearchPhase()
    {
        SearchPhase phase = CurrentSearchPhase();

        if (phase == SearchPhase.FollowTrail) return "Follow trail";
        if (phase == SearchPhase.Probe) return $"Probe {probeCount}";

        int ring = currentSearchPointIndex < searchPointRing.Count
            ? searchPointRing[currentSearchPointIndex]
            : maxSearchRing;

        return $"Sweep - ring {ring} of {maxSearchRing}";
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private void UpdateStateDisplay()
    {
        if (stateText == null) return;

        int d = player != null
            ? ManhattanDistance(mover.GridPosition, player.GridPosition)
            : 0;

        string accent = ColorUtility.ToHtmlStringRGB(GetStateColour(currentState));

        StringBuilder sb = new StringBuilder(384);

        // Header + the current state in its own colour, so it matches the sprite tint.
        sb.Append("<nobr><size=75%><b><color=#").Append(MutedHex)
          .Append(">ENEMY AI  |  STAGE ").Append((int)stage).Append("</color></b></size></nobr>\n");

        sb.Append("<nobr><size=140%><b><color=#").Append(accent).Append('>')
          .Append(PrettyState(currentState).ToUpper()).Append("</color></b></size></nobr>\n");

        AppendRow(sb, "From", $"{PrettyState(previousState)}  ({lastEventName})");
        AppendRow(sb, "Target", $"{targetPosition}   player {d} away");
        AppendRow(sb, "Lives", BuildHearts());

        if (currentState == EnemyState.Search)
        {
            AppendRow(sb, "Search", $"{searchStyle}  |  {DescribeSearchPhase()}");

            string progress = UsesProbes()
                ? $"seen {AreaCoveragePercent():0}%  |  {probeCount} probes"
                : $"{searchChecked.Count} / {searchPoints.Count} points";

            AppendRow(sb, "Time", $"{searchTimer:0} / {searchDuration:0} s  |  {progress}");

            if (searchStyle == SearchStyle.Smart && playerHeading != Vector2Int.zero)
                AppendRow(sb, "Heading", playerHeading.ToString());
        }

        if (stage >= FSMStage.Stage3_Snooze)
        {
            string zzz = currentState == EnemyState.Snooze
                ? "  <color=#8AA0FF><b>Zzz</b></color>"
                : "";
            AppendRow(sb, "Energy", $"{energy:0}  {BuildEnergyBar()}{zzz}");

            if (currentState != EnemyState.Snooze)
                AppendRow(sb, "Awake", $"{awakeTimer:0} / {minAwakeSeconds:0} s");
        }

        if (transitionHistory.Count > 0)
        {
            sb.Append("<nobr><size=80%><color=#").Append(MutedHex).Append("><b>RECENT</b></color></size></nobr>\n");

            for (int i = transitionHistory.Count - 1; i >= 0; i--)
            {
                // Newest line is brightest, older ones fade out.
                string hex = i == transitionHistory.Count - 1 ? "FFFFFF" : MutedHex;
                sb.Append("<nobr><size=80%><color=#").Append(hex).Append('>')
                  .Append(transitionHistory[i]).Append("</color></size></nobr>\n");
            }
        }

        stateText.text = sb.ToString();
    }

    // ---------------------------------------------------------------
    // In-game search path (works in the Game view, no Gizmos needed)
    //   blue line  = the route the enemy has actually walked
    //   thin line  = the planned route through the remaining search points
    //   dashed-ish square outline = the search area (Smart)
    //   dots       = search points: green = trail/ring, red = probe,
    //                faded = already checked, big = current target
    // ---------------------------------------------------------------

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return TilemapGridSystem.Instance != null
            ? (Vector3)TilemapGridSystem.Instance.GridToWorld(cell)
            : transform.position;
    }

    private float CellSize(Vector2Int cell)
    {
        float size = Vector3.Distance(CellToWorld(cell), CellToWorld(cell + Vector2Int.right));
        return size > 0.0001f ? size : 1f;
    }

    private Material GetVisualMaterial()
    {
        if (visualMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) visualMaterial = new Material(shader);
        }
        return visualMaterial;
    }

    // A small soft-edged circle, generated once so no art asset is needed.
    private Sprite GetDotSprite()
    {
        if (dotSprite != null) return dotSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float centre = (size - 1) / 2f;
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                float alpha = Mathf.Clamp01(radius - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();

        // Pixels per unit = size, so the sprite is exactly 1 world unit wide.
        dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return dotSprite;
    }

    private LineRenderer CreateLine(string label, Color colour, float width, int order)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(searchVisualsRoot.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterial = GetVisualMaterial();
        lr.startColor = colour;
        lr.endColor = colour;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = order;
        lr.positionCount = 0;
        return lr;
    }

    private void EnsureSearchVisuals()
    {
        if (searchVisualsRoot != null) return;

        // A separate root (not a child of the enemy) so the lines stay put while it walks.
        searchVisualsRoot = new GameObject($"Search Path ({name})");

        walkedLine = CreateLine("Walked route", walkedPathColour,
            searchPathWidth, searchPathSortingOrder);

        plannedLine = CreateLine("Planned route",
            new Color(searchColour.r, searchColour.g, searchColour.b, 0.75f),
            searchPathWidth * 0.6f, searchPathSortingOrder - 1);

        areaLine = CreateLine("Search area",
            new Color(searchColour.r, searchColour.g, searchColour.b, 0.95f),
            searchPathWidth * 0.6f, searchPathSortingOrder - 2);
        areaLine.loop = true;
    }

    // Keeps every line at least minSearchLinePixels thick on screen. A line thinner than
    // one pixel can miss the pixel centres and partly vanish, so the width is worked out
    // from the camera: world units per pixel = 2 x orthographicSize / screen height.
    private void ApplySearchLineWidths()
    {
        if (walkedLine == null || plannedLine == null || areaLine == null) return;

        float minWorld = 0f;
        Camera cam = Camera.main;

        if (cam != null && cam.orthographic)
            minWorld = minSearchLinePixels * (2f * cam.orthographicSize) / Mathf.Max(1, cam.pixelHeight);

        SetLineWidth(walkedLine, Mathf.Max(searchPathWidth, minWorld));
        SetLineWidth(areaLine, Mathf.Max(searchPathWidth * 0.8f, minWorld));
        SetLineWidth(plannedLine, Mathf.Max(searchPathWidth * 0.6f, minWorld * 0.7f));
    }

    private static void SetLineWidth(LineRenderer lr, float width)
    {
        lr.startWidth = width;
        lr.endWidth = width;
    }

    // Called at the start of every search: clear the old drawing and begin a new one.
    private void ResetSearchVisuals()
    {
        if (!showSearchPath) return;
        if (TilemapGridSystem.Instance == null || mover == null) return;

        EnsureSearchVisuals();
        searchVisualsRoot.SetActive(true);
        ApplySearchLineWidths();

        walkedPoints.Clear();
        lastWalkedCell = mover.GridPosition;
        walkedPoints.Add(CellToWorld(lastWalkedCell));
        walkedLine.positionCount = 1;
        walkedLine.SetPosition(0, walkedPoints[0]);

        UpdateAreaOutline();
        nextVisualRefresh = 0f;   // redraw the dots and planned line straight away
    }

    private void UpdateAreaOutline()
    {
        if (areaLine == null) return;

        if (!UsesProbes())
        {
            areaLine.positionCount = 0;
            return;
        }

        Vector3 centre = CellToWorld(searchAreaCentre);
        float half = CellSize(searchAreaCentre) * (Mathf.Max(1, searchAreaRadius) + 0.5f);

        areaLine.positionCount = 4;
        areaLine.SetPosition(0, centre + new Vector3(-half, -half, 0f));
        areaLine.SetPosition(1, centre + new Vector3(-half, half, 0f));
        areaLine.SetPosition(2, centre + new Vector3(half, half, 0f));
        areaLine.SetPosition(3, centre + new Vector3(half, -half, 0f));
    }

    private void UpdateSearchVisuals()
    {
        if (searchVisualsRoot == null) return;

        searchVisualsRoot.SetActive(showSearchPath);

        // Re-check the widths now and then so they follow camera zoom / window resizing.
        if (Time.unscaledTime >= nextWidthRefresh)
        {
            nextWidthRefresh = Time.unscaledTime + 0.5f;
            ApplySearchLineWidths();
        }

        if (!showSearchPath || currentState != EnemyState.Search) return;

        // Extend the walked route by one point every time the enemy enters a new cell.
        Vector2Int cell = mover.GridPosition;
        if (cell != lastWalkedCell)
        {
            lastWalkedCell = cell;
            walkedPoints.Add(CellToWorld(cell));

            walkedLine.positionCount = walkedPoints.Count;
            walkedLine.SetPosition(walkedPoints.Count - 1, walkedPoints[walkedPoints.Count - 1]);
        }

        // Planned line and dots only need refreshing a few times a second.
        if (Time.unscaledTime < nextVisualRefresh) return;
        nextVisualRefresh = Time.unscaledTime + 0.1f;

        RefreshPlannedLine();
        RefreshMarkers();
    }

    private void RefreshPlannedLine()
    {
        int remaining = Mathf.Max(0, searchPoints.Count - currentSearchPointIndex);

        if (remaining == 0)
        {
            plannedLine.positionCount = 0;
            return;
        }

        plannedLine.positionCount = remaining + 1;
        plannedLine.SetPosition(0, CellToWorld(mover.GridPosition));

        for (int i = 0; i < remaining; i++)
            plannedLine.SetPosition(i + 1, CellToWorld(searchPoints[currentSearchPointIndex + i]));
    }

    private void RefreshMarkers()
    {
        while (searchMarkers.Count < searchPoints.Count)
        {
            GameObject go = new GameObject("Point " + (searchMarkers.Count + 1));
            go.transform.SetParent(searchVisualsRoot.transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.sharedMaterial = GetVisualMaterial();
            sr.sortingOrder = searchPathSortingOrder + 1;
            searchMarkers.Add(sr);
        }

        float cellSize = CellSize(mover.GridPosition);

        for (int i = 0; i < searchMarkers.Count; i++)
        {
            SpriteRenderer sr = searchMarkers[i];

            if (i >= searchPoints.Count)
            {
                sr.gameObject.SetActive(false);
                continue;
            }

            sr.gameObject.SetActive(true);
            sr.transform.position = CellToWorld(searchPoints[i]);

            bool isProbe = searchPointRing[i] < 0;
            bool isChecked = searchChecked.Contains(searchPoints[i]);
            bool isCurrent = i == currentSearchPointIndex;

            Color colour = isProbe ? ProbeMarkerColour : TrailMarkerColour;
            colour.a = isChecked ? 0.35f : 1f;
            sr.color = colour;

            float size = cellSize * (isCurrent ? 0.5f : 0.3f);
            sr.transform.localScale = new Vector3(size, size, 1f);
        }
    }

    // The search is over (found the player, gave up, or fell asleep).
    private void OnLeftSearch()
    {
        if (searchVisualsRoot == null) return;

        // The planned route no longer applies.
        plannedLine.positionCount = 0;

        if (keepPathAfterSearch) return;

        walkedLine.positionCount = 0;
        areaLine.positionCount = 0;
        foreach (SpriteRenderer sr in searchMarkers) sr.gameObject.SetActive(false);
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log(message);
    }

    private void OnDrawGizmosSelected()
    {
        if (TilemapGridSystem.Instance == null)
            return;

        Gizmos.DrawWireSphere(
            TilemapGridSystem.Instance.GridToWorld(patrolHome),
            0.2f
        );

        Gizmos.DrawWireSphere(
            TilemapGridSystem.Instance.GridToWorld(patrolAway),
            0.2f
        );

    }

    // Shows the search to students: every search point as a dot (bright = still to check,
    // dim = checked, big = the current target), the planned route as a line, and an
    // arrow for the player's last heading. Visible in the Scene view, and in the Game
    // view when the Gizmos button is on.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying ||
            currentState != EnemyState.Search ||
            TilemapGridSystem.Instance == null ||
            searchPoints.Count == 0)
            return;

        if (UsesProbes())
        {
            // The search area (outline) and every cell the enemy has already seen (faint fill).
            Vector3 centreWorld = TilemapGridSystem.Instance.GridToWorld(searchAreaCentre);
            float cell = Vector3.Distance(
                centreWorld,
                TilemapGridSystem.Instance.GridToWorld(searchAreaCentre + Vector2Int.right));
            if (cell <= 0.0001f) cell = 1f;

            int r = Mathf.Max(1, searchAreaRadius);

            Gizmos.color = new Color(searchColour.r, searchColour.g, searchColour.b, 0.08f);
            foreach (Vector2Int seen in searchCovered)
            {
                if (Mathf.Abs(seen.x - searchAreaCentre.x) > r ||
                    Mathf.Abs(seen.y - searchAreaCentre.y) > r) continue;

                Gizmos.DrawCube(
                    TilemapGridSystem.Instance.GridToWorld(seen),
                    new Vector3(cell * 0.9f, cell * 0.9f, 0.01f));
            }

            Gizmos.color = new Color(searchColour.r, searchColour.g, searchColour.b, 0.9f);
            Gizmos.DrawWireCube(centreWorld, new Vector3(cell * (2 * r + 1), cell * (2 * r + 1), 0.01f));
        }

        for (int i = 0; i < searchPoints.Count; i++)
        {
            Vector3 world = TilemapGridSystem.Instance.GridToWorld(searchPoints[i]);

            bool isChecked = searchChecked.Contains(searchPoints[i]);
            bool isCurrent = i == currentSearchPointIndex;

            Color c = searchColour;
            if (isChecked) c = new Color(searchColour.r, searchColour.g, searchColour.b, 0.25f);

            Gizmos.color = c;
            Gizmos.DrawSphere(world, isCurrent ? 0.22f : 0.1f);

            // Route line between consecutive points still ahead of the enemy.
            if (i + 1 < searchPoints.Count && i >= currentSearchPointIndex)
            {
                Gizmos.color = new Color(searchColour.r, searchColour.g, searchColour.b, 0.5f);
                Gizmos.DrawLine(world, TilemapGridSystem.Instance.GridToWorld(searchPoints[i + 1]));
            }
        }

        // Last known position and the heading the player was running in.
        Vector3 last = TilemapGridSystem.Instance.GridToWorld(lastKnownPlayerPosition);
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(last, Vector3.one * 0.4f);

        if (playerHeading != Vector2Int.zero)
        {
            Vector3 tip = last + new Vector3(playerHeading.x, playerHeading.y, 0f) * 0.9f;
            Gizmos.DrawLine(last, tip);
            Gizmos.DrawWireSphere(tip, 0.1f);
        }
    }

    // Lets a spawner / room generator assign the player after the enemy is
    // created, since the field is private for the Inspector.
    public void SetPlayer(TilemapGridMover p)
    {
        player = p;
        CachePlayerHealth();
    }

    // Overload for older scripts (e.g. RoomGenerator) that still pass a plain
    // GridMover. It uses the TilemapGridMover on the same GameObject, so the
    // shared caller does not need to change.
    public void SetPlayer(GridMover p)
    {
        if (p == null)
        {
            SetPlayer((TilemapGridMover)null);
            return;
        }

        TilemapGridMover tilemapMover = p.GetComponent<TilemapGridMover>();

        if (tilemapMover == null)
        {
            Debug.LogWarning(
                "FSMEnemyController.SetPlayer: the player has a GridMover but no " +
                "TilemapGridMover, so the enemy cannot track it."
            );
        }

        SetPlayer(tilemapMover);
    }
}