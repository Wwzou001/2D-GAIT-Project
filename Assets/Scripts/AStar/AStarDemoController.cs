using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(GridMover))]
public class AStarDemoController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GridMover player;

    [Header("Movement")]
    [SerializeField] private float stepDelay = 0.25f;

    [Header("Visualisation")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private float pathLineWidth = 0.08f;
    [SerializeField] private TMP_Text debugText;

    private GridMover mover;
    private LineRenderer lineRenderer;

    private List<Vector2Int> currentPath;
    private bool isFollowingPath = false;

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
            CalculateAndFollowPath();
        }
    }

    private void CalculateAndFollowPath()
    {
        // Make sure the player has been assigned
        if (player == null)
        {
            Debug.LogWarning(
                "AStarDemoController: Player has not been assigned."
            );

            return;
        }

        // Ghost's current grid position
        Vector2Int start = mover.GridPosition;

        // Player's current grid position becomes the A* goal
        Vector2Int goal = player.GridPosition;

        int exploredNodes;

        // AStarPathfinder is static, so call it directly
        currentPath = AStarPathfinder.FindPath(
            start,
            goal,
            out exploredNodes
        );

        // No valid path found
        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.Log(
                $"A*: No path found from {start} to player at {goal}."
            );

            if (debugText != null)
            {
                debugText.text =
                    $"A* PATHFINDING\n" +
                    $"Start: {start}\n" +
                    $"Player: {goal}\n" +
                    $"No path found\n" +
                    $"Nodes Explored: {exploredNodes}";
            }

            ClearPathLine();

            return;
        }

        Debug.Log(
            $"A*: Path found from {start} to player at {goal}. " +
            $"Path Length: {currentPath.Count}, " +
            $"Nodes Explored: {exploredNodes}"
        );

        // Update the teaching/debug text
        if (debugText != null)
        {
            debugText.text =
                $"A* PATHFINDING\n" +
                $"Start: {start}\n" +
                $"Target Player: {goal}\n" +
                $"Path Length: {currentPath.Count}\n" +
                $"Nodes Explored: {exploredNodes}";
        }

        // Draw the calculated path
        if (showPath)
        {
            DrawPath(currentPath);
        }
        else
        {
            ClearPathLine();
        }

        // Move the ghost along the path
        StartCoroutine(
            FollowPath(currentPath)
        );
    }

    private IEnumerator FollowPath(
        List<Vector2Int> path
    )
    {
        isFollowingPath = true;

        foreach (Vector2Int nextPosition in path)
        {
            yield return new WaitForSeconds(stepDelay);

            // Stop if the game has ended
            if (GameManager.Instance != null &&
                GameManager.Instance.GameOver)
            {
                break;
            }

            Vector2Int currentPosition =
                mover.GridPosition;

            Vector2Int difference =
                nextPosition - currentPosition;

            Direction direction;

            // Work out which direction the ghost
            // needs to move to reach the next A* node
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
                // Already on this position
                continue;
            }

            Debug.Log(
                $"A*: Moving from {currentPosition} " +
                $"to {nextPosition}"
            );

            mover.TryMove(direction);
        }

        isFollowingPath = false;

        Debug.Log(
            $"A*: Finished path. " +
            $"Ghost position = {mover.GridPosition}"
        );
    }

    private void SetupLineRenderer()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer =
                gameObject.AddComponent<LineRenderer>();
        }

        Shader spriteShader =
            Shader.Find("Sprites/Default");

        if (spriteShader != null)
        {
            lineRenderer.material =
                new Material(spriteShader);
        }

        lineRenderer.startWidth =
            pathLineWidth;

        lineRenderer.endWidth =
            pathLineWidth;

        lineRenderer.positionCount = 0;

        // Keep the path above most sprites
        lineRenderer.sortingOrder = 5;
    }

    private void DrawPath(
        List<Vector2Int> path
    )
    {
        if (lineRenderer == null ||
            GridSystem.Instance == null)
        {
            return;
        }

        // +1 because we also include
        // the ghost's starting position
        lineRenderer.positionCount =
            path.Count + 1;

        Vector3 startWorld =
            GridSystem.Instance.GridToWorld(
                mover.GridPosition
            );

        lineRenderer.SetPosition(
            0,
            startWorld
        );

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 worldPosition =
                GridSystem.Instance.GridToWorld(
                    path[i]
                );

            lineRenderer.SetPosition(
                i + 1,
                worldPosition
            );
        }
    }

    private void ClearPathLine()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
}