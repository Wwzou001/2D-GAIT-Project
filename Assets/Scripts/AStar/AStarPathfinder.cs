using System.Collections.Generic;
using UnityEngine;


/// Sprint 2 A* skeleton.
/// Finds a 4-directional path across the existing GridSystem.

public static class AStarPathfinder
{
    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;

        public Node(Vector2Int position)
        {
            Position = position;
        }
    }

    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (GridSystem.Instance == null)
        {
            Debug.LogError("A*: GridSystem.Instance is null.");
            return null;
        }

        if (!GridSystem.Instance.IsInBounds(start) ||
            !GridSystem.Instance.IsInBounds(goal))
        {
            Debug.LogWarning("A*: Start or goal is outside the grid.");
            return null;
        }

        if (GridSystem.Instance.IsObstacle(goal))
        {
            Debug.LogWarning("A*: Goal is inside an obstacle.");
            return null;
        }

        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Node> nodes = new Dictionary<Vector2Int, Node>();

        Node startNode = GetOrCreateNode(nodes, start);
        startNode.GCost = 0;
        startNode.HCost = Heuristic(start, goal);
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            Node current = GetLowestCostNode(openList);

            if (current.Position == goal)
            {
                List<Vector2Int> path = ReconstructPath(current);
                Debug.Log($"A*: Path found from {start} to {goal}. Steps: {path.Count}");
                return path;
            }

            openList.Remove(current);
            closedSet.Add(current.Position);

            foreach (Vector2Int neighbourPos in GetNeighbours(current.Position))
            {
                if (closedSet.Contains(neighbourPos))
                    continue;

                if (!GridSystem.Instance.IsInBounds(neighbourPos))
                    continue;

                if (GridSystem.Instance.IsObstacle(neighbourPos))
                    continue;

                Node neighbour = GetOrCreateNode(nodes, neighbourPos);

                int tentativeG = current.GCost + 1;

                bool isNewNode = !openList.Contains(neighbour);
                if (isNewNode || tentativeG < neighbour.GCost)
                {
                    neighbour.Parent = current;
                    neighbour.GCost = tentativeG;
                    neighbour.HCost = Heuristic(neighbourPos, goal);

                    if (isNewNode)
                        openList.Add(neighbour);
                }
            }
        }

        Debug.LogWarning($"A*: No path found from {start} to {goal}.");
        return null;
    }

    private static Node GetOrCreateNode(
        Dictionary<Vector2Int, Node> nodes,
        Vector2Int position)
    {
        if (!nodes.TryGetValue(position, out Node node))
        {
            node = new Node(position);
            nodes[position] = node;
        }

        return node;
    }

    private static Node GetLowestCostNode(List<Node> openList)
    {
        Node best = openList[0];

        for (int i = 1; i < openList.Count; i++)
        {
            Node candidate = openList[i];

            if (candidate.FCost < best.FCost ||
                (candidate.FCost == best.FCost &&
                 candidate.HCost < best.HCost))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        // Manhattan distance is suitable because GridMover moves
        // only Up, Down, Left and Right.
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static IEnumerable<Vector2Int> GetNeighbours(Vector2Int position)
    {
        yield return position + Vector2Int.up;
        yield return position + Vector2Int.down;
        yield return position + Vector2Int.left;
        yield return position + Vector2Int.right;
    }

    private static List<Vector2Int> ReconstructPath(Node goalNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node current = goalNode;

        while (current.Parent != null)
        {
            path.Add(current.Position);
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }
}