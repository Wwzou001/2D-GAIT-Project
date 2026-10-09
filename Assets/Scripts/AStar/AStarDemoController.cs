using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Teaching/demo controller for A*.
/// Press P to walk to the player's cell using A*, and watch how the search works.
///
/// Works with either mover type: the agent can have a GridMover (original grid
/// scenes) or a TilemapGridMover (tilemap scenes), and so can the player.
///
/// Parameters:
/// - heuristic: how H is estimated. Press H in play mode to cycle through them
///   and see how the explored area changes for the same route.
/// - replanMode: when the agent recalculates its route while walking.
///     EveryStep     recalculates before every move (always up to date, costs the most).
///     EveryInterval recalculates every replanInterval seconds, or when blocked.
///     WhenBlocked   keeps its route until a moving obstacle blocks the next cell
///                   (event-driven, the cheapest). Compare "Replans" and "Compute time".
/// - movingObstacles / tilemapMovingObstacles: movers whose CURRENT cells A* treats as blocked.
/// - showExploredNodes: a heat map of every explored cell with its F, G and H costs.
///
/// Hotkeys (turn off with enableHotkeys):  P start   H heuristic   E labels   R replan mode   Z study zoom
/// </summary>
public class AStarDemoController : MonoBehaviour
{
    public enum ReplanMode { EveryStep, EveryInterval, WhenBlocked }

    private enum DemoStatus { Ready, Running, NoPath, Arrived, Stopped }

    [Header("Target")]
    [Tooltip("The player in the original fixed-grid scenes.")]
    [SerializeField] private GridMover player;
    [Tooltip("The player in tilemap scenes (use this one if the player has a TilemapGridMover).")]
    [SerializeField] private TilemapGridMover tilemapPlayer;

    [Header("A* Parameters")]
    [SerializeField] private AStarPathfinder.HeuristicType heuristic =
        AStarPathfinder.HeuristicType.Manhattan;
    [Tooltip("Seconds between grid moves, so the walk is easy to follow.")]
    [SerializeField] private float stepDelay = 0.25f;

    [Header("Replanning")]
    [SerializeField] private ReplanMode replanMode = ReplanMode.WhenBlocked;
    [Tooltip("Used by EveryInterval: seconds between recalculations.")]
    [SerializeField] private float replanInterval = 1f;

    [Header("Moving Obstacles")]
    [Tooltip("GridMover objects whose current cells should be blocked by A*.")]
    [SerializeField] private List<GridMover> movingObstacles = new List<GridMover>();
    [Tooltip("TilemapGridMover objects whose current cells should be blocked by A*.")]
    [SerializeField] private List<TilemapGridMover> tilemapMovingObstacles = new List<TilemapGridMover>();

    [Header("Auto-find (when drag and drop is not possible)")]
    [Tooltip("If no player is assigned above, use the object tagged 'Player' when P is pressed.")]
    [SerializeField] private bool autoFindPlayer = true;
    [Tooltip("If both obstacle lists are empty, use the object with this exact name as the moving obstacle. " +
             "Leave empty to turn this off.")]
    [SerializeField] private string movingObstacleObjectName = "MovingObstacle";

