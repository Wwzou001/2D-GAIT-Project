using UnityEngine;

public class MctsEnemyController : MonoBehaviour
{
    [SerializeField] private MCTSGridMover enemyMover;
    [SerializeField] private MCTSGridMover playerMover;

    [SerializeField] private int simulationsPerMove = 300;
    [SerializeField] private int rolloutDepth = 15;
    [SerializeField] private float moveInterval = 0.6f;
    [SerializeField] private bool logDecisions = true;

    [SerializeField] private bool stepMode = false;
    private bool advanceRequested = false;

    public bool StepMode => stepMode;

    public bool WaitingForStep => stepMode && IsEnemyTurn() && !advanceRequested;

    public int SimulationsPerMove => simulationsPerMove;

    private MctsAgent agent;
    private float timer;

    private void Awake()
    {
        if (enemyMover == null)
            enemyMover = GetComponent<MCTSGridMover>();

        agent = new MctsAgent(simulationsPerMove, rolloutDepth);
    }

    private bool IsEnemyTurn()
    {
        return MCTSGameManager.Instance == null || !MCTSGameManager.Instance.IsPlayerTurn;
    }

    public void RequestNextStep()
    {
        if (stepMode)
        {
            advanceRequested = true;
        }
    }

    // A UI toggle switch between auto and step by step at runtime
    public void SetStepMode(bool enable)
    {
        stepMode = enable;
        advanceRequested = false;
        timer = 0f;
    }

    // Set slider's min/max
    public void SetSimulationsPerMove(float value)
    {
        simulationsPerMove = Mathf.Max(1, Mathf.RoundToInt(value));
        agent = new MctsAgent(simulationsPerMove, rolloutDepth);
    }

    private void Update()
    {
        if (MCTSGameManager.Instance != null && MCTSGameManager.Instance.GameOver)
            return;

        // Not enemy turn yet, wait
        if (MCTSGameManager.Instance != null && MCTSGameManager.Instance.IsPlayerTurn)
        {
            timer = 0f;
            advanceRequested = false;
            return;
        }

        if (stepMode)
        {
            // Wait for a 'next step' click before doing anything
            if (!advanceRequested) return;
            advanceRequested = false;
        }
        else
        {
            // Automatic mode: Enemy turn
            timer += Time.deltaTime;
            if (timer < moveInterval) return;
            timer = 0f;
        }
        Direction move = agent.ChooseMove(enemyMover.GridPosition, playerMover.GridPosition, out string log);

        if (logDecisions)
            Debug.Log(log);

        bool moved = enemyMover.TryMove(move);

        if (moved && MCTSGameManager.Instance != null)
        {
            MCTSGameManager.Instance.EndEnemyTurn();
        }
    }
}
