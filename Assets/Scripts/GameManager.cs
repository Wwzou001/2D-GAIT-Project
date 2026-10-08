using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
 
// Runs the rules of the game.
//   Win:  pick up the key, then reach the door cell.
//   Lose: the enemy catches the player.
// It also counts flies, spawns the key once every fly has been shot,
// and exposes the numbers that GameHud draws.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
 
    [Header("Characters")]
    [SerializeField] private GridMover player;
    [SerializeField] private GridMover enemy;
 
    [Header("Door")]
    // The floor cell in front of the door. The player wins by standing here with the key.
    [SerializeField] private Vector2Int doorGridPosition;
 
    [Header("UI")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
 
    // Optional. The old text style counter. Leave empty if you use the GameHud pictures.
    [SerializeField] private TMP_Text coinCounterText;
 
    private bool gameOver = false;
    private bool hasKey = false;
    private bool keySpawned = false;   // the key only appears once
 
    private int totalFlies = 0;        // how many flies there were at the start
    private int fliesShot = 0;         // flies hit by a flame, they count as collected
 
    public bool GameOver => gameOver;
 
    // Lets RoomGenerator move the win cell to match the room size.
    public void SetDoorPosition(Vector2Int cell)
    {
        doorGridPosition = cell;
    }
 
    // Numbers for the HUD (GameHud reads these).
    public int TotalCoins => GridSystem.Instance != null ? GridSystem.Instance.TotalCoins : 0;
    public int CoinsCollected => GridSystem.Instance != null
        ? GridSystem.Instance.TotalCoins - GridSystem.Instance.RemainingCoins()
        : 0;
    public int TotalKeys => GridSystem.Instance != null ? GridSystem.Instance.TotalKeys : 0;
    public int KeysCollected => GridSystem.Instance != null ? GridSystem.Instance.KeysCollected : 0;
    public int TotalFlies => totalFlies;
    public int FliesShot => fliesShot;
 
    private void Awake()
    {
        Instance = this;
 
        // Make sure a game left paused by an earlier run does not start frozen.
        Time.timeScale = 1f;
 
        if (endGamePanel != null)
            endGamePanel.SetActive(false);
    }
 
    private void Start()
    {
        // Count the flies once at the start so the HUD can show "shot / total".
        totalFlies = FindObjectsByType<SpiderFSM>(FindObjectsSortMode.None).Length;
 
        // Only the player can pick up the key. Without this the enemy could take it
        // and the player would then be able to win without ever touching the key.
        if (enemy != null)
        {
            enemy.CanCollectKeys = false;
        }
 
        // Find out when the player picks up the key.
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected += HandleKeyCollected;
        }
 
        UpdateCoinCounter();
    }
 
    private void OnDestroy()
    {
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected -= HandleKeyCollected;
        }
    }
 
    // Called by GridMover after every successful move.
    public void CheckGameState()
    {
        if (gameOver)
            return;
 
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
        // The player has the key and is standing on the door cell.
        if (player != null && hasKey && player.GridPosition == doorGridPosition)
        {
            WinGame();
        }
    }
 
    private void HandleKeyCollected(Vector2Int pos)
    {
        hasKey = true;
        UpdateCoinCounter();
    }
 
    // Called by Projectile every time a flame hits a fly.
    // The fly counts as collected straight away.
    public void SpiderShot()
    {
        fliesShot++;
        UpdateCoinCounter();
 
        // When the last fly has been shot, the key appears on a random empty cell.
        if (!keySpawned && totalFlies > 0 && fliesShot >= totalFlies)
        {
            keySpawned = true;
 
            // Do not put the key on the cell the player is standing on, or on the door cell.
            Vector2Int playerCell = player != null ? player.GridPosition : new Vector2Int(-1, -1);
            GridSystem.Instance.SpawnKeyRandomly(playerCell, doorGridPosition);
        }
    }
 
    // Updates the optional text counter. Does nothing if it is not assigned.
    private void UpdateCoinCounter()
    {
        if (coinCounterText == null || GridSystem.Instance == null)
            return;
 
        string text = $"Coins: {CoinsCollected} / {TotalCoins}";
        text += $"\nKey: {KeysCollected} / {TotalKeys}";
 
        if (totalFlies > 0)
        {
            text += $"\nSpiders: {fliesShot} / {totalFlies}";
        }
 
        coinCounterText.text = text;
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