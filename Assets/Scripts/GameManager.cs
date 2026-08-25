using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Characters")]
    [SerializeField] private GridMover player;
    [SerializeField] private GridMover enemy;

    [Header("UI")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text coinCounterText;

    private bool gameOver = false;

    public bool GameOver => gameOver;

    private void Awake()
    {
        Instance = this;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);
    }

    private void Start()
    {
        UpdateCoinCounter();
    }

    public void CheckGameState()
    {
        if (gameOver)
            return;

        // Update coin counter after every successful move
        UpdateCoinCounter();

        // LOSS CONDITION
        // Player and enemy occupy the same grid square.
        if (player != null && enemy != null &&
            player.GridPosition == enemy.GridPosition)
        {
            LoseGame();
            return;
        }

        // WIN CONDITION
        // All coins have been collected.
        if (GridSystem.Instance.RemainingCoins() == 0)
        {
            WinGame();
        }
    }

    private void UpdateCoinCounter()
    {
        if (coinCounterText != null && GridSystem.Instance != null)
        {
            int totalCoins = GridSystem.Instance.TotalCoins;
            int remainingCoins = GridSystem.Instance.RemainingCoins();
            int collectedCoins = totalCoins - remainingCoins;

            coinCounterText.text = $"Coins: {collectedCoins} / {totalCoins}";
        }
    }




    private void WinGame()
    {
        gameOver = true;

        Debug.Log("GAME OVER - PLAYER WINS!");

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        if (resultText != null)
            resultText.text = "YOU WIN!";
    }

    private void LoseGame()
    {
        gameOver = true;

        Debug.Log("GAME OVER - PLAYER LOSES!");

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        if (resultText != null)
            resultText.text = "YOU LOSE!";
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}