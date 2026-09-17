using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Teaching/demo controller for A*.
/// P starts a pathfinding demonstration to the player's current grid cell.
///
/// Parameters:
/// - stepDelay: time between grid moves so the path is easy to observe.
/// - heuristic: Manhattan or Euclidean H-cost calculation.
/// - movingObstacles: GridMover objects treated as temporary blocked cells.
/// - showExploredNodes: displays every expanded A* node with its G/H costs.
/// - replanForMovingObstacles: recalculates before each move so moving obstacles
///   can invalidate an old route without breaking the agent.
/// </summary>
[RequireComponent(typeof(GridMover))]
public class AStarDemoController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GridMover player;

    [Header("A* Parameters")]
    [SerializeField] private AStarPathfinder.HeuristicType heuristic =
        AStarPathfinder.HeuristicType.Manhattan;
    [SerializeField] private float stepDelay = 0.25f;

    [Header("Moving Obstacles")]
    [Tooltip("GridMover objects whose current cells should be blocked by A*.")]
    [SerializeField] private List<GridMover> movingObstacles = new List<GridMover>();
    [SerializeField] private bool replanForMovingObstacles = true;

    [Header("Teaching Visualisation")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showExploredNodes = true;
    [SerializeField] private float pathLineWidth = 0.08f;
    [SerializeField] private float nodeTextSize = 2.2f;
    [SerializeField] private TMP_Text debugText;

    private GridMover mover;
    private LineRenderer lineRenderer;
    private bool isFollowingPath;
    private readonly List<GameObject> nodeLabels = new List<GameObject>();

    private void Awake()
    {
        mover = GetComponent<GridMover>();
        SetupLineRenderer();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame &&
            !isFollowingPath)
        {
            StartCoroutine(FollowPlayerWithAStar());
        }
    }

    private IEnumerator FollowPlayerWithAStar()
    {
        if (player == null)
        {
            Debug.LogWarning("A*: Player has not been assigned.");
            yield break;
        }

        isFollowingPath = true;
        Vector2Int originalGoal = player.GridPosition;

        while (mover.GridPosition != originalGoal)
        {
            if (GameManager.Instance != null && GameManager.Instance.GameOver)
                break;

            // The player remains the demo goal. If you want the ghost to follow a
            // moving player too, replace originalGoal with player.GridPosition here.
            Vector2Int goal = originalGoal;
            HashSet<Vector2Int> blocked = GetMovingObstacleCells();

            AStarPathfinder.SearchResult result = AStarPathfinder.FindPath(
                mover.GridPosition,
                goal,
                heuristic,
                blocked);

            UpdateVisualisation(result, goal);

            if (!result.PathFound || result.Path.Count == 0)
            {
                Debug.LogWarning("A*: No currently valid path. Waiting for environment change.");
                yield return new WaitForSeconds(stepDelay);

                if (!replanForMovingObstacles)
                    break;

                continue;
            }

            Vector2Int nextPosition = result.Path[0];
            Direction direction;

            if (!TryGetDirection(mover.GridPosition, nextPosition, out direction))
                break;

            // Re-check the dynamic environment immediately before moving.
            blocked = GetMovingObstacleCells();
            if (blocked.Contains(nextPosition))
            {
                yield return new WaitForSeconds(stepDelay);
                continue;
            }

            mover.TryMove(direction);
            yield return new WaitForSeconds(stepDelay);

            if (!replanForMovingObstacles)
            {
                // With replanning disabled, this loop still calculates again on the
                // next step; keeping the flag exposed documents the intended mode.
            }
        }

        isFollowingPath = false;
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

    private void UpdateVisualisation(
        AStarPathfinder.SearchResult result,
        Vector2Int goal)
    {
        ClearNodeLabels();

        if (showPath && result.PathFound)
            DrawPath(result.Path);
        else
            ClearPathLine();

        if (showExploredNodes)
        {
            foreach (AStarPathfinder.NodeDebugInfo node in result.ExploredNodes)
                CreateNodeLabel(node);
        }

        if (debugText != null)
        {
            debugText.text =
                $"A* PATHFINDING\n" +
                $"Heuristic: {heuristic}\n" +
                $"Start: {mover.GridPosition}\n" +
                $"Goal: {goal}\n" +
                $"Path Length: {(result.PathFound ? result.Path.Count : 0)}\n" +
                $"Nodes Explored: {result.ExploredNodes.Count}\n" +
                $"Moving Obstacles: {GetMovingObstacleCells().Count}";
        }
    }

    private void CreateNodeLabel(AStarPathfinder.NodeDebugInfo node)
    {
        GameObject labelObject = new GameObject($"AStarNode_{node.Position.x}_{node.Position.y}");
        labelObject.transform.position = GridSystem.Instance.GridToWorld(node.Position) +
                                         new Vector3(0f, 0f, -0.2f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = $"G:{node.GCost:0.#}\nH:{node.HCost:0.#}";
        label.fontSize = nodeTextSize;
        label.alignment = TextAlignmentOptions.Center;
        label.sortingOrder = 20;

        nodeLabels.Add(labelObject);
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

    private void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            lineRenderer.material = new Material(spriteShader);

        lineRenderer.startWidth = pathLineWidth;
        lineRenderer.endWidth = pathLineWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.sortingOrder = 5;
    }

    private void DrawPath(List<Vector2Int> path)
    {
        lineRenderer.positionCount = path.Count + 1;
        lineRenderer.SetPosition(0, GridSystem.Instance.GridToWorld(mover.GridPosition));

        for (int i = 0; i < path.Count; i++)
            lineRenderer.SetPosition(i + 1, GridSystem.Instance.GridToWorld(path[i]));
    }

    private void ClearPathLine()
    {
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    private void ClearNodeLabels()
    {
        foreach (GameObject label in nodeLabels)
        {
            if (label != null)
                Destroy(label);
        }

        nodeLabels.Clear();
    }

    private void OnDestroy()
    {
        ClearNodeLabels();
    }
}
