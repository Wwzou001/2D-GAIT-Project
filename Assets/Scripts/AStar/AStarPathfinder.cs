using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding implementation for the existing GridSystem.
///
/// Supports:
/// - 4-directional grid movement
/// - obstacle avoidance
/// - Manhattan-distance heuristic
/// - path reconstruction
/// - explored-node count for teaching/debugging
/// </summary>

public static class AStarPathfinder
{
    private class Node
    {
        public Vector2Int Position;

        public Node Parent;

        public int GCost;

        public int HCost;

        public int FCost =>
            GCost + HCost;

        public Node(
            Vector2Int position)
        {
            Position = position;

            // Start high so a cheaper path
            // can replace it.
            GCost = int.MaxValue;
        }
    }

     
    // SIMPLE VERSION
     

    // Keeps compatibility with old code.
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal)
    {
        int ignoredExploredCount;

        return FindPath(
            start,
            goal,
            out ignoredExploredCount
        );
    }

     
    // VERSION WITH DEBUG INFORMATION
     

    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        out int exploredNodes)
    {
        exploredNodes = 0;

        if (GridSystem.Instance == null)
        {
            Debug.LogError(
                "A*: GridSystem.Instance is null."
            );

            return null;
        }

        // Validate positions.
        if (!GridSystem.Instance.IsInBounds(start) ||
            !GridSystem.Instance.IsInBounds(goal))
        {
            Debug.LogWarning(
                "A*: Start or goal is outside the grid."
            );

            return null;
        }

        // Goal cannot be an obstacle.
        if (GridSystem.Instance.IsObstacle(goal))
        {
            Debug.LogWarning(
                "A*: Goal is inside an obstacle."
            );

            return null;
        }

        List<Node> openList =
            new List<Node>();

        HashSet<Vector2Int> closedSet =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Node> nodes =
            new Dictionary<Vector2Int, Node>();

        Node startNode =
            GetOrCreateNode(
                nodes,
                start
            );

        startNode.GCost = 0;

        startNode.HCost =
            Heuristic(
                start,
                goal
            );

        openList.Add(startNode);

        
        // MAIN A* LOOP
       

        while (openList.Count > 0)
        {
            Node current =
                GetLowestCostNode(
                    openList
                );

            exploredNodes++;

            // Goal reached.
            if (current.Position == goal)
            {
                List<Vector2Int> path =
                    ReconstructPath(
                        current
                    );

                Debug.Log(
                    $"A*: Path found from " +
                    $"{start} to {goal}. " +
                    $"Path length = {path.Count}, " +
                    $"Nodes explored = {exploredNodes}"
                );

                return path;
            }

            openList.Remove(current);

            closedSet.Add(
                current.Position
            );

            // Explore neighbours.
            foreach (
                Vector2Int neighbourPosition
                in GetNeighbours(
                    current.Position
                ))
            {
                // Already processed.
                if (closedSet.Contains(
                    neighbourPosition))
                {
                    continue;
                }

                // Outside grid.
                if (!GridSystem.Instance
                    .IsInBounds(
                        neighbourPosition))
                {
                    continue;
                }

                // Blocked cell.
                if (GridSystem.Instance
                    .IsObstacle(
                        neighbourPosition))
                {
                    continue;
                }

                Node neighbour =
                    GetOrCreateNode(
                        nodes,
                        neighbourPosition
                    );

                int tentativeG =
                    current.GCost + 1;

                bool isNewNode =
                    !openList.Contains(
                        neighbour
                    );

                if (isNewNode ||
                    tentativeG <
                    neighbour.GCost)
                {
                    neighbour.Parent =
                        current;

                    neighbour.GCost =
                        tentativeG;

                    neighbour.HCost =
                        Heuristic(
                            neighbourPosition,
                            goal
                        );

                    if (isNewNode)
                    {
                        openList.Add(
                            neighbour
                        );
                    }
                }
            }
        }

        Debug.LogWarning(
            $"A*: No path found from " +
            $"{start} to {goal}. " +
            $"Nodes explored = {exploredNodes}"
        );

        return null;
    }

     
    // NODE CREATION
     

    private static Node GetOrCreateNode(
        Dictionary<Vector2Int, Node> nodes,
        Vector2Int position)
    {
        if (!nodes.TryGetValue(
            position,
            out Node node))
        {
            node =
                new Node(position);

            nodes[position] =
                node;
        }

        return node;
    }

     
    // LOWEST F COST
     

    private static Node GetLowestCostNode(
        List<Node> openList)
    {
        Node best =
            openList[0];

        for (int i = 1;
             i < openList.Count;
             i++)
        {
            Node candidate =
                openList[i];

            // Prefer lowest F.
            // If equal, prefer lower H.
            if (candidate.FCost <
                    best.FCost ||
                (candidate.FCost ==
                    best.FCost &&
                 candidate.HCost <
                    best.HCost))
            {
                best =
                    candidate;
            }
        }

        return best;
    }

     
    // HEURISTIC
     

    private static int Heuristic(
        Vector2Int a,
        Vector2Int b)
    {
        // Manhattan distance because
        // movement is only:
        // Up / Down / Left / Right.

        return Mathf.Abs(a.x - b.x) +
               Mathf.Abs(a.y - b.y);
    }

     
    // NEIGHBOURS
     

    private static IEnumerable<Vector2Int>
        GetNeighbours(
            Vector2Int position)
    {
        yield return
            position + Vector2Int.up;

        yield return
            position + Vector2Int.down;

        yield return
            position + Vector2Int.left;

        yield return
            position + Vector2Int.right;
    }

   
    // BUILD FINAL PATH
    

    private static List<Vector2Int>
        ReconstructPath(
            Node goalNode)
    {
        List<Vector2Int> path =
            new List<Vector2Int>();

        Node current =
            goalNode;

        while (current.Parent != null)
        {
            path.Add(
                current.Position
            );

            current =
                current.Parent;
        }

        path.Reverse();

        return path;
    }
}