using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Demo controller for the Sprint 2 A* skeleton.
/// Attach to a GameObject that already has GridMover.
/// Set a goal cell in the Inspector and press P to calculate/follow the path.


[RequireComponent(typeof(GridMover))]
public class AStarDemoController : MonoBehaviour
{
    [Header("A* Demo")]
    [SerializeField] private Vector2Int goal = new Vector2Int(4, 4);
    [SerializeField] private float stepDelay = 0.25f;
    [SerializeField] private bool drawDebugPath = true;

    private GridMover mover;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private bool followingPath;

    private void Awake()
    {
        mover = GetComponent<GridMover>();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame &&
            !followingPath)
        {
            CalculateAndFollowPath();
        }
    }

    public void CalculateAndFollowPath()
    {
        currentPath = AStarPathfinder.FindPath(mover.GridPosition, goal);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning("A* Demo: No path available.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FollowPath());
    }

    private IEnumerator FollowPath()
    {
        followingPath = true;

        foreach (Vector2Int nextCell in currentPath)
        {
            if (GameManager.Instance != null && GameManager.Instance.GameOver)
                break;

            Direction direction = DirectionFromTo(mover.GridPosition, nextCell);
            bool moved = mover.TryMove(direction);

            Debug.Log(
                $"A* Demo: {mover.GridPosition} -> {nextCell}, moved = {moved}");

            if (!moved)
            {
                Debug.LogWarning(
                    "A* Demo: Path became blocked. Recalculate the path.");
                break;
            }

            yield return new WaitForSeconds(stepDelay);
        }

        followingPath = false;
    }

    private Direction DirectionFromTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;

        if (delta == Vector2Int.up) return Direction.Up;
        if (delta == Vector2Int.down) return Direction.Down;
        if (delta == Vector2Int.left) return Direction.Left;

        return Direction.Right;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugPath || currentPath == null || GridSystem.Instance == null)
            return;

        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 world = GridSystem.Instance.GridToWorld(currentPath[i]);
            Gizmos.DrawWireSphere(world, 0.15f);

            if (i > 0)
            {
                Vector3 previous =
                    GridSystem.Instance.GridToWorld(currentPath[i - 1]);
                Gizmos.DrawLine(previous, world);
            }
        }
    }
}