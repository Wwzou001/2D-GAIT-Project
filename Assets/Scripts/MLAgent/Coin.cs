using UnityEngine;
using UnityEngine.SceneManagement;

public class Coin : MonoBehaviour
{
    [SerializeField] private int value = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.LevelOver) return;

        if (!other.CompareTag("Player")) return;

        if (PlatformerGameManager.Instance != null)
        {
            PlatformerGameManager.Instance.CollectCoin(value);
        }

        Destroy(gameObject);
    }
}
