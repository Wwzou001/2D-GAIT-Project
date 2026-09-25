using UnityEngine;

public class LevelEndFlag : MonoBehaviour
{
    private bool triggeredThisEpisode = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggeredThisEpisode) return; // prevent repeat
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        if (!other.CompareTag("Player")) return;

        triggeredThisEpisode = true;

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

    // New episode reset
    public void ResetTrigger()
    {
        triggeredThisEpisode = false;
    }
}