    [Header("Teaching Visualisation")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showExploredNodes = true;
    [Tooltip("P = start, H = next heuristic, E = show/hide labels, R = next replan mode.")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private float pathLineWidth = 0.08f;
    [Tooltip("The path line is never thinner than this many screen pixels (thin lines can vanish).")]
    [SerializeField] private float minLinePixels = 3f;
    [Tooltip("Size of the F / G / H numbers inside each explored cell (1 = default). " +
             "They are only readable when the camera is close - press Z for the study zoom.")]
    [SerializeField] private float nodeLabelScale = 1f;
    [Tooltip("Stop writing numbers into cells after this many (the heat map still draws every cell). " +
             "Each labelled cell is a separate text object, so very large searches get slow.")]
    [SerializeField] private int maxLabelledCells = 250;
    [Tooltip("The numbers are hidden while a cell is smaller than this many screen pixels, because " +
             "smaller than that they just smear together. Zoom in (Z) to see them.")]
    [SerializeField] private float labelMinCellPixels = 28f;
    [Tooltip("Also write the F cost (G + H) as a third line in each cell. Off by default so the " +
             "G and H numbers can be bigger. F is still shown by the heat colour and the panel.")]
    [SerializeField] private bool showFCost = false;
    [Tooltip("Study zoom (Z): the cell size, in screen pixels, it aims for. It zooms out only as " +
             "far as needed to fit the route, and never below 40 pixels per cell.")]
    [SerializeField] private float studyCellPixels = 56f;
    [Tooltip("Sorting order of the heat map. Labels draw above it, the path line above that.")]
    [SerializeField] private int visualSortingOrder = 3;
    [SerializeField] private TMP_Text debugText;
    [Tooltip("Font size of the info panel. 0 keeps the size set on the text object.")]
    [SerializeField] private float panelFontSize = 14f;
    [Tooltip("Draws a thin black outline around the panel text so it is readable on any background.")]
    [SerializeField] private bool improveTextContrast = true;

    private GridMover gridMover;
    private TilemapGridMover tilemapMover;
    private LineRenderer lineRenderer;
    private bool isFollowingPath;
    private DemoStatus status = DemoStatus.Ready;
    private string stuckMessage;   // set when the mover keeps refusing moves

    // Study zoom (camera moved in on the explored area)
    private bool studyZoomOn;
    private Vector3 savedCameraPosition;
    private float savedCameraSize;

    // Font size (TextMeshPro units) at which a 3-line F / G / H label fits inside one
    // 1-unit cell without touching its neighbours. Use Node Label Scale to adjust.
    private const float LabelFontTwoLines = 3.1f;     // G and H only
    private const float LabelFontThreeLines = 2.3f;   // F, G and H

    private readonly List<GameObject> labelObjects = new List<GameObject>();
    private bool labelsVisible;
    private float nextLabelCheck;

    // Snapshot of the ORIGINAL search started when P is pressed. The agent may replan
    // while walking, but the teaching display keeps this first search so the explored
    // nodes and costs stay stable while you study them.
    private Vector2Int demoStart;
    private Vector2Int demoGoal;
    private AStarPathfinder.SearchResult initialSearchResult;

    private List<Vector2Int> currentPath;
    private int pathIndex;
    private int replanCount;
    private float lastComputeMs;
    private float lastPlanTime;

    private GameObject visualRoot;
    private Material visualMaterial;
    private Sprite squareSprite;

    private const string MutedHex = "D3D9DF";
    private const string GoodHex = "5CD65C";
    private const string WarnHex = "F2C94C";
    private const string BadHex = "FF5A5A";
    private const string AccentHex = "4D99FF";

    // ---------------------------------------------------------------
    // Setup
    // ---------------------------------------------------------------

    private void Awake()
    {
        gridMover = GetComponent<GridMover>();
        tilemapMover = GetComponent<TilemapGridMover>();

        if (gridMover == null && tilemapMover == null)
        {
            Debug.LogError(
                "AStarDemoController: " + gameObject.name +
                " has neither a GridMover nor a TilemapGridMover.",
                this
            );
        }

        SetupLineRenderer();

        if (debugText != null)
        {
            StylePanelText(debugText);
        }
    }

    private void Start()
    {
        RefreshPanel();
    }

#pragma warning disable 0618   // enableWordWrapping is obsolete in newer TextMeshPro but still works
    // One line per row (no wrapping), outlined so it is readable on any background.
    private void StylePanelText(TMP_Text text)
    {
        text.richText = true;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        if (panelFontSize > 0f) text.fontSize = panelFontSize;

        if (improveTextContrast)
        {
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }
    }
#pragma warning restore 0618

    private void OnDisable()
    {
        if (studyZoomOn) ToggleStudyZoom();
    }

    private void OnDestroy()
    {
        ClearVisuals();

        if (visualMaterial != null) Destroy(visualMaterial);

        if (squareSprite != null)
        {
            Destroy(squareSprite.texture);
            Destroy(squareSprite);
        }
    }

    // ---------------------------------------------------------------
    // Mover helpers (work with either mover type)
    // ---------------------------------------------------------------

    private Vector2Int AgentCell
    {
        get
        {
            if (tilemapMover != null) return tilemapMover.GridPosition;
            return gridMover != null ? gridMover.GridPosition : Vector2Int.zero;
        }
    }

    // Fills in the player (and the moving obstacle) automatically if the Inspector
    // slots are empty, so the demo still works when drag and drop is not possible.
    private void AutoFindReferences()
    {
        if (autoFindPlayer && player == null && tilemapPlayer == null)
        {
            GameObject found = GameObject.FindWithTag("Player");

            if (found != null)
            {
                tilemapPlayer = found.GetComponent<TilemapGridMover>();

                if (tilemapPlayer == null)
                    player = found.GetComponent<GridMover>();

                if (tilemapPlayer != null || player != null)
                    Debug.Log($"A*: no player was assigned, so '{found.name}' (tag Player) is the target.", this);
                else
                    Debug.LogWarning(
                        $"A*: found '{found.name}' with tag Player, but it has no " +
                        "TilemapGridMover or GridMover component.", this);
            }
        }

        if (!string.IsNullOrEmpty(movingObstacleObjectName) &&
            movingObstacles.Count == 0 && tilemapMovingObstacles.Count == 0)
        {
            GameObject obstacle = GameObject.Find(movingObstacleObjectName);

            if (obstacle != null)
            {
                TilemapGridMover tilemapObstacle = obstacle.GetComponent<TilemapGridMover>();
                GridMover gridObstacle = obstacle.GetComponent<GridMover>();

                if (tilemapObstacle != null) tilemapMovingObstacles.Add(tilemapObstacle);
                else if (gridObstacle != null) movingObstacles.Add(gridObstacle);
            }
        }
    }

    private bool TryGetPlayerCell(out Vector2Int cell)
    {
        AutoFindReferences();

        if (tilemapPlayer != null)
        {
            cell = tilemapPlayer.GridPosition;
            return true;
        }

        if (player != null)
        {
            cell = player.GridPosition;
            return true;
        }

        cell = Vector2Int.zero;
        return false;
    }

    private void TryStep(Direction direction)
    {
        if (tilemapMover != null) tilemapMover.TryMove(direction);
        else if (gridMover != null) gridMover.TryMove(direction);
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

    private float CellSize(Vector2Int cell)
    {
        float size = Vector3.Distance(CellToWorld(cell), CellToWorld(cell + Vector2Int.right));
        return size > 0.0001f ? size : 1f;
    }

    private HashSet<Vector2Int> GetMovingObstacleCells()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        foreach (GridMover obstacle in movingObstacles)
        {
            if (obstacle == null || obstacle == gridMover || obstacle == player)
                continue;

            blocked.Add(obstacle.GridPosition);
        }

        foreach (TilemapGridMover obstacle in tilemapMovingObstacles)
        {
            if (obstacle == null || obstacle == tilemapMover || obstacle == tilemapPlayer)
                continue;

            blocked.Add(obstacle.GridPosition);
        }

        return blocked;
    }

    // ---------------------------------------------------------------
    // Input
    // ---------------------------------------------------------------

    private void Update()
    {
        UpdateLabelVisibility();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.pKey.wasPressedThisFrame && !isFollowingPath)
            StartCoroutine(RunDemo());

        if (!enableHotkeys) return;

        if (keyboard.hKey.wasPressedThisFrame) CycleHeuristic();

        if (keyboard.eKey.wasPressedThisFrame)
        {
            showExploredNodes = !showExploredNodes;
            RefreshVisuals();
            RefreshPanel();
        }

        if (keyboard.zKey.wasPressedThisFrame) ToggleStudyZoom();

        if (keyboard.rKey.wasPressedThisFrame)
        {
            replanMode = (ReplanMode)(((int)replanMode + 1) % System.Enum.GetValues(typeof(ReplanMode)).Length);
            RefreshPanel();
        }
    }

    // Study zoom: moves the camera in on the explored cells so the F / G / H numbers
    // are big enough to read. Press Z again to put the camera back exactly as it was.
    // (If another script moves the camera every frame it will fight this.)
    private void ToggleStudyZoom()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        if (studyZoomOn)
        {
            cam.transform.position = savedCameraPosition;
            cam.orthographicSize = savedCameraSize;
            studyZoomOn = false;
            RefreshPanel();
            return;
        }

        if (initialSearchResult == null || initialSearchResult.ExploredNodes.Count == 0)
            return;

        // Frame the ROUTE (start to goal) when there is one. Fitting every explored cell would
        // zoom out too far for the numbers to be readable.
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

        if (initialSearchResult.PathFound && initialSearchResult.Path.Count > 0)
        {
            Vector3 startWorld = CellToWorld(demoStart);
            min = Vector3.Min(min, startWorld);
            max = Vector3.Max(max, startWorld);

            foreach (Vector2Int pathCell in initialSearchResult.Path)
            {
                Vector3 world = CellToWorld(pathCell);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
        }
        else
        {
            foreach (AStarPathfinder.NodeDebugInfo node in initialSearchResult.ExploredNodes)
            {
                Vector3 world = CellToWorld(node.Position);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
        }

        float cell = CellSize(demoStart);

        savedCameraPosition = cam.transform.position;
        savedCameraSize = cam.orthographicSize;

        // Centre on the explored area and fit it in view, with one cell of margin.
        Vector3 centre = (min + max) * 0.5f;
        centre.z = cam.transform.position.z;

        float halfHeight = (max.y - min.y) * 0.5f + cell * 1.5f;
        float halfWidth = (max.x - min.x) * 0.5f + cell * 1.5f;
        float fit = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.1f, cam.aspect));

        // Camera size that gives a cell of N screen pixels: size = cell x screenHeight / (2 x N).
        float screenHeight = Mathf.Max(1, cam.pixelHeight);
        float closest = cell * screenHeight / (2f * Mathf.Max(20f, studyCellPixels));   // biggest cells we aim for
        float farthest = cell * screenHeight / (2f * 40f);                              // never below 40 px per cell

        // Fit the route, but keep the cells between those two limits and never zoom OUT.
        float size = Mathf.Clamp(fit, closest, farthest);

        cam.transform.position = centre;
        cam.orthographicSize = Mathf.Min(savedCameraSize, size);

        studyZoomOn = true;
        RefreshPanel();
    }

