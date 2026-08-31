using UnityEngine;

public class MctsEnemyController : MonoBehaviour
{
    public enum AlgorithmType { MCS, MCTS }

    [SerializeField] private MCTSGameManager.Slot mySlot = MCTSGameManager.Slot.B;

    [SerializeField] private AlgorithmType algorithm = AlgorithmType.MCTS;

    [SerializeField] private MCTSGridMover enemyMover;
    [SerializeField] private MCTSGridMover playerMover;

    [SerializeField] private int simulationsPerMove = 300;
    [SerializeField] private int rolloutDepth = 15;
    [SerializeField] private float moveInterval = 0.6f;
    [SerializeField] private bool logDecisions = true;

    public int SimulationsPerMove => simulationsPerMove;
    public AlgorithmType Algorithm => algorithm;

    public double LastDecisionTimeMs => algorithm == AlgorithmType.MCS
        ? (mcsAgent != null ? mcsAgent.LastDecisionTimeMs : 0)
        : (mctsAgent != null ? mctsAgent.LastDecisionTimeMs : 0);

    // Only one agent in use at a time based on algorithm
    private MCSAgent mcsAgent;
    private MCTSAgent mctsAgent;
    private float timer;

    // 1 move normal, 2 move when fountain buff active
    private int movesRemainingThisTurn = 0;

    private void Awake()
    {
        if (enemyMover == null)
            enemyMover = GetComponent<MCTSGridMover>();
    }

    private void Start()
    {
        BuildAgent();
    }

    private bool IsHunter()
    {
        if (MCTSGameManager.Instance == null) return true;
        return MCTSGameManager.Instance.SlotForRole(MCTSGameManager.AgentRole.Hunter) == mySlot;
    }

    private void BuildAgent()
    {
        bool isHunter = IsHunter();
        mcsAgent = new MCSAgent(simulationsPerMove, rolloutDepth, isHunter);
        mctsAgent = new MCTSAgent(simulationsPerMove, rolloutDepth, isHunter: isHunter);  
    }

    // Set slider's min/max
    public void SetSimulationsPerMove(float value)
    {
        simulationsPerMove = Mathf.Max(0, Mathf.RoundToInt(value));
        BuildAgent();
    }

    // Let UI or toggle switch algorithms at runtime
    public void SetAlgorithms(AlgorithmType newAlgorithms)
    {
        algorithm = newAlgorithms;
        BuildAgent();
    }

    private void Update()
    {
        if (MCTSGameManager.Instance != null && MCTSGameManager.Instance.GameOver)
            return;

        // Not enemy turn yet, wait
        if (MCTSGameManager.Instance != null && !MCTSGameManager.Instance.IsSlotTurn(mySlot))
        {
            timer = 0f;
            movesRemainingThisTurn = 0; // reset to check next turn buff fresh
            return;
        }

        timer += Time.deltaTime;
        if (timer < moveInterval) return;
        timer = 0f;

        // Start new turn
        if (movesRemainingThisTurn <= 0)
        {
            movesRemainingThisTurn = enemyMover.MoveDistance;
        }

        Direction move;
        string log;

        if (algorithm == AlgorithmType.MCS)
        {
            move = mcsAgent.ChooseMove(enemyMover.GridPosition, playerMover.GridPosition, out log);
        }
        else
        {
            move = mctsAgent.ChooseMove(enemyMover.GridPosition, playerMover.GridPosition, out log);
        }

        if (logDecisions)
            Debug.Log(log);

        bool moved = enemyMover.TryMove(move);

        // Only end turn when move succeed
        if (moved)
        {
            movesRemainingThisTurn--;
            if (movesRemainingThisTurn <= 0 && MCTSGameManager.Instance != null)
            {
                MCTSGameManager.Instance.EndTurn(mySlot);
            }
        }
    }
}
