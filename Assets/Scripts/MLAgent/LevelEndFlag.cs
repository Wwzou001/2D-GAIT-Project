using UnityEngine;

public class LevelEndFlag : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        if (!other.CompareTag("Player")) return;

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.Win();
        }

        // Notify the ML-Agents wrapper, if player has one
        PlatformerAgent agent = other.GetComponent<PlatformerAgent>();
        if (agent != null)
        {
            agent.OnGoalReached();
        }
    }
}