    private void CycleHeuristic()
    {
        int count = System.Enum.GetValues(typeof(AStarPathfinder.HeuristicType)).Length;
        heuristic = (AStarPathfinder.HeuristicType)(((int)heuristic + 1) % count);

        // Re-run the same search with the new heuristic so the two can be compared directly.
        if (initialSearchResult != null)
        {
            initialSearchResult = RunSearch(demoStart, demoGoal, GetMovingObstacleCells());
            RefreshVisuals();
        }

        RefreshPanel();
    }

    // ---------------------------------------------------------------
    // The demo
    // ---------------------------------------------------------------

    private IEnumerator RunDemo()
    {
        if (gridMover == null && tilemapMover == null)
        {
            Debug.LogWarning("A*: this object has no GridMover or TilemapGridMover.");
            yield break;
        }

        Vector2Int goal;
        if (!TryGetPlayerCell(out goal))
        {
            Debug.LogWarning(
                "A*: no player assigned. Fill in 'Player' (GridMover) or " +
                "'Tilemap Player' (TilemapGridMover) in the Inspector.",
                this
            );
            status = DemoStatus.Stopped;
            RefreshPanel();
            yield break;
        }

        isFollowingPath = true;
        replanCount = 0;

        // Capture one complete search for the teaching display.
        demoStart = AgentCell;
        demoGoal = goal;
        initialSearchResult = RunSearch(demoStart, demoGoal, GetMovingObstacleCells());

        lastPlanTime = Time.time;
        replanCount = 1;

        currentPath = initialSearchResult.PathFound
            ? new List<Vector2Int>(initialSearchResult.Path)
            : null;
        pathIndex = 0;
        status = initialSearchResult.PathFound ? DemoStatus.Running : DemoStatus.NoPath;
        stuckMessage = null;

        Debug.Log(
            $"A*: {demoStart} to {demoGoal} with {heuristic}: " +
            $"path found = {initialSearchResult.PathFound}, " +
            $"nodes explored = {initialSearchResult.ExploredNodes.Count}, " +
            $"took {lastComputeMs:0.000} ms.", this);

        RefreshVisuals();
        RefreshPanel();

        WaitForSeconds wait = new WaitForSeconds(stepDelay);
        int refusedMoves = 0;
        int noPathTries = 0;

        while (AgentCell != goal)
        {
            if (GameManager.Instance != null && GameManager.Instance.GameOver)
                break;

            HashSet<Vector2Int> blocked = GetMovingObstacleCells();

            // Decide whether the route needs recalculating.
            bool needPlan = currentPath == null || pathIndex >= currentPath.Count;

            if (!needPlan)
            {
                bool nextBlocked = blocked.Contains(currentPath[pathIndex]);

                switch (replanMode)
                {
                    case ReplanMode.EveryStep:
                        needPlan = true;
                        break;

                    case ReplanMode.EveryInterval:
                        needPlan = nextBlocked || Time.time - lastPlanTime >= replanInterval;
                        break;

                    default: // WhenBlocked: only react to a moving obstacle
                        needPlan = nextBlocked;
                        break;
                }
            }

            if (needPlan)
            {
                AStarPathfinder.SearchResult result = RunSearch(AgentCell, goal, blocked);

                lastPlanTime = Time.time;
                replanCount++;

                if (!result.PathFound || result.Path.Count == 0)
                {
                    currentPath = null;
                    status = DemoStatus.NoPath;
                    UpdatePathLine();
                    RefreshPanel();

                    // Wait for the environment to change, but not forever.
                    if (++noPathTries >= 40) break;

                    yield return wait;
                    continue;
                }

                noPathTries = 0;
                currentPath = result.Path;
                pathIndex = 0;
                status = DemoStatus.Running;
            }

            Vector2Int step = currentPath[pathIndex];
            Vector2Int before = AgentCell;

            // Already standing on this cell (e.g. moved by something else): skip it.
            if (before == step)
            {
                pathIndex++;
                continue;
            }

            // Re-check the dynamic environment immediately before moving.
            if (GetMovingObstacleCells().Contains(step))
            {
                currentPath = null;
                yield return wait;
                continue;
            }

            Direction direction;
            if (!TryGetDirection(before, step, out direction))
            {
                currentPath = null;
                yield return wait;
                continue;
            }

            TryStep(direction);
            yield return wait;

            if (AgentCell != before)
            {
                pathIndex++;
                refusedMoves = 0;
                stuckMessage = null;
            }
            else if (++refusedMoves >= 3)
            {
                // The mover keeps refusing this move: plan again from scratch.
                stuckMessage = $"the mover refused to step {direction} from {before}";
                Debug.LogWarning(
                    $"A*: {stuckMessage} 3 times in a row. The agent is blocked, or its " +
                    "TilemapGridMover will not move it (is it enabled, and is the next cell walkable?).",
                    this);

                currentPath = null;
                refusedMoves = 0;
            }

            UpdatePathLine();
            RefreshPanel();
        }

        isFollowingPath = false;
        status = AgentCell == goal ? DemoStatus.Arrived : DemoStatus.Stopped;
        currentPath = null;
        UpdatePathLine();
        RefreshPanel();
    }

