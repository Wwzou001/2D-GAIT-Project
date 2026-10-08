using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable A* pathfinder for the grid environment.
///
/// Environment assumptions:
/// - Movement is limited to the four cardinal grid directions.
/// - Each normal grid step has cost 1.
/// - GridSystem supplies bounds and fixed-obstacle information. If a scene has
///   no GridSystem but does have a TilemapGridSystem, that is used instead
///   (a cell is blocked when it is not walkable). Scenes that already have a
///   GridSystem behave exactly as before.
/// - dynamicBlockedCells supplies temporary/moving obstacles for the current search.
///
/// Teaching parameters:
/// - HeuristicType selects how H is estimated (see the enum below).
/// - SearchResult.ExploredNodes exposes G/H/F values for visualisation.
/// - SearchResult.ComputeMilliseconds shows what one search costs, so the
///   price of replanning every step can be demonstrated.
/// </summary>
public static class AStarPathfinder
{
    /// <summary>
    /// How H (the estimated distance still to go) is calculated.
    /// </summary>
    public enum HeuristicType
    {
        /// <summary>dx + dy. Exact for 4-direction movement, so the best choice here.</summary>
        Manhattan,

        /// <summary>Straight-line distance. Never overestimates, but is weaker, so it explores more nodes.</summary>
        Euclidean,

        /// <summary>max(dx, dy). Meant for 8-direction movement; on this grid it underestimates and explores even more.</summary>
        Chebyshev
    }

    public struct NodeDebugInfo
    {
        public Vector2Int Position;
        public float GCost;
        public float HCost;
        public float FCost;

        public NodeDebugInfo(Vector2Int position, float gCost, float hCost)
        {
            Position = position;
            GCost = gCost;
            HCost = hCost;
            FCost = gCost + hCost;
        }
    }

    public class SearchResult
    {
        public List<Vector2Int> Path = new List<Vector2Int>();
        public List<NodeDebugInfo> ExploredNodes = new List<NodeDebugInfo>();
        public bool PathFound;

        /// <summary>How long this one search took, in milliseconds.</summary>
        public float ComputeMilliseconds;
    }

    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;

