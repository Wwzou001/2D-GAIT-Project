using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    // Represents one grid cell during the A* search
    private class Node
    {
        public Vector2Int Position;

        public Node Parent;

        public int GCost;
        public int HCost;

        public int FCost
        {
            get
            {
                return GCost + HCost;
            }
        }

        public Node(
            Vector2Int position,
            Node parent,
            int gCost,
            int hCost
        )
        {
            Position = position;
            Parent = parent;
            GCost = gCost;
            HCost = hCost;
        }
    }

    // Old version kept for compatibility
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal
    )
    {
        int exploredNodes;

        return FindPath(
            start,
            goal,
            out exploredNodes
        );
    }

    // Main A* pathfinding method
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        out int exploredNodes
    )
    {
        exploredNodes = 0;

        // Make sure GridSystem exists
        if (GridSystem.Instance == null)
        {
            Debug.LogError(
                "A*: GridSystem.Instance is null."
            );

            return null;
        }

        // Check that start is inside the grid
        if (!GridSystem.Instance.IsInBounds(start))
        {
            Debug.LogWarning(
                $"A*: Start position {start} is outside the grid."
            );

            return null;
        }

        // Check that goal is inside the grid
        if (!GridSystem.Instance.IsInBounds(goal))
        {
            Debug.LogWarning(
                $"A*: Goal position {goal} is outside the grid."
            );

            return null;
        }

        // Goal cannot be an obstacle
        if (GridSystem.Instance.IsObstacle(goal))
        {
            Debug.LogWarning(
                $"A*: Goal position {goal} is blocked by an obstacle."
            );

            return null;
        }

        // If already at the goal
        if (start == goal)
        {
            Debug.Log(
                "A*: Start and goal are the same position."
            );

            return new List<Vector2Int>();
        }

        List<Node> openList =
            new List<Node>();

        HashSet<Vector2Int> closedSet =
            new HashSet<Vector2Int>();

        Node startNode =
            new Node(
                start,
                null,
                0,
                ManhattanDistance(start, goal)
            );

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            // Find the node with the lowest F cost
            Node currentNode =
                GetLowestCostNode(openList);

            openList.Remove(currentNode);

            // Skip if already explored
            if (closedSet.Contains(
                currentNode.Position
            ))
            {
                continue;
            }

            closedSet.Add(
                currentNode.Position
            );

            exploredNodes++;

            Debug.Log(
                $"A*: Exploring {currentNode.Position} | " +
                $"G={currentNode.GCost}, " +
                $"H={currentNode.HCost}, " +
                $"F={currentNode.FCost}"
            );

            // Goal reached
            if (currentNode.Position == goal)
            {
                List<Vector2Int> path =
                    ReconstructPath(currentNode);

                Debug.Log(
                    $"A*: Path found from {start} to {goal}. " +
                    $"Path Length = {path.Count}, " +
                    $"Nodes Explored = {exploredNodes}"
                );

                return path;
            }

            // Check all four neighbouring cells
            foreach (
                Vector2Int neighbourPosition
                in GetNeighbours(
                    currentNode.Position
                )
            )
            {
                // Ignore cells outside the grid
                if (!GridSystem.Instance.IsInBounds(
                    neighbourPosition
                ))
                {
                    continue;
                }

                // Ignore obstacle cells
                if (GridSystem.Instance.IsObstacle(
                    neighbourPosition
                ))
                {
                    continue;
                }

                // Ignore cells already fully explored
                if (closedSet.Contains(
                    neighbourPosition
                ))
                {
                    continue;
                }

                int newGCost =
                    currentNode.GCost + 1;

                int newHCost =
                    ManhattanDistance(
                        neighbourPosition,
                        goal
                    );

                Node existingNode =
                    FindNodeInOpenList(
                        openList,
                        neighbourPosition
                    );

                // If node has not yet been discovered
                if (existingNode == null)
                {
                    Node neighbourNode =
                        new Node(
                            neighbourPosition,
                            currentNode,
                            newGCost,
                            newHCost
                        );

                    openList.Add(
                        neighbourNode
                    );
                }
                else
                {
                    // Better route to an already discovered node
                    if (newGCost <
                        existingNode.GCost)
                    {
                        existingNode.GCost =
                            newGCost;

                        existingNode.Parent =
                            currentNode;
                    }
                }
            }
        }

        Debug.LogWarning(
            $"A*: No path found from {start} to {goal}. " +
            $"Nodes Explored = {exploredNodes}"
        );

        return null;
    }

    // Returns the node with the lowest F cost
    private static Node GetLowestCostNode(
        List<Node> openList
    )
    {
        Node bestNode =
            openList[0];

        for (int i = 1;
             i < openList.Count;
             i++)
        {
            Node candidate =
                openList[i];

            // Lower F cost is better
            if (candidate.FCost <
                bestNode.FCost)
            {
                bestNode = candidate;
            }

            // If F costs are equal,
            // prefer lower H cost
            else if (
                candidate.FCost ==
                bestNode.FCost &&
                candidate.HCost <
                bestNode.HCost
            )
            {
                bestNode = candidate;
            }
        }

        return bestNode;
    }

    // Search for an existing node
    // in the open list
    private static Node FindNodeInOpenList(
        List<Node> openList,
        Vector2Int position
    )
    {
        foreach (Node node in openList)
        {
            if (node.Position == position)
            {
                return node;
            }
        }

        return null;
    }

    // Get neighbouring cells:
    // Up, Down, Left, Right
    private static List<Vector2Int> GetNeighbours(
        Vector2Int position
    )
    {
        return new List<Vector2Int>
        {
            position + Vector2Int.up,
            position + Vector2Int.down,
            position + Vector2Int.left,
            position + Vector2Int.right
        };
    }

    // Manhattan distance is appropriate
    // for a 4-direction grid
    private static int ManhattanDistance(
        Vector2Int a,
        Vector2Int b
    )
    {
        return
            Mathf.Abs(a.x - b.x) +
            Mathf.Abs(a.y - b.y);
    }

    // Build final path by following
    // parent nodes backwards
    private static List<Vector2Int> ReconstructPath(
        Node goalNode
    )
    {
        List<Vector2Int> path =
            new List<Vector2Int>();

        Node currentNode =
            goalNode;

        while (
            currentNode != null &&
            currentNode.Parent != null
        )
        {
            path.Add(
                currentNode.Position
            );

            currentNode =
                currentNode.Parent;
        }

        // Currently:
        // Goal -> ... -> Start

        path.Reverse();

        // Final result:
        // Start's next cell -> ... -> Goal

        return path;
    }
}