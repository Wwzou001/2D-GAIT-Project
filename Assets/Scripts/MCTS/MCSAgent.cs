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

    // Detect danger radius
    private const int DangerRadius = 2;

    // Agent follow chase/flee/seek during rollout
    private const float GreedyStepEpsilon = 0.2f;

    // Store coin position
    private List<Vector2Int> coinPositionsCache;

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

        coinPositionsCache = FindAllCoins();

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

        // If this turn can collect last coin, take it without run simulations
        if (!isHunter && coinPositionsCache.Count == 1)
        {
            foreach (Direction move in legalMoves)
            {
                Vector2Int nextPos = selfPos + Offset(move);
                if (nextPos == coinPositionsCache[0])
                {
                    log += $"[MCS] Last coin reachable this move -- taking {move} for the win.";
                    stopwatch.Stop();
                    LastDecisionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                    return move;
                }
            }
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
        float coinPickUpBonus = 0f;

        if (!isHunter && IsWinningCoinPickup(state.EnemyPos))
        {
            return 5f;
        }

        for (int step = 0; step < rolloutDepth; step++)
        {
            state.EnemyPos = GreedyOrRandomStep(state.EnemyPos, state.PlayerPos, isHunter);
            
            if (!isHunter)
            {
                if (IsWinningCoinPickup(state.EnemyPos))
                {
                    return 5f;
                }

                if (GridSystem.Instance.IsCoin(state.EnemyPos))
                { 
                    coinPickUpBonus += 0.5f;
                }
            }
            if (state.EnemyCaughtPlayer())
                return isHunter ? 1f : (-1f + coinPickUpBonus); // caught = great for hunter, terrible for collector

            state.PlayerPos = GreedyOrRandomStep(state.PlayerPos, state.EnemyPos, !isHunter); // rough guess at player behaviour
        }

        int distanceToOpponent = Mathf.Abs(state.EnemyPos.x - state.PlayerPos.x) + Mathf.Abs(state.EnemyPos.y - state.PlayerPos.y);
        // Hunter: closer is better. Collector: further is better

        if (isHunter)
        {
            return -distanceToOpponent / 10f; // closer = less negative = better
        }

        // Collector: balance stay far away with picking up coins
        return CollectorScore(state.EnemyPos, distanceToOpponent) + coinPickUpBonus;
    }

    private bool IsWinningCoinPickup(Vector2Int pos)
    {
        return coinPositionsCache != null && coinPositionsCache.Count == 1 && pos == coinPositionsCache[0];
    }

    // Collector scoring
    private float CollectorScore(Vector2Int myPos, int distanceToHunter)
    {
        float safetyScore = distanceToHunter / 10f; // further from hunter = better

        int coinDistance = NearestCoinDistance(myPos);
        float coinScore = coinDistance >= 0 ? 1f / (coinDistance + 1f) : 0f;

        if (distanceToHunter <= DangerRadius)
        {
            // In danger,escape first
            return safetyScore * 1f + coinScore * 0.3f;
        }

        // Safe, coin first
        return safetyScore * 0.3f + coinScore * 0.9f;
    }

    // Rollout step for moverpos, give other agent position and role
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

    // Heuristic value of moving to canidatepos
    private float EvaluateGreedyStep(Vector2Int candidatePos, Vector2Int otherPos, bool moverIsHunter)
    {
        int distanceToOther = Mathf.Abs(candidatePos.x - otherPos.x) + Mathf.Abs(candidatePos.y - otherPos.y);

        if (moverIsHunter)
        {
            return -distanceToOther; // closer to opponent = better
        }

        if (distanceToOther <= DangerRadius)
        {
            return distanceToOther; // futer from hunter = better when danger
        }

        int coinDist = NearestCoinDistance(candidatePos);
        return coinDist >= 0 ? -coinDist : 0f; // closer to nearest coin = better when safe
    }

    // Manhattan distacne to the nearest remaining coin, use cached coin list
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