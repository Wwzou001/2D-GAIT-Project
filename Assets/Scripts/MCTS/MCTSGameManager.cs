using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MCTSGameManager : MonoBehaviour
{
    public static MCTSGameManager Instance { get; private set; }

    public enum Slot { A, B }

    public enum AgentRole { Collector, Hunter }

    [SerializeField] private MCTSGridMover agentA;
    [SerializeField] private AgentRole agentARole = AgentRole.Collector;

    [SerializeField] private MCTSGridMover agentB;
    [SerializeField] private AgentRole agentBRole = AgentRole.Hunter;

    [SerializeField] private Slot firstTurn = Slot.A;

    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text coinCounterText;

    // Config screen support
    [SerializeField] private GameObject configPanel; // the P1/P2 dropdowns/sliders panel, shown until Start is pressed
    [SerializeField] private TMP_Text toggleButtonLabel; // reflect what pressing it will do next

    private bool gameStarted = false;
    private bool isPaused = false;

    public bool GameStarted => gameStarted;
    public bool IsPaused => isPaused;

    // True when turns/AI decisions should be able to happen
    private bool IsRunning => gameStarted && !isPaused;

    private bool gameOver = false;

    public bool GameOver => gameOver;
    public Slot CurrentTurn { get; private set; }

    public bool IsCollectorTurn => IsSlotTurn(SlotForRole(AgentRole.Collector));

    private void Awake()
    {
        Instance = this;

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        CurrentTurn = firstTurn;
        UpdateConfigVisibility();
        UpdateToggleButtonLabel();
    }
    void Start()
    {
        UpdateCoinCounter();
    }

    // Wire to config screen's "Start Game" button
    public void ToggleGameState()
    {
        if (!gameStarted)
        {
            gameStarted = true;
        }
        else
        {
            isPaused = !isPaused;
        }
        UpdateConfigVisibility();
        UpdateToggleButtonLabel();
    }

    private void UpdateConfigVisibility()
    {
        if (configPanel != null)
        {
            // Show whenever the match isn't actively running: before start, or when paused
            configPanel.SetActive(!IsRunning);
        }
    }

    public void UpdateToggleButtonLabel()
    {
        if (toggleButtonLabel == null) return;

        if (!gameStarted)
        {
            toggleButtonLabel.text = "Start Game";
        }
        else
        {
            toggleButtonLabel.text = isPaused ? "Resume" : "Pause";
        }
    }
    
    // No slot turn is active before the game has actually start
    public bool IsSlotTurn(Slot slot) => IsRunning && CurrentTurn == slot;

    // Which slot current hold a given role
    public Slot SlotForRole(AgentRole role) => agentARole == role ? Slot.A : Slot.B;

    // Call by human input or an AI agent controller
    public void EndTurn(Slot slot)
    {
        if (!gameStarted) return;
        if (CurrentTurn != slot) return; // ignore stale or out of turn call
        CurrentTurn = slot == Slot.A ? Slot.B : Slot.A;
    }

    public void CheckGameStatus()
    {
        if (GameOver)
        {
            return;
        }

        UpdateCoinCounter();

        // Win/Loss condition: two agent at same cell, hunter win, collector loss
        if (agentA != null && agentB != null && agentA.GridPosition == agentB.GridPosition)
        {
            Slot hunterSlot = SlotForRole(AgentRole.Hunter);
            EndGame(winningSlot: hunterSlot);
            return;
        }

        // Win condition
        if (GridSystem.Instance.RemainingCoins() == 0)
        {
            Slot collectorSlot = SlotForRole(AgentRole.Collector);
            EndGame(winningSlot: collectorSlot);
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

    private void EndGame(Slot winningSlot)
    {
        gameOver = true;

        AgentRole winningRole = winningSlot == SlotForRole(AgentRole.Hunter) ? AgentRole.Hunter : AgentRole.Collector;
        string message = winningRole == AgentRole.Hunter ? "Hunter Wins!" : "Collector Wins!";
        string logMessage = $"Game Over - {message}";
        Debug.Log(logMessage);

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text = message;
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