    // Runs one A* search and records how long it took (shown as "Compute time").
    // Timed here, so this script works with any version of AStarPathfinder.
    private AStarPathfinder.SearchResult RunSearch(
        Vector2Int start,
        Vector2Int goal,
        HashSet<Vector2Int> blocked)
    {
        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        AStarPathfinder.SearchResult result =
            AStarPathfinder.FindPath(start, goal, heuristic, blocked);

        timer.Stop();
        lastComputeMs = (float)timer.Elapsed.TotalMilliseconds;

        return result;
    }

    private bool TryGetDirection(Vector2Int from, Vector2Int to, out Direction direction)
    {
        Vector2Int difference = to - from;

        if (difference.x > 0) direction = Direction.Right;
        else if (difference.x < 0) direction = Direction.Left;
        else if (difference.y > 0) direction = Direction.Up;
        else if (difference.y < 0) direction = Direction.Down;
        else
        {
            direction = Direction.Up;
            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------
    // Info panel (TextMeshPro rich text)
    // ---------------------------------------------------------------

    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append("<nobr><color=#").Append(MutedHex).Append('>')
          .Append(label).Append("</color>  ").Append(value).Append("</nobr>\n");
    }

    private string StatusHex()
    {
        switch (status)
        {
            case DemoStatus.Running: return AccentHex;
            case DemoStatus.NoPath: return BadHex;
            case DemoStatus.Arrived: return GoodHex;
            case DemoStatus.Stopped: return WarnHex;
            default: return MutedHex;
        }
    }

    private string StatusLabel()
    {
        switch (status)
        {
            case DemoStatus.Running: return "WALKING THE PATH";
            case DemoStatus.NoPath: return "NO PATH";
            case DemoStatus.Arrived: return "ARRIVED";
            case DemoStatus.Stopped: return "STOPPED";
            default: return "READY - PRESS P";
        }
    }

    private string ReplanLabel()
    {
        switch (replanMode)
        {
            case ReplanMode.EveryStep: return "Every step";
            case ReplanMode.EveryInterval: return $"Every {replanInterval:0.#} s";
            default: return "Only when blocked";
        }
    }

    private static string Swatch(string hex, string text)
    {
        return "<color=#" + hex + ">■</color> " + text;
    }

    private void RefreshPanel()
    {
        if (debugText == null) return;

        StringBuilder sb = new StringBuilder(512);

        sb.Append("<nobr><size=75%><b><color=#").Append(MutedHex).Append(">A* PATHFINDING</color></b></size></nobr>\n");
        sb.Append("<nobr><size=140%><b><color=#").Append(StatusHex()).Append('>')
          .Append(StatusLabel()).Append("</color></b></size></nobr>\n");

        AppendRow(sb, "Heuristic", heuristic.ToString());
        AppendRow(sb, "Replan", ReplanLabel());

        if (initialSearchResult != null)
        {
            AppendRow(sb, "Route", $"{demoStart} to {demoGoal}");

            if (initialSearchResult.PathFound)
            {
                float g = GetGoalGCost(initialSearchResult);
                float h = GetGoalHCost(initialSearchResult);

                AppendRow(sb, "Path",
                    $"{initialSearchResult.Path.Count} steps  |  {initialSearchResult.ExploredNodes.Count} cells explored");
                AppendRow(sb, "Cost",
                    $"<color=#8FD694>G {g:0.#}</color>  <color=#F2C94C>H {h:0.#}</color>  F {g + h:0.#}");
            }
            else
            {
                AppendRow(sb, "Path", $"none  |  {initialSearchResult.ExploredNodes.Count} cells explored");
            }

            AppendRow(sb, "Time", $"{lastComputeMs:0.000} ms  |  {replanCount} search(es)");
        }

        AppendRow(sb, "Obstacles", GetMovingObstacleCells().Count.ToString());

        if (initialSearchResult != null && showExploredNodes && labelObjects.Count > 0 && !labelsVisible)
        {
            sb.Append("<nobr><size=85%><color=#").Append(WarnHex)
              .Append(studyZoomOn
                  ? ">Cells still too small for numbers: enlarge the Game view</color></size></nobr>\n"
                  : ">F / G / H numbers hidden: press Z to zoom in</color></size></nobr>\n");
        }

        // Plain-English reasons, so a stuck demo explains itself.
        if (status == DemoStatus.NoPath &&
            initialSearchResult != null &&
            !initialSearchResult.PathFound &&
            initialSearchResult.ExploredNodes.Count == 0 &&
            demoStart != demoGoal)
        {
            sb.Append("<nobr><size=85%><color=#").Append(BadHex)
              .Append(">Searched 0 cells: the pathfinder cannot see the grid.</color></size></nobr>\n");
            sb.Append("<nobr><size=85%><color=#").Append(BadHex)
              .Append(">Use the new AStarPathfinder.cs (tilemap support),</color></size></nobr>\n");
            sb.Append("<nobr><size=85%><color=#").Append(BadHex)
              .Append(">and check the goal cell is walkable.</color></size></nobr>\n");
        }

        if (!string.IsNullOrEmpty(stuckMessage) && status == DemoStatus.Running)
        {
            sb.Append("<nobr><size=85%><color=#").Append(WarnHex)
              .Append(">Stuck: ").Append(stuckMessage).Append("</color></size></nobr>\n");
        }

        // Legend (one line): markers, then the heat scale.
        sb.Append("<nobr><size=80%>")
          .Append(Swatch("33CC66", "start")).Append("  ")
          .Append(Swatch("FF4D4D", "goal")).Append("  ")
          .Append(Swatch(AccentHex, "path")).Append("   ")
          .Append(Swatch("5CD65C", "low F")).Append(" ")
          .Append(Swatch(WarnHex, "mid")).Append(" ")
          .Append(Swatch(BadHex, "high F"))
          .Append("</size></nobr>\n");

        if (enableHotkeys)
        {
            sb.Append("<nobr><size=75%><color=#").Append(MutedHex)
              .Append(">P start   H heuristic   E labels   R replan   Z zoom</color></size></nobr>");
        }

        debugText.text = sb.ToString();
    }

    private float GetGoalGCost(AStarPathfinder.SearchResult result)
    {
        if (result == null || !result.PathFound || result.ExploredNodes.Count == 0)
            return 0f;

        // The goal is the final explored node when A* succeeds.
        return result.ExploredNodes[result.ExploredNodes.Count - 1].GCost;
    }

    private float GetGoalHCost(AStarPathfinder.SearchResult result)
    {
        if (result == null || !result.PathFound || result.ExploredNodes.Count == 0)
            return 0f;

        return result.ExploredNodes[result.ExploredNodes.Count - 1].HCost;
    }

    // ---------------------------------------------------------------
    // Scene visuals: heat map of explored cells + labels + path line
    // ---------------------------------------------------------------

    private Material GetVisualMaterial()
    {
        if (visualMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) visualMaterial = new Material(shader);
        }
        return visualMaterial;
    }

