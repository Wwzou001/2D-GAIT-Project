using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MCTSGameManager : MonoBehaviour
{
    public static MCTSGameManager Instance { get; private set; }

    [SerializeField] private MCTSGridMover player;
    [SerializeField] private MCTSGridMover enemy;

    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text coinCounterText;

    [SerializeField] private bool playerGoesFirst = true;

    private bool gameOver = false;

    public bool GameOver => gameOver;
    public bool IsPlayerTurn {  get; private set; }

    private void Awake()
    {
        Instance = this;

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        IsPlayerTurn = playerGoesFirst;
    }
    void Start()
    {
        UpdateCoinCounter();
    }

    // Call by MCTSPlayerMover after a successful move
    public void EndPlayerTurn()
    {
        IsPlayerTurn = false;
    }

    // Call by MCTSEnemyController after a successful move
    public void EndEnemyTurn()
    {
        IsPlayerTurn = true;
    }

    public void CheckGameStatus()
    {
        if (GameOver)
        {
            return;
        }

        UpdateCoinCounter();

        // Loss condition
        if (player != null && enemy != null && player.GridPosition == enemy.GridPosition)
        {
            LoseGame();
            return;
        }

        // Win condition
        if (GridSystem.Instance.RemainingCoins() == 0)
        {
            WinGame();
        }
    }

    void UpdateCoinCounter()
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
        Debug.Log("Game Over - Player Wins!");

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text = "You Win!";
        }
    }

    private void LoseGame()
    {
        gameOver = true;
        Debug.Log("Game Over - Player Loses!");

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text = "You Lose!";
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
