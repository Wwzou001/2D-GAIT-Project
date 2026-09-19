using UnityEngine;

// Just holds a position for the enemy and player at some point in a simulation.
public struct MCTSState
{
    public Vector2Int SelfPos;
    public Vector2Int OpponentPos;

    public MCTSState(Vector2Int selfPos, Vector2Int opponentPos)
    {
        SelfPos = selfPos;
        OpponentPos = opponentPos;
    }

    public bool SelfCaughtOpponent()
    {
        return SelfPos == OpponentPos;
    }
}
