using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.ComponentModel;

public class MCTSAgent
{
    private readonly int iterations; // total selection/expansion/simulation/backprop cycles
    private readonly int rolloutDepth;
    private readonly float explorationConstant; // c in UCB1 formula
    // True is hunter, false is collector
    private readonly bool isHunter;
    private readonly System.Random rng = new System.Random();

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

        Node root = new Node
        {
            State = new MctsState(selfPos, opponentPos),
            UntriedMoves = GetLegalMoves(selfPos)
        };

        string role = isHunter ? "Hunter" : "Collector";
        log = $"[MCTS-{role}] Self@{selfPos} vs Opponent@{opponentPos}\n";

        if (root.UntriedMoves.Count == 0)
        {
            log += "[MCTS] No legal moves, staying put.";
            stopwatch.Stop();
            LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return Direction.Up;
        }

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
        float coinBonus = 0f;

        for (int step = 0; step < rolloutDepth; step++)
        {
            if (state.EnemyCaughtPlayer()) return isHunter ? 1f : -1f;

            state.EnemyPos = RandomStep(state.EnemyPos);
            state.PlayerPos = RandomStep(state.PlayerPos);

            if (!isHunter && GridSystem.Instance.IsCoin(state.EnemyPos))
            {
                coinBonus += 0.3f;
            }
        }

        int distance = Mathf.Abs(state.EnemyPos.x - state.PlayerPos.x) + Mathf.Abs(state.EnemyPos.y - state.PlayerPos.y);
        
        if (isHunter)
        {
            return -distance / 10f;
        }

        return (distance / 10f) + coinBonus;
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

    private Vector2Int RandomStep(Vector2Int from)
    {
        List<Direction> options = GetLegalMoves(from);
        if (options.Count == 0) return from;
        return from + Offset(options[rng.Next(options.Count)]);
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
