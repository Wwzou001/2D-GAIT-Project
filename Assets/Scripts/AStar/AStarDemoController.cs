using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// A* demonstration controller.
///
/// Press P to:
/// 1. Calculate a path
/// 2. Visualise the final path
/// 3. Display debug statistics
/// 4. Move the agent along the path
/// </summary>

[RequireComponent(typeof(GridMover))]
public class AStarDemoController : MonoBehaviour
{
    [Header("A* Demo")]

    [SerializeField]
    private Vector2Int goal =
        new Vector2Int(4, 4);

    [SerializeField]
    private float stepDelay =
        0.25f;


    [Header("Path Visualisation")]

    [SerializeField]
    private bool showPath =
        true;

    [SerializeField]
    private float pathLineWidth =
        0.08f;


    [Header("Teaching / Debug UI")]

    // Optional TMP text.
    [SerializeField]
    private TMP_Text debugText;


    private GridMover mover;

    private List<Vector2Int>
        currentPath =
            new List<Vector2Int>();

    private bool followingPath;

    private int exploredNodes;

    private LineRenderer lineRenderer;


     
    // INITIALISE
     

    private void Awake()
    {
        mover =
            GetComponent<GridMover>();

        SetupLineRenderer();
    }

     
    // INPUT
     

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.pKey
                .wasPressedThisFrame &&
            !followingPath)
        {
            CalculateAndFollowPath();
        }
    }

     
    // CALCULATE PATH
     

    public void CalculateAndFollowPath()
    {
        Vector2Int start =
            mover.GridPosition;

        currentPath =
            AStarPathfinder.FindPath(
                start,
                goal,
                out exploredNodes
            );

        if (currentPath == null ||
            currentPath.Count == 0)
        {
            Debug.LogWarning(
                "A* Demo: No path available."
            );

            UpdateDebugText(
                start,
                false
            );

            ClearPathVisual();

            return;
        }

        Debug.Log(
            $"A* DEMO\n" +
            $"Start: {start}\n" +
            $"Goal: {goal}\n" +
            $"Path length: " +
            $"{currentPath.Count}\n" +
            $"Nodes explored: " +
            $"{exploredNodes}"
        );

        UpdateDebugText(
            start,
            true
        );

        DrawPath(
            start
        );

        StopAllCoroutines();

        StartCoroutine(
            FollowPath()
        );
    }

     
    // FOLLOW PATH
     

    private IEnumerator FollowPath()
    {
        followingPath = true;

        foreach (
            Vector2Int nextCell
            in currentPath)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.GameOver)
            {
                break;
            }

            // Save old position BEFORE moving.
            Vector2Int previousPosition =
                mover.GridPosition;

            Direction direction =
                DirectionFromTo(
                    previousPosition,
                    nextCell
                );

            bool moved =
                mover.TryMove(
                    direction
                );

            Debug.Log(
                $"A* STEP: " +
                $"{previousPosition} -> " +
                $"{nextCell}, " +
                $"Moved = {moved}"
            );

            if (!moved)
            {
                Debug.LogWarning(
                    "A* Demo: Path became " +
                    "blocked. Recalculate path."
                );

                break;
            }

            yield return
                new WaitForSeconds(
                    stepDelay
                );
        }

        followingPath = false;

        Debug.Log(
            "A* Demo: movement complete."
        );
    }

     
    // CONVERT PATH STEP TO GRIDMOVER DIRECTION
     

    private Direction DirectionFromTo(
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int delta =
            to - from;

        if (delta == Vector2Int.up)
            return Direction.Up;

        if (delta == Vector2Int.down)
            return Direction.Down;

        if (delta == Vector2Int.left)
            return Direction.Left;

        return Direction.Right;
    }

     
    // LINE RENDERER SETUP
     

    private void SetupLineRenderer()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer =
                gameObject.AddComponent
                    <LineRenderer>();
        }

        lineRenderer.positionCount = 0;

        lineRenderer.startWidth =
            pathLineWidth;

        lineRenderer.endWidth =
            pathLineWidth;

        lineRenderer.useWorldSpace =
            true;

        lineRenderer.sortingOrder =
            5;

        // Create a basic material automatically.
        if (lineRenderer.material == null)
        {
            Shader shader =
                Shader.Find(
                    "Sprites/Default"
                );

            if (shader != null)
            {
                lineRenderer.material =
                    new Material(shader);
            }
        }
    }

     
    // DRAW FINAL PATH IN GAME VIEW
     

    private void DrawPath(
        Vector2Int start)
    {
        if (!showPath ||
            lineRenderer == null)
        {
            return;
        }

        // +1 because path does not include start.
        lineRenderer.positionCount =
            currentPath.Count + 1;

        Vector3 startWorld =
            GridSystem.Instance
                .GridToWorld(start);

        // Slightly in front of the grid.
        startWorld.z = -0.1f;

        lineRenderer.SetPosition(
            0,
            startWorld
        );

        for (int i = 0;
             i < currentPath.Count;
             i++)
        {
            Vector3 worldPosition =
                GridSystem.Instance
                    .GridToWorld(
                        currentPath[i]
                    );

            worldPosition.z = -0.1f;

            lineRenderer.SetPosition(
                i + 1,
                worldPosition
            );
        }
    }

    private void ClearPathVisual()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount =
                0;
        }
    }

     
    // DEBUG UI
     

    private void UpdateDebugText(
        Vector2Int start,
        bool pathFound)
    {
        if (debugText == null)
            return;

        if (pathFound)
        {
            debugText.text =
                "A* PATHFINDING\n" +
                $"Start: {start}\n" +
                $"Goal: {goal}\n" +
                $"Path Length: " +
                $"{currentPath.Count}\n" +
                $"Nodes Explored: " +
                $"{exploredNodes}";
        }
        else
        {
            debugText.text =
                "A* PATHFINDING\n" +
                $"Start: {start}\n" +
                $"Goal: {goal}\n" +
                "No valid path found.";
        }
    }

     
    // SCENE VIEW GIZMOS
     

    private void OnDrawGizmos()
    {
        if (currentPath == null ||
            GridSystem.Instance == null)
        {
            return;
        }

        for (int i = 0;
             i < currentPath.Count;
             i++)
        {
            Vector3 world =
                GridSystem.Instance
                    .GridToWorld(
                        currentPath[i]
                    );

            Gizmos.DrawWireSphere(
                world,
                0.15f
            );

            if (i > 0)
            {
                Vector3 previous =
                    GridSystem.Instance
                        .GridToWorld(
                            currentPath[i - 1]
                        );

                Gizmos.DrawLine(
                    previous,
                    world
                );
            }
        }
    }
}