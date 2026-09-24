using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class FullGameManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private RoomManager roomManager;

    [Header("Player")]
    [SerializeField] private GridMover player;

    [Header("UI")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text coinCounterText;


    //tjhese are assigned whenever RoomManager creates a room
    private GridSystem currentGrid;
    private GridMover currentEnemy;


    private bool hasKey = false;
    private bool gameOver = false;


    public bool GameOver => gameOver;
    public bool HasKey => hasKey;


    private void Awake()
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }
    }


    private void Start()
    {
    }


    //called by RoomManager whenever a new room is created
    public void SetCurrentRoom(
        GridSystem gridSystem,
        GridMover enemy
    )
    {
        //remove old room
        if (currentGrid != null)
        {
            currentGrid.KeyCollected -= HandleKeyCollected;
        }
        currentGrid = gridSystem;
        currentEnemy = enemy;


        //add new room
        if (currentGrid != null)
        {
            currentGrid.KeyCollected += HandleKeyCollected;
        }
        StartNewRoom();
    }


    private void HandleKeyCollected(Vector2Int pos)
    {
        hasKey = true;
        UpdateCoinCounter();
    }


    private void OnDestroy()
    {
        if (currentGrid != null)
        {
            currentGrid.KeyCollected -= HandleKeyCollected;
        }
    }


    public void CheckGameState()
    {
        if (gameOver)
            return;
        if (currentGrid == null)
            return;



        // Update coin counter after every successful move
        UpdateCoinCounter();

        // LOSS CONDITION
        // Player and enemy occupy the same grid square.
        if (player != null &&
            currentEnemy != null &&
            player.GridPosition == currentEnemy.GridPosition)
        {
            LoseGame();
            return;
        }
    }


    private void UpdateCoinCounter()
    {
        if (coinCounterText == null)
            return;


        if (currentGrid == null)
            return;


        int totalCoins = currentGrid.TotalCoins;
        int remainingCoins = currentGrid.RemainingCoins();
        int collectedCoins = totalCoins - remainingCoins;


        coinCounterText.text =
            $"Coins: {collectedCoins} / {totalCoins}\n" +
            $"Key: {(hasKey ? 1 : 0)} / 1";
    }


    private void WinGame()
    {
        gameOver = true;

        Debug.Log("GAME OVER - PLAYER WINS!");


        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }


        if (resultText != null)
        {
            resultText.text = "YOU WIN!";
        }
    }


    private void LoseGame()
    {
        gameOver = true;

        Debug.Log("GAME OVER - PLAYER LOSES!");


        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }


        if (resultText != null)
        {
            resultText.text = "YOU LOSE!";
        }
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

        if (currentGrid == null)
        {
            Debug.LogWarning("No current room grid.");
            return;
        }

        //let the ROOM decide if its door is unlocked.
        if (!currentGrid.IsDoorUnlocked())
        {
            Debug.Log("Door is locked. You need to find the key first.");
            return;
        }

        if (roomManager == null)
        {
            Debug.LogError("No RoomManager assigned.");
            return;
        }

        roomManager.EnterNextRoom();
    }


    public void StartNewRoom()
    {
        hasKey = false;

        UpdateCoinCounter();

        Debug.Log("New room started.");
    }
}