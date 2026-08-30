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

    private bool gameOver = false;

    public bool GameOver => gameOver;
    public Slot CurrentTurn { get; private set; }

    public bool IsPlayerTurn => IsSlotTurn(SlotForRole(AgentRole.Collector));

    private void Awake()
    {
        Instance = this;

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        CurrentTurn = firstTurn;
    }
    void Start()
    {
        UpdateCoinCounter();
    }

    public bool IsSlotTurn(Slot slot) => CurrentTurn == slot;

    // Which slot current hold a given role
    public Slot SlotForRole(AgentRole role) => agentARole == role ? Slot.A : Slot.B;

    // Call by human input or an AI agent controller
    public void EndTurn(Slot slot)
    {
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

        bool collectorWon = winningSlot == SlotForRole(AgentRole.Collector);
        string message = collectorWon ? "You Win!" : "You Lose!";
        string logMessage = collectorWon ? "Game Over - Player Wins!" : "Game Over - Player Loses!";
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
