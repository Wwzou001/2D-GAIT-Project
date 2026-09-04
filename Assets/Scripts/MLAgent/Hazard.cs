using UnityEngine;

public class Hazard : MonoBehaviour
{
    public enum HazardType { Spike, Pit }

    [SerializeField] private HazardType hazardType = HazardType.Spike;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        // Make sure player game object have "Player" tag
        if (!other.CompareTag("Player")) return;

        string reason = hazardType == HazardType.Spike ? "hit a spike" : "fell into a pit";

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.Lose(reason);
        }
    }
}
