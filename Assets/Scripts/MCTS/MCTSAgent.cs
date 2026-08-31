using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class MCTSAgent
{
    private readonly int iterations; // total selection/expansion/simulation/backprop cycles
    private readonly int rolloutDepth;
    private readonly float explorationConstant; // c in UCB1 formula
    // True is hunter, false is collector
    private readonly bool isHunter;
    private readonly System.Random rng = new System.Random();

    private const int DangerRadius = 2;
    private const float GreedyStepEpsilon = 0.2f;

    // Cached coin position
    private List<Vector2Int> coinPositionsCache;

    public double LastDecisionTimeMs {  get; private set; }

    public MCTSAgent(int iterations = 300, int rolloutDepth = 15, float explorationConstant = 1.41f, bool isHunter = true)
    {
        this.iterations = iterations;
        this.rolloutDepth = rolloutDepth;
        this.explorationConstant= explorationConstant;
        this.isHunter = isHunter;
    }

    private class Node
    {
        public Node Parent;
        public Direction? MoveFromParent; // the move lead to this node, null for root
        public MctsState State;
        public List<Node> Children = new List<Node>();
        public List<Direction> UntriedMoves;
        public int Visits;
        public float TotalScore;

        public float AverageScore => Visits == 0 ? 0f : TotalScore / Visits;
    }

    public Direction ChooseMove(Vector2Int selfPos, Vector2Int opponentPos, out string log)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        coinPositionsCache = FindAllCoins();

        string role = isHunter ? "Hunter" : "Collector";
        log = $"[MCTS-{role}] Self@{selfPos} vs Opponent@{opponentPos}\n";

        List<Direction> rootLegalMoves = GetLegalMoves(selfPos);

        if (rootLegalMoves.Count == 0)
        {
            log += "[MCTS] No legal moves, staying put.";
            stopwatch.Stop();
            LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return Direction.Up;
        }

        // Immediate win check
        if (!isHunter && coinPositionsCache.Count == 1)
        {
            foreach (Direction move in rootLegalMoves)
            {
                Vector2Int nextPos = selfPos + Offset(move);
                if (nextPos == coinPositionsCache[0])
                {
                    log += $"[MCTS] Last coin reachable this move -- taking {move} for the win.";
                    stopwatch.Stop();
                    LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                    return move;
                }
            }
        }

        Node root = new Node
        {
            State = new MctsState(selfPos, opponentPos),
            UntriedMoves = rootLegalMoves
        };

        if (iterations <= 0)
        {
            Direction randomMove = root.UntriedMoves[rng.Next(root.UntriedMoves.Count)];
            log += $"[MCTS] 0 iteration -- picking a purely random move: {randomMove}";
            stopwatch.Stop();
            LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return randomMove;
        }

        for (int i = 0; i < iterations; i++)
        {
            Node node = Select(root);
            node = Expand(node);
            float result = Simulate(node.State);
            Backpropagate(node, result);
        }

        // Standard MCTS choice
        Node bestChild = null;
        foreach (Node child in root.Children)
        {
            log += $" {child.MoveFromParent}: visits {child.Visits}, avg score {child.AverageScore:F2}\n";
            if (bestChild == null || child.Visits > bestChild.Visits)
            {
                bestChild = child;
            }
        }

        stopwatch.Stop();
        LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        Direction chosen = bestChild != null ? bestChild.MoveFromParent.Value : root.UntriedMoves[0];
        log += $"[MCTS] Chose {chosen} ({iterations} iterations, {LastDecisionTimeMs:F2} ms)";
        return chosen;
    }

    // Selection: walk down the tree via UCB1 until reach a node still has untries moves
    private Node Select(Node node)
    {
        while (node.UntriedMoves.Count == 0 && node.Children.Count > 0)
        {
            node = BestByUcb1(node);
        }
        return node;
    }

    private Node BestByUcb1 (Node node)
    {
        Node best = null;
        float bestValue = float.MinValue;

        foreach (Node child in node.Children)
        {
            float exploitation = child.AverageScore;
            float exploration = explorationConstant * Mathf.Sqrt(Mathf.Log(node.Visits) / child.Visits);
            float ucb1 = exploitation + exploration;

            if (ucb1 > bestValue)
            {
                bestValue = ucb1;
                best = child;
            }
        }
        return best;
    }

    // Expansion: add one new child for an untried move
    private Node Expand(Node node)
    {
        if (node.UntriedMoves.Count == 0) return node; // nothing left to expand

        int index = rng.Next(node.UntriedMoves.Count);
        Direction move = node.UntriedMoves[index];
        node.UntriedMoves.RemoveAt(index);

        MctsState childState = node.State;
        childState.EnemyPos = childState.EnemyPos + Offset(move);

        // Advance the opponent, to reflect both sides are moving
        if (!childState.EnemyCaughtPlayer())
        { 
            childState.PlayerPos = GreedyOrRandomStep(childState.PlayerPos, childState.EnemyPos, !isHunter);
        }
        Node child = new Node
        {
            Parent = node,
            MoveFromParent = move,
            State = childState,
            UntriedMoves = GetLegalMoves(childState.EnemyPos)
        };

        node.Children.Add(child);
        return child;
    }

    // Simulation: play out randomly from node's state, same random rollout logic as MCSAgent
    private float Simulate(MctsState startState)
    {
        var state = startState;
        float coinPickupBonus = 0f;

        if (!isHunter && IsWinningCoinPickup(state.EnemyPos))
        {
            return 5f;
        }

        if (state.EnemyCaughtPlayer())
        {
            return isHunter ? 1f : -1f;
        }

        for (int step = 0; step < rolloutDepth; step++)
        {
            state.EnemyPos = GreedyOrRandomStep(state.EnemyPos, state.PlayerPos, isHunter);
            state.PlayerPos = GreedyOrRandomStep(state.PlayerPos, state.EnemyPos, !isHunter);

            if (state.EnemyCaughtPlayer()) return isHunter ? 1f : (-1f + coinPickupBonus);


            if (!isHunter)
            {
                if (IsWinningCoinPickup(state.EnemyPos))
                { 
                    return 5f;
                }

                if (GridSystem.Instance.IsCoin(state.EnemyPos))
                { 
                    coinPickupBonus += 0.5f; 
                }
            }
        }

        int distanceToOpponent = Mathf.Abs(state.EnemyPos.x - state.PlayerPos.x) + Mathf.Abs(state.EnemyPos.y - state.PlayerPos.y);
        
        if (isHunter)
        {
            return -distanceToOpponent / 10f;
        }

        return CollectorScore(state.EnemyPos, distanceToOpponent) + coinPickupBonus;
    }

    private bool IsWinningCoinPickup(Vector2Int pos)
    {
        return coinPositionsCache != null && coinPositionsCache.Count == 1 && pos == coinPositionsCache[0];
    }

    private float CollectorScore(Vector2Int myPos, int distanceToHunter)
    {
        float saftyScore = distanceToHunter / 10f;

        int coinDistance = NearestCoinDistance(myPos);
        float coinScore = coinDistance >= 0 ? 1f / (coinDistance + 1f) : 0f;

        if (distanceToHunter <= DangerRadius)
        {
            return saftyScore * 1f + coinScore * 0.3f;
        }

        return saftyScore * 0.3f + coinScore * 0.9f;
    }

    private Vector2Int GreedyOrRandomStep(Vector2Int moverPos, Vector2Int otherPos, bool moverIsHunter)
    {
        List<Direction> options = GetLegalMoves(moverPos);
        if (options.Count == 0) return moverPos;

        if (rng.NextDouble() < GreedyStepEpsilon)
        {
            return moverPos + Offset(options[rng.Next(options.Count)]);
        }

        Direction bestDir = options[0];
        float bestValue = float.MinValue;

        foreach (Direction dir in options)
        {
            Vector2Int candidate = moverPos + Offset(dir);
            float value = EvaluateGreedyStep(candidate, otherPos, moverIsHunter);
            if (value > bestValue)
            {
                bestValue = value;
                bestDir = dir;
            }
        }

        return moverPos + Offset(bestDir);
    }

    private float EvaluateGreedyStep(Vector2Int candidatePos, Vector2Int otherPos, bool moverIsHunter)
    {
        int distanceToOther = Mathf.Abs(candidatePos.x - otherPos.x) + Mathf.Abs(candidatePos.y - otherPos.y);

        if (moverIsHunter)
        {
            return -distanceToOther;
        }

        if (distanceToOther <= DangerRadius)
        {
            return distanceToOther;
        }

        int coinDist = NearestCoinDistance(candidatePos);
        return coinDist >= 0 ? -coinDist : 0f;
    }

    private int NearestCoinDistance(Vector2Int from)
    {
        if (coinPositionsCache == null || coinPositionsCache.Count == 0) return -1;

        int nearest = -1;
        foreach (Vector2Int coin in coinPositionsCache)
        {
            int d = Mathf.Abs(from.x - coin.x) + Mathf.Abs(from.y - coin.y);
            if (nearest == -1 || d < nearest)
            {
                nearest = d; 
            }
        }
        return nearest;
    }

    private List<Vector2Int> FindAllCoins()
    {
        List<Vector2Int> coins = new List<Vector2Int>();
        int width = GridSystem.Instance.Width;
        int height = GridSystem.Instance.Height;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (GridSystem.Instance.IsCoin(cell))
                {
                    coins.Add(cell);
                }
            }
        }
        return coins;
    }

    // Backpropagation: push the simulation result back up through every node visited on the way down, updating visit counts and scores
    private void Backpropagate(Node node, float result)
    {
        while (node != null)
        {
            node.Visits++;
            node.TotalScore += result;
            node = node.Parent;
        }
    }

    private List<Direction> GetLegalMoves(Vector2Int from)
    {
        List<Direction> result = new List<Direction>();
        foreach (Direction dir in (Direction[]) Enum.GetValues(typeof(Direction)))
        {
            Vector2Int target = from + Offset(dir);
            if (!GridSystem.Instance.IsInBounds(target)) continue;
            if (GridSystem.Instance.IsObstacle(target)) continue;
            result.Add(dir);
        }
        return result;
    }

    private Vector2Int Offset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(0, 1);
            case Direction.Down: return new Vector2Int(0, -1);
            case Direction.Left: return new Vector2Int(-1, 0);
            case Direction.Right: return new Vector2Int(1, 0);
            default: return Vector2Int.zero;
        }
    }
}
