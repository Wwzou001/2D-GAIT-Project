using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable A* pathfinder for the grid environment.
///
/// Environment assumptions:
/// - Movement is limited to the four cardinal grid directions.
/// - Each normal grid step has cost 1.
/// - GridSystem supplies bounds and fixed-obstacle information.
/// - dynamicBlockedCells supplies temporary/moving obstacles for the current search.
///
/// Teaching parameters:
/// - HeuristicType selects how H is estimated.
/// - SearchResult.ExploredNodes exposes G/H/F values for visualisation.
/// </summary>
public static class AStarPathfinder
{
    public enum HeuristicType
    {
        Manhattan,
        Euclidean
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

    // Compatibility overload: existing code can still call FindPath(start, goal).
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        return FindPath(start, goal, HeuristicType.Manhattan, null).Path;
    }

    // Compatibility overload used by the earlier teaching/debug controller.
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        out int exploredNodes)
    {
        SearchResult result = FindPath(start, goal, HeuristicType.Manhattan, null);
        exploredNodes = result.ExploredNodes.Count;
        return result.PathFound ? result.Path : null;
    }

    /// <summary>
    /// Runs A* and returns both the path and the explored-node data used for teaching.
    /// dynamicBlockedCells should contain the CURRENT grid positions of moving obstacles.
    /// Calling this method again after an obstacle moves gives A* the updated environment.
    /// </summary>
    public static SearchResult FindPath(
        Vector2Int start,
        Vector2Int goal,
        HeuristicType heuristic,
        HashSet<Vector2Int> dynamicBlockedCells)
    {
        SearchResult result = new SearchResult();

        if (GridSystem.Instance == null)
        {
            Debug.LogError("A*: GridSystem.Instance is null.");
            return result;
        }

        if (!GridSystem.Instance.IsInBounds(start) ||
            !GridSystem.Instance.IsInBounds(goal))
        {
            Debug.LogWarning($"A*: Start {start} or goal {goal} is outside the grid.");
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

        openList.Add(new Node(start, null, 0f, CalculateHeuristic(start, goal, heuristic)));

        while (openList.Count > 0)
        {
            Node currentNode = GetLowestCostNode(openList);
            openList.Remove(currentNode);

            if (closedSet.Contains(currentNode.Position))
                continue;

            closedSet.Add(currentNode.Position);

            // Store the exact G/H/F values at the moment this node is explored.
            result.ExploredNodes.Add(
                new NodeDebugInfo(currentNode.Position, currentNode.GCost, currentNode.HCost));

            if (currentNode.Position == goal)
            {
                result.Path = ReconstructPath(currentNode);
                result.PathFound = true;
                return result;
            }

            foreach (Vector2Int neighbour in GetNeighbours(currentNode.Position))
            {
                if (!GridSystem.Instance.IsInBounds(neighbour))
                    continue;

                if (IsBlocked(neighbour, dynamicBlockedCells, start))
                    continue;

                if (closedSet.Contains(neighbour))
                    continue;

                float newGCost = currentNode.GCost + 1f;
                float newHCost = CalculateHeuristic(neighbour, goal, heuristic);
                Node existingNode = FindNodeInOpenList(openList, neighbour);

                if (existingNode == null)
                {
                    openList.Add(new Node(neighbour, currentNode, newGCost, newHCost));
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

    private static bool IsBlocked(
        Vector2Int position,
        HashSet<Vector2Int> dynamicBlockedCells,
        Vector2Int start)
    {
        // The agent's own start cell must remain traversable.
        if (position == start)
            return false;

        if (GridSystem.Instance.IsObstacle(position))
            return true;

        return dynamicBlockedCells != null && dynamicBlockedCells.Contains(position);
    }

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
                return Mathf.Sqrt(dx * dx + dy * dy);

            case HeuristicType.Manhattan:
            default:
                return dx + dy;
        }
    }

    private static Node GetLowestCostNode(List<Node> openList)
    {
        Node bestNode = openList[0];

        for (int i = 1; i < openList.Count; i++)
        {
            Node candidate = openList[i];

            if (candidate.FCost < bestNode.FCost ||
                (Mathf.Approximately(candidate.FCost, bestNode.FCost) &&
                 candidate.HCost < bestNode.HCost))
            {
                bestNode = candidate;
            }
        }

        return bestNode;
    }

    private static Node FindNodeInOpenList(List<Node> openList, Vector2Int position)
    {
        foreach (Node node in openList)
        {
            if (node.Position == position)
                return node;
        }

        return null;
    }

    private static List<Vector2Int> GetNeighbours(Vector2Int position)
    {
        return new List<Vector2Int>
        {
            position + Vector2Int.up,
            position + Vector2Int.down,
            position + Vector2Int.left,
            position + Vector2Int.right
        };
    }

    private static List<Vector2Int> ReconstructPath(Node goalNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node currentNode = goalNode;

        // Excludes the start cell and includes the goal cell.
        while (currentNode != null && currentNode.Parent != null)
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }

        path.Reverse();
        return path;
    }
}
