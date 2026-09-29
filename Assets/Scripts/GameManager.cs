using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
 
// Runs the rules of the game:
//   Win:  pick up the key, then reach the door cell.
//   Lose: the enemy catches the player.
// It also counts flies and updates the HUD, and spawns the key
// once every fly has been shot.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
 
    [Header("Characters")]
    [SerializeField] private GridMover player;
    [SerializeField] private GridMover enemy;
 
    [Header("Door")]
    // The floor cell in front of the door. The player wins by standing here with the key.
    [SerializeField] private Vector2Int doorGridPosition;
 
    [Header("End screen")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
 
 /* old code for ui panel
    [Header("HUD icon rows (one picture per item)")]
    [SerializeField] private IconCounter coinCounter;
    [SerializeField] private IconCounter keyCounter;
    [SerializeField] private IconCounter fliesShotCounter;   // shows flies collected by shooting
 */
    // Other scripts (like PlayerShooting) check this so they stop once the game is over.
    public bool GameOver => gameOver;
 
    private bool gameOver = false;
    private bool hasKey = false;
 
    private int totalFlies = 0;            // how many flies there were at the start
    private int fliesShot = 0;             // flies hit by a flame, they count as collected
    private bool keySpawned = false;       // the key only appears once

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

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;
 
        if (endGamePanel != null)
            endGamePanel.SetActive(false);
    }
 
    private void Start()
    {
        // Count the flies once at the start so the HUD can show "shot / total".
        totalFlies = FindObjectsByType<FlyFSM>(FindObjectsSortMode.None).Length;
        if (enemy != null)
        {
            enemy.CanCollectKeys = false;
        }
 
        // Find out when the player picks up the key.
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected += HandleKeyCollected;
        }
 
    }
 
    private void OnDestroy()
    {
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.KeyCollected -= HandleKeyCollected;
        }
    }
 
    // Called by GridMover after every move.
    public void CheckGameState()
    {
        if (gameOver)
            return;
 
 
        // LOSS: the enemy is on the same cell as the player.
        if (player != null && enemy != null && player.GridPosition == enemy.GridPosition)
        {
            LoseGame();
            return;
        }
 
        // WIN: the player has the key and is standing on the door cell.
        if (player != null && hasKey && player.GridPosition == doorGridPosition)
        {
            WinGame();
        }
    }
 
    private void HandleKeyCollected(Vector2Int pos)
    {
        hasKey = true;
    }
 
    // Called by Projectile every time a flame hits a fly.
    // The fly counts as collected straight away.
    public void FlyShot()
    {
        fliesShot++;
 
        // When the last fly has been shot, the key appears on a random empty cell.
        if (!keySpawned && totalFlies > 0 && fliesShot >= totalFlies)
        {
            keySpawned = true;
 
            // Do not drop the key on the cell the player is standing on.
            Vector2Int avoid = player != null ? player.GridPosition : new Vector2Int(-1, -1);
            GridSystem.Instance.SpawnKeyRandomly(avoid);
        }
    }
 
 /* old code now swiched to GameHud.cs
    // Refreshes every icon row in the HUD. Each row can be left empty if you do not use it.
    private void UpdateCoinCounter()
    {
        if (GridSystem.Instance == null)
            return;
 
        if (coinCounter != null)
        {
            int totalCoins = GridSystem.Instance.TotalCoins;
            int collectedCoins = totalCoins - GridSystem.Instance.RemainingCoins();
            coinCounter.SetCount(collectedCoins, totalCoins);
        }
 
        if (keyCounter != null)
        {
            keyCounter.SetCount(GridSystem.Instance.KeysCollected, GridSystem.Instance.TotalKeys);
        }
 
        if (fliesShotCounter != null)
        {
            fliesShotCounter.SetCount(fliesShot, totalFlies);
        }
    }
*/
 
    private void WinGame()
    {
        gameOver = true;
        ShowEndScreen("YOU WIN!");
    }
 
    private void LoseGame()
    {
        gameOver = true;
        ShowEndScreen("YOU LOSE!");
    }
 
    private void ShowEndScreen(string message)
    {
        Time.timeScale = 0f;   // pause the game
 
        if (endGamePanel != null)
            endGamePanel.SetActive(true);
 
        if (resultText != null)
            resultText.text = message;
    }
 
    // Hook this up to the Restart button.
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}