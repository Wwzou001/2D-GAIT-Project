using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// Runs a simple Monte Carlo search: for each direction the enemy could move,
public class MCSAgent
{
    private readonly int simulationsPerMove;
    private readonly int rolloutDepth;
    // If this agent is hunter true, if this agent is collector false
    private readonly bool isHunter;
    private readonly System.Random rng = new System.Random();

    public double LastDecisionTimeMs { get; private set; }

    public MCSAgent(int simulationsPerMove = 300, int rolloutDepth = 15, bool isHunter = true)
    {
        this.simulationsPerMove = simulationsPerMove;
        this.rolloutDepth = rolloutDepth;
        this.isHunter = isHunter;
    }

    public Direction ChooseMove(Vector2Int selfPos, Vector2Int opponentPos, out string log)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        List<Direction> legalMoves = GetLegalMoves(selfPos);
        string role = isHunter ? "Hunter" : "Collector";
        log = $"[MCS-{role}] Self@{selfPos} vs Opponent@{opponentPos}\n";

        if (legalMoves.Count == 0)
        {
            log += "[MCS] No legal moves, staying put.";
            stopwatch.Stop();
            LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return Direction.Up; // won't actually move, TryMove will just fail
        }

        // 0 simulation minimum
        if (simulationsPerMove <= 0)
        {
            Direction randomMove = legalMoves[rng.Next(legalMoves.Count)];
            log += $"[MCS] 0 simulations \u2014 picking a purely random move: {randomMove}";
            stopwatch.Stop();
            LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return randomMove;
        }

        Direction bestMove = legalMoves[0];
        float bestScore = float.MinValue;

        foreach (Direction move in legalMoves)
        {
            Vector2Int nextPos = selfPos + Offset(move);
            float score = AverageRolloutScore(nextPos, opponentPos);
            log += $"  {move}: avg score {score:F2}\n";

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        stopwatch.Stop();
        LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        log += $"[MCS] Chose {bestMove} ({simulationsPerMove} simulations, {LastDecisionTimeMs:F2} ms)";
        return bestMove;
    }

    private float AverageRolloutScore(Vector2Int startSelfPos, Vector2Int startOpponentPos)
    {
        float total = 0f;
        for (int i = 0; i < simulationsPerMove; i++)
        {
            total += RandomRollout(startSelfPos, startOpponentPos);
        }
        return total / simulationsPerMove;
    }

    // Plays forward randomly and scores the outcome.
    // Catching the player = best result. Otherwise, ending up closer is better.
    private float RandomRollout(Vector2Int selfPos, Vector2Int opponentPos)
    {
        var state = new MctsState(selfPos, opponentPos);
        float coinBonus = 0f;

        for (int step = 0; step < rolloutDepth; step++)
        {
            if (state.EnemyCaughtPlayer())
                return isHunter ? 1f : -1f; // caught = great for hunter, terrible for collector

            state.EnemyPos = RandomStep(state.EnemyPos);
            state.PlayerPos = RandomStep(state.PlayerPos); // rough guess at player behaviour

            // Only collector care about coins
            if (!isHunter && GridSystem.Instance.IsCoin(state.EnemyPos))
            {
                coinBonus += 0.3f;
            }
        }

        int distance = Mathf.Abs(state.EnemyPos.x - state.PlayerPos.x) + Mathf.Abs(state.EnemyPos.y - state.PlayerPos.y);
        // Hunter: closer is better. Collector: further is better
        
        if (isHunter)
        {
            return -distance / 10f; // closer = less negative = better
        }

        // Collector: balance stay far away with picking up coins
        return (distance / 10f) + coinBonus; 
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
        foreach (Direction dir in (Direction[])Enum.GetValues(typeof(Direction)))
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