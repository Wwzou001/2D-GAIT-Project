using System;
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxLives = 3;

    [Header("Game over (optional)")]
    [Tooltip("Shown when the player runs out of lives. Wire its Restart button to GameManager.RestartGame.")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private string gameOverMessage = "YOU LOSE!";

    // Raised once when the player runs out of lives. Other scripts can
    // subscribe instead of PlayerHealth needing a GameManager.PlayerDied().
    public static event Action<PlayerHealth> PlayerDied;

    public int CurrentLives { get; private set; }
    public int MaxLives => maxLives;
    public bool IsDead => CurrentLives <= 0;

    private void Awake()
    {
        // Undo any pause left over from a previous run (GameManager does the
        // same, but not every scene has one).
        Time.timeScale = 1f;

        CurrentLives = maxLives;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead)
            return;

        CurrentLives -= amount;
        CurrentLives = Mathf.Max(CurrentLives, 0);

        Debug.Log($"Player lost a life! Lives: {CurrentLives}/{maxLives}");

        if (CurrentLives <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"Player has lost all {maxLives} lives!");

        PlayerDied?.Invoke(this);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = gameOverMessage;

        // Pause the game. GameManager.RestartGame() sets this back to 1.
        Time.timeScale = 0f;
    }
}