using UnityEngine;

// Just holds a position for the enemy and player at some point in a simulation.
public struct MctsState
{
    public Vector2Int EnemyPos;
    public Vector2Int PlayerPos;

    public MctsState(Vector2Int enemyPos, Vector2Int playerPos)
    {
        EnemyPos = enemyPos;
        PlayerPos = playerPos;
    }

    public bool EnemyCaughtPlayer()
    {
        return EnemyPos == PlayerPos;
    }
}