    private Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        // 4 pixels per unit, so the sprite is exactly 1 world unit wide.
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return squareSprite;
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot == null)
            visualRoot = new GameObject($"A* Visuals ({name})");
    }

    private void ClearVisuals()
    {
        if (visualRoot != null) Destroy(visualRoot);
        visualRoot = null;
        labelObjects.Clear();
    }

    // How many screen pixels wide one grid cell currently is.
    private float CellPixelSize()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return 9999f;

        return CellSize(demoStart) * cam.pixelHeight / (2f * cam.orthographicSize);
    }

    // Shows the F / G / H numbers only when cells are big enough to read them.
    private void UpdateLabelVisibility()
    {
        if (labelObjects.Count == 0 || Time.unscaledTime < nextLabelCheck) return;
        nextLabelCheck = Time.unscaledTime + 0.2f;

        bool show = showExploredNodes && CellPixelSize() >= labelMinCellPixels;
        if (show == labelsVisible) return;

        labelsVisible = show;

        foreach (GameObject label in labelObjects)
            if (label != null) label.SetActive(show);

        RefreshPanel();
    }

    private void AddSquare(Vector2Int cell, Color colour, float size, int order)
    {
        GameObject go = new GameObject($"Cell {cell.x},{cell.y}");
        go.transform.SetParent(visualRoot.transform, false);
        go.transform.position = CellToWorld(cell);
        go.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.sharedMaterial = GetVisualMaterial();
        sr.color = colour;
        sr.sortingOrder = order;
    }

    // Green (cheap) -> yellow -> red (expensive).
    private static Color HeatColour(float t)
    {
        Color green = new Color(0.36f, 0.84f, 0.36f);
        Color yellow = new Color(0.95f, 0.79f, 0.30f);
        Color red = new Color(1.00f, 0.35f, 0.35f);

        Color c = t < 0.5f
            ? Color.Lerp(green, yellow, t * 2f)
            : Color.Lerp(yellow, red, (t - 0.5f) * 2f);

        c.a = 0.32f;
        return c;
    }

    private void RefreshVisuals()
    {
        ClearVisuals();

        if (initialSearchResult == null)
        {
            ClearPathLine();
            return;
        }

        EnsureVisualRoot();

        float cell = CellSize(demoStart);
        float size = cell * 0.92f;

        // Heat map of every explored cell, coloured by F cost.
        if (showExploredNodes && initialSearchResult.ExploredNodes.Count > 0)
        {
            float minF = float.MaxValue;
            float maxF = float.MinValue;

            foreach (AStarPathfinder.NodeDebugInfo node in initialSearchResult.ExploredNodes)
            {
                minF = Mathf.Min(minF, node.FCost);
                maxF = Mathf.Max(maxF, node.FCost);
            }

            float range = Mathf.Max(0.0001f, maxF - minF);

            int labelled = 0;
            labelsVisible = CellPixelSize() >= labelMinCellPixels;

            foreach (AStarPathfinder.NodeDebugInfo node in initialSearchResult.ExploredNodes)
            {
                float t = (node.FCost - minF) / range;
                AddSquare(node.Position, HeatColour(t), size, visualSortingOrder);

                if (labelled < maxLabelledCells)
                {
                    CreateNodeLabel(node, cell);
                    labelled++;
                }
            }
        }

        // The route A* found, then the start and goal on top.
        if (initialSearchResult.PathFound)
        {
            foreach (Vector2Int pathCell in initialSearchResult.Path)
                AddSquare(pathCell, new Color(0.30f, 0.60f, 1.00f, 0.38f), size, visualSortingOrder + 1);
        }

        AddSquare(demoStart, new Color(0.20f, 0.80f, 0.40f, 0.70f), size, visualSortingOrder + 1);
        AddSquare(demoGoal, new Color(1.00f, 0.30f, 0.30f, 0.70f), size, visualSortingOrder + 1);

        UpdatePathLine();
    }

    // Label: F in bold on top, then G (green) and H (yellow) underneath.
    private void CreateNodeLabel(AStarPathfinder.NodeDebugInfo node, float cellSize)
    {
        GameObject labelObject = new GameObject($"AStarNode_{node.Position.x}_{node.Position.y}");
        labelObject.transform.SetParent(visualRoot.transform, false);
        labelObject.transform.position = CellToWorld(node.Position) + new Vector3(0f, 0f, -0.2f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.richText = true;

        // G (green) and H (yellow) on their own lines. F (white) is an optional third line:
        // with only two lines the text can be about 40% bigger, which is what makes it readable.
        string text =
            $"<color=#8FE39A><b>G{node.GCost:0.#}</b></color>\n" +
            $"<color=#FFD84D><b>H{node.HCost:0.#}</b></color>";

        if (showFCost)
            text = $"<b>F{node.FCost:0.#}</b>\n" + text;

        label.text = text;

        float baseSize = showFCost ? LabelFontThreeLines : LabelFontTwoLines;
        label.fontSize = baseSize * Mathf.Max(0.1f, nodeLabelScale) * cellSize;
        label.alignment = TextAlignmentOptions.Center;
        label.sortingOrder = 20;
        label.raycastTarget = false;

        labelObjects.Add(labelObject);
        labelObject.SetActive(labelsVisible);

        if (improveTextContrast)
        {
            // A thin dark outline keeps the numbers readable on the orange floor.
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 255);
        }
    }

    private void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            lineRenderer.material = new Material(spriteShader);

        lineRenderer.startColor = new Color(0.30f, 0.60f, 1.00f);
        lineRenderer.endColor = new Color(0.30f, 0.60f, 1.00f);
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;
        lineRenderer.startWidth = pathLineWidth;
        lineRenderer.endWidth = pathLineWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.sortingOrder = visualSortingOrder + 3;
    }

    // Keeps the line at least minLinePixels thick on screen (thin lines can vanish in parts).
    private void ApplyLineWidth()
    {
        float width = pathLineWidth;

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float minWorld = minLinePixels * (2f * cam.orthographicSize) / Mathf.Max(1, cam.pixelHeight);
            width = Mathf.Max(width, minWorld);
        }

        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }

    // Draws the part of the route the agent still has to walk.
    private void UpdatePathLine()
    {
        if (lineRenderer == null) return;

        ApplyLineWidth();

        if (!showPath || currentPath == null || pathIndex >= currentPath.Count)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        int remaining = currentPath.Count - pathIndex;
        lineRenderer.positionCount = remaining + 1;
        lineRenderer.SetPosition(0, CellToWorld(AgentCell));

        for (int i = 0; i < remaining; i++)
            lineRenderer.SetPosition(i + 1, CellToWorld(currentPath[pathIndex + i]));
    }

    private void ClearPathLine()
    {
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }
}