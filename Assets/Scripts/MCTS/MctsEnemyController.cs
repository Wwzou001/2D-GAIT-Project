using UnityEngine;

public class MctsEnemyController : MonoBehaviour
{
    [SerializeField] private MCTSGameManager.Slot mySlot = MCTSGameManager.Slot.B;

    [SerializeField] private MCTSGridMover enemyMover;
    [SerializeField] private MCTSGridMover playerMover;

    [SerializeField] private int simulationsPerMove = 300;
    [SerializeField] private int rolloutDepth = 15;
    [SerializeField] private float moveInterval = 0.6f;
    [SerializeField] private bool logDecisions = true;

    public int SimulationsPerMove => simulationsPerMove;

    public double LastDecisionTimeMs => agent != null ? agent.LastDecisionTimeMs : 0;

    private MCSAgent agent;
    private float timer;

    private void Awake()
    {
        if (enemyMover == null)
            enemyMover = GetComponent<MCTSGridMover>();

        agent = new MCSAgent(simulationsPerMove, rolloutDepth);
    }

    // Set slider's min/max
    public void SetSimulationsPerMove(float value)
    {
        simulationsPerMove = Mathf.Max(0, Mathf.RoundToInt(value));
        agent = new MCSAgent(simulationsPerMove, rolloutDepth);
    }

    private void Update()
    {
        if (MCTSGameManager.Instance != null && MCTSGameManager.Instance.GameOver)
            return;

        // Not enemy turn yet, wait
        if (MCTSGameManager.Instance != null && !MCTSGameManager.Instance.IsSlotTurn(mySlot))
        {
            timer = 0f;
            return;
        }
       
        timer += Time.deltaTime;
        if (timer < moveInterval) return;   
        timer = 0f;

        Direction move = agent.ChooseMove(enemyMover.GridPosition, playerMover.GridPosition, out string log);

        if (logDecisions)
            Debug.Log(log);

        bool moved = enemyMover.TryMove(move);

        if (moved && MCTSGameManager.Instance != null)
        {
            MCTSGameManager.Instance.EndTurn(mySlot);
        }
    }
}
