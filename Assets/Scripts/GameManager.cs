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

    
    private bool hasKey = false; 

    private bool gameOver = false;

    public bool GameOver => gameOver;
    public bool HasKey => hasKey;

    private void Awake()
    {
        Instance = this;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);
    }

    private void Start()
    {
        UpdateCoinCounter();
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected += HandleKeyCollected;
        }
    }

    private void HandleKeyCollected(Vector2Int pos)
    {
        hasKey = true;
    }

    private void OnDestroy()
    {
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected -= HandleKeyCollected;
        }
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

    }

    private void UpdateCoinCounter()
    {
        if (coinCounterText != null && GridSystem.Instance != null)
        {
            int totalCoins = GridSystem.Instance.TotalCoins;
            int remainingCoins = GridSystem.Instance.RemainingCoins();
            int collectedCoins = totalCoins - remainingCoins;

            int totalKeys = GridSystem.Instance.TotalKeys;
            int remainingKeys = GridSystem.Instance.RemainingKeys();
            int collectedKeys = totalKeys - remainingKeys;

            coinCounterText.text = $"Coins: {collectedCoins} / {totalCoins}\nKey: {(hasKey ? 1 : 0)} / 1";
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

    public void TryEnterDoor()
    {
        if (gameOver)
            return;

        if (hasKey)
        {
            WinGame();
        }
        else
        {
            Debug.Log("Door is locked. You need to find the key first.");
        }
    }

    public void StartNewRoom()
    {
        hasKey = false;

        UpdateCoinCounter();

        Debug.Log("New room started.");
    }
}