        public Node(Vector2Int position, Node parent, float gCost, float hCost)
        {
            Position = position;
            Parent = parent;
            GCost = gCost;
            HCost = hCost;
        }
    }

    // Safety net: a tilemap has no bounds query, so an unreachable goal could
    // otherwise keep exploring outwards. No real map comes close to this size.
    private const int MaxExploredNodes = 20000;

    // Compatibility overload: existing code can still call FindPath(start, goal).
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        return FindPath(
            start,
            goal,
            HeuristicType.Manhattan,
            null
        ).Path;
    }

    // Compatibility overload used by the earlier teaching/debug controller.
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        out int exploredNodes)
    {
        SearchResult result = FindPath(
            start,
            goal,
            HeuristicType.Manhattan,
            null
        );

        exploredNodes = result.ExploredNodes.Count;

        return result.PathFound ? result.Path : null;
    }

    /// <summary>
    /// Runs A* and returns both the path and explored-node data.
    /// dynamicBlockedCells contains the CURRENT positions
    /// of moving obstacles.
    /// </summary>
    public static SearchResult FindPath(
        Vector2Int start,
        Vector2Int goal,
        HeuristicType heuristic,
        HashSet<Vector2Int> dynamicBlockedCells)
    {
        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        SearchResult result = RunSearch(start, goal, heuristic, dynamicBlockedCells);

        timer.Stop();
        result.ComputeMilliseconds = (float)timer.Elapsed.TotalMilliseconds;

        return result;
    }

    private static SearchResult RunSearch(
        Vector2Int start,
        Vector2Int goal,
        HeuristicType heuristic,
        HashSet<Vector2Int> dynamicBlockedCells)
    {
        SearchResult result = new SearchResult();

        if (GridSystem.Instance == null && TilemapGridSystem.Instance == null)
        {
            Debug.LogError("A*: the scene has neither a GridSystem nor a TilemapGridSystem.");
            return result;
        }

        if (!InBounds(start) || !InBounds(goal))
        {
            Debug.LogWarning(
                $"A*: Start {start} or goal {goal} is outside the grid."
            );

            return result;
        }

        if (IsBlocked(goal, dynamicBlockedCells, start))
        {
            Debug.LogWarning($"A*: Goal {goal} is blocked.");
            return result;
        }

        if (start == goal)
        {
            result.PathFound = true;
            return result;
        }

        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        openList.Add(
            new Node(
                start,
                null,
                0f,
                CalculateHeuristic(start, goal, heuristic)
            )
        );

        while (openList.Count > 0)
        {
            if (result.ExploredNodes.Count >= MaxExploredNodes)
            {
                Debug.LogWarning(
                    $"A*: gave up after exploring {MaxExploredNodes} nodes " +
                    $"({start} to {goal}). Is the goal unreachable?"
                );
                return result;
            }

            Node currentNode = GetLowestCostNode(openList);

            openList.Remove(currentNode);

            if (closedSet.Contains(currentNode.Position))
                continue;

            closedSet.Add(currentNode.Position);

            // Save G/H/F information for teaching visualisation.
            result.ExploredNodes.Add(
                new NodeDebugInfo(
                    currentNode.Position,
                    currentNode.GCost,
                    currentNode.HCost
                )
            );

            // Goal reached.
            if (currentNode.Position == goal)
            {
                result.Path = ReconstructPath(currentNode);
                result.PathFound = true;

                return result;
            }

            foreach (Vector2Int neighbour
                     in GetNeighbours(currentNode.Position))
            {
                if (!InBounds(neighbour))
                    continue;

                if (IsBlocked(
                    neighbour,
                    dynamicBlockedCells,
                    start))
                    continue;

                if (closedSet.Contains(neighbour))
                    continue;

                // Every grid movement currently costs 1.
                float newGCost =
                    currentNode.GCost + 1f;

                float newHCost =
                    CalculateHeuristic(
                        neighbour,
                        goal,
                        heuristic
                    );

                Node existingNode =
                    FindNodeInOpenList(
                        openList,
                        neighbour
                    );

                if (existingNode == null)
                {
                    openList.Add(
                        new Node(
                            neighbour,
                            currentNode,
                            newGCost,
                            newHCost
                        )
                    );
                }
                else if (newGCost < existingNode.GCost)
                {
                    existingNode.GCost = newGCost;
                    existingNode.Parent = currentNode;
                }
            }
        }

        return result;
    }

    // ---------------------------------------------------------------
    // Grid queries. GridSystem wins when it exists (the original scenes);
    // TilemapGridSystem is only used when there is no GridSystem.
    // ---------------------------------------------------------------

    private static bool InBounds(Vector2Int position)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.IsInBounds(position);

        // The tilemap has no bounds query: cells outside the map simply are not
        // walkable, and IsBlocked() handles that.
        return TilemapGridSystem.Instance != null;
    }

    private static bool IsFixedObstacle(Vector2Int position)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.IsObstacle(position);

        return TilemapGridSystem.Instance != null &&
               !TilemapGridSystem.Instance.IsWalkable(position);
    }

    private static bool IsBlocked(
        Vector2Int position,
        HashSet<Vector2Int> dynamicBlockedCells,
        Vector2Int start)
    {
        // The agent's own starting cell must remain traversable.
        if (position == start)
            return false;

        // Fixed obstacle.
        if (IsFixedObstacle(position))
            return true;

        // Moving/dynamic obstacle.
        return dynamicBlockedCells != null &&
               dynamicBlockedCells.Contains(position);
    }

    /// <summary>
    /// Calculates the H cost using the selected heuristic.
    ///
    /// Manhattan  = dx + dy
    /// Euclidean  = sqrt(dx² + dy²)
    /// Chebyshev  = max(dx, dy)
    /// </summary>
    private static float CalculateHeuristic(
        Vector2Int a,
        Vector2Int b,
        HeuristicType heuristic)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        switch (heuristic)
        {
            case HeuristicType.Euclidean:
                return Mathf.Sqrt(
                    dx * dx + dy * dy
                );

            case HeuristicType.Chebyshev:
                return Mathf.Max(dx, dy);

            case HeuristicType.Manhattan:
            default:
                return dx + dy;
        }
    }

    private static Node GetLowestCostNode(
        List<Node> openList)
    {
        Node bestNode = openList[0];

        for (int i = 1; i < openList.Count; i++)
        {
            Node candidate = openList[i];

            if (candidate.FCost < bestNode.FCost ||
                (Mathf.Approximately(
                    candidate.FCost,
                    bestNode.FCost) &&
                 candidate.HCost < bestNode.HCost))
            {
                bestNode = candidate;
            }
        }

        return bestNode;
    }

    private static Node FindNodeInOpenList(
        List<Node> openList,
        Vector2Int position)
    {
        foreach (Node node in openList)
        {
            if (node.Position == position)
                return node;
        }

        return null;
    }

    private static List<Vector2Int> GetNeighbours(
        Vector2Int position)
    {
        return new List<Vector2Int>
        {
            position + Vector2Int.up,
            position + Vector2Int.down,
            position + Vector2Int.left,
            position + Vector2Int.right
        };
    }

    private static List<Vector2Int> ReconstructPath(
        Node goalNode)
    {
        List<Vector2Int> path =
            new List<Vector2Int>();

        Node currentNode = goalNode;

        // Exclude the start cell and include the goal.
        while (currentNode != null &&
               currentNode.Parent != null)
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }

        path.Reverse();

        return path;
    }
